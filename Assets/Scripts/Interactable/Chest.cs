using System.Collections;
using UnityEngine;

/// <summary>
/// Handles treasure chest interactions, opening animations, inventory additions, and full-inventory fallbacks.
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

    private IEnumerator OpenChestRoutine()
    {
        chestOpened = true;

        // Temporarily disable interaction detection via Layer change
        int defaultLayer = LayerMask.NameToLayer("Default");
        gameObject.layer = defaultLayer;

        // 1. Lock gameplay and camera via GameState
        GameManager.Instance?.SetGameState(GameManager.GameState.Interacting);

        // Retrieve item data from ItemDatabase
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

        // Wait for chest opening animation to finish
        yield return new WaitForSeconds(2f);

        // Add item to inventory and retrieve overflow amount
        if (InventoryController.Instance != null)
        {
            int remaining = InventoryController.Instance.AddItem(itemSlug, itemQuantity);

            // Handle Full Inventory Scenario
            if (remaining > 0)
            {
                NotificationController.Instance?.ShowNotification(NotificationType.Warning, "Tus mochilas están llenas");

                // Restore interactable layer state and re-allow interaction
                gameObject.layer = originalLayer;
                chestOpened = false;

                // Re-close chest animation
                if (chestAnimator != null)
                {
                    chestAnimator.SetTrigger("closeChest");
                    yield return new WaitForSeconds(1f);
                }

                GameManager.Instance?.SetGameState(GameManager.GameState.WorldExploration);
                yield break; // Abort remaining pickup flow
            }

            // Play item discovery sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SFXType.UI_ItemFound);
            }

            // Show UI notification
            if (itemData != null && NotificationController.Instance != null)
            {
                string notificationMessage = itemQuantity > 1
                    ? $"Has obtenido: {itemData.ItemName} x{itemQuantity}"
                    : $"Has obtenido: {itemData.ItemName}";

                NotificationController.Instance.ShowNotification(NotificationType.ItemObtained, notificationMessage, itemData.Icon);
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 2. Restore game state back to free exploration (Chest remains in 'Default' layer permanently)
        GameManager.Instance?.SetGameState(GameManager.GameState.WorldExploration);
    }

    #endregion
}