using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationController : MonoBehaviour
{
    // Singleton Instance
    public static NotificationController Instance { get; private set; }

    #region Private Fields

    private GameObject notifPrefab;
    private GameObject container;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Load notification UI prefab dynamically
        notifPrefab = Resources.Load<GameObject>("Prefabs/UIComponents/PlayerUI/Notification");

        // Safely cache container reference
        ContentSizeFitter sizeFitter = GetComponentInChildren<ContentSizeFitter>();
        if (sizeFitter != null)
        {
            container = sizeFitter.gameObject;
        }
        else
        {
            Debug.LogError("[NotificationController] ContentSizeFitter component not found in children!");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Public Notification Methods

    /// <summary>
    /// Instantiates a notification UI element, configures its message and icon, and handles smooth fade transitions.
    /// </summary>
    public IEnumerator AddNotification(string message, Sprite icon)
    {
        if (notifPrefab == null || container == null)
        {
            Debug.LogWarning("[NotificationController] Cannot display notification: Prefab or Container reference is missing.");
            yield break;
        }

        GameObject newNotification = Instantiate(notifPrefab, container.transform);

        // Assign message text
        Transform messageTransform = newNotification.transform.Find("Message");
        if (messageTransform != null && messageTransform.TryGetComponent<TextMeshProUGUI>(out var textComp))
        {
            textComp.SetText(message);
        }

        // Assign icon sprite
        Transform iconTransform = newNotification.transform.Find("Icon");
        if (iconTransform != null && iconTransform.TryGetComponent<Image>(out var imageComp))
        {
            imageComp.sprite = icon;
        }

        // Handle fade-in, display duration, and fade-out transitions
        if (newNotification.TryGetComponent<CanvasGroup>(out var canvasGroup))
        {
            // Fade In
            while (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha += 0.2f;
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(3f);

            // Fade Out
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= 0.2f;
                yield return new WaitForSeconds(0.1f);
            }
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }

        Destroy(newNotification);
    }

    #endregion
}