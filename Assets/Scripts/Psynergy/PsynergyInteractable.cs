using System;
using UnityEngine;

public class PsynergyInteractable : MonoBehaviour, IPsynergyTargetable
{
    [Header("Psynergy Configuration")]
    [Tooltip("The SlugName of the required Psynergy in JSON (e.g., 'catch', 'move', 'frost')")]
    [SerializeField] private string requiredPsynergySlug = "catch";
    [SerializeField] private bool singleUse = true;

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private Renderer objRenderer;
    [SerializeField] private Color highlightColor = Color.yellow;

    private Color originalColor;
    private bool hasBeenUsed;

    public Transform Transform => transform;

    private void Awake()
    {
        if (objRenderer == null)
        {
            objRenderer = GetComponent<Renderer>();
        }

        if (objRenderer != null)
        {
            originalColor = objRenderer.material.color;
        }
    }

    public bool CanBeTargeted()
    {
        return !hasBeenUsed;
    }

    public bool IsCompatibleWith(Psynergy psynergy)
    {
        if (hasBeenUsed || psynergy == null) return false;

        // Verify it's a world Psynergy and matches the JSON SlugName
        return psynergy.IsWorldPsynergy &&
               string.Equals(psynergy.SlugName, requiredPsynergySlug, StringComparison.OrdinalIgnoreCase);
    }

    public bool ExecutePsynergy(Psynergy psynergy, GameObject caster)
    {
        if (!IsCompatibleWith(psynergy))
        {
            Debug.Log($"[PsynergyInteractable] {gameObject.name} is not compatible with {psynergy?.Name}!");
            return false;
        }

        OnPsynergyTriggered(psynergy, caster);

        if (singleUse)
        {
            hasBeenUsed = true;
            OnTargetLost();
        }

        return true;
    }

    protected virtual void OnPsynergyTriggered(Psynergy psynergy, GameObject caster)
    {
        Debug.Log($"[PsynergyInteractable] Successfully applied {psynergy.Name} on {gameObject.name}!");

        // Specific reaction logic based on the SlugName
        if (string.Equals(psynergy.SlugName, "catch", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: Animate item flying to player, add item to inventory, etc.
            gameObject.SetActive(false);
        }
    }

    public void OnTargetGained()
    {
        if (hasBeenUsed || objRenderer == null) return;
        objRenderer.material.color = highlightColor;
    }

    public void OnTargetLost()
    {
        if (objRenderer == null) return;
        objRenderer.material.color = originalColor;
    }
}