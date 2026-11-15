
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    public class BlackoutPlayerHealth : MonoBehaviour, IDamageable
    {
        [Min(1)] public int maxHealth = 3;

        [Tooltip("TESTING ONLY: H = take 1 damage, R = reset health. Keep OFF for the final build.")]
        public bool debugDamageWithH = false;

        [Header("Hit Feedback")]
        [Tooltip("Seconds after a hit where the player can't be hurt again (gives time to run).")]
        [Min(0f)] public float invulnerableAfterHit = 1.0f;

        public int CurrentHealth { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        private float invulnerableUntil;

        /// <summary>Raised when the player takes damage but is still alive (used for the speed boost).</summary>
        public event System.Action Damaged;
        public bool IsDead => CurrentHealth <= 0;

        public event System.Action<int, int> HealthChanged;
        public event System.Action Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        private void Start()
        {
            GameEvents.RaiseHealthChanged(CurrentHealth, maxHealth);
        }

        private void Update()
        {
            if (!debugDamageWithH ||
                Cursor.lockState != CursorLockMode.Locked)
                return;

            if (HPressed())
                TakeDamage(1);

            if (RPressed())
                ResetHealth();
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || IsInvulnerable)
                return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            GameEvents.RaiseHealthChanged(CurrentHealth, maxHealth);

            if (IsDead)
            {
                Died?.Invoke();
            }
            else
            {
                invulnerableUntil = Time.time + invulnerableAfterHit;
                Damaged?.Invoke();
            }
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            invulnerableUntil = 0f;

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            GameEvents.RaiseHealthChanged(CurrentHealth, maxHealth);
        }

        private static bool HPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   Keyboard.current.hKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.H);
#else
            return false;
#endif
        }

        private static bool RPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   Keyboard.current.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }
    }
}
