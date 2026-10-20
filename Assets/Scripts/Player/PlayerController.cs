using System.Collections;
using System.Collections.Generic;
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
/// edge sensor probing to PlayerLedgeDetector, block push sensing to PlayerPushDetector,
/// and ladder/vine path sensing to PlayerClimbDetector.
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

    [Header("Climbing Parameters")]
    [SerializeField] private float climbSpeed = 2.0f;
    [SerializeField] private float dismountStepDuration = 0.45f;
    [SerializeField] private float topDismountOffset = 0.8f;
    [SerializeField] private float bottomDismountStepDuration = 0.35f;
    [SerializeField] private float bottomDismountOffset = 0.6f;
    [SerializeField] private float mountLockDuration = 0.15f;

    [Tooltip("Distancia hacia abajo (en Y) a la que se traslada rápidamente al personaje al colgarse desde un saliente para sumergir su torso bajo la cornisa.")]
    [SerializeField] private float topLedgeSafetyOffset = 1.3f;

    [Tooltip("Duración en segundos del movimiento rápido de colgarse/descender al entrar a la escalera desde un saliente.")]
    [SerializeField] private float topMountDropDuration = 0.35f;

    [Header("Layer Settings for Locomotion Safety")]
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private string ledgeLayerName = "Ledge";
    [SerializeField] private string climbableLayerName = "Climbable";

    [Header("Sub-Sensor References")]
    [Tooltip("Direct reference to the active PlayerLedgeDetector child component.")]
    [SerializeField] private PlayerLedgeDetector ledgeDetector;

    [Tooltip("Direct reference to the active PlayerPushDetector child component.")]
    [SerializeField] private PlayerPushDetector pushDetector;

    [Tooltip("Direct reference to the active PlayerClimbDetector child component.")]
    [SerializeField] private PlayerClimbDetector climbDetector;

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
    private Coroutine pushBlockCoroutine;
    private Coroutine climbDismountCoroutine;
    private Coroutine climbMountCoroutine;

    private bool isClimbingTransitioning = false;

    // Active Climbing Path Tracking Data
    private ClimbablePath currentClimbPath;
    private int currentClimbSegmentIndex = 0;

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
        GameEvents.Psynergy.OnWorldPsynergyCast += HandleWorldPsynergyCast;
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
        GameEvents.Psynergy.OnWorldPsynergyCast -= HandleWorldPsynergyCast;

        StopAutoWalk();
        InterruptPsynergyCast();

        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
            rotateCoroutine = null;
        }

        if (pushBlockCoroutine != null)
        {
            StopCoroutine(pushBlockCoroutine);
            pushBlockCoroutine = null;
        }

        if (climbDismountCoroutine != null)
        {
            StopCoroutine(climbDismountCoroutine);
            climbDismountCoroutine = null;
        }

        if (climbMountCoroutine != null)
        {
            StopCoroutine(climbMountCoroutine);
            climbMountCoroutine = null;
        }
    }

    private void Update()
    {
        if (!IsWorldExploration() && currentMovementState != PlayerMovementState.CastingPsynergy)
        {
            if (autoWalkCoroutine == null)
            {
                animController.ResetLocomotionAnimations();
            }
            return;
        }

        // Apply standard gravity physics unless climbing, airborne auto-jumping, or pushing
        if (currentMovementState != PlayerMovementState.Climbing &&
            currentMovementState != PlayerMovementState.AutoJumping &&
            currentMovementState != PlayerMovementState.PushingObject &&
            currentMovementState != PlayerMovementState.CastingPsynergy)
        {
            motor.HandleGravity();
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
        if (!IsWorldExploration() || currentMovementState == PlayerMovementState.CastingPsynergy || currentMovementState == PlayerMovementState.Falling || currentMovementState == PlayerMovementState.PushingObject || currentMovementState == PlayerMovementState.Climbing) return;

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
        if (!IsWorldExploration() || currentMovementState == PlayerMovementState.CastingPsynergy || currentMovementState == PlayerMovementState.Falling || currentMovementState == PlayerMovementState.PushingObject || currentMovementState == PlayerMovementState.Climbing) return;

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
        if (!IsWorldExploration() || currentMovementState == PlayerMovementState.CastingPsynergy || currentMovementState == PlayerMovementState.Falling || currentMovementState == PlayerMovementState.PushingObject || currentMovementState == PlayerMovementState.Climbing) return;

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

            // Restore movement state when exiting Interacting/Casting states
            if (currentMovementState == PlayerMovementState.CastingPsynergy)
            {
                currentMovementState = PlayerMovementState.FreeExploration;
            }
        }
        else
        {
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

    private void HandleWorldPsynergyCast()
    {
        currentMovementState = PlayerMovementState.CastingPsynergy;
        motor.StopImmediate();
        animController.ResetLocomotionAnimations();

        // 1. Orientar hacia el objetivo enfocado o hacia donde mira la cámara
        if (psynergyCurrentTarget != null)
        {
            Vector3 targetPos = psynergyCurrentTarget.Transform.position;
            HandleRequestLookAt(targetPos, 0.15f);
        }
        else if (mainCamera != null)
        {
            Vector3 cameraForward = Vector3.Scale(mainCamera.forward, new Vector3(1, 0, 1)).normalized;
            if (cameraForward.sqrMagnitude > 0.001f)
            {
                Vector3 lookTarget = transform.position + cameraForward * 5f;
                HandleRequestLookAt(lookTarget, 0.15f);
            }
        }

        // 2. Ejecutar la animación de brazo extendido / casteo
        animController.TriggerCastPsynergy();
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
        animController.UpdateLocomotion(normalizedSpeed);

        // --- ACTIVE SENSOR PROBING (Executed only during active movement) ---
        if (isMoving)
        {
            Vector3 moveDirection = Vector3.zero;
            if (mainCamera != null)
            {
                Vector3 inputDir = new Vector3(currentMoveInput.x, 0f, currentMoveInput.y).normalized;
                moveDirection = Quaternion.Euler(0f, mainCamera.eulerAngles.y, 0f) * inputDir;
            }

            // 1. PRIORIDAD 1: Prospección de escalada (Ladders / Vines / Rock Paths)
            if (climbDetector != null && climbDetector.TryDetectClimbablePath(moveDirection, out ClimbablePath detectedPath, out Vector3 attachPoint, out int initialSegmentIndex, out bool isMountingFromTop))
            {
                StartClimbing(detectedPath, initialSegmentIndex, isMountingFromTop);
                return;
            }

            // 2. PRIORIDAD 2: Prospección de salto automático en cornisas
            if (ledgeDetector != null && currentMovementState == PlayerMovementState.FreeExploration)
            {
                ledgeDetector.CheckForLedgeJump(moveDirection, motor.CurrentSpeed);
            }

            // 3. PRIORIDAD 3: Prospección de empuje de bloques de puzle
            if (pushDetector != null && currentMovementState == PlayerMovementState.FreeExploration)
            {
                pushDetector.CheckForPushableBlock(moveDirection, motor.CurrentSpeed);
            }
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

        // Temporarily disable layer collisions against Ledge AND Climbable layers during flight
        ToggleSpecialLayersCollision(true);

        // Face jump direction
        if (jumpDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(jumpDirection);
        }

        // Perform physical parabolic jump trajectory
        GameEvents.Audio.RequestPlaySFX(SFXType.World_Jump);
        yield return StartCoroutine(motor.PerformParabolicJump(startPosition, targetLandingPosition, autoJumpArcHeight, autoJumpDuration));

        // Post-landing recovery delay
        if (autoJumpLandingDelay > 0f)
        {
            yield return new WaitForSeconds(autoJumpLandingDelay);
        }

        // RESTORE layer collisions upon landing
        ToggleSpecialLayersCollision(false);

        // Restore free locomotion
        animController.ResetLocomotionAnimations();
        currentMovementState = PlayerMovementState.FreeExploration;
    }

    #endregion

    #region Phase 3: Climbable Paths System (Vines / Ladders / Zigzag Rocks)

    private void StartClimbing(ClimbablePath path, int segmentIndex, bool isMountingFromTop)
    {
        currentMovementState = PlayerMovementState.Climbing;
        motor.StopImmediate();

        currentClimbPath = path;
        currentClimbSegmentIndex = segmentIndex;

        ToggleSpecialLayersCollision(true);

        if (climbMountCoroutine != null) StopCoroutine(climbMountCoroutine);
        climbMountCoroutine = StartCoroutine(MountClimbableRoutine(segmentIndex, isMountingFromTop));
    }

    private IEnumerator MountClimbableRoutine(int segmentIndex, bool isMountingFromTop)
    {
        isClimbingTransitioning = true;

        ToggleSpecialLayersCollision(true);

        List<ClimbSegment> segments = currentClimbPath.GetWorldSegments();

        if (segments.Count > segmentIndex)
        {
            ClimbSegment currentSegment = segments[segmentIndex];

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            Vector3 targetAttachPoint = currentClimbPath.GetAttachPointAtPlayerHeight(transform.position, segmentIndex);
            Quaternion targetRotation = currentClimbPath.GetSegmentRotation(segmentIndex);

            if (isMountingFromTop)
            {
                float targetY = currentSegment.endPoint.y - topLedgeSafetyOffset;
                float minY = Mathf.Min(currentSegment.startPoint.y, currentSegment.endPoint.y);
                targetAttachPoint.y = Mathf.Max(targetY, minY);

                if (animController != null)
                {
                    animController.SetClimbingState(true);
                    animController.UpdateClimbSpeed(0f);
                }

                float elapsed = 0f;
                while (elapsed < topMountDropDuration)
                {
                    float rawT = elapsed / topMountDropDuration;
                    float t = Mathf.SmoothStep(0f, 1f, rawT);

                    transform.position = Vector3.Lerp(startPosition, targetAttachPoint, t);
                    transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                transform.position = targetAttachPoint;
                transform.rotation = targetRotation;
            }
            else
            {
                if (currentSegment.isBottomExit && motor.IsGrounded)
                {
                    targetAttachPoint.y = transform.position.y;
                }

                transform.position = targetAttachPoint;

                float elapsed = 0f;
                float rotateDuration = 0.12f;

                while (elapsed < rotateDuration)
                {
                    transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / rotateDuration);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                transform.rotation = targetRotation;

                if (animController != null)
                {
                    animController.SetClimbingState(true);
                    animController.UpdateClimbSpeed(0f);
                }
            }
        }

        if (mountLockDuration > 0f)
        {
            yield return new WaitForSeconds(mountLockDuration);
        }

        isClimbingTransitioning = false;
        climbMountCoroutine = null;
    }

    private void HandleClimbingMovement()
    {
        if (currentClimbPath == null || isClimbingTransitioning) return;

        List<ClimbSegment> segments = currentClimbPath.GetWorldSegments();
        if (currentClimbSegmentIndex < 0 || currentClimbSegmentIndex >= segments.Count)
        {
            ExitClimbing();
            return;
        }

        ClimbSegment currentSegment = segments[currentClimbSegmentIndex];
        Vector3 segmentVector = currentSegment.endPoint - currentSegment.startPoint;
        float segmentLength = segmentVector.magnitude;

        if (segmentLength < 0.001f)
        {
            ExitClimbing();
            return;
        }

        Vector3 segmentDirection = segmentVector / segmentLength;

        float rawVerticalInput = currentMoveInput.y;
        float moveMagnitude = Mathf.Abs(rawVerticalInput);
        bool hasMovementInput = moveMagnitude > 0.05f;

        if (hasMovementInput)
        {
            Vector3 moveDelta = segmentDirection * (Mathf.Sign(rawVerticalInput) * climbSpeed * Time.deltaTime);
            Vector3 newPosition = transform.position + moveDelta;

            Vector3 projectedPoint = currentSegment.startPoint + Vector3.Project(newPosition - currentSegment.startPoint, segmentDirection);

            float progress = Vector3.Dot(projectedPoint - currentSegment.startPoint, segmentDirection) / segmentLength;

            if (rawVerticalInput > 0f)
            {
                if (progress >= 1.0f)
                {
                    if (currentClimbSegmentIndex < segments.Count - 1)
                    {
                        currentClimbSegmentIndex++;
                        transform.position = segments[currentClimbSegmentIndex].startPoint;
                        transform.rotation = currentClimbPath.GetSegmentRotation(currentClimbSegmentIndex);
                    }
                    else if (currentSegment.isTopExit)
                    {
                        DismountTopLedge();
                        return;
                    }
                }
                else
                {
                    transform.position = projectedPoint;
                }
            }
            else if (rawVerticalInput < 0f)
            {
                if (progress <= 0.0f)
                {
                    if (currentClimbSegmentIndex > 0)
                    {
                        currentClimbSegmentIndex--;
                        transform.position = segments[currentClimbSegmentIndex].endPoint;
                        transform.rotation = currentClimbPath.GetSegmentRotation(currentClimbSegmentIndex);
                    }
                    else if (currentSegment.isBottomExit || motor.IsGrounded)
                    {
                        DismountBottomGround();
                        return;
                    }
                }
                else
                {
                    transform.position = projectedPoint;
                }
            }
        }

        if (animController != null)
        {
            animController.UpdateClimbSpeed(hasMovementInput ? 1.0f : 0.0f);
        }
    }

    private void DismountTopLedge()
    {
        if (climbDismountCoroutine != null) return;
        climbDismountCoroutine = StartCoroutine(DismountTopLedgeRoutine());
    }

    private IEnumerator DismountTopLedgeRoutine()
    {
        isClimbingTransitioning = true;

        ToggleSpecialLayersCollision(true);

        List<ClimbSegment> segments = currentClimbPath.GetWorldSegments();
        Vector3 inwardDirection = transform.forward;

        if (segments.Count > 0)
        {
            inwardDirection = -segments[segments.Count - 1].wallNormal;
        }

        if (animController != null)
        {
            animController.SetClimbingState(false);
            animController.UpdateLocomotion(0.5f);
        }

        yield return new WaitForSeconds(0.15f);

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.identity;
        if (inwardDirection.sqrMagnitude > 0.001f)
        {
            targetRotation = Quaternion.LookRotation(inwardDirection);
        }

        float effectiveOffset = currentClimbPath != null ? currentClimbPath.GetEffectiveTopDismountOffset(topDismountOffset) : topDismountOffset;

        Vector3 startPos = transform.position;
        Vector3 projectedHorizontalTarget = startPos + (inwardDirection.normalized * effectiveOffset);

        Vector3 targetPos = projectedHorizontalTarget;
        Vector3 rayStart = projectedHorizontalTarget + (Vector3.up * 1.5f);

        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        int layerMask = ~(1 << playerLayer);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 3.0f, layerMask, QueryTriggerInteraction.Ignore))
        {
            targetPos = hit.point;
        }

        float elapsedTime = 0f;

        while (elapsedTime < dismountStepDuration)
        {
            float t = elapsedTime / dismountStepDuration;

            transform.position = Vector3.Lerp(startPos, targetPos, t);

            if (inwardDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            }

            if (animController != null)
            {
                animController.UpdateLocomotion(0.5f);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        if (inwardDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = targetRotation;
        }

        ExitClimbing();
        isClimbingTransitioning = false;
        climbDismountCoroutine = null;
    }

    private void DismountBottomGround()
    {
        if (climbDismountCoroutine != null) return;
        climbDismountCoroutine = StartCoroutine(DismountBottomGroundRoutine());
    }

    private IEnumerator DismountBottomGroundRoutine()
    {
        isClimbingTransitioning = true;

        List<ClimbSegment> segments = currentClimbPath.GetWorldSegments();
        Vector3 outwardDirection = transform.forward;

        if (segments.Count > 0)
        {
            outwardDirection = segments[0].wallNormal;
        }

        if (animController != null)
        {
            animController.SetClimbingState(false);
            animController.UpdateLocomotion(0.5f);
        }

        yield return new WaitForSeconds(0.15f);

        if (outwardDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(outwardDirection);
        }

        Vector3 forwardStep = outwardDirection.normalized * bottomDismountOffset;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + forwardStep;

        float elapsedTime = 0f;

        while (elapsedTime < bottomDismountStepDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / bottomDismountStepDuration);

            if (animController != null)
            {
                animController.UpdateLocomotion(0.5f);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;

        ExitClimbing();
        isClimbingTransitioning = false;
        climbDismountCoroutine = null;
    }

    private void ExitClimbing()
    {
        currentClimbPath = null;
        currentClimbSegmentIndex = 0;

        if (animController != null)
        {
            animController.SetClimbingState(false);
            animController.ResetLocomotionAnimations();
        }

        ToggleSpecialLayersCollision(false);

        currentMovementState = PlayerMovementState.FreeExploration;
    }

    private void ToggleSpecialLayersCollision(bool ignore)
    {
        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        int ledgeLayer = LayerMask.NameToLayer(ledgeLayerName);
        int climbableLayer = LayerMask.NameToLayer(climbableLayerName);

        if (playerLayer != -1)
        {
            if (ledgeLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, ledgeLayer, ignore);
            }

            if (climbableLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, climbableLayer, ignore);
            }
        }
    }

    #endregion

    #region Phase 4: Pushable Objects System

    public void ExecuteBlockPush(PushableBlock block, Vector3 pushDirection)
    {
        if (currentMovementState != PlayerMovementState.FreeExploration) return;

        if (!block.CanBePushed(pushDirection, out bool fallsIntoPit))
        {
            return;
        }

        currentMovementState = PlayerMovementState.PushingObject;
        motor.StopImmediate();

        pushBlockCoroutine = StartCoroutine(PushBlockRoutine(block, pushDirection, fallsIntoPit));
    }

    private IEnumerator PushBlockRoutine(PushableBlock block, Vector3 pushDirection, bool fallsIntoPit)
    {
        transform.rotation = Quaternion.LookRotation(pushDirection);
        animController.SetPushingState(true);

        Coroutine blockMoveCoroutine = StartCoroutine(block.MoveBlockRoutine(pushDirection, fallsIntoPit));

        float elapsedTime = 0f;
        float pushDuration = block.PushDuration;
        float pushSpeed = block.TileSize / pushDuration;

        CharacterController charController = GetComponent<CharacterController>();

        while (elapsedTime < pushDuration)
        {
            if (charController != null && charController.enabled)
            {
                Vector3 moveVelocity = (pushDirection.normalized * pushSpeed) + (Vector3.down * 9.81f);
                charController.Move(moveVelocity * Time.deltaTime);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        yield return blockMoveCoroutine;

        animController.SetPushingState(false);
        currentMovementState = PlayerMovementState.FreeExploration;
        pushBlockCoroutine = null;
    }

    #endregion

    #region Golden Sun Special Locomotion States (Psynergy, Slide, Fall)

    private void InterruptPsynergyCast()
    {
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

            Vector3 normalizedDir = direction.normalized;

            Vector3 unrotatedDir = normalizedDir;
            if (mainCamera != null)
            {
                unrotatedDir = Quaternion.Euler(0f, -mainCamera.eulerAngles.y, 0f) * normalizedDir;
            }

            Vector2 autoWalkInput = new Vector2(unrotatedDir.x, unrotatedDir.z);
            float normalizedSpeed = motor.ProcessLocomotion(autoWalkInput, false, false);
            animController.UpdateLocomotion(normalizedSpeed);

            yield return null;
        }

        motor.StopImmediate();
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