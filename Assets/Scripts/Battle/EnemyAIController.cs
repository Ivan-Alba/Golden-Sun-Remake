using System.Collections.Generic;
using UnityEngine;

public class EnemyAIController : MonoBehaviour
{
    private FighterStats stats;

    private void Awake()
    {
        stats = GetComponent<FighterStats>();
    }

    /// <summary>
    /// Selects a valid alive target from the provided list of candidates.
    /// </summary>
    public GameObject SelectTarget(List<GameObject> candidates)
    {
        List<GameObject> validTargets = new List<GameObject>();

        foreach (GameObject candidate in candidates)
        {
            if (candidate != null && candidate.TryGetComponent<FighterStats>(out var targetStats))
            {
                if (targetStats.CurrHp > 0)
                {
                    validTargets.Add(candidate);
                }
            }
        }

        if (validTargets.Count == 0) return null;

        // Basic AI: Pick a random alive target (Can be expanded with priority algorithms)
        int randomIndex = Random.Range(0, validTargets.Count);
        return validTargets[randomIndex];
    }
}