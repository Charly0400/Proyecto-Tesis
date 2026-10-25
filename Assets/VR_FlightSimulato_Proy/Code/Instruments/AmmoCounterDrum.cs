using UnityEngine;
using UnityEngine.UI;

namespace Charly.FlightController.Instruments {
    /// <summary>
    /// Contador mecánico de munición tipo odómetro. Funciona con dos tipos de rodillo (se pueden usar ambos):
    /// - 3D: prismas de 10 caras con la tira de dígitos pegada (un número por cara). Giran 36° por número.
    /// - 2D: RawImage con la tira vertical 0-9 (Wrap Mode = Repeat), que se desplaza cambiando su uvRect.
    /// Las unidades ruedan de forma continua; cada rodillo superior solo rueda mientras el de su derecha pasa de 9 a 0.
    /// Se actualiza con el evento OnAmmoChanged del AircraftGun.
    /// </summary>
    public class AmmoCounterDrum : MonoBehaviour {
        [Tooltip("Arma cuya munición se muestra. Si se deja vacío se busca en el avión")]
        [SerializeField] private AircraftGun m_gun;

        [Header("Ametralladoras")]
        [Tooltip("De DERECHA a IZQUIERDA: unidades, decenas, centenas, millares... El pivote de cada uno debe estar en su centro")]
        [SerializeField] private Transform[] m_drumMeshes;
        [Tooltip("Eje LOCAL sobre el que gira cada rodillo (el cilindro de ProBuilder se crea a lo largo de Y)")]
        [SerializeField] private Vector3 m_rotationAxis = Vector3.up;
        [Tooltip("Giro extra en grados para que el 0 quede centrado en la ventana. Se puede ajustar en Play (luego copiar el valor)")]
        [SerializeField] private float m_zeroAngle = 0f;
        [Tooltip("Invierte el sentido de giro si los números pasan en el orden contrario")]
        [SerializeField] private bool m_invertRotation = false;

        [Header("Rodillos 2D (UI)")]
        [Tooltip("De DERECHA a IZQUIERDA: unidades, decenas, centenas, millares...")]
        [SerializeField] private RawImage[] m_drums;
        [Tooltip("Activar si en la textura el 0 está arriba y el 9 abajo. Apagar si es al revés")]
        [SerializeField] private bool m_zeroAtTop = true;
        [Tooltip("Cuántos dígitos se ven por la ventana. 1 = solo el actual; 1.4 = asoman el anterior y el siguiente")]
        [Range(1f, 3f)]
        [SerializeField] private float m_visibleDigits = 1f;

        [Header("Movimiento")]
        [Tooltip("Factor de suavizado del giro de los rodillos. 0 = cambio instantáneo")]
        [Range(0f, 30f)]
        [SerializeField] private float m_SmoothingFactor = 12f;

        private const float CELL = 0.1f;            // alto de un dígito en UV (10 dígitos por textura)
        private const float DEGREES_PER_DIGIT = 36f; // 360° / 10 caras

        private Quaternion[] m_meshBaseRotations;
        private float m_targetValue;
        private float m_displayValue;
        private float m_appliedValue = -1f;
        private bool m_hasValue;

        private int DrumCount => Mathf.Max(m_drumMeshes != null ? m_drumMeshes.Length : 0, m_drums != null ? m_drums.Length : 0);

        private void Awake() {
            if (m_gun == null) {
                Rigidbody aircraft = GetComponentInParent<Rigidbody>();
                if (aircraft != null)
                    m_gun = aircraft.GetComponentInChildren<AircraftGun>(true);
            }

            if (m_gun == null)
                Debug.LogWarning($"{name}: no se encontró un AircraftGun, el contador no se moverá.", this);

            // El giro se suma a la rotación con la que se colocó cada rodillo en la escena
            if (m_drumMeshes != null) {
                m_meshBaseRotations = new Quaternion[m_drumMeshes.Length];
                for (int i = 0; i < m_drumMeshes.Length; i++) {
                    if (m_drumMeshes[i] != null)
                        m_meshBaseRotations[i] = m_drumMeshes[i].localRotation;
                }
            }
        }

        private void OnEnable() {
            if (m_gun != null)
                m_gun.OnAmmoChanged.AddListener(OnAmmoChanged);
        }

        private void OnDisable() {
            if (m_gun != null)
                m_gun.OnAmmoChanged.RemoveListener(OnAmmoChanged);
        }

        private void OnValidate() {
            // Para ver al instante los cambios de m_zeroAngle / sentido de giro mientras se ajustan en Play
            m_appliedValue = -1f;
        }

        private void Start() {
            // Si el arma todavía no avisó su munición inicial, se arranca con el cargador lleno sin animar
            if (!m_hasValue && m_gun != null)
                SetImmediate(m_gun.maxAmmo);
        }

        private void Update() {
            if (m_SmoothingFactor <= 0f)
                m_displayValue = m_targetValue;
            else
                m_displayValue = Mathf.Lerp(m_displayValue, m_targetValue, 1f - Mathf.Exp(-m_SmoothingFactor * Time.deltaTime));

            if (Mathf.Abs(m_displayValue - m_targetValue) < 0.001f)
                m_displayValue = m_targetValue;

            // Cambiar el uvRect reconstruye el Canvas: solo se hace cuando los rodillos se movieron
            if (!Mathf.Approximately(m_displayValue, m_appliedValue)) {
                ApplyDrums(m_displayValue);
                m_appliedValue = m_displayValue;
            }
        }

        private void OnAmmoChanged(int ammo) {
            // El primer valor (al cargar la escena) se muestra sin animar
            if (!m_hasValue) SetImmediate(ammo);
            else m_targetValue = ClampToDrums(ammo);
        }

        /// <summary>Pone los rodillos en un valor sin animación.</summary>
        public void SetImmediate(int value) {
            m_targetValue = m_displayValue = ClampToDrums(value);
            m_hasValue = true;
        }

        private float ClampToDrums(int value) {
            return Mathf.Clamp(value, 0, Mathf.Pow(10f, DrumCount) - 1f);
        }

        private void ApplyDrums(float value) {
            float place = 1f;
            for (int i = 0; i < DrumCount; i++) {
                float digit = Mathf.Floor(value / place) % 10f;
                // Lo que marcan los rodillos de la derecha: este solo rueda durante su última unidad (…9 → …0)
                float lower = value % place;
                float roll = Mathf.Max(0f, lower - (place - 1f));
                float position = digit + roll;

                if (m_drumMeshes != null && i < m_drumMeshes.Length)
                    SetMeshDrum(i, position);
                if (m_drums != null && i < m_drums.Length)
                    SetImageDrum(m_drums[i], position);

                place *= 10f;
            }
        }

        private void SetMeshDrum(int index, float position) {
            Transform drum = m_drumMeshes[index];
            if (drum == null || m_meshBaseRotations == null) return;

            float angle = m_zeroAngle + (m_invertRotation ? -1f : 1f) * position * DEGREES_PER_DIGIT;
            drum.localRotation = m_meshBaseRotations[index] * Quaternion.AngleAxis(angle, m_rotationAxis);
        }

        private void SetImageDrum(RawImage drum, float position) {
            if (drum == null) return;

            float height = CELL * m_visibleDigits;
            float center = m_zeroAtTop ? 1f - (position + 0.5f) * CELL : (position + 0.5f) * CELL;
            drum.uvRect = new Rect(0f, center - height * 0.5f, 1f, height);
        }
    }
}
