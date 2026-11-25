using System.Collections;
using ALIyerEdon;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelUnlocker : MonoBehaviour
{
    [SerializeField] private Race_Manager raceManager;
    [SerializeField] private string levelKey = "Level"; // Key prefix for PlayerPrefs
    [SerializeField] private int unlockLevelId = -1; // ID of the level to unlock

    [Header("Auto-Detection (Multi-Mode)")]
    [Tooltip("Автоматически определять индекс следующего уровня по номеру текущей сцены или raceManager.levelID + 1")]
    [SerializeField] private bool autoDetectNextLevelId = true;

    public void UnlockLevel()
    {
        StartCoroutine(UnlockLevelCoroutine());
    }

    private IEnumerator UnlockLevelCoroutine()
    {
        // Ждём конца текущего кадра, чтобы Race_Manager.Finish_Race() успел вызвать Start_Finish_UI.Update_Award()
        // и активировать иконку полученного кубка (Gold / Silver / Bronze) без задержки в 1 секунду.
        yield return new WaitForEndOfFrame();

        if (raceManager == null)
        {
            raceManager = FindAnyObjectByType<Race_Manager>();
        }

        Start_Finish_UI startFinishUI = FindAnyObjectByType<Start_Finish_UI>();
        if (raceManager == null)
        {
            Debug.LogWarning("Race_Manager reference is missing.");
            yield break;
        }

        if (startFinishUI == null)
        {
            Debug.LogWarning("Start_Finish_UI reference is missing.");
            yield break;
        }

        GameMode activeMode = ResolveActiveMode();
        int targetLevelId = ResolveTargetUnlockLevelId();
        if (targetLevelId < 0)
        {
            yield break;
        }

        int maxLevels = GameModeSaveKeys.GetDefaultMaxLevelsForMode(activeMode);
        if (maxLevels > 0 && targetLevelId >= maxLevels)
        {
            Debug.Log($"[LevelUnlocker] Final level ({targetLevelId}) of mode {activeMode} completed.");
            yield break;
        }

        string targetUnlockKey = ResolveUnlockKey(activeMode, targetLevelId);

        if (startFinishUI.goldIcon != null && startFinishUI.goldIcon.activeSelf)
        {
            PlayerPrefs.SetInt(targetUnlockKey, 3);
            PlayerPrefs.Save();
            Debug.Log($"[{targetUnlockKey}] unlocked with Gold!");
        }
        else if (startFinishUI.silverIcon != null && startFinishUI.silverIcon.activeSelf)
        {
            PlayerPrefs.SetInt(targetUnlockKey, 3);
            PlayerPrefs.Save();
            Debug.Log($"[{targetUnlockKey}] unlocked with Silver!");
        }
        else if (startFinishUI.bronzeIcon != null && startFinishUI.bronzeIcon.activeSelf)
        {
            PlayerPrefs.SetInt(targetUnlockKey, 3);
            PlayerPrefs.Save();
            Debug.Log($"[{targetUnlockKey}] unlocked with Bronze!");
        }
    }

    private GameMode ResolveActiveMode()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (GameModeSaveKeys.TryDetectModeFromSceneName(sceneName, out GameMode sceneMode))
        {
            return sceneMode;
        }

        return GameModeSaveKeys.GetActiveMode();
    }

    private int ResolveTargetUnlockLevelId()
    {
        if (autoDetectNextLevelId)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (GameModeSaveKeys.TryParseTrackLevelIndexFromSceneName(sceneName, out int parsedLevelIndex))
            {
                unlockLevelId = parsedLevelIndex + 1;
                return unlockLevelId;
            }

            if (raceManager != null && raceManager.levelID >= 0)
            {
                unlockLevelId = raceManager.levelID + 1;
                return unlockLevelId;
            }
        }

        return unlockLevelId;
    }

    private string ResolveUnlockKey(GameMode mode, int targetLevelId)
    {
        if (string.IsNullOrEmpty(levelKey) || levelKey == "Level")
        {
            return GameModeSaveKeys.GetLevelUnlockKey(mode, targetLevelId);
        }

        return levelKey + targetLevelId.ToString();
    }
}
