using UnityEngine;
using UnityEditor;

public class EscalarObjeto : EditorWindow {
    float factor = 0.8f;

    [MenuItem("Tools/Escalar seleccionado")]
    static void Abrir() => GetWindow<EscalarObjeto>("Escalar");

    void OnGUI() {
        factor = EditorGUILayout.FloatField("Factor", factor);
        EditorGUILayout.HelpBox("Selecciona el hijo con la malla, NO la raíz con Rigidbody ni nada que contenga el XR Origin.", MessageType.Info);

        if (GUILayout.Button("Aplicar")) {
            foreach (var go in Selection.gameObjects) {
                if (Contiene(go, "XROrigin")) {
                    Debug.LogError($"{go.name}: contiene el XR Origin, no se escaló.");
                    continue;
                }
                if (go.GetComponent<Rigidbody>() != null)
                    Debug.LogWarning($"{go.name}: tiene Rigidbody; revisa colliders y física después.");

                Undo.RecordObject(go.transform, "Escalar objeto");
                go.transform.localScale *= factor;
                Debug.Log($"{go.name} → escala ×{factor}, nueva: {go.transform.localScale}");
            }
        }
    }

    static bool Contiene(GameObject go, string tipo) {
        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c != null && c.GetType().Name == tipo) return true;
        return false;
    }
}