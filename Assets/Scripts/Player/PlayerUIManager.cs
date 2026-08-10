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
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void Start()
    {
        // Re-subscribe in case GameManager initialized after OnEnable
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

            // Sync initial state
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshPartyInfo();
    }

    #endregion

    #region State Event Handler

    /// <summary>
    /// Reacts to global state changes and configures UI elements accordingly.
    /// </summary>
    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        Debug.Log($"{nameof(HandleGameStateChanged)}: Game state changed to: {newState}");
        switch (newState)
        {
            case GameManager.GameState.WorldExploration:
                SetExplorationUIVisible(true);
                SetDialogueUIVisible(false);
                SetAimUIVisible(false);
                break;

            case GameManager.GameState.InDialogue:
                SetExplorationUIVisible(false);
                SetDialogueUIVisible(true);
                SetAimUIVisible(false);
                break;

            case GameManager.GameState.InMenu:
            case GameManager.GameState.InBattle:
            case GameManager.GameState.Interacting:
                SetExplorationUIVisible(false);
                SetDialogueUIVisible(false);
                SetAimUIVisible(false);
                break;
        }
    }

    #endregion

    #region Exploration HUD Visibility Controls

    /// <summary>
    /// Controls the visibility of all exploration HUD components (minimap, party, shortcuts, stamina).
    /// </summary>
    public void SetExplorationUIVisible(bool visible)
    {
        if (partyInfoPanel != null) partyInfoPanel.SetActive(visible);
        if (minimapPanel != null) minimapPanel.SetActive(visible);
        if (shortcutsPanel != null) shortcutsPanel.SetActive(visible);
        //if (notificationPanel != null) notificationPanel.SetActive(visible);

        // Hide interaction icon when exploration UI is hidden
        if (!visible && interactionIcon != null)
        {
            interactionIcon.SetActive(false);
        }

        // Hide stamina bar when exploration UI is hidden
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
        // Only show stamina bar if exploration UI is currently active
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
        // Only show interaction icon if exploration UI is currently active
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
                FighterStats stats = party[i].GetComponent<FighterStats>();
                partySlots[i].UpdateData(stats, party[i].name);
            }
            else
            {
                partySlots[i].gameObject.SetActive(false);
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
    }

    public void SetPlayerUIVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    #endregion
}