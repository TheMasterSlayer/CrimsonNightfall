using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class SCP096Controller : MonoBehaviour
{
    private enum State
    {
        Idle,
        Distress,
        Chasing,
        Caught
    }

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip distressClip;
    [SerializeField] private AnimationClip sprintClip;
    [SerializeField] private string idleStateName = "096_Idle";
    [SerializeField] private string distressStateName = "096_Distress";
    [SerializeField] private string sprintStateName = "096_Sprint";

    [Header("Distress Camera")]
    [SerializeField] private Camera scpCamera;
    [SerializeField] private float cameraSwitchTime = 28f;

    [Header("SCP Elevator Intro")]
    [SerializeField] private float introFreezeDuration = 1f;
    [SerializeField] private float introScpCameraDuration = 2f;
    [SerializeField] private string introPanicMessage = "WHAT THE... I NEED TO RUN!!";
    [SerializeField] private float introPanicMessageDuration = 4f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 11f;
    [SerializeField] private float chaseAcceleration = 80f;
    [SerializeField] private float chaseAngularSpeed = 1080f;
    [SerializeField] private float directChargeAssistSpeed = 4f;
    [SerializeField] private float stuckVelocityThreshold = 0.35f;
    [SerializeField] private float stuckAssistDelay = 0.45f;
    [SerializeField] private bool disableObstacleAvoidanceWhileChasing = true;
    [SerializeField] private float catchDistance = 1.6f;
    [SerializeField] private float destinationRefreshInterval = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioClip idleLoop;
    [SerializeField] private AudioClip distressLoop;
    [SerializeField] private AudioClip chaseLoop;
    [SerializeField] private AudioClip caughtClip;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    [Header("SCP Lose Screen")]
    [SerializeField] private float jumpscareHoldDuration = 1.25f;
    [SerializeField] private float loseFadeDuration = 2.5f;
    [SerializeField] private float questionFadeDuration = 1.5f;
    [SerializeField] private float mainMenuButtonDelay = 2f;
    [SerializeField] private string mainMenuScene = "MainMenu";

    private NavMeshAgent _agent;
    private Transform _player;
    private PlayerController _playerController;
    private Camera _playerCamera;
    private AudioSource _audioSource;
    private State _state = State.Idle;
    private float _destinationTimer;
    private float _stuckTimer;
    private CanvasGroup _loseGroup;
    private Image _loseBackground;
    private Text _questionText;
    private CanvasGroup _buttonGroup;

    public bool IsChasing => _state == State.Chasing;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        ElevatorAIZone.EnsureRuntimeZones();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (scpCamera == null)
        {
            Transform cameraChild = transform.Find("Camera");
            if (cameraChild != null)
                scpCamera = cameraChild.GetComponent<Camera>();
        }

        if (scpCamera != null)
            scpCamera.enabled = false;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.volume = audioVolume;

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
            _playerController = playerObject.GetComponent<PlayerController>();
            _playerCamera = playerObject.GetComponentInChildren<Camera>(true);
        }

        BuildLoseScreen();
    }

    private void Start()
    {
        SetIdle();
    }

    private void Update()
    {
        if (_state != State.Chasing || _player == null || GameManager.Instance != null && GameManager.Instance.IsGameOver)
            return;

        _destinationTimer -= Time.deltaTime;
        if (_destinationTimer <= 0f)
        {
            _destinationTimer = destinationRefreshInterval;
            _agent.SetDestination(GetChaseDestination());
        }

        if (!IsPlayerInBlockedElevatorZone())
            ApplyDirectChargeAssist();

        if (Vector3.Distance(transform.position, _player.position) <= catchDistance)
            StartCoroutine(CatchPlayerSequence());
    }

    public void TriggerDistressSequence(Transform introAnchor = null)
    {
        if (_state != State.Idle)
            return;

        StartCoroutine(DistressSequence(introAnchor));
    }

    private void SetIdle()
    {
        _state = State.Idle;
        _agent.isStopped = true;
        _agent.ResetPath();

        if (animator != null)
            animator.Play(idleStateName, 0, 0f);

        PlayLoop(idleLoop);
    }

    private IEnumerator DistressSequence(Transform introAnchor)
    {
        _state = State.Distress;
        _agent.isStopped = true;
        _agent.ResetPath();

        if (animator != null)
            animator.Play(distressStateName, 0, 0f);

        PlayLoop(distressLoop);
        float distressStartedAt = Time.time;

        yield return PlayElevatorIntro(introAnchor);

        float distressDuration = distressClip != null ? distressClip.length : Mathf.Max(cameraSwitchTime, 30f);
        float switchTime = Mathf.Clamp(cameraSwitchTime, 0f, distressDuration);
        float elapsedSinceDistressStarted = Time.time - distressStartedAt;

        yield return new WaitForSeconds(Mathf.Max(0f, switchTime - elapsedSinceDistressStarted));
        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        SwitchToScpCamera(true);

        yield return new WaitForSeconds(Mathf.Max(0f, distressDuration - switchTime));
        SwitchToScpCamera(false);
        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        StartChasing();
    }

    private IEnumerator PlayElevatorIntro(Transform introAnchor)
    {
        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        CharacterController characterController = _player != null ? _player.GetComponent<CharacterController>() : null;
        if (characterController != null)
            characterController.enabled = false;

        if (_player != null && introAnchor != null)
            _player.SetPositionAndRotation(introAnchor.position, introAnchor.rotation);

        if (characterController != null)
            characterController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yield return new WaitForSeconds(Mathf.Max(0f, introFreezeDuration));

        SwitchToScpCamera(true);
        yield return new WaitForSeconds(Mathf.Max(0f, introScpCameraDuration));
        SwitchToScpCamera(false);

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        if (!string.IsNullOrWhiteSpace(introPanicMessage))
            CollectionInventory.ShowBottomMessage(introPanicMessage, introPanicMessageDuration);
    }

    private void StartChasing()
    {
        _state = State.Chasing;
        _agent.isStopped = false;
        _agent.speed = chaseSpeed;
        _agent.acceleration = chaseAcceleration;
        _agent.angularSpeed = chaseAngularSpeed;
        _agent.autoBraking = false;
        _agent.stoppingDistance = 0f;

        if (disableObstacleAvoidanceWhileChasing)
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        if (animator != null)
            animator.Play(sprintStateName, 0, 0f);

        PlayLoop(chaseLoop);

        if (_player != null)
            _agent.SetDestination(GetChaseDestination());
    }

    private Vector3 GetChaseDestination()
    {
        if (_player != null &&
            ElevatorAIZone.TryGetZoneForPosition(_player.position, out ElevatorAIZone elevatorZone) &&
            elevatorZone.BlocksAI)
        {
            return elevatorZone.GetWaitPosition(transform.position);
        }

        return _player != null ? _player.position : transform.position;
    }

    private bool IsPlayerInBlockedElevatorZone()
    {
        return _player != null &&
               ElevatorAIZone.TryGetZoneForPosition(_player.position, out ElevatorAIZone elevatorZone) &&
               elevatorZone.BlocksAI;
    }

    private void ApplyDirectChargeAssist()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _player == null)
            return;

        if (_agent.velocity.magnitude > stuckVelocityThreshold)
        {
            _stuckTimer = 0f;
            return;
        }

        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < stuckAssistDelay)
            return;

        Vector3 direction = _player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
            return;

        _agent.Move(direction.normalized * directChargeAssistSpeed * Time.deltaTime);
        _agent.SetDestination(GetChaseDestination());
    }

    private IEnumerator CatchPlayerSequence()
    {
        if (_state == State.Caught)
            yield break;

        _state = State.Caught;
        _agent.isStopped = true;
        _agent.ResetPath();
        CollectionInventory.ForceCloseInventory();
        ClosetHide.CancelAllClosetTransitionsForCaughtPlayer();
        PlayOneShot(caughtClip);

        if (_playerController != null)
            _playerController.SetInputEnabled(false);

        SwitchToScpCamera(true);
        yield return new WaitForSeconds(jumpscareHoldDuration);
        yield return ShowScpLoseScreen();
    }

    private IEnumerator ShowScpLoseScreen()
    {
        EnsureEventSystem();

        _loseGroup.gameObject.SetActive(true);
        _loseGroup.alpha = 1f;
        _loseBackground.color = new Color(0f, 0f, 0f, 0f);
        _questionText.canvasRenderer.SetAlpha(0f);
        _buttonGroup.alpha = 0f;
        _buttonGroup.interactable = false;
        _buttonGroup.blocksRaycasts = false;

        float startVolume = AudioListener.volume;
        for (float elapsed = 0f; elapsed < loseFadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / loseFadeDuration);
            _loseBackground.color = new Color(0f, 0f, 0f, t);
            AudioListener.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        _loseBackground.color = Color.black;
        AudioListener.volume = 0f;

        _questionText.text = "???";
        _questionText.CrossFadeAlpha(1f, questionFadeDuration, true);
        yield return new WaitForSecondsRealtime(questionFadeDuration + mainMenuButtonDelay);

        for (float elapsed = 0f; elapsed < questionFadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            _buttonGroup.alpha = Mathf.Clamp01(elapsed / questionFadeDuration);
            yield return null;
        }

        _buttonGroup.alpha = 1f;
        _buttonGroup.interactable = true;
        _buttonGroup.blocksRaycasts = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SwitchToScpCamera(bool useScpCamera)
    {
        if (_playerCamera != null)
            _playerCamera.enabled = !useScpCamera;

        if (scpCamera != null)
            scpCamera.enabled = useScpCamera;
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
        if (_audioSource == null || clip == null)
            return;

        _audioSource.PlayOneShot(clip, audioVolume);
    }

    private void BuildLoseScreen()
    {
        GameObject canvasObject = new GameObject("SCP-096 Lose Screen Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreateRect("SCP-096 Lose Screen", canvasObject.transform);
        Stretch(panel.GetComponent<RectTransform>());
        _loseGroup = panel.AddComponent<CanvasGroup>();
        _loseBackground = panel.AddComponent<Image>();
        _loseBackground.color = new Color(0f, 0f, 0f, 0f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _questionText = CreateText(panel.transform, "Question Text", "???", font, 96, Color.white, TextAnchor.MiddleCenter);
        SetAnchors(_questionText.rectTransform, new Vector2(0.15f, 0.38f), new Vector2(0.85f, 0.62f));

        Button button = CreateButton(panel.transform, font);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.18f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(420f, 76f);

        _buttonGroup = button.gameObject.AddComponent<CanvasGroup>();
        _buttonGroup.alpha = 0f;
        _buttonGroup.interactable = false;
        _buttonGroup.blocksRaycasts = false;

        panel.SetActive(false);
    }

    private Button CreateButton(Transform parent, Font font)
    {
        GameObject buttonObject = CreateRect("Main Menu Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.075f, 0.075f, 0.08f, 1f);

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.035f, 0.055f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => SceneManager.LoadScene(mainMenuScene));

        Text label = CreateText(buttonObject.transform, "Label", "MAIN MENU", font, 28, new Color(0.88f, 0.86f, 0.84f), TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        label.fontStyle = FontStyle.Bold;
        return button;
    }

    private static GameObject CreateRect(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Text CreateText(Transform parent, string objectName, string value, Font font, int size, Color color, TextAnchor alignment)
    {
        GameObject textObject = CreateRect(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
