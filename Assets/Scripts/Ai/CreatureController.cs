using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;

// the "brain" of the creature: holds the settings, creates the state machine and the 5 states,
// and lets thrown crates stun it (IStunnable from Core)
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CreatureSenses))]
public class CreatureController : MonoBehaviour, IStunnable
{
    [Header("Patrol")]
    // the patrol route in order (PatrolPoint_1..10)
    // (right-click this component > "Fill Patrol Points From Level", or leave it empty and they are found at the start)
    public List<Transform> patrolPoints = new List<Transform>();
    // name of the GameObject whose children are the patrol points
    public string patrolPointsParentName = "PatrolPoints";
    // walking speed while patrolling (m/s)
    public float patrolSpeed = 2.0f;
    // how long the creature pauses at each patrol point (seconds)
    public float patrolWaitTime = 1.5f;
    // if the creature has not moved for this long, it skips to the next point (seconds)
    public float stuckTimeout = 3.0f;

    [Header("Chase")]
    // running speed while chasing the player (m/s)
    public float chaseSpeed = 4.5f;
    // how often the chase target is updated (seconds), not every frame so it is cheaper
    public float chaseRepathInterval = 0.2f;
    // after losing sight, keep going to the last seen position for this long before investigating (seconds)
    public float loseSightTime = 1.5f;

    [Header("Investigate")]
    // walking speed while going to check a noise or the last seen position (m/s)
    public float investigateSpeed = 3.0f;
    // how long the creature looks around after reaching the spot (seconds)
    public float lookAroundTime = 3.0f;
    // how far it turns to each side while looking around (degrees)
    public float lookAroundAngle = 90.0f;

    [Header("Attack")]
    // the creature attacks when the player is this close (must be bigger than the agent's Stopping Distance)
    public float attackRange = 1.8f;
    // extra distance before it stops attacking (stops it flickering between Attack and Chase)
    public float attackRangeBuffer = 0.4f;
    // when the claws strike in the attack animation, as a fraction of it (0 = start, 0.5 = halfway, 1 = end)
    // the player only loses health at this moment, so it matches what you see
    public float attackHitPoint = 0.5f;
    // how long one attack animation lasts (set automatically from the Attack animation by CreatureAnimation)
    public float attackDuration = 2.6f;
    // pause after an attack animation finishes, before the next swing starts (seconds)
    public float attackCooldown = 1.2f;
    // damage per hit (the player dies after 3 hits)
    public int attackDamage = 1;
    // how fast the creature turns to face the player while attacking (degrees per second)
    public float attackTurnSpeed = 360.0f;

    [Header("Stunned")]
    // after a stun ends, the creature cannot be stunned again for this long (seconds)
    public float stunImmunityTime = 3.0f;
    // stun length used by debug key 5 (a real crate passes its own time)
    public float debugStunSeconds = 3.0f;

    [Header("Transitions")]
    // minimum time in a state before a sense-based transition is allowed (stops flickering)
    public float minTimeInState = 0.5f;

    [Header("Debug")]
    // write every state change to the Console
    public bool logStateChanges = true;
    // write extra Console lines for Patrol and Investigate
    [FormerlySerializedAs("logPatrolPoints")]
    public bool logStateDetails = true;
    // press 1-5 in Play mode to force a state (testing only)
    public bool debugKeys = true;

    // the components and the state machine the states use
    private NavMeshAgent agent;
    private CreatureSenses senses;
    private StateMachine fsm;

    // the 5 states (created once and reused)
    private PatrolState patrol;
    private InvestigateState investigate;
    private ChaseState chase;
    private AttackState attack;
    private StunnedState stunned;

    // the time when the stun immunity ends
    private float stunImmuneUntil = 0.0f;

    // tells CreatureAnimation that an attack swing has started
    public event System.Action AttackSwing;

    // lets the states read the components and the state machine (but not change them)
    public NavMeshAgent Agent
    {
        get { return agent; }
    }

    public CreatureSenses Senses
    {
        get { return senses; }
    }

    public StateMachine Fsm
    {
        get { return fsm; }
    }

    // lets the states switch to each other
    public PatrolState Patrol
    {
        get { return patrol; }
    }

    public InvestigateState Investigate
    {
        get { return investigate; }
    }

