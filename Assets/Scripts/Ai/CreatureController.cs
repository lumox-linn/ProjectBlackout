using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------
// CreatureController  (Task B2)
//
// The MonoBehaviour on the Creature. It is the "brain host":
// - owns the NavMeshAgent
// - creates the StateMachine and the 5 states (once, in Awake)
// - starts in Patrol
// - runs the state machine every frame
//
// Later tasks add: speeds and tuning values (B3+), senses (B4/B5),
// IStunnable (B7).
// ---------------------------------------------------------------------------
[RequireComponent(typeof(NavMeshAgent))]
public class CreatureController : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Write every state change to the Console.")]
    [SerializeField] private bool logStateChanges = true;

    [Tooltip("In Play mode, press 1-5 to force a state: " +
             "1 Patrol, 2 Investigate, 3 Chase, 4 Attack, 5 Stunned. For testing only.")]
    [SerializeField] private bool debugKeys = true;

    // --- Things the states can use (read-only from outside) ---
    public NavMeshAgent Agent { get; private set; }
    public StateMachine Fsm { get; private set; }

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

    // New Input System: read the keyboard directly (no Input.GetKey).
    private void HandleDebugKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return; // no keyboard connected
        }

        if (keyboard.digit1Key.wasPressedThisFrame) Fsm.ChangeState(Patrol);
        if (keyboard.digit2Key.wasPressedThisFrame) Fsm.ChangeState(Investigate);
        if (keyboard.digit3Key.wasPressedThisFrame) Fsm.ChangeState(Chase);
        if (keyboard.digit4Key.wasPressedThisFrame) Fsm.ChangeState(Attack);
        if (keyboard.digit5Key.wasPressedThisFrame) Fsm.ChangeState(Stunned);
    }
}
