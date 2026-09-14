using System.Collections.Generic;
using UnityEngine;

#region Enums & Structs

/// <summary>
/// Defines the origin or trigger mechanism of the battle.
/// </summary>
public enum BattleSourceType
{
    OverworldCollision, // Collision with an overworld enemy or random encounter zone
    ScriptedEvent       // Triggered by story events, dialogues, or fixed bosses with explicit prefabs
}

/// <summary>
/// Unified initialization context payload passed when requesting any battle.
/// Supports both Overworld Collision encounters and Scripted/Event encounters.
/// </summary>
public struct BattleInitializationData
{
    public BattleSourceType SourceType;                 // Origin: OverworldCollision vs. ScriptedEvent
    public CollisionAdvantageType AdvantageType;        // Tactical state: Neutral, PlayerAdvantage, EnemyAmbush
    public List<GameObject> EnemyPrefabs;               // Explicit prefabs list (used for Event/Boss battles)
    public EnemySpawnZone SpawnZone;                    // Spawn zone source for random exploration encounters
    public GameObject InitiatorWorldObject;             // 3D Overworld enemy object to freeze or destroy
    public AudioClip CustomBattleBGM;                   // Optional override BGM (e.g. Boss theme)
}

/// <summary>
/// Container holding the result of the 3D battlefield spawning phase.
/// </summary>
public struct BattleSpawnResult
{
    public List<GameObject> HeroList;
    public List<GameObject> EnemiesList;        // Fixed-size list matching spatial slot indices (contains nulls for empty slots)
    public List<GameObject> AllActiveFighters;  // Combined list of living combatants
}

#endregion

/// <summary>
/// Domain component responsible for managing the 3D battle arena, 
/// dynamically calculating slot spatial layouts, and spawning heroes and enemies into the scene.
/// </summary>
public class BattleFieldSpawner : MonoBehaviour
{
    [Header("Row Center Placeholders (Dynamic Spawning & Global VFX)")]
    [Tooltip("Center placeholder anchor for the hero party row.")]
    [SerializeField] private Transform heroCenterAnchor;

    [Tooltip("Center placeholder anchor for the enemy row.")]
    [SerializeField] private Transform enemyCenterAnchor;

    [Header("Dynamic Layout Settings")]
    [Tooltip("Horizontal spacing between hero combatants in meters.")]
    [SerializeField] private float heroSpacing = 2.0f;

    [Tooltip("Horizontal spacing between enemy combatants in meters.")]
    [SerializeField] private float enemySpacing = 2.5f;

    [Tooltip("LayerMask used to snap spawned fighters precisely onto arena floor geometry.")]
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Configuration Constraints")]
    [SerializeField] private int maxEnemiesInBattle = 3;

    public int MaxEnemiesInBattle => maxEnemiesInBattle;

    #region Public Spatial Center Getters

    /// <summary>
    /// Returns the central GameObject anchor for the hero party row.
    /// </summary>
    public GameObject GetHeroCenterAnchor() => heroCenterAnchor != null ? heroCenterAnchor.gameObject : null;

    /// <summary>
    /// Returns the central GameObject anchor for the enemy row.
    /// </summary>
    public GameObject GetEnemyCenterAnchor() => enemyCenterAnchor != null ? enemyCenterAnchor.gameObject : null;

    #endregion

    /// <summary>
    /// Spawns combatants and sets up the 3D arena based on the initialization payload and party data.
    /// </summary>
    public BattleSpawnResult SpawnBattleField(BattleInitializationData data, IReadOnlyList<GameObject> activeParty)
    {
        BattleSpawnResult result = new BattleSpawnResult
        {
            HeroList = new List<GameObject>(),
            EnemiesList = new List<GameObject>(new GameObject[maxEnemiesInBattle]),
            AllActiveFighters = new List<GameObject>()
        };

        // 1. DYNAMICALLY POSITION HEROES IN ARENA (Left-to-Right order)
        if (activeParty != null && activeParty.Count > 0)
        {
            List<Vector3> heroPositions = CalculateRowPositions(heroCenterAnchor, activeParty.Count, heroSpacing, invertHorizontal: false);

            for (int i = 0; i < activeParty.Count; i++)
            {
                GameObject hero = activeParty[i];
                if (hero == null) continue;

                Vector3 targetWorldPos = SnapToGround(heroPositions[i]);
                Quaternion targetRotation = ResolveFighterRotation(heroCenterAnchor, targetWorldPos, enemyCenterAnchor);

                hero.transform.position = targetWorldPos;
                hero.transform.rotation = targetRotation;

                FighterStats stats = hero.GetComponent<FighterStats>();
                if (stats != null && stats.CurrHp > 0)
                {
                    result.HeroList.Add(hero);
                    result.AllActiveFighters.Add(hero);
                }
            }
        }

        // 2. RESOLVE ENEMY PREFABS TO INSTANTIATE
        List<GameObject> prefabsToInstantiate = new List<GameObject>();

        if (data.SourceType == BattleSourceType.ScriptedEvent)
        {
            if (data.EnemyPrefabs != null && data.EnemyPrefabs.Count > 0)
            {
                int limit = Mathf.Min(data.EnemyPrefabs.Count, maxEnemiesInBattle);
                for (int i = 0; i < limit; i++)
                {
                    if (data.EnemyPrefabs[i] != null)
                    {
                        prefabsToInstantiate.Add(data.EnemyPrefabs[i]);
                    }
                }
            }
        }
        else // BattleSourceType.OverworldCollision
        {
            if (data.SpawnZone != null)
            {
                int countToSpawn = Random.Range(1, maxEnemiesInBattle + 1);

                for (int i = 0; i < countToSpawn; i++)
                {
                    // Index 0 is the Overworld collision initiator
                    GameObject prefab = (i == 0 && data.InitiatorWorldObject != null)
                        ? data.SpawnZone.GetMatchingEnemyPrefab(data.InitiatorWorldObject)
                        : data.SpawnZone.GetRandomEnemyPrefab();

                    if (prefab != null)
                    {
                        prefabsToInstantiate.Add(prefab);
                    }
                }
            }
        }

        // 3. DISTRIBUTE ENEMIES SYMMETRICALLY & MAP TO ENEMIESLIST FROM LEFT TO RIGHT
        if (prefabsToInstantiate.Count > 0)
        {
            int totalEnemies = prefabsToInstantiate.Count;

            // Sort prefabs so the initiator is placed at the spatial center index of the group
            List<GameObject> orderedPrefabs = OrderPrefabsSymmetrically(prefabsToInstantiate);

            // Calculate world 3D positions strictly ordered Left-to-Right (inverting axis to align screen-space)
            List<Vector3> enemyWorldPositions = CalculateRowPositions(enemyCenterAnchor, totalEnemies, enemySpacing, invertHorizontal: true);

            // Map the totalEnemies group to the central slots of maxEnemiesInBattle array
            int startSlotIndex = (maxEnemiesInBattle - totalEnemies) / 2;

            for (int i = 0; i < totalEnemies; i++)
            {
                int targetSlotIndex = startSlotIndex + i;

                Vector3 targetWorldPos = SnapToGround(enemyWorldPositions[i]);
                Quaternion targetRotation = ResolveFighterRotation(enemyCenterAnchor, targetWorldPos, heroCenterAnchor);

                GameObject spawnedEnemy = Instantiate(orderedPrefabs[i], targetWorldPos, targetRotation);

                // Populate EnemiesList strictly aligned Left-to-Right
                result.EnemiesList[targetSlotIndex] = spawnedEnemy;
                result.AllActiveFighters.Add(spawnedEnemy);
            }
        }

        // 4. CLEANUP OVERWORLD INITIATOR IF APPLICABLE
        if (data.InitiatorWorldObject != null)
        {
            Destroy(data.InitiatorWorldObject);
        }

        return result;
    }

