using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameLogCategory
{
    General = 0,
    SaveLoad = 1,
    Replay = 2,
    Match = 3,
    UI = 4,
    Sensors = 5,
    Audio = 6,
    AI = 7,
    Planning = 8,
    Camera = 9
}

public enum GameLogLevel
{
    Error = 0,
    Warning = 1,
    Info = 2,
    Verbose = 3
}

[Serializable]
public sealed class GameLogCategorySetting
{
    public GameLogCategory category = GameLogCategory.General;
    public bool enabled = true;
    public GameLogLevel maxLevel = GameLogLevel.Info;
}

[DefaultExecutionOrder(-10000)]
public class LogManager : MonoBehaviour
{
    private static LogManager instance;

    [Header("Global")]
    [SerializeField] private bool enableLogs = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Categories")]
    [SerializeField] private List<GameLogCategorySetting> categories = new List<GameLogCategorySetting>();

    private readonly Dictionary<GameLogCategory, GameLogCategorySetting> cacheByCategory = new Dictionary<GameLogCategory, GameLogCategorySetting>();

    public static LogManager Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (dontDestroyOnLoad)
        {
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        EnsureCategoriesComplete();
        RebuildCache();
    }

    private void OnValidate()
    {
        EnsureCategoriesComplete();
        RebuildCache();
    }

    public bool IsEnabled(GameLogCategory category, GameLogLevel level)
    {
        if (!enableLogs)
            return false;

        if (!cacheByCategory.TryGetValue(category, out GameLogCategorySetting setting) || setting == null)
            return true;

        return setting.enabled && level <= setting.maxLevel;
    }

    public static bool ShouldLog(GameLogCategory category, GameLogLevel level)
    {
        if (instance == null)
            return true;

        return instance.IsEnabled(category, level);
    }

    public static void Log(GameLogCategory category, GameLogLevel level, string message, UnityEngine.Object context = null)
    {
        if (!ShouldLog(category, level))
            return;

        string finalMessage = $"[{category}][{level}] {message}";
        switch (level)
        {
            case GameLogLevel.Error:
                if (context != null) Debug.LogError(finalMessage, context);
                else Debug.LogError(finalMessage);
                break;
            case GameLogLevel.Warning:
                if (context != null) Debug.LogWarning(finalMessage, context);
                else Debug.LogWarning(finalMessage);
                break;
            default:
                if (context != null) Debug.Log(finalMessage, context);
                else Debug.Log(finalMessage);
                break;
        }
    }

    public static void Info(GameLogCategory category, string message, UnityEngine.Object context = null)
    {
        Log(category, GameLogLevel.Info, message, context);
    }

    public static void Warning(GameLogCategory category, string message, UnityEngine.Object context = null)
    {
        Log(category, GameLogLevel.Warning, message, context);
    }

    public static void Error(GameLogCategory category, string message, UnityEngine.Object context = null)
    {
        Log(category, GameLogLevel.Error, message, context);
    }

    public static void Verbose(GameLogCategory category, string message, UnityEngine.Object context = null)
    {
        Log(category, GameLogLevel.Verbose, message, context);
    }

    [ContextMenu("Enable Info For All")]
    public void EnableInfoForAll()
    {
        for (int i = 0; i < categories.Count; i++)
        {
            GameLogCategorySetting setting = categories[i];
            if (setting == null)
                continue;
            setting.enabled = true;
            setting.maxLevel = GameLogLevel.Info;
        }

        RebuildCache();
    }

    [ContextMenu("Enable Verbose For All")]
    public void EnableVerboseForAll()
    {
        for (int i = 0; i < categories.Count; i++)
        {
            GameLogCategorySetting setting = categories[i];
            if (setting == null)
                continue;
            setting.enabled = true;
            setting.maxLevel = GameLogLevel.Verbose;
        }

        RebuildCache();
    }

    [ContextMenu("Disable All")]
    public void DisableAll()
    {
        for (int i = 0; i < categories.Count; i++)
        {
            GameLogCategorySetting setting = categories[i];
            if (setting == null)
                continue;
            setting.enabled = false;
        }

        RebuildCache();
    }

    private void EnsureCategoriesComplete()
    {
        if (categories == null)
            categories = new List<GameLogCategorySetting>();

        Array enumValues = Enum.GetValues(typeof(GameLogCategory));
        for (int i = 0; i < enumValues.Length; i++)
        {
            GameLogCategory category = (GameLogCategory)enumValues.GetValue(i);
            if (TryFindSettingIndex(category) >= 0)
                continue;

            categories.Add(new GameLogCategorySetting
            {
                category = category,
                enabled = true,
                maxLevel = GameLogLevel.Info
            });
        }
    }

    private int TryFindSettingIndex(GameLogCategory category)
    {
        if (categories == null)
            return -1;

        for (int i = 0; i < categories.Count; i++)
        {
            GameLogCategorySetting setting = categories[i];
            if (setting != null && setting.category == category)
                return i;
        }

        return -1;
    }

    private void RebuildCache()
    {
        cacheByCategory.Clear();
        if (categories == null)
            return;

        for (int i = 0; i < categories.Count; i++)
        {
            GameLogCategorySetting setting = categories[i];
            if (setting == null)
                continue;
            cacheByCategory[setting.category] = setting;
        }
    }
}
