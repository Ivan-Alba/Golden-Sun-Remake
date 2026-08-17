using System;
using System.Collections.Generic;
using UnityEngine;

#region Enums & Structs

/// <summary>
/// Defines the targeting behavior for Psynergies in combat and menu.
/// </summary>
public enum PsynergyTargetType
{
    Self,
    SingleEnemy,
    MultiEnemyArea,
    AllEnemies,
    SingleAlly,
    AllAllies,
    None
}

public enum WorldActionType
{
    None,
    Catch,      // Retrieve distant items or objects
    Move,       // Push or pull pillars and blocks
    Frost,      // Freeze water puddles into ice pillars
    MindRead,   // Read minds of NPCs and animals
    Reveal,     // Reveal hidden objects, doors, and pathways
    Force,      // Strike distant objects like trunks or trees
    Lift,       // Lift round boulders
    Douse,      // Douse fires or fill containers with water
    Growth,     // Grow plants and vines to climb
    Pound,      // Pound wooden stakes or pillars into the ground
    Whirlwind,  // Clear bushes, vines, or rustle leaves
    Cyclone,    // Ride updrafts or cyclone spots
    Hover,      // Float over specialized hover tiles
    Blaze,      // Ignite torches or create flames
    Sand,       // Melt into sand to travel under obstacles
    Parch,      // Evaporate large bodies of standing water
    Grind,      // Grind rocks or hard obstacles
    Teleport    // Teleport between active teleport pads
}

/// <summary>
/// Encapsulates metadata and assets required for overworld map interactions.
/// </summary>
[Serializable]
public struct WorldInteractionData
{
    [SerializeField] private bool canBeUsedInWorld;
    [SerializeField] private WorldActionType worldAction;
    [Tooltip("Visual effect prefab instantiated when executing this Psynergy in the overworld.")]
    [SerializeField] private GameObject worldVfxPrefab;
    [Tooltip("Audio clip played when executing this Psynergy in the overworld.")]
    [SerializeField] private AudioClip worldSoundEffect;

    public bool CanBeUsedInWorld => canBeUsedInWorld;
    public WorldActionType WorldAction => worldAction;
    public GameObject WorldVfxPrefab => worldVfxPrefab;
    public AudioClip WorldSoundEffect => worldSoundEffect;

    public WorldInteractionData(bool canBeUsedInWorld, WorldActionType worldAction, GameObject worldVfxPrefab = null, AudioClip worldSoundEffect = null)
    {
        this.canBeUsedInWorld = canBeUsedInWorld;
        this.worldAction = worldAction;
        this.worldVfxPrefab = worldVfxPrefab;
        this.worldSoundEffect = worldSoundEffect;
    }
}

/// <summary>
/// Encapsulates metadata and assets required for Pause/Party Menu usage out of combat.
/// </summary>
[Serializable]
public struct MenuInteractionData
{
    [SerializeField] private bool canBeUsedInMenu;
    [Tooltip("Optional audio clip played when this Psynergy is cast from the party menu (e.g., healing chime).")]
    [SerializeField] private AudioClip menuSoundEffect;
    [Tooltip("Optional UI VFX or screen overlay prefab instantiated when cast from the menu.")]
    [SerializeField] private GameObject menuVfxPrefab;

    public bool CanBeUsedInMenu => canBeUsedInMenu;
    public AudioClip MenuSoundEffect => menuSoundEffect;
    public GameObject MenuVfxPrefab => menuVfxPrefab;

    public MenuInteractionData(bool canBeUsedInMenu, AudioClip menuSoundEffect = null, GameObject menuVfxPrefab = null)
    {
        this.canBeUsedInMenu = canBeUsedInMenu;
        this.menuSoundEffect = menuSoundEffect;
        this.menuVfxPrefab = menuVfxPrefab;
    }
}

#endregion

public abstract class PsynergySO : ScriptableObject
{
    [Header("Core Identification")]
    [SerializeField] private string psynergyName;
    [SerializeField] private string slugName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Cost & Element")]
    [SerializeField] private int ppCost;
    [SerializeField] private ElementalType element = ElementalType.None;

    [Header("Battle Rules")]
    [SerializeField] private bool isBattlePsynergy = true;
    [SerializeField] private PsynergyTargetType targetType = PsynergyTargetType.SingleEnemy;
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private AudioClip soundEffect;

    [Header("Area & Spread Rules")]
    [Tooltip("Number of additional targets affected around the primary target (e.g., 0 = single target, 1 = 3 targets total, 2 = 5 targets total).")]
    [SerializeField] private int areaRange = 0;
    [Tooltip("If true, damage or effect potency diminishes from the main target towards outer targets (Golden Sun area damage formula).")]
    [SerializeField] private bool hasDiminishingSpread = true;

    [Header("World Rules")]
    [SerializeField] private WorldInteractionData worldData;

    [Header("Menu Rules")]
    [SerializeField] private MenuInteractionData menuData;

    #region Public Properties

    public string PsynergyName => psynergyName;
    public string SlugName => slugName;
    public string Description => description;
    public Sprite Icon => icon;
    public int PpCost => ppCost;
    public ElementalType Element => element;

    // Battle Properties
    public bool IsBattlePsynergy => isBattlePsynergy;
    public PsynergyTargetType TargetType => targetType;
    public GameObject VfxPrefab => vfxPrefab;
    public AudioClip SoundEffect => soundEffect;
    public int AreaRange => areaRange;
    public bool HasDiminishingSpread => hasDiminishingSpread;

    // Context Data
    public WorldInteractionData WorldData => worldData;
    public MenuInteractionData MenuData => menuData;

    // Computed UI Shortcut Property
    public Sprite ElemIcon => PsynergyDatabase.Instance != null ? PsynergyDatabase.Instance.GetElementIcon(element) : null;

    #endregion

    #region Execution Methods

    /// <summary>
    /// Executes the combat logic for this Psynergy.
    /// Derived classes (Attack, Heal, Support) override this method to supply specific logic.
    /// </summary>
    public virtual void ExecuteBattleEffect(FighterStats caster, List<FighterStats> targets)
    {
        if (!isBattlePsynergy)
        {
            Debug.LogWarning($"[Psynergy] '{psynergyName}' is a non-battle Psynergy and cannot be executed in combat.");
        }
    }

    /// <summary>
    /// Executes overworld interaction logic if applicable.
    /// </summary>
    public virtual void ExecuteWorldEffect()
    {
        if (!worldData.CanBeUsedInWorld)
        {
            Debug.LogWarning($"[Psynergy] '{psynergyName}' cannot be used in the overworld.");
            return;
        }

        Debug.Log($"[Psynergy] Triggering world interaction: {worldData.WorldAction}");
    }

    /// <summary>
    /// Executes pause/party menu logic out of combat if applicable.
    /// </summary>
    public virtual void ExecuteMenuEffect(FighterStats caster, List<FighterStats> targets)
    {
        if (!menuData.CanBeUsedInMenu)
        {
            Debug.LogWarning($"[Psynergy] '{psynergyName}' cannot be executed from the party menu.");
            return;
        }

        Debug.Log($"[Psynergy] Executing menu action for '{psynergyName}'.");
    }

    #endregion
}