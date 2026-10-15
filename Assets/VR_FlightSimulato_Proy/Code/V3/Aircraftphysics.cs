using UnityEngine;

namespace Charly.FlightController {
    /// <summary>
    /// MODELO DE VUELO con física real de Unity.
    /// Nunca escribe linearVelocity ni rotation: solo aplica FUERZAS y TORQUES al Rigidbody
    /// (empuje, sustentación, arrastre, superficies de control, estabilidad y amortiguación).
    /// Lee el input desde FlightController (Throttle, Pitch, Roll, Yaw, EngineOn).
    ///
    /// Ejes locales del avión: X = derecha (pitch), Y = arriba (yaw), Z = adelante (roll).
    /// Torque positivo sobre X = morro ABAJO; sobre Y = giro a la DERECHA; sobre Z = alabeo a la IZQUIERDA.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AircraftPhysics : MonoBehaviour {
        [Header("Referencias")]
        [Tooltip("Si se deja vacío se busca en este mismo GameObject")]
        [SerializeField] private FlightControllerV3 input;

        [Header("Rigidbody")]
        [Tooltip("Masa del avión en kg. Define cuánto cuesta acelerar/girar")]
        [SerializeField] private float mass = 1200f;
        [Tooltip("Desplazamiento del centro de masa (local). Más bajo/adelante = más estable")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.3f, 0.2f);
        [Tooltip("Amortiguación angular base del Rigidbody (la mayor parte la da 'Rotational Damping')")]
        [SerializeField] private float angularDamping = 0.5f;
        [Tooltip("Límite de velocidad de giro en grados/seg (confort VR y estabilidad numérica)")]
        [SerializeField] private float maxAngularVelocityDeg = 120f;

        [Header("Motor")]
        [Tooltip("Empuje a throttle 100% y velocidad 0 (Newtons)")]
        [SerializeField] private float maxThrust = 6000f;
        [Tooltip("Velocidad (m/s) a la que el empuje cae a 0. Limita la velocidad máxima")]
        [SerializeField] private float thrustFalloffSpeed = 250f;

        [Header("Sustentación (alas)")]
        [Tooltip("Sustentación con ángulo de ataque 0 (curvatura del ala). Mayor = vuela con menos ángulo")]
        [SerializeField] private float liftAtZeroAoA = 1.5f;
        [Tooltip("Cuánta sustentación extra por cada radián de ángulo de ataque")]
        [SerializeField] private float liftSlope = 60f;
        [Tooltip("Ángulo de ataque (grados) a partir del cual el ala empieza a perder sustentación (pérdida)")]
        [SerializeField] private float stallAngle = 15f;
        [Tooltip("Cuántos grados pasados del stallAngle tarda la sustentación en llegar al mínimo")]
        [SerializeField] private float stallFalloffAngle = 10f;
        [Tooltip("Fracción de sustentación que queda en pérdida total (0 = nada, 1 = sin pérdida)")]
        [Range(0f, 1f)]
        [SerializeField] private float stallResidualLift = 0.3f;

        [Header("Arrastre")]
        [Tooltip("Resistencia del aire al avanzar (crece con velocidad²). Define velocidad máxima y planeo")]
        [SerializeField] private float dragCoefficient = 0.5f;
        [Tooltip("Resistencia extra al generar mucha sustentación (frena en maniobras y a baja velocidad)")]
        [SerializeField] private float inducedDragCoefficient = 0.005f;
        [Tooltip("Resistencia lateral: evita que el avión 'derrape' como un hielo")]
        [SerializeField] private float lateralDragCoefficient = 5f;

        [Header("Superficies de control (torque máximo, N·m)")]
        [SerializeField] private float pitchTorque = 6000f;
        [SerializeField] private float rollTorque = 8000f;
        [SerializeField] private float yawTorque = 3000f;
        [Tooltip("Velocidad hacia adelante (m/s) a la que los controles dan el 100% de su efecto")]
        [SerializeField] private float controlAuthoritySpeed = 40f;

        [Header("Estabilidad (el avión tiende a alinearse con su movimiento)")]
        [Tooltip("Torque que baja/sube el morro para alinearlo con la trayectoria")]
        [SerializeField] private float pitchStability = 3000f;
        [Tooltip("Torque que gira el morro para alinearlo con la trayectoria (veleta)")]
        [SerializeField] private float yawStability = 4000f;
        [Tooltip("Frena la rotación: X = pitch, Y = yaw, Z = roll. Velocidad máx. de giro ≈ torque / damping")]
        [SerializeField] private Vector3 rotationalDamping = new Vector3(6000f, 6000f, 6000f);

        [Header("Telemetría (solo lectura, para ajustar en Play)")]
        [SerializeField] private float t_AirspeedMps;
        [SerializeField] private float t_AngleOfAttackDeg;
        [SerializeField] private float t_SideSlipDeg;
        [SerializeField] private float t_LiftNewtons;
        [SerializeField] private float t_DragNewtons;
        [SerializeField] private float t_ThrustNewtons;
        [SerializeField] private bool t_Stalled;

        public float AirspeedMps => t_AirspeedMps;
        public float AngleOfAttackDeg => t_AngleOfAttackDeg;
        public bool IsStalled => t_Stalled;

        private Rigidbody rb;

        private void Awake() {
            rb = GetComponent<Rigidbody>();
            if (input == null)
                input = GetComponent<FlightControllerV3>();

            if (input == null)
                Debug.LogWarning("AircraftPhysics: no se encontró FlightController, el avión no recibirá controles.", this);

            SetupRigidbody();
        }

