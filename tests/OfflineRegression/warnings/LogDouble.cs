using System;
using System.Collections.Generic;

namespace AllLive.UWP.Helper
{
    public enum LogType { INFO, DEBUG, ERROR, FATAL }

    public static class LogHelper
    {
        public static readonly List<LogType> Levels = new List<LogType>();
        public static Action<LogType> OnLog;

        public static void Log(string message, LogType type, Exception ex = null)
        {
            Levels.Add(type);
            OnLog?.Invoke(type);
        }

        public static void Reset()
        {
            Levels.Clear();
            OnLog = null;
        }
    }
}
