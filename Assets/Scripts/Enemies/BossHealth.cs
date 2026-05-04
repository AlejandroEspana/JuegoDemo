using UnityEngine;
using System;

public class BossHealth : MonoBehaviour, IDamageable
{
    [Header("Salud del Jefe")]
    [SerializeField] private int maxHealth = 500;
    private int currentHealth;

    // Eventos para desacoplar la UI de la lógica interna (Buenas Prácticas)
    public event Action<int, int> OnHealthChanged; 
    public event Action OnBossDeath;

    // Efectos de Feedback
    private SpriteRenderer sr;
    private Color originalColor;
    private Coroutine flashCoroutine;

    void Awake()
    {
        currentHealth = maxHealth;
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
    }

    void Start()
    {
        // Notificamos a la UI los valores iniciales
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Disparar evento para que la UI se actualice automáticamente
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        FlashRed();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void FlashRed()
    {
        if (sr == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        sr.color = originalColor;
    }

    private void Die()
    {
        OnBossDeath?.Invoke();
        
        // Aquí podrías agregar efectos de explosión, cámara lenta, etc.
        Destroy(gameObject, 0.5f); // Destruir temporalmente después de medio segundo
    }
}
