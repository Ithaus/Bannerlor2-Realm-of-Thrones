using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 371 (Jeff 09.11: "nie jest tak ze oblezenie sie skonczy az zabije 13k ludzi w jednej bitwie, tylko bylo tak historycznie ze szturm sie zalamywal
    /// i kontynuowano za dzien dwa 5"; decyzje: szturm trwa godziny; idzie ODDZIAL SZTURMOWY, nie cala armia - "nie na sztywno 3000 tylko procentowo";
    /// lamie sie po ok. 20-30% strat oddzialu albo po utracie machin / dowodcy; kolejny szturm po 2 dobach, gdy machiny przetrwaly, po 7-14, gdy trzeba je
    /// odbudowac). ZALAMANIE i ODSTAPIENIE - tempa rund SZ nie ruszamy.
    ///  - Oddzial szturmowy K = min(A, max(SiegeAssaultPartyShare x A, SiegeAssaultPartyMinVsDefenders x D)); A - zdrowi oblegajacy na starcie szturmu,
    ///    D - obroncy. Domyslnie 25% (szturmowano kolumnami / "forlorn hope" i pierwsza fala - cwierc sil, reszta w obozie, w rezerwie i przy machinach;
    ///    u Jeffa 13 658 -> 3 414, mniej wiecej tyle, ile misja przy polu 1500 i 3 falach w ogole wystawia), dolna granica 1.5 x obroncy (mala armia
    ///    idzie cala - z mniejszym oddzialem szturm nie mialby sensu).
    ///  - Prog: prog OB2 (0.22 +- 0.05, SZ3 morale) x fW dowodcy, fW = 1 + 0.08 x Valor + (Leadership - 75) / 500 + (Tactics - 75) / 750 w [0.8, 1.3]
    ///    (klucz SiegeAssaultBreakCommander; Valor -2..2 - dzielny wodz trzyma ludzi dluzej, umiejetnosci 0..300). Straty liczone wobec K, nie calej armii -
    ///    dotad 559 / 13 658 = 4% przy progu 23% (Ashemark): przy przewadze ponad ok. 5:1 OB2 matematycznie nie mogl zadzialac.
    ///  - Bez misji: tylko mianownik w UpdatePost (cas / K). W misji: SiegeBreak371 (gracz broni, wodz atakujacych AI).
    ///  - Przerwa: machiny (taran, wieza) przetrwaly -> T = SiegeRepelPauseMidDays (2) x fM x fL x fO w [Min 1, Max 5]; machina stracona w szturmie
    ///    (gra lamie taran i wieze po kazdej misji - SiegeEvent.SetSiegeEngineStatesForSiegeSide) -> SiegeRepelRebuildMinDays (7) x ten sam iloczyn w
    ///    [7, 14]. Zastepuje T = 7 x ... w [3, 28] (stary klucz SiegeRepelPauseDays dziala tylko przy SiegeAssaultBreak NIE). Budowa machin i weto OB1
    ///    "brama" biegna rownolegle z przerwa (wazniejsze dluzsze), wiec 7-14 to dolna granica, a nie druga doliczona budowa.
    /// Wyjatek per obiekt = licznik potkniec (Stumble), bez globalnego wylacznika.
    /// </summary>
    internal static partial class SiegeWalls
    {
        // ------------------------------------------------------------ klucze 371
        internal static bool BreakOn { get { var s = Settings.Current; return s != null && s.SiegeAssaultBreak; } }
        internal static bool BreakMissionOn { get { var s = Settings.Current; return s != null && s.SiegeAssaultBreak && s.SiegeAssaultBreakMission; } }
        private static double PartyShare { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeAssaultPartyShare) ? 0.25 : Clamp(s.SiegeAssaultPartyShare, 0.05, 1.0); } }
        private static double PartyMinVsDef { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeAssaultPartyMinVsDefenders) ? 1.5 : Clamp(s.SiegeAssaultPartyMinVsDefenders, 0.0, 10.0); } }
        private static bool CommanderOn { get { var s = Settings.Current; return s != null && s.SiegeAssaultBreakCommander; } }
        /// <summary>371: mnoznik progu po upadku wodza atakujacych albo utracie wszystkich taranow i wiez w misji (kazde osobno).</summary>
        internal static double ShockFactor { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeAssaultBreakShockFactor) ? 0.5 : Clamp(s.SiegeAssaultBreakShockFactor, 0.1, 1.0); } }
        internal static float BreakEndSeconds { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeAssaultBreakEndSeconds) ? 45f : (float)Clamp(s.SiegeAssaultBreakEndSeconds, 10.0, 300.0); } }
        private static double PauseMin { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeRepelPauseMinDays) ? 1.0 : Clamp(s.SiegeRepelPauseMinDays, 0.25, 28.0); } }
        private static double PauseMax { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeRepelPauseMaxDays) ? 5.0 : Clamp(s.SiegeRepelPauseMaxDays, PauseMin, 28.0); } }
        private static double PauseMid { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeRepelPauseMidDays) ? 2.0 : Clamp(s.SiegeRepelPauseMidDays, PauseMin, PauseMax); } }
        private static double RebuildMin { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeRepelRebuildMinDays) ? 7.0 : Clamp(s.SiegeRepelRebuildMinDays, 0.25, 56.0); } }
        private static double RebuildMax { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeRepelRebuildMaxDays) ? 14.0 : Clamp(s.SiegeRepelRebuildMaxDays, RebuildMin, 56.0); } }

        // liczniki doby 371 (misja)
        private static int _dMisSeen, _dMisBreak, _dMisFuse, _dMisLate, _dMisNone, _dMisInside, _dMisRegistered;

        /// <summary>371: oddzial szturmowy K z zdrowych oblegajacych A i obroncow D (procent armii z dolna granica wobec obroncow, nie wiecej niz A).</summary>
        internal static int PartyOf(int a, int d)
        {
            if (a <= 0) return 0;
            double k = Math.Max(PartyShare * a, PartyMinVsDef * Math.Max(0, d));
            return (int)Math.Max(1.0, Math.Min(a, Math.Round(k)));
        }

        /// <summary>371: prog zalamania szturmu (udzial strat oddzialu) = prog OB2 (SZ3) x fW dowodcy.</summary>
        internal static double EffThr(Assault a)
        {
            if (a == null || a.Thr <= 0f) return 0.0;
            return Clamp(a.Thr * (BreakOn ? a.FW : 1.0), 0.01, 0.95);
        }

        /// <summary>371: fW = 1 + 0.08 x Valor + (Leadership - 75) / 500 + (Tactics - 75) / 750 w [0.8, 1.3]; klucz wylaczony albo bez bohatera - 1.</summary>
        private static double CommanderFactor(Hero h, out string txt)
        {
            txt = "-";
            if (!CommanderOn) { txt = "klucz wylaczony"; return 1.0; }
            if (h == null) { txt = "bez bohatera"; return 1.0; }
            int v = 0, l = 0, t = 0;
            try { v = h.GetTraitLevel(DefaultTraits.Valor); } catch { }
            try { l = h.GetSkillValue(DefaultSkills.Leadership); t = h.GetSkillValue(DefaultSkills.Tactics); } catch { }
            double f = Clamp(1.0 + 0.08 * v + (l - 75) / 500.0 + (t - 75) / 750.0, 0.8, 1.3);
            txt = "Leadership " + l + ", Tactics " + t + ", Valor " + v + " -> x" + F(f, "0.00");
            return f;
        }

        /// <summary>371: zbudowane tarany i wieze obozu (IsConstructed, HP &gt; 0). Rzuca - lapie wolajacy.</summary>
        private static void Engines(Settlement st, out int rams, out int towers)
        {
            rams = 0; towers = 0;
            var ev = st != null ? st.SiegeEvent : null;
            var side = ev != null ? ev.GetSiegeEventSide(BattleSideEnum.Attacker) : null;
            if (side == null || side.SiegeEngines == null) return;
            foreach (var x in side.SiegeEngines.AllSiegeEngines())
            {
                if (x == null || !x.IsConstructed || x.Hitpoints <= 0f) continue;
                var t = x.SiegeEngine;
                if (t == DefaultSiegeEngineTypes.Ram || t == DefaultSiegeEngineTypes.ImprovedRam) rams++;
                else if (t == DefaultSiegeEngineTypes.SiegeTower) towers++;
            }
        }

        /// <summary>Nowy rekord szturmu AI (OB2 + 371): prog, oddzial szturmowy, wodz, machiny na starcie. Rzuca - lapie wolajacy.</summary>
        private static Assault NewAssault(MapEvent me, MobileParty lp, Settlement st)
        {
            float l = RepelLoss, r = RepelSpread, u = MBRandom.RandomFloat;
            var a = new Assault
            {
                St = st, Start = Now, U = u, Thr = l > 0f ? (float)Clamp(l + r * (2.0 * u - 1.0), 0.01, 0.95) : 0f,
                Men0 = me.AttackerSide.TroopCount, Def0 = me.DefenderSide != null ? me.DefenderSide.TroopCount : 0,
                Fac = lp.MapFaction != null ? lp.MapFaction.StringId : "-", Leader = Hn(lp), Capital = IsCapital(st)
            };
            try
            {
                a.K = PartyOf(a.Men0, a.Def0);
                a.FW = CommanderFactor(lp.LeaderHero, out a.FWTxt);
                Engines(st, out a.Ram0, out a.Tower0);
            }
            catch (Exception e) { Stumble("NewAssault.371", e); if (a.K <= 0) a.K = Math.Max(1, a.Men0); }
            return a;
        }

        /// <summary>371 (SiegeBreak371): rekord szturmu biezacej bitwy gracza-OBRONCY; brak (np. bitwa sprzed wczytania) - zakladany jak przy MapEventStarted.
        /// null + powod, gdy szturm poza regula (skrypt fabuly, Inni, morze, wodz gracz). Nie rzuca.</summary>
        internal static Assault MissionAssault(MapEvent me, out string why)
        {
            why = null;
            try
            {
                if (!Live || me == null) { why = "bez kampanii"; return null; }
                if (!me.IsSiegeAssault) { why = "nie szturm"; return null; }
                if (me.IsNavalMapEvent) { why = "morze"; return null; }
                var st = me.MapEventSettlement;
                if (st == null || !st.IsFortification) { why = "bez twierdzy"; return null; }
                Assault a;
                if (_assaults.TryGetValue(me, out a)) return a;
                var lpb = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                var lp = lpb != null && lpb.IsMobile ? lpb.MobileParty : null;
                if (lp == null || lp.IsMainParty) { why = "wodz nie AI"; return null; }
                if (Scripted(lp)) { why = "skrypt fabuly"; return null; }
                if (Und(lp, st)) { why = "Inni"; return null; }
                a = NewAssault(me, lp, st);
                _assaults[me] = a;
                why = "rekord zalozony w misji";
                return a;
            }
            catch (Exception e) { Stumble("MissionAssault", e); why = "potkniecie"; return null; }
        }

        /// <summary>371: przerwa po odparciu. lost - taran albo wieza obecne na starcie szturmu juz nie stoja (trzeba odbudowac).</summary>
        private static double Pause371(double fM, double fL, double fO, bool lost, int prior, out double grow, out double lo, out double hi, out double baseDays)
        {
            baseDays = lost ? RebuildMin : PauseMid;
            lo = lost ? RebuildMin : PauseMin;
            hi = lost ? RebuildMax : PauseMax;
            double T = Clamp(baseDays * fM * fL * fO, lo, hi);
            T = SiegeWalls2.GrowPause(T, prior, hi, out grow);                    // 307c (2): wczesniejsze odparcia wydluzaja - najwyzej do sufitu
            return T;
        }

        /// <summary>371: czy taran albo wieza ze startu szturmu juz nie stoja. Nie rzuca.</summary>
        private static bool EnginesLost(Assault a, out string txt)
        {
            txt = "-";
            try
            {
                if (a == null || a.Ram0 < 0) return false;
                int r, t;
                Engines(a.St, out r, out t);
                txt = "taran " + a.Ram0 + " -> " + r + ", wieze " + a.Tower0 + " -> " + t;
                return (a.Ram0 > 0 && r < a.Ram0) || (a.Tower0 > 0 && t < a.Tower0) || a.MisEngLost;
            }
            catch (Exception e) { Stumble("EnginesLost", e); return false; }
        }

        // ------------------------------------------------------------ misja (z SiegeBreak371)
        internal static void NoteMissionSeen() { _dMisSeen++; }
        internal static void NoteMissionNone() { _dMisNone++; }
        internal static void NoteMissionBreak(bool fuse, int late) { _dMisBreak++; if (fuse) _dMisFuse++; _dMisLate += late; }
        internal static void NoteMissionInside() { _dMisInside++; }
        internal static void NoteMissionStumble(string where, Exception e) { Stumble("SiegeBreak371." + where, e); }

        /// <summary>Z OnMapEventEnded: szturm zalamany w misji gracza-obroncy (droga gry FinishBattleAndKeepSiegeEvent juz przeszla) - przerwa jak po OB2.</summary>
        private static void RegisterMissionBreak(MapEvent me, Assault a)
        {
            try
            {
                int left = 0, dmen = 0;
                try { left = me.AttackerSide.TroopCount; dmen = me.DefenderSide.TroopCount; } catch { }
                Repel(me, a, left, dmen, false);
                _dMisRegistered++;
            }
            catch (Exception e) { Stumble("RegisterMissionBreak", e); }
        }

        /// <summary>Fragment linii "Mury oblezen (OB0)".</summary>
        private static string Break371DayText()
        {
            return " | 371: zalamanie " + (BreakOn ? "tak" : "nie") + " (oddzial " + F(PartyShare * 100.0, "0") + "% armii, co najmniej " + F(PartyMinVsDef, "0.0")
                 + " x obroncy; wodz " + (CommanderOn ? "tak" : "nie") + ", wstrzas x" + F(ShockFactor, "0.00") + "; przerwa " + F(PauseMin, "0.0") + "-" + F(PauseMid, "0.0") + "-" + F(PauseMax, "0.0")
                 + " dob, odbudowa machin " + F(RebuildMin, "0.0") + "-" + F(RebuildMax, "0.0") + " dob), misja " + (BreakMissionOn ? "tak" : "nie")
                 + ": misji szturmu z graczem w obronie " + _dMisSeen + ", poza regula " + _dMisNone + ", zalaman " + _dMisBreak + " (bezpiecznik " + _dMisFuse + ", wystawionych po zalamaniu "
                 + _dMisLate + "), wstrzymane - atakujacy w srodku " + _dMisInside + ", przerw zapisanych " + _dMisRegistered;
        }

        private static void ZeroDay371() { _dMisSeen = _dMisBreak = _dMisFuse = _dMisLate = _dMisNone = _dMisInside = _dMisRegistered = 0; }
    }
}
