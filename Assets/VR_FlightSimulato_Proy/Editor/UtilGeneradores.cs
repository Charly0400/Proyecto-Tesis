// Utilidades compartidas por los generadores del editor.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Arma una malla con piezas simples (caras, cajas, vigas, prismas) en un mismo sistema de coordenadas.
public class ConstructorMalla
{
    readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
    readonly List<Vector2> uv = new List<Vector2>();
    readonly List<int> t = new List<int>();

    public bool Vacio => v.Count == 0;

    // Cuadrilátero p0..p3 (en orden alrededor). Se voltea solo para que mire hacia 'normal'.
    // Con 'normales' (una por vértice) queda sombreado suave.
    public void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector3[] normales = null)
    {
        var q = new[] { p0, p1, p2, p3 };
        var nq = normales ?? new[] { normal, normal, normal, normal };
        var u = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        // Unity dibuja la cara del lado hacia donde apunta Cross(p1-p0, p2-p0).
        if (Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), normal) < 0f)
        { System.Array.Reverse(q); System.Array.Reverse(nq); System.Array.Reverse(u); }
        int i = v.Count;
        for (int k = 0; k < 4; k++) { v.Add(q[k]); n.Add(nq[k]); uv.Add(u[k]); }
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    public void Tri(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 normal)
    {
        if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) < 0f) (p1, p2) = (p2, p1);
        int i = v.Count;
        v.Add(p0); v.Add(p1); v.Add(p2);
        n.Add(normal); n.Add(normal); n.Add(normal);
        uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    // Cara rectangular centrada en 'c' con ejes 'a' y 'b'.
    public void Cara(Vector3 c, Vector3 a, Vector3 b, Vector3 normal, float largoA, float largoB)
    {
        var ea = a * (largoA * 0.5f); var eb = b * (largoB * 0.5f);
        Quad(c - ea - eb, c + ea - eb, c + ea + eb, c - ea + eb, normal);
    }

    public void Caja(Vector3 centro, Vector3 tam, Quaternion rot)
    {
        Vector3 x = rot * Vector3.right, y = rot * Vector3.up, z = rot * Vector3.forward;
        Cara(centro + x * tam.x * 0.5f, y, z, x, tam.y, tam.z);
        Cara(centro - x * tam.x * 0.5f, y, z, -x, tam.y, tam.z);
        Cara(centro + y * tam.y * 0.5f, x, z, y, tam.x, tam.z);
        Cara(centro - y * tam.y * 0.5f, x, z, -y, tam.x, tam.z);
        Cara(centro + z * tam.z * 0.5f, x, y, z, tam.x, tam.y);
        Cara(centro - z * tam.z * 0.5f, x, y, -z, tam.x, tam.y);
    }

    public void Caja(Vector3 centro, Vector3 tam) => Caja(centro, tam, Quaternion.identity);

    // Barra de sección cuadrada entre dos puntos (barandales, marcos, antenas).
    public void Viga(Vector3 p0, Vector3 p1, float grosor)
    {
        var d = p1 - p0;
        if (d.sqrMagnitude < 1e-6f) return;
        var arriba = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        Caja((p0 + p1) * 0.5f, new Vector3(grosor, grosor, d.magnitude), Quaternion.LookRotation(d, arriba));
    }

    // Prisma o tronco de 'lados' lados sobre el eje Y. 'giro' en radianes rota el polígono.
    public void Prisma(Vector3 baseCentro, float rAbajo, float rArriba, float alto, int lados, bool suave, bool tapas, float giro = 0f)
    {
        var abajo = new Vector3[lados]; var arribaP = new Vector3[lados]; var radial = new Vector3[lados];
        for (int i = 0; i < lados; i++)
        {
            float a = giro + Mathf.PI * 2f * i / lados;
            radial[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            abajo[i] = baseCentro + radial[i] * rAbajo;
            arribaP[i] = baseCentro + radial[i] * rArriba + Vector3.up * alto;
        }
        float pendiente = (rAbajo - rArriba) / Mathf.Max(alto, 1e-4f);
        for (int i = 0; i < lados; i++)
        {
            int j = (i + 1) % lados;
            var fuera = (radial[i] + radial[j]).normalized;
            var nf = (fuera + Vector3.up * pendiente).normalized;
            Vector3[] ns = null;
            if (suave)
            {
                var ni = (radial[i] + Vector3.up * pendiente).normalized;
                var nj = (radial[j] + Vector3.up * pendiente).normalized;
                ns = new[] { ni, nj, nj, ni };
            }
            Quad(abajo[i], abajo[j], arribaP[j], arribaP[i], nf, ns);
        }
        if (!tapas) return;
        var cArriba = baseCentro + Vector3.up * alto;
        for (int i = 0; i < lados; i++)
        {
            int j = (i + 1) % lados;
            Tri(cArriba, arribaP[i], arribaP[j], Vector3.up);
            Tri(baseCentro, abajo[i], abajo[j], Vector3.down);
        }
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
        return mesh;
    }
}

public static class UtilGeneradores
{
    const string CarpetaMallas = "Assets/VR_FlightSimulato_Proy/Generado/Mallas";

    // Guarda la malla como asset para que sobreviva al aplicarla a un prefab (una malla creada solo en
    // memoria se pierde y las instancias del prefab salen vacías). El nombre lleva un hash del contenido:
    // mallas iguales (p. ej. el mismo ventanal en los tres hangares) comparten un solo archivo.
    public static Mesh GuardarMalla(Mesh mesh)
    {
        var sb = new StringBuilder();
        foreach (var v in mesh.vertices)
            sb.Append(v.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(v.y.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(v.z.ToString("F3", CultureInfo.InvariantCulture)).Append(';');
        foreach (var t in mesh.triangles) sb.Append(t).Append(',');
        string hash = Hash128.Compute(sb.ToString()).ToString().Substring(0, 10);

        string ruta = $"{CarpetaMallas}/{mesh.name}_{hash}.asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente != null) return existente;

        GeneradorMarcasPista.CrearCarpeta(CarpetaMallas);
        AssetDatabase.CreateAsset(mesh, ruta);
        AssetDatabase.SaveAssets();
        return mesh;
    }
}
#endif
