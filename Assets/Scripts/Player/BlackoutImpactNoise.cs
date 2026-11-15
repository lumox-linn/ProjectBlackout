
using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    // Sends collision noise to Linn's shared NoiseSystem.
    // Recently thrown crates can also stun creatures implementing IStunnable.
    [RequireComponent(typeof(Rigidbody))]
    public class BlackoutImpactNoise : MonoBehaviour
    {
        // Preserved for existing listeners in the PlayerTest scene.
        public static event System.Action<Vector3, float> ImpactMade;

        [Header("Impact Noise")]
        [Min(0f)] public float minimumSpeed = 2.0f;
        [Min(0f)] public float cooldownSeconds = 0.3f;

        [Header("Thrown Crate Stun")]
        [Min(0f)] public float minimumStunSpeed = 6f;
        [Min(0f)] public float stunDuration = 3f;
        [Min(0.1f)] public float stunWindowSeconds = 3f;

        private float nextNoiseTime;
        private float stunArmedUntil = float.NegativeInfinity;
        private BlackoutMagneticObject magnetic;

        private void Awake()
        {
            magnetic = GetComponent<BlackoutMagneticObject>();
        }

        // Called ONLY when the magnet glove throws (not when dropping).
        public void MarkThrown()
        {
            stunArmedUntil = Time.time + stunWindowSeconds;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Avoid generating sound or stuns from held crates.
            if (magnetic != null && magnetic.IsHeld)
                return;

            float speed = collision.relativeVelocity.magnitude;

            // A4: A sufficiently forceful impact by a recently thrown crate
            // can stun the creature once; ordinary collisions cannot.
            // Check this independently of noise cooldown to avoid missed stuns.
            if (Time.time <= stunArmedUntil && speed >= minimumStunSpeed)
            {
                IStunnable target =
                    collision.collider.GetComponentInParent<IStunnable>();

                if (target != null)
                {
                    stunArmedUntil = float.NegativeInfinity;
                    target.Stun(stunDuration);
                }
            }

            // A3: Gentle contacts and rapid repeated impacts are silent.
            if (speed < minimumSpeed || Time.time < nextNoiseTime)
                return;

            nextNoiseTime = Time.time + cooldownSeconds;

            Vector3 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;

            NoiseSystem.Emit(
                point,
                Mathf.Clamp(speed * 2f, 5f, 20f),
                gameObject
            );

            // Retain the original PlayerTest event for existing listeners.
            ImpactMade?.Invoke(point, speed);
        }
    }
}
