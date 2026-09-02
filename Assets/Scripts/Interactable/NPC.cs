using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct NPCDialogueRule
{
    [Tooltip("Required story/game flag for this dialogue rule to activate.")]
    public string requiredFlag;

    [Tooltip("Dialogue ScriptableObject to assign when the flag condition is met.")]
    public DialogueSO dialogue;
}

/// <summary>
/// Controls world interaction for NPCs, smooth rotation towards the player, 
/// and dynamic dialogue evaluation using localized DialogueSO assets and GameFlags.
/// </summary>
public class NPC : Interactable
{
    #region Serialized Fields

    [Header("NPC Settings")]
    [SerializeField] private string npcName = "";
    [SerializeField] private float rotationSpeed = 4f;
    [SerializeField] private Sprite portraitSprite = null;

    [Header("Dynamic Dialogue Setup")]
    [Tooltip("Fallback dialogue asset when no story flags are met.")]
    [SerializeField] private DialogueSO defaultDialogue;

    [Tooltip("Conditional dialogue rules evaluated in top-to-bottom order of priority.")]
    [SerializeField] private List<NPCDialogueRule> conditionalDialogues = new List<NPCDialogueRule>();

    [Header("Camera Framing")]
    [SerializeField] private float eyeHeightOffset = 1.4f;

    #endregion

    #region Private Fields

    private bool lookAtPlayer;
    private Quaternion initialRotation;
    private Transform cachedPlayerTransform;

    #endregion

    #region Properties

    public string NpcName { get => npcName; set => npcName = value; }
    public Sprite PortraitSprite => portraitSprite;
    public bool LookAtPlayer { get => lookAtPlayer; set => lookAtPlayer = value; }
    public float EyeHeightOffset => eyeHeightOffset;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (string.IsNullOrEmpty(npcName)) npcName = "";

        // Cache initial spawn rotation
        initialRotation = transform.rotation;
    }

    private void Update()
    {
        if (lookAtPlayer && cachedPlayerTransform != null)
        {
            // Calculate horizontal direction to prevent vertical tilt on slopes
            Vector3 direction = cachedPlayerTransform.position - transform.position;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // Return smoothly to original orientation
            transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, rotationSpeed * Time.deltaTime);
        }
    }

    #endregion

    #region Interaction Logic

    public override void Interact()
    {
        // Cache rotation and player transform
        initialRotation = transform.rotation;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerTransform = playerObj.transform;
            lookAtPlayer = true;

            // Decoupled request: Tell player to face this NPC
            GameEvents.Player.RequestLookAt(transform.position, 0.25f);
        }

        // Trigger player interaction animation
        GameEvents.Player.TriggerPlayInteractionAnimation(PlayerInteractionAnimType.InteractNPC);

        // Evaluate and start active dialogue via decoupled event
        DialogueSO activeDialogue = EvaluateActiveDialogue();

        if (activeDialogue != null)
        {
            GameEvents.UI.RequestStartDialogue(activeDialogue, npcName, gameObject, portraitSprite);
        }

        // Debug Command
        if (npcName == "DEBUG_ALL_ITEMS")
        {
            GameEvents.Inventory.RequestAllItems();
        }
    }

    /// <summary>
    /// Evaluates story flags in top-to-bottom priority to select the current active dialogue.
    /// </summary>
    private DialogueSO EvaluateActiveDialogue()
    {
        foreach (var rule in conditionalDialogues)
        {
            if (string.IsNullOrEmpty(rule.requiredFlag)) continue;

            if (GameManager.Instance != null && GameManager.Instance.HasFlag(rule.requiredFlag))
            {
                return rule.dialogue;
            }
        }

        return defaultDialogue;
    }

    /// <summary>
    /// Call this method when dialogue ends to resume normal rotation behavior.
    /// </summary>
    public void EndDialogueLook()
    {
        lookAtPlayer = false;
        cachedPlayerTransform = null;
    }

    #endregion

    #region Editor Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Calculate the world position of the eye pivot
        Vector3 eyePos = transform.position + Vector3.up * eyeHeightOffset;

        // Draw a cyan wireframe sphere at eye level
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(eyePos, 0.15f);

        // Draw a vertical line from root position to the eye height
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawLine(transform.position, eyePos);
    }
#endif

    #endregion
}