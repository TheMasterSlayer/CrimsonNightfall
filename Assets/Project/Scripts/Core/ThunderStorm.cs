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

    [Header("Rain Ambience")]
    [SerializeField] private AudioClip rainClip;
    [SerializeField] [Range(0f, 1f)] private float rainRoomVolume = 0.07f;
    [SerializeField] [Range(0f, 1f)] private float rainWindowVolume = 0.32f;
    [SerializeField] [Min(0f)] private float rainNearDistance = 2.5f;
    [SerializeField] [Min(0f)] private float rainFarDistance = 10f;
    [SerializeField] [Min(0.01f)] private float rainVolumeSmoothTime = 0.5f;

    private static readonly string[] RainWindowNamePrefixes =
    {
        "BoxSashWindow",
        "StableDoor",
        "RoundWindowWood"
    };

    private AudioSource _audioSource;
    private AudioSource _rainSource;
    private Transform _rainListener;
    private Transform[] _rainWindows;
    private float _rainVolumeVelocity;

    private void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f; // 2D — thunder fills the whole room
        _audioSource.volume       = thunderVolume;

        _rainSource = gameObject.AddComponent<AudioSource>();
        _rainSource.clip = rainClip;
        _rainSource.loop = true;
        _rainSource.playOnAwake = false;
        _rainSource.spatialBlend = 0f;
        _rainSource.volume = rainRoomVolume;
    }

    private void Start()
    {
        if (lightningLight != null)
            lightningLight.intensity = 0f;

        CacheRainWindows();

        if (rainClip != null)
            _rainSource.Play();

        StartCoroutine(ThunderLoop());
    }

    private void Update()
    {
        UpdateRainVolume();
    }

    private void CacheRainWindows()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        _rainListener = player != null ? player.transform : null;

        Transform[] sceneTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        System.Collections.Generic.List<Transform> windows = new();

        foreach (Transform sceneTransform in sceneTransforms)
        {
            foreach (string prefix in RainWindowNamePrefixes)
            {
                if (sceneTransform.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    windows.Add(sceneTransform);
                    break;
                }
            }
        }

        _rainWindows = windows.ToArray();
    }

    private void UpdateRainVolume()
    {
        if (_rainSource == null || !_rainSource.isPlaying)
            return;

        float targetVolume = rainRoomVolume;

        if (_rainListener != null && _rainWindows != null && _rainWindows.Length > 0)
        {
            float nearestSqrDistance = float.PositiveInfinity;

            foreach (Transform window in _rainWindows)
            {
                if (window == null)
                    continue;

                float sqrDistance = (_rainListener.position - window.position).sqrMagnitude;
                nearestSqrDistance = Mathf.Min(nearestSqrDistance, sqrDistance);
            }

            float nearestDistance = Mathf.Sqrt(nearestSqrDistance);
            float farDistance = Mathf.Max(rainNearDistance + 0.01f, rainFarDistance);
            float proximity = 1f - Mathf.InverseLerp(
                rainNearDistance, farDistance, nearestDistance);
            targetVolume = Mathf.Lerp(rainRoomVolume, rainWindowVolume, proximity);
        }

        _rainSource.volume = Mathf.SmoothDamp(
            _rainSource.volume, targetVolume, ref _rainVolumeVelocity, rainVolumeSmoothTime);
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
