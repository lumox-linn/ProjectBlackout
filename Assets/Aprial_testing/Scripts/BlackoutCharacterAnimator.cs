using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// Drives the Ch11 body Animator from the player's movement, the magnet glove and health.
    /// Parameters (created by Tools > Project Blackout > Setup Player Animator):
    ///   Speed    (float 0..1)  overall horizontal speed, kept for compatibility
    ///   Forward  (float -1..1) -1 = walking back, 0 = idle, 0.5 = walk, 1 = sprint
    ///   Carrying (bool)        a crate is held by the magnet -> "Standing" carry animations
    ///   Boosted  (bool)        after-hit speed boost -> temp-Running
    ///   Die      (trigger)
    /// Any parameter missing from the controller is simply skipped.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class BlackoutCharacterAnimator : MonoBehaviour
    {
        [Header("Smoothing")]
        [Min(0f)] public float dampTime = 0.12f;

        [Header("Hit Boost Animation")]
        [Tooltip("Extra playback speed during the after-hit boost (1 = normal speed).")]
        [Min(1f)] public float boostAnimationSpeed = 1f;

        private Animator animator;
        private CharacterController controller;
        private BlackoutFPSInput movement;
        private BlackoutPlayerHealth health;
        private BlackoutMagnetGlove glove;

        private bool isDead;
        private bool hasSpeed, hasForward, hasCarrying, hasBoosted, hasDie, hasHit;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int ForwardHash = Animator.StringToHash("Forward");
        private static readonly int CarryingHash = Animator.StringToHash("Carrying");
        private static readonly int BoostedHash = Animator.StringToHash("Boosted");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        private void Awake()
        {
            animator = GetComponent<Animator>();

            controller = GetComponentInParent<CharacterController>();
            movement = GetComponentInParent<BlackoutFPSInput>();
            health = GetComponentInParent<BlackoutPlayerHealth>();
            glove = GetComponentInParent<BlackoutMagnetGlove>();
            if (glove == null)
                glove = transform.root.GetComponentInChildren<BlackoutMagnetGlove>(true);

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == SpeedHash) hasSpeed = true;
                else if (p.nameHash == ForwardHash) hasForward = true;
                else if (p.nameHash == CarryingHash) hasCarrying = true;
                else if (p.nameHash == BoostedHash) hasBoosted = true;
                else if (p.nameHash == DieHash) hasDie = true;
                else if (p.nameHash == HitHash && p.type == AnimatorControllerParameterType.Trigger) hasHit = true;
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += OnPlayerDied;
                health.Damaged += OnPlayerHit;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnPlayerDied;
                health.Damaged -= OnPlayerHit;
            }
        }

        private void Update()
        {
            if (isDead || controller == null || movement == null)
                return;

            float speed = 0f;
            float forward = 0f;

            if (GameEvents.CurrentState == GameState.Playing)
            {
                Vector3 velocity = controller.velocity;
                velocity.y = 0f;
                float mag = velocity.magnitude;

                float walk = Mathf.Max(0.1f, movement.walkSpeed);
                float sprint = Mathf.Max(walk + 0.01f, movement.sprintSpeed);

                speed = Mathf.Clamp01(mag / sprint);

                float localZ = movement.transform.InverseTransformDirection(velocity).z;
                bool movingBack = localZ < -0.1f;

                if (movingBack)
                    forward = -Mathf.Clamp01(mag / walk);
                else if (mag <= walk)
                    forward = 0.5f * (mag / walk);
                else
                    forward = 0.5f + 0.5f * Mathf.Clamp01((mag - walk) / (sprint - walk));
            }

            float dt = Time.unscaledDeltaTime;
            if (hasSpeed) animator.SetFloat(SpeedHash, speed, dampTime, dt);
            if (hasForward) animator.SetFloat(ForwardHash, forward, dampTime, dt);
            if (hasCarrying) animator.SetBool(CarryingHash, glove != null && glove.IsHolding);
            if (hasBoosted) animator.SetBool(BoostedHash, movement.IsBoosted);

            animator.speed = movement.IsBoosted
                ? Mathf.Lerp(1f, boostAnimationSpeed, movement.BoostAmount)
                : 1f;
        }

        private void OnPlayerHit()
        {
            if (!isDead && hasHit)
                animator.SetTrigger(HitHash);
        }

        private void OnPlayerDied()
        {
            if (isDead)
                return;

            isDead = true;
            animator.speed = 1f;

            if (hasSpeed) animator.SetFloat(SpeedHash, 0f);
            if (hasForward) animator.SetFloat(ForwardHash, 0f);
            if (hasBoosted) animator.SetBool(BoostedHash, false);
            if (hasDie) animator.SetTrigger(DieHash);
        }
    }
}
