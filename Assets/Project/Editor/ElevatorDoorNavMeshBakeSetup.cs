using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ElevatorDoorNavMeshBakeSetup
{
    private const string MenuPath = "CrimsonNightfall/Prepare Elevator Doors For NavMesh Bake";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        Prepare(SceneManager.GetActiveScene(), true);
    }

    public static void RunBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        Prepare(scene, false);
    }

    private static void Prepare(Scene scene, bool showDialog)
    {
        Undo.SetCurrentGroupName("Prepare Elevator Doors For NavMesh Bake");
        int undoGroup = Undo.GetCurrentGroup();

        int configured = 0;
        foreach (ElevatorDoorController doors in Object.FindObjectsByType<ElevatorDoorController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (doors.gameObject.scene != scene)
                continue;

            if (PrepareElevator(doors))
                configured++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary =
            $"Prepared {configured} elevator door controller(s). The scene doors are open for NavMesh baking, " +
            "but each controller will close them when Play starts.";

        Debug.Log(summary);
        if (showDialog)
            EditorUtility.DisplayDialog("Elevator Doors Prepared", summary, "OK");
    }

    private static bool PrepareElevator(ElevatorDoorController doors)
    {
        if (doors == null)
            return false;

        Transform leftDoor = doors.LeftDoor;
        Transform rightDoor = doors.RightDoor;
        if (leftDoor == null || rightDoor == null)
            return false;

        Vector3 leftClosed = leftDoor.localPosition;
        Vector3 rightClosed = rightDoor.localPosition;

        SerializedObject serializedObject = new SerializedObject(doors);
        bool changed = false;
        changed |= SetBool(serializedObject, "doorsStartClosed", true);
        changed |= SetBool(serializedObject, "useSavedClosedPositions", true);
        changed |= SetBool(serializedObject, "startOpen", false);
        changed |= SetVector3(serializedObject, "leftClosedLocalPosition", leftClosed);
        changed |= SetVector3(serializedObject, "rightClosedLocalPosition", rightClosed);

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(doors);
        }

        Undo.RecordObject(leftDoor, "Move Elevator Left Door Open For Bake");
        Undo.RecordObject(rightDoor, "Move Elevator Right Door Open For Bake");
        leftDoor.localPosition = leftClosed + doors.LeftOpenOffset;
        rightDoor.localPosition = rightClosed + doors.RightOpenOffset;
        EditorUtility.SetDirty(leftDoor);
        EditorUtility.SetDirty(rightDoor);

        return true;
    }

    private static bool SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.boolValue == value)
            return false;

        property.boolValue = value;
        return true;
    }

    private static bool SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.vector3Value == value)
            return false;

        property.vector3Value = value;
        return true;
    }
}
