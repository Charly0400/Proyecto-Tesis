using UnityEngine;

namespace Charly.Airfield {
    /// <summary>
    /// Faro giratorio de aeródromo: hace girar la cabeza con las lentes verde y blanca. Es SOLO visual.
    /// Va en el objeto de la cabeza, con el pivote en su centro. Lo crea Tools > Luces de aeródromo.
    /// </summary>
    public class RotatingBeacon : MonoBehaviour {
        [Tooltip("Vueltas por minuto (los faros reales destellan entre 12 y 30 veces por minuto)")]
        [SerializeField] private float rpm = 12f;

        private void Update() {
            // RPM * 360 / 60 = grados por segundo
            transform.Rotate(Vector3.up, rpm * 6f * Time.deltaTime, Space.Self);
        }
    }
}
