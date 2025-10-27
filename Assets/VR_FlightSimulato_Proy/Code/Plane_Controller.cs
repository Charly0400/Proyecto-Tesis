using MikeNspired.XRIStarterKit;
using System.Collections.Generic;
using UnityEngine;

namespace Cahrly.FlightController {
    [RequireComponent(typeof(Rigidbody))]
    public class Plane_Controller : MonoBehaviour {
        #region Inspector Fields - Thrust / Lift / Steering / Drag
        [Header("Thrust")]
        [SerializeField] float maxThrust = 15000f;
        [SerializeField] float throttleSpeed = 0.5f;
        [SerializeField] float initialSpeed = 0f;

        [Header("Lift")]
        [SerializeField] float liftPower = 15f;
        [SerializeField] AnimationCurve liftAOACurve;
        [SerializeField] float inducedDrag = 0.1f;
        [SerializeField] AnimationCurve inducedDragCurve;
        [SerializeField] float rudderPower = 2f;
        [SerializeField] AnimationCurve rudderAOACurve;
        [SerializeField] AnimationCurve rudderInducedDragCurve;
        [SerializeField] float flapsLiftPower = 5f;
        [SerializeField] float flapsAOABias = 2f;
        [SerializeField] float flapsDrag = 1f;
        [SerializeField] float flapsRetractSpeed = 20f;

        [Header("Steering")]
        [SerializeField] Vector3 turnSpeed = new Vector3(40f, 40f, 40f);
        [SerializeField] Vector3 turnAcceleration = new Vector3(60f, 60f, 60f);
        [SerializeField] AnimationCurve steeringCurve;

        [Header("Drag (curves)")]
        [SerializeField] AnimationCurve dragForward;
        [SerializeField] AnimationCurve dragBack;
        [SerializeField] AnimationCurve dragLeft;
        [SerializeField] AnimationCurve dragRight;
        [SerializeField] AnimationCurve dragTop;
        [SerializeField] AnimationCurve dragBottom;
        [SerializeField] Vector3 angularDrag = new Vector3(1f, 1f, 1f);
        [SerializeField] float airbrakeDrag = 3f;

        [Header("G-Limits")]
        [SerializeField] float gLimit = 8f;
        [SerializeField] float gLimitPitch = 6f;

        [Header("Misc")]
        [SerializeField] List<Collider> landingGear = new List<Collider>();
        [SerializeField] PhysicsMaterial landingGearBrakesMaterial;
        [SerializeField] bool flapsDeployed = false;

        [Header("VR Controls")]
        [SerializeField] ThrottleXR throttleLever;
        [SerializeField] JoystickXR flightStick;

        [Header("XR Integration")]
        public Transform xrOrigin;
        public Transform pilotSeat;
        public float xrSmoothness = 5f;

        [Header("Control Settings")]
        public bool invertPitch = false;
        public bool invertRoll = false;
        public float controlSensitivity = 1.0f;
        #endregion

        #region State
        Rigidbody m_Rigidbody;

        // Inputs
        float throttleInput = 0f;                // [-1,1] expected (throttle lever typical 0..1)
        Vector3 controlInput = Vector3.zero;    // (pitch, yaw, roll) in [-1,1]

        // Plane state readable desde fuera
        public float Throttle { get; private set; } = 0f;
        public Vector3 EffectiveInput { get; private set; } = Vector3.zero;
        public Vector3 Velocity { get; private set; } = Vector3.zero;
        public Vector3 LocalVelocity { get; private set; } = Vector3.zero;
        public Vector3 LocalGForce { get; private set; } = Vector3.zero;
        public Vector3 LocalAngularVelocity { get; private set; } = Vector3.zero;
        public float AngleOfAttack { get; private set; } = 0f;
        public float AngleOfAttackYaw { get; private set; } = 0f;
        public bool AirbrakeDeployed { get; private set; } = false;

        Vector3 lastVelocity = Vector3.zero;
        PhysicsMaterial landingGearDefaultMaterial;
        bool engineOn = false;
        #endregion

        #region Unity lifecycle
        void Awake() {
            m_Rigidbody = GetComponent<Rigidbody>();
            SetupRigidbody();
        }

