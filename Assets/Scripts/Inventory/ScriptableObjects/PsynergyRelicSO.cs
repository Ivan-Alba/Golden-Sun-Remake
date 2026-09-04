using System.Reflection;
using UnityEngine;

/// <summary>
/// Represents a specialized equipment piece (Psynergy Relic / Gem) that grants field 
/// or combat Psynergies (e.g. Catch Gem, Force Gem, Lift Gem) to the hero while equipped.
/// Inherits from EquipableSO to integrate seamlessly with equipment slots, stats, and audio feedback.
/// Automatically enforces non-salable economy rules in the editor.
/// </summary>
[CreateAssetMenu(fileName = "Relic_", menuName = "GoldenSun/Items/Psynergy Relic")]
public class PsynergyRelicSO : EquipableSO
{
    #region Relic Granted Abilities

    [Header("Relic Granted Abilities")]
    [Tooltip("Reference to the Psynergy spell/ability granted to the host hero while this relic remains equipped.")]
    [SerializeField] private PsynergySO grantedPsynergy;

    #endregion

    #region Public Properties

    public PsynergySO GrantedPsynergy => grantedPsynergy;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 1. Enforce EquipmentSlot.PsynergyRelic automatically in inspector
        FieldInfo slotProp = typeof(EquipableSO).GetField("equipmentSlot", BindingFlags.NonPublic | BindingFlags.Instance);
        if (slotProp != null && EquipmentSlot != EquipmentSlot.PsynergyRelic)
        {
            slotProp.SetValue(this, EquipmentSlot.PsynergyRelic);
        }

        // 2. Enforce EquipmentSubtype.Relic automatically in inspector
        FieldInfo subtypeProp = typeof(EquipableSO).GetField("equipmentSubtype", BindingFlags.NonPublic | BindingFlags.Instance);
        if (subtypeProp != null && EquipmentSubtype != EquipmentSubtype.Relic)
        {
            subtypeProp.SetValue(this, EquipmentSubtype.Relic);
        }

        // 3. Enforce Non-Salable and Non-Buyable rules for Psynergy Relics (ItemSO fields)
        FieldInfo canSellProp = typeof(ItemSO).GetField("canSell", BindingFlags.NonPublic | BindingFlags.Instance);
        if (canSellProp != null)
        {
            canSellProp.SetValue(this, false);
        }

        FieldInfo buyPriceProp = typeof(ItemSO).GetField("buyPrice", BindingFlags.NonPublic | BindingFlags.Instance);
        if (buyPriceProp != null)
        {
            buyPriceProp.SetValue(this, 0);
        }

        FieldInfo sellPriceProp = typeof(ItemSO).GetField("sellPrice", BindingFlags.NonPublic | BindingFlags.Instance);
        if (sellPriceProp != null)
        {
            sellPriceProp.SetValue(this, 0);
        }
    }
#endif

    #endregion
}