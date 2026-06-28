using UnityEngine;

/// <summary>
/// Attach to any Light to make it flicker.
/// Works for candles, faulty overhead lights, etc.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [SerializeField] private float minIntensity  = 0.8f;
    [SerializeField] private float maxIntensity  = 1.2f;
    [SerializeField] private float flickerSpeed  = 8f;
    [SerializeField] [Min(0.01f)] private float updateInterval = 0.1f;

    private Light _light;
    private float _baseIntensity;
    private float _noiseOffset;
    private float _nextUpdateTime;

    private void Awake()
    {
        _light         = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _noiseOffset   = Random.Range(0f, 100f); // each light flickers differently
    }

    private void Update()
    {
        if (Time.time < _nextUpdateTime)
            return;

        _nextUpdateTime = Time.time + updateInterval;
        float noise        = Mathf.PerlinNoise(_noiseOffset + Time.time * flickerSpeed, 0f);
        _light.intensity   = Mathf.Lerp(minIntensity, maxIntensity, noise) * _baseIntensity;
    }
}
