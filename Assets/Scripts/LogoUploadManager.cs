using AppFirebase;
using UnityEngine;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using UnityEngine.UI;
using System;
using System.Collections;
using UnityEngine.Networking;
using SFB;
using System.Collections.Generic;

// Script that allows businesses to upload, remove, display their logo
public class LogoUploadManager : MonoBehaviour
{
    public Button logoUploadButton;
    private AppAuth auth;
    private AppFirestore db;
    private AppStorage storage;
    private AppUser currentUser;
    private string userBoothId;
    private string downloadUrl;
    public StandSelectionManager standSelectionManager;

    // Previously this setup ran twice - once here in Awake() (unconditionally, with no null
    // check before calling RetrieveUserBoothId(), which would throw a NullReferenceException
    // via currentUser.UserId if no one was logged in yet) and then again, identically, in
    // Start() (which did have the null check, but by then Awake() could already have
    // crashed, and either way it redundantly re-ran the same Firestore query a second time
    // on every scene load). Consolidated into Awake() alone with the null check in the right
    // place.
    void Awake()
    {
        auth = AppAuth.DefaultInstance;
        db = AppFirestore.DefaultInstance;
        storage = AppStorage.DefaultInstance;
        currentUser = auth.CurrentUser;

        logoUploadButton.onClick.AddListener(() => StartCoroutine(GetFilePathFromUser()));

        if (currentUser == null)
        {
            Debug.LogError("No user is logged in.");
            return;
        }
        RetrieveUserBoothId();
    }

    // Using SFB extension to upload logo from device
    System.Collections.IEnumerator GetFilePathFromUser()
    {
        // Standalone File Browser
        var extensions = new[] {
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
        };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select Logo", "", extensions, false);
        if (paths.Length > 0)
        {
            string filePath = paths[0];
            if (!string.IsNullOrEmpty(filePath))
            {
                UploadLogo(filePath);
            }
        }
        yield return null;
    }

    // 
    void RetrieveUserBoothId()
    {
        db.Collection("booths").WhereEqualTo("UserId", currentUser.UserId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error getting booths: " + task.Exception);
                return;
            }

            var documents = task.Result.Documents;
            if (documents.Count() > 0)
            {
                var boothDocument = documents.First();
                userBoothId = boothDocument.Id;
                Debug.Log("Booth assignment found: " + userBoothId);
            }
            else
            {
                Debug.LogWarning("No booth assigned to the current user.");
            }
        });
    }

    // Method that uploads the logo to firebase storage under business UID
    // and extracts download url
    void UploadLogo(string filePath)
    {
        Debug.Log($"Attempting to upload logo from path: {filePath}");
        RetrieveUserBoothId();

        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found at path: {filePath}");
            return;
        }

        byte[] fileContents = File.ReadAllBytes(filePath);
        if (fileContents == null || fileContents.Length == 0)
        {
            Debug.LogError($"Failed to read file contents or file is empty at path: {filePath}");
            return;
        }

        string fileName = Path.GetFileName(filePath);
        Debug.Log($"Filename extracted: {fileName}");

        try
        {
            var storageRef = AppStorage.DefaultInstance.GetReference($"user_logos/{currentUser.UserId}/{fileName}");
            Debug.Log($"Storage reference created: {storageRef.Path}");

            storageRef.PutBytesAsync(fileContents).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Error uploading file: " + task.Exception);
                    return;
                }

                Debug.Log("Upload successful, attempting to get download URL.");
                storageRef.GetDownloadUrlAsync().ContinueWithOnMainThread(downloadTask =>
                {
                    if (downloadTask.IsFaulted || downloadTask.IsCanceled)
                    {
                        Debug.LogError("Error getting download URL: " + downloadTask.Exception);
                        return;
                    }

                    downloadUrl = downloadTask.Result.ToString();
                    Debug.Log($"File available at: {downloadUrl}");

                    SaveLogoUrlToFirestore(downloadUrl);
                });
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while attempting to upload logo: {ex.Message}");
        }
    }

    // Button that manages the removal of the logo from the user database
    public void RemoveLogo()
    {
        AppUser currentUser = auth.CurrentUser;
        if (currentUser == null)
        {
            Debug.LogError("No user is logged in. Cannot remove logo.");
            return;
        }

        AppDocumentReference userDocRef = db.Collection("bus_users").Document(currentUser.UserId);

        var updates = new Dictionary<string, object>
        {
            { "logo", AppFieldValue.Delete }
        };

        userDocRef.UpdateAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Error removing logo: " + task.Exception);
                return;
            }
            Debug.Log("Logo successfully removed.");

            standSelectionManager.LoadBusinessLogo();
        });
    }

    // Saving the download url from storage to each business user document for
    // application to their booth/dashboard
    void SaveLogoUrlToFirestore(string logoUrl)
    {
        var busUserRef = db.Collection("bus_users").Document(currentUser.UserId);
        busUserRef.UpdateAsync("logo", logoUrl).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error updating Firestore with logo URL: " + task.Exception);
                return;
            }
            if (standSelectionManager != null)
            {
                standSelectionManager.LoadBusinessLogo();
            }

            Debug.Log("Firestore updated with logo URL for business user: " + currentUser.UserId);
        });
    }

    // Method that allows application of logo using booth tag, firebase business id
    // to the mesh render of Stand_X_Logo_X where X is the booth number
    void ApplyLogoToBoothMaterial(string logoUrl)
    {
        GameObject boothObject = GameObject.FindWithTag(userBoothId);

        if (boothObject == null)
        {
            Debug.LogError($"Booth GameObject with tag '{userBoothId}' not found in the scene.");
            return;
        }
        string boothNumber = userBoothId.Split('_')[1];
        string logoTag = $"{userBoothId}_Logo_{boothNumber}";

        GameObject logoObject = GameObject.FindWithTag(logoTag);

        if (logoObject == null)
        {
            Debug.LogError($"Booth logo GameObject with tag '{logoTag}' not found.");
            return;
        }

        MeshRenderer logoRenderer = logoObject.GetComponent<MeshRenderer>();
        if (logoRenderer == null)
        {
            Debug.LogError($"MeshRenderer component not found on the logo GameObject with tag '{logoTag}'.");
            return;
        }

        StartCoroutine(DownloadAndApplyTexture(logoUrl, logoRenderer));
    }

    // Using the download url to fetch and apply the texture logo
    System.Collections.IEnumerator DownloadAndApplyTexture(string textureUrl, MeshRenderer renderer)
    {
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(textureUrl))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error downloading texture: " + uwr.error);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);

                Material[] materials = renderer.materials;
                materials[0].mainTexture = texture;
                renderer.materials = materials;
            }
        }
    }
}