using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AircraftVehicle : MonoBehaviour {
    [Header("XR References")]
    public Transform xrOrigin; // Arrastra el XR Origin aquí en el inspector
    public Transform pilotSeat; // Punto de anclaje dentro del avión

    [Header("Settings")]
    public bool lockPosition = true;
    public bool lockRotation = true;

    private Vector3 initialOffset;
    private bool isPlayerSeated = false;

    private void Awake() {
        if (xrOrigin != null && pilotSeat != null) {
            initialOffset = xrOrigin.position - pilotSeat.position;
        }
    }

    private void Update() {
        if (isPlayerSeated && xrOrigin != null && pilotSeat != null) {
            UpdateXRPosition();
        }
    }

    public void SeatPlayer() {
        if (xrOrigin == null || pilotSeat == null) return;

        // Hacer al XR Origin hijo del avión
        xrOrigin.SetParent(transform);
        isPlayerSeated = true;

        // Posicionar en el asiento del piloto
        xrOrigin.position = pilotSeat.position;
        xrOrigin.rotation = pilotSeat.rotation;

        Debug.Log("Jugador sentado en el avión");
    }

    public void UnseatPlayer() {
        if (xrOrigin != null) {
            xrOrigin.SetParent(null);
            isPlayerSeated = false;
        }
    }

    private void UpdateXRPosition() {
        if (lockPosition) {
            // Mantener posición relativa al asiento
            xrOrigin.position = pilotSeat.position;
        }

        if (lockRotation) {
            // Mantener rotación del asiento pero permitir movimiento de cabeza
            xrOrigin.rotation = pilotSeat.rotation;
        }
    }

    // Para entrar/salir del avión
    public void ToggleSeating() {
        if (isPlayerSeated) UnseatPlayer();
        else SeatPlayer();
    }
}