using UnityEngine;

[DisallowMultipleComponent]
public class ElevatorDoorController : MonoBehaviour
{
    [Header("Doors")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Movement")]
    [SerializeField] private Vector3 leftOpenOffset = new Vector3(-0.8f, 0f, 0f);
    [SerializeField] private Vector3 rightOpenOffset = new Vector3(0.8f, 0f, 0f);
    [SerializeField] private float openSpeed = 2.2f;
    [SerializeField] private bool doorsStartClosed = true;
    [SerializeField] private bool useSavedClosedPositions;
    [SerializeField] private Vector3 leftClosedLocalPosition;
    [SerializeField] private Vector3 rightClosedLocalPosition;
    [SerializeField, HideInInspector] private bool startOpen;

    [Header("Auto Close")]
    [SerializeField] private bool autoClose = true;
    [SerializeField] private float autoCloseDelay = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip doorOpenCloseSound;
    [SerializeField] private AudioClip elevatorMovingSound;
    [SerializeField] private AudioClip elevatorArrivalSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;
    [SerializeField] private bool loopMovingSound = true;

    [Header("Door State")]
    [SerializeField] private float fullyOpenDistance = 0.05f;

    private Vector3 _leftClosedPosition;
    private Vector3 _rightClosedPosition;
    private bool _open;
    private float _autoCloseTimer;
    private AudioSource _movingAudioSource;

    public bool IsOpen => _open;
    public bool AreDoorsFullyOpen => DoorsReachedOpenPosition();
    public Transform LeftDoor => leftDoor;
    public Transform RightDoor => rightDoor;
    public Vector3 LeftOpenOffset => leftOpenOffset;
    public Vector3 RightOpenOffset => rightOpenOffset;

    private void Awake()
    {
        AutoFindDoors();

        if (leftDoor != null)
            _leftClosedPosition = useSavedClosedPositions ? leftClosedLocalPosition : leftDoor.localPosition;

        if (rightDoor != null)
            _rightClosedPosition = useSavedClosedPositions ? rightClosedLocalPosition : rightDoor.localPosition;

        _open = !doorsStartClosed || startOpen;
        SnapToCurrentState();
        EnsureMovingAudioSource();
    }

    private void Update()
    {
        if (leftDoor != null)
        {
            Vector3 target = _leftClosedPosition + (_open ? leftOpenOffset : Vector3.zero);
            leftDoor.localPosition = Vector3.MoveTowards(leftDoor.localPosition, target, openSpeed * Time.deltaTime);
        }

        if (rightDoor != null)
        {
            Vector3 target = _rightClosedPosition + (_open ? rightOpenOffset : Vector3.zero);
            rightDoor.localPosition = Vector3.MoveTowards(rightDoor.localPosition, target, openSpeed * Time.deltaTime);
        }

        UpdateAutoClose();
    }

    public void Open()
    {
        if (!_open)
            PlayOneShot(doorOpenCloseSound);

        _open = true;
        ResetAutoCloseTimer();
    }

    public void OpenAndStayOpen()
    {
        if (!_open)
            PlayOneShot(doorOpenCloseSound);

        autoClose = false;
        _autoCloseTimer = 0f;
        _open = true;
    }

    public void Close()
    {
        if (_open)
            PlayOneShot(doorOpenCloseSound);

        _open = false;
        _autoCloseTimer = 0f;
    }

    public void Toggle()
    {
        if (_open)
            Close();
        else
            Open();
    }

    private void SnapToCurrentState()
    {
        if (leftDoor != null)
            leftDoor.localPosition = _leftClosedPosition + (_open ? leftOpenOffset : Vector3.zero);

        if (rightDoor != null)
            rightDoor.localPosition = _rightClosedPosition + (_open ? rightOpenOffset : Vector3.zero);
    }

    private bool DoorsReachedOpenPosition()
    {
        bool leftReached = leftDoor == null || Vector3.Distance(
            leftDoor.localPosition,
            _leftClosedPosition + leftOpenOffset) <= fullyOpenDistance;

        bool rightReached = rightDoor == null || Vector3.Distance(
            rightDoor.localPosition,
            _rightClosedPosition + rightOpenOffset) <= fullyOpenDistance;

        return leftReached && rightReached;
    }

    private void UpdateAutoClose()
    {
        if (!_open || !autoClose)
            return;

        _autoCloseTimer -= Time.deltaTime;
        if (_autoCloseTimer <= 0f)
            Close();
    }

    private void ResetAutoCloseTimer()
    {
        _autoCloseTimer = Mathf.Max(0f, autoCloseDelay);
    }

    public void PlayElevatorMovingSound()
    {
        if (elevatorMovingSound == null)
            return;

        EnsureMovingAudioSource();
        if (_movingAudioSource == null)
        {
            PlayOneShot(elevatorMovingSound);
            return;
        }

        _movingAudioSource.clip = elevatorMovingSound;
        _movingAudioSource.volume = audioVolume;
        _movingAudioSource.loop = loopMovingSound;

        if (!_movingAudioSource.isPlaying)
            _movingAudioSource.Play();
    }

    public void StopElevatorMovingSound()
    {
        if (_movingAudioSource != null && _movingAudioSource.isPlaying)
            _movingAudioSource.Stop();
    }

    public void PlayElevatorArrivalSound()
    {
        PlayOneShot(elevatorArrivalSound);
    }

    private void EnsureMovingAudioSource()
    {
        if (_movingAudioSource != null || elevatorMovingSound == null)
            return;

        _movingAudioSource = GetComponent<AudioSource>();
        if (_movingAudioSource == null)
            _movingAudioSource = gameObject.AddComponent<AudioSource>();

        _movingAudioSource.playOnAwake = false;
        _movingAudioSource.spatialBlend = 1f;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, audioVolume);
    }

    private void AutoFindDoors()
    {
        if (leftDoor != null && rightDoor != null)
            return;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string lowerName = child.name.ToLowerInvariant();
            if (leftDoor == null && lowerName.Contains("left") && lowerName.Contains("door"))
                leftDoor = child;
            else if (rightDoor == null && lowerName.Contains("right") && lowerName.Contains("door"))
                rightDoor = child;
        }
    }
}
