using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using ALIyerEdon;

public enum GameMode
{
    Career = 0,
    Formula1 = 1,
    Offroad = 2
}

public static class GameModeSaveKeys
{
    public const string ActiveModeKey = "ActiveGameMode";

    public static GameMode GetActiveMode()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && TryDetectModeFromSceneName(activeScene.name, out GameMode sceneMode))
        {
            return sceneMode;
        }

        int raw = PlayerPrefs.GetInt(ActiveModeKey, (int)GameMode.Career);
        if (Enum.IsDefined(typeof(GameMode), raw))
            return (GameMode)raw;
        return GameMode.Career;
    }

    public static void SetActiveMode(GameMode mode)
    {
        PlayerPrefs.SetInt(ActiveModeKey, (int)mode);
        if (NitroStreets.Fuel.ModeFuelManager.Instance != null)
        {
            NitroStreets.Fuel.ModeFuelManager.Instance.ActiveMode = mode;
        }
    }

    public static bool TryDetectModeFromSceneName(string sceneName, out GameMode detectedMode)
    {
        detectedMode = GameMode.Career;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (sceneName.StartsWith("F1_", StringComparison.OrdinalIgnoreCase) ||
            sceneName.IndexOf("Formula", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            detectedMode = GameMode.Formula1;
            return true;
        }

        if (sceneName.StartsWith("Offroad_", StringComparison.OrdinalIgnoreCase) ||
            sceneName.IndexOf("Offroad", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            detectedMode = GameMode.Offroad;
            return true;
        }

        if (sceneName.StartsWith("Career_", StringComparison.OrdinalIgnoreCase) ||
            sceneName.IndexOf("Career", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            detectedMode = GameMode.Career;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Извлекает 0-based индекс уровня из имени сцены вида "F1_Track_1" -> 0, "Career_Track_12" -> 11.
    /// </summary>
    public static bool TryParseTrackLevelIndexFromSceneName(string sceneName, out int levelIndex)
    {
        levelIndex = -1;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        int end = sceneName.Length - 1;
        while (end >= 0 && !char.IsDigit(sceneName[end]))
        {
            end--;
        }

        if (end < 0)
            return false;

        int start = end;
        while (start >= 0 && char.IsDigit(sceneName[start]))
        {
            start--;
        }

        string numberPart = sceneName.Substring(start + 1, end - start);
        if (int.TryParse(numberPart, out int trackNumber) && trackNumber >= 1)
        {
            levelIndex = trackNumber - 1;
            return true;
        }

        return false;
    }

    public static int GetDefaultMaxLevelsForMode(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Career:
                return 24;
            case GameMode.Formula1:
                return 30;
            default:
                return 30;
        }
    }

    public static string GetPrefix(GameMode mode)
    {
        return mode == GameMode.Career ? string.Empty : $"{mode}_";
    }

    public static string GetLevelUnlockKey(GameMode mode, int levelIndex)
    {
        return $"{GetPrefix(mode)}Level{levelIndex}";
    }

    public static string GetLevelAwardKey(GameMode mode, int levelIndex)
    {
        return $"{GetPrefix(mode)}Award_Level_{levelIndex}";
    }

    public static string GetSelectedLevelIdKey(GameMode mode)
    {
        return $"{GetPrefix(mode)}LevelID";
    }

    public static string GetCarUnlockKey(GameMode mode, int carIndex)
    {
        return $"{GetPrefix(mode)}Car{carIndex}";
    }

    public static string GetSelectedCarIdKey(GameMode mode)
    {
        return $"{GetPrefix(mode)}CarID";
    }
}

[Serializable]
public class ModeEntry
{
    public GameMode mode;
    public ModeButton modeButton;
    public LevelSelect levelSelect;
}

public class ModeSelect : MonoBehaviour
{
    public GameMode SelectedMode;

    [SerializeField] private LevelSelect careerLevelSelect;
    [FormerlySerializedAs("offroadLevelSelect")]
    [SerializeField] private LevelSelect formula1LevelSelect;
    [SerializeField] private ModeButton careerModeButton;
    [FormerlySerializedAs("offroadModeButton")]
    [SerializeField] private ModeButton formula1ModeButton;

    [Header("Дополнительные режимы (для масштабирования)")]
    [SerializeField] private List<ModeEntry> additionalModes = new List<ModeEntry>();

    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color unselectedColor = Color.white;
    [SerializeField] private Text TotalScores;
    [SerializeField] private AudioSource buttonClickSound;

    private bool listenersAssigned;

    private void Start()
    {
        AssignListenersIfNeeded();
        RefreshModeSelectUI();
    }

    private void OnEnable()
    {
        AssignListenersIfNeeded();
        RefreshModeSelectUI();
    }

    private void AssignListenersIfNeeded()
    {
        if (listenersAssigned)
            return;

        if (careerModeButton != null)
            careerModeButton.AssignButtonClickListener(SelectCareerMode);

        if (formula1ModeButton != null)
            formula1ModeButton.AssignButtonClickListener(SelectFormula1Mode);

        if (additionalModes != null)
        {
            for (int i = 0; i < additionalModes.Count; i++)
            {
                ModeEntry entry = additionalModes[i];
                if (entry != null && entry.modeButton != null)
                {
                    GameMode targetMode = entry.mode;
                    entry.modeButton.AssignButtonClickListener(() => SelectMode(targetMode));
                }
            }
        }

        listenersAssigned = true;
    }

    private void RefreshModeSelectUI()
    {
        UpdateModeButtons();
        if (TotalScores != null)
            TotalScores.text = PlayerPrefs.GetInt("TotalScores").ToString();
    }

    public void SelectCareerMode()
    {
        SelectMode(GameMode.Career);
    }

    public void SelectFormula1Mode()
    {
        SelectMode(GameMode.Formula1);
    }

    public void SelectOffroadMode()
    {
        SelectMode(GameMode.Formula1);
    }

    public void SelectMode(GameMode mode)
    {
        SelectedMode = mode;
        GameModeSaveKeys.SetActiveMode(mode);
        NitroStreets.Fuel.ModeFuelManager.EnsureInstance().ActiveMode = mode;
        UpdateModeButtons();

        LevelSelect targetLevelSelect = GetLevelSelectForMode(mode);
        if (targetLevelSelect == null)
        {
            Debug.LogError($"Level select panel for mode {mode} is not assigned.", this);
            return;
        }

        SetAllLevelSelectsInactiveExcept(targetLevelSelect);
        targetLevelSelect.gameMode = mode;
        targetLevelSelect.gameObject.SetActive(true);

        if (buttonClickSound != null)
            buttonClickSound.Play();

        gameObject.SetActive(false);
    }

    private LevelSelect GetLevelSelectForMode(GameMode mode)
    {
        if (mode == GameMode.Career)
            return careerLevelSelect;
        if (mode == GameMode.Formula1)
            return formula1LevelSelect;

        if (additionalModes != null)
        {
            for (int i = 0; i < additionalModes.Count; i++)
            {
                if (additionalModes[i] != null && additionalModes[i].mode == mode)
                    return additionalModes[i].levelSelect;
            }
        }

        return null;
    }

    private void SetAllLevelSelectsInactiveExcept(LevelSelect activePanel)
    {
        if (careerLevelSelect != null && careerLevelSelect != activePanel)
            careerLevelSelect.gameObject.SetActive(false);

        if (formula1LevelSelect != null && formula1LevelSelect != activePanel)
            formula1LevelSelect.gameObject.SetActive(false);

        if (additionalModes != null)
        {
            for (int i = 0; i < additionalModes.Count; i++)
            {
                LevelSelect ls = additionalModes[i]?.levelSelect;
                if (ls != null && ls != activePanel)
                    ls.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateModeButtons()
    {
        UpdateSingleModeButton(GameMode.Career, careerModeButton, careerLevelSelect);
        UpdateSingleModeButton(GameMode.Formula1, formula1ModeButton, formula1LevelSelect);

        if (additionalModes != null)
        {
            for (int i = 0; i < additionalModes.Count; i++)
            {
                ModeEntry entry = additionalModes[i];
                if (entry != null)
                {
                    UpdateSingleModeButton(entry.mode, entry.modeButton, entry.levelSelect);
                }
            }
        }
    }

    private void UpdateSingleModeButton(GameMode mode, ModeButton modeButton, LevelSelect levelSelect)
    {
        if (modeButton == null)
            return;

        modeButton.mode = mode;
        if (levelSelect != null)
            modeButton.levelSelectPanel = levelSelect.gameObject;

        if (modeButton.isLocked)
        {
            if (modeButton.button != null)
                modeButton.button.interactable = false;
            if (modeButton.lockedIcon != null)
                modeButton.lockedIcon.SetActive(true);
            if (modeButton.lockedPanel != null)
                modeButton.lockedPanel.SetActive(true);

            modeButton.SetSelected(SelectedMode == mode, selectedColor, unselectedColor);
            modeButton.UpdateProgressText("Coming Soon");
            return;
        }

        if (modeButton.button != null)
            modeButton.button.interactable = true;
        if (modeButton.lockedIcon != null)
            modeButton.lockedIcon.SetActive(false);
        if (modeButton.lockedPanel != null)
            modeButton.lockedPanel.SetActive(false);

        modeButton.SetSelected(SelectedMode == mode, selectedColor, unselectedColor);

        if (levelSelect != null && levelSelect.levelPrices != null && levelSelect.levelPrices.Length > 0)
        {
            levelSelect.gameMode = mode;
            levelSelect.EnsureModeStorageInitialized();
            int lastUnlockedIndex = FindLastUnlockedLevelIndex(mode, levelSelect.levelPrices.Length);
            int totalLevels = levelSelect.levelPrices.Length;
            modeButton.UpdateProgressText($"Level: {lastUnlockedIndex + 1}/{totalLevels}");
        }
    }

    private int FindLastUnlockedLevelIndex(GameMode mode, int totalLevels)
    {
        int lastLevelIndex = 0;
        for (int i = 0; i < totalLevels; i++)
        {
            if (PlayerPrefs.GetInt(GameModeSaveKeys.GetLevelUnlockKey(mode, i), 0) == 3)
            {
                lastLevelIndex = i;
            }
        }

        return lastLevelIndex;
    }
}
