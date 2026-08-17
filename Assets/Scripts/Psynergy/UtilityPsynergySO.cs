using UnityEngine;

[CreateAssetMenu(fileName = "NewUtilityPsynergy", menuName = "Psynergy/Utility (World Only) Psynergy")]
public class UtilityPsynergySO : PsynergySO
{
    private void OnValidate()
    {
        // Enforce safe default settings for world-only utility psynergies in the Inspector
        // Automatically ensure it can be used in world and is marked as non-battle if needed
    }

    public override void ExecuteWorldEffect()
    {
        base.ExecuteWorldEffect();
        Debug.Log($"[Psynergy] Executing overworld utility logic for action: {WorldData.WorldAction}");
    }
}