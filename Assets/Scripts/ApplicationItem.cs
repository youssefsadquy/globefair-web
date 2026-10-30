using UnityEngine;
using UnityEngine.UI;
using AppFirebase;

// Prefab script for each user application to populate
public class ApplicationItem : MonoBehaviour
{
    [SerializeField] private Text businessNameText;
    [SerializeField] private Text contactEmailText;
    [SerializeField] private Text jobTitleText;
    [SerializeField] private Text applicationDateText;
    [SerializeField] private Button discardButton;

    private AppFirestore db;
    private string applicationId;
    private bool confirmDiscard = false;

    private UserApplicationsManager userApplicationsManager;

    void Start()
    {
        db = AppFirestore.DefaultInstance;
        discardButton.onClick.AddListener(OnDiscardClicked);
    }

    // Method to assign fields on prefab
    public void SetApplicationInfo(UserApplicationsManager manager, string applicationId, string listingId, string applicationDate, string jobTitle, string businessName, string contactEmail)
    {
        this.userApplicationsManager = manager;
        this.applicationId = applicationId;
        this.jobTitleText.text = jobTitle;
        this.applicationDateText.text = applicationDate;
        this.businessNameText.text = businessName;
        this.contactEmailText.text = contactEmail;
    }

    // Method to discard own applications
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
            db.Collection("candidates").Document(applicationId).DeleteAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log("Application discarded.");
                    Destroy(this.gameObject);
                    userApplicationsManager.ViewApplications();
                }
                else
                {
                    Debug.LogError("Error discarding application: " + task.Exception);
                    discardButton.GetComponentInChildren<Text>().text = "DISCARD";
                }
            });
            confirmDiscard = false;
        }
    }
}