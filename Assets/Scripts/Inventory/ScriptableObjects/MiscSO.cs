using UnityEngine;

[CreateAssetMenu(fileName = "New_MiscItem", menuName = "Inventory/Items/Misc Item")]
public class MiscSO : ItemSO
{
    [Header("Minigame or Event Settings")]
    [SerializeField] private bool isMinigameToken = false; // E.g., Game Tickets, Lucky Medals

    #region Properties

    public bool IsMinigameToken => isMinigameToken;

    #endregion
}