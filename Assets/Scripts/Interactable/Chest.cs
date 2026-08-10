using System.Collections;
using UnityEngine;

public class Chest : Interactable
{
    [SerializeField] private string itemSlug;
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

        // Retrieve item data
        Item itemFound = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItem(itemSlug) : null;

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

        // Play item discovery sound and show UI notification
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GetComponent<Sounds>()?.PlaySound(GameManager.ITEM_FOUNDED_SOUND);
        }

        if (itemFound != null && NotificationController.Instance != null)
        {
            StartCoroutine(NotificationController.Instance.AddNotification($"Has obtenido: {itemFound.ItemName}", itemFound.Icon));
        }

        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.AddItem(itemSlug);
        }

        yield return new WaitForSeconds(1f);

        // 2. Restore game state back to free exploration
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);
        }
    }
}