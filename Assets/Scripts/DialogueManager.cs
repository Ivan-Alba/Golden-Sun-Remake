using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    private GameObject dialoguePanel;
    private TextMeshProUGUI dialogueText, nameText;
    private int dialogueIndex;
    private bool inDialogue;
    private string _name;
    private bool keyPressed;

    private GameObject element = null;
    private List<string> dialogueLines = new List<string>();

    //Getters and setters
    public bool InDialogue { get => inDialogue; set => inDialogue = value; }

    void Awake()
    {
        
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        } else
        {
            instance = this;
        }

        dialoguePanel = GameObject.Find("DialoguePanel");
        dialogueText = dialoguePanel.transform.Find("DialogueText").GetComponent<TextMeshProUGUI>();
        nameText = dialoguePanel.transform.Find("NamePanel").GetComponentInChildren<TextMeshProUGUI>();
        dialoguePanel.SetActive(false);

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(GameManager.Instance.KeyConfirm) && !keyPressed && inDialogue) {
            keyPressed = true;
            GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
            ContinueDialogue();
        }

        if (Input.GetKeyUp(GameManager.Instance.KeyConfirm))
        {
            keyPressed = false;
        }
    }

    public void AddNewDialogue(string[] lines, string name = null, GameObject element = null)
    {
        keyPressed = true;
        inDialogue = true;
        dialogueIndex = 0;
        dialogueLines = new List<string>(lines.Length);
        dialogueLines.AddRange(lines);
        this._name = name;
        this.element = element;
        CreateDialogue();

    }

    private void CreateDialogue()
    {
        dialogueText.SetText(dialogueLines[dialogueIndex]);
        if (_name != null)
        {
            nameText.SetText(_name);
            nameText.transform.parent.gameObject.SetActive(true);
        } else
        {
            nameText.transform.parent.gameObject.SetActive(false);
        }
        dialoguePanel.SetActive(true);

    }

    private void ContinueDialogue()
    {
        Debug.Log("ContinueDialogue");
        if (dialogueIndex < dialogueLines.Count -1)
        {
            dialogueIndex++;
            CreateDialogue();
        } else
        {
            StartCoroutine(CloseDialogue());
        }
    }

    private IEnumerator CloseDialogue()
    {
        dialoguePanel.SetActive(false);

        if (element != null && element.GetComponent<NPC>() != null) // IF NPC
        {
            element.GetComponent<NPC>().LookAtPlayer = false;

        }
        /** if (npc != null)
         {
             npc.LookAtPlayer = false;
         }
        **/

        yield return new WaitForSeconds(0.3f);
        inDialogue = false;
    }
}
