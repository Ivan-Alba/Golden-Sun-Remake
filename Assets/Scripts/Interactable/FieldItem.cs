using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldItem : Interactable
{
    private GameObject player;
    private Animator cloackIsaacAnim;
    private Animator playerAnimator;
    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player.transform.GetChild(0).name == "Isaac_world")
        {
            cloackIsaacAnim = GameObject.Find("CloackIsaac").GetComponent<Animator>();
        }
    }
    public string itemName;
    public override void Interact()
    {
        StartCoroutine(pickItem());
    }

    private IEnumerator pickItem()
    {
        //STOPS PLAYERS MOVEMENT
        player.GetComponent<PlayerController>().StopPlayerController = true;

        player.GetComponent<Animator>().SetTrigger("pickFloor");
        //CLOACK ANIM
        //cloackIsaacAnim.SetTrigger("pick");
        yield return new WaitForSeconds(1);


        //Show notification
        if (notifController != null)
        {
            StartCoroutine(notifController.AddNotification
                ($"Has encontrado: {ItemDatabase.Instance.GetItem(itemName).ItemName}",
                ItemDatabase.Instance.GetItem(itemName).Icon));

            Debug.Log("Coges Objeto");
        }
        //Add item to inventory
        InventoryController.Instance.AddItem(itemName);
        GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.ITEM_FOUNDED_SOUND);
        //INVISIBLE
        gameObject.transform.position = new Vector3(0, -1000, 0);
        yield return new WaitForSeconds(0.7f);
        player.GetComponent<PlayerController>().StopPlayerController = false;
        Destroy(gameObject, 5f);
    }

}
