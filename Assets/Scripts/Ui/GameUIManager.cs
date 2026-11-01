
using UnityEngine;

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

    private void ShowGameState(GameState state)
    {
        currentState = state;

        mainMenuPanel.SetActive(state == GameState.Menu);
        hudPanel.SetActive(state == GameState.Playing ||
                           state == GameState.Paused);
        pausePanel.SetActive(state == GameState.Paused);
        winPanel.SetActive(state == GameState.Won);
        losePanel.SetActive(state == GameState.Lost);

        settingsPanel.SetActive(false);
    }

    public void PlayGame()
    {
        GameEvents.RaiseGameStateChanged(GameState.Playing);
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
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
