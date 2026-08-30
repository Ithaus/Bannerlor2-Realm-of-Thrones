using System;
using System.IO;
using TaleWorlds.Library;

namespace GrandTourney
{
    internal static class Log
    {
        private static string _path;
        private static readonly object Gate = new object();

        internal static void Init(string moduleDir)
        {
            try
            {
                _path = Path.Combine(moduleDir, "GrandTourney.log");
                File.WriteAllText(_path, "=== Grand Tourney " + DateTime.Now + " ===" + Environment.NewLine);
            }
            catch { _path = null; }
        }

        internal static void Info(string msg)
        {
            if (_path == null || !Settings.Current.LogEnabled) return;
            try
            {
                lock (Gate) File.AppendAllText(_path, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }

        internal static void Error(string where, Exception e)
        {
            if (_path == null) return;
            try
            {
                lock (Gate) File.AppendAllText(_path, "[" + DateTime.Now.ToString("HH:mm:ss") + "] ERROR in " + where + ": " + e + Environment.NewLine);
            }
            catch { }
        }

        internal static void Player(string text, bool bad = false)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(text,
                    bad ? Colors.Red : new Color(0.9f, 0.8f, 0.4f)));
            }
            catch { }
        }
    }
}
