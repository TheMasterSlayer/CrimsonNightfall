using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SecretRoomSceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up Secret Room Button";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        SetUp(SceneManager.GetActiveScene(), true);
    }

    public static void RunBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        SetUp(scene, false);
    }

    private static void SetUp(Scene scene, bool showDialog)
    {
        GameObject secretButton = FindByName(scene, "SecretButton");
        GameObject secretDoor = FindByName(scene, "Secret_Door");
        GameObject secretWall1 = FindByName(scene, "Secret_Wall_1");
        GameObject secretWall2 = FindByName(scene, "Secret_Wall_2");
        GameObject secretEntry = FindByName(scene, "Secret_Entry");

        if (secretButton == null || secretDoor == null)
        {
            Debug.LogWarning(
                $"Secret room setup missing object(s). SecretButton: {secretButton != null}, Secret_Door: {secretDoor != null}.");
            return;
        }

        GameObject[] additionalBlockers = BuildBlockerArray(secretWall1, secretWall2);
        if (additionalBlockers.Length < 2)
            Debug.LogWarning(
                $"Secret room setup found {additionalBlockers.Length}/2 wall blocker(s). Secret_Wall_1: {secretWall1 != null}, Secret_Wall_2: {secretWall2 != null}.");

        Undo.SetCurrentGroupName("Set Up Secret Room Button");
        int undoGroup = Undo.GetCurrentGroup();

        int configured = 0;

        SecretButtonController controller = secretButton.GetComponent<SecretButtonController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<SecretButtonController>(secretButton);
            configured++;
        }

        Light glow = EnsureGlow(secretDoor.transform, ref configured);
        if (ConfigureController(controller, secretDoor, additionalBlockers, glow))
            configured++;

        foreach (Renderer renderer in secretDoor.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
            {
                Undo.RecordObject(renderer, "Hide Secret Door Renderer");
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
                configured++;
            }
        }

        configured += EnableColliders(secretDoor, "Enable Secret Door Blocker");
        foreach (GameObject blocker in additionalBlockers)
            configured += EnableColliders(blocker, "Enable Secret Wall Blocker");

        if (secretEntry != null)
            configured += ConfigureSecretEntry(secretEntry);
        else
            Debug.LogWarning("Secret room setup could not find Secret_Entry for Chaotic Mode insight.");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary = $"Secret room setup complete. Configured {configured} object(s).";
        Debug.Log(summary);
        if (showDialog)
            EditorUtility.DisplayDialog("Secret Room Setup", summary, "OK");
    }

    private static Light EnsureGlow(Transform secretDoor, ref int configured)
    {
        Transform existing = secretDoor.Find("SecretDoor_BlueGlow");
        Light light = existing != null ? existing.GetComponent<Light>() : null;

        if (light == null)
        {
            GameObject glowObject = new GameObject("SecretDoor_BlueGlow");
            Undo.RegisterCreatedObjectUndo(glowObject, "Create Secret Door Glow");
            glowObject.transform.SetParent(secretDoor, false);
            glowObject.transform.localPosition = new Vector3(0f, 0.25f, -0.35f);
            glowObject.transform.localRotation = Quaternion.identity;
            glowObject.transform.localScale = Vector3.one;

            light = Undo.AddComponent<Light>(glowObject);
            configured++;
        }

        Undo.RecordObject(light, "Configure Secret Door Glow");
        light.type = LightType.Point;
        light.color = new Color(0.35f, 0.85f, 1f);
        light.intensity = 1.35f;
        light.range = 3f;
        light.enabled = false;
        EditorUtility.SetDirty(light);
        return light;
    }

    private static bool ConfigureController(
        SecretButtonController controller,
        GameObject secretDoor,
        GameObject[] additionalBlockers,
        Light glow)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        bool changed = false;

        changed |= SetObjectReference(serializedObject, "secretDoor", secretDoor);
        changed |= SetObjectReferenceArray(serializedObject, "additionalPassageBlockers", additionalBlockers);
        changed |= SetObjectReference(serializedObject, "passageGlow", glow);
        changed |= SetString(serializedObject, "activatedMessage", "You have enabled a secret room.");
        changed |= SetBool(serializedObject, "blockPassageUntilPressed", true);
        changed |= SetBool(serializedObject, "keepDoorInvisible", true);

        if (!changed)
            return false;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        return true;
    }

    private static int EnableColliders(GameObject target, string undoName)
    {
        if (target == null)
            return 0;

        int configured = 0;
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
        {
            if (collider.enabled)
                continue;

            Undo.RecordObject(collider, undoName);
            collider.enabled = true;
            EditorUtility.SetDirty(collider);
            configured++;
        }

        return configured;
    }

    private static int ConfigureSecretEntry(GameObject secretEntry)
    {
        int configured = 0;

        ReadablePaper paper = secretEntry.GetComponent<ReadablePaper>();
        if (paper == null)
        {
            paper = Undo.AddComponent<ReadablePaper>(secretEntry);
            configured++;
        }

        SerializedObject serializedObject = new SerializedObject(paper);
        bool changed = false;

        changed |= SetBool(serializedObject, "grantsChaoticModeInsight", true);
        changed |= SetString(
            serializedObject,
            "chaoticInsightMessage",
            "you gained insight on Chaotic Mode... now survive this night... or forever forget the truth...");

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(paper);
            configured++;
        }

        return configured;
    }

    private static bool SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetObjectReferenceArray(
        SerializedObject serializedObject,
        string propertyName,
        GameObject[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return false;

        values ??= new GameObject[0];
        bool changed = property.arraySize != values.Length;
        if (property.arraySize != values.Length)
            property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == values[i])
                continue;

            element.objectReferenceValue = values[i];
            changed = true;
        }

        return changed;
    }

    private static bool SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.stringValue == value)
            return false;

        property.stringValue = value;
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

    private static GameObject[] BuildBlockerArray(params GameObject[] blockers)
    {
        int count = 0;
        foreach (GameObject blocker in blockers)
        {
            if (blocker != null)
                count++;
        }

        GameObject[] validBlockers = new GameObject[count];
        int index = 0;
        foreach (GameObject blocker in blockers)
        {
            if (blocker != null)
                validBlockers[index++] = blocker;
        }

        return validBlockers;
    }
}
