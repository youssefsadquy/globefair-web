using UnityEngine;
using AppFirebase;

// Navigation system for the landing scene that allows redirection from Fair scene
public class NavigationManager : MonoBehaviour
{
    public GameObject canvasLanding;
    public GameObject canvasLogin;
    public GameObject canvasSignup;
    public GameObject canvasLoginBusiness;
    public GameObject canvasSignupBusiness;
    public GameObject userDashboardCanvas;
    public GameObject businessDashboardCanvas;
    public GameObject canvasCustomization;
    public GameObject BusinessListings;
    public GameObject EditListings;
    public GameObject businessBioCanvas;
    public GameObject businessQACanvas;
    public GameObject businessCandidates;
    public GameObject userApplication;
    public GameObject passwordResetCanvas;
    public GameObject analyticsDashboard;
    public GameObject settingsCanvas;
    public GameObject quitCanvas;
    public GameObject chatUsers;
    public GameObject chatBusinesses;

    private AppAuth auth;
    private AppFirestore db;

    private Animator animator;

    void Awake()
    {
        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (PlayerPrefs.GetInt("LoadDashboard", 0) == 1)
        {
            PlayerPrefs.SetInt("LoadDashboard", 0);
            LoadAppropriateDashboard();
        }
        else
        {
            GoToCanvasLanding();
        }
    }

