using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MinimapCameraController : MonoBehaviour
{
    GameObject player;
    GameObject arrow;
    private void Awake()
    {
        arrow = GameObject.FindGameObjectWithTag("Minimap").transform.Find("Arrow").gameObject;
        player = GameObject.FindGameObjectWithTag("Player");
    }
    // Start is called before the first frame update
    private void LateUpdate()
    {

        //Camera
        Vector3 newPosition = player.transform.position;
        newPosition.y = transform.position.y;
        transform.position = newPosition;

        //Arrow
        arrow.transform.rotation = Quaternion.Euler(0, 0, -player.transform.localEulerAngles.y) ;
    }
}
