using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnController : MonoBehaviour
{
    [Header("Respawn Configuration")]
    [SerializeField] private GameObject[] enemyList;
    [SerializeField] private float respawnDistance = 50f;
    [SerializeField] private float respawnCooldown = 120f; // Cooldown time in seconds

    private GameObject player;
    private float cooldownTimer = 0f;

    private void Start()
    {
        RespawnEnemy();
    }

    private void Update()
    {
        // Only evaluate respawn logic during active world exploration
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration)
            return;

        // If no active enemy exists under this spawner
        if (transform.childCount == 0)
        {
            // Accumulate timer while no enemy exists
            cooldownTimer += Time.deltaTime;

            // Condition 1: Has the cooldown time passed?
            if (cooldownTimer >= respawnCooldown)
            {
                // Lazy search for player if reference is missing
                if (player == null)
                {
                    player = GameObject.FindGameObjectWithTag("Player");
                }

                // Condition 2: Is the player far enough from the spawn point?
                if (player != null && Vector3.Distance(player.transform.position, transform.position) > respawnDistance)
                {
                    RespawnEnemy();
                    cooldownTimer = 0f; // Reset timer for next spawn cycle
                }
            }
        }
    }

    private void RespawnEnemy()
    {
        if (enemyList == null || enemyList.Length == 0) return;

        int randomEnemy = Random.Range(0, enemyList.Length);
        if (enemyList[randomEnemy] == null) return;

        GameObject newEnemy = Instantiate(enemyList[randomEnemy], transform.position, Quaternion.identity);
        newEnemy.transform.SetParent(transform);

        WorldEnemyAI enemyAi = newEnemy.GetComponent<WorldEnemyAI>();
        if (enemyAi != null)
        {
            enemyAi.enabled = true;
        }
    }

    public GameObject GetRandomEnemy()
    {
        if (enemyList == null || enemyList.Length == 0) return null;
        return enemyList[Random.Range(0, enemyList.Length)];
    }

    public GameObject GetEnemyLike(GameObject likeThis)
    {
        if (likeThis == null || enemyList == null) return null;

        foreach (GameObject prefab in enemyList)
        {
            if (prefab != null && likeThis.name.StartsWith(prefab.name))
            {
                return prefab;
            }
        }
        return null;
    }
}