using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// sight and hearing for the creature
// sight: the player is seen only if they are close enough AND inside the view cone AND no wall is in between
// (the cheap distance and angle checks run first, so the raycast only runs when both pass)
// hearing: listens to NoiseSystem.OnNoise (Core) and hears a noise if it is within the noise's radius
// field-of-view technique (distance + angle + raycast) based on:
// Lague, S. (2015) "Field of view visualisation (E01)", YouTube (Sebastian Lague's channel)
// Unity Scripting API: https://docs.unity3d.com/ScriptReference/Vector3.Angle.html
// Unity Scripting API: https://docs.unity3d.com/ScriptReference/Physics.Raycast.html
[DisallowMultipleComponent]
public class CreatureSenses : MonoBehaviour
{
    [Header("Sight")]
    // where the creature looks from (found automatically if left empty)
    [SerializeField] private Transform eyes;
    // how far the creature can see (metres)
    public float viewDistance = 12.0f;
    // total width of the view cone in degrees (110 = 55 degrees to each side)
    public float viewAngle = 110.0f;
    // how often sight is checked (0.2 = 5 times per second, not every frame)
    public float sightCheckInterval = 0.2f;
    // which layers can block sight (leave on Everything)
    public LayerMask sightBlockers = ~0;
    // the tag the player uses
    public string playerTag = "Player";

    [Header("Hearing")]
    // multiplies every noise's radius (1 = hear a noise exactly as far as its radius)
    public float hearingMultiplier = 1.0f;
    // how long a heard noise waits for a state to react to it before it is forgotten (seconds)
    public float noiseMemoryTime = 0.5f;

    [Header("Debug")]
    // write to the Console when the creature starts or stops seeing the player
    public bool logSightChanges = true;
    // write to the Console when the creature hears (or is too far from) a noise
    public bool logNoises = true;

    // the sight results
    private bool canSeePlayer = false;
    private Transform player;
    private Vector3 lastSeenPlayerPosition;
    private bool hasSeenPlayer = false;
    private float lastSeenTime = 0.0f;

    // the hearing results
    private bool hasHeardNoise = false;
    private Vector3 lastNoisePosition;
    private float lastNoiseTime = 0.0f;
    // true when a noise has been heard but no state has reacted to it yet
    private bool noisePending = false;

    // the player's collider (we aim at its middle, not the player's feet)
    private Collider playerCollider;
    // the time of the next sight check
    private float nextCheckTime = 0.0f;

    // lets the states read the results (but not change them)
    public bool CanSeePlayer
    {
        get { return canSeePlayer; }
    }

    public Transform Player
    {
        get { return player; }
    }

    public Vector3 LastSeenPlayerPosition
    {
        get { return lastSeenPlayerPosition; }
    }

    public bool HasSeenPlayer
    {
        get { return hasSeenPlayer; }
    }

    // seconds since the player was last seen (Infinity if never seen)
    public float TimeSinceLastSeen
    {
        get
        {
            if (!hasSeenPlayer)
            {
                return Mathf.Infinity;
            }
            return Time.time - lastSeenTime;
        }
    }

    public bool HasHeardNoise
    {
        get { return hasHeardNoise; }
    }

    public Vector3 LastNoisePosition
    {
        get { return lastNoisePosition; }
    }

    void Awake()
    {
        // find the Eyes child if it was not assigned
        if (eyes == null)
        {
            eyes = transform.Find("Eyes");
        }

        // no eyes at all: look from the creature's middle
        if (eyes == null)
        {
            eyes = transform;
        }
    }

    // listen for noises while the creature is enabled
    void OnEnable()
    {
        NoiseSystem.OnNoise += HandleNoise;
    }

    // stop listening (stops errors if the creature is destroyed)
    void OnDisable()
    {
        NoiseSystem.OnNoise -= HandleNoise;
    }

    void Start()
    {
        // find the player by its tag
        FindPlayer();

        // random first check, so several creatures would not all raycast on the same frame
        nextCheckTime = Time.time + Random.Range(0.0f, sightCheckInterval);
    }

    void Update()
    {
        // only check sight a few times per second
        if (Time.time < nextCheckTime)
        {
            return;
        }
        nextCheckTime = Time.time + sightCheckInterval;

        // the player might not exist yet (or was replaced), so try to find it again
        if (player == null)
        {
            FindPlayer();
            if (player == null)
            {
                SetCanSee(false);
                return;
            }
        }

        // check if the player can be seen and remember where they were
        bool seen = CheckSight();
        if (seen)
        {
            lastSeenPlayerPosition = player.position;
            lastSeenTime = Time.time;
            hasSeenPlayer = true;
        }
        SetCanSee(seen);
    }

