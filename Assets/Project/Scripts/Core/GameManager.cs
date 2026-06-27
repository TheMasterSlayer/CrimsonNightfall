using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central game hub. Tracks collected items, triggers the win screen,
/// and handles the player being caught. All other scripts talk to this.
///
/// Uses the Singleton pattern so any script can call GameManager.Instance
/// without needing a dragged reference in the Inspector.
/// </summary>
public class GameManager : MonoBehaviour
{
    private static readonly string[] LoseTips =
    {
        "Tip: Listen for the entities footsteps and sounds, alongside opening doors to gauge how close the entity is.",
        "Tip: Make sure to utilize CTRL for crouching to avoid being seen, and to look at lower areas where items may be hidden.",
        "Tip: Closets are your best friend. Tactically use them to hide, but don't linger too long.",
        "Tip: Remember to sprint with SHIFT, but your stamina is limited.",
        "Tip: If you can see it, you can probably reach it. Make sure to look up and down for valuable items."
    };

    private static readonly string[] NormalEndingLines =
    {
        "You escaped without a scratch or a mark this time...",
        "but you open your eyes to only continue back to where you started...",
        "standing in the main room... stuck in this never-ending nightmare..."
    };

    private static readonly string[] SecretEndingLines =
    {
        "You learned the truth...",
        "you gained insight on the true, chaotic area...",
        "where you can finally escape this never-ending nightmare..."
    };

    // ── Singleton ──────────────────────────────────────────────────────────

