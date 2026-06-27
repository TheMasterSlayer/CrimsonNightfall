using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class SCP096SceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up SCP-096";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string FbxPath = "Assets/ThirdParty/scp-096-unity/source/SCP-096_Unity.fbx";
    private const string ControllerFolder = "Assets/Project/AnimationControllers";
    private const string ControllerPath = ControllerFolder + "/SCP096.controller";

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
        Undo.SetCurrentGroupName("Set Up SCP-096");
        int undoGroup = Undo.GetCurrentGroup();
        int configured = 0;

        GameObject scp = FindByName(scene, "SCP-096");
        GameObject scpElevator = FindByName(scene, "SCP_Elevator");

        if (scp == null)
        {
            Debug.LogWarning("SCP-096 setup could not find SCP-096 in the scene.");
            return;
        }

        AnimationClip idleClip = FindClip("Armature|096_Idle");
        AnimationClip distressClip = FindClip("Armature|096_DistressTest");
        AnimationClip sprintClip = FindClip("Armature|096_Sprint");
        AnimatorController animatorController = ConfigureAnimatorController(idleClip, distressClip, sprintClip);

        Animator animator = scp.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(scp);
            configured++;
        }

        if (animator.runtimeAnimatorController != animatorController)
        {
            Undo.RecordObject(animator, "Assign SCP-096 Animator");
            animator.runtimeAnimatorController = animatorController;
            EditorUtility.SetDirty(animator);
            configured++;
        }

        NavMeshAgent agent = scp.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = Undo.AddComponent<NavMeshAgent>(scp);
            configured++;
        }

        Undo.RecordObject(agent, "Configure SCP-096 NavMeshAgent");
        agent.speed = 11f;
        agent.acceleration = 80f;
        agent.angularSpeed = 1080f;
        agent.autoBraking = false;
        agent.stoppingDistance = 0f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        EditorUtility.SetDirty(agent);

        SCP096Controller controller = scp.GetComponent<SCP096Controller>();
        if (controller == null)
        {
            controller = Undo.AddComponent<SCP096Controller>(scp);
            configured++;
        }

        Camera scpCamera = FindChildCamera(scp.transform, "Camera");
        if (scpCamera != null && scpCamera.enabled)
        {
            Undo.RecordObject(scpCamera, "Disable SCP-096 Camera");
            scpCamera.enabled = false;
            EditorUtility.SetDirty(scpCamera);
            configured++;
        }

        configured += ConfigureScpController(controller, animator, scpCamera, idleClip, distressClip, sprintClip);

        if (scpElevator != null)
        {
            configured += ConfigureScpElevatorDoors(scpElevator);
            configured += ConfigureScpElevatorKeypads(scpElevator, controller);
        }
        else
        {
            Debug.LogWarning("SCP-096 setup could not find SCP_Elevator.");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary = $"SCP-096 setup complete. Configured {configured} object(s).";
        Debug.Log(summary);
        if (showDialog)
            EditorUtility.DisplayDialog("SCP-096 Setup", summary, "OK");
    }

    private static int ConfigureScpController(
        SCP096Controller controller,
        Animator animator,
        Camera scpCamera,
        AnimationClip idleClip,
        AnimationClip distressClip,
        AnimationClip sprintClip)
    {
        return SetSerializedProperties(
            controller,
            ("animator", animator),
            ("scpCamera", scpCamera),
            ("idleClip", idleClip),
            ("distressClip", distressClip),
            ("sprintClip", sprintClip),
            ("idleStateName", "096_Idle"),
            ("distressStateName", "096_Distress"),
            ("sprintStateName", "096_Sprint"),
            ("cameraSwitchTime", 28f),
            ("chaseSpeed", 11f),
            ("chaseAcceleration", 80f),
            ("chaseAngularSpeed", 1080f),
            ("directChargeAssistSpeed", 4f),
            ("stuckVelocityThreshold", 0.35f),
            ("stuckAssistDelay", 0.45f),
            ("disableObstacleAvoidanceWhileChasing", true),
            ("introFreezeDuration", 1f),
            ("introScpCameraDuration", 2f),
            ("introPanicMessage", "WHAT THE... I NEED TO RUN!!"),
            ("introPanicMessageDuration", 4f)) ? 1 : 0;
    }

    private static int ConfigureScpElevatorKeypads(GameObject scpElevator, SCP096Controller controller)
    {
        int configured = 0;
        foreach (Transform child in scpElevator.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "ElevatorKeypad")
                continue;

            ElevatorKeypadController keypad = child.GetComponent<ElevatorKeypadController>();
            if (keypad == null)
            {
                keypad = Undo.AddComponent<ElevatorKeypadController>(child.gameObject);
                configured++;
            }

            if (SetSerializedProperties(
                    keypad,
                    ("cube004StartsScp096", true),
                    ("onlyCube004CanBePressed", true),
                    ("scp096Controller", controller)))
            {
                configured++;
            }
        }

        return configured;
    }

    private static int ConfigureScpElevatorDoors(GameObject scpElevator)
    {
        ElevatorDoorController doors = scpElevator.GetComponent<ElevatorDoorController>();
        if (doors == null)
            return 0;

        return SetSerializedProperties(doors, ("autoClose", false)) ? 1 : 0;
    }

    private static AnimatorController ConfigureAnimatorController(
        AnimationClip idleClip,
        AnimationClip distressClip,
        AnimationClip sprintClip)
    {
        EnsureControllerFolder();

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorState idleState = EnsureState(controller, "096_Idle", idleClip, true);
        EnsureState(controller, "096_Distress", distressClip, false);
        EnsureState(controller, "096_Sprint", sprintClip, false);

        controller.layers[0].stateMachine.defaultState = idleState;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorState EnsureState(AnimatorController controller, string stateName, Motion clip, bool defaultState)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = null;

        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                state = childState.state;
                break;
            }
        }

        if (state == null)
            state = stateMachine.AddState(stateName);

        state.motion = clip;

        if (defaultState)
            stateMachine.defaultState = state;

        return state;
    }

    private static AnimationClip FindClip(string clipName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
        {
            if (asset is AnimationClip clip && clip.name == clipName)
                return clip;
        }

        Debug.LogWarning($"Could not find SCP-096 animation clip '{clipName}'.");
        return null;
    }

    private static Camera FindChildCamera(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child.GetComponent<Camera>();
        }

        return null;
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

    private static void EnsureControllerFolder()
    {
        if (AssetDatabase.IsValidFolder(ControllerFolder))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Project"))
            AssetDatabase.CreateFolder("Assets", "Project");

        AssetDatabase.CreateFolder("Assets/Project", "AnimationControllers");
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
            else if (value is Object objectValue && property.objectReferenceValue != objectValue)
            {
                property.objectReferenceValue = objectValue;
                changed = true;
            }
            else if (value == null && property.propertyType == SerializedPropertyType.ObjectReference &&
                     property.objectReferenceValue != null)
            {
                property.objectReferenceValue = null;
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
