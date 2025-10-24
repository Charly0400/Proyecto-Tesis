using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cahrly.FlightController {

    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour {
        [Header("VR Controls")]
        [SerializeField] private XRSlider throttleLever;
        [SerializeField] private Transform flightStick;

        [Header("Flight Settings")]
        public float m_MaxSpeed = 200f;          // Velocidad máxima (100%)
        public float m_TakeoffSpeed = 60f;       // Velocidad mínima para despegar
        public float m_RotationSpeed = 50f;      // Velocidad de giro
        public float m_LiftForce = 15f;          // Fuerza de sustentación

        [Header("Aerodynamics")]
        public float m_MinLiftSpeed = 40f;       // Velocidad mínima para generar sustentación
        public float m_MaxLiftSpeed = 120f;      // Velocidad para sustentación máxima

        [Header("XR Integration")]
        public Transform xrOrigin;
        public Transform pilotSeat;
        public float xrSmoothness = 5f;

        private Rigidbody m_Rigidbody;
        private float m_ThrottleInput;           // 0 a 1 (0% a 100%)
        private Vector2 m_DirectionInput;        // X: yaw/roll, Y: pitch
        private bool m_EngineOn;

        public AircraftVehicle aircraft;
        private bool isInAircraft = false;

        private void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
            SetupRigidbody();
        }
        void Start() {
            // Conectar la palanca al avión
            if (throttleLever != null) {
                var sliderComponent = throttleLever.GetComponent<XRSlider>();
                if (sliderComponent != null) {
                    // Usar el método SetMovingParent si existe
                    var method = sliderComponent.GetType().GetMethod("SetMovingParent");
                    if (method != null) {
                        method.Invoke(sliderComponent, new object[] { transform });
                    }
                }
            }
        }
        private void SetupRigidbody() {
            m_Rigidbody.linearDamping = 0.2f;            // Resistencia al movimiento
            m_Rigidbody.angularDamping = 1.5f;     // Resistencia a la rotación
            m_Rigidbody.useGravity = true;      // Importante para avión
        }


        private void FixedUpdate() {
            if (!m_EngineOn) return;

            MovePlane();
            ApplyLift();
            RotatePlane();
            UpdateXRPosition();
        }

        private void MovePlane() {
            // ✅ CALCULO CORRECTO: throttle (0-1) * velocidad máxima
            float currentSpeed = m_ThrottleInput * m_MaxSpeed;

            // ✅ Movimiento SOLO hacia adelante en la dirección del avión
            Vector3 movement = transform.forward * currentSpeed * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + movement);

            Debug.Log($"Throttle: {m_ThrottleInput * 100}% - Speed: {currentSpeed} m/s");
        }

        private void ApplyLift() {
            float currentSpeed = m_ThrottleInput * m_MaxSpeed;

            // ✅ Sustentación SOLO si hay suficiente velocidad
            if (currentSpeed > m_MinLiftSpeed) {
                float liftFactor = Mathf.Clamp01((currentSpeed - m_MinLiftSpeed) /
                                               (m_MaxLiftSpeed - m_MinLiftSpeed));

                Vector3 liftForce = transform.up * m_LiftForce * liftFactor * Time.fixedDeltaTime;
                m_Rigidbody.AddForce(liftForce, ForceMode.VelocityChange);
            }
        }

        private void RotatePlane() {
            float currentSpeed = m_ThrottleInput * m_MaxSpeed;

            // ✅ No girar a muy baja velocidad
            if (currentSpeed < m_TakeoffSpeed * 0.3f) return;

            float speedFactor = Mathf.Clamp(currentSpeed / m_MaxSpeed, 0.3f, 1f);

            float yaw = m_DirectionInput.x * m_RotationSpeed * speedFactor * Time.fixedDeltaTime;
            float pitch = -m_DirectionInput.y * m_RotationSpeed * speedFactor * Time.fixedDeltaTime;
            float roll = -m_DirectionInput.x * m_RotationSpeed * 0.8f * speedFactor * Time.fixedDeltaTime;

            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.Euler(pitch, yaw, roll));
        }

        // === CONTROL DE PALANCAS ===
        public void SetThrottle(float input) {
            // ✅ Palanca: 0 = neutro (0%), 0.5 = 50%, 1 = 100%
            m_ThrottleInput = Mathf.Clamp01(input);
        }

        public void SetDirectionInput(Vector2 input) {
            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void EngineState(int state) {
            if (state == 0) StopEngine();
            else StartEngine();
        }

        public void StartEngine() {
            if (m_EngineOn) return;
            m_EngineOn = true;
            Debug.Log("Motor encendido");
        }

        public void StopEngine() {
            m_EngineOn = false;
            m_ThrottleInput = 0f; // Resetear throttle al apagar motor
            Debug.Log("Motor apagado");
        }

        private void UpdateXRPosition() {
            if (xrOrigin == null || pilotSeat == null) return;

            // Suavizado de movimiento para comfort en VR
            xrOrigin.position = Vector3.Lerp(
                xrOrigin.position,
                pilotSeat.position,
                Time.deltaTime * xrSmoothness
            );

            // Rotación suavizada (solo el cuerpo, no la cabeza)
            xrOrigin.rotation = Quaternion.Slerp(
                xrOrigin.rotation,
                pilotSeat.rotation,
                Time.deltaTime * (xrSmoothness / 2f)
            );
        }

        public void RestartLevel() {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ToggleAircraftEntrance() {
            if (isInAircraft) {
                aircraft.UnseatPlayer();
                isInAircraft = false;
            }
            else {
                aircraft.SeatPlayer();
                isInAircraft = true;
            }
        }

    }
}
