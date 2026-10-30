using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Threading.Tasks;
using SFB;
using System.Collections;
using System;

// Script that manages the fetching of the user dashboard with all the functionalities
// like cv uploading/removal, personal info displaying, starting fair, logout
public class UserDashboardManager : MonoBehaviour
{
    public Text nameText;
    public Text emailText;
    public Button uploadCVButton;
    public Button startGameButton;
    public Button logoutButton;
    public GameObject landingCanvas;
    public GameObject userDashboardCanvas;
    public Button customizeAvatarButton;

    private AppAuth auth;
    private AppFirestore db;
    private AppStorage storage;
    private AppUser currentUser;

    public Image cvStatusImage;
    public Sprite greenTickSprite;
    public Sprite redCrossSprite;
    public Button removeCVButton;
    private IDisposable firestoreListener;
    public Text feedbackText;

    void Awake()
    {
        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
        storage = AppStorage.DefaultInstance;
        currentUser = auth.CurrentUser;
    }

    void Start()
    {
        if (currentUser != null)
        {
            FetchUserDetails();
            uploadCVButton.onClick.AddListener(UploadCV);
            startGameButton.onClick.AddListener(StartFair);
            logoutButton.onClick.AddListener(LogoutUser);
            customizeAvatarButton.onClick.AddListener(CustomizeAvatar);
        }
        else
        {
            Debug.LogError("No user is logged in.");
        }

        SetupFirestoreListener();
        removeCVButton.onClick.AddListener(RemoveCV);
    }

    // Method to display user info on dashboard
    private void FetchUserDetails()
    {
        if (currentUser == null) return;

        AppDocumentReference userDocRef = db.Collection("users").Document(currentUser.UserId);
        userDocRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error fetching user details: " + task.Exception);
                return;
            }

            AppDocumentSnapshot snapshot = task.Result;
            if (snapshot.Exists)
            {
                nameText.text = snapshot.GetValue<string>("FirstName") ?? "Name";
                emailText.text = snapshot.GetValue<string>("Email") ?? "Email";
            }
            else
            {
                Debug.LogError("Document does not exist!");
                nameText.text = "Name";
                emailText.text = "Email";
            }
        });
    }

    void OnEnable()
    {
        currentUser = auth.CurrentUser;
        if (currentUser != null)
        {
            FetchUserDetails();
        }
    }

    // Onclick for upload CV button
    private void UploadCV()
    {
        var extensions = new[] { new ExtensionFilter("PDF Files", "pdf") };

#if UNITY_WEBGL
        // WebGL has no filesystem and browsers cannot show a synchronous/blocking file dialog -
        // see StandaloneFileBrowserWebGL.cs. This used to throw a NullReferenceException the
        // instant the button was clicked, because StandaloneFileBrowser.cs had no WebGL branch
        // at all (fixed there) and this call was still the old synchronous OpenFilePanel (fixed
        // here). The async callback delivers a blob: URL rather than a path, so the bytes are
        // fetched over UnityWebRequest inside UploadPDF below instead of File.ReadAllBytes.
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", extensions, false, paths =>
        {
            if (paths != null && paths.Length > 0)
            {
                StartCoroutine(UploadPDF(paths[0]));
            }
        });
#else
        var paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, false);

        if (paths.Length > 0)
        {
            StartCoroutine(UploadPDF(paths[0]));
        }
#endif
    }

    // Method to upload button on storage and extract URL
    // On WebGL "path" is actually a blob: URL handed back by StandaloneFileBrowserWebGL, not a
    // filesystem path - there is no filesystem on WebGL, so the bytes are fetched over
    // UnityWebRequest instead of File.ReadAllBytes (which would throw immediately on a blob: URL).
    private IEnumerator UploadPDF(string path)
    {
        var storageRef = storage.GetReferenceFromUrl("gs://globefair-46eb2.appspot.com");
        var pdfRef = storageRef.Child("cvs/" + currentUser.UserId + ".pdf");

        byte[] fileBytes = null;
#if UNITY_WEBGL
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(path))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error reading selected file: " + request.error);
                ShowFeedback("Could not read the selected file", false);
                yield break;
            }
            fileBytes = request.downloadHandler.data;
        }
#else
        try
        {
            fileBytes = File.ReadAllBytes(path);
        }
        catch (IOException e)
        {
            Debug.LogError("Error reading file: " + e.Message);
            yield break;
        }
