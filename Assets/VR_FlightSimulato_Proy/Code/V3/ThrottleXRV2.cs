using XR.Interaction.Toolkit.Samples;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static Unity.Mathematics.math;

namespace MikeNspired.XRIStarterKit {
    public class ThrottleXRV2 : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable {
        [SerializeField]
        [Tooltip("The object that is visually grabbed and manipulated")]
        Transform m_Handle = null;

        [SerializeField]
        [Tooltip("The default behaviour uses the attach transform")]
        bool m_UseControllerForPosition = true;

        [SerializeField]
        [Tooltip("The value of the slider")]
        [Range(0.0f, 1.0f)]
        float m_Value = 0.5f;

        [SerializeField]
        [Tooltip("The offset of the slider at value '1'")]
        float m_MaxPosition = 0.5f;

        [SerializeField]
        [Tooltip("The offset of the slider at value '0'")]
        float m_MinPosition = -0.5f;

        [SerializeField]
        [Tooltip("Sensitivity multiplier for throttle movement (higher = less physical movement needed)")]
        [Range(0.1f, 10.0f)]
        float m_Sensitivity = 2.0f;

        [SerializeField]
        [Tooltip("Events to trigger when the slider is moved")]
        UnityEventFloat m_OnValueChange = new UnityEventFloat();

        [SerializeField]
        [Tooltip("Remap sliders min value of 0 to a new value")]
        float m_RemapValueMin = 0f;

        [SerializeField]
        [Tooltip("Remap sliders max value of 1 to a new value")]
        float m_RemapValueMax = 1f;

        [Header("Moving Parent Support")]
        [SerializeField]
        [Tooltip("Reference to the moving parent (like the aircraft)")]
        Transform m_MovingParent;

        IXRSelectInteractor m_Interactor;
        ControllerInputActionManager m_Controller;

        // Cache para posiciones relativas
        private Vector3 m_InitialGrabLocalPosition;
        private float m_InitialGrabValue;
        private Transform m_InitialInteractorTransform;

        /// <summary>
        /// The value of the slider
        /// </summary>
        public float Value {
            get { return m_Value; }
            set {
                SetValue(value);
                SetSliderPosition(value);
            }
        }

        /// <summary>
        /// Sensitivity multiplier for throttle movement
        /// </summary>
        public float Sensitivity {
            get { return m_Sensitivity; }
            set { m_Sensitivity = Mathf.Clamp(value, 0.1f, 10.0f); }
        }

        /// <summary>
        /// Events to trigger when the slider is moved
        /// </summary>
        public UnityEventFloat OnValueChange => m_OnValueChange;

        void Start() {
            // Si no se asignó manualmente, buscar el parent moving
            if (m_MovingParent == null)
                m_MovingParent = transform.parent;

            SetValue(m_Value);
            SetSliderPosition(m_Value);
        }

        protected override void OnEnable() {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
        }

        protected override void OnDisable() {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args) {
            m_Interactor = args.interactorObject;
            m_Controller = m_Interactor.transform.GetComponentInParent<ControllerInputActionManager>();

            // Elegimos la transform a usar. Si se pidió usar el Controller pero
            // no existe el componente en los padres, caemos de vuelta al attach
            // transform en lugar de lanzar NullReferenceException.
            Transform interactorTransform;
            if (m_UseControllerForPosition && m_Controller != null) {
                interactorTransform = m_Controller.transform;
            }
            else {
                interactorTransform = m_Interactor.GetAttachTransform(this);
            }

            m_InitialInteractorTransform = interactorTransform;
            m_InitialGrabValue = m_Value;

            // Convertir posición mundial a local relativa al moving parent
            if (m_MovingParent != null) {
                m_InitialGrabLocalPosition = m_MovingParent.InverseTransformPoint(interactorTransform.position);
            }
            else {
                m_InitialGrabLocalPosition = transform.InverseTransformPoint(interactorTransform.position);
            }
        }

        void EndGrab(SelectExitEventArgs args) {
            m_Interactor = null;
            m_Controller = null;
            m_InitialInteractorTransform = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase) {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic) {
                if (isSelected) {
                    UpdateSliderPosition();
                }
            }
        }

        void UpdateSliderPosition() {
            if (m_Interactor == null) return;

            Vector3 currentPosition;

            if (m_UseControllerForPosition) {
                if (m_Controller != null)
                    currentPosition = m_Controller.transform.position;
                else
                    currentPosition = m_Interactor.GetAttachTransform(this).position;
            }
            else {
                currentPosition = m_Interactor.GetAttachTransform(this).position;
            }

            // Evitamos división por cero/NaN si Max y Min quedaron iguales
            // por un error de configuración en el inspector.
            float range = m_MaxPosition - m_MinPosition;
            if (Mathf.Approximately(range, 0f)) {
                Debug.LogWarning($"{name}: m_MaxPosition y m_MinPosition son iguales, no se puede calcular el rango del throttle.", this);
                return;
            }

            float sliderValue;

            if (m_MovingParent != null) {
                // Usar coordenadas locales relativas al moving parent
                Vector3 currentLocalPosition = m_MovingParent.InverseTransformPoint(currentPosition);

                float displacement = currentLocalPosition.z - m_InitialGrabLocalPosition.z;
                float sensitivityAdjustedDisplacement = displacement * m_Sensitivity;
                float displacementNormalized = sensitivityAdjustedDisplacement / range;

                sliderValue = Mathf.Clamp01(m_InitialGrabValue + displacementNormalized);
            }
            else {
                if (m_InitialInteractorTransform == null) return;

                var localPosition = transform.InverseTransformPoint(currentPosition);
                float displacement = localPosition.z - transform.InverseTransformPoint(m_InitialInteractorTransform.position).z;
                float sensitivityAdjustedDisplacement = displacement * m_Sensitivity;

                sliderValue = Mathf.Clamp01(m_InitialGrabValue + sensitivityAdjustedDisplacement / range);
            }

            SetValue(sliderValue);
            SetSliderPosition(sliderValue);
        }

        void SetSliderPosition(float value) {
            if (m_Handle == null)
                return;

            var handlePos = m_Handle.localPosition;
            handlePos.z = Mathf.Lerp(m_MinPosition, m_MaxPosition, value);
            m_Handle.localPosition = handlePos;
        }

        void SetValue(float value) {
            m_Value = value;
            m_OnValueChange?.Invoke(remap(0, 1, m_RemapValueMin, m_RemapValueMax, m_Value));
        }

        void OnDrawGizmosSelected() {
            var sliderMinPoint = transform.TransformPoint(new Vector3(0.0f, 0.0f, m_MinPosition));
            var sliderMaxPoint = transform.TransformPoint(new Vector3(0.0f, 0.0f, m_MaxPosition));

            Gizmos.color = Color.green;
            Gizmos.DrawLine(sliderMinPoint, sliderMaxPoint);
        }

        void OnValidate() {
            SetSliderPosition(m_Value);
        }

        // Método para asignar el moving parent en runtime si es necesario
        public void SetMovingParent(Transform movingParent) {
            m_MovingParent = movingParent;
        }
    }
}