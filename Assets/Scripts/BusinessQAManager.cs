using AppFirebase;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;

public class BusinessQAManager : MonoBehaviour
{
    // Section 1 - Creating/Editing of Q&A
    public InputField questionInputField;
    public InputField answerInputField;
    public Button submitButton;
    public Text feedbackText;
    public Scrollbar verticalScrollbar;

    // Section 2 - Displaying Q&A
    public GameObject qaPrefab;
    public Transform contentPanel;

    private AppFirestore db;
    private AppAuth auth;
    private string currentQAId;

    void Start()
    {
        db = AppFirestore.DefaultInstance;
        auth = AppAuth.DefaultInstance;

        if (auth.CurrentUser != null)
        {
            submitButton.onClick.AddListener(SubmitQA);
            LoadQAs();
        }
        else
        {
            Debug.LogError("No authenticated user found.");
        }
    }

    private void OnEnable()
    {
        AppAuth auth = AppAuth.DefaultInstance;
        if (auth.CurrentUser != null)
        {
            LoadQAs();
        }
        else
        {
            Debug.LogError("No authenticated user found.");
        }
    }

    // Method attached to Q&A creator canvas to upload Q&A to firebase
    public void SubmitQA()
    {
        var newQA = new Dictionary<string, object>
        {
            { "question", questionInputField.text },
            { "answer", answerInputField.text },
            { "businessUid", auth.CurrentUser.UserId }
        };

        Task task;
        if (string.IsNullOrEmpty(currentQAId))
        {
            task = db.Collection("businessQA").AddAsync(newQA);
        }
        else
        {
            task = db.Collection("businessQA").Document(currentQAId).SetAsync(newQA);
            currentQAId = null;
        }

        task.ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted)
            {
                Debug.LogError("Error submitting Q&A: " + t.Exception);
                ShowFeedback("Failed to submit Q&A. Please try again.", false);
            }
            else
            {
                ShowFeedback("Q&A submitted successfully.", true);
                ClearInputFields();
                LoadQAs();
            }
        });
    }

    // Fetch the current user existing Q&As
    private void LoadQAs()
    {
        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }
        db.Collection("businessQA").WhereEqualTo("businessUid", auth.CurrentUser.UserId)
            .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("Error getting Q&As: " + task.Exception);
                    return;
                }

                foreach (AppDocumentSnapshot documentSnapshot in task.Result.Documents)
                {
                    GameObject qaInstance = Instantiate(qaPrefab, contentPanel);
                    QAPrefabScript qaScript = qaInstance.GetComponent<QAPrefabScript>();
                    qaScript.Setup(documentSnapshot, this);
                }
            });
    }

    // On click of edit button the editor fetches the q&a to be edited
    public void PopulateQAForEditing(string qaId)
    {
        currentQAId = qaId;
        AppDocumentReference docRef = db.Collection("businessQA").Document(qaId);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists)
            {
                Debug.LogError("Error fetching Q&A: " + task.Exception);
                ShowFeedback("Failed to fetch Q&A for editing.", false);
            }
            else
            {
                AppDocumentSnapshot snapshot = task.Result;
                questionInputField.text = snapshot.GetValue<string>("question");
                answerInputField.text = snapshot.GetValue<string>("answer");
                ScrollToTop();
            }
        });
    }

    private void ClearInputFields()
    {
        questionInputField.text = "";
        answerInputField.text = "";
        currentQAId = null;
    }

    public void ShowFeedback(string message, bool isSuccess)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isSuccess ? Color.green : Color.red;
            StartCoroutine(ClearFeedbackAfterDelay(2));
        }
        else
        {
            Debug.LogError("Feedback text object not found!");
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