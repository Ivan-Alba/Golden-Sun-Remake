using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointID;

    public string SpawnPointID => spawnPointID;

    // Draw visual helper in Editor
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}