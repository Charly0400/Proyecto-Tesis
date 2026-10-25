// Generador de marcas de rodaje (líneas amarillas) para rodajes hechos con cubos escalados.
// Crea UNA sola malla con:
//  - eje central continuo que se une en los cruces y entra a la pista hasta su eje,
//  - bordes dobles que se cortan en la boca de cada cruce,
//  - marcas de espera (2 líneas continuas + 2 discontinuas) antes de entrar a la pista.
// Menú: Tools > Marcas de rodaje
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class GeneradorMarcasRodaje : EditorWindow
{
    const string NombreObjeto = "Marcas_Rodajes";
    const string RutaMaterial = "Assets/VR_FlightSimulato_Proy/Materials/Pista/MarcasRodaje.mat";

    // ---------- Parámetros ----------
    [SerializeField] internal Transform pista;
    [SerializeField] internal List<Transform> rodajes = new List<Transform>();      // rodaje paralelo y conectores
    [SerializeField] internal List<Transform> plataformas = new List<Transform>();
    Material material;                    // opcional: si no hay, se crea uno amarillo URP/Lit
    float alturaSobreRodaje = 0.06f;      // m; un poco más que las marcas de pista para quedar encima
    float anchoEje = 0.4f;                // m (real 0.15; más ancho para que se lea en VR)
    float anchoBorde = 0.25f, separacionBorde = 0.25f;
    float distanciaEspera = 40f;          // m del eje de la pista a la marca de espera
    bool eje = true, bordes = true, espera = true, ejeSobrePista = true;

    const float Paso = 0.5f;              // m entre muestras al recortar bordes
    const float MargenBorde = 0.4f;       // m entre la orilla del rodaje y la primera línea de borde
    const float AnchoEspera = 0.3f, PasoEspera = 0.6f, GuionEspera = 1f;
    const float Traslape = 2.5f;          // m que se alarga una punta de rodaje que toca otra zona

    [MenuItem("Tools/Marcas de rodaje")]
    static void Abrir() => GetWindow<GeneradorMarcasRodaje>("Marcas de rodaje");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Pon la pista, los rodajes (incluye los conectores) y las plataformas. Tip: selecciona varios en la jerarquía con Ctrl+clic y pulsa 'Usar selección'. Las marcas se generan como 'Marcas_Rodajes' junto a la pista; volver a generar reemplaza las anteriores.", MessageType.Info);
        pista = (Transform)EditorGUILayout.ObjectField("Pista", pista, typeof(Transform), true);
        Lista("Rodajes y conectores", rodajes);
        Lista("Plataformas", plataformas);

        EditorGUILayout.LabelField("Apariencia", EditorStyles.boldLabel);
        material = (Material)EditorGUILayout.ObjectField("Material (opcional)", material, typeof(Material), false);
        alturaSobreRodaje = EditorGUILayout.FloatField("Altura sobre el rodaje (m)", alturaSobreRodaje);
        anchoEje = EditorGUILayout.FloatField("Ancho del eje (m)", anchoEje);
        anchoBorde = EditorGUILayout.FloatField("Ancho línea de borde (m)", anchoBorde);
        separacionBorde = EditorGUILayout.FloatField("Separación borde doble (m)", separacionBorde);

        EditorGUILayout.LabelField("Marcas", EditorStyles.boldLabel);
        eje = EditorGUILayout.Toggle("Eje central", eje);
        using (new EditorGUI.DisabledScope(!eje))
            ejeSobrePista = EditorGUILayout.Toggle("Continuar eje sobre la pista", ejeSobrePista);
        bordes = EditorGUILayout.Toggle("Bordes dobles", bordes);
        espera = EditorGUILayout.Toggle("Marcas de espera", espera);
        using (new EditorGUI.DisabledScope(!espera))
            distanciaEspera = EditorGUILayout.FloatField("Distancia al eje de pista (m)", distanciaEspera);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(pista == null))
        {
            if (GUILayout.Button("Generar marcas", GUILayout.Height(32))) Generar();
            if (GUILayout.Button("Borrar marcas")) Borrar();
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

    // ---------- Zonas pavimentadas (rectángulos en XZ de mundo) ----------
    internal class Zona
    {
        public Vector2 c, u, v;     // centro y ejes (u = lateral, v = a lo largo)
        public float hw, hl, y;     // medio ancho, medio largo y altura de la cara superior
        public bool esPista, esPlataforma;

        public Vector2 Punto(float a, float b) => c + u * a + v * b;
        public bool Contiene(Vector2 p, float margen)
        {
            var d = p - c;
            return Mathf.Abs(Vector2.Dot(d, u)) <= hw + margen && Mathf.Abs(Vector2.Dot(d, v)) <= hl + margen;
        }
    }

    internal static Zona CrearZona(Transform t)
    {
        if (t == null || !GeneradorMarcasPista.Medir(t, out var centro, out var marco, out float ancho, out float largo)) return null;
        var r = marco * Vector3.right;
        var f = marco * Vector3.forward;
        return new Zona
        {
            c = new Vector2(centro.x, centro.z),
            u = new Vector2(r.x, r.z).normalized,
            v = new Vector2(f.x, f.z).normalized,
            hw = ancho * 0.5f, hl = largo * 0.5f, y = centro.y,
        };
    }

    // ---------- Generación ----------
    readonly List<Zona> zonas = new List<Zona>();
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<int> tris = new List<int>();
    int esperas;

    void Generar()
    {
        zonas.Clear(); verts.Clear(); tris.Clear(); esperas = 0;
        var zp = CrearZona(pista);
        if (zp == null) { EditorUtility.DisplayDialog("Marcas de rodaje", "La pista necesita un MeshFilter.", "OK"); return; }
        zp.esPista = true;
        zonas.Add(zp);
        var usados = new HashSet<Transform> { pista };
        var rodajesZona = new List<Zona>();
        foreach (var t in rodajes)
        {
            if (t == null || !usados.Add(t)) continue;
            var z = CrearZona(t);
            if (z != null) { zonas.Add(z); rodajesZona.Add(z); }
        }
        foreach (var t in plataformas)
        {
            if (t == null || !usados.Add(t)) continue;
            var z = CrearZona(t);
            if (z != null) { z.esPlataforma = true; zonas.Add(z); }
        }
        if (rodajesZona.Count == 0) { EditorUtility.DisplayDialog("Marcas de rodaje", "Agrega al menos un rodaje.", "OK"); return; }

        Extender(rodajesZona);
        foreach (var a in rodajesZona)
        {
            if (eje || espera) EjeYEspera(a);
            if (bordes) Bordes(a);
        }

        int grupoUndo = Undo.GetCurrentGroup();
        Borrar();
        var go = new GameObject(NombreObjeto);
        Undo.RegisterCreatedObjectUndo(go, "Generar marcas de rodaje");
        go.transform.SetParent(pista.parent, false);
        for (int i = 0; i < verts.Count; i++) verts[i] = go.transform.InverseTransformPoint(verts[i]);

        var mesh = new Mesh { name = NombreObjeto };
        if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        var normales = new Vector3[verts.Count];
        for (int i = 0; i < normales.Length; i++) normales[i] = go.transform.InverseTransformDirection(Vector3.up);
        mesh.normals = normales;
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material != null ? material : MaterialPorDefecto();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(pista.gameObject) & ~StaticEditorFlags.ContributeGI);

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log($"[Marcas rodaje] {tris.Count / 6} líneas en {rodajesZona.Count} rodajes, {esperas} marcas de espera.");
    }

    // Alarga un poco las puntas de rodaje que tocan otra zona, para que bordes y ejes
    // se unan aunque los cubos solo se toquen sin encimarse.
    void Extender(List<Zona> rodajesZona)
    {
        var alargar = new List<(Zona z, int lado)>();
        foreach (var a in rodajesZona)
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var p = a.Punto(0f, lado * a.hl);
                foreach (var b in zonas)
                    if (b != a && b.Contiene(p, 2f)) { alargar.Add((a, lado)); break; }
            }
        foreach (var (z, lado) in alargar)
        {
            z.c += z.v * (lado * Traslape * 0.5f);
            z.hl += Traslape * 0.5f;
        }
    }

    // ---------- Eje central y marcas de espera ----------
    void EjeYEspera(Zona a)
    {
        float t0 = Extremo(a, -1), t1 = Extremo(a, 1);
        if (eje && t1 - t0 > 1f) Linea(a.Punto(0f, t0), a.Punto(0f, t1), anchoEje, a);
    }

    // Lleva el extremo del eje hasta el eje del rodaje o pista con el que conecta,
    // o hasta la orilla si es una plataforma (o la pista con "continuar eje" apagado).
    // Sirve tanto si al eje le falta para llegar al cruce como si se pasa de él (esquinas en L).
    float Extremo(Zona a, int lado)
    {
        float t = lado * a.hl, nuevo = t, mejor = float.MaxValue;
        Zona pistaConectada = null;
        foreach (var b in zonas)
        {
            if (b == a) continue;
            // Eje de b que cruza la dirección de a.
            bool porAncho = Mathf.Abs(Vector2.Dot(a.v, b.u)) >= Mathf.Abs(Vector2.Dot(a.v, b.v));
            var ejeB = porAncho ? b.u : b.v;
            float mitad = porAncho ? b.hw : b.hl;
            float k = Vector2.Dot(a.v, ejeB);
            if (Mathf.Abs(k) < 0.3f) continue;  // casi paralelos: no se cruzan
            float l0 = Vector2.Dot(a.c - b.c, ejeB);
            bool hastaOrilla = b.esPlataforma || (b.esPista && !ejeSobrePista);
            float tb = ((hastaOrilla ? Mathf.Sign(l0) * mitad : 0f) - l0) / k;
            if (tb * lado <= 0f) continue;      // quedaría del otro lado del centro del rodaje
            if (!b.Contiene(a.Punto(0f, tb), a.hw + 2f)) continue;   // el cruce no cae sobre b
            float sobra = lado * (t - tb);      // > 0: el eje se pasa del cruce; < 0: le falta
            if (sobra < -(mitad + 2f) || sobra > mitad + a.hw + 5f) continue;  // cruce lejos de la punta
            float cambio = Mathf.Abs(tb - t);
            if (cambio < mejor) { mejor = cambio; nuevo = tb; pistaConectada = b.esPista ? b : null; }
        }
        if (espera && pistaConectada != null) Espera(a, lado, pistaConectada);
        return nuevo;
    }

    // Patrón A de la OACI: 2 líneas continuas del lado del rodaje y 2 discontinuas del lado de la pista.
    void Espera(Zona a, int lado, Zona p)
    {
        float k = Vector2.Dot(a.v, p.u);
        if (Mathf.Abs(k) < 0.3f) return;
        float l0 = Vector2.Dot(a.c - p.c, p.u);
        float d = Mathf.Max(distanciaEspera, p.hw + 5f);
        float th = (Mathf.Sign(l0) * d - l0) / k;
        float mitad = a.hw - MargenBorde;
        if (mitad <= 0f || !a.Contiene(a.Punto(0f, th), 0f) || p.Contiene(a.Punto(0f, th + lado * 3f * PasoEspera), 0f))
        {
            Debug.LogWarning("[Marcas rodaje] Un conector es muy corto para la marca de espera; baja la distancia o alárgalo.");
            return;
        }
        esperas++;
        for (int j = 0; j < 4; j++)
        {
            float tj = th + lado * j * PasoEspera;
            if (j < 2) { Linea(a.Punto(-mitad, tj), a.Punto(mitad, tj), AnchoEspera, a); continue; }
            for (float x = -mitad; x < mitad; x += 2f * GuionEspera)
                Linea(a.Punto(x, tj), a.Punto(Mathf.Min(x + GuionEspera, mitad), tj), AnchoEspera, a);
        }
    }

    // ---------- Bordes ----------
    void Bordes(Zona a)
    {
        for (int linea = 0; linea < 2; linea++)
        {
            float ins = MargenBorde + anchoBorde * 0.5f + linea * (anchoBorde + separacionBorde);
            float wa = a.hw - ins, la = a.hl - ins;
            if (wa <= 0f || la <= 0f) return;
            Vector2 e0 = a.Punto(-wa, -la), e1 = a.Punto(-wa, la), e2 = a.Punto(wa, la), e3 = a.Punto(wa, -la);
            Tramo(a, e0, e1, ins); Tramo(a, e1, e2, ins); Tramo(a, e2, e3, ins); Tramo(a, e3, e0, ins);
        }
    }

    // Dibuja el lado p→q solo donde no entra en otra zona pavimentada (bocas de cruces, pista y plataformas).
    void Tramo(Zona a, Vector2 p, Vector2 q, float ins)
    {
        float largo = Vector2.Distance(p, q);
        int n = Mathf.Max(1, Mathf.CeilToInt(largo / Paso));
        float inicio = -1f;
        for (int i = 0; i <= n; i++)
        {
            float f = i / (float)n;
            bool libre = !Bloqueado(a, Vector2.Lerp(p, q, f), ins);
            if (libre && inicio < 0f) inicio = f;
            if ((!libre || i == n) && inicio >= 0f)
            {
                float fin = libre ? f : (i - 1) / (float)n;
                if ((fin - inicio) * largo >= 1f) Linea(Vector2.Lerp(p, q, inicio), Vector2.Lerp(p, q, fin), anchoBorde, a);
                inicio = -1f;
            }
        }
    }

    // Pista y plataformas no llevan bordes propios: el borde se detiene antes de entrar.
    // Otro rodaje sí: el borde se corta solo donde el punto queda más adentro que la línea
    // equivalente del otro rodaje, así las dos líneas se encuentran justo en la esquina.
    bool Bloqueado(Zona a, Vector2 p, float ins)
    {
        foreach (var b in zonas)
        {
            if (b == a) continue;
            float margen = b.esPista || b.esPlataforma ? 0.5f : -(ins + 0.05f);
            if (b.Contiene(p, margen)) return true;
        }
        return false;
    }

    // ---------- Malla ----------
    // Línea p0→p1 en XZ de mundo; se alarga medio ancho en cada punta para cerrar las esquinas.
    void Linea(Vector2 p0, Vector2 p1, float ancho, Zona a)
    {
        var d = (p1 - p0).normalized;
        p0 -= d * ancho * 0.5f; p1 += d * ancho * 0.5f;
        var r = new Vector2(d.y, -d.x) * (ancho * 0.5f);
        float y0 = Altura(p0, a) + alturaSobreRodaje, y1 = Altura(p1, a) + alturaSobreRodaje;
        int i = verts.Count;
        verts.Add(Mundo(p0 - r, y0)); verts.Add(Mundo(p1 - r, y1));
        verts.Add(Mundo(p1 + r, y1)); verts.Add(Mundo(p0 + r, y0));
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }

    static Vector3 Mundo(Vector2 p, float y) => new Vector3(p.x, y, p.y);

    // Cara superior más alta entre las zonas que contienen el punto (por si pista y rodaje no están al mismo nivel).
    float Altura(Vector2 p, Zona a)
    {
        float y = a.y;
        foreach (var b in zonas) if (b.Contiene(p, 0.01f)) y = Mathf.Max(y, b.y);
        return y;
    }

    // ---------- Utilidades ----------
    static Material MaterialPorDefecto()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogWarning("[Marcas rodaje] No encontré URP/Lit; usa tu propio material."); return null; }
        m = new Material(shader) { name = "MarcasRodaje" };
        m.SetColor("_BaseColor", new Color(0.93f, 0.72f, 0.12f));
        m.SetFloat("_Smoothness", 0.15f);
        GeneradorMarcasPista.CrearCarpeta(System.IO.Path.GetDirectoryName(RutaMaterial).Replace('\\', '/'));
        AssetDatabase.CreateAsset(m, RutaMaterial);
        AssetDatabase.SaveAssets();
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
