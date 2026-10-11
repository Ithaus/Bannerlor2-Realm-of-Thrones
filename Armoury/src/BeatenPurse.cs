using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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
    ///     Recenzja 3.2 (3): niszczyciel nie zawsze jest zwyciezca - gra niszczy tez partie ZWYCIESKIEJ strony bez ludzi (niszczyciel = przywodca
    ///     przegranych, MapEventSide.HandleMapEventEndForPartyInternal), a DestroyPartyAction z niszczycielem bywa bez bitwy (banda przylacza sie do
    ///     gracza, upadek rodu - DestroyClanAction, zadania). Dlatego pkt 3 tylko dla partii POKONANYCH w bitwie: SplitPostfix zapamietuje pokonanych
    ///     (karawany i bandy, takze z bohaterem na czele, takze zasadzka przy oblezeniu), a nasluch zniszczenia przyjmuje tylko zapamietanego, gdy
    ///     niszczy go przywodca strony zwyciezcow tej samej bitwy przy jej koncu. Reszta (niszczyciel nie pokonal ich w bitwie) - poza regula jak
    ///     rozwiazane (znika jak przed 3.2, osobny licznik; przylaczenie band do gracza - D11-2 nizej).
    ///  4. KORONA 1/9 (prawo trzecich 178): czesc z kies wzieta przez wodza-wasala krolestwa w wojnie (takze gracza) idzie do podstawy 1/9
    ///     (Ransom178.NoteBeatenGold - z kiesy odbiorcy nastepnego ranka, jak reszta 1/9).
    ///  5. PODDANIE GRACZOWI BEZ BITWY (recenzja 3.2 (1)): karawana albo tabor wsi oddaje graczowi kiese w rozmowie (poddanie i odjazd, wziecie
    ///     do niewoli, okup za przepuszczenie) - gra: GiveGoldAction partia -> gracz (CaravansCampaignBehavior / VillagerCampaignBehavior,
    ///     conversation_*_surrender_leave / took_prisoner / looted_leave). Prefiks/postfiks tych metod mierzy spadek kiesy: osobny licznik w linii
    ///     3.2 i 1/9 korony od gracza-wasala (jedna regula z bitwa). Kopie tych metod w BK BKCaravansBehavior sa martwe (AddDialogs BK = samo ret).
    ///  6. D11-2 (decyzja Jeffa 11.10, wylacznik BandJoinPurseToPlayer): banda, ktora gra przylacza do gracza bez bitwy (BanditInteractionsCampaignBehavior.
    ///     OpenRosterScreenAfterBanditEncounter: zdarzenie BanditPartyRecruited tuz przed DestroyPartyAction.Apply(MainParty, banda) - takze bandy, ktore
    ///     dolaczylyby do spotkania), oddaje cala kiese graczowi (GiveGoldAction banda -> gracz; bez 1/9 korony - to nie lup wojenny). Dotad poza regula:
    ///     od E3/7 kasa najblizszego miasta (CaravanCapital.RoutePurse), wczesniej znikala. Niezalezne od wylacznika 3.2.
    ///  7. D11-6 (decyzja Jeffa 11.10, wylacznik LordPurseOnlyToVictor): werdykt pkt 3 takze dla DRUZYN LORDOW - SplitPostfix zapamietuje druzyny lordow
    ///     strony pokonanej (niezaleznie od wylacznika 3.2), a sakiewke ludzi zniszczonej druzyny lorda (MenPurse.OnPartyDestroyed - biegnie po nas) bierze
    ///     tylko przywodca zwyciezcow tej bitwy (dalej jak dotad: wodz 1/3 + 2/3 jego ludziom, gracz - jego ludziom, banda, osada); niszczyciel, ktory
    ///     jej nie pokonal (gra niszczy tez druzyne ZWYCIESKIEJ strony bez ludzi z niszczycielem = przywodca przegranych; DestroyPartyAction bez bitwy) -
    ///     sakiewka do kasy najblizszego miasta (LordPurseToTown). Kiesa wodza zostaje z wodzem (bez zmian).
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
        private static int _yieldHooked;                                               // pkt 5: ile metod poddania wpietych (z 6)
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // pkt 3 (recenzja 3.2): pokonani w bitwie (karawany i bandy) -> bitwa, w ktorej przegrali; czyszczone przy zniszczeniu, co dobe i w Reset
        // (bitwa AI liczy lup i konczy sie w tym samym ticku, bitwa gracza - przy zatrzymanym czasie w menu spotkania)
        private static readonly Dictionary<MobileParty, MapEvent> _beaten = new Dictionary<MobileParty, MapEvent>();

        // ------------------------------------------------------------ liczniki doby
        private static int _raisedN, _battles, _stumbles;
        private static long _raisedGold, _remAdded, _undeadCut;
        private static readonly long[] _paid = new long[T], _kept = new long[T], _gone = new long[T], _disb = new long[T];
        private static readonly int[] _paidN = new int[T], _goneN = new int[T], _disbN = new int[T];
        private static long _toLords, _toPlayer, _toBands, _toParties;                 // pkt 2: wyplata gry, rozbicie wedlug zwyciezcow
        private static long _dLords, _dPlayer, _dBands, _dParties, _dSettl, _dTown, _dLost;   // pkt 3: reszta przy zniszczeniu
        private static readonly long[] _dWin = new long[T];                            // pkt 3: reszta przy zniszczeniu do zwyciezcy (wodz, partia, jego osada) wedlug rodzaju
        private static readonly long[] _odd = new long[T];                             // pkt 3: zniszczone z niszczycielem, ktory ich nie pokonal w bitwie (poza regula)
        private static readonly int[] _oddN = new int[T];
        private static readonly long[] _yield = new long[T];                           // pkt 5: kiesy oddane graczowi w rozmowie (tabory, karawany)
        private static readonly int[] _yieldN = new int[T];
        private static long _yieldBribe;                                               // pkt 5: z tego okup za przepuszczenie (czesc kiesy)
        // D11-2: bandy przylaczane teraz do gracza (zdarzenie BanditPartyRecruited -> zaraz DestroyPartyAction) i ich kiesy oddane graczowi dzis
        private static readonly HashSet<MobileParty> _joined = new HashSet<MobileParty>();
        private static long _joinGold; private static int _joinN;
        internal static bool JoinOn { get { var s = Settings.Current; return s != null && s.BandJoinPurseToPlayer; } }
        // D11-6: sakiewki ludzi zniszczonych druzyn lordow - pokonanych przez przywodce zwyciezcow (dalej jak dotad) i niepokonanych (-> najblizsze miasto)
        internal static bool LordVerdictOn { get { var s = Settings.Current; return s != null && s.LordPurseOnlyToVictor; } }
        private static long _lpWin, _lpTown; private static int _lpWinN, _lpTownN;

        /// <summary>Ksiega pieniadza (MoneyLedger.Daily biegnie przed naszym Daily - te same granice doby): kiesy pokonanych zdjete w wyplacie gry.</summary>
        internal static long DayTaken { get { return _paid[TVill] + _paid[TBand] + _paid[TCar]; } }
        internal static long DayToLords { get { return _toLords; } }
        internal static long DayToParties { get { return _toBands + _toParties; } }

        internal static void Reset()
        {
            _hooksLogged = false; _errSites.Clear();
            ClearDay();
            BattlePlunder.Reset();                          // E5/A6 (198): migawki i liczniki lupu z puli - ten sam punkt nowej kampanii
        }

        private static void ClearDay()
        {
            _raisedN = _battles = _stumbles = 0;
            _raisedGold = _remAdded = _undeadCut = 0;
            Array.Clear(_paid, 0, T); Array.Clear(_kept, 0, T); Array.Clear(_gone, 0, T); Array.Clear(_disb, 0, T);
            Array.Clear(_paidN, 0, T); Array.Clear(_goneN, 0, T); Array.Clear(_disbN, 0, T);
            _toLords = _toPlayer = _toBands = _toParties = 0;
            _dLords = _dPlayer = _dBands = _dParties = _dSettl = _dTown = _dLost = 0;
            Array.Clear(_dWin, 0, T); Array.Clear(_odd, 0, T); Array.Clear(_oddN, 0, T); Array.Clear(_yield, 0, T); Array.Clear(_yieldN, 0, T);
            _yieldBribe = 0;
            _beaten.Clear();
            _joined.Clear(); _joinGold = 0; _joinN = 0;
            _lpWin = _lpTown = 0; _lpWinN = _lpTownN = 0;
            _verdictParty = null; _verdictBeaten = false;
        }

        // 3.1 (poprawka po recenzji 3.1+3.3, W13 - jedna regula kiesa/sakiewka): werdykt "pokonana w tej bitwie przez przywodce zwyciezcow" dla partii
        // niszczonej TERAZ - nasluch MenPurse.OnPartyDestroyed biegnie po naszym (dopisany wczesniej; gra wola nasluchy od ostatniego) i bierze ten sam werdykt
        // dla sakiewki ludzi karawany (Seal31B.PurseOfBeaten). Tylko odczyt, bez zlota.
        private static MobileParty _verdictParty;
        private static bool _verdictBeaten;

        /// <summary>true = 3.2 czynne, partia niszczona teraz to karawana z niszczycielem, ktorej 3.2 NIE uznala za pokonana w bitwie przez przywodce
        /// zwyciezcow (przypadek "poza regula": zwycieska strona bez ludzi, przylaczenie, zadania, upadek rodu).</summary>
        internal static bool CaravanNotBeatenNow(MobileParty mp)
        {
            return On && mp != null && mp.IsCaravan && ReferenceEquals(_verdictParty, mp) && !_verdictBeaten;
        }

        /// <summary>
        /// D11-6, MenPurse.OnPartyDestroyed (sakiewka juz zdjeta): druzyna lorda niszczona TERAZ z niszczycielem - gdy werdykt NIE uznal jej za pokonana w tej
        /// bitwie przez przywodce zwyciezcow, sakiewka ludzi -> kasa najblizszego miasta (true = obsluzone). Pokonana - false (dalej jak dotad: zwyciezca),
        /// tylko licznik. Bez niszczyciela (rozwiazana) - false (jak dotad: najblizsze miasto).
        /// </summary>
        internal static bool LordPurseToTown(MobileParty mp, PartyBase destroyer, int purse)
        {
            try
            {
                if (!LordVerdictOn || mp == null || !mp.IsLordParty || destroyer == null || purse <= 0 || !ReferenceEquals(_verdictParty, mp)) return false;
                if (_verdictBeaten) { _lpWin += purse; _lpWinN++; return false; }
                var nt = VillageTakings.NearestTown(mp);
                if (nt == null || nt.Town == null) return false;                            // bez miasta - jak dotad (nie gubimy zlota)
                nt.Town.ChangeGold(purse);
                MoneyLedger.NotePurseGone(purse, true);                                      // paczka 169: linia "Obieg" (tylko licznik)
                MoneyLedger.Note169(MoneyLedger.N169PurseGone, nt, purse);
                _lpTown += purse; _lpTownN++;
                return true;
            }
            catch (Exception e) { Stumble("BeatenPurse.LordPurseToTown", e); return false; }
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
                bool lords = LordVerdictOn;                     // D11-6: werdykt dla druzyn lordow - niezalezny od wylacznika 3.2
                if ((!On && !lords) || !Live || __instance == null || __0 == null || __1 == null) return;
                // pkt 3 (recenzja 3.2): pokonani tej bitwy - karawany i bandy, takze z bohaterem na czele (gra bierze im tylko 5% zlota wodza) i takze
                // w zasadzce przy oblezeniu; tylko ich reszte przy zniszczeniu bierze zwyciezca (nie partie zwycieskiej strony bez ludzi);
                // D11-6: takze druzyny lordow (sakiewka ludzi - MenPurse)
                foreach (var d in __1)
                {
                    var dmp = d != null && d.Party != null ? d.Party.MobileParty : null;
                    if (dmp == null) continue;
                    int dt = TypeOf(dmp);
                    if ((On && (dt == TBand || dt == TCar)) || (lords && dmp.IsLordParty)) _beaten[dmp] = __instance;
                }
                if (!On) return;                                // lup z kies (pkt 1-2) - tylko przy 3.2
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

        // ------------------------------------------------------------ 6. D11-2: banda przylacza sie do gracza
        /// <summary>Nasluch BanditPartyRecruited: gra wola go dla kazdej bandy przylaczanej do gracza tuz przed DestroyPartyAction.Apply(MainParty, banda).</summary>
        internal static void OnBanditRecruited(MobileParty mp)
        {
            try { if (mp != null) _joined.Add(mp); }
            catch (Exception e) { Stumble("BeatenPurse.OnBanditRecruited", e); }
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
                if (party == null) return;
                MapEvent ev = null;
                bool known = _beaten.TryGetValue(party, out ev);
                if (known) _beaten.Remove(party);
                // werdykt dla sakiewki (MenPurse - po nas): to samo wyrazenie co "beaten" nizej
                _verdictParty = party;
                _verdictBeaten = known && ev != null && destroyer != null && ev.IsFinalized && ev.WinningSide != BattleSideEnum.None
                                 && ReferenceEquals(destroyer, ev.GetLeaderParty(ev.WinningSide));
                // D11-2: banda przylaczona do gracza (bez bitwy) - cala kiesa do gracza, bez 1/9 korony; niezalezne od wylacznika 3.2
                if (_joined.Remove(party) && JoinOn && Live && destroyer != null && destroyer.MobileParty != null && destroyer.MobileParty.IsMainParty && TypeOf(party) == TBand)
                {
                    int jg = party.PartyTradeGold;
                    var me = Hero.MainHero;
                    if (jg > 0 && me != null && me.IsAlive)
                    {
                        GiveGoldAction.ApplyForPartyToCharacter(party.Party, me, jg, true);   // zdarzenie gry: ksiega widzi przelew banda -> gracz
                        int moved = jg - party.PartyTradeGold;
                        if (moved > 0)
                        {
                            _joinGold += moved; _joinN++;
                            Log.Player("The outlaws who joined you hand over the coin they carried: " + moved + " denars.");
                        }
                    }
                    // reszta (nie powinna zostac) - dalej jak dotad: przy 3.2 "poza regula" (kiesa 0 - nic)
                }
                if (!On || !Live) return;
                if (party.IsVillager || (party.IsLordParty && party.LeaderHero != null)) return;   // tabor wsi - 112; kiesa wodza zostaje z wodzem
                int t = TypeOf(party);
                if (t < 0) return;
                int g = party.PartyTradeGold;
                if (g <= 0) return;
                if (destroyer == null) { _disb[t] += g; _disbN[t]++; return; }   // rozwiazana bez bitwy - poza regula (jak dotad znika; licznik)
                // recenzja 3.2 (3): zwyciezca tylko dla partii pokonanej w bitwie - zapamietanej w SplitPostfix i niszczonej przy koncu tej bitwy
                // (IsFinalized) przez przywodce strony zwyciezcow (gra: niszczyciel pokonanego = GetLeaderParty(strona przeciwna)). Inaczej
                // (partia zwycieskiej strony bez ludzi - niszczyciel = przywodca przegranych; banda przylacza sie do gracza; upadek rodu; zadania)
                // - poza regula jak rozwiazana: znika jak przed 3.2, bez 1/9, osobny licznik
                bool beaten = known && ev != null && ev.IsFinalized && ev.WinningSide != BattleSideEnum.None
                              && ReferenceEquals(destroyer, ev.GetLeaderParty(ev.WinningSide));
                if (!beaten) { _odd[t] += g; _oddN[t]++; CaravanCapital.RoutePurse(party, 2); return; }   // 3.1 (31, E3/7): poza regula 3.2 - kiesa do glowy rodu wlasciciela, brak - najblizsze miasto
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
                        _dWin[t] += moved;
                        Ransom178.NoteBeatenGold(hero, moved);   // 4: korona 1/9 od wodza-wasala
                    }
                    else if (hero == null && win.IsPartyTradeActive)
                    {
                        GiveGoldAction.ApplyForPartyToParty(party.Party, win.Party, g, true);
                        int moved = g - party.PartyTradeGold;
                        if (OutlawLaw.IsOutlawParty(win)) _dBands += moved; else _dParties += moved;
                        _dWin[t] += moved;
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
                if (own != null) { _dSettl += m; _dWin[t] += m; } else _dTown += m;
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

        // ------------------------------------------------------------ 5. poddanie graczowi bez bitwy (karawana, tabor wsi)
        /// <summary>Stan miedzy prefiksem a postfiksem metody poddania: partia, jej kiesa przed oddaniem, czy to okup za przepuszczenie.</summary>
        internal sealed class YieldState { internal MobileParty P; internal int Gold; internal bool Bribe; }

        /// <summary>
        /// Prefiks metod rozmowy, w ktorych gra oddaje graczowi kiese karawany albo taboru wsi (GiveGoldAction partia -> Hero.MainHero): poddanie
        /// i odjazd, wziecie do niewoli (partia znika - DestroyPartyAction po oddaniu kiesy, wiec nasluch zniszczenia widzi kiese 0), okup za
        /// przepuszczenie. Gra bierze partie z MobileParty.ConversationParty, a przy niewoli z PlayerEncounter (takze po rozmowie - ConversationEndOneShot).
        /// </summary>
        public static void YieldPrefix(MethodBase __originalMethod, out YieldState __state)
        {
            __state = null;
            try
            {
                if (!On || !Live) return;
                string n = __originalMethod != null ? __originalMethod.Name : "";
                MobileParty mp = null;
                if (n.IndexOf("took_prisoner", StringComparison.Ordinal) >= 0 && PlayerEncounter.Current != null) mp = PlayerEncounter.EncounteredMobileParty;
                if (mp == null) mp = MobileParty.ConversationParty;
                if (mp == null || !(mp.IsCaravan || mp.IsVillager)) return;
                __state = new YieldState { P = mp, Gold = mp.PartyTradeGold, Bribe = n.IndexOf("looted", StringComparison.Ordinal) >= 0 };
            }
            catch (Exception e) { Stumble("BeatenPurse.YieldPrefix", e); }
        }

        /// <summary>Postfiks: spadek kiesy = zloto oddane graczowi (jedyna zmiana kiesy w tych metodach) - licznik "poddane graczowi" i 1/9 korony.</summary>
        public static void YieldPostfix(YieldState __state)
        {
            try
            {
                if (__state == null || __state.P == null) return;
                long moved = (long)Math.Max(0, __state.Gold) - Math.Max(0, __state.P.PartyTradeGold);
                if (moved <= 0) return;
                int t = __state.P.IsVillager ? TVill : TCar;
                _yield[t] += moved; _yieldN[t]++;
                if (__state.Bribe) _yieldBribe += moved;
                Ransom178.NoteBeatenGold(Hero.MainHero, moved);   // 4: korona 1/9 od gracza-wasala - jak z kiesy rozbitej w bitwie
            }
            catch (Exception e) { Stumble("BeatenPurse.YieldPostfix", e); }
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
            // pkt 5 (recenzja 3.2): poddanie graczowi bez bitwy - metody rozmowy gry (klasy bez konstruktora statycznego - latka przy ladowaniu bezpieczna)
            var yieldM = new[]
            {
                new KeyValuePair<Type, string>(typeof(CaravansCampaignBehavior), "conversation_caravan_surrender_leave_on_consequence"),
                new KeyValuePair<Type, string>(typeof(CaravansCampaignBehavior), "conversation_caravan_took_prisoner_on_consequence"),
                new KeyValuePair<Type, string>(typeof(CaravansCampaignBehavior), "conversation_caravan_looted_leave_on_consequence"),
                new KeyValuePair<Type, string>(typeof(VillagerCampaignBehavior), "conversation_village_farmer_surrender_leave_on_consequence"),
                new KeyValuePair<Type, string>(typeof(VillagerCampaignBehavior), "conversation_village_farmer_took_prisoner_on_consequence"),
                new KeyValuePair<Type, string>(typeof(VillagerCampaignBehavior), "conversation_village_farmer_looted_leave_on_consequence"),
            };
            var yMiss = new List<string>();
            foreach (var kv in yieldM)
            {
                try
                {
                    var ym = AccessTools.Method(kv.Key, kv.Value, Type.EmptyTypes);
                    if (ym == null) { yMiss.Add(kv.Value); continue; }
                    h.Patch(ym, prefix: new HarmonyMethod(typeof(BeatenPurse), nameof(YieldPrefix)), postfix: new HarmonyMethod(typeof(BeatenPurse), nameof(YieldPostfix)) { priority = Priority.Last });
                    _yieldHooked++;
                }
                catch (Exception e) { yMiss.Add(kv.Value + " (" + e.Message + ")"); Log.Error("BeatenPurse.ApplyAll(poddanie)", e); }
            }
            done.Add("poddanie graczowi bez bitwy " + _yieldHooked + "/" + yieldM.Length);
            if (yMiss.Count > 0) miss.Add("poddanie graczowi: " + string.Join(", ", yMiss.ToArray()));
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
                       + "; latki: model " + (_modelHooked ? "tak" : "NIE") + ", podzial " + (_splitHooked ? "tak" : "NIE")
                       + ", poddanie graczowi bez bitwy " + _yieldHooked + "/6.";
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
                // recenzja 3.2 (2): zloto do zwyciezcow = wyplata gry w bitwie + reszta przy zniszczeniu (pkt 3) + sakwy taborow z 112 (VillageTakings - jego
                // Daily biegnie PO nas, liczniki doby sa jeszcze cale); "do band" ze wszystkich trzech drog (miara wzrostu band dla autotestu)
                long cartLords = VillageTakings.CartToLords, cartBands = VillageTakings.CartToBands;
                long toBandsAll = _toBands + _dBands + cartBands;
                var sb = new StringBuilder(1024);
                // poprawka po recenzji 3.1+3.3: od E3/7 kiesy rozwiazanych i "poza regula" nie znikaja (gdy Caravan Capital Real wlaczone)
                string gone7 = CaravanCapital.On
                    ? "E3/7: kiesa do glowy rodu wlasciciela (karawana notabla - notabl), bez glowy i bandy - do kasy najblizszego miasta; linia Szczelnosc (164a), kapital (164a-6)"
                    : "Caravan Capital Real wylaczone - jak dotad znika, w \"kiesy partii bez wodza, ktore zniknely\"";
                sb.Append("Kiesy rozbitych (3.2): dzien ").Append(day);
                if (!On) sb.Append(" | WYLACZONE (Beaten Purse To Victor) - lup z kies jak w grze (10% taboru i karawany, 50% bandy), reszta znika z partia");
                sb.Append(" | zloto rozbitych do zwyciezcow: tabory ").Append(_paid[TVill] + cartLords + cartBands).Append(", bandy ").Append(_paid[TBand] + _dWin[TBand])
                  .Append(", karawany ").Append(_paid[TCar] + _dWin[TCar]).Append("; z tego do band ").Append(toBandsAll)
                  .Append(" (wyplata gry w bitwie: tabory ").Append(_paid[TVill]).Append(", bandy ").Append(_paid[TBand]).Append(", karawany ").Append(_paid[TCar])
                  .Append(", do band ").Append(_toBands).Append("; reszta przy zniszczeniu: bandy ").Append(_dWin[TBand]).Append(", karawany ").Append(_dWin[TCar])
                  .Append(", do band ").Append(_dBands).Append("; sakwy taborow (112): wodzom ").Append(cartLords).Append(", bandom ").Append(cartBands).Append(")")
                  .Append(" | wyplata gry w bitwie: razem ").Append(paid).Append(" z ").Append(_paidN[TVill]).Append("/").Append(_paidN[TBand]).Append("/").Append(_paidN[TCar]).Append(" partii w ").Append(_battles)
                  .Append(" bitwach; do wodzow ").Append(_toLords).Append(_toPlayer > 0 ? " (w tym gracz " + _toPlayer + ")" : "").Append(", do kies band ").Append(_toBands)
                  .Append(", do kies innych partii bez wodza ").Append(_toParties).Append("; model gry podniosl lup ").Append(_raisedN).Append(" razy o ").Append(_raisedGold)
                  .Append(", zaokraglenia gry dopisane zwyciezcom ").Append(_remAdded)
                  .Append(" | w bitwie bez zwyciezcy, ktory moze wziac (milicja, patrol, Inni), zostalo w kiesach pokonanych ").Append(kept)
                  .Append(" (tabory ").Append(_kept[TVill]).Append(", bandy ").Append(_kept[TBand]).Append(", karawany ").Append(_kept[TCar]).Append("; Innym nie wyplacono ").Append(_undeadCut).Append(")")
                  .Append(" | reszta przy zniszczeniu rozbitych (karawany i bandy; tabory - 112): bandy ").Append(_gone[TBand]).Append(" (").Append(_goneN[TBand])
                  .Append("), karawany ").Append(_gone[TCar]).Append(" (").Append(_goneN[TCar]).Append(") -> wodzom ").Append(_dLords).Append(_dPlayer > 0 ? " (w tym gracz " + _dPlayer + ")" : "")
                  .Append(", bandom ").Append(_dBands).Append(", innym partiom ").Append(_dParties).Append(", kasie osady zwyciezcy ").Append(_dSettl)
                  .Append(", najblizszemu miastu ").Append(_dTown).Append(", zniklo ").Append(_dLost)
                  .Append(" | poddane graczowi bez bitwy (kiesa oddana w rozmowie, korona 1/9 jak z bitwy): tabory ").Append(_yield[TVill]).Append(" (").Append(_yieldN[TVill])
                  .Append("), karawany ").Append(_yield[TCar]).Append(" (").Append(_yieldN[TCar]).Append("); w tym okup za przepuszczenie ").Append(_yieldBribe)
                  .Append(" | rozwiazane bez bitwy (").Append(gone7).Append("): bandy ").Append(_disb[TBand]).Append(" (").Append(_disbN[TBand])
                  .Append("), karawany ").Append(_disb[TCar]).Append(" (").Append(_disbN[TCar]).Append(")")
                  .Append(" | sakiewki ludzi zniszczonych druzyn lordow (D11-6 ").Append(LordVerdictOn ? "WL" : "WYL - niszczyciel bierze jak dotad").Append("): pokonane w bitwie przez przywodce zwyciezcow - do zwyciezcy jak dotad ")
                  .Append(_lpWin).Append(" (").Append(_lpWinN).Append("), niszczyciel ich nie pokonal (zwycieska strona bez ludzi, bez bitwy) -> najblizsze miasto ").Append(_lpTown).Append(" (").Append(_lpTownN).Append(")")
                  .Append(" | przylaczone do gracza (D11-2 ").Append(JoinOn ? "WL" : "WYL - poza regula jak nizej").Append("): kiesy band -> gracz ").Append(_joinGold).Append(" (").Append(_joinN).Append(" band; bez 1/9 korony)")
                  .Append(" | zniszczone przez kogos, kto ich nie pokonal w bitwie (przylaczenie do gracza przy D11-2 wylaczonym, upadek rodu, zadania, zwycieska strona bez ludzi - poza regula 3.2, jak rozwiazane: ").Append(gone7).Append("): bandy ")
                  .Append(_odd[TBand]).Append(" (").Append(_oddN[TBand]).Append("), karawany ").Append(_odd[TCar]).Append(" (").Append(_oddN[TCar]).Append(")")
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
