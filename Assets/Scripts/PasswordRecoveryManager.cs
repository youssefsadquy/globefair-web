using UnityEngine;
using UnityEngine.UI;
using AppFirebase;
using System.Collections;

// Script that uses firebase SendPasswordResetEmail method to recover and change account password
public class PasswordRecoveryManager : MonoBehaviour
{
    public InputField recoveryEmailInputField;
    public Button sendRecoveryEmailButton;
    public Text feedbackText;

    private AppAuth auth;

    void Awake()
    {
        auth = AppAuth.DefaultInstance;
    }

    void Start()
    {
        sendRecoveryEmailButton.onClick.AddListener(SendPasswordResetEmail);
    }

    // Method to initialise password reset with firebase
    private void SendPasswordResetEmail()
    {
        string emailAddress = recoveryEmailInputField.text.Trim();
        if (string.IsNullOrEmpty(emailAddress))
        {
            ShowFeedback("Please enter your email address.", false);
            return;
        }

        auth.SendPasswordResetEmailAsync(emailAddress).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                ShowFeedback("Password reset was canceled.", false);
                return;
            }
            if (task.IsFaulted)
            {
                ShowFeedback("Error sending password reset email", false);
                return;
            }

            ShowFeedback("If the email is registered, a reset link will be sent. Please check your inbox.", true);
            ClearInputFields();
        });
    }

    public void ClearInputFields()
    {
        recoveryEmailInputField.text = "";
    }

    // Conditional feedback
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
}
