using UnityEngine;

/// <summary>Walking into the exit completes the last objective and wins the game.</summary>
[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    [SerializeField] private string objectiveId = "exit";
    private bool used;

    private void Reset() { GetComponent<Collider>().isTrigger = true; }

    private void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag("Player"))
            return;
        used = true;

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.Complete(objectiveId);

        if (GameManager.Instance != null)
            GameManager.Instance.WinGame();   // Eliza's Win screen

        Debug.Log("[Exit] Player escaped: WIN");
    }
}