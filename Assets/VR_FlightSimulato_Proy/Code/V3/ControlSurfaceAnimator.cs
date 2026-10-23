using UnityEngine;

namespace Charly.FlightController {
    /// <summary>
    /// Anima una superficie de control (alerón, elevador, timón o flap) según el input de FlightControllerV3.
    /// Es SOLO visual: no aplica fuerzas (eso lo hace AircraftPhysics).
    ///
    /// Va en un GameObject vacío colocado sobre la línea de la BISAGRA, con el mesh de la superficie como hijo.
    /// El vacío gira sobre 'hingeAxis' (ejes locales); el gizmo amarillo muestra ese eje para alinearlo.
    /// Quitar los colliders de la pieza: moverlos cada frame altera el centro de masa del avión.
    /// </summary>
    public class ControlSurfaceAnimator : MonoBehaviour {
        public enum ControlInput { Pitch, Roll, Yaw, Flaps }

        [Header("Referencias")]
        [Tooltip("Si se deja vacío se busca en los padres")]
        [SerializeField] private FlightControllerV3 input;

        [Header("Input")]
        [Tooltip("Pitch = elevadores, Roll = alerones, Yaw = timón, Flaps = flaps (0..1)")]
        [SerializeField] private ControlInput source = ControlInput.Roll;
        [Tooltip("Invierte el sentido. Úsalo en el alerón de un lado para que se muevan opuestos")]
        [SerializeField] private bool invert = false;

        [Header("Bisagra")]
        [Tooltip("Eje de giro en ejes LOCALES de este objeto (debe ir a lo largo de la bisagra)")]
        [SerializeField] private Vector3 hingeAxis = Vector3.right;
        [Tooltip("Grados cuando el input vale +1")]
        [SerializeField] private float positiveDeflection = 20f;
        [Tooltip("Grados cuando el input vale -1 (no se usa en flaps). Puede ser distinto para deflexión asimétrica")]
        [SerializeField] private float negativeDeflection = 20f;
        [Tooltip("Velocidad máxima de movimiento (grados/seg). Evita que la superficie salte de golpe")]
        [SerializeField] private float deflectionSpeed = 80f;

        /// <summary>Ángulo actual de la superficie en grados.</summary>
        public float CurrentAngle => m_CurrentAngle;

        private Quaternion m_RestRotation;
        private float m_CurrentAngle;

        private void Awake() {
            if (input == null)
                input = GetComponentInParent<FlightControllerV3>();

            if (input == null)
                Debug.LogWarning("ControlSurfaceAnimator: no se encontró FlightControllerV3 en los padres.", this);

            // Se guarda la pose de reposo para no acumular errores al rotar cada frame
            m_RestRotation = transform.localRotation;
        }

        private void Update() {
            float value = ReadInput();
            if (invert) value = -value;

            float target = value >= 0f ? value * positiveDeflection : value * negativeDeflection;
            m_CurrentAngle = Mathf.MoveTowards(m_CurrentAngle, target, deflectionSpeed * Time.deltaTime);

            transform.localRotation = m_RestRotation * Quaternion.AngleAxis(m_CurrentAngle, hingeAxis.normalized);
        }

        private float ReadInput() {
            if (input == null) return 0f;

            switch (source) {
                case ControlInput.Pitch: return input.Pitch;
                case ControlInput.Roll: return input.Roll;
                case ControlInput.Yaw: return input.Yaw;
                case ControlInput.Flaps: return input.Flaps;
                default: return 0f;
            }
        }

        private void OnDrawGizmosSelected() {
            if (hingeAxis.sqrMagnitude < 0.0001f) return;

            Vector3 axis = transform.TransformDirection(hingeAxis.normalized);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position - axis, transform.position + axis);
            Gizmos.DrawSphere(transform.position, 0.03f);
        }
    }
}
