using System.Collections;
using UnityEngine;

/// <summary>
/// Single Responsibility Component dedicated to character physics, acceleration/deceleration,
/// slope/ground adherence, camera-relative rotation, direct CharacterController execution,
/// and deterministic parabolic auto-jumps with dynamic slope elevation handling.
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
    public bool IsControllerEnabled => controller != null && controller.enabled;

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
    /// Applies vertical gravity settlement and resolves CharacterController ground snapping 
    /// without modifying horizontal momentum or interrupting auto-walk routines.
    /// </summary>
    public void ApplyVerticalSettlement()
    {
        if (controller == null || !controller.enabled) return;

        HandleGravity();

        // Movemos ÚNICAMENTE en el eje Y para posar la cápsula en el suelo
        Vector3 verticalDisplacement = new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime;
        controller.Move(verticalDisplacement);
    }

    /// <summary>
    /// Instantly stops horizontal momentum (useful for cutscenes, UI, or edge halts).
    /// </summary>
    public void StopImmediate()
    {
        currentSpeed = 0f;
        currentMoveDirection = Vector3.zero;
    }

    /// <summary>
    /// Resets accumulated vertical velocity to prevent ground snap glitches upon landing.
    /// </summary>
    public void ResetVerticalVelocity()
    {
        verticalVelocity = stickToGroundForce;
    }

    #endregion

    #region Auto-Jump & Parabolic Movement Utilities

    /// <summary>
    /// Enables or disables the CharacterController component safely.
    /// </summary>
    public void SetControllerEnabled(bool enabledState)
    {
        if (controller != null)
        {
            controller.enabled = enabledState;
        }
    }

    /// <summary>
    /// Manual position override when CharacterController is disabled.
    /// </summary>
    public void SetPositionDirectly(Vector3 targetPosition)
    {
        transform.position = targetPosition;
    }

    /// <summary>
    /// Executes a deterministic dynamic parabolic jump arc towards a target landing position.
    /// Interpolates 3D position (including height deltas) while adding a parabolic arc offset relative to travel progress.
    /// </summary>
    /// <param name="startPosition">Starting world coordinates at edge takeoff.</param>
    /// <param name="targetPosition">Landing world coordinates at target tile center.</param>
    /// <param name="jumpHeight">Maximum vertical apex offset added over the 3D trajectory.</param>
    /// <param name="duration">Total travel time in seconds for the jump animation.</param>
    public IEnumerator PerformParabolicJump(Vector3 startPosition, Vector3 targetPosition, float jumpHeight, float duration)
    {
        float elapsedTime = 0f;

        // Rotate character towards the landing target at takeoff
        Vector3 jumpDirection = (targetPosition - startPosition);
        jumpDirection.y = 0f;
        if (jumpDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(jumpDirection);
        }

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;

            // 1. Base 3D Linear Interpolation (handles slope/height delta continuously from start.y to target.y)
            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, t);

            // 2. Add relative parabolic curve offset: 4 * t * (1 - t) reaches 1.0 at t = 0.5
            float verticalArcOffset = jumpHeight * 4f * t * (1f - t);
            currentPos.y += verticalArcOffset;

            // 3. Execute frame displacement via CharacterController
            if (controller != null && controller.enabled)
            {
                Vector3 moveDisplacement = currentPos - transform.position;
                controller.Move(moveDisplacement);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Precise final position snap
        if (controller != null && controller.enabled)
        {
            Vector3 finalSnap = targetPosition - transform.position;
            controller.Move(finalSnap);
        }

        ResetVerticalVelocity();
    }

    #endregion
}