using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sounds : MonoBehaviour
{

    AudioClip _audio;
    AudioSource audioSource;
    [SerializeField] AudioClip[] sounds;
       


    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        _audio = audioSource.clip;
    }

    public void PlaySound (int index = 0)
    {
        if (sounds.Length > 0)
        {
            audioSource.PlayOneShot(sounds[index]);
        } else
        {
            audioSource.PlayOneShot(_audio);

        }
    }

    public void StopSound()
    {
        audioSource.Stop();
    }


}
