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

    // Virtual framing targets for eye-level camera focus
    private GameObject playerEyePivot;
    private GameObject npcEyePivot;

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

        CleanupEyePivots();
    }

    #endregion

    #region Event Callbacks

    private void HandleDialogueAdvance()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InDialogue) return;

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

        CleanupEyePivots();

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

            // Force Cinemachine to warp internal state to current exploration position instantly (No first-time camera jump)
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