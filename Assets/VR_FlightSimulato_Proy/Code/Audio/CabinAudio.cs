using MikeNspired.XRIStarterKit;
using UnityEngine;

public class CabinAudio : MonoBehaviour {
    [Header("Audio Sources")]
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioSource loopSource;

    [Header("Audio Clips")]
    public AudioClip doorOpenClip;
    public AudioClip doorCloseClip;
    public AudioClip doorSlideClip;

    [Header("Settings")]
    [Range(0f, 1f)] public float loopVolume = 0.5f;
    public float loopPitch = 1f;

    [SerializeField] private XRToggleSlider slider;

    // Para calcular velocidad
    private float lastValue;
    private float lastTime;
    private bool wasPlaying = false;

    private void Awake() {
        slider = GetComponent<XRToggleSlider>();

        // DEBUG: Verificar que todo esté bien
        if (oneShotSource == null) Debug.LogError("oneShotSource no está asignado!", this);
        if (loopSource == null) Debug.LogError("loopSource no está asignado!", this);
        if (doorSlideClip == null) Debug.LogError("doorSlideClip no está asignado!", this);

        // Configurar loop
        if (loopSource != null && doorSlideClip != null) {
            loopSource.clip = doorSlideClip;
            loopSource.loop = true;
            loopSource.volume = 0f; // Empezar en 0
            loopSource.playOnAwake = false;
        }
    }

    private void Start() {
        if (slider != null) {
            // Inicializar valores para velocidad
            lastValue = slider.Value;
            lastTime = Time.time;

            // Suscribir a eventos
            slider.OnValueChange.AddListener(OnSliderValueChanged);
            slider.OnMinValue.AddListener(OnDoorOpened);
            slider.OnMaxValue.AddListener(OnDoorClosed);
        }
    }

    private void OnSliderValueChanged(float newValue) {
        // Calcular velocidad (cambio por segundo)
        float currentTime = Time.time;
        float deltaTime = currentTime - lastTime;
        float speed = 0f;

        if (deltaTime > 0) {
            speed = Mathf.Abs(newValue - lastValue) / deltaTime;
        }

        // DEBUG: Ver velocidad
        // Debug.Log($"Valor: {newValue}, Velocidad: {speed}, Selected: {slider.isSelected}");

        // Si el jugador está interactuando
        if (slider.isSelected) {
            // Si hay movimiento significativo
            if (speed > 0.1f) {
                if (!loopSource.isPlaying && doorSlideClip != null) {
                    loopSource.Play();
                    wasPlaying = true;
                }
                // Ajustar volumen según velocidad
                SetLoopVolumeBasedOnSpeed(speed);
            }
            else {
                // Muy lento - bajar volumen
                loopSource.volume = 0f;
            }
        }
        else {
            // Si nadie está tocando, parar el sonido
            if (loopSource.isPlaying && wasPlaying) {
                loopSource.volume = 0f;
                loopSource.Stop();
                wasPlaying = false;
            }
        }

        // Guardar valores para próxima vez
        lastValue = newValue;
        lastTime = currentTime;
    }

    // Versión mejorada del método de velocidad
    private void SetLoopVolumeBasedOnSpeed(float speed) {
        if (loopSource == null) return;

        // Mapear velocidad a volumen (ajusta estos valores según necesites)
        float minSpeed = 0.1f;
        float maxSpeed = 5f;
        float normalizedSpeed = Mathf.Clamp01((speed - minSpeed) / (maxSpeed - minSpeed));

        // Volumen mínimo para que se escuche aún moviendo lento
        float minVolume = 0.1f * loopVolume;
        float targetVolume = Mathf.Lerp(minVolume, loopVolume, normalizedSpeed);

        // Smooth del volumen (para evitar cambios bruscos)
        loopSource.volume = Mathf.Lerp(loopSource.volume, targetVolume, Time.deltaTime * 10f);

        // También ajustar pitch si quieres
        loopSource.pitch = Mathf.Lerp(0.8f, 1.2f, normalizedSpeed);
    }

    private void OnDoorOpened() {
        PlayOneShotSound(doorOpenClip);
        StopLoopSound();
    }

    private void OnDoorClosed() {
        PlayOneShotSound(doorCloseClip);
        StopLoopSound();
    }

    private void PlayOneShotSound(AudioClip clip) {
        if (clip != null && oneShotSource != null) {
            Debug.Log($"Reproduciendo sonido: {clip.name}");
            oneShotSource.PlayOneShot(clip);
        }
    }

    private void StopLoopSound() {
        if (loopSource.isPlaying) {
            loopSource.Stop();
            wasPlaying = false;
        }
    }

    private void OnDestroy() {
        if (slider != null) {
            slider.OnValueChange.RemoveListener(OnSliderValueChanged);
            slider.OnMinValue.RemoveListener(OnDoorOpened);
            slider.OnMaxValue.RemoveListener(OnDoorClosed);
        }
    }
}