using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XR.Interaction.Toolkit.Samples;

namespace MikeNspired.XRIStarterKit {
    public class JoystickXR : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable {
        [Header("Joystick References")]
        [SerializeField] private Transform handle;
        [SerializeField] private Transform movingParent;

        [Header("Joystick Settings")]
        [SerializeField] private float maxAngle = 60f;
        [SerializeField] private float sensitivity = 0.3f;
        [SerializeField] private float returnSpeed = 5f;
        [SerializeField] private bool returnToCenter = true;
        [SerializeField] private float deadzone = 0.05f;

        [Header("Yaw (Rotación) Settings")]
        [SerializeField] private float yawSensitivity = 2.0f; // Sensibilidad para la rotación
        [SerializeField] private float yawDeadzone = 5f; // Zona muerta en grados
        [SerializeField] private bool enableYawControl = true;

        [Header("Events")]
        public UnityEventVector2 OnJoystickMove;
        public UnityEventFloat OnYawInput; // Nuevo evento para la guiñada

        [Header("Position Source (Throttle-style)")]
        [SerializeField] private bool m_UseControllerForPosition = true;

        private IXRSelectInteractor m_Interactor;
        private ControllerInputActionManager m_Controller;

        private Transform interactorTransform;
        private bool isGrabbed = false;
        private Vector3 initialGrabLocalPosition;
        private Quaternion initialHandleRotation;
        private Vector2 currentInput;
        private float currentYawInput;

        // Nuevas variables para control de rotación
        private Quaternion initialControllerRotation;
        private Transform controllerTransform;

        [System.Serializable]
        public class UnityEventVector2 : UnityEvent<Vector2> { }

        [System.Serializable]
        public class UnityEventFloat : UnityEvent<float> { }


        void Start() {
            if (movingParent == null)
                movingParent = transform.parent;

            selectEntered.AddListener(OnGrab);
            selectExited.AddListener(OnRelease);

            initialHandleRotation = handle.localRotation;
        }

        void OnGrab(SelectEnterEventArgs args) {
            m_Interactor = args.interactorObject;
            m_Controller = m_Interactor.transform.GetComponentInParent<ControllerInputActionManager>();

            isGrabbed = true;

            // Elegir la transform a usar (controller root o attach transform)
            controllerTransform = m_UseControllerForPosition && m_Controller != null ?
                m_Controller.transform : m_Interactor.GetAttachTransform(this);

            Vector3 grabWorldPosition = controllerTransform.position;

            if (movingParent != null)
                initialGrabLocalPosition = movingParent.InverseTransformPoint(grabWorldPosition);
            else
                initialGrabLocalPosition = transform.InverseTransformPoint(grabWorldPosition);

            // Guardar la rotación inicial del controlador
            initialControllerRotation = controllerTransform.rotation;

            StopAllCoroutines();
        }

        void OnRelease(SelectExitEventArgs args) {
            isGrabbed = false;
            m_Interactor = null;
            m_Controller = null;

            if (returnToCenter)
                StartCoroutine(ReturnToCenter());
            // Notificar que la guiñada volvió a cero
            OnYawInput?.Invoke(0f);
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase) {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic) {
                if (isSelected) {
                    UpdateJoystickPosition();
                    UpdateYawInput();
                }
            }
        }

        void UpdateJoystickPosition() {
            if (m_Interactor == null) return;

            Vector3 currentPosition;

            // Obtener la posición actual según la fuente configurada
            if (m_UseControllerForPosition) {
                if (m_Controller != null)
                    currentPosition = m_Controller.transform.position;
                else
                    // Fallback: si activaste la opción pero no hay Controller component, usa attach transform
                    currentPosition = m_Interactor.GetAttachTransform(this).position;
            }
            else {
                currentPosition = m_Interactor.GetAttachTransform(this).position;
            }

            Vector3 currentLocalPosition = movingParent != null
                ? movingParent.InverseTransformPoint(currentPosition)
                : transform.InverseTransformPoint(currentPosition);

            // Calcular desplazamiento desde la posición inicial (misma lógica que antes)
            Vector3 displacement = currentLocalPosition - initialGrabLocalPosition;

            Vector2 rawInput = new Vector2(
                Mathf.Clamp(displacement.x * sensitivity, -1f, 1f),
                Mathf.Clamp(displacement.z * sensitivity, -1f, 1f)
            );

            if (rawInput.magnitude < deadzone)
                rawInput = Vector2.zero;

            currentInput = rawInput;

            UpdateVisual();
            OnJoystickMove?.Invoke(currentInput);
        }

        void UpdateYawInput() {
            if (!enableYawControl || controllerTransform == null) return;

            // Calcular la rotación relativa desde el agarre inicial
            Quaternion relativeRotation = Quaternion.Inverse(initialControllerRotation) * controllerTransform.rotation;

            // Convertir a ángulos de Euler
            Vector3 relativeEuler = relativeRotation.eulerAngles;

            // Normalizar ángulos a -180 a 180
            float yawAngle = relativeEuler.y;
            if (yawAngle > 180f) yawAngle -= 360f;

            // Aplicar zona muerta
            if (Mathf.Abs(yawAngle) < yawDeadzone) {
                currentYawInput = 0f;
            }
            else {
                // Normalizar a -1 a 1
                currentYawInput = Mathf.Clamp(yawAngle / 90f * yawSensitivity, -1f, 1f);
            }

            // Disparar evento de guiñada
            OnYawInput?.Invoke(currentYawInput);
        }


        void UpdateVisual() {
            if (handle != null) {
                handle.localRotation = initialHandleRotation *
                    Quaternion.Euler(currentInput.y * maxAngle, 0f, -currentInput.x * maxAngle);
            }
        }

        IEnumerator ReturnToCenter() {
            Vector2 startInput = currentInput;
            float elapsedTime = 0f;

            while (elapsedTime < 1f) {
                elapsedTime += Time.deltaTime * returnSpeed;
                currentInput = Vector2.Lerp(startInput, Vector2.zero, elapsedTime);

                UpdateVisual();
                OnJoystickMove?.Invoke(currentInput);

                yield return null;
            }

            currentInput = Vector2.zero;
            UpdateVisual();
            OnJoystickMove?.Invoke(currentInput);
        }

        public void SetMovingParent(Transform parent) {
            movingParent = parent;
        }

        public Vector2 GetCurrentInput() {
            return currentInput;
        }

        public float GetCurrentYawInput() {
            return currentYawInput;
        }
    }
}