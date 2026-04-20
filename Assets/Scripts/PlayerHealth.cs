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

    void Start()
    {
        currentHealth = maxHealth;
        controller = GetComponent<PlayerController>();
        sprite = GetComponent<SpriteRenderer>();
    }

    public void TakeDamage(int damage)
    {
        if (controller != null && controller.IsInvulnerable())
        {
            return;
        }

        currentHealth -= damage;

        StartCoroutine(Blink());

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