        void Start() {
            SetUpLeverAndJoystick();
            if (landingGear.Count > 0) landingGearDefaultMaterial = landingGear[0].sharedMaterial;
            if (initialSpeed != 0f) m_Rigidbody.linearVelocity = m_Rigidbody.rotation * new Vector3(0, 0, initialSpeed);
        }

        void Update() {
            UpdateXRPosition();
        }

        void FixedUpdate() {
            float dt = Time.fixedDeltaTime;

            CalculateState(dt);
            CalculateGForce(dt);
            UpdateFlaps();

            UpdateThrottle(dt);

            if (engineOn) {
                UpdateThrust();
                UpdateLift();
                UpdateSteering(dt);
            }

            UpdateDrag();
            UpdateAngularDrag();

            CalculateState(dt);
        }
        #endregion

        #region Rigidbody setup & VR wiring
        void SetupRigidbody() {
            m_Rigidbody.linearDamping = 0.2f;
            m_Rigidbody.angularDamping = 1.5f;
            m_Rigidbody.useGravity = true;
        }

        void SetUpLeverAndJoystick() {
            if (throttleLever != null) {
                throttleLever.SetMovingParent(transform);
                throttleLever.OnValueChange.AddListener(OnThrottleInput);
            }
            if (flightStick != null) {
                flightStick.OnJoystickMove.AddListener(OnJoystickInput);
            }
        }

        private void OnThrottleInput(float input) {
            throttleInput = Mathf.Clamp(input, -1f, 1f);
        }

        private void OnJoystickInput(Vector2 input) {
            Vector2 processed = input * controlSensitivity;
            if (invertPitch) processed.y = -processed.y;
            if (invertRoll) processed.x = -processed.x;

            float pitch = Mathf.Clamp(processed.y, -1f, 1f);
            float roll = Mathf.Clamp(-processed.x, -1f, 1f);
            controlInput = new Vector3(pitch, 0f, roll);
        }

        void UpdateXRPosition() {
            if (xrOrigin == null || pilotSeat == null) return;
            xrOrigin.position = Vector3.Lerp(xrOrigin.position, pilotSeat.position, Time.deltaTime * xrSmoothness);
            xrOrigin.rotation = Quaternion.Slerp(xrOrigin.rotation, pilotSeat.rotation, Time.deltaTime * (xrSmoothness * 0.5f));
        }
        #endregion

