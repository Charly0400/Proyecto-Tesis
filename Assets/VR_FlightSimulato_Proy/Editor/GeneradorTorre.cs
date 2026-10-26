// Torre de control: edificio de operaciones, fuste con franja naranja, balcón con barandal,
// cabina de vidrio inclinada (octágono más ancho arriba), techo con alero y antena.
// Se guarda como prefab con sus mallas como assets y se coloca donde estaba la torre actual,
// que solo se desactiva. Deja un hijo 'PuntoFaro' en la cima para Tools > Luces de aeródromo.
// Menú: Tools > Torre de control
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorTorre : EditorWindow
{
    const string CarpetaPrefabs = "Assets/VR_FlightSimulato_Proy/Prefabs/Generado";
    const string CarpetaMateriales = "Assets/VR_FlightSimulato_Proy/Materials/Torre";

    // ---------- Parámetros ----------
    [SerializeField] Transform ubicacion;
    string nombre = "TorreControl";
    float alturaTotal = 30f;              // m hasta el techo de la cabina (sin antena)
    float baseAncho = 14f, baseFondo = 10f, baseAlto = 4.5f;
    float diametroFuste = 5f;
    float diametroCabinaAbajo = 8f, diametroCabinaArriba = 10f, altoCabina = 4f;
    GeneradorVentanal.TipoVidrio tipoVidrio = GeneradorVentanal.TipoVidrio.Transparente;
    bool antena = true;

    const float AltoTecho = 0.6f, AltoLosa = 0.4f, AltoAntepecho = 1f, AltoBarandal = 1.1f;

    [MenuItem("Tools/Torre de control")]
    static void Abrir() => GetWindow<GeneradorTorre>("Torre de control");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Pon en 'Ubicación' tu torre actual: la nueva se coloca en su lugar (misma posición y giro, apoyada en el suelo) y la vieja solo se desactiva. El frente de la torre (puerta) mira hacia el eje Z azul de la ubicación.", MessageType.Info);
        ubicacion = (Transform)EditorGUILayout.ObjectField("Ubicación (torre actual)", ubicacion, typeof(Transform), true);
        nombre = EditorGUILayout.TextField("Nombre del prefab", nombre);

        EditorGUILayout.LabelField("Medidas (m)", EditorStyles.boldLabel);
        alturaTotal = EditorGUILayout.FloatField("Altura total", alturaTotal);
        baseAncho = EditorGUILayout.FloatField("Edificio: ancho", baseAncho);
        baseFondo = EditorGUILayout.FloatField("Edificio: fondo", baseFondo);
        baseAlto = EditorGUILayout.FloatField("Edificio: alto", baseAlto);
        diametroFuste = EditorGUILayout.FloatField("Diámetro del fuste", diametroFuste);
        diametroCabinaAbajo = EditorGUILayout.FloatField("Cabina: diámetro abajo", diametroCabinaAbajo);
        diametroCabinaArriba = EditorGUILayout.FloatField("Cabina: diámetro arriba", diametroCabinaArriba);
        altoCabina = EditorGUILayout.FloatField("Cabina: alto", altoCabina);

        EditorGUILayout.LabelField("Acabados", EditorStyles.boldLabel);
        tipoVidrio = (GeneradorVentanal.TipoVidrio)EditorGUILayout.EnumPopup("Vidrio de la cabina", tipoVidrio);
        antena = EditorGUILayout.Toggle("Antena", antena);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(ubicacion == null || string.IsNullOrWhiteSpace(nombre)))
            if (GUILayout.Button("Generar torre", GUILayout.Height(32))) Generar();
    }

    // ---------- Generación ----------
    void Generar()
    {
        float yTecho = alturaTotal - AltoTecho, yCabina = yTecho - altoCabina, yLosa = yCabina - AltoLosa;
        if (yLosa < baseAlto + 3f)
        { EditorUtility.DisplayDialog("Torre", "La altura total no alcanza para el edificio, el fuste y la cabina. Súbela o baja el edificio.", "OK"); return; }

        string rutaPrefab = $"{CarpetaPrefabs}/{nombre}.prefab";
        // Posición: la de la ubicación, apoyada en lo más bajo de sus renderers (el suelo de la torre vieja).
        var pos = ubicacion.position;
        var rs = ubicacion.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            pos = new Vector3(pos.x, b.min.y, pos.z);
        }
        var giro = Quaternion.Euler(0f, ubicacion.eulerAngles.y, 0f);
        var padre = ubicacion.parent;
        bool ubicacionEsGenerada = EsInstanciaDe(ubicacion.gameObject, rutaPrefab);

        int grupoUndo = Undo.GetCurrentGroup();
        // Quitar torres generadas antes con este prefab (se reemplazan).
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && EsInstanciaDe(t.gameObject, rutaPrefab))
                Undo.DestroyObjectImmediate(t.gameObject);
        if (!ubicacionEsGenerada && ubicacion != null)
        {
            Undo.RecordObject(ubicacion.gameObject, "Desactivar torre anterior");
            ubicacion.gameObject.SetActive(false);
        }

        var raiz = Construir(yTecho, yCabina, yLosa);
        raiz.transform.SetParent(padre, false);
        raiz.transform.SetPositionAndRotation(pos, giro);
        Undo.RegisterCreatedObjectUndo(raiz, "Generar torre");

        GeneradorMarcasPista.CrearCarpeta(CarpetaPrefabs);
        PrefabUtility.SaveAsPrefabAssetAndConnect(raiz, rutaPrefab, InteractionMode.AutomatedAction);
        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        ubicacion = raiz.transform;
        Debug.Log($"[Torre] '{rutaPrefab}' generada: {alturaTotal:0.#} m. Vuelve a generar las luces para poner el faro en la cima.");
    }

    static bool EsInstanciaDe(GameObject go, string rutaPrefab) =>
        PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == rutaPrefab;

    GameObject Construir(float yTecho, float yCabina, float yLosa)
    {
        var base_ = new ConstructorMalla(); var fuste = new ConstructorMalla(); var franja = new ConstructorMalla();
        var gris = new ConstructorMalla(); var techo = new ConstructorMalla(); var ventanas = new ConstructorMalla();
        var oscuro = new ConstructorMalla(); var marcos = new ConstructorMalla(); var vidrio = new ConstructorMalla();

        float W = baseAncho, D = baseFondo, hb = baseAlto, rf = diametroFuste * 0.5f;
        float rb = diametroCabinaAbajo * 0.5f, rt = diametroCabinaArriba * 0.5f, rBal = rb + 1.2f;
        float giro8 = Mathf.PI / 8f;   // octágono con una cara al frente (+Z)

        // Edificio de operaciones: cuerpo, zócalo, pretil, ventanas, puerta y tejadillo.
        base_.Caja(new Vector3(0f, hb * 0.5f, 0f), new Vector3(W, hb, D));
        gris.Caja(new Vector3(0f, 0.2f, 0f), new Vector3(W + 0.1f, 0.4f, D + 0.1f));
        gris.Caja(new Vector3(0f, hb + 0.15f, 0f), new Vector3(W + 0.3f, 0.3f, D + 0.3f));
        int nx = Mathf.Max(1, Mathf.FloorToInt((W - 2f) / 2.4f)), nz = Mathf.Max(1, Mathf.FloorToInt((D - 2f) / 2.4f));
        for (int k = 0; k < nx; k++)
        {
            float x = -(nx - 1) * 1.2f + k * 2.4f;
            ventanas.Caja(new Vector3(x, 2.2f, -(D * 0.5f + 0.03f)), new Vector3(1.6f, 1.4f, 0.1f));
            if (Mathf.Abs(x) > 1.6f) ventanas.Caja(new Vector3(x, 2.2f, D * 0.5f + 0.03f), new Vector3(1.6f, 1.4f, 0.1f));
        }
        for (int k = 0; k < nz; k++)
        {
            float z = -(nz - 1) * 1.2f + k * 2.4f;
            ventanas.Caja(new Vector3(W * 0.5f + 0.03f, 2.2f, z), new Vector3(0.1f, 1.4f, 1.6f));
            ventanas.Caja(new Vector3(-(W * 0.5f + 0.03f), 2.2f, z), new Vector3(0.1f, 1.4f, 1.6f));
        }
        oscuro.Caja(new Vector3(0f, 1.2f, D * 0.5f + 0.04f), new Vector3(1.6f, 2.4f, 0.1f));
        gris.Caja(new Vector3(0f, 2.75f, D * 0.5f + 0.6f), new Vector3(2.4f, 0.12f, 1.2f));

        // Fuste, transición hacia el balcón, franja naranja y ventanitas al frente.
        fuste.Prisma(new Vector3(0f, hb, 0f), rf, rf, yLosa - hb, 16, true, false);
        fuste.Prisma(new Vector3(0f, yLosa - 1.2f, 0f), rf, rBal * 0.85f, 1.2f, 16, true, false);
        franja.Prisma(new Vector3(0f, yLosa - 2.2f, 0f), rf + 0.04f, rf + 0.04f, 0.7f, 16, true, false);
        for (float y = hb + 3f; y < yLosa - 3f; y += 4f)
            ventanas.Caja(new Vector3(0f, y, rf + 0.02f), new Vector3(0.7f, 1.2f, 0.12f));

        // Balcón con barandal.
        gris.Prisma(new Vector3(0f, yLosa, 0f), rBal, rBal, AltoLosa, 8, false, true, giro8);
        var esquinas = Poligono(rBal - 0.15f, 8, giro8, yCabina);
        for (int i = 0; i < 8; i++)
        {
            Vector3 a = esquinas[i], b = esquinas[(i + 1) % 8], m = (a + b) * 0.5f;
            gris.Viga(a, a + Vector3.up * AltoBarandal, 0.07f);
            gris.Viga(m, m + Vector3.up * AltoBarandal, 0.07f);
            gris.Viga(a + Vector3.up * AltoBarandal, b + Vector3.up * AltoBarandal, 0.07f);
            gris.Viga(a + Vector3.up * AltoBarandal * 0.5f, b + Vector3.up * AltoBarandal * 0.5f, 0.05f);
        }

        // Cabina: antepecho opaco y vidrio inclinado hacia afuera, con marcos en esquinas, centro y bordes.
        float rAnte = Mathf.Lerp(rb, rt, AltoAntepecho / altoCabina);
        base_.Prisma(new Vector3(0f, yCabina, 0f), rb, rAnte, AltoAntepecho, 8, false, false, giro8);
        var vAbajo = Poligono(rAnte, 8, giro8, yCabina + AltoAntepecho);
        var vArriba = Poligono(rt, 8, giro8, yTecho);
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8;
            var fuera = ((vAbajo[i] + vAbajo[j]) * 0.5f); fuera.y = 0f; fuera.Normalize();
            Vector3 a0 = Hacia(vAbajo[i], 0.985f), a1 = Hacia(vAbajo[j], 0.985f), b1 = Hacia(vArriba[j], 0.985f), b0 = Hacia(vArriba[i], 0.985f);
            vidrio.Quad(a0, a1, b1, b0, fuera);
            vidrio.Quad(a0, a1, b1, b0, -fuera);
            marcos.Viga(vAbajo[i], vArriba[i], 0.15f);
            marcos.Viga(vAbajo[i], vAbajo[j], 0.18f);
            marcos.Viga(vArriba[i], vArriba[j], 0.18f);
            marcos.Viga((vAbajo[i] + vAbajo[j]) * 0.5f, (vArriba[i] + vArriba[j]) * 0.5f, 0.08f);
        }
        oscuro.Caja(new Vector3(0f, yCabina + 0.45f, rb * 0.55f), new Vector3(rb, 0.9f, 0.8f));   // consola

        // Techo con alero y antena a un lado (el centro queda libre para el faro).
        techo.Prisma(new Vector3(0f, yTecho, 0f), rt + 0.6f, rt + 0.6f, AltoTecho, 8, false, true, giro8);
        if (antena)
        {
            var pie = new Vector3(-rt * 0.45f, alturaTotal, -rt * 0.45f);
            gris.Prisma(pie, 0.09f, 0.05f, 5f, 6, false, true);
            gris.Viga(pie + new Vector3(-0.6f, 3.5f, 0f), pie + new Vector3(0.6f, 3.5f, 0f), 0.05f);
        }

        var raiz = new GameObject(nombre);
        Hijo(raiz, "Edificio", base_, Mat("Torre_Base", new Color(0.85f, 0.84f, 0.81f), 0.2f, 0f), true);
        Hijo(raiz, "Fuste", fuste, Mat("Torre_Fuste", new Color(0.91f, 0.9f, 0.88f), 0.25f, 0f), true);
        Hijo(raiz, "Franja", franja, Mat("Torre_Franja", new Color(0.85f, 0.35f, 0.19f), 0.3f, 0f), true);
        Hijo(raiz, "Metal", gris, Mat("Torre_Gris", new Color(0.6f, 0.64f, 0.67f), 0.4f, 0.3f), true);
        Hijo(raiz, "Techo", techo, Mat("Torre_Techo", new Color(0.81f, 0.83f, 0.85f), 0.3f, 0f), true);
        Hijo(raiz, "Ventanas", ventanas, Mat("Torre_Ventana", new Color(0.18f, 0.29f, 0.39f), 0.85f, 0.3f), true);
        Hijo(raiz, "Puertas", oscuro, Mat("Torre_Oscuro", new Color(0.29f, 0.33f, 0.38f), 0.3f, 0.2f), true);
        Hijo(raiz, "Marcos", marcos, GeneradorVentanal.MaterialMarco(), true);
        Hijo(raiz, "VidrioCabina", vidrio, GeneradorVentanal.MaterialVidrio(tipoVidrio), false);

        var faro = new GameObject("PuntoFaro");
        faro.transform.SetParent(raiz.transform, false);
        faro.transform.localPosition = new Vector3(0f, alturaTotal, 0f);

        // Colisiones simples: edificio, fuste y bloque de la cabina.
        var cEdificio = raiz.AddComponent<BoxCollider>();
        cEdificio.center = new Vector3(0f, hb * 0.5f, 0f); cEdificio.size = new Vector3(W, hb, D);
        var cFuste = raiz.AddComponent<CapsuleCollider>();
        cFuste.direction = 1; cFuste.radius = rf; cFuste.height = yLosa - hb + 2f * rf;
        cFuste.center = new Vector3(0f, (hb + yLosa) * 0.5f, 0f);
        var cCabina = raiz.AddComponent<BoxCollider>();
        cCabina.center = new Vector3(0f, (yLosa + alturaTotal) * 0.5f, 0f);
        cCabina.size = new Vector3(2f * rBal, alturaTotal - yLosa, 2f * rBal);
        return raiz;
    }

    static Vector3[] Poligono(float radio, int lados, float giro, float y)
    {
        var p = new Vector3[lados];
        for (int i = 0; i < lados; i++)
        {
            float a = giro + Mathf.PI * 2f * i / lados;
            p[i] = new Vector3(Mathf.Cos(a) * radio, y, Mathf.Sin(a) * radio);
        }
        return p;
    }

    // Acerca el punto al eje vertical (para meter el vidrio detrás de los marcos).
    static Vector3 Hacia(Vector3 p, float factor) => new Vector3(p.x * factor, p.y, p.z * factor);

    void Hijo(GameObject raiz, string parte, ConstructorMalla m, Material mat, bool estatico)
    {
        if (m.Vacio) return;
        var go = new GameObject(parte);
        go.transform.SetParent(raiz.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = UtilGeneradores.GuardarMalla(m.CrearMesh($"{nombre}_{parte}"));
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = estatico ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (estatico) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
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
