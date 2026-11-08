using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A door that slides open and closed. Other scripts (the pressure plate, the generator)
/// call Open(), Close() or SetOpen(true/false). The door sinks into the floor when it opens.
/// </summary>
public class Door : MonoBehaviour
{
    [Tooltip("How far the door moves when it opens. Negative Y = it sinks into the floor.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, -3.1f, 0f);

    [Tooltip("How fast the door moves, in metres per second.")]
    [SerializeField] private float speed = 3f;

    [Tooltip("Tick this in Play mode to test the door.")]
    [SerializeField] private bool isOpen = false;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private NavMeshObstacle obstacle;   // stops the creature walking through the closed door

    public bool IsOpen => isOpen;

    private void Awake()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + openOffset;
        obstacle = GetComponent<NavMeshObstacle>();
    }

    private void Update()
    {
        // Slide a little towards the open or closed position every frame.
        Vector3 target = isOpen ? openPosition : closedPosition;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);

        // While open, switch the obstacle off so the creature can walk through.
        if (obstacle != null)
            obstacle.enabled = !isOpen;
    }

    public void Open() { isOpen = true; }
    public void Close() { isOpen = false; }
    public void SetOpen(bool open) { isOpen = open; }
}