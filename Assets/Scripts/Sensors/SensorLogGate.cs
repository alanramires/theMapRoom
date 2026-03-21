using UnityEngine;

public static class SensorLogGate
{
    private static MatchController cachedMatchController;

    private static MatchController ResolveMatchController()
    {
        if (cachedMatchController != null)
            return cachedMatchController;

        cachedMatchController = Object.FindAnyObjectByType<MatchController>();
        return cachedMatchController;
    }

    public static bool IsPodeMirarEnabled() => ResolveMatchController()?.EnablePodeMirarSensorLogs ?? false;
    public static bool IsPodeEmbarcarEnabled() => ResolveMatchController()?.EnablePodeEmbarcarSensorLogs ?? false;
    public static bool IsPodeDesembarcarEnabled() => ResolveMatchController()?.EnablePodeDesembarcarSensorLogs ?? false;
    public static bool IsPodeCapturarEnabled() => ResolveMatchController()?.EnablePodeCapturarSensorLogs ?? false;
    public static bool IsPodeFundirEnabled() => ResolveMatchController()?.EnablePodeFundirSensorLogs ?? false;
    public static bool IsPodeSuprirEnabled() => ResolveMatchController()?.EnablePodeSuprirSensorLogs ?? false;
    public static bool IsPodeTransferirEnabled() => ResolveMatchController()?.EnablePodeTransferirSensorLogs ?? false;
    public static bool IsServicoDoComandoEnabled() => ResolveMatchController()?.EnableServicoDoComandoSensorLogs ?? false;
    public static bool IsPodePousarEnabled() => ResolveMatchController()?.EnablePodePousarSensorLogs ?? false;
    public static bool IsPodeDecolarEnabled() => ResolveMatchController()?.EnablePodeDecolarSensorLogs ?? false;

    public static void Log(string sensorTag, string message)
    {
        if (string.IsNullOrWhiteSpace(sensorTag))
            sensorTag = "Sensor";
        Debug.Log($"[{sensorTag}] {message}");
    }
}
