using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance { get; private set; }

    #region Serialized Fields

    [Header("Exploration UI Containers")]
    [SerializeField] private GameObject partyInfoPanel;
    [SerializeField] private GameObject minimapPanel;
    [SerializeField] private GameObject shortcutsPanel;
    [SerializeField] private GameObject notificationPanel;

    [Header("Stamina UI Components")]
    [SerializeField] private GameObject staminaBarObject;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private Color normalStaminaColor = new Color(0.94f, 0.71f, 0f);
    [SerializeField] private Color lowStaminaColor = Color.red;

    [Header("Interaction & Aiming Components")]
    [SerializeField] private GameObject interactionIcon;
    [SerializeField] private GameObject aimUI;
    [SerializeField] private Image reticleImage;
    [SerializeField] private Color unlockedReticleColor = Color.white;
    [SerializeField] private Color lockedReticleColor = Color.red;

    [Header("Dialogue Component")]
    [SerializeField] private GameObject dialoguePanel;

    [Header("Party Member Subcomponents")]
    [SerializeField] private List<PartyMemberUI> partySlots;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (GameManager.Instance != null)
        {
            // 1. Subscribe to future state changes
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

            // 2. Immediately sync UI with the current state upon enablement
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }

        // Subscribe to Psynergy shortcut updates
        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.OnShortcutsChanged += HandleShortcutsChanged;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        // Unsubscribe from Psynergy shortcut updates
        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.OnShortcutsChanged -= HandleShortcutsChanged;
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }

        RefreshShortcuts();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshPartyInfo();
        RefreshShortcuts();
    }

    #endregion

    #region State Event Handler

    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        Debug.Log($"{nameof(HandleGameStateChanged)}: Game state changed to: {newState}");
        switch (newState)
        {
            case GameManager.GameState.WorldExploration:
                SetExplorationUIVisible(true);
                SetDialogueUIVisible(false);
                SetAimUIVisible(false);
                SetNotificationsUIVisible(true);
                break;

            case GameManager.GameState.InDialogue:
                SetNotificationsUIVisible(true);
                SetExplorationUIVisible(false);
                SetDialogueUIVisible(true);
                SetAimUIVisible(false);
                break;

            case GameManager.GameState.InMenu:
            case GameManager.GameState.InBattle:
            case GameManager.GameState.Cutscene:
            case GameManager.GameState.Loading:
                SetNotificationsUIVisible(false);
                SetExplorationUIVisible(false);
                SetDialogueUIVisible(false);
                SetAimUIVisible(false);
                break;
            case GameManager.GameState.Interacting:
                SetExplorationUIVisible(false);
                SetDialogueUIVisible(false);
                SetAimUIVisible(false);
                break;
        }
    }

    #endregion

    #region Exploration HUD Visibility Controls

    public void SetExplorationUIVisible(bool visible)
    {
        if (partyInfoPanel != null) partyInfoPanel.SetActive(visible);
        if (minimapPanel != null) minimapPanel.SetActive(visible);
        if (shortcutsPanel != null) shortcutsPanel.SetActive(visible);

        if (!visible && interactionIcon != null)
        {
            interactionIcon.SetActive(false);
        }

        if (!visible && staminaBarObject != null)
        {
            staminaBarObject.SetActive(false);
        }
    }

    public void SetDialogueUIVisible(bool visible)
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(visible);
        }
    }

    public void SetNotificationsUIVisible(bool visible)
    {
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(visible);
        }
    }

    #endregion

    #region Stamina Methods

    public void UpdateStamina(float currentValue, float maxValue)
    {
        if (staminaSlider == null) return;

        staminaSlider.maxValue = maxValue;
        staminaSlider.value = currentValue;

        if (staminaFillImage != null)
        {
            staminaFillImage.color = (currentValue < 30f) ? lowStaminaColor : normalStaminaColor;
        }

        bool isFull = currentValue >= maxValue;
        SetStaminaBarVisible(!isFull);
    }

    public void SetStaminaBarVisible(bool visible)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration) return;

        if (staminaBarObject != null)
        {
            staminaBarObject.SetActive(visible);
        }
    }

    #endregion

    #region Interaction Icon Methods

    public void SetInteractionIconVisible(bool visible)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration) return;

        if (interactionIcon != null)
        {
            interactionIcon.SetActive(visible);
        }
    }

    #endregion

    #region Party Info Methods

    public void RefreshPartyInfo()
    {
        if (GameManager.Instance == null) return;

        List<GameObject> party = GameManager.Instance.GetParty();

        for (int i = 0; i < partySlots.Count; i++)
        {
            if (i < party.Count && party[i] != null)
            {
                HeroStats stats = party[i].GetComponent<HeroStats>();
                partySlots[i].UpdateData(stats, party[i].name);
            }
            else
            {
                partySlots[i].gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Shortcuts UI Methods

    /// <summary>
    /// Event handler for shortcuts changes triggered by PsynergyWorldController.
    /// </summary>
    private void HandleShortcutsChanged(GameObject hero, PsynergySO[] shortcuts)
    {
        if (GameManager.Instance != null && GameManager.Instance.ActiveHero == hero)
        {
            UpdateShortcutsUI(shortcuts);
        }
    }

    /// <summary>
    /// Refreshes the active hero's shortcuts display manually.
    /// </summary>
    public void RefreshShortcuts()
    {
        if (GameManager.Instance != null && GameManager.Instance.ActiveHero != null && PsynergyWorldController.Instance != null)
        {
            PsynergySO[] shortcuts = PsynergyWorldController.Instance.GetShortcuts(GameManager.Instance.ActiveHero);
            UpdateShortcutsUI(shortcuts);
        }
    }

    /// <summary>
    /// Updates the 4 shortcut icons (Icon0..Icon3) inside the shortcutsPanel object.
    /// </summary>
    public void UpdateShortcutsUI(PsynergySO[] shortcuts)
    {
        if (shortcutsPanel == null) return;

        for (int i = 0; i < 4; i++)
        {
            Transform iconTrans = shortcutsPanel.transform.Find($"Icon{i}");
            if (iconTrans == null) continue;

            PsynergySO currentPsynergy = (shortcuts != null && i < shortcuts.Length) ? shortcuts[i] : null;

            if (currentPsynergy != null)
            {
                Image img = iconTrans.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = currentPsynergy.Icon;
                }
                iconTrans.gameObject.SetActive(true);
            }
            else
            {
                iconTrans.gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Specific Panel Methods

    public void SetMinimapVisible(bool visible)
    {
        if (minimapPanel != null)
        {
            minimapPanel.SetActive(visible);
        }
    }

    public void SetAimUIVisible(bool visible)
    {
        if (aimUI != null)
        {
            aimUI.SetActive(visible);
        }

        // Reset target lock state when hiding aim UI
        if (!visible)
        {
            SetAimTargetLocked(false);
        }
    }

    /// <summary>
    /// Changes the reticle appearance when locking onto a valid targetable object.
    /// </summary>
    public void SetAimTargetLocked(bool locked)
    {
        if (reticleImage != null)
        {
            reticleImage.color = locked ? lockedReticleColor : unlockedReticleColor;
        }
    }

    public void SetPlayerUIVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    #endregion
}