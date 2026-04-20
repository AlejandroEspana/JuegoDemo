using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 5;
    private int currentHealth;

    private PlayerController controller;
    private SpriteRenderer sprite;

    public float blinkDuration = 1f;
    public float blinkInterval = 0.1f;
    public float knockbackForce = 6f;

    private Rigidbody2D rb;

    void Start()
    {
        currentHealth = maxHealth;
        controller = GetComponent<PlayerController>();
        sprite = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(int damage, Vector2 hitDirection = default)
    {
        // 🛡️ Invulnerabilidad (dash)
        if (controller != null && controller.IsInvulnerable())
        {
            return;
        }

        currentHealth -= damage;

        StartCoroutine(Blink());

        if (hitDirection != Vector2.zero && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode2D.Impulse);
            if (controller != null) controller.ApplyKnockbackState(0.3f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator Blink()
    {
        float elapsed = 0f;

        while (elapsed < blinkDuration)
        {
            sprite.enabled = !sprite.enabled;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        sprite.enabled = true;
    }

    void Die()
    {
        Debug.Log("Jugador murió");
    }
}