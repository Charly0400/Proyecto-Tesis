using UnityEngine;
using UnityEditor;

public static class MedirObjeto {
    [MenuItem("Tools/Medir seleccionado")]
    static void Medir() {
        foreach (var go in Selection.gameObjects) {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) {
                Debug.LogWarning($"{go.name}: no tiene Renderers");
                continue;
            }

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            Vector3 s = b.size;
            Debug.Log($"{go.name} → X: {s.x:F2} m | Y (alto): {s.y:F2} m | Z: {s.z:F2} m");
        }
    }
}