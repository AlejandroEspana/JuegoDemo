using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [Header("Configuración del Slot")]
    [Tooltip("El ID debe ser único por botón, ej: 1, 2, 3...")]
    [SerializeField] private int slotID;
    
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI slotStatusText;
    public Button slotButton;

    private SaveMenuController menuController;
    private bool hasData = false;

    public void Initialize(SaveMenuController controller)
    {
        menuController = controller;
        RefreshSlot();

        // Limpiar y añadir listener para evitar múltiples llamadas
        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(OnSlotClicked);
    }

    public void RefreshSlot()
    {
        // Consultar al Manager si existe archivo en el disco
        hasData = SaveManager.Instance.DoesSaveExist(slotID);

        if (hasData)
        {
            slotStatusText.text = $"Partida {slotID}\n<color=#2ecc71>Datos Encontrados</color>";
        }
        else
        {
            slotStatusText.text = $"Partida {slotID}\n<color=#95a5a6>Vacío</color>";
        }
    }

    private void OnSlotClicked()
    {
        // Avisar al controlador principal del menú
        menuController.OnSlotSelected(slotID, hasData);
    }
}
