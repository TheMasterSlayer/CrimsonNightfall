using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class NPCDialogueController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 45f;
    [SerializeField] private string promptMessage = "Press E to talk.";

    [Header("Dialogue")]
    [SerializeField] private string speakerName = "NPC";
    [SerializeField] [TextArea(2, 4)] private string[] npcLines;
    [SerializeField] private string[] responseTexts;

    [Header("SCP Survival Dialogue")]
    [SerializeField] private bool useScpSurvivalDialogue;
    [SerializeField] [TextArea(2, 4)] private string[] scpSurvivedNpcLines;
    [SerializeField] private string[] scpSurvivedResponseTexts;
    [SerializeField] [TextArea(2, 4)] private string[] scpRepeatNpcLines;
    [SerializeField] private string[] scpRepeatResponseTexts;
    [SerializeField] private GameObject[] activateObjectsAfterScpDialogue;
    [SerializeField] private Behaviour[] disableBehavioursAfterScpDialogue;
    [SerializeField] private string[] consumeInventoryIdsAfterScpDialogue;

    [Header("Dialogue Completion")]
    [SerializeField] private bool onlyCompleteOnce = true;
    [SerializeField] private Behaviour[] enableBehavioursOnComplete;

    private static NPCDialogueController _activeDialogue;

    private Camera _camera;
    private PlayerController _playerController;
    private CanvasGroup _dialogueGroup;
    private Text _speakerText;
    private Text _lineText;
    private Text _responseText;
    private Button _responseButton;
    private string[] _activeNpcLines;
    private string[] _activeResponseTexts;
    private int _lineIndex;
    private bool _isOpen;
    private float _promptTimer;
    private bool _completed;
    private bool _completedScpSurvivalDialogue;

    private void Awake()
    {
        EnsureCollider();
    }

    private void Update()
    {
        if (_isOpen)
            return;

        if (_activeDialogue != null)
            return;

        if (!IsFocused())
            return;

        _promptTimer -= Time.deltaTime;
        if (_promptTimer <= 0f)
        {
            CollectionInventory.ShowBottomMessage(promptMessage, 0.2f);
            _promptTimer = 0.15f;
        }

        if (Input.GetKeyDown(KeyCode.E))
            OpenDialogue();
    }

    private void OpenDialogue()
    {
        if (npcLines == null || npcLines.Length == 0)
            return;

        ChooseDialogue();
        if (_activeNpcLines == null || _activeNpcLines.Length == 0)
            return;

        _camera = Camera.main;
        _playerController = FindFirstObjectByType<PlayerController>();

        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildUiIfNeeded();
        _lineIndex = 0;
        _isOpen = true;
        _activeDialogue = this;
        _dialogueGroup.gameObject.SetActive(true);
        _dialogueGroup.alpha = 1f;
        ShowCurrentLine();
    }

    private void AdvanceDialogue()
    {
        _lineIndex++;
        if (_lineIndex >= _activeNpcLines.Length)
        {
            CloseDialogue();
            return;
        }

        ShowCurrentLine();
    }

    private void CloseDialogue()
    {
        if (_dialogueGroup != null)
        {
            _dialogueGroup.alpha = 0f;
            _dialogueGroup.gameObject.SetActive(false);
        }

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _isOpen = false;
        if (_activeDialogue == this)
            _activeDialogue = null;

        CompleteDialogue();
    }

    private void CompleteDialogue()
    {
        if (useScpSurvivalDialogue && ScpElevatorProgress.SurvivedScpElevator && !_completedScpSurvivalDialogue)
        {
            _completedScpSurvivalDialogue = true;
            ActivateObjects(activateObjectsAfterScpDialogue);
            DisableBehaviours(disableBehavioursAfterScpDialogue);
            ConsumeInventoryItems(consumeInventoryIdsAfterScpDialogue);
            return;
        }

        if (onlyCompleteOnce && _completed)
            return;

        _completed = true;

        if (enableBehavioursOnComplete == null)
            return;

        foreach (Behaviour behaviour in enableBehavioursOnComplete)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }
    }

    private void ShowCurrentLine()
    {
        _speakerText.text = speakerName;
        _lineText.text = _activeNpcLines[_lineIndex];
        _responseText.text = GetResponseText(_lineIndex);
    }

    private string GetResponseText(int index)
    {
        if (_activeResponseTexts == null || _activeResponseTexts.Length == 0)
            return "Okay.";

        if (index >= 0 && index < _activeResponseTexts.Length && !string.IsNullOrWhiteSpace(_activeResponseTexts[index]))
            return _activeResponseTexts[index];

        return "Okay.";
    }

    private void ChooseDialogue()
    {
        _activeNpcLines = npcLines;
        _activeResponseTexts = responseTexts;

        if (!useScpSurvivalDialogue || !ScpElevatorProgress.SurvivedScpElevator)
            return;

        if (_completedScpSurvivalDialogue)
        {
            _activeNpcLines = scpRepeatNpcLines;
            _activeResponseTexts = scpRepeatResponseTexts;
        }
        else
        {
            _activeNpcLines = scpSurvivedNpcLines;
            _activeResponseTexts = scpSurvivedResponseTexts;
        }
    }

    private static void ActivateObjects(GameObject[] objects)
    {
        if (objects == null)
            return;

        foreach (GameObject target in objects)
        {
            if (target != null)
                target.SetActive(true);
        }
    }

    private static void DisableBehaviours(Behaviour[] behaviours)
    {
        if (behaviours == null)
            return;

        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }
    }

    private static void ConsumeInventoryItems(string[] itemIds)
    {
        if (itemIds == null)
            return;

        foreach (string itemId in itemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
                CollectionInventory.ConsumeItem(itemId);
        }
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 focusPoint = GetFocusPoint();
        Vector3 toNpc = focusPoint - camera.transform.position;
        if (toNpc.magnitude > interactionRange)
            return false;

        if (Vector3.Angle(camera.transform.forward, toNpc) > interactionAngle)
            return false;

        return !Physics.Linecast(
            camera.transform.position,
            focusPoint,
            out RaycastHit hit,
            ~0,
            QueryTriggerInteraction.Ignore) ||
            hit.transform == transform ||
            hit.transform.IsChildOf(transform);
    }

    private Vector3 GetFocusPoint()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : transform.position + Vector3.up;
    }

    private void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.center = Vector3.up;
        collider.size = new Vector3(1f, 2f, 1f);
    }

    private void BuildUiIfNeeded()
    {
        if (_dialogueGroup != null)
            return;

        EnsureEventSystem();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Color panelColor = new Color(0.015f, 0.015f, 0.018f, 0.92f);
        Color crimson = new Color(0.72f, 0.035f, 0.055f);
        Color pale = new Color(0.88f, 0.86f, 0.84f);

        GameObject canvasObject = new GameObject($"{name} Dialogue Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreateRect("Dialogue Panel", canvasObject.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.12f, 0.06f);
        panelRect.anchorMax = new Vector2(0.88f, 0.28f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = crimson;
        outline.effectDistance = new Vector2(2f, -2f);

        _dialogueGroup = panel.AddComponent<CanvasGroup>();

        _speakerText = CreateText(panel.transform, "Speaker", font, speakerName, 26, crimson, TextAnchor.MiddleLeft);
        SetAnchors(_speakerText.rectTransform, new Vector2(0.035f, 0.72f), new Vector2(0.965f, 0.94f));
        _speakerText.fontStyle = FontStyle.Bold;

        _lineText = CreateText(panel.transform, "Line", font, string.Empty, 24, pale, TextAnchor.UpperLeft);
        SetAnchors(_lineText.rectTransform, new Vector2(0.035f, 0.26f), new Vector2(0.965f, 0.72f));
        _lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _lineText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject buttonObject = CreateRect("Response Button", panel.transform);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.58f, 0.05f);
        buttonRect.anchorMax = new Vector2(0.965f, 0.24f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.075f, 0.075f, 0.08f, 1f);
        _responseButton = buttonObject.AddComponent<Button>();
        _responseButton.targetGraphic = buttonImage;
        _responseButton.onClick.AddListener(AdvanceDialogue);

        Outline buttonOutline = buttonObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(0.45f, 0.45f, 0.45f);
        buttonOutline.effectDistance = new Vector2(1f, -1f);

        _responseText = CreateText(buttonObject.transform, "Text", font, "Okay.", 18, pale, TextAnchor.MiddleCenter);
        SetAnchors(_responseText.rectTransform, Vector2.zero, Vector2.one);
        _responseText.horizontalOverflow = HorizontalWrapMode.Wrap;

        panel.SetActive(false);
    }

    private static GameObject CreateRect(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        Font font,
        string value,
        int size,
        Color color,
        TextAnchor alignment)
    {
        GameObject textObject = CreateRect(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
