
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

    private GameState currentState = GameState.Menu;

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
        ShowGameState(GameState.Menu);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }



    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // ESC - Pause / Resume
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else if (currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        // TEMPORARY WIN TEST
        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            Debug.Log("F9 detected - WIN");
            GameEvents.RaiseGameStateChanged(GameState.Won);
        }

        // TEMPORARY LOSE TEST
        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            Debug.Log("F10 detected - LOSE");
            GameEvents.RaiseGameStateChanged(GameState.Lost);
        }
    }


    private void ShowGameState(GameState state)
    {
        currentState = state;

        Time.timeScale = state == GameState.Paused ? 0f : 1f;

        mainMenuPanel.SetActive(state == GameState.Menu);
        hudPanel.SetActive(state == GameState.Playing ||
                           state == GameState.Paused);
        pausePanel.SetActive(state == GameState.Paused);
        winPanel.SetActive(state == GameState.Won);
        losePanel.SetActive(state == GameState.Lost);

        settingsPanel.SetActive(false);

        bool playing = state == GameState.Playing;
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;
    }

    public void PlayGame()
    {
        GameEvents.RaiseGameStateChanged(GameState.Playing);
    }

    public void PauseGame()
    {
        if (currentState == GameState.Playing)
        {
            GameEvents.RaiseGameStateChanged(GameState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            GameEvents.RaiseGameStateChanged(GameState.Playing);
        }
    }

    public void ReturnToMainMenu()
    {
        GameEvents.RaiseGameStateChanged(GameState.Menu);
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        ShowGameState(currentState);
    }

    public void QuitGame()
    {
        Debug.Log("Quit button clicked!");
        Application.Quit();
    }
}
