using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles NPC autonomous locomotion, waypoint navigation, and explicit spatial orientation.
/// Centralizes ALL rotation logic (patrol orientation, idle spawn return, and dynamic player facing during dialogues).
/// </summary>
public class NPCRoaming : MonoBehaviour
{
    #region Serialized Fields

    [Header("Patrol Configuration")]
    [Tooltip("Assigned route containing spatial waypoints. Optional for static NPCs.")]
    [SerializeField] private NPCRoute assignedRoute;

    [Tooltip("Default walking speed along the route.")]
    [SerializeField] private float walkSpeed = 1.8f;

    [Tooltip("Distance threshold to consider a waypoint reached.")]
    [SerializeField] private float arrivalDistanceThreshold = 0.35f;

    [Header("Rotation Settings")]
    [Tooltip("Rotation speed when aligning towards targets or player (deg/sec).")]
    [SerializeField] private float rotationSpeed = 360f;

    [Tooltip("Native NavMesh angular speed.")]
    [SerializeField] private float navMeshAngularSpeed = 480f;

    [Tooltip("Native NavMesh acceleration rate.")]
    [SerializeField] private float navMeshAcceleration = 16f;

    [Header("Component References")]
    [Tooltip("Explicit Animator reference for locomotion Blend Tree synchronization.")]
    [SerializeField] private Animator animator;

    [Tooltip("Optional NavMeshAgent for obstacle avoidance.")]
    [SerializeField] private NavMeshAgent navAgent;

    #endregion

    #region Animator Hashes (Mixamo Common Parameters)

    private static readonly int AnimHashSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimHashIsTalking = Animator.StringToHash("IsTalking");

    #endregion

    #region Private Runtime State

    private NPC npcInteractionComponent;
    private Quaternion initialSpawnRotation;
    private Transform activePlayerTransform;

    private int currentWaypointIndex;
    private bool isForwardDirection = true;
    private bool isPausedByDialogue;
    private bool isWaitingAtWaypoint;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        initialSpawnRotation = transform.rotation;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        npcInteractionComponent = GetComponent<NPC>();

        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        if (navAgent != null)
        {
            navAgent.speed = walkSpeed;
            navAgent.angularSpeed = navMeshAngularSpeed;
            navAgent.acceleration = navMeshAcceleration;
            navAgent.updateRotation = true;
            navAgent.updatePosition = true;
        }

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
        }
    }

    private void OnEnable()
    {
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void Start()
    {
        if (assignedRoute != null && assignedRoute.Count > 0)
        {
            Vector3 initialPos = assignedRoute.GetWaypoint(0).position;

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.Warp(initialPos);
                SetCurrentWaypointDestination();
            }
            else
            {
                transform.position = initialPos;
            }
        }
    }

    private void Update()
    {
        // PRIORIDAD 1: Durante un diálogo, orienta suavemente al NPC hacia el jugador
        if (isPausedByDialogue && activePlayerTransform != null)
        {
            RotateTowardsPosition(activePlayerTransform.position);
            UpdateAnimatorParameters(0f);
            return;
        }

        // PRIORIDAD 2: NPC Estático (Sin Ruta Asignada)
        if (assignedRoute == null || assignedRoute.Count == 0)
        {
            // Vuelve suavemente a su rotación de spawn si no está hablando
            transform.rotation = Quaternion.RotateTowards(transform.rotation, initialSpawnRotation, rotationSpeed * Time.deltaTime);
            UpdateAnimatorParameters(0f);
            return;
        }

        // PRIORIDAD 3: NPC en Ruta / Esperando en Waypoint
        if (isWaitingAtWaypoint)
        {
            UpdateAnimatorParameters(0f);
            return;
        }

        ExecutePatrolNavigation();
    }

    #endregion

    #region Navigation & Orientation Execution

    private void ExecutePatrolNavigation()
    {
        Waypoint targetWaypoint = assignedRoute.GetWaypoint(currentWaypointIndex);
        float currentTargetSpeed = targetWaypoint.customSpeed > 0f ? targetWaypoint.customSpeed : walkSpeed;

        Vector3 targetPosition = targetWaypoint.position;
        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0f;

        float distanceToTarget = directionToTarget.magnitude;

        if (distanceToTarget <= arrivalDistanceThreshold)
        {
            if (!isWaitingAtWaypoint)
            {
                StartCoroutine(Routine_ArriveAtWaypoint(targetWaypoint));
            }
            return;
        }

        // Native NavMesh Execution
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.speed = currentTargetSpeed;

            if (!navAgent.hasPath)
            {
                SetCurrentWaypointDestination();
            }

            UpdateAnimatorParameters(currentTargetSpeed);
        }
        // Direct Transform Translation Fallback
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentTargetSpeed * Time.deltaTime);

            if (directionToTarget != Vector3.zero)
            {
                RotateTowardsPosition(targetPosition);
            }

            UpdateAnimatorParameters(currentTargetSpeed);
        }
    }

    private void RotateTowardsPosition(Vector3 targetWorldPos)
    {
        Vector3 direction = targetWorldPos - transform.position;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void SetCurrentWaypointDestination()
    {
        if (navAgent != null && navAgent.enabled && assignedRoute != null && assignedRoute.Count > 0)
        {
            navAgent.SetDestination(assignedRoute.GetWaypoint(currentWaypointIndex).position);
        }
    }

    private IEnumerator Routine_ArriveAtWaypoint(Waypoint waypoint)
    {
        isWaitingAtWaypoint = true;
        UpdateAnimatorParameters(0f);

        if (navAgent != null && navAgent.enabled)
        {
            navAgent.ResetPath();
        }

        if (waypoint.waitTime > 0f)
        {
            yield return new WaitForSeconds(waypoint.waitTime);
        }

        AdvanceToNextWaypointIndex();
        SetCurrentWaypointDestination();
        isWaitingAtWaypoint = false;
    }

    private void AdvanceToNextWaypointIndex()
    {
        int totalPoints = assignedRoute.Count;
        if (totalPoints <= 1) return;

        switch (assignedRoute.Mode)
        {
            case PatrolMode.Loop:
                currentWaypointIndex = (currentWaypointIndex + 1) % totalPoints;
                break;

            case PatrolMode.PingPong:
                if (isForwardDirection)
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= totalPoints - 1)
                    {
                        isForwardDirection = false;
                    }
                }
                else
                {
                    currentWaypointIndex--;
                    if (currentWaypointIndex <= 0)
                    {
                        isForwardDirection = true;
                    }
                }
                break;

            case PatrolMode.Once:
                if (currentWaypointIndex < totalPoints - 1)
                {
                    currentWaypointIndex++;
                }
                break;
        }
    }

    #endregion

    #region Animation & Event Synchronization

    private void UpdateAnimatorParameters(float currentSpeed)
    {
        if (animator == null) return;

        animator.SetFloat(AnimHashSpeed, currentSpeed);
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.InDialogue)
        {
            // Solo si ESTE NPC es con el que interactuamos
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                activePlayerTransform = playerObj.transform;
            }

            isPausedByDialogue = true;
            if (navAgent != null && navAgent.enabled) navAgent.ResetPath();

            if (animator != null)
            {
                animator.SetFloat(AnimHashSpeed, 0f);
                animator.SetBool(AnimHashIsTalking, true);
            }
        }
        else if (previousState == GameManager.GameState.InDialogue)
        {
            isPausedByDialogue = false;
            activePlayerTransform = null;

            if (animator != null)
            {
                animator.SetBool(AnimHashIsTalking, false);
            }

            SetCurrentWaypointDestination();
        }
    }

    #endregion
}