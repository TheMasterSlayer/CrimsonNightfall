using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const string NormalGameplayScene = "SampleScene";
    private const string BackgroundResource = "TitleScreenBackground";

    private static readonly Color Crimson = new Color(0.72f, 0.035f, 0.055f);
    private static readonly Color Pale = new Color(0.88f, 0.86f, 0.84f);
    private static readonly Color Dim = new Color(0.40f, 0.40f, 0.40f);
    private static readonly Color Panel = new Color(0.025f, 0.025f, 0.028f, 0.96f);

    private Font _font;
    private GameObject _mainPage;
    private GameObject _modePage;
    private GameObject _controlsPage;
    private GameObject _modal;
    private Text _description;
    private Text _modalTitle;
    private Text _modalBody;
    private Button _startButton;
    private Image _normalBorder;
    private Image _chaosBorder;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        GameModeSettings.Clear();
        GameSettings.Apply();

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildInterface();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_modal.activeSelf)
                _modal.SetActive(false);
            else if (_controlsPage.activeSelf)
                ShowModePage();
            else if (_modePage.activeSelf)
                ShowMainPage();
        }

        if (_controlsPage.activeSelf &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            LoadGameplay();
        }
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage(canvasObject.transform, "Black Background", Color.black);
        Stretch(background.rectTransform);

        _mainPage = CreateRect("Main Page", canvasObject.transform);
        Stretch(_mainPage.GetComponent<RectTransform>());
        BuildMainPage(_mainPage.transform);

        _modePage = CreateRect("Mode Page", canvasObject.transform);
        Stretch(_modePage.GetComponent<RectTransform>());
        BuildModePage(_modePage.transform);

        _controlsPage = CreateRect("Controls Page", canvasObject.transform);
        Stretch(_controlsPage.GetComponent<RectTransform>());
        BuildControlsPage(_controlsPage.transform);

        BuildModal(canvasObject.transform);
        ShowMainPage();
    }

    private void BuildMainPage(Transform parent)
    {
        Image photo = CreateImage(parent, "Title Screen Background", new Color(0.12f, 0.12f, 0.12f));
        SetAnchors(photo.rectTransform, new Vector2(0.5f, 0f), Vector2.one);

        Sprite suppliedImage = LoadBackgroundSprite();
        if (suppliedImage != null)
        {
            photo.sprite = suppliedImage;
            photo.preserveAspect = true;
            photo.color = Color.white;
        }
        else
        {
            Text placeholder = CreateText(photo.transform, "Background Placeholder",
                "ADD YOUR IN-GAME PHOTO AS\nAssets/Resources/TitleScreenBackground.png", 24, Dim, TextAnchor.MiddleCenter);
            Stretch(placeholder.rectTransform);
        }

        Image shade = CreateImage(parent, "Image Shade", new Color(0f, 0f, 0f, 0.30f));
        SetAnchors(shade.rectTransform, new Vector2(0.5f, 0f), Vector2.one);

        Image divider = CreateImage(parent, "Crimson Divider", Crimson);
        SetAnchors(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        divider.rectTransform.sizeDelta = new Vector2(3f, 0f);

        Text title = CreateText(parent, "Title", "CrimsonNightfall", 72, Crimson, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 0.87f), new Vector2(900f, 110f));
        title.fontStyle = FontStyle.Bold;

        Transform menu = CreateRect("Menu Options", parent).transform;
        Place(menu.GetComponent<RectTransform>(), new Vector2(0.25f, 0.49f), new Vector2(470f, 490f));

        VerticalLayoutGroup layout = menu.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        AddMenuButton(menu, "PLAY", ShowModePage);
        AddMenuButton(menu, "SETTINGS", () => ShowSettings());
        AddMenuButton(menu, "CREDITS", () => ShowModal("CREDITS", LoadMenuText("Credits")));
        AddMenuButton(menu, "DISCLAIMER", () => ShowModal("DISCLAIMER", LoadMenuText("Disclaimer")));
        AddMenuButton(menu, "EXIT", ExitGame);

        Text version = CreateText(parent, "Footer", "A HORROR EXPERIENCE", 16,
            new Color(0.55f, 0.55f, 0.55f), TextAnchor.MiddleLeft);
        SetAnchors(version.rectTransform, Vector2.zero, Vector2.zero);
        version.rectTransform.anchoredPosition = new Vector2(40f, 28f);
        version.rectTransform.sizeDelta = new Vector2(400f, 40f);
    }

    private void BuildModePage(Transform parent)
    {
        Image vignette = CreateImage(parent, "Mode Background", new Color(0.015f, 0.015f, 0.018f));
        Stretch(vignette.rectTransform);

        Text heading = CreateText(parent, "Heading", "CHOOSE MODE", 58, Crimson, TextAnchor.MiddleCenter);
        Place(heading.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(800f, 90f));
        heading.fontStyle = FontStyle.Bold;

        Text instruction = CreateText(parent, "Instruction", "SELECT HOW THE NIGHT WILL HUNT YOU", 18, Dim,
            TextAnchor.MiddleCenter);
        Place(instruction.rectTransform, new Vector2(0.5f, 0.80f), new Vector2(700f, 40f));

        _normalBorder = AddModeButton(parent, "NORMAL", new Vector2(0.30f, 0.65f), GameMode.Normal);
        _chaosBorder = AddModeButton(parent, "CHAOS", new Vector2(0.70f, 0.65f), GameMode.Chaos);

        Image descriptionPanel = CreateImage(parent, "Description Panel", Panel);
        Place(descriptionPanel.rectTransform, new Vector2(0.5f, 0.40f), new Vector2(1000f, 220f));
        AddOutline(descriptionPanel.gameObject, new Color(0.20f, 0.20f, 0.20f), new Vector2(1f, -1f));

        _description = CreateText(descriptionPanel.transform, "Description",
            "Select Normal or Chaos to reveal the rules of the night.", 24, Pale, TextAnchor.MiddleCenter);
        Stretch(_description.rectTransform);
        _description.rectTransform.offsetMin = new Vector2(70f, 35f);
        _description.rectTransform.offsetMax = new Vector2(-70f, -35f);
        _description.horizontalOverflow = HorizontalWrapMode.Wrap;
        _description.verticalOverflow = VerticalWrapMode.Overflow;

        _startButton = CreateButton(parent, "START", StartSelectedMode, new Vector2(420f, 82f), 30);
        Place(_startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.15f), new Vector2(420f, 82f));
        _startButton.interactable = false;

        Button back = CreateButton(parent, "BACK", ShowMainPage, new Vector2(180f, 54f), 18);
        SetAnchors(back.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        back.GetComponent<RectTransform>().anchoredPosition = new Vector2(120f, 55f);
    }

    private void BuildControlsPage(Transform parent)
    {
        Image background = CreateImage(parent, "Controls Background", new Color(0.01f, 0.01f, 0.012f));
        Stretch(background.rectTransform);

        Text heading = CreateText(parent, "Heading", "BEFORE YOU ENTER", 52, Crimson, TextAnchor.MiddleCenter);
        Place(heading.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(900f, 90f));
        heading.fontStyle = FontStyle.Bold;

        Image panel = CreateImage(parent, "Controls Panel", Panel);
        Place(panel.rectTransform, new Vector2(0.5f, 0.50f), new Vector2(760f, 480f));
        AddOutline(panel.gameObject, new Color(0.28f, 0.02f, 0.03f), new Vector2(2f, -2f));

        string controls =
            "<b>WASD</b>   -   Move\n\n" +
            "<b>E</b>   -   Interact / Pickup\n\n" +
            "<b>F</b>   -   Flashlight Toggle\n\n" +
            "<b>SHIFT</b>   -   Sprint\n\n" +
            "<b>CTRL</b>   -   Crouch";

        Text controlText = CreateText(panel.transform, "Controls", controls, 29, Pale, TextAnchor.MiddleLeft);
        controlText.supportRichText = true;
        Stretch(controlText.rectTransform);
        controlText.rectTransform.offsetMin = new Vector2(100f, 45f);
        controlText.rectTransform.offsetMax = new Vector2(-70f, -45f);

        Button enter = CreateButton(parent, "ENTER THE NIGHT", LoadGameplay, new Vector2(420f, 76f), 24);
        Place(enter.GetComponent<RectTransform>(), new Vector2(0.5f, 0.13f), new Vector2(420f, 76f));

        Text hint = CreateText(parent, "Keyboard Hint", "PRESS ENTER OR SPACE", 14, Dim, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0.5f, 0.065f), new Vector2(400f, 30f));
    }

    private void BuildModal(Transform parent)
    {
        _modal = CreateRect("Modal", parent);
        Stretch(_modal.GetComponent<RectTransform>());

        Image blocker = _modal.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.84f);

        Image box = CreateImage(_modal.transform, "Modal Panel", Panel);
        Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(920f, 720f));
        AddOutline(box.gameObject, Crimson, new Vector2(2f, -2f));

        _modalTitle = CreateText(box.transform, "Modal Title", string.Empty, 40, Crimson, TextAnchor.MiddleCenter);
        SetAnchors(_modalTitle.rectTransform, new Vector2(0.1f, 0.77f), new Vector2(0.9f, 0.95f));
        _modalTitle.fontStyle = FontStyle.Bold;

        _modalBody = CreateText(box.transform, "Modal Body", string.Empty, 23, Pale, TextAnchor.MiddleCenter);
        SetAnchors(_modalBody.rectTransform, new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.76f));

        Button close = CreateButton(box.transform, "CLOSE", () => _modal.SetActive(false), new Vector2(260f, 62f), 20);
        Place(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0.10f), new Vector2(260f, 62f));
    }

    private void ShowSettings()
    {
        ShowModal("SETTINGS", string.Empty);
        _modalBody.gameObject.SetActive(false);

        Transform panel = _modalTitle.transform.parent;
        Transform oldSettings = panel.Find("Settings Controls");
        if (oldSettings != null)
            Destroy(oldSettings.gameObject);

        Transform settings = CreateRect("Settings Controls", panel).transform;
        SetAnchors(settings.GetComponent<RectTransform>(), new Vector2(0.12f, 0.17f), new Vector2(0.88f, 0.76f));

        AddSliderSetting(settings, "MASTER VOLUME", 0.86f, GameSettings.MasterVolume,
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
    }

    private void ShowModal(string title, string body)
    {
        Transform settings = _modalTitle != null ? _modalTitle.transform.parent.Find("Settings Controls") : null;
        if (settings != null)
            Destroy(settings.gameObject);

        _modalTitle.text = title;
        _modalBody.text = body;
        _modalBody.gameObject.SetActive(true);
        _modal.SetActive(true);
    }

    private void ShowMainPage()
    {
        _mainPage.SetActive(true);
        _modePage.SetActive(false);
        _controlsPage.SetActive(false);
        _modal.SetActive(false);
    }

    private void ShowModePage()
    {
        _mainPage.SetActive(false);
        _modePage.SetActive(true);
        _controlsPage.SetActive(false);
        _modal.SetActive(false);
    }

    private void SelectMode(GameMode mode)
    {
        GameModeSettings.Select(mode);
        _startButton.interactable = true;

        bool normal = mode == GameMode.Normal;
        _normalBorder.color = normal ? Crimson : new Color(0.18f, 0.18f, 0.18f);
        _chaosBorder.color = normal ? new Color(0.18f, 0.18f, 0.18f) : Crimson;

        _description.text = normal
            ? "Normal stamina consumption, basic entity field-of-view and aggression, normal physics, item placement simplified."
            : "Faster stamina consumption, greater entity field-of-view and aggression, objects will move when collided with by chasing entity, item placement more difficult.";
    }

    private void StartSelectedMode()
    {
        if (GameModeSettings.SelectedMode == GameMode.None)
            return;

        _mainPage.SetActive(false);
        _modePage.SetActive(false);
        _controlsPage.SetActive(true);
    }

    private void LoadGameplay()
    {
        if (GameModeSettings.SelectedMode == GameMode.Normal)
            SceneManager.LoadScene(NormalGameplayScene);
        else
            SceneManager.LoadScene(NormalGameplayScene);
    }

    private static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void AddMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, label, action, new Vector2(420f, 68f), 25);
        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 68f;
    }

    private Image AddModeButton(Transform parent, string label, Vector2 anchor, GameMode mode)
    {
        Image border = CreateImage(parent, label + " Border", new Color(0.18f, 0.18f, 0.18f));
        Place(border.rectTransform, anchor, new Vector2(440f, 130f));

        Button button = CreateButton(border.transform, label, () => SelectMode(mode), new Vector2(430f, 120f), 30);
        Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(430f, 120f));
        return border;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action,
        Vector2 size, int fontSize)
    {
        GameObject buttonObject = CreateRect(label + " Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.075f, 0.075f, 0.08f, 0.98f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.40f, 0.42f);
        colors.pressedColor = Crimson;
        colors.selectedColor = new Color(0.85f, 0.20f, 0.22f);
        colors.disabledColor = new Color(0.26f, 0.26f, 0.26f, 0.75f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.10f;
        button.colors = colors;
        button.onClick.AddListener(action);
        buttonObject.GetComponent<RectTransform>().sizeDelta = size;

        AddOutline(buttonObject, new Color(0.30f, 0.30f, 0.30f), new Vector2(1f, -1f));

        Text text = CreateText(buttonObject.transform, "Label", label, fontSize, Pale, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyle.Bold;
        return button;
    }

    private Slider CreateSlider(Transform parent)
    {
        GameObject root = CreateRect("Volume Slider", parent);
        Slider slider = root.AddComponent<Slider>();

        Image background = CreateImage(root.transform, "Background", new Color(0.17f, 0.17f, 0.17f));
        SetAnchors(background.rectTransform, new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));

        Image fill = CreateImage(background.transform, "Fill", Crimson);
        Stretch(fill.rectTransform);

        RectTransform fillArea = CreateRect("Fill Area", root.transform).GetComponent<RectTransform>();
        Stretch(fillArea);
        fill.transform.SetParent(fillArea, false);

        Image handle = CreateImage(root.transform, "Handle", Pale);
        handle.rectTransform.sizeDelta = new Vector2(22f, 44f);

        RectTransform handleArea = CreateRect("Handle Slide Area", root.transform).GetComponent<RectTransform>();
        Stretch(handleArea);
        handle.transform.SetParent(handleArea, false);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private void AddSliderSetting(Transform parent, string label, float rowY, float value,
        UnityEngine.Events.UnityAction<float> action, float min = 0f, float max = 1f)
    {
        Text settingLabel = CreateText(parent, label + " Label", label, 20, Pale, TextAnchor.MiddleLeft);
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
        Text settingLabel = CreateText(parent, label + " Label", label, 20, Pale, TextAnchor.MiddleLeft);
        SetAnchors(settingLabel.rectTransform, new Vector2(0f, rowY), new Vector2(0.42f, rowY + 0.12f));

        Toggle toggle = CreateToggle(parent);
        SetAnchors(toggle.GetComponent<RectTransform>(), new Vector2(0.47f, rowY + 0.015f),
            new Vector2(0.55f, rowY + 0.105f));
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(action);
    }

    private Toggle CreateToggle(Transform parent)
    {
        GameObject root = CreateRect("Fullscreen Toggle", parent);
        Toggle toggle = root.AddComponent<Toggle>();

        Image background = CreateImage(root.transform, "Background", new Color(0.18f, 0.18f, 0.18f));
        Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(42f, 42f));
        AddOutline(background.gameObject, Dim, new Vector2(1f, -1f));

        Image checkmark = CreateImage(background.transform, "Checkmark", Crimson);
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

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private static string LoadMenuText(string resourceName)
    {
        TextAsset content = Resources.Load<TextAsset>(resourceName);
        return content != null ? content.text : resourceName + " text is missing.";
    }

    private static Sprite LoadBackgroundSprite()
    {
        Sprite sprite = Resources.Load<Sprite>(BackgroundResource);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(BackgroundResource);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
    }
}
