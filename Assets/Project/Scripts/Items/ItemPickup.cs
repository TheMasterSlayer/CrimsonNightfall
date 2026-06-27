using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public static bool IsAnyItemInspecting { get; private set; }

    [Header("Item")]
    [SerializeField] private string itemName = "Key";
    [SerializeField] private bool grantsKey;
    [SerializeField] private string keyId = "RoomKey";
    [SerializeField] private Sprite inventoryIcon;
    [SerializeField] private bool addToInventory = true;
    [SerializeField] private string collectionMessage;
    [SerializeField] private bool collectionMessageRequiresEsc;
    [SerializeField] private bool showCollectionMessageAtBottom;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] [Range(0f, 1f)] private float pickupVolume = 1f;

    [Header("Prompt UI")]
    [SerializeField] private GameObject promptObject;

    [Header("Interaction")]
    [SerializeField] private bool allowLookInteraction = true;
    [SerializeField] private float interactDistance = 3f;

    [Header("Inspect Before Collect")]
    [SerializeField] private bool inspectBeforeCollect = true;
    [SerializeField] private float inspectDistance = 0.8f;
    [SerializeField] private float inspectMoveSpeed = 10f;
    [SerializeField] private float inspectRotationSpeed = 120f;
    [SerializeField] private string inspectHint = "Move mouse to inspect. Press E to collect. Press ESC to put back.";

    private bool _playerInRange;
    private bool _collected;
    private bool _isInspecting;

    private Transform _originalParent;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Collider[] _colliders;
    private PlayerController _playerController;

    private void Awake()
    {
        CollectionInventory.EnsureExists();
        _colliders = GetComponentsInChildren<Collider>();
    }

    private void OnDisable()
    {
        if (_isInspecting)
            IsAnyItemInspecting = false;
    }

    private void Update()
    {
        if (_collected)
            return;

        if (_isInspecting)
        {
            UpdateInspection();
            return;
        }

        if (ClosetHide.IsPlayerInAnyEntryZone)
            return;

        if (Input.GetKeyDown(KeyCode.E) && (_playerInRange || IsLookedAt()))
        {
            if (inspectBeforeCollect)
                StartInspection();
            else
                Collect();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _playerInRange = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other) || _isInspecting)
            return;

        _playerInRange = false;
        ShowPrompt(false);
    }

    private void StartInspection()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Collect();
            return;
        }

        _isInspecting = true;
        IsAnyItemInspecting = true;
        _originalParent = transform.parent;
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;

        _playerController = FindFirstObjectByType<PlayerController>();
        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        SetCollidersEnabled(false);
        ShowPrompt(false);
        CollectionInventory.ShowMessage(inspectHint, 2f);
    }

    private void UpdateInspection()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            StopInspection();
            return;
        }

        Vector3 targetPosition = camera.transform.position + camera.transform.forward * inspectDistance;
        transform.position = Vector3.Lerp(transform.position, targetPosition, inspectMoveSpeed * Time.deltaTime);

        float rotateX = Input.GetAxis("Mouse X") * inspectRotationSpeed * Time.deltaTime;
        float rotateY = Input.GetAxis("Mouse Y") * inspectRotationSpeed * Time.deltaTime;
        transform.Rotate(camera.transform.up, -rotateX, Space.World);
        transform.Rotate(camera.transform.right, rotateY, Space.World);

        if (Input.GetKeyDown(KeyCode.E))
            Collect();
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            StopInspection();
    }

    private void StopInspection()
    {
        _isInspecting = false;
        IsAnyItemInspecting = false;
        transform.SetParent(_originalParent, true);
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;

        SetCollidersEnabled(true);

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        if (_playerInRange)
            ShowPrompt(true);
    }

    private void Collect()
    {
        _collected = true;
        _playerInRange = false;
        _isInspecting = false;
        IsAnyItemInspecting = false;
        ShowPrompt(false);
        SetCollidersEnabled(false);

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        if (grantsKey)
            GameManager.Instance.AddKey(keyId);

        string displayName = GetDisplayName();

        if (addToInventory)
            CollectionInventory.AddItem(displayName, grantsKey ? keyId : itemName, inventoryIcon);

        SendMessage("OnItemCollectedByPlayer", SendMessageOptions.DontRequireReceiver);

        string message = string.IsNullOrWhiteSpace(collectionMessage)
            ? $"{displayName} has been collected."
            : collectionMessage;

        if (collectionMessageRequiresEsc)
            CollectionInventory.ShowEscMessage(message);
        else if (showCollectionMessageAtBottom)
            CollectionInventory.ShowBottomMessage(message);
        else
            CollectionInventory.ShowMessage(message);

        GameManager.Instance.OnItemCollected();

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);

        gameObject.SetActive(false);
    }

    private void ShowPrompt(bool show)
    {
        if (promptObject != null)
            promptObject.SetActive(show);
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

    private static bool IsPlayerCollider(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null)
            return;

        foreach (Collider itemCollider in _colliders)
        {
            if (itemCollider != null)
                itemCollider.enabled = enabled;
        }
    }

    private string GetDisplayName()
    {
        string source = itemName;
        if (string.IsNullOrWhiteSpace(source) || source == "Key")
            source = name;

        return ObjectNameToDisplayName(source);
    }

    private static string ObjectNameToDisplayName(string source)
    {
        source = source.Replace("_", " ");
        System.Text.StringBuilder result = new System.Text.StringBuilder();

        for (int i = 0; i < source.Length; i++)
        {
            char current = source[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(source[i - 1]))
                result.Append(' ');

            result.Append(current);
        }

        return result.ToString().Trim();
    }
}
