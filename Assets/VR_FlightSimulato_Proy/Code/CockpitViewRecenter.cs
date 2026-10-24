using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Recentra la vista VR dentro de la cabina: deja la cabeza del jugador (la cámara del XR Origin) en el
/// punto de vista del piloto y mirando al frente del avión, sin importar dónde esté o hacia dónde mire
/// en la habitación real. Compensa la posición y el giro reales del visor, no solo rota el rig.
/// Lo llama AircraftVehicle.SeatPlayer() al sentarse; también se puede llamar en cualquier momento con
/// la acción de entrada (por defecto B / Y del mando y la tecla C) o desde un botón de la cabina (Recentrar).
/// </summary>
[RequireComponent(typeof(AircraftVehicle))]
public class CockpitViewRecenter : MonoBehaviour {
    // Si el XR Origin no tiene componente XROrigin, la misma altura por defecto que su Camera Y Offset
    private const float ALTURA_OJOS_POR_DEFECTO = 1.36144f;

    [Header("Punto de vista")]
    [Tooltip("Dónde deben quedar los ojos del piloto (un objeto vacío hijo del avión). Su rotación define hacia dónde " +
             "se mira: girándolo en X se baja o sube la vista. " +
             "Si se deja vacío se usa el asiento + el Camera Y Offset del XR Origin (donde quedaban los ojos hasta ahora) " +
             "mirando al frente del avión. Clic derecho en el componente > 'Crear Punto Vista' lo crea ahí para luego ajustarlo")]
    public Transform puntoVista;

    [Header("Entrada")]
    [Tooltip("Botones y teclas para recentrar. Por defecto: B y Y del mando y la tecla C")]
    public InputAction accionRecentrar = new InputAction("Recentrar vista", InputActionType.Button);

    private AircraftVehicle avion;

    private AircraftVehicle Avion => avion != null ? avion : (avion = GetComponent<AircraftVehicle>());

    // Hacia dónde se mira: la rotación del Punto Vista (para ajustar el ángulo) o, sin él, la del avión
    private Transform Referencia => puntoVista != null ? puntoVista : transform;

    private void Reset() {
        accionRecentrar = new InputAction("Recentrar vista", InputActionType.Button);
        AgregarBindingsPorDefecto();
    }

    private void Awake() {
        // Por si el componente se agregó sin pasar por Reset (por código, por ejemplo)
        if (accionRecentrar.bindings.Count == 0)
            AgregarBindingsPorDefecto();
    }

    private void OnEnable() {
        accionRecentrar.performed += AlPresionarRecentrar;
        accionRecentrar.Enable();
    }

    private void OnDisable() {
        accionRecentrar.performed -= AlPresionarRecentrar;
        accionRecentrar.Disable();
    }

    private void AgregarBindingsPorDefecto() {
        // B / Y: los controles de vuelo son por agarre (grip / gatillo) y estos botones no los usa nada más
        accionRecentrar.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
        accionRecentrar.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
        accionRecentrar.AddBinding("<Keyboard>/c");
    }

    private void AlPresionarRecentrar(InputAction.CallbackContext contexto) {
        Recentrar();
    }

    /// <summary>
    /// Mueve y gira el XR Origin para que la cámara quede en el punto de vista mirando al frente del avión.
    /// Solo cambia el giro horizontal: la inclinación real de la cabeza se respeta. Solo actúa con el jugador sentado.
    /// </summary>
    public void Recentrar() {
        if (Avion == null || Avion.xrOrigin == null) return;

        if (!Avion.isPlayerSeated) {
            Debug.Log("Recentrar vista: el jugador no está sentado en el avión");
            return;
        }

        Transform cabeza = ObtenerCamara();
        if (cabeza == null) {
            Debug.LogWarning("Recentrar vista: no se encontró la cámara del XR Origin", this);
            return;
        }

        Transform rig = Avion.xrOrigin;
        Vector3 arriba = Referencia.up;
        Vector3 frente = Referencia.forward;
        Vector3 destino = PuntoVistaMundo();

        // 1) El 'arriba' del rig es el de la referencia (el asiento está inclinado: no se hereda su rotación)
        rig.rotation = Quaternion.FromToRotation(rig.up, arriba) * rig.rotation;

        // 2) Girar el rig alrededor de la cabeza, sobre el eje vertical de la referencia, hasta que la mirada
        //    horizontal del visor apunte a su frente
        Vector3 mirada = Vector3.ProjectOnPlane(cabeza.forward, arriba);
        if (mirada.sqrMagnitude < 0.0001f) // mirando justo arriba o abajo: la coronilla indica hacia dónde está la cara
            mirada = Vector3.ProjectOnPlane(cabeza.up, arriba) * -Mathf.Sign(Vector3.Dot(cabeza.forward, arriba));
        rig.RotateAround(cabeza.position, arriba, Vector3.SignedAngle(mirada, frente, arriba));

        // 3) Trasladar el rig para que la cabeza quede justo en el punto de vista
        rig.position += destino - cabeza.position;

        Debug.Log("Vista recentrada en la cabina");
    }

    private Transform ObtenerCamara() {
        var origen = Avion.xrOrigin.GetComponent<XROrigin>();
        if (origen != null && origen.Camera != null)
            return origen.Camera.transform;

        var camara = Avion.xrOrigin.GetComponentInChildren<Camera>();
        return camara != null ? camara.transform : null;
    }

    private Vector3 PuntoVistaMundo() {
        if (puntoVista != null)
            return puntoVista.position;

        // Sin punto asignado: donde quedaban los ojos hasta ahora con el visor en el origen del tracking
        Transform asiento = Avion.pilotSeat != null ? Avion.pilotSeat : transform;
        float alturaOjos = ALTURA_OJOS_POR_DEFECTO;
        if (Avion.xrOrigin != null) {
            var origen = Avion.xrOrigin.GetComponent<XROrigin>();
            if (origen != null) alturaOjos = origen.CameraYOffset;
        }
        return asiento.position + asiento.up * alturaOjos;
    }

    private void OnDrawGizmosSelected() {
        if (Avion == null) return;

        // Dónde quedarán los ojos y hacia dónde mirarán
        Vector3 ojos = PuntoVistaMundo();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ojos, 0.1f);
        Gizmos.DrawLine(ojos, ojos + Referencia.forward * 0.6f);
    }

#if UNITY_EDITOR
    [ContextMenu("Crear Punto Vista")]
    private void CrearPuntoVista() {
        if (puntoVista != null) {
            Debug.Log("Ya hay un Punto Vista asignado", puntoVista);
            return;
        }

        Vector3 posicion = PuntoVistaMundo();
        var punto = new GameObject("PuntoVistaPiloto");
        UnityEditor.Undo.RegisterCreatedObjectUndo(punto, "Crear Punto Vista");
        punto.transform.SetParent(transform, false);
        punto.transform.SetPositionAndRotation(posicion, transform.rotation);

        UnityEditor.Undo.RecordObject(this, "Crear Punto Vista");
        puntoVista = punto.transform;
        UnityEditor.Selection.activeGameObject = punto;
    }
#endif
}
