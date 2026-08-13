using System.Collections;
using UnityEngine;

public class FieldItem : Interactable
{
    [Header("Item Configuration")]
    [SerializeField] private string itemName;

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

        // 1. Lock gameplay and camera via GameState
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.Interacting);
        }

        // Prevent further interaction triggers
        gameObject.tag = "Untagged";

        // 2. Play picking animation on Player
        if (PlayerController.Instance != null && PlayerController.Instance.PlayerAnim != null)
        {
            PlayerController.Instance.PlayerAnim.SetTrigger("pickFloor");
        }

        yield return new WaitForSeconds(2.2f);

        // 3. Retrieve item data and show UI Notification
        if (ItemDatabase.Instance != null)
        {
            var itemData = ItemDatabase.Instance.GetItem(itemName);
            if (itemData != null && NotificationController.Instance != null)
            {
                StartCoroutine(NotificationController.Instance.AddNotification(
                    $"Has encontrado: {itemData.ItemName}",
                    itemData.Icon
                ));
            }
        }

        // 4. Add item to Inventory and play SFX
        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.AddItem(itemName);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance?.PlaySFX(SFXType.UI_ItemFound);
        }

        // 5. Hide world object visually while processing cleanup
        transform.position = new Vector3(0f, -1000f, 0f);

        yield return new WaitForSeconds(0.7f);

        // 6. Restore game state back to free exploration
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);
        }

        Destroy(gameObject, 5f);
    }
}