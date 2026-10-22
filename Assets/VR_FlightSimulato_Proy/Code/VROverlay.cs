using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Charly.FlightController {
    /// <summary>
    /// Utilidades para dibujar UI pegada a la cámara del visor y ENCIMA de todo (la cabina no la tapa).
    /// La usan MapBoundary y CrashHandler para el fade y los avisos.
    /// </summary>
    public static class VROverlay {
        // Material de UI que se dibuja encima de todo (ignora profundidad)
        public const int OverlayQueue = 4000;
        private static readonly int GuiZTestMode = Shader.PropertyToID("unity_GUIZTestMode");

        /// <summary>Canvas World Space hijo de la cámara, sin raycaster (las manos XR no lo pueden tocar).</summary>
        public static GameObject CreateCameraCanvas(Camera camera, string name, float distance, float width, float height, float scale, int sortingOrder) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.layer = camera.gameObject.layer;
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = sortingOrder;
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
            return go;
        }

        public static Material CreateOverlayMaterial(Material source) {
            var mat = new Material(source);
            mat.SetInt(GuiZTestMode, (int)CompareFunction.Always);
            mat.renderQueue = OverlayQueue;
            return mat;
        }

        /// <summary>
        /// Canvas de texto frente a la vista (1 unidad del canvas = 1 mm) con un TextMeshPro que lo llena.
        /// Devuelve null si TextMeshPro no tiene fuente por defecto.
        /// </summary>
        public static TextMeshProUGUI CreateText(Camera camera, string name, Vector3 localPosition, Vector2 sizeMm, float fontSize, int sortingOrder, Object context) {
            var root = CreateCameraCanvas(camera, name, localPosition.z, sizeMm.x, sizeMm.y, 0.001f, sortingOrder);
            root.transform.localPosition = localPosition;

            var textGo = new GameObject("Texto", typeof(RectTransform));
            textGo.layer = root.layer;
            textGo.transform.SetParent(root.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            if (tmp.font == null) {
                Debug.LogWarning($"{name}: TextMeshPro no tiene fuente por defecto; el texto no se verá.", context);
                Object.Destroy(root);
                return null;
            }
            tmp.fontMaterial = CreateOverlayMaterial(tmp.fontMaterial);
            root.SetActive(false);
            return tmp;
        }
    }

    /// <summary>Imagen de color que cubre toda la vista del visor, para hacer fade a negro (y bajar el audio).</summary>
    public class OverlayFade {
        private readonly GameObject m_Root;
        private readonly Image m_Image;
        private readonly Color m_Color;

        public float Alpha { get; private set; }

        private OverlayFade(GameObject root, Image image, Color color) {
            m_Root = root;
            m_Image = image;
            m_Color = color;
            SetAlpha(0f);
        }

        /// <summary>Devuelve null si no hay cámara.</summary>
        public static OverlayFade Create(Camera camera, string name, Color color, int sortingOrder) {
            if (camera == null) return null;

            // Canvas en el mundo pegado a la cámara: funciona en ambos ojos del visor
            float distance = Mathf.Max(camera.nearClipPlane * 1.5f, 0.05f);
            var root = VROverlay.CreateCameraCanvas(camera, name, distance, 100f, 100f, distance * 12f / 100f, sortingOrder);

            var imageGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            imageGo.layer = root.layer;
            imageGo.transform.SetParent(root.transform, false);
            var rt = (RectTransform)imageGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var image = imageGo.GetComponent<Image>();
            image.raycastTarget = false;
            image.material = VROverlay.CreateOverlayMaterial(Canvas.GetDefaultCanvasMaterial());
            return new OverlayFade(root, image, color);
        }

        public void SetAlpha(float alpha) {
            Alpha = alpha;
            if (m_Image == null) return;
            Color c = m_Color;
            c.a = alpha;
            m_Image.color = c;
            m_Root.SetActive(alpha > 0.001f);
        }

        /// <summary>Anima el fade y baja el volumen general: 'audioDuck' 0 = nada, 1 = silencio con negro total.</summary>
        public IEnumerator Fade(float from, float to, float duration, float listenerVolume, float audioDuck) {
            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                SetAlpha(a);
                AudioListener.volume = listenerVolume * (1f - audioDuck * a);
                yield return null;
            }
            SetAlpha(to);
            AudioListener.volume = listenerVolume * (1f - audioDuck * to);
        }
    }
}
