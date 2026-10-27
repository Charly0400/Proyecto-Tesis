// GeneradorP47: materiales URP/Lit y texturas generadas (camuflaje, insignias, bandas, líneas de paneles, suciedad).
// Esquemas: verde oliva/gris (USAAF), metal natural, Escuadrón 201 de la FAEM (Luzón, 1945) y Fuerza Aérea Mexicana
// (1949: bandas amarillas, número y matrícula con una fuente de trazos).
// Las texturas se guardan como PNG en Materials/P47/Texturas y se regeneran en cada generación.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class GeneradorP47
{
    Material mFus, mAla, mCola, mInterior, mPanel, mMetal, mMetalOscuro, mCromo, mLlanta, mRin, mNegro, mAmarillo,
             mVidrio, mDisco, mLuzRoja, mLuzVerde, mLuzBlanca, mEscape, mBuje;

    // Colores (sRGB) de la USAAF
    static readonly Color VerdeOliva = new Color(0.33f, 0.32f, 0.21f), GrisNeutro = new Color(0.55f, 0.56f, 0.56f);
    static readonly Color Aluminio = new Color(0.78f, 0.79f, 0.80f), Blanco = new Color(0.88f, 0.88f, 0.86f);
    static readonly Color AzulInsignia = new Color(0.08f, 0.13f, 0.30f), BlancoInsignia = new Color(0.92f, 0.92f, 0.90f);
    // Fuerza Aérea Mexicana: colores de la bandera (#006847, #ce1126), bandas de reconocimiento y letreros
    static readonly Color VerdeMexico = new Color(0.0f, 0.408f, 0.278f), RojoMexico = new Color(0.808f, 0.067f, 0.149f);
    static readonly Color NegroBanda = new Color(0.06f, 0.06f, 0.06f), AmarilloBanda = new Color(1.0f, 0.87f, 0.16f);
    static readonly Color FileteOscuro = new Color(0.17f, 0.16f, 0.08f), NegroLetras = new Color(0.05f, 0.05f, 0.05f);

    string letreroNumero, letreroMatricula;

    void CrearMateriales()
    {
        bool metal = Metal;
        letreroNumero = Numero;
        letreroMatricula = Matricula;
        // Pintura: textura de color + (en metal natural) mapa metálico/lisura
        var tf = Pintar(2048, 1024, PixelFuselaje);
        var ta = Pintar(2048, 1024, PixelAla);
        var tc = Pintar(1024, 1024, PixelCola);
        mFus = MatPintura("P47_Fuselaje", Textura("P47_T_Fuselaje", tf.color, 2048, 1024, false, TextureWrapMode.Clamp, TextureWrapMode.Repeat),
                          metal ? Textura("P47_T_Fuselaje_MS", tf.ms, 2048, 1024, true, TextureWrapMode.Clamp, TextureWrapMode.Repeat) : null);
        mAla = MatPintura("P47_Alas", Textura("P47_T_Alas", ta.color, 2048, 1024, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp),
                          metal ? Textura("P47_T_Alas_MS", ta.ms, 2048, 1024, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp) : null);
        mCola = MatPintura("P47_Cola", Textura("P47_T_Cola", tc.color, 1024, 1024, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp),
                           metal ? Textura("P47_T_Cola_MS", tc.ms, 1024, 1024, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp) : null);

        mInterior = MatLiso("P47_Interior", new Color(0.42f, 0.45f, 0.25f), 0f, 0.2f);
        mPanel = MatLiso("P47_Tablero", new Color(0.07f, 0.07f, 0.07f), 0f, 0.35f);
        mMetal = MatLiso("P47_Metal", new Color(0.62f, 0.63f, 0.65f), 0.75f, 0.5f);
        mMetalOscuro = MatLiso("P47_MetalOscuro", new Color(0.16f, 0.16f, 0.17f), 0.6f, 0.4f);
        mCromo = MatLiso("P47_Cromo", new Color(0.90f, 0.90f, 0.92f), 1f, 0.92f);
        mLlanta = MatLiso("P47_Llanta", new Color(0.05f, 0.05f, 0.05f), 0f, 0.18f);
        mRin = MatLiso("P47_Rin", new Color(0.55f, 0.56f, 0.58f), 0.5f, 0.45f);
        mNegro = MatLiso("P47_NegroHelice", new Color(0.04f, 0.04f, 0.04f), 0f, 0.35f);
        mAmarillo = MatLiso("P47_AmarilloPuntas", new Color(0.95f, 0.72f, 0.06f), 0f, 0.4f);
        mEscape = MatLiso("P47_Escape", new Color(0.24f, 0.20f, 0.17f), 0.55f, 0.3f);
        mBuje = EsFAM ? MatLiso("P47_Buje", AmarilloBanda, 0f, 0.45f) : MatLiso("P47_Buje", new Color(0.75f, 0.76f, 0.78f), 0.8f, 0.55f);
        mLuzRoja = MatLuz("P47_LuzRoja", new Color(1f, 0.08f, 0.05f));
        mLuzVerde = MatLuz("P47_LuzVerde", new Color(0.1f, 1f, 0.2f));
        mLuzBlanca = MatLuz("P47_LuzBlanca", new Color(1f, 0.97f, 0.9f));
        mVidrio = MatVidrio();
        mDisco = MatDisco(Textura("P47_T_DiscoHelice", PintarDisco(256), 256, 256, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp));
    }

    // ---------- Materiales ----------
    Material Mat(string nombreMat, string shader, Action<Material> config)
    {
        string ruta = $"{CarpetaMateriales}/{nombreMat}.mat";
        Material m = guardarAssets ? AssetDatabase.LoadAssetAtPath<Material>(ruta) : null;
        bool nuevo = m == null;
        if (nuevo)
        {
            var s = Shader.Find(shader);
            if (s == null) { Debug.LogError($"[P-47] No se encontró el shader '{shader}' (¿URP instalado?)"); return null; }
            m = new Material(s) { name = nombreMat };
        }
        config(m);
        if (!guardarAssets) return m;
        if (nuevo)
        {
            GeneradorMarcasPista.CrearCarpeta(CarpetaMateriales);
            AssetDatabase.CreateAsset(m, ruta);
        }
        else EditorUtility.SetDirty(m);
        return m;
    }

    Material MatLiso(string nombreMat, Color color, float metalico, float lisura) =>
        Mat(nombreMat, "Universal Render Pipeline/Lit", m =>
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metalico);
            m.SetFloat("_Smoothness", lisura);
        });

    Material MatPintura(string nombreMat, Texture2D color, Texture2D metalLisura) =>
        Mat(nombreMat, "Universal Render Pipeline/Lit", m =>
        {
            m.SetTexture("_BaseMap", color);
            m.SetColor("_BaseColor", Color.white);
            if (metalLisura != null)
            {
                m.SetTexture("_MetallicGlossMap", metalLisura);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.SetFloat("_Smoothness", 1f);
            }
            else
            {
                m.SetTexture("_MetallicGlossMap", null);
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.28f);
            }
        });

    Material MatLuz(string nombreMat, Color color) =>
        Mat(nombreMat, "Universal Render Pipeline/Lit", m =>
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.8f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2.5f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        });

    // Vidrio de la cabina: transparente, de dos caras (se ve también desde adentro en VR).
    Material MatVidrio() =>
        Mat("P47_Vidrio", "Universal Render Pipeline/Lit", m =>
        {
            // Mezcla alfa normal (no premultiplicada): los reflejos se atenúan y desde la cabina se ve bien el morro
            m.SetColor("_BaseColor", new Color(0.80f, 0.86f, 0.90f, 0.12f));
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.9f);
            Transparente(m, false);
            m.SetFloat("_Cull", (float)CullMode.Off);
        });

    // Disco borroso de la hélice: Unlit transparente; PropellerAnimator le cambia el alfa de _BaseColor.
    Material MatDisco(Texture2D tex) =>
        Mat("P47_DiscoHelice", "Universal Render Pipeline/Unlit", m =>
        {
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.6f));
            Transparente(m, false);
            m.SetFloat("_Cull", (float)CullMode.Off);
        });

    static void Transparente(Material m, bool premultiplicado)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", premultiplicado ? 1f : 0f);
        m.SetFloat("_SrcBlend", (float)(premultiplicado ? BlendMode.One : BlendMode.SrcAlpha));
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (premultiplicado) m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); else m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    // ---------- Texturas ----------
    Texture2D Textura(string nombreTex, Color32[] px, int w, int h, bool lineal, TextureWrapMode wrapU, TextureWrapMode wrapV)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, lineal) { name = nombreTex, wrapModeU = wrapU, wrapModeV = wrapV };
        tex.SetPixels32(px);
        tex.Apply(true);
        tex.anisoLevel = 4;
        if (!guardarAssets) return tex;

        string carpeta = $"{CarpetaMateriales}/Texturas";
        GeneradorMarcasPista.CrearCarpeta(carpeta);
        string ruta = $"{carpeta}/{nombreTex}.png";
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), ruta), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.sRGBTexture = !lineal;
        imp.mipmapEnabled = true;
        imp.wrapModeU = wrapU;
        imp.wrapModeV = wrapV;
        imp.anisoLevel = 4;
        imp.maxTextureSize = Mathf.Max(w, h);
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = nombreTex.Contains("Disco");
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // Color y mapa metálico (R) / lisura (A) para el esquema de metal natural.
    delegate void Pixel(float u, float v, out Color color, out bool pintado);

    (Color32[] color, Color32[] ms) Pintar(int w, int h, Pixel pixel)
    {
        var c = new Color32[w * h];
        var ms = new Color32[w * h];
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                float u = (px + 0.5f) / w, v = (py + 0.5f) / h;
                pixel(u, v, out Color col, out bool pintado);
                c[py * w + px] = col;
                // Pintura: mate, no metálica. Aluminio: metálico y pulido con algo de variación.
                byte liso = (byte)(pintado ? 70 : 150 + 30 * Ruido(u * 300f, v * 150f));
                ms[py * w + px] = new Color32(pintado ? (byte)0 : (byte)215, 0, 0, liso);
            }
        return (c, ms);
    }

    bool Metal => esquema != Esquema.VerdeOlivaGris;
    bool Es201 => esquema == Esquema.Escuadron201;
    bool EsFAM => esquema == Esquema.FAM1949;
    bool Mexicano => Es201 || EsFAM;
    string Numero => string.IsNullOrWhiteSpace(numero) ? "" : numero.Trim();
    string Matricula => "10" + Numero.PadLeft(2, '0');   // FAM: PZT-1020 para el "20"

    // Base (verde oliva arriba / gris neutro abajo, o aluminio) con suciedad leve.
    Color Base(bool arriba, float suavidad, float u, float v, out bool pintado)
    {
        pintado = !Metal;
        Color c = Metal ? Aluminio : Color.Lerp(GrisNeutro, VerdeOliva, arriba ? 1f : suavidad);
        float n = 0.94f + 0.08f * Ruido(u * 90f, v * 45f) + 0.03f * Ruido(u * 700f, v * 350f);
        return c * n;
    }

    static readonly Curva Demarcacion = new Curva(0.62f, -0.30f, 3.0f, -0.25f, 6.0f, -0.12f, 9.0f, 0f, 10.4f, 0.05f);

    void PixelFuselaje(float u, float v, out Color c, out bool pintado)
    {
        pintado = true;
        if (u < 0.01f) { c = new Color(0.12f, 0.12f, 0.11f); return; }   // interior de la toma de la cofia
        float x = FusX0 + (u - 0.01f) / 0.99f * (FusX1 - FusX0);
        float t = v * 360f - 90f;
        var s = SeccionFus(x, t * Mathf.Deg2Rad);
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.05f, s.y - Demarcacion[x]));
        c = Base(false, k, u, v, out pintado);
        // Panel antirreflejo delante del parabrisas (metal natural)
        if (Metal && x < CabX0 && Mathf.Abs(s.x) < 0.24f && s.y > 0.2f) { c = VerdeOliva * 0.9f; pintado = true; }
        // Bandas blancas de identificación (Europa 1943-44): 60 cm en la cofia
        if (bandasBlancas && !Metal && x < FusX0 + 0.60f) c = Blanco;
        // Escuadrón 201: nariz blanca festoneada y banda negra con filetes blancos delante de la cola
        if (Es201)
        {
            if (x < BordeNariz(t)) { c = Blanco; pintado = true; }
            if (bandasNegras && Banda(x, XBandaFus, AnchoBanda, FileteBanda, NegroBanda, BlancoInsignia, ref c)) pintado = true;
        }
        bool costado = Mathf.Abs(s.x) > 0.3f * FusAncho[x];
        if (EsFAM)
        {
            // FAM 1949: nariz blanca cerrada por un chevrón rojo (en los costados llega 80 cm atrás) y banda amarilla
            float borde = FusX0 + 0.10f + 0.68f * (1f - Mathf.Abs(Mathf.Sin(t * Mathf.Deg2Rad)));
            if (x < borde + 0.045f) { c = x < borde ? Blanco : RojoMexico; pintado = true; }
            if (Banda(x, XBandaFAM, 0.95f, FileteFAM, AmarilloBanda, FileteOscuro, ref c)) pintado = true;
            if (costado)
            {
                // Triángulo sobre la banda, número detrás de la cabina y flor de lis detrás de la cofia. Los letreros
                // se leen de la nariz a la cola en el costado izquierdo y al revés en el derecho.
                int tri = TrianguloMexicano(x - XBandaFAM, s.y - 0.14f, 0.45f);
                if (tri > 0) { c = ColorTriangulo(tri); pintado = true; }
                float h = s.x < 0f ? x : -x, inicio = (s.x < 0f ? XNumero : -XNumero) - AnchoLetrero(letreroNumero, 0.88f) * 0.5f;
                if (Letrero(letreroNumero, h - inicio, s.y + 0.34f, 0.88f, 0.15f) || FlorDeLis((x - 2.68f) / 0.53f, (s.y + 0.08f) / 0.64f))
                { c = NegroLetras; pintado = true; }
            }
            // Panel antirreflejo hasta el labio de la cofia y franja oscura sobre el lomo hasta la deriva
            if ((x < CabX0 && Mathf.Abs(s.x) < 0.24f && s.y > 0.2f) || (x > CabAtras - 0.1f && x < 9.2f && Mathf.Abs(s.x) < 0.075f && s.y > 0f))
            { c = VerdeOliva * 0.85f; pintado = true; }
        }
        // Insignia de la USAAF en los costados
        if (costado && !EsFAM)
        {
            int ins = Insignia(x - 7.05f, s.y - 0.05f, 0.44f);
            if (ins == 1) { c = AzulInsignia; pintado = true; }
            else if (ins == 2) { c = BlancoInsignia; pintado = true; }
        }
        // Líneas de paneles
        foreach (float xl in LineasFus) if (Mathf.Abs(x - xl) < 0.005f) c *= 0.8f;
        if (x > CowlX1 && (Mathf.Abs(Mathf.DeltaAngle(t, 0f)) < 0.3f || Mathf.Abs(Mathf.DeltaAngle(t, 180f)) < 0.3f)) c *= 0.82f;
        // Hollín de los escapes hacia atrás por la panza
        if (x > 2.8f && x < 5.2f && s.y < -0.45f && Mathf.Abs(s.x) > 0.18f && Mathf.Abs(s.x) < 0.55f)
            c *= 1f - 0.35f * (1f - Mathf.InverseLerp(2.8f, 5.2f, x)) * Ruido(x * 6f, t * 0.3f);
        c.a = 1f;
    }

    static readonly float[] LineasFus = { 1.22f, CowlX0, CowlX1, IntX1, CabX0, CabAtras, 6.40f, 7.25f, 8.10f, 8.95f, 9.70f };

    void PixelAla(float u, float v, out Color c, out bool pintado)
    {
        float lado = u < 0.5f ? -1f : 1f, s = Mathf.Abs(u - 0.5f) * 2f * Ala.semi;
        float cara = v < 0.5f ? -1f : 1f, xi = cara < 0f ? 1f - 2f * v : 2f * v - 1f;
        float x = Ala.BordeAtaque(s) + xi * Ala.Cuerda(s);
        bool arriba = cara > 0f || xi < 0.03f;   // el color de arriba envuelve un poco el borde de ataque
        c = Base(arriba, 0f, u, v, out pintado);
        // Insignias: arriba del ala izquierda y abajo del ala derecha (norma USAAF). El Escuadrón 201 llevaba el
        // triángulo de la Fuerza Aérea Mexicana en las otras dos posiciones (arriba del ala derecha).
        float sc = 4.35f, xc = Ala.BordeAtaque(sc) + 0.45f * Ala.Cuerda(sc);
        if (EsFAM)
        {
            // FAM 1949: triángulo en las dos alas, arriba y abajo; banda amarilla sobre las ametralladoras y un
            // cuadro rojo en la raíz de cada flap
            float st = 4.55f, xt = Ala.BordeAtaque(st) + 0.45f * Ala.Cuerda(st);
            int tri = TrianguloMexicano(s - st, xt - x, 0.68f);
            if (tri > 0) { c = ColorTriangulo(tri); pintado = true; }
            if (Banda(s, SBandaFAM, 0.95f, FileteFAM, AmarilloBanda, FileteOscuro, ref c)) pintado = true;
            if (cara > 0f && Mathf.Abs(s - 1.15f) < 0.15f && Mathf.Abs(xi - 0.86f) < 0.15f / Ala.Cuerda(s)) { c = RojoMexico; pintado = true; }
        }
        else if ((lado < 0f && cara > 0f) || (lado > 0f && cara < 0f))
        {
            int ins = Insignia(s - sc, xc - x, 0.58f);
            if (ins == 1) { c = AzulInsignia; pintado = true; }
            else if (ins == 2) { c = BlancoInsignia; pintado = true; }
        }
        else if (Es201)
        {
            int tri = TrianguloMexicano(s - sc, xc - x, 0.74f);
            if (tri > 0) { c = ColorTriangulo(tri); pintado = true; }
        }
        if (Es201 && bandasNegras && Banda(s, SBandaAla, AnchoBanda, FileteBanda, NegroBanda, BlancoInsignia, ref c)) pintado = true;
        // Líneas de paneles: estaciones, larguero delantero y tapas de las ametralladoras
        foreach (float sl in LineasAla) if (Mathf.Abs(s - sl) < 0.006f) c *= 0.8f;
        if (Mathf.Abs(xi - 0.10f) < 0.0035f) c *= 0.82f;
        if (cara > 0f && s > 2.40f && s < 3.42f && (Mathf.Abs(xi - 0.45f) < 0.0035f)) c *= 0.8f;
        c.a = 1f;
    }

    static readonly float[] LineasAla = { 0.62f, 1.30f, 2.40f, 3.42f, FlapS1 + 0.02f, AlerS1 + 0.05f };

    void PixelCola(float u, float v, out Color c, out bool pintado)
    {
        float cara = v < 0.5f ? -1f : 1f, xi = cara < 0f ? 1f - 2f * v : 2f * v - 1f;
        if (u < 0.5f)
        {
            float s = Mathf.Abs(u - 0.25f) * 4f * Estab.semi;
            c = Base(cara > 0f || xi < 0.03f, 0f, u, v, out pintado);
            if (bandasBlancas && !Metal && s > 1.05f && s < 1.35f) c = Blanco;
            if (Mathf.Abs(xi - Estab.fRef) < 0.004f) c *= 0.8f;
        }
        else
        {
            float s = (u - 0.5f) * 2f * Deriva.semi;
            c = Base(true, 1f, u, v, out pintado);
            if (bandasBlancas && !Metal && s > 0.62f && s < 0.92f) c = Blanco;
            // Fuerza Aérea Mexicana: timón con los colores de la bandera, de la bisagra hacia atrás verde, blanco y rojo
            if (Mexicano && xi > Deriva.fRef && s < TimonS1 + 0.01f)
            {
                float f = (xi - Deriva.fRef) / (1f - Deriva.fRef);
                c = f < 1f / 3f ? VerdeMexico : f < 2f / 3f ? BlancoInsignia : RojoMexico;
                pintado = true;
            }
            // FAM 1949: matrícula en dos renglones (PZT / 1020) en la parte fija de la deriva
            float x = Deriva.BordeAtaque(s) + xi * Deriva.Cuerda(s);
            if (EsFAM && xi < Deriva.fRef && s > 0.8f && s < 1.25f && x > 9.4f && x < 10.3f)
            {
                float h = cara < 0f ? x : -x;
                foreach (var (texto, base0) in new[] { ("PZT", 1.06f), (letreroMatricula, 0.84f) })
                {
                    float inicio = (cara < 0f ? 9.86f : -9.86f) - AnchoLetrero(texto, 0.15f) * 0.5f;
                    if (Letrero(texto, h - inicio, s - base0, 0.15f, 0.026f)) { c = NegroLetras; pintado = true; }
                }
            }
            if (Mathf.Abs(xi - Deriva.fRef) < 0.004f) c *= 0.8f;
        }
        c.a = 1f;
    }

    // Insignia de la USAAF (1943-47): estrella blanca en círculo azul, barras blancas y borde azul.
    // (px, pz) en metros desde el centro; 'pz' hacia arriba (o hacia el borde de ataque en las alas). 0 = nada, 1 = azul, 2 = blanco.
    static int Insignia(float px, float pz, float r)
    {
        float ax = Mathf.Abs(px);
        float d2 = px * px + pz * pz;
        if (d2 > 4.6f * r * r && ax > 2.2f * r) return 0;
        bool barra = ax < 2f * r && pz > -0.191f * r && pz < 0.309f * r;
        if (d2 < r * r) return Estrella(px / r, pz / r) ? 2 : 1;
        if (barra) return 2;
        float e = 0.125f * r;
        bool borde = d2 < (r + e) * (r + e) || (ax < 2f * r + e && pz > -0.191f * r - e && pz < 0.309f * r + e);
        return borde ? 1 : 0;
    }

    // Insignia de la Fuerza Aérea Mexicana: triángulo equilátero con el vértice hacia abajo (hacia el borde de salida en
    // las alas), borde rojo, campo blanco y triángulo verde al centro, con las proporciones de Roundel_of_Mexico.svg.
    // (px, pz) en metros desde el centro, 'pz' hacia arriba (o hacia el borde de ataque); R = radio del triángulo exterior.
    // 0 = nada, 1 = rojo, 2 = blanco, 3 = verde.
    static int TrianguloMexicano(float px, float pz, float R)
    {
        float d = Mathf.Max(pz, Mathf.Max(0.866f * px - 0.5f * pz, -0.866f * px - 0.5f * pz));   // 'distancia' a los lados
        float apotema = 0.5f * R;
        if (d > apotema) return 0;
        return d > 0.654f * apotema ? 1 : d > 0.296f * apotema ? 2 : 3;
    }

    static Color ColorTriangulo(int parte) => parte == 1 ? RojoMexico : parte == 2 ? BlancoInsignia : VerdeMexico;

    // Escuadrón 201: bandas negras de reconocimiento (fuselaje delante de la cola y alas por dentro de la insignia)
    const float XBandaFus = 8.35f, SBandaAla = 2.70f, AnchoBanda = 0.50f, FileteBanda = 0.05f;
    // FAM 1949: bandas amarillas (fuselaje delante de la cola y alas sobre las ametralladoras) y número detrás de la cabina
    const float XBandaFAM = 7.70f, SBandaFAM = 2.92f, FileteFAM = 0.025f, XNumero = 6.05f;

    // Banda de color con filetes a los dos lados, centrada en 'centro' (m). Devuelve si pintó.
    static bool Banda(float pos, float centro, float ancho, float filete, Color color, Color colorFilete, ref Color c)
    {
        float d = Mathf.Abs(pos - centro);
        if (d < ancho * 0.5f) { c = color; return true; }
        if (d < ancho * 0.5f + filete) { c = colorFilete; return true; }
        return false;
    }

    // ---------- Letreros: fuente de trazos (números, PZT) ----------
    // Cada glifo son polilíneas del eje del trazo en una caja de 0.6 x 1 (alto = 1).
    static Dictionary<char, Vector2[][]> glifos;

    static Vector2[] Arco(float cx, float cy, float rx, float ry, float a0, float a1, int n = 20)
    {
        var p = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, (float)i / n) * Mathf.Deg2Rad;
            p[i] = new Vector2(cx + rx * Mathf.Cos(a), cy + ry * Mathf.Sin(a));
        }
        return p;
    }

    static Vector2[] Linea(params float[] xy)
    {
        var p = new Vector2[xy.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
        return p;
    }

    static Vector2[] Unir(params Vector2[][] partes)
    {
        var l = new List<Vector2>();
        foreach (var p in partes) l.AddRange(p);
        return l.ToArray();
    }

    static Dictionary<char, Vector2[][]> CrearGlifos()
    {
        var g = new Dictionary<char, Vector2[][]>
        {
            ['0'] = new[] { Arco(0.30f, 0.50f, 0.21f, 0.41f, 0f, 360f, 36) },
            ['1'] = new[] { Linea(0.14f, 0.72f, 0.34f, 0.91f, 0.34f, 0.09f) },
            ['2'] = new[] { Unir(Arco(0.30f, 0.69f, 0.21f, 0.22f, 160f, -30f), Linea(0.09f, 0.09f, 0.51f, 0.09f)) },
            ['3'] = new[] { Arco(0.29f, 0.70f, 0.19f, 0.21f, 150f, -90f), Arco(0.29f, 0.29f, 0.22f, 0.20f, 90f, -150f) },
            ['4'] = new[] { Linea(0.42f, 0.09f, 0.42f, 0.91f, 0.09f, 0.34f, 0.53f, 0.34f) },
            ['5'] = new[] { Unir(Linea(0.49f, 0.91f, 0.13f, 0.91f, 0.11f, 0.55f), Arco(0.30f, 0.31f, 0.21f, 0.23f, 125f, -150f)) },
            ['6'] = new[] { Unir(Arco(0.30f, 0.50f, 0.21f, 0.41f, 65f, 180f), Arco(0.30f, 0.30f, 0.21f, 0.21f, 180f, 540f, 28)) },
            ['7'] = new[] { Linea(0.09f, 0.91f, 0.51f, 0.91f, 0.21f, 0.09f) },
            ['8'] = new[] { Arco(0.30f, 0.71f, 0.18f, 0.19f, 0f, 360f, 28), Arco(0.30f, 0.29f, 0.21f, 0.21f, 0f, 360f, 28) },
            ['P'] = new[] { Unir(Linea(0.11f, 0.09f, 0.11f, 0.91f), Arco(0.30f, 0.70f, 0.21f, 0.21f, 90f, -90f), Linea(0.11f, 0.49f)) },
            ['Z'] = new[] { Linea(0.09f, 0.91f, 0.51f, 0.91f, 0.09f, 0.09f, 0.51f, 0.09f) },
            ['T'] = new[] { Linea(0.04f, 0.91f, 0.56f, 0.91f), Linea(0.30f, 0.91f, 0.30f, 0.09f) },
            ['-'] = new[] { Linea(0.12f, 0.50f, 0.48f, 0.50f) },
        };
        // El 9 es el 6 girado media vuelta
        var nueve = new List<Vector2>();
        foreach (var p in g['6'][0]) nueve.Add(new Vector2(0.6f - p.x, 1f - p.y));
        g['9'] = new[] { nueve.ToArray() };
        return g;
    }

    static float AnchoLetrero(string texto, float alto) => texto.Length > 0 ? (0.72f * texto.Length - 0.12f) * alto : 0f;

    // ¿El punto cae en el trazo del letrero? h = distancia horizontal desde el inicio del texto, w = altura sobre la
    // base (m); 'trazo' = grosor de la línea (m).
    static bool Letrero(string texto, float h, float w, float alto, float trazo)
    {
        if (string.IsNullOrEmpty(texto) || h < 0f || w < 0f || w > alto) return false;
        float paso = 0.72f * alto;
        int i = Mathf.FloorToInt(h / paso);
        if (i >= texto.Length) return false;
        if (glifos == null) glifos = CrearGlifos();
        if (!glifos.TryGetValue(char.ToUpperInvariant(texto[i]), out var glifo)) return false;
        var p = new Vector2((h - i * paso) / alto, w / alto);
        if (p.x > 0.6f) return false;
        float r = 0.5f * trazo / alto;
        foreach (var linea in glifo)
            for (int k = 0; k < linea.Length - 1; k++)
                if (DistSegmento2(p, linea[k], linea[k + 1]) < r * r) return true;
        return false;
    }

    static float DistSegmento2(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
        return (p - (a + ab * t)).sqrMagnitude;
    }

    // Flor de lis (emblema del costado en el perfil de 1949): pétalo central en punta, dos pétalos laterales que se
    // curvan hacia afuera y abajo, banda horizontal y remate inferior. (px, pz) en unidades del alto, pz = 0 abajo.
    static readonly Vector2[] PetaloLateral = Linea(0.07f, 0.42f, 0.17f, 0.55f, 0.27f, 0.66f, 0.36f, 0.67f, 0.42f, 0.60f, 0.42f, 0.50f, 0.36f, 0.45f);
    static readonly Vector2[] RemateLateral = Linea(0.05f, 0.30f, 0.15f, 0.24f, 0.21f, 0.14f, 0.18f, 0.06f, 0.12f, 0.07f);

    static bool FlorDeLis(float px, float pz)
    {
        if (pz < 0f || pz > 1f || Mathf.Abs(px) > 0.5f) return false;
        float ax = Mathf.Abs(px);
        var p = new Vector2(ax, pz);
        if (pz > 0.36f && ax < 0.115f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, (pz - 0.36f) / 0.64f + 0.12f)), 0.85f)) return true;
        if (pz < 0.40f && pz > 0.02f && ax < 0.075f * Mathf.Sin(Mathf.PI * (pz - 0.02f) / 0.38f)) return true;
        if (Mathf.Abs(pz - 0.36f) < 0.04f && ax < 0.27f) return true;
        for (int k = 0; k < PetaloLateral.Length - 1; k++)
            if (DistSegmento2(p, PetaloLateral[k], PetaloLateral[k + 1]) < 0.05f * 0.05f) return true;
        for (int k = 0; k < RemateLateral.Length - 1; k++)
            if (DistSegmento2(p, RemateLateral[k], RemateLateral[k + 1]) < 0.035f * 0.035f) return true;
        return false;
    }

    // Borde trasero de la nariz blanca (estación, m): 10 festones redondos alrededor de la cofia.
    static float BordeNariz(float tGrados)
    {
        float q = 2f * Mathf.Repeat(tGrados / 36f, 1f) - 1f;
        return 0.98f + 0.14f * Mathf.Sqrt(1f - q * q);
    }

    // Estrella de cinco puntas con una punta hacia arriba, radio 1 (punto dentro del polígono).
    static bool Estrella(float x, float y)
    {
        bool dentro = false;
        const int n = 10;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float ai = Mathf.PI * 0.5f + Mathf.PI * i / 5f, aj = Mathf.PI * 0.5f + Mathf.PI * j / 5f;
            float ri = i % 2 == 0 ? 1f : 0.382f, rj = j % 2 == 0 ? 1f : 0.382f;
            float xi = Mathf.Cos(ai) * ri, yi = Mathf.Sin(ai) * ri, xj = Mathf.Cos(aj) * rj, yj = Mathf.Sin(aj) * rj;
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) dentro = !dentro;
        }
        return dentro;
    }

    // Disco borroso: gris translúcido con un anillo amarillo donde giran las puntas.
    static Color32[] PintarDisco(int n)
    {
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                Color c;
                float rTip = (RadioHelice - 0.10f) / RadioHelice;
                if (r > 1f) c = new Color(0f, 0f, 0f, 0f);
                else if (r > rTip) c = new Color(0.95f, 0.72f, 0.06f, 0.75f);
                else c = new Color(0.08f, 0.08f, 0.08f, Mathf.SmoothStep(0f, 0.5f, Mathf.InverseLerp(0.12f, 0.25f, r)));
                px[y * n + x] = c;
            }
        return px;
    }

    static float Ruido(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi, ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), ux), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), ux), uy);
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }
}
#endif
