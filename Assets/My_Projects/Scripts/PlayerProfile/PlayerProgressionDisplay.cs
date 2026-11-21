using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerProgressionDisplay : MonoBehaviour
{
    [System.Serializable]
    public class ProgressionUIEntry
    {
        public Slider experienceSlider;
        public Text levelText;
        public Text experienceText;

        public bool IsValid => experienceSlider != null || levelText != null || experienceText != null;

        public void Refresh(int totalExperience)
        {
            int currentLevel = totalExperience / PlayerProgression.ExperiencePerLevel + 1;
            int experienceIntoLevel = totalExperience % PlayerProgression.ExperiencePerLevel;

            if (experienceSlider != null)
            {
                experienceSlider.minValue = 0f;
                experienceSlider.maxValue = PlayerProgression.ExperiencePerLevel;
                experienceSlider.wholeNumbers = true;
                experienceSlider.interactable = false;
                experienceSlider.SetValueWithoutNotify(experienceIntoLevel);
            }

            if (levelText != null)
            {
                levelText.text = $"LV. {currentLevel}";
            }

            if (experienceText != null)
            {
                experienceText.text = $"{experienceIntoLevel}/{PlayerProgression.ExperiencePerLevel} XP";
            }
        }
    }

    [Header("Manual UI References (Single)")]
    [SerializeField] private Slider experienceSlider;
    [SerializeField] private Text levelText;
    [SerializeField] private Text experienceText;

    [Header("Manual UI References (Multiple Menus)")]
    [Tooltip("Список UI-элементов прогрессии для разных меню")]
    [SerializeField] private ProgressionUIEntry[] additionalUiEntries;

    [Header("Auto-Attach in Garage Menus")]
    [Tooltip("Автоматически встраивать аккуратную шкалу уровня и опыта в каждый блок Player_Profile во всех меню Гаража")]
    [SerializeField] private bool autoAttachToPlayerProfiles = true;

    private readonly List<ProgressionUIEntry> activeEntries = new List<ProgressionUIEntry>();

    private static bool isSubscribedToSceneLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!isSubscribedToSceneLoaded)
        {
            isSubscribedToSceneLoaded = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    public static PlayerProgressionDisplay EnsureInstance()
    {
        PlayerProgressionDisplay existing = FindFirstObjectByType<PlayerProgressionDisplay>();
        if (existing != null)
            return existing;

        Canvas mainCanvas = FindFirstObjectByType<Canvas>();
        if (mainCanvas != null)
        {
            return mainCanvas.gameObject.AddComponent<PlayerProgressionDisplay>();
        }

        return null;
    }

    private void Awake()
    {
        InitializeEntries();
    }

    private void OnEnable()
    {
        PlayerProgression.ExperienceChanged += RefreshAll;
        RefreshAll(PlayerProgression.TotalExperience);
    }

    private void OnDisable()
    {
        PlayerProgression.ExperienceChanged -= RefreshAll;
    }

    private void InitializeEntries()
    {
        activeEntries.Clear();

        // 1. Ручные одиночные привязки
        if (experienceSlider != null || levelText != null || experienceText != null)
        {
            activeEntries.Add(new ProgressionUIEntry
            {
                experienceSlider = experienceSlider,
                levelText = levelText,
                experienceText = experienceText
            });
        }

        // 2. Дополнительные привязки для нескольких меню
        if (additionalUiEntries != null)
        {
            for (int i = 0; i < additionalUiEntries.Length; i++)
            {
                if (additionalUiEntries[i] != null && additionalUiEntries[i].IsValid)
                {
                    activeEntries.Add(additionalUiEntries[i]);
                }
            }
        }

        // 3. Автоматический поиск и встраивание в Player_Profile во всех меню Гаража
        if (autoAttachToPlayerProfiles)
        {
            SetupGaragePlayerProfiles();
        }

        // 4. Fallback оверлей, если вообще ничего не найдено и не привязано
        if (activeEntries.Count == 0)
        {
            CreateDefaultUi();
        }
    }

    private void SetupGaragePlayerProfiles()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
            return;

        // Находим все Player_Profile (включая неактивные меню)
        Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
                continue;

            if (t.name == "Player_Profile")
            {
                AttachProgressionBarToProfile(t);
            }
        }
    }

    private void AttachProgressionBarToProfile(Transform profileTransform)
    {
        Transform existingBar = profileTransform.Find("ProgressionBar");
        if (existingBar != null)
            return;

        // Ищем Player_Name_Text внутри этого Player_Profile
        Text nameText = null;
        Text[] texts = profileTransform.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name.Contains("Name") || texts[i].name == "Player_Name_Text")
            {
                nameText = texts[i];
                break;
            }
        }

        Font font = (nameText != null && nameText.font != null)
            ? nameText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        float posX = 67.5f;
        float width = 266f;

        if (nameText != null)
        {
            RectTransform nameRect = nameText.rectTransform;
            posX = nameRect.anchoredPosition.x;
            width = nameRect.sizeDelta.x;

            // Смещаем имя чуть выше, освобождая место для аккуратной полосы прогресса
            nameRect.anchoredPosition = new Vector2(posX, 18f);
            nameRect.sizeDelta = new Vector2(width, 38f);
            nameText.alignment = TextAnchor.MiddleLeft;
        }

        // Контейнер ProgressionBar
        GameObject barObject = new GameObject("ProgressionBar", typeof(RectTransform));
        barObject.transform.SetParent(profileTransform, false);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(posX, -20f);
        barRect.sizeDelta = new Vector2(width, 26f);

        // Текст уровня (LV. X)
        Text levelBadge = CreateText("LevelBadge", barObject.transform,
            new Vector2(0f, 0f), new Vector2(58f, 24f), 18, TextAnchor.MiddleLeft, font, new Color(1f, 0.84f, 0.25f, 1f));
        levelBadge.fontStyle = FontStyle.Bold;
        RectTransform lvlRect = levelBadge.rectTransform;
        lvlRect.anchorMin = new Vector2(0f, 0.5f);
        lvlRect.anchorMax = new Vector2(0f, 0.5f);
        lvlRect.pivot = new Vector2(0f, 0.5f);
        lvlRect.anchoredPosition = Vector2.zero;

        // Полоса опыта (Slider)
        Image sliderBg = CreateImage("ExperienceSlider", barObject.transform, new Color(0.08f, 0.12f, 0.18f, 0.9f));
        RectTransform sliderRect = sliderBg.rectTransform;
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(0f, 0.5f);
        sliderRect.pivot = new Vector2(0f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(62f, 0f);
        sliderRect.sizeDelta = new Vector2(130f, 12f);

        Image fill = CreateImage("Fill", sliderBg.transform, new Color(0.2f, 0.6f, 1f, 1f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Slider slider = sliderBg.gameObject.AddComponent<Slider>();
        slider.targetGraphic = sliderBg;
        slider.fillRect = fillRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = PlayerProgression.ExperiencePerLevel;
        slider.wholeNumbers = true;
        slider.interactable = false;

        // Текст количества опыта (XP / Max)
        Text xpText = CreateText("ExperienceText", barObject.transform,
            Vector2.zero, new Vector2(70f, 22f), 13, TextAnchor.MiddleRight, font, new Color(0.82f, 0.88f, 0.95f, 0.95f));
        RectTransform xpRect = xpText.rectTransform;
        xpRect.anchorMin = new Vector2(1f, 0.5f);
        xpRect.anchorMax = new Vector2(1f, 0.5f);
        xpRect.pivot = new Vector2(1f, 0.5f);
        xpRect.anchoredPosition = Vector2.zero;

        activeEntries.Add(new ProgressionUIEntry
        {
            experienceSlider = slider,
            levelText = levelBadge,
            experienceText = xpText
        });
    }

    private void RefreshAll(int totalExperience)
    {
        for (int i = 0; i < activeEntries.Count; i++)
        {
            if (activeEntries[i] != null && activeEntries[i].IsValid)
            {
                activeEntries[i].Refresh(totalExperience);
            }
        }
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

        activeEntries.Add(new ProgressionUIEntry
        {
            experienceSlider = experienceSlider,
            levelText = levelText,
            experienceText = experienceText
        });
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string objectName, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Font customFont = null, Color? customColor = null)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = customColor ?? Color.white;
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
