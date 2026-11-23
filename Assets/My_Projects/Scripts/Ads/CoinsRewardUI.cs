using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NitroStreets.Ads
{
    /// <summary>
    /// UI-компонент окна предложения просмотра рекламы за получение бесплатных монет/денег (+500 $).
    /// Открывается при клике на кнопку добавления денег ('Add_Money_Button' / '+') в любом меню Гаража.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoinsRewardUI : MonoBehaviour
    {
        public static CoinsRewardUI Instance { get; private set; }

        [Header("Настройки награды")]
        [Tooltip("Количество монет за просмотр одного рекламного ролика")]
        [SerializeField] private int coinsRewardAmount = 500;

        [Header("Окно предложения рекламы")]
        [SerializeField] private GameObject coinsOfferPopup;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private Button closeOfferButton;
        [SerializeField] private Text popupTitleText;
        [SerializeField] private Text popupStatusText;
        [SerializeField] private TMP_Text popupStatusTmpText;

        [Header("Опциональная привязка кнопки открытия (+)")]
        [SerializeField] private Button openOfferButton;

        private bool isExplicitlyOpening;

        public int CoinsRewardAmount
        {
            get => coinsRewardAmount;
            set
            {
                coinsRewardAmount = value;
                RefreshUI();
            }
        }

        public GameObject CoinsOfferPopup => coinsOfferPopup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedBootstrap()
        {
            CoinsRewardUI ui = FindFirstObjectByType<CoinsRewardUI>(FindObjectsInactive.Include);
            if (ui != null)
            {
                Instance = ui;
                ui.AutoBindAllAddMoneyButtonsOnScene();
            }
        }

        private void Awake()
        {
            Instance = this;

            if (coinsOfferPopup == null)
            {
                coinsOfferPopup = gameObject;
            }

            BindButtons();
            AutoBindAllAddMoneyButtonsOnScene();

            // Закрываем окно при старте ТОЛЬКО если оно не открывается прямо сейчас через ShowOfferPopup()
            if (coinsOfferPopup != null && !isExplicitlyOpening)
            {
                coinsOfferPopup.SetActive(false);
            }
        }

        private void Start()
        {
            AutoBindAllAddMoneyButtonsOnScene();
        }

        private void OnEnable()
        {
            Instance = this;

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRewardedAdLoadedEvent += UpdateWatchAdButtonState;
            }

            RefreshUI();
        }

        private void OnDisable()
        {
            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRewardedAdLoadedEvent -= UpdateWatchAdButtonState;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Автоматически находит все кнопки добавления денег во всех меню на сцене и гарантирует их работу по клику.
        /// </summary>
        public void AutoBindAllAddMoneyButtonsOnScene()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();

            if (canvas == null)
                return;

            Button[] allButtons = canvas.GetComponentsInChildren<Button>(true);
            foreach (Button btn in allButtons)
            {
                if (btn == null || btn == watchAdButton || btn == closeOfferButton)
                    continue;

                string btnName = btn.name.ToLowerInvariant();
                Transform p = btn.transform.parent;
                string parentName = p != null ? p.name.ToLowerInvariant() : "";

                bool isMoneyAddButton =
                    btnName.Contains("add_money") ||
                    btnName.Contains("addmoney") ||
                    btnName.Contains("add_coins") ||
                    btnName.Contains("addcoins") ||
                    (parentName.Contains("money") && (btnName.Contains("add") || btnName.Contains("plus"))) ||
                    (parentName.Contains("coins") && (btnName.Contains("add") || btnName.Contains("plus")));

                if (isMoneyAddButton)
                {
                    btn.onClick.RemoveListener(ShowOfferPopup);
                    btn.onClick.AddListener(ShowOfferPopup);
                }
            }
        }

        /// <summary>
        /// Глобальный статический метод для удобного открытия окна из любого скрипта или UI-кнопки.
        /// </summary>
        public static void OpenPopup()
        {
            CoinsRewardUI ui = Instance;
            if (ui == null)
            {
                ui = FindFirstObjectByType<CoinsRewardUI>(FindObjectsInactive.Include);
            }

            if (ui != null)
            {
                ui.ShowOfferPopup();
            }
            else
            {
                Debug.LogWarning("[CoinsRewardUI] CoinsRewardUI не найден на сцене!");
            }
        }

        public void BindButtons()
        {
            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            if (closeOfferButton != null)
            {
                closeOfferButton.onClick.RemoveListener(HideOfferPopup);
                closeOfferButton.onClick.AddListener(HideOfferPopup);
            }

            if (openOfferButton != null)
            {
                openOfferButton.onClick.RemoveListener(ShowOfferPopup);
                openOfferButton.onClick.AddListener(ShowOfferPopup);
            }
        }

        /// <summary>
        /// Открывает окно предложения посмотреть рекламу за монеты.
        /// </summary>
        public void ShowOfferPopup()
        {
            if (coinsOfferPopup == null)
            {
                coinsOfferPopup = gameObject;
            }

            isExplicitlyOpening = true;
            try
            {
                coinsOfferPopup.transform.SetAsLastSibling();
                coinsOfferPopup.SetActive(true);
            }
            finally
            {
                isExplicitlyOpening = false;
            }

            RefreshUI();
            UpdateWatchAdButtonState();
            Debug.Log("[CoinsRewardUI] Окно предложения рекламы монет успешно открыто!");
        }

        /// <summary>
        /// Закрывает окно предложения.
        /// </summary>
        public void HideOfferPopup()
        {
            if (coinsOfferPopup != null)
            {
                coinsOfferPopup.SetActive(false);
            }
        }

        private void OnWatchAdClicked()
        {
            int reward = coinsRewardAmount > 0 ? coinsRewardAmount : 500;

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.ShowRewardedAd(
                    onRewardEarned: () =>
                    {
                        AdMobManager.Instance.AddCoinsAndRefreshUI(reward);
                        HideOfferPopup();
                        RefreshUI();
                    },
                    onAdClosed: UpdateWatchAdButtonState);
            }
            else
            {
                Debug.LogWarning("[CoinsRewardUI] AdMobManager не найден, начисляем монеты в режиме симуляции.");
                int currentScores = PlayerPrefs.GetInt("TotalScores", 0);
                PlayerPrefs.SetInt("TotalScores", currentScores + reward);
                PlayerPrefs.Save();
                AdMobManager.RefreshAllScoreTextsOnScene(currentScores + reward);
                HideOfferPopup();
                RefreshUI();
            }
        }

        private void UpdateWatchAdButtonState()
        {
            if (watchAdButton == null)
                return;

            bool adReady = AdMobManager.Instance == null || AdMobManager.Instance.IsRewardedAdReady();
            watchAdButton.interactable = adReady;
        }

        public void RefreshUI()
        {
            string msg = $"Watch a short video ad\nto receive +{coinsRewardAmount} Coins!";

            if (popupStatusText != null)
                popupStatusText.text = msg;

            if (popupStatusTmpText != null)
                popupStatusTmpText.text = msg;

            UpdateWatchAdButtonState();
        }

        public void SetReferences(GameObject popup, Button watchBtn, Button closeBtn, Text titleTxt, Text statusTxt)
        {
            coinsOfferPopup = popup;
            watchAdButton = watchBtn;
            closeOfferButton = closeBtn;
            popupTitleText = titleTxt;
            popupStatusText = statusTxt;
            BindButtons();
        }
    }
}
