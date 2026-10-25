using UnityEngine;

namespace Charly.FlightController {
    /// <summary>
    /// Bala de la ametralladora. NO usa Rigidbody: se mueve por código y en cada frame lanza un raycast
    /// desde la posición anterior hasta la nueva, así no atraviesa nada aunque vaya a 900 m/s.
    /// La crea y la recicla AircraftGun (pool), nunca se destruye.
    /// </summary>
    public class Projectile : MonoBehaviour {
        [Tooltip("Estela del trazador. Si se deja vacío se busca en los hijos")]
        [SerializeField] private TrailRenderer tracerTrail;
        [Tooltip("Malla visible de la bala (opcional). Solo se muestra en los trazadores")]
        [SerializeField] private Renderer tracerRenderer;

        // Compartido por todas las balas: los raycast se hacen uno a la vez en el hilo principal
        private static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        private AircraftGun m_Owner;
        private Vector3 m_Velocity;
        private float m_TimeLeft;
        private bool m_InFlight;

        private void Awake() {
            if (tracerTrail == null)
                tracerTrail = GetComponentInChildren<TrailRenderer>();
        }

        public void Launch(AircraftGun owner, Vector3 position, Vector3 velocity, bool isTracer) {
            m_Owner = owner;
            m_Velocity = velocity;
            m_TimeLeft = owner.bulletLifetime;
            m_InFlight = true;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));

            // Si no se limpia, la estela dibuja una raya desde donde murió la bala anterior
            if (tracerTrail != null) {
                tracerTrail.Clear();
                tracerTrail.emitting = isTracer;
            }
            if (tracerRenderer != null)
                tracerRenderer.enabled = isTracer;
        }

        private void Update() {
            if (!m_InFlight) return;

            float dt = Time.deltaTime;
            m_Velocity += Physics.gravity * (m_Owner.gravityScale * dt);

            Vector3 start = transform.position;
            Vector3 step = m_Velocity * dt;
            float distance = step.magnitude;

            if (distance > 0f && TryHit(start, step / distance, distance, out RaycastHit hit)) {
                transform.position = hit.point;
                m_InFlight = false;
                m_Owner.HandleHit(this, hit, m_Velocity);
                return;
            }

            transform.SetPositionAndRotation(start + step, Quaternion.LookRotation(m_Velocity));

            m_TimeLeft -= dt;
            if (m_TimeLeft <= 0f) {
                m_InFlight = false;
                m_Owner.ReleaseProjectile(this);
            }
        }

        /// <summary>
        /// Impacto más cercano en el tramo recorrido este frame, ignorando el avión que disparó
        /// (y el rig XR, que es hijo del avión mientras el piloto está sentado).
        /// </summary>
        private bool TryHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest) {
            int count = Physics.RaycastNonAlloc(origin, direction, s_Hits, distance, m_Owner.hitMask, QueryTriggerInteraction.Ignore);
            Transform shooter = m_Owner.ShooterRoot;

            closest = default;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < count; i++) {
                RaycastHit hit = s_Hits[i];
                if (shooter != null && hit.collider.transform.IsChildOf(shooter)) continue;
                if (hit.distance < closestDistance) {
                    closestDistance = hit.distance;
                    closest = hit;
                }
            }
            return closestDistance < float.MaxValue;
        }
    }
}
