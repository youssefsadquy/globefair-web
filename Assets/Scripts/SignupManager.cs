using UnityEngine;
using UnityEngine.UI;
using AppFirebase;
using System.Collections.Generic;
using System.Collections;

// Signup method using firebase authentication to create normal users and collection
public class SignupManager : MonoBehaviour
{
    public InputField firstNameInputField;
    public InputField emailInputField;
    public InputField passwordInputField;
    public InputField confirmPasswordInputField;
    public Button signupButton;
    public Text feedbackText;
    public GameObject userDashboardCanvas;
    public GameObject signupCanvas;

    private AppAuth auth;
    private AppFirestore db;

    void Awake()
    {
        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
    }

    void Start()
    {
        signupButton.onClick.AddListener(TrySignup);
    }

    // Using the populated fields create the user and collection with their name and email
    public void TrySignup()
    {
        string email = emailInputField.text.Trim();
        string password = passwordInputField.text;
        string confirmPassword = confirmPasswordInputField.text;
        string firstName = firstNameInputField.text.Trim();

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("All fields are required.", false);
            return;
        }

        if (password != confirmPassword)
        {
            ShowFeedback("Passwords do not match.", false);
            return;
        }

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                ShowFeedback("Signup was canceled.", false);
                return;
            }
            if (task.IsFaulted)
            {
                ShowFeedback("Signup encountered an error", false);
                return;
            }

            AppUser newUser = task.Result.User;
            AppDocumentReference docRef = db.Collection("users").Document(newUser.UserId);
            Dictionary<string, object> newUserMap = new Dictionary<string, object>
            {
                { "FirstName", firstName },
                { "Email", email }
            };

            docRef.SetAsync(newUserMap).ContinueWithOnMainThread(firestoreTask =>
            {
                if (firestoreTask.IsCanceled)
                {
                    ShowFeedback("Failed to add user details.", false);
                    return;
                }
                if (firestoreTask.IsFaulted)
                {
                    ShowFeedback("Failed to add user details to Firestore", false);
                    return;
                }
                // Sign in the user
                ShowFeedback("Signup successful! Logging in...", true);
                SignInUser(email, password);
            });
        });
    }

    // Signing in the user immediately on successfull signup
    private void SignInUser(string email, string password)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync was canceled.");
                ShowFeedback("Error occurred. Proceed with manual login", true);
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                ShowFeedback("Error occurred. Proceed with manual login", true);
                return;
            }

            // User signed in successfully, redirect to user dashboard
            ShowFeedback("Logged in successfully. Welcome!", true);
            ShowUserDashboard();
        });
    }

    // Showing appropriate dashboard
    private void ShowUserDashboard()
    {
        userDashboardCanvas.SetActive(true);
        signupCanvas.SetActive(false);
        ResetFieldsAndFeedback();
    }

    private void ResetFieldsAndFeedback()
    {
        // Reset input fields
        firstNameInputField.text = "";
        emailInputField.text = "";
        passwordInputField.text = "";
        confirmPasswordInputField.text = "";

        // Reset feedback message
        feedbackText.text = "";
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

    void OnDestroy()
    {
        signupButton.onClick.RemoveListener(TrySignup);
    }
}