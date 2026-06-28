using UnityEngine;

[RequireComponent(typeof(Light))]
public class RainbowPointLight : MonoBehaviour
{
    [SerializeField] private float cycleSpeed = 0.25f;
    [SerializeField] private float saturation = 1f;
    [SerializeField] private float value = 1f;
    [SerializeField] private bool forcePointLight = true;
    [SerializeField] [Min(0.01f)] private float updateInterval = 0.1f;

    private Light _light;
    private float _hue;
    private float _nextUpdateTime;
    private float _lastUpdateTime;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _lastUpdateTime = Time.time;

        if (forcePointLight)
            _light.type = LightType.Point;
    }

    private void Update()
    {
        if (Time.time < _nextUpdateTime)
            return;

        float deltaTime = Time.time - _lastUpdateTime;
        _lastUpdateTime = Time.time;
        _nextUpdateTime = Time.time + updateInterval;

        _hue = Mathf.Repeat(_hue + cycleSpeed * deltaTime, 1f);
        _light.color = Color.HSVToRGB(_hue, Mathf.Clamp01(saturation), Mathf.Clamp01(value));
    }
}
