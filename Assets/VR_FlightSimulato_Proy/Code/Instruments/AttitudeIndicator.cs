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

        private float m_pitchDegRaw;
        private float m_rollDegRaw;

        // Z local con la que se colocó la barra en la escena. Solo movemos X/Y para no
        // dejarla en el mismo plano que el fondo del horizonte (eso provocaba parpadeo).
        private float m_verticalLineBaseZ;

        // m_smoothnessAlpha se ajustó "por frame" a esta frecuencia; se escala con deltaTime
        // para que el suavizado se sienta igual aunque cambien los FPS.
        private const float SMOOTHING_REFERENCE_HZ = 90f;

        private void Awake() {
            if (m_verticalLineTransform != null)
                m_verticalLineBaseZ = m_verticalLineTransform.localPosition.z;
        }

        void Update() {
            if (m_planeTransform == null || m_verticalLineTransform == null || m_rollLineTransform == null) return;

            GetRawAngles();
            ApplySmoothness();
            MapToGraphics();
            //DebugDisplay();
        }

        private void GetRawAngles() {
            Vector3 forward = m_planeTransform.forward;
            Vector3 right = m_planeTransform.right;
            Vector3 up = m_planeTransform.up;

            // Pitch: ángulo del morro respecto al plano horizontal (-90..90)
            float horizontalForward = Mathf.Sqrt((forward.x * forward.x) + (forward.z * forward.z));
            m_pitchDegRaw = Mathf.Atan2(forward.y, horizontalForward) * Mathf.Rad2Deg;

            // Roll: right.y = sin(roll)·cos(pitch) y up.y = cos(roll)·cos(pitch),
            // así que atan2 da el alabeo real en -180..180 (incluye vuelo invertido)
            m_rollDegRaw = Mathf.Atan2(right.y, up.y) * Mathf.Rad2Deg;
        }

        private void ApplySmoothness() {
            float t = 1f - Mathf.Pow(1f - m_smoothnessAlpha, Time.deltaTime * SMOOTHING_REFERENCE_HZ);
            m_pitchDegSmoothed = Mathf.Lerp(m_pitchDegSmoothed, m_pitchDegRaw, t);
            // LerpAngle para que al cruzar 180/-180 (invertido) no dé la vuelta completa
            m_rollDegSmoothed = Mathf.DeltaAngle(0f, Mathf.LerpAngle(m_rollDegSmoothed, m_rollDegRaw, t));
        }

        private void MapToGraphics() {
            // Morro arriba -> la línea del horizonte baja (como en un horizonte artificial real)
            float pitch = Mathf.Clamp(m_pitchDegSmoothed, -90f, 90f);
            float offsetY = -pitch * m_pixelsPerDegree;
            m_verticalLineTransform.localPosition = new Vector3(0, offsetY, m_verticalLineBaseZ);

            m_rollLineTransform.localEulerAngles = new Vector3(0, 0, -m_rollDegSmoothed);
        }

        private void DebugDisplay() {
            Debug.Log($"Pitch: {m_pitchDegSmoothed} | Roll: {m_rollDegSmoothed}");
        }
    }
}

