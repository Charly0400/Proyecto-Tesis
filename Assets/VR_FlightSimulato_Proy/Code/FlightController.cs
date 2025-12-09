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

        [Header("Breaks")]
        public SphereCollider[] wheels;

        public float CurrentSpeed { get; private set; }
        public float CurrentSpeedMps { get; private set; }
        public float CurrentThrottleTarget { get; private set; }
        public float CurrentThrottleActual { get; private set; }

        private Rigidbody m_Rigidbody;
        private float m_ThrottleInput; // Target del throttle (posición del lever)
        private float m_CurrentThrottle; // Throttle actual (gradual)
        private Vector2 m_DirectionInput;
        private float m_YawInput;
        private bool m_EngineOn;

        // Nueva: Velocidad actual para movimiento suave
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

            // Debug para ver los valores
            Debug.Log($"Throttle Target: {m_ThrottleInput:F2}, Throttle Actual: {m_CurrentThrottle:F2}, Velocity: {m_CurrentVelocity:F1}");
        }

        private void FixedUpdate() {
            if (!m_EngineOn) {
                m_CurrentVelocity = Mathf.Lerp(m_CurrentVelocity, 0f, Time.fixedDeltaTime * 5f);
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
            CurrentSpeed = m_CurrentVelocity; // Usar la velocidad actual
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

            // Calcular velocidad objetivo inmediatamente
            m_TargetVelocity = m_ThrottleInput * m_MaxSpeed;
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
            if (!useGradualThrottle) {
                m_TargetVelocity = m_ThrottleInput * m_MaxSpeed;
            }
            else {
                m_TargetVelocity = m_CurrentThrottle * m_MaxSpeed;
            }
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

        private void MovePlane() {
            // Suavizar la transición de velocidad
            float acceleration = (m_TargetVelocity > m_CurrentVelocity) ?
                throttleAccelerationRate * 2f : throttleDecelerationRate * 3f;

            m_CurrentVelocity = Mathf.MoveTowards(
                m_CurrentVelocity,
                m_TargetVelocity,
                acceleration * Time.fixedDeltaTime
            );

            // Mover el avión con la velocidad suavizada
            Vector3 movement = transform.forward * m_CurrentVelocity * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + movement);
        }

        private void ApplyLift() {
            // Usar la velocidad ACTUAL del movimiento (suavizada) para el lift
            float currentSpeed = m_CurrentVelocity;

            if (currentSpeed > m_MinLiftSpeed) {
                float liftFactor = Mathf.Clamp01((currentSpeed - m_MinLiftSpeed) / (m_MaxLiftSpeed - m_MinLiftSpeed));
                Vector3 liftForce = transform.up * m_LiftForce * liftFactor * Time.fixedDeltaTime;
                m_Rigidbody.AddForce(liftForce, ForceMode.VelocityChange);
            }
        }

        private void RotatePlane() {
            // Usar la velocidad ACTUAL para la rotación
            float currentSpeed = m_CurrentVelocity;

            // Ajustar el umbral mínimo de rotación
            if (currentSpeed < m_TakeoffSpeed * 0.5f) return;

            // Usar una curva más estable para el speedFactor
            float speedFactor = CalculateStableSpeedFactor(currentSpeed);

            // Aplicar rotaciones con límites
            float pitch = Mathf.Clamp(m_DirectionInput.y * m_RotationSpeed * speedFactor, -90f, 90f) * Time.fixedDeltaTime;
            float roll = Mathf.Clamp(-m_DirectionInput.x * m_RotationSpeed * speedFactor, -90f, 90f) * Time.fixedDeltaTime;
            float yaw = Mathf.Clamp(m_YawInput * m_YawSpeed * speedFactor, -45f, 45f) * Time.fixedDeltaTime;

            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.Euler(pitch, yaw, roll));
        }

        private float CalculateStableSpeedFactor(float currentSpeed) {
            // Si la velocidad es muy baja, poco factor
            if (currentSpeed < m_TakeoffSpeed) {
                return Mathf.Clamp01(currentSpeed / m_TakeoffSpeed) * 0.5f;
            }
            // Si está en rango medio, factor constante
            else if (currentSpeed < m_MaxSpeed * 0.7f) {
                return 0.7f;
            }
            // Si es alta velocidad, factor completo
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

        // Método para debug visual
        void OnGUI() {
            GUILayout.Label($"=== AVIÓN DEBUG ===");
            GUILayout.Label($"Throttle Posición: {m_ThrottleInput:F2}");
            GUILayout.Label($"Throttle Actual: {m_CurrentThrottle:F2}");
            GUILayout.Label($"Velocidad Objetivo: {m_TargetVelocity:F1}");
            GUILayout.Label($"Velocidad Actual: {m_CurrentVelocity:F1}");
            GUILayout.Label($"Velocidad Real: {CurrentSpeedMps:F1} m/s");
            GUILayout.Label($"Factor Rotación: {CalculateStableSpeedFactor(m_CurrentVelocity):F2}");
            GUILayout.Label($"Motor: {(m_EngineOn ? "ENCENDIDO" : "APAGADO")}");
        }
    }
}