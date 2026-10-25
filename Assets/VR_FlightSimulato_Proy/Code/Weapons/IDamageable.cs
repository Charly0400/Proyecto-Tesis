using UnityEngine;

namespace Charly.FlightController {
    /// <summary>
    /// Cualquier cosa que pueda recibir daño de las balas (blancos, drones, torretas...).
    /// Las balas lo buscan con GetComponentInParent en el collider impactado.
    /// </summary>
    public interface IDamageable {
        void TakeDamage(float amount, Vector3 point, Vector3 normal);
    }
}
