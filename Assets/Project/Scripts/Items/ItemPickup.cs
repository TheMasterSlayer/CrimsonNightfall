using UnityEngine;

/// <summary>
/// Attach to any collectible item GameObject in the scene.
/// The player walks into the trigger zone and presses E to collect it.
///
/// Setup required on the item GameObject:
///   - A Collider component with "Is Trigger" checked (e.g. Sphere Collider)
///   - This script
/// Optional:
///   - A child mesh/sprite for the visual
///   - An AudioClip for the pickup sound
/// </summary>
public class ItemPickup : MonoBehaviour
{
    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("Item")]
    [SerializeField] private string itemName = "Key";

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] [Range(0f, 1f)] private float pickupVolume = 1f;

    [Header("Prompt UI (wire up later)")]
    [SerializeField] private GameObject promptObject; // optional — leave empty for now

    // ── Private State ──────────────────────────────────────────────────────

    private bool _playerInRange = false;
    private bool _collected     = false;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Update()
    {
        // Only check for input when the player is standing in the trigger zone
        if (_playerInRange && !_collected && Input.GetKeyDown(KeyCode.E))
            Collect();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange = true;
        ShowPrompt(true);
        Debug.Log($"[ItemPickup] Press E to pick up: {itemName}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange = false;
        ShowPrompt(false);
    }

    // ── Collection Logic ───────────────────────────────────────────────────

    private void Collect()
    {
        _collected     = true;
        _playerInRange = false;

        ShowPrompt(false);

        // Tell the GameManager an item was found
        bool allCollected = GameManager.Instance.OnItemCollected();

        Debug.Log($"[ItemPickup] Collected: {itemName}. All collected: {allCollected}");

        // Play the pickup sound at this position before the object is destroyed.
        // PlayClipAtPoint spawns a temporary AudioSource so the sound
        // finishes even after the GameObject is gone.
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);

        // Disable instead of Destroy so the sound has time to play
        gameObject.SetActive(false);
    }

    // ── UI Helper ──────────────────────────────────────────────────────────

    private void ShowPrompt(bool show)
    {
        if (promptObject != null)
            promptObject.SetActive(show);
    }
}