using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New_PsynergyRelic", menuName = "Inventory/Items/Psynergy Relic")]
public class PsynergyRelicSO : ItemSO
{
    [Header("Relic Granted Abilities")]
    [SerializeField] private Psynergy grantedPsynergy; // Granted field/battle Psynergy (e.g., Catch, Carry, Cloak)

    [Header("Optional Relic Stats")]
    [SerializeField] private List<EquipmentStatBonus> statModifiers = new List<EquipmentStatBonus>();

    private void OnValidate()
    {
        // Enforce that Psynergy Relics always belong to the dedicated slot
        // and belong to the PsynergyRelic ItemType
    }

    #region Properties

    public EquipmentSlot EquipmentSlot => EquipmentSlot.PsynergyRelic;
    public Psynergy GrantedPsynergy => grantedPsynergy;
    public IReadOnlyList<EquipmentStatBonus> StatModifiers => statModifiers;

    #endregion
}