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
    public List<GameObject> EnemiesList;        // Fixed-size array/list matching slot indices (contains nulls for empty slots)
    public GameObject[] EnemyPositions;         // Direct slot transforms used for target positioning
    public List<GameObject> AllActiveFighters;  // Combined list of living combatants
}

#endregion

/// <summary>
/// Domain component responsible for managing the 3D battle arena, 
/// calculating slot layouts, and spawning heroes and enemies into the scene.
/// </summary>
public class BattleFieldSpawner : MonoBehaviour
{
    [Header("Arena Spatial Slot Anchors")]
    [Tooltip("Direct references to hero spawn slot transforms inside the arena prefab.")]
    [SerializeField] private Transform[] heroSlots = new Transform[4];

    [Tooltip("Direct references to enemy spawn slot transforms inside the arena prefab.")]
    [SerializeField] private Transform[] enemySlots = new Transform[5];

    [Header("Configuration Constraints")]
    [SerializeField] private int maxEnemiesInBattle = 3;

    public int MaxEnemiesInBattle => maxEnemiesInBattle;
    public Transform[] EnemySlots => enemySlots;
    public Transform[] HeroSlots => heroSlots;

    /// <summary>
    /// Spawns combatants and sets up the 3D arena based on the initialization payload and party data.
    /// </summary>
    public BattleSpawnResult SpawnBattleField(BattleInitializationData data, IReadOnlyList<GameObject> activeParty)
    {
        BattleSpawnResult result = new BattleSpawnResult
        {
            HeroList = new List<GameObject>(),
            EnemiesList = new List<GameObject>(new GameObject[maxEnemiesInBattle]),
            EnemyPositions = new GameObject[maxEnemiesInBattle],
            AllActiveFighters = new List<GameObject>()
        };

        // 1. POSITION HEROES IN ARENA SLOTS
        if (activeParty != null && activeParty.Count > 0)
        {
            int slotOffset = (activeParty.Count <= 2) ? 1 : 0;

            for (int i = 0; i < activeParty.Count; i++)
            {
                GameObject hero = activeParty[i];
                if (hero == null) continue;

                int targetSlotIndex = Mathf.Clamp(i + slotOffset, 0, heroSlots.Length - 1);

                // Position hero at slot anchor
                if (heroSlots[targetSlotIndex] != null)
                {
                    hero.transform.position = heroSlots[targetSlotIndex].position;
                    hero.transform.rotation = heroSlots[targetSlotIndex].rotation;
                }

                // Add to alive combatants list
                FighterStats stats = hero.GetComponent<FighterStats>();
                if (stats != null && stats.CurrHp > 0)
                {
                    result.HeroList.Add(hero);
                    result.AllActiveFighters.Add(hero);
                }
            }
        }

        // 2. RESOLVE & INSTANTIATE ENEMIES
        List<GameObject> prefabsToInstantiate = new List<GameObject>();

        if (data.SourceType == BattleSourceType.ScriptedEvent)
        {
            // Event / Boss Encounter: Use explicit prefabs provided in payload
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
            // Exploration Collision Encounter: Resolve via SpawnZone
            if (data.SpawnZone != null)
            {
                int countToSpawn = Random.Range(1, maxEnemiesInBattle + 1);

                for (int i = 0; i < countToSpawn; i++)
                {
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

        // 3. DISTRIBUTE ENEMIES SYMMETRICALLY FROM CENTER SLOT
        int centerIndex = maxEnemiesInBattle / 2;
        List<int> spawnOrder = CalculateSpawnSlotsOrder(centerIndex, maxEnemiesInBattle);

        for (int i = 0; i < prefabsToInstantiate.Count; i++)
        {
            if (i >= spawnOrder.Count) break;

            int slotIndex = spawnOrder[i];
            Transform slotTransform = enemySlots[slotIndex];

            if (slotTransform == null) continue;

            GameObject spawnedEnemy = Instantiate(prefabsToInstantiate[i], slotTransform.position, slotTransform.rotation);

            result.EnemyPositions[slotIndex] = slotTransform.gameObject;
            result.EnemiesList[slotIndex] = spawnedEnemy;
            result.AllActiveFighters.Add(spawnedEnemy);
        }

        // 4. CLEANUP OVERWORLD INITIATOR IF APPLICABLE
        if (data.InitiatorWorldObject != null)
        {
            Destroy(data.InitiatorWorldObject);
        }

        return result;
    }

    /// <summary>
    /// Calculates slot indices order starting from center and expanding outwards symmetrically.
    /// Example for 3 slots: [1, 0, 2]
    /// Example for 7 slots: [3, 2, 4, 1, 5, 0, 6]
    /// </summary>
    private List<int> CalculateSpawnSlotsOrder(int centerIndex, int maxSlots)
    {
        List<int> order = new List<int> { centerIndex };

        for (int offset = 1; order.Count < maxSlots; offset++)
        {
            int left = centerIndex - offset;
            int right = centerIndex + offset;

            if (left >= 0) order.Add(left);
            if (right < maxSlots) order.Add(right);
        }

        return order;
    }
}