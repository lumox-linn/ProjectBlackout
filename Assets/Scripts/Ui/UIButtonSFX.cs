
using UnityEngine;
using UnityEngine.UI;

public class UIButtonSFX : MonoBehaviour
{
    [Header("Button Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickSound;

    private void Start()
    {
        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Button button in buttons)
        {
            button.onClick.AddListener(PlayClickSound);
        }
    }

    private void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }
}
