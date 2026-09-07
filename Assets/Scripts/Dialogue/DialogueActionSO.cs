using UnityEngine;

/// <summary>
/// Base class for modular dialogue actions. 
/// Allows ScriptableObject-driven events to be triggered from dialogue choices without scene references.
/// </summary>
public abstract class DialogueActionSO : ScriptableObject
{
    public abstract void ExecuteAction();
}

[CreateAssetMenu(fileName = "Action_OpenSaveBook", menuName = "GoldenSun/Dialogue/Actions/Open Save Book")]
public class OpenSaveBookActionSO : DialogueActionSO
{
    [SerializeField] private GameEvents.UI.SaveBookMode mode = GameEvents.UI.SaveBookMode.Save;

    public override void ExecuteAction()
    {
        GameEvents.UI.RequestOpenSaveLoadBook(mode);
    }
}

[CreateAssetMenu(fileName = "Action_ChangeScene", menuName = "GoldenSun/Dialogue/Actions/Change Scene")]
public class ChangeSceneActionSO : DialogueActionSO
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string spawnID;

    public override void ExecuteAction()
    {
        // Ejemplo de llamada a tu sistema de escenas global
        GameEvents.Scene.RequestChangeScene(targetSceneName, spawnID);
    }
}

