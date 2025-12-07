using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Charly.FlightController {
    /// <summary>
    /// Controlador de vuelo para aviones en Realidad Virtual con físicas reales
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour {
        [Header("VR Controls")]
        [Tooltip("Palanca de throttle para controlar la potencia del motor")]
        [SerializeField] private ThrottleXR throttleLever;

        [Tooltip("Joystick de vuelo para controlar alabeo, cabeceo y guiñada")]
        [SerializeField] private JoystickXR flightStick;

        [Header("Flight Settings")]
        [Tooltip("Fuerza máxima de empuje del motor en Newtons")]
        [Range(10000f, 200000f)]
        public float m_MaxThrust = 50000f;

        [Tooltip("Masa del avión en kg")]
        [Range(500f, 50000f)]
        public float m_AircraftMass = 10000f;

        [Tooltip("Velocidad mínima requerida para despegar en km/h")]
        [Range(30f, 100f)]
        public float m_TakeoffSpeed = 60f;

        [Tooltip("Velocidad de rotación para alabeo y cabeceo en grados por segundo")]
        [Range(10f, 100f)]
        public float m_RotationSpeed = 50f;

        [Tooltip("Fuerza de sustentación aplicada a medida que gana velocidad")]
        [Range(5f, 30f)]
        public float m_LiftForce = 15f;

        [Tooltip("Velocidad de rotación para guiñada en grados por segundo")]
        [Range(10f, 50f)]
        public float m_YawSpeed = 30f;

        [Header("Drag Settings")]
        [Tooltip("Coeficiente de arrastre en dirección forward (eje Z)")]
        [Range(0.01f, 0.5f)]
        public float forwardDrag = 0.1f;

        [Tooltip("Coeficiente de arrastre en dirección lateral (eje X)")]
        [Range(1f, 5f)]
        public float sideDrag = 2.0f;

        [Tooltip("Coeficiente de arrastre en dirección vertical (eje Y)")]
        [Range(0.5f, 3f)]
        public float upDrag = 1.0f;

        [Header("Aerodynamics")]
        [Tooltip("Velocidad mínima para comenzar a generar sustentación")]
        [Range(20f, 80f)]
        public float m_MinLiftSpeed = 40f;

        [Tooltip("Velocidad a la que se alcanza sustentación máxima")]
        [Range(80f, 200f)]
        public float m_MaxLiftSpeed = 120f;

        [Tooltip("Coeficiente de sustentación (afecta cuánta sustentación se genera)")]
        [Range(0.5f, 3f)]
        public float liftCoefficient = 1.5f;

        [Tooltip("Coeficiente de resistencia aerodinámica")]
        [Range(0.01f, 0.2f)]
        public float dragCoefficient = 0.05f;

        [Header("XR Integration")]
        [Tooltip("Transform del origen XR (para seguimiento de movimiento)")]
        public Transform xrOrigin;

        [Tooltip("Asiento del piloto dentro de la cabina")]
        public Transform pilotSeat;

        [Tooltip("Suavizado del movimiento de la cámara en XR")]
        [Range(1f, 10f)]
        public float xrSmoothness = 5f;

        [Header("Control Settings")]
        [Tooltip("Invertir eje de cabeceo (hacia adelante/atrás)")]
        public bool invertPitch = false;

        [Tooltip("Invertir eje de alabeo (izquierda/derecha)")]
        public bool invertRoll = false;

        [Tooltip("Invertir eje de guiñada (rotación izquierda/derecha)")]
        public bool invertYaw = false;

        [Tooltip("Sensibilidad general de los controles")]
        [Range(0.5f, 30f)]
        public float controlSensitivity = 1.0f;

        [Tooltip("Sensibilidad específica para la guiñada (rotación de muñeca)")]
        [Range(0.5f, 3f)]
        public float yawSensitivity = 1.0f;

        [Header("Breaks")]
        [Tooltip("Ruedas del avión para aplicar frenado mediante fricción")]
        public SphereCollider[] wheels;

        /// <summary>
        /// Velocidad actual del avión en km/h (para instrumentos)
        /// </summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>
        /// Velocidad actual del avión en metros por segundo (para físicas)
        /// </summary>
        public float CurrentSpeedMps { get; private set; }

        private Rigidbody m_Rigidbody;
        private float m_ThrottleInput;
        private Vector2 m_DirectionInput;
        private float m_YawInput;
        private bool m_EngineOn;
        private AircraftVehicle aircraft;


        #region Unity Methods

        /// <summary>
        /// Inicialización en Awake para garantizar que el Rigidbody esté listo
        /// </summary>
        private void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
            SetupRigidbody();
        }

        /// <summary>
        /// Configuración inicial de controles VR
        /// </summary>
        private void Start() {
            SetUpLeverAndJoystick();
        }

        /// <summary>
        /// Actualización por frame - usado para valores de instrumentos
        /// </summary>
        private void Update() {
            UpdateSpeedValues();
        }

        /// <summary>
        /// Actualización de físicas - aplica todas las fuerzas y torques del vuelo
        /// </summary>
        private void FixedUpdate() {
            if (!m_EngineOn) return;

            ApplyThrust();
            ApplyAerodynamicForces();
            ApplyControlSurfaces();
            ApplyDrag();
        }
        #endregion

        /// <summary>
        /// Calcula y actualiza los valores de velocidad para instrumentos y físicas
        /// </summary>
        private void UpdateSpeedValues() {
            CurrentSpeedMps = m_Rigidbody.linearVelocity.magnitude;
            CurrentSpeed = CurrentSpeedMps * 3.6f; // Convertir m/s a km/h
        }

        /// <summary>
        /// Configura los parámetros iniciales del Rigidbody para comportamiento de vuelo realista
        /// </summary>
        private void SetupRigidbody() {
            m_Rigidbody.mass = m_AircraftMass;  // Masa del avión en kg
            m_Rigidbody.linearDamping = 0f; // Drag manual para mayor control
            m_Rigidbody.angularDamping = 5f;
            m_Rigidbody.useGravity = true;
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        /// <summary>
        /// Configura los eventos de los controles VR (throttle y joystick)
        /// </summary>
        private void SetUpLeverAndJoystick() {
            if (throttleLever != null) {
                throttleLever.SetMovingParent(transform);
                throttleLever.OnValueChange.AddListener(OnThrottleInput);
            }

            if (flightStick != null) {
                flightStick.OnJoystickMove.AddListener(OnJoystickInput);
                flightStick.OnYawInput.AddListener(OnYawInput);
            }

            Debug.Log("Controles VR configurados correctamente");
        }

        /// <summary>
        /// Callback para entrada del throttle (0 a 1)
        /// </summary>
        /// <param name="input">Valor normalizado del throttle (0 = apagado, 1 = máximo)</param>
        private void OnThrottleInput(float input) {
            m_ThrottleInput = Mathf.Clamp01(input);
        }

        /// <summary>
        /// Callback para entrada del joystick (alabeo y cabeceo)
        /// </summary>
        /// <param name="input">Vector2 con entrada normalizada (-1 a 1 en cada eje)</param>
        private void OnJoystickInput(Vector2 input) {
            input *= controlSensitivity;

            if (invertPitch) input.y = -input.y;
            if (invertRoll) input.x = -input.x;

            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>
        /// Callback para entrada de guiñada (rotación de muñeca)
        /// </summary>
        /// <param name="input">Valor normalizado de guiñada (-1 a 1)</param>
        private void OnYawInput(float input) {
            input *= yawSensitivity;
            if (invertYaw) input = -input;
            m_YawInput = Mathf.Clamp(input, -1f, 1f);
        }

        /// <summary>
        /// Aplica la fuerza de empuje del motor en la dirección forward del avión
        /// </summary>
        private void ApplyThrust() {
            float thrustForce = m_ThrottleInput * m_MaxThrust;
            Vector3 thrustVector = transform.forward * thrustForce;

            m_Rigidbody.AddForce(thrustVector, ForceMode.Force);

            // Debug visual para ver la fuerza de empuje
            Debug.DrawRay(transform.position, transform.forward * (thrustForce / m_MaxThrust) * 5f, Color.red);
        }

        /// <summary>
        /// Aplica fuerzas aerodinámicas (sustentación y resistencia)
        /// </summary>
        private void ApplyAerodynamicForces() {
            Vector3 localVelocity = transform.InverseTransformDirection(m_Rigidbody.linearVelocity);
            float airSpeed = localVelocity.z;

            // 1. Sustentación (Lift)
            if (Mathf.Abs(airSpeed) > m_MinLiftSpeed) {
                float angleOfAttack = m_DirectionInput.y * 15f;
                float liftSpeedFactor = Mathf.Clamp01((Mathf.Abs(airSpeed) - m_MinLiftSpeed) / (m_MaxLiftSpeed - m_MinLiftSpeed));

                float liftMagnitude = 0.5f * liftCoefficient * Mathf.Pow(airSpeed, 2) *
                                      Mathf.Clamp01(1 + angleOfAttack * 0.1f) * liftSpeedFactor;

                Vector3 liftForce = transform.up * liftMagnitude * m_LiftForce * Time.fixedDeltaTime;
                m_Rigidbody.AddForce(liftForce, ForceMode.Force);

                // Debug visual para sustentación
                Debug.DrawRay(transform.position, transform.up * (liftMagnitude / 10000f) * 5f, Color.green);
            }

            // 2. Resistencia aerodinámica (Drag)
            float dragMagnitude = 0.5f * dragCoefficient * Mathf.Pow(m_Rigidbody.linearVelocity.magnitude, 2);
            Vector3 dragForce = -m_Rigidbody.linearVelocity.normalized * dragMagnitude;
            m_Rigidbody.AddForce(dragForce, ForceMode.Force);
        }

        /// <summary>
        /// Aplica los controles de vuelo (alabeo, cabeceo, guiñada) como torques físicos
        /// </summary>
        private void ApplyControlSurfaces() {
            if (CurrentSpeedMps < m_TakeoffSpeed * 0.3f) return;

            float speedFactor = Mathf.Clamp(CurrentSpeedMps / (m_MaxThrust / 10000f), 0.3f, 1f);

            float pitchTorque = m_DirectionInput.y * m_RotationSpeed * speedFactor;
            float rollTorque = -m_DirectionInput.x * m_RotationSpeed * speedFactor;
            float yawTorque = m_YawInput * m_YawSpeed * speedFactor;

            Vector3 torque = new Vector3(pitchTorque, yawTorque, rollTorque);
            Vector3 worldTorque = transform.TransformDirection(torque);
            m_Rigidbody.AddTorque(worldTorque, ForceMode.Force);

            ApplyAerodynamicStabilization();
        }

        /// <summary>
        /// Aplica estabilización aerodinámica para mantener el avión nivelado
        /// </summary>
        private void ApplyAerodynamicStabilization() {
            Vector3 angularVelocity = m_Rigidbody.angularVelocity;

            // Reducir rotación no deseada
            float stabilizationForce = 10f;
            Vector3 stabilizationTorque = -angularVelocity * stabilizationForce;
            m_Rigidbody.AddTorque(stabilizationTorque, ForceMode.Force);

            // Estabilizar alabeo cuando no hay input
            float currentRoll = transform.eulerAngles.z;
            if (currentRoll > 180f) currentRoll -= 360f;

            if (Mathf.Abs(m_DirectionInput.x) < 0.1f && Mathf.Abs(currentRoll) > 1f) {
                float rollCorrection = -currentRoll * 0.5f;
                m_Rigidbody.AddRelativeTorque(0f, 0f, rollCorrection, ForceMode.Force);
            }
        }

        /// <summary>
        /// Aplica arrastre aerodinámico en diferentes ejes (forward, lateral, vertical)
        /// </summary>
        private void ApplyDrag() {
            Vector3 localVelocity = transform.InverseTransformDirection(m_Rigidbody.linearVelocity);

            Vector3 drag = new Vector3(
                -localVelocity.x * sideDrag,
                -localVelocity.y * upDrag,
                -localVelocity.z * forwardDrag
            );

            Vector3 worldDrag = transform.TransformDirection(drag);
            m_Rigidbody.AddForce(worldDrag, ForceMode.Force);
        }

        #region ThrottleAndLeverInputs

        /// <summary>
        /// Establece manualmente el valor del throttle
        /// </summary>
        /// <param name="input">Valor del throttle (0 a 1)</param>
        public void SetThrottle(float input) {
            m_ThrottleInput = Mathf.Clamp01(input);
        }

        /// <summary>
        /// Establece manualmente la entrada de dirección (alabeo y cabeceo)
        /// </summary>
        /// <param name="input">Vector2 normalizado con entrada de dirección</param>
        public void SetDirectionInput(Vector2 input) {
            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        }
        #endregion

        #region Engine

        /// <summary>
        /// Controla el estado del motor (0 = apagado, 1 = encendido)
        /// </summary>
        /// <param name="state">Estado del motor (0 o 1)</param>
        public void EngineState(int state) {
            if (state == 0) StopEngine();
            else StartEngine();
        }

        /// <summary>
        /// Enciende el motor del avión
        /// </summary>
        public void StartEngine() {
            if (m_EngineOn) return;
            m_EngineOn = true;
            Debug.Log("Motor encendido");
        }

        /// <summary>
        /// Apaga el motor del avión
        /// </summary>
        public void StopEngine() {
            m_EngineOn = false;
            m_ThrottleInput = 0f;
            Debug.Log("Motor apagado");
        }
        #endregion

        /// <summary>
        /// Reinicia el nivel actual
        /// </summary>
        public void RestartLevel() {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Aplica o libera los frenos de las ruedas
        /// </summary>
        /// <param name="isBraking">True para aplicar frenos, False para liberar</param>
        public void WheelsBreaks(bool isBraking) {
            float friction = isBraking ? 0.8f : 0.1f;

            foreach (SphereCollider wheel in wheels) {
                wheel.material.dynamicFriction = friction;

            }
        }

        /// <summary>
        /// Limpia los event listeners al destruir el objeto
        /// </summary>
        private void OnDestroy() {
            if (throttleLever != null)
                throttleLever.OnValueChange.RemoveListener(OnThrottleInput);

            if (flightStick != null) {
                flightStick.OnJoystickMove.RemoveListener(OnJoystickInput);
                flightStick.OnYawInput?.RemoveListener(OnYawInput);
            }
        }
    }
}