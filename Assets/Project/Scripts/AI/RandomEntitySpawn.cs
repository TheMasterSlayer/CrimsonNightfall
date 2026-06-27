using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Places the entity at one randomly selected spawn point before its AI starts.
/// Spawn points should be positioned on or close to the baked NavMesh.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[DefaultExecutionOrder(-1000)]
public class RandomEntitySpawn : MonoBehaviour
{
    [Header("Possible Spawn Locations")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("NavMesh Placement")]
    [SerializeField] [Min(0.1f)] private float navMeshSearchRadius = 3f;
    [SerializeField] private bool useSpawnPointRotation = true;

    private void Start()
    {
        Transform selectedPoint = SelectRandomPoint();
        if (selectedPoint == null)
        {
            Debug.LogWarning(
                $"[RandomEntitySpawn] {name} has no valid spawn points and will remain at its scene position.",
                this);
            return;
        }

        if (!NavMesh.SamplePosition(
                selectedPoint.position, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning(
                $"[RandomEntitySpawn] No NavMesh was found near {selectedPoint.name}. " +
                $"{name} will remain at its scene position.",
                selectedPoint);
            return;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        agent.Warp(hit.position);

        if (useSpawnPointRotation)
            transform.rotation = selectedPoint.rotation;
    }

    private Transform SelectRandomPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        int validPointCount = 0;
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                validPointCount++;
        }

        if (validPointCount == 0)
            return null;

        int selectedIndex = Random.Range(0, validPointCount);
        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            if (selectedIndex == 0)
                return point;

            selectedIndex--;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null)
            return;

        Gizmos.color = new Color(0.85f, 0.05f, 0.08f, 0.9f);
        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            Gizmos.DrawWireSphere(point.position, 0.4f);
            Gizmos.DrawLine(point.position, point.position + point.forward);
        }
    }
}
