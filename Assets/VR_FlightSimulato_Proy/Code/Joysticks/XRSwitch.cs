using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

    /// <summary>
    /// Switch de palanca de dos posiciones (por ejemplo el encendido del motor) para VR.
    /// La palanca gira sobre el eje X local de su pivote siguiendo a la mano: se mide cuánto gira la mano alrededor
    /// de ese eje desde que se agarra, así no salta al agarrarla y no depende de la escala del avión.
    /// Cambia de estado al acercarse al extremo (con histéresis), vibra el mando y dispara On / Off una sola vez por
    /// cambio. Al soltarla vuelve a la posición del estado actual. Agarrar y soltar rápido sin moverla también la cambia.
    /// </summary>
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
        [Tooltip("Qué tan cerca del extremo hay que llevar la palanca para que cambie de estado. " +
                 "0.3 = al 70% del recorrido; para regresar hay que bajarla al 30%")]
        [Range(0.05f, 0.5f)]
        float m_ActivationThreshold = 0.3f;

        [SerializeField]
        [Tooltip("Estado al iniciar")]
        bool m_StartOn = false;

        [SerializeField]
        [Tooltip("Multiplica el giro de la mano: con 1 la palanca gira lo mismo que la mano alrededor del pivote")]
        [Range(0.5f, 4f)]
        float m_Sensitivity = 1.5f;

        [Header("Toque rápido")]
        [SerializeField]
        [Tooltip("Agarrar y soltar rápido sin mover la palanca la cambia de estado")]
        bool m_ToggleOnTap = true;

        [SerializeField]
        [Tooltip("Tiempo máximo del toque, en segundos")]
        float m_TapMaxTime = 0.35f;

        [SerializeField]
        [Tooltip("Giro máximo de la mano para que cuente como toque, en grados")]
        float m_TapMaxAngle = 6f;

        [Header("Mano en la punta")]
        [SerializeField]
        [Tooltip("Attach points del XRHandPoser (pueden ser hijos de la palanca). Al iniciar se sacan de la palanca: " +
                 "siguen la posición de la punta pero solo giran una parte de lo que gira la palanca, para que la " +
                 "muñeca no se tuerza 90° entre ON y OFF. Su pose debe estar hecha con la palanca a 0°")]
        Transform[] m_HandAttachPoints = new Transform[0];

        [SerializeField]
        [Tooltip("Punto de la palanca que toman los dedos (hijo de la palanca, en la punta). La mano gira alrededor de él, " +
                 "así los dedos no se despegan de la punta. Si se deja vacío la mano gira alrededor del pivote")]
        Transform m_HandGrabPoint = null;

        [SerializeField]
        [Tooltip("Qué parte del giro de la palanca sigue la mano (0 = no gira, 1 = gira igual que la palanca)")]
        [Range(0f, 1f)]
        float m_HandRotationFollow = 0.35f;

        [Header("Vibración al cambiar")]
        [SerializeField]
        [Range(0f, 1f)]
        float m_HapticAmplitude = 0.5f;

        [SerializeField]
        float m_HapticDuration = 0.06f;

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
        private float m_CurrentValue; // 0=OFF, 1=ON
        private bool m_IsOn;
        private IXRSelectInteractor m_CurrentInteractor;

        // Datos del agarre actual
        private float m_GrabHandAngle;
        private float m_GrabValue;
        private float m_GrabTime;
        private float m_MaxHandTurn;
        private bool m_ChangedDuringGrab;

        // Con la palanca a 0° y en el espacio del padre del pivote: el punto de agarre respecto al pivote, y cada
        // attach point respecto al punto de agarre
        private Vector3 m_GrabNeutralOffset;
        private Vector3[] m_AttachNeutralOffsets;
        private Quaternion[] m_AttachNeutralRotations;

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

        private Transform PivotTransform => m_Pivot != null ? m_Pivot : m_Handle;

        protected override void Awake() {
            base.Awake();

            // Sin disparar eventos: es el estado inicial, no un cambio
            m_IsOn = m_StartOn;
            m_CurrentValue = m_IsOn ? 1f : 0f;
            if (PivotTransform != null)
                PivotTransform.localRotation = RotationFor(m_CurrentValue);
            else
                Debug.LogWarning($"{name}: el switch no tiene Handle ni Pivot asignado", this);

            SetupHandAttachPoints();
        }

        void SetupHandAttachPoints() {
            Transform pivot = PivotTransform;
            int count = m_HandAttachPoints.Length;
            m_AttachNeutralOffsets = new Vector3[count];
            m_AttachNeutralRotations = new Quaternion[count];
            if (pivot == null || pivot.parent == null) return;

            // Quitar el giro actual de la palanca: así queda la pose con la palanca a 0°
            Transform frame = pivot.parent;
            Quaternion undo = Quaternion.Inverse(Quaternion.Euler(PivotAngle(), 0f, 0f));
            Vector3 grab = m_HandGrabPoint != null ? frame.InverseTransformPoint(m_HandGrabPoint.position) : pivot.localPosition;
            m_GrabNeutralOffset = undo * (grab - pivot.localPosition);
            for (int i = 0; i < count; i++) {
                Transform attach = m_HandAttachPoints[i];
                if (attach == null) continue;
                m_AttachNeutralOffsets[i] = undo * (frame.InverseTransformPoint(attach.position) - grab);
                m_AttachNeutralRotations[i] = undo * (Quaternion.Inverse(frame.rotation) * attach.rotation);
                // Ya no los mueve la palanca: los mueve UpdateHandAttachPoints
                attach.SetParent(frame, true);
            }
            UpdateHandAttachPoints();
        }

        // Ángulo actual del pivote en X (se rota solo en X, ver RotationFor)
        float PivotAngle() {
            Quaternion q = PivotTransform.localRotation;
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(q.x, q.w) * Mathf.Rad2Deg);
        }

        void UpdateHandAttachPoints() {
            Transform pivot = PivotTransform;
            if (pivot == null || pivot.parent == null || m_AttachNeutralOffsets == null) return;

            float angle = PivotAngle();
            Quaternion partial = Quaternion.Euler(angle * m_HandRotationFollow, 0f, 0f);
            // El punto de agarre gira con la palanca; la mano gira solo una parte alrededor de él
            Vector3 grab = pivot.localPosition + Quaternion.Euler(angle, 0f, 0f) * m_GrabNeutralOffset;
            for (int i = 0; i < m_HandAttachPoints.Length; i++) {
                Transform attach = m_HandAttachPoints[i];
                if (attach == null) continue;
                attach.localPosition = grab + partial * m_AttachNeutralOffsets[i];
                attach.localRotation = partial * m_AttachNeutralRotations[i];
            }
        }

        protected override void OnEnable() {
            base.OnEnable();
            selectEntered.AddListener(OnSelectEntered);
            selectExited.AddListener(OnSelectExited);
        }

        protected override void OnDisable() {
            selectEntered.RemoveListener(OnSelectEntered);
            selectExited.RemoveListener(OnSelectExited);
            m_CurrentInteractor = null;
            base.OnDisable();
        }

        void OnSelectEntered(SelectEnterEventArgs args) {
            m_CurrentInteractor = args.interactorObject;
            m_GrabHandAngle = HandAngle();
            m_GrabValue = m_CurrentValue;
            m_GrabTime = Time.time;
            m_MaxHandTurn = 0f;
            m_ChangedDuringGrab = false;
        }

        void OnSelectExited(SelectExitEventArgs args) {
            bool tap = m_ToggleOnTap && !m_ChangedDuringGrab &&
                       m_MaxHandTurn < m_TapMaxAngle && Time.time - m_GrabTime < m_TapMaxTime;
            if (tap)
                ChangeState(!m_IsOn, args.interactorObject);

            // Regresa a la posición del estado actual (si no pasó el umbral, vuelve a donde estaba)
            m_CurrentValue = m_IsOn ? 1f : 0f;
            m_OnValueChange?.Invoke(m_CurrentValue);
            m_CurrentInteractor = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase) {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic) {
                if (isSelected && m_CurrentInteractor != null) {
                    UpdateSwitchValue();
                }

                // Smoothly rotate handle
                SmoothRotateHandle();
                UpdateHandAttachPoints();
            }
        }

        void UpdateSwitchValue() {
            float range = m_OnAngle - m_OffAngle;
            if (Mathf.Approximately(range, 0f)) return;

            float handTurn = Mathf.DeltaAngle(m_GrabHandAngle, HandAngle());
            m_MaxHandTurn = Mathf.Max(m_MaxHandTurn, Mathf.Abs(handTurn));

            float value = Mathf.Clamp01(m_GrabValue + handTurn * m_Sensitivity / range);
            if (!Mathf.Approximately(value, m_CurrentValue)) {
                m_CurrentValue = value;
                m_OnValueChange?.Invoke(m_CurrentValue);
            }

            // Histéresis: para encender hay que llegar cerca de ON y para apagar cerca de OFF,
            // así no parpadea si la mano se queda a la mitad
            if (!m_IsOn && m_CurrentValue >= 1f - m_ActivationThreshold)
                ChangeState(true, m_CurrentInteractor);
            else if (m_IsOn && m_CurrentValue <= m_ActivationThreshold)
                ChangeState(false, m_CurrentInteractor);
        }

        /// <summary>
        /// Ángulo de la mano alrededor del eje X local del pivote, medido en el espacio de su padre: es el mismo eje
        /// sobre el que gira la palanca, y como ángulo no le afecta la escala del avión.
        /// </summary>
        float HandAngle() {
            Transform pivot = PivotTransform;
            if (pivot == null || m_CurrentInteractor == null) return 0f;

            Vector3 hand = m_CurrentInteractor.GetAttachTransform(this).position;
            Vector3 v = pivot.parent != null
                ? pivot.parent.InverseTransformPoint(hand) - pivot.localPosition
                : hand - pivot.position;

            // Euler(a, 0, 0) lleva el 'arriba' (0, 1, 0) a (0, cos a, sin a)
            return Mathf.Atan2(v.z, v.y) * Mathf.Rad2Deg;
        }

        void ChangeState(bool on, IXRSelectInteractor interactor) {
            if (on == m_IsOn) return;

            m_IsOn = on;
            if (isSelected) m_ChangedDuringGrab = true;

            if (interactor is XRBaseInputInteractor inputInteractor && m_HapticAmplitude > 0f)
                inputInteractor.SendHapticImpulse(m_HapticAmplitude, m_HapticDuration);

            if (m_IsOn)
                m_OnSwitchOn?.Invoke();
            else
                m_OnSwitchOff?.Invoke();
        }

        Quaternion RotationFor(float value) {
            return Quaternion.Euler(Mathf.Lerp(m_OffAngle, m_OnAngle, value), 0f, 0f);
        }

        void SmoothRotateHandle() {
            Transform targetTransform = PivotTransform;
            if (targetTransform == null) return;

            targetTransform.localRotation = Quaternion.Slerp(
                targetTransform.localRotation,
                RotationFor(m_CurrentValue),
                Time.deltaTime * m_RotationSpeed
            );
        }

        /// <summary>
        /// Cambia el estado desde código o desde un botón. Solo dispara On / Off si de verdad cambia.
        /// </summary>
        public void SetSwitchState(bool on) {
            m_CurrentValue = on ? 1f : 0f;
            m_OnValueChange?.Invoke(m_CurrentValue);
            ChangeState(on, null);
        }

        public void ToggleSwitch() {
            SetSwitchState(!m_IsOn);
        }

        void OnDrawGizmosSelected() {
            Transform pivot = PivotTransform;
            if (pivot == null) return;

            // Hacia dónde apunta la palanca en ON (verde) y en OFF (rojo)
            Transform frame = pivot.parent;
            Vector3 onDir = Quaternion.Euler(m_OnAngle, 0f, 0f) * Vector3.up;
            Vector3 offDir = Quaternion.Euler(m_OffAngle, 0f, 0f) * Vector3.up;
            if (frame != null) {
                onDir = frame.TransformDirection(onDir);
                offDir = frame.TransformDirection(offDir);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawLine(pivot.position, pivot.position + onDir.normalized * 0.1f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pivot.position, pivot.position + offDir.normalized * 0.1f);
        }
    }

    [System.Serializable]
    public class UnityEventFloat : UnityEvent<float> { }
