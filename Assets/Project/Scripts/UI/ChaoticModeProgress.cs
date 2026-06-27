using UnityEngine;

public static class ChaoticModeProgress
{
    private const string ChaoticUnlockedKey = "Progress.ChaoticModeUnlocked";

    private static bool _secretInsightFoundThisRun;

    public static bool IsChaoticModeUnlocked => PlayerPrefs.GetInt(ChaoticUnlockedKey, 0) == 1;
    public static bool SecretInsightFoundThisRun => _secretInsightFoundThisRun;

    public static void ResetRunProgress()
    {
        _secretInsightFoundThisRun = false;
    }

    public static void MarkSecretInsightFound()
    {
        _secretInsightFoundThisRun = true;
    }

    public static bool TryUnlockAfterEscape()
    {
        if (!_secretInsightFoundThisRun)
            return false;

        if (IsChaoticModeUnlocked)
            return true;

        PlayerPrefs.SetInt(ChaoticUnlockedKey, 1);
        PlayerPrefs.Save();
        return true;
    }
}
