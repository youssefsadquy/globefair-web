using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System;

// Script attached to listing prefab to populate using listing id and business id
public class ListingPrefabScript : MonoBehaviour
{
    public Text jobTitleText;
    public Text jobLocationText;
    public Text salaryText;
    public Text creationDateText;
    private LoadBusinessListings loadBusinessListingsScript;
    private ListingEditForm listingEditFormScript;

    public string documentId;

    private void Start()
    {
        loadBusinessListingsScript = FindObjectOfType<LoadBusinessListings>();
    }

    // Using the business id fetches all the owned listings and populates listing
    // scrollview with the prefabs
    public void Setup(Dictionary<string, object> data, string docId)
    {
        Debug.Log("Setting up listing with data: " + data.ToString());
        documentId = docId;

        if (data.TryGetValue("jobTitle", out object jobTitle))
        {
            Debug.Log("Job Title: " + (jobTitle as string));
            jobTitleText.text = jobTitle as string;
        }

        if (data.TryGetValue("creationDate", out object creationDate))
        {
            if (creationDate is AppTimestamp timestamp)
            {
                DateTime dateTime = timestamp.UtcDateTime;
                string dateString = dateTime.ToString("dd-MM-yyyy HH:mm:ss");
                Debug.Log("Creation Date: " + dateString);
                creationDateText.text = dateString;
            }
        }

        if (data.TryGetValue("location", out object jobLocation))
        {
            Debug.Log("Location: " + (jobLocation as string));
            jobLocationText.text = jobLocation as string;
        }

        if (data.TryGetValue("salary", out object salary))
        {
            Debug.Log("Salary: " + (salary as string));
            salaryText.text = salary as string;
        }
    }

    // Button on prefab to delete listing from firebase
    public void DeleteListing()
    {
        if (!string.IsNullOrEmpty(documentId))
        {
            AppFirestore db = AppFirestore.DefaultInstance;
            db.Collection("businessListings").Document(documentId).DeleteAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("Error deleting listing: " + task.Exception);
                    loadBusinessListingsScript?.ShowFeedback("Failed to delete listing.", false);
                }
                else
                {
                    Debug.Log("Listing deleted successfully.");
                    loadBusinessListingsScript?.ShowFeedback("Listing deleted successfully.", true);
                    Destroy(gameObject);
                }
            });
        }
        else
        {
            Debug.LogError("Document ID is null or empty.");
            loadBusinessListingsScript?.ShowFeedback("Cannot delete: No listing selected.", false);
        }
    }

    // Edit button attached to prefab that passes the listingid to editor canvas
    // script to handle editing/saving
    public void OpenEditForm()
    {
        AppFirestore db = AppFirestore.DefaultInstance;
        AppDocumentReference docRef = db.Collection("businessListings").Document(documentId);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists)
            {
                Debug.LogError("Error fetching listing: " + task.Exception);
                loadBusinessListingsScript?.ShowFeedback("Failed to fetch listing for editing.", false);
            }
            else
            {
                AppDocumentSnapshot snapshot = task.Result;
                Dictionary<string, object> data = snapshot.ToDictionary();
                ListingEditForm.Instance.OpenEditForm(data, documentId);
            }
        });
    }
}