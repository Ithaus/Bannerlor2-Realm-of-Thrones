using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// MURY OBLEZEN OB1-OB4 (projekt docs/oblezenia/PROJEKT-WYBRANY.md rozdz. 2 i 4 z decyzjami Jeffa 14.10, ktore go zmieniaja).
    ///
    /// OB1 - decyzja AI o szturmie (postfiks BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical, wolany z SiegeRansom.AssaultPost PRZED okupem).
    ///   Jeff 14.10: "czas nie ma znaczenia; mury licza sie w pelni, chyba ze zostana rozbite albo postawiono machiny obleznicze lub tarany". Wykladnik
    ///   przewagi murow bez czlonu czasu gry (-0.02/h po 96 h): w = 0.8 x (srednie HP odcinkow murow 0..1) x 1.25 bez taranu x 1.25 bez wiezy (mnozniki
    ///   gry, BesiegerCamp.cs:408-418), nigdy ponizej 0. Stolica i ostatnia twierdza - ta sama regula. Latka tylko ODRZUCA "true" gry: s = sila
    ///   oblegajacych / (obroncy x przewaga^w) <= 1 -> "false"; inaczej "true" z prawdopodobienstwem min(1, 0.1 s kara) / min(1, 0.1 s_gry kara) - wzor
    ///   szansy gry z naszym w. Gracz decyduje sam (jego oboz gra nie pyta; drabiny po przygotowaniach jak w grze).
    ///   Lamanie murow: RBM (RBMCombat_siege_engines.xml) daje katapultom, onagerom i bricole is_anti_personnel="true", a gra nie celuje nimi w mury
    ///   (BesiegerCamp.FindAttackableWallSectionWithHighestPriority wychodzi przy IsAntiPersonnel) - mury bije tylko trebusz (1200 x 5 strzalow na dobe).
    ///   AI losuje strategie (DefaultSiegeEventModel.GetSiegeStrategyScore: RandomFloat) - trebusze ma tylko WearOutDefenders (ok. 1/3 obozow). Gdy
    ///   OB1 wstrzymuje szturm, a oboz ma juz taran i wieze (droga "podstaw taran i wieze" wyczerpana), AI przechodzi na WearOutDefenders
    ///   (BesiegerCamp.SetSiegeStrategy - ta sama droga, co zmiana strategii gracza w menu) - klucz SiegeAiBreachWhenWallsHold.
    /// Przewaga osady (postfiks DefaultCombatSimulationModel.GetSettlementAdvantage - jedna liczba dla decyzji, symulacji szturmu AI i gracza, slawy
    ///   i oceny dolaczenia do szturmu): gra liczy juz poziom murow (1 + 4 + poziom - 1: 5/6/7, wyrwa x0.25) i machiny oblegajacych (dzielnik), a poziom
    ///   murow drugi raz przez wytrzymalosc (30/50/67 tys.) - bez dopisku. Dopisane: (a) samo miano stolicy x SiegeCapitalWallsFactor (1.25 - jeden
    ///   stopien murow gry to +17-20%, stolica ma straz krola i zamek w murach); (b) machiny dystansowe OBRONCOW - lustro wag gry dla oblegajacych
    ///   (x(1 + 0.08 na zwykla + 0.12 na ogniowa)); to jest realny skutek warsztatu oblezniczego (balisty i katapulty na start oblezenia) i machin
    ///   zbudowanych w oblezeniu, znika, gdy oblegajacy je zbija. Koszary, place cwiczen, zbrojownia BK, oddzial przyboczny BK - dzialaja przez liczbe
    ///   i jakosc obroncow (sila w CalculateCurrentStrength) - dopisek do murow liczylby je drugi raz; spichlerze / magazyny - zapas jedzenia (czas
    ///   oblezenia, nie szturm) - tez bez dopisku.
    /// OB2 - szturm AI odparty (Jeff 14.10: "szturm nie wyszedl - moga szturmowac znowu za x dni w zaleznosci od morale, ilosci ludzi i oplacalnosci"):
    ///   szturm z decyzji AI (bitwa Siege, wodz nie gracz, nie skrypt fabuly - Ai.DoNotMakeNewDecisions, nie Inni) dostaje przy MapEventStarted prog
    ///   strat armii U[L - r, L + r] (0.22 +- 0.05). Postfiks MapEvent.Update (po pelnej rundzie symulacji, bitwy bez gracza): straty strony
    ///   oblegajacej (TroopCasualties / (TroopCasualties + zdrowi)) >= prog i mury pracuja (obroncow > 100 miasto / > 30 zamek, nie sala lorda; w bitwie
    ///   SiegeOutside - tylko gdy zaloga bije sie z murow, OB3) -> MapEvent.FinishBattleAndKeepSiegeEvent: droga gry dla szturmu, z ktorego AI sie
    ///   wycofuje (PlayerEncounter.ContinueBattle, EnemyRetreated) - bitwa bez zwyciezcy, oblegajacy wracaja do obozu, oblezenie trwa, bez rozejmu.
    ///   Nastepny szturm najwczesniej po przerwie T = SiegeRepelPauseDays (7) x fM x fL x fO, w granicach [3/7, 4] x podstawa:
    ///   fM = 100 / (morale armii + 50) w [0.67, 2]; fL = 3 / (zdrowi oblegajacy / zdrowi obroncy po szturmie) w [0.5, 2] (3 = prog gry do rozpoczecia
    ///   oblezenia przy murach 2, DefaultTargetScoreCalculatingModel.cs:226-241); fO = sqrt(182 / N) w [0.5, 2], N = wartosc twierdzy dla frakcji
    ///   (GetSettlementValueForFaction - ta sama miara, ktora AI wybiera cele) / dobowy zold armii (CitySack.NeedOf) - "ile dob zoldu warta jest
    ///   twierdza"; 182 = pol roku. Przerwa w pamieci i w zapisie (arm_siege_walls). Odstapienie - zwykla droga gry (spojnosc, jedzenie, zaraza, SCA).
    /// OB3 - bitwa ze szturmu, do ktorej dolaczyla odsiecz spoza murow (gra zmienia typ na SiegeOutside, MapEvent.cs:651-654): gdy ludzie twierdzy
    ///   (zaloga, milicja, druzyny w osadzie) to >= SiegeReliefWallsShare strony obrony, a obroncow > 100 / 30 - tury rundy wzorem szturmu gry
    ///   (DefaultCombatSimulationModel.cs:251-256), postfiks GetSimulationTicksForBattleRound. Takze twierdze gracza (gracz poza bitwa).
    /// OB4 - budowa obozu i machin strony OBLEGAJACEJ (WorldPace.SiegePostfix, raz na lancuch): x SiegeBesiegerPrepPercent (50) / klasa (zamek 1,
    ///   miasto 1.5, stolica 2.5). Jeff 14.10: "realistyczne oblezenia takze dla mnie i czas budowy tez realistyczny" - takze oboz gracza. Bez Innych
    ///   i bez obozow skryptu fabuly ROT (szturm fabuly czeka tylko na przygotowania - ApplyStartAssaultAgainstWalls po IsPreparationComplete).
    /// Wyjatek per obiekt = licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7). Kazdy klucz wylaczony = zachowanie 7bc5dd0b.
    /// </summary>
    internal static class SiegeWalls
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
        private static float Now { get { return (float)CampaignTime.Now.ToDays; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string F(double v, string f) { return v.ToString(f, Inv); }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Hn(MobileParty mp) { try { return mp == null ? "-" : mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : mp.StringId; } catch { return "?"; } }
        private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }

        // ------------------------------------------------------------ klucze
        internal static bool Ob1On { get { var s = Settings.Current; return s != null && s.SiegeWallsHoldUntilBreached; } }
        internal static float CapitalFactor { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeCapitalWallsFactor) ? 1f : (float)Clamp(s.SiegeCapitalWallsFactor, 1.0, 3.0); } }
        private static bool EnginesOn { get { var s = Settings.Current; return s != null && s.SiegeDefenderEnginesOnWalls; } }
        private static bool BreachOn { get { var s = Settings.Current; return s != null && s.SiegeAiBreachWhenWallsHold; } }
        private static float RepelLoss { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeAssaultRepelLoss) || s.SiegeAssaultRepelLoss <= 0f ? 0f : (float)Clamp(s.SiegeAssaultRepelLoss, 0.01, 0.9); } }
        private static float RepelSpread { get { var s = Settings.Current; float l = RepelLoss; return s == null || float.IsNaN(s.SiegeAssaultRepelSpread) || s.SiegeAssaultRepelSpread <= 0f ? 0f : (float)Clamp(s.SiegeAssaultRepelSpread, 0.0, Math.Min(0.2, l * 0.9)); } }
        private static double PauseDays { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeRepelPauseDays) ? 7.0 : Clamp(s.SiegeRepelPauseDays, 1.0, 28.0); } }
        private static bool FromWallsOn { get { var s = Settings.Current; return s != null && s.SiegeReliefFromWalls; } }
        private static double WallsShare { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeReliefWallsShare) ? 0.5 : Clamp(s.SiegeReliefWallsShare, 0.0, 1.0); } }
        private static bool LogOn { get { var s = Settings.Current; return s != null && s.SiegeWallsLog && s.LogEnabled; } }

        // ------------------------------------------------------------ potkniecia
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("SiegeWalls." + where, e); } catch { }
        }

        private static bool Und(MobileParty mp) { try { return mp != null && Undead.Party(mp); } catch { return false; } }
        /// <summary>Oblezenie z Innymi po ktorejkolwiek stronie (wodz obozu albo rod pana osady) - oblezeniami Innych rzadzi CrashScribe T2c; jak SiegeRansom.Evaluate.</summary>
        private static bool Und(MobileParty mp, Settlement st)
        {
            if (Und(mp)) return true;
            try { return st != null && st.OwnerClan != null && ClanIncomeBook.IsUndeadClan(st.OwnerClan); } catch { return false; }
        }
        /// <summary>Druzyna skryptu (fabula ROT, zadania): gra nie podejmuje za nia decyzji - szturm i budowe prowadzi skrypt.</summary>
        private static bool Scripted(MobileParty mp) { try { return mp != null && !mp.IsMainParty && mp.Ai != null && mp.Ai.DoNotMakeNewDecisions; } catch { return false; } }

        // ------------------------------------------------------------ stolica (pamiec dnia - CapitalTreasury.IsCapital czyta tytuly BK)
        private static readonly Dictionary<Settlement, int> _capDay = new Dictionary<Settlement, int>();
        internal static bool IsCapital(Settlement st)
        {
            if (st == null) return false;
            int today = Today, v;
            if (_capDay.TryGetValue(st, out v) && (v >> 1) == today) return (v & 1) == 1;
            bool c = CapitalTreasury.IsCapital(st);
            _capDay[st] = (today << 1) | (c ? 1 : 0);
            return c;
        }

        /// <summary>Srednie HP odcinkow murow 0..1 (Settlement.SettlementWallSectionHitPointsRatioList; brak odcinkow = 1).</summary>
        internal static double WallHp(Settlement st)
        {
            try
            {
                var l = st != null ? st.SettlementWallSectionHitPointsRatioList : null;
                if (l == null || l.Count == 0) return 1.0;
                double sum = 0;
                for (int i = 0; i < l.Count; i++) sum += l[i];
                return Clamp(sum / l.Count, 0.0, 1.0);
            }
            catch { return 1.0; }
        }

        /// <summary>Machiny dystansowe obroncow (zbudowane, jak gra liczy machiny oblegajacych): x(1 + 0.08 n + 0.12 n_ogniowe).</summary>
        private static float EnginesFactor(Settlement st, out int n, out int nf)
        {
            n = nf = 0;
            var ev = st.SiegeEvent;
            var side = ev != null ? ev.GetSiegeEventSide(BattleSideEnum.Defender) : null;
            if (side == null || side.SiegeEngines == null) return 1f;
            foreach (var x in side.SiegeEngines.AllSiegeEngines())
            {
                if (x == null || !x.IsConstructed || x.SiegeEngine == null || !x.SiegeEngine.IsRanged) continue;
                string id = x.SiegeEngine.StringId;
                if (id != null && id.StartsWith("fire", StringComparison.Ordinal)) nf++; else n++;
            }
            return 1f + 0.08f * n + 0.12f * nf;
        }

        // ------------------------------------------------------------ latka: przewaga osady
        private static int _dAdv;
        public static void AdvPost(Settlement settlement, ref float __result)
        {
            try
            {
                if (settlement == null || settlement.SiegeEvent == null || !settlement.IsFortification || __result <= 0f) return;
                var s = Settings.Current;
                if (s == null) return;
                float f = 1f, cf = CapitalFactor;
                if (cf > 1f && IsCapital(settlement)) f *= cf;
                if (s.SiegeDefenderEnginesOnWalls) { int n, nf; f *= EnginesFactor(settlement, out n, out nf); }
                if (f != 1f) { __result *= f; _dAdv++; }
            }
            catch (Exception e) { Stumble("AdvPost", e); }
        }

        // ------------------------------------------------------------ miara OB1 (tylko odczyt)
        internal sealed class Meas
        {
            public double Att, Def, Adv, Hp, W, WGame, S, SGame, Pen, POurs, PGame, Div, Base, Perks, CapF, EngF;
            public bool Ram, Tower, Empty, Breach, Capital; public int Eq, Level, DefN, DefNf;
        }

        /// <summary>Wzor gry (BesiegerCamp.cs:375-428) z naszym w i skladnikami przewagi (DefaultCombatSimulationModel.GetSettlementAdvantage :172-227).</summary>
        internal static Meas Measure(BesiegerCamp camp)
        {
            var ev = camp.SiegeEvent;
            var st = ev.BesiegedSettlement;
            var m = new Meas();
            float f = MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == st ? 0.5f : 1f;
            foreach (var pb in st.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege))
            {
                if (pb == null || !pb.IsMobile) continue;
                var mp = pb.MobileParty;
                if (mp.CurrentSettlement == st && (mp.Aggressiveness > 0.01f || mp.IsMilitia || mp.IsGarrison)) m.Def += f * pb.CalculateCurrentStrength();
            }
            foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) if (pb != null) m.Att += pb.CalculateCurrentStrength();
            bool impRam = false; int towers = 0, ranged = 0, fire = 0;
            var side = ev.GetSiegeEventSide(BattleSideEnum.Attacker);
            if (side != null && side.SiegeEngines != null)
                foreach (var x in side.SiegeEngines.AllSiegeEngines())
                {
                    if (x == null || !x.IsConstructed) continue;
                    var t = x.SiegeEngine;
                    if (t == DefaultSiegeEngineTypes.Ram || t == DefaultSiegeEngineTypes.ImprovedRam) { m.Ram = true; if (t == DefaultSiegeEngineTypes.ImprovedRam) impRam = true; }
                    else if (t == DefaultSiegeEngineTypes.SiegeTower) { m.Tower = true; towers++; }
                    else if (t == DefaultSiegeEngineTypes.Trebuchet || t == DefaultSiegeEngineTypes.Onager || t == DefaultSiegeEngineTypes.Ballista) ranged++;
                    else if (t == DefaultSiegeEngineTypes.FireOnager || t == DefaultSiegeEngineTypes.FireBallista) fire++;
                }
            var csm = Campaign.Current.Models.CombatSimulationModel;
            m.Adv = csm.GetSettlementAdvantage(st);
            m.Hp = WallHp(st);
            double h0 = CampaignTime.HoursInDay * 4.0, el = ev.SiegeStartTime.ElapsedHoursUntilNow;
            m.WGame = 0.8 - (el > h0 ? (el - h0) * 0.02 : 0.0);
            m.W = 0.8 * m.Hp;
            if (!m.Ram) { m.WGame *= 1.25; m.W *= 1.25; }
            if (!m.Tower) { m.WGame *= 1.25; m.W *= 1.25; }
            if (m.W < 0) m.W = 0;
            // skladniki przewagi (do linii)
            try
            {
                m.Level = st.Town != null ? st.Town.GetWallLevel() : 0;
                m.Breach = st.SettlementTotalWallHitPoints < 1E-05f;
                m.Base = 1.0 + (4.0 + (m.Level - 1)) * (m.Breach ? 0.25 : 1.0);
                m.Div = 1.0 + ((m.Ram || towers > 0) ? 0.25 : 0.0) + (impRam ? 0.24 : (m.Ram ? 0.16 : 0.0)) + (towers > 1 ? 0.24 : (towers == 1 ? 0.16 : 0.0)) + ranged * 0.08 + fire * 0.12;
                m.Capital = IsCapital(st);
                m.CapF = m.Capital ? CapitalFactor : 1.0;
                int n = 0, nf = 0;
                m.EngF = EnginesOn ? EnginesFactor(st, out n, out nf) : 1.0;
                m.DefN = n; m.DefNf = nf;
                double game = m.Adv / Math.Max(1e-6, m.CapF * m.EngF);
                m.Perks = m.Base > 0 ? game / (m.Base / m.Div) : 1.0;
            }
            catch { }
            if (m.Def <= 0) { m.Empty = true; m.S = m.SGame = 999.0; m.POurs = m.PGame = 1.0; return m; }
            m.S = m.Att / (m.Def * Math.Pow(m.Adv, m.W));
            m.SGame = m.Att / (m.Def * Math.Pow(m.Adv, m.WGame));
            m.Eq = csm.GetNumberOfEquipmentsBuilt(st);
            double p = csm.GetMaximumSiegeEquipmentProgress(st) + 0.25 * (5 - m.Eq);
            m.Pen = Math.Min(3, m.Eq) / 3.0 * (1.0 - 0.85 * p * p);
            m.POurs = m.S > 1.0 ? Clamp(0.1 * m.S * m.Pen, 0.0, 1.0) : 0.0;
            m.PGame = m.SGame > 1.0 ? Clamp(0.1 * m.SGame * m.Pen, 0.0, 1.0) : 0.0;
            return m;
        }

        /// <summary>Skladniki przewagi w jednym napisie (linie OB1 i O0).</summary>
        private static string AdvText(Meas m)
        {
            return "przewaga " + F(m.Adv, "0.000") + " = mury poziom " + m.Level + " (baza " + F(m.Base, "0.00") + (m.Breach ? ", WYRWA x0.25" : "") + ") / machiny oblegajacych "
                 + F(m.Div, "0.00") + " x perki " + F(m.Perks, "0.00") + " x stolica " + F(m.CapF, "0.00") + " x machiny obroncow " + F(m.EngF, "0.00")
                 + " (" + m.DefN + " zwykle, " + m.DefNf + " ogniowe)";
        }

        /// <summary>Obrona osady: zaloga, milicja, lordowie w murach (L7) i jedzenie.</summary>
        private static string DefenceText(Settlement st)
        {
            int gar = 0, mil = 0, lp = 0, lm = 0;
            try
            {
                foreach (var mp in st.Parties)
                {
                    if (mp == null || !mp.IsActive || mp.MemberRoster == null) continue;
                    int n = mp.MemberRoster.TotalHealthyCount;
                    if (mp.IsGarrison) gar += n;
                    else if (mp.IsMilitia) mil += n;
                    else if (mp.IsLordParty && mp.Aggressiveness > 0.01f) { lp++; lm += n; }
                }
            }
            catch { }
            string food = "-";
            try
            {
                var t = st.Town;
                if (t != null)
                {
                    float ch = t.FoodChange;
                    food = ch < 0f ? F(t.FoodStocks / -ch, "0") + " dob (" + F(t.FoodStocks, "0") + ", " + F(ch, "0.0") + "/d)" : "bez ubytku (" + F(t.FoodStocks, "0") + ")";
                }
            }
            catch { }
            return "obrona: zaloga " + gar + ", milicja " + mil + ", lordowie w murach " + lp + " druzyn / " + lm + " ludzi; jedzenie " + food;
        }

        // ------------------------------------------------------------ OB1: weto decyzji AI (z SiegeRansom.AssaultPost)
        private static int _dTrue, _dPass, _dWalls, _dRoll, _dPause, _dExcl, _dBreach, _dEmpty;
        private static readonly HashSet<SiegeEvent> _vetoLogged = new HashSet<SiegeEvent>();
        private static BesiegerCamp _lastCamp; private static Meas _last; private static float _lastAt = -1f;

        /// <summary>true = szturm AI wstrzymany (przerwa po odparciu albo mury trzymaja albo losowanie wzoru gry z naszym w). Wolane tylko przy "true" gry.
        /// Wyjatek - false (wynik gry bez zmian).</summary>
        internal static bool Veto(BesiegerCamp camp)
        {
            try
            {
                if (!Live || camp == null) return false;
                var ev = camp.SiegeEvent;
                var st = ev != null ? ev.BesiegedSettlement : null;
                if (st == null || st.Town == null || !st.IsFortification) return false;
                var lp = camp.LeaderParty;
                if (lp == null || lp.IsMainParty) return false;                // gracz decyduje sam
                if (Scripted(lp) || Und(lp, st)) { _dExcl++; return false; }
                _dTrue++;
                var pz = PauseOf(ev, false);
                if (pz != null && Now < pz.Until)
                {
                    _dPause++;
                    if (LogOn && _vetoLogged.Add(ev)) VetoLine(camp, null, "przerwa po odparciu do dnia " + F(pz.Until, "0.0") + " (odparc w tym oblezeniu " + pz.Repels + ")", null);
                    return true;
                }
                if (!Ob1On) { _dPass++; return false; }
                var m = Measure(camp);
                _lastCamp = camp; _last = m; _lastAt = Now;
                if (m.Empty) { _dEmpty++; _dPass++; return false; }
                if (m.S <= 1.0)
                {
                    _dWalls++;
                    string sw = TryBreach(camp, m);
                    if (LogOn && (_vetoLogged.Add(ev) || sw != null)) VetoLine(camp, m, "mury trzymaja (s OB1 " + F(m.S, "0.000") + " <= 1)", sw);
                    return true;
                }
                if (m.PGame > 0 && m.POurs < m.PGame && MBRandom.RandomFloat >= m.POurs / m.PGame) { _dRoll++; return true; }
                _dPass++;
                return false;
            }
            catch (Exception e) { Stumble("Veto", e); return false; }
        }

        /// <summary>AI z taranem i wieza, ktorego mury dalej trzymaja, buduje trebusze (jedyne machiny RBM, ktore bija w mury). Zwraca napis zmiany albo null.</summary>
        private static string TryBreach(BesiegerCamp camp, Meas m)
        {
            try
            {
                if (!BreachOn || !m.Ram || !m.Tower || m.Hp <= 0.0) return null;
                var cur = camp.SiegeStrategy;
                if (cur == null || cur == DefaultSiegeStrategies.WearOutDefenders || cur == DefaultSiegeStrategies.Custom) return null;
                camp.SetSiegeStrategy(DefaultSiegeStrategies.WearOutDefenders);
                _dBreach++;
                return "strategia " + cur.StringId + " -> " + DefaultSiegeStrategies.WearOutDefenders.StringId + " (trebusze - jedyne machiny RBM bijace w mury)";
            }
            catch (Exception e) { Stumble("TryBreach", e); return null; }
        }

        private static void VetoLine(BesiegerCamp camp, Meas m, string why, string sw)
        {
            try
            {
                var st = camp.SiegeEvent.BesiegedSettlement;
                var lp = camp.LeaderParty;
                int men = 0;
                foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) if (pb != null) men += pb.NumberOfHealthyMembers;
                var sb = new StringBuilder(700);
                sb.Append("Mury oblezen (OB1): dzien ").Append(Today).Append(" | osada ").Append(st.StringId).Append(" (").Append(Nm(st)).Append(", ").Append(st.IsTown ? "miasto" : "zamek")
                  .Append(", stolica ").Append(IsCapital(st) ? "tak" : "nie").Append(") | wodz ").Append(Hn(lp)).Append(" (frakcja ").Append(lp.MapFaction != null ? lp.MapFaction.StringId : "-")
                  .Append("), ludzi ").Append(men).Append(", oblezenie ").Append(F(camp.SiegeEvent.SiegeStartTime.ElapsedDaysUntilNow, "0.0")).Append(" dob | powod: ").Append(why);
                if (m != null)
                    sb.Append(" | HP murow ").Append(F(m.Hp * 100.0, "0")).Append("%, taran ").Append(m.Ram ? "tak" : "nie").Append(", wieza ").Append(m.Tower ? "tak" : "nie")
                      .Append(" | w OB1 ").Append(F(m.W, "0.000")).Append(" (gra ").Append(F(m.WGame, "0.000")).Append(") | ").Append(AdvText(m))
                      .Append(" | sila oblegajacy ").Append(F(m.Att, "0")).Append(" / obroncy ").Append(F(m.Def, "0")).Append(" | s OB1 ").Append(F(m.S, "0.000")).Append(" (gra ").Append(F(m.SGame, "0.000")).Append(')');
                try { sb.Append(" | strategia ").Append(camp.SiegeStrategy != null ? camp.SiegeStrategy.StringId : "-"); } catch { }
                if (sw != null) sb.Append(" | ZMIANA: ").Append(sw);
                sb.Append(" | ").Append(DefenceText(st)).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("VetoLine", e); }
        }

        /// <summary>Pola OB1 do linii "Okup oblezenia (O0)" (decyzja przepuszczona). Uzywa miary z tej samej chwili, gdy jest.</summary>
        internal static string O0Part(BesiegerCamp camp)
        {
            try
            {
                if (camp == null || camp.SiegeEvent == null) return "";
                Meas m = (_lastCamp == camp && _last != null && Math.Abs(Now - _lastAt) < 0.01f) ? _last : Measure(camp);
                return " | OB1 " + (Ob1On ? "tak" : "nie") + ": HP murow " + F(m.Hp * 100.0, "0") + "%, taran " + (m.Ram ? "tak" : "nie") + ", wieza " + (m.Tower ? "tak" : "nie")
                     + ", w " + F(m.W, "0.000") + " (gra " + F(m.WGame, "0.000") + "), s OB1 " + (m.Empty ? "pusta warownia" : F(m.S, "0.000")) + " (gra " + (m.Empty ? "-" : F(m.SGame, "0.000"))
                     + "), szansa na mysl " + F(m.POurs * 100.0, "0.0") + "% (gra " + F(m.PGame * 100.0, "0.0") + "%) | " + AdvText(m) + " | " + DefenceText(camp.SiegeEvent.BesiegedSettlement);
            }
            catch (Exception e) { Stumble("O0Part", e); return ""; }
        }

        // ------------------------------------------------------------ OB2: przerwa po odparciu (pamiec + zapis)
        private sealed class Pause { public SiegeEvent Ev; public string St; public double Start; public float Until; public int Repels; }
        private static readonly Dictionary<SiegeEvent, Pause> _pauses = new Dictionary<SiegeEvent, Pause>();
        private static readonly List<Pause> _pendingPauses = new List<Pause>();

        private static Pause PauseOf(SiegeEvent ev, bool create)
        {
            if (ev == null) return null;
            Pause p;
            if (_pauses.TryGetValue(ev, out p)) return p;
            var st = ev.BesiegedSettlement;
            if (_pendingPauses.Count > 0 && st != null)
            {
                double start = ev.SiegeStartTime.ToHours;
                for (int i = _pendingPauses.Count - 1; i >= 0; i--)
                {
                    var r = _pendingPauses[i];
                    if (r.St != st.StringId || Math.Abs(r.Start - start) > 0.05) continue;
                    _pendingPauses.RemoveAt(i);
                    r.Ev = ev;
                    _pauses[ev] = r;
                    return r;
                }
            }
            if (!create || st == null) return null;
            p = new Pause { Ev = ev, St = st.StringId, Start = ev.SiegeStartTime.ToHours };
            _pauses[ev] = p;
            return p;
        }

        // ------------------------------------------------------------ OB2/OB3: rejestr szturmow AI
        private sealed class Assault
        {
            public Settlement St; public float Start; public float Thr; public int Men0, Def0, LastCas = -1; public double Loss;
            public string Fac, Leader; public bool Capital, Repelled, Outside, FromWalls, Off; public int WallRounds, FieldRounds; public int Fort = -1, Side = -1;   // Off - odparcie nie wyszlo (wyjatek), dalej jak w grze
        }
        private static readonly Dictionary<MapEvent, Assault> _assaults = new Dictionary<MapEvent, Assault>();
        private static int _dAssaults, _dWon, _dRepelled, _dDefWon, _dNone, _dOutside, _dFromWalls, _dFromWallsHeld, _dScripted;
        private static double _dLossSum, _dThrSum, _dPauseSum;

        /// <summary>Nasluch MapEventStarted: szturm AI (nie gracz, nie skrypt, nie Inni, nie morze) dostaje prog strat. Nie rzuca.</summary>
        internal static void OnMapEventStarted(MapEvent me, PartyBase attacker, PartyBase defender)
        {
            try
            {
                if (!Live || me == null || !me.IsSiegeAssault) return;
                var st = me.MapEventSettlement;
                if (st == null || !st.IsFortification) return;
                if (me.IsNavalMapEvent) return;
                var lpb = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                var lp = lpb != null && lpb.IsMobile ? lpb.MobileParty : null;
                if (lp == null || lp.IsMainParty) return;
                if (Scripted(lp)) { _dScripted++; return; }
                if (Und(lp, st)) return;
                float l = RepelLoss, r = RepelSpread;
                var a = new Assault
                {
                    St = st, Start = Now, Thr = l > 0f ? (float)Clamp(l + r * (2.0 * MBRandom.RandomFloat - 1.0), 0.01, 0.95) : 0f,
                    Men0 = me.AttackerSide.TroopCount, Def0 = me.DefenderSide != null ? me.DefenderSide.TroopCount : 0,
                    Fac = lp.MapFaction != null ? lp.MapFaction.StringId : "-", Leader = Hn(lp), Capital = IsCapital(st)
                };
                _assaults[me] = a;
                _dAssaults++;
            }
            catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        /// <summary>Postfiks MapEvent.Update (po rundzie symulacji bitwy bez gracza): odparcie szturmu AI. Szybkie wyjscie dla bitew spoza rejestru.</summary>
        public static void UpdatePost(MapEvent __instance)
        {
            if (_assaults.Count == 0 || __instance == null) return;
            Assault a;
            if (!_assaults.TryGetValue(__instance, out a)) return;
            try
            {
                if (a.Repelled || a.Off || __instance.IsFinalized || __instance.HasWinner || __instance.IsPlayerMapEvent) return;
                if (RepelLoss <= 0f || a.Thr <= 0f) return;
                var att = __instance.AttackerSide; var def = __instance.DefenderSide;
                if (att == null || def == null) return;
                int cas = att.TroopCasualties;
                if (cas == a.LastCas) return;
                a.LastCas = cas;
                int left = att.TroopCount;
                a.Loss = cas / (double)Math.Max(1, cas + left);
                if (a.Loss < a.Thr) return;
                var st = a.St;
                if (st == null || st.CurrentSiegeState == Settlement.SiegeState.InTheLordsHall) return;
                int dmen = def.TroopCount;
                if (dmen <= (st.IsTown ? 100 : 30)) return;                 // mury pracuja tylko z obsada (ten sam prog, co gra)
                if (__instance.IsSiegeOutside && !a.FromWalls) return;        // bitwa w polu (odsiecz wieksza od twierdzy) - zwykla bitwa
                if (!__instance.IsSiegeAssault && !__instance.IsSiegeOutside) return;
                var lpb = att.LeaderParty;
                if (lpb == null || !lpb.IsMobile || lpb.MobileParty == null) return;
                Repel(__instance, a, left, dmen);
            }
            catch (Exception e) { Stumble("UpdatePost", e); }
        }

        private static void Repel(MapEvent me, Assault a, int left, int dmen)
        {
            var st = a.St;
            var ev = st.SiegeEvent;
            var camp = ev != null ? ev.BesiegerCamp : null;
            var lp = camp != null && camp.LeaderParty != null ? camp.LeaderParty : me.AttackerSide.LeaderParty.MobileParty;
            // skladniki przerwy
            double morale = 50.0; long menW = 0; double mor = 0;
            try
            {
                foreach (var mep in me.AttackerSide.Parties)
                {
                    var pb = mep != null ? mep.Party : null;
                    if (pb == null || !pb.IsMobile || pb.MobileParty == null) continue;
                    int n = pb.NumberOfHealthyMembers;
                    if (n <= 0) continue;
                    mor += pb.MobileParty.Morale * n; menW += n;
                }
                if (menW > 0) morale = mor / menW;
            }
            catch { }
            double fM = Clamp(100.0 / (morale + 50.0), 0.67, 2.0);
            double ratio = left / (double)Math.Max(1, dmen);
            double fL = Clamp(3.0 / Math.Max(0.01, ratio), 0.5, 2.0);
            double val = 0; long wage = 0;
            try { if (lp != null && lp.MapFaction != null) val = st.GetSettlementValueForFaction(lp.MapFaction); } catch { }
            try { if (lp != null) wage = CitySack.NeedOf(lp).ArmyWage; } catch { }
            double worth = wage > 0 ? val / wage : 9999.0;
            double fO = Clamp(Math.Sqrt(182.0 / Math.Max(1.0, worth)), 0.5, 2.0);
            double b = PauseDays;
            double T = Clamp(b * fM * fL * fO, b * 3.0 / 7.0, b * 4.0);
            // FinishBattleAndKeepSiegeEvent ustawia _keepSiegeEvent PRZED RecalculateShortTermBehavior wodza - wyjatek w nim zostawilby bitwe z flaga, przez ktora
            // zwyciestwo oblegajacych nie dawaloby zdobycia (MapEvent.FinalizeEventAux :2088). Najpierw ta sama metoda u nas - wyjatek = bez odparcia (szturm jak w grze)
            try { me.AttackerSide.LeaderParty.MobileParty.RecalculateShortTermBehavior(); }
            catch (Exception e) { Stumble("Repel.Recalculate", e); a.Off = true; return; }
            var pz = PauseOf(ev, true);
            if (pz != null) { pz.Until = Now + (float)T; pz.Repels++; }
            a.Repelled = true;
            _dRepelled++; _dLossSum += a.Loss; _dThrSum += a.Thr; _dPauseSum += T;
            if (LogOn)
            {
                try
                {
                    var sb = new StringBuilder(700);
                    sb.Append("Szturm odparty (OB2): dzien ").Append(Today).Append(" | osada ").Append(st.StringId).Append(" (").Append(Nm(st)).Append(", ").Append(st.IsTown ? "miasto" : "zamek")
                      .Append(", stolica ").Append(a.Capital ? "tak" : "nie").Append(") | frakcja ").Append(a.Fac).Append(", wodz ").Append(a.Leader)
                      .Append(" | ludzi na starcie ").Append(a.Men0).Append(", zostalo ").Append(left).Append("; obroncow bylo ").Append(a.Def0).Append(", zostalo ").Append(dmen)
                      .Append(" | prog armii ").Append(F(a.Thr * 100.0, "0.0")).Append("%, straty ").Append(F(a.Loss * 100.0, "0.0")).Append('%')
                      .Append(" | godzin szturmu ").Append(F((Now - a.Start) * 24.0, "0")).Append(" | bitwa ").Append(me.IsSiegeOutside ? "z murow (OB3)" : "szturm")
                      .Append(" | przerwa ").Append(F(T, "0.0")).Append(" dob do dnia ").Append(F(Now + T, "0.0")).Append(" (podstawa ").Append(F(b, "0.0"))
                      .Append(" x morale ").Append(F(morale, "0")).Append(" -> ").Append(F(fM, "0.00"))
                      .Append(" x ludzie ").Append(left).Append('/').Append(dmen).Append(" = ").Append(F(ratio, "0.00")).Append(" -> ").Append(F(fL, "0.00"))
                      .Append(" x oplacalnosc ").Append(F(val, "0")).Append(" / zold ").Append(wage).Append("/d = ").Append(worth >= 9999 ? "-" : F(worth, "0")).Append(" dob -> ").Append(F(fO, "0.00"))
                      .Append(") | odparc w tym oblezeniu ").Append(pz != null ? pz.Repels : 1).Append('.');
                    Log.Info(sb.ToString());
                }
                catch (Exception e) { Stumble("RepelLine", e); }
            }
            me.FinishBattleAndKeepSiegeEvent();                                 // droga gry: bitwa bez zwyciezcy, oblezenie trwa (PlayerEncounter.ContinueBattle)
        }

        /// <summary>Postfiks DefaultCombatSimulationModel.GetSimulationTicksForBattleRound (OB3). Wyjscie po typie bitwy i rejestrze.</summary>
        public static void TicksPost(MapEvent mapEvent, ref ValueTuple<int, int> __result)
        {
            if (_assaults.Count == 0 || mapEvent == null || mapEvent.EventType != MapEvent.BattleTypes.SiegeOutside) return;
            Assault a;
            if (!_assaults.TryGetValue(mapEvent, out a)) return;
            try
            {
                if (!a.Outside) { a.Outside = true; _dOutside++; }
                if (!FromWallsOn || mapEvent.IsInvulnerable || mapEvent.RetreatingSide != BattleSideEnum.None) return;
                var st = mapEvent.MapEventSettlement;
                if (st == null || !st.IsFortification || st.CurrentSiegeState == Settlement.SiegeState.InTheLordsHall) return;
                int D = mapEvent.DefenderSide.NumRemainingSimulationTroops, A = mapEvent.AttackerSide.NumRemainingSimulationTroops;
                if ((st.IsTown && D <= 100) || (st.IsCastle && D <= 30)) { a.FieldRounds++; return; }
                int fort = 0, tot = 0;
                foreach (var mep in mapEvent.DefenderSide.Parties)
                {
                    var pb = mep != null ? mep.Party : null;
                    if (pb == null) continue;
                    int n = pb.NumberOfHealthyMembers;
                    tot += n;
                    var mp = pb.IsMobile ? pb.MobileParty : null;
                    if (mp == null || mp.CurrentSettlement == st || mp.IsGarrison || mp.IsMilitia) fort += n;
                }
                a.Fort = fort; a.Side = tot;
                if (tot <= 0 || fort / (double)tot < WallsShare) { a.FieldRounds++; return; }
                // wzor szturmu gry (DefaultCombatSimulationModel.cs:251-256), te same liczby
                float adv = Campaign.Current.Models.CombatSimulationModel.GetSettlementAdvantage(st) * 0.7f;
                int atk = TaleWorlds.Library.MathF.Round(1.5f + TaleWorlds.Library.MathF.Pow(D, 0.3f)) * 2;
                int dfn = TaleWorlds.Library.MathF.Round(0.5f + TaleWorlds.Library.MathF.Max(1f + TaleWorlds.Library.MathF.Pow(D, 0.3f) * adv, (float)((D + 1) / (A + 1)))) * 2;
                __result = new ValueTuple<int, int>(dfn, atk);
                a.WallRounds++;
                if (!a.FromWalls) { a.FromWalls = true; _dFromWalls++; }
            }
            catch (Exception e) { Stumble("TicksPost", e); }
        }

        /// <summary>Nasluch MapEventEnded: wynik szturmu AI (zdobyty / odparty / obronca wygral / bez rozstrzygniecia) i linia OB3. Nie rzuca.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || _assaults.Count == 0) return;
                Assault a;
                if (!_assaults.TryGetValue(me, out a)) return;
                _assaults.Remove(me);
                var w = me.WinningSide;
                if (a.Repelled) { }
                else if (w == BattleSideEnum.Attacker) _dWon++;
                else if (w == BattleSideEnum.Defender) _dDefWon++;
                else _dNone++;
                bool held = a.Repelled || w == BattleSideEnum.Defender;
                if (a.FromWalls && held) _dFromWallsHeld++;
                if (a.Outside && LogOn)
                {
                    var st = a.St;
                    Log.Info("Bitwa ze szturmu (OB3): dzien " + Today + " | osada " + (st != null ? st.StringId : "-") + " (" + Nm(st) + ", " + (st != null && st.IsTown ? "miasto" : "zamek")
                             + ") | frakcja " + a.Fac + ", wodz " + a.Leader + " | ludzie twierdzy " + (a.Fort < 0 ? "-" : a.Fort.ToString(Inv)) + " / strona obrony "
                             + (a.Side < 0 ? "-" : a.Side.ToString(Inv)) + " (udzial " + (a.Side > 0 ? F(a.Fort / (double)a.Side, "0.00") : "-") + ", prog " + F(WallsShare, "0.00")
                             + ") | rundy z murow " + a.WallRounds + ", w polu " + a.FieldRounds + " | wynik " + (a.Repelled ? "odparty (OB2)" : w == BattleSideEnum.Attacker ? "zdobyte"
                             : w == BattleSideEnum.Defender ? "wygrala twierdza" : "bez rozstrzygniecia") + ", twierdza " + (held ? "utrzymana" : "nie") + ".");
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        // ------------------------------------------------------------ OB4: tempo budowy strony oblegajacej (z WorldPace.SiegePostfix)
        private sealed class PrepRec { public int Day; public float F; }
        private static readonly Dictionary<SiegeEvent, PrepRec> _prep = new Dictionary<SiegeEvent, PrepRec>();
        private static readonly HashSet<SiegeEvent> _dPrepAi = new HashSet<SiegeEvent>(), _dPrepPlayer = new HashSet<SiegeEvent>(), _dPrepExcl = new HashSet<SiegeEvent>();

        /// <summary>Mnoznik OB4 dla strony oblegajacej (1 = bez zmian). Pamiec dnia na oblezenie. Nie rzuca.</summary>
        internal static float PrepFactor(SiegeEvent ev, ISiegeEventSide side)
        {
            try
            {
                if (ev == null || side == null || side.BattleSide != BattleSideEnum.Attacker) return 1f;
                var s = Settings.Current;
                if (s == null) return 1f;
                int today = Today;
                PrepRec r;
                if (_prep.TryGetValue(ev, out r) && r.Day == today) return r.F;
                bool first = r == null;
                if (r == null) { r = new PrepRec(); _prep[ev] = r; }
                r.Day = today; r.F = 1f;
                double pct = Clamp(s.SiegeBesiegerPrepPercent, 10, 100) / 100.0;
                double tf = float.IsNaN(s.SiegeTownPrepFactor) ? 1.0 : Clamp(s.SiegeTownPrepFactor, 1.0, 4.0);
                double cf = float.IsNaN(s.SiegeCapitalPrepFactor) ? 1.0 : Clamp(s.SiegeCapitalPrepFactor, 1.0, 4.0);
                if (pct >= 1.0 && tf <= 1.0 && cf <= 1.0) return 1f;
                var camp = ev.BesiegerCamp;
                var st = ev.BesiegedSettlement;
                var lp = camp != null ? camp.LeaderParty : null;
                if (st == null || lp == null) return 1f;
                bool player = lp.IsMainParty || (MobileParty.MainParty != null && MobileParty.MainParty.BesiegerCamp == camp);
                string excl = Und(lp, st) ? "Inni" : Scripted(lp) ? "skrypt fabuly" : null;
                bool cap = IsCapital(st);
                double cls = cap ? cf : st.IsTown ? tf : 1.0;
                if (excl == null) r.F = (float)(pct / cls);
                if (first && LogOn)
                    Log.Info("Mury oblezen (OB4): dzien " + today + " | osada " + st.StringId + " (" + Nm(st) + ", " + (cap ? "stolica" : st.IsTown ? "miasto" : "zamek") + ") | wodz " + Hn(lp)
                             + " (frakcja " + (lp.MapFaction != null ? lp.MapFaction.StringId : "-") + ") | gracz " + (player ? "tak" : "nie")
                             + " | tempo budowy obozu i machin oblegajacych x" + F(r.F, "0.000") + (excl != null ? " (wykluczone: " + excl + ")" : " (" + F(pct * 100.0, "0") + "% / klasa " + F(cls, "0.00") + ")") + ".");
                return r.F;
            }
            catch (Exception e) { Stumble("PrepFactor", e); return 1f; }
            finally { Count(ev, side); }
        }

        private static void Count(SiegeEvent ev, ISiegeEventSide side)
        {
            try
            {
                if (ev == null || side == null || side.BattleSide != BattleSideEnum.Attacker) return;
                PrepRec r;
                if (!_prep.TryGetValue(ev, out r)) return;
                var camp = ev.BesiegerCamp; var lp = camp != null ? camp.LeaderParty : null;
                if (lp == null) return;
                if (r.F == 1f) _dPrepExcl.Add(ev);
                else if (lp.IsMainParty || (MobileParty.MainParty != null && MobileParty.MainParty.BesiegerCamp == camp)) _dPrepPlayer.Add(ev);
                else _dPrepAi.Add(ev);
            }
            catch { }
        }

        // ------------------------------------------------------------ pomiary (tylko log)
        private static readonly Dictionary<string, int> _dDispersed = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _dEngWall, _dEngAnti, _dEngRam, _dEngTower, _dEngPlayer, _dWallHits, _dWallBroken, _dSiegesNew;

        internal static void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayers)
        {
            try
            {
                var lp = army != null ? army.LeaderParty : null;
                if (lp == null || (lp.BesiegerCamp == null && lp.BesiegedSettlement == null)) return;
                string k = reason.ToString();
                int c; _dDispersed.TryGetValue(k, out c); _dDispersed[k] = c + 1;
            }
            catch (Exception e) { Stumble("OnArmyDispersed", e); }
        }

        internal static void OnEngineBuilt(SiegeEvent ev, BattleSideEnum side, SiegeEngineType t)
        {
            try
            {
                if (ev == null || t == null || side != BattleSideEnum.Attacker) return;
                var camp = ev.BesiegerCamp;
                if (camp != null && camp.LeaderParty != null && camp.LeaderParty.IsMainParty) { _dEngPlayer++; return; }
                if (t == DefaultSiegeEngineTypes.Ram || t == DefaultSiegeEngineTypes.ImprovedRam) _dEngRam++;
                else if (t == DefaultSiegeEngineTypes.SiegeTower) _dEngTower++;
                else if (t.IsRanged && !t.IsAntiPersonnel) _dEngWall++;
                else if (t.IsRanged) _dEngAnti++;
            }
            catch (Exception e) { Stumble("OnEngineBuilt", e); }
        }

        internal static void OnWallHit(MobileParty besieger, Settlement st, BattleSideEnum side, SiegeEngineType t, bool broken)
        {
            _dWallHits++;
            if (broken) _dWallBroken++;
        }

        internal static void OnSiegeStarted(SiegeEvent ev) { _dSiegesNew++; }

        internal static void OnSiegeEnded(SiegeEvent ev)
        {
            try
            {
                if (ev == null) return;
                _pauses.Remove(ev);
                _prep.Remove(ev);
                _vetoLogged.Remove(ev);
            }
            catch (Exception e) { Stumble("OnSiegeEnded", e); }
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                // porzadek: bitwy i oblezenia, ktorych juz nie ma
                List<MapEvent> deadMe = null;
                foreach (var kv in _assaults) if (kv.Key == null || kv.Key.IsFinalized) { if (deadMe == null) deadMe = new List<MapEvent>(); deadMe.Add(kv.Key); }
                if (deadMe != null) foreach (var me in deadMe) _assaults.Remove(me);
                List<SiegeEvent> dead = null;
                foreach (var kv in _pauses) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) _pauses.Remove(ev);
                dead = null;
                foreach (var kv in _prep) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) _prep.Remove(ev);
                if (!LogOn) return;
                // oblezenia trwajace
                int going = 0; double longest = 0; string longestSt = "-"; var over45 = new List<string>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsFortification || st.SiegeEvent == null) continue;
                    going++;
                    double age = st.SiegeEvent.SiegeStartTime.ElapsedDaysUntilNow;
                    if (age > longest) { longest = age; longestSt = st.StringId; }
                    if (age > 45.0) over45.Add(st.StringId + " " + F(age, "0") + " dob" + (IsCapital(st) ? " (stolica)" : ""));
                }
                var s = Settings.Current;
                var sb = new StringBuilder(1200);
                sb.Append("Mury oblezen (OB0): dzien ").Append(Today)
                  .Append(" | klucze: OB1 ").Append(Ob1On ? "tak" : "nie").Append(", stolica x").Append(F(CapitalFactor, "0.00")).Append(", machiny obroncow ").Append(EnginesOn ? "tak" : "nie")
                  .Append(", lamanie murow ").Append(BreachOn ? "tak" : "nie").Append(", OB2 prog ").Append(F(RepelLoss, "0.00")).Append(" +- ").Append(F(RepelSpread, "0.00"))
                  .Append(", przerwa ").Append(F(PauseDays, "0.0")).Append(" dob, OB3 ").Append(FromWallsOn ? "tak" : "nie").Append(" (udzial ").Append(F(WallsShare, "0.00")).Append(')')
                  .Append(", OB4 ").Append(s != null ? s.SiegeBesiegerPrepPercent : 100).Append("% (miasto x").Append(s != null ? F(s.SiegeTownPrepFactor, "0.00") : "-")
                  .Append(", stolica x").Append(s != null ? F(s.SiegeCapitalPrepFactor, "0.00") : "-").Append(')')
                  .Append(" | decyzje AI: gra true ").Append(_dTrue).Append(", przepuszczone ").Append(_dPass).Append(" (pusta warownia ").Append(_dEmpty).Append(")")
                  .Append(", OB1 wstrzymane: mury ").Append(_dWalls).Append(", losowanie ").Append(_dRoll).Append(", przerwa po odparciu ").Append(_dPause)
                  .Append("; wykluczone (skrypt, Inni) ").Append(_dExcl).Append("; zmiana strategii na lamanie murow ").Append(_dBreach)
                  .Append(" | przewaga podniesiona (wywolan) ").Append(_dAdv)
                  .Append(" | OB4: obozy AI ").Append(_dPrepAi.Count).Append(", z graczem ").Append(_dPrepPlayer.Count).Append(", bez zmian ").Append(_dPrepExcl.Count)
                  .Append(" | szturmy AI: rozpoczete ").Append(_dAssaults).Append(" (skrypt fabuly ").Append(_dScripted).Append("), zdobyte ").Append(_dWon).Append(", odparte ").Append(_dRepelled);
                if (_dRepelled > 0) sb.Append(" (straty sr. ").Append(F(_dLossSum / _dRepelled * 100.0, "0.0")).Append("%, prog sr. ").Append(F(_dThrSum / _dRepelled * 100.0, "0.0")).Append("%, przerwa sr. ").Append(F(_dPauseSum / _dRepelled, "0.0")).Append(" dob)");
                sb.Append(", obronca wygral ").Append(_dDefWon).Append(", bez rozstrzygniecia ").Append(_dNone)
                  .Append(" | bitwy ze szturmu z odsiecza (SiegeOutside) ").Append(_dOutside).Append(", z murow ").Append(_dFromWalls).Append(" (twierdza utrzymana ").Append(_dFromWallsHeld).Append(')')
                  .Append(" | armie rozwiazane przy oblezeniu: ");
                if (_dDispersed.Count == 0) sb.Append('0');
                else { bool f1 = true; foreach (var kv in _dDispersed) { if (!f1) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); f1 = false; } }
                sb.Append(" | machiny AI zbudowane: na mury (trebusze) ").Append(_dEngWall).Append(", przeciw machinom ").Append(_dEngAnti).Append(", tarany ").Append(_dEngRam)
                  .Append(", wieze ").Append(_dEngTower).Append("; gracz ").Append(_dEngPlayer)
                  .Append(" | trafienia w mury ").Append(_dWallHits).Append(", odcinki rozbite ").Append(_dWallBroken)
                  .Append(" | oblezenia: nowe ").Append(_dSiegesNew).Append(", trwajace ").Append(going).Append(", najdluzsze ").Append(F(longest, "0.0")).Append(" dob (").Append(longestSt).Append(')')
                  .Append(", ponad 45 dob: ").Append(over45.Count == 0 ? "-" : string.Join(", ", over45.ToArray()))
                  .Append(" | w pamieci: szturmy ").Append(_assaults.Count).Append(", przerwy ").Append(_pauses.Count + _pendingPauses.Count)
                  .Append(" | potkniecia ").Append(_dStumbles).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }

        private static void ZeroDay()
        {
            _dTrue = _dPass = _dWalls = _dRoll = _dPause = _dExcl = _dBreach = _dEmpty = _dAdv = 0;
            _dAssaults = _dWon = _dRepelled = _dDefWon = _dNone = _dOutside = _dFromWalls = _dFromWallsHeld = _dScripted = 0;
            _dLossSum = _dThrSum = _dPauseSum = 0;
            _dEngWall = _dEngAnti = _dEngRam = _dEngTower = _dEngPlayer = _dWallHits = _dWallBroken = _dSiegesNew = 0;
            _dDispersed.Clear(); _dPrepAi.Clear(); _dPrepPlayer.Clear(); _dPrepExcl.Clear(); _vetoLogged.Clear(); _dStumbles = 0;
        }

        /// <summary>Z SiegeRansom.Reset (konstruktor ArmouryBehavior: nowa kampania i kazde wczytanie).</summary>
        internal static void Reset()
        {
            _assaults.Clear(); _pauses.Clear(); _pendingPauses.Clear(); _prep.Clear(); _capDay.Clear();   // zapis (Import) przychodzi PO konstruktorze
            _lastCamp = null; _last = null; _lastAt = -1f;
            ZeroDay();
            _stumbles = 0; _err.Clear();
        }

        // ------------------------------------------------------------ zapis "arm_siege_walls" (wersja 1): przerwy po odparciu trwajacych oblezen
        internal static string Export()
        {
            var sb = new StringBuilder(64);
            sb.Append('1');
            float now = Now;
            foreach (var kv in _pauses)
            {
                var p = kv.Value;
                if (p == null || kv.Key == null || p.St == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) continue;
                sb.Append(";P|").Append(p.St).Append('|').Append(p.Start.ToString("R", Inv)).Append('|').Append(p.Until.ToString("R", Inv)).Append('|').Append(p.Repels.ToString(Inv));
            }
            foreach (var p in _pendingPauses)
            {
                var st = Settlement.Find(p.St);
                if (st == null || st.SiegeEvent == null || Math.Abs(st.SiegeEvent.SiegeStartTime.ToHours - p.Start) > 0.05) continue;
                sb.Append(";P|").Append(p.St).Append('|').Append(p.Start.ToString("R", Inv)).Append('|').Append(p.Until.ToString("R", Inv)).Append('|').Append(p.Repels.ToString(Inv));
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _pendingPauses.Clear(); _pauses.Clear();
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(';');
            if (parts.Length == 0 || parts[0] != "1") { Log.Info("Mury oblezen: zapis arm_siege_walls w nieznanej wersji - pominiety (przerwy po odparciu od nowa)."); return; }
            int bad = 0;
            for (int i = 1; i < parts.Length; i++)
            {
                try
                {
                    var a = parts[i].Split('|');
                    if (a.Length >= 5 && a[0] == "P")
                        _pendingPauses.Add(new Pause { St = a[1], Start = double.Parse(a[2], NumberStyles.Float, Inv), Until = float.Parse(a[3], NumberStyles.Float, Inv), Repels = int.Parse(a[4], Inv) });
                    else bad++;
                }
                catch { bad++; }
            }
            if (bad > 0) Log.Info("Mury oblezen: zapis arm_siege_walls - pominieto " + bad + " uszkodzonych wpisow.");
        }

        // ------------------------------------------------------------ latki w kampanii (z SiegeRansom.EnsureHooks, raz na proces)
        private static string _hookAdv = "nie zakladana", _hookUpdate = "nie zakladana", _hookTicks = "nie zakladana";

        internal static void EnsureHooks()
        {
            try
            {
                var ma = AccessTools.Method(typeof(DefaultCombatSimulationModel), "GetSettlementAdvantage", new[] { typeof(Settlement) });
                if (ma == null) _hookAdv = "BRAK (DefaultCombatSimulationModel.GetSettlementAdvantage)";
                else { Seal31.Patch("mury oblezen: przewaga osady (DefaultCombatSimulationModel.GetSettlementAdvantage)", ma, typeof(SiegeWalls), null, nameof(AdvPost), null, Priority.Normal, Priority.Low); _hookAdv = "wpieta"; }
            }
            catch (Exception e) { _hookAdv = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks.Adv", e); }
            try
            {
                var mu = AccessTools.Method(typeof(MapEvent), "Update", Type.EmptyTypes);
                var mk = AccessTools.Method(typeof(MapEvent), "FinishBattleAndKeepSiegeEvent", Type.EmptyTypes);
                if (mu == null || mk == null) _hookUpdate = "BRAK (MapEvent.Update / FinishBattleAndKeepSiegeEvent)";
                else { Seal31.Patch("mury oblezen: odparcie szturmu (MapEvent.Update)", mu, typeof(SiegeWalls), null, nameof(UpdatePost), null, Priority.Normal, Priority.Low); _hookUpdate = "wpieta"; }
            }
            catch (Exception e) { _hookUpdate = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks.Update", e); }
            try
            {
                var mt = AccessTools.Method(typeof(DefaultCombatSimulationModel), "GetSimulationTicksForBattleRound", new[] { typeof(MapEvent) });
                if (mt == null) _hookTicks = "BRAK (DefaultCombatSimulationModel.GetSimulationTicksForBattleRound)";
                else { Seal31.Patch("mury oblezen: bitwa ze szturmu z murow (DefaultCombatSimulationModel.GetSimulationTicksForBattleRound)", mt, typeof(SiegeWalls), null, nameof(TicksPost), null, Priority.Normal, Priority.Low); _hookTicks = "wpieta"; }
            }
            catch (Exception e) { _hookTicks = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks.Ticks", e); }
        }

        private static string Owners(System.Reflection.MethodBase m)
        {
            try
            {
                if (m == null) return "brak metody";
                var info = Harmony.GetPatchInfo(m);
                if (info == null || info.Owners == null || info.Owners.Count == 0) return "-";
                var l = new List<string>();
                foreach (var o in info.Owners) l.Add(o);
                return string.Join(", ", l.ToArray());
            }
            catch (Exception e) { return "blad " + e.GetType().Name; }
        }

        private static string TypeOf(object o) { return o == null ? "-" : o.GetType().FullName; }

        /// <summary>Linia startowa (ArmouryBehavior.OnSessionLaunched, po Seal31.EnsureHooks): czynne modele, stan latek i wlasciciele latek Harmony.</summary>
        internal static void SessionStart()
        {
            try
            {
                if (Campaign.Current == null || Campaign.Current.Models == null) return;
                var mods = Campaign.Current.Models;
                var s = Settings.Current;
                var mAssault = AccessTools.Method(typeof(BesiegerCamp), "StartingAssaultOnBesiegedSettlementIsLogical");
                var mAdv = AccessTools.Method(typeof(DefaultCombatSimulationModel), "GetSettlementAdvantage", new[] { typeof(Settlement) });
                var mTicks = AccessTools.Method(typeof(DefaultCombatSimulationModel), "GetSimulationTicksForBattleRound", new[] { typeof(MapEvent) });
                var mUpd = AccessTools.Method(typeof(MapEvent), "Update", Type.EmptyTypes);
                var mRun = AccessTools.Method(typeof(DefaultEncounterModel), "GetMapEventSideRunAwayChance");
                var mBuild = AccessTools.Method(typeof(DefaultSiegeEventModel), "GetConstructionProgressPerHour");
                Log.Info("Mury oblezen (OB start): latki - przewaga osady " + _hookAdv + ", odparcie szturmu (MapEvent.Update) " + _hookUpdate + ", bitwa z murow " + _hookTicks
                         + " | modele: symulacja " + TypeOf(mods.CombatSimulationModel) + ", spotkania " + TypeOf(mods.EncounterModel) + ", armie " + TypeOf(mods.ArmyManagementCalculationModel)
                         + ", oblezenie " + TypeOf(mods.SiegeEventModel) + ", strategie " + TypeOf(mods.SiegeStrategyActionModel)
                         + " | wlasciciele latek Harmony: decyzja szturmu [" + Owners(mAssault) + "], przewaga [" + Owners(mAdv) + "], tury rundy [" + Owners(mTicks) + "], MapEvent.Update ["
                         + Owners(mUpd) + "], ucieczka strony [" + Owners(mRun) + "], budowa machin (Default) [" + Owners(mBuild) + "]"
                         + " | klucze: OB1 " + (Ob1On ? "tak" : "nie") + ", stolica x" + F(CapitalFactor, "0.00") + ", machiny obroncow " + (EnginesOn ? "tak" : "nie") + ", lamanie murow "
                         + (BreachOn ? "tak" : "nie") + ", OB2 prog " + F(RepelLoss, "0.00") + " +- " + F(RepelSpread, "0.00") + ", przerwa " + F(PauseDays, "0.0") + " dob, OB3 "
                         + (FromWallsOn ? "tak" : "nie") + " (udzial " + F(WallsShare, "0.00") + "), OB4 " + (s != null ? s.SiegeBesiegerPrepPercent : 100) + "% (miasto x"
                         + (s != null ? F(s.SiegeTownPrepFactor, "0.00") : "-") + ", stolica x" + (s != null ? F(s.SiegeCapitalPrepFactor, "0.00") : "-") + "), log " + (LogOn ? "tak" : "nie") + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }
    }
}
