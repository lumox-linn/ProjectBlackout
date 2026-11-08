using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    // Small flashlight toggle; the actual visual component is a Spot Light under Main Camera.
    public class BlackoutFlashlightController : MonoBehaviour
    {
        [SerializeField] private Light flashlight;
        public bool IsOn => flashlight != null && flashlight.enabled;
        public System.Action<bool> FlashlightChanged;

        public void SetLight(Light lightComponent) { flashlight = lightComponent; }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked || !TogglePressed() || flashlight == null)
                return;
            flashlight.enabled = !flashlight.enabled;
            FlashlightChanged?.Invoke(flashlight.enabled);
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F);
#else
            return false;
#endif
        }
    }
}
