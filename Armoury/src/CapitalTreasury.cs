using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// L5b-1 - SKARBIEC KORONY PRZY ZDOBYCIU STOLICY (decyzja Jeffa 13.10 "5 tak, i tak": docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md rozdz. 8 pyt. 5b
    /// i lista "na slowo" rozdz. 6). Wylacznik CapitalTreasurySeized (wylaczony = stan a913a58 co do zlotowki; zostaje linia pomiaru "w cieniu"), czesc CapitalTreasuryShare.
    ///
    /// CO JEST STOLICA (sprawdzone w dekompilacji): BannerKings trzyma stolice krolestwa w BKCapitalBehavior (slownik krolestwo -> Town, zapis "bannerkings-capitals";
    /// DailyTick: krolestwo bez stolicy albo ze stolica utracona dostaje nowa - BK GetIdealCapital: losowo z miast rodu wladcy, bez nich losowe lenno), z niej korzysta dwor BK
    /// (BKCourtierBehavior), ekonomia BK (BKEconomyModel, BKGrowthModel) i komunikat gracza "X is now the capital of Y". To jest stolica, ktora gracz widzi i w ktorej
    /// siedzi dwor - wiec w niej lezy skarbiec. Bez BK (albo przed pierwsza doba BK, nowe krolestwo bez wpisu) - Kingdom.InitialHomeSettlement (XML ROT: 29 krolestw ma
    /// initial_home_settlement, np. battania town_B2, vlandia town_EW4 King's Landing). Odrzucone: FactionMidSettlement (srodek geometryczny, nie siedziba) i sama
    /// InitialHomeSettlement (po utracie krolestwo nie mialoby stolicy nigdy wiecej). Stolica BK zmienia sie dopiero w DailyTick BK - w chwili zdarzenia slownik BK
    /// pokazuje jeszcze te, ktora wlasnie pada.
    ///
    /// KIEDY: zmiana pana twierdzy ze szczegolem BySiege (gra wola ApplyBySiege tylko z KingdomManager.SiegeCompleted - szturm, przegrany wypad, wypad z blokady;
    /// ROT i BK uzywaja ApplyByDefault / ByKingDecision, nigdy BySiege) - pokoj, dar, decyzja krola, bunt, upadek rodu NIE. Wyjatek blizniakow ROT
    /// (ROTOthersCampaignBehavior.OnSettlementOwnerChanged: Red Keep <-> King's Landing, Mur <-> Castle Black - ApplyByDefault wewnatrz zdarzenia BySiege): zmiana
    /// ApplyByDefault w oknie KingdomManager.SiegeCompleted (prefiks / finalizer tej latki) na rzecz tej samej frakcji co zdobywca liczy sie jako zdobycie ("blizniak").
    /// Przegrany = krolestwo rodu starego pana (oldOwner.Clan.Kingdom); osada musi byc jego stolica.
    ///
    /// ILE: nadwyzka = skarbiec - zapas wojny (KingdomTreasury.WarReserveFor - ta sama liczba co W5b i 184: wieksze z Crown Reserve Gold i War Tax Reserve Days
    /// zwrotu zoldu) - dlug korony w Banku (186, CrownBorrow.DebtOf: zloto winne Bankowi nie jest skarbem korony). Zabrane = floor(CapitalTreasuryShare x nadwyzka),
    /// 0 przy nadwyzce < 1. Skarbiec przegranego nigdy nie schodzi ponizej zapasu wojny i dlugu (wojsko przegranego dalej ma z czego dostac zwrot zoldu - prog 85 tys.).
    ///
    /// KOMU: skarbiec krolestwa zdobywcy (newOwner.MapFaction). Zdobywca bez krolestwa (rod niezalezny, takze gracz) - kiesa glowy rodu (ksiega rodu: jednorazowy
    /// wplyw od prawdziwego platnika, KRansom). Inni (White Walkers) - nic: trup monety nie bierze (ta sama zasada co CasualtyLoot / BeatenPurse), skarbiec zostaje.
    ///
    /// OBIEG ZAMKNIETY: skarbiec -> skarbiec (albo -> kiesa glowy rodu) w jednej chwili, zdjete = dopisane (kontrola w linii). Migawka korony 165 przesunieta o te kwote
    /// u obu stron (CrownIncome.ShiftSnapshot, jak GL-2): to nie jest "wplyw dnia" zdobywcy ani "ujemny wplyw" przegranego - zmienia zapas, z ktorego 165 oddaje
    /// 1/360 na dobe, a W5b - 1/30 nadwyzki w potrzebie. "Pieniadz swiata" bez zmian (oba skarbce w swiecie). Linia 165 i "Obieg" (korona) maja pozycje L5b-1.
    ///
    /// Linie: "Skarbiec stolicy (L5b-1): dzien" przy kazdym upadku stolicy; "Skarbce stolic (L5b-1): dzien" raz na dobe, tylko w dobie ze zdobyciem twierdzy albo
    /// potknieciem. Stan tylko w pamieci (liczniki doby) - nic w zapisie. Wyjatek per zdarzenie = licznik potkniec (CLAUDE.md 7), bez globalnego wylacznika.
    /// </summary>
    internal static class CapitalTreasury
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CapitalTreasurySeized; } }

        // okno KingdomManager.SiegeCompleted ze zdobyciem: glebokosc, frakcja zdobywcy, osada szturmu, frakcja osady przed zmiana pana
        private static int _depth;
        private static IFaction _winF, _oldF;
        private static Settlement _siegeSt;

        // liczniki doby (linia doby i Last* dla 165 / "Obieg")
        private static int _dForts, _dSiege, _dTwin, _dFalls, _dFallsBk, _dFallsHome, _dZero, _dUndead, _dShadowN, _dToHeadsN;
        private static long _dTaken, _dToCrowns, _dToHeads, _dShadow, _dDiff;
        internal static long LastTaken, LastToCrowns, LastToHeads;
        internal static int LastFalls;
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static string _hook = "nie zakladana";
        private static bool _hookTried;

        // BK: BKCapitalBehavior.GetCapital(Kingdom) przez refleksje (zachowanie kampanii - pamiec na kampanie)
        private static bool _bkTried;
        private static Type _bkType;
        private static MethodInfo _bkGet, _bkBehGet;
        private static object _bkBeh;
        private static Campaign _bkFor;

        internal static void Reset()
        {
            _depth = 0; _winF = null; _oldF = null; _siegeSt = null;
            ZeroDay();
            LastTaken = LastToCrowns = LastToHeads = 0; LastFalls = 0;
            _stumbles = 0; _err.Clear();
            _bkBeh = null; _bkFor = null;
        }

        private static void ZeroDay()
        {
            _dForts = _dSiege = _dTwin = _dFalls = _dFallsBk = _dFallsHome = _dZero = _dUndead = _dShadowN = _dToHeadsN = 0;
            _dTaken = _dToCrowns = _dToHeads = _dShadow = _dDiff = 0;
            _dStumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("CapitalTreasury." + where, e); } catch { }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ latka: okno zdobycia (KingdomManager.SiegeCompleted)
        /// <summary>Seal31.EnsureHooks (w kampanii, raz na proces): prefiks i finalizer na KingdomManager.SiegeCompleted - okno, w ktorym zmiana pana blizniaka ROT
        /// (ApplyByDefault wewnatrz zdarzenia BySiege) liczy sie jako zdobycie.</summary>
        internal static void EnsureHooks()
        {
            if (_hookTried) return;
            _hookTried = true;
            try
            {
                var m = AccessTools.Method(typeof(KingdomManager), "SiegeCompleted");
                if (m == null) _hook = "BRAK (KingdomManager.SiegeCompleted nie znaleziona)";
                else
                {
                    Seal31.Patch("L5b-1: okno zdobycia dla blizniakow ROT (KingdomManager.SiegeCompleted)", m, typeof(CapitalTreasury), nameof(SiegePre), null, nameof(SiegeFin));
                    _hook = "wpieta (KingdomManager.SiegeCompleted)";
                }
            }
            catch (Exception e) { _hook = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks", e); }
        }

        /// <summary>Prefiks KingdomManager.SiegeCompleted: ten sam warunek zdobycia co gra (szturm, wypad, wypad z blokady i wygrana) - otwiera okno.
        /// __state = okno otwarte przez to wywolanie (finalizer zamyka tylko swoje).</summary>
        public static void SiegePre(Settlement settlement, MobileParty capturerParty, bool isWin, MapEvent.BattleTypes battleType, out bool __state)
        {
            __state = false;
            try
            {
                bool capture = isWin && (battleType == MapEvent.BattleTypes.Siege || battleType == MapEvent.BattleTypes.SallyOut
                                         || battleType == MapEvent.BattleTypes.BlockadeSallyOutBattle);
                if (!capture || settlement == null || capturerParty == null) return;
                _depth++; __state = true;
                _winF = capturerParty.MapFaction;
                _siegeSt = settlement;
                _oldF = settlement.MapFaction;
            }
            catch (Exception e) { Stumble("SiegePre", e); }
        }

        /// <summary>Finalizer KingdomManager.SiegeCompleted: zamyka okno otwarte przez prefiks (biegnie zawsze, takze po wyjatku gry - wyjatek idzie dalej bez zmian).</summary>
        public static Exception SiegeFin(Exception __exception, bool __state)
        {
            try
            {
                if (__state && _depth > 0) _depth--;
                if (_depth == 0) { _winF = null; _oldF = null; _siegeSt = null; }
            }
            catch { }
            return __exception;
        }

        /// <summary>A14 / G14 (drobne2, FallenCrown): w oknie KingdomManager.SiegeCompleted ze zdobyciem twierdzy krolestwa `k` - frakcja zdobywcy (krolestwo upada
        /// razem z ta twierdza); poza oknem albo inna frakcja osady - null. Tylko odczyt.</summary>
        internal static IFaction SiegeWinnerOf(Kingdom k)
        {
            return _depth > 0 && k != null && ReferenceEquals(_oldF, k) ? _winF : null;
        }

        // ------------------------------------------------------------ stolica krolestwa
        /// <summary>Stolica krolestwa: BK (BKCapitalBehavior.GetCapital) albo - bez BK lub bez wpisu BK - Kingdom.InitialHomeSettlement. src: "BK" / "siedziba" / "-".</summary>
        private static Settlement CapitalOf(Kingdom k, out string src)
        {
            src = "-";
            if (k == null) return null;
            try
            {
                if (!_bkTried)
                {
                    _bkTried = true;
                    _bkType = QuartermasterLaw.FindType("BannerKings.Behaviours.BKCapitalBehavior");
                    _bkGet = _bkType != null ? AccessTools.Method(_bkType, "GetCapital", new[] { typeof(Kingdom) }) : null;
                    var gm = typeof(Campaign).GetMethod("GetCampaignBehavior", Type.EmptyTypes);
                    _bkBehGet = gm != null && _bkType != null ? gm.MakeGenericMethod(_bkType) : null;
                }
                if (_bkGet != null && _bkBehGet != null && Campaign.Current != null)
                {
                    if (!ReferenceEquals(_bkFor, Campaign.Current)) { _bkBeh = _bkBehGet.Invoke(Campaign.Current, null); _bkFor = Campaign.Current; }
                    if (_bkBeh != null)
                    {
                        var town = _bkGet.Invoke(_bkBeh, new object[] { k }) as Town;
                        if (town != null && town.Settlement != null) { src = "BK"; return town.Settlement; }
                    }
                }
            }
            catch (Exception e) { Stumble("CapitalOf(BK)", e); }
            try
            {
                var home = k.InitialHomeSettlement;
                if (home != null && home.IsFortification) { src = "siedziba"; return home; }
            }
            catch (Exception e) { Stumble("CapitalOf(siedziba)", e); }
            return null;
        }

        /// <summary>Okup zamiast szturmu (SiegeRansom, P7): osada jest TERAZ stolica krolestwa swojego pana (ta sama stolica co L5b-1: BK, bez BK poczatkowa siedziba).
        /// Tylko odczyt; wyjatek = false (licznik potkniec L5b-1).</summary>
        internal static bool IsCapital(Settlement st)
        {
            try
            {
                var k = st != null ? st.MapFaction as Kingdom : null;
                if (k == null || k.IsEliminated) return false;
                string src;
                var cap = CapitalOf(k, out src);
                return cap != null && ReferenceEquals(cap, st);
            }
            catch (Exception e) { Stumble("IsCapital", e); return false; }
        }

        // ------------------------------------------------------------ zdarzenie: zmiana pana
        /// <summary>Nasluch OnSettlementOwnerChangedEvent (ArmouryBehavior.RegisterEvents, po KingdomBalance). Nie rzuca.</summary>
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer,
                                            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (st == null || !st.IsFortification || newOwner == null) return;
                bool siege = detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege;
                bool twin = !siege && _depth > 0 && detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.Default
                            && _winF != null && ReferenceEquals(newOwner.MapFaction, _winF) && !ReferenceEquals(st, _siegeSt);
                if (!siege && !twin) return;
                _dForts++; if (siege) _dSiege++; else _dTwin++;
                Kingdom loser = oldOwner != null && oldOwner.Clan != null ? oldOwner.Clan.Kingdom : null;
                if (loser == null && siege && ReferenceEquals(st, _siegeSt)) loser = _oldF as Kingdom;   // stary pan bez glowy rodu - frakcja osady sprzed zmiany (prefiks)
                if (loser == null || loser.IsEliminated) return;
                IFaction wf = newOwner.MapFaction;
                if (wf == null || ReferenceEquals(wf, loser)) return;
                string src;
                var cap = CapitalOf(loser, out src);
                if (cap == null || !ReferenceEquals(cap, st)) return;
                Fall(st, loser, newOwner, wf, src, twin);
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        /// <summary>Upadek stolicy: rachunek, przelew (przy kluczu), linia i wiadomosc gracza.</summary>
        private static void Fall(Settlement st, Kingdom loser, Hero newOwner, IFaction wf, string src, bool twin)
        {
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays;
            _dFalls++; if (src == "BK") _dFallsBk++; else _dFallsHome++;
            long w0 = loser.KingdomBudgetWallet;
            long reserve = KingdomTreasury.WarReserveFor(loser);
            long debt = (long)Math.Ceiling(CrownBorrow.DebtOf(loser));
            if (reserve < 0) { Stumble("Fall(zapas wojny)", new InvalidOperationException("zapas wojny nieczytelny - " + loser.StringId)); return; }   // bez zapasu nic nie bierzemy
            long surplus = w0 - reserve - debt - TownFolk.CrownTownDebt(loser);   // korona2: zloto winne miastom tez nie jest skarbem korony (klucz wylaczony - 0)
            float share = s != null ? Math.Max(0f, Math.Min(1f, s.CapitalTreasuryShare)) : 0f;
            long take = surplus >= 1 ? (long)Math.Floor((double)share * surplus) : 0;          // double: float x long tracilby zlotowki przy duzych skarbcach
            take = Math.Max(0, Math.Min(take, w0));
            var wk = wf as Kingdom;
            bool undead = false;
            try { undead = newOwner.Clan != null && ClanIncomeBook.IsUndeadClan(newOwner.Clan); } catch { undead = false; }
            Hero head = wk == null ? (newOwner.Clan != null && newOwner.Clan.Leader != null && newOwner.Clan.Leader.IsAlive ? newOwner.Clan.Leader : newOwner) : null;
            string kind = undead ? "Inni - trup monety nie bierze" : (wk != null ? "skarbiec krolestwa " + Nm(wk) : "kiesa glowy rodu bez krolestwa " + (head != null ? head.Name.ToString() : "-"));
            long v0 = wk != null ? wk.KingdomBudgetWallet : (head != null ? head.Gold : 0);
            long moved = 0, got = 0;
            string mode;
            if (undead) { _dUndead++; mode = "nic (Inni)"; take = 0; }
            else if (take <= 0) { _dZero++; mode = "nic (brak nadwyzki ponad zapas wojny i dlug Banku)"; }
            else if (!On) { _dShadowN++; _dShadow += take; mode = "W CIENIU (klucz wylaczony) - wzieloby " + take; take = 0; }
            else
            {
                int it = (int)Math.Min(int.MaxValue, take);
                loser.KingdomBudgetWallet -= it;                               // najpierw przegrany, potem zdobywca: zdobywca dostaje dokladnie to, co zeszlo
                moved = w0 - loser.KingdomBudgetWallet;
                if (wk != null)
                {
                    wk.KingdomBudgetWallet += (int)moved;
                    got = (long)wk.KingdomBudgetWallet - v0;
                    CrownIncome.ShiftSnapshot(wk, got);                          // 165: skarb przejety, nie wplyw dnia zdobywcy
                    _dToCrowns += got;
                }
                else if (head != null)
                {
                    head.ChangeHeroGold((int)moved);
                    got = (long)head.Gold - v0;
                    CirculationWindows.NoteHeroGold(head, got);                  // glowa poza swiatem - licznik ksiegi (jak dary koron)
                    if (head != Hero.MainHero) ClanIncomeBook.NoteInflow(head, (int)got, ClanIncomeBook.KRansom);   // jednorazowy wplyw od prawdziwego platnika
                    _dToHeads += got; _dToHeadsN++;
                }
                else { loser.KingdomBudgetWallet += (int)moved; moved = 0; }     // nie ma komu dac - zloto wraca (nic nie znika)
                CrownIncome.ShiftSnapshot(loser, -moved);                        // 165: ubytek skarbu, nie "ujemny wplyw" przegranego
                _dTaken += moved; _dDiff += moved - got;
                mode = "przelew";
            }
            long w1 = loser.KingdomBudgetWallet;
            long v1 = wk != null ? wk.KingdomBudgetWallet : (head != null ? head.Gold : 0);
            bool pLose = Clan.PlayerClan != null && ReferenceEquals(Clan.PlayerClan.Kingdom, loser);
            bool pWin = Clan.PlayerClan != null && (wk != null ? ReferenceEquals(Clan.PlayerClan.Kingdom, wk) : (newOwner.Clan == Clan.PlayerClan));
            var sb = new StringBuilder(700);
            sb.Append("Skarbiec stolicy (L5b-1): dzien ").Append(day)
              .Append(" | stolica ").Append(Nm(st)).Append(" (").Append(st.StringId).Append(") krolestwa ").Append(Nm(loser)).Append(" (").Append(loser.StringId).Append(", wedlug ")
              .Append(src == "BK" ? "BK" : "poczatkowej siedziby").Append(')')
              .Append(" | zdobycie ").Append(twin ? "blizniak" : "szturm").Append(", zdobywca ").Append(Nm(wf)).Append(" (").Append(kind).Append(')')
              .Append(" | skarbiec ").Append(w0).Append(", zapas wojny ").Append(reserve).Append(", dlug Banku ").Append(debt).Append(", nadwyzka ").Append(surplus)
              .Append(" | czesc ").Append(((double)share).ToString("0.000000", Inv)).Append(" -> zabrane ").Append(moved).Append(" (").Append(mode).Append(')')
              .Append(" | skarbiec przegranego ").Append(w0).Append(" -> ").Append(w1).Append(", odbiorca ").Append(v0).Append(" -> ").Append(v1)
              .Append(" | zdjete ").Append(moved).Append(" - dopisane ").Append(got).Append(" = ").Append(moved - got)
              .Append(" | gracz ").Append(pLose ? "przegrany" : (pWin ? "zdobywca" : "-")).Append('.');
            Log.Info(sb.ToString());
            if (moved <= 0) return;
            try
            {
                if (pLose)
                    Log.Player(Nm(st) + " has fallen to " + Nm(wf) + ": the enemy has carried off " + moved.ToString("N0", Inv) + " denars from the royal treasury of " + Nm(loser) + ".", true);
                else if (pWin && wk != null)
                    Log.Player("With " + Nm(st) + " taken, " + moved.ToString("N0", Inv) + " denars from the royal treasury of " + Nm(loser) + " pass to the treasury of " + Nm(wk) + ".");
                else if (pWin)
                    Log.Player("With " + Nm(st) + " taken, " + moved.ToString("N0", Inv) + " denars from the royal treasury of " + Nm(loser) + " are now in your purse.");
            }
            catch (Exception e) { Stumble("Fall(wiadomosc)", e); }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Raz na dobe, PRZED CrownIncome.End (linia 165 czyta Last*), w bloku korony: liczby doby do Last* i linia doby (tylko w dobie ze zdobyciem twierdzy
        /// albo potknieciem). Nie rusza zlota.</summary>
        internal static void DayRoll()
        {
            try
            {
                LastTaken = _dTaken; LastToCrowns = _dToCrowns; LastToHeads = _dToHeads; LastFalls = _dFalls;
                var s = Settings.Current;
                if (s != null && s.LogEnabled && (_dForts > 0 || _dStumbles > 0))
                {
                    var sb = new StringBuilder(600);
                    sb.Append("Skarbce stolic (L5b-1): dzien ").Append((int)CampaignTime.Now.ToDays)
                      .Append(" | klucz Capital Treasury Seized ").Append(On ? "tak" : "nie").Append(", czesc ").Append(Math.Max(0f, Math.Min(1f, s.CapitalTreasuryShare)).ToString("0.000", Inv))
                      .Append(" | zdobycia twierdz ").Append(_dForts).Append(" (szturm ").Append(_dSiege).Append(", blizniak ROT ").Append(_dTwin).Append(')')
                      .Append(" | upadki stolic ").Append(_dFalls).Append(" (wedlug BK ").Append(_dFallsBk).Append(", wedlug poczatkowej siedziby ").Append(_dFallsHome)
                      .Append("; bez nadwyzki ").Append(_dZero).Append(", Inni ").Append(_dUndead).Append(')')
                      .Append(" | zabrane ").Append(_dTaken).Append(": do skarbcow koron ").Append(_dToCrowns).Append(", do kies glow rodow bez krolestwa ").Append(_dToHeads)
                      .Append(" (").Append(_dToHeadsN).Append(')')
                      .Append(" | w cieniu ").Append(_dShadowN).Append(" na ").Append(_dShadow)
                      .Append(" | kontrola: zdjete - dopisane = ").Append(_dDiff)
                      .Append(" | latka okna: ").Append(_hook)
                      .Append(" | potkniecia ").Append(_dStumbles).Append(" (od wczytania ").Append(_stumbles).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("DayRoll", e); }
            finally { ZeroDay(); }
        }
    }
}
