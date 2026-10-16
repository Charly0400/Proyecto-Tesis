// Generador de orillas decorativas para MainScene.
// Crea anillos de Terrain alrededor de la cuadrícula existente, SIN TerrainCollider,
// con colinas suaves que se unen al borde del mapa y montañas que crecen hacia afuera.
// Menú: Tools > Orillas Terrain
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorOrillasTerrain : EditorWindow
{
    const string NombrePadre = "Orillas_SinColision";
    const string CarpetaAssets = "Assets/VR_FlightSimulato_Proy/Terrain/Orillas";

    // ---------- Parámetros ----------
    int anillos = 3;                       // cuántos terrenos de ancho tiene la orilla por lado
    int resolucionAnillo1 = 257;           // heightmap del anillo pegado al mapa
    int resolucionResto = 129;             // heightmap de los anillos exteriores
    int resolucionAlphamap = 128;
    float alturaColinas = 120f;            // m
    float alturaMontanas = 1100f;          // m
    float inicioMontanas = 900f;           // m desde el borde del mapa
    float distanciaMezcla = 600f;          // m en los que se suman las colinas sobre el relieve espejo
    float distanciaEspejo = 2500f;         // m en los que el relieve del mapa se refleja antes de desvanecerse
    bool igualarResolucion = true;         // anillo 1 con la misma resolución que el mapa (colores y unión iguales)
    bool usarPintor = true;                // pintar con el Procedural Terrain Painter de la escena
    int resAnillo1Actual;
    float escalaColinas = 1200f;           // longitud de onda aprox. (m)
    float escalaMontanas = 2600f;
    int semilla = 1234;
    TerrainLayer capaRoca;                 // opcional: se pinta en laderas y cimas
    float pixelError = 20f;

    [MenuItem("Tools/Orillas Terrain")]
    static void Abrir() => GetWindow<GeneradorOrillasTerrain>("Orillas Terrain");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Genera terrenos decorativos (sin colisión) alrededor de los Terrain existentes de la escena abierta.", MessageType.Info);
        EditorGUILayout.LabelField("Tamaño", EditorStyles.boldLabel);
        anillos = EditorGUILayout.IntSlider("Anillos (terrenos por lado)", anillos, 1, 6);
        igualarResolucion = EditorGUILayout.Toggle("Anillo 1 = resolución del mapa", igualarResolucion);
        using (new EditorGUI.DisabledScope(igualarResolucion))
            resolucionAnillo1 = EditorGUILayout.IntPopup("Heightmap anillo 1", resolucionAnillo1, new[] { "129", "257", "513" }, new[] { 129, 257, 513 });
        resolucionResto = EditorGUILayout.IntPopup("Heightmap anillos externos", resolucionResto, new[] { "65", "129", "257" }, new[] { 65, 129, 257 });
        resolucionAlphamap = EditorGUILayout.IntPopup("Resolución de pintura", resolucionAlphamap, new[] { "64", "128", "256" }, new[] { 64, 128, 256 });

        EditorGUILayout.LabelField("Relieve", EditorStyles.boldLabel);
        alturaColinas = EditorGUILayout.FloatField("Altura colinas (m)", alturaColinas);
        alturaMontanas = EditorGUILayout.FloatField("Altura montañas (m)", alturaMontanas);
        inicioMontanas = EditorGUILayout.FloatField("Montañas empiezan a (m)", inicioMontanas);
        distanciaMezcla = EditorGUILayout.FloatField("Distancia de mezcla (m)", distanciaMezcla);
        distanciaEspejo = EditorGUILayout.FloatField("Distancia de espejo (m)", distanciaEspejo);
        escalaColinas = EditorGUILayout.FloatField("Escala colinas (m)", escalaColinas);
        escalaMontanas = EditorGUILayout.FloatField("Escala montañas (m)", escalaMontanas);
        semilla = EditorGUILayout.IntField("Semilla", semilla);

        EditorGUILayout.LabelField("Apariencia", EditorStyles.boldLabel);
        usarPintor = EditorGUILayout.Toggle("Usar Terrain Painter del mapa", usarPintor);
        using (new EditorGUI.DisabledScope(usarPintor))
            capaRoca = (TerrainLayer)EditorGUILayout.ObjectField("Capa roca (sin pintor)", capaRoca, typeof(TerrainLayer), false);
        pixelError = EditorGUILayout.Slider("Pixel Error", pixelError, 1f, 50f);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generar orillas", GUILayout.Height(32))) Generar();
        if (GUILayout.Button("Borrar orillas")) Borrar(true);
    }

    // ---------- Datos del mapa interior ----------
    Dictionary<Vector2Int, Terrain> interior;
    Vector3 tam; float minX, maxX, minZ, maxZ; int nx, nz;
    float[][] perfil = new float[4][];          // 0 izq, 1 der, 2 abajo, 3 arriba (alturas cada 1 m)
    float[][] perfilSuave = new float[4][];     // mismo perfil muy suavizado (relieve lejano, sin rayas)
    float[][][] perfilPintura = new float[4][][]; // pesos por capa cada pasoPintura m
    const float pasoPintura = 4f;
    float promedioBorde;
    List<TerrainLayer> capas;
    Dictionary<(Terrain, int), float[,,]> tiras = new Dictionary<(Terrain, int), float[,,]>();
    Vector2 offColinas, offMontanas;

    void Generar()
    {
        var todos = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var internos = new List<Terrain>();
        foreach (var t in todos)
            if (t.terrainData != null && !EsOrilla(t.transform)) internos.Add(t);
        if (internos.Count == 0) { EditorUtility.DisplayDialog("Orillas", "No encontré Terrain en la escena abierta.", "OK"); return; }

        tam = internos[0].terrainData.size;
        foreach (var t in internos)
            if (Mathf.Abs(t.terrainData.size.x - tam.x) > 0.01f || Mathf.Abs(t.terrainData.size.z - tam.z) > 0.01f)
            { EditorUtility.DisplayDialog("Orillas", "Los Terrain no tienen todos el mismo tamaño; no puedo armar la cuadrícula.", "OK"); return; }

        minX = minZ = float.MaxValue; maxX = maxZ = float.MinValue;
        foreach (var t in internos)
        {
            var p = t.transform.position;
            minX = Mathf.Min(minX, p.x); minZ = Mathf.Min(minZ, p.z);
            maxX = Mathf.Max(maxX, p.x + tam.x); maxZ = Mathf.Max(maxZ, p.z + tam.z);
        }
        nx = Mathf.RoundToInt((maxX - minX) / tam.x); nz = Mathf.RoundToInt((maxZ - minZ) / tam.z);
        interior = new Dictionary<Vector2Int, Terrain>();
        foreach (var t in internos)
        {
            var p = t.transform.position;
            interior[new Vector2Int(Mathf.RoundToInt((p.x - minX) / tam.x), Mathf.RoundToInt((p.z - minZ) / tam.z))] = t;
        }

        if (GameObject.Find(NombrePadre) != null)
        {
            if (!EditorUtility.DisplayDialog("Orillas", "Ya existen orillas generadas. ¿Reemplazarlas?", "Reemplazar", "Cancelar")) return;
            Borrar(false);
        }

        var referencia = internos[0];
        var rnd = new System.Random(semilla);
        offColinas = new Vector2(rnd.Next(0, 5000), rnd.Next(0, 5000));
        offMontanas = new Vector2(rnd.Next(0, 5000), rnd.Next(0, 5000));

        try
        {
            EditorUtility.DisplayProgressBar("Orillas", "Leyendo bordes del mapa…", 0f);
            var pintor = usarPintor ? Object.FindFirstObjectByType<sc.terrain.proceduralpainter.TerrainPainter>() : null;
            bool conPintor = pintor != null && pintor.layerSettings != null && pintor.layerSettings.Count > 0;
            if (usarPintor && !conPintor) Debug.LogWarning("[Orillas] No encontré un Terrain Painter configurado; uso la pintura del borde.");

            resAnillo1Actual = igualarResolucion ? referencia.terrainData.heightmapResolution : resolucionAnillo1;
            ConstruirCapas(internos);
            ConstruirPerfiles(resAnillo1Actual);

            float alturaMax = 0f; foreach (var pr in perfil) foreach (var h in pr) alturaMax = Mathf.Max(alturaMax, h);
            float necesaria = alturaMax + alturaColinas + alturaMontanas + 50f;
            // Misma altura que el mapa para que el pintor interprete igual alturas y pendientes.
            float alturaTerreno = Mathf.Max(referencia.terrainData.size.y, necesaria);
            if (conPintor && necesaria > referencia.terrainData.size.y)
                Debug.LogWarning($"[Orillas] Las montañas superan la altura del mapa ({referencia.terrainData.size.y} m); los colores pueden variar un poco.");

            CrearCarpeta(CarpetaAssets);
            var padre = new GameObject(NombrePadre);
            Undo.RegisterCreatedObjectUndo(padre, "Generar orillas");

            var lista = new List<Vector2Int>();
            for (int gx = -anillos; gx < nx + anillos; gx++)
                for (int gz = -anillos; gz < nz + anillos; gz++)
                    if (gx < 0 || gz < 0 || gx >= nx || gz >= nz) lista.Add(new Vector2Int(gx, gz));

            var nuevos = new List<Terrain>();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < lista.Count; i++)
                {
                    var g = lista[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Orillas", $"Terreno {i + 1} de {lista.Count}", (float)i / lista.Count)) break;
                    int anillo = Mathf.Max(Mathf.Max(-g.x, g.x - (nx - 1)), Mathf.Max(-g.y, g.y - (nz - 1)));
                    nuevos.Add(CrearTile(g, anillo == 1 ? resAnillo1Actual : resolucionResto, alturaTerreno, referencia, padre.transform, !conPintor));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            if (conPintor) PintarConPintor(pintor, nuevos);

            AssetDatabase.SaveAssets();
            Terrain.SetConnectivityDirty();
            EditorSceneManager.MarkSceneDirty(padre.scene);
            Debug.Log($"[Orillas] {padre.transform.childCount} terrenos creados sin colisión en '{CarpetaAssets}'.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            tiras.Clear();
        }
    }

    static bool EsOrilla(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name == NombrePadre) return true;
        return false;
    }

    // ---------- Lectura del borde ----------
    float AlturaInterior(float x, float z)
    {
        int ix = Mathf.Clamp(Mathf.FloorToInt((x - minX) / tam.x), 0, nx - 1);
        int iz = Mathf.Clamp(Mathf.FloorToInt((z - minZ) / tam.z), 0, nz - 1);
        if (!interior.TryGetValue(new Vector2Int(ix, iz), out var t)) return 0f;
        var p = t.transform.position;
        float u = Mathf.Clamp01((x - p.x) / tam.x), v = Mathf.Clamp01((z - p.z) / tam.z);
        return t.terrainData.GetInterpolatedHeight(u, v) + p.y;
    }

    void ConstruirCapas(List<Terrain> internos)
    {
        capas = new List<TerrainLayer>();
        foreach (var t in internos)
            foreach (var l in t.terrainData.terrainLayers)
                if (l != null && !capas.Contains(l)) capas.Add(l);
        if (capaRoca != null && !capas.Contains(capaRoca)) capas.Add(capaRoca);
    }

    void ConstruirPerfiles(int resAnillo1)
    {
        // Ventana de máximo: el borde de baja resolución queda igual o un poco encima del mapa (sin huecos).
        int ventana = Mathf.CeilToInt(tam.x / (resAnillo1 - 1) * 0.5f) + 1;
        float suma = 0; int cuenta = 0;
        for (int lado = 0; lado < 4; lado++)
        {
            bool enX = lado >= 2; // abajo/arriba recorren X
            float ini = enX ? minX : minZ, fin = enX ? maxX : maxZ;
            int n = Mathf.CeilToInt(fin - ini) + 1;
            var crudo = new float[n];
            for (int i = 0; i < n; i++)
            {
                float c = Mathf.Min(ini + i, fin);
                crudo[i] = enX ? AlturaInterior(c, lado == 2 ? minZ : maxZ) : AlturaInterior(lado == 0 ? minX : maxX, c);
                suma += crudo[i]; cuenta++;
            }
            // Promedio móvil amplio (±1500 m) para el relieve lejano.
            var acum = new double[n + 1];
            for (int i = 0; i < n; i++) acum[i + 1] = acum[i] + crudo[i];
            var suave = new float[n];
            const int radio = 1500;
            for (int i = 0; i < n; i++)
            {
                int a = Mathf.Max(0, i - radio), b = Mathf.Min(n - 1, i + radio);
                suave[i] = (float)((acum[b + 1] - acum[a]) / (b - a + 1));
            }
            perfilSuave[lado] = suave;
            var pr = new float[n];
            for (int i = 0; i < n; i++)
            {
                float m = float.MinValue;
                for (int k = Mathf.Max(0, i - ventana); k <= Mathf.Min(n - 1, i + ventana); k++) m = Mathf.Max(m, crudo[k]);
                pr[i] = m;
            }
            perfil[lado] = pr;

            // pintura del borde
            int np = Mathf.CeilToInt((fin - ini) / pasoPintura) + 1;
            var pp = new float[np][];
            for (int i = 0; i < np; i++)
            {
                float c = Mathf.Min(ini + i * pasoPintura, fin);
                pp[i] = enX ? PinturaInterior(c, lado == 2 ? minZ : maxZ, lado) : PinturaInterior(lado == 0 ? minX : maxX, c, lado);
            }
            perfilPintura[lado] = pp;
        }
        promedioBorde = cuenta > 0 ? suma / cuenta : 0f;
    }

    float[] PinturaInterior(float x, float z, int lado)
    {
        var w = new float[capas.Count];
        if (capas.Count == 0) return w;
        int ix = Mathf.Clamp(Mathf.FloorToInt((x - minX) / tam.x), 0, nx - 1);
        int iz = Mathf.Clamp(Mathf.FloorToInt((z - minZ) / tam.z), 0, nz - 1);
        if (!interior.TryGetValue(new Vector2Int(ix, iz), out var t)) { w[0] = 1; return w; }
        var td = t.terrainData;
        if (td.alphamapLayers == 0) { w[0] = 1; return w; }
        int ar = td.alphamapResolution;
        if (!tiras.TryGetValue((t, lado), out var tira))
        {
            tira = lado switch
            {
                0 => td.GetAlphamaps(0, 0, 1, ar),
                1 => td.GetAlphamaps(ar - 1, 0, 1, ar),
                2 => td.GetAlphamaps(0, 0, ar, 1),
                _ => td.GetAlphamaps(0, ar - 1, ar, 1),
            };
            tiras[(t, lado)] = tira;
        }
        var p = t.transform.position;
        int idx = lado < 2
            ? Mathf.Clamp(Mathf.FloorToInt((z - p.z) / tam.z * ar), 0, ar - 1)
            : Mathf.Clamp(Mathf.FloorToInt((x - p.x) / tam.x * ar), 0, ar - 1);
        var capasTile = td.terrainLayers;
        for (int l = 0; l < capasTile.Length; l++)
        {
            int u = capas.IndexOf(capasTile[l]);
            if (u < 0) continue;
            w[u] += lado < 2 ? tira[idx, 0, l] : tira[0, idx, l];
        }
        return w;
    }

    // Devuelve la altura del borde más cercano y la distancia a él.
    float Borde(float x, float z, out float d, out int lado, out float coord)
    {
        float cx = Mathf.Clamp(x, minX, maxX), cz = Mathf.Clamp(z, minZ, maxZ);
        d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
        bool fueraX = x <= minX || x >= maxX, fueraZ = z <= minZ || z >= maxZ;
        if (fueraX && !fueraZ) { lado = x <= minX ? 0 : 1; coord = cz - minZ; }
        else { lado = z <= minZ ? 2 : 3; coord = cx - minX; } // incluye esquinas
        var pr = perfil[lado];
        int i = Mathf.Clamp(Mathf.RoundToInt(coord), 0, pr.Length - 1);
        return pr[i];
    }

    // ---------- Relieve ----------
    static float Suave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    float Colinas(float x, float z)
    {
        float s = 0, a = 1, f = 1f / escalaColinas, norm = 0;
        for (int o = 0; o < 4; o++)
        {
            s += a * Mathf.PerlinNoise(x * f + offColinas.x, z * f + offColinas.y);
            norm += a; a *= 0.5f; f *= 2.1f;
        }
        return s / norm;
    }

    float Montanas(float x, float z)
    {
        float s = 0, a = 1, f = 1f / escalaMontanas, norm = 0;
        for (int o = 0; o < 5; o++)
        {
            float n = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * f + offMontanas.x, z * f + offMontanas.y) - 1f);
            s += a * n * n;
            norm += a; a *= 0.5f; f *= 2.05f;
        }
        return s / norm;
    }

    // Altura del mapa en el punto reflejado como espejo respecto al borde.
    float AlturaEspejo(float x, float z)
    {
        float rx = x < minX ? 2f * minX - x : (x > maxX ? 2f * maxX - x : x);
        float rz = z < minZ ? 2f * minZ - z : (z > maxZ ? 2f * maxZ - z : z);
        return AlturaInterior(Mathf.Clamp(rx, minX, maxX), Mathf.Clamp(rz, minZ, maxZ));
    }

    float Altura(float x, float z, out float d, out float baseH)
    {
        float b = Borde(x, z, out d, out int lado, out float coord);
        var ps = perfilSuave[lado];
        float lejano = ps[Mathf.Clamp(Mathf.RoundToInt(coord), 0, ps.Length - 1)];
        float desvanecer = Suave(d / distanciaEspejo);
        float espaciado = tam.x / (resAnillo1Actual - 1);
        float cercano = desvanecer >= 1f ? lejano : (d < espaciado * 0.5f ? b : AlturaEspejo(x, z));
        baseH = Mathf.Lerp(cercano, lejano, desvanecer);
        float mezcla = Suave(d / distanciaMezcla);
        float maxD = anillos * tam.x * 0.85f;
        float mascaraMont = Suave((d - inicioMontanas) / Mathf.Max(1f, maxD - inicioMontanas));
        float relieve = Colinas(x, z) * alturaColinas + Montanas(x, z) * alturaMontanas * mascaraMont;
        return baseH + mezcla * relieve + (1f - mezcla) * 0.2f;
    }

    // ---------- Creación de cada terreno ----------
    // Pinta las orillas con una copia temporal del Terrain Painter del mapa (mismas reglas y mismos límites),
    // sin repintar ni modificar los terrenos originales.
    static void PintarConPintor(sc.terrain.proceduralpainter.TerrainPainter original, List<Terrain> nuevos)
    {
        EditorUtility.DisplayProgressBar("Orillas", "Pintando con el Terrain Painter…", 1f);
        var temporal = new GameObject("_PintorTemporalOrillas");
        temporal.hideFlags = HideFlags.HideAndDontSave;
        var copia = temporal.AddComponent<sc.terrain.proceduralpainter.TerrainPainter>();
        try
        {
            EditorUtility.CopySerialized(original, copia);
            copia.terrains = nuevos.ToArray();
            copia.bounds = original.bounds;               // mismos límites => mismas alturas y ruido que el mapa
            copia.autoRepaint = false;
            copia.terrainListeners = new List<sc.terrain.proceduralpainter.TerrainChangeListener>();
            copia.SetTerrainLayers();
            copia.RepaintAll(true);
        }
        finally
        {
            Object.DestroyImmediate(temporal);
            sc.terrain.proceduralpainter.TerrainPainter.Current = original;
        }
    }

    Terrain CrearTile(Vector2Int g, int res, float alturaTerreno, Terrain referencia, Transform padre, bool pintarBorde)
    {
        var pos = new Vector3(minX + g.x * tam.x, 0f, minZ + g.y * tam.z);
        var td = new TerrainData();
        td.heightmapResolution = res;
        td.size = new Vector3(tam.x, alturaTerreno, tam.z);
        td.alphamapResolution = resolucionAlphamap;
        td.baseMapResolution = Mathf.Min(256, resolucionAlphamap * 2);
        td.SetDetailResolution(32, 8); // sin pasto: resolución mínima para no inflar el archivo

        var alturas = new float[res, res];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
            {
                float x = pos.x + xi / (float)(res - 1) * tam.x;
                float z = pos.z + zi / (float)(res - 1) * tam.z;
                alturas[zi, xi] = Mathf.Clamp01((Altura(x, z, out _, out _) - pos.y) / alturaTerreno);
            }
        td.SetHeights(0, 0, alturas);

        if (pintarBorde && capas.Count > 0)
        {
            td.terrainLayers = capas.ToArray();
            int ar = resolucionAlphamap, nl = capas.Count;
            int iRoca = capaRoca != null ? capas.IndexOf(capaRoca) : -1;
            var mapa = new float[ar, ar, nl];
            for (int zi = 0; zi < ar; zi++)
                for (int xi = 0; xi < ar; xi++)
                {
                    float x = pos.x + (xi + 0.5f) / ar * tam.x;
                    float z = pos.z + (zi + 0.5f) / ar * tam.z;
                    Borde(x, z, out float d, out int lado, out float coord);
                    var pp = perfilPintura[lado];
                    var w = pp[Mathf.Clamp(Mathf.RoundToInt(coord / pasoPintura), 0, pp.Length - 1)];

                    float roca = 0f;
                    if (iRoca >= 0)
                    {
                        const float e = 4f;
                        float h = Altura(x, z, out _, out float bh);
                        float gx = (Altura(x + e, z, out _, out _) - Altura(x - e, z, out _, out _)) / (2 * e);
                        float gz = (Altura(x, z + e, out _, out _) - Altura(x, z - e, out _, out _)) / (2 * e);
                        float pendiente = Mathf.Sqrt(gx * gx + gz * gz);
                        roca = Mathf.Max(Suave((pendiente - 0.45f) / 0.4f), Suave(((h - bh) / Mathf.Max(1f, alturaMontanas) - 0.35f) / 0.3f));
                        roca *= Suave(d / distanciaMezcla);
                    }

                    float suma = 0;
                    for (int l = 0; l < nl; l++) { float v = w[l] * (1f - roca) + (l == iRoca ? roca : 0f); mapa[zi, xi, l] = v; suma += v; }
                    if (suma <= 0.0001f) { mapa[zi, xi, 0] = 1f; suma = 1f; }
                    for (int l = 0; l < nl; l++) mapa[zi, xi, l] /= suma;
                }
            td.SetAlphamaps(0, 0, mapa);
        }

        string nombre = $"Orilla_{g.x}_{g.y}";
        AssetDatabase.CreateAsset(td, $"{CarpetaAssets}/{nombre}.asset");

        // Solo componente Terrain: sin TerrainCollider.
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        go.isStatic = true;
        var t = go.AddComponent<Terrain>();
        t.terrainData = td;
        t.materialTemplate = referencia.materialTemplate;
        t.drawInstanced = referencia.drawInstanced;
        t.groupingID = referencia.groupingID;
        t.allowAutoConnect = true;
        t.heightmapPixelError = pixelError;
        t.basemapDistance = Mathf.Min(referencia.basemapDistance, 1000f);
        t.drawTreesAndFoliage = false;
        t.shadowCastingMode = ShadowCastingMode.Off;
        t.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return t;
    }

    // ---------- Utilidades ----------
    static void CrearCarpeta(string ruta)
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

    static void Borrar(bool preguntar)
    {
        if (preguntar && !EditorUtility.DisplayDialog("Orillas", "¿Borrar las orillas generadas y sus archivos?", "Borrar", "Cancelar")) return;
        var padre = GameObject.Find(NombrePadre);
        if (padre != null)
        {
            var escena = padre.scene;
            Undo.DestroyObjectImmediate(padre);
            EditorSceneManager.MarkSceneDirty(escena);
        }
        if (AssetDatabase.IsValidFolder(CarpetaAssets)) AssetDatabase.DeleteAsset(CarpetaAssets);
        Terrain.SetConnectivityDirty();
    }
}
#endif
