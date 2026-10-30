using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;


public class BusinessDashboardManager : MonoBehaviour
{
    public Text businessNameText;
    public Text emailText;
    public Button startGameButton;
    public Button logoutButton;
    public GameObject landingCanvas;
    public GameObject businessDashboardCanvas;
    public StandSelectionManager standSelectionManager;

    private AppAuth auth;
    private AppFirestore db;
    private AppUser currentUser;

    void Awake()
    {
        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
        currentUser = auth.CurrentUser;

        auth.StateChanged += AuthStateChanged;
    }

    void Start()
    {
        if (currentUser != null)
        {
            FetchBusinessDetails();
            startGameButton.onClick.AddListener(StartFair);
            logoutButton.onClick.AddListener(LogoutUser);
        }
        else
        {
            Debug.LogError("No business user is logged in.");
        }
    }

    private void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (auth.CurrentUser != currentUser)
        {
            Debug.Log("Auth state changed, user has either signed in or out");
            currentUser = auth.CurrentUser;

            if (currentUser != null)
            {
                FetchBusinessDetails();
                RefreshUI();
            }
            else
            {
                ResetDashboard();
                standSelectionManager.gameObject.SetActive(false);
            }
        }
    }

    private void FetchBusinessDetails()
    {
        if (currentUser == null) return;

        AppDocumentReference businessDocRef = db.Collection("bus_users").Document(currentUser.UserId);
        businessDocRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error fetching business details: " + task.Exception);
                return;
            }

            AppDocumentSnapshot snapshot = task.Result;
            if (snapshot.Exists)
            {
                businessNameText.text = snapshot.GetValue<string>("BusinessName") ?? "Business Name";
                emailText.text = snapshot.GetValue<string>("Email") ?? "Email";
            }
            else
            {
                Debug.LogError("Business does not exist!");
                businessNameText.text = "Business Name";
                emailText.text = "Email";
            }
        });
    }

    void OnEnable()
    {
        currentUser = auth.CurrentUser;
        if (currentUser != null)
        {
            FetchBusinessDetails();
            RefreshUI();
        }
        else
        {
            Debug.LogError("No user is logged in.");
        }
    }

    public void StartFair()
    {
        LoadingManager.Instance.EnableLoading("Loading the fair...");

        StartCoroutine(LoadGameScene("GlobeFair"));

        if (businessDashboardCanvas != null)
        {
            businessDashboardCanvas.SetActive(false);
        }
    }

    private IEnumerator LoadGameScene(string sceneName)
    {
        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        LoadingManager.Instance.DisableLoading();
    }

    private void LogoutUser()
    {
        auth.SignOut();
        ResetDashboard();

        if (businessDashboardCanvas != null)
        {
            businessDashboardCanvas.SetActive(false);
        }

        landingCanvas.SetActive(true);
    }

    private void RefreshUI()
    {
        if (auth.CurrentUser == null)
        {
            Debug.LogError("No user is logged in.");
            return;
        }

        FetchBusinessDetails();

        if (businessDashboardCanvas != null)
        {
            businessDashboardCanvas.SetActive(true);
        }


    }

    private void ResetDashboard()
    {
        businessNameText.text = "Business Name";
        emailText.text = "Email";
    }

    void OnDestroy()
    {
        auth.StateChanged -= AuthStateChanged;
        // Previously these passed a freshly-created "() => X()" lambda to RemoveListener,
        // a different delegate instance from the one AddListener was given in Start() -
        // UnityEvent.RemoveListener compares delegate identity, so this silently removed
        // nothing. The method group refers to the exact same delegate that was added.
        startGameButton.onClick.RemoveListener(StartFair);
        logoutButton.onClick.RemoveListener(LogoutUser);
    }
}
