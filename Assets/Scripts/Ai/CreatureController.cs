using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// ---------------------------------------------------------------------------
// CreatureController  (B2, updated in B3, B4, B5 and B6)
//
// The MonoBehaviour on the Creature. It is the "brain host":
// - owns the NavMeshAgent
// - holds the tuning values the states use (shown in the Inspector)
// - creates the StateMachine and the 5 states (once, in Awake)
// - starts in Patrol and runs the state machine every frame
//
// B3 added: the patrol point list, patrol speed / wait time / stuck timeout,
// auto-finding the patrol points, and a gizmo that draws the patrol route.
// B4 added: the link to CreatureSenses, and the chase settings.
// B5 added: the investigate settings (speed, look-around time and angle).
// B6 added: the attack settings, FlatDistanceToPlayer(), attack range gizmo.
// Later tasks add: IStunnable (B7).
// ---------------------------------------------------------------------------
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CreatureSenses))]
public class CreatureController : MonoBehaviour
{
    [Header("Patrol (B3)")]
    [Tooltip("The patrol route, in order. Drag PatrolPoint_1..10 here, or right-click this " +
             "component's header > 'Fill Patrol Points From Level'. If the list is empty when the " +
             "game starts, the points are found automatically under the object named below.")]
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

    [Tooltip("Name of the GameObject whose children are the patrol points (Level/PatrolPoints).")]
    [SerializeField] private string patrolPointsParentName = "PatrolPoints";

    [Tooltip("Walking speed while patrolling (m/s).")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("How long the creature pauses at each patrol point (seconds).")]
    [SerializeField] private float patrolWaitTime = 1.5f;

    [Tooltip("If the creature hasn't moved for this long on the way to a point, " +
             "it gives up and goes to the next point (seconds).")]
    [SerializeField] private float stuckTimeout = 3f;

    [Header("Chase (B4)")]
    [Tooltip("Running speed while chasing the player (m/s).")]
    [SerializeField] private float chaseSpeed = 4.5f;

    [Tooltip("How often the chase target is updated (seconds). Not every frame = cheaper.")]
    [SerializeField] private float chaseRepathInterval = 0.2f;

    [Tooltip("After losing sight of the player, keep chasing towards the last seen position " +
             "for this long before giving up and switching to Investigate (seconds).")]
    [SerializeField] private float loseSightTime = 1.5f;

    [Header("Investigate (B5)")]
    [Tooltip("Walking speed while going to check a noise or the last seen position (m/s).")]
    [SerializeField] private float investigateSpeed = 3f;

    [Tooltip("How long the creature looks around after reaching the spot (seconds).")]
    [SerializeField] private float lookAroundTime = 3f;

    [Tooltip("How far it turns to each side while looking around (degrees).")]
    [SerializeField] private float lookAroundAngle = 90f;

    [Header("Attack (B6)")]
    [Tooltip("The creature attacks when the player is this close (metres). " +
             "Must be bigger than the NavMeshAgent's Stopping Distance.")]
    [SerializeField] private float attackRange = 1.8f;

    [Tooltip("Extra distance before it gives up attacking and chases again (metres). " +
             "Stops it flickering between Attack and Chase at the edge of the range.")]
    [SerializeField] private float attackRangeBuffer = 0.4f;

    [Tooltip("Delay before the FIRST hit after reaching the player (seconds). Gives the player a moment to react.")]
    [SerializeField] private float attackWindup = 0.4f;

    [Tooltip("Time between hits (seconds).")]
    [SerializeField] private float attackCooldown = 1.2f;

    [Tooltip("Damage per hit. The player dies after 3 hits.")]
    [SerializeField] private int attackDamage = 1;

    [Tooltip("How fast the creature turns to face the player while attacking (degrees per second).")]
    [SerializeField] private float attackTurnSpeed = 360f;

    [Header("Debug")]
    [Tooltip("Write every state change to the Console.")]
    [SerializeField] private bool logStateChanges = true;

    [Tooltip("Write a Console line each time a patrol point is reached.")]
    [SerializeField] private bool logPatrolPoints = true;

    [Tooltip("In Play mode, click the Game view, then press 1-5 (number row or number pad) to force a state: " +
             "1 Patrol, 2 Investigate, 3 Chase, 4 Attack, 5 Stunned. For testing only.")]
    [SerializeField] private bool debugKeys = true;

    // --- Things the states can use (read-only from outside) ---
    public NavMeshAgent Agent { get; private set; }
    public CreatureSenses Senses { get; private set; }
    public StateMachine Fsm { get; private set; }

    public IReadOnlyList<Transform> PatrolPoints => patrolPoints;
    public float PatrolSpeed => patrolSpeed;
    public float PatrolWaitTime => patrolWaitTime;
    public float StuckTimeout => stuckTimeout;
    public bool LogPatrolPoints => logPatrolPoints;

    public float ChaseSpeed => chaseSpeed;
    public float ChaseRepathInterval => chaseRepathInterval;
    public float LoseSightTime => loseSightTime;

    public float InvestigateSpeed => investigateSpeed;
    public float LookAroundTime => lookAroundTime;
    public float LookAroundAngle => lookAroundAngle;

    public float AttackRange => attackRange;
    public float AttackRangeBuffer => attackRangeBuffer;
    public float AttackWindup => attackWindup;
    public float AttackCooldown => attackCooldown;
    public int AttackDamage => attackDamage;
    public float AttackTurnSpeed => attackTurnSpeed;

