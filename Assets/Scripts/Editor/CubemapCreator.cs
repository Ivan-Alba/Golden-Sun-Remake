#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class CubemapCreator : EditorWindow
{
    private Texture2D _right;  // +X
    private Texture2D _left;   // -X
    private Texture2D _up;     // +Y
    private Texture2D _down;   // -Y
    private Texture2D _front;  // +Z
    private Texture2D _back;   // -Z

    [MenuItem("Tools/Environment/Create Modern Cubemap from 6 Textures")]
    public static void ShowWindow()
    {
        GetWindow<CubemapCreator>("Modern Cubemap Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Assign 6 Skybox Faces", EditorStyles.boldLabel);

        _right = (Texture2D)EditorGUILayout.ObjectField("Right (+X)", _right, typeof(Texture2D), false);
        _left = (Texture2D)EditorGUILayout.ObjectField("Left (-X)", _left, typeof(Texture2D), false);
        _up = (Texture2D)EditorGUILayout.ObjectField("Up (+Y)", _up, typeof(Texture2D), false);
        _down = (Texture2D)EditorGUILayout.ObjectField("Down (-Y)", _down, typeof(Texture2D), false);
        _front = (Texture2D)EditorGUILayout.ObjectField("Front (+Z)", _front, typeof(Texture2D), false);
        _back = (Texture2D)EditorGUILayout.ObjectField("Back (-Z)", _back, typeof(Texture2D), false);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Build Modern Cubemap Asset"))
        {
            BuildModernCubemap();
        }
    }

    private void BuildModernCubemap()
    {
        if (_right == null || _left == null || _up == null || _down == null || _front == null || _back == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign all 6 face textures.", "OK");
            return;
        }

        string savePath = EditorUtility.SaveFilePanelInProject("Save Cubemap PNG", "Skybox_Cubemap", "png", "Select where to save the Cubemap asset");
        if (string.IsNullOrEmpty(savePath)) return;

        // 1. Ensure textures are temporarily readable
        Texture2D[] faces = new Texture2D[] { _right, _left, _up, _down, _front, _back };
        for (int i = 0; i < faces.Length; i++)
        {
            string assetPath = AssetDatabase.GetAssetPath(faces[i]);
            TextureImporter faceImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (faceImporter != null && !faceImporter.isReadable)
            {
                faceImporter.isReadable = true;
                faceImporter.SaveAndReimport();
            }
        }

        int faceSize = _right.width;

        // 2. Create a combined horizontal layout texture (6 faces side by side: +X, -X, +Y, -Y, +Z, -Z)
        Texture2D atlas = new Texture2D(faceSize * 6, faceSize, TextureFormat.RGBA32, false);

        for (int i = 0; i < 6; i++)
        {
            Color[] pixels = faces[i].GetPixels();
            atlas.SetPixels(i * faceSize, 0, faceSize, faceSize, pixels);
        }

        atlas.Apply();

        // 3. Save PNG file to project
        byte[] bytes = atlas.EncodeToPNG();
        File.WriteAllBytes(savePath, bytes);
        DestroyImmediate(atlas);

        AssetDatabase.Refresh();

        // 4. Configure Unity 6 TextureImporter using textureShape and generateCubemap properly
        TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        EditorUtility.DisplayDialog("Success", $"Modern Cubemap imported successfully at:\n{savePath}", "OK");
    }
}
#endif