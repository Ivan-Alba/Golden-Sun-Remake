using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum NotificationType
{
    Info,
    ItemObtained,
    KeyItemObtained,
    PsynergyLearned,
    LevelUp,
    QuestUpdate,
    Warning
}

/// <summary>
/// Controls the instantiation, styling, and lifecycle animations of notification cards in the HUD stream.
/// Listens exclusively to GameEvents.UI.OnShowNotification for decoupled messaging.
/// </summary>
public class NotificationController : MonoBehaviour
{
    #region Unity Lifecycle

    private void OnEnable()
    {
        // Event Subscriptions
        GameEvents.UI.OnShowNotification -= HandleShowNotification;
        GameEvents.UI.OnShowNotification += HandleShowNotification;
    }

    private void OnDisable()
    {
        // Event Unsubscriptions
        GameEvents.UI.OnShowNotification -= HandleShowNotification;
    }

    #endregion

    #region Event Handlers

    private void HandleShowNotification(NotificationType type, string message, Sprite icon)
    {
        ShowNotification(type, message, icon);
    }

    #endregion

    #region Public Notification API

    /// <summary>
    /// Displays a typed notification card with automatic formatting and styling based on NotificationType.
    /// </summary>
    public void ShowNotification(NotificationType type, string message, Sprite icon = null)
    {
        StartCoroutine(AddNotificationRoutine(type, message, icon));
    }

    /// <summary>
    /// Spawns a custom-categorized notification card with a dynamic string category.
    /// </summary>
    public void ShowNotification(string customCategory, string message, Sprite icon = null)
    {
        StartCoroutine(AddNotificationRoutine(NotificationType.Info, message, icon, customCategory));
    }

    #endregion

    #region Private Flow & Animation

    private IEnumerator AddNotificationRoutine(NotificationType type, string message, Sprite icon, string overrideCategory = null)
    {
        if (PlayerUIManager.Instance == null) yield break;

        VisualElement container = PlayerUIManager.Instance.GetNotificationsContainer();
        VisualTreeAsset template = PlayerUIManager.Instance.NotificationCardTemplate;

        if (container == null || template == null) yield break;

        // 1. Instantiate template
        TemplateContainer cardInstance = template.Instantiate();
        VisualElement cardElement = cardInstance.Q<VisualElement>("hud_notification_card") ?? cardInstance;

        cardElement.pickingMode = PickingMode.Ignore;

        // 2. Query child controls
        Label categoryLabel = cardElement.Q<Label>("label_notification_category");
        Label messageLabel = cardElement.Q<Label>("label_notification_message");
        VisualElement iconImg = cardElement.Q<VisualElement>("notification_icon_img");
        VisualElement accentBar = cardElement.Q<VisualElement>("notification_type_accent");

        // 3. Populate category label and CSS accent class
        string categoryTitle = !string.IsNullOrEmpty(overrideCategory) ? overrideCategory.ToUpper() : GetCategoryTitle(type);
        string accentClass = GetAccentClass(type);

        if (categoryLabel != null) categoryLabel.text = categoryTitle;
        if (messageLabel != null) messageLabel.text = message;

        if (accentBar != null && !string.IsNullOrEmpty(accentClass))
        {
            accentBar.AddToClassList(accentClass);
        }

        // 4. Assign icon sprite or hide frame if null
        if (iconImg != null)
        {
            if (icon != null)
            {
                iconImg.style.backgroundImage = Background.FromSprite(icon);
            }
            else
            {
                VisualElement frame = cardElement.Q<VisualElement>("notification_icon_frame");
                if (frame != null) frame.style.display = DisplayStyle.None;
            }
        }

        // 5. Configure native UI Toolkit CSS transition for opacity
        cardElement.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
        cardElement.style.transitionDuration = new List<TimeValue> { new TimeValue(0.4f, TimeUnit.Second) };
        cardElement.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOut) };

        // Initial transparent state
        cardElement.style.opacity = 0f;
        container.Add(cardElement);

        // Wait 1 frame for UI Toolkit to register position and opacity state
        yield return null;

        // 6. Reproduce sound effect for notification
        SFXType sfx = GetSFXForNotification(type);
        if (sfx != SFXType.None)
        {
            GameEvents.Audio.RequestPlaySFX(sfx);
        }

        // 7. Fade In (Triggered natively by UI Toolkit)
        cardElement.style.opacity = 1f;
        yield return new WaitForSeconds(0.4f);

        // 8. On-screen hold duration
        yield return new WaitForSeconds(4.0f);

        // 9. Fade Out (Triggered natively by UI Toolkit)
        cardElement.style.opacity = 0f;
        yield return new WaitForSeconds(0.4f);

        // 10. Remove node from hierarchy
        container.Remove(cardElement);
    }

    #endregion

    #region Helper Formatting Methods

    private string GetCategoryTitle(NotificationType type)
    {
        return type switch
        {
            NotificationType.ItemObtained => "NUEVO OBJETO",
            NotificationType.KeyItemObtained => "OBJETO CLAVE",
            NotificationType.PsynergyLearned => "NUEVA PSINERGÍA",
            NotificationType.LevelUp => "¡SUBIDA DE NIVEL!",
            NotificationType.QuestUpdate => "MISIONES",
            NotificationType.Warning => "ATENCIÓN",
            NotificationType.Info => "INFORMACIÓN",
            _ => "SISTEMA"
        };
    }

    private string GetAccentClass(NotificationType type)
    {
        return type switch
        {
            NotificationType.ItemObtained => "accent-item",
            NotificationType.KeyItemObtained => "accent-key-item",
            NotificationType.PsynergyLearned => "accent-psynergy",
            NotificationType.LevelUp => "accent-levelup",
            NotificationType.QuestUpdate => "accent-quest",
            NotificationType.Warning => "accent-warning",
            _ => "accent-info"
        };
    }

    /// <summary>
    /// Maps notification types to their corresponding sound effect.
    /// Returns SFXType.None if the notification type has no audio feedback.
    /// </summary>
    private SFXType GetSFXForNotification(NotificationType type)
    {
        return type switch
        {
            NotificationType.ItemObtained => SFXType.UI_ItemFound,
            NotificationType.Warning => SFXType.UI_Error,
            _ => SFXType.None
        };
    }

    #endregion
}