
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    public class BlackoutMagnetGlove : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform holdPoint;

        [Header("Targeting / Physics")]
        [Min(1f)] public float pickupRange = 9f;
        [Min(1f)] public float throwImpulse = 19f;
        [Min(1f)] public float pullGain = 5f;
        [Min(1f)] public float followAcceleration = 12f;
        [Min(1f)] public float maxPullSpeed = 11f;
        [Min(1f)] public float maxHoldSeparation = 12f;

        [Header("Mouse Wheel Holding Distance")]
        [Min(0.1f)] public float scrollStep = 0.45f;

        [Tooltip("Reverse the scroll-wheel movement.")]
        public bool reverseScrollDirection = true;

        [Min(0.5f)] public float minHoldDistance = 1.9f;
        [Min(0.5f)] public float maxHoldDistance = 5.5f;

        private BlackoutMagneticObject held;
        private Rigidbody heldBody;

        private float previousLinearDamping;
        private float previousAngularDamping;
        private bool previousGravity;

        private float holdDistance = 2.65f;

        public BlackoutMagneticObject HeldObject => held;
        public float HoldDistance => holdDistance;

        public void SetReferences(Camera camera, Transform targetHoldPoint)
        {
            playerCamera = camera;
            holdPoint = targetHoldPoint;
        }

        private void Awake()
        {
            if (holdPoint != null)
            {
                holdDistance = Mathf.Clamp(
                    holdPoint.localPosition.z,
                    minHoldDistance,
                    Mathf.Max(minHoldDistance, maxHoldDistance)
                );

                UpdateHoldPoint();
            }
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked ||
                playerCamera == null ||
                holdPoint == null)
            {
                if (held != null)
                    Release(false);

                return;
            }

            if (held != null)
            {
                // Drop when right-click is released.
                if (!RightHeld())
                {
                    Release(false);
                    return;
                }

                // Mouse wheel adjusts object distance.
                float wheel = ReadWheel();

                if (Mathf.Abs(wheel) > 0.0001f)
                {
                    float direction =
                        reverseScrollDirection ? 1f : -1f;

                    holdDistance = Mathf.Clamp(
                        holdDistance + wheel * scrollStep * direction,
                        minHoldDistance,
                        Mathf.Max(minHoldDistance, maxHoldDistance)
                    );

                    UpdateHoldPoint();
                }

                // Throw while holding.
                if (LeftClick())
                {
                    Release(true);
                    return;
                }
            }
            else if (RightPressed())
            {
                TryPickUp();
            }
        }

        private void UpdateHoldPoint()
        {
            if (holdPoint == null)
                return;

            Vector3 local = holdPoint.localPosition;
            local.z = holdDistance;
            holdPoint.localPosition = local;
        }

        private void FixedUpdate()
        {
            if (held == null)
                return;

            if (heldBody == null)
            {
                held = null;
                return;
            }

            Vector3 gap = holdPoint.position - heldBody.position;

            if (gap.magnitude > maxHoldSeparation)
            {
                Release(false);
                return;
            }

            Vector3 desiredVelocity = Vector3.ClampMagnitude(
                gap * pullGain,
                maxPullSpeed
            );

            Vector3 acceleration =
                (desiredVelocity - heldBody.linearVelocity)
                * followAcceleration;

            heldBody.AddForce(
                acceleration,
                ForceMode.Acceleration
            );
        }

        private void TryPickUp()
        {
            Ray ray = playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

            if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                pickupRange,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                return;
            }

            BlackoutMagneticObject target =
                hit.collider.GetComponentInParent<BlackoutMagneticObject>();

            if (target == null ||
                !target.canBePickedUp ||
                target.IsHeld)
            {
                return;
            }

            Rigidbody body =
                target.Body != null
                    ? target.Body
                    : target.GetComponent<Rigidbody>();

            if (body == null || body.isKinematic)
                return;

            held = target;
            heldBody = body;
            held.IsHeld = true;

            previousGravity = heldBody.useGravity;
            previousLinearDamping = heldBody.linearDamping;
            previousAngularDamping = heldBody.angularDamping;

            heldBody.useGravity = false;
            heldBody.linearDamping = 4f;
            heldBody.angularDamping = 6f;
        }

        private void Release(bool throwObject)
        {
            if (heldBody != null)
            {
                heldBody.useGravity = previousGravity;
                heldBody.linearDamping = previousLinearDamping;
                heldBody.angularDamping = previousAngularDamping;

                if (throwObject && playerCamera != null)
                {
                    // A4: Arm a recently thrown crate to stun IStunnable targets.
                    BlackoutImpactNoise impact =
                        heldBody.GetComponent<BlackoutImpactNoise>();

                    if (impact != null)
                        impact.MarkThrown();

                    heldBody.AddForce(
                        playerCamera.transform.forward * throwImpulse,
                        ForceMode.Impulse
                    );
                }
            }

            if (held != null)
                held.IsHeld = false;

            held = null;
            heldBody = null;
        }

        private void OnDisable()
        {
            if (held != null)
                Release(false);
        }

        private void OnGUI()
        {
            if (playerCamera == null || !enabled)
                return;

            const float crossSize = 22f;

            GUI.Label(
                new Rect(
                    Screen.width / 2f - crossSize / 2f,
                    Screen.height / 2f - crossSize / 2f,
                    crossSize,
                    crossSize
                ),
                "+"
            );

            string state = held != null
                ? "HOLDING: " + held.name +
                  " | " + holdDistance.ToString("F1") + "m"
                : "Aim at a teal metal crate; HOLD RMB to pull";

            GUI.Box(
                new Rect(12f, 12f, 425f, 107f),
                "BLACKOUT | PLAYER TEST\n" + state +
                "\nHOLD RMB = magnet | Wheel = near/far | LMB = throw" +
                "\nWASD move | Space jump | Shift sprint | F flashlight" +
                "\nEsc unlock mouse | Click to relock"
            );
        }

        private static bool RightPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null &&
                   Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        private static bool RightHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null &&
                   Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private static bool LeftClick()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null &&
                   Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private static float ReadWheel()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null
                ? Mouse.current.scroll.ReadValue().y / 120f
                : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxis("Mouse ScrollWheel") * 10f;
#else
            return 0f;
#endif
        }
    }
}
