using UnityEngine;

public class PersistentCore : MonoBehaviour
{
    public static PersistentCore Instance { get; private set; }

    private void Awake()
    {
        // Singleton check to prevent duplicates when reloading scenes containing [PERSISTENT_CORE]
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Preserve this root object and all its children across scene loads
        DontDestroyOnLoad(gameObject);
    }
}