using UnityEngine;
using AppFirebase;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;

// This script implements the chat functionality between normal users and business users
public class ChatManager : MonoBehaviour
{
    // Normal user UI references
    public GameObject userChatCanvas;
    public Transform userParentList;
    public GameObject userPrefabList;
    public InputField userInputField;
    public Button userSendButton;
    public Transform userParentChat;
    public GameObject userMessagePrefab;
    public GameObject userChatName;

    // Business user UI references
    public GameObject businessChatCanvas;
    public Transform businessParentList;
    public GameObject businessPrefabList;
    public InputField businessInputField;
    public Button businessSendButton;
    public Transform businessParentChat;
    public GameObject businessMessagePrefab;
    public GameObject businessChatName;

    private AppFirestore db;
    private const string UsersCollection = "users";
    private const string BusinessUsersCollection = "bus_users";
    private const string ChatCollection = "chats";
    private string localUserID;
    private string currentBusinessID;
    private string selectedUserID;

    private bool chatInitialised = false;

    void Start()
    {
        InitialiseFirebase();
        ClearChatHistory();
    }

    void Update()
    {
        // No chat canvas are active do nothing
        if (!userChatCanvas.activeSelf && !businessChatCanvas.activeSelf)
        {
            chatInitialised = false;
            return;
        }

        // If one of the canvases is active initialise the chat
        if (!chatInitialised && (userChatCanvas.activeSelf || businessChatCanvas.activeSelf))
        {
            ClearChatHistory();
            if (AppAuth.DefaultInstance.CurrentUser != null)
            {
                localUserID = AppAuth.DefaultInstance.CurrentUser.UserId;
            }
            else
            {
                Debug.LogError("No authenticated user found.");
            }
            InitialiseChatUI();
            FetchAndDisplayUsers();
            FetchAndDisplayBusinesses();
            chatInitialised = true;
        }
    }

    // Initial setup firebase
    void InitialiseFirebase()
    {
        // FirebaseApp.CheckAndFixDependenciesAsync() was an Android-only Google Play
        // Services check and doesn't exist for WebGL - AppFirestore.DefaultInstance is
        // ready to use immediately on every platform.
        db = AppFirestore.DefaultInstance;
    }

    // Method called when a normal user is selected from the list
    public void OnUserSelected(string userID, string businessName)
    {
        // Clears chat and fetches history
        Debug.Log($"Business selected with ID: {userID}");
        currentBusinessID = userID;
        selectedUserID = null;

        foreach (Transform child in userParentChat)
        {
            Destroy(child.gameObject);
        }

        var userChatNameText = userChatName.GetComponent<Text>();
        userChatNameText.text = businessName;

        FetchChatHistory(userID);
    }

    // Method called when a business user is selected from the list
    public void OnBusinessSelected(string businessID, string businessName)
    {
        // Clears chat and fetches history
        Debug.Log($"Candidate selected with ID: {businessID}");
        currentBusinessID = businessID;
        selectedUserID = null;

        foreach (Transform child in businessParentChat)
        {
            Destroy(child.gameObject);
        }

        var businessChatNameText = businessChatName.GetComponent<Text>();
        businessChatNameText.text = businessName;
        FetchChatHistory(businessID);
    }

    // Fetches chat history between the current user and selected user
    private void FetchChatHistory(string otherUserID)
    {
        ClearChatHistory();
        string chatPartners = CreateChatPartnersComposite(localUserID, otherUserID);

        // Fetching messages where the 2 ids are in chatPartners store in a list and sort with timestamp
        var chatQuery = db.Collection(ChatCollection)
            .WhereEqualTo("chatPartners", chatPartners)
            .OrderBy("timestamp");

        chatQuery.GetSnapshotAsync().ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted)
            {
                Debug.LogError("Error fetching messages: " + t.Exception);
                return;
            }

            var allMessages = new List<Dictionary<string, object>>();
            if (t.Result != null)
            {
                foreach (var doc in t.Result.Documents)
                {
                    allMessages.Add(doc.ToDictionary());
                }
            }

