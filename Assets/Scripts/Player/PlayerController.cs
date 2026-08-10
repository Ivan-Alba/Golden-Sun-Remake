using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("References")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private Camera aimCamera;

    #endregion

    #region Private Fields

    private CharacterController controller;
    private Animator playerAnim;
    private AudioSource audioSource;

    private float verticalVelocity;
    private float turnSmoothVelocity;
    private float currentStamina;
    private bool enoughStaminaToRun = true;

    private GameObject objectHit;
    private bool inChoosePsynergyBar;
    private bool aiming;

    private float timeCount;
    private readonly float secondsInIdle = 15f;

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
    }

    private void OnEnable()
    {
        SubscribeToInputEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();
    }

    private void Update()
    {
        // Abort execution and force Idle animation if the game is not in free exploration state
        if (!IsWorldExploration())
        {
            ResetMovementAnimations();
            return;
        }

        HandleGravity();
        HandleMovement();
        UpdateInteractionUI();
    }

    private void FixedUpdate()
    {
        // Forward Raycast for interactable detection
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
        InputManager.Instance.OnHoldPsynergyBarChanged += HandleHoldPsynergyBar;
        InputManager.Instance.OnPsynergyNavigateUpPressed += HandlePsynergyUp;
        InputManager.Instance.OnPsynergyNavigateDownPressed += HandlePsynergyDown;
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
        InputManager.Instance.OnHoldPsynergyBarChanged -= HandleHoldPsynergyBar;
        InputManager.Instance.OnPsynergyNavigateUpPressed -= HandlePsynergyUp;
        InputManager.Instance.OnPsynergyNavigateDownPressed -= HandlePsynergyDown;
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
        if (!IsWorldExploration()) return;

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
        if (!IsWorldExploration()) return;

        if (aiming)
        {
            StopAiming();
        }

        if (MenuController.Instance != null)
        {
            MenuController.Instance.ShowMenu();
        }
    }

    private void HandleUsePsynergy(int slotIndex)
    {
        if (!IsWorldExploration()) return;

        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.UsePsynergy(slotIndex, null);
        }
    }

    private void HandleHoldPsynergyBar(int slotIndex, bool isHolding)
    {
        if (!IsWorldExploration()) return;

        inChoosePsynergyBar = isHolding;

        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.ShowPsynergyChoosePanel(isHolding, slotIndex);
        }
    }

    private void HandlePsynergyUp()
    {
        if (!IsWorldExploration()) return;

        if (inChoosePsynergyBar && PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.ChoosePanelMoveUp();
        }
    }

    private void HandlePsynergyDown()
    {
        if (!IsWorldExploration()) return;

        if (inChoosePsynergyBar && PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.ChoosePanelMoveDown();
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

        if (direction.magnitude >= 0.1f)
        {
            timeCount = 0f;

            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

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

    /// <summary>
    /// Resets movement animator parameters to force the player into Idle state.
    /// Only writes to the Animator if movement parameters are currently true.
    /// </summary>
    public void ResetMovementAnimations()
    {
        if (playerAnim == null) return;

        // Only set parameters if they are active to avoid continuous Animator writes
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

    #region Aiming Logic

    private void StartAiming()
    {
        if (!IsWorldExploration()) return;
        if (aiming) return;

        if (mainCamera.GetComponent<Camera>().enabled) Camera.main.enabled = false;
        aimCamera.enabled = true;

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.SetAimUIVisible(true);
        }

        if (TryGetComponent<PlayerAiming>(out var aimingScript))
        {
            aimingScript.enabled = true;
        }

        aiming = true;
    }

    private void StopAiming()
    {
        // StopAiming can be triggered automatically during state transitions, 
        // so we only check if aiming is currently active.
        if (!aiming) return;

        if (TryGetComponent<PlayerAiming>(out var aimingScript))
        {
            aimingScript.Reset();
            aimingScript.enabled = false;
        }

        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, transform.eulerAngles.z);
        mainCamera.GetComponent<Camera>().enabled = true;
        aimCamera.enabled = false;

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.SetAimUIVisible(false);
        }

        aiming = false;
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

    /// <summary>
    /// Helper method to check if the game is currently in free exploration state.
    /// </summary>
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