public static class ScpElevatorProgress
{
    public static bool SurvivedScpElevator { get; private set; }
    public static bool ExitElevatorAnchorDisabled { get; private set; }
    public static bool ScpElevatorAnchorDisabled { get; private set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SurvivedScpElevator = false;
        ExitElevatorAnchorDisabled = false;
        ScpElevatorAnchorDisabled = false;
    }

    public static void MarkSurvived()
    {
        SurvivedScpElevator = true;
    }

    public static void DisableExitElevatorAnchor()
    {
        ExitElevatorAnchorDisabled = true;
    }

    public static void DisableScpElevatorAnchor()
    {
        ScpElevatorAnchorDisabled = true;
    }
}
