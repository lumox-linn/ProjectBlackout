using UnityEngine;

// ---------------------------------------------------------------------------
// TestPlayerHealth  (B6 test helper, TEMPORARY)
//
// A stand-in for Eliza's real PlayerHealth (C9), so the creature's Attack can
// be tested on the PlayerStandIn capsule. It implements the shared
// IDamageable interface from Core, exactly like the real one will.
// Put it on PlayerStandIn. Remove it once the real player is merged.
//
// At 0 health it freezes the game (Time.timeScale = 0), the same way the
// real GameManager does on LOSE, so the creature stops attacking.
// ---------------------------------------------------------------------------
public class TestPlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 3;

    [Tooltip("Read-only during Play: the current health.")]
    [SerializeField] private int currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0)
        {
            return; // already "dead"
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"[TestPlayerHealth] Ouch! Health {currentHealth}/{maxHealth}", this);

        if (currentHealth == 0)
        {
            Debug.Log("[TestPlayerHealth] Player is dead: LOSE. Game frozen " +
                      "(right-click this component > Reset Health to carry on).", this);
            Time.timeScale = 0f; // like GameManager.LoseGame(): everything stops
        }
    }

    // Right-click the component header > "Reset Health" to test again without restarting.
    [ContextMenu("Reset Health")]
    private void ResetHealth()
    {
        currentHealth = maxHealth;
        Time.timeScale = 1f; // unfreeze
        Debug.Log($"[TestPlayerHealth] Health reset to {maxHealth}.", this);
    }
}