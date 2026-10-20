using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct NPCDialogueRule
{
    [Tooltip("Required story/game flag ScriptableObject for this dialogue rule to activate.")]
    public FlagSO requiredFlag;

    [Tooltip("Fallback slug identifier if no FlagSO asset is directly assigned.")]
    public string requiredFlagSlug;

    [Tooltip("Dialogue ScriptableObject to assign when the flag condition is met.")]
    public DialogueSO dialogue;
}

/// <summary>
/// Controls world interaction for NPCs and dynamic dialogue evaluation using localized DialogueSO assets and FlagSO assets.
/// Pure interaction handler decoupled from spatial rotation and locomotion.
/// </summary>
public class NPC : Interactable
{
    #region Serialized Fields

    [Header("NPC Settings")]
    [SerializeField] private string npcName = "";
    [SerializeField] private Sprite portraitSprite = null;

    [Header("Dynamic Dialogue Setup")]
    [Tooltip("Fallback dialogue asset when no story flags are met.")]
    [SerializeField] private DialogueSO defaultDialogue;

    [Tooltip("Conditional dialogue rules evaluated in top-to-bottom order of priority.")]
    [SerializeField] private List<NPCDialogueRule> conditionalDialogues = new List<NPCDialogueRule>();

    #endregion

    #region Private Runtime State

    private bool isCurrentlyTalking;

    #endregion

    #region Properties

    public string NpcName { get => npcName; set => npcName = value; }
    public Sprite PortraitSprite => portraitSprite;

    #endregion

    #region Interaction Logic

    public override void Interact()
    {
        isCurrentlyTalking = true;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            // Request player to face this NPC
            GameEvents.Player.RequestLookAt(transform.position, 0.25f);
        }

        // Trigger player interaction animation
        GameEvents.Player.RequestInteractAnimation();

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

    private DialogueSO EvaluateActiveDialogue()
    {
        foreach (var rule in conditionalDialogues)
        {
            bool isFlagActive = false;

            if (rule.requiredFlag != null)
            {
                GameEvents.World.RequestCheckFlag(rule.requiredFlag, state => isFlagActive = state);
            }
            else if (!string.IsNullOrEmpty(rule.requiredFlagSlug))
            {
                GameEvents.Database.RequestFlagBySlug(rule.requiredFlagSlug, flagAsset =>
                {
                    if (flagAsset != null)
                    {
                        GameEvents.World.RequestCheckFlag(flagAsset, state => isFlagActive = state);
                    }
                });
            }

            if (isFlagActive)
            {
                return rule.dialogue;
            }
        }

        return defaultDialogue;
    }

    public void EndDialogueLook()
    {
        isCurrentlyTalking = false;
    }

    #endregion
}