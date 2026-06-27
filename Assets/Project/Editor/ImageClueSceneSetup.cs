using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ImageClueSceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up Image Clue And Exit Key";
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
        GameObject imageClue = FindByName(scene, "ImageClue");
        GameObject exitKey = FindByName(scene, "ExitKey");
        GameObject exitKeySpawns = FindByName(scene, "EK_Spawns");

        if (imageClue == null || exitKey == null || exitKeySpawns == null)
        {
            Debug.LogWarning(
                $"Image clue setup missing object(s). ImageClue: {imageClue != null}, ExitKey: {exitKey != null}, EK_Spawns: {exitKeySpawns != null}.");
            return;
        }

        Undo.SetCurrentGroupName("Set Up Image Clue And Exit Key");
        int undoGroup = Undo.GetCurrentGroup();

        int configured = 0;
        Renderer paperRenderer = EnsurePaperVisual(imageClue.transform, ref configured);
        configured += EnsureCollider(imageClue);

        ItemPickup pickup = imageClue.GetComponent<ItemPickup>();
        if (pickup == null)
        {
            pickup = Undo.AddComponent<ItemPickup>(imageClue);
            configured++;
        }

        if (SetSerializedProperties(
                pickup,
                ("itemName", "ImageClue"),
                ("grantsKey", false),
                ("keyId", ""),
                ("addToInventory", true),
                ("collectionMessage", "You gained insight on where the Exit Key is."),
                ("inspectBeforeCollect", true),
                ("inspectHint", "Move mouse to inspect. Press E to collect. Press ESC to put back.")))
        {
            configured++;
        }

        RandomItemSpawn randomItemSpawn = imageClue.GetComponent<RandomItemSpawn>();
        if (randomItemSpawn == null)
        {
            Undo.AddComponent<RandomItemSpawn>(imageClue);
            configured++;
        }

        RandomItemSpawn exitKeyRandomSpawn = exitKey.GetComponent<RandomItemSpawn>();
        if (exitKeyRandomSpawn != null && exitKeyRandomSpawn.enabled)
        {
            Undo.RecordObject(exitKeyRandomSpawn, "Disable ExitKey Random Spawn");
            exitKeyRandomSpawn.enabled = false;
            EditorUtility.SetDirty(exitKeyRandomSpawn);
            configured++;
        }

        ImageClueController controller = imageClue.GetComponent<ImageClueController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<ImageClueController>(imageClue);
            configured++;
        }

        if (ConfigureImageClueController(controller, paperRenderer, exitKey, GetSortedChildren(exitKeySpawns.transform)))
            configured++;

        EditorUtility.SetDirty(imageClue);
        EditorUtility.SetDirty(exitKey);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary = $"Image clue setup complete. Configured {configured} item(s).";
        Debug.Log(summary);
        if (showDialog)
            EditorUtility.DisplayDialog("Image Clue Setup", summary, "OK");
    }

    private static Renderer EnsurePaperVisual(Transform imageClue, ref int configured)
    {
        Transform existing = imageClue.Find("ImageClue_Paper");
        if (existing != null)
        {
            Renderer existingRenderer = existing.GetComponent<Renderer>();
            if (existingRenderer != null)
                return existingRenderer;
        }

        GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Undo.RegisterCreatedObjectUndo(paper, "Create Image Clue Paper");
        paper.name = "ImageClue_Paper";
        paper.transform.SetParent(imageClue, false);
        paper.transform.localPosition = Vector3.zero;
        paper.transform.localRotation = Quaternion.identity;
        paper.transform.localScale = new Vector3(0.65f, 0.45f, 1f);

        Collider collider = paper.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = paper.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.name = "ImageClue_Paper_Runtime";
            material.color = Color.white;
            renderer.sharedMaterial = material;
        }

        configured++;
        return renderer;
    }

    private static int EnsureCollider(GameObject imageClue)
    {
        BoxCollider collider = imageClue.GetComponent<BoxCollider>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider>(imageClue);

        bool changed = false;
        if (!collider.isTrigger)
        {
            collider.isTrigger = true;
            changed = true;
        }

        if (collider.size != new Vector3(0.8f, 0.55f, 0.12f))
        {
            collider.size = new Vector3(0.8f, 0.55f, 0.12f);
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(collider);
            return 1;
        }

        return 0;
    }

    private static bool ConfigureImageClueController(
        ImageClueController controller,
        Renderer paperRenderer,
        GameObject exitKey,
        Transform[] exitKeySpawns)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        bool changed = false;

        changed |= SetString(serializedObject, "inventoryItemId", "ImageClue");
        changed |= SetString(serializedObject, "selectedHint", "left click to view image clue.");
        changed |= SetString(serializedObject, "insightMessage", "You gained insight on where the Exit Key is.");
        changed |= SetString(serializedObject, "clueImageResourcePrefix", "ImageClue");
        changed |= SetInt(serializedObject, "clueImageResourceCount", 4);
        changed |= SetObjectReference(serializedObject, "clueSurfaceRenderer", paperRenderer);
        changed |= SetObjectReference(serializedObject, "exitKey", exitKey);
        changed |= SetTransformArray(serializedObject, "exitKeySpawnPoints", exitKeySpawns);

        if (!changed)
            return false;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        return true;
    }

    private static Transform[] GetSortedChildren(Transform parent)
    {
        List<Transform> children = new List<Transform>();
        foreach (Transform child in parent)
            children.Add(child);

        children.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return children.ToArray();
    }

    private static bool SetSerializedProperties(Object target, params (string name, object value)[] values)
    {
        SerializedObject serializedObject = new SerializedObject(target);
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

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        return changed;
    }

    private static bool SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.stringValue == value)
            return false;

        property.stringValue = value;
        return true;
    }

    private static bool SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.intValue == value)
            return false;

        property.intValue = value;
        return true;
    }

    private static bool SetTransformArray(SerializedObject serializedObject, string propertyName, Transform[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return false;

        bool changed = property.arraySize != values.Length;
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
}
