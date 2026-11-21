using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ALIyerEdon;
using NitroStreets.Ads;

namespace NitroStreets.Fuel
{
    /// <summary>
    /// UI-компонент отображения запаса топлива ("10/10"), таймера восстановления ("09:59")
    /// и окна предложения посмотреть рекламу за +1 топливо для конкретного режима (GameMode).
    /// Поддерживает автоматическую генерацию UI на панели выбора уровней.
    /// </summary>
    public class ModeFuelUI : MonoBehaviour
    {
        [Header("Режим")]
        [SerializeField] private GameMode targetMode = GameMode.Career;

        [Header("Тексты индикатора топлива (10/10 и таймер)")]
        [SerializeField] private Text fuelCountText;
        [SerializeField] private TMP_Text fuelCountTmpText;
        [SerializeField] private string fuelFormat = "{0}/{1}";

        [SerializeField] private Text timerText;
        [SerializeField] private TMP_Text timerTmpText;
        [SerializeField] private GameObject timerContainer;
        [SerializeField] private bool hideTimerWhenFull = false;
        [SerializeField] private string fullTimerText = "FULL";

        [Header("Опциональная кнопка пополнения (+)")]
        [SerializeField] private Button openOfferButton;

        [Header("Окно предложения рекламы (при клике на уровень с 0/10)")]
        [SerializeField] private GameObject noFuelOfferPopup;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private Button closeOfferButton;
        [SerializeField] private Text popupStatusText;
        [SerializeField] private TMP_Text popupStatusTmpText;
        [SerializeField] private Text popupTimerText;
        [SerializeField] private TMP_Text popupTimerTmpText;

        [Header("Цвета индикации")]
        [SerializeField] private Color normalFuelColor = Color.white;
        [SerializeField] private Color emptyFuelColor = new Color(1f, 0.35f, 0.35f, 1f);

        [Header("Авто-генерация UI")]
        [Tooltip("Автоматически создавать виджет топлива и окно предложения рекламы, если они ещё не назначены")]
        [SerializeField] private bool autoGenerateMissingUI = true;

        private float nextUiRefreshTime;

        public GameMode TargetMode
        {
            get => targetMode;
            set
            {
                targetMode = value;
                RefreshUI();
            }
        }

        private void Awake()
        {
            if (autoGenerateMissingUI)
            {
                EnsureUIExists();
            }

            BindButtons();

            if (noFuelOfferPopup != null)
            {
                noFuelOfferPopup.SetActive(false);
            }
        }

        private void OnEnable()
        {
            ModeFuelManager manager = ModeFuelManager.EnsureInstance();
            if (manager != null)
            {
                manager.OnFuelChanged += HandleFuelChanged;
            }

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRewardedAdLoadedEvent += UpdateWatchAdButtonState;
            }

            RefreshUI();
        }

