using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the objectives in order and tells the HUD which one is current (GameEvents.ObjectiveChanged).
/// Other scripts call ObjectiveManager.Instance.Complete("id") when the player finishes a step.
/// </summary>
public class ObjectiveManager : MonoBehaviour
{
    [System.Serializable]
    public class Objective
    {
        public string id;                 // short name used by other scripts, e.g. "tutorial"
        [TextArea] public string text;    // what the HUD shows

        public Objective() { }
        public Objective(string id, string text) { this.id = id; this.text = text; }
    }

    public static ObjectiveManager Instance { get; private set; }

    [SerializeField]
    private List<Objective> objectives = new List<Objective>
    {
        new Objective("gauntlet",  "Find the magnetic gauntlet ([E] to pick it up)"),
        new Objective("tutorial",  "Hold RIGHT-CLICK to pull the crate onto the plate, release to drop it"),
        new Objective("storage",   "Clear the crates blocking the storage room (hold a crate + LEFT-CLICK to throw)"),
        new Objective("plate",     "Put the heavy crate on the pressure plate in the main corridor"),
        new Objective("generator", "Turn on the generator"),
        new Objective("exit",      "Escape through the exit"),
    };

    private int currentIndex;
    private readonly HashSet<string> done = new HashSet<string>();

    public string CurrentId => currentIndex < objectives.Count ? objectives[currentIndex].id : null;
    public string CurrentText => currentIndex < objectives.Count ? objectives[currentIndex].text : "";

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Start() { ShowCurrent(); }

    public void Complete(string id)
    {
        // Ignore empty ids and steps that were already completed.
        if (string.IsNullOrEmpty(id) || !done.Add(id))
            return;

        Debug.Log($"[Objectives] Completed: {id}");

        // Move forward past every step that's done (so finishing one early still works).
        bool moved = false;
        while (currentIndex < objectives.Count && done.Contains(objectives[currentIndex].id))
        {
            currentIndex++;
            moved = true;
        }

        if (moved)
            ShowCurrent();
    }

    private void ShowCurrent()
    {
        Debug.Log($"[Objectives] Now: {CurrentText}");
        GameEvents.RaiseObjectiveChanged(CurrentText);
    }
}