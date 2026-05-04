using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("Configuración")]
    [SerializeField] private string bossName = "Guardián del Nivel 1";

    void OnEnable()
    {
        if (bossHealth != null)
        {
            // Nos suscribimos a los eventos del jefe
            bossHealth.OnHealthChanged += UpdateHealthBar;
            bossHealth.OnBossDeath += HandleBossDeath;
        }

        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }
    }

    void OnDisable()
    {
        if (bossHealth != null)
        {
            // Siempre es importante desuscribirse para evitar Memory Leaks
            bossHealth.OnHealthChanged -= UpdateHealthBar;
            bossHealth.OnBossDeath -= HandleBossDeath;
        }
    }

    private void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (healthSlider == null) return;

        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
    }

    private void HandleBossDeath()
    {
        // Ocultar la barra de vida cuando el jefe muere
        gameObject.SetActive(false);
    }
}
