using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private PlayerInputActions inputActions;

    public Vector2 MoveInput { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFirstPersonView { get; private set; }

    // --- EVENTS FOR DISCRETE ACTIONS ---
    public event Action<int> OnChangeHeroPressed;
    public event Action OnInteractPressed;
    public event Action OnToggleMenuPressed;
    public event Action OnStartFirstPersonViewPressed;
    public event Action OnStopFirstPersonViewPressed;
    public event Action<int> OnUsePsynergyPressed;
    public event Action OnDialogueAdvancePressed;

    // --- BATTLE EVENTS ---
    public event Action OnBattleConfirmPressed;
    public event Action OnBattleCancelPressed;
    public event Action OnBattleUpPressed;
    public event Action OnBattleDownPressed;
    public event Action OnBattleLeftPressed;
    public event Action OnBattleRightPressed;

    // --- MENU NAVIGATION EVENTS ---
    public event Action OnMenuNextTabPressed;
    public event Action OnMenuPreviousTabPressed;
    public event Action OnMenuNextHeroPressed;
    public event Action OnMenuPreviousHeroPressed;
    public event Action OnMenuConfirmPressed;
    public event Action OnMenuCancelPressed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        inputActions = new PlayerInputActions();
        LoadControlBindings();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        SubscribeToInputEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();
        inputActions.Disable();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.LoadingData)
        {
            MoveInput = Vector2.zero;
            IsRunning = false;
            return;
        }

        switch (GameManager.Instance.CurrentState)
        {
            case GameManager.GameState.WorldExploration:
                EnsureWorldExplorationActive();
                break;

            case GameManager.GameState.InDialogue:
                ProcessDialogueState();
                break;

            case GameManager.GameState.InMenu:
                ProcessMenuState();
                break;

            case GameManager.GameState.InBattle:
                ProcessCombatState();
                break;

            case GameManager.GameState.Cutscene:
            case GameManager.GameState.Interacting:
                ProcessCutsceneState();
                break;
        }
    }

    #region Input Event Subscriptions

    private void SubscribeToInputEvents()
    {
        UnsubscribeFromInputEvents();

        // --- WORLD EXPLORATION MAP ---
        inputActions.WorldExploration.Move.performed += OnMovePerformed;
        inputActions.WorldExploration.Move.canceled += OnMoveCanceled;

        inputActions.WorldExploration.Run.started += OnRunStarted;
        inputActions.WorldExploration.Run.canceled += OnRunCanceled;

        inputActions.WorldExploration.Interact.performed += OnInteractPerformed;
        inputActions.WorldExploration.ToggleMenu.performed += OnToggleMenuPerformed;

        inputActions.WorldExploration.Hero1.performed += OnHero1Performed;
        inputActions.WorldExploration.Hero2.performed += OnHero2Performed;
        inputActions.WorldExploration.Hero3.performed += OnHero3Performed;
        inputActions.WorldExploration.Hero4.performed += OnHero4Performed;

        // Toggle Right-Click for First Person View
        inputActions.WorldExploration.Aim.performed += OnFirstPersonViewTogglePerformed;

        // Direct Psynergy Shortcuts inside WorldExploration Map
        inputActions.WorldExploration.Psynergy1.performed += OnPsynergy1Performed;
        inputActions.WorldExploration.Psynergy2.performed += OnPsynergy2Performed;
        inputActions.WorldExploration.Psynergy3.performed += OnPsynergy3Performed;
        inputActions.WorldExploration.Psynergy4.performed += OnPsynergy4Performed;

        // --- DIALOGUE MAP ---
        inputActions.Dialogue.Confirm.performed += OnDialogueAdvancePerformed;

        // --- BATTLE MAP ---
        inputActions.Battle.Confirm.performed += OnBattleConfirmPerformed;
        inputActions.Battle.Cancel.performed += OnBattleCancelPerformed;
        inputActions.Battle.Up.performed += OnBattleUpPerformed;
        inputActions.Battle.Down.performed += OnBattleDownPerformed;
        inputActions.Battle.Left.performed += OnBattleLeftPerformed;
        inputActions.Battle.Right.performed += OnBattleRightPerformed;

        // --- MENU MAP ---
        inputActions.Menu.ToggleMenu.performed += OnToggleMenuPerformed;
        inputActions.Menu.NextTab.performed += OnMenuNextTabPerformed;
        inputActions.Menu.PreviousTab.performed += OnMenuPreviousTabPerformed;
        inputActions.Menu.NextHero.performed += OnMenuNextHeroPerformed;
        inputActions.Menu.PreviousHero.performed += OnMenuPreviousHeroPerformed;
        inputActions.Menu.Confirm.performed += OnMenuConfirmPerformed;
        inputActions.Menu.Cancel.performed += OnMenuCancelPerformed;
    }

    private void UnsubscribeFromInputEvents()
    {
        if (inputActions == null) return;

        inputActions.WorldExploration.Move.performed -= OnMovePerformed;
        inputActions.WorldExploration.Move.canceled -= OnMoveCanceled;

        inputActions.WorldExploration.Run.started -= OnRunStarted;
        inputActions.WorldExploration.Run.canceled -= OnRunCanceled;

        inputActions.WorldExploration.Interact.performed -= OnInteractPerformed;
        inputActions.WorldExploration.ToggleMenu.performed -= OnToggleMenuPerformed;

        inputActions.WorldExploration.Hero1.performed -= OnHero1Performed;
        inputActions.WorldExploration.Hero2.performed -= OnHero2Performed;
        inputActions.WorldExploration.Hero3.performed -= OnHero3Performed;
        inputActions.WorldExploration.Hero4.performed -= OnHero4Performed;

        inputActions.WorldExploration.Aim.performed -= OnFirstPersonViewTogglePerformed;

        inputActions.WorldExploration.Psynergy1.performed -= OnPsynergy1Performed;
        inputActions.WorldExploration.Psynergy2.performed -= OnPsynergy2Performed;
        inputActions.WorldExploration.Psynergy3.performed -= OnPsynergy3Performed;
        inputActions.WorldExploration.Psynergy4.performed -= OnPsynergy4Performed;

        inputActions.Dialogue.Confirm.performed -= OnDialogueAdvancePerformed;

        inputActions.Battle.Confirm.performed -= OnBattleConfirmPerformed;
        inputActions.Battle.Cancel.performed -= OnBattleCancelPerformed;
        inputActions.Battle.Up.performed -= OnBattleUpPerformed;
        inputActions.Battle.Down.performed -= OnBattleDownPerformed;
        inputActions.Battle.Left.performed -= OnBattleLeftPerformed;
        inputActions.Battle.Right.performed -= OnBattleRightPerformed;

        inputActions.Menu.ToggleMenu.performed -= OnToggleMenuPerformed;
        inputActions.Menu.NextTab.performed -= OnMenuNextTabPerformed;
        inputActions.Menu.PreviousTab.performed -= OnMenuPreviousTabPerformed;
        inputActions.Menu.NextHero.performed -= OnMenuNextHeroPerformed;
        inputActions.Menu.PreviousHero.performed -= OnMenuPreviousHeroPerformed;
        inputActions.Menu.Confirm.performed -= OnMenuConfirmPerformed;
        inputActions.Menu.Cancel.performed -= OnMenuCancelPerformed;
    }

    #endregion

    #region Event Callbacks

    private void OnMovePerformed(InputAction.CallbackContext ctx) => MoveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => MoveInput = Vector2.zero;

    private void OnRunStarted(InputAction.CallbackContext ctx) => IsRunning = true;
    private void OnRunCanceled(InputAction.CallbackContext ctx) => IsRunning = false;

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsFirstPersonView) OnInteractPressed?.Invoke();
    }

    private void OnToggleMenuPerformed(InputAction.CallbackContext ctx) => OnToggleMenuPressed?.Invoke();

    private void OnHero1Performed(InputAction.CallbackContext ctx) => OnChangeHeroPressed?.Invoke(0);
    private void OnHero2Performed(InputAction.CallbackContext ctx) => OnChangeHeroPressed?.Invoke(1);
    private void OnHero3Performed(InputAction.CallbackContext ctx) => OnChangeHeroPressed?.Invoke(2);
    private void OnHero4Performed(InputAction.CallbackContext ctx) => OnChangeHeroPressed?.Invoke(3);

    private void OnFirstPersonViewTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (IsFirstPersonView)
        {
            StopFirstPersonView();
        }
        else
        {
            StartFirstPersonView();
        }
    }

    private void OnPsynergy1Performed(InputAction.CallbackContext ctx) => OnUsePsynergyPressed?.Invoke(0);
    private void OnPsynergy2Performed(InputAction.CallbackContext ctx) => OnUsePsynergyPressed?.Invoke(1);
    private void OnPsynergy3Performed(InputAction.CallbackContext ctx) => OnUsePsynergyPressed?.Invoke(2);
    private void OnPsynergy4Performed(InputAction.CallbackContext ctx) => OnUsePsynergyPressed?.Invoke(3);

    private void OnDialogueAdvancePerformed(InputAction.CallbackContext ctx) => OnDialogueAdvancePressed?.Invoke();

    // --- BATTLE CALLBACKS ---
    private void OnBattleConfirmPerformed(InputAction.CallbackContext ctx) => OnBattleConfirmPressed?.Invoke();
    private void OnBattleCancelPerformed(InputAction.CallbackContext ctx) => OnBattleCancelPressed?.Invoke();
    private void OnBattleUpPerformed(InputAction.CallbackContext ctx) => OnBattleUpPressed?.Invoke();
    private void OnBattleDownPerformed(InputAction.CallbackContext ctx) => OnBattleDownPressed?.Invoke();
    private void OnBattleLeftPerformed(InputAction.CallbackContext ctx) => OnBattleLeftPressed?.Invoke();
    private void OnBattleRightPerformed(InputAction.CallbackContext ctx) => OnBattleRightPressed?.Invoke();

    // --- MENU CALLBACKS ---
    private void OnMenuNextTabPerformed(InputAction.CallbackContext ctx) => OnMenuNextTabPressed?.Invoke();
    private void OnMenuPreviousTabPerformed(InputAction.CallbackContext ctx) => OnMenuPreviousTabPressed?.Invoke();
    private void OnMenuNextHeroPerformed(InputAction.CallbackContext ctx) => OnMenuNextHeroPressed?.Invoke();
    private void OnMenuPreviousHeroPerformed(InputAction.CallbackContext ctx) => OnMenuPreviousHeroPressed?.Invoke();
    private void OnMenuConfirmPerformed(InputAction.CallbackContext ctx) => OnMenuConfirmPressed?.Invoke();
    private void OnMenuCancelPerformed(InputAction.CallbackContext ctx) => OnMenuCancelPressed?.Invoke();

    #endregion

    #region State & Map Management

    private void EnsureWorldExplorationActive()
    {
        if (!inputActions.WorldExploration.enabled) inputActions.WorldExploration.Enable();
        if (inputActions.Dialogue.enabled) inputActions.Dialogue.Disable();
        if (inputActions.Battle.enabled) inputActions.Battle.Disable();
        if (inputActions.Menu.enabled) inputActions.Menu.Disable();
    }

    private void ProcessDialogueState()
    {
        MoveInput = Vector2.zero;
        IsRunning = false;

        if (IsFirstPersonView) StopFirstPersonView();

        inputActions.WorldExploration.Disable();
        if (inputActions.Battle.enabled) inputActions.Battle.Disable();
        if (inputActions.Menu.enabled) inputActions.Menu.Disable();

        if (!inputActions.Dialogue.enabled) inputActions.Dialogue.Enable();
    }

    private void ProcessMenuState()
    {
        MoveInput = Vector2.zero;
        IsRunning = false;

        if (IsFirstPersonView) StopFirstPersonView();

        inputActions.WorldExploration.Disable();
        inputActions.Dialogue.Disable();
        if (inputActions.Battle.enabled) inputActions.Battle.Disable();

        if (!inputActions.Menu.enabled) inputActions.Menu.Enable();
    }

    private void ProcessCutsceneState()
    {
        MoveInput = Vector2.zero;
        IsRunning = false;

        if (IsFirstPersonView) StopFirstPersonView();

        inputActions.WorldExploration.Disable();
        inputActions.Dialogue.Disable();
        if (inputActions.Battle.enabled) inputActions.Battle.Disable();
        if (inputActions.Menu.enabled) inputActions.Menu.Disable();
    }

    private void ProcessCombatState()
    {
        MoveInput = Vector2.zero;
        IsRunning = false;

        if (IsFirstPersonView) StopFirstPersonView();

        inputActions.WorldExploration.Disable();
        inputActions.Dialogue.Disable();
        if (inputActions.Menu.enabled) inputActions.Menu.Disable();

        if (!inputActions.Battle.enabled) inputActions.Battle.Enable();
    }

    private void StartFirstPersonView()
    {
        if (IsFirstPersonView) return;

        IsFirstPersonView = true;
        OnStartFirstPersonViewPressed?.Invoke();
    }

    private void StopFirstPersonView()
    {
        if (!IsFirstPersonView) return;

        IsFirstPersonView = false;
        OnStopFirstPersonViewPressed?.Invoke();
    }

    #endregion

    #region Binding Persistence

    public void SaveControlBindings()
    {
        string overridesJson = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("InputBindingOverrides", overridesJson);
        PlayerPrefs.Save();
    }

    public void LoadControlBindings()
    {
        if (PlayerPrefs.HasKey("InputBindingOverrides"))
        {
            string overridesJson = PlayerPrefs.GetString("InputBindingOverrides");
            inputActions.LoadBindingOverridesFromJson(overridesJson);
        }
    }

    #endregion
}