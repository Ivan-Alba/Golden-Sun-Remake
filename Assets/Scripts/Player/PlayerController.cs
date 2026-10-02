using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Internal state machine for locomotion and interaction states of the player hero.
/// </summary>
public enum PlayerMovementState
{
    FreeExploration,  // Standard walking and running movement
    InFirstPerson,    // Inspection / First-person viewing mode
    AutoJumping,      // Automatic tile-to-tile jumping arc
    PushingObject,    // Pushing grid-aligned puzzle objects
    Climbing,         // Climbing ladders, vines, or ropes
    Sliding,          // Automatic sliding down slopes or sand dunes
    Falling,          // Falling down holes, dungeon pitfalls, or chasm drops
    CastingPsynergy,  // Frozen executing field Psynergy animations (Move, Force, etc.)
    AutoWalking       // Controlled automatically by cutscene or trigger events
}

/// <summary>
/// Central coordinator for player exploration states, input routing, stamina management, 
/// auto-walk commands, and first-person inspection camera mode.
/// Delegates movement physics to PlayerMovementMotor, animation handling to PlayerAnimationController,
/// and edge sensor probing to PlayerLedgeDetector.
/// </summary>
[RequireComponent(typeof(PlayerMovementMotor))]
[RequireComponent(typeof(PlayerAnimationController))]
public class PlayerController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainRate = 10f;
    [SerializeField] private float staminaRegenRate = 7f;

    [Header("Auto-Jump Parameters")]
    [SerializeField] private float autoJumpArcHeight = 0.6f;
    [SerializeField] private float autoJumpDuration = 0.45f;
    [SerializeField] private float autoJumpLandingDelay = 0.15f;

    [Header("Layer Settings for Auto-Jump")]
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private string ledgeLayerName = "Ledge";

    [Header("Sub-Sensor References")]
    [Tooltip("Direct reference to the active PlayerLedgeDetector child component.")]
    [SerializeField] private PlayerLedgeDetector ledgeDetector;

    [Header("References & Cinemachine")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private CinemachineCamera freeLookCamera;
    [SerializeField] private CinemachineCamera firstPersonCamera;
    [SerializeField] private float firstPersonViewRotationSpeed = 1200f;

    [Header("Cinemachine Priority Settings")]
    [SerializeField] private int defaultFirstPersonViewPriority = 0;
    [SerializeField] private int activeFirstPersonViewPriority = 20;

    #endregion

    #region Private Fields

    private PlayerMovementMotor motor;
    private PlayerAnimationController animController;
    private CinemachinePanTilt firstPersonViewPanTilt;

    private Interactable interactionCurrentTarget;
    private IPsynergyTargetable psynergyCurrentTarget;

    private Vector2 currentMoveInput;
    private bool isRunningInput;
    private PlayerMovementState currentMovementState = PlayerMovementState.FreeExploration;
    private GameManager.GameState currentGameState = GameManager.GameState.None;

    private float currentStamina;
    private bool enoughStaminaToRun = true;

    private float idleTimeCounter;
    private readonly float secondsInIdle = 15f;

    private Coroutine autoWalkCoroutine;
    private Coroutine rotateCoroutine;
    private Coroutine psynergyCastCoroutine;

    #endregion

    #region Properties

    public PlayerMovementState CurrentMovementState => currentMovementState;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        motor = GetComponent<PlayerMovementMotor>();
        animController = GetComponent<PlayerAnimationController>();

        currentStamina = maxStamina;
    }

    private void Start()
    {
        GameEvents.Player.TriggerStaminaChanged(currentStamina, maxStamina);
        GameEvents.Hero.TriggerHeroStatusChanged();

        if (firstPersonCamera != null)
        {
            firstPersonCamera.Priority.Value = defaultFirstPersonViewPriority;
            firstPersonViewPanTilt = firstPersonCamera.GetComponent<CinemachinePanTilt>();
        }
    }

    private void OnEnable()
    {
        SubscribeToInputEvents();

        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
        GameEvents.Player.OnRequestTeleport += HandleRequestTeleport;
        GameEvents.Player.OnRequestLookAt += HandleRequestLookAt;
        GameEvents.Player.OnRequestAutoWalk += HandleRequestAutoWalk;
        GameEvents.Player.OnRequestStopAutoWalk += HandleRequestStopAutoWalk;
        GameEvents.World.OnInteractableTargetChanged += HandleInteractableTargetChanged;
        GameEvents.World.OnPsynergyTargetChanged += HandlePsynergyTargetChanged;
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();

        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.Player.OnRequestTeleport -= HandleRequestTeleport;
        GameEvents.Player.OnRequestLookAt -= HandleRequestLookAt;
        GameEvents.Player.OnRequestAutoWalk -= HandleRequestAutoWalk;
        GameEvents.Player.OnRequestStopAutoWalk -= HandleRequestStopAutoWalk;
        GameEvents.World.OnInteractableTargetChanged -= HandleInteractableTargetChanged;
        GameEvents.World.OnPsynergyTargetChanged -= HandlePsynergyTargetChanged;

        StopAutoWalk();
        InterruptPsynergyCast();

        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
            rotateCoroutine = null;
        }
    }

    private void Update()
    {
        if (!IsWorldExploration())
        {
            if (autoWalkCoroutine == null)
            {
                animController.ResetLocomotionAnimations();
            }
            return;
        }

        // Apply standard gravity physics unless climbing or airborne auto-jumping
        if (currentMovementState != PlayerMovementState.Climbing && currentMovementState != PlayerMovementState.AutoJumping)
        {
            motor.HandleGravity();
            animController.UpdateAirborneState(motor.IsGrounded, motor.VerticalVelocity);
        }

        switch (currentMovementState)
        {
            case PlayerMovementState.FreeExploration:
                HandleFreeExploration();
                break;

            case PlayerMovementState.InFirstPerson:
            case PlayerMovementState.CastingPsynergy:
                animController.ResetLocomotionAnimations();
                motor.StopImmediate();
                break;

            case PlayerMovementState.Climbing:
                HandleClimbingMovement();
                break;

            case PlayerMovementState.Sliding:
                HandleSlidingMovement();
                break;

            case PlayerMovementState.Falling:
                HandleFallingMovement();
                break;

            case PlayerMovementState.AutoJumping:
            case PlayerMovementState.PushingObject:
            case PlayerMovementState.AutoWalking:
                // Managed by dedicated coroutines or external state modules
                break;
        }
    }

    private void LateUpdate()
    {
        if (!IsWorldExploration()) return;

        if (currentMovementState == PlayerMovementState.InFirstPerson)
        {
            HandleFirstPersonViewRotation();
        }
    }

    #endregion

    #region Input Subscriptions & Handlers

    private void SubscribeToInputEvents()
    {
        UnsubscribeFromInputEvents();

        GameEvents.Input.OnMoveInputChanged += HandleMoveInputChanged;
        GameEvents.Input.OnRunStateChanged += HandleRunStateChanged;
        GameEvents.Input.OnChangeHeroPressed += HandleChangeHero;
        GameEvents.Input.OnInteractPressed += HandleInteraction;
        GameEvents.Input.OnToggleFirstPersonViewPressed += HandleToggleFirstPersonView;
        GameEvents.Input.OnUsePsynergyPressed += HandleUsePsynergy;
    }

    private void UnsubscribeFromInputEvents()
    {
        GameEvents.Input.OnMoveInputChanged -= HandleMoveInputChanged;
        GameEvents.Input.OnRunStateChanged -= HandleRunStateChanged;
        GameEvents.Input.OnChangeHeroPressed -= HandleChangeHero;
        GameEvents.Input.OnInteractPressed -= HandleInteraction;
        GameEvents.Input.OnToggleFirstPersonViewPressed -= HandleToggleFirstPersonView;
        GameEvents.Input.OnUsePsynergyPressed -= HandleUsePsynergy;
    }

    private void HandleMoveInputChanged(Vector2 moveInput)
    {
        currentMoveInput = moveInput;
    }

    private void HandleRunStateChanged(bool isRunning)
    {
        isRunningInput = isRunning;
    }

    private void HandleChangeHero(int heroIndex)
    {
        if (!IsWorldExploration() || currentMovementState == PlayerMovementState.CastingPsynergy || currentMovementState == PlayerMovementState.Falling) return;

        GameEvents.Party.RequestActiveHeroIndexChange(heroIndex);
    }

    private void HandleInteraction()
    {
        if (!IsWorldExploration() || currentMovementState != PlayerMovementState.FreeExploration) return;

        if (interactionCurrentTarget != null)
        {
            interactionCurrentTarget.Interact();
        }
    }

    private void HandleToggleFirstPersonView()
    {
        if (!IsWorldExploration() || currentMovementState == PlayerMovementState.CastingPsynergy || currentMovementState == PlayerMovementState.Falling) return;

        if (currentMovementState == PlayerMovementState.InFirstPerson)
        {
            StopFirstPersonView();
        }
        else if (currentMovementState == PlayerMovementState.FreeExploration)
        {
            StartFirstPersonView();
        }
    }

    private void HandleUsePsynergy(int slotIndex)
    {
        if (!IsWorldExploration() || currentMovementState == PlayerMovementState.CastingPsynergy || currentMovementState == PlayerMovementState.Falling) return;

        GameEvents.Psynergy.RequestUseShortcut(slotIndex, psynergyCurrentTarget);
    }

    #endregion

    #region Event Handlers (GameState & Player Events)

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        currentGameState = newState;

        bool isGameplayActive = (newState == GameManager.GameState.WorldExploration);
        bool isTitleScreen = (newState == GameManager.GameState.TitleScreen);

        motor.SetControllerEnabled(!isTitleScreen);

        if (isGameplayActive)
        {
            StopAutoWalk();
        }
        else
        {
            InterruptPsynergyCast();

            if (currentMovementState == PlayerMovementState.InFirstPerson)
            {
                StopFirstPersonView();
            }
        }
    }

    private void HandleRequestTeleport(Vector3 position, Quaternion rotation)
    {
        StopAutoWalk();
        InterruptPsynergyCast();
        currentMovementState = PlayerMovementState.FreeExploration;

        motor.SetControllerEnabled(false);

        transform.position = position;
        transform.rotation = rotation;

        if (currentGameState != GameManager.GameState.TitleScreen)
        {
            motor.SetControllerEnabled(true);
        }
    }

    private void HandleRequestAutoWalk(Vector3 destination)
    {
        StopAutoWalk();
        InterruptPsynergyCast();
        currentMovementState = PlayerMovementState.AutoWalking;
        autoWalkCoroutine = StartCoroutine(MoveToDestinationRoutine(destination));
    }

    private void HandleRequestStopAutoWalk()
    {
        StopAutoWalk();
    }

    private void HandleRequestLookAt(Vector3 targetPosition, float duration)
    {
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        rotateCoroutine = StartCoroutine(RotateTowardsRoutine(targetPosition, duration));
    }

    private IEnumerator RotateTowardsRoutine(Vector3 targetPosition, float duration)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) yield break;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
        rotateCoroutine = null;
    }

    private void HandleInteractableTargetChanged(Interactable target)
    {
        interactionCurrentTarget = target;
    }

    private void HandlePsynergyTargetChanged(IPsynergyTargetable target)
    {
        psynergyCurrentTarget = target;
    }

    #endregion

    #region Movement Logic & State Handlers

    private void HandleFreeExploration()
    {
        bool isMoving = currentMoveInput.sqrMagnitude >= 0.01f;
        bool isSprinting = isRunningInput && enoughStaminaToRun;

        // Process locomotion directly via motor
        float normalizedSpeed = motor.ProcessLocomotion(currentMoveInput, isSprinting, false);

        // Update locomotion Blend Trees
        animController.UpdateLocomotion(normalizedSpeed, isMoving, isSprinting);

        // --- ACTIVE LEDGE PROBING (Executed only during active movement) ---
        if (isMoving && ledgeDetector != null)
        {
            Vector3 moveDirection = Vector3.zero;
            if (mainCamera != null)
            {
                Vector3 inputDir = new Vector3(currentMoveInput.x, 0f, currentMoveInput.y).normalized;
                moveDirection = Quaternion.Euler(0f, mainCamera.eulerAngles.y, 0f) * inputDir;
            }

            // Probe using current horizontal speed scalar from motor
            ledgeDetector.CheckForLedgeJump(moveDirection, motor.CurrentSpeed);
        }

        // Stamina and Idle counter management
        if (isMoving)
        {
            idleTimeCounter = 0f;

            if (isSprinting)
            {
                ModifyStamina(-staminaDrainRate * Time.deltaTime);
            }
            else
            {
                ModifyStamina(staminaRegenRate * Time.deltaTime);
            }
        }
        else
        {
            idleTimeCounter += Time.deltaTime;
            if (idleTimeCounter >= secondsInIdle)
            {
                animController.TriggerLongIdle();
                idleTimeCounter = 1f;
            }

            ModifyStamina(staminaRegenRate * Time.deltaTime);
        }
    }

    /// <summary>
    /// Invoked externally by PlayerLedgeDetector when a valid jump condition is detected across a gap.
    /// Initiates a parabolic jump transition towards the exact destination point.
    /// </summary>
    public void ExecuteAutoJumpFromLedge(Vector3 targetLandingPosition, Vector3 jumpDirection)
    {
        if (currentMovementState != PlayerMovementState.FreeExploration) return;

        currentMovementState = PlayerMovementState.AutoJumping;
        motor.StopImmediate();

        if (animController != null)
        {
            animController.TriggerAutoJump();
        }

        StartCoroutine(AutoJumpRoutine(targetLandingPosition, jumpDirection));
    }

    private IEnumerator AutoJumpRoutine(Vector3 targetLandingPosition, Vector3 jumpDirection)
    {
        Vector3 startPosition = transform.position;

        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        int ledgeLayer = LayerMask.NameToLayer(ledgeLayerName);

        // 1. Temporarily disable layer collision between Player and Ledge during flight
        if (playerLayer != -1 && ledgeLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, ledgeLayer, true);
        }

        // Face jump direction
        if (jumpDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(jumpDirection);
        }

        // 2. Perform physical parabolic jump trajectory
        yield return StartCoroutine(motor.PerformParabolicJump(startPosition, targetLandingPosition, autoJumpArcHeight, autoJumpDuration));

        // 3. Post-landing recovery delay (prevents instant chained jump exploits and aligns landing animation)
        if (autoJumpLandingDelay > 0f)
        {
            yield return new WaitForSeconds(autoJumpLandingDelay);
        }

        // 4. RESTORE layer collisions against ledge borders upon landing
        if (playerLayer != -1 && ledgeLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, ledgeLayer, false);
        }

        // 5. Restore free locomotion
        animController.ResetLocomotionAnimations();
        currentMovementState = PlayerMovementState.FreeExploration;
    }

    #endregion

    #region Golden Sun Special Locomotion States (Psynergy, Slide, Climb, Fall)

    public void LockMovementForPsynergyCast(float duration)
    {
        if (psynergyCastCoroutine != null) StopCoroutine(psynergyCastCoroutine);
        psynergyCastCoroutine = StartCoroutine(PsynergyCastRoutine(duration));
    }

    private IEnumerator PsynergyCastRoutine(float duration)
    {
        currentMovementState = PlayerMovementState.CastingPsynergy;
        animController.ResetLocomotionAnimations();
        animController.TriggerCastPsynergy();

        yield return new WaitForSeconds(duration);

        currentMovementState = PlayerMovementState.FreeExploration;
        psynergyCastCoroutine = null;
    }

    private void InterruptPsynergyCast()
    {
        if (psynergyCastCoroutine != null)
        {
            StopCoroutine(psynergyCastCoroutine);
            psynergyCastCoroutine = null;
        }

        if (currentMovementState == PlayerMovementState.CastingPsynergy)
        {
            currentMovementState = PlayerMovementState.FreeExploration;
        }
    }

    public void StartFalling()
    {
        StopAutoWalk();
        InterruptPsynergyCast();
        currentMovementState = PlayerMovementState.Falling;
        animController.ResetLocomotionAnimations();
        animController.TriggerFall();
    }

    private void HandleFallingMovement()
    {
        // Direct vertical downward movement or floor transition
    }

    private void HandleClimbingMovement()
    {
        // Ladder/Vine climbing logic
    }

    private void HandleSlidingMovement()
    {
        // Slope/Sand sliding logic
    }

    #endregion

    #region Auto-Walk Destination System

    private void StopAutoWalk()
    {
        if (autoWalkCoroutine != null)
        {
            StopCoroutine(autoWalkCoroutine);
            autoWalkCoroutine = null;
        }

        if (currentMovementState == PlayerMovementState.AutoWalking)
        {
            currentMovementState = PlayerMovementState.FreeExploration;
        }

        animController.ResetLocomotionAnimations();
    }

    private IEnumerator MoveToDestinationRoutine(Vector3 targetPosition)
    {
        float stopThreshold = 0.15f;

        while (true)
        {
            Vector3 currentPosHorizontal = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 targetPosHorizontal = new Vector3(targetPosition.x, 0f, targetPosition.z);

            Vector3 direction = (targetPosHorizontal - currentPosHorizontal);
            float distance = direction.magnitude;

            if (distance <= stopThreshold)
            {
                break;
            }

            Vector2 autoWalkInput = new Vector2(direction.normalized.x, direction.normalized.z);
            float normalizedSpeed = motor.ProcessLocomotion(autoWalkInput, false, false);
            animController.UpdateLocomotion(normalizedSpeed, true, false);

            yield return null;
        }

        animController.ResetLocomotionAnimations();
        currentMovementState = PlayerMovementState.FreeExploration;
        autoWalkCoroutine = null;
    }

    #endregion

    #region Inspection / First Person Camera Logic

    private void StartFirstPersonView()
    {
        if (!IsWorldExploration()) return;

        currentMovementState = PlayerMovementState.InFirstPerson;

        if (mainCamera != null)
        {
            Vector3 cameraForward = Vector3.Scale(mainCamera.forward, new Vector3(1, 0, 1)).normalized;
            if (cameraForward.sqrMagnitude > 0.001f)
            {
                Vector3 targetLookPos = transform.position + cameraForward * 5f;
                GameEvents.Player.RequestLookAt(targetLookPos, 0.3f);
            }
        }

        if (freeLookCamera != null && firstPersonViewPanTilt != null && mainCamera != null)
        {
            if (freeLookCamera.TryGetComponent<CinemachineOrbitalFollow>(out var orbitalFollow))
            {
                firstPersonViewPanTilt.PanAxis.Value = orbitalFollow.HorizontalAxis.Value;
            }

            float pitchAngle = mainCamera.eulerAngles.x;
            if (pitchAngle > 180f) pitchAngle -= 360f;

            firstPersonViewPanTilt.TiltAxis.Value = pitchAngle;
        }

        if (firstPersonCamera != null)
        {
            firstPersonCamera.Priority.Value = activeFirstPersonViewPriority;
        }

        GameEvents.UI.RequestSetFirstPersonUIVisible(true);
        GameEvents.Player.TriggerFirstPersonViewChanged(true);
    }

    private void StopFirstPersonView()
    {
        currentMovementState = PlayerMovementState.FreeExploration;

        if (freeLookCamera != null && firstPersonViewPanTilt != null)
        {
            if (freeLookCamera.TryGetComponent<CinemachineOrbitalFollow>(out var orbitalFollow))
            {
                orbitalFollow.HorizontalAxis.Value = firstPersonViewPanTilt.PanAxis.Value;
                float normalizedPitch = Mathf.InverseLerp(45f, -45f, firstPersonViewPanTilt.TiltAxis.Value);
                orbitalFollow.VerticalAxis.Value = Mathf.Clamp01(normalizedPitch);
            }
        }

        if (firstPersonCamera != null)
        {
            firstPersonCamera.Priority.Value = defaultFirstPersonViewPriority;
        }

        GameEvents.UI.RequestSetFirstPersonUIVisible(false);
        GameEvents.Player.TriggerFirstPersonViewChanged(false);
    }

    private void HandleFirstPersonViewRotation()
    {
        if (mainCamera == null || !IsWorldExploration()) return;

        Vector3 cameraForward = Vector3.Scale(mainCamera.forward, new Vector3(1, 0, 1)).normalized;

        if (cameraForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, firstPersonViewRotationSpeed * Time.deltaTime);
        }
    }

    #endregion

    #region Stamina Management

    private void ModifyStamina(float amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);

        if (currentStamina < 5f)
        {
            enoughStaminaToRun = false;
        }
        else if (!enoughStaminaToRun && currentStamina > 30f)
        {
            enoughStaminaToRun = true;
        }

        GameEvents.Player.TriggerStaminaChanged(currentStamina, maxStamina);
    }

    #endregion

    #region Utilities & State Checks

    private bool IsWorldExploration() => currentGameState == GameManager.GameState.WorldExploration;

    #endregion
}