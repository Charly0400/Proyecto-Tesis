using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Charly.FlightController {
    /// <summary>
    /// LÍMITE DEL MAPA con forma de cilindro: círculo horizontal + techo de altura.
    ///   Zona libre → Zona de advertencia (alarma + aviso + cuenta regresiva) → Límite duro.
    /// En el límite duro (o si se acaba la cuenta) la pantalla hace fade a negro, el avión se gira
    /// sobre el eje vertical conservando velocidad, altura y actitud (alabeo/cabeceo), se mete un poco
    /// hacia dentro del mapa y la pantalla vuelve. Si el piloto ya está regresando, la cuenta se pausa.
    /// No usa colliders: todo se calcula con la posición del avión.
    /// </summary>
    public class MapBoundary : MonoBehaviour {
        public enum TurnMode {
            [Tooltip("Gira exactamente 180° (sale por donde entró)")]
            Opuesto180,
            [Tooltip("Gira lo necesario para apuntar al centro del mapa")]
            HaciaElCentro
        }

        [Header("Avión")]
        [Tooltip("Rigidbody del avión. Si se deja vacío se busca el FlightControllerV3 de la escena")]
        [SerializeField] private Rigidbody aircraft;

        [Header("Forma del límite (clic derecho en el componente → 'Ajustar al terreno')")]
        [Tooltip("Centro del círculo en el mundo (X, Z)")]
        [SerializeField] private Vector2 center = Vector2.zero;
        [Tooltip("Radio del límite duro en metros")]
        [SerializeField] private float hardRadius = 2000f;
        [Tooltip("A qué fracción del radio empieza la zona de advertencia")]
        [Range(0.5f, 0.98f)]
        [SerializeField] private float warningFraction = 0.8f;
        [Tooltip("Al ajustar al terreno: metros que el límite duro queda dentro del borde del terreno con colisión")]
        [SerializeField] private float edgeMargin = 300f;
        [Tooltip("Altitud máxima (Y en el mundo) del límite duro")]
        [SerializeField] private float ceilingAltitude = 3000f;
        [Tooltip("Metros por debajo del techo donde empieza la advertencia de altitud")]
        [SerializeField] private float ceilingWarningMargin = 300f;

        [Header("Advertencia")]
        [Tooltip("Segundos que tiene el piloto para regresar antes del fade")]
        [SerializeField] private float warningTime = 10f;
        [Tooltip("Qué tanto debe apuntar hacia dentro para contar como 'regresando' (0 = cualquier giro hacia dentro, 1 = directo al centro)")]
        [Range(0f, 1f)]
        [SerializeField] private float returningThreshold = 0.2f;
        [Tooltip("Por debajo de esta velocidad (m/s) la cuenta no corre (p. ej. avión parado en tierra)")]
        [SerializeField] private float minSpeedForCountdown = 5f;

        [Header("Regreso (fade + giro)")]
        [SerializeField] private TurnMode turnMode = TurnMode.Opuesto180;
        [SerializeField] private float fadeOutTime = 0.4f;
        [SerializeField] private float blackHoldTime = 0.3f;
        [SerializeField] private float fadeInTime = 0.5f;
        [SerializeField] private Color fadeColor = Color.black;
        [Tooltip("Cuánto baja el volumen general durante el negro (0 = nada, 1 = silencio)")]
        [Range(0f, 1f)]
        [SerializeField] private float audioDuck = 0.8f;
        [Tooltip("Metros que el avión queda dentro de la zona libre después del giro")]
        [SerializeField] private float pushInsideDistance = 150f;
        [Tooltip("Segundos sin advertencias después de regresar")]
        [SerializeField] private float graceTime = 3f;
        [Tooltip("Altura mínima sobre el suelo al reaparecer (si venía volando más alto que esto)")]
        [SerializeField] private float minGroundClearance = 60f;
        [Tooltip("Capas que cuentan como suelo para la altura mínima")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Cámara")]
        [Tooltip("Cámara del visor. Si se deja vacío se usa Camera.main")]
        [SerializeField] private Camera headCamera;

        [Header("Alarma")]
        [Tooltip("Si se deja vacío se crea uno 2D en este GameObject")]
        [SerializeField] private AudioSource alarmSource;
        [Tooltip("Sonido en loop. Si se deja vacío se genera un 'bip-bip' simple")]
        [SerializeField] private AudioClip alarmClip;
        [Range(0f, 1f)]
        [SerializeField] private float alarmVolume = 0.5f;

        [Header("Aviso en pantalla")]
        [Tooltip("Texto propio (p. ej. en el panel de la cabina). Si se deja vacío y 'Crear aviso' está activo, se crea uno frente a la vista")]
        [SerializeField] private TMP_Text warningText;
        [SerializeField] private bool createDefaultHud = true;
        [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.15f);
        [SerializeField] private Color returningColor = new Color(1f, 0.8f, 0.2f);

        [Header("Eventos")]
        public UnityEvent onWarningStarted;
        public UnityEvent onWarningEnded;
        public UnityEvent onReturnedToMap;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;

        // ───────── Estado (lectura para otros scripts / UI) ─────────
        public bool IsWarning => m_InWarning;
        public bool IsReturning => m_Returning;
        public bool IsRelocating => m_Relocating;
        public float TimeLeft => m_TimeLeft;
        public float WarningRadius => hardRadius * warningFraction;
        public float CeilingWarningAltitude => ceilingAltitude - ceilingWarningMargin;

        private bool m_InWarning;
        private bool m_Returning;
        private bool m_Relocating;
        private float m_TimeLeft;
        private float m_GraceEndTime;

        private Image m_FadeImage;
        private GameObject m_FadeRoot;
        private GameObject m_HudRoot;

        // Material de UI que se dibuja encima de todo (ignora profundidad): la cabina no tapa el fade ni el aviso
        private const int OverlayQueue = 4000;
        private static readonly int GuiZTestMode = Shader.PropertyToID("unity_GUIZTestMode");

        #region Unity Methods
        private void Reset() {
            FitToTerrain();
        }

        private void Start() {
            if (aircraft == null) {
                var fc = FindFirstObjectByType<FlightControllerV3>();
                if (fc != null) aircraft = fc.GetComponent<Rigidbody>();
            }
            if (aircraft == null) {
                Debug.LogError("MapBoundary: no encontré el Rigidbody del avión. Asígnalo en el Inspector.", this);
                enabled = false;
                return;
            }

            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null)
                Debug.LogWarning("MapBoundary: no hay cámara (Camera.main). No habrá fade ni aviso en pantalla.", this);

            SetupAlarm();
            SetupFade();
            SetupHud();

            m_TimeLeft = warningTime;
        }

        private void Update() {
            if (m_Relocating) return;

            Vector3 pos = aircraft.position;
            Vector2 offset = new Vector2(pos.x, pos.z) - center;
            float dist = offset.magnitude;

            // Límite duro: regreso inmediato
            if (dist >= hardRadius || pos.y >= ceilingAltitude) {
                StartCoroutine(Relocate());
                return;
            }

            bool outsideHorizontal = dist > WarningRadius;
            bool outsideCeiling = pos.y > CeilingWarningAltitude;
            bool inWarning = (outsideHorizontal || outsideCeiling) && Time.time >= m_GraceEndTime;

            if (inWarning && !m_InWarning) BeginWarning();
            else if (!inWarning && m_InWarning) EndWarning();

            if (!m_InWarning) return;

            m_Returning = IsHeadingBack(offset, dist, outsideHorizontal, outsideCeiling);
            if (!m_Returning) m_TimeLeft -= Time.deltaTime;

            UpdateAlarm();
            UpdateHud(outsideHorizontal, outsideCeiling);

            if (m_TimeLeft <= 0f) StartCoroutine(Relocate());
        }
        #endregion

        #region Advertencia
        /// <summary>True si el avión ya va de regreso (o va tan lento que no tiene caso contar).</summary>
        private bool IsHeadingBack(Vector2 offset, float dist, bool outsideHorizontal, bool outsideCeiling) {
            Vector3 v = aircraft.linearVelocity;
            if (v.magnitude < minSpeedForCountdown) return true;

            bool back = true;
            if (outsideHorizontal) {
                Vector2 flatVel = new Vector2(v.x, v.z);
                Vector2 inward = -offset / Mathf.Max(dist, 0.01f);
                back &= flatVel.sqrMagnitude > 1f && Vector2.Dot(flatVel.normalized, inward) > returningThreshold;
            }
            if (outsideCeiling)
                back &= v.y < -1f;
            return back;
        }

        private void BeginWarning() {
            m_InWarning = true;
            m_TimeLeft = warningTime;
            if (logEvents) Debug.Log("[MapBoundary] Fuera del área de operación: advertencia iniciada", this);
            onWarningStarted?.Invoke();
        }

        private void EndWarning() {
            m_InWarning = false;
            m_Returning = false;
            m_TimeLeft = warningTime;
            StopAlarm();
            SetHudVisible(false);
            if (logEvents) Debug.Log("[MapBoundary] De vuelta en el área de operación", this);
            onWarningEnded?.Invoke();
        }
        #endregion

        #region Regreso al mapa
        private IEnumerator Relocate() {
            m_Relocating = true;
            m_InWarning = false;
            m_Returning = false;
            StopAlarm();
            SetHudVisible(false);

            float listenerVolume = AudioListener.volume;

            yield return Fade(0f, 1f, fadeOutTime, listenerVolume);
            TurnAndMoveInside();
            if (blackHoldTime > 0f) yield return new WaitForSeconds(blackHoldTime);
            yield return Fade(1f, 0f, fadeInTime, listenerVolume);

            AudioListener.volume = listenerVolume;
            m_TimeLeft = warningTime;
            m_GraceEndTime = Time.time + graceTime;
            m_Relocating = false;

            if (logEvents) Debug.Log("[MapBoundary] Avión regresado al mapa", this);
            onReturnedToMap?.Invoke();
        }

        /// <summary>
        /// Gira el avión (posición, rotación, velocidad y velocidad angular) sobre el eje vertical del mundo
        /// y lo mete dentro del mapa. El XR Origin es hijo del asiento, así que gira con él.
        /// </summary>
        private void TurnAndMoveInside() {
            Vector3 pos = aircraft.position;
            Quaternion rot = aircraft.rotation;
            Vector3 vel = aircraft.linearVelocity;
            Vector3 angVel = aircraft.angularVelocity;

            float groundBefore = GroundHeight(pos);
            float heightAboveGround = pos.y - groundBefore;

            Vector2 offset = new Vector2(pos.x, pos.z) - center;
            float dist = offset.magnitude;
            Quaternion turn = Quaternion.identity;

            // Techo: nivela la trayectoria (pitch alrededor del eje lateral de la trayectoria) y baja el avión
            if (pos.y > CeilingWarningAltitude) {
                if (vel.y > 0f) {
                    Vector3 dir = vel.sqrMagnitude > 1f ? vel : rot * Vector3.forward;
                    Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
                    if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(rot * Vector3.up, Vector3.up);
                    if (flat.sqrMagnitude > 0.0001f)
                        turn = Quaternion.FromToRotation(dir.normalized, flat.normalized);
                }
                pos.y = Mathf.Min(pos.y, CeilingWarningAltitude - pushInsideDistance);
            }

            // Círculo: giro sobre el eje vertical y empuje hacia dentro
            if (dist > WarningRadius) {
                Vector2 outward = dist > 0.01f ? offset / dist : Vector2.up;

                Quaternion yaw;
                if (turnMode == TurnMode.Opuesto180) {
                    yaw = Quaternion.AngleAxis(180f, Vector3.up);
                }
                else {
                    Vector3 heading = Vector3.ProjectOnPlane(turn * (vel.sqrMagnitude > 1f ? vel : rot * Vector3.forward), Vector3.up);
                    Vector3 inward = new Vector3(-outward.x, 0f, -outward.y);
                    yaw = Quaternion.AngleAxis(Vector3.SignedAngle(heading, inward, Vector3.up), Vector3.up);
                }
                turn = yaw * turn;

                Vector2 inside = center + outward * Mathf.Max(0f, WarningRadius - pushInsideDistance);
                pos.x = inside.x;
                pos.z = inside.y;
            }

            // No reaparecer dentro de una montaña. Si venía rodando o muy bajo, conserva su altura sobre el suelo.
            float groundAfter = GroundHeight(pos);
            if (!float.IsNegativeInfinity(groundAfter)) {
                float clearance = float.IsNegativeInfinity(groundBefore) ? minGroundClearance : Mathf.Min(heightAboveGround, minGroundClearance);
                pos.y = Mathf.Max(pos.y, groundAfter + clearance);
            }

            Quaternion newRot = turn * rot;
            aircraft.transform.SetPositionAndRotation(pos, newRot);
            aircraft.position = pos;
            aircraft.rotation = newRot;
            aircraft.linearVelocity = turn * vel;
            aircraft.angularVelocity = turn * angVel;
            Physics.SyncTransforms();
        }

        /// <summary>Altura del suelo bajo un punto (ignora el propio avión). -Infinito si no hay suelo.</summary>
        private float GroundHeight(Vector3 point) {
            Vector3 origin = new Vector3(point.x, ceilingAltitude + 5000f, point.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, ceilingAltitude + 10000f, groundLayers, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var hit in hits) {
                if (hit.collider.attachedRigidbody == aircraft || hit.collider.transform.IsChildOf(aircraft.transform)) continue;
                best = Mathf.Max(best, hit.point.y);
            }
            return best;
        }
        #endregion

        #region Fade
        private void SetupFade() {
            if (headCamera == null) return;

            // Canvas en el mundo pegado a la cámara: funciona en ambos ojos del visor
            float distance = Mathf.Max(headCamera.nearClipPlane * 1.5f, 0.05f);
            m_FadeRoot = CreateCameraCanvas("BoundaryFade", distance, 100f, 100f, distance * 12f / 100f, 32767);

            var imageGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            imageGo.layer = m_FadeRoot.layer;
            imageGo.transform.SetParent(m_FadeRoot.transform, false);
            var rt = (RectTransform)imageGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            m_FadeImage = imageGo.GetComponent<Image>();
            m_FadeImage.raycastTarget = false;
            m_FadeImage.material = CreateOverlayMaterial(Canvas.GetDefaultCanvasMaterial());
            SetFadeAlpha(0f);
        }

        private IEnumerator Fade(float from, float to, float duration, float listenerVolume) {
            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                SetFadeAlpha(a);
                AudioListener.volume = listenerVolume * (1f - audioDuck * a);
                yield return null;
            }
            SetFadeAlpha(to);
            AudioListener.volume = listenerVolume * (1f - audioDuck * to);
        }

        private void SetFadeAlpha(float alpha) {
            if (m_FadeImage == null) return;
            Color c = fadeColor;
            c.a = alpha;
            m_FadeImage.color = c;
            m_FadeRoot.SetActive(alpha > 0.001f);
        }
        #endregion

        #region Alarma
        private void SetupAlarm() {
            if (alarmSource == null) {
                alarmSource = gameObject.AddComponent<AudioSource>();
                alarmSource.spatialBlend = 0f; // 2D: suena "dentro de la cabina"
            }
            alarmSource.playOnAwake = false;
            alarmSource.loop = true;
            alarmSource.volume = alarmVolume;
            alarmSource.clip = alarmClip != null ? alarmClip : CreateBeepClip();
        }

        /// <summary>Suena mientras está en advertencia y NO va de regreso.</summary>
        private void UpdateAlarm() {
            if (alarmSource == null) return;
            bool shouldPlay = m_InWarning && !m_Returning;
            if (shouldPlay && !alarmSource.isPlaying) alarmSource.Play();
            else if (!shouldPlay && alarmSource.isPlaying) alarmSource.Stop();
        }

        private void StopAlarm() {
            if (alarmSource != null && alarmSource.isPlaying) alarmSource.Stop();
        }

        /// <summary>"Bip-bip" de 1 s (dos tonos de 880 Hz) para probar sin tener un sonido propio.</summary>
        private static AudioClip CreateBeepClip() {
            const int sampleRate = 44100;
            const float frequency = 880f;
            const float beepLength = 0.15f;
            var data = new float[sampleRate];
            float[] starts = { 0f, 0.25f };

            foreach (float start in starts) {
                int from = (int)(start * sampleRate);
                int count = (int)(beepLength * sampleRate);
                for (int i = 0; i < count; i++) {
                    float t = i / (float)sampleRate;
                    float envelope = Mathf.Min(1f, Mathf.Min(i, count - i) / (0.005f * sampleRate)); // sin clics
                    data[from + i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.6f;
                }
            }

            var clip = AudioClip.Create("AlarmaLimiteMapa", sampleRate, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion

        #region Aviso en pantalla
        private void SetupHud() {
            if (warningText != null) {
                m_HudRoot = warningText.gameObject;
            }
            else if (createDefaultHud && headCamera != null) {
                // 1 unidad del canvas = 1 mm. Panel de 70 x 16 cm a 0.9 m, un poco arriba del centro de la vista
                m_HudRoot = CreateCameraCanvas("BoundaryHud", 0.9f, 700f, 160f, 0.001f, 32000);
                m_HudRoot.transform.localPosition = new Vector3(0f, 0.08f, 0.9f);

                var textGo = new GameObject("Aviso", typeof(RectTransform));
                textGo.layer = m_HudRoot.layer;
                textGo.transform.SetParent(m_HudRoot.transform, false);
                var rt = (RectTransform)textGo.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 42f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.raycastTarget = false;
                if (tmp.font == null)
                    Debug.LogWarning("MapBoundary: TextMeshPro no tiene fuente por defecto; el aviso no se verá.", this);
                else
                    tmp.fontMaterial = CreateOverlayMaterial(tmp.fontMaterial);
                warningText = tmp;
            }
            SetHudVisible(false);
        }

        private void UpdateHud(bool outsideHorizontal, bool outsideCeiling) {
            if (warningText == null) return;
            SetHudVisible(true);

            string title = outsideHorizontal ? "FUERA DEL ÁREA DE OPERACIÓN" : "ALTITUD MÁXIMA";
            string action;
            if (m_Returning)
                action = "Regresando...";
            else if (outsideHorizontal && outsideCeiling)
                action = $"Gire de regreso y descienda  {Mathf.CeilToInt(m_TimeLeft)}";
            else if (outsideHorizontal)
                action = $"Gire de regreso  {Mathf.CeilToInt(m_TimeLeft)}";
            else
                action = $"Descienda  {Mathf.CeilToInt(m_TimeLeft)}";

            warningText.text = $"{title}\n<size=75%>{action}</size>";
            warningText.color = m_Returning ? returningColor : warningColor;
        }

        private void SetHudVisible(bool visible) {
            if (m_HudRoot != null && m_HudRoot.activeSelf != visible) m_HudRoot.SetActive(visible);
        }
        #endregion

        #region Utilidades
        /// <summary>Canvas World Space hijo de la cámara, sin raycaster (las manos XR no lo pueden tocar).</summary>
        private GameObject CreateCameraCanvas(string name, float distance, float width, float height, float scale, int sortingOrder) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.layer = headCamera.gameObject.layer;
            go.transform.SetParent(headCamera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = headCamera;
            canvas.sortingOrder = sortingOrder;
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
            return go;
        }

        private static Material CreateOverlayMaterial(Material source) {
            var mat = new Material(source);
            mat.SetInt(GuiZTestMode, (int)CompareFunction.Always);
            mat.renderQueue = OverlayQueue;
            return mat;
        }

        /// <summary>
        /// Centra el círculo en los Terrain CON colisión (las orillas decorativas no tienen) y usa el lado
        /// más corto menos 'edgeMargin' como radio. El techo queda sobre la montaña más alta.
        /// </summary>
        [ContextMenu("Ajustar al terreno")]
        public void FitToTerrain() {
            bool any = false;
            Bounds bounds = default;
            foreach (var t in FindObjectsByType<Terrain>(FindObjectsSortMode.None)) {
                if (t.terrainData == null) continue;
                var col = t.GetComponent<TerrainCollider>();
                if (col == null || !col.enabled) continue;

                Vector3 size = t.terrainData.size;
                var b = new Bounds(t.transform.position + size * 0.5f, size);
                if (!any) { bounds = b; any = true; }
                else bounds.Encapsulate(b);
            }

            if (!any) {
                Debug.LogWarning("MapBoundary: no encontré Terrain con TerrainCollider para ajustar el límite.", this);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Ajustar límite al terreno");
#endif
            center = new Vector2(bounds.center.x, bounds.center.z);
            hardRadius = Mathf.Max(100f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f - edgeMargin);
            ceilingAltitude = Mathf.Max(ceilingAltitude, bounds.max.y + ceilingWarningMargin + 500f);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"MapBoundary: centro ({center.x:F0}, {center.y:F0}), radio {hardRadius:F0} m, " +
                      $"advertencia a {WarningRadius:F0} m, techo {ceilingAltitude:F0} m", this);
        }

        private void OnDrawGizmosSelected() {
            float baseY = aircraft != null ? Mathf.Min(0f, aircraft.position.y) : 0f;
            Gizmos.color = Color.yellow;
            DrawCircle(WarningRadius, baseY);
            DrawCircle(WarningRadius, CeilingWarningAltitude);
            Gizmos.color = Color.red;
            DrawCircle(hardRadius, baseY);
            DrawCircle(hardRadius, ceilingAltitude);

            // Paredes del cilindro
            for (int i = 0; i < 8; i++) {
                float a = i * Mathf.PI * 0.25f;
                var p = new Vector3(center.x + Mathf.Cos(a) * hardRadius, 0f, center.y + Mathf.Sin(a) * hardRadius);
                Gizmos.DrawLine(new Vector3(p.x, baseY, p.z), new Vector3(p.x, ceilingAltitude, p.z));
            }
        }

        private void DrawCircle(float radius, float y) {
            const int segments = 128;
            Vector3 prev = new Vector3(center.x + radius, y, center.y);
            for (int i = 1; i <= segments; i++) {
                float a = i / (float)segments * Mathf.PI * 2f;
                var next = new Vector3(center.x + Mathf.Cos(a) * radius, y, center.y + Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
        #endregion
    }
}
