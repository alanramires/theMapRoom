using UnityEngine;

/// <summary>
/// Applies the WebGL frame-rate budget before the first scene is loaded.
/// </summary>
public static class WebGLPerformance
{
    private const int TargetFrameRate = 30;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
#endif
    }
}
