// Generador de luces de aeródromo (aparatos visibles de día, con lentes emisivas).
// Crea 'Luces_Aerodromo' junto a la pista con:
//  - luces de borde de pista (blancas) y de umbral/fin de pista (verde hacia la aproximación, roja hacia la pista),
//  - PAPI funcional en cada cabecera (script PapiLights: blancas/rojas según el ángulo del piloto),
//  - luces de borde de rodaje y plataforma (azules),
//  - faro giratorio sobre la torre (script RotatingBeacon).
// Los aparatos fijos se combinan en una malla por material para que pesen poco en VR.
// Menú: Tools > Luces de aeródromo
#if UNITY_EDITOR
using System.Collections.Generic;
using Charly.Airfield;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorLucesAerodromo : EditorWindow
{
    const string NombreObjeto = "Luces_Aerodromo";
    const string CarpetaMateriales = "Assets/VR_FlightSimulato_Proy/Materials/Luces";

    // ---------- Parámetros ----------
    [SerializeField] Transform pista, torre;
    [SerializeField] List<Transform> rodajes = new List<Transform>();
    [SerializeField] List<Transform> plataformas = new List<Transform>();
    float escala = 1.5f;                  // tamaño de los aparatos (1 = real; más grande se ve mejor en VR)
    float intensidadEmision = 2f;         // se aplica a los materiales de lentes al generar
    float fueraDelBorde = 1.5f;           // m desde la orilla del pavimento hasta la luz
    float separacionPista = 60f, separacionRodaje = 30f;
    bool lucesPista = true, umbrales = true, papi = true, lucesRodaje = true, lucesPlataforma = true, faro = true;

    [MenuItem("Tools/Luces de aeródromo")]
    static void Abrir() => GetWindow<GeneradorLucesAerodromo>("Luces de aeródromo");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Usa la misma pista, rodajes y plataformas que 'Marcas de rodaje' (botón de abajo para copiarlos si esa ventana está abierta). Las luces se generan como 'Luces_Aerodromo' junto a la pista; volver a generar reemplaza las anteriores.", MessageType.Info);
        if (GUILayout.Button("Copiar de 'Marcas de rodaje'")) CopiarDeRodaje();

        pista = (Transform)EditorGUILayout.ObjectField("Pista", pista, typeof(Transform), true);
        Lista("Rodajes y conectores", rodajes);
        Lista("Plataformas", plataformas);
        torre = (Transform)EditorGUILayout.ObjectField("Torre (para el faro)", torre, typeof(Transform), true);

        EditorGUILayout.LabelField("Apariencia", EditorStyles.boldLabel);
        escala = EditorGUILayout.Slider("Escala de aparatos", escala, 0.5f, 3f);
        intensidadEmision = EditorGUILayout.Slider("Intensidad de lentes", intensidadEmision, 0f, 8f);
        fueraDelBorde = EditorGUILayout.FloatField("Distancia a la orilla (m)", fueraDelBorde);
        separacionPista = EditorGUILayout.FloatField("Separación en pista (m)", separacionPista);
        separacionRodaje = EditorGUILayout.FloatField("Separación en rodaje (m)", separacionRodaje);

        EditorGUILayout.LabelField("Luces", EditorStyles.boldLabel);
        lucesPista = EditorGUILayout.Toggle("Borde de pista", lucesPista);
        umbrales = EditorGUILayout.Toggle("Umbral y fin de pista", umbrales);
        papi = EditorGUILayout.Toggle("PAPI", papi);
        lucesRodaje = EditorGUILayout.Toggle("Borde de rodaje", lucesRodaje);
        lucesPlataforma = EditorGUILayout.Toggle("Borde de plataforma", lucesPlataforma);
        using (new EditorGUI.DisabledScope(torre == null))
            faro = EditorGUILayout.Toggle("Faro en la torre", faro);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(pista == null))
        {
            if (GUILayout.Button("Generar luces", GUILayout.Height(32))) Generar();
            if (GUILayout.Button("Borrar luces")) Borrar();
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

    void CopiarDeRodaje()
    {
        var ventanas = Resources.FindObjectsOfTypeAll<GeneradorMarcasRodaje>();
        if (ventanas.Length == 0) { EditorUtility.DisplayDialog("Luces", "Abre primero Tools > Marcas de rodaje con sus listas llenas.", "OK"); return; }
        var w = ventanas[0];
        pista = w.pista;
        rodajes = new List<Transform>(w.rodajes);
        plataformas = new List<Transform>(w.plataformas);
    }

    // ---------- Malla combinada ----------
    class Malla
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<int> t = new List<int>();

        public void Caja(Vector3 centro, Vector3 tam, Quaternion rot)
        {
            var h = tam * 0.5f;
            for (int eje = 0; eje < 3; eje++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 normal = Vector3.zero, a = Vector3.zero, b = Vector3.zero;
                    normal[eje] = s;
                    a[(eje + 1) % 3] = 1f;
                    b[(eje + 2) % 3] = 1f;
                    var cara = Vector3.Scale(normal, h);
                    var ea = Vector3.Scale(a, h); var eb = Vector3.Scale(b, h);
                    var q = new[] { cara - ea - eb, cara + ea - eb, cara + ea + eb, cara - ea + eb };
                    // Unity dibuja la cara del lado hacia donde apunta Cross(p1-p0, p2-p0).
                    if (Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), normal) < 0f) System.Array.Reverse(q);
                    int i = v.Count;
                    foreach (var p in q) { v.Add(centro + rot * p); n.Add(rot * normal); }
                    t.Add(i); t.Add(i + 1); t.Add(i + 2);
                    t.Add(i); t.Add(i + 2); t.Add(i + 3);
                }
        }

        public Mesh CrearMesh(string nombre, Transform destino)
        {
            var mesh = new Mesh { name = nombre };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var vl = new List<Vector3>(v.Count); var nl = new List<Vector3>(n.Count);
            for (int i = 0; i < v.Count; i++)
            {
                vl.Add(destino != null ? destino.InverseTransformPoint(v[i]) : v[i]);
                nl.Add(destino != null ? destino.InverseTransformDirection(n[i]) : n[i]);
            }
            mesh.SetVertices(vl);
            mesh.SetNormals(nl);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // ---------- Generación ----------
    Malla metal, blancas, verdes, rojas, azules;
    readonly List<GeneradorMarcasRodaje.Zona> zonas = new List<GeneradorMarcasRodaje.Zona>();
    GeneradorMarcasRodaje.Zona zonaPista;
    Vector3 centro, derecha, adelante;
    float W, L;
    int cuenta, omitidas;

    void Generar()
    {
        if (!GeneradorMarcasPista.Medir(pista, out centro, out var marco, out W, out L))
        { EditorUtility.DisplayDialog("Luces", "La pista necesita un MeshFilter.", "OK"); return; }
        derecha = marco * Vector3.right; adelante = marco * Vector3.forward;

        zonas.Clear();
        zonaPista = GeneradorMarcasRodaje.CrearZona(pista);
        zonaPista.esPista = true;
        zonas.Add(zonaPista);
        var conLuces = new List<GeneradorMarcasRodaje.Zona>();
        var usados = new HashSet<Transform> { pista };
        foreach (var t in rodajes)
        {
            if (t == null || !usados.Add(t)) continue;
            var z = GeneradorMarcasRodaje.CrearZona(t);
            if (z == null) continue;
            zonas.Add(z);
            if (lucesRodaje) conLuces.Add(z);
        }
        foreach (var t in plataformas)
        {
            if (t == null || !usados.Add(t)) continue;
            var z = GeneradorMarcasRodaje.CrearZona(t);
            if (z == null) continue;
            z.esPlataforma = true;
            zonas.Add(z);
            if (lucesPlataforma) conLuces.Add(z);
        }

        metal = new Malla(); blancas = new Malla(); verdes = new Malla(); rojas = new Malla(); azules = new Malla();
        cuenta = omitidas = 0;
        var mMetal = Mat("Luz_Metal", new Color(0.55f, 0.56f, 0.58f), false);
        var mBlanca = Mat("Luz_Blanca", new Color(1f, 0.95f, 0.85f), true);
        var mVerde = Mat("Luz_Verde", new Color(0.1f, 1f, 0.3f), true);
        var mRoja = Mat("Luz_Roja", new Color(1f, 0.1f, 0.05f), true);
        var mAzul = Mat("Luz_Azul", new Color(0.15f, 0.35f, 1f), true);
        var mPapi = Mat("PAPI_Lente", Color.white, true);

        int grupoUndo = Undo.GetCurrentGroup();
        Borrar();
        Physics.SyncTransforms();   // para que Ocupado vea los objetos movidos en el editor
        var raiz = new GameObject(NombreObjeto);
        Undo.RegisterCreatedObjectUndo(raiz, "Generar luces de aeródromo");
        raiz.transform.SetParent(pista.parent, false);

        if (lucesPista) BordePista();
        if (umbrales) { Umbral(0); Umbral(1); }
        if (lucesRodaje || lucesPlataforma) BordeRodajes(conLuces);
        if (papi) { Papi(0, raiz.transform, mPapi); Papi(1, raiz.transform, mPapi); }
        if (faro && torre != null) Faro(raiz.transform, mVerde, mBlanca);

        var flags = GameObjectUtility.GetStaticEditorFlags(pista.gameObject) & ~StaticEditorFlags.ContributeGI;
        Combinar(raiz.transform, "Metal", metal, mMetal, flags);
        Combinar(raiz.transform, "Blancas", blancas, mBlanca, flags);
        Combinar(raiz.transform, "Verdes", verdes, mVerde, flags);
        Combinar(raiz.transform, "Rojas", rojas, mRoja, flags);
        Combinar(raiz.transform, "Azules", azules, mAzul, flags);

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        Debug.Log($"[Luces] {cuenta} aparatos generados; {omitidas} omitidos por chocar con objetos o quedar bajo techo.");
    }

    void Combinar(Transform raiz, string nombre, Malla m, Material mat, StaticEditorFlags flags)
    {
        if (m.v.Count == 0) return;
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = m.CrearMesh($"{NombreObjeto}_{nombre}", go.transform);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, flags);
    }

    // Punto visto por el piloto que aterriza en 'lado': a = a su derecha, d = desde el extremo de la pista.
    Vector3 PosPiloto(int lado, float a, float d) =>
        lado == 0 ? centro + derecha * a + adelante * (-L * 0.5f + d)
                  : centro - derecha * a + adelante * (L * 0.5f - d);

    Vector3 Rumbo(int lado) => lado == 0 ? adelante : -adelante;

    static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);

    // Altura del Terrain bajo el punto (las luces van en el pasto); si no hay, la de la pista.
    Vector3 AlSuelo(Vector3 p)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            var o = t.GetPosition(); var s = t.terrainData.size;
            if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z)
                return new Vector3(p.x, t.SampleHeight(p) + o.y, p.z);
        }
        return new Vector3(p.x, centro.y, p.z);
    }

    // Hay un hangar, tanque u otro objeto con collider donde iría la luz, o la luz quedaría bajo techo
    // (dentro de un hangar). No cuentan el Terrain ni los colliders planos (pavimento, suelos),
    // que miden menos de 1 m de alto.
    bool Ocupado(Vector3 suelo)
    {
        var centroLuz = suelo + Vector3.up * (0.6f * escala);
        foreach (var c in Physics.OverlapSphere(centroLuz, 0.5f * escala, ~0, QueryTriggerInteraction.Ignore))
            if (!(c is TerrainCollider) && c.bounds.size.y >= 1f) return true;
        return Physics.Raycast(centroLuz, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore);
    }

    bool SobrePavimento(Vector3 p, float margen)
    {
        var q = XZ(p);
        foreach (var z in zonas) if (z.Contiene(q, margen)) return true;
        return false;
    }

    // Luz elevada: base y poste de metal con la lente arriba. 'p' = punto en el suelo.
    // Con 'lente2' la lente se parte a la mitad: 'lente' hacia 'haciaLente', 'lente2' hacia el otro lado.
    void LuzElevada(Vector3 p, Vector3 rumbo, Malla lente, Malla lente2 = null, Vector3 haciaLente = default)
    {
        float s = escala;
        if (Ocupado(p)) { omitidas++; return; }
        var rot = Quaternion.LookRotation(new Vector3(rumbo.x, 0f, rumbo.z).normalized, Vector3.up);
        metal.Caja(p + Vector3.up * (0.05f * s), new Vector3(0.3f, 0.1f, 0.3f) * s, rot);
        metal.Caja(p + Vector3.up * (0.225f * s), new Vector3(0.07f, 0.25f, 0.07f) * s, rot);
        var cabeza = p + Vector3.up * (0.42f * s);
        if (lente2 == null) lente.Caja(cabeza, new Vector3(0.16f, 0.14f, 0.16f) * s, rot);
        else
        {
            var mitad = haciaLente.normalized * (0.04f * s);
            lente.Caja(cabeza + mitad, new Vector3(0.16f, 0.14f, 0.08f) * s, rot);
            lente2.Caja(cabeza - mitad, new Vector3(0.16f, 0.14f, 0.08f) * s, rot);
        }
        cuenta++;
    }

    void BordePista()
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(L / separacionPista));
        for (int i = 0; i <= n; i++)
        {
            float z = -L * 0.5f + L * i / n;
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var p = centro + derecha * (lado * (W * 0.5f + fueraDelBorde)) + adelante * z;
                if (SobrePavimento(p, 0.5f)) continue;   // boca de un conector
                LuzElevada(AlSuelo(p), adelante, blancas);
            }
        }
    }

    // Fila justo antes del extremo: verde hacia el avión que llega, roja hacia la pista.
    void Umbral(int lado)
    {
        int n = Mathf.Max(2, Mathf.FloorToInt(W / 3f) + 1);
        for (int i = 0; i < n; i++)
        {
            float a = -W * 0.5f + W * i / (n - 1);
            var p = PosPiloto(lado, a, -fueraDelBorde);
            LuzElevada(AlSuelo(p), Rumbo(lado), verdes, rojas, -Rumbo(lado));
        }
    }

    // Luces azules alrededor de cada rodaje/plataforma, salvo donde caen sobre otro pavimento o junto a la pista.
    void BordeRodajes(List<GeneradorMarcasRodaje.Zona> conLuces)
    {
        var puestas = new List<Vector2>();
        float minimo = separacionRodaje * 0.4f;
        foreach (var z in conLuces)
        {
            float wa = z.hw + fueraDelBorde, la = z.hl + fueraDelBorde;
            var esquinas = new[] { z.Punto(-wa, -la), z.Punto(-wa, la), z.Punto(wa, la), z.Punto(wa, -la) };
            for (int k = 0; k < 4; k++)
            {
                Vector2 p0 = esquinas[k], p1 = esquinas[(k + 1) % 4];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p0, p1) / separacionRodaje));
                for (int i = 0; i < n; i++)
                {
                    var q = Vector2.Lerp(p0, p1, i / (float)n);
                    var p = new Vector3(q.x, centro.y, q.y);
                    if (SobrePavimento(p, 0.5f) || zonaPista.Contiene(q, fueraDelBorde + 4f)) continue;
                    bool cerca = false;
                    foreach (var o in puestas) if ((o - q).sqrMagnitude < minimo * minimo) { cerca = true; break; }
                    if (cerca) continue;
                    puestas.Add(q);
                    LuzElevada(AlSuelo(p), new Vector3(z.v.x, 0f, z.v.y), azules);
                }
            }
        }
    }

    static float LateralPapi(float anchoPista, int i) => anchoPista * 0.5f + 15f + i * 9f;

    // ¿Caben las 4 unidades sin tocar pavimento ni objetos? signo -1 = izquierda del piloto, +1 = derecha.
    bool PapiLibre(int lado, float signo, float d)
    {
        for (int i = 0; i < 4; i++)
        {
            var p = PosPiloto(lado, signo * LateralPapi(W, i), d);
            if (SobrePavimento(p, 3f * escala) || Ocupado(AlSuelo(p))) return false;
        }
        return true;
    }

    // 4 unidades a la izquierda del piloto, a la altura del punto de visada. La más cercana a la pista
    // tiene el ángulo más alto: en la senda de 3° se ven 2 rojas (adentro) y 2 blancas (afuera).
    // Si ahí pasa un rodaje, se recorre a lo largo de la pista y, como último recurso, se pasa a la derecha
    // (la OACI lo permite cuando la izquierda no está libre).
    void Papi(int lado, Transform raiz, Material mLente)
    {
        float dist = L < 800f ? 150f : L < 1200f ? 250f : L < 2400f ? 300f : 400f;
        float dIdeal = 6f + dist + 10f, d = dIdeal, signo = -1f;
        bool libre = false;
        float[] corrimientos = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f };
        for (int k = 0; k < 2 && !libre; k++)
        {
            float sg = k == 0 ? -1f : 1f;
            foreach (float c in corrimientos)
                if (PapiLibre(lado, sg, dIdeal + c)) { d = dIdeal + c; signo = sg; libre = true; break; }
        }
        if (!libre) Debug.LogWarning($"[Luces] No encontré lugar libre para el PAPI {(lado == 0 ? "A" : "B")}; quedó en su posición ideal.");
        else if (signo > 0f || d != dIdeal)
            Debug.Log($"[Luces] PAPI {(lado == 0 ? "A" : "B")} movido para no quedar sobre un rodaje: {(signo > 0f ? "a la derecha, " : "")}{d - dIdeal:+0;-0;0} m a lo largo de la pista.");
        float[] angulos = { 3.5f, 3.1667f, 2.8333f, 2.5f };
        var rumbo = Rumbo(lado);
        var rot = Quaternion.LookRotation(rumbo, Vector3.up);
        float s = escala;

        var go = new GameObject(lado == 0 ? "PAPI_A" : "PAPI_B");
        go.transform.SetParent(raiz, false);
        var lentes = new Renderer[4];
        for (int i = 0; i < 4; i++)
        {
            var suelo = AlSuelo(PosPiloto(lado, signo * LateralPapi(W, i), d));
            if (i == 0) go.transform.position = suelo;
            var caja = suelo + Vector3.up * (0.65f * s);
            metal.Caja(suelo + derecha * (0.45f * s) + Vector3.up * (0.2f * s), new Vector3(0.08f, 0.4f, 0.08f) * s, rot);
            metal.Caja(suelo - derecha * (0.45f * s) + Vector3.up * (0.2f * s), new Vector3(0.08f, 0.4f, 0.08f) * s, rot);
            metal.Caja(caja, new Vector3(1.2f, 0.5f, 0.7f) * s, rot);

            // Lente en la cara que mira hacia el avión que llega.
            var lente = new GameObject($"Lente_{i + 1}");
            lente.transform.SetParent(go.transform, false);
            lente.transform.SetPositionAndRotation(caja - rumbo * (0.365f * s), rot);
            var m = new Malla();
            m.Caja(Vector3.zero, new Vector3(0.9f, 0.25f, 0.03f) * s, Quaternion.identity);
            lente.AddComponent<MeshFilter>().sharedMesh = m.CrearMesh($"PAPI_Lente_{i + 1}", null);
            var mr = lente.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mLente;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            lentes[i] = mr;
            cuenta++;
        }
        go.AddComponent<PapiLights>().Configure(lentes, angulos);
    }

    // Pedestal fijo sobre la torre y cabeza giratoria con media lente verde y media blanca.
    void Faro(Transform raiz, Material mVerde, Material mBlanca)
    {
        var rs = torre.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Debug.LogWarning("[Luces] La torre no tiene renderers; no puse el faro."); return; }
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float s = escala;
        var arriba = new Vector3(b.center.x, b.max.y, b.center.z);
        metal.Caja(arriba + Vector3.up * (0.2f * s), new Vector3(0.5f, 0.4f, 0.5f) * s, Quaternion.identity);

        var cabeza = new GameObject("Faro");
        cabeza.transform.SetParent(raiz, false);
        cabeza.transform.position = arriba + Vector3.up * (0.65f * s);
        cabeza.AddComponent<RotatingBeacon>();
        MitadFaro(cabeza.transform, "Verde", mVerde, 1f, s);
        MitadFaro(cabeza.transform, "Blanca", mBlanca, -1f, s);
        cuenta++;
    }

    static void MitadFaro(Transform cabeza, string nombre, Material mat, float lado, float s)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(cabeza, false);
        go.transform.localPosition = Vector3.forward * (lado * 0.125f * s);
        var m = new Malla();
        m.Caja(Vector3.zero, new Vector3(0.5f, 0.5f, 0.25f) * s, Quaternion.identity);
        go.AddComponent<MeshFilter>().sharedMesh = m.CrearMesh($"Faro_{nombre}", null);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------- Utilidades ----------
    Material Mat(string nombre, Color color, bool emisivo)
    {
        string ruta = $"{CarpetaMateriales}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogWarning("[Luces] No encontré URP/Lit."); return null; }
            m = new Material(shader) { name = nombre };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", emisivo ? 0.6f : 0.35f);
            GeneradorMarcasPista.CrearCarpeta(CarpetaMateriales);
            AssetDatabase.CreateAsset(m, ruta);
        }
        if (emisivo)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", m.GetColor("_BaseColor") * intensidadEmision);
            EditorUtility.SetDirty(m);
        }
        return m;
    }

    void Borrar()
    {
        if (pista == null) return;
        var aBorrar = new List<GameObject>();
        if (pista.parent != null)
        {
            foreach (Transform hijo in pista.parent) if (hijo.name == NombreObjeto) aBorrar.Add(hijo.gameObject);
        }
        else
        {
            foreach (var raiz in pista.gameObject.scene.GetRootGameObjects()) if (raiz.name == NombreObjeto) aBorrar.Add(raiz);
        }
        foreach (var go in aBorrar) Undo.DestroyObjectImmediate(go);
        if (aBorrar.Count > 0) EditorSceneManager.MarkSceneDirty(pista.gameObject.scene);
    }
}
#endif
