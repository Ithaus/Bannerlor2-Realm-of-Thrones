using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 471b-2 (Jeff 13.11, odpowiedz D2: "liczyc strate od 0.1% do 0.5% dziennie w zaleznbosci od medycyny plus szansa ze kazdego dnia ktos zachoruje
    /// gdy wieksza medycyna wieksza szansa ze nie"). STRATY OBOZU OBLEZNICZEGO - ZAMIANA stawki RealisticBannerlord, nie dodatek.
    /// DZIS (dekompilacja RealisticBannerlord.dll, SiegeAttritionBehavior.OnDailyTickParty :22-51): kazda druzyna z BesiegerCamp traci co dobe
    /// U(Min..Max) + min(doba/5, 10) szeregowych (MCM Jeffa 2..5), chirurg Medicine >= 50 x0.75, >= 100 x0.5 - na DRUZYNE, nie na czlowieka;
    /// CrashScribe (Mends.RbSiegeCampFed) puszcza ja tylko przy glodzie druzyny. Do tego zaraza obozowa CampFever (398/399) - po inkubacji (14 dob)
    /// CODZIENNIE, bez konca, narost do x4.
    /// ODTAD (klucz SiegeCampAttritionByMedicine):
    ///  (1) prefiks na SiegeAttritionBehavior.OnDailyTickParty (AccessTools.DeclaredMethod na klasie RB, w kampanii) - druzyna oblegajaca: straty RB pominiete
    ///      (straty RB oblezonej twierdzy - OnDailyTickSettlement - bez zmian, tam rzadzi GL / SiegeWalls2.RbSettlementPre);
    ///      471b-p6: wyjatki - druzyna GLODUJACA (RB jak dotad: kara za glod, CrashScribe Mends.RbSiegeCampFed), Inni i oblezenia fabuly oszczedzone przez zaraze
    ///      (Daily ich nie liczy - RB jak dotad, nie zero strat);
    ///  (2) doba (Daily, po CampFever.OnDaily): kazda druzyna obozu traci p ludzi (zdrowi szeregowi, losowo wazeni liczebnoscia) - p = Low - (Low - High) x
    ///      min(1, Med / 200), Med = najlepsza Medycyna chirurga (EffectiveSurgeon) wsrod druzyn obozu ("najlepszy chirurg w armii"); domyslnie 0.5% .. 0.1%.
    ///      Zmarli: ksiega ludzi (PeopleBook FDiePlague - ta sama przyczyna co zaraza obozu), pomiar E4/0 (Measure40.NoteDisease), trupy pod murami
    ///      (SiegeMisery.AddCorpses, znacznik sanitarny). Bez Innych (nie choruja), bez druzyn w bitwie (roster nalezy do bitwy), bez oblezen fabuly
    ///      oszczedzonych przez zaraze (CampFever.Spared - jedna regula dla obu). Bohaterowie nie gina.
    ///  (3) CHOROBA = zaraza CampFever po stronie OBOZU tylko w czasie WYBUCHU: po inkubacji co dobe szansa c = SiegeCampOutbreakChancePercent x
    ///      (1 - 0.75 x min(1, Med / 200)) (4% .. 1%), wybuch trwa 5-9 dob; narost zarazy liczony od dnia wybuchu (ten sam sufit). Poza wybuchem oboz
    ///      nie choruje. Obroncy w murach - CampFever bez zmian (ta sama zaraza co dotad). Stan wybuchu - tylko pamiec sesji (po wczytaniu losowanie od nowa).
    /// Klucz NIE = co do bitu jak przed: prefiks przepuszcza RB, Daily nic nie robi, CampFever liczy oboz jak dotad.
    /// Wyjatek per druzyna / oblezenie = licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static class SiegeCamp471
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const double MedFull = 200.0;
        private const int ObMin = 5, ObMax = 9;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SiegeCampAttritionByMedicine; } }
        private static double PctLow { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeCampAttritionLowMedicinePercent) ? 0.5 : Math.Max(0.0, Math.Min(5.0, s.SiegeCampAttritionLowMedicinePercent)); } }
        private static double PctHigh { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeCampAttritionHighMedicinePercent) ? 0.1 : Math.Max(0.0, Math.Min(5.0, s.SiegeCampAttritionHighMedicinePercent)); } }
        private static bool FeverOn { get { var s = Settings.Current; return s != null && s.SiegeSicknessEnabled; } }   // 471b-p6 (A8/B8): wybuch tylko z zaraza CampFever
        private static double ObPct { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeCampOutbreakChancePercent) ? 4.0 : Math.Max(0.0, Math.Min(50.0, s.SiegeCampOutbreakChancePercent)); } }

        private sealed class Acc
        {
            public int Dead, Days, ObCount, ObDays, ObFrom = -1, ObUntil = -1, Med = -1;
            public double RateSum; public string MedWho = "-";
            public int FeverSick, FeverDead;
            public int MinePend, MineDay = -1;                 // 471b-p6 (recenzja A7): zmarli gracza do komunikatu (najwyzej raz na 7 dob)
        }
        private static readonly Dictionary<SiegeEvent, Acc> _acc = new Dictionary<SiegeEvent, Acc>();
        private static int _stumbles, _dRbSkip, _dRbStarve, _dDead, _dObNew, _dSieges;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static string _hookRb = "nie zakladana";

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("SiegeCamp471." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }
        private static string F(double v, string f) { return v.ToString(f, Inv); }
        private static string Hn(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Z SiegeWalls.Reset (nowa kampania i kazde wczytanie).</summary>
        internal static void Reset() { _acc.Clear(); _stumbles = 0; _err.Clear(); _dRbSkip = _dRbStarve = _dDead = _dObNew = _dSieges = 0; }

        private static Acc AccOf(SiegeEvent ev)
        {
            Acc a;
            if (!_acc.TryGetValue(ev, out a)) { a = new Acc(); _acc[ev] = a; }
            return a;
        }

        /// <summary>Druzyny obozu (bez powtorzen), zywi (bez Innych) i najlepsza Medycyna chirurga wsrod nich.</summary>
        private static List<MobileParty> Camp(SiegeEvent ev, out int med, out Hero who, out int men)
        {
            med = 0; who = null; men = 0;
            var l = new List<MobileParty>(8);
            var camp = ev != null ? ev.BesiegerCamp : null;
            if (camp == null) return l;
            foreach (var pb in camp.GetInvolvedPartiesForEventType())
            {
                var mp = pb != null ? pb.MobileParty : null;
                if (mp == null || mp.MemberRoster == null || l.Contains(mp) || Undead.Party(mp)) continue;
                l.Add(mp);
                men += mp.MemberRoster.TotalManCount;
                try
                {
                    var sg = mp.EffectiveSurgeon;
                    int m = sg != null ? sg.GetSkillValue(DefaultSkills.Medicine) : 0;
                    if (m > med || who == null && sg != null && m >= med) { med = m; who = sg; }
                }
                catch (Exception e) { Stumble("Camp.Surgeon", e); }
            }
            return l;
        }

        /// <summary>Dzienna stawka strat obozu (ulamek) przy najlepszej Medycynie med.</summary>
        internal static double Rate(int med)
        {
            double k = Math.Max(0.0, Math.Min(1.0, med / MedFull));
            return (PctLow - (PctLow - PctHigh) * k) / 100.0;
        }

        /// <summary>Dzienna szansa wybuchu choroby (ulamek) przy najlepszej Medycynie med.</summary>
        internal static double ObChance(int med)
        {
            double k = Math.Max(0.0, Math.Min(1.0, med / MedFull));
            return ObPct / 100.0 * (1.0 - 0.75 * k);
        }

        // ------------------------------------------------------------ (1) prefiks RB
        /// <summary>Prefiks RealisticBannerlord SiegeAttritionBehavior.OnDailyTickParty: przy kluczu druzyna oblegajaca bez strat RB (stawke liczy Daily). Nie rzuca.</summary>
        public static bool RbPartyPre(MobileParty __0)
        {
            try
            {
                if (!On || __0 == null || __0.BesiegerCamp == null) return true;
                // 471b-p6 (recenzja A3): glod druzyny - RB jak dotad (CrashScribe Mends.RbSiegeCampFed puszcza RB tylko przy glodzie: to kara za GLOD, inne zjawisko
                // niz straty obozu wedlug medycyny; bez tego glodujacy oboz nie tracilby z glodu nikogo)
                if (__0.Party != null && __0.Party.IsStarving) { _dRbStarve++; return true; }
                // 471b-p6 (recenzja B4): obozy, ktorych Daily nie liczy (Inni, oblezenie fabuly oszczedzone przez zaraze) - RB jak dotad, nie zero strat
                if (Undead.Party(__0)) return true;
                var ev = __0.BesiegerCamp.SiegeEvent;
                if (ev != null && CampFever.Spared(ev) != null) return true;
                _dRbSkip++;
                return false;
            }
            catch { return true; }
        }

        /// <summary>Z SiegeWalls.EnsureHooks (kampania, raz na proces). Cel prywatny zadeklarowany w RB - DeclaredMethod na klasie deklarujacej.</summary>
        internal static void EnsureHooks()
        {
            try
            {
                var tr = AccessTools.TypeByName("RealisticBannerlord.Systems.Siege.SiegeAttritionBehavior");
                var mr = tr != null ? AccessTools.DeclaredMethod(tr, "OnDailyTickParty", new[] { typeof(MobileParty) }) : null;
                if (tr == null) _hookRb = "bez RealisticBannerlord";
                else if (mr == null) _hookRb = "BRAK (SiegeAttritionBehavior.OnDailyTickParty)";
                else
                {
                    Seal31.Patch("471b-2: straty obozu wedlug medycyny zamiast RB (SiegeAttritionBehavior.OnDailyTickParty)", mr, typeof(SiegeCamp471), nameof(RbPartyPre), null, null, Priority.Normal);
                    _hookRb = "BRAK (latka nie weszla - linia 'Szczelnosc (164a)')";
                    var info = Harmony.GetPatchInfo(mr);
                    if (info != null && info.Prefixes != null)
                        foreach (var p in info.Prefixes)
                            if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(SiegeCamp471) && p.PatchMethod.Name == nameof(RbPartyPre)) _hookRb = "wpieta";
                }
            }
            catch (Exception e) { _hookRb = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks", e); }
        }

        /// <summary>Z SiegeWalls.SessionStart: linia startowa 471b-2.</summary>
        internal static void SessionStart()
        {
            try
            {
                Log.Info("Straty obozu (471b-2 start): klucz Siege Camp Attrition By Medicine " + (On ? "TAK" : "NIE") + " | latka RB OnDailyTickParty " + _hookRb
                         + " | stawka dzienna " + F(PctLow, "0.00") + "% (Medycyna 0) .. " + F(PctHigh, "0.00") + "% (Medycyna " + F(MedFull, "0") + "+), najlepszy chirurg obozu"
                         + " | wybuch choroby" + (FeverOn ? "" : " (zaraza obozowa CampFever WYLACZONA - bez wybuchow)") + ": po inkubacji zarazy szansa " + F(ObChance(0) * 100.0, "0.0") + "% .. " + F(ObChance((int)MedFull) * 100.0, "0.0") + "% na dobe, trwa " + ObMin + "-" + ObMax
                         + " dob (zaraza CampFever obozu tylko w wybuchu; obroncy bez zmian).");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ (3) wybuch choroby (z CampFever.TickSiege)
        /// <summary>Z CampFever.TickSiege (po inkubacji, nie fabula): czy oboz choruje tej doby. Losuje wybuch (raz na dobe i oblezenie), podaje narost zarazy
        /// liczony od dnia wybuchu (sufit rampCap). Gracz w obozie - komunikat przy wybuchu. Nie rzuca (wyjatek = bez zarazy obozu tej doby).</summary>
        internal static bool Outbreak(SiegeEvent ev, Settlement st, float rampPct, float rampCap, out float ramp)
        {
            ramp = 1f;
            try
            {
                if (ev == null) return false;
                int med, men; Hero who;
                Camp(ev, out med, out who, out men);
                var a = AccOf(ev);
                a.Med = med; a.MedWho = Hn(who);
                int today = (int)CampaignTime.Now.ToDays;
                if (a.ObUntil >= 0 && today >= a.ObUntil) { a.ObUntil = -1; a.ObFrom = -1; }
                if (a.ObUntil < 0)
                {
                    double c = ObChance(med);
                    if (c <= 0.0 || MBRandom.RandomFloat >= c) return false;
                    a.ObFrom = today; a.ObUntil = today + MBRandom.RandomInt(ObMin, ObMax + 1); a.ObCount++; _dObNew++;
                    try
                    {
                        var main = MobileParty.MainParty;
                        if (main != null && ev.BesiegerCamp != null && main.BesiegerCamp == ev.BesiegerCamp)
                            Log.Player("Sickness breaks out in the siege camp before " + st.Name + ". It will run for some days; a good surgeon keeps it down.", true);
                    }
                    catch { }
                    Log.Info("Straty obozu (471b-2): dzien " + today + " | osada " + st.StringId + " - WYBUCH choroby w obozie (nr " + a.ObCount + ", do dnia " + a.ObUntil
                             + ") | szansa dzis " + F(c * 100.0, "0.0") + "% (najlepsza Medycyna " + med + ", " + a.MedWho + "), ludzi obozu " + men + ".");
                }
                a.ObDays++;
                float r = 1f + (today - a.ObFrom) * Math.Max(0f, rampPct) / 100f;
                ramp = r > rampCap ? rampCap : r;
                return true;
            }
            catch (Exception e) { Stumble("Outbreak", e); return false; }
        }

        /// <summary>Z CampFever.TickSiege: chorzy i zmarli zarazy obozu tej doby (do linii konca oblezenia).</summary>
        internal static void NoteFever(SiegeEvent ev, int sick, int dead)
        {
            try { if (ev != null && (sick > 0 || dead > 0)) { var a = AccOf(ev); a.FeverSick += sick; a.FeverDead += dead; } } catch { }
        }

        // ------------------------------------------------------------ (2) doba: straty obozu
        /// <summary>Z ArmouryBehavior (doba, po CampFever.OnDaily): straty obozow wedlug medycyny i linia doby. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                List<SiegeEvent> gone = null;
                foreach (var kv in _acc) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (gone == null) gone = new List<SiegeEvent>(); gone.Add(kv.Key); }
                if (gone != null) foreach (var ev in gone) _acc.Remove(ev);
                if (!On) return;
                var mgr = Campaign.Current != null ? Campaign.Current.SiegeEventManager : null;
                if (mgr == null || mgr.SiegeEvents == null) return;
                var sb = new StringBuilder(800);
                foreach (var ev in new List<SiegeEvent>(mgr.SiegeEvents))
                {
                    try { One(ev, sb); }
                    catch (Exception e) { Stumble("One", e); }
                }
                if (LogOn)
                    Log.Info("Straty obozu (471b-2 doba): dzien " + (int)CampaignTime.Now.ToDays + " | oblezen ze stratami " + _dSieges + ", zmarli dzis " + _dDead + ", stawka RB pominieta (druzyn) " + _dRbSkip + ", RB przy glodzie druzyny (jak dotad) " + _dRbStarve
                             + ", wybuchy choroby nowe " + _dObNew + " | " + (sb.Length == 0 ? "-" : sb.ToString()) + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _dRbSkip = _dRbStarve = _dDead = _dObNew = _dSieges = 0; }
        }

        private static bool LogOn { get { var s = Settings.Current; return s != null && s.SiegeWallsLog && s.LogEnabled; } }

        private static void One(SiegeEvent ev, StringBuilder sb)
        {
            var st = ev != null ? ev.BesiegedSettlement : null;
            if (st == null || st.SiegeEvent != ev || ev.BesiegerCamp == null) return;
            if (CampFever.Spared(ev) != null) return;                                  // oblezenie fabuly ROT oszczedzone przez zaraze - jedna regula dla obu
            int med, men; Hero who;
            var parties = Camp(ev, out med, out who, out men);
            if (parties.Count == 0 || men <= 0) return;
            double p = Rate(med);
            var a = AccOf(ev);
            a.Med = med; a.MedWho = Hn(who);
            int dead = 0, mine = 0;
            foreach (var mp in parties)
            {
                try
                {
                    if (mp.MapEvent != null) continue;                                    // roster w bitwie nalezy do bitwy
                    int healthy = 0;
                    var r = mp.MemberRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var ch = r.GetCharacterAtIndex(i);
                        if (ch == null || ch.IsHero) continue;
                        healthy += r.GetElementNumber(i) - r.GetElementWoundedNumber(i);
                    }
                    if (healthy <= 0) continue;
                    int n = MBRandom.RoundRandomized((float)(healthy * p));
                    if (n <= 0) continue;
                    int m0 = Measure40.MenOf(mp), p0 = PeopleBook.MenOf(mp);
                    int k = 0;
                    for (int j = 0; j < n; j++) if (KillOne(r)) k++;
                    if (k <= 0) continue;
                    try { Measure40.NoteDisease(mp, m0 - Measure40.MenOf(mp), k); } catch { }
                    try { int pd = p0 - PeopleBook.MenOf(mp); if (pd > 0) PeopleBook.DieShares(PeopleBook.Take(mp.MemberRoster, pd, PeopleBook.FDiePlague), PeopleBook.FDiePlague); } catch { }
                    dead += k;
                    if (mp.IsMainParty) mine += k;
                }
                catch (Exception e) { Stumble("Party", e); }
            }
            if (dead > 0) SiegeMisery.AddCorpses(st, false, dead, SiegeMisery.SrcFever);   // znacznik sanitarny pod murami (nie rzuca)
            a.Dead += dead; a.Days++; a.RateSum += p;
            _dDead += dead; if (dead > 0) _dSieges++;
            a.MinePend += mine;                                                        // 471b-p6 (recenzja A7): komunikat przy pierwszej stracie, potem najwyzej raz na 7 dob z suma
            int today = (int)CampaignTime.Now.ToDays;
            if (a.MinePend > 0 && (a.MineDay < 0 || today - a.MineDay >= 7))
            {
                try { Log.Player("Hardship in the siege camp before " + st.Name + ": " + a.MinePend + " of your men died " + (a.MineDay < 0 ? "today" : "since the last report") + (med >= 50 ? " (your surgeon kept it down)." : "."), true); } catch { }
                a.MineDay = today; a.MinePend = 0;
            }
            if (sb.Length < 2500)
                sb.Append(sb.Length > 0 ? "; " : "").Append(st.StringId).Append(' ').Append(men).Append(" ludzi, Med ").Append(med).Append(" (").Append(a.MedWho).Append("), ")
                  .Append(F(p * 100.0, "0.00")).Append("% -> zmarli ").Append(dead).Append(", razem ").Append(a.Dead)
                  .Append(a.ObUntil >= 0 ? ", CHOROBA do dnia " + a.ObUntil : ", bez choroby (szansa " + F(ObChance(med) * 100.0, "0.0") + "%)");
        }

        /// <summary>Losowy zdrowy szeregowy (wazony liczebnoscia) umiera. false - nie bylo kogo.</summary>
        private static bool KillOne(TroopRoster roster)
        {
            int pool = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero) continue;
                pool += roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
            }
            if (pool <= 0) return false;
            int pick = MBRandom.RandomInt(pool);
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero) continue;
                int h = roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
                if (h <= 0) continue;
                if (pick >= h) { pick -= h; continue; }
                roster.AddToCounts(ch, -1);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ teksty do linii 471 (start / koniec)
        /// <summary>Do linii "(471) start": stawka i szansa wybuchu obozu przy obecnej najlepszej Medycynie.</summary>
        internal static string StartText(SiegeEvent ev)
        {
            try
            {
                if (!On) return "straty obozu: RB i zaraza jak dotad (klucz 471b-2 NIE)";
                int med, men; Hero who;
                Camp(ev, out med, out who, out men);
                return "straty obozu (471b-2): " + F(Rate(med) * 100.0, "0.00") + "%/d, najlepsza Medycyna " + med + " (" + Hn(who) + "), szansa wybuchu choroby " + F(ObChance(med) * 100.0, "0.0") + "%/d" + (FeverOn ? "" : " (zaraza wylaczona - bez wybuchow)");
            }
            catch (Exception e) { Stumble("StartText", e); return "straty obozu: blad"; }
        }

        /// <summary>Do linii "(471) koniec": zmarli ze strat, stawka srednia, wybuchy i zaraza obozu.</summary>
        internal static string EndText(SiegeEvent ev)
        {
            Acc a;
            if (ev == null || !_acc.TryGetValue(ev, out a)) return "straty obozu (471b-2): brak zapisu";
            return "straty obozu (471b-2): zmarli " + a.Dead + " w " + a.Days + " dob (sr. " + F(a.Days > 0 ? a.RateSum / a.Days * 100.0 : 0.0, "0.00") + "%/d, Medycyna ostatnio " + a.Med
                   + "), wybuchy choroby " + a.ObCount + " (dob choroby " + a.ObDays + ", zaraza obozu: chorzy " + a.FeverSick + ", zmarli " + a.FeverDead + ")";
        }
    }
}
