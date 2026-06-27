using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PropsSceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up Scene Colliders, Lights, And Doors";
    private const StaticEditorFlags AllStaticFlags = (StaticEditorFlags)int.MaxValue;
    private const string RoomKeyId = "RoomKey";

    private static readonly HashSet<string> InitiallyLockedDoors = new HashSet<string>
    {
        "Library_Door",
        "HiddenStudy_Door_Left",
        "HiddenStudy_Door_Right",
        "WineCellar_Door",
        "MasterBedroom_Door_Right",
        "MasterBedroom_Door_Left"
    };

    [MenuItem(MenuPath)]
    private static void RunFromMenu()
    {
        SetUpScene(SceneManager.GetActiveScene(), true);
    }

    private static void SetUpScene(Scene scene, bool showDialog)
    {
        List<GameObject> propsRoots = FindPropsRoots(scene);
        int collidersAdded = 0;
        int pointLightsMarkedStatic = 0;
        int lockedDoorsConfigured = 0;
        int keysConfigured = 0;
        bool navMeshDoorsConfigured = false;

        Undo.SetCurrentGroupName("Set Up Props Colliders And Lights");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (GameObject propsRoot in propsRoots)
        {
            foreach (Transform child in propsRoot.GetComponentsInChildren<Transform>(true))
            {
                GameObject gameObject = child.gameObject;

                if (TryAddEfficientCollider(gameObject))
                {
                    collidersAdded++;
                }

                Light light = gameObject.GetComponent<Light>();
                if (light != null && light.type == LightType.Point)
                {
                    StaticEditorFlags currentFlags = GameObjectUtility.GetStaticEditorFlags(gameObject);
                    if (currentFlags != AllStaticFlags)
                    {
                        Undo.RecordObject(gameObject, "Mark Point Light Static");
                        GameObjectUtility.SetStaticEditorFlags(gameObject, AllStaticFlags);
                        pointLightsMarkedStatic++;
                    }
                }
            }
        }

        GameObject doorsRoot = FindByName(scene, "Doors");
        if (doorsRoot != null)
        {
            foreach (Transform child in doorsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (TryAddEfficientCollider(child.gameObject))
                    collidersAdded++;

                if (InitiallyLockedDoors.Contains(child.name) && ConfigureLockedDoor(child.gameObject))
                    lockedDoorsConfigured++;
            }

            NavMeshModifier modifier = doorsRoot.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = Undo.AddComponent<NavMeshModifier>(doorsRoot);
                navMeshDoorsConfigured = true;
            }

            if (!modifier.ignoreFromBuild || !modifier.applyToChildren)
            {
                Undo.RecordObject(modifier, "Exclude Doors From NavMesh Build");
                modifier.ignoreFromBuild = true;
                modifier.applyToChildren = true;
                EditorUtility.SetDirty(modifier);
                navMeshDoorsConfigured = true;
            }
        }

        GameObject roomKey = FindByName(scene, "Item_Key");
        if (roomKey != null)
        {
            ItemPickup pickup = roomKey.GetComponent<ItemPickup>();
            if (pickup != null && SetSerializedProperties(
                    pickup,
                    ("grantsKey", true),
                    ("keyId", RoomKeyId)))
            {
                keysConfigured++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        bool sceneChanged = collidersAdded > 0 || pointLightsMarkedStatic > 0 ||
                            lockedDoorsConfigured > 0 || keysConfigured > 0 || navMeshDoorsConfigured;
        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (navMeshDoorsConfigured)
        {
            NavMeshSurface surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface != null)
            {
                surface.BuildNavMesh();
                AssetDatabase.SaveAssets();
            }
        }

        string summary =
            $"Props setup complete in '{scene.name}': {propsRoots.Count} Props_ roots, " +
            $"{collidersAdded} BoxColliders added, {pointLightsMarkedStatic} point lights marked static, " +
            $"{lockedDoorsConfigured} locked doors configured, {keysConfigured} room keys configured, " +
            $"doors excluded from NavMesh: {navMeshDoorsConfigured}.";

        Debug.Log(summary);
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Props Setup", summary, "OK");
        }
    }

    private static GameObject FindByName(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }
        }

        return null;
    }

    private static bool ConfigureLockedDoor(GameObject gameObject)
    {
        RoomDoorController roomDoor = gameObject.GetComponent<RoomDoorController>();
        if (roomDoor != null)
            return SetSerializedProperties(roomDoor, ("startsLocked", true), ("requiredKeyId", RoomKeyId));

        RightRoomDoorController rightDoor = gameObject.GetComponent<RightRoomDoorController>();
        return rightDoor != null &&
               SetSerializedProperties(rightDoor, ("startsLocked", true), ("requiredKeyId", RoomKeyId));
    }

    private static bool SetSerializedProperties(UnityEngine.Object target, params (string name, object value)[] values)
    {
        var serializedObject = new SerializedObject(target);
        bool changed = false;

        foreach ((string name, object value) in values)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
                continue;

            if (value is bool boolValue && property.boolValue != boolValue)
            {
                property.boolValue = boolValue;
                changed = true;
            }
            else if (value is string stringValue && property.stringValue != stringValue)
            {
                property.stringValue = stringValue;
                changed = true;
            }
        }

        if (!changed)
            return false;

        Undo.RecordObject(target, "Configure Door And Key");
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return true;
    }

    private static List<GameObject> FindPropsRoots(Scene scene)
    {
        var propsRoots = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name.StartsWith("Props_", StringComparison.Ordinal))
                {
                    propsRoots.Add(transform.gameObject);
                }
            }
        }

        return propsRoots;
    }

    private static bool TryAddEfficientCollider(GameObject gameObject)
    {
        if (gameObject.GetComponent<Collider>() != null)
        {
            return false;
        }

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshFilter != null && meshFilter.sharedMesh != null && meshRenderer != null)
        {
            Bounds bounds = meshFilter.sharedMesh.bounds;
            if (!HasUsableSize(bounds.size))
            {
                return false;
            }

            BoxCollider collider = Undo.AddComponent<BoxCollider>(gameObject);
            collider.center = bounds.center;
            collider.size = bounds.size;
            return true;
        }

        SkinnedMeshRenderer skinnedRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer == null || !HasUsableSize(skinnedRenderer.localBounds.size))
        {
            return false;
        }

        BoxCollider skinnedCollider = Undo.AddComponent<BoxCollider>(gameObject);
        skinnedCollider.center = skinnedRenderer.localBounds.center;
        skinnedCollider.size = skinnedRenderer.localBounds.size;
        return true;
    }

    private static bool HasUsableSize(Vector3 size)
    {
        const float minimumDimension = 0.001f;
        return size.x > minimumDimension && size.y > minimumDimension && size.z > minimumDimension;
    }
}
