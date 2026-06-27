using UnityEngine;

[DisallowMultipleComponent]
public class ExitDoorController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 40f;
    [SerializeField] private string requiredKeyId = "ExitKey";

    [Header("Door")]
    [SerializeField] private Transform doorMesh;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 120f;

    [Header("Messages")]
    [SerializeField] private string lasersMessage = "I need to disable the lasers first.";
    [SerializeField] private string planksMessage = "The wooden planks are blocking the door.";
    [SerializeField] private string missingKeyMessage = "It seems I need the exit key. Maybe there is a clue somewhere?";
    [SerializeField] private string selectKeyMessage = "Select the Exit Key in your inventory first.";

    [Header("Audio")]
    [SerializeField] private AudioClip exitKeyUsedSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    private bool _isOpen;
    private float _currentAngle;

    private void Update()
    {
        AnimateDoor();

        if (!_isOpen && Input.GetKeyDown(KeyCode.E) && IsFocused())
            TryOpen();
    }

    private void TryOpen()
    {
        if (!LaserBarrier.AreLasersDisabled)
        {
            CollectionInventory.ShowBottomMessage(lasersMessage);
            return;
        }

        if (!WoodenPlanksController.AllPlanksRemoved)
        {
            CollectionInventory.ShowBottomMessage(planksMessage);
            return;
        }

        if (!CollectionInventory.HasItem(requiredKeyId))
        {
            CollectionInventory.ShowBottomMessage(missingKeyMessage);
            return;
        }

        if (!CollectionInventory.IsSelected(requiredKeyId))
        {
            CollectionInventory.ShowBottomMessage(selectKeyMessage);
            return;
        }

        _isOpen = true;
        PlayOneShot(exitKeyUsedSound);

        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerEscaped();
    }

    private void AnimateDoor()
    {
        if (!_isOpen || doorMesh == null)
            return;

        float nextAngle = Mathf.MoveTowards(_currentAngle, openAngle, openSpeed * Time.deltaTime);
        float delta = nextAngle - _currentAngle;
        doorMesh.Rotate(Vector3.up, delta, Space.Self);
        _currentAngle = nextAngle;
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 focusPoint = GetFocusPoint();
        Vector3 toDoor = focusPoint - camera.transform.position;
        if (toDoor.magnitude > interactionRange)
            return false;

        return Vector3.Angle(camera.transform.forward, toDoor) <= interactionAngle;
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
