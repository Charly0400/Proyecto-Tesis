using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Charly.FlightController {
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour {
        [Header("VR Controls")]
        [SerializeField] private ThrottleXR throttleLever;
        [SerializeField] private JoystickXR flightStick;

        [Header("Flight Settings")]
        public float m_MaxSpeed = 200f;
        public float m_TakeoffSpeed = 60f;
        public float m_RotationSpeed = 50f;
        public float m_LiftForce = 15f;

        [Header("Aerodynamics")]
        public float m_MinLiftSpeed = 40f;
        public float m_MaxLiftSpeed = 120f;

        [Header("XR Integration")]
        public Transform xrOrigin;
        public Transform pilotSeat;
        public float xrSmoothness = 5f;

        [Header("Control Settings")]
        public bool invertPitch = false;
        public bool invertRoll = false;
        public float controlSensitivity = 1.0f;

        public float CurrentSpeed { get; private set; }
        public float CurrentSpeedMps { get; private set; }

        private Rigidbody m_Rigidbody;
        private float m_ThrottleInput;
        private Vector2 m_DirectionInput;
        private bool m_EngineOn;

        public AircraftVehicle aircraft;
        private bool isInAircraft = false;

        #region Unity Methods
        private void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
            SetupRigidbody();
        }

        private void Start() {
            SetUpLeverAndJoystick();
        }

        private void Update() {
            // Actualizar valores de velocidad para instrumentos
            UpdateSpeedValues();
        }

        private void FixedUpdate() {
            if (!m_EngineOn) return;

            MovePlane();
            ApplyLift();
            RotatePlane();
        }
        #endregion

        private void UpdateSpeedValues() {
            // Calcular velocidad actual en m/s y unidades del juego
            CurrentSpeedMps = m_Rigidbody.linearVelocity.magnitude;
            CurrentSpeed = m_ThrottleInput * m_MaxSpeed; // Velocidad objetivo basada en throttle
        }

        private void SetupRigidbody() {
            m_Rigidbody.linearDamping = 0.2f;
            m_Rigidbody.angularDamping = 1.5f;
            m_Rigidbody.useGravity = true;
        }

        private void SetUpLeverAndJoystick() {
            // Configurar throttle lever  
            if (throttleLever != null) {
                throttleLever.SetMovingParent(transform);
                throttleLever.OnValueChange.AddListener(OnThrottleInput);
            }

            if (flightStick != null) {
                // Solo necesitamos conectar el evento
                flightStick.OnJoystickMove.AddListener(OnJoystickInput);
            }

            Debug.Log("Controles VR configurados correctamente");
        }

        private void OnThrottleInput(float input) {
            // El throttle va de 0 a 1
            m_ThrottleInput = Mathf.Clamp01(input);
        }

        private void OnJoystickInput(Vector2 input) {
            // Aplicar sensibilidad
            input *= controlSensitivity;

            // Aplicar inversión
            if (invertPitch) input.y = -input.y;
            if (invertRoll) input.x = -input.x;

            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void MovePlane() {
            float currentSpeed = m_ThrottleInput * m_MaxSpeed;
            Vector3 movement = transform.forward * currentSpeed * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + movement);
        }

        private void ApplyLift() {
            float currentSpeed = m_ThrottleInput * m_MaxSpeed;

            if (currentSpeed > m_MinLiftSpeed) {
                float liftFactor = Mathf.Clamp01((currentSpeed - m_MinLiftSpeed) / (m_MaxLiftSpeed - m_MinLiftSpeed));
                Vector3 liftForce = transform.up * m_LiftForce * liftFactor * Time.fixedDeltaTime;
                m_Rigidbody.AddForce(liftForce, ForceMode.VelocityChange);
            }
        }

        private void RotatePlane() {
            float currentSpeed = m_ThrottleInput * m_MaxSpeed;
            if (currentSpeed < m_TakeoffSpeed * 0.3f) return;

            float speedFactor = Mathf.Clamp(currentSpeed / m_MaxSpeed, 0.3f, 1f);

            // Mapeo estándar: X = Roll, Y = Pitch
            float pitch = m_DirectionInput.y * m_RotationSpeed * speedFactor * Time.fixedDeltaTime;
            float roll = -m_DirectionInput.x * m_RotationSpeed * speedFactor * Time.fixedDeltaTime;
            float yaw = m_DirectionInput.x * m_RotationSpeed * 0.5f * speedFactor * Time.fixedDeltaTime;

            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.Euler(pitch, yaw, roll));
        }

        #region ThrottleAndLeverInputs
        public void SetThrottle(float input) {
            m_ThrottleInput = Mathf.Clamp01(input);
        }

        public void SetDirectionInput(Vector2 input) {
            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        } 
        #endregion

        #region Engine
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
            m_ThrottleInput = 0f;
            Debug.Log("Motor apagado");
        } 
        #endregion

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

        private void OnDestroy() {
            if (throttleLever != null)
                throttleLever.OnValueChange.RemoveListener(OnThrottleInput);

            if (flightStick != null)
                flightStick.OnJoystickMove.RemoveListener(OnJoystickInput);
        }
    }
}