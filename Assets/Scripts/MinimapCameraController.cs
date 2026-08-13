using UnityEngine;
using UnityEngine.Rendering;

public class MinimapCameraController : MonoBehaviour
{
    private GameObject player;
    private GameObject arrow;
    private Camera targetCamera;
    private bool previousFogState;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();

        GameObject minimapUI = GameObject.FindGameObjectWithTag("Minimap");
        if (minimapUI != null)
        {
            Transform arrowTransform = minimapUI.transform.Find("Arrow");
            if (arrowTransform != null)
            {
                arrow = arrowTransform.gameObject;
            }
        }

        player = GameObject.FindGameObjectWithTag("Player");
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == targetCamera)
        {
            previousFogState = RenderSettings.fog;
            RenderSettings.fog = false;
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == targetCamera)
        {
            RenderSettings.fog = previousFogState;
        }
    }

    private void LateUpdate()
    {
        if (player != null)
        {
            // Camera position tracking
            Vector3 newPosition = player.transform.position;
            newPosition.y = transform.position.y;
            transform.position = newPosition;

            // Arrow rotation tracking
            if (arrow != null)
            {
                arrow.transform.rotation = Quaternion.Euler(0, 0, -player.transform.localEulerAngles.y);
            }
        }
    }
}