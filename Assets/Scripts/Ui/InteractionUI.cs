using UnityEngine;
using TMPro;

public class InteractionUI : MonoBehaviour
{
    [Header("Interaction Prompt")]
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private TMP_Text interactionText;

    private void OnEnable()
    {
        HideInteraction();
        GameEvents.InteractPromptChanged += OnPromptChanged;
    }

    private void OnDisable()
    {
        GameEvents.InteractPromptChanged -= OnPromptChanged;
        HideInteraction();
    }

    private void OnPromptChanged(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            HideInteraction();
        else
            ShowInteraction(action);
    }

    public void ShowInteraction(string action = "INTERACT")
    {
        interactionText.text = $"[E] {action}";
        interactionHint.SetActive(true);
    }

    public void HideInteraction()
    {
        interactionHint.SetActive(false);
    }
}