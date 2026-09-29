using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Charly.FlightController {
    [RequireComponent(typeof(Rigidbody))]
    public class FlightControllerV3 : MonoBehaviour {
        [Header("VR Controls")]
        [SerializeField] private ThrottleXRV2 throttleLever;
        [SerializeField] private JoystickXRV2 flightStick;

        [Header("Flight Settings")]
        public float m_MaxSpeed = 200f;
        public float m_TakeoffSpeed = 60f;
        public float m_RotationSpeed = 50f;
        public float m_LiftForce = 15f;
        public float m_YawSpeed = 30f;

        [Header("Aerodynamics")]
        public float m_MinLiftSpeed = 40f;
        public float m_MaxLiftSpeed = 120f;

        [Header("Throttle Acceleration")]
        public float throttleAccelerationRate = 1.0f; // Más lento para mejor control
        public float throttleDecelerationRate = 2.0f; // Desaceleración más rápida
        public bool useGradualThrottle = true; // Opción para activar/desactivar

        [Header("XR Integration")]
        public Transform xrOrigin;
        public Transform pilotSeat;
        public float xrSmoothness = 5f;

        [Header("Control Settings")]
        public bool invertPitch = false;
        public bool invertRoll = false;
        public bool invertYaw = false;
        public float controlSensitivity = 1.0f;
        public float yawSensitivity = 1.0f;

        [Header("Brakes")]
        public SphereCollider[] wheels;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        public float CurrentSpeed { get; private set; }
        public float CurrentSpeedMps { get; private set; }
        public float CurrentThrottleTarget { get; private set; }
        public float CurrentThrottleActual { get; private set; }

        private Rigidbody m_Rigidbody;
        private float m_ThrottleInput; // Target del throttle (posición del lever)
        private float m_CurrentThrottle; // Throttle actual (gradual, tras rampa de aceleración)
        private Vector2 m_DirectionInput;
        private float m_YawInput;
        private bool m_EngineOn;

        // Velocidad "forward" actual, ya suavizada. Un único punto de suavizado.
        private float m_CurrentVelocity = 0f;
        private float m_TargetVelocity = 0f;

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
            UpdateSpeedValues();

            if (enableDebugLogging) {
                Debug.Log($"Throttle Target: {m_ThrottleInput:F2}, Throttle Actual: {m_CurrentThrottle:F2}, Velocity: {m_CurrentVelocity:F1}");
            }
        }

        private void FixedUpdate() {
            if (!m_EngineOn) {
                // Sin motor: dejamos que la velocidad forward decaiga, la física
                // (gravedad) se encarga del resto.
                m_CurrentVelocity = Mathf.Lerp(m_CurrentVelocity, 0f, Time.fixedDeltaTime * 5f);
                m_TargetVelocity = 0f;
                return;
            }

            UpdateThrottleGradual();
            CalculateTargetVelocity();
            MovePlane();
            ApplyLift();
            RotatePlane();
        }
        #endregion

        private void UpdateSpeedValues() {
            CurrentSpeedMps = m_Rigidbody.linearVelocity.magnitude;
            CurrentSpeed = m_CurrentVelocity;
            CurrentThrottleTarget = m_ThrottleInput;
            CurrentThrottleActual = m_CurrentThrottle;
        }

        private void SetupRigidbody() {
            m_Rigidbody.linearDamping = 0.2f;
            m_Rigidbody.angularDamping = 1.5f;
            m_Rigidbody.useGravity = true;
        }

        private void SetUpLeverAndJoystick() {
            if (throttleLever != null) {
                throttleLever.OnValueChange.AddListener(OnThrottleInput);
            }

            if (flightStick != null) {
                flightStick.OnJoystickMove.AddListener(OnJoystickInput);
                flightStick.OnYawInput.AddListener(OnYawInput);
            }

            Debug.Log("Controles VR configurados correctamente");
        }

        private void OnThrottleInput(float input) {
            m_ThrottleInput = Mathf.Clamp01(input);
            // Nota: m_TargetVelocity se recalcula cada FixedUpdate en
            // CalculateTargetVelocity(), no hace falta setearlo aquí.
        }

        private void UpdateThrottleGradual() {
            if (!useGradualThrottle) {
                m_CurrentThrottle = m_ThrottleInput;
                return;
            }

            float accelerationRate = (m_ThrottleInput > m_CurrentThrottle) ?
                throttleAccelerationRate : throttleDecelerationRate;

            m_CurrentThrottle = Mathf.MoveTowards(
                m_CurrentThrottle,
                m_ThrottleInput,
                accelerationRate * Time.fixedDeltaTime
            );
        }

        private void CalculateTargetVelocity() {
            float throttleValue = useGradualThrottle ? m_CurrentThrottle : m_ThrottleInput;
            m_TargetVelocity = throttleValue * m_MaxSpeed;
        }

        private void OnJoystickInput(Vector2 input) {
            input *= controlSensitivity;

            if (invertPitch) input.y = -input.y;
            if (invertRoll) input.x = -input.x;

            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void OnYawInput(float input) {
            input *= yawSensitivity;
            if (invertYaw) input = -input;
            m_YawInput = Mathf.Clamp(input, -1f, 1f);
        }

        /// <summary>
        /// Único punto de suavizado: la rampa del throttle (UpdateThrottleGradual)
        /// ya define qué tan rápido cambia la velocidad objetivo, así que aquí
        /// simplemente igualamos la velocidad actual a la objetivo, evitando
        /// una segunda capa de "MoveTowards" que sumaba retraso innecesario.
        /// </summary>
        private void MovePlane() {
            m_CurrentVelocity = m_TargetVelocity;

            // Aplicamos la velocidad directamente al Rigidbody en vez de usar
            // MovePosition, para que la física (gravedad, colisiones) sea
            // consistente con el movimiento. Conservamos la componente vertical
            // actual (gravedad/lift) y reemplazamos solo el empuje hacia adelante.
            Vector3 currentVelocity = m_Rigidbody.linearVelocity;
            Vector3 verticalComponent = Vector3.Project(currentVelocity, transform.up);
            Vector3 forwardVelocity = transform.forward * m_CurrentVelocity;

            m_Rigidbody.linearVelocity = forwardVelocity + verticalComponent;
        }

        private void ApplyLift() {
            float currentSpeed = m_CurrentVelocity;

            if (currentSpeed > m_MinLiftSpeed) {
                float liftFactor = Mathf.Clamp01((currentSpeed - m_MinLiftSpeed) / (m_MaxLiftSpeed - m_MinLiftSpeed));
                Vector3 liftForce = transform.up * m_LiftForce * liftFactor * Time.fixedDeltaTime;
                m_Rigidbody.AddForce(liftForce, ForceMode.VelocityChange);
            }
        }

        private void RotatePlane() {
            float currentSpeed = m_CurrentVelocity;

            if (currentSpeed < m_TakeoffSpeed * 0.5f) return;

            float speedFactor = CalculateStableSpeedFactor(currentSpeed);

            float pitch = Mathf.Clamp(m_DirectionInput.y * m_RotationSpeed * speedFactor, -90f, 90f) * Time.fixedDeltaTime;
            float roll = Mathf.Clamp(-m_DirectionInput.x * m_RotationSpeed * speedFactor, -90f, 90f) * Time.fixedDeltaTime;
            float yaw = Mathf.Clamp(m_YawInput * m_YawSpeed * speedFactor, -45f, 45f) * Time.fixedDeltaTime;

            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.Euler(pitch, yaw, roll));
        }

        private float CalculateStableSpeedFactor(float currentSpeed) {
            if (currentSpeed < m_TakeoffSpeed) {
                return Mathf.Clamp01(currentSpeed / m_TakeoffSpeed) * 0.5f;
            }
            else if (currentSpeed < m_MaxSpeed * 0.7f) {
                return 0.7f;
            }
            else {
                return 1.0f;
            }
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
            m_CurrentThrottle = 0f;
            m_TargetVelocity = 0f;

            // Sincronizamos la palanca física con el estado interno para que,
            // al volver a agarrarla, no arranque con un valor "fantasma"
            // basado en la posición física donde quedó.
            if (throttleLever != null) {
                throttleLever.Value = 0f;
            }

            Debug.Log("Motor apagado");
        }
        #endregion

        public void RestartLevel() {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void WheelsBreaks(bool isBraking) {
            float friction;
            friction = isBraking ? .2f : 0f;

            foreach (SphereCollider wheel in wheels) {
                wheel.material.dynamicFriction = friction;
            }
        }

        public void ToggleAircraftEntrance() {
            if (aircraft == null) {
                Debug.LogWarning("FlightController: 'aircraft' no está asignado.");
                return;
            }

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

            if (flightStick != null) {
                flightStick.OnJoystickMove.RemoveListener(OnJoystickInput);
                flightStick.OnYawInput.RemoveListener(OnYawInput);
            }
        }
    }
}