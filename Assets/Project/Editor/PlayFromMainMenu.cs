using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayFromMainMenu
{
    private const string MainMenuPath = "Assets/Project/Scenes/MainMenu/MainMenu.unity";

    static PlayFromMainMenu()
    {
        EditorApplication.delayCall += SetPlayModeStartScene;
    }

    [MenuItem("CrimsonNightfall/Play From Main Menu")]
    private static void Play()
    {
        SetPlayModeStartScene();
        EditorApplication.isPlaying = true;
    }

    private static void SetPlayModeStartScene()
    {
        SceneAsset mainMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
        EditorSceneManager.playModeStartScene = mainMenu;
    }
}
