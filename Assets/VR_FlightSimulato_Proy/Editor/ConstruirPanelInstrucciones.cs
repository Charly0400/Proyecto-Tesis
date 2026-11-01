using System.Linq;
using Charly.FlightController;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Arma el panel de instrucciones dentro del prefab "Spatial Panel Manipulator Model" (el que usa MainScene):
/// borra las tarjetas viejas del StepManager, deja el lienzo en formato teléfono (9:16) con la diapositiva arriba
/// y los botones Anterior / Siguiente + contador abajo, y le pone InstructionsPanel con las imágenes de
/// Sprites/Instrucciones. Se puede volver a correr cuando se regeneren las diapositivas (exportar.ps1).
/// </summary>
public static class ConstruirPanelInstrucciones {
    private const string RutaPrefab = "Assets/VR_FlightSimulato_Proy/Prefabs/Prefabs map/Spatial Panel Manipulator Model.prefab";
    private const string CarpetaDiapositivas = "Assets/VR_FlightSimulato_Proy/Sprites/Instrucciones";

    // Medidas del lienzo en unidades de UI (el lienzo está escalado x0.01 y el prefab x0.2: 100 u = 0.2 m)
    private const float Ancho = 320f;
    private const float Margen = 12f;
    private const float AltoBotones = 60f;
    private const float AnchoBoton = 110f;
    private const float Separacion = 10f;

    private static readonly string[] HijosQueSeQuedan = { "Background", "PanelOutline" };

    [MenuItem("Tools/Instrucciones/Construir panel")]
    public static void Construir() {
        Sprite[] diapositivas = ConfigurarDiapositivas();
        if (diapositivas.Length == 0) {
            Debug.LogError($"No hay imágenes en {CarpetaDiapositivas}. Corre Docs/Instrucciones/exportar.ps1 primero.");
            return;
        }

        GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
        try {
            var tarjeta = raiz.transform.Find("CoachingCardRoot") as RectTransform;
            if (tarjeta == null) {
                Debug.LogError("El prefab no tiene 'CoachingCardRoot'.");
                return;
            }

            ArmarPanel(tarjeta, diapositivas);
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            Debug.Log($"Panel de instrucciones listo con {diapositivas.Length} diapositivas ({RutaPrefab}).");
        }
        finally {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    /// <summary>Importa las imágenes como Sprite con mipmaps (en VR se ven de lejos y sin mipmaps titilan).</summary>
    private static Sprite[] ConfigurarDiapositivas() {
        string[] rutas = AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaDiapositivas })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(r => r)
            .ToArray();

        foreach (string ruta in rutas) {
            var importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
            bool listo = importador.textureType == TextureImporterType.Sprite
                         && importador.spriteImportMode == SpriteImportMode.Single
                         && importador.mipmapEnabled
                         && importador.filterMode == FilterMode.Trilinear
                         && importador.maxTextureSize == 2048;
            if (listo) continue;

            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.mipmapEnabled = true;
            importador.filterMode = FilterMode.Trilinear;
            importador.anisoLevel = 2;
            importador.maxTextureSize = 2048;
            importador.textureCompression = TextureImporterCompression.Compressed;
            importador.SaveAndReimport();
        }

        return rutas.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
    }

