using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIcon : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Awake()
    {
        mainCameraTransform = Camera.main.transform;

    }

    private void LateUpdate()
    {
        transform.LookAt(transform.position + mainCameraTransform.rotation *
            Vector3.forward, mainCameraTransform.rotation * Vector3.up);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
