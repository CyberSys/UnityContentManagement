using UnityEngine;

public partial class ContentDatabase : ScriptableObject
{
    [Header("Show loading progress in console/logs")]
    [SerializeField]
    private bool Logging = true;

    public static void Log(string text)
    {
        if (!Get().Logging)
            return;

        Debug.Log($"[{nameof(ContentDatabase)}] {text}");
    }

    public static void LogWarning(string text)
    {
        if (!Get().Logging)
            return;

        Debug.LogWarning($"[{nameof(ContentDatabase)}] {text}");
    }

    public static void LogError(string text)
    {
        if (!Get().Logging)
            return;

        Debug.LogError($"[{nameof(ContentDatabase)}] {text}");
    }
}
