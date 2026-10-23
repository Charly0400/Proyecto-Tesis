using UnityEngine;

namespace Charly.FlightController {
    /// <summary>
    /// Hace girar la hélice según el motor y el throttle de FlightControllerV3. Es SOLO visual.
    /// - Con motor encendido: RPM entre ralentí y máximo según el throttle, con inercia al subir/bajar.
    /// - Con motor apagado: gira como molino de viento según la velocidad del avión.
    /// - Opcional: a RPM alto oculta las aspas y muestra un disco borroso, porque en VR (72-120 Hz)
    ///   una hélice rápida parece girar lento o al revés ("efecto rueda de carreta").
    ///
    /// Va en el objeto de la hélice, con el pivote en el centro del buje. El gizmo cian muestra el eje de giro.
    /// </summary>
    public class PropellerAnimator : MonoBehaviour {
        [Header("Referencias")]
        [Tooltip("Si se deja vacío se busca en los padres")]
        [SerializeField] private FlightControllerV3 input;

        [Header("Giro")]
        [Tooltip("Eje de giro en ejes LOCALES de este objeto")]
        [SerializeField] private Vector3 spinAxis = Vector3.forward;
        [Tooltip("Invierte el sentido de giro")]
        [SerializeField] private bool reverse = false;

        [Header("RPM")]
        [Tooltip("RPM de la hélice en ralentí (motor encendido, throttle 0)")]
        [SerializeField] private float idleRpm = 300f;
        [Tooltip("RPM de la hélice a throttle 100%")]
        [SerializeField] private float maxRpm = 1300f;
        [Tooltip("Qué tan rápido SUBEN las RPM (RPM por segundo)")]
        [SerializeField] private float spoolUpRate = 400f;
        [Tooltip("Qué tan rápido BAJAN las RPM (RPM por segundo)")]
        [SerializeField] private float spoolDownRate = 250f;

        [Header("Molino de viento (motor apagado)")]
        [Tooltip("RPM por cada m/s de velocidad hacia adelante con el motor apagado")]
        [SerializeField] private float windmillRpmPerMps = 3f;
        [Tooltip("Tope de RPM como molino de viento")]
        [SerializeField] private float maxWindmillRpm = 400f;

        [Header("Disco borroso (opcional)")]
        [Tooltip("Renderers de las aspas: se ocultan a RPM alto. Vacío = siempre visibles")]
        [SerializeField] private Renderer[] bladeRenderers;
        [Tooltip("Renderer del disco borroso (material transparente con _BaseColor, p. ej. URP Unlit/Lit Transparent)")]
        [SerializeField] private Renderer blurDisc;
        [Tooltip("RPM a la que el disco empieza a aparecer")]
        [SerializeField] private float blurStartRpm = 350f;
        [Tooltip("RPM a la que el disco es totalmente visible y las aspas se ocultan")]
        [SerializeField] private float blurFullRpm = 650f;
        [Tooltip("Opacidad máxima del disco")]
        [Range(0f, 1f)]
        [SerializeField] private float blurMaxAlpha = 0.5f;

        /// <summary>RPM actuales de la hélice (sirve también para el audio del motor).</summary>
        public float Rpm => m_Rpm;
        /// <summary>RPM normalizadas 0..1 respecto a maxRpm.</summary>
        public float NormalizedRpm => maxRpm > 0f ? m_Rpm / maxRpm : 0f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private float m_Rpm;
        private MaterialPropertyBlock m_Block;
        private Color m_DiscColor = Color.white;
        private bool m_BladesVisible = true;

        private void Awake() {
            if (input == null)
                input = GetComponentInParent<FlightControllerV3>();

            if (input == null)
                Debug.LogWarning("PropellerAnimator: no se encontró FlightControllerV3 en los padres.", this);

            if (blurDisc != null) {
                m_Block = new MaterialPropertyBlock();
                if (blurDisc.sharedMaterial != null && blurDisc.sharedMaterial.HasProperty(BaseColorId))
                    m_DiscColor = blurDisc.sharedMaterial.GetColor(BaseColorId);
            }
        }

        private void Update() {
            float target = GetTargetRpm();
            float rate = (target > m_Rpm) ? spoolUpRate : spoolDownRate;
            m_Rpm = Mathf.MoveTowards(m_Rpm, target, rate * Time.deltaTime);

            // RPM * 360 / 60 = grados por segundo
            float degrees = m_Rpm * 6f * Time.deltaTime * (reverse ? -1f : 1f);
            transform.Rotate(spinAxis.normalized, degrees, Space.Self);

            UpdateBlur();
        }

        private float GetTargetRpm() {
            if (input == null) return 0f;

            float windmill = Mathf.Min(Mathf.Max(0f, input.CurrentSpeed) * windmillRpmPerMps, maxWindmillRpm);
            if (!input.EngineOn) return windmill;

            float powered = Mathf.Lerp(idleRpm, maxRpm, input.Throttle);
            return Mathf.Max(powered, windmill);
        }

        private void UpdateBlur() {
            if (blurDisc == null && (bladeRenderers == null || bladeRenderers.Length == 0)) return;

            float t = Mathf.InverseLerp(blurStartRpm, blurFullRpm, m_Rpm);

            if (blurDisc != null) {
                blurDisc.enabled = t > 0f;
                if (blurDisc.enabled) {
                    Color c = m_DiscColor;
                    c.a = t * blurMaxAlpha;
                    blurDisc.GetPropertyBlock(m_Block);
                    m_Block.SetColor(BaseColorId, c);
                    blurDisc.SetPropertyBlock(m_Block);
                }
            }

            // Sin disco asignado las aspas no se ocultan (si no, la hélice desaparecería)
            bool bladesVisible = blurDisc == null || t < 1f;
            if (bladesVisible != m_BladesVisible && bladeRenderers != null) {
                m_BladesVisible = bladesVisible;
                foreach (var blade in bladeRenderers) {
                    if (blade != null) blade.enabled = bladesVisible;
                }
            }
        }

        private void OnDrawGizmosSelected() {
            if (spinAxis.sqrMagnitude < 0.0001f) return;

            Vector3 axis = transform.TransformDirection(spinAxis.normalized);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + axis * 1.5f);
            Gizmos.DrawSphere(transform.position, 0.05f);
        }
    }
}
