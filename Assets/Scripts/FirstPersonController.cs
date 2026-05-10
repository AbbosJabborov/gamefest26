using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 150f;
    [SerializeField] private float upperLookLimit = 80f;
    [SerializeField] private float lowerLookLimit = -80f;

    [Header("References")]
    [SerializeField] private Transform cameraRoot;

    private CharacterController controller;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private Vector3 velocity;
    private float xRotation;
    private bool isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraRoot == null)
        {
            Debug.LogError("Camera Root not assigned.");
        }

        LockCursor();
    }

    private void Update()
    {
        HandleCursor();
        GroundCheck();
        HandleLook();
        HandleMovement();
    }

    private void GroundCheck()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }
    }

    private void HandleMovement()
    {
        Vector3 move =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        controller.Move(move * moveSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        float mouseX = lookInput.x * lookSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * lookSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(
            xRotation,
            lowerLookLimit,
            upperLookLimit
        );

        cameraRoot.localRotation =
            Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleCursor()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked
        )
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (isGrounded && value.isPressed)
        {
            velocity.y =
                Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
