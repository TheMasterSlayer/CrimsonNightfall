using UnityEngine;

/// <summary>
/// The front door the player must reach to escape.
/// Interacting with it while all items are collected triggers the win condition.
/// If items are still missing, it plays locked feedback instead.
/// </summary>
public class DoorController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip lockedSound;
    [SerializeField] private AudioClip openSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    [Header("Door Swing (optional)")]
    [SerializeField] private Transform doorMesh;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    [Header("Prompt UI (wire up later)")]
    [SerializeField] private GameObject promptObject;

    private bool _playerInRange;
    private bool _isOpen;
    private bool _isOpening;

    private void Update()
    {
        if (_playerInRange && !_isOpen && Input.GetKeyDown(KeyCode.E))
            TryOpen();

        if (_isOpening)
            SwingDoorOpen();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInRange = true;

        if (!_isOpen)
            ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInRange = false;
        ShowPrompt(false);
    }

    private void TryOpen()
    {
        if (!GameManager.Instance.AllItemsCollected)
        {
            if (lockedSound != null)
                AudioSource.PlayClipAtPoint(lockedSound, transform.position, audioVolume);

            return;
        }

        _isOpen = true;
        _isOpening = true;

        ShowPrompt(false);

        if (openSound != null)
            AudioSource.PlayClipAtPoint(openSound, transform.position, audioVolume);

        GameManager.Instance.OnPlayerEscaped();
    }

    private void SwingDoorOpen()
    {
        if (doorMesh == null)
        {
            _isOpening = false;
            return;
        }

        float targetY = transform.eulerAngles.y + openAngle;
        float currentY = doorMesh.eulerAngles.y;
        float newY = Mathf.MoveTowardsAngle(currentY, targetY, openSpeed * 60f * Time.deltaTime);

        doorMesh.eulerAngles = new Vector3(
            doorMesh.eulerAngles.x,
            newY,
            doorMesh.eulerAngles.z
        );

        if (Mathf.Abs(Mathf.DeltaAngle(newY, targetY)) < 0.5f)
            _isOpening = false;
    }

    private void ShowPrompt(bool show)
    {
        if (promptObject != null)
            promptObject.SetActive(show);
    }
}
