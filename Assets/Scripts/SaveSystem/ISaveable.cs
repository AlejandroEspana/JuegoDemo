public interface ISaveable
{
    /// <summary>
    /// Se llama al momento de guardar la partida.
    /// El objeto debe inyectar su estado actual dentro del objeto SaveData.
    /// </summary>
    void PopulateSaveData(SaveData a_SaveData);
    
    /// <summary>
    /// Se llama después de que la escena ha cargado y el sistema recuperó el archivo de guardado.
    /// El objeto debe restaurar su estado usando los valores contenidos en SaveData.
    /// </summary>
    void LoadFromSaveData(SaveData a_SaveData);
}
