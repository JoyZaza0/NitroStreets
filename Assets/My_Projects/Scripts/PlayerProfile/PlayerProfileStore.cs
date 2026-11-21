using UnityEngine;

public static class PlayerProfileStore
{
    public const int FlagCount = 10;

    private const string RegisteredKey = "PlayerProfile.v1.Registered";
    private const string PlayerNameKey = "PlayerProfile.v1.Name";
    private const string FlagIndexKey = "PlayerProfile.v1.FlagIndex";

    public static bool IsRegistered =>
        PlayerPrefs.GetInt(RegisteredKey, 0) == 1 &&
        !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(PlayerNameKey, string.Empty)) &&
        PlayerPrefs.GetInt(FlagIndexKey, -1) >= 0 &&
        PlayerPrefs.GetInt(FlagIndexKey, -1) < FlagCount;

    public static string PlayerName => PlayerPrefs.GetString(PlayerNameKey, string.Empty);
    public static int FlagIndex => PlayerPrefs.GetInt(FlagIndexKey, -1);

    public static bool Save(string playerName, int flagIndex)
    {
        string normalizedName = playerName == null ? string.Empty : playerName.Trim();
        if (normalizedName.Length == 0 || flagIndex < 0 || flagIndex >= FlagCount)
        {
            Debug.LogError("Player profile requires a non-empty name and a valid flag index.");
            return false;
        }

        PlayerPrefs.SetString(PlayerNameKey, normalizedName);
        PlayerPrefs.SetInt(FlagIndexKey, flagIndex);
        PlayerPrefs.SetInt(RegisteredKey, 1);
        PlayerPrefs.Save();
        return true;
    }
}
