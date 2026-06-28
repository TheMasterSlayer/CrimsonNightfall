using System.Collections;
using UnityEngine;

/// <summary>
/// Handles all entity audio — ambient patrol sounds, chase alert,
/// and footsteps. Attach to the AIEntity GameObject.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class EntitySounds : MonoBehaviour
{
    [Header("Patrol Ambient Sounds")]
    [Tooltip("Random sounds that play occasionally while patrolling")]
    [SerializeField] private AudioClip[] ambientSounds;
    [SerializeField] private float ambientMinInterval = 6f;
    [SerializeField] private float ambientMaxInterval = 15f;

    [Header("Chase Sounds")]
    [Tooltip("Plays once when the entity first spots the player")]
    [SerializeField] private AudioClip alertSound;
    [Tooltip("Loops while actively chasing")]
    [SerializeField] private AudioClip chaseLoopSound;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float walkStepInterval = 0.55f;
    [SerializeField] private float runStepInterval  = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.5f;

    // ── Private State ──────────────────────────────────────────────────────

    private AudioSource _audioSource;
    private AudioSource _footstepSource;
    private AIEntity    _ai;
    private UnityEngine.AI.NavMeshAgent _agent;

    private bool  _wasChasing     = false;
    private float _footstepTimer  = 0f;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 1f; // 3D sound — volume drops with distance
        _audioSource.rolloffMode  = AudioRolloffMode.Linear;
        _audioSource.maxDistance  = 30f;

        // Second AudioSource for footsteps so they don't interrupt ambient sounds
        _footstepSource              = gameObject.AddComponent<AudioSource>();
        _footstepSource.spatialBlend = 1f;
        _footstepSource.rolloffMode  = AudioRolloffMode.Linear;
        _footstepSource.maxDistance  = 15f;
        _footstepSource.volume       = footstepVolume;

        _ai = GetComponent<AIEntity>();
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    private void Start()
    {
        StartCoroutine(AmbientSoundLoop());
    }

    private void Update()
    {
        HandleChaseAudio();
        HandleFootsteps();
    }

    // ── Ambient ────────────────────────────────────────────────────────────

    private IEnumerator AmbientSoundLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(ambientMinInterval, ambientMaxInterval)
            );

            // Only play ambient sounds while patrolling — not during chase
            if (!_ai.IsChasing && ambientSounds.Length > 0)
            {
                AudioClip clip = ambientSounds[Random.Range(0, ambientSounds.Length)];
                _audioSource.PlayOneShot(clip);
            }
        }
    }

    // ── Chase ──────────────────────────────────────────────────────────────

    private void HandleChaseAudio()
    {
        bool isChasing = _ai.IsChasing;

        // Just started chasing — play alert then start loop
        if (isChasing && !_wasChasing)
        {
            if (alertSound != null)
                _audioSource.PlayOneShot(alertSound);

            if (chaseLoopSound != null)
            {
                _audioSource.clip = chaseLoopSound;
                _audioSource.loop = true;
                // Delay loop slightly so alert plays first
                _audioSource.PlayDelayed(alertSound != null ? alertSound.length : 0f);
            }
        }

        // Stopped chasing — stop the loop
        if (!isChasing && _wasChasing)
        {
            _audioSource.loop = false;
            _audioSource.Stop();
        }

        _wasChasing = isChasing;
    }

    // ── Footsteps ──────────────────────────────────────────────────────────

    private void HandleFootsteps()
    {
        if (footstepSounds.Length == 0) return;

        NavMeshAgentMoving(out bool isMoving, out bool isRunning);

        if (!isMoving)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            AudioClip step = footstepSounds[Random.Range(0, footstepSounds.Length)];
            _footstepSource.PlayOneShot(step, footstepVolume);
            _footstepTimer = isRunning ? runStepInterval : walkStepInterval;
        }
    }

    private void NavMeshAgentMoving(out bool isMoving, out bool isRunning)
    {
        float speed = _agent != null ? _agent.velocity.magnitude : 0f;
        isMoving  = speed > 0.1f;
        isRunning = _ai.IsChasing;
    }
}