            allMessages.Sort((x, y) => ((AppTimestamp)x["timestamp"]).CompareTo((AppTimestamp)y["timestamp"]));
            DisplayMessages(allMessages);
        });
    }

    // Clearing chat history
    private void ClearChatHistory()
    {
        foreach (Transform child in userParentChat)
        {
            Destroy(child.gameObject);
        }
        foreach (Transform child in businessParentChat)
        {
            Destroy(child.gameObject);
        }
    }

    // Method to initialise each individual message
    private void DisplayMessages(List<Dictionary<string, object>> messages)
    {
        foreach (var messageData in messages)
        {
            CreateMessageItem(messageData);
        }
    }

    // Adding/removing listeners to send button for each chat
    void InitialiseChatUI()
    {
        userSendButton.onClick.RemoveAllListeners();
        businessSendButton.onClick.RemoveAllListeners();

        userSendButton.onClick.AddListener(OnUserSendButtonClicked);
        businessSendButton.onClick.AddListener(OnBusinessSendButtonClicked);
    }

    // Creates a message item assigning all the values in firebase and refreshes UI
    public void SendMessage(string message, bool isUserSender)
    {
        string receiverID = isUserSender ? currentBusinessID : selectedUserID;
        string chatPartners = CreateChatPartnersComposite(localUserID, receiverID);

        if (string.IsNullOrEmpty(receiverID))
        {
            Debug.LogError("No recipient selected.");
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("No message to send");
            return;
        }

        var messageData = new Dictionary<string, object>
        {
            { "senderID", localUserID },
            { "receiverID", isUserSender ? currentBusinessID : localUserID },
            { "timestamp", AppTimestamp.GetCurrentTimestamp() },
            { "text", message },
            { "chatPartners", chatPartners }
        };

        db.Collection(ChatCollection).AddAsync(messageData).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Error sending message: " + task.Exception);
                return;
            }
            Debug.Log("Message sent successfully.");
            RefreshChatUI(message, isUserSender);
        });
    }

    // Creating a composite to identify components of the same chat
    private string CreateChatPartnersComposite(string userID1, string userID2)
    {
        List<string> ids = new List<string> { userID1, userID2 };
        ids.Sort();
        return string.Join("_", ids);
    }

    // Refreshing chat after sending message
    private void RefreshChatUI(string message, bool isUserSender)
    {
        CreateMessageItem(new Dictionary<string, object>
        {
            { "text", message },
            { "senderID", isUserSender ? localUserID : currentBusinessID }
        });
    }

    // Creates and distinguishes messages in the UI using a prefab
    private void CreateMessageItem(Dictionary<string, object> messageData)
    {
        string messageText = messageData["text"] as string;
        string senderID = messageData["senderID"] as string;
        Transform parent;
        GameObject prefab;

        if (userChatCanvas.activeSelf)
        {
            parent = userParentChat;
            prefab = userMessagePrefab;
        }
        else if (businessChatCanvas.activeSelf)
        {
            parent = businessParentChat;
            prefab = businessMessagePrefab;
        }
        else
        {
            Debug.LogError("No chat canvas is active.");
            return;
        }

        GameObject messageItem = Instantiate(prefab, parent);
        messageItem.GetComponentInChildren<Text>().text = messageText;

        Image messageImage = messageItem.GetComponentInChildren<Image>();
        if (senderID == localUserID)
        {
            messageImage.color = new Color(0.8f, 0.9f, 1.0f);
        }
        else
        {
            messageImage.color = new Color(0.9f, 0.9f, 0.9f);
        }
    }

    // Sending the message and clearing the field
    private void OnUserSendButtonClicked()
    {
        string message = userInputField.text;
        SendMessage(message, true);
        userInputField.text = "";
    }

    // Sending the message and clearing the field
    private void OnBusinessSendButtonClicked()
    {
        string message = businessInputField.text;
        SendMessage(message, true);
        businessInputField.text = "";
    }

    // Allows to fetch businesses and populate a list of contacts for users
    private void FetchAndDisplayUsers()
    {
        db.Collection(BusinessUsersCollection).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error fetching users: " + task.Exception);
                return;
            }

            foreach (Transform child in userParentList)
            {
                Destroy(child.gameObject);
            }

            foreach (AppDocumentSnapshot document in task.Result.Documents)
            {
                Dictionary<string, object> user = document.ToDictionary();
                string userID = document.Id;
                string businessName = user["BusinessName"] as string;

                GameObject userItem = Instantiate(userPrefabList, userParentList);
                userItem.GetComponentInChildren<Text>().text = businessName;

                Button userButton = userItem.GetComponent<Button>();
                userButton.onClick.AddListener(delegate { OnUserSelected(userID, businessName); });
            }
        });
    }

    // Allows to fetch users and populate a list of contacts for businesses
    private void FetchAndDisplayBusinesses()
    {
        db.Collection(UsersCollection).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Error fetching businesses: " + task.Exception);
                return;
            }

            foreach (Transform child in businessParentList)
            {
                Destroy(child.gameObject);
            }

            foreach (AppDocumentSnapshot document in task.Result.Documents)
            {
                Dictionary<string, object> business = document.ToDictionary();
                string businessName = business["FirstName"] as string;
                GameObject businessItem = Instantiate(businessPrefabList, businessParentList);
                businessItem.GetComponentInChildren<Text>().text = businessName;
                Button businessButton = businessItem.GetComponent<Button>();
                string businessID = document.Id;
                businessButton.onClick.AddListener(delegate { OnBusinessSelected(businessID, businessName); });
            }
        });
    }
}