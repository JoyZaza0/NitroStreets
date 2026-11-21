using System;
using UnityEngine;

public static class PlayerProgression
{
    public const int ExperiencePerLevel = 100;
    private const string ExperienceKey = "PlayerProgression.v1.Experience";

    public static event Action<int> ExperienceChanged;

    public static int TotalExperience => Mathf.Max(0, PlayerPrefs.GetInt(ExperienceKey, 0));
    public static int Level => TotalExperience / ExperiencePerLevel + 1;
    public static int ExperienceIntoCurrentLevel => TotalExperience % ExperiencePerLevel;

    public static int AddExperience(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogError($"Experience amount must be positive; received {amount}.");
            return TotalExperience;
        }

        long updatedExperience = (long)TotalExperience + amount;
        int savedExperience = updatedExperience > int.MaxValue ? int.MaxValue : (int)updatedExperience;
        PlayerPrefs.SetInt(ExperienceKey, savedExperience);
        PlayerPrefs.Save();
        ExperienceChanged?.Invoke(savedExperience);
        return savedExperience;
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(ExperienceKey);
        PlayerPrefs.Save();
        ExperienceChanged?.Invoke(0);
    }
}
