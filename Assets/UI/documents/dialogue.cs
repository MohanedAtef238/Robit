using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using System.Collections;

public class GeminiChatWidget : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private TextField _inputField;
    private Label _outputLabel;
    private Button _inputButton;
    private bool _isDisplayingOutput = false;

    private const string API_KEY = "AIzaSyAwe47o5MO4EYYTLr3qq2KxGx86pTMN5qQ";

    private const string API_URL = "https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash-lite:generateContent";

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _inputField = root.Q<TextField>("inputDialogueField");
        _outputLabel = root.Q<Label>("outputDialogueLabel");
        _inputButton = root.Q<Button>("inputButton");

        _inputButton.clicked += OnMainButtonClick;

        // Allow Enter key to trigger the same logic
        _inputField.RegisterCallback<KeyDownEvent>(evt => {
            if (evt.keyCode == KeyCode.Return) OnMainButtonClick();
        });

        ResetUI();
    }

    void OnMainButtonClick()
    {
        if (_isDisplayingOutput)
        {
            ResetUI();
        }
        else
        {
            string prompt = _inputField.value.Trim();
            if (!string.IsNullOrEmpty(prompt)) SendPrompt(prompt);
        }
    }

    void ResetUI()
    {
        _isDisplayingOutput = false;
        _inputField.style.display = DisplayStyle.Flex;
        _outputLabel.style.display = DisplayStyle.None;
        _inputField.value = "";
        _inputField.Focus();
    }

    void SendPrompt(string prompt)
    {
        _isDisplayingOutput = true;
        _inputField.style.display = DisplayStyle.None;
        _outputLabel.style.display = DisplayStyle.Flex;
        _outputLabel.text = "Thinking...";

        StartCoroutine(PostRequest(prompt));
    }

    IEnumerator PostRequest(string prompt)
    {
        // A cleaner way to build the JSON to avoid formatting errors
        string json = "{\"contents\":[{\"parts\":[{\"text\":\"" + prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"}]}]}";

        using (UnityWebRequest req = new UnityWebRequest($"{API_URL}?key={API_KEY}", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<GeminiResponse>(req.downloadHandler.text);
                _outputLabel.text = response.candidates[0].content.parts[0].text;
            }
            else
            {
                // This will tell us exactly WHY it failed in the console
                Debug.LogError($"API Error: {req.responseCode} - {req.downloadHandler.text}");
                _outputLabel.text = "Error: Check the console for details.";
            }
        }
    }

    [System.Serializable] public class GeminiResponse { public Candidate[] candidates; }
    [System.Serializable] public class Candidate { public Content content; }
    [System.Serializable] public class Content { public Part[] parts; }
    [System.Serializable] public class Part { public string text; }
}