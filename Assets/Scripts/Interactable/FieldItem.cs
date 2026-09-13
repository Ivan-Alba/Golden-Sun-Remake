using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Handles field item pickups lying on the ground, player interaction animations, 
/// decoupled inventory transactions, localized UI notifications, and object cleanup.
/// </summary>
public class FieldItem : Interactable
{
    #region Serialized Fields

    [Header("Item Configuration")]
    [Tooltip("Item asset granted upon collecting this field item.")]
    [SerializeField] private ItemSO itemData;

    [Tooltip("Quantity of items granted upon pickup.")]
    [SerializeField] private int itemQuantity = 1;

    [Header("Timing Settings")]
    [Tooltip("Duration in seconds to wait for player floor picking animation before attempting inventory transaction.")]
    [SerializeField] private float playerPickDuration = 2.2f;

    [Tooltip("Delay in seconds after displaying notification before restoring game state and destroying.")]
    [SerializeField] private float endSequenceDelay = 0.7f;

    [Tooltip("Delay in seconds before destroying the GameObject after collection.")]
    [SerializeField] private float destroyDelay = 0.1f;

    [Header("Localization Table Entries (UI_Strings Table)")]
    [Tooltip("Localized entry for full inventory warning (e.g. 'Your inventory is full').")]
    [SerializeField] private LocalizedString inventoryFullMessage;

    #endregion

    #region Private Fields & Runtime State

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
    /// Initiates the ground item collection routine if not already picked up.
    /// </summary>
    public override void Interact()
    {
        if (itemPicked) return;

        StartCoroutine(PickItemRoutine());
    }

    #endregion

    #region Save System & Persistence Preparation

    /// <summary>
    /// Sets the item to an already collected state without triggering animations or granting items.
    /// Called by Save/Load systems when re-entering a previously visited scene.
    /// </summary>
    public void SetFieldItemCollectedState(bool collected)
    {
        itemPicked = collected;

        if (itemPicked)
        {
            gameObject.SetActive(false);
        }
    }

    #endregion

    #region Interaction Coroutine

    /// <summary>
    /// Coroutine sequence handling state locks, picking animation events, 
    /// inventory transaction validation, UI notification, and world object cleanup.
    /// </summary>
    private IEnumerator PickItemRoutine()
    {
        if (itemData == null)
        {
            Debug.LogError($"[FieldItem] No ItemSO assigned to field item '{gameObject.name}'!", this);
            yield break;
        }

        GameEvents.Player.RequestLookAt(transform.position);

        itemPicked = true;

        // Temporarily disable interaction detection via Layer change
        int defaultLayer = LayerMask.NameToLayer("Default");
        gameObject.layer = defaultLayer;

        // 1. Lock gameplay and camera via GameState event command
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Interacting);

        // 2. Play floor picking animation on Player via decoupled event
        GameEvents.Player.TriggerPlayInteractionAnimation(PlayerInteractionAnimType.PickFloor);

        yield return new WaitForSeconds(playerPickDuration);

        // 3. Attempt to add item to Inventory via decoupled event transaction
        int remaining = -1;
        GameEvents.Inventory.RequestAddItem(itemData, itemQuantity, (result, recipientHeroName) =>
        {
            remaining = result;
        });

        // FALLBACK CASE A: System error / No active inventory listener responded
        if (remaining < 0)
        {
            Debug.LogError($"[FieldItem] Transaction failed for item '{itemData.ItemSlug}'. No inventory handler responded to RequestAddItem.", this);

            // Rollback interaction state
            gameObject.layer = originalLayer;
            itemPicked = false;

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort remaining pickup and destruction routine
        }

        // FALLBACK CASE B: Backpacks/Inventory full (Overflow scenario)
        if (remaining > 0)
        {
            string fullMsg = inventoryFullMessage != null ? inventoryFullMessage.GetLocalizedString() : "Inventory Full";
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, fullMsg);

            // Restore original interactable layer state and re-allow interaction
            gameObject.layer = originalLayer;
            itemPicked = false;

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort destruction routine
        }

        // 4. Display item obtained UI notification using localized item name (UI handles notification sound internally)
        string localizedItemName = itemData.ItemNameLocalizedString != null
            ? itemData.ItemNameLocalizedString.GetLocalizedString()
            : itemData.ItemName;

        string notificationText = itemQuantity > 1
            ? $"{localizedItemName} x{itemQuantity}"
            : $"{localizedItemName}";

        GameEvents.UI.TriggerShowNotification(NotificationType.ItemObtained, notificationText, itemData.Icon);

        // Visually hide all renderers, colliders, animations, and Halo components immediately
        HideVisuals();

        yield return new WaitForSeconds(endSequenceDelay);

        // 5. Restore game state back to free exploration and destroy instance
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);

        Destroy(gameObject, destroyDelay);
    }

    /// <summary>
    /// Disables all renderers, colliders, and behaviours (Animation, Halo, Light, etc.) 
    /// attached to this object without stopping the coroutine on this script.
    /// </summary>
    private void HideVisuals()
    {
        // Disable all renderers (MeshRenderer, SpriteRenderer, etc.)
        var renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

        // Disable all colliders
        var colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        // Disable all Behaviours (This covers Animation, Halo, Lights, Projectors) except THIS script
        var behaviours = GetComponentsInChildren<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this)
            {
                behaviours[i].enabled = false;
            }
        }
    }

    #endregion
}