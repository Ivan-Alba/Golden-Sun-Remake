using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Defines the alert states of an overworld enemy AI.
/// </summary>
public enum EnemyAlertState
{
    Calm,
    Suspicious,
    Chasing
}

/// <summary>
/// Controls overworld enemy AI roaming, multi-zone player perception, threat accumulation,
/// smooth pathfinding chase behavior via NavMeshAgent, and decoupled state broadcasting via GameEvents.
/// </summary>
public class OverworldEnemyAI : MonoBehaviour
{
    #region Animator Hashes

    private static readonly int MoveSpeedHash = Animator.StringToHash("Move");

    #endregion

    #region Serialized Fields

    [Header("Patrol & Roaming Settings")]
    [Tooltip("Movement speed while patrolling.")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("Minimum wait time in seconds at each patrol waypoint.")]
    [SerializeField] private float minWaitTime = 3f;

    [Tooltip("Maximum wait time in seconds at each patrol waypoint.")]
    [SerializeField] private float maxWaitTime = 8f;

    [Tooltip("Maximum time in seconds allowed for a patrol route before timing out.")]
    [SerializeField] private float maxPatrolTimeOut = 10f;

    [Header("Perception & Threat Detection")]
    [Tooltip("Broad 360-degree radius to perceive player presence and noise.")]
    [SerializeField] private float perceptionRadius = 12f;

    [Tooltip("Close-range direct vision radius.")]
    [SerializeField] private float visionRadius = 8f;

    [Tooltip("Forward viewing cone angle in degrees (half-angle from center).")]
    [SerializeField] private float visionAngle = 55f;

    [Tooltip("Speed multiplier for threat accumulation when approaching from behind.")]
    [SerializeField] private float stealthApproachMultiplier = 0.35f;

    [Tooltip("Threat build rate per second when in general perception range.")]
    [SerializeField] private float baseThreatBuildRate = 0.5f;

    [Tooltip("Threat decay rate per second when player is out of perception range.")]
    [SerializeField] private float threatDecayRate = 0.4f;

