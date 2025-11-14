using System.ComponentModel;
using TMPro;
using UnityEngine;

namespace Charly.FlightController.Instruments {
    public enum SpeedUnit {
        [Description("Millas por hora")]
        MPH,
        [Description("Nudos")]
        KTS,
        [Description("Kilómetros por hora")]
        KPH
    }

    public class AirSpeedInstrument : MonoBehaviour {
        [Tooltip("Rigidbody del avión para obtener la velocidad")]
        [SerializeField] private Rigidbody m_aircraftRigidbody;
        [Tooltip("Texto para mostrar la velocidad numérica")]
        [SerializeField] private TextMeshProUGUI m_SpeedText;

        [Header("Aguja del Velocímetro Configuración")]
        [Tooltip("Transform de la aguja del velocímetro")]
        [SerializeField] private Transform m_NeedleTransform;

        [Tooltip("Velocidad máxima del avión en unidades del mundo")]
        [SerializeField] private float m_MaxDisplaySpeed = 700f;

        [Tooltip("Velocidad mínima del avión en unidades del mundo")]
        [SerializeField] private float m_MinDisplaySpeed = 0f;

        [Tooltip("Ángulo (en grados) donde la aguja apunta cuando la lectura = min value.")]
        [SerializeField] private float m_StartAngle = 0;

        [Tooltip("Cuánto gira la aguja desde min hasta max")]
        [SerializeField] private float m_SweepAngle = 240f;

        [Tooltip("Factor de suavizado para el movimiento de la aguja")]
        [Range(0f, 20f)]
        [SerializeField] private float m_SmoothingFactor = 0.1f;

        [Tooltip("Unidad actual de lectura.")]
        public SpeedUnit m_Unit = SpeedUnit.MPH;

        [Header("Dirección de la Aguja")]
        [Tooltip("Si está marcado, la aguja gira en sentido horario (derecha). Si no, en sentido antihorario (izquierda)")]
        [SerializeField] private bool m_Clockwise = true;

        [Header("Configuración de Escala No Lineal")]
        [Tooltip("Velocidades clave en la escala (deben estar en orden ascendente)")]
        [SerializeField] private float[] m_SpeedMarkers = { 0, 50, 100, 150, 200, 250, 300, 400, 500, 600, 700 };

        [Tooltip("Porcentajes de ángulo para cada velocidad clave (0-1)")]
        [SerializeField] private float[] m_AnglePercentages = { 0f, 0.1f, 0.18f, 0.25f, 0.32f, 0.38f, 0.44f, 0.58f, 0.72f, 0.86f, 1f };

        [Header("Debug")]
        [Tooltip("Velocidad para pruebas en el Editor")]
        [SerializeField] private float m_DebugSpeed = 0f;
        [SerializeField] private float m_radiusGizmos = 0.7f;

        // estado interno
        private float m_DisplayAngle = 0f;

        // constantes de conversión (m/s -> unidad)
        private const float MPS_TO_KPH = 3.6f;
        private const float MPS_TO_MPH = 2.2369362920544f;
        private const float MPS_TO_KTS = 1.9438444924406f;

        void Update() {
            if (m_aircraftRigidbody == null || m_NeedleTransform == null) return;

            // 1. obtener velocidad real en m/s
            float speedMps = m_aircraftRigidbody.linearVelocity.magnitude;

            // 2. convertir a unidad seleccionada
            float factor = GetMpsToUnitFactor(m_Unit);
            float speedInUnit = speedMps * factor;

            // 3. calcular targetAngle usando mapeo no lineal
            float targetAngle = CalculateNonLinearAngle(speedInUnit);

            // 4. suavizar
            float smooth = 1f - Mathf.Exp(-m_SmoothingFactor * Time.deltaTime);
            m_DisplayAngle = Mathf.LerpAngle(m_DisplayAngle, targetAngle, smooth);

            // 5. aplicar rotación (con dirección)
            float finalAngle = m_Clockwise ? m_DisplayAngle : -m_DisplayAngle;
            m_NeedleTransform.localEulerAngles = new Vector3(0, 0, finalAngle);

            // 6. actualizar texto si aplica
            if (m_SpeedText != null) {
                m_SpeedText.text = Mathf.RoundToInt(speedInUnit).ToString() + " " + GetUnitLabel(m_Unit);
            }
        }

