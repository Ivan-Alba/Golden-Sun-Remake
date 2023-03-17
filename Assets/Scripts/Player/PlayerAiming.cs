using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAiming : MonoBehaviour
{
    public Cinemachine.AxisState xAxis;
    public Cinemachine.AxisState yAxis;

    private Transform cameraLookAt;
    private Transform aimParent;

    private GameObject target;
    public bool isPsynergyAnimation;
    private void Awake()
    {
        aimParent = transform.Find("Aim");
        target = null;
    }

    private void Update()
    {

        //KEY 1
        if (!isPsynergyAnimation && Input.GetKey(KeyCode.Alpha1) && target != null)
        {
            PsynergyWorldController.Instance.UsePsynergy(0, target);
        };
    }

    private void OnEnable()
    {
        target = null;
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
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, xAxis.Value * 0.3f, transform.eulerAngles.z);
        aimParent.eulerAngles = new Vector3(yAxis.Value * 0.3f, xAxis.Value * 0.3f, 0);

        Ray ray = aimParent.Find("AimCamera").GetComponent<Camera>().ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {

            if (hit.transform.gameObject != null)
            {
                target = hit.transform.gameObject;
            }
            else
            {
                target = null;
            }
        }



    }
}
