using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public SaveData CurrentSaveData { get; private set; }
    public int CurrentSlotID { get; private set; } = -1;

    // Evento disparado cuando la partida se ha cargado por completo
    public event Action OnGameLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private string GetSavePath(int slotID)
    {
        // Se guarda en C:\Users\<Usuario>\AppData\LocalLow\<Empresa>\<Juego>
        return Path.Combine(Application.persistentDataPath, $"save_slot_{slotID}.dat");
    }

    public bool DoesSaveExist(int slotID)
    {
        return File.Exists(GetSavePath(slotID));
    }

    public void CreateNewGame(int slotID)
    {
        CurrentSlotID = slotID;
        CurrentSaveData = new SaveData(); // Inicializa "Nivel1", vida al maximo, isNewGame = true
        
        // NO guardamos inmediatamente porque estaríamos guardando la escena actual (Menú Principal).
        // En lugar de eso, cargamos Nivel1 primero.
        
        // Cargar escena inicial
        LoadScene(CurrentSaveData.sceneName);
    }

    public void LoadGame(int slotID)
    {
        if (!DoesSaveExist(slotID))
        {
            Debug.LogError($"[SaveManager] No se encontró un archivo en el slot {slotID}");
            return;
        }

        CurrentSlotID = slotID;
        string path = GetSavePath(slotID);

        try
        {
            // Leer y descifrar
            string encryptedJson = File.ReadAllText(path);
            string json = CryptoUtility.Decrypt(encryptedJson);
            
            // Reconstruir objeto
            CurrentSaveData = JsonUtility.FromJson<SaveData>(json);

            // Cargar escena guardada
            LoadScene(CurrentSaveData.sceneName);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Error al cargar la partida: {e.Message}");
        }
    }

    public void SaveGame(int slotID = -1)
    {
        if (slotID == -1)
        {
            slotID = CurrentSlotID;
        }

        if (slotID == -1)
        {
            Debug.LogError("[SaveManager] Error: No se ha seleccionado ningún slot activo para guardar.");
            return;
        }

        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData();
        }

        // 1. Recolectar datos actuales
        CurrentSaveData.sceneName = SceneManager.GetActiveScene().name;

        // Buscar todos los componentes ISaveable de la escena para que inyecten su info
        IEnumerable<ISaveable> saveables = FindObjectsOfType<MonoBehaviour>().OfType<ISaveable>();
        foreach (ISaveable saveable in saveables)
        {
            saveable.PopulateSaveData(CurrentSaveData);
        }

        // 2. Serializar a JSON y cifrar a AES
        string json = JsonUtility.ToJson(CurrentSaveData, true);
        string encryptedJson = CryptoUtility.Encrypt(json);

        // 3. Escribir a disco
        string path = GetSavePath(slotID);
        File.WriteAllText(path, encryptedJson);
        
        Debug.Log($"[SaveManager] Partida guardada con éxito en {path}");
    }

    private void LoadScene(string sceneName)
    {
        // Carga asíncrona para que no congele el juego
        SceneManager.LoadSceneAsync(sceneName).completed += (AsyncOperation op) =>
        {
            if (CurrentSaveData.isNewGame)
            {
                // Si es una partida nueva, recolectamos el estado inicial de la escena (incluyendo 
                // la posición del Player donde sea que lo hayas puesto en el Editor) 
                // para NO teletransportarlo a (0,0,0).
                CurrentSaveData.isNewGame = false;
                
                IEnumerable<ISaveable> initialSaveables = FindObjectsOfType<MonoBehaviour>().OfType<ISaveable>();
                foreach (ISaveable saveable in initialSaveables)
                {
                    saveable.PopulateSaveData(CurrentSaveData);
                }
                
                // Ahora sí guardamos el archivo en disco (ahora con la escena y posiciones correctas)
                SaveGame(CurrentSlotID);
            }

            // Una vez que la escena carga y los datos están listos, buscar ISaveables y aplicar datos
            IEnumerable<ISaveable> saveables = FindObjectsOfType<MonoBehaviour>().OfType<ISaveable>();
            foreach (ISaveable saveable in saveables)
            {
                saveable.LoadFromSaveData(CurrentSaveData);
            }

            OnGameLoaded?.Invoke();
        };
    }
}