        private float CalculateNonLinearAngle(float speed) {
            // Si no hay suficientes puntos, usar mapeo lineal
            if (m_SpeedMarkers == null || m_AnglePercentages == null || m_SpeedMarkers.Length < 2 || m_SpeedMarkers.Length != m_AnglePercentages.Length) {
                Debug.LogWarning("Configuración de escala no lineal inválida. Usando mapeo lineal.");
                return MapSpeedToAngleLinear(speed);
            }

            // Clampear la velocidad dentro del rango
            speed = Mathf.Clamp(speed, m_MinDisplaySpeed, m_MaxDisplaySpeed);

            // Encontrar el segmento en el que se encuentra la velocidad
            int index = 1;
            while (index < m_SpeedMarkers.Length && speed > m_SpeedMarkers[index]) {
                index++;
            }

            // Si está en el último marcador o más allá
            if (index >= m_SpeedMarkers.Length) {
                return m_StartAngle + m_AnglePercentages[m_AnglePercentages.Length - 1] * m_SweepAngle;
            }

            // Interpolar entre el marcador anterior y el actual
            float prevSpeed = m_SpeedMarkers[index - 1];
            float nextSpeed = m_SpeedMarkers[index];
            float prevAnglePercent = m_AnglePercentages[index - 1];
            float nextAnglePercent = m_AnglePercentages[index];

            float t = (speed - prevSpeed) / (nextSpeed - prevSpeed);
            float anglePercent = Mathf.Lerp(prevAnglePercent, nextAnglePercent, t);

            return m_StartAngle + anglePercent * m_SweepAngle;
        }

        private float MapSpeedToAngleLinear(float speed) {
            float normalizedSpeed = Mathf.Clamp01((speed - m_MinDisplaySpeed) / (m_MaxDisplaySpeed - m_MinDisplaySpeed));
            return m_StartAngle + normalizedSpeed * m_SweepAngle;
        }

        private static float GetMpsToUnitFactor(SpeedUnit unit) {
            switch (unit) {
                case SpeedUnit.KPH: return MPS_TO_KPH;
                case SpeedUnit.MPH: return MPS_TO_MPH;
                case SpeedUnit.KTS: return MPS_TO_KTS;
                default: return MPS_TO_MPH;
            }
        }

        private static float GetMphToUnitFactor(SpeedUnit unit) {
            switch (unit) {
                case SpeedUnit.KPH: return 1.609344f;
                case SpeedUnit.MPH: return 1f;
                case SpeedUnit.KTS: return 0.86897624190065f;
                default: return 1f;
            }
        }

        private static string GetUnitLabel(SpeedUnit unit) {
            switch (unit) {
                case SpeedUnit.KPH: return "km/h";
                case SpeedUnit.MPH: return "MPH";
                case SpeedUnit.KTS: return "kts";
                default: return "MPH";
            }
        }

        public void SetUnit(SpeedUnit unit) {
            m_Unit = unit;
        }
        #region Gizmos Debugger

        // Método para debug: fuerza una velocidad específica
        public void SetDebugSpeed(float speed) {
            float targetAngle = CalculateNonLinearAngle(speed);
            m_DisplayAngle = targetAngle;

            float finalAngle = m_Clockwise ? m_DisplayAngle : -m_DisplayAngle;
            m_NeedleTransform.localEulerAngles = new Vector3(0, 0, finalAngle);
        }

        // Dibujar Gizmos en el Editor para visualizar la escala
        private void OnDrawGizmosSelected() {
            // Solo dibujar si estamos en el editor y tenemos un transform
            if (!Application.isPlaying && m_NeedleTransform != null) {
                DrawSpeedScaleGizmos();
            }
        }

