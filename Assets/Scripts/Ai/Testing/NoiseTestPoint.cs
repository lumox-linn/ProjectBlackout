using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// test helper: makes a fake noise without the keyboard or the crates
// put it on an empty GameObject, then in Play mode right-click this component > "Make Noise Here"
// (NoiseDebugger in Core draws the yellow noise sphere)
public class NoiseTestPoint : MonoBehaviour
{
    // how far away the noise can be heard (metres)
    public float radius = 15.0f;

    // makes a noise at this object's position
    [ContextMenu("Make Noise Here")]
    private void MakeNoise()
    {
        // only works while the game is playing
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[NoiseTestPoint] Only works in Play mode.", this);
            return;
        }

        NoiseSystem.Emit(transform.position, radius, gameObject);
    }

    // a small orange circle so the point is easy to find
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1.0f, 0.6f, 0.0f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }

    // shows how far the noise would reach when this object is selected
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1.0f, 0.6f, 0.0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
