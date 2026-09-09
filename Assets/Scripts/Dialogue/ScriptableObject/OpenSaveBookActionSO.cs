using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Action_OpenSaveBook", menuName = "GoldenSun/Dialogue/Actions/Open Save Book")]
public class OpenSaveBookActionSO : DialogueActionSO
{
    [SerializeField] private GameEvents.UI.SaveBookMode mode = GameEvents.UI.SaveBookMode.Save;

    public override bool RequiresWaiting => true;

    public override void ExecuteAction(Action onComplete)
    {
        Action closeListener = null;
        closeListener = () =>
        {
            SaveLoadBookUIController.OnBookClosed -= closeListener;
            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InDialogue);
            onComplete?.Invoke();
        };

        SaveLoadBookUIController.OnBookClosed += closeListener;
        GameEvents.UI.RequestOpenSaveLoadBook(mode);
    }

    public override void ExecuteAction()
    {
        GameEvents.UI.RequestOpenSaveLoadBook(mode);
    }
}