        private void DrawSpeedScaleGizmos() {
            // Dibujar el círculo del velocímetro
            Vector3 center = transform.position;
            float radius = m_radiusGizmos;

            // Dibujar círculo base
            DrawCircle(center, radius, Color.white);

            // Dibujar marcas de velocidad
            if (m_SpeedMarkers != null && m_AnglePercentages != null && m_SpeedMarkers.Length == m_AnglePercentages.Length) {
                for (int i = 0; i < m_SpeedMarkers.Length; i++) {
                    // Aplicar dirección a las marcas también
                    float baseAngle = m_StartAngle + m_AnglePercentages[i] * m_SweepAngle;
                    float markerAngle = m_Clockwise ? baseAngle : -baseAngle;
                    DrawSpeedMarker(center, radius, markerAngle, m_SpeedMarkers[i].ToString(), Color.yellow);
                }
            }

            // Dibujar la aguja en su posición actual (para edición) con dirección aplicada
            float debugAngle = CalculateNonLinearAngle(m_DebugSpeed);
            float finalDebugAngle = m_Clockwise ? debugAngle : -debugAngle;
            DrawNeedle(center, radius, finalDebugAngle, Color.red);

            // Dibujar dirección de rotación
            DrawRotationDirection(center, radius);
        }

        private void DrawCircle(Vector3 center, float radius, Color color) {
            Gizmos.color = color;
            int segments = 360;
            Vector3 prevPoint = center + Quaternion.Euler(0, 0, 0) * Vector3.up * radius;
            for (int i = 1; i <= segments; i++) {
                float angle = (float)i / segments * 360f;
                Vector3 nextPoint = center + Quaternion.Euler(0, 0, angle) * Vector3.up * radius;
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }

        private void DrawSpeedMarker(Vector3 center, float radius, float angle, string label, Color color) {
            Gizmos.color = color;
            Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.up;
            Vector3 start = center + direction * (radius * 0.9f);
            Vector3 end = center + direction * radius;
            Gizmos.DrawLine(start, end);

            // Dibujar el texto (solo en el editor, usando Handles)
#if UNITY_EDITOR
            UnityEditor.Handles.Label(center + direction * (radius * 1.1f), label);
#endif
        }

        private void DrawNeedle(Vector3 center, float radius, float angle, Color color) {
            Gizmos.color = color;
            Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.up;
            Gizmos.DrawLine(center, center + direction * radius);

        }

        private void DrawRotationDirection(Vector3 center, float radius) {
            // Dibujar indicador de dirección
            Gizmos.color = m_Clockwise ? Color.green : Color.blue;

            float indicatorAngle = 45f; // Ángulo para el indicador
            float baseIndicatorAngle = m_StartAngle + indicatorAngle;
            float finalIndicatorAngle = m_Clockwise ? baseIndicatorAngle : -baseIndicatorAngle;

            Vector3 dir = Quaternion.Euler(0, 0, finalIndicatorAngle) * Vector3.up;
            Vector3 start = center + dir * (radius * 0.7f);
            Vector3 end = center + dir * (radius * 0.9f);

            Gizmos.DrawLine(start, end);

            // Dibujar flecha
            float arrowAngle = m_Clockwise ? -20f : 20f;
            Vector3 arrowDir1 = Quaternion.Euler(0, 0, finalIndicatorAngle + arrowAngle) * Vector3.up;
            Vector3 arrowDir2 = Quaternion.Euler(0, 0, finalIndicatorAngle - arrowAngle) * Vector3.up;

            Gizmos.DrawLine(end, end + arrowDir1 * 0.1f);
            Gizmos.DrawLine(end, end + arrowDir2 * 0.1f);

            // Etiqueta de dirección
#if UNITY_EDITOR
            string directionLabel = m_Clockwise ? "Horario" : "Antihorario";
            UnityEditor.Handles.Label(center + Vector3.down * (radius * 1.3f), directionLabel);
#endif
        }

        // Método público para cambiar dirección en tiempo de ejecución
        public void SetClockwiseDirection(bool clockwise) {
            m_Clockwise = clockwise;
        }

        // Método público para alternar dirección
        public void ToggleDirection() {
            m_Clockwise = !m_Clockwise;
        }
    } 
    #endregion
}