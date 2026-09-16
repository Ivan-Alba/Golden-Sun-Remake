using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller responsible for 3D battle camera presentations, UI transition snapshot fades,
/// secondary turn UI camera attachment, and broadcasting active primary rendering camera events.
/// </summary>
public class BattleCameraController : MonoBehaviour
{
    [Header("UI Render Transition")]
    [SerializeField] private GameObject cameraRenderImage;

    [Header("Camera Prefabs")]
    [SerializeField] private GameObject turnCameraPrefab;
    [SerializeField] private GameObject victoryCameraPrefab;

    private GameObject victoryCamera;
    private GameObject lastHitHero;

    // Persistent Turn Camera Instance (RenderTexture / HUD Sub-camera)
    private GameObject turnCameraInstance;
    private Camera turnCameraComp;

    // Internal Main Battle Camera Reference
    private Camera battleCameraComp;

    // Cached Previous Camera (Overworld / Exploration Camera)
    private Camera _cachedPreviousCamera;

    // Lifecycle Flag to distinguish initial entry transition from internal repositioning transition
    private bool _isBattleCameraActive = false;

    private bool transitionDone;
    public bool TransitionDone { get => transitionDone; set => transitionDone = value; }

    private void Awake()
    {
        battleCameraComp = GetComponent<Camera>();

        // Load prefabs if not assigned in Inspector
        if (turnCameraPrefab == null)
        {
            turnCameraPrefab = Resources.Load<GameObject>("Prefabs/TurnCamera");
        }

        if (victoryCameraPrefab == null)
        {
            victoryCameraPrefab = Resources.Load<GameObject>("Prefabs/VictoryPoseCamera");
        }

        // Lazy search for RenderImageCanvas child if not assigned
        if (cameraRenderImage == null)
        {
            GameObject canvasObj = GameObject.Find("RenderImageCanvas");
            if (canvasObj != null && canvasObj.transform.childCount > 0)
            {
                cameraRenderImage = canvasObj.transform.GetChild(0).gameObject;
            }
        }
    }

    private void OnEnable()
    {
        GameEvents.Camera.OnActiveCameraChanged += HandleActiveCameraChanged;
    }

    private void OnDisable()
    {
        GameEvents.Camera.OnActiveCameraChanged -= HandleActiveCameraChanged;
    }

    private void HandleActiveCameraChanged(Camera newCamera)
    {
        if (newCamera == null) return;

        // Ignore internal battle cameras to prevent overwriting exploration camera reference
        if (newCamera == battleCameraComp) return;
        if (turnCameraComp != null && newCamera == turnCameraComp) return;
        if (victoryCamera != null && victoryCamera.TryGetComponent<Camera>(out var vicCam) && newCamera == vicCam) return;

        // Strictly cache Overworld/Exploration active camera
        _cachedPreviousCamera = newCamera;
    }

    #region Battle Transition Pipeline

    /// <summary>
    /// Executes the snapshot transition fade sequence.
    /// Handles both entry transition (Overworld -> Battle) and camera repositioning (CloseUp -> Idle).
    /// </summary>
    public IEnumerator StartTransition()
    {
        transitionDone = false;

        Camera camToFreeze = null;

        if (!_isBattleCameraActive)
        {
            // 1ST PASS: Transitioning from Overworld to Battle Arena
            if (_cachedPreviousCamera == null)
            {
                // Fallback to MainCamera tag or Camera.main if event didn't trigger prior
                if (Camera.main != null && Camera.main != battleCameraComp)
                {
                    _cachedPreviousCamera = Camera.main;
                }
                else
                {
                    GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera");
                    if (mainCamObj != null) _cachedPreviousCamera = mainCamObj.GetComponent<Camera>();
                }
            }

            camToFreeze = _cachedPreviousCamera;
        }
        else
        {
            // 2ND PASS: Repositioning Battle Camera from Enemy CloseUp to Idle state
            camToFreeze = battleCameraComp;
        }

        RenderTexture renderTexture = Resources.Load<RenderTexture>("RenderTexture/CameraRender");

        // 1. Take INSTANT snapshot of the selected camera
        if (camToFreeze != null && renderTexture != null)
        {
            camToFreeze.targetTexture = renderTexture;
            camToFreeze.Render(); // Forces single frame render into texture
            camToFreeze.targetTexture = null; // IMMEDIATELY release so it outputs to display again
        }

        // 2. Perform camera activation logic during 1st transition pass
        if (!_isBattleCameraActive)
        {
            if (_cachedPreviousCamera != null)
            {
                _cachedPreviousCamera.enabled = false;
            }

            if (battleCameraComp != null)
            {
                battleCameraComp.enabled = true;
                battleCameraComp.targetTexture = null;
                GameEvents.Camera.TriggerActiveCameraChanged(battleCameraComp);
            }

            _isBattleCameraActive = true;
        }

        // 3. Show UI image containing frozen snapshot and play wipe/fade animation
        if (cameraRenderImage != null)
        {
            cameraRenderImage.SetActive(true);

            Animation anim = cameraRenderImage.GetComponent<Animation>();
            if (anim != null)
            {
                anim.Rewind();
                anim.Play();
            }
        }

        // Small delay to ensure snapshot is displayed on UI before notifying transition end
        yield return new WaitForSeconds(0.1f);

        // BattleCamera can now reposition itself behind the UI snapshot
        transitionDone = true;

        yield return new WaitForSeconds(1.5f);

        // Hide UI overlay to reveal newly positioned BattleCamera
        if (cameraRenderImage != null)
        {
            cameraRenderImage.SetActive(false);

            RawImage rawImg = cameraRenderImage.GetComponent<RawImage>();
            if (rawImg != null)
            {
                rawImg.color = Color.white;
            }
        }
    }

