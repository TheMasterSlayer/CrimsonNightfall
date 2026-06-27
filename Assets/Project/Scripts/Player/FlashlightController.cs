using UnityEngine;

/// <summary>
/// Attach to a Spotlight that is a child of PlayerCamera.
/// Press F to toggle. Exposes IsFlashlightOn for the AI to check.
///
/// Setup:
///   - Right-click PlayerCamera → Light → Spot Light
///   - Set Range: 25, Spot Angle: 60, Color: warm white
///   - Add this script to that Spot Light GameObject
/// </summary>
[RequireComponent(typeof(Light))]
public class FlashlightController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;

    [Header("Audio")]
    [SerializeField] private AudioClip clickSound;
    [SerializeField] [Range(0f, 1f)] private float clickVolume = 0.8f;

    private Light _light;

    // The AI reads this to know whether the flashlight is on
    public bool IsFlashlightOn => _light != null && _light.enabled;

    private void Awake()
    {
        _light         = GetComponent<Light>();
        _light.enabled = false; // starts off — player must turn it on
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    private void Toggle()
    {
        _light.enabled = !_light.enabled;

        if (clickSound != null)
            AudioSource.PlayClipAtPoint(clickSound, transform.position, clickVolume);
    }
}
