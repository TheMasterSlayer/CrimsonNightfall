using UnityEngine;

[DisallowMultipleComponent]
public class ElevatorButtonController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 4.5f;
    [SerializeField] [Range(5f, 120f)] private float interactionAngle = 75f;
    [SerializeField] private bool requireLineOfSight = false;

    [Header("Door")]
    [SerializeField] private ElevatorDoorController elevatorDoors;
    [SerializeField] private SecretElevatorKeyAccess secretKeyAccess;

    [Header("Messages")]
    [SerializeField] private bool showPrompt = true;
    [SerializeField] private string promptMessage = "Press E to use elevator";
    [SerializeField] private string noPowerMessage = "The elevator seems to be out of service. I wonder if I can power it on.";

    [Header("Audio")]
    [SerializeField] private AudioClip buttonPressedSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    private Bounds _bounds;

    private void Awake()
    {
        if (elevatorDoors == null)
            elevatorDoors = GetComponentInParent<ElevatorDoorController>();

        if (secretKeyAccess == null)
            secretKeyAccess = GetComponent<SecretElevatorKeyAccess>();

        RefreshBounds();
    }

    private void Update()
    {
        bool canInteract = IsFocused();

        if (showPrompt && canInteract)
            CollectionInventory.ShowBottomMessage(promptMessage, 0.15f);

        if (Input.GetKeyDown(KeyCode.E) && canInteract)
            Interact();
    }

    private void Interact()
    {
        PlayOneShot(buttonPressedSound);

        if (!FuseboxController.ElevatorPowerOn)
        {
            CollectionInventory.ShowBottomMessage(noPowerMessage);
            return;
        }

        if (!SecretElevatorKeyAccess.IsUnlocked &&
            CollectionInventory.HasItem("ElevatorKey") &&
            CollectionInventory.IsSelected("ElevatorKey") &&
            secretKeyAccess != null)
        {
            secretKeyAccess.TryUnlockFromSelectedItem();
            return;
        }

        if (elevatorDoors != null)
            elevatorDoors.Open();
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 focusPoint = GetClosestPoint(camera.transform.position);
        Vector3 toButton = focusPoint - camera.transform.position;
        float distance = toButton.magnitude;

        if (distance > interactionRange)
            return false;

        if (Vector3.Angle(camera.transform.forward, toButton) > interactionAngle)
            return false;

        if (!requireLineOfSight)
            return true;

        if (!Physics.Raycast(camera.transform.position, toButton.normalized, out RaycastHit hit, distance + 0.15f, ~0, QueryTriggerInteraction.Collide))
            return true;

        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private Vector3 GetClosestPoint(Vector3 fromPosition)
    {
        if (_bounds.size.sqrMagnitude <= 0.001f)
            RefreshBounds();

        return _bounds.size.sqrMagnitude > 0.001f
            ? _bounds.ClosestPoint(fromPosition)
            : transform.position;
    }

    private void RefreshBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds)
            {
                _bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                _bounds.Encapsulate(renderer.bounds);
            }
        }

        foreach (Collider collider in colliders)
        {
            if (!hasBounds)
            {
                _bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                _bounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
            _bounds = new Bounds(transform.position, Vector3.one * 0.5f);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, GetClosestPoint(transform.position), audioVolume);
    }
}
