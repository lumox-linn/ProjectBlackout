using UnityEngine;
using ProjectBlackout.PlayerTest;

/// <summary>
/// Trigger box next to the generator. When the player drops the power cell (tag "PowerCell")
/// into it, the cell snaps into place, the generator gets power and the objective completes.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PowerSocket : MonoBehaviour
{
    [SerializeField] private Generator generator;
    [Tooltip("Where the cell sits once inserted.")]
    [SerializeField] private Transform snapPoint;
    [SerializeField] private string objectiveId = "powercell";

    private bool filled;

    private void Reset() { GetComponent<Collider>().isTrigger = true; }

    private void OnTriggerStay(Collider other)
    {
        if (filled || !other.CompareTag("PowerCell"))
            return;

        // Wait until the player lets go of it with the magnet.
        var magnetic = other.GetComponentInParent<BlackoutMagneticObject>();
        if (magnetic != null && magnetic.IsHeld)
            return;

        filled = true;

        // Lock the cell into the socket so it can't be pulled out again.
        Rigidbody body = other.attachedRigidbody;
        if (body != null) body.isKinematic = true;
        if (magnetic != null) magnetic.canBePickedUp = false;

        Transform cell = body != null ? body.transform : other.transform;
        if (snapPoint != null)
            cell.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);

        if (generator != null) generator.InsertPowerCell();
        if (ObjectiveManager.Instance != null) ObjectiveManager.Instance.Complete(objectiveId);
    }
}