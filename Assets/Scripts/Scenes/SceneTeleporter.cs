using UnityEngine;

public class SceneTeleporter : MonoBehaviour
{
    [Header("Destination Settings")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnID;

    private bool isTeleporting = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTeleporting) return;

        // Check if player enters trigger
        if (other.CompareTag("Player"))
        {
            isTeleporting = true;
            GameManager.Instance.ChangeScene(targetSceneName, targetSpawnID);
        }
    }
}