using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Cinemachine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    #region Serialized Fields

    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject namePanel;

    [Header("Camera Control")]
    [SerializeField] private CinemachineVirtualCamera dialogueCamera;
    [SerializeField] private CinemachineTargetGroup targetGroup;
    [SerializeField] private int activeCameraPriority = 20;
    [SerializeField] private int inactiveCameraPriority = 0;

    #endregion

    #region Private Fields

    private int dialogueIndex;
    private bool keyPressed;
    private string currentNpcName;
    private GameObject currentInteractableElement;
    private List<string> dialogueLines = new List<string>();

    #endregion

    #region Unity Lifecycle

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

        // Auto-find references if missing in Inspector
        if (dialoguePanel == null) dialoguePanel = GameObject.Find("DialoguePanel");
        if (dialogueText == null && dialoguePanel != null) dialogueText = dialoguePanel.transform.Find("DialogueText")?.GetComponent<TextMeshProUGUI>();
        if (nameText == null && dialoguePanel != null) nameText = dialoguePanel.transform.Find("NamePanel")?.GetComponentInChildren<TextMeshProUGUI>();

        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority = inactiveCameraPriority;
        }
    }

    private void Update()
    {
        // Only process dialogue progression if in InDialogue state
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InDialogue) return;

        if (Input.GetKeyDown(GameManager.Instance.KeyConfirm) && !keyPressed)
        {
            keyPressed = true;
            GameManager.Instance.GetComponent<Sounds>()?.PlaySound(GameManager.MOVE_SOUND);
            ContinueDialogue();
        }

        if (Input.GetKeyUp(GameManager.Instance.KeyConfirm))
        {
            keyPressed = false;
        }
    }

    #endregion

    #region Dialogue System Flow

    /// <summary>
    /// Starts a dialogue sequence and sets game state to InDialogue.
    /// </summary>
    public void StartDialogue(string[] lines, string name = null, GameObject element = null)
    {
        if (lines == null || lines.Length == 0) return;

        // 1. Set Game State to InDialogue
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.InDialogue);
        }

        keyPressed = true;
        dialogueIndex = 0;
        dialogueLines = new List<string>(lines);
        currentNpcName = name;
        currentInteractableElement = element;

        // 2. Position and activate dialogue camera focus
        FocusDialogueCamera(element);

        // 3. Render initial line and panel
        DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        if (dialogueText != null)
        {
            dialogueText.SetText(dialogueLines[dialogueIndex]);
        }

        if (!string.IsNullOrEmpty(currentNpcName))
        {
            if (nameText != null) nameText.SetText(currentNpcName);
            if (namePanel != null) namePanel.SetActive(true);
        }
        else
        {
            if (namePanel != null) namePanel.SetActive(false);
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
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
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        // Bajar prioridad de la cámara de diálogo
        if (dialogueCamera != null) dialogueCamera.Priority = inactiveCameraPriority;

        // Notificar al NPC para soltar la mirada
        if (currentInteractableElement != null && currentInteractableElement.TryGetComponent<NPC>(out var npc))
        {
            npc.EndDialogueLook();
        }

        yield return new WaitForSeconds(0.3f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);
        }
    }

    #endregion

    #region Camera Focus Helpers

    private void FocusDialogueCamera(GameObject npcTarget)
    {
        if (dialogueCamera == null || npcTarget == null) return;

        if (targetGroup != null && PlayerController.Instance != null)
        {
            // 1. Reconstruir el grupo
            targetGroup.m_Targets = new CinemachineTargetGroup.Target[0];
            targetGroup.AddMember(PlayerController.Instance.transform, 1f, 1.2f);
            targetGroup.AddMember(npcTarget.transform, 1f, 1.2f);

            // 2. Asignar los objetivos a la camara
            dialogueCamera.Follow = targetGroup.transform;
            dialogueCamera.LookAt = targetGroup.transform;

            // 3. Orientar la camara para que el desplazamiento lateral sea SIEMPRE perpendicular a la linea de mirada
            Vector3 playerToNpc = (npcTarget.transform.position - PlayerController.Instance.transform.position).normalized;
            Vector3 sideVector = Vector3.Cross(Vector3.up, playerToNpc).normalized;

            // Posicionamos el offset de la camara a un lado de la linea de mirada
            Vector3 midPoint = (PlayerController.Instance.transform.position + npcTarget.transform.position) * 0.5f;
            Vector3 desiredCameraPos = midPoint + (sideVector * 2.5f) + (Vector3.up * 1.8f) - (playerToNpc * 2.0f);

            dialogueCamera.transform.position = desiredCameraPos;
        }

        // 4. Activar la camara de dialogo
        dialogueCamera.Priority = activeCameraPriority;
    }

    #endregion
}