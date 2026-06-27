using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

public static class BasementGroundRepairTool
{
    private const string MenuPath = "CrimsonNightfall/Repair Basement Ground";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TargetName = "Basement_Ground";
    private const float FlatLocalY = -0.5f;

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        Repair(SceneManager.GetActiveScene(), true);
    }

    public static void RunBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        Repair(scene, false);
    }

    private static void Repair(Scene scene, bool showDialog)
    {
        GameObject target = FindByName(scene, TargetName);
        if (target == null)
        {
            Debug.LogWarning($"Could not find {TargetName} in {scene.path}.");
            return;
        }

        ProBuilderMesh proBuilderMesh = target.GetComponent<ProBuilderMesh>();
        if (proBuilderMesh == null)
        {
            Debug.LogWarning($"{TargetName} does not have a ProBuilderMesh component.");
            return;
        }

        Undo.SetCurrentGroupName("Repair Basement Ground");
        Undo.RecordObject(proBuilderMesh, "Flatten Basement Ground");

        Vector3[] positions = proBuilderMesh.positions.ToArray();
        for (int i = 0; i < positions.Length; i++)
            positions[i].y = FlatLocalY;

        proBuilderMesh.positions = positions;
        proBuilderMesh.ToMesh();
        proBuilderMesh.Refresh();

        MeshCollider meshCollider = target.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            Undo.RecordObject(meshCollider, "Refresh Basement Ground Collider");
            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            EditorUtility.SetDirty(meshCollider);
        }

        EditorUtility.SetDirty(proBuilderMesh);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary = $"{TargetName} flattened to local Y {FlatLocalY}.";
        Debug.Log(summary);
        if (showDialog)
            EditorUtility.DisplayDialog("Basement Ground Repair", summary, "OK");
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
