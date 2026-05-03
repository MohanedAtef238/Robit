using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using System.Collections;

public class GeminiChatWidget : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private TextField inputDialogueField;
    private Label outputDialogueLabel;
    private Button inputButton;

    private const string API_KEY = "AIzaSyAy7qQunp79ix0ZeidTrqPrcCR4JZ3oE8I";
    private const string GEMINI_MODEL = "models/gemini-2.5-flash-lite";

    void Start()
    {
        if (uiDocument == null)
        {
            RobitLogger.LogError("UIDocument not assigned!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        inputDialogueField = root.Q<TextField>("inputDialogueField");
        outputDialogueLabel = root.Q<Label>("outputDialogueLabel");
        inputButton = root.Q<Button>("inputButton");

        if (inputDialogueField == null || outputDialogueLabel == null || inputButton == null)
        {
            RobitLogger.LogError("One or more UI elements not found! Check names in UI Builder.");
            return;
        }

        // Initialize visibility
        inputDialogueField.style.display = DisplayStyle.Flex;
        outputDialogueLabel.style.display = DisplayStyle.None;

        // Toggle visibility when input field is clicked
        inputDialogueField.RegisterCallback<PointerDownEvent>(evt =>
        {
            ToggleUI(showInput: true);
        });

        // Toggle visibility when output label is clicked
        outputDialogueLabel.RegisterCallback<PointerDownEvent>(evt =>
        {
            ToggleUI(showInput: true); // clicking output label shows input
        });

        // Button click to send
        inputButton.clicked += OnSendClicked;

        // Enter key sends
        inputDialogueField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                OnSendClicked();
                evt.StopPropagation();
            }
        });
    }

    // -----------------------------
    // Toggle input/output visibility
    // -----------------------------
    void ToggleUI(bool showInput)
    {
        inputDialogueField.style.display = showInput ? DisplayStyle.Flex : DisplayStyle.None;
        outputDialogueLabel.style.display = showInput ? DisplayStyle.None : DisplayStyle.Flex;

        if (showInput)
        {
            inputDialogueField.value = ""; // Clear previous text
            inputDialogueField.Focus();
        }
    }

    void OnSendClicked()
    {
        string userText = inputDialogueField.value.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        // Hide input and show output
        ToggleUI(showInput: false);
        outputDialogueLabel.text = "Thinking...";

        StartCoroutine(GetGeminiReply(userText));
    }

    IEnumerator GetGeminiReply(string prompt)
    {
        string url = $"https://generativelanguage.googleapis.com/v1/{GEMINI_MODEL}:generateContent?key={API_KEY}";
        string jsonBody = $@"
        {{
            ""contents"": [
                {{
                    ""parts"": [
                        {{ ""text"": ""{EscapeJson(prompt)}"" }}
                    ]
                }}
            ]
        }}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            RobitLogger.LogError($"Gemini API Error: {request.result}\n{request.downloadHandler.text}");
            outputDialogueLabel.text = "Error getting response. Check console.";
            yield break;
        }

        string response = request.downloadHandler.text;
        string reply = ParseGeminiResponse(response);
        outputDialogueLabel.text = reply;
    }

    [System.Serializable]
    public class GeminiResponse { public Candidate[] candidates; }
    [System.Serializable]
    public class Candidate { public Content content; }
    [System.Serializable]
    public class Content { public Part[] parts; }
    [System.Serializable]
    public class Part { public string text; }

    string ParseGeminiResponse(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<GeminiResponse>(json);
            if (data != null &&
                data.candidates != null &&
                data.candidates.Length > 0 &&
                data.candidates[0].content.parts.Length > 0)
            {
                return data.candidates[0].content.parts[0].text;
            }
        }
        catch (System.Exception ex)
        {
            RobitLogger.LogWarning("Failed to parse Gemini response: " + ex);
        }
        return "No response";
    }

    string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r");
    }
}
