using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Arrastra aquí el objeto Panel que contiene tu Menú de Pausa")]
    public GameObject pausePanel;
    
    [Header("Configuración")]
    [Tooltip("El nombre exacto de la escena de tu menú principal")]
    public string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;

    private void Start()
    {
        // Asegurarnos de que el panel empiece oculto y el tiempo corra normal
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        Time.timeScale = 1f;
    }

    private void Update()
    {
        // Usamos el New Input System para detectar la tecla Escape
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        
        if (pausePanel != null)
            pausePanel.SetActive(true);
        
        // Congelar el tiempo y físicas del juego
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        
        if (pausePanel != null)
            pausePanel.SetActive(false);
        
        // Descongelar el juego
        Time.timeScale = 1f;
    }

    public void SaveProgress()
    {
        if (SaveManager.Instance != null)
        {
            // Llama al SaveManager para que recolecte posiciones y guarde en disco
            SaveManager.Instance.SaveGame();
            Debug.Log("[PauseMenu] Progreso guardado manualmente en el slot actual.");
        }
        else
        {
            Debug.LogError("[PauseMenu] No se encontró el SaveManager. ¿Pasaste por el menú principal primero?");
        }
    }

    public void ReturnToMainMenu()
    {
        // MUY IMPORTANTE: Restaurar el tiempo a 1 ANTES de cambiar de escena.
        // Si no lo haces, el menú principal cargará congelado.
        Time.timeScale = 1f;
        
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
