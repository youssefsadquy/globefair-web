using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

// Signup method using firebase authentication to create business users and collection
public class SignupBusinessManager : MonoBehaviour
{
    public InputField businessNameInputField;
    public InputField emailInputField;
    public InputField passwordInputField;
    public InputField confirmPasswordInputField;
    public Button signupButton;
    public Text feedbackText;

    public GameObject signupCanvas;
    public GameObject businessDashboardCanvas;

    private AppAuth auth;
    private AppFirestore db;

    void Awake()
    {
        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
    }

    void Start()
    {
        signupButton.onClick.AddListener(TryBusinessSignup);
    }

    // Using the populated fields create the user
    public void TryBusinessSignup()
    {
        string businessName = businessNameInputField.text.Trim();
        string email = emailInputField.text.Trim();
        string password = passwordInputField.text;
        string confirmPassword = confirmPasswordInputField.text;

        if (string.IsNullOrEmpty(businessName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("All fields are required.", false);
            return;
        }

        if (password != confirmPassword)
        {
            ShowFeedback("Passwords do not match.", false);
            return;
        }

        // Firebase Authentication sign-up
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

            // Firebase user created then add to Firestore
            AppUser newBusinessUser = task.Result.User;
            AppDocumentReference docRef = db.Collection("bus_users").Document(newBusinessUser.UserId);
            Dictionary<string, object> businessUserData = new Dictionary<string, object>
            {
                { "BusinessName", businessName },
                { "Email", email }
            };

            docRef.SetAsync(businessUserData).ContinueWithOnMainThread(firestoreTask =>
            {
                if (firestoreTask.IsCanceled)
                {
                    ShowFeedback("Failed to add business user details.", false);
                    return;
                }
                if (firestoreTask.IsFaulted)
                {
                    ShowFeedback("Failed to add business user details", false);
                    return;
                }
                ShowFeedback("Business signup successful! Logging in...", true);
                SignInUser(email, password);
            });
        });
    }

    // After successfull signup the user is signed in immediatelly
    private void SignInUser(string email, string password)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                return;
            }

            // User signed in successfully, redirect to business dashboard
            Debug.Log("User signed in successfully");
            ShowBusinessDashboard();
        });
    }

    private void ResetFieldsAndFeedback()
    {
        // Reset input fields
        businessNameInputField.text = "";
        emailInputField.text = "";
        passwordInputField.text = "";
        confirmPasswordInputField.text = "";

        // Reset feedback message
        feedbackText.text = "";
    }

    // Showing appropriate dashboard
    private void ShowBusinessDashboard()
    {
        signupCanvas.SetActive(false);
        businessDashboardCanvas.SetActive(true);

        ResetFieldsAndFeedback();
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
        signupButton.onClick.RemoveListener(TryBusinessSignup);
    }
}