        private void SetupRigidbody() {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = mass;
            rb.linearDamping = 0f;                       // el arrastre lo calculamos nosotros
            rb.angularDamping = angularDamping;
            rb.maxAngularVelocity = maxAngularVelocityDeg * Mathf.Deg2Rad;
            rb.interpolation = RigidbodyInterpolation.Interpolate;   // evita vibración del cockpit en VR
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            rb.ResetCenterOfMass();
            rb.centerOfMass += centerOfMassOffset;
        }

        private void FixedUpdate() {
            Vector3 worldVel = rb.linearVelocity;
            Vector3 localVel = transform.InverseTransformDirection(worldVel);
            float speed = worldVel.magnitude;
            float forwardSpeed = Mathf.Max(0f, localVel.z);

            // Qué tanto "muerden" las superficies de control: 0 parado, 1 a controlAuthoritySpeed
            float authority = Mathf.Clamp01(forwardSpeed / Mathf.Max(0.01f, controlAuthoritySpeed));

            // Ángulo de ataque (aoa) y derrape (beta), en radianes
            float aoa = 0f;
            float beta = 0f;
            if (forwardSpeed > 1f) {
                aoa = Mathf.Atan2(-localVel.y, localVel.z);   // + si el aire llega por debajo del morro
                beta = Mathf.Atan2(localVel.x, localVel.z);   // + si el avión se mueve hacia su derecha
            }

            float thrust = ApplyThrust(forwardSpeed);
            float lift = ApplyLift(aoa, forwardSpeed);
            float drag = ApplyDrag(worldVel, localVel, speed, lift);
            ApplyTorques(aoa, beta, authority);

            // Telemetría
            t_AirspeedMps = speed;
            t_AngleOfAttackDeg = aoa * Mathf.Rad2Deg;
            t_SideSlipDeg = beta * Mathf.Rad2Deg;
            t_LiftNewtons = lift;
            t_DragNewtons = drag;
            t_ThrustNewtons = thrust;
        }

        /// <summary>Empuje hacia adelante. Solo con motor encendido; cae al acercarse a thrustFalloffSpeed.</summary>
        private float ApplyThrust(float forwardSpeed) {
            if (input == null || !input.EngineOn) return 0f;

            float falloff = Mathf.Clamp01(1f - forwardSpeed / Mathf.Max(1f, thrustFalloffSpeed));
            float thrust = input.Throttle * maxThrust * falloff;

            rb.AddRelativeForce(Vector3.forward * thrust);
            return thrust;
        }

        /// <summary>Sustentación = Cl(aoa) * velocidad². Con pérdida (stall) si el ángulo es excesivo.</summary>
        private float ApplyLift(float aoa, float forwardSpeed) {
            float aoaDeg = Mathf.Abs(aoa * Mathf.Rad2Deg);

            float stallFactor = 1f;
            t_Stalled = false;
            if (aoaDeg > stallAngle) {
                float t = Mathf.Clamp01((aoaDeg - stallAngle) / Mathf.Max(0.01f, stallFalloffAngle));
                stallFactor = Mathf.Lerp(1f, stallResidualLift, t);
                t_Stalled = true;
            }

            float cl = (liftAtZeroAoA + liftSlope * aoa) * stallFactor;
            float lift = cl * forwardSpeed * forwardSpeed;

            rb.AddRelativeForce(Vector3.up * lift);
            return lift;
        }

        /// <summary>Arrastre parásito + inducido (opuestos a la velocidad) y arrastre lateral.</summary>
        private float ApplyDrag(Vector3 worldVel, Vector3 localVel, float speed, float lift) {
            float drag = 0f;

            if (speed > 0.1f) {
                float speedSqr = speed * speed;
                float parasite = dragCoefficient * speedSqr;
                float induced = inducedDragCoefficient * lift * lift / Mathf.Max(speedSqr, 1f);
                drag = parasite + induced;

                rb.AddForce(-worldVel.normalized * drag);
            }

            // Resistencia contra el movimiento lateral (derrape)
            rb.AddRelativeForce(Vector3.right * (-localVel.x * speed * lateralDragCoefficient));

            return drag;
        }

        /// <summary>Torques: controles del piloto + estabilidad aerodinámica + amortiguación.</summary>
        private void ApplyTorques(float aoa, float beta, float authority) {
            float pitchIn = input != null ? input.Pitch : 0f;
            float rollIn = input != null ? input.Roll : 0f;
            float yawIn = input != null ? input.Yaw : 0f;

            // 1) Controles (z negativo: stick a la derecha => alabeo a la derecha)
            Vector3 control = new Vector3(
                pitchIn * pitchTorque,
                yawIn * yawTorque,
                -rollIn * rollTorque) * authority;

            // 2) Estabilidad: alinear el morro con la trayectoria
            Vector3 stability = new Vector3(
                aoa * pitchStability,
                beta * yawStability,
                0f) * authority;

            // 3) Amortiguación: se opone a la velocidad angular actual (en ejes locales)
            Vector3 localAngVel = transform.InverseTransformDirection(rb.angularVelocity);
            Vector3 damping = -Vector3.Scale(localAngVel, rotationalDamping) * authority;

            rb.AddRelativeTorque(control + stability + damping);
        }

        private void OnDrawGizmosSelected() {
            var body = GetComponent<Rigidbody>();
            if (body == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.TransformPoint(body.centerOfMass), 0.15f);
        }
    }
}