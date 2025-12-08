using MikeNspired.XRIStarterKit;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public class AircraftVehicle : MonoBehaviour {
    [Header("XR References")]
    public Transform xrOrigin;
    public Transform pilotSeat;

    [Header("Settings")]
    public bool desactivarMovimiento = true;

    [Header("Toggle Canvas")]
    public GameObject unseatButtonCanvas;
    public GameObject seatButtonCanvas;
    public bool isPlayerSeated = false;
    public bool isWindowClosed = true;

    private LocomotionProvider[] proveedoresMovimiento;
    private CharacterController controladorPersonaje;

    private void Awake() {
        if (xrOrigin != null) {
            // Obtener todos los componentes de locomoción
            proveedoresMovimiento = xrOrigin.GetComponentsInChildren<LocomotionProvider>();
            controladorPersonaje = xrOrigin.GetComponentInChildren<CharacterController>();
        }
    }

    private void Start() {
        ToggleButtonCanva();
    }

    public void SeatPlayer() {
        if (xrOrigin == null || pilotSeat == null) return;

        // Hacer que XR Origin sea hijo del asiento
        xrOrigin.transform.SetParent(pilotSeat);

        // Posicionar exactamente en el asiento
        xrOrigin.transform.localPosition = Vector3.zero;
        xrOrigin.transform.localRotation = Quaternion.identity;

        // Desactivar sistemas de movimiento
        if (desactivarMovimiento) {
            DesactivarMovimientoXR();
        }

        isPlayerSeated = true;
        ToggleButtonCanva();
        Debug.Log("Jugador anclado al asiento del piloto");
    }

    public void UnseatPlayer() {
        if (xrOrigin != null) {

            // Reactivar movimiento
            if (desactivarMovimiento) {
                ActivarMovimientoXR();
            }

            isPlayerSeated = false;
            ToggleButtonCanva();
            Debug.Log("Jugador no anclado al asiento del piloto");
        }
    }

    private void DesactivarMovimientoXR() {
        // Desactivar todos los proveedores de locomoción
        foreach (var proveedor in proveedoresMovimiento) {
            if (proveedor != null)
                proveedor.enabled = false;
        }

        // Desactivar CharacterController si existe
        if (controladorPersonaje != null) {
            controladorPersonaje.enabled = false;
        }
    }

    private void ActivarMovimientoXR() {
        // Reactivar todos los proveedores de locomoción
        foreach (var proveedor in proveedoresMovimiento) {
            if (proveedor != null)
                proveedor.enabled = true;
        }

        // Reactivar CharacterController
        if (controladorPersonaje != null) {
            controladorPersonaje.enabled = true;
        }
    }


    public void ToggleSeating() {
        if (isPlayerSeated) UnseatPlayer();
        else SeatPlayer();
    }

    private void ToggleButtonCanva() {
        // Asegurarse de que los canvas existen
        if (unseatButtonCanvas == null || seatButtonCanvas == null) {
            Debug.LogWarning("Canvas no asignados en el inspector");
            return;
        }

        unseatButtonCanvas.SetActive(!isPlayerSeated);
        seatButtonCanvas.SetActive(isPlayerSeated);

        Debug.Log($"Player seated: {isPlayerSeated}, SeatButton: {!isPlayerSeated}, UnseatButton: {isPlayerSeated}");
    }

    private void ToggleWindowButton() {
        if (seatButtonCanvas == null) {
            Debug.LogWarning("Canvas no asignados en el inspector");
            return;
        }

        if (isPlayerSeated)
            seatButtonCanvas.SetActive(!isWindowClosed);

        Debug.Log($"Window closed: {isWindowClosed}, SeatButton active: {!isWindowClosed}");
    }

    public void ActiveWindowButton() {
        isWindowClosed = false;
        ToggleWindowButton();
    }

    public void CloseWindowButton() {
        isWindowClosed = true;
        ToggleWindowButton();
    }

    public void OnCloseWindow(float value) {
        if (!isPlayerSeated)
            return;

        // Umbral para considerar que el slider llegó al máximo
        const float MAX_THRESHOLD = 0.98f;

        if (value >= MAX_THRESHOLD)
            ActiveWindowButton();     // Ventana abierta  activar
        else
            CloseWindowButton();      // Ventana no está abierta  apagar
    }
}