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

    [Header("Patrullaje y Detección (Bordes/Paredes)")]
    [SerializeField] float patrolSpeed = 1f;
    [SerializeField] float edgeCheckOffsetX = 0.5f; // Distancia adelante para detectar
    [SerializeField] float edgeCheckDistance = 1.2f; // Profundidad del rayo
    [SerializeField] LayerMask groundLayer;

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

    float patrolDirection = 1f; // 1 = Derecha, -1 = Izquierda

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
        // En lugar de estar completamente quietos, patrullarán a menor velocidad
        currentSpeedX = Mathf.Lerp(currentSpeedX, patrolDirection * patrolSpeed, Time.deltaTime / accelerationTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        bool isFacingLeft = patrolDirection < 0f;
        SetFacing(isFacingLeft);

        CheckLedgeOrWall();
    }

    void HandleChase()
    {
        float dir = Mathf.Sign(player.position.x - transform.position.x);

        currentSpeedX     = Mathf.Lerp(currentSpeedX, dir * speed, Time.deltaTime / accelerationTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        bool isFacingLeft = dir < 0f;
        SetFacing(isFacingLeft);

        CheckLedgeOrWall();
    }

    void CheckLedgeOrWall()
    {
        // Si no se configuró la Layer de Tierra en Unity, desactivamos el límite para no romper nada
        if (groundLayer == 0) return;

        // Elevamos el origen de los rayos un poco (0.2f) para que el rayo "Wall" no asuma que 
        // el piso en el que estamos parados es una pared.
        Vector2 pos = (Vector2)transform.position + new Vector2(0f, 0.2f);
        float dirX = sr.flipX ? -1f : 1f; 
        Vector2 frontPos = pos + new Vector2(dirX * edgeCheckOffsetX, 0);

        // Rayos
        RaycastHit2D groundInfo = Physics2D.Raycast(frontPos, Vector2.down, edgeCheckDistance, groundLayer);
        RaycastHit2D wallInfo = Physics2D.Raycast(pos, new Vector2(dirX, 0), edgeCheckOffsetX + 0.1f, groundLayer);

        bool edgeDetected = (groundInfo.collider == null); // No hay suelo adelante
        bool wallDetected = (wallInfo.collider != null && !wallInfo.collider.isTrigger && !wallInfo.collider.CompareTag("Player") && !wallInfo.collider.CompareTag("Enemy"));

        if (edgeDetected || wallDetected)
        {
            if (currentState == State.Idle)
            {
                // Durante patrullaje, tocamos borde o pared: Damos la media vuelta
                patrolDirection *= -1f;
            }
            else
            {
                // Durante persecución, detectamos un vacío: Metemos los frenos de emergencia
                currentSpeedX = 0f;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
    }

    void SetFacing(bool isFacingLeft)
    {
        if (sr.flipX != isFacingLeft)
        {
            sr.flipX = isFacingLeft;

            if (attackPoint != null)
            {
                Vector3 pos = attackPoint.localPosition;
                pos.x = Mathf.Abs(pos.x) * (isFacingLeft ? -1f : 1f);
                attackPoint.localPosition = pos;
            }
        }
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

    public void DealDamage()
    {
        if (hasDealtDamage) return;
        hasDealtDamage = true; // Impedir causar doble daño

        bool hitAnything = false;

        if (attackPoint != null)
        {
            Collider2D[] hits = playerLayerMask == 0 
                ? Physics2D.OverlapCircleAll(attackPoint.position, attackRadius)
                : Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, playerLayerMask);

            foreach (Collider2D hit in hits)
            {
                // Si no hay layermask configurado, obligamos que el objeto tenga el tag "Player"
                if (playerLayerMask != 0 || hit.CompareTag("Player"))
                {
                    IDamageable damageable = GetDamageable(hit.gameObject);
                    if (damageable != null)
                    {
                        Vector2 hitDirection = (hit.transform.position - transform.position).normalized;
                        damageable.TakeDamage(damage, hitDirection);
                        hitAnything = true;
                        Debug.Log("[EnemyAI] Golpe exitoso al jugador usando overlap.");
                    }
                }
            }
        }
        else
        {
            // Comportamiento antiguo por si no han configurado el Attack Point
            if (player != null)
            {
                float dist = Vector2.Distance(transform.position, player.position);
                // Damos un margen generoso por si corrió durante el retardo
                if (dist <= attackRange * 2f)
                {
                    IDamageable damageable = GetDamageable(player.gameObject);
                    if (damageable != null)
                    {
                        Vector2 hitDirection = (player.position - transform.position).normalized;
                        damageable.TakeDamage(damage, hitDirection);
                        hitAnything = true;
                        Debug.Log("[EnemyAI] Golpe exitoso al jugador por distancia (fallback sin AttackPoint).");
                    }
                }
            }
        }

        if (!hitAnything)
        {
            Debug.Log("[EnemyAI] El ataque falló. Ni el área cubrió al jugador, ni se encontró IDamageable.");
        }
    }

    // ─────────────────────────────────────────
    //  DAÑO POR CONTACTO DIRECTO
    // ─────────────────────────────────────────

    void OnCollisionEnter2D(Collision2D collision) => HandleContactDamage(collision.gameObject);
    void OnCollisionStay2D(Collision2D collision)  => HandleContactDamage(collision.gameObject);

    void HandleContactDamage(GameObject hitObject)
    {
        if (Time.time < lastAttackTime + attackCooldown) return;
        
        if (hitObject.CompareTag("Player"))
        {
            IDamageable damageable = GetDamageable(hitObject);
            if (damageable != null)
            {
                // No aplicar daño si está invulnerable en su dash
                if (playerController != null && playerController.IsInvulnerable()) return;
                
                Vector2 hitDirection = (hitObject.transform.position - transform.position).normalized;
                damageable.TakeDamage(damage, hitDirection);
                lastAttackTime = Time.time;
                Debug.Log("[EnemyAI] Golpe exitoso por contacto físico directo.");
            }
        }
    }

    IDamageable GetDamageable(GameObject obj)
    {
        IDamageable d = obj.GetComponent<IDamageable>();
        if (d == null) d = obj.GetComponentInParent<IDamageable>();
        if (d == null) d = obj.GetComponentInChildren<IDamageable>();
        return d;
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

        // Mostrar detectores visuales en la escena (Editor)
        Vector2 pos = (Vector2)transform.position + new Vector2(0f, 0.2f);
        float dirX = (Application.isPlaying && sr != null) ? (sr.flipX ? -1f : 1f) : 1f;
        Vector2 frontPos = pos + new Vector2(dirX * edgeCheckOffsetX, 0);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(frontPos, frontPos + Vector2.down * edgeCheckDistance); // Borde
        Gizmos.DrawLine(pos, pos + new Vector2(dirX * (edgeCheckOffsetX + 0.1f), 0)); // Pared
    }
}