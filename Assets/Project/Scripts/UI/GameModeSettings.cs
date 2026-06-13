public enum GameMode
{
    None,
    Normal,
    Chaos
}

public static class GameModeSettings
{
    public static GameMode SelectedMode { get; private set; } = GameMode.None;

    public static void Select(GameMode mode)
    {
        SelectedMode = mode;
    }

    public static void Clear()
    {
        SelectedMode = GameMode.None;
    }
}
