using System.Collections.Generic;
using UnityEngine;


/// <summary>Apresentacao e placar do progresso confirmado da campanha.</summary>
public class CampanhaManager : MonoBehaviour
{
    [Header("Cor dos setores vencidos")]
    [Tooltip("Intensidade da cor do vencedor no terreno: 0 original; 1 cor completa.")]
    [Range(0f, 1f)]
    [SerializeField] private float winnerTintStrength = 0.7f;
    [SerializeField] private CampaignSelectionController selectionController;

    public float WinnerTintStrength => winnerTintStrength;
    private float lastTintStrength = -1f;

    private void Awake()
    {
        if (selectionController == null)
            selectionController = GetComponent<CampaignSelectionController>();
        if (selectionController == null)
            foreach (CampaignSelectionController controller in
                FindObjectsByType<CampaignSelectionController>(FindObjectsSortMode.None))
                if (controller.gameObject.scene == gameObject.scene)
                {
                    selectionController = controller;
                    break;
                }
    }

    private void Update()
    {
        if (lastTintStrength == winnerTintStrength) return;
        lastTintStrength = winnerTintStrength;
        selectionController?.RefreshCampaignProgressPresentation();
    }

    /// <summary>Dono de cada quadrante, na ordem da tela. Ver GetSectorCounts.</summary>
    public void GetQuadrantOwners(List<PlayerSlotId> destino)
    {
        selectionController?.GetQuadrantOwners(destino);
    }

    public void GetSectorCounts(out int slot0, out int slot1, out int total)
    {
        slot0 = slot1 = total = 0;
        if (selectionController != null)
            selectionController.GetWonSectorCounts(out slot0, out slot1, out total);
    }
}
