using UnityEngine;

/// <summary>
/// Component attached to world elements (NPCs, altars, statues, interactables) 
/// to define a custom eye-level or focus height for Cinemachine framing during dialogues.
/// </summary>
public class DialogueFocusTarget : MonoBehaviour
{
    [Tooltip("Altura vertical (offset desde la posición del objeto) hacia donde apuntará la cámara de diálogo.")]
    [SerializeField] private float eyeHeightOffset = 1.4f;

    public float EyeHeightOffset => eyeHeightOffset;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeightOffset, 0.3f);
    }
#endif
}