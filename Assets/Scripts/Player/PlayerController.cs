using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Controls player movement, physics, stamina management, camera targeting, and interaction execution.
/// Reacts to decoupled commands via GameEvents.Player and GameEvents.GameState.
/// </summary>
public class PlayerController : MonoBehaviour
{
    #region Animator Hashes (Cached for performance)

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int LongTimeHash = Animator.StringToHash("longTime");

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
    [SerializeField] private CinemachineCamera aimVirtualCamera;
    [SerializeField] private float aimRotationSpeed = 1200f;

    [Header("Cinemachine Priority Settings")]
    [SerializeField] private int defaultAimPriority = 0;
    [SerializeField] private int activeAimPriority = 20;

    #endregion

    #region Private Fields

    private CharacterController controller;
    private Animator playerAnim;
    private FootstepAudioEmitter footstepEmitter;
    private CinemachinePanTilt aimPanTilt;
    private PlayerInteractionDetector interactionDetector;

    private float verticalVelocity;
    private float turnSmoothVelocity;
    private float currentStamina;
    private bool enoughStaminaToRun = true;

    private float timeCount;
    private readonly float secondsInIdle = 15f;

    private Coroutine autoWalkCoroutine;
    private Coroutine rotateCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerAnim = GetComponent<Animator>();
        interactionDetector = GetComponent<PlayerInteractionDetector>();
        footstepEmitter = GetComponent<FootstepAudioEmitter>();

