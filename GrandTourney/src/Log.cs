using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;

namespace GrandTourney
{
    internal static class Log
    {
        private static string _path;
        private static readonly object Gate = new object();
        private const int KeepLogs = 12;      // wpis 496: ile ostatnich sesji trzymamy (jak Armoury)

        internal static void Init(string moduleDir)
        {
            try
            {
                // wpis 496: log z data w nazwie (jak Armoury) - nowa sesja / autotest nie nadpisuje poprzedniego
                _path = Path.Combine(moduleDir, "GrandTourney-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                File.WriteAllText(_path, "=== Grand Tourney " + DateTime.Now + " ===" + Environment.NewLine);
                // stary plik o stalej nazwie nigdy juz nie dostanie ani linii - znika (jak Armoury.log)
                try { var legacy = Path.Combine(moduleDir, "GrandTourney.log"); if (File.Exists(legacy)) File.Delete(legacy); } catch { }
                Prune(moduleDir);
            }
            catch { _path = null; }
        }

        /// <summary>Zostawia KeepLogs najnowszych logow sesji, reszte kasuje (kopia Armoury Log.Prune).</summary>
        private static void Prune(string dir)
        {
            try
            {
                var files = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(dir, "GrandTourney-*.log")) files.Add(new FileInfo(f));
                files.Sort(delegate (FileInfo a, FileInfo b) { return b.CreationTimeUtc.CompareTo(a.CreationTimeUtc); });
                for (int i = KeepLogs; i < files.Count; i++) { try { files[i].Delete(); } catch { } }
            }
            catch { }
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