    private static void ArmarPanel(RectTransform tarjeta, Sprite[] diapositivas) {
        // El botón del StepManager se reutiliza como "Siguiente" (ya trae el poke, el sonido y los colores)
        Transform botonBase = tarjeta.Find("Text Poke Button Special");
        if (botonBase == null) botonBase = tarjeta.Find("Boton Siguiente");
        if (botonBase == null) {
            Debug.LogError("No se encontró el botón del panel ('Text Poke Button Special').");
            return;
        }

        // Fuera las tarjetas viejas (y lo que haya dejado una corrida anterior de esta herramienta)
        for (int i = tarjeta.childCount - 1; i >= 0; i--) {
            Transform hijo = tarjeta.GetChild(i);
            if (hijo != botonBase && !HijosQueSeQuedan.Contains(hijo.name))
                Object.DestroyImmediate(hijo.gameObject);
        }

        var pasos = tarjeta.GetComponent("StepManager");
        if (pasos != null) Object.DestroyImmediate(pasos);

        // Lienzo 9:16 + barra de botones. Se conserva el borde de abajo para que siga apoyado en el asa
        float altoDiapositiva = Mathf.Round((Ancho - 2f * Margen) * 16f / 9f);
        float alto = Margen + altoDiapositiva + Separacion + AltoBotones + Margen;
        float bordeInferior = tarjeta.anchoredPosition.y - tarjeta.sizeDelta.y * tarjeta.localScale.y * 0.5f;
        tarjeta.sizeDelta = new Vector2(Ancho, alto);
        tarjeta.anchoredPosition = new Vector2(tarjeta.anchoredPosition.x, bordeInferior + alto * tarjeta.localScale.y * 0.5f);

        var fondo = tarjeta.Find("Background") as RectTransform;
        if (fondo != null) Estirar(fondo);
        var contorno = tarjeta.Find("PanelOutline");
        if (contorno != null) contorno.SetAsLastSibling();

        // Diapositiva con esquinas redondeadas (máscara con el mismo sprite del fondo)
        var marco = NuevoHijo("Marco Diapositiva", tarjeta);
        Anclar(marco, new Vector2(0.5f, 1f), new Vector2(0f, -Margen), new Vector2(Ancho - 2f * Margen, altoDiapositiva));
        var imagenMarco = marco.gameObject.AddComponent<Image>();
        Image imagenFondo = fondo != null ? fondo.GetComponent<Image>() : null;
        if (imagenFondo != null) {
            imagenMarco.sprite = imagenFondo.sprite;
            imagenMarco.type = Image.Type.Sliced;
            imagenMarco.pixelsPerUnitMultiplier = imagenFondo.pixelsPerUnitMultiplier;
        }
        imagenMarco.raycastTarget = false;
        marco.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        marco.SetSiblingIndex(fondo != null ? fondo.GetSiblingIndex() + 1 : 0);

        var diapositiva = NuevoHijo("Diapositiva", marco);
        Estirar(diapositiva);
        var imagenDiapositiva = diapositiva.gameObject.AddComponent<Image>();
        imagenDiapositiva.sprite = diapositivas[0];
        imagenDiapositiva.preserveAspect = true;
        imagenDiapositiva.raycastTarget = false;

        // Botones
        var siguiente = (RectTransform)botonBase;
        siguiente.name = "Boton Siguiente";
        ArmarBoton(siguiente, new Vector2(1f, 0f), new Vector2(-Margen, Margen), "Siguiente");

        var anterior = (RectTransform)Object.Instantiate(siguiente.gameObject, tarjeta).transform;
        anterior.name = "Boton Anterior";
        ArmarBoton(anterior, new Vector2(0f, 0f), new Vector2(Margen, Margen), "Anterior");

        // Contador de página entre los botones
        TMP_Text textoBoton = siguiente.GetComponentInChildren<TMP_Text>(true);
        var pagina = NuevoHijo("Pagina", tarjeta);
        Anclar(pagina, new Vector2(0.5f, 0f), new Vector2(0f, Margen), new Vector2(Ancho - 2f * Margen - 2f * AnchoBoton, AltoBotones));
        var textoPagina = pagina.gameObject.AddComponent<TextMeshProUGUI>();
        if (textoBoton != null) textoPagina.font = textoBoton.font;
        textoPagina.text = $"1 / {diapositivas.Length}";
        textoPagina.fontSize = 22f;
        textoPagina.color = new Color(0.62f, 0.69f, 0.78f);
        textoPagina.alignment = TextAlignmentOptions.Center;
        textoPagina.raycastTarget = false;

        if (contorno != null) contorno.SetAsLastSibling();

        // Componente que maneja todo
        var panel = tarjeta.GetComponent<InstructionsPanel>();
        if (panel == null) panel = tarjeta.gameObject.AddComponent<InstructionsPanel>();
        var so = new SerializedObject(panel);
        var lista = so.FindProperty("slides");
        lista.arraySize = diapositivas.Length;
        for (int i = 0; i < diapositivas.Length; i++)
            lista.GetArrayElementAtIndex(i).objectReferenceValue = diapositivas[i];
        so.FindProperty("slideImage").objectReferenceValue = imagenDiapositiva;
        so.FindProperty("previousButton").objectReferenceValue = anterior.GetComponent<Button>();
        so.FindProperty("nextButton").objectReferenceValue = siguiente.GetComponent<Button>();
        so.FindProperty("nextButtonLabel").objectReferenceValue = textoBoton;
        so.FindProperty("pageLabel").objectReferenceValue = textoPagina;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Posición, tamaño y texto del botón. Quita las llamadas del Inspector: InstructionsPanel conecta el clic por código.</summary>
    private static void ArmarBoton(RectTransform boton, Vector2 esquina, Vector2 posicion, string texto) {
        Anclar(boton, esquina, posicion, new Vector2(AnchoBoton, AltoBotones));

        var clic = boton.GetComponent<Button>().onClick;
        for (int i = clic.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(clic, i);

        var etiqueta = boton.GetComponentInChildren<TMP_Text>(true);
        if (etiqueta != null) {
            etiqueta.text = texto;
            etiqueta.enableAutoSizing = true;
            etiqueta.fontSizeMin = 12f;
            etiqueta.fontSizeMax = 20f;
            etiqueta.margin = new Vector4(10f, 0f, 10f, 0f);
        }

        // El relleno del poke crece hasta el tamaño del botón
        var poke = boton.GetComponent("XRPokeFollowAffordanceFill");
        if (poke != null) {
            var so = new SerializedObject(poke);
            so.FindProperty("m_PokeFillMaxSizeX").floatValue = AnchoBoton;
            so.FindProperty("m_PokeFillMaxSizeY").floatValue = AltoBotones;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static RectTransform NuevoHijo(string nombre, Transform padre) {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = padre.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        return rt;
    }

    /// <summary>Ancla y pivote en la misma esquina/borde, para colocar con un desplazamiento desde ahí.</summary>
    private static void Anclar(RectTransform rt, Vector2 ancla, Vector2 posicion, Vector2 tamano) {
        rt.anchorMin = rt.anchorMax = rt.pivot = ancla;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }

    private static void Estirar(RectTransform rt) {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
