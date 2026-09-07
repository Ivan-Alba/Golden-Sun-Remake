using UnityEngine;

/// <summary>
/// World interaction spot for Save/Load points (e.g. Sanctum altars, monuments, resting spots).
/// Inherits from Interactable to integrate seamlessly with interaction prompts and player orientation.
/// Triggers an introductory dialogue choice or the Save/Load Book UI directly via GameEvents.
/// </summary>
public class SavePointSpot : Interactable
{
    #region Serialized Fields

    [Header("Save Point Settings")]
    [SerializeField] private string savePointName = "Monumento del Sol";
    [SerializeField] private GameEvents.UI.SaveBookMode defaultMode = GameEvents.UI.SaveBookMode.Save;

    [Header("Dialogue Integration (Optional)")]
    [Tooltip("Optional dialogue asset containing choices (Save / Load / Cancel). If assigned, triggers a dialogue prompt before opening the book.")]
    [SerializeField] private DialogueSO savePointDialogue;
    [SerializeField] private string speakerName = "Altar Sagrado";
    [SerializeField] private Sprite speakerPortrait;

    [Header("Spawn Point Reference")]
    [Tooltip("Reference to the SpawnPoint associated with this save spot where the player will reappear upon loading.")]
    [SerializeField] private SpawnPoint linkedSpawnPoint;

    [Header("Audio & FX")]
    [SerializeField] private SFXType interactionSFX = SFXType.UI_Confirm;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Fallback: If no explicit SpawnPoint reference was dragged in the Inspector,
        // try to find a SpawnPoint component attached to this same GameObject or its children.
        if (linkedSpawnPoint == null)
        {
            linkedSpawnPoint = GetComponentInChildren<SpawnPoint>();
        }
    }

    #endregion

    #region Interaction Logic

    public override void Interact()
    {
        // 1. Notify GameManager about the active SpawnPoint associated with this save spot
        if (linkedSpawnPoint != null && !string.IsNullOrEmpty(linkedSpawnPoint.SpawnID))
        {
            GameEvents.Scene.SetCurrentSpawnPoint(linkedSpawnPoint.SpawnID);
        }
        else
        {
            Debug.LogWarning($"[SavePointSpot] '{savePointName}' has no valid SpawnPoint linked!");
        }

        // 2. Orient the player towards this save point
        GameEvents.Player.RequestLookAt(transform.position, 0.25f);

        // 3. Play player interaction animation (e.g., praying/touching altar)
        GameEvents.Player.TriggerPlayInteractionAnimation(PlayerInteractionAnimType.InteractNPC);

        // 4. Optional interaction SFX
        GameEvents.Audio.RequestPlaySFX(interactionSFX);

        // 5. Check if we have an interactive dialogue choice set up
        if (savePointDialogue != null)
        {
            Debug.Log($"<color=cyan>[SavePointSpot] Interacted with '{savePointName}'. Starting choice dialogue.</color>");

            // Disparamos el evento de diálogo que el DialogueController ya está escuchando
            GameEvents.UI.RequestStartDialogue(savePointDialogue, speakerName, gameObject, speakerPortrait);

            // Nota: Las opciones de "Guardar", "Cargar" o "Cancelar" dentro del DialogueSO 
            // deben invocar las acciones correspondientes (ej. abrir libro en Save, Load o cerrar).
        }
        else
        {
            // Fallback directo por si no se quiere usar diálogo previo
            Debug.Log($"<color=yellow>[SavePointSpot] Opening Save/Load Book directly in {defaultMode} mode.</color>");
            GameEvents.UI.RequestOpenSaveLoadBook(defaultMode);
        }
    }

    #endregion

    #region Gizmos & Editor Visualization

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.84f, 0f, 0.75f); // Golden glow for save points
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.4f);

        if (linkedSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, linkedSpawnPoint.transform.position + Vector3.up * 0.5f);
        }
    }
#endif

    #endregion
}