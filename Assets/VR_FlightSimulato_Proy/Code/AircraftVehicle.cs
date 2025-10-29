using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public class AircraftVehicle : MonoBehaviour {
    [Header("XR References")]
    public Transform xrOrigin;
    public Transform pilotSeat;

    [Header("Settings")]
    public bool desactivarMovimiento = true;
    //public bool lockPosition = true;
    //public bool lockRotation = true;
    //public float updateThreshold = 0.01f; // Umbral para evitar updates innecesarios

    //private bool isPlayerSeated = false;
    //private Vector3 lastPosition;
    //private Quaternion lastRotation;

    private bool isPlayerSeated = false;
    private LocomotionProvider[] proveedoresMovimiento;
    private CharacterController controladorPersonaje;

    private void Awake() {
        if (xrOrigin != null) {
            // Obtener todos los componentes de locomoción
            proveedoresMovimiento = xrOrigin.GetComponentsInChildren<LocomotionProvider>();
            controladorPersonaje = xrOrigin.GetComponentInChildren<CharacterController>();
        }
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
        Debug.Log("Jugador anclado al asiento del piloto");
    }

    public void UnseatPlayer() {
        if (xrOrigin != null) {
            // Quitar la relación de parentesco
            xrOrigin.transform.SetParent(null);

            // Reactivar movimiento
            if (desactivarMovimiento) {
                ActivarMovimientoXR();
            }

            isPlayerSeated = false;
        }
    }

    //private void FixedUpdate() {
    //    if (isPlayerSeated && xrOrigin != null && pilotSeat != null) {
    //        UpdateXRPosition();
    //    }
    //}

    //private void UpdateXRPosition() {
    //    bool positionChanged = Vector3.Distance(lastPosition, pilotSeat.position) > updateThreshold;
    //    bool rotationChanged = Quaternion.Angle(lastRotation, pilotSeat.rotation) > updateThreshold;

    //    // Solo actualizar si hay cambios significativos
    //    if (positionChanged || rotationChanged) {
    //        if (lockPosition) {
    //            xrOrigin.position = pilotSeat.position;
    //        }

    //        if (lockRotation) {
    //            xrOrigin.rotation = pilotSeat.rotation;
    //        }

    //        lastPosition = pilotSeat.position;
    //        lastRotation = pilotSeat.rotation;
    //    }
    //}
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
}