using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E8/H (271; decyzja Jeffa 12.10, projekt etapu 8 rozdz. 6 odp. 7; rozpoznanie docs/etap8/rozpoznanie-H-jezdziec-uprzaz.md, wariant A) -
    /// JEZDZIEC = KON + UPRZAZ. "Sam kon nie wystarczy - musi miec tez uprzaz (siodlo), zeby wojsko na nim jezdzilo i mozna bylo stworzyc jezdzca."
    /// Dotad uprzaz byla wymagana w jednym miejscu (notabl awansujacy ochotnika na konnego - VolunteerKit); wszedzie indziej wystarczal sam kon.
    /// JEDNA REGULA (klucz RiderNeedsHarness; dziala tam, gdzie kon jest sztuka zbrojowni - CavalryNeedsMounts i TroopsFightWithOwnKitOnly):
    ///  - "uprzaz" = dowolna sztuka HorseHarness rodziny konia (tak paruje DTE: ArmorComponent.FamilyType = rodzina potwora konia; samo siodlo bez pancerza
    ///    pasuje do kazdego) - najtansze siodlo robi jezdzca, lader wzorca to juz "lepsze" (E6/R3 kupuje go po staremu);
    ///  - objeci regula: szeregowi, ktorych wzorzec ma zwyklego wierzchowca (Stables.IsPlainMount) i uprzaz; wzorzec bez uprzezy (jednorozce Skagosu) jedzie
    ///    bez niej; wielblady, slonie, mamuty, smoki - poza Stajnia jak dotad (MountLaw); bohaterowie poza regula;
    ///  - stan pary liczy JEDNA funkcja Pairs (konie zbrojowni, uprzaz rodziny konia, jezdzcy) - obok Stables.Balance, ktorego nie zmienia (kon bez siodla
    ///    dalej je pasze i dalej jest koniem: stan koni, pasza, kolumna marszu, zapas koni K2 i zgony koni K1 bez zmian).
    /// Punkty wpiecia: (1) tworzenie jezdzca - awans AI (Stables.FilterTargets / PayInHorses), awans gracza (PartyScreenLogic.ValidateCommand, zatwierdzenie
    /// ekranu w Stables.BankPaidHorses), zamiana ROT (RotHorseGuard), najemnik z karczmy (RecruitKit.MercHorseKit + pula weteranow HorseFate);
    /// (2) bitwa gracza - GarrisonKit.SaddleGuard: kon bez uprzezy po strazy "nic z niczego" schodzi, jedna regula dla druzyny gracza, lordow AI i zalog;
    /// (3) zakup - AiGear.BuySaddles: lord AI kupuje najtansze pasujace siodlo do zapasu (ludzie, ktorzy maja na czym usiasc, + luz AiSpareHorseShare),
    /// brak na polce = zamowienie dla siodlarzy; (4) uprzaz idzie za koniem - weteran zabiera pare do puli karczmy, najemnik dostaje pare z puli (koniec
    /// rzedu ze wzorca z niczego), uprzaz zbrojowni rozbitej partii idzie z konmi do zwyciezcow albo na polke (HorseFate.Spoils).
    /// Zamknieta ekonomia: sztuka polka miasta -> zbrojownia partii (zloto sakiewka ludzi, potem kiesa pana -> kasa miasta, droga AiGear); tabor -> zbrojownia
    /// tej samej partii; zbrojownia -> pula weteranow miasta -> zbrojownia najemcy albo polka; nic z niczego, nic w nicosc. Bez zapisu (para liczona ze
    /// zbrojowni na biezaco; pula weteranow - istniejacy zapis HorseFate, te same wiersze "przedmiot=ile"). Linia doby "Jezdzcy i uprzaz (E8/H)" takze przy
    /// wylaczonym kluczu (spis par to pomiar "przed"). Stan sesji czyszczony w Reset (z Stables.Reset - StateHygiene).
    /// </summary>
    internal static class SaddleLaw
    {
        /// <summary>Koszyk zakupu siodla w AiGear (typ uprzezy x tier 0 - poza koszykami E6/R3, ktore maja tier 1-6): odroznia zakup E8/H w dostawie.</summary>
        internal const int Bucket = (int)ItemObject.ItemTypeEnum.HorseHarness * 10;
        /// <summary>Tyle dob po sprzedazy uprzezy zakup siodla tej samej partii liczy sie jako "kupno po sprzedazy" (prog H-6; tylko licznik).</summary>
        private const int RebuyDays = 7;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.RiderNeedsHarness; } }
        /// <summary>Regula czynna: klucz + kon jako sztuka zbrojowni (awans wymaga konia, wojsko walczy tylko tym, co ma) - bez tego DTE dopelnia rzad ze wzorca.</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.RiderNeedsHarness && s.CavalryNeedsMounts && GarrisonKit.OwnKitOn; } }

        // ------------------------------------------------------------ rodzina i wzorzec
        private static int _horseFam = -1;
        // 0 inne, 1 kon pod siodlo, 2 uprzaz rodziny konia, 3 uprzaz innej rodziny
        private static readonly Dictionary<ItemObject, byte> _itemKind = new Dictionary<ItemObject, byte>();
        // 0 nie jezdziec zwyklego wierzchowca, 1 jezdziec ze wzorcem bez uprzezy (wyjatek z danych), 2 jezdziec z uprzeza we wzorcu (objety regula)
        private static readonly Dictionary<CharacterObject, byte> _troopKind = new Dictionary<CharacterObject, byte>();
        private static readonly object _lock = new object();

        /// <summary>Rodzina potwora zwyklego konia (dane gry: 1) - z pierwszego wierzchowca pod siodlo w spisie przedmiotow.</summary>
        internal static int HorseFamily()
        {
            if (_horseFam >= 0) return _horseFam;
            int f = 1;
            try
            {
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!Stables.IsPlainMount(it)) continue;
                    f = it.HorseComponent.Monster.FamilyType;
                    break;
                }
            }
            catch { f = 1; }
            _horseFam = f;
            return f;
        }

        private static byte ItemKind(ItemObject it)
        {
            if (it == null) return 0;
            var ty = it.ItemType;
            if (ty != ItemObject.ItemTypeEnum.Horse && ty != ItemObject.ItemTypeEnum.HorseHarness) return 0;
            byte k;
            lock (_lock)
            {
                if (_itemKind.TryGetValue(it, out k)) return k;
                k = 0;
                try
                {
                    if (ty == ItemObject.ItemTypeEnum.Horse) k = (byte)(Stables.IsPlainMount(it) ? 1 : 0);
                    else if (CampCart.IsPackSaddle(it)) k = 3;   // m97-oboz: siodlo transportowe (juczne) nie jest siodlem bojowym - nie robi jezdzca
                    else
                    {
                        // jak DTE PartyEquipmentDistributor.PopulateDictionaries: uprzaz z pancerzem - wedlug rodziny; samo siodlo (bez pancerza) - do kazdego konia
                        var ac = it.ArmorComponent;
                        if (ac == null) k = (byte)(it.HasSaddleComponent ? 2 : 3);
                        else k = (byte)(ac.FamilyType == HorseFamily() ? 2 : 3);
                    }
                }
                catch { k = 0; }
                _itemKind[it] = k;
            }
            return k;
        }

        /// <summary>Uprzaz, ktora DTE zalozy na zwyklego konia (rodzina konia albo samo siodlo).</summary>
        internal static bool Fits(ItemObject harness) { return ItemKind(harness) == 2; }

        /// <summary>Uprzaz pasujaca do TEGO wierzchowca (bitwa: sloty 10 i 11 czlowieka).</summary>
        internal static bool Fits(ItemObject harness, ItemObject horse)
        {
            try
            {
                if (harness == null || horse == null || harness.ItemType != ItemObject.ItemTypeEnum.HorseHarness) return false;
                var ac = harness.ArmorComponent;
                if (ac == null) return harness.HasSaddleComponent;
                var hc = horse.HorseComponent;
                return hc != null && hc.Monster != null && hc.Monster.FamilyType == ac.FamilyType;
            }
            catch { return false; }
        }

        private static byte TroopKind(CharacterObject ch)
        {
            if (ch == null || ch.IsHero) return 0;
            byte k;
            lock (_lock)
            {
                if (_troopKind.TryGetValue(ch, out k)) return k;
                k = 0;
                try
                {
                    // ten sam wzorzec co dorobek startowy (ColdStart.FillToTemplate) i uprzaz E6/R3: pierwszy zestaw bojowy jednostki
                    var eq = ch.Equipment;
                    if (eq != null && Stables.IsPlainMount(eq[EquipmentIndex.Horse].Item))
                    {
                        var h = eq[EquipmentIndex.HorseHarness].Item;
                        k = (byte)(h != null && h.ItemType == ItemObject.ItemTypeEnum.HorseHarness ? 2 : 1);
                    }
                }
                catch { k = 0; }
                _troopKind[ch] = k;
            }
            return k;
        }

        /// <summary>Szeregowy objety regula: wzorzec ma zwyklego wierzchowca i uprzaz.</summary>
        internal static bool Needs(CharacterObject ch) { return TroopKind(ch) == 2; }

        // ------------------------------------------------------------ stan pary
        /// <summary>Stan par partii: jezdzcy objeci regula wobec koni i uprzezy rodziny konia w zbrojowni.</summary>
        internal sealed class PairState
        {
            public int Riders;         // objeci regula (z rannymi)
            public int Exempt;         // jezdzcy zwyklego wierzchowca ze wzorcem bez uprzezy - jada bez
            public int Horses;         // konie pod siodlo w zbrojowni
            public int Harness;        // uprzaz rodziny konia w zbrojowni
            public int HarnessOther;   // uprzaz innej rodziny (wielblad, slon)
            public int Mounted;        // objeci regula z koniem
            public int Paired;         // z para
            public int NoSaddle;       // z koniem bez uprzezy
            public int NoHorse;        // bez konia (objeci regula i wyjatki)
            public int FreeHarness;    // uprzaz ponad jezdzcow z koniem
        }

        /// <summary>
        /// JEDNA funkcja stanu pary. Konie dostaja najpierw jezdzcy objeci regula (DTE rozdaje pary z uprzeza pierwsze), wyjatki - z reszty koni.
        /// adjust - poprawka liczby ludzi wedlug typu, jak w Stables.Balance (dodatnia: jezdzcy, ktorzy zaraz dojda - awans; ujemna: jezdzcy, ktorzy wlasnie
        /// weszli i nie sa jeszcze rozliczeni - zamiana ROT, albo odchodza - weteran).
        /// </summary>
        internal static PairState Pairs(TroopRoster roster, Dictionary<ItemObject, int> arm, Dictionary<CharacterObject, int> adjust = null)
        {
            var p = new PairState();
            try
            {
                if (arm != null)
                    foreach (var kv in arm)
                    {
                        if (kv.Key == null || kv.Value <= 0) continue;
                        byte k = ItemKind(kv.Key);
                        if (k == 1) p.Horses += kv.Value;
                        else if (k == 2) p.Harness += kv.Value;
                        else if (k == 3) p.HarnessOther += kv.Value;
                    }
                int needy = 0, exempt = 0;
                if (roster != null)
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.Character == null || el.Number <= 0) continue;
                        byte k = TroopKind(el.Character);
                        if (k == 2) needy += el.Number; else if (k == 1) exempt += el.Number;
                    }
                if (adjust != null)
                    foreach (var kv in adjust)
                    {
                        byte k = TroopKind(kv.Key);
                        if (k == 2) needy += kv.Value; else if (k == 1) exempt += kv.Value;
                    }
                if (needy < 0) needy = 0;
                if (exempt < 0) exempt = 0;
                p.Riders = needy; p.Exempt = exempt;
                p.Mounted = Math.Min(needy, p.Horses);
                int exMounted = Math.Min(exempt, p.Horses - p.Mounted);
                p.Paired = Math.Min(p.Mounted, p.Harness);
                p.NoSaddle = p.Mounted - p.Paired;
                p.NoHorse = (needy - p.Mounted) + (exempt - exMounted);
                p.FreeHarness = Math.Max(0, p.Harness - p.Mounted);
            }
            catch (Exception e) { Stumble("Pairs", e); }
            return p;
        }

        internal static PairState Pairs(MobileParty mp, Dictionary<CharacterObject, int> adjust = null)
        {
            return mp != null ? Pairs(mp.MemberRoster, ArmOf(mp), adjust) : new PairState();
        }

        /// <summary>Zbrojownia partii: AI - slownik DTE; druzyna gracza - konie i uprzaz z ArmyArmory DTE (kopia do odczytu).</summary>
        private static Dictionary<ItemObject, int> ArmOf(MobileParty mp)
        {
            if (mp == null) return null;
            if (mp.IsMainParty) return PlayerArm();
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm;
            return all != null && all.TryGetValue(mp.Id, out arm) ? arm : null;
        }

        private static Dictionary<ItemObject, int> PlayerArm()
        {
            var r = QuartermasterLaw.DteArmory();
            if (r == null) return null;
            var d = new Dictionary<ItemObject, int>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || ItemKind(it) == 0) continue;
                int n; d.TryGetValue(it, out n); d[it] = n + el.Amount;
            }
            return d;
        }

        /// <summary>Uprzaz rodziny konia w taborze (sakwach) partii.</summary>
        internal static int BagFit(ItemRoster bag)
        {
            int n = 0;
            if (bag == null) return 0;
            for (int i = 0; i < bag.Count; i++)
            {
                var el = bag[i];
                if (el.Amount > 0 && ItemKind(el.EquipmentElement.Item) == 2) n += el.Amount;
            }
            return n;
        }

        /// <summary>Partie pod brama AI i zakupem: partie lordow (takze rodu gracza), nie druzyna gracza (ekran druzyny), nie zalogi (nie maja Stajni
        /// i nie kupuja uprzezy - zalozenie do potwierdzenia), nie umarli. Karawany, wsie, milicje, bandy - bez zmian (DTE ich nie ubiera ze zbrojowni).</summary>
        internal static bool Ruled(PartyBase party)
        {
            var mp = party != null ? party.MobileParty : null;
            return mp != null && !mp.IsMainParty && mp.IsLordParty && On && !Undead.Party(mp);
        }

        /// <summary>Uprzaz, na ktorej mozna posadzic NOWEGO jezdzca: uprzaz zbrojowni i taboru RAZEM ponad jezdzcow, ktorzy maja juz konia.
        /// Recenzja E8/H: dotad "wolna w zbrojowni (od zera w gore) + tabor" - gdy partia miala jezdzcow z koniem bez uprzezy, siodlo z taboru liczylo sie
        /// jako wolne, choc Seat i postoj oddaja je najpierw im: awans przechodzil, a jezdzcow bez pary przybywalo. Ta sama regula co brama gracza (PlayerAvail).</summary>
        internal static int Avail(PartyBase party, Dictionary<CharacterObject, int> adjust = null)
        {
            var mp = party.MobileParty;
            var p = Pairs(mp, adjust);
            return Math.Max(0, p.Harness + BagFit(party.ItemRoster) - p.Mounted);
        }

        // ------------------------------------------------------------ liczniki doby (tylko log)
        private static int _dGateRefused, _dGateTrimmed, _dPayRefused, _dUpPaired, _dSeatBag, _dSeatLoose, _dSeatRefused;
        private static int _dRotPaired, _dRotNoPair, _dMercPair, _dMercBare, _dMercTemplate, _dVetIn, _dVetNone, _dVetOut;
        private static int _dBuyParties, _dBuyNeed, _dBuyPcs, _dBuyGold, _dBuyShort, _dOrders, _dResaddle, _dRebuy, _dSaleHeld;
        private static int _dPlRefused, _dPlSeat, _dBatP, _dBatL, _dBatG, _dSpoil;
        private static int _dStumbles, _stumbles;
        private static long _dTicks; private static int _dCalls;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly Dictionary<string, int> _lastSale = new Dictionary<string, int>();   // partia -> doba ostatniej sprzedazy uprzezy (H-6)
        private static string _wGate = "BRAK";

        internal static void Reset()
        {
            lock (_lock) { _itemKind.Clear(); _troopKind.Clear(); }
            _horseFam = -1;
            _lastSale.Clear(); _errSites.Clear(); _stumbles = 0;
            DayClear();
        }

        private static void DayClear()
        {
            _dGateRefused = _dGateTrimmed = _dPayRefused = _dUpPaired = _dSeatBag = _dSeatLoose = _dSeatRefused = 0;
            _dRotPaired = _dRotNoPair = _dMercPair = _dMercBare = _dMercTemplate = _dVetIn = _dVetNone = _dVetOut = 0;
            _dBuyParties = _dBuyNeed = _dBuyPcs = _dBuyGold = _dBuyShort = _dOrders = _dResaddle = _dRebuy = _dSaleHeld = 0;
            _dPlRefused = _dPlSeat = _dBatP = _dBatL = _dBatG = _dSpoil = 0;
            _dStumbles = 0; _dTicks = 0; _dCalls = 0;
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_errSites.Add(where)) Log.Error("SaddleLaw." + where, e);
        }

        private static long Tick() { _dCalls++; return Stopwatch.GetTimestamp(); }
        private static void Tock(long t0) { _dTicks += Stopwatch.GetTimestamp() - t0; }

        // ------------------------------------------------------------ (1) brama: awans AI
        /// <summary>
        /// Stables.FilterTargets (po bramie konia): cel na jezdnego objetego regula odpada bez wolnej uprzezy (awans czeka albo skreca w piechote), przy
        /// kilku sztukach liczba awansow przycieta. Zrodlo juz konne - bez zmian (jak kon: awans niczego nie sadza w siodle).
        /// </summary>
        internal static void FilterTargets(PartyBase party, IList list)
        {
            if (list == null || list.Count == 0 || !Ruled(party)) return;
            long t0 = Tick();
            try
            {
                int avail = -1;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var boxed = list[i];
                    if (boxed == null || !ArgsBound(boxed.GetType())) continue;
                    var target = _fUpTarget.GetValue(boxed) as CharacterObject;
                    if (!Needs(target)) continue;
                    var src = _fUpSource.GetValue(boxed) as CharacterObject;
                    if (src != null && !src.IsHero && src.IsMounted) continue;
                    int need = (int)_fUpCount.GetValue(boxed);
                    if (need <= 0) continue;
                    if (avail < 0) avail = Avail(party);
                    if (avail <= 0) { _dGateRefused += need; list.RemoveAt(i); continue; }
                    if (need <= avail) continue;
                    _dGateTrimmed += need - avail;
                    list[i] = _upCtor.Invoke(new object[] { src, target, avail, _fUpGold.GetValue(boxed), _fUpXp.GetValue(boxed), _fUpChance.GetValue(boxed) });
                }
            }
            catch (Exception e) { Stumble("FilterTargets", e); }
            finally { Tock(t0); }
        }

        // pola struktury gry PartyUpgraderCampaignBehavior.TroopUpgradeArgs (prywatna) - raz na sesje, bez Traverse przy kazdym celu awansu
        private static Type _upType;
        private static bool _upOk;
        private static System.Reflection.FieldInfo _fUpSource, _fUpTarget, _fUpCount, _fUpGold, _fUpXp, _fUpChance;
        private static System.Reflection.ConstructorInfo _upCtor;

        private static bool ArgsBound(Type t)
        {
            if (t == _upType) return _upOk;
            _upType = t; _upOk = false;
            try
            {
                _fUpSource = AccessTools.Field(t, "Target"); _fUpTarget = AccessTools.Field(t, "UpgradeTarget"); _fUpCount = AccessTools.Field(t, "PossibleUpgradeCount");
                _fUpGold = AccessTools.Field(t, "UpgradeGoldCost"); _fUpXp = AccessTools.Field(t, "UpgradeXpCost"); _fUpChance = AccessTools.Field(t, "UpgradeChance");
                _upCtor = t.GetConstructor(new[] { typeof(CharacterObject), typeof(CharacterObject), typeof(int), typeof(int), typeof(int), typeof(float) });
                _upOk = _fUpSource != null && _fUpTarget != null && _fUpCount != null && _fUpGold != null && _fUpXp != null && _fUpChance != null && _upCtor != null;
                if (!_upOk) Log.Info("Jezdzcy i uprzaz (E8/H): OSTRZEZENIE - struktura celu awansu gry ma inne pola (" + t.FullName + ") - brama AI na liscie celow spi (zostaje brama przy zaplacie).");
            }
            catch (Exception e) { Stumble("ArgsBound", e); }
            return _upOk;
        }

        /// <summary>Stables.PayInHorses, PRZED zdjeciem konia z taboru: false - brak wolnej uprzezy dla n nowych jezdzcow, awans czeka (XP zostaje).</summary>
        internal static bool PayGate(PartyBase party, CharacterObject target, int n)
        {
            if (n <= 0 || !Needs(target) || !Ruled(party)) return true;
            long t0 = Tick();
            try
            {
                if (Avail(party) >= n) return true;
                _dPayRefused += n;
                return false;
            }
            catch (Exception e) { Stumble("PayGate", e); return true; }
            finally { Tock(t0); }
        }

        /// <summary>
        /// Po zaplacie koniem (kon juz w zbrojowni, nowi jezdzcy jeszcze poza skladem - adjust): uprzaz dla n nowych jezdzcow - wolna w zbrojowni zostaje
        /// na miejscu, brakujaca przechodzi z taboru do zbrojowni tej samej partii (najtansza najpierw), jak kon przy awansie. Tylko licznik "awanse z para".
        /// </summary>
        internal static void AfterPay(PartyBase party, CharacterObject target, int n)
        {
            if (n <= 0 || !Needs(target) || !Ruled(party)) return;
            long t0 = Tick();
            try
            {
                int moved = Seat(party.MobileParty, new Dictionary<CharacterObject, int> { { target, n } });
                _dUpPaired += n;
                _dSeatBag += moved;
            }
            catch (Exception e) { Stumble("AfterPay", e); }
            finally { Tock(t0); }
        }

        /// <summary>Uprzaz z taboru do zbrojowni tak, by kazdy jezdziec z koniem mial pare; zwraca przeniesione sztuki.</summary>
        private static int Seat(MobileParty mp, Dictionary<CharacterObject, int> adjust)
        {
            var p = Pairs(mp, adjust);
            int lack = p.Mounted - p.Harness;
            if (lack <= 0) return 0;
            int moved = MoveFromBag(mp, lack);
            if (moved > 0 && !Stables.PersistentArmory(mp)) _dSeatLoose += moved;   // zbrojownia, ktorej DTE nie trzyma trwale (partia bez wodza) - pomiar, jak "do zbrojowni nietrwalej" koni
            return moved;
        }

        /// <summary>Najtansza uprzaz rodziny konia z taboru partii AI do jej zbrojowni DTE (ta sama partia - przeniesienie). DTE nie przyjal - zostaje w taborze.</summary>
        private static int MoveFromBag(MobileParty mp, int want)
        {
            var bag = mp != null ? mp.ItemRoster : null;
            if (bag == null || want <= 0) return 0;
            int moved = 0;
            HashSet<ItemObject> skip = null;
            while (want > 0)
            {
                int best = -1, bestVal = int.MaxValue;
                for (int i = 0; i < bag.Count; i++)
                {
                    var el = bag[i];
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || ItemKind(it) != 2 || (skip != null && skip.Contains(it))) continue;
                    int v = el.EquipmentElement.ItemValue;
                    if (v < bestVal) { bestVal = v; best = i; }
                }
                if (best < 0) break;
                var e = bag[best];
                int take = Math.Min(want, e.Amount);
                if (!AiGear.AddToArmory(mp, e.EquipmentElement.Item, take))
                {
                    if (skip == null) skip = new HashSet<ItemObject>();
                    skip.Add(e.EquipmentElement.Item);
                    _dSeatRefused += take;
                    continue;
                }
                bag.AddToCounts(e.EquipmentElement, -take);
                want -= take; moved += take;
            }
            return moved;
        }

        // ------------------------------------------------------------ (1) brama: zamiana ROT
        /// <summary>
        /// RotHorseGuard.Apply, przed koniem: ilu z n konnych z zamiany nie ma pary (wolna uprzaz zbrojowni + tabor, bez jeszcze nierozliczonych z tej
        /// zamiany - pending). Ci ida ta sama droga co konni bez konia: pieszy tego samego tieru z puli rodu.
        /// </summary>
        internal static int RotNoPair(PartyBase party, CharacterObject val, int n, Dictionary<CharacterObject, int> pending)
        {
            if (n <= 0 || !Needs(val) || !Ruled(party)) return 0;
            long t0 = Tick();
            try
            {
                int avail = Avail(party, pending);
                int no = Math.Max(0, n - Math.Max(0, avail));
                _dRotNoPair += no;
                return no;
            }
            catch (Exception e) { Stumble("RotNoPair", e); return 0; }
            finally { Tock(t0); }
        }

        /// <summary>RotHorseGuard.Apply, po koniu i po rozliczeniu typu w pending: uprzaz z taboru pod nowych jezdzcow z koniem (horsed - ilu dostalo konia).</summary>
        internal static void RotSeat(PartyBase party, CharacterObject val, int horsed, Dictionary<CharacterObject, int> pending)
        {
            if (horsed <= 0 || !Needs(val) || !Ruled(party)) return;
            long t0 = Tick();
            try
            {
                _dSeatBag += Seat(party.MobileParty, pending);
                _dRotPaired += horsed;
            }
            catch (Exception e) { Stumble("RotSeat", e); }
            finally { Tock(t0); }
        }

        // ------------------------------------------------------------ (1) brama: awans gracza (ekran druzyny)
        /// <summary>
        /// Postfiks PartyScreenLogic.ValidateCommand (gra zna jeden wymog przedmiotu przy awansie - kategorie konia): awans piechura na jezdzca objetego
        /// regula tylko, gdy jest wolna uprzaz - w zbrojowni druzyny ponad jezdzcow z koniem (z konmi zaplaconymi juz w tej sesji ekranu) albo w sakwach.
        /// Strzalka awansu wyglada na czynna (ekran pokazuje jeden wymog), klikniecie daje komunikat.
        /// </summary>
        public static void ValidatePostfix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, ref bool __result)
        {
            try
            {
                if (!__result || command == null || command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop || !On) return;
                if (__instance == null || __instance.RightOwnerParty != PartyBase.MainParty || command.RosterSide != PartyScreenLogic.PartyRosterSide.Right) return;
                var src = command.Character;
                if (src == null || src.IsHero || src.IsMounted) return;
                var ups = src.UpgradeTargets;
                if (ups == null || command.UpgradeTarget < 0 || command.UpgradeTarget >= ups.Length || !Needs(ups[command.UpgradeTarget])) return;
                int n = Math.Max(1, command.TotalNumber);
                int total;
                int avail = PlayerAvail(__instance, out total);
                if (avail >= n) return;
                __result = false;
                _dPlRefused += n;
                MBInformationManager.AddQuickInformation(new TextObject(avail > 0
                    ? "A rider needs a saddle as well as a horse - you have spare horse harness for only " + avail + " more."
                    : total > 0
                        ? "A rider needs a saddle as well as a horse - all the horse harness in your baggage and troop armoury is already needed by the riders you have."
                        : "A rider needs a saddle as well as a horse - there is no spare horse harness in your baggage or in the troop armoury."));
            }
            catch (Exception e) { Stumble("ValidatePostfix", e); }
        }

        /// <summary>Wolna uprzaz druzyny gracza w tej sesji ekranu: zbrojownia i sakwy RAZEM ponad jezdzcow z koniem (z awansowanymi juz w tej sesji - sklad
        /// ekranu jest zywy, konie zaplacone w sesji z historii ekranu; siodlo z sakw schodzi dopiero przy zatwierdzeniu). total - cala uprzaz rodziny konia.
        /// Recenzja E8/H: dotad max(0, zbrojownia - jezdzcy) + sakwy - niedobor zbrojowni byl obcinany do zera, wiec jedno siodlo w sakwach przepuszczalo
        /// dowolnie wiele awansow w jednej sesji ekranu (brama gracza nie zamykala sie nigdy).</summary>
        private static int PlayerAvail(PartyScreenLogic logic, out int total)
        {
            total = int.MaxValue;
            var arm = PlayerArm();
            if (arm == null) return int.MaxValue;   // zbrojownia nieznana - nie blokujemy
            var roster = logic.MemberRosters != null && logic.MemberRosters.Length > 1 ? logic.MemberRosters[1] : MobileParty.MainParty.MemberRoster;
            int paid = 0;
            var hist = logic.CurrentData != null ? logic.CurrentData.UsedUpgradeHorsesHistory : null;
            if (hist != null) foreach (var t in hist) if (t != null) paid += Math.Max(0, t.Item2);
            var p = Pairs(roster, arm);
            int mounted = Math.Min(p.Riders, p.Horses + paid);
            total = p.Harness + BagFit(PartyBase.MainParty.ItemRoster);
            return Math.Max(0, total - mounted);
        }

        /// <summary>m114-musztra: wolna uprzaz druzyny gracza poza ekranem druzyny - ta sama liczba co PlayerAvail bez sesji ekranu (zaplaconych koni 0):
        /// zbrojownia i sakwy RAZEM ponad jezdzcow z koniem. Zbrojownia nieznana - int.MaxValue (brama nie blokuje).</summary>
        internal static int PlayerSpare()
        {
            try
            {
                var arm = PlayerArm();
                var main = MobileParty.MainParty;
                if (arm == null || main == null) return int.MaxValue;
                var p = Pairs(main.MemberRoster, arm);
                return Math.Max(0, p.Harness + BagFit(PartyBase.MainParty.ItemRoster) - Math.Min(p.Riders, p.Horses));
            }
            catch (Exception e) { Stumble("PlayerSpare", e); return int.MaxValue; }
        }

        /// <summary>
        /// Stables.BankPaidHorses (zatwierdzony ekran druzyny, konie juz w zbrojowni): uprzaz z sakw do zbrojowni dla nowych jezdzcow bez pary - najwyzej tyle
        /// sztuk, ile koni zaplacono w tej sesji ekranu (stare braki uprzezy gracz uzupelnia sam). Sztuka z sakw do zbrojowni tej samej druzyny.
        /// </summary>
        internal static void PlayerBank(int paidHorses)
        {
            if (paidHorses <= 0 || !On) return;
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                var main = MobileParty.MainParty;
                if (armory == null || main == null || main.ItemRoster == null) return;
                var p = Pairs(main.MemberRoster, PlayerArm());
                int want = Math.Min(paidHorses, p.Mounted - p.Harness);
                int moved = 0;
                var bag = main.ItemRoster;
                while (want > 0)
                {
                    int best = -1, bestVal = int.MaxValue;
                    for (int i = 0; i < bag.Count; i++)
                    {
                        var el = bag[i];
                        if (el.Amount <= 0 || ItemKind(el.EquipmentElement.Item) != 2) continue;
                        int v = el.EquipmentElement.ItemValue;
                        if (v < bestVal) { bestVal = v; best = i; }
                    }
                    if (best < 0) break;
                    var e = bag[best];
                    int take = Math.Min(want, e.Amount);
                    var ee = e.EquipmentElement;
                    armory.AddToCounts(ee, take);
                    bag.AddToCounts(ee, -take);
                    want -= take; moved += take;
                }
                if (moved <= 0) return;
                _dPlSeat += moved;
                Log.Info("Jezdzcy i uprzaz (E8/H): awans gracza - " + moved + " szt. uprzezy z sakw przeszlo do zbrojowni druzyny (para dla nowych jezdzcow).");
                Log.Player(moved + (moved == 1 ? " saddle goes" : " saddles go") + " from the baggage to the troop armoury with the new riders.");
            }
            catch (Exception e) { Stumble("PlayerBank", e); }
        }

        // ------------------------------------------------------------ (2) bitwa gracza
        /// <summary>GarrisonKit.SaddleGuard: jezdziec zszedl z konia z braku uprzezy (0 druzyna gracza, 1 lord AI, 2 zaloga).</summary>
        internal static void NoteBattle(int kind) { if (kind == 0) _dBatP++; else if (kind == 1) _dBatL++; else _dBatG++; }

        // ------------------------------------------------------------ (3) zakup i zapas
        /// <summary>
        /// JEDNA definicja zapasu uprzezy rodziny konia partii (kupno AiGear.BuySaddles do zapasu; sprzedaz nadwyzek E8/Z nie schodzi ponizej): ludzie, ktorzy
        /// maja na czym usiasc - jezdzcy objeci regula i (partie lordow) gotowi do awansu na jezdnego (Stables.NeedByCategory), najwyzej tyle, ile partia ma
        /// koni pod siodlo (zbrojownia + tabor) - plus luz floor(AiSpareHorseShare x jezdzcy z koniem), zeby strata jednej sztuki nie zsadzala jezdzca. Siodlo bez konia
        /// nikogo nie posadzi, wiec jezdziec bez konia nie podnosi zapasu (kupi je, gdy przyjdzie kon - Stajnia biegnie przed zakupami sprzetu).
        /// Zapas nigdy nie przekracza zapasu E8/Z (AiBuyRules.HarnessKeep liczy wszystkich konnych + czekajacych + luz, sprzedaje ponad 1.5 x) - kupno do
        /// zapasu i sprzedaz nadwyzki sie nie spotkaja.
        /// </summary>
        internal static int Target(MobileParty mp, TroopRoster roster, bool garrison, out PairState p)
        {
            p = Pairs(roster ?? (mp != null ? mp.MemberRoster : null), ArmOf(mp));
            if (mp == null) return 0;
            int wait = 0, horses = p.Horses;
            if (!garrison && mp.Party != null)
            {
                var nk = Stables.NeedByCategory(mp.Party);
                if (nk != null) for (int k = 0; k < nk.Length; k++) wait += Math.Max(0, nk[k]);
                horses += Stables.CountAnyMounts(mp.Party);
            }
            int seats = Math.Min(p.Riders + wait, horses);
            var c = Settings.Current;
            double share = c != null ? MBMath.ClampFloat(c.AiSpareHorseShare, 0f, 1f) : 0.1;
            // luz od jezdzcow z koniem (nie od czekajacych na awans), w dol do calej sztuki: partia ponizej 10 jezdzcow nie trzyma zapasowego siodla
            // (zaokraglenie w gore dawaloby sztuke kazdej partii - kilkaset siodel z polek w pierwszych dobach kampanii, zanim siodlarze dostana zamowienia).
            // Zapas E8/Z zaokragla w gore, wiec zapas siodel <= zapas E8/Z dla kazdej partii
            return seats + (int)Math.Floor(share * Math.Min(p.Riders, horses) + 1e-4);   // +1e-4: 0.1f x 30 to 3.0000000447 albo 2.9999999 - ma byc 3
        }

        /// <summary>Ile siodel brakuje partii lorda do zapasu (zbrojownia; tabor przenosi OnSettlementEntered przed zakupem).</summary>
        internal static int BuyNeed(MobileParty mp)
        {
            if (mp == null || !Ruled(mp.Party)) return 0;
            PairState p;
            int t = Target(mp, null, false, out p);
            return Math.Max(0, t - p.Harness);
        }

        /// <summary>
        /// Postoj partii lorda w osadzie (po Stajni - Remount i zakup koni, przed sprzedaza nadwyzek i zakupami sprzetu): uprzaz rodziny konia z taboru
        /// (lup, dobytek rozbitych) idzie do zbrojowni do wysokosci zapasu - zanim lord kupi nowe siodla, a gra sprzeda tabor jako towar.
        /// </summary>
        internal static void OnSettlementEntered(MobileParty mp, Settlement st)
        {
            if (mp == null || st == null || mp.IsMainParty || !mp.IsLordParty || mp.MapEvent != null) return;
            try
            {
                if (mp.ItemRoster != null && BagFit(mp.ItemRoster) > 0)
                {
                    long t0 = Tick();
                    try
                    {
                        int want = BuyNeed(mp);
                        if (want > 0) _dResaddle += MoveFromBag(mp, want);
                    }
                    finally { Tock(t0); }
                }
                GarrisonHorses.GiveHarness(mp, st);   // r3-jazda (3): po uprzezy z taboru, przed zakupem siodel - uprzaz zalogi twierdzy swojego krolestwa ponad jej jezdzcow (wlasny try)
            }
            catch (Exception e) { Stumble("OnSettlementEntered", e); }
        }

        /// <summary>AiGear.BuySaddles: wizyta z brakiem (need przed zakupem, left po zakupach, order - polka bez pasujacej sztuki dla AI: zamowienie).</summary>
        internal static void NoteVisit(MobileParty mp, int need, int left, bool order)
        {
            _dBuyParties++; _dBuyNeed += need; _dBuyShort += Math.Max(0, left);
            if (order) _dOrders++;
            if (left < need && mp != null)
            {
                int day = (int)CampaignTime.Now.ToDays, sold;
                if (_lastSale.TryGetValue(mp.StringId ?? "", out sold) && day - sold >= 0 && day - sold < RebuyDays) _dRebuy += need - left;
            }
        }

        /// <summary>AiGear (dostawa koszyka Bucket): kupione siodla i zloto - osobno od linii "Uprzaz (215)".</summary>
        internal static void NoteBought(int n, int cost) { _dBuyPcs += n; _dBuyGold += cost; }

        /// <summary>MenPurse.SellSpareHarness: partia sprzedala uprzaz ponad zapas (pamiec doby do licznika "kupno po sprzedazy").</summary>
        internal static void NoteSale(MobileParty mp, int sold)
        {
            if (sold <= 0 || mp == null) return;
            _lastSale[mp.StringId ?? ""] = (int)CampaignTime.Now.ToDays;
            if (_lastSale.Count > 5000) _lastSale.Clear();
        }

        /// <summary>MenPurse.SellSpareHarness: sztuki rodziny konia zatrzymane pod zapas (nie sprzedane mimo nadwyzki liczonej bez rodzin).</summary>
        internal static void NoteSaleHeld(int n) { if (n > 0) _dSaleHeld += n; }

        // ------------------------------------------------------------ (4) uprzaz idzie za koniem
        /// <summary>
        /// HorseFate.VetTakeHorses (got koni poszlo z weteranami do puli karczmy): weteran objety regula zabiera tez uprzaz - wolna w zbrojowni partii po
        /// odejsciu n ludzi (ponad jezdzcow z koniem, ktorzy zostaja), od najnizszego tieru i najtanszej. Zbrojownia -> pula miasta (przeniesienie).
        /// </summary>
        internal static void VetTake(MobileParty party, Dictionary<ItemObject, int> arm, Dictionary<ItemObject, int> pool, CharacterObject troop, int n, int got)
        {
            if (got <= 0 || arm == null || pool == null || party == null || !On || !Needs(troop)) return;
            try
            {
                var p = Pairs(party.MemberRoster, arm, new Dictionary<CharacterObject, int> { { troop, -n } });
                int take = Math.Min(got, p.FreeHarness), took = 0;
                if (take > 0)
                {
                    var items = new List<ItemObject>();
                    foreach (var kv in arm) if (kv.Value > 0 && ItemKind(kv.Key) == 2) items.Add(kv.Key);
                    items.Sort(CompareCheap);
                    foreach (var it in items)
                    {
                        if (took >= take) break;
                        int c = arm[it];
                        int k = Math.Min(c, take - took);
                        if (c - k > 0) arm[it] = c - k; else arm.Remove(it);
                        int pc; pool.TryGetValue(it, out pc); pool[it] = pc + k;
                        took += k;
                    }
                }
                _dVetIn += took; _dVetNone += got - took;
            }
            catch (Exception e) { Stumble("VetTake", e); }
        }

        private static int CompareCheap(ItemObject a, ItemObject b)
        {
            int d = AiGear.TierOf(a).CompareTo(AiGear.TierOf(b)); if (d != 0) return d;
            d = a.Value.CompareTo(b.Value); if (d != 0) return d;
            return string.CompareOrdinal(a.StringId, b.StringId);
        }

        /// <summary>RecruitKit.MercHorseKit: uprzaz z puli karczmy dla najemnika, ktory dostal z niej konia (null - w puli nie ma pasujacej).</summary>
        internal static ItemObject VetHire(Dictionary<ItemObject, int> pool, ItemObject horse)
        {
            try
            {
                ItemObject best = null;
                if (pool != null)
                    foreach (var kv in pool)
                    {
                        if (kv.Value <= 0 || !Fits(kv.Key, horse)) continue;
                        if (best == null || CompareCheap(kv.Key, best) < 0) best = kv.Key;
                    }
                if (best == null) { _dMercBare++; return null; }
                int c = pool[best];
                if (c <= 1) pool.Remove(best); else pool[best] = c - 1;
                _dMercPair++;
                return best;
            }
            catch (Exception e) { Stumble("VetHire", e); return null; }
        }

        /// <summary>RecruitKit.MercHorseKit przy wylaczonej regule: rzad najemnika ze wzorca (z niczego, jak dotad) - tylko licznik.</summary>
        internal static void NoteMercTemplate(int n) { if (n > 0) _dMercTemplate += n; }
        /// <summary>HorseFate.VetPoolDaily: uprzaz puli karczmy ponad konie puli - na polke miasta.</summary>
        internal static void NoteVetOut(int n) { if (n > 0) _dVetOut += n; }
        /// <summary>HorseFate.Spoils: uprzaz zbrojowni rozbitej partii poszla z konmi do zwyciezcow albo na polke.</summary>
        internal static void NoteSpoil(int n) { if (n > 0) _dSpoil += n; }

        // ------------------------------------------------------------ linia doby
        private sealed class Census
        {
            public long Parties, Men, TypeMounted, Riders, Exempt, Mounted, Paired, NoSaddle, NoHorse, Horses, Harness, Other, Free;
        }

        private static void CensusOne(MobileParty mp, Census c)
        {
            if (mp == null || !mp.IsActive || mp.MemberRoster == null) return;
            var r = mp.MemberRoster;
            int men = 0, typeM = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                men += el.Number;
                if (ch.IsMounted) typeM += el.Number;
            }
            if (men <= 0) return;
            var p = Pairs(r, ArmOf(mp));
            c.Parties++; c.Men += men; c.TypeMounted += typeM;
            c.Riders += p.Riders; c.Exempt += p.Exempt; c.Mounted += p.Mounted; c.Paired += p.Paired; c.NoSaddle += p.NoSaddle; c.NoHorse += p.NoHorse;
            c.Horses += p.Horses; c.Harness += p.Harness; c.Other += p.HarnessOther; c.Free += p.FreeHarness;
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.00", CultureInfo.InvariantCulture) : "0.00"; }

        private static void Seg(StringBuilder sb, string name, Census c)
        {
            sb.Append(" | ").Append(name).Append(": partii ").Append(c.Parties).Append(", ludzi ").Append(c.Men)
              .Append(", jazda wedlug typu ").Append(c.TypeMounted).Append(" (").Append(Pct(c.TypeMounted, c.Men)).Append("%)")
              .Append(", objeci regula ").Append(c.Riders).Append(", z koniem ").Append(c.Mounted).Append(" (").Append(Pct(c.Mounted, c.Men)).Append("%)")
              .Append(", z para ").Append(c.Paired).Append(" (").Append(Pct(c.Paired, c.Men)).Append("%)")
              .Append(", z koniem bez uprzezy ").Append(c.NoSaddle).Append(", bez konia ").Append(c.NoHorse).Append(", wzorzec bez uprzezy ").Append(c.Exempt)
              .Append("; zbrojownie: konie ").Append(c.Horses).Append(", uprzaz rodziny konia ").Append(c.Harness).Append(", innej rodziny ").Append(c.Other)
              .Append(", wolna ").Append(c.Free);
        }

        /// <summary>
        /// Linia doby "Jezdzcy i uprzaz (E8/H)" (takt doby ArmouryBehavior, po "Konie AI (175)"): spis par osobno dla partii rodow AI, zalog i rodu gracza
        /// (takze przy wylaczonej regule - pomiar "przed": "z koniem bez uprzezy" = tylu zeszloby z koni), liczniki bramy, zakupow, puli weteranow i bitwy.
        /// </summary>
        internal static void Daily()
        {
            try
            {
                var ci = CultureInfo.InvariantCulture;
                var lords = new Census(); var gar = new Census(); var mine = new Census();
                double ms = 0;
                long t0 = Stopwatch.GetTimestamp();
                try
                {
                    foreach (var mp in MobileParty.AllLordParties)
                    {
                        if (mp == null || mp.IsMainParty) continue;
                        CensusOne(mp, mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan ? mine : lords);
                    }
                    foreach (var mp in MobileParty.AllGarrisonParties) CensusOne(mp, gar);
                    CensusOne(MobileParty.MainParty, mine);
                }
                catch (Exception e) { Stumble("Census", e); }
                try { GarrisonHorses.NoteCavalry(lords.Men, lords.TypeMounted, lords.Mounted); } catch (Exception e) { Stumble("NoteCavalry", e); }   // r3-jazda: jazda wedlug typu i z koniem do linii "Konie zalog dla lordow" (tylko odczyt spisu)
                ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                long poolHar = 0;
                try { HorseFate.VetPoolHarness((it, n) => poolHar += n); } catch (Exception e) { Stumble("VetPool", e); }
                var s = Settings.Current;
                var sb = new StringBuilder();
                sb.Append("Jezdzcy i uprzaz (E8/H): dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(" | regula [").Append(On ? "WL" : "wyl").Append("] (klucz RiderNeedsHarness ").Append(KeyOn ? "tak" : "nie")
                  .Append(", awans wymaga konia ").Append(s != null && s.CavalryNeedsMounts ? "tak" : "nie").Append(", wojsko tylko z wlasnym sprzetem ")
                  .Append(GarrisonKit.OwnKitOn ? "tak" : "nie").Append("; brama gracza ").Append(_wGate).Append(")");
                Seg(sb, "partie rodow AI", lords);
                Seg(sb, "zalogi", gar);
                Seg(sb, "rod gracza", mine);
                sb.Append(" | brama AI: awanse na jezdnego z para ").Append(_dUpPaired).Append(", wstrzymane z braku uprzezy - cele odrzucone ").Append(_dGateRefused)
                  .Append(" ludzi, przyciete ").Append(_dGateTrimmed).Append(", przy zaplacie ").Append(_dPayRefused)
                  .Append("; zamiany ROT: z para ").Append(_dRotPaired).Append(", bez pary ").Append(_dRotNoPair)
                  .Append("; uprzaz z taboru do zbrojowni przy awansach i zamianach ").Append(_dSeatBag).Append(" (w tym do zbrojowni nietrwalej ").Append(_dSeatLoose)
                  .Append(", DTE nie przyjal ").Append(_dSeatRefused).Append(")")
                  .Append(" | najemnicy: z para z puli ").Append(_dMercPair).Append(", kon z puli bez uprzezy ").Append(_dMercBare).Append(", rzad ze wzorca (z niczego) ").Append(_dMercTemplate)
                  .Append("; pula weteranow: uprzaz +").Append(_dVetIn).Append(" (weteran bez uprzezy ").Append(_dVetNone).Append("), na polki ").Append(_dVetOut)
                  .Append(", stan ").Append(poolHar)
                  .Append(" | zakupy lordow AI: partie z brakiem ").Append(_dBuyParties).Append(", brak przed zakupem ").Append(_dBuyNeed).Append(", kupione ").Append(_dBuyPcs)
                  .Append(" szt. za ").Append(_dBuyGold).Append(" zl, brak po zakupach ").Append(_dBuyShort).Append(", zamowienia dla siodlarzy ").Append(_dOrders)
                  .Append(", z taboru do zbrojowni przy postoju ").Append(_dResaddle).Append(", kupno w ").Append(RebuyDays).Append(" dob po sprzedazy uprzezy ").Append(_dRebuy)
                  .Append(" szt., zatrzymane pod zapas przy sprzedazy nadwyzek ").Append(_dSaleHeld)
                  .Append(" | polki miast i zamkow: uprzaz ").Append(SupplyDemand.HarnessOnShelves >= 0 ? SupplyDemand.HarnessOnShelves.ToString(ci) : "brak spisu")
                  .Append(" | gracz: awanse wstrzymane z braku uprzezy ").Append(_dPlRefused).Append(", uprzaz z sakw do zbrojowni ").Append(_dPlSeat)
                  .Append(" | bitwa gracza: zeszlo z koni z braku uprzezy - gracz ").Append(_dBatP).Append(", lordowie ").Append(_dBatL).Append(", zalogi ").Append(_dBatG)
                  .Append(" | dobytek rozbitych: uprzaz zbrojowni ").Append(_dSpoil)
                  .Append(" | koszt: spis ").Append(ms.ToString("0.0", ci)).Append(" ms, zdarzenia ")
                  .Append((_dTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", ci)).Append(" ms (").Append(_dCalls).Append(" wywolan)")
                  .Append(" | potkniecia dzis ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            DayClear();
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(PartyScreenLogic), "ValidateCommand");
                if (m != null)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(SaddleLaw), nameof(ValidatePostfix)));
                    _wGate = "wpieta";
                }
                Log.Info("Jezdzcy i uprzaz (E8/H): start - regula " + (On ? "CZYNNA" : "WYLACZONA") + " (klucz RiderNeedsHarness " + (KeyOn ? "tak" : "nie")
                         + "); jezdziec = kon + uprzaz rodziny konia (dowolne siodlo); brama awansu gracza " + _wGate + " " + (m != null ? "1/1" : "0/1")
                         + " (PartyScreenLogic.ValidateCommand); brama AI i zakup - w Stables / AiGear, bitwa - GarrisonKit, pula weteranow - HorseFate.");
            }
            catch (Exception e) { Stumble("ApplyAll", e); }
        }
    }
}
