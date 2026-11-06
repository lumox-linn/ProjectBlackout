
using UnityEngine;
using TMPro;

public class HealthUI : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;

    private int currentHealth = 100;
    private int maxHealth = 100;

    private void Start()
    {
        UpdateHealthDisplay();
    }

    public void SetHealth(int health)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        UpdateHealthDisplay();
    }

    private void UpdateHealthDisplay()
    {
        healthText.text = $"HEALTH {currentHealth}/{maxHealth}";
    }
}
