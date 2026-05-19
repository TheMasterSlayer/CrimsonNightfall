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

    private enum State { Patrolling, Chasing, Returning }

    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float       waypointWaitTime = 2f;

    [Header("Detection")]
    [SerializeField] private float sightRange                  = 12f;
    [SerializeField] private float sightAngle                  = 60f;  // degrees each side — total FOV is double this
    [SerializeField] private float crouchDetectionMultiplier   = 0.5f; // halves range when player crouches
    [SerializeField] private float catchDistance               = 1.5f;

    [Header("Speed")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed  = 5.5f;

    [Header("Chase")]
    [SerializeField] private float losePlayerDelay = 3f; // seconds before giving up chase

    // ── Private State ──────────────────────────────────────────────────────

    private NavMeshAgent     _agent;
    private Transform        _player;
    private PlayerController _playerController;

    private State   _state              = State.Patrolling;
    private int     _waypointIndex      = 0;
    private float   _waitTimer          = 0f;
    private bool    _waitingAtWaypoint  = false;
    private bool    _canSeePlayer       = false;

    private Vector3 _lastKnownPlayerPos;
    private float   _losePlayerTimer    = 0f;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            _player           = playerObj.transform;
            _playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    private void Start()
    {
        SetState(State.Patrolling);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        _canSeePlayer = CheckLineOfSight();

        switch (_state)
        {
            case State.Patrolling: UpdatePatrol();  break;
            case State.Chasing:   UpdateChase();   break;
            case State.Returning: UpdateReturn();  break;
        }
    }

    // ── State Updates ──────────────────────────────────────────────────────

    private void UpdatePatrol()
    {
        if (_canSeePlayer)
        {
            SetState(State.Chasing);
            return;
        }

        if (waypoints.Length == 0) return;

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
        if (_canSeePlayer)
        {
            _lastKnownPlayerPos = _player.position;
            _losePlayerTimer    = losePlayerDelay;
            _agent.SetDestination(_player.position);

            if (Vector3.Distance(transform.position, _player.position) <= catchDistance)
                GameManager.Instance.OnPlayerCaught();
        }
        else
        {
            // Move to last known position while counting down
            _agent.SetDestination(_lastKnownPlayerPos);
            _losePlayerTimer -= Time.deltaTime;

            if (_losePlayerTimer <= 0f)
                SetState(State.Returning);
        }
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

        if (Physics.Linecast(eyePos, playerChest, out hit))
            return hit.transform.CompareTag("Player");

        return true;
    }

    // ── State Machine Transitions ──────────────────────────────────────────

    private void SetState(State newState)
    {
        _state = newState;

        switch (newState)
        {
            case State.Patrolling:
                _agent.speed = patrolSpeed;
                if (waypoints.Length > 0)
                    _agent.SetDestination(waypoints[_waypointIndex].position);
                Debug.Log("[AI] Resuming patrol.");
                break;

            case State.Chasing:
                _agent.speed        = chaseSpeed;
                _lastKnownPlayerPos = _player.position;
                _losePlayerTimer    = losePlayerDelay;
                Debug.Log("[AI] Player spotted — chasing!");
                break;

            case State.Returning:
                _agent.speed    = patrolSpeed;
                _waypointIndex  = FindNearestWaypointIndex();
                if (waypoints.Length > 0)
                    _agent.SetDestination(waypoints[_waypointIndex].position);
                Debug.Log("[AI] Lost the player. Returning to patrol route.");
                break;
        }
    }

    // ── Patrol Helpers ─────────────────────────────────────────────────────

    private void AdvanceWaypoint()
    {
        _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
        _agent.SetDestination(waypoints[_waypointIndex].position);
    }

    private int FindNearestWaypointIndex()
    {
        if (waypoints.Length == 0) return 0;

        int   nearest  = 0;
        float shortest = float.MaxValue;

        for (int i = 0; i < waypoints.Length; i++)
        {
            float dist = Vector3.Distance(transform.position, waypoints[i].position);
            if (dist < shortest) { shortest = dist; nearest = i; }
        }
        return nearest;
    }

    // ── Editor Gizmos ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Sight range sphere — red when player spotted, yellow otherwise
        Gizmos.color = _canSeePlayer ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Catch distance in red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

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