using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.Events;

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

        [Header("Events")]
        public UnityEventVector2 OnJoystickMove;

        private Transform interactorTransform;
        private bool isGrabbed = false;
        private Vector3 initialGrabLocalPosition;
        private Quaternion initialHandleRotation;
        private Vector2 currentInput;

        [System.Serializable]
        public class UnityEventVector2 : UnityEvent<Vector2> { }

        void Start() {
            if (movingParent == null)
                movingParent = transform.parent;

            selectEntered.AddListener(OnGrab);
            selectExited.AddListener(OnRelease);

            initialHandleRotation = handle.localRotation;
        }

        void OnGrab(SelectEnterEventArgs args) {
            interactorTransform = args.interactorObject.GetAttachTransform(this);
            isGrabbed = true;

            // Mismo enfoque que el throttle
            Vector3 grabWorldPosition = interactorTransform.position;
            initialGrabLocalPosition = movingParent.InverseTransformPoint(grabWorldPosition);

            StopAllCoroutines();
        }

        void OnRelease(SelectExitEventArgs args) {
            isGrabbed = false;
            interactorTransform = null;

            if (returnToCenter)
                StartCoroutine(ReturnToCenter());
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase) {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic) {
                if (isSelected) {
                    UpdateJoystickPosition();
                }
            }
        }

        void UpdateJoystickPosition() {
            if (interactorTransform == null) return;

            Vector3 currentPosition = interactorTransform.position;
            Vector3 currentLocalPosition = movingParent.InverseTransformPoint(currentPosition);

            // Calcular desplazamiento desde posición inicial
            Vector3 displacement = currentLocalPosition - initialGrabLocalPosition;

            Vector2 rawInput = new Vector2(
                Mathf.Clamp(displacement.x * sensitivity, -1f, 1f),
                Mathf.Clamp(displacement.z * sensitivity, -1f, 1f)
            );

            // Aplicar deadzone
            if (rawInput.magnitude < deadzone)
                rawInput = Vector2.zero;

            currentInput = rawInput;

            // Actualizar visual
            UpdateVisual();

            // Disparar evento
            OnJoystickMove?.Invoke(currentInput);
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
    }
}