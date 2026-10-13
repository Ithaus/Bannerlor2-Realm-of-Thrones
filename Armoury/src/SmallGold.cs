using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E5/A7 (audyt zlota z niczego 11.10, rozdz. 1b wiersze 12-14 i 16, rozdz. 5b "E5/A7 - drobne zrodla gry") - male zrodla zlota z niczego w grze.
    /// Kazde ma wlasny wylacznik; linia doby "Drobne zrodla (E5/A7): dzien N" z kwotami przed (co gra by dala) i po (co zaplacone).
    ///
    /// 1. RABUNEK WSI (RaidLootFromVillage; G RaidEventComponent.cs:170-189): kazdy krok lupienia daje wodzowi napastnika 4 zl za stracony hearth x mnoznik
    ///    lupu - GiveGoldAction(nic, wodz) z niczego. Teraz platnik = KIESA WSI (Village.Gold), najwyzej tyle, ile w niej jest - pusta wies nie daje nic.
    ///    Mechanizm (wzor NotableGold/RealPayer): prefiks Low + finalizer na RaidEventComponent.Update otwiera kontekst (tylko w fazie lupienia - ten sam warunek
    ///    co gra: DefenderSide.TroopCount == 0; hearth wsi zapamietany PO podmianie E4/5 Devastation - prefiks Normal); prefiks Low na GiveGoldAction.ApplyInternal
    ///    zamienia strone "nic" na wies (giverParty = Settlement.Party wsi) i przycina kwote do kiesy wsi. Dopasowanie: pierwsze zdarzenie "nic -> wodz" w oknie
    ///    i dopiero po spadku hearth (gra zdejmuje hearth tuz przed wyplata - R:175), wiec zdarzenia sluchaczy z dalszej czesci kroku nie sa nasze.
    ///    Ksiega pieniadza widzi przelew wies -> bohater (platnik i odbiorca). Pomiar "lup z niczego" (Hearth E4/0: okno W6 Measure40 i Devastation) liczy
    ///    przyrost kiesy wodza w kroku MINUS to, co zaplacila wies (RaidPaidSum) - przy czynnym wylaczniku 0. Gracz = AI (ta sama regula); gra pokazuje graczowi
    ///    kwote przed obcieciem, wiec raz na rabunek - komunikat, gdy kiesa wsi nie starcza. Lupienie gracza akcja wrogosci (VillageHostileActionCampaignBehavior
    ///    :556) - poza zakresem (tylko gracz). E7/R1 (projekt etapu 7) ma lupic swoje rabunki ta sama regula (kiesa wsi do jej wysokosci).
    /// 2. KIESA STARTOWA NOWEJ ZALOGI (GarrisonNoStartPurse; G GarrisonPartyComponent.cs:96): gra daje kazdej nowej zalodze PartyGoldLowerThreshold (5 000)
    ///    z niczego - po zdobyciu twierdzy stara zaloga ginie (ChangeOwnerOfSettlementAction.cs:34-41), nowa powstaje z 5 000. Teraz nowa zaloga po starcie gry
    ///    zostaje z kiesa sprzed InitializePartyTrade (nowa partia: 0). Recenzja A7: kiesa STAREJ zalogi przy zdobyciu (BySiege) nie miala odbiorcy - 3.2
    ///    BeatenPurse.TypeOf pomija zalogi (tylko tabory, karawany, bandy), E3/7 RoutePurse tylko partie rozwiazane (niszczyciel null), wiec znikala z partia
    ///    (U8 "garnizony" w ksiedze - CirculationWindows.OnPartyDestroyed). Teraz okno ChangeOwnerOfSettlementAction.ApplyInternal (BySiege, jest zaloga) zdejmuje
    ///    kiese starej zalogi przed zniszczeniem, a nowa zaloga tej osady ja przejmuje (audyt 5b E5/A7 (2): "dostaje kiese starej zalogi" - przelew, zloto ani
    ///    nie powstaje, ani nie znika; falszywa para U8/R znika). Gdy nowej zalogi nie ma (gra nie zniszczyla starej, wyjatek) - kiesa wraca do zalogi osady,
    ///    a bez zalogi - do kasy osady (licznik kontroli). Dalej kiese wypelnia rod wlasciciela z salda zwyklym rozliczeniem zoldu (BK PartyExpensesPrefix:
    ///    zold + 200/d, dopoki kiesa < 5 000; gra: do 5 000 - SoldierPay placi zalodze tylko to, co jest w kiesie). Prefiks zapamietuje kiese przed, postfiks
    ///    ja przywraca - partia zamieniona w zaloge (SetPartyComponent) nie traci swojej kiesy. Tworzenie swiata (przed GameStarted) - regula gry (bilans otwarcia).
    /// 3. "ZA TIER" (TierIncomeAiOff; G DefaultClanFinanceModel.cs:133-137): rody AI bez lenn poza krolestwem i najemnicy dostaja Tier x 80 (+40) na dobe
    ///    z niczego. MercContract.TierPostfix zdejmowal to tylko najemnikom AI z umowa 185; teraz (ten sam postfiks, ten sam wpis bez opisu) wszystkim rodom AI
    ///    z warunkiem gry. Gracz bez zmian (gra i tak go pomija). Ksiega obiegu (O40) nie liczy zdjetego "za tier" jako zrodla - MercContract.CancelsTier.
    /// 4. PERKI PO 5 000 DLA AI (TradePerkGiftsAiOff): Insurance Plans (DestroyPartyAction.cs:19-21 - zniszczona karawana wlasciciela z perkiem) i Rapid
    ///    Development (WorkshopsCampaignBehavior.cs:324-326 - miasto warsztatu zdobyte przez wroga) - GiveGoldAction(nic, wlasciciel, 5 000). Platnika nie ma
    ///    (ubezpieczyciela w swiecie nie ma) - dla AI 0 ("nie robimy doplat"). Gracz bez zmian (perk wybrany swiadomie; rozdz. 1c audytu - zrodla tylko gracza).
    ///    Okna: prefiks Low + finalizer High na DestroyPartyAction.ApplyInternal (tylko gdy gra wyplaci - ten sam warunek) i na
    ///    WorkshopsCampaignBehavior.OnSettlementOwnerChanged (tylko miasto); GivePrefix tnie "nic -> wlasciciel" o kwocie premii perka.
    /// Wyjatek per zdarzenie - licznik potkniec, bez globalnego wylacznika. Nic do zapisu (stan doby i kontekstu od zera po wczytaniu).
    /// </summary>
    internal static class SmallGold
    {
        internal static bool RaidOn { get { var s = Settings.Current; return s != null && s.RaidLootFromVillage; } }
        internal static bool GarrisonOn { get { var s = Settings.Current; return s != null && s.GarrisonNoStartPurse; } }
        internal static bool TierOn { get { var s = Settings.Current; return s != null && s.TierIncomeAiOff; } }
        internal static bool PerksOn { get { var s = Settings.Current; return s != null && s.TradePerkGiftsAiOff; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private const int KRaid = 0, KIns = 1, KRapid = 2;

        internal sealed class Ctx
        {
            public int Kind; public Village V; public Hero H; public Settlement S; public float H0; public bool Done, Player, Off;
        }

        private static readonly Ctx[] _stack = new Ctx[4];
        private static int _depth;

        /// <summary>Suma tego, co wsie zaplacily wodzom za lup (od Reset) - okna "lup z niczego" (Measure40 W6, Devastation) odejmuja przyrost w kroku.</summary>
        internal static long RaidPaidSum;

        // liczniki doby
        private static long _rDue, _rPaid, _rDueP, _rPaidP;
        private static long _rOff;
        private static int _rN, _rShortN, _rEmptyN, _rOffN;
        private static long _gTaken, _gOpenGold, _gOffGold;
        private static int _gN, _gOpenN, _gOffN;
        private static long _gHand, _gBack, _gTown;               // recenzja A7: kiesa starej zalogi - nowej zalodze / z powrotem do zalogi osady / do kasy osady
        private static int _gHandN, _gBackN, _gTownN;
        private static Settlement _capSt;                          // okno zdobycia: osada, ktorej stara zaloga oddala kiese
        private static long _capPurse;                             // kiesa starej zalogi czekajaca na nowa zaloge tej osady
        private static long _tCut, _tCutMerc, _tOff;
        private static int _tN, _tOffN;
        private static long _pIns, _pRapid, _pInsP, _pRapidP, _pOff;
        private static int _pInsN, _pRapidN, _pInsPN, _pRapidPN, _pOffN;
        private static int _strays, _overflow, _stumbles;
        private static RaidEventComponent _told;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static string _wiredText = "-", _missingText = "-";
        private static bool _startLogged;

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie) - przed tworzeniem swiata (bilans otwarcia zalog od zera).</summary>
        internal static void Reset()
        {
            Array.Clear(_stack, 0, _stack.Length); _depth = 0;
            RaidPaidSum = 0; _told = null;
            _gOpenGold = 0; _gOpenN = 0; _startLogged = false;
            _capSt = null; _capPurse = 0;
            _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _rDue = _rPaid = _rDueP = _rPaidP = _rOff = 0; _rN = _rShortN = _rEmptyN = _rOffN = 0;
            _gTaken = _gOffGold = 0; _gN = _gOffN = 0;
            _gHand = _gBack = _gTown = 0; _gHandN = _gBackN = _gTownN = 0;
            _tCut = _tCutMerc = _tOff = 0; _tN = _tOffN = 0;
            _pIns = _pRapid = _pInsP = _pRapidP = _pOff = 0; _pInsN = _pRapidN = _pInsPN = _pRapidPN = _pOffN = 0;
            _strays = _overflow = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("SmallGold." + where, e); } catch { }
        }

        private static void Push(Ctx c)
        {
            if (_depth >= _stack.Length) { _overflow++; return; }
            _stack[_depth++] = c;
        }

        /// <summary>Finalizer okien (High): zdejmuje kontekst tego wywolania (zawsze - takze po wyjatku).</summary>
        public static void CtxFin(Ctx __state)
        {
            if (__state == null) return;
            try
            {
                if (_depth > 0 && ReferenceEquals(_stack[_depth - 1], __state)) { _stack[--_depth] = null; return; }
                for (int i = _depth - 1; i >= 0; i--)
                    if (ReferenceEquals(_stack[i], __state)) { for (int j = i; j < _depth - 1; j++) _stack[j] = _stack[j + 1]; _stack[--_depth] = null; _strays++; return; }
            }
            catch (Exception e) { Stumble("CtxFin", e); }
        }

        // ------------------------------------------------------------ 1. rabunek wsi: okno RaidEventComponent.Update(ref bool finish)
        public static void RaidPre(RaidEventComponent __instance, out Ctx __state)
        {
            __state = null;
            try
            {
                if (__instance == null || !Live) return;
                if (DistrictRaid.Owns(__instance)) return;                      // E7/R1 (v2): rabunek osada po osadzie placi sam (PayRaidLoot - ten sam platnik), oryginal nie biegnie
                var def = __instance.DefenderSide;
                if (def == null || def.TroopCount > 0) return;                  // poza faza lupienia gra nie placi (R:162) - bez alokacji co klatke
                var st = __instance.MapEventSettlement;
                var v = st != null ? st.Village : null;
                if (v == null) return;
                var att = __instance.AttackerSide;
                var lp = att != null ? att.LeaderParty : null;
                var leader = lp != null ? lp.LeaderHero : null;
                if (leader == null) return;                                     // bez wodza gra nie placi (R:177)
                // przy zdjetym wylaczniku okno tez sie otwiera - tylko licznik "z niczego jak dotad" (bez zamiany strony)
                __state = new Ctx { Kind = KRaid, V = v, H = leader, H0 = v.Hearth, Player = leader == Hero.MainHero, Off = !RaidOn };
                Push(__state);
            }
            catch (Exception e) { __state = null; Stumble("RaidPre", e); }
        }

        // ------------------------------------------------------------ 2. nowa zaloga: GarrisonPartyComponent.OnMobilePartySetOnCreation
        public static void GarrisonPre(GarrisonPartyComponent __instance, out int __state)
        {
            __state = -1;
            try
            {
                var mp = __instance != null ? __instance.MobileParty : null;
                if (mp == null) return;
                __state = Math.Max(0, mp.PartyTradeGold);                      // nowa partia: 0; partia zamieniana w zaloge - jej dotychczasowa kiesa
            }
            catch (Exception e) { __state = -1; Stumble("GarrisonPre", e); }
        }

        public static void GarrisonPost(GarrisonPartyComponent __instance, int __state)
        {
            try
            {
                if (__state < 0) return;
                var mp = __instance != null ? __instance.MobileParty : null;
                if (mp == null) return;
                int now = mp.PartyTradeGold;
                long made = (long)now - __state;                                // tyle InitializePartyTrade dopisal z niczego (5 000 u nowej)
                if (made > 0)
                {
                    if (!Live) { _gOpenN++; _gOpenGold += made; return; }      // tworzenie swiata - regula gry (bilans otwarcia)
                    if (!GarrisonOn) { _gOffN++; _gOffGold += made; return; }
                    mp.PartyTradeGold = __state;                                // kiesa jak przed - zloto nie powstaje; kiese wypelnia rod rozliczeniem zoldu
                    _gN++; _gTaken += made;
                }
                TakeOver(__instance, mp);                                       // recenzja A7: nowa zaloga zdobytej osady przejmuje kiese starej (przelew)
            }
            catch (Exception e) { Stumble("GarrisonPost", e); }
        }

        /// <summary>Nowa zaloga osady z otwartego okna zdobycia przejmuje kiese starej zalogi (zdjeta w CapturePre) - przelew miedzy zalogami tej samej osady.</summary>
        private static void TakeOver(GarrisonPartyComponent gc, MobileParty mp)
        {
            if (_capSt == null || _capPurse <= 0 || gc == null || mp == null || !ReferenceEquals(gc.Settlement, _capSt)) return;
            long p = _capPurse;
            _capPurse = 0; _capSt = null;
            mp.PartyTradeGold = (int)Math.Min(int.MaxValue, (long)Math.Max(0, mp.PartyTradeGold) + p);
            _gHand += p; _gHandN++;
        }

        // ------------------------------------------------------------ 2a. stara zaloga przy zdobyciu: okno ChangeOwnerOfSettlementAction.ApplyInternal
        /// <summary>Prefiks (Low): przy zdobyciu (BySiege) gra niszczy stara zaloge (DestroyPartyAction - jej kiesa znikala z partia, U8) i od razu tworzy nowa
        /// (AddGarrisonParty). Kiesa starej zalogi zdjeta tu, przed zniszczeniem - przejmie ja nowa zaloga tej osady (TakeOver w GarrisonPost).</summary>
        public static void CapturePre(Settlement __0, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail __3, out bool __state)
        {
            __state = false;
            try
            {
                if (!Live || !GarrisonOn || __0 == null || !__0.IsFortification || __0.Town == null) return;
                if (__3 != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;   // tylko wtedy gra niszczy zaloge (:33-36)
                var g = __0.Town.GarrisonParty;
                if (g == null || !g.IsActive) return;
                if (_capSt != null) Flush();                                    // niedokonczone okno (nie powinno sie zdarzyc) - najpierw oddac tamta kiese
                int purse = Math.Max(0, g.PartyTradeGold);
                _capSt = __0; _capPurse = purse; __state = true;
                if (purse > 0) g.PartyTradeGold = 0;                            // zloto czeka na nowa zaloge - nie znika z niszczona partia
            }
            catch (Exception e) { __state = false; Stumble("CapturePre", e); }
        }

        /// <summary>Finalizer (High): kiesa, ktorej nowa zaloga nie przejela (gra nie zniszczyla starej, wyjatek) - do obecnej zalogi osady, bez zalogi - do kasy osady.</summary>
        public static void CaptureFin(bool __state)
        {
            if (!__state) return;
            try { Flush(); }
            catch (Exception e) { Stumble("CaptureFin", e); }
        }

        private static void Flush()
        {
            var st = _capSt; long p = _capPurse;
            _capSt = null; _capPurse = 0;
            if (st == null || p <= 0) return;
            var g = st.Town != null ? st.Town.GarrisonParty : null;
            if (g != null && g.IsActive) { g.PartyTradeGold = (int)Math.Min(int.MaxValue, (long)Math.Max(0, g.PartyTradeGold) + p); _gBack += p; _gBackN++; return; }
            if (st.SettlementComponent != null) { st.SettlementComponent.ChangeGold((int)Math.Min(int.MaxValue, p)); _gTown += p; _gTownN++; }
        }

        // ------------------------------------------------------------ 3. "za tier" - wolane z MercContract.TierPostfix (przy rozliczeniu, applyWithdrawals)
        /// <summary>Czy "za tier" tego rodu zdejmuje E5/A7 (warunek gry: rod AI bez lenn poza krolestwem albo najemnik) - ten sam warunek w TierPostfix i CancelsTier.</summary>
        internal static bool CutsTier(Clan c)
        {
            try
            {
                if (!TierOn || c == null || c == Clan.PlayerClan || c.IsEliminated) return false;
                var mf = c.MapFaction;
                return mf != null && (!mf.IsKingdomFaction || c.IsUnderMercenaryService) && c.Fiefs != null && c.Fiefs.Count == 0;
            }
            catch { return false; }
        }

        internal static void NoteTier(long x, bool merc) { _tCut += x; _tN++; if (merc) _tCutMerc += x; }

        /// <summary>Wylacznik zdjety: "za tier" rodu AI, ktory E5/A7 by zdjal, idzie z niczego jak dotad (licznik - wolane z TierPostfix).</summary>
        internal static void NoteTierOff(Clan c)
        {
            try
            {
                if (TierOn || c == null || c == Clan.PlayerClan || c.IsEliminated) return;
                var mf = c.MapFaction;
                if (mf == null || !(!mf.IsKingdomFaction || c.IsUnderMercenaryService) || c.Fiefs == null || c.Fiefs.Count != 0) return;
                long x = (long)c.Tier * (80 + (c.IsUnderMercenaryService ? 40 : 0));
                if (x > 0) { _tOff += x; _tOffN++; }
            }
            catch { }
        }

        // ------------------------------------------------------------ 4. perki 5 000: okna DestroyPartyAction.ApplyInternal i OnSettlementOwnerChanged (warsztaty)
        public static void InsPre(MobileParty __1, out Ctx __state)
        {
            __state = null;
            try
            {
                if (__1 == null || !Live || !__1.IsCaravan || __1 == MobileParty.MainParty) return;
                var owner = __1.Party != null ? __1.Party.Owner : null;
                if (owner == null || !owner.GetPerkValue(DefaultPerks.Trade.InsurancePlans)) return;   // ten sam warunek co gra (DestroyPartyAction.cs:19)
                __state = new Ctx { Kind = KIns, H = owner, Player = owner == Hero.MainHero };
                Push(__state);
            }
            catch (Exception e) { __state = null; Stumble("InsPre", e); }
        }

        public static void RapidPre(Settlement __0, out Ctx __state)
        {
            __state = null;
            try
            {
                if (__0 == null || !Live || !__0.IsTown || __0.Town == null) return;
                __state = new Ctx { Kind = KRapid, S = __0 };
                Push(__state);
            }
            catch (Exception e) { __state = null; Stumble("RapidPre", e); }
        }

        private static bool OwnsWorkshopIn(Settlement s, Hero h)
        {
            var ws = s != null && s.Town != null ? s.Town.Workshops : null;
            if (ws == null || h == null) return false;
            foreach (var w in ws) if (w != null && ReferenceEquals(w.Owner, h)) return true;
            return false;
        }

        // ------------------------------------------------------------ wspolny prefiks na GiveGoldAction.ApplyInternal (Low)
        public static void GivePrefix(Hero __0, ref PartyBase __1, Hero __2, PartyBase __3, ref int __4)
        {
            if (_depth == 0) return;
            try
            {
                var c = _stack[_depth - 1];
                if (c == null || c.Done || __4 <= 0) return;
                if (__0 != null || __1 != null || __3 != null || __2 == null) return;   // tylko "nic -> bohater"
                if (c.Kind == KRaid)
                {
                    if (!ReferenceEquals(__2, c.H) || c.V == null) { _strays++; return; }
                    if (!(c.V.Hearth < c.H0)) { _strays++; return; }            // przed spadkiem hearth to nie wyplata lupu (R:175 -> R:188)
                    c.Done = true;
                    if (c.Off) { _rOffN++; _rOff += __4; return; }               // wylacznik zdjety - lup z niczego jak dotad (licznik)
                    long due = __4, pay;
                    PartyBase party;
                    if (!LootPayer(c.V, due, out party, out pay)) { _strays++; return; }   // bez strony wsi - jak w grze (licznik)
                    __1 = party;                                                 // dajacy = kiesa wsi (gra obcina do jej stanu - GiveGoldAction.cs:21-25)
                    __4 = (int)pay;
                    _rN++; _rDue += due; _rPaid += pay;
                    if (c.Player) { _rDueP += due; _rPaidP += pay; }
                    if (pay < due) { _rShortN++; if (pay == 0) _rEmptyN++; }
                    if (c.Player && pay < due) TellPlayer(pay, due);
                    return;
                }
                bool perk;
                int bonus;
                if (c.Kind == KIns)
                {
                    bonus = (int)DefaultPerks.Trade.InsurancePlans.PrimaryBonus;                         // gra: (int)PrimaryBonus (DestroyPartyAction.cs:21)
                    perk = ReferenceEquals(__2, c.H) && __4 == bonus;
                    if (perk) c.Done = true;                                                             // jedna wyplata na zniszczona karawane
                }
                else
                {
                    bonus = TaleWorlds.Library.MathF.Round(DefaultPerks.Trade.RapidDevelopment.PrimaryBonus);   // gra: MathF.Round(PrimaryBonus) (:326)
                    perk = __4 == bonus && OwnsWorkshopIn(c.S, __2) && __2.GetPerkValue(DefaultPerks.Trade.RapidDevelopment);
                }
                if (!perk) return;                                               // inne zdarzenia w oknie perka (np. zwrot ceny warsztatu gracza - ChangeOwnerOfWorkshopAction) nie sa nasze
                bool player = __2 == Hero.MainHero;
                if (player)
                {
                    if (c.Kind == KIns) { _pInsPN++; _pInsP += __4; } else { _pRapidPN++; _pRapidP += __4; }   // gracz bez zmian
                    return;
                }
                if (!PerksOn) { _pOffN++; _pOff += __4; return; }
                if (c.Kind == KIns) { _pInsN++; _pIns += __4; } else { _pRapidN++; _pRapid += __4; }
                __4 = 0;                                                         // uciete - zloto nie powstaje (zdarzenie z 0 ksiega pomija)
            }
            catch (Exception e) { Stumble("GivePrefix", e); }
        }

        /// <summary>
        /// JEDEN PLATNIK LUPU Z RABUNKU (E5/A7 i E7/R1, R15): kiesa wsi (Settlement.Party wsi), najwyzej tyle, ile w niej jest - pusta wies nie daje nic.
        /// Dopisuje zaplacone do RaidPaidSum (okna "lup z niczego" odejmuja przelew). false - wies bez strony (jak w grze: brak platnika).
        /// </summary>
        internal static bool LootPayer(Village v, long due, out PartyBase payer, out long pay)
        {
            payer = v != null && v.Settlement != null ? v.Settlement.Party : null;
            pay = 0;
            if (payer == null) return false;
            pay = Math.Max(0, Math.Min(due, Math.Max(0, (long)v.Gold)));
            RaidPaidSum += pay;
            return true;
        }

        /// <summary>
        /// E7/R1 (235): lup zlota rabunku osada po osadzie - ta sama funkcja platnika (LootPayer), przelew kiesa wsi -> wodz napastnika (GiveGoldAction ze strona wsi,
        /// ksiega pieniadza widzi platnika i odbiorce; GivePrefix go nie rusza - dajacy nie jest "nic"). Zwraca zaplacone.
        /// </summary>
        internal static long PayRaidLoot(Village v, Hero leader, long due)
        {
            if (v == null || leader == null || due <= 0) return 0;
            PartyBase payer; long pay;
            if (!LootPayer(v, due, out payer, out pay) || pay <= 0) return 0;
            GiveGoldAction.ApplyForPartyToCharacter(payer, leader, (int)Math.Min(int.MaxValue, pay), true);
            return pay;
        }

        private static void TellPlayer(long pay, long due)
        {
            try
            {
                var me = Hero.MainHero;
                var ev = me != null && me.PartyBelongedTo != null ? me.PartyBelongedTo.MapEvent : null;
                var comp = ev != null ? ev.Component as RaidEventComponent : null;
                if (comp != null && ReferenceEquals(comp, _told)) return;      // raz na rabunek
                _told = comp;
                Log.Player("The village purse is running dry: this time you took " + pay + " of " + due + " denars, and from now on only what is left in it.", true);
            }
            catch (Exception e) { Stumble("TellPlayer", e); }
        }

        // ------------------------------------------------------------ wpiecia (SubModuleMain - po NotableGold)
        private static void Patch(Harmony h, string label, MethodBase m, string pre, string post, string fin, int prePrio)
        {
            try
            {
                if (h == null) { _missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { _missing.Add(label); return; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(SmallGold), pre) { priority = prePrio } : null,
                           postfix: post != null ? new HarmonyMethod(typeof(SmallGold), post) { priority = Priority.Normal } : null,
                           finalizer: fin != null ? new HarmonyMethod(typeof(SmallGold), fin) { priority = Priority.High } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear();
            // rabunek: prefiks Low - po prefiksach W6 (First) i E4/5 Devastation (Normal), ktory podmienia hearth na hearth gry
            Patch(h, "rabunek wsi (RaidEventComponent.Update)", AccessTools.Method(typeof(RaidEventComponent), "Update", new[] { typeof(bool).MakeByRefType() }),
                  nameof(RaidPre), null, nameof(CtxFin), Priority.Low);
            var gar = AccessTools.Method(typeof(GarrisonPartyComponent), "OnMobilePartySetOnCreation");
            Patch(h, "nowa zaloga (GarrisonPartyComponent.OnMobilePartySetOnCreation)", gar != null && gar.DeclaringType == typeof(GarrisonPartyComponent) ? gar : null,
                  nameof(GarrisonPre), nameof(GarrisonPost), null, Priority.Normal);
            Patch(h, "kiesa starej zalogi (ChangeOwnerOfSettlementAction.ApplyInternal)", AccessTools.Method(typeof(ChangeOwnerOfSettlementAction), "ApplyInternal"),
                  nameof(CapturePre), null, nameof(CaptureFin), Priority.Low);
            Patch(h, "Insurance Plans (DestroyPartyAction.ApplyInternal)", AccessTools.Method(typeof(DestroyPartyAction), "ApplyInternal"),
                  nameof(InsPre), null, nameof(CtxFin), Priority.Low);
            Patch(h, "Rapid Development (WorkshopsCampaignBehavior.OnSettlementOwnerChanged)", AccessTools.Method(typeof(WorkshopsCampaignBehavior), "OnSettlementOwnerChanged"),
                  nameof(RapidPre), null, nameof(CtxFin), Priority.Low);
            Patch(h, "strona przelewu (GiveGoldAction.ApplyInternal)", AccessTools.Method(typeof(GiveGoldAction), "ApplyInternal"), nameof(GivePrefix), null, null, Priority.Low);
            _wiredText = _wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-";
            _missingText = _missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-";
            Log.Info("Drobne zrodla (E5/A7): latki przy starcie gry - wpiete: " + _wiredText + "; BRAK: " + _missingText
                     + "; \"za tier\" - przez MercContract.TierPostfix (CalculateClanIncomeInternal, w kampanii).");
        }

        // ------------------------------------------------------------ linia doby (ArmouryBehavior - po MoneyLedger.Daily)
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (Campaign.Current == null || s == null || !s.LogEnabled) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info("Drobne zrodla (E5/A7): start | wylaczniki: Raid Loot From Village " + (RaidOn ? "tak" : "nie") + ", Garrison No Start Purse " + (GarrisonOn ? "tak" : "nie")
                             + ", Tier Income Ai Off " + (TierOn ? "tak" : "nie") + ", Trade Perk Gifts Ai Off " + (PerksOn ? "tak" : "nie") + " | latki: " + _wiredText + "; BRAK: " + _missingText
                             + " | bilans otwarcia (tworzenie swiata - regula gry): " + (_gOpenN > 0 ? "zalogi " + _gOpenN + " (kiesa startowa gry " + _gOpenGold + ")" : "brak w tej sesji (wczytany zapis - zalogi juz istnieja)") + ".");
                }
                var sb = new StringBuilder(900);
                sb.Append("Drobne zrodla (E5/A7): dzien ").Append(day);
                sb.Append(" | rabunek wsi z kiesy wsi").Append(RaidOn ? "" : " WYLACZONE (Raid Loot From Village - lup z niczego jak dotad)")
                  .Append(": gra dalaby ").Append(_rDue).Append(" w ").Append(_rN).Append(" krokach, z kies wsi ").Append(_rPaid).Append(", uciete ").Append(_rDue - _rPaid)
                  .Append(" (kiesa wsi nie starczyla w ").Append(_rShortN).Append(" krokach, pusta w ").Append(_rEmptyN).Append("); gracz: gra dalaby ").Append(_rDueP).Append(", z kies wsi ").Append(_rPaidP);
                if (_rOffN > 0) sb.Append("; z niczego jak dotad ").Append(_rOff).Append(" w ").Append(_rOffN).Append(" krokach");
                sb.Append(" | nowe zalogi").Append(GarrisonOn ? "" : " WYLACZONE (Garrison No Start Purse)").Append(": ").Append(_gN).Append(", kiesa startowa gry ").Append(_gTaken)
                  .Append(" -> 0 (po zdobyciu kiesa starej zalogi, dalej kiese wypelnia rod rozliczeniem zoldu)");
                if (_gOffN > 0) sb.Append(", z niczego jak dotad ").Append(_gOffGold).Append(" u ").Append(_gOffN);
                sb.Append("; kiesa starej zalogi przy zdobyciu: nowej zalodze ").Append(_gHand).Append(" (").Append(_gHandN).Append("), z powrotem do zalogi osady ").Append(_gBack)
                  .Append(" (").Append(_gBackN).Append(')');
                sb.Append(" | za tier rody AI bez lenn").Append(TierOn ? "" : " WYLACZONE (Tier Income Ai Off)").Append(": gra dalaby ").Append(_tCut).Append(" u ").Append(_tN)
                  .Append(" rodow (w tym najemnicy ").Append(_tCutMerc).Append("), dostaly 0; poza tym 185 zdejmuje najemnikom z umowa (linia Kontrakty najemnikow)");
                if (_tOffN > 0) sb.Append(", z niczego jak dotad ").Append(_tOff).Append(" u ").Append(_tOffN);
                sb.Append(" | perki 5000 AI").Append(PerksOn ? "" : " WYLACZONE (Trade Perk Gifts Ai Off)").Append(": Insurance Plans uciete ").Append(_pIns).Append(" (").Append(_pInsN)
                  .Append("), Rapid Development uciete ").Append(_pRapid).Append(" (").Append(_pRapidN).Append(')');
                if (_pOffN > 0) sb.Append(", z niczego jak dotad ").Append(_pOff).Append(" (").Append(_pOffN).Append(')');
                sb.Append("; gracz bez zmian: Insurance Plans ").Append(_pInsP).Append(" (").Append(_pInsPN).Append("), Rapid Development ").Append(_pRapidP).Append(" (").Append(_pRapidPN).Append(')');
                sb.Append(" | kontrola (prog 0): zdarzenia w oknie rabunku niedopasowane ").Append(_strays).Append(", stos przepelniony ").Append(_overflow)
                  .Append(", kiesa starej zalogi bez zalogi - do kasy osady ").Append(_gTown).Append(" (").Append(_gTownN).Append(')');
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }
    }
}