    public ChaseState Chase
    {
        get { return chase; }
    }

    public AttackState Attack
    {
        get { return attack; }
    }

    public StunnedState Stunned
    {
        get { return stunned; }
    }

    // the name of the current state (shown by the debug label)
    public string CurrentStateName
    {
        get
        {
            if (fsm == null || fsm.CurrentState == null)
            {
                return "-";
            }
            return fsm.CurrentState.Name;
        }
    }

    // true while the creature cannot be stunned (shortly after a stun)
    public bool IsStunImmune
    {
        get { return Time.time < stunImmuneUntil; }
    }

    void Awake()
    {
        // get the components on the creature
        agent = GetComponent<NavMeshAgent>();
        senses = GetComponent<CreatureSenses>();

        // safety net in case the senses component was not added in the Inspector
        if (senses == null)
        {
            Debug.LogWarning("[Creature] No CreatureSenses component found, so one was added at runtime. Add it to the Creature prefab so its settings are saved.", this);
            senses = gameObject.AddComponent<CreatureSenses>();
        }

        // remove empty slots from the patrol list (go backwards so removing does not skip any)
        for (int i = patrolPoints.Count - 1; i >= 0; i--)
        {
            if (patrolPoints[i] == null)
            {
                patrolPoints.RemoveAt(i);
            }
        }

        // find the patrol points automatically if none were assigned
        if (patrolPoints.Count == 0)
        {
            FindPatrolPointsInScene();
        }

        // the agent must stop before it is out of attack range, or it can never attack
        if (agent.stoppingDistance >= attackRange)
        {
            Debug.LogWarning("[Creature] NavMeshAgent Stopping Distance (" + agent.stoppingDistance + ") should be smaller than Attack Range (" + attackRange + "), or the creature can't reach attack range.", this);
        }

        // create the state machine and the 5 states
        fsm = new StateMachine(this, logStateChanges, minTimeInState);
        patrol = new PatrolState(this);
        investigate = new InvestigateState(this);
        chase = new ChaseState(this);
        attack = new AttackState(this);
        stunned = new StunnedState(this);
    }

    void Start()
    {
        // the creature starts in Patrol
        fsm.ChangeState(patrol);
    }

    void Update()
    {
        // lets these be changed in the Inspector during Play mode
        fsm.logChanges = logStateChanges;
        fsm.minTimeInState = minTimeInState;

        // check the test keys
        if (debugKeys)
        {
            HandleDebugKeys();
        }

        // run the current state
        fsm.Tick();
    }

    // called by a thrown crate (IStunnable): any state -> Stunned, unless already stunned or immune
    public void Stun(float seconds)
    {
        // ignore it if the creature is not set up yet or the time is not valid
        if (fsm == null || seconds <= 0.0f)
        {
            return;
        }

        // already stunned
        if (fsm.CurrentState == stunned)
        {
            Debug.Log("[Creature] Hit while already stunned. Ignored.", this);
            return;
        }

        // still immune from the last stun
        if (IsStunImmune)
        {
            float timeLeft = stunImmuneUntil - Time.time;
            Debug.Log("[Creature] Hit, but immune to stuns for another " + timeLeft.ToString("0.0") + " s. Ignored.", this);
            return;
        }

        // stun the creature for the given time
        Debug.Log("[Creature] Stunned for " + seconds.ToString("0.0") + " s!", this);
        stunned.SetDuration(seconds);
        fsm.ChangeState(stunned);
    }

    // called by StunnedState when the stun wears off
    public void StartStunImmunity()
    {
        stunImmuneUntil = Time.time + stunImmunityTime;
        Debug.Log("[Creature] Stun over. Immune to stuns for " + stunImmunityTime.ToString("0.0") + " s.", this);
    }

    // called by AttackState when a swing starts
    public void RaiseAttackSwing()
    {
        if (AttackSwing != null)
        {
            AttackSwing();
        }
    }

    // horizontal distance to the player (height ignored), Infinity if there is no player
    public float FlatDistanceToPlayer()
    {
        if (senses == null || senses.Player == null)
        {
            return Mathf.Infinity;
        }

        Vector3 offset = senses.Player.position - transform.position;
        offset.y = 0.0f;
        return offset.magnitude;
    }

