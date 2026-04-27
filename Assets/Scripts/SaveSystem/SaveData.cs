using UnityEngine;

[System.Serializable]
public class SaveData
{
    // Nombre de la escena donde el jugador guardó por última vez
    public string sceneName;

    // Posición actual del jugador
    public Vector3 playerPosition;

    // Estadísticas del jugador
    public float playerHealth;
    public int score;

    // Bandera para saber si es la primera vez que se carga
    public bool isNewGame;

    // Constructor por defecto. Define los valores para una "Nueva Partida"
    public SaveData()
    {
        sceneName = "Nivel1";
        playerPosition = Vector3.zero;
        playerHealth = 100f; // Asumiendo 100 como base, ajustable según el PlayerHealth
        score = 0;
        isNewGame = true;
    }
}
