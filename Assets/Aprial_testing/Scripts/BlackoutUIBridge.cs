using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    [DefaultExecutionOrder(500)]
    public sealed class BlackoutUIBridge : MonoBehaviour
    {
        public static BlackoutUIBridge Instance { get; private set; }

        public static bool SettingsOpen =>
            Instance != null && Instance.settingsOpen;

        private static bool autoPlayAfterReload;

        // Drag these in the Inspector (or use Tools > Project Blackout > Setup Scene References).
        [Header("Team managers")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameUIManager teamUIManager;

        [Header("UI panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;
        [Tooltip("Optional")]
        [SerializeField] private GameObject howToPlayPanel;
        private bool showingInstructions;

        private bool settingsOpen;
        private bool initialized;
        private bool syncedState;
        private bool pendingEnableGameManager;
        private bool wasTeamUIManagerEnabled;

        // Reset when Play is pressed (static values survive between plays in the Editor).
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            autoPlayAfterReload = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += OnTeamStateChanged;
        }

        private void Start()
        {
            if (gameManager == null || teamUIManager == null)
            {
                Debug.LogError(
                    "BlackoutUIBridge: GameManager or GameUIManager not assigned in the Inspector.",
                    this
                );

                enabled = false;
                return;
            }

            // Prevent both UI scripts processing Escape.
            wasTeamUIManagerEnabled = teamUIManager.enabled;
            teamUIManager.enabled = false;

            if (mainMenuPanel == null ||
                hudPanel == null ||
                pausePanel == null ||
                settingsPanel == null ||
                winPanel == null ||
                losePanel == null)
            {
                Debug.LogError(
                    "BlackoutUIBridge: One or more UI panels are not assigned in the Inspector.",
                    this
                );

                teamUIManager.enabled = wasTeamUIManagerEnabled;
                enabled = false;
                return;
            }

            WirePanelButtons(mainMenuPanel);
            WirePanelButtons(pausePanel);
            WirePanelButtons(settingsPanel);
            WirePanelButtons(winPanel);
            WirePanelButtons(losePanel);
            if (howToPlayPanel != null)
                WirePanelButtons(howToPlayPanel);

            initialized = true;
            settingsOpen = false;

            // Restart button: load a fresh level and enter gameplay.
            // Main Menu button: load a fresh level but show the menu.
            bool startGameNow = autoPlayAfterReload;
            autoPlayAfterReload = false;

            gameManager.SetGameState(GameState.Menu);
            RefreshPanels();

            if (startGameNow)
            {
                BeginPlaying();
            }
        }

        private void WirePanelButtons(GameObject panel)
        {
            foreach (Button button in
                     panel.GetComponentsInChildren<Button>(true))
            {
                string name = button.gameObject.name
                    .Trim()
                    .ToLowerInvariant();

                System.Action action = null;

                switch (name)
                {
                    case "howtoplaybutton":
                        action = OpenHowToPlay;
                        break;
                    case "continuebutton":
                    case "continue":
                        action = CloseHowToPlay;
                        break;
                    case "playbutton":
                    case "play":
                    case "startbutton":
                        action = BeginPlaying;
                        break;

                    case "resume":
                    case "resumebutton":
                        action = ResumePlaying;
                        break;

                    case "settings":
                    case "settingsbutton":
                        action = OpenSettings;
                        break;

                    case "back":
                    case "backbutton":
                        action = CloseSettings;
                        break;

                    case "mainmenu":
                    case "mainmenubutton":
                        action = ReturnToMenu;
                        break;

                    case "restart":
                    case "restartbutton":
                        action = Restart;
                        break;

                    case "quit":
                    case "quitbutton":
                        action = Quit;
                        break;
                }

                if (action == null)
                {
                    continue;
                }

                // Replace old callbacks at runtime only.
                // The original prefab is not modified.
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => action());
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            // Keep existing F9 win / F10 lose testing.
            if (Keyboard.current != null &&
                gameManager.CurrentState == GameState.Playing)
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

            if (EscapePressed())
            {
                if (settingsOpen)
                    CloseSettings();
                else if (gameManager.CurrentState == GameState.Playing)
                    gameManager.PauseGame();
                else if (gameManager.CurrentState == GameState.Paused)
                    gameManager.ResumeGame();
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            if (pendingEnableGameManager)
            {
                pendingEnableGameManager = false;

                if (gameManager != null)
                {
                    gameManager.enabled = true;
                }
            }

            ApplyCursor();
        }

        private void OnTeamStateChanged(GameState state)
        {
            if (gameManager == null)
            {
                return;
            }

            // Synchronize external GameEvents with GameManager.
            if (!syncedState && gameManager.CurrentState != state)
            {
                syncedState = true;
                gameManager.SetGameState(state);
                syncedState = false;
            }

            if (initialized)
            {
                settingsOpen = false;

                if (!gameManager.enabled)
                {
                    pendingEnableGameManager = true;
                }

                RefreshPanels();
            }
        }

        private void RefreshPanels()
        {
            GameState state = gameManager.CurrentState;

            mainMenuPanel.SetActive(
                !settingsOpen && state == GameState.Menu
            );

            hudPanel.SetActive(
                state == GameState.Playing ||
                state == GameState.Paused
            );

            pausePanel.SetActive(
                !settingsOpen && !showingInstructions &&
                state == GameState.Paused
            );
            if (howToPlayPanel != null)
            {
                howToPlayPanel.SetActive(showingInstructions);

                if (showingInstructions)
                    howToPlayPanel.transform.SetAsLastSibling();
            }

            settingsPanel.SetActive(settingsOpen);

            winPanel.SetActive(
                !settingsOpen && state == GameState.Won
            );

            losePanel.SetActive(
                !settingsOpen && state == GameState.Lost
            );

            Time.timeScale =
                state == GameState.Playing && !settingsOpen
                ? 1f
                : 0f;

            ApplyCursor();
        }

        private void ApplyCursor()
        {
            bool playing =
                gameManager != null &&
                gameManager.CurrentState == GameState.Playing &&
                !settingsOpen;

            Cursor.lockState = playing
                ? CursorLockMode.Locked
                : CursorLockMode.None;

            Cursor.visible = !playing;
        }

        public void BeginPlaying()
        {
            if (gameManager == null)
            {
                return;
            }

            // Never resume a dead player or completed game.
            if (gameManager.CurrentState == GameState.Lost ||
                gameManager.CurrentState == GameState.Won)
            {
                Restart();
                return;
            }

            bool startingRun = gameManager.CurrentState == GameState.Menu;

            settingsOpen = false;
            showingInstructions = startingRun && howToPlayPanel != null;

            gameManager.SetGameState(
                showingInstructions ? GameState.Paused : GameState.Playing
            );
        }
        public void OpenHowToPlay()
        {
            if (howToPlayPanel == null)
                return;

            settingsOpen = false;
            showingInstructions = true;
            RefreshPanels();
        }
        public void CloseHowToPlay()
        {
            if (!showingInstructions)
                return;

            showingInstructions = false;

            if (gameManager.CurrentState == GameState.Menu)
                RefreshPanels();
            else
                gameManager.ResumeGame();
        }
        public void ResumePlaying()
        {
            if (gameManager.CurrentState == GameState.Paused)
            {
                gameManager.ResumeGame();
            }
            else
            {
                BeginPlaying();
            }
        }

        public void OpenSettings()
        {
            if (gameManager.CurrentState == GameState.Playing)
            {
                gameManager.PauseGame();
            }

            if (gameManager.CurrentState != GameState.Menu &&
                gameManager.CurrentState != GameState.Paused)
            {
                return;
            }

            settingsOpen = true;
            gameManager.enabled = false;

            RefreshPanels();
        }

        public void CloseSettings()
        {
            if (!settingsOpen)
            {
                return;
            }

            settingsOpen = false;
            pendingEnableGameManager = true;

            RefreshPanels();
        }

        // MAIN MENU: reload the scene, then display the home screen.
        public void ReturnToMenu()
        {
            ReloadLevel(false);
        }

        // RESTART: reload the scene, then start gameplay automatically.
        public void Restart()
        {
            ReloadLevel(true);
        }

        private static void ReloadLevel(bool autoPlay)
        {
            autoPlayAfterReload = autoPlay;

            // Reset timeScale so reloading works from death/pause.
            Time.timeScale = 1f;

            SceneManager.LoadScene(
                SceneManager.GetActiveScene().name
            );
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= OnTeamStateChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Instance = null;

            if (teamUIManager != null)
            {
                teamUIManager.enabled = wasTeamUIManagerEnabled;
            }

            if (gameManager != null)
            {
                gameManager.enabled = true;
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