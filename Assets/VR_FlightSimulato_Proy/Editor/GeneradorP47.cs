// Republic P-47D-30 Thunderbolt (cabina de burbuja) a escala real: 1 unidad = 1 m.
// Envergadura 12.43 m, largo 11.02 m, área alar 27.87 m², hélice cuatripala de 3.96 m, llantas de 864 mm.
// Misma jerarquía que el Thunderbolt V3 de la escena: Body_Parts, Anim_Parts (Motor + Hinge_*), Gear_Parts,
// PuertaCabina/CabinaMovible, Colliders, Guns, Seat, PuntoVistaPiloto y Cabin (ancla para la cabina VR).
// Piezas móviles: alerones, flaps, timón de profundidad (Hinge_Timon), timón de dirección (Hinge_Cola), hélice,
// tren retráctil con pistones telescópicos y compuertas, rueda de cola orientable, cabina deslizable,
// aletas de enfriamiento (cowl flaps) y compuertas del intercooler.
// Materiales URP/Lit con texturas generadas (camuflaje, insignias, bandas, líneas de paneles).
// Se guarda como prefab con sus mallas como assets. Menú: Tools > Avión P-47 Thunderbolt
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Charly.FlightController;
using MikeNspired.XRIStarterKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public partial class GeneradorP47 : EditorWindow
{
    const string CarpetaPrefabs = "Assets/VR_FlightSimulato_Proy/Prefabs/Generado";
    const string CarpetaMateriales = "Assets/VR_FlightSimulato_Proy/Materials/P47";
    const string RutaFisicaTren = "Assets/VR_FlightSimulato_Proy/GearLanding.physicMaterial";

    public enum Esquema { VerdeOlivaGris, MetalNatural }

    // ---------- Parámetros ----------
    [SerializeField] Transform ubicacion;
    string nombre = "P47_Thunderbolt";
    Esquema esquema = Esquema.VerdeOlivaGris;
    bool bandasBlancas = true;
    bool agregarFisica = true;

    // Altura de la línea de empuje sobre el suelo con el avión nivelado (cola arriba) y ángulo de tres puntos.
    public const float AlturaEmpuje = 2.30f, AnguloTresPuntos = 12.5f;

    [MenuItem("Tools/Avión P-47 Thunderbolt")]
    static void Abrir() => GetWindow<GeneradorP47>("P-47 Thunderbolt");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Crea un P-47D Thunderbolt a escala real (1 unidad = 1 m) con la misma jerarquía que tu Thunderbolt V3. " +
            "Las superficies de control y la hélice ya traen ControlSurfaceAnimator / PropellerAnimator: se mueven solas en cuanto " +
            "el avión tiene un FlightControllerV3. 'Ubicación' (opcional) = dónde se pone en la escena, apoyado en sus tres ruedas.", MessageType.Info);
        ubicacion = (Transform)EditorGUILayout.ObjectField("Ubicación (opcional)", ubicacion, typeof(Transform), true);
        nombre = EditorGUILayout.TextField("Nombre del prefab", nombre);
        esquema = (Esquema)EditorGUILayout.EnumPopup("Pintura", esquema);
        bandasBlancas = EditorGUILayout.Toggle("Bandas blancas (cofia y cola)", bandasBlancas);
        agregarFisica = EditorGUILayout.Toggle("Agregar física de vuelo", agregarFisica);
        if (agregarFisica)
            EditorGUILayout.HelpBox("Agrega Rigidbody, FlightControllerV3, AircraftPhysics (preset P-47, tren, centro de masa y " +
                "'Wing Incidence' = 0 ya puestos), CrashHandler, AircraftVehicle (asiento y eventos de la cabina deslizable) y " +
                "CockpitViewRecenter. Falta lo de la escena, igual que en tu Thunderbolt V3: XR Origin, palanca, joystick, switch, " +
                "canvas de sentarse, audio y AircraftGun (las bocas ya están en Guns).", MessageType.None);
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(nombre)))
            if (GUILayout.Button("Generar P-47", GUILayout.Height(32))) Generar();
    }

    // ---------- Generación ----------
    void Generar()
    {
        string rutaPrefab = $"{CarpetaPrefabs}/{nombre}.prefab";
        int grupoUndo = Undo.GetCurrentGroup();
        // Quitar aviones generados antes con este prefab (se reemplazan).
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && EsInstanciaDe(t.gameObject, rutaPrefab))
            {
                if (ubicacion == t) ubicacion = null;
                Undo.DestroyObjectImmediate(t.gameObject);
            }

        guardarAssets = true;
        var r = Construir();
        if (agregarFisica) AgregarFisica(r);
        Colocar(r);
        Undo.RegisterCreatedObjectUndo(r, "Generar P-47");

        GeneradorMarcasPista.CrearCarpeta(CarpetaPrefabs);
        PrefabUtility.SaveAsPrefabAssetAndConnect(r, rutaPrefab, InteractionMode.AutomatedAction);
        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(r.scene);
        Selection.activeGameObject = r;
        Debug.Log($"[P-47] '{rutaPrefab}' generado: {ResumenTamano(r)}");
    }

    static bool EsInstanciaDe(GameObject go, string rutaPrefab) =>
        PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == rutaPrefab;

    // Apoya el avión en sus tres ruedas en la ubicación (o frente a la cámara de la escena).
    void Colocar(GameObject r)
    {
        Vector3 pos; float rumbo = 0f; Transform padre = null;
        if (ubicacion != null) { pos = ubicacion.position; rumbo = ubicacion.eulerAngles.y; padre = ubicacion.parent; }
        else pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        float suelo = pos.y;
        foreach (var h in Physics.RaycastAll(pos + Vector3.up * 50f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (ubicacion != null && h.collider.transform.IsChildOf(ubicacion)) continue;
            if (h.collider.transform.IsChildOf(r.transform)) continue;
            suelo = h.point.y; break;
        }
        var rot = Quaternion.Euler(-AnguloTresPuntos, rumbo, 0f);
        var contacto = P(EjeTrenX, 0f, -AlturaEmpuje);      // punto medio entre las ruedas principales, en el suelo
        r.transform.SetParent(padre, true);
        r.transform.SetPositionAndRotation(new Vector3(pos.x, suelo, pos.z) - rot * contacto, rot);
    }

    static string ResumenTamano(GameObject r)
    {
        var b = new Bounds(r.transform.position, Vector3.zero);
        int tris = 0;
        foreach (var mf in r.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            tris += (int)(mf.sharedMesh.GetIndexCount(0) / 3);
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null) b.Encapsulate(mr.bounds);
        }
        return $"{b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m, {tris} triángulos";
    }

    // ---------- Construcción ----------
    bool guardarAssets = true;
    GameObject raiz;
    Transform tBody, tAnim, tGear, tColliders;
    readonly List<LandingGearAnimator.Pieza> piezasTren = new List<LandingGearAnimator.Pieza>();
    readonly List<LandingGearAnimator.Rueda> ruedasTren = new List<LandingGearAnimator.Rueda>();
    readonly List<CowlFlapsAnimator.Aleta> aletasMotor = new List<CowlFlapsAnimator.Aleta>();
    readonly List<Collider> collidersTren = new List<Collider>();
    readonly List<Renderer> rendAspas = new List<Renderer>();
    Renderer rendDisco;

    public GameObject Construir()
    {
        piezasTren.Clear(); ruedasTren.Clear(); aletasMotor.Clear(); collidersTren.Clear(); rendAspas.Clear(); rendDisco = null;
        CrearMateriales();

        raiz = new GameObject(nombre);
        tBody = Vacio(raiz.transform, "Body_Parts").transform;
        tAnim = Vacio(raiz.transform, "Anim_Parts").transform;
        tGear = Vacio(raiz.transform, "Gear_Parts").transform;

        ConstruirFuselaje();
        ConstruirMotorYHelice();
        ConstruirAlas();
        ConstruirCola();
        ConstruirCabina();
        ConstruirTren();
        ConstruirDetalles();
        tColliders = Vacio(raiz.transform, "Colliders").transform;
        ConstruirColliders();
        ConstruirPuntos();
        ConfigurarAnimadores();
        return raiz;
    }

    // Objeto vacío con pose en ejes del avión (la raíz está en el origen mientras se construye).
    static GameObject Vacio(Transform padre, string nombreGo, Vector3 posicion = default, Quaternion? rotacion = null)
    {
        var go = new GameObject(nombreGo);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(posicion, rotacion ?? Quaternion.identity);
        return go;
    }

    // Malla nueva expresada en ejes locales de 'go' (la geometría se agrega en ejes del avión).
    static Malla MallaDe(GameObject go) => new Malla { Matriz = go.transform.worldToLocalMatrix };

    // Pone la malla al objeto (o a un hijo nuevo si 'hijo' tiene nombre).
    MeshRenderer Poner(GameObject go, Malla m, Material mat, string hijo = null, bool sombras = true)
    {
        if (m == null || m.Vacia) return null;
        var destino = go;
        if (!string.IsNullOrEmpty(hijo)) destino = Vacio(go.transform, hijo, go.transform.position, go.transform.rotation);
        string nombreMalla = $"P47_{destino.name}";
        var mesh = m.CrearMesh(nombreMalla);
        destino.AddComponent<MeshFilter>().sharedMesh = guardarAssets ? UtilGeneradores.GuardarMalla(mesh) : mesh;
        var mr = destino.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = sombras ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return mr;
    }

    // Parte fija: un hijo de Body_Parts con su malla.
    MeshRenderer Fija(string parte, Malla m, Material mat, bool sombras = true) =>
        Poner(Vacio(tBody, parte), m, mat, null, sombras);

    // Pivote con ejes: X a lo largo de 'derecha', Y lo más cerca posible de 'arriba'.
    static Quaternion Ejes(Vector3 derecha, Vector3 arriba)
    {
        Vector3 adelante = Vector3.Cross(derecha, arriba).normalized;
        return Quaternion.LookRotation(adelante, Vector3.Cross(adelante, derecha).normalized);
    }

    // ---------- Componentes ----------
    void ConfigurarAnimadores()
    {
        // Tren de aterrizaje
        var tren = tGear.gameObject.AddComponent<LandingGearAnimator>();
        var so = new SerializedObject(tren);
        var lp = so.FindProperty("piezas");
        lp.arraySize = piezasTren.Count;
        for (int i = 0; i < piezasTren.Count; i++)
        {
            var e = lp.GetArrayElementAtIndex(i); var p = piezasTren[i];
            e.FindPropertyRelative("pieza").objectReferenceValue = p.pieza;
            e.FindPropertyRelative("tipo").enumValueIndex = (int)p.tipo;
            e.FindPropertyRelative("tramo").vector2Value = p.tramo;
            e.FindPropertyRelative("posicionArriba").vector3Value = p.posicionArriba;
            e.FindPropertyRelative("rotacionArriba").quaternionValue = p.rotacionArriba;
            e.FindPropertyRelative("posicionAbajo").vector3Value = p.posicionAbajo;
            e.FindPropertyRelative("rotacionAbajo").quaternionValue = p.rotacionAbajo;
        }
        var lr = so.FindProperty("ruedas");
        lr.arraySize = ruedasTren.Count;
        for (int i = 0; i < ruedasTren.Count; i++)
        {
            var e = lr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("rueda").objectReferenceValue = ruedasTren[i].rueda;
            e.FindPropertyRelative("radio").floatValue = ruedasTren[i].radio;
            e.FindPropertyRelative("eje").vector3Value = ruedasTren[i].eje;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // Aletas de enfriamiento
        var enfriamiento = tAnim.Find("Aletas_Enfriamiento");
        if (enfriamiento != null)
        {
            var cf = enfriamiento.gameObject.AddComponent<CowlFlapsAnimator>();
            so = new SerializedObject(cf);
            var la = so.FindProperty("aletas");
            la.arraySize = aletasMotor.Count;
            for (int i = 0; i < aletasMotor.Count; i++)
            {
                var e = la.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("aleta").objectReferenceValue = aletasMotor[i].aleta;
                e.FindPropertyRelative("eje").vector3Value = aletasMotor[i].eje;
                e.FindPropertyRelative("anguloAbierta").floatValue = aletasMotor[i].anguloAbierta;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Hélice
        var motor = tAnim.Find("Motor");
        if (motor != null)
        {
            var pa = motor.gameObject.AddComponent<PropellerAnimator>();
            so = new SerializedObject(pa);
            so.FindProperty("spinAxis").vector3Value = Vector3.forward;
            so.FindProperty("reverse").boolValue = true;   // R-2800: horario visto desde la cabina
            var br = so.FindProperty("bladeRenderers");
            br.arraySize = rendAspas.Count;
            for (int i = 0; i < rendAspas.Count; i++) br.GetArrayElementAtIndex(i).objectReferenceValue = rendAspas[i];
            so.FindProperty("blurDisc").objectReferenceValue = rendDisco;
            so.FindProperty("blurMaxAlpha").floatValue = 0.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void Superficie(Transform bisagra, ControlSurfaceAnimator.ControlInput fuente, bool invertir, Vector3 eje, float positiva, float negativa)
    {
        var csa = bisagra.gameObject.AddComponent<ControlSurfaceAnimator>();
        var so = new SerializedObject(csa);
        so.FindProperty("source").enumValueIndex = (int)fuente;
        so.FindProperty("invert").boolValue = invertir;
        so.FindProperty("hingeAxis").vector3Value = eje;
        so.FindProperty("positiveDeflection").floatValue = positiva;
        so.FindProperty("negativeDeflection").floatValue = negativa;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigurarCabinaDeslizable(GameObject cabina, float recorrido)
    {
        var slider = cabina.AddComponent<XRToggleSlider>();
        var so = new SerializedObject(slider);
        so.FindProperty("m_Handle").objectReferenceValue = cabina.transform;
        so.FindProperty("m_Value").floatValue = 1f;            // empieza abierta, como el Thunderbolt V3
        so.FindProperty("m_MinPosition").floatValue = 0f;       // cerrada
        so.FindProperty("m_MaxPosition").floatValue = -recorrido; // abierta: deslizada hacia la cola
        so.ApplyModifiedPropertiesWithoutUndo();
        cabina.transform.localPosition = new Vector3(0f, 0f, -recorrido);
    }

    // Componentes del Thunderbolt V3 que no dependen de la escena. Faltan (como en el V3): XR Origin, palanca,
    // joystick, switch de encendido, canvas de sentarse/pararse, audio del motor y AircraftGun.
    void AgregarFisica(GameObject r)
    {
        var rb = r.AddComponent<Rigidbody>();
        rb.mass = 6000f;
        var vuelo = r.AddComponent<FlightControllerV3>();
        var fisica = r.AddComponent<AircraftPhysics>();
        var preset = typeof(AircraftPhysics).GetMethod("ApplyP47Preset", BindingFlags.Instance | BindingFlags.NonPublic);
        preset?.Invoke(fisica, null);
        var so = new SerializedObject(fisica);
        so.FindProperty("wingIncidenceDeg").floatValue = 0f;    // la raíz ya va alineada con la línea de empuje
        so.FindProperty("centerOfMassOffset").vector3Value = AjusteCentroDeMasa(r);
        so.FindProperty("logTelemetry").boolValue = false;
        AsignarLista(so.FindProperty("landingGear"), collidersTren);
        so.ApplyModifiedPropertiesWithoutUndo();
        var crash = r.AddComponent<CrashHandler>();
        so = new SerializedObject(crash);
        AsignarLista(so.FindProperty("landingGear"), collidersTren);
        so.ApplyModifiedPropertiesWithoutUndo();

        var asiento = r.transform.Find("Seat");
        var avion = r.AddComponent<AircraftVehicle>();
        avion.pilotSeat = asiento;
        vuelo.pilotSeat = asiento;
        vuelo.aircraft = avion;
        var recentrar = r.AddComponent<CockpitViewRecenter>();
        recentrar.puntoVista = r.transform.Find("PuntoVistaPiloto");

        // Cabina deslizable: mismos eventos que en el Thunderbolt V3
        var slider = r.GetComponentInChildren<XRToggleSlider>(true);
        if (slider != null)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(slider.OnValueChange, avion.OnCloseWindow);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(slider.OnMaxValue, avion.ActiveWindowButton);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(slider.OnMinValue, avion.CloseWindowButton);
        }
    }

    // El Rigidbody calcula el centro de masa con los colliders (densidad pareja), que lo dejan muy atrás.
    // Se corrige para que quede ~45 cm detrás del eje del tren principal y 25 cm bajo la línea de empuje.
    Vector3 AjusteCentroDeMasa(GameObject r)
    {
        Vector3 suma = Vector3.zero;
        float volumen = 0f;
        foreach (var c in r.GetComponentsInChildren<Collider>(true))
        {
            float v; Vector3 centro;
            if (c is BoxCollider b) { var s = Vector3.Scale(b.size, c.transform.lossyScale); v = s.x * s.y * s.z; centro = c.transform.TransformPoint(b.center); }
            else if (c is SphereCollider e) { v = 4.18879f * Mathf.Pow(e.radius, 3f); centro = c.transform.TransformPoint(e.center); }
            else continue;
            suma += centro * v; volumen += v;
        }
        if (volumen <= 0f) return new Vector3(0f, -0.25f, 0f);
        Vector3 calculado = r.transform.InverseTransformPoint(suma / volumen);
        return P(EjeTrenX + 0.45f, 0f, -0.25f) - calculado;
    }

    static void AsignarLista(SerializedProperty lista, List<Collider> valores)
    {
        if (lista == null) return;
        lista.arraySize = valores.Count;
        for (int i = 0; i < valores.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
    }
}
#endif
