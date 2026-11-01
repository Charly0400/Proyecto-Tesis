using UnityEngine;

/// <summary>
/// Guarda abatible de un XRSwitch (por ejemplo el encendido del motor): se levanta cuando la mano se acerca o agarra
/// el switch y se queda arriba mientras está encendido; al apagarlo y alejar la mano vuelve a taparlo.
/// Solo visual. GeneradorControlesP47 lo configura solo.
/// </summary>
public class GuardaSwitchVisual : MonoBehaviour {
    [Tooltip("Si se deja vacío se busca en este objeto")]
    [SerializeField] private XRSwitch interruptor;
    [Tooltip("Gira sobre su eje X local (la bisagra)")]
    [SerializeField] private Transform guarda;
    [Tooltip("Grados que gira al abrirse: 160 la deja recargada en el tablero, arriba del switch")]
    [SerializeField] private float anguloAbierta = 160f;
    [SerializeField] private float velocidad = 12f;

    private Quaternion m_Cerrada;

    private void Awake() {
        if (interruptor == null) interruptor = GetComponent<XRSwitch>();
        if (guarda != null) m_Cerrada = guarda.localRotation;
    }

    private void Update() {
        if (guarda == null || interruptor == null) return;
        bool abierta = interruptor.isHovered || interruptor.isSelected || interruptor.IsOn;
        var objetivo = m_Cerrada * Quaternion.Euler(abierta ? anguloAbierta : 0f, 0f, 0f);
        guarda.localRotation = Quaternion.Slerp(guarda.localRotation, objetivo, 1f - Mathf.Exp(-velocidad * Time.deltaTime));
    }
}
