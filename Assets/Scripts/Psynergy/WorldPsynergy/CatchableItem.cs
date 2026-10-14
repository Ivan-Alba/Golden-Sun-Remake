using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Attached to world items that can be collected from a distance using the Catch Psynergy ability.
/// Validates inventory capacity upon hand grasp, hides world mesh during hand return travel,
/// fires localization-aware notification events upon sequence completion, and deactivates itself.
/// </summary>
public class CatchableItem : PsynergyInteractable
{
    #region Serialized Fields

    [Header("Item & Collection Settings")]
    [Tooltip("The ScriptableObject item definition added to inventory upon collection.")]
    [SerializeField] private ItemSO itemData;

    [Tooltip("Amount of units of this item rewarded upon catching.")]
    [SerializeField] private int quantity = 1;

    [Header("Localization Table Entries (UI_Strings Table)")]
    [Tooltip("Localized entry for full inventory warning (e.g. 'Your inventory is full').")]
    [SerializeField] private LocalizedString inventoryFullMessage;

    #endregion

    #region Private Runtime State

    private bool isBeingCaught;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        // Enforce Catch action configuration for PsynergyInteractable base
        requiredWorldAction = WorldActionType.Catch;
        singleUse = true; // Items can only be caught/collected once
    }

    #endregion

    #region IPsynergyTargetable Overrides

    public override bool CanBeTargeted()
    {
        // Item cannot be targeted if already being processed/caught or used
        return base.CanBeTargeted() && !isBeingCaught;
    }

    /// <summary>
    /// Phase 1: Fired at the keyframe of hand impact/grasp (AE_OnImpactOrGrasp).
    /// Performs transactional inventory check and hides physical visuals while held by the hand.
    /// </summary>
    protected override void OnPsynergyTriggered(PsynergySO psynergy, GameObject player)
    {
        if (player == null || itemData == null)
        {
            Debug.LogWarning($"[CatchableItem] Unable to execute Catch on {gameObject.name}. Missing player or ItemSO reference.");
            return;
        }

        // 1. Transactional Inventory Check: Attempt to add items before moving/hiding the object
        int remaining = -1;
        GameEvents.Inventory.RequestAddItem(itemData, quantity, (result, recipientHeroName) =>
        {
            remaining = result;
        });

        // FALLBACK CASE A: System error or no inventory listener responded
        if (remaining < 0)
        {
            Debug.LogError($"[CatchableItem] Transaction failed for item '{itemData.ItemSlug}'. No inventory handler responded.", this);
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);
            return;
        }

        // FALLBACK CASE B: Backpacks/Inventory Full (Overflow scenario)
        if (remaining > 0)
        {
            string fullMsg = inventoryFullMessage != null ? inventoryFullMessage.GetLocalizedString() : "Inventory Full";
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, fullMsg);
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);

            Debug.Log($"[CatchableItem] Could not catch '{itemData.ItemName}'. Inventory is full (Remaining overflow: {remaining}).");
            return;
        }

        // 2. Transaction successful: Hide world visuals (object is now held inside the Psynergy hand)
        isBeingCaught = true;
        SetItemVisualsVisible(false);
    }

    /// <summary>
    /// Phase 2: Fired when the visual sequence (hand return flight and despawn) finishes completely.
    /// Broadcasts localized acquisition UI notifications, plays audio, and deactivates the object.
    /// </summary>
    public override void OnPsynergySequenceFinished(PsynergySO psynergy, GameObject caster)
    {
        if (!isBeingCaught) return;

        // 1. Format localized message and broadcast UI notification
        string localizedItemName = itemData.ItemNameLocalizedString != null
            ? itemData.ItemNameLocalizedString.GetLocalizedString()
            : itemData.ItemName;

        string notificationMessage = quantity > 1
            ? $"{localizedItemName} x{quantity}"
            : $"{localizedItemName}";

        if (itemData.ItemType == ItemType.KeyItem)
        {
            GameEvents.UI.TriggerShowNotification(NotificationType.KeyItemObtained, notificationMessage, itemData.Icon);
        }
        else
        {
            GameEvents.UI.TriggerShowNotification(NotificationType.ItemObtained, notificationMessage, itemData.Icon);
        }

        // 2. Play item obtained SFX
        GameEvents.Audio.RequestPlaySFX(SFXType.UI_ItemFound);

        Debug.Log($"[CatchableItem] Successfully caught {quantity}x '{itemData.ItemName}'. Added to inventory.");

        // 3. Deactivate world object
        gameObject.SetActive(false);
    }

    #endregion

    #region Private Helpers

    private void SetItemVisualsVisible(bool visible)
    {
        var renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible;
        }

        var colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = visible;
        }
    }

    #endregion
}