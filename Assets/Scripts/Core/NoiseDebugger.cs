using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Testing helper: draws every noise as a yellow wire sphere in the Scene view (and the Game view
/// if Gizmos is switched on) for a couple of seconds. It can also fake a noise with a key press.
///
/// Setup: create an empty GameObject called "NoiseDebugger" in your test scene and add this script.
/// Remove it (or untick it) in the final game.
/// </summary>
public class NoiseDebugger : MonoBehaviour
{
    [Tooltip("How long each noise sphere stays visible, in seconds.")]
    [SerializeField] private float showSeconds = 2f;

    [Header("Fake noise for testing (optional)")]
    [Tooltip("Press this key in Play mode to make a noise at the Fake Noise Point.")]
    [SerializeField] private Key fakeNoiseKey = Key.N;
    [SerializeField] private Transform fakeNoisePoint;
    [SerializeField] private float fakeNoiseRadius = 15f;

    private struct Entry
    {
        public NoiseEvent noise;
        public float time;
    }

    private readonly List<Entry> recent = new List<Entry>();

    private void OnEnable()  { NoiseSystem.OnNoise += Record; }
    private void OnDisable() { NoiseSystem.OnNoise -= Record; }

    private void Record(NoiseEvent noise)
    {
        recent.Add(new Entry { noise = noise, time = Time.time });
        string who = noise.Source != null ? noise.Source.name : "unknown";
        Debug.Log($"[Noise] {who} at {noise.Position} (radius {noise.Radius:0.0} m)");
    }

    private void Update()
    {
        // Uses the new Input System (the project's Active Input Handling is "Input System Package").
        if (fakeNoisePoint != null && Keyboard.current != null && Keyboard.current[fakeNoiseKey].wasPressedThisFrame)
            NoiseSystem.Emit(fakeNoisePoint.position, fakeNoiseRadius, gameObject);
        recent.RemoveAll(r => Time.time - r.time > showSeconds);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        foreach (var r in recent)
        {
            Gizmos.DrawWireSphere(r.noise.Position, r.noise.Radius);
            Gizmos.DrawSphere(r.noise.Position, 0.2f);
        }
    }
}
