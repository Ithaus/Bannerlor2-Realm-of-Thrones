using System;
using System.IO;
using System.Xml;

namespace CrashScribe
{
    internal static class Config
    {
        // --- co lapiemy ---
        public static bool CatchUnhandled = true;      // bledy, ktore i tak wywala gre
        public static bool CatchFirstChance = true;    // bledy zlapane gdzies dalej - czesto prawdziwa przyczyna
        public static bool OnlyModdedFirstChance = true; // ...ale tylko te, w ktorych widac jakiegos moda
        public static bool WrapModMethods = true;      // siec Harmony na metodach naszych modow
        public static bool SwallowModErrors = true;    // zlapany blad w modzie: zapisz i jedz dalej zamiast CTD
        public static bool WrapCampaignHotspots = true;// dodatkowa siec na newralgicznych miejscach gry
        public static bool ShowInGame = true;          // krotka czerwona notka na ekranie, ze cos poszlo nie tak
        public static int MaxReportsPerSession = 300;
        public static int KeepSessions = 15;

        // mody objete siecia; puste = wszystkie spoza Native
        public static string WrapModules = "RealisticCaptivity,GrandTourney,Armoury";

        // --- werbunek ---
        public static bool LocalRecruits = true;   // ochotnicy wedle kultury OSADY, nie notabla (chlopi z Polnocy to Polnocnicy)

        // --- rozrusznik fabuly ROT (os serialu + tempo nadrabiania) ---
        public static bool FabulaPacerEnabled = true;  // fabula ROT idzie po kolei, wedle osi serialu
        public static int FabulaPaceDays = 4;          // najwyzej jedno wydarzenie fabularne na tyle dni
        public static float FabulaTimeScale = 2.0f;    // rozciagniecie calej osi fabuly: 1.0 = daty ROT bez zmian, 2.0 = wszystko dwa razy dalej (przy dluzszym roku, zeby lata zgadzaly sie z serialem)

        internal static void Load(string moduleDataDir)
        {
            try
            {
                var path = Path.Combine(moduleDataDir, "CrashScribe.settings.xml");
                if (!File.Exists(path)) return;
                var doc = new XmlDocument();
                doc.Load(path);
                var root = doc.DocumentElement;
                if (root == null) return;
                foreach (XmlNode n in root.ChildNodes)
                {
                    if (n.NodeType != XmlNodeType.Element) continue;
                    var v = n.InnerText.Trim();
                    switch (n.Name)
                    {
                        case "CatchUnhandled": CatchUnhandled = B(v); break;
                        case "CatchFirstChance": CatchFirstChance = B(v); break;
                        case "OnlyModdedFirstChance": OnlyModdedFirstChance = B(v); break;
                        case "WrapModMethods": WrapModMethods = B(v); break;
                        case "SwallowModErrors": SwallowModErrors = B(v); break;
                        case "WrapCampaignHotspots": WrapCampaignHotspots = B(v); break;
                        case "ShowInGame": ShowInGame = B(v); break;
                        case "MaxReportsPerSession": MaxReportsPerSession = I(v, MaxReportsPerSession); break;
                        case "KeepSessions": KeepSessions = I(v, KeepSessions); break;
                        case "WrapModules": WrapModules = v; break;
                        case "LocalRecruits": LocalRecruits = B(v); break;
                        case "FabulaPacerEnabled": FabulaPacerEnabled = B(v); break;
                        case "FabulaPaceDays": FabulaPaceDays = I(v, FabulaPaceDays); break;
                        case "FabulaTimeScale": FabulaTimeScale = F(v, FabulaTimeScale); break;
                    }
                }
            }
            catch { }
        }

        private static bool B(string v) { return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); }
        private static int I(string v, int d) { int r; return int.TryParse(v, out r) ? r : d; }
        private static float F(string v, float d)
        {
            float r;
            return float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out r) ? r : d;
        }
    }
}
