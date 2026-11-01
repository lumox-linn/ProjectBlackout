/// <summary>
/// Anything the player can use with the E key: the generator, doors, switches.
/// Eliza's PlayerInteractor (C10) looks for this. Linn's Generator and Door (D5, D6) implement it.
///
/// Example:
///     public class Generator : MonoBehaviour, IInteractable
///     {
///         public string Prompt => "Turn on generator";
///         public bool CanInteract => !isOn;
///         public void Interact() { isOn = true; /* unlock the exit */ }
///     }
/// </summary>
public interface IInteractable
{
    /// <summary>Text after "Press E to", e.g. "Turn on generator".</summary>
    string Prompt { get; }

    /// <summary>False when it can't be used right now (already on, locked). The prompt is hidden.</summary>
    bool CanInteract { get; }

    /// <summary>What happens when the player presses E.</summary>
    void Interact();
}
