using UnityEngine;

/// <summary>
/// The generator. Turning it on opens the exit door, completes the "generator" objective
/// and makes a loud noise that attracts the creature (the final chase).
/// Implements IInteractable, so PlayerInteractor shows the prompt and calls Interact() on E.
/// </summary>
public class Generator : MonoBehaviour, IInteractable
{
    [SerializeField] private Door exitDoor;
    [SerializeField] private string objectiveId = "generator";

    [Header("Noise (attracts the creature)")]
    [SerializeField] private float noiseRadius = 30f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource startSound;

    [Header("Power cell")]
    [SerializeField] private bool needsPowerCell = true;

    private bool isOn;
    private bool hasPowerCell;

    // IInteractable: what the HUD shows and whether it can be used.
    public string Prompt => "ACTIVATE GENERATOR";
    public bool CanInteract => !isOn && (!needsPowerCell || hasPowerCell);

    public void Interact()
    {
        if (!CanInteract) return;
        isOn = true;

        if (exitDoor != null) exitDoor.Open();
        if (startSound != null) startSound.Play();

        // Loud start-up: the creature hears this from most of the map.
        NoiseSystem.Emit(transform.position, noiseRadius, gameObject);

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.Complete(objectiveId);

        Debug.Log("[Generator] ON: exit door opening");
    }

    /// <summary>Called by the PowerSocket when the cell is dropped in.</summary>
    public void InsertPowerCell()
    {
        hasPowerCell = true;
        Debug.Log("[Generator] Power cell inserted");
    }
}