
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private GameState currentState = GameState.Menu;

    public GameState CurrentState => currentState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        SetGameState(GameState.Menu);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (currentState == GameState.Playing)
                PauseGame();
            else if (currentState == GameState.Paused)
                ResumeGame();
        }
    }

    public void SetGameState(GameState newState)
    {
        currentState = newState;

        Time.timeScale =
            newState == GameState.Playing ? 1f : 0f;

        GameEvents.RaiseGameStateChanged(newState);

        Debug.Log("Game State: " + newState);
    }

    public void StartGame()
    {
        SetGameState(GameState.Playing);
    }

    public void PauseGame()
    {
        if (currentState == GameState.Playing)
            SetGameState(GameState.Paused);
    }

    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
            SetGameState(GameState.Playing);
    }

    public void WinGame()
    {
        if (currentState == GameState.Playing)
            SetGameState(GameState.Won);
    }

    public void LoseGame()
    {
        if (currentState == GameState.Playing)
            SetGameState(GameState.Lost);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
