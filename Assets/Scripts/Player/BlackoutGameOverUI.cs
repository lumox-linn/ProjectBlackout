using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// Owns ONLY the Aprial health -> team Lost-state connection.
    /// The team's existing UI/Aprial bridge shows LosePanel.
    /// Never deactivates Canvas or independently locks the cursor.
    /// </summary>
    public class BlackoutGameOverUI : MonoBehaviour
    {
        [Header("Aprial Health")]
        [SerializeField] private BlackoutPlayerHealth playerHealth;

        // Kept for compatibility with already-serialized Inspector fields.
        [Header("Legacy fields - not used by shared main scene")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject[] panelsToHide;
        [SerializeField] private MonoBehaviour[] scriptsToDisable;

        private bool gameOverTriggered;

        private void Awake()
        {
            if (playerHealth == null)
                playerHealth = GetComponent<BlackoutPlayerHealth>();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.Died -= OnPlayerDied;
        }

        private void OnPlayerDied()
        {
            if (gameOverTriggered)
                return;

            gameOverTriggered = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoseGame();
            }
            else
            {
                // Standalone PlayerTest fallback; no team game manager exists.
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (gameOverPanel != null)
                    gameOverPanel.SetActive(true);
            }
        }

        // Older Restart buttons can still call this method.
        public void RestartGame()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
