using UnityEngine;

// ---------------------------------------------------------------------------
// CreatureSenses  (B4: sight. B5 will add hearing.)
//
// Sight: the creature sees the player only if ALL THREE are true:
//   1. Distance: the player is within viewDistance (12 m)
//   2. Angle:    the player is inside the view cone (110° in front)
//   3. Line of sight: a raycast from the eyes to the player hits the player
//      first (no wall, door or crate in between)
// The cheap checks (distance, angle) run first, so the more expensive raycast
// only runs when the first two pass.
//
// The check runs a few times per second (sightCheckInterval), not every frame,
// which saves performance. The states just read the results:
//   CanSeePlayer, Player, LastSeenPlayerPosition, TimeSinceLastSeen.
//
// Field-of-view technique (distance + angle + raycast) based on:
//   Lague, S. (2015) "Field of view visualisation (E01)", YouTube
//   (Sebastian Lague's channel).
//   Unity Scripting API: Vector3.Angle and Physics.Raycast
//   https://docs.unity3d.com/ScriptReference/Vector3.Angle.html
//   https://docs.unity3d.com/ScriptReference/Physics.Raycast.html
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public class CreatureSenses : MonoBehaviour
{
    [Header("Sight (B4)")]
    [Tooltip("Where the creature looks from. Drag the 'Eyes' child here " +
             "(found automatically if left empty).")]
    [SerializeField] private Transform eyes;

    [Tooltip("How far the creature can see (metres).")]
    [SerializeField] private float viewDistance = 12f;

    [Tooltip("Total width of the view cone in degrees (110 = 55° to each side).")]
    [Range(1f, 360f)]
    [SerializeField] private float viewAngle = 110f;

    [Tooltip("How often sight is checked (seconds). 0.2 = 5 times per second.")]
    [SerializeField] private float sightCheckInterval = 0.2f;

    [Tooltip("Which layers can block sight. Leave on Everything.")]
    [SerializeField] private LayerMask sightBlockers = ~0;

    [Tooltip("The tag the player uses.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [Tooltip("Write to the Console when the creature starts or stops seeing the player.")]
    [SerializeField] private bool logSightChanges = true;

    // --- Results that the states read ---
    public bool CanSeePlayer { get; private set; }
    public Transform Player { get; private set; }
    public Vector3 LastSeenPlayerPosition { get; private set; }
    public bool HasSeenPlayer { get; private set; }

    // Seconds since the player was last seen (Infinity if never seen).
    public float TimeSinceLastSeen => HasSeenPlayer ? Time.time - lastSeenTime : Mathf.Infinity;

    private Collider playerCollider;
    private float nextCheckTime;
    private float lastSeenTime;

    private void Awake()
    {
        if (eyes == null)
        {
            eyes = transform.Find("Eyes");
        }
        if (eyes == null)
        {
            eyes = transform; // fallback: look from the creature's middle
        }
    }

    private void Start()
    {
        FindPlayer();

        // Random first check, so several creatures wouldn't all raycast on the same frame.
        nextCheckTime = Time.time + Random.Range(0f, sightCheckInterval);
    }

    private void Update()
    {
        if (Time.time < nextCheckTime)
        {
            return;
        }
        nextCheckTime = Time.time + sightCheckInterval;

        // The player might not exist yet (or was replaced): try to find it again.
        if (Player == null)
        {
            FindPlayer();
            if (Player == null)
            {
                SetCanSee(false);
                return;
            }
        }

        bool seen = CheckSight();
        if (seen)
        {
            LastSeenPlayerPosition = Player.position;
            lastSeenTime = Time.time;
            HasSeenPlayer = true;
        }
        SetCanSee(seen);
    }

    private bool CheckSight()
    {
        Vector3 origin = eyes.position;
        Vector3 target = PlayerTargetPoint();
        Vector3 toPlayer = target - origin;
        float distance = toPlayer.magnitude;

        // 1) Distance check (cheapest).
        if (distance > viewDistance)
        {
            return false;
        }

        // 2) Angle check. Ignore height (y) so looking up/down a step doesn't matter.
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (flatToPlayer.sqrMagnitude > 0.0001f &&
            Vector3.Angle(flatForward, flatToPlayer) > viewAngle * 0.5f)
        {
            return false;
        }

        // 3) Line of sight: what does a ray from the eyes towards the player hit first?
        //    Triggers (e.g. door zones, pressure plate) are ignored.
        //    A ray that starts inside a collider (our own capsule) doesn't hit it.
        if (Physics.Raycast(origin, toPlayer / distance, out RaycastHit hit, distance,
                            sightBlockers, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == Player || hit.transform.IsChildOf(Player);
        }

        // Nothing in the way at all.
        return true;
    }

    // Aim at the middle of the player's collider (not their feet).
    private Vector3 PlayerTargetPoint()
    {
        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }
        return Player.position + Vector3.up;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag(playerTag);
        if (playerObject == null)
        {
            Player = null;
            playerCollider = null;
            return;
        }

        Player = playerObject.transform;
        playerCollider = playerObject.GetComponent<Collider>();
        if (playerCollider == null)
        {
            playerCollider = playerObject.GetComponentInChildren<Collider>();
        }
    }

    private void SetCanSee(bool seen)
    {
        if (seen != CanSeePlayer && logSightChanges)
        {
            Debug.Log(seen ? "[Senses] I can see the player!" : "[Senses] Lost sight of the player.", this);
        }
        CanSeePlayer = seen;
    }

    // ---------------------------------------------------------------------------
    // Scene view gizmos (always shown, not only when selected):
    // - the view cone: yellow = can't see the player, red = can see them
    // - a red line to the player while they're visible
    // - a magenta sphere at the last seen position when they're out of sight
    // ---------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        Transform eyeTransform = eyes != null ? eyes : transform.Find("Eyes");
        Vector3 origin = eyeTransform != null ? eyeTransform.position : transform.position;

        Vector3 forward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        float halfAngle = viewAngle * 0.5f;
        const int segments = 24;

        Gizmos.color = (Application.isPlaying && CanSeePlayer) ? Color.red : Color.yellow;

        Vector3 previous = origin + Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward * viewDistance;
        Gizmos.DrawLine(origin, previous);
        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfAngle + viewAngle * i / segments;
            Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * viewDistance;
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
        Gizmos.DrawLine(origin, previous);

        if (!Application.isPlaying)
        {
            return;
        }

        if (CanSeePlayer && Player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, PlayerTargetPoint());
        }
        else if (HasSeenPlayer)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(LastSeenPlayerPosition, 0.4f);
        }
    }
}
