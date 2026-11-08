using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    // Adapted from the lab FPSInput example: CharacterController, WASD, local-to-world motion.
    [RequireComponent(typeof(CharacterController))]
    public class BlackoutFPSInput : MonoBehaviour
    {
        [Header("Movement")]
        [Min(0.1f)] public float walkSpeed = 4.2f;
        [Min(0.1f)] public float sprintSpeed = 6.2f;
        public float gravity = -18f;

        [Header("Optional: OFF for the initial Blackout prototype")]
        public bool allowJump = false;
        [Min(0.1f)] public float jumpHeight = 1.1f;

        private CharacterController controller;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 input = ReadMovement();
            Vector3 horizontal = new Vector3(input.x, 0f, input.y);
            horizontal = Vector3.ClampMagnitude(horizontal, 1f);
            float speed = IsSprinting() ? sprintSpeed : walkSpeed;

            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            if (allowJump && controller.isGrounded && JumpPressed())
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalSpeed += gravity * Time.deltaTime;
            Vector3 worldMotion = transform.TransformDirection(horizontal * speed);
            worldMotion.y = verticalSpeed;
            controller.Move(worldMotion * Time.deltaTime);
        }

        private static Vector2 ReadMovement()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
            return Vector2.zero;
#endif
        }

        private static bool IsSprinting()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift);
#else
            return false;
#endif
        }

        private static bool JumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