    #endregion

    #region Animator State Triggers

    public void IntroBattle()
    {
        GetComponent<Animator>()?.SetTrigger("introBattle");
    }

    public void SetInitialIdle()
    {
        GetComponent<Animator>()?.SetTrigger("initialIdle");
    }

    public void Idle()
    {
        GetComponent<Animator>()?.SetTrigger("cameraIdle");
    }

    public void Victory(GameObject lastHitHero)
    {
        this.lastHitHero = lastHitHero;
        GetComponent<Animator>()?.SetTrigger("victory");
    }

    public void LastHitHeroVictory()
    {
        if (lastHitHero != null && victoryCameraPrefab != null)
        {
            victoryCamera = Instantiate(victoryCameraPrefab, lastHitHero.transform);

            if (victoryCamera.TryGetComponent<Camera>(out var vicCam))
            {
                GameEvents.Camera.TriggerActiveCameraChanged(vicCam);
            }
        }
    }

    #endregion

    #region Turn Camera Management (Sub-Camera HUD Output)

    /// <summary>
    /// Repositions and displays the turn camera container for the active hero.
    /// Pure sub-camera output rendering into UI RenderTexture.
    /// </summary>
    public void SetTurnCameraOnHero(GameObject hero)
    {
        if (hero == null) return;

        Transform container = hero.transform.Find("ContainerTurnCamera");
        if (container == null) return;

        // Instantiate single instance if not present
        if (turnCameraInstance == null && turnCameraPrefab != null)
        {
            turnCameraInstance = Instantiate(turnCameraPrefab);
        }

        if (turnCameraInstance != null)
        {
            // Attach to current hero's camera container
            turnCameraInstance.transform.SetParent(container, false);
            turnCameraInstance.transform.localPosition = Vector3.zero;
            turnCameraInstance.transform.localRotation = Quaternion.identity;

            turnCameraComp = turnCameraInstance.GetComponent<Camera>();
            if (turnCameraComp != null)
            {
                turnCameraComp.enabled = true;
            }

            turnCameraInstance.SetActive(true);
        }
    }

    /// <summary>
    /// Disables turn HUD camera presentation without altering the primary active camera state.
    /// </summary>
    public void HideTurnCamera()
    {
        if (turnCameraInstance == null) return;

        if (turnCameraComp != null)
        {
            turnCameraComp.enabled = false;
        }

        turnCameraInstance.SetActive(false);
    }

    #endregion

    #region Teardown & Reset

    public void SetOffBattleCamera()
    {
        GetComponent<Animator>()?.SetTrigger("initialPos");

        if (victoryCamera != null)
        {
            Destroy(victoryCamera);
            victoryCamera = null;
        }

        if (turnCameraInstance != null)
        {
            Destroy(turnCameraInstance);
            turnCameraInstance = null;
            turnCameraComp = null;
        }

        if (battleCameraComp != null)
        {
            battleCameraComp.enabled = false;
        }

        _isBattleCameraActive = false;

        // Restore and broadcast previous primary active camera (Overworld / Exploration Camera)
        if (_cachedPreviousCamera != null)
        {
            _cachedPreviousCamera.enabled = true;
            GameEvents.Camera.TriggerActiveCameraChanged(_cachedPreviousCamera);
        }
    }

    #endregion
}