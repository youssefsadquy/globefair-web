using UnityEngine;
using UnityEngine.UI;
using AppFirebase;
using System.Collections;

// Using firebase authentication to login existing users
public class LoginManager : MonoBehaviour
{
    public InputField emailInputField;
    public InputField passwordInputField;
    public Button loginButton;
    public Text feedbackText;
    public GameObject userDashboardCanvas;
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
        loginButton.onClick.AddListener(LoginWithEmailPassword);
    }

    // Firebase method that takes email and password and validates
    private void LoginWithEmailPassword()
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Email and Password are required.", false);
            return;
        }

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                ShowFeedback("SignIn was canceled.", false);
                return;
            }
            if (task.IsFaulted)
            {
                ShowFeedback("SignIn encountered an error", false);
                return;
            }

            // Firebase user signed in successfully
            ShowFeedback("Signed in successfully. Logging in...", true);
            CheckUserCollection(task.Result.User);
        });
    }

    // Method that allows recognition type of user and redirection to correct dashboard
    private void CheckUserCollection(AppUser user)
    {
        AppDocumentReference userDocRef = db.Collection("users").Document(user.UserId);
        userDocRef.GetSnapshotAsync().ContinueWithOnMainThread(userTask =>
        {
            if (userTask.IsFaulted || userTask.IsCanceled)
            {
                ShowFeedback("Error getting user", false);
            }
            else if (userTask.Result.Exists)
            {
                ShowFeedback("Welcome back!", true);
                ShowDashboard(userDashboardCanvas);
            }
            else
            {
                AppDocumentReference busUserDocRef = db.Collection("bus_users").Document(user.UserId);
                busUserDocRef.GetSnapshotAsync().ContinueWithOnMainThread(busUserTask =>
                {
                    if (busUserTask.IsFaulted || busUserTask.IsCanceled)
                    {
                        ShowFeedback("Error getting business user", false);
                    }
                    else if (busUserTask.Result.Exists)
                    {
                        ShowFeedback("Welcome to your business dashboard!", true);
                        ShowDashboard(businessDashboardCanvas);
                    }
                    else
                    {
                        ShowFeedback("User does not exist!", false);
                    }
                });
            }
        });
    }

    // Determined dashboard and fields clearance
    private void ShowDashboard(GameObject dashboardCanvas)
    {
        dashboardCanvas.SetActive(true);
        gameObject.SetActive(false);
        ResetFieldsAndFeedback();
    }

    private void ResetFieldsAndFeedback()
    {
        // Reset input fields
        emailInputField.text = "";
        passwordInputField.text = "";

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
        loginButton.onClick.RemoveListener(LoginWithEmailPassword);
    }
}