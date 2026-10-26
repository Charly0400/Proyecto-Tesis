// Hangar completo generado: lámina con nervaduras, zócalo, franja naranja, techo con alero,
// fachada con vidrio sobre el portón y 4 portones corredizos (2 por lado, se pueden dejar abiertos o cerrados),
// ventanas altas en costados y fondo, armaduras de acero con lámparas, oficinas con tapanco y escaleras,
// taller al fondo y piso de concreto con círculo, línea guía y franja de seguridad.
// Se guarda como prefab con sus mallas como assets. El piso trae TeleportationArea si XR Interaction Toolkit está instalado.
// Origen = centro del piso; el portón mira hacia +Z.
// Menú: Tools > Hangar completo
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorHangar : EditorWindow
{
    const string CarpetaPrefabs = "Assets/VR_FlightSimulato_Proy/Prefabs/Generado";
    const string CarpetaMateriales = "Assets/VR_FlightSimulato_Proy/Materials/Hangar";

    // ---------- Parámetros ----------
    [SerializeField] Transform ubicacion;
    string nombre = "HangarGenerado";
    bool desactivarOriginal = true;
    float ancho = 50f, fondo = 45f, alturaAlero = 14f, alturaCumbrera = 18f;
    float anchoPorton = 28f, altoPorton = 11f;
    float apertura = 1f;                  // 0 = portones cerrados, 1 = abiertos
    bool oficinas = true, taller = true, marcasPiso = true;
    GeneradorVentanal.TipoVidrio tipoVidrio = GeneradorVentanal.TipoVidrio.Transparente;

    const float Muro = 0.3f, Zocalo = 1f, Franja = 0.6f, Paso = 1f, AnchoNervio = 0.22f, ProfNervio = 0.06f;
    const float Faldon = 6f;              // m horizontales de la parte inclinada del techo

    [MenuItem("Tools/Hangar completo")]
    static void Abrir() => GetWindow<GeneradorHangar>("Hangar completo");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Pon en 'Ubicación' uno de tus hangares: el nuevo se centra en su piso y su portón mira hacia el mismo lado. Si la ubicación ya es un hangar generado, se regenera en su lugar.", MessageType.Info);
        ubicacion = (Transform)EditorGUILayout.ObjectField("Ubicación", ubicacion, typeof(Transform), true);
        nombre = EditorGUILayout.TextField("Nombre del prefab", nombre);
        desactivarOriginal = EditorGUILayout.Toggle("Desactivar el original", desactivarOriginal);

        EditorGUILayout.LabelField("Medidas (m)", EditorStyles.boldLabel);
        ancho = EditorGUILayout.FloatField("Ancho", ancho);
        fondo = EditorGUILayout.FloatField("Fondo", fondo);
        alturaAlero = EditorGUILayout.FloatField("Altura al alero", alturaAlero);
        alturaCumbrera = EditorGUILayout.FloatField("Altura máxima", alturaCumbrera);
        anchoPorton = EditorGUILayout.FloatField("Ancho del portón", anchoPorton);
        altoPorton = EditorGUILayout.FloatField("Alto del portón", altoPorton);

        EditorGUILayout.LabelField("Detalles", EditorStyles.boldLabel);
        apertura = EditorGUILayout.Slider("Portones (0 cerrado, 1 abierto)", apertura, 0f, 1f);
        tipoVidrio = (GeneradorVentanal.TipoVidrio)EditorGUILayout.EnumPopup("Vidrio", tipoVidrio);
        oficinas = EditorGUILayout.Toggle("Oficinas con tapanco", oficinas);
        taller = EditorGUILayout.Toggle("Taller al fondo", taller);
        marcasPiso = EditorGUILayout.Toggle("Marcas en el piso", marcasPiso);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(ubicacion == null || string.IsNullOrWhiteSpace(nombre)))
            if (GUILayout.Button("Generar hangar", GUILayout.Height(32))) Generar();
    }

    // ---------- Colocación ----------
    void Generar()
    {
        if (ancho < 30f || fondo < 25f || anchoPorton > ancho - 10f || altoPorton > alturaAlero - 2f || alturaCumbrera < alturaAlero + 1f)
        { EditorUtility.DisplayDialog("Hangar", "Medidas fuera de rango: ancho ≥ 30, fondo ≥ 25, portón al menos 10 m más angosto que el hangar y 2 m más bajo que el alero, y altura máxima ≥ alero + 1.", "OK"); return; }

        string rutaPrefab = $"{CarpetaPrefabs}/{nombre}.prefab";
        bool regenerar = PrefabUtility.IsPartOfPrefabInstance(ubicacion.gameObject)
                         && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ubicacion.gameObject) == rutaPrefab;
        Vector3 pos; Quaternion rot;
        var piso = regenerar ? null : GeneradorPisoHangar.BuscarPiso(ubicacion);
        if (regenerar) { pos = ubicacion.position; rot = ubicacion.rotation; }
        else if (piso != null)
        {
            Physics.SyncTransforms();
            pos = GeneradorPisoHangar.CentroPiso(piso, out float mx, out float mz);
            var frente = GeneradorPisoHangar.DetectarFrente(ubicacion, piso, pos, mx, mz);
            var dir = GeneradorPisoHangar.DireccionMundo(piso, frente);
            dir.y = 0f;
            rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else { pos = ubicacion.position; rot = Quaternion.Euler(0f, ubicacion.eulerAngles.y, 0f); }
        var padre = ubicacion.parent;

        int grupoUndo = Undo.GetCurrentGroup();
        if (regenerar) Undo.DestroyObjectImmediate(PrefabUtility.GetOutermostPrefabInstanceRoot(ubicacion.gameObject));
        else if (desactivarOriginal) { Undo.RecordObject(ubicacion.gameObject, "Desactivar hangar original"); ubicacion.gameObject.SetActive(false); }

        var raiz = Construir();
        raiz.transform.SetParent(padre, false);
        raiz.transform.SetPositionAndRotation(pos, rot);
        Undo.RegisterCreatedObjectUndo(raiz, "Generar hangar");

        GeneradorMarcasPista.CrearCarpeta(CarpetaPrefabs);
        PrefabUtility.SaveAsPrefabAssetAndConnect(raiz, rutaPrefab, InteractionMode.AutomatedAction);
        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        ubicacion = raiz.transform;
        Debug.Log($"[Hangar] '{rutaPrefab}' generado: {ancho:0.#} × {fondo:0.#} m, portón {anchoPorton:0.#} × {altoPorton:0.#} m.");
    }

    // ---------- Construcción ----------
    ConstructorMalla lamina, zocalo, franja, techo, estructura, oficina, ventana, oscuro, marcos, vidrio,
                     lampara, madera, rojo, cajas, amarillo, negro, porton, piso;
    readonly List<(Vector3 c, Vector3 s)> colisiones = new List<(Vector3, Vector3)>();
    float hw, hd, hf, He, Hr;

    float AlturaTecho(float x)
    {
        float ax = Mathf.Abs(x);
        return ax <= hf ? Hr : He + (Hr - He) * (hw - ax) / (hw - hf);
    }

    GameObject Construir()
    {
        lamina = new ConstructorMalla(); zocalo = new ConstructorMalla(); franja = new ConstructorMalla();
        techo = new ConstructorMalla(); estructura = new ConstructorMalla(); oficina = new ConstructorMalla();
        ventana = new ConstructorMalla(); oscuro = new ConstructorMalla(); marcos = new ConstructorMalla();
        vidrio = new ConstructorMalla(); lampara = new ConstructorMalla(); madera = new ConstructorMalla();
        rojo = new ConstructorMalla(); cajas = new ConstructorMalla(); amarillo = new ConstructorMalla();
        negro = new ConstructorMalla(); porton = new ConstructorMalla(); piso = new ConstructorMalla();
        colisiones.Clear();
        hw = ancho * 0.5f; hd = fondo * 0.5f; He = alturaAlero; Hr = alturaCumbrera;
        hf = Mathf.Max(hw * 0.6f, hw - Faldon);

        piso.Caja(new Vector3(0f, -0.15f, 0f), new Vector3(ancho, 0.3f, fondo));
        Fachada();
        Fondo();
        Costado(1f);
        Costado(-1f);
        Techo();
        Armaduras();
        if (oficinas && fondo >= 30f) { Oficinas(1f); Oficinas(-1f); }
        if (taller) Taller();
        if (marcasPiso) MarcasPiso();

        var raiz = new GameObject(nombre);
        Hijo(raiz, "Lamina", lamina, Mat("Hangar_Lamina", new Color(0.68f, 0.71f, 0.75f), 0.45f, 0.5f));
        Hijo(raiz, "Zocalo", zocalo, Mat("Hangar_Zocalo", new Color(0.43f, 0.47f, 0.5f), 0.35f, 0.4f));
        Hijo(raiz, "Franja", franja, Mat("Hangar_Franja", new Color(0.85f, 0.35f, 0.19f), 0.3f, 0f));
        Hijo(raiz, "Techo", techo, Mat("Hangar_Techo", new Color(0.36f, 0.39f, 0.42f), 0.3f, 0.3f));
        Hijo(raiz, "Estructura", estructura, Mat("Hangar_Estructura", new Color(0.29f, 0.33f, 0.38f), 0.4f, 0.6f));
        Hijo(raiz, "Oficinas", oficina, Mat("Hangar_Oficina", new Color(0.89f, 0.88f, 0.85f), 0.2f, 0f));
        Hijo(raiz, "VentanasOficina", ventana, Mat("Hangar_Ventana", new Color(0.18f, 0.29f, 0.39f), 0.85f, 0.3f));
        Hijo(raiz, "Puertas", oscuro, Mat("Hangar_Puerta", new Color(0.25f, 0.28f, 0.32f), 0.3f, 0.3f));
        Hijo(raiz, "Marcos", marcos, GeneradorVentanal.MaterialMarco());
        Hijo(raiz, "Vidrio", vidrio, GeneradorVentanal.MaterialVidrio(tipoVidrio), false);
        Hijo(raiz, "Lamparas", lampara, MatEmisivo("Hangar_Lampara", new Color(1f, 0.95f, 0.77f), 2.5f));
        Hijo(raiz, "Madera", madera, Mat("Hangar_Madera", new Color(0.54f, 0.43f, 0.29f), 0.2f, 0f));
        Hijo(raiz, "Rojo", rojo, Mat("Hangar_Rojo", new Color(0.75f, 0.22f, 0.17f), 0.4f, 0.1f));
        Hijo(raiz, "Cajas", cajas, Mat("Hangar_Caja", new Color(0.42f, 0.42f, 0.23f), 0.2f, 0f));
        Hijo(raiz, "MarcasAmarillas", amarillo, AssetDatabase.LoadAssetAtPath<Material>("Assets/VR_FlightSimulato_Proy/Materials/Pista/MarcasRodaje.mat")
                                                ?? Mat("Hangar_Amarillo", new Color(0.93f, 0.72f, 0.12f), 0.15f, 0f));
        Hijo(raiz, "MarcasNegras", negro, AssetDatabase.LoadAssetAtPath<Material>("Assets/VR_FlightSimulato_Proy/Materials/Pista/MarcaNegra.mat")
                                          ?? Mat("Hangar_Negro", new Color(0.05f, 0.05f, 0.05f), 0.1f, 0f));

        // Piso: objeto propio con collider y área de teletransporte.
        var goPiso = Hijo(raiz, "Piso", piso, BuscarMaterial("MAT_ConcretoHangar") ?? BuscarMaterial("MAT_Concrete")
                                              ?? Mat("Hangar_Piso", new Color(0.79f, 0.8f, 0.8f), 0.4f, 0f));
        var cPiso = goPiso.AddComponent<BoxCollider>();
        cPiso.center = new Vector3(0f, -0.15f, 0f); cPiso.size = new Vector3(ancho, 0.3f, fondo);
        AgregarTeleport(goPiso);

        Portones(raiz);
        foreach (var (c, s) in colisiones)
        {
            var bc = raiz.AddComponent<BoxCollider>();
            bc.center = c; bc.size = s;
        }
        if (oficinas && fondo >= 30f) { Rampa(raiz, 1f); Rampa(raiz, -1f); }
        return raiz;
    }

    // Caja de min a max; con 'colision' también se agrega un BoxCollider.
    void Solido(ConstructorMalla m, Vector3 min, Vector3 max, bool colision = true)
    {
        var c = (min + max) * 0.5f; var s = max - min;
        if (s.x <= 0.001f || s.y <= 0.001f || s.z <= 0.001f) return;
        m.Caja(c, s);
        if (colision) colisiones.Add((c, s));
    }

    // Nervaduras verticales sobre una cara de muro. 'enX' = la cara mira hacia ±X (costados); si no, hacia ±Z.
    // 'a0..a1' es el tramo a lo largo del muro; 'arriba' da la altura superior en cada posición.
    void Nervios(bool enX, float plano, float signo, float a0, float a1, float y0, System.Func<float, float> arriba)
    {
        for (float p = Mathf.Floor(a0) + 0.5f; p < a1; p += Paso)
        {
            if (p - AnchoNervio * 0.5f < a0 || p + AnchoNervio * 0.5f > a1) continue;
            float y1 = arriba(p);
            if (y1 - y0 < 0.2f) continue;
            float n = plano + signo * ProfNervio * 0.5f;
            if (enX) lamina.Caja(new Vector3(n, (y0 + y1) * 0.5f, p), new Vector3(ProfNervio, y1 - y0, AnchoNervio));
            else lamina.Caja(new Vector3(p, (y0 + y1) * 0.5f, n), new Vector3(AnchoNervio, y1 - y0, ProfNervio));
        }
    }

    // Prisma recto a partir de un polígono convexo en XY, extruido de z0 a z1 (frontones y faldones del techo).
    static void Extruir(ConstructorMalla m, Vector2[] poli, float z0, float z1)
    {
        var c = Vector2.zero;
        foreach (var p in poli) c += p;
        c /= poli.Length;
        for (int i = 1; i < poli.Length - 1; i++)
        {
            m.Tri(new Vector3(poli[0].x, poli[0].y, z1), new Vector3(poli[i].x, poli[i].y, z1), new Vector3(poli[i + 1].x, poli[i + 1].y, z1), Vector3.forward);
            m.Tri(new Vector3(poli[0].x, poli[0].y, z0), new Vector3(poli[i].x, poli[i].y, z0), new Vector3(poli[i + 1].x, poli[i + 1].y, z0), Vector3.back);
        }
        for (int i = 0; i < poli.Length; i++)
        {
            Vector2 a = poli[i], b = poli[(i + 1) % poli.Length];
            var normal = new Vector2(b.y - a.y, a.x - b.x).normalized;
            if (Vector2.Dot(normal, (a + b) * 0.5f - c) < 0f) normal = -normal;
            m.Quad(new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1), new Vector3(normal.x, normal.y, 0f));
        }
    }

    // Marco con montantes y vidrio doble cara. 'h' = eje horizontal del muro, 'n' = normal hacia afuera.
    void Vidriera(Vector3 centro, Vector3 h, Vector3 n, float w, float alto, float separacion, int filas)
    {
        var v = Vector3.up;
        Vector3 ew = h * (w * 0.5f), ev = v * (alto * 0.5f);
        marcos.Viga(centro - ew - ev, centro + ew - ev, 0.18f);
        marcos.Viga(centro - ew + ev, centro + ew + ev, 0.18f);
        marcos.Viga(centro - ew - ev, centro - ew + ev, 0.18f);
        marcos.Viga(centro + ew - ev, centro + ew + ev, 0.18f);
        int columnas = Mathf.Max(1, Mathf.RoundToInt(w / separacion));
        for (int i = 1; i < columnas; i++)
        {
            var x = centro - ew + h * (w * i / columnas);
            marcos.Viga(x - ev, x + ev, 0.1f);
        }
        for (int j = 1; j < filas; j++)
        {
            var y = centro - ev + v * (alto * j / filas);
            marcos.Viga(y - ew, y + ew, 0.1f);
        }
        vidrio.Quad(centro - ew - ev, centro + ew - ev, centro + ew + ev, centro - ew + ev, n);
        vidrio.Quad(centro - ew - ev, centro + ew - ev, centro + ew + ev, centro - ew + ev, -n);
    }

    // Frontón inclinado (cuña) de un lado, entre el alero y donde empieza el techo plano.
    void Cunas(float z0, float z1)
    {
        Extruir(lamina, new[] { new Vector2(-hw, He), new Vector2(-hf, He), new Vector2(-hf, Hr) }, z0, z1);
        Extruir(lamina, new[] { new Vector2(hw, He), new Vector2(hf, Hr), new Vector2(hf, He) }, z0, z1);
    }

    void Fachada()
    {
        float z0 = hd - Muro, z1 = hd, wd = anchoPorton * 0.5f, yLintel = altoPorton + 1f, yVidrio = Hr - 1.5f;
        Solido(lamina, new Vector3(-hw, 0f, z0), new Vector3(-wd, He, z1));
        Solido(lamina, new Vector3(wd, 0f, z0), new Vector3(hw, He, z1));
        Solido(lamina, new Vector3(-wd, altoPorton, z0), new Vector3(wd, yLintel, z1));
        Solido(lamina, new Vector3(-wd, yVidrio, z0), new Vector3(wd, Hr, z1));
        Solido(lamina, new Vector3(-hf, He, z0), new Vector3(-wd, Hr, z1));
        Solido(lamina, new Vector3(wd, He, z0), new Vector3(hf, Hr, z1));
        Cunas(z0, z1);
        if (yVidrio - yLintel > 1f)
            Vidriera(new Vector3(0f, (yLintel + yVidrio) * 0.5f, hd - Muro * 0.5f), Vector3.right, Vector3.forward, anchoPorton, yVidrio - yLintel, 2.5f, 2);
        else Solido(lamina, new Vector3(-wd, yLintel, z0), new Vector3(wd, yVidrio, z1));   // sin espacio para vidrio: muro

        // Nervaduras, zócalo y franja (solo fuera del portón).
        Nervios(false, hd, 1f, -hw, -wd, Zocalo, x => x < -hf ? AlturaTecho(x) - 0.2f : Hr - 0.2f);
        Nervios(false, hd, 1f, wd, hw, Zocalo, x => x > hf ? AlturaTecho(x) - 0.2f : Hr - 0.2f);
        foreach (float s in new[] { -1f, 1f })
        {
            float xa = s < 0 ? -hw : wd, xb = s < 0 ? -wd : hw;
            Solido(zocalo, new Vector3(xa, 0f, hd), new Vector3(xb, Zocalo, hd + 0.08f), false);
            Solido(franja, new Vector3(xa, He - Franja, hd), new Vector3(xb, He, hd + 0.08f), false);
        }

        // Riel superior y rieles en el piso para los portones, puerta peatonal y lámparas.
        Solido(zocalo, new Vector3(-hw + 1f, altoPorton + 0.6f, hd), new Vector3(hw - 1f, altoPorton + 0.9f, hd + 0.9f), false);
        foreach (float zr in new[] { hd + 0.3f, hd + 0.65f })
            Solido(zocalo, new Vector3(-hw + 1f, 0f, zr - 0.05f), new Vector3(hw - 1f, 0.03f, zr + 0.05f), false);
        Solido(oscuro, new Vector3(hw - 3.6f, 0f, hd + 0.05f), new Vector3(hw - 2.4f, 2.2f, hd + 0.11f), false);
        foreach (float x in new[] { -(wd + 1f), wd + 1f })
        {
            estructura.Caja(new Vector3(x, altoPorton + 1.6f, hd + 0.25f), new Vector3(0.4f, 0.15f, 0.5f));
            lampara.Caja(new Vector3(x, altoPorton + 1.52f, hd + 0.3f), new Vector3(0.35f, 0.02f, 0.35f));
        }
    }

    // Muro del fondo con ventanas altas.
    void Fondo()
    {
        float z0 = -hd, z1 = -hd + Muro, yb = He - 3.2f, yt = He - 1.2f;
        int n = Mathf.Max(1, Mathf.FloorToInt((2f * hf - 4f) / 12f));
        var huecos = Huecos(n, 8f, 12f);
        Solido(lamina, new Vector3(-hw, 0f, z0), new Vector3(hw, yb, z1));
        Solido(lamina, new Vector3(-hw, yt, z0), new Vector3(hw, He, z1));
        float x = -hw;
        foreach (var (a, b) in huecos) { Solido(lamina, new Vector3(x, yb, z0), new Vector3(a, yt, z1)); x = b; }
        Solido(lamina, new Vector3(x, yb, z0), new Vector3(hw, yt, z1));
        Solido(lamina, new Vector3(-hf, He, z0), new Vector3(hf, Hr, z1));
        Cunas(z0, z1);
        foreach (var (a, b) in huecos)
            Vidriera(new Vector3((a + b) * 0.5f, (yb + yt) * 0.5f, -hd + Muro * 0.5f), Vector3.right, Vector3.back, b - a, yt - yb, 2f, 1);

        Nervios(false, -hd, -1f, -hw, hw, Zocalo, p => EnHueco(p, huecos) ? yb : AlturaTecho(p) - 0.2f);
        Nervios(false, -hd, -1f, -hw, hw, yt, p => EnHueco(p, huecos) ? He - Franja : yt);
        Solido(zocalo, new Vector3(-hw, 0f, -hd - 0.08f), new Vector3(hw, Zocalo, -hd), false);
        Solido(franja, new Vector3(-hw, He - Franja, -hd - 0.08f), new Vector3(hw, He, -hd), false);
    }

    // Costado (signo +1 = derecha, -1 = izquierda) con ventanas altas, bajantes, puerta y rejilla.
    void Costado(float s)
    {
        float x0 = s > 0 ? hw - Muro : -hw, x1 = s > 0 ? hw : -hw + Muro, xf = s * hw, yb = He - 3.2f, yt = He - 1.2f;
        int n = Mathf.Max(1, Mathf.FloorToInt((fondo - 6f) / 10f));
        var huecos = Huecos(n, 8f, 10f);
        Solido(lamina, new Vector3(x0, 0f, -hd), new Vector3(x1, yb, hd));
        Solido(lamina, new Vector3(x0, yt, -hd), new Vector3(x1, He, hd));
        float z = -hd;
        foreach (var (a, b) in huecos) { Solido(lamina, new Vector3(x0, yb, z), new Vector3(x1, yt, a)); z = b; }
        Solido(lamina, new Vector3(x0, yb, z), new Vector3(x1, yt, hd));
        foreach (var (a, b) in huecos)
            Vidriera(new Vector3(xf - s * Muro * 0.5f, (yb + yt) * 0.5f, (a + b) * 0.5f), Vector3.forward, Vector3.right * s, b - a, yt - yb, 2f, 1);

        Nervios(true, xf, s, -hd, hd, Zocalo, p => EnHueco(p, huecos) ? yb : He - Franja);
        Nervios(true, xf, s, -hd, hd, yt, p => EnHueco(p, huecos) ? He - Franja : yt);
        Solido(zocalo, new Vector3(s > 0 ? hw : -hw - 0.08f, 0f, -hd), new Vector3(s > 0 ? hw + 0.08f : -hw, Zocalo, hd), false);
        Solido(franja, new Vector3(s > 0 ? hw : -hw - 0.08f, He - Franja, -hd), new Vector3(s > 0 ? hw + 0.08f : -hw, He, hd), false);

        foreach (float zb in new[] { -hd + 0.4f, hd - 0.4f })
            zocalo.Tubo(new Vector3(xf + s * 0.15f, 0f, zb), new Vector3(xf + s * 0.15f, He - 0.2f, zb), 0.08f, 8);
        float zp = -hd + 6f;
        Solido(oscuro, new Vector3(s > 0 ? hw + 0.05f : -hw - 0.11f, 0f, zp - 0.6f), new Vector3(s > 0 ? hw + 0.11f : -hw - 0.05f, 2.2f, zp + 0.6f), false);
        Solido(zocalo, new Vector3(s > 0 ? hw : -hw - 0.9f, 2.5f, zp - 0.9f), new Vector3(s > 0 ? hw + 0.9f : -hw, 2.6f, zp + 0.9f), false);
        Solido(zocalo, new Vector3(s > 0 ? hw : -hw - 0.15f, 1.5f, -hd + 10f), new Vector3(s > 0 ? hw + 0.15f : -hw, 2.5f, -hd + 11.5f), false);
    }

    static List<(float, float)> Huecos(int n, float largo, float paso)
    {
        var l = new List<(float, float)>();
        for (int i = 0; i < n; i++)
        {
            float c = -(n - 1) * paso * 0.5f + i * paso;
            l.Add((c - largo * 0.5f, c + largo * 0.5f));
        }
        return l;
    }

    static bool EnHueco(float p, List<(float, float)> huecos)
    {
        foreach (var (a, b) in huecos) if (p > a - 0.2f && p < b + 0.2f) return true;
        return false;
    }

    // Techo: parte plana y dos faldones inclinados, con alero de 0.6 m.
    void Techo()
    {
        const float alero = 0.6f, grosor = 0.3f;
        float z0 = -hd - alero, z1 = hd + alero;
        float yAlero = He - (Hr - He) / (hw - hf) * alero;
        Extruir(techo, new[] { new Vector2(-hf, Hr), new Vector2(hf, Hr), new Vector2(hf, Hr + grosor), new Vector2(-hf, Hr + grosor) }, z0, z1);
        Extruir(techo, new[] { new Vector2(-hw - alero, yAlero), new Vector2(-hf, Hr), new Vector2(-hf, Hr + grosor), new Vector2(-hw - alero, yAlero + grosor) }, z0, z1);
        Extruir(techo, new[] { new Vector2(hw + alero, yAlero), new Vector2(hw + alero, yAlero + grosor), new Vector2(hf, Hr + grosor), new Vector2(hf, Hr) }, z0, z1);
        colisiones.Add((new Vector3(0f, Hr + grosor * 0.5f, 0f), new Vector3(2f * hf, grosor, z1 - z0)));
    }

    // Armaduras cada 7.5 m con cuerda inferior, cuerda superior siguiendo el techo y diagonales; lámparas colgadas.
    void Armaduras()
    {
        float yInf = He - 0.8f, xi = -hw + Muro, xo = hw - Muro;
        for (float z = -hd + 4f; z < hd - 3f; z += 7.5f)
        {
            estructura.Viga(new Vector3(xi, yInf, z), new Vector3(xo, yInf, z), 0.25f);
            var sup = new[] { new Vector3(xi, He - 0.35f, z), new Vector3(-hf, Hr - 0.35f, z), new Vector3(hf, Hr - 0.35f, z), new Vector3(xo, He - 0.35f, z) };
            for (int i = 0; i < 3; i++) estructura.Viga(sup[i], sup[i + 1], 0.25f);
            bool sube = true;
            for (float x = xi + 2.5f; x < xo - 0.1f; x += 2.5f)
            {
                float yS = AlturaTecho(x) - 0.35f, yPrev = AlturaTecho(x - 2.5f) - 0.35f;
                estructura.Viga(new Vector3(x, yInf, z), new Vector3(x, yS, z), 0.12f);
                if (sube) estructura.Viga(new Vector3(x - 2.5f, yInf, z), new Vector3(x, yS, z), 0.12f);
                else estructura.Viga(new Vector3(x - 2.5f, yPrev, z), new Vector3(x, yInf, z), 0.12f);
                sube = !sube;
            }
            foreach (float x in new[] { -12f, -4f, 4f, 12f })
            {
                if (Mathf.Abs(x) > hf - 2f) continue;
                estructura.Viga(new Vector3(x, yInf, z), new Vector3(x, yInf - 1.5f, z), 0.04f);
                estructura.Prisma(new Vector3(x, yInf - 1.8f, z), 0.55f, 0.2f, 0.3f, 12, false, true);
                lampara.Prisma(new Vector3(x, yInf - 1.82f, z), 0.5f, 0.5f, 0.02f, 12, false, true);
            }
        }
    }

    // Oficinas de dos niveles junto a un costado, con pasillo-tapanco, barandal, columnas y escalera al frente.
    float ZFinOficinas => hd - 14f;

    void Oficinas(float s)
    {
        const float prof = 7f, pasillo = 1.2f, hPlanta = 3.5f, losa = 0.3f, hAlta = 3f;
        float xi = s * (hw - Muro), xo = s * (hw - prof), xp = s * (hw - prof - pasillo);
        float za = -hd + Muro, zb = ZFinOficinas, yLosa = hPlanta + losa;
        Solido(oficina, Min(xi, xo, 0f, za), Max(xi, xo, hPlanta, zb));
        Solido(zocalo, Min(xi, xp, hPlanta, za), Max(xi, xp, yLosa, zb));
        Solido(oficina, Min(xi, xo + s * 0.5f, yLosa, za), Max(xi, xo + s * 0.5f, yLosa + hAlta, zb));

        float xFrente = xo - s * 0.03f, xFrenteAlto = xo + s * 0.47f;
        int k = 0;
        for (float z = za + 2f; z < zb - 1.5f; z += 3f, k++)
        {
            if (k % 3 == 1) Solido(oscuro, Min(xFrente, xFrente - s * 0.06f, 0f, z - 0.5f), Max(xFrente, xFrente - s * 0.06f, 2.2f, z + 0.5f), false);
            else Solido(ventana, Min(xFrente, xFrente - s * 0.06f, 1f, z - 0.8f), Max(xFrente, xFrente - s * 0.06f, 2.2f, z + 0.8f), false);
            Solido(ventana, Min(xFrenteAlto, xFrenteAlto - s * 0.06f, yLosa + 1f, z - 0.9f), Max(xFrenteAlto, xFrenteAlto - s * 0.06f, yLosa + 2.2f, z + 0.9f), false);
        }
        float xb = xp + s * 0.05f;
        for (float z = za + 1f; z <= zb; z += 6f)
            estructura.Viga(new Vector3(xb, 0f, z), new Vector3(xb, hPlanta, z), 0.2f);
        int postes = Mathf.Max(2, Mathf.CeilToInt((zb - za) / 1.5f) + 1);
        for (int i = 0; i < postes; i++)
        {
            float z = Mathf.Lerp(za, zb, i / (float)(postes - 1));
            estructura.Viga(new Vector3(xb, yLosa, z), new Vector3(xb, yLosa + 1f, z), 0.05f);
        }
        estructura.Viga(new Vector3(xb, yLosa + 1f, za), new Vector3(xb, yLosa + 1f, zb), 0.06f);
        estructura.Viga(new Vector3(xb, yLosa + 0.5f, za), new Vector3(xb, yLosa + 0.5f, zb), 0.04f);

        // Escalera recta desde el piso (hacia el portón) hasta el borde del tapanco.
        const float huella = 0.28f, peralte = 0.2f;
        int escalones = Mathf.RoundToInt(yLosa / peralte);
        for (int i = 0; i < escalones; i++)
        {
            float top = yLosa - i * peralte;
            Solido(estructura, Min(xo, xp, 0f, zb + i * huella), Max(xo, xp, top, zb + (i + 1) * huella), false);
        }
        estructura.Viga(new Vector3(xp, yLosa + 1f, zb), new Vector3(xp, peralte + 1f, zb + escalones * huella), 0.05f);
    }

    // Collider inclinado sobre la escalera para poder subir caminando.
    void Rampa(GameObject raiz, float s)
    {
        float yLosa = 3.8f, largo = Mathf.RoundToInt(yLosa / 0.2f) * 0.28f, xc = s * (hw - 7.6f);
        var go = new GameObject(s > 0 ? "RampaEscaleraDer" : "RampaEscaleraIzq");
        go.transform.SetParent(raiz.transform, false);
        go.transform.localPosition = new Vector3(xc, yLosa * 0.5f, ZFinOficinas + largo * 0.5f);
        go.transform.localRotation = Quaternion.Euler(Mathf.Atan2(yLosa, largo) * Mathf.Rad2Deg, 0f, 0f);
        var bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.2f, 0.1f, Mathf.Sqrt(largo * largo + yLosa * yLosa));
    }

    static Vector3 Min(float xa, float xb, float y, float z) => new Vector3(Mathf.Min(xa, xb), y, z);
    static Vector3 Max(float xa, float xb, float y, float z) => new Vector3(Mathf.Max(xa, xb), y, z);

    // Taller junto al muro del fondo: racks, banco, gabinetes, cajas y tambos.
    void Taller()
    {
        float z = -hd + Muro + 0.6f, limite = hw - (oficinas ? 8.5f : 2f);
        if (limite < 8f) return;
        foreach (float xc in new[] { -limite + 1.6f, -limite + 5f })
        {
            for (int i = 0; i < 4; i++)
            {
                float x = xc + (i % 2 == 0 ? -1.5f : 1.5f), zz = z + (i < 2 ? -0.45f : 0.45f);
                estructura.Viga(new Vector3(x, 0f, zz), new Vector3(x, 3f, zz), 0.08f);
            }
            for (int nivel = 0; nivel < 3; nivel++)
            {
                float y = 0.15f + nivel * 1.1f;
                zocalo.Caja(new Vector3(xc, y, z), new Vector3(3f, 0.06f, 0.9f));
                cajas.Caja(new Vector3(xc - 0.8f, y + 0.25f, z), new Vector3(0.9f, 0.45f, 0.7f));
                cajas.Caja(new Vector3(xc + 0.6f, y + 0.2f + nivel * 0.05f, z), new Vector3(1.1f, 0.35f + nivel * 0.1f, 0.6f));
            }
        }
        madera.Caja(new Vector3(0f, 0.9f, z + 0.1f), new Vector3(2.5f, 0.08f, 0.8f));
        foreach (float dx in new[] { -1.15f, 1.15f })
            estructura.Viga(new Vector3(dx, 0f, z + 0.1f), new Vector3(dx, 0.86f, z + 0.1f), 0.08f);
        estructura.Caja(new Vector3(0.9f, 1f, z + 0.1f), new Vector3(0.2f, 0.15f, 0.15f));
        rojo.Caja(new Vector3(2.4f, 0.6f, z), new Vector3(0.9f, 1.2f, 0.6f));
        rojo.Caja(new Vector3(3.4f, 0.6f, z), new Vector3(0.9f, 1.2f, 0.6f));
        float xCajas = limite - 2f;
        cajas.Caja(new Vector3(xCajas, 0.6f, z + 0.2f), Vector3.one * 1.2f);
        cajas.Caja(new Vector3(xCajas - 1.3f, 0.6f, z + 0.2f), Vector3.one * 1.2f);
        cajas.Caja(new Vector3(xCajas - 0.65f, 1.8f, z + 0.2f), Vector3.one * 1.2f);
        rojo.Prisma(new Vector3(xCajas - 3f, 0f, z + 0.2f), 0.3f, 0.3f, 0.9f, 12, true, true);
        rojo.Prisma(new Vector3(xCajas - 3.7f, 0f, z + 0.2f), 0.3f, 0.3f, 0.9f, 12, true, true);
        foreach (float x in new[] { -(anchoPorton * 0.5f + 1f), anchoPorton * 0.5f + 1f })
            rojo.Prisma(new Vector3(x, 0f, hd - 1f), 0.09f, 0.09f, 0.5f, 10, true, true);
    }

    // Círculo, línea guía con barra de alto y franja amarilla/negra en la entrada.
    void MarcasPiso()
    {
        const float y = 0.012f, w = 0.35f;
        float libreX = hw - (oficinas ? 8.5f : 2f);
        float r = Mathf.Min(10f, libreX - 1f, hd - 2f);
        for (int i = 0; i < 72; i++)
        {
            float a0 = i * Mathf.PI * 2f / 72f, a1 = (i + 1) * Mathf.PI * 2f / 72f;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            amarillo.Quad(d0 * (r - w * 0.5f) + Vector3.up * y, d0 * (r + w * 0.5f) + Vector3.up * y,
                          d1 * (r + w * 0.5f) + Vector3.up * y, d1 * (r - w * 0.5f) + Vector3.up * y, Vector3.up);
        }
        float wd = anchoPorton * 0.5f;
        amarillo.Caja(new Vector3(0f, y, (hd - 1.2f) * 0.5f), new Vector3(w, 0.004f, hd - 1.2f));
        amarillo.Caja(new Vector3(0f, y, 0f), new Vector3(3f, 0.004f, w));
        amarillo.Caja(new Vector3(0f, y, hd - 0.6f), new Vector3(anchoPorton - 2f, 0.004f, 0.6f));
        for (float x = -wd + 1f; x + 0.6f <= wd - 1f; x += 1.2f)
            negro.Quad(new Vector3(x, y + 0.004f, hd - 0.3f), new Vector3(x + 0.6f, y + 0.004f, hd - 0.3f),
                       new Vector3(x + 1f, y + 0.004f, hd - 0.9f), new Vector3(x + 0.4f, y + 0.004f, hd - 0.9f), Vector3.up);
    }

    // 4 portones corredizos (objetos propios para poder animarlos después); 2 rieles por lado.
    void Portones(GameObject raiz)
    {
        float wd = anchoPorton * 0.5f, lw = anchoPorton / 4f + 0.3f, hl = altoPorton + 0.6f, g = 0.25f;
        var hoja = new ConstructorMalla(); var base_ = new ConstructorMalla();
        hoja.Caja(new Vector3(0f, hl * 0.5f, 0f), new Vector3(lw, hl, g));
        for (float x = -lw * 0.5f + 0.25f; x < lw * 0.5f - 0.1f; x += 0.5f)
            foreach (float s in new[] { -1f, 1f })
                hoja.Caja(new Vector3(x, (hl + 0.8f) * 0.5f, s * (g * 0.5f + 0.025f)), new Vector3(0.12f, hl - 0.8f, 0.05f));
        base_.Caja(new Vector3(0f, 0.4f, 0f), new Vector3(lw + 0.02f, 0.8f, g + 0.12f));
        var mHoja = UtilGeneradores.GuardarMalla(hoja.CrearMesh($"{nombre}_Porton"));
        var mBase = UtilGeneradores.GuardarMalla(base_.CrearMesh($"{nombre}_PortonBase"));
        var matHoja = Mat("Hangar_Porton", new Color(0.76f, 0.8f, 0.82f), 0.45f, 0.5f);
        var matBase = Mat("Hangar_Zocalo", new Color(0.43f, 0.47f, 0.5f), 0.35f, 0.4f);

        for (int k = 0; k < 4; k++)
        {
            bool exterior = k == 0 || k == 3, izquierda = k < 2;
            float cerrado = -wd + anchoPorton / 8f + k * anchoPorton / 4f;
            float abierto = (izquierda ? -1f : 1f) * (wd + lw * 0.5f + 0.3f);
            var go = new GameObject($"Porton_{k + 1}");
            go.transform.SetParent(raiz.transform, false);
            go.transform.localPosition = new Vector3(Mathf.Lerp(cerrado, abierto, apertura), 0.03f, hd + (exterior ? 0.65f : 0.3f));
            go.AddComponent<MeshFilter>().sharedMesh = mHoja;
            go.AddComponent<MeshRenderer>().sharedMaterial = matHoja;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, hl * 0.5f, 0f); bc.size = new Vector3(lw, hl, g);
            var b = new GameObject("Base");
            b.transform.SetParent(go.transform, false);
            b.AddComponent<MeshFilter>().sharedMesh = mBase;
            b.AddComponent<MeshRenderer>().sharedMaterial = matBase;
        }
    }

    // ---------- Utilidades ----------
    GameObject Hijo(GameObject raiz, string parte, ConstructorMalla m, Material mat, bool estatico = true)
    {
        var go = new GameObject(parte);
        go.transform.SetParent(raiz.transform, false);
        if (!m.Vacio)
        {
            go.AddComponent<MeshFilter>().sharedMesh = UtilGeneradores.GuardarMalla(m.CrearMesh($"{nombre}_{parte}"));
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!estatico) mr.shadowCastingMode = ShadowCastingMode.Off;
        }
        if (estatico) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // Área de teletransporte de XR Interaction Toolkit, si está en el proyecto (se busca por nombre para no depender de la versión).
    static void AgregarTeleport(GameObject go)
    {
        foreach (var tipo in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            if (tipo.Name == "TeleportationArea" && !tipo.IsAbstract && !tipo.IsDefined(typeof(System.ObsoleteAttribute), false))
            { go.AddComponent(tipo); return; }
        Debug.LogWarning("[Hangar] No encontré TeleportationArea; el piso queda sin teletransporte.");
    }

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

    static Material MatEmisivo(string nombreMat, Color color, float intensidad)
    {
        var m = Mat(nombreMat, color, 0.6f, 0f);
        if (m == null) return null;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        m.SetColor("_EmissionColor", color * intensidad);
        EditorUtility.SetDirty(m);
        return m;
    }
}
#endif