        currentStamina = maxStamina;
    }

    private void Start()
    {
        // If InputManager wasn't ready during OnEnable, subscribe now safely
        SubscribeToInputEvents();

        // Broadcast initial stamina and party status via events instead of calling PlayerUIManager directly
        GameEvents.Player.TriggerStaminaChanged(currentStamina, maxStamina);
        GameEvents.Player.TriggerHeroStatusChanged();

        if (aimVirtualCamera != null)
        {
            aimVirtualCamera.Priority.Value = defaultAimPriority;
            aimPanTilt = aimVirtualCamera.GetComponent<CinemachinePanTilt>();
        }
    }

    private void OnEnable()
    {
        SubscribeToInputEvents();

        // Subscribe to GameEvents
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
        GameEvents.Player.OnRequestTeleport += HandleRequestTeleport;
        GameEvents.Player.OnRequestLookAt += HandleRequestLookAt;
        GameEvents.Player.OnRequestAutoWalk += HandleRequestAutoWalk;
        GameEvents.Player.OnRequestStopAutoWalk += HandleRequestStopAutoWalk;
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();

        // Unsubscribe from GameEvents
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.Player.OnRequestTeleport -= HandleRequestTeleport;
        GameEvents.Player.OnRequestLookAt -= HandleRequestLookAt;
        GameEvents.Player.OnRequestAutoWalk -= HandleRequestAutoWalk;
        GameEvents.Player.OnRequestStopAutoWalk -= HandleRequestStopAutoWalk;

        StopAutoWalk();

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
            // Reset standard movement animations if not auto-walking
            if (autoWalkCoroutine == null)
            {
                ResetMovementAnimations();
            }
            return;
        }

        HandleGravity();
        HandleMovement();
    }

    private void LateUpdate()
    {
        if (InputManager.Instance != null && InputManager.Instance.IsAiming)
        {
            HandleAimRotation();
        }
    }

    #endregion

    #region Event Handlers (GameState & Player Events)

    /// <summary>
    /// Reacts to global game state shifts, enabling/disabling player movement and delegating mesh visibility.
    /// </summary>
    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        bool isGameplayActive = (newState == GameManager.GameState.WorldExploration);
        bool isTitleScreen = (newState == GameManager.GameState.TitleScreen);

        // Control character physics controller
        if (controller != null) controller.enabled = !isTitleScreen;

        // Delegate mesh visibility toggle to PlayerVisualManager via GameEvents
        GameEvents.Player.RequestSetPlayerVisibility(!isTitleScreen);

        // Whenever player regains control in world exploration, cancel any residual auto-walk operations
        if (isGameplayActive)
        {
            StopAutoWalk();
        }
    }

    /// <summary>
    /// Safely repositions and rotates the player, toggling CharacterController to avoid physics glitches.
    /// </summary>
    private void HandleRequestTeleport(Vector3 position, Quaternion rotation)
    {
        StopAutoWalk();

        if (controller != null) controller.enabled = false;

        transform.position = position;
        transform.rotation = rotation;

        if (controller != null && GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.TitleScreen)
        {
            controller.enabled = true;
        }
    }

    private void HandleRequestAutoWalk(Vector3 destination)
    {
        StopAutoWalk();
        autoWalkCoroutine = StartCoroutine(Routine_MoveToDestination(destination));
    }

    private void HandleRequestStopAutoWalk()
    {
        StopAutoWalk();
    }

    private void HandleRequestLookAt(Vector3 targetPosition, float duration)
    {
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        rotateCoroutine = StartCoroutine(Routine_RotateTowards(targetPosition, duration));
    }

    private IEnumerator Routine_RotateTowards(Vector3 targetPosition, float duration)
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

    #endregion

    #region Input Subscriptions

    private void SubscribeToInputEvents()
    {
        if (InputManager.Instance == null) return;

        // Unsubscribe first to avoid duplicate callbacks
        UnsubscribeFromInputEvents();

        InputManager.Instance.OnChangeHeroPressed += HandleChangeHero;
        InputManager.Instance.OnInteractPressed += HandleInteraction;
        InputManager.Instance.OnStartAimPressed += StartAiming;
        InputManager.Instance.OnStopAimPressed += StopAiming;
        InputManager.Instance.OnUsePsynergyPressed += HandleUsePsynergy;
    }

    private void UnsubscribeFromInputEvents()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnChangeHeroPressed -= HandleChangeHero;
        InputManager.Instance.OnInteractPressed -= HandleInteraction;
        InputManager.Instance.OnStartAimPressed -= StartAiming;
        InputManager.Instance.OnStopAimPressed -= StopAiming;
        InputManager.Instance.OnUsePsynergyPressed -= HandleUsePsynergy;
    }

    #endregion

    #region Input Event Handlers

    private void HandleChangeHero(int heroIndex)
    {
        if (!IsWorldExploration()) return;

        GameManager.Instance.ChangeActiveHero(heroIndex);

        // Notify UI elements to update party info via event
        GameEvents.Player.TriggerHeroStatusChanged();
    }

    private void HandleInteraction()
    {
        if (!IsWorldExploration() || (InputManager.Instance != null && InputManager.Instance.IsAiming)) return;

        if (interactionDetector != null && interactionDetector.CurrentTarget != null)
        {
            interactionDetector.CurrentTarget.Interact();
        }
    }

    private void HandleUsePsynergy(int slotIndex)
    {
        if (!IsWorldExploration()) return;

        IPsynergyTargetable currentTarget = null;

        if (TryGetComponent<PlayerAiming>(out var aimingScript))
        {
            currentTarget = aimingScript.CurrentTarget;
        }

        // Requests Psynergy execution via global event bus (decoupled from PsynergyWorldController)
        GameEvents.Psynergy.RequestUseShortcut(slotIndex, currentTarget);
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

        Vector2 inputMove = InputManager.Instance != null ? InputManager.Instance.MoveInput : Vector2.zero;
        Vector3 direction = new Vector3(inputMove.x, 0f, inputMove.y).normalized;

        bool isAimingState = InputManager.Instance != null && InputManager.Instance.IsAiming;

        if (direction.magnitude >= 0.1f)
        {
            timeCount = 0f;

            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

            if (!isAimingState)
            {
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            if (playerAnim != null) playerAnim.SetBool(IsMovingHash, true);

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (controller.isGrounded && IsEdgeAhead(moveDirection))
            {
                moveDirection = Vector3.zero;
            }

            bool isRunningInput = InputManager.Instance != null && InputManager.Instance.IsRunning;

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

            timeCount += Time.deltaTime;
            if (timeCount >= secondsInIdle)
            {
                if (playerAnim != null) playerAnim.SetTrigger(LongTimeHash);
                timeCount = 1f;
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

    #region Auto-Walk Destination System

    private void StopAutoWalk()
    {
        if (autoWalkCoroutine != null)
        {
            StopCoroutine(autoWalkCoroutine);
            autoWalkCoroutine = null;
        }

        ResetMovementAnimations();
    }

    private IEnumerator Routine_MoveToDestination(Vector3 targetPosition)
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
        autoWalkCoroutine = null;
    }

    #endregion

    #region Aiming Logic

    private void StartAiming()
    {
        if (!IsWorldExploration()) return;

        if (mainCamera != null)
        {
            Vector3 cameraForward = Vector3.Scale(mainCamera.forward, new Vector3(1, 0, 1)).normalized;
            if (cameraForward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(cameraForward);
            }
        }

        if (freeLookCamera != null && aimPanTilt != null && mainCamera != null)
        {
            if (freeLookCamera.TryGetComponent<CinemachineOrbitalFollow>(out var orbitalFollow))
            {
                aimPanTilt.PanAxis.Value = orbitalFollow.HorizontalAxis.Value;
            }

            float pitchAngle = mainCamera.eulerAngles.x;
            if (pitchAngle > 180f) pitchAngle -= 360f;

            aimPanTilt.TiltAxis.Value = pitchAngle;
        }

        if (aimVirtualCamera != null)
        {
            aimVirtualCamera.Priority.Value = activeAimPriority;
        }

        GameEvents.UI.RequestSetAimUIVisible(true);

        if (TryGetComponent<PlayerAiming>(out var aimingScript))
        {
            aimingScript.enabled = true;
        }
    }

    private void StopAiming()
    {
        if (freeLookCamera != null && aimPanTilt != null)
        {
            if (freeLookCamera.TryGetComponent<CinemachineOrbitalFollow>(out var orbitalFollow))
            {
                orbitalFollow.HorizontalAxis.Value = aimPanTilt.PanAxis.Value;
                float normalizedPitch = Mathf.InverseLerp(45f, -45f, aimPanTilt.TiltAxis.Value);
                orbitalFollow.VerticalAxis.Value = Mathf.Clamp01(normalizedPitch);
            }
        }

        if (aimVirtualCamera != null)
        {
            aimVirtualCamera.Priority.Value = defaultAimPriority;
        }

        if (TryGetComponent<PlayerAiming>(out var aimingScript))
        {
            aimingScript.Reset();
            aimingScript.enabled = false;
        }

        GameEvents.UI.RequestSetAimUIVisible(false);
    }

    private void HandleAimRotation()
    {
        if (mainCamera == null) return;

        Vector3 cameraForward = Vector3.Scale(mainCamera.forward, new Vector3(1, 0, 1)).normalized;

        if (cameraForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, aimRotationSpeed * Time.deltaTime);
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

        // Notify stamina changes via GameEvents instead of calling PlayerUIManager directly
        GameEvents.Player.TriggerStaminaChanged(currentStamina, maxStamina);
    }

    /// <summary>
    /// Invoked directly by Unity Animation Events on character walk/run animations.
    /// Delegates footstep audio detection and execution to FootstepAudioEmitter.
    /// </summary>
    public void playFootprints()
    {
        if (footstepEmitter != null)
        {
            footstepEmitter.OnFootstep();
        }
    }

    #endregion

    #region Utilities & State Checks

    private bool IsWorldExploration() => GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.WorldExploration;

    #endregion
}