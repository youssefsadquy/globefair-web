using UnityEngine;
using UnityEngine.UI;
using AppFirebase;
using System.Collections;
using System.Collections.Generic;

// Script to allow users to leave a feedback and to save it on a collection in firebase
public class FeedbackHandler : MonoBehaviour
{
    public GameObject feedbackPanel;
    public InputField nameInput;
    public InputField emailInput;
    public InputField feedbackInput;
    public Text feedbackText;

    private AppFirestore db;

    void Start()
    {
        feedbackPanel.SetActive(false);
        db = AppFirestore.DefaultInstance;
    }

    public void OpenFeedbackPanel()
    {
        feedbackPanel.SetActive(true);
    }

    public void CloseFeedbackPanel()
    {
        feedbackPanel.SetActive(false);
    }

    public void SubmitFeedback()
    {
        // Check if all fields are completed
        if (string.IsNullOrWhiteSpace(nameInput.text) ||
            string.IsNullOrWhiteSpace(emailInput.text) ||
            string.IsNullOrWhiteSpace(feedbackInput.text))
        {
            ShowFeedback("All fields are required.", false);
            return;
        }

        // Check if the email format is valid
        if (!IsValidEmail(emailInput.text))
        {
            ShowFeedback("Please enter a valid email address.", false);
            return;
        }

        // Creating a feedback document on firebase
        AppDocumentReference docRef = db.Collection("feedback").Document();
        Dictionary<string, object> feedback = new Dictionary<string, object>
        {
            { "name", nameInput.text },
            { "email", emailInput.text },
            { "message", feedbackInput.text }
        };

        docRef.SetAsync(feedback).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted)
            {
                ShowFeedback("Feedback submitted successfully.", true);
                ClearInputFields();
            }
            else
            {
                ShowFeedback("Error submitting feedback.", false);
            }
        });
    }

    // Method to validate email using regular expression
    private bool IsValidEmail(string email)
    {
        System.Text.RegularExpressions.Regex emailRegex = new System.Text.RegularExpressions.Regex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
        return emailRegex.IsMatch(email);
    }

    // Displaying feedback conditional
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

    private IEnumerator ClearFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        feedbackText.text = "";
    }

    // Method to clear the input fields
    private void ClearInputFields()
    {
        nameInput.text = "";
        emailInput.text = "";
        feedbackInput.text = "";
    }
}