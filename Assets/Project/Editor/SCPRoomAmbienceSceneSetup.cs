using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SCPRoomAmbienceSceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up SCP Room Ambience";
    private const string RoomName = "SCP_Room";
    private const string ZoneName = "SCP_Room_AmbienceZone";

    [MenuItem(MenuPath)]
    private static void RunFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject room = FindByName(scene, RoomName);
        if (room == null)
        {
            EditorUtility.DisplayDialog("SCP Room Ambience", $"Could not find '{RoomName}' in the active scene.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Set Up SCP Room Ambience");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject zone = FindChildByName(room.transform, ZoneName);
        if (zone == null)
        {
            zone = new GameObject(ZoneName);
            Undo.RegisterCreatedObjectUndo(zone, "Create SCP Room Ambience Zone");
            zone.transform.SetParent(room.transform, true);
        }

        Bounds bounds = CalculateBounds(room.transform, zone.transform);
        zone.transform.position = bounds.center;
        zone.transform.rotation = Quaternion.identity;
        zone.transform.localScale = Vector3.one;

        BoxCollider collider = zone.GetComponent<BoxCollider>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider>(zone);

        Undo.RecordObject(collider, "Configure SCP Room Ambience Collider");
        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.size = new Vector3(
            Mathf.Max(1f, bounds.size.x),
            Mathf.Max(1f, bounds.size.y),
            Mathf.Max(1f, bounds.size.z));
        EditorUtility.SetDirty(collider);

        if (zone.GetComponent<SCPRoomAmbienceZone>() == null)
            Undo.AddComponent<SCPRoomAmbienceZone>(zone);

        AudioSource audioSource = zone.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = Undo.AddComponent<AudioSource>(zone);

        Undo.RecordObject(audioSource, "Configure SCP Room Ambience AudioSource");
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        EditorUtility.SetDirty(audioSource);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog(
            "SCP Room Ambience",
            "SCP room ambience zone created/configured.\n\nDrag your ambience clip into the Ambience Clip slot on SCP_Room_AmbienceZone.",
            "OK");
    }

    private static Bounds CalculateBounds(Transform room, Transform zoneToIgnore)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(room.position, new Vector3(8f, 4f, 8f));

        foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform == zoneToIgnore || renderer.transform.IsChildOf(zoneToIgnore))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        foreach (Collider collider in room.GetComponentsInChildren<Collider>(true))
        {
            if (collider.transform == zoneToIgnore || collider.transform.IsChildOf(zoneToIgnore))
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    private static GameObject FindByName(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject result = FindChildByName(root.transform, objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameObject FindChildByName(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child.gameObject;
        }

        return null;
    }
}
