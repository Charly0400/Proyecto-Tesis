// Palanca de vuelo y acelerador del P-47 para VR (escala real, 1 unidad = 1 m), listos para JoystickXRV2 y ThrottleXRV2.
// Palanca: placa y fuelle de cuero fijos; 'Pivote' (el handle del JoystickXRV2) con la vara y una empuñadura tipo
// pistola de 12 cm inclinada 10° hacia adelante, con ranuras para los dedos, gatillo (dispara: AircraftGun ya escucha
// 'activated') y botón de pulgar (B / Y recentra la vista con CockpitViewRecenter). PalancaBotonesVisual los anima.
// Acelerador: cuadrante con riel recto de 15 cm y marcas; 'Palanca' (el handle del ThrottleXRV2) se desliza en Z local,
// adelante = más potencia, y se queda donde se suelta. Colliders amplios para agarrarlos sin apuntar exacto.
// Con un P-47 generado como 'Avión' se colocan en su cabina (palanca al frente del asiento, acelerador en la consola
// izquierda). Se guardan como prefabs con sus mallas como assets. Menú: Tools > Controles P-47 (palanca y acelerador)
#if UNITY_EDITOR
using MikeNspired.XRIStarterKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Malla = GeneradorP47.Malla;

public class GeneradorControlesP47 : EditorWindow
{
    const string CarpetaPrefabs = "Assets/VR_FlightSimulato_Proy/Prefabs/Generado";
    const string CarpetaMateriales = "Assets/VR_FlightSimulato_Proy/Materials/P47";
    const string RutaPalanca = CarpetaPrefabs + "/P47_Palanca.prefab", RutaAcelerador = CarpetaPrefabs + "/P47_Acelerador.prefab";
    const float Recorrido = 0.075f;   // mitad del riel del acelerador

    [SerializeField] Transform avion;

