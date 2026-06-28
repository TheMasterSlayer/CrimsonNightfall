using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SCP096EasterEggController : MonoBehaviour
{
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
    [SerializeField] [Range(1f, 90f)] private float lookAngle = 18f;
    [SerializeField] private Vector3 generatedTriggerCenter = new Vector3(0f, 1.5f, 3f);
    [SerializeField] private Vector3 generatedTriggerSize = new Vector3(5f, 3f, 6f);
    [SerializeField] private LayerMask lineOfSightMask = ~0;

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

    private void Awake()
    {
        if (spawnChancePercent <= 0f || Random.Range(0f, 100f) > spawnChancePercent)
        {
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

        if (PlayerIsInsideTrigger() && PlayerIsLookingAtScp())
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

        return detectionTrigger.bounds.Contains(_player.position);
    }

    private bool PlayerIsLookingAtScp()
    {
        Vector3 target = GetLookTarget();
        Vector3 toTarget = target - _playerCamera.transform.position;
        if (toTarget.sqrMagnitude <= 0.001f)
            return true;

        if (Vector3.Angle(_playerCamera.transform.forward, toTarget) > lookAngle)
            return false;

        float distance = toTarget.magnitude;
        if (!Physics.Raycast(_playerCamera.transform.position, toTarget.normalized, out RaycastHit hit, distance + 0.25f, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform == transform || hit.transform.IsChildOf(transform);
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
        if (detectionTrigger == null)
        {
            Transform existing = transform.Find("SCP_EasterEgg_Trigger");
            if (existing != null)
                detectionTrigger = existing.GetComponent<BoxCollider>();
        }

        if (detectionTrigger == null)
        {
            GameObject triggerObject = new GameObject("SCP_EasterEgg_Trigger");
            triggerObject.transform.SetParent(transform, false);
            triggerObject.transform.localPosition = generatedTriggerCenter;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
            detectionTrigger = triggerObject.AddComponent<BoxCollider>();
            detectionTrigger.size = generatedTriggerSize;
        }

        detectionTrigger.isTrigger = true;
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

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
            return;

        _player = playerObject.transform;
        _playerCamera = playerObject.GetComponentInChildren<Camera>(true);
    }
}
