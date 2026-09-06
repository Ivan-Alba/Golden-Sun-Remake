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
    Climbing,         // Climbing ladders, vines, or ropes
    Sliding,          // Automatic sliding down slopes or sand dunes
    Falling,          // Falling down holes, dungeon pitfalls, or chasm drops
    CastingPsynergy,  // Frozen executing field Psynergy animations (Move, Force, etc.)
    AutoWalking       // Controlled automatically by cutscene or trigger events
}

/// <summary>
/// Controls player exploration movement, character physics, stamina drain/regeneration,
/// edge detection, auto-walk cutscene commands, and first-person inspection camera mode.
/// Fully decoupled via GameEvents (Input, Player, World, GameState).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    #region Animator Hashes (Cached for performance)

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int LongTimeHash = Animator.StringToHash("longTime");
    private static readonly int CastPsynergyHash = Animator.StringToHash("castPsynergy");

    #endregion

    #region Serialized Fields

    [Header("Movement Settings")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float turnSmoothTime = 0.1f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float stickToGroundForce = -2f;

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainRate = 10f;
    [SerializeField] private float staminaRegenRate = 7f;

    [Header("Edge Detection Settings")]
    [SerializeField] private float frontMargin = 0.8f;
    [SerializeField] private float extraDropMargin = 0.2f;
    [SerializeField] private LayerMask groundLayer;

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

    private CharacterController controller;
    private Animator playerAnim;
    private FootstepAudioEmitter footstepEmitter;
    private CinemachinePanTilt firstPersonViewPanTilt;

    private Interactable interactionCurrentTarget;
    private IPsynergyTargetable psynergyCurrentTarget;

    private Vector2 currentMoveInput;
    private bool isRunningInput;
    private PlayerMovementState currentMovementState = PlayerMovementState.FreeExploration;
    private GameManager.GameState currentGameState = GameManager.GameState.TitleScreen;

    private float verticalVelocity;
    private float turnSmoothVelocity;
    private float currentStamina;
    private bool enoughStaminaToRun = true;

    private float idleTimeCounter;
    private readonly float secondsInIdle = 15f;

    private Coroutine autoWalkCoroutine;
    private Coroutine rotateCoroutine;
    private Coroutine psynergyCastCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerAnim = GetComponent<Animator>();
        footstepEmitter = GetComponent<FootstepAudioEmitter>();

        currentStamina = maxStamina;
    }

    private void Start()
    {
        GameEvents.Player.TriggerStaminaChanged(currentStamina, maxStamina);
        GameEvents.Player.TriggerHeroStatusChanged();

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
                ResetMovementAnimations();
            }
            return;
        }

        // Apply gravity standard physics unless climbing or falling programmatically
        if (currentMovementState != PlayerMovementState.Climbing)
        {
            HandleGravity();
        }

        switch (currentMovementState)
        {
            case PlayerMovementState.FreeExploration:
                HandleMovement();
                break;

            case PlayerMovementState.InFirstPerson:
            case PlayerMovementState.CastingPsynergy:
                ResetMovementAnimations();
                if (controller != null && controller.enabled)
                {
                    controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
                }
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

            case PlayerMovementState.AutoWalking:
                // Managed by auto-walk routine
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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeActiveHero(heroIndex);
        }

        GameEvents.Player.TriggerHeroStatusChanged();
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

        if (controller != null) controller.enabled = !isTitleScreen;

        GameEvents.Player.RequestSetPlayerVisibility(!isTitleScreen);

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

        if (controller != null) controller.enabled = false;

        transform.position = position;
        transform.rotation = rotation;

        if (controller != null && currentGameState != GameManager.GameState.TitleScreen)
        {
            controller.enabled = true;
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

    #region Movement & Physics Logic

    private void HandleGravity()
    {
        if (controller == null || !controller.enabled) return;

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = stickToGroundForce;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        if (controller == null || !controller.enabled) return;

        Vector3 direction = new Vector3(currentMoveInput.x, 0f, currentMoveInput.y).normalized;

        if (direction.magnitude >= 0.1f)
        {
            idleTimeCounter = 0f;

            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + (mainCamera != null ? mainCamera.eulerAngles.y : 0f);
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            if (playerAnim != null) playerAnim.SetBool(IsMovingHash, true);

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (controller.isGrounded && IsEdgeAhead(moveDirection))
            {
                moveDirection = Vector3.zero;
            }

            if (isRunningInput && enoughStaminaToRun)
            {
                if (playerAnim != null) playerAnim.SetBool(IsRunningHash, true);

                Vector3 movement = (moveDirection.normalized * (speed * 2f)) + new Vector3(0f, verticalVelocity, 0f);
                controller.Move(movement * Time.deltaTime);

                ModifyStamina(-staminaDrainRate * Time.deltaTime);
            }
            else
            {
                if (playerAnim != null) playerAnim.SetBool(IsRunningHash, false);

                Vector3 movement = (moveDirection.normalized * speed) + new Vector3(0f, verticalVelocity, 0f);
                controller.Move(movement * Time.deltaTime);

                ModifyStamina(staminaRegenRate * Time.deltaTime);
            }
        }
        else
        {
            if (playerAnim != null) playerAnim.SetBool(IsMovingHash, false);

            controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);

            idleTimeCounter += Time.deltaTime;
            if (idleTimeCounter >= secondsInIdle)
            {
                if (playerAnim != null) playerAnim.SetTrigger(LongTimeHash);
                idleTimeCounter = 1f;
            }

            ModifyStamina(staminaRegenRate * Time.deltaTime);
        }
    }

    private bool IsEdgeAhead(Vector3 moveDirection)
    {
        if (controller == null) return false;

        float dynamicEdgeOffset = controller.radius + frontMargin;
        float safetyMargin = 0.2f;
        float rayStartHeight = controller.stepOffset + safetyMargin;
        float maxWalkableDrop = controller.stepOffset + extraDropMargin;
        float totalRayDistance = rayStartHeight + maxWalkableDrop;

        Vector3 rayOrigin = transform.position + (moveDirection.normalized * dynamicEdgeOffset) + (Vector3.up * rayStartHeight);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, totalRayDistance, groundLayer))
        {
            float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);

            if (slopeAngle > controller.slopeLimit)
            {
#if UNITY_EDITOR
                Debug.DrawRay(rayOrigin, Vector3.down * totalRayDistance, Color.red);
#endif
                return true;
            }

