using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Attach to a closet root/entrance GameObject.
/// Uses a single stationary interaction area for range, moves the player to HidePosition,
/// and can play raw imported door clips through an Animator.
/// </summary>
public class ClosetHide : MonoBehaviour
{
    private static readonly List<ClosetHide> Closets = new List<ClosetHide>();
    private static PlayerController CachedPlayerController;
    private static CharacterController CachedCharacterController;
    private static Transform CachedPlayerTransform;
    private static Transform CachedPlayerCameraTransform;

    [Header("References")]
    [SerializeField] private Transform hidePosition;
    [SerializeField] private Animator closetAnimator;

    [Header("Interaction Range")]
    [SerializeField] private Collider interactionArea;
    [SerializeField] private string interactionAreaName = "ClosetInteractionArea";
    [SerializeField] private bool requirePlayerInsideInteractionArea = true;
    [SerializeField] private bool createInteractionAreaIfMissing = true;
    [SerializeField] private Vector3 defaultInteractionAreaLocalPosition = new Vector3(0f, 1f, -0.9f);
    [SerializeField] private Vector3 defaultInteractionAreaSize = new Vector3(1.4f, 2f, 0.9f);

    [Header("Hiding Look")]
    [SerializeField] private float maxLookAngle = 35f;
    [SerializeField] private float lookSensitivity = 1.5f;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 0.35f;

    [Header("Imported Door Clips")]
    [SerializeField] private AnimationClip leftDoorOpenClip;
    [SerializeField] private AnimationClip rightDoorOpenClip;
    [SerializeField] private AnimationClip leftDoorCloseClip;
    [SerializeField] private AnimationClip rightDoorCloseClip;
    [SerializeField] private float openPoseTime = 2.5f;
    [SerializeField] private float closedPoseTimeOffset = 0.15f;

    [Header("Closed Transform Offset (Optional)")]
    [SerializeField] private bool useClosedTransformOffsets;
    [SerializeField] private Transform leftDoorTransform;
    [SerializeField] private Transform rightDoorTransform;
    [SerializeField] private string leftAnimatedDoorName = "LeftDoor";
    [SerializeField] private string rightAnimatedDoorName = "RightDoor";
    [SerializeField] private Vector3 leftClosedLocalPositionOffset;
    [SerializeField] private Vector3 rightClosedLocalPositionOffset;
    [SerializeField] private Vector3 leftClosedLocalEulerOffset;
    [SerializeField] private Vector3 rightClosedLocalEulerOffset;

    [Header("Door Obstacle Colliders")]
    [SerializeField] private bool resizeDoorObstacleColliders = true;
    [SerializeField] private Vector2 doorObstacleColliderXYSize = new Vector2(0.07f, 0.03f);

    [Header("Animator Fallback")]
    [SerializeField] private string openTrigger = "Open";
    [SerializeField] private string closeTrigger = "Close";
    [SerializeField] private string openStateName = "Open";
    [SerializeField] private string closeStateName = "Close";
    [SerializeField] private bool useAnimatorTriggers = false;

    [Header("Closet Timing")]
    [SerializeField] private float closeAnimationDelay = 0.6f;
    [SerializeField] private float openAnimationDelay = 0.6f;
    [SerializeField] private bool startOpen = true;
    [SerializeField] private bool disableClosetCameras = true;

    [Header("Messages")]
    [SerializeField] private string enterMessage = "Press E to hide.";
    [SerializeField] private string exitMessage = "Press E to exit.";
    [SerializeField] private float promptDuration = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip openDoorSound;
    [SerializeField] private AudioClip closeDoorSound;
    [SerializeField] private AudioClip doorSound;
    [SerializeField] [Range(0f, 1f)] private float doorVolume = 1f;

    [Header("Prompts")]
    [SerializeField] private GameObject enterPrompt;
    [SerializeField] private GameObject exitPrompt;

    private bool _playerInRange;
    private bool _isHiding;
    private bool _isTransitioning;
    private bool _interactionAreaHasPlayer;
    private string _lastInteractionCause = "Unknown";
    private Coroutine _transitionRoutine;
    private bool _transitionCancelled;

    private PlayerController _playerController;
    private CharacterController _characterController;
    private Transform _playerTransform;
    private Transform _playerCameraTransform;