    // Method that detects the user on Landing for quick signin (only from other scenes)
    public void LoadAppropriateDashboard()
    {
        AppUser currentUser = auth.CurrentUser;
        if (currentUser != null)
        {
            db.Collection("users").Document(currentUser.UserId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Error fetching user document: " + task.Exception);
                    return;
                }

                AppDocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    // Show the user dashboard
                    SetActiveCanvas(userDashboardCanvas);
                }
                else
                {
                    db.Collection("bus_users").Document(currentUser.UserId).GetSnapshotAsync().ContinueWithOnMainThread(busTask =>
                    {
                        if (busTask.IsFaulted || busTask.IsCanceled)
                        {
                            Debug.LogError("Error fetching business user document: " + busTask.Exception);
                            return;
                        }

                        AppDocumentSnapshot busSnapshot = busTask.Result;
                        if (busSnapshot.Exists)
                        {
                            // Show the business dashboard
                            SetActiveCanvas(businessDashboardCanvas);
                        }
                        else
                        {
                            Debug.LogError("No dashboard found for user!");
                            // Show the landing canvas if no dashboard is found
                            SetActiveCanvas(canvasLanding);
                        }
                    });
                }
            });
        }
        else
        {
            Debug.LogError("No user is logged in.");
            // Show the landing canvas if no user is logged in
            SetActiveCanvas(canvasLanding);
        }
    }

    private void SetActiveCanvas(GameObject canvas)
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        userDashboardCanvas.SetActive(false);
        businessDashboardCanvas.SetActive(false);
        settingsCanvas.SetActive(false);
        quitCanvas.SetActive(false);

        canvas.SetActive(true);
    }


    // Method when the user clicks the user login button
    public void GoToCanvasLogin()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(true);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        passwordResetCanvas.SetActive(false);
    }

    // Method to open settings
    public void GoTosettings()
    {
        settingsCanvas.SetActive(true);
        animator.Play("SlideUp");
    }

    // Method to close settings
    public void CloseSettings()
    {
        settingsCanvas.SetActive(false);
    }

    // Method to quit application
    public void GoToquit()
    {
        quitCanvas.SetActive(true);
        animator.Play("exitAnimatorLanding");
    }

    // Method to quit application
    public void CloseQuit()
    {
        quitCanvas.SetActive(false);
    }

    // Method when the user clicks the password reset button
    public void GoToCanvasPasswordReset()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        passwordResetCanvas.SetActive(true);
    }

    // Method when the user clicks the user signup button
    public void GoToCanvasSignup()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(true);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        passwordResetCanvas.SetActive(false);
    }

    // Method when the user clicks the business login button
    public void GoToCanvasLoginBusiness()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(true);
        canvasSignupBusiness.SetActive(false);
        passwordResetCanvas.SetActive(false);
    }

    // Method when the user clicks the business signup button
    public void GoToCanvasSignupBusiness()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(true);
        passwordResetCanvas.SetActive(false);
    }

    // Method to return to the landing canvas from any other canvas
    public void GoToCanvasLanding()
    {
        canvasLanding.SetActive(true);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        passwordResetCanvas.SetActive(false);
    }

    // Method to return to the Customisation canvas from any other canvas
    public void GoToCanvasCustomisation()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(true);
        businessDashboardCanvas.SetActive(false);
        userDashboardCanvas.SetActive(false);
        businessBioCanvas.SetActive(false);
        businessQACanvas.SetActive(false);
    }

    // Method to return to the Dashboard canvas from any other canvas
    public void GoTobusinessDashboardCanvas()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(false);
        businessDashboardCanvas.SetActive(true);
        userDashboardCanvas.SetActive(false);
        BusinessListings.SetActive(false);
        businessCandidates.SetActive(false);
        analyticsDashboard.SetActive(false);
        chatBusinesses.SetActive(false);
    }

    // Method to return to the Listings canvas from any other canvas
    public void GoTobusinessListingsCanvas()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(false);
        businessDashboardCanvas.SetActive(false);
        userDashboardCanvas.SetActive(false);
        BusinessListings.SetActive(true);
        EditListings.SetActive(false);
    }

    // Method to return to the BusinessBio canvas from any other canvas
    public void GoTobusinessBioCanvas()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(false);
        businessDashboardCanvas.SetActive(false);
        userDashboardCanvas.SetActive(false);
        BusinessListings.SetActive(false);
        EditListings.SetActive(false);
        businessBioCanvas.SetActive(true);
        analyticsDashboard.SetActive(false);
    }

    // Method to return to the BusinessQA canvas from any other canvas
    public void GoTobusinessQACanvas()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(false);
        businessDashboardCanvas.SetActive(false);
        userDashboardCanvas.SetActive(false);
        BusinessListings.SetActive(false);
        EditListings.SetActive(false);
        businessBioCanvas.SetActive(false);
        businessQACanvas.SetActive(true);
    }

    // Method to return to the BusinessCandidates canvas from any other canvas
    public void GoTobusinessCandidates()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(false);
        businessDashboardCanvas.SetActive(false);
        userDashboardCanvas.SetActive(false);
        BusinessListings.SetActive(false);
        EditListings.SetActive(false);
        businessCandidates.SetActive(true);
        analyticsDashboard.SetActive(false);
    }

    // Method to return to the analyticsDashboard canvas from any other canvas
    public void GoToanalyticsDashboard()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(false);
        businessDashboardCanvas.SetActive(false);
        userDashboardCanvas.SetActive(false);
        BusinessListings.SetActive(false);
        EditListings.SetActive(false);
        businessCandidates.SetActive(false);
        analyticsDashboard.SetActive(true);
    }

    public void GoTobusinessChat()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        canvasLoginBusiness.SetActive(false);
        canvasSignupBusiness.SetActive(false);
        canvasCustomization.SetActive(false);
        businessDashboardCanvas.SetActive(false);
        userDashboardCanvas.SetActive(false);
        BusinessListings.SetActive(false);
        EditListings.SetActive(false);
        businessCandidates.SetActive(false);
        analyticsDashboard.SetActive(false);
        chatBusinesses.SetActive(true);
    }

    // Method to return to the userDashboard canvas from any other canvas
    public void GoTouserDashboard()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        userDashboardCanvas.SetActive(true);
        userApplication.SetActive(false);
        chatUsers.SetActive(false);
    }

    // Method to return to the userApplication canvas from any other canvas
    public void GoTouserApplication()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        userDashboardCanvas.SetActive(false);
        userApplication.SetActive(true);
    }

    public void GoTouserChat()
    {
        canvasLanding.SetActive(false);
        canvasLogin.SetActive(false);
        canvasSignup.SetActive(false);
        userDashboardCanvas.SetActive(false);
        userApplication.SetActive(false);
        chatUsers.SetActive(true);
    }

}
