// Palanca de vuelo y acelerador del P-47 para VR (escala real, 1 unidad = 1 m), listos para JoystickXRV2 y ThrottleXRV2.
// Palanca: placa y fuelle de cuero fijos; 'Pivote' (el handle del JoystickXRV2) con la vara y una empuñadura tipo
// pistola de 12 cm inclinada 10° hacia adelante, con ranuras para los dedos, gatillo (dispara: AircraftGun ya escucha
// 'activated') y botón de pulgar (B / Y recentra la vista con CockpitViewRecenter). PalancaBotonesVisual los anima.
// Acelerador: cuadrante con riel recto de 15 cm y marcas; 'Palanca' (el handle del ThrottleXRV2) se desliza en Z local,
// adelante = más potencia, y se queda donde se suelta. Colliders amplios para agarrarlos sin apuntar exacto.
// Switch de encendido (XRSwitch) con guarda roja que se levanta al acercar la mano (GuardaSwitchVisual) y palanca del
// tren (XRSwitch, encendido = tren abajo) con perilla en forma de llanta y luces verde/roja (PalancaTren la conecta
// con el LandingGearAnimator del avión).
// Con un P-47 generado como 'Avión' se colocan en su cabina (palanca al frente del asiento, acelerador en la consola
// izquierda, switch y tren en el lado izquierdo del tablero). Se guardan como prefabs con sus mallas como assets.
// Menú: Tools > Controles P-47 (palanca y acelerador)
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
    const string RutaPalanca = CarpetaPrefabs + "/P47_Palanca.prefab", RutaAcelerador = CarpetaPrefabs + "/P47_Acelerador.prefab",
                 RutaSwitch = CarpetaPrefabs + "/P47_SwitchEncendido.prefab", RutaTren = CarpetaPrefabs + "/P47_PalancaTren.prefab",
                 RutaAviso = CarpetaPrefabs + "/P47_AvisoPerdida.prefab",
                 RutaReset = CarpetaPrefabs + "/P47_BotonReset.prefab", RutaReaparecer = CarpetaPrefabs + "/P47_BotonReaparecer.prefab";
    // Switch y tren van en la cara trasera del tablero (x = 3.875): su eje Y local mira al piloto y su Z local hacia arriba
    static readonly Quaternion EnTablero = Quaternion.Euler(-90f, 0f, 0f);
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
            "0.57 m) y los attach points dentro de la Empunadura. Al XRHandPoser del acelerador asígnale los attach points de " +
            "Palanca/GrabAttachPoints (no los del ThrottleXR viejo: no se mueven y la mano no sigue a la palanca). " +
            "El switch de encendido (XRSwitch) va en el tablero: conecta su On Switch On / Off al motor como en el V3. " +
            "La palanca del tren ya mueve el LandingGearAnimator del avión y prende sus luces sola. El aviso de pérdida (barra " +
            "ámbar en la visera, vibración de la palanca y pitido) lee AircraftPhysics solo. Los botones de reaparecer (casa) y " +
            "reiniciar el nivel (flecha) se mantienen agarrados 2 s y no hay que conectarlos; reemplazan al botón de reset viejo.", MessageType.Info);
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
                if (ruta == RutaPalanca || ruta == RutaAcelerador || ruta == RutaSwitch || ruta == RutaTren || ruta == RutaAviso ||
                    ruta == RutaReset || ruta == RutaReaparecer)
                    Undo.DestroyObjectImmediate(t.gameObject);
            }
        GeneradorMarcasPista.CrearCarpeta(CarpetaPrefabs);
        Vector3 frente = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var palanca = ConstruirPalanca(true);
        Colocar(palanca, GeneradorP47.P(4.38f, 0f, -0.30f), Quaternion.identity, frente);
        var acelerador = ConstruirAcelerador(true);
        Colocar(acelerador, GeneradorP47.P(4.30f, -0.44f, 0.13f), Quaternion.identity, frente + Vector3.left * 0.4f);
        var interruptor = ConstruirSwitch(true);
        Colocar(interruptor, GeneradorP47.P(3.876f, -0.27f, 0.12f), EnTablero, frente + Vector3.right * 0.4f);
        var tren = ConstruirPalancaTren(true);
        Colocar(tren, GeneradorP47.P(3.876f, -0.42f, 0.27f), EnTablero, frente + Vector3.right * 0.8f);
        // Aviso de pérdida: en el labio de la visera, a la vista bajo la mira K-14
        var aviso = ConstruirAvisoPerdida(true);
        Colocar(aviso, GeneradorP47.PuntoFus(3.985f, Mathf.PI * 0.5f, 0.94f) + Vector3.down * 0.035f, EnTablero, frente + Vector3.up * 0.3f);
        // Botones de mantener presionado en el lado derecho del tablero: reaparecer más a la mano, reiniciar más lejos
        var reaparecer = ConstruirBotonMantener("P47_BotonReaparecer", BotonMantener.Accion.ReaparecerEnBase, true);
        Colocar(reaparecer, GeneradorP47.P(3.876f, 0.27f, 0.12f), EnTablero, frente + Vector3.right * 1.2f);
        var reset = ConstruirBotonMantener("P47_BotonReset", BotonMantener.Accion.ReiniciarNivel, true);
        Colocar(reset, GeneradorP47.P(3.876f, 0.39f, 0.12f), EnTablero, frente + Vector3.right * 1.4f);
        foreach (var (go, ruta) in new[] { (palanca, RutaPalanca), (acelerador, RutaAcelerador), (interruptor, RutaSwitch), (tren, RutaTren),
                                           (aviso, RutaAviso), (reaparecer, RutaReaparecer), (reset, RutaReset) })
        {
            Undo.RegisterCreatedObjectUndo(go, "Generar controles P-47");
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, ruta, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
        Undo.CollapseUndoOperations(grupo); 
        Selection.objects = new Object[] { palanca, acelerador, interruptor, tren, aviso, reaparecer, reset };
        Debug.Log("[Controles P-47] Palanca, acelerador, switch y palanca del tren generados" + (avion != null ? $" en la cabina de '{avion.name}'" : ""));
    }

    void Colocar(GameObject go, Vector3 enCabina, Quaternion rotCabina, Vector3 enEscena)
    {
        if (avion != null)
        {
            go.transform.SetParent(avion, false);
            go.transform.SetLocalPositionAndRotation(enCabina, rotCabina);
        }
        else go.transform.SetPositionAndRotation(enEscena, rotCabina);
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
        // Dónde se pone la mano al agarrar (los attach points del XRHandPoser): hijos de la Palanca para que la mano
        // la siga. Misma pose respecto al pomo que la del ThrottleXR del Thunderbolt V3 (Pose_TwoFingerPinch)
        var puntos = Hijo(palanca.transform, "GrabAttachPoints", Vector3.zero, Quaternion.identity);
        Hijo(puntos.transform, "LeftHandAttach", new Vector3(0.1216f, 0.0932f, -0.0094f), new Quaternion(0.37439f, -0.76286f, -0.16846f, 0.4995f));
        Hijo(puntos.transform, "RightHandAttach", new Vector3(-0.0005f, 0.2069f, -0.0802f), new Quaternion(0.03747f, -0.06335f, 0.5918f, 0.80272f));

        var th = raiz.AddComponent<ThrottleXRV2>();
        var so = new SerializedObject(th);
        so.FindProperty("m_Handle").objectReferenceValue = palanca.transform;
        so.FindProperty("m_MinPosition").floatValue = -Recorrido;   // ralentí atrás
        so.FindProperty("m_MaxPosition").floatValue = Recorrido;    // potencia máxima adelante
        so.FindProperty("m_Value").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ---------- Switch de encendido ----------
    // Ejes locales: Y sale del tablero hacia el piloto, Z hacia arriba (ON), X a la derecha.
    public static GameObject ConstruirSwitch(bool guardar)
    {
        Material metalOscuro = Mat("P47C_MetalOscuro", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.4f, guardar),
                 cromo = Mat("P47C_Cromo", new Color(0.85f, 0.86f, 0.88f), 1f, 0.85f, guardar),
                 rojoGuarda = Mat("P47C_RojoGuarda", new Color(0.78f, 0.07f, 0.05f), 0f, 0.6f, guardar),
                 marcas = Mat("P47C_Marcas", new Color(0.85f, 0.82f, 0.70f), 0f, 0.3f, guardar);

        var raiz = new GameObject("P47_SwitchEncendido");
        // Placa con tuerca central y cuatro tornillos
        var placa = Hijo(raiz.transform, "Placa", Vector3.zero, Quaternion.identity);
        var mp = new Malla();
        mp.Caja(new Vector3(0f, 0.003f, 0f), new Vector3(0.07f, 0.006f, 0.10f), Quaternion.identity);
        mp.Tubo(new Vector3(0f, 0.006f, 0f), new Vector3(0f, 0.012f, 0f), 0.011f, 0.010f, 16);
        foreach (float x in new[] { -0.027f, 0.027f })
            foreach (float z in new[] { -0.040f, 0.040f })
                mp.Tubo(new Vector3(x, 0.006f, z), new Vector3(x, 0.008f, z), 0.0035f, 0.003f, 8);
        Poner(placa, mp, metalOscuro, guardar);
        // Marca de ON (arriba, larga) y de OFF (abajo, corta)
        var mm = new Malla();
        mm.Caja(new Vector3(0f, 0.0063f, 0.028f), new Vector3(0.018f, 0.001f, 0.004f), Quaternion.identity);
        mm.Caja(new Vector3(0f, 0.0063f, -0.044f), new Vector3(0.008f, 0.001f, 0.003f), Quaternion.identity);
        Poner(Hijo(raiz.transform, "Marcas", Vector3.zero, Quaternion.identity), mm, marcas, guardar);

        // Palanca cromada con bola en la punta: gira en X local (XRSwitch). OFF = -30° (abajo), ON = +30° (arriba)
        var pivote = Hijo(raiz.transform, "Pivote", new Vector3(0f, 0.010f, 0f), Quaternion.identity);
        var ml = new Malla();
        ml.Tubo(Vector3.zero, Vector3.up * 0.040f, 0.0045f, 0.0035f, 12);
        ml.Elipsoide(Vector3.up * 0.042f, Vector3.one * 0.0065f, Quaternion.identity, 8, 12);
        Poner(Hijo(pivote.transform, "Palanca", Vector3.zero, Quaternion.identity), ml, cromo, guardar);

        // Guarda roja: bisagra en el borde de arriba; cerrada tapa la palanca en OFF
        var guarda = Hijo(raiz.transform, "Guarda", new Vector3(0f, 0.058f, 0.036f), Quaternion.identity);
        var mg = new Malla();
        mg.Caja(new Vector3(0f, -0.0015f, -0.040f), new Vector3(0.034f, 0.003f, 0.078f), Quaternion.identity);
        foreach (float x in new[] { -0.0155f, 0.0155f })
            mg.Caja(new Vector3(x, -0.025f, -0.040f), new Vector3(0.003f, 0.050f, 0.078f), Quaternion.identity);
        mg.Caja(new Vector3(0f, -0.025f, -0.0775f), new Vector3(0.034f, 0.050f, 0.003f), Quaternion.identity);
        mg.Tubo(new Vector3(-0.017f, 0f, 0f), new Vector3(0.017f, 0f, 0f), 0.003f, 0.003f, 8);   // bisagra
        Poner(guarda, mg, rojoGuarda, guardar);

        // Zona de agarre amplia (no gira, para no perder la mano)
        var agarre = Hijo(raiz.transform, "Agarre", Vector3.zero, Quaternion.identity);
        var caja = agarre.AddComponent<BoxCollider>();
        caja.center = new Vector3(0f, 0.04f, 0f);
        caja.size = new Vector3(0.07f, 0.08f, 0.11f);

        var sw = raiz.AddComponent<XRSwitch>();
        var so = new SerializedObject(sw);
        so.FindProperty("m_Handle").objectReferenceValue = pivote.transform;
        so.FindProperty("m_Pivot").objectReferenceValue = pivote.transform;
        so.FindProperty("m_OnAngle").floatValue = 30f;
        so.FindProperty("m_OffAngle").floatValue = -30f;
        so.FindProperty("m_StartOn").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        var vis = raiz.AddComponent<GuardaSwitchVisual>();
        so = new SerializedObject(vis);
        so.FindProperty("interruptor").objectReferenceValue = sw;
        so.FindProperty("guarda").objectReferenceValue = guarda.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ---------- Palanca del tren ----------
    // Mismos ejes que el switch. La palanca sale de una ranura vertical: ARRIBA = tren arriba, ABAJO = tren abajo.
    public static GameObject ConstruirPalancaTren(bool guardar)
    {
        Material metalOscuro = Mat("P47C_MetalOscuro", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.4f, guardar),
                 metal = Mat("P47C_Metal", new Color(0.30f, 0.30f, 0.31f), 0.8f, 0.5f, guardar),
                 cromo = Mat("P47C_Cromo", new Color(0.85f, 0.86f, 0.88f), 1f, 0.85f, guardar),
                 baquelita = Mat("P47C_Baquelita", new Color(0.05f, 0.045f, 0.04f), 0f, 0.55f, guardar),
                 perilla = Mat("P47C_PerillaTren", new Color(0.88f, 0.87f, 0.84f), 0f, 0.35f, guardar),
                 marcas = Mat("P47C_Marcas", new Color(0.85f, 0.82f, 0.70f), 0f, 0.3f, guardar);
        Material verdeOn = MatLuz("P47C_LuzVerdeOn", new Color(0.15f, 1f, 0.25f), true, guardar),
                 verdeOff = MatLuz("P47C_LuzVerdeOff", new Color(0.05f, 0.22f, 0.07f), false, guardar),
                 rojaOn = MatLuz("P47C_LuzRojaOn", new Color(1f, 0.12f, 0.08f), true, guardar),
                 rojaOff = MatLuz("P47C_LuzRojaOff", new Color(0.25f, 0.04f, 0.03f), false, guardar);

        var raiz = new GameObject("P47_PalancaTren");
        var placa = Hijo(raiz.transform, "Placa", Vector3.zero, Quaternion.identity);
        var mp = new Malla();
        mp.Caja(new Vector3(0f, 0.003f, 0f), new Vector3(0.075f, 0.006f, 0.21f), Quaternion.identity);
        Poner(placa, mp, metalOscuro, guardar);
        var mr = new Malla();
        mr.Caja(new Vector3(0f, 0.0062f, 0f), new Vector3(0.012f, 0.001f, 0.16f), Quaternion.identity);
        Poner(Hijo(raiz.transform, "Ranura", Vector3.zero, Quaternion.identity), mr, baquelita, guardar);
        // Marcas: UP arriba (larga) y DN abajo (corta), a la izquierda de la ranura
        var mm = new Malla();
        mm.Caja(new Vector3(-0.025f, 0.0063f, 0.070f), new Vector3(0.016f, 0.001f, 0.004f), Quaternion.identity);
        mm.Caja(new Vector3(-0.025f, 0.0063f, -0.070f), new Vector3(0.008f, 0.001f, 0.004f), Quaternion.identity);
        Poner(Hijo(raiz.transform, "Marcas", Vector3.zero, Quaternion.identity), mm, marcas, guardar);

        // Luces a la derecha: roja (tren moviéndose) arriba y verde (abajo y asegurado) debajo
        Renderer Luz(string nombre, float z, Material apagada)
        {
            var bisel = new Malla();
            bisel.Tubo(new Vector3(0.025f, 0.006f, z), new Vector3(0.025f, 0.009f, z), 0.0085f, 0.0085f, 16);
            Poner(Hijo(raiz.transform, nombre + "_Bisel", Vector3.zero, Quaternion.identity), bisel, metal, guardar);
            var domo = new Malla();
            domo.Elipsoide(new Vector3(0.025f, 0.009f, z), new Vector3(0.0065f, 0.004f, 0.0065f), Quaternion.identity, 8, 14);
            var go = Hijo(raiz.transform, nombre, Vector3.zero, Quaternion.identity);
            Poner(go, domo, apagada, guardar);
            return go.GetComponent<Renderer>();
        }
        var luzRoja = Luz("LuzRoja", 0.075f, rojaOff);
        var luzVerde = Luz("LuzVerde", 0.045f, verdeOff);

        // Brazo y perilla en forma de llanta: el pivote está 2 cm detrás de la placa para que el brazo cruce la ranura
        var pivote = Hijo(raiz.transform, "Pivote", new Vector3(0f, -0.02f, 0f), Quaternion.identity);
        var mb = new Malla();
        mb.Tubo(Vector3.zero, Vector3.up * 0.105f, 0.0055f, 0.0055f, 12);
        Poner(Hijo(pivote.transform, "Brazo", Vector3.zero, Quaternion.identity), mb, cromo, guardar);
        var mll = new Malla();
        mll.Tubo(new Vector3(-0.011f, 0.112f, 0f), new Vector3(0.011f, 0.112f, 0f), 0.022f, 0.022f, 28);
        Poner(Hijo(pivote.transform, "Perilla", Vector3.zero, Quaternion.identity), mll, perilla, guardar);
        var mrin = new Malla();
        mrin.Tubo(new Vector3(-0.013f, 0.112f, 0f), new Vector3(0.013f, 0.112f, 0f), 0.009f, 0.009f, 16);
        Poner(Hijo(pivote.transform, "Rin", Vector3.zero, Quaternion.identity), mrin, metal, guardar);

        var agarre = Hijo(raiz.transform, "Agarre", Vector3.zero, Quaternion.identity);
        var caja = agarre.AddComponent<BoxCollider>();
        caja.center = new Vector3(0f, 0.06f, 0f);
        caja.size = new Vector3(0.075f, 0.075f, 0.20f);

        // Encendido = tren abajo (la palanca abajo, -Z local); empieza abajo como el tren
        var sw = raiz.AddComponent<XRSwitch>();
        var so = new SerializedObject(sw);
        so.FindProperty("m_Handle").objectReferenceValue = pivote.transform;
        so.FindProperty("m_Pivot").objectReferenceValue = pivote.transform;
        so.FindProperty("m_OnAngle").floatValue = -38f;
        so.FindProperty("m_OffAngle").floatValue = 38f;
        so.FindProperty("m_StartOn").boolValue = true;
        so.FindProperty("m_ToggleOnTap").boolValue = false;   // el tren no se sube con un roce
        so.ApplyModifiedPropertiesWithoutUndo();
        var pt = raiz.AddComponent<PalancaTren>();
        so = new SerializedObject(pt);
        so.FindProperty("luzVerde").objectReferenceValue = luzVerde;
        so.FindProperty("luzRoja").objectReferenceValue = luzRoja;
        so.FindProperty("verdeEncendida").objectReferenceValue = verdeOn;
        so.FindProperty("verdeApagada").objectReferenceValue = verdeOff;
        so.FindProperty("rojaEncendida").objectReferenceValue = rojaOn;
        so.FindProperty("rojaApagada").objectReferenceValue = rojaOff;
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ---------- Aviso de pérdida ----------
    // Mismos ejes que el switch: Y hacia el piloto, Z hacia arriba. Carcasa oscura con una lente ámbar que AvisoPerdida enciende.
    public static GameObject ConstruirAvisoPerdida(bool guardar)
    {
        Material metalOscuro = Mat("P47C_MetalOscuro", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.4f, guardar),
                 ambarOn = MatLuz("P47C_LuzAmbarOn", new Color(1f, 0.48f, 0.02f), true, guardar, 1.2f),
                 ambarOff = MatLuz("P47C_LuzAmbarOff", new Color(0.30f, 0.17f, 0.03f), false, guardar);

        var raiz = new GameObject("P47_AvisoPerdida");
        var mc = new Malla();
        mc.Caja(new Vector3(0f, 0.006f, 0f), new Vector3(0.11f, 0.012f, 0.028f), Quaternion.identity);
        Poner(Hijo(raiz.transform, "Carcasa", Vector3.zero, Quaternion.identity), mc, metalOscuro, guardar);
        var ml = new Malla();
        ml.Caja(new Vector3(0f, 0.0125f, 0f), new Vector3(0.094f, 0.002f, 0.016f), Quaternion.identity);
        var lente = Hijo(raiz.transform, "Lente", Vector3.zero, Quaternion.identity);
        Poner(lente, ml, ambarOff, guardar);

        var aviso = raiz.AddComponent<AvisoPerdida>();
        var so = new SerializedObject(aviso);
        so.FindProperty("lente").objectReferenceValue = lente.GetComponent<Renderer>();
        so.FindProperty("encendida").objectReferenceValue = ambarOn;
        so.FindProperty("apagada").objectReferenceValue = ambarOff;
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ---------- Botones de mantener presionado ----------
    // Mismos ejes que el switch. Placa, anillo de 16 segmentos que se encienden en ámbar y tapa con ícono:
    // reaparecer = tapa azul con una casa; reiniciar = tapa roja con una flecha circular.
    public static GameObject ConstruirBotonMantener(string nombre, BotonMantener.Accion accion, bool guardar)
    {
        bool casa = accion == BotonMantener.Accion.ReaparecerEnBase;
        Material metalOscuro = Mat("P47C_MetalOscuro", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.4f, guardar),
                 tapa = casa ? Mat("P47C_TapaAzul", new Color(0.10f, 0.28f, 0.62f), 0f, 0.55f, guardar)
                             : Mat("P47C_Rojo", new Color(0.70f, 0.08f, 0.06f), 0f, 0.5f, guardar),
                 icono = Mat("P47C_Icono", new Color(0.95f, 0.94f, 0.90f), 0f, 0.4f, guardar),
                 segOn = MatLuz("P47C_LuzAmbarOn", new Color(1f, 0.48f, 0.02f), true, guardar, 1.2f),
                 segOff = MatLuz("P47C_SegmentoOff", new Color(0.16f, 0.13f, 0.08f), false, guardar);

        var raiz = new GameObject(nombre);
        var mp = new Malla();
        mp.Caja(new Vector3(0f, 0.003f, 0f), new Vector3(0.066f, 0.006f, 0.066f), Quaternion.identity);
        foreach (float x in new[] { -0.026f, 0.026f })
            foreach (float z in new[] { -0.026f, 0.026f })
                mp.Tubo(new Vector3(x, 0.006f, z), new Vector3(x, 0.008f, z), 0.003f, 0.0026f, 8);
        Poner(Hijo(raiz.transform, "Placa", Vector3.zero, Quaternion.identity), mp, metalOscuro, guardar);

        // Anillo: 16 segmentos, empieza arriba (+Z) y avanza en sentido horario visto de frente
        const int n = 16;
        var segmentos = new Renderer[n];
        for (int k = 0; k < n; k++)
        {
            float a = Mathf.PI * 0.5f - Mathf.PI * 2f * (k + 0.5f) / n;
            var c = new Vector3(Mathf.Cos(a) * 0.025f, 0.0068f, Mathf.Sin(a) * 0.025f);
            var ms = new Malla();
            ms.Caja(c, new Vector3(0.0075f, 0.0016f, 0.004f), Quaternion.AngleAxis(-a * Mathf.Rad2Deg + 90f, Vector3.up));
            var seg = Hijo(raiz.transform, $"Segmento_{k:00}", Vector3.zero, Quaternion.identity);
            Poner(seg, ms, segOff, guardar);
            segmentos[k] = seg.GetComponent<Renderer>();
        }

        // Tapa (se hunde) con el ícono encima
        var boton = Hijo(raiz.transform, "Boton", Vector3.zero, Quaternion.identity);
        var mt = new Malla();
        mt.Tubo(new Vector3(0f, 0.004f, 0f), new Vector3(0f, 0.015f, 0f), 0.017f, 0.0165f, 28);
        Poner(Hijo(boton.transform, "Tapa", Vector3.zero, Quaternion.identity), mt, tapa, guardar);
        var mi = new Malla();
        const float yi = 0.0153f;
        if (casa)
        {
            var perfil = new[] { new Vector3(-0.008f, yi, -0.0075f), new Vector3(0.008f, yi, -0.0075f), new Vector3(0.008f, yi, 0.0015f),
                                 new Vector3(0f, yi, 0.009f), new Vector3(-0.008f, yi, 0.0015f), new Vector3(-0.008f, yi, -0.0075f) };
            mi.Tapa(perfil, Vector3.up, Vector2.zero);
        }
        else
        {
            var arco = new System.Collections.Generic.List<Vector3>();
            for (int k = 0; k <= 16; k++)
            {
                float a = Mathf.Lerp(130f, 400f, k / 16f) * Mathf.Deg2Rad;
                arco.Add(new Vector3(Mathf.Cos(a) * 0.0085f, yi, Mathf.Sin(a) * 0.0085f));
            }
            mi.TuboCurvo(arco, 0.0016f, 8);
            Vector3 fin = arco[arco.Count - 1], tangente = (fin - arco[arco.Count - 2]).normalized, lado = Vector3.Cross(Vector3.up, tangente);
            mi.Tapa(new[] { fin + tangente * 0.0045f, fin + lado * 0.0038f, fin - lado * 0.0038f, fin + tangente * 0.0045f }, Vector3.up, Vector2.zero);
        }
        Poner(Hijo(boton.transform, "Icono", Vector3.zero, Quaternion.identity), mi, icono, guardar);

        var agarre = Hijo(raiz.transform, "Agarre", Vector3.zero, Quaternion.identity);
        var caja = agarre.AddComponent<BoxCollider>();
        caja.center = new Vector3(0f, 0.02f, 0f);
        caja.size = new Vector3(0.07f, 0.05f, 0.07f);

        var bm = raiz.AddComponent<BotonMantener>();
        var so = new SerializedObject(bm);
        so.FindProperty("accion").enumValueIndex = (int)accion;
        so.FindProperty("boton").objectReferenceValue = boton.transform;
        so.FindProperty("segmentoEncendido").objectReferenceValue = segOn;
        so.FindProperty("segmentoApagado").objectReferenceValue = segOff;
        var lista = so.FindProperty("segmentos");
        lista.arraySize = n;
        for (int k = 0; k < n; k++) lista.GetArrayElementAtIndex(k).objectReferenceValue = segmentos[k];
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

    // Foco de indicador: encendido brilla (emisión), apagado es el mismo vidrio oscuro.
    // 'brillo' multiplica la emisión: con mucho, un ámbar se satura a amarillo.
    static Material MatLuz(string nombre, Color color, bool encendida, bool guardar, float brillo = 2.5f)
    {
        var m = Mat(nombre, color, 0f, 0.85f, guardar);
        if (encendida)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * brillo);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        if (guardar) EditorUtility.SetDirty(m);
        return m;
    }
}
#endif