    [Tooltip("Threat threshold (0.0 to 1.0) to enter Suspicious state and turn towards player.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float suspiciousThreshold = 0.5f;

    [Header("Chase & Tether Settings")]
    [Tooltip("Movement speed while chasing the player.")]
    [SerializeField] private float followSpeed = 5.5f;

    [Tooltip("Maximum allowed distance from spawn origin before abandoning chase.")]
    [SerializeField] private float maxFollowDistance = 30f;

    [Tooltip("Rotation speed when tracking player in Suspicious state.")]
    [SerializeField] private float trackingRotationSpeed = 5f;

    #endregion

    #region Private Runtime State

    private NavMeshAgent agent;
    private Animator animator;
    private EnemySpawnZone spawnZone;
    private Transform playerTransform;

    private EnemyAlertState currentState = EnemyAlertState.Calm;
    private float currentThreatLevel = 0f;

    private bool isWorldExplorationActive = true;
    private Coroutine roamingCoroutine;

    #endregion

    #region Properties

    public EnemyAlertState CurrentState => currentState;
    public float CurrentThreatLevel => currentThreatLevel;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (agent == null)
        {
            Debug.LogError($"[OverworldEnemyAI] '{name}' lacks a NavMeshAgent component!", this);
        }

        LocatePlayerTransform();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
        StopActiveRoaming();
    }

    private void Update()
    {
        if (!isWorldExplorationActive || agent == null || !agent.enabled) return;

        EvaluateDetectionAndThreat();
        EvaluateAIBehavior();
        UpdateAnimator();
    }

    #endregion

    #region Initialization & Subscriptions

    private void LocatePlayerTransform()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"[OverworldEnemyAI] '{name}' could not locate persistent Player GameObject on Awake!", this);
        }
    }

    private void SubscribeToEvents()
    {
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameManager.GameState previous, GameManager.GameState newState)
    {
        isWorldExplorationActive = (newState == GameManager.GameState.WorldExploration);

        if (!isWorldExplorationActive)
        {
            StopActiveRoaming();

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            if (animator != null && animator.enabled)
            {
                animator.SetFloat(MoveSpeedHash, 0f);
            }
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }
    }

    /// <summary>
    /// Injected by EnemySpawnZone immediately after instantiation.
    /// </summary>
    public void InitializeSpawnZone(EnemySpawnZone zone)
    {
        spawnZone = zone;
    }

    #endregion

    #region Detection & Threat Mechanics

    private void EvaluateDetectionAndThreat()
    {
        if (playerTransform == null) return;

        Vector3 toPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = toPlayer.magnitude;

        Vector3 spawnOrigin = (spawnZone != null) ? spawnZone.transform.position : transform.position;
        float distanceFromSpawn = Vector3.Distance(playerTransform.position, spawnOrigin);

        // Abandon chase if player exceeds tether distance
        if (distanceFromSpawn > maxFollowDistance)
        {
            DecayThreat();
            return;
        }

        bool inPerceptionRange = distanceToPlayer <= perceptionRadius;
        bool inVisionRange = distanceToPlayer <= visionRadius;

        Vector3 dirToPlayerNorm = toPlayer.normalized;
        dirToPlayerNorm.y = 0f;

        Vector3 forwardNorm = transform.forward;
        forwardNorm.y = 0f;

        float angleToPlayer = Vector3.Angle(forwardNorm, dirToPlayerNorm);
        bool inVisionCone = inVisionRange && (angleToPlayer <= visionAngle);

        if (inVisionCone)
        {
            // Instant 100% threat if entering frontal vision cone
            SetThreatLevel(1.0f);
        }
        else if (inPerceptionRange)
        {
            // Calculate build rate: slower if approaching from behind
            bool isBehind = angleToPlayer > visionAngle;
            float multiplier = isBehind ? stealthApproachMultiplier : 1.0f;

            float threatDelta = baseThreatBuildRate * multiplier * Time.deltaTime;
            SetThreatLevel(currentThreatLevel + threatDelta);
        }
        else
        {
            DecayThreat();
        }
    }

    private void DecayThreat()
    {
        if (currentThreatLevel > 0f)
        {
            SetThreatLevel(currentThreatLevel - (threatDecayRate * Time.deltaTime));
        }
    }

    private void SetThreatLevel(float newValue)
    {
        float clamped = Mathf.Clamp01(newValue);
        if (Mathf.Approximately(currentThreatLevel, clamped)) return;

        currentThreatLevel = clamped;

        EnemyAlertState newState = EvaluateAlertStateFromThreat(currentThreatLevel);
        if (newState != currentState)
        {
            currentState = newState;
        }

        GameEvents.World.TriggerEnemyThreatLevelChanged(gameObject, currentThreatLevel, currentState);
    }

    private EnemyAlertState EvaluateAlertStateFromThreat(float threat)
    {
        if (threat >= 1.0f) return EnemyAlertState.Chasing;
        if (threat >= suspiciousThreshold) return EnemyAlertState.Suspicious;
        return EnemyAlertState.Calm;
    }

    #endregion

    #region AI Logic & Behavior State Machine

    private void EvaluateAIBehavior()
    {
        switch (currentState)
        {
            case EnemyAlertState.Calm:
                ExecuteCalmPatrolState();
                break;

            case EnemyAlertState.Suspicious:
                ExecuteSuspiciousState();
                break;

            case EnemyAlertState.Chasing:
                ExecuteChaseState();
                break;
        }
    }

    private void ExecuteCalmPatrolState()
    {
        if (agent.speed != patrolSpeed)
        {
            agent.speed = patrolSpeed;
        }

        if (!agent.hasPath && !agent.pathPending && roamingCoroutine == null)
        {
            roamingCoroutine = StartCoroutine(Routine_Roam());
        }
    }

    private void ExecuteSuspiciousState()
    {
        StopActiveRoaming();

        if (agent.hasPath)
        {
            agent.ResetPath();
        }

        // Rotate to track player position while suspicious
        if (playerTransform != null)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, trackingRotationSpeed * Time.deltaTime);
            }
        }
    }

    private void ExecuteChaseState()
    {
        StopActiveRoaming();

        if (playerTransform == null) return;

        if (agent.speed != followSpeed)
        {
            agent.speed = followSpeed;
        }

        agent.SetDestination(playerTransform.position);
    }

    private IEnumerator Routine_Roam()
    {
        if (spawnZone == null) yield break;

        Vector3 randomPoint = spawnZone.GetRandomNavMeshPoint();

        agent.speed = patrolSpeed;
        if (!agent.SetDestination(randomPoint))
        {
            roamingCoroutine = null;
            yield break;
        }

        float patrolTimer = 0f;

        while (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.2f))
        {
            patrolTimer += Time.deltaTime;
            if (patrolTimer >= maxPatrolTimeOut)
            {
                agent.ResetPath();
                break;
            }
            yield return null;
        }

        float waitDuration = UnityEngine.Random.Range(minWaitTime, maxWaitTime);
        yield return new WaitForSeconds(waitDuration);

        roamingCoroutine = null;
    }

    private void StopActiveRoaming()
    {
        if (roamingCoroutine != null)
        {
            StopCoroutine(roamingCoroutine);
            roamingCoroutine = null;
        }
    }

    /// <summary>
    /// Disables agent and animations when entering combat trigger collision.
    /// </summary>
    public void CollisionWithPlayer()
    {
        StopActiveRoaming();
        SetThreatLevel(0f);
        currentState = EnemyAlertState.Calm;

        if (agent != null) agent.enabled = false;
        if (animator != null) animator.enabled = false;
    }

    private void UpdateAnimator()
    {
        if (animator != null && animator.enabled)
        {
            float currentSpeed = agent.velocity.magnitude;
            animator.SetFloat(MoveSpeedHash, currentSpeed);
        }
    }

    #endregion

    #region Editor Debugging Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Perception Radius (360)
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);

        // Direct Vision Radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        // Vision Cone Lines
        Vector3 leftBound = Quaternion.Euler(0, -visionAngle, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, visionAngle, 0) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftBound * visionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, rightBound * visionRadius);

        // Tether Max Distance
        if (spawnZone != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnZone.transform.position, maxFollowDistance);
        }
    }
#endif

    #endregion
}