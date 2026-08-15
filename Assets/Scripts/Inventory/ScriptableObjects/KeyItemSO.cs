using UnityEngine;

[CreateAssetMenu(fileName = "New_KeyItem", menuName = "Inventory/Items/Key Item")]
public class KeyItemSO : ItemSO
{
    [Header("Quest & Story Context")]
    [SerializeField] private string questId;
    [SerializeField] private bool isStoryMandatory = false;

    #region Properties

    public string QuestId => questId;
    public bool IsStoryMandatory => isStoryMandatory;

    #endregion
}