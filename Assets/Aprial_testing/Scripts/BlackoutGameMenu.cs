
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    [DefaultExecutionOrder(-200)]
    public class BlackoutGameMenu : MonoBehaviour
    {
        public static BlackoutGameMenu Instance
        {
            get;
            private set;
        }

        public static bool IsGameplayActive =>
            Instance != null &&
            Instance.enabled &&
            !Instance.menuOpen;

        [Header("Menu Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Magnet Glove")]
        [SerializeField] private BlackoutMagnetGlove magnetGlove;

        private bool menuOpen = true;
        private bool settingsOpen = false;
        private float resumeTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            menuOpen = true;
            settingsOpen = false;

            ApplyMenuState();
        }

        private void Update()
        {
            if (!EscapePressed())
                return;

            if (!menuOpen)
            {
                OpenMenu();
            }
            else if (settingsOpen)
            {
                BackToMenu();
            }
            else
            {
                PlayOrResume();
            }
        }

        public void PlayOrResume()
        {
            menuOpen = false;
            settingsOpen = false;
            ApplyMenuState();
        }

        public void OpenMenu()
        {
            if (!menuOpen)
            {
                resumeTimeScale = Mathf.Max(
                    0.0001f, Time.timeScale
                );
            }

            menuOpen = true;
            settingsOpen = false;
            ApplyMenuState();
        }

        public void ShowSettings()
        {
            menuOpen = true;
            settingsOpen = true;
            ApplyMenuState();
        }

        public void BackToMenu()
        {
            menuOpen = true;
            settingsOpen = false;
            ApplyMenuState();
        }

        private void ApplyMenuState()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(
                    menuOpen && !settingsOpen
                );

            if (settingsPanel != null)
                settingsPanel.SetActive(
                    menuOpen && settingsOpen
                );

            if (menuOpen)
            {
                Time.timeScale = 0f;

                Cursor.lockState =
                    CursorLockMode.None;

                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = resumeTimeScale;

                Cursor.lockState =
                    CursorLockMode.Locked;

                Cursor.visible = false;
            }
        }

        private void LateUpdate()
        {
            if (!menuOpen)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SetBaseThrowImpulse(float value)
        {
            if (magnetGlove != null)
                magnetGlove.throwImpulse =
                    Mathf.Clamp(
                        value,
                        1f,
                        magnetGlove.maxThrowImpulse
                    );
        }

        public void SetMaxThrowImpulse(float value)
        {
            if (magnetGlove != null)
                magnetGlove.maxThrowImpulse =
                    Mathf.Max(
                        magnetGlove.throwImpulse,
                        value
                    );
        }

        public void SetChargeDuration(float value)
        {
            if (magnetGlove != null)
                magnetGlove.chargeDuration =
                    Mathf.Max(0.1f, value);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
