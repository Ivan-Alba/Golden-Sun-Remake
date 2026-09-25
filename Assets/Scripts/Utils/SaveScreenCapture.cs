using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Utility helper for capturing clean, UI-free in-game screenshots 
/// and converting them to Base64 strings for save slot cards.
/// Supports temporary viewpoint overrides via Transform.
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
    /// Static entry point to capture an in-game thumbnail asynchronously without needing a MonoBehaviour reference.
    /// Finds or creates a temporary runner to execute the capture coroutine.
    /// </summary>
    /// <param name="onCaptured">Callback providing the resulting Base64 JPG string.</param>
    /// <param name="customViewpoint">Optional custom viewpoint Transform (e.g. from a SavePoint Cinemachine camera). Uses Camera.main default pose if null.</param>
    /// <param name="targetCamera">Optional specific camera reference. Defaults to Camera.main if null.</param>
    public static void CaptureScreenshotBase64(Action<string> onCaptured, Transform customViewpoint = null, Camera targetCamera = null)
    {
        // 1. Ensure we have an active runner MonoBehaviour to execute the coroutine
        CoroutineRunner.Run(Routine_CaptureSaveThumbnail(customViewpoint, onCaptured, targetCamera));
    }

    /// <summary>
    /// Captures a frame directly from the target main camera (ignoring UI canvases)
    /// and delivers a lightweight Base64-encoded JPG string via callback.
    /// </summary>

    /// <param name="customViewpoint">Optional custom position/rotation Transform override for the capture.</param>
    /// <param name="onCaptured">Callback providing the Base64 image string.</param>
    /// <param name="targetCamera">The main world camera to render from (e.g. Camera.main).</param>
    public static IEnumerator Routine_CaptureSaveThumbnail(Transform customViewpoint, Action<string> onCaptured, Camera targetCamera = null)
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

        // Cache original main camera pose
        Vector3 originalPosition = targetCamera.transform.position;
        Quaternion originalRotation = targetCamera.transform.rotation;

        try
        {
            // If a custom viewpoint Transform was provided, temporarily match its pose
            if (customViewpoint != null)
            {
                targetCamera.transform.position = customViewpoint.position;
                targetCamera.transform.rotation = customViewpoint.rotation;
            }

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
        finally
        {
            // Always restore main camera's original pose if it was temporarily shifted
            if (customViewpoint != null && targetCamera != null)
            {
                targetCamera.transform.position = originalPosition;
                targetCamera.transform.rotation = originalRotation;
            }
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

    #region Internal Coroutine Runner Helper

    /// <summary>
    /// Lightweight persistent helper MonoBehaviour used exclusively to host static coroutines.
    /// </summary>
    private class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner instance;

        public static void Run(IEnumerator coroutine)
        {
            if (instance == null)
            {
                GameObject runnerObj = new GameObject("[SaveScreenCapture_Runner]");
                instance = runnerObj.AddComponent<CoroutineRunner>();
                DontDestroyOnLoad(runnerObj);
            }

            instance.StartCoroutine(coroutine);
        }
    }

    #endregion
}