using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.AI;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class SCP096MansionEnemy : MonoBehaviour
{
    private enum State { Patrolling, Chasing, LostPlayerPause, Returning, Caught }

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waypointWaitTime = 2f;

    [Header("Detection")]
    [SerializeField] private float sightRange = 14f;
    [SerializeField] private float sightAngle = 70f;
    [SerializeField] private float crouchDetectionMultiplier = 0.5f;
    [SerializeField] private float catchDistance = 1.6f;
    [SerializeField] private float proximityCatchRadius = 0.9f;

    [Header("Speed")]
    [SerializeField] private float patrolSpeed = 2.25f;
    [SerializeField] private float chaseSpeed = 7.5f;

    [Header("Chase")]
    [SerializeField] private float losePlayerDelay = 3f;
    [SerializeField] private float lostPlayerIdleTime = 3f;
    [SerializeField] private float chaseDestinationSampleRadius = 2f;

    [Header("Doors")]
    [SerializeField] private float doorOpenRange = 2.25f;
    [SerializeField] private float doorCheckInterval = 0.15f;

    [Header("Flashlight Detection")]
    [SerializeField] private bool reactToFlashlight;
    [SerializeField] private float flashlightDetectionRange = 25f;
    [SerializeField] private float flashlightDetectionAngle = 45f;

    [Header("Jumpscare")]
    [SerializeField] private Camera jumpscareCamera;
    [SerializeField] private float jumpscareZoomAmount = 10f;

    [Header("Animation Clips")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip chaseClip;
    [SerializeField] private AnimationClip caughtClip;

    [Header("Fallback Animator States")]
    [SerializeField] private string idleStateName = "096_Idle";
    [SerializeField] private string walkStateName = "096_Idle";
    [SerializeField] private string chaseStateName = "096_Sprint";
    [SerializeField] private string caughtStateName = "096_Distress";

    [Header("Audio")]
    [SerializeField] private AudioClip idleLoop;
    [SerializeField] private AudioClip chaseLoop;
    [SerializeField] private AudioClip caughtSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    private NavMeshAgent _agent;
    private Transform _player;
    private PlayerController _playerController;
    private FlashlightController _flashlight;
    private JumpscareController _jumpscareController;
    private AudioSource _audioSource;
    private State _state = State.Patrolling;
    private int _waypointIndex;
    private float _waitTimer;
    private bool _waitingAtWaypoint;
    private bool _canSeePlayer;
    private Vector3 _lastKnownPlayerPos;
    private float _losePlayerTimer;
    private float _lostPlayerIdleTimer;
    private float _doorCheckTimer;
    private Animation _legacyAnimation;
    private PlayableGraph _clipGraph;
    private AnimationClipPlayable _clipPlayable;
    private AnimationClip _currentClip;
    private bool _currentClipLoops;

    public bool IsChasing => _state == State.Chasing;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _legacyAnimation = GetComponentInChildren<Animation>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.volume = audioVolume;

        DiscoverWaypoints();
        FindPlayer();
    }

    private void OnEnable()
    {
        FindPlayer();
        DiscoverWaypoints();
        SetState(State.Patrolling);
    }

    private void OnDisable()
    {
        DestroyClipGraph();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
            return;

        if (_state == State.Caught)
            return;

        if (_player == null)
            FindPlayer();

        UpdateClipLoop();
        UpdateNearbyDoors();

        if (CheckProximityCatch())
            return;

        _canSeePlayer = CheckLineOfSight() || CheckFlashlightDetection();

        switch (_state)
        {
            case State.Patrolling: UpdatePatrol(); break;
            case State.Chasing: UpdateChase(); break;
            case State.LostPlayerPause: UpdateLostPlayerPause(); break;
            case State.Returning: UpdateReturn(); break;
        }
    }

    private void UpdatePatrol()
    {
        if (_canSeePlayer)
        {
            SetState(State.Chasing);
            return;
        }

        if (waypoints == null || waypoints.Length == 0 || FindNearestWaypointIndex() < 0)
        {
            _agent.ResetPath();
            return;
        }

        if (_waitingAtWaypoint)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _waitingAtWaypoint = false;
                AdvanceWaypoint();
            }

            return;
        }

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            _waitingAtWaypoint = true;
            _waitTimer = waypointWaitTime;
            PlayAnimation(idleClip, idleStateName, true);
        }
    }

    private void UpdateChase()
    {
        if (_player == null)
            return;

        if (ElevatorAIZone.TryGetZoneForPosition(_player.position, out ElevatorAIZone elevatorZone))
        {
            _losePlayerTimer = losePlayerDelay;

            if (elevatorZone.BlocksAI)
            {
                Vector3 waitPosition = elevatorZone.GetWaitPosition(transform.position);
                _lastKnownPlayerPos = waitPosition;
                _agent.SetDestination(waitPosition);
                return;
            }
        }

        if (_canSeePlayer)
        {
            Vector3 chasePosition = GetReachableChasePosition(_player.position);
            _lastKnownPlayerPos = chasePosition;
            _losePlayerTimer = losePlayerDelay;
            _agent.SetDestination(chasePosition);

            if (Vector3.Distance(transform.position, _player.position) <= catchDistance)
                TriggerCatch();
        }
        else
        {
            _agent.SetDestination(_lastKnownPlayerPos);
            _losePlayerTimer -= Time.deltaTime;

            if (_losePlayerTimer <= 0f)
                SetState(State.LostPlayerPause);
        }
    }

    private void UpdateLostPlayerPause()
    {
        if (_canSeePlayer)
        {
            SetState(State.Chasing);
            return;
        }

        _lostPlayerIdleTimer -= Time.deltaTime;
        if (_lostPlayerIdleTimer <= 0f)
            SetState(State.Returning);
    }

    private void UpdateReturn()
    {
        if (_canSeePlayer)
        {
            SetState(State.Chasing);
            return;
        }

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            SetState(State.Patrolling);
    }

    private void SetState(State newState)
    {
        _state = newState;

        switch (newState)
        {
            case State.Patrolling:
                _agent.isStopped = false;
                _agent.speed = patrolSpeed;
                _waypointIndex = FindNearestWaypointIndex();
                PlayAnimation(walkClip, walkStateName, true);
                PlayLoop(idleLoop);
                if (_waypointIndex >= 0)
                    _agent.SetDestination(waypoints[_waypointIndex].position);
                break;

            case State.Chasing:
                _agent.isStopped = false;
                _agent.speed = chaseSpeed;
                _lastKnownPlayerPos = _player != null ? _player.position : transform.position;
                _losePlayerTimer = losePlayerDelay;
                PlayAnimation(chaseClip, chaseStateName, true);
                PlayLoop(chaseLoop);
                break;

            case State.LostPlayerPause:
                _agent.isStopped = true;
                _agent.ResetPath();
                _lostPlayerIdleTimer = lostPlayerIdleTime;
                PlayAnimation(idleClip, idleStateName, true);
                PlayLoop(idleLoop);
                break;

            case State.Returning:
                _agent.isStopped = false;
                _agent.speed = patrolSpeed;
                _waypointIndex = FindNearestWaypointIndex();
                PlayAnimation(walkClip, walkStateName, true);
                PlayLoop(idleLoop);
                if (_waypointIndex >= 0)
                    _agent.SetDestination(waypoints[_waypointIndex].position);
                break;
        }
    }

    private bool CheckLineOfSight()
    {
        if (_player == null)
            return false;

        if (_playerController != null && _playerController.IsHiding)
            return false;

        float effectiveSightRange = (_playerController != null && _playerController.IsCrouching)
            ? sightRange * crouchDetectionMultiplier
            : sightRange;

        Vector3 toPlayer = _player.position - transform.position;
        if (toPlayer.magnitude > effectiveSightRange)
            return false;

        if (Vector3.Angle(transform.forward, toPlayer) > sightAngle)
            return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.6f;
        Vector3 playerChest = _player.position + Vector3.up * 1.0f;
        if (Physics.Linecast(eyePos, playerChest, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return IsPlayerHit(hit.transform);

        return true;
    }

    private bool CheckProximityCatch()
    {
        if (_player == null)
            return false;

        if (_playerController != null && _playerController.IsHiding)
            return false;

        if (Vector3.Distance(transform.position, _player.position) > proximityCatchRadius)
            return false;

        TriggerCatch();
        return true;
    }

    private void TriggerCatch()
    {
        if (_state == State.Caught)
            return;

        _state = State.Caught;
        CollectionInventory.ForceCloseInventory();
        ClosetHide.CancelAllClosetTransitionsForCaughtPlayer();

        _agent.isStopped = true;
        _agent.ResetPath();

        if (_player != null)
        {
            Vector3 direction = _player.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        PlayAnimation(caughtClip, caughtStateName, false);
        PlayOneShot(caughtSound);

        if (_jumpscareController != null)
            _jumpscareController.TriggerJumpscare(jumpscareCamera, jumpscareZoomAmount);
        else if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerCaught();
    }

    private bool CheckFlashlightDetection()
    {
        if (!reactToFlashlight)
            return false;

        if (_flashlight == null || !_flashlight.IsFlashlightOn)
            return false;

        if (_player == null)
            return false;

        if (_playerController != null && _playerController.IsHiding)
            return false;

        Vector3 toEnemy = transform.position - _player.position;
        float distance = toEnemy.magnitude;
        if (distance > flashlightDetectionRange)
            return false;

        float angle = Vector3.Angle(_flashlight.transform.forward, toEnemy);
        return angle <= flashlightDetectionAngle;
    }

    private void UpdateNearbyDoors()
    {
        _doorCheckTimer -= Time.deltaTime;
        if (_doorCheckTimer > 0f)
            return;

        _doorCheckTimer = doorCheckInterval;
        Vector3 checkPosition = transform.position + transform.forward * (doorOpenRange * 0.5f);
        RoomDoorController.OpenNearbyForAI(checkPosition, doorOpenRange);
        RightRoomDoorController.OpenNearbyForAI(checkPosition, doorOpenRange);
    }

    private Vector3 GetReachableChasePosition(Vector3 desiredPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, chaseDestinationSampleRadius, NavMesh.AllAreas))
            return hit.position;

        return desiredPosition;
    }

    private bool IsPlayerHit(Transform hitTransform)
    {
        return hitTransform != null &&
               (_player != null &&
                (hitTransform == _player || hitTransform.IsChildOf(_player) || hitTransform.CompareTag("Player")));
    }

    private void DiscoverWaypoints()
    {
        if (HasAnyUsableWaypoint())
            return;

        GameObject root = GameObject.Find("Waypoints");
        if (root == null)
        {
            waypoints = System.Array.Empty<Transform>();
            return;
        }

        List<Transform> discovered = new List<Transform>();
        foreach (Transform child in root.transform)
        {
            if (child.gameObject.activeInHierarchy)
                discovered.Add(child);
        }

        waypoints = discovered.ToArray();
    }

    private bool HasAnyUsableWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
            return false;

        foreach (Transform waypoint in waypoints)
        {
            if (waypoint != null && waypoint.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private void AdvanceWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        List<int> available = new List<int>();
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (i != _waypointIndex && waypoints[i] != null && waypoints[i].gameObject.activeInHierarchy)
                available.Add(i);
        }

        if (available.Count == 0)
            _waypointIndex = FindNearestWaypointIndex();
        else
            _waypointIndex = available[Random.Range(0, available.Count)];

        if (_waypointIndex >= 0)
        {
            _agent.SetDestination(waypoints[_waypointIndex].position);
            PlayAnimation(walkClip, walkStateName, true);
        }
    }

    private int FindNearestWaypointIndex()
    {
        if (waypoints == null || waypoints.Length == 0)
            return -1;

        int nearest = -1;
        float shortest = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform waypoint = waypoints[i];
            if (waypoint == null || !waypoint.gameObject.activeInHierarchy)
                continue;

            float distance = Vector3.Distance(transform.position, waypoint.position);
            if (distance < shortest)
            {
                shortest = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
            return;

        _player = playerObject.transform;
        _playerController = playerObject.GetComponent<PlayerController>();
        _flashlight = playerObject.GetComponentInChildren<FlashlightController>(true);
        _jumpscareController = playerObject.GetComponent<JumpscareController>();

        if (jumpscareCamera == null)
        {
            Transform cameraChild = FindChildByName(transform, "Camera");
            if (cameraChild != null)
                jumpscareCamera = cameraChild.GetComponent<Camera>();
        }

        if (jumpscareCamera != null)
            jumpscareCamera.enabled = false;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private void PlayAnimation(AnimationClip clip, string fallbackState, bool loop)
    {
        if (clip != null && animator != null)
        {
            PlayAnimatorClip(clip, loop);
            return;
        }

        if (clip != null && _legacyAnimation != null)
        {
            if (_legacyAnimation.GetClip(clip.name) == null)
                _legacyAnimation.AddClip(clip, clip.name);

            _legacyAnimation.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            _legacyAnimation.Play(clip.name);
            return;
        }

        if (animator != null && !string.IsNullOrWhiteSpace(fallbackState))
        {
            DestroyClipGraph();
            animator.Play(fallbackState, 0, 0f);
        }
    }

    private void PlayAnimatorClip(AnimationClip clip, bool loop)
    {
        DestroyClipGraph();

        _clipGraph = PlayableGraph.Create($"{name}_{clip.name}_Graph");
        _clipGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        _clipPlayable = AnimationClipPlayable.Create(_clipGraph, clip);
        _clipPlayable.SetApplyFootIK(false);
        _clipPlayable.SetDuration(loop ? double.PositiveInfinity : clip.length);
        _currentClip = clip;
        _currentClipLoops = loop;

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_clipGraph, "SCP096MansionEnemyClip", animator);
        output.SetSourcePlayable(_clipPlayable);
        _clipGraph.Play();
    }

    private void UpdateClipLoop()
    {
        if (!_currentClipLoops || _currentClip == null || !_clipGraph.IsValid() || !_clipPlayable.IsValid())
            return;

        if (_clipPlayable.GetTime() < _currentClip.length)
            return;

        _clipPlayable.SetTime(0d);
        _clipGraph.Evaluate(0f);
    }

    private void DestroyClipGraph()
    {
        if (_clipGraph.IsValid())
            _clipGraph.Destroy();

        _currentClip = null;
        _currentClipLoops = false;
    }

    private void PlayLoop(AudioClip clip)
    {
        if (_audioSource == null)
            return;

        if (clip == null)
        {
            _audioSource.Stop();
            _audioSource.clip = null;
            return;
        }

        if (_audioSource.clip == clip && _audioSource.isPlaying)
            return;

        _audioSource.clip = clip;
        _audioSource.volume = audioVolume;
        _audioSource.loop = true;
        _audioSource.Play();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip, audioVolume);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _canSeePlayer ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        Gizmos.color = new Color(1f, 0.35f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, proximityCatchRadius);
    }
}
