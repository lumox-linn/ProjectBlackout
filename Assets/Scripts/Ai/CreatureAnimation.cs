using UnityEngine;

// ---------------------------------------------------------------------------
// CreatureAnimation  (B9)
//
// Connects the AI to the Animator on the creature's model. The AI never
// talks to the Animator directly; this script watches the AI and sets
// Animator parameters (built by Tools > Creature > Set Up Mixamo Creature):
//
//   Speed       (float)   = how fast the NavMeshAgent is moving
//                           -> Locomotion blend tree: 0 Idle, 2 Walk, 4.5 Run
//   Attack      (trigger) = an attack swing started (CreatureController.AttackSwing)
//   Stunned     (bool)    = the FSM is in the Stunned state
//   LookDirection (int)   = Investigate's look-around: 1 = LookRight clip,
//                           -1 = LookLeft clip, 0 = not looking around.
//                           Synced with the body sweep (right first, then left).
//
// Optional procedural head turn (off by default): turns the chest, neck and
// head a little further than the body in LateUpdate, AFTER the Animator has
// posed the skeleton. Use it if there are no LookLeft/LookRight clips.
//
// Put it on the Creature root. The Animator goes on the model child.
// Character and animations: Mixamo (Adobe), https://www.mixamo.com
// ---------------------------------------------------------------------------
[RequireComponent(typeof(CreatureController))]
public class CreatureAnimation : MonoBehaviour
{
    [Tooltip("The Animator on the model child (found automatically if left empty).")]
    [SerializeField] private Animator animator;

    [Tooltip("Smooths the Speed parameter so walk/run blend gently (seconds).")]
    [SerializeField] private float speedDampTime = 0.1f;

    [Header("Look around (optional procedural head turn)")]
    [Tooltip("Turn the head/chest in code while looking around. " +
             "Leave OFF when using the LookLeft/LookRight animations.")]
    [SerializeField] private bool useProceduralHeadTurn = false;

    [Tooltip("How far the head turns AHEAD of the body (degrees), shared between chest, neck and head.")]
    [SerializeField] private float headLeadAngle = 45f;

    [Tooltip("How the turn is shared out. The rest goes to the head.")]
    [Range(0f, 1f)] [SerializeField] private float chestShare = 0.25f;
    [Range(0f, 1f)] [SerializeField] private float neckShare = 0.3f;

    [Tooltip("How fast the head turn fades in and out (per second).")]
    [SerializeField] private float lookBlendSpeed = 4f;

    // Hashing the names once is faster than passing strings every frame.
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int StunnedHash = Animator.StringToHash("Stunned");
    private static readonly int LookDirectionHash = Animator.StringToHash("LookDirection");

    private CreatureController creature;
    private bool subscribed;

    private Transform chestBone;
    private Transform neckBone;
    private Transform headBone;
    private float lookWeight; // 0 = no extra head turn, 1 = full
    private float lastTurnDirection; // kept while fading out, so the head eases back instead of snapping

    private void Awake()
    {
        creature = GetComponent<CreatureController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning("[CreatureAnimation] No Animator found on the model. Animations are off.", this);
            enabled = false;
            return;
        }

        // The NavMeshAgent moves the creature, not the animation.
        animator.applyRootMotion = false;

        // Humanoid rigs let us ask for bones by body part, whatever they're called.
        if (animator.isHuman)
        {
            chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    // Start, not Awake: CreatureController creates its state machine in its own Awake,
    // and Unity doesn't promise which Awake runs first.
    private void Start()
    {
        creature.Fsm.StateChanged += OnStateChanged;
        creature.AttackSwing += OnAttackSwing;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed)
        {
            creature.Fsm.StateChanged -= OnStateChanged;
            creature.AttackSwing -= OnAttackSwing;
        }
    }

    private void Update()
    {
        animator.SetFloat(SpeedHash, creature.Agent.velocity.magnitude, speedDampTime, Time.deltaTime);
        animator.SetInteger(LookDirectionHash, creature.Investigate.LookSide);
    }

    // Runs after the Animator has posed the skeleton this frame.
    private void LateUpdate()
    {
        // Fade the extra turn in when looking around starts, and out when it ends.
        bool lookingAround = useProceduralHeadTurn && creature.Investigate.IsLookingAround;
        if (lookingAround)
        {
            lastTurnDirection = creature.Investigate.LookTurnDirection;
        }

        float targetWeight = lookingAround ? 1f : 0f;
        lookWeight = Mathf.MoveTowards(lookWeight, targetWeight, lookBlendSpeed * Time.deltaTime);
        if (lookWeight <= 0f)
        {
            return;
        }

        // Positive = right, negative = left (same as the body's turn).
        float totalAngle = lastTurnDirection * headLeadAngle * lookWeight;
        float headShare = Mathf.Max(0f, 1f - chestShare - neckShare);

        // Parent first: turning the chest also carries the neck and head with it,
        // so the angles add up along the spine.
        TurnBone(chestBone, totalAngle * chestShare);
        TurnBone(neckBone, totalAngle * neckShare);
        TurnBone(headBone, totalAngle * headShare);
    }

    // Turns a bone around the creature's up axis (left/right), on top of its animated pose.
    private void TurnBone(Transform bone, float angle)
    {
        if (bone == null)
        {
            return;
        }
        bone.rotation = Quaternion.AngleAxis(angle, transform.up) * bone.rotation;
    }

    private void OnStateChanged(IState oldState, IState newState)
    {
        animator.SetBool(StunnedHash, newState == creature.Stunned);
    }

    private void OnAttackSwing()
    {
        animator.SetTrigger(AttackHash);
    }
}
