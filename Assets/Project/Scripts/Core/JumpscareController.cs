using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the Player GameObject.
/// When the AI catches the player, disables the player camera,
/// switches to the assigned jumpscare camera, and zooms in slightly.
/// </summary>
public class JumpscareController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [InspectorName("Jumpscare Camera")]
    [SerializeField] private Camera cameraFace;

    [Header("Settings")]
    [SerializeField] private float zoomAmount = 5f;
    [SerializeField]
    [Min(0.1f)]
    [InspectorName("Jumpscare Duration (Seconds)")]
    private float holdDuration = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip jumpscareStinger;
    [SerializeField] [Range(0f, 1f)] private float stingerVolume = 1f;

    private bool _isPlaying;
    private float _cameraFaceOriginalFov;

    private void Awake()
    {
        ResolveCameras();

        if (cameraFace != null)
        {
            _cameraFaceOriginalFov = cameraFace.fieldOfView;
            cameraFace.enabled = false;
        }
    }

    /// <summary>Called by AIEntity when the player is caught.</summary>
    public void TriggerJumpscare()
    {
        if (_isPlaying)
            return;

        StartCoroutine(JumpscareSequence());
    }

    private IEnumerator JumpscareSequence()
    {
        _isPlaying = true;
        CollectionInventory.ForceCloseInventory();

        // Lock player input
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.SetInputEnabled(false);

        // Play stinger sound
        if (jumpscareStinger != null)
            AudioSource.PlayClipAtPoint(jumpscareStinger, transform.position, stingerVolume);

        // Switch cameras
        if (playerCamera != null) playerCamera.enabled = false;
        if (cameraFace   != null)
        {
            cameraFace.enabled      = true;
            cameraFace.fieldOfView = Mathf.Max(1f, _cameraFaceOriginalFov - zoomAmount);
        }

        // Hold on the entity's face
        yield return new WaitForSeconds(holdDuration);

        // Clean up and trigger lose screen
        if (cameraFace != null)
        {
            cameraFace.enabled = false;
            cameraFace.fieldOfView = _cameraFaceOriginalFov;
        }

        if (playerCamera != null) playerCamera.enabled = true;

        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerCaught();
    }

    private void ResolveCameras()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (cameraFace != null)
            return;

        foreach (Camera sceneCamera in FindObjectsByType<Camera>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sceneCamera == playerCamera)
                continue;

            if (sceneCamera.name == "Main Camera")
            {
                cameraFace = sceneCamera;
                cameraFace.enabled = false;
                return;
            }
        }

        Debug.LogError("[Jumpscare] The assigned jumpscare camera could not be found.", this);
    }
}
