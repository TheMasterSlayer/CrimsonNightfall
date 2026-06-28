using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGamePauseMenu : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";

    private readonly Color _crimson = new Color(0.72f, 0.035f, 0.055f);
    private readonly Color _pale = new Color(0.88f, 0.86f, 0.84f);
    private readonly Color _dim = new Color(0.40f, 0.40f, 0.40f);
    private readonly Color _panel = new Color(0.025f, 0.025f, 0.028f, 0.96f);

    private GameObject _pausePanel;
    private GameObject _mainPage;
    private GameObject _settingsPage;
    private PlayerController _playerController;
    private Font _font;
    private bool _isPaused;

    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUi();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            return;

        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (_isPaused)
            Resume();
        else if (CanOpenPauseMenu())
            Pause();
    }

    private bool CanOpenPauseMenu()
    {
        if (ItemPickup.IsAnyItemInspecting)
            return false;

        _playerController = FindFirstObjectByType<PlayerController>();
        return _playerController == null || _playerController.enabled || CollectionInventory.IsInventoryOpen;
    }

    private void Pause()
    {
        _isPaused = true;
        Time.timeScale = 0f;

        CollectionInventory.ForceCloseInventory();

        if (_playerController == null)
            _playerController = FindFirstObjectByType<PlayerController>();

        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _pausePanel.SetActive(true);
        ShowMainPage();
    }

    private void Resume()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        _pausePanel.SetActive(false);

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    private void ShowMainPage()
    {
        _mainPage.SetActive(true);
        _settingsPage.SetActive(false);
    }

    private void ShowSettingsPage()
    {
        _mainPage.SetActive(false);
        _settingsPage.SetActive(true);
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Pause Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _pausePanel = CreateRect("Pause Menu", canvasObject.transform);
        Stretch(_pausePanel.GetComponent<RectTransform>());

        Image dimmer = _pausePanel.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.72f);

        BuildMainPage(_pausePanel.transform);
        BuildSettingsPage(_pausePanel.transform);

        _pausePanel.SetActive(false);
    }

    private void BuildMainPage(Transform parent)
    {
        _mainPage = CreateRect("Pause Main Page", parent);
        Stretch(_mainPage.GetComponent<RectTransform>());

        Image box = CreateImage(_mainPage.transform, "Pause Panel", _panel);
        Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(520f, 430f));
        AddOutline(box.gameObject, _crimson, new Vector2(2f, -2f));

        Text title = CreateText(box.transform, "Title", "PAUSED", 46, _crimson, TextAnchor.MiddleCenter);
        SetAnchors(title.rectTransform, new Vector2(0.10f, 0.74f), new Vector2(0.90f, 0.94f));
        title.fontStyle = FontStyle.Bold;

        Button resume = CreateButton(box.transform, "Resume", Resume, new Vector2(340f, 66f), 24);
        Place(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 0.57f), new Vector2(340f, 66f));

        Button settings = CreateButton(box.transform, "Settings", ShowSettingsPage, new Vector2(340f, 66f), 24);
        Place(settings.GetComponent<RectTransform>(), new Vector2(0.5f, 0.39f), new Vector2(340f, 66f));

        Button mainMenu = CreateButton(box.transform, "Main Menu", GoToMainMenu, new Vector2(340f, 66f), 24);
        Place(mainMenu.GetComponent<RectTransform>(), new Vector2(0.5f, 0.21f), new Vector2(340f, 66f));
    }

    private void BuildSettingsPage(Transform parent)
    {
        _settingsPage = CreateRect("Pause Settings Page", parent);
        Stretch(_settingsPage.GetComponent<RectTransform>());

        Image box = CreateImage(_settingsPage.transform, "Settings Panel", _panel);
        Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(820f, 620f));
        AddOutline(box.gameObject, _crimson, new Vector2(2f, -2f));

        Text title = CreateText(box.transform, "Title", "SETTINGS", 42, _crimson, TextAnchor.MiddleCenter);
        SetAnchors(title.rectTransform, new Vector2(0.10f, 0.82f), new Vector2(0.90f, 0.96f));
        title.fontStyle = FontStyle.Bold;

        Transform settings = CreateRect("Settings Controls", box.transform).transform;
        SetAnchors(settings.GetComponent<RectTransform>(), new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.78f));

        AddSliderSetting(settings, "MASTER VOLUME", 0.84f, GameSettings.MasterVolume,
            value => GameSettings.MasterVolume = value);
        AddSliderSetting(settings, "MOUSE SENSITIVITY", 0.66f, GameSettings.MouseSensitivity,
            value => GameSettings.MouseSensitivity = value, 0.25f, 2f);
        AddToggleSetting(settings, "SPRINT TOGGLE", 0.47f, GameSettings.SprintToggle,
            value => GameSettings.SprintToggle = value);
        AddToggleSetting(settings, "CROUCH TOGGLE", 0.31f, GameSettings.CrouchToggle,
            value => GameSettings.CrouchToggle = value);
        AddToggleSetting(settings, "INVERT LOOK Y", 0.15f, GameSettings.InvertY,
            value => GameSettings.InvertY = value);
        AddToggleSetting(settings, "FULLSCREEN", 0f, GameSettings.Fullscreen,
            value => GameSettings.Fullscreen = value);

        Button back = CreateButton(box.transform, "BACK", ShowMainPage, new Vector2(220f, 54f), 19);
        Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0.10f), new Vector2(220f, 54f));

        _settingsPage.SetActive(false);
    }

    private void AddSliderSetting(Transform parent, string label, float rowY, float value,
        UnityEngine.Events.UnityAction<float> action, float min = 0f, float max = 1f)
    {
        Text settingLabel = CreateText(parent, label + " Label", label, 20, _pale, TextAnchor.MiddleLeft);
        SetAnchors(settingLabel.rectTransform, new Vector2(0f, rowY), new Vector2(0.42f, rowY + 0.12f));

        Slider slider = CreateSlider(parent);
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        SetAnchors(slider.GetComponent<RectTransform>(), new Vector2(0.47f, rowY + 0.015f),
            new Vector2(1f, rowY + 0.105f));
        slider.onValueChanged.AddListener(action);
    }

    private void AddToggleSetting(Transform parent, string label, float rowY, bool value,
        UnityEngine.Events.UnityAction<bool> action)
    {
        Text settingLabel = CreateText(parent, label + " Label", label, 20, _pale, TextAnchor.MiddleLeft);
        SetAnchors(settingLabel.rectTransform, new Vector2(0f, rowY), new Vector2(0.42f, rowY + 0.12f));

        Toggle toggle = CreateToggle(parent);
        SetAnchors(toggle.GetComponent<RectTransform>(), new Vector2(0.47f, rowY + 0.015f),
            new Vector2(0.55f, rowY + 0.105f));
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(action);
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action,
        Vector2 size, int fontSize)
    {
        GameObject buttonObject = CreateRect(label + " Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.075f, 0.075f, 0.08f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.45f, 0.47f);
        colors.pressedColor = _crimson;
        button.colors = colors;

        AddOutline(buttonObject, new Color(0.30f, 0.30f, 0.30f), new Vector2(1f, -1f));

        Text text = CreateText(buttonObject.transform, "Label", label, fontSize, _pale, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyle.Bold;
        buttonObject.GetComponent<RectTransform>().sizeDelta = size;
        return button;
    }

    private Slider CreateSlider(Transform parent)
    {
        GameObject root = CreateRect("Slider", parent);
        Slider slider = root.AddComponent<Slider>();

        Image background = CreateImage(root.transform, "Background", new Color(0.17f, 0.17f, 0.17f));
        SetAnchors(background.rectTransform, new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));

        Image fill = CreateImage(background.transform, "Fill", _crimson);
        Stretch(fill.rectTransform);

        RectTransform fillArea = CreateRect("Fill Area", root.transform).GetComponent<RectTransform>();
        Stretch(fillArea);
        fill.transform.SetParent(fillArea, false);

        Image handle = CreateImage(root.transform, "Handle", _pale);
        handle.rectTransform.sizeDelta = new Vector2(22f, 44f);

        RectTransform handleArea = CreateRect("Handle Slide Area", root.transform).GetComponent<RectTransform>();
        Stretch(handleArea);
        handle.transform.SetParent(handleArea, false);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    private Toggle CreateToggle(Transform parent)
    {
        GameObject root = CreateRect("Toggle", parent);
        Toggle toggle = root.AddComponent<Toggle>();

        Image background = CreateImage(root.transform, "Background", new Color(0.18f, 0.18f, 0.18f));
        Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(42f, 42f));
        AddOutline(background.gameObject, _dim, new Vector2(1f, -1f));

        Image checkmark = CreateImage(background.transform, "Checkmark", _crimson);
        Stretch(checkmark.rectTransform);
        checkmark.rectTransform.offsetMin = new Vector2(7f, 7f);
        checkmark.rectTransform.offsetMax = new Vector2(-7f, -7f);

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        return toggle;
    }

    private Text CreateText(Transform parent, string name, string value, int size, Color color, TextAnchor alignment)
    {
        GameObject textObject = CreateRect(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = CreateRect(name, parent);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
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
