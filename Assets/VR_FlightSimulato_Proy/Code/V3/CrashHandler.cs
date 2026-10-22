using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Charly.FlightController {
    /// <summary>
    /// CHOQUES del avión, pensado para VR (nada de sacudir la cámara ni dejar que el avión dé vueltas con el piloto dentro).
    ///   Contacto con el tren de aterrizaje → se tolera si la velocidad vertical y la inclinación son razonables.
    ///   Contacto con fuselaje/alas/obstáculos → se tolera solo a muy baja velocidad (rozar al rodar).
    /// Al chocar: el avión se congela, sonido de impacto, fade a negro, mensaje con la causa,
    /// y el avión reaparece en la pista con el motor apagado.
    /// Va en el mismo GameObject que el Rigidbody (ahí llegan las colisiones de todos los colliders hijos).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CrashHandler : MonoBehaviour {
        /// <summary>Datos de un choque (para registrar resultados).</summary>
        [Serializable]
        public struct CrashInfo {
            public string reason;
            public float time;                 // segundos desde que empezó la escena
            public float flightTime;           // segundos desde la última reaparición
            public Vector3 position;
            public float speedKmh;             // velocidad del avión justo antes del impacto
            public float impactSpeedMps;       // velocidad relativa total contra lo que golpeó
            public float sinkRateMps;          // componente de esa velocidad perpendicular a la superficie
            public float rollDeg;              // alabeo (+ = ala derecha abajo)
            public float tiltDeg;              // inclinación respecto a la actitud en reposo sobre la pista
            public string otherCollider;
            public string ownCollider;
        }

        [Header("Referencias (vacías = se buscan solas)")]
        [SerializeField] private FlightControllerV3 flightController;
        [Tooltip("Para pausar el límite del mapa durante el choque")]
        [SerializeField] private MapBoundary mapBoundary;
        [Tooltip("Cámara del visor. Si se deja vacío se usa Camera.main")]
        [SerializeField] private Camera headCamera;

        [Header("Tren de aterrizaje")]
        [Tooltip("Colliders de las ruedas. Si se deja VACÍO, cualquier contacto contra el suelo con el avión derecho " +
                 "cuenta como tren de aterrizaje (más tolerante). Si se llena, solo esos colliders pueden aterrizar")]
        [SerializeField] private List<Collider> landingGear = new List<Collider>();
        [Tooltip("Qué tan horizontal debe ser la superficie para contar como suelo (normal.y mínima)")]
        [Range(0f, 1f)]
        [SerializeField] private float minGroundNormalY = 0.7f;

        [Header("Aterrizaje tolerante")]
        [Tooltip("Velocidad vertical máxima (m/s) al tocar con el tren. Aterrizaje real ~1-3 m/s; muy duro ~4-5")]
        [SerializeField] private float maxLandingSinkRate = 6f;
        [Tooltip("Alabeo máximo (grados) al tocar con el tren")]
        [SerializeField] private float maxLandingRollDeg = 25f;
        [Tooltip("Inclinación máxima (grados) respecto a cómo descansa el avión en la pista (cubre morro muy abajo o muy arriba)")]
        [SerializeField] private float maxLandingTiltDeg = 30f;

        [Header("Golpes de fuselaje / alas / obstáculos")]
        [Tooltip("Velocidad relativa (m/s) a partir de la cual un golpe que NO es del tren cuenta como choque. ~10 m/s = 36 km/h")]
        [SerializeField] private float maxBodyImpactSpeed = 10f;
        [Tooltip("Inclinación (grados) tocando el suelo a partir de la cual el avión se considera volcado")]
        [SerializeField] private float flippedTiltDeg = 75f;

        [Header("Reaparición")]
        [Tooltip("Dónde reaparece. Si se deja vacío, donde estaba el avión al empezar la escena")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float fadeOutTime = 0.25f;
        [Tooltip("Segundos en negro mostrando el mensaje antes de reaparecer")]
        [SerializeField] private float messageTime = 3f;
        [SerializeField] private float fadeInTime = 0.6f;
        [SerializeField] private Color fadeColor = Color.black;
        [Tooltip("Cuánto baja el volumen general durante el negro (0 = nada, 1 = silencio)")]
        [Range(0f, 1f)]
        [SerializeField] private float audioDuck = 1f;
        [Tooltip("Segundos sin detectar choques después de reaparecer")]
        [SerializeField] private float graceTime = 1.5f;

        [Header("Sonido de impacto")]
        [Tooltip("Si se deja vacío se genera un golpe simple")]
        [SerializeField] private AudioClip impactClip;
        [Range(0f, 1f)]
        [SerializeField] private float impactVolume = 0.8f;

        [Header("Eventos")]
        public UnityEvent onCrashed;
        public UnityEvent onRespawned;
        /// <summary>Para registrar resultados desde código (p. ej. guardar en CSV).</summary>
        public event Action<CrashInfo> Crashed;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;

        // ───────── Estado (lectura para otros scripts / UI) ─────────
        public bool IsCrashed => m_Crashed;
        public int CrashCount { get; private set; }
        public CrashInfo LastCrash { get; private set; }

        private Rigidbody m_Rigidbody;
        private Vector3 m_SpawnPosition;
        private Quaternion m_SpawnRotation;
        private Vector3 m_RestLocalUp;          // "arriba" del avión descansando en la pista, en ejes locales
        private Vector3 m_LastVelocity;         // velocidad del paso de física anterior (antes de resolver el choque)
        private float m_GraceEndTime;
        private float m_FlightStartTime;
        private bool m_Crashed;
        private bool m_BoundaryWasEnabled;
        private CollisionDetectionMode m_CollisionMode;
        private float m_ListenerVolume = 1f;

        private OverlayFade m_Fade;
        private TextMeshProUGUI m_Message;
        private AudioSource m_ImpactSource;

        #region Unity Methods
        private void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
            if (flightController == null) flightController = GetComponent<FlightControllerV3>();
        }

        private void Start() {
            if (mapBoundary == null) mapBoundary = FindFirstObjectByType<MapBoundary>();
            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null)
                Debug.LogWarning("CrashHandler: no hay cámara (Camera.main). No habrá fade ni mensaje.", this);

            m_SpawnPosition = spawnPoint != null ? spawnPoint.position : m_Rigidbody.position;
            m_SpawnRotation = spawnPoint != null ? spawnPoint.rotation : m_Rigidbody.rotation;
            // Se asume que la escena empieza con el avión descansando sobre la pista
            m_RestLocalUp = Quaternion.Inverse(m_SpawnRotation) * Vector3.up;

            m_Fade = OverlayFade.Create(headCamera, "CrashFade", fadeColor, 32000);
            if (headCamera != null)
                m_Message = VROverlay.CreateText(headCamera, "CrashMessage", new Vector3(0f, 0f, 0.9f),
                                                 new Vector2(800f, 260f), 48f, 32767, this);

            m_ImpactSource = gameObject.AddComponent<AudioSource>();
            m_ImpactSource.playOnAwake = false;
            m_ImpactSource.spatialBlend = 0f; // 2D: suena "dentro de la cabina"
            m_ImpactSource.ignoreListenerVolume = true; // el fade baja todo lo demás, pero el golpe suena completo
            m_ImpactSource.clip = impactClip != null ? impactClip : CreateImpactClip();

            m_GraceEndTime = Time.time + graceTime;
            m_FlightStartTime = Time.time;
        }

        private void FixedUpdate() {
            if (!m_Crashed) m_LastVelocity = m_Rigidbody.linearVelocity;
        }

        private void OnCollisionEnter(Collision collision) {
            if (!CanCrash(collision)) return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 relVel = collision.relativeVelocity;
            float impactSpeed = relVel.magnitude;
            float sinkRate = Mathf.Abs(Vector3.Dot(relVel, contact.normal));
            float roll = RollDeg();
            float tilt = TiltDeg();

            string reason = null;
            if (IsGearContact(collision)) {
                if (sinkRate > maxLandingSinkRate)
                    reason = $"Aterrizaje demasiado duro ({sinkRate:F1} m/s de descenso, máx. {maxLandingSinkRate:F0})";
                else if (Mathf.Abs(roll) > maxLandingRollDeg)
                    reason = $"Aterrizaje con las alas inclinadas ({Mathf.Abs(roll):F0}°)";
                else if (tilt > maxLandingTiltDeg)
                    reason = $"Aterrizaje con el morro en mal ángulo ({tilt:F0}°)";
            }
            else if (impactSpeed > maxBodyImpactSpeed) {
                bool ground = Mathf.Abs(contact.normal.y) >= minGroundNormalY;
                reason = ground ? "El fuselaje o un ala golpeó el suelo" : $"Choque contra '{collision.collider.name}'";
            }

            if (reason != null) Crash(reason, collision, impactSpeed, sinkRate);
        }

        // Un toque suave puede terminar con el avión volcado (p. ej. capotar): eso también es choque
        private void OnCollisionStay(Collision collision) {
            if (!CanCrash(collision)) return;
            if (TiltDeg() > flippedTiltDeg && Mathf.Abs(collision.GetContact(0).normal.y) >= minGroundNormalY)
                Crash("El avión se volcó", collision, collision.relativeVelocity.magnitude, 0f);
        }
        #endregion

        #region Detección
        private bool CanCrash(Collision collision) {
            if (m_Crashed || Time.time < m_GraceEndTime || collision.contactCount == 0) return false;
            // Piezas del propio avión (palancas, switches, rig XR sentado) nunca cuentan como choque.
            // Si llegan a chocar es porque se perdió su IgnoreCollision; AircraftPhysics lo vuelve a aplicar.
            if (collision.collider != null && collision.collider.transform.IsChildOf(transform)) return false;
            // Durante el regreso del límite del mapa el avión se teletransporta: no contar eso
            return mapBoundary == null || !mapBoundary.IsRelocating;
        }

        /// <summary>True si todo el contacto es del tren de aterrizaje contra el suelo.</summary>
        private bool IsGearContact(Collision collision) {
            for (int i = 0; i < collision.contactCount; i++) {
                ContactPoint c = collision.GetContact(i);
                if (Mathf.Abs(c.normal.y) < minGroundNormalY) return false;
                if (landingGear.Count > 0 && !landingGear.Contains(c.thisCollider)) return false;
            }
            // Sin lista de ruedas: muy inclinado = tocó con un ala o el lomo, no con las ruedas.
            // Un poco inclinado sí cuenta como aterrizaje, para que el mensaje diga "alas inclinadas" / "mal ángulo".
            return landingGear.Count > 0 || TiltDeg() <= maxLandingTiltDeg + maxLandingRollDeg;
        }

        /// <summary>Alabeo en grados: + = ala derecha abajo. No depende del ángulo de cabeceo del modelo.</summary>
        private float RollDeg() {
            return -Mathf.Asin(Mathf.Clamp(transform.right.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Grados entre el "arriba" del avión ahora y su "arriba" descansando en la pista.</summary>
        private float TiltDeg() {
            return Vector3.Angle(transform.rotation * m_RestLocalUp, Vector3.up);
        }
        #endregion

        #region Choque y reaparición
        private void Crash(string reason, Collision collision, float impactSpeed, float sinkRate) {
            m_Crashed = true;
            CrashCount++;

            var info = new CrashInfo {
                reason = reason,
                time = Time.time,
                flightTime = Time.time - m_FlightStartTime,
                position = m_Rigidbody.position,
                speedKmh = m_LastVelocity.magnitude * 3.6f,
                impactSpeedMps = impactSpeed,
                sinkRateMps = sinkRate,
                rollDeg = RollDeg(),
                tiltDeg = TiltDeg(),
                otherCollider = collision.collider != null ? collision.collider.name : "?",
                ownCollider = collision.contactCount > 0 ? collision.GetContact(0).thisCollider.name : "?"
            };
            LastCrash = info;

            if (logEvents)
                Debug.Log($"[Crash] #{CrashCount} {info.reason} | {info.speedKmh:F0} km/h, impacto {info.impactSpeedMps:F1} m/s, " +
                          $"descenso {info.sinkRateMps:F1} m/s, alabeo {info.rollDeg:F0}°, inclinación {info.tiltDeg:F0}° | " +
                          $"'{info.ownCollider}' contra '{info.otherCollider}' | vuelo {info.flightTime:F0} s", this);

            // Congelar: en VR es mucho mejor que dejar al avión rodar o rebotar con el piloto dentro
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            // Un Rigidbody cinemático solo admite CCD especulativo (si no, Unity avisa en consola)
            m_CollisionMode = m_Rigidbody.collisionDetectionMode;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            m_Rigidbody.isKinematic = true;
            if (flightController != null) flightController.StopEngine();

            if (mapBoundary != null) {
                m_BoundaryWasEnabled = mapBoundary.enabled;
                mapBoundary.enabled = false;
            }

            if (m_ImpactSource != null) m_ImpactSource.PlayOneShot(m_ImpactSource.clip, impactVolume);

            onCrashed?.Invoke();
            Crashed?.Invoke(info);

            StartCoroutine(CrashSequence(info));
        }

        private IEnumerator CrashSequence(CrashInfo info) {
            m_ListenerVolume = AudioListener.volume;
            yield return Fade(0f, 1f, fadeOutTime);

            for (float left = messageTime; left > 0f; left -= Time.deltaTime) {
                ShowMessage($"IMPACTO\n<size=55%>{info.reason}\n{info.speedKmh:F0} km/h\n\nReapareciendo en {Mathf.CeilToInt(left)}</size>");
                yield return null;
            }
            HideMessage();

            Respawn();
            yield return Fade(1f, 0f, fadeInTime);
            AudioListener.volume = m_ListenerVolume;

            if (logEvents) Debug.Log("[Crash] Avión de vuelta en la pista", this);
            onRespawned?.Invoke();
        }

        /// <summary>Pone el avión en la pista, quieto y con el motor apagado. El XR Origin es hijo del asiento, así que viaja con él.</summary>
        private void Respawn() {
            transform.SetPositionAndRotation(m_SpawnPosition, m_SpawnRotation);
            m_Rigidbody.position = m_SpawnPosition;
            m_Rigidbody.rotation = m_SpawnRotation;
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.collisionDetectionMode = m_CollisionMode;
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();

            if (flightController != null) flightController.StopEngine();
            if (mapBoundary != null && m_BoundaryWasEnabled) mapBoundary.enabled = true;

            m_LastVelocity = Vector3.zero;
            m_GraceEndTime = Time.time + graceTime;
            m_FlightStartTime = Time.time;
            m_Crashed = false;
        }

        private IEnumerator Fade(float from, float to, float duration) {
            if (m_Fade == null) yield break;
            yield return m_Fade.Fade(from, to, duration, m_ListenerVolume, audioDuck);
        }

        private void ShowMessage(string text) {
            if (m_Message == null) return;
            m_Message.text = text;
            m_Message.color = Color.white;
            m_Message.transform.parent.gameObject.SetActive(true);
        }

        private void HideMessage() {
            if (m_Message != null) m_Message.transform.parent.gameObject.SetActive(false);
        }
        #endregion

        /// <summary>Golpe de ~0.6 s (ruido grave que se apaga) para probar sin tener un sonido propio.</summary>
        private static AudioClip CreateImpactClip() {
            const int sampleRate = 44100;
            const float length = 0.6f;
            int count = (int)(length * sampleRate);
            var data = new float[count];
            var random = new System.Random(1);
            float low = 0f;

            for (int i = 0; i < count; i++) {
                float t = i / (float)sampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (noise - low) * 0.08f;                               // filtro pasa-bajos: suena a golpe, no a siseo
                float thump = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 9f);
                float envelope = Mathf.Exp(-t * 6f) * Mathf.Min(1f, i / (0.002f * sampleRate));
                data[i] = Mathf.Clamp((low * 2.5f + thump) * envelope, -1f, 1f) * 0.8f;
            }

            var clip = AudioClip.Create("ImpactoAvion", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
