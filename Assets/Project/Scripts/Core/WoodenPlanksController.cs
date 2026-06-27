using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WoodenPlanksController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 40f;
    [SerializeField] private string requiredItemId = "Crowbar";

    [Header("Drop")]
    [SerializeField] private float dropForce = 1.5f;
    [SerializeField] private float torqueForce = 4f;
    [SerializeField] private bool disablePlankColliderAfterDrop;
    [SerializeField] private float colliderDisableDelay = 3f;

    [Header("Messages")]
    [SerializeField] private string lasersMessage = "I need to disable the lasers first.";
    [SerializeField] private string missingCrowbarMessage = "It seems I need an item to remove these planks.";
    [SerializeField] private string selectCrowbarMessage = "Select the Crowbar in your inventory first.";
    [SerializeField] private string removedMessage = "The plank comes loose.";

    [Header("Audio")]
    [SerializeField] private AudioClip plankRemovedSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    private readonly List<Transform> _planks = new List<Transform>();
    private int _nextPlankIndex;

    public static bool AllPlanksRemoved { get; private set; }

    private void Awake()
    {
        AllPlanksRemoved = false;
        _planks.Clear();

        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy)
            {
                LockPlankInPlace(child);
                _planks.Add(child);
            }
        }
    }

    private void Update()
    {
        if (AllPlanksRemoved)
            return;

        if (Input.GetKeyDown(KeyCode.E) && IsFocused())
            TryRemoveNextPlank();
    }

    private void TryRemoveNextPlank()
    {
        if (!LaserBarrier.AreLasersDisabled)
        {
            CollectionInventory.ShowBottomMessage(lasersMessage);
            return;
        }

        if (!CollectionInventory.HasItem(requiredItemId))
        {
            CollectionInventory.ShowBottomMessage(missingCrowbarMessage);
            return;
        }

        if (!CollectionInventory.IsSelected(requiredItemId))
        {
            CollectionInventory.ShowBottomMessage(selectCrowbarMessage);
            return;
        }

        Transform plank = GetNextPlank();
        if (plank == null)
        {
            AllPlanksRemoved = true;
            return;
        }

        DropPlank(plank);
        PlayOneShot(plankRemovedSound);
        CollectionInventory.ShowBottomMessage(removedMessage, 1.5f);

        if (_nextPlankIndex >= _planks.Count)
            AllPlanksRemoved = true;
    }

    private Transform GetNextPlank()
    {
        while (_nextPlankIndex < _planks.Count)
        {
            Transform plank = _planks[_nextPlankIndex++];
            if (plank != null && plank.gameObject.activeInHierarchy)
                return plank;
        }

        return null;
    }

    private void DropPlank(Transform plank)
    {
        plank.SetParent(null, true);

        Collider plankCollider = plank.GetComponent<Collider>();
        if (plankCollider == null)
            plankCollider = plank.gameObject.AddComponent<BoxCollider>();

        Rigidbody body = plank.GetComponent<Rigidbody>();
        if (body == null)
            body = plank.gameObject.AddComponent<Rigidbody>();

        body.isKinematic = false;
        body.useGravity = true;
        body.AddForce((Vector3.down + Random.insideUnitSphere * 0.35f) * dropForce, ForceMode.Impulse);
        body.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

        if (disablePlankColliderAfterDrop)
            StartCoroutine(DisableColliderLater(plankCollider));
    }

    private static void LockPlankInPlace(Transform plank)
    {
        Rigidbody body = plank.GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
    }

    private System.Collections.IEnumerator DisableColliderLater(Collider target)
    {
        yield return new WaitForSeconds(colliderDisableDelay);

        if (target != null)
            target.enabled = false;
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 focusPoint = GetFocusPoint();
        Vector3 toPlanks = focusPoint - camera.transform.position;
        if (toPlanks.magnitude > interactionRange)
            return false;

        return Vector3.Angle(camera.transform.forward, toPlanks) <= interactionAngle;
    }

    private Vector3 GetFocusPoint()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : transform.position;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, GetFocusPoint(), audioVolume);
    }
}
