using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class NavMeshDoorwayDiagnostics
{
    private const string LinkRootName = "DoorwayNavMeshLinks";

    [MenuItem("CrimsonNightfall/Repair Study And Guest Room NavMesh Links")]
    private static void RunFromMenu()
    {
        SetUpDoorwayLink("Study_Door");
        SetUpDoorwayLink("GuestRoom_Door");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private static bool SetUpDoorwayLink(string doorName)
    {
        GameObject door = GameObject.Find(doorName);
        if (door == null)
        {
            Debug.LogWarning($"[NavMesh Doorway] Could not find {doorName}.");
            return false;
        }

        Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[NavMesh Doorway] {doorName} has no renderer bounds.");
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 thinAxis = bounds.size.x <= bounds.size.z ? Vector3.right : Vector3.forward;
        Vector3 sideA = bounds.center + thinAxis * (bounds.extents[thinAxis == Vector3.right ? 0 : 2] + 1.25f);
        Vector3 sideB = bounds.center - thinAxis * (bounds.extents[thinAxis == Vector3.right ? 0 : 2] + 1.25f);

        bool foundA = NavMesh.SamplePosition(sideA, out NavMeshHit hitA, 2f, NavMesh.AllAreas);
        bool foundB = NavMesh.SamplePosition(sideB, out NavMeshHit hitB, 2f, NavMesh.AllAreas);
        if (!foundA || !foundB)
        {
            Debug.LogWarning($"[NavMesh Doorway] Could not sample both sides of {doorName}.");
            return false;
        }

        GameObject linkRoot = GameObject.Find(LinkRootName);
        if (linkRoot == null)
        {
            linkRoot = new GameObject(LinkRootName);
            Undo.RegisterCreatedObjectUndo(linkRoot, "Create Doorway NavMesh Links");
        }

        string linkName = $"NavMeshLink_{doorName}";
        Transform existing = linkRoot.transform.Find(linkName);
        GameObject linkObject;
        bool changed = false;

        if (existing == null)
        {
            linkObject = new GameObject(linkName);
            Undo.RegisterCreatedObjectUndo(linkObject, "Create Doorway NavMesh Link");
            linkObject.transform.SetParent(linkRoot.transform);
            changed = true;
        }
        else
        {
            linkObject = existing.gameObject;
        }

        Vector3 midpoint = (hitA.position + hitB.position) * 0.5f;
        Undo.RecordObject(linkObject.transform, "Position Doorway NavMesh Link");
        linkObject.transform.SetPositionAndRotation(midpoint, Quaternion.identity);

        NavMeshLink link = linkObject.GetComponent<NavMeshLink>();
        if (link == null)
        {
            link = Undo.AddComponent<NavMeshLink>(linkObject);
            changed = true;
        }

        Undo.RecordObject(link, "Configure Doorway NavMesh Link");
        link.agentTypeID = 0;
        link.startPoint = hitA.position - midpoint;
        link.endPoint = hitB.position - midpoint;
        link.width = 0.8f;
        link.bidirectional = true;
        link.costModifier = -1f;
        link.area = 0;
        EditorUtility.SetDirty(link);

        var path = new NavMeshPath();
        bool pathComplete = NavMesh.CalculatePath(hitA.position, hitB.position, NavMesh.AllAreas, path) &&
                            path.status == NavMeshPathStatus.PathComplete;

        Debug.Log(
            $"[NavMesh Doorway] {doorName}: center={bounds.center:F3}, size={bounds.size:F3}, " +
            $"axis={thinAxis}, sideA={(foundA ? hitA.position.ToString("F3") : "none")}, " +
            $"sideB={(foundB ? hitB.position.ToString("F3") : "none")}, connected={pathComplete}.");

        return changed;
    }
}
