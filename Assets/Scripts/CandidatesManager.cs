using AppFirebase;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Script that allows businesses to fetch all of their listings applications using prefabs
public class CandidatesManager : MonoBehaviour
{
    [SerializeField] private GameObject candidatePrefab;
    [SerializeField] private Transform candidatesContainer;

    private AppFirestore db;
    private AppAuth auth;

    void Start()
    {
        db = AppFirestore.DefaultInstance;
        auth = AppAuth.DefaultInstance;

        LoadCandidatesForBusiness();
    }

    // Method to fetch all candidates for the listings
    // of the current business user
    private void LoadCandidatesForBusiness()
    {
        // Unlike every sibling manager in this codebase, this read had no null check at all -
        // if CurrentUser is null (e.g. this canvas becomes active before Firebase Auth has
        // resolved), this would throw a NullReferenceException instead of failing gracefully.
        if (auth.CurrentUser == null)
        {
            Debug.LogError("No authenticated user found.");
            return;
        }

        string businessId = auth.CurrentUser.UserId;

        db.Collection("businessListings").WhereEqualTo("businessUid", businessId).GetSnapshotAsync().ContinueWithOnMainThread(listingsTask =>
        {
            if (listingsTask.IsFaulted || listingsTask.IsCanceled)
            {
                Debug.LogError("Error fetching listings: " + listingsTask.Exception);
                return;
            }

            // Find all the listing IDs of the current business
            List<string> listingIds = new List<string>();
            foreach (AppDocumentSnapshot listingSnapshot in listingsTask.Result.Documents)
            {
                listingIds.Add(listingSnapshot.Id);
            }

            // Fetch candidates that applied to listings of the current business
            db.Collection("candidates").WhereIn("listingId", listingIds).GetSnapshotAsync().ContinueWithOnMainThread(candidatesTask =>
            {
                if (candidatesTask.IsFaulted || candidatesTask.IsCanceled)
                {
                    Debug.LogError("Error fetching candidates: " + candidatesTask.Exception);
                    return;
                }

                foreach (AppDocumentSnapshot candidateSnapshot in candidatesTask.Result.Documents)
                {
                    string userId = candidateSnapshot.GetValue<string>("userId");
                    string listingId = candidateSnapshot.GetValue<string>("listingId");
                    AppTimestamp applicationDate = candidateSnapshot.GetValue<AppTimestamp>("applicationDate");
                    string cvUrl = candidateSnapshot.GetValue<string>("cvUrl");

                    // Fetch user data for full name and email
                    db.Collection("users").Document(userId).GetSnapshotAsync().ContinueWithOnMainThread(userTask =>
                    {
                        if (userTask.IsFaulted || userTask.IsCanceled)
                        {
                            Debug.LogError("Error fetching user details: " + userTask.Exception);
                            return;
                        }

                        AppDocumentSnapshot userSnapshot = userTask.Result;
                        string firstName = userSnapshot.GetValue<string>("FirstName");
                        string email = userSnapshot.GetValue<string>("Email");

                        // Fetch listing data for listing title
                        db.Collection("businessListings").Document(listingId).GetSnapshotAsync().ContinueWithOnMainThread(listingTask =>
                        {
                            if (listingTask.IsFaulted || listingTask.IsCanceled)
                            {
                                Debug.LogError("Error fetching listing details: " + listingTask.Exception);
                                return;
                            }

                            AppDocumentSnapshot listingSnapshot = listingTask.Result;
                            string jobTitle = listingSnapshot.GetValue<string>("jobTitle");

                            // Create the prefab with all the components
                            GameObject candidateGO = Instantiate(candidatePrefab, candidatesContainer);
                            candidateGO.GetComponent<CandidateItem>().SetCandidateInfo(this, userId, candidateSnapshot.Id, firstName, email, jobTitle, applicationDate.UtcDateTime.ToString("dd MMM yyyy HH:mm"), cvUrl);
                        });
                    });
                }
            });
        });
    }

    // On enter populating with candidates
    public void ViewCandidates()
    {
        ClearCandidatesContainer();
        LoadCandidatesForBusiness();
    }

    // On exit clearing the container
    public void ClearCandidatesContainer()
    {
        foreach (Transform child in candidatesContainer)
        {
            Destroy(child.gameObject);
        }
    }
}