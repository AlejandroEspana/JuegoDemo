using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class BossAI : MonoBehaviour
{
    private enum BossState { Idle, Telegraphing, Attacking }

    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform meleePoint;

    [Header("Configuración de Estados")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float idealDistance = 6f; // Distancia preferida
    
    [Header("Ataque Melee (Corto Alcance)")]
    [SerializeField] private float meleeRange = 2.5f;
    [SerializeField] private float meleeDamage = 20f;
    [SerializeField] private float meleeCooldown = 3f;
    [SerializeField] private float meleeTelegraphTime = 0.8f;

    [Header("Embestida (Dash)")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.5f;
    [SerializeField] private float dashDamage = 30f;
    [SerializeField] private float dashCooldown = 6f;
    [SerializeField] private float dashTelegraphTime = 1f;

    [Header("Ataque a Distancia")]
    [SerializeField] private float rangedCooldown = 5f;
    [SerializeField] private float rangedTelegraphTime = 0.5f;

    // Hashes de Animación (Optimizados y Seguros)
    private readonly int hashIdle = Animator.StringToHash("Idle");
    private readonly int hashAttack = Animator.StringToHash("Attack");
    private readonly int hashDash = Animator.StringToHash("Dash");
    private readonly int hashDistanceAttack = Animator.StringToHash("DistanceAttack");
    private readonly int hashTelegraph = Animator.StringToHash("Telegraph"); // Opcional, si tienes animación de aviso

    // Componentes y Estado
    private Rigidbody2D rb;
    private Animator anim;
    private BossState currentState = BossState.Idle;

    // Temporizadores
    private float lastMeleeTime = -10f;
    private float lastDashTime = -10f;
    private float lastRangedTime = -10f;

    // Bandera para daño por contacto durante el Dash
    private bool isDashing = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        
        // Auto-buscar al jugador si no se asignó en el inspector
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        // Solo pensar qué hacer si está inactivo
        if (currentState == BossState.Idle)
        {
            LookAtPlayer();
            DecideNextMove();
        }
    }

    private void DecideNextMove()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // 1. ¿Está lo suficientemente cerca para un Melee y no está en cooldown?
        if (distanceToPlayer <= meleeRange && Time.time >= lastMeleeTime + meleeCooldown)
        {
            StartCoroutine(MeleeRoutine());
            return;
        }

        // 2. ¿Está a distancia media/larga y el Dash está listo?
        if (distanceToPlayer > meleeRange && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(DashRoutine());
            return;
        }

        // 3. ¿Está lejos y el ataque a distancia está listo?
        if (distanceToPlayer >= idealDistance && Time.time >= lastRangedTime + rangedCooldown)
        {
            StartCoroutine(RangedRoutine());
            return;
        }

        // 4. Si todo está en cooldown, moverse lentamente o esperar
        HandleIdleMovement(distanceToPlayer);
    }

    private void LookAtPlayer()
    {
        if (player.position.x > transform.position.x)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    private void HandleIdleMovement(float distance)
    {
        anim.SetBool(hashIdle, true);

        // Si está más lejos de lo ideal, acercarse lentamente (sin pasarse del rango melee)
        if (distance > idealDistance)
        {
            Vector2 targetPos = new Vector2(player.position.x, rb.linearVelocity.y);
            transform.position = Vector2.MoveTowards(transform.position, new Vector2(player.position.x, transform.position.y), walkSpeed * Time.deltaTime);
        }
    }

    // --- CORRUTINAS DE ATAQUE (MÁQUINA DE ESTADOS) ---

    private IEnumerator MeleeRoutine()
    {
        currentState = BossState.Telegraphing;
        anim.SetBool(hashIdle, false);
        anim.SetTrigger(hashTelegraph); // Activar animación de aviso si existe

        rb.linearVelocity = Vector2.zero; // Se detiene completamente para avisar
        yield return new WaitForSeconds(meleeTelegraphTime);

        currentState = BossState.Attacking;
        anim.SetTrigger(hashAttack); // Animación del golpe
        lastMeleeTime = Time.time;

        // Validar daño en el frame del ataque usando un OverlapCircle
        Collider2D hit = Physics2D.OverlapCircle(meleePoint != null ? meleePoint.position : transform.position, meleeRange, LayerMask.GetMask("Player"));
        if (hit != null)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null) damageable.TakeDamage((int)meleeDamage);
        }

        // Recuperación post-ataque
        yield return new WaitForSeconds(0.5f);
        currentState = BossState.Idle;
    }

    private IEnumerator DashRoutine()
    {
        currentState = BossState.Telegraphing;
        anim.SetBool(hashIdle, false);
        anim.SetTrigger(hashTelegraph);

        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(dashTelegraphTime);

        currentState = BossState.Attacking;
        anim.SetBool(hashDash, true); // Animación de embestida continua
        lastDashTime = Time.time;
        isDashing = true; // Activa daño por contacto

        // Aplicar fuerza de empuje en la dirección en la que mira
        float direction = Mathf.Sign(transform.localScale.x);
        rb.linearVelocity = new Vector2(direction * dashSpeed, rb.linearVelocity.y);

        yield return new WaitForSeconds(dashDuration);

        // Frenar
        rb.linearVelocity = Vector2.zero;
        isDashing = false;
        anim.SetBool(hashDash, false);
        
        yield return new WaitForSeconds(0.5f); // Tiempo de recuperación después del dash
        currentState = BossState.Idle;
    }

    private IEnumerator RangedRoutine()
    {
        currentState = BossState.Telegraphing;
        anim.SetBool(hashIdle, false);
        anim.SetTrigger(hashTelegraph);

        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(rangedTelegraphTime);

        currentState = BossState.Attacking;
        anim.SetTrigger(hashDistanceAttack);
        lastRangedTime = Time.time;

        if (projectilePrefab != null && firePoint != null)
        {
            Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        }

        yield return new WaitForSeconds(0.5f);
        currentState = BossState.Idle;
    }

    // --- MANEJO DE COLISIONES ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Daño por contacto directo solo si está haciendo el Dash
        if (isDashing && collision.gameObject.CompareTag("Player"))
        {
            IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage((int)dashDamage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Dibujar el rango Melee en el editor para configurarlo fácilmente
        Gizmos.color = Color.red;
        if (meleePoint != null)
        {
            Gizmos.DrawWireSphere(meleePoint.position, meleeRange);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, meleeRange);
        }
    }
}
