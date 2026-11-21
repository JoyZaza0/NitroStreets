using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using ALIyerEdon;

[DisallowMultipleComponent]
public sealed class PlayerProgressionDisplay : MonoBehaviour
{
    [SerializeField] private Slider experienceSlider;
    [SerializeField] private Text levelText;
    [SerializeField] private Text experienceText;

    private void OnEnable()
    {
        if (experienceSlider == null && levelText == null && experienceText == null)
            CreateDefaultUi();
        else if (experienceSlider == null || levelText == null || experienceText == null)
        {
            Debug.LogError("PlayerProgressionDisplay requires a Slider and level/experience Text references.", this);
            enabled = false;
            return;
        }

        PlayerProgression.ExperienceChanged += Refresh;
        Refresh(PlayerProgression.TotalExperience);
    }

    private void OnDisable()
    {
        PlayerProgression.ExperienceChanged -= Refresh;
    }

   

    private void Refresh(int totalExperience)
    {
        int currentLevel = totalExperience / PlayerProgression.ExperiencePerLevel + 1;
        int experienceIntoLevel = totalExperience % PlayerProgression.ExperiencePerLevel;

        experienceSlider.minValue = 0f;
        experienceSlider.maxValue = PlayerProgression.ExperiencePerLevel;
        experienceSlider.wholeNumbers = true;
        experienceSlider.interactable = false;
        experienceSlider.SetValueWithoutNotify(experienceIntoLevel);

        levelText.text = $"Level {currentLevel}";
        experienceText.text = $"{experienceIntoLevel} / {PlayerProgression.ExperiencePerLevel} XP";
    }

    private void CreateDefaultUi()
    {
        GameObject canvasObject = new GameObject("PlayerProgressionCanvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image panel = CreateImage("ProgressionPanel", canvas.transform, new Color(0.09f, 0.13f, 0.19f, 0.92f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(500f, 132f);

        levelText = CreateText("LevelText", panel.transform, new Vector2(20f, -16f), new Vector2(205f, 38f), 28, TextAnchor.MiddleLeft);
        experienceText = CreateText("ExperienceText", panel.transform, new Vector2(235f, -16f), new Vector2(245f, 38f), 24, TextAnchor.MiddleRight);

        Image sliderBackground = CreateImage("ExperienceSlider", panel.transform, new Color(0.035f, 0.055f, 0.085f, 1f));
        RectTransform sliderRect = sliderBackground.rectTransform;
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(0f, 1f);
        sliderRect.pivot = new Vector2(0f, 1f);
        sliderRect.anchoredPosition = new Vector2(20f, -78f);
        sliderRect.sizeDelta = new Vector2(460f, 24f);

        Image fill = CreateImage("Fill", sliderBackground.transform, new Color(0.12f, 0.55f, 0.9f, 1f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        experienceSlider = sliderBackground.gameObject.AddComponent<Slider>();
        experienceSlider.targetGraphic = sliderBackground;
        experienceSlider.fillRect = fillRect;
        experienceSlider.direction = Slider.Direction.LeftToRight;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string objectName, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform rectTransform = text.rectTransform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
        return text;
    }
}
