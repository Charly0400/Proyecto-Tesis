using System;
using System.Collections.Generic;
using UnityEngine;

namespace Charly.FlightController {
    /// <summary>
    /// Abre y cierra las aletas de enfriamiento del motor (cowl flaps) y las compuertas del intercooler. Es SOLO visual.
    /// - Automático: abiertas con el motor encendido a baja velocidad (en tierra el motor se calienta) y cerradas
    ///   en vuelo rápido. Con el motor apagado se cierran.
    /// - Manual: se controlan con SetApertura (0..1), Abrir, Cerrar o Alternar (p. ej. desde un XRSwitch de la cabina).
    ///
    /// Cada aleta va en un objeto vacío sobre su bisagra (borde delantero) con la malla como hijo, igual que
    /// ControlSurfaceAnimator. GeneradorP47 las crea y configura.
    /// </summary>
    public class CowlFlapsAnimator : MonoBehaviour {
        public enum Modo { Automatico, Manual }

        [Serializable]
        public class Aleta {
            public Transform aleta;
            [Tooltip("Eje de la bisagra en ejes LOCALES de la aleta")]
            public Vector3 eje = Vector3.right;
            [Tooltip("Grados con la aleta totalmente abierta")]
            public float anguloAbierta = 15f;
        }

        [Header("Referencias")]
        [Tooltip("Si se deja vacío se busca en los padres")]
        [SerializeField] private FlightControllerV3 input;

        [Header("Control")]
        [SerializeField] private Modo modo = Modo.Automatico;
        [Tooltip("Velocidad (m/s) hasta la que van totalmente abiertas en modo automático")]
        [SerializeField] private float velocidadAbiertas = 45f;
        [Tooltip("Velocidad (m/s) a partir de la que van cerradas en modo automático")]
        [SerializeField] private float velocidadCerradas = 90f;
        [Tooltip("Fracción del recorrido por segundo (0.5 = 2 s de cerradas a abiertas)")]
        [SerializeField] private float rapidez = 0.5f;

        [Header("Aletas")]
        [SerializeField] private List<Aleta> aletas = new List<Aleta>();

        /// <summary>Apertura actual 0..1.</summary>
        public float Apertura => m_Apertura;

        private float m_Apertura;
        private float m_Manual;
        private Quaternion[] m_Reposo;

        private void Awake() {
            if (input == null) input = GetComponentInParent<FlightControllerV3>();
            m_Reposo = new Quaternion[aletas.Count];
            for (int i = 0; i < aletas.Count; i++)
                m_Reposo[i] = aletas[i].aleta != null ? aletas[i].aleta.localRotation : Quaternion.identity;
        }

        private void Update() {
            float objetivo = modo == Modo.Manual ? m_Manual : ObjetivoAutomatico();
            if (Mathf.Approximately(objetivo, m_Apertura)) return;
            m_Apertura = Mathf.MoveTowards(m_Apertura, objetivo, rapidez * Time.deltaTime);
            for (int i = 0; i < aletas.Count; i++) {
                var a = aletas[i];
                if (a.aleta == null || a.eje.sqrMagnitude < 1e-6f) continue;
                a.aleta.localRotation = m_Reposo[i] * Quaternion.AngleAxis(a.anguloAbierta * m_Apertura, a.eje.normalized);
            }
        }

        private float ObjetivoAutomatico() {
            if (input == null || !input.EngineOn) return 0f;
            return 1f - Mathf.InverseLerp(velocidadAbiertas, velocidadCerradas, Mathf.Abs(input.CurrentSpeed));
        }

        /// <summary>Pasa a modo manual con la apertura indicada (0 = cerradas, 1 = abiertas).</summary>
        public void SetApertura(float valor) {
            modo = Modo.Manual;
            m_Manual = Mathf.Clamp01(valor);
        }

        public void Abrir() => SetApertura(1f);
        public void Cerrar() => SetApertura(0f);
        public void Alternar() => SetApertura(m_Manual > 0.5f ? 0f : 1f);
        public void ModoAutomatico() => modo = Modo.Automatico;
    }
}
