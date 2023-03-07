using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationController : MonoBehaviour
{

    FighterController fighterController;
    // Start is called before the first frame update
    void Awake()
    {
        if (GetComponent<FighterController>() != null)
        {
            fighterController = GetComponent<FighterController>();
        } else
        {
            fighterController = transform.parent.GetComponent<FighterController>();
        }
    }


    public void AttackAnimationDone()
    {
        fighterController.AttackDone();
    }
}
