using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ALIyerEdon;

#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
#endif

namespace NitroStreets.Ads
{
    /// <summary>
    /// Центральный менеджер рекламы Google AdMob (Banner, Interstitial, Rewarded).
    /// Автоматически создаётся при запуске игры (если его ещё нет на сцене) и сохраняется между сценами.
    /// По умолчанию использует официальные тестовые ID Google AdMob.
    /// </summary>
    public class AdMobManager : MonoBehaviour
    {
        public static AdMobManager Instance { get; private set; }

        [Header("Общие настройки")]
        [Tooltip("Автоматически создавать AdMobManager при старте игры, если его нет на сцене")]
        public static bool AutoBootstrap = true;

        [Tooltip("Использовать официальные тестовые ID от Google. Выключите перед публикацией в Google Play!")]
        [SerializeField] private bool useTestAds = true;

        [Tooltip("Включить симуляцию показа рекламы в Редакторе Unity, если плагин Google Mobile Ads ещё не импортирован")]
        [SerializeField] private bool simulateInEditorWithoutSdk = true;

        [Header("Android Ad Unit IDs (Боевые ID из кабинета AdMob)")]
        [SerializeField] private string androidBannerId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";
        [SerializeField] private string androidInterstitialId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";
        [SerializeField] private string androidRewardedId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";

        [Header("iOS Ad Unit IDs (Боевые ID из кабинета AdMob)")]
        [SerializeField] private string iosBannerId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";
        [SerializeField] private string iosInterstitialId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";
        [SerializeField] private string iosRewardedId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";

        [Header("Настройки Баннера (Banner)")]
        [Tooltip("Показывать ли баннер автоматически в меню/гараже")]
        [SerializeField] private bool autoShowBannerInGarage = true;
        [Tooltip("Скрывать ли баннер во время активного заезда")]
        [SerializeField] private bool hideBannerDuringRace = true;
        [SerializeField] private string garageSceneName = "Garage";
        [Tooltip("Позиция баннера: true = снизу (Bottom), false = сверху (Top)")]
        [SerializeField] private bool bannerAtBottom = true;

        [Header("Настройки Межстраничной рекламы (Interstitial)")]
        [Tooltip("Показывать межстраничную рекламу каждые N завершённых заездов (1 = после каждого заезда)")]
        [Min(1)]
        [SerializeField] private int racesBetweenInterstitials = 1;
        [Tooltip("Минимальный интервал (в секундах) между показами межстраничной рекламы")]
        [SerializeField] private float interstitialCooldownSeconds = 45f;

        [Header("Настройки Вознаграждаемой рекламы (Rewarded)")]
        [Tooltip("Количество монет по умолчанию за просмотр рекламы в гараже")]
        [SerializeField] private int defaultGarageRewardCoins = 500;

        // Официальные тестовые Ad Unit ID от Google
        private const string TestAndroidBannerId = "ca-app-pub-3940256099942544/6300978111";
        private const string TestAndroidInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        private const string TestAndroidRewardedId = "ca-app-pub-3940256099942544/5224354917";

        private const string TestIosBannerId = "ca-app-pub-3940256099942544/2934735716";
        private const string TestIosInterstitialId = "ca-app-pub-3940256099942544/4411468910";
        private const string TestIosRewardedId = "ca-app-pub-3940256099942544/1712485313";

        private bool isInitialized;
        private int completedRacesCounter;
        private float lastInterstitialTime = -999f;

        // Очередь действий для выполнения в главном потоке Unity (AdMob вызывает колбэки из фоновых потоков Android/iOS)
        private readonly Queue<Action> mainThreadActions = new Queue<Action>();
        private readonly object queueLock = new object();

        public event Action OnRewardedAdLoadedEvent;
        public event Action<int> OnCoinsRewardedEvent;

#if GOOGLE_MOBILE_ADS
        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!AutoBootstrap || Instance != null)
                return;

            AdMobManager existing = FindFirstObjectByType<AdMobManager>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            // Попробуем загрузить префаб из Resources, если пользователь его создал
            AdMobManager prefab = Resources.Load<AdMobManager>("AdMobManager");
            if (prefab != null)
            {
                Instantiate(prefab);
            }
            else
            {
                GameObject go = new GameObject("[AdMobManager]");
                go.AddComponent<AdMobManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            InitializeAdMob();
        }

        private void Update()
        {
            lock (queueLock)
            {
                while (mainThreadActions.Count > 0)
                {
                    Action action = mainThreadActions.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AdMobManager] Ошибка в колбэке главного потока: {ex}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                DestroyBanner();
                DestroyInterstitial();
                DestroyRewarded();
            }
        }

