using System.Collections;
using UnityEngine;

/// <summary>
/// Handles field item pickups (items lying on the ground), animations, inventory additions, and destruction.
/// </summary>
public class FieldItem : Interactable
{
    [Header("Item Configuration")]
    [SerializeField] private string itemSlug;
    [SerializeField] private int itemQuantity = 1;

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

    private IEnumerator PickItemRoutine()
    {
        itemPicked = true;

        // Temporarily disable interaction detection via Layer change
        int defaultLayer = LayerMask.NameToLayer("Default");
        gameObject.layer = defaultLayer;

        // 1. Lock gameplay and camera via GameState
        GameManager.Instance?.SetGameState(GameManager.GameState.Interacting);

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
            NotificationController.Instance?.ShowNotification(NotificationType.Warning, "Tus mochilas están llenas");

            // Restore original interactable layer and reset flag
            gameObject.layer = originalLayer;
            itemPicked = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);
            }

            yield break; // Abort destruction routine
        }

        // 5. Successful Pickup Scenario
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SFXType.UI_ItemFound);
        }

        if (itemData != null && NotificationController.Instance != null)
        {
            string notificationText = itemQuantity > 1
                ? $"Has recogido: {itemData.ItemName} x{itemQuantity}"
                : $"Has recogido: {itemData.ItemName}";

            NotificationController.Instance.ShowNotification(NotificationType.ItemObtained, notificationText, itemData.Icon);
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

    #endregion
}