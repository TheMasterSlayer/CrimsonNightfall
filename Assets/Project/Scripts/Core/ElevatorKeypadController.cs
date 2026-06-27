using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ElevatorKeypadController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 35f;

    [Header("Keypad View")]
    [SerializeField] private Transform viewPoint;
    [SerializeField] private float viewDistance = 0.65f;
    [SerializeField] private float viewMoveSpeed = 8f;

    [Header("Travel Targets")]
    [SerializeField] private Transform basementElevator;
    [SerializeField] private Transform mainElevator;
    [SerializeField] private Transform upperElevator;
    [SerializeField] private Transform secretElevator;
    [SerializeField] private string secretElevatorName = "Secret_Elevator";
    [SerializeField] private Transform scpElevator;
    [SerializeField] private string scpElevatorName = "SCP_Elevator";
    [SerializeField] private int currentFloor = 2;
    [SerializeField] private float travelDurationPerFloor = 2.5f;
    [SerializeField] private float centerPlayerDuration = 0.35f;
    [SerializeField] private float floorAnchorDetectionRadius = 5f;

    [Header("Player Travel Anchors")]
    [SerializeField] private Transform basementPlayerAnchor;
    [SerializeField] private Transform mainPlayerAnchor;
    [SerializeField] private Transform upperPlayerAnchor;
    [SerializeField] private Transform secretPlayerAnchor;
    [SerializeField] private Transform exitPlayerAnchor;
    [SerializeField] private Transform scpPlayerAnchor;

    [Header("Doors")]
    [SerializeField] private ElevatorDoorController elevatorDoors;

    [Header("Audio")]
    [SerializeField] private AudioClip keypadButtonPressedSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    [Header("SCP-096")]
    [SerializeField] private bool cube004StartsScp096;
    [SerializeField] private bool onlyCube004CanBePressed;
    [SerializeField] private bool onlyCube005CanBePressed;
    [SerializeField] private SCP096Controller scp096Controller;
    [SerializeField] private bool cube005ReturnsToSecretElevatorAndMarksScpSurvived;

    [Header("Secret Floor")]
    [SerializeField] private string secretFloorLockedMessage = "This floor is locked.";

    [Header("SCP Elevator Malfunction")]
    [SerializeField] private string rockyCharmsItemId = "RockyCharms";
    [SerializeField] private float scpFadeDuration = 1f;
    [SerializeField] private float scpBlackoutDuration = 5f;
    [SerializeField] private float scpMessageDelayAfterFade = 1f;
    [SerializeField] private string scpAftermathMessage = "What just happened... I think the elevator malfunctioned... Where am I...";

    [Header("Exit Elevator Return Transition")]
    [SerializeField] private float exitReturnFadeDuration = 1f;
    [SerializeField] private float exitReturnStartupDelay = 3f;
    [SerializeField] private float exitReturnBlackoutDuration = 5f;

    private Camera _camera;
    private PlayerController _playerController;
    private CharacterController _characterController;
    private Vector3 _savedCameraLocalPosition;
    private Quaternion _savedCameraLocalRotation;
    private Vector3 _runtimeViewPosition;
    private Quaternion _runtimeViewRotation;
    private CanvasGroup _fadeGroup;
    private bool _viewingKeypad;
    private bool _travelling;

    private void Awake()
    {
        if (elevatorDoors == null)
            elevatorDoors = GetComponentInParent<ElevatorDoorController>();

        if (scp096Controller == null)
            scp096Controller = FindFirstObjectByType<SCP096Controller>(FindObjectsInactive.Include);

        AutoFindTargets();
        EnsureButtonColliders();
    }

    private void Update()
    {
        if (_travelling)
            return;

        if (_viewingKeypad)
        {
            UpdateKeypadView();
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) && IsFocused())
            EnterKeypadView();
    }

    private void EnterKeypadView()
    {
        _camera = Camera.main;
        _playerController = FindFirstObjectByType<PlayerController>();
        _characterController = _playerController != null ? _playerController.GetComponent<CharacterController>() : null;

        if (_camera == null || _playerController == null)
            return;

        _savedCameraLocalPosition = _camera.transform.localPosition;
        _savedCameraLocalRotation = _camera.transform.localRotation;
        CalculateRuntimeView();

        _playerController.SetInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _viewingKeypad = true;
    }

    private void ExitKeypadView(bool restorePlayerInput = true)
    {
        if (_camera != null)
        {
            _camera.transform.localPosition = _savedCameraLocalPosition;
            _camera.transform.localRotation = _savedCameraLocalRotation;
        }

        if (restorePlayerInput && _playerController != null)
            _playerController.SetInputEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _viewingKeypad = false;
    }

    private void UpdateKeypadView()
    {
        if (_camera == null)
            return;

        Vector3 targetPosition = viewPoint != null
            ? viewPoint.position
            : _runtimeViewPosition;
        Quaternion targetRotation = viewPoint != null
            ? viewPoint.rotation
            : _runtimeViewRotation;

        _camera.transform.position = Vector3.Lerp(_camera.transform.position, targetPosition, viewMoveSpeed * Time.deltaTime);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, targetRotation, viewMoveSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
        {
            ExitKeypadView();
            return;
        }

        if (Input.GetMouseButtonDown(0))
            TryPressKeypadButton();
    }

    private void TryPressKeypadButton()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Collide))
            return;

        Transform hitTransform = hit.transform;
        if (!hitTransform.IsChildOf(transform) && hitTransform != transform)
            return;

        if (onlyCube004CanBePressed && hitTransform.name != "Cube.004")
            return;

        if (onlyCube005CanBePressed && hitTransform.name != "Cube.005")
            return;

        int targetFloor = GetFloorForButton(hitTransform.name);
        if (targetFloor == -2)
            return;

        PlayOneShot(keypadButtonPressedSound, hit.point);
        RefreshCurrentFloorFromPlayerAnchor();

        if (targetFloor == -1)
        {
            if (cube004StartsScp096 && scp096Controller != null)
            {
                if (elevatorDoors != null)
                    elevatorDoors.OpenAndStayOpen();

                ExitKeypadView(false);
                gameObject.SetActive(false);
                scp096Controller.TriggerDistressSequence(scpPlayerAnchor);
                return;
            }

            if (elevatorDoors != null)
                elevatorDoors.Toggle();
            return;
        }

        if (targetFloor == 0 && cube005ReturnsToSecretElevatorAndMarksScpSurvived)
        {
            ScpElevatorProgress.MarkSurvived();
            StartCoroutine(TravelToSecretFromExitElevator());
            return;
        }

        if (targetFloor < 0 || targetFloor == currentFloor)
            return;

        if (targetFloor == 0 && ScpElevatorProgress.SurvivedScpElevator)
        {
            StartCoroutine(TravelToFloor(0));
            return;
        }

        if (targetFloor == 0 && CanTriggerScpElevatorMalfunction())
        {
            StartCoroutine(TravelToScpElevator());
            return;
        }

        if (targetFloor == 0 && !SecretElevatorKeyAccess.IsUnlocked)
        {
            CollectionInventory.ShowBottomMessage(secretFloorLockedMessage);
            return;
        }

        StartCoroutine(TravelToFloor(targetFloor));
    }

    private void CalculateRuntimeView()
    {
        if (_camera == null)
            return;

        Vector3 focusPoint = GetFocusPoint();
        Vector3 directionFromKeypadToPlayer = _camera.transform.position - focusPoint;

        if (directionFromKeypadToPlayer.sqrMagnitude < 0.01f)
            directionFromKeypadToPlayer = -transform.forward;

        directionFromKeypadToPlayer.Normalize();
        _runtimeViewPosition = focusPoint + directionFromKeypadToPlayer * viewDistance;
        _runtimeViewPosition.y = Mathf.Lerp(_runtimeViewPosition.y, focusPoint.y, 0.35f);
        _runtimeViewRotation = Quaternion.LookRotation(focusPoint - _runtimeViewPosition, Vector3.up);
    }

    private IEnumerator TravelToFloor(int targetFloor)
    {
        Transform target = GetAnchorForFloor(targetFloor);
        if (target == null)
            target = GetTargetForFloor(targetFloor);

        if (target == null || _playerController == null)
            yield break;

        _travelling = true;
        _viewingKeypad = false;

        if (_camera != null)
        {
            _camera.transform.localPosition = _savedCameraLocalPosition;
            _camera.transform.localRotation = _savedCameraLocalRotation;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Transform playerTransform = _playerController.transform;
        Vector3 targetPosition = target.position;
        Quaternion targetRotation = target.rotation;
        float duration = Mathf.Max(0.25f, Mathf.Abs(targetFloor - currentFloor) * travelDurationPerFloor);
        ElevatorDoorController departureDoors = GetDoorControllerForFloor(currentFloor) ?? elevatorDoors;
        ElevatorDoorController arrivalDoors = GetDoorControllerForFloor(targetFloor);

        if (_characterController != null)
            _characterController.enabled = false;

        Transform currentAnchor = GetAnchorForFloor(currentFloor);
        if (currentAnchor != null)
            yield return MovePlayer(playerTransform, currentAnchor.position, currentAnchor.rotation, centerPlayerDuration);

        CollectionInventory.ShowBottomMessage("The elevator begins moving.", 2f);
        departureDoors?.PlayElevatorMovingSound();
        yield return MovePlayer(playerTransform, targetPosition, targetRotation, duration);
        departureDoors?.StopElevatorMovingSound();
        arrivalDoors?.PlayElevatorArrivalSound();

        if (_characterController != null)
            _characterController.enabled = true;

        currentFloor = targetFloor;
        _travelling = false;

        if (_playerController != null)
            _playerController.SetInputEnabled(true);
    }

    private int GetFloorForButton(string buttonName)
    {
        if (buttonName == "Cube.001")
            return 3;

        if (buttonName == "Cube.002")
            return 2;

        if (buttonName == "Cube.003")
            return 1;

        if (buttonName == "Cube.004")
            return -1;

        if (buttonName == "Cube.005")
            return 0;

        return -2;
    }

    private Transform GetTargetForFloor(int floor)
    {
        if (floor == 0)
            return secretElevator;

        if (floor == 1)
            return basementElevator;

        if (floor == 2)
            return mainElevator;

        if (floor == 3)
            return upperElevator;

        return null;
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 toKeypad = GetFocusPoint() - camera.transform.position;
        if (toKeypad.magnitude > interactionRange)
            return false;

        return Vector3.Angle(camera.transform.forward, toKeypad) <= interactionAngle;
    }

    private Vector3 GetFocusPoint()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : transform.position;
    }

    private void AutoFindTargets()
    {
        if (basementElevator == null)
            basementElevator = FindSceneTransform("Basement_Elevator");

        if (mainElevator == null)
            mainElevator = FindSceneTransform("Main_Elevator");

        if (upperElevator == null)
            upperElevator = FindSceneTransform("Upper_Elevator");

        if (secretElevator == null && !string.IsNullOrWhiteSpace(secretElevatorName))
            secretElevator = FindSceneTransform(secretElevatorName);

        if (scpElevator == null && !string.IsNullOrWhiteSpace(scpElevatorName))
            scpElevator = FindSceneTransform(scpElevatorName);

        if (basementPlayerAnchor == null)
            basementPlayerAnchor = FindSceneTransformStartingWith("Basement_Elevator_PlayerAnchor");

        if (mainPlayerAnchor == null)
            mainPlayerAnchor = FindSceneTransformStartingWith("Main_Elevator_PlayerAnchor");

        if (upperPlayerAnchor == null)
            upperPlayerAnchor = FindSceneTransformStartingWith("Upper_Elevator_PlayerAnchor");

        if (secretPlayerAnchor == null)
            secretPlayerAnchor = FindSceneTransformStartingWith("Secret_Elevator_PlayerAnchor");

        if (exitPlayerAnchor == null)
            exitPlayerAnchor = FindSceneTransformStartingWith("Exit_Elevator_PlayerAnchor");

        if (scpPlayerAnchor == null)
            scpPlayerAnchor = FindSceneTransformStartingWith("SCP_Elevator_PlayerAnchor");
    }

    private void EnsureButtonColliders()
    {
        AddColliderIfMissing("Cube.001");
        AddColliderIfMissing("Cube.002");
        AddColliderIfMissing("Cube.003");
        AddColliderIfMissing("Cube.004");
        AddColliderIfMissing("Cube.005");
    }

    private void AddColliderIfMissing(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null || child.GetComponent<Collider>() != null)
            return;

        child.gameObject.AddComponent<BoxCollider>();
    }

    private static Transform FindSceneTransform(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private static Transform FindSceneTransformStartingWith(string objectNamePrefix)
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene.IsValid() && candidate.name.StartsWith(objectNamePrefix))
                return candidate;
        }

        return null;
    }

    private Transform GetAnchorForFloor(int floor)
    {
        if (floor == 0)
            return secretPlayerAnchor;

        if (floor == 1)
            return basementPlayerAnchor;

        if (floor == 2)
            return mainPlayerAnchor;

        if (floor == 3)
            return upperPlayerAnchor;

        if (floor == 5)
        {
            if (ScpElevatorProgress.ExitElevatorAnchorDisabled || ScpElevatorProgress.ScpElevatorAnchorDisabled)
                return null;

            return exitPlayerAnchor;
        }

        return null;
    }

    private void RefreshCurrentFloorFromPlayerAnchor()
    {
        Transform player = _playerController != null ? _playerController.transform : null;
        if (player == null)
        {
            PlayerController foundPlayer = FindFirstObjectByType<PlayerController>();
            player = foundPlayer != null ? foundPlayer.transform : null;
        }

        if (player == null)
            return;

        float maxDistance = Mathf.Max(0.1f, floorAnchorDetectionRadius);
        int detectedFloor = currentFloor;
        float bestDistance = maxDistance;

        CheckAnchorDistance(player.position, secretPlayerAnchor, 0, ref detectedFloor, ref bestDistance);
        CheckAnchorDistance(player.position, basementPlayerAnchor, 1, ref detectedFloor, ref bestDistance);
        CheckAnchorDistance(player.position, mainPlayerAnchor, 2, ref detectedFloor, ref bestDistance);
        CheckAnchorDistance(player.position, upperPlayerAnchor, 3, ref detectedFloor, ref bestDistance);

        currentFloor = detectedFloor;
    }

    private static void CheckAnchorDistance(
        Vector3 playerPosition,
        Transform anchor,
        int floor,
        ref int detectedFloor,
        ref float bestDistance)
    {
        if (anchor == null || !anchor.gameObject.activeInHierarchy)
            return;

        float distance = Vector3.Distance(playerPosition, anchor.position);
        if (distance > bestDistance)
            return;

        bestDistance = distance;
        detectedFloor = floor;
    }

    private IEnumerator MovePlayer(
        Transform playerTransform,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float duration)
    {
        Vector3 startPosition = playerTransform.position;
        Quaternion startRotation = playerTransform.rotation;
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            playerTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            playerTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        playerTransform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private bool CanTriggerScpElevatorMalfunction()
    {
        if (ScpElevatorProgress.SurvivedScpElevator || ScpElevatorProgress.ScpElevatorAnchorDisabled)
            return false;

        return scpElevator != null &&
               currentFloor >= 1 &&
               currentFloor <= 3 &&
               CollectionInventory.HasItem(rockyCharmsItemId);
    }

    private IEnumerator TravelToScpElevator()
    {
        if (scpElevator == null || _playerController == null)
            yield break;

        _travelling = true;
        _viewingKeypad = false;

        if (_camera != null)
        {
            _camera.transform.localPosition = _savedCameraLocalPosition;
            _camera.transform.localRotation = _savedCameraLocalRotation;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Transform playerTransform = _playerController.transform;
        float originalVolume = AudioListener.volume;
        EnsureFadeOverlay();
        ElevatorDoorController departureDoors = GetDoorControllerForFloor(currentFloor) ?? elevatorDoors;
        ElevatorDoorController arrivalDoors = scpElevator != null
            ? scpElevator.GetComponentInParent<ElevatorDoorController>()
            : null;

        if (_characterController != null)
            _characterController.enabled = false;

        Transform currentAnchor = GetAnchorForFloor(currentFloor);
        if (currentAnchor != null)
            yield return MovePlayer(playerTransform, currentAnchor.position, currentAnchor.rotation, centerPlayerDuration);

        departureDoors?.PlayElevatorMovingSound();
        yield return FadeBlackAndVolume(0f, 1f, originalVolume, 0f, scpFadeDuration);

        playerTransform.SetPositionAndRotation(scpElevator.position, scpElevator.rotation);
        yield return new WaitForSeconds(Mathf.Max(0f, scpBlackoutDuration));

        yield return FadeBlackAndVolume(1f, 0f, 0f, originalVolume, scpFadeDuration);
        departureDoors?.StopElevatorMovingSound();
        arrivalDoors?.PlayElevatorArrivalSound();

        if (_characterController != null)
            _characterController.enabled = true;

        currentFloor = 5;
        _travelling = false;

        if (_playerController != null)
            _playerController.SetInputEnabled(true);

        yield return new WaitForSeconds(Mathf.Max(0f, scpMessageDelayAfterFade));
        CollectionInventory.ShowEscMessage(scpAftermathMessage);
    }

    private IEnumerator TravelToSecretFromExitElevator()
    {
        Transform target = secretPlayerAnchor != null ? secretPlayerAnchor : secretElevator;
        if (target == null || _playerController == null)
            yield break;

        _travelling = true;
        _viewingKeypad = false;

        if (_camera != null)
        {
            _camera.transform.localPosition = _savedCameraLocalPosition;
            _camera.transform.localRotation = _savedCameraLocalRotation;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Transform playerTransform = _playerController.transform;
        float originalVolume = AudioListener.volume;
        EnsureFadeOverlay();
        ElevatorDoorController departureDoors = GetDoorControllerForFloor(5) ?? elevatorDoors;
        ElevatorDoorController arrivalDoors = GetDoorControllerForFloor(0);

        if (_characterController != null)
            _characterController.enabled = false;

        Transform currentAnchor = currentFloor == 5 ? exitPlayerAnchor : GetAnchorForFloor(currentFloor);
        if (currentAnchor != null)
            playerTransform.SetPositionAndRotation(currentAnchor.position, currentAnchor.rotation);

        DisableExitAndScpPlayerAnchorsForRestOfGame();

        departureDoors?.PlayElevatorMovingSound();
        yield return FadeBlackAndVolume(0f, 1f, originalVolume, 0f, exitReturnFadeDuration);
        yield return new WaitForSeconds(Mathf.Max(0f, exitReturnStartupDelay));

        playerTransform.SetPositionAndRotation(target.position, target.rotation);
        yield return new WaitForSeconds(Mathf.Max(0f, exitReturnBlackoutDuration));

        yield return FadeBlackAndVolume(1f, 0f, 0f, originalVolume, exitReturnFadeDuration);
        departureDoors?.StopElevatorMovingSound();
        arrivalDoors?.PlayElevatorArrivalSound();

        if (_characterController != null)
            _characterController.enabled = true;

        currentFloor = 0;
        _travelling = false;

        if (_playerController != null)
            _playerController.SetInputEnabled(true);
    }

    private void DisableExitAndScpPlayerAnchorsForRestOfGame()
    {
        ScpElevatorProgress.DisableExitElevatorAnchor();
        ScpElevatorProgress.DisableScpElevatorAnchor();

        if (exitPlayerAnchor == null)
            exitPlayerAnchor = FindSceneTransformStartingWith("Exit_Elevator_PlayerAnchor");

        if (scpPlayerAnchor == null)
            scpPlayerAnchor = FindSceneTransformStartingWith("SCP_Elevator_PlayerAnchor");

        if (exitPlayerAnchor != null)
            exitPlayerAnchor.gameObject.SetActive(false);

        if (scpPlayerAnchor != null)
            scpPlayerAnchor.gameObject.SetActive(false);
    }

    private IEnumerator FadeBlackAndVolume(
        float startAlpha,
        float targetAlpha,
        float startVolume,
        float targetVolume,
        float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (_fadeGroup != null)
                _fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            AudioListener.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (_fadeGroup != null)
        {
            _fadeGroup.alpha = targetAlpha;
            _fadeGroup.gameObject.SetActive(targetAlpha > 0.001f);
        }

        AudioListener.volume = targetVolume;
    }

    private ElevatorDoorController GetDoorControllerForFloor(int floor)
    {
        Transform anchor = GetAnchorForFloor(floor);
        ElevatorDoorController controller = anchor != null ? anchor.GetComponentInParent<ElevatorDoorController>() : null;
        if (controller != null)
            return controller;

        Transform target = GetTargetForFloor(floor);
        if (target == null && floor == 5)
            target = exitPlayerAnchor != null ? exitPlayerAnchor : scpElevator;

        if (target == null)
            return null;

        controller = target.GetComponent<ElevatorDoorController>();
        if (controller != null)
            return controller;

        controller = target.GetComponentInParent<ElevatorDoorController>();
        if (controller != null)
            return controller;

        return target.GetComponentInChildren<ElevatorDoorController>(true);
    }

    private void PlayOneShot(AudioClip clip, Vector3 position)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, audioVolume);
    }

    private void EnsureFadeOverlay()
    {
        if (_fadeGroup != null)
        {
            _fadeGroup.gameObject.SetActive(true);
            return;
        }

        GameObject canvasObject = new GameObject("SCP Elevator Fade Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject fadeObject = new GameObject("Fade", typeof(RectTransform));
        fadeObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = fadeObject.AddComponent<Image>();
        image.color = Color.black;

        _fadeGroup = fadeObject.AddComponent<CanvasGroup>();
        _fadeGroup.alpha = 0f;
        _fadeGroup.blocksRaycasts = true;
        _fadeGroup.interactable = false;
    }
}
