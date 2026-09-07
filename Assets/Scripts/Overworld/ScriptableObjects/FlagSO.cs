using UnityEngine;
using UnityEngine.Localization;

public enum FlagCategory
{
    General,
    Story,
    ChestOpened,
    DoorUnlocked,
    NPCState,
    PuzzleSolved
}

/// <summary>
/// ScriptableObject representing a single boolean world flag or state variable.
/// Used to decouple puzzle states, opened chests, story triggers, and NPC dialogues.
/// Can optionally contain chronicle/journal metadata for the Save Book & Codex UI.
/// </summary>
[CreateAssetMenu(fileName = "Flag_", menuName = "GoldenSun/World/Flag Asset")]
public class FlagSO : ScriptableObject
{
    #region Serialized Fields

    [Header("Flag Identification")]
    [Tooltip("Unique string identifier used for database lookups and save data transactions.")]
    [SerializeField] private string flagSlug = "flag_chest_01";

    [Tooltip("Category classification for editor organization and filtering.")]
    [SerializeField] private FlagCategory category = FlagCategory.General;

    [TextArea(2, 4)]
    [Tooltip("Internal developer description of what this flag controls.")]
    [SerializeField] private string description;

    [Header("Default State")]
    [Tooltip("Default state on new game creation.")]
    [SerializeField] private bool defaultValue = false;

    [Header("Chronicle & Journal Metadata (Optional / Story Flags)")]
    [Tooltip("If true, activating this flag updates the main chronicle page in the Save Book / Codex.")]
    [SerializeField] private bool updatesChronicle = false;

    [Tooltip("Main chapter title (e.g., 'Capítulo II: El Viaje de los Adeptos'). Can be left empty if remaining in current chapter.")]
    [SerializeField] private LocalizedString chapterTitle;

    [Tooltip("Specific milestone/event title (e.g., 'El Robo en Vault', 'El Rescate en Imil').")]
    [SerializeField] private LocalizedString milestoneTitle;

    [Tooltip("Key artwork or location illustration for the left page of the Save Book.")]
    [SerializeField] private Sprite chronicleIllustration;

    [Tooltip("Summary narrative text describing the events that occurred up to this milestone.")]
    [SerializeField] private LocalizedString chronicleSummaryText;

    #endregion

    #region Public Properties

    public string FlagSlug => flagSlug;
    public FlagCategory Category => category;
    public string Description => description;
    public bool DefaultValue => defaultValue;

    // Chronicle Properties
    public bool UpdatesChronicle => updatesChronicle;
    public LocalizedString ChapterTitle => chapterTitle;
    public LocalizedString MilestoneTitle => milestoneTitle;
    public Sprite ChronicleIllustration => chronicleIllustration;
    public LocalizedString ChronicleSummaryText => chronicleSummaryText;

    #endregion

    #region Unity Editor Validation

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(flagSlug))
        {
            flagSlug = name.Replace("Flag_", string.Empty).ToLowerInvariant();
        }
    }

    #endregion
}