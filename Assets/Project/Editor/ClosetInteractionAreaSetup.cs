using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClosetInteractionAreaSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up Closet Interaction Areas";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ClosetsRootName = "Closets";
    private const string AreaName = "ClosetInteractionArea";

    private static readonly Vector3 AreaLocalPosition = new Vector3(0f, 1f, -0.9f);
    private static readonly Vector3 AreaSize = new Vector3(1.4f, 2f, 0.9f);

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
        Undo.SetCurrentGroupName("Set Up Closet Interaction Areas");
        int undoGroup = Undo.GetCurrentGroup();

        int closetsChecked = 0;
        int areasCreated = 0;
        int areasConfigured = 0;
        int scriptsConfigured = 0;

        ClosetHide[] closetHides = Object.FindObjectsByType<ClosetHide>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (ClosetHide closetHide in closetHides)
        {
            if (closetHide.gameObject.scene != scene)
                continue;

            Transform closet = closetHide.transform;
            closetsChecked++;

            BoxCollider areaCollider = EnsureInteractionArea(closet, ref areasCreated, ref areasConfigured);
            if (ConfigureClosetHide(closetHide, areaCollider))
                scriptsConfigured++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary =
            $"Closet interaction area setup complete. Checked {closetsChecked} closet(s), " +
            $"created {areasCreated} area(s), configured {areasConfigured} area collider(s), " +
            $"assigned {scriptsConfigured} ClosetHide script(s).";

        Debug.Log(summary);
        if (showDialog)
            EditorUtility.DisplayDialog("Closet Interaction Areas", summary, "OK");
    }

    private static BoxCollider EnsureInteractionArea(Transform closet, ref int created, ref int configured)
    {
        Transform areaTransform = closet.Find(AreaName);
        if (areaTransform == null)
        {
            GameObject area = new GameObject(AreaName);
            Undo.RegisterCreatedObjectUndo(area, "Create Closet Interaction Area");
            area.transform.SetParent(closet, false);
            areaTransform = area.transform;
            created++;
        }

        BoxCollider boxCollider = areaTransform.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = Undo.AddComponent<BoxCollider>(areaTransform.gameObject);
            configured++;
        }

        Undo.RecordObject(areaTransform, "Configure Closet Interaction Area Transform");
        areaTransform.localPosition = AreaLocalPosition;
        areaTransform.localRotation = Quaternion.identity;
        areaTransform.localScale = Vector3.one;
        EditorUtility.SetDirty(areaTransform);

        bool changed = false;
        if (!boxCollider.isTrigger)
        {
            boxCollider.isTrigger = true;
            changed = true;
        }

        if (boxCollider.center != Vector3.zero)
        {
            boxCollider.center = Vector3.zero;
            changed = true;
        }

        if (boxCollider.size != AreaSize)
        {
            boxCollider.size = AreaSize;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(boxCollider);
            configured++;
        }

        return boxCollider;
    }

    private static bool ConfigureClosetHide(ClosetHide closetHide, Collider interactionArea)
    {
        SerializedObject serializedObject = new SerializedObject(closetHide);
        bool changed = false;

        changed |= SetObjectReference(serializedObject, "interactionArea", interactionArea);
        changed |= SetString(serializedObject, "interactionAreaName", AreaName);
        changed |= SetBool(serializedObject, "requirePlayerInsideInteractionArea", true);
        changed |= SetBool(serializedObject, "createInteractionAreaIfMissing", true);
        changed |= SetVector3(serializedObject, "defaultInteractionAreaLocalPosition", AreaLocalPosition);
        changed |= SetVector3(serializedObject, "defaultInteractionAreaSize", AreaSize);

        if (!changed)
            return false;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(closetHide);
        return true;
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
