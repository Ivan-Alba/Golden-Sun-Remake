using System.Collections;
using UnityEngine;

/// <summary>
/// Handles trigger-based scene transitions, managing auto-walk sequences and progressive screen fades.
/// Decoupled from transition UI singletons while delegating scene change requests to the Event Bus.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTeleporter : MonoBehaviour
{
    #region Serialized Fields

    [Header("Destination Settings")]
    [Tooltip("Target scene name to load upon completing the teleport sequence.")]
    [SerializeField] private string targetSceneName;

    [Tooltip("Spawn point identifier in the destination scene.")]
    [SerializeField] private string targetSpawnID;

    [Header("Auto-Walk Sequence Settings")]
    [Tooltip("Optional transform target the player automatically walks towards upon trigger.")]
    [SerializeField] private Transform walkTarget;

    [Tooltip("Duration in seconds for the initial auto-walk and fade sequence.")]
    [SerializeField] private float sequenceDuration = 1.2f;

    [Header("Ambient Fade Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float targetOverlayAlpha = 1f;

    [Tooltip("Duration for the screen fade overlay to reach target alpha.")]
    [SerializeField] private float fadeDuration = 1.5f;

    #endregion

    #region Private Fields

    private bool isTeleporting = false;
    private Collider triggerCollider;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    #endregion

    #region Trigger Events

    private void OnTriggerEnter(Collider other)
    {
        if (isTeleporting) return;

        if (other.CompareTag("Player"))
        {
            isTeleporting = true;

            // Disable collider immediately to prevent duplicate triggers from child colliders
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            StartCoroutine(Routine_TeleportSequence());
        }
    }

    #endregion

    #region Sequence Coroutine

    private IEnumerator Routine_TeleportSequence()
    {
        // 2. Order player to walk towards the target point if assigned
        if (walkTarget != null)
        {
            GameEvents.Player.RequestAutoWalk(walkTarget.position);
        }

        // 1. Lock controls and set state to Cutscene/Transition
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Cutscene);

        // 3. Start progressive ambient fade to dark via event
        GameEvents.Scene.RequestFadeToBlack(targetOverlayAlpha, fadeDuration);

        // 4. Wait for sequence duration (unscaled to avoid timeScale locks)
        yield return new WaitForSecondsRealtime(sequenceDuration);

        // 5. Delegate scene change via decoupled event bus
        GameEvents.Scene.RequestChangeScene(targetSceneName, targetSpawnID);
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