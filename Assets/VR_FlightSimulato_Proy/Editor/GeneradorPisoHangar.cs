// Piso de hangar: reutiliza el hijo 'Floor' del prefab (plano con collider y área de teletransporte),
// le pone concreto, lo sube al nivel de la plataforma y le pinta encima:
//  - círculo amarillo donde va el avión,
//  - línea guía desde el portón hasta el centro del círculo, con barra de alto,
//  - franja de seguridad amarilla y negra en la entrada.
// También escala el hangar SIN hundirlo: el pivote está muy arriba del piso, así que al cambiar la Y
// el hangar bajaba; aquí se recoloca para que el piso quede donde estaba.
// Menú: Tools > Piso de hangar
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorPisoHangar : EditorWindow
{
    const string NombrePiso = "Floor";
    const string NombreMarcas = "Marcas_Hangar";
    const string NombreMaterialPiso = "MAT_ConcretoHangar";
    const string RutaAmarillo = "Assets/VR_FlightSimulato_Proy/Materials/Pista/MarcasRodaje.mat";
    const string RutaNegro = "Assets/VR_FlightSimulato_Proy/Materials/Pista/MarcaNegra.mat";

    internal enum Frente { Auto, MasX, MenosX, MasZ, MenosZ }

    // ---------- Parámetros ----------
    [SerializeField] List<Transform> hangares = new List<Transform>();
    [SerializeField] Transform plataforma;
    Vector3 escala = new Vector3(1f, 0.75f, 1f);
    Frente frente = Frente.Auto;
    float radioCirculo = 0f;              // 0 = automático según el tamaño del piso
    float anchoLinea = 0.35f;             // m (más ancho que lo real para que se lea en VR)
    bool circulo = true, guia = true, franja = true, ponerConcreto = true;

    const float AlturaMarcas = 0.02f;     // m sobre el piso
    const float FranjaInicio = 0.3f, FranjaFin = 0.9f, PasoFranja = 1.2f;

    [MenuItem("Tools/Piso de hangar")]
    static void Abrir() => GetWindow<GeneradorPisoHangar>("Piso de hangar");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Selecciona la RAÍZ de cada hangar (el objeto del prefab) y pulsa 'Usar selección'. El piso es el hijo 'Floor' que ya trae el prefab.", MessageType.Info);
        Lista("Hangares", hangares);

        EditorGUILayout.LabelField("1. Tamaño", EditorStyles.boldLabel);
        escala = EditorGUILayout.Vector3Field("Escala del hangar", escala);
        using (new EditorGUI.DisabledScope(hangares.Count == 0))
            if (GUILayout.Button("Aplicar escala (sin hundir el hangar)")) AplicarEscala();

        EditorGUILayout.LabelField("2. Piso y marcas", EditorStyles.boldLabel);
        plataforma = (Transform)EditorGUILayout.ObjectField("Plataforma (altura)", plataforma, typeof(Transform), true);
        frente = (Frente)EditorGUILayout.EnumPopup("Lado del portón", frente);
        radioCirculo = EditorGUILayout.FloatField("Radio del círculo (0 = auto)", radioCirculo);
        anchoLinea = EditorGUILayout.FloatField("Ancho de líneas (m)", anchoLinea);
        ponerConcreto = EditorGUILayout.Toggle("Material de concreto", ponerConcreto);
        circulo = EditorGUILayout.Toggle("Círculo", circulo);
        guia = EditorGUILayout.Toggle("Línea guía", guia);
        franja = EditorGUILayout.Toggle("Franja de seguridad", franja);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(hangares.Count == 0))
        {
            if (GUILayout.Button("Generar piso y marcas", GUILayout.Height(32))) Generar();
            if (GUILayout.Button("Borrar marcas")) foreach (var h in hangares) if (h != null) BorrarMarcas(BuscarPiso(h));
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

    // ---------- Piso ----------
    internal static Transform BuscarPiso(Transform hangar)
    {
        foreach (var t in hangar.GetComponentsInChildren<Transform>(true))
            if (t.name == NombrePiso && t.GetComponent<MeshFilter>() != null) return t;
        return null;
    }

    // Centro de la cara superior del piso y medio tamaño en metros a lo largo de sus ejes X y Z.
    internal static Vector3 CentroPiso(Transform piso, out float mitadX, out float mitadZ)
    {
        var b = piso.GetComponent<MeshFilter>().sharedMesh.bounds;
        var ls = piso.lossyScale;
        mitadX = b.extents.x * Mathf.Abs(ls.x);
        mitadZ = b.extents.z * Mathf.Abs(ls.z);
        return piso.TransformPoint(b.center + Vector3.up * b.extents.y);
    }

    void AplicarEscala()
    {
        int grupoUndo = Undo.GetCurrentGroup();
        foreach (var h in hangares)
        {
            if (h == null) continue;
            var piso = BuscarPiso(h);
            if (piso == null) { Debug.LogWarning($"[Hangar] '{h.name}' no tiene hijo '{NombrePiso}'; escalo sin recolocar."); }
            var antes = piso != null ? CentroPiso(piso, out _, out _) : h.position;
            Undo.RecordObject(h, "Escalar hangar");
            h.localScale = escala;
            var despues = piso != null ? CentroPiso(piso, out _, out _) : h.position;
            h.position += antes - despues;   // el piso queda en el mismo lugar
            Debug.Log($"[Hangar] '{h.name}' escalado a {escala}; piso en su lugar.");
        }
        Undo.CollapseUndoOperations(grupoUndo);
        if (hangares.Count > 0 && hangares[0] != null) EditorSceneManager.MarkSceneDirty(hangares[0].gameObject.scene);
    }

    void Generar()
    {
        int grupoUndo = Undo.GetCurrentGroup();
        Physics.SyncTransforms();
        var matPiso = ponerConcreto ? MaterialPiso() : null;
        var amarillo = MaterialColor(RutaAmarillo, "MarcasRodaje", new Color(0.93f, 0.72f, 0.12f));
        var negro = MaterialColor(RutaNegro, "MarcaNegra", new Color(0.05f, 0.05f, 0.05f));

        foreach (var h in hangares)
        {
            if (h == null) continue;
            var piso = BuscarPiso(h);
            if (piso == null) { Debug.LogWarning($"[Hangar] '{h.name}' no tiene hijo '{NombrePiso}'."); continue; }

            if (plataforma != null && GeneradorMarcasPista.Medir(plataforma, out var arriba, out _, out _, out _))
            {
                var c = CentroPiso(piso, out _, out _);
                Undo.RecordObject(piso, "Subir piso de hangar");
                piso.position += Vector3.up * (arriba.y + 0.01f - c.y);
            }
            if (matPiso != null)
            {
                var r = piso.GetComponent<Renderer>();
                Undo.RecordObject(r, "Material de piso");
                r.sharedMaterial = matPiso;
            }

            var centro = CentroPiso(piso, out float mx, out float mz);
            RevisarTerreno(h, piso, centro, mx, mz);
            var dir = frente == Frente.Auto ? DetectarFrente(h, piso, centro, mx, mz) : frente;
            Marcas(piso, dir, mx, mz, amarillo, negro);
        }
        Undo.CollapseUndoOperations(grupoUndo);
        if (hangares.Count > 0 && hangares[0] != null) EditorSceneManager.MarkSceneDirty(hangares[0].gameObject.scene);
    }

    // El portón es el único lado por el que un rayo desde el centro sale sin chocar con el hangar.
    internal static Frente DetectarFrente(Transform hangar, Transform piso, Vector3 centro, float mx, float mz)
    {
        var lados = new[] { Frente.MasX, Frente.MenosX, Frente.MasZ, Frente.MenosZ };
        var abiertos = new List<Frente>();
        foreach (var lado in lados)
        {
            var d = DireccionMundo(piso, lado);
            float largo = (lado == Frente.MasX || lado == Frente.MenosX ? mx : mz) + 3f;
            bool choca = false;
            foreach (var hit in Physics.RaycastAll(centro + Vector3.up * 2f, d, largo, ~0, QueryTriggerInteraction.Ignore))
                if (hit.transform.IsChildOf(hangar) && hit.transform != piso) { choca = true; break; }
            if (!choca) abiertos.Add(lado);
        }
        if (abiertos.Count == 1)
        {
            Debug.Log($"[Hangar] '{hangar.name}': portón detectado en {abiertos[0]} del piso.");
            return abiertos[0];
        }
        Debug.LogWarning($"[Hangar] '{hangar.name}': no pude detectar el portón ({abiertos.Count} lados abiertos); uso MenosZ. Elígelo a mano en 'Lado del portón' si quedó mal.");
        return Frente.MenosZ;
    }

    internal static Vector3 DireccionMundo(Transform piso, Frente lado) => piso.TransformDirection(Local(lado)).normalized;

    static Vector3 Local(Frente lado) => lado switch
    {
        Frente.MasX => Vector3.right,
        Frente.MenosX => Vector3.left,
        Frente.MasZ => Vector3.forward,
        _ => Vector3.back,
    };

    // Avisa si el Terrain sobresale del piso (es lo que hacía ver pasto dentro del hangar).
    static void RevisarTerreno(Transform hangar, Transform piso, Vector3 centro, float mx, float mz)
    {
        float peor = 0f;
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                var p = centro + piso.right * (i * mx * 0.9f) + piso.forward * (j * mz * 0.9f);
                foreach (var t in Terrain.activeTerrains)
                {
                    var o = t.GetPosition(); var s = t.terrainData.size;
                    if (p.x < o.x || p.x > o.x + s.x || p.z < o.z || p.z > o.z + s.z) continue;
                    peor = Mathf.Max(peor, t.SampleHeight(p) + o.y - centro.y);
                }
            }
        if (peor > 0f)
            Debug.LogWarning($"[Hangar] '{hangar.name}': el terreno sobresale hasta {peor:0.00} m del piso. Aplánalo bajo el hangar (Terrain Tools > Set Height) para que no se vea pasto adentro.");
    }

    // ---------- Marcas ----------
    readonly List<Vector3> vA = new List<Vector3>(), vN = new List<Vector3>();
    readonly List<int> tA = new List<int>(), tN = new List<int>();
    Vector3 haciaPorton, haciaFondo, travesia;
    float mitadFondo;

    // Punto en metros: a = a lo ancho del portón, b = desde el borde del portón hacia el fondo.
    Vector3 Q(float a, float b, float y) => haciaPorton * mitadFondo + haciaFondo * b + travesia * a + Vector3.up * y;

    static void Cuad(List<Vector3> v, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        var q = new[] { p0, p1, p2, p3 };
        if (Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), Vector3.up) < 0f) System.Array.Reverse(q);
        int i = v.Count;
        v.AddRange(q);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    void RectAB(float a0, float a1, float b0, float b1, bool enNegro = false)
    {
        float y = AlturaMarcas + (enNegro ? 0.005f : 0f);
        Cuad(enNegro ? vN : vA, enNegro ? tN : tA, Q(a0, b0, y), Q(a1, b0, y), Q(a1, b1, y), Q(a0, b1, y));
    }

    void Marcas(Transform piso, Frente lado, float mx, float mz, Material amarillo, Material negro)
    {
        BorrarMarcas(piso);
        vA.Clear(); vN.Clear(); tA.Clear(); tN.Clear();

        haciaPorton = Local(lado);
        haciaFondo = -haciaPorton;
        travesia = new Vector3(haciaFondo.z, 0f, -haciaFondo.x);
        bool porX = lado == Frente.MasX || lado == Frente.MenosX;
        mitadFondo = porX ? mx : mz;
        float mitadAncho = porX ? mz : mx;
        float r = radioCirculo > 0f ? radioCirculo : 0.38f * Mathf.Min(2f * mitadAncho, 2f * mitadFondo);
        float w = anchoLinea, cb = mitadFondo;   // el círculo va en el centro del piso

        if (franja)
        {
            float a0 = -mitadAncho + 1f, a1 = mitadAncho - 1f;
            RectAB(a0, a1, FranjaInicio, FranjaFin);
            float alto = FranjaFin - FranjaInicio;
            for (float x = a0; x + PasoFranja * 0.5f <= a1; x += PasoFranja)
                Cuad(vN, tN, Q(x, FranjaInicio, AlturaMarcas + 0.005f), Q(x + PasoFranja * 0.5f, FranjaInicio, AlturaMarcas + 0.005f),
                     Q(Mathf.Min(x + PasoFranja * 0.5f + alto, a1), FranjaFin, AlturaMarcas + 0.005f), Q(Mathf.Min(x + alto, a1), FranjaFin, AlturaMarcas + 0.005f));
        }
        if (circulo)
        {
            const int n = 72;
            for (int i = 0; i < n; i++)
            {
                float t0 = i * Mathf.PI * 2f / n, t1 = (i + 1) * Mathf.PI * 2f / n;
                Vector2 d0 = new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)), d1 = new Vector2(Mathf.Cos(t1), Mathf.Sin(t1));
                float ri = r - w * 0.5f, re = r + w * 0.5f;
                Cuad(vA, tA, Q(d0.x * ri, cb + d0.y * ri, AlturaMarcas), Q(d0.x * re, cb + d0.y * re, AlturaMarcas),
                     Q(d1.x * re, cb + d1.y * re, AlturaMarcas), Q(d1.x * ri, cb + d1.y * ri, AlturaMarcas));
            }
        }
        if (guia)
        {
            float inicio = franja ? FranjaFin + 0.5f : 0.5f;
            RectAB(-w * 0.5f, w * 0.5f, inicio, cb);
            RectAB(-1.5f, 1.5f, cb - w * 0.5f, cb + w * 0.5f);   // barra de alto en el centro
        }

        var raiz = new GameObject(NombreMarcas);
        Undo.RegisterCreatedObjectUndo(raiz, "Marcas de hangar");
        raiz.transform.SetParent(piso, false);
        // El piso tiene escala no uniforme: se compensa para que las marcas queden en metros.
        var ls = piso.lossyScale;
        raiz.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        raiz.transform.localPosition = piso.GetComponent<MeshFilter>().sharedMesh.bounds.center;
        // La raíz tiene los ejes del piso y escala 1 en mundo: los puntos de Q ya están en metros.
        CrearHijo(raiz.transform, "Amarillo", vA, tA, amarillo);
        CrearHijo(raiz.transform, "Negro", vN, tN, negro);
    }

    static void CrearHijo(Transform raiz, string nombre, List<Vector3> v, List<int> t, Material mat)
    {
        if (v.Count == 0) return;
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);
        var mesh = new Mesh { name = $"{NombreMarcas}_{nombre}" };
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        var normales = new Vector3[v.Count];
        for (int i = 0; i < normales.Length; i++) normales[i] = Vector3.up;
        mesh.normals = normales;
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = UtilGeneradores.GuardarMalla(mesh);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    static void BorrarMarcas(Transform piso)
    {
        if (piso == null) return;
        var aBorrar = new List<GameObject>();
        foreach (Transform hijo in piso) if (hijo.name == NombreMarcas) aBorrar.Add(hijo.gameObject);
        foreach (var go in aBorrar) Undo.DestroyObjectImmediate(go);
    }

    // ---------- Materiales ----------
    static Material BuscarMaterial(string nombre)
    {
        foreach (var guid in AssetDatabase.FindAssets($"{nombre} t:Material"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(ruta) == nombre) return AssetDatabase.LoadAssetAtPath<Material>(ruta);
        }
        return null;
    }

    // Copia de MAT_Concrete más limpia y pulida: poca variación y algo de brillo.
    static Material MaterialPiso()
    {
        var m = BuscarMaterial(NombreMaterialPiso);
        if (m != null) return m;
        var origen = BuscarMaterial("MAT_Concrete");
        if (origen == null) { Debug.LogWarning("[Hangar] No encontré MAT_Concrete; dejo el material actual del piso."); return null; }
        m = new Material(origen) { name = NombreMaterialPiso };
        if (m.HasProperty("_Variacion")) m.SetFloat("_Variacion", 0.05f);
        if (m.HasProperty("_SmoothnessMax")) m.SetFloat("_SmoothnessMax", 0.4f);
        var ruta = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(origen)).Replace('\\', '/') + $"/{NombreMaterialPiso}.mat";
        AssetDatabase.CreateAsset(m, ruta);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Hangar] Creé '{ruta}'. Ajusta su Tint si lo quieres más claro.");
        return m;
    }

    static Material MaterialColor(string ruta, string nombre, Color color)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        m = new Material(shader) { name = nombre };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.15f);
        GeneradorMarcasPista.CrearCarpeta(System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/'));
        AssetDatabase.CreateAsset(m, ruta);
        AssetDatabase.SaveAssets();
        return m;
    }
}
#endif
