using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// Compatibility for the original Aprial player prefab.
    /// In the shared main scene, the TEAM GameManager and Aprial UI bridge control
    /// menus, pause and cursor. This component does not compete with them.
    /// In standalone PlayerTest (no team GameManager), its old menu mode works.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class BlackoutGameMenu : MonoBehaviour
    {
        public static BlackoutGameMenu Instance { get; private set; }

        public static bool IsGameplayActive
        {
            get
            {
                GameManager teamManager = GameManager.Instance;
                if (teamManager != null)
                    return teamManager.CurrentState == GameState.Playing &&
                           !BlackoutUIBridge.SettingsOpen;

                return Instance != null && Instance.enabled && !Instance.menuOpen;
            }
        }

        [Header("Standalone PlayerTest Only")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Magnet Glove / Settings Sliders")]
        [SerializeField] private BlackoutMagnetGlove magnetGlove;

        private bool menuOpen = true;
        private bool settingsOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            // Shared main scene: nothing to do; no cursor or timescale changes.
            if (GameManager.Instance != null)
                return;

            menuOpen = true;
            settingsOpen = false;
            RefreshStandaloneMenu();
        }

        private void Update()
        {
            if (GameManager.Instance != null || !EscapePressed())
                return;

            if (!menuOpen)
                OpenMenu();
            else if (settingsOpen)
                BackToMenu();
            else
                PlayOrResume();
        }

        public void PlayOrResume()
        {
            if (BlackoutUIBridge.Instance != null)
            {
                BlackoutUIBridge.Instance.ResumePlaying();
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
                return;
            }

            menuOpen = false;
            settingsOpen = false;
            RefreshStandaloneMenu();
        }

        public void OpenMenu()
        {
            if (BlackoutUIBridge.Instance != null)
            {
                BlackoutUIBridge.Instance.ReturnToMenu();
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Menu);
                return;
            }

            menuOpen = true;
            settingsOpen = false;
            RefreshStandaloneMenu();
        }

        public void ShowSettings()
        {
            if (BlackoutUIBridge.Instance != null)
            {
                BlackoutUIBridge.Instance.OpenSettings();
                return;
            }

            if (GameManager.Instance != null)
                return;

            menuOpen = true;
            settingsOpen = true;
            RefreshStandaloneMenu();
        }

        public void BackToMenu()
        {
            if (BlackoutUIBridge.Instance != null)
            {
                BlackoutUIBridge.Instance.CloseSettings();
                return;
            }

            if (GameManager.Instance != null)
                return;

            menuOpen = true;
            settingsOpen = false;
            RefreshStandaloneMenu();
        }

        private void RefreshStandaloneMenu()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(menuOpen && !settingsOpen);
            if (settingsPanel != null)
                settingsPanel.SetActive(menuOpen && settingsOpen);

            Time.timeScale = menuOpen ? 0f : 1f;
            Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menuOpen;
        }

        private void LateUpdate()
        {
            if (GameManager.Instance != null || !menuOpen)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SetBaseThrowImpulse(float value)
        {
            if (magnetGlove != null)
                magnetGlove.throwImpulse = Mathf.Clamp(value, 1f, magnetGlove.maxThrowImpulse);
        }

        public void SetMaxThrowImpulse(float value)
        {
            if (magnetGlove != null)
                magnetGlove.maxThrowImpulse = Mathf.Max(magnetGlove.throwImpulse, value);
        }

        public void SetChargeDuration(float value)
        {
            if (magnetGlove != null)
                magnetGlove.chargeDuration = Mathf.Max(0.1f, value);
        }

        public void QuitGame()
        {
            if (BlackoutUIBridge.Instance != null)
            {
                BlackoutUIBridge.Instance.Quit();
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;
            if (GameManager.Instance == null)
                Time.timeScale = 1f;
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
    }
}
