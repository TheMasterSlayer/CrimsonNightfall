using UnityEngine;

/// <summary>
/// Handles first-person movement: walk, sprint, crouch, and mouse look.
/// Attach to a GameObject that has:
///   - CharacterController component
///   - A child Camera named "PlayerCamera"
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("Movement")]
    [SerializeField] private float walkSpeed    = 3.5f;
    [SerializeField] private float sprintSpeed  = 6.5f;
    [SerializeField] private float crouchSpeed  = 1.5f;
    [SerializeField] private float gravity      = -15f;

    [Header("Crouch")]
    [SerializeField] private float standHeight       = 1.8f;
    [SerializeField] private float crouchHeight      = 0.9f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [SerializeField] private float standCameraHeight  = 1.6f;
    [SerializeField] private float crouchCameraHeight = 0.6f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalClamp    = 85f;   // degrees up/down

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    // ── Private State ──────────────────────────────────────────────────────

    private CharacterController _controller;
    private Vector3             _velocity;           // tracks vertical (gravity) velocity
    private float               _verticalRotation;   // camera pitch accumulator
    private float               _targetHeight;       // capsule height we're lerping toward
    private float               _targetCameraY;      // camera local Y we're lerping toward
    private bool                _isCrouching;
    private bool                _isSprinting;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        _controller    = GetComponent<CharacterController>();
        _targetHeight  = standHeight;
        _targetCameraY = standCameraHeight;

        // Lock and hide the cursor for first-person look
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Update()
    {
        HandleCrouch();
        HandleMovement();
        HandleMouseLook();
    }

    // ── Movement ───────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        // Read raw WASD / arrow key input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical   = Input.GetAxis("Vertical");

        // Build a local-space direction vector and transform it to world space
        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;

        // Only one speed mode is active at a time; crouching overrides sprinting
        float speed = _isCrouching ? crouchSpeed
                    : _isSprinting ? sprintSpeed
                    : walkSpeed;

        // Apply horizontal movement
        _controller.Move(moveDirection * speed * Time.deltaTime);

        // Gravity: reset when grounded so velocity doesn't accumulate
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;   // small negative keeps isGrounded reliable

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    // ── Crouch ─────────────────────────────────────────────────────────────

    private void HandleCrouch()
    {
        // Hold Ctrl to crouch; can't stand up if something is above you
        bool wantCrouch = Input.GetKey(KeyCode.LeftControl);

        if (wantCrouch)
        {
            _isCrouching   = true;
            _isSprinting   = false;
            _targetHeight  = crouchHeight;
            _targetCameraY = crouchCameraHeight;
        }
        else if (!IsObstructedAbove())
        {
            _isCrouching   = false;
            _isSprinting   = Input.GetKey(KeyCode.LeftShift);
            _targetHeight  = standHeight;
            _targetCameraY = standCameraHeight;
        }

        // Smoothly resize the CharacterController
        _controller.height = Mathf.Lerp(
            _controller.height,
            _targetHeight,
            crouchTransitionSpeed * Time.deltaTime
        );

        // Keep the controller centred vertically
        _controller.center = new Vector3(0f, _controller.height / 2f, 0f);

        // Smoothly move the camera up or down to match crouch state
        Vector3 camPos = playerCamera.transform.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, _targetCameraY, crouchTransitionSpeed * Time.deltaTime);
        playerCamera.transform.localPosition = camPos;
    }

    /// <summary>Returns true when a ceiling would block standing back up.</summary>
    private bool IsObstructedAbove()
    {
        // Cast a short ray upward from the top of the collider
        Vector3 topPoint = transform.position + Vector3.up * crouchHeight;
        float   checkDistance = standHeight - crouchHeight + 0.05f;
        return Physics.Raycast(topPoint, Vector3.up, checkDistance);
    }

    // ── Mouse Look ─────────────────────────────────────────────────────────

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate the whole player body left/right (yaw)
        transform.Rotate(Vector3.up * mouseX);

        // Rotate only the camera up/down (pitch), clamped to avoid over-rotation
        _verticalRotation -= mouseY;
        _verticalRotation  = Mathf.Clamp(_verticalRotation, -verticalClamp, verticalClamp);
        playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
    }

    // ── Public API (used by other systems, e.g. hiding in closets) ─────────

    /// <summary>Freeze or unfreeze all player input.</summary>
    public void SetInputEnabled(bool enabled)
    {
        this.enabled = enabled;

        if (!enabled)
        {
            // Stop any leftover horizontal motion
            _velocity.x = 0f;
            _velocity.z = 0f;
        }
    }

    public bool IsCrouching  => _isCrouching;
    public bool IsSprinting  => _isSprinting;

    // Set to true by ClosetHide.cs when the player is inside a closet
    public bool IsHiding { get; set; } = false;
}