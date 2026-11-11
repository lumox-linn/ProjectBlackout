using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Goes on the player's camera. Looks for an IInteractable straight ahead (generator, etc.),
/// tells the HUD what to show through GameEvents.InteractPromptChanged (Eliza's InteractionUI
/// adds the "[E]"), and calls Interact() when E is pressed.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("How far the player can reach, in metres.")]
    [SerializeField] private float reach = 3f;

    [Tooltip("Which layers the look-ray can hit.")]
    [SerializeField] private LayerMask mask = ~0;

    private IInteractable current;
    private string shownPrompt;

    private void Update()
    {
        // 1) What are we looking at? (only while actually playing)
        IInteractable target = null;
        if (GameEvents.CurrentState == GameState.Playing)
        {
            var ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<IInteractable>();
                if (target != null && !target.CanInteract)
                    target = null;   // e.g. generator already on: no prompt
            }
        }
        current = target;

        // 2) Tell the HUD (null = hide the prompt).
        SetPrompt(current != null ? current.Prompt : null);

        // 3) Press E to use it.
        if (current != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            current.Interact();
            SetPrompt(null);
        }
    }

    // Only announce when the text changes, not every frame.
    private void SetPrompt(string text)
    {
        if (text == shownPrompt) return;
        shownPrompt = text;
        GameEvents.RaiseInteractPromptChanged(text);
    }

    private void OnDisable() { SetPrompt(null); }
}