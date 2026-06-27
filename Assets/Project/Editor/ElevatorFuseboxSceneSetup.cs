using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ElevatorFuseboxSceneSetup
{
    private const string MenuPath = "CrimsonNightfall/Set Up Fusebox And Elevators";
    private const string SecretElevatorGreenMaterialPath =
        "Assets/ThirdParty/Industrial Props/Materials 1/Green.mat";

    [MenuItem(MenuPath)]
    private static void RunFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        int configured = 0;

        Undo.SetCurrentGroupName("Set Up Fusebox And Elevators");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject itemsRoot = FindByName(scene, "Items");
        if (itemsRoot != null)
        {
            configured += ConfigureItem(itemsRoot, "Wrench", "Wrench");
            configured += ConfigureItem(itemsRoot, "Fuse_1", "Fuse_1");
            configured += ConfigureItem(itemsRoot, "Fuse_2", "Fuse_2");
            configured += ConfigureSecretElevatorKey(itemsRoot);

            GameObject fusebox = FindChildByName(itemsRoot.transform, "Fusebox");
            if (fusebox != null)
            {
                if (fusebox.GetComponent<FuseboxController>() == null)
                {
                    Undo.AddComponent<FuseboxController>(fusebox);
                    configured++;
                }

                configured += EnsurePointLight(fusebox.transform, "Fuse1_Light");
                configured += EnsurePointLight(fusebox.transform, "Fuse2_Light");
            }
        }

        GameObject elevatorsRoot = FindByName(scene, "Elevators");
        if (elevatorsRoot != null)
        {
            GameObject basementTarget = FindByName(scene, "Basement_Elevator");
            GameObject mainTarget = FindByName(scene, "Main_Elevator");
            GameObject upperTarget = FindByName(scene, "Upper_Elevator");
            GameObject secretTarget = FindByName(scene, "Secret_Elevator");
            GameObject scpTarget = FindByName(scene, "SCP_Elevator");
            GameObject exitElevator = FindByName(scene, "Exit_Elevator");
            GameObject basementAnchor = FindByNameStartingWith(scene, "Basement_Elevator_PlayerAnchor");
            GameObject mainAnchor = FindByNameStartingWith(scene, "Main_Elevator_PlayerAnchor");
            GameObject upperAnchor = FindByNameStartingWith(scene, "Upper_Elevator_PlayerAnchor");
            GameObject secretAnchor = FindByNameStartingWith(scene, "Secret_Elevator_PlayerAnchor");
            GameObject exitAnchor = FindByNameStartingWith(scene, "Exit_Elevator_PlayerAnchor");
            GameObject scpAnchor = FindByNameStartingWith(scene, "SCP_Elevator_PlayerAnchor");

            foreach (Transform elevator in elevatorsRoot.transform)
            {
                configured += ConfigureElevator(
                    elevator.gameObject,
                    basementTarget,
                    mainTarget,
                    upperTarget,
                    secretTarget,
                    scpTarget,
                    basementAnchor,
                    mainAnchor,
                    upperAnchor,
                    secretAnchor,
                    exitAnchor,
                    scpAnchor);
            }

            if (exitElevator != null && exitElevator.transform.parent != elevatorsRoot.transform)
                configured += ConfigureElevator(
                    exitElevator,
                    basementTarget,
                    mainTarget,
                    upperTarget,
                    secretTarget,
                    scpTarget,
                    basementAnchor,
                    mainAnchor,
                    upperAnchor,
                    secretAnchor,
                    exitAnchor,
                    scpAnchor);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string summary = $"Fusebox/elevator setup complete. Configured {configured} object(s).";
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Fusebox And Elevators", summary, "OK");
    }

    private static int ConfigureItem(GameObject root, string objectName, string itemId)
    {
        GameObject item = FindChildByName(root.transform, objectName);
        if (item == null)
            return 0;

        ItemPickup pickup = item.GetComponent<ItemPickup>();
        if (pickup == null)
            pickup = Undo.AddComponent<ItemPickup>(item);

        SetSerializedProperties(pickup, ("itemName", itemId), ("addToInventory", true));
        return 1;
    }

    private static int ConfigureSecretElevatorKey(GameObject root)
    {
        GameObject item = FindChildByName(root.transform, "Secret_Elevator_Key");
        if (item == null)
            return 0;

        ItemPickup pickup = item.GetComponent<ItemPickup>();
        if (pickup == null)
            pickup = Undo.AddComponent<ItemPickup>(item);

        return SetSerializedProperties(
            pickup,
            ("itemName", "Secret Elevator Key"),
            ("grantsKey", true),
            ("keyId", "ElevatorKey"),
            ("addToInventory", true),
            ("collectionMessage", "I wonder how I can use this key for the elevator..."),
            ("collectionMessageRequiresEsc", true),
            ("showCollectionMessageAtBottom", false)) ? 1 : 0;
    }

    private static int ConfigureSecretElevatorAccess(GameObject buttonPanel)
    {
        SecretElevatorKeyAccess access = buttonPanel.GetComponent<SecretElevatorKeyAccess>();
        if (access == null)
            access = Undo.AddComponent<SecretElevatorKeyAccess>(buttonPanel);

        Material greenMaterial = AssetDatabase.LoadAssetAtPath<Material>(SecretElevatorGreenMaterialPath);
        bool changed = SetSerializedProperties(
            access,
            ("requiredItemId", "ElevatorKey"),
            ("consumeKeyOnUse", false),
            ("insertedMessage", "You have inserted the Secret Key..."),
            ("indicatorChildName", "Text.005"),
            ("unlockedMaterial", greenMaterial));

        return changed ? 1 : 0;
    }

    private static int EnsurePointLight(Transform root, string childName)
    {
        GameObject child = FindChildByName(root, childName);
        if (child == null || child.GetComponent<Light>() != null)
            return 0;

        Light light = Undo.AddComponent<Light>(child);
        light.type = LightType.Point;
        light.range = 3f;
        light.intensity = 8f;
        light.color = Color.red;
        EditorUtility.SetDirty(light);
        return 1;
    }

    private static int EnsureCollider(GameObject gameObject)
    {
        if (gameObject.GetComponent<Collider>() != null)
            return 0;

        Undo.AddComponent<BoxCollider>(gameObject);
        return 1;
    }

    private static int ConfigureKeypad(
        ElevatorKeypadController keypad,
        string elevatorName,
        ElevatorDoorController doors,
        GameObject basementTarget,
        GameObject mainTarget,
        GameObject upperTarget,
        GameObject secretTarget,
        GameObject scpTarget,
        GameObject basementAnchor,
        GameObject mainAnchor,
        GameObject upperAnchor,
        GameObject secretAnchor,
        GameObject exitAnchor,
        GameObject scpAnchor)
    {
        int floor = 2;
        string lowerName = elevatorName.ToLowerInvariant();

        if (lowerName.Contains("basement"))
            floor = 1;
        else if (lowerName.Contains("upper"))
            floor = 3;
        else if (lowerName.Contains("secret"))
            floor = 0;
        else if (lowerName.Contains("exit") || lowerName.Contains("scp"))
            floor = 5;

        return SetSerializedProperties(
            keypad,
            ("currentFloor", floor),
            ("elevatorDoors", doors),
            ("basementElevator", basementTarget != null ? basementTarget.transform : null),
            ("mainElevator", mainTarget != null ? mainTarget.transform : null),
            ("upperElevator", upperTarget != null ? upperTarget.transform : null),
            ("secretElevator", secretTarget != null ? secretTarget.transform : null),
            ("secretElevatorName", "Secret_Elevator"),
            ("scpElevator", scpTarget != null ? scpTarget.transform : null),
            ("scpElevatorName", "SCP_Elevator"),
            ("basementPlayerAnchor", basementAnchor != null ? basementAnchor.transform : null),
            ("mainPlayerAnchor", mainAnchor != null ? mainAnchor.transform : null),
            ("upperPlayerAnchor", upperAnchor != null ? upperAnchor.transform : null),
            ("secretPlayerAnchor", secretAnchor != null ? secretAnchor.transform : null),
            ("exitPlayerAnchor", exitAnchor != null ? exitAnchor.transform : null),
            ("scpPlayerAnchor", scpAnchor != null ? scpAnchor.transform : null),
            ("centerPlayerDuration", 0.35f),
            ("floorAnchorDetectionRadius", 5f),
            ("cube005ReturnsToSecretElevatorAndMarksScpSurvived", lowerName.Contains("exit")),
            ("onlyCube005CanBePressed", lowerName.Contains("exit")),
            ("exitReturnFadeDuration", 1f),
            ("exitReturnStartupDelay", 3f),
            ("exitReturnBlackoutDuration", 5f),
            ("rockyCharmsItemId", "RockyCharms"),
            ("scpBlackoutDuration", 5f),
            ("scpAftermathMessage", "What just happened... I think the elevator malfunctioned... Where am I...")) ? 1 : 0;
    }

    private static int ConfigureElevator(
        GameObject elevator,
        GameObject basementTarget,
        GameObject mainTarget,
        GameObject upperTarget,
        GameObject secretTarget,
        GameObject scpTarget,
        GameObject basementAnchor,
        GameObject mainAnchor,
        GameObject upperAnchor,
        GameObject secretAnchor,
        GameObject exitAnchor,
        GameObject scpAnchor)
    {
        int configured = 0;
        ElevatorDoorController doors = elevator.GetComponent<ElevatorDoorController>();
        if (doors == null)
        {
            doors = Undo.AddComponent<ElevatorDoorController>(elevator);
            configured++;
        }

        foreach (Transform child in elevator.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("Elevator_Buttons") || child.name.StartsWith("Elevators_Buttons"))
            {
                if (child.GetComponent<ElevatorButtonController>() == null)
                {
                    Undo.AddComponent<ElevatorButtonController>(child.gameObject);
                    configured++;
                }

                configured += ConfigureElevatorButton(child.gameObject);
                configured += ConfigureSecretElevatorAccess(child.gameObject);
                configured += EnsureCollider(child.gameObject);
            }

            if (child.name == "ElevatorKeypad")
            {
                ElevatorKeypadController keypad = child.GetComponent<ElevatorKeypadController>();
                if (keypad == null)
                {
                    keypad = Undo.AddComponent<ElevatorKeypadController>(child.gameObject);
                    configured++;
                }

                configured += EnsureKeypadButtonColliders(keypad.transform);
                configured += ConfigureKeypad(
                    keypad,
                    elevator.name,
                    doors,
                    basementTarget,
                    mainTarget,
                    upperTarget,
                    secretTarget,
                    scpTarget,
                    basementAnchor,
                    mainAnchor,
                    upperAnchor,
                    secretAnchor,
                    exitAnchor,
                    scpAnchor);
            }
        }

        return configured;
    }

    private static int ConfigureElevatorButton(GameObject buttonPanel)
    {
        ElevatorButtonController button = buttonPanel.GetComponent<ElevatorButtonController>();
        if (button == null)
            return 0;

        return SetSerializedProperties(
            button,
            ("interactionRange", 4.5f),
            ("interactionAngle", 75f),
            ("requireLineOfSight", false),
            ("showPrompt", true),
            ("promptMessage", "Press E to use elevator")) ? 1 : 0;
    }

    private static int EnsureKeypadButtonColliders(Transform keypad)
    {
        int added = 0;
        added += EnsureNamedChildCollider(keypad, "Cube.001");
        added += EnsureNamedChildCollider(keypad, "Cube.002");
        added += EnsureNamedChildCollider(keypad, "Cube.003");
        added += EnsureNamedChildCollider(keypad, "Cube.004");
        added += EnsureNamedChildCollider(keypad, "Cube.005");
        return added;
    }

    private static int EnsureNamedChildCollider(Transform root, string childName)
    {
        GameObject child = FindChildByName(root, childName);
        return child != null ? EnsureCollider(child) : 0;
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

    private static GameObject FindByNameStartingWith(Scene scene, string objectNamePrefix)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith(objectNamePrefix))
                    return child.gameObject;
            }
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
            else if (value is int intValue && property.intValue != intValue)
            {
                property.intValue = intValue;
                changed = true;
            }
            else if (value is string stringValue && property.stringValue != stringValue)
            {
                property.stringValue = stringValue;
                changed = true;
            }
            else if (value is float floatValue && !Mathf.Approximately(property.floatValue, floatValue))
            {
                property.floatValue = floatValue;
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
