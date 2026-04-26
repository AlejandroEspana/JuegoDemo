using UnityEngine;

/// <summary>
/// Proyectil que se mueve en línea recta con velocidad constante y daña al jugador al impactar.
/// Requiere estar configurado como un Trigger (IsTrigger = true) o un Collider normal para chocar.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Configuración del Proyectil")]
    [SerializeField] float speed = 10f;
    [SerializeField] int damage = 1;
    [SerializeField] float lifeTime = 3f;
    
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Hacemos que el Rigidbody ignore la gravedad para viajar verdaderamente en línea recta
        rb.gravityScale = 0f; 
        
        // Ponemos detección continua para que no atraviese colisionadores delgados
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Start()
    {
        // El proyectil se autodestruye si no choca con nada para no saturar la memoria
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// Configura y lanza la bala en la dirección deseada.
    /// </summary>
    public void Setup(Vector2 direction)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        
        // Asignamos la velocidad constante
        rb.linearVelocity = direction.normalized * speed;

        // Rotar el sprite de la bala para que apunte hacia donde vuela
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    // Funciona si el collider está configurado como Is Trigger
    void OnTriggerEnter2D(Collider2D collision)
    {
        HandleHit(collision.gameObject);
    }

    // Funciona si el collider usa física sólida normal
    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    void HandleHit(GameObject hitObject)
    {
        // 1. Evitamos que las balas maten a otros enemigos o choquen consigo mismas
        if (hitObject.CompareTag("Enemy") || hitObject.CompareTag("Untagged")) return;

        // 2. Verificamos si tocamos al Jugador
        if (hitObject.CompareTag("Player"))
        {
            // Validar si el jugador está haciendo DASH (Intocable)
            // Buscamos el PlayerController en el objeto o sus padres
            PlayerController pc = hitObject.GetComponent<PlayerController>();
            if (pc == null) pc = hitObject.GetComponentInParent<PlayerController>();

            if (pc != null && pc.IsInvulnerable())
            {
                // El jugador es intangible en este momento. 
                // Ignoramos la colisión y permitimos que la bala pase de largo (Atraviese el cuerpo).
                return;
            }

            // Si NO está en Dash, le aplicamos el daño
            IDamageable damageable = GetDamageable(hitObject);
            if (damageable != null)
            {
                Vector2 hitDir = rb.linearVelocity.normalized;
                damageable.TakeDamage(damage, hitDir);
                Debug.Log("[EnemyProjectile] El jugador fue alcanzado por el disparo.");
            }
        }

        // 3. Destruir el proyectil
        // Llegará a esta línea si chocó contra el jugador vulnerable, contra el Suelos, Paredes o un Techo.
        Destroy(gameObject);
    }

    // Método robusto para encontrar el script IDamageable (como PlayerHealth)
    IDamageable GetDamageable(GameObject obj)
    {
        IDamageable d = obj.GetComponent<IDamageable>();
        if (d == null) d = obj.GetComponentInParent<IDamageable>();
        if (d == null) d = obj.GetComponentInChildren<IDamageable>();
        return d;
    }
}
