using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Attached to world items that can be collected from a distance using the Catch Psynergy ability.
/// Validates inventory capacity prior to triggering flight animation, executes parabolic flight toward the hero,
/// fires localization-aware notification events, and deactivates itself upon collection.
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

    [Header("Catch Flight Animation Parameters")]
    [Tooltip("Duration in seconds for the item to fly from its initial position to the player.")]
    [SerializeField] private float flightDuration = 0.65f;

    [Tooltip("Maximum arc elevation height added to the flight trajectory.")]
    [SerializeField] private float arcHeight = 1.2f;

    [Tooltip("Height offset relative to the player position where the item finishes its flight (e.g. 1.0m = chest height).")]
    [SerializeField] private float playerReceiveHeightOffset = 1.0f;

    #endregion

    #region Private Runtime State

    private bool isFlying;

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
        // Item cannot be targeted if already executing flight or collected
        return base.CanBeTargeted() && !isFlying;
    }

    protected override void OnPsynergyTriggered(PsynergySO psynergy, GameObject player)
    {
        if (player == null || itemData == null)
        {
            Debug.LogWarning($"[CatchableItem] Unable to execute Catch on {gameObject.name}. Missing player or ItemSO reference.");
            CompleteInteraction();
            return;
        }

        StartCoroutine(Routine_ExecuteCatchFlight(player));
    }

    #endregion

    #region Private Execution Routines

    private IEnumerator Routine_ExecuteCatchFlight(GameObject player)
    {
        // 1. Transactional Inventory Check: Attempt to add items before moving the physical object
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
            CompleteInteraction();
            yield break;
        }

        // FALLBACK CASE B: Backpacks/Inventory Full (Overflow scenario)
        if (remaining > 0)
        {
            string fullMsg = inventoryFullMessage != null ? inventoryFullMessage.GetLocalizedString() : "Inventory Full";
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, fullMsg);
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);

            Debug.Log($"[CatchableItem] Could not catch '{itemData.ItemName}'. Inventory is full (Remaining overflow: {remaining}).");

            // Abort flight and restore exploration state
            CompleteInteraction();
            yield break;
        }

        // 2. Transaction successful: Execute parabolic flight animation toward player
        isFlying = true;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < flightDuration)
        {
            float t = elapsedTime / flightDuration;

            // Target position follows player in real-time up to chest height
            Vector3 targetPosition = player.transform.position + Vector3.up * playerReceiveHeightOffset;

            // Parabolic flight math
            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = currentPos;

            // Aesthetic Y-axis spin during flight
            transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Snap to exact final target position at chest level
        Vector3 finalPlayerPos = player.transform.position + Vector3.up * playerReceiveHeightOffset;
        transform.position = finalPlayerPos;

        // 3. Format localized message and broadcast UI notification
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

        // 4. Play item obtained SFX
        GameEvents.Audio.RequestPlaySFX(SFXType.UI_ItemFound);

        Debug.Log($"[CatchableItem] Successfully caught {quantity}x '{itemData.ItemName}'. Added to inventory.");

        isFlying = false;

        // 5. Release game state lock and deactivate object
        CompleteInteraction();
        gameObject.SetActive(false);
    }

    #endregion
}