    // Horizontal distance to the player (height ignored). Infinity if there's no player.
    public float FlatDistanceToPlayer()
    {
        if (Senses == null || Senses.Player == null)
        {
            return Mathf.Infinity;
        }

        Vector3 offset = Senses.Player.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // The 5 states. Created once and reused (no new objects every switch).
    public PatrolState Patrol { get; private set; }
    public InvestigateState Investigate { get; private set; }
    public ChaseState Chase { get; private set; }
    public AttackState Attack { get; private set; }
    public StunnedState Stunned { get; private set; }

    // Used by the debug label.
    public string CurrentStateName
    {
        get
        {
            if (Fsm == null || Fsm.CurrentState == null)
            {
                return "-";
            }
            return Fsm.CurrentState.Name;
        }
    }

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Senses = GetComponent<CreatureSenses>();
        if (Senses == null)
        {
            // Safety net in case the component wasn't added in the Inspector.
            Debug.LogWarning("[Creature] No CreatureSenses component found, so one was added at runtime. " +
                             "Add it to the Creature prefab so its settings are saved.", this);
            Senses = gameObject.AddComponent<CreatureSenses>();
        }

        // Remove empty slots, then auto-fill if nothing was assigned.
        patrolPoints.RemoveAll(point => point == null);
        if (patrolPoints.Count == 0)
        {
            FindPatrolPointsInScene();
        }

        // The agent must stop BEFORE it's out of attack range, or it can never attack.
        if (Agent.stoppingDistance >= attackRange)
        {
            Debug.LogWarning($"[Creature] NavMeshAgent Stopping Distance ({Agent.stoppingDistance}) should be " +
                             $"smaller than Attack Range ({attackRange}), or the creature can't reach attack range.", this);
        }

        Fsm = new StateMachine(this, logStateChanges);

        Patrol = new PatrolState(this);
        Investigate = new InvestigateState(this);
        Chase = new ChaseState(this);
        Attack = new AttackState(this);
        Stunned = new StunnedState(this);
    }

    private void Start()
    {
        // Starting state.
        Fsm.ChangeState(Patrol);
    }

    private void Update()
    {
        // Lets you tick/untick "Log State Changes" during Play mode.
        Fsm.LogChanges = logStateChanges;

        if (debugKeys)
        {
            HandleDebugKeys();
        }

        Fsm.Tick();
    }

    // Finds the object called patrolPointsParentName (e.g. "PatrolPoints") and adds
    // its children to the list, in Hierarchy order (PatrolPoint_1, _2, ... _10).
    // Runs once, so GameObject.Find is fine here (it would be slow every frame).
    private void FindPatrolPointsInScene()
    {
        GameObject parent = GameObject.Find(patrolPointsParentName);
        if (parent == null)
        {
            Debug.LogWarning($"[Creature] No patrol points assigned and no GameObject called " +
                             $"'{patrolPointsParentName}' found. The creature will stand still in Patrol.", this);
            return;
        }

        foreach (Transform child in parent.transform)
        {
            patrolPoints.Add(child);
        }

        Debug.Log($"[Creature] Found {patrolPoints.Count} patrol points under '{patrolPointsParentName}'.", this);
    }

    // Right-click the Creature Controller header in the Inspector > "Fill Patrol Points From Level".
    // Fills the list in the Editor so you can see the route before pressing Play.
    [ContextMenu("Fill Patrol Points From Level")]
    private void FillPatrolPointsFromLevel()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Fill Patrol Points");
#endif
        patrolPoints.Clear();
        FindPatrolPointsInScene();
    }

    // New Input System: read the keyboard directly (no Input.GetKey).
    // Accepts both the number row above the letters AND the number pad.
    private void HandleDebugKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return; // no keyboard connected
        }

        if (Pressed(keyboard.digit1Key, keyboard.numpad1Key)) Fsm.ChangeState(Patrol);
        if (Pressed(keyboard.digit2Key, keyboard.numpad2Key)) Fsm.ChangeState(Investigate);
        if (Pressed(keyboard.digit3Key, keyboard.numpad3Key)) Fsm.ChangeState(Chase);
        if (Pressed(keyboard.digit4Key, keyboard.numpad4Key)) Fsm.ChangeState(Attack);
        if (Pressed(keyboard.digit5Key, keyboard.numpad5Key)) Fsm.ChangeState(Stunned);
    }

    private static bool Pressed(KeyControl rowKey, KeyControl numpadKey)
    {
        return rowKey.wasPressedThisFrame || numpadKey.wasPressedThisFrame;
    }

    // Backup for testing without the keyboard: in Play mode, right-click the
    // "Creature Controller" header in the Inspector and pick one of these.
    [ContextMenu("Debug: Force Patrol")]      private void ForcePatrol()      { ForceState(Patrol); }
    [ContextMenu("Debug: Force Investigate")] private void ForceInvestigate() { ForceState(Investigate); }
    [ContextMenu("Debug: Force Chase")]       private void ForceChase()       { ForceState(Chase); }
    [ContextMenu("Debug: Force Attack")]      private void ForceAttack()      { ForceState(Attack); }
    [ContextMenu("Debug: Force Stunned")]     private void ForceStunned()     { ForceState(Stunned); }

    private void ForceState(IState state)
    {
        if (!Application.isPlaying || Fsm == null)
        {
            Debug.LogWarning("[Creature] Force State only works in Play mode.", this);
            return;
        }
        Fsm.ChangeState(state);
    }

    // Scene view, when the Creature is selected:
    // - the patrol route (green)
    // - the attack range (red circle)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        DrawFlatCircle(transform.position, attackRange);

        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            return;
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            Transform current = patrolPoints[i];
            Transform next = patrolPoints[(i + 1) % patrolPoints.Count]; // last one links back to the first
            if (current == null || next == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(current.position, 0.4f);
            Gizmos.DrawLine(current.position, next.position);
        }
    }

    private static void DrawFlatCircle(Vector3 centre, float radius)
    {
        const int segments = 32;
        Vector3 previous = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 point = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
