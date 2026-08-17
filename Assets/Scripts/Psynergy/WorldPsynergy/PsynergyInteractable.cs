using UnityEngine;

public class PsynergyInteractable : MonoBehaviour, IPsynergyTargetable
{
    [Header("Psynergy Configuration")]
    [Tooltip("The WorldAction required for this object to react.")]
    [SerializeField] private WorldActionType requiredWorldAction = WorldActionType.Catch;
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

    public bool IsCompatibleWith(PsynergySO psynergy)
    {
        if (hasBeenUsed || psynergy == null) return false;

        // Fast integer enum comparison
        return psynergy.WorldData.CanBeUsedInWorld &&
               psynergy.WorldData.WorldAction == requiredWorldAction;
    }

    public bool ExecutePsynergy(PsynergySO psynergy, GameObject caster)
    {
        if (!IsCompatibleWith(psynergy))
        {
            Debug.Log($"[PsynergyInteractable] {gameObject.name} is not compatible with {psynergy?.PsynergyName}!");
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

    protected virtual void OnPsynergyTriggered(PsynergySO psynergy, GameObject caster)
    {
        Debug.Log($"[PsynergyInteractable] Successfully applied {psynergy.PsynergyName} on {gameObject.name}!");

        // Action-based logic using Enum
        switch (psynergy.WorldData.WorldAction)
        {
            case WorldActionType.Catch:
                // TODO: Animate item flying to player, add item to inventory, etc.
                gameObject.SetActive(false);
                break;

            case WorldActionType.Move:
                // TODO: Move block logic
                break;

            case WorldActionType.Frost:
                // TODO: Freeze water puddle logic
                break;
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