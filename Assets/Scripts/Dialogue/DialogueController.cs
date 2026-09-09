using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public enum EmoteType
{
    Exclamation,
    Question,
    Idea,
    Sweat,
    Heart,
    Anger
}

/// <summary>
/// Manages active dialogue execution, typewriter animations, Cinemachine camera framing,  
/// story flag progression, and branching choice resolution.
/// Reacts to input events via GameEvents.Input and decoupled GameEvents.GameState.
/// </summary>
public class DialogueController : MonoBehaviour
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
    private bool isWaitingForAction;

    private string defaultSpeakerName;
    private Sprite defaultPortrait;
    private GameObject currentInteractableElement;

    // Active choices cache for index resolution
    private readonly List<DialogueChoice> currentActiveChoices = new List<DialogueChoice>();

    // Virtual framing targets for eye-level camera focus
    private GameObject playerEyePivot;
    private GameObject targetEyePivot;

    // Local GameState cache
    private GameManager.GameState _cachedGameState;

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
        // UI & System Event Subscriptions
        GameEvents.UI.OnStartDialogueRequested -= HandleStartDialogueRequested;
        GameEvents.UI.OnStartDialogueRequested += HandleStartDialogueRequested;

        GameEvents.UI.OnSelectDialogueChoiceRequested -= HandleSelectDialogueChoiceRequested;
        GameEvents.UI.OnSelectDialogueChoiceRequested += HandleSelectDialogueChoiceRequested;

        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;

        // Input Event Subscriptions
        SubscribeToInputEvents();
    }

    private void OnDisable()
    {
        // UI & System Event Unsubscriptions
        GameEvents.UI.OnStartDialogueRequested -= HandleStartDialogueRequested;
        GameEvents.UI.OnSelectDialogueChoiceRequested -= HandleSelectDialogueChoiceRequested;

        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;

        // Input Event Unsubscriptions
        UnsubscribeFromInputEvents();

        CleanupEyePivots();
    }

    #endregion

    #region Event Subscriptions & Callbacks

    private void SubscribeToInputEvents()
    {
        UnsubscribeFromInputEvents();

        GameEvents.Input.OnDialogueAdvancePressed += HandleDialogueAdvance;
    }

    private void UnsubscribeFromInputEvents()
    {
        GameEvents.Input.OnDialogueAdvancePressed -= HandleDialogueAdvance;
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        _cachedGameState = newState;
    }

    private void HandleStartDialogueRequested(DialogueSO dialogue, string speakerName, GameObject speakerObj, Sprite portrait)
    {
        StartDialogue(dialogue, speakerName, speakerObj, portrait);
    }

    private void HandleDialogueAdvance()
    {
        if (_cachedGameState != GameManager.GameState.InDialogue) return;

        // Block manual advancement if waiting for choices or executing a blocking action
        if (isAwaitingChoice || isWaitingForAction) return;

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

        // Process choice-specific story flag via asset or database slug
        if (selectedChoice.setFlagOnSelect != null)
        {
            GameEvents.World.RequestSetFlag(selectedChoice.setFlagOnSelect, true);
        }
        else if (!string.IsNullOrEmpty(selectedChoice.setFlagSlugOnSelect))
        {
            GameEvents.Database.RequestFlagBySlug(selectedChoice.setFlagSlugOnSelect, flagAsset =>
            {
                if (flagAsset != null) GameEvents.World.RequestSetFlag(flagAsset, true);
            });
        }

        isAwaitingChoice = false;
        currentActiveChoices.Clear();

        // Execute modular actions assigned to this choice with support for asynchronous/blocking actions
        if (selectedChoice.actionsOnSelect != null && selectedChoice.actionsOnSelect.Count > 0)
        {
            bool waitingForAnyAction = false;
            int actionsRemaining = 0;

            foreach (var action in selectedChoice.actionsOnSelect)
            {
                if (action != null && action.RequiresWaiting)
                {
                    waitingForAnyAction = true;
                    actionsRemaining++;
                }
            }

            if (waitingForAnyAction)
            {
                // Ocultamos la UI de diálogo por completo mientras el menú externo esté abierto
                GameEvents.UI.RequestSetDialogueUIVisible(false);

                isWaitingForAction = true;

                foreach (var action in selectedChoice.actionsOnSelect)
                {
                    if (action != null)
                    {
                        if (action.RequiresWaiting)
                        {
                            action.ExecuteAction(() =>
                            {
                                actionsRemaining--;
                                if (actionsRemaining <= 0)
                                {
                                    ResumeDialogueAfterAction(selectedChoice);
                                }
                            });
                        }
                        else
                        {
                            action.ExecuteAction();
                        }
                    }
                }
                return; // Pause dialogue flow here until all blocking actions invoke their completion callback
            }
            else
            {
                for (int i = 0; i < selectedChoice.actionsOnSelect.Count; i++)
                {
                    if (selectedChoice.actionsOnSelect[i] != null)
                    {
                        selectedChoice.actionsOnSelect[i].ExecuteAction();
                    }
                }
            }
        }

        ContinueDialogueFlow(selectedChoice);
    }

    private void ResumeDialogueAfterAction(DialogueChoice selectedChoice)
    {
        isWaitingForAction = false;

        // Solo reactivamos la visibilidad de la UI si realmente hay un siguiente diálogo o nodo al que avanzar.
        // Si no hay siguiente diálogo y estamos en el último nodo, el flujo cerrará limpiamente sin mostrar elementos fantasma.
        bool willContinue = (selectedChoice.nextDialogue != null) || (currentNodeIndex + 1 < currentDialogue.nodes.Count);

        if (willContinue)
        {
            GameEvents.UI.RequestSetDialogueUIVisible(true);
        }

        ContinueDialogueFlow(selectedChoice);
    }

    private void ContinueDialogueFlow(DialogueChoice selectedChoice)
    {
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
        isWaitingForAction = false;

        // Asegurar que la UI esté visible al iniciar un nuevo bloque de diálogo
        GameEvents.UI.RequestSetDialogueUIVisible(true);

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

        // Process node completion flag via asset or database lookup
        if (node.setFlagOnComplete != null)
        {
            GameEvents.World.RequestSetFlag(node.setFlagOnComplete, true);
        }
        else if (!string.IsNullOrEmpty(node.setFlagSlugOnComplete))
        {
            GameEvents.Database.RequestFlagBySlug(node.setFlagSlugOnComplete, flagAsset =>
            {
                if (flagAsset != null)
                {
                    GameEvents.World.RequestSetFlag(flagAsset, true);
                }
            });
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

                // Filter out choices restricted by flags via callback request
                if (choice.requiredFlag != null)
                {
                    bool hasFlag = false;
                    GameEvents.World.RequestCheckFlag(choice.requiredFlag, state => hasFlag = state);

                    if (!hasFlag)
                    {
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(choice.requiredFlagSlug))
                {
                    bool hasFlag = false;
                    GameEvents.Database.RequestFlagBySlug(choice.requiredFlagSlug, flagAsset =>
                    {
                        if (flagAsset != null)
                        {
                            GameEvents.World.RequestCheckFlag(flagAsset, state => hasFlag = state);
                        }
                    });

                    if (!hasFlag)
                    {
                        continue;
                    }
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

        // Asegurarnos de que la UI del diálogo esté oculta al iniciar el cierre final
        GameEvents.UI.RequestSetDialogueUIVisible(false);

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority.Value = inactiveCameraPriority;
        }

        if (currentInteractableElement != null && currentInteractableElement.TryGetComponent<NPC>(out var npc))
        {
            npc.EndDialogueLook();
        }

        // Set global flag marking this dialogue asset as read via event bus
        if (currentDialogue != null && currentDialogue.markAsReadOnEnd && currentDialogue.readFlag != null)
        {
            GameEvents.World.RequestSetFlag(currentDialogue.readFlag, true);
        }
        else if (currentDialogue != null && currentDialogue.markAsReadOnEnd && !string.IsNullOrEmpty(currentDialogue.dialogueId))
        {
            GameEvents.Database.RequestFlagBySlug($"SPOKEN_TO_{currentDialogue.dialogueId}", flagAsset =>
            {
                if (flagAsset != null)
                {
                    GameEvents.World.RequestSetFlag(flagAsset, true);
                }
            });
        }

        yield return new WaitForSeconds(0.3f);

        CleanupEyePivots();

        currentDialogue = null;
        currentNodeIndex = 0;
        isAwaitingChoice = false;
        isWaitingForAction = false;
        currentActiveChoices.Clear();

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
    }

    #endregion

    #region Camera Focus Helpers

    private void FocusDialogueCamera(GameObject targetElement)
    {
        if (dialogueCamera == null || targetElement == null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        // 1. Setup eye-level pivots for the target and player
        SetupEyePivots(playerObj.transform, targetElement.transform);

        // 2. Assign targets to Cinemachine TargetGroup
        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();

            targetGroup.Targets.Add(new CinemachineTargetGroup.Target
            {
                Object = playerEyePivot.transform,
                Weight = 1f,
                Radius = 0.8f
            });

            // Only add target eye pivot to the group if it was successfully created
            if (targetEyePivot != null)
            {
                targetGroup.Targets.Add(new CinemachineTargetGroup.Target
                {
                    Object = targetEyePivot.transform,
                    Weight = 1f,
                    Radius = 0.8f
                });
            }

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

    private void SetupEyePivots(Transform playerTransform, Transform targetTransform)
    {
        CleanupEyePivots();

        // 1. Setup Player Pivot
        playerEyePivot = new GameObject("Player_EyePivot");
        playerEyePivot.transform.position = playerTransform.position + Vector3.up * eyeHeightOffset;
        playerEyePivot.transform.SetParent(playerTransform, true);

        // 2. Setup Target Pivot (Checks universally for DialogueFocusTarget component)
        if (currentInteractableElement != null && currentInteractableElement.TryGetComponent<DialogueFocusTarget>(out var focusTarget))
        {
            targetEyePivot = new GameObject("Target_EyePivot");
            targetEyePivot.transform.position = targetTransform.position + Vector3.up * focusTarget.EyeHeightOffset;
            targetEyePivot.transform.SetParent(targetTransform, true);
        }
        else
        {
            // Fallback safety: If no DialogueFocusTarget is present, do not create a forced low pivot.
            targetEyePivot = null;
        }
    }

    private void CleanupEyePivots()
    {
        if (playerEyePivot != null)
        {
            Destroy(playerEyePivot);
            playerEyePivot = null;
        }

        if (targetEyePivot != null)
        {
            Destroy(targetEyePivot);
            targetEyePivot = null;
        }
    }

    #endregion
}