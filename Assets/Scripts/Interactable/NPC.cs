using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC : Interactable
{
    #region Serialized Fields

    [Header("NPC Settings")]
    [SerializeField] private string npcName = "";
    [SerializeField] private float rotationSpeed = 4f;

    [Header("Dialogue Content")]
    [SerializeField] private string[] dialogue;

    #endregion

    #region Private Fields

    private bool lookAtPlayer;
    private Quaternion initialRotation;

    #endregion

    #region Properties

    public string NpcName { get => npcName; set => npcName = value; }
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
        if (lookAtPlayer)
        {
            if (PlayerController.Instance != null)
            {
                // Calculate horizontal direction to prevent vertical tilt on slopes
                Vector3 direction = PlayerController.Instance.transform.position - transform.position;
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

    #endregion

    #region Interaction Logic

    public override void Interact()
    {
        // Cache rotation and start looking at player
        initialRotation = transform.rotation;
        lookAtPlayer = true;

        // Smoothly turn Player towards NPC
        if (PlayerController.Instance != null)
        {
            StartCoroutine(RotatePlayerToNpc(PlayerController.Instance.transform));
        }

        // Trigger Dialogue System
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialogue, npcName, gameObject);
        }

        // Debug Command
        if (npcName == "DEBUG_ALL_ITEMS" && InventoryController.Instance != null)
        {
            InventoryController.Instance.AllItems();
        }
    }

    /// <summary>
    /// Call this method when dialogue ends to resume normal rotation behavior.
    /// </summary>
    public void EndDialogueLook()
    {
        lookAtPlayer = false;
    }

    private IEnumerator RotatePlayerToNpc(Transform playerTransform)
    {
        Vector3 direction = transform.position - playerTransform.position;
        direction.y = 0; // Prevent tilting on vertical axes

        if (direction == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float elapsedTime = 0f;
        float duration = 0.25f; // Fast, natural response time

        Quaternion startRotation = playerTransform.rotation;

        while (elapsedTime < duration)
        {
            playerTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        playerTransform.rotation = targetRotation;
    }

    #endregion
}