        #region API - external setters
        public void SetThrottleInput(float input) {
            throttleInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void SetControlInput(Vector3 input) {
            controlInput = Vector3.ClampMagnitude(input, 1f);
        }

        public void SetDirectionInput(Vector2 input2D) {
            float pitch = Mathf.Clamp(input2D.y * controlSensitivity, -1f, 1f);
            float roll = Mathf.Clamp(-input2D.x * controlSensitivity, -1f, 1f);
            controlInput = new Vector3(pitch, 0f, roll);
        }

        public void EngineState(int state) {
            if (state == 0) StopEngine();
            else StartEngine();
        }

        public void StartEngine() {
            if (engineOn) return;
            engineOn = true;
            Debug.Log("[Plane_Controller] Motor encendido");
        }

        public void StopEngine() {
            engineOn = false;
            throttleInput = 0f;
            Debug.Log("[Plane_Controller] Motor apagado");
        }

        public void ToggleFlaps() {
            if (LocalVelocity.z < flapsRetractSpeed) FlapsDeployed = !FlapsDeployed;
        }

        public bool FlapsDeployed {
            get { return flapsDeployed; }
            private set {
                flapsDeployed = value;
                foreach (var lg in landingGear) if (lg != null) lg.enabled = value;
            }
        }

        void OnDestroy() {
            if (throttleLever != null) throttleLever.OnValueChange.RemoveListener(OnThrottleInput);
            if (flightStick != null) flightStick.OnJoystickMove.RemoveListener(OnJoystickInput);
        }
        #endregion

        #region Physics helpers - Throttle / Flaps
        void UpdateThrottle(float dt) {
            float target = 0f;
            if (throttleInput > 0f) target = 1f;

            Throttle = MoveTo(Throttle, target, throttleSpeed * Mathf.Abs(throttleInput), dt);

            AirbrakeDeployed = Throttle == 0f && throttleInput == -1f;

            if (AirbrakeDeployed) {
                foreach (var lg in landingGear) if (lg != null) lg.sharedMaterial = landingGearBrakesMaterial;
            }
            else {
                foreach (var lg in landingGear) if (lg != null && landingGearDefaultMaterial != null) lg.sharedMaterial = landingGearDefaultMaterial;
            }
        }

        void UpdateFlaps() {
            if (LocalVelocity.z > flapsRetractSpeed) FlapsDeployed = false;
        }
        #endregion

        #region State calculations (AOA / velocities / g-forces)
        void CalculateAngleOfAttack() {
            if (LocalVelocity.sqrMagnitude < 0.1f) {
                AngleOfAttack = 0f; AngleOfAttackYaw = 0f; return;
            }
            AngleOfAttack = Mathf.Atan2(-LocalVelocity.y, LocalVelocity.z);
            AngleOfAttackYaw = Mathf.Atan2(LocalVelocity.x, LocalVelocity.z);
        }

        void CalculateGForce(float dt) {
            var invRotation = Quaternion.Inverse(m_Rigidbody.rotation);
            var acceleration = (Velocity - lastVelocity) / Mathf.Max(dt, 1e-6f);
            LocalGForce = invRotation * acceleration;
            lastVelocity = Velocity;
        }

        void CalculateState(float dt) {
            var invRotation = Quaternion.Inverse(m_Rigidbody.rotation);
            Velocity = m_Rigidbody.linearVelocity;
            LocalVelocity = invRotation * Velocity;
            LocalAngularVelocity = invRotation * m_Rigidbody.angularVelocity;
            CalculateAngleOfAttack();
        }
        #endregion

        #region Forces: Thrust / Drag / Lift / AngularDrag
        void UpdateThrust() {
            m_Rigidbody.AddRelativeForce(Throttle * maxThrust * Vector3.forward);
        }

        void UpdateDrag() {
            var lv = LocalVelocity;
            if (lv.sqrMagnitude < 1e-6f) return;

            var lv2 = lv.sqrMagnitude;
            float airbrakeDragLocal = AirbrakeDeployed ? airbrakeDrag : 0f;
            float flapsDragLocal = FlapsDeployed ? flapsDrag : 0f;

            var coefficient = Scale6(
                lv.normalized,
                dragRight.Evaluate(Mathf.Abs(lv.x)), dragLeft.Evaluate(Mathf.Abs(lv.x)),
                dragTop.Evaluate(Mathf.Abs(lv.y)), dragBottom.Evaluate(Mathf.Abs(lv.y)),
                dragForward.Evaluate(Mathf.Abs(lv.z)) + airbrakeDragLocal + flapsDragLocal,
                dragBack.Evaluate(Mathf.Abs(lv.z))
            );

            var drag = coefficient.magnitude * lv2 * -lv.normalized;
            m_Rigidbody.AddRelativeForce(drag);
        }

        Vector3 CalculateLift(float angleOfAttack, Vector3 rightAxis, float liftPowerLocal, AnimationCurve aoaCurve, AnimationCurve inducedDragCurveLocal) {
            var liftVelocity = Vector3.ProjectOnPlane(LocalVelocity, rightAxis);
            var v2 = liftVelocity.sqrMagnitude;
            if (v2 < Mathf.Epsilon) return Vector3.zero;

            var liftCoefficient = aoaCurve.Evaluate(angleOfAttack * Mathf.Rad2Deg);
            var liftForce = v2 * liftCoefficient * liftPowerLocal;
            var liftDirection = Vector3.Cross(liftVelocity.normalized, rightAxis);
            var lift = liftDirection * liftForce;

            var dragForce = liftCoefficient * liftCoefficient;
            var induced = -liftVelocity.normalized * v2 * dragForce * inducedDrag * inducedDragCurveLocal.Evaluate(Mathf.Max(0, LocalVelocity.z));
            return lift + induced;
        }

        void UpdateLift() {
            if (LocalVelocity.sqrMagnitude < 1f) return;

            float flapsLiftPowerLocal = FlapsDeployed ? flapsLiftPower : 0f;
            float flapsAOABiasLocal = FlapsDeployed ? flapsAOABias : 0f;

            var liftForce = CalculateLift(
                AngleOfAttack + (flapsAOABiasLocal * Mathf.Deg2Rad), Vector3.right,
                liftPower + flapsLiftPowerLocal,
                liftAOACurve,
                inducedDragCurve
            );

            var yawForce = CalculateLift(AngleOfAttackYaw, Vector3.up, rudderPower, rudderAOACurve, rudderInducedDragCurve);

            m_Rigidbody.AddRelativeForce(liftForce);
            m_Rigidbody.AddRelativeForce(yawForce);
        }

        void UpdateAngularDrag() {
            var av = LocalAngularVelocity;
            if (av.sqrMagnitude < 1e-6f) return;
            var drag = av.sqrMagnitude * -av.normalized;
            m_Rigidbody.AddRelativeTorque(Vector3.Scale(drag, angularDrag), ForceMode.Acceleration);
        }
        #endregion

        #region Steering (torque / g-limiter / control)
        Vector3 CalculateGForce(Vector3 angularVelocity, Vector3 velocity) {
            return Vector3.Cross(angularVelocity, velocity);
        }

        Vector3 CalculateGForceLimit(Vector3 input) {
            return Scale6(input,
                gLimit, gLimitPitch,
                gLimit, gLimit,
                gLimit, gLimit
            ) * 9.81f;
        }

        float CalculateGLimiter(Vector3 controlInputLocal, Vector3 maxAngularVelocity) {
            if (controlInputLocal.magnitude < 0.01f) return 1f;
            var maxInput = controlInputLocal.normalized;
            var limit = CalculateGForceLimit(maxInput);
            var maxGForce = CalculateGForce(Vector3.Scale(maxInput, maxAngularVelocity), LocalVelocity);
            if (maxGForce.magnitude > limit.magnitude) return limit.magnitude / maxGForce.magnitude;
            return 1f;
        }

        float CalculateSteering(float dt, float angularVelocity, float targetVelocity, float acceleration) {
            var error = targetVelocity - angularVelocity;
            var accel = acceleration * dt;
            return Mathf.Clamp(error, -accel, accel);
        }

        void UpdateSteering(float dt) {
            var speed = Mathf.Max(0f, LocalVelocity.z);
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
                Mathf.Clamp((targetAV.x - av.x) / (turnAcceleration.x + Mathf.Epsilon), -1f, 1f),
                Mathf.Clamp((targetAV.y - av.y) / (turnAcceleration.y + Mathf.Epsilon), -1f, 1f),
                Mathf.Clamp((targetAV.z - av.z) / (turnAcceleration.z + Mathf.Epsilon), -1f, 1f)
            );

            var effective = (correctionInput + controlInput) * gForceScaling;

            EffectiveInput = new Vector3(
                Mathf.Clamp(effective.x, -1f, 1f),
                Mathf.Clamp(effective.y, -1f, 1f),
                Mathf.Clamp(effective.z, -1f, 1f)
            );
        }
        #endregion

