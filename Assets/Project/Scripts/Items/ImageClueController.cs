using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-2000)]
public class ImageClueController : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private string inventoryItemId = "ImageClue";
    [SerializeField] private string selectedHint = "left click to view image clue.";
    [SerializeField] private string insightMessage = "You gained insight on where the Exit Key is.";

    [Header("Clue Images")]
    [SerializeField] private Sprite[] clueImages;
    [SerializeField] private string clueImageResourcePrefix = "ImageClue";
    [SerializeField] private int clueImageResourceCount = 4;
    [SerializeField] private Renderer clueSurfaceRenderer;
    [SerializeField] private SpriteRenderer clueSpriteRenderer;
    [SerializeField] private string texturePropertyName = "_BaseMap";

    [Header("Linked Exit Key")]
    [SerializeField] private GameObject exitKey;
    [SerializeField] private Transform[] exitKeySpawnPoints;
    [SerializeField] private bool useSpawnRotation = true;

    public int SelectedClueIndex { get; private set; } = -1;
    public Sprite SelectedClueImage { get; private set; }
    public Transform SelectedExitKeySpawn =>
        SelectedClueIndex >= 0 && exitKeySpawnPoints != null && SelectedClueIndex < exitKeySpawnPoints.Length
            ? exitKeySpawnPoints[SelectedClueIndex]
            : null;

    private void Awake()
    {
        AutoFindReferences();
        SortLinkedCluesAndSpawns();
        HideExitKeyUntilClueCollected();
        SelectLinkedClueAndExitKeySpawn();
        ApplyClueImageToPaper();
    }

    private void OnItemCollectedByPlayer()
    {
        if (SelectedClueImage != null)
            ImageClueInventoryViewer.Ensure(inventoryItemId, SelectedClueImage, selectedHint);

        SpawnExitKey();
        CollectionInventory.ShowMessage(insightMessage, 3f);
    }

    private void AutoFindReferences()
    {
        if (clueImages == null || clueImages.Length == 0)
            clueImages = LoadClueImagesFromResources();

        if (clueSurfaceRenderer == null)
            clueSurfaceRenderer = GetComponentInChildren<Renderer>(true);

        if (clueSpriteRenderer == null)
            clueSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (exitKey == null)
            exitKey = GameObject.Find("ExitKey");

        if (exitKeySpawnPoints == null || exitKeySpawnPoints.Length == 0)
        {
            GameObject spawnRoot = GameObject.Find("EK_Spawns");
            if (spawnRoot != null)
            {
                exitKeySpawnPoints = new Transform[spawnRoot.transform.childCount];
                for (int i = 0; i < spawnRoot.transform.childCount; i++)
                    exitKeySpawnPoints[i] = spawnRoot.transform.GetChild(i);
            }
        }
    }

    private Sprite[] LoadClueImagesFromResources()
    {
        Sprite[] loaded = new Sprite[Mathf.Max(0, clueImageResourceCount)];
        int count = 0;

        for (int i = 1; i <= clueImageResourceCount; i++)
        {
            string resourceName = $"{clueImageResourcePrefix}{i:00}";
            Sprite sprite = Resources.Load<Sprite>(resourceName);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourceName);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f);
                }
            }

            if (sprite == null)
                continue;

            loaded[count] = sprite;
            count++;
        }

        Sprite[] result = new Sprite[count];
        for (int i = 0; i < count; i++)
            result[i] = loaded[i];

        return result;
    }

    private void HideExitKeyUntilClueCollected()
    {
        if (exitKey == null)
            return;

        RandomItemSpawn randomSpawn = exitKey.GetComponent<RandomItemSpawn>();
        if (randomSpawn != null)
            randomSpawn.enabled = false;

        exitKey.SetActive(false);
    }

    private void SortLinkedCluesAndSpawns()
    {
        if (clueImages != null)
        {
            System.Array.Sort(clueImages, (left, right) =>
                string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty));
        }

        if (exitKeySpawnPoints != null)
        {
            System.Array.Sort(exitKeySpawnPoints, (left, right) =>
                string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty));
        }
    }

    private void SelectLinkedClueAndExitKeySpawn()
    {
        int pairCount = Mathf.Min(
            clueImages != null ? clueImages.Length : 0,
            exitKeySpawnPoints != null ? exitKeySpawnPoints.Length : 0);

        if (pairCount <= 0)
        {
            Debug.LogWarning(
                "[ImageClue] No linked clue image / ExitKey spawn pairs are assigned. Assign clue images and EK_Spawns in matching order.",
                this);
            return;
        }

        SelectedClueIndex = Random.Range(0, pairCount);
        SelectedClueImage = clueImages[SelectedClueIndex];
    }

    private void ApplyClueImageToPaper()
    {
        if (SelectedClueImage == null)
            return;

        if (clueSpriteRenderer != null)
            clueSpriteRenderer.sprite = SelectedClueImage;

        if (clueSurfaceRenderer == null || SelectedClueImage.texture == null)
            return;

        Material material = clueSurfaceRenderer.material;
        if (material.HasProperty(texturePropertyName))
            material.SetTexture(texturePropertyName, SelectedClueImage.texture);
        else if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", SelectedClueImage.texture);
    }

    private void SpawnExitKey()
    {
        Transform spawn = SelectedExitKeySpawn;
        if (exitKey == null || spawn == null)
        {
            Debug.LogWarning("[ImageClue] Cannot spawn ExitKey because the key or selected spawn is missing.", this);
            return;
        }

        exitKey.transform.position = spawn.position;
        if (useSpawnRotation)
            exitKey.transform.rotation = spawn.rotation;

        exitKey.SetActive(true);
    }
}