#endif

        var uploadTask = pdfRef.PutBytesAsync(fileBytes);
        yield return new WaitUntil(() => uploadTask.IsCompleted);

        if (uploadTask.IsFaulted)
        {
            foreach (var error in uploadTask.Exception.Flatten().InnerExceptions)
            {
                Debug.LogError(error.Message);
            }
        }
        else if (uploadTask.IsCanceled)
        {
            Debug.LogError("Upload was canceled.");
        }
        else
        {
            Debug.Log("File uploaded successfully.");

            var getDownloadUrlTask = pdfRef.GetDownloadUrlAsync();
            yield return new WaitUntil(() => getDownloadUrlTask.IsCompleted);

            if (getDownloadUrlTask.IsFaulted || getDownloadUrlTask.IsCanceled)
            {
                Debug.LogError("Error getting download URL: " + getDownloadUrlTask.Exception);
            }
            else
            {
                string pdfUrl = getDownloadUrlTask.Result.ToString();
                SavePdfUrlToFirestore(pdfUrl);
            }
        }
    }

    // Method to set the CV url under the current user document
    private void SavePdfUrlToFirestore(string pdfUrl)
    {
        AppDocumentReference userDocRef = db.Collection("users").Document(currentUser.UserId);
        userDocRef.UpdateAsync("cv", pdfUrl).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error updating Firestore with PDF URL: " + task.Exception);
            }
            else
            {
                Debug.Log("Firestore updated with PDF URL for user: " + currentUser.UserId);
            }
        });
    }

    // Live UI update for CV uploading status
    private void SetupFirestoreListener()
    {
        AppDocumentReference userDocRef = db.Collection("users").Document(currentUser.UserId);
        firestoreListener = userDocRef.Listen(snapshot =>
        {
            if (snapshot.Exists && snapshot.TryGetValue("cv", out string cvLink))
            {
                cvStatusImage.sprite = greenTickSprite;
                ShowFeedback("CV is uploaded.", true);
            }
            else
            {
                cvStatusImage.sprite = redCrossSprite;
            }
        });
    }

    // Method to delete uploaded CV
    private void RemoveCV()
    {
        var storageRef = storage.GetReferenceFromUrl("gs://globefair-46eb2.appspot.com/cvs/" + currentUser.UserId + ".pdf");
        storageRef.DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error deleting CV from storage: " + task.Exception);
                ShowFeedback("CV not found", false);
            }
            else
            {
                Debug.Log("CV deleted from storage successfully.");
                ShowFeedback("CV deleted from storage successfully", true);

                AppDocumentReference userDocRef = db.Collection("users").Document(currentUser.UserId);
                userDocRef.UpdateAsync("cv", AppFieldValue.Delete).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError("Error removing CV from Firestore: " + task.Exception);
                        ShowFeedback("CV not found", false);
                    }
                    else
                    {
                        Debug.Log("CV removed from Firestore successfully.");
                        ShowFeedback("CV removed successfully", true);
                    }
                });
            }
        });
    }

    // Onclick for start fair button
    public void StartFair()
    {
        LoadingManager.Instance.EnableLoading("Loading the fair...");

        StartCoroutine(LoadGameScene("GlobeFair"));

        if (userDashboardCanvas != null)
        {
            userDashboardCanvas.SetActive(false);
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

    // Changes scene to teh avatar customiser
    public void CustomizeAvatar()
    {
        LoadingManager.Instance.EnableLoading("Loading customisation...");

        StartCoroutine(LoadGameScene("PlayerCustomiser"));

        if (userDashboardCanvas != null)
        {
            userDashboardCanvas.SetActive(false);
        }
    }

    // Onclick for logout button
    private void LogoutUser()
    {
        auth.SignOut();
        ResetDashboard();
        landingCanvas.SetActive(true);
        gameObject.SetActive(false);
    }

    private void ResetDashboard()
    {
        nameText.text = "Name";
        emailText.text = "Email";
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
        // Previously these passed a freshly-created "() => X()" lambda to RemoveListener,
        // which is a different delegate instance from the one AddListener was given in
        // Start() - UnityEvent.RemoveListener compares delegate identity, so those calls
        // silently removed nothing. Passing the method group directly makes add/remove
        // refer to the exact same delegate.
        uploadCVButton.onClick.RemoveListener(UploadCV);
        startGameButton.onClick.RemoveListener(StartFair);
        logoutButton.onClick.RemoveListener(LogoutUser);
        customizeAvatarButton.onClick.RemoveListener(CustomizeAvatar);
        removeCVButton.onClick.RemoveListener(RemoveCV);
        firestoreListener?.Dispose();
    }
}
