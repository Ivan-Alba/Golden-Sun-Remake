using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldItem : Interactable
{
    [Header("Item Configuration")]
    public string itemName;

    private GameObject player;

    public override void Interact()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player != null)
        {
            StartCoroutine(PickItem());
        }
        else
        {
            Debug.LogWarning($"[FieldItem] Cannot pick {itemName}: Player object not found!");
        }
    }

    private IEnumerator PickItem()
    {
        // 1. Stop Player Movement
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.StopPlayerController = true;
        }

        // 2. Play Animation
        Animator playerAnim = player.GetComponent<Animator>();
        if (playerAnim != null)
        {
            playerAnim.SetTrigger("pickFloor");
        }

        yield return new WaitForSeconds(2.2f);

        // 3. Show Notification UI
        if (NotifController != null && ItemDatabase.Instance != null)
        {
            var itemData = ItemDatabase.Instance.GetItem(itemName);
            if (itemData != null)
            {
                StartCoroutine(NotifController.AddNotification(
                    $"Has encontrado: {itemData.ItemName}",
                    itemData.Icon
                ));
            }
        }

        // 4. Add item to Inventory and Play SFX
        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.AddItem(itemName);
        }

        if (GameManager.Instance != null)
        {
            Sounds soundSystem = GameManager.Instance.GetComponent<Sounds>();
            if (soundSystem != null)
            {
                soundSystem.PlaySound(GameManager.ITEM_FOUNDED_SOUND);
            }
        }

        // 5. Hide world object immediately to prevent re-interaction
        transform.position = new Vector3(0, -1000f, 0);

        yield return new WaitForSeconds(0.7f);

        // 6. Restore Player Movement and Destroy object
        if (playerController != null)
        {
            playerController.StopPlayerController = false;
        }

        Destroy(gameObject, 5f);
    }
}