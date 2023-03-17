using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PsynergyReactive : MonoBehaviour
{
    GameObject player;
    
    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");    
    }

    //Qué hace el objeto cuando recibe la psinergía con la que reacciona
    public void React()
    {
        switch (gameObject.tag)
        {
            case "CatchReactive":
                CatchReactive();
                break;
            case "MoveReactive":
                break;
        }
    }

    //CATCH
    public void CatchReactive()
    {
        Destroy(gameObject);
    }
}
