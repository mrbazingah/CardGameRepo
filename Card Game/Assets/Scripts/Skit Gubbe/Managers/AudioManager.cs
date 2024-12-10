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
        myAudioSource.PlayOneShot(playCardSFX, 1f);
    }

    public void PlayShufflingSFX()
    {
        myAudioSource.PlayOneShot(shufflingSFX, 1f);
    }
}
