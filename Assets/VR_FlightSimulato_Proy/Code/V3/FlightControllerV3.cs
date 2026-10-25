using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Charly.FlightController {
    /// <summary>
    /// CAPA DE INPUT del avión.
    /// Recibe la palanca (ThrottleXR) y el joystick (JoystickXR), suaviza el throttle,
    /// controla motor on/off, asiento y frenos, y EXPONE valores normalizados.
    /// NO aplica física: de eso se encarga AircraftPhysics (mismo GameObject).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FlightControllerV3 : MonoBehaviour {
        [Header("VR Controls")]
        [SerializeField] private ThrottleXR throttleLever;
        [SerializeField] private JoystickXR flightStick;
        [Tooltip("Switch de encendido. Si se deja vacío se busca en los hijos (solo se usa para reiniciar los controles)")]
        [SerializeField] private XRSwitch engineSwitch;

        [Header("Physics Reference")]
        [Tooltip("Si se deja vacío se busca en este mismo GameObject")]
        [SerializeField] private AircraftPhysics aircraftPhysics;

        [Header("Throttle (respuesta del motor)")]
        [Tooltip("Qué tan rápido SUBE el throttle real (unidades de throttle por segundo)")]
        public float throttleAccelerationRate = 1.0f;
        [Tooltip("Qué tan rápido BAJA el throttle real (unidades de throttle por segundo)")]
        public float throttleDecelerationRate = 2.0f;
        [Tooltip("Si está apagado, el throttle real sigue a la palanca al instante")]
        public bool useGradualThrottle = true;

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

        [Header("Frenos (stick del control izquierdo)")]
        [Tooltip("Abajo = frenar ambas ruedas (proporcional a cuánto se empuja). A un lado = frenar más la rueda de ese lado " +
                 "(freno diferencial, ayuda a girar). Solo con el piloto sentado. La fuerza la aplica AircraftPhysics")]
        public InputAction brakeInput = new InputAction("Frenos", InputActionType.Value, BrakeBindingDefault, expectedControlType: "Vector2");
        [Tooltip("Zona muerta del stick (0..1): el pulgar apoyado o un stick con juego no frenan")]
        [Range(0f, 0.9f)]
        public float brakeDeadZone = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        // ───────── SALIDAS NORMALIZADAS (las lee AircraftPhysics) ─────────
        /// <summary>Throttle REAL (ya suavizado) 0..1. Es 0 si el motor está apagado.</summary>
        public float Throttle => m_CurrentThrottle;
        /// <summary>Posición de la palanca 0..1 (lo que pide el piloto).</summary>
        public float ThrottleTarget => m_ThrottleInput;
        /// <summary>Joystick adelante/atrás -1..1 (adelante = positivo = morro abajo).</summary>
        public float Pitch => m_DirectionInput.y;
        /// <summary>Joystick izquierda/derecha -1..1 (derecha = positivo = alabeo a la derecha).</summary>
        public float Roll => m_DirectionInput.x;
        /// <summary>Giro de muñeca -1..1 (derecha = positivo).</summary>
        public float Yaw => m_YawInput;
        /// <summary>Posición de los flaps 0..1 (0 = retraídos, 1 = extendidos al máximo).</summary>
        public float Flaps => m_FlapsInput;
        /// <summary>Freno de la rueda principal izquierda 0..1.</summary>
        public float BrakeLeft => m_BrakeLeft;
        /// <summary>Freno de la rueda principal derecha 0..1.</summary>
        public float BrakeRight => m_BrakeRight;
        public bool EngineOn => m_EngineOn;

        // ───────── Compatibilidad con UI / otros scripts ─────────
        /// <summary>Velocidad hacia adelante en m/s (componente sobre transform.forward).</summary>
        public float CurrentSpeed { get; private set; }
        /// <summary>Velocidad total del Rigidbody en m/s.</summary>
        public float CurrentSpeedMps { get; private set; }
        public float CurrentThrottleTarget { get; private set; }
        public float CurrentThrottleActual { get; private set; }

        private Rigidbody m_Rigidbody;
        private float m_ThrottleInput;    // posición de la palanca
        private float m_CurrentThrottle;  // throttle real (suavizado)
        private Vector2 m_DirectionInput;
        private float m_YawInput;
        private float m_FlapsInput;
        private bool m_EngineOn;
        private float m_BrakeLeft;
        private float m_BrakeRight;
        private bool m_BrakeButton;       // WheelsBreaks(true): ambos frenos a fondo

        private const string BrakeBindingDefault = "<XRController>{LeftHand}/{Primary2DAxis}";

        private JoystickXRV2 m_FlightStickV2;
        private ThrottleXRV2[] m_ThrottlesV2;

        public AircraftVehicle aircraft;
        private bool isInAircraft = false;

        #region Unity Methods
        private void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
            if (aircraftPhysics == null)
                aircraftPhysics = GetComponent<AircraftPhysics>();

            if (aircraftPhysics == null)
                Debug.LogWarning("FlightController: no hay AircraftPhysics en este GameObject. El avión no tendrá física de vuelo.", this);

            // Por si la acción quedó sin bindings en la escena
            if (brakeInput.bindings.Count == 0)
                brakeInput.AddBinding(BrakeBindingDefault);
        }

        private void OnEnable() {
            brakeInput.Enable();
        }

        private void OnDisable() {
            brakeInput.Disable();
            m_BrakeLeft = m_BrakeRight = 0f;
        }

        private void Start() {
            SetUpLeverAndJoystick();
        }

        private void Update() {
            UpdateThrottleGradual();
            UpdateBrakes();
            UpdateSpeedValues();

            if (enableDebugLogging) {
                Debug.Log($"Throttle Target: {m_ThrottleInput:F2}, Throttle Real: {m_CurrentThrottle:F2}, Vel: {CurrentSpeed:F1} m/s");
            }
        }
        #endregion

        private void UpdateSpeedValues() {
            Vector3 v = m_Rigidbody.linearVelocity;
            CurrentSpeedMps = v.magnitude;
            CurrentSpeed = Vector3.Dot(v, transform.forward);
            CurrentThrottleTarget = m_ThrottleInput;
            CurrentThrottleActual = m_CurrentThrottle;
        }

        private void SetUpLeverAndJoystick() {
            if (throttleLever != null) {
                throttleLever.OnValueChange.AddListener(OnThrottleInput);
            }

            if (flightStick != null) {
                flightStick.OnJoystickMove.AddListener(OnJoystickInput);
                flightStick.OnYawInput.AddListener(OnYawInput);
            }

            // El joystick en uso es JoystickXRV2: su pitch/roll llega por el evento del Inspector (SetDirectionInput),
            // pero el giro de muñeca (cola / yaw) no estaba conectado. Se conecta aquí por código.
            m_FlightStickV2 = GetComponentInChildren<JoystickXRV2>();
            if (m_FlightStickV2 != null)
                m_FlightStickV2.OnYawInput.AddListener(OnYawInput);

            // La palanca en uso es ThrottleXRV2 (manda SetThrottle por el Inspector) y el switch manda
            // StartEngine/StopEngine. Se guardan para poder regresarlos a su posición al reiniciar.
            m_ThrottlesV2 = GetComponentsInChildren<ThrottleXRV2>(true);
            if (engineSwitch == null)
                engineSwitch = GetComponentInChildren<XRSwitch>(true);

            Debug.Log("Controles VR configurados correctamente");
        }

        private void OnThrottleInput(float input) {
            m_ThrottleInput = Mathf.Clamp01(input);
        }

        /// <summary>
        /// Único punto de suavizado del throttle. Con el motor apagado el objetivo es 0.
        /// </summary>
        private void UpdateThrottleGradual() {
            float target = m_EngineOn ? m_ThrottleInput : 0f;

            if (!useGradualThrottle) {
                m_CurrentThrottle = target;
                return;
            }

            float rate = (target > m_CurrentThrottle) ? throttleAccelerationRate : throttleDecelerationRate;
            m_CurrentThrottle = Mathf.MoveTowards(m_CurrentThrottle, target, rate * Time.deltaTime);
        }

        /// <summary>
        /// Stick izquierdo: abajo = ambos frenos, a un lado = más freno en la rueda de ese lado.
        /// Solo con el piloto sentado: parado fuera del avión ese stick sirve para caminar (locomoción XRI).
        /// </summary>
        private void UpdateBrakes() {
            Vector2 stick = (aircraft == null || aircraft.isPlayerSeated) ? brakeInput.ReadValue<Vector2>() : Vector2.zero;

            float both = m_BrakeButton ? 1f : ApplyBrakeDeadZone(-stick.y);
            float side = ApplyBrakeDeadZone(Mathf.Abs(stick.x)) * Mathf.Sign(stick.x);   // + = derecha
            m_BrakeLeft = Mathf.Clamp01(both + Mathf.Max(0f, -side));
            m_BrakeRight = Mathf.Clamp01(both + Mathf.Max(0f, side));
        }

        private float ApplyBrakeDeadZone(float value) {
            return Mathf.Clamp01((value - brakeDeadZone) / Mathf.Max(0.01f, 1f - brakeDeadZone));
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

        #region ThrottleAndLeverInputs
        public void SetThrottle(float input) {
            m_ThrottleInput = Mathf.Clamp01(input);
        }

        public void SetDirectionInput(Vector2 input) {
            m_DirectionInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>Para conectar por el Inspector la palanca o switch de flaps (0..1).</summary>
        public void SetFlaps(float input) {
            m_FlapsInput = Mathf.Clamp01(input);
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

            // Sincroniza la palanca física con el estado interno
            if (throttleLever != null) {
                throttleLever.Value = 0f;
            }

            Debug.Log("Motor apagado");
        }

        /// <summary>
        /// Deja la cabina como al empezar: switch de encendido abajo (eso dispara su OnSwitchOff → StopEngine y apaga
        /// el sonido del motor) y todas las palancas de propulsión en 0. Lo usa CrashHandler al chocar y al reaparecer.
        /// </summary>
        public void ResetControls() {
            // Primero se sueltan: si la mano los sigue agarrando los volvería a mover
            // (y el switch, al soltarse, dispara su estado actual; por eso se apaga después)
            Release(engineSwitch);
            Release(m_FlightStickV2);
            if (m_ThrottlesV2 != null) {
                foreach (var throttle in m_ThrottlesV2) Release(throttle);
            }

            if (engineSwitch != null)
                engineSwitch.SetSwitchState(false);
            StopEngine();

            if (m_ThrottlesV2 != null) {
                foreach (var throttle in m_ThrottlesV2) {
                    if (throttle != null) throttle.Value = 0f;
                }
            }

            m_DirectionInput = Vector2.zero;
            m_YawInput = 0f;
            m_BrakeButton = false;
        }

        private static void Release(XRBaseInteractable interactable) {
            if (interactable != null && interactable.isSelected && interactable.interactionManager != null)
                interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);
        }
        #endregion

        public void RestartLevel() {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Ambos frenos a fondo mientras sea true (para un botón o switch de la cabina), además del stick izquierdo.
        /// Ya no toca la fricción de las ruedas: la fuerza de frenado la aplica AircraftPhysics con las ruedas en el suelo.
        /// </summary>
        public void WheelsBreaks(bool isBraking) {
            m_BrakeButton = isBraking;
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

            if (m_FlightStickV2 != null)
                m_FlightStickV2.OnYawInput.RemoveListener(OnYawInput);
        }
    }
}