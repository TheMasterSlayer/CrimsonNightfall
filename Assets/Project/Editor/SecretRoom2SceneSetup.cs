using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SecretRoom2SceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up Secret Room 2 NPCs";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaxwellFbxPath = "Assets/ThirdParty/maxwell/source/chouCatM.fbx";
    private const string WitchFbxPath = "Assets/ThirdParty/elaina/source/Witch.fbx";
    private const string ControllerFolder = "Assets/Project/AnimationControllers";

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
        Undo.SetCurrentGroupName("Set Up Secret Room 2 NPCs");
        int undoGroup = Undo.GetCurrentGroup();

        int configured = 0;
        GameObject secretRoom = FindByName(scene, "Secret_Room_2");
        GameObject maxwell = secretRoom != null
            ? FindDirectOrNestedChild(secretRoom.transform, "Maxwell")
            : FindByName(scene, "Maxwell");
        GameObject witch = secretRoom != null
            ? FindDirectOrNestedChild(secretRoom.transform, "Witch")
            : FindByName(scene, "Witch");
        GameObject rockyCharms = FindByName(scene, "Rocky_Charms");
        GameObject rockyCharms2 = secretRoom != null
            ? FindDirectOrNestedChild(secretRoom.transform, "Rocky_Charms_2")
            : FindByName(scene, "Rocky_Charms_2");
        GameObject secretNote2 = FindByName(scene, "Secret_Note_2");
        ItemPickup rockyCharmsPickup = null;

        if (rockyCharms != null)
        {
            configured += ConfigureRockyCharms(rockyCharms, out rockyCharmsPickup);
        }
        else
        {
            Debug.LogWarning("Secret Room 2 setup could not find Rocky_Charms.");
        }

        if (secretNote2 != null)
            configured += ConfigureSecretNote2(secretNote2);
        else
            Debug.LogWarning("Secret Room 2 setup could not find Secret_Note_2.");

        if (rockyCharms2 != null)
            configured += HideObjectUntilMaxwellReward(rockyCharms2, "Hide Rocky Charms 2 Until Maxwell Reward");
        else
            Debug.LogWarning("Secret Room 2 setup could not find Rocky_Charms_2.");

        AnimationClip maxwellClip = FindClip(MaxwellFbxPath, "Take 001");
        AnimationClip witchClip = FindClip(WitchFbxPath, "Action");

        if (maxwell != null)
        {
            configured += ConfigureAnimator(maxwell, "Maxwell", maxwellClip, "Maxwell.controller");
            configured += ConfigureDialogue(
                maxwell,
                "Maxwell",
                new[]
                {
                    "Meow meow. I want some food. There is a box of Rocky Charms in the dining room I want. If you find it and give it to me, I will give you a secret note I found.",
                    "Get me Rocky Charms. I'm hungry."
                },
                new[]
                {
                    "Wait, how do I understand you...?",
                    "Okay."
                },
                rockyCharmsPickup,
                BuildRewardObjects(secretNote2, rockyCharms2),
                maxwell.GetComponent<Animator>());
            configured += EnsureCollider(maxwell);
        }
        else
        {
            Debug.LogWarning("Secret Room 2 setup could not find Maxwell.");
        }

        if (witch != null)
        {
            configured += ConfigureAnimator(witch, "Witch", witchClip, "Witch.controller");
            configured += ConfigureDialogue(
                witch,
                "Witch",
                new[]
                {
                    "We have never seen another human here before... it seems you have done well surviving so far...",
                    "Feel free to stay down here with us as long as needed. There is only peace and quiet here."
                },
                new[]
                {
                    "It is much more cozy in here than up there.",
                    "Okay."
                },
                null,
                null,
                null);
            configured += EnsureCollider(witch);
        }
        else
        {
            Debug.LogWarning("Secret Room 2 setup could not find Witch.");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary = $"Secret Room 2 NPC setup complete. Configured {configured} object(s).";
        Debug.Log(summary);
        if (showDialog)
            EditorUtility.DisplayDialog("Secret Room 2 NPCs", summary, "OK");
    }

    private static int ConfigureAnimator(
        GameObject target,
        string stateName,
        Motion clip,
        string controllerFileName)
    {
        if (clip == null)
        {
            Debug.LogWarning($"No animation clip found for {target.name}; dialogue setup will still work.");
            return 0;
        }

        EnsureControllerFolder();
        string controllerPath = $"{ControllerFolder}/{controllerFileName}";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        bool configured = false;

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            configured = true;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = FindState(stateMachine, stateName);
        if (state == null)
        {
            state = stateMachine.AddState(stateName);
            configured = true;
        }

        if (state.motion != clip)
        {
            state.motion = clip;
            configured = true;
        }

        if (stateMachine.defaultState != state)
        {
            stateMachine.defaultState = state;
            configured = true;
        }

        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(target);
            configured = true;
        }

        if (animator.runtimeAnimatorController != controller)
        {
            Undo.RecordObject(animator, "Assign NPC Animator Controller");
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            configured = true;
        }

        if (configured)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        return configured ? 1 : 0;
    }

    private static int ConfigureDialogue(
        GameObject target,
        string speaker,
        string[] npcLines,
        string[] responseTexts,
        Behaviour enableOnComplete,
        GameObject[] activateAfterScpDialogue,
        Behaviour disableAfterScpDialogue)
    {
        NPCDialogueController controller = target.GetComponent<NPCDialogueController>();
        bool configured = false;

        if (controller == null)
        {
            controller = Undo.AddComponent<NPCDialogueController>(target);
            configured = true;
        }

        SerializedObject serializedObject = new SerializedObject(controller);
        configured |= SetString(serializedObject, "speakerName", speaker);
        configured |= SetString(serializedObject, "promptMessage", "Press E to talk.");
        configured |= SetStringArray(serializedObject, "npcLines", npcLines);
        configured |= SetStringArray(serializedObject, "responseTexts", responseTexts);
        configured |= SetObjectReferenceArray(
            serializedObject,
            "enableBehavioursOnComplete",
            enableOnComplete != null ? new Object[] { enableOnComplete } : new Object[0]);
        configured |= SetBool(serializedObject, "useScpSurvivalDialogue", activateAfterScpDialogue != null);
        configured |= SetStringArray(
            serializedObject,
            "scpSurvivedNpcLines",
            activateAfterScpDialogue != null
                ? new[]
                {
                    "Meow. You took forever. You better have my RockyCharms.",
                    "Meow. That can happen here. This secret note actually explains more about that. I placed it on the floor. Thanks for the RockyCharms. Meow."
                }
                : new string[0]);
        configured |= SetStringArray(
            serializedObject,
            "scpSurvivedResponseTexts",
            activateAfterScpDialogue != null
                ? new[] { "I was sent to a place I shouldn't have been!", "Okay..." }
                : new string[0]);
        configured |= SetStringArray(
            serializedObject,
            "scpRepeatNpcLines",
            activateAfterScpDialogue != null
                ? new[] { "Meow. Munch Munch. Yummy." }
                : new string[0]);
        configured |= SetStringArray(
            serializedObject,
            "scpRepeatResponseTexts",
            activateAfterScpDialogue != null
                ? new[] { "..." }
                : new string[0]);
        configured |= SetObjectReferenceArray(
            serializedObject,
            "activateObjectsAfterScpDialogue",
            activateAfterScpDialogue != null ? activateAfterScpDialogue : new Object[0]);
        configured |= SetObjectReferenceArray(
            serializedObject,
            "disableBehavioursAfterScpDialogue",
            disableAfterScpDialogue != null ? new Object[] { disableAfterScpDialogue } : new Object[0]);
        configured |= SetStringArray(
            serializedObject,
            "consumeInventoryIdsAfterScpDialogue",
            activateAfterScpDialogue != null ? new[] { "RockyCharms" } : new string[0]);

        if (!configured)
            return 0;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        return 1;
    }

    private static int ConfigureSecretNote2(GameObject secretNote)
    {
        int configured = 0;

        ReadablePaper paper = secretNote.GetComponent<ReadablePaper>();
        if (paper == null)
        {
            paper = Undo.AddComponent<ReadablePaper>(secretNote);
            configured++;
        }

        SerializedObject serializedObject = new SerializedObject(paper);
        bool changed = false;
        changed |= SetString(serializedObject, "promptMessage", "Press E to read.");
        changed |= SetStringIfEmpty(serializedObject, "paperDescription", "Write Secret Note 2 text here.");

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(paper);
            configured++;
        }

        if (secretNote.activeSelf)
        {
            Undo.RecordObject(secretNote, "Hide Secret Note 2 Until Maxwell Dialogue");
            secretNote.SetActive(false);
            EditorUtility.SetDirty(secretNote);
            configured++;
        }

        return configured;
    }

    private static GameObject[] BuildRewardObjects(params GameObject[] objects)
    {
        int count = 0;
        foreach (GameObject target in objects)
        {
            if (target != null)
                count++;
        }

        if (count == 0)
            return null;

        GameObject[] result = new GameObject[count];
        int index = 0;
        foreach (GameObject target in objects)
        {
            if (target != null)
                result[index++] = target;
        }

        return result;
    }

    private static int HideObjectUntilMaxwellReward(GameObject target, string undoName)
    {
        if (target == null || !target.activeSelf)
            return 0;

        Undo.RecordObject(target, undoName);
        target.SetActive(false);
        EditorUtility.SetDirty(target);
        return 1;
    }

    private static int ConfigureRockyCharms(GameObject rockyCharms, out ItemPickup pickup)
    {
        int configured = 0;
        pickup = rockyCharms.GetComponent<ItemPickup>();
        if (pickup == null)
        {
            pickup = Undo.AddComponent<ItemPickup>(rockyCharms);
            configured++;
        }

        SerializedObject serializedObject = new SerializedObject(pickup);
        bool changed = false;
        changed |= SetString(serializedObject, "itemName", "Rocky Charms");
        changed |= SetBool(serializedObject, "grantsKey", true);
        changed |= SetString(serializedObject, "keyId", "RockyCharms");
        changed |= SetBool(serializedObject, "addToInventory", true);
        changed |= SetString(serializedObject, "collectionMessage", "Rocky Charms has been collected.");

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(pickup);
            configured++;
        }

        if (pickup.enabled)
        {
            Undo.RecordObject(pickup, "Disable Rocky Charms Pickup Until Maxwell Dialogue");
            pickup.enabled = false;
            EditorUtility.SetDirty(pickup);
            configured++;
        }

        return configured;
    }

    private static int EnsureCollider(GameObject target)
    {
        if (target.GetComponentInChildren<Collider>() != null)
            return 0;

        BoxCollider collider = Undo.AddComponent<BoxCollider>(target);
        collider.isTrigger = false;
        collider.center = Vector3.up;
        collider.size = new Vector3(1f, 2f, 1f);
        EditorUtility.SetDirty(collider);
        return 1;
    }

    private static AnimationClip FindClip(string assetPath, string clipName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is AnimationClip clip && clip.name == clipName)
                return clip;
        }

        Debug.LogWarning($"Could not find animation clip '{clipName}' in {assetPath}.");
        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
                return childState.state;
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

    private static bool SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.stringValue == value)
            return false;

        property.stringValue = value;
        return true;
    }

    private static bool SetStringIfEmpty(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !string.IsNullOrWhiteSpace(property.stringValue))
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

    private static bool SetStringArray(SerializedObject serializedObject, string propertyName, string[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return false;

        values ??= new string[0];
        bool changed = property.arraySize != values.Length;
        if (property.arraySize != values.Length)
            property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element.stringValue == values[i])
                continue;

            element.stringValue = values[i];
            changed = true;
        }

        return changed;
    }

    private static bool SetObjectReferenceArray(
        SerializedObject serializedObject,
        string propertyName,
        Object[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return false;

        values ??= new Object[0];
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

    private static GameObject FindByName(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject result = FindDirectOrNestedChild(root.transform, objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameObject FindDirectOrNestedChild(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child.gameObject;
        }

        return null;
    }
}
