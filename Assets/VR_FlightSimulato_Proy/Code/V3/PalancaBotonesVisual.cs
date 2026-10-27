using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace MikeNspired.XRIStarterKit {
    /// <summary>
    /// Solo visual: hunde el gatillo de la palanca mientras se dispara (eventos activated/deactivated del
    /// JoystickXRV2, los mismos que usa AircraftGun) y el botón de pulgar mientras se presiona su acción
    /// (por defecto B / Y del mando y la tecla C, igual que CockpitViewRecenter). GeneradorControlesP47 lo configura solo.
    /// </summary>
    public class PalancaBotonesVisual : MonoBehaviour {
        [Tooltip("Si se deja vacío se busca en este objeto")]
        [SerializeField] private JoystickXRV2 palanca;
        [Tooltip("Gira sobre su eje X local al apretar")]
        [SerializeField] private Transform gatillo;
        [SerializeField] private float anguloGatillo = 18f;
        [Tooltip("Se hunde sobre su eje Y local al presionar")]
        [SerializeField] private Transform boton;
        [SerializeField] private float hundimientoBoton = 0.004f;
        [Tooltip("Botones que hunden el botón de pulgar. Por defecto: B y Y del mando y la tecla C")]
        public InputAction accionBoton = new InputAction("Botón palanca", InputActionType.Button);

        private Quaternion m_RotGatillo;
        private Vector3 m_PosBoton;
        private bool m_Gatillo;

        private void Awake() {
            if (palanca == null) palanca = GetComponent<JoystickXRV2>();
            if (gatillo != null) m_RotGatillo = gatillo.localRotation;
            if (boton != null) m_PosBoton = boton.localPosition;
            if (accionBoton.bindings.Count == 0) {
                accionBoton.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
                accionBoton.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
                accionBoton.AddBinding("<Keyboard>/c");
            }
        }

        private void OnEnable() {
            accionBoton.Enable();
            if (palanca == null) return;
            palanca.activated.AddListener(AlApretar);
            palanca.deactivated.AddListener(AlSoltar);
            palanca.selectExited.AddListener(AlSoltarPalanca);
        }

        private void OnDisable() {
            accionBoton.Disable();
            if (palanca == null) return;
            palanca.activated.RemoveListener(AlApretar);
            palanca.deactivated.RemoveListener(AlSoltar);
            palanca.selectExited.RemoveListener(AlSoltarPalanca);
        }

        private void AlApretar(ActivateEventArgs args) => m_Gatillo = true;
        private void AlSoltar(DeactivateEventArgs args) => m_Gatillo = false;
        private void AlSoltarPalanca(SelectExitEventArgs args) => m_Gatillo = false;

        private void Update() {
            float k = 1f - Mathf.Exp(-25f * Time.deltaTime);
            if (gatillo != null) {
                var objetivo = m_RotGatillo * Quaternion.Euler(m_Gatillo ? anguloGatillo : 0f, 0f, 0f);
                gatillo.localRotation = Quaternion.Slerp(gatillo.localRotation, objetivo, k);
            }
            if (boton != null) {
                var objetivo = m_PosBoton + (accionBoton.IsPressed() ? boton.localRotation * Vector3.down * hundimientoBoton : Vector3.zero);
                boton.localPosition = Vector3.Lerp(boton.localPosition, objetivo, k);
            }
        }
    }
}
