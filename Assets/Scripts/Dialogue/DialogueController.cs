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
/// Manages active dialogue execution, typewriter animations, Cinemachine camera framing (supporting 3 levels),  
/// story flag progression, and branching choice resolution.
/// Reacts to input events via GameEvents.Input and decoupled GameEvents.GameState.
/// </summary>
public class DialogueController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Camera Control")]
    [SerializeField] private CinemachineCamera dialogueCamera;
    [SerializeField] private CinemachineTargetGroup targetGroup;
    [SerializeField] private DialogueFocusProvider playerFocusProvider;
    [SerializeField] private int activeCameraPriority = 20;
    [SerializeField] private int inactiveCameraPriority = 0;

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

    // Virtual framing and look-at targets for camera focus and zoom calculations
    private GameObject playerEyePivot;
    private GameObject playerFramingPivot;
    private GameObject targetEyePivot;
    private GameObject targetFramingPivot;
    private GameObject groupLookAtPivot; // Pivote intermedio inteligente para el punto medio de los ojos

    // Local GameState caches
    private GameManager.GameState _cachedGameState;
    private GameManager.GameState _cachedPreviousGameState;

    // Active custom cinematic camera tracking (Level 2)
    private CinemachineCamera activeCustomDialogueCamera;

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
                // Hide dialogue HUD completely while external menu is active
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
                return;
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

        // Restore dialogue HUD visibility only if there is a subsequent dialogue node or branch
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

        // Cache previous state before shifting to InDialogue (crucial for Cutscenes support)
        _cachedPreviousGameState = _cachedGameState;

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InDialogue);

        currentDialogue = dialogue;
        currentNodeIndex = 0;
        defaultSpeakerName = speakerName;
        defaultPortrait = portrait;
        currentInteractableElement = element;
        isAwaitingChoice = false;
        isWaitingForAction = false;

        // Ensure dialogue HUD is visible on startup
        GameEvents.UI.RequestSetDialogueUIVisible(true);

        // Setup cameras based on the 3 levels architecture
        SetupDialogueCamera(element);

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

        // Ensure dialogue UI is hidden when closing
        GameEvents.UI.RequestSetDialogueUIVisible(false);

        // Reset generic dialogue camera priority
        if (dialogueCamera != null)
        {
            dialogueCamera.Priority.Value = inactiveCameraPriority;
        }

        // Reset custom cinematic camera priority (Level 2) if active
        if (activeCustomDialogueCamera != null)
        {
            activeCustomDialogueCamera.Priority.Value = inactiveCameraPriority;
            activeCustomDialogueCamera = null;
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

        yield return new WaitForSeconds(0.8f);

        CleanupEyePivots();

        currentDialogue = null;
        currentNodeIndex = 0;
        isAwaitingChoice = false;
        isWaitingForAction = false;
        currentActiveChoices.Clear();

        // Return to the exact game state cached before starting dialogue (Exploration, Cutscene, etc.)
        GameEvents.GameState.RequestGameStateChange(_cachedPreviousGameState);
    }

    #endregion

    #region Camera Focus Helpers (3-Levels Architecture)

    private void SetupDialogueCamera(GameObject targetElement)
    {
        activeCustomDialogueCamera = null;

        // Check if a DialogueFocusProvider is attached to the interactable element
        DialogueFocusProvider focusProvider = targetElement != null ? targetElement.GetComponent<DialogueFocusProvider>() : null;

        if (focusProvider == null)
        {
            // LEVEL 0: No Provider (Text-only / Cutscene overlay mode)
            if (dialogueCamera != null)
            {
                dialogueCamera.Priority.Value = inactiveCameraPriority;
            }
            return;
        }

        // LEVEL 2: Custom Cinematic Camera defined on the provider
        if (focusProvider.CustomDialogueCamera != null)
        {
            activeCustomDialogueCamera = focusProvider.CustomDialogueCamera;

            if (dialogueCamera != null)
            {
                dialogueCamera.Priority.Value = inactiveCameraPriority;
            }

            activeCustomDialogueCamera.Priority.Value = activeCameraPriority;
            return;
        }

        // LEVEL 1: Standard NPC / Object framing using generic dialogue camera & TargetGroup
        if (dialogueCamera == null || targetElement == null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        // 1. Setup eye, framing and group look-at pivots for both Player and Target using their providers
        SetupPivotsWithProvider(playerObj.transform, targetElement.transform, playerFocusProvider, focusProvider);

        // 2. Assign targets to Cinemachine TargetGroup (Separating LookAt target from Framing bounds)
        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();

            // Player framing target (Controls zoom/bounds including feet, using active hero configuration)
            if (playerFramingPivot != null)
            {
                float playerRadius = playerFocusProvider != null ? playerFocusProvider.TargetRadius : 0.8f;
                targetGroup.Targets.Add(new CinemachineTargetGroup.Target
                {
                    Object = playerFramingPivot.transform,
                    Weight = 1f,
                    Radius = playerRadius
                });
            }

            // Target framing center (Controls zoom and bounds with dynamic radius including base/feet)
            if (targetFramingPivot != null)
            {
                targetGroup.Targets.Add(new CinemachineTargetGroup.Target
                {
                    Object = targetFramingPivot.transform,
                    Weight = 1f,
                    Radius = focusProvider.TargetRadius > 0f ? focusProvider.TargetRadius : 0.8f
                });
            }

            // Follow tracks the TargetGroup center (body framing), LookAt targets the precise middle point between both eye levels
            dialogueCamera.Follow = targetGroup.transform;
            dialogueCamera.LookAt = groupLookAtPivot != null ? groupLookAtPivot.transform : targetEyePivot.transform;
        }

        // 3. Align dialogue camera start position with main exploration camera
        if (Camera.main != null)
        {
            Vector3 startPos = Camera.main.transform.position;
            Quaternion startRot = Camera.main.transform.rotation;

            dialogueCamera.transform.position = startPos;
            dialogueCamera.transform.rotation = startRot;

            dialogueCamera.ForceCameraPosition(startPos, startRot);
        }

        // 4. Elevate generic dialogue camera priority
        dialogueCamera.Priority.Value = activeCameraPriority;
    }

    private void SetupPivotsWithProvider(Transform playerTransform, Transform targetTransform, DialogueFocusProvider playerProvider, DialogueFocusProvider targetProvider)
    {
        CleanupEyePivots();

        // 1. Setup Player Eye Pivot
        float playerEyeHeight = playerProvider != null ? playerProvider.EyeHeightOffset : 1.4f;
        playerEyePivot = new GameObject("Player_EyePivot");
        playerEyePivot.transform.position = playerTransform.position + Vector3.up * playerEyeHeight;
        playerEyePivot.transform.SetParent(playerTransform, true);

        // 2. Setup Player Framing Pivot (For group bounding including feet)
        float playerFramingCenter = playerProvider != null ? playerProvider.FramingCenterOffset : 1.0f;
        playerFramingPivot = new GameObject("Player_FramingPivot");
        playerFramingPivot.transform.position = playerTransform.position + Vector3.up * playerFramingCenter;
        playerFramingPivot.transform.SetParent(playerTransform, true);

        // 3. Setup Target Eye Pivot
        targetEyePivot = new GameObject("Target_EyePivot");
        targetEyePivot.transform.position = targetTransform.position + Vector3.up * targetProvider.EyeHeightOffset;
        targetEyePivot.transform.SetParent(targetTransform, true);

        // 4. Setup Target Framing Pivot
        targetFramingPivot = new GameObject("Target_FramingPivot");
        targetFramingPivot.transform.position = targetTransform.position + Vector3.up * targetProvider.FramingCenterOffset;
        targetFramingPivot.transform.SetParent(targetTransform, true);

        // 5. Setup Group Look-At Pivot (Calculates the exact middle point between Player's Eyes and Target's Eyes)
        groupLookAtPivot = new GameObject("Group_LookAtPivot");
        Vector3 middleEyePosition = Vector3.Lerp(playerEyePivot.transform.position, targetEyePivot.transform.position, 0.5f);
        groupLookAtPivot.transform.position = middleEyePosition;
        // Parent it to the target element or leave in world space so it sits stably between them
        groupLookAtPivot.transform.SetParent(targetTransform, true);
    }

    private void CleanupEyePivots()
    {
        if (playerEyePivot != null)
        {
            Destroy(playerEyePivot);
            playerEyePivot = null;
        }

        if (playerFramingPivot != null)
        {
            Destroy(playerFramingPivot);
            playerFramingPivot = null;
        }

        if (targetEyePivot != null)
        {
            Destroy(targetEyePivot);
            targetEyePivot = null;
        }

        if (targetFramingPivot != null)
        {
            Destroy(targetFramingPivot);
            targetFramingPivot = null;
        }

        if (groupLookAtPivot != null)
        {
            Destroy(groupLookAtPivot);
            groupLookAtPivot = null;
        }
    }

    #endregion
}