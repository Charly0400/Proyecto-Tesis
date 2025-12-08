using MikeNspired.XRIStarterKit;
using UnityEngine;

public class EngineAudio : MonoBehaviour {
    [Header("Engine Components")]
    [SerializeField] private XRSwitch engineSwitch;
    [SerializeField] private ThrottleXR throttle;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource engineAudioSource;      // Sonidos principales del motor
    [SerializeField] private AudioSource oneShotAudioSource;     // Sonidos de transición

    [Header("Engine Sound Clips")]
    public AudioClip engineStartClip;        // 1. Encendido del motor
    public AudioClip idleLoopClip;           // 2. Loop motor encendido sin acelerar
    public AudioClip accelerationStartClip;  // 3. Comienzo de aceleración
    public AudioClip fullThrottleLoopClip;   // 4. Loop motor a máximo
    public AudioClip decelerationClip;       // 5. Desaceleración
    public AudioClip engineStopClip;         // 6. Apagado del motor

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float idleVolume = 0.5f;
    [Range(0f, 1f)] public float fullThrottleVolume = 0.8f;
    [Range(0.5f, 2f)] public float idlePitch = 1f;
    [Range(0.5f, 2f)] public float fullThrottlePitch = 1.2f;

    [Header("Transition Settings")]
    public float fadeSpeed = 5f;
    public float throttleDeadZone = 0.1f;    // Zona muerta para considerar "acelerando"

    // Estados internos
    private bool engineRunning = false;
    private bool isAccelerating = false;
    private bool wasAccelerating = false;
    private float currentThrottleValue = 0f;
    private float targetVolume = 0f;
    private float targetPitch = 1f;
    private bool isTransitioning = false;

    private void Awake() {
        // Buscar componentes si no están asignados
        if (engineSwitch == null) engineSwitch = GetComponent<XRSwitch>();
        if (throttle == null) throttle = GetComponent<ThrottleXR>();

        // Configurar audio sources si no están asignados
        if (engineAudioSource == null) {
            engineAudioSource = gameObject.AddComponent<AudioSource>();
            engineAudioSource.spatialBlend = 1f;
            engineAudioSource.loop = true;
        }

        if (oneShotAudioSource == null) {
            oneShotAudioSource = gameObject.AddComponent<AudioSource>();
            oneShotAudioSource.spatialBlend = 1f;
        }
    }

    private void Start() {
        // Suscribir a eventos del switch
        if (engineSwitch != null) {
            engineSwitch.OnSwitchOn.AddListener(OnEngineSwitchOn);
            engineSwitch.OnSwitchOff.AddListener(OnEngineSwitchOff);
        }

        // Suscribir a eventos del throttle
        if (throttle != null) {
            throttle.OnValueChange.AddListener(OnThrottleValueChanged);
        }

        // Configurar audio inicial
        engineAudioSource.volume = 0f;
        engineAudioSource.Stop();
    }

    private void Update() {
        if (!engineRunning) return;

        // Smooth del volumen y pitch
        if (!isTransitioning) {
            engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, targetVolume, Time.deltaTime * fadeSpeed);
            engineAudioSource.pitch = Mathf.Lerp(engineAudioSource.pitch, targetPitch, Time.deltaTime * fadeSpeed);
        }

        // Detectar cambio de estado de aceleración
        isAccelerating = currentThrottleValue > throttleDeadZone;

