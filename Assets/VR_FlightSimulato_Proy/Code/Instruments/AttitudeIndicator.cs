using UnityEngine;

namespace Charly.FlightController.Instruments {
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

        void Update() {
            GetRawAngles();
            ApplySmoothness();
            MapToGraphics();
            //DebugDisplay();
        }

        private void GetRawAngles() {
            m_forward = m_planeTransform.forward;
            m_right = m_planeTransform.right;
     
            float horizontalForward = Mathf.Sqrt((m_forward.x * m_forward.x) + (m_forward.z * m_forward.z));
            float pitchRad = Mathf.Atan2(m_forward.y, horizontalForward);
            m_pitchDegRaw = pitchRad * Mathf.Rad2Deg;

            float horizontalRight = Mathf.Sqrt(Mathf.Pow(m_right.x, 2f) + Mathf.Pow(m_right.z, 2f));
            float rollRad = Mathf.Atan2(m_right.y, horizontalRight);
            m_rollDegRaw = rollRad * Mathf.Rad2Deg;
        }

        private void ApplySmoothness() {   
            m_pitchDegSmoothed = Mathf.Lerp( m_pitchDegSmoothed, m_pitchDegRaw , m_smoothnessAlpha);
            m_rollDegSmoothed  = Mathf.Lerp( m_rollDegSmoothed, m_rollDegRaw, m_smoothnessAlpha);
        }

        private void MapToGraphics() {

            m_pitchDegRaw = Mathf.Clamp(m_pitchDegRaw, -90f, 90f);
            float offsetY = m_pitchDegSmoothed * m_pixelsPerDegree;
            m_verticalLineTransform.localPosition = new Vector3(0, offsetY, 0);

            m_rollDegRaw = Mathf.Clamp(m_rollDegRaw, -180f, 180f);
            m_rollLineTransform.localEulerAngles = new Vector3(0, 0, -m_rollDegSmoothed);

        }

        private void DebugDisplay() {
            Debug.Log($"Pitch: {m_pitchDegSmoothed} | Roll: {m_rollDegSmoothed}");
        }
    }
}

