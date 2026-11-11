using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The generator. Turning it on opens the exit door, completes the "generator" objective
/// and makes a loud noise that attracts the creature (the final chase).
/// Implements IInteractable so Eliza's PlayerInteractor can use it. Until that exists,
/// it also checks for E itself when the player is close.
/// </summary>
public class Generator : MonoBehaviour, IInteractable
{
    [SerializeField] private Door exitDoor;
    [SerializeField] private string objectiveId = "generator";

    [Header("Noise (attracts the creature)")]
    [SerializeField] private float noiseRadius = 30f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource startSound;

    [Header("Fallback input until PlayerInteractor exists")]
    [SerializeField] private bool useOwnInput = true;
    [SerializeField] private float useRange = 2.5f;

    private bool isOn;
    private Transform player;

    // IInteractable: what the HUD shows and whether it can be used.
    public string Prompt => "ACTIVATE GENERATOR";
    public bool CanInteract => !isOn && (!needsPowerCell || hasPowerCell);

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (!useOwnInput || isOn || player == null || Keyboard.current == null)
            return;
        if (GameEvents.CurrentState != GameState.Playing)
            return;

        bool close = Vector3.Distance(player.position, transform.position) <= useRange;
        if (close && Keyboard.current.eKey.wasPressedThisFrame)
            Interact();
    }

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

    [Header("Power cell")]
    [SerializeField] private bool needsPowerCell = true;
    private bool hasPowerCell;

    /// <summary>Called by the PowerSocket when the cell is dropped in.</summary>
    public void InsertPowerCell()
    {
        hasPowerCell = true;
        Debug.Log("[Generator] Power cell inserted");
    }
}