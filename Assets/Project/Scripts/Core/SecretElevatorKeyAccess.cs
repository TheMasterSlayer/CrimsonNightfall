using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SecretElevatorKeyAccess : MonoBehaviour
{
    public static bool IsUnlocked { get; private set; }

    [Header("Key")]
    [SerializeField] private string requiredItemId = "ElevatorKey";
    [SerializeField] private bool consumeKeyOnUse;

    [Header("Messages")]
    [SerializeField] private string insertedMessage = "You have inserted the Secret Key...";
    [SerializeField] private string wrongItemMessage = "It looks like this elevator needs something else.";

    [Header("Indicator")]
    [SerializeField] private string indicatorChildName = "Text.005";
    [SerializeField] private Material unlockedMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsUnlocked = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyIndicators();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public bool TryUnlockFromSelectedItem()
    {
        if (IsUnlocked)
        {
            ApplyIndicators();
            return false;
        }

        if (!CollectionInventory.HasItem(requiredItemId) || !CollectionInventory.IsSelected(requiredItemId))
        {
            CollectionInventory.ShowBottomMessage(wrongItemMessage);
            return false;
        }

        IsUnlocked = true;

        if (consumeKeyOnUse)
            CollectionInventory.ConsumeItem(requiredItemId);

        CollectionInventory.ShowBottomMessage(insertedMessage, 3f);
        ApplyIndicators();
        return true;
    }

    public static void ApplyIndicators()
    {
        if (!IsUnlocked)
            return;

        Material material = null;
        foreach (SecretElevatorKeyAccess access in FindObjectsByType<SecretElevatorKeyAccess>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (material == null && access.unlockedMaterial != null)
                material = access.unlockedMaterial;
        }

        if (material == null)
            return;

        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!transform.gameObject.scene.IsValid() || transform.name != "Text.005")
                continue;

            if (IsUnderNamedParent(transform, "SCP_Elevator"))
                continue;

            ApplyMaterialToRenderers(transform, material);
        }
    }

    private void ApplyIndicator()
    {
        if (unlockedMaterial == null)
            return;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != indicatorChildName)
                continue;

            ApplyMaterialToRenderers(child, unlockedMaterial);
        }
    }

    private static void ApplyMaterialToRenderers(Transform root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.material = material;
    }

    private static bool IsUnderNamedParent(Transform transform, string parentName)
    {
        while (transform != null)
        {
            if (transform.name == parentName)
                return true;

            transform = transform.parent;
        }

        return false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyIndicators();
    }
}
