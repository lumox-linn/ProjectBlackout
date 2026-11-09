
using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    [RequireComponent(typeof(Animator))]
    public class BlackoutCharacterAnimator : MonoBehaviour
    {
        private Animator animator;
        private CharacterController controller;
        private BlackoutFPSInput movement;
        private BlackoutPlayerHealth health;

        private bool isDead;

        private static readonly int SpeedHash =
            Animator.StringToHash("Speed");

        private static readonly int DieHash =
            Animator.StringToHash("Die");

        private void Awake()
        {
            animator = GetComponent<Animator>();

            controller =
                GetComponentInParent<CharacterController>();

            movement =
                GetComponentInParent<BlackoutFPSInput>();

            health =
                GetComponentInParent<BlackoutPlayerHealth>();
        }

        private void OnEnable()
        {
            if (health != null)
                health.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Died -= OnPlayerDied;
        }

        private void Update()
        {
            if (isDead || controller == null ||
                movement == null)
                return;

            float speed = 0f;

            if (GameEvents.CurrentState == GameState.Playing)
            {
                Vector3 velocity = controller.velocity;
                velocity.y = 0f;

                speed = Mathf.Clamp01(
                    velocity.magnitude /
                    Mathf.Max(0.1f, movement.sprintSpeed)
                );
            }

            animator.SetFloat(
                SpeedHash,
                speed,
                0.12f,
                Time.unscaledDeltaTime
            );
        }

        private void OnPlayerDied()
        {
            if (isDead)
                return;

            isDead = true;

            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
        }
    }
}
