using System.Collections.Generic;
using AppFirebase;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;

// Script that allows users to fetch all of their applications using prefabs
public class UserApplicationsManager : MonoBehaviour
{
    [SerializeField] private GameObject applicationPrefab;
    [SerializeField] private Transform applicationsContainer;
    [SerializeField] private GameObject noApplicationsPrefab;

    private AppFirestore db;
    private AppAuth auth;

    void Start()
    {
        db = AppFirestore.DefaultInstance;
        auth = AppAuth.DefaultInstance;

        LoadUserApplications();
    }

    // Method to fetch all the listings applied for the user
    private void LoadUserApplications()
    {
        // No null check here previously - if CurrentUser is null (e.g. this runs before
        // Firebase Auth has resolved) this threw a NullReferenceException instead of
        // failing gracefully like every sibling manager in this codebase.
        if (auth.CurrentUser == null)
        {
            Debug.LogError("No authenticated user found.");
            return;
        }

        string userId = auth.CurrentUser.UserId;

        // Fetch applications
        db.Collection("candidates").WhereEqualTo("userId", userId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error fetching user applications: " + task.Exception);
                return;
            }

            // No applications found
            var documents = task.Result.Documents;
            if (!documents.Any())
            {
                Instantiate(noApplicationsPrefab, applicationsContainer);
                return;
            }

            foreach (AppDocumentSnapshot applicationSnapshot in task.Result.Documents)
            {
                string listingId = applicationSnapshot.GetValue<string>("listingId");
                AppTimestamp applicationDate = applicationSnapshot.GetValue<AppTimestamp>("applicationDate");

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
                    string businessUid = listingSnapshot.GetValue<string>("businessUid");

                    // Fetch the business details name, email
                    db.Collection("bus_users").Document(businessUid).GetSnapshotAsync().ContinueWithOnMainThread(businessUserTask =>
                    {
                        if (businessUserTask.IsFaulted || businessUserTask.IsCanceled)
                        {
                            Debug.LogError("Error fetching business user details: " + businessUserTask.Exception);
                            return;
                        }

                        AppDocumentSnapshot businessUserSnapshot = businessUserTask.Result;
                        string businessName = businessUserSnapshot.GetValue<string>("businessName");
                        string contactEmail = businessUserSnapshot.GetValue<string>("contactEmail");

                        // Create the prefab with all the components
                        GameObject applicationGO = Instantiate(applicationPrefab, applicationsContainer);
                        applicationGO.GetComponent<ApplicationItem>().SetApplicationInfo(
                            this,
                            applicationSnapshot.Id,
                            listingId,
                            applicationDate.UtcDateTime.ToString("dd MMM yyyy HH:mm"),
                            jobTitle,
                            businessName,
                            contactEmail
                        );
                    });
                });
            }
        });
    }

    // On enter populating with applications
    public void ViewApplications()
    {
        ClearApplicationsContainer();
        LoadUserApplications();
    }
    // On exit clearing the container
    public void ClearApplicationsContainer()
    {
        foreach (Transform child in applicationsContainer)
        {
            Destroy(child.gameObject);
        }
    }
}