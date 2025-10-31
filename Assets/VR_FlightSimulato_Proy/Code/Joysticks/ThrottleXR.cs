using XR.Interaction.Toolkit.Samples;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static Unity.Mathematics.math;

namespace MikeNspired.XRIStarterKit {
    public class ThrottleXR : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable {
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

            // Guardar estado inicial del grab
            var interactorTransform = m_UseControllerForPosition ?
                m_Controller.transform : m_Interactor.GetAttachTransform(this);

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
                    return;
            }
            else {
                currentPosition = m_Interactor.GetAttachTransform(this).position;
            }

            float sliderValue;

            if (m_MovingParent != null) {
                // Usar coordenadas locales relativas al moving parent
                Vector3 currentLocalPosition = m_MovingParent.InverseTransformPoint(currentPosition);

                // Calcular el desplazamiento desde la posición inicial de grab 
                float displacement = currentLocalPosition.z - m_InitialGrabLocalPosition.z;

                // Convertir desplazamiento a valor del slider
                float displacementNormalized = displacement / (m_MaxPosition - m_MinPosition);
                sliderValue = Mathf.Clamp01(m_InitialGrabValue + displacementNormalized);
            }
            else {
                // Método original (para objetos estáticos)
                var localPosition = transform.InverseTransformPoint(currentPosition);
                sliderValue = Mathf.Clamp01((localPosition.z - m_MinPosition) / (m_MaxPosition - m_MinPosition));
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