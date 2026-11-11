using ProjectBlackout.PlayerTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private BlackoutPlayerHealth playerHealth;
    [SerializeField] private Image[] hearts;

    private int currentHealth;
    private int maxHealth = 3;

    private void OnEnable()
    {
        GameEvents.HealthChanged += OnHealthChanged;

        if (playerHealth != null)
            OnHealthChanged(playerHealth.CurrentHealth, playerHealth.maxHealth);
    }

    private void OnDisable()
    {
        GameEvents.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        maxHealth = max;
        SetHealth(current);
    }

    public void SetHealth(int health)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);

        if (healthText != null)
            healthText.text = $"HEALTH {currentHealth}/{maxHealth}";

        for (int i = 0; i < hearts.Length; i++)
        {
            Color color = hearts[i].color;
            color.a = i < currentHealth ? 1f : 0.2f;
            hearts[i].color = color;
        }
    }
}