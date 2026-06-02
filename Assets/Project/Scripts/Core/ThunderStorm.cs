using System.Collections;
using UnityEngine;

/// <summary>
/// Handles random lightning flashes and thunder audio.
/// Attach to an empty GameObject in the scene.
/// Requires a Directional or Point Light assigned as the lightning light.
/// </summary>
public class ThunderStorm : MonoBehaviour
{
    [Header("Lightning")]
    [SerializeField] private Light lightningLight;
    [SerializeField] private float lightningIntensity  = 8f;
    [SerializeField] private Color lightningColor      = new Color(0.85f, 0.9f, 1f);

    [Header("Timing")]
    [SerializeField] private float minTimeBetween = 8f;
    [SerializeField] private float maxTimeBetween = 25f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] thunderClips; // assign 2-3 different thunder sounds
    [SerializeField] [Range(0f, 1f)] private float thunderVolume = 0.8f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f; // 2D — thunder fills the whole room
        _audioSource.volume       = thunderVolume;
    }

    private void Start()
    {
        if (lightningLight != null)
            lightningLight.intensity = 0f;

        StartCoroutine(ThunderLoop());
    }

    // ── Thunder Loop ───────────────────────────────────────────────────────

    private IEnumerator ThunderLoop()
    {
        while (true)
        {
            // Wait a random amount of time between strikes
            yield return new WaitForSeconds(Random.Range(minTimeBetween, maxTimeBetween));

            yield return StartCoroutine(LightningFlash());
        }
    }

    private IEnumerator LightningFlash()
    {
        // Some strikes do a double flash for realism
        int flashes = Random.value > 0.5f ? 2 : 1;

        for (int i = 0; i < flashes; i++)
        {
            // Flash on
            if (lightningLight != null)
            {
                lightningLight.color     = lightningColor;
                lightningLight.intensity = lightningIntensity;
            }

            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));

            // Flash off
            if (lightningLight != null)
                lightningLight.intensity = 0f;

            if (i < flashes - 1)
                yield return new WaitForSeconds(Random.Range(0.05f, 0.1f));
        }

        // Play thunder sound slightly after the flash (sound travels slower than light)
        yield return new WaitForSeconds(Random.Range(0.1f, 0.8f));

        if (thunderClips != null && thunderClips.Length > 0)
        {
            AudioClip clip = thunderClips[Random.Range(0, thunderClips.Length)];
            _audioSource.PlayOneShot(clip, thunderVolume);
        }
    }
}