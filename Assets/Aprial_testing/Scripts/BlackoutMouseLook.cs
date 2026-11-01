using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    // Inspired by the lab MouseLook: player turns horizontally, camera turns vertically.
    public class BlackoutMouseLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [Range(0.02f, 1f)] public float sensitivity = 0.12f;
        [Range(30f, 89f)] public float verticalLimit = 80f;
        private float pitch;

        public void SetCameraPivot(Transform pivot) { cameraPivot = pivot; }

        private void Start()
        {
            if (cameraPivot == null)
            {
                Debug.LogError("BlackoutMouseLook needs its cameraPivot assigned.", this);
                enabled = false;
                return;
            }
            pitch = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (EscapePressed())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (PrimaryClick())
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }

            Vector2 mouse = ReadMouseDelta();
            transform.Rotate(Vector3.up, mouse.x * sensitivity, Space.Self);
            pitch = Mathf.Clamp(pitch - mouse.y * sensitivity, -verticalLimit, verticalLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            // The old Input Manager mouse axis has different units from Input System pixels.
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 18f;
#else
            return Vector2.zero;
#endif
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private static bool PrimaryClick()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
