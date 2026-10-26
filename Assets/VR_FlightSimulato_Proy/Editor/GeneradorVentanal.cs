// Ventanal: convierte una pared (p. ej. la franja sobre el portón del hangar) en vidrio con marcos.
// Mide la pared desde su malla, crea los marcos (perímetro, montantes y travesaños) y un panel de vidrio,
// y apaga SOLO el render de la pared original: su collider se queda para que no se pueda atravesar en VR.
// El vidrio puede ser transparente u opaco reflejante, y se puede cambiar sin regenerar.
// Menú: Tools > Ventanal
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorVentanal : EditorWindow
{
    const string NombreVentanal = "Ventanal";
    const string CarpetaMateriales = "Assets/VR_FlightSimulato_Proy/Materials/Hangar";

    internal enum TipoVidrio { Transparente, Opaco }

    // ---------- Parámetros ----------
    [SerializeField] List<Transform> paredes = new List<Transform>();
    TipoVidrio tipoVidrio = TipoVidrio.Transparente;
    float separacionMontantes = 2.5f;     // m entre montantes verticales
    int filas = 2;                        // filas de vidrios (filas - 1 travesaños)
    float anchoPerimetro = 0.3f;          // m del marco exterior
    float anchoMontante = 0.12f;          // m de montantes y travesaños
    float profundidadMontante = 0.3f;     // m; el perímetro usa todo el grosor de la pared para cerrarla

    [MenuItem("Tools/Ventanal")]
    static void Abrir() => GetWindow<GeneradorVentanal>("Ventanal");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Selecciona la pared (o una por hangar) y pulsa 'Usar selección'. La pared deja de dibujarse pero conserva su collider. 'Quitar ventanal' la regresa como estaba.", MessageType.Info);
        Lista("Paredes", paredes);

        EditorGUILayout.LabelField("Vidrio", EditorStyles.boldLabel);
        tipoVidrio = (TipoVidrio)EditorGUILayout.EnumPopup("Tipo de vidrio", tipoVidrio);
        using (new EditorGUI.DisabledScope(paredes.Count == 0))
            if (GUILayout.Button("Cambiar vidrio (sin regenerar)")) CambiarVidrio();

        EditorGUILayout.LabelField("Marcos", EditorStyles.boldLabel);
        separacionMontantes = EditorGUILayout.FloatField("Separación de montantes (m)", separacionMontantes);
        filas = EditorGUILayout.IntSlider("Filas de vidrios", filas, 1, 4);
        anchoPerimetro = EditorGUILayout.FloatField("Ancho del marco exterior (m)", anchoPerimetro);
        anchoMontante = EditorGUILayout.FloatField("Ancho de montantes (m)", anchoMontante);
        profundidadMontante = EditorGUILayout.FloatField("Profundidad de montantes (m)", profundidadMontante);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(paredes.Count == 0))
        {
            if (GUILayout.Button("Generar ventanal", GUILayout.Height(32))) Generar();
            if (GUILayout.Button("Quitar ventanal")) foreach (var p in paredes) if (p != null) Quitar(p);
        }
    }

    static void Lista(string titulo, List<Transform> lista)
    {
        EditorGUILayout.LabelField($"{titulo} ({lista.Count})", EditorStyles.boldLabel);
        for (int i = 0; i < lista.Count; i++)
            lista[i] = (Transform)EditorGUILayout.ObjectField(lista[i], typeof(Transform), true);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Usar selección"))
            {
                lista.Clear();
                foreach (var go in Selection.gameObjects) lista.Add(go.transform);
            }
            if (GUILayout.Button("Limpiar")) lista.Clear();
        }
    }

    // ---------- Generación ----------
    void Generar()
    {
        int grupoUndo = Undo.GetCurrentGroup();
        var mMarco = MaterialMarco();
        var mVidrio = MaterialVidrio(tipoVidrio);
        foreach (var p in paredes)
        {
            if (p == null) continue;
            var mf = p.GetComponent<MeshFilter>();
            var mr = p.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null || mr == null) { Debug.LogWarning($"[Ventanal] '{p.name}' no tiene malla; lo salto."); continue; }
            Quitar(p);
            Crear(p, mf.sharedMesh.bounds, mMarco, mVidrio);
            Undo.RecordObject(mr, "Ocultar pared");
            mr.enabled = false;
        }
        Undo.CollapseUndoOperations(grupoUndo);
        if (paredes.Count > 0 && paredes[0] != null) EditorSceneManager.MarkSceneDirty(paredes[0].gameObject.scene);
    }

    void Crear(Transform pared, Bounds b, Material mMarco, Material mVidrio)
    {
        // Ejes locales de la pared: V = el que más apunta hacia arriba, N = el más delgado de los otros dos, H = el restante.
        var ls = pared.lossyScale;
        var ejes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        var tam = new[] { b.size.x * Mathf.Abs(ls.x), b.size.y * Mathf.Abs(ls.y), b.size.z * Mathf.Abs(ls.z) };
        int iV = 0;
        for (int i = 1; i < 3; i++)
            if (Mathf.Abs(Vector3.Dot(pared.TransformDirection(ejes[i]).normalized, Vector3.up)) >
                Mathf.Abs(Vector3.Dot(pared.TransformDirection(ejes[iV]).normalized, Vector3.up))) iV = i;
        int a = (iV + 1) % 3, c = (iV + 2) % 3;
        int iN = tam[a] <= tam[c] ? a : c, iH = iN == a ? c : a;
        Vector3 H = ejes[iH], V = ejes[iV], N = ejes[iN];
        float w = tam[iH], h = tam[iV], t = tam[iN];

        var raiz = new GameObject(NombreVentanal);
        Undo.RegisterCreatedObjectUndo(raiz, "Generar ventanal");
        raiz.transform.SetParent(pared, false);
        raiz.transform.localPosition = b.center;
        // Se compensa la escala para que todo quede en metros con los ejes de la pared.
        raiz.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);

        var rot = Quaternion.LookRotation(N, V);   // x de la caja = a lo ancho, y = alto, z = grosor
        var marcos = new Malla();
        float p = Mathf.Min(anchoPerimetro, Mathf.Min(w, h) * 0.25f);
        marcos.Caja(V * (h * 0.5f - p * 0.5f), new Vector3(w, p, t), rot);
        marcos.Caja(-V * (h * 0.5f - p * 0.5f), new Vector3(w, p, t), rot);
        marcos.Caja(H * (w * 0.5f - p * 0.5f), new Vector3(p, h, t), rot);
        marcos.Caja(-H * (w * 0.5f - p * 0.5f), new Vector3(p, h, t), rot);

        float wi = w - 2f * p, hi = h - 2f * p, prof = Mathf.Min(profundidadMontante, t);
        int columnas = Mathf.Max(1, Mathf.RoundToInt(wi / Mathf.Max(0.5f, separacionMontantes)));
        for (int i = 1; i < columnas; i++)
            marcos.Caja(H * (-wi * 0.5f + wi * i / columnas), new Vector3(anchoMontante, hi, prof), rot);
        for (int j = 1; j < filas; j++)
            marcos.Caja(V * (-hi * 0.5f + hi * j / filas), new Vector3(wi, anchoMontante, prof), rot);

        // Vidrio: dos caras opuestas en el plano medio, así se ve desde afuera y desde adentro sin duplicar la transparencia.
        var vidrio = new Malla();
        vidrio.Cara(Vector3.zero, H, V, N, wi, hi);
        vidrio.Cara(Vector3.zero, H, V, -N, wi, hi);

        Hijo(raiz.transform, "Marcos", marcos, mMarco, ShadowCastingMode.On, true);
        Hijo(raiz.transform, "Vidrio", vidrio, mVidrio, ShadowCastingMode.Off, false);
        Debug.Log($"[Ventanal] '{pared.name}': {w:0.#} × {h:0.#} m, {columnas} columnas × {filas} filas.");
    }

    static void Hijo(Transform raiz, string nombre, Malla m, Material mat, ShadowCastingMode sombras, bool estatico)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = UtilGeneradores.GuardarMalla(m.CrearMesh($"{NombreVentanal}_{nombre}"));
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = sombras;
        if (estatico) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    void CambiarVidrio()
    {
        var mat = MaterialVidrio(tipoVidrio);
        int cambiados = 0;
        foreach (var p in paredes)
        {
            var v = p != null ? p.Find($"{NombreVentanal}/Vidrio") : null;
            var r = v != null ? v.GetComponent<MeshRenderer>() : null;
            if (r == null) continue;
            Undo.RecordObject(r, "Cambiar vidrio");
            r.sharedMaterial = mat;
            cambiados++;
        }
        Debug.Log($"[Ventanal] Vidrio {tipoVidrio.ToString().ToLower()} en {cambiados} ventanales.");
    }

    static void Quitar(Transform pared)
    {
        var aBorrar = new List<GameObject>();
        foreach (Transform hijo in pared) if (hijo.name == NombreVentanal) aBorrar.Add(hijo.gameObject);
        foreach (var go in aBorrar) Undo.DestroyObjectImmediate(go);
        var mr = pared.GetComponent<MeshRenderer>();
        if (mr != null && !mr.enabled) { Undo.RecordObject(mr, "Mostrar pared"); mr.enabled = true; }
    }

    // ---------- Malla ----------
    class Malla
    {
        readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();

        // Cara rectangular centrada en 'c', con ejes 'a' y 'b' y normal 'normal'.
        public void Cara(Vector3 c, Vector3 a, Vector3 b, Vector3 normal, float largoA, float largoB)
        {
            var ea = a * (largoA * 0.5f); var eb = b * (largoB * 0.5f);
            var q = new[] { c - ea - eb, c + ea - eb, c + ea + eb, c - ea + eb };
            var u = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            // Unity dibuja la cara del lado hacia donde apunta Cross(p1-p0, p2-p0).
            if (Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), normal) < 0f) { System.Array.Reverse(q); System.Array.Reverse(u); }
            int i = v.Count;
            for (int k = 0; k < 4; k++) { v.Add(q[k]); n.Add(normal); uv.Add(u[k]); }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void Caja(Vector3 centro, Vector3 tam, Quaternion rot)
        {
            Vector3 x = rot * Vector3.right, y = rot * Vector3.up, z = rot * Vector3.forward;
            Cara(centro + x * tam.x * 0.5f, y, z, x, tam.y, tam.z);
            Cara(centro - x * tam.x * 0.5f, y, z, -x, tam.y, tam.z);
            Cara(centro + y * tam.y * 0.5f, x, z, y, tam.x, tam.z);
            Cara(centro - y * tam.y * 0.5f, x, z, -y, tam.x, tam.z);
            Cara(centro + z * tam.z * 0.5f, x, y, z, tam.x, tam.y);
            Cara(centro - z * tam.z * 0.5f, x, y, -z, tam.x, tam.y);
        }

        public Mesh CrearMesh(string nombre)
        {
            var mesh = new Mesh { name = nombre };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // ---------- Materiales ----------
    static Material Cargar(string nombre, out string ruta)
    {
        ruta = $"{CarpetaMateriales}/{nombre}.mat";
        return AssetDatabase.LoadAssetAtPath<Material>(ruta);
    }

    static Material Guardar(Material m, string ruta)
    {
        GeneradorMarcasPista.CrearCarpeta(CarpetaMateriales);
        AssetDatabase.CreateAsset(m, ruta);
        AssetDatabase.SaveAssets();
        return m;
    }

    internal static Material MaterialMarco()
    {
        var m = Cargar("Marco_Metal", out var ruta);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        m = new Material(shader) { name = "Marco_Metal" };
        m.SetColor("_BaseColor", new Color(0.23f, 0.25f, 0.28f));
        m.SetFloat("_Metallic", 0.6f);
        m.SetFloat("_Smoothness", 0.5f);
        return Guardar(m, ruta);
    }

    internal static Material MaterialVidrio(TipoVidrio tipo)
    {
        string nombre = tipo == TipoVidrio.Transparente ? "Vidrio_Transparente" : "Vidrio_Opaco";
        var m = Cargar(nombre, out var ruta);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        m = new Material(shader) { name = nombre };
        if (tipo == TipoVidrio.Opaco)
        {
            m.SetColor("_BaseColor", new Color(0.24f, 0.34f, 0.44f));
            m.SetFloat("_Metallic", 0.4f);
            m.SetFloat("_Smoothness", 0.85f);
            return Guardar(m, ruta);
        }
        // URP/Lit en modo transparente (lo mismo que elegir Surface Type = Transparent en el Inspector).
        m.SetColor("_BaseColor", new Color(0.6f, 0.75f, 0.85f, 0.25f));
        m.SetFloat("_Metallic", 0.1f);
        m.SetFloat("_Smoothness", 0.9f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return Guardar(m, ruta);
    }
}
#endif
