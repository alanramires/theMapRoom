using UnityEngine;

public partial class TurnStateManager
{
    private void HandleTransferActionRequested()
    {
        bool canTransfer = availableSensorActionCodes.Contains('T');
        if (!canTransfer || cachedPodeTransferirTargets.Count == 0)
        {
            string reason = string.IsNullOrWhiteSpace(cachedPodeTransferirReason)
                ? "sem opcoes validas agora."
                : cachedPodeTransferirReason;
            Debug.Log($"Pode Transferir (\"T\"): {reason}");
            LogScannerPanel();
            return;
        }

        int sampleCount = Mathf.Min(5, cachedPodeTransferirTargets.Count);
        Debug.Log($"Pode Transferir (\"T\"): {cachedPodeTransferirTargets.Count} opcao(oes) valida(s).");
        for (int i = 0; i < sampleCount; i++)
        {
            PodeTransferirOption option = cachedPodeTransferirTargets[i];
            if (option == null)
                continue;

            string label = !string.IsNullOrWhiteSpace(option.displayLabel)
                ? option.displayLabel
                : $"{option.flowMode}";
            Debug.Log($"- T[{i + 1}] {label}");
        }

        LogScannerPanel();
    }
}
