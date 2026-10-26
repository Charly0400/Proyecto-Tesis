// GeneradorP47: materiales URP/Lit y texturas generadas (camuflaje, insignias, bandas, líneas de paneles, suciedad).
// Las texturas se guardan como PNG en Materials/P47/Texturas y se regeneran en cada generación.
#if UNITY_EDITOR
using System;
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

    void CrearMateriales()
    {
        bool metal = esquema == Esquema.MetalNatural;
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
        mBuje = MatLiso("P47_Buje", new Color(0.75f, 0.76f, 0.78f), 0.8f, 0.55f);
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

    bool Metal => esquema == Esquema.MetalNatural;

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
        // Insignia en los costados
        if (Mathf.Abs(s.x) > 0.3f * FusAncho[x])
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
        // Insignias: arriba del ala izquierda y abajo del ala derecha (norma USAAF)
        if ((lado < 0f && cara > 0f) || (lado > 0f && cara < 0f))
        {
            float sc = 4.35f, xc = Ala.BordeAtaque(sc) + 0.45f * Ala.Cuerda(sc);
            int ins = Insignia(s - sc, xc - x, 0.58f);
            if (ins == 1) { c = AzulInsignia; pintado = true; }
            else if (ins == 2) { c = BlancoInsignia; pintado = true; }
        }
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
