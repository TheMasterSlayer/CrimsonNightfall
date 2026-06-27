using UnityEngine;

[RequireComponent(typeof(Light))]
public class RainbowPointLight : MonoBehaviour
{
    [SerializeField] private float cycleSpeed = 0.25f;
    [SerializeField] private float saturation = 1f;
    [SerializeField] private float value = 1f;
    [SerializeField] private bool forcePointLight = true;

    private Light _light;
    private float _hue;

    private void Awake()
    {
        _light = GetComponent<Light>();

        if (forcePointLight)
            _light.type = LightType.Point;
    }

    private void Update()
    {
        _hue = Mathf.Repeat(_hue + cycleSpeed * Time.deltaTime, 1f);
        _light.color = Color.HSVToRGB(_hue, Mathf.Clamp01(saturation), Mathf.Clamp01(value));
    }
}
