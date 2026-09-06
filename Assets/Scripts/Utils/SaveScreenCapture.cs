using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Utility helper for capturing clean, UI-free in-game screenshots 
/// and converting them to Base64 strings for save slot cards.
/// </summary>
public static class SaveScreenCapture
{
    #region Configuration Constants

    private const int THUMBNAIL_WIDTH = 320;
    private const int THUMBNAIL_HEIGHT = 180;
    private const int JPG_QUALITY = 75;

    #endregion

    #region Public API

    /// <summary>
    /// Captures a frame directly from the target main camera (ignoring UI canvases)
    /// and delivers a lightweight Base64-encoded JPG string via callback.
    /// </summary>
    /// <param name="targetCamera">The main world camera to render from (e.g. Camera.main).</param>
    /// <param name="onCaptured">Callback providing the Base64 image string.</param>
    public static IEnumerator Routine_CaptureSaveThumbnail(Camera targetCamera, Action<string> onCaptured)
    {
        // Wait until the end of the current frame so physics and positioning are completely settled
        yield return new WaitForEndOfFrame();

        string base64Image = string.Empty;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            Debug.LogWarning("[SaveScreenCapture] No active camera available to render save thumbnail.");
            onCaptured?.Invoke(string.Empty);
            yield break;
        }

        try
        {
            // 1. Create temporary RenderTexture at thumbnail target resolution
            RenderTexture renderTexture = RenderTexture.GetTemporary(THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT, 24);
            RenderTexture previousTarget = targetCamera.targetTexture;

            // 2. Render target camera directly into the RenderTexture
            targetCamera.targetTexture = renderTexture;
            targetCamera.Render();

            // 3. Read pixels from active RenderTexture
            RenderTexture.active = renderTexture;
            Texture2D thumbnailTexture = new Texture2D(THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT, TextureFormat.RGB24, false);
            thumbnailTexture.ReadPixels(new Rect(0, 0, THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT), 0, 0);
            thumbnailTexture.Apply();

            // 4. Restore camera state and release temporary render texture
            targetCamera.targetTexture = previousTarget;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);

            // 5. Encode texture bytes to lightweight JPG Base64 string
            byte[] bytes = thumbnailTexture.EncodeToJPG(JPG_QUALITY);
            base64Image = Convert.ToBase64String(bytes);

            // Clean up CPU texture memory
            UnityEngine.Object.Destroy(thumbnailTexture);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveScreenCapture] Failed to capture save thumbnail: {ex.Message}");
        }

        onCaptured?.Invoke(base64Image);
    }

    /// <summary>
    /// Helper method for UI components to decode a Base64 string back into a Sprite.
    /// </summary>
    public static Sprite Base64ToSprite(string base64String)
    {
        if (string.IsNullOrEmpty(base64String)) return null;

        try
        {
            byte[] bytes = Convert.FromBase64String(base64String);
            Texture2D texture = new Texture2D(2, 2);

            if (texture.LoadImage(bytes))
            {
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveScreenCapture] Failed to convert Base64 string to Sprite: {ex.Message}");
        }

        return null;
    }

    #endregion
}