    private Vector3 _entryPosition;
    private Quaternion _entryRotation;
    private float _hidingYaw;
    private PlayableGraph _doorGraph;
    private Coroutine _doorAnimationRoutine;
    private bool _applyClosedOffsets;
    private bool _closedOffsetBaseCaptured;
    private readonly Collider[] _interactionOverlapResults = new Collider[16];
    private readonly List<DoorColliderOriginalSize> _doorColliderOriginalSizes = new List<DoorColliderOriginalSize>();
    private Vector3 _leftClosedBaseLocalPosition;
    private Vector3 _rightClosedBaseLocalPosition;
    private Quaternion _leftClosedBaseLocalRotation;
    private Quaternion _rightClosedBaseLocalRotation;
    private bool _doorObstacleCollidersResized;
    private float _nextInteractionRangeCheckTime;

    private const float InteractionRangeCheckInterval = 0.25f;

    public static bool IsPlayerInAnyEntryZone
    {
        get
        {
            foreach (ClosetHide closet in Closets)
            {
                if (closet != null && closet._playerInRange && !closet._isHiding && !closet._isTransitioning)
                    return true;
            }

            return false;
        }
    }

    public static void CancelAllClosetTransitionsForCaughtPlayer()
    {
        foreach (ClosetHide closet in Closets)
        {
            if (closet != null)
                closet.CancelTransitionForCaughtPlayer();
        }
    }

    private void Awake()
    {
        if (closetAnimator == null)
            closetAnimator = GetComponentInChildren<Animator>(true);

        if (closetAnimator == null)
            closetAnimator = CreateAnimatorOnRig();

        if (closetAnimator != null)
            closetAnimator.applyRootMotion = false;

        if (hidePosition == null)
        {
            Transform foundHidePosition = transform.Find("HidePosition");
            if (foundHidePosition != null)
                hidePosition = foundHidePosition;
        }

        if (interactionArea == null)
            interactionArea = FindInteractionAreaCollider();

        if (interactionArea == null && createInteractionAreaIfMissing)
            interactionArea = CreateDefaultInteractionArea();

        ConfigureInteractionArea();
        AutoFindDoorTransforms();

        if (disableClosetCameras)
            DisableChildCameras();
    }

    private void OnEnable()
    {
        if (!Closets.Contains(this))
            Closets.Add(this);
    }

    private void Start()
    {
        if (requirePlayerInsideInteractionArea && interactionArea == null)
        {
            Debug.LogWarning(
                $"[Closet] {name} is set to require an interaction area, but no '{interactionAreaName}' collider was found.",
                this);
        }

        if (startOpen)
            PlayClosetAnimation(true, true);
    }

    private void OnDisable()
    {
        Closets.Remove(this);
        _interactionAreaHasPlayer = false;
        SetPlayerInRange(false);
        _transitionRoutine = null;
        RestoreDoorObstacleColliders();
        StopDoorGraph();
    }

    private void LateUpdate()
    {
        ApplyClosedOffsetsIfNeeded();
    }

