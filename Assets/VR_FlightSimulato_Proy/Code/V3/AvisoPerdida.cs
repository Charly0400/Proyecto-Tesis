using Charly.FlightController;
using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Aviso de pérdida en vuelo: una barra ámbar en la visera del tablero (en la línea de vista), vibración del mando que
/// sostiene la palanca y un pitido. Avisa unos grados ANTES de la pérdida (como el aviso real) y se vuelve más rápido
/// e intenso ya en pérdida. No avisa en tierra ni a baja velocidad. GeneradorControlesP47 lo configura solo.
/// </summary>
public class AvisoPerdida : MonoBehaviour {
    [Header("Referencias (vacías = se buscan en el avión)")]
    [SerializeField] private AircraftPhysics fisica;
    [SerializeField] private JoystickXRV2 palanca;
    [Tooltip("Lente de la barra: se cambia entre el material encendido y el apagado")]
    [SerializeField] private Renderer lente;
    [SerializeField] private Material encendida, apagada;

    [Header("Cuándo avisa")]
    [Tooltip("Grados antes del ángulo de pérdida en que empieza a avisar")]
    [SerializeField] private float margenGrados = 3f;
    [Tooltip("Velocidad mínima (m/s) para avisar: evita que suene rodando o detenido")]
    [SerializeField] private float velocidadMinima = 20f;

    [Header("Parpadeo y vibración (veces por segundo)")]
    [SerializeField] private float ritmoAviso = 3f;
    [SerializeField] private float ritmoPerdida = 6f;
    [Range(0f, 1f)] [SerializeField] private float vibracionAviso = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float vibracionPerdida = 0.8f;

    [Header("Pitido")]
    [SerializeField] private bool pitido = true;
    [Range(0f, 1f)] [SerializeField] private float volumen = 0.35f;
    [SerializeField] private float frecuencia = 1400f;

    /// <summary>0 = nada, 1 = cerca de la pérdida, 2 = en pérdida.</summary>
    public int Nivel { get; private set; }

    private AudioSource m_Audio;
    private float m_Fase;
    private bool m_Encendida;

    private void Awake() {
        if (fisica == null) fisica = GetComponentInParent<AircraftPhysics>();
        if (palanca == null && fisica != null) palanca = fisica.GetComponentInChildren<JoystickXRV2>(true);
        if (pitido) {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 0.8f;
            m_Audio.volume = volumen;
            m_Audio.clip = CrearPitido(frecuencia);
        }
        PonerLuz(false);
    }

    private void Update() {
        Nivel = CalcularNivel();
        if (Nivel == 0) {
            m_Fase = 0f;
            if (m_Encendida) PonerLuz(false);
            return;
        }
        // Cada ciclo: medio tiempo encendida (con vibración y pitido al prender) y medio apagada
        float ritmo = Nivel == 2 ? ritmoPerdida : ritmoAviso;
        float antes = m_Fase;
        m_Fase = (m_Fase + Time.deltaTime * ritmo) % 1f;
        bool prender = m_Fase < 0.5f;
        if (prender && (!m_Encendida || m_Fase < antes)) Pulso(1f / ritmo * 0.5f);
        if (prender != m_Encendida) PonerLuz(prender);
    }

    private int CalcularNivel() {
        if (fisica == null || fisica.IsOnGround || fisica.AirspeedMps < velocidadMinima) return 0;
        if (fisica.IsStalled) return 2;
        return Mathf.Abs(fisica.AngleOfAttackDeg) > fisica.StallAngleDeg - margenGrados ? 1 : 0;
    }

    private void Pulso(float duracion) {
        if (m_Audio != null) m_Audio.PlayOneShot(m_Audio.clip, Nivel == 2 ? 1f : 0.7f);
        if (palanca == null || !palanca.isSelected) return;
        float fuerza = Nivel == 2 ? vibracionPerdida : vibracionAviso;
        foreach (var interactor in palanca.interactorsSelecting)
            if (interactor is XRBaseInputInteractor mando) mando.SendHapticImpulse(fuerza, duracion);
    }

    private void PonerLuz(bool prendida) {
        m_Encendida = prendida;
        var m = prendida ? encendida : apagada;
        if (lente != null && m != null) lente.sharedMaterial = m;
    }

    // Pitido corto con entrada y salida suaves (sin chasquidos)
    private static AudioClip CrearPitido(float hz) {
        const int muestras = 44100 / 10;
        var datos = new float[muestras];
        for (int i = 0; i < muestras; i++) {
            float t = i / 44100f, env = Mathf.Min(1f, Mathf.Min(i, muestras - i) / 300f);
            datos[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * env * 0.6f;
        }
        var clip = AudioClip.Create("PitidoPerdida", muestras, 1, 44100, false);
        clip.SetData(datos, 0);
        return clip;
    }
}