public class ImageClueInventoryViewer : MonoBehaviour
{
    private static ImageClueInventoryViewer _instance;

    private string _itemId;
    private string _selectedHint;
    private Sprite _clueImage;
    private GameObject _viewer;
    private PlayerController _playerController;
    private bool _isViewing;
    private bool _waitForMouseRelease;
    private string _lastSelectedId;

    public static void Ensure(string itemId, Sprite clueImage, string selectedHint)
    {
        if (_instance == null)
        {
            GameObject viewerObject = new GameObject("ImageClueInventoryViewer");
            _instance = viewerObject.AddComponent<ImageClueInventoryViewer>();
            DontDestroyOnLoad(viewerObject);
        }

        _instance.Configure(itemId, clueImage, selectedHint);
    }

    private void Configure(string itemId, Sprite clueImage, string selectedHint)
    {
        _itemId = itemId;
        _clueImage = clueImage;
        _selectedHint = selectedHint;
    }

    private void Update()
    {
        if (string.IsNullOrWhiteSpace(_itemId) || _clueImage == null)
            return;

        if (_isViewing)
        {
            if (_waitForMouseRelease && !Input.GetMouseButton(0))
                _waitForMouseRelease = false;

            if (!_waitForMouseRelease && Input.GetMouseButtonDown(0))
                CloseViewer();

            return;
        }

        string selectedId = CollectionInventory.SelectedItemId;
        if (selectedId != _lastSelectedId)
        {
            _lastSelectedId = selectedId;
            if (selectedId == _itemId)
                CollectionInventory.ShowBottomMessage(_selectedHint, 3f);
        }

        if (selectedId == _itemId && !CollectionInventory.IsInventoryOpen && Input.GetMouseButtonDown(0))
            OpenViewer();
    }

    private void OpenViewer()
    {
        EnsureViewerExists();
        _viewer.SetActive(true);
        _isViewing = true;
        _waitForMouseRelease = true;

        _playerController = FindFirstObjectByType<PlayerController>();
        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CollectionInventory.ShowBottomMessage("Left click to put away.", 2f);
    }

    private void CloseViewer()
    {
        if (_viewer != null)
            _viewer.SetActive(false);

        _isViewing = false;
        _waitForMouseRelease = false;

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void EnsureViewerExists()
    {
        if (_viewer != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _viewer = new GameObject("Image Clue Viewer", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(_viewer);

        Canvas canvas = _viewer.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        CanvasScaler scaler = _viewer.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image blocker = CreateImage(_viewer.transform, "Dim Background", new Color(0f, 0f, 0f, 0.82f));
        Stretch(blocker.rectTransform);

        Image paper = CreateImage(_viewer.transform, "Paper", new Color(0.92f, 0.90f, 0.84f, 1f));
        Place(paper.rectTransform, new Vector2(0.5f, 0.53f), new Vector2(980f, 680f));

        Image clue = CreateImage(paper.transform, "Clue Image", Color.white);
        Stretch(clue.rectTransform);
        clue.rectTransform.offsetMin = new Vector2(42f, 58f);
        clue.rectTransform.offsetMax = new Vector2(-42f, -78f);
        clue.sprite = _clueImage;
        clue.preserveAspect = true;

        Text hint = CreateText(_viewer.transform, "Hint", "Left click to put away.", font, 24, Color.white);
        Place(hint.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(620f, 50f));

        _viewer.SetActive(false);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = CreateRect(name, parent);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string name, string value, Font font, int size, Color color)
    {
        GameObject textObject = CreateRect(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        return text;
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
