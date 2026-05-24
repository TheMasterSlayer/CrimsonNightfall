using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to a closet entrance GameObject.
/// Player walks up and presses E to hide — the AI can no longer see them.
/// Smoothly transitions the player into the closet and allows limited
/// horizontal mouse look while hiding. Press E again to exit.
///
/// Setup required:
///   - A Collider with "Is Trigger" checked on this GameObject
///   - A child empty GameObject named "HidePosition" inside the closet
///     (position it facing the door so the player peers inward)
/// </summary>
public class ClosetHide : MonoBehaviour
{
    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private Transform hidePosition;

    [Header("Hiding Look")]
    [SerializeField] private float maxLookAngle    = 35f;  // degrees left/right while hiding
    [SerializeField] private float lookSensitivity = 1.5f;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 0.35f;

    [Header("Audio")]
    [SerializeField] private AudioClip doorSound;
    [SerializeField] [Range(0f, 1f)] private float doorVolume = 1f;

    [Header("Prompts (wire up later)")]
    [SerializeField] private GameObject enterPrompt;
    [SerializeField] private GameObject exitPrompt;

    // ── Private State ──────────────────────────────────────────────────────

    private bool                _playerInRange   = false;
    private bool                _isHiding        = false;
    private bool                _isTransitioning = false;

    private PlayerController    _playerController;
    private CharacterController _characterController;
    private Transform           _playerTransform;

    private Vector3    _entryPosition;
    private Quaternion _entryRotation;
    private float      _hidingYaw = 0f; // horizontal look offset while hiding

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Update()
    {
        if (_isTransitioning) return;
        if (!_playerInRange && !_isHiding) return;

        if (_isHiding)
            HandleHidingLook();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (_isHiding) StartCoroutine(ExitHide());
            else           StartCoroutine(EnterHide());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange       = true;
        _playerTransform     = other.transform;
        _playerController    = other.GetComponent<PlayerController>();
        _characterController = other.GetComponent<CharacterController>();

        if (!_isHiding)
        {
            ShowPrompt(true, false);
            Debug.Log("[Closet] Press E to hide.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_isHiding) return; // ignore — player was moved inside by the script

        _playerInRange = false;
        ShowPrompt(false, false);
    }

    // ── Hide Logic ─────────────────────────────────────────────────────────

    private IEnumerator EnterHide()
    {
        if (_playerController == null || hidePosition == null) yield break;

        _isTransitioning = true;
        _hidingYaw       = 0f;

        _entryPosition = _playerTransform.position;
        _entryRotation = _playerTransform.rotation;

        _playerController.SetInputEnabled(false);

        if (doorSound != null)
            AudioSource.PlayClipAtPoint(doorSound, transform.position, doorVolume);

        // Smoothly move player into the closet
        yield return StartCoroutine(SmoothMove(
            _playerTransform.position, hidePosition.position,
            _playerTransform.rotation, hidePosition.rotation
        ));

        _playerController.IsHiding = true;
        _isHiding        = true;
        _isTransitioning = false;

        ShowPrompt(false, true);
        Debug.Log("[Closet] Player is hiding.");
    }

    private IEnumerator ExitHide()
    {
        if (_playerController == null) yield break;

        _isTransitioning           = true;
        _playerController.IsHiding = false;

        if (doorSound != null)
            AudioSource.PlayClipAtPoint(doorSound, transform.position, doorVolume);

        // Smoothly move player back to where they entered
        yield return StartCoroutine(SmoothMove(
            _playerTransform.position, _entryPosition,
            _playerTransform.rotation, _entryRotation
        ));

        _playerController.SetInputEnabled(true);
        _isHiding        = false;
        _playerInRange   = false;
        _isTransitioning = false;

        ShowPrompt(false, false);
        Debug.Log("[Closet] Player left hiding spot.");
    }

    // ── Smooth Movement Coroutine ──────────────────────────────────────────

    private IEnumerator SmoothMove(Vector3 fromPos, Vector3 toPos,
                                   Quaternion fromRot, Quaternion toRot)
    {
        float elapsed = 0f;

        // Disable CharacterController so it doesn't fight the lerp
        _characterController.enabled = false;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;

            // SmoothStep gives an ease-in/ease-out feel — not robotic
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);

            _playerTransform.position = Vector3.Lerp(fromPos, toPos, t);
            _playerTransform.rotation = Quaternion.Slerp(fromRot, toRot, t);

            yield return null;
        }

        // Snap to exact final values
        _playerTransform.position = toPos;
        _playerTransform.rotation = toRot;

        _characterController.enabled = true;
    }

    // ── Hiding Look ────────────────────────────────────────────────────────

    private void HandleHidingLook()
    {
        // Allow limited horizontal mouse look so the player can peek left/right
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        _hidingYaw   = Mathf.Clamp(_hidingYaw + mouseX, -maxLookAngle, maxLookAngle);

        // Rotate player body relative to the hide position's base direction
        _playerTransform.rotation = hidePosition.rotation * Quaternion.Euler(0f, _hidingYaw, 0f);
    }

    // ── UI Helper ──────────────────────────────────────────────────────────

    private void ShowPrompt(bool showEnter, bool showExit)
    {
        if (enterPrompt != null) enterPrompt.SetActive(showEnter);
        if (exitPrompt  != null) exitPrompt.SetActive(showExit);
    }
}
