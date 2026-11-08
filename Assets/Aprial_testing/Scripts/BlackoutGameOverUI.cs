
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBlackout.PlayerTest
{
    public class BlackoutGameOverUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BlackoutPlayerHealth playerHealth;
        [SerializeField] private GameObject gameOverPanel;

        [Header("Hide When Player Dies")]
        [SerializeField] private GameObject[] panelsToHide;

        [Header("Disable When Player Dies")]
        [SerializeField] private MonoBehaviour[] scriptsToDisable;

        private bool isGameOver = false;

        private void Awake()
        {
            if (playerHealth == null)
                playerHealth = GetComponent<BlackoutPlayerHealth>();

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.Died += ShowGameOver;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.Died -= ShowGameOver;
        }

        private void ShowGameOver()
        {
            if (isGameOver)
                return;

            isGameOver = true;

            // Disable player controls and pause menu.
            foreach (MonoBehaviour script in scriptsToDisable)
            {
                if (script != null && script != this)
                    script.enabled = false;
            }

            // Hide other UI panels.
            foreach (GameObject panel in panelsToHide)
            {
                if (panel != null)
                    panel.SetActive(false);
            }

            // Display existing Game Over UI.
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            // Stop game physics and gameplay time.
            Time.timeScale = 0f;

            // Show the mouse so UI buttons are clickable.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("GAME OVER - Player lost all hearts!");
        }

        private void LateUpdate()
        {
            if (!isGameOver)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Connect this to your Restart button.
        public void RestartGame()
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(
                SceneManager.GetActiveScene().name
            );
        }
    }
}
