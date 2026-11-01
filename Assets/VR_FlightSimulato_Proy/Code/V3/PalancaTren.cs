using Charly.FlightController;
using UnityEngine;

/// <summary>
/// Conecta una palanca de tren (XRSwitch: encendido = tren abajo) con el LandingGearAnimator del avión y maneja sus
/// luces: verde = tren abajo y asegurado, roja = tren en movimiento, apagadas = tren arriba. Si el tren no puede
/// subir (avión en tierra), la palanca regresa sola a ABAJO al soltarla. GeneradorControlesP47 lo configura solo.
/// </summary>
[RequireComponent(typeof(XRSwitch))]
public class PalancaTren : MonoBehaviour {
    [Tooltip("Si se deja vacío se busca en los padres")]
    [SerializeField] private LandingGearAnimator tren;
    [SerializeField] private Renderer luzVerde, luzRoja;
    [SerializeField] private Material verdeEncendida, verdeApagada, rojaEncendida, rojaApagada;

    private XRSwitch m_Palanca;

    private void Awake() {
        m_Palanca = GetComponent<XRSwitch>();
        if (tren == null) tren = GetComponentInParent<LandingGearAnimator>();
        if (tren == null) {
            // El tren cuelga de Gear_Parts, que es hermano de la cabina: se busca desde la raíz del avión
            var raiz = transform.root;
            tren = raiz.GetComponentInChildren<LandingGearAnimator>(true);
        }
    }

    private void OnEnable() {
        m_Palanca.OnSwitchOn.AddListener(Bajar);
        m_Palanca.OnSwitchOff.AddListener(Subir);
    }

    private void OnDisable() {
        m_Palanca.OnSwitchOn.RemoveListener(Bajar);
        m_Palanca.OnSwitchOff.RemoveListener(Subir);
    }

    private void Bajar() { if (tren != null) tren.BajarTren(); }
    private void Subir() { if (tren != null) tren.SubirTren(); }

    private void Update() {
        if (tren == null) return;
        // Bloqueado en tierra: la palanca dice ARRIBA pero el tren sigue abajo; al soltarla vuelve a ABAJO
        if (!m_Palanca.IsOn && tren.TrenAbajo && !m_Palanca.isSelected)
            m_Palanca.SetSwitchState(true);

        bool verde = tren.TrenAbajo && !tren.EnMovimiento, roja = tren.EnMovimiento;
        Poner(luzVerde, verde ? verdeEncendida : verdeApagada);
        Poner(luzRoja, roja ? rojaEncendida : rojaApagada);
    }

    private static void Poner(Renderer r, Material m) {
        if (r != null && m != null && r.sharedMaterial != m) r.sharedMaterial = m;
    }
}
