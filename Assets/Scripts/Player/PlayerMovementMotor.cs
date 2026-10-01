using System.Collections;
using UnityEngine;

/// <summary>
/// Single Responsibility Component dedicated to character physics, acceleration/deceleration,
/// slope/ground adherence, camera-relative rotation, and direct CharacterController execution.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovementMotor : MonoBehaviour
{
    #region Serialized Fields

    [Header("Speed Settings")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 25f;

    [Header("Rotation Settings")]
    [SerializeField] private float turnSmoothTime = 0.08f;

    [Header("Gravity & Grounding Settings")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float stickToGroundForce = -3f;

    #endregion

    #region Private Fields

    private CharacterController controller;
    private Transform mainCamera;

    private float currentSpeed;
    private float verticalVelocity;
    private float turnSmoothVelocity;
    private Vector3 currentMoveDirection;

    #endregion

    #region Properties

    public bool IsGrounded => controller != null && controller.isGrounded;
    public float CurrentSpeed => currentSpeed;
    public float VerticalVelocity => verticalVelocity;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }
    }

    #endregion

    #region Locomotion Execution

    /// <summary>
    /// Processes camera-relative movement input, applies acceleration/deceleration, 
    /// smooth rotation, and moves the CharacterController.
    /// </summary>
    /// <param name="moveInput">Raw Vector2 move input from Input System.</param>
    /// <param name="isSprinting">True if run input is active and valid.</param>
    /// <param name="isBlockedByEdge">True if edge detector signals a drop/wall ahead.</param>
    /// <returns>Normalized speed float (0.0 to 1.0) for Animator Blend Trees.</returns>
    public float ProcessLocomotion(Vector2 moveInput, bool isSprinting, bool isBlockedByEdge = false)
    {
        if (controller == null || !controller.enabled) return 0f;

        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        bool hasInput = inputDirection.sqrMagnitude >= 0.01f;

        // Target speed calculation
        float targetSpeed = 0f;
        if (hasInput && !isBlockedByEdge)
        {
            targetSpeed = isSprinting ? runSpeed : walkSpeed;
        }

        // Smooth acceleration and deceleration
        float rate = (targetSpeed > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);

        // Direction & Rotation
        if (hasInput)
        {
            float cameraAngle = mainCamera != null ? mainCamera.eulerAngles.y : 0f;
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraAngle;
            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
            currentMoveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }

        // Apply gravity & ground sticking
        HandleGravity();

        // Final velocity composition
        Vector3 finalVelocity = (currentMoveDirection * currentSpeed) + new Vector3(0f, verticalVelocity, 0f);
        controller.Move(finalVelocity * Time.deltaTime);

        // Returns normalized speed: 0.0 = Idle, 0.5 = Walk, 1.0 = Run
        float maxSpeed = isSprinting ? runSpeed : walkSpeed;
        return Mathf.Clamp01(currentSpeed / runSpeed);
    }

    /// <summary>
    /// Applies gravity physics and grounded force pinning to avoid floating on slopes.
    /// </summary>
    public void HandleGravity()
    {
        if (controller == null || !controller.enabled) return;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = stickToGroundForce;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// Instantly stops horizontal momentum (useful for cutscenes, UI, or edge halts).
    /// </summary>
    public void StopImmediate()
    {
        currentSpeed = 0f;
        currentMoveDirection = Vector3.zero;
    }

    #endregion

    #region Auto-Jump & Parabolic Movement Utilities

    /// <summary>
    /// Temporarily disables CharacterController for controlled arc jumps/tweens between tiles.
    /// </summary>
    public void SetControllerEnabled(bool enabledState)
    {
        if (controller != null)
        {
            controller.enabled = enabledState;
        }
    }

    /// <summary>
    /// Manual position override when CharacterController is disabled (e.g. during Auto-Jump curve).
    /// </summary>
    public void SetPositionDirectly(Vector3 targetPosition)
    {
        transform.position = targetPosition;
    }

    #endregion
}