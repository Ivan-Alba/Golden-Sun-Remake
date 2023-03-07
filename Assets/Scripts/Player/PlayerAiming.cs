using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAiming : MonoBehaviour
{
    public Cinemachine.AxisState xAxis;
    public Cinemachine.AxisState yAxis;

    private Transform cameraLookAt;

    private void Awake()
    {
        cameraLookAt = transform.Find("Aim").Find("CameraLookAt");
    }

    private void OnEnable()
    {
        yAxis.Value = transform.eulerAngles.x;
        xAxis.Value = transform.eulerAngles.y;
    }

    // Update is called once per frame
    public void Reset()
    {
        xAxis.Value = 0;
        yAxis.Value = 0;
    }
    void FixedUpdate()
    {
        xAxis.Update(Time.fixedDeltaTime);
        yAxis.Update(Time.fixedDeltaTime);
        cameraLookAt.eulerAngles = new Vector3(yAxis.Value, xAxis.Value, 0);
        transform.eulerAngles = new Vector3(yAxis.Value,xAxis.Value, transform.eulerAngles.z);
    }
}
