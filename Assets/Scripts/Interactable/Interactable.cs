using UnityEngine;

/// <summary>
/// Base class for all world objects that can be interacted with by the player (chests, NPCs, doors, etc.).
/// Provides interaction properties and world-space UI anchor positioning.
/// </summary>
public class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string actionText = "Interact";
    [SerializeField] private string keyPrompt = "E";

    [Header("UI Anchor & Positioning")]
    [Tooltip("Optional Transform to lock the World-Prompt position. If null, uses the object's transform with promptOffset.")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.2f, 0f);

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
    /// Calculates and returns the exact 3D world position where the UI Toolkit World-Prompt should float.
    /// </summary>
    public Vector3 PromptWorldPosition
    {
        get
        {
            if (promptAnchor != null)
            {
                return promptAnchor.position;
            }
            return transform.position + promptOffset;
        }
    }

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

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Visualizes the UI prompt floating position directly inside the Unity Scene View
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(PromptWorldPosition, 0.15f);
    }

    #endregion
}