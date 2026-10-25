using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

/// <summary>
/// Acomoda las UVs de los rodillos del contador de munición (prismas de 10 caras hechos con ProBuilder):
/// cada cara del costado recibe 1/10 de la tira de dígitos (0 arriba, 9 abajo), en orden alrededor del eje
/// y en una columna continua. Las tapas no se tocan. Las UVs quedan como "manuales" en el ProBuilderMesh.
/// Acepta caras partidas en triángulos (por ejemplo, mallas importadas y convertidas con ProBuilderize).
/// </summary>
public class MapearUVsRodillo : EditorWindow {
    private const int DIGITS = 10;
    private const float DEGREES_PER_FACE = 360f / DIGITS;

    private bool m_mirror;
    private bool m_reverse;

    [MenuItem("Tools/Rodillos/Mapear UVs del contador")]
    static void Open() {
        GetWindow<MapearUVsRodillo>("UVs de rodillos").minSize = new Vector2(320, 170);
    }

    void OnGUI() {
        EditorGUILayout.HelpBox("Selecciona los rodillos (ProBuilder, 10 caras) y aplica. Si los números salen " +
            "en espejo activa 'Espejo'; si salen de cabeza activa los dos.", MessageType.Info);

        m_mirror = EditorGUILayout.Toggle(new GUIContent("Espejo", "Voltea los números de izquierda a derecha"), m_mirror);
        m_reverse = EditorGUILayout.Toggle(new GUIContent("Invertir sentido", "Voltea los números de arriba a abajo e invierte el orden alrededor del rodillo"), m_reverse);

        int count = SelectedMeshes().Count;
        using (new EditorGUI.DisabledScope(count == 0)) {
            if (GUILayout.Button(count == 0 ? "Selecciona rodillos de ProBuilder" : $"Aplicar a {count} rodillo(s)", GUILayout.Height(28)))
                foreach (var mesh in SelectedMeshes()) Apply(mesh, m_mirror, m_reverse);
        }
    }

    void OnSelectionChange() => Repaint();

    static List<ProBuilderMesh> SelectedMeshes() {
        var list = new List<ProBuilderMesh>();
        foreach (var go in Selection.gameObjects) {
            var mesh = go.GetComponent<ProBuilderMesh>();
            if (mesh != null) list.Add(mesh);
        }
        return list;
    }

