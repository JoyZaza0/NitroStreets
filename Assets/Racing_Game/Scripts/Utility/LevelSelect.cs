//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ALIyerEdon;

namespace ALIyerEdon
{
	public class LevelSelect : MonoBehaviour
	{

		[Header("UI Elements ____________________________________________________" +
			"____________________________________________________")]
		[Space(5)]
		// Enable car select menu
		public GameObject backgroundUI;
		public GameObject carSelectUI;
		public GameObject purchaseUI;
		public GameObject WatchAdsOffer;
		public GameObject loading;
		public GameObject modeSelectUI;

		// Display total scores
		public Text TotalScores;
		public Dropdown difficultyLevel;

		[Header("Levels Info ____________________________________________________" +
			"____________________________________________________")]
		[Space(5)]
		// Each level value
		public int[] levelPrices;

		//TotalScore text, level value text
		public Text[] levelPriceText;

		public GameObject[] levelLocks;

		[Header("Awards ____________________________________________________" +
			"____________________________________________________")]
		[Space(5)]
		public Sprite goldAward;
		public Sprite bronzeAward;
		public Sprite silverAward;
		public Sprite noneAward;

		[Space(5)]
		public Image[] awardIcons;

		// MainLevel name
		public string[] levelNames;

		[Header("Fuel System ____________________________________________________" +
			"____________________________________________________")]
		[Space(5)]
		public GameMode gameMode = GameMode.Career;
		public bool useFuelSystem = true;
		public NitroStreets.Fuel.ModeFuelUI modeFuelUI;

		void Awake()
		{
			AutoDetectModeFromName();
			EnsureModeStorageInitialized();
			if (useFuelSystem)
			{
				EnsureFuelUI();
			}
		}

		void OnEnable()
		{
			AutoDetectModeFromName();
			EnsureModeStorageInitialized();
			GameModeSaveKeys.SetActiveMode(gameMode);

			if (useFuelSystem)
			{
				NitroStreets.Fuel.ModeFuelManager.EnsureInstance().ActiveMode = gameMode;
				EnsureFuelUI();
			}

			RefreshLevelSelectUI();
		}

