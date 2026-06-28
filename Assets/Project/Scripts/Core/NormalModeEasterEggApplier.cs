using UnityEngine;
using UnityEngine.SceneManagement;

public class NormalModeEasterEggApplier : MonoBehaviour
{
    private const string NormalSceneName = "Normal_Mode";

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != NormalSceneName)
            return;

        ApplyRainbowEntityEyes();
    }

    private static void ApplyRainbowEntityEyes()
    {
        bool enabled = EasterEggSettings.NormalRainbowEntityEnabled;
        ApplyRainbowToEye("EyeGlow_Left", enabled);
        ApplyRainbowToEye("EyeGlow_Right", enabled);
    }

    private static void ApplyRainbowToEye(string eyeName, bool enabled)
    {
        GameObject eye = GameObject.Find(eyeName);
        if (eye == null)
            return;

        RainbowPointLight rainbow = eye.GetComponent<RainbowPointLight>();
        if (enabled)
        {
            if (rainbow == null)
                eye.AddComponent<RainbowPointLight>();

            return;
        }

        if (rainbow != null)
            Destroy(rainbow);
    }
}
