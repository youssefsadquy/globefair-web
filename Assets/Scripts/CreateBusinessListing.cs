using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

// Method to manage business positions
public class CreateBusinessListing : MonoBehaviour
{
    public InputField jobTitleInput;
    public InputField jobDescriptionInput;
    public InputField jobLocationInput;
    public InputField salaryInput;
    public InputField howToApplyInput;
    public Button submitButton;
    public Text feedbackText;
    public Scrollbar verticalScrollbar;

    private AppFirestore db;
    private AppAuth auth;

    // Initialisation of firebase and feedback fields
    void Start()
    {
        // FirebaseApp.CheckAndFixDependenciesAsync() was an Android-only Google Play
        // Services check and doesn't exist for WebGL - Firestore/Auth are ready to use
        // immediately on every platform.
        if (feedbackText == null)
        {
            feedbackText = GameObject.FindGameObjectWithTag("FeedbackText1").GetComponent<Text>();
            if (feedbackText == null)
            {
                Debug.LogError("Failed to find the FeedbackText1 object in the scene.");
                return;
            }
        }
        db = AppFirestore.DefaultInstance;
        auth = AppAuth.DefaultInstance;
        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(CreateListing);
    }

    // Method to set assigned fields to save on firebase
    void CreateListing()
    {
        if (auth.CurrentUser == null)
        {
            Debug.LogError("No authenticated user found.");
            ShowFeedback("Please log in to create a listing.", false);
            return;
        }
        AppDocumentReference docRef = db.Collection("businessListings").Document();
        Dictionary<string, object> newListing = new Dictionary<string, object>
        {
            {"jobTitle", jobTitleInput.text},
            {"jobDescription", jobDescriptionInput.text},
            {"location", jobLocationInput.text},
            {"salary", salaryInput.text},
            {"howToApply", howToApplyInput.text},
            {"businessUid", auth.CurrentUser.UserId},
            {"creationDate", AppTimestamp.GetCurrentTimestamp()}
        };

        docRef.SetAsync(newListing).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Error adding listing: " + task.Exception);
                ShowFeedback("Failed to create listing. Please try again.", false);
            }
            else
            {
                Debug.Log("Listing added with ID: " + docRef.Id);
                ClearInputFields();
                ShowFeedback("Listing created successfully.", true);
            }
        });
    }

    // On submission the fields are cleared
    private void ClearInputFields()
    {
        jobTitleInput.text = "";
        jobDescriptionInput.text = "";
        jobLocationInput.text = "";
        salaryInput.text = "";
        howToApplyInput.text = "";
    }

    // Conditional displaying colour red/green
    private void ShowFeedback(string message, bool isSuccess)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isSuccess ? Color.green : Color.red;
            StartCoroutine(ClearFeedbackAfterDelay(3));
        }
        else
        {
            Debug.LogError("Feedback text object not found!");
        }
    }

    // Delayed disappearance of the feedback
    private IEnumerator ClearFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        feedbackText.text = "";
        ScrollToTop();
    }

    // Auto scrolling after submitting
    private void ScrollToTop()
    {
        if (verticalScrollbar)
        {
            verticalScrollbar.value = 1;
        }
    }
}