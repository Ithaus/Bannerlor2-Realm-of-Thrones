using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E5/A6 (paczka 198, projekt etapu 5 R20; decyzja Jeffa 11.10 ok. 16:20 "tak, zrob od razu w porcji A") - LUP W BITWIE TYLKO Z KIESY POKONANEGO.
    /// Wylacznik BattlePlunderFromLoserOnly.
    ///
    /// GRA (dekompilacja TaleWorlds.CampaignSystem 1.4.8):
    ///  - MapEvent.CalculatePlunderedAndLostGoldAmounts (MapEvent.cs:1498-1526; wolane z CalculateMapEventResults przy OnBattleWon): pokonany z wodzem traci
    ///    GoldLost = 5% zlota wodza, najwyzej 10 000 (DefaultBattleRewardModel.cs:104-107), bez wodza z kiesa handlowa 10% / banda 50% (3.2 BeatenPurse:
    ///    cala kiesa); suma = pula, dzielona miedzy zwyciezcow wedlug wkladu (PlunderedGold).
    ///  - MapEvent.CalculateAndCommitMapEventResults (MapEvent.cs:1410-1431): lup cial, przedmiotow, jencow, statkow, potem CaptureDefeatedPartyMembers
    ///    (MapEvent.cs:1955-2041), ktory wodza pokonanej partii ZDEJMUJE z partii (RemovePartyLeader :1979-1982 - wziety do niewoli albo zbiegly;
    ///    gracza zdejmuje TakePrisonerAction :8-11), a dopiero potem CommitCalculatedMapEventResults -> MapEventParty.CommitGoldChanges
    ///    (MapEventParty.cs:499-524): zwyciezca z wodzem dostaje PlunderedGold zdarzeniem z niczego; pokonany placi GoldLost tylko, gdy
    ///    party.LeaderHero != null - a ten jest juz null; partia lorda nie ma kiesy handlowej (IsPartyTradeActive tylko z InitializePartyTrade:
    ///    karawany, tabory, bandy, dezerterzy, zalogi), wiec galaz "bez wodza" tez nic nie zdejmuje. Skutek: udzial puli z wodzow pokonanych
    ///    zwyciezcy dostaja Z NICZEGO (ksiega: ZRODLA "bitwy", UJSCIA "bitwy" = 0). Wodz zostaje w partii tylko przy odwrocie strony (RetreatingSide)
    ///    i przy smierci w bitwie (DeathMark) - wtedy gra placi zdarzeniem wodz -> nic.
    ///  - Statki zatopione w bitwie morskiej (LootDefeatedPartyShips, MapEvent.cs:1800-1817) dopisuja PlunderedGold PO podziale puli - to paczka B5
    ///    (ShipsPrizesToPort 181), tu tylko liczone osobno. E5/B5 (181 domyslnie WL): PrizeFin zdejmuje dopisek w finalizerze LootDefeatedPartyShips,
    ///    przed CommitCalculatedMapEventResults (MapEvent.cs:1419-1429), a port placi zwyciezcom przelewem - CountShips widzi wtedy statki 0, migawka
    ///    puli (Pool) = PlunderedGold sprzed LootDefeatedPartyShips, wiec rozliczenie A6 bez zmian; 1/9 korony z pryzow zglasza 181 (PayPrize).
    ///  - Inne mody: NavalDLCBattleRewardModel oddaje metody lupu modelowi bazowemu; ROT (CalculateAndCommitMapEventResults - tylko przedmioty smokow),
    ///    BK, RBM, BEE, DTE tych metod nie lataja (skan DLL i dekompilacja 11.10). Recenzja A6 (2): ROT EnlistmentPatches.patch39 - postfiks na
    ///    GETTERZE MapEventParty.PlunderedGold (blob atrybutu: MapEventParty, "PlunderedGold", MethodType.Getter) zwraca 0 dla MainParty, gdy gracz
    ///    jest na zaciagu; gra, 3.2 i A6 czytaja ten sam getter, wiec takiego udzialu nikt nie wyplaca i pokonani za niego nie placa (reszte
    ///    zaokraglen 3.2 - total minus widoczne udzialy - dostaje "top" i ten udzial A6 obciaza pokonanych jak kazdy inny; rachunek zamkniety).
    ///  - Nasz RealisticCaptivity (CaptivityBehavior.LootBaggage, nasluch HeroPrisonerTaken): pojmany GRACZ oddaje pojmujacemu LootGoldPercent
    ///    (60 u Jeffa, ModuleData/RealisticCaptivity.settings.xml) zlota - w TakePrisonerAction wewnatrz CaptureDefeatedPartyMembers, czyli PRZED
    ///    rozliczeniem A6 (recenzja A6 (1)). Pojmujacy bez bohatera (banda bez wodza, milicja/zaloga - gra daje jenca osadzie) - RC daje
    ///    zloto null, czyli w nicosc (stary wyciek RC, nie A6 - do decyzji Jeffa); A6 i tak liczy je jako zaliczke (gracz nie traci drugi raz).
    ///
    /// TERAZ (jedna regula: gracz, AI, bandy; suma lupu zwyciezcow z puli = suma tego, co naprawde schodzi z pokonanych):
    ///  1. MIGAWKA (postfiks Last na CalculatePlunderedAndLostGoldAmounts - po 3.2 BeatenPurse.SplitPostfix): wodz kazdego pokonanego z GoldLost
    ///     (zanim gra zdejmie go z partii) z jego zlotem w tej chwili i udzial kazdego zwyciezcy w puli (PlunderedGold przed dopiskiem za statki).
    ///  2. ROZLICZENIE (prefiks CommitCalculatedMapEventResults, przed kazda wyplata): zwyciezcy, ktorym gra wyplaci (wodz albo kiesa handlowa;
    ///     Inni - nie, gdy wlaczone 3.2: trup monety nie bierze, jak 3.2/112 - ich udzial z puli zdjety; recenzja A6 (3): przy wylaczonym 3.2 kiesy
    ///     placa pelne GoldLost, wiec udzialu Innych nie zdejmujemy - inaczej zszedlby z pokonanych w nicosc) maja dostac PayW. Kiesy partii bez
    ///     wodza (3.2) placa, co gra z nich zdejmie (min(GoldLost, kiesa)); reszte PayW placa wodzowie pokonanych - proporcjonalnie do tego, co
    ///     moga dac: min(GoldLost, zloto wodza) MINUS to, co od migawki juz zeszlo z jego zlota (recenzja A6 (1): RealisticCaptivity zabral pojmanemu
    ///     graczowi czesc kiesy dla pojmujacego - to zaliczka na jego czesc puli, nie drugi lup za to samo; jedna regula dla wszystkich wodzow,
    ///     u AI zwykle 0); wodz, ktory zostal w partii - gra (GoldLost = jego czesc); wodz zdjety z partii (niewola, ucieczka, gracz w niewoli) -
    ///     A6 w jego CommitGoldChanges. Gdy pokonani nie maja tyle (wodz poza swiatem, bez zlota albo juz oddal przy pojmaniu), udzialy zwyciezcow
    ///     z puli maleja do tego, co naprawde zejdzie; udzialow, ktorych gra nie wyplaci (milicja, osada, partia lorda bez wodza), pokonani nie placa.
    ///     Recenzja A6 (2a): czesc udzialu wodza-zwyciezcy, ktora placa wodzowie pokonanych, idzie do podstawy 1/9 korony (Ransom178.NoteBeatenGold -
    ///     prawo trzecich 178, decyzja Jeffa 09.10 Q3b "korona bierze 1/9 lupu wasali"; czesc z kies zglasza 3.2, statki - 181).
    ///  3. OBCIAZENIE (prefiks Normal na MapEventParty.CommitGoldChanges - po oknie ksiegi O14 "bitwy", ktore ma prefiks First): GiveGoldAction
    ///     wodz -> nic (to samo zdarzenie, ktorym gra placi GoldLost); ksiega widzi je w UJSCIACH "bitwy" obok ZRODEL "bitwy" (wyplata gry).
    ///     Gdyby partia nie doszla do swojej wyplaty - postfiks CommitCalculatedMapEventResults obciaza po wszystkim (licznik "poza oknem").
    /// Platnik -> odbiorca: zloto wodza pokonanego (i kiesa rozbitej partii - 3.2) -> zwyciezcy wedlug wkladu. Bez podwojenia: 3.2 przenosi kiesy
    /// partii bez wodza (A6 ich nie rusza), CasualtyLoot i MenPurse - sakiewki ludzi (inny garnek), statki - B5, RealisticCaptivity - zaliczka
    /// (wyzej). Wodz-wasal zwyciezcow -> skarbiec korony: 1/9 jego czesci z kies wodzow (178, nastepnego ranka). Linia "Lup w bitwie (A6)".
    /// </summary>
    internal static class BattlePlunder
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BattlePlunderFromLoserOnly; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private static bool _splitHooked, _commitHooked, _chargeHooked;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // 1. migawka podzialu: bitwa -> (pokonany -> wodz przed zdjeciem z partii; zwyciezca -> udzial w puli)
        private sealed class Snap
        {
            internal readonly List<MapEventParty> Losers = new List<MapEventParty>();
            internal readonly List<Hero> Leaders = new List<Hero>();
            internal readonly List<int> Gold0 = new List<int>();          // zloto wodza w chwili migawki (recenzja A6 (1): zaliczka oddana przy pojmaniu)
            internal readonly List<MapEventParty> Winners = new List<MapEventParty>();
            internal readonly List<int> Pool = new List<int>();
        }
        private static readonly Dictionary<MapEvent, Snap> _snaps = new Dictionary<MapEvent, Snap>();

        // 3. zaplanowane obciazenia wodzow zdjetych z partii (gra ich nie obciazy) - wykonywane w CommitGoldChanges tej partii
        private sealed class Charge { internal Hero H; internal int Amount; }
        private static readonly Dictionary<MapEventParty, Charge> _pending = new Dictionary<MapEventParty, Charge>();

        // ------------------------------------------------------------ liczniki doby
        private static int _battles, _snapN, _noSnap, _stumbles, _staleSnaps, _stalePending, _late, _gameTook;
        private static long _payLords, _payParties;            // wyplata z puli (lad): wodzom (zdarzenie z niczego) i kiesom partii bez wodza
        private static long _takeGame, _takeOurs, _takePlayer, _takePurse;   // zdjete z pokonanych: gra (wodz w partii), A6 (wodz zdjety), kiesy partii bez wodza
        private static long _shipsPaid, _shipsUnpaid, _noSnapGold;   // dopisek gry za statki zatopione (po podziale puli) - B5, bez zmian: wyplacony / bez wyplaty
        private static long _cut, _undeadCut, _unpaid, _excess, _short, _noPayer, _vanilla;
        private static long _prepaid, _prepaidPlayer, _ninth;  // recenzja A6: zaliczone wodzom (oddane przy pojmaniu - RealisticCaptivity), podstawa 1/9 korony z czesci wodzow

        internal static void Reset()
        {
            try { _errSites.Clear(); _snaps.Clear(); _pending.Clear(); ClearDay(); }
            catch { }
        }

        private static void ClearDay()
        {
            _battles = _snapN = _noSnap = _stumbles = _staleSnaps = _stalePending = _late = _gameTook = 0;
            _payLords = _payParties = _takeGame = _takeOurs = _takePlayer = _takePurse = _shipsPaid = _shipsUnpaid = _noSnapGold = 0;
            _cut = _undeadCut = _unpaid = _excess = _short = _noPayer = _vanilla = 0;
            _prepaid = _prepaidPlayer = _ninth = 0;
        }

        /// <summary>Wyjatek przy jednej bitwie: liczony zawsze, do pliku raz na miejsce (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("BattlePlunder." + where, e); } catch { }
        }

        // ------------------------------------------------------------ 1. migawka podzialu
        /// <summary>Postfiks Last MapEvent.CalculatePlunderedAndLostGoldAmounts (po 3.2): wodzowie pokonanych i udzialy zwyciezcow w puli. Tylko odczyt.</summary>
        public static void SplitPostfix(MapEvent __instance, MBReadOnlyList<MapEventParty> __0, MBReadOnlyList<MapEventParty> __1)
        {
            try
            {
                if (!Live || __instance == null || __0 == null || __1 == null) return;
                _snaps.Remove(__instance);                      // ponowne liczenie tej samej bitwy - gra ustawia PlunderedGold i GoldLost od nowa
                if (__instance.IsSiegeAmbush) return;          // gra nie liczy tu puli
                Snap sn = null;
                foreach (var d in __1)
                {
                    if (d == null || d.GoldLost <= 0) continue;
                    if (sn == null) sn = new Snap();
                    var lh = d.Party != null ? d.Party.LeaderHero : null;
                    sn.Losers.Add(d); sn.Leaders.Add(lh); sn.Gold0.Add(lh != null ? lh.Gold : 0);
                }
                if (sn == null) return;                         // bez puli - nic do wyplaty
                foreach (var w in __0)
                {
                    if (w == null || w.PlunderedGold <= 0) continue;
                    sn.Winners.Add(w); sn.Pool.Add(w.PlunderedGold);
                }
                _snaps[__instance] = sn;
                _snapN++;
            }
            catch (Exception e) { Stumble("SplitPostfix", e); }
        }

        // ------------------------------------------------------------ 2. rozliczenie przed wyplata
        /// <summary>Rozdzial kwoty total na pozycje wedlug wag (kazda najwyzej swoja waga, suma = total przy total &lt;= suma wag): w dol + najwieksze reszty.</summary>
        private static long[] Split(long total, long[] weights)
        {
            int n = weights.Length;
            var r = new long[n];
            long sum = 0;
            for (int i = 0; i < n; i++) sum += Math.Max(0, weights[i]);
            if (total <= 0 || sum <= 0) return r;
            if (total >= sum) { for (int i = 0; i < n; i++) r[i] = Math.Max(0, weights[i]); return r; }
            var frac = new double[n];
            long given = 0;
            for (int i = 0; i < n; i++)
            {
                double x = (double)total * Math.Max(0, weights[i]) / sum;
                r[i] = (long)Math.Floor(x); frac[i] = x - r[i]; given += r[i];
            }
            long rem = total - given;
            while (rem > 0)
            {
                int best = -1;
                for (int i = 0; i < n; i++) if (r[i] < weights[i] && (best < 0 || frac[i] > frac[best])) best = i;
                if (best < 0) break;
                r[best]++; frac[best] = -1; rem--;
            }
            return r;
        }

        /// <summary>
        /// Prefiks MapEvent.CommitCalculatedMapEventResults: suma wyplat zwyciezcom z puli = suma tego, co naprawde zejdzie z pokonanych.
        /// Bez migawki (np. kontynuacja bitwy gracza) - gra bez zmian, licznik.
        /// </summary>
        public static void CommitPrefix(MapEvent __instance)
        {
            try
            {
                if (_pending.Count > 0) { _stalePending += _pending.Count; _pending.Clear(); }   // poprzednia wyplata przerwana wyjatkiem (cudzy kod) - liczymy
                if (__instance == null) return;
                Snap sn;
                bool has = _snaps.TryGetValue(__instance, out sn);
                if (has) _snaps.Remove(__instance);
                if (!Live) return;
                // statki (B5, bez zmian): dopisek gry = PlunderedGold ponad udzial z puli - u kazdego zwyciezcy, takze bez migawki (bitwa bez puli)
                if (!CountShips(__instance, sn)) { _noSnap++; return; }   // pula bez migawki - gra bez zmian, zloto w "wyplata bez migawki"
                if (!has) return;
                bool on = On;
                // recenzja A6 (3): udzial Innych zdejmujemy tylko przy wlaczonym 3.2 - wtedy kiesy pokonanych placa tylko wyplacana czesc puli (3.2 fd);
                // przy wylaczonym 3.2 kiesy placa pelne GoldLost, wiec zdjety udzial Innych zszedlby z pokonanych w nicosc - Inni biora jak w grze
                bool cutUndead = BeatenPurse.On;

                // zwyciezcy: udzial z puli (bez dopisku za statki) i czy gra go wyplaci
                int nw = sn.Winners.Count;
                var pool = new long[nw]; var ship = new long[nw]; var pay = new bool[nw];
                long payW = 0, undead = 0;
                for (int i = 0; i < nw; i++)
                {
                    var w = sn.Winners[i];
                    int pg = w.PlunderedGold;
                    if (pg <= 0) continue;
                    pool[i] = Math.Min(sn.Pool[i], pg); ship[i] = pg - pool[i];
                    var p = w.Party; var mp = p != null ? p.MobileParty : null;
                    bool hero = p != null && p.LeaderHero != null;
                    bool trade = !hero && p != null && p.IsMobile && mp != null && mp.IsPartyTradeActive;
                    if (!hero && !trade) { _unpaid += pool[i]; continue; }            // gra nic nie wyplaci (milicja, osada, partia lorda bez wodza)
                    if (cutUndead && mp != null && Undead.Party(mp)) { undead += pool[i]; continue; }   // Inni - nizej (wlaczone A6 i 3.2: udzial zdjety)
                    pay[i] = true; payW += pool[i];
                }

                // pokonani: co zejdzie naprawde (gra: wodz w partii i kiesa handlowa; A6: wodz zdjety z partii)
                int nl = sn.Losers.Count;
                var cls = new int[nl]; var cap = new long[nl]; var who = new Hero[nl];   // cls: 0 - nic, 1 - wodz (gra), 2 - wodz zdjety (A6), 3 - kiesa
                var pre = new long[nl];                         // recenzja A6 (1): czesc puli wodza zaliczona na to, co juz oddal od migawki (pojmanie)
                long purseReal = 0, lordCap = 0, gameCap = 0;
                for (int i = 0; i < nl; i++)
                {
                    var d = sn.Losers[i];
                    int lost = d.GoldLost;
                    if (lost <= 0) continue;
                    var p = d.Party;
                    Hero now = p != null ? p.LeaderHero : null;
                    if (now != null)
                    {
                        // wodz w partii: ten sam w migawce - zaliczka liczona tak samo (zwykle 0: odwrot i smierc w bitwie bez pojmania)
                        cls[i] = 1; who[i] = now; cap[i] = LeaderCap(lost, now, sn.Leaders[i] == now ? sn.Gold0[i] : now.Gold, out pre[i]);
                        lordCap += cap[i]; gameCap += Math.Min(lost, Math.Max(0, now.Gold));   // gra (wylaczone A6) zdjelaby pelne min(GoldLost, zloto)
                    }
                    else if (p != null && p.IsMobile && p.MobileParty != null && p.MobileParty.IsPartyTradeActive)
                    {
                        cls[i] = 3; cap[i] = Math.Min(lost, Math.Max(0, p.MobileParty.PartyTradeGold)); purseReal += cap[i];
                    }
                    else
                    {
                        var h = sn.Leaders[i];
                        if (h != null && !CirculationWindows.OutOfWorld(h)) { cls[i] = 2; who[i] = h; cap[i] = LeaderCap(lost, h, sn.Gold0[i], out pre[i]); lordCap += cap[i]; }
                        else _noPayer += lost;                  // wodz poza swiatem albo partia bez wodza i bez kiesy - nikt nie zaplaci
                    }
                }
                _battles++;
                // jak dotad (gra): wyplaca wszystkim z wodzem albo kiesa (takze Innym), zdejmuje tylko kiesy i wodzow, ktorzy zostali w partii
                long vanilla = payW + undead - purseReal - gameCap;
                _vanilla += vanilla;

                if (!on)
                {
                    // wylaczone - gra jak dotad; licznik wyplaty i zdjec wedlug gry (netto z niczego = vanilla)
                    for (int i = 0; i < nw; i++) if (pay[i] || (pool[i] > 0 && IsUndeadPayable(sn.Winners[i]))) AddPay(sn.Winners[i], pool[i]);
                    _takePurse += purseReal; _takeGame += gameCap;
                    return;
                }

                // Inni: trup monety nie bierze - udzial z puli zdjety (zostaje u pokonanych), dopisek za statki zostaje (B5)
                if (undead > 0)
                {
                    for (int i = 0; i < nw; i++)
                        if (!pay[i] && pool[i] > 0 && IsUndeadPayable(sn.Winners[i])) { sn.Winners[i].PlunderedGold = (int)ship[i]; _undeadCut += pool[i]; pool[i] = 0; }
                }

                // wodzowie placa reszte (ponad kiesy) proporcjonalnie do tego, co moga dac
                long needL = Math.Max(0, payW - purseReal);
                long lordPay = Math.Min(needL, lordCap);
                var lordW = new long[nl];
                for (int i = 0; i < nl; i++) lordW[i] = cls[i] == 1 || cls[i] == 2 ? cap[i] : 0;
                var part = Split(lordPay, lordW);
                for (int i = 0; i < nl; i++)
                {
                    if (cls[i] != 1 && cls[i] != 2) continue;
                    var d = sn.Losers[i];
                    d.GoldLost = (int)part[i];                  // wodz w partii: gra obciazy go ta kwota (GiveGoldAction wodz -> nic)
                    if (cls[i] == 1) _takeGame += part[i];
                    else if (part[i] > 0) _pending[d] = new Charge { H = who[i], Amount = (int)part[i] };
                    if (pre[i] > 0) { _prepaid += pre[i]; if (who[i] == Hero.MainHero) _prepaidPlayer += pre[i]; }   // recenzja A6 (1): zaliczka z pojmania
                }
                _takePurse += purseReal;

                // gdy pokonani nie maja tyle - udzialy zwyciezcow z puli maleja do tego, co naprawde zejdzie
                long supply = purseReal + lordPay;
                long[] fin = pool;
                if (payW > supply)
                {
                    long cut = payW - supply;
                    var pw = new long[nw];
                    for (int i = 0; i < nw; i++) pw[i] = pay[i] ? pool[i] : 0;
                    fin = Split(supply, pw);
                    for (int i = 0; i < nw; i++) if (pay[i]) sn.Winners[i].PlunderedGold = (int)(fin[i] + ship[i]);
                    _cut += cut;
                }
                else if (supply > payW) _excess += supply - payW;   // kiesy (3.2, zaokraglenia w gore) zaplacily wiecej niz pula wyplacana
                for (int i = 0; i < nw; i++) if (pay[i]) AddPay(sn.Winners[i], fin[i]);

                // recenzja A6 (2a): korona 1/9 (prawo trzecich 178; Jeff 09.10 Q3b "korona bierze 1/9 lupu wasali") - czesc udzialu wodza-zwyciezcy
                // placona przez wodzow pokonanych (lordPay rozdzielone wedlug wyplacanych udzialow - jak 3.2 dzieli czesc z kies); wasal w wojnie
                // sprawdza Ransom178 (Taxed), Inni - nie (jak 3.2)
                if (lordPay > 0)
                {
                    var fw = new long[nw];
                    for (int i = 0; i < nw; i++) fw[i] = pay[i] ? fin[i] : 0;
                    var lp = Split(lordPay, fw);
                    for (int i = 0; i < nw; i++)
                    {
                        if (lp[i] <= 0) continue;
                        var p = sn.Winners[i].Party;
                        var h = p != null ? p.LeaderHero : null;
                        var mp = p != null ? p.MobileParty : null;
                        if (h == null || !h.IsAlive || (mp != null && Undead.Party(mp))) continue;
                        Ransom178.NoteBeatenGold(h, lp[i]);
                        _ninth += lp[i];
                    }
                }
            }
            catch (Exception e) { Stumble("CommitPrefix", e); }
        }

        /// <summary>
        /// Recenzja A6 (1): ile wodz pokonanych moze dac z puli - min(GoldLost, zloto teraz) minus to, co od migawki juz zeszlo z jego zlota
        /// (zaliczka: RealisticCaptivity LootBaggage przy pojmaniu gracza oddaje czesc kiesy pojmujacemu - drugi raz za to samo nie bierzemy).
        /// Jedna regula dla gracza i AI (u AI zwykle 0). Nic z niczego: mniejsza czesc wodza = mniejsze udzialy zwyciezcow (ciecie nizej).
        /// </summary>
        private static long LeaderCap(int lost, Hero h, int gold0, out long credited)
        {
            long g = Math.Max(0, h.Gold);
            long full = Math.Min((long)lost, g);
            long gone = Math.Max(0L, (long)gold0 - g);
            credited = Math.Min(full, gone);
            return full - credited;
        }

        private static bool IsUndeadPayable(MapEventParty w)
        {
            var p = w != null ? w.Party : null;
            var mp = p != null ? p.MobileParty : null;
            if (mp == null || !Undead.Party(mp)) return false;
            return p.LeaderHero != null || (p.IsMobile && mp.IsPartyTradeActive);
        }

        private static void AddPay(MapEventParty w, long x)
        {
            if (x <= 0 || w == null || w.Party == null) return;
            if (w.Party.LeaderHero != null) _payLords += x; else _payParties += x;
        }

        private static bool Payable(MapEventParty w)
        {
            var p = w != null ? w.Party : null;
            if (p == null) return false;
            var mp = p.MobileParty;
            return p.LeaderHero != null || (p.IsMobile && mp != null && mp.IsPartyTradeActive);
        }

        /// <summary>
        /// Dopisek gry za statki zatopione (LootDefeatedPartyShips, po podziale puli) u wszystkich uczestnikow z PlunderedGold: ponad udzial z migawki,
        /// wyplacony (wodz albo kiesa handlowa) albo bez wyplaty. false = jest pula (GoldLost), a migawki nie ma - wtedy nic nie liczy (zloto w _noSnapGold).
        /// </summary>
        private static bool CountShips(MapEvent me, Snap sn)
        {
            long paid = 0, unpaid = 0, all = 0; bool lost = false;
            foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
            {
                if (side == null) continue;
                foreach (var w in side.Parties)
                {
                    if (w == null) continue;
                    if (w.GoldLost > 0) lost = true;
                    int pg = w.PlunderedGold;
                    if (pg <= 0) continue;
                    all += pg;
                    int pool = 0;
                    if (sn != null) { int k = sn.Winners.IndexOf(w); if (k >= 0) pool = Math.Min(sn.Pool[k], pg); }
                    long ship = pg - pool;
                    if (ship <= 0) continue;
                    if (Payable(w)) paid += ship; else unpaid += ship;
                }
            }
            if (sn == null && lost) { _noSnapGold += all; return false; }
            _shipsPaid += paid; _shipsUnpaid += unpaid;
            return true;
        }

        // ------------------------------------------------------------ 3. obciazenie wodza zdjetego z partii
        /// <summary>Prefiks Normal MapEventParty.CommitGoldChanges (po oknie ksiegi O14 - prefiks First): wodz pokonanego placi swoja czesc puli.</summary>
        public static void ChargePrefix(MapEventParty __instance)
        {
            try
            {
                if (_pending.Count == 0 || __instance == null) return;
                Charge c;
                if (!_pending.TryGetValue(__instance, out c)) return;
                _pending.Remove(__instance);
                var p = __instance.Party;
                // wodz wrocil do partii albo partia ma kiese - gra sama zdejmie GoldLost (ustawione w rozliczeniu na te sama kwote)
                if (p != null && (p.LeaderHero != null || (p.IsMobile && p.MobileParty != null && p.MobileParty.IsPartyTradeActive))) { _gameTook++; _takeGame += c.Amount; return; }
                Execute(c);
            }
            catch (Exception e) { Stumble("ChargePrefix", e); }
        }

        /// <summary>Postfiks MapEvent.CommitCalculatedMapEventResults: obciazenia, ktore nie doszly do wyplaty swojej partii - po wszystkim (poza oknem ksiegi).</summary>
        public static void CommitPostfix()
        {
            try
            {
                if (_pending.Count == 0) return;
                var left = new List<Charge>(_pending.Values);
                _pending.Clear();
                foreach (var c in left) { _late++; Execute(c); }
            }
            catch (Exception e) { _pending.Clear(); Stumble("CommitPostfix", e); }
        }

        private static void Execute(Charge c)
        {
            try
            {
                var h = c.H;
                if (h == null || c.Amount <= 0 || CirculationWindows.OutOfWorld(h)) { _short += Math.Max(0, c.Amount); return; }
                int g0 = h.Gold;
                GiveGoldAction.ApplyBetweenCharacters(h, null, c.Amount, true);   // to samo zdarzenie, ktorym gra placi GoldLost (ksiega: UJSCIA "bitwy")
                int moved = Math.Max(0, g0 - h.Gold);
                _takeOurs += moved;
                if (moved < c.Amount) _short += c.Amount - moved;
                if (h == Hero.MainHero && moved > 0)
                {
                    _takePlayer += moved;
                    Log.Player("The victors took " + moved + " denars from your purse.", true);
                }
            }
            catch (Exception e) { Stumble("Execute", e); }
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            try
            {
                var m = AccessTools.Method(typeof(MapEvent), "CalculatePlunderedAndLostGoldAmounts");
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && !m.IsStatic && ps.Length == 2)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(BattlePlunder), nameof(SplitPostfix)) { priority = Priority.Last });
                    _splitHooked = true; done.Add("migawka podzialu (MapEvent.CalculatePlunderedAndLostGoldAmounts, po 3.2)");
                }
                else miss.Add("migawka podzialu (brak MapEvent.CalculatePlunderedAndLostGoldAmounts(winners, defeated))");
            }
            catch (Exception e) { miss.Add("migawka podzialu (" + e.Message + ")"); Log.Error("BattlePlunder.ApplyAll(podzial)", e); }
            try
            {
                var m = AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults", Type.EmptyTypes);
                if (m != null && !m.IsStatic)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(BattlePlunder), nameof(CommitPrefix)) { priority = Priority.Low },
                               postfix: new HarmonyMethod(typeof(BattlePlunder), nameof(CommitPostfix)) { priority = Priority.Last });
                    _commitHooked = true; done.Add("rozliczenie przed wyplata (MapEvent.CommitCalculatedMapEventResults)");
                }
                else miss.Add("rozliczenie (brak MapEvent.CommitCalculatedMapEventResults())");
            }
            catch (Exception e) { miss.Add("rozliczenie (" + e.Message + ")"); Log.Error("BattlePlunder.ApplyAll(rozliczenie)", e); }
            try
            {
                var m = AccessTools.Method(typeof(MapEventParty), "CommitGoldChanges", Type.EmptyTypes);
                if (m != null && !m.IsStatic)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(BattlePlunder), nameof(ChargePrefix)) { priority = Priority.Normal });
                    _chargeHooked = true; done.Add("obciazenie wodza zdjetego z partii (MapEventParty.CommitGoldChanges, po oknie ksiegi)");
                }
                else miss.Add("obciazenie (brak MapEventParty.CommitGoldChanges())");
            }
            catch (Exception e) { miss.Add("obciazenie (" + e.Message + ")"); Log.Error("BattlePlunder.ApplyAll(obciazenie)", e); }
            var s = Settings.Current;
            Log.Info("BattlePlunder (E5/A6): lup w bitwie tylko z kiesy pokonanego - latki wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "zadne")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "") + ". Wylacznik Battle Plunder From Loser Only "
                     + (s != null && s.BattlePlunderFromLoserOnly ? "WLACZONY" : "wylaczony") + ".");
        }

        // ------------------------------------------------------------ raz na dobe
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                // migawki bitew juz zakonczonych bez wyplaty (gracz wyszedl ze spotkania) - wyrzucone; trwajace zostaja
                if (_snaps.Count > 0)
                {
                    var dead = new List<MapEvent>();
                    foreach (var kv in _snaps) if (kv.Key == null || kv.Key.IsFinalized) dead.Add(kv.Key);
                    foreach (var k in dead) _snaps.Remove(k);
                    _staleSnaps += dead.Count;
                }
                long payAll = _payLords + _payParties, take = _takeGame + _takeOurs + _takePurse;
                long net = payAll - take;
                var sb = new StringBuilder(1024);
                sb.Append("Lup w bitwie (A6): dzien ").Append(day);
                sb.Append(On ? " | WLACZONE (Battle Plunder From Loser Only)" : " | WYLACZONE (Battle Plunder From Loser Only) - gra jak dotad, tylko pomiar");
                sb.Append(" | bitwy z pula zlota ").Append(_battles).Append(" (migawek ").Append(_snapN).Append(", wyplata bez migawki ").Append(_noSnap).Append(" - zloto ").Append(_noSnapGold).Append(")")
                  .Append(" | pula wyplacona zwyciezcom (lad): wodzom ").Append(_payLords).Append(", kiesom partii bez wodza ").Append(_payParties).Append(" (razem ").Append(payAll).Append(")")
                  .Append(" | zdjete z pokonanych: wodzowie w partii (gra - odwrot, smierc w bitwie) ").Append(_takeGame)
                  .Append(", wodzowie zdjeci z partii (A6 - niewola, ucieczka) ").Append(_takeOurs).Append(_takePlayer > 0 ? " (w tym gracz " + _takePlayer + ")" : "")
                  .Append(", kiesy partii bez wodza (3.2) ").Append(_takePurse).Append(" (razem ").Append(take).Append(")")
                  .Append(" | netto z niczego na ladzie ").Append(net >= 0 ? "+" : "").Append(net).Append(" (prog 0 +- 1 tys./d; bez A6 byloby ").Append(_vanilla >= 0 ? "+" : "").Append(_vanilla).Append(")")
                  .Append(" | statki zatopione w bitwach morskich (NavalDLC, dopisek gry do PlunderedGold po podziale puli - paczka B5: przy Ship Prizes To Port ")
                  .Append(ShipsToPort181.PrizeOn ? "WL dopisek zdejmuje 181, port placi zwyciezcom - tu 0" : "WYL gra placi jak dotad").Append("): wyplacone z niczego ").Append(_shipsPaid)
                  .Append(", bez wyplaty (zwyciezca bez wodza i kiesy) ").Append(_shipsUnpaid)
                  .Append(" | obciete zwyciezcom (pokonany bez zlota albo poza swiatem) ").Append(_cut).Append(", Innym nie wyplacono ").Append(_undeadCut)
                  .Append(", udzialy, ktorych gra nie wyplaca (milicja, osada, partia bez wodza i kiesy) - pokonani ich nie placa ").Append(_unpaid)
                  .Append(", kiesy zaplacily ponad pule (zaokraglenia 3.2) ").Append(_excess).Append(", pokonani bez platnika ").Append(_noPayer)
                  .Append(" | zaliczone wodzom pokonanych (zloto zabrane im przy pojmaniu - RealisticCaptivity) ").Append(_prepaid).Append(" (w tym gracz ").Append(_prepaidPlayer).Append(")")
                  .Append(" | korona 1/9 (178): czesc wodzow-zwyciezcow z kies wodzow pokonanych zgloszona do podstawy ").Append(_ninth).Append(" (liczy tylko wasal w wojnie)")
                  .Append(BeatenPurse.On ? "" : " | 3.2 WYLACZONE - Inni biora udzial z puli jak w grze")
                  .Append(" | kontrolki: obciazenie mniejsze niz plan ").Append(_short).Append(", poza oknem ksiegi ").Append(_late).Append(", przejete przez gre ").Append(_gameTook)
                  .Append(", wyplaty przerwane ").Append(_stalePending).Append(", migawki bez wyplaty ").Append(_staleSnaps)
                  .Append(" | latki: migawka ").Append(_splitHooked ? "tak" : "NIE").Append(", rozliczenie ").Append(_commitHooked ? "tak" : "NIE").Append(", obciazenie ").Append(_chargeHooked ? "tak" : "NIE");
                if (_stumbles > 0) sb.Append(" | wyjatki zlapane ").Append(_stumbles).Append(" (pierwszy z kazdego miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("BattlePlunder.Daily", e); }
            finally { ClearDay(); }
        }
    }
}
