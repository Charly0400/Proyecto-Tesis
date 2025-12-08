using XR.Interaction.Toolkit.Samples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

    public class XRSwitch : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable {
        [Header("Switch Configuration")]
        [SerializeField]
        [Tooltip("The handle/lever that moves visually")]
        Transform m_Handle = null;

        [SerializeField]
        [Tooltip("The pivot point for handle rotation (optional)")]
        Transform m_Pivot = null;

        [SerializeField]
        [Tooltip("Up/ON rotation angle in degrees")]
        float m_OnAngle = 30f;

        [SerializeField]
        [Tooltip("Down/OFF rotation angle in degrees")]
        float m_OffAngle = -30f;

        [SerializeField]
        [Tooltip("Smooth rotation speed")]
        float m_RotationSpeed = 10f;

        [SerializeField]
        [Tooltip("Threshold to trigger state change (0-1)")]
        [Range(0.0f, 0.5f)]
        float m_ActivationThreshold = 0.3f;

        [Header("Attach Point Configuration")]
        [SerializeField]
        [Tooltip("Custom attach transform for this interactable")]
        Transform m_CustomAttachTransform = null;

        [SerializeField]
        [Tooltip("If true, will create a dynamic attach point at grab position")]
        bool m_UseDynamicAttach = true;

        [Header("Events")]
        [SerializeField]
        [Tooltip("Event triggered when switch is turned ON")]
        UnityEvent m_OnSwitchOn = new UnityEvent();

        [SerializeField]
        [Tooltip("Event triggered when switch is turned OFF")]
        UnityEvent m_OnSwitchOff = new UnityEvent();

        [SerializeField]
        [Tooltip("Event triggered when switch value changes")]
        UnityEventFloat m_OnValueChange = new UnityEventFloat();

        // State variables
        private float m_CurrentValue = 0.5f; // 0=OFF, 1=ON
        private bool m_IsOn = false;
        private IXRSelectInteractor m_CurrentInteractor;
        private ControllerInputActionManager m_Controller;
        private Quaternion m_TargetRotation;
        private Transform m_OriginalAttachTransform;
        private Transform m_DynamicAttachTransform;

        /// <summary>
        /// Current switch state (true = ON, false = OFF)
        /// </summary>
        public bool IsOn {
            get { return m_IsOn; }
            set { SetSwitchState(value); }
        }

        /// <summary>
        /// Current normalized value (0-1)
        /// </summary>
        public float CurrentValue => m_CurrentValue;

        public UnityEvent OnSwitchOn => m_OnSwitchOn;
        public UnityEvent OnSwitchOff => m_OnSwitchOff;
        public UnityEventFloat OnValueChange => m_OnValueChange;

        void Start() {
            InitializeSwitch();

            // Create dynamic attach transform if needed
            if (m_UseDynamicAttach && m_DynamicAttachTransform == null) {
                GameObject dynamicAttach = new GameObject("DynamicAttach");
                dynamicAttach.transform.SetParent(transform);
                dynamicAttach.transform.localPosition = Vector3.zero;
                dynamicAttach.transform.localRotation = Quaternion.identity;
                m_DynamicAttachTransform = dynamicAttach.transform;
            }
        }

        void InitializeSwitch() {
            // Set initial rotation based on starting value
            UpdateHandleRotation(m_IsOn ? 1f : 0f);
            m_TargetRotation = m_Handle.localRotation;
        }

        protected override void OnEnable() {
            base.OnEnable();
            selectEntered.AddListener(OnSelectEntered);
            selectExited.AddListener(OnSelectExited);
        }

        protected override void OnDisable() {
            selectEntered.RemoveListener(OnSelectEntered);
            selectExited.RemoveListener(OnSelectExited);

            if (m_CurrentInteractor != null) {
                CleanupInteraction();
            }

            base.OnDisable();
        }

        void OnSelectEntered(SelectEnterEventArgs args) {
            m_CurrentInteractor = args.interactorObject;
            m_Controller = m_CurrentInteractor.transform.GetComponentInParent<ControllerInputActionManager>();

            SetupAttachTransform(args);
            UpdateSwitchValue();
        }

        void OnSelectExited(SelectExitEventArgs args) {
            // Snap to nearest state when released
            SnapToNearestState();

            // Clean up attach transform
            if (m_UseDynamicAttach && m_OriginalAttachTransform != null) {
                var grabInteractor = m_CurrentInteractor as XRBaseInteractor;
                if (grabInteractor != null) {
                    grabInteractor.attachTransform = m_OriginalAttachTransform;
                }
            }

            CleanupInteraction();
        }

        void SetupAttachTransform(SelectEnterEventArgs args) {
            if (!m_UseDynamicAttach) return;

            // Store original attach transform
            var grabInteractor = args.interactorObject as XRBaseInteractor;
            if (grabInteractor != null) {
                m_OriginalAttachTransform = grabInteractor.attachTransform;

                // Set dynamic attach position at grab point
                if (m_DynamicAttachTransform != null) {
                    m_DynamicAttachTransform.position = args.interactableObject.GetAttachTransform(args.interactorObject).position;
                    m_DynamicAttachTransform.rotation = args.interactableObject.GetAttachTransform(args.interactorObject).rotation;
                    grabInteractor.attachTransform = m_DynamicAttachTransform;
                }
            }
        }

        void CleanupInteraction() {
            m_CurrentInteractor = null;
            m_Controller = null;
            m_OriginalAttachTransform = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase) {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic) {
                if (isSelected) {
                    UpdateSwitchValue();
                }

                // Smoothly rotate handle
                SmoothRotateHandle();
            }
        }

        void UpdateSwitchValue() {
            if (m_CurrentInteractor == null) return;

            // Get controller position in local space
            Vector3 controllerPos = m_Controller.transform.position;
            Vector3 localControllerPos = transform.InverseTransformPoint(controllerPos);

            // Calculate vertical movement (using Y axis for up/down)
            float verticalMovement = localControllerPos.y;

            // Normalize movement to 0-1 range with deadzone
            float normalizedValue = Mathf.Clamp01((verticalMovement + 1f) / 2f); // Assuming movement between -1 and 1

            // Apply value with smoothing
            m_CurrentValue = Mathf.Lerp(m_CurrentValue, normalizedValue, Time.deltaTime * 10f);

            // Update visual
            UpdateHandleRotation(m_CurrentValue);

            // Trigger value change event
            m_OnValueChange?.Invoke(m_CurrentValue);

            // Update dynamic attach position if using
            if (m_UseDynamicAttach && m_DynamicAttachTransform != null && m_CurrentInteractor != null) {
                UpdateDynamicAttachPosition();
            }
        }

        void UpdateDynamicAttachPosition() {
            // Keep dynamic attach at the grab point
            var interactorTransform = m_CurrentInteractor.GetAttachTransform(this);
            if (interactorTransform != null) {
                m_DynamicAttachTransform.position = interactorTransform.position;
            }
        }

        void UpdateHandleRotation(float value) {
            float angle = Mathf.Lerp(m_OffAngle, m_OnAngle, value);
            m_TargetRotation = Quaternion.Euler(angle, 0f, 0f);

            // Check for state change
            CheckStateChange(value);
        }

        void SmoothRotateHandle() {
            if (m_Handle == null) return;

            Transform targetTransform = m_Pivot != null ? m_Pivot : m_Handle;
            targetTransform.localRotation = Quaternion.Slerp(
                targetTransform.localRotation,
                m_TargetRotation,
                Time.deltaTime * m_RotationSpeed
            );
        }

        void CheckStateChange(float value) {
            bool newState = value > 0.5f;

            if (newState != m_IsOn) {
                m_IsOn = newState;

                if (m_IsOn)
                    m_OnSwitchOn?.Invoke();
                else
                    m_OnSwitchOff?.Invoke();
            }
        }

        void SnapToNearestState() {
            // Snap to ON if above threshold, OFF if below
            bool shouldBeOn = m_CurrentValue > 0.5f;
            float targetValue = shouldBeOn ? 1f : 0f;

            m_CurrentValue = targetValue;
            m_IsOn = shouldBeOn;

            UpdateHandleRotation(targetValue);

            // Trigger event for final state
            if (shouldBeOn)
                m_OnSwitchOn?.Invoke();
            else
                m_OnSwitchOff?.Invoke();
        }

        public void SetSwitchState(bool on) {
            m_IsOn = on;
            float targetValue = on ? 1f : 0f;
            m_CurrentValue = targetValue;
            UpdateHandleRotation(targetValue);

            if (on)
                m_OnSwitchOn?.Invoke();
            else
                m_OnSwitchOff?.Invoke();
        }

        public void ToggleSwitch() {
            SetSwitchState(!m_IsOn);
        }

        void OnDrawGizmosSelected() {
            if (m_Handle == null) return;

            // Draw rotation arc
            Vector3 handlePos = m_Handle.position;
            Vector3 upPos = handlePos + Quaternion.Euler(m_OnAngle, 0, 0) * Vector3.forward * 0.2f;
            Vector3 downPos = handlePos + Quaternion.Euler(m_OffAngle, 0, 0) * Vector3.forward * 0.2f;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(handlePos, upPos);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(handlePos, downPos);
        }
    }

    [System.Serializable]
    public class UnityEventFloat : UnityEvent<float> { }