        if (isAccelerating != wasAccelerating) {
            if (isAccelerating) {
                // Cambió de IDLE a ACELERANDO
                StartAcceleration();
            }
            else {
                // Cambió de ACELERANDO a IDLE
                StartDeceleration();
            }

            wasAccelerating = isAccelerating;
        }
    }

    // Evento: Switch encendido
    private void OnEngineSwitchOn() {
        if (engineRunning) return;

        Debug.Log("Encendiendo motor...");
        engineRunning = true;

        // Reproducir sonido de encendido
        if (engineStartClip != null && oneShotAudioSource != null) {
            oneShotAudioSource.PlayOneShot(engineStartClip);
        }

        // Configurar para estado IDLE
        SetupIdleState();
    }

    // Evento: Switch apagado
    private void OnEngineSwitchOff() {
        if (!engineRunning) return;

        Debug.Log("Apagando motor...");
        engineRunning = false;

        // Detener audio del motor
        engineAudioSource.Stop();
        engineAudioSource.volume = 0f;

        // Reproducir sonido de apagado
        if (engineStopClip != null && oneShotAudioSource != null) {
            oneShotAudioSource.PlayOneShot(engineStopClip);
        }

        // Resetear estados
        isAccelerating = false;
        wasAccelerating = false;
        isTransitioning = false;
    }

    // Evento: Throttle cambió
    private void OnThrottleValueChanged(float throttleValue) {
        currentThrottleValue = throttleValue;

        if (!engineRunning) return;

        // Actualizar parámetros de audio según el throttle
        if (isAccelerating && engineAudioSource.clip == fullThrottleLoopClip) {
            // Ajustar volumen/pitch según throttle (solo si estamos en modo full throttle)
            float normalizedThrottle = Mathf.InverseLerp(throttleDeadZone, 1f, throttleValue);
            targetVolume = Mathf.Lerp(idleVolume, fullThrottleVolume, normalizedThrottle);
            targetPitch = Mathf.Lerp(idlePitch, fullThrottlePitch, normalizedThrottle);
        }
    }

    // Configurar estado IDLE del motor
    private void SetupIdleState() {
        if (idleLoopClip == null) return;

        engineAudioSource.clip = idleLoopClip;
        targetVolume = idleVolume;
        targetPitch = idlePitch;

        if (!engineAudioSource.isPlaying) {
            engineAudioSource.Play();
        }

        isTransitioning = false;
    }

    // Comenzar aceleración
    private void StartAcceleration() {
        if (!engineRunning) return;

        Debug.Log("Comenzando aceleración...");
        isTransitioning = true;

        // Reproducir sonido de aceleración inicial
        if (accelerationStartClip != null && oneShotAudioSource != null) {
            oneShotAudioSource.PlayOneShot(accelerationStartClip);

            // Cambiar a full throttle loop después de que termine la aceleración inicial
            Invoke(nameof(SwitchToFullThrottle), accelerationStartClip.length * 0.8f);
        }
        else {
            // Si no hay clip de aceleración, cambiar inmediatamente
            SwitchToFullThrottle();
        }
    }

    // Cambiar a loop de throttle máximo
    private void SwitchToFullThrottle() {
        if (!engineRunning) return;

        if (fullThrottleLoopClip != null) {
            engineAudioSource.clip = fullThrottleLoopClip;

            // Configurar volumen/pitch según throttle actual
            float normalizedThrottle = Mathf.InverseLerp(throttleDeadZone, 1f, currentThrottleValue);
            targetVolume = Mathf.Lerp(idleVolume, fullThrottleVolume, normalizedThrottle);
            targetPitch = Mathf.Lerp(idlePitch, fullThrottlePitch, normalizedThrottle);

            if (!engineAudioSource.isPlaying) {
                engineAudioSource.Play();
            }
        }

        isTransitioning = false;
    }

    // Comenzar desaceleración
    private void StartDeceleration() {
        if (!engineRunning) return;

        Debug.Log("Comenzando desaceleración...");
        isTransitioning = true;

        // Reproducir sonido de desaceleración
        if (decelerationClip != null && oneShotAudioSource != null) {
            oneShotAudioSource.PlayOneShot(decelerationClip);

            // Volver a idle después de que termine la desaceleración
            Invoke(nameof(SwitchToIdle), decelerationClip.length * 0.8f);
        }
        else {
            // Si no hay clip de desaceleración, cambiar inmediatamente
            SwitchToIdle();
        }
    }

    // Volver a estado IDLE
    private void SwitchToIdle() {
        if (!engineRunning) return;

        SetupIdleState();
    }

    // Métodos públicos para control manual (opcional)
    public void StartEngine() {
        OnEngineSwitchOn();
    }

    public void StopEngine() {
        OnEngineSwitchOff();
    }

    public void SetThrottle(float value) {
        OnThrottleValueChanged(value);
    }

    private void OnDestroy() {
        // Limpiar eventos
        if (engineSwitch != null) {
            engineSwitch.OnSwitchOn.RemoveListener(OnEngineSwitchOn);
            engineSwitch.OnSwitchOff.RemoveListener(OnEngineSwitchOff);
        }

        if (throttle != null) {
            throttle.OnValueChange.RemoveListener(OnThrottleValueChanged);
        }
    }
}