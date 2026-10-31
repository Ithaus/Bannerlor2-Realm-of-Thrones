using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// OKUP ZAMIAST SZTURMU - ostatni etap lupu (decyzja Jeffa 13.10, pyt. 5b "tak, i tak"; projekt docs/lup-miast/OKUP-ZAMIAST-SZTURMU-PROJEKT.md rozdz. 2-6,
    /// poprawki P1-P13 ze sprawdzenia 14.10). Ta czesc (O0, klucz SiegeRansomMeasureLog) - POMIAR NA SUCHO i wspolna regula, gra bez zmian.
    ///
    /// CHWILA (P1): decyzja gry o szturmie. Gra pyta BesiegerCamp.IsReadyToBesiege tylko z mysli AI wodza obozu (MobilePartyAi.GetBesiegeBehavior :714;
    /// BK go nie wola); IsReadyToBesiege = przygotowania 100% i StartingAssaultOnBesiegedSettlementIsLogical (BesiegerCamp.cs:375-430: stosunek sil z przewaga
    /// murow > 1, potem losowanie 10% stosunku po karze za maszyny). Latka siedzi na StartingAssaultOnBesiegedSettlementIsLogical (postfiks), nie na getterze
    /// IsReadyToBesiege: getter ma kilka bajtow IL i JIT moze go wkleic w GetBesiegeBehavior (latka gettera bylaby wtedy martwa), a metoda z petla foreach
    /// (try/finally) nie bywa wklejana. Wolajacy jest jeden (getter :55), wiec "true" = decyzja gry o szturmie, takze przy pustej warowni.
    ///
    /// CYKL: pierwsze "true" w oblezeniu otwiera cykl decyzji - regula liczona raz, linia "Okup oblezenia (O0)" raz; nastepne "true" tylko licznik. Cykl zamyka
    /// koniec szturmu tej osady (MapEventEnded) albo doba bez szturmu (stan mogl sie zmienic - nowa linia). Szturm bez zdobycia (wygrywa obronca albo nikt)
    /// przy trwajacym oblezeniu - licznik "szturmow bez zdobycia w tym oblezeniu" (P2). Pamiec: obiekt SiegeEvent (koniec oblezenia - wpis znika).
    ///
    /// REGULA (rozdz. 2 po P2-P8; ta sama dla AI i gracza):
    ///  - moneta osady = kasa kupcow (Town.Gold, cala) + kiesy ludu W, R, P, gmina (zamek - podzamcze) - CitySack.Read, te same zbiorniki co lup L2;
    ///  - okup = suma floor(SiegeRansomShare x zbiornik) (1/4, P3); okupu nie ma, gdy okup < doba zoldu armii wodza (CitySack.NeedOf.ArmyWage) - jedno zdanie
    ///    zamiast "moneta >= 4 dob zoldu" i "okup >= doba zoldu" (P3);
    ///  - nigdy: pod stolica krolestwa pana osady (CapitalTreasury.IsCapital - BK albo poczatkowa siedziba; P7), w osadach fabuly ROT (StoryIds: King's Landing
    ///    town_EW4 i Red Keep ROT_castle61 - Blackwater, Winterfell town_B2 - Bastards, Harrenhal ROT_town32, Mur ROT_castle60 i Castle Black town_B5 - Wall; P8),
    ///    gdy wodz obozu albo rod pana osady to Inni (Undead), przy buncie (IsUnderRebellionAttack);
    ///  - obronca placi (AI i gracz-oblegajacy - jedna regula): mury nie wytrzymaja - stosunek MUROW > 1 (oblegajacy / obroncy x przewaga murow^w0, w0 = 0.8 x1.25
    ///    bez taranu x1.25 bez wiezy - wzor gry BEZ czlonu czasu; recenzja 14.10: czlon czasu gry -0.02/h po 96 h to niecierpliwosc oblegajacych, nie sila murow -
    ///    po ok. 136 h w = 0 i slaba druzyna wymuszalaby okup) albo pusta warownia, i jest z czego (okup >= doba zoldu);
    ///  - AI-oblegajacy przyjmuje (P2): nigdy przy pustej warowni (gra zdobywa ja bez walki - lup 1/3 i lenno wiecej warte niz 1/4); armia w potrzebie (ta sama
    ///    NeedOf co L4) ALBO w tym oblezeniu ta sama frakcja miala juz szturm bez zdobycia ALBO szturm bylby krwawy - stosunek murow < SiegeRansomBloodyRatio
    ///    (1.5; wartosc do kalibracji po O0). Stosunek gry z czlonem czasu (ten, ktory decyduje o szturmie) tylko w linii O0.
    /// Pomiar przy wylaczonym SiegeRansomMeasureLog - brak linii (latka liczy tylko, gdy ktorys klucz okupu jest wlaczony). Wyjatek - licznik potkniec, bez
    /// globalnego wylacznika (CLAUDE.md 7); wynik gry przy wyjatku bez zmian.
    /// </summary>
    internal static partial class SiegeRansom
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ klucze
        internal static bool MeasureOn { get { var s = Settings.Current; return s != null && s.SiegeRansomMeasureLog && s.LogEnabled; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        /// <summary>Czesc kazdego zbiornika monety brana jako okup (SiegeRansomShare, przycinana do 0..1).</summary>
        internal static double Share
        {
            get
            {
                var s = Settings.Current;
                if (s == null || float.IsNaN(s.SiegeRansomShare) || s.SiegeRansomShare <= 0f) return 0.0;
                return s.SiegeRansomShare > 1f ? 1.0 : s.SiegeRansomShare;
            }
        }

        /// <summary>Prog "szturm krwawy" (P2): stosunek gry bez losowania ponizej - AI-oblegajacy przyjmuje okup. 0 = powod wylaczony.</summary>
        internal static double BloodyRatio { get { var s = Settings.Current; return s != null && !float.IsNaN(s.SiegeRansomBloodyRatio) ? Math.Max(0f, s.SiegeRansomBloodyRatio) : 1.5; } }

        // osady fabuly ROT (dekompilacja ROT.dll 8.1.8: ROT.Misc.ROTSettlements, ROT.Events.*SiegeEvent) - nigdy okupu (P8)
        private static readonly HashSet<string> StoryIds = new HashSet<string>(StringComparer.Ordinal) { "town_EW4", "ROT_castle61", "town_B2", "ROT_town32", "ROT_castle60", "town_B5" };
        internal static bool IsStory(Settlement st) { return st != null && st.StringId != null && StoryIds.Contains(st.StringId); }

        // ------------------------------------------------------------ potkniecia
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("SiegeRansom." + where, e); } catch { }
        }

        private static float Now { get { return (float)CampaignTime.Now.ToDays; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string N(long v) { return v.ToString("N0", Inv); }
        private static string F(double v, string f) { return v.ToString(f, Inv); }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ stan oblezenia (pamiec; obiekt SiegeEvent)
        internal sealed class Siege
        {
            public SiegeEvent Ev; public Settlement St;
            public bool Open; public float OpenDay; public int Trues;      // cykl decyzji o szturmie
            public int Failed; public string FailFaction;                  // szturmy bez zdobycia w tym oblezeniu (P2)
            public Judge Last;                                             // regula z ostatniego cyklu
            public string NoDeal;                                          // recenzja 14.10: w tym oblezeniu juz bez umow (powod) - umowa AI przepadla przez bitwe
                                                                           // albo oboz nie dal sie zwinac; dalej szturm jak w grze
        }
        private static readonly Dictionary<SiegeEvent, Siege> _sieges = new Dictionary<SiegeEvent, Siege>();

        private static Siege StateOf(SiegeEvent ev, bool create)
        {
            Siege s;
            if (ev == null) return null;
            if (_sieges.TryGetValue(ev, out s)) return s;
            if (!create) return null;
            s = new Siege { Ev = ev, St = ev.BesiegedSettlement };
            RestoreFailed(s);                                              // O1: szturmy bez zdobycia z zapisu (to samo oblezenie po wczytaniu)
            _sieges[ev] = s;
            return s;
        }

        // ------------------------------------------------------------ regula (O0 liczy, O1-O3 wykonuja)
        internal sealed class Judge
        {
            public Settlement St; public BesiegerCamp Camp; public MobileParty Leader; public IFaction Faction;
            public CitySack.Stock K; public readonly long[] Want = new long[5]; public long Ransom;   // 0 kupcy (kasa), 1-4 lud W, R, P, gmina
            public CitySack.Need Need; public long Wage;
            public double Ratio, Chance, Adv, W, Att, Def; public int Engines; public bool Ram, Tower, Empty;
            public double RatioWalls, W0, Def0;                            // stosunek murow bez czlonu czasu i bez polowy gry przy graczu w osadzie (recenzja 14.10)
            public int Parties, Men, UndeadMen;
            public bool Capital, Story, Undead, Rebel; public string Excl;  // wykluczenie (null - brak)
            public bool DefPays; public string DefWhy;
            public bool AccNeed, AccFailed, AccBloody, AiAccepts; public string AccWhy;
            public bool AccFailedHeld; public double Edge;                 // F1 (oblezenia 3): szturm bez zdobycia, ale przewaga oblegajacy / obroncy w pelni dalej >= prog DR
            public string Player;                                          // "w osadzie", "pan osady", "w obozie", "-"
            public int Failed; public double Days;
        }

        /// <summary>Regula dla obozu (tylko odczyt). Nie rzuca - wyjatek = null (licznik potkniec).</summary>
        internal static Judge Evaluate(BesiegerCamp camp, Siege s)
        {
            try
            {
                var ev = camp != null ? camp.SiegeEvent : null;
                var st = ev != null ? ev.BesiegedSettlement : null;
                var leader = camp != null ? camp.LeaderParty : null;
                if (st == null || st.Town == null || leader == null) return null;
                var j = new Judge { St = st, Camp = camp, Leader = leader, Faction = camp.MapFaction ?? leader.MapFaction };
                j.Days = ev.SiegeStartTime.ElapsedDaysUntilNow;
                j.Failed = s != null ? s.Failed : 0;
                // wykluczenia (P7, P8)
                j.Capital = CapitalTreasury.IsCapital(st);
                j.Story = IsStory(st);
                bool und = false;
                try { und = Undead.Party(leader) || (st.OwnerClan != null && ClanIncomeBook.IsUndeadClan(st.OwnerClan)); } catch { und = false; }
                j.Undead = und;
                try { j.Rebel = st.IsUnderRebellionAttack(); } catch { j.Rebel = false; }
                j.Excl = j.Capital ? "stolica" : j.Story ? "fabula" : j.Undead ? "Inni" : j.Rebel ? "bunt" : null;
                // druzyny obozu (P4) - ludzie
                foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege))
                {
                    var mp = pb != null ? pb.MobileParty : null;
                    if (mp == null || !mp.IsActive) continue;
                    int n = 0;
                    try { n = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0; } catch { n = 0; }
                    if (n <= 0) continue;
                    bool u = false;
                    try { u = Undead.Party(mp); } catch { u = false; }
                    j.Parties++;
                    if (u) j.UndeadMen += n; else j.Men += n;
                }
                // moneta i okup (P3)
                j.K = CitySack.Read(st);
                double share = Share;
                j.Want[0] = Fl(share, j.K.Kasa);
                for (int c = 0; c < 4; c++) j.Want[c + 1] = Fl(share, j.K.Folk[c]);
                for (int i = 0; i < 5; i++) j.Ransom += j.Want[i];
                j.Need = CitySack.NeedOf(leader);
                j.Wage = j.Need.ArmyWage;
                // stosunek gry bez losowania (P2)
                GameRatio(camp, j);
                // gracz: "w osadzie" tylko jako strona obrony - w wojnie z oblegajacymi (kryterium gry: menu_siege_leave_on_condition; neutralny gosc to nie
                // obronca - recenzja 14.10)
                var main = MobileParty.MainParty;
                bool mainDef = false;
                try { mainDef = main != null && main.CurrentSettlement == st && main.MapFaction != null && j.Faction != null && FactionManager.IsAtWarAgainstFaction(main.MapFaction, j.Faction); }
                catch { mainDef = false; }
                j.Player = mainDef ? "w osadzie"
                         : (st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan) ? "pan osady"
                         : (main != null && main.BesiegerCamp == camp) ? "w obozie" : "-";
                // obronca: placi, gdy mury nie wytrzymaja (stosunek murow bez czlonu czasu > 1 albo pusta warownia) i jest z czego - jedna regula AI i gracza
                if (j.Excl != null) { j.DefPays = false; j.DefWhy = "wykluczone (" + j.Excl + ")"; }
                else if (j.Men <= 0) { j.DefPays = false; j.DefWhy = "oboz bez zywych (same Inni albo pusty)"; }
                else if (!j.Empty && j.RatioWalls <= 1.0) { j.DefPays = false; j.DefWhy = "mury wytrzymaja (stosunek murow " + F(j.RatioWalls, "0.00") + " <= 1)"; }
                else if (j.Ransom <= 0 || j.Ransom < Math.Max(1L, j.Wage)) { j.DefPays = false; j.DefWhy = "okup " + j.Ransom + " < doba zoldu " + j.Wage; }
                else { j.DefPays = true; j.DefWhy = "mury nie wytrzymaja (" + (j.Empty ? "pusta warownia" : "stosunek murow " + F(j.RatioWalls, "0.00")) + "), okup " + j.Ransom + " >= doba zoldu " + j.Wage; }
                // AI-oblegajacy (P2): pusta warownia - nigdy (zdobycie bez walki); szturm bez zdobycia liczy sie tylko tej samej frakcji
                string fid = j.Faction != null ? j.Faction.StringId : null;
                j.AccNeed = j.Need.InNeed;
                bool same = j.Failed > 0 && s != null && fid != null && string.Equals(s.FailFaction, fid, StringComparison.Ordinal);
                // F1 (oblezenia 3, klucz SiegeRansomRepelNeedsWeakEdge; Jeff: "poczekaja do nastepnego szturmu, chyba ze przybedzie odsiecz"): po odpartym szturmie
                // okup tylko przy przewadze stopnialej ponizej progu "miazdzacej przewagi" DR (SiegeLadderMinRatio, ta sama miara: sila obozu / obroncy w pelni);
                // wodz silniejszy czeka na nastepny szturm (OB2 daje przerwe). Kazde odparcie kosztuje ok. 22% ludzi i morale - regula sama sie ogranicza
                double lr = SiegeWalls2.LadderRatio;
                j.Edge = j.Def0 > 0 ? j.Att / j.Def0 : 999.0;
                j.AccFailedHeld = same && SiegeWalls2.RepelEdgeOn && j.Edge >= lr;
                j.AccFailed = same && !j.AccFailedHeld;
                double br = BloodyRatio;
                j.AccBloody = br > 0 && !j.Empty && j.RatioWalls < br;
                j.AiAccepts = !j.Empty && (j.AccNeed || j.AccFailed || j.AccBloody);
                var why = new List<string>(3);
                if (j.AccNeed) why.Add("potrzeba");
                if (j.AccFailed) why.Add("szturm bez zdobycia x" + j.Failed);
                if (j.AccBloody) why.Add("krwawy szturm " + F(j.RatioWalls, "0.00") + " < " + F(br, "0.00"));
                if (j.Empty) j.AccWhy = "pusta warownia - zdobycie bez walki";
                else j.AccWhy = why.Count > 0 ? string.Join(", ", why.ToArray())
                              : "bez powodu (wojsko syte, " + (j.AccFailedHeld ? "po odparciu przewaga " + F(j.Edge, "0.00") + " >= " + F(lr, "0.0") + " - czeka na nastepny szturm, "
                                                               : j.Failed > 0 ? "szturm bez zdobycia innej frakcji, " : "") + "stosunek murow " + F(j.RatioWalls, "0.00") + " >= " + F(br, "0.00") + ")";
                return j;
            }
            catch (Exception e) { Stumble("Evaluate", e); return null; }
        }

        private static long Fl(double share, long v) { return v <= 0 || share <= 0 ? 0 : (long)Math.Floor(share * v); }

        /// <summary>Stosunek sil gry BEZ losowania (BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical :375-418) i szansa na mysl po karze za maszyny
        /// (:421-427). Pusta warownia (sila obroncow 0) - Empty, stosunek 999. Do tego stosunek MUROW (recenzja 14.10): ten sam wzor bez czlonu czasu
        /// (w0 = 0.8 x1.25 bez taranu x1.25 bez wiezy) i bez polowy obroncow, ktora gra liczy przy graczu w osadzie - miara "mury nie wytrzymaja". Tylko odczyt.</summary>
        internal static void GameRatio(BesiegerCamp camp, Judge j)
        {
            var ev = camp.SiegeEvent;
            var st = ev.BesiegedSettlement;
            double def0 = 0, att = 0;
            float f = MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == st ? 0.5f : 1f;
            foreach (var pb in st.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege))
            {
                if (pb == null || !pb.IsMobile) continue;
                var mp = pb.MobileParty;
                if (mp.CurrentSettlement == st && (mp.Aggressiveness > 0.01f || mp.IsMilitia || mp.IsGarrison)) def0 += pb.CalculateCurrentStrength();
            }
            double def = f * def0;
            foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) if (pb != null) att += pb.CalculateCurrentStrength();
            bool ram = false, tower = false;
            int built = 0;
            var side = ev.GetSiegeEventSide(BattleSideEnum.Attacker);
            if (side != null && side.SiegeEngines != null)
                foreach (var x in side.SiegeEngines.AllSiegeEngines())
                {
                    if (x == null || !x.IsConstructed) continue;
                    built++;
                    if (x.SiegeEngine == DefaultSiegeEngineTypes.Ram || x.SiegeEngine == DefaultSiegeEngineTypes.ImprovedRam) ram = true;
                    else if (x.SiegeEngine == DefaultSiegeEngineTypes.SiegeTower) tower = true;
                }
            var csm = Campaign.Current.Models.CombatSimulationModel;
            double adv = csm.GetSettlementAdvantage(st);
            double h0 = CampaignTime.HoursInDay * 4.0;
            double el = ev.SiegeStartTime.ElapsedHoursUntilNow;
            double w = 0.8 - (el > h0 ? (el - h0) * 0.02 : 0.0);
            double w0 = SiegeWalls.Ob1On ? 0.8 * SiegeWalls.WallHp(st) : 0.8;   // OB1: mury licza sie w pelni, dopoki nie rozbite (srednie HP odcinkow)
            if (!ram) { w *= 1.25; w0 *= 1.25; }
            if (!tower) { w *= 1.25; w0 *= 1.25; }
            j.Def = def; j.Def0 = def0; j.Att = att; j.Adv = adv; j.W = w; j.W0 = w0; j.Ram = ram; j.Tower = tower; j.Engines = built;
            if (def <= 0) { j.Empty = true; j.Ratio = 999.0; j.RatioWalls = 999.0; j.Chance = 1.0; return; }
            j.RatioWalls = att / (def0 * Math.Pow(adv, w0));
            double r = att / (def * Math.Pow(adv, w));
            j.Ratio = r;
            if (r > 1.0)
            {
                int eq = csm.GetNumberOfEquipmentsBuilt(st);
                double r2 = r * Math.Min(3, eq) / 3.0;
                double p = csm.GetMaximumSiegeEquipmentProgress(st) + 0.25 * (5 - eq);
                r2 *= 1.0 - 0.85 * (p * p);
                j.Chance = Math.Max(0.0, Math.Min(1.0, r2 * 0.1));
            }
            else j.Chance = 0.0;
        }

        // ------------------------------------------------------------ latka: decyzja gry o szturmie
        private static int _dTrues, _dCycles, _dLines, _dDefPays, _dAiAcc, _dBoth, _dAssaultEnd, _dAssaultFail, _dPlayerSide;
        private static readonly Dictionary<string, int> _dExcl = new Dictionary<string, int>(StringComparer.Ordinal);
        private static string _hookAssault = "nie zakladana";

        /// <summary>Postfiks BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical. Zwykle wywolanie z false - jedno porownanie. "true" - decyzja gry o szturmie:
        /// pierwszy raz w cyklu regula i linia O0; O1-O3 moga wynik zmienic na false (okup w toku). Wyjatek - wynik gry bez zmian.</summary>
        public static void AssaultPost(BesiegerCamp __instance, ref bool __result)
        {
            if (!__result)
            {
                if (!SiegeWalls2.Unblock(__instance)) return;
                __result = true;                                           // MB (SiegeWalls2.Unblock): gra wstrzymala szturm przez machine w budowie albo brak machin - dalej OB1 i okup jak zwykle
            }
            try
            {
                bool ransomOnly;
                if (SiegeWalls.Veto(__instance, out ransomOnly)) { __result = false; return; }   // OB1 / przerwa OB2 (SiegeWalls.cs) - PRZED okupem: okup liczy tylko po naszym "tak"
                if (ransomOnly) __result = false;                          // recenzja 307c (SiegeRepelHoldRansom): po odparciach bez szturmu, ale okup / kapitulacja ponizej liczone jak zwykle
                if (!Live || !AnyKey) return;
                var ev = __instance != null ? __instance.SiegeEvent : null;
                if (ev == null || ev.BesiegedSettlement == null || ev.BesiegedSettlement.Town == null) return;
                var s = StateOf(ev, true);
                _dTrues++;
                if (Hold(s, true)) { __result = false; return; }            // O1-O3: okup uzgodniony albo pytanie gracza w toku - szturm czeka
                s.Trues++;
                if (s.Open && Now - s.OpenDay < 1f) return;                // cykl juz rozstrzygniety - gra szturmuje jak dotad
                s.Open = true; s.OpenDay = Now; s.Trues = 1;
                _dCycles++;
                var j = Evaluate(__instance, s);
                s.Last = j;
                if (j == null) return;
                if (j.Excl != null) { int c; _dExcl.TryGetValue(j.Excl, out c); _dExcl[j.Excl] = c + 1; }
                if (j.AccFailedHeld && !j.AiAccepts && !j.Empty && j.Excl == null && j.DefPays) SiegeWalls2.NoteRepelWait();   // F1 (oblezenia 3): okup po odparciu odpadl (obronca by zaplacil) - linia "Oblezenia 3 (doba)"
                if (j.DefPays) _dDefPays++;
                if (j.AiAccepts) _dAiAcc++;
                if (j.DefPays && j.AiAccepts) _dBoth++;
                if (j.Player != "-") _dPlayerSide++;
                string outcome = Decide(s, j);                             // O1-O3 (wylaczone - "szturm (gra)")
                bool held = Hold(s, false);
                if (held) __result = false;
                if (ransomOnly)
                {
                    SiegeWalls2.NoteRetryRansom(held);
                    if (!held) outcome = "bez szturmu (307c: po odparciach te same mury; bez umowy - " + outcome + ")";
                }
                if (MeasureOn) MeasureLine(j, outcome);
            }
            catch (Exception e) { Stumble("AssaultPost", e); }
        }

        private static bool AnyKey { get { var s = Settings.Current; return s != null && (s.SiegeRansomMeasureLog || AiOn || PlayerDefOn); } }

        private static void MeasureLine(Judge j, string outcome)
        {
            var st = j.St;
            var k = j.K;
            var sb = new StringBuilder(1100);
            sb.Append("Okup oblezenia (O0): dzien ").Append(Today).Append(" | osada ").Append(st.StringId).Append(" (").Append(Nm(st)).Append(", ").Append(st.IsTown ? "miasto" : "zamek")
              .Append(", krolestwo ").Append(st.MapFaction != null ? st.MapFaction.StringId : "-").Append(", rod ").Append(st.OwnerClan != null ? st.OwnerClan.StringId : "-").Append(')')
              .Append(" | oblegajacy: frakcja ").Append(j.Faction != null ? j.Faction.StringId : "-").Append(", wodz ").Append(j.Leader.LeaderHero != null ? j.Leader.LeaderHero.Name.ToString() : j.Leader.StringId)
              .Append(" (armia ").Append(j.Leader.Army != null ? "tak" : "nie").Append("), druzyn ").Append(j.Parties).Append(" (ludzi ").Append(j.Men).Append(", Inni ").Append(j.UndeadMen).Append(')')
              .Append(", oblezenie ").Append(F(j.Days, "0.0")).Append(" dob, szturmow bez zdobycia ").Append(j.Failed)
              .Append(" | moneta: kupcy ").Append(k.Kasa).Append(", lud W ").Append(k.Folk[0]).Append(" R ").Append(k.Folk[1]).Append(" P ").Append(k.Folk[2]).Append(" gmina ").Append(k.Folk[3])
              .Append(" (kiesa ").Append(k.FolkLive ? "czynna" : "nieczynna").Append("), razem ").Append(k.Kasa + k.FolkSum)
              .Append(" | okup ").Append(j.Ransom).Append(" (czesc ").Append(F(Share, "0.000")).Append("; kupcy ").Append(j.Want[0]).Append(", lud ").Append(j.Want[1] + j.Want[2] + j.Want[3] + j.Want[4]).Append(')')
              .Append(" | zold armii ").Append(j.Wage).Append("/d, okup = ").Append(j.Wage > 0 ? F((double)j.Ransom / j.Wage, "0.0") : "-").Append(" dob zoldu")
              .Append(" | stosunek gry ").Append(j.Empty ? "pusta warownia" : F(j.Ratio, "0.000")).Append(" (oblegajacy ").Append(F(j.Att, "0")).Append(" / obroncy ").Append(F(j.Def, "0"))
              .Append(" x przewaga ").Append(F(j.Adv, "0.000")).Append("^").Append(F(j.W, "0.000")).Append("; maszyny ").Append(j.Engines).Append(j.Ram ? ", taran" : "").Append(j.Tower ? ", wieza" : "")
              .Append("; szansa na mysl ").Append(F(j.Chance * 100.0, "0.0")).Append("%)")
              .Append(" | mury ").Append(j.Empty ? "pusta warownia" : F(j.RatioWalls, "0.000")).Append(" (bez czlonu czasu: obroncy ").Append(F(j.Def0, "0")).Append(" x przewaga^")
              .Append(F(j.W0, "0.000")).Append(')')
              .Append(" | potrzeba ").Append(j.Need.InNeed ? "tak" : "nie").Append(" (niezaplacony zold ").Append(j.Need.Unpaid).Append(" z ").Append(j.Need.Parties).Append(" druzyn, kiesa rodu wodza ")
              .Append(j.Need.LeadGold).Append(" = ").Append(j.Need.LeadDays >= 999 ? "-" : F(j.Need.LeadDays, "0.0")).Append(" dob)")
              .Append(" | stolica ").Append(j.Capital ? "tak" : "nie").Append(", fabula ").Append(j.Story ? "tak" : "nie").Append(", Inni ").Append(j.Undead ? "tak" : "nie")
              .Append(", bunt ").Append(j.Rebel ? "tak" : "nie").Append(", gracz ").Append(j.Player)
              .Append(" | obronca zaplacilby ").Append(j.DefPays ? "tak" : "nie").Append(" (").Append(j.DefWhy).Append(')')
              .Append(" | AI przyjalby ").Append(j.AiAccepts ? "tak" : "nie").Append(" (").Append(j.AccWhy).Append(')')
              .Append(" | wynik ").Append(outcome)
              .Append(SiegeWalls.O0Part(j.Camp)).Append('.');                 // OB1: HP murow, taran / wieza, w, skladniki przewagi, obrona
            Log.Info(sb.ToString());
            _dLines++;
        }

        // ------------------------------------------------------------ szturmy: koniec bitwy (MapEventEnded)
        /// <summary>Nasluch MapEventEnded: koniec szturmu osady zamyka cykl decyzji; szturm bez zdobycia przy trwajacym oblezeniu - licznik P2. Szturm w trakcie
        /// umowy (gracz-oblegajacy po "Accept", gracz-obronca w toku, szturm inna droga niz mysl wodza) - umowa przepada: miasto nie placi po szturmie, ktory
        /// wlasnie odparlo (recenzja 14.10). Nie rzuca.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || !me.IsSiegeAssault || me.MapEventSettlement == null || (_sieges.Count == 0 && _deals.Count == 0)) return;
                var st = me.MapEventSettlement;
                _dAssaultEnd++;
                if (st.SiegeEvent != null && _deals.Count > 0) DropDeals(st.SiegeEvent, "szturm w trakcie umowy", "the walls were stormed");
                var s = StateOf(st.SiegeEvent, false);
                if (s == null) return;
                s.Open = false;
                if (me.WinningSide != BattleSideEnum.Attacker && st.SiegeEvent != null)
                {
                    s.Failed++;
                    _dAssaultFail++;
                    var lp = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                    s.FailFaction = lp != null && lp.MapFaction != null ? lp.MapFaction.StringId : s.FailFaction;
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        /// <summary>Nasluch OnSiegeEventEnded: oblezenie skonczone - stan i okup w toku znikaja (O1-O3 dopisuja swoje). Nie rzuca.</summary>
        internal static void OnSiegeEnded(SiegeEvent ev)
        {
            try
            {
                if (ev == null) return;
                _sieges.Remove(ev);
                _refused.Remove(ev);
                DropDeals(ev, "oblezenie zakonczone", "the siege is over");
            }
            catch (Exception e) { Stumble("OnSiegeEnded", e); }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po lupie miast): linia doby (przy pomiarze i ruchu) i porzadek pamieci. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                // porzadek: oblezenia, ktorych juz nie ma (zdarzenie konca pominiete)
                List<SiegeEvent> dead = null;
                foreach (var kv in _sieges)
                    if (kv.Key == null || kv.Value.St == null || kv.Value.St.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) { _sieges.Remove(ev); DropDeals(ev, "oblezenie nieaktualne (porzadek doby)", "the siege is over"); }
                if (_refused.Count > 0) _refused.RemoveWhere(ev => ev == null || ev.BesiegedSettlement == null || ev.BesiegedSettlement.SiegeEvent != ev);
                DailyMore();                                               // O1: rozejmy - wygasle
                var s = Settings.Current;
                if (s != null && s.LogEnabled && (MeasureOn || AiOn || PlayerDefOn || PlayerBesOn) && (_dTrues > 0 || _dAssaultEnd > 0 || _dStumbles > 0 || DayAny))
                {
                    var sb = new StringBuilder(900);
                    sb.Append("Okup oblezenia (doba): dzien ").Append(Today)
                      .Append(" | klucze: pomiar ").Append(s.SiegeRansomMeasureLog ? "tak" : "nie").Append(", AI ").Append(AiOn ? "tak" : "nie")
                      .Append(", gracz-obronca ").Append(PlayerDefOn ? "tak" : "nie").Append(", gracz-oblegajacy ").Append(PlayerBesOn ? "tak" : "nie")
                      .Append(", czesc ").Append(F(Share, "0.000")).Append(", prog krwawego szturmu ").Append(F(BloodyRatio, "0.00")).Append(", rozejm ").Append(TruceDays).Append(" dob")
                      .Append(" | decyzje gry o szturmie (true) ").Append(_dTrues).Append(", cykli ").Append(_dCycles).Append(" (linii O0 ").Append(_dLines).Append(')')
                      .Append(", obronca zaplacilby ").Append(_dDefPays).Append(", AI przyjalby ").Append(_dAiAcc).Append(", oba ").Append(_dBoth).Append(", z graczem ").Append(_dPlayerSide)
                      .Append(" | bez okupu (wykluczone): ");
                    if (_dExcl.Count == 0) sb.Append('0');
                    else { bool first = true; foreach (var kv in _dExcl) { if (!first) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); first = false; } }
                    sb.Append(" | szturmy zakonczone ").Append(_dAssaultEnd).Append(", bez zdobycia przy trwajacym oblezeniu ").Append(_dAssaultFail)
                      .Append(" | oblezenia w pamieci ").Append(_sieges.Count);
                    DayPart(sb);                                           // O1-O3: okupy, przelewy, rozejmy, gracz
                    sb.Append(" | latka decyzji: ").Append(_hookAssault).Append(" | potkniecia ").Append(_dStumbles).Append(" (od wczytania ").Append(_stumbles).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }

        private static void ZeroDay()
        {
            _dTrues = _dCycles = _dLines = _dDefPays = _dAiAcc = _dBoth = _dAssaultEnd = _dAssaultFail = _dPlayerSide = 0;
            _dExcl.Clear();
            _dStumbles = 0;
            ZeroDayMore();
        }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie).</summary>
        internal static void Reset()
        {
            _sieges.Clear();
            ZeroDay();
            _stumbles = 0; _err.Clear();
            ResetMore();
            SiegeWalls.Reset();                                            // OB1-OB4: pamiec szturmow, przerw i licznikow od zera
        }

        // ------------------------------------------------------------ latki w kampanii (Seal31.EnsureHooks, raz na proces)
        private static bool _hooksTried;

        internal static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            try
            {
                var m = AccessTools.Method(typeof(BesiegerCamp), "StartingAssaultOnBesiegedSettlementIsLogical");
                if (m == null) _hookAssault = "BRAK (BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical nie znaleziona)";
                else
                {
                    Seal31.Patch("okup oblezenia: decyzja gry o szturmie (BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical)", m, typeof(SiegeRansom), null, nameof(AssaultPost), null,
                                 Priority.Normal, Priority.Low);
                    _hookAssault = "wpieta";
                }
            }
            catch (Exception e) { _hookAssault = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks", e); }
            EnsureHooksMore();
            try { SiegeWalls.EnsureHooks(); } catch (Exception e) { Stumble("SiegeWalls.EnsureHooks", e); }   // OB1-OB3: przewaga osady, odparcie szturmu, bitwa z murow
        }
    }
}