    static void Apply(ProBuilderMesh mesh, bool mirror, bool reverse) {
        Undo.RecordObject(mesh, "Mapear UVs del rodillo");

        // Asegura que el arreglo de UVs exista y esté al día antes de editarlo
        mesh.ToMesh();
        mesh.Refresh();

        IList<Vector3> positions = mesh.positions;

        if (!TryFindAxis(mesh, out Vector3 axis, out List<Face> sides)) {
            Debug.LogWarning($"{mesh.name}: no se encontraron 10 lados alrededor de un eje (X, Y o Z local). ¿Es un prisma de 10 caras?", mesh);
            return;
        }

        Basis(axis, out Vector3 refA, out Vector3 refB);
        float AngleOf(Vector3 v) => Mathf.Atan2(Vector3.Dot(v, refB), Vector3.Dot(v, refA)) * Mathf.Rad2Deg;

        // Centro del rodillo y rango a lo largo del eje (para U)
        var sideIndexes = new HashSet<int>();
        foreach (var face in sides) foreach (int i in face.distinctIndexes) sideIndexes.Add(i);

        Vector3 center = Vector3.zero;
        float minAxial = float.MaxValue, maxAxial = float.MinValue;
        foreach (int i in sideIndexes) {
            center += positions[i];
            float a = Vector3.Dot(positions[i], axis);
            minAxial = Mathf.Min(minAxial, a);
            maxAxial = Mathf.Max(maxAxial, a);
        }
        center /= sideIndexes.Count;
        float axialRange = Mathf.Max(0.0001f, maxAxial - minAxial);

        // Cada lado se identifica por la dirección de su normal (los dos triángulos de un lado comparten normal).
        // El lado de menor ángulo es el 0 y los demás siguen en orden alrededor del eje.
        var faceAngles = new Dictionary<Face, float>();
        float startAngle = float.MaxValue;
        foreach (var face in sides) {
            float angle = AngleOf(UnityEngine.ProBuilder.Math.Normal(mesh, face));
            faceAngles[face] = angle;
            startAngle = Mathf.Min(startAngle, angle);
        }

        var uvs = mesh.textures != null ? new List<Vector2>(mesh.textures) : new List<Vector2>(new Vector2[mesh.vertexCount]);
        var vertexCell = new Dictionary<int, int>();
        var usedCells = new HashSet<int>();

        foreach (var face in sides) {
            int cell = CellOf(faceAngles[face], startAngle);
            float cellAngle = startAngle + cell * DEGREES_PER_FACE;
            if (reverse) cell = (DIGITS - cell) % DIGITS;
            usedCells.Add(cell);

            foreach (int i in face.distinctIndexes) {
                // Un vértice compartido por dos lados distintos no puede tener las UVs de ambos
                if (vertexCell.TryGetValue(i, out int other) && other != cell) {
                    Debug.LogWarning($"{mesh.name}: dos lados comparten vértices; no se pueden separar sus UVs.", mesh);
                    return;
                }
                vertexCell[i] = cell;

                Vector3 p = positions[i];
                float u = (Vector3.Dot(p, axis) - minAxial) / axialRange;
                // Posición dentro del lado a lo largo de la vuelta: 0 en una arista, 1 en la otra
                float t = 0.5f + Mathf.DeltaAngle(cellAngle, AngleOf(p - center)) / DEGREES_PER_FACE;
                if (reverse) t = 1f - t;

                if (mirror) u = 1f - u;
                // 0 arriba en la textura: la celda k ocupa V entre 1-(k+1)/10 y 1-k/10
                uvs[i] = new Vector2(u, 1f - (cell + t) / DIGITS);
            }
        }

        if (usedCells.Count != DIGITS) {
            Debug.LogWarning($"{mesh.name}: los lados no se repartieron en las {DIGITS} celdas ({usedCells.Count}). Revisa que el prisma sea regular.", mesh);
            return;
        }

        foreach (var face in sides) face.manualUV = true;
        mesh.textures = uvs;
        mesh.ToMesh();
        mesh.Refresh();
        UnityEditor.EditorUtility.SetDirty(mesh);
        ProBuilderEditor.Refresh();

        Debug.Log($"{mesh.name}: UVs del rodillo acomodadas (eje local {AxisName(axis)}, {sides.Count} caras).", mesh);
    }

    /// <summary>
    /// Busca el eje local (X, Y o Z) que tenga 10 lados a su alrededor: caras con la normal perpendicular a él,
    /// agrupadas por dirección (un lado puede venir partido en varios triángulos).
    /// </summary>
    static bool TryFindAxis(ProBuilderMesh mesh, out Vector3 axis, out List<Face> sides) {
        foreach (Vector3 candidate in new[] { Vector3.up, Vector3.right, Vector3.forward }) {
            Basis(candidate, out Vector3 refA, out Vector3 refB);

            var found = new List<Face>();
            var angles = new List<float>();
            foreach (var face in mesh.faces) {
                Vector3 normal = UnityEngine.ProBuilder.Math.Normal(mesh, face);
                if (Mathf.Abs(Vector3.Dot(normal, candidate)) < 0.1f) {
                    found.Add(face);
                    angles.Add(Mathf.Atan2(Vector3.Dot(normal, refB), Vector3.Dot(normal, refA)) * Mathf.Rad2Deg);
                }
            }
            if (found.Count < DIGITS) continue;

            float start = Mathf.Min(angles.ToArray());
            var cells = new HashSet<int>();
            foreach (float angle in angles) cells.Add(CellOf(angle, start));

            if (cells.Count == DIGITS) {
                axis = candidate;
                sides = found;
                return true;
            }
        }
        axis = Vector3.up;
        sides = null;
        return false;
    }

    static int CellOf(float angle, float startAngle) {
        return Mathf.RoundToInt((angle - startAngle) / DEGREES_PER_FACE) % DIGITS;
    }

    /// <summary>Dos ejes perpendiculares al eje del rodillo, para medir ángulos alrededor de él.</summary>
    static void Basis(Vector3 axis, out Vector3 refA, out Vector3 refB) {
        refA = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        refB = Vector3.Cross(axis, refA);
    }

    static string AxisName(Vector3 axis) => axis == Vector3.right ? "X" : axis == Vector3.up ? "Y" : "Z";
}
