using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class SCPPropPhysicsObject : MonoBehaviour
{
    [Header("Weight")]
    [SerializeField] private bool autoCalculateMass = true;
    [SerializeField] private float minimumMass = 0.35f;
    [SerializeField] private float maximumMass = 18f;
    [SerializeField] private float massPerCubicMeter = 3.5f;

    [Header("Impact Scaling")]
    [SerializeField] private float smallObjectBoost = 1.65f;
    [SerializeField] private float largeObjectResistance = 1.15f;
    [SerializeField] private float referenceSize = 2f;

    private Rigidbody _body;
    private float _boundsMagnitude = 1f;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        RefreshPhysicsProfile();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        RefreshPhysicsProfile();
    }

    public void RefreshPhysicsProfile()
    {
        if (_body == null)
            _body = GetComponent<Rigidbody>();

        EnsureDynamicSafeColliders();

        Bounds bounds = CalculateBounds();
        Vector3 size = bounds.size;
        _boundsMagnitude = Mathf.Max(0.1f, size.magnitude);

        if (autoCalculateMass && _body != null)
        {
            float volume = Mathf.Max(0.02f, size.x * size.y * size.z);
            _body.mass = Mathf.Clamp(volume * massPerCubicMeter, minimumMass, maximumMass);
        }
    }

    public void ApplyScpImpact(
        Vector3 scpPosition,
        Vector3 scpVelocity,
        float baseForce,
        float maxForce,
        float torque,
        float upwardLift,
        float fullForceSpeed)
    {
        if (_body == null)
            _body = GetComponent<Rigidbody>();

        if (_body == null)
            return;

        PrepareForStampede();
        _body.isKinematic = false;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.WakeUp();

        Vector3 direction = transform.position - scpPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = scpVelocity.sqrMagnitude > 0.001f ? scpVelocity : transform.forward;

        direction.Normalize();
        direction = (direction + Vector3.up * upwardLift).normalized;

        float speedRatio = fullForceSpeed > 0f ? Mathf.Clamp01(scpVelocity.magnitude / fullForceSpeed) : 1f;
        float sizeRatio = Mathf.Clamp(_boundsMagnitude / Mathf.Max(0.1f, referenceSize), 0.2f, 4f);
        float sizeMultiplier = Mathf.Lerp(smallObjectBoost, largeObjectResistance, Mathf.InverseLerp(0.2f, 4f, sizeRatio));
        float massResistance = 1f / Mathf.Pow(Mathf.Max(0.1f, _body.mass), 0.35f);
        float force = Mathf.Min(maxForce, baseForce * Mathf.Lerp(0.45f, 1f, speedRatio) * sizeMultiplier * massResistance);

        Vector3 hitPoint = _body.worldCenterOfMass - direction * Mathf.Max(0.25f, _boundsMagnitude * 0.25f);
        _body.AddForceAtPosition(direction * force, hitPoint, ForceMode.Impulse);
        _body.AddTorque(Random.onUnitSphere * torque * sizeMultiplier, ForceMode.Impulse);
    }

    private Bounds CalculateBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private void EnsureDynamicSafeColliders()
    {
        foreach (MeshCollider meshCollider in GetComponentsInChildren<MeshCollider>(true))
        {
            if (meshCollider.convex)
                continue;

            meshCollider.convex = true;
        }
    }

    private void PrepareForStampede()
    {
        EnsureDynamicSafeColliders();

        foreach (NavMeshObstacle obstacle in GetComponentsInChildren<NavMeshObstacle>(true))
            obstacle.enabled = false;
    }
}
