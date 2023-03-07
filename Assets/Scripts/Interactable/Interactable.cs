using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    protected NotificationController notifController;

    private void Start()
    {
        notifController = GameObject.Find("Notifications").GetComponent<NotificationController>();
    }

    public virtual void Interact()
    {

    }

}
