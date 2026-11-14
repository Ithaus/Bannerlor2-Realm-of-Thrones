using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 510 (perki, audyt docs/audyty-2026-11-14/PERKI.md; decyzje Jeffa 14.11 PERKI-DECYZJE.md, punkty 49, 50, 52-55, 67, 96, 104) - OKUPY I ZLOTO Z PLATNIKIEM.
    /// Kazdy punkt ma wlasny klucz (MCM "The law of the battlefield"); klucz NIE = gra / nasz kod jak przed 510, co do znaku.
    ///  49/96 PerkRansomByCaptor: okup 178 (Ransom178.PriceOf - jedna cena wszystkich drog: AI-AI, kurier, posrednik, ekran druzyny, barter, 384) gubil premie
    ///        porywacza: Ransom Broker (gra DefaultRansomValueCalculationModel.cs:40-48: +PrimaryBonus (25%), polowa na morzu, tylko gdy sprzedajacy prowadzi
    ///        partie) i BK Kidnapper (BK VanillaModelTweakPatches.cs:1229-1232: +30% za lorda, perk stylu zycia BK). Teraz mnoznik w PriceOf wedlug porywacza =
    ///        wodz partii, w ktorej jeniec siedzi (osada: jej wlasciciel). Placi rod jenca (krol - skarbiec) ta sama droga co reszte okupu (gotowka + dlug 168).
    ///  55 PerkManOfMeansRansom: okup gracza 178 (Ransom178.PlayerPrice; RC RansomAmountPatch podmienia kwote gry) gubil Man of Means (gra PlayerCaptivity.cs:171:
    ///        x (1 + SecondaryBonus) = x0.7). Teraz x0.7 w PlayerPrice i w PriceOf(gracz) (384).
    ///  50 PerkManhunterAfterBk: BK (VanillaModelTweakPatches.cs:1215-1225) w mieście z polityka Enslavement podmienia cene szeregowego jenca na cene niewolnika -
    ///        premia Manhunter gry (+20%, sprzedajacy na ladzie) ginie. Postfiks po BK (Low; RC Last biegnie po nas): gdy BK zmienil wynik gry - x1.2 na tym samym
    ///        warunku co gra. Placi kupiec: zolnierzy w SellPrisonersAction placi osada kupujaca (RealPayer KPris, z tych samych wywolan modelu).
    ///  52 PerkSpringOfGoldFromMerchants: odsetki Spring of Gold (gra DefaultClanFinanceModel.cs:160-164: 0.1% kiesy glowy rodu dziennie, do 1 000; kazdy rod, takze
    ///        AI) z niczego. Teraz placa kiesy kupcow (Town.Gold) miast, w ktorych bohaterowie rodu maja warsztaty (wedlug liczby warsztatow, kazde miasto najwyzej
    ///        tyle, ile ma; miasta wroga rodu pomijane). Czego kupcy nie maja - odsetek nie ma (ujemny wpis tej samej linii). Postfiks
    ///        CalculateClanIncomeInternal (w kampanii): przy applyWithdrawals = false tylko odczyt (podglad, straznik m105 - KtKnownPatches), przy true kasy miast
    ///        schodza w modelu (jak wyplaty warsztatow gry).
    ///  53/54 PerkTradeGiftsPaid: Insurance Plans (5 000 za zniszczona karawane) - wyplaca gildia (kiesa kupcow) miasta macierzystego karawany do wysokosci kiesy;
    ///        Rapid Development (5 000 za warsztat w miescie zdobytym przez wroga) - odkup placi nowy pan miasta z wlasnej kiesy (najwyzej tyle, ile ma).
    ///        Gracz i AI ta sama regula (dotad: AI 0 przez SmallGold TradePerkGiftsAiOff, gracz z niczego). Wpiecie: SmallGold.GivePrefix (ten sam okno i dopasowanie).
    ///  104 PerkNavalPortGoldPaid: NavalDLC ShipTradeCampaignBehavior.OnSettlementEntered (:428-458): Salvage - licznik ceł miasta +40 za kazdy konwoj (z niczego),
    ///        ShipwrightsHand - rekrut do zalogi z niczego. Teraz: Salvage placi kasa konwoju (najwyzej tyle, ile ma; jak myto Tollgates w RealPayer),
    ///        ShipwrightsHand wylaczony (licznik "rekrut z niczego - nie dany"). MerchantPrince i MasterShipwright juz placi nadwyzka portu (181 ShipTradeThroughPort,
    ///        ShipsToPort181.P_Between) - bez zmian.
    ///  67 PerkVeterinarianHorses: druga premia Veterinarian (gra CampaignBattleRecoveryBehavior.cs:83-104: 50% szansy na konia ze wzorca za kazdego zabitego
    ///        konnego - kon z niczego, obok koni zwracanych przez HorseFate) wylaczona; zamiast niej padly kon partii z perkiem (chirurg, nie na morzu) z szansa
    ///        SecondaryBonus (50%) jest tylko ranny i wraca - nie schodzi ze zbrojowni w HorseFate (misja: droga wlasciciela; autobitwa: AutoDeaths).
    /// Wyjatek per zdarzenie - licznik potkniec (pierwszy z miejsca do logu jako ERROR), bez globalnego wylacznika. Nic do zapisu (liczniki doby).
    /// Linia startu "Perki 510 (okupy i zloto): start" i linia doby "Perki 510 (okupy i zloto): dzien N".
    /// </summary>
    internal static class Perk510Gold
    {
        private static Settings S { get { return Settings.Current; } }
        internal static bool RansomOn { get { var s = S; return s != null && s.PerkRansomByCaptor; } }
        internal static bool MeansOn { get { var s = S; return s != null && s.PerkManOfMeansRansom; } }
        internal static bool HunterOn { get { var s = S; return s != null && s.PerkManhunterAfterBk; } }
        internal static bool SpringOn { get { var s = S; return s != null && s.PerkSpringOfGoldFromMerchants; } }
        internal static bool GiftsOn { get { var s = S; return s != null && s.PerkTradeGiftsPaid; } }
        internal static bool NavalOn { get { var s = S; return s != null && s.PerkNavalPortGoldPaid; } }
        internal static bool VetOn { get { var s = S; return s != null && s.PerkVeterinarianHorses; } }

        // ------------------------------------------------------------ liczniki doby
        private static readonly HashSet<string> _rCaptives = new HashSet<string>(), _rKidnap = new HashSet<string>(), _rBroker = new HashSet<string>();
        private static int _meansN, _hunterN, _hunterSea;
        private static long _hunterAdd;
        private static int _springClans, _springShort, _springNoShop;
        private static long _springDue, _springPaid;
        private static int _insN, _insShort, _rapidN, _rapidShort, _rapidPlayerPays;
        private static long _insDue, _insPaid, _rapidDue, _rapidPaid;
        private static int _salvN, _salvShort, _handN, _handTroops;
        private static long _salvDue, _salvPaid;
        private static int _vetSaved, _vetLost, _vetGameOff, _vetGameRiders;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startLogged;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("Perk510Gold." + where, e); } catch { }
        }

        // ================================================================ 49/96 + 55: mnoznik okupu 178
        /// <summary>Ransom178.PriceOf: mnoznik ceny jenca - premie porywacza (Ransom Broker, BK Kidnapper) i, gdy jencem jest gracz, Man of Means.
        /// Klucze wylaczone - 1 (cena jak dotad). Nie rzuca.</summary>
        internal static double RansomFactor(Hero captive)
        {
            double f = 1.0;
            try
            {
                if (captive == null) return 1.0;
                if (RansomOn) f *= CaptorFactor(captive);
                if (captive == Hero.MainHero) f *= MeansFactor();
            }
            catch (Exception e) { Stumble("RansomFactor", e); return 1.0; }
            return f;
        }

        /// <summary>Ransom178.PlayerPrice: Man of Means gracza (x (1 + SecondaryBonus) - jak gra PlayerCaptivity.cs:171). Klucz wylaczony - 1.</summary>
        internal static double PlayerRansomFactor()
        {
            try { return MeansFactor(); }
            catch (Exception e) { Stumble("PlayerRansomFactor", e); return 1.0; }
        }

        private static int _meansDay = -1;
        private static double MeansFactor()
        {
            var me = Hero.MainHero;
            if (!MeansOn || me == null || !me.GetPerkValue(DefaultPerks.Trade.ManOfMeans)) return 1.0;
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _meansDay) { _meansDay = d; _meansN++; }   // raz na dobe do linii (wycena wolana wiele razy)
            return Math.Max(0.0, 1.0 + DefaultPerks.Trade.ManOfMeans.SecondaryBonus);
        }

        /// <summary>Porywacz: wodz partii, w ktorej jeniec siedzi; jeniec w osadzie - wlasciciel osady.</summary>
        private static Hero CaptorOf(Hero captive)
        {
            var pb = captive.PartyBelongedToAsPrisoner;
            if (pb == null) return null;
            if (pb.IsMobile && pb.MobileParty != null) return pb.MobileParty.LeaderHero;
            if (pb.IsSettlement && pb.Settlement != null) return pb.Settlement.Owner;
            return null;
        }

        private static double CaptorFactor(Hero captive)
        {
            var c = CaptorOf(captive);
            if (c == null || c == captive) return 1.0;
            double add = 0;
            bool broker = false, kid = false;
            // gra (DefaultRansomValueCalculationModel.cs:40-48): sprzedajacy prowadzi partie i ma perk; na morzu polowa
            if (c.IsPartyLeader && c.GetPerkValue(DefaultPerks.Roguery.RansomBroker))
            {
                double b = DefaultPerks.Roguery.RansomBroker.PrimaryBonus;
                var mp = c.PartyBelongedTo;
                if (mp != null && mp.IsCurrentlyAtSea) b *= 0.5;
                add += b; broker = true;
            }
            // BK (VanillaModelTweakPatches.cs:1229-1232): +30% za bohatera, perk stylu zycia Kidnapper sprzedajacego
            if (BkKidnapper(c)) { add += 0.3; kid = true; }
            if (add <= 0) return 1.0;
            try
            {
                string id = captive.StringId ?? "";
                _rCaptives.Add(id);
                if (broker) _rBroker.Add(id);
                if (kid) _rKidnap.Add(id);
            }
            catch { }
            // BK dolicza swoje 30% do wyniku gry z premia brokera (kolejno), wiec iloczyn - ta sama kolejnosc co gra + BK
            double f = 1.0;
            if (broker) f *= 1.0 + DefaultPerks.Roguery.RansomBroker.PrimaryBonus * (c.PartyBelongedTo != null && c.PartyBelongedTo.IsCurrentlyAtSea ? 0.5 : 1.0);
            if (kid) f *= 1.3;
            return f;
        }

        // BK przez odbicie (raz na proces): BannerKingsConfig.Instance.EducationManager.GetHeroEducation(h).HasPerk(LifestyleOutlawKidnapper)
        private static bool _bkTried;
        private static PropertyInfo _bkInst, _bkEdu;
        private static MethodInfo _bkGetEdu, _bkHas;
        private static PerkObject _bkKid;

        private static bool BkKidnapper(Hero h)
        {
            try
            {
                if (!_bkTried)
                {
                    _bkTried = true;
                    var cfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    var mgr = AccessTools.TypeByName("BannerKings.Managers.EducationManager");
                    var dat = AccessTools.TypeByName("BannerKings.Managers.Education.EducationData");
                    _bkInst = cfg != null ? AccessTools.Property(cfg, "Instance") : null;
                    _bkEdu = cfg != null ? AccessTools.Property(cfg, "EducationManager") : null;
                    _bkGetEdu = mgr != null ? AccessTools.Method(mgr, "GetHeroEducation", new[] { typeof(Hero) }) : null;
                    _bkHas = dat != null ? AccessTools.Method(dat, "HasPerk", new[] { typeof(PerkObject) }) : null;
                    try { _bkKid = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<PerkObject>("LifestyleOutlawKidnapper") : null; } catch { _bkKid = null; }
                }
                if (_bkKid == null && MBObjectManager.Instance != null) _bkKid = MBObjectManager.Instance.GetObject<PerkObject>("LifestyleOutlawKidnapper");
                if (_bkInst == null || _bkEdu == null || _bkGetEdu == null || _bkHas == null || _bkKid == null) return false;
                var inst = _bkInst.GetValue(null, null);
                var mgrI = inst != null ? _bkEdu.GetValue(inst, null) : null;
                var edu = mgrI != null ? _bkGetEdu.Invoke(mgrI, new object[] { h }) : null;
                return edu != null && (bool)_bkHas.Invoke(edu, new object[] { _bkKid });
            }
            catch (Exception e) { Stumble("BkKidnapper", e); return false; }
        }

        // ================================================================ 50: Manhunter po BK
        [ThreadStatic] private static CharacterObject _hPris;
        [ThreadStatic] private static int _hV0;

        /// <summary>Postfiks First na DefaultRansomValueCalculationModel.PrisonerRansomValue: wynik gry (z Manhunter) przed BK.</summary>
        public static void HunterFirst(CharacterObject __0, int __result)
        {
            _hPris = __0; _hV0 = __result;
        }

        /// <summary>Postfiks Low (po BK Normal, przed RC Last): BK podmienil cene szeregowego na cene niewolnika - premia Manhunter na warunku gry.</summary>
        public static void HunterLow(CharacterObject __0, Hero __1, ref int __result)
        {
            try
            {
                var pris = _hPris; int v0 = _hV0;
                _hPris = null;
                if (!HunterOn || __0 == null || __0.IsHero || __1 == null || !ReferenceEquals(pris, __0)) return;
                if (__result == v0) return;                                       // BK nie podmienil - premia gry juz w wyniku
                if (!__1.GetPerkValue(DefaultPerks.Roguery.Manhunter)) return;
                var mp = __1.PartyBelongedTo;
                if (mp == null) return;                                            // ten sam warunek co gra (:35)
                if (mp.IsCurrentlyAtSea) { _hunterSea++; return; }
                int was = __result;
                __result = MathF.Round(__result + __result * DefaultPerks.Roguery.Manhunter.PrimaryBonus);
                _hunterN++; _hunterAdd += __result - was;
            }
            catch (Exception e) { Stumble("HunterLow", e); }
        }

        // ================================================================ 52: Spring of Gold z kies kupcow
        /// <summary>Postfiks DefaultClanFinanceModel.CalculateClanIncomeInternal (w kampanii). applyWithdrawals (__2) = false - tylko odczyt.</summary>
        public static void SpringPostfix(Clan __0, ref ExplainedNumber __1, bool __2)
        {
            try
            {
                if (!SpringOn) return;
                var c = __0;
                if (c == null || c.IsEliminated) return;
                var leader = c.Leader;
                if (leader == null || !leader.GetPerkValue(DefaultPerks.Trade.SpringOfGold)) return;
                int due = MathF.Min(1000, MathF.Round((float)leader.Gold * DefaultPerks.Trade.SpringOfGold.PrimaryBonus));   // gra :162
                if (due <= 0) return;
                bool noShop;
                long paid = SpringPay(c, due, __2, out noShop);
                if (paid < due) __1.Add(-(due - paid), DefaultPerks.Trade.SpringOfGold.Name);
                if (__2)
                {
                    _springClans++; _springDue += due; _springPaid += paid;
                    if (paid < due) { _springShort++; if (noShop) _springNoShop++; }
                }
            }
            catch (Exception e) { Stumble("SpringPostfix", e); }
        }

        /// <summary>Kiesy kupcow miast z warsztatami bohaterow rodu: udzial wedlug liczby warsztatow, kazde miasto najwyzej swoja kiesa; reszta - druga runda.
        /// take = true - kasy schodza (Town.ChangeGold); false - tylko rachunek. Zwraca zaplacone.</summary>
        private static long SpringPay(Clan c, int due, bool take, out bool noShop)
        {
            noShop = false;
            var n = new Dictionary<Town, int>();
            int all = 0;
            var mf = c.MapFaction;
            foreach (var h in c.Heroes)
            {
                if (h == null || !h.IsAlive || h.OwnedWorkshops == null) continue;
                foreach (var w in h.OwnedWorkshops)
                {
                    var t = w != null && w.Settlement != null ? w.Settlement.Town : null;
                    if (t == null) continue;
                    var tf = t.Settlement != null ? t.Settlement.MapFaction : null;
                    if (mf != null && tf != null && mf.IsAtWarWith(tf)) continue;   // kupcy miasta wroga nie placa
                    int k; n.TryGetValue(t, out k); n[t] = k + 1; all++;
                }
            }
            if (all == 0) { noShop = true; return 0; }
            var have = new Dictionary<Town, long>();
            foreach (var kv in n) have[kv.Key] = Math.Max(0, kv.Key.Gold);
            long left = due, paid = 0;
            for (int round = 0; round < 2 && left > 0; round++)
            {
                long want = left;
                foreach (var kv in n)
                {
                    if (left <= 0) break;
                    long share = round == 0 ? (long)Math.Ceiling((double)want * kv.Value / all) : left;
                    long x = Math.Min(Math.Min(share, left), have[kv.Key]);
                    if (x <= 0) continue;
                    have[kv.Key] -= x; left -= x; paid += x;
                    if (take) kv.Key.ChangeGold(-(int)x);
                }
            }
            return paid;
        }

        // ================================================================ 53/54: Insurance Plans i Rapid Development z platnikiem
        /// <summary>SmallGold.GivePrefix (okno perka dopasowane: "nic -> wlasciciel" o kwocie premii). true - platnik ustawiony (dajacy i kwota), SmallGold nic wiecej
        /// nie robi; false - klucz wylaczony (SmallGold jak dotad). ins - Insurance Plans (home: miasto macierzyste karawany), inaczej Rapid Development (newOwner: nowy pan).</summary>
        internal static bool TradeGiftPayer(bool ins, Settlement home, Hero newOwner, Hero recipient, ref Hero giverHero, ref PartyBase giverParty, ref int amount)
        {
            if (!GiftsOn) return false;
            try
            {
                long due = amount, pay = 0;
                if (ins)
                {
                    var t = home != null && home.IsTown ? home.Town : null;
                    if (t != null && home.Party != null) { pay = Math.Min(due, Math.Max(0, t.Gold)); if (pay > 0) giverParty = home.Party; }
                    _insN++; _insDue += due; _insPaid += pay; if (pay < due) _insShort++;
                }
                else
                {
                    if (newOwner != null && newOwner != recipient) { pay = Math.Min(due, Math.Max(0, newOwner.Gold)); if (pay > 0) giverHero = newOwner; }
                    _rapidN++; _rapidDue += due; _rapidPaid += pay; if (pay < due) _rapidShort++;
                    if (newOwner == Hero.MainHero && pay > 0)
                    {
                        _rapidPlayerPays++;
                        Log.Player("You pay " + pay + " denars to " + (recipient != null ? recipient.Name.ToString() : "?") + " for a workshop in the town you have taken (Rapid Development).", true);
                    }
                }
                amount = (int)pay;                                               // 0 - zdarzenie z 0 (ksiega pomija), nic z niczego
                if (recipient == Hero.MainHero && pay < due)
                    Log.Player((ins ? "Your caravan's home guild" : "The new lord of the town") + " could pay only " + pay + " of " + due + " denars (" + (ins ? "Insurance Plans" : "Rapid Development") + ").", true);
                return true;
            }
            catch (Exception e) { Stumble("TradeGiftPayer", e); return false; }
        }

        // ================================================================ 104: NavalDLC Salvage / ShipwrightsHand
        private static PerkObject _pSalvage, _pHand;
        private static bool _navTried;

        private static void NavalPerks()
        {
            if (_navTried) return;
            _navTried = true;
            try
            {
                var t = AccessTools.TypeByName("NavalDLC.CharacterDevelopment.NavalPerks+Boatswain");
                var a = t != null ? AccessTools.Property(t, "Salvage") : null;
                var b = t != null ? AccessTools.Property(t, "ShipwrightsHand") : null;
                _pSalvage = a != null ? a.GetValue(null, null) as PerkObject : null;
                _pHand = b != null ? b.GetValue(null, null) as PerkObject : null;
            }
            catch (Exception e) { Stumble("NavalPerks", e); }
        }

        /// <summary>Prefiks ShipTradeCampaignBehavior.OnSettlementEntered (NavalDLC :428-458) - ta sama metoda bez wartosci z niczego: Salvage z kasy konwoju,
        /// ShipwrightsHand bez rekruta. Klucz wylaczony albo brak perkow NavalDLC - gra (true). Wyjatek - gra (true), licznik.</summary>
        public static bool NavalEnteredPrefix(MobileParty __0, Settlement __1)
        {
            if (!NavalOn) return true;
            try
            {
                NavalPerks();
                if (_pSalvage == null || _pHand == null) return true;
                var mp = __0; var st = __1;
                if (mp == null || !mp.IsCaravan || !mp.HasNavalNavigationCapability || st == null || !st.IsTown || st.Town == null || st.Town.Governor == null) return false;   // gra: nic
                var gov = st.Town.Governor;
                if (gov.GetPerkValue(_pSalvage))
                {
                    int due = MathF.Round(_pSalvage.SecondaryBonus);
                    int pay = Math.Max(0, Math.Min(due, mp.PartyTradeGold));
                    if (pay > 0) { mp.PartyTradeGold -= pay; st.Town.TradeTaxAccumulated += pay; }
                    _salvN++; _salvDue += due; _salvPaid += pay; if (pay < due) _salvShort++;
                }
                if (gov.GetPerkValue(_pHand) && st.MapFaction != null && st.MapFaction.BasicTroop != null)
                {
                    int wage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(st.MapFaction.BasicTroop);
                    if (st.GarrisonWagePaymentLimit > wage + 5) { _handN++; _handTroops += Math.Max(0, MathF.Round(_pHand.SecondaryBonus)); }   // gra by dopisala (najwyzej tyle)
                }
                return false;
            }
            catch (Exception e) { Stumble("NavalEnteredPrefix", e); return true; }
        }

        // ================================================================ 67: Veterinarian
        /// <summary>Prefiks CampaignBattleRecoveryBehavior.RecoverMountWithChance: kon ze wzorca (z niczego) - nie dany (licznik). Klucz wylaczony - gra.</summary>
        public static bool VetGamePrefix(int __1)
        {
            if (!VetOn) return true;
            _vetGameOff++; _vetGameRiders += Math.Max(0, __1);
            return false;
        }

        /// <summary>HorseFate (zgon konia partii): partia z perkiem Veterinarian (chirurg; nie na morzu) - z szansa SecondaryBonus kon tylko ranny, wraca
        /// (true - nie zdejmowac ze zbrojowni). Klucz wylaczony - false.</summary>
        internal static bool VetSaves(MobileParty p)
        {
            try
            {
                if (!VetOn || p == null || p.IsCurrentlyAtSea || !p.HasPerk(DefaultPerks.Medicine.Veterinarian)) return false;
                if (MBRandom.RandomFloat < DefaultPerks.Medicine.Veterinarian.SecondaryBonus) { _vetSaved++; return true; }
                _vetLost++;
                return false;
            }
            catch (Exception e) { Stumble("VetSaves", e); return false; }
        }

        // ================================================================ wpiecia
        private static Harmony _h;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static string _wiredText = "-", _missingText = "-";
        private static bool _hooksTried;

        private static void Patch(string label, MethodBase m, string pre, string post, int prio)
        {
            try
            {
                if (_h == null) { _missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { _missing.Add(label); return; }
                _h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(Perk510Gold), pre) { priority = prio } : null,
                            postfix: post != null ? new HarmonyMethod(typeof(Perk510Gold), post) { priority = prio } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        /// <summary>SubModuleMain (po SmallGold): latki na typach gry i NavalDLC (wylaczniki czytane na zywo).</summary>
        internal static void ApplyAll(Harmony h)
        {
            _h = h;
            _wired.Clear(); _missing.Clear();
            // 50: dwa postfiksy na metodzie deklarujacej (BK latkuje DefaultRansomValueCalculationModel - ta sama metoda)
            var rv = AccessTools.DeclaredMethod(typeof(DefaultRansomValueCalculationModel), "PrisonerRansomValue");
            Patch("Manhunter - wynik gry (PrisonerRansomValue, First)", rv, null, nameof(HunterFirst), Priority.First);
            Patch("Manhunter - po BK (PrisonerRansomValue, Low)", rv, null, nameof(HunterLow), Priority.Low);
            // 104
            var nt = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior");
            Patch("NavalDLC Salvage / ShipwrightsHand (ShipTradeCampaignBehavior.OnSettlementEntered)",
                  nt != null ? AccessTools.DeclaredMethod(nt, "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) }) : null,
                  nameof(NavalEnteredPrefix), null, Priority.Normal);
            // 67
            Patch("Veterinarian - kon ze wzorca (CampaignBattleRecoveryBehavior.RecoverMountWithChance)",
                  AccessTools.DeclaredMethod(typeof(CampaignBattleRecoveryBehavior), "RecoverMountWithChance"), nameof(VetGamePrefix), null, Priority.Normal);
            _wiredText = _wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-";
            _missingText = _missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-";
            Log.Info("Perki 510 (okupy i zloto): latki przy starcie gry - wpiete: " + _wiredText + "; BRAK: " + _missingText
                     + "; Spring of Gold - w kampanii (CalculateClanIncomeInternal); Ransom Broker / Kidnapper / Man of Means - w cenie 178 (Ransom178.PriceOf);"
                     + " Insurance Plans / Rapid Development - przez SmallGold.GivePrefix; Veterinarian - przez HorseFate.");
        }

        /// <summary>ArmouryBehavior.OnSessionLaunched: latka modelu finansow (pulapka CLAUDE.md - dopiero w kampanii), raz na proces.</summary>
        internal static void EnsureHooks()
        {
            if (_hooksTried || _h == null || Campaign.Current == null) return;
            _hooksTried = true;
            string r = "BRAK";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultClanFinanceModel), "CalculateClanIncomeInternal");
                if (m != null) { _h.Patch(m, postfix: new HarmonyMethod(typeof(Perk510Gold), nameof(SpringPostfix))); r = "wpiety"; }
            }
            catch (Exception e) { r = "BLAD"; Log.Error("Perk510Gold.EnsureHooks(CalculateClanIncomeInternal)", e); }
            _springWire = r;
            Log.Info("Perki 510 (okupy i zloto): Spring of Gold (DefaultClanFinanceModel.CalculateClanIncomeInternal) " + r + ".");
        }
        private static string _springWire = "nie probowano";

        // ================================================================ linia doby
        internal static void Daily()
        {
            try
            {
                var s = S;
                if (Campaign.Current == null || s == null || !s.LogEnabled) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info("Perki 510 (okupy i zloto): start | klucze: Perk Ransom By Captor " + YN(RansomOn) + ", Perk Man Of Means Ransom " + YN(MeansOn)
                             + ", Perk Manhunter After Bk " + YN(HunterOn) + ", Perk Spring Of Gold From Merchants " + YN(SpringOn) + ", Perk Trade Gifts Paid " + YN(GiftsOn)
                             + ", Perk Naval Port Gold Paid " + YN(NavalOn) + ", Perk Veterinarian Horses " + YN(VetOn) + " | latki: " + _wiredText + "; BRAK: " + _missingText
                             + "; Spring of Gold " + _springWire + " | BK Kidnapper: " + (_bkTried ? (_bkHas != null && _bkKid != null ? "odbicie gotowe" : "BRAK (BK albo perk)") : "jeszcze nie sprawdzany")
                             + " | NavalDLC perki: " + (_navTried ? (_pSalvage != null && _pHand != null ? "tak" : "BRAK") : "jeszcze nie sprawdzane") + ".");
                }
                var sb = new StringBuilder(700);
                sb.Append("Perki 510 (okupy i zloto): dzien ").Append(day);
                sb.Append(" | okup 178 z premia porywacza: jency ").Append(_rCaptives.Count).Append(" (Ransom Broker ").Append(_rBroker.Count).Append(", BK Kidnapper ").Append(_rKidnap.Count)
                  .Append("); Man of Means gracza (x0.7) - dob z wycena ").Append(_meansN);
                sb.Append(" | Manhunter po BK: ").Append(_hunterN).Append(" wycen, +").Append(_hunterAdd).Append(" (placi kupujacy - RealPayer), na morzu bez premii ").Append(_hunterSea);
                sb.Append(" | Spring of Gold z kies kupcow: rody ").Append(_springClans).Append(", nalezne ").Append(_springDue).Append(", zaplacone ").Append(_springPaid)
                  .Append(" (niedoplata u ").Append(_springShort).Append(", w tym bez warsztatow ").Append(_springNoShop).Append(')');
                sb.Append(" | Insurance Plans z gildii miasta karawany: ").Append(_insN).Append(", nalezne ").Append(_insDue).Append(", zaplacone ").Append(_insPaid).Append(" (niedoplata ").Append(_insShort).Append(')');
                sb.Append(" | Rapid Development od nowego pana: ").Append(_rapidN).Append(", nalezne ").Append(_rapidDue).Append(", zaplacone ").Append(_rapidPaid).Append(" (niedoplata ").Append(_rapidShort)
                  .Append(", placil gracz ").Append(_rapidPlayerPays).Append(')');
                sb.Append(" | NavalDLC Salvage z kasy konwoju: ").Append(_salvN).Append(", nalezne ").Append(_salvDue).Append(", zaplacone ").Append(_salvPaid).Append(" (niedoplata ").Append(_salvShort)
                  .Append("); ShipwrightsHand - rekrut z niczego nie dany: ").Append(_handN).Append(" wjazdow, do ").Append(_handTroops).Append(" ludzi");
                sb.Append(" | Veterinarian: kon ze wzorca nie dany ").Append(_vetGameOff).Append(" razy (zabitych konnych ").Append(_vetGameRiders).Append("); padle konie partii z perkiem - ranne, wrocily ")
                  .Append(_vetSaved).Append(", padly ").Append(_vetLost);
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { Zero(); }
        }

        private static string YN(bool b) { return b ? "tak" : "nie"; }

        private static void Zero()
        {
            _rCaptives.Clear(); _rKidnap.Clear(); _rBroker.Clear();
            _meansN = _hunterN = _hunterSea = 0; _hunterAdd = 0;
            _springClans = _springShort = _springNoShop = 0; _springDue = _springPaid = 0;
            _insN = _insShort = _rapidN = _rapidShort = _rapidPlayerPays = 0; _insDue = _insPaid = _rapidDue = _rapidPaid = 0;
            _salvN = _salvShort = _handN = _handTroops = 0; _salvDue = _salvPaid = 0;
            _vetSaved = _vetLost = _vetGameOff = _vetGameRiders = 0;
            _stumbles = 0;
        }
    }
}
