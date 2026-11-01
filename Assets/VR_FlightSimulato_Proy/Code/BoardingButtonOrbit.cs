using UnityEngine;

/// <summary>
/// Botón flotante para subirse al avión (va en su Canvas world space, hijo del avión). Gira alrededor de la cabina
/// hacia el lado donde está el jugador, para no tener que rodear el avión, pero solo dentro de un sector a cada costado
/// de la cabina: si el jugador está al frente o atrás se queda en la orilla del sector. Solo se puede presionar si
/// el jugador está en uno de los costados y nada del avión se interpone entre su cabeza y el botón.
/// La posición inicial del Canvas define el radio, la altura y el punto de la cabina alrededor del que gira.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class BoardingButtonOrbit : MonoBehaviour {
    [Tooltip("El avión (raíz). Si se deja vacío se busca el AircraftVehicle en los padres")]
    public Transform avion;

    [Tooltip("Mitad del sector permitido a cada costado de la cabina, en grados (0 = siempre justo al lado)")]
    [Range(0f, 80f)]
    public float mitadSector = 25f;

    [Tooltip("Grados extra fuera del sector en los que el botón todavía funciona (para no apagarlo justo en la orilla)")]
    [Range(0f, 30f)]
    public float margen = 10f;

    [Tooltip("Qué tan rápido sigue al jugador al girar")]
    public float velocidadGiro = 4f;

    [Tooltip("Transparencia del botón cuando no se puede usar")]
    [Range(0f, 1f)]
    public float alfaBloqueado = 0.25f;

    [Tooltip("Capas que pueden tapar el botón (solo cuentan los colliders del avión)")]
    public LayerMask capasBloqueo = ~0;

    private Vector3 centroLocal;   // punto de la cabina alrededor del que gira, en ejes del avión
    private float radio;
    private float anguloActual;    // 0 = al frente (+Z del avión), 90 = a la derecha
    private CanvasGroup grupo;
    private Transform camara;
    private readonly RaycastHit[] golpes = new RaycastHit[16];

    private void Awake() {
        if (avion == null) {
            var av = GetComponentInParent<AircraftVehicle>();
            avion = av != null ? av.transform : transform.root;
        }

        // Gira sobre el eje vertical del avión que pasa por la línea central, a la altura y la Z donde se colocó
        Vector3 local = avion.InverseTransformPoint(transform.position);
        centroLocal = new Vector3(0f, local.y, local.z);
        radio = Mathf.Max(0.5f, Mathf.Abs(local.x));
        anguloActual = local.x >= 0f ? 90f : -90f;

        grupo = GetComponent<CanvasGroup>();
        if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();

        // Un LazyFollow en el mismo Canvas lo movería por su cuenta: esta clase ya lo coloca y lo orienta
        foreach (var comp in GetComponents<MonoBehaviour>())
            if (comp != this && comp.GetType().Name == "LazyFollow") comp.enabled = false;
    }

    private void OnEnable() {
        // Al volver a aparecer (al bajarse del avión) se coloca de una vez del lado correcto, sin animación
        if (BuscarCamara()) anguloActual = AnguloObjetivo(out _);
        Colocar();
    }

    private void LateUpdate() {
        if (!BuscarCamara()) return;

        float objetivo = AnguloObjetivo(out bool enCostado);
        anguloActual = Mathf.LerpAngle(anguloActual, objetivo, 1f - Mathf.Exp(-velocidadGiro * Time.deltaTime));
        Colocar();

        bool usable = enCostado && !Tapado();
        grupo.alpha = usable ? 1f : alfaBloqueado;
        grupo.interactable = usable;
        grupo.blocksRaycasts = usable;
    }

    private bool BuscarCamara() {
        if (camara == null && Camera.main != null) camara = Camera.main.transform;
        return camara != null && avion != null;
    }

    /// <summary>
    /// Ángulo del jugador alrededor de la cabina, recortado al sector del costado donde está.
    /// enCostado = el jugador está dentro del sector (más el margen).
    /// </summary>
    private float AnguloObjetivo(out bool enCostado) {
        Vector3 rel = avion.InverseTransformPoint(camara.position) - centroLocal;
        float angulo = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;   // 0 = frente, 90 = derecha, -90 = izquierda
        float lado = angulo >= 0f ? 90f : -90f;
        float desvio = Mathf.DeltaAngle(lado, angulo);
        enCostado = Mathf.Abs(desvio) <= mitadSector + margen;
        return lado + Mathf.Clamp(desvio, -mitadSector, mitadSector);
    }

    private void Colocar() {
        float rad = anguloActual * Mathf.Deg2Rad;
        Vector3 local = centroLocal + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * radio;
        transform.position = avion.TransformPoint(local);

        // De frente al jugador, solo girando sobre el eje vertical del avión
        Vector3 mirar = camara != null ? transform.position - camara.position : avion.TransformDirection(local - centroLocal);
        mirar = Vector3.ProjectOnPlane(mirar, avion.up);
        if (mirar.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(mirar, avion.up);
    }

    // ¿Algún collider del avión entre la cabeza del jugador y el botón?
    private bool Tapado() {
        Vector3 desde = camara.position, hacia = transform.position;
        Vector3 dir = hacia - desde;
        int n = Physics.RaycastNonAlloc(desde, dir.normalized, golpes, dir.magnitude, capasBloqueo, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (golpes[i].transform.IsChildOf(avion)) return true;
        return false;
    }
}
