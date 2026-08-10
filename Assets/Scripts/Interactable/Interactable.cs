using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    private NotificationController notifController;

    // Protected property with lazy loading to prevent NullReferenceExceptions
    protected NotificationController NotifController
    {
        get
        {
            if (notifController == null)
            {
                GameObject notifObj = GameObject.Find("Notifications");
                if (notifObj != null)
                {
                    notifController = notifObj.GetComponent<NotificationController>();
                }
            }
            return notifController;
        }
    }

    public virtual void Interact()
    {

    }
}