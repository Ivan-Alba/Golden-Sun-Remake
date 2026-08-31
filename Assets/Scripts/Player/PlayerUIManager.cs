using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance { get; private set; }

    #region Serialized Fields & UI Toolkit Document

    [Header("UI Document Reference")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Stamina Colors")]
    [SerializeField] private Color normalStaminaColor = new Color(0.18f, 0.8f, 0.44f); // #2ECC71
    [SerializeField] private Color lowStaminaColor = new Color(0.9f, 0.3f, 0.23f);     // Red

    [Header("Notification Template")]
    [SerializeField] private VisualTreeAsset notificationCardTemplate;

    [Header("Minimap Setup")]
    [Tooltip("Asigna aquí la Custom RenderTexture procesada por el Shader (CRT_MinimapProcessed)")]
    [SerializeField] private CustomRenderTexture minimapCustomRenderTexture;

    public VisualTreeAsset NotificationCardTemplate => notificationCardTemplate;

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

    // Party Member Controllers
    private readonly List<HeroStatusCardController> _partyCardControllers = new List<HeroStatusCardController>();

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

    #endregion

    #region Unity Lifecycle & Initialization

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeUIElements();
    }

    private void InitializeUIElements()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError($"[{nameof(PlayerUIManager)}] CRITICAL: UIDocument reference is missing on {gameObject.name}!");
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

        // Query Notifications Stream Container
        _notificationsStreamContainer = _root.Q<VisualElement>("notifications_stream_container");
        if (_notificationsStreamContainer != null)
        {
            _notificationsStreamContainer.Clear();
        }

        // Minimap Setup with CRT
        SetupMinimap();

        // Query Stamina Bar
        _staminaContainer = _root.Q<VisualElement>("bottom_center_stamina_container");
        _staminaFill = _root.Q<VisualElement>("stamina_bar_fill");
        _staminaBarBackground = _root.Q<VisualElement>(className: "stamina-bar-background");

        // Query Reticle & Interaction Prompts
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

        // Query and Bind Party Cards
        InitializePartyCardControllers();
    }

    private void SetupMinimap()
    {
        VisualElement minimapViewport = _root.Q<VisualElement>("minimap_viewport");
        if (minimapViewport == null || minimapCustomRenderTexture == null) return;

        // Aseguramos que la Custom Render Texture se esté actualizando dinámicamente
        if (!minimapCustomRenderTexture.IsCreated())
        {
            minimapCustomRenderTexture.Create();
        }

        // Asignamos directamente la CRT (que ya contiene el Shader procesado) al background de UI Toolkit
        minimapViewport.style.backgroundImage = Background.FromRenderTexture(minimapCustomRenderTexture);
    }

    private void InitializePartyCardControllers()
    {
        _partyCardControllers.Clear();

        if (_bottomLeftPartyContainer == null) return;

        VisualElement card0 = _bottomLeftPartyContainer.Q<VisualElement>("hero_status_card_0");
        VisualElement card1 = _bottomLeftPartyContainer.Q<VisualElement>("hero_status_card_1");

        if (card0 != null) _partyCardControllers.Add(new HeroStatusCardController(card0));
        if (card1 != null) _partyCardControllers.Add(new HeroStatusCardController(card1));
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
        SubscribeEvents();

        if (GameManager.Instance != null)
        {
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }

        RefreshPartyInfo();
        RefreshShortcuts();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeUIElements();

        if (GameManager.Instance != null)
        {
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }

        RefreshPartyInfo();
        RefreshShortcuts();
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

            GameManager.Instance.OnPartyChanged -= RefreshPartyInfo;
            GameManager.Instance.OnPartyChanged += RefreshPartyInfo;
        }

        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.OnShortcutsChanged -= HandleShortcutsChanged;
            PsynergyWorldController.Instance.OnShortcutsChanged += HandleShortcutsChanged;
        }

        GameEvents.UI.OnShowZoneBanner -= HandleShowZoneBanner;
        GameEvents.UI.OnShowZoneBanner += HandleShowZoneBanner;
    }

    private void UnsubscribeEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnPartyChanged -= RefreshPartyInfo;
        }

        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.OnShortcutsChanged -= HandleShortcutsChanged;
        }

        GameEvents.UI.OnShowZoneBanner -= HandleShowZoneBanner;
    }

    #endregion

    #region State Event Handler

    private void HandleGameStateChanged(GameManager.GameState newState)
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

    public void SetExplorationUIVisible(bool visible)
    {
        SetElementDisplay(_explorationLayer, visible);
    }

    public void SetDialogueUIVisible(bool visible)
    {
        SetElementDisplay(_dialogueLayer, visible);
    }

    public void SetNotificationsUIVisible(bool visible)
    {
        SetElementDisplay(_notificationsLayer, visible);
    }

    public void SetPlayerUIVisible(bool visible)
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

    public void UpdateDialogueUI(string speakerName, string text, Sprite portrait = null)
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
    }

    #endregion

    #region Stamina Methods

    public void UpdateStamina(float currentValue, float maxValue)
    {
        if (_staminaFill == null) return;

        float percentage = Mathf.Clamp01(currentValue / maxValue) * 100f;
        _staminaFill.style.width = Length.Percent(percentage);

        _staminaFill.style.backgroundColor = (currentValue < 30f) ? lowStaminaColor : normalStaminaColor;

        bool isFull = currentValue >= maxValue;
        SetStaminaBarVisible(!isFull);
    }

    public void SetStaminaBarVisible(bool visible)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration) return;

        if (_staminaBarBackground != null)
        {
            _staminaBarBackground.style.opacity = visible ? 1f : 0f;
        }
    }

    #endregion

    #region Interaction Icon Methods (TODO)

    public void SetInteractionIconVisible(bool visible)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration) return;

        // TODO: Implement World-to-Screen interaction prompt display logic using _worldInteractionPromptsContainer
    }

    #endregion

    #region Party Info Methods

    public void RefreshPartyInfo()
    {
        if (GameManager.Instance == null) return;

        List<GameObject> party = GameManager.Instance.GetParty();
        GameObject activeHero = GameManager.Instance.ActiveHero;

        if (party == null || party.Count == 0) return;

        for (int i = 0; i < _partyCardControllers.Count; i++)
        {
            if (i < party.Count && party[i] != null)
            {
                HeroStats stats = party[i].GetComponent<HeroStats>();
                bool isActive = (party[i] == activeHero);

                _partyCardControllers[i].UpdateData(stats, party[i].name, keyNumber: i + 1, isActiveHero: isActive);
            }
            else
            {
                _partyCardControllers[i].SetVisible(false);
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

    public void RefreshShortcuts()
    {
        if (GameManager.Instance != null && GameManager.Instance.ActiveHero != null && PsynergyWorldController.Instance != null)
        {
            PsynergySO[] shortcuts = PsynergyWorldController.Instance.GetShortcuts(GameManager.Instance.ActiveHero);
            UpdateShortcutsUI(shortcuts);
        }
    }

    public void UpdateShortcutsUI(PsynergySO[] shortcuts)
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

    public void SetMinimapVisible(bool visible)
    {
        SetElementDisplay(_topRightContainer, visible);
    }

    public void SetAimUIVisible(bool visible)
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

    public void SetAimTargetLocked(bool locked)
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

    public VisualElement GetNotificationsContainer()
    {
        return _notificationsStreamContainer;
    }
}