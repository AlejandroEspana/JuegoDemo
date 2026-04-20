using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    private Vector2 movement;
    private bool isPressingDown;
    private bool isFacingRight = true;

    [Header("Salto")]
    public float jumpForce = 7f;
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Dash")]
    public float dashSpeed = 12f;
    public float dashTime = 0.2f;
    public float dashCooldown = 2f;
    private bool isDashing = false;
    private bool canDash = true;

    [Header("Ataque")]
    public Transform attackPoint;
    public float attackRadius = 0.5f;
    public LayerMask enemyLayers;
    public int damage = 1;
    public float attackCooldown = 1f;
    private bool canAttack = true;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private SpriteRenderer sprite;

    private bool isInvulnerable = false;

    public float dashBlinkInterval = 0.05f;

    // 🔥 Layers
    int playerLayer;
    int enemyLayer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        sprite = GetComponent<SpriteRenderer>();

        playerLayer = LayerMask.NameToLayer("Player");
        enemyLayer = LayerMask.NameToLayer("Enemy");
    }

    void Update()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);
        }

        Flip();
    }

    void FixedUpdate()
    {
        if (!isDashing)
        {
            rb.linearVelocity = new Vector2(movement.x * speed, rb.linearVelocity.y);
        }
    }

    // ===== INPUT =====

    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
        isPressingDown = movement.y < -0.5f;
    }

    public void OnJump(InputValue value)
    {
        if (!value.isPressed) return;

        if (isPressingDown && isGrounded)
        {
            StartCoroutine(DisablePlatformCollision());
            return;
        }

        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed && canDash && !isDashing)
        {
            StartCoroutine(Dash());
        }
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed && canAttack)
        {
            StartCoroutine(AttackCoroutine());
        }
    }

    // ===== DASH =====

    IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;
        isInvulnerable = true;

        // 🔥 ATRAVESAR ENEMIGOS
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);

        StartCoroutine(DashBlink());

        Vector2 dir = movement.normalized;
        if (dir == Vector2.zero)
            dir = isFacingRight ? Vector2.right : Vector2.left;

        rb.linearVelocity = dir * dashSpeed;

        yield return new WaitForSeconds(dashTime);

        isDashing = false;
        isInvulnerable = false;

        // 🔥 VOLVER A COLISIONAR
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);

        sprite.enabled = true;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    IEnumerator DashBlink()
    {
        while (isDashing)
        {
            sprite.enabled = !sprite.enabled;
            yield return new WaitForSeconds(dashBlinkInterval);
        }

        sprite.enabled = true;
    }

    // ===== ATAQUE =====

    IEnumerator AttackCoroutine()
    {
        canAttack = false;

        Attack();

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }

    void Attack()
    {
        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRadius,
            enemyLayers
        );

        foreach (Collider2D enemy in enemies)
        {
            EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
            if (eh != null)
            {
                eh.TakeDamage(damage);
            }
        }
    }

    // ===== GIRO =====

    void Flip()
    {
        if (movement.x > 0)
        {
            sprite.flipX = false;
            isFacingRight = true;
        }
        else if (movement.x < 0)
        {
            sprite.flipX = true;
            isFacingRight = false;
        }

        if (attackPoint != null)
        {
            attackPoint.localPosition = new Vector3(
                Mathf.Abs(attackPoint.localPosition.x) * (isFacingRight ? 1 : -1),
                attackPoint.localPosition.y,
                attackPoint.localPosition.z
            );
        }
    }

    // ===== PLATAFORMAS =====

    IEnumerator DisablePlatformCollision()
    {
        Collider2D platform = Physics2D.OverlapCircle(
            groundCheck.position,
            groundRadius,
            groundLayer
        );

        if (platform != null && platform.CompareTag("Platform"))
        {
            Physics2D.IgnoreCollision(playerCollider, platform, true);

            yield return new WaitForSeconds(0.5f);

            Physics2D.IgnoreCollision(playerCollider, platform, false);
        }
    }

    // ===== INVULNERABILIDAD =====

    public bool IsInvulnerable()
    {
        return isInvulnerable;
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }

        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}