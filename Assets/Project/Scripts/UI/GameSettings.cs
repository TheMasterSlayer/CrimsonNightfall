using UnityEngine;

public static class GameSettings
{
    private const string SprintToggleKey = "Settings.SprintToggle";
    private const string CrouchToggleKey = "Settings.CrouchToggle";
    private const string InvertYKey = "Settings.InvertY";
    private const string MouseSensitivityKey = "Settings.MouseSensitivity";
    private const string MasterVolumeKey = "Settings.MasterVolume";
    private const string FullscreenKey = "Settings.Fullscreen";

    public static bool SprintToggle
    {
        get => PlayerPrefs.GetInt(SprintToggleKey, 0) == 1;
        set => SetBool(SprintToggleKey, value);
    }

    public static bool CrouchToggle
    {
        get => PlayerPrefs.GetInt(CrouchToggleKey, 0) == 1;
        set => SetBool(CrouchToggleKey, value);
    }

    public static bool InvertY
    {
        get => PlayerPrefs.GetInt(InvertYKey, 0) == 1;
        set => SetBool(InvertYKey, value);
    }

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(MouseSensitivityKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp(value, 0.25f, 2f));
            PlayerPrefs.Save();
        }
    }

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.Save();
        }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            SetBool(FullscreenKey, value);
            Screen.fullScreen = value;
        }
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
        Screen.fullScreen = Fullscreen;
    }

    private static void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
