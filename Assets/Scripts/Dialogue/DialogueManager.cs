using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Manages active dialogue execution, typewriter animations, Cinemachine camera framing, 
/// story flag progression, and branching choice resolution.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Camera Control")]
    [SerializeField] private CinemachineCamera dialogueCamera;
    [SerializeField] private CinemachineTargetGroup targetGroup;
    [SerializeField] private int activeCameraPriority = 20;
    [SerializeField] private int inactiveCameraPriority = 0;
    [SerializeField] private float eyeHeightOffset = 1.4f;

    [Header("Typewriter Settings")]
    [SerializeField] private float charactersPerSecond = 30f;

    #endregion

    #region Private Fields

    private DialogueSO currentDialogue;
    private int currentNodeIndex;
    private Coroutine typewriterCoroutine;
    private bool isTyping;
    private bool isAwaitingChoice;

    private string defaultSpeakerName;
    private Sprite defaultPortrait;
    private GameObject currentInteractableElement;

    // Active choices cache for index resolution
    private List<DialogueChoice> currentActiveChoices = new List<DialogueChoice>();

    // Virtual framing targets for eye-level camera focus
    private GameObject playerEyePivot;
    private GameObject npcEyePivot;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        if (dialogueCamera != null)
        {
            dialogueCamera.Priority.Value = inactiveCameraPriority;
        }
    }

    private void OnEnable()
    {
        // Event Subscriptions
        GameEvents.UI.OnStartDialogueRequested -= HandleStartDialogueRequested;
        GameEvents.UI.OnStartDialogueRequested += HandleStartDialogueRequested;

        GameEvents.UI.OnSelectDialogueChoiceRequested -= HandleSelectDialogueChoiceRequested;
        GameEvents.UI.OnSelectDialogueChoiceRequested += HandleSelectDialogueChoiceRequested;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDialogueAdvancePressed += HandleDialogueAdvance;
        }
    }

    private void OnDisable()
    {
        // Event Unsubscriptions
        GameEvents.UI.OnStartDialogueRequested -= HandleStartDialogueRequested;
        GameEvents.UI.OnSelectDialogueChoiceRequested -= HandleSelectDialogueChoiceRequested;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDialogueAdvancePressed -= HandleDialogueAdvance;
        }

        CleanupEyePivots();
    }

    #endregion

    #region Event Callbacks

    private void HandleStartDialogueRequested(DialogueSO dialogue, string speakerName, GameObject speakerObj, Sprite portrait)
    {
        StartDialogue(dialogue, speakerName, speakerObj, portrait);
    }

    private void HandleDialogueAdvance()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InDialogue) return;

        // Block manual advancement if the UI is currently displaying choices and waiting for input
        if (isAwaitingChoice) return;

        if (isTyping)
        {
            CompleteCurrentLine();
            return;
        }

        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
        AdvanceDialogue();
    }

    private void HandleSelectDialogueChoiceRequested(int choiceIndex)
    {
        if (!isAwaitingChoice || choiceIndex < 0 || choiceIndex >= currentActiveChoices.Count) return;

        DialogueChoice selectedChoice = currentActiveChoices[choiceIndex];

        // Process choice-specific story flag if set
        if (!string.IsNullOrEmpty(selectedChoice.setFlagOnSelect) && GameManager.Instance != null)
        {
            GameManager.Instance.SetFlag(selectedChoice.setFlagOnSelect, true);
        }

        isAwaitingChoice = false;
        currentActiveChoices.Clear();

        // Branch to next dialogue if assigned, otherwise advance to next node or finish
        if (selectedChoice.nextDialogue != null)
        {
            StartDialogue(selectedChoice.nextDialogue, defaultSpeakerName, currentInteractableElement, defaultPortrait);
        }
        else
        {
            AdvanceDialogue();
        }
    }

    #endregion

    #region Dialogue System Flow

    private void StartDialogue(DialogueSO dialogue, string speakerName, GameObject element, Sprite portrait)
    {
        if (dialogue == null || dialogue.nodes == null || dialogue.nodes.Count == 0) return;

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InDialogue);

        currentDialogue = dialogue;
        currentNodeIndex = 0;
        defaultSpeakerName = speakerName;
        defaultPortrait = portrait;
        currentInteractableElement = element;
        isAwaitingChoice = false;

        FocusDialogueCamera(element);
        DisplayCurrentNode();
    }

    private void AdvanceDialogue()
    {
        currentNodeIndex++;

        if (currentDialogue != null && currentNodeIndex < currentDialogue.nodes.Count)
        {
            DisplayCurrentNode();
        }
        else
        {
            StartCoroutine(CloseDialogueRoutine());
        }
    }

    private void DisplayCurrentNode()
    {
        DialogueNode node = currentDialogue.nodes[currentNodeIndex];

        // Process node completion flags if specified
        if (!string.IsNullOrEmpty(node.setFlagOnComplete) && GameManager.Instance != null)
        {
            GameManager.Instance.SetFlag(node.setFlagOnComplete, true);
        }

        // Resolve localized text line
        string localizedText = (node.localizedText != null && !node.localizedText.IsEmpty)
            ? node.localizedText.GetLocalizedString()
            : string.Empty;

        // Resolve speaker overrides
        string speaker = (node.overrideSpeakerName != null && !node.overrideSpeakerName.IsEmpty)
            ? node.overrideSpeakerName.GetLocalizedString()
            : defaultSpeakerName;

        Sprite portrait = node.overridePortrait != null ? node.overridePortrait : defaultPortrait;

        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterRoutine(speaker, localizedText, portrait, node));
    }

    private IEnumerator TypewriterRoutine(string speaker, string line, Sprite portrait, DialogueNode node)
    {
        isTyping = true;
        float delay = 1f / Mathf.Max(charactersPerSecond, 1f);

        for (int i = 0; i <= line.Length; i++)
        {
            string currentSubstring = line.Substring(0, i);

            // Send partial line without choices during typewriter effect
            GameEvents.UI.RequestUpdateDialogue(speaker, currentSubstring, portrait, node.choiceType, null);

            yield return new WaitForSeconds(delay);
        }

        isTyping = false;
        EvaluateNodeChoices(speaker, line, portrait, node);
    }

    private void CompleteCurrentLine()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);

        isTyping = false;

        DialogueNode node = currentDialogue.nodes[currentNodeIndex];

        string localizedText = (node.localizedText != null && !node.localizedText.IsEmpty)
            ? node.localizedText.GetLocalizedString()
            : string.Empty;

        string speaker = (node.overrideSpeakerName != null && !node.overrideSpeakerName.IsEmpty)
            ? node.overrideSpeakerName.GetLocalizedString()
            : defaultSpeakerName;

        Sprite portrait = node.overridePortrait != null ? node.overridePortrait : defaultPortrait;

        EvaluateNodeChoices(speaker, localizedText, portrait, node);
    }

    private void EvaluateNodeChoices(string speaker, string line, Sprite portrait, DialogueNode node)
    {
        currentActiveChoices.Clear();
        List<DialogueChoiceData> choiceDataList = null;

        if (node.choices != null && node.choices.Count > 0)
        {
            choiceDataList = new List<DialogueChoiceData>();

            for (int i = 0; i < node.choices.Count; i++)
            {
                var choice = node.choices[i];

                // Filter out choices restricted by flags
                if (!string.IsNullOrEmpty(choice.requiredFlag) && GameManager.Instance != null && !GameManager.Instance.HasFlag(choice.requiredFlag))
                {
                    continue;
                }

                currentActiveChoices.Add(choice);

                string choiceText = (choice.localizedChoiceText != null && !choice.localizedChoiceText.IsEmpty)
                    ? choice.localizedChoiceText.GetLocalizedString()
                    : string.Empty;

                choiceDataList.Add(new DialogueChoiceData(choiceText, currentActiveChoices.Count - 1));
            }

            if (choiceDataList.Count > 0)
            {
                isAwaitingChoice = true;
            }
            else
            {
                choiceDataList = null;
            }
        }

        // Notify HUD with fully resolved line and choice data payload
        GameEvents.UI.RequestUpdateDialogue(speaker, line, portrait, node.choiceType, choiceDataList);
    }

    private IEnumerator CloseDialogueRoutine()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority.Value = inactiveCameraPriority;
        }

        if (currentInteractableElement != null && currentInteractableElement.TryGetComponent<NPC>(out var npc))
        {
            npc.EndDialogueLook();
        }

        // Set global flag marking this dialogue asset as read
        if (currentDialogue != null && currentDialogue.markAsReadOnEnd && !string.IsNullOrEmpty(currentDialogue.dialogueId) && GameManager.Instance != null)
        {
            GameManager.Instance.SetFlag($"SPOKEN_TO_{currentDialogue.dialogueId}", true);
        }

        yield return new WaitForSeconds(0.3f);

        CleanupEyePivots();

        currentDialogue = null;
        currentNodeIndex = 0;
        isAwaitingChoice = false;
        currentActiveChoices.Clear();

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
    }

    #endregion

    #region Camera Focus Helpers

    private void FocusDialogueCamera(GameObject npcTarget)
    {
        if (dialogueCamera == null || npcTarget == null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        // 1. Setup eye-level pivots
        SetupEyePivots(playerObj.transform, npcTarget.transform);

        // 2. Assign targets to TargetGroup
        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();

            targetGroup.Targets.Add(new CinemachineTargetGroup.Target
            {
                Object = playerEyePivot.transform,
                Weight = 1f,
                Radius = 0.8f
            });

            targetGroup.Targets.Add(new CinemachineTargetGroup.Target
            {
                Object = npcEyePivot.transform,
                Weight = 1f,
                Radius = 0.8f
            });

            dialogueCamera.Follow = targetGroup.transform;
            dialogueCamera.LookAt = targetGroup.transform;
        }

        // 3. Align dialogue camera start position with main exploration camera
        if (Camera.main != null)
        {
            Vector3 startPos = Camera.main.transform.position;
            Quaternion startRot = Camera.main.transform.rotation;

            dialogueCamera.transform.position = startPos;
            dialogueCamera.transform.rotation = startRot;

            // Force Cinemachine to warp internal state to current exploration position instantly
            dialogueCamera.ForceCameraPosition(startPos, startRot);
        }

        // 4. Elevate priority to trigger Cinemachine's smooth framing blend
        dialogueCamera.Priority.Value = activeCameraPriority;
    }

    private void SetupEyePivots(Transform playerTransform, Transform npcTransform)
    {
        CleanupEyePivots();

        // 1. Player Pivot
        playerEyePivot = new GameObject("Player_EyePivot");
        playerEyePivot.transform.position = playerTransform.position + Vector3.up * eyeHeightOffset;

        // 2. NPC Pivot (Reads custom height offset if available)
        float npcOffset = eyeHeightOffset;
        if (currentInteractableElement != null && currentInteractableElement.TryGetComponent<NPC>(out var npcScript))
        {
            npcOffset = npcScript.EyeHeightOffset;
        }

        npcEyePivot = new GameObject("NPC_EyePivot");
        npcEyePivot.transform.position = npcTransform.position + Vector3.up * npcOffset;

        playerEyePivot.transform.SetParent(playerTransform, true);
        npcEyePivot.transform.SetParent(npcTransform, true);
    }

    private void CleanupEyePivots()
    {
        if (playerEyePivot != null)
        {
            Destroy(playerEyePivot);
            playerEyePivot = null;
        }

        if (npcEyePivot != null)
        {
            Destroy(npcEyePivot);
            npcEyePivot = null;
        }
    }

    #endregion
}