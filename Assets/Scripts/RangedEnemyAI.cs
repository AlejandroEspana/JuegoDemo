using UnityEngine;

/// <summary>
/// Inteligencia Artificial para un enemigo de ataque a distancia que dispara proyectiles (Prefabs).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class RangedEnemyAI : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────

    [Header("Movimiento")]
    [SerializeField] float speed = 2.5f;
    [SerializeField] float accelerationTime = 0.06f;

    [Header("Patrullaje y Detección (Bordes/Paredes)")]
    [SerializeField] float patrolSpeed = 1f;
    [SerializeField] float edgeCheckOffsetX = 0.5f;
    [SerializeField] float edgeCheckDistance = 1.2f;
    [SerializeField] LayerMask groundLayer;

    [Header("Detección y Rango")]
    [SerializeField] float detectionRange = 10f; // A qué distancia te ve
    [SerializeField] float attackRange    = 7f;  // Desde dónde empieza a disparar
    [SerializeField] float retreatRange   = 3f;  // (Opcional) Si te acercas demasiado, huye hacia atrás

    [Header("Ataque y Arma")]
    [SerializeField] GameObject projectilePrefab; // Prefab de la bala que hemos creado
    [SerializeField] Transform firePoint;         // Desde qué posición nace la bala (punta del arma)
    [SerializeField] float attackCooldown = 2f;   // Pausa entre cada disparo

    [Header("Animación")]
    [SerializeField] Animator anim;

    // ─────────────────────────────────────────
    //  ESTADO INTERNO
    // ─────────────────────────────────────────

    enum State { Idle, Chase, Retreat, Attack }
    State currentState = State.Idle;

    float patrolDirection = 1f;

    Transform player;
    Rigidbody2D rb;
    SpriteRenderer sr;

    float lastAttackTime = -999f;
    float currentSpeedX;
    float stunTimer;
    bool isShooting = false; // Bloquea el voltearse mientras dispara
    
    static readonly int HashSpeedX   = Animator.StringToHash("SpeedX");
    static readonly int HashIsAttack = Animator.StringToHash("Attack");

    // ─────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (anim == null) anim = GetComponent<Animator>();
    }

    void Start()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj != null) player = obj.transform;
    }

    void Update()
    {
        if (player == null) return;

        // Si recibe un disparo y es aturdido (Integrado con EnemyHealth si es que choca), pausa el IA
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            currentSpeedX = rb.linearVelocity.x; 
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
    //  LÓGICA  
    // ─────────────────────────────────────────

    void UpdateState()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist < retreatRange)
            currentState = State.Retreat;
        else if (dist <= attackRange)
            currentState = State.Attack;
        else
            currentState = State.Chase;
    }

    void ExecuteState()
    {
        switch (currentState)
        {
            case State.Idle:    HandleIdle();             break;
            case State.Chase:   HandleMove(1f);           break; // Se acerca al jugador
            case State.Retreat: HandleMove(-1f);          break; // Se aleja del jugador (táctico)
            case State.Attack:  HandleAttack();           break;
        }
    }

    void HandleIdle()
    {
        currentSpeedX = Mathf.Lerp(currentSpeedX, patrolDirection * patrolSpeed, Time.deltaTime / accelerationTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        bool isFacingLeft = patrolDirection < 0f;
        SetFacing(isFacingLeft);

        CheckLedgeOrWall();
    }

    void HandleMove(float directionMultiplier)
    {
        float dirX = Mathf.Sign(player.position.x - transform.position.x) * directionMultiplier;

        currentSpeedX = Mathf.Lerp(currentSpeedX, dirX * speed, Time.deltaTime / accelerationTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        // Sin importar hacia donde se mueve (avanza o retrocede), siempre mira fijamente al jugador
        bool isFacingLeft = (player.position.x < transform.position.x);
        SetFacing(isFacingLeft);

        CheckLedgeOrWall();
    }

    void CheckLedgeOrWall()
    {
        if (groundLayer == 0) return;

        Vector2 pos = (Vector2)transform.position + new Vector2(0f, 0.2f);
        float dirX = currentSpeedX < 0 ? -1f : (currentSpeedX > 0 ? 1f : (sr.flipX ? -1f : 1f)); 
        Vector2 frontPos = pos + new Vector2(dirX * edgeCheckOffsetX, 0);

        RaycastHit2D groundInfo = Physics2D.Raycast(frontPos, Vector2.down, edgeCheckDistance, groundLayer);
        RaycastHit2D wallInfo = Physics2D.Raycast(pos, new Vector2(dirX, 0), edgeCheckOffsetX + 0.1f, groundLayer);

        bool edgeDetected = (groundInfo.collider == null); 
        bool wallDetected = (wallInfo.collider != null && !wallInfo.collider.isTrigger && !wallInfo.collider.CompareTag("Player") && !wallInfo.collider.CompareTag("Enemy"));

        if (edgeDetected || wallDetected)
        {
            if (currentState == State.Idle)
            {
                patrolDirection *= -1f;
            }
            else
            {
                // Prevent falling/crashing while moving during Chase or Retreat
                currentSpeedX = 0f;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
    }

    void HandleAttack()
    {
        // Frenar para disparar apuntando
        currentSpeedX = Mathf.Lerp(currentSpeedX, 0f, Time.deltaTime / accelerationTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        // Si NO está a mitad de la animación de disparo, sigue apuntando al jugador
        if (!isShooting)
        {
            bool isFacingLeft = (player.position.x < transform.position.x);
            SetFacing(isFacingLeft);
        }

        // Control de tiempo para no disparar balas infinitas de golpe
        if (Time.time >= lastAttackTime + attackCooldown && !isShooting)
        {
            lastAttackTime = Time.time;
            isShooting = true;

            if (anim != null) anim.SetTrigger(HashIsAttack);

            // Iniciar corrutina de retardo de disparo (Ej. espera a que el brazo de la animación se posicione)
            StartCoroutine(ShootCoroutine());
        }
    }

    System.Collections.IEnumerator ShootCoroutine()
    {
        yield return new WaitForSeconds(0.2f); // Tiempo hasta que la bala sale del arma
        SpawnProjectile();
        isShooting = false; // Ya puede volver a voltearse
    }

    void SpawnProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[RangedEnemy] No has asignado el prefab del 'Projectile' en el inspector.");
            return;
        }

        // Dónde nace la bala (firePoint)
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        
        // Crear bala instanciándola en el mundo
        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        if (proj.TryGetComponent(out EnemyProjectile projScript))
        {
            // Cambiamos el aim-bot diagonal por un disparo 100% horizontal basado en hacia dónde mira el enemigo.
            // Así evitamos que dispare por la espalda si el jugador salta encima.
            Vector2 shootDirection = sr.flipX ? Vector2.left : Vector2.right;
            projScript.Setup(shootDirection);
        }
    }

    // ─────────────────────────────────────────
    //  UTILERIA  
    // ─────────────────────────────────────────

    void SetFacing(bool isFacingLeft)
    {
        // Actualizamos sr.flipX siempre, y corregimos el firePoint basándonos en eso.
        sr.flipX = isFacingLeft;

        if (firePoint != null)
        {
            Vector3 pos = firePoint.localPosition;
            // Si el sprite original (isFacingLeft=false) mira a la derecha,
            // al ser true (mirar izquierda), la x debe ser negativa.
            pos.x = Mathf.Abs(pos.x) * (isFacingLeft ? -1f : 1f);
            firePoint.localPosition = pos;
        }
    }

    void UpdateAnimations()
    {
        if (anim == null) return;
        anim.SetFloat(HashSpeedX, Mathf.Abs(currentSpeedX));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, retreatRange);

        // Mostrar detectores
        Vector2 pos = (Vector2)transform.position + new Vector2(0f, 0.2f);
        float dirX = (Application.isPlaying && sr != null) ? (sr.flipX ? -1f : 1f) : 1f;
        Vector2 frontPos = pos + new Vector2(dirX * edgeCheckOffsetX, 0);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(frontPos, frontPos + Vector2.down * edgeCheckDistance);
        Gizmos.DrawLine(pos, pos + new Vector2(dirX * (edgeCheckOffsetX + 0.1f), 0));
    }
}
