using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Plane_Controller : MonoBehaviour {
    [Header("Basic Flight Parameters")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float health = 100f;
    [SerializeField] float maxThrust = 100f;
    [SerializeField] float throttleSpeed = 0.5f;
    [SerializeField] float gLimit = 8f;
    [SerializeField] float gLimitPitch = 6f;

    [Header("Lift & Aerodynamics")]
    [SerializeField] float liftPower = 0.5f;
    [SerializeField] AnimationCurve liftAOACurve;
    [SerializeField] float inducedDrag = 0.01f;
    [SerializeField] AnimationCurve inducedDragCurve;
    [SerializeField] float rudderPower = 0.1f;
    [SerializeField] AnimationCurve rudderAOACurve;
    [SerializeField] AnimationCurve rudderInducedDragCurve;

    [Header("Steering")]
    [SerializeField] Vector3 turnSpeed = new Vector3(90f, 25f, 45f);
    [SerializeField] Vector3 turnAcceleration = new Vector3(180f, 90f, 120f);
    [SerializeField] AnimationCurve steeringCurve;

    [Header("Drag")]
    [SerializeField] AnimationCurve dragForward;
    [SerializeField] AnimationCurve dragBack;
    [SerializeField] AnimationCurve dragLeft;
    [SerializeField] AnimationCurve dragRight;
    [SerializeField] AnimationCurve dragTop;
    [SerializeField] AnimationCurve dragBottom;
    [SerializeField] Vector3 angularDrag = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] float airbrakeDrag = 2f;

    [Header("VR Hand Controls")]
    [SerializeField] private XRGrabInteractable flightStick;
    [SerializeField] private XRGrabInteractable throttleLever;
    [SerializeField] private Transform flightStickTransform;
    [SerializeField] private Transform throttleTransform;
    [SerializeField] private float stickSensitivity = 2.0f;
    [SerializeField] private float throttleSensitivity = 1.5f;
    [SerializeField] private Vector2 stickDeadzone = new Vector2(0.05f, 0.05f);

    [Header("Input System Testing")]
    [SerializeField] private bool useKeyboardInput = false;
    [SerializeField] private float keyboardSensitivity = 1.0f;

    [Header("Misc")]
    [SerializeField] float initialSpeed = 50f;
    [SerializeField] List<GameObject> graphics;
    [SerializeField] GameObject damageEffect;
    [SerializeField] GameObject deathEffect;

    // Private variables
    private float throttleInput;
    private Vector3 controlInput;
    private Vector3 lastVelocity;
    private Vector3 flightStickNeutralPos;
    private Quaternion flightStickNeutralRot;
    private Vector3 throttleNeutralPos;
    private bool isFlightStickGrabbed = false;
    private bool isThrottleGrabbed = false;
    private Vector3 smoothedControlInput = Vector3.zero;

    // Input System
    private PlayerInput playerInput;
    private InputAction pitchAction;
    private InputAction rollAction;
    private InputAction yawAction;
    private InputAction throttleAction;

    public Rigidbody Rigidbody { get; private set; }
    public float Throttle { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector3 LocalVelocity { get; private set; }
    public Vector3 LocalGForce { get; private set; }
    public Vector3 LocalAngularVelocity { get; private set; }
    public float AngleOfAttack { get; private set; }
    public float AngleOfAttackYaw { get; private set; }
    public bool AirbrakeDeployed { get; private set; }
    public bool Dead { get; private set; }

    void Start() {
        Rigidbody = GetComponent<Rigidbody>();

        if (useKeyboardInput) {
            SetupInputSystem();
        }
        else {
            SetupVRControls();
        }

        Rigidbody.linearVelocity = Rigidbody.rotation * new Vector3(0, 0, initialSpeed);
    }

    void SetupVRControls() {
        if (flightStick != null) {
            flightStickNeutralPos = flightStickTransform.localPosition;
            flightStickNeutralRot = flightStickTransform.localRotation;

            flightStick.selectEntered.AddListener(OnFlightStickGrabbed);
            flightStick.selectExited.AddListener(OnFlightStickReleased);
        }

        if (throttleLever != null) {
            throttleNeutralPos = throttleTransform.localPosition;

            throttleLever.selectEntered.AddListener(OnThrottleGrabbed);
            throttleLever.selectExited.AddListener(OnThrottleReleased);
        }
    }

    void SetupInputSystem() {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null) {
            playerInput = gameObject.AddComponent<PlayerInput>();
        }

        // Configurar acciones de input
        pitchAction = new InputAction("Pitch", InputActionType.Value, "<Keyboard>/w,<Keyboard>/s");
        rollAction = new InputAction("Roll", InputActionType.Value, "<Keyboard>/a,<Keyboard>/d");
        yawAction = new InputAction("Yaw", InputActionType.Value, "<Keyboard>/q,<Keyboard>/e");
        throttleAction = new InputAction("Throttle", InputActionType.Value, "<Keyboard>/upArrow,<Keyboard>/downArrow");

        pitchAction.AddCompositeBinding("Axis")
            .With("Positive", "<Keyboard>/w")
            .With("Negative", "<Keyboard>/s");

        rollAction.AddCompositeBinding("Axis")
            .With("Positive", "<Keyboard>/d")
            .With("Negative", "<Keyboard>/a");

        yawAction.AddCompositeBinding("Axis")
            .With("Positive", "<Keyboard>/e")
            .With("Negative", "<Keyboard>/q");

        throttleAction.AddCompositeBinding("Axis")
            .With("Positive", "<Keyboard>/upArrow")
            .With("Negative", "<Keyboard>/downArrow");

        pitchAction.Enable();
        rollAction.Enable();
        yawAction.Enable();
        throttleAction.Enable();
    }

    void OnFlightStickGrabbed(SelectEnterEventArgs args) {
        isFlightStickGrabbed = true;
    }

    void OnFlightStickReleased(SelectExitEventArgs args) {
        isFlightStickGrabbed = false;
    }

    void OnThrottleGrabbed(SelectEnterEventArgs args) {
        isThrottleGrabbed = true;
    }

    void OnThrottleReleased(SelectExitEventArgs args) {
        isThrottleGrabbed = false;
    }

    void UpdateInputs() {
        if (useKeyboardInput) {
            UpdateKeyboardInput();
        }
        else {
            UpdateVRHandControls();
        }
    }

    void UpdateVRHandControls() {
        UpdateFlightStickControl();
        UpdateThrottleControl();
    }

    void UpdateKeyboardInput() {
        // Leer inputs del teclado
        float pitch = pitchAction.ReadValue<float>();
        float roll = rollAction.ReadValue<float>();
        float yaw = yawAction.ReadValue<float>();
        float throttle = throttleAction.ReadValue<float>();

        // Aplicar sensibilidad y asignar inputs
        controlInput = new Vector3(pitch, yaw, roll) * keyboardSensitivity;
        throttleInput = throttle;
    }

    void UpdateFlightStickControl() {
        if (!isFlightStickGrabbed || flightStickTransform == null) {
            smoothedControlInput = Vector3.Lerp(smoothedControlInput, Vector3.zero, Time.deltaTime * 5f);
            return;
        }

        Quaternion localRotOffset = flightStickTransform.localRotation * Quaternion.Inverse(flightStickNeutralRot);
        Vector3 eulerOffset = localRotOffset.eulerAngles;

        float pitch = NormalizeAngle(eulerOffset.x);
        float roll = NormalizeAngle(eulerOffset.z);
        float yaw = NormalizeAngle(eulerOffset.y);

        Vector3 rawInput = new Vector3(
            ApplyDeadzone(pitch / 45f, stickDeadzone.y),
            ApplyDeadzone(yaw / 45f, stickDeadzone.x),
            ApplyDeadzone(roll / 45f, stickDeadzone.x)
        );

        rawInput = Vector3.ClampMagnitude(rawInput * stickSensitivity, 1f);
        smoothedControlInput = Vector3.Lerp(smoothedControlInput, rawInput, Time.deltaTime * 8f);
        controlInput = smoothedControlInput;
    }

    void UpdateThrottleControl() {
        if (!isThrottleGrabbed || throttleTransform == null) {
            return;
        }

        float verticalOffset = throttleTransform.localPosition.y - throttleNeutralPos.y;
        float rawThrottleInput = Mathf.Clamp(verticalOffset * throttleSensitivity, -1f, 1f);
        throttleInput = rawThrottleInput;
    }

    float ApplyDeadzone(float value, float deadzone) {
        if (Mathf.Abs(value) < deadzone) return 0f;
        return Mathf.Sign(value) * (Mathf.Abs(value) - deadzone) / (1f - deadzone);
    }

    float NormalizeAngle(float angle) {
        angle = angle % 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    void UpdateThrottle(float dt) {
        float target = 0;
        if (throttleInput > 0) target = 1;

        Throttle = Mathf.MoveTowards(Throttle, target, throttleSpeed * Mathf.Abs(throttleInput) * dt);
        AirbrakeDeployed = Throttle == 0 && throttleInput == -1;
    }

    void CalculateAngleOfAttack() {
        if (LocalVelocity.sqrMagnitude < 0.1f) {
            AngleOfAttack = 0;
            AngleOfAttackYaw = 0;
            return;
        }

        AngleOfAttack = Mathf.Atan2(-LocalVelocity.y, LocalVelocity.z);
        AngleOfAttackYaw = Mathf.Atan2(LocalVelocity.x, LocalVelocity.z);
    }

    void CalculateGForce(float dt) {
        var invRotation = Quaternion.Inverse(Rigidbody.rotation);
        var acceleration = (Velocity - lastVelocity) / dt;
        LocalGForce = invRotation * acceleration;
        lastVelocity = Velocity;
    }

    void CalculateState(float dt) {
        var invRotation = Quaternion.Inverse(Rigidbody.rotation);
        Velocity = Rigidbody.linearVelocity;
        LocalVelocity = invRotation * Velocity;
        LocalAngularVelocity = invRotation * Rigidbody.angularVelocity;
        CalculateAngleOfAttack();
    }

    void UpdateThrust() {
        Rigidbody.AddRelativeForce(Throttle * maxThrust * Vector3.forward);
    }

    void UpdateDrag() {
        var lv = LocalVelocity;
        var lv2 = lv.sqrMagnitude;

        float airbrakeDrag = AirbrakeDeployed ? this.airbrakeDrag : 0;

        var coefficient = Scale6(
            lv.normalized,
            dragRight.Evaluate(Mathf.Abs(lv.x)), dragLeft.Evaluate(Mathf.Abs(lv.x)),
            dragTop.Evaluate(Mathf.Abs(lv.y)), dragBottom.Evaluate(Mathf.Abs(lv.y)),
            dragForward.Evaluate(Mathf.Abs(lv.z)) + airbrakeDrag,
            dragBack.Evaluate(Mathf.Abs(lv.z))
        );

        var drag = coefficient.magnitude * lv2 * -lv.normalized;
        Rigidbody.AddRelativeForce(drag);
    }

    Vector3 CalculateLift(float angleOfAttack, Vector3 rightAxis, float liftPower, AnimationCurve aoaCurve, AnimationCurve inducedDragCurve) {
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

    void UpdateLift() {
        if (LocalVelocity.sqrMagnitude < 1f) return;

        var liftForce = CalculateLift(AngleOfAttack, Vector3.right, liftPower, liftAOACurve, inducedDragCurve);
        var yawForce = CalculateLift(AngleOfAttackYaw, Vector3.up, rudderPower, rudderAOACurve, rudderInducedDragCurve);

        Rigidbody.AddRelativeForce(liftForce);
        Rigidbody.AddRelativeForce(yawForce);
    }

    void UpdateAngularDrag() {
        var av = LocalAngularVelocity;
        var drag = av.sqrMagnitude * -av.normalized;
        Rigidbody.AddRelativeTorque(Vector3.Scale(drag, angularDrag), ForceMode.Acceleration);
    }

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

    float CalculateGLimiter(Vector3 controlInput, Vector3 maxAngularVelocity) {
        if (controlInput.magnitude < 0.01f) {
            return 1;
        }

        var maxInput = controlInput.normalized;
        var limit = CalculateGForceLimit(maxInput);
        var maxGForce = CalculateGForce(Vector3.Scale(maxInput, maxAngularVelocity), LocalVelocity);

        if (maxGForce.magnitude > limit.magnitude) {
            return limit.magnitude / maxGForce.magnitude;
        }

        return 1;
    }

    float CalculateSteering(float dt, float angularVelocity, float targetVelocity, float acceleration) {
        var error = targetVelocity - angularVelocity;
        var accel = acceleration * dt;
        return Mathf.Clamp(error, -accel, accel);
    }

    void UpdateSteering(float dt) {
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

        Rigidbody.AddRelativeTorque(correction * Mathf.Deg2Rad, ForceMode.VelocityChange);
    }

    void FixedUpdate() {
        float dt = Time.fixedDeltaTime;

        UpdateInputs(); // Actualiza tanto VR como teclado
        CalculateState(dt);
        CalculateGForce(dt);
        UpdateThrottle(dt);

        if (!Dead) {
            UpdateThrust();
            UpdateLift();
            UpdateSteering(dt);
        }
        else {
            Vector3 up = Rigidbody.rotation * Vector3.up;
            Vector3 forward = Rigidbody.linearVelocity.normalized;
            Rigidbody.rotation = Quaternion.LookRotation(forward, up);
        }

        UpdateDrag();
        UpdateAngularDrag();
        CalculateState(dt);
    }

    // Utility function
    public static Vector3 Scale6(
        Vector3 value,
        float posX, float negX,
        float posY, float negY,
        float posZ, float negZ
    ) {
        Vector3 result = value;

        if (result.x > 0) {
            result.x *= posX;
        }
        else if (result.x < 0) {
            result.x *= negX;
        }

        if (result.y > 0) {
            result.y *= posY;
        }
        else if (result.y < 0) {
            result.y *= negY;
        }

        if (result.z > 0) {
            result.z *= posZ;
        }
        else if (result.z < 0) {
            result.z *= negZ;
        }

        return result;
    }

    // Métodos de calibración para VR
    public void CalibrateFlightStickNeutral() {
        if (flightStickTransform != null && !isFlightStickGrabbed) {
            flightStickNeutralPos = flightStickTransform.localPosition;
            flightStickNeutralRot = flightStickTransform.localRotation;
        }
    }

    public void CalibrateThrottleNeutral() {
        if (throttleTransform != null && !isThrottleGrabbed) {
            throttleNeutralPos = throttleTransform.localPosition;
        }
    }

    // Método para cambiar entre modos en tiempo de ejecución
    public void ToggleInputMode() {
        useKeyboardInput = !useKeyboardInput;

        if (useKeyboardInput) {
            SetupInputSystem();
        }
        else {
            // Deshabilitar acciones de input system si existen
            if (pitchAction != null) pitchAction.Disable();
            if (rollAction != null) rollAction.Disable();
            if (yawAction != null) yawAction.Disable();
            if (throttleAction != null) throttleAction.Disable();
        }
    }

    void OnDestroy() {
        // Limpiar acciones de input
        if (pitchAction != null) pitchAction.Dispose();
        if (rollAction != null) rollAction.Dispose();
        if (yawAction != null) yawAction.Dispose();
        if (throttleAction != null) throttleAction.Dispose();
    }
}