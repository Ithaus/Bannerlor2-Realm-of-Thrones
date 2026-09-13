using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;

namespace RealisticCaptivity
{
    internal static class Log
    {
        private static string _path;
        private static readonly object Gate = new object();

        private const int KeepLogs = 12;      // ile ostatnich sesji trzymamy

        /// <summary>
        /// PLIK NA SESJE (Jeff 13.09: "logi gina, zanim zdazysz o bledzie powiedziec").
        /// Init robil File.WriteAllText na STALEJ nazwie, wiec kazde odpalenie gry
        /// kasowalo dowody z poprzedniej sesji - 13.09 przepadl przez to caly zapis
        /// niewoli, w ktorej Jeff stracil rynsztunek (11 sztuk, 43196). Teraz jak
        /// w CrashScribe: osobny plik ze znacznikiem czasu, stare kasujemy dopiero
        /// powyzej KeepLogs sztuk.
        /// </summary>
        internal static void Init(string moduleDir)
        {
            try
            {
                _path = Path.Combine(moduleDir, "RealisticCaptivity-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                File.WriteAllText(_path, "=== Realistic Captivity " + DateTime.Now + " ===" + Environment.NewLine);
                // stary plik o stalej nazwie nigdy juz nie dostanie ani linii - znika,
                // zeby nikt nie czytal za rok zamrozonych bzdur jako swiezego logu
                try { var legacy = Path.Combine(moduleDir, "RealisticCaptivity.log"); if (File.Exists(legacy)) File.Delete(legacy); } catch { }
                Prune(moduleDir);
            }
            catch { _path = null; }
        }

        /// <summary>Zostawia KeepLogs najnowszych logow sesji, reszte kasuje.</summary>
        private static void Prune(string dir)
        {
            try
            {
                var files = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(dir, "RealisticCaptivity-*.log")) files.Add(new FileInfo(f));
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
