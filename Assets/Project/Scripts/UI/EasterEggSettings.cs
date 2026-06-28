using UnityEngine;

public static class EasterEggSettings
{
    private const string NormalRainbowUnlockedKey = "EasterEgg.NormalRainbowEntity.Unlocked";
    private const string NormalRainbowEnabledKey = "EasterEgg.NormalRainbowEntity.Enabled";
    private const string ScpEntitySwitchUnlockedKey = "EasterEgg.ScpEntitySwitch.Unlocked";
    private const string ScpEntitySwitchEnabledKey = "EasterEgg.ScpEntitySwitch.Enabled";

    public static bool IsNormalRainbowEntityUnlocked =>
        PlayerPrefs.GetInt(NormalRainbowUnlockedKey, 0) == 1 || ChaoticModeProgress.IsChaoticModeUnlocked;
    public static bool IsScpEntitySwitchUnlocked => PlayerPrefs.GetInt(ScpEntitySwitchUnlockedKey, 0) == 1;

    public static bool NormalRainbowEntityEnabled
    {
        get => IsNormalRainbowEntityUnlocked && PlayerPrefs.GetInt(NormalRainbowEnabledKey, 0) == 1;
        set => SetNormalRainbowEnabled(value);
    }

    public static bool ScpEntitySwitchEnabled
    {
        get => IsScpEntitySwitchUnlocked && PlayerPrefs.GetInt(ScpEntitySwitchEnabledKey, 0) == 1;
        set => SetUnlockedBool(ScpEntitySwitchUnlockedKey, ScpEntitySwitchEnabledKey, value);
    }

    public static bool UnlockNormalRainbowEntity()
    {
        return Unlock(NormalRainbowUnlockedKey);
    }

    public static bool UnlockScpEntitySwitch()
    {
        return Unlock(ScpEntitySwitchUnlockedKey);
    }

    private static bool Unlock(string key)
    {
        if (PlayerPrefs.GetInt(key, 0) == 1)
            return false;

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        return true;
    }

    private static void SetUnlockedBool(string unlockKey, string settingKey, bool value)
    {
        if (PlayerPrefs.GetInt(unlockKey, 0) != 1)
            value = false;

        PlayerPrefs.SetInt(settingKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void SetNormalRainbowEnabled(bool value)
    {
        if (!IsNormalRainbowEntityUnlocked)
            value = false;

        PlayerPrefs.SetInt(NormalRainbowEnabledKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