    public static GameManager Instance { get; private set; }

    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("Items")]
    [SerializeField] private int totalItemsRequired = 3;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("UI Panels")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] [Min(1f)] private float loseTipInterval = 8f;

    [Header("Win Ending")]
    [SerializeField] private float winFadeToBlackDuration = 3f;
    [SerializeField] private float winTextFadeDuration = 1.5f;
    [SerializeField] private float winTextHoldDuration = 2.7f;

    [Header("Lose Ending")]
    [SerializeField] private float loseFadeToBlackDuration = 2.5f;

    // ── Private State ──────────────────────────────────────────────────────

    private int  _itemsCollected = 0;
    private bool _gameOver       = false;
    private readonly HashSet<string> _keys = new HashSet<string>();
    private readonly HashSet<string> _unlockedDoors = new HashSet<string>();
    private CanvasGroup _winGroup;
    private Image _winBackground;
    private Text _winTitleText;
    private Text _winEndingText;
    private CanvasGroup _winButtonGroup;
    private Image _loseBackground;
    private CanvasGroup _loseContentGroup;
    private Text _loseTipText;
    private Coroutine _loseTipRoutine;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        // Enforce a single instance; destroy any duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ChaoticModeProgress.ResetRunProgress();
        CollectionInventory.ResetInventory();
        BuildWinScreenIfNeeded();
        BuildLoseScreenIfNeeded();
    }

    private void Start()
    {
        // Make sure UI panels are hidden at game start
        if (winPanel  != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Ensure the cursor is locked when gameplay begins
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ItemPickup when the player collects an item.
    /// Returns true if all items are now collected.
    /// </summary>
    public bool OnItemCollected()
    {
        if (_gameOver) return false;

        _itemsCollected++;

        return _itemsCollected >= totalItemsRequired;
    }

    public void AddKey(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            return;

        _keys.Add(keyId);
    }

    public bool HasKey(string keyId)
    {
        return !string.IsNullOrWhiteSpace(keyId) && _keys.Contains(keyId);
    }

    public void MarkDoorUnlocked(string doorName)
    {
        if (!string.IsNullOrWhiteSpace(doorName))
            _unlockedDoors.Add(doorName);
    }

    public bool IsDoorUnlocked(string doorName)
    {
        return !string.IsNullOrWhiteSpace(doorName) && _unlockedDoors.Contains(doorName);
    }

    /// <summary>Called by DoorController when the player escapes.</summary>
    public void OnPlayerEscaped()
    {
        if (_gameOver) return;
        _gameOver = true;

        bool foundSecretInsight = ChaoticModeProgress.SecretInsightFoundThisRun;
        ChaoticModeProgress.TryUnlockAfterEscape();
        ShowWin(foundSecretInsight);
    }

    /// <summary>Called by the AI when it catches the player.</summary>
    public void OnPlayerCaught()
    {
        if (_gameOver) return;
        _gameOver = true;
        CollectionInventory.ForceCloseInventory();

        ShowLose();
    }

    /// <summary>How many items the player still needs to find.</summary>
    public int ItemsRemaining => totalItemsRequired - _itemsCollected;

    /// <summary>True once all required items have been picked up.</summary>
    public bool AllItemsCollected => _itemsCollected >= totalItemsRequired;

    /// <summary>True if the game has ended (win or lose).</summary>
    public bool IsGameOver => _gameOver;

    // ── UI Helpers ─────────────────────────────────────────────────────────

    private void ShowWin(bool secretEnding)
    {
        if (winPanel != null) winPanel.SetActive(true);

        // Disable the player controller so they can't move during the screen
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        StartCoroutine(PlayWinSequence(secretEnding));
    }

    private IEnumerator PlayWinSequence(bool secretEnding)
    {
        string[] lines = secretEnding ? SecretEndingLines : NormalEndingLines;

        _winGroup.alpha = 1f;
        _winBackground.color = new Color(0f, 0f, 0f, 0f);
        _winTitleText.text = string.Empty;
        _winTitleText.canvasRenderer.SetAlpha(0f);
        _winEndingText.text = string.Empty;
        _winEndingText.canvasRenderer.SetAlpha(0f);
        _winButtonGroup.alpha = 0f;
        _winButtonGroup.interactable = false;
        _winButtonGroup.blocksRaycasts = false;

        float startVolume = AudioListener.volume;
        for (float elapsed = 0f; elapsed < winFadeToBlackDuration; elapsed += Time.unscaledDeltaTime)
        {
            float alpha = Mathf.Clamp01(elapsed / winFadeToBlackDuration);
            _winBackground.color = new Color(0f, 0f, 0f, alpha);
            AudioListener.volume = Mathf.Lerp(startVolume, 0f, alpha);
            yield return null;
        }

        _winBackground.color = Color.black;
        AudioListener.volume = 0f;

        _winTitleText.CrossFadeAlpha(1f, winTextFadeDuration, true);
        yield return new WaitForSecondsRealtime(winTextFadeDuration + 0.35f);

        for (int i = 0; i < lines.Length; i++)
        {
            _winEndingText.text = lines[i];
            _winEndingText.canvasRenderer.SetAlpha(0f);
            _winEndingText.CrossFadeAlpha(1f, winTextFadeDuration, true);
            yield return new WaitForSecondsRealtime(winTextFadeDuration + winTextHoldDuration);

            if (i < lines.Length - 1)
            {
                _winEndingText.CrossFadeAlpha(0f, winTextFadeDuration, true);
                yield return new WaitForSecondsRealtime(winTextFadeDuration * 0.75f);
            }
        }

        for (float elapsed = 0f; elapsed < winTextFadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            _winButtonGroup.alpha = Mathf.Clamp01(elapsed / winTextFadeDuration);
            yield return null;
        }

        _winButtonGroup.alpha = 1f;
        _winButtonGroup.interactable = true;
        _winButtonGroup.blocksRaycasts = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowLose()
    {
        if (losePanel != null) losePanel.SetActive(true);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (_loseTipRoutine != null)
            StopCoroutine(_loseTipRoutine);

        StartCoroutine(PlayLoseSequence());
    }

    private IEnumerator PlayLoseSequence()
    {
        if (_loseBackground == null || _loseContentGroup == null)
        {
            AudioListener.volume = 0f;
            _loseTipRoutine = StartCoroutine(RotateLoseTips());
            yield break;
        }

        _loseBackground.color = new Color(0f, 0f, 0f, 0f);
        _loseContentGroup.alpha = 0f;
        _loseContentGroup.interactable = false;
        _loseContentGroup.blocksRaycasts = false;

        float startVolume = AudioListener.volume;
        float duration = Mathf.Max(0.01f, loseFadeToBlackDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float alpha = Mathf.Clamp01(elapsed / duration);
            _loseBackground.color = new Color(0f, 0f, 0f, alpha);
            AudioListener.volume = Mathf.Lerp(startVolume, 0f, alpha);
            yield return null;
        }

        _loseBackground.color = Color.black;
        AudioListener.volume = 0f;

        _loseContentGroup.alpha = 1f;
        _loseContentGroup.interactable = true;
        _loseContentGroup.blocksRaycasts = true;
        _loseTipRoutine = StartCoroutine(RotateLoseTips());
    }

    private IEnumerator RotateLoseTips()
    {
        int tipIndex = Random.Range(0, LoseTips.Length);

        while (losePanel != null && losePanel.activeSelf)
        {
            if (_loseTipText != null)
                _loseTipText.text = LoseTips[tipIndex];

            tipIndex = (tipIndex + 1) % LoseTips.Length;
            yield return new WaitForSecondsRealtime(loseTipInterval);
        }
    }

    private void BuildWinScreenIfNeeded()
    {
        if (winPanel != null)
            return;

        EnsureEventSystem();

        GameObject canvasObject = new GameObject(
            "Win Screen Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        winPanel = CreateUiObject("Win Screen", canvasObject.transform);
        Stretch(winPanel.GetComponent<RectTransform>());
        _winGroup = winPanel.AddComponent<CanvasGroup>();
        _winGroup.alpha = 1f;

        _winBackground = winPanel.AddComponent<Image>();
        _winBackground.color = new Color(0f, 0f, 0f, 0f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _winTitleText = CreateLoseText(
            winPanel.transform, "Ending Label", "Normal_Ending", font, 34,
            new Color(0.62f, 0.62f, 0.62f), TextAnchor.MiddleCenter);
        SetAnchors(_winTitleText.rectTransform, new Vector2(0.20f, 0.70f), new Vector2(0.80f, 0.82f));

        _winEndingText = CreateLoseText(
            winPanel.transform, "Ending Text", string.Empty, font, 48,
            Color.white, TextAnchor.MiddleCenter);
        SetAnchors(_winEndingText.rectTransform, new Vector2(0.12f, 0.35f), new Vector2(0.88f, 0.65f));
        _winEndingText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _winEndingText.verticalOverflow = VerticalWrapMode.Overflow;

        Button mainMenuButton = CreateLoseButton(winPanel.transform, font);
        RectTransform buttonRect = mainMenuButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.18f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(420f, 76f);

        _winButtonGroup = mainMenuButton.gameObject.AddComponent<CanvasGroup>();
        _winButtonGroup.alpha = 0f;
        _winButtonGroup.interactable = false;
        _winButtonGroup.blocksRaycasts = false;

        winPanel.SetActive(false);
    }

    private void BuildLoseScreenIfNeeded()
    {
        if (losePanel != null)
            return;

        EnsureEventSystem();

        GameObject canvasObject = new GameObject(
            "Lose Screen Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        losePanel = CreateUiObject("Lose Screen", canvasObject.transform);
        Stretch(losePanel.GetComponent<RectTransform>());
        _loseBackground = losePanel.AddComponent<Image>();
        _loseBackground.color = new Color(0f, 0f, 0f, 0f);

        GameObject contentObject = CreateUiObject("Lose Screen Content", losePanel.transform);
        Stretch(contentObject.GetComponent<RectTransform>());
        _loseContentGroup = contentObject.AddComponent<CanvasGroup>();
        _loseContentGroup.alpha = 0f;
        _loseContentGroup.interactable = false;
        _loseContentGroup.blocksRaycasts = false;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Text title = CreateLoseText(
            contentObject.transform, "Title", "YOU WERE CAUGHT", font, 64,
            new Color(0.72f, 0.035f, 0.055f), TextAnchor.MiddleCenter);
        SetAnchors(title.rectTransform, new Vector2(0.15f, 0.72f), new Vector2(0.85f, 0.90f));
        title.fontStyle = FontStyle.Bold;

        _loseTipText = CreateLoseText(
            contentObject.transform, "Tip", LoseTips[0], font, 26,
            new Color(0.88f, 0.86f, 0.84f), TextAnchor.MiddleCenter);
        SetAnchors(_loseTipText.rectTransform, new Vector2(0.18f, 0.35f), new Vector2(0.82f, 0.65f));
        _loseTipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _loseTipText.verticalOverflow = VerticalWrapMode.Overflow;

        Button mainMenuButton = CreateLoseButton(contentObject.transform, font);
        RectTransform buttonRect = mainMenuButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.18f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(420f, 76f);

        losePanel.SetActive(false);
    }

    private Button CreateLoseButton(Transform parent, Font font)
    {
        GameObject buttonObject = CreateUiObject("Main Menu Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.075f, 0.075f, 0.08f, 1f);

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.035f, 0.055f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(GoToMainMenu);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.45f, 0.47f);
        colors.pressedColor = new Color(0.72f, 0.035f, 0.055f);
        button.colors = colors;

        Text label = CreateLoseText(
            buttonObject.transform, "Label", "MAIN MENU", font, 28,
            new Color(0.88f, 0.86f, 0.84f), TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        label.fontStyle = FontStyle.Bold;
        return button;
    }

    private static Text CreateLoseText(
        Transform parent, string objectName, string value, Font font, int size, Color color, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
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

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ── Button Callbacks (wire these to your UI buttons) ──────────────────

    /// <summary>Restart button on the lose screen.</summary>
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Main menu button on win or lose screen.</summary>
    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
}
