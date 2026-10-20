using System;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// R3-10c (POMYSLY-35-2026-10-20 nr 33, Jeff 20.10 "pozostale tak"; audyt docs/audyt-2026-10-09/13-UMIEJETNOSCI.md Z12): Medycyna i Taktyka
    /// w regule "przegrani uchodza" (H3, LosersFlee.Plan). Dla gracza: dobry chirurg pokonanych ratuje czesc ludzi, ktorzy zgineliby w ucieczce
    /// (najwyzej czwarta czesc zabitych, pelne przy Medycynie 250), a lepszy taktyk zwyciezcy zabija w poscigu wiecej (najwyzej +10 punktow
    /// procentowych), gorszy - mniej (najwyzej -10). Widelki 5-65% zostaja.
    ///
    /// Wzor (na partie przegrana, p - wynik sytuacji z LosersFlee.Plan, juz w granicach 5-65%):
    ///   p1 = granice(p + 0.10 x clamp((Tw - Tp) / 300, -1, 1))          - Taktyka wodzow stron (raz na bitwe)
    ///   p2 = granice(p1 x (1 - 0.25 x min(1, Med / 250)))               - Medycyna chirurga tej partii przegranej
    /// Tw / Tp - Taktyka wodza strony: MapEventSide.LeaderParty.LeaderHero (ten sam wodz, ktorego gra bierze do symulacji -
    /// MapEventSide.LeaderSimulationModifier); gdy wodz strony nie jest bohaterem (np. zaloga osady na czele) - najlepsza Taktyka wsrod bohaterow
    /// prowadzacych partie tej strony; bez zadnego bohatera - 0 (banda, karawana bez bohatera: nikt nie dowodzi, jak w grze).
    /// Med - Medycyna chirurga partii (MobileParty.EffectiveSurgeon: wyznaczony chirurg w partii, inaczej jej wodz; bez bohatera - 0, bez zmiany).
    /// Tabory wsi (stala smiertelnosc NonCombatantDeathPercent) - bez zmian: nie walcza i nie maja chirurga. Zwyciezcy - bez zmian (limit
    /// WinnerDeathCapPercent). Zmienia sie tylko PODZIAL przegranych (zabici / zywi), suma ludzi ta sama - zywi ida jak dotad do niewoli albo
    /// do domu (zamknieta ksiega ludzi H3 bez zmian).
    ///
    /// Wylacznik LosersFleeSkills (off = p jak dotad). Watek: prefiks wyniku bitwy (watek glowny), bez alokacji w liczeniu; napisy tylko w linii
    /// bitwy i linii doby. Zapisu nie ma (liczniki doby). Blad w liczeniu partii = p bez zmian i potkniecie (bez gaszenia funkcji).
    /// </summary>
    internal static class LoserSkills
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.LosersFleeSkills; } }

        private const float PMin = 0.05f, PMax = 0.65f;            // widelki H3 (LosersFlee.PMin / PMax - te same liczby)
        private const float TacW = 0.10f, TacSpan = 300f;          // Taktyka: najwyzej 10 punktow przy roznicy 300
        private const float MedW = 0.25f, MedFull = 250f;          // Medycyna: najwyzej 1/4 zabitych mniej, pelne przy 250

        // ostatnia bitwa (prefiks -> BattleLine w postfiksie tej samej bitwy)
        private static MapEvent _me;
        private static int _tw, _tp, _bParties, _bMedParties, _bMedMax;
        private static bool _twHero, _tpHero;
        private static float _shift, _bP2Min, _bP2Max;
        // partia w toku (PartyP -> Note po planie tej partii; inna partia albo blad planu = brak liczenia)
        private static MobileParty _pendMp;
        private static float _pendP0, _pendP1, _pendP2;
        private static int _pendMed;

        // liczniki doby
        private static int _battles, _tacUp, _tacDown, _tacEven, _noHeroW, _noHeroL;
        private static double _sumUp, _sumDown;
        private static int _parties, _men, _medParties, _medMen, _medFull;
        private static long _sumMed;
        private static double _e0, _e1, _e2;                        // oczekiwani zabici: sama sytuacja / z taktyka / z taktyka i chirurgiem
        private static float _p2Min = 1f, _p2Max;
        private static int _plMen; private static double _plE1, _plE2;   // partia gracza pokonana (autobitwa): bez chirurga / z chirurgiem
        private static int _plWinBattles; private static double _plWinShift;  // gracz wodzem zwyciezcow: bitwy i suma przesuniec
        private static int _stumble;
        private static bool _err;

        internal static void Reset()
        {
            ClearDay();
            _me = null; _pendMp = null; _err = false;
        }

        private static void ClearDay()
        {
            _battles = _tacUp = _tacDown = _tacEven = _noHeroW = _noHeroL = 0;
            _sumUp = _sumDown = 0;
            _parties = _men = _medParties = _medMen = _medFull = 0; _sumMed = 0;
            _e0 = _e1 = _e2 = 0; _p2Min = 1f; _p2Max = 0f;
            _plMen = 0; _plE1 = _plE2 = 0; _plWinBattles = 0; _plWinShift = 0;
            _stumble = 0;
        }

        private static float Bound(float p) { return float.IsNaN(p) ? PMin : (p < PMin ? PMin : (p > PMax ? PMax : p)); }

        /// <summary>Taktyka wodza strony (patrz naglowek); hero = czy byl jakis bohater-wodz.</summary>
        private static int SideTactics(MapEventSide side, out bool hero)
        {
            hero = false;
            if (side == null) return 0;
            var lp = side.LeaderParty;
            var h = lp != null ? lp.LeaderHero : null;
            if (h != null) { hero = true; return h.GetSkillValue(DefaultSkills.Tactics); }
            int best = 0;
            for (int i = 0; i < side.Parties.Count; i++)
            {
                var pb = side.Parties[i] != null ? side.Parties[i].Party : null;
                var ph = pb != null ? pb.LeaderHero : null;
                if (ph == null) continue;
                int v = ph.GetSkillValue(DefaultSkills.Tactics);
                if (!hero || v > best) best = v;
                hero = true;
            }
            return best;
        }

        /// <summary>
        /// Raz na bitwe objeta H3 (LosersFlee.Plan, po p sytuacji): przesuniecie p od Taktyki wodzow, 0 przy wylaczonym kluczu albo bledzie.
        /// </summary>
        internal static float Shift(MapEvent me, MapEventSide win, MapEventSide lose)
        {
            _me = null; _pendMp = null;
            if (!On || me == null) return 0f;
            try
            {
                bool hw, hl;
                int tw = SideTactics(win, out hw), tp = SideTactics(lose, out hl);
                float d = (tw - tp) / TacSpan;
                d = d < -1f ? -1f : (d > 1f ? 1f : d);
                float shift = TacW * d;
                _me = me; _tw = tw; _tp = tp; _twHero = hw; _tpHero = hl; _shift = shift;
                _bParties = _bMedParties = _bMedMax = 0; _bP2Min = 1f; _bP2Max = 0f;
                _battles++;
                if (!hw) _noHeroW++;
                if (!hl) _noHeroL++;
                if (shift > 0f) { _tacUp++; _sumUp += shift; }
                else if (shift < 0f) { _tacDown++; _sumDown += shift; }
                else _tacEven++;
                try
                {
                    var lp = win != null ? win.LeaderParty : null;
                    if (lp != null && lp == PartyBase.MainParty) { _plWinBattles++; _plWinShift += shift; }
                }
                catch { }
                return shift;
            }
            catch (Exception e)
            {
                _stumble++;
                if (!_err) { _err = true; Log.Error("LoserSkills.Shift", e); }
                return 0f;
            }
        }

        /// <summary>Smiertelnosc partii przegranej (nie taboru wsi) z Taktyka i chirurgiem; przy wylaczonym kluczu - p bez zmian.</summary>
        internal static float PartyP(float p, float shift, MobileParty mp)
        {
            _pendMp = null;
            if (!On) return p;
            try
            {
                float p1 = Bound(p + shift);
                Hero surgeon = mp != null ? mp.EffectiveSurgeon : null;
                int med = surgeon != null ? surgeon.GetSkillValue(DefaultSkills.Medicine) : 0;
                float m = med > 0 ? Math.Min(1f, med / MedFull) : 0f;
                float p2 = Bound(p1 * (1f - MedW * m));
                _pendMp = mp; _pendP0 = p; _pendP1 = p1; _pendP2 = p2; _pendMed = med;
                return p2;
            }
            catch (Exception e)
            {
                _stumble++;
                if (!_err) { _err = true; Log.Error("LoserSkills.PartyP", e); }
                return p;
            }
        }

        /// <summary>Po planie partii (LosersFlee.Plan, partia dodana do planu): liczniki doby i bitwy. n - szeregowi przegranej partii.</summary>
        internal static void Note(MobileParty mp, int n)
        {
            if (_pendMp == null || mp != _pendMp || n <= 0) { _pendMp = null; return; }
            _pendMp = null;
            _parties++; _men += n;
            _e0 += n * (double)_pendP0; _e1 += n * (double)_pendP1; _e2 += n * (double)_pendP2;
            if (_pendP2 < _p2Min) _p2Min = _pendP2;
            if (_pendP2 > _p2Max) _p2Max = _pendP2;
            _bParties++;
            if (_pendP2 < _bP2Min) _bP2Min = _pendP2;
            if (_pendP2 > _bP2Max) _bP2Max = _pendP2;
            if (_pendMed > 0)
            {
                _medParties++; _medMen += n; _sumMed += _pendMed;
                if (_pendMed >= MedFull) _medFull++;
                _bMedParties++;
                if (_pendMed > _bMedMax) _bMedMax = _pendMed;
            }
            if (mp == MobileParty.MainParty) { _plMen += n; _plE1 += n * (double)_pendP1; _plE2 += n * (double)_pendP2; }
        }

        private static string Pts(double v) { return (v >= 0 ? "+" : "") + (100.0 * v).ToString("0.#", CultureInfo.InvariantCulture); }
        private static string Pc(double x, double of) { return of > 0 ? (100.0 * x / of).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "-"; }

        /// <summary>Dopisek linii "Bitwa: H3" tej bitwy (pusty przy wylaczonym kluczu albo innej bitwie).</summary>
        internal static string BattleText(MapEvent me)
        {
            if (me == null || me != _me || !On) return "";
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(" | chirurg i taktyka (R3-10c): Taktyka zwyciezcy ").Append(_twHero ? _tw.ToString(ci) : "brak wodza (0)")
              .Append(" vs przegranego ").Append(_tpHero ? _tp.ToString(ci) : "brak wodza (0)").Append(" -> ").Append(Pts(_shift)).Append(" pkt");
            sb.Append(", chirurg: partii z Medycyna ").Append(_bMedParties).Append(" z ").Append(_bParties);
            if (_bMedParties > 0) sb.Append(" (najlepszy ").Append(_bMedMax).Append(')');
            if (_bParties > 0) sb.Append(", p partii po zmianie ").Append((100.0 * _bP2Min).ToString("0.#", ci)).Append(_bP2Max > _bP2Min + 0.0005f ? "-" + (100.0 * _bP2Max).ToString("0.#", ci) : "").Append('%');
            return sb.ToString();
        }

        /// <summary>Linia doby (z LosersFlee.Daily, po linii "Przegrani (H3)"); po niej liczniki doby od zera.</summary>
        internal static void Daily(int day)
        {
            try
            {
                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append("Przegrani - chirurg i taktyka (R3-10c): dzien ").Append(day);
                if (!On)
                {
                    sb.Append(" - WYLACZONE (Losers Flee Skills; smiertelnosc przegranych jak w linii \"Przegrani (H3)\" - sama sytuacja).");
                    Log.Info(sb.ToString());
                    return;
                }
                sb.Append(" - bitew ").Append(_battles).Append(": Taktyka zwyciezcy lepsza ").Append(_tacUp);
                if (_tacUp > 0) sb.Append(" (srednio ").Append(Pts(_sumUp / _tacUp)).Append(" pkt)");
                sb.Append(", gorsza ").Append(_tacDown);
                if (_tacDown > 0) sb.Append(" (srednio ").Append(Pts(_sumDown / _tacDown)).Append(" pkt)");
                sb.Append(", rowna ").Append(_tacEven).Append("; bez wodza-bohatera: zwyciezcy ").Append(_noHeroW).Append(", przegrani ").Append(_noHeroL);
                sb.Append(" | partie przegranych (bez taborow wsi) ").Append(_parties).Append(", ludzi ").Append(_men).Append(": z chirurgiem (Medycyna > 0) ").Append(_medParties)
                  .Append(" partii, ").Append(_medMen).Append(" ludzi");
                if (_medParties > 0) sb.Append(", srednia Medycyna ").Append((_sumMed / (double)_medParties).ToString("0", ci)).Append(", pelne (250+) ").Append(_medFull);
                sb.Append(" | oczekiwani zabici: sama sytuacja ").Append(_e0.ToString("0", ci)).Append(" (").Append(Pc(_e0, _men)).Append("), z Taktyka ").Append(_e1.ToString("0", ci))
                  .Append(" (").Append(Pc(_e1, _men)).Append("), z Taktyka i chirurgiem ").Append(_e2.ToString("0", ci)).Append(" (").Append(Pc(_e2, _men)).Append(")");
                sb.Append(" - Taktyka ").Append((_e1 - _e0 >= 0 ? "+" : "")).Append((_e1 - _e0).ToString("0", ci)).Append(", chirurdzy uratowali ").Append((_e1 - _e2).ToString("0", ci));
                sb.Append(" | p partii po zmianie: ");
                if (_parties > 0) sb.Append((100.0 * _p2Min).ToString("0.#", ci)).Append("-").Append((100.0 * _p2Max).ToString("0.#", ci)).Append("% (widelki 5-65%)");
                else sb.Append('-');
                sb.Append(" | gracz: pokonany ").Append(_plMen).Append(" ludzi (oczekiwani zabici bez chirurga ").Append(_plE1.ToString("0.#", ci)).Append(", z chirurgiem ").Append(_plE2.ToString("0.#", ci))
                  .Append("), wodzem zwyciezcow w ").Append(_plWinBattles).Append(" bitwach");
                if (_plWinBattles > 0) sb.Append(" (srednio ").Append(Pts(_plWinShift / _plWinBattles)).Append(" pkt)");
                sb.Append(" | potkniecia ").Append(_stumble).Append('.');
                Log.Info(sb.ToString());
            }
            finally { ClearDay(); }
        }
    }
}
