using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Charly.FlightController.Instruments {
    public enum VerticalSpeedUnit {
        [Description("Pies por minuto")]
        FPM,
        [Description("Metros por segundo")]
        MPS
    }

    /// <summary>
    /// VARIÓMETRO / VSI (Vertical Speed Indicator) estilo P-47.
    /// La aguja descansa en 0 apuntando a la izquierda; al subir gira en sentido horario (por arriba)
    /// y al bajar en sentido antihorario (por abajo). La escala no es lineal, por eso se mapea con marcadores.
    /// Hay dos unidades:
    ///   'Unidad de lectura'   → la del texto y de <see cref="VerticalSpeed"/> (se puede cambiar en cualquier momento).
    ///   'Unidad de la carátula' → en la que están impresos los números de la carátula y los marcadores de la escala.
    ///   La aguja siempre sigue a la carátula, así nunca apunta a un número equivocado.
    /// </summary>
    public class VerticalSpeedIndicator : MonoBehaviour {
        [Tooltip("Rigidbody del avión. Si se deja vacío se busca en los padres")]
        [SerializeField] private Rigidbody m_aircraftRigidbody;

        [Header("Unidades")]
        [Tooltip("Unidad de la lectura numérica y de la propiedad VerticalSpeed")]
        [SerializeField] private VerticalSpeedUnit m_Unit = VerticalSpeedUnit.FPM;
        [Tooltip("Unidad en la que está impresa la carátula y en la que se escriben los marcadores de la escala. " +
                 "ClimbGauge está en miles de pies por minuto. Cámbiala solo si cambias la carátula")]
        [SerializeField] private VerticalSpeedUnit m_DialUnit = VerticalSpeedUnit.FPM;

        [Header("Aguja")]
        [Tooltip("Pivote de la aguja (gira en Z). Si se deja vacío se busca un hijo llamado 'Pivote_Aguja'")]
        [SerializeField] private Transform m_NeedleTransform;

        [Tooltip("Factor de suavizado. Un VSI real tiene retraso de unos segundos; más alto = responde más rápido")]
        [Range(0f, 20f)]
        [SerializeField] private float m_SmoothingFactor = 2f;

        [Header("Escala (medida sobre la carátula ClimbGauge)")]
        [Tooltip("Velocidades verticales clave en la UNIDAD DE LA CARÁTULA, en orden ascendente. Se usan igual para subir y para bajar")]
        [FormerlySerializedAs("m_SpeedMarkersFpm")]
        [SerializeField] private float[] m_SpeedMarkers = { 0f, 500f, 1000f, 2000f, 4000f, 6000f };

        [Tooltip("Grados que gira la aguja desde 0 para cada marcador")]
        [SerializeField] private float[] m_AngleMarkers = { 0f, 24.3f, 46.3f, 81.5f, 131f, 180f };

        [Header("Lectura numérica (opcional)")]
        [SerializeField] private TextMeshProUGUI m_readoutText;

        // constantes de conversión (m/s -> unidad)
        private const float MPS_TO_FPM = 196.850394f;
        private const float MPS_TO_MPS = 1f;

        private float m_DisplayMps;
        private bool m_WarnedInvalidScale;

        /// <summary>Velocidad vertical mostrada (suavizada) en la unidad de lectura. + = subiendo.</summary>
        public float VerticalSpeed => m_DisplayMps * GetMpsToUnitFactor(m_Unit);
        public VerticalSpeedUnit Unit => m_Unit;

        private void Awake() {
            if (m_aircraftRigidbody == null) m_aircraftRigidbody = GetComponentInParent<Rigidbody>();
            if (m_NeedleTransform == null) m_NeedleTransform = FindChild(transform, "Pivote_Aguja");

            if (m_aircraftRigidbody == null || m_NeedleTransform == null)
                Debug.LogError("VerticalSpeedIndicator: falta el Rigidbody del avión o el pivote de la aguja. Asígnalos en el Inspector.", this);
        }

        private void Update() {
            if (m_aircraftRigidbody == null || m_NeedleTransform == null) return;

            // 1) Velocidad vertical en m/s (+ = subiendo), suavizada (retraso del instrumento)
            float targetMps = m_aircraftRigidbody.linearVelocity.y;
            if (m_SmoothingFactor <= 0f) m_DisplayMps = targetMps;
            else m_DisplayMps = Mathf.Lerp(m_DisplayMps, targetMps, 1f - Mathf.Exp(-m_SmoothingFactor * Time.deltaTime));

            // 2) Aguja en la unidad de la carátula. Subir = horario (-Z), bajar = antihorario (+Z), igual que las demás agujas
            float dialValue = m_DisplayMps * GetMpsToUnitFactor(m_DialUnit);
            float angle = AngleForSpeed(Mathf.Abs(dialValue)) * Mathf.Sign(dialValue);
            m_NeedleTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);

            // 3) Texto en la unidad de lectura
            if (m_readoutText != null)
                m_readoutText.text = FormatReadout(VerticalSpeed, m_Unit);
        }

        public void SetUnit(VerticalSpeedUnit unit) {
            m_Unit = unit;
        }

        /// <summary>Pasa a la siguiente unidad (útil para un botón o switch de la cabina).</summary>
        public void CycleUnit() {
            m_Unit = m_Unit == VerticalSpeedUnit.FPM ? VerticalSpeedUnit.MPS : VerticalSpeedUnit.FPM;
        }

        /// <summary>Interpola el ángulo entre los marcadores. Se queda en el último si se pasa.</summary>
        private float AngleForSpeed(float value) {
            if (m_SpeedMarkers == null || m_AngleMarkers == null || m_SpeedMarkers.Length < 2 ||
                m_SpeedMarkers.Length != m_AngleMarkers.Length) {
                if (!m_WarnedInvalidScale) {
                    Debug.LogWarning("VerticalSpeedIndicator: los marcadores de escala no tienen el mismo tamaño. La aguja se queda en 0.", this);
                    m_WarnedInvalidScale = true;
                }
                return 0f;
            }

            int last = m_SpeedMarkers.Length - 1;
            if (value >= m_SpeedMarkers[last]) return m_AngleMarkers[last];

            int index = 1;
            while (index < last && value > m_SpeedMarkers[index]) index++;

            float t = Mathf.InverseLerp(m_SpeedMarkers[index - 1], m_SpeedMarkers[index], value);
            return Mathf.Lerp(m_AngleMarkers[index - 1], m_AngleMarkers[index], t);
        }

        private static float GetMpsToUnitFactor(VerticalSpeedUnit unit) {
            switch (unit) {
                case VerticalSpeedUnit.MPS:
                    return MPS_TO_MPS;
                case VerticalSpeedUnit.FPM:
                default:
                    return MPS_TO_FPM;
            }
        }

        private static string FormatReadout(float value, VerticalSpeedUnit unit) {
            switch (unit) {
                case VerticalSpeedUnit.MPS:
                    return value.ToString("F1") + " m/s";
                case VerticalSpeedUnit.FPM:
                default:
                    return Mathf.RoundToInt(value / 10f) * 10 + " ft/min";
            }
        }

        private static Transform FindChild(Transform parent, string name) {
            foreach (Transform child in parent) {
                if (child.name == name) return child;
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
