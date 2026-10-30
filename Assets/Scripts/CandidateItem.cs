using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using UnityEngine.Networking;
using System.IO;

// Prefab script for each candidate application to populate
public class CandidateItem : MonoBehaviour
{
    [SerializeField] private Text firstNameText;
    [SerializeField] private Text emailText;
    [SerializeField] private Text jobTitleText;
    [SerializeField] private Text applicationDateText;
    [SerializeField] private Button discardButton;
    [SerializeField] private Button downloadCVButton;

    private AppFirestore db;
    private string userId;
    private string candidateId;
    private string cvUrl;
    private bool confirmDiscard = false;

    private CandidatesManager candidatesManager;

    void Start()
    {
        db = AppFirestore.DefaultInstance;
        discardButton.onClick.AddListener(OnDiscardClicked);
        downloadCVButton.onClick.AddListener(OnDownloadCVClicked);
    }

    // Method to assign fields on prefab
    public void SetCandidateInfo(CandidatesManager manager, string userId, string candidateId, string firstName, string email, string jobTitle, string applicationDate, string cvUrl)
    {
        firstNameText.text = firstName;
        emailText.text = email;
        jobTitleText.text = jobTitle;
        applicationDateText.text = applicationDate;
        this.cvUrl = cvUrl;
        this.candidatesManager = manager;
        this.candidateId = candidateId;
        // Was missing entirely: the userId field stayed null forever, so every downloaded CV
        // was saved as literally "null.pdf" on native platforms - multiple candidates' CVs
        // downloaded in the same session would all overwrite that same file.
        this.userId = userId;
    }

    // Method to discard candidates applications
    private void OnDiscardClicked()
    {
        if (!confirmDiscard)
        {
            // Confirmation step
            discardButton.GetComponentInChildren<Text>().text = "SURE?";
            confirmDiscard = true;
        }
        else
        {
            // Delete application document and refresh the list
            db.Collection("candidates").Document(candidateId).DeleteAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log("Candidate application discarded.");
                    Destroy(this.gameObject);
                    StartCoroutine(DelayedRefreshCandidates());
                }
                else
                {
                    Debug.LogError("Error discarding candidate application: " + task.Exception);
                    discardButton.GetComponentInChildren<Text>().text = "DISCARD";
                }
            });
            confirmDiscard = false;
        }
    }

    private IEnumerator DelayedRefreshCandidates()
    {
        yield return new WaitForSeconds(0.5f);
        candidatesManager.ViewCandidates();
    }

    private void OnDownloadCVClicked()
    {
        StartCoroutine(DownloadCV(cvUrl));
    }

    // Method to download and open CV to device
    private IEnumerator DownloadCV(string url)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + webRequest.error);
            }
            else
            {
                byte[] bytes = webRequest.downloadHandler.data;
                string fileName = (string.IsNullOrEmpty(userId) ? "cv" : userId) + ".pdf";

#if UNITY_WEBGL && !UNITY_EDITOR
                // File.WriteAllBytes + Process.Start (the non-WebGL branch below) don't work
                // in a WebGL build - persistentDataPath is a sandboxed virtual store the user
                // can't reach, and there's no OS process to open a viewer with from inside a
                // browser sandbox, so CV downloads were silently broken on WebGL. Trigger a
                // normal browser download instead.
                AppBrowserDownload.DownloadBytesFile(fileName, bytes, "application/pdf");
                Debug.Log("CV download triggered: " + fileName);
#else
                string filePath = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllBytes(filePath, bytes);
                Debug.Log("CV downloaded to: " + filePath);

                System.Diagnostics.Process.Start(filePath);
#endif
            }
        }
    }
}