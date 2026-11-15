using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    // Marker component, similar to the lab ChangeColour component looked up by RayShooter.
    [RequireComponent(typeof(Rigidbody))]
    public class BlackoutMagneticObject : MonoBehaviour
    {
        public bool canBePickedUp = true;
        public bool IsHeld { get; internal set; }
        public Rigidbody Body { get; private set; }

        private void Awake() { Body = GetComponent<Rigidbody>(); }
    }
}
