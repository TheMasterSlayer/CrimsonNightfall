using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SCP096EasterEggController : MonoBehaviour
{
    private static readonly Vector3 MinimumGeneratedTriggerCenter = new Vector3(0f, 1.75f, 5f);
    private static readonly Vector3 MinimumGeneratedTriggerSize = new Vector3(12f, 4f, 14f);

    [Header("Spawn Chance")]
    [SerializeField] [Range(0f, 100f)] private float spawnChancePercent = 10f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip dyingClip;
    [SerializeField] private string idleStateName = "096_Idle";
    [SerializeField] private string dyingStateName = "096_Dying";

    [Header("Detection")]
    [SerializeField] private BoxCollider detectionTrigger;
    [SerializeField] private bool requireLookingAtScp;
    [SerializeField] [Range(1f, 90f)] private float lookAngle = 18f;
    [SerializeField] private Vector3 generatedTriggerCenter = new Vector3(0f, 1.75f, 5f);
    [SerializeField] private Vector3 generatedTriggerSize = new Vector3(12f, 4f, 14f);
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private bool debugDetection;
    [SerializeField] private float debugLogInterval = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip idleLoop;
    [SerializeField] private AudioClip spottedClip;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    [Header("Message")]
    [SerializeField] private string disappearanceMessage = "HOLY COW! I hope I never see that thing again...!";
    [SerializeField] private float disappearanceMessageDuration = 5f;

    private Transform _player;
    private Camera _playerCamera;
    private AudioSource _audioSource;
    private bool _triggered;
    private float _nextDebugLogTime;

    private void Awake()
    {
        PreventUnapprovedAudioPlayback();

        if (spawnChancePercent <= 0f || Random.Range(0f, 100f) > spawnChancePercent)
        {
            StopAllAudioSources();
            gameObject.SetActive(false);
            return;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        EnsureDetectionTrigger();
        EnsureAudioSource();
        FindPlayer();
    }

    private void Start()
    {
        if (!gameObject.activeSelf)
            return;

        PlayIdle();
        PlayLoop(idleLoop);
    }

    private void Update()
    {
        if (_triggered)
            return;

        if (_player == null || _playerCamera == null)
            FindPlayer();

        if (_player == null || _playerCamera == null)
            return;

        bool insideTrigger = PlayerIsInsideTrigger();
        string lookFailureReason = string.Empty;
        bool passesLookCheck = !requireLookingAtScp || PlayerIsLookingAtScp(out lookFailureReason);

        if (debugDetection && Time.time >= _nextDebugLogTime)
        {
            _nextDebugLogTime = Time.time + debugLogInterval;
            Debug.Log(
                $"[SCP_EasterEgg] Detection check. Active={gameObject.activeInHierarchy}, " +
                $"PlayerFound={_player != null}, CameraFound={_playerCamera != null}, " +
                $"Trigger='{(detectionTrigger != null ? detectionTrigger.name : "None")}', " +
                $"InsideTrigger={insideTrigger}, RequireLook={requireLookingAtScp}, " +
                $"PassesLook={passesLookCheck}, LookFailure='{lookFailureReason}'",
                this);
        }

        if (insideTrigger && passesLookCheck)
            StartCoroutine(TriggerEasterEgg());
    }

    private void PlayIdle()
    {
        if (animator != null)
            animator.Play(idleStateName, 0, 0f);
    }

    private IEnumerator TriggerEasterEgg()
    {
        _triggered = true;
        StopAudio();
        PlayClip(spottedClip, false);

        if (animator != null)
            animator.Play(dyingStateName, 0, 0f);

        float waitTime = dyingClip != null ? dyingClip.length : 2f;
        yield return new WaitForSeconds(Mathf.Max(0.1f, waitTime));

        StopAudio();
        CollectionInventory.ShowBottomMessage(disappearanceMessage, disappearanceMessageDuration);
        Destroy(gameObject);
    }

    private bool PlayerIsInsideTrigger()
    {
        if (detectionTrigger == null)
            return false;

        Bounds triggerBounds = detectionTrigger.bounds;
        if (triggerBounds.Contains(_player.position))
            return true;

        if (_playerCamera != null && triggerBounds.Contains(_playerCamera.transform.position))
            return true;

        CharacterController characterController = _player.GetComponent<CharacterController>();
        if (characterController != null && triggerBounds.Intersects(characterController.bounds))
            return true;

        Collider playerCollider = _player.GetComponent<Collider>();
        return playerCollider != null && triggerBounds.Intersects(playerCollider.bounds);
    }

    private bool PlayerIsLookingAtScp(out string failureReason)
    {
        failureReason = string.Empty;

        Vector3 target = GetLookTarget();
        Vector3 toTarget = target - _playerCamera.transform.position;
        if (toTarget.sqrMagnitude <= 0.001f)
            return true;

        float angle = Vector3.Angle(_playerCamera.transform.forward, toTarget);
        if (angle > lookAngle)
        {
            failureReason = $"Angle {angle:0.0} is greater than Look Angle {lookAngle:0.0}.";
            return false;
        }

        float distance = toTarget.magnitude;
        if (!Physics.Raycast(_playerCamera.transform.position, toTarget.normalized, out RaycastHit hit, distance + 0.25f, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return true;

        bool hitScp = hit.transform == transform || hit.transform.IsChildOf(transform);
        if (!hitScp)
            failureReason = $"Line of sight hit '{hit.transform.name}' on '{hit.collider.name}'.";

        return hitScp;
    }

    private Vector3 GetLookTarget()
    {
        Renderer renderer = GetComponentInChildren<Renderer>(true);
        if (renderer != null)
            return renderer.bounds.center;

        Collider collider = GetComponentInChildren<Collider>(true);
        if (collider != null)
            return collider.bounds.center;

        return transform.position + Vector3.up * 1.5f;
    }

    private void EnsureDetectionTrigger()
    {
        bool createdTrigger = false;

        if (detectionTrigger == null)
        {
            detectionTrigger = FindExistingDetectionTrigger();
        }

        if (detectionTrigger == null)
        {
            GameObject triggerObject = new GameObject("SCP_EasterEgg_Trigger");
            triggerObject.transform.SetParent(transform, false);
            triggerObject.transform.localPosition = GetEffectiveGeneratedTriggerCenter();
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
            detectionTrigger = triggerObject.AddComponent<BoxCollider>();
            createdTrigger = true;
        }

        detectionTrigger.isTrigger = true;
        if (createdTrigger)
            detectionTrigger.size = GetEffectiveGeneratedTriggerSize();
    }

    private BoxCollider FindExistingDetectionTrigger()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform)
                continue;

            if (child.name != "Detection" && child.name != "SCP_EasterEgg_Trigger")
                continue;

            BoxCollider boxCollider = child.GetComponent<BoxCollider>();
            if (boxCollider != null)
                return boxCollider;
        }

        return null;
    }

    private Vector3 GetEffectiveGeneratedTriggerCenter()
    {
        return new Vector3(
            generatedTriggerCenter.x,
            Mathf.Max(generatedTriggerCenter.y, MinimumGeneratedTriggerCenter.y),
            Mathf.Max(generatedTriggerCenter.z, MinimumGeneratedTriggerCenter.z));
    }

    private Vector3 GetEffectiveGeneratedTriggerSize()
    {
        return Vector3.Max(generatedTriggerSize, MinimumGeneratedTriggerSize);
    }

    private void EnsureAudioSource()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        _audioSource.spatialBlend = 1f;
        _audioSource.volume = audioVolume;
    }

    private void PlayLoop(AudioClip clip)
    {
        PlayClip(clip, true);
    }

    private void PlayClip(AudioClip clip, bool loop)
    {
        if (_audioSource == null || clip == null)
            return;

        _audioSource.clip = clip;
        _audioSource.volume = audioVolume;
        _audioSource.loop = loop;
        _audioSource.Play();
    }

    private void StopAudio()
    {
        if (_audioSource != null)
            _audioSource.Stop();
    }

    private void OnDisable()
    {
        StopAllAudioSources();
    }

    private void PreventUnapprovedAudioPlayback()
    {
        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
        {
            if (source == null)
                continue;

            source.playOnAwake = false;
            source.Stop();
        }
    }

    private void StopAllAudioSources()
    {
        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
        {
            if (source != null)
                source.Stop();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
            return;

        _player = playerObject.transform;
        _playerCamera = playerObject.GetComponentInChildren<Camera>(true);
    }
}
