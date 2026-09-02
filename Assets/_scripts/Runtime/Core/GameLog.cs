using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Yunus.Game.Core
{
    /// <summary>
    /// Routine trace logging for the game systems. Off by default so the console stays clean.
    ///
    /// Use it for "what just happened" narration (level loaded, grid built, snap succeeded).
    /// Genuine problems must still go through <c>UnityEngine.Debug.LogWarning</c> / <c>LogError</c>
    /// directly - those are always shown.
    ///
    /// The <see cref="ConditionalAttribute"/> markers strip every <see cref="Info(string)"/> call
    /// (and its argument evaluation) from non-development player builds; in the editor the calls run
    /// but only print when <see cref="Verbose"/> is true (toggle it, or define GAMELOG_VERBOSE).
    /// </summary>
    public static class GameLog
    {
        public static bool Verbose =
#if GAMELOG_VERBOSE
            true;
#else
            false;
#endif

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message)
        {
            if (Verbose) Debug.Log(message);
        }
    }
}
