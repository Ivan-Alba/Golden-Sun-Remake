using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC : Interactable
{
    #region Serialized Fields

    [Header("NPC Settings")]
    [SerializeField] private string npcName = "";
    [SerializeField] private float rotationSpeed = 4f;
    [SerializeField] private Sprite portraitSprite = null;

    [Header("Dialogue Content")]
    [SerializeField] private string[] dialogue;

    #endregion

    #region Private Fields

    private bool lookAtPlayer;
    private Quaternion initialRotation;
    private Transform cachedPlayerTransform;

    #endregion

    #region Properties

    public string NpcName { get => npcName; set => npcName = value; }
    public Sprite PortraitSprite { get => portraitSprite; }
    public bool LookAtPlayer { get => lookAtPlayer; set => lookAtPlayer = value; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (string.IsNullOrEmpty(npcName)) npcName = "";

        // Cache initial spawn rotation
        initialRotation = transform.rotation;
    }

    private void Update()
    {
        if (lookAtPlayer && cachedPlayerTransform != null)
        {
            // Calculate horizontal direction to prevent vertical tilt on slopes
            Vector3 direction = cachedPlayerTransform.position - transform.position;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // Return smoothly to original orientation
            transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, rotationSpeed * Time.deltaTime);
        }
    }

    #endregion

    #region Interaction Logic

    public override void Interact()
    {
        // Cache rotation and player transform using Tag
        initialRotation = transform.rotation;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerTransform = playerObj.transform;
            lookAtPlayer = true;

            // Decoupled request: Tell player to face this NPC
            GameEvents.Player.RequestLookAt(transform.position, 0.25f);
        }

        // Trigger player interaction animation if needed
        GameEvents.Player.TriggerPlayInteractionAnimation(PlayerInteractionAnimType.InteractNPC);

        // Trigger Dialogue System
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialogue, npcName, gameObject, portraitSprite);
        }

        // Debug Command
        if (npcName == "DEBUG_ALL_ITEMS")
        {
            GameEvents.Inventory.RequestAllItems();
        }
    }

    /// <summary>
    /// Call this method when dialogue ends to resume normal rotation behavior.
    /// </summary>
    public void EndDialogueLook()
    {
        lookAtPlayer = false;
        cachedPlayerTransform = null;
    }

    #endregion
}