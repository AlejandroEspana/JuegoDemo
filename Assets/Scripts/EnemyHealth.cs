using UnityEngine;
using System.Collections;

/// <summary>
/// Gestiona la vida del enemigo, feedback visual al recibir daño y muerte.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyHealth : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────

    [Header("Vida")]
    [SerializeField] int maxHealth = 3;

    [Header("Feedback de daño")]
    [SerializeField] float blinkDuration = 0.3f;
    [SerializeField] float blinkInterval = 0.05f;
    [SerializeField] Color damageColor   = Color.red;     // tinte al recibir daño
    [SerializeField] float knockbackForce = 4f;           // empujón al recibir golpe

    [Header("Muerte")]
    [SerializeField] GameObject deathVFXPrefab;           // opcional: partículas al morir
    [SerializeField] float deathDelay = 0f;               // tiempo antes de destruir

    // ─────────────────────────────────────────
    //  ESTADO INTERNO
    // ─────────────────────────────────────────

    int currentHealth;
    bool isDead;

    SpriteRenderer sr;
    Rigidbody2D    rb;
    Color          originalColor;

    WaitForSeconds waitBlink;

    // ─────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();   // opcional, puede ser null

        originalColor  = sr.color;
        currentHealth  = maxHealth;
        waitBlink      = new WaitForSeconds(blinkInterval);
    }

    // ─────────────────────────────────────────
    //  API PÚBLICA
    // ─────────────────────────────────────────

    /// <summary>
    /// Aplica daño al enemigo. Llamado por el Animation Event del jugador.
    /// </summary>
    public void TakeDamage(int damage, Vector2 hitDirection = default)
    {
        if (isDead) return;

        currentHealth -= damage;

        StartCoroutine(DamageFeedback());

        if (hitDirection != Vector2.zero)
            ApplyKnockback(hitDirection);

        if (currentHealth <= 0)
            StartCoroutine(Die());
    }

    public int  GetCurrentHealth() => currentHealth;
    public int  GetMaxHealth()     => maxHealth;
    public bool IsDead()           => isDead;

    // ─────────────────────────────────────────
    //  FEEDBACK VISUAL
    // ─────────────────────────────────────────

    IEnumerator DamageFeedback()
    {
        float timer = 0f;

        while (timer < blinkDuration)
        {
            // Alternar entre color de daño y color original
            sr.color   = sr.color == originalColor ? damageColor : originalColor;
            sr.enabled = !sr.enabled;

            yield return waitBlink;
            timer += blinkInterval;
        }

        // Asegurar estado visual correcto al terminar
        sr.color   = originalColor;
        sr.enabled = true;
    }

    void ApplyKnockback(Vector2 direction)
    {
        if (rb == null) return;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction.normalized * knockbackForce, ForceMode2D.Impulse);

        if (TryGetComponent(out EnemyAI ai))
        {
            ai.ApplyStun(blinkDuration); // Aturdir el mismo tiempo que parpadea
        }
    }

    // ─────────────────────────────────────────
    //  MUERTE
    // ─────────────────────────────────────────

    IEnumerator Die()
    {
        isDead = true;

        // Detener movimiento
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Spawner de VFX opcional
        if (deathVFXPrefab != null)
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);

        if (deathDelay > 0f)
            yield return new WaitForSeconds(deathDelay);

        Destroy(gameObject);
    }
}