using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;

namespace Armoury
{
    internal static class Log
    {
        private static string _path, _topicDir;
        private static readonly object Gate = new object();
        private const int KeepLogs = 12;      // ile ostatnich sesji trzymamy

        /// <summary>
        /// PLIK NA SESJE (Jeff 13.09: "logi gina, zanim zdazysz o bledzie powiedziec").
        /// Init robil File.WriteAllText na STALEJ nazwie, wiec kazde odpalenie gry
        /// kasowalo dowody z poprzedniej sesji. 13.09 przepadl przez to caly zapis
        /// niewoli, w ktorej Jeff stracil rynsztunek - uratowalo nas tylko to, ze
        /// audyt przeczytal plik przed jego restartem. Teraz jak w CrashScribe:
        /// osobny plik ze znacznikiem czasu, stare kasujemy dopiero powyzej KeepLogs.
        /// </summary>
        internal static void Init(string moduleDir)
        {
            try
            {
                _path = Path.Combine(moduleDir, "Armoury-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                File.WriteAllText(_path, "=== Armoury " + DateTime.Now + " ===" + Environment.NewLine);
                // stary plik o stalej nazwie nigdy juz nie dostanie ani linii - znika,
                // zeby nikt (ani Jeff, ani Claude) nie czytal za rok zamrozonych bzdur
                try { var legacy = Path.Combine(moduleDir, "Armoury.log"); if (File.Exists(legacy)) File.Delete(legacy); } catch { }
                Prune(moduleDir);
                // wpis 63 (Jeff 04.10: "ten log sie robi tak dlugi, ze za duzo tam jest smieci - trzeba to uporzadkowac"):
                // szczegoly ida do osobnych plikow tematycznych w Logs/<sesja>/, glowny log = start, bledy, podsumowania dnia
                try
                {
                    var root = Path.Combine(moduleDir, "Logs");
                    _topicDir = Path.Combine(root, Path.GetFileNameWithoutExtension(_path).Replace("Armoury-", ""));
                    Directory.CreateDirectory(_topicDir);
                    var dirs = new List<DirectoryInfo>();
                    foreach (var d in Directory.GetDirectories(root)) dirs.Add(new DirectoryInfo(d));
                    dirs.Sort(delegate (DirectoryInfo a, DirectoryInfo b) { return b.CreationTimeUtc.CompareTo(a.CreationTimeUtc); });
                    for (int i = KeepLogs; i < dirs.Count; i++) { try { dirs[i].Delete(true); } catch { } }
                }
                catch { _topicDir = null; }
            }
            catch { _path = null; }
        }

        /// <summary>Zostawia KeepLogs najnowszych logow sesji, reszte kasuje.</summary>
        private static void Prune(string dir)
        {
            try
            {
                var files = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(dir, "Armoury-*.log")) files.Add(new FileInfo(f));
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
                string topic = TopicOf(msg);
                string file = topic != null && _topicDir != null ? Path.Combine(_topicDir, topic + ".log") : _path;
                lock (Gate) File.AppendAllText(file, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }

        /// <summary>
        /// Plik CSV w katalogu sesji (Logs/&lt;sesja&gt;/nazwa): naglowek przy pierwszym zapisie, potem dopisywanie wierszy.
        /// Zwraca sciezke pliku albo null (log wylaczony, brak katalogu sesji, blad zapisu). Wiersze koncza sie znakiem nowej linii.
        /// </summary>
        internal static string Csv(string name, string header, string rows)
        {
            if (_topicDir == null || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rows)) return null;
            try
            {
                if (!Settings.Current.LogEnabled) return null;
                string file = Path.Combine(_topicDir, name);
                lock (Gate)
                {
                    if (!File.Exists(file)) File.WriteAllText(file, header + Environment.NewLine);
                    File.AppendAllText(file, rows);
                }
                return file;
            }
            catch { return null; }
        }

        /// <summary>Plik tematyczny dla szczegolowej linii (null = glowny log). Podsumowania dnia zostaja w glownym.</summary>
        private static string TopicOf(string m)
        {
            if (m == null) return null;
            if (m.StartsWith("Kronika unikatow") || m.StartsWith("UniqueSpoils")) return "unikaty";
            if (m.StartsWith("Bitwa:")) return "bitwy";
            if (m.StartsWith("Balans krolestw (175) wedlug krolestw")) return "balans";   // 175.0: szczegoly wedlug krolestw (krotka linia dnia zostaje w glownym)
            if (m.StartsWith("Budowa:")) return "budowy";
            if (m.StartsWith("Finanse:")) return "finanse";
            if (m.StartsWith("ZakupyAI: dzien") || m.StartsWith("PodazPopyt: kupcy")) return null;
            if (m.StartsWith("ZakupyAI:") || m.StartsWith("Stajnia AI:") || m.StartsWith("Oferta:")) return "zakupy";
            if (m.StartsWith("PodazPopyt:")) return "handel";
            if (m.StartsWith("Warsztaty (diagnoza)")) return "warsztaty";
            if (m.StartsWith("AiNightCamp")) return "noc";
            if (m.StartsWith("Wioski: ogien") || m.StartsWith("Wioski: zgaszony") || m.StartsWith("Wioski: drzewo")) return "wioski";   // W2: kazdy rabunek i drzewa wsi-matek (diagnostyka) - do pliku tematycznego; start, wypelnienie i podsumowania w glownym
            if (m.StartsWith("UniqueLaw: zamiennik") || m.StartsWith("LegendaryLaw: zamiennik") || m.StartsWith("TroopFit:   ")
                || m.StartsWith("Uniques: ") || m.StartsWith("Rozrzut miotanych:") || m.StartsWith("Podloga zlomu:")) return "start";
            return null;
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
