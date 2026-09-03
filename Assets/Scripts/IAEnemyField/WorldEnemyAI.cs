using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls overworld enemy AI roaming, player detection, pathfinding chase behavior via NavMeshAgent,
/// and reactive game state pause/resume capabilities via GameEvents.GameState.
/// </summary>
public class WorldEnemyAI : MonoBehaviour
{
    #region Animator Hashes

    private static readonly int MoveSpeedHash = Animator.StringToHash("Move");

    #endregion

    #region Serialized Fields

    [Header("Roaming & Patrol Settings")]
    [Tooltip("Normal movement speed while patrolling.")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("Minimum wait time in seconds at each patrol waypoint.")]
    [SerializeField] private float minWaitTime = 3f;

    [Tooltip("Maximum wait time in seconds at each patrol waypoint.")]
    [SerializeField] private float maxWaitTime = 8f;

    [Tooltip("Maximum time in seconds allowed for a patrol route before timing out.")]
    [SerializeField] private float maxPatrolTimeOut = 10f;

    [Header("Detection & Chase Settings")]
    [Tooltip("Radius around the enemy to detect player presence.")]
    [SerializeField] private float followRadius = 12f;

    [Tooltip("Movement speed while chasing the player.")]
    [SerializeField] private float followSpeed = 6f;

    [Tooltip("Maximum allowed distance from spawn origin before abandoning chase.")]
    [SerializeField] private float maxFollowDistance = 30f;

    [Tooltip("Layer mask identifying player character colliders.")]
    [SerializeField] private LayerMask playerMask;

    #endregion

    #region Private Runtime State

    private NavMeshAgent agent;
    private Animator animator;
    private EnemySpawnZone spawnZone;
    private Transform playerTransform;

    private bool isChasing;
    private bool isWorldExplorationActive = true;
    private Coroutine roamingCoroutine;

    #endregion

    #region Unity Lifecycle & Events

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (agent == null)
        {
            Debug.LogError($"[WorldEnemyAI] '{name}' lacks a NavMeshAgent component!", this);
        }
    }

    private void OnEnable()
    {
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        StopActiveRoaming();
    }

    private void Start()
    {
        LocatePlayerTransform();
    }

    private void Update()
    {
        if (!isWorldExplorationActive || agent == null || !agent.enabled) return;

        // Fallback in case player was spawned after scene start
        if (playerTransform == null)
        {
            LocatePlayerTransform();
            if (playerTransform == null) return;
        }

        EvaluateAIBehavior();
        UpdateAnimator();
    }

    #endregion

    #region Event Handlers & Initialization

    /// <summary>
    /// Injected by EnemySpawnZone immediately after instantiation.
    /// </summary>
    public void InitializeSpawnZone(EnemySpawnZone zone)
    {
        spawnZone = zone;
    }

    private void LocatePlayerTransform()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void HandleGameStateChanged(GameManager.GameState previous, GameManager.GameState newState)
    {
        isWorldExplorationActive = (newState == GameManager.GameState.WorldExploration);

        if (!isWorldExplorationActive)
        {
            // Pause AI navigation and freeze locomotion
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
            // Resume AI navigation
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }
    }

    #endregion

    #region AI Logic & Behavior State Machine

    private void EvaluateAIBehavior()
    {
        bool canChasePlayer = false;

        if (playerTransform != null)
        {
            bool playerInRange = Physics.CheckSphere(transform.position, followRadius, playerMask);
            Vector3 spawnOrigin = (spawnZone != null) ? spawnZone.transform.position : transform.position;
            float distanceFromSpawn = Vector3.Distance(playerTransform.position, spawnOrigin);

            canChasePlayer = playerInRange && (distanceFromSpawn <= maxFollowDistance);
        }

        // --- Chase State ---
        if (canChasePlayer)
        {
            if (!isChasing)
            {
                isChasing = true;
                StopActiveRoaming();
            }

            agent.speed = followSpeed;
            agent.SetDestination(playerTransform.position);
        }
        // --- Patrol / Roaming State ---
        else
        {
            if (isChasing)
            {
                isChasing = false;
                agent.ResetPath();
            }

            if (!agent.hasPath && !agent.pathPending && roamingCoroutine == null)
            {
                roamingCoroutine = StartCoroutine(Routine_Roam());
            }
        }
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

        // Wait until destination is reached, path becomes invalid, or time-out triggers
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

        float waitDuration = Random.Range(minWaitTime, maxWaitTime);
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
        isChasing = false;

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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, followRadius);

        if (spawnZone != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnZone.transform.position, maxFollowDistance);
        }
    }
#endif

    #endregion
}