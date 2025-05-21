using UnityEngine;

namespace Cahrly.FlightController {

    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour {

        [Header("Settings")]
        public float m_MaxSpeed = 200f;  // Aumentar (un avión comercial vuela a ~250 m/s)
        public float m_Acceleration = 30f; // Aumentar considerablemente
        public float m_TakeoffSpeed = 60f; // Nueva variable para velocidad mínima de despegue
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

            // Fuerza de sustentación básica (simulando alas)
            float liftPower = Mathf.Clamp(m_CurrentSpeed * 0.2f, 0f, 15f);
            Vector3 liftForce = transform.up * liftPower * Time.fixedDeltaTime;

            m_Rigidbody.MovePosition(m_Rigidbody.position + forwardMovement + liftForce);
        }

        private void RotatePlane() {
            if (m_CurrentSpeed < m_TakeoffSpeed * 0.3f) return; // No girar a baja velocidad

            float speedFactor = Mathf.Clamp(m_CurrentSpeed / m_MaxSpeed, 0.3f, 1f);

            float yaw = m_DirectionInput.x * m_RotationSpeed * speedFactor * Time.fixedDeltaTime;
            float pitch = -m_DirectionInput.y * m_RotationSpeed * speedFactor * Time.fixedDeltaTime;
            float roll = -m_DirectionInput.x * m_RotationSpeed * 0.8f * speedFactor * Time.fixedDeltaTime;

            // Aplicar fuerza de sustentación durante giros
            Vector3 liftForce = transform.up * (m_CurrentSpeed * 0.1f * Mathf.Abs(m_DirectionInput.y));
            m_Rigidbody.AddForce(liftForce, ForceMode.Force);

            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.Euler(pitch, yaw, roll));
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
        public void SetDirectionInput(Vector2 input) {
            Debug.Log(input);
            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        }
    }
}
