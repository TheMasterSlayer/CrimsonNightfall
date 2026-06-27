using UnityEngine;

[DisallowMultipleComponent]
public class SCP096DebugCameraToggle : MonoBehaviour
{
    [Header("Toggle")]
    [SerializeField] private KeyCode toggleKey = KeyCode.P;
    [SerializeField] private bool enableDebugToggle = true;

    [Header("Cameras")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera scp096Camera;
    [SerializeField] private string scp096ObjectName = "SCP-096";
    [SerializeField] private string scp096CameraChildName = "Camera";

    private bool _usingScpCamera;
    private bool _playerCameraWasEnabled;
    private bool _scpCameraWasEnabled;

    private void Awake()
    {
        AutoFindCameras();
    }

    private void Update()
    {
        if (!enableDebugToggle || !Input.GetKeyDown(toggleKey))
            return;

        AutoFindCameras();

        if (playerCamera == null || scp096Camera == null)
        {
            Debug.LogWarning("[SCP096DebugCameraToggle] Missing player camera or SCP-096 camera.", this);
            return;
        }

        if (_usingScpCamera)
            RestorePreviousView();
        else
            SwitchToScpView();
    }

    private void SwitchToScpView()
    {
        _playerCameraWasEnabled = playerCamera.enabled;
        _scpCameraWasEnabled = scp096Camera.enabled;

        playerCamera.enabled = false;
        scp096Camera.enabled = true;
        _usingScpCamera = true;
    }

    private void RestorePreviousView()
    {
        playerCamera.enabled = _playerCameraWasEnabled;
        scp096Camera.enabled = _scpCameraWasEnabled;
        _usingScpCamera = false;
    }

    private void AutoFindCameras()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (scp096Camera != null)
            return;

        GameObject scp = GameObject.Find(scp096ObjectName);
        if (scp == null)
            return;

        Transform cameraChild = FindChildByName(scp.transform, scp096CameraChildName);
        if (cameraChild != null)
            scp096Camera = cameraChild.GetComponent<Camera>();
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
}
