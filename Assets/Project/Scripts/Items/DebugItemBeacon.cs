using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class DebugItemBeacon : MonoBehaviour
{
    [Header("Screen Marker")]
    [SerializeField] private bool showScreenMarker = true;
    [SerializeField] private string labelOverride;
    [SerializeField] private Color markerColor = Color.yellow;
    [SerializeField] private float screenMarkerSize = 18f;
    [SerializeField] private float screenEdgePadding = 35f;
    [SerializeField] private bool showDistance = true;
    [SerializeField] private bool showSpawnPointName = true;

    [Header("World Beacon")]
    [SerializeField] private bool showWorldBeacon = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private float beaconSize = 0.35f;
    [SerializeField] private float pulseSpeed = 3f;

    [Header("Object Glow")]
    [SerializeField] private bool tintObjectRenderers = true;
    [SerializeField] private float emissionStrength = 2.5f;

    private GameObject _beacon;
    private Material _beaconMaterial;
    private Texture2D _markerTexture;
    private GUIStyle _labelStyle;
    private Renderer[] _renderers;
    private RandomItemSpawn _randomItemSpawn;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _randomItemSpawn = GetComponent<RandomItemSpawn>();
        CreateMarkerTexture();
        CreateWorldBeacon();
        ApplyRendererTint();
    }

    private void OnEnable()
    {
        if (_beacon != null)
            _beacon.SetActive(showWorldBeacon);
    }

    private void OnDisable()
    {
        if (_beacon != null)
            _beacon.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_beacon != null)
            Destroy(_beacon);

        if (_markerTexture != null)
            Destroy(_markerTexture);
    }

    private void LateUpdate()
    {
        if (_beacon == null)
            return;

        _beacon.SetActive(showWorldBeacon);
        if (!showWorldBeacon)
            return;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.2f;
        _beacon.transform.position = transform.position + worldOffset;
        _beacon.transform.localScale = Vector3.one * beaconSize * pulse;
    }

    private void OnGUI()
    {
        if (!showScreenMarker || _markerTexture == null)
            return;

        EnsureLabelStyle();

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 worldPosition = transform.position + worldOffset;
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        bool behindCamera = screenPosition.z < 0f;

        if (behindCamera)
        {
            screenPosition.x = Screen.width - screenPosition.x;
            screenPosition.y = Screen.height - screenPosition.y;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        guiPosition.x = Mathf.Clamp(guiPosition.x, screenEdgePadding, Screen.width - screenEdgePadding);
        guiPosition.y = Mathf.Clamp(guiPosition.y, screenEdgePadding, Screen.height - screenEdgePadding);

        Rect markerRect = new Rect(
            guiPosition.x - screenMarkerSize * 0.5f,
            guiPosition.y - screenMarkerSize * 0.5f,
            screenMarkerSize,
            screenMarkerSize
        );

        Color previousColor = GUI.color;
        GUI.color = markerColor;
        GUI.DrawTexture(markerRect, _markerTexture);
        GUI.color = previousColor;

        string label = GetLabel(camera);
        Vector2 labelSize = _labelStyle.CalcSize(new GUIContent(label));
        Rect labelRect = new Rect(
            guiPosition.x - labelSize.x * 0.5f,
            guiPosition.y + screenMarkerSize * 0.5f + 4f,
            labelSize.x,
            labelSize.y
        );

        GUI.Label(labelRect, label, _labelStyle);
    }

    private void CreateWorldBeacon()
    {
        if (_beacon != null)
            return;

        _beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _beacon.name = $"{name}_DebugBeacon";
        _beacon.transform.SetParent(transform, false);

        Collider beaconCollider = _beacon.GetComponent<Collider>();
        if (beaconCollider != null)
            Destroy(beaconCollider);

        Renderer beaconRenderer = _beacon.GetComponent<Renderer>();
        _beaconMaterial = CreateAlwaysVisibleMaterial(markerColor);
        if (beaconRenderer != null && _beaconMaterial != null)
            beaconRenderer.sharedMaterial = _beaconMaterial;
    }

    private void ApplyRendererTint()
    {
        if (!tintObjectRenderers || _renderers == null)
            return;

        foreach (Renderer itemRenderer in _renderers)
        {
            if (itemRenderer == null || itemRenderer.gameObject == _beacon)
                continue;

            foreach (Material material in itemRenderer.materials)
            {
                if (material == null)
                    continue;

                if (material.HasProperty("_Color"))
                    material.color = Color.Lerp(material.color, markerColor, 0.45f);

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", markerColor * emissionStrength);
                }
            }
        }
    }

    private Material CreateAlwaysVisibleMaterial(Color color)
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        Material material = shader != null
            ? new Material(shader)
            : new Material(Shader.Find("Standard"));

        material.hideFlags = HideFlags.HideAndDontSave;
        material.color = color;

        if (shader != null)
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            material.renderQueue = 5000;
        }

        return material;
    }

    private void CreateMarkerTexture()
    {
        _markerTexture = new Texture2D(1, 1);
        _markerTexture.hideFlags = HideFlags.HideAndDontSave;
        _markerTexture.SetPixel(0, 0, Color.white);
        _markerTexture.Apply();
    }

    private void EnsureLabelStyle()
    {
        if (_labelStyle != null)
        {
            _labelStyle.normal.textColor = markerColor;
            return;
        }

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = markerColor
            }
        };
    }

    private string GetLabel(Camera camera)
    {
        string displayName = string.IsNullOrWhiteSpace(labelOverride) ? name : labelOverride;
        string label = displayName;

        if (showSpawnPointName && _randomItemSpawn != null)
            label += $"\nSpawn: {_randomItemSpawn.SelectedSpawnPointName}";

        float distance = Vector3.Distance(camera.transform.position, transform.position);
        if (showDistance)
            label += $"\n{distance:0}m";

        return label;
    }

    private void OnValidate()
    {
        screenMarkerSize = Mathf.Max(4f, screenMarkerSize);
        screenEdgePadding = Mathf.Max(0f, screenEdgePadding);
        beaconSize = Mathf.Max(0.01f, beaconSize);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        emissionStrength = Mathf.Max(0f, emissionStrength);
    }
}
