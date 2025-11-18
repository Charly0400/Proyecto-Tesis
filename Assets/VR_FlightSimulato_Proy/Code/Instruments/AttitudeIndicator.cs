using UnityEngine;

namespace Charly.Instruments {
    public class AttitudeIndicator : MonoBehaviour {

        [Header("Componentes del indicador de actitud")]
        [Tooltip("Transform del horizonte artificial")]
        [SerializeField] private Transform m_planeTransform;
        [Tooltip("Transform de la línea vertical (pitch)")]
        [SerializeField] private Transform m_verticalLineTransform;
        [Tooltip("Transform de la línea de rotación (roll) ")]
        [SerializeField] private Transform m_rollLineTransform;

        [Header("Ajustes del instrunmento")]
        [SerializeField] private float m_pixelsPerDegree;
        [Range(0f, 1f)]
        [SerializeField] private float m_smoothnessAlpha;

        private float m_pitchDegSmoothed;
        private float m_rollDegSmoothed;

        private Vector3 m_forward;
        private Vector3 m_right;
        private float m_pitchDegRaw;
        private float m_rollDegRaw;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start() {
 
        }

        // Update is called once per frame
        void Update() {
            GetRawAngles();
            ApplySmoothness();
            MapToGraphics();
            DebugDisplay();
        }

        // ----------------------------------------------------------
        // 1. OBTENER ÁNGULOS CRUDOS (pitch y roll reales del avión)
        // ----------------------------------------------------------
        private void GetRawAngles() {
            m_forward = m_planeTransform.forward;
            m_right = m_planeTransform.right;

            // PISTA 2:
            // Para pitch: compara forward.y contra su magnitud horizontal.
            float horizontalForward = Mathf.Sqrt((m_forward.x * m_forward.x) + (m_forward.z * m_forward.z));

            // PISTA 3:
            // Usa Mathf.Atan2(vertical, horizontal) para obtener radianes.
            float pitchRad = Mathf.Atan2(m_forward.y, horizontalForward);

            // PISTA 4:
            // Convierte radianes a grados:
            // m_pitchDegRaw = pitchRad * Mathf.Rad2Deg;
            m_pitchDegRaw = pitchRad * Mathf.Rad2Deg;

            // ----------------------------------------------

            // PISTA 5:
            // Para roll: es lo mismo pero usando 'right' en vez de 'forward'.
            // Magnitud horizontal:
            // float horizontalRight = Mathf.Sqrt( right.x*right.x + right.z*right.z );
            float horizontalRight = Mathf.Sqrt(Mathf.Pow(m_right.x, 2f) + Mathf.Pow(m_right.z, 2f));

            // float rollRad = Mathf.Atan2( ??? , ??? );
            float rollRad = Mathf.Atan2(m_right.y, horizontalRight);
            m_rollDegRaw = rollRad * Mathf.Rad2Deg;

            // CONSEJO:
            // Imprime los valores para ver si muy grandes, invertidos, etc.
            //Debug.Log($"RAW Pitch: {m_pitchDegRaw}, RAW Roll: {m_rollDegRaw}");
        }


        // ----------------------------------------------------------
        // 2. SUAVIZADO EXPONENCIAL
        // ----------------------------------------------------------
        private void ApplySmoothness() {
            // PISTA:
            // Aplica la famosa fórmula:
            // smoothed = (1 - alpha) * old + alpha * current;
            //
            // Misión:
            // - Usar m_pitchDegRaw para actualizar m_pitchDegSmoothed
            // - Usar m_rollDegRaw para actualizar m_rollDegSmoothed

            // EJEMPLO DEL PATRÓN (no copiar literal):
            m_pitchDegSmoothed = Mathf.Lerp( m_pitchDegSmoothed, m_pitchDegRaw , m_smoothnessAlpha);
            m_rollDegSmoothed  = Mathf.Lerp( m_rollDegSmoothed, m_rollDegRaw, m_smoothnessAlpha);
        }


        // ----------------------------------------------------------
        // 3. MOVER Y ROTAR LOS ELEMENTOS GRÁFICOS DEL INSTRUMENTO
        // ----------------------------------------------------------
        private void MapToGraphics() {

            // PITCH >> mover verticalLine EN Y
            // PISTA:
            m_pitchDegRaw = Mathf.Clamp(m_pitchDegRaw, -90f, 90f);
            float offsetY = m_pitchDegSmoothed * m_pixelsPerDegree;
            m_verticalLineTransform.localPosition = new Vector3(0, offsetY, 0);

            // ROLL  >> rotar rollLine
            // PISTA:
            // El roll se aplica rotando en Z (local)
            m_rollDegRaw = Mathf.Clamp(m_rollDegRaw, -180f, 180f);
            m_rollLineTransform.localEulerAngles = new Vector3(0, 0, -m_rollDegSmoothed);

            // CONSEJO:
            // Puede requerir invertir signo:
            // -m_rollDegSmoothed o +m_rollDegSmoothed
        }


        // ----------------------------------------------------------
        // 4. DEBUG EN PANTALLA (opcional pero útil)
        // ----------------------------------------------------------
        private void DebugDisplay() {

            // PISTA:
            // Usa Debug.Log solo si necesitas verificar valores.
            // Por ejemplo:
            Debug.Log($"Pitch: {m_pitchDegSmoothed} | Roll: {m_rollDegSmoothed}");
        }
    }
}

