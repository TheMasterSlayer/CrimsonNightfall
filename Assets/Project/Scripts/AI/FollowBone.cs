using UnityEngine;

/// <summary>
/// Attach to Eye_Left and Eye_Right (children of AIEntity).
/// Drag Cube.002 into the Head Mesh slot.
/// Uses SkinnedMeshRenderer.bounds.center to track the exact world-space
/// centre of the head mesh every frame — includes all animation bobbing.
/// </summary>
public class FollowBone : MonoBehaviour
{
    [Tooltip("Drag Cube.002 here")]
    [SerializeField] private SkinnedMeshRenderer headMesh;

    [Tooltip("Rotation source — drag the highest bone in Armature.001")]
    [SerializeField] private Transform neckBone;

    [Tooltip("X = left/right, Y = up/down, Z = forward/back from head centre")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, 0f);

    [Tooltip("How quickly eyes follow head movement. Lower = smoother/less movement. Try 5-15.")]
    [SerializeField] private float smoothSpeed = 10f;

    private Transform _entityRoot;

    private void Awake()
    {
        _entityRoot = transform.parent;

        // Force bounds to update every frame even when off-screen
        // Without this, bounds.center freezes when the entity is culled
        if (headMesh != null)
            headMesh.updateWhenOffscreen = true;
    }

    private void LateUpdate()
    {
        if (headMesh == null) return;

        Vector3 headCentre  = headMesh.bounds.center;
        Vector3 targetPos   = headCentre + _entityRoot.rotation * offset;

        // Smooth the position so the eyes follow the head without overshooting
        // Lower smoothSpeed = less movement influence from neck mesh bounds
        transform.position  = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);

        if (neckBone != null)
            transform.rotation = neckBone.rotation;
        else
            transform.rotation = _entityRoot.rotation;
    }
}