        private void EnqueueMainThread(Action action)
        {
            if (action == null)
                return;

            lock (queueLock)
            {
                mainThreadActions.Enqueue(action);
            }
        }

        /// <summary>
        /// Инициализация SDK Google Mobile Ads и предзагрузка объявлений.
        /// </summary>
        public void InitializeAdMob()
        {
            if (isInitialized)
                return;

#if GOOGLE_MOBILE_ADS
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(initStatus =>
            {
                EnqueueMainThread(() =>
                {
                    isInitialized = true;
                    Debug.Log("[AdMobManager] Google Mobile Ads SDK успешно инициализирован.");

                    LoadInterstitialAd();
                    LoadRewardedAd();
                    HandleSceneBanner(SceneManager.GetActiveScene().name);
                });
            });
#else
            isInitialized = true;
            Debug.Log("[AdMobManager] Плагин Google Mobile Ads ещё не импортирован. Работает режим совместимости/симуляции.");
            HandleSceneBanner(SceneManager.GetActiveScene().name);
            OnRewardedAdLoadedEvent?.Invoke();
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSceneBanner(scene.name);
        }

        private void HandleSceneBanner(string sceneName)
        {
            if (!isInitialized)
                return;

            bool isGarage = string.Equals(sceneName, garageSceneName, StringComparison.OrdinalIgnoreCase);

            if (isGarage && autoShowBannerInGarage)
            {
                ShowBanner();
            }
            else if (!isGarage && hideBannerDuringRace)
            {
                HideBanner();
            }
        }

        #region ID Helpers

        private string GetBannerAdUnitId()
        {
#if UNITY_ANDROID
            return useTestAds ? TestAndroidBannerId : androidBannerId;
#elif UNITY_IPHONE || UNITY_IOS
            return useTestAds ? TestIosBannerId : iosBannerId;
#else
            return TestAndroidBannerId;
#endif
        }

        private string GetInterstitialAdUnitId()
        {
#if UNITY_ANDROID
            return useTestAds ? TestAndroidInterstitialId : androidInterstitialId;
#elif UNITY_IPHONE || UNITY_IOS
            return useTestAds ? TestIosInterstitialId : iosInterstitialId;
#else
            return TestAndroidInterstitialId;
#endif
        }

        private string GetRewardedAdUnitId()
        {
#if UNITY_ANDROID
            return useTestAds ? TestAndroidRewardedId : androidRewardedId;
#elif UNITY_IPHONE || UNITY_IOS
            return useTestAds ? TestIosRewardedId : iosRewardedId;
#else
            return TestAndroidRewardedId;
#endif
        }

        #endregion

        #region Banner Ads (Баннерная реклама)

        /// <summary>
        /// Загрузить и показать адаптивный или стандартный баннер.
        /// </summary>
        public void ShowBanner()
        {
            string adUnitId = GetBannerAdUnitId();
            if (string.IsNullOrWhiteSpace(adUnitId))
                return;

#if GOOGLE_MOBILE_ADS
            if (bannerView == null)
            {
                AdPosition pos = bannerAtBottom ? AdPosition.Bottom : AdPosition.Top;
                bannerView = new BannerView(adUnitId, AdSize.Banner, pos);
                bannerView.OnBannerAdLoaded += () => Debug.Log("[AdMobManager] Баннер загружен.");
                bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
                    Debug.LogWarning($"[AdMobManager] Ошибка загрузки баннера: {error}");

                AdRequest request = new AdRequest();
                bannerView.LoadAd(request);
            }
            else
            {
                bannerView.Show();
            }
#else
            if (simulateInEditorWithoutSdk)
                Debug.Log($"[AdMobManager] (Симуляция) Показ баннера ({(bannerAtBottom ? "Bottom" : "Top")}).");
#endif
        }

        /// <summary>
        /// Скрыть баннер (без удаления из памяти).
        /// </summary>
        public void HideBanner()
        {
#if GOOGLE_MOBILE_ADS
            if (bannerView != null)
            {
                bannerView.Hide();
            }
#endif
        }

        /// <summary>
        /// Полностью уничтожить баннер.
        /// </summary>
        public void DestroyBanner()
        {
#if GOOGLE_MOBILE_ADS
            if (bannerView != null)
            {
                bannerView.Destroy();
                bannerView = null;
            }
#endif
        }

        #endregion

        #region Interstitial Ads (Межстраничная реклама)

