using UnityEngine;

[CreateAssetMenu(fileName = "Action_ChangeScene", menuName = "GoldenSun/Dialogue/Actions/Change Scene")]
public class ChangeSceneActionSO : DialogueActionSO
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string spawnID;

    public override void ExecuteAction()
    {
        GameEvents.Scene.RequestChangeScene(targetSceneName, spawnID);
    }
}