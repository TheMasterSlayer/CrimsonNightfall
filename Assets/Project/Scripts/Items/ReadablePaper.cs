using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ReadablePaper : MonoBehaviour
{
    [Header("Paper Text")]
    [SerializeField] [TextArea(6, 18)] private string paperDescription = "Write your paper text here.";

    [Header("Chaotic Mode Insight")]
    [SerializeField] private bool grantsChaoticModeInsight;
    [SerializeField] private string chaoticInsightMessage = "you gained insight on Chaotic Mode... now survive this night... or forever forget the truth...";

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private string promptMessage = "Press E to read.";
    [SerializeField] private bool allowLookInteraction = true;

    [Header("Read View")]
    [SerializeField] private float paperDistance = 0.75f;
    [SerializeField] private float paperMoveSpeed = 10f;
    [SerializeField] private Vector3 paperRotationOffset = new Vector3(0f, 180f, 0f);

    [Header("Panel")]
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.18f, 0.08f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.82f, 0.92f);
    [SerializeField] private int maxTextSize = 34;
    [SerializeField] private int minTextSize = 18;

    private bool _playerInRange;
    private bool _isReading;
    private bool _isShowingInsight;
    private Transform _originalParent;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Collider[] _colliders;
    private PlayerController _playerController;
    private CanvasGroup _readGroup;
    private CanvasGroup _insightGroup;
    private Text _descriptionText;
    private Text _insightText;
    private Font _font;

    private void Awake()
    {
        CollectionInventory.EnsureExists();
        _colliders = GetComponentsInChildren<Collider>();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildReadUi();
    }

    private void Update()
    {
        if (_isReading)
        {
            UpdateReadView();
            return;
        }

        if (_isShowingInsight)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                HideInsightMessage();

            return;
        }

        if (ClosetHide.IsPlayerInAnyEntryZone || ItemPickup.IsAnyItemInspecting)
            return;

        if (Input.GetKeyDown(KeyCode.E) && (_playerInRange || IsLookedAt()))
            StartReading();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _playerInRange = true;
        CollectionInventory.ShowBottomMessage(promptMessage, 0.25f);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_isReading && IsPlayerCollider(other))
            CollectionInventory.ShowBottomMessage(promptMessage, 0.25f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _playerInRange = false;
    }

    private void StartReading()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        _isReading = true;
        _originalParent = transform.parent;
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;

        _playerController = FindFirstObjectByType<PlayerController>();
        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        SetCollidersEnabled(false);

        _descriptionText.text = paperDescription;
        _readGroup.alpha = 1f;
        _readGroup.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UpdateReadView()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            StopReading();
            return;
        }

        Vector3 targetPosition = camera.transform.position + camera.transform.forward * paperDistance;
        transform.position = Vector3.Lerp(transform.position, targetPosition, paperMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            camera.transform.rotation * Quaternion.Euler(paperRotationOffset),
            paperMoveSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            StopReading();
    }

    private void StopReading()
    {
        _isReading = false;
        transform.SetParent(_originalParent, true);
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;

        SetCollidersEnabled(true);

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        _readGroup.alpha = 0f;
        _readGroup.gameObject.SetActive(false);

        TryShowChaoticInsightAfterReading();
    }

    private void BuildReadUi()
    {
        GameObject canvasObject = new GameObject($"{name}_ReadablePaperCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Readable Paper Text Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelAnchorMin;
        panelRect.anchorMax = panelAnchorMax;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        _readGroup = panel.AddComponent<CanvasGroup>();
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.04f, 0.035f, 0.03f, 0.92f);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.035f, 0.055f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject pageObject = new GameObject("Paper Page", typeof(RectTransform));
        pageObject.transform.SetParent(panel.transform, false);

        RectTransform pageRect = pageObject.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.04f, 0.04f);
        pageRect.anchorMax = new Vector2(0.96f, 0.96f);
        pageRect.offsetMin = Vector2.zero;
        pageRect.offsetMax = Vector2.zero;

        Image page = pageObject.AddComponent<Image>();
        page.color = new Color(0.78f, 0.72f, 0.60f, 0.96f);

        GameObject textObject = new GameObject("Description", typeof(RectTransform));
        textObject.transform.SetParent(pageObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.06f, 0.06f);
        textRect.anchorMax = new Vector2(0.94f, 0.94f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _descriptionText = textObject.AddComponent<Text>();
        _descriptionText.font = _font;
        _descriptionText.fontSize = maxTextSize;
        _descriptionText.color = new Color(0.09f, 0.075f, 0.06f);
        _descriptionText.alignment = TextAnchor.MiddleLeft;
        _descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
        _descriptionText.resizeTextForBestFit = true;
        _descriptionText.resizeTextMinSize = minTextSize;
        _descriptionText.resizeTextMaxSize = maxTextSize;

        panel.SetActive(false);

        BuildInsightUi(canvasObject.transform);
    }

    private void BuildInsightUi(Transform canvasParent)
    {
        GameObject panel = new GameObject("Chaotic Insight Message", typeof(RectTransform));
        panel.transform.SetParent(canvasParent, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetAnchors(panelRect, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.26f));

        _insightGroup = panel.AddComponent<CanvasGroup>();
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.015f, 0.015f, 0.018f, 0.94f);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.035f, 0.055f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        SetAnchors(textRect, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.90f));

        _insightText = textObject.AddComponent<Text>();
        _insightText.font = _font;
        _insightText.fontSize = 24;
        _insightText.color = new Color(0.88f, 0.86f, 0.84f);
        _insightText.alignment = TextAnchor.MiddleCenter;
        _insightText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _insightText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject hintObject = new GameObject("Dismiss Hint", typeof(RectTransform));
        hintObject.transform.SetParent(panel.transform, false);

        Text hint = hintObject.AddComponent<Text>();
        hint.font = _font;
        hint.text = "Press ESC to continue";
        hint.fontSize = 16;
        hint.color = new Color(0.55f, 0.55f, 0.55f);
        hint.alignment = TextAnchor.MiddleCenter;
        SetAnchors(hint.rectTransform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.22f));

        panel.SetActive(false);
    }

    private void TryShowChaoticInsightAfterReading()
    {
        if (!grantsChaoticModeInsight || ChaoticModeProgress.SecretInsightFoundThisRun)
            return;

        ChaoticModeProgress.MarkSecretInsightFound();
        _isShowingInsight = true;

        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        _insightText.text = chaoticInsightMessage;
        _insightGroup.alpha = 1f;
        _insightGroup.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HideInsightMessage()
    {
        _isShowingInsight = false;
        _insightGroup.alpha = 0f;
        _insightGroup.gameObject.SetActive(false);

        if (_playerController != null)
            _playerController.SetInputEnabled(true);
    }

    private bool IsLookedAt()
    {
        if (!allowLookInteraction)
            return false;

        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Collide))
            return false;

        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null)
            return;

        foreach (Collider paperCollider in _colliders)
        {
            if (paperCollider != null)
                paperCollider.enabled = enabled;
        }
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null;
    }
}
