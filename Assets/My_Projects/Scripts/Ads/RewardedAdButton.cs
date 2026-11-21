using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ALIyerEdon;

namespace NitroStreets.Ads
{
    /// <summary>
    /// Компонент для любой UI-кнопки просмотра вознаграждаемой рекламы (Rewarded Ad).
    /// Просто повесьте его на кнопку в Гараже или на экране Финиша!
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RewardedAdButton : MonoBehaviour
    {
        public enum RewardActionType
        {
            GiveCoins,
            DoubleRaceReward,
            CustomUnityEvent
        }

        [Header("Настройки награды")]
        [SerializeField] private RewardActionType rewardType = RewardActionType.GiveCoins;

        [Tooltip("Количество монет при выборе GiveCoins")]
        [SerializeField] private int coinsReward = 500;

        [Tooltip("Скрывать или отключать кнопку после однократного получения награды (например, для x2 награды за заезд)")]
        [SerializeField] private bool disableAfterSingleUse = false;

        [Tooltip("Проверять готовность рекламы и делать кнопку неактивной, пока реклама грузится")]
        [SerializeField] private bool autoUpdateInteractable = true;

        [Header("Опциональный текст на кнопке")]
        [SerializeField] private Text optionalRewardLabel;
        [SerializeField] private string labelFormat = "+{0} $ (AD)";

        [Header("Дополнительное событие при получении награды")]
        public UnityEvent onRewardGranted;

        private Button button;
        private bool usedOnce;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnButtonClicked);

            if (optionalRewardLabel != null && rewardType == RewardActionType.GiveCoins)
            {
                optionalRewardLabel.text = string.Format(labelFormat, coinsReward);
            }
        }

        private void OnEnable()
        {
            usedOnce = false;
            UpdateButtonState();

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRewardedAdLoadedEvent += UpdateButtonState;
            }
        }

        private void OnDisable()
        {
            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRewardedAdLoadedEvent -= UpdateButtonState;
            }
        }

        private void UpdateButtonState()
        {
            if (button == null)
                return;

            if (disableAfterSingleUse && usedOnce)
            {
                button.interactable = false;
                return;
            }

            if (autoUpdateInteractable && AdMobManager.Instance != null)
            {
                button.interactable = AdMobManager.Instance.IsRewardedAdReady();
            }
        }

        private void OnButtonClicked()
        {
            if (disableAfterSingleUse && usedOnce)
                return;

            if (AdMobManager.Instance == null)
            {
                Debug.LogWarning("[RewardedAdButton] AdMobManager не найден!");
                return;
            }

            AdMobManager.Instance.ShowRewardedAd(GrantReward, UpdateButtonState);
        }

        private void GrantReward()
        {
            usedOnce = true;

            switch (rewardType)
            {
                case RewardActionType.GiveCoins:
                    AdMobManager.Instance.AddCoinsAndRefreshUI(coinsReward);
                    break;

                case RewardActionType.DoubleRaceReward:
                    Start_Finish_UI finishUI = FindFirstObjectByType<Start_Finish_UI>();
                    if (finishUI != null)
                    {
                        finishUI.GrantDoubleRewardImmediate();
                    }
                    else
                    {
                        AdMobManager.Instance.AddCoinsAndRefreshUI(coinsReward);
                    }
                    break;

                case RewardActionType.CustomUnityEvent:
                    break;
            }

            // Если в гараже открыт GarageCustomizationManager, обновим и его отображение баланса
            GarageCustomizationManager garageMgr = FindFirstObjectByType<GarageCustomizationManager>();
            if (garageMgr != null)
            {
                garageMgr.SendMessage("RefreshBalanceDisplay", SendMessageOptions.DontRequireReceiver);
            }

            onRewardGranted?.Invoke();
            UpdateButtonState();
        }
    }
}
