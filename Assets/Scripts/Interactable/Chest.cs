using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Handles treasure chest interactions, opening/closing animation sequences, 
/// decoupled inventory transactions, localization-aware notifications, and 3D spatialized SFX.
/// </summary>
public class Chest : Interactable
{
    #region Serialized Fields

    [Header("Chest Content")]
    [Tooltip("Item asset granted to the player upon opening the chest.")]
    [SerializeField] private ItemSO itemData;

    [Tooltip("Quantity of items rewarded from this chest.")]
    [SerializeField] private int itemQuantity = 1;

    [Header("Timing Settings")]
    [Tooltip("Delay in seconds after player initiates pick interaction before opening chest lid.")]
    [SerializeField] private float playerPickDuration = 1.0f;

    [Tooltip("Duration in seconds required for chest opening animation sequence to settle.")]
    [SerializeField] private float chestOpeningDuration = 2.0f;

    [Tooltip("Pause duration in seconds after displaying item notification before releasing game state lock.")]
    [SerializeField] private float endSequenceDelay = 0.5f;

    [Header("Localization Table Entries (UI_Strings Table)")]
    [Tooltip("Localized entry for full inventory warning (e.g. 'Your inventory is full').")]
    [SerializeField] private LocalizedString inventoryFullMessage;

    [SerializeField] private Animator chestAnimator;

    #endregion

    #region Private Fields & Runtime State

    private bool chestOpened;
    private int originalLayer;
    private static readonly int OpenChestHash = Animator.StringToHash("openChest");
    private static readonly int CloseChestHash = Animator.StringToHash("closeChest");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        originalLayer = gameObject.layer;
    }

    #endregion

    #region Interactable Overrides

    /// <summary>
    /// Initiates the chest interaction sequence if not already opened.
    /// </summary>
    public override void Interact()
    {
        if (chestOpened) return;

        StartCoroutine(OpenChestRoutine());
    }

    #endregion

    #region Save System & Persistence Preparation

    /// <summary>
    /// Sets the chest to an already opened state without triggering animations or item grants.
    /// Used by Save/Load systems when reloading an explored scene.
    /// </summary>
    public void SetChestOpenedState(bool opened)
    {
        chestOpened = opened;

        if (chestOpened)
        {
            // Set interaction layer to Default to prevent future raycasts/prompts
            gameObject.layer = LayerMask.NameToLayer("Default");

            if (chestAnimator != null)
            {
                chestAnimator.Play(OpenChestHash, 0, 1.0f); // Jump directly to opened pose
            }
        }
    }

    #endregion

    #region Animation Events (Audio)

    /// <summary>
    /// Invoked directly by Unity Animation Events on the chest opening animation clip.
    /// Emits a spatialized 3D audio request to the central AudioController Object Pool.
    /// </summary>
    public void PlayChestOpenSFX()
    {
        GameEvents.Audio.RequestPlaySFXAtPosition(SFXType.World_ChestOpen, transform.position);
    }

    #endregion

    #region Interaction Coroutine

    /// <summary>
    /// Coroutine sequence handling state locks, animation sync, 
    /// inventory transaction verification, and transactional rollbacks.
    /// </summary>
    private IEnumerator OpenChestRoutine()
    {
        if (itemData == null)
        {
            Debug.LogError($"[Chest] No ItemSO assigned to chest '{gameObject.name}'!", this);
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

        // Trigger chest opening animation (Invokes PlayChestOpenSFX via Animation Event)
        if (chestAnimator != null)
        {
            chestAnimator.SetTrigger(OpenChestHash);
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
            Debug.LogError($"[Chest] Transaction failed for item '{itemData.ItemSlug}'. No inventory handler responded to RequestAddItem.", this);

            // Rollback interaction state
            gameObject.layer = originalLayer;
            chestOpened = false;

            if (chestAnimator != null)
            {
                chestAnimator.SetTrigger(CloseChestHash);
            }

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort remaining pickup flow
        }

        // FALLBACK CASE B: Backpacks/Inventory full (Overflow scenario)
        if (remaining > 0)
        {
            string fullMsg = inventoryFullMessage != null ? inventoryFullMessage.GetLocalizedString() : "Inventory Full";
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, fullMsg);

            // Restore interactable layer state and re-allow interaction
            gameObject.layer = originalLayer;
            chestOpened = false;

            // Re-close chest animation
            if (chestAnimator != null)
            {
                chestAnimator.SetTrigger(CloseChestHash);
                yield return new WaitForSeconds(1.0f);
            }

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
            yield break; // Abort remaining pickup flow
        }

        // 3. Display item obtained notification using localized item name
        string localizedItemName = itemData.ItemNameLocalizedString != null
            ? itemData.ItemNameLocalizedString.GetLocalizedString()
            : itemData.ItemName;

        string notificationMessage = itemQuantity > 1
            ? $"{localizedItemName} x{itemQuantity}"
            : $"{localizedItemName}";

        GameEvents.UI.TriggerShowNotification(NotificationType.ItemObtained, notificationMessage, itemData.Icon);

        yield return new WaitForSeconds(endSequenceDelay);

        // 4. Restore game state back to free exploration (Chest remains opened in 'Default' layer permanently)
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
    }

    #endregion
}