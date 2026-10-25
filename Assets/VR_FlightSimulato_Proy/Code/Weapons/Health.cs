using UnityEngine;
using UnityEngine.Events;

namespace Charly.FlightController {
    /// <summary>
    /// Vida de un blanco. Ponerlo en el objeto raíz del blanco: los colliders hijos también reciben el daño.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable {
        [Header("Vida")]
        public float maxHealth = 100f;
        [Tooltip("Destruir el GameObject al morir. Si está apagado solo se desactiva")]
        public bool destroyOnDeath = true;

        [Header("Muerte")]
        [Tooltip("Efecto que se crea al morir (explosión, humo...). Opcional")]
        [SerializeField] private GameObject deathEffectPrefab;
        [Tooltip("Segundos que vive el efecto de muerte antes de borrarse")]
        [SerializeField] private float deathEffectLifetime = 5f;

        [Header("Events")]
        [Tooltip("Recibe la vida restante después de cada impacto")]
        public UnityEvent<float> OnDamaged;
        public UnityEvent OnDeath;

        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        private void Awake() {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 normal) {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnDamaged?.Invoke(CurrentHealth);

            if (CurrentHealth <= 0f)
                Die();
        }

        /// <summary>Devuelve la vida al máximo y reactiva el objeto (para reaparecer blancos).</summary>
        public void ResetHealth() {
            CurrentHealth = maxHealth;
            IsDead = false;
            gameObject.SetActive(true);
        }

        private void Die() {
            IsDead = true;
            OnDeath?.Invoke();

            if (deathEffectPrefab != null) {
                GameObject effect = Instantiate(deathEffectPrefab, transform.position, transform.rotation);
                Destroy(effect, deathEffectLifetime);
            }

            if (destroyOnDeath) Destroy(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
