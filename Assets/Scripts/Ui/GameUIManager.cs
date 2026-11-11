using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject howToPlayPanel;

    private GameState currentState = GameState.Menu;
    private bool showingInstructions;

    private void OnEnable()
    {
        GameEvents.GameStateChanged += ShowGameState;
    }

    private void OnDisable()
    {
        GameEvents.GameStateChanged -= ShowGameState;
    }

    private void Start()
    {
        ShowGameState(GameManager.Instance.CurrentState);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (showingInstructions)
                CloseHowToPlay();
            else if (settingsPanel.activeSelf)
                CloseSettings();
            else if (currentState == GameState.Playing)
                PauseGame();
            else if (currentState == GameState.Paused)
                ResumeGame();
        }

        if (Keyboard.current.f9Key.wasPressedThisFrame)
            GameManager.Instance.WinGame();

        if (Keyboard.current.f10Key.wasPressedThisFrame)
            GameManager.Instance.LoseGame();
    }

    private void ShowGameState(GameState state)
    {
        currentState = state;

        if (state != GameState.Paused)
            showingInstructions = false;

        mainMenuPanel.SetActive(state == GameState.Menu);
        hudPanel.SetActive(
            state == GameState.Playing || state == GameState.Paused
        );

        pausePanel.SetActive(
            state == GameState.Paused && !showingInstructions
        );

        winPanel.SetActive(state == GameState.Won);
        losePanel.SetActive(state == GameState.Lost);
        settingsPanel.SetActive(false);

        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(showingInstructions);

            if (showingInstructions)
                howToPlayPanel.transform.SetAsLastSibling();
        }

        bool playing = state == GameState.Playing;
        Cursor.lockState =
            playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;
    }

    public void PlayGame()
    {
        Debug.Log(
            $"[UI Start] Object={name}, " +
            $"Enabled={enabled}, Active={gameObject.activeInHierarchy}, " +
            $"InstructionsAssigned={howToPlayPanel != null}"
        );

        GameManager.Instance.StartGame();

        Debug.Log(
            $"[UI Start] UI state={currentState}, " +
            $"Manager state={GameManager.Instance.CurrentState}"
        );

        OpenHowToPlay();

        Debug.Log(
            $"[UI Start] Instructions={showingInstructions}, " +
            $"PanelVisible={howToPlayPanel != null && howToPlayPanel.activeInHierarchy}"
        );
    }

    public void OpenHowToPlay()
    {
        if (currentState != GameState.Playing || howToPlayPanel == null)
            return;

        showingInstructions = true;
        GameManager.Instance.PauseGame();
    }

    public void CloseHowToPlay()
    {
        if (!showingInstructions)
            return;

        showingInstructions = false;
        GameManager.Instance.ResumeGame();
    }

    public void PauseGame()
    {
        GameManager.Instance.PauseGame();
    }

    public void ResumeGame()
    {
        showingInstructions = false;
        GameManager.Instance.ResumeGame();
    }

    public void RestartGame()
    {
        GameManager.Instance.RestartGame();
    }

    public void ReturnToMainMenu()
    {
        showingInstructions = false;
        GameManager.Instance.SetGameState(GameState.Menu);
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        ShowGameState(currentState);
    }

    public void QuitGame()
    {
        GameManager.Instance.QuitGame();
    }
}