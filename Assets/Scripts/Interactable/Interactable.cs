using UnityEngine;

/// <summary>
/// Defines the directional restriction type required to interact with an object.
/// </summary>
public enum InteractionDirectionType
{
    AnyDirection,
    FrontalCone,
    ExactFacing
}

/// <summary>
/// Base class for all world objects that can be interacted with by the player (chests, NPCs, doors, etc.).
/// Provides pure interaction properties, delegating UI positioning entirely to WorldSpaceUIProvider.
/// </summary>
public class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string actionText = "Interact";
    [SerializeField] private string keyPrompt = "E";
    [SerializeField] private InteractionDirectionType directionType = InteractionDirectionType.AnyDirection;

    #region Properties

    /// <summary>
    /// Gets the contextual action description (e.g., "Open", "Talk", "Examine").
    /// </summary>
    public string ActionText => actionText;

    /// <summary>
    /// Gets the input key assigned to trigger this interaction.
    /// </summary>
    public string KeyPrompt => keyPrompt;

    /// <summary>
    /// Gets the directional restriction type required for this interaction.
    /// </summary>
    public InteractionDirectionType DirectionType => directionType;

    #endregion

    #region Public Methods

    /// <summary>
    /// Virtual method triggered when the player presses the interaction button while targeting this object.
    /// Override in derived classes (e.g., ChestInteractable, NPCInteractable).
    /// </summary>
    public virtual void Interact()
    {
        // To be overridden in concrete subclasses
    }

    #endregion
}