using UnityEngine;

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

    #endregion

    #region Public Properties

    public string FlagSlug => flagSlug;
    public FlagCategory Category => category;
    public string Description => description;
    public bool DefaultValue => defaultValue;

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