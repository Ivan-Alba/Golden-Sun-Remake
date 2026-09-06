using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridge component between Unity Input System (PlayerInputActions) and the game's decoupled event bus (GameEvents.Input).
/// Listens to game state shifts to dynamically enable or disable input action maps without polling in Update.
/// </summary>
public class InputController : MonoBehaviour
{
    #region Private Fields

    private PlayerInputActions inputActions;
    private bool isFirstPersonViewActive;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        LoadControlBindings();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        SubscribeToInputEvents();

        // Listen for global game state changes to switch action maps without per-frame polling
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;

        UnsubscribeFromInputEvents();
        inputActions.Disable();
    }

    #endregion

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

        inputActions.WorldExploration.Aim.performed += OnFirstPersonViewTogglePerformed;

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

    #region Input Event Callbacks

    private void OnMovePerformed(InputAction.CallbackContext ctx)
        => GameEvents.Input.TriggerMoveInputChanged(ctx.ReadValue<Vector2>());

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
        => GameEvents.Input.TriggerMoveInputChanged(Vector2.zero);

    private void OnRunStarted(InputAction.CallbackContext ctx)
        => GameEvents.Input.TriggerRunStateChanged(true);

    private void OnRunCanceled(InputAction.CallbackContext ctx)
        => GameEvents.Input.TriggerRunStateChanged(false);

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!isFirstPersonViewActive)
        {
            GameEvents.Input.TriggerInteractPressed();
        }
    }

    private void OnToggleMenuPerformed(InputAction.CallbackContext ctx)
        => GameEvents.Input.TriggerToggleMenuPressed();

    private void OnHero1Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerChangeHeroPressed(0);
    private void OnHero2Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerChangeHeroPressed(1);
    private void OnHero3Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerChangeHeroPressed(2);
    private void OnHero4Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerChangeHeroPressed(3);

    private void OnFirstPersonViewTogglePerformed(InputAction.CallbackContext ctx)
    {
        isFirstPersonViewActive = !isFirstPersonViewActive;
        GameEvents.Input.TriggerToggleFirstPersonViewPressed();
    }

    private void OnPsynergy1Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerUsePsynergyPressed(0);
    private void OnPsynergy2Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerUsePsynergyPressed(1);
    private void OnPsynergy3Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerUsePsynergyPressed(2);
    private void OnPsynergy4Performed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerUsePsynergyPressed(3);

    private void OnDialogueAdvancePerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerDialogueAdvancePressed();

    // --- BATTLE CALLBACKS ---
    private void OnBattleConfirmPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerBattleConfirmPressed();
    private void OnBattleCancelPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerBattleCancelPressed();
    private void OnBattleUpPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerBattleUpPressed();
    private void OnBattleDownPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerBattleDownPressed();
    private void OnBattleLeftPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerBattleLeftPressed();
    private void OnBattleRightPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerBattleRightPressed();

    // --- MENU CALLBACKS ---
    private void OnMenuNextTabPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerMenuNextTabPressed();
    private void OnMenuPreviousTabPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerMenuPreviousTabPressed();
    private void OnMenuNextHeroPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerMenuNextHeroPressed();
    private void OnMenuPreviousHeroPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerMenuPreviousHeroPressed();
    private void OnMenuConfirmPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerMenuConfirmPressed();
    private void OnMenuCancelPerformed(InputAction.CallbackContext ctx) => GameEvents.Input.TriggerMenuCancelPressed();

    #endregion

    #region State Event Handlers & Map Switching

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.WorldExploration:
                EnableWorldExplorationMap();
                break;

            case GameManager.GameState.InDialogue:
                EnableDialogueMap();
                break;

            case GameManager.GameState.InMenu:
                EnableMenuMap();
                break;

            case GameManager.GameState.InBattle:
                EnableBattleMap();
                break;

            case GameManager.GameState.Cutscene:
            case GameManager.GameState.Interacting:
            case GameManager.GameState.Loading:
            case GameManager.GameState.TitleScreen:
                DisableAllMaps();
                break;
        }
    }

    private void EnableWorldExplorationMap()
    {
        DisableAllMaps();
        if (!inputActions.WorldExploration.enabled) inputActions.WorldExploration.Enable();

        // TODO: Enable WorldSailing map when active vehicle is a boat/ship
    }

    private void EnableDialogueMap()
    {
        ResetContinuousInputs();
        DisableAllMaps();
        if (!inputActions.Dialogue.enabled) inputActions.Dialogue.Enable();
    }

    private void EnableMenuMap()
    {
        ResetContinuousInputs();
        DisableAllMaps();
        if (!inputActions.Menu.enabled) inputActions.Menu.Enable();
    }

    private void EnableBattleMap()
    {
        ResetContinuousInputs();
        DisableAllMaps();
        if (!inputActions.Battle.enabled) inputActions.Battle.Enable();
    }

    private void DisableAllMaps()
    {
        if (inputActions.WorldExploration.enabled) inputActions.WorldExploration.Disable();
        if (inputActions.Dialogue.enabled) inputActions.Dialogue.Disable();
        if (inputActions.Battle.enabled) inputActions.Battle.Disable();
        if (inputActions.Menu.enabled) inputActions.Menu.Disable();

        isFirstPersonViewActive = false;
    }

    private void ResetContinuousInputs()
    {
        GameEvents.Input.TriggerMoveInputChanged(Vector2.zero);
        GameEvents.Input.TriggerRunStateChanged(false);
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