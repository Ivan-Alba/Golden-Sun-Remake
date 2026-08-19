using UnityEngine;

/// <summary>
/// Represents a special equipment piece (Psynergy Relic) that grants field or battle Psynergies when equipped.
/// Inherits from EquipableSO to leverage equipment stat modifiers and slot rules seamlessly.
/// </summary>
[CreateAssetMenu(fileName = "New_PsynergyRelic", menuName = "Inventory/Items/Psynergy Relic")]
public class PsynergyRelicSO : EquipableSO
{
    [Header("Relic Granted Abilities")]
    [Tooltip("Reference to the Psynergy granted to the hero while this relic is equipped.")]
    [SerializeField] private PsynergySO grantedPsynergy;

    private void OnValidate()
    {
        // Ensure Psynergy Relics are permanently configured for the Relic Equipment Slot and ItemType
        // Note: Set equipmentSlot to EquipmentSlot.PsynergyRelic in inspector
    }

    #region Properties

    public PsynergySO GrantedPsynergy => grantedPsynergy;

    #endregion
}