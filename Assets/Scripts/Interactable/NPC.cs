using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC : Interactable
{
    GameObject player;

    //LOKATPLAYER
    bool lookAtPlayer;
    Quaternion initialRotation;
    int rotationSpeed = 4;

    
    // TODO: DIALOGUE (SEARCH HOW TO USE JSON FILES)
    [SerializeField] string npcName;
    public string[] dialogue;

    //Getters and setters
    public bool LookAtPlayer { get => lookAtPlayer; set => lookAtPlayer = value; }
    public string NpcName { get => npcName; set => npcName = value; }

    private void Awake()
    {
        if (npcName == null) npcName = "";

            player = GameObject.FindGameObjectWithTag("Player");
    }

    private void Update()
    {
        if (LookAtPlayer)
        {
            Quaternion targetRotation = Quaternion.LookRotation(player.transform.position - transform.position);
            // Smoothly rotate towards the target point.
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, rotationSpeed * Time.deltaTime);
        }

    }
    public override void Interact()
    {
        //LOOK AT PLAYER
        initialRotation = transform.rotation;
        LookAtPlayer = true;
        //transform.LookAt(new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z));

        //DIALOG
        DialogueManager.instance.AddNewDialogue(dialogue, this.npcName, gameObject);
        if  (this.npcName == "DEBUG_ALL_ITEMS")
        {
            InventoryController.Instance.AllItems();
        }

    }

}
