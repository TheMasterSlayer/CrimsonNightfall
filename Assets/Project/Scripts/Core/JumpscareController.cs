using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the Player GameObject.
/// When the AI catches the player, disables the player camera,
/// switches to CameraFace on the entity, and zooms in slightly.
/// </summary>
public class JumpscareController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera cameraFace;     // the Camera component on the entity model

    [Header("Settings")]
    [SerializeField] private float zoomAmount   = 5f;    // how many FOV degrees to zoom in
    [SerializeField] private float holdDuration = 1.5f;  // seconds to hold before lose screen

    [Header("Audio")]
    [SerializeField] private AudioClip jumpscareStinger;
    [SerializeField] [Range(0f, 1f)] private float stingerVolume = 1f;

    private void Awake()
    {
        // Make sure CameraFace starts disabled
        if (cameraFace != null)
            cameraFace.enabled = false;
    }

    /// <summary>Called by AIEntity when the player is caught.</summary>
    public void TriggerJumpscare()
    {
        StartCoroutine(JumpscareSequence());
    }

    private IEnumerator JumpscareSequence()
    {
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
            cameraFace.fieldOfView -= zoomAmount;
        }

        // Hold on the entity's face
        yield return new WaitForSeconds(holdDuration);

        // Clean up and trigger lose screen
        if (cameraFace   != null) cameraFace.enabled   = false;
        if (playerCamera != null) playerCamera.enabled = true;

        GameManager.Instance.OnPlayerCaught();
    }
}
