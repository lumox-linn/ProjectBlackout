using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// connects the AI to the Animator on the creature's model
// (the states never touch the Animator; this script watches the AI and sets the Animator parameters)
// Speed (float): how fast the agent moves -> Locomotion blend tree (0 Idle, 2 Walk, 4.5 Run)
// Attack (trigger): an attack swing started
// Stunned (bool): the creature is in the Stunned state
// LookDirection (int): 1 = LookRight, -1 = LookLeft, 0 = not looking around
// character and animations: Mixamo (Adobe), https://www.mixamo.com
[RequireComponent(typeof(CreatureController))]
public class CreatureAnimation : MonoBehaviour
{
    // the Animator on the model child (found automatically if left empty)
    [SerializeField] private Animator animator;
    // smooths the Speed parameter so walk/run/idle blend gently (seconds)
    public float speedDampTime = 0.25f;

    [Header("Look around (optional procedural head turn)")]
    // turn the head/chest in code while looking around (leave off when using the LookLeft/LookRight animations)
    public bool useProceduralHeadTurn = false;
    // how far the head turns ahead of the body (degrees), shared between chest, neck and head
    public float headLeadAngle = 45.0f;
    // how the turn is shared out (the rest goes to the head)
    public float chestShare = 0.25f;
    public float neckShare = 0.3f;
    // how fast the head turn fades in and out (per second)
    public float lookBlendSpeed = 4.0f;

    // the Animator parameters (hashing the names once is faster than using strings every frame)
    private static int speedHash = Animator.StringToHash("Speed");
    private static int attackHash = Animator.StringToHash("Attack");
    private static int stunnedHash = Animator.StringToHash("Stunned");
    private static int lookDirectionHash = Animator.StringToHash("LookDirection");

    // the creature the animations follow
    private CreatureController creature;
    // whether we are listening to the creature's events
    private bool subscribed = false;

    // the bones turned by the procedural head turn
    private Transform chestBone;
    private Transform neckBone;
    private Transform headBone;
    // 0 = no extra head turn, 1 = full
    private float lookWeight = 0.0f;
    // kept while fading out, so the head eases back instead of snapping
    private float lastTurnDirection = 0.0f;

    void Awake()
    {
        creature = GetComponent<CreatureController>();

        // find the Animator on the model
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // no Animator: turn this script off
        if (animator == null)
        {
            Debug.LogWarning("[CreatureAnimation] No Animator found on the model. Animations are off.", this);
            enabled = false;
            return;
        }

        // the NavMeshAgent moves the creature, not the animation
        animator.applyRootMotion = false;

        // humanoid rigs let us ask for bones by body part, whatever they are called
        if (animator.isHuman)
        {
            chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    // Start, not Awake: CreatureController creates its state machine in its own Awake,
    // and Unity does not promise which Awake runs first
    void Start()
    {
        creature.Fsm.StateChanged += OnStateChanged;
        creature.AttackSwing += OnAttackSwing;
        subscribed = true;

        // tell the creature how long the attack animation is, so it waits for it to finish
        SetAttackDurationFromClip();
    }

    // finds the "Attack" clip in the Animator Controller and copies its length to the creature
    private void SetAttackDurationFromClip()
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name == "Attack")
            {
                creature.attackDuration = clip.length;
                Debug.Log("[CreatureAnimation] Attack animation is " + clip.length.ToString("0.00") + " s long.", this);
                return;
            }
        }
    }

    // stop listening when the creature is destroyed
    void OnDestroy()
    {
        if (subscribed)
        {
            creature.Fsm.StateChanged -= OnStateChanged;
            creature.AttackSwing -= OnAttackSwing;
        }
    }

    void Update()
    {
        // walk/run blend follows the agent's real speed
        animator.SetFloat(speedHash, creature.Agent.velocity.magnitude, speedDampTime, Time.deltaTime);

        // look right / look left while investigating
        animator.SetInteger(lookDirectionHash, creature.Investigate.LookSide);
    }

    // runs after the Animator has posed the skeleton this frame
    void LateUpdate()
    {
        // fade the extra turn in when looking around starts, and out when it ends
        bool lookingAround = useProceduralHeadTurn && creature.Investigate.IsLookingAround;
        if (lookingAround)
        {
            lastTurnDirection = creature.Investigate.LookTurnDirection;
        }

        float targetWeight = lookingAround ? 1.0f : 0.0f;
        lookWeight = Mathf.MoveTowards(lookWeight, targetWeight, lookBlendSpeed * Time.deltaTime);
        if (lookWeight <= 0.0f)
        {
            return;
        }

        // positive = right, negative = left (same as the body's turn)
        float totalAngle = lastTurnDirection * headLeadAngle * lookWeight;
        float headShare = Mathf.Max(0.0f, 1.0f - chestShare - neckShare);

        // parent first: turning the chest also carries the neck and head, so the angles add up along the spine
        TurnBone(chestBone, totalAngle * chestShare);
        TurnBone(neckBone, totalAngle * neckShare);
        TurnBone(headBone, totalAngle * headShare);
    }

    // turns a bone left/right around the creature's up axis, on top of its animated pose
    private void TurnBone(Transform bone, float angle)
    {
        if (bone == null)
        {
            return;
        }
        bone.rotation = Quaternion.AngleAxis(angle, transform.up) * bone.rotation;
    }

    // called by the state machine on every state change
    private void OnStateChanged(IState oldState, IState newState)
    {
        animator.SetBool(stunnedHash, newState == creature.Stunned);
    }

    // called by CreatureController when an attack swing starts
    private void OnAttackSwing()
    {
        animator.SetTrigger(attackHash);
    }
}
