using System.Collections;
using UnityEngine;
using static GameManager;

public class SceneTeleporter : MonoBehaviour
{
    #region Serialized Fields

    [Header("Destination Settings")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnID;

    [Header("Auto-Walk Sequence Settings")]
    [SerializeField] private Transform walkTarget;
    [SerializeField] private float sequenceDuration = 1.2f;

    [Header("Ambient Fade Settings")]
    [Range(0f, 1f)][SerializeField] private float targetOverlayAlpha = 1f;
    [SerializeField] private float fadeDuration = 1.5f;

    #endregion

    #region Private Fields

    private bool isTeleporting = false;

    #endregion

    #region Trigger Events

    private void OnTriggerEnter(Collider other)
    {
        if (isTeleporting) return;

        if (other.CompareTag("Player"))
        {
            isTeleporting = true;
            StartCoroutine(TeleportSequenceRoutine(other.gameObject));
        }
    }

    #endregion

    #region Sequence Coroutine

    private IEnumerator TeleportSequenceRoutine(GameObject playerObject)
    {
        // 1. Lock controls and camera movement
        GameManager.Instance.SetGameState(GameState.Cutscene);

        // 2. Order player to walk towards the target point if defined
        if (walkTarget != null && playerObject.TryGetComponent<PlayerController>(out PlayerController playerController))
        {
            playerController.MoveToDestination(walkTarget.position);
        }

        // 3. Start progressive ambient fade to dark (NOT the full logo curtain yet)
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.FadeToBlack(targetOverlayAlpha, fadeDuration);
        }

        // 4. Wait for the sequence duration (player walking + screen darkening)
        yield return new WaitForSeconds(sequenceDuration);

        // 5. Call regular ChangeScene, which triggers the usual logo curtain transition
        GameManager.Instance.ChangeScene(targetSceneName, targetSpawnID);
    }

    #endregion

    #region Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (walkTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(walkTarget.position, 0.3f);
            Gizmos.DrawLine(transform.position, walkTarget.position);
        }
    }
#endif

    #endregion
}