#if UNITY_EDITOR
            Debug.DrawRay(rayOrigin, Vector3.down * totalRayDistance, Color.green);
#endif
            return false;
        }

#if UNITY_EDITOR
        Debug.DrawRay(rayOrigin, Vector3.down * totalRayDistance, Color.red);
#endif
        return true;
    }

    private void ResetMovementAnimations()
    {
        if (playerAnim == null) return;

        if (playerAnim.GetBool(IsMovingHash))
        {
            playerAnim.SetBool(IsMovingHash, false);
        }

        if (playerAnim.GetBool(IsRunningHash))
        {
            playerAnim.SetBool(IsRunningHash, false);
        }
    }

    #endregion

    #region Golden Sun Special Locomotion States (Psynergy, Slide, Climb, Fall)

    /// <summary>
    /// Temporarily locks movement while playing the overworld Psynergy casting animation.
    /// </summary>
    public void LockMovementForPsynergyCast(float duration)
    {
        if (psynergyCastCoroutine != null) StopCoroutine(psynergyCastCoroutine);
        psynergyCastCoroutine = StartCoroutine(PsynergyCastRoutine(duration));
    }

    private IEnumerator PsynergyCastRoutine(float duration)
    {
        currentMovementState = PlayerMovementState.CastingPsynergy;
        ResetMovementAnimations();

        if (playerAnim != null)
        {
            playerAnim.SetTrigger(CastPsynergyHash);
        }

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

    /// <summary>
    /// Triggers automatic downward falling when stepping into pitfall traps or floor holes.
    /// </summary>
    public void StartFalling()
    {
        StopAutoWalk();
        InterruptPsynergyCast();
        currentMovementState = PlayerMovementState.Falling;
        ResetMovementAnimations();

        // TODO: Trigger falling animation (e.g. playerAnim.SetTrigger("fallHole"))
    }

    private void HandleFallingMovement()
    {
        if (controller == null || !controller.enabled) return;

        // TODO: Apply direct vertical downward movement through holes or trigger scene/floor transition
        controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

    private void HandleClimbingMovement()
    {
        // TODO: Golden Sun Ladder/Vine climbing logic (move vertically along Y axis, disable gravity)
    }

    private void HandleSlidingMovement()
    {
        // TODO: Golden Sun Slope/Sand sliding logic (move downwards continuously along slope vector)
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

        ResetMovementAnimations();
    }

    private IEnumerator MoveToDestinationRoutine(Vector3 targetPosition)
    {
        if (playerAnim != null)
        {
            playerAnim.SetBool(IsMovingHash, true);
            playerAnim.SetBool(IsRunningHash, false);
        }

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

            Vector3 moveDirection = direction.normalized;

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }

            HandleGravity();

            if (controller != null && controller.enabled)
            {
                Vector3 movement = (moveDirection * speed) + new Vector3(0f, verticalVelocity, 0f);
                controller.Move(movement * Time.deltaTime);
            }

            yield return null;
        }

        ResetMovementAnimations();
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

    #region Stamina & Audio Animation Events

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

    /// <summary>
    /// Animation Event Callback triggered by the locomotion clip.
    /// Broadcasts footstep sound triggers to the attached FootstepAudioEmitter.
    /// </summary>
    public void PlayFootprints()
    {
        if (footstepEmitter != null)
        {
            footstepEmitter.OnFootstep();
        }
    }

    // Backwards compatibility for legacy animation clip events
    public void playFootprints() => PlayFootprints();

    #endregion

    #region Utilities & State Checks

    private bool IsWorldExploration() => currentGameState == GameManager.GameState.WorldExploration;

    #endregion
}