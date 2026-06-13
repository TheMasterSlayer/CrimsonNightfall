using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomDoorController : MonoBehaviour
{
    private static readonly List<RoomDoorController> Doors = new List<RoomDoorController>();

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 35f;

    [Header("Door Swing")]
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private bool hingeOnOppositeSide;

    [Header("Audio")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.8f;

    private Bounds _bounds;
    private Vector3 _hingePoint;
    private float _currentAngle;
    private float _targetAngle;
    private bool _initialized;
    private AudioSource _audioSource;

    public bool IsOpen => Mathf.Abs(_targetAngle) > 0.1f;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!Doors.Contains(this))
            Doors.Add(this);
    }

    private void OnDisable()
    {
        Doors.Remove(this);
    }

    private void Update()
    {
        AnimateDoor();

        if (Input.GetKeyDown(KeyCode.E) && FindFocusedDoor() == this)
            Toggle();
    }

    public void Toggle()
    {
        _targetAngle = IsOpen ? 0f : ChooseOpeningAngle();
        PlaySound(IsOpen ? openSound : closeSound);
    }

    private float ChooseOpeningAngle()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return openAngle;

        Vector3 centerOffset = _bounds.center - _hingePoint;
        Vector3 positiveCenter = _hingePoint + Quaternion.AngleAxis(openAngle, Vector3.up) * centerOffset;
        Vector3 negativeCenter = _hingePoint + Quaternion.AngleAxis(-openAngle, Vector3.up) * centerOffset;

        float positiveDistance = Vector3.SqrMagnitude(positiveCenter - camera.transform.position);
        float negativeDistance = Vector3.SqrMagnitude(negativeCenter - camera.transform.position);
        return positiveDistance >= negativeDistance ? openAngle : -openAngle;
    }

    private void Initialize()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[RoomDoor] {name} has no renderers to calculate a hinge from.", this);
            return;
        }

        _bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            _bounds.Encapsulate(renderers[i].bounds);

        bool widthAlongX = _bounds.size.x >= _bounds.size.z;
        float side = hingeOnOppositeSide ? 1f : -1f;
        _hingePoint = _bounds.center;

        if (widthAlongX)
            _hingePoint.x += side * _bounds.extents.x;
        else
            _hingePoint.z += side * _bounds.extents.z;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = 12f;
        _initialized = true;
    }

    private void AnimateDoor()
    {
        if (!_initialized)
            return;

        float nextAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, rotationSpeed * Time.deltaTime);
        float delta = nextAngle - _currentAngle;

        if (Mathf.Abs(delta) > 0.001f)
            transform.RotateAround(_hingePoint, Vector3.up, delta);

        _currentAngle = nextAngle;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || _audioSource == null)
            return;

        _audioSource.PlayOneShot(clip, volume);
    }

    private static RoomDoorController FindFocusedDoor()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return null;

        RoomDoorController bestDoor = null;
        float bestScore = float.MaxValue;

        foreach (RoomDoorController door in Doors)
        {
            if (door == null || !door._initialized)
                continue;

            Vector3 toDoor = door._bounds.center - camera.transform.position;
            float distance = toDoor.magnitude;
            if (distance > door.interactionRange)
                continue;

            float angle = Vector3.Angle(camera.transform.forward, toDoor);
            if (angle > door.interactionAngle)
                continue;

            float score = distance + angle * 0.04f;
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    private void OnDrawGizmosSelected()
    {
        if (_initialized)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_hingePoint, 0.08f);
            Gizmos.DrawWireSphere(_bounds.center, interactionRange);
        }
    }
}