    // adds the children of the "PatrolPoints" object to the list, in Hierarchy order
    // (runs once, so GameObject.Find is fine here)
    private void FindPatrolPointsInScene()
    {
        GameObject parent = GameObject.Find(patrolPointsParentName);
        if (parent == null)
        {
            Debug.LogWarning("[Creature] No patrol points assigned and no GameObject called '" + patrolPointsParentName + "' found. The creature will stand still in Patrol.", this);
            return;
        }

        foreach (Transform child in parent.transform)
        {
            patrolPoints.Add(child);
        }

        Debug.Log("[Creature] Found " + patrolPoints.Count + " patrol points under '" + patrolPointsParentName + "'.", this);
    }

    // right-click this component > "Fill Patrol Points From Level" to see the route before pressing Play
    [ContextMenu("Fill Patrol Points From Level")]
    private void FillPatrolPointsFromLevel()
    {
#if UNITY_EDITOR
        // lets the change be undone and saved
        UnityEditor.Undo.RecordObject(this, "Fill Patrol Points");
#endif
        patrolPoints.Clear();
        FindPatrolPointsInScene();
    }

    // 1 Patrol, 2 Investigate, 3 Chase, 4 Attack, 5 Stun (number row or number pad, new Input System)
    private void HandleDebugKeys()
    {
        Keyboard keyboard = Keyboard.current;

        // no keyboard connected
        if (keyboard == null)
        {
            return;
        }

        if (Pressed(keyboard.digit1Key, keyboard.numpad1Key))
        {
            fsm.ChangeState(patrol);
        }
        if (Pressed(keyboard.digit2Key, keyboard.numpad2Key))
        {
            fsm.ChangeState(investigate);
        }
        if (Pressed(keyboard.digit3Key, keyboard.numpad3Key))
        {
            fsm.ChangeState(chase);
        }
        if (Pressed(keyboard.digit4Key, keyboard.numpad4Key))
        {
            fsm.ChangeState(attack);
        }
        if (Pressed(keyboard.digit5Key, keyboard.numpad5Key))
        {
            // goes through the immunity check, like a real crate hit
            Stun(debugStunSeconds);
        }
    }

    // true if either the number-row key or the number-pad key was pressed this frame
    private bool Pressed(KeyControl rowKey, KeyControl numpadKey)
    {
        return rowKey.wasPressedThisFrame || numpadKey.wasPressedThisFrame;
    }

    // test menu: right-click this component in Play mode
    [ContextMenu("Debug: Force Patrol")]
    private void ForcePatrol()
    {
        ForceState(patrol);
    }

    [ContextMenu("Debug: Force Investigate")]
    private void ForceInvestigate()
    {
        ForceState(investigate);
    }

    [ContextMenu("Debug: Force Chase")]
    private void ForceChase()
    {
        ForceState(chase);
    }

    [ContextMenu("Debug: Force Attack")]
    private void ForceAttack()
    {
        ForceState(attack);
    }

    [ContextMenu("Debug: Stun (like a crate hit)")]
    private void DebugStun()
    {
        if (Application.isPlaying)
        {
            Stun(debugStunSeconds);
        }
    }

    // switches state from the test menu (only in Play mode)
    private void ForceState(IState state)
    {
        if (!Application.isPlaying || fsm == null)
        {
            Debug.LogWarning("[Creature] Force State only works in Play mode.", this);
            return;
        }
        fsm.ChangeState(state);
    }

    // draws the attack range (red) and the patrol route (green) when the creature is selected
    void OnDrawGizmosSelected()
    {
        // attack range circle
        Gizmos.color = Color.red;
        DrawFlatCircle(transform.position, attackRange);

        // nothing more to draw without patrol points
        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            return;
        }

        // patrol route
        Gizmos.color = Color.green;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            // the last point links back to the first
            Transform current = patrolPoints[i];
            Transform next = patrolPoints[(i + 1) % patrolPoints.Count];
            if (current == null || next == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(current.position, 0.4f);
            Gizmos.DrawLine(current.position, next.position);
        }
    }

    // draws a circle on the ground made of short lines
    private void DrawFlatCircle(Vector3 centre, float radius)
    {
        int segments = 32;
        Vector3 previous = centre + new Vector3(radius, 0.0f, 0.0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2.0f / segments;
            Vector3 point = centre + new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