        private void OnDisable()
        {
            if (ModeFuelManager.Instance != null)
            {
                ModeFuelManager.Instance.OnFuelChanged -= HandleFuelChanged;
            }

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRewardedAdLoadedEvent -= UpdateWatchAdButtonState;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextUiRefreshTime)
            {
                nextUiRefreshTime = Time.unscaledTime + 0.25f;
                RefreshTimerOnly();
            }
        }

        private void BindButtons()
        {
            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            if (closeOfferButton != null)
            {
                closeOfferButton.onClick.RemoveListener(HideNoFuelAdOffer);
                closeOfferButton.onClick.AddListener(HideNoFuelAdOffer);
            }

            if (openOfferButton != null)
            {
                openOfferButton.onClick.RemoveListener(OnOpenOfferButtonClicked);
                openOfferButton.onClick.AddListener(OnOpenOfferButtonClicked);
            }
        }

        private void HandleFuelChanged(GameMode changedMode, int current, int max)
        {
            if (changedMode != targetMode)
                return;

            RefreshUI();
        }

        /// <summary>
        /// Открывает окно предложения посмотреть рекламу за +1 топливо.
        /// Вызывается ТОЛЬКО когда игрок кликает на уровень при 0/10 топлива (или на кнопку +).
        /// </summary>
        public void ShowNoFuelAdOffer()
        {
            if (autoGenerateMissingUI && noFuelOfferPopup == null)
            {
                EnsureUIExists();
                BindButtons();
            }

            if (noFuelOfferPopup != null)
            {
                noFuelOfferPopup.transform.SetAsLastSibling();
                noFuelOfferPopup.SetActive(true);
            }

            RefreshUI();
            UpdateWatchAdButtonState();
        }

        public void HideNoFuelAdOffer()
        {
            if (noFuelOfferPopup != null)
            {
                noFuelOfferPopup.SetActive(false);
            }
        }

        private void OnOpenOfferButtonClicked()
        {
            ModeFuelManager manager = ModeFuelManager.EnsureInstance();
            if (manager != null && manager.GetCurrentFuel(targetMode) < manager.GetMaxFuel(targetMode))
            {
                ShowNoFuelAdOffer();
            }
        }

        private void OnWatchAdClicked()
        {
            ModeFuelManager fuelManager = ModeFuelManager.EnsureInstance();
            int rewardAmount = fuelManager != null ? fuelManager.GetAdRewardAmount(targetMode) : 1;

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.ShowRewardedAd(
                    onRewardEarned: () =>
                    {
                        ModeFuelManager.EnsureInstance().AddFuel(targetMode, rewardAmount);
                        HideNoFuelAdOffer();
                        RefreshUI();
                    },
                    onAdClosed: UpdateWatchAdButtonState);
            }
            else
            {
                Debug.LogWarning("[ModeFuelUI] AdMobManager не найден, начисляем топливо в режиме симуляции.");
                ModeFuelManager.EnsureInstance().AddFuel(targetMode, rewardAmount);
                HideNoFuelAdOffer();
                RefreshUI();
            }
        }

        private void UpdateWatchAdButtonState()
        {
            if (watchAdButton == null)
                return;

            ModeFuelManager manager = ModeFuelManager.EnsureInstance();
            bool needFuel = manager != null && manager.GetCurrentFuel(targetMode) < manager.GetMaxFuel(targetMode);
            bool adReady = AdMobManager.Instance == null || AdMobManager.Instance.IsRewardedAdReady();

            watchAdButton.interactable = needFuel && adReady;
        }

        public void RefreshUI()
        {
            ModeFuelManager manager = ModeFuelManager.EnsureInstance();
            if (manager == null)
                return;

            int current = manager.GetCurrentFuel(targetMode);
            int max = manager.GetMaxFuel(targetMode);
            bool isEmpty = current <= 0;
            bool isFull = current >= max;

            string countFormatted = string.Format(fuelFormat, current, max);
            Color countColor = isEmpty ? emptyFuelColor : normalFuelColor;

            if (fuelCountText != null)
            {
                fuelCountText.text = countFormatted;
                fuelCountText.color = countColor;
            }

            if (fuelCountTmpText != null)
            {
                fuelCountTmpText.text = countFormatted;
                fuelCountTmpText.color = countColor;
            }

            if (openOfferButton != null)
            {
                openOfferButton.gameObject.SetActive(!isFull);
            }

            int rewardAmount = manager.GetAdRewardAmount(targetMode);
            string popupMsg = isEmpty
                ? $"OUT OF FUEL ({current}/{max})\nWatch an ad to restore +{rewardAmount} Fuel!"
                : $"FUEL: {current}/{max}\nWatch an ad to restore +{rewardAmount} Fuel!";

            if (popupStatusText != null)
                popupStatusText.text = popupMsg;
            if (popupStatusTmpText != null)
                popupStatusTmpText.text = popupMsg;

            RefreshTimerOnly();
            UpdateWatchAdButtonState();
        }

        private void RefreshTimerOnly()
        {
            ModeFuelManager manager = ModeFuelManager.Instance;
            if (manager == null)
                return;

            bool hasActiveTimer = manager.TryGetSecondsUntilNextFuel(targetMode, out int secondsLeft);

            if (timerContainer != null)
            {
                if (hideTimerWhenFull)
                    timerContainer.SetActive(hasActiveTimer);
                else if (!timerContainer.activeSelf)
                    timerContainer.SetActive(true);
            }

            string formattedTimer;
            if (hasActiveTimer)
            {
                int minutes = secondsLeft / 60;
                int seconds = secondsLeft % 60;
                formattedTimer = $"{minutes:00}:{seconds:00}";
            }
            else
            {
                formattedTimer = hideTimerWhenFull ? string.Empty : fullTimerText;
            }

            if (timerText != null)
                timerText.text = formattedTimer;

            if (timerTmpText != null)
                timerTmpText.text = formattedTimer;

            string popupTimerStr = hasActiveTimer
                ? $"Next +1 fuel in: {formattedTimer}"
                : "Fuel tank is full!";

            if (popupTimerText != null)
                popupTimerText.text = popupTimerStr;

            if (popupTimerTmpText != null)
                popupTimerTmpText.text = popupTimerStr;
        }

        /// <summary>
        /// Автоматически создаёт или находит и привязывает UI-элементы топлива (виджет 10/10 + таймер 09:59 и окно рекламы).
        /// </summary>
        [ContextMenu("Сгенерировать / Настроить UI топлива")]
        public void EnsureUIExists()
        {
            LevelSelect levelSelect = GetComponent<LevelSelect>();
            if (levelSelect == null)
                levelSelect = GetComponentInParent<LevelSelect>(true);

            Font sharedFont = ResolveSharedFont(levelSelect);

            // 1. Виджет топлива в верхней панели (10/10 + таймер)
            if (fuelCountText == null && fuelCountTmpText == null)
            {
                Transform topHeader = FindTopHeaderTransform();
                Transform parentForWidget = topHeader != null ? topHeader : transform;

                Transform existingWidget = parentForWidget.Find("Fuel_Widget");
                if (existingWidget == null)
                {
                    existingWidget = CreateFuelWidget(parentForWidget, sharedFont);
                }

                BindWidgetReferences(existingWidget);
            }

            // 2. Модальное окно предложения посмотреть рекламу за +1 топливо
            if (noFuelOfferPopup == null)
            {
                Transform existingPopup = transform.Find("Fuel_Ad_Offer_Popup");
                if (existingPopup == null)
                {
                    existingPopup = CreateFuelAdOfferPopup(transform, sharedFont);
                }

                BindPopupReferences(existingPopup);
            }
        }

        private Transform FindTopHeaderTransform()
        {
            Transform levelsPanel = transform.Find("Levels_Panel");
            if (levelsPanel != null)
            {
                Transform top = levelsPanel.Find("Top");
                if (top != null)
                    return top;
            }

            Transform directTop = transform.Find("Top");
            if (directTop != null)
                return directTop;

            return null;
        }

        private Font ResolveSharedFont(LevelSelect levelSelect)
        {
            if (levelSelect != null && levelSelect.TotalScores != null && levelSelect.TotalScores.font != null)
                return levelSelect.TotalScores.font;

            Text anyText = GetComponentInChildren<Text>(true);
            if (anyText != null && anyText.font != null)
                return anyText.font;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Transform CreateFuelWidget(Transform parent, Font font)
        {
            GameObject widgetObj = new GameObject("Fuel_Widget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform widgetRect = widgetObj.GetComponent<RectTransform>();
            widgetRect.SetParent(parent, false);

            // Размещаем слева от плашки Coins в верхней панели Top
            widgetRect.anchorMin = new Vector2(1f, 0.5f);
            widgetRect.anchorMax = new Vector2(1f, 0.5f);
            widgetRect.pivot = new Vector2(0.5f, 0.5f);
            widgetRect.anchoredPosition = new Vector2(-675f, 0f);
            widgetRect.sizeDelta = new Vector2(290f, 88f);

            Image bgImage = widgetObj.GetComponent<Image>();
            bgImage.color = new Color(0.06f, 0.09f, 0.14f, 0.88f);

            Outline bgOutline = widgetObj.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0f, 0.83f, 1f, 0.7f);
            bgOutline.effectDistance = new Vector2(2f, -2f);

            // Иконка/бейдж FUEL слева
            GameObject badgeObj = new GameObject("Fuel_Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.SetParent(widgetRect, false);
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(10f, 0f);
            badgeRect.sizeDelta = new Vector2(72f, 64f);

            Image badgeImg = badgeObj.GetComponent<Image>();
            badgeImg.color = new Color(1f, 0.6f, 0.08f, 0.95f);

            GameObject badgeLabelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform badgeLabelRect = badgeLabelObj.GetComponent<RectTransform>();
            badgeLabelRect.SetParent(badgeRect, false);
            badgeLabelRect.anchorMin = Vector2.zero;
            badgeLabelRect.anchorMax = Vector2.one;
            badgeLabelRect.offsetMin = Vector2.zero;
            badgeLabelRect.offsetMax = Vector2.zero;

            Text badgeText = badgeLabelObj.GetComponent<Text>();
            badgeText.font = font;
            badgeText.text = "FUEL";
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = Color.black;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.resizeTextForBestFit = true;
            badgeText.resizeTextMinSize = 10;
            badgeText.resizeTextMaxSize = 24;

            // Текст количества топлива: "10/10"
            GameObject countObj = new GameObject("Fuel_Count_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform countRect = countObj.GetComponent<RectTransform>();
            countRect.SetParent(widgetRect, false);
            countRect.anchorMin = new Vector2(0f, 0.42f);
            countRect.anchorMax = new Vector2(1f, 0.96f);
            countRect.offsetMin = new Vector2(90f, 0f);
            countRect.offsetMax = new Vector2(-58f, 0f);

            Text countText = countObj.GetComponent<Text>();
            countText.font = font;
            countText.text = "10/10";
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = Color.white;
            countText.fontStyle = FontStyle.Bold;
            countText.resizeTextForBestFit = true;
            countText.resizeTextMinSize = 14;
            countText.resizeTextMaxSize = 36;

            // Контейнер и текст таймера: "09:59" / "FULL"
            GameObject timerObj = new GameObject("Fuel_Timer_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.SetParent(widgetRect, false);
            timerRect.anchorMin = new Vector2(0f, 0.04f);
            timerRect.anchorMax = new Vector2(1f, 0.44f);
            timerRect.offsetMin = new Vector2(90f, 0f);
            timerRect.offsetMax = new Vector2(-58f, 0f);

            Text tmrText = timerObj.GetComponent<Text>();
            tmrText.font = font;
            tmrText.text = "FULL";
            tmrText.alignment = TextAnchor.MiddleCenter;
            tmrText.color = new Color(0.55f, 0.92f, 1f, 1f);
            tmrText.resizeTextForBestFit = true;
            tmrText.resizeTextMinSize = 10;
            tmrText.resizeTextMaxSize = 22;

            // Кнопка "+" для вызова окна пополнения за рекламу
            GameObject plusBtnObj = new GameObject("Add_Fuel_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform plusRect = plusBtnObj.GetComponent<RectTransform>();
            plusRect.SetParent(widgetRect, false);
            plusRect.anchorMin = new Vector2(1f, 0.5f);
            plusRect.anchorMax = new Vector2(1f, 0.5f);
            plusRect.pivot = new Vector2(1f, 0.5f);
            plusRect.anchoredPosition = new Vector2(-8f, 0f);
            plusRect.sizeDelta = new Vector2(44f, 64f);

            Image plusImg = plusBtnObj.GetComponent<Image>();
            plusImg.color = new Color(0.12f, 0.78f, 0.35f, 1f);

            Button plusBtn = plusBtnObj.GetComponent<Button>();
            plusBtn.targetGraphic = plusImg;

            GameObject plusTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform plusTxtRect = plusTxtObj.GetComponent<RectTransform>();
            plusTxtRect.SetParent(plusRect, false);
            plusTxtRect.anchorMin = Vector2.zero;
            plusTxtRect.anchorMax = Vector2.one;
            plusTxtRect.offsetMin = Vector2.zero;
            plusTxtRect.offsetMax = Vector2.zero;

            Text plusTxt = plusTxtObj.GetComponent<Text>();
            plusTxt.font = font;
            plusTxt.text = "+";
            plusTxt.alignment = TextAnchor.MiddleCenter;
            plusTxt.color = Color.white;
            plusTxt.fontStyle = FontStyle.Bold;
            plusTxt.resizeTextForBestFit = true;
            plusTxt.resizeTextMinSize = 16;
            plusTxt.resizeTextMaxSize = 36;

            return widgetRect;
        }

        private void BindWidgetReferences(Transform widgetRoot)
        {
            if (widgetRoot == null)
                return;

            Transform countTr = widgetRoot.Find("Fuel_Count_Text");
            if (countTr != null)
            {
                fuelCountText = countTr.GetComponent<Text>();
                fuelCountTmpText = countTr.GetComponent<TMP_Text>();
            }

            Transform timerTr = widgetRoot.Find("Fuel_Timer_Text");
            if (timerTr != null)
            {
                timerContainer = timerTr.gameObject;
                timerText = timerTr.GetComponent<Text>();
                timerTmpText = timerTr.GetComponent<TMP_Text>();
            }

            Transform plusTr = widgetRoot.Find("Add_Fuel_Button");
            if (plusTr != null)
            {
                openOfferButton = plusTr.GetComponent<Button>();
            }
        }

        private Transform CreateFuelAdOfferPopup(Transform parent, Font font)
        {
            // Полноэкранная подложка-затемнение
            GameObject popupRoot = new GameObject("Fuel_Ad_Offer_Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rootRect = popupRoot.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image overlayImg = popupRoot.GetComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.78f);
            overlayImg.raycastTarget = true;

            // Центральное окно
            GameObject dialogObj = new GameObject("Dialog_Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform dialogRect = dialogObj.GetComponent<RectTransform>();
            dialogRect.SetParent(rootRect, false);
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.anchoredPosition = Vector2.zero;
            dialogRect.sizeDelta = new Vector2(620f, 360f);

            Image dialogImg = dialogObj.GetComponent<Image>();
            dialogImg.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

            Outline dialogOutline = dialogObj.AddComponent<Outline>();
            dialogOutline.effectColor = new Color(1f, 0.6f, 0.08f, 0.9f);
            dialogOutline.effectDistance = new Vector2(3f, -3f);

            // Заголовок
            GameObject titleObj = new GameObject("Title_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.SetParent(dialogRect, false);
            titleRect.anchorMin = new Vector2(0.05f, 0.74f);
            titleRect.anchorMax = new Vector2(0.95f, 0.94f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            Text titleTxt = titleObj.GetComponent<Text>();
            titleTxt.font = font;
            titleTxt.text = "NO FUEL";
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(1f, 0.65f, 0.1f, 1f);
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.resizeTextForBestFit = true;
            titleTxt.resizeTextMinSize = 18;
            titleTxt.resizeTextMaxSize = 42;

            // Статус-сообщение
            GameObject statusObj = new GameObject("Status_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.SetParent(dialogRect, false);
            statusRect.anchorMin = new Vector2(0.06f, 0.44f);
            statusRect.anchorMax = new Vector2(0.94f, 0.72f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            Text statusTxt = statusObj.GetComponent<Text>();
            statusTxt.font = font;
            statusTxt.text = "OUT OF FUEL (0/10)\nWatch an ad to restore +1 Fuel!";
            statusTxt.alignment = TextAnchor.MiddleCenter;
            statusTxt.color = Color.white;
            statusTxt.resizeTextForBestFit = true;
            statusTxt.resizeTextMinSize = 14;
            statusTxt.resizeTextMaxSize = 28;

            // Таймер внутри попапа
            GameObject timerObj = new GameObject("Popup_Timer_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.SetParent(dialogRect, false);
            timerRect.anchorMin = new Vector2(0.06f, 0.30f);
            timerRect.anchorMax = new Vector2(0.94f, 0.43f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;

            Text timerTxt = timerObj.GetComponent<Text>();
            timerTxt.font = font;
            timerTxt.text = "Next +1 fuel in: 09:59";
            timerTxt.alignment = TextAnchor.MiddleCenter;
            timerTxt.color = new Color(0.55f, 0.92f, 1f, 1f);
            timerTxt.resizeTextForBestFit = true;
            timerTxt.resizeTextMinSize = 12;
            timerTxt.resizeTextMaxSize = 24;

            // Кнопка "WATCH AD (+1 FUEL)"
            GameObject watchBtnObj = new GameObject("Watch_Ad_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform watchBtnRect = watchBtnObj.GetComponent<RectTransform>();
            watchBtnRect.SetParent(dialogRect, false);
            watchBtnRect.anchorMin = new Vector2(0.08f, 0.07f);
            watchBtnRect.anchorMax = new Vector2(0.58f, 0.25f);
            watchBtnRect.offsetMin = Vector2.zero;
            watchBtnRect.offsetMax = Vector2.zero;

            Image watchBtnImg = watchBtnObj.GetComponent<Image>();
            watchBtnImg.color = new Color(0.12f, 0.75f, 0.32f, 1f);

            Button watchBtn = watchBtnObj.GetComponent<Button>();
            watchBtn.targetGraphic = watchBtnImg;

            GameObject watchTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform watchTxtRect = watchTxtObj.GetComponent<RectTransform>();
            watchTxtRect.SetParent(watchBtnRect, false);
            watchTxtRect.anchorMin = Vector2.zero;
            watchTxtRect.anchorMax = Vector2.one;
            watchTxtRect.offsetMin = new Vector2(8f, 4f);
            watchTxtRect.offsetMax = new Vector2(-8f, -4f);

            Text watchTxt = watchTxtObj.GetComponent<Text>();
            watchTxt.font = font;
            watchTxt.text = "WATCH AD (+1 FUEL)";
            watchTxt.alignment = TextAnchor.MiddleCenter;
            watchTxt.color = Color.white;
            watchTxt.fontStyle = FontStyle.Bold;
            watchTxt.resizeTextForBestFit = true;
            watchTxt.resizeTextMinSize = 12;
            watchTxt.resizeTextMaxSize = 26;

            // Кнопка "CLOSE"
            GameObject closeBtnObj = new GameObject("Close_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform closeBtnRect = closeBtnObj.GetComponent<RectTransform>();
            closeBtnRect.SetParent(dialogRect, false);
            closeBtnRect.anchorMin = new Vector2(0.62f, 0.07f);
            closeBtnRect.anchorMax = new Vector2(0.92f, 0.25f);
            closeBtnRect.offsetMin = Vector2.zero;
            closeBtnRect.offsetMax = Vector2.zero;

            Image closeBtnImg = closeBtnObj.GetComponent<Image>();
            closeBtnImg.color = new Color(0.75f, 0.2f, 0.2f, 1f);

            Button closeBtn = closeBtnObj.GetComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;

            GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
            closeTxtRect.SetParent(closeBtnRect, false);
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = new Vector2(8f, 4f);
            closeTxtRect.offsetMax = new Vector2(-8f, -4f);

            Text closeTxt = closeTxtObj.GetComponent<Text>();
            closeTxt.font = font;
            closeTxt.text = "CLOSE";
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.resizeTextForBestFit = true;
            closeTxt.resizeTextMinSize = 12;
            closeTxt.resizeTextMaxSize = 26;

            popupRoot.SetActive(false);
            return rootRect;
        }

        private void BindPopupReferences(Transform popupRoot)
        {
            if (popupRoot == null)
                return;

            noFuelOfferPopup = popupRoot.gameObject;

            Transform dialog = popupRoot.Find("Dialog_Box");
            Transform searchRoot = dialog != null ? dialog : popupRoot;

            Transform statusTr = searchRoot.Find("Status_Text");
            if (statusTr != null)
            {
                popupStatusText = statusTr.GetComponent<Text>();
                popupStatusTmpText = statusTr.GetComponent<TMP_Text>();
            }

            Transform timerTr = searchRoot.Find("Popup_Timer_Text");
            if (timerTr != null)
            {
                popupTimerText = timerTr.GetComponent<Text>();
                popupTimerTmpText = timerTr.GetComponent<TMP_Text>();
            }

            Transform watchTr = searchRoot.Find("Watch_Ad_Button");
            if (watchTr != null)
            {
                watchAdButton = watchTr.GetComponent<Button>();
            }

            Transform closeTr = searchRoot.Find("Close_Button");
            if (closeTr != null)
            {
                closeOfferButton = closeTr.GetComponent<Button>();
            }
        }
    }
}
