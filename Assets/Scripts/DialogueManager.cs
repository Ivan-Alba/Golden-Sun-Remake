using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

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

    private int dialogueIndex;
    private string currentNpcName;
    private Sprite currentNpcPortrait;
    private GameObject currentInteractableElement;
    private List<string> dialogueLines = new List<string>();

    private Coroutine typewriterCoroutine;
    private bool isTyping = false;
    private string currentFullLine = "";

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority.Value = inactiveCameraPriority;
        }
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDialogueAdvancePressed += HandleDialogueAdvance;
        }
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDialogueAdvancePressed -= HandleDialogueAdvance;
        }
    }

    #endregion

    #region Event Callbacks

    private void HandleDialogueAdvance()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InDialogue) return;

        // If typewriter effect is still running, complete the sentence immediately
        if (isTyping)
        {
            CompleteCurrentLine();
            return;
        }

        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
        ContinueDialogue();
    }

    #endregion

    #region Dialogue System Flow

    /// <summary>
    /// Starts a dialogue sequence and sets game state to InDialogue.
    /// </summary>
    public void StartDialogue(string[] lines, string name = null, GameObject element = null, Sprite portrait = null)
    {
        if (lines == null || lines.Length == 0) return;

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InDialogue);

        dialogueIndex = 0;
        dialogueLines = new List<string>(lines);
        currentNpcName = name;
        currentNpcPortrait = portrait;
        currentInteractableElement = element;

        FocusDialogueCamera(element);
        DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        currentFullLine = dialogueLines[dialogueIndex];
        typewriterCoroutine = StartCoroutine(TypewriterRoutine(currentFullLine));
    }

    private IEnumerator TypewriterRoutine(string line)
    {
        isTyping = true;
        float delay = 1f / charactersPerSecond;

        for (int i = 0; i <= line.Length; i++)
        {
            string currentSubstring = line.Substring(0, i);

            if (PlayerUIManager.Instance != null)
            {
                PlayerUIManager.Instance.UpdateDialogueUI(currentNpcName, currentSubstring, currentNpcPortrait);
            }

            // TODO: Optional retro dialogue bleeps via GameEvents.Audio.RequestPlaySFX(...)

            yield return new WaitForSeconds(delay);
        }

        isTyping = false;
    }

    private void CompleteCurrentLine()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        isTyping = false;

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.UpdateDialogueUI(currentNpcName, currentFullLine, currentNpcPortrait);
        }
    }

    private void ContinueDialogue()
    {
        if (dialogueIndex < dialogueLines.Count - 1)
        {
            dialogueIndex++;
            DisplayCurrentLine();
        }
        else
        {
            StartCoroutine(CloseDialogueRoutine());
        }
    }

    private IEnumerator CloseDialogueRoutine()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority.Value = inactiveCameraPriority;
        }

        if (currentInteractableElement != null && currentInteractableElement.TryGetComponent<NPC>(out var npc))
        {
            npc.EndDialogueLook();
        }

        yield return new WaitForSeconds(0.3f);

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
    }

    #endregion

    #region Camera Focus Helpers

    private void FocusDialogueCamera(GameObject npcTarget)
    {
        if (dialogueCamera == null || npcTarget == null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        Transform playerTransform = playerObj.transform;

        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();

            // Add player target with height offset weighting
            targetGroup.Targets.Add(new CinemachineTargetGroup.Target
            {
                Object = playerTransform,
                Weight = 1f,
                Radius = 1.2f
            });

            // Add NPC target
            targetGroup.Targets.Add(new CinemachineTargetGroup.Target
            {
                Object = npcTarget.transform,
                Weight = 1f,
                Radius = 1.2f
            });

            dialogueCamera.Follow = targetGroup.transform;
            dialogueCamera.LookAt = targetGroup.transform;

            // Calculate camera framing vector offset based on eye level midpoint
            Vector3 playerEyePos = playerTransform.position + Vector3.up * eyeHeightOffset;
            Vector3 npcEyePos = npcTarget.transform.position + Vector3.up * eyeHeightOffset;

            Vector3 playerToNpc = (npcEyePos - playerEyePos).normalized;
            Vector3 sideVector = Vector3.Cross(Vector3.up, playerToNpc).normalized;

            Vector3 midPoint = (playerEyePos + npcEyePos) * 0.5f;
            Vector3 desiredCameraPos = midPoint + (sideVector * 2.5f) - (playerToNpc * 2.0f);

            dialogueCamera.transform.position = desiredCameraPos;
        }

        dialogueCamera.Priority.Value = activeCameraPriority;
    }

    #endregion
}