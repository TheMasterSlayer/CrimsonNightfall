using UnityEngine;

[DisallowMultipleComponent]
public class LaserGlow : MonoBehaviour
{
    [Header("Aura")]
    [SerializeField] private Color glowColor = new Color(1f, 0.02f, 0.02f, 0.28f);
    [SerializeField] private float auraScale = 1.8f;
    [SerializeField] private float emissionStrength = 5f;
    [SerializeField] private bool createAura = true;

    [Header("Light Flicker")]
    [SerializeField] private bool createLight = true;
    [SerializeField] private float lightRange = 2.5f;
    [SerializeField] private float minIntensity = 0.6f;
    [SerializeField] private float maxIntensity = 1.4f;
    [SerializeField] private float flickerSpeed = 18f;

    private Renderer[] _sourceRenderers;
    private Material _auraMaterial;
    private Light _light;

    private void Awake()
    {
        _sourceRenderers = GetComponentsInChildren<Renderer>(true);

        if (createAura)
            CreateAuraCopies();

        if (createLight)
            CreateGlowLight();
    }

    private void OnEnable()
    {
        LaserBarrier.LasersDisabled += DisableGlow;
    }

    private void OnDisable()
    {
        LaserBarrier.LasersDisabled -= DisableGlow;
    }

    private void Update()
    {
        if (_light == null)
            return;

        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, transform.GetInstanceID() * 0.01f);
        _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }

    private void CreateAuraCopies()
    {
        _auraMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _auraMaterial.name = $"{name}_RuntimeLaserAura";
        _auraMaterial.SetColor("_BaseColor", glowColor);
        _auraMaterial.SetColor("_EmissionColor", glowColor * emissionStrength);
        _auraMaterial.EnableKeyword("_EMISSION");
        _auraMaterial.SetFloat("_Surface", 1f);
        _auraMaterial.SetFloat("_Blend", 0f);
        _auraMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _auraMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _auraMaterial.SetInt("_ZWrite", 0);
        _auraMaterial.renderQueue = 3000;

        foreach (Renderer sourceRenderer in _sourceRenderers)
        {
            if (sourceRenderer == null || sourceRenderer.GetComponentInParent<LaserGlow>() != this)
                continue;

            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                continue;

            GameObject auraObject = new GameObject(sourceRenderer.name + "_Aura");
            auraObject.transform.SetParent(sourceRenderer.transform, false);
            auraObject.transform.localScale = new Vector3(auraScale, 1f, auraScale);

            MeshFilter auraFilter = auraObject.AddComponent<MeshFilter>();
            auraFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer auraRenderer = auraObject.AddComponent<MeshRenderer>();
            auraRenderer.sharedMaterial = _auraMaterial;
            auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraRenderer.receiveShadows = false;
        }
    }

    private void CreateGlowLight()
    {
        GameObject lightObject = new GameObject("Laser Glow Light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.zero;

        _light = lightObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = Color.red;
        _light.range = lightRange;
        _light.intensity = maxIntensity;
    }

    private void DisableGlow()
    {
        if (this != null)
            gameObject.SetActive(false);
    }
}
