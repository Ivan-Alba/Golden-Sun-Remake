using UnityEngine;
using UnityEditor;
using System.IO;

public class MaskMapGenerator : EditorWindow
{
    private Texture2D metallicTex;
    private Texture2D aoTex;
    private Texture2D heightTex;
    private Texture2D smoothnessTex;

    [MenuItem("Tools/Mask Map Generator")]
    public static void ShowWindow()
    {
        GetWindow<MaskMapGenerator>("Mask Map Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Combine Textures into URP Mask Map", EditorStyles.boldLabel);

        metallicTex = (Texture2D)EditorGUILayout.ObjectField("Red Channel (Metallic)", metallicTex, typeof(Texture2D), false);
        aoTex = (Texture2D)EditorGUILayout.ObjectField("Green Channel (AO)", aoTex, typeof(Texture2D), false);
        heightTex = (Texture2D)EditorGUILayout.ObjectField("Blue Channel (Height)", heightTex, typeof(Texture2D), false);
        smoothnessTex = (Texture2D)EditorGUILayout.ObjectField("Alpha Channel (Smoothness)", smoothnessTex, typeof(Texture2D), false);

        if (GUILayout.Button("Generate Mask Map"))
        {
            GenerateMaskMap();
        }
    }

    private void GenerateMaskMap()
    {
        int width = GetWidth();
        int height = GetHeight();

        if (width == 0 || height == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please assign at least one texture.", "OK");
            return;
        }

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                float v = (float)y / height;

                float r = metallicTex != null ? metallicTex.GetPixelBilinear(u, v).r : 0f;
                float g = aoTex != null ? aoTex.GetPixelBilinear(u, v).r : 1f;
                float b = heightTex != null ? heightTex.GetPixelBilinear(u, v).r : 0f;
                float a = smoothnessTex != null ? smoothnessTex.GetPixelBilinear(u, v).r : 0f;

                result.SetPixel(x, y, new Color(r, g, b, a));
            }
        }

        result.Apply();

        byte[] bytes = result.EncodeToPNG();
        string path = EditorUtility.SaveFilePanelInProject("Save Mask Map", "Terrain_MaskMap", "png", "Save your combined texture");

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "Mask Map generated successfully!", "OK");
        }
    }

    private int GetWidth()
    {
        if (metallicTex != null) return metallicTex.width;
        if (aoTex != null) return aoTex.width;
        if (heightTex != null) return heightTex.width;
        if (smoothnessTex != null) return smoothnessTex.width;
        return 0;
    }

    private int GetHeight()
    {
        if (metallicTex != null) return metallicTex.height;
        if (aoTex != null) return aoTex.height;
        if (heightTex != null) return heightTex.height;
        if (smoothnessTex != null) return smoothnessTex.height;
        return 0;
    }
}