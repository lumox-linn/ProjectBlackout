using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    public class BlackoutMagnetGlove : MonoBehaviour
    {
        public enum MagnetInputMode
        {
            Hold,
            Toggle
        }

        [Header("Magnet Control - Inspector Only")]
        [Tooltip("Hold: hold RMB to carry. Toggle: click RMB to pick up or drop.")]
        [SerializeField]
        private MagnetInputMode inputMode = MagnetInputMode.Hold;

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private GameObject gloveVisual;

        [Header("Glove Unlock")]
        [SerializeField] private bool unlockedAtStart = false;

        [Header("Targeting / Physics")]
        [Min(1f)] public float pickupRange = 9f;
        [Min(1f)] public float pullGain = 5f;
        [Min(1f)] public float followAcceleration = 12f;
        [Min(1f)] public float maxPullSpeed = 11f;
        [Min(1f)] public float maxHoldSeparation = 12f;

        [Header("Charge Throw Settings")]
        [Min(1f)] public float throwImpulse = 19f;
        [Min(1f)] public float maxThrowImpulse = 40f;
        [Min(0.1f)] public float chargeDuration = 1.6f;

        [Header("Mouse Wheel Holding Distance")]
        [Min(0.1f)] public float scrollStep = 0.45f;
        public bool reverseScrollDirection = true;

        [Min(0.5f)] public float minHoldDistance = 1.9f;
        [Min(0.5f)] public float maxHoldDistance = 5.5f;

        private BlackoutMagneticObject held;
        private Rigidbody heldBody;

        private float previousLinearDamping;
        private float previousAngularDamping;
        private bool previousGravity;

        private float holdDistance = 2.65f;

        private bool hasGlove;
        private bool isCharging;
        private float chargedSeconds;

        public BlackoutMagneticObject HeldObject => held;
        public bool IsHolding => held != null;

        /// <summary>Raised when a held object is thrown. Argument: charge 0..1 (used by the glove viewmodel).</summary>
        public event System.Action<float> Thrown;
        public float HoldDistance => holdDistance;
        public bool HasGlove => hasGlove;
        public bool IsCharging => isCharging;

        public float Charge01 =>
            Mathf.Clamp01(
                chargedSeconds / Mathf.Max(0.1f, chargeDuration)
            );

        public float CurrentThrowImpulse =>
            Mathf.Lerp(
                throwImpulse,
                Mathf.Max(throwImpulse, maxThrowImpulse),
                Charge01
            );

        public void SetReferences(
            Camera camera,
            Transform targetHoldPoint)
        {
            playerCamera = camera;
            holdPoint = targetHoldPoint;
        }

        public void SetGloveVisual(GameObject visual)
        {
            gloveVisual = visual;
        }

        public void UnlockGlove()
        {
            if (hasGlove)
            {
                return;
            }

            hasGlove = true;

            if (gloveVisual != null)
            {
                gloveVisual.SetActive(true);
            }

            Debug.Log("Magnet glove unlocked!");
        }

        private void Awake()
        {
            hasGlove = unlockedAtStart;

            if (gloveVisual != null)
            {
                gloveVisual.SetActive(hasGlove);
            }

            if (holdPoint != null)
            {
                holdDistance = Mathf.Clamp(
                    holdPoint.localPosition.z,
                    minHoldDistance,
                    maxHoldDistance
                );

                UpdateHoldPoint();
            }
        }

        private void OnValidate()
        {
            maxThrowImpulse = Mathf.Max(
                throwImpulse,
                maxThrowImpulse
            );

            chargeDuration = Mathf.Max(
                0.1f,
                chargeDuration
            );
        }

        private void Update()
        {
            // Stop the magnet outside gameplay or before unlocking.
            if (!hasGlove ||
                GameEvents.CurrentState != GameState.Playing ||
                Cursor.lockState != CursorLockMode.Locked ||
                playerCamera == null ||
                holdPoint == null)
            {
                if (held != null)
                {
                    Release(false);
                }

                ResetCharge();
                return;
            }

            // No object held: RMB attempts to pick something up.
            if (held == null)
            {
                if (RightPressed())
                {
                    TryPickUp();
                }

                return;
            }

            // HOLD mode:
            // Release RMB to drop the object.
            //
            // TOGGLE mode:
            // Click RMB again to drop the object.
            if ((inputMode == MagnetInputMode.Hold && !RightHeld()) ||
                (inputMode == MagnetInputMode.Toggle && RightPressed()))
            {
                Release(false);
                return;
            }

            // Adjust hold distance with the mouse wheel.
            float wheel = ReadWheel();

            if (Mathf.Abs(wheel) > 0.0001f)
            {
                float direction =
                    reverseScrollDirection ? 1f : -1f;

                holdDistance = Mathf.Clamp(
                    holdDistance + wheel * scrollStep * direction,
                    minHoldDistance,
                    maxHoldDistance
                );

                UpdateHoldPoint();
            }

            // Start charging when LMB is pressed.
            if (LeftPressed())
            {
                isCharging = true;
                chargedSeconds = 0f;
            }

            // Continue charging while holding LMB.
            if (isCharging && LeftHeld())
            {
                chargedSeconds = Mathf.Min(
                    chargeDuration,
                    chargedSeconds + Time.deltaTime
                );
            }

            // Throw when LMB is released.
            if (isCharging && LeftReleased())
            {
                Release(true);
            }
        }

        private void FixedUpdate()
        {
            if (held == null)
            {
                return;
            }

            if (heldBody == null)
            {
                held = null;
                ResetCharge();
                return;
            }

            if (holdPoint == null)
            {
                Release(false);
                return;
            }

            Vector3 gap =
                holdPoint.position - heldBody.position;

            if (gap.magnitude > maxHoldSeparation)
            {
                Release(false);
                return;
            }

            Vector3 desiredVelocity =
                Vector3.ClampMagnitude(
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

        private void UpdateHoldPoint()
        {
            if (holdPoint == null)
            {
                return;
            }

            Vector3 position = holdPoint.localPosition;
            position.z = holdDistance;
            holdPoint.localPosition = position;
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
                hit.collider.GetComponentInParent<
                    BlackoutMagneticObject>();

            if (target == null ||
                !target.canBePickedUp ||
                target.IsHeld)
            {
                return;
            }

            Rigidbody body = target.Body != null
                ? target.Body
                : target.GetComponent<Rigidbody>();

            if (body == null || body.isKinematic)
            {
                return;
            }

            held = target;
            heldBody = body;
            held.IsHeld = true;

            // Save original physics values.
            previousGravity = body.useGravity;
            previousLinearDamping = body.linearDamping;
            previousAngularDamping = body.angularDamping;

            // Adjust physics while carrying.
            body.useGravity = false;
            body.linearDamping = 4f;
            body.angularDamping = 6f;

            ResetCharge();
        }

        private void Release(bool throwObject)
        {
            if (heldBody != null)
            {
                // Restore original physics.
                heldBody.useGravity = previousGravity;
                heldBody.linearDamping = previousLinearDamping;
                heldBody.angularDamping = previousAngularDamping;

                if (throwObject && playerCamera != null)
                {
                    // Preserve thrown-crate impact/stun logic.
                    BlackoutImpactNoise impact =
                        heldBody.GetComponent<BlackoutImpactNoise>();

                    if (impact != null)
                    {
                        impact.MarkThrown();
                    }

                    heldBody.AddForce(
                        playerCamera.transform.forward *
                        CurrentThrowImpulse,
                        ForceMode.Impulse
                    );

                    Thrown?.Invoke(Charge01);
                }
            }

            if (held != null)
            {
                held.IsHeld = false;
            }

            held = null;
            heldBody = null;

            ResetCharge();
        }

        private void ResetCharge()
        {
            isCharging = false;
            chargedSeconds = 0f;
        }

        private void OnDisable()
        {
            if (held != null)
            {
                Release(false);
            }

            ResetCharge();
        }

        private void OnGUI()
        {
            if (!enabled ||
                GameEvents.CurrentState != GameState.Playing ||
                !isCharging ||
                held == null)
            {
                return;
            }

            float barWidth = 250f;
            float x = Screen.width / 2f - barWidth / 2f;
            float y = Screen.height - 90f;

            GUI.Box(
                new Rect(x - 10f, y - 28f, 270f, 75f),
                "THROW CHARGE: " +
                Mathf.RoundToInt(Charge01 * 100f) + "%"
            );

            GUI.Box(
                new Rect(x, y + 3f, barWidth, 20f),
                GUIContent.none
            );

            Color oldColor = GUI.color;

            GUI.color = Charge01 >= 1f
                ? Color.green
                : Color.cyan;

            GUI.DrawTexture(
                new Rect(
                    x + 2f,
                    y + 5f,
                    (barWidth - 4f) * Charge01,
                    16f
                ),
                Texture2D.whiteTexture
            );

            GUI.color = oldColor;
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

        private static bool LeftPressed()
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

        private static bool LeftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null &&
                   Mouse.current.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        private static bool LeftReleased()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null &&
                   Mouse.current.leftButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(0);
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