using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Charly.FlightController {
    /// <summary>
    /// Ametralladora fija del avión. Dispara mientras se aprieta el GATILLO de la mano que sostiene el joystick
    /// (evento 'activate' de XRI sobre JoystickXRV2), alternando entre los cañones.
    /// Las balas (Projectile) salen de un pool, se les suma la velocidad del avión y hacen raycast por tramos.
    /// Ponerlo en un hijo del avión con su eje Z (azul) alineado con los cañones del modelo: hacia ahí apunta.
    /// </summary>
    public class AircraftGun : MonoBehaviour {
        [Header("Referencias")]
        [Tooltip("Joystick cuyo gatillo dispara. Si se deja vacío se busca en el avión")]
        [SerializeField] private JoystickXRV2 flightStick;
        [Tooltip("Rigidbody del avión: su velocidad se suma a la bala y sus colliders se ignoran. Si se deja vacío se busca en los padres")]
        [SerializeField] private Rigidbody aircraftBody;
        [Tooltip("Para disparar solo con el piloto sentado. Si se deja vacío se busca en los padres")]
        [SerializeField] private AircraftVehicle aircraft;
        [Tooltip("Puntos de salida de las balas (se alternan). Solo importa su posición: la dirección la da el eje Z (azul) de este objeto. Vacío = este transform")]
        [SerializeField] private Transform[] muzzles;
        [Tooltip("Fogonazo de cada cañón, en el mismo orden que 'muzzles' (opcional)")]
        [SerializeField] private ParticleSystem[] muzzleFlashes;

        [Header("Disparo")]
        [Tooltip("Disparos por minuto, entre todos los cañones")]
        public float roundsPerMinute = 900f;
        [Tooltip("Velocidad de salida de la bala en m/s (se le suma la del avión)")]
        public float muzzleVelocity = 850f;
        [Tooltip("Dispersión en grados (radio del cono)")]
        public float spread = 0.4f;
        [Tooltip("Distancia en metros, sobre el eje Z de este objeto, donde se cruzan las balas de todos los cañones. 0 = disparan en paralelo")]
        public float convergenceDistance = 400f;
        [Tooltip("Munición total. 0 o menos = infinita")]
        public int maxAmmo = 1000;
        [Tooltip("Arma armada. Se puede conectar a un XRSwitch (Master Arm) con SetArmed")]
        public bool armed = true;

        [Header("Balas")]
        [Tooltip("Prefab con Projectile y un TrailRenderer. Si se deja vacío se genera uno sencillo con 'tracerMaterial'")]
        [SerializeField] private Projectile projectilePrefab;
        [Tooltip("Material de la estela para la bala generada. Si se deja vacío se usa URP Particles/Unlit (solo seguro en el editor)")]
        [SerializeField] private Material tracerMaterial;
        public float damage = 10f;
        [Tooltip("Segundos que vive una bala que no choca con nada")]
        public float bulletLifetime = 3f;
        [Tooltip("1 = gravedad real, 0 = la bala va en línea recta")]
        public float gravityScale = 1f;
        [Tooltip("Cada cuántas balas una es trazador (visible). 1 = todas")]
        public int tracerEvery = 4;
        [Tooltip("Impulso que se aplica a los Rigidbody impactados")]
        public float impactForce = 20f;
        [Tooltip("Capas contra las que chocan las balas. Los colliders del propio avión se ignoran siempre")]
        public LayerMask hitMask = ~0;

        [Header("Impactos")]
        [Tooltip("Partículas que se reproducen donde pega la bala (opcional). Se crean varias copias y se reciclan")]
        [SerializeField] private ParticleSystem impactEffectPrefab;
        [SerializeField] private int impactEffectCount = 20;

        [Header("Audio")]
        [Tooltip("Si se deja vacío se busca en este GameObject")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shotClip;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Header("Háptica (control que sostiene el joystick)")]
        [Range(0f, 1f)]
        public float hapticAmplitude = 0.35f;
        public float hapticDuration = 0.04f;

        [Header("Events")]
        [Tooltip("Munición restante después de cada disparo y al recargar")]
        public UnityEvent<int> OnAmmoChanged;

        /// <summary>Raíz del avión: las balas ignoran todo lo que cuelga de aquí.</summary>
        public Transform ShooterRoot => aircraftBody != null ? aircraftBody.transform : transform.root;
        public int Ammo => m_Ammo;
        public bool IsFiring => m_TriggerHeld && CanFire;
        private bool HasInfiniteAmmo => maxAmmo <= 0;
        private bool CanFire => armed && (HasInfiniteAmmo || m_Ammo > 0) && (aircraft == null || aircraft.isPlayerSeated);

        private ObjectPool<Projectile> m_Pool;
        private Transform m_Container;
        private ParticleSystem[] m_Impacts;
        private int m_NextImpact;

        private bool m_TriggerHeld;
        private XRBaseInputInteractor m_HapticInteractor;
        private float m_ShotTimer;
        private int m_NextMuzzle;
        private int[] m_MuzzleShots;   // balas disparadas por cada cañón (para repartir las trazadoras)
        private int m_Ammo;

        #region Unity Methods
        private void Start() {
            if (aircraftBody == null)
                aircraftBody = GetComponentInParent<Rigidbody>();
            if (aircraft == null)
                aircraft = GetComponentInParent<AircraftVehicle>();
            if (flightStick == null)
                flightStick = ShooterRoot.GetComponentInChildren<JoystickXRV2>(true);
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (muzzles == null || muzzles.Length == 0)
                muzzles = new[] { transform };
            m_MuzzleShots = new int[muzzles.Length];

            // Las balas viven fuera del avión: si fueran hijas se moverían con él
            m_Container = new GameObject($"{name}_Balas").transform;
            if (projectilePrefab == null)
                projectilePrefab = CreateDefaultProjectile();
            m_Pool = new ObjectPool<Projectile>(
                () => Instantiate(projectilePrefab, m_Container),
                p => p.gameObject.SetActive(true),
                p => p.gameObject.SetActive(false),
                p => { if (p != null) Destroy(p.gameObject); },
                collectionCheck: false, defaultCapacity: 32, maxSize: 256);

            CreateImpactEffects();
            Reload();

            if (flightStick != null) {
                flightStick.activated.AddListener(OnTriggerPressed);
                flightStick.deactivated.AddListener(OnTriggerReleased);
                flightStick.selectExited.AddListener(OnStickReleased);
            }
            else {
                Debug.LogWarning($"{name}: no se encontró un JoystickXRV2, el arma no podrá dispararse con el gatillo.", this);
            }
        }

        private void Update() {
            if (!IsFiring) return;

            float interval = 60f / Mathf.Max(1f, roundsPerMinute);
            // Tras un tirón de FPS no se dispara una ráfaga de golpe
            m_ShotTimer = Mathf.Min(m_ShotTimer + Time.deltaTime, interval * 3f);

            while (m_ShotTimer >= interval && CanFire) {
                m_ShotTimer -= interval;
                FireOnce();
            }
        }

        private void OnDestroy() {
            if (flightStick != null) {
                flightStick.activated.RemoveListener(OnTriggerPressed);
                flightStick.deactivated.RemoveListener(OnTriggerReleased);
                flightStick.selectExited.RemoveListener(OnStickReleased);
            }

            m_Pool?.Dispose();
            if (m_Container != null)
                Destroy(m_Container.gameObject);
        }
        #endregion

        #region Gatillo
        private void OnTriggerPressed(ActivateEventArgs args) {
            m_TriggerHeld = true;
            m_HapticInteractor = args.interactorObject as XRBaseInputInteractor;
            // El primer disparo sale en el mismo frame en que se aprieta
            m_ShotTimer = 60f / Mathf.Max(1f, roundsPerMinute);
        }

        private void OnTriggerReleased(DeactivateEventArgs args) {
            StopFiring();
        }

        private void OnStickReleased(SelectExitEventArgs args) {
            // Al soltar el joystick con el gatillo apretado no llega 'deactivated'
            StopFiring();
        }

        private void StopFiring() {
            m_TriggerHeld = false;
            m_HapticInteractor = null;
        }
        #endregion

        #region Disparo
        private void FireOnce() {
            int index = m_NextMuzzle;
            m_NextMuzzle = (m_NextMuzzle + 1) % muzzles.Length;
            Transform muzzle = muzzles[index];

            // Se apunta con el eje Z de ESTE objeto (no con el del avión): el modelo puede estar inclinado
            // respecto a la raíz, y así basta con alinear este objeto con los cañones del modelo
            Vector3 direction = transform.forward;
            if (convergenceDistance > 0f) {
                Vector3 convergencePoint = transform.position + transform.forward * convergenceDistance;
                direction = (convergencePoint - muzzle.position).normalized;
            }
            direction = ApplySpread(direction);

            Vector3 velocity = direction * muzzleVelocity;
            if (aircraftBody != null)
                velocity += aircraftBody.linearVelocity;

            // Cada cañón lleva su propia cuenta, desfasada según su índice. Con una cuenta global, si el número
            // de cañones es múltiplo de 'tracerEvery' (8 y 4) las trazadoras caen siempre en los mismos cañones
            bool isTracer = tracerEvery <= 1 || (m_MuzzleShots[index] + index) % tracerEvery == 0;
            m_MuzzleShots[index]++;
            m_Pool.Get().Launch(this, muzzle.position, velocity, isTracer);

            if (muzzleFlashes != null && index < muzzleFlashes.Length && muzzleFlashes[index] != null)
                muzzleFlashes[index].Play(true);

            if (audioSource != null && shotClip != null) {
                audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
                audioSource.PlayOneShot(shotClip);
            }

            if (m_HapticInteractor != null && hapticAmplitude > 0f)
                m_HapticInteractor.SendHapticImpulse(hapticAmplitude, hapticDuration);

            if (!HasInfiniteAmmo) {
                m_Ammo--;
                OnAmmoChanged?.Invoke(m_Ammo);
            }
        }

        private Vector3 ApplySpread(Vector3 direction) {
            if (spread <= 0f) return direction;
            Vector2 offset = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
            return (Quaternion.LookRotation(direction) * new Vector3(offset.x, offset.y, 1f)).normalized;
        }

        /// <summary>Para conectar por el Inspector un switch de Master Arm.</summary>
        public void SetArmed(bool value) {
            armed = value;
        }

        /// <summary>Munición al máximo. Conectar a CrashHandler.onRespawned para recargar al reaparecer.</summary>
        public void Reload() {
            m_Ammo = maxAmmo;
            OnAmmoChanged?.Invoke(m_Ammo);
        }
        #endregion

        #region Balas e impactos
        /// <summary>Lo llama Projectile al chocar: daño, empuje físico, efecto y regreso al pool.</summary>
        public void HandleHit(Projectile projectile, RaycastHit hit, Vector3 velocity) {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(damage, hit.point, hit.normal);

            if (hit.rigidbody != null && impactForce > 0f)
                hit.rigidbody.AddForceAtPosition(velocity.normalized * impactForce, hit.point, ForceMode.Impulse);

            PlayImpact(hit.point, hit.normal);
            ReleaseProjectile(projectile);
        }

        public void ReleaseProjectile(Projectile projectile) {
            m_Pool.Release(projectile);
        }

        private void CreateImpactEffects() {
            if (impactEffectPrefab == null || impactEffectCount <= 0) return;

            m_Impacts = new ParticleSystem[impactEffectCount];
            for (int i = 0; i < m_Impacts.Length; i++) {
                m_Impacts[i] = Instantiate(impactEffectPrefab, m_Container);
                m_Impacts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>Recicla los efectos en orden: si hay más impactos que copias, se reusa el más viejo.</summary>
        private void PlayImpact(Vector3 point, Vector3 normal) {
            if (m_Impacts == null) return;

            ParticleSystem effect = m_Impacts[m_NextImpact];
            m_NextImpact = (m_NextImpact + 1) % m_Impacts.Length;

            effect.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
            effect.Clear(true);
            effect.Play(true);
        }

        /// <summary>
        /// Bala por defecto para probar sin hacer un prefab: un objeto inactivo con una estela amarilla.
        /// Queda dentro del contenedor y sirve de plantilla para el pool.
        /// </summary>
        private Projectile CreateDefaultProjectile() {
            var template = new GameObject("Bala (plantilla)");
            template.SetActive(false);
            template.transform.SetParent(m_Container, false);

            var trail = template.AddComponent<TrailRenderer>();
            trail.time = 0.04f;
            trail.minVertexDistance = 1f;
            trail.widthMultiplier = 0.12f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.colorGradient = new Gradient {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Material material = tracerMaterial;
            if (material == null) {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null) material = new Material(shader);
                Debug.LogWarning($"{name}: no hay 'projectilePrefab' ni 'tracerMaterial'. Se usa un material generado que puede no existir en el build.", this);
            }
            trail.sharedMaterial = material;

            return template.AddComponent<Projectile>();
        }
        #endregion
    }
}
