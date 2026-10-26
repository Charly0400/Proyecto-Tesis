// GeneradorP47: utilidades de geometría (curvas, mallas con UV propias, superficies sustentadoras, fuselaje).
// Coordenadas del avión: x = estación (m desde la punta del buje hacia atrás), y = lado (derecha +),
// z = altura sobre la línea de empuje. P() las pasa a ejes locales de Unity (+Z = morro, +Y = arriba).
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class GeneradorP47
{
    // Origen del prefab: sobre la línea de empuje, en la estación del centro de gravedad aproximado.
    public const float EstacionOrigen = 3.35f;
    public static Vector3 P(float x, float y, float z) => new Vector3(y, z, EstacionOrigen - x);

    // ---------- Curva suave por puntos (cúbica monótona, sin oscilaciones) ----------
    public sealed class Curva
    {
        readonly float[] xs, ys, ms;
        public Curva(params float[] pares)
        {
            int n = pares.Length / 2;
            xs = new float[n]; ys = new float[n]; ms = new float[n];
            for (int i = 0; i < n; i++) { xs[i] = pares[2 * i]; ys[i] = pares[2 * i + 1]; }
            var d = new float[Mathf.Max(1, n - 1)];
            for (int i = 0; i < n - 1; i++) d[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
            for (int i = 0; i < n; i++)
            {
                if (i == 0) ms[i] = n > 1 ? d[0] : 0f;
                else if (i == n - 1) ms[i] = d[n - 2];
                else ms[i] = d[i - 1] * d[i] <= 0f ? 0f : (d[i - 1] + d[i]) * 0.5f;
            }
            for (int i = 0; i < n - 1; i++)   // Fritsch–Carlson
            {
                if (Mathf.Abs(d[i]) < 1e-9f) { ms[i] = ms[i + 1] = 0f; continue; }
                float a = ms[i] / d[i], b = ms[i + 1] / d[i], h = a * a + b * b;
                if (h > 9f) { float k = 3f / Mathf.Sqrt(h); ms[i] = k * a * d[i]; ms[i + 1] = k * b * d[i]; }
            }
        }
        public float this[float x]
        {
            get
            {
                int n = xs.Length;
                if (x <= xs[0]) return ys[0];
                if (x >= xs[n - 1]) return ys[n - 1];
                int i = 0;
                while (x > xs[i + 1]) i++;
                float h = xs[i + 1] - xs[i], t = (x - xs[i]) / h, t2 = t * t, t3 = t2 * t;
                return (2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * h * ms[i]
                     + (-2 * t3 + 3 * t2) * ys[i + 1] + (t3 - t2) * h * ms[i + 1];
            }
        }
    }

    // ---------- Malla con UV propias ----------
    public sealed class Malla
    {
        readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();
        public Matrix4x4 Matriz = Matrix4x4.identity;   // se aplica a todo lo que se agrega
        public bool Vacia => v.Count == 0;

        public int V(Vector3 p, Vector3 normal, Vector2 coord)
        {
            v.Add(Matriz.MultiplyPoint3x4(p));
            n.Add(Matriz.MultiplyVector(normal).normalized);
            uv.Add(coord);
            return v.Count - 1;
        }

        public void T(int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }

        // Rejilla de puntos P[fila, columna]. Con 'centro' se orienta sola (normales lejos del centro de cada fila).
        // 'costura': la primera y la última columna son el mismo punto (anillo cerrado con UV continuas).
        public void Rejilla(Vector3[,] P, Vector2[,] UV, Func<int, Vector3> centro = null, bool invertir = false,
                            bool costura = false, Func<int, int, bool> incluir = null)
        {
            int ni = P.GetLength(0), nj = P.GetLength(1);
            var nor = new Vector3[ni, nj];
            var degenerado = new bool[ni, nj];
            float suma = 0f;
            for (int i = 0; i < ni; i++)
                for (int j = 0; j < nj; j++)
                {
                    Vector3 di = P[Mathf.Min(i + 1, ni - 1), j] - P[Mathf.Max(i - 1, 0), j];
                    Vector3 dj;
                    if (costura && (j == 0 || j == nj - 1)) dj = P[i, 1] - P[i, nj - 2];
                    else dj = P[i, Mathf.Min(j + 1, nj - 1)] - P[i, Mathf.Max(j - 1, 0)];
                    Vector3 c = Vector3.Cross(di, dj);
                    degenerado[i, j] = c.sqrMagnitude < 1e-16f;
                    nor[i, j] = c.normalized;
                    if (centro != null && !degenerado[i, j]) suma += Vector3.Dot(nor[i, j], P[i, j] - centro(i));
                }
            // Polos y puntas (todos los puntos de la fila en el mismo lugar): promedio de la fila vecina
            for (int i = 0; i < ni; i++)
                for (int j = 0; j < nj; j++)
                {
                    if (!degenerado[i, j]) continue;
                    Vector3 prom = Vector3.zero;
                    foreach (int iv in new[] { i - 1, i + 1 })
                        if (iv >= 0 && iv < ni)
                            for (int jv = 0; jv < nj; jv++) if (!degenerado[iv, jv]) prom += nor[iv, jv];
                    nor[i, j] = prom.sqrMagnitude > 1e-12f ? prom.normalized : Vector3.up;
                }
            if (centro != null) invertir = suma < 0f;
            var idx = new int[ni, nj];
            for (int i = 0; i < ni; i++)
                for (int j = 0; j < nj; j++)
                    idx[i, j] = V(P[i, j], invertir ? -nor[i, j] : nor[i, j], UV != null ? UV[i, j] : Vector2.zero);
            for (int i = 0; i < ni - 1; i++)
                for (int j = 0; j < nj - 1; j++)
                {
                    if (incluir != null && !incluir(i, j)) continue;
                    int a = idx[i, j], b = idx[i + 1, j], c = idx[i + 1, j + 1], d = idx[i, j + 1];
                    // Unity dibuja la cara hacia Cross(b-a, c-a) = Cross(di, dj)
                    if (!invertir) { T(a, b, c); T(a, c, d); } else { T(a, c, b); T(a, d, c); }
                }
        }

        // Tapa plana en abanico (cierra un perfil). Mira hacia 'haciaFuera'.
        public void Tapa(IList<Vector3> perfil, Vector3 haciaFuera, Vector2 coord)
        {
            if (perfil.Count < 3) return;
            Vector3 c = Vector3.zero;
            foreach (var p in perfil) c += p;
            c /= perfil.Count;
            int ic = V(c, haciaFuera, coord);
            var ids = new int[perfil.Count];
            for (int k = 0; k < perfil.Count; k++) ids[k] = V(perfil[k], haciaFuera, coord);
            for (int k = 0; k < perfil.Count - 1; k++) Tri(ic, ids[k], ids[k + 1], c, perfil[k], perfil[k + 1], haciaFuera);
        }

        void Tri(int a, int b, int c, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 haciaFuera)
        {
            if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), haciaFuera) >= 0f) T(a, b, c); else T(a, c, b);
        }

        public void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 coord = default)
        {
            int a = V(p0, normal, coord), b = V(p1, normal, coord), c = V(p2, normal, coord), d = V(p3, normal, coord);
            Tri(a, b, c, p0, p1, p2, normal);
            Tri(a, c, d, p0, p2, p3, normal);
        }

        public void Caja(Vector3 centro, Vector3 tam, Quaternion rot, Vector2 coord = default)
        {
            Vector3 x = rot * Vector3.right * (tam.x * 0.5f), y = rot * Vector3.up * (tam.y * 0.5f), z = rot * Vector3.forward * (tam.z * 0.5f);
            void Cara(Vector3 c, Vector3 a, Vector3 b) => Quad(c - a - b, c + a - b, c + a + b, c - a + b, c - centro, coord);
            Cara(centro + x, y, z); Cara(centro - x, y, z);
            Cara(centro + y, x, z); Cara(centro - y, x, z);
            Cara(centro + z, x, y); Cara(centro - z, x, y);
        }

        // Tubo (tronco de cono) de p0 a p1 con sombreado suave.
        public void Tubo(Vector3 p0, Vector3 p1, float r0, float r1, int lados = 12, bool tapas = true, Vector2 coord = default)
        {
            Vector3 eje = p1 - p0;
            if (eje.sqrMagnitude < 1e-10f) return;
            Vector3 d = eje.normalized;
            Vector3 a = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized, b = Vector3.Cross(d, a);
            var P = new Vector3[2, lados + 1];
            var U = new Vector2[2, lados + 1];
            var anillo0 = new List<Vector3>(); var anillo1 = new List<Vector3>();
            for (int k = 0; k <= lados; k++)
            {
                float ang = Mathf.PI * 2f * k / lados;
                Vector3 r = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
                P[0, k] = p0 + r * r0; P[1, k] = p1 + r * r1;
                U[0, k] = coord; U[1, k] = coord;
                anillo0.Add(P[0, k]); anillo1.Add(P[1, k]);
            }
            Rejilla(P, U, i => i == 0 ? p0 : p1, costura: true);
            if (!tapas) return;
            if (r0 > 1e-4f) Tapa(anillo0, -d, coord);
            if (r1 > 1e-4f) Tapa(anillo1, d, coord);
        }

        // Elipsoide con semiejes 'radios' (en ejes de 'rot').
        public void Elipsoide(Vector3 centro, Vector3 radios, Quaternion rot, int lat = 10, int lon = 16, Vector2 coord = default)
        {
            var P = new Vector3[lat + 1, lon + 1];
            var U = new Vector2[lat + 1, lon + 1];
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat - Mathf.PI * 0.5f;
                for (int j = 0; j <= lon; j++)
                {
                    float ph = Mathf.PI * 2f * j / lon;
                    var l = new Vector3(Mathf.Cos(th) * Mathf.Cos(ph) * radios.x, Mathf.Sin(th) * radios.y, Mathf.Cos(th) * Mathf.Sin(ph) * radios.z);
                    P[i, j] = centro + rot * l; U[i, j] = coord;
                }
            }
            Rejilla(P, U, i => centro, costura: true);
        }

        // Disco plano (una cara, o las dos).
        public void Disco(Vector3 centro, Vector3 normal, float radio, int lados = 24, bool dosCaras = false, Vector2 coord = default)
        {
            Vector3 d = normal.normalized;
            Vector3 a = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized, b = Vector3.Cross(d, a);
            var anillo = new List<Vector3>();
            for (int k = 0; k <= lados; k++)
            {
                float ang = Mathf.PI * 2f * k / lados;
                anillo.Add(centro + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * radio);
            }
            Tapa(anillo, d, coord);
            if (dosCaras) Tapa(anillo, -d, coord);
        }

        public Mesh CrearMesh(string nombre)
        {
            var mesh = new Mesh { name = nombre };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    // ---------- Perfil aerodinámico NACA de 4 dígitos (borde de salida cerrado) ----------
    public static float NacaEspesor(float xi, float t)
    {
        xi = Mathf.Clamp01(xi);
        return 5f * t * (0.2969f * Mathf.Sqrt(xi) - 0.126f * xi - 0.3516f * xi * xi + 0.2843f * xi * xi * xi - 0.1036f * xi * xi * xi * xi);
    }

    public static float NacaCurvatura(float xi, float m, float p)
    {
        if (m <= 0f) return 0f;
        xi = Mathf.Clamp01(xi);
        return xi < p ? m / (p * p) * (2f * p * xi - xi * xi) : m / ((1f - p) * (1f - p)) * (1f - 2f * p + 2f * p * xi - xi * xi);
    }

    // ---------- Superficie sustentadora (ala, estabilizador, deriva) ----------
    // Planta superelíptica con una línea recta de referencia a la fracción 'fRef' de la cuerda (la de la bisagra
    // en los timones). 's' = distancia a lo largo de la envergadura desde la raíz (siempre >= 0).
    public sealed class Sustentadora
    {
        public float semi, c0, nPlanta = 2f, fRef = 0.36f, xRef, zRef, flecha;   // flecha: m de x por m de envergadura
        public float diedro, incRaiz, incPunta, espRaiz = 0.12f, espPunta = 0.09f, sEspRaiz;
        public float curvatura, posCurvatura = 0.4f;
        public bool vertical;                   // deriva: la envergadura va hacia arriba y el espesor hacia los lados
        public float sMax = 0.997f;             // fracción de la envergadura donde se cierra la punta

        public float Cuerda(float s)
        {
            float e = Mathf.Clamp01(s / semi);
            return c0 * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(e, nPlanta)), 1f / nPlanta);
        }
        public float XRef(float s) => xRef + s * flecha;
        public float BordeAtaque(float s) => XRef(s) - fRef * Cuerda(s);
        public float Espesor(float s) => Mathf.Lerp(espRaiz, espPunta, Mathf.InverseLerp(sEspRaiz, semi, s));
        public float Incidencia(float s) => Mathf.Lerp(incRaiz, incPunta, Mathf.Clamp01(s / semi));

        // Fracción de cuerda de un punto de estación x en la sección s.
        public float Xi(float s, float x) => (x - BordeAtaque(s)) / Mathf.Max(1e-4f, Cuerda(s));

        // Punto de la cara superior (cara = +1) o inferior (-1). 'lado' = +1 derecha, -1 izquierda.
        // En la deriva la cara +1 es la derecha.
        public Vector3 Punto(float s, float xi, float cara, float lado = 1f)
        {
            float c = Cuerda(s), t = Espesor(s);
            float zl = (NacaCurvatura(xi, curvatura, posCurvatura) + cara * NacaEspesor(xi, t)) * c;
            return Seccion(s, xi, zl, lado);
        }

        // Punto de la línea de curvatura desplazado 'zl' (m) en el sentido del espesor.
        public Vector3 Seccion(float s, float xi, float zl, float lado = 1f)
        {
            float c = Cuerda(s), xr = XRef(s);
            float dx = BordeAtaque(s) + xi * c - xr;
            float ang = Incidencia(s) * Mathf.Deg2Rad;
            float x = xr + dx * Mathf.Cos(ang) + zl * Mathf.Sin(ang);
            float h = zl * Mathf.Cos(ang) - dx * Mathf.Sin(ang);
            if (vertical) return P(x, h, zRef + s);
            return P(x, lado * s, zRef + s * Mathf.Tan(diedro * Mathf.Deg2Rad) + h);
        }

        public Vector3 Camber(float s, float xi, float lado = 1f) =>
            Seccion(s, xi, NacaCurvatura(xi, curvatura, posCurvatura) * Cuerda(s), lado);

        public float MedioEspesor(float s, float xi) => NacaEspesor(xi, Espesor(s)) * Cuerda(s);
    }

    // Estaciones repartidas entre a y b con paso máximo 'paso', incluyendo siempre los valores 'fijos'.
    public static List<float> Estaciones(float a, float b, float paso, params float[] fijos)
    {
        var l = new List<float> { a, b };
        foreach (var f in fijos) if (f > a && f < b) l.Add(f);
        l.Sort();
        var r = new List<float>();
        for (int k = 0; k < l.Count - 1; k++)
        {
            float x0 = l[k], x1 = l[k + 1];
            if (x1 - x0 < 1e-4f) continue;
            int n = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / paso));
            for (int q = 0; q < n; q++) r.Add(x0 + (x1 - x0) * q / n);
        }
        r.Add(b);
        return r;
    }

    // Fracciones de cuerda con más densidad cerca del borde de ataque, partidas en el corte 'xiCorte'.
    public static float[] FraccionesCuerda(int nAntes, int nDespues, float xiCorte)
    {
        var f = new float[nAntes + nDespues + 1];
        for (int k = 0; k <= nAntes; k++)
        {
            float u = (float)k / nAntes;
            f[k] = xiCorte * (1f - Mathf.Cos(u * Mathf.PI * 0.5f));   // denso en el borde de ataque
        }
        for (int k = 1; k <= nDespues; k++) f[nAntes + k] = Mathf.Lerp(xiCorte, 1f, (float)k / nDespues);
        return f;
    }

    // ---------- Fuselaje ----------
    // Secciones superelípticas: semiancho, techo, piso, altura del ancho máximo y exponentes arriba/abajo.
    public static readonly Curva FusAncho = new Curva(0.62f, 0.655f, 0.70f, 0.672f, 1.0f, 0.705f, 1.5f, 0.725f, 2.0f, 0.72f,
        2.42f, 0.70f, 2.9f, 0.665f, 3.6f, 0.645f, 4.6f, 0.625f, 5.6f, 0.59f, 6.5f, 0.535f, 7.5f, 0.455f, 8.5f, 0.36f,
        9.3f, 0.275f, 10.0f, 0.17f, 10.4f, 0.085f);
    public static readonly Curva FusSup = new Curva(0.62f, 0.655f, 0.70f, 0.672f, 1.0f, 0.71f, 1.5f, 0.735f, 2.0f, 0.73f,
        2.42f, 0.712f, 2.9f, 0.69f, 3.6f, 0.70f, 4.6f, 0.70f, 5.6f, 0.70f, 6.5f, 0.665f, 7.5f, 0.605f, 8.5f, 0.535f,
        9.3f, 0.47f, 10.0f, 0.38f, 10.4f, 0.25f);
    public static readonly Curva FusInf = new Curva(0.62f, -0.80f, 0.70f, -0.84f, 1.0f, -0.92f, 1.5f, -0.955f, 2.0f, -0.96f,
        2.42f, -0.96f, 3.0f, -0.955f, 3.6f, -0.95f, 4.6f, -0.935f, 5.4f, -0.90f, 6.3f, -0.80f, 7.5f, -0.62f, 8.5f, -0.45f,
        9.3f, -0.30f, 10.0f, -0.10f, 10.4f, 0.03f);
    public static readonly Curva FusMed = new Curva(0.62f, -0.06f, 2.42f, -0.05f, 3.6f, 0.0f, 5.6f, 0.05f, 7.5f, 0.02f,
        9.3f, 0.06f, 10.4f, 0.14f);
    public static readonly Curva FusNSup = new Curva(0.62f, 2.0f, 2.42f, 2.05f, 3.0f, 2.3f, 5.6f, 2.5f, 8.0f, 2.3f, 10.4f, 2.1f);
    public static readonly Curva FusNInf = new Curva(0.62f, 2.1f, 1.5f, 2.3f, 3.0f, 2.7f, 5.4f, 2.8f, 7.5f, 2.4f, 10.4f, 2.1f);
    public const float FusX0 = 0.62f, FusX1 = 10.4f;

    // (y, z) del contorno del fuselaje en la estación x para el ángulo t (rad): 0 = derecha, π/2 = arriba.
    public static Vector2 SeccionFus(float x, float t, float escala = 1f)
    {
        float a = FusAncho[x], zs = FusSup[x], zi = FusInf[x], zm = FusMed[x];
        float c = Mathf.Cos(t), s = Mathf.Sin(t);
        float e = 2f / (s >= 0f ? FusNSup[x] : FusNInf[x]);
        float y = a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
        float z = s >= 0f ? zm + (zs - zm) * Mathf.Pow(s, e) : zm - (zm - zi) * Mathf.Pow(-s, e);
        return new Vector2(y * escala, zm + (z - zm) * escala);
    }

    public static Vector3 PuntoFus(float x, float t, float escala = 1f)
    {
        var s = SeccionFus(x, t, escala);
        return P(x, s.x, s.y);
    }

    // Semiancho del fuselaje a la altura z (para saber dónde lo corta el ala, etc.).
    public static float AnchoFusEnZ(float x, float z)
    {
        float mejor = 0f;
        for (int k = 0; k <= 90; k++)
        {
            float t = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, k / 90f);
            var s = SeccionFus(x, t);
            if (Mathf.Abs(s.y - z) < 0.02f) mejor = Mathf.Max(mejor, s.x);
        }
        return mejor;
    }

    static bool EnRangoAngulo(float tGrados, float a, float b)
    {
        float t = Mathf.Repeat(tGrados - a, 360f);
        return t < Mathf.Repeat(b - a, 360f);
    }
}
#endif
