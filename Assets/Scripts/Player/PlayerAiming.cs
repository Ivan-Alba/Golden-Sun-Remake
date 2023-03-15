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
            
            //PSINERGIA ASIGNADA A 1 BUSCAR TAG EN JSON
            Psynergy usedPsynergy = (Psynergy)PsynergyWorldController.Instance.HeroShortcuts[GameManager.instance.ActiveHero.name].GetValue(0);
            if (usedPsynergy != null)
            {
                Debug.Log($"Has usado la psinergía: {usedPsynergy.SlugName}");
                //SI TARGET == PSINERGIA TAG
                if (target.tag == $"{usedPsynergy.SlugName}Reactive")
                {
                    //LLAMAR PSINERGÍA USO

                    Destroy(target.gameObject);

                }
            } else
            {
                Debug.Log($"No hay psinergía asignada");
            }

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
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, xAxis.Value, transform.eulerAngles.z);
        aimParent.eulerAngles = new Vector3(yAxis.Value, xAxis.Value, 0);

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