    private void Update()
    {
        if (_isTransitioning)
            return;

        if (!_isHiding && Time.time >= _nextInteractionRangeCheckTime)
        {
            _nextInteractionRangeCheckTime = Time.time + InteractionRangeCheckInterval;
            UpdateInteractionRange();
        }

        if (_isHiding)
            HandleHidingLook();

        if (!_playerInRange && !_isHiding)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (_isHiding)
                BeginTransition(ExitHide());
            else if (!ItemPickup.IsAnyItemInspecting)
            {
                BeginTransition(EnterHide());
            }
            else
                CollectionInventory.ShowBottomMessage("Finish inspecting the item first.", 1.5f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (requirePlayerInsideInteractionArea || interactionArea != null || !IsPlayerCollider(other))
            return;

        CachePlayer(other);
        _lastInteractionCause = $"fallback trigger {GetObjectPath(other.transform)}";
        SetPlayerInRange(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (requirePlayerInsideInteractionArea || interactionArea != null || !IsPlayerCollider(other) || _isHiding)
            return;

        SetPlayerInRange(false);
    }

    private IEnumerator EnterHide()
    {
        if (_playerController == null || hidePosition == null)
            yield break;

        _isTransitioning = true;
        _transitionCancelled = false;
        _hidingYaw = 0f;

        _entryPosition = _playerTransform.position;
        _entryRotation = _playerTransform.rotation;

        _playerController.ResetVerticalLook();
        _playerController.SetInputEnabled(false);

        yield return StartCoroutine(SmoothMove(
            _playerTransform.position,
            hidePosition.position,
            _playerTransform.rotation,
            hidePosition.rotation
        ));

        if (_transitionCancelled || IsGameOver())
        {
            CleanupCancelledTransition();
            yield break;
        }

        PlayDoorAudio(false);
        float closeDuration = PlayClosetAnimation(false);
        yield return WaitForSecondsCancellable(Mathf.Max(closeAnimationDelay, closeDuration));

        if (_transitionCancelled || IsGameOver())
        {
            CleanupCancelledTransition();
            yield break;
        }

        _playerController.IsHiding = true;
        _isHiding = true;
        ApplyDoorObstacleColliderResize();
        _isTransitioning = false;

        ShowPrompt(false, true);
        CollectionInventory.ShowBottomMessage(exitMessage, promptDuration);
        _transitionRoutine = null;
    }

    private IEnumerator ExitHide()
    {
        if (_playerController == null)
            yield break;

        _isTransitioning = true;
        _transitionCancelled = false;
        _playerController.IsHiding = false;

        PlayDoorAudio(true);
        float openDuration = PlayClosetAnimation(true);
        yield return WaitForSecondsCancellable(Mathf.Max(openAnimationDelay, openDuration));

        if (_transitionCancelled || IsGameOver())
        {
            CleanupCancelledTransition();
            yield break;
        }

        yield return StartCoroutine(SmoothMove(
            _playerTransform.position,
            _entryPosition,
            _playerTransform.rotation,
            _entryRotation
        ));

        if (_transitionCancelled || IsGameOver())
        {
            CleanupCancelledTransition();
            yield break;
        }

        _playerController.SetInputEnabled(true);
        _isHiding = false;
        RestoreDoorObstacleColliders();
        _isTransitioning = false;

        SetPlayerInRange(false);
        _transitionRoutine = null;
    }

    private IEnumerator SmoothMove(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot)
    {
        float elapsed = 0f;

        if (_characterController != null)
            _characterController.enabled = false;

        while (elapsed < transitionDuration)
        {
            if (_transitionCancelled || IsGameOver())
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);

            _playerTransform.position = Vector3.Lerp(fromPos, toPos, t);
            _playerTransform.rotation = Quaternion.Slerp(fromRot, toRot, t);

            yield return null;
        }

        _playerTransform.position = toPos;
        _playerTransform.rotation = toRot;

        if (_characterController != null)
            _characterController.enabled = true;
    }

    private IEnumerator WaitForSecondsCancellable(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_transitionCancelled || IsGameOver())
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void BeginTransition(IEnumerator transition)
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(transition);
    }

    private void CancelTransitionForCaughtPlayer()
    {
        if (!_isTransitioning && !_isHiding)
            return;

        _transitionCancelled = true;

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        CleanupCancelledTransition();
    }

    private void CleanupCancelledTransition()
    {
        StopDoorGraph();
        RestoreDoorObstacleColliders();
        ShowPrompt(false, false);

        if (_characterController != null && !_characterController.enabled)
            _characterController.enabled = true;

        if (_playerController != null)
            _playerController.IsHiding = false;

        _isHiding = false;
        _isTransitioning = false;
        _transitionRoutine = null;
    }

    private static bool IsGameOver()
    {
        return GameManager.Instance != null && GameManager.Instance.IsGameOver;
    }

    private void UpdateInteractionRange()
    {
        if (!EnsureCachedPlayer())
        {
            SetPlayerInRange(false);
            return;
        }

        _playerController = CachedPlayerController;
        _characterController = CachedCharacterController;
        _playerTransform = CachedPlayerTransform;
        _playerCameraTransform = CachedPlayerCameraTransform;

        SetPlayerInRange(IsPlayerNearInteractionArea());
    }

