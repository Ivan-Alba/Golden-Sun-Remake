using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Central manager for the player's 2D HUD overlays using UI Toolkit.
/// Controls layer visibility, party status cards, dialogues, stamina, minimap integration, and notification streams.
/// Listens exclusively to GameEvents for decoupled messaging.
/// </summary>
public class HUDController : MonoBehaviour
{
    #region Serialized Fields & UI Toolkit Document

    [Header("UI Document Reference")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Stamina Colors")]
    [SerializeField] private Color normalStaminaColor = new Color(0.18f, 0.8f, 0.44f); // #2ECC71
    [SerializeField] private Color lowStaminaColor = new Color(0.9f, 0.3f, 0.23f);     // Red

    [Header("Notification Template")]
    [SerializeField] private VisualTreeAsset notificationCardTemplate;

    [Header("Minimap Setup")]
    [Tooltip("Assign the Custom RenderTexture processed by CRT_MinimapProcessed shader here.")]
    [SerializeField] private CustomRenderTexture minimapCustomRenderTexture;

    #endregion

    #region UI Toolkit VisualElement References

    private VisualElement _root;

    // Layers
    private VisualElement _explorationLayer;
    private VisualElement _dialogueLayer;
    private VisualElement _notificationsLayer;

    // Independent Zone Banner Elements
    private VisualElement _zoneTitleBannerContainer;
    private Label _labelBannerZoneName;
    private Label _labelBannerRegionName;
    private Coroutine _zoneBannerCoroutine;

    // Sub-Containers (Exploration Layer)
    private VisualElement _topLeftContainer;
    private VisualElement _topRightContainer;
    private VisualElement _bottomLeftPartyContainer;
    private VisualElement _dpadWheel;
    private VisualElement _notificationsStreamContainer;

    // Presenters
    private readonly List<HeroStatusCardPresenter> _partyCardPresenters = new List<HeroStatusCardPresenter>();
    private NotificationPresenter _notificationPresenter;

    // Stamina Bar
    private VisualElement _staminaContainer;
    private VisualElement _staminaFill;
    private VisualElement _staminaBarBackground;

    // Aiming Reticle
    private VisualElement _aimingReticle;

    // World Interaction Prompts Container
    private VisualElement _worldInteractionPromptsContainer;

    // Shortcuts Elements
    private VisualElement _shortcutUpIcon;
    private VisualElement _shortcutLeftIcon;
    private VisualElement _shortcutRightIcon;
    private VisualElement _shortcutDownIcon;

    // Dialogue Elements
    private Label _labelSpeakerName;
    private Label _labelDialogueText;
    private VisualElement _dialoguePortraitFrame;
    private VisualElement _dialoguePortraitImg;

    // TODO: Add VisualElement references for Choice Boxes (Binary Yes/No & Standard Vertical List) once added to UXML

    #endregion

    #region Unity Lifecycle & Initialization

    private void Awake()
    {
        InitializeUIElements();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SubscribeEvents();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeEvents();
    }

    private void Start()
    {
        SyncInitialUIState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeUIElements();
        SyncInitialUIState();
    }

    /// <summary>
    /// Synchronizes initial HUD visibility and party state without duplicating subscriptions.
    /// </summary>
    private void SyncInitialUIState()
    {
        if (GameManager.Instance != null)
        {
            HandleGameStateChanged(GameManager.GameState.None, GameManager.Instance.CurrentState);
        }

        RefreshPartyInfo();
        RefreshShortcuts();
    }

    /// <summary>
    /// Queries all necessary VisualElements from the root UIDocument and binds initial properties.
    /// </summary>
    private void InitializeUIElements()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError($"[{nameof(HUDController)}] CRITICAL: UIDocument reference is missing on {gameObject.name}!");
            return;
        }

        _root = uiDocument.rootVisualElement;

        if (_root == null)
        {
            return;
        }

        // Prevent HUD root from capturing mouse inputs when elements are hidden
        _root.pickingMode = PickingMode.Ignore;

        // Query Independent Zone Banner Layer
        _zoneTitleBannerContainer = _root.Q<VisualElement>("zone_title_banner_container");
        _labelBannerZoneName = _root.Q<Label>("label_banner_zone_name");
        _labelBannerRegionName = _root.Q<Label>("label_banner_region_name");