        #region Collision
        void OnCollisionEnter(Collision collision) {
            for (int i = 0; i < collision.contactCount; i++) {
                var contact = collision.contacts[i];
                if (landingGear.Contains(contact.thisCollider)) return;

                // Simula destrucción por colisión fuerte
                // (si quieres mantener effects/graphics, re-intégralos aquí)
                m_Rigidbody.isKinematic = true;
                m_Rigidbody.position = contact.point;
                m_Rigidbody.rotation = Quaternion.Euler(0, m_Rigidbody.rotation.eulerAngles.y, 0);
                return;
            }
        }
        #endregion

        #region Local helper methods (replacements for missing Utilities)
        // Replaces Utilities.MoveTo(current, target, rate, dt)
        private static float MoveTo(float current, float target, float rate, float dt) {
            return Mathf.MoveTowards(current, target, rate * dt);
        }

        // Replaces Utilities.Scale6(dir, right, left, top, bottom, forward, back)
        // Returns a Vector3 where each component is the weighted coefficient for that axis.
        private static Vector3 Scale6(Vector3 dir, float right, float left, float top, float bottom, float forward, float back) {
            // dir expected normalized (but we guard anyway)
            Vector3 n = dir;
            float nx = n.x;
            float ny = n.y;
            float nz = n.z;

            float cx = nx >= 0f ? right * nx : left * -nx;
            float cy = ny >= 0f ? top * ny : bottom * -ny;
            float cz = nz >= 0f ? forward * nz : back * -nz;

            return new Vector3(cx, cy, cz);
        }
        #endregion
    }
}
