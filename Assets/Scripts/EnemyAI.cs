using UnityEngine;

/// <summary>
/// IA del enemigo: detecta, persigue y ataca al jugador.
/// Requiere layers "Enemy" y "Player" configurados en el proyecto.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyAI : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────

    [Header("Movimiento")]
    [SerializeField] float speed          = 2f;
    [SerializeField] float accelerationTime = 0.06f;

    [Header("Detección")]
    [SerializeField] float detectionRange = 6f;
    [SerializeField] float attackRange    = 1.2f;

    [Header("Ataque")]
    [SerializeField] int   damage         = 1;
    [SerializeField] float attackCooldown = 1f;
    [SerializeField] Transform attackPoint;
    [SerializeField] float attackRadius = 0.8f;
    [SerializeField] LayerMask playerLayerMask;

    [Header("Animación")]
    [SerializeField] Animator anim;

    // ─────────────────────────────────────────
    //  ESTADO INTERNO
    // ─────────────────────────────────────────

    enum State { Idle, Chase, Attack }
    State currentState = State.Idle;

    Transform      player;
    PlayerHealth   playerHealth;
    PlayerController playerController;

    Rigidbody2D    rb;
    SpriteRenderer sr;

    float lastAttackTime = -999f;
    float currentSpeedX;
    float stunTimer;
    bool hasDealtDamage; // Para poder funcionar con o sin Animation Events

    // Hashes del Animator
    static readonly int HashSpeedX   = Animator.StringToHash("SpeedX");
    static readonly int HashIsAttack = Animator.StringToHash("Attack");

    // ─────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        if (anim == null)
            anim = GetComponent<Animator>();
    }

    void Start()
    {
        // Buscar jugador una sola vez
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj == null)
        {
            Debug.LogWarning("[EnemyAI] No se encontró objeto con tag 'Player'.");
            return;
        }

        player            = obj.transform;
        playerHealth      = obj.GetComponent<PlayerHealth>();
        playerController  = obj.GetComponent<PlayerController>();
    }

    void Update()
    {
        if (player == null) return;

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            currentSpeedX = rb.linearVelocity.x; // Sincronizar par que no frene el empuje
            UpdateAnimations();
            return;
        }

        UpdateState();
        ExecuteState();
        UpdateAnimations();
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
    }

    // ─────────────────────────────────────────
    //  MÁQUINA DE ESTADOS
    // ─────────────────────────────────────────

    void UpdateState()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist <= attackRange)
            currentState = State.Attack;
        else
            currentState = State.Chase;
    }

    void ExecuteState()
    {
        switch (currentState)
        {
            case State.Idle:   HandleIdle();   break;
            case State.Chase:  HandleChase();  break;
            case State.Attack: HandleAttack(); break;
        }
    }

    // ─────────────────────────────────────────
    //  COMPORTAMIENTOS
    // ─────────────────────────────────────────

    void HandleIdle()
    {
        // Desacelerar suavemente hasta parar
        currentSpeedX     = Mathf.Lerp(currentSpeedX, 0f, Time.deltaTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
    }

    void HandleChase()
    {
        float dir = Mathf.Sign(player.position.x - transform.position.x);

        currentSpeedX     = Mathf.Lerp(currentSpeedX, dir * speed, Time.deltaTime / accelerationTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        // Girar sprite según dirección
        sr.flipX = dir < 0f;
    }

    void HandleAttack()
    {
        // Parar al atacar
        currentSpeedX     = Mathf.Lerp(currentSpeedX, 0f, Time.deltaTime / accelerationTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        if (Time.time < lastAttackTime + attackCooldown) return;

        // No iniciar ataque si el jugador está invulnerable (dash)
        if (playerController != null && playerController.IsInvulnerable()) return;

        lastAttackTime = Time.time;
        hasDealtDamage = false;

        if (anim != null) anim.SetTrigger(HashIsAttack);

        // Corrutina que da una ventana de 0.2s antes de atacar (Evita que requieras configurar el Animation Event)
        StartCoroutine(AttackFallbackCoroutine());
    }

    System.Collections.IEnumerator AttackFallbackCoroutine()
    {
        yield return new WaitForSeconds(0.2f);
        if (!hasDealtDamage) DealDamage();
    }

    /// <summary>
    /// Llamado por el Animation Event del enemigo en el frame de impacto, o por la corrutina si no existe evento.
    /// </summary>
    public void DealDamage()
    {
        hasDealtDamage = true; // Impedir causar doble daño

        if (attackPoint != null)
        {
            if (playerLayerMask == 0)
            {
                // Si olvidaron configurar el Layermask en el inspector, buscar usando Tag
                Collider2D[] allHits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius);
                foreach (Collider2D hit in allHits)
                {
                    if (hit.CompareTag("Player") && hit.TryGetComponent(out PlayerHealth ph))
                    {
                        Vector2 hitDirection = (hit.transform.position - transform.position).normalized;
                        ph.TakeDamage(damage, hitDirection);
                    }
                }
            }
            else
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, playerLayerMask);
                foreach (Collider2D hit in hits)
                {
                    if (hit.TryGetComponent(out PlayerHealth ph))
                    {
                        Vector2 hitDirection = (hit.transform.position - transform.position).normalized;
                        ph.TakeDamage(damage, hitDirection);
                    }
                }
            }
        }
        else
        {
            // Comportamiento antiguo por si no han configurado el Attack Point
            if (playerHealth != null)
            {
                float dist = Vector2.Distance(transform.position, player.position);
                if (dist <= attackRange)
                {
                    Vector2 hitDirection = (player.position - transform.position).normalized;
                    playerHealth.TakeDamage(damage, hitDirection);
                }
            }
        }
    }

    // ─────────────────────────────────────────
    //  ANIMACIONES
    // ─────────────────────────────────────────

    void UpdateAnimations()
    {
        if (anim == null) return;
        anim.SetFloat(HashSpeedX, Mathf.Abs(currentSpeedX));
    }

    // ─────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
    }
}