using UnityEngine;

/// <summary>
/// World interaction spot for Save/Load points (e.g. Sanctum altars, monuments, resting spots).
/// Inherits from Interactable to integrate seamlessly with interaction prompts and player orientation.
/// Triggers the Save/Load Book UI directly via GameEvents and notifies GameManager of the active SpawnPoint.
/// </summary>
public class SavePointSpot : Interactable
{
    #region Serialized Fields

    [Header("Save Point Settings")]
    [SerializeField] private string savePointName = "Monumento del Sol";
    [SerializeField] private GameEvents.UI.SaveBookMode defaultMode = GameEvents.UI.SaveBookMode.Save;

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

        // TODO: In the future, trigger a dialogue/choice window (Save / Load / Cancel)
        // prior to opening the book. For now, open the book directly in the mode configured.
        /*
        GameEvents.UI.RequestStartChoiceDialogue(
            "¿Deseas registrar tus avances o rememorar tus viajes?",
            new string[] { "Guardar", "Cargar", "Cancelar" },
            selectedIndex => { ... }
        );
        */

        // 5. Request opening Save/Load Book UI via decoupled event bus
        Debug.Log($"<color=yellow>[SavePointSpot] Interacted with '{savePointName}'. Opening Save/Load Book in {defaultMode} mode.</color>");
        GameEvents.UI.RequestOpenSaveLoadBook(defaultMode);
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