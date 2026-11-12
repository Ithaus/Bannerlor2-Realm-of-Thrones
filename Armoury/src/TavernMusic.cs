using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.Objects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 474-P4 (Jeff 13.11, projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md pkt 2 "Gwar i muzyka", paczka 4): muzyka grajkow w karczmie miasta wedlug regionu.
    /// Dowod (dekompilacja SandBox.dll 1.4.8, SandBox.CampaignBehaviors.SettlementMusiciansCampaignBehavior): OnMissionStarted -> dla kazdej MusicianGroup sceny
    /// SetPlayList(CreateRandomPlayList(osada)). CreateRandomPlayList wazy kultury (krolestwa, miasta w poblizu, +10 kultura osady) i bierze po tyle utworow danej
    /// kultury, ile wypadnie z proporcji do liczby utworow kultury o najwiekszej wadze; gdy wyjdzie pusto - CALA lista (wszystkie kultury). Utwory sa tylko dla 6
    /// kultur gry (SandBox/ModuleData/settlement_tracks.xml: aserai, battania, empire, khuzait, sturgia, vlandia - po 2 "tavern" i 2 "lords_hall"), a ROT trzyma
    /// te id dla wlasnych krain (battania = Polnoc, vlandia = Westerlands, aserai = Dorne, sturgia = Zelazne Wyspy, empire = Braavos, khuzait = Dothrakowie).
    /// Kultury ROT bez utworow (river, reach, vale, stormlands, crownlands, ...) maja najwieksza wage i 0 utworow -> num8 = 0 -> pusta lista -> wszystko naraz
    /// (w Dorzeczu Dornijczycy i Dothrakowie). Tylko SandBox.dll ma ten typ (grep po Modules).
    /// Zmiana: postfiks CreateRandomPlayList - w lokacji "tavern" przy kulturze osady z mapy Region() lista = utwory "tavern" kultur gry tego regionu (pierwsza - wlasna
    /// muzyka regionu, druga - pokrewna), przemieszane. Kultura spoza mapy albo brak utworow - lista gry bez zmian. Liczone raz przy wejsciu do karczmy (zero pracy na mapie).
    /// Klucz TavernMusicByRegion NIE = postfiks nic nie robi (lista gry co do bitu).
    /// </summary>
    internal static class TavernMusic
    {
        private const string P = "Muzyka karczmy (474-P4): ";
        internal static bool On { get { var s = Settings.Current; return s != null && s.TavernMusicByRegion; } }

        private static Harmony _h;
        private static bool _tried, _patched;
        private static string _hook = "nie probowano";
        private static int _stumbles, _dSet, _dKept;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _seenUnmapped = new HashSet<string>(StringComparer.Ordinal);

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("TavernMusic." + where, e); } catch { }
        }

        /// <summary>Region ROT (id kultury osady) -> kultury gry z utworami karczmy: pierwsza - muzyka regionu, druga - pokrewna. null - lista gry bez zmian.</summary>
        internal static string[] Region(string culture)
        {
            switch (culture)
            {
                // Polnoc i za Murem
                case "battania": return new[] { "battania", "sturgia" };
                case "nightswatch": return new[] { "sturgia", "battania" };
                case "skagosi": return new[] { "sturgia", "battania" };
                case "freefolk": case "wildlings": return new[] { "battania", "sturgia" };
                // Zelazne Wyspy
                case "sturgia": return new[] { "sturgia" };
                // krolestwa poludnia Westeros
                case "vlandia": return new[] { "vlandia" };
                case "reach": return new[] { "vlandia" };
                case "river": return new[] { "vlandia", "empire" };
                case "vale": return new[] { "vlandia", "battania" };
                case "stormlands": return new[] { "vlandia", "battania" };
                case "crownlands": return new[] { "vlandia", "empire" };
                case "dragonstone": return new[] { "empire", "vlandia" };
                // Dorne
                case "aserai": return new[] { "aserai" };
                // Wolne Miasta i Valyria
                case "empire": return new[] { "empire" };
                case "pentoshi": case "myrish": case "lyseni": case "tyroshi": case "volantine": case "valyrian": return new[] { "empire", "aserai" };
                case "norvos": case "qohorik": case "nord": return new[] { "empire", "sturgia" };
                case "ibbenese": return new[] { "sturgia" };
                // Zatoka Niewolniczych Miast, wschod, Wyspy Letnie
                case "ghiscari": case "qartheen": return new[] { "aserai", "khuzait" };
                case "yiti": return new[] { "khuzait", "aserai" };
                case "sarnor": return new[] { "khuzait", "empire" };
                case "khuzait": return new[] { "khuzait" };
                case "summer": return new[] { "aserai" };
                default: return null;
            }
        }

        /// <summary>W kampanii (OnSessionLaunched), raz na proces: postfiks na SettlementMusiciansCampaignBehavior.CreateRandomPlayList (DeclaredMethod na klasie deklarujacej).</summary>
        internal static void EnsurePatch()
        {
            if (_tried) { Log.Info(P + "latka " + _hook + " | klucz Tavern Music By Region " + (On ? "TAK" : "NIE") + "."); return; }
            _tried = true;
            try
            {
                var t = AccessTools.TypeByName("SandBox.CampaignBehaviors.SettlementMusiciansCampaignBehavior");
                var m = t != null ? AccessTools.DeclaredMethod(t, "CreateRandomPlayList", new[] { typeof(Settlement) }) : null;
                if (m == null) _hook = "BRAK (" + (t == null ? "typ SettlementMusiciansCampaignBehavior" : "CreateRandomPlayList(Settlement)") + ") - muzyka gry bez zmian";
                else
                {
                    _h = _h ?? new Harmony("com.jeff.armoury.474music");
                    _h.Patch(m, postfix: new HarmonyMethod(typeof(TavernMusic), nameof(Postfix)));
                    _patched = HasOurs(m);   // CLAUDE.md 7: wyjatek Harmony bywa polykany - sprawdzamy, czy postfiks naprawde siedzi
                    _hook = (_patched ? "wpieta " : "NIE weszla (brak postfiksu po Patch) - muzyka gry bez zmian: ") + m.DeclaringType.FullName + "." + m.Name + (_patched ? " (postfiks)" : "");
                }
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name + " - muzyka gry bez zmian"; Stumble("EnsurePatch", e); }
            Log.Info(P + "latka " + _hook + " | klucz Tavern Music By Region " + (On ? "TAK" : "NIE") + ".");
        }

        private static bool HasOurs(MethodBase m)
        {
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) return false;
            foreach (var x in pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(TavernMusic)) return true;
            return false;
        }

        private static void Postfix(Settlement settlement, ref List<SettlementMusicData> __result)
        {
            if (!On) return;
            try
            {
                var cm = CampaignMission.Current;
                string loc = cm != null && cm.Location != null ? cm.Location.StringId : null;
                if (loc != "tavern" || settlement == null || settlement.Culture == null) return;
                string cid = settlement.Culture.StringId;
                var want = Region(cid);
                if (want == null)
                {
                    _dKept++;
                    if (_seenUnmapped.Add(cid ?? "-")) Log.Info(P + settlement.Name + " (" + settlement.StringId + ", kultura " + cid + ") - kultura spoza mapy regionow, lista gry bez zmian (" + Count(__result) + " utworow).");
                    return;
                }
                var all = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObjectTypeList<SettlementMusicData>() : null;
                if (all == null) return;
                var list = new List<SettlementMusicData>();
                var per = new int[want.Length];
                foreach (var md in all)
                {
                    if (md == null || md.LocationId != "tavern" || md.Culture == null) continue;
                    int k = Array.IndexOf(want, md.Culture.StringId);
                    if (k < 0) continue;
                    list.Add(md); per[k]++;
                }
                if (list.Count == 0)
                {
                    _dKept++;
                    Log.Info(P + settlement.Name + " (" + cid + ") - brak utworow karczmy dla " + string.Join("/", want) + ", lista gry bez zmian (" + Count(__result) + ").");
                    return;
                }
                list.Shuffle();
                int before = Count(__result);
                __result = list;
                _dSet++;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < want.Length; i++) sb.Append(i > 0 ? ", " : "").Append(want[i]).Append(' ').Append(per[i]);
                Log.Info(P + settlement.Name + " (" + settlement.StringId + ", kultura " + cid + ") -> " + sb + " (utworow " + list.Count + ", lista gry miala " + before + ")"
                         + " | ustawione " + _dSet + "x, bez zmian " + _dKept + "x, potkniecia " + _stumbles + " od startu gry.");
            }
            catch (Exception e) { Stumble("Postfix", e); }
        }

        private static int Count(List<SettlementMusicData> l) { return l != null ? l.Count : 0; }
    }
}