		private void AutoDetectModeFromName()
		{
			if (gameObject.name.IndexOf("Formula", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
				gameObject.name.IndexOf("F1", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				gameMode = GameMode.Formula1;
			}
			else if (gameObject.name.IndexOf("Offroad", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				gameMode = GameMode.Offroad;
			}
			else if (gameObject.name.IndexOf("Career", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				gameMode = GameMode.Career;
			}
		}

		public void EnsureModeStorageInitialized()
		{
			bool changed = false;
			string firstLevelKey = GameModeSaveKeys.GetLevelUnlockKey(gameMode, 0);
			if (PlayerPrefs.GetInt(firstLevelKey, 0) != 3)
			{
				PlayerPrefs.SetInt(firstLevelKey, 3);
				changed = true;
			}

			int maxLevels = Mathf.Max(
				levelPrices != null ? levelPrices.Length : 0,
				awardIcons != null ? awardIcons.Length : 0,
				levelLocks != null ? levelLocks.Length : 0);

			for (int i = 0; i < maxLevels; i++)
			{
				string awardKey = GameModeSaveKeys.GetLevelAwardKey(gameMode, i);
				if (!PlayerPrefs.HasKey(awardKey))
				{
					PlayerPrefs.SetInt(awardKey, 3);
					changed = true;
				}
			}

			if (changed)
			{
				PlayerPrefs.Save();
			}
		}

		public void EnsureFuelUI()
		{
			if (!useFuelSystem)
				return;

			if (modeFuelUI == null)
			{
				modeFuelUI = GetComponent<NitroStreets.Fuel.ModeFuelUI>();
				if (modeFuelUI == null)
				{
					modeFuelUI = gameObject.AddComponent<NitroStreets.Fuel.ModeFuelUI>();
				}
			}

			modeFuelUI.TargetMode = gameMode;
		}

		void Start()
		{
			RefreshLevelSelectUI();
		}

		public void RefreshLevelSelectUI()
		{
			if (WatchAdsOffer != null)
				WatchAdsOffer.SetActive(false);
			if (purchaseUI != null)
				purchaseUI.SetActive(false);

			// Update total scores display
			if (TotalScores != null)
				TotalScores.text = PlayerPrefs.GetInt("TotalScores").ToString();

			if (difficultyLevel != null)
				difficultyLevel.value = PlayerPrefs.GetInt("Difficulty Level");

			if (levelLocks != null)
			{
				for (int l = 0; l < levelLocks.Length; l++)
				{
					if (levelLocks[l] == null)
						continue;

					// Enable lock and level price displays
					if (PlayerPrefs.GetInt(GameModeSaveKeys.GetLevelUnlockKey(gameMode, l), 0) == 3)
						levelLocks[l].SetActive(false);
					else
						levelLocks[l].SetActive(true);
				}
			}

			// Update current level value text
			if (levelPriceText != null)
			{
				for (int a = 0; a < levelPriceText.Length; a++)
				{
					if (levelPriceText[a] == null)
						continue;

					if (PlayerPrefs.GetInt(GameModeSaveKeys.GetLevelUnlockKey(gameMode, a), 0) == 3)
						levelPriceText[a].text = "";
					else if (levelPrices != null && a < levelPrices.Length)
						levelPriceText[a].text = "Price : " + levelPrices[a].ToString();
				}
			}

			// Update award icons
			if (awardIcons != null)
			{
				for (int aw = 0; aw < awardIcons.Length; aw++)
				{
					if (awardIcons[aw] == null)
						continue;

					int awardState = PlayerPrefs.GetInt(GameModeSaveKeys.GetLevelAwardKey(gameMode, aw), 3);
					if (awardState == 0)
						awardIcons[aw].sprite = goldAward;
					else if (awardState == 1)
						awardIcons[aw].sprite = bronzeAward;
					else if (awardState == 2)
						awardIcons[aw].sprite = silverAward;
					else
						awardIcons[aw].sprite = noneAward;
				}
			}

			LevelUIHelper helper = GetComponentInChildren<LevelUIHelper>(true);
			if (helper != null)
			{
				helper.UpdateLevelCardVisuals();
			}
		}

		// Buy current selected level
		public void BuyLevel()
		{
			// Check player have enough money
			if (levelPrices[currentSelectedLevel] <= PlayerPrefs.GetInt("TotalScores"))
			{
				PlayerPrefs.SetInt(GameModeSaveKeys.GetLevelUnlockKey(gameMode, currentSelectedLevel), 3);

				// Reduce current level price from the total scores
				PlayerPrefs.SetInt("TotalScores",
					PlayerPrefs.GetInt("TotalScores") - levelPrices[currentSelectedLevel]);

				// Disable lock icon for current level
				levelLocks[currentSelectedLevel].SetActive(false);

				// Clear level price text
				levelPriceText[currentSelectedLevel].text = "";

				// Update total scores display
				TotalScores.text = PlayerPrefs.GetInt("TotalScores").ToString();

				purchaseUI.SetActive(false);
				WatchAdsOffer.SetActive(false);

				LevelUIHelper helper = GetComponentInChildren<LevelUIHelper>(true);
				if (helper != null)
				{
					helper.UpdateLevelCardVisuals();
				}
			}
			else
			{
				// Show the shop offer window
				WatchAdsOffer.SetActive(true);
				purchaseUI.SetActive(false);
			}
		}

		int currentSelectedLevel = 0;

		// Select current level
		public void SelectLevel(int ID)
		{
			currentSelectedLevel = ID;

			if (PlayerPrefs.GetInt(GameModeSaveKeys.GetLevelUnlockKey(gameMode, ID), 0) == 3)
			{
				GameModeSaveKeys.SetActiveMode(gameMode);

				if (useFuelSystem)
				{
					var fuelManager = NitroStreets.Fuel.ModeFuelManager.EnsureInstance();
					fuelManager.ActiveMode = gameMode;

					if (!fuelManager.HasFuel(gameMode, 1))
					{
						EnsureFuelUI();
						if (modeFuelUI != null)
						{
							modeFuelUI.ShowNoFuelAdOffer();
						}
						return;
					}
				}

				// Set current selected level ID for instantiate in main level
				PlayerPrefs.SetInt(GameModeSaveKeys.GetSelectedLevelIdKey(gameMode), ID);
				PlayerPrefs.SetInt("LevelID", ID);

				if (carSelectUI != null)
				{
					CarSelect cs = carSelectUI.GetComponent<CarSelect>();
					if (cs != null)
					{
						cs.levelSelect = this;
						cs.gameMode = this.gameMode;
					}
				}

				gameObject.SetActive(false);

				if (backgroundUI != null)
					backgroundUI.SetActive(false);

				if (carSelectUI != null)
					carSelectUI.SetActive(true);

				if (purchaseUI != null)
					purchaseUI.SetActive(false);
			}
			else
			{
				if (purchaseUI != null)
					purchaseUI.SetActive(true);
				if (WatchAdsOffer != null)
					WatchAdsOffer.SetActive(false);
			}
		}

		public void BackToModeSelect()
		{
			if (purchaseUI != null)
				purchaseUI.SetActive(false);
			if (WatchAdsOffer != null)
				WatchAdsOffer.SetActive(false);

			gameObject.SetActive(false);

			if (modeSelectUI == null)
			{
				Canvas rootCanvas = GetComponentInParent<Canvas>(true);
				Transform searchRoot = rootCanvas != null ? rootCanvas.transform : transform.parent;
				if (searchRoot != null)
				{
					Transform modeTr = searchRoot.Find("Mode_Select");
					if (modeTr != null)
						modeSelectUI = modeTr.gameObject;
				}
			}

			if (modeSelectUI != null)
				modeSelectUI.SetActive(true);
		}

		public void SelectLevel_Offroad()
		{
			loading.SetActive(true);
			SceneManager.LoadSceneAsync(levelNames[7]);
		}

		public void Set_Difficulty()
		{
			PlayerPrefs.SetInt("Difficulty Level", difficultyLevel.value);
		}
	}
}
