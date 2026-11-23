//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ALIyerEdon
{
	public class CarSelect : MonoBehaviour
	{
		// SpawnPoint
		public Transform spawnPoint;

		// Cars prefabs array
		public GameObject[] cars;

		// Each Car value
		public int[] carPrices;

		// Lock Icon,Shop window,Buy button
		public GameObject lockIcon, buyButton, Price, noEnoughScores;
		public GameObject purchaseUI;
		public GameObject colorPicker;

		// Display total scores
		public Text TotalScores;

		// Selected car ID
		[HideInInspector] public int ID;

		//TotalScore text, car value text
		public Text carPriceText;

		public LevelSelect levelSelect;

		// SetActive(true) loading window before start loading level
		public GameObject Loading;

		[Header("Mode & Customization Settings")]
		public GameMode gameMode = GameMode.Career;
		public bool allowCustomization = true;

		void Awake()
		{
			AutoDetectMode();
			EnsureModeCarStorageInitialized();
		}

		void OnEnable()
		{
			AutoDetectMode();
			EnsureModeCarStorageInitialized();
			RefreshSelectedCarAndSpawn();
		}

		void Start()
		{
			AutoDetectMode();
			EnsureModeCarStorageInitialized();
			RefreshSelectedCarAndSpawn();
		}

		private void AutoDetectMode()
		{
			if (levelSelect != null)
			{
				gameMode = levelSelect.gameMode;
				return;
			}

			if (gameObject.name.IndexOf("Formula", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
				gameObject.name.IndexOf("F1", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				gameMode = GameMode.Formula1;
				allowCustomization = false;
			}
			else if (gameObject.name.IndexOf("Offroad", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				gameMode = GameMode.Offroad;
			}
			else
			{
				gameMode = GameMode.Career;
			}
		}

		public void EnsureModeCarStorageInitialized()
		{
			string firstCarKey = GameModeSaveKeys.GetCarUnlockKey(gameMode, 0);
			if (PlayerPrefs.GetInt(firstCarKey, 0) != 3)
			{
				PlayerPrefs.SetInt(firstCarKey, 3);
				PlayerPrefs.Save();
			}
		}

		public void RefreshSelectedCarAndSpawn()
		{
			if (cars == null || cars.Length == 0)
			{
				Debug.LogWarning($"[CarSelect] Car array on {gameObject.name} is empty.", this);
				return;
			}

			string selectedCarKey = GameModeSaveKeys.GetSelectedCarIdKey(gameMode);
			ID = Mathf.Clamp(PlayerPrefs.GetInt(selectedCarKey, 0), 0, cars.Length - 1);
			PlayerPrefs.SetInt(selectedCarKey, ID);

			if (TotalScores != null)
				TotalScores.text = PlayerPrefs.GetInt("TotalScores").ToString();

			SpawnCurrentCar();
			UpdateCarLockAndPriceUI();

			if (gameObject.activeInHierarchy)
				StartCoroutine(ColorPanel_Check());
		}

		private void SpawnCurrentCar()
		{
			if (cars == null || ID < 0 || ID >= cars.Length || cars[ID] == null || spawnPoint == null)
				return;

			DestroyExistingPlayerCars();
			Instantiate(cars[ID], spawnPoint.position, spawnPoint.rotation);
		}

		public static void DestroyExistingPlayerCars()
		{
			GameObject[] existingPlayers = GameObject.FindGameObjectsWithTag("Player");
			if (existingPlayers == null)
				return;

			for (int i = 0; i < existingPlayers.Length; i++)
			{
				if (existingPlayers[i] != null)
				{
					existingPlayers[i].tag = "Untagged";
					existingPlayers[i].SetActive(false);
					Destroy(existingPlayers[i]);
				}
			}
		}

		private void UpdateCarLockAndPriceUI()
		{
			bool isUnlocked = PlayerPrefs.GetInt(GameModeSaveKeys.GetCarUnlockKey(gameMode, ID), 0) == 3;

			if (lockIcon != null)
				lockIcon.SetActive(!isUnlocked);
			if (buyButton != null)
				buyButton.SetActive(!isUnlocked);
			if (Price != null)
				Price.SetActive(!isUnlocked);

			if (carPriceText != null && carPrices != null && ID >= 0 && ID < carPrices.Length)
				carPriceText.text = carPrices[ID].ToString() + " $";
		}

		bool nextClick, prevClick;

		// Public function for NextCar select button in menu
		public void NextCar()
		{
			if (!prevClick && cars != null && cars.Length > 0)
			{
				nextClick = true;

				if (ID < cars.Length - 1)
					ID++;

				string selectedCarKey = GameModeSaveKeys.GetSelectedCarIdKey(gameMode);
				PlayerPrefs.SetInt(selectedCarKey, ID);

				SpawnCurrentCar();
				UpdateCarLockAndPriceUI();

				if (gameObject.activeInHierarchy)
					StartCoroutine(ColorPanel_Check());

				nextClick = false;
			}
		}

		// Public function for PrevCar select button in menu
		public void PrevCar()
		{
			if (!nextClick && cars != null && cars.Length > 0)
			{
				prevClick = true;

				if (ID > 0)
					ID--;

				string selectedCarKey = GameModeSaveKeys.GetSelectedCarIdKey(gameMode);
				PlayerPrefs.SetInt(selectedCarKey, ID);

				SpawnCurrentCar();
				UpdateCarLockAndPriceUI();

				if (gameObject.activeInHierarchy)
					StartCoroutine(ColorPanel_Check());

				prevClick = false;
			}
		}

		public void Buy_CurrentCar()
		{
			if (purchaseUI != null)
				purchaseUI.SetActive(true);
			if (noEnoughScores != null)
				noEnoughScores.SetActive(false);
		}

		// Buy current selected car
		public void BuyCar()
		{
			// Check player have enough money
			if (carPrices[ID] <= PlayerPrefs.GetInt("TotalScores"))
			{
				PlayerPrefs.SetInt(GameModeSaveKeys.GetCarUnlockKey(gameMode, ID), 3);

				// Reduce current car price from the total scores
				PlayerPrefs.SetInt("TotalScores",
					PlayerPrefs.GetInt("TotalScores") - carPrices[ID]);

				UpdateCarLockAndPriceUI();

				if (purchaseUI != null)
					purchaseUI.SetActive(false);

				// Update total scores display
				if (TotalScores != null)
					TotalScores.text = PlayerPrefs.GetInt("TotalScores").ToString();
			}
			else
			{
				// Show the shop offer window
				if (noEnoughScores != null)
					noEnoughScores.SetActive(true);
				if (purchaseUI != null)
					purchaseUI.SetActive(false);
			}
		}

		// Select current car
		public void SelectCar()
		{
			if (PlayerPrefs.GetInt(GameModeSaveKeys.GetCarUnlockKey(gameMode, ID), 0) == 3)
			{
				GameMode targetMode = levelSelect != null ? levelSelect.gameMode : gameMode;
				GameModeSaveKeys.SetActiveMode(targetMode);

				if (levelSelect == null || levelSelect.useFuelSystem)
				{
					var fuelManager = NitroStreets.Fuel.ModeFuelManager.EnsureInstance();

					if (!fuelManager.TryConsumeFuel(targetMode, 1))
					{
						if (levelSelect != null)
						{
							gameObject.SetActive(false);
							levelSelect.gameObject.SetActive(true);
							if (levelSelect.backgroundUI != null)
								levelSelect.backgroundUI.SetActive(true);

							levelSelect.EnsureFuelUI();
							if (levelSelect.modeFuelUI != null)
								levelSelect.modeFuelUI.ShowNoFuelAdOffer();
						}
						return;
					}
				}

				// Set current selected car ID for instantiate in main level
				PlayerPrefs.SetInt(GameModeSaveKeys.GetSelectedCarIdKey(targetMode), ID);
				PlayerPrefs.SetInt("CarID", ID);

				if (Loading != null)
					Loading.SetActive(true);

				var cursorSim = FindFirstObjectByType<ControllerMouseSimulator>();
				if (cursorSim != null)
					cursorSim.Update_CursorIcon(false);

				int selectedLevelId = PlayerPrefs.GetInt(
					GameModeSaveKeys.GetSelectedLevelIdKey(targetMode),
					PlayerPrefs.GetInt("LevelID", 0));

				if (levelSelect != null && levelSelect.levelNames != null &&
					selectedLevelId >= 0 && selectedLevelId < levelSelect.levelNames.Length)
				{
					SceneManager.LoadSceneAsync(levelSelect.levelNames[selectedLevelId]);
				}
				else
				{
					Debug.LogError($"[CarSelect] Invalid level index {selectedLevelId} or unassigned levelNames on LevelSelect.", this);
				}
			}
			else
			{
				if (purchaseUI != null)
					purchaseUI.SetActive(true);
				if (noEnoughScores != null)
					noEnoughScores.SetActive(false);
			}
		}

		public void BackToLevelSelect()
		{
			if (purchaseUI != null)
				purchaseUI.SetActive(false);
			if (noEnoughScores != null)
				noEnoughScores.SetActive(false);

			gameObject.SetActive(false);

			if (levelSelect != null)
			{
				levelSelect.gameObject.SetActive(true);
				if (levelSelect.backgroundUI != null)
					levelSelect.backgroundUI.SetActive(true);
			}
		}

		IEnumerator ColorPanel_Check()
		{
			yield return new WaitForEndOfFrame();

			if (colorPicker == null)
				yield break;

			if (!allowCustomization)
			{
				colorPicker.SetActive(false);
				yield break;
			}

			if (FindObjectOfType<Car_Color>())
				colorPicker.SetActive(true);
			else
				colorPicker.SetActive(false);
		}
	}
}
