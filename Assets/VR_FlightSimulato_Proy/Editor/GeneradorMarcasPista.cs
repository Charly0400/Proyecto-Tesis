// Generador de marcas de pista (estilo OACI) para pistas hechas con un cubo escalado.
// Crea UNA sola malla con todas las marcas (umbral, números, eje, bordes, zona de toma y
// punto de visada) como objeto hermano de la pista, unos centímetros por encima de ella.
// El eje largo de la pista se detecta solo (X o Z local).
// Menú: Tools > Marcas de pista
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorMarcasPista : EditorWindow
{
    const string PrefijoNombre = "Marcas_";
    const string RutaMaterial = "Assets/VR_FlightSimulato_Proy/Materials/Pista/MarcasPista.mat";

    // ---------- Parámetros ----------
    Transform pista;
    Material material;                    // opcional: si no hay, se crea uno blanco URP/Lit
    string numeroA = "", numeroB = "";    // A = cabecera en el extremo negativo del eje largo
    float alturaSobrePista = 0.05f;       // m; evita z-fighting con el asfalto
    bool bordes = true, umbral = true, numeros = true, eje = true, zonaToma = true, puntoVisada = true;

    // ---------- Medidas OACI (m) ----------
    const float InicioUmbral = 6f;        // las franjas empiezan a 6 m del extremo
    const float LargoUmbral = 30f;
    const float AltoNumero = 9f;
    const float SeparacionTrasUmbral = 12f;
    const float LineaEje = 30f, HuecoEje = 20f;
    const float LargoZonaToma = 22.5f, AnchoFranjaToma = 1.8f, SeparacionFranjaToma = 1.5f;

    [MenuItem("Tools/Marcas de pista")]
    static void Abrir() => GetWindow<GeneradorMarcasPista>("Marcas de pista");

    void OnEnable()
    {
        if (pista == null && Selection.activeTransform != null) pista = Selection.activeTransform;
        if (pista != null && numeroA == "") CalcularNumeros();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Selecciona la pista (cubo escalado). Las marcas se generan como un objeto hermano llamado 'Marcas_<pista>'. Volver a generar reemplaza las anteriores.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        pista = (Transform)EditorGUILayout.ObjectField("Pista", pista, typeof(Transform), true);
        if (EditorGUI.EndChangeCheck() && pista != null) CalcularNumeros();

        if (pista != null && Medir(pista, out _, out _, out float ancho, out float largo))
            EditorGUILayout.LabelField("Medida detectada", $"{ancho:0.#} m de ancho × {largo:0.#} m de largo");
        else if (pista != null)
            EditorGUILayout.HelpBox("El objeto no tiene MeshFilter; no puedo medirlo.", MessageType.Warning);

        material = (Material)EditorGUILayout.ObjectField("Material (opcional)", material, typeof(Material), false);
        alturaSobrePista = EditorGUILayout.FloatField("Altura sobre la pista (m)", alturaSobrePista);

        EditorGUILayout.LabelField("Números de cabecera", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            numeroA = EditorGUILayout.TextField("Cabecera A / B", numeroA);
            numeroB = EditorGUILayout.TextField(numeroB);
        }
        if (GUILayout.Button("Calcular por rumbo (asume +Z = norte)")) CalcularNumeros();

        EditorGUILayout.LabelField("Marcas", EditorStyles.boldLabel);
        bordes = EditorGUILayout.Toggle("Franjas de borde", bordes);
        umbral = EditorGUILayout.Toggle("Umbral", umbral);
        numeros = EditorGUILayout.Toggle("Números", numeros);
        eje = EditorGUILayout.Toggle("Eje central", eje);
        zonaToma = EditorGUILayout.Toggle("Zona de toma de contacto", zonaToma);
        puntoVisada = EditorGUILayout.Toggle("Punto de visada", puntoVisada);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(pista == null))
        {
            if (GUILayout.Button("Generar marcas", GUILayout.Height(32))) Generar();
            if (GUILayout.Button("Borrar marcas")) Borrar();
        }
    }

    // ---------- Medición ----------
    // Devuelve el centro de la cara superior y un marco donde X = ancho y Z = largo de la pista.
    internal static bool Medir(Transform t, out Vector3 centroArriba, out Quaternion marco, out float ancho, out float largo)
    {
        centroArriba = default; marco = default; ancho = largo = 0f;
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        var b = mf.sharedMesh.bounds;
        var tam = Vector3.Scale(b.size, t.lossyScale);
        centroArriba = t.TransformPoint(b.center + Vector3.up * b.extents.y);
        marco = t.rotation;
        ancho = tam.x; largo = tam.z;
        if (tam.x > tam.z)
        {
            marco *= Quaternion.Euler(0f, 90f, 0f);
            ancho = tam.z; largo = tam.x;
        }
        return true;
    }

    void CalcularNumeros()
    {
        if (pista == null || !Medir(pista, out _, out var marco, out _, out _)) return;
        var dir = marco * Vector3.forward;
        float rumbo = Mathf.Repeat(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 360f);
        int a = Mathf.RoundToInt(rumbo / 10f) % 36;
        if (a == 0) a = 36;
        int b = (a + 18) % 36;
        if (b == 0) b = 36;
        numeroA = a.ToString("00"); numeroB = b.ToString("00");
    }

    // ---------- Generación ----------
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<int> tris = new List<int>();
    float L, W;

    void Generar()
    {
        if (!Medir(pista, out var centro, out var marco, out W, out L))
        { EditorUtility.DisplayDialog("Marcas de pista", "La pista necesita un MeshFilter.", "OK"); return; }
        if (L < 300f || W < 10f)
        { EditorUtility.DisplayDialog("Marcas de pista", $"La pista mide {W:0.#} × {L:0.#} m; es demasiado pequeña para las marcas.", "OK"); return; }

        verts.Clear(); tris.Clear();
        if (bordes) Bordes();
        for (int lado = 0; lado < 2; lado++)
        {
            if (umbral) Umbral(lado);
            if (numeros) Numero(lado, lado == 0 ? numeroA : numeroB);
            if (puntoVisada || zonaToma) ZonaToma(lado);
        }
        if (eje) Eje();

        int grupoUndo = Undo.GetCurrentGroup();
        Borrar();
        var mesh = new Mesh { name = PrefijoNombre + pista.name };
        if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        var normales = new Vector3[verts.Count];
        var uvs = new Vector2[verts.Count];
        for (int i = 0; i < verts.Count; i++) { normales[i] = Vector3.up; uvs[i] = new Vector2(verts[i].x, verts[i].z); }
        mesh.normals = normales;
        mesh.uv = uvs;
        mesh.RecalculateBounds();

        var go = new GameObject(PrefijoNombre + pista.name);
        Undo.RegisterCreatedObjectUndo(go, "Generar marcas de pista");
        go.transform.SetParent(pista.parent, false);
        go.transform.SetPositionAndRotation(centro + marco * Vector3.up * alturaSobrePista, marco);
        if (pista.parent != null)
        {
            var ps = pista.parent.lossyScale;
            go.transform.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
        }
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material != null ? material : MaterialPorDefecto();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        // Mismos flags que la pista, sin GI: la malla no tiene UV2 de lightmap.
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(pista.gameObject) & ~StaticEditorFlags.ContributeGI);

        Undo.CollapseUndoOperations(grupoUndo);  // un solo Ctrl+Z deshace borrar + generar
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log($"[Marcas] {tris.Count / 6} marcas generadas sobre '{pista.name}' ({W:0.#} × {L:0.#} m).");
    }

    // Cuadro en el marco de la pista: x = lateral, z = longitudinal (0 = centro de la pista).
    void Quad(float x, float z, float ancho, float largo)
    {
        float x0 = x - ancho * 0.5f, x1 = x + ancho * 0.5f, z0 = z - largo * 0.5f, z1 = z + largo * 0.5f;
        int i = verts.Count;
        verts.Add(new Vector3(x0, 0f, z0)); verts.Add(new Vector3(x0, 0f, z1));
        verts.Add(new Vector3(x1, 0f, z1)); verts.Add(new Vector3(x1, 0f, z0));
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }

    // Cuadro visto por el piloto que aterriza en 'lado': a = desplazamiento a su derecha,
    // d = distancia desde el extremo de la pista hasta el borde cercano del cuadro.
    void QuadPiloto(int lado, float a, float d, float ancho, float largo)
    {
        if (lado == 0) Quad(a, -L * 0.5f + d + largo * 0.5f, ancho, largo);
        else Quad(-a, L * 0.5f - d - largo * 0.5f, ancho, largo);
    }

    float AnchoLinea => W >= 30f ? 0.9f : 0.45f;

    void Bordes()
    {
        float largo = L - 2f * InicioUmbral;
        float x = W * 0.5f - AnchoLinea * 0.5f;
        Quad(-x, 0f, AnchoLinea, largo);
        Quad(x, 0f, AnchoLinea, largo);
    }

    void Umbral(int lado)
    {
        int n = W >= 60f ? 16 : W >= 45f ? 12 : W >= 30f ? 8 : W >= 23f ? 6 : 4;
        // Franjas y espacios del mismo ancho, hueco central doble, hasta 3 m del borde.
        float s = Mathf.Min(1.8f, (W * 0.5f - 3f) / n);
        for (int k = 0; k < n / 2; k++)
        {
            float a = s + 2f * k * s + s * 0.5f;
            QuadPiloto(lado, -a, InicioUmbral, s, LargoUmbral);
            QuadPiloto(lado, a, InicioUmbral, s, LargoUmbral);
        }
    }

    float InicioNumero => InicioUmbral + LargoUmbral + SeparacionTrasUmbral;
    float InicioEje => numeros ? InicioNumero + AltoNumero + SeparacionTrasUmbral : InicioNumero;

    void Eje()
    {
        float tramo = L - 2f * InicioEje;
        int n = Mathf.FloorToInt((tramo + HuecoEje) / (LineaEje + HuecoEje));
        if (n <= 0) return;
        float ocupado = n * (LineaEje + HuecoEje) - HuecoEje;
        float z = -L * 0.5f + InicioEje + (tramo - ocupado) * 0.5f + LineaEje * 0.5f;
        for (int i = 0; i < n; i++, z += LineaEje + HuecoEje) Quad(0f, z, AnchoLinea, LineaEje);
    }

    // ---------- Números (7 segmentos) ----------
    // Segmentos: a arriba, b arriba-der, c abajo-der, d abajo, e abajo-izq, f arriba-izq, g medio.
    static readonly string[] Segmentos = { "abcdef", "bc", "abged", "abgcd", "fgbc", "afgcd", "afgedc", "abc", "abcdefg", "abcdfg" };

    void Numero(int lado, string texto)
    {
        var digitos = new List<int>();
        foreach (char c in texto) if (char.IsDigit(c)) digitos.Add(c - '0');
        if (digitos.Count == 0) return;
        if (digitos.Count == 1) digitos.Insert(0, 0);

        float escala = Mathf.Min(1f, W / 30f);
        float h = AltoNumero * escala, wd = 3.6f * escala, t = 0.9f * escala, sep = 2.4f * escala;
        float total = digitos.Count * wd + (digitos.Count - 1) * sep;
        for (int i = 0; i < digitos.Count; i++)
        {
            float izq = -total * 0.5f + i * (wd + sep);
            if (digitos[i] == 1) { Segmento(lado, izq, wd * 0.5f - t * 0.5f, wd * 0.5f + t * 0.5f, 0f, h); continue; }
            foreach (char s in Segmentos[digitos[i]])
            {
                switch (s)
                {
                    case 'a': Segmento(lado, izq, 0f, wd, h - t, h); break;
                    case 'b': Segmento(lado, izq, wd - t, wd, h * 0.5f, h); break;
                    case 'c': Segmento(lado, izq, wd - t, wd, 0f, h * 0.5f); break;
                    case 'd': Segmento(lado, izq, 0f, wd, 0f, t); break;
                    case 'e': Segmento(lado, izq, 0f, t, 0f, h * 0.5f); break;
                    case 'f': Segmento(lado, izq, 0f, t, h * 0.5f, h); break;
                    case 'g': Segmento(lado, izq, 0f, wd, h * 0.5f - t * 0.5f, h * 0.5f + t * 0.5f); break;
                }
            }
        }
    }

    // u = horizontal dentro del dígito, v = vertical (v crece alejándose del umbral, así el piloto lo lee derecho).
    void Segmento(int lado, float izq, float u0, float u1, float v0, float v1) =>
        QuadPiloto(lado, izq + (u0 + u1) * 0.5f, InicioNumero + v0, u1 - u0, v1 - v0);

    // ---------- Punto de visada y zona de toma ----------
    void ZonaToma(int lado)
    {
        // Tabla OACI según la longitud: distancia desde el umbral, largo, ancho y separación interior.
        float dist, largo, ancho, hueco;
        if (L < 800f) { dist = 150f; largo = 30f; ancho = 4f; hueco = 6f; }
        else if (L < 1200f) { dist = 250f; largo = 45f; ancho = 6f; hueco = 9f; }
        else if (L < 2400f) { dist = 300f; largo = 45f; ancho = 6f; hueco = 18f; }
        else { dist = 400f; largo = 60f; ancho = 10f; hueco = 18f; }
        hueco = Mathf.Min(hueco, W * 0.4f);
        ancho = Mathf.Min(ancho, W * 0.5f - hueco * 0.5f - 1.5f);

        if (puntoVisada && ancho > 1f)
        {
            float a = hueco * 0.5f + ancho * 0.5f;
            QuadPiloto(lado, -a, InicioUmbral + dist, ancho, largo);
            QuadPiloto(lado, a, InicioUmbral + dist, ancho, largo);
        }
        if (!zonaToma) return;

        int pares = L < 900f ? 1 : L < 1200f ? 2 : L < 1500f ? 3 : L < 2400f ? 4 : 6;
        float[] posiciones = { 150f, 450f, 600f, 750f, 900f, 1050f };
        int[] franjas = { 3, 2, 2, 1, 1, 1 };
        for (int p = 0, hechos = 0; p < posiciones.Length && hechos < pares; p++)
        {
            float d = InicioUmbral + posiciones[p];
            bool chocaVisada = puntoVisada && d < InicioUmbral + dist + largo && d + LargoZonaToma > InicioUmbral + dist;
            if (chocaVisada) continue;
            if (d + LargoZonaToma > L * 0.5f - 30f) break;  // no invadir la mitad de la otra cabecera
            hechos++;
            for (int k = 0; k < franjas[p]; k++)
            {
                float a = hueco * 0.5f + AnchoFranjaToma * 0.5f + k * (AnchoFranjaToma + SeparacionFranjaToma);
                if (a + AnchoFranjaToma * 0.5f > W * 0.5f - 1.5f) break;
                QuadPiloto(lado, -a, d, AnchoFranjaToma, LargoZonaToma);
                QuadPiloto(lado, a, d, AnchoFranjaToma, LargoZonaToma);
            }
        }
    }

    // ---------- Utilidades ----------
    static Material MaterialPorDefecto()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogWarning("[Marcas] No encontré URP/Lit; usa tu propio material."); return null; }
        m = new Material(shader) { name = "MarcasPista" };
        m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.9f));
        m.SetFloat("_Smoothness", 0.15f);
        CrearCarpeta(System.IO.Path.GetDirectoryName(RutaMaterial).Replace('\\', '/'));
        AssetDatabase.CreateAsset(m, RutaMaterial);
        AssetDatabase.SaveAssets();
        return m;
    }

    internal static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        var partes = ruta.Split('/');
        string actual = partes[0];
        for (int i = 1; i < partes.Length; i++)
        {
            string sig = actual + "/" + partes[i];
            if (!AssetDatabase.IsValidFolder(sig)) AssetDatabase.CreateFolder(actual, partes[i]);
            actual = sig;
        }
    }

    void Borrar()
    {
        if (pista == null) return;
        var padre = pista.parent;
        string nombre = PrefijoNombre + pista.name;
        var aBorrar = new List<GameObject>();
        if (padre != null)
        {
            foreach (Transform hijo in padre) if (hijo.name == nombre) aBorrar.Add(hijo.gameObject);
        }
        else
        {
            foreach (var raiz in pista.gameObject.scene.GetRootGameObjects()) if (raiz.name == nombre) aBorrar.Add(raiz);
        }
        foreach (var go in aBorrar) Undo.DestroyObjectImmediate(go);
        if (aBorrar.Count > 0) EditorSceneManager.MarkSceneDirty(pista.gameObject.scene);
    }
}
#endif
