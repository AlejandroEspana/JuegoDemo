using UnityEngine;

public class SaveMenuController : MonoBehaviour
{
    [Header("Slots Array")]
    [Tooltip("Arrastra aquí los GameObjects que tienen el script SaveSlotUI")]
    public SaveSlotUI[] saveSlots;

    private void Start()
    {
        // Inicializa automáticamente los slots en el menú
        foreach (SaveSlotUI slot in saveSlots)
        {
            slot.Initialize(this);
        }
    }

    /// <summary>
    /// Llamado por el botón del slot cuando el usuario hace clic en él.
    /// </summary>
    public void OnSlotSelected(int slotID, bool hasData)
    {
        // Desactivamos los botones para evitar múltiples clics
        SetSlotsInteractable(false);

        if (hasData)
        {
            Debug.Log($"[SaveMenu] Cargando partida del Slot {slotID}...");
            SaveManager.Instance.LoadGame(slotID);
        }
        else
        {
            Debug.Log($"[SaveMenu] Creando nueva partida en el Slot {slotID}...");
            SaveManager.Instance.CreateNewGame(slotID);
        }
    }

    private void SetSlotsInteractable(bool state)
    {
        foreach (SaveSlotUI slot in saveSlots)
        {
            if (slot.slotButton != null)
                slot.slotButton.interactable = state;
        }
    }
}
