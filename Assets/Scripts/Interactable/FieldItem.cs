using System.Collections;
using UnityEngine;

public class FieldItem : Interactable
{
    [Header("Item Configuration")]
    [SerializeField] private string itemSlug;
    [SerializeField] private int itemQuantity = 1;

    private bool itemPicked;

    public override void Interact()
    {
        // Prevent multiple interactions while the picking routine is active
        if (itemPicked) return;

        StartCoroutine(PickItemRoutine());
    }

    private IEnumerator PickItemRoutine()
    {
        itemPicked = true;
        string originalTag = gameObject.tag;

        // 1. Lock gameplay and camera via GameState
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.Interacting);
        }

        // Temporarily prevent further interaction triggers during animation
        gameObject.tag = "Untagged";

        // 2. Play picking animation on Player
        if (PlayerController.Instance != null && PlayerController.Instance.PlayerAnim != null)
        {
            PlayerController.Instance.PlayerAnim.SetTrigger("pickFloor");
        }

        yield return new WaitForSeconds(2.2f);

        // Retrieve item data from ItemDatabase
        ItemSO itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItem(itemSlug) : null;

        // 3. Attempt to add item to Inventory and evaluate remaining space
        int remaining = itemQuantity;
        if (InventoryController.Instance != null)
        {
            remaining = InventoryController.Instance.AddItem(itemSlug, itemQuantity);
        }

        // 4. Handle Full Inventory Scenario (Cannot fit item)
        if (remaining > 0)
        {
            if (NotificationController.Instance != null)
            {
                StartCoroutine(NotificationController.Instance.AddNotification(
                    "Tus mochilas están llenas",
                    itemData != null ? itemData.Icon : null
                ));
            }

            // Restore interactable state and game control without destroying object
            gameObject.tag = originalTag;
            itemPicked = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);
            }

            yield break; // Cancel remaining item destruction routine
        }

        // 5. Successful Pickup Scenario
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SFXType.UI_ItemFound);
        }

        if (itemData != null && NotificationController.Instance != null)
        {
            string notificationText = itemQuantity > 1
                ? $"Has encontrado: {itemData.ItemName} x{itemQuantity}"
                : $"Has encontrado: {itemData.ItemName}";

            StartCoroutine(NotificationController.Instance.AddNotification(
                notificationText,
                itemData.Icon
            ));
        }

        // Hide world object visually while processing cleanup
        transform.position = new Vector3(0f, -1000f, 0f);

        yield return new WaitForSeconds(0.7f);

        // Restore game state back to free exploration
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);
        }

        Destroy(gameObject, 5f);
    }
}