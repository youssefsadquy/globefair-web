using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System;

// Method that allows business to create, update business information
public class BusinessBioManager : MonoBehaviour
{
    public InputField businessNameInputField;
    public InputField descriptionInputField;
    public InputField missionInputField;
    public InputField industryInputField;
    public InputField foundedInputField;
    public InputField employeesInputField;
    public InputField websiteInputField;
    public InputField contactEmailInputField;
    public InputField headquartersInputField;
    public Text feedbackText;
    public Scrollbar verticalScrollbar;

    public Button saveButton;

    private AppFirestore db;
    private AppAuth auth;
    private string businessUid;

    // Setting fields to detect business user and load/save
    // their info on firebase
    void Start()
    {
        db = AppFirestore.DefaultInstance;
        auth = AppAuth.DefaultInstance;
        saveButton.onClick.AddListener(SaveBusinessBio);
        auth.StateChanged += OnAuthStateChanged;

        if (auth.CurrentUser != null)
        {
            businessUid = auth.CurrentUser.UserId;
            LoadBusinessBio();
        }
        else
        {
            // On WebGL, Firebase Auth's session restore is asynchronous, so CurrentUser can
            // still read null here even for a genuinely signed-in user if Start() runs before
            // that restore finishes. Rather than getting stuck on this message forever, wait
            // for StateChanged (it fires once for the initial restore too) and retry then.
            ShowFeedback("No authenticated user found.", false);
        }
    }

    // Fetching everytime the canvas is activated in case of user change
    void OnEnable()
    {
        auth = AppAuth.DefaultInstance;
        if (auth.CurrentUser != null)
        {
            businessUid = auth.CurrentUser.UserId;
            LoadBusinessBio();
        }
        else
        {
            ShowFeedback("No authenticated user found.", false);
        }
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        if (auth.CurrentUser != null && auth.CurrentUser.UserId != businessUid)
        {
            businessUid = auth.CurrentUser.UserId;
            LoadBusinessBio();
        }
    }

    void OnDestroy()
    {
        if (auth != null) auth.StateChanged -= OnAuthStateChanged;
    }

    void OnDisable()
    {
        ClearInputFields();
    }

    // Called after leaving the canvas to clear fields
    void ClearInputFields()
    {
        businessNameInputField.text = "";
        descriptionInputField.text = "";
        missionInputField.text = "";
        industryInputField.text = "";
        foundedInputField.text = "";
        employeesInputField.text = "";
        websiteInputField.text = "";
        contactEmailInputField.text = "";
        headquartersInputField.text = "";
    }

    // Fetching business info based on business userUID
    public void LoadBusinessBio()
    {
        AppDocumentReference businessDocRef = db.Collection("bus_users").Document(businessUid);

        businessDocRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists)
            {
                ShowFeedback("Error loading business bio: " + task.Exception, false);
                return;
            }
            AppDocumentSnapshot snapshot = task.Result;
            businessNameInputField.text = snapshot.GetValue<string>("businessName");
            descriptionInputField.text = snapshot.GetValue<string>("description");
            missionInputField.text = snapshot.GetValue<string>("mission");
            industryInputField.text = snapshot.GetValue<string>("industry");
            foundedInputField.text = snapshot.GetValue<string>("founded");
            employeesInputField.text = snapshot.GetValue<string>("employees");
            websiteInputField.text = snapshot.GetValue<string>("website");
            contactEmailInputField.text = snapshot.GetValue<string>("contactEmail");
            headquartersInputField.text = snapshot.GetValue<string>("headquarters");
        });
    }

    // Method that allows editing of the fields on firebase and saving
    void SaveBusinessBio()
    {
        AppDocumentReference businessDocRef = db.Collection("bus_users").Document(businessUid);

        var updatedBio = new Dictionary<string, object>
        {
            { "businessName", businessNameInputField.text },
            { "description", descriptionInputField.text },
            { "mission", missionInputField.text },
            { "industry", industryInputField.text },
            { "founded", foundedInputField.text },
            { "employees", employeesInputField.text },
            { "website", websiteInputField.text },
            { "contactEmail", contactEmailInputField.text },
            { "headquarters", headquartersInputField.text }
        };

        businessDocRef.UpdateAsync(updatedBio).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                ShowFeedback("Error saving business bio: " + task.Exception, false);
            }
            else
            {
                ShowFeedback("Business bio updated successfully.", true);
                LoadBusinessBio();
            }
        });
    }

    // Feedback display conditional
    public void ShowFeedback(string message, bool isSuccess)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isSuccess ? Color.green : Color.red;
            StartCoroutine(ClearFeedbackAfterDelay(3));
        }
    }

    private IEnumerator ClearFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackText != null)
        {
            feedbackText.text = "";
        }
        ScrollToTop();
    }

    private void ScrollToTop()
    {
        if (verticalScrollbar)
        {
            verticalScrollbar.value = 1;
        }
    }
}