    // called by NoiseSystem every time something makes a noise
    private void HandleNoise(NoiseEvent noise)
    {
        // ignore noises the creature makes itself
        if (noise.Source == gameObject)
        {
            return;
        }

        // how far away the noise is and how far it can be heard
        float distance = Vector3.Distance(transform.position, noise.Position);
        float hearingRadius = noise.Radius * hearingMultiplier;
        string sourceName = noise.Source != null ? noise.Source.name : "unknown";

        // too far away to hear (sound goes through walls, so there is no raycast here)
        if (distance > hearingRadius)
        {
            if (logNoises)
            {
                Debug.Log("[Senses] Noise from " + sourceName + " too far away (" + distance.ToString("0.0") + " m > " + hearingRadius.ToString("0.0") + " m). Ignored.", this);
            }
            return;
        }

        // remember the noise until a state reacts to it
        lastNoisePosition = noise.Position;
        lastNoiseTime = Time.time;
        hasHeardNoise = true;
        noisePending = true;

        if (logNoises)
        {
            Debug.Log("[Senses] Heard a noise from " + sourceName + ", " + distance.ToString("0.0") + " m away.", this);
        }
    }

    // the states call this to react to a noise
    // returns true (once) if a recent noise is waiting
    public bool TryConsumeNoise(out Vector3 position)
    {
        position = lastNoisePosition;
        if (!noisePending)
        {
            return false;
        }

        // the noise is used up, and it only counts if it is recent
        noisePending = false;
        return Time.time - lastNoiseTime <= noiseMemoryTime;
    }

    // the three sight checks: distance, angle, then line of sight
    private bool CheckSight()
    {
        Vector3 origin = eyes.position;
        Vector3 target = PlayerTargetPoint();
        Vector3 toPlayer = target - origin;
        float distance = toPlayer.magnitude;

        // 1) distance check (cheapest)
        if (distance > viewDistance)
        {
            return false;
        }

        // 2) angle check (height is ignored so looking up or down a step does not matter)
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0.0f, toPlayer.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
        if (flatToPlayer.sqrMagnitude > 0.0001f && Vector3.Angle(flatForward, flatToPlayer) > viewAngle * 0.5f)
        {
            return false;
        }

        // 3) line of sight: what does a ray from the eyes towards the player hit first?
        // (triggers are ignored, and a ray that starts inside our own capsule does not hit it)
        RaycastHit hit;
        if (Physics.Raycast(origin, toPlayer / distance, out hit, distance, sightBlockers, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == player || hit.transform.IsChildOf(player);
        }

        // nothing in the way at all
        return true;
    }

    // aim at the middle of the player's collider (not their feet)
    private Vector3 PlayerTargetPoint()
    {
        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }
        return player.position + Vector3.up;
    }

    // finds the player by its tag and gets its collider
    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag(playerTag);
        if (playerObject == null)
        {
            player = null;
            playerCollider = null;
            return;
        }

        player = playerObject.transform;
        playerCollider = playerObject.GetComponent<Collider>();
        if (playerCollider == null)
        {
            playerCollider = playerObject.GetComponentInChildren<Collider>();
        }
    }

    // updates canSeePlayer and logs when it changes
    private void SetCanSee(bool seen)
    {
        if (seen != canSeePlayer && logSightChanges)
        {
            if (seen)
            {
                Debug.Log("[Senses] I can see the player!", this);
            }
            else
            {
                Debug.Log("[Senses] Lost sight of the player.", this);
            }
        }
        canSeePlayer = seen;
    }

    // Scene view: the view cone (yellow = can't see the player, red = can see them),
    // a red line to the visible player, a magenta sphere at the last seen position,
    // and a cyan sphere + line to the last heard noise (for 3 seconds)
    void OnDrawGizmos()
    {
        // look from the eyes (they may not be found yet in Edit mode)
        Transform eyeTransform = eyes != null ? eyes : transform.Find("Eyes");
        Vector3 origin = eyeTransform != null ? eyeTransform.position : transform.position;

        Vector3 forward = new Vector3(transform.forward.x, 0.0f, transform.forward.z).normalized;
        float halfAngle = viewAngle * 0.5f;
        int segments = 24;

        // the view cone colour
        if (Application.isPlaying && canSeePlayer)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }

        // draw the cone as an arc made of short lines
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

        // the rest only makes sense in Play mode
        if (!Application.isPlaying)
        {
            return;
        }

        // red line to the visible player, or a magenta sphere where they were last seen
        if (canSeePlayer && player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, PlayerTargetPoint());
        }
        else if (hasSeenPlayer)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastSeenPlayerPosition, 0.4f);
        }

        // the last heard noise
        if (hasHeardNoise && Time.time - lastNoiseTime < 3.0f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastNoisePosition, 0.5f);
            Gizmos.DrawLine(transform.position, lastNoisePosition);
        }
    }
}
