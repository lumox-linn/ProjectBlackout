
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    public class BlackoutMouseLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;

        [Range(0.02f, 1f)]
        public float sensitivity = 0.12f;

        [Range(30f, 89f)]
        public float verticalLimit = 80f;

        private float pitch;

        public void SetCameraPivot(Transform pivot)
        {
            cameraPivot = pivot;
        }

        private void Start()
        {
            if (cameraPivot == null)
            {
                Debug.LogError(
                    "Camera Pivot is not assigned!", this
                );
                enabled = false;
                return;
            }

            pitch = Mathf.DeltaAngle(
                0f, cameraPivot.localEulerAngles.x
            );

            // Do not lock the cursor here.
            // The menu controller handles cursor locking.
        }

        private void Update()
        {
            if (GameEvents.CurrentState != GameState.Playing)
                return;

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 mouse = ReadMouseDelta();

            transform.Rotate(
                Vector3.up,
                mouse.x * sensitivity,
                Space.Self
            );

            pitch = Mathf.Clamp(
                pitch - mouse.y * sensitivity,
                -verticalLimit,
                verticalLimit
            );

            cameraPivot.localRotation =
                Quaternion.Euler(pitch, 0f, 0f);
        }

        private static Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null
                ? Mouse.current.delta.ReadValue()
                : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y")
            ) * 18f;
#else
            return Vector2.zero;
#endif
        }
    }
}
