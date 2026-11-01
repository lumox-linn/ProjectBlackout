using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    // Lightweight integration hook for the creature AI teammate. Does not implement AI.
    [RequireComponent(typeof(Rigidbody))]
    public class BlackoutImpactNoise : MonoBehaviour
    {
        // Min's future AI can subscribe to ImpactMade and decide whether to investigate.
        // Arguments: impact world position, approximate loudness (relative impact speed).
        public static event System.Action<Vector3, float> ImpactMade;

        [Min(0f)] public float minimumSpeed = 2.0f;
        [Min(0f)] public float cooldownSeconds = 0.3f;
        private float nextNoiseTime;
        private BlackoutMagneticObject magnetic;

        private void Awake() { magnetic = GetComponent<BlackoutMagneticObject>(); }

        private void OnCollisionEnter(Collision collision)
        {
            if (magnetic != null && magnetic.IsHeld) return;
            if (Time.time < nextNoiseTime) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minimumSpeed) return;

            nextNoiseTime = Time.time + cooldownSeconds;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            ImpactMade?.Invoke(point, speed);
        }
    }
}
