using UnityEngine;

namespace Cahrly.FlightController {

    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour {

        [Header("Settings")]
        public float m_MaxSpeed = 50f;
        public float m_Acceleration = 10f;
        public float m_Drag = 5f;
        public float m_RotationSpeed = 50f;
        //public AudioSource m_EngineAudio;

        private Rigidbody m_Rigidbody;
        private float m_ThrottleInput;
        private Vector2 m_DirectionInput; // X: yaw/roll, Y: pitch
        private float m_CurrentSpeed;
        private bool m_EngineOn;

        private void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        private void Start() {
            //StopEngine();
        }

        private void FixedUpdate() {
            //if (!m_EngineOn) return;

            HandleThrottle();
            MovePlane();
            RotatePlane();
        }

        private void HandleThrottle() {
            // Aumentar velocidad
            m_CurrentSpeed += m_ThrottleInput * m_Acceleration * Time.fixedDeltaTime;

            // Aplicar desaceleración natural
            if (m_ThrottleInput <= 0)
                m_CurrentSpeed -= m_Drag * Time.fixedDeltaTime;

            // Limitar
            m_CurrentSpeed = Mathf.Clamp(m_CurrentSpeed, 0, m_MaxSpeed);
        }

        private void MovePlane() {
            Vector3 forwardMovement = transform.forward * m_CurrentSpeed * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + forwardMovement);
        }

        private void RotatePlane() {
            float yaw = m_DirectionInput.x * m_RotationSpeed * Time.fixedDeltaTime;
            float pitch = -m_DirectionInput.y * m_RotationSpeed * Time.fixedDeltaTime;

            // Roll más realista que depende de la velocidad
            float rollFactor = Mathf.Clamp(m_CurrentSpeed / m_MaxSpeed, 0.2f, 1f);
            float roll = -m_DirectionInput.x * m_RotationSpeed * 0.5f * rollFactor * Time.fixedDeltaTime;

            // Aplicar rotación con interpolación para suavizar
            Quaternion targetRotation = m_Rigidbody.rotation * Quaternion.Euler(pitch, yaw, roll);
            m_Rigidbody.MoveRotation(Quaternion.Slerp(m_Rigidbody.rotation, targetRotation, 0.5f));
        }

        public void EngineState(int state) {
            if (state == 0)
                StopEngine();
            else
                StartEngine();
        }

        public void StartEngine() {
            if (m_EngineOn) return;
            m_EngineOn = true;
            //if (m_EngineAudio) m_EngineAudio.Play();
        }

        public void StopEngine() {
            m_EngineOn = false;
            m_CurrentSpeed = 0f;
            //if (m_EngineAudio) m_EngineAudio.Stop();
        }

        // === 👇 Estas se asignan desde los XRJoystick en Unity ===
        public void SetThrottle(float input) => m_ThrottleInput = Mathf.Clamp(input, -1f, 1f);
        public void SetDirectionInput(Vector2 input) => m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
    }
}
