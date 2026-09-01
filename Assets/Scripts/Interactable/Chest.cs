using System.Collections;
using UnityEngine;

/// <summary>
/// Handles treasure chest interactions, opening animations, inventory additions via event transactions,
/// and full-inventory/error fallbacks.
/// </summary>
public class Chest : Interactable
{
    [Header("Chest Content")]
    [SerializeField] private string itemSlug;
    [SerializeField] private int itemQuantity = 1;

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
        chestOpened = true;

        // Temporarily disable interaction detection via Layer change
        int defaultLayer = LayerMask.NameToLayer("Default");
        gameObject.layer = defaultLayer;

        // 1. Lock gameplay and camera via GameState event command
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Interacting);

        // Retrieve item data from ItemDatabase for notification styling
        ItemSO itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItem(itemSlug) : null;

        // Trigger player picking animation
        if (PlayerController.Instance != null && PlayerController.Instance.PlayerAnim != null)
        {
            PlayerController.Instance.PlayerAnim.SetTrigger("pickChest");
        }

        yield return new WaitForSeconds(1f);

        // Trigger chest opening animation
        if (chestAnimator != null)
        {
            chestAnimator.SetTrigger("openChest");
        }

        // Wait for chest opening animation to settle
        yield return new WaitForSeconds(2f);

        // 2. Request item addition to inventory via decoupled event transaction
        int remaining = -1;
        GameEvents.Inventory.RequestAddItem(itemSlug, itemQuantity, (result) =>
        {
            remaining = result;
        });

        // FALLBACK CASE A: System error / No active inventory listener responded
        if (remaining < 0)
        {
            Debug.LogError($"[Chest] Transaction failed for item '{itemSlug}'. No inventory handler responded to RequestAddItem.");

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

        // SUCCESS CASE (remaining == 0): Item successfully stored in inventory
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SFXType.UI_ItemFound);
        }

        // Show UI notification
        if (itemData != null)
        {
            string notificationMessage = itemQuantity > 1
                ? $"Has obtenido: {itemData.ItemName} x{itemQuantity}"
                : $"Has obtenido: {itemData.ItemName}";

            GameEvents.UI.TriggerShowNotification(NotificationType.ItemObtained, notificationMessage, itemData.Icon);
        }

        yield return new WaitForSeconds(0.5f);

        // 3. Restore game state back to free exploration (Chest remains in 'Default' layer permanently)
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
    }

    #endregion
}