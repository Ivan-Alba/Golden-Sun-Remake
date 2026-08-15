using System.Collections;
using UnityEngine;

public class Chest : Interactable
{
    [Header("Chest Content")]
    [SerializeField] private string itemSlug;
    [SerializeField] private int itemQuantity = 1;

    private bool chestOpened;

    public override void Interact()
    {
        // Prevent interacting if chest is already opened or opening process has started
        if (chestOpened) return;

        StartCoroutine(OpenChestRoutine());
    }

    private IEnumerator OpenChestRoutine()
    {
        chestOpened = true;

        // 1. Lock gameplay and camera via GameState
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.Interacting);
        }

        // Prevent further interaction triggers
        gameObject.tag = "Untagged";

        // Retrieve item data using the new ItemSO architecture
        ItemSO itemFound = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItem(itemSlug) : null;

        // Trigger player animation
        if (PlayerController.Instance != null && PlayerController.Instance.PlayerAnim != null)
        {
            PlayerController.Instance.PlayerAnim.SetTrigger("pickChest");
        }

        yield return new WaitForSeconds(1f);

        // Trigger chest opening animation
        if (TryGetComponent<Animator>(out var chestAnim))
        {
            chestAnim.SetTrigger("openChest");
        }

        // Wait for chest opening animation to finish
        yield return new WaitForSeconds(2f);

        // Add item to inventory and retrieve overflow
        if (InventoryController.Instance != null)
        {
            int remaining = InventoryController.Instance.AddItem(itemSlug, itemQuantity);

            // TODO: Handle full inventory scenario.
            // If 'remaining > 0', it means all hero backpacks are full.
            // In the future, we should:
            // 1. Show a notification: "Backpacks are full!".
            // 2. Revert 'chestOpened = false' and restore the 'Interactable' tag.
            // 3. Keep the chest closed (or re-closable) so the loot isn't lost.
        }


        // Play item discovery sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SFXType.UI_ItemFound);
        }

        // Show UI notification
        if (itemFound != null && NotificationController.Instance != null)
        {
            string notificationMessage = itemQuantity > 1
                ? $"Has obtenido: {itemFound.ItemName} x{itemQuantity}"
                : $"Has obtenido: {itemFound.ItemName}";

            StartCoroutine(NotificationController.Instance.AddNotification(notificationMessage, itemFound.Icon));
        }

        yield return new WaitForSeconds(1f);

        // 2. Restore game state back to free exploration
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);
        }
    }
}