using System.Collections;
using UnityEngine;

/// <summary>
/// Handles field item pickups (items lying on the ground), picking animations, 
/// decoupled inventory transactions via events, and object cleanup/destruction.
/// </summary>
public class FieldItem : Interactable
{
    [Header("Item Configuration")]
    [SerializeField] private ItemSO itemData;
    [SerializeField] private int itemQuantity = 1;

    [Header("Timing Settings")]
    [Tooltip("Duration to wait for the player's ground picking animation to complete before processing inventory.")]
    [SerializeField] private float playerPickDuration = 2.2f;

    [Tooltip("Delay after triggering the notification before restoring player movement state.")]
    [SerializeField] private float endSequenceDelay = 0.7f;

    [Tooltip("Delay before destroying the GameObject after hidden from the scene.")]
    [SerializeField] private float destroyDelay = 5f;

    #region Private Fields

    private bool itemPicked;
    private int originalLayer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        originalLayer = gameObject.layer;
    }

    #endregion

    #region Interactable Overrides

    /// <summary>
    /// Triggers the item pickup routine if it hasn't been collected yet.
    /// </summary>
    public override void Interact()
    {
        if (itemPicked) return;

        StartCoroutine(PickItemRoutine());
    }

    #endregion

    #region Interaction Coroutine

    /// <summary>
    /// Coroutine sequence that handles state locks, picking animations, 
    /// inventory addition event transactions, and world object destruction.
    /// </summary>
    private IEnumerator PickItemRoutine()
    {
        if (itemData == null)
        {
            Debug.LogError($"[FieldItem] No ItemSO assigned to field item '{gameObject.name}'!");
            yield break;
        }

        itemPicked = true;

        // Temporarily disable interaction detection via Layer change
        int defaultLayer = LayerMask.NameToLayer("Default");
        gameObject.layer = defaultLayer;

        // 1. Lock gameplay and camera via GameState event command
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Interacting);

        // 2. Play picking animation on Player via decoupled event
        GameEvents.Player.TriggerPlayInteractionAnimation(PlayerInteractionAnimType.PickFloor);

        yield return new WaitForSeconds(playerPickDuration);

        // 3. Attempt to add item to Inventory via decoupled event transaction
        int remaining = -1;
        GameEvents.Inventory.RequestAddItem(itemData, itemQuantity, (result) =>
        {
            remaining = result;
        });

        // FALLBACK CASE A: System error / No active inventory listener responded
        if (remaining < 0)
        {
            Debug.LogError($"[FieldItem] Transaction failed for item '{itemData.ItemSlug}'. No inventory handler responded to RequestAddItem.");

            // Rollback interaction state
            gameObject.layer = originalLayer;
            itemPicked = false;

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort remaining pickup and destruction routine
        }

        // FALLBACK CASE B: Backpacks are full (Overflow scenario)
        if (remaining > 0)
        {
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, "Tus mochilas están llenas");

            // Restore original interactable layer state and re-allow interaction
            gameObject.layer = originalLayer;
            itemPicked = false;

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort destruction routine
        }

        if (itemData != null)
        {
            string notificationText = itemQuantity > 1
                ? $"Has recogido: {itemData.ItemName} x{itemQuantity}"
                : $"Has recogido: {itemData.ItemName}";

            GameEvents.UI.TriggerShowNotification(NotificationType.ItemObtained, notificationText, itemData.Icon);
        }

        // Hide world object visually while processing cleanup
        transform.position = new Vector3(0f, -1000f, 0f);

        yield return new WaitForSeconds(endSequenceDelay);

        // Restore game state back to free exploration
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);

        Destroy(gameObject, destroyDelay);
    }

    #endregion
}