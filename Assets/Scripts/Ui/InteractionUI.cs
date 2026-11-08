
using UnityEngine;
using TMPro;

public class InteractionUI : MonoBehaviour
{
    [Header("Interaction Prompt")]
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private TMP_Text interactionText;

    private void Start()
    {
        HideInteraction();
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
