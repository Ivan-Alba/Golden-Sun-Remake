using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationController : MonoBehaviour
{

    FighterController fighterController;
    private AudioSource audioSource;

    // Start is called before the first frame update
    void Awake()
    {
        if (GetComponent<FighterController>() != null)
        {
            fighterController = GetComponent<FighterController>();
        }
        else
        {
            fighterController = transform.parent.GetComponent<FighterController>();
        }

        audioSource = GetComponent<AudioSource>();

    }


    public void AttackAnimationDone()
    {
        fighterController.AttackDone();
    }

    public void playFootprints()
    {
        if (audioSource == null) return;

        audioSource.pitch = Random.Range(0.9f, 1.1f);

        audioSource.PlayOneShot(audioSource.clip);
    }

}