        /// <summary>
        /// Предзагрузить межстраничную рекламу.
        /// </summary>
        public void LoadInterstitialAd()
        {
#if GOOGLE_MOBILE_ADS
            DestroyInterstitial();

            string adUnitId = GetInterstitialAdUnitId();
            if (string.IsNullOrWhiteSpace(adUnitId))
                return;

            AdRequest request = new AdRequest();
            InterstitialAd.Load(adUnitId, request, (InterstitialAd ad, LoadAdError error) =>
            {
                EnqueueMainThread(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogWarning($"[AdMobManager] Не удалось загрузить Interstitial: {error}");
                        return;
                    }

                    interstitialAd = ad;
                    Debug.Log("[AdMobManager] Interstitial загружен и готов к показу.");
                });
            });
#endif
        }

        public bool IsInterstitialReady()
        {
#if GOOGLE_MOBILE_ADS
            return interstitialAd != null && interstitialAd.CanShowAd();
#else
            return simulateInEditorWithoutSdk;
#endif
        }

        /// <summary>
        /// Вызывается при завершении гонки. Учитывает счётчик заездов и кулдаун перед показом Interstitial.
        /// </summary>
        public void OnRaceFinished(Action onContinue = null)
        {
            completedRacesCounter++;
            if (completedRacesCounter < racesBetweenInterstitials)
            {
                onContinue?.Invoke();
                return;
            }

            if (Time.unscaledTime - lastInterstitialTime < interstitialCooldownSeconds)
            {
                onContinue?.Invoke();
                return;
            }

            completedRacesCounter = 0;
            ShowInterstitialAd(onContinue);
        }

        /// <summary>
        /// Показать межстраничную рекламу немедленно (если готова).
        /// </summary>
        public void ShowInterstitialAd(Action onAdClosed = null)
        {
            if (Time.unscaledTime - lastInterstitialTime < interstitialCooldownSeconds)
            {
                onAdClosed?.Invoke();
                return;
            }

#if GOOGLE_MOBILE_ADS
            if (interstitialAd != null && interstitialAd.CanShowAd())
            {
                bool callbackInvoked = false;
                void Complete()
                {
                    if (callbackInvoked) return;
                    callbackInvoked = true;
                    EnqueueMainThread(() =>
                    {
                        lastInterstitialTime = Time.unscaledTime;
                        onAdClosed?.Invoke();
                        LoadInterstitialAd();
                    });
                }

                interstitialAd.OnAdFullScreenContentClosed += Complete;
                interstitialAd.OnAdFullScreenContentFailed += (AdError err) =>
                {
                    Debug.LogWarning($"[AdMobManager] Ошибка показа Interstitial: {err}");
                    Complete();
                };

                interstitialAd.Show();
            }
            else
            {
                LoadInterstitialAd();
                onAdClosed?.Invoke();
            }
#else
            if (simulateInEditorWithoutSdk)
            {
                Debug.Log("[AdMobManager] (Симуляция) Показана межстраничная реклама (Interstitial).");
                lastInterstitialTime = Time.unscaledTime;
            }
            onAdClosed?.Invoke();
#endif
        }

        private void DestroyInterstitial()
        {
#if GOOGLE_MOBILE_ADS
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }
#endif
        }

        #endregion

        #region Rewarded Ads (Вознаграждаемая реклама)

        /// <summary>
        /// Предзагрузить вознаграждаемую рекламу.
        /// </summary>
        public void LoadRewardedAd()
        {
#if GOOGLE_MOBILE_ADS
            DestroyRewarded();

            string adUnitId = GetRewardedAdUnitId();
            if (string.IsNullOrWhiteSpace(adUnitId))
                return;

            AdRequest request = new AdRequest();
            RewardedAd.Load(adUnitId, request, (RewardedAd ad, LoadAdError error) =>
            {
                EnqueueMainThread(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogWarning($"[AdMobManager] Не удалось загрузить Rewarded Ad: {error}");
                        return;
                    }

                    rewardedAd = ad;
                    Debug.Log("[AdMobManager] Rewarded Ad загружена и готова к показу.");
                    OnRewardedAdLoadedEvent?.Invoke();
                });
            });
