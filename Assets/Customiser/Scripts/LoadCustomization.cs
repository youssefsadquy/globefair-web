using UMA.CharacterSystem;
using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

// Script that allows to load presaved avatar customisation from current user collection on firebase
public class LoadCustomization : MonoBehaviour
{
    public DynamicCharacterAvatar avatar;
    public Button loadButton;
    public Text feedbackText;

    private AppAuth auth;
    private AppFirestore db;
    private AppStorage storage;

    // Automatically loading the avatar if exists
    void Awake()
    {
        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
        storage = AppStorage.DefaultInstance;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (loadButton != null)
        {
            loadButton.onClick.AddListener(LoadAvatarCustomization);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LoadAvatarCustomization();
    }

    // Fetching the customisation using the authentication state
    public void LoadAvatarCustomization()
    {
        // Read auth.CurrentUser live at the point of use rather than caching it once in
        // Awake(): on WebGL, Firebase Auth's session restore is asynchronous (it checks
        // IndexedDB before it knows whether there's a returning signed-in user), so a value
        // cached at Awake() can be stuck at null forever even for a user who is genuinely
        // signed in, if Awake() happened to run before that restore finished.
        AppUser currentUser = auth.CurrentUser;
        if (currentUser == null)
        {
            Debug.LogError("No user is logged in.");
            ShowFeedback("No user is logged in.", false);
            return;
        }

        var avatarDataRef = storage.GetReference($"umaData/{currentUser.UserId}.json");
        avatarDataRef.GetBytesAsync(long.MaxValue).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error getting avatar data from Storage: " + task.Exception);
                ShowFeedback("Failed to load avatar customization.", false);
                return;
            }

            // Deserialize to string
            string avatarDataString = System.Text.Encoding.UTF8.GetString(task.Result);
            avatar.ChangeRace(avatar.activeRace.name);
            // Load the avatar data from the string
            AvatarDefinition avatarDef = AvatarDefinition.FromCompressedString(avatarDataString, '|');
            avatar.LoadAvatarDefinition(avatarDef);
            avatar.BuildCharacter();
            ShowFeedback("Avatar customization loaded successfully.", true);
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

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}