using UnityEngine;

namespace Charly.Airfield {
    /// <summary>
    /// PAPI (indicador de trayectoria de aproximación): 4 luces junto a la pista que se ven blancas o rojas
    /// según el ángulo desde el que las mira el piloto.
    /// - En la senda correcta (~3°): 2 blancas y 2 rojas.
    /// - Muy alto: 4 blancas. Muy bajo: 4 rojas.
    ///
    /// Lo crea Tools > Luces de aeródromo. Las lentes van de la unidad más cercana a la pista a la más lejana.
    /// </summary>
    public class PapiLights : MonoBehaviour {
        [Header("Lentes")]
        [Tooltip("Renderers de las lentes, de la unidad más cercana a la pista a la más lejana")]
        [SerializeField] private Renderer[] lenses;
        [Tooltip("Ángulo de ajuste de cada unidad (°): por encima se ve blanca, por debajo roja")]
        [SerializeField] private float[] angles = { 3.5f, 3.1667f, 2.8333f, 2.5f };

        [Header("Colores")]
        [SerializeField] private Color white = new Color(1f, 0.96f, 0.88f);
        [SerializeField] private Color red = new Color(1f, 0.08f, 0.04f);
        [Tooltip("Multiplicador de la emisión")]
        [SerializeField] private float emissionIntensity = 3f;

        [Header("Observador")]
        [Tooltip("Cámara del piloto. Vacío = Camera.main")]
        [SerializeField] private Camera viewer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock m_Block;
        private int[] m_State;   // -1 sin asignar, 0 roja, 1 blanca

        /// <summary>Lo usa el generador del editor para asignar lentes y ángulos.</summary>
        public void Configure(Renderer[] lensRenderers, float[] settingAngles) {
            lenses = lensRenderers;
            angles = settingAngles;
        }

        private void Awake() {
            m_Block = new MaterialPropertyBlock();
            m_State = new int[lenses != null ? lenses.Length : 0];
            for (int i = 0; i < m_State.Length; i++) m_State[i] = -1;
        }

        private void Update() {
            if (viewer == null) viewer = Camera.main;
            if (viewer == null || lenses == null) return;

            Vector3 eye = viewer.transform.position;
            for (int i = 0; i < lenses.Length && i < angles.Length; i++) {
                var lens = lenses[i];
                if (lens == null) continue;

                Vector3 d = eye - lens.transform.position;
                float elevation = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                int state = elevation >= angles[i] ? 1 : 0;
                if (state == m_State[i]) continue;   // solo se toca el renderer cuando cambia

                m_State[i] = state;
                Color c = state == 1 ? white : red;
                lens.GetPropertyBlock(m_Block);
                m_Block.SetColor(BaseColorId, c);
                m_Block.SetColor(EmissionColorId, c * emissionIntensity);
                lens.SetPropertyBlock(m_Block);
            }
        }
    }
}
