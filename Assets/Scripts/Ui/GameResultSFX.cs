
using UnityEngine;

public class GameResultSFX : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip loseSound;

    private void OnEnable()
    {
        GameEvents.GameStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameStateChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (audioSource == null)
            return;

        if (state == GameState.Won && winSound != null)
        {
            audioSource.PlayOneShot(winSound);
        }
        else if (state == GameState.Lost && loseSound != null)
        {
            audioSource.PlayOneShot(loseSound);
        }
    }
}
