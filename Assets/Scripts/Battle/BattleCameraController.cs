using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleCameraController : MonoBehaviour
{
    GameObject turnCameraPrefab;
    GameObject victoryCameraPrefab;

    bool transitionDone;
    GameObject cameraRenderImage;

    GameObject victoryCamera;
    GameObject lastHitHero;


    public bool TransitionDone { get => transitionDone; set => transitionDone = value; }

    // Start is called before the first frame update
    void Awake()
    {
        turnCameraPrefab = Resources.Load("Prefabs/TurnCamera") as GameObject;
        victoryCameraPrefab = Resources.Load("Prefabs/VictoryPoseCamera") as GameObject;
        cameraRenderImage = GameObject.Find("RenderImageCanvas").transform.GetChild(0).gameObject;
    }

    public IEnumerator StartTransition(GameObject cameraToFade = null)
    {
        if (cameraToFade == null)
        {
            cameraToFade = gameObject;
        } 

        transitionDone = false;

        //TRANSICIÓN
        RenderTexture renderTexture = (RenderTexture)Resources.Load("RenderTexture/CameraRender");
        cameraToFade.GetComponent<Camera>().targetTexture = renderTexture;
        cameraRenderImage.SetActive(true);

        yield return new WaitForSeconds(1);

        //CAMBIAMOS A CAMARA BATALLA
        if (Camera.main != null)
        {
            Camera.main.enabled = false;

        }

        cameraToFade.GetComponent<Camera>().targetTexture = null;
        cameraRenderImage.GetComponent<Animation>().Play();
        transitionDone = true;
        yield return new WaitForSeconds(2);
        cameraRenderImage.SetActive(false);
        cameraRenderImage.GetComponent<RawImage>().color = new Color(255, 255, 255, 255);
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
