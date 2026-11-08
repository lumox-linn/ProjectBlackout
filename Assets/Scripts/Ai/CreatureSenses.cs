using UnityEngine;

// ---------------------------------------------------------------------------
// CreatureSenses  (B4: sight, B5: hearing)
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
// Hearing (B5): listens to NoiseSystem.OnNoise (Linn's shared Core script).
// A noise is heard if the distance from the creature to the noise is
// <= the noise's radius. Sound goes through walls, so no raycast here.
// A heard noise is kept as "pending" for a short time; the current state
// takes it with TryConsumeNoise(). States that don't care (Chase) just
// ignore it and it's forgotten.
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

    [Header("Hearing (B5)")]
    [Tooltip("Multiplies every noise's radius. 1 = hear a noise exactly as far as its radius.")]
    [SerializeField] private float hearingMultiplier = 1f;

    [Tooltip("How long a heard noise waits for a state to react to it before it's forgotten (seconds).")]
    [SerializeField] private float noiseMemoryTime = 0.5f;

    [Header("Debug")]
    [Tooltip("Write to the Console when the creature starts or stops seeing the player.")]
    [SerializeField] private bool logSightChanges = true;

    [Tooltip("Write to the Console when the creature hears (or is too far from) a noise.")]
    [SerializeField] private bool logNoises = true;

    // --- Results that the states read ---
    public bool CanSeePlayer { get; private set; }
    public Transform Player { get; private set; }
    public Vector3 LastSeenPlayerPosition { get; private set; }
    public bool HasSeenPlayer { get; private set; }

    // Seconds since the player was last seen (Infinity if never seen).
    public float TimeSinceLastSeen => HasSeenPlayer ? Time.time - lastSeenTime : Mathf.Infinity;

    // Hearing results.
    public bool HasHeardNoise { get; private set; }
    public Vector3 LastNoisePosition { get; private set; }

    private Collider playerCollider;
    private float nextCheckTime;
    private float lastSeenTime;
    private float lastNoiseTime;
    private bool noisePending;

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

    // Subscribe/unsubscribe to the shared noise event (as the Core script says).
    // Unsubscribing in OnDisable stops errors if the creature is destroyed.
    private void OnEnable()
    {
        NoiseSystem.OnNoise += HandleNoise;
    }

    private void OnDisable()
    {
        NoiseSystem.OnNoise -= HandleNoise;
    }

    private void HandleNoise(NoiseEvent noise)
    {
        // Ignore noises the creature makes itself.
        if (noise.Source == gameObject)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, noise.Position);
        float hearingRadius = noise.Radius * hearingMultiplier;
        string sourceName = noise.Source != null ? noise.Source.name : "unknown";

        if (distance > hearingRadius)
        {
            if (logNoises)
            {
                Debug.Log($"[Senses] Noise from {sourceName} too far away ({distance:F1} m > {hearingRadius:F1} m). Ignored.", this);
            }
            return;
        }

        LastNoisePosition = noise.Position;
        lastNoiseTime = Time.time;
        HasHeardNoise = true;
        noisePending = true;

        if (logNoises)
        {
            Debug.Log($"[Senses] Heard a noise from {sourceName}, {distance:F1} m away.", this);
        }
    }

    // States call this to react to a noise. Returns true (once) if a recent noise is waiting.
    public bool TryConsumeNoise(out Vector3 position)
    {
        position = LastNoisePosition;
        if (!noisePending)
        {
            return false;
        }

        noisePending = false;
        return Time.time - lastNoiseTime <= noiseMemoryTime;
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
    // - a cyan sphere + line to the last heard noise (for 3 seconds)
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

        if (HasHeardNoise && Time.time - lastNoiseTime < 3f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(LastNoisePosition, 0.5f);
            Gizmos.DrawLine(transform.position, LastNoisePosition);
        }
    }
}
