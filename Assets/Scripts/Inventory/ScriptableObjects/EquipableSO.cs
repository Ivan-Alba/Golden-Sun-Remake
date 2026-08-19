using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct EquipmentStatBonus
{
    public StatType statType;
    public float value;
    public StatModifierType modifierType; // Flat, PercentBase, PercentTotal
}

[CreateAssetMenu(fileName = "New_Equipable", menuName = "Inventory/Items/Equipable Standard")]
public class EquipableSO : ItemSO
{
    [Header("Equipment Slot & Stats")]
    [SerializeField] private EquipmentSlot equipmentSlot = EquipmentSlot.Armor;
    [SerializeField] private List<EquipmentStatBonus> statModifiers = new List<EquipmentStatBonus>();

    [Header("Elemental & Combat Modifiers")]
    [SerializeField] private ElementalType element = ElementalType.None;
    [SerializeField] private int elementalPowerBonus = 0;

    [Header("Active Item Use in Battle (Anillos, Varas Usables)")]
    [SerializeField] private bool useInBattle = false;
    [SerializeField] private ItemBattleEffectSO battleUseEffect = null;

    #region Public Properties

    public EquipmentSlot EquipmentSlot => equipmentSlot;
    public IReadOnlyList<EquipmentStatBonus> StatModifiers => statModifiers;
    public ElementalType Element => element;
    public int ElementalPowerBonus => elementalPowerBonus;

    public bool UseInBattle => useInBattle;
    public ItemBattleEffectSO BattleUseEffect => battleUseEffect;

    #endregion
}