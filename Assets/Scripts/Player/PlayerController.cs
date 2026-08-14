using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class PlayerController : MonoBehaviour
{
    // Singleton Instance
    public static PlayerController Instance { get; private set; }

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
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    [SerializeField] private CinemachineVirtualCamera aimVirtualCamera;
    [SerializeField] private float aimRotationSpeed = 1200f;

    [Header("Cinemachine Priority Settings")]
    [SerializeField] private int defaultAimPriority = 0;
    [SerializeField] private int activeAimPriority = 20;

    #endregion

    #region Private Fields

    private CharacterController controller;
    private Animator playerAnim;
    private AudioSource audioSource;
    private CinemachinePOV aimPOV;

    private float verticalVelocity;
    private float turnSmoothVelocity;
    private float currentStamina;
    private bool enoughStaminaToRun = true;

    private GameObject objectHit;

    private float timeCount;
    private readonly float secondsInIdle = 15f;

    private Coroutine autoWalkCoroutine;

    #endregion

    #region Properties

    public Animator PlayerAnim { get => playerAnim; set => playerAnim = value; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        playerAnim = GetComponent<Animator>();

        currentStamina = maxStamina;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        SubscribeToInputEvents();

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.RefreshPartyInfo();
            PlayerUIManager.Instance.UpdateStamina(currentStamina, maxStamina);
        }

        if (aimVirtualCamera != null)
        {
            aimVirtualCamera.Priority = defaultAimPriority;
            aimPOV = aimVirtualCamera.GetCinemachineComponent<CinemachinePOV>();
        }
    }

    private void OnEnable()
    {
        SubscribeToInputEvents();

        // Subscribe to GameManager state changes to handle transitions cleanly
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();

        // Unsubscribe from GameManager state changes
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        StopAutoWalk();
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
        UpdateInteractionUI();
    }

    private void LateUpdate()
    {
        if (InputManager.Instance != null && InputManager.Instance.IsAiming)
        {
            HandleAimRotation();
        }
    }

    private void FixedUpdate()
    {
        Ray ray = new Ray(transform.position + Vector3.up, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.DrawRay(transform.position + Vector3.up, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
            objectHit = hit.transform.gameObject;
        }
        else
        {
            objectHit = null;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Interactable"))
        {
            objectHit = other.gameObject;
        }
    }

    #endregion

    #region GameState Events

    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        // Whenever player regains control in world exploration, cancel any residual auto-walk operations
        if (newState == GameManager.GameState.WorldExploration)
        {
            StopAutoWalk();
        }
    }

    #endregion

    #region Input Subscriptions

    private void SubscribeToInputEvents()
    {
        if (InputManager.Instance == null) return;

        UnsubscribeFromInputEvents();

        InputManager.Instance.OnChangeHeroPressed += HandleChangeHero;
        InputManager.Instance.OnInteractPressed += HandleInteraction;
        InputManager.Instance.OnToggleMenuPressed += HandleMenuToggle;
        InputManager.Instance.OnStartAimPressed += StartAiming;
        InputManager.Instance.OnStopAimPressed += StopAiming;
        InputManager.Instance.OnUsePsynergyPressed += HandleUsePsynergy;
    }

    private void UnsubscribeFromInputEvents()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnChangeHeroPressed -= HandleChangeHero;
        InputManager.Instance.OnInteractPressed -= HandleInteraction;
        InputManager.Instance.OnToggleMenuPressed -= HandleMenuToggle;
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

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.RefreshPartyInfo();
        }
    }

    private void HandleInteraction()
    {
        if (!IsWorldExploration() || InputManager.Instance.IsAiming) return;

        if (objectHit != null && objectHit.CompareTag("Interactable") &&
            Vector3.Distance(objectHit.transform.position, transform.position) < 2f)
        {
            if (PlayerUIManager.Instance != null)
            {
                PlayerUIManager.Instance.SetInteractionIconVisible(false);
            }
            objectHit.GetComponent<Interactable>()?.Interact();
        }
    }

    private void HandleMenuToggle()
    {
        /*if (!IsWorldExploration()) return;

        if (InputManager.Instance != null && InputManager.Instance.IsAiming)
        {
            StopAiming();
        }

        if (MenuController.Instance != null)
        {
            MenuController.Instance.ShowMenu();
        }*/
    }

    private void HandleUsePsynergy(int slotIndex)
    {
        if (!IsWorldExploration()) return;

        if (PsynergyWorldController.Instance != null)
        {
            IPsynergyTargetable currentTarget = null;

            if (TryGetComponent<PlayerAiming>(out var aimingScript))
            {
                currentTarget = aimingScript.CurrentTarget;
            }

            PsynergyWorldController.Instance.UsePsynergy(slotIndex, currentTarget);
        }
    }

    #endregion

    #region Movement & Physics Logic

    private void HandleGravity()
    {
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

            PlayerAnim.SetBool("isMoving", true);

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (controller.isGrounded && IsEdgeAhead(moveDirection))
            {
                moveDirection = Vector3.zero;
            }

            bool isRunningInput = InputManager.Instance != null && InputManager.Instance.IsRunning;

            if (isRunningInput && enoughStaminaToRun)
            {
                PlayerAnim.SetBool("isRunning", true);

                Vector3 movement = (moveDirection.normalized * (speed * 2f)) + new Vector3(0f, verticalVelocity, 0f);
                controller.Move(movement * Time.deltaTime);

                ModifyStamina(-staminaDrainRate * Time.deltaTime);
            }
            else
            {
                PlayerAnim.SetBool("isRunning", false);

                Vector3 movement = (moveDirection.normalized * speed) + new Vector3(0f, verticalVelocity, 0f);
                controller.Move(movement * Time.deltaTime);

                ModifyStamina(staminaRegenRate * Time.deltaTime);
            }
        }
        else
        {
            PlayerAnim.SetBool("isMoving", false);

            controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);

            timeCount += Time.deltaTime;
            if (timeCount >= secondsInIdle)
            {
                PlayerAnim.SetTrigger("longTime");
                timeCount = 1f;
            }

            ModifyStamina(staminaRegenRate * Time.deltaTime);
        }
    }

    private bool IsEdgeAhead(Vector3 moveDirection)
    {
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
                Debug.DrawRay(rayOrigin, Vector3.down * totalRayDistance, Color.red);
                return true;
            }

            Debug.DrawRay(rayOrigin, Vector3.down * totalRayDistance, Color.green);
            return false;
        }

        Debug.DrawRay(rayOrigin, Vector3.down * totalRayDistance, Color.red);
        return true;
    }

    public void ResetMovementAnimations()
    {
        if (playerAnim == null) return;

        if (playerAnim.GetBool("isMoving"))
        {
            playerAnim.SetBool("isMoving", false);
        }

        if (playerAnim.GetBool("isRunning"))
        {
            playerAnim.SetBool("isRunning", false);
        }
    }

    #endregion

    #region Auto-Walk Destination System

    /// <summary>
    /// Forces player to automatically walk towards a target destination (useful for triggers/cutscenes).
    /// </summary>
    public void MoveToDestination(Vector3 destination)
    {
        StopAutoWalk();
        autoWalkCoroutine = StartCoroutine(Routine_MoveToDestination(destination));
    }

    /// <summary>
    /// Cancels any active auto-walk operation and resets movement animations.
    /// </summary>
    public void StopAutoWalk()
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
            playerAnim.SetBool("isMoving", true);
            playerAnim.SetBool("isRunning", false);
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

            // Rotate smoothly towards destination
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }

            // Apply gravity
            HandleGravity();

            // Move Character
            Vector3 movement = (moveDirection * speed) + new Vector3(0f, verticalVelocity, 0f);
            controller.Move(movement * Time.deltaTime);

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

        if (freeLookCamera != null && aimPOV != null && mainCamera != null)
        {
            aimPOV.m_HorizontalAxis.Value = freeLookCamera.m_XAxis.Value;

            float pitchAngle = mainCamera.eulerAngles.x;
            if (pitchAngle > 180f) pitchAngle -= 360f;

            aimPOV.m_VerticalAxis.Value = pitchAngle;
        }

        if (aimVirtualCamera != null)
        {
            aimVirtualCamera.Priority = activeAimPriority;
        }

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.SetAimUIVisible(true);
        }

        if (TryGetComponent<PlayerAiming>(out var aimingScript))
        {
            aimingScript.enabled = true;
        }
    }

    private void StopAiming()
    {
        if (freeLookCamera != null && aimPOV != null)
        {
            freeLookCamera.m_XAxis.Value = aimPOV.m_HorizontalAxis.Value;

            float normalizedPitch = Mathf.InverseLerp(45f, -45f, aimPOV.m_VerticalAxis.Value);
            freeLookCamera.m_YAxis.Value = Mathf.Clamp01(normalizedPitch);
        }

        if (aimVirtualCamera != null)
        {
            aimVirtualCamera.Priority = defaultAimPriority;
        }

        if (TryGetComponent<PlayerAiming>(out var aimingScript))
        {
            aimingScript.Reset();
            aimingScript.enabled = false;
        }

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.SetAimUIVisible(false);
        }
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

    #region Stamina & Interaction Helpers

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

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.UpdateStamina(currentStamina, maxStamina);
        }
    }

    private void UpdateInteractionUI()
    {
        bool canInteract = objectHit != null && objectHit.CompareTag("Interactable") &&
                           Vector3.Distance(objectHit.transform.position, transform.position) < 2f;

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.SetInteractionIconVisible(canInteract);
        }
    }

    #endregion

    #region Utilities & Callbacks

    private bool IsWorldExploration()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.WorldExploration;
    }

    public void playFootprints()
    {
        if (audioSource == null) return;

        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(audioSource.clip);
    }

    public GameObject GameObjectForward()
    {
        return objectHit;
    }

    #endregion
}