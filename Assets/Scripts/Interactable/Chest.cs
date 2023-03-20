using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : Interactable
{
    public string itemSlug;
    bool chestOpened;

    private GameObject player;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
    }

    public override void Interact()
    {
        StartCoroutine(OpenChest());
       
    }

    private IEnumerator OpenChest()
    {
        Item itemFounded;
        itemFounded = ItemDatabase.Instance.GetItem(itemSlug);
        //No Interactable anymore
        gameObject.tag = "Untagged";

        //Stop player movement
        player.GetComponent<PlayerController>().StopPlayerController = true;
        player.GetComponent<Animator>().SetTrigger("pickChest");

        yield return new WaitForSeconds(1f);
        Animator anim = gameObject.GetComponent<Animator>();
        anim.SetTrigger("openChest");

        //Wait end of animation
        yield return new WaitForSeconds(2);

        // ITEM OBTAINED
        GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.ITEM_FOUNDED_SOUND);
        StartCoroutine(notifController.AddNotification($"Has obtenido: {itemFounded.ItemName}", itemFounded.Icon));
        InventoryController.Instance.AddItem(itemSlug);

        yield return new WaitForSeconds(1);

        player.GetComponent<PlayerController>().StopPlayerController = false;

    }
}
