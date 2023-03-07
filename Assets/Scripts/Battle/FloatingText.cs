using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    private float destroyTime = 1f;
    GameObject battleCamera;

    // Start is called before the first frame update
    void Start()
    {
        Destroy(gameObject, destroyTime);
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
            battleCamera = GameObject.Find("BattleCamera");
        transform.LookAt(battleCamera.transform);

    }


}
