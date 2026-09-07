using UnityEngine;

/// <summary>
/// Base class for all world objects that can be interacted with by the player (chests, NPCs, doors, etc.).
/// Provides pure interaction properties, delegating UI positioning entirely to WorldSpaceUIProvider.
/// </summary>
public class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string actionText = "Interact";
    [SerializeField] private string keyPrompt = "E";

    #region Properties

    /// <summary>
    /// Gets the contextual action description (e.g., "Open", "Talk", "Examine").
    /// </summary>
    public string ActionText => actionText;

    /// <summary>
    /// Gets the input key assigned to trigger this interaction.
    /// </summary>
    public string KeyPrompt => keyPrompt;

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