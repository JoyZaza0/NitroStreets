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
			if (useFuelSystem)
			{
				EnsureFuelUI();
			}
		}

		void OnEnable()
		{
			AutoDetectModeFromName();
			if (useFuelSystem)
			{
				NitroStreets.Fuel.ModeFuelManager.EnsureInstance().ActiveMode = gameMode;
				EnsureFuelUI();
			}
		}

		private void AutoDetectModeFromName()
		{
			if (gameObject.name.IndexOf("Offroad", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				gameMode = GameMode.Offroad;
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
			WatchAdsOffer.SetActive(false);
			purchaseUI.SetActive(false);

			// Update total scores display
			TotalScores.text = PlayerPrefs.GetInt("TotalScores").ToString();

			difficultyLevel.value = PlayerPrefs.GetInt("Difficulty Level");

			for (int l = 0; l < levelPriceText.Length; l++)
			{
				// Enable lock and level price displays
				if (PlayerPrefs.GetInt("Level" + l.ToString()) == 3)
					levelLocks[l].SetActive(false);
				else
					levelLocks[l].SetActive(true);
			}


			// Update current level value text
			for (int a = 0; a < levelPriceText.Length; a++)
			{
				if (PlayerPrefs.GetInt("Level" + a.ToString()) == 3)
					levelPriceText[a].text = "";
				else
					levelPriceText[a].text = "Price : " + levelPrices[a].ToString();
			}

			// Update award icons
			for (int aw = 0; aw < awardIcons.Length; aw++)
			{
				if (PlayerPrefs.GetInt("Award_Level_" + aw.ToString()) == 0)
					awardIcons[aw].sprite = goldAward;

				if (PlayerPrefs.GetInt("Award_Level_" + aw.ToString()) == 1)
					awardIcons[aw].sprite = bronzeAward;

				if (PlayerPrefs.GetInt("Award_Level_" + aw.ToString()) == 2)
					awardIcons[aw].sprite = silverAward;

				if (PlayerPrefs.GetInt("Award_Level_" + aw.ToString()) == 3)
					awardIcons[aw].sprite = noneAward;

			}
		}

		// Buy current selected level
		public void BuyLevel()
		{
			// Check player have enough money
			if (levelPrices[currentSelectedLevel] <= PlayerPrefs.GetInt("TotalScores"))
			{

				PlayerPrefs.SetInt("Level" + currentSelectedLevel.ToString(), 3);

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

			if (PlayerPrefs.GetInt("Level" + ID.ToString()) == 3)
			{
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
				PlayerPrefs.SetInt("LevelID", ID);

				if (carSelectUI != null)
				{
					CarSelect cs = carSelectUI.GetComponent<CarSelect>();
					if (cs != null)
					{
						cs.levelSelect = this;
					}
				}

				gameObject.SetActive(false);

				backgroundUI.SetActive(false);

				carSelectUI.SetActive(true);

				purchaseUI.SetActive(false);
			}
			else
			{
				purchaseUI.SetActive(true);
				WatchAdsOffer.SetActive(false);
			}
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