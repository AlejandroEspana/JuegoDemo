using UnityEngine;

/// <summary>
/// Interfaz para cualquier entidad que pueda recibir daño.
/// Ayuda a desacoplar el código para que Player y Enemy no dependan estrictamente de clases específicas.
/// </summary>
public interface IDamageable
{
    void TakeDamage(int damage, Vector2 hitDirection = default);
}