    [MenuItem("Tools/Controles P-47 (palanca y acelerador)")]
    static void Abrir() => GetWindow<GeneradorControlesP47>("Controles P-47");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Crea la palanca de vuelo (JoystickXRV2) y el acelerador (ThrottleXRV2) del P-47. Con 'Avión' (el P-47 " +
            "generado) quedan como hijos suyos en la cabina; si no, frente a la cámara de la escena. Falta conectar sus eventos " +
            "como en tu Thunderbolt V3: OnJoystickMove / OnYawInput de la palanca y On Value Change del acelerador. Si a la palanca " +
            "le copias ReleaseHandAtDistance y XRHandPoser del V3: 'distance' de 1 (mide desde la base y la empuñadura está a " +
            "0.57 m) y los attach points dentro de la Empunadura.", MessageType.Info);
        avion = (Transform)EditorGUILayout.ObjectField("Avión (opcional)", avion, typeof(Transform), true);
        EditorGUILayout.Space();
        if (GUILayout.Button("Generar palanca y acelerador", GUILayout.Height(32))) Generar();
    }

    void Generar()
    {
        int grupo = Undo.GetCurrentGroup();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
            {
                string ruta = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if (ruta == RutaPalanca || ruta == RutaAcelerador) Undo.DestroyObjectImmediate(t.gameObject);
            }
        GeneradorMarcasPista.CrearCarpeta(CarpetaPrefabs);
        Vector3 frente = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var palanca = ConstruirPalanca(true);
        Colocar(palanca, GeneradorP47.P(4.38f, 0f, -0.30f), frente);
        var acelerador = ConstruirAcelerador(true);
        Colocar(acelerador, GeneradorP47.P(4.30f, -0.44f, 0.13f), frente + Vector3.left * 0.4f);
        foreach (var (go, ruta) in new[] { (palanca, RutaPalanca), (acelerador, RutaAcelerador) })
        {
            Undo.RegisterCreatedObjectUndo(go, "Generar controles P-47");
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, ruta, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
        Undo.CollapseUndoOperations(grupo);
        Selection.objects = new Object[] { palanca, acelerador };
        Debug.Log("[Controles P-47] Palanca y acelerador generados" + (avion != null ? $" en la cabina de '{avion.name}'" : ""));
    }

    void Colocar(GameObject go, Vector3 enCabina, Vector3 enEscena)
    {
        if (avion != null)
        {
            go.transform.SetParent(avion, false);
            go.transform.localPosition = enCabina;
            go.transform.localRotation = Quaternion.identity;
        }
        else go.transform.position = enEscena;
    }

    // ---------- Palanca ----------
    public static GameObject ConstruirPalanca(bool guardar)
    {
        Material metal = Mat("P47C_Metal", new Color(0.30f, 0.30f, 0.31f), 0.8f, 0.5f, guardar),
                 metalOscuro = Mat("P47C_MetalOscuro", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.4f, guardar),
                 cuero = Mat("P47C_Cuero", new Color(0.20f, 0.13f, 0.08f), 0f, 0.25f, guardar),
                 baquelita = Mat("P47C_Baquelita", new Color(0.05f, 0.045f, 0.04f), 0f, 0.55f, guardar),
                 rojo = Mat("P47C_Rojo", new Color(0.70f, 0.08f, 0.06f), 0f, 0.5f, guardar);

        var raiz = new GameObject("P47_Palanca");
        // Base fija: placa atornillada al piso y fuelle de cuero
        var placa = Hijo(raiz.transform, "Base_Placa", Vector3.zero, Quaternion.identity);
        var mp = new Malla();
        mp.Tubo(Vector3.zero, Vector3.up * 0.008f, 0.095f, 0.095f, 28);
        for (int k = 0; k < 4; k++)
        {
            float a = Mathf.PI * 0.5f * k + Mathf.PI * 0.25f;
            var c = new Vector3(Mathf.Cos(a) * 0.078f, 0.008f, Mathf.Sin(a) * 0.078f);
            mp.Tubo(c, c + Vector3.up * 0.004f, 0.006f, 0.006f, 8);
        }
        Poner(placa, mp, metalOscuro, guardar);
        var fuelle = Hijo(raiz.transform, "Base_Fuelle", Vector3.zero, Quaternion.identity);
        float[] fy = { 0.008f, 0.022f, 0.034f, 0.046f, 0.058f, 0.070f, 0.082f, 0.092f, 0.102f, 0.110f };
        float[] fr = { 0.072f, 0.074f, 0.058f, 0.062f, 0.046f, 0.049f, 0.034f, 0.036f, 0.022f, 0.016f };
        var mf = new Malla();
        mf.Rejilla(Anillos(fy, i => fr[i], i => fr[i], 24), null, i => new Vector3(0f, fy[i], 0f), costura: true);
        Poner(fuelle, mf, cuero, guardar);

        // Parte móvil: gira en el pivote (origen) con el JoystickXRV2
        var pivote = Hijo(raiz.transform, "Pivote", Vector3.zero, Quaternion.identity);
        var vara = Hijo(pivote.transform, "Vara", Vector3.zero, Quaternion.identity);
        var mv = new Malla();
        mv.Tubo(Vector3.zero, Vector3.up * 0.50f, 0.0125f, 0.0125f, 14);
        mv.Tubo(Vector3.up * 0.487f, Vector3.up * 0.507f, 0.017f, 0.017f, 16);
        Poner(vara, mv, metal, guardar);

        // Empuñadura tipo pistola: sección ovalada, tope para el meñique abajo, ranuras para los dedos al frente
        var agarre = Hijo(pivote.transform, "Empunadura", Vector3.up * 0.505f, Quaternion.Euler(10f, 0f, 0f));
        float[] gy = { -0.004f, 0f, 0.010f, 0.017f, 0.030f, 0.045f, 0.060f, 0.075f, 0.090f, 0.102f, 0.112f, 0.120f, 0.1255f };
        float[] ga = { 0.014f, 0.024f, 0.024f, 0.0170f, 0.0165f, 0.0168f, 0.0172f, 0.0175f, 0.0170f, 0.0163f, 0.0148f, 0.0110f, 0f };
        float[] gb = { 0.016f, 0.027f, 0.027f, 0.0200f, 0.0205f, 0.0210f, 0.0212f, 0.0212f, 0.0205f, 0.0195f, 0.0175f, 0.0130f, 0f };
        var mg = new Malla();
        mg.Rejilla(Anillos(gy, i => ga[i], i => gb[i], 28, (y, frente) =>
            frente && y > 0.018f && y < 0.092f ? 1f - 0.07f * Mathf.Abs(Mathf.Sin((y - 0.018f) * Mathf.PI / 0.0247f)) : 1f),
            null, i => new Vector3(0f, gy[i], 0f), costura: true);
        Poner(agarre, mg, baquelita, guardar);
        var col = agarre.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.06f, 0.005f);
        col.radius = 0.045f;
        col.height = 0.20f;

        // Botón de pulgar arriba, inclinado hacia el piloto
        var boton = Hijo(agarre.transform, "Boton", new Vector3(0f, 0.117f, -0.006f), Quaternion.Euler(-25f, 0f, 0f));
        var mb = new Malla();
        mb.Tubo(Vector3.down * 0.004f, Vector3.up * 0.0065f, 0.0075f, 0.0072f, 16);
        Poner(boton, mb, rojo, guardar);

        // Gatillo al frente para el índice (gira sobre su borde de arriba)
        var gatillo = Hijo(agarre.transform, "Gatillo", new Vector3(0f, 0.086f, 0.016f), Quaternion.identity);
        var mt = new Malla();
        mt.TuboCurvo(new[] { new Vector3(0f, 0f, 0.002f), new Vector3(0f, -0.006f, 0.014f), new Vector3(0f, -0.016f, 0.021f),
                             new Vector3(0f, -0.027f, 0.021f), new Vector3(0f, -0.035f, 0.015f) }, 0.0055f, 10);
        Poner(gatillo, mt, metal, guardar);

        var js = raiz.AddComponent<JoystickXRV2>();
        var so = new SerializedObject(js);
        so.FindProperty("handle").objectReferenceValue = pivote.transform;
        // Como el Thunderbolt V3: 'sensitivity' es input por metro de mano y con 7 el tope llega a ~14 cm (el 0.3 por
        // defecto pedía 3 m). maxAngle es solo visual: así la empuñadura, a ~0.57 m del pivote, recorre lo mismo que la mano.
        const float sensibilidad = 7f, alturaAgarre = 0.57f;
        so.FindProperty("sensitivity").floatValue = sensibilidad;
        so.FindProperty("maxAngle").floatValue = Mathf.Asin(1f / (sensibilidad * alturaAgarre)) * Mathf.Rad2Deg;   // ≈ 14.5°
        so.ApplyModifiedPropertiesWithoutUndo();
        var vis = raiz.AddComponent<PalancaBotonesVisual>();
        so = new SerializedObject(vis);
        so.FindProperty("palanca").objectReferenceValue = js;
        so.FindProperty("gatillo").objectReferenceValue = gatillo.transform;
        so.FindProperty("boton").objectReferenceValue = boton.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ---------- Acelerador ----------
    public static GameObject ConstruirAcelerador(bool guardar)
    {
        Material metal = Mat("P47C_Metal", new Color(0.30f, 0.30f, 0.31f), 0.8f, 0.5f, guardar),
                 metalOscuro = Mat("P47C_MetalOscuro", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.4f, guardar),
                 baquelita = Mat("P47C_Baquelita", new Color(0.05f, 0.045f, 0.04f), 0f, 0.55f, guardar),
                 marcas = Mat("P47C_Marcas", new Color(0.85f, 0.82f, 0.70f), 0f, 0.3f, guardar);

        var raiz = new GameObject("P47_Acelerador");
        // Cuadrante: base, dos rieles con la ranura en medio y topes en los extremos
        var cuadrante = Hijo(raiz.transform, "Cuadrante", Vector3.zero, Quaternion.identity);
        var mc = new Malla();
        mc.Caja(new Vector3(0f, 0.003f, 0f), new Vector3(0.08f, 0.006f, 0.26f), Quaternion.identity);
        foreach (float x in new[] { -0.021f, 0.021f })
            mc.Caja(new Vector3(x, 0.028f, 0f), new Vector3(0.022f, 0.044f, 0.24f), Quaternion.identity);
        foreach (float z in new[] { -0.114f, 0.114f })
            mc.Caja(new Vector3(0f, 0.028f, z), new Vector3(0.064f, 0.044f, 0.012f), Quaternion.identity);
        Poner(cuadrante, mc, metalOscuro, guardar);
        // Marcas del recorrido sobre el riel de afuera (las de los extremos más largas)
        var marcasGo = Hijo(raiz.transform, "Marcas", Vector3.zero, Quaternion.identity);
        var mm = new Malla();
        for (int k = 0; k <= 4; k++)
        {
            float z = Mathf.Lerp(-Recorrido, Recorrido, k / 4f);
            bool extremo = k == 0 || k == 4;
            mm.Caja(new Vector3(-0.026f, 0.0505f, z), new Vector3(extremo ? 0.012f : 0.007f, 0.001f, 0.003f), Quaternion.identity);
        }
        Poner(marcasGo, mm, marcas, guardar);

        // Palanca: se desliza por la ranura (Z local) con el ThrottleXRV2
        var palanca = Hijo(raiz.transform, "Palanca", new Vector3(0f, 0f, -Recorrido), Quaternion.identity);
        var mb = new Malla();
        mb.Caja(new Vector3(0f, 0.032f, 0f), new Vector3(0.016f, 0.03f, 0.03f), Quaternion.identity);
        mb.Tubo(new Vector3(0f, 0.045f, 0f), new Vector3(0f, 0.132f, -0.012f), 0.0075f, 0.0065f, 12);
        Poner(Hijo(palanca.transform, "Brazo", palanca.transform.localPosition, Quaternion.identity, true), mb, metal, guardar);
        var mpo = new Malla();
        mpo.Elipsoide(new Vector3(0f, 0.142f, -0.014f), new Vector3(0.022f, 0.02f, 0.024f), Quaternion.identity, 12, 20);
        Poner(Hijo(palanca.transform, "Pomo", palanca.transform.localPosition, Quaternion.identity, true), mpo, baquelita, guardar);
        var esfera = palanca.AddComponent<SphereCollider>();
        esfera.center = new Vector3(0f, 0.125f, -0.01f);
        esfera.radius = 0.055f;

        var th = raiz.AddComponent<ThrottleXRV2>();
        var so = new SerializedObject(th);
        so.FindProperty("m_Handle").objectReferenceValue = palanca.transform;
        so.FindProperty("m_MinPosition").floatValue = -Recorrido;   // ralentí atrás
        so.FindProperty("m_MaxPosition").floatValue = Recorrido;    // potencia máxima adelante
        so.FindProperty("m_Value").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ---------- Utilidades ----------
    // Anillos ovalados alrededor del eje Y (semiejes a en X, b en Z); 'escalaZ' deforma el lado de adelante (+Z).
    static Vector3[,] Anillos(float[] ys, System.Func<int, float> a, System.Func<int, float> b, int lados,
                              System.Func<float, bool, float> escalaZ = null)
    {
        var P = new Vector3[ys.Length, lados + 1];
        for (int i = 0; i < ys.Length; i++)
            for (int k = 0; k <= lados; k++)
            {
                float ang = Mathf.PI * 2f * k / lados, s = Mathf.Sin(ang);
                float z = b(i) * s * (escalaZ != null ? escalaZ(ys[i], s > 0f) : 1f);
                P[i, k] = new Vector3(a(i) * Mathf.Cos(ang), ys[i], z);
            }
        return P;
    }

    // Hijo con pose local. Con 'mismaPose' la geometría (hecha en ejes del padre) no se desplaza.
    static GameObject Hijo(Transform padre, string nombre, Vector3 posLocal, Quaternion rotLocal, bool mismaPose = false)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        if (!mismaPose) go.transform.SetLocalPositionAndRotation(posLocal, rotLocal);
        return go;
    }

    static void Poner(GameObject go, Malla m, Material mat, bool guardar)
    {
        var mesh = m.CrearMesh($"P47C_{go.transform.parent?.name}_{go.name}");
        go.AddComponent<MeshFilter>().sharedMesh = guardar ? UtilGeneradores.GuardarMalla(mesh) : mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material Mat(string nombre, Color color, float metalico, float lisura, bool guardar)
    {
        string ruta = $"{CarpetaMateriales}/{nombre}.mat";
        var m = guardar ? AssetDatabase.LoadAssetAtPath<Material>(ruta) : null;
        bool nuevo = m == null;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metalico);
        m.SetFloat("_Smoothness", lisura);
        if (!guardar) return m;
        if (nuevo) { GeneradorMarcasPista.CrearCarpeta(CarpetaMateriales); AssetDatabase.CreateAsset(m, ruta); }
        else EditorUtility.SetDirty(m);
        return m;
    }
}
#endif
