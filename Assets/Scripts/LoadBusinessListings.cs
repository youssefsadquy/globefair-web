using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

// Method to fetch, load and display listing for the current business user
public class LoadBusinessListings : MonoBehaviour
{
    public GameObject listingPrefab;
    public Transform contentPanel;
    private AppFirestore db;
    private string businessUid;
    public Text feedbackText;

    void Start()
    {
        db = AppFirestore.DefaultInstance;
        if (AppAuth.DefaultInstance.CurrentUser != null)
        {
            businessUid = AppAuth.DefaultInstance.CurrentUser.UserId;
            LoadListings();
        }
        else
        {
            Debug.LogError("No authenticated user found.");
        }
    }

    // Refreshing everytime the canvas is activated for user change
    private void OnEnable()
    {
        AppAuth auth = AppAuth.DefaultInstance;
        if (auth.CurrentUser != null)
        {
            businessUid = auth.CurrentUser.UserId;
            LoadListings();
        }
        else
        {
            Debug.LogError("No authenticated user found.");
        }
    }

    // Fetching all the listing where the business uid is equal to the current user
    public void LoadListings()
    {
        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }

        db.Collection("businessListings").WhereEqualTo("businessUid", businessUid)
            .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("Error getting listings: " + task.Exception);
                    return;
                }

                AppQuerySnapshot snapshot = task.Result;
                foreach (AppDocumentSnapshot documentSnapshot in snapshot.Documents)
                {
                    GameObject listingInstance = Instantiate(listingPrefab, contentPanel);
                    PopulateListing(listingInstance, documentSnapshot);
                }
            });
    }

    // Applying the fetched listing to each prefab
    private void PopulateListing(GameObject listingInstance, AppDocumentSnapshot documentSnapshot)
    {
        ListingPrefabScript listingScript = listingInstance.GetComponent<ListingPrefabScript>();

        if (documentSnapshot.Exists)
        {
            Dictionary<string, object> listingData = documentSnapshot.ToDictionary();
            string documentId = documentSnapshot.Id;
            listingScript.Setup(listingData, documentId);
        }
        Canvas.ForceUpdateCanvases();
    }

    // Method that allows feedback showing and hiding
    public void ShowFeedback(string message, bool isSuccess)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isSuccess ? Color.green : Color.red;
            StartCoroutine(ClearFeedbackAfterDelay(5));
        }
    }

    private IEnumerator ClearFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackText != null)
        {
            feedbackText.text = "";
        }
    }
}