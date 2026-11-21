using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerRegistrationBootstrap : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "Garage";
    [SerializeField] private Sprite[] flagOptions;

    private InputField nameInput;
    private Image flagPreview;
    private Text statusText;
    private int selectedFlagIndex;
    private bool isLoadingScene;

    private void Start()
    {
        if (PlayerProfileStore.IsRegistered)
        {
            LoadMainMenu();
            return;
        }

        if (flagOptions == null || flagOptions.Length == 0)
        {
            Debug.LogError("PlayerRegistrationBootstrap requires at least one flag sprite.", this);
            return;
        }

        var canvas = FindAnyObjectByType<Canvas>();
        
        if (canvas == null)
            CreateRegistrationUi();
    }

    [ContextMenu("Create Registration UI")]
    private void CreateRegistrationUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("PlayerRegistrationCanvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920f, 1200f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image backdrop = CreateImage("Backdrop", canvas.transform, new Color(0.035f, 0.055f, 0.085f, 1f));
        StretchToParent(backdrop.rectTransform);

        Image panel = CreateImage("RegistrationPanel", canvas.transform, new Color(0.09f, 0.13f, 0.19f, 1f));
        SetCenteredRect(panel.rectTransform, Vector2.zero, new Vector2(700f, 760f));

        Text title = CreateText("Title", panel.transform, new Vector2(0f, 310f), new Vector2(620f, 70f), 40, TextAnchor.MiddleCenter, Color.white);
        title.text = "PLAYER REGISTRATION";
        Text nameLabel = CreateText("NameLabel", panel.transform, new Vector2(0f, 220f), new Vector2(560f, 48f), 26, TextAnchor.MiddleLeft, Color.white);
        nameLabel.text = "Enter player name";

        Image inputBackground = CreateImage("NameInputBackground", panel.transform, new Color(0.16f, 0.21f, 0.29f, 1f));
        RectTransform inputRect = inputBackground.rectTransform;
        SetCenteredRect(inputRect, new Vector2(0f, 155f), new Vector2(560f, 72f));

        nameInput = inputBackground.gameObject.AddComponent<InputField>();
        nameInput.characterLimit = 24;
        nameInput.lineType = InputField.LineType.SingleLine;
        nameInput.targetGraphic = inputBackground;

        Text inputText = CreateText("InputText", inputBackground.transform, Vector2.zero, Vector2.zero, 28, TextAnchor.MiddleLeft, Color.white);
        StretchToParent(inputText.rectTransform);
        inputText.rectTransform.offsetMin = new Vector2(18f, 6f);
        inputText.rectTransform.offsetMax = new Vector2(-18f, -6f);
        nameInput.textComponent = inputText;

        Text placeholder = CreateText("Placeholder", inputBackground.transform, Vector2.zero, Vector2.zero, 26, TextAnchor.MiddleLeft, new Color(0.65f, 0.69f, 0.75f, 1f));
        placeholder.text = "Name";
        placeholder.fontStyle = FontStyle.Italic;
        StretchToParent(placeholder.rectTransform);
        placeholder.rectTransform.offsetMin = new Vector2(18f, 6f);
        placeholder.rectTransform.offsetMax = new Vector2(-18f, -6f);
        nameInput.placeholder = placeholder;

        Text flagLabel = CreateText("FlagLabel", panel.transform, new Vector2(0f, 65f), new Vector2(560f, 48f), 26, TextAnchor.MiddleCenter, Color.white);
        flagLabel.text = "Choose a flag";
        flagPreview = CreateImage("FlagPreview", panel.transform, Color.white);
        SetCenteredRect(flagPreview.rectTransform, new Vector2(0f, -50f), new Vector2(300f, 170f));
        flagPreview.preserveAspect = true;

        Button previousFlagButton = CreateButton("PreviousFlagButton", panel.transform, "‹", new Vector2(-235f, -50f), new Vector2(80f, 80f));
        previousFlagButton.onClick.AddListener(SelectPreviousFlag);
        Button nextFlagButton = CreateButton("NextFlagButton", panel.transform, "›", new Vector2(235f, -50f), new Vector2(80f, 80f));
        nextFlagButton.onClick.AddListener(SelectNextFlag);
        UpdateFlagPreview();

        statusText = CreateText("StatusText", panel.transform, new Vector2(0f, -180f), new Vector2(560f, 48f), 22, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.45f, 1f));
        statusText.text = string.Empty;

        Button submitButton = CreateButton("ContinueButton", panel.transform, "CONTINUE", new Vector2(0f, -270f), new Vector2(360f, 78f));
        submitButton.onClick.AddListener(SubmitRegistration);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        SetCenteredRect(text.rectTransform, position, size);
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size)
    {
        Image background = CreateImage(objectName, parent, new Color(0.12f, 0.48f, 0.78f, 1f));
        SetCenteredRect(background.rectTransform, position, size);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        Text text = CreateText("Label", background.transform, Vector2.zero, Vector2.zero, label.Length > 2 ? 24 : 52, TextAnchor.MiddleCenter, Color.white);
        StretchToParent(text.rectTransform);
        text.text = label;
        return button;
    }

    private void SelectPreviousFlag()
    {
        selectedFlagIndex = (selectedFlagIndex - 1 + flagOptions.Length) % flagOptions.Length;
        UpdateFlagPreview();
    }

    private void SelectNextFlag()
    {
        selectedFlagIndex = (selectedFlagIndex + 1) % flagOptions.Length;
        UpdateFlagPreview();
    }

    private void UpdateFlagPreview()
    {
        flagPreview.sprite = flagOptions[selectedFlagIndex];
        if (flagOptions[selectedFlagIndex] == null)
            Debug.LogError($"Flag sprite at index {selectedFlagIndex} is not assigned.", this);
    }

    private void SubmitRegistration()
    {
        string playerName = nameInput.text.Trim();
        if (playerName.Length == 0)
        {
            statusText.text = "Please enter a name.";
            return;
        }

        if (flagOptions[selectedFlagIndex] == null)
        {
            statusText.text = "The selected flag is unavailable.";
            return;
        }

        if (!PlayerProfileStore.Save(playerName, selectedFlagIndex))
        {
            statusText.text = "Could not save the player profile.";
            Debug.LogError("Player profile could not be saved.");
            return;
        }

        LoadMainMenu();
    }

    private void LoadMainMenu()
    {
        if (isLoadingScene)
            return;

        if (string.IsNullOrWhiteSpace(mainMenuScene))
        {
            Debug.LogError("PlayerRegistrationBootstrap has no main menu scene configured.", this);
            return;
        }

        isLoadingScene = true;
        AsyncOperation operation = SceneManager.LoadSceneAsync(mainMenuScene);
        if (operation == null)
        {
            isLoadingScene = false;
            Debug.LogError($"Could not load the main menu scene '{mainMenuScene}'. Add it to Build Settings.", this);
        }
    }

    private static void SetCenteredRect(RectTransform rectTransform, Vector2 position, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
