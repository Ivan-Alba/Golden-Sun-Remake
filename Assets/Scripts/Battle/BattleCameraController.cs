using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleCameraController : MonoBehaviour
{

    [Header("UI Render Transition")]
    [SerializeField] private GameObject cameraRenderImage;

    [Header("Camera Prefabs")]
    [SerializeField] private GameObject turnCameraPrefab;
    [SerializeField] private GameObject victoryCameraPrefab;

    GameObject victoryCamera;
    GameObject lastHitHero;

    bool transitionDone;
    public bool TransitionDone { get => transitionDone; set => transitionDone = value; }

    void Awake()
    {
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

public IEnumerator StartTransition(GameObject cameraToFade = null)
{
    transitionDone = false;

    // Determine which camera to freeze (MainCamera or BattleCamera)
    GameObject targetObj = (cameraToFade != null) ? cameraToFade : gameObject;
    Camera camToFreeze = targetObj.GetComponent<Camera>();

    RenderTexture renderTexture = Resources.Load<RenderTexture>("RenderTexture/CameraRender");

    // 1. Take INSTANT snapshot of the selected camera
    if (camToFreeze != null && renderTexture != null)
    {
        camToFreeze.targetTexture = renderTexture;
        camToFreeze.Render(); // Forces single frame render into texture
        camToFreeze.targetTexture = null; // IMMEDIATELY release so it outputs to display again
    }

    // 2. Handle camera toggles if transitioning from World MainCamera
    if (cameraToFade != null && cameraToFade != gameObject)
    {
        Camera worldCam = cameraToFade.GetComponent<Camera>();
        if (worldCam != null) worldCam.enabled = false;

        Camera battleCam = GetComponent<Camera>();
        if (battleCam != null)
        {
            battleCam.enabled = true;
            battleCam.targetTexture = null;
        }
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

    public void IntroBattle()
    {
        gameObject.GetComponent<Animator>().SetTrigger("introBattle");

    }

    public void SetInitialIdle()
    {
        gameObject.GetComponent<Animator>().SetTrigger("initialIdle");
    }
    public void Idle()
    {
        gameObject.GetComponent<Animator>().SetTrigger("cameraIdle");
    }
    public void Victory(GameObject lastHitHero)
    {
        this.lastHitHero = lastHitHero;
        gameObject.GetComponent<Animator>().SetTrigger("victory");
    }

    public void LastHitHeroVictory()
    {
        victoryCamera = Instantiate(victoryCameraPrefab, lastHitHero.transform);
    }

    public void SetTurnCameraOnHero(GameObject hero)
    {
        Instantiate(turnCameraPrefab, hero.transform.Find("ContainerTurnCamera"));
    }

    public void DestoyTurnCamera(GameObject hero)
    {
        Destroy(hero.transform.Find("TurnCamera)"));
    }

    public void SetOffBattleCamera()
    {
        gameObject.GetComponent<Animator>().SetTrigger("initialPos");
        Destroy(victoryCamera);
    }
}
