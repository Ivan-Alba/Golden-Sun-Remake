using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Defines the combat encounter conditions based on physical impact orientation in the overworld.
/// </summary>
public enum EncounterType
{
    Neutral,
    PlayerAdvantage,
    EnemyAmbush
}

/// <summary>
/// Detects physical contacts between the player and overworld enemies.
/// Calculates encounter orientation advantages, plays impact feedback via GameEvents, 
/// and initiates battle transitions cleanly without direct manager coupling.
/// </summary>
public class EnemyDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [Tooltip("Optional reference to the root persistent Player GameObject. Auto-assigned in Awake if null.")]
    [SerializeField] private GameObject player;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip audioCollision;

    [Header("Encounter Angle Thresholds")]
    [Tooltip("Maximum angle difference in degrees to qualify as an advantage/ambush from behind.")]
    [SerializeField] private float backstabAngleThreshold = 60f;

    #endregion

    #region Private Fields

    private Collider enemyTouched;
    private bool battleLaunched;

    // Local GameState cache
    private GameManager.GameState _cachedGameState;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Locate root persistent Player object once on Awake
        if (player == null && transform.root != null)
        {
            player = transform.root.gameObject;
        }
    }

    private void OnEnable()
    {
        SubscribeToGameEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameEvents();
    }

    #endregion

    #region Event Subscriptions & Handlers

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

    private void OnTriggerEnter(Collider other)
    {
        if (battleLaunched) return;

        // Clean validation: check for AI/Enemy component instead of string tag comparison
        if (!other.TryGetComponent<OverworldEnemyAI>(out _) && !other.TryGetComponent<EnemyStats>(out _))
            return;

        if (_cachedGameState != GameManager.GameState.WorldExploration)
            return;

        ExecuteEncounterTrigger(other);
    }

    #endregion

    #region Encounter Logic

    private void ExecuteEncounterTrigger(Collider enemyCollider)
    {
        battleLaunched = true;
        enemyTouched = enemyCollider;

        // 1. Lock overworld inputs during transition
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Cutscene);
        GameEvents.Player.TriggerPlayInteractionAnimation(PlayerInteractionAnimType.EnemyHitReaction);

        // 2. Determine encounter type based on impact angle
        EncounterType encounterType = CalculateEncounterType(enemyCollider.transform);

        Debug.Log($"Encounter Type: {encounterType}");

        // 3. Play collision sound via Audio events
        if (audioCollision != null)
        {
            GameEvents.Audio.RequestPlaySFXClip(audioCollision);
        }

        // 4. Handle Player physical reaction
        if (player != null)
        {
            if (player.TryGetComponent<CharacterController>(out var cc))
            {
                cc.SimpleMove(Vector3.zero);
            }

            // Only rotate to face enemy if the player was NOT ambushed from behind
            if (encounterType != EncounterType.EnemyAmbush)
            {
                Vector3 lookTarget = new Vector3(enemyCollider.transform.position.x, player.transform.position.y, enemyCollider.transform.position.z);
                GameEvents.Player.RequestLookAt(lookTarget, 0.15f);
            }
        }

        // 5. Freeze Overworld Enemy AI and animator
        if (enemyCollider.TryGetComponent<OverworldEnemyAI>(out var enemyAI))
        {
            enemyAI.CollisionWithPlayer();
        }

        if (enemyCollider.TryGetComponent<Animator>(out var enemyAnim))
        {
            enemyAnim.enabled = false;
        }

        // 6. Begin battle transition delay
        StartCoroutine(Routine_StartBattle(encounterType));
    }

    /// <summary>
    /// Calculates whether the impact was a Player Advantage, Enemy Ambush, or Neutral encounter.
    /// Uses backstabAngleThreshold from Inspector to calculate precise dot-product cones.
    /// </summary>
    private EncounterType CalculateEncounterType(Transform enemyTransform)
    {
        if (player == null || enemyTransform == null) return EncounterType.Neutral;

        // Convert inspector angle (in degrees) to required dot product threshold (cosine)
        float minDotThreshold = Mathf.Cos(backstabAngleThreshold * Mathf.Deg2Rad);

        // Player forward vector projected on XZ plane
        Vector3 playerForward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;

        // Direction vector from Player to Enemy projected on XZ plane
        Vector3 dirToEnemy = Vector3.ProjectOnPlane(enemyTransform.position - player.transform.position, Vector3.up).normalized;

        // Enemy forward vector projected on XZ plane
        Vector3 enemyForward = Vector3.ProjectOnPlane(enemyTransform.forward, Vector3.up).normalized;

        float playerFacingEnemyDot = Vector3.Dot(playerForward, dirToEnemy);
        float enemyFacingSameWayDot = Vector3.Dot(playerForward, enemyForward);

        // 1. PLAYER ADVANTAGE (Player faces enemy within cone AND enemy faces away)
        if (playerFacingEnemyDot > minDotThreshold && enemyFacingSameWayDot > minDotThreshold)
        {
            return EncounterType.PlayerAdvantage;
        }

        // 2. ENEMY AMBUSH (Enemy is within player's rear backstab cone AND enemy faces same way)
        float enemyInPlayerBackConeDot = Vector3.Dot(playerForward, dirToEnemy);

        if (enemyInPlayerBackConeDot < -minDotThreshold && enemyFacingSameWayDot > minDotThreshold)
        {
            return EncounterType.EnemyAmbush;
        }

        // 3. NEUTRAL ENCOUNTER (Side swipes, frontal collisions or ambiguous angles)
        return EncounterType.Neutral;
    }

    private IEnumerator Routine_StartBattle(EncounterType encounterType)
    {
        yield return new WaitForSeconds(0.8f);

        if (enemyTouched != null)
        {
            GameObject enemyObj = enemyTouched.gameObject;
            GameObject spawnZoneObj = enemyTouched.transform.parent != null
                ? enemyTouched.transform.parent.gameObject
                : null;

            GameEvents.Battle.RequestCollisionBattle(enemyObj, spawnZoneObj, encounterType);
        }

        battleLaunched = false;
    }

    #endregion
}