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
    private Camera _overrideCamera;
    private float _overrideZoomAmount = -1f;

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

    public void TriggerJumpscare(Camera overrideJumpscareCamera)
    {
        TriggerJumpscare(overrideJumpscareCamera, -1f);
    }

    public void TriggerJumpscare(Camera overrideJumpscareCamera, float overrideZoomAmount)
    {
        if (_isPlaying)
            return;

        _overrideCamera = overrideJumpscareCamera;
        _overrideZoomAmount = overrideZoomAmount;
        StartCoroutine(JumpscareSequence());
    }

    private IEnumerator JumpscareSequence()
    {
        _isPlaying = true;
        CollectionInventory.ForceCloseInventory();
        Camera activeJumpscareCamera = _overrideCamera != null ? _overrideCamera : cameraFace;
        float activeCameraOriginalFov = activeJumpscareCamera != null
            ? activeJumpscareCamera.fieldOfView
            : 60f;
        float activeZoomAmount = _overrideZoomAmount >= 0f ? _overrideZoomAmount : zoomAmount;

        // Lock player input
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.SetInputEnabled(false);

        // Play stinger sound
        if (jumpscareStinger != null)
            AudioSource.PlayClipAtPoint(jumpscareStinger, transform.position, stingerVolume);

        // Switch cameras
        if (playerCamera != null) playerCamera.enabled = false;
        if (activeJumpscareCamera != null)
        {
            activeJumpscareCamera.enabled = true;
            activeJumpscareCamera.fieldOfView = Mathf.Max(1f, activeCameraOriginalFov - activeZoomAmount);
        }

        // Hold on the entity's face
        yield return new WaitForSeconds(holdDuration);

        // Clean up and trigger lose screen
        if (activeJumpscareCamera != null)
        {
            activeJumpscareCamera.enabled = false;
            activeJumpscareCamera.fieldOfView = activeCameraOriginalFov;
        }

        if (playerCamera != null) playerCamera.enabled = true;
        _overrideCamera = null;
        _overrideZoomAmount = -1f;

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
