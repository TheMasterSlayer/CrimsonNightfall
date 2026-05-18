using UnityEngine;

/// <summary>
/// The front door the player must reach to escape.
/// Interacting with it while all items are collected triggers the win condition.
/// If items are still missing, it shows a locked message instead.
///
/// Setup required on the door GameObject:
///   - A Collider with "Is Trigger" checked (Box Collider works well)
///   - This script
/// Optional:
///   - A child mesh for the door visual
///   - AudioClips for locked rattle and open creak sounds
/// </summary>
public class DoorController : MonoBehaviour
{
    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("Audio")]
    [SerializeField] private AudioClip lockedSound;   // rattle / thud
    [SerializeField] private AudioClip openSound;     // creak / swing
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    [Header("Door Swing (optional)")]
    [SerializeField] private Transform doorMesh;      // the visual door to rotate open
    [SerializeField] private float     openAngle     = 90f;
    [SerializeField] private float     openSpeed     = 2f;

    [Header("Prompt UI (wire up later)")]
    [SerializeField] private GameObject promptObject;

    // ── Private State ──────────────────────────────────────────────────────

    private bool _playerInRange = false;
    private bool _isOpen        = false;
    private bool _isOpening     = false;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Update()
    {
        if (_playerInRange && !_isOpen && Input.GetKeyDown(KeyCode.E))
            TryOpen();

        if (_isOpening)
            SwingDoorOpen();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange = true;

        if (!_isOpen)
        {
            ShowPrompt(true);

            // Give the player a hint in the console about how many items remain
            int remaining = GameManager.Instance.ItemsRemaining;
            if (remaining > 0)
                Debug.Log($"[Door] Locked. {remaining} item(s) still needed.");
            else
                Debug.Log("[Door] All items collected — press E to escape!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange = false;
        ShowPrompt(false);
    }

    // ── Door Logic ─────────────────────────────────────────────────────────

    private void TryOpen()
    {
        if (!GameManager.Instance.AllItemsCollected)
        {
            // Door is locked — play a rattle sound and log feedback
            if (lockedSound != null)
                AudioSource.PlayClipAtPoint(lockedSound, transform.position, audioVolume);

            int remaining = GameManager.Instance.ItemsRemaining;
            Debug.Log($"[Door] Still locked. Find {remaining} more item(s).");
            return;
        }

        // All items collected — open the door and trigger the win
        _isOpen    = true;
        _isOpening = true;

        ShowPrompt(false);

        if (openSound != null)
            AudioSource.PlayClipAtPoint(openSound, transform.position, audioVolume);

        Debug.Log("[Door] Opened! Player escapes.");

        GameManager.Instance.OnPlayerEscaped();
    }

    // ── Door Swing Animation ───────────────────────────────────────────────

    private void SwingDoorOpen()
    {
        // No mesh assigned — skip animation, the GameManager handles the rest
        if (doorMesh == null)
        {
            _isOpening = false;
            return;
        }

        // Smoothly rotate the door mesh around its Y axis until fully open
        float targetY    = transform.eulerAngles.y + openAngle;
        float currentY   = doorMesh.eulerAngles.y;
        float newY       = Mathf.MoveTowardsAngle(currentY, targetY, openSpeed * 60f * Time.deltaTime);

        doorMesh.eulerAngles = new Vector3(
            doorMesh.eulerAngles.x,
            newY,
            doorMesh.eulerAngles.z
        );

        if (Mathf.Abs(Mathf.DeltaAngle(newY, targetY)) < 0.5f)
            _isOpening = false;
    }

    // ── UI Helper ──────────────────────────────────────────────────────────

    private void ShowPrompt(bool show)
    {
        if (promptObject != null)
            promptObject.SetActive(show);
    }
}