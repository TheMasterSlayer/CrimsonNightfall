using UnityEngine;

/// <summary>
/// Attach to EntityModel. Automatically finds the root bone of the skeleton
/// and locks its XZ position every frame so a walk animation that moves
/// forward doesn't cause a snap/teleport when it loops.
/// The NavMeshAgent on the parent handles all actual world movement.
/// </summary>
public class EntityModelRoot : MonoBehaviour
{
    private Transform _rootBone;
    private float     _startX;
    private float     _startZ;

    private void Start()
    {
        // Search up to 5 levels deep for the real moving bone
        _rootBone = FindMovingRootBone(transform, 5);

        if (_rootBone != null)
        {
            _startX = 0f;
            _startZ = 0f;
        }
        else
        {
            Debug.LogWarning("[EntityModelRoot] Could not find root bone.");
        }
    }

    private void LateUpdate()
    {
        transform.localPosition = Vector3.zero;

        if (_rootBone != null)
        {
            Vector3 p = _rootBone.localPosition;
            _rootBone.localPosition = new Vector3(_startX, p.y, _startZ);
        }
    }

    // Recursively searches for the first bone that has multiple children
    // (indicating it's a structural root bone, not just a container or leaf)
    private Transform FindMovingRootBone(Transform parent, int depthRemaining)
    {
        if (depthRemaining <= 0) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.GetComponent<SkinnedMeshRenderer>() != null) continue;
            if (child.GetComponent<MeshRenderer>()       != null) continue;

            // A bone with multiple children is the structural root
            if (child.childCount > 1) return child;

            // Keep searching deeper
            Transform deeper = FindMovingRootBone(child, depthRemaining - 1);
            if (deeper != null) return deeper;
        }

        return null;
    }
}
