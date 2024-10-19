using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] AudioClip playCardSFX;
    [SerializeField] AudioClip shufflingSFX;

    AudioSource myAudioSource;

    void Awake()
    {
        myAudioSource = GetComponent<AudioSource>();
    }

    public void PlayCardSFX()
    {
        myAudioSource.clip = playCardSFX;
        myAudioSource.Play();
    }

    public void PlayShufflingSFX()
    {
        myAudioSource.clip = shufflingSFX;
        myAudioSource.Play();
    }
}
