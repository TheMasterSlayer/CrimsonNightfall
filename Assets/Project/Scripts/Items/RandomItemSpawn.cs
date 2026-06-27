using UnityEngine;

/// <summary>
/// Places this collectible at one randomly selected spawn point when the scene starts.
/// Create empty GameObjects for the possible locations and assign their transforms here.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class RandomItemSpawn : MonoBehaviour
{
    [Header("Possible Spawn Locations")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Placement")]
    [SerializeField] private bool useSpawnPointRotation = true;

    public Transform SelectedSpawnPoint { get; private set; }
    public string SelectedSpawnPointName => SelectedSpawnPoint != null ? SelectedSpawnPoint.name : "Scene Position";

    private void Awake()
    {
        Transform selectedPoint = SelectRandomPoint();
        if (selectedPoint == null)
        {
            Debug.LogWarning(
                $"[RandomItemSpawn] {name} has no valid spawn points and will remain at its scene position.",
                this);
            return;
        }

        SelectedSpawnPoint = selectedPoint;
        transform.position = selectedPoint.position;

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

        Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.9f);
        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            Gizmos.DrawWireSphere(point.position, 0.2f);
            Gizmos.DrawLine(point.position, point.position + point.forward * 0.45f);
        }
    }
}
