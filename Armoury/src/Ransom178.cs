using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PACZKA 178 - OKUPY WEDLUG MAJATKU, WIELCY JENCY I PRAWO TRZECICH (projekt etapu 2, krok D; PLAN 2.13, 2.14; [D] 00:00 okupy wedlug majatku, 03:45 nr 13,
    /// 17, 18, 04:55 Q3b, 10:50 wielcy jency; odpowiedzi Jeffa 11:05: pulap rok D, okup krola ze skarbca). Wylacznik LordRansomByIncome (z drabina dlugu 168 -
    /// raty okupow w tej samej ksiedze), CrownGreatCaptives, CrownThirds.
    ///  - CENA (wszystkie drogi): glowa rodu RansomHeadYears (0.5) x D x 364, kazdy inny lord RansomLordDays (60) x D; krol - skarbiec jego krolestwa (0.5 x wplywy dnia
    ///    korony x 364, srednia 28 dob). Bez minimow, bez wzrostu za czas niewoli; szansa zgody 10% dziennie (AI-AI).
    ///  - ZAPLATA: gotowka najwyzej RansomCashShare x (G - 5 000) z kies rodziny jenca; reszta - dlug okupu w ksiedze 168 (wierzyciel porywacz; raty w limicie 15% D,
    ///    kolejka bez blokowania), pulap dlugow okupow rodu 364 D (okup ponad pulap mniejszy). Krol: gotowka z nadwyzki skarbca ponad rezerwe, reszta - dlug korony
    ///    splacany ratami z wplywow dnia (jak reparacje 165). Jeniec wolny po gotowce.
    ///  - AI-AI: wlasny przeplyw zamiast barteru gry (prefiks ConsiderRansomPrisoner): rodzina jenca -> glowa rodu porywacza; kurier (jeniec u gracza albo gracz placi):
    ///    ten sam przeplyw bez dosypki (AcceptRansomOffer); posrednik i ekran druzyny: gra placi graczowi tylko gotowke (cena RC = gotowka), rod jenca oddaje ja w nicosc
    ///    (rownowazy zloto gry z niczego), reszta - dlug wobec gracza; okup gracza: gotowka do porywacza (2.14), reszta - dlug gracza wobec porywacza.
    ///  - WIELCY JENCY: krol albo nastepca tronu (pierwszy w kolejce dziedziczenia rodu krola) pojmany przez rod krolestwa - przy pojmaniu jeniec przechodzi na korone
    ///    zdobywcy (caly okup do jej skarbca), zdobywca dostaje nagrode CrownGreatCaptiveReward (1/10) okupu ze skarbca (wplywy dnia, potem zapas ponad rezerwe; reszta
    ///    z pierwszych wplat). Zdobywca bez krolestwa - okup dla niego.
    ///  - PRAWO TRZECICH: korona krolestwa w wojnie (placi zwrot - bez najemnikow) bierze raz na dobe 1/9 okupow otrzymanych przez swoje rody (gotowka i raty; nie
    ///    wielcy jency), 1/3 trzeciej lorda AI ze sprzedazy nadwyzek ludzi i sakw (KThird), 1/9 sprzedazy lupu lorda AI przez gre (PartiesSellLoot), od gracza-wasala 1/9
    ///    sakw zdobytych przez jego ludzi (z ich sakiewki) i 1/9 wartosci lupu z ekranu po bitwie (z kiesy). Z kiesy odbiorcy; czego brak - jutro.
    ///    3.2: takze 1/9 zlota z kies rozbitych partii bez wodza (tabor wsi, banda, karawana), ktore wzial wodz-wasal albo gracz (NoteBeatenGold - BeatenPurse, VillageTakings),
    ///    i z kiesy karawany albo taboru wsi oddanej graczowi w rozmowie bez bitwy (poddanie, niewola, okup za przepuszczenie - BeatenPurse pkt 5, recenzja 3.2).
    /// Platnik -> odbiorca: rodzina jenca -> glowa rodu porywacza / skarbiec korony zdobywcy; skarbiec krola -> porywacz; skarbiec zdobywcy -> zdobywca (nagroda);
    /// odbiorcy okupow i lupu -> skarbiec (1/9). Zadnego zlota z niczego (posrednik - zloto gry zrownowazone). Zapis: "arm_rans178".
    /// </summary>
    internal static class Ransom178
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.LordRansomByIncome && DebtLadder.On; } }
        private static bool GreatOn { get { var s = Settings.Current; return On && s.CrownGreatCaptives; } }
        private static bool ThirdsOn { get { var s = Settings.Current; return s != null && s.CrownThirds && CrownIncome.On; } }

        private sealed class Acc { public double Third, Ninth, Purse; public long Carry; }
        private sealed class Reward { public string Hero = "", Kingdom = "", Captive = ""; public long Left; }   // 367c: Captive - nagroda za pojmanie tego jenca ("" - bez przypisania)
        private sealed class Ema { public double V; public int N; }

        private static readonly Dictionary<string, Acc> _acc = new Dictionary<string, Acc>();          // id bohatera -> podstawa 1/9 doby i zaleglosc
        private static readonly Dictionary<string, string> _crownCaptive = new Dictionary<string, string>(); // id jenca -> id korony zdobywcy
        private static readonly List<Reward> _rewards = new List<Reward>();
        private static readonly Dictionary<string, long> _paidFor = new Dictionary<string, long>();   // 367c: id jenca korony -> wyplacona juz nagroda za jego pojmanie (log zdjecia)
        private static int _dRevokedN; private static long _dRevokedKept, _dRevokedVoid;   // 367c: licznik doby - jency zdjeci z koron (nagroda zostaje / anulowana)

        /// <summary>272 (unia Dorzecza z Polnoca): wielcy jency korony `fromId` (okup do jej skarbca) i nagrody, ktore ta korona jest winna zdobywcom, przechodza na
        /// korone `toId` razem z jej skarbcem - inaczej po upadku krolestwa nagroda przepada, a okup wielkiego jenca idzie do porywacza. Nie rzuca.</summary>
        internal static void MoveKingdom(string fromId, string toId, out int captives, out int rewards)
        {
            captives = 0; rewards = 0;
            try
            {
                // 367c: unia 332 bez zniszczenia (czesc rodow zostaje) - jeniec trzymany przez rod, ktory ZOSTAL w starej koronie, zostaje jej jencem
                // (razem z przypisana do niego nagroda); inaczej najblizszy CrownCheck zdjalby go z nowej korony, choc trzymajacy dalej sluzy starej
                var stay = new HashSet<string>();
                foreach (var k in new List<string>(_crownCaptive.Keys))
                {
                    if (_crownCaptive[k] != fromId) continue;
                    try
                    {
                        Hero h = Hero.Find(k);
                        var holder = h != null ? HolderClan(h) : null;
                        if (holder != null && !holder.IsEliminated && holder.Kingdom != null && holder.Kingdom.StringId == fromId) { stay.Add(k); continue; }
                    }
                    catch (Exception e) { Stumble("MoveKingdom.Holder", e); }
                    _crownCaptive[k] = toId; captives++;
                }
                foreach (var r in _rewards) if (r != null && r.Kingdom == fromId && !stay.Contains(r.Captive ?? "")) { r.Kingdom = toId; rewards++; }
            }
            catch (Exception e) { Stumble("MoveKingdom", e); }
        }
        private static readonly Dictionary<string, Ema> _crownIncome = new Dictionary<string, Ema>();   // id krolestwa -> srednia wplywow dnia (28 dob)
        private static readonly Dictionary<Kingdom, long> _thirdsToday = new Dictionary<Kingdom, long>();
        private static readonly Dictionary<Kingdom, Hero> _heirCache = new Dictionary<Kingdom, Hero>();
        private static int _heirDay = -1, _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // liczniki doby (linia "Okupy (178)")
        private static int _dAiN, _dCourierN, _dBrokerN, _dPlayerN, _dZeroN, _dBlockedN, _dCapN, _dKingN, _dGreatTakenN, _dDungeonN, _dBrokerShortN, _dRewardOwnKing;
        private static long _dPrice, _dCash, _dDebt, _dCapCut, _dKingCash, _dKingDebt, _dRewardQueued, _dRewardPaid, _dBaseThird, _dBaseLoot, _dBaseRansom, _dBasePurse,
                            _dBaseScreen, _dThirdsGot, _dThirdsPurse, _dThirdsCarry, _dToCrown, _dBrokerShort, _dBaseGold;
        private static string _dMax;
        private static long _dMaxV;
        internal static long LastThirds, LastRewards, LastRansomCash, LastToCrown;

        internal static void Reset()
        {
            _acc.Clear(); _crownCaptive.Clear(); _rewards.Clear(); _paidFor.Clear(); _crownIncome.Clear(); _thirdsToday.Clear(); _heirCache.Clear(); _heirDay = -1;
            _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0; ZeroDay(); LastThirds = LastRewards = LastRansomCash = LastToCrown = 0;
        }

        private static void ZeroDay()
        {
            _dAiN = _dCourierN = _dBrokerN = _dPlayerN = _dZeroN = _dBlockedN = _dCapN = _dKingN = _dGreatTakenN = _dDungeonN = _dBrokerShortN = _dRewardOwnKing = _dRevokedN = 0;
            _dRevokedKept = _dRevokedVoid = 0;
            _dPrice = _dCash = _dDebt = _dCapCut = _dKingCash = _dKingDebt = _dRewardQueued = _dRewardPaid = _dBaseThird = _dBaseLoot = _dBaseRansom = _dBasePurse = 0;
            _dBaseScreen = _dThirdsGot = _dThirdsPurse = _dThirdsCarry = _dToCrown = _dBrokerShort = _dBaseGold = 0;
            _dMax = null; _dMaxV = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("Ransom178." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static bool AtWar(Kingdom k)
        {
            try
            {
                if (k == null) return false;
                var list = k.FactionsAtWarWith;
                if (list != null) for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && o.IsKingdomFaction) return true; }
            }
            catch { }
            return false;
        }

        private static bool Lord(Hero h)
        {
            return h != null && h.IsLord && h.IsAlive && h != Hero.MainHero && h.Clan != null && !h.Clan.IsBanditFaction && !ClanIncomeBook.IsUndeadClan(h.Clan);
        }

        // ------------------------------------------------------------ cena
        /// <summary>Okup wedlug majatku; byCrown - krol, placi skarbiec (cena z wplywow korony); D - dochod, od ktorego liczono.</summary>
        internal static long PriceOf(Hero captive, out bool byCrown, out double D)
        {
            var s = Settings.Current;
            byCrown = false; D = 0;
            if (captive == null || captive.Clan == null) return 0;
            var k = captive.MapFaction as Kingdom;
            double head = Math.Max(0f, s.RansomHeadYears) * 364.0;
            if (s.RansomKingFromTreasury && k != null && !k.IsEliminated && k.Leader == captive)
            {
                byCrown = true;
                D = CrownAvg(k);
                // M6a-2: okup powtorny w oknie RansomRepeatDays tanszy (RansomRepeat.Factor; klucz wylaczony - mnoznik 1, cena jak dotad)
                return (long)(Math.Max(0f, s.RansomKingDays) * D * RansomRepeat.Factor(captive));   // Jeff 10.10 (wariant 1): krol 20 dni wplywow korony, osobno od glowy rodu
            }
            D = DebtLadder.DebtD(captive.Clan);
            double w = captive.Clan.Leader == captive ? head : Math.Max(0f, s.RansomLordDays);
            return (long)(w * D * RansomRepeat.Factor(captive));   // M6a-2: jw.
        }

        private static double CrownAvg(Kingdom k)
        {
            Ema e;
            if (k != null && k.StringId != null && _crownIncome.TryGetValue(k.StringId, out e)) return Math.Max(0, e.V);
            var d = CrownIncome.DayOf(k);
            return d != null ? Math.Max(0, d.Measured) : 0;
        }

        /// <summary>Gotowka, ktora rod jenca zaplaci od razu: najwyzej RansomCashShare x (kiesy rodziny - 5 000).</summary>
        private static long CashCap(Clan c, long price)
        {
            var s = Settings.Current;
            long G = ClanIncomeBook.FamilyGoldOf(c);
            return Math.Max(0, Math.Min(price, (long)(Math.Max(0f, Math.Min(1f, s.RansomCashShare)) * Math.Max(0, G - Math.Max(0, s.FamilyPurseFloor)))));
        }

        private static long CrownCash(Kingdom k, long price)
        {
            var s = Settings.Current;
            return Math.Max(0, Math.Min(price, (long)(Math.Max(0f, Math.Min(1f, s.RansomCashShare)) * Math.Max(0, (long)k.KingdomBudgetWallet - Math.Max(0, s.CrownReserveGold)))));
        }

        /// <summary>Gotowka z kies rodziny: glowa, potem dorosli czlonkowie - kazdy najwyzej do podlogi 5 000 (FamilyPurseFloor). Zwraca zebrane (zdjete).</summary>
        private static long Collect(Clan c, long want)
        {
            var s = Settings.Current;
            long floor = Math.Max(0, s.FamilyPurseFloor), got = 0;
            if (c == null || want <= 0) return 0;
            var heads = new List<Hero>();
            if (c.Leader != null) heads.Add(c.Leader);
            foreach (var h in c.Heroes) if (h != null && h != c.Leader && h.IsAlive && !h.IsChild && h.Clan == c && (h.IsLord || h == Hero.MainHero)) heads.Add(h);
            foreach (var h in heads)
            {
                if (got >= want) break;
                long x = Math.Min(want - got, Math.Max(0, (long)h.Gold - floor));
                if (x <= 0) continue;
                h.ChangeHeroGold(-(int)x);
                CirculationWindows.NoteHeroGold(h, -x);
                got += x;
            }
            return got;
        }

        // ------------------------------------------------------------ wielcy jency
        private static Hero HeirOf(Kingdom k)
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (today != _heirDay) { _heirCache.Clear(); _heirDay = today; }
            Hero h;
            if (_heirCache.TryGetValue(k, out h)) return h;
            h = null;
            try
            {
                var rc = k.RulingClan;
                if (rc != null)
                {
                    var ap = rc.GetHeirApparents();
                    if (ap != null && ap.Count > 0) h = ap.OrderByDescending(x => x.Value).ThenByDescending(x => x.Key.Age).First().Key;
                }
            }
            catch (Exception e) { Stumble("HeirOf", e); }
            _heirCache[k] = h;
            return h;
        }

        private static bool IsGreat(Hero h)
        {
            var k = h != null ? h.MapFaction as Kingdom : null;
            if (k == null || k.IsEliminated) return false;
            if (k.Leader == h) return true;
            return h.Clan != null && h.Clan == k.RulingClan && HeirOf(k) == h;
        }

        /// <summary>223 (eskorta lordow): krol albo nastepca tronu bez wzgledu na klucz wielkich jencow - ta sama regula rangi. Watek glowny (pamiec nastepcy).</summary>
        internal static bool IsRulerOrHeir(Hero h) { try { return h != null && IsGreat(h); } catch { return false; } }

        /// <summary>Recenzja D (harness 2.14/178): krol albo nastepca tronu przy czynnych wielkich jencach.</summary>
        internal static bool IsGreatCaptive(Hero h) { try { return GreatOn && h != null && IsGreat(h); } catch { return false; } }

        internal static Clan CaptorClan(PartyBase p)   // 367d: uzywane tez przez CaptiveGrudge.OnReleased
        {
            if (p == null) return null;
            if (p.IsSettlement && p.Settlement != null) return p.Settlement.OwnerClan;
            var mp = p.MobileParty;
            if (mp == null) return null;
            if ((mp.IsGarrison || mp.IsMilitia || mp.IsCaravan || mp.IsVillager) && p.Owner != null) return p.Owner.IsNotable && p.Owner.CurrentSettlement != null ? p.Owner.CurrentSettlement.OwnerClan : p.Owner.Clan;
            if (mp.IsPatrolParty && mp.HomeSettlement != null) return mp.HomeSettlement.OwnerClan;
            return mp.ActualClan;
        }

        /// <summary>Zdarzenie gry: pojmanie - krol albo nastepca tronu przechodzi na korone zdobywcy, nagroda 1/10 okupu dla zdobywcy (do wyplaty w kroku korony).</summary>
        private static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (!GreatOn || prisoner == null || !Lord(prisoner) || !IsGreat(prisoner)) return;
                var cc = CaptorClan(capturer);
                var k = cc != null ? cc.Kingdom : null;
                if (k == null || k.IsEliminated || k == prisoner.MapFaction) return;
                var captor = capturer != null && capturer.LeaderHero != null && capturer.LeaderHero.IsAlive ? capturer.LeaderHero : cc.Leader;
                _crownCaptive[prisoner.StringId] = k.StringId;
                bool byCrown; double D;
                long price = PriceOf(prisoner, out byCrown, out D);
                long reward = (long)(Math.Max(0f, Math.Min(1f, Settings.Current.CrownGreatCaptiveReward)) * price);
                // m116-fabula (Jeff 24.10, raport: Joffrey dostal 7322 zl nagrody ze skarbca wlasnej korony za Neda): zdobywca = krol korony zdobywcy - bez nagrody
                bool ownKing = captor != null && captor == k.Leader && Settings.Current.RansomNoRewardToOwnKing;
                if (ownKing) { _dRewardOwnKing++; reward = 0; }
                _paidFor.Remove(prisoner.StringId);
                if (captor != null && reward > 0) { _rewards.Add(new Reward { Hero = captor.StringId, Kingdom = k.StringId, Captive = prisoner.StringId, Left = reward }); _dRewardQueued += reward; }
                _dGreatTakenN++;
                Log.Info("Okupy (178): wielki jeniec - " + Name(prisoner) + " (" + (prisoner.MapFaction != null && prisoner.MapFaction.Leader == prisoner ? "krol" : "nastepca tronu") + ", "
                         + prisoner.MapFaction.Name + ") pojmany przez " + Name(captor) + " - przechodzi na korone " + k.Name + "; okup ok. " + price + " zl do skarbca " + k.Name
                         + (ownKing ? ", nagroda pominieta - zdobywca to krol korony (m116)." : ", nagroda dla zdobywcy " + reward + " zl ze skarbca."));
                if (captor == Hero.MainHero && !ownKing)
                    Log.Player("You hand " + Name(prisoner) + " to your liege. The crown of " + k.Name + " will have the ransom, and pays you a reward of " + reward + " denars.");
            }
            catch (Exception e) { Stumble("OnPrisonerTaken", e); }
        }

        private static void OnPrisonerReleased(Hero h, PartyBase party, IFaction f, EndCaptivityDetail d, bool show)
        {
            try
            {
                if (h == null || h.StringId == null) return;
                _crownCaptive.Remove(h.StringId);
                _paidFor.Remove(h.StringId);
                // 367c: jeniec wolny (okup poszedl tam, gdzie mial) - niewyplacona nagroda za niego zostaje, ale bez przypisania (nastepne pojmanie to nowa sprawa)
                foreach (var r in _rewards) if (r != null && r.Captive == h.StringId) r.Captive = "";
            }
            catch { }
        }

        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(owner, OnPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(owner, OnPrisonerReleased);
        }

        // ------------------------------------------------------------ jeden przeplyw okupu
        private struct Rcv { public Hero Hero; public Kingdom Crown; }

        private static Rcv ReceiverFor(Hero captive, Clan captorClan)
        {
            var r = new Rcv();
            var k = CrownCheck(captive);   // 367c: korona tylko, gdy trzymajacy jej dalej sluzy
            if (k != null) { r.Crown = k; return r; }
            var h = captorClan != null ? captorClan.Leader : null;
            r.Hero = h != null && h.IsAlive ? h : null;
            return r;
        }

        /// <summary>Gotowka do odbiorcy (glowa rodu porywacza albo skarbiec korony zdobywcy); 1/9 korony porywacza - licznik doby.</summary>
        private static void Pay(Rcv r, long x)
        {
            if (x <= 0) return;
            int ix = (int)Math.Min(int.MaxValue, x);
            if (r.Crown != null) { r.Crown.KingdomBudgetWallet += ix; _dToCrown += x; return; }
            if (r.Hero == null) return;
            r.Hero.ChangeHeroGold(ix);
            CirculationWindows.NoteHeroGold(r.Hero, x);
            if (r.Hero != Hero.MainHero) ClanIncomeBook.NoteInflow(r.Hero, ix, ClanIncomeBook.KRansom);
            NoteRansomIn(r.Hero, x);
        }

        /// <summary>Okup jenca (AI-AI, kurier): cena, gotowka od rodziny (albo skarbca krola), dlug okupu w ksiedze 168. Jenca uwalnia wolajacy. false - bez umowy.</summary>
        private static bool Execute(Hero captive, Clan captorClan, string path, double mult = 1.0)
        {
            var s = Settings.Current;
            bool byCrown; double D;
            long price = PriceOf(captive, out byCrown, out D);
            long fullBase = price;   // M6a-2: cena z mnoznikiem okupu powtornego, przed pulapem dlugu (licznik)
            if (mult != 1.0) price = (long)(price * Math.Max(0.0, mult));   // 366: warunek R (okup podwojony) - ten sam przeplyw, licznik M6a-2 z ceny zwyklej
            _lastExecPrice = 0;
            var rcv = ReceiverFor(captive, captorClan);
            if (rcv.Hero == null && rcv.Crown == null) return false;
            var payerClan = captive.Clan;
            long cash, debt;
            if (byCrown)
            {
                var k = (Kingdom)captive.MapFaction;
                cash = CrownCash(k, price);
                if (cash > 0) { k.KingdomBudgetWallet -= (int)Math.Min(int.MaxValue, cash); }
                debt = price - cash;
                long aidK = VassalRansomAid.Help(captive, captorClan, debt);   // R3-01d: wasale krolestwa doplacaja do braku skarbca (zloto z ich kies, nie ze skarbca)
                if (aidK > 0) { cash += aidK; debt -= aidK; }
                Pay(rcv, cash);
                if (debt > 0) CrownIncome.AddCrownDebt(k, rcv.Crown != null ? rcv.Crown.StringId : "h:" + rcv.Hero.StringId, debt);
                _dKingN++; _dKingCash += cash; _dKingDebt += debt;
            }
            else
            {
                long have = DebtLadder.RansomDebtOf(payerClan);
                if (!s.RansomQueueNoBlock && have > 0) { _dBlockedN++; return false; }
                cash = Collect(payerClan, CashCap(payerClan, price));
                debt = price - cash;
                long cap = (long)(Math.Max(0f, s.RansomDebtCapDays) * D) - have;
                if (debt > Math.Max(0, cap)) { long cut = debt - Math.Max(0, cap); _dCapCut += cut; _dCapN++; debt = Math.Max(0, cap); price = cash + debt; }
                long aid = VassalRansomAid.Help(captive, captorClan, debt);   // R3-01d: pomoc wasali na okup glowy rodu - po pulapie, do dlugu, ktory rod bylby winien
                if (aid > 0) { cash += aid; debt -= aid; }
                Pay(rcv, cash);
                if (debt > 0) DebtLadder.AddRansomDebt(payerClan, rcv.Crown != null, rcv.Crown != null ? rcv.Crown.StringId : rcv.Hero.StringId, "", debt);
            }
            if (price <= 0) _dZeroN++;
            // M6a-2: licznik okupu powtornego (cena pelna = cena bez mnoznika; po mnozniku = cena przed pulapem dlugu - pulap liczy sie osobno, jak dotad)
            try { double rf = RansomRepeat.Factor(captive); RansomRepeat.Note(rf > 0 && rf < 1.0 ? (long)Math.Round(fullBase / rf) : fullBase, fullBase, rf < 1.0); } catch { }
            _dPrice += price; _dCash += cash; _dDebt += debt;
            _lastExecPrice = price;
            if (price > _dMaxV) { _dMaxV = price; _dMax = Name(payerClan) + " -> " + (rcv.Crown != null ? "korona " + rcv.Crown.Name : Name(rcv.Hero)) + " " + price + " za " + Name(captive); }
            Log.Info("Okupy (178): " + path + " - " + Name(captive) + " (" + (captive.Clan.Leader == captive ? "glowa rodu" : "lord") + ", " + Name(payerClan) + ") wolny za " + price
                     + " zl (D " + Math.Round(D) + (byCrown ? ", placi skarbiec " + captive.MapFaction.Name : "") + "): gotowka " + cash + ", dlug " + debt + " -> "
                     + (rcv.Crown != null ? "skarbiec " + rcv.Crown.Name + " (wielki jeniec)" : Name(rcv.Hero) + " (" + Name(rcv.Hero.Clan) + ")") + ".");
            return true;
        }

        // ------------------------------------------------------------ 366: rokowania z wielkim jencem gracza (CaptiveTerms) - te same reguly i ten sam przeplyw
        private static long _lastExecPrice;

        /// <summary>366: czy okupy wedlug majatku sa czynne (warunek R idzie tylko tym przeplywem).</summary>
        internal static bool FlowsOn { get { try { return On; } catch { return false; } } }

        /// <summary>366: zwykla cena okupu (PriceOf) - 0 gdy nie lord albo blad.</summary>
        internal static long PriceFor(Hero h)
        {
            try { bool b; double D; return h != null && Lord(h) ? PriceOf(h, out b, out D) : 0; }
            catch (Exception e) { Stumble("PriceFor", e); return 0; }
        }

        /// <summary>366: srednia wplywow dnia korony (28 dob) - miara gniewu pana przy utraconym okupie.</summary>
        internal static double CrownDayIncome(Kingdom k) { try { return k != null ? CrownAvg(k) : 0; } catch { return 0; } }

        /// <summary>366: korona, do ktorej skarbca idzie okup wielkiego jenca (null - jeniec nie przeszedl na korone; okup dla porywacza).</summary>
        internal static Kingdom CaptiveCrown(Hero h)
        {
            try { return CrownCheck(h); }
            catch (Exception e) { Stumble("CaptiveCrown", e); return null; }
        }

        // ------------------------------------------------------------ 367c: jeniec korony tylko, dopoki trzymajacy sluzy tej koronie
        /// <summary>Rod, ktory fizycznie trzyma jenca (partia - jak przy pojmaniu, loch - wlasciciel osady). null - nie wiadomo.</summary>
        private static Clan HolderClan(Hero h)
        {
            try { return h != null && h.IsPrisoner ? CaptorClan(h.PartyBelongedToAsPrisoner) : null; }
            catch { return null; }
        }

        /// <summary>367c: korona wielkiego jenca albo null. Gdy rod trzymajacego jenca nie sluzy juz tej koronie (odejscie, defekcja, wygnanie) - jeniec schodzi
        /// z korony (Revoke): okup i rokowania naleza do trzymajacego. Trzymajacy nieznany (bez partii, rod martwy) - bez zmian. Bez zdarzenia ClanChangedKingdom:
        /// unia 272/332 przenosi rody PRZED Ransom178.MoveKingdom - zdarzenie zdjeloby jencow korony, ktora sie wlasnie laczy. Wolajacy lapie wyjatki.</summary>
        private static Kingdom CrownCheck(Hero h)
        {
            string kid;
            if (!GreatOn || h == null || h.StringId == null || !_crownCaptive.TryGetValue(h.StringId, out kid)) return null;
            var k = KingdomById(kid);
            if (k == null || k.IsEliminated) return null;
            var holder = HolderClan(h);
            if (holder == null || holder.IsEliminated || holder.Kingdom == k) return k;
            Revoke(h, k, holder);
            return null;
        }

        /// <summary>367c: zdjecie jenca z korony. Nagroda za niego juz wyplacona zostaje u zdobywcy (korona ja stracila - zdrada kosztuje korone, zloto sie nie rusza);
        /// niewyplacona (przypisana do tego jenca) - anulowana (skarbiec jej nie placi). Wpisy sprzed 367c bez przypisania - placone dalej jak dotad.</summary>
        private static void Revoke(Hero h, Kingdom k, Clan holder)
        {
            _crownCaptive.Remove(h.StringId);
            long kept;
            bool known = _paidFor.TryGetValue(h.StringId, out kept);
            _paidFor.Remove(h.StringId);
            long voided = 0;
            foreach (var r in _rewards.ToArray())
                if (r != null && r.Captive == h.StringId && r.Kingdom == k.StringId) { voided += Math.Max(0, r.Left); _rewards.Remove(r); }
            _dRevokedN++; _dRevokedKept += kept; _dRevokedVoid += voided;
            Log.Info("Okupy (178): jeniec " + Name(h) + " wraca do " + Name(holder) + " - " + Name(holder) + " nie sluzy juz koronie " + k.Name
                     + (holder.Kingdom != null ? " (teraz " + holder.Kingdom.Name + ")" : " (bez krolestwa)") + " (nagroda wyplacona " + (known ? kept.ToString(Inv) : "? - sprzed 367c, bez zapisu") + " zostaje"
                     + (voided > 0 ? ", niewyplacona " + voided + " anulowana" : "") + "); okup i rokowania - dla " + Name(holder) + ".");
            if (holder == Clan.PlayerClan)
                Log.Player(Name(h) + " is your captive now: you no longer serve " + k.Name + "." + (voided > 0 ? " The crown's unpaid reward of " + voided.ToString("N0", Inv) + " denars is void." : ""));
        }

        /// <summary>367c: dzienne sprawdzenie wszystkich jencow koron (takze pierwsza doba po wczytaniu zapisu). Potkniecia per jeniec.</summary>
        private static void CrownSweep()
        {
            foreach (var id in new List<string>(_crownCaptive.Keys))
            {
                try
                {
                    Hero h = null;
                    try { h = Hero.Find(id); } catch { h = null; }
                    if (h == null) continue;
                    CrownCheck(h);
                }
                catch (Exception e) { Stumble("CrownSweep", e); }
            }
        }

        /// <summary>366: niewyplacona nagroda zdobywcy `to` od korony `k` (suma wpisow - nagrody nie sa przypisane do jenca).</summary>
        internal static long PendingReward(Hero to, Kingdom k)
        {
            long v = 0;
            try { if (to != null && k != null) foreach (var r in _rewards) if (r != null && r.Hero == to.StringId && r.Kingdom == k.StringId) v += Math.Max(0, r.Left); }
            catch (Exception e) { Stumble("PendingReward", e); }
            return v;
        }

        /// <summary>366 (warunek T): uwolnienie bez okupu - korona nie dostanie okupu, wiec skresla niewyplacona nagrode zdobywcy (najwyzej `max`). Zwraca skreslone.</summary>
        internal static long CancelReward(Hero to, Kingdom k, long max)
        {
            long done = 0;
            try
            {
                if (to == null || k == null || max <= 0) return 0;
                foreach (var r in _rewards.ToArray())
                {
                    if (done >= max) break;
                    if (r == null || r.Hero != to.StringId || r.Kingdom != k.StringId || r.Left <= 0) continue;
                    long x = Math.Min(r.Left, max - done);
                    r.Left -= x; done += x;
                    if (r.Left <= 0) _rewards.Remove(r);
                }
            }
            catch (Exception e) { Stumble("CancelReward", e); }
            return done;
        }

        /// <summary>366 (warunek R): okup x mult tym samym przeplywem co kurier (Execute: skarbiec korony jenca albo rod jenca -> skarbiec korony zdobywcy albo
        /// porywacz; brak gotowki - dlug korony / dlug okupu, pomoc wasali R3-01d). Gdy okup idzie do korony - nagroda gracza 1/10 takze od nadwyzki (ta sama regula
        /// co przy pojmaniu, m116 - bez nagrody dla krola tej korony). Jenca NIE uwalnia (wolajacy). false - bez umowy.</summary>
        internal static bool TermsRansom(Hero captive, double mult, out long price, out long extraReward, out string receiver)
        {
            price = 0; extraReward = 0; receiver = "-";
            try
            {
                if (!On || captive == null || !Lord(captive) || Clan.PlayerClan == null) return false;
                long basePrice = PriceFor(captive);
                var crown = CaptiveCrown(captive);
                if (!Execute(captive, Clan.PlayerClan, "rokowania 366 (okup x" + mult.ToString("0.##", Inv) + ")", mult)) return false;
                price = _lastExecPrice;
                receiver = crown != null ? "skarbiec " + crown.Name : Name(Hero.MainHero);
                if (crown != null)
                {
                    var s = Settings.Current;
                    bool ownKing = crown.Leader == Hero.MainHero && s.RansomNoRewardToOwnKing;
                    extraReward = ownKing ? 0 : (long)(Math.Max(0f, Math.Min(1f, s.CrownGreatCaptiveReward)) * Math.Max(0, price - basePrice));
                    if (extraReward > 0) { _rewards.Add(new Reward { Hero = Hero.MainHero.StringId, Kingdom = crown.StringId, Left = extraReward }); _dRewardQueued += extraReward; }
                }
                return true;
            }
            catch (Exception e) { Stumble("TermsRansom", e); return false; }
        }

        // ------------------------------------------------------------ latki gry
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static MethodInfo _mCaptorClan;
        private static FieldInfo _fCurHero, _fDeclined, _fPrisoner;
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            var rb = typeof(RansomOfferCampaignBehavior);
            _mCaptorClan = AccessTools.Method(rb, "GetCaptorClanOfPrisoner", new[] { typeof(Hero) });
            _fCurHero = AccessTools.Field(rb, "_currentRansomHero");
            _fDeclined = AccessTools.Field(rb, "_heroesWithDeclinedRansomOffers");
            _fPrisoner = AccessTools.Field(typeof(SetPrisonerFreeBarterable), "_prisonerCharacter");
            Wire("okup AI-AI i oferty kuriera (ConsiderRansomPrisoner)", _mCaptorClan != null && _fCurHero != null && _fDeclined != null ? AccessTools.Method(rb, "ConsiderRansomPrisoner", new[] { typeof(Hero) }) : null, nameof(ConsiderPrefix), null);
            Wire("kurier: gracz placi czesc gotowka (IsAffirmativeOptionEnabled)", AccessTools.Method(rb, "IsAffirmativeOptionEnabled", new[] { typeof(Hero), typeof(int) }), nameof(AffirmPrefix), null);
            Wire("cena w barterze i kurierze (GetUnitValueForFaction)", _fPrisoner != null ? AccessTools.Method(typeof(SetPrisonerFreeBarterable), "GetUnitValueForFaction", new[] { typeof(IFaction) }) : null, null, nameof(UnitValuePostfix));
            Wire("posrednik i ekran druzyny (SellPrisonersAction)", AccessTools.Method(typeof(SellPrisonersAction), "ApplyInternal"), nameof(SalePrefix), nameof(SalePostfix));
            Wire("1/9 sprzedazy lupu lorda AI (PartiesSellLoot)", AccessTools.Method(typeof(PartiesSellLootCampaignBehavior), "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) }), nameof(LootPrefix), nameof(LootPostfix));
            Wire("1/9 lupu gracza z ekranu po bitwie (DoLootInventory)", AccessTools.Method(typeof(PlayerEncounter), "DoLootInventory"), nameof(ScreenLootPrefix), null);
            Log.Info("Okupy (178): latki - " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-") + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-") + ".");
        }

        private static void Wire(string label, MethodBase m, string pre, string post)
        {
            try
            {
                if (m == null) { _missing.Add(label); return; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(Ransom178), pre) { priority = Priority.First } : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(Ransom178), post) { priority = Priority.Last } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        private static Clan GetCaptorClan(object beh, Hero h)
        {
            try { return _mCaptorClan != null && h != null && h.PartyBelongedToAsPrisoner != null ? _mCaptorClan.Invoke(beh, new object[] { h }) as Clan : null; }
            catch { return null; }
        }

        private static Hero PayerHeroOf(Hero captive)
        {
            var c = captive.Clan;
            if (c.Leader != captive) return c.Leader;
            foreach (var h in c.AliveLords) if (h != null && h != captive) return h;
            return null;
        }

        /// <summary>Prefiks ConsiderRansomPrisoner: AI-AI - wlasny przeplyw (10% dziennie); jeniec gracza albo gracz placi - oferta kuriera bez sprawdzania kiesy placacego
        /// (placi czesc gotowka, reszta na raty).</summary>
        public static bool ConsiderPrefix(RansomOfferCampaignBehavior __instance, Hero __0)
        {
            try
            {
                if (RescueHunt.HoldCourier(__0)) return false;   // 222: kurier nie przychodzi, zanim rod jenca nie wie o pojmaniu (takze przy 178 wylaczonym)
                if (!On || __0 == null || !Lord(__0)) return true;
                var hero = __0;
                var captorClan = GetCaptorClan(__instance, hero);
                if (captorClan == null) return false;   // gra tez wychodzi
                bool player = captorClan == Clan.PlayerClan || hero.Clan == Clan.PlayerClan;
                if (player)
                {
                    if (hero.Clan == Clan.PlayerClan && !DebtLadder.PlayerOnLadder) return true;   // gracz poza drabina placi jak w grze
                    var payer = PayerHeroOf(hero);
                    if (payer == null || (payer == Hero.MainHero && payer.IsPrisoner)) return false;
                    if (_fCurHero.GetValue(__instance) != null || MobileParty.MainParty == null || MobileParty.MainParty.IsInRaftState) return false;
                    var declined = _fDeclined.GetValue(__instance) as List<Hero>;
                    float chance = declined != null && declined.Contains(hero) ? 0.12f : 0.2f;
                    if (MBRandom.RandomFloat < chance)
                    {
                        __instance.SetCurrentRansomHero(hero, payer);
                        var text = new TextObject("{=ZqJ92UN4}A courier with a ransom offer for the freedom of {CAPTIVE_HERO.NAME} has arrived.");
                        StringHelpers.SetCharacterProperties("CAPTIVE_HERO", hero.CharacterObject, text);
                        Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new RansomOfferMapNotification(hero, text));
                    }
                    return false;
                }
                if (MBRandom.RandomFloat >= 0.1f) return false;   // szansa zgody: stale 10% dziennie (jak barter gry)
                var payerHero = PayerHeroOf(hero);
                if (Execute(hero, captorClan, "AI-AI")) { _dAiN++; EndCaptivityAction.ApplyByRansom(hero, payerHero ?? hero.Clan.Leader); }
                return false;
            }
            catch (Exception e) { Stumble("ConsiderPrefix", e); return true; }
        }

        /// <summary>RansomFlows.AcceptPrefix (kurier przyjety przez gracza): ten sam przeplyw - true = obsluzone (oryginal gry pominiety).</summary>
        internal static bool CourierAccept(object beh, Hero hero, Hero payer)
        {
            try
            {
                if (!On || hero == null || !Lord(hero) || payer == null) return false;
                if (payer == Hero.MainHero && !DebtLadder.PlayerOnLadder) return false;
                var b = beh as RansomOfferCampaignBehavior;
                var captorClan = GetCaptorClan(beh, hero);
                if (b == null || captorClan == null) return false;
                bool crown = CrownCheck(hero) != null;   // 367c: ta sama regula co odbiorca okupu
                bool ok = Execute(hero, captorClan, "kurier");
                try { var declined = _fDeclined != null ? _fDeclined.GetValue(beh) as List<Hero> : null; if (declined != null) declined.Remove(hero); } catch { }
                b.SetCurrentRansomHero(null);
                if (!ok) { Log.Player("The courier's masters could not settle the ransom for " + Name(hero) + ". The offer lapses.", true); return true; }
                _dCourierN++;
                EndCaptivityAction.ApplyByRansom(hero, hero.Clan.Leader);
                long debt = DebtLadder.RansomDebtOf(hero.Clan);
                if (payer == Hero.MainHero) Log.Player("You pay what you can at once for " + Name(hero) + "; the rest of the ransom is a debt your house pays off from its income.");
                else if (crown) Log.Player(Name(hero.Clan) + " ransoms " + Name(hero) + ". A royal captive: the ransom goes to your liege's treasury; your reward comes from the crown.");
                else Log.Player(Name(hero.Clan) + " ransoms " + Name(hero) + ": part in coin now, the rest owed to you and paid from their income.");
                return true;
            }
            catch (Exception e) { Stumble("CourierAccept", e); return false; }
        }

        /// <summary>Kurier, gracz placi za czlonka rodu: przycisk czynny takze, gdy kiesa < ceny (placi czesc gotowka, reszta na raty).</summary>
        public static bool AffirmPrefix(Hero __0, ref ValueTuple<bool, string> __result)
        {
            try
            {
                if (!On || __0 != Hero.MainHero || !DebtLadder.PlayerOnLadder) return true;
                __result = new ValueTuple<bool, string>(true, string.Empty);
                return false;
            }
            catch { return true; }
        }

        /// <summary>Barter i kurier: wartosc uwolnienia lorda = nasza cena / 1.1 (kurier gry mnozy x1.1) - bez wzrostu za czas niewoli.</summary>
        public static void UnitValuePostfix(SetPrisonerFreeBarterable __instance, ref int __result)
        {
            try
            {
                if (!On || __result == 0) return;
                var p = _fPrisoner.GetValue(__instance) as Hero;
                if (p == null || !Lord(p)) return;
                bool byCrown; double D;
                long price = PriceOf(p, out byCrown, out D);
                int v = (int)Math.Min(int.MaxValue, price / 1.1);
                __result = __result > 0 ? v : -v;
            }
            catch (Exception e) { Stumble("UnitValuePostfix", e); }
        }

        // ------------------------------------------------------------ posrednik i ekran druzyny (gracz sprzedaje lordow)
        /// <summary>RC przez refleksje: czy okupy 178 sa czynne (RC nie potraca wtedy nadwyzki ponad cene gry - robi to Armoury).</summary>
        public static bool RcActive() { try { return On; } catch { return false; } }

        /// <summary>RC (FairRansomPatch.LordPrice) przez refleksje: cena lorda przy 178 - sprzedaje gracz: tylko gotowka (tyle gra wyplaci z niczego, a rod jenca odda);
        /// trzyma gracz: pelna cena. -1 = 178 wylaczone (RC liczy po swojemu).</summary>
        public static int RcLordPrice(Hero h, Hero seller, int vanilla)
        {
            try
            {
                if (!On || h == null || !Lord(h) || h.Clan == Clan.PlayerClan) return -1;
                bool byCrown; double D;
                long price = PriceOf(h, out byCrown, out D);
                if (seller != Hero.MainHero) return (int)Math.Min(int.MaxValue, price);
                long cash = byCrown ? CrownCash((Kingdom)h.MapFaction, price) : CashCap(h.Clan, price);
                return (int)Math.Min(int.MaxValue, cash);
            }
            catch (Exception e) { Stumble("RcLordPrice", e); return -1; }
        }

        public static void SalePrefix(PartyBase __0, TroopRoster __2, out List<KeyValuePair<Hero, long>> __state)
        {
            __state = null;
            try
            {
                if (!On || __0 != PartyBase.MainParty || __2 == null) return;
                foreach (var el in __2.GetTroopRoster())
                {
                    var h = el.Character != null && el.Character.IsHero ? el.Character.HeroObject : null;
                    if (h == null || !Lord(h) || h.Clan == Clan.PlayerClan) continue;
                    bool byCrown; double D;
                    long price = PriceOf(h, out byCrown, out D);
                    if (__state == null) __state = new List<KeyValuePair<Hero, long>>();
                    __state.Add(new KeyValuePair<Hero, long>(h, price));
                }
            }
            catch (Exception e) { Stumble("SalePrefix", e); __state = null; }
        }

        /// <summary>Gra zaplacila graczowi za lordow z niczego tyle, ile gotowki zaplaci rod jenca (cena RC) - rod jenca oddaje te gotowke (w nicosc, rownowazy),
        /// reszta ceny to dlug rodu wobec gracza (ksiega 168). Jeniec przeniesiony do lochu osady (nie uwolniony) - jak w grze, licznik.</summary>
        public static void SalePostfix(List<KeyValuePair<Hero, long>> __state)
        {
            if (__state == null) return;
            foreach (var kv in __state)
            {
                try
                {
                    var h = kv.Key; long price = kv.Value;
                    if (h.IsPrisoner) { _dDungeonN++; continue; }
                    var s = Settings.Current;
                    bool byCrown; double D;
                    PriceOf(h, out byCrown, out D);
                    long cash, debt, aid = 0;
                    if (byCrown)
                    {
                        var k = (Kingdom)h.MapFaction;
                        cash = CrownCash(k, price);
                        if (cash > 0) k.KingdomBudgetWallet -= (int)Math.Min(int.MaxValue, cash);   // gra zaplacila graczowi z niczego - skarbiec krola oddaje (w nicosc)
                        debt = price - cash;
                        aid = VassalRansomAid.Help(h, Clan.PlayerClan, debt); debt -= aid;   // R3-01d: wasale krolestwa jenca doplacaja graczowi do braku skarbca
                        if (debt > 0) CrownIncome.AddCrownDebt(k, "h:" + Hero.MainHero.StringId, debt);
                    }
                    else
                    {
                        long want = CashCap(h.Clan, price);
                        cash = Collect(h.Clan, want);
                        if (cash < want)
                        {
                            // rodzina nie zebrala calej gotowki (zloto pod podlogami) - roznice gracz oddaje (dostal ja z niczego), trafia do dlugu
                            long back = Math.Min(want - cash, Math.Max(0, (long)Hero.MainHero.Gold));
                            if (back > 0) Hero.MainHero.ChangeHeroGold(-(int)back);
                            if (want - cash - back > 0) { _dBrokerShort += want - cash - back; _dBrokerShortN++; }
                        }
                        debt = price - want;
                        long have = DebtLadder.RansomDebtOf(h.Clan);
                        long cap = (long)(Math.Max(0f, s.RansomDebtCapDays) * D) - have;
                        debt += want - cash;
                        if (debt > Math.Max(0, cap)) { _dCapCut += debt - Math.Max(0, cap); _dCapN++; debt = Math.Max(0, cap); }
                        aid = VassalRansomAid.Help(h, Clan.PlayerClan, debt); debt -= aid;   // R3-01d: pomoc wasali na okup glowy rodu - po pulapie, zloto wasali do gracza
                        if (debt > 0) DebtLadder.AddRansomDebt(h.Clan, false, Hero.MainHero.StringId, "", debt);
                    }
                    if (aid > 0) { Hero.MainHero.ChangeHeroGold((int)Math.Min(int.MaxValue, aid)); CirculationWindows.NoteHeroGold(Hero.MainHero, aid); }   // R3-01d: kiesy wasali -> gracz (prawdziwe zloto, nie z gry)
                    NoteRansomIn(Hero.MainHero, cash + aid);
                    _dBrokerN++; _dPrice += cash + aid + debt; _dCash += cash + aid; _dDebt += debt;
                    Log.Info("Okupy (178): posrednik - gracz sprzedal " + Name(h) + " (" + Name(h.Clan) + "): gotowka " + cash + " (gra wyplacila graczowi, " + (byCrown ? "skarbiec krola" : "rodzina jenca")
                             + " oddala ja - w nicosc 0 netto)" + (aid > 0 ? ", od wasali (R3-01d) " + aid : "") + ", dlug wobec gracza " + debt + ".");
                    if (debt > 0 || aid > 0) Log.Player(Name(h.Clan) + " pays " + cash + " denars now for " + Name(h) + (aid > 0 ? ", its vassals add " + aid : "")
                                                        + (debt > 0 ? ", and it owes you " + debt + " more, paid off from their income." : "."));
                }
                catch (Exception e) { Stumble("SalePostfix", e); }
            }
        }

        // ------------------------------------------------------------ okup gracza (RC - kwota, 2.14 - zaplata)
        /// <summary>RC (RansomAmountPatch) przez refleksje: gotowka okupu gracza (dzis do zaplaty); -1 = 178 wylaczone albo gracz poza drabina.</summary>
        public static int RcPlayerRansom()
        {
            try
            {
                if (!On || !DebtLadder.PlayerOnLadder || Clan.PlayerClan == null) return -1;
                long price = PlayerPrice();
                return (int)Math.Max(1, Math.Min(int.MaxValue, CashCap(Clan.PlayerClan, price)));
            }
            catch (Exception e) { Stumble("RcPlayerRansom", e); return -1; }
        }

        private static long PlayerPrice()
        {
            var s = Settings.Current;
            return (long)(Math.Max(0f, s.RansomHeadYears) * 364.0 * DebtLadder.DebtD(Clan.PlayerClan));
        }

        /// <summary>RansomFlows.MenuRansomPrefix po zaplaceniu gotowki porywaczowi: reszta okupu gracza - dlug gracza wobec odbiorcy (ksiega 168).</summary>
        internal static void PlayerRansomPaid(Hero to, long paid)
        {
            try
            {
                if (!On || !DebtLadder.PlayerOnLadder || to == null || to == Hero.MainHero) return;
                var s = Settings.Current;
                long price = PlayerPrice();
                long debt = Math.Max(0, price - paid);
                long cap = (long)(Math.Max(0f, s.RansomDebtCapDays) * DebtLadder.DebtD(Clan.PlayerClan)) - DebtLadder.RansomDebtOf(Clan.PlayerClan);
                if (debt > Math.Max(0, cap)) { _dCapCut += debt - Math.Max(0, cap); _dCapN++; debt = Math.Max(0, cap); }
                NoteRansomIn(to, paid);
                if (debt > 0) DebtLadder.AddRansomDebt(Clan.PlayerClan, false, to.StringId, "", debt);
                _dPlayerN++; _dPrice += paid + debt; _dCash += paid; _dDebt += debt;
                Log.Info("Okupy (178): okup gracza - gotowka " + paid + " do " + Name(to) + ", dlug gracza " + debt + " (cena " + price + ").");
                if (debt > 0) Log.Player("You owe " + Name(to) + " another " + debt + " denars of your ransom, paid off from your income.");
            }
            catch (Exception e) { Stumble("PlayerRansomPaid", e); }
        }

        // ------------------------------------------------------------ prawo trzecich (1/9) - podstawa doby
        private static Acc AccOf(Hero h)
        {
            Acc a;
            if (!_acc.TryGetValue(h.StringId, out a)) { a = new Acc(); _acc[h.StringId] = a; }
            return a;
        }

        /// <summary>Czy korona bierze 1/9 od tego bohatera: rod w krolestwie w wojnie (placi zwrot), nie najemnik; gracz - wasal (nie krol).</summary>
        private static bool Taxed(Hero h)
        {
            if (!ThirdsOn || h == null || !h.IsAlive || h.StringId == null) return false;
            var c = h.Clan;
            if (c == null || c.Kingdom == null || c.IsUnderMercenaryService || !AtWar(c.Kingdom)) return false;
            if (h == Hero.MainHero && c.Kingdom.RulingClan == c) return false;
            return true;
        }

        /// <summary>Lup miast (CitySack, L2/L5): czy korona wezmie 1/9 od tego bohatera (ta sama regula co podstawa doby) - tylko odczyt do napisu.</summary>
        internal static bool WouldTax(Hero h) { try { return Taxed(h); } catch { return false; } }

        /// <summary>noc 25.10 P5 (okno lupu, recenzja (2)): ile korona wezmie od tego bohatera o swicie z podstawy zebranej od ostatniego rozliczenia (bez zaleglosci
        /// Carry) - ten sam wzor co ThirdsSettle: Third x sh x 3 + Ninth x sh, sakwa Purse x sh. Tylko odczyt; SpoilsReport bierze roznice przed i po ramce.</summary>
        internal static long PendingClaim(Hero h)
        {
            try
            {
                Acc a;
                if (h == null || h.StringId == null || !_acc.TryGetValue(h.StringId, out a)) return 0;
                var s = Settings.Current;
                if (s == null) return 0;
                double sh = Math.Max(0f, Math.Min(1f, s.CrownThirdsShare));
                return (long)(a.Third * sh * 3.0 + a.Ninth * sh) + (long)(a.Purse * sh);
            }
            catch { return 0; }
        }

        /// <summary>326d (okno lupu): czesc PendingClaim brana o swicie najpierw z sakiewki ludzi gracza (sakwa Purse x sh - ten sam wzor co ThirdsSettle). Tylko odczyt.</summary>
        internal static long PendingMenClaim(Hero h)
        {
            try
            {
                Acc a;
                if (h == null || h.StringId == null || !_acc.TryGetValue(h.StringId, out a)) return 0;
                var s = Settings.Current;
                if (s == null) return 0;
                double sh = Math.Max(0f, Math.Min(1f, s.CrownThirdsShare));
                return (long)(a.Purse * sh);
            }
            catch { return 0; }
        }

        /// <summary>ClanIncomeBook.NoteInflow(KThird): trzecia lorda AI (nadwyzki ludzi, sakwy) - korona bierze jej 1/3.</summary>
        internal static void NoteThird(Hero h, long x) { try { if (x > 0 && Taxed(h)) { AccOf(h).Third += x; _dBaseThird += x; } } catch (Exception e) { Stumble("NoteThird", e); } }

        /// <summary>Okup otrzymany (gotowka i raty; nie wielcy jency) - korona 1/9.</summary>
        internal static void NoteRansomIn(Hero h, long x) { try { if (x > 0 && Taxed(h)) { AccOf(h).Ninth += x; _dBaseRansom += x; } } catch (Exception e) { Stumble("NoteRansomIn", e); } }

        /// <summary>3.2 (BeatenPurse) i 112 (VillageTakings): zloto z kiesy rozbitej partii bez wodza (tabor wsi, banda, karawana), ktore wzial wodz zwyciezcow
        /// (takze gracz), oraz kiesa karawany albo taboru oddana graczowi w rozmowie (BeatenPurse.YieldPostfix) - korona 1/9 (decyzja Jeffa 09.10 04:55, Q3b:
        /// "korona bierze 1/9 lupu wasali").</summary>
        internal static void NoteBeatenGold(Hero h, long x) { try { if (x > 0 && Taxed(h)) { AccOf(h).Ninth += x; _dBaseGold += x; } } catch (Exception e) { Stumble("NoteBeatenGold", e); } }

        /// <summary>MenPurse.OnPartyDestroyed: sakwa rozbitej partii do sakiewki ludzi gracza - korona 1/9 z tej sakiewki.</summary>
        internal static void NotePlayerPurse(long x) { try { if (x > 0 && Taxed(Hero.MainHero)) { AccOf(Hero.MainHero).Purse += x; _dBasePurse += x; } } catch (Exception e) { Stumble("NotePlayerPurse", e); } }

        public static void LootPrefix(MobileParty __0, out int __state)
        {
            __state = int.MinValue;
            try { if (ThirdsOn && __0 != null && !__0.IsMainParty && __0.LeaderHero != null) __state = __0.LeaderHero.Gold; } catch { }
        }

        /// <summary>Gra (PartiesSellLoot): lord AI sprzedal lup w miescie - kasa miasta -> lord; korona 1/9.</summary>
        public static void LootPostfix(MobileParty __0, int __state)
        {
            try
            {
                if (__state == int.MinValue || __0 == null || __0.LeaderHero == null) return;
                long got = (long)__0.LeaderHero.Gold - __state;
                if (got > 0 && Taxed(__0.LeaderHero)) { AccOf(__0.LeaderHero).Ninth += got; _dBaseLoot += got; }
            }
            catch (Exception e) { Stumble("LootPostfix", e); }
        }

        /// <summary>Ekran lupu gracza po bitwie: 1/9 wartosci lupu (cena najblizszego miasta przy sprzedazy) - z kiesy gracza jutro.</summary>
        public static void ScreenLootPrefix(PlayerEncounter __instance)
        {
            try
            {
                var s = Settings.Current;
                if (!s.CrownThirdsPlayerLoot || __instance == null || !Taxed(Hero.MainHero)) return;
                var roster = __instance.RosterToReceiveLootItems;
                if (roster == null || roster.Count == 0) return;
                var mp = MobileParty.MainParty;
                Settlement town = null; float bd = float.MaxValue;
                var pos = mp != null ? mp.GetPosition2D : TaleWorlds.Library.Vec2.Invalid;
                foreach (var t in Settlement.All) { if (t == null || !t.IsTown || t.Town == null) continue; float d = pos.IsValid ? pos.DistanceSquared(t.GetPosition2D) : 0f; if (d < bd) { bd = d; town = t; } }
                if (town == null) return;
                long v = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null || el.Amount <= 0) continue;
                    v += (long)MenPurse.SellPriceHere(el.EquipmentElement, town, mp) * el.Amount;
                }
                if (v > 0) { AccOf(Hero.MainHero).Ninth += v; _dBaseScreen += v; }
            }
            catch (Exception e) { Stumble("ScreenLootPrefix", e); }
        }

        /// <summary>Wplacone dzis do skarbca krolestwa z 1/9 (CrownIncome.Begin - "nasze" wplywy dnia).</summary>
        internal static long ThirdsOf(Kingdom k) { long v; return k != null && _thirdsToday.TryGetValue(k, out v) ? v : 0; }

        /// <summary>Krok korony (po daninie i cle, przed CrownIncome.Begin): 1/9 z licznikow doby do skarbcow - z kiesy odbiorcy (gracz: sakwy z sakiewki jego ludzi);
        /// czego brak - jutro.</summary>
        internal static void ThirdsSettle()
        {
            _thirdsToday.Clear();
            if (Campaign.Current == null) return;
            if (!ThirdsOn) { _acc.Clear(); return; }
            var s = Settings.Current;
            double sh = Math.Max(0f, Math.Min(1f, s.CrownThirdsShare));
            var drop = new List<string>();
            foreach (var kv in _acc)
            {
                try
                {
                    var a = kv.Value;
                    Hero h = null;
                    try { h = Hero.Find(kv.Key); } catch { h = null; }
                    var k = h != null && h.Clan != null ? h.Clan.Kingdom : null;
                    if (h == null || !h.IsAlive || k == null || k.IsEliminated) { drop.Add(kv.Key); continue; }
                    long owed = a.Carry + (long)(a.Third * sh * 3.0 + a.Ninth * sh);
                    long purse = (long)(a.Purse * sh);
                    a.Third = a.Ninth = a.Purse = 0;
                    int tookMen = 0;
                    if (purse > 0 && h == Hero.MainHero && MobileParty.MainParty != null)
                    {
                        int took = MenPurse.Take(MobileParty.MainParty, (int)Math.Min(int.MaxValue, purse));
                        if (took > 0) { k.KingdomBudgetWallet += took; Add(k, took); _dThirdsPurse += took; tookMen = took; }
                        owed += purse - took;   // reszta z kiesy gracza
                    }
                    long got = Math.Min(owed, Math.Max(0, (long)h.Gold));
                    if (got > 0)
                    {
                        h.ChangeHeroGold(-(int)got);
                        CirculationWindows.NoteHeroGold(h, -got);
                        k.KingdomBudgetWallet += (int)got;
                        Add(k, got); _dThirdsGot += got;
                    }
                    a.Carry = owed - got;
                    // 326d: gracz widzi, ile korona wziela o swicie i skad (raz na dobe - ThirdsSettle raz na swit); tylko komunikat, przelewy bez zmian
                    if (h == Hero.MainHero && (got > 0 || tookMen > 0))
                        Log.Player("The crown took its share (1/9) of your spoils at dawn: " + (got > 0 ? got + " denars from your purse" : "nothing from your purse")
                                   + (tookMen > 0 ? ", " + tookMen + " from your men's purse" : "") + (a.Carry > 0 ? "; " + a.Carry + " still owed" : "") + ".");
                    _dThirdsCarry += a.Carry;
                    if (a.Carry <= 0) drop.Add(kv.Key);
                }
                catch (Exception e) { Stumble("ThirdsSettle", e); }
            }
            foreach (var k in drop) _acc.Remove(k);
        }

        private static void Add(Kingdom k, long x) { long v; _thirdsToday.TryGetValue(k, out v); _thirdsToday[k] = v + x; }

        // ------------------------------------------------------------ krok korony: nagrody za wielkich jencow, srednia wplywow korony
        internal static void CrownStep()
        {
            if (Campaign.Current == null) return;
            try
            {
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated || k.StringId == null) continue;
                    var d = CrownIncome.DayOf(k);
                    if (d == null) continue;
                    Ema e;
                    if (!_crownIncome.TryGetValue(k.StringId, out e)) { e = new Ema(); _crownIncome[k.StringId] = e; }
                    e.N = Math.Min(28, e.N + 1);
                    e.V += (d.Measured - e.V) / e.N;
                }
            }
            catch (Exception ex) { Stumble("CrownStep(srednia)", ex); }
            if (!GreatOn) return;
            try { CrownSweep(); } catch (Exception ex) { Stumble("CrownStep(367c)", ex); }   // 367c: przed nagrodami - nie placi za jenca, ktorego korona stracila
            var s = Settings.Current;
            foreach (var r in _rewards.ToArray())
            {
                try
                {
                    var k = KingdomById(r.Kingdom);
                    Hero h = null;
                    try { h = Hero.Find(r.Hero); } catch { h = null; }
                    if (h != null && !h.IsAlive) { var lead = h.Clan != null ? h.Clan.Leader : null; h = lead != null && lead.IsAlive ? lead : null; }
                    if (k == null || k.IsEliminated || h == null) { _rewards.Remove(r); continue; }   // nikt nie odbierze - nagroda przepada (zloto sie nie rusza)
                    long left = CrownIncome.LeftFor(k);
                    long stock = Math.Max(0, (long)k.KingdomBudgetWallet - left - Math.Max(0, s.CrownReserveGold));
                    long pay = Math.Min(r.Left, Math.Min(left + stock, (long)Math.Max(0, k.KingdomBudgetWallet)));
                    if (pay <= 0) continue;
                    int ip = (int)Math.Min(int.MaxValue, pay);
                    k.KingdomBudgetWallet -= ip;
                    CrownIncome.Spent(k, Math.Min(ip, left));
                    h.ChangeHeroGold(ip);
                    CirculationWindows.NoteHeroGold(h, ip);
                    if (h != Hero.MainHero) ClanIncomeBook.NoteInflow(h, ip, ClanIncomeBook.KRansom);
                    r.Left -= ip; _dRewardPaid += ip;
                    if (!string.IsNullOrEmpty(r.Captive)) { long pv; _paidFor.TryGetValue(r.Captive, out pv); _paidFor[r.Captive] = pv + ip; }
                    try { var kd = CrownIncome.DayOf(k); if (kd != null) kd.Reward178 += ip; } catch { }   // 312e (recenzja): tylko licznik linii "Skarbce:"
                    if (h == Hero.MainHero) Log.Player("The crown of " + k.Name + " pays you " + ip + " denars as the reward for a royal captive.");
                    if (r.Left <= 0) _rewards.Remove(r);
                }
                catch (Exception e) { Stumble("CrownStep(nagroda)", e); }
            }
        }

        // ------------------------------------------------------------ linia "Okupy (178)"
        internal static void Report()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || Campaign.Current == null) return;
                if (!On && !ThirdsOn) return;
                long pend = 0; foreach (var r in _rewards) pend += r.Left;
                double sh = Math.Max(0f, Math.Min(1f, s.CrownThirdsShare));
                double due = (_dBaseThird * 3.0 + _dBaseLoot + _dBaseRansom + _dBasePurse + _dBaseScreen + _dBaseGold) * sh;
                var sb = new StringBuilder(1200);
                sb.Append("Okupy (178): dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(" | okupy dzis: AI-AI ").Append(_dAiN).Append(", kurier ").Append(_dCourierN).Append(", posrednik i ekran druzyny ").Append(_dBrokerN).Append(", okup gracza ").Append(_dPlayerN)
                  .Append(" - cena razem ").Append(_dPrice).Append(" = gotowka ").Append(_dCash).Append(" + dlug okupu ").Append(_dDebt)
                  .Append(" (najwiekszy: ").Append(_dMax ?? "-").Append(')')
                  .Append(" | pulap dlugu okupow (").Append(Math.Round(s.RansomDebtCapDays)).Append(" D): przycieto ").Append(_dCapN).Append(" okupow o ").Append(_dCapCut)
                  .Append("; okup 0 (rod bez dochodu albo pulap pelny i bez gotowki) ").Append(_dZeroN).Append(", bez umowy - kolejka zablokowana ").Append(_dBlockedN)
                  .Append(" | krol ze skarbca: ").Append(_dKingN).Append(" (gotowka ").Append(_dKingCash).Append(", nowy dlug korony ").Append(_dKingDebt).Append("; raty dlugu korony dzis ")
                  .Append(CrownIncome.LastKingRansomPaid).Append(", do splaty razem ").Append(CrownIncome.KingRansomDebtLeft()).Append(')')
                  .Append(" | wielcy jency: pojmani dzis ").Append(_dGreatTakenN).Append(", u koron zdobywcow ").Append(_crownCaptive.Count).Append(", okup do skarbcow zdobywcow ").Append(_dToCrown)
                  .Append("; nagrody zdobywcow: nowe ").Append(_dRewardQueued).Append(", wyplacone ").Append(_dRewardPaid).Append(", czeka ").Append(pend).Append(", pominiete - zdobywca krolem (m116) ").Append(_dRewardOwnKing)
                  .Append("; zdjeci z koron - trzymajacy nie sluzy juz koronie (367c) ").Append(_dRevokedN).Append(" (nagroda wyplacona zostaje ").Append(_dRevokedKept).Append(", anulowana ").Append(_dRevokedVoid).Append(')')
                  .Append(" | prawo trzecich (1/9): podstawa - trzecie lordow ").Append(_dBaseThird).Append(" (korona 1/3), sprzedaz lupu lordow AI ").Append(_dBaseLoot)
                  .Append(", okupy otrzymane ").Append(_dBaseRansom).Append(", sakwy ludzi gracza ").Append(_dBasePurse).Append(", lup gracza z ekranu ").Append(_dBaseScreen).Append(", zloto z kies rozbitych i poddanych graczowi (3.2/112) ").Append(_dBaseGold)
                  .Append("; nalezne z dzisiejszej podstawy ").Append(Math.Round(due)).Append(" | sciagniete do skarbcow (rozliczenie dzis rano, podstawa wczoraj) ").Append(_dThirdsGot + _dThirdsPurse)
                  .Append(" (z sakiewki ludzi gracza ").Append(_dThirdsPurse).Append("), zaleglosc odbiorcow ").Append(_dThirdsCarry)
                  .Append(" | lochy: gracz sprzedal lorda do lochu osady (zloto gry jak dotad) ").Append(_dDungeonN).Append(", posrednik - gotowki nie zebrano od rodziny ").Append(_dBrokerShort)
                  .Append(" | latki: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-").Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                  .Append(On ? "" : " (okupy wedlug majatku wylaczone)").Append(ThirdsOn ? "" : " (prawo trzecich wylaczone)")
                  .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
                if (_importN >= 0) { sb.Append(" Wczytano z zapisu: wpisow ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Report", e); }
            finally
            {
                LastRewards = _dRewardPaid; LastRansomCash = _dCash; LastToCrown = _dToCrown;
                LastThirds = _dThirdsGot + _dThirdsPurse;
                ZeroDay();
            }
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_rans178")
        private static readonly char[] Bad = { '|', ';', ':' };

        /// <summary>"v1|krolestwo:srednia:n;...|jeniec:korona;...|bohater:korona:reszta;...|bohater:zaleglosc;...|pozycja nagrody:jeniec;...|jeniec:wyplacona nagroda;..." (367c: 2 ostatnie)</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("v1|");
                bool f = true;
                foreach (var kv in _crownIncome) { if (kv.Key.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(kv.Key).Append(':').Append(Math.Round(kv.Value.V).ToString(Inv)).Append(':').Append(kv.Value.N.ToString(Inv)); }
                sb.Append('|'); f = true;
                foreach (var kv in _crownCaptive) { if (kv.Key.IndexOfAny(Bad) >= 0 || kv.Value.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(kv.Key).Append(':').Append(kv.Value); }
                sb.Append('|'); f = true;
                var ties = new StringBuilder(64); int pos = 0;
                foreach (var r in _rewards)
                {
                    if (r.Left <= 0 || r.Hero.IndexOfAny(Bad) >= 0 || r.Kingdom.IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false; sb.Append(r.Hero).Append(':').Append(r.Kingdom).Append(':').Append(r.Left.ToString(Inv));
                    if (!string.IsNullOrEmpty(r.Captive) && r.Captive.IndexOfAny(Bad) < 0) { if (ties.Length > 0) ties.Append(';'); ties.Append(pos.ToString(Inv)).Append(':').Append(r.Captive); }
                    pos++;
                }
                sb.Append('|'); f = true;
                foreach (var kv in _acc) { if (kv.Value.Carry <= 0 || kv.Key.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(kv.Key).Append(':').Append(kv.Value.Carry.ToString(Inv)); }
                // 367c: pola dopisane na koncu (stara wersja czyta 5 pierwszych i reszte pomija): przypisanie nagrod do jencow (pozycja:jeniec), wyplacone nagrody (jeniec:zl)
                sb.Append('|').Append(ties.ToString()).Append('|'); f = true;
                foreach (var kv in _paidFor) { if (kv.Value <= 0 || kv.Key.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv)); }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _crownIncome.Clear(); _crownCaptive.Clear(); _rewards.Clear(); _paidFor.Clear(); _acc.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 5 || f[0] != "v1") { _importBad++; return; }
                foreach (var p in f[1].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); double v; int n; if (x.Length == 3 && double.TryParse(x[1], NumberStyles.Float, Inv, out v) && int.TryParse(x[2], out n)) { _crownIncome[x[0]] = new Ema { V = v, N = Math.Max(0, Math.Min(28, n)) }; _importN++; } else _importBad++; }
                foreach (var p in f[2].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); if (x.Length == 2) { _crownCaptive[x[0]] = x[1]; _importN++; } else _importBad++; }
                var at = new Dictionary<int, Reward>(); int pos = 0;   // 367c: pozycja wpisu (jak przy zapisie - kazdy niepusty) -> nagroda
                foreach (var p in f[3].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); long v; if (x.Length == 3 && long.TryParse(x[2], NumberStyles.Integer, Inv, out v) && v > 0) { var rw = new Reward { Hero = x[0], Kingdom = x[1], Left = v }; _rewards.Add(rw); at[pos] = rw; _importN++; } else _importBad++; pos++; }
                foreach (var p in f[4].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); long v; if (x.Length == 2 && long.TryParse(x[1], NumberStyles.Integer, Inv, out v) && v > 0) { _acc[x[0]] = new Acc { Carry = v }; _importN++; } else _importBad++; }
                if (f.Length >= 7)   // 367c (zapis sprzed 367c - brak pol: nagrody bez przypisania, wyplacone nieznane)
                {
                    foreach (var p in f[5].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); int ix; Reward rw; if (x.Length == 2 && int.TryParse(x[0], NumberStyles.Integer, Inv, out ix) && at.TryGetValue(ix, out rw)) { rw.Captive = x[1]; _importN++; } else _importBad++; }
                    foreach (var p in f[6].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); long v; if (x.Length == 2 && long.TryParse(x[1], NumberStyles.Integer, Inv, out v) && v > 0) { _paidFor[x[0]] = v; _importN++; } else _importBad++; }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
