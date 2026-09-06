using System;
using System.Collections.Generic;

#region Save Header DTOs

/// <summary>
/// Lightweight preview DTO storing hero info for save slot selection UI cards.
/// </summary>
[Serializable]
public class HeroPreviewData
{
    public string heroName;
    public int level;

    public HeroPreviewData() { }

    public HeroPreviewData(string name, int lvl)
    {
        heroName = name;
        level = lvl;
    }
}

/// <summary>
/// Lightweight metadata header container used by UI to list save slots without loading full game states.
/// Includes encoded thumbnail image for modern UI save cards.
/// </summary>
[Serializable]
public class SaveHeaderData
{
    public int saveSlotIndex;
    public string saveTimestamp;
    public float totalTime;
    public int totalGold;
    public string locationName;
    public string saveThumbnailBase64;
    public List<HeroPreviewData> partyPreview = new List<HeroPreviewData>();
}

#endregion

#region World & Scene State DTOs

/// <summary>
/// Container holding deterministic player spawn positioning and target scene data.
/// </summary>
[Serializable]
public class WorldSceneStateData
{
    public string currentSceneName;
    public string spawnPointID;
    public float[] playerPosition = new float[3];
    public float[] playerRotation = new float[3];
    public string currentZoneName;
    public string currentRegionName;
    public bool isAutoSave;
}

#endregion

#region Party & Progression DTOs

/// <summary>
/// Container storing active party members, reserve/inactive heroes, and the current overworld leader hero.
/// </summary>
[Serializable]
public class PartyProgressionData
{
    public string activeLeaderHeroName;
    public List<string> partyMemberNames = new List<string>();
    public List<string> reserveHeroNames = new List<string>();
}

#endregion

#region Inventory & Equipment Data Transfer Objects (DTOs)

/// <summary>
/// Serializable DTO representing a single item slot in backpacks or global key bag.
/// </summary>
[Serializable]
public class InventorySlotData
{
    public int slotIndex;
    public string itemSlug;
    public int quantity;

    public InventorySlotData() { }

    public InventorySlotData(int index, string slug, int qty)
    {
        slotIndex = index;
        itemSlug = slug;
        quantity = qty;
    }
}

/// <summary>
/// Serializable DTO storing backpack slots for a specific party hero.
/// </summary>
[Serializable]
public class HeroInventoryData
{
    public string heroName;
    public List<InventorySlotData> slots = new List<InventorySlotData>();
}

/// <summary>
/// Serializable DTO for standard equipment slots (Weapon, Armor, Helm, Shield, Boots, Ring).
/// </summary>
[Serializable]
public class EquipmentSlotData
{
    public EquipmentSlot slotType;
    public string itemSlug;
}

/// <summary>
/// Serializable DTO storing equipped items and Psynergy Relics for a specific hero.
/// </summary>
[Serializable]
public class HeroEquipmentData
{
    public string heroName;
    public List<EquipmentSlotData> standardEquipment = new List<EquipmentSlotData>();
    public List<string> relicSlugs = new List<string>();
}

#endregion

#region Hero Stats & Psynergy DTOs

/// <summary>
/// Serializable DTO storing level and experience progression for a hero.
/// </summary>
[Serializable]
public class HeroProgressionData
{
    public string heroName;
    public int level;
    public int currentXp;
    public int nextLevelXp;
}

/// <summary>
/// Serializable DTO storing current Health (HP) and Psynergy Points (PP) for a hero.
/// </summary>
[Serializable]
public class HeroVitalStatusData
{
    public string heroName;
    public int currentHp;
    public int currentPp;
}

/// <summary>
/// Serializable DTO storing a single base stat entry without active modifiers or class multipliers.
/// </summary>
[Serializable]
public class StatBaseData
{
    public StatType statType;
    public float baseValue;

    public StatBaseData() { }

    public StatBaseData(StatType type, float baseVal)
    {
        statType = type;
        baseValue = baseVal;
    }
}

/// <summary>
/// Serializable DTO storing base stats for a specific hero.
/// </summary>
[Serializable]
public class HeroStatsContainerData
{
    public string heroName;
    public List<StatBaseData> baseStats = new List<StatBaseData>();
}

/// <summary>
/// Serializable DTO storing unlocked story/event Psynergies for a specific hero.
/// </summary>
[Serializable]
public class HeroStoryPsynergyData
{
    public string heroName;
    public List<string> unlockedStoryPsynergySlugs = new List<string>();
}

#region Golden Sun Djinn & World Flags DTOs

/// <summary>
/// Serializable DTO storing individual Djinn assignment, current operational state, and leader status.
/// </summary>
[Serializable]
public class DjinnSaveData
{
    public string djinnSlug;
    public string assignedHeroName;
    public DjinnState currentState;
    public int recoveryTurnsLeft;
    public bool isLeader;

    public DjinnSaveData() { }

    public DjinnSaveData(string slug, string hero, DjinnState state, int recoveryTurns = 0, bool leader = false)
    {
        djinnSlug = slug;
        assignedHeroName = hero;
        currentState = state;
        recoveryTurnsLeft = recoveryTurns;
        isLeader = leader;
    }
}

/// <summary>
/// Serializable Key-Value DTO for story, chest, and environmental flags.
/// </summary>
[Serializable]
public class GameFlagData
{
    public string key;
    public bool state;

    public GameFlagData() { }

    public GameFlagData(string key, bool state)
    {
        this.key = key;
        this.state = state;
    }
}

#endregion

/// <summary>
/// Serializable DTO storing assigned field Psynergy shortcuts (D-Pad / Hotline) for a hero.
/// </summary>
[Serializable]
public class HeroShortcutData
{
    public string heroName;
    public string[] shortcutSlugs = new string[4];
}

#endregion

/// <summary>
/// Root persistent data container used for serializing and deserializing game save files to disk (JSON).
/// </summary>
[Serializable]
public class GameData
{
    #region Meta & Header Info

    public SaveHeaderData header = new SaveHeaderData();

    #endregion

    #region World & Scene State

    public WorldSceneStateData worldState = new WorldSceneStateData();

    #endregion

    #region Party & General Progression

    public PartyProgressionData partyState = new PartyProgressionData();

    #endregion

    #region Inventory & Equipment Data

    public List<InventorySlotData> globalBagKeyItems = new List<InventorySlotData>();
    public List<HeroInventoryData> heroesInventories = new List<HeroInventoryData>();
    public List<HeroEquipmentData> heroesEquipment = new List<HeroEquipmentData>();

    #endregion

    #region Hero Stats & Psynergy Data

    public List<HeroStatsContainerData> heroesStats = new List<HeroStatsContainerData>();
    public List<HeroProgressionData> heroesProgression = new List<HeroProgressionData>();
    public List<HeroVitalStatusData> heroesVitals = new List<HeroVitalStatusData>();
    public List<HeroStoryPsynergyData> heroesStoryPsynergies = new List<HeroStoryPsynergyData>();
    public List<HeroShortcutData> heroesShortcuts = new List<HeroShortcutData>();

    #endregion

    #region Djinn & Story Flags

    public List<DjinnSaveData> collectedDjinn = new List<DjinnSaveData>();
    public List<GameFlagData> gameFlags = new List<GameFlagData>();

    #endregion

    #region Constructors

    public GameData()
    {
        // Default constructor for JSON deserialization
    }

    #endregion
}