    #region Spatial Layout & Order Helpers

    /// <summary>
    /// Resolves fighter orientation. Uses parallel anchor forward rotation for clean formation lines.
    /// </summary>
    private Quaternion ResolveFighterRotation(Transform ownAnchor, Vector3 originPos, Transform targetAnchor)
    {
        if (ownAnchor != null)
        {
            // Pure parallel alignment with anchor orientation
            return ownAnchor.rotation;
        }

        if (targetAnchor != null)
        {
            Vector3 direction = (targetAnchor.position - originPos);
            direction.y = 0;
            if (direction != Vector3.zero) return Quaternion.LookRotation(direction, Vector3.up);
        }

        return Quaternion.identity;
    }

    /// <summary>
    /// Orders prefabs so index 0 (Initiator) ends up at the physical center position of the active group.
    /// Input list: [Initiator, EnemyA, EnemyB] -> Ordered output: [EnemyA, Initiator, EnemyB] (Left to Right)
    /// </summary>
    private List<GameObject> OrderPrefabsSymmetrically(List<GameObject> inputPrefabs)
    {
        int count = inputPrefabs.Count;
        GameObject[] ordered = new GameObject[count];

        if (count == 1)
        {
            ordered[0] = inputPrefabs[0];
            return new List<GameObject>(ordered);
        }

        int centerPosIndex = (count - 1) / 2;
        ordered[centerPosIndex] = inputPrefabs[0]; // Initiator placed at center slot

        int leftOffset = 1;
        int rightOffset = 1;

        for (int i = 1; i < count; i++)
        {
            if (i % 2 != 0)
            {
                // Place to the right of center
                if (centerPosIndex + rightOffset < count)
                {
                    ordered[centerPosIndex + rightOffset] = inputPrefabs[i];
                    rightOffset++;
                }
            }
            else
            {
                // Place to the left of center
                if (centerPosIndex - leftOffset >= 0)
                {
                    ordered[centerPosIndex - leftOffset] = inputPrefabs[i];
                    leftOffset++;
                }
            }
        }

        return new List<GameObject>(ordered);
    }

    /// <summary>
    /// Calculates world positions evenly spaced along the local X-axis (right vector) of the center anchor.
    /// Arranges targets from left to right.
    /// </summary>
    private List<Vector3> CalculateRowPositions(Transform centerAnchor, int count, float spacing, bool invertHorizontal = false)
    {
        List<Vector3> positions = new List<Vector3>();
        if (centerAnchor == null || count <= 0) return positions;

        Vector3 anchorPos = centerAnchor.position;
        Vector3 rightVector = invertHorizontal ? -centerAnchor.right : centerAnchor.right;

        float startOffset = -((count - 1) * spacing) / 2.0f;

        for (int i = 0; i < count; i++)
        {
            float currentOffset = startOffset + (i * spacing);
            Vector3 calculatedPos = anchorPos + (rightVector * currentOffset);
            positions.Add(calculatedPos);
        }

        return positions;
    }

    /// <summary>
    /// Performs a downward Raycast from elevated position to snap spatial points onto arena ground geometry.
    /// </summary>
    private Vector3 SnapToGround(Vector3 originalPosition)
    {
        Vector3 rayStart = originalPosition + (Vector3.up * 5.0f);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 15.0f, groundLayerMask))
        {
            return hit.point;
        }

        return originalPosition;
    }

    #endregion
}