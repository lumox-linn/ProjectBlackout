
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
public class UIButtonSFX : MonoBehaviour
{
    [Header("Button Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickSound;

    private IEnumerator Start()
    {
        // wait so UIBridge has finished rewiring the buttons
        yield return null;
        yield return null;

        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            button.onClick.RemoveListener(PlayClickSound);   // no double clicks
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
