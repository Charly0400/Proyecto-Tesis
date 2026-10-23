using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
        [Tooltip("Freno de la hélice con poco throttle (crece con velocidad²). Hace que al cortar motor el avión pierda velocidad de verdad")]
        [SerializeField] private float propellerBrake = 0.25f;

        [Header("Sustentación (alas)")]
        [Tooltip("Ángulo (grados, + = morro arriba) entre el +Z del objeto raíz y el eje real del fuselaje/ala. " +
                 "Empuje, sustentación, ángulo de ataque y torques se calculan sobre ese eje. Visualízalo con el gizmo cian")]
        [SerializeField] private float wingIncidenceDeg = 18.6f;
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

        [Header("Debug")]
        [Tooltip("Escribe la telemetría en la consola cada 'telemetryLogInterval' segundos (prefijo [FlightTelemetry])")]
        [SerializeField] private bool logTelemetry = true;
        [SerializeField] private float telemetryLogInterval = 0.5f;
        private float nextTelemetryLogTime;

        [Header("Colisiones internas")]
        [Tooltip("Ignora choques entre los colliders del avión y los de sus hijos con Rigidbody propio " +
                 "(palancas, switches, manos/rig XR sentado). Con Interpolate esos hijos van un paso atrás " +
                 "y a alta velocidad el avión 'choca consigo mismo' y se frena de golpe")]
        [SerializeField] private bool ignoreInternalCollisions = true;
        [Tooltip("Capa física para los colliders del avión (créala en Project Settings → Tags and Layers). " +
                 "Las piezas con Rigidbody propio la excluyen (Collider.excludeLayers): a diferencia de IgnoreCollision, " +
                 "eso no se pierde si la pieza se desactiva y reactiva. Vacío o inexistente = solo IgnoreCollision")]
        [SerializeField] private string aircraftLayerName = "Avion";
        private int aircraftLayer = -1;

        [Header("Física en VR")]
        [Tooltip("Corre la física a la frecuencia del visor y SIN Interpolate. Así los colliders de la cabina " +
                 "están donde se ven y las palancas se pueden agarrar a cualquier velocidad")]
        [SerializeField] private bool syncPhysicsToHeadset = true;
        [Tooltip("Frecuencia de física (Hz) si no se puede leer la del visor")]
        [SerializeField] private float fallbackPhysicsHz = 90f;

        public float AirspeedMps => t_AirspeedMps;
        public float AngleOfAttackDeg => t_AngleOfAttackDeg;
        public bool IsStalled => t_Stalled;

        private Rigidbody rb;

        // Rotación local del eje aerodinámico (fuselaje) respecto al objeto raíz.
        // Euler X negativo = morro arriba.
        private Quaternion AeroRotation => Quaternion.Euler(-wingIncidenceDeg, 0f, 0f);

        private void Awake() {
            rb = GetComponent<Rigidbody>();
            if (input == null)
                input = GetComponent<FlightControllerV3>();

            if (input == null)
                Debug.LogWarning("AircraftPhysics: no se encontró FlightController, el avión no recibirá controles.", this);

            SetupRigidbody();
            SetupAircraftLayer();
        }

        /// <summary>Pone los colliders propios del avión (los que mueve este Rigidbody) en la capa del avión.</summary>
        private void SetupAircraftLayer() {
            if (!ignoreInternalCollisions || string.IsNullOrEmpty(aircraftLayerName)) return;

            aircraftLayer = LayerMask.NameToLayer(aircraftLayerName);
            if (aircraftLayer < 0) {
                Debug.LogWarning($"AircraftPhysics: no existe la capa '{aircraftLayerName}'. Créala en Project Settings → Tags and Layers " +
                                 "para que las piezas de la cabina nunca choquen con el avión. Mientras tanto se usa solo IgnoreCollision.", this);
                return;
            }

            int moved = 0;
            foreach (var c in GetComponentsInChildren<Collider>(true)) {
                if (c.isTrigger || c.attachedRigidbody != rb || c.gameObject.layer == aircraftLayer) continue;
                // Las manos XR solo detectan la capa Default: no mover nada que se pueda agarrar o presionar
                if (BelongsToInteractable(c.transform)) continue;
                c.gameObject.layer = aircraftLayer;
                moved++;
            }

            if (logTelemetry)
                Debug.Log($"[FlightTelemetry] {moved} colliders del avión puestos en la capa '{aircraftLayerName}'", this);
        }

        /// <summary>True si entre 't' y la raíz del avión (sin incluirla) hay un interactable XR.</summary>
        private bool BelongsToInteractable(Transform t) {
            for (; t != null && t != transform; t = t.parent) {
                if (t.GetComponent<XRBaseInteractable>() != null) return true;
            }
            return false;
        }

        private void Start() {
            RefreshIgnoredCollisions();

            if (syncPhysicsToHeadset) {
                // Con Interpolate el avión se DIBUJA en una posición y su física está hasta un paso atrás
                // (a 120 m/s y 50 Hz son ~2.4 m): las manos "tocan" palancas que físicamente están en otro lado.
                float hz = fallbackPhysicsHz;
                var displays = new List<XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(displays);
                foreach (var display in displays) {
                    if (display.running && display.TryGetDisplayRefreshRate(out float rate) && rate > 0f) {
                        hz = rate;
                        break;
                    }
                }
                Time.fixedDeltaTime = 1f / hz;
            }

            if (logTelemetry) {
                Bounds b = new Bounds(transform.position, Vector3.zero);
                foreach (var c in GetComponentsInChildren<Collider>())
                    if (!c.isTrigger && c.attachedRigidbody == rb) b.Encapsulate(c.bounds);
                Debug.Log($"[FlightTelemetry] Física a {1f / Time.fixedDeltaTime:F0} Hz. Tamaño aprox. del avión (colliders): " +
                          $"{b.size.x:F1} x {b.size.y:F1} x {b.size.z:F1} m (P-47 real: 12.4 m envergadura, 11 m largo)", this);
            }
        }

        /// <summary>
        /// Valores para un P-47 con sensación "arcade realista" (estilo War Thunder).
        /// Clic derecho sobre el componente en el Inspector → "Aplicar preset P-47". Se puede deshacer con Ctrl+Z.
        /// </summary>
        [ContextMenu("Aplicar preset P-47")]
        private void ApplyP47Preset() {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Preset P-47");
#endif
            // ~6 t, motor de ~2000 hp: relación empuje/peso ~0.5 (ya no sube en vertical para siempre)
            mass = 6000f;
            maxAngularVelocityDeg = 150f;
            maxThrust = 36000f;
            thrustFalloffSpeed = 260f;          // vel. máx. nivelado ≈ 172 m/s ≈ 620 km/h
            propellerBrake = 0.25f;             // sin motor pierde ~1.3 m/s² en vuelo nivelado (planeo ~1:7)
            // Despega ~155 km/h, entra en pérdida ~130 km/h, crucero con ~1° de ángulo de ataque
            liftAtZeroAoA = 2f;
            liftSlope = 150f;
            stallAngle = 16f;
            stallFalloffAngle = 8f;
            stallResidualLift = 0.25f;
            dragCoefficient = 0.4f;             // ≈ arrastre real de un P-47
            inducedDragCoefficient = 0.004f;    // los giros cerrados y subidas cuestan velocidad
            lateralDragCoefficient = 25f;
            // Torques escalados a la nueva masa: ~60°/s pitch, ~110°/s roll con palanca a fondo
            pitchTorque = 30000f;
            rollTorque = 55000f;
            yawTorque = 20000f;
            controlAuthoritySpeed = 50f;
            pitchStability = 15000f;
            yawStability = 20000f;
            rotationalDamping = new Vector3(30000f, 30000f, 28000f);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log("AircraftPhysics: preset P-47 aplicado. Guarda la escena para conservarlo.", this);
        }

        /// <summary>
        /// Hace que los colliders propios del avión no choquen con colliders de hijos que tienen su propio
        /// Rigidbody (cinemáticos o no). Llamar de nuevo si se agregan hijos (p. ej. al sentar al jugador).
        /// </summary>
        public void RefreshIgnoredCollisions() {
            if (!ignoreInternalCollisions) return;

            var all = GetComponentsInChildren<Collider>(true);
            int pairs = 0;

            // Protección permanente: las piezas con Rigidbody propio excluyen la capa del avión
            if (aircraftLayer >= 0) {
                int mask = 1 << aircraftLayer;
                foreach (var other in all) {
                    if (other.attachedRigidbody != null && other.attachedRigidbody != rb)
                        other.excludeLayers |= mask;
                }
            }

            foreach (var own in all) {
                if (own.isTrigger || own.attachedRigidbody != rb) continue;
                foreach (var other in all) {
                    if (other.attachedRigidbody == null || other.attachedRigidbody == rb) continue;
                    Physics.IgnoreCollision(own, other, true);
                    pairs++;
                }
            }

            if (logTelemetry)
                Debug.Log($"[FlightTelemetry] Colisiones internas ignoradas: {pairs} pares", this);
        }

        /// <summary>True si el collider es parte del avión (incluye palancas y el rig XR cuando el piloto va sentado).</summary>
        public bool IsOwnPart(Collider other) {
            return other != null && other.transform.IsChildOf(transform);
        }

        // Si hay empuje pero el avión no se mueve, dice qué colliders lo están frenando (máx. 1 vez por segundo)
        private float nextStuckLogTime;
        private void OnCollisionStay(Collision collision) {
            if (!logTelemetry || t_ThrustNewtons < 1000f || t_AirspeedMps > 1f || Time.time < nextStuckLogTime) return;
            nextStuckLogTime = Time.time + 1f;
            for (int i = 0; i < collision.contactCount; i++) {
                var c = collision.GetContact(i);
                var mat = c.thisCollider.sharedMaterial;
                Debug.Log($"[FlightTelemetry] ATASCADO: '{c.thisCollider.name}' (material: {(mat != null ? $"{mat.name} est={mat.staticFriction} comb={mat.frictionCombine}" : "ninguno → fricción 0.6")}) " +
                          $"toca '{c.otherCollider.name}' impulso={c.impulse.magnitude:F0}", this);
            }
        }

        private void OnCollisionEnter(Collision collision) {
            // Una pieza propia (palanca, switch, rig XR) volvió a chocar con el avión: Unity pierde el IgnoreCollision
            // si ese collider se desactiva/reactiva o si no estaba activo al aplicarlo. Se vuelve a aplicar para que no lo frene.
            if (IsOwnPart(collision.collider)) {
                Debug.LogWarning($"AircraftPhysics: '{collision.collider.name}' (pieza del propio avión) chocó con el avión. " +
                                 "Se vuelven a ignorar las colisiones internas.", collision.collider);
                RefreshIgnoredCollisions();
            }

            if (!logTelemetry) return;
            Debug.Log($"[FlightTelemetry] COLISIÓN con '{collision.collider.name}' " +
                      $"(rb: {(collision.rigidbody != null ? collision.rigidbody.name : "ninguno")}, " +
                      $"propio: '{(collision.contactCount > 0 ? collision.GetContact(0).thisCollider.name : "?")}') " +
                      $"velRel={collision.relativeVelocity.magnitude:F1} impulso={collision.impulse.magnitude:F0}", this);
        }

        private void SetupRigidbody() {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = mass;
            rb.linearDamping = 0f;                       // el arrastre lo calculamos nosotros
            rb.angularDamping = angularDamping;
            rb.maxAngularVelocity = maxAngularVelocityDeg * Mathf.Deg2Rad;
            // Con física sincronizada al visor no hace falta interpolar (y la interpolación desfasa los colliders)
            rb.interpolation = syncPhysicsToHeadset ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            rb.ResetCenterOfMass();
            rb.centerOfMass += centerOfMassOffset;
        }

        private void FixedUpdate() {
            Quaternion aero = AeroRotation;
            Vector3 worldVel = rb.linearVelocity;
            // Velocidad en ejes aerodinámicos (Z = eje del fuselaje, no el +Z del objeto raíz)
            Vector3 localVel = Quaternion.Inverse(aero) * transform.InverseTransformDirection(worldVel);
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

            float thrust = ApplyThrust(forwardSpeed, aero);
            float lift = ApplyLift(aoa, forwardSpeed, aero);
            float drag = ApplyDrag(worldVel, localVel, speed, lift, aero);
            ApplyTorques(aoa, beta, authority, aero);

            // Telemetría
            t_AirspeedMps = speed;
            t_AngleOfAttackDeg = aoa * Mathf.Rad2Deg;
            t_SideSlipDeg = beta * Mathf.Rad2Deg;
            t_LiftNewtons = lift;
            t_DragNewtons = drag;
            t_ThrustNewtons = thrust;

            if (logTelemetry && Time.time >= nextTelemetryLogTime) {
                nextTelemetryLogTime = Time.time + telemetryLogInterval;
                Vector3 e = transform.eulerAngles;
                Debug.Log($"[FlightTelemetry] t={Time.time:F1} motor={(input != null && input.EngineOn)} " +
                          $"thr={(input != null ? input.Throttle : 0f):F2} vel={speed:F1} ({speed * 3.6f:F0} km/h) fwd={forwardSpeed:F1} " +
                          $"alt={transform.position.y:F1} vy={worldVel.y:F1} aoa={t_AngleOfAttackDeg:F1} beta={t_SideSlipDeg:F1} " +
                          $"lift={lift:F0} peso={rb.mass * -Physics.gravity.y:F0} thrust={thrust:F0} drag={drag:F0} " +
                          $"stall={t_Stalled} pitch={(input != null ? input.Pitch : 0f):F2} roll={(input != null ? input.Roll : 0f):F2} " +
                          $"yaw={(input != null ? input.Yaw : 0f):F2} " +
                          $"rot=({e.x:F1},{e.y:F1},{e.z:F1})", this);
            }
        }

        /// <summary>Empuje hacia adelante. Solo con motor encendido; cae al acercarse a thrustFalloffSpeed.</summary>
        private float ApplyThrust(float forwardSpeed, Quaternion aero) {
            if (input == null || !input.EngineOn) return 0f;

            float falloff = Mathf.Clamp01(1f - forwardSpeed / Mathf.Max(1f, thrustFalloffSpeed));
            float thrust = input.Throttle * maxThrust * falloff;

            rb.AddRelativeForce(aero * Vector3.forward * thrust);
            return thrust;
        }

        /// <summary>Sustentación = Cl(aoa) * velocidad². Con pérdida (stall) si el ángulo es excesivo.</summary>
        private float ApplyLift(float aoa, float forwardSpeed, Quaternion aero) {
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

            rb.AddRelativeForce(aero * Vector3.up * lift);
            return lift;
        }

        /// <summary>Arrastre parásito + inducido (opuestos a la velocidad) y arrastre lateral.</summary>
        private float ApplyDrag(Vector3 worldVel, Vector3 localVel, float speed, float lift, Quaternion aero) {
            float drag = 0f;

            if (speed > 0.1f) {
                float speedSqr = speed * speed;
                // La hélice frena cuando no empuja (throttle bajo o motor apagado)
                float throttle = (input != null && input.EngineOn) ? input.Throttle : 0f;
                float parasite = (dragCoefficient + propellerBrake * (1f - throttle)) * speedSqr;
                float induced = inducedDragCoefficient * lift * lift / Mathf.Max(speedSqr, 1f);
                drag = parasite + induced;

                rb.AddForce(-worldVel.normalized * drag);
            }

            // Resistencia contra el movimiento lateral (derrape)
            rb.AddRelativeForce(aero * Vector3.right * (-localVel.x * speed * lateralDragCoefficient));

            return drag;
        }

        /// <summary>Torques: controles del piloto + estabilidad aerodinámica + amortiguación.</summary>
        private void ApplyTorques(float aoa, float beta, float authority, Quaternion aero) {
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

            // 3) Amortiguación: se opone a la velocidad angular actual (en ejes aerodinámicos)
            Vector3 localAngVel = Quaternion.Inverse(aero) * transform.InverseTransformDirection(rb.angularVelocity);
            Vector3 damping = -Vector3.Scale(localAngVel, rotationalDamping) * authority;

            // Los torques se calcularon en ejes aerodinámicos: se pasan a ejes locales del objeto raíz
            rb.AddRelativeTorque(aero * (control + stability + damping));
        }

        private void OnDrawGizmosSelected() {
            var body = GetComponent<Rigidbody>();
            if (body == null) return;

            Vector3 com = transform.TransformPoint(body.centerOfMass);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(com, 0.15f);

            // Eje aerodinámico (cian): debe coincidir con el morro del fuselaje.
            // Eje +Z del objeto raíz (gris) como referencia.
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(com, com + transform.TransformDirection(AeroRotation * Vector3.forward) * 8f);
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(com, com + transform.forward * 6f);
        }
    }
}