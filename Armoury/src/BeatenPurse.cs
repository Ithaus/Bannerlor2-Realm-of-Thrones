using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA 3.2 - CALA KIESA ROZBITEJ PARTII BEZ WODZA DLA ZWYCIEZCY (PLAN-DO-KONCA-MODA etap 3 pkt 3.2; audyt 15 Z15-3; decyzja Jeffa 09.10 ok. 04:55,
    /// Q3b: "jak rozbije karawane, to zabiera wszystko zwyciezca" - jedna regula: gracz, AI i bandyci; korona 1/9 lupu wasali; autotest pilnuje band).
    /// Wylacznik BeatenPurseToVictor.
    ///
    /// GRA (dekompilacja TaleWorlds.CampaignSystem 1.4.8): DefaultBattleRewardModel.CalculatePlunderedGoldAmountFromDefeatedParty - pokonana partia
    /// bez wodza z kiesa handlowa traci 10% kiesy (tabor wsi, karawana), banda 50% (wodz: 5% jego zlota, najwyzej 10 000 - bez zmian). MapEvent.
    /// CalculatePlunderedAndLostGoldAmounts sklada te kwoty w jedna pule i dzieli ja miedzy zwyciezcow wedlug wkladu (GetLootGoldChances: wklad > 0,
    /// bez patroli; kazdy udzial w dol do zlotowki). MapEventParty.CommitGoldChanges wyplaca: wodzowi - zdarzenie z niczego (GiveGoldAction nic -> wodz),
    /// partii bez wodza z kiesa handlowa (banda, karawana, tabor, zaloga) - do kiesy bez zdarzenia, innej (milicja, partia lorda bez wodza) - nic,
    /// udzial przepada; pokonany bez wodza traci GoldLost z kiesy bez zdarzenia. Reszta kiesy pokonanego ginela z partia (MobilePartyDestroyed - ksiega:
    /// "kiesy partii bez wodza, ktore zniknely z mapy"); sakwe taboru wsi lapala tylko 112 (VillagerPurseSurvives - cala reszta do niszczyciela).
    /// NavalDLCBattleRewardModel oddaje obie metody modelowi bazowemu (BaseModel = DefaultBattleRewardModel) - latka na Default dziala tez pod NavalDLC.
    /// Inne mody (BK, BKROTPatch, RBM, DTE, Spoils of War, BetterEconomy, ROT) tych metod nie lataja (BK i BKROTPatch: tylko slawa i wplywy).
    ///
    /// TERAZ (ta sama droga gry, bez wlasnego podzialu):
    ///  1. LUP = CALA KIESA: postfiks modelu - tabor wsi, karawana i banda (OutlawLaw.IsOutlawParty) bez wodza traca cala kiese; gra dzieli ja miedzy
    ///     zwyciezcow tak samo, jak dotad dzielila 10% / 50%.
    ///  2. NIC W NICOSC PRZY WYPLACIE: postfiks MapEvent.CalculatePlunderedAndLostGoldAmounts - zaokraglenia gry (w dol) do zwyciezcy z najwiekszym
    ///     udzialem, ktory moze go wziac; czesc z kies, ktorej gra nikomu nie wyplaci (udzial milicji i partii bez kiesy, brak zwyciezcy z wkladem;
    ///     Inni - trup monety nie bierze, jak w 112), zostaje w kiesie pokonanego (mniejszy GoldLost, w gore do zlotowki - nic z niczego).
    ///  3. NIC W NICOSC PRZY ZNISZCZENIU: rozbita karawana albo banda (niszczyciel != null) znika z mapy z ta reszta - reszta do niszczyciela
    ///     (wodz - GiveGoldAction partia -> bohater; partia z kiesa handlowa - partia -> partia), a gdy nie moze wziac - do kasy jego osady (milicja,
    ///     zaloga, patrol), inaczej do kasy najblizszego miasta (jak 112-p i sakiewki MenPurse). Tabor wsi - jak dotad 112 (VillageTakings).
    ///     Rozwiazane bez bitwy (niszczyciel null) - poza regula, tylko licznik (jak dotad znikaja).
    ///  4. KORONA 1/9 (prawo trzecich 178): czesc z kies wzieta przez wodza-wasala krolestwa w wojnie (takze gracza) idzie do podstawy 1/9
    ///     (Ransom178.NoteBeatenGold - z kiesy odbiorcy nastepnego ranka, jak reszta 1/9).
    /// Platnik -> odbiorca: kiesa rozbitej partii -> zwyciezcy wedlug wkladu / niszczyciel / jego osada / najblizsze miasto; 1/9 -> skarbiec.
    /// Ksiega pieniadza: lup wodza to zdarzenie gry z niczego (ZRODLA "bitwy"), kiesa pokonanego maleje bez zdarzenia (NETTO "bitwy - zloto partii
    /// bez wodza") - razem przelew, ksiega zamyka sie jak dotad; "kiesy partii bez wodza, ktore zniknely z mapy" (karawany, bandy) spadaja do rozwiazanych
    /// bez bitwy. Linia "Kiesy rozbitych (3.2)" (pomiar band - S16: linia "Utarg wsi (112)", awanse u pasera - "Paser").
    /// </summary>
    internal static class BeatenPurse
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BeatenPurseToVictor; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        // rodzaje pokonanych - kolejnosc linii: tabory, bandy, karawany
        private const int TVill = 0, TBand = 1, TCar = 2, T = 3;

        private static bool _modelHooked, _splitHooked, _hooksLogged;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // ------------------------------------------------------------ liczniki doby
        private static int _raisedN, _battles, _stumbles;
        private static long _raisedGold, _remAdded, _undeadCut;
        private static readonly long[] _paid = new long[T], _kept = new long[T], _gone = new long[T], _disb = new long[T];
        private static readonly int[] _paidN = new int[T], _goneN = new int[T], _disbN = new int[T];
        private static long _toLords, _toPlayer, _toBands, _toParties;                 // pkt 2: wyplata gry, rozbicie wedlug zwyciezcow
        private static long _dLords, _dPlayer, _dBands, _dParties, _dSettl, _dTown, _dLost;   // pkt 3: reszta przy zniszczeniu

        /// <summary>Ksiega pieniadza (MoneyLedger.Daily biegnie przed naszym Daily - te same granice doby): kiesy pokonanych zdjete w wyplacie gry.</summary>
        internal static long DayTaken { get { return _paid[TVill] + _paid[TBand] + _paid[TCar]; } }
        internal static long DayToLords { get { return _toLords; } }
        internal static long DayToParties { get { return _toBands + _toParties; } }

        internal static void Reset()
        {
            _hooksLogged = false; _errSites.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _raisedN = _battles = _stumbles = 0;
            _raisedGold = _remAdded = _undeadCut = 0;
            Array.Clear(_paid, 0, T); Array.Clear(_kept, 0, T); Array.Clear(_gone, 0, T); Array.Clear(_disb, 0, T);
            Array.Clear(_paidN, 0, T); Array.Clear(_goneN, 0, T); Array.Clear(_disbN, 0, T);
            _toLords = _toPlayer = _toBands = _toParties = 0;
            _dLords = _dPlayer = _dBands = _dParties = _dSettl = _dTown = _dLost = 0;
        }

        /// <summary>Wyjatek przy jednym zdarzeniu: liczony zawsze, do pliku raz na miejsce (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error(where, e); } catch { }
        }

        /// <summary>Rodzaj partii objetej regula (-1 = inna): tabor wsi, banda (jak w prawie wyrzutkow), karawana.</summary>
        private static int TypeOf(MobileParty mp)
        {
            if (mp == null) return -1;
            if (mp.IsVillager) return TVill;
            if (mp.IsCaravan) return TCar;
            if (OutlawLaw.IsOutlawParty(mp)) return TBand;
            return -1;
        }

        /// <summary>Pokonany objety pkt 1: partia bez wodza z kiesa handlowa (tak samo rozpoznaje ja gra), tabor wsi / banda / karawana.</summary>
        private static int PurseLoser(PartyBase p)
        {
            if (p == null || p.LeaderHero != null || !p.IsMobile) return -1;
            var mp = p.MobileParty;
            if (mp == null || !mp.IsPartyTradeActive) return -1;
            return TypeOf(mp);
        }

        // ------------------------------------------------------------ 1. lup = cala kiesa (model gry)
        /// <summary>Postfiks DefaultBattleRewardModel.CalculatePlunderedGoldAmountFromDefeatedParty: tabor wsi, karawana i banda bez wodza traca cala kiese.</summary>
        public static void PlunderPostfix(PartyBase __0, ref int __result)
        {
            try
            {
                if (!On || !Live || PurseLoser(__0) < 0) return;
                int g = __0.MobileParty.PartyTradeGold;
                if (g <= __result) return;
                _raisedN++; _raisedGold += g - __result;
                __result = g;
            }
            catch (Exception e) { Stumble("BeatenPurse.PlunderPostfix", e); }
        }

        // ------------------------------------------------------------ 2. nic w nicosc przy wyplacie (podzial gry)
        /// <summary>
        /// Postfiks MapEvent.CalculatePlunderedAndLostGoldAmounts (po ustawieniu GoldLost pokonanych i PlunderedGold zwyciezcow, przed wyplata
        /// CommitGoldChanges - w bitwie AI od razu, w bitwie gracza po ekranie lupu). Podzial gry zostaje; poprawiamy tylko to, co zniknieloby w wyplacie.
        /// </summary>
        public static void SplitPostfix(MapEvent __instance, MBReadOnlyList<MapEventParty> __0, MBReadOnlyList<MapEventParty> __1)
        {
            try
            {
                if (!On || !Live || __instance == null || __0 == null || __1 == null) return;
                if (__instance.IsSiegeAmbush) return;          // gra nie liczy tu lupu - kiesa zostaje, przy zniszczeniu bierze ja niszczyciel (pkt 3)
                long total = 0, purse = 0;
                foreach (var d in __1)
                {
                    if (d == null || d.GoldLost <= 0) continue;
                    total += d.GoldLost;
                    if (PurseLoser(d.Party) >= 0) purse += d.GoldLost;
                }
                if (purse <= 0 || total <= 0) return;           // bez kies objetych regula - wszystko jak w grze
                _battles++;
                double pf = (double)purse / total;              // czesc puli z kies (reszta - 5% zlota pokonanych wodzow, regula gry)

                // zaokraglenia gry (kazdy udzial w dol): do zwyciezcy z najwiekszym udzialem, ktory moze go wziac (wodz albo kiesa handlowa, nie Inni)
                long sumPg = 0; MapEventParty top = null;
                foreach (var w in __0)
                {
                    if (w == null || w.PlunderedGold <= 0) continue;
                    sumPg += w.PlunderedGold;
                    if (CanTake(w) && (top == null || w.PlunderedGold > top.PlunderedGold)) top = w;
                }
                long rem = total - sumPg;
                if (top != null && rem > 0 && sumPg > 0) { top.PlunderedGold += (int)Math.Min(int.MaxValue - top.PlunderedGold, rem); _remAdded += rem; }

                // co gra naprawde wyplaci; Inni - bez czesci z kies
                long ok = 0, okLords = 0, okPlayer = 0, okBands = 0;
                foreach (var w in __0)
                {
                    if (w == null || w.PlunderedGold <= 0) continue;
                    var p = w.Party; var mp = p != null ? p.MobileParty : null;
                    bool hero = p != null && p.LeaderHero != null;
                    bool trade = !hero && mp != null && mp.IsPartyTradeActive;
                    if (!hero && !trade) continue;              // milicja, partia lorda bez wodza: gra nic nie wyplaci - czesc z kies zostaje u pokonanych (nizej)
                    if (mp != null && Undead.Party(mp))
                    {
                        int cut = (int)Math.Min(w.PlunderedGold, Math.Round(w.PlunderedGold * pf));
                        w.PlunderedGold -= cut; _undeadCut += cut;   // trup monety nie bierze (jak 112): czesc z kies zostaje u pokonanych, lup z wodzow jak w grze
                        continue;
                    }
                    ok += w.PlunderedGold;
                    if (hero) { okLords += w.PlunderedGold; if (p.LeaderHero == Hero.MainHero) okPlayer += w.PlunderedGold; }
                    else if (OutlawLaw.IsOutlawParty(mp)) okBands += w.PlunderedGold;
                }
                double fd = Math.Min(1.0, (double)ok / total);   // czesc puli wyplacona zwyciezcom

                // pokonani z kiesa: placa tylko te czesc, ktora gra wyplaci (w gore - zwyciezcy dostaja nie wiecej, niz pokonani daja)
                long paidAll = 0;
                foreach (var d in __1)
                {
                    if (d == null || d.GoldLost <= 0) continue;
                    int t = PurseLoser(d.Party);
                    if (t < 0) continue;
                    int lost = d.GoldLost;
                    int pay = fd >= 1.0 ? lost : (int)Math.Min(lost, Math.Ceiling(lost * fd));
                    d.GoldLost = pay;
                    _paid[t] += pay; if (pay > 0) _paidN[t]++;
                    _kept[t] += lost - pay;
                    paidAll += pay;
                }
                if (paidAll <= 0 || ok <= 0) return;

                // rozbicie zaplaconego wedlug zwyciezcow (pula wspolna z lupem z wodzow - wedlug udzialow) i 1/9 korony od wodzow-wasali
                double q = (double)paidAll / ok;               // czesc kazdego udzialu, ktora pochodzi z kies (= pf przy fd < 1)
                _toLords += (long)Math.Round(okLords * q); _toPlayer += (long)Math.Round(okPlayer * q); _toBands += (long)Math.Round(okBands * q);
                _toParties += (long)Math.Round((ok - okLords - okBands) * q);
                foreach (var w in __0)
                {
                    if (w == null || w.PlunderedGold <= 0) continue;
                    var h = w.Party != null ? w.Party.LeaderHero : null;
                    var mp = w.Party != null ? w.Party.MobileParty : null;
                    if (h == null || !h.IsAlive || (mp != null && Undead.Party(mp))) continue;
                    Ransom178.NoteBeatenGold(h, (long)Math.Round(w.PlunderedGold * q));
                }
            }
            catch (Exception e) { Stumble("BeatenPurse.SplitPostfix", e); }
        }

        /// <summary>Czy gra wyplaci temu zwyciezcy udzial (CommitGoldChanges) i czy wolno mu wziac kiese: wodz albo partia z kiesa handlowa, nie Inni.</summary>
        private static bool CanTake(MapEventParty w)
        {
            var p = w != null ? w.Party : null;
            if (p == null) return false;
            var mp = p.MobileParty;
            if (mp != null && Undead.Party(mp)) return false;
            return p.LeaderHero != null || (mp != null && mp.IsPartyTradeActive);
        }

        // ------------------------------------------------------------ 3. nic w nicosc przy zniszczeniu (karawana, banda)
        /// <summary>
        /// MobilePartyDestroyed: rozbita karawana albo banda znika z mapy z tym, czego gra w bitwie nikomu nie wyplacila (pkt 2), albo z cala kiesa,
        /// gdy gra lupu nie liczyla (zasadzka przy oblezeniu) albo karawana miala wodza. Dopisany PO VillageTakings (biegnie przed nim i przed
        /// OutlawLaw - licznik "kiesy band, ktore dzis zniknely" - oraz przed ksiega obiegu, dopisana pierwsza: B-1 w ArmouryBehavior.RegisterEvents).
        /// </summary>
        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                if (party == null || !On || !Live) return;
                if (party.IsVillager || (party.IsLordParty && party.LeaderHero != null)) return;   // tabor wsi - 112; kiesa wodza zostaje z wodzem
                int t = TypeOf(party);
                if (t < 0) return;
                int g = party.PartyTradeGold;
                if (g <= 0) return;
                if (destroyer == null) { _disb[t] += g; _disbN[t]++; return; }   // rozwiazana bez bitwy - poza regula (jak dotad znika; licznik)
                _gone[t] += g; _goneN[t]++;
                var win = destroyer.MobileParty;
                if (win != null && !ReferenceEquals(win, party) && win.IsActive && !Undead.Party(win))
                {
                    var hero = win.LeaderHero;
                    if (hero != null && hero.IsAlive)
                    {
                        GiveGoldAction.ApplyForPartyToCharacter(party.Party, hero, g, true);
                        int moved = g - party.PartyTradeGold;
                        _dLords += moved; if (hero == Hero.MainHero) _dPlayer += moved;
                        Ransom178.NoteBeatenGold(hero, moved);   // 4: korona 1/9 od wodza-wasala
                    }
                    else if (hero == null && win.IsPartyTradeActive)
                    {
                        GiveGoldAction.ApplyForPartyToParty(party.Party, win.Party, g, true);
                        int moved = g - party.PartyTradeGold;
                        if (OutlawLaw.IsOutlawParty(win)) _dBands += moved; else _dParties += moved;
                    }
                    g = party.PartyTradeGold;
                    if (g <= 0) return;
                }
                // niszczyciel nie moze wziac (milicja, patrol, Inni, partia lorda bez wodza): kasa jego osady, inaczej najblizszego miasta
                var own = SettlementOf(destroyer);
                var st = own ?? VillageTakings.NearestTown(party);
                if (st == null || st.SettlementComponent == null) { _dLost += g; return; }
                GiveGoldAction.ApplyForPartyToSettlement(party.Party, st, g, true);   // zdarzenie gry: ksiega widzi przelew partia -> osada
                int m = g - party.PartyTradeGold;
                if (own != null) _dSettl += m; else _dTown += m;
                _dLost += party.PartyTradeGold;
            }
            catch (Exception e) { Stumble("BeatenPurse.OnPartyDestroyed", e); }
        }

        /// <summary>Osada zwyciezcy, ktory nie nosi kiesy: osada-niszczyciel albo osada milicji, zalogi lub patrolu (miasto, zamek, wies).</summary>
        private static Settlement SettlementOf(PartyBase destroyer)
        {
            Settlement st = null;
            if (destroyer == null) return null;
            if (destroyer.IsSettlement) st = destroyer.Settlement;
            else
            {
                var mp = destroyer.MobileParty;
                if (mp != null && (mp.IsMilitia || mp.IsGarrison || mp.IsPatrolParty)) st = mp.CurrentSettlement ?? mp.HomeSettlement;
            }
            return st != null && (st.IsTown || st.IsCastle || st.IsVillage) ? st : null;
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            try
            {
                var m = AccessTools.Method(typeof(DefaultBattleRewardModel), "CalculatePlunderedGoldAmountFromDefeatedParty", new[] { typeof(PartyBase) });
                if (m != null && m.ReturnType == typeof(int))
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(BeatenPurse), nameof(PlunderPostfix)) { priority = Priority.Last });
                    _modelHooked = true; done.Add("lup z kiesy pokonanego (model gry)");
                }
                else miss.Add("lup z kiesy pokonanego (brak DefaultBattleRewardModel.CalculatePlunderedGoldAmountFromDefeatedParty)");
            }
            catch (Exception e) { miss.Add("lup z kiesy pokonanego (" + e.Message + ")"); Log.Error("BeatenPurse.ApplyAll(model)", e); }
            try
            {
                var c = AccessTools.Method(typeof(MapEvent), "CalculatePlunderedAndLostGoldAmounts");
                var ps = c != null ? c.GetParameters() : null;
                if (c != null && !c.IsStatic && ps.Length == 2)
                {
                    h.Patch(c, postfix: new HarmonyMethod(typeof(BeatenPurse), nameof(SplitPostfix)));
                    _splitHooked = true; done.Add("podzial lupu zlota (MapEvent)");
                }
                else miss.Add("podzial lupu zlota (brak MapEvent.CalculatePlunderedAndLostGoldAmounts(winners, defeated))");
            }
            catch (Exception e) { miss.Add("podzial lupu zlota (" + e.Message + ")"); Log.Error("BeatenPurse.ApplyAll(podzial)", e); }
            var s = Settings.Current;
            Log.Info("BeatenPurse (3.2): cala kiesa rozbitej partii bez wodza dla zwyciezcy - latki wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "zadne")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "") + "; reszta przy zniszczeniu karawany i bandy - z nasluchu zdarzen. Wylacznik Beaten Purse To Victor "
                     + (s != null && s.BeatenPurseToVictor ? "WLACZONY" : "wylaczony") + ".");
        }

        /// <summary>Raz na sesje (pierwsza doba): czynny model lupu i jego modele bazowe - latka na Default dziala tylko, gdy lancuch do niego dochodzi.</summary>
        private static string HooksLine()
        {
            try
            {
                var names = new List<string>();
                object m = Campaign.Current.Models.BattleRewardModel;
                for (int i = 0; i < 6 && m != null; i++)
                {
                    names.Add(m.GetType().FullName);
                    PropertyInfo bp = null;
                    try { bp = AccessTools.Property(m.GetType(), "BaseModel"); } catch { bp = null; }
                    object next = null;
                    try { next = bp != null ? bp.GetValue(m, null) : null; } catch { next = null; }
                    if (ReferenceEquals(next, m)) break;
                    m = next;
                }
                bool def = false;
                foreach (var n in names) if (n == typeof(DefaultBattleRewardModel).FullName) def = true;
                return "Kiesy rozbitych (3.2) - model lupu: " + (names.Count > 0 ? string.Join(" -> ", names.ToArray()) : "brak")
                       + (def ? " (dochodzi do DefaultBattleRewardModel - latka modelu dziala)" : " (NIE dochodzi do DefaultBattleRewardModel - lup z kies jak w grze)")
                       + "; latki: model " + (_modelHooked ? "tak" : "NIE") + ", podzial " + (_splitHooked ? "tak" : "NIE") + ".";
            }
            catch (Exception e) { return "Kiesy rozbitych (3.2) - model lupu: blad odczytu (" + e.Message + ")."; }
        }

        // ------------------------------------------------------------ raz na dobe
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                if (!_hooksLogged) { _hooksLogged = true; Log.Info(HooksLine()); }
                int day = (int)CampaignTime.Now.ToDays - 1;
                long paid = DayTaken, kept = _kept[TVill] + _kept[TBand] + _kept[TCar];
                var sb = new StringBuilder(1024);
                sb.Append("Kiesy rozbitych (3.2): dzien ").Append(day);
                if (!On) sb.Append(" | WYLACZONE (Beaten Purse To Victor) - lup z kies jak w grze (10% taboru i karawany, 50% bandy), reszta znika z partia");
                sb.Append(" | zloto rozbitych do zwyciezcow: tabory ").Append(_paid[TVill]).Append(", bandy ").Append(_paid[TBand]).Append(", karawany ").Append(_paid[TCar])
                  .Append("; z tego do band ").Append(_toBands)
                  .Append(" (razem ").Append(paid).Append(" z ").Append(_paidN[TVill]).Append("/").Append(_paidN[TBand]).Append("/").Append(_paidN[TCar]).Append(" partii w ").Append(_battles)
                  .Append(" bitwach; do wodzow ").Append(_toLords).Append(_toPlayer > 0 ? " (w tym gracz " + _toPlayer + ")" : "").Append(", do kies band ").Append(_toBands)
                  .Append(", do kies innych partii bez wodza ").Append(_toParties).Append("; model gry podniosl lup ").Append(_raisedN).Append(" razy o ").Append(_raisedGold)
                  .Append(", zaokraglenia gry dopisane zwyciezcom ").Append(_remAdded).Append(")")
                  .Append(" | w bitwie bez zwyciezcy, ktory moze wziac (milicja, patrol, Inni), zostalo w kiesach pokonanych ").Append(kept)
                  .Append(" (tabory ").Append(_kept[TVill]).Append(", bandy ").Append(_kept[TBand]).Append(", karawany ").Append(_kept[TCar]).Append("; Innym nie wyplacono ").Append(_undeadCut).Append(")")
                  .Append(" | reszta przy zniszczeniu rozbitych (karawany i bandy; tabory - 112): bandy ").Append(_gone[TBand]).Append(" (").Append(_goneN[TBand])
                  .Append("), karawany ").Append(_gone[TCar]).Append(" (").Append(_goneN[TCar]).Append(") -> wodzom ").Append(_dLords).Append(_dPlayer > 0 ? " (w tym gracz " + _dPlayer + ")" : "")
                  .Append(", bandom ").Append(_dBands).Append(", innym partiom ").Append(_dParties).Append(", kasie osady zwyciezcy ").Append(_dSettl)
                  .Append(", najblizszemu miastu ").Append(_dTown).Append(", zniklo ").Append(_dLost)
                  .Append(" | rozwiazane bez bitwy (poza regula - jak dotad w \"kiesy partii bez wodza, ktore zniknely\"): bandy ").Append(_disb[TBand]).Append(" (").Append(_disbN[TBand])
                  .Append("), karawany ").Append(_disb[TCar]).Append(" (").Append(_disbN[TCar]).Append(")")
                  .Append(" | ksiega: kiesy pokonanych zdjete bez zdarzenia ").Append(paid).Append(" (NETTO \"bitwy - zloto partii bez wodza\"), z tego wodzom zdarzeniem gry z niczego ")
                  .Append(_toLords).Append(" (ZRODLA \"bitwy\") i kiesom partii bez wodza ").Append(_toBands + _toParties).Append(" (NETTO) - przelew, nie zrodlo");
                if (_stumbles > 0) sb.Append(" | wyjatki zlapane ").Append(_stumbles).Append(" (pierwszy z kazdego miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("BeatenPurse.Daily", e); }
            finally { ClearDay(); }
        }
    }
}