    private bool IsPlayerNearInteractionArea()
    {
        if (interactionArea == null)
        {
            _lastInteractionCause = "no closet interaction area found";
            return !requirePlayerInsideInteractionArea && _playerInRange;
        }

        if (requirePlayerInsideInteractionArea)
        {
            if (_interactionAreaHasPlayer)
            {
                _lastInteractionCause = $"trigger overlap with {GetObjectPath(interactionArea.transform)}";
                return true;
            }

            if (_playerTransform != null &&
                IsPlayerPositionInsideInteractionCollider(interactionArea, _playerTransform.position + Vector3.up * 0.1f))
            {
                _lastInteractionCause = $"player feet inside {GetObjectPath(interactionArea.transform)}";
                return true;
            }

            if (_characterController != null &&
                IsPlayerPositionInsideInteractionCollider(interactionArea, _characterController.bounds.center))
            {
                _lastInteractionCause = $"player body center inside {GetObjectPath(interactionArea.transform)}";
                return true;
            }

            if (_playerCameraTransform != null &&
                IsPlayerPositionInsideInteractionCollider(interactionArea, _playerCameraTransform.position))
            {
                _lastInteractionCause = $"player camera inside {GetObjectPath(interactionArea.transform)}";
                return true;
            }

            _lastInteractionCause = "not inside closet interaction area";
            return false;
        }

        Vector3 playerPosition = _playerTransform != null ? _playerTransform.position : transform.position;
        Vector3 closestPoint = interactionArea.ClosestPoint(playerPosition);
        bool inRange = Vector3.Distance(playerPosition, closestPoint) <= 0.01f;
        _lastInteractionCause = inRange
            ? $"near {GetObjectPath(interactionArea.transform)}"
            : "not near closet interaction area";
        return inRange;
    }

    private bool IsPlayerOverlappingInteractionArea()
    {
        BoxCollider boxCollider = interactionArea as BoxCollider;
        if (boxCollider == null)
            return false;

        Vector3 center = boxCollider.transform.TransformPoint(boxCollider.center);
        Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, Abs(boxCollider.transform.lossyScale));
        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _interactionOverlapResults,
            boxCollider.transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _interactionOverlapResults[i];
            _interactionOverlapResults[i] = null;

