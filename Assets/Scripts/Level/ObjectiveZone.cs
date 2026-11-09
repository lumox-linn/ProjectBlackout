using UnityEngine;

/// <summary>An invisible trigger box: when the player walks in, it completes an objective (once).</summary>
[RequireComponent(typeof(Collider))]
public class ObjectiveZone : MonoBehaviour
{
    [SerializeField] private string objectiveId = "storage";
    private bool triggered;

    // Runs when the component is first added: makes the collider a trigger automatically.
    private void Reset() { GetComponent<Collider>().isTrigger = true; }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player"))
            return;

        triggered = true;
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.Complete(objectiveId);
    }
}