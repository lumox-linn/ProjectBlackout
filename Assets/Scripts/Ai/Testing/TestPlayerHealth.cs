using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// test helper: a stand-in for the real PlayerHealth, so the creature's Attack can be tested on the PlayerStandIn
// (it implements IDamageable from Core, like the real one; at 0 health it freezes the game like LOSE does)
public class TestPlayerHealth : MonoBehaviour, IDamageable
{
    // health at the start
    [SerializeField] private int maxHealth = 3;
    // the current health (shown in the Inspector during Play)
    [SerializeField] private int currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    // called by the creature's attack
    public void TakeDamage(int amount)
    {
        // already "dead"
        if (currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log("[TestPlayerHealth] Ouch! Health " + currentHealth + "/" + maxHealth, this);

        // like GameManager.LoseGame(): everything stops
        if (currentHealth == 0)
        {
            Debug.Log("[TestPlayerHealth] Player is dead: LOSE. Game frozen (right-click this component > Reset Health to carry on).", this);
            Time.timeScale = 0.0f;
        }
    }

    // right-click this component > "Reset Health" to test again without restarting
    [ContextMenu("Reset Health")]
    private void ResetHealth()
    {
        currentHealth = maxHealth;
        Time.timeScale = 1.0f;
        Debug.Log("[TestPlayerHealth] Health reset to " + maxHealth + ".", this);
    }
}
