using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlaytestInventoryGrant : MonoBehaviour
{
    public enum PlaytestItemPreset
    {
        Custom,
        Wrench,
        Fuse1,
        Fuse2,
        MasterBedroomKey,
        HiddenStudyKey,
        WineCellarKey,
        LibraryKey,
        Crowbar,
        ImageClue,
        ExitKey,
        ElevatorKey,
        RockyCharms
    }

    [System.Serializable]
    public class PlaytestItem
    {
        public PlaytestItemPreset preset = PlaytestItemPreset.Custom;
        public string customDisplayName = "Test Item";
        public string customItemId = "TestItem";
        public Sprite icon;
        public bool alsoAddToGameManagerKeys = true;
    }

    [Header("Playtest Grants")]
    [SerializeField] private bool grantOnStart = true;
    [SerializeField] private bool showGrantMessage = true;
    [SerializeField] private List<PlaytestItem> itemsToGrant = new List<PlaytestItem>();

    [Header("Optional Auto Select")]
    [SerializeField] private bool selectItemAfterGrant;
    [SerializeField] private PlaytestItemPreset itemToSelect = PlaytestItemPreset.Custom;
    [SerializeField] private string customItemIdToSelect;

    private IEnumerator Start()
    {
        if (!grantOnStart)
            yield break;

        yield return null;
        GrantItems();
    }

    [ContextMenu("Grant Items Now")]
    public void GrantItems()
    {
        CollectionInventory.EnsureExists();

        int granted = 0;
        foreach (PlaytestItem item in itemsToGrant)
        {
            GetPresetValues(item, out string displayName, out string itemId);
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            CollectionInventory.AddItem(displayName, itemId, item.icon);

            if (item.alsoAddToGameManagerKeys && GameManager.Instance != null)
                GameManager.Instance.AddKey(itemId);

            granted++;
        }

        if (selectItemAfterGrant)
        {
            string selectedId = itemToSelect == PlaytestItemPreset.Custom
                ? customItemIdToSelect
                : GetPresetItemId(itemToSelect);

            if (!string.IsNullOrWhiteSpace(selectedId))
                CollectionInventory.SelectItemById(selectedId);
        }

        if (showGrantMessage && granted > 0)
            CollectionInventory.ShowBottomMessage($"Playtest granted {granted} item(s).", 2f);
    }

    private static void GetPresetValues(PlaytestItem item, out string displayName, out string itemId)
    {
        if (item.preset == PlaytestItemPreset.Custom)
        {
            displayName = string.IsNullOrWhiteSpace(item.customDisplayName)
                ? item.customItemId
                : item.customDisplayName;
            itemId = item.customItemId;
            return;
        }

        displayName = GetPresetDisplayName(item.preset);
        itemId = GetPresetItemId(item.preset);
    }

    private static string GetPresetDisplayName(PlaytestItemPreset preset)
    {
        switch (preset)
        {
            case PlaytestItemPreset.Wrench:
                return "Wrench";
            case PlaytestItemPreset.Fuse1:
                return "Fuse 1";
            case PlaytestItemPreset.Fuse2:
                return "Fuse 2";
            case PlaytestItemPreset.MasterBedroomKey:
                return "Master Bedroom Key";
            case PlaytestItemPreset.HiddenStudyKey:
                return "Hidden Study Key";
            case PlaytestItemPreset.WineCellarKey:
                return "Wine Cellar Key";
            case PlaytestItemPreset.LibraryKey:
                return "Library Key";
            case PlaytestItemPreset.Crowbar:
                return "Crowbar";
            case PlaytestItemPreset.ImageClue:
                return "Image Clue";
            case PlaytestItemPreset.ExitKey:
                return "Exit Key";
            case PlaytestItemPreset.ElevatorKey:
                return "Secret Elevator Key";
            case PlaytestItemPreset.RockyCharms:
                return "Rocky Charms";
            default:
                return "Test Item";
        }
    }

    private static string GetPresetItemId(PlaytestItemPreset preset)
    {
        switch (preset)
        {
            case PlaytestItemPreset.Wrench:
                return "Wrench";
            case PlaytestItemPreset.Fuse1:
                return "Fuse_1";
            case PlaytestItemPreset.Fuse2:
                return "Fuse_2";
            case PlaytestItemPreset.MasterBedroomKey:
                return "MasterBedroomKey";
            case PlaytestItemPreset.HiddenStudyKey:
                return "HiddenStudyKey";
            case PlaytestItemPreset.WineCellarKey:
                return "WineCellarKey";
            case PlaytestItemPreset.LibraryKey:
                return "LibraryKey";
            case PlaytestItemPreset.Crowbar:
                return "Crowbar";
            case PlaytestItemPreset.ImageClue:
                return "ImageClue";
            case PlaytestItemPreset.ExitKey:
                return "ExitKey";
            case PlaytestItemPreset.ElevatorKey:
                return "ElevatorKey";
            case PlaytestItemPreset.RockyCharms:
                return "RockyCharms";
            default:
                return string.Empty;
        }
    }
}
