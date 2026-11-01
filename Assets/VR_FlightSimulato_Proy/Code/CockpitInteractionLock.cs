using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Bloquea los controles de la cabina (palanca, acelerador, switches...) mientras el jugador no esté sentado
/// en este avión. Sin esto, el rayo lejano del Near-Far Interactor (10 m, cono de 6°) alcanza la cabina desde
/// el suelo, debajo o al lado del avión, y deja agarrar y mover sus objetos.
/// Se registra como filtro de hover y de select en el XRInteractionManager, así que vale para todas las manos
/// (rayo, agarre cercano y poke) sin tocar el XR Origin ni cada interactable. La UI (botón de sentarse) no pasa
/// por estos filtros y sigue funcionando desde fuera.
/// </summary>
[RequireComponent(typeof(AircraftVehicle))]
public class CockpitInteractionLock : MonoBehaviour, IXRHoverFilter, IXRSelectFilter {
    [Tooltip("Raíz de los objetos que solo se pueden usar sentado. Si se deja vacío se usa todo el avión " +
             "(este objeto y sus hijos). Asignar la cabina si no es hija del avión o si algo del avión debe poder " +
             "usarse desde fuera")]
    public Transform raizCabina;

    private AircraftVehicle avion;
    private readonly List<XRInteractionManager> managers = new List<XRInteractionManager>();
    private bool iniciado;

    private Transform Raiz => raizCabina != null ? raizCabina : transform;

    public bool canProcess => isActiveAndEnabled;

    private void Awake() {
        avion = GetComponent<AircraftVehicle>();
    }

    private void Start() {
        // En Start y no en OnEnable: los interactores crean el XRInteractionManager en su Awake si no hay uno
        iniciado = true;
        Registrar();
    }

    private void OnEnable() {
        if (iniciado) Registrar();
    }

    private void OnDisable() {
        foreach (var manager in managers) {
            if (manager == null) continue;
            manager.hoverFilters.Remove(this);
            manager.selectFilters.Remove(this);
        }
        managers.Clear();
    }

    private void Registrar() {
        foreach (var manager in FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None)) {
            manager.hoverFilters.Add(this);
            manager.selectFilters.Add(this);
            managers.Add(manager);
        }

        if (managers.Count == 0)
            Debug.LogWarning("Bloqueo de cabina: no se encontró ningún XRInteractionManager en la escena", this);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) {
        return Permitido(interactable);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) {
        return Permitido(interactable);
    }

    private bool Permitido(IXRInteractable interactable) {
        if (avion.isPlayerSeated) return true;

        // Un punto de teletransporte puesto sobre el avión no es un control de la cabina
        if (interactable is BaseTeleportationInteractable) return true;

        return !interactable.transform.IsChildOf(Raiz);
    }
}
