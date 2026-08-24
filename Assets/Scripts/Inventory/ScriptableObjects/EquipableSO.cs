using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New_Equipable", menuName = "Inventory/Items/Equipable Standard")]
public class EquipableSO : ItemSO
{
    [Header("Equipment Slot & Stats")]
    [SerializeField] private EquipmentSlot equipmentSlot = EquipmentSlot.Armor;

    [Tooltip("Lista unificada de modificadores de estadísticas (fijos o porcentuales).")]
    [SerializeField] private List<StatBonus> statModifiers = new List<StatBonus>();

    [Header("Elemental & Combat Modifiers")]
    [SerializeField] private ElementalType element = ElementalType.None;
    [SerializeField] private int elementalPowerBonus = 0;

    [Header("Active Item Use in Battle (Anillos, Varas Usables)")]
    [SerializeField] private bool useInBattle = false;
    [SerializeField] private ItemBattleEffectSO battleUseEffect = null;

    #region Public Properties

    public EquipmentSlot EquipmentSlot => equipmentSlot;
    public IReadOnlyList<StatBonus> StatModifiers => statModifiers;
    public ElementalType Element => element;
    public int ElementalPowerBonus => elementalPowerBonus;

    public bool UseInBattle => useInBattle;
    public ItemBattleEffectSO BattleUseEffect => battleUseEffect;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Devuelve los StatModifiers generados por esta pieza de equipo, listos para inyectar en Stat.cs o StatCalculator.
    /// </summary>
    public List<(StatType statType, StatModifier modifier)> GetStatModifiers()
    {
        var result = new List<(StatType, StatModifier)>();
        if (statModifiers == null) return result;

        for (int i = 0; i < statModifiers.Count; i++)
        {
            if (statModifiers[i] != null)
            {
                result.Add((statModifiers[i].statType, statModifiers[i].ToStatModifier(this, isTemporary: false)));
            }
        }
        return result;
    }

    #endregion
}