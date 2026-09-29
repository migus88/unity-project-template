using System;
using System.Diagnostics;

namespace Core.Logging
{
    public static class Log
    {
        [Conditional("GAME_LOG_VERBOSE")]
        public static void Verbose(LogTag tag, string message)
        {
            UnityEngine.Debug.Log(Format(tag, message));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(LogTag tag, string message)
        {
            UnityEngine.Debug.Log(Format(tag, message));
        }

        public static void Warn(LogTag tag, string message)
        {
            UnityEngine.Debug.LogWarning(Format(tag, message));
        }

        public static void Error(LogTag tag, string message)
        {
            UnityEngine.Debug.LogError(Format(tag, message));
        }

        public static void Exception(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }

        private static string Format(LogTag tag, string message)
        {
            return $"[{tag.Name}] {message}";
        }
    }
}
