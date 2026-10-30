using System;
using UnityEngine;

/// <summary>
/// One noise in the world: where it happened, how far it can be heard, and what made it.
/// </summary>
public struct NoiseEvent
{
    public Vector3 Position;   // where the noise happened
    public float Radius;       // how far away it can be heard (metres)
    public GameObject Source;  // what made it (can be null)

    public NoiseEvent(Vector3 position, float radius, GameObject source)
    {
        Position = position;
        Radius = radius;
        Source = source;
    }
}

/// <summary>
/// The shared "noise channel" between physics and AI.
///
/// Anyone can make a noise:      NoiseSystem.Emit(transform.position, 10f, gameObject);
/// Anyone can listen for noises: NoiseSystem.OnNoise += HandleNoise;   (and -= when disabled)
///
/// It is a static class, so it needs no GameObject in the scene.
/// Owner: Linn (Scripts/Core). Used by Aprial (A3 impact noise) and Sylvia (B5 hearing).
/// </summary>
public static class NoiseSystem
{
    /// <summary>Fires every time something makes a noise.</summary>
    public static event Action<NoiseEvent> OnNoise;

    /// <summary>Make a noise at a position that can be heard within radius metres.</summary>
    public static void Emit(Vector3 position, float radius, GameObject source = null)
    {
        if (radius <= 0f) return;
        OnNoise?.Invoke(new NoiseEvent(position, radius, source));
    }

    // Static events survive between play sessions when "Enter Play Mode Options" skips
    // domain reload. This clears old listeners each time Play is pressed so they don't pile up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnNoise = null;
    }
}
