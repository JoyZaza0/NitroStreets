//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ALIyerEdon;

namespace ALIyerEdon
{
    public class Start_Finish_UI : MonoBehaviour
    {
        // Display race finish menu
        [Header("Menu ____________________________________________________" +
            "____________________________________________________")]
        [Space(5)]
        public GameObject finishRaceMenu;
        public GameObject raceUI;
        public GameObject startButton;
        public UnityEngine.UI.Text totalScores;

        // Award icons 
        [Header("Awards ____________________________________________________" +
            "____________________________________________________")]
        [Space(5)]
        public GameObject goldIcon;
        public GameObject bronzeIcon;
        public GameObject silverIcon;
        public GameObject noAward;

        // Give scores
        [Header("Scores ____________________________________________________" +
            "____________________________________________________")]
        [Space(5)]
        public int goldAward = 3000;
        public int bronzeAward = 2000;
        public int silverAward = 1000;

        [Header("AdMob (Удвоение награды за рекламу)")]
        [Tooltip("Опциональная кнопка 'x2 Награда за рекламу' на экране финиша")]
        public GameObject doubleRewardButton;

        [HideInInspector] public int LastEarnedAward { get; private set; }
        private bool rewardDoubled;

        // Update player's position
        [Header("Positions ____________________________________________________" +
            "____________________________________________________")]
        [Space(5)] public UnityEngine.UI.Text[] positions;
        public UnityEngine.UI.Text[] driversName;
        public Image[] driversFlag;
        public Sprite[] availableFlags;

        public void Update_Driver_Flags(List<string> racerNames, List<int> racerIds)
        {
            if (driversFlag == null || availableFlags == null)
                return;

            for (int i = 0; i < driversFlag.Length && i < racerNames.Count && i < racerIds.Count; i++)
            {
                if (driversFlag[i] == null)
                    continue;

                int flagIndex = Get_Racer_Flag_Index(racerNames[i], racerIds[i]);
                driversFlag[i].sprite =
                    flagIndex >= 0 && flagIndex < availableFlags.Length
                        ? availableFlags[flagIndex]
                        : null;
            }
        }

        private int Get_Racer_Flag_Index(string racerName, int racerId)
        {
            Car_Position[] carPositions = FindObjectsOfType<Car_Position>();

            for (int i = 0; i < carPositions.Length; i++)
            {
                if (carPositions[i] == null ||
                    carPositions[i].RacerID != racerId ||
                    carPositions[i].RacerName != racerName)
                    continue;

                if (carPositions[i].isPlayer)
                    return PlayerProfileStore.FlagIndex;

                return carPositions[i].RacerFlagIndex;
            }

            return -1;
        }

        // Update award menu from "Race_Manager" component at the race finish
        public void Update_Award(int position, int level_ID)
        {
            rewardDoubled = false;
            LastEarnedAward = 0;

            //  Save current level award to display in the main menu (garage scene)
            if (PlayerPrefs.GetInt("Award_Level_" + level_ID.ToString()) > position)
                PlayerPrefs.SetInt("Award_Level_" + level_ID.ToString(), position);

            // Enable disable award icons (gold, bronze, silver)
            if (position == 0)
            {
                goldIcon.SetActive(true);
                bronzeIcon.SetActive(false);
                silverIcon.SetActive(false);
                noAward.SetActive(false);
                LastEarnedAward = goldAward;
                PlayerPrefs.SetInt("TotalScores", PlayerPrefs.GetInt("TotalScores") + goldAward);
            }
            if (position == 1)
            {
                goldIcon.SetActive(false);
                bronzeIcon.SetActive(true);
                silverIcon.SetActive(false);
                noAward.SetActive(false);
                LastEarnedAward = bronzeAward;
                PlayerPrefs.SetInt("TotalScores", PlayerPrefs.GetInt("TotalScores") + bronzeAward);
            }
            if (position == 2)
            {
                goldIcon.SetActive(false);
                bronzeIcon.SetActive(false);
                silverIcon.SetActive(true);
                noAward.SetActive(false);
                LastEarnedAward = silverAward;
                PlayerPrefs.SetInt("TotalScores", PlayerPrefs.GetInt("TotalScores") + silverAward);
            }
            if (position == 3)
            {
                goldIcon.SetActive(false);
                bronzeIcon.SetActive(false);
                silverIcon.SetActive(false);
                noAward.SetActive(true);
                LastEarnedAward = 0;
            }

            if (doubleRewardButton != null)
            {
                doubleRewardButton.SetActive(LastEarnedAward > 0);
            }
        }

        /// <summary>
        /// Метод для привязки к UI-кнопке 'x2 Награда' на экране финиша.
        /// Показывает вознаграждаемую рекламу AdMob и удваивает полученные за гонку монеты.
        /// </summary>
        public void WatchAdToDoubleReward()
        {
            if (rewardDoubled || LastEarnedAward <= 0)
                return;

            if (NitroStreets.Ads.AdMobManager.Instance != null)
            {
                NitroStreets.Ads.AdMobManager.Instance.ShowRewardedAd(GrantDoubleRewardImmediate);
            }
        }

        public void GrantDoubleRewardImmediate()
        {
            if (rewardDoubled || LastEarnedAward <= 0)
                return;

            rewardDoubled = true;
            if (NitroStreets.Ads.AdMobManager.Instance != null)
            {
                NitroStreets.Ads.AdMobManager.Instance.AddCoinsAndRefreshUI(LastEarnedAward);
            }
            else
            {
                int newTotal = PlayerPrefs.GetInt("TotalScores") + LastEarnedAward;
                PlayerPrefs.SetInt("TotalScores", newTotal);
                if (totalScores != null)
                    totalScores.text = "Total Coins : " + newTotal.ToString();
            }

            if (doubleRewardButton != null)
            {
                doubleRewardButton.SetActive(false);
            }
        }
    }


}