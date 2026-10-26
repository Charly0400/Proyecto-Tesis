using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Charly.FlightController {
    /// <summary>
    /// Sube y baja el tren de aterrizaje (piernas, pistones telescópicos, compuertas y rueda de cola). Es SOLO visual
    /// y de colisión: las ruedas llevan sus colliders, así que con el tren arriba el avión apoya la panza.
    ///
    /// Cada pieza guarda dos poses locales (tren ARRIBA y tren ABAJO) y se mueve en su 'tramo' del recorrido
    /// (0 = arriba, 1 = abajo). Las compuertas de tipo 'Compuerta' se abren mientras el tren se mueve y se cierran
    /// al terminar (opcional con el tren abajo). GeneradorP47 llena todo esto solo.
    ///
    /// Para la cabina: conectar un XRSwitch a BajarTren / SubirTren (o AlternarTren), o una palanca a SetPalancaTren (0..1).
    /// No deja subir el tren con el avión en tierra (lee AircraftPhysics.IsOnGround).
    /// </summary>
    public class LandingGearAnimator : MonoBehaviour {
        public enum TipoPieza { Recorrido, Compuerta }

        [Serializable]
        public class Pieza {
            public Transform pieza;
            public TipoPieza tipo = TipoPieza.Recorrido;
            [Tooltip("Parte del recorrido (0 = arriba .. 1 = abajo) en la que se mueve. En compuertas: cuándo se abren al bajar el tren")]
            public Vector2 tramo = new Vector2(0f, 1f);
            [Tooltip("Pose local con el tren ARRIBA (en compuertas: cerrada)")]
            public Vector3 posicionArriba;
            public Quaternion rotacionArriba = Quaternion.identity;
            [Tooltip("Pose local con el tren ABAJO (en compuertas: abierta)")]
            public Vector3 posicionAbajo;
            public Quaternion rotacionAbajo = Quaternion.identity;
        }

        [Serializable]
        public class Rueda {
            public Transform rueda;
            public float radio = 0.43f;
            [Tooltip("Eje de giro en ejes LOCALES de la rueda")]
            public Vector3 eje = Vector3.right;
        }

        [Header("Referencias")]
        [Tooltip("Si se deja vacío se busca en los padres")]
        [SerializeField] private AircraftPhysics aircraftPhysics;
        [Tooltip("Si se deja vacío se busca en los padres (para girar las ruedas al rodar)")]
        [SerializeField] private Rigidbody body;

        [Header("Estado")]
        [SerializeField] private bool empiezaAbajo = true;
        [Tooltip("Segundos de un ciclo completo (el P-47 real tardaba unos 6 s)")]
        [SerializeField] private float duracion = 6f;
        [Tooltip("Cerrar las compuertas de las ruedas una vez que el tren terminó de bajar (como el P-47). Si no, quedan abiertas")]
        [SerializeField] private bool cerrarCompuertasConTrenAbajo = true;
        [Tooltip("No permite subir el tren mientras alguna rueda toque el suelo")]
        [SerializeField] private bool bloquearEnTierra = true;

        [Header("Piezas")]
        [SerializeField] private List<Pieza> piezas = new List<Pieza>();
        [Tooltip("Ruedas que giran al rodar en tierra")]
        [SerializeField] private List<Rueda> ruedas = new List<Rueda>();

        [Header("Eventos")]
        public UnityEvent onEmpiezaASubir;
        public UnityEvent onEmpiezaABajar;
        public UnityEvent onTrenArriba;
        public UnityEvent onTrenAbajo;

        /// <summary>0 = tren arriba, 1 = tren abajo.</summary>
        public float Progreso => m_Progreso;
        /// <summary>Hacia dónde va el tren (true = abajo).</summary>
        public bool TrenAbajo => m_Objetivo;
        public bool EnMovimiento => !Mathf.Approximately(m_Progreso, m_Objetivo ? 1f : 0f);

        private float m_Progreso;
        private bool m_Objetivo;

        private void Awake() {
            if (aircraftPhysics == null) aircraftPhysics = GetComponentInParent<AircraftPhysics>();
            if (body == null) body = GetComponentInParent<Rigidbody>();
            m_Objetivo = empiezaAbajo;
            m_Progreso = empiezaAbajo ? 1f : 0f;
            Aplicar();
        }

        private void Update() {
            float objetivo = m_Objetivo ? 1f : 0f;
            if (!Mathf.Approximately(m_Progreso, objetivo)) {
                m_Progreso = Mathf.MoveTowards(m_Progreso, objetivo, Time.deltaTime / Mathf.Max(0.1f, duracion));
                Aplicar();
                if (Mathf.Approximately(m_Progreso, objetivo)) {
                    if (m_Objetivo) onTrenAbajo?.Invoke();
                    else onTrenArriba?.Invoke();
                }
            }
            GirarRuedas();
        }

        public void BajarTren() => SetTren(true);
        public void SubirTren() => SetTren(false);
        public void AlternarTren() => SetTren(!m_Objetivo);

        /// <summary>Para una palanca 0..1: arriba de 0.5 = tren abajo.</summary>
        public void SetPalancaTren(float valor) => SetTren(valor >= 0.5f);

        public void SetTren(bool abajo) {
            if (abajo == m_Objetivo) return;
            if (!abajo && bloquearEnTierra && aircraftPhysics != null && aircraftPhysics.IsOnGround) {
                Debug.Log("Tren de aterrizaje: no se puede subir con el avión en tierra", this);
                return;
            }
            m_Objetivo = abajo;
            if (abajo) onEmpiezaABajar?.Invoke();
            else onEmpiezaASubir?.Invoke();
        }

        /// <summary>Lleva todas las piezas a la pose de 'progreso' (0 = arriba, 1 = abajo). También sirve en el editor.</summary>
        public void Aplicar(float progreso) {
            m_Progreso = Mathf.Clamp01(progreso);
            Aplicar();
        }

        private void Aplicar() {
            foreach (var p in piezas) {
                if (p.pieza == null) continue;
                float t = Peso(p, m_Progreso);
                p.pieza.localPosition = Vector3.Lerp(p.posicionArriba, p.posicionAbajo, t);
                p.pieza.localRotation = Quaternion.Slerp(p.rotacionArriba, p.rotacionAbajo, t);
            }
        }

        private float Peso(Pieza p, float progreso) {
            float abre = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.tramo.x, p.tramo.y, progreso));
            if (p.tipo == TipoPieza.Recorrido) return abre;
            // Compuerta: abierta en medio del recorrido; se cierra en el tramo simétrico del final
            if (!cerrarCompuertasConTrenAbajo) return abre;
            float cierra = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - p.tramo.y, 1f - p.tramo.x, progreso));
            return abre * (1f - cierra);
        }

        private void GirarRuedas() {
            if (body == null || ruedas.Count == 0 || m_Progreso < 0.99f) return;
            if (aircraftPhysics != null && !aircraftPhysics.IsOnGround) return;
            float avance = Vector3.Dot(body.linearVelocity, body.transform.forward);
            foreach (var r in ruedas) {
                if (r.rueda == null || r.radio <= 0f) continue;
                float grados = avance / r.radio * Mathf.Rad2Deg * Time.deltaTime;
                r.rueda.Rotate(r.eje, grados, Space.Self);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Probar: tren arriba")]
        private void ProbarArriba() { UnityEditor.Undo.RecordObjects(Transforms(), "Tren arriba"); Aplicar(0f); }

        [ContextMenu("Probar: tren abajo")]
        private void ProbarAbajo() { UnityEditor.Undo.RecordObjects(Transforms(), "Tren abajo"); Aplicar(1f); }

        private UnityEngine.Object[] Transforms() {
            var lista = new List<UnityEngine.Object>();
            foreach (var p in piezas) if (p.pieza != null) lista.Add(p.pieza);
            return lista.ToArray();
        }
#endif
    }
}
