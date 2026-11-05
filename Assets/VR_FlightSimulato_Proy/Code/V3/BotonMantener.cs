using Charly.FlightController;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Botón de cabina que hay que MANTENER presionado (agarrarlo con el grip) mientras se llena un anillo de luz; si se
/// suelta antes, se cancela. Pensado para acciones serias como reiniciar el nivel o reaparecer en la base, para que un
/// roce de la mano no las active. Vibra suave con cada segmento del anillo y fuerte al completarse.
/// GeneradorControlesP47 lo configura solo.
/// </summary>
public class BotonMantener : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable {
    // Las nuevas van al final: el valor se guarda como número en escenas y prefabs
    public enum Accion { Ninguna, ReiniciarNivel, ReaparecerEnBase, ReaparecerEnHangar }

    [Header("Acción")]
    [SerializeField] private Accion accion = Accion.Ninguna;
    public Accion AccionConfigurada => accion;
    [Tooltip("Segundos que hay que mantenerlo presionado")]
    [SerializeField] private float segundos = 2f;
    [Tooltip("Además de la acción (o en lugar de ella con 'Ninguna')")]
    public UnityEvent onCompletado = new UnityEvent();

    [Header("Visual")]
    [Tooltip("Tapa del botón: se hunde sobre su eje Y local mientras se presiona")]
    [SerializeField] private Transform boton;
    [SerializeField] private float hundimiento = 0.004f;
    [Tooltip("Segmentos del anillo, en orden; se encienden según el avance")]
    [SerializeField] private Renderer[] segmentos;
    [SerializeField] private Material segmentoEncendido, segmentoApagado;

    [Header("Vibración")]
    [Range(0f, 1f)] [SerializeField] private float vibracionSegmento = 0.15f;
    [Range(0f, 1f)] [SerializeField] private float vibracionFinal = 0.8f;

    /// <summary>Avance de 0 a 1.</summary>
    public float Progreso { get; private set; }

    private Vector3 m_PosBoton;
    private bool m_Disparado;
    private int m_Encendidos = -1;

    protected override void Awake() {
        base.Awake();
        if (boton != null) m_PosBoton = boton.localPosition;
        Pintar();
    }

    private void Update() {
        bool presionado = isSelected;
        if (!presionado) m_Disparado = false;

        if (presionado && !m_Disparado) {
            Progreso = Mathf.Min(1f, Progreso + Time.deltaTime / Mathf.Max(0.1f, segundos));
            if (Progreso >= 1f) {
                m_Disparado = true;   // hay que soltar para volver a usarlo
                Vibrar(vibracionFinal, 0.25f);
                Ejecutar();
            }
        }
        else if (!presionado) {
            Progreso = Mathf.MoveTowards(Progreso, 0f, Time.deltaTime * 3f);   // se vacía rápido al soltar
        }

        if (boton != null) {
            var objetivo = m_PosBoton + (presionado ? boton.localRotation * Vector3.down * hundimiento : Vector3.zero);
            boton.localPosition = Vector3.Lerp(boton.localPosition, objetivo, 1f - Mathf.Exp(-25f *  Time.deltaTime));
        }
        Pintar();
    }

    private void Pintar() {
        if (segmentos == null || segmentos.Length == 0) return;
        int encendidos = Mathf.FloorToInt(Progreso * segmentos.Length + 0.001f);
        if (encendidos == m_Encendidos) return;
        if (encendidos > m_Encendidos && m_Encendidos >= 0 && encendidos < segmentos.Length) Vibrar(vibracionSegmento, 0.03f);
        m_Encendidos = encendidos;
        for (int i = 0; i < segmentos.Length; i++)
            if (segmentos[i] != null) segmentos[i].sharedMaterial = i < encendidos ? segmentoEncendido : segmentoApagado;
    }

    private void Vibrar(float fuerza, float duracion) {
        if (fuerza <= 0f) return;
        foreach (var interactor in interactorsSelecting)
            if (interactor is XRBaseInputInteractor mando) mando.SendHapticImpulse(fuerza, duracion);
    }

    private void Ejecutar() {
        switch (accion) {
            case Accion.ReiniciarNivel:
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;
            case Accion.ReaparecerEnBase:
            case Accion.ReaparecerEnHangar:
                var choques = GetComponentInParent<CrashHandler>();
                if (choques == null) choques = FindFirstObjectByType<CrashHandler>();
                if (choques == null) Debug.LogWarning($"{name}: no se encontró un CrashHandler para reaparecer", this);
                else if (accion == Accion.ReaparecerEnHangar) choques.ReaparecerEnHangar();
                else choques.Reaparecer();
                break;
        }
        onCompletado?.Invoke();
    }
}
