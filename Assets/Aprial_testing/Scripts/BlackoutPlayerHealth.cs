
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    public class BlackoutPlayerHealth : MonoBehaviour, IDamageable
    {
        [Min(1)] public int maxHealth = 3;

        [Tooltip("Only for testing without a creature in PlayerTest scene")]
        public bool debugDamageWithH = true;

        public int CurrentHealth { get; private set; }
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
            if (amount <= 0 || IsDead)
                return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            GameEvents.RaiseHealthChanged(CurrentHealth, maxHealth);

            if (IsDead)
                Died?.Invoke();
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            GameEvents.RaiseHealthChanged(CurrentHealth, maxHealth);
        }

        private void OnGUI()
        {
            if (!debugDamageWithH)
                return;

            GUI.Box(
                new Rect(Screen.width - 178f, 12f, 166f, 60f),
                "HEALTH: " + CurrentHealth + " / " + maxHealth +
                "\nH = damage, R = reset"
            );
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
