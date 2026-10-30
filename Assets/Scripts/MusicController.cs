using UnityEngine;
using UnityEngine.UI;

// Music control with volume for settings menu
public class MusicController : MonoBehaviour
{
    private AudioSource audioSource;
    public Slider volumeSlider;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.Play();

        volumeSlider.value = audioSource.volume;
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    void OnVolumeChanged(float volume)
    {
        audioSource.volume = volume;
    }
}