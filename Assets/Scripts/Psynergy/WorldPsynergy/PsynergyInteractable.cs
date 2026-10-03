using UnityEngine;

/// <summary>
/// Base class for all world objects that react to Psynergy abilities (e.g., Move, Frost, Growth, Catch).
/// Implements IPsynergyTargetable and utilizes MaterialPropertyBlocks for zero-allocation visual feedback.
/// </summary>
public class PsynergyInteractable : MonoBehaviour, IPsynergyTargetable
{
    #region Serialized Fields

    [Header("Psynergy Configuration")]
    [Tooltip("The WorldAction required for this object to react to cast Psynergy.")]
    [SerializeField] private WorldActionType requiredWorldAction = WorldActionType.Catch;
    [SerializeField] private bool singleUse = true;

    [Header("Visual Feedback Settings")]
    [SerializeField] private Renderer objRenderer;
    [SerializeField] private Color highlightColor = Color.yellow;
    [Tooltip("Shader property name used for the outline or glow effect.")]
    [SerializeField] private string shaderColorPropertyName = "_BaseColor";

    #endregion

    #region Private Runtime State

    private MaterialPropertyBlock propertyBlock;
    private int colorPropertyID;
    private Color originalColor = Color.white;
    private bool hasBeenUsed;
    private bool isProximityActive;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the transform component of this interactable target.
    /// </summary>
    public Transform Transform => transform;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        if (objRenderer == null)
        {
            objRenderer = GetComponent<Renderer>();
        }

        propertyBlock = new MaterialPropertyBlock();
        colorPropertyID = Shader.PropertyToID(shaderColorPropertyName);

        if (objRenderer != null && objRenderer.sharedMaterial != null)
        {
            if (objRenderer.sharedMaterial.HasProperty(colorPropertyID))
            {
                originalColor = objRenderer.sharedMaterial.GetColor(colorPropertyID);
            }
        }
    }

    #endregion

    #region IPsynergyTargetable Implementation

    public bool CanBeTargeted()
    {
        return !hasBeenUsed;
    }

    public bool IsCompatibleWith(PsynergySO psynergy)
    {
        if (hasBeenUsed || psynergy == null) return false;

        return psynergy.WorldData.CanBeUsedInWorld &&
               psynergy.WorldData.WorldAction == requiredWorldAction;
    }

    public bool ExecutePsynergy(PsynergySO psynergy, GameObject caster)
    {
        if (!IsCompatibleWith(psynergy))
        {
            Debug.LogWarning($"[PsynergyInteractable] {gameObject.name} is not compatible with {psynergy?.PsynergyName}!");
            return false;
        }

        OnPsynergyTriggered(psynergy, caster);

        if (singleUse)
        {
            hasBeenUsed = true;
            OnProximityExited();
            OnTargetLost();
        }

        return true;
    }

    #endregion

    #region Proximity & Visual Feedback (Shader Highlights)

    public void OnProximityEntered()
    {
        if (hasBeenUsed || isProximityActive) return;

        isProximityActive = true;
        ApplyColorProperty(highlightColor);
    }

    public void OnProximityExited()
    {
        if (!isProximityActive) return;

        isProximityActive = false;
        ApplyColorProperty(originalColor);
    }

    #endregion

    #region Aiming & Lock-On States

    public void OnTargetGained()
    {
        if (hasBeenUsed) return;

        // Enhance focus color or trigger reticle lock-on effect
        ApplyColorProperty(highlightColor * 1.25f);
    }

    public void OnTargetLost()
    {
        // Revert to proximity color if player is still nearby, otherwise restore original
        ApplyColorProperty(isProximityActive ? highlightColor : originalColor);
    }

    #endregion

    #region Protected Interaction Methods

    /// <summary>
    /// Executes the specific action logic when a compatible Psynergy is applied.
    /// Override this method in child classes for custom puzzle mechanics (e.g., PushBlock, FreezeWater).
    /// </summary>
    protected virtual void OnPsynergyTriggered(PsynergySO psynergy, GameObject caster)
    {
        Debug.Log($"[PsynergyInteractable] Applied {psynergy.PsynergyName} on {gameObject.name}.");

        switch (psynergy.WorldData.WorldAction)
        {
            case WorldActionType.Catch:
                // TODO: Play flying item animation towards player and add to inventory
                gameObject.SetActive(false);
                break;

            case WorldActionType.Move:
                // TODO: Trigger grid-based movement logic
                break;

            case WorldActionType.MindRead:
                // TODO: Specific mind read dialogues
                break;
        }
    }

    #endregion

    #region Private Helpers

    private void ApplyColorProperty(Color targetColor)
    {
        if (objRenderer == null) return;

        objRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorPropertyID, targetColor);
        objRenderer.SetPropertyBlock(propertyBlock);
    }

    #endregion
}