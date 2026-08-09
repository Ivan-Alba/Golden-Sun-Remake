using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC : Interactable
{
    [Header("NPC Settings")]
    [SerializeField] private string npcName = "";
    [SerializeField] private int rotationSpeed = 4;

    [Header("Dialogue Content")]
    public string[] dialogue;

    private GameObject player;
    private bool lookAtPlayer;
    private Quaternion initialRotation;

    // Getters and setters
    public bool LookAtPlayer { get => lookAtPlayer; set => lookAtPlayer = value; }
    public string NpcName { get => npcName; set => npcName = value; }

    private void Awake()
    {
        if (string.IsNullOrEmpty(npcName)) npcName = "";

        // Cache initial spawn rotation
        initialRotation = transform.rotation;
    }

    private void Update()
    {
        if (LookAtPlayer)
        {
            // Lazy search if player reference is missing
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            if (player != null)
            {
                // Calculate horizontal direction to prevent vertical tilt on slopes/hills
                Vector3 direction = player.transform.position - transform.position;
                direction.y = 0;

                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }
        else
        {
            // Return smoothly to original orientation
            transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, rotationSpeed * Time.deltaTime);
        }
    }

    public override void Interact()
    {
        // Ensure player is cached at interaction time
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        // Lock rotation and look at player
        initialRotation = transform.rotation;
        LookAtPlayer = true;

        // Trigger Dialogue System
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.AddNewDialogue(dialogue, npcName, gameObject);
        }

        // Debug Command
        if (npcName == "DEBUG_ALL_ITEMS" && InventoryController.Instance != null)
        {
            InventoryController.Instance.AllItems();
        }
    }
}