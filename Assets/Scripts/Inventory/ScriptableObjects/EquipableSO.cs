using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct EquipmentStatBonus
{
    public StatType statType;
    public float value;
    public StatModifierType modifierType; // Flat, PercentBase, PercentTotal
}

[CreateAssetMenu(fileName = "New_Equipable", menuName = "Inventory/Items/Equipable")]
public class EquipableSO : ItemSO
{
    [Header("Equipment Slot & Stats")]
    [SerializeField] private EquipmentSlot equipmentSlot = EquipmentSlot.Weapon;
    [SerializeField] private List<EquipmentStatBonus> statModifiers = new List<EquipmentStatBonus>();

    [Header("Combat Bonuses")]
    [SerializeField] private int criticalRateBonus = 0;
    [SerializeField] private ElementalType element = ElementalType.None;
    [SerializeField] private int elementalPowerBonus = 0;

    [Header("Battle Usage (Artifact Skill)")]
    [SerializeField] private bool useInBattle = false;
    [SerializeField] private Psynergy battleUsePsynergy; // Reference to Psynergy invoked when used as item in battle

    #region Properties

    public EquipmentSlot EquipmentSlot => equipmentSlot;
    public IReadOnlyList<EquipmentStatBonus> StatModifiers => statModifiers;
    public int CriticalRateBonus => criticalRateBonus;
    public ElementalType Element => element;
    public int ElementalPowerBonus => elementalPowerBonus;
    public bool UseInBattle => useInBattle;
    public Psynergy BattleUsePsynergy => battleUsePsynergy;

    #endregion
}