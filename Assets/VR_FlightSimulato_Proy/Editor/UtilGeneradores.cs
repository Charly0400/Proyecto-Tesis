// Utilidades compartidas por los generadores del editor.
#if UNITY_EDITOR
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

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
