using System.Collections;
using UnityEngine;

/// <summary>
/// Handles treasure chest interactions, opening animations, inventory additions via event transactions,
/// and full-inventory/error fallbacks.
/// </summary>
public class Chest : Interactable
{
    [Header("Chest Content")]
    [SerializeField] private ItemSO itemData;
    [SerializeField] private int itemQuantity = 1;

    [Header("Timing Settings")]
    [Tooltip("Delay after player starts picking animation before opening the chest.")]
    [SerializeField] private float playerPickDuration = 1.0f;

    [Tooltip("Time required for the chest opening animation to settle.")]
    [SerializeField] private float chestOpeningDuration = 2.0f;

    [Tooltip("Pause duration after showing item notification before releasing game state.")]
    [SerializeField] private float endSequenceDelay = 0.5f;

    #region Private Fields

    private bool chestOpened;
    private Animator chestAnimator;
    private int originalLayer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        chestAnimator = GetComponent<Animator>();
        originalLayer = gameObject.layer;
    }

    #endregion

    #region Interactable Overrides

    /// <summary>
    /// Triggers the chest opening routine if it hasn't been opened yet.
    /// </summary>
    public override void Interact()
    {
        if (chestOpened) return;

        StartCoroutine(OpenChestRoutine());
    }

    #endregion

    #region Interaction Coroutine

    /// <summary>
    /// Coroutine sequence that handles state locks, opening animations, 
    /// inventory addition callbacks, and transactional rollback logic.
    /// </summary>
    private IEnumerator OpenChestRoutine()
    {
        if (itemData == null)
        {
            Debug.LogError($"[Chest] No ItemSO assigned to chest '{gameObject.name}'!");
            yield break;
        }

        GameEvents.Player.RequestLookAt(transform.position);

        chestOpened = true;

        // Temporarily disable interaction detection via Layer change
        int defaultLayer = LayerMask.NameToLayer("Default");
        gameObject.layer = defaultLayer;

        // 1. Lock gameplay and camera via GameState event command
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Interacting);

        // Decoupled event request for player animation
        GameEvents.Player.TriggerPlayInteractionAnimation(PlayerInteractionAnimType.PickChest);

        yield return new WaitForSeconds(playerPickDuration);

        // Trigger chest opening animation
        if (chestAnimator != null)
        {
            chestAnimator.SetTrigger("openChest");
        }

        // Wait for chest opening animation to settle
        yield return new WaitForSeconds(chestOpeningDuration);

        // 2. Request item addition to inventory via decoupled event transaction
        int remaining = -1;
        GameEvents.Inventory.RequestAddItem(itemData, itemQuantity, (result) =>
        {
            remaining = result;
        });

        // FALLBACK CASE A: System error / No active inventory listener responded
        if (remaining < 0)
        {
            Debug.LogError($"[Chest] Transaction failed for item '{itemData.ItemSlug}'. No inventory handler responded to RequestAddItem.");

            // Rollback interaction state
            gameObject.layer = originalLayer;
            chestOpened = false;

            if (chestAnimator != null)
            {
                chestAnimator.SetTrigger("closeChest");
            }

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort remaining pickup flow
        }

        // FALLBACK CASE B: Backpacks are full (Overflow scenario)
        if (remaining > 0)
        {
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, "Tus mochilas están llenas");

            // Restore interactable layer state and re-allow interaction
            gameObject.layer = originalLayer;
            chestOpened = false;

            // Re-close chest animation
            if (chestAnimator != null)
            {
                chestAnimator.SetTrigger("closeChest");
                yield return new WaitForSeconds(1f);
            }

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort remaining pickup flow
        }

        // Show UI notification
        if (itemData != null)
        {
            string notificationMessage = itemQuantity > 1
                ? $"Has obtenido: {itemData.ItemName} x{itemQuantity}"
                : $"Has obtenido: {itemData.ItemName}";

            GameEvents.UI.TriggerShowNotification(NotificationType.ItemObtained, notificationMessage, itemData.Icon);
        }

        yield return new WaitForSeconds(endSequenceDelay);

        // 3. Restore game state back to free exploration (Chest remains in 'Default' layer permanently)
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
    }

    #endregion
}