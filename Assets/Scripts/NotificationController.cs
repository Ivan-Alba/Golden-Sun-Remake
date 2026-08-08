using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationController : MonoBehaviour
{
    GameObject notifPrefab;
    GameObject container;

    // Start is called before the first frame update
    void Awake()
    {
        notifPrefab = Resources.Load("Prefabs/UIComponents/PlayerUI/Notification") as GameObject;
        container = gameObject.GetComponentInChildren<ContentSizeFitter>().gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator AddNotification(string message, Sprite icon)
    {
        GameObject newNotification = Instantiate(notifPrefab, container.transform);
        newNotification.transform.Find("Message").GetComponent<TextMeshProUGUI>().SetText(message);
        newNotification.transform.Find("Icon").GetComponent<Image>().sprite = icon;

        while (newNotification.GetComponent<CanvasGroup>().alpha < 1)
        {
            newNotification.GetComponent<CanvasGroup>().alpha += 0.2f;
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(3);

        while (newNotification.GetComponent<CanvasGroup>().alpha > 0)
        {
            newNotification.GetComponent<CanvasGroup>().alpha -= 0.2f;
            yield return new WaitForSeconds(0.1f);
        }

        Destroy(newNotification);
    }

}
