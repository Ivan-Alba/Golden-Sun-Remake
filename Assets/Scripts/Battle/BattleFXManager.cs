using TMPro;
using UnityEngine;

public class BattleFXManager : MonoBehaviour
{
    public static BattleFXManager Instance { get; private set; }

    [SerializeField] private GameObject floatingTextPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SpawnFloatingText(Transform targetTransform, string text, Color color)
    {
        if (floatingTextPrefab == null || targetTransform == null) return;

        Vector3 offset = Vector3.up * 1.5f;

        if (targetTransform.TryGetComponent<BoxCollider>(out var collider))
        {
            offset = new Vector3(0, collider.bounds.size.y, 0.2f);
        }

        GameObject textObj = Instantiate(floatingTextPrefab, targetTransform.position + offset, Quaternion.identity);
        if (textObj.TryGetComponent<TextMeshPro>(out var tmp))
        {
            tmp.SetText(text);
            tmp.color = color;
        }
    }
}