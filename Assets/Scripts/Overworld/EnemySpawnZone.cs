using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles localized enemy spawning, cooldown timers, distance-based player validation,
/// distance-based enemy active culling, and NavMesh random point generation for world exploration roaming.
/// </summary>
public class EnemySpawnZone : MonoBehaviour
{
    #region Serialized Fields

    [Header("Spawn Configuration")]
    [Tooltip("List of possible enemy prefabs spawned in this zone.")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Tooltip("Cooldown duration in seconds before respawning an enemy after death/removal.")]
    [SerializeField] private float respawnCooldown = 120f;

    [Tooltip("Minimum distance required between the player and this spawner to allow respawning.")]
    [SerializeField] private float respawnDistance = 120f;

    [Header("Culling & Optimization Settings")]
    [Tooltip("Maximum distance from the player at which the spawned enemy remains active in the hierarchy.")]
    [SerializeField] private float cullingDistance = 140f;

    [Tooltip("Time interval in seconds between distance checks to minimize CPU overhead.")]
    [SerializeField] private float cullingCheckInterval = 0.5f;

    [Header("Patrol & NavMesh Settings")]
    [Tooltip("Maximum radius around this spawner for generating random patrol waypoints.")]
    [SerializeField] private float patrolRadius = 15f;

    [Tooltip("Maximum search attempts to sample a valid point on the NavMesh.")]
    [SerializeField] private int maxSampleAttempts = 30;

    #endregion

    #region Private Runtime State

    private GameObject activeEnemy;
    private Transform playerTransform;
    private float cooldownTimer;
    private float cullingTimer;

    // Local GameState cache
    private GameManager.GameState _cachedGameState;

    #endregion

    #region Properties

    public float PatrolRadius => patrolRadius;

    #endregion

    #region Unity Lifecycle & Events

    private void Awake()
    {
        CachePlayerTransform();
    }

    private void OnEnable()
    {
        SubscribeToGameEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameEvents();
    }

    private void Start()
    {
        SpawnEnemy();
    }

    private void Update()
    {
        if (!IsWorldExplorationActive()) return;

        // 1. Handle Respawn Cooldown Logic
        if (activeEnemy == null)
        {
            cooldownTimer += Time.deltaTime;

            if (cooldownTimer >= respawnCooldown)
            {
                if (IsPlayerFarEnough())
                {
                    SpawnEnemy();
                    cooldownTimer = 0f;
                }
            }
        }
        else
        {
            // 2. Handle Enemy Distance Culling (Executed on interval to save CPU)
            cullingTimer += Time.deltaTime;
            if (cullingTimer >= cullingCheckInterval)
            {
                cullingTimer = 0f;
                EvaluateEnemyCulling();
            }
        }
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToGameEvents()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void UnsubscribeFromGameEvents()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        _cachedGameState = newState;
    }

    #endregion

    #region Spawning & Enemy Lifecycle

    /// <summary>
    /// Instantiates a random enemy prefab from the assigned list and initializes its AI controller.
    /// </summary>
    public void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        int randomIndex = UnityEngine.Random.Range(0, enemyPrefabs.Length);
        GameObject selectedPrefab = enemyPrefabs[randomIndex];

        if (selectedPrefab == null) return;

        activeEnemy = Instantiate(selectedPrefab, transform.position, Quaternion.identity, transform);

        // Inject spawn zone reference and enable the AI component for overworld exploration
        if (activeEnemy.TryGetComponent<OverworldEnemyAI>(out var enemyAI))
        {
            enemyAI.enabled = true;
            enemyAI.InitializeSpawnZone(this);
        }

        // Initial culling check right after spawning
        EvaluateEnemyCulling();
    }

    public GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;
        return enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)];
    }

    public GameObject GetMatchingEnemyPrefab(GameObject target)
    {
        if (target == null || enemyPrefabs == null) return null;

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            var prefab = enemyPrefabs[i];
            if (prefab != null && target.name.StartsWith(prefab.name, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates distance to the player and toggles the active enemy GameObject state.
    /// </summary>
    private void EvaluateEnemyCulling()
    {
        if (activeEnemy == null) return;

        if (playerTransform == null)
        {
            CachePlayerTransform();
            if (playerTransform == null) return;
        }

        // Use sqrMagnitude to avoid square root calculation overhead
        float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;
        bool shouldBeActive = sqrDistance <= (cullingDistance * cullingDistance);

        if (activeEnemy.activeSelf != shouldBeActive)
        {
            activeEnemy.SetActive(shouldBeActive);
        }
    }

    #endregion

    #region NavMesh Point Sampling Engine

    /// <summary>
    /// Samples a valid random point on the NavMesh within the designated patrol radius.
    /// </summary>
    public Vector3 GetRandomNavMeshPoint()
    {
        Vector3 center = transform.position;

        for (int i = 0; i < maxSampleAttempts; i++)
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * patrolRadius;
            randomDirection += center;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
#if UNITY_EDITOR
                Debug.DrawRay(hit.position, Vector3.up * 1.5f, Color.green, 1.0f);
#endif
                return hit.position;
            }
        }

        return center;
    }

    #endregion

    #region Private Helper Methods

    private void CachePlayerTransform()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private bool IsPlayerFarEnough()
    {
        if (playerTransform == null)
        {
            CachePlayerTransform();
            if (playerTransform == null) return true;
        }

        return Vector3.Distance(playerTransform.position, transform.position) > respawnDistance;
    }

    private bool IsWorldExplorationActive()
    {
        return _cachedGameState == GameManager.GameState.WorldExploration;
    }

    #endregion

    #region Editor Debugging Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, respawnDistance);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, cullingDistance);
    }
#endif

    #endregion
}