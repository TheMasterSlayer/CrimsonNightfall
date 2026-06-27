using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class SCPRoomAmbienceZone : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip ambienceClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.75f;
    [SerializeField] private bool playOnStartIfPlayerInside = true;
    [SerializeField] private bool pollPlayerPosition = true;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    private AudioSource _audioSource;
    private BoxCollider _trigger;
    private Transform _player;
    private bool _playerInside;
    private float _targetVolume;

    private void Awake()
    {
        _trigger = GetComponent<BoxCollider>();
        _trigger.isTrigger = true;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.clip = ambienceClip;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 0f;

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            _player = playerObject.transform;
    }

    private void Start()
    {
        if (!playOnStartIfPlayerInside || _player == null)
            return;

        if (IsWorldPointInsideTrigger(_player.position))
            SetPlayerInside(true);
    }

    private void Update()
    {
        if (_audioSource == null)
            return;

        if (pollPlayerPosition)
            UpdatePlayerInsideFromPosition();

        if (_audioSource.clip != ambienceClip)
            _audioSource.clip = ambienceClip;

        float fadeDuration = _targetVolume > _audioSource.volume ? fadeInDuration : fadeOutDuration;
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        _audioSource.volume = Mathf.MoveTowards(
            _audioSource.volume,
            _targetVolume,
            Time.deltaTime * volume / fadeDuration);

        if (!_playerInside && _audioSource.isPlaying && _audioSource.volume <= 0.001f)
            _audioSource.Stop();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            SetPlayerInside(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            SetPlayerInside(false);
    }

    private void UpdatePlayerInsideFromPosition()
    {
        if (_player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                _player = playerObject.transform;
        }

        if (_player == null)
            return;

        bool inside = IsWorldPointInsideTrigger(_player.position);
        if (inside != _playerInside)
            SetPlayerInside(inside);
    }

    private void SetPlayerInside(bool inside)
    {
        _playerInside = inside;
        _targetVolume = inside ? volume : 0f;

        if (inside && ambienceClip != null && !_audioSource.isPlaying)
        {
            _audioSource.clip = ambienceClip;
            _audioSource.Play();
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player"))
            return true;

        return other.GetComponentInParent<PlayerController>() != null;
    }

    private bool IsWorldPointInsideTrigger(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint) - _trigger.center;
        Vector3 halfSize = _trigger.size * 0.5f;

        return Mathf.Abs(localPoint.x) <= halfSize.x &&
               Mathf.Abs(localPoint.y) <= halfSize.y &&
               Mathf.Abs(localPoint.z) <= halfSize.z;
    }
}
