using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// Aprial-owned adapter for the existing GameManager/GameUIManager/Canvas.
    /// Does not change or overwrite teammates' scripts or prefabs.
    /// Runs ONLY in a scene containing the Aprial player and both team managers.
    ///
    /// GameManager stays authoritative for state and Esc. GameUIManager's Update
    /// is disabled at runtime to prevent it from handling the SAME Esc press.
    /// Existing menu graphics and settings sliders remain intact.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class BlackoutUIBridge : MonoBehaviour
    {
        public static BlackoutUIBridge Instance { get; private set; }

        public static bool SettingsOpen => Instance != null && Instance.settingsOpen;

        private GameManager gameManager;
        private GameUIManager teamUIManager;
        private GameObject mainMenuPanel;
        private GameObject hudPanel;
        private GameObject pausePanel;
        private GameObject settingsPanel;
        private GameObject winPanel;
        private GameObject losePanel;

        private bool settingsOpen;
        private bool pendingEnableGameManager;
        private bool wasTeamUIManagerEnabled;
        private bool syncedState;
        private bool initialized;

        // Attach automatically, so no scene/prefab from another member needs editing.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInAprialScene()
        {
            if (FindFirstObjectByType<BlackoutFPSInput>() == null ||
                FindFirstObjectByType<GameManager>() == null ||
                FindFirstObjectByType<GameUIManager>() == null ||
                FindFirstObjectByType<BlackoutUIBridge>() != null)
                return;

            GameObject bridge = new GameObject("Aprial_UIBridge_Runtime");
            bridge.AddComponent<BlackoutUIBridge>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameManager = FindFirstObjectByType<GameManager>();
            teamUIManager = FindFirstObjectByType<GameUIManager>();
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += OnTeamStateChanged;
        }

        private void Start()
        {
            if (gameManager == null || teamUIManager == null)
            {
                Debug.LogWarning("BlackoutUIBridge: missing team GameManager / GameUIManager.", this);
                enabled = false;
                return;
            }

            // The team's GameUIManager also reads Escape and changes Time.timeScale.
            // Disable THAT COMPONENT AT RUNTIME only; do not edit its source/prefab.
            wasTeamUIManagerEnabled = teamUIManager.enabled;
            teamUIManager.enabled = false;

            Transform uiRoot = teamUIManager.transform.parent != null
                ? teamUIManager.transform.parent
                : teamUIManager.transform;

            mainMenuPanel = FindChild(uiRoot, "MainMenuPanel");
            hudPanel = FindChild(uiRoot, "HUDPanel");
            pausePanel = FindChild(uiRoot, "PausePanel");
            settingsPanel = FindChild(uiRoot, "SettingsPanel");
            winPanel = FindChild(uiRoot, "WinPanel");
            losePanel = FindChild(uiRoot, "LosePanel");

            if (mainMenuPanel == null || hudPanel == null ||
                pausePanel == null || settingsPanel == null ||
                winPanel == null || losePanel == null)
            {
                Debug.LogError(
                    "BlackoutUIBridge: one or more UI panels were not found under GameUISystem. " +
                    "Verify Canvas contains MainMenuPanel, HUDPanel, PausePanel, " +
                    "SettingsPanel, WinPanel and LosePanel.", this);
                teamUIManager.enabled = wasTeamUIManagerEnabled;
                enabled = false;
                return;
            }

            WireExistingButtons();
            initialized = true;
            settingsOpen = false;

            // GameManager.Start() has already run (this class executes later).
            gameManager.SetGameState(GameState.Menu);
            RefreshPanels();
        }

        private static GameObject FindChild(Transform parent, string targetName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == targetName)
                    return child.gameObject;
            }
            return null;
        }

        private void WireExistingButtons()
        {
            // Overwrite ONLY runtime onClick events for navigation buttons.
            // Serialized events on teammates' prefab assets are never saved/modified.
            WirePanelButtons(mainMenuPanel);
            WirePanelButtons(pausePanel);
            WirePanelButtons(settingsPanel);
            WirePanelButtons(winPanel);
            WirePanelButtons(losePanel);
        }

        private void WirePanelButtons(GameObject panel)
        {
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                string name = button.gameObject.name.Trim().ToLowerInvariant();
                System.Action action = null;

                if (name == "playbutton" || name == "play" || name == "startbutton")
                    action = BeginPlaying;
                else if (name == "resume" || name == "resumebutton")
                    action = ResumePlaying;
                else if (name == "settings" || name == "settingsbutton")
                    action = OpenSettings;
                else if (name == "backbutton" || name == "back")
                    action = CloseSettings;
                else if (name == "mainmenu" || name == "mainmenubutton")
                    action = ReturnToMenu;
                else if (name == "restartbutton" || name == "restart")
                    action = Restart;
                else if (name == "quitbutton" || name == "quit")
                    action = Quit;

                if (action == null)
                    continue;

                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => action());
            }
        }

        // Existing GameManager.Update() handles Escape in Playing/Paused.
        // While Settings is open, we disable its Update until Settings closes,
        // so Escape closes Settings instead of accidentally resuming gameplay.
        private void Update()
        {
            if (!initialized)
                return;

#if ENABLE_INPUT_SYSTEM
            // Preserve the team's temporary win/lose keyboard tests.
            if (Keyboard.current != null && gameManager.CurrentState == GameState.Playing)
            {
                if (Keyboard.current.f9Key.wasPressedThisFrame)
                {
                    gameManager.WinGame();
                    return;
                }
                if (Keyboard.current.f10Key.wasPressedThisFrame)
                {
                    gameManager.LoseGame();
                    return;
                }
            }
#endif

            if (settingsOpen && EscapePressed())
                CloseSettings();
        }

        private void LateUpdate()
        {
            if (!initialized)
                return;

            // Re-enable only AFTER all Update calls for this frame have completed.
            if (pendingEnableGameManager)
            {
                pendingEnableGameManager = false;
                if (gameManager != null)
                    gameManager.enabled = true;
            }

            ApplyCursor();
        }

        private void OnTeamStateChanged(GameState state)
        {
            if (gameManager == null)
                return;

            // Some existing UI methods raise GameEvents directly instead of
            // GameManager.SetGameState(). Keep the authoritative manager in sync.
            if (!syncedState && gameManager.CurrentState != state)
            {
                syncedState = true;
                gameManager.SetGameState(state);
                syncedState = false;
            }

            if (initialized)
            {
                settingsOpen = false;
                if (gameManager != null && !gameManager.enabled)
                    pendingEnableGameManager = true;
                RefreshPanels();
            }
        }

        private void RefreshPanels()
        {
            if (gameManager == null)
                return;

            GameState state = gameManager.CurrentState;

            mainMenuPanel.SetActive(!settingsOpen && state == GameState.Menu);
            pausePanel.SetActive(!settingsOpen && state == GameState.Paused);
            settingsPanel.SetActive(settingsOpen);
            hudPanel.SetActive(state == GameState.Playing || state == GameState.Paused);
            winPanel.SetActive(!settingsOpen && state == GameState.Won);
            losePanel.SetActive(!settingsOpen && state == GameState.Lost);

            Time.timeScale = state == GameState.Playing && !settingsOpen ? 1f : 0f;
            ApplyCursor();
        }

        private void ApplyCursor()
        {
            bool playing = gameManager != null &&
                           gameManager.CurrentState == GameState.Playing &&
                           !settingsOpen;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        public void BeginPlaying()
        {
            settingsOpen = false;
            gameManager.SetGameState(GameState.Playing);
        }

        public void ResumePlaying()
        {
            if (gameManager.CurrentState == GameState.Paused)
                gameManager.ResumeGame();
            else
                BeginPlaying();
        }

        public void OpenSettings()
        {
            if (gameManager.CurrentState == GameState.Playing)
                gameManager.PauseGame();

            if (gameManager.CurrentState != GameState.Menu &&
                gameManager.CurrentState != GameState.Paused)
                return;

            settingsOpen = true;
            gameManager.enabled = false; // prevent Escape double handling in Settings
            RefreshPanels();
        }

        public void CloseSettings()
        {
            if (!settingsOpen)
                return;

            settingsOpen = false;
            pendingEnableGameManager = true;
            RefreshPanels();
        }

        public void ReturnToMenu()
        {
            settingsOpen = false;
            gameManager.enabled = true;
            gameManager.SetGameState(GameState.Menu);
            RefreshPanels();
        }

        public void Restart()
        {
            settingsOpen = false;
            gameManager.enabled = true;
            gameManager.RestartGame();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            gameManager.QuitGame();
#endif
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= OnTeamStateChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;
            if (teamUIManager != null)
                teamUIManager.enabled = wasTeamUIManagerEnabled;
            if (gameManager != null)
                gameManager.enabled = true;
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