#endif
        }

        /// <summary>
        /// Проверка доступности рекламы с вознаграждением.
        /// </summary>
        public bool IsRewardedAdReady()
        {
#if GOOGLE_MOBILE_ADS
            return rewardedAd != null && rewardedAd.CanShowAd();
#else
            return simulateInEditorWithoutSdk;
#endif
        }

        /// <summary>
        /// Показать рекламу с вознаграждением.
        /// </summary>
        /// <param name="onRewardEarned">Вызывается в главном потоке при успешном досмотре рекламы</param>
        /// <param name="onAdClosed">Вызывается после закрытия рекламного окна</param>
        public void ShowRewardedAd(Action onRewardEarned, Action onAdClosed = null)
        {
#if GOOGLE_MOBILE_ADS
            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                bool closedHandled = false;
                void HandleClose()
                {
                    if (closedHandled) return;
                    closedHandled = true;
                    EnqueueMainThread(() =>
                    {
                        onAdClosed?.Invoke();
                        LoadRewardedAd();
                    });
                }

                rewardedAd.OnAdFullScreenContentClosed += HandleClose;
                rewardedAd.OnAdFullScreenContentFailed += (AdError err) =>
                {
                    Debug.LogWarning($"[AdMobManager] Ошибка показа Rewarded Ad: {err}");
                    HandleClose();
                };

                rewardedAd.Show((Reward reward) =>
                {
                    EnqueueMainThread(() =>
                    {
                        Debug.Log($"[AdMobManager] Награда получена: {reward.Amount} {reward.Type}");
                        onRewardEarned?.Invoke();
                    });
                });
            }
            else
            {
                Debug.LogWarning("[AdMobManager] Rewarded Ad ещё не загружена. Запускаем повторную загрузку.");
                LoadRewardedAd();
                onAdClosed?.Invoke();
            }
#else
            if (simulateInEditorWithoutSdk)
            {
                Debug.Log("[AdMobManager] (Симуляция) Просмотрена вознаграждаемая реклама (Rewarded Ad). Выдаём награду!");
                onRewardEarned?.Invoke();
                onAdClosed?.Invoke();
            }
            else
            {
                Debug.LogWarning("[AdMobManager] Плагин Google Mobile Ads не установлен, симуляция отключена.");
                onAdClosed?.Invoke();
            }
#endif
        }

        /// <summary>
        /// Удобный метод для привязки к UI-кнопке в Инспекторе: показать рекламу и начислить монеты.
        /// </summary>
        public void ShowRewardedForCoins(int coinsAmount)
        {
            int amount = coinsAmount > 0 ? coinsAmount : defaultGarageRewardCoins;
            ShowRewardedAd(() =>
            {
                AddCoinsAndRefreshUI(amount);
            });
        }

        /// <summary>
        /// Удобный метод без параметров (использует defaultGarageRewardCoins).
        /// </summary>
        public void ShowRewardedForDefaultCoins()
        {
            ShowRewardedForCoins(defaultGarageRewardCoins);
        }

        /// <summary>
        /// Начисляет монеты в PlayerPrefs ("TotalScores") и обновляет все тексты баланса на текущей сцене.
        /// </summary>
        public void AddCoinsAndRefreshUI(int amount)
        {
            if (amount <= 0)
                return;

            int newTotal = PlayerPrefs.GetInt("TotalScores") + amount;
            PlayerPrefs.SetInt("TotalScores", newTotal);
            PlayerPrefs.Save();

            RefreshAllScoreTextsOnScene(newTotal);
            OnCoinsRewardedEvent?.Invoke(amount);
            Debug.Log($"[AdMobManager] Начислено +{amount} монет! Новый баланс: {newTotal}");
        }

        public static void RefreshAllScoreTextsOnScene(int newTotal)
        {
            string balanceStr = newTotal.ToString();

            // Обновляем CarSelect
            CarSelect carSelect = FindFirstObjectByType<CarSelect>();
            if (carSelect != null && carSelect.TotalScores != null)
                carSelect.TotalScores.text = balanceStr;

            // Обновляем MainUtility
            MainUtility mainUtility = FindFirstObjectByType<MainUtility>();
            if (mainUtility != null && mainUtility.totalCoinsText != null)
                mainUtility.totalCoinsText.text = balanceStr;

            // Обновляем LevelSelect
            LevelSelect levelSelect = FindFirstObjectByType<LevelSelect>();
            if (levelSelect != null && levelSelect.TotalScores != null)
                levelSelect.TotalScores.text = balanceStr;

            // Обновляем Start_Finish_UI (экран старта/финиша гонки)
            Start_Finish_UI[] startFinishUIs = FindObjectsByType<Start_Finish_UI>(FindObjectsSortMode.None);
            foreach (Start_Finish_UI ui in startFinishUIs)
            {
                if (ui != null && ui.totalScores != null)
                    ui.totalScores.text = "Total Coins : " + balanceStr;
            }
        }

        private void DestroyRewarded()
        {
#if GOOGLE_MOBILE_ADS
            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }
#endif
        }

        #endregion
    }
}
