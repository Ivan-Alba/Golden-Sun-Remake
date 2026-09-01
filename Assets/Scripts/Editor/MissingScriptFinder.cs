using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class MissingScriptFinder : EditorWindow
{
    [MenuItem("Tools/Find Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow<MissingScriptFinder>("Missing Scripts Finder");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Find in Loaded Scenes", GUILayout.Height(30)))
        {
            FindInActiveScenes();
        }
    }

    private static void FindInActiveScenes()
    {
        int missingCount = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            GameObject[] rootObjects = scene.GetRootGameObjects();

            foreach (GameObject rootObj in rootObjects)
            {
                Transform[] allTransforms = rootObj.GetComponentsInChildren<Transform>(true);

                foreach (Transform t in allTransforms)
                {
                    Component[] components = t.GetComponents<Component>();

                    for (int c = 0; c < components.Length; c++)
                    {
                        if (components[c] == null)
                        {
                            missingCount++;
                            Debug.LogError($"[Missing Script] Found on GameObject: '{t.name}' (Path: {GetGameObjectPath(t)}) in Scene: '{scene.name}'", t.gameObject);

                            // Si el objeto se llama StaticLightingSky o es un residuo de HDRP, lo eliminamos por completo:
                            if (t.name == "StaticLightingSky")
                            {
                                Undo.DestroyObjectImmediate(t.gameObject);
                                break; // Rompemos el bucle de componentes porque el objeto ya no existe
                            }
                        }
                    }
                }
            }
        }

        Debug.Log($"Search complete. Found {missingCount} missing script(s).");
    }

    private static string GetGameObjectPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}