        if (_zoneTitleBannerContainer != null)
        {
            _zoneTitleBannerContainer.pickingMode = PickingMode.Ignore;
            _zoneTitleBannerContainer.RemoveFromClassList("banner-visible");
            _zoneTitleBannerContainer.style.display = DisplayStyle.None;
        }

        // Query Layers
        _explorationLayer = _root.Q<VisualElement>("hud_exploration_layer");
        _dialogueLayer = _root.Q<VisualElement>("hud_dialogue_layer");
        _notificationsLayer = _root.Q<VisualElement>("hud_notifications_layer");

        if (_explorationLayer != null) _explorationLayer.pickingMode = PickingMode.Ignore;
        if (_dialogueLayer != null) _dialogueLayer.pickingMode = PickingMode.Ignore;
        if (_notificationsLayer != null) _notificationsLayer.pickingMode = PickingMode.Ignore;

        // Query Sub-Containers
        _topLeftContainer = _root.Q<VisualElement>("top_left_container");
        _topRightContainer = _root.Q<VisualElement>("top_right_container");
        _bottomLeftPartyContainer = _root.Q<VisualElement>("bottom_left_party_container");
        _dpadWheel = _root.Q<VisualElement>("dpad_hud_wheel");

        // Query Notifications Stream Container & Initialize Presenter
        _notificationsStreamContainer = _root.Q<VisualElement>("notifications_stream_container");
        if (_notificationsStreamContainer != null)
        {
            _notificationsStreamContainer.Clear();
            _notificationPresenter = new NotificationPresenter(_notificationsStreamContainer, notificationCardTemplate, this);
        }

        // Minimap Setup with Custom Render Texture
        SetupMinimap();

        // Query Stamina Bar
        _staminaContainer = _root.Q<VisualElement>("bottom_center_stamina_container");
        _staminaFill = _root.Q<VisualElement>("stamina_bar_fill");
        _staminaBarBackground = _root.Q<VisualElement>(className: "stamina-bar-background");

        // Query Reticle & Interaction Prompts Container
        _aimingReticle = _root.Q<VisualElement>("aiming_reticle");
        _worldInteractionPromptsContainer = _root.Q<VisualElement>("world_interaction_prompts_container");

        // Query D-Pad Shortcut Icons
        _shortcutUpIcon = _root.Q<VisualElement>("hud_shortcut_up_icon");
        _shortcutLeftIcon = _root.Q<VisualElement>("hud_shortcut_left_icon");
        _shortcutRightIcon = _root.Q<VisualElement>("hud_shortcut_right_icon");
        _shortcutDownIcon = _root.Q<VisualElement>("hud_shortcut_down_icon");

        // Query Dialogue Elements
        _labelSpeakerName = _root.Q<Label>("label_speaker_name");
        _labelDialogueText = _root.Q<Label>("label_dialogue_text");
        _dialoguePortraitFrame = _root.Q<VisualElement>("dialogue_portrait_frame");
        _dialoguePortraitImg = _root.Q<VisualElement>("dialogue_portrait_img");

        // TODO: Query choice container elements from UXML here

        // Query and Bind Party Cards
        InitializePartyCardControllers();
    }

    private void SetupMinimap()
    {
        VisualElement minimapViewport = _root.Q<VisualElement>("minimap_viewport");
        if (minimapViewport == null || minimapCustomRenderTexture == null) return;

        if (!minimapCustomRenderTexture.IsCreated())
        {
            minimapCustomRenderTexture.Create();
        }

        minimapViewport.style.backgroundImage = Background.FromRenderTexture(minimapCustomRenderTexture);
    }

    private void InitializePartyCardControllers()
    {
        _partyCardPresenters.Clear();

        if (_bottomLeftPartyContainer == null) return;

        VisualElement card0 = _bottomLeftPartyContainer.Q<VisualElement>("hero_status_card_0");
        VisualElement card1 = _bottomLeftPartyContainer.Q<VisualElement>("hero_status_card_1");

        if (card0 != null) _partyCardPresenters.Add(new HeroStatusCardPresenter(card0));
        if (card1 != null) _partyCardPresenters.Add(new HeroStatusCardPresenter(card1));
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeEvents()
    {
        // GameEvents.GameState
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;

        // GameEvents.UI
        GameEvents.UI.OnShowZoneBanner -= HandleShowZoneBanner;
        GameEvents.UI.OnShowZoneBanner += HandleShowZoneBanner;

        GameEvents.UI.OnShowNotification -= HandleShowNotification;
        GameEvents.UI.OnShowNotification += HandleShowNotification;

        GameEvents.UI.OnSetAimUIVisibleRequested -= SetAimUIVisible;
        GameEvents.UI.OnSetAimUIVisibleRequested += SetAimUIVisible;

        GameEvents.UI.OnSetAimTargetLockedRequested -= SetAimTargetLocked;
        GameEvents.UI.OnSetAimTargetLockedRequested += SetAimTargetLocked;

        GameEvents.UI.OnUpdateDialogueRequested -= UpdateDialogueUI;
        GameEvents.UI.OnUpdateDialogueRequested += UpdateDialogueUI;

        // GameEvents.Player
        GameEvents.Player.OnStaminaChanged -= UpdateStamina;
        GameEvents.Player.OnStaminaChanged += UpdateStamina;

        GameEvents.Player.OnHeroStatusChanged -= RefreshPartyInfo;
        GameEvents.Player.OnHeroStatusChanged += RefreshPartyInfo;

        GameEvents.Player.OnActiveHeroChanged -= HandleActiveHeroChanged;
        GameEvents.Player.OnActiveHeroChanged += HandleActiveHeroChanged;

        // GameEvents.Fighter
        GameEvents.Fighter.OnHpChanged -= HandleFighterResourceChanged;
        GameEvents.Fighter.OnHpChanged += HandleFighterResourceChanged;

        GameEvents.Fighter.OnPpChanged -= HandleFighterResourceChanged;
        GameEvents.Fighter.OnPpChanged += HandleFighterResourceChanged;

        GameEvents.Fighter.OnFighterDied -= HandleFighterDied;
        GameEvents.Fighter.OnFighterDied += HandleFighterDied;

        // GameEvents.Psynergy
        GameEvents.Psynergy.OnShortcutsChanged -= HandleShortcutsChanged;
        GameEvents.Psynergy.OnShortcutsChanged += HandleShortcutsChanged;
    }

    private void UnsubscribeEvents()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;

        GameEvents.UI.OnShowZoneBanner -= HandleShowZoneBanner;
        GameEvents.UI.OnShowNotification -= HandleShowNotification;
        GameEvents.UI.OnSetAimUIVisibleRequested -= SetAimUIVisible;
        GameEvents.UI.OnSetAimTargetLockedRequested -= SetAimTargetLocked;
        GameEvents.UI.OnUpdateDialogueRequested -= UpdateDialogueUI;

        GameEvents.Player.OnStaminaChanged -= UpdateStamina;
        GameEvents.Player.OnHeroStatusChanged -= RefreshPartyInfo;
        GameEvents.Player.OnActiveHeroChanged -= HandleActiveHeroChanged;

        GameEvents.Fighter.OnHpChanged -= HandleFighterResourceChanged;
        GameEvents.Fighter.OnPpChanged -= HandleFighterResourceChanged;
        GameEvents.Fighter.OnFighterDied -= HandleFighterDied;

        GameEvents.Psynergy.OnShortcutsChanged -= HandleShortcutsChanged;
    }

    #endregion

    #region State & Hero Event Handlers

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
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

            case GameManager.GameState.TitleScreen:
            case GameManager.GameState.InMenu:
            case GameManager.GameState.InBattle:
            case GameManager.GameState.Cutscene:
            case GameManager.GameState.Loading:
                SetNotificationsUIVisible(false);
                SetExplorationUIVisible(false);
                SetDialogueUIVisible(false);
                SetAimUIVisible(false);
                HideZoneBannerImmediate();
                break;

            case GameManager.GameState.Interacting:
                SetNotificationsUIVisible(true);
                SetExplorationUIVisible(false);
                SetDialogueUIVisible(false);
                SetAimUIVisible(false);
                break;
        }
    }

    private void HandleActiveHeroChanged(GameObject activeHero)
    {
        RefreshPartyInfo();
        RefreshShortcuts();
    }

    private void HandleShowNotification(NotificationType type, string message, Sprite icon)
    {
        _notificationPresenter?.ShowNotification(type, message, icon);
    }

    #region Fighter Event Handlers

    private void HandleFighterResourceChanged(GameObject fighterObj, int current, int max)
    {
        Debug.Log("HandleFighterResourceChanged called for " + fighterObj.name + " with current: " + current + ", max: " + max);
        if (GameManager.Instance == null || fighterObj == null) return;

        List<GameObject> party = GameManager.Instance.GetParty();
        if (party != null && party.Contains(fighterObj))
        {
            RefreshPartyInfo();
        }
    }

    private void HandleFighterDied(GameObject fighterObj)
    {
        if (GameManager.Instance == null || fighterObj == null) return;

        List<GameObject> party = GameManager.Instance.GetParty();
        if (party != null && party.Contains(fighterObj))
        {
            RefreshPartyInfo();
        }
    }

    #endregion

    #endregion

    #region Zone Banner Handling

    private void HandleShowZoneBanner(string zoneName, string regionName, float duration)
    {
        if (_zoneBannerCoroutine != null)
        {
            StopCoroutine(_zoneBannerCoroutine);
        }

        _zoneBannerCoroutine = StartCoroutine(Routine_ShowZoneBanner(zoneName, regionName, duration));
    }

    private IEnumerator Routine_ShowZoneBanner(string zoneName, string regionName, float duration)
    {
        if (_zoneTitleBannerContainer == null) yield break;

        if (_labelBannerZoneName != null) _labelBannerZoneName.text = zoneName;
        if (_labelBannerRegionName != null) _labelBannerRegionName.text = regionName;

        _zoneTitleBannerContainer.style.display = DisplayStyle.Flex;

        yield return null;

        _zoneTitleBannerContainer.AddToClassList("banner-visible");

        yield return new WaitForSeconds(duration);

        _zoneTitleBannerContainer.RemoveFromClassList("banner-visible");

        yield return new WaitForSeconds(0.5f);

        _zoneTitleBannerContainer.style.display = DisplayStyle.None;
        _zoneBannerCoroutine = null;
    }

    private void HideZoneBannerImmediate()
    {
        if (_zoneBannerCoroutine != null)
        {
            StopCoroutine(_zoneBannerCoroutine);
            _zoneBannerCoroutine = null;
        }

        if (_zoneTitleBannerContainer != null)
        {
            _zoneTitleBannerContainer.RemoveFromClassList("banner-visible");
            _zoneTitleBannerContainer.style.display = DisplayStyle.None;
        }
    }

    #endregion

    #region Layer Visibility Controls

    private void SetExplorationUIVisible(bool visible)
    {
        SetElementDisplay(_explorationLayer, visible);
    }

    private void SetDialogueUIVisible(bool visible)
    {
        SetElementDisplay(_dialogueLayer, visible);
    }

    private void SetNotificationsUIVisible(bool visible)
    {
        SetElementDisplay(_notificationsLayer, visible);
    }

    private void SetPlayerUIVisible(bool visible)
    {
        SetElementDisplay(_root, visible);
    }

    private void SetElementDisplay(VisualElement element, bool visible)
    {
        if (element != null)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    #endregion

    #region Dialogue UI Controls

    private void UpdateDialogueUI(string speakerName, string text, Sprite portrait, DialogueChoiceType choiceType, List<DialogueChoiceData> choices)
    {
        if (_labelSpeakerName != null)
        {
            _labelSpeakerName.text = speakerName;

            if (_labelSpeakerName.parent != null)
            {
                _labelSpeakerName.parent.style.display = string.IsNullOrEmpty(speakerName) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        if (_labelDialogueText != null)
        {
            _labelDialogueText.text = text;
        }

        if (_dialoguePortraitFrame != null)
        {
            if (portrait != null && _dialoguePortraitImg != null)
            {
                _dialoguePortraitImg.style.backgroundImage = Background.FromSprite(portrait);
                _dialoguePortraitFrame.style.display = DisplayStyle.Flex;
            }
            else
            {
                _dialoguePortraitFrame.style.display = DisplayStyle.None;
            }
        }

        // TODO: Handle rendering choices UI based on choiceType and choices list
        // e.g., Display Binary Yes/No UI or generate a dynamic vertical button list
        if (choices != null && choices.Count > 0)
        {
            // Pending UXML elements creation for options container
        }
    }

    #endregion

    #region Stamina Methods

    private void UpdateStamina(float currentValue, float maxValue)
    {
        if (_staminaFill == null) return;

        float percentage = Mathf.Clamp01(currentValue / maxValue) * 100f;
        _staminaFill.style.width = Length.Percent(percentage);

        _staminaFill.style.backgroundColor = (currentValue < 30f) ? lowStaminaColor : normalStaminaColor;

        bool isFull = currentValue >= maxValue;
        SetStaminaBarVisible(!isFull);
    }

    private void SetStaminaBarVisible(bool visible)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration) return;

        if (_staminaBarBackground != null)
        {
            _staminaBarBackground.style.opacity = visible ? 1f : 0f;
        }
    }

    #endregion

    #region Party Info Methods

    private void RefreshPartyInfo()
    {
        if (GameManager.Instance == null) return;

        List<GameObject> party = GameManager.Instance.GetParty();
        GameObject activeHero = GameManager.Instance.ActiveHero;

        if (party == null || party.Count == 0) return;

        for (int i = 0; i < _partyCardPresenters.Count; i++)
        {
            if (i < party.Count && party[i] != null)
            {
                HeroStats stats = party[i].GetComponent<HeroStats>();
                bool isActive = (party[i] == activeHero);

                _partyCardPresenters[i].UpdateData(stats, party[i].name, keyNumber: i + 1, isActiveHero: isActive);
            }
            else
            {
                _partyCardPresenters[i].SetVisible(false);
            }
        }
    }

    #endregion

    #region Shortcuts UI Methods

    private void HandleShortcutsChanged(GameObject hero, PsynergySO[] shortcuts)
    {
        if (GameManager.Instance != null && GameManager.Instance.ActiveHero == hero)
        {
            UpdateShortcutsUI(shortcuts);
        }
    }

    private void RefreshShortcuts()
    {
        if (GameManager.Instance != null && GameManager.Instance.ActiveHero != null)
        {
            GameEvents.Psynergy.RequestGetShortcuts(
                GameManager.Instance.ActiveHero,
                shortcuts => UpdateShortcutsUI(shortcuts)
            );
        }
    }

    private void UpdateShortcutsUI(PsynergySO[] shortcuts)
    {
        VisualElement[] shortcutIcons = new VisualElement[]
        {
            _shortcutUpIcon,
            _shortcutLeftIcon,
            _shortcutRightIcon,
            _shortcutDownIcon
        };

        for (int i = 0; i < shortcutIcons.Length; i++)
        {
            if (shortcutIcons[i] == null) continue;

            PsynergySO currentPsynergy = (shortcuts != null && i < shortcuts.Length) ? shortcuts[i] : null;

            if (currentPsynergy != null && currentPsynergy.Icon != null)
            {
                shortcutIcons[i].style.backgroundImage = Background.FromSprite(currentPsynergy.Icon);
                if (shortcutIcons[i].parent != null) shortcutIcons[i].parent.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (shortcutIcons[i].parent != null) shortcutIcons[i].parent.style.display = DisplayStyle.None;
            }
        }
    }

    #endregion

    #region Specific HUD Panel & Aiming Controls

    private void SetMinimapVisible(bool visible)
    {
        SetElementDisplay(_topRightContainer, visible);
    }

    private void SetAimUIVisible(bool visible)
    {
        if (_aimingReticle != null)
        {
            _aimingReticle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_dpadWheel != null)
        {
            if (visible)
            {
                _dpadWheel.RemoveFromClassList("passive-dpad");
                _dpadWheel.AddToClassList("aiming-active");
            }
            else
            {
                _dpadWheel.RemoveFromClassList("aiming-active");
                _dpadWheel.AddToClassList("passive-dpad");
            }
        }

        if (!visible)
        {
            SetAimTargetLocked(false);
        }
    }

    private void SetAimTargetLocked(bool locked)
    {
        if (_aimingReticle != null)
        {
            StyleColor targetColor = locked
                ? new StyleColor(Color.red)
                : new StyleColor(new Color(1f, 0.84f, 0f));

            _aimingReticle.style.borderLeftColor = targetColor;
            _aimingReticle.style.borderRightColor = targetColor;
            _aimingReticle.style.borderTopColor = targetColor;
            _aimingReticle.style.borderBottomColor = targetColor;
        }
    }

    #endregion
}