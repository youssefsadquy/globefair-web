using UnityEngine;
using UnityEngine.UI;
using AppFirebase;
using UnityEngine.SceneManagement;

// Script that allows navigation to and from customisation scene
public class SceneManagerCustomisation : MonoBehaviour
{
    public Button backButton;
    public SaveCustomization saveCustomizationScript;

    private AppAuth auth;
    private bool _navigating;

    void Start()
    {
        backButton.onClick.AddListener(SaveAndLoadDashboard);
        auth = AppAuth.DefaultInstance;
    }

    // Saving the progress and changing scene with current authentication status.
    // Previously this used a fixed 2-second Invoke() before navigating away regardless of
    // whether the save had actually finished - on a slow connection the Storage upload +
    // Firestore write can easily take longer than that, so the scene (and the
    // SaveCustomization component on it) could be unloaded mid-save, silently losing the
    // customization. Now it waits for SaveCustomization's own SaveCompleted event instead.
    private void SaveAndLoadDashboard()
    {
        if (_navigating) return;
        _navigating = true;

        backButton.interactable = false;
        saveCustomizationScript.SaveCompleted += OnSaveCompleted;
        saveCustomizationScript.SaveAvatarCustomization();
    }

    private void OnSaveCompleted(bool success)
    {
        saveCustomizationScript.SaveCompleted -= OnSaveCompleted;
        // Navigate regardless of success/failure (matches the previous always-navigate
        // behaviour) - SaveCustomization already surfaced any failure via its own feedback
        // text before this fires. A short delay just lets that feedback be visible for a beat.
        Invoke(nameof(LoadDashboard), 0.5f);
    }

    private void LoadDashboard()
    {
        PlayerPrefs.SetInt("LoadDashboard", 1);
        SceneManager.LoadScene("Landing");
    }

    void OnDestroy()
    {
        if (saveCustomizationScript != null)
        {
            saveCustomizationScript.SaveCompleted -= OnSaveCompleted;
        }
    }
}