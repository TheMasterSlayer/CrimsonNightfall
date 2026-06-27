using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ElevatorAIZoneSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("CrimsonNightfall/Set Up Elevator AI Zones")]
    public static void SetUpElevatorAIZones()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int configured = 0;

        foreach (ElevatorDoorController doors in Object.FindObjectsByType<ElevatorDoorController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            ElevatorAIZone zone = doors.GetComponentInChildren<ElevatorAIZone>(true);
            if (zone == null)
            {
                GameObject zoneObject = new GameObject("ElevatorAIZone");
                Undo.RegisterCreatedObjectUndo(zoneObject, "Create Elevator AI Zone");
                zoneObject.transform.SetParent(doors.transform, false);
                zone = Undo.AddComponent<ElevatorAIZone>(zoneObject);
            }

            Undo.RecordObject(zone, "Configure Elevator AI Zone");
            zone.Configure(doors);
            EditorUtility.SetDirty(zone);
            configured++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Configured {configured} Elevator AI Zone(s).");
    }
}
