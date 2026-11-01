using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Charly.FlightController {
    /// <summary>
    /// Panel de instrucciones tipo teléfono: muestra una diapositiva (Sprite) a la vez y se navega con
    /// los botones Anterior / Siguiente (rayo o dedo en VR). Reemplaza al StepManager de la plantilla VR,
    /// que solo podía avanzar y necesitaba una tarjeta armada a mano por cada paso.
    /// Las diapositivas se generan en Docs/Instrucciones y se cargan con Tools → Instrucciones → Construir panel.
    /// </summary>
    public class InstructionsPanel : MonoBehaviour {
        [Header("Diapositivas")]
        [Tooltip("En orden. Imágenes 1080 x 1920 (9:16)")]
        [SerializeField] private List<Sprite> slides = new List<Sprite>();
        [Tooltip("Image donde se muestra la diapositiva actual")]
        [SerializeField] private Image slideImage;
        [Tooltip("Diapositiva con la que empieza (0 = la primera)")]
        [SerializeField] private int startIndex = 0;

        [Header("Navegación")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("Texto del botón Siguiente: en la última diapositiva cambia a 'Inicio' y regresa a la primera")]
        [SerializeField] private TMP_Text nextButtonLabel;
        [SerializeField] private string nextText = "Siguiente";
        [SerializeField] private string restartText = "Inicio";
        [Tooltip("Contador de página, p. ej. '3 / 19'")]
        [SerializeField] private TMP_Text pageLabel;

        [Header("Transición")]
        [Tooltip("Segundos del fundido entre diapositivas. 0 = cambio instantáneo")]
        [SerializeField] private float fadeTime = 0.12f;

        public int CurrentIndex => m_Index;
        public int Count => slides.Count;

        private int m_Index;
        private Coroutine m_Fade;

        #region Unity Methods
        private void Awake() {
            if (previousButton != null) previousButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(NextOrRestart);
        }

        private void OnEnable() {
            m_Index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, slides.Count - 1));
            Show(m_Index, false);
        }

        private void OnDestroy() {
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(NextOrRestart);
        }
        #endregion

        #region Navegación
        /// <summary>Avanza una diapositiva. En la última no hace nada (ver NextOrRestart).</summary>
        public void Next() {
            if (m_Index < slides.Count - 1) Show(m_Index + 1, true);
        }

        public void Previous() {
            if (m_Index > 0) Show(m_Index - 1, true);
        }

        /// <summary>Lo que hace el botón Siguiente: avanza, y en la última diapositiva regresa a la primera.</summary>
        public void NextOrRestart() {
            Show(m_Index < slides.Count - 1 ? m_Index + 1 : 0, true);
        }

        /// <summary>Salta a una diapositiva (índice desde 0). Útil para abrir el panel en un tema concreto.</summary>
        public void GoTo(int index) {
            if (slides.Count == 0) return;
            Show(Mathf.Clamp(index, 0, slides.Count - 1), true);
        }

        public void First() {
            GoTo(0);
        }
        #endregion

        private void Show(int index, bool animate) {
            m_Index = index;
            UpdateControls();

            if (slideImage == null || slides.Count == 0) return;

            if (m_Fade != null) StopCoroutine(m_Fade);
            if (animate && fadeTime > 0f && isActiveAndEnabled)
                m_Fade = StartCoroutine(FadeTo(slides[index]));
            else
                SetSlide(slides[index], 1f);
        }

        private void UpdateControls() {
            bool isLast = m_Index >= slides.Count - 1;

            if (previousButton != null) previousButton.interactable = m_Index > 0;
            if (nextButton != null) nextButton.interactable = slides.Count > 1;
            if (nextButtonLabel != null) nextButtonLabel.text = isLast ? restartText : nextText;
            if (pageLabel != null) pageLabel.text = slides.Count > 0 ? $"{m_Index + 1} / {slides.Count}" : "";
        }

        /// <summary>Baja la diapositiva a negro, la cambia y la vuelve a subir.</summary>
        private IEnumerator FadeTo(Sprite sprite) {
            float half = fadeTime * 0.5f;

            for (float t = 0f; t < half; t += Time.unscaledDeltaTime) {
                SetSlide(slideImage.sprite, 1f - t / half);
                yield return null;
            }

            for (float t = 0f; t < half; t += Time.unscaledDeltaTime) {
                SetSlide(sprite, t / half);
                yield return null;
            }

            SetSlide(sprite, 1f);
            m_Fade = null;
        }

        private void SetSlide(Sprite sprite, float brightness) {
            slideImage.sprite = sprite;
            slideImage.color = new Color(brightness, brightness, brightness, 1f);
        }
    }
}
