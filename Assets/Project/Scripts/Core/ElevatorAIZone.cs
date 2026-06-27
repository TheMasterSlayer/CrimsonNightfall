using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class ElevatorAIZone : MonoBehaviour
{
    private static readonly List<ElevatorAIZone> Zones = new List<ElevatorAIZone>();
    private static bool _runtimeZonesEnsured;

    [SerializeField] private ElevatorDoorController elevatorDoors;
    [SerializeField] private Transform waitPoint;
    [SerializeField] private Vector3 fallbackSize = new Vector3(3.2f, 3f, 3.2f);
    [SerializeField] private Vector3 fallbackCenter = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float waitPointNavMeshSearchRadius = 2f;
    [SerializeField] private bool debugLogging;
    [SerializeField] private float debugLogInterval = 0.75f;

    private BoxCollider _zoneCollider;
    private float _nextDebugLogTime;

    public bool BlocksAI => elevatorDoors != null && !elevatorDoors.AreDoorsFullyOpen;
    public bool DebugLogging => debugLogging;
    public string ElevatorName => elevatorDoors != null ? elevatorDoors.name : name;

    private void Awake()
    {
        EnsureCollider();
    }

    private void OnEnable()
    {
        EnsureCollider();
        if (!Zones.Contains(this))
            Zones.Add(this);
    }

    private void OnDisable()
    {
        Zones.Remove(this);
    }

    private void Reset()
    {
        elevatorDoors = GetComponentInParent<ElevatorDoorController>();
        EnsureCollider();
        FitToElevatorRenderers();
    }

    private void OnValidate()
    {
        if (elevatorDoors == null)
            elevatorDoors = GetComponentInParent<ElevatorDoorController>();

        EnsureCollider();
    }

    public void Configure(ElevatorDoorController doors)
    {
        elevatorDoors = doors;
        EnsureCollider();
        FitToElevatorRenderers();
    }

    public bool Contains(Vector3 worldPosition)
    {
        EnsureCollider();
        Vector3 localPoint = transform.InverseTransformPoint(worldPosition) - _zoneCollider.center;
        Vector3 halfSize = _zoneCollider.size * 0.5f;

        return Mathf.Abs(localPoint.x) <= halfSize.x &&
               Mathf.Abs(localPoint.y) <= halfSize.y &&
               Mathf.Abs(localPoint.z) <= halfSize.z;
    }

    public Vector3 GetWaitPosition(Vector3 aiPosition)
    {
        Vector3 target = waitPoint != null
            ? waitPoint.position
            : _zoneCollider.ClosestPoint(aiPosition);

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, waitPointNavMeshSearchRadius, NavMesh.AllAreas))
            return hit.position;

        return target;
    }

    public static bool TryGetBlockingZoneForPosition(Vector3 position, out ElevatorAIZone zone)
    {
        EnsureRuntimeZones();

        if (TryGetZoneForPosition(position, out zone) && zone.BlocksAI)
        {
            zone.LogDebug(
                $"BLOCKING AI: player is inside '{zone.name}' for '{zone.ElevatorName}'. " +
                $"DoorsFullyOpen={zone.elevatorDoors != null && zone.elevatorDoors.AreDoorsFullyOpen}, " +
                $"PlayerPosition={position}");
            return true;
        }

        zone = null;
        return false;
    }

    public static bool TryGetZoneForPosition(Vector3 position, out ElevatorAIZone zone)
    {
        EnsureRuntimeZones();

        for (int i = Zones.Count - 1; i >= 0; i--)
        {
            ElevatorAIZone candidate = Zones[i];
            if (candidate == null)
            {
                Zones.RemoveAt(i);
                continue;
            }

            if (candidate.isActiveAndEnabled && candidate.Contains(position))
            {
                zone = candidate;
                return true;
            }
        }

        zone = null;
        return false;
    }

    public void LogAIWait(Vector3 aiPosition, Vector3 waitPosition)
    {
        LogDebug(
            $"AI waiting outside '{ElevatorName}'. " +
            $"AIPosition={aiPosition}, WaitPosition={waitPosition}, " +
            $"DoorsFullyOpen={elevatorDoors != null && elevatorDoors.AreDoorsFullyOpen}");
    }

    private void LogDebug(string message)
    {
        if (!debugLogging || Time.time < _nextDebugLogTime)
            return;

        _nextDebugLogTime = Time.time + Mathf.Max(0.05f, debugLogInterval);
        Debug.Log($"[ElevatorAIZone] {message}", this);
    }

    private static Transform GetPlayerTransform()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        return player != null ? player.transform : null;
    }

    public static void EnsureRuntimeZones()
    {
        if (_runtimeZonesEnsured)
            return;

        _runtimeZonesEnsured = true;
        foreach (ElevatorDoorController doors in FindObjectsByType<ElevatorDoorController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (doors.GetComponentInChildren<ElevatorAIZone>(true) != null)
                continue;

            GameObject zoneObject = new GameObject("ElevatorAIZone");
            zoneObject.transform.SetParent(doors.transform, false);
            ElevatorAIZone zone = zoneObject.AddComponent<ElevatorAIZone>();
            zone.Configure(doors);
        }
    }

    private void EnsureCollider()
    {
        if (_zoneCollider == null)
            _zoneCollider = GetComponent<BoxCollider>();

        if (_zoneCollider == null)
            _zoneCollider = gameObject.AddComponent<BoxCollider>();

        _zoneCollider.isTrigger = true;
        if (_zoneCollider.size == Vector3.zero)
        {
            _zoneCollider.center = fallbackCenter;
            _zoneCollider.size = fallbackSize;
        }
    }

    private void FitToElevatorRenderers()
    {
        EnsureCollider();

        Bounds localBounds = new Bounds();
        bool hasBounds = false;
        Transform root = elevatorDoors != null ? elevatorDoors.transform : transform;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            EncapsulateWorldBounds(renderer.bounds, ref localBounds, ref hasBounds);

        if (!hasBounds)
        {
            _zoneCollider.center = fallbackCenter;
            _zoneCollider.size = fallbackSize;
            return;
        }

        Vector3 size = localBounds.size;
        size.x = Mathf.Max(size.x, fallbackSize.x);
        size.y = Mathf.Max(size.y, fallbackSize.y);
        size.z = Mathf.Max(size.z, fallbackSize.z);

        _zoneCollider.center = localBounds.center;
        _zoneCollider.size = size;
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        EncapsulateWorldPoint(new Vector3(min.x, min.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, max.y, max.z), ref localBounds, ref hasBounds);
    }

    private void EncapsulateWorldPoint(Vector3 worldPoint, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        localBounds.Encapsulate(localPoint);
    }

    private void OnDrawGizmosSelected()
    {
        EnsureCollider();
        Gizmos.color = BlocksAI ? new Color(1f, 0.1f, 0.1f, 0.35f) : new Color(0.1f, 0.8f, 1f, 0.25f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(_zoneCollider.center, _zoneCollider.size);
        Gizmos.DrawWireCube(_zoneCollider.center, _zoneCollider.size);
        Gizmos.matrix = oldMatrix;
    }
}
