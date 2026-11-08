using UnityEngine;

// ---------------------------------------------------------------------------
// NoiseTestPoint  (B5 test helper, TEMPORARY)
//
// Makes a fake noise without needing the keyboard or Aprial's crates.
// Put it on an empty GameObject, move the object where you want the noise,
// then in Play mode: right-click this component's header > "Make Noise Here".
// (NoiseDebugger in Core draws the yellow noise sphere.)
// ---------------------------------------------------------------------------
public class NoiseTestPoint : MonoBehaviour
{
    [Tooltip("How far away the noise can be heard (metres).")]
    [SerializeField] private float radius = 15f;

    [ContextMenu("Make Noise Here")]
    private void MakeNoise()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[NoiseTestPoint] Only works in Play mode.", this);
            return;
        }

        NoiseSystem.Emit(transform.position, radius, gameObject);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f); // orange
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }

    // Shows how far the noise would reach when this object is selected.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
