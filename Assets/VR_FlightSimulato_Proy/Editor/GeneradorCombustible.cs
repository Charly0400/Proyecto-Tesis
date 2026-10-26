// Zona de combustible: dique (muro de contención) con piso de concreto, tanques horizontales sobre soportes
// con franja de color por combustible (rojo AVGAS 100LL, negro JET A-1), pasarela con barandal y escalera,
// tubería hacia una isla con surtidor, manguera, bolardos y extintor, y letrero de "no fumar".
// Se guarda como prefab con sus mallas como assets y se coloca donde están los tanques actuales,
// que solo se desactivan. La isla queda hacia el eje X rojo de la ubicación.
// Menú: Tools > Zona de combustible
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorCombustible : EditorWindow
{
    const string CarpetaPrefabs = "Assets/VR_FlightSimulato_Proy/Prefabs/Generado";
    const string CarpetaMateriales = "Assets/VR_FlightSimulato_Proy/Materials/Combustible";

    // ---------- Parámetros ----------
    [SerializeField] Transform ubicacion;
    string nombre = "ZonaCombustible";
    int tanques = 2;
    float largoTanque = 8f, diametroTanque = 3f, separacion = 2f;
    bool isla = true, letrero = true;

    [MenuItem("Tools/Zona de combustible")]
    static void Abrir() => GetWindow<GeneradorCombustible>("Zona de combustible");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Pon en 'Ubicación' tus tanques actuales (o el objeto que los agrupa): la zona nueva se centra ahí, apoyada en el suelo, y lo viejo solo se desactiva. La isla con el surtidor queda hacia el eje X rojo de la ubicación.", MessageType.Info);
        ubicacion = (Transform)EditorGUILayout.ObjectField("Ubicación (tanques actuales)", ubicacion, typeof(Transform), true);
        nombre = EditorGUILayout.TextField("Nombre del prefab", nombre);

        EditorGUILayout.LabelField("Tanques", EditorStyles.boldLabel);
        tanques = EditorGUILayout.IntSlider("Cantidad", tanques, 1, 4);
        largoTanque = EditorGUILayout.FloatField("Largo (m)", largoTanque);
        diametroTanque = EditorGUILayout.FloatField("Diámetro (m)", diametroTanque);
        separacion = EditorGUILayout.FloatField("Separación entre tanques (m)", separacion);
        EditorGUILayout.LabelField("Franjas: rojo = AVGAS 100LL, negro = JET A-1 (se alternan).", EditorStyles.miniLabel);

        EditorGUILayout.LabelField("Extras", EditorStyles.boldLabel);
        isla = EditorGUILayout.Toggle("Isla con surtidor", isla);
        letrero = EditorGUILayout.Toggle("Letrero de no fumar", letrero);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(ubicacion == null || string.IsNullOrWhiteSpace(nombre)))
            if (GUILayout.Button("Generar zona de combustible", GUILayout.Height(32))) Generar();
    }

    // ---------- Generación ----------
    void Generar()
    {
        string rutaPrefab = $"{CarpetaPrefabs}/{nombre}.prefab";
        // Centro: el de los renderers de la ubicación, apoyado en lo más bajo (el suelo de los tanques viejos).
        var pos = ubicacion.position;
        var rs = ubicacion.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            pos = new Vector3(b.center.x, b.min.y, b.center.z);
        }
        var giro = Quaternion.Euler(0f, ubicacion.eulerAngles.y, 0f);
        var padre = ubicacion.parent;
        bool ubicacionEsGenerada = EsInstanciaDe(ubicacion.gameObject, rutaPrefab);

        int grupoUndo = Undo.GetCurrentGroup();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && EsInstanciaDe(t.gameObject, rutaPrefab))
                Undo.DestroyObjectImmediate(t.gameObject);
        if (!ubicacionEsGenerada && ubicacion != null)
        {
            Undo.RecordObject(ubicacion.gameObject, "Desactivar tanques anteriores");
            ubicacion.gameObject.SetActive(false);
        }

        var raiz = Construir();
        raiz.transform.SetParent(padre, false);
        raiz.transform.SetPositionAndRotation(pos, giro);
        Undo.RegisterCreatedObjectUndo(raiz, "Generar zona de combustible");

        GeneradorMarcasPista.CrearCarpeta(CarpetaPrefabs);
        PrefabUtility.SaveAsPrefabAssetAndConnect(raiz, rutaPrefab, InteractionMode.AutomatedAction);
        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        ubicacion = raiz.transform;
        Debug.Log($"[Combustible] '{rutaPrefab}' generada con {tanques} tanques. Si hay luces cerca, vuelve a generarlas para que esquiven la zona.");
    }

    static bool EsInstanciaDe(GameObject go, string rutaPrefab) =>
        PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == rutaPrefab;

    GameObject Construir()
    {
        var piso = new ConstructorMalla(); var muro = new ConstructorMalla(); var tanque = new ConstructorMalla();
        var rojo = new ConstructorMalla(); var negro = new ConstructorMalla(); var metal = new ConstructorMalla();
        var blanco = new ConstructorMalla(); var amarillo = new ConstructorMalla();

        float L = largoTanque, d = diametroTanque, r = d * 0.5f, paso = d + separacion;
        const float AltoPiso = 0.15f, AltoMuro = 1f, GrosorMuro = 0.3f, Tapa = 0.4f;
        float hx = L * 0.5f + Tapa + 2f;
        float hz = (tanques - 1) * paso * 0.5f + r + 2f;
        float yc = AltoPiso + 0.6f + r;
        var zs = new float[tanques];
        for (int i = 0; i < tanques; i++) zs[i] = -(tanques - 1) * paso * 0.5f + i * paso;

        // Dique: piso de concreto y muro bajo alrededor.
        piso.Caja(new Vector3(0f, AltoPiso * 0.5f, 0f), new Vector3(2f * hx + 2f * GrosorMuro, AltoPiso, 2f * hz + 2f * GrosorMuro));
        muro.Caja(new Vector3(hx + GrosorMuro * 0.5f, AltoMuro * 0.5f, 0f), new Vector3(GrosorMuro, AltoMuro, 2f * hz + 2f * GrosorMuro));
        muro.Caja(new Vector3(-(hx + GrosorMuro * 0.5f), AltoMuro * 0.5f, 0f), new Vector3(GrosorMuro, AltoMuro, 2f * hz + 2f * GrosorMuro));
        muro.Caja(new Vector3(0f, AltoMuro * 0.5f, hz + GrosorMuro * 0.5f), new Vector3(2f * hx, AltoMuro, GrosorMuro));
        muro.Caja(new Vector3(0f, AltoMuro * 0.5f, -(hz + GrosorMuro * 0.5f)), new Vector3(2f * hx, AltoMuro, GrosorMuro));

        // Tanques acostados a lo largo de X, con tapas abombadas, dos franjas y dos soportes.
        float hSoporte = 0.6f + r * 0.5f;
        for (int i = 0; i < tanques; i++)
        {
            var centro = new Vector3(0f, yc, zs[i]);
            var acostado = Matrix4x4.TRS(centro, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            tanque.Matriz = acostado;
            tanque.Prisma(new Vector3(0f, -L * 0.5f, 0f), r, r, L, 20, true, false);
            tanque.Prisma(new Vector3(0f, L * 0.5f, 0f), r, r * 0.55f, Tapa, 20, true, true);
            tanque.Prisma(new Vector3(0f, -L * 0.5f - Tapa, 0f), r * 0.55f, r, Tapa, 20, true, true);
            tanque.Matriz = Matrix4x4.identity;

            var banda = i % 2 == 0 ? rojo : negro;
            banda.Matriz = acostado;
            banda.Prisma(new Vector3(0f, L * 0.25f - 0.3f, 0f), r + 0.02f, r + 0.02f, 0.6f, 20, true, false);
            banda.Prisma(new Vector3(0f, -L * 0.25f - 0.3f, 0f), r + 0.02f, r + 0.02f, 0.6f, 20, true, false);
            banda.Matriz = Matrix4x4.identity;

            metal.Caja(new Vector3(L * 0.3f, AltoPiso + hSoporte * 0.5f, zs[i]), new Vector3(0.4f, hSoporte, d * 0.8f));
            metal.Caja(new Vector3(-L * 0.3f, AltoPiso + hSoporte * 0.5f, zs[i]), new Vector3(0.4f, hSoporte, d * 0.8f));
        }

        // Pasarela sobre los tanques, con barandal, y escalera hasta el piso del dique.
        float yPasarela = yc + r + 0.1f, z0 = zs[0] - 0.5f, zEscalera = zs[tanques - 1] + r + 0.6f;
        metal.Caja(new Vector3(0f, yPasarela, (z0 + zEscalera) * 0.5f), new Vector3(0.9f, 0.08f, zEscalera - z0));
        int postes = Mathf.Max(2, Mathf.CeilToInt((zEscalera - z0) / 1.5f) + 1);
        for (int lado = -1; lado <= 1; lado += 2)
        {
            float x = lado * 0.45f;
            for (int k = 0; k < postes; k++)
            {
                float z = Mathf.Lerp(z0, zEscalera, k / (float)(postes - 1));
                metal.Viga(new Vector3(x, yPasarela, z), new Vector3(x, yPasarela + 1f, z), 0.05f);
            }
            metal.Viga(new Vector3(x, yPasarela + 1f, z0), new Vector3(x, yPasarela + 1f, zEscalera), 0.05f);
            metal.Viga(new Vector3(x, yPasarela + 0.5f, z0), new Vector3(x, yPasarela + 0.5f, zEscalera), 0.04f);
        }
        float ze = zEscalera + 0.1f;
        amarillo.Viga(new Vector3(-0.25f, AltoPiso, ze), new Vector3(-0.25f, yPasarela + 1f, ze), 0.06f);
        amarillo.Viga(new Vector3(0.25f, AltoPiso, ze), new Vector3(0.25f, yPasarela + 1f, ze), 0.06f);
        for (float y = AltoPiso + 0.3f; y < yPasarela; y += 0.3f)
            amarillo.Viga(new Vector3(-0.25f, y, ze), new Vector3(0.25f, y, ze), 0.04f);

        // Tubería: de cada tanque a un colector y de ahí, por encima del muro, a la isla.
        float yTubo = yc - r * 0.5f, xColector = L * 0.5f + Tapa + 0.8f;
        float xIsla = hx + GrosorMuro + 3.5f;
        for (int i = 0; i < tanques; i++)
            metal.Tubo(new Vector3(L * 0.5f + Tapa - 0.1f, yTubo, zs[i]), new Vector3(xColector, yTubo, zs[i]), 0.1f);
        metal.Tubo(new Vector3(xColector, yTubo, zs[0]), new Vector3(xColector, yTubo, zs[tanques - 1]), 0.12f);
        if (isla)
        {
            var c0 = new Vector3(xColector, yTubo, 0f);
            var c1 = new Vector3(xIsla - 0.8f, yTubo, 0f);
            var c2 = new Vector3(xIsla - 0.8f, 0.45f, 0f);
            var c3 = new Vector3(xIsla - 0.3f, 0.45f, 0f);
            metal.Tubo(c0, c1, 0.12f); metal.Tubo(c1, c2, 0.12f); metal.Tubo(c2, c3, 0.12f);
            foreach (var p in new[] { c0, c1, c2 }) metal.Caja(p, Vector3.one * 0.3f);
            metal.Caja(new Vector3(xIsla - 0.8f, (yTubo + AltoPiso) * 0.5f, 1f), new Vector3(0.15f, yTubo - AltoPiso, 0.15f));

            // Isla: base, surtidor con franja roja y pantalla, manguera, bolardos y extintor.
            muro.Caja(new Vector3(xIsla, 0.125f, 0f), new Vector3(1.5f, 0.25f, 4f));
            blanco.Caja(new Vector3(xIsla, 0.25f + 0.8f, 0f), new Vector3(0.6f, 1.6f, 0.9f));
            rojo.Caja(new Vector3(xIsla, 0.25f + 1.7f, 0f), new Vector3(0.66f, 0.2f, 0.96f));
            negro.Caja(new Vector3(xIsla + 0.31f, 1.45f, 0f), new Vector3(0.02f, 0.35f, 0.5f));
            negro.Tubo(new Vector3(xIsla + 0.3f, 1.3f, 0.35f), new Vector3(xIsla + 0.6f, 0.7f, 0.45f), 0.035f, 6);
            negro.Tubo(new Vector3(xIsla + 0.6f, 0.7f, 0.45f), new Vector3(xIsla + 0.33f, 1f, 0.42f), 0.035f, 6);
            foreach (float z in new[] { -2.4f, 2.4f })
                amarillo.Prisma(new Vector3(xIsla, 0f, z), 0.12f, 0.12f, 1f, 10, true, true);
            rojo.Prisma(new Vector3(xIsla - 0.45f, 0.25f, -1.5f), 0.09f, 0.09f, 0.5f, 10, true, true);
        }

        // Letrero de "no fumar": poste, tablero blanco y círculo rojo con diagonal, mirando hacia +X.
        if (letrero)
        {
            float x = xIsla + 1.5f, z = 2.6f, yT = 2.1f, rIcono = 0.28f;
            metal.Tubo(new Vector3(x, 0f, z), new Vector3(x, 2.5f, z), 0.05f, 8);
            blanco.Caja(new Vector3(x + 0.07f, yT, z), new Vector3(0.04f, 0.8f, 1f));
            float xi = x + 0.1f;
            for (int k = 0; k < 16; k++)
            {
                float a0 = Mathf.PI * 2f * k / 16f, a1 = Mathf.PI * 2f * (k + 1) / 16f;
                rojo.Viga(new Vector3(xi, yT + Mathf.Sin(a0) * rIcono, z + Mathf.Cos(a0) * rIcono),
                          new Vector3(xi, yT + Mathf.Sin(a1) * rIcono, z + Mathf.Cos(a1) * rIcono), 0.06f);
            }
            rojo.Viga(new Vector3(xi, yT + 0.2f, z - 0.2f), new Vector3(xi, yT - 0.2f, z + 0.2f), 0.06f);
        }

        var raiz = new GameObject(nombre);
        Hijo(raiz, "Piso", piso, BuscarMaterial("MAT_Concrete") ?? Mat("Comb_Muro", new Color(0.73f, 0.71f, 0.67f), 0.2f, 0f), true);
        Hijo(raiz, "Muro", muro, Mat("Comb_Muro", new Color(0.73f, 0.71f, 0.67f), 0.2f, 0f), true);
        Hijo(raiz, "Tanques", tanque, Mat("Comb_Tanque", new Color(0.9f, 0.91f, 0.92f), 0.5f, 0.2f), true);
        Hijo(raiz, "Rojo", rojo, Mat("Comb_Rojo", new Color(0.75f, 0.22f, 0.17f), 0.4f, 0f), true);
        Hijo(raiz, "Negro", negro, Mat("Comb_Negro", new Color(0.17f, 0.17f, 0.16f), 0.4f, 0f), true);
        Hijo(raiz, "Metal", metal, Mat("Comb_Metal", new Color(0.54f, 0.56f, 0.59f), 0.45f, 0.5f), true);
        Hijo(raiz, "Blanco", blanco, Mat("Comb_Blanco", new Color(0.96f, 0.95f, 0.93f), 0.3f, 0f), true);
        Hijo(raiz, "Amarillo", amarillo, Mat("Comb_Amarillo", new Color(0.88f, 0.7f, 0.13f), 0.35f, 0f), true);

        // Colisiones simples: piso, muros, tanques e isla.
        Caja(raiz, new Vector3(0f, AltoPiso * 0.5f, 0f), new Vector3(2f * hx + 2f * GrosorMuro, AltoPiso, 2f * hz + 2f * GrosorMuro));
        Caja(raiz, new Vector3(hx + GrosorMuro * 0.5f, AltoMuro * 0.5f, 0f), new Vector3(GrosorMuro, AltoMuro, 2f * hz + 2f * GrosorMuro));
        Caja(raiz, new Vector3(-(hx + GrosorMuro * 0.5f), AltoMuro * 0.5f, 0f), new Vector3(GrosorMuro, AltoMuro, 2f * hz + 2f * GrosorMuro));
        Caja(raiz, new Vector3(0f, AltoMuro * 0.5f, hz + GrosorMuro * 0.5f), new Vector3(2f * hx, AltoMuro, GrosorMuro));
        Caja(raiz, new Vector3(0f, AltoMuro * 0.5f, -(hz + GrosorMuro * 0.5f)), new Vector3(2f * hx, AltoMuro, GrosorMuro));
        foreach (float z in zs) Caja(raiz, new Vector3(0f, yc, z), new Vector3(L + 2f * Tapa, d, d));
        if (isla) Caja(raiz, new Vector3(xIsla, 0.9f, 0f), new Vector3(1.5f, 1.8f, 4f));
        return raiz;
    }

    static void Caja(GameObject raiz, Vector3 centro, Vector3 tam)
    {
        var c = raiz.AddComponent<BoxCollider>();
        c.center = centro; c.size = tam;
    }

    void Hijo(GameObject raiz, string parte, ConstructorMalla m, Material mat, bool estatico)
    {
        if (m.Vacio) return;
        var go = new GameObject(parte);
        go.transform.SetParent(raiz.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = UtilGeneradores.GuardarMalla(m.CrearMesh($"{nombre}_{parte}"));
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        if (estatico) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // ---------- Materiales ----------
    static Material BuscarMaterial(string nombreMat)
    {
        foreach (var guid in AssetDatabase.FindAssets($"{nombreMat} t:Material"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(ruta) == nombreMat) return AssetDatabase.LoadAssetAtPath<Material>(ruta);
        }
        return null;
    }

    static Material Mat(string nombreMat, Color color, float suavidad, float metal)
    {
        string ruta = $"{CarpetaMateriales}/{nombreMat}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        m = new Material(shader) { name = nombreMat };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", suavidad);
        m.SetFloat("_Metallic", metal);
        GeneradorMarcasPista.CrearCarpeta(CarpetaMateriales);
        AssetDatabase.CreateAsset(m, ruta);
        AssetDatabase.SaveAssets();
        return m;
    }
}
#endif