            if (hit != null && IsPlayerCollider(hit))
            {
                CachePlayer(hit);
                return true;
            }
        }

        return false;
    }

    private bool IsPlayerPositionInsideInteractionCollider(Collider doorCollider, Vector3 playerPosition)
    {
        BoxCollider boxCollider = doorCollider as BoxCollider;
        if (boxCollider == null)
            return doorCollider.bounds.Contains(playerPosition);

        Vector3 localPoint = boxCollider.transform.InverseTransformPoint(playerPosition) - boxCollider.center;
        Vector3 extents = boxCollider.size * 0.5f;
        const float tolerance = 0.03f;

        return Mathf.Abs(localPoint.x) <= extents.x + tolerance &&
               Mathf.Abs(localPoint.y) <= extents.y + tolerance &&
               Mathf.Abs(localPoint.z) <= extents.z + tolerance;
    }

    private void SetPlayerInRange(bool inRange)
    {
        if (_playerInRange == inRange)
            return;

        _playerInRange = inRange;

        if (_playerInRange && !_isHiding)
        {
            ShowPrompt(true, false);
            CollectionInventory.ShowBottomMessage(enterMessage, promptDuration);
        }
        else if (!_isHiding)
        {
            ShowPrompt(false, false);
        }
    }

    private void CachePlayer(Collider other)
    {
        _playerTransform = other.transform;
        _playerController = other.GetComponent<PlayerController>();
        _characterController = other.GetComponent<CharacterController>();

        if (_playerController == null)
            _playerController = other.GetComponentInParent<PlayerController>();

        if (_characterController == null && _playerController != null)
            _characterController = _playerController.GetComponent<CharacterController>();

        if (_playerController != null)
            _playerTransform = _playerController.transform;

        if (_playerController != null)
        {
            CachedPlayerController = _playerController;
            CachedCharacterController = _characterController;
            CachedPlayerTransform = _playerTransform;

            Camera playerCamera = _playerController.GetComponentInChildren<Camera>(true);
            CachedPlayerCameraTransform = playerCamera != null ? playerCamera.transform : null;
            _playerCameraTransform = CachedPlayerCameraTransform;
        }
    }

    private static bool EnsureCachedPlayer()
    {
        if (CachedPlayerController != null && CachedPlayerTransform != null)
            return true;

        CachedPlayerController = FindFirstObjectByType<PlayerController>();
        if (CachedPlayerController == null)
            return false;

        CachedCharacterController = CachedPlayerController.GetComponent<CharacterController>();
        CachedPlayerTransform = CachedPlayerController.transform;

        Camera playerCamera = CachedPlayerController.GetComponentInChildren<Camera>(true);
        CachedPlayerCameraTransform = playerCamera != null ? playerCamera.transform : null;
        return true;
    }

    private string GetInteractionColliderSummary()
    {
        return interactionArea != null ? GetObjectPath(interactionArea.transform) : "none";
    }

    private static string GetObjectPath(Transform target)
    {
        if (target == null)
            return "null";

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private void HandleHidingLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        _hidingYaw = Mathf.Clamp(_hidingYaw + mouseX, -maxLookAngle, maxLookAngle);
        _playerTransform.rotation = hidePosition.rotation * Quaternion.Euler(0f, _hidingYaw, 0f);
    }

    private void ShowPrompt(bool showEnter, bool showExit)
    {
        if (enterPrompt != null)
            enterPrompt.SetActive(showEnter);

        if (exitPrompt != null)
            exitPrompt.SetActive(showExit);
    }

    private void PlayDoorAudio(bool opening)
    {
        AudioClip clip = opening ? openDoorSound : closeDoorSound;
        if (clip == null)
            clip = doorSound;

        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, doorVolume);
    }

    private float PlayClosetAnimation(bool open, bool instant = false)
    {
        if (closetAnimator == null)
            return 0f;

        AnimationClip[] clips = GetDoorClips(open);
        if (clips.Length > 0)
        {
            return PlayDoorClips(clips, open, instant);
        }

        if (closetAnimator.runtimeAnimatorController == null)
            return 0f;

        string stateName = open ? openStateName : closeStateName;

        if (instant)
        {
            closetAnimator.Play(stateName, 0, 1f);
            closetAnimator.Update(0f);
            return 0f;
        }

        if (useAnimatorTriggers)
        {
            closetAnimator.ResetTrigger(open ? closeTrigger : openTrigger);
            closetAnimator.SetTrigger(open ? openTrigger : closeTrigger);
        }
        else
        {
            closetAnimator.Play(stateName, 0, 0f);
        }

        return open ? openAnimationDelay : closeAnimationDelay;
    }

    private AnimationClip[] GetDoorClips(bool open)
    {
        List<AnimationClip> clips = new List<AnimationClip>(2);

        AnimationClip leftClip = leftDoorOpenClip != null ? leftDoorOpenClip : leftDoorCloseClip;
        AnimationClip rightClip = rightDoorOpenClip != null ? rightDoorOpenClip : rightDoorCloseClip;

        if (leftClip != null)
            clips.Add(leftClip);

        if (rightClip != null)
            clips.Add(rightClip);

        return clips.ToArray();
    }

    private float PlayDoorClips(AnimationClip[] clips, bool open, bool instant)
    {
        StopDoorGraph();

        _doorGraph = PlayableGraph.Create($"{name}_ClosetDoorGraph");
        _doorGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        _applyClosedOffsets = false;
        _closedOffsetBaseCaptured = false;

        AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(_doorGraph, clips.Length);
        float segmentDuration = 0f;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(_doorGraph, clips[i]);
            clipPlayable.SetApplyFootIK(false);

            float clipOpenTime = Mathf.Clamp(openPoseTime, 0f, clips[i].length);
            float clipClosedTime = GetClosedPoseTime(clips[i]);
            double startTime = open ? clipClosedTime : clipOpenTime;
            double endTime = open ? clipOpenTime : clips[i].length;
            endTime = open ? clipOpenTime : clipClosedTime;
            double speed = open ? -1d : 1d;

            if (instant)
            {
                clipPlayable.SetTime(clipOpenTime);
                clipPlayable.SetSpeed(0d);
            }
            else
            {
                clipPlayable.SetTime(startTime);
                clipPlayable.SetSpeed(speed);
            }

            _doorGraph.Connect(clipPlayable, 0, mixer, i);
            mixer.SetInputWeight(i, 1f);
            segmentDuration = Mathf.Max(segmentDuration, Mathf.Abs((float)(endTime - startTime)));
        }

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_doorGraph, "ClosetDoors", closetAnimator);
        output.SetSourcePlayable(mixer);

        if (instant)
        {
            _doorGraph.Evaluate(0f);
            _applyClosedOffsets = false;
            return 0f;
        }

        _doorGraph.Play();
        _doorAnimationRoutine = StartCoroutine(FreezeDoorGraphAfter(segmentDuration, open));
        return segmentDuration;
    }

    private IEnumerator FreezeDoorGraphAfter(float delay, bool open)
    {
        yield return new WaitForSeconds(delay);

        if (!_doorGraph.IsValid())
            yield break;

        _doorGraph.Stop();

        AnimationClip[] clips = GetDoorClips(open);
        for (int i = 0; i < clips.Length; i++)
        {
            float targetTime = open ? Mathf.Clamp(openPoseTime, 0f, clips[i].length) : GetClosedPoseTime(clips[i]);
            PlayDoorClipPose(i, targetTime);
        }

        _doorGraph.Evaluate(0f);
        _applyClosedOffsets = !open && useClosedTransformOffsets;
        _closedOffsetBaseCaptured = false;
        _doorAnimationRoutine = null;
    }

    private float GetClosedPoseTime(AnimationClip clip)
    {
        float minimumClosedTime = Mathf.Clamp(openPoseTime, 0f, clip.length);
        return Mathf.Clamp(clip.length - Mathf.Max(0f, closedPoseTimeOffset), minimumClosedTime, clip.length);
    }

    private void PlayDoorClipPose(int inputIndex, float time)
    {
        if (!_doorGraph.IsValid())
            return;

        Playable root = _doorGraph.GetRootPlayable(0);
        if (!root.IsValid() || inputIndex >= root.GetInputCount())
            return;

        Playable input = root.GetInput(inputIndex);
        if (!input.IsValid())
            return;

        input.SetTime(time);
        input.SetSpeed(0d);
    }

    private void StopDoorGraph()
    {
        if (_doorAnimationRoutine != null)
        {
            StopCoroutine(_doorAnimationRoutine);
            _doorAnimationRoutine = null;
        }

        if (_doorGraph.IsValid())
            _doorGraph.Destroy();
    }

    private Collider FindInteractionAreaCollider()
    {
        Transform area = FindChildByName(transform, interactionAreaName);
        return area != null ? area.GetComponent<Collider>() : null;
    }

    private void ConfigureInteractionArea()
    {
        if (interactionArea == null)
            return;

        interactionArea.isTrigger = true;

        ClosetInteractionAreaSensor sensor = interactionArea.GetComponent<ClosetInteractionAreaSensor>();
        if (sensor == null)
            sensor = interactionArea.gameObject.AddComponent<ClosetInteractionAreaSensor>();

        sensor.Initialize(this);
    }

    public void NotifyInteractionAreaPlayerInside(Collider playerCollider)
    {
        if (!IsPlayerCollider(playerCollider))
            return;

        CachePlayer(playerCollider);
        _interactionAreaHasPlayer = true;
        _lastInteractionCause = $"trigger reported player inside {GetObjectPath(interactionArea.transform)}";
    }

    public void NotifyInteractionAreaPlayerExited(Collider playerCollider)
    {
        if (!IsPlayerCollider(playerCollider))
            return;

        _interactionAreaHasPlayer = false;
        _lastInteractionCause = $"trigger reported player exited {GetObjectPath(interactionArea.transform)}";
    }

    private Collider CreateDefaultInteractionArea()
    {
        GameObject area = new GameObject(interactionAreaName);
        area.transform.SetParent(transform, false);
        area.transform.localPosition = defaultInteractionAreaLocalPosition;
        area.transform.localRotation = Quaternion.identity;
        area.transform.localScale = Vector3.one;

        BoxCollider boxCollider = area.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = defaultInteractionAreaSize;
        return boxCollider;
    }

    private void AutoFindDoorTransforms()
    {
        if (leftDoorTransform == null)
            leftDoorTransform = FindChildByName(transform, leftAnimatedDoorName);

        if (rightDoorTransform == null)
            rightDoorTransform = FindChildByName(transform, rightAnimatedDoorName);
    }

    private void ApplyDoorObstacleColliderResize()
    {
        if (!resizeDoorObstacleColliders || _doorObstacleCollidersResized)
            return;

        ResizeDoorObstacleColliders(leftDoorTransform);
        ResizeDoorObstacleColliders(rightDoorTransform);
        _doorObstacleCollidersResized = true;
    }

    private void ResizeDoorObstacleColliders(Transform doorRoot)
    {
        if (doorRoot == null)
            return;

        BoxCollider[] colliders = doorRoot.GetComponents<BoxCollider>();
        if (colliders.Length == 0)
            colliders = doorRoot.GetComponentsInChildren<BoxCollider>(true);

        foreach (BoxCollider boxCollider in colliders)
        {
            if (boxCollider == null || boxCollider == interactionArea)
                continue;

            if (boxCollider.GetComponent<ClosetInteractionAreaSensor>() != null)
                continue;

            _doorColliderOriginalSizes.Add(new DoorColliderOriginalSize(boxCollider, boxCollider.size));

            Vector3 size = boxCollider.size;
            size.x = doorObstacleColliderXYSize.x;
            size.y = doorObstacleColliderXYSize.y;
            boxCollider.size = size;
        }
    }

    private void RestoreDoorObstacleColliders()
    {
        if (!_doorObstacleCollidersResized)
            return;

        foreach (DoorColliderOriginalSize original in _doorColliderOriginalSizes)
        {
            if (original.Collider != null)
                original.Collider.size = original.Size;
        }

        _doorColliderOriginalSizes.Clear();
        _doorObstacleCollidersResized = false;
    }

    private void ApplyClosedOffsetsIfNeeded()
    {
        if (!_applyClosedOffsets)
            return;

        if (!_closedOffsetBaseCaptured)
            CaptureClosedOffsetBase();

        if (leftDoorTransform != null)
        {
            leftDoorTransform.localPosition = _leftClosedBaseLocalPosition + leftClosedLocalPositionOffset;
            leftDoorTransform.localRotation = _leftClosedBaseLocalRotation * Quaternion.Euler(leftClosedLocalEulerOffset);
        }

        if (rightDoorTransform != null)
        {
            rightDoorTransform.localPosition = _rightClosedBaseLocalPosition + rightClosedLocalPositionOffset;
            rightDoorTransform.localRotation = _rightClosedBaseLocalRotation * Quaternion.Euler(rightClosedLocalEulerOffset);
        }
    }

    private void CaptureClosedOffsetBase()
    {
        if (leftDoorTransform != null)
        {
            _leftClosedBaseLocalPosition = leftDoorTransform.localPosition;
            _leftClosedBaseLocalRotation = leftDoorTransform.localRotation;
        }

        if (rightDoorTransform != null)
        {
            _rightClosedBaseLocalPosition = rightDoorTransform.localPosition;
            _rightClosedBaseLocalRotation = rightDoorTransform.localRotation;
        }

        _closedOffsetBaseCaptured = true;
    }

    private Animator CreateAnimatorOnRig()
    {
        Transform rig = FindChildByName(transform, "TheRig-Closet");
        GameObject target = rig != null ? rig.gameObject : gameObject;
        return target.AddComponent<Animator>();
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private void DisableChildCameras()
    {
        foreach (Camera closetCamera in GetComponentsInChildren<Camera>(true))
            closetCamera.enabled = false;

        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private readonly struct DoorColliderOriginalSize
    {
        public DoorColliderOriginalSize(BoxCollider collider, Vector3 size)
        {
            Collider = collider;
            Size = size;
        }

        public BoxCollider Collider { get; }
        public Vector3 Size { get; }
    }
}

public class ClosetInteractionAreaSensor : MonoBehaviour
{
    private ClosetHide _closet;

    public void Initialize(ClosetHide closet)
    {
        _closet = closet;
    }

    private void OnTriggerEnter(Collider other)
    {
        _closet?.NotifyInteractionAreaPlayerInside(other);
    }

    private void OnTriggerExit(Collider other)
    {
        _closet?.NotifyInteractionAreaPlayerExited(other);
    }
}
