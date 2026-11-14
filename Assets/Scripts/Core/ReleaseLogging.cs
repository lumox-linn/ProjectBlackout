using UnityEngine;

// Turns off normal Debug.Log messages in the final (non-development) build.
// Warnings, errors and crashes still get logged, and the Editor keeps every log.
public static class ReleaseLogging
{
    // runs automatically when the game starts; no GameObject needed
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Debug.unityLogger.filterLogType = Debug.isDebugBuild ? LogType.Log : LogType.Warning;
    }
}