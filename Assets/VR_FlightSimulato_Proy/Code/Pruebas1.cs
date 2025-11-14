using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Charly.FlightController {
    [RequireComponent(typeof(Rigidbody))]
    public class Pruebas1 : MonoBehaviour {
        [Header("VR Controls")]
        [SerializeField] private ThrottleXR throttleLever;
        [SerializeField] private JoystickXR flightStick;

        [Header("Flight Physics Settings")]
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float health = 100f;
        [SerializeField] float maxThrust = 100f;
        [SerializeField] float throttleSpeed = 2f;
        [SerializeField] float gLimit = 5f;
        [SerializeField] float gLimitPitch = 3f;

        [Header("Lift Settings")]
        [SerializeField] float liftPower = 1f;
        [SerializeField] AnimationCurve liftAOACurve;
        [SerializeField] float inducedDrag = 0.01f;
        [SerializeField] AnimationCurve inducedDragCurve;
        [SerializeField] float rudderPower = 0.5f;
        [SerializeField] AnimationCurve rudderAOACurve;
        [SerializeField] AnimationCurve rudderInducedDragCurve;
        [SerializeField] float flapsLiftPower = 0.3f;
        [SerializeField] float flapsAOABias = 5f;
        [SerializeField] float flapsDrag = 0.01f;
        [SerializeField] float flapsRetractSpeed = 40f;

        [Header("Steering Settings")]
        [SerializeField] Vector3 turnSpeed = new Vector3(50f, 10f, 50f);
        [SerializeField] Vector3 turnAcceleration = new Vector3(100f, 20f, 100f);
        [SerializeField] AnimationCurve steeringCurve;

        [Header("Drag Settings")]
        [SerializeField] AnimationCurve dragForward;
        [SerializeField] AnimationCurve dragBack;
        [SerializeField] AnimationCurve dragLeft;
        [SerializeField] AnimationCurve dragRight;
        [SerializeField] AnimationCurve dragTop;
        [SerializeField] AnimationCurve dragBottom;
        [SerializeField] Vector3 angularDrag = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] float airbrakeDrag = 2f;

        [Header("VR Integration")]
        public Transform xrOrigin;
        public Transform pilotSeat;
        public float xrSmoothness = 5f;

        [Header("Control Settings")]
        public bool invertPitch = false;
        public bool invertRoll = false;
        public float controlSensitivity = 1.0f;

        [Header("Misc Settings")]
        [SerializeField] List<Collider> landingGear;
        [SerializeField] PhysicsMaterial landingGearBrakesMaterial;
        [SerializeField] List<GameObject> graphics;
        [SerializeField] GameObject damageEffect;
        [SerializeField] GameObject deathEffect;
        [SerializeField] bool flapsDeployed;
        [SerializeField] float initialSpeed = 30f;

        #region Private Variables
        private Rigidbody m_Rigidbody;

        private float throttleInput;
        private Vector3 controlInput;
        private float m_ThrottleInput;
        private bool m_EngineOn = true;

        private Vector3 lastVelocity;
        private PhysicsMaterial landingGearDefaultMaterial;
        #endregion

        #region Properties
        public float MaxHealth {
            get { return maxHealth; }
            set { maxHealth = Mathf.Max(0, value); }
        }

        public float Health {
            get { return health; }
            private set {
                health = Mathf.Clamp(value, 0, maxHealth);
                if (health <= MaxHealth * .5f && health > 0) {
                    if (damageEffect != null) damageEffect.SetActive(true);
                }
                else {
                    if (damageEffect != null) damageEffect.SetActive(false);
                }
                if (health == 0 && MaxHealth != 0 && !Dead) {
                    Die();
                }
            }
        }

        public bool Dead { get; private set; }
        public float Throttle { get; private set; }
        public Vector3 EffectiveInput { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 LocalVelocity { get; private set; }
        public Vector3 LocalGForce { get; private set; }
        public Vector3 LocalAngularVelocity { get; private set; }
        public float AngleOfAttack { get; private set; }
        public float AngleOfAttackYaw { get; private set; }
        public bool AirbrakeDeployed { get; private set; }

        public bool FlapsDeployed {
            get { return flapsDeployed; }
            private set {
                flapsDeployed = value;
                foreach (var lg in landingGear) {
                    lg.enabled = value;
                }
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
            SetupRigidbody();
        }

        void Start() {
            if (landingGear.Count > 0) {
                landingGearDefaultMaterial = landingGear[0].sharedMaterial;
            }

            m_Rigidbody.linearVelocity = m_Rigidbody.rotation * new Vector3(0, 0, initialSpeed);
            SetUpLeverAndJoystick();
        }

        private void FixedUpdate() {
            if (!m_EngineOn) return;

            float dt = Time.fixedDeltaTime;

            CalculateState(dt);
            CalculateGForce(dt);
            UpdateFlaps();
            UpdateThrottle(dt);

            if (!Dead) {
                UpdateThrust();
                UpdateLift();
                UpdateSteering(dt);
            }
            else {
                Vector3 up = m_Rigidbody.rotation * Vector3.up;
                Vector3 forward = m_Rigidbody.linearVelocity.normalized;
                m_Rigidbody.rotation = Quaternion.LookRotation(forward, up);
            }

            UpdateDrag();
            UpdateAngularDrag();
            CalculateState(dt);
            UpdateXRPosition();
        }

        private void OnDestroy() {
            if (throttleLever != null)
                throttleLever.OnValueChange.RemoveListener(OnThrottleInput);

            if (flightStick != null)
                flightStick.OnJoystickMove.RemoveListener(OnJoystickInput);
        }

        private void OnCollisionEnter(Collision collision) {
            for (int i = 0; i < collision.contactCount; i++) {
                var contact = collision.contacts[i];

                if (landingGear.Contains(contact.thisCollider)) {
                    return;
                }

                Health = 0;

                m_Rigidbody.isKinematic = true;
                m_Rigidbody.position = contact.point;
                m_Rigidbody.rotation = Quaternion.Euler(0, m_Rigidbody.rotation.eulerAngles.y, 0);

                foreach (var go in graphics) {
                    go.SetActive(false);
                }

                return;
            }
        }
        #endregion

        #region VR Controls Setup
        private void SetUpLeverAndJoystick() {
            if (throttleLever != null) {
                throttleLever.SetMovingParent(transform);
                throttleLever.OnValueChange.AddListener(OnThrottleInput);
            }

            if (flightStick != null) {
                flightStick.OnJoystickMove.AddListener(OnJoystickInput);
            }

            Debug.Log("Controles VR configurados correctamente");
        }

        private void OnThrottleInput(float input) {
            m_ThrottleInput = Mathf.Clamp01(input);
            SetThrottleInput(m_ThrottleInput);
        }

        private void OnJoystickInput(Vector2 input) {
            input *= controlSensitivity;

            if (invertPitch) input.y = -input.y;
            if (invertRoll) input.x = -input.x;

            Vector3 flightInput = new Vector3(input.y, 0, input.x);
            SetControlInput(flightInput);
        }
        #endregion

        #region Flight Physics Core
        private void SetupRigidbody() {
            m_Rigidbody.linearDamping = 0.2f;
            m_Rigidbody.angularDamping = 1.5f;
            m_Rigidbody.useGravity = true;
        }

        private void CalculateState(float dt) {
            var invRotation = Quaternion.Inverse(m_Rigidbody.rotation);
            Velocity = m_Rigidbody.linearVelocity;
            LocalVelocity = invRotation * Velocity;
            LocalAngularVelocity = invRotation * m_Rigidbody.angularVelocity;
            CalculateAngleOfAttack();
        }

        private void CalculateAngleOfAttack() {
            if (LocalVelocity.sqrMagnitude < 0.1f) {
                AngleOfAttack = 0;
                AngleOfAttackYaw = 0;
                return;
            }

            AngleOfAttack = Mathf.Atan2(-LocalVelocity.y, LocalVelocity.z);
            AngleOfAttackYaw = Mathf.Atan2(LocalVelocity.x, LocalVelocity.z);
        }

        private void CalculateGForce(float dt) {
            var invRotation = Quaternion.Inverse(m_Rigidbody.rotation);
            var acceleration = (Velocity - lastVelocity) / dt;
            LocalGForce = invRotation * acceleration;
            lastVelocity = Velocity;
        }
        #endregion

        #region Thrust & Throttle Control
        public void SetThrottleInput(float input) {
            if (Dead) return;
            throttleInput = input;
        }

        private void UpdateThrottle(float dt) {
            float target = 0;
            if (throttleInput > 0) target = 1;

            Throttle = MoveTo(Throttle, target, throttleSpeed * Mathf.Abs(throttleInput), dt);
            AirbrakeDeployed = Throttle == 0 && throttleInput == -1;

            if (AirbrakeDeployed) {
                foreach (var lg in landingGear) {
                    lg.sharedMaterial = landingGearBrakesMaterial;
                }
            }
            else {
                foreach (var lg in landingGear) {
                    lg.sharedMaterial = landingGearDefaultMaterial;
                }
            }
        }

        private void UpdateThrust() {
            m_Rigidbody.AddRelativeForce(Throttle * maxThrust * Vector3.forward);
        }
        #endregion

        #region Steering & Control
        public void SetControlInput(Vector3 input) {
            if (Dead) return;
            controlInput = Vector3.ClampMagnitude(input, 1);
        }

        private void UpdateSteering(float dt) {
            var speed = Mathf.Max(0, LocalVelocity.z);
            var steeringPower = steeringCurve.Evaluate(speed);

            var gForceScaling = CalculateGLimiter(controlInput, turnSpeed * Mathf.Deg2Rad * steeringPower);
            var targetAV = Vector3.Scale(controlInput, turnSpeed * steeringPower * gForceScaling);
            var av = LocalAngularVelocity * Mathf.Rad2Deg;

            var correction = new Vector3(
                CalculateSteering(dt, av.x, targetAV.x, turnAcceleration.x * steeringPower),
                CalculateSteering(dt, av.y, targetAV.y, turnAcceleration.y * steeringPower),
                CalculateSteering(dt, av.z, targetAV.z, turnAcceleration.z * steeringPower)
            );

            m_Rigidbody.AddRelativeTorque(correction * Mathf.Deg2Rad, ForceMode.VelocityChange);

            var correctionInput = new Vector3(
                Mathf.Clamp((targetAV.x - av.x) / turnAcceleration.x, -1, 1),
                Mathf.Clamp((targetAV.y - av.y) / turnAcceleration.y, -1, 1),
                Mathf.Clamp((targetAV.z - av.z) / turnAcceleration.z, -1, 1)
            );

            var effectiveInput = (correctionInput + controlInput) * gForceScaling;
            EffectiveInput = new Vector3(
                Mathf.Clamp(effectiveInput.x, -1, 1),
                Mathf.Clamp(effectiveInput.y, -1, 1),
                Mathf.Clamp(effectiveInput.z, -1, 1)
            );
        }

        private float CalculateSteering(float dt, float angularVelocity, float targetVelocity, float acceleration) {
            var error = targetVelocity - angularVelocity;
            var accel = acceleration * dt;
            return Mathf.Clamp(error, -accel, accel);
        }

        private Vector3 CalculateGForceLimit(Vector3 input) {
            return Scale6(input,
                gLimit, gLimitPitch,
                gLimit, gLimit,
                gLimit, gLimit
            ) * 9.81f;
        }

        private float CalculateGLimiter(Vector3 controlInput, Vector3 maxAngularVelocity) {
            if (controlInput.magnitude < 0.01f) {
                return 1;
            }

            var maxInput = controlInput.normalized;
            var limit = CalculateGForceLimit(maxInput);
            var maxGForce = Vector3.Cross(Vector3.Scale(maxInput, maxAngularVelocity), LocalVelocity);

            if (maxGForce.magnitude > limit.magnitude) {
                return limit.magnitude / maxGForce.magnitude;
            }

            return 1;
        }
        #endregion

        #region Aerodynamics & Lift
        private void UpdateLift() {
            if (LocalVelocity.sqrMagnitude < 1f) return;

            float flapsLiftPower = FlapsDeployed ? this.flapsLiftPower : 0;
            float flapsAOABias = FlapsDeployed ? this.flapsAOABias : 0;

            var liftForce = CalculateLift(
                AngleOfAttack + (flapsAOABias * Mathf.Deg2Rad), Vector3.right,
                liftPower + flapsLiftPower,
                liftAOACurve,
                inducedDragCurve
            );

            var yawForce = CalculateLift(AngleOfAttackYaw, Vector3.up, rudderPower, rudderAOACurve, rudderInducedDragCurve);

            m_Rigidbody.AddRelativeForce(liftForce);
            m_Rigidbody.AddRelativeForce(yawForce);
        }

        private Vector3 CalculateLift(float angleOfAttack, Vector3 rightAxis, float liftPower, AnimationCurve aoaCurve, AnimationCurve inducedDragCurve) {
            var liftVelocity = Vector3.ProjectOnPlane(LocalVelocity, rightAxis);
            var v2 = liftVelocity.sqrMagnitude;

            var liftCoefficient = aoaCurve.Evaluate(angleOfAttack * Mathf.Rad2Deg);
            var liftForce = v2 * liftCoefficient * liftPower;

            var liftDirection = Vector3.Cross(liftVelocity.normalized, rightAxis);
            var lift = liftDirection * liftForce;

            var dragForce = liftCoefficient * liftCoefficient;
            var dragDirection = -liftVelocity.normalized;
            var inducedDrag = dragDirection * v2 * dragForce * this.inducedDrag * inducedDragCurve.Evaluate(Mathf.Max(0, LocalVelocity.z));

            return lift + inducedDrag;
        }
        #endregion

        #region Drag System
        private void UpdateDrag() {
            var lv = LocalVelocity;
            var lv2 = lv.sqrMagnitude;

            float airbrakeDrag = AirbrakeDeployed ? this.airbrakeDrag : 0;
            float flapsDrag = FlapsDeployed ? this.flapsDrag : 0;

            var coefficient = Scale6(
                lv.normalized,
                dragRight.Evaluate(Mathf.Abs(lv.x)), dragLeft.Evaluate(Mathf.Abs(lv.x)),
                dragTop.Evaluate(Mathf.Abs(lv.y)), dragBottom.Evaluate(Mathf.Abs(lv.y)),
                dragForward.Evaluate(Mathf.Abs(lv.z)) + airbrakeDrag + flapsDrag,
                dragBack.Evaluate(Mathf.Abs(lv.z))
            );

            var drag = coefficient.magnitude * lv2 * -lv.normalized;
            m_Rigidbody.AddRelativeForce(drag);
        }

        private void UpdateAngularDrag() {
            var av = LocalAngularVelocity;
            var drag = av.sqrMagnitude * -av.normalized;
            m_Rigidbody.AddRelativeTorque(Vector3.Scale(drag, angularDrag), ForceMode.Acceleration);
        }
        #endregion

        #region Flaps & Landing Gear
        private void UpdateFlaps() {
            if (LocalVelocity.z > flapsRetractSpeed) {
                FlapsDeployed = false;
            }
        }

        public void ToggleFlaps() {
            if (LocalVelocity.z < flapsRetractSpeed) {
                FlapsDeployed = !FlapsDeployed;
            }
        }
        #endregion

        #region VR Integration
        private void UpdateXRPosition() {
            if (xrOrigin == null || pilotSeat == null) return;

            xrOrigin.position = Vector3.Lerp(xrOrigin.position, pilotSeat.position, Time.deltaTime * xrSmoothness);
            xrOrigin.rotation = Quaternion.Slerp(xrOrigin.rotation, pilotSeat.rotation, Time.deltaTime * (xrSmoothness / 2f));
        }
        #endregion

        #region Health & Damage System
        public void ApplyDamage(float damage) {
            Health -= damage;
        }

        private void Die() {
            throttleInput = 0;
            Throttle = 0;
            Dead = true;

            if (damageEffect != null) {
                var ps = damageEffect.GetComponent<ParticleSystem>();
                if (ps != null) ps.Pause();
            }
            if (deathEffect != null) deathEffect.SetActive(true);
        }
        #endregion

        #region Public Methods
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

        public void RestartLevel() {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        #endregion

        #region Utility Methods
        private float MoveTo(float current, float target, float speed, float dt) {
            if (current < target)
                return Mathf.Min(current + speed * dt, target);
            else if (current > target)
                return Mathf.Max(current - speed * dt, target);
            return target;
        }

        private Vector3 Scale6(Vector3 value, float posX, float negX, float posY, float negY, float posZ, float negZ) {
            Vector3 result = value;

            if (result.x > 0) result.x *= posX;
            else if (result.x < 0) result.x *= negX;

            if (result.y > 0) result.y *= posY;
            else if (result.y < 0) result.y *= negY;

            if (result.z > 0) result.z *= posZ;
            else if (result.z < 0) result.z *= negZ;

            return result;
        }
        #endregion
    }
}