using UnityEngine.ProBuilder;
using System.ComponentModel;
using UnityEngine;
using TMPro;

namespace Charly.FlightController.Instruments {

    public enum AltimeterMode {
        [Description("Unidades en Pies/Ft")]
        Feet,
        [Description("Unidades en Metros/M")]
        Meters
    }

    public class AltimeterInstrument : MonoBehaviour {

        [SerializeField] private Transform m_aircraftTransform;

        [Header("Configuración del Altímetro")]
        [Tooltip("Aguja que indica los 100 de pies")]
        [SerializeField] private Transform m_needle100;
        [Tooltip("Aguja que indica los 1000 de pies")]
        [SerializeField] private Transform m_needle1K;
        [Tooltip("Aguja que indica los 10000 de pies")]
        [SerializeField] private Transform m_needle10K;

        [Tooltip("Modo de visualización del altímetro (pies o metros)")]
        [SerializeField] private AltimeterMode m_altimeterMode = AltimeterMode.Feet;

        [Tooltip("Factor de suavizado para el movimiento de la aguja")]
        [Range(0f, 20f)]
        [SerializeField] private float m_SmoothingFactor = 6f;

        [SerializeField] private TextMeshProUGUI m_readoutText;

        private const float M_TO_FT = 3.28084f;

        private const float SCALE_100 = 1000f;
        private const float SCALE_1K  = 10000f;
        private const float SCALE_10K = 100000f;

        private float m_currentAngle100 = 0f;
        private float m_currentAngle1K = 0f;
        private float m_currentAngle10K = 0f;

        private void Update() {
            // 1) Obtener altitud (pies)
            float currentAltitudeUnit = GetAltitudeUnit();

            // 2) Calcular ángulos objetivo para cada aguja
            float target100 = AngleForScale(currentAltitudeUnit, SCALE_100);
            float target1k = AngleForScale(currentAltitudeUnit, SCALE_1K);
            float target10k = AngleForScale(currentAltitudeUnit, SCALE_10K);

            // 3) Smooth
            m_currentAngle100 = SmoothAngle(m_currentAngle100, target100);
            m_currentAngle1K = SmoothAngle(m_currentAngle1K, target1k);
            m_currentAngle10K = SmoothAngle(m_currentAngle10K, target10k);

            // 4) Aplicar rotaciones
            ApplyNeedleRotation(m_needle100, m_currentAngle100);
            ApplyNeedleRotation(m_needle1K, m_currentAngle1K);
            ApplyNeedleRotation(m_needle10K, m_currentAngle10K);

            if (m_readoutText != null)
                m_readoutText.text = FormatAltitudeDisplay(currentAltitudeUnit);
        }

        private float GetAltitudeUnit () {
            float altitudeMeters = m_aircraftTransform.position.y;

            switch (m_altimeterMode) {
                case AltimeterMode.Meters:
                    return altitudeMeters;
                case AltimeterMode.Feet:
                    return altitudeMeters * M_TO_FT;
                default:
                    return altitudeMeters * M_TO_FT;
            }
        }

        private string FormatAltitudeDisplay(float alt) {
            switch (m_altimeterMode) {
                case AltimeterMode.Meters:      
                    return Mathf.RoundToInt(alt).ToString() + " m";
                case AltimeterMode.Feet:
                default:
                    return Mathf.RoundToInt(alt * M_TO_FT).ToString() + " ft";
            }
        }

        private float AngleForScale(float alt, float scale) {
            float remainder = Mathf.Repeat(alt, scale);
            float fraction = remainder / scale;
            float angle = fraction * 360f; 
            return angle;
        }

        private float SmoothAngle(float currentAngle, float targetAngle) {
            if (m_SmoothingFactor <= 0f) return targetAngle;
            return Mathf.LerpAngle(currentAngle, targetAngle, 1f - Mathf.Exp(-m_SmoothingFactor * Time.deltaTime));
        }

        private void ApplyNeedleRotation(Transform needle, float angleDegrees) {
            if (needle == null) return;
            needle.localRotation = Quaternion.Euler(0f, 0f, -angleDegrees);
        }
    }
}
