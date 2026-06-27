using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SCPPropsSceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up SCP Props";
    private const string PropsRootName = "SCP_Props";
    private const string ScpName = "SCP-096";

    [MenuItem(MenuPath)]
    private static void RunFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Set Up SCP Props");
        int undoGroup = Undo.GetCurrentGroup();

        int collidersAdded = 0;
        int meshCollidersRepaired = 0;
        int rigidbodiesAdded = 0;
        int propComponentsAdded = 0;
        int nestedRigidbodiesRemoved = 0;
        int nestedPropComponentsRemoved = 0;
        int pushersAdded = 0;

        GameObject propsRoot = FindByName(scene, PropsRootName);
        if (propsRoot == null)
        {
            EditorUtility.DisplayDialog("SCP Props Setup", $"Could not find '{PropsRootName}' in the active scene.", "OK");
            return;
        }

        foreach (Transform child in propsRoot.transform)
        {
            if (!HasVisibleRenderer(child.gameObject))
                continue;

            RemoveNestedPhysicsComponents(child, ref nestedRigidbodiesRemoved, ref nestedPropComponentsRemoved);

            if (!HasColliderInSelfOrChildren(child.gameObject))
            {
                AddBestCollider(child.gameObject);
                collidersAdded++;
            }

            meshCollidersRepaired += RepairMeshCollidersForDynamicRigidbody(child.gameObject);

            Rigidbody body = child.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody>(child.gameObject);
                rigidbodiesAdded++;
            }

            body.mass = CalculateStartingMass(child.gameObject);
            body.linearDamping = 0.35f;
            body.angularDamping = 0.15f;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            EditorUtility.SetDirty(body);

            if (child.GetComponent<SCPPropPhysicsObject>() == null)
            {
                Undo.AddComponent<SCPPropPhysicsObject>(child.gameObject);
                propComponentsAdded++;
            }
        }

        GameObject scp = FindByName(scene, ScpName);
        if (scp != null)
        {
            SCP096PropPusher pusher = scp.GetComponent<SCP096PropPusher>();
            if (pusher == null)
            {
                pusher = Undo.AddComponent<SCP096PropPusher>(scp);
                pushersAdded++;
            }

            ConfigurePusher(pusher);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary =
            $"SCP props setup complete.\n\n" +
            $"Colliders added: {collidersAdded}\n" +
            $"Mesh colliders made convex: {meshCollidersRepaired}\n" +
            $"Rigidbodies added: {rigidbodiesAdded}\n" +
            $"Prop physics components added: {propComponentsAdded}\n" +
            $"Nested rigidbodies removed: {nestedRigidbodiesRemoved}\n" +
            $"Nested prop physics components removed: {nestedPropComponentsRemoved}\n" +
            $"SCP-096 pushers added: {pushersAdded}";

        Debug.Log(summary);
        EditorUtility.DisplayDialog("SCP Props Setup", summary, "OK");
    }

    private static void AddBestCollider(GameObject gameObject)
    {
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinnedMesh = gameObject.GetComponent<SkinnedMeshRenderer>();

        if (meshFilter != null && meshFilter.sharedMesh != null && skinnedMesh == null)
        {
            MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(gameObject);
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = true;
            EditorUtility.SetDirty(meshCollider);
            return;
        }

        BoxCollider boxCollider = Undo.AddComponent<BoxCollider>(gameObject);
        Renderer renderer = gameObject.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            Bounds localBounds = WorldBoundsToLocalBounds(gameObject.transform, renderer.bounds);
            boxCollider.center = localBounds.center;
            boxCollider.size = localBounds.size;
        }

        EditorUtility.SetDirty(boxCollider);
    }

    private static void ConfigurePusher(SCP096PropPusher pusher)
    {
        SetSerializedProperties(
            pusher,
            ("impactRadius", 2.25f),
            ("impactForwardOffset", 1.1f),
            ("propHitCooldown", 0.08f),
            ("baseImpactForce", 85f),
            ("maxImpactForce", 220f),
            ("upwardLift", 0.12f),
            ("torqueForce", 32f),
            ("minimumRunSpeedForFullForce", 4f),
            ("temporarilyIgnorePropCollision", true),
            ("ignorePropCollisionDuration", 1.25f));
    }

    private static int RepairMeshCollidersForDynamicRigidbody(GameObject gameObject)
    {
        int repaired = 0;
        foreach (MeshCollider meshCollider in gameObject.GetComponentsInChildren<MeshCollider>(true))
        {
            if (meshCollider.convex)
                continue;

            Undo.RecordObject(meshCollider, "Make SCP Prop MeshCollider Convex");
            meshCollider.convex = true;
            EditorUtility.SetDirty(meshCollider);
            repaired++;
        }

        return repaired;
    }

    private static void RemoveNestedPhysicsComponents(
        Transform mainProp,
        ref int rigidbodiesRemoved,
        ref int propComponentsRemoved)
    {
        foreach (Transform child in mainProp.GetComponentsInChildren<Transform>(true))
        {
            if (child == mainProp)
                continue;

            SCPPropPhysicsObject propComponent = child.GetComponent<SCPPropPhysicsObject>();
            if (propComponent != null)
            {
                Undo.DestroyObjectImmediate(propComponent);
                propComponentsRemoved++;
            }

            Rigidbody body = child.GetComponent<Rigidbody>();
            if (body != null)
            {
                Undo.DestroyObjectImmediate(body);
                rigidbodiesRemoved++;
            }
        }
    }

    private static bool HasVisibleRenderer(GameObject gameObject)
    {
        foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
                return true;
        }

        return false;
    }

    private static bool HasColliderInSelfOrChildren(GameObject gameObject)
    {
        return gameObject.GetComponentInChildren<Collider>(true) != null;
    }

    private static float CalculateStartingMass(GameObject gameObject)
    {
        Bounds bounds = CalculateBounds(gameObject);
        Vector3 size = bounds.size;
        float volume = Mathf.Max(0.02f, size.x * size.y * size.z);
        return Mathf.Clamp(volume * 3.5f, 0.35f, 18f);
    }

    private static Bounds CalculateBounds(GameObject gameObject)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(gameObject.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static Bounds WorldBoundsToLocalBounds(Transform transform, Bounds worldBounds)
    {
        Vector3 min = transform.InverseTransformPoint(worldBounds.min);
        Vector3 max = transform.InverseTransformPoint(worldBounds.max);
        Bounds bounds = new Bounds();
        bounds.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
        return bounds;
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
            else if (value is float floatValue && !Mathf.Approximately(property.floatValue, floatValue))
            {
                property.floatValue = floatValue;
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

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return true;
    }
}
