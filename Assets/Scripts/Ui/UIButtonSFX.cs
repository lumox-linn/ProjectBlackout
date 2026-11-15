using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIButtonSFX : MonoBehaviour
{
    [Header("Button Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickSound;

    [Header("Buttons")]
    [SerializeField] private Button[] buttons;

    private IEnumerator Start()
    {
        // Wait so UIBridge has finished rewiring the buttons.
        yield return null;
        yield return null;

        foreach (Button button in buttons)
        {
            if (button == null) continue;

            button.onClick.RemoveListener(PlayClickSound);
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