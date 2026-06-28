using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Three-state AI: Patrolling waypoints → Chasing the player → Returning to patrol.
/// Uses Unity's NavMesh for pathfinding.
///
/// Requirements:
///   - NavMeshAgent component on this GameObject
///   - A baked NavMesh in the scene (see setup instructions below)
///   - Waypoint GameObjects assigned in the Inspector
///   - Player tagged "Player" with a PlayerController component
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AIEntity : MonoBehaviour
{
    // ── State Machine ──────────────────────────────────────────────────────

    private enum State { Patrolling, Chasing, LostPlayerPause, Returning }

    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float       waypointWaitTime = 2f;

    [Header("Detection")]
    [SerializeField] private float sightRange                  = 12f;
    [SerializeField] private float sightAngle                  = 60f;  // degrees each side — total FOV is double this
    [SerializeField] private float crouchDetectionMultiplier   = 0.5f; // halves range when player crouches
    [SerializeField] private float catchDistance               = 1.5f;
    [SerializeField] private float proximityCatchRadius        = 0.9f;

    [Header("Speed")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed  = 5.5f;

    [Header("Chase")]
    [SerializeField] private float losePlayerDelay = 3f; // seconds before giving up chase
    [SerializeField] private float lostPlayerIdleTime = 3f; // seconds to stand still before returning to patrol
    [SerializeField] private float chaseDestinationSampleRadius = 2f;

    [Header("Doors")]
    [SerializeField] private float doorOpenRange = 2.25f;
    [SerializeField] private float doorCheckInterval = 0.15f;

    [Header("Flashlight Detection")]
    [SerializeField] private bool reactToFlashlight = true;
    [SerializeField] private float flashlightDetectionRange = 25f;
    [SerializeField] private float flashlightDetectionAngle = 45f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float animationSpeedDampTime = 0.2f;

    [Header("Jumpscare")]
    [SerializeField] private JumpscareController _jumpscareController;

    [Header("Debug")]
    [SerializeField] private bool debugElevatorAI;
    [SerializeField] private float debugElevatorAILogInterval = 0.75f;

    // ── Private State ──────────────────────────────────────────────────────

    private NavMeshAgent         _agent;
    private Transform            _player;
    private PlayerController     _playerController;
    private FlashlightController _flashlight;
    private bool                 _isCatching = false;

    private State   _state              = State.Patrolling;
    private int     _waypointIndex      = 0;
    private float   _waitTimer          = 0f;
    private bool    _waitingAtWaypoint  = false;
    private bool    _canSeePlayer       = false;

    private Vector3 _lastKnownPlayerPos;
    private float   _losePlayerTimer    = 0f;
    private float   _lostPlayerIdleTimer = 0f;
    private float   _doorCheckTimer     = 0f;
    private float   _nextElevatorDebugLogTime = 0f;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        DiscoverWaypoints();
        ElevatorAIZone.EnsureRuntimeZones();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            _player           = playerObj.transform;
            _playerController = playerObj.GetComponent<PlayerController>();
            _flashlight       = playerObj.GetComponentInChildren<FlashlightController>();
        }
    }

    private void Start()
    {
        SyncRestrictedWaypointStates();
        SetState(State.Patrolling);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_isCatching) return;

        SyncRestrictedWaypointStates();
        UpdateNearbyDoors();
        if (CheckProximityCatch())
            return;

        _canSeePlayer = CheckLineOfSight() || CheckFlashlightDetection();

        UpdateAnimatorSpeed();

        switch (_state)
        {
            case State.Patrolling: UpdatePatrol();  break;
            case State.Chasing:   UpdateChase();   break;
            case State.LostPlayerPause: UpdateLostPlayerPause(); break;
            case State.Returning: UpdateReturn();  break;
        }
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

    private void UpdateAnimatorSpeed()
    {
        if (_animator == null || _agent == null)
            return;

        float targetSpeed = 0f;
        if (!_agent.isStopped)
        {
            float maxSpeed = Mathf.Max(0.01f, chaseSpeed);
            targetSpeed = Mathf.Clamp01(_agent.velocity.magnitude / maxSpeed);
        }

        _animator.SetFloat("Speed", targetSpeed, animationSpeedDampTime, Time.deltaTime);
    }

    // ── State Updates ──────────────────────────────────────────────────────

    private void UpdatePatrol()
    {
        if (_canSeePlayer)
        {
            SetState(State.Chasing);
            return;
        }

        if (waypoints.Length == 0 || FindNearestWaypointIndex() < 0)
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
            _waitTimer         = waypointWaitTime;
        }
    }

    private void UpdateChase()
    {
        if (_player != null &&
            ElevatorAIZone.TryGetZoneForPosition(_player.position, out ElevatorAIZone elevatorZone))
        {
            _losePlayerTimer = losePlayerDelay;

            if (elevatorZone.BlocksAI)
            {
                Vector3 waitPosition = elevatorZone.GetWaitPosition(transform.position);
                _lastKnownPlayerPos = waitPosition;
                _agent.SetDestination(waitPosition);
                elevatorZone.LogAIWait(transform.position, waitPosition);
                LogElevatorDebug(
                    $"Player is inside blocked elevator zone '{elevatorZone.name}' for '{elevatorZone.ElevatorName}'. " +
                    $"Destination={waitPosition}, PathStatus={_agent.pathStatus}, RemainingDistance={_agent.remainingDistance}");
                return;
            }

            Vector3 chasePosition = GetReachableChasePosition(_player.position);
            _lastKnownPlayerPos = chasePosition;
            bool destinationSet = _agent.SetDestination(chasePosition);
            LogElevatorDebug(
                $"Player is inside OPEN elevator zone '{elevatorZone.name}' for '{elevatorZone.ElevatorName}'. " +
                $"Chasing into elevator. RawPlayerPosition={_player.position}, Destination={chasePosition}, " +
                $"DestinationSet={destinationSet}, PathStatus={_agent.pathStatus}, RemainingDistance={_agent.remainingDistance}");

            if (Vector3.Distance(transform.position, _player.position) <= catchDistance)
                TriggerCatch();

            return;
        }

        if (_canSeePlayer)
        {
            Vector3 chasePosition = GetReachableChasePosition(_player.position);
            _lastKnownPlayerPos = chasePosition;
            _losePlayerTimer    = losePlayerDelay;
            _agent.SetDestination(chasePosition);

            if (Vector3.Distance(transform.position, _player.position) <= catchDistance)
                TriggerCatch();
        }
        else
        {
            // Move to last known position while counting down
            _agent.SetDestination(_lastKnownPlayerPos);
            _losePlayerTimer -= Time.deltaTime;

            if (_losePlayerTimer <= 0f)
                SetState(State.LostPlayerPause);
        }
    }

    private Vector3 GetReachableChasePosition(Vector3 desiredPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, chaseDestinationSampleRadius, NavMesh.AllAreas))
            return hit.position;

        return desiredPosition;
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

    // ── Detection ──────────────────────────────────────────────────────────

    private bool CheckLineOfSight()
    {
        if (_player == null) return false;

        // Can't see a hidden player
        if (_playerController != null && _playerController.IsHiding) return false;

        // Crouching reduces how far the AI can see
        float effectiveSightRange = (_playerController != null && _playerController.IsCrouching)
            ? sightRange * crouchDetectionMultiplier
            : sightRange;

        Vector3 toPlayer = _player.position - transform.position;
        float   distance = toPlayer.magnitude;

        // Too far away?
        if (distance > effectiveSightRange) return false;

        // Outside the field of view cone?
        if (Vector3.Angle(transform.forward, toPlayer) > sightAngle) return false;

        // Raycast from the AI's eye level toward the player's chest.
        // If the first thing hit is the player, nothing is blocking the view.
        Vector3    eyePos      = transform.position + Vector3.up * 1.6f;
        Vector3    playerChest = _player.position   + Vector3.up * 1.0f;
        RaycastHit hit;

        if (Physics.Linecast(eyePos, playerChest, out hit, ~0, QueryTriggerInteraction.Ignore))
        {
            bool hitPlayer = IsPlayerHit(hit.transform);
            LogElevatorDebug(
                $"Visibility ray sees '{hit.transform.name}' on '{hit.collider.gameObject.name}'. " +
                $"HitPlayer={hitPlayer}, PlayerCrouching={(_playerController != null && _playerController.IsCrouching)}");
            return hitPlayer;
        }

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

    private bool IsPlayerHit(Transform hitTransform)
    {
        if (hitTransform == null || _player == null)
            return false;

        return hitTransform == _player ||
               hitTransform.IsChildOf(_player) ||
               hitTransform.CompareTag("Player");
    }

    // ── Flashlight Detection ───────────────────────────────────────────────

    private bool CheckFlashlightDetection()
    {
        if (!reactToFlashlight) return false;
        if (_flashlight == null || !_flashlight.IsFlashlightOn) return false;
        if (_playerController != null && _playerController.IsHiding)  return false;

        // Direction and distance from player to AI
        Vector3 toAI     = transform.position - _player.position;
        float   distance = toAI.magnitude;

        if (distance > flashlightDetectionRange) return false;

        // Use the flashlight's forward direction — it sits on the camera
        // so it already accounts for where the player is looking vertically
        float angle = Vector3.Angle(_flashlight.transform.forward, toAI);

        if (angle > flashlightDetectionAngle) return false;

        return true;
    }

    // ── State Machine Transitions ──────────────────────────────────────────

    private void SetState(State newState)
    {
        _state = newState;

        switch (newState)
        {
            case State.Patrolling:
                _agent.isStopped = false;
                _agent.speed = patrolSpeed;
                if (_animator != null) _animator.SetBool("IsChasing", false);
                _waypointIndex = FindNearestWaypointIndex();
                if (_waypointIndex >= 0)
                    _agent.SetDestination(waypoints[_waypointIndex].position);
                break;

            case State.Chasing:
                _agent.isStopped  = false;
                _agent.speed        = chaseSpeed;
                _lastKnownPlayerPos = _player.position;
                _losePlayerTimer    = losePlayerDelay;
                if (_animator != null) _animator.SetBool("IsChasing", true);
                break;

            case State.LostPlayerPause:
                _agent.isStopped = true;
                _agent.ResetPath();
                _lostPlayerIdleTimer = lostPlayerIdleTime;
                if (_animator != null) _animator.SetBool("IsChasing", false);
                break;

            case State.Returning:
                _agent.isStopped = false;
                _agent.speed    = patrolSpeed;
                _waypointIndex  = FindNearestWaypointIndex();
                if (_animator != null) _animator.SetBool("IsChasing", false);
                if (_waypointIndex >= 0)
                    _agent.SetDestination(waypoints[_waypointIndex].position);
                break;
        }
    }

    // ── Catch & Jumpscare ──────────────────────────────────────────────────

    private void TriggerCatch()
    {
        _isCatching = true;
        CollectionInventory.ForceCloseInventory();
        ClosetHide.CancelAllClosetTransitionsForCaughtPlayer();

        // Stop the AI moving
        _agent.isStopped = true;

        // Face the player
        Vector3 dir = (_player.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        // Play jumpscare animation
        if (_animator != null)
            _animator.SetTrigger("Jumpscare");

        // Hand off to the jumpscare camera sequence
        if (_jumpscareController != null)
            _jumpscareController.TriggerJumpscare();
        else
            GameManager.Instance.OnPlayerCaught();
    }

    // ── Patrol Helpers ─────────────────────────────────────────────────────

    // ── Public API ─────────────────────────────────────────────────────────

    public bool IsChasing => _state == State.Chasing;

    private void AdvanceWaypoint()
    {
        var available = new List<int>();
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (i != _waypointIndex && IsWaypointAvailable(waypoints[i]))
                available.Add(i);
        }

        if (available.Count == 0)
        {
            if (_waypointIndex >= 0 && IsWaypointAvailable(waypoints[_waypointIndex]))
                return;

            _waypointIndex = FindNearestWaypointIndex();
            if (_waypointIndex < 0)
                return;
        }
        else
        {
            _waypointIndex = available[Random.Range(0, available.Count)];
        }

        _agent.SetDestination(waypoints[_waypointIndex].position);
    }

    private int FindNearestWaypointIndex()
    {
        if (waypoints.Length == 0) return -1;

        int   nearest  = -1;
        float shortest = float.MaxValue;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (!IsWaypointAvailable(waypoints[i]))
                continue;

            float dist = Vector3.Distance(transform.position, waypoints[i].position);
            if (dist < shortest) { shortest = dist; nearest = i; }
        }
        return nearest;
    }

    private void DiscoverWaypoints()
    {
        GameObject waypointRoot = GameObject.Find("Waypoints");
        if (waypointRoot == null)
            return;

        var discovered = new List<Transform>();
        foreach (Transform waypoint in waypointRoot.transform)
            discovered.Add(waypoint);

        if (discovered.Count > 0)
            waypoints = discovered.ToArray();
    }

    private static bool IsWaypointAvailable(Transform waypoint)
    {
        return waypoint != null && waypoint.gameObject.activeInHierarchy;
    }

    private void SyncRestrictedWaypointStates()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        foreach (Transform waypoint in waypoints)
        {
            if (waypoint == null)
                continue;

            bool? shouldBeActive = GetRestrictedWaypointState(waypoint.name, gameManager);
            if (shouldBeActive.HasValue && waypoint.gameObject.activeSelf != shouldBeActive.Value)
                waypoint.gameObject.SetActive(shouldBeActive.Value);
        }
    }

    private static bool? GetRestrictedWaypointState(string waypointName, GameManager gameManager)
    {
        switch (waypointName)
        {
            case "WP_Library":
                return gameManager.IsDoorUnlocked("Library_Door");

            case "WP_WineCellar":
                return gameManager.IsDoorUnlocked("WineCellar_Door");

            case "WP_MasterBedroom":
                return gameManager.IsDoorUnlocked("MasterBedroom_Door_Left") ||
                       gameManager.IsDoorUnlocked("MasterBedroom_Door_Right");

            case "WP_HiddenStudy":
                return gameManager.IsDoorUnlocked("HiddenStudy_Door_Left") ||
                       gameManager.IsDoorUnlocked("HiddenStudy_Door_Right");

            default:
                return null;
        }
    }

    // ── Editor Gizmos ──────────────────────────────────────────────────────

    private void LogElevatorDebug(string message)
    {
        if (!debugElevatorAI || Time.time < _nextElevatorDebugLogTime)
            return;

        _nextElevatorDebugLogTime = Time.time + Mathf.Max(0.05f, debugElevatorAILogInterval);
        Debug.Log($"[AIEntity Visibility] {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        // Sight range sphere — red when player spotted, yellow otherwise
        Gizmos.color = _canSeePlayer ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Catch distance in red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        Gizmos.color = new Color(1f, 0.35f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, proximityCatchRadius);

        // FOV cone lines
        Vector3 left  = Quaternion.Euler(0, -sightAngle, 0) * transform.forward * sightRange;
        Vector3 right = Quaternion.Euler(0,  sightAngle, 0) * transform.forward * sightRange;
        Gizmos.color  = Color.yellow;
        Gizmos.DrawRay(transform.position, left);
        Gizmos.DrawRay(transform.position, right);

        // Waypoints
        if (waypoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (Transform wp in waypoints)
            if (wp != null) Gizmos.DrawSphere(wp.position, 0.25f);
    }
}
