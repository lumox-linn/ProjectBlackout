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

        [Header("Hit Speed Boost (like Dead by Daylight)")]
        [Tooltip("Speed multiplier right after being hit.")]
        [Min(1f)] public float hitBoostMultiplier = 1.5f;
        [Tooltip("How long the boost lasts, in seconds.")]
        [Min(0f)] public float hitBoostDuration = 2f;

        /// <summary>True while the after-hit speed boost is active.</summary>
        public bool IsBoosted => boostTimeLeft > 0f;
        /// <summary>0..1 how much boost is left (1 = just got hit).</summary>
        public float BoostAmount => hitBoostDuration > 0f ? Mathf.Clamp01(boostTimeLeft / hitBoostDuration) : 0f;
        public event System.Action BoostStarted;

        private BlackoutPlayerHealth health;
        private float boostTimeLeft;

        [Header("Optional: OFF for the initial Blackout prototype")]
        public bool allowJump = false;
        [Min(0.1f)] public float jumpHeight = 1.1f;

        private CharacterController controller;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<BlackoutPlayerHealth>();
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += StartHitBoost;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= StartHitBoost;
        }

        /// <summary>Starts the after-hit sprint. Also callable from other scripts or for testing.</summary>
        public void StartHitBoost()
        {
            boostTimeLeft = hitBoostDuration;
            BoostStarted?.Invoke();
        }

        private void Update()
        {
            if (!BlackoutGameMenu.IsGameplayActive ||
                Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 input = ReadMovement();
            Vector3 horizontal = new Vector3(input.x, 0f, input.y);
            horizontal = Vector3.ClampMagnitude(horizontal, 1f);
            float speed = IsSprinting() ? sprintSpeed : walkSpeed;

            // After-hit boost: always at least sprint speed, then multiplied. Fades out over the last 30%.
            if (boostTimeLeft > 0f)
            {
                boostTimeLeft -= Time.deltaTime;
                float fade = Mathf.Clamp01(BoostAmount / 0.3f);
                float boosted = Mathf.Max(speed, sprintSpeed) * hitBoostMultiplier;
                speed = Mathf.Lerp(speed, boosted, fade);
            }

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
