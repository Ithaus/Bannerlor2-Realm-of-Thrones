using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>403 (PLAN-396 commit 403) - ROCZNY SUFIT SMIERCI BOHATEROW Z NASZYCH REGUL (rany 403, loch 404, pozniej wyrok 409). Jedno konto na swiat:
    /// ile bohaterow zabil w tym roku nasz kod, z podzialem na zrodlo. Pyta i pisze TYLKO HeroFate.TryKill (jedno API smierci).
    ///
    /// Rok = CampaignTime.Now.GetYear (kalendarz Armoury 364 dni przy Long Year; bez niego rok gry 84 dni - sufit jest "na rok kalendarza, ktory widzi gracz").
    /// Sufity: Lord Deaths Max Per Year (wspolny dla wszystkich zrodel) i Hero Wounds Max Deaths Per Year (zrodlo "wounds"). 0 = nikt nie umiera ta droga.
    /// Po przekroczeniu modul dalej dziala - TryKill odmawia z powodem "sufit roku", liczone. Zapis arm_lordtoll_v1: "rok|zrodlo:liczba,.." (stary zapis =
    /// puste konto). Reset w konstruktorze ArmouryBehavior i w OnGameEnd.</summary>
    internal static class LordToll
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _year = int.MinValue;
        private static readonly Dictionary<string, int> _n = new Dictionary<string, int>(StringComparer.Ordinal);

        internal const string Wounds = "wounds", Dungeon = "dungeon", Sentence = "sentence";

        internal static void Reset() { _year = int.MinValue; _n.Clear(); }

        internal static string Export()
        {
            if (_year == int.MinValue) return "";
            var sb = new StringBuilder(48);
            sb.Append(_year.ToString(Inv)).Append('|');
            bool first = true;
            foreach (var kv in _n)
            {
                if (kv.Value <= 0) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
            }
            return sb.ToString();
        }

        internal static void Import(string text)
        {
            Reset();
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var p = text.Split('|');
                int y;
                if (p.Length < 1 || !int.TryParse(p[0], NumberStyles.Integer, Inv, out y)) return;
                _year = y;
                if (p.Length < 2 || p[1].Length == 0) return;
                foreach (var part in p[1].Split(','))
                {
                    int i = part.IndexOf(':');
                    int v;
                    if (i <= 0 || !int.TryParse(part.Substring(i + 1), NumberStyles.Integer, Inv, out v) || v <= 0) continue;
                    _n[part.Substring(0, i)] = v;
                }
            }
            catch (Exception e) { Reset(); Log.Error("LordToll.Import", e); }
        }

        /// <summary>Nowy rok kalendarza = konto od zera (linia do logu, gdy stary rok mial zgony).</summary>
        private static void Roll()
        {
            if (Campaign.Current == null) return;
            int y = CampaignTime.Now.GetYear;
            if (y == _year) return;
            if (_year != int.MinValue && Total > 0)
                Log.Info("LordToll: rok " + _year + " zamkniety - smierci bohaterow z naszych regul: " + Describe() + ". Nowy rok " + y + " od zera.");
            _year = y; _n.Clear();
        }

        internal static int Year { get { Roll(); return _year; } }

        internal static int Count(string source)
        {
            Roll();
            int v;
            return source != null && _n.TryGetValue(source, out v) ? v : 0;
        }

        internal static int Total
        {
            get { int t = 0; foreach (var kv in _n) t += kv.Value; return t; }
        }

        internal static int MaxAll { get { var s = Settings.Current; return s != null ? Math.Max(0, s.LordDeathsMaxPerYear) : 0; } }
        internal static int MaxWounds { get { var s = Settings.Current; return s != null ? Math.Max(0, s.HeroWoundsMaxDeathsPerYear) : 0; } }

        /// <summary>Czy w tym roku wolno jeszcze zabic bohatera z tego zrodla (sufit wspolny i sufit zrodla). why: opis do logu przy odmowie.</summary>
        internal static bool Allow(string source, out string why)
        {
            why = null;
            Roll();
            int all = Total, maxAll = MaxAll;
            if (all >= maxAll) { why = "sufit roku (wspolny " + all + "/" + maxAll + ")"; return false; }
            if (source == Wounds)
            {
                int w = Count(Wounds), maxW = MaxWounds;
                if (w >= maxW) { why = "sufit roku (rany " + w + "/" + maxW + ")"; return false; }
            }
            return true;
        }

        /// <summary>Smierc wykonana - dopisz do konta roku.</summary>
        internal static void Note(string source)
        {
            Roll();
            if (string.IsNullOrEmpty(source)) source = "?";
            int v;
            _n.TryGetValue(source, out v);
            _n[source] = v + 1;
        }

        /// <summary>"wounds 3, dungeon 1" (albo "-").</summary>
        internal static string Describe()
        {
            if (_n.Count == 0) return "-";
            var sb = new StringBuilder();
            foreach (var kv in _n) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
            return sb.ToString();
        }
    }
}
