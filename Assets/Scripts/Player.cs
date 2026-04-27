using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Controlador principal del jugador.
/// Maneja movimiento, salto, dash y combate con animaciones fluidas.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour, ISaveable
{
    // ─────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────

    [Header("Referencias")]
    [SerializeField] Transform groundCheck;
    [SerializeField] Transform attackPoint;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask enemyLayers;

    [Header("Escala Dinámica")]
    [SerializeField] float baseHeight = 2f;

    [Header("Movimiento")]
    [SerializeField] float baseSpeed        = 6f;
    [SerializeField] float accelerationTime = 0.08f;
    [SerializeField] float decelerationTime = 0.05f;

    [Header("Salto")]
    [SerializeField] float baseJumpForce    = 12f;
    [SerializeField] float coyoteTime       = 0.12f;
    [SerializeField] float jumpBufferTime   = 0.12f;
    [SerializeField] float fallMultiplier   = 2.5f;
    [SerializeField] float lowJumpMultiplier = 2f;

    [Header("Dash")]
    [SerializeField] float baseDashSpeed     = 18f;
    [SerializeField] float dashDuration      = 0.15f;
    [SerializeField] float dashCooldown      = 2f;
    [SerializeField] float dashBlinkInterval = 0.05f;

    [Header("Ataque")]
    [SerializeField] float baseAttackRadius = 0.6f;
    [SerializeField] int   damage           = 1;
    [SerializeField] float attackCooldown   = 0.5f;

    [Header("Ground Check")]
    [SerializeField] float baseGroundRadius = 0.2f;

    // ─────────────────────────────────────────
    //  VALORES ESCALADOS
    // ─────────────────────────────────────────

    float speed;
    float jumpForce;
    float dashSpeed;
    float attackRadius;
    float groundRadius;
    float scaleMultiplier;

    // ─────────────────────────────────────────
    //  ESTADO INTERNO
    // ─────────────────────────────────────────

    // Movimiento
    Vector2 inputDir;
    float   currentSpeedX;
    bool    isFacingRight = true;
    bool    isPressingDown;

    // Salto
    bool  isGrounded;
    float coyoteTimeCounter;
    float jumpBufferCounter;
    bool  jumpHeld;

    // Dash
    bool isDashing;
    bool canDash = true;

    // Ataque
    bool            canAttack = true;
    bool            isAttacking;
    AttackDirection currentAttackDir;
    bool            hasDealtDamage;

    // Invulnerabilidad
    bool isInvulnerable;
    float knockbackTimer;

    // Capas
    int playerLayer;
    int enemyLayer;

    // Componentes cacheados
    Rigidbody2D    rb;
    Collider2D     col;
    SpriteRenderer sr;
    Animator       anim;

    // WaitForSeconds cacheados — sin garbage collection
    WaitForSeconds waitDashDuration;
    WaitForSeconds waitDashCooldown;
    WaitForSeconds waitDashBlink;
    WaitForSeconds waitAttackCooldown;
    WaitForSeconds waitPlatformIgnore;

    // Hashes del Animator — más rápido que strings en Update
    static readonly int HashSpeed       = Animator.StringToHash("Speed");
    static readonly int HashIsGrounded  = Animator.StringToHash("IsGrounded");
    static readonly int HashIsDashing   = Animator.StringToHash("IsDashing");
    static readonly int HashVerticalVel = Animator.StringToHash("VerticalVel");
    static readonly int HashAttack      = Animator.StringToHash("Attack");
    static readonly int HashAttackDir   = Animator.StringToHash("AttackDir");

    enum AttackDirection { Neutral = 0, Up = 1, Down = 2 }

    // ─────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────

    void Awake()
    {
        rb   = GetComponent<Rigidbody2D>();
        col  = GetComponent<Collider2D>();
        sr   = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        playerLayer = LayerMask.NameToLayer("Player");
        enemyLayer  = LayerMask.NameToLayer("Enemy");

        if (playerLayer == -1 || enemyLayer == -1)
            Debug.LogError("[PlayerController] Crea los layers 'Player' y 'Enemy'.");

        CalculateDynamicScale();
        CacheWaitForSeconds();
    }

    void Update()
    {
        if (knockbackTimer > 0f) knockbackTimer -= Time.deltaTime;

        CheckGround();
        HandleCoyoteTime();
        HandleJumpBuffer();
        HandleGravityModifier();
        UpdateAnimations();
        Flip();
    }

    void FixedUpdate()
    {
        if (!isDashing && knockbackTimer <= 0f)
            ApplyMovement();
    }

    // ─────────────────────────────────────────
    //  INICIALIZACIÓN
    // ─────────────────────────────────────────

    void CalculateDynamicScale()
    {
        float characterHeight = sr.bounds.size.y;
        scaleMultiplier = characterHeight / baseHeight;

        speed        = baseSpeed        * scaleMultiplier;
        jumpForce    = baseJumpForce    * scaleMultiplier;
        dashSpeed    = baseDashSpeed    * scaleMultiplier;
        attackRadius = baseAttackRadius * scaleMultiplier;
        groundRadius = baseGroundRadius * scaleMultiplier;
    }

    void CacheWaitForSeconds()
    {
        waitDashDuration   = new WaitForSeconds(dashDuration);
        waitDashCooldown   = new WaitForSeconds(dashCooldown);
        waitDashBlink      = new WaitForSeconds(dashBlinkInterval);
        waitAttackCooldown = new WaitForSeconds(attackCooldown);
        waitPlatformIgnore = new WaitForSeconds(0.5f);
    }

    // ─────────────────────────────────────────
    //  INPUT (New Input System)
    // ─────────────────────────────────────────

    public void OnMove(InputValue value)
    {
        inputDir       = value.Get<Vector2>();
        isPressingDown = inputDir.y < -0.5f;
    }

    public void OnJump(InputValue value)
    {
        jumpHeld = value.isPressed;
        if (!value.isPressed) return;

        if (isPressingDown && isGrounded)
        {
            StartCoroutine(DisablePlatformCollision());
            return;
        }

        jumpBufferCounter = jumpBufferTime;
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed && canDash && !isDashing)
            StartCoroutine(DashRoutine());
    }

    public void OnAttack(InputValue value)
    {
        if (!value.isPressed || !canAttack || isAttacking) return;
        StartCoroutine(AttackRoutine());
    }

    // ─────────────────────────────────────────
    //  MOVIMIENTO
    // ─────────────────────────────────────────

    void ApplyMovement()
    {
        float targetSpeed = inputDir.x * speed;
        float smoothTime  = Mathf.Abs(targetSpeed) > 0.01f ? accelerationTime : decelerationTime;

        currentSpeedX     = Mathf.Lerp(currentSpeedX, targetSpeed, Time.fixedDeltaTime / smoothTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
    }

    void Flip()
    {
        if      (inputDir.x >  0.01f) SetFacing(true);
        else if (inputDir.x < -0.01f) SetFacing(false);
    }

    void SetFacing(bool facingRight)
    {
        if (isFacingRight == facingRight) return;

        isFacingRight = facingRight;
        
        // Volvemos a flipX: Evita que el collider "salte" y se atasque dentro del enemigo.
        sr.flipX = !facingRight;

        if (attackPoint != null)
        {
            Vector3 pos = attackPoint.localPosition;
            pos.x = Mathf.Abs(pos.x) * (facingRight ? 1f : -1f);
            attackPoint.localPosition = pos;
        }
    }

    // ─────────────────────────────────────────
    //  SALTO
    // ─────────────────────────────────────────

    void CheckGround()
    {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);
    }

    void HandleCoyoteTime()
    {
        if (isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;
    }

    void HandleJumpBuffer()
    {
        if (jumpBufferCounter <= 0f) return;

        jumpBufferCounter -= Time.deltaTime;

        if (coyoteTimeCounter > 0f)
        {
            ExecuteJump();
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }
    }

    void ExecuteJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    void HandleGravityModifier()
    {
        if (rb.linearVelocity.y < 0f)
        {
            // Caída más pesada y natural
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            // Salto corto al soltar el botón
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.deltaTime;
        }
    }

    // ─────────────────────────────────────────
    //  DASH
    // ─────────────────────────────────────────

    IEnumerator DashRoutine()
    {
        isDashing      = true;
        canDash        = false;
        isInvulnerable = true;

        IgnoreEnemyCollision(true);
        StartCoroutine(DashBlinkRoutine());

        Vector2 dir = inputDir.normalized;
        if (dir == Vector2.zero)
            dir = isFacingRight ? Vector2.right : Vector2.left;

        rb.linearVelocity = dir * dashSpeed;

        yield return waitDashDuration;

        isDashing      = false;
        isInvulnerable = false;
        sr.enabled     = true;

        IgnoreEnemyCollision(false);

        yield return waitDashCooldown;
        canDash = true;
    }

    IEnumerator DashBlinkRoutine()
    {
        while (isDashing)
        {
            sr.enabled = !sr.enabled;
            yield return waitDashBlink;
        }
        sr.enabled = true;
    }

    void IgnoreEnemyCollision(bool ignore)
    {
        if (playerLayer != -1 && enemyLayer != -1)
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, ignore);
    }

    // ─────────────────────────────────────────
    //  ATAQUE
    // ─────────────────────────────────────────

    IEnumerator AttackRoutine()
    {
        canAttack   = false;
        isAttacking = true;
        hasDealtDamage = false;

        currentAttackDir = inputDir.y >  0.5f ? AttackDirection.Up
                         : inputDir.y < -0.5f ? AttackDirection.Down
                         :                      AttackDirection.Neutral;

        anim.SetInteger(HashAttackDir, (int)currentAttackDir);
        anim.SetTrigger(HashAttack);

        StartCoroutine(AttackFallbackCoroutine());

        yield return waitAttackCooldown;

        isAttacking = false;
        canAttack   = true;
    }

    IEnumerator AttackFallbackCoroutine()
    {
        // Respaldo en caso de que no exista el Animation Event en la animación de ataque
        yield return new WaitForSeconds(0.15f);
        if (!hasDealtDamage) DealDamage();
    }

    /// <summary>
    /// Llamado por Animation Event en el frame de impacto.
    /// ¡NO borrar! Lo invoca el Animator automáticamente.
    /// </summary>
    public void DealDamage()
    {
        if (hasDealtDamage) return;
        hasDealtDamage = true;

        if (attackPoint == null) 
        {
            Debug.LogWarning("[Player] No hay 'Attack Point' asignado. Aplicando daño en área (fallback).");
            Collider2D[] fallbackHits = Physics2D.OverlapCircleAll(transform.position, attackRadius * 1.5f);
            foreach (Collider2D hit in fallbackHits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    IDamageable damageable = GetDamageable(hit.gameObject);
                    if (damageable != null)
                    {
                        Vector2 hitDirection = (hit.transform.position - transform.position).normalized;
                        damageable.TakeDamage(damage, hitDirection);
                        Debug.Log("[Player] Golpe exitoso a: " + hit.name);
                    }
                }
            }
            return;
        }

        Collider2D[] hits = enemyLayers == 0 
            ? Physics2D.OverlapCircleAll(attackPoint.position, attackRadius)
            : Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, enemyLayers);

        bool hitAnything = false;
        foreach (Collider2D hit in hits)
        {
            if (enemyLayers != 0 || hit.CompareTag("Enemy"))
            {
                IDamageable damageable = GetDamageable(hit.gameObject);
                if (damageable != null)
                {
                    Vector2 hitDirection = (hit.transform.position - transform.position).normalized;
                    damageable.TakeDamage(damage, hitDirection);
                    hitAnything = true;
                    Debug.Log("[Player] Golpe exitoso a: " + hit.name);
                }
            }
        }

        if (!hitAnything)
        {
            Debug.Log("[Player] El ataque no golpeó a ningún enemigo con componente IDamageable o Tag adecuado.");
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
        // Speed normalizado (0=quieto · 1=velocidad máx) con damping suave
        float normalizedSpeed = speed > 0f ? Mathf.Abs(currentSpeedX) / speed : 0f;
        anim.SetFloat(HashSpeed,       normalizedSpeed, 0.05f, Time.deltaTime);
        anim.SetFloat(HashVerticalVel, rb.linearVelocity.y);
        anim.SetBool(HashIsGrounded,   isGrounded);
        anim.SetBool(HashIsDashing,    isDashing);
    }

    // ─────────────────────────────────────────
    //  PLATAFORMAS ONE-WAY
    // ─────────────────────────────────────────

    IEnumerator DisablePlatformCollision()
    {
        if (groundCheck == null) yield break;

        Collider2D platform = Physics2D.OverlapCircle(
            groundCheck.position,
            groundRadius,
            groundLayer
        );

        if (platform != null && platform.CompareTag("Platform"))
        {
            Physics2D.IgnoreCollision(col, platform, true);
            yield return waitPlatformIgnore;
            Physics2D.IgnoreCollision(col, platform, false);
        }
    }

    // ─────────────────────────────────────────
    //  API PÚBLICA
    // ─────────────────────────────────────────

    public bool IsInvulnerable() => isInvulnerable;
    public bool IsGrounded()     => isGrounded;
    public bool IsDashing()      => isDashing;
    
    public void ApplyKnockbackState(float duration)
    {
        knockbackTimer = duration;
        currentSpeedX = rb.linearVelocity.x; 
    }

    // ─────────────────────────────────────────
    //  SISTEMA DE GUARDADO (ISaveable)
    // ─────────────────────────────────────────

    public void PopulateSaveData(SaveData a_SaveData)
    {
        // Guardar la posición actual
        a_SaveData.playerPosition = transform.position;
    }

    public void LoadFromSaveData(SaveData a_SaveData)
    {
        // Al usar Rigidbody2D, es importante actualizar tanto el Transform como la posición del RB
        // para evitar que el motor de físicas "teletransporte" al jugador de vuelta a su posición previa.
        transform.position = a_SaveData.playerPosition;
        
        if (rb != null)
        {
            rb.position = a_SaveData.playerPosition;
            rb.linearVelocity = Vector2.zero; // Reseteamos cualquier inercia o caída
        }
    }

    // ─────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        float scaleMult = 1f;

        // Si estamos en el editor y el juego no está corriendo, estimamos el escalado
        if (!Application.isPlaying && baseHeight > 0f)
        {
            SpriteRenderer spriteRend = GetComponent<SpriteRenderer>();
            if (spriteRend != null && spriteRend.bounds.size.y > 0)
                scaleMult = spriteRend.bounds.size.y / baseHeight;
        }
        else if (Application.isPlaying)
        {
            scaleMult = scaleMultiplier;
        }

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            float r = Application.isPlaying ? attackRadius : baseAttackRadius * scaleMult;
            Gizmos.DrawWireSphere(attackPoint.position, r);
        }

        if (groundCheck != null)
        {
            Gizmos.color = Color.blue;
            float rG = Application.isPlaying ? groundRadius : baseGroundRadius * scaleMult;
            Gizmos.DrawWireSphere(groundCheck.position, rG);
        }
    }
}