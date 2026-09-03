using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject player;

    [Header("Audio")]
    [SerializeField] private AudioClip audioCollision;

    private Collider enemyTouched;
    private bool battleLaunched;

    private void Awake()
    {
        // Fallback in case player is not assigned via Inspector
        if (player == null && transform.parent != null)
        {
            player = transform.parent.gameObject;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only process collisions during active world exploration
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration)
            return;

        if (other.gameObject.CompareTag("Enemy") && !battleLaunched)
        {
            // Dynamically search for BattleManager in the current scene
            BattleManager bm = FindObjectOfType<BattleManager>();
            if (bm == null)
            {
                Debug.LogWarning("[EnemyDetector] Enemy touched, but no BattleManager exists in this scene!");
                return;
            }

            battleLaunched = true;

            // 1. Lock inputs during overworld encounter animation using Cutscene state
            if (GameManager.Instance != null)
            {
                GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Cutscene);
            }

            // 2. Play collision sound
            AudioSource audioSrc = GetComponent<AudioSource>();
            if (audioSrc != null && audioCollision != null)
            {
                audioSrc.PlayOneShot(audioCollision);
            }

            // 3. Freeze player and rotate towards enemy
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.SimpleMove(Vector3.zero);

                player.transform.LookAt(new Vector3(other.transform.position.x, player.transform.position.y, other.transform.position.z));

                Animator anim = player.GetComponent<Animator>();
                if (anim != null) anim.SetTrigger("attacked");
            }

            // 4. Temporarily disable enemy AI and animator
            WorldEnemyAI enemyAi = other.gameObject.GetComponent<WorldEnemyAI>();
            if (enemyAi != null) enemyAi.CollisionWithPlayer();

            Animator enemyAnim = other.gameObject.GetComponent<Animator>();
            if (enemyAnim != null) enemyAnim.enabled = false;

            enemyTouched = other;

            // 5. Start transition delay
            StartCoroutine(StartBattle(bm));
        }
    }

    private IEnumerator StartBattle(BattleManager bm)
    {
        // Delay for impact animation / sound before entering battle screen
        yield return new WaitForSeconds(1.0f);

        if (bm != null && enemyTouched != null)
        {
            bm.AwakeBattle(enemyTouched.gameObject, enemyTouched.transform.parent.gameObject);
        }

        battleLaunched = false;
    }
}