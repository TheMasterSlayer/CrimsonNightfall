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
    [SerializeField] [Min(0.01f)] private float lightUpdateInterval = 0.08f;

    private Renderer[] _sourceRenderers;
    private Material _auraMaterial;
    private Light _light;
    private float _nextLightUpdateTime;

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

        if (Time.time < _nextLightUpdateTime)
            return;

        _nextLightUpdateTime = Time.time + lightUpdateInterval;
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, transform.GetInstanceID() * 0.01f);
        _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }

    private void CreateAuraCopies()
    {
        Shader auraShader = FindAuraShader();
        if (auraShader == null)
        {
            Debug.LogWarning("[LaserGlow] No compatible unlit shader was found. Laser aura copies will be skipped.", this);
            return;
        }

        _auraMaterial = new Material(auraShader);
        _auraMaterial.name = $"{name}_RuntimeLaserAura";
        SetMaterialColor(_auraMaterial, glowColor);
        SetMaterialColor(_auraMaterial, "_EmissionColor", glowColor * emissionStrength);

        if (_auraMaterial.HasProperty("_Surface"))
            _auraMaterial.SetFloat("_Surface", 1f);

        if (_auraMaterial.HasProperty("_Blend"))
            _auraMaterial.SetFloat("_Blend", 0f);

        if (_auraMaterial.HasProperty("_SrcBlend"))
            _auraMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (_auraMaterial.HasProperty("_DstBlend"))
            _auraMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (_auraMaterial.HasProperty("_ZWrite"))
            _auraMaterial.SetInt("_ZWrite", 0);

        _auraMaterial.EnableKeyword("_EMISSION");
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

    private static Shader FindAuraShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit") ??
               Shader.Find("Universal Render Pipeline/Lit") ??
               Shader.Find("Unlit/Color") ??
               Shader.Find("Sprites/Default") ??
               Shader.Find("Standard");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        SetMaterialColor(material, "_BaseColor", color);
        SetMaterialColor(material, "_Color", color);
    }

    private static void SetMaterialColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
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
