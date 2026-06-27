using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CollectionInventory : MonoBehaviour
{
    private class InventoryEntry
    {
        public string DisplayName;
        public string Id;
        public Sprite Icon;
        public Button Button;
    }

    public static CollectionInventory Instance { get; private set; }
    public static string SelectedItemId => Instance != null ? Instance._selectedItemId : null;
    public static bool IsInventoryOpen => Instance != null && Instance._inventoryPanel != null && Instance._inventoryPanel.activeSelf;

    private readonly List<InventoryEntry> _items = new List<InventoryEntry>();
    private readonly Color _crimson = new Color(0.72f, 0.035f, 0.055f);
    private readonly Color _pale = new Color(0.88f, 0.86f, 0.84f);
    private readonly Color _panel = new Color(0.025f, 0.025f, 0.028f, 0.96f);

    private GameObject _inventoryPanel;
    private Transform _slotGrid;
    private Text _selectedText;
    private Text _messageText;
    private CanvasGroup _messageGroup;
    private Text _blockingMessageText;
    private CanvasGroup _blockingMessageGroup;
    private Text _bottomMessageText;
    private CanvasGroup _bottomMessageGroup;
    private Font _font;
    private string _selectedItemId;
    private float _messageTimer;
    private float _bottomMessageTimer;
    private PlayerController _blockedPlayerController;
    private PlayerController _inventoryPlayerController;
    private bool _inventoryDisabledPlayerInput;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        new GameObject("CollectionInventory").AddComponent<CollectionInventory>();
    }

    public static void AddItem(string displayName, string id, Sprite icon = null)
    {
        EnsureExists();
        Instance.AddItemInternal(displayName, id, icon);
    }

    public static bool HasItem(string id)
    {
        return Instance != null && Instance._items.Exists(item => item.Id == id);
    }

    public static bool IsSelected(string id)
    {
        return Instance != null && Instance._selectedItemId == id;
    }

    public static bool SelectItemById(string id)
    {
        return Instance != null && Instance.SelectItemByIdInternal(id);
    }

    public static bool ConsumeItem(string id)
    {
        return Instance != null && Instance.RemoveItemInternal(id);
    }

    public static void ResetInventory()
    {
        if (Instance == null)
            return;

        CollectionInventory inventory = Instance;
        Instance = null;
        Destroy(inventory.gameObject);
    }

    public static void ShowMessage(string message, float duration = 3f)
    {
        EnsureExists();
        Instance.ShowMessageInternal(message, duration);
    }

    public static void ShowEscMessage(string message)
    {
        EnsureExists();
        Instance.ShowEscMessageInternal(message);
    }

    public static void ShowBottomMessage(string message, float duration = 3f)
    {
        EnsureExists();
        Instance.ShowBottomMessageInternal(message, duration);
    }

    public static void ForceCloseInventory()
    {
        if (Instance == null || Instance._inventoryPanel == null || !Instance._inventoryPanel.activeSelf)
            return;

        Instance.SetInventoryOpen(false, false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUi();
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (_blockingMessageGroup != null && _blockingMessageGroup.gameObject.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                HideEscMessage();

            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
            SetInventoryOpen(!_inventoryPanel.activeSelf);

        UpdateMessage();
        UpdateBottomMessage();
    }

    private void AddItemInternal(string displayName, string id, Sprite icon)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Item";

        if (string.IsNullOrWhiteSpace(id))
            id = displayName;

        if (_items.Exists(item => item.Id == id))
            return;

        InventoryEntry entry = new InventoryEntry
        {
            DisplayName = displayName,
            Id = id,
            Icon = icon
        };

        _items.Add(entry);
        CreateSlot(entry);
        ShowMessageInternal($"{displayName} has been collected.", 3f);
    }

    private void SetInventoryOpen(bool open, bool restorePlayerInput = true)
    {
        _inventoryPanel.SetActive(open);
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (open)
        {
            _inventoryPlayerController = FindFirstObjectByType<PlayerController>();
            _inventoryDisabledPlayerInput = _inventoryPlayerController != null && _inventoryPlayerController.enabled;

            if (_inventoryDisabledPlayerInput)
                _inventoryPlayerController.SetInputEnabled(false);
        }
        else
        {
            if (restorePlayerInput && _inventoryDisabledPlayerInput && _inventoryPlayerController != null)
                _inventoryPlayerController.SetInputEnabled(true);

            _inventoryPlayerController = null;
            _inventoryDisabledPlayerInput = false;
        }
    }

    private void SelectItem(InventoryEntry entry)
    {
        _selectedItemId = entry.Id;
        _selectedText.text = $"Selected: {entry.DisplayName}";

        foreach (InventoryEntry item in _items)
        {
            Image image = item.Button.GetComponent<Image>();
            image.color = item == entry ? _crimson : new Color(0.075f, 0.075f, 0.08f, 1f);
        }
    }

    private bool SelectItemByIdInternal(string id)
    {
        InventoryEntry entry = _items.Find(item => item.Id == id);
        if (entry == null)
            return false;

        SelectItem(entry);
        return true;
    }

    private bool RemoveItemInternal(string id)
    {
        InventoryEntry entry = _items.Find(item => item.Id == id);
        if (entry == null)
            return false;

        _items.Remove(entry);

        if (entry.Button != null)
            Destroy(entry.Button.gameObject);

        if (_selectedItemId == id)
        {
            _selectedItemId = null;
            _selectedText.text = "Selected: None";
        }

        return true;
    }

    private void ShowMessageInternal(string message, float duration)
    {
        _messageText.text = message;
        _messageTimer = duration;
        _messageGroup.alpha = 1f;
        _messageGroup.gameObject.SetActive(true);
    }

    private void ShowEscMessageInternal(string message)
    {
        _blockedPlayerController = FindFirstObjectByType<PlayerController>();
        if (_blockedPlayerController != null)
            _blockedPlayerController.SetInputEnabled(false);

        if (_inventoryPanel != null && _inventoryPanel.activeSelf)
            SetInventoryOpen(false, false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _blockingMessageText.text = message + "\n\nPress ESC to close.";
        _blockingMessageGroup.alpha = 1f;
        _blockingMessageGroup.gameObject.SetActive(true);
    }

    private void HideEscMessage()
    {
        _blockingMessageText.text = string.Empty;
        _blockingMessageGroup.alpha = 0f;
        _blockingMessageGroup.gameObject.SetActive(false);

        if (_blockedPlayerController != null)
            _blockedPlayerController.SetInputEnabled(true);

        _blockedPlayerController = null;
    }

    private void ShowBottomMessageInternal(string message, float duration)
    {
        _bottomMessageText.text = message;
        _bottomMessageTimer = duration;
        _bottomMessageGroup.alpha = 1f;
        _bottomMessageGroup.gameObject.SetActive(true);
    }

    private void UpdateMessage()
    {
        if (!_messageGroup.gameObject.activeSelf)
            return;

        _messageTimer -= Time.unscaledDeltaTime;
        if (_messageTimer > 0f)
            return;

        _messageGroup.alpha = Mathf.MoveTowards(_messageGroup.alpha, 0f, Time.unscaledDeltaTime * 2f);
        if (_messageGroup.alpha <= 0f)
        {
            _messageText.text = string.Empty;
            _messageGroup.gameObject.SetActive(false);
        }
    }

    private void UpdateBottomMessage()
    {
        if (!_bottomMessageGroup.gameObject.activeSelf)
            return;

        _bottomMessageTimer -= Time.unscaledDeltaTime;
        if (_bottomMessageTimer > 0f)
            return;

        _bottomMessageGroup.alpha = Mathf.MoveTowards(_bottomMessageGroup.alpha, 0f, Time.unscaledDeltaTime * 2f);
        if (_bottomMessageGroup.alpha <= 0f)
        {
            _bottomMessageText.text = string.Empty;
            _bottomMessageGroup.gameObject.SetActive(false);
        }
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = CreateRect("Inventory Canvas", transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildInventoryPanel(canvasObject.transform);
        BuildMessage(canvasObject.transform);
        BuildEscMessage(canvasObject.transform);
        BuildBottomMessage(canvasObject.transform);
    }

    private void BuildInventoryPanel(Transform parent)
    {
        _inventoryPanel = CreateRect("Inventory Panel", parent);
        Stretch(_inventoryPanel.GetComponent<RectTransform>());

        Image blocker = _inventoryPanel.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.78f);

        Image box = CreateImage(_inventoryPanel.transform, "Inventory Box", _panel);
        Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(980f, 680f));
        AddOutline(box.gameObject, _crimson, new Vector2(2f, -2f));

        Text title = CreateText(box.transform, "Title", "INVENTORY", 46, _crimson, TextAnchor.MiddleCenter);
        SetAnchors(title.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f));
        title.fontStyle = FontStyle.Bold;

        _selectedText = CreateText(box.transform, "Selected Item", "Selected: None", 22, _pale, TextAnchor.MiddleCenter);
        SetAnchors(_selectedText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.16f));

        GameObject gridObject = CreateRect("Slot Grid", box.transform);
        _slotGrid = gridObject.transform;
        SetAnchors(gridObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.78f));

        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(120f, 120f);
        grid.spacing = new Vector2(18f, 18f);
        grid.childAlignment = TextAnchor.UpperCenter;

        _inventoryPanel.SetActive(false);
    }

    private void BuildMessage(Transform parent)
    {
        GameObject messageObject = CreateRect("Collection Message", parent);
        SetAnchors(messageObject.GetComponent<RectTransform>(), new Vector2(0.20f, 0.72f), new Vector2(0.80f, 0.86f));

        _messageGroup = messageObject.AddComponent<CanvasGroup>();
        Image image = messageObject.AddComponent<Image>();
        image.color = new Color(0.015f, 0.015f, 0.018f, 0.88f);
        AddOutline(messageObject, _crimson, new Vector2(1f, -1f));

        _messageText = CreateText(messageObject.transform, "Text", string.Empty, 28, _pale, TextAnchor.MiddleCenter);
        Stretch(_messageText.rectTransform);
        _messageText.horizontalOverflow = HorizontalWrapMode.Wrap;

        messageObject.SetActive(false);
    }

    private void BuildEscMessage(Transform parent)
    {
        GameObject messageObject = CreateRect("Important Message", parent);
        SetAnchors(messageObject.GetComponent<RectTransform>(), new Vector2(0.22f, 0.38f), new Vector2(0.78f, 0.62f));

        _blockingMessageGroup = messageObject.AddComponent<CanvasGroup>();
        Image image = messageObject.AddComponent<Image>();
        image.color = new Color(0.015f, 0.015f, 0.018f, 0.94f);
        AddOutline(messageObject, _crimson, new Vector2(2f, -2f));

        _blockingMessageText = CreateText(messageObject.transform, "Text", string.Empty, 28, _pale, TextAnchor.MiddleCenter);
        Stretch(_blockingMessageText.rectTransform);
        _blockingMessageText.rectTransform.offsetMin = new Vector2(40f, 30f);
        _blockingMessageText.rectTransform.offsetMax = new Vector2(-40f, -30f);
        _blockingMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _blockingMessageText.verticalOverflow = VerticalWrapMode.Overflow;

        messageObject.SetActive(false);
    }

    private void BuildBottomMessage(Transform parent)
    {
        GameObject messageObject = CreateRect("Bottom Interaction Message", parent);
        SetAnchors(messageObject.GetComponent<RectTransform>(), new Vector2(0.25f, 0.10f), new Vector2(0.75f, 0.18f));

        _bottomMessageGroup = messageObject.AddComponent<CanvasGroup>();
        Image image = messageObject.AddComponent<Image>();
        image.color = new Color(0.015f, 0.015f, 0.018f, 0.72f);
        AddOutline(messageObject, _crimson, new Vector2(1f, -1f));

        _bottomMessageText = CreateText(messageObject.transform, "Text", string.Empty, 20, _pale, TextAnchor.MiddleCenter);
        Stretch(_bottomMessageText.rectTransform);
        _bottomMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;

        messageObject.SetActive(false);
    }

    private void CreateSlot(InventoryEntry entry)
    {
        GameObject slot = CreateRect(entry.DisplayName + " Slot", _slotGrid);
        Image background = slot.AddComponent<Image>();
        background.color = new Color(0.075f, 0.075f, 0.08f, 1f);
        AddOutline(slot, new Color(0.30f, 0.30f, 0.30f), new Vector2(1f, -1f));

        Button button = slot.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => SelectItem(entry));
        entry.Button = button;

        Image icon = CreateImage(slot.transform, "Icon", Color.white);
        Place(icon.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(74f, 74f));
        icon.sprite = entry.Icon;
        icon.enabled = entry.Icon != null;
        icon.preserveAspect = true;

        Text label = CreateText(slot.transform, "Label", entry.DisplayName, 14, _pale, TextAnchor.MiddleCenter);
        SetAnchors(label.rectTransform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.30f));
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private Text CreateText(Transform parent, string objectName, string value, int size, Color color, TextAnchor alignment)
    {
        GameObject textObject = CreateRect(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        return text;
    }

    private static Image CreateImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = CreateRect(objectName, parent);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static GameObject CreateRect(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
