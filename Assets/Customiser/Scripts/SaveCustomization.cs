using UMA.CharacterSystem;
using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

// Method that allows to save the custmisation into storage and save the download URL
// for each user gameState
public class SaveCustomization : MonoBehaviour
{
    public Button saveButton;
    public DynamicCharacterAvatar avatar;
    public Text feedbackText;

    private AppAuth auth;
    private AppFirestore db;
    private AppStorage storage;

    /// <summary>Fires once the save flow reaches a terminal state (success or failure),
    /// including the "not logged in" early-out - lets callers (SceneManagerCustomisation)
    /// wait for the actual result instead of guessing a fixed delay.</summary>
    public event System.Action<bool> SaveCompleted;

    void Awake()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveAvatarCustomization);
        }

        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
        storage = AppStorage.DefaultInstance;
    }

    // Using the current authentication state to save the customisation to storage and firebase
    public void SaveAvatarCustomization()
    {
        // Read auth.CurrentUser live rather than a value cached once in Awake(): on WebGL,
        // Firebase Auth's session restore is asynchronous, so a cached value can be stuck at
        // null forever even for a genuinely signed-in user if Awake() ran before that restore
        // finished.
        AppUser currentUser = auth.CurrentUser;
        if (currentUser == null)
        {
            Debug.LogError("No user is logged in.");
            ShowFeedback("No user is logged in.", false);
            SaveCompleted?.Invoke(false);
            return;
        }

        string avatarDataString = avatar.GetAvatarDefinition(true).ToCompressedString("|");
        SaveAvatarDataToStorage(avatarDataString, currentUser);
    }

    // Save the avatarDataString to Firebase Storage
    private void SaveAvatarDataToStorage(string avatarDataString, AppUser currentUser)
    {
        var avatarDataRef = storage.GetReference($"umaData/{currentUser.UserId}.json");
        byte[] avatarDataBytes = System.Text.Encoding.UTF8.GetBytes(avatarDataString);

        avatarDataRef.PutBytesAsync(avatarDataBytes).ContinueWithOnMainThread((task) =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error saving avatar data to Storage: " + task.Exception);
                ShowFeedback("Failed to save avatar data.", false);
                SaveCompleted?.Invoke(false);
                return;
            }

            avatarDataRef.GetDownloadUrlAsync().ContinueWithOnMainThread((urlTask) =>
            {
                if (urlTask.IsFaulted || urlTask.IsCanceled)
                {
                    Debug.LogError("Error getting avatar data download URL: " + urlTask.Exception);
                    ShowFeedback("Failed to retrieve customisation.", false);
                    SaveCompleted?.Invoke(false);
                    return;
                }

                string downloadUrl = urlTask.Result.ToString();
                SaveDownloadUrlToFirestore(downloadUrl, currentUser);
            });
        });
    }

    // then saving the URL in Firestore gameState
    private void SaveDownloadUrlToFirestore(string downloadUrl, AppUser currentUser)
    {
        AppDocumentReference userDocRef = db.Collection("gameStates").Document(currentUser.UserId);
        userDocRef.UpdateAsync("playerCustomisation", downloadUrl).ContinueWithOnMainThread((updateTask) =>
        {
            if (updateTask.IsFaulted || updateTask.IsCanceled)
            {
                // If the document doesn't exist yet the update fails - fall back to creating it.
                // (The AppFirestore facade surfaces a plain Exception on every platform rather
                // than the native FirestoreException/FirestoreError type, so we no longer branch
                // on the specific error code - any update failure just retries as a create.)
                userDocRef.SetAsync(new Dictionary<string, object> { { "playerCustomisation", downloadUrl } })
                    .ContinueWithOnMainThread((setTask) =>
                {
                    if (setTask.IsFaulted || setTask.IsCanceled)
                    {
                        Debug.LogError("Error creating new gameState in Firestore: " + setTask.Exception);
                        ShowFeedback("Failed to create customisation.", false);
                        SaveCompleted?.Invoke(false);
                    }
                    else
                    {
                        Debug.Log("GameState created and customisation saved successfully.");
                        ShowFeedback("Customization saved successfully.", true);
                        SaveCompleted?.Invoke(true);
                    }
                });
            }
            else
            {
                Debug.Log("Download URL saved to Firestore successfully.");
                ShowFeedback("Customization saved successfully.", true);
                SaveCompleted?.Invoke(true);
            }
        });
    }

    // Method to display messages to user UI feedback field
    public void ShowFeedback(string message, bool isSuccess)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isSuccess ? Color.green : Color.red;
            StartCoroutine(ClearFeedbackAfterDelay(3));
        }
    }

    // Method to clear feedback
    private IEnumerator ClearFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackText != null)
        {
            feedbackText.text = "";
        }
    }
}