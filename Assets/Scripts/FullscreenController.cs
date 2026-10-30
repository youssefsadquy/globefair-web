using UnityEngine;

// Script to allow user to switch between full screen mode or windowed
public class FullscreenController : MonoBehaviour
{
    public CustomToggle fullscreenToggle;

    void Start()
    {
        fullscreenToggle.isOn = Screen.fullScreen;
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
    }

    // Switch screen mode
    void OnFullscreenToggleChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        Debug.Log("Application Fullscreen");
    }
}