using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KA - KARCZMY W MIESCIE (16.10, Jeff: "w miescie moge spac tylko w karczmie, daj do wyboru trzy jakosci karczmy i daj pomysl jakie sa bonusy za kazda;
    /// ludzie sami placa za siebie i za swoje konie, dostaja zold, ja place za siebie i za konie, ktore nie sa na ludziach"; wczesniej: "nocleg w miescie czy
    /// tawernie kosztuje, ale daje wyspanie w lozku +10% do predkosci poruszania sie + 5 do morale"; projekt zatwierdzony, klucze TownInns, TownInnPriceBonus).
    ///
    /// T1 Menu miasta "Take lodgings at an inn" (tylko miasta) -> trzy jakosci; linia opcji = caly rachunek i kto placi, podpowiedz = szczegoly:
    ///   0 wspolna izba (sloma)                 czlowiek 1, kon 1, pokoj gracza 3  | do zmierzchu +5% predkosci na mapie, +2 morale
    ///   1 porzadna karczma (lozka, ogien, ale)   2 / 2 / 8                         | +10%, +5 morale, sen x1.25 (dlug snu splaca sie szybciej)
    ///   2 dobry dom (pokoje, wino, laznia)      4 / 3 / 20                         | +12%, +8 morale, sen x1.25, gojenie rannych +50% tej nocy, ceny gracza
    ///                                                                                w TYM miescie do zmierzchu: kupno / sprzedaz lepiej do 3% (klucz TownInnPriceBonus)
    ///   Recenzja 16.10: bez posilku - druzyna je swoj prowiant z taboru jak w drodze (posilek karczmy bylby placony drugi raz; ale i wino to nie racje gry).
    ///   Wybor = zaplata, potem ten sam sen w menu co w obozie (NightRest, arm_sleep_wait): pobudka nie przed switem (gdy swit najwyzej 12 h przed nami - wieczorem
    ///   i w nocy, od 18:00 przy zwyklych godzinach obozu; nocleg wziety w dzien to zwykly sen "do wyspania" - godzina pobudki w podpowiedzi), potem stan
    ///   "wypoczety" od pobudki do najblizszego zmierzchu (18:00), najwyzej 12 h. Premia tylko bez dlugu snu (NightRest: kara dlugu i premia nigdy naraz).
    ///   Pobudka przed przespaniem bazy (SleepHoursNeeded) i bez pelnego paska - bez premii (zaplacone przepada u karczmarza).
    /// T2 Kto placi: kazdy szeregowy z sakiewki ludzi (MenPurse - wolna czesc po rezerwie napraw, odziezy i oszczednosciach K1, jak markietani) za siebie i swojego
    ///   konia (IsMounted - ta sama regula co zold konnych 160); sakiewka jest wspolna dla druzyny, wiec rozdzial "kogo stac": najpierw kazdy dostaje wspolna izbe
    ///   (piesi przed konnymi - taniej), potem z reszty kolejni wyzej, do wybranej jakosci; kogo nie stac na nic - stajnia za darmo (jak oboz). Przy zbrojowni DTE
    ///   i TroopsFightWithOwnKitOnly konny bez konia w zbrojowni idzie pieszo - placi jak pieszy. Towarzysze (wszyscy bohaterowie druzyny poza graczem) za siebie
    ///   i swojego konia, kazdy osobno: najpierw z wlasnej kiesy (Hero.Gold), brak do wybranej jakosci z wolnej sakiewki ludzi, ktora zostala po szeregowych
    ///   (recenzja 16.10: zold bohaterow druzyny gracza wchodzi w TotalWage i SoldierPay wplaca go do sakiewki ludzi, nie do Hero.Gold), albo najlepsza tansza,
    ///   na ktora starczy, albo stajnia. Gracz za siebie (pokoj), swojego konia i konie bez jezdzca (luzaki i juczne z taboru gracza - ItemRoster, bez zywego
    ///   inwentarza - oraz konie zbrojowni DTE ponad liczbe konnych szeregowych) w wybranej jakosci; gracza nie stac - opcja nieczynna. Premia druzyny wazona
    ///   liczba osob w kazdej jakosci (stajnia = 0). W oknie lupu (CitySack L3 - wojsko krolestwa zdobywcy nie placi) to, co szloby z sakiewki ludzi (szeregowi
    ///   i doplata towarzyszy), jest darowane - spia w wybranej jakosci, zloto zostaje w sakiewce; to samo w rachunku menu i przy zaplacie.
    ///   Zloto do kiesy ludu miasta (karczmarze, uslugi KLife): szeregowi MenPurse.InnPay (TownFolk.Earn calosci), bohaterowie GiveGoldAction do kasy miasta
    ///   i TownFolk.EarnFromTown (droga oplat gracza PlayerFees8); kiesa ludu nieczynna - zostaje w kasie miasta. Kontrola obiegu przy kazdym noclegu: suma
    ///   posiadaczy (sakiewki ludzi, kiesy gracza i towarzyszy, kasa miasta, kiesy ludu miast i gminy) przed i po - roznica 0.
    /// T3 Sen w miescie bez noclegu - bez zmian (NightRest: postoj pod dachem jak oboz, baza bez premii). Dom gracza: POMINIETY - dom jest w RealisticCaptivity
    ///   (Homes.Vault, pole internal innego DLL), Armoury nie ma do niego API; refleksja w cudze pole to nie "czyste API" (decyzja projektu).
    /// T4 Zapis "arm_inns" (SaveText.Sync, wlasny try): tylko stan noclegu - faza (1 oplacony sen, 2 wypoczety), jakosc, miasto, godziny, premie, zysk z cen (pole
    ///   opcjonalne). Brak klucza = brak. Czasy z zapisu sprawdzane na starcie sesji (CheckTimes: wypoczeci > 24 h, pobudka > 12 h, nocleg z przyszlosci).
    /// T5 Linie "Karczmy (KA):" - latki, start sesji, kazdy nocleg, sen, pobudka, doba; tools/sprawdz_logi.py --grupa nkarczmy.
    /// Ceny (T1, dobry dom): tylko ekran handlu gracza z TYM miastem - prefiks / finalizer na InventoryLogic.GetItemPrice ustawiaja flage watku, postfiks na
    /// TownMarketData.GetPrice zmienia cene tylko pod ta flaga (sciezka rynku; cena z historii transakcji ekranu - odkup tej samej sztuki - bez zmiany).
    /// Krok premii liczony w tej chwili z rozpietosci gry tej sztuki (B kupno, S sprzedaz, obie bez premii): min(3%, (B-S)/(2(B+S))) po obu stronach - premia
    /// zjada najwyzej polowe rozpietosci gry, wiec kupno z premia zawsze > sprzedaz z premia (kupno >= S+1, sprzedaz <= kupno z premia - 1) i petla kupno-sprzedaz
    /// nie daje zysku z kasy miasta. Tylko po nocy (pobudka o swicie), do zmierzchu i najwyzej 12 h, i najwyzej ok. 5 x cena pokoju zysku (szacunek z 3% obrotu
    /// przy kazdym zamknieciu ekranu handlu - potem premia cen zdjeta). Nasze zakupy i sprzedaze ludzi (MenPurse, MarketData wprost), AI i model cen nietknieci -
    /// rdzen ekonomii bez zmian, zloto dalej gracz <-> kasa miasta jak w kazdym handlu.
    /// Gojenie (dobry dom): postfiks na DefaultPartyHealingModel (te same metody co SlowHealing) - partia gracza, tylko w czasie snu oplaconego noclegu,
    /// wynik x (1 + 0.5 x udzial osob w dobrym domu) jako Add w punktach (rozpiska pokazuje przyrost, nie procent od sumy wspolczynnikow); kary (wynik <= 0) bez zmian.
    /// Predkosc i morale: NightRest.SpeedPostfix / MoralePostfix (te same latki co kara snu, raz na lancuch modeli - SpeedDepth) wolaja SpeedBonus / MoraleBonus,
    /// gdy dlug gracza 0. Predkosc: sufit kolumny marszowej (MarchPace LimitMax, biegnie wczesniej) podniesiony o ten sam procent - inaczej premia znikala pod
    /// sufitem (recenzja 16.10, log 11-22-18: 6 z 9 audytow "=> 3.00" = sufit). Predkosc liczona rownolegle - stan noclegu to obiekt podmieniany w calosci (_rest).
    /// Wyjatek w naszym kodzie - licznik potkniec (pierwsze do Log.Error; w predkosci i morale - licznik Interlocked w linii doby), nic nie gasimy.
    /// </summary>
    internal static partial class TownInns   // sw-zajazdy: zajazdy przy szlakach w TownInns.Road.cs (te same ceny, rachunek i stan noclegu)
    {
        private const string P = "Karczmy (KA): ";
        internal const string Menu = "arm_inn";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // jakosci 0..2 (-1 = stajnia, darmo jak oboz)
        private static readonly string[] QName = { "Common room", "Decent inn", "Good house" };
        // recenzja 16.10: bez posilku w opisie - druzyna je wlasny prowiant z taboru (ale i wino to nie racje gry)
        private static readonly string[] QWhat = { "straw and a roof", "beds, a warm hearth and ale", "rooms, wine and a bath" };
        private static readonly string[] QLog = { "wspolna izba", "porzadna karczma", "dobry dom" };
        private static readonly int[] PMan = { 1, 2, 4 }, PHorse = { 1, 2, 3 }, PRoom = { 3, 8, 20 };
        private static readonly float[] QSpeed = { 5f, 10f, 12f }, QMorale = { 2f, 5f, 8f }, QSleep = { 1f, 1.25f, 1.25f }, QHeal = { 0f, 0f, 0.5f };
        private const int DuskHour = 18;             // zmierzch (NightRest.DayPart: 18-21)
        private const float PriceStep = 0.03f;       // dobry dom: kupno / sprzedaz najwyzej 3% lepiej (krok z rozpietosci gry - MarketPostfix)
        private const long HoldMaxMin = 12 * 60;     // czekanie na swit tylko, gdy swit najwyzej 12 h przed nami
        private const long RestedMaxMin = 12 * 60;   // recenzja 16.10: wypoczeci do zmierzchu, ale najwyzej 12 h od pobudki
        private const long DropAwakeMin = 120;       // oplacony nocleg bez snu w menu dluzej niz 2 h - stan zdjety (log)
        private static int PriceGainCap { get { return 5 * PRoom[2]; } }
        /// <summary>m95-nocleg (Jeff 20.10 "4 denary za nocleg to nie za duzo? sprawdz, czy stawki sa realne"): we wspolnej izbie gracz spi na slomie wsrod swoich ludzi
        /// i placi za lozko jak czlowiek (PMan[0]); wlasny pokoj tylko w porzadnej karczmie i dobrym domu. Klucz Inn Common Room Shared Bed (NIE = 3 d jak dotad).</summary>
        internal static int RoomOf(int q) { var s = Settings.Current; return (q == 0 && s != null && s.InnCommonRoomSharedBed) ? PMan[0] : PRoom[q]; }
        /// <summary>m95-nocleg: napis "your room" albo "your bed" (wspolna izba z kluczem).</summary>
        internal static string RoomWord(int q) { var s = Settings.Current; return (q == 0 && s != null && s.InnCommonRoomSharedBed) ? "your bed" : "your room"; }   // recenzja 16.10: zysk z premii cen na jeden nocleg najwyzej 5 x pokoj dobrego domu

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownInns && s.NightRestEnabled; } }
        private static bool PriceKey { get { var s = Settings.Current; return s != null && s.TownInnPriceBonus; } }

        // ------------------------------------------------------------ stan noclegu (zapis arm_inns)
        /// <summary>Faza 1 - nocleg oplacony, sen w menu; faza 2 - wypoczety do UntilMin. Pola premii stale; zmiana fazy = nowy obiekt (predkosc czyta rownolegle).</summary>
        private sealed class Rest
        {
            internal int Phase, Q, PlayerQ;
            internal string TownId, TownName;
            internal long CommitMin, StartMin, WakeMin, UntilMin;   // minuty kampanii: zaplata, poczatek snu, pobudka nie przed (0 = bez czekania), koniec premii
            internal float Speed, Morale, Sleep = 1f, Heal;
            internal bool Price;                                    // gracz spal w dobrym domu - ceny w tym miescie (klucz TownInnPriceBonus czytany na zywo)
            internal long PriceGain;                                // szacowany zysk gracza z premii cen w tym noclegu (limit PriceGainCap)
            internal string RoadUid;                                // sw-zajazdy: nocleg w zajezdzie przy szlaku (uid z arm_roadside_inns.tsv; TownId = wies okregu); null = karczma miasta
            internal string HostUid;                                // n20-wioska: nocleg we wlasnej wiosce pobocznej gracza (uid wioski; TownId = wies gry okregu) - TownInns.Host.cs

            /// <summary>Nazwa miasta do logu i komunikatow (po wczytaniu szukana przy pierwszym uzyciu, na watku glownym).</summary>
            internal string Town
            {
                get
                {
                    if (TownName == null && RoadUid != null) TownName = RoadsideInns.NameOf(RoadUid);   // sw-zajazdy: nazwa zajazdu (null - jak dotad nizej)
                    if (TownName == null)
                    {
                        try { var st = Settlement.Find(TownId); TownName = st != null ? st.Name.ToString() : TownId; }
                        catch { TownName = TownId; }
                    }
                    return TownName;
                }
            }

            internal Rest Copy(int phase)
            {
                return new Rest { Phase = phase, Q = Q, PlayerQ = PlayerQ, TownId = TownId, TownName = TownName, CommitMin = CommitMin, StartMin = StartMin,
                                  WakeMin = WakeMin, UntilMin = UntilMin, Speed = Speed, Morale = Morale, Sleep = Sleep, Heal = Heal, Price = Price, PriceGain = PriceGain,
                                  RoadUid = RoadUid, HostUid = HostUid };
            }
        }
        private static volatile Rest _rest;

        // liczniki doby (linia "dzien") i sesji
        private static int _dStays, _dHorses, _dArmHorses, _dWoke, _dNoRest, _dDropped, _dHealH, _dBuyN, _dSellN, _dPriceCut, _stumbles, _stumblesDay, _priceHooks, _healHooks;
        // potkniecia predkosci i morale (liczone rownolegle - Interlocked): doba i od wczytania
        private static int _parStumbles, _parStumblesDay;
        private static readonly int[] _dPeople = new int[4];
        private static long _dMen, _dComp, _dCompPurse, _dPlayer, _dFolk, _dCoffer, _dDiff, _dBuy, _dSell;
        private static bool _errLogged;
        // wolna sakiewka ludzi do podgladu w menu (TroopSelfMend.OutstandingCost nie jest tani) - liczona raz na minute gry; zaplata liczy od nowa
        private static long _freeAt = -1; private static int _freeCache, _purseCache;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (!_errLogged) { _errLogged = true; Log.Error("TownInns." + where, e); }
        }

        /// <summary>Nowa kampania albo wczytanie (konstruktor ArmouryBehavior, PRZED SyncData): stan noclegu i liczniki od zera.</summary>
        internal static void Reset()
        {
            _rest = null;
            DayReset();
            _stumbles = 0; _errLogged = false; _freeAt = -1;
            Interlocked.Exchange(ref _parStumbles, 0);
        }

        private static void DayReset()
        {
            _dStays = _dHorses = _dArmHorses = _dWoke = _dNoRest = _dDropped = _dHealH = _dBuyN = _dSellN = _dPriceCut = _stumblesDay = 0;
            Interlocked.Exchange(ref _parStumblesDay, 0);
            Array.Clear(_dPeople, 0, 4);
            _dMen = _dComp = _dCompPurse = _dPlayer = _dFolk = _dCoffer = _dDiff = _dBuy = _dSell = 0;
        }

        /// <summary>Wyjatek w predkosci / morale (liczone rownolegle): tylko licznik Interlocked - do linii doby.</summary>
        private static void ParStumble()
        {
            Interlocked.Increment(ref _parStumbles);
            Interlocked.Increment(ref _parStumblesDay);
        }

        // ------------------------------------------------------------ czas
        private static long NowMin() { return (long)CampaignTime.Now.ToMinutes; }
        private static long Mod(long a, long m) { long r = a % m; return r < 0 ? r + m : r; }
        private static long NextAt(long now, int hour) { long d = now - Mod(now, 1440) + hour * 60L; return d <= now ? d + 1440 : d; }
        private static string When(long min) { return (min / 1440).ToString(Inv) + " " + (Mod(min, 1440) / 60).ToString("00", Inv) + ":" + Mod(min, 60).ToString("00", Inv); }
        private static string Clock(long min) { return (Mod(min, 1440) / 60).ToString("00", Inv) + ":" + Mod(min, 60).ToString("00", Inv); }
        private static string F1(float v) { return v.ToString("0.0", Inv); }

        // ------------------------------------------------------------ rachunek
        private sealed class Bill
        {
            // Mounted - konni szeregowi z koniem; Unhorsed - konni bez konia w zbrojowni DTE (TroopsFightWithOwnKitOnly) - wliczeni do Foot
            // CompCost[i] = zaplacone za towarzysza (kiesa CompGold[i] + sakiewka ludzi CompPurse[i]); CompWaivedSum - czesc z sakiewki darowana w oknie lupu
            internal int Q, Foot, Mounted, Unhorsed, MenCost, MenFree, MenPurse, CompCostSum, CompGoldSum, CompPurseSum, CompWaivedSum, Spare, ArmorySpare, PlayerCost, Persons;
            internal readonly int[] FootAt = new int[3], MountAt = new int[3];
            internal readonly int[] People = new int[4];          // 0 stajnia, 1..3 = jakosc 0..2 (szeregowi, towarzysze, gracz)
            internal bool MenNoPurse, OwnHorse, PlayerCan, Sacked, Armory;
            internal readonly List<Hero> Comp = new List<Hero>();
            internal readonly List<int> CompQ = new List<int>(), CompCost = new List<int>(), CompGold = new List<int>(), CompPurse = new List<int>();
            internal int Total { get { return MenCost + CompCostSum + PlayerCost; } }
            internal int MenStable { get { int n = Foot + Mounted; for (int l = 0; l < 3; l++) n -= FootAt[l] + MountAt[l]; return n; } }
        }

        /// <summary>Konie zbrojowni DTE gracza (ArmyArmory): wierzchowce i juczne osobno, bez zywego inwentarza. false - zbrojowni DTE nie ma.</summary>
        private static bool ArmoryHorses(out int mounts, out int pack)
        {
            mounts = pack = 0;
            var r = QuartermasterLaw.DteArmory();
            if (r == null) return false;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.ItemType != ItemObject.ItemTypeEnum.Horse || it.HorseComponent == null || it.HorseComponent.IsLiveStock) continue;
                if (it.HorseComponent.IsPackAnimal) pack += el.Amount; else mounts += el.Amount;
            }
            return true;
        }

        /// <summary>Okno lupu (CitySack L3): co szloby z sakiewki ludzi, jest darowane. Bramka bez licznika doby postoju (menu odswiezane co klatke).</summary>
        private static bool SackedHere(Settlement st, MobileParty mp) { return MenPurse.On && CitySack.StayWindow(st, mp); }

        private static int ManCost(int q, bool horse) { return q < 0 ? 0 : PMan[q] + (horse ? PHorse[q] : 0); }

        private static bool HasMount(Hero h)
        {
            try { var eq = h != null ? h.BattleEquipment : null; return eq != null && !eq[EquipmentIndex.Horse].IsEmpty; }
            catch { return false; }
        }

        /// <summary>Konie bez jezdzca: wierzchowce i juczne w taborze gracza (ItemRoster), bez zywego inwentarza.</summary>
        private static int SpareHorses(MobileParty mp)
        {
            int n = 0;
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.ItemType != ItemObject.ItemTypeEnum.Horse || it.HorseComponent == null || it.HorseComponent.IsLiveStock) continue;
                n += el.Amount;
            }
            return n;
        }

        /// <summary>Wolna sakiewka ludzi gracza: po rezerwie napraw (z szacunkiem materialu z polki tego miasta, jak OnLeft), odziezy i oszczednosciach K1.</summary>
        private static int MenFree(MobileParty mp, Settlement st, bool fresh, out int purse)
        {
            long now = NowMin();
            if (!fresh && _freeAt == now) { purse = _purseCache; return _freeCache; }
            purse = MenPurse.Get(mp);
            int res, kept;
            long mend = Math.Max(0, TroopSelfMend.OutstandingCost(st));
            int free = MenPurse.CampFree(mp, mend, 1f, out res, out kept);
            _freeAt = now; _freeCache = free; _purseCache = purse;
            return free;
        }

        /// <summary>Rozdzial szeregowych: kazdy poziom od wspolnej izby do wybranego - najpierw piesi (taniej), potem konni; kto nie wszedl - zostaje nizej.</summary>
        private static void Allocate(Bill b, int q, long budget)
        {
            int fPrev = b.Foot, mPrev = b.Mounted;
            long left = Math.Max(0, budget);
            for (int l = 0; l <= q; l++)
            {
                int dF = ManCost(l, false) - ManCost(l - 1, false), dM = ManCost(l, true) - ManCost(l - 1, true);
                int nF = dF > 0 ? (int)Math.Min(fPrev, left / dF) : fPrev; left -= (long)nF * dF;
                int nM = dM > 0 ? (int)Math.Min(mPrev, left / dM) : mPrev; left -= (long)nM * dM;
                b.FootAt[l] = nF; b.MountAt[l] = nM;
                if (l > 0) { b.FootAt[l - 1] -= nF; b.MountAt[l - 1] -= nM; }
                fPrev = nF; mPrev = nM;
            }
            b.MenCost = 0;
            for (int l = 0; l <= q; l++) b.MenCost += b.FootAt[l] * ManCost(l, false) + b.MountAt[l] * ManCost(l, true);
        }

        /// <summary>Rachunek jakosci q. sacked - okno lupu (SackedHere): szeregowi w wybranej jakosci bez zaplaty, towarzysze placa tylko z wlasnej kiesy (czesc
        /// z sakiewki ludzi darowana). Ten sam rachunek w podpowiedzi menu i przy zaplacie.</summary>
        private static Bill Plan(Settlement st, int q, bool fresh, bool sacked)
        {
            var b = new Bill { Q = q, Sacked = sacked };
            var mp = MobileParty.MainParty;
            var me = Hero.MainHero;
            var roster = mp.MemberRoster;
            int mountedRaw = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || el.Number <= 0) continue;
                if (c.IsHero)
                {
                    var h = c.HeroObject;
                    if (h != null && h != me && h.IsAlive) b.Comp.Add(h);
                    continue;
                }
                if (c.IsMounted) mountedRaw += el.Number; else b.Foot += el.Number;
            }
            // zbrojownia DTE (recenzja 16.10): konie ponad liczbe konnych szeregowych nie niosa nikogo - rachunek gracza; przy TroopsFightWithOwnKitOnly konny bez
            // konia w zbrojowni idzie pieszo (placi jak pieszy). Bez zbrojowni DTE - jak dotad (konny = IsMounted)
            b.Mounted = mountedRaw;
            int armMounts, armPack;
            b.Armory = ArmoryHorses(out armMounts, out armPack);
            if (b.Armory)
            {
                b.ArmorySpare = Math.Max(0, armMounts - mountedRaw) + armPack;
                if (GarrisonKit.OwnKitOn)
                {
                    b.Unhorsed = Math.Max(0, mountedRaw - armMounts);
                    b.Mounted = mountedRaw - b.Unhorsed;
                    b.Foot += b.Unhorsed;
                }
            }
            if (MenPurse.On) { int purse; b.MenFree = MenFree(mp, st, fresh, out purse); b.MenPurse = purse; }
            else b.MenNoPurse = true;
            if (sacked) { b.FootAt[q] = b.Foot; b.MountAt[q] = b.Mounted; b.MenCost = 0; }
            else Allocate(b, q, b.MenFree);
            for (int l = 0; l < 3; l++) b.People[l + 1] += b.FootAt[l] + b.MountAt[l];
            b.People[0] += b.MenStable;
            // towarzysze (recenzja 16.10): najpierw wlasna kiesa, brak do wybranej jakosci z wolnej sakiewki ludzi, ktora zostala po szeregowych (tam idzie ich zold -
            // SoldierPay wplaca TotalWage druzyny gracza, z zoldem bohaterow, do sakiewki ludzi); nie starcza - najlepsza tansza albo stajnia
            // przeglad calosci 20.10: przy CompanionWageByLevel zold takiego bohatera idzie do JEGO kiesy (SoldierPay -> CompanionWage.PayFromWage), nie do
            // sakiewki ludzi - placi wiec tylko z kiesy; kogo nie stac, spi w tanszym lozku albo w stajni, jak zolnierz
            long left = b.MenNoPurse || sacked ? 0 : Math.Max(0, (long)b.MenFree - b.MenCost);
            foreach (var h in b.Comp)
            {
                bool horse = HasMount(h);
                int gold = Math.Max(0, h.Gold);
                bool ownPay = CompanionWage.Applies(h);
                long reach = ownPay ? 0 : left;
                int hq = q;
                if (!sacked) while (hq >= 0 && ManCost(hq, horse) > gold + reach) hq--;
                int cost = ManCost(hq, horse);
                int fromGold = Math.Min(gold, cost);
                int fromPurse = sacked || ownPay ? 0 : cost - fromGold;
                if (sacked) b.CompWaivedSum += cost - fromGold;
                left -= fromPurse;
                b.CompQ.Add(hq); b.CompGold.Add(fromGold); b.CompPurse.Add(fromPurse); b.CompCost.Add(fromGold + fromPurse);
                b.CompGoldSum += fromGold; b.CompPurseSum += fromPurse; b.CompCostSum += fromGold + fromPurse;
                b.People[hq + 1]++;
            }
            b.OwnHorse = HasMount(me);
            b.Spare = SpareHorses(mp);
            b.PlayerCost = RoomOf(q) + ((b.OwnHorse ? 1 : 0) + b.Spare + b.ArmorySpare) * PHorse[q];
            b.PlayerCan = me.Gold >= b.PlayerCost;
            b.People[q + 1]++;
            b.Persons = b.Foot + b.Mounted + b.Comp.Count + 1;
            return b;
        }

        private static void Bonus(Bill b, out float speed, out float morale, out float sleep, out float heal)
        {
            speed = morale = heal = 0f; sleep = 1f;
            if (b.Persons <= 0) return;
            for (int l = 0; l < 3; l++)
            {
                float sh = b.People[l + 1] / (float)b.Persons;
                speed += sh * QSpeed[l]; morale += sh * QMorale[l]; sleep += sh * (QSleep[l] - 1f); heal += sh * QHeal[l];
            }
        }

        // ------------------------------------------------------------ menu
        private static bool Allowed(Settlement st, out string why)
        {
            why = null;
            var mp = MobileParty.MainParty;
            if (st == null || !st.IsTown || st.Town == null || mp == null || Hero.MainHero == null) { why = "There is no inn to be had here."; return false; }
            if (st.IsUnderSiege) { why = "The town is under siege - no inn takes guests now."; return false; }
            if (mp.MapEvent != null) { why = "Not in the middle of a fight."; return false; }
            if (Campaign.Current != null && Campaign.Current.IsMainHeroDisguised) { why = "In disguise you cannot bring your company to an inn."; return false; }
            if (NightRest.EnlistedNow) { why = "In service your lord decides where the men sleep."; return false; }
            if (mp.Army != null && mp.Army.LeaderParty != mp) { why = "You march in another lord's army - he decides where the host sleeps."; return false; }
            // recenzja 16.10: flaga snu i menu snu naraz - przy wylaczonym PlayerSleepGuard flaga po przerwanym snie w obozie bywa nieaktualna
            if (InSleepMenu()) { why = "The men are already asleep."; return false; }
            return true;
        }

        /// <summary>Godzina, od ktorej nocleg czeka do switu (swit gracza minus 12 h; przy zwyklych godzinach obozu 18:00).</summary>
        private static string HoldFrom() { return Clock(Mod(NightRest.PlayerDawn * 60L - HoldMaxMin, 1440)); }

        /// <summary>Przewidywana pobudka po noclegu wzietym teraz - te same zasady co sen w menu (NightRest.SleepTick): cel = NightRest.SleepTargetNow (brak do
        /// NeededHours w tej dobie), noc snu NightRest.MenuNight pelna stawka (swit-9 do switu; swit 6 -> 21-5), dzien DayRestFactor, mnoznik lozka; czekanie do switu, gdy swit najwyzej 12 h przed nami; bezpiecznik
        /// snu NightRest.SleepCapHours. atDawn - nocleg czeka do switu. Szacunek co 15 min (podpowiedz menu).</summary>
        private static long PredictWake(long now, float sleepFactor, out bool atDawn, out long dawn)
        {
            dawn = NextAt(now, NightRest.PlayerDawn);
            atDawn = dawn - now <= HoldMaxMin;
            var s = Settings.Current;
            float day = s != null ? Math.Max(0.1f, Math.Min(1f, s.DayRestFactor)) : 0.5f;
            float target = NightRest.SleepTargetNow(), f = Math.Max(1f, sleepFactor);
            long cap = now + (long)(NightRest.SleepCapHours() * 60f);
            float acc = 0f;
            long t = now;
            while (t < cap && acc < target)
            {
                long h = Mod(t, 1440) / 60;
                acc += 0.25f * (NightRest.MenuNight((int)h) ? 1f : day) * f;   // m97-oboz: noc snu do switu gracza (swit-9), jak sen w menu
                t += 15;
            }
            if (atDawn && t < dawn) t = dawn;
            return Math.Min(t, cap);
        }

        /// <summary>Koniec stanu "wypoczeci": najblizszy zmierzch po pobudce, najwyzej 12 h od pobudki.</summary>
        private static long RestedUntil(long wake) { return Math.Min(NextAt(wake, DuskHour), wake + RestedMaxMin); }

        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("town", "arm_inn_enter", "{=!}Take lodgings at an inn", EnterCondition,
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Menu); }, false, 8);
                starter.AddGameMenu(Menu, "{=!}{ARM_INN_TEXT}", MenuInit, GameMenu.MenuOverlayType.SettlementWithBoth);
                for (int i = 0; i < 3; i++)
                {
                    int q = i;
                    starter.AddGameMenuOption(Menu, "arm_inn_q" + q, "{=!}{ARM_INN_Q" + q + "}",
                        delegate (MenuCallbackArgs a) { return QCondition(a, q); },
                        delegate (MenuCallbackArgs a) { Commit(q); }, false, q);
                }
                // sw-zajazdy (InnGossip): plotki karczmarza w miescie - okno z tekstem (bez zmiany menu); klucz wylaczony - opcji nie ma
                starter.AddGameMenuOption(Menu, "arm_inn_gossip", "{=!}{ARM_GOSSIP_TOWN}", Rumours.TownGossipCondition, Rumours.TownGossipConsequence, false, 5);
                // m95-menu (recenzja): napis mowi, dokad prowadzi - "Back to the tavern district", gdy wejscie stoi w dzielnicy karczm; inaczej jak dotad
                starter.AddGameMenuOption(Menu, "arm_inn_back", "{=!}{ARM_INN_BACK}",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; TownMenus.SetBackLabel(a, "ARM_INN_BACK", "arm_inn_enter", "Back to the town"); return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(TownMenus.BackTo("arm_inn_enter")); }, true, 9);   // m95-menu: do dzielnicy karczm, gdy wejscie tam przeniesione
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static bool EnterCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                var st = Settlement.CurrentSettlement;
                if (!On || st == null || !st.IsTown) return false;
                string why;
                if (!Allowed(st, out why)) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}" + why); }
                else args.Tooltip = new TextObject("{=!}A common room, a decent inn or a good house - the men pay for themselves. Taken from " + HoldFrom()
                                                   + " on, you sleep until dawn; by day, until the men have slept. You wake rested until dusk, at most 12 hours.");
                return true;
            }
            catch (Exception e) { Stumble("EnterCondition", e); return false; }
        }

        private static void MenuInit(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                try { if (st != null && st.SettlementComponent != null && !string.IsNullOrEmpty(st.SettlementComponent.WaitMeshName)) args.MenuContext.SetBackgroundMeshName(st.SettlementComponent.WaitMeshName); } catch { }
                var sb = new StringBuilder();
                sb.Append("The inns of ").Append(st != null ? st.Name.ToString() : "the town").Append(" offer three kinds of night (a man / a horse / your own room):\n");
                sb.Append("\nCommon room - straw and a roof: 1 / 1 / ").Append(RoomOf(0)).Append(RoomOf(0) == PMan[0] ? " (you sleep on the straw among your men)" : "").Append(". Rested until dusk: +5% speed on the map, +2 morale.");
                sb.Append("\nDecent inn - beds, a warm hearth and ale: 2 / 2 / 8. +10% speed, +5 morale, and sleep comes a quarter faster (sleep debt is paid off sooner).");
                sb.Append("\nGood house - rooms, wine and a bath: 4 / 3 / 20. +12% speed, +8 morale, sleep a quarter faster, the wounded heal half again as fast that night");
                sb.Append(PriceKey ? ", and after a night there you trade up to 3% better in this town until dusk." : ".");
                sb.Append("\n\nEvery soldier pays for himself and his horse out of his own purse - they draw their pay for it. A man who cannot afford your choice takes the best cheaper bed he can, or sleeps in the stable for free, as in camp. " + (CompanionWage.On ? "Companions pay from their own purses, where their pay goes." : "Companions pay from their own purses first and the rest from the men's purse, where their pay goes.") + " You pay for yourself, your horse and every horse that carries nobody - in the baggage or in the armoury. The men eat from their own wagons, as on the road. The party's rest follows how many slept in each kind of bed.");
                sb.Append("\nTaken from ").Append(HoldFrom()).Append(" on, you sleep until dawn; by day, until the men have slept. You wake rested until dusk, at most 12 hours - unless the men still owe sleep.");
                var mp = MobileParty.MainParty;
                if (mp != null && st != null)
                {
                    if (MenPurse.On)
                    {
                        int purse; int free = MenFree(mp, st, true, out purse);
                        sb.Append("\n\nYour men have ").Append(free).Append(" denars to spare (purse ").Append(purse).Append(", the rest is kept for mending and better kit).");
                        if (SackedHere(st, mp)) sb.Append(" The town is yours by the sack - the men take their beds without paying.");
                    }
                    else sb.Append("\n\nYour men keep no purses (Men Purse is off) - they sleep in the stable.");
                    int spare = SpareHorses(mp), am, ap, arm = 0;
                    if (ArmoryHorses(out am, out ap))
                    {
                        int riders = 0;
                        for (int i = 0; i < mp.MemberRoster.Count; i++) { var el = mp.MemberRoster.GetElementCopyAtIndex(i); if (el.Character != null && !el.Character.IsHero && el.Character.IsMounted) riders += el.Number; }
                        arm = Math.Max(0, am - riders) + ap;
                    }
                    if (spare + arm > 0) sb.Append(" Horses that carry nobody: ").Append(spare + arm).Append(arm > 0 ? " (" + spare + " in the baggage, " + arm + " in the armoury)." : ".");
                }
                MBTextManager.SetTextVariable("ARM_INN_TEXT", sb.ToString());
            }
            catch (Exception e) { Stumble("MenuInit", e); }
        }

        private static string QualityOf(int q) { return q < 0 ? "the stable" : QName[q].ToLowerInvariant(); }

        private static bool QCondition(MenuCallbackArgs args, int q)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                var st = Settlement.CurrentSettlement;
                string why;
                if (!On || !Allowed(st, out why)) return false;
                // recenzja 16.10: okno lupu w rachunku menu tak samo jak przy zaplacie (bramka bez licznika doby postoju)
                var b = Plan(st, q, false, SackedHere(st, MobileParty.MainParty));
                int men = b.Foot + b.Mounted;
                string split = b.Total + " d: you " + b.PlayerCost
                               + (men > 0 ? ", the men " + (b.Sacked ? "nothing (the sack)" : b.MenCost.ToString(Inv)) : "")
                               + (b.Comp.Count > 0 ? ", companions " + b.CompCostSum : "");
                // n20-napisy: na przycisku rodzaj noclegu i suma; co daje i kto ile placi - pierwsza linia podpowiedzi
                MBTextManager.SetTextVariable("ARM_INN_Q" + q, ShortLabels.Pick(QName[q] + " - " + b.Total + " d", QName[q] + " (" + QWhat[q] + ") - " + split));
                var t = new StringBuilder();
                if (ShortLabels.On) t.Append(QName[q]).Append(" (").Append(QWhat[q]).Append(") - ").Append(split).Append(".\n");
                t.Append(PMan[q]).Append(" d a man, ").Append(PHorse[q]).Append(" d a horse, ").Append(RoomOf(q)).Append(" d ").Append(RoomWord(q)).Append(".");
                if (men > 0)
                {
                    t.Append("\nThe men (").Append(men).Append(", ").Append(b.Mounted).Append(" mounted");
                    if (b.Unhorsed > 0) t.Append(", ").Append(b.Unhorsed).Append(" riders with no horse in the armoury go as footmen");
                    t.Append("): ");
                    var parts = new List<string>();
                    for (int l = 2; l >= 0; l--) { int n = b.FootAt[l] + b.MountAt[l]; if (n > 0) parts.Add(n + " in a " + QualityOf(l)); }
                    if (b.MenStable > 0) parts.Add(b.MenStable + " in the stable");
                    t.Append(string.Join(", ", parts.ToArray()))
                     .Append(b.Sacked ? " - by right of the sack they pay nothing." : b.MenNoPurse ? " - they keep no purses." : " - they pay " + b.MenCost + " of the " + b.MenFree + " d they can spare.");
                }
                if (b.Comp.Count > 0)
                {
                    t.Append("\nCompanions: ");
                    var parts = new List<string>();
                    for (int i = 0; i < b.Comp.Count; i++)
                        parts.Add(b.Comp[i].Name + " " + (b.CompQ[i] < 0 ? "the stable"
                                  : QualityOf(b.CompQ[i]) + " " + b.CompCost[i] + " d" + (b.CompPurse[i] > 0 ? " (" + b.CompGold[i] + " their own, " + b.CompPurse[i] + " from the men's purse)" : "")));
                    t.Append(string.Join(", ", parts.ToArray())).Append(".");
                    if (b.CompWaivedSum > 0) t.Append(" The sack spares them ").Append(b.CompWaivedSum).Append(" d they would take from the men's purse.");
                }
                t.Append("\nYou: ").Append(RoomWord(q) == "your bed" ? "bed " : "room ").Append(RoomOf(q));
                if (b.OwnHorse) t.Append(" + your horse ").Append(PHorse[q]);
                if (b.Spare > 0) t.Append(" + ").Append(b.Spare).Append(b.Spare == 1 ? " horse" : " horses").Append(" in the baggage that carry nobody x ").Append(PHorse[q]);
                if (b.ArmorySpare > 0) t.Append(" + ").Append(b.ArmorySpare).Append(b.ArmorySpare == 1 ? " horse" : " horses").Append(" in the armoury that carry nobody x ").Append(PHorse[q]);
                t.Append(" = ").Append(b.PlayerCost).Append(" d (you have ").Append(Hero.MainHero.Gold).Append(").");
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                // recenzja 16.10: godzina pobudki wprost - wieczorem i w nocy do switu, w dzien do wyspania
                bool atDawn; long dawn;
                long wake = PredictWake(NowMin(), sl, out atDawn, out dawn);
                t.Append(!atDawn ? "\nBy day you sleep until the men have slept (about " : wake <= dawn ? "\nYou sleep until dawn (" : "\nYou sleep past dawn, until the men have slept (about ")
                 .Append(Clock(wake)).Append(") and wake rested until ").Append(Clock(RestedUntil(wake))).Append(": +").Append(F1(sp)).Append("% speed, +").Append(F1(mo)).Append(" morale");
                if (sl > 1.001f) t.Append(", sleep x").Append(sl.ToString("0.00", Inv));
                if (he > 0.001f) t.Append(", healing tonight +").Append((he * 100f).ToString("0", Inv)).Append("%");
                if (q == 2 && PriceKey) t.Append(atDawn ? ", you trade up to 3% better here" : " (better prices here only after a night until dawn)");
                t.Append(".");
                if (NightRest.Debt > 0) t.Append(" The men still owe sleep - the bed's comfort shows once it is paid off.");
                if (!b.PlayerCan) { args.IsEnabled = false; t.Append("\nYou cannot pay your own part (").Append(b.PlayerCost).Append(" d)."); }
                args.Tooltip = new TextObject("{=!}" + t.ToString());
                return true;
            }
            catch (Exception e) { Stumble("QCondition", e); return false; }
        }

        // ------------------------------------------------------------ zaplata
        /// <summary>Suma posiadaczy dotknietych noclegiem (kontrola obiegu): sakiewki ludzi, kiesy gracza i towarzyszy, kasa miasta, kiesy ludu miast i gminy.</summary>
        private static long World(Settlement st, List<Hero> comp)
        {
            long w = MenPurse.TotalNow() + Hero.MainHero.Gold + st.Town.Gold + TownFolk.TotalTown() + TownFolk.TotalGmina();
            foreach (var h in comp) w += h.Gold;
            return w;
        }

        /// <summary>Bohater -> karczmarze: przelew gry do kasy miasta, potem kasa -> kiesa ludu (TownFolk.EarnFromTown, uslugi - droga oplat gracza PlayerFees8).</summary>
        private static int HeroPays(Hero h, Settlement st, int cost, bool quiet, out int folk)
        {
            folk = 0;
            if (h == null || cost <= 0) return 0;
            int had = h.Gold;
            GiveGoldAction.ApplyForCharacterToSettlement(h, st, cost, quiet);
            int paid = Math.Max(0, had - h.Gold);
            if (paid > 0)
            {
                int t0 = st.Town.Gold;
                TownFolk.EarnFromTown(st, paid, TownFolk.KLife);
                folk = Math.Max(0, Math.Min(paid, t0 - st.Town.Gold));
            }
            return paid;
        }

        private static void Commit(int q)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                var mp = MobileParty.MainParty;
                var me = Hero.MainHero;
                string why = null;
                if (!On || !Allowed(st, out why)) { if (On) Log.Player(why ?? "No inn takes you in now.", true); return; }
                var b = Plan(st, q, true, SackedHere(st, mp));
                if (!b.PlayerCan) { Log.Player("You cannot pay your own part of the lodging (" + b.PlayerCost + " d).", true); return; }
                long w0 = World(st, b.Comp);
                // sakiewka ludzi: szeregowi i doplata towarzyszy jednym przelewem (ta sama ksiega InnPay); rozdzial zaplaconego - najpierw szeregowi
                int purseFolk = 0, pursePaid = 0, purseNeed = b.MenCost + b.CompPurseSum;
                if (purseNeed > 0) pursePaid = MenPurse.InnPay(mp, st, purseNeed, out purseFolk);
                int menPaid = Math.Min(pursePaid, b.MenCost), compPursePaid = pursePaid - menPaid;
                int compGoldPaid = 0, compFolk = 0;
                var compLog = new StringBuilder();
                for (int i = 0; i < b.Comp.Count; i++)
                {
                    int f, paid = HeroPays(b.Comp[i], st, b.CompGold[i], true, out f);
                    compGoldPaid += paid; compFolk += f;
                    if (compLog.Length > 0) compLog.Append(", ");
                    compLog.Append(b.Comp[i].StringId).Append(' ').Append(b.CompQ[i] < 0 ? "stajnia" : QLog[b.CompQ[i]]).Append(' ').Append(paid + b.CompPurse[i])
                           .Append(" (kiesa ").Append(paid).Append(", sakiewka ludzi ").Append(b.CompPurse[i]).Append(')');
                }
                int compPaid = compGoldPaid + compPursePaid;
                int playerFolk, playerPaid = HeroPays(me, st, b.PlayerCost, PayNotes.Quiet(false), out playerFolk);   // m96-wies: komunikat gry wylaczony przy PlayerPaymentReasons (InnNote)
                long diff = World(st, b.Comp) - w0;
                int paidAll = menPaid + compPaid + playerPaid, folkAll = purseFolk + compFolk + playerFolk;
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                long now = NowMin();
                _rest = new Rest { Phase = 1, Q = q, PlayerQ = q, TownId = st.StringId, TownName = st.Name.ToString(), CommitMin = now,
                                   Speed = sp, Morale = mo, Sleep = sl, Heal = he, Price = q == 2 };
                _dStays++; _dHorses += b.Spare; _dArmHorses += b.ArmorySpare; for (int l = 0; l < 4; l++) _dPeople[l] += b.People[l];
                _dMen += menPaid; _dComp += compPaid; _dCompPurse += compPursePaid; _dPlayer += playerPaid; _dFolk += folkAll; _dCoffer += paidAll - folkAll; _dDiff += diff;
                Log.Info(P + "nocleg w " + st.Name + " (" + st.StringId + ") " + When(now) + " - wybor " + q + " (" + QLog[q] + ")"
                         + " | szeregowi " + (b.Foot + b.Mounted) + " (pieszych " + b.Foot + ", konnych " + b.Mounted
                         + (b.Unhorsed > 0 ? ", konnych bez konia w zbrojowni (jak piesi) " + b.Unhorsed : "") + "): wspolna izba " + (b.FootAt[0] + b.MountAt[0])
                         + ", porzadna karczma " + (b.FootAt[1] + b.MountAt[1]) + ", dobry dom " + (b.FootAt[2] + b.MountAt[2]) + ", stajnia " + b.MenStable
                         + "; sakiewka ludzi " + b.MenPurse + ", wolne " + b.MenFree + ", zaplacili " + menPaid + " z " + b.MenCost
                         + (b.MenNoPurse ? " (sakiewki wylaczone)" : "") + (b.Sacked ? " (okno lupu - bez zaplaty z sakiewki)" : "")
                         + " | towarzysze " + b.Comp.Count + (compLog.Length > 0 ? ": " + compLog : "") + ", z kiesy " + compGoldPaid + ", z sakiewki ludzi " + compPursePaid
                         + " z " + b.CompPurseSum + (b.CompWaivedSum > 0 ? ", darowane w oknie lupu " + b.CompWaivedSum : "")
                         + " | gracz: pokoj " + RoomOf(q) + ", swoj kon " + (b.OwnHorse ? 1 : 0)
                         + ", konie bez jezdzca " + (b.Spare + b.ArmorySpare) + " (tabor " + b.Spare + ", zbrojownia " + (b.Armory ? b.ArmorySpare.ToString(Inv) : "brak") + ") x " + PHorse[q]
                         + " = " + b.PlayerCost + ", zaplacil " + playerPaid
                         + " | razem " + paidAll + " (ludzie " + menPaid + ", towarzysze " + compPaid + ", gracz " + playerPaid + ") -> kiesa ludu " + folkAll + ", kasa miasta " + (paidAll - folkAll)
                         + " | kontrola obiegu: roznica " + diff
                         + " | osoby " + b.Persons + ": stajnia " + b.People[0] + ", wspolna izba " + b.People[1] + ", porzadna karczma " + b.People[2] + ", dobry dom " + b.People[3]
                         + " | premia po snie: predkosc +" + F1(sp) + "%, morale +" + F1(mo) + ", sen x" + sl.ToString("0.00", Inv) + ", gojenie +" + (he * 100f).ToString("0", Inv) + "%"
                         + ", cena " + (q == 2 && PriceKey ? "tak" : "nie") + ".");
                string men = b.Foot + b.Mounted == 0 ? "" : b.Sacked ? " Your men take their beds by right of the sack and pay nothing." : " Your men paid " + menPaid + " from their purses" + (b.MenStable > 0 ? ", " + b.MenStable + " of them sleep in the stable." : ".");
                string comps = compPaid > 0 ? " Your companions paid " + compPaid + (compPursePaid > 0 ? " (" + compPursePaid + " of it from the men's purse, where their pay goes)." : ".") : "";
                InnNote("inn", q, st.Name.ToString(), b, playerPaid, men + comps,   // m96-wies: jeden komunikat z powodem i rozbiciem (klucz wylaczony - jak dotad)
                        "You take lodgings at a " + QualityOf(q) + " in " + st.Name + " - " + paidAll + " denars in all, " + playerPaid + " of them yours." + men + comps);
                NightRest.BeginInnSleep();
            }
            catch (Exception e) { Stumble("Commit", e); }
        }

        // ------------------------------------------------------------ sen (wolane z NightRest)
        /// <summary>NightRest.SleepInit w osadzie: oplacony nocleg w tym miescie - poczatek snu, pobudka nie przed switem (gdy swit w 12 h), tlo miasta, tekst.
        /// null = sen bez noclegu (tekst NightRest jak dotad). Po wczytaniu w trakcie snu (faza 1 z zapisu) godziny zostaja z zapisu.</summary>
        internal static string OnSleepInit(MenuCallbackArgs args)
        {
            try
            {
                var r = _rest;
                var mp = MobileParty.MainParty;
                var st = mp != null ? mp.CurrentSettlement : null;
                if (r != null && r.HostUid != null) return HostSleepInit(args, r);   // n20-wioska: sen we wlasnej wiosce pobocznej (TownInns.Host.cs)
                if (r != null && r.RoadUid != null) return RoadSleepInit(args, r);   // sw-zajazdy: sen w zajezdzie przy szlaku (TownInns.Road.cs)
                if (r == null || r.Phase != 1 || st == null || st.StringId != r.TownId) return null;
                long now = NowMin();
                if (r.StartMin <= 0)
                {
                    r.StartMin = now;
                    long dawn = NextAt(now, NightRest.PlayerDawn);
                    r.WakeMin = dawn - now <= HoldMaxMin ? dawn : 0;
                    Log.Info(P + "sen w karczmie (" + QLog[Math.Max(0, r.Q)] + ") w " + r.Town + " od " + When(now) + ", "
                             + (r.WakeMin > 0 ? "pobudka nie przed " + When(r.WakeMin) + " (swit)" : "bez czekania do switu (swit dalej niz 12 h) - sen do wyspania")
                             + ", sen x" + r.Sleep.ToString("0.00", Inv) + ", gojenie +" + (r.Heal * 100f).ToString("0", Inv) + "%.");
                }
                try { if (st.SettlementComponent != null && !string.IsNullOrEmpty(st.SettlementComponent.WaitMeshName)) args.MenuContext.SetBackgroundMeshName(st.SettlementComponent.WaitMeshName); } catch { }
                string bed = r.Q == 2 ? "The house gives you rooms, a bath and wine; the men sleep in proper beds between clean sheets"
                           : r.Q == 1 ? "The men drink their ale by the hearth and sleep in beds"
                           : "The men bed down on the straw of the common room";
                return bed + (r.WakeMin > 0 ? " - nobody stirs before dawn." : ".");
            }
            catch (Exception e) { Stumble("OnSleepInit", e); return null; }
        }

        /// <summary>NightRest.SleepTick: mnoznik godzin snu w lozku (porzadna karczma / dobry dom - x1.25 wazone liczba osob); poza noclegiem 1.</summary>
        internal static float SleepFactor()
        {
            var r = InnSleep();
            return r != null && r.Sleep > 1f ? r.Sleep : 1f;
        }

        /// <summary>Oplacony nocleg, ktorego sen zaczal sie w karczmie (OnSleepInit), a partia jest w tym miescie - inaczej null (sen w obozie po przerwanym
        /// noclegu przy wylaczonym PlayerSleepGuard nie dostaje mnoznika, czekania do switu ani premii). Watek glowny (SleepTick, LeaveSleep).</summary>
        private static Rest InnSleep()
        {
            var r = _rest;
            if (r == null || r.Phase != 1 || r.StartMin <= 0 || !On) return null;
            try
            {
                if (r.HostUid != null) return HostNear(r) ? r : null;   // n20-wioska: wlasna wioska - partia przy niej albo w wsi gry okregu
                if (r.RoadUid != null) return RoadNear(r) ? r : null;   // sw-zajazdy: zajazd przy szlaku - partia poza osada, przy zajezdzie
                var mp = MobileParty.MainParty;
                var st = mp != null ? mp.CurrentSettlement : null;
                return st != null && st.StringId == r.TownId ? r : null;
            }
            catch { return null; }
        }

        /// <summary>NightRest.SleepTick: oplacony nocleg - pasek pelny, ale do switu nikt nie wstaje.</summary>
        internal static bool HoldAbed()
        {
            var r = InnSleep();
            return r != null && r.WakeMin > 0 && NowMin() < r.WakeMin;
        }

        /// <summary>NightRest.SleepTick: pasek snu przy czekaniu do switu - mniejsze z paska snu i zegara do switu.</summary>
        internal static float Progress(float p)
        {
            try
            {
                var r = InnSleep();
                if (r == null || r.WakeMin <= 0 || r.WakeMin <= r.StartMin) return p;
                float c = (NowMin() - r.StartMin) / (float)(r.WakeMin - r.StartMin);
                return Math.Max(0f, Math.Min(p, Math.Min(1f, c)));
            }
            catch { return p; }
        }

        /// <summary>NightRest.LeaveSleep: pobudka po noclegu - wypoczeci do zmierzchu, gdy pasek pelny albo przespana baza (SleepHoursNeeded); inaczej bez premii.</summary>
        internal static void OnWake(float menuRest, bool full) { Wake(menuRest, full, "pobudka"); }

        /// <summary>NightRest.EndSleepQuiet: sen w karczmie przerwany (atak, inne menu) - premia tylko po przespanej bazie.</summary>
        internal static void OnInterrupted(float menuRest, string why) { Wake(menuRest, false, "sen przerwany (menu '" + why + "')"); }

        private static void Wake(float menuRest, bool full, string how)
        {
            try
            {
                var r0 = _rest;
                if (r0 == null || r0.Phase != 1 || r0.StartMin <= 0) return;   // sen nie zaczal sie w karczmie (OnSleepInit) - ten sen to nie nocleg
                var r = InnSleep();
                if (r == null)
                {
                    // nocleg oplacony, ale ten sen nie jest w miescie noclegu (albo klucz wylaczony w trakcie) - stan zdjety, bez premii
                    _rest = null; _dNoRest++;
                    Log.Info(P + how + " poza miastem noclegu " + r0.Town + " (albo Town Inns wylaczone) " + When(NowMin()) + " - stan noclegu zdjety, bez premii.");
                    return;
                }
                var s = Settings.Current;
                float baseH = s != null ? Math.Max(1f, s.SleepHoursNeeded) : 6f;
                bool ok = full || menuRest + 0.01f >= baseH;
                long now = NowMin();
                if (!ok)
                {
                    _rest = null; _dNoRest++;
                    Log.Player(r.HostUid != null ? "The men were up before they had slept their fill " + HostWhere(r) + " - nobody wakes rested." : "The men were up before they had slept in their beds - the lodging is paid, but nobody wakes rested.", true);
                    Log.Info(P + how + " w " + r.Town + " " + When(now) + " - w menu " + F1(menuRest) + " h z bazy " + F1(baseH) + " h, pelny pasek nie -> bez premii.");
                    return;
                }
                var n = r.Copy(2);
                // recenzja 16.10: do zmierzchu, ale najwyzej 12 h od pobudki (sen dzienny po 18:00 nie daje premii na cala nastepna dobe); premia cen tylko po nocy -
                // sen czekal do switu (WakeMin > 0), nie po drzemce w dzien
                n.UntilMin = RestedUntil(now);
                n.Price = r.Price && r.WakeMin > 0;
                _rest = n; _dWoke++;
                var msg = new StringBuilder((n.HostUid != null ? (IsHouse(n) ? "Rested in your own house: until " : "Rested under your villagers' roofs: until ") : "Rested in a bed: until ") + Clock(n.UntilMin) + " the column marches " + F1(n.Speed) + "% faster and morale is +" + F1(n.Morale) + ".");
                if (n.Price && PriceKey) msg.Append(" The good house's name opens doors - you trade up to 3% better in ").Append(n.Town).Append(" until ").Append(Clock(n.UntilMin)).Append(".");
                if (NightRest.Debt > 0) msg.Append(n.HostUid != null ? " Old weariness still lingers - the night's rest shows once the sleep debt is paid off." : " Old weariness still lingers - the bed's comfort shows once the sleep debt is paid off.");
                // m98-plotki (recenzja gra, wazne): pijany albo skacowany - premia lozka wstrzymana w latkach NightRest (InnDrink.SpoilsRest), komunikat to mowi
                bool spoilt = InnDrink.SpoilsRest;
                if (spoilt) msg.Append(InnDrink.Drunk ? " But you are still drunk - the bed does you no good until you sleep it off." : " But the ale still pounds in your head - the bed does you no good until the hangover passes.");
                Log.Player(msg.ToString());
                Log.Info(P + how + " w " + n.Town + " " + When(now) + " - w menu " + F1(menuRest) + " h, pelny pasek " + (full ? "tak" : "nie") + " -> wypoczeci do "
                         + When(n.UntilMin) + " (predkosc +" + F1(n.Speed) + "%, morale +" + F1(n.Morale) + ", cena " + (n.Price && PriceKey ? "tak" : "nie")
                         + (r.Price && !n.Price ? " - bez nocy do switu" : "")
                         + ", dlug snu " + NightRest.Debt + (NightRest.Debt > 0 ? " - premia czeka na splate dlugu" : "") + (spoilt ? ", kara kufli (m98) - premia wstrzymana do konca kary" : "") + ").");
            }
            catch (Exception e) { Stumble("Wake", e); }
        }

        // ------------------------------------------------------------ premie (predkosc i morale z NightRest, gojenie, ceny)
        private static readonly TextObject _txtBed = new TextObject("{=!}Slept in a bed");
        private static readonly TextObject _txtRoof = new TextObject("{=!}Slept under a roof");   // przeglad calosci 20.10: nocleg we wlasnej wiosce (HostUid) - slome, nie lozko
        private static readonly TextObject _txtBath = new TextObject("{=!}A good bed and a bath");

        /// <summary>Wypoczety teraz (faza 2, przed zmierzchem, klucz). Bez wyjatku - czytane rownolegle (predkosc).</summary>
        private static Rest Live()
        {
            var r = _rest;
            if (r == null || r.Phase != 2 || !On) return null;
            try { return NowMin() < r.UntilMin ? r : null; } catch { return null; }
        }

        /// <summary>NightRest.SpeedPostfix (gracz bez dlugu snu, zewnetrzny poziom lancucha, PO MarchPace): +x% WYNIKU - dzielone przez (1 + suma wspolczynnikow),
        /// jak kara snu - i sufit LimitMax (kolumna marszowa MarchPace: "Marching column" sciaga wynik do sufitu, LimitMax go trzyma) w gore o ten sam procent.
        /// Recenzja 16.10 (krytyczne): bez tego Add podnosil tylko wartosc przed docieciem, ResultNumber zostawal na suficie, a rozpiska pokazywala "Slept in a bed +x"
        /// i "Maximum -x" (log 11-22-18: 6 z 9 audytow "=> 3.00" = sufit 4.0 x 75%). Wypoczety piechur idzie o x% szybciej niz zwykle tempo piechura - kolumna dalej
        /// idzie tempem najwolniejszego, tylko ten jest wypoczety. Zapas 0.001 - zaokraglenie float nie robi linii "Maximum -0.00".</summary>
        internal static void SpeedBonus(ref ExplainedNumber r)
        {
            try
            {
                var x = Live();
                if (x == null || x.Speed <= 0f) return;
                float pct = x.Speed / 100f;
                float lim = r.LimitMaxValue;
                float add = r.ResultNumber * pct;
                float f = 1f + r.SumOfFactors;
                if (add <= 0f) return;
                r.Add(f > 0.01f ? add / f : add, x.HostUid != null ? (IsHouse(x) ? _txtHouse : _txtRoof) : _txtBed);   // r4-dom: wlasny dom
                if (lim > 0f && lim < float.MaxValue * 0.5f) r.LimitMax(lim * (1f + pct) + 0.001f);
            }
            catch { ParStumble(); }
        }

        /// <summary>NightRest.MoralePostfix (gracz bez dlugu snu, zewnetrzny poziom lancucha): +punkty morale.</summary>
        internal static void MoraleBonus(ref ExplainedNumber r)
        {
            try
            {
                var x = Live();
                if (x != null && x.Morale > 0f) r.Add(x.Morale, x.HostUid != null ? (IsHouse(x) ? _txtHouse : _txtRoof) : _txtBed);   // r4-dom: wlasny dom
            }
            catch { ParStumble(); }
        }

        /// <summary>Gojenie w dobrym domu: partia gracza (czlonkowie, nie jency), tylko w czasie snu oplaconego noclegu; wynik x (1 + premia) - po SlowHealing (Low) i BK.
        /// Recenzja 16.10: Add w punktach (wynik x premia / (1 + suma wspolczynnikow)), nie AddFactor - rozpiska pokazuje przyrost, a nie procent, ktory przy ujemnej
        /// sumie wspolczynnikow (SlowHealing) wygladal na "+25%" przy obiecanych +50%. Dzialanie to samo: wynik rosnie o polowe.</summary>
        public static void HealPostfix(PartyBase __0, bool __1, ref ExplainedNumber __result)
        {
            try
            {
                if (__1 || __0 == null || __0 != PartyBase.MainParty) return;
                var r = _rest;
                if (r == null || r.Phase != 1 || r.Heal <= 0f || r.StartMin <= 0 || !On || !InSleepMenu()) return;
                float res = __result.ResultNumber;
                if (res <= 0f) return;   // kara (glod) - bez zmian
                float f = 1f + __result.SumOfFactors;
                if (f <= 0.01f) return;
                __result.Add(res * r.Heal / f, _txtBath);
            }
            catch (Exception e) { Stumble("HealPostfix", e); }
        }

        /// <summary>Menu snu (arm_sleep_wait) jest teraz otwarte - samo id menu, bez flagi snu (po wczytaniu gra potrafi odtworzyc menu oczekiwania bez init,
        /// wtedy flaga NightRest jest false, choc gracz stoi w menu snu). Watek glowny.</summary>
        private static bool SleepMenuOpen()
        {
            try
            {
                var c = Campaign.Current;
                var mc = c != null ? c.CurrentMenuContext : null;
                var gm = mc != null ? mc.GameMenu : null;
                return gm != null && gm.StringId == "arm_sleep_wait";
            }
            catch { return false; }
        }

        /// <summary>Gracz spi teraz w menu snu (flaga NightRest i menu arm_sleep_wait - przy wylaczonym PlayerSleepGuard flaga snu bywa nieaktualna). Watek glowny.</summary>
        private static bool InSleepMenu() { return NightRest.SleepingNow && SleepMenuOpen(); }

        [ThreadStatic] private static bool _tradeHere;

        private static Rest PriceLive()
        {
            if (!PriceKey) return null;
            var r = Live();
            return r != null && r.Price ? r : null;
        }

        /// <summary>Ekran handlu gracza z miastem noclegu w dobrym domu - flaga watku na czas InventoryLogic.GetItemPrice.</summary>
        public static void InvPrefix(InventoryLogic __instance)
        {
            _tradeHere = false;
            try
            {
                if (__instance == null || !__instance.IsTrading) return;
                var r = PriceLive();
                if (r == null) return;
                var op = __instance.OtherParty;
                _tradeHere = op != null && op.IsSettlement && op.Settlement != null && op.Settlement.StringId == r.TownId;
            }
            catch { _tradeHere = false; }
        }

        public static void InvFinalizer() { _tradeHere = false; }

        /// <summary>Cena rynku miasta pod flaga ekranu handlu. Recenzja 16.10: krok z rozpietosci gry tej sztuki liczonej w tej chwili (B kupno, S sprzedaz - obie bez
        /// premii, flaga zdjeta na czas drugiego odczytu): krok = min(3%, (B-S)/(2(B+S))) po obu stronach, czyli premia zjada najwyzej polowe rozpietosci gry.
        /// Kupno = B x (1-krok), nie mniej niz S+1; sprzedaz = S x (1+krok), nie wiecej niz kupno z premia - 1. Dotad stale 3% przy waskiej rozpietosci (Handel ok. 300,
        /// Whole Seller, Rumor Network, cecha kupca - ok. 1.059 < 1.062) dawalo maly zysk z kasy miasta na petli kupno-sprzedaz.</summary>
        public static void MarketPostfix(TownMarketData __instance, EquipmentElement __0, MobileParty __1, bool __2, PartyBase __3, ref int __result)
        {
            if (!_tradeHere) return;
            try
            {
                if (__1 == null || __1 != MobileParty.MainParty || __result <= 0 || __0.Item == null) return;
                int other;
                _tradeHere = false;
                try { other = __instance.GetPrice(__0, __1, !__2, __3); }
                finally { _tradeHere = true; }
                int buy = __2 ? other : __result, sell = __2 ? __result : other;
                if (buy <= sell || buy + sell <= 0) return;   // gra nie daje rozpietosci - bez premii
                float step = Math.Min(PriceStep, (buy - sell) / (2f * (buy + sell)));
                if (step <= 0f) return;
                int buyAdj = Math.Min(buy, Math.Max(sell + 1, (int)Math.Round(buy * (1f - step))));
                if (!__2) { __result = Math.Max(1, buyAdj); return; }
                int up = (int)Math.Round(sell * (1f + step));
                if (up >= buyAdj) up = Math.Max(sell, buyAdj - 1);
                __result = up;
            }
            catch (Exception e) { Stumble("MarketPostfix", e); }
        }

        /// <summary>PlayerInventoryExchangeEvent: handel w miescie noclegu przy czynnej premii cen - do linii doby (oszczednosc szacowana z 3% - gorny szacunek, krok bywa
        /// mniejszy) i do limitu zysku na nocleg (PriceGainCap = 5 x pokoj dobrego domu): po przekroczeniu premia cen zdjeta. Limit sprawdzany przy zamknieciu ekranu
        /// handlu (zdarzenie gry) - jeden ekran moze go przekroczyc o swoja czesc.</summary>
        internal static void OnPlayerExchange(List<(ItemRosterElement, int)> bought, List<(ItemRosterElement, int)> sold, bool isTrading)
        {
            try
            {
                if (!isTrading) return;
                var r = PriceLive();
                var st = Settlement.CurrentSettlement;
                if (r == null || st == null || st.StringId != r.TownId) return;
                long b = 0, s = 0;
                if (bought != null) foreach (var x in bought) { if (x.Item2 > 0) { _dBuyN += Math.Max(1, x.Item1.Amount); _dBuy += x.Item2; b += x.Item2; } }
                if (sold != null) foreach (var x in sold) { if (x.Item2 > 0) { _dSellN += Math.Max(1, x.Item1.Amount); _dSell += x.Item2; s += x.Item2; } }
                long gain = (long)Math.Round(b * PriceStep / (1.0 - PriceStep) + s * PriceStep / (1.0 + PriceStep));
                if (gain <= 0) return;
                var n = r.Copy(2);
                n.PriceGain = r.PriceGain + gain;
                if (n.PriceGain >= PriceGainCap)
                {
                    n.Price = false; _dPriceCut++;
                    Log.Player("The good house's name has opened all the doors it will in " + r.Town + " - from now on you trade at the usual prices.");
                    Log.Info(P + "premia cen w " + r.Town + " zdjeta " + When(NowMin()) + " - zysk z cen ok. " + n.PriceGain + " d (limit " + PriceGainCap + " = 5 x pokoj dobrego domu).");
                }
                _rest = n;
            }
            catch (Exception e) { Stumble("OnPlayerExchange", e); }
        }

        // ------------------------------------------------------------ zegar, doba, sesja
        /// <summary>Godzina gry: koniec premii o zmierzchu (komunikat), oplacony nocleg bez snu w menu dluzej niz 2 h - zdjety; godzina gojenia w dobrym domu.</summary>
        internal static void Hourly()
        {
            try
            {
                var r = _rest;
                if (r == null) return;
                long now = NowMin();
                if (r.Phase == 2 && now >= r.UntilMin)
                {
                    _rest = null;
                    if (On) Log.Player(r.HostUid != null ? (Mod(r.UntilMin, 1440) == DuskHour * 60 ? "Dusk - the night's rest " + HostWhere(r) + " wears off." : "The night's rest " + HostWhere(r) + " wears off.")
                                                          : Mod(r.UntilMin, 1440) == DuskHour * 60 ? "Dusk - the comfort of the inn wears off." : "The comfort of the inn wears off.");
                    Log.Info(P + "koniec premii o " + When(now) + " (wypoczeci od noclegu w " + r.Town + ").");
                }
                // recenzja 16.10: samo id menu snu (bez flagi NightRest - po wczytaniu w srodku snu gra odtwarza menu oczekiwania bez init, flaga jest wtedy false)
                else if (r.Phase == 1 && !SleepMenuOpen() && now - Math.Max(r.CommitMin, r.StartMin) > DropAwakeMin)
                {
                    _rest = null; _dDropped++;
                    Log.Info(P + "oplacony nocleg w " + r.Town + " bez snu w menu od " + When(Math.Max(r.CommitMin, r.StartMin)) + " - stan zdjety, bez premii.");
                }
                else if (r.Phase == 1 && r.Heal > 0f && r.StartMin > 0 && InSleepMenu() && On) _dHealH++;
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static string Keys()
        {
            var s = Settings.Current;
            return "klucze: Town Inns " + (s != null && s.TownInns ? "tak" : "WYLACZONE") + ", Town Inn Price Bonus " + (s != null && s.TownInnPriceBonus ? "tak" : "WYLACZONE")
                   + ", Night Rest " + (s != null && s.NightRestEnabled ? "tak" : "WYLACZONE");
        }

        private static string StateText()
        {
            var r = _rest;
            if (r == null) return "brak";
            if (r.Phase == 1) return (r.HostUid != null ? "nocleg we wlasnej wiosce bez oplaty (" : "sen oplacony (") + QLog[Math.Max(0, r.Q)] + ", " + r.Town + ", od " + When(Math.Max(r.CommitMin, r.StartMin)) + ")";
            return "wypoczeci do " + When(r.UntilMin) + " (predkosc +" + F1(r.Speed) + "%, morale +" + F1(r.Morale) + ", cena " + (r.Price && PriceKey ? "tak w " + r.Town : "nie")
                   + (r.PriceGain > 0 ? ", zysk z cen ok. " + r.PriceGain + " z " + PriceGainCap : "") + ")";
        }

        /// <summary>Recenzja 16.10: czasy z zapisu przy pierwszym uzyciu, gdy CampaignTime.Now jest pewny (start sesji) - bledne albo bardzo dalekie czasy nie trzymaja
        /// stanu bez konca: wypoczeci dalej niz 24 h - zdjete; pobudka dalej niz 12 h - bez czekania do switu; oplacony nocleg z czasem z przyszlosci - zdjety.</summary>
        private static string CheckTimes()
        {
            var r = _rest;
            if (r == null) return "";
            long now = NowMin();
            // recenzja calosci (scalenie6, f10-uklad): oplacony nocleg w zajezdzie, ktorego uid nie ma w pliku zajazdow tej sesji (uklad v7: 135 zajazdow, z dawnych 196 uid
            // zostaly 4) - dotad stan schodzil po cichu przy pobudce (InnSleep: brak zajazdu = daleko, linia tylko w logu); teraz od razu linia i komunikat gracza. Pieniadze
            // bez zmian: lozko zaplacone przy wejsciu, kiesy wsi, podatek panow i korona juz rozliczone. Plik bez zajazdow (brak, blad) albo zajazdy wylaczone - jak dotad.
            if (r.Phase == 1 && r.RoadUid != null && RoadsideInns.On)
            {
                RoadsideInns.EnsureLoaded();
                if (RoadsideInns.InnCount > 0 && RoadsideInns.Find(r.RoadUid) == null)
                {
                    _rest = null; _dNoRest++;
                    Log.Player("The roadside inn where you took lodgings is no longer by the road - the night's lodging is lost and nobody wakes rested.", true);
                    return " (zapis: nocleg w zajezdzie " + r.RoadUid + " - zajazdu nie ma w pliku zajazdow tej sesji (inny uklad wiosek), stan zdjety bez premii, komunikat gracza)";
                }
            }
            if (r.Phase == 2 && r.UntilMin - now > 24 * 60) { _rest = null; return " (zapis: wypoczeci do " + When(r.UntilMin) + " - dalej niz 24 h, stan zdjety)"; }
            if (r.Phase == 1 && Math.Max(r.CommitMin, r.StartMin) > now + 60) { _rest = null; return " (zapis: nocleg z " + When(Math.Max(r.CommitMin, r.StartMin)) + " - z przyszlosci, stan zdjety)"; }
            if (r.Phase == 1 && r.WakeMin > now + HoldMaxMin) { long w = r.WakeMin; r.WakeMin = 0; return " (zapis: pobudka " + When(w) + " - dalej niz 12 h, bez czekania do switu)"; }
            return "";
        }

        internal static void SessionStart()
        {
            try
            {
                string fix = CheckTimes();
                Log.Info(P + "start sesji | " + Keys() + " | latki: cena (ekran handlu) " + _priceHooks + "/2, gojenie " + _healHooks + "/2 | stan noclegu: " + StateText() + fix
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Linia doby (z OnDailyTick, za dobe od poprzedniej linii) - zawsze przy wlaczonym kluczu TownInns.</summary>
        internal static void Daily(int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TownInns) { DayReset(); return; }
                long est(long sum, float up) { return (long)Math.Round(sum * PriceStep / (double)up); }
                Log.Info(P + "dzien " + day + " | " + Keys()
                         + " | noclegi " + _dStays + ": osoby " + (_dPeople[0] + _dPeople[1] + _dPeople[2] + _dPeople[3]) + " (wspolna izba " + _dPeople[1] + ", porzadna karczma " + _dPeople[2]
                         + ", dobry dom " + _dPeople[3] + ", stajnia " + _dPeople[0] + "), konie bez jezdzca " + (_dHorses + _dArmHorses) + " (zbrojownia " + _dArmHorses + ")"
                         + " | zaplacone " + (_dMen + _dComp + _dPlayer) + " (ludzie " + _dMen + ", towarzysze " + _dComp + " (z sakiewki ludzi " + _dCompPurse + "), gracz " + _dPlayer
                         + ") -> kiesa ludu " + _dFolk + ", kasa miasta " + _dCoffer
                         + " | kontrola obiegu (prog 0): roznica " + _dDiff
                         + " | pobudki: wypoczeci " + _dWoke + ", bez premii " + _dNoRest + ", noclegi bez snu " + _dDropped
                         + " | stan noclegu: " + StateText()
                         + " | gojenie dobrego domu: godzin " + _dHealH
                         + " | cena dobrego domu: kupione " + _dBuyN + " szt. za " + _dBuy + " (taniej ok. " + est(_dBuy, 1f - PriceStep) + "), sprzedane " + _dSellN + " szt. za " + _dSell
                         + " (drozej ok. " + est(_dSell, 1f + PriceStep) + "), premia zdjeta po limicie " + _dPriceCut
                         + " | potkniecia dzis " + _stumblesDay + " (od wczytania " + _stumbles + "), w predkosci i morale dzis " + Volatile.Read(ref _parStumblesDay)
                         + " (od wczytania " + Volatile.Read(ref _parStumbles) + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayReset(); }
        }

        // ------------------------------------------------------------ zapis
        /// <summary>"v1;faza;q;qGracza;miasto;zaplata;start;pobudka;koniec;predkosc;morale;sen;gojenie;cena;zyskCen" (minuty kampanii, kultura niezmienna). Brak stanu = "".
        /// Pole 15 (zysk z premii cen, recenzja 16.10) opcjonalne - zapis z 14 polami czyta sie jak dotad (zysk 0).
        /// sw-zajazdy: pole 16 (uid zajazdu przy szlaku) tylko przy noclegu w zajezdzie - starsza DLL je pomija (czyta 15 pol; sen poza miastem = stan zdjety bez premii,
        /// wypoczeci - jak po karczmie); karczma miasta pisze 15 pol jak dotad.</summary>
        internal static string Export()
        {
            var r = _rest;
            if (r == null || r.Phase < 1) return "";
            var f = new List<string> { "v1", r.Phase.ToString(Inv), r.Q.ToString(Inv), r.PlayerQ.ToString(Inv), r.TownId ?? "", r.CommitMin.ToString(Inv), r.StartMin.ToString(Inv),
                                       r.WakeMin.ToString(Inv), r.UntilMin.ToString(Inv), r.Speed.ToString("R", Inv), r.Morale.ToString("R", Inv), r.Sleep.ToString("R", Inv),
                                       r.Heal.ToString("R", Inv), r.Price ? "1" : "0", r.PriceGain.ToString(Inv) };
            if (r.RoadUid != null || r.HostUid != null) f.Add(r.RoadUid ?? "");
            if (r.HostUid != null) f.Add(r.HostUid);   // n20-wioska: pole 17 - uid wlasnej wioski (starsza DLL czyta 15/16 pol: sen poza wsia gry okregu = stan zdjety bez premii)
            return string.Join(";", f.ToArray());
        }

        internal static void Import(string data)
        {
            _rest = null;
            if (string.IsNullOrEmpty(data)) return;
            if (data.Length > 4096) { Log.Info(P + "zapis arm_inns za dlugi (" + data.Length + " znakow) - pominiety."); return; }   // sw-zajazdy: straznik rozmiaru
            try
            {
                var a = data.Split(';');
                if (a.Length < 14 || a[0] != "v1") { Log.Info(P + "zapis arm_inns w nieznanym formacie - pominiety."); return; }
                var r = new Rest();
                r.Phase = int.Parse(a[1], Inv); r.Q = int.Parse(a[2], Inv); r.PlayerQ = int.Parse(a[3], Inv); r.TownId = a[4];
                r.CommitMin = long.Parse(a[5], Inv); r.StartMin = long.Parse(a[6], Inv); r.WakeMin = long.Parse(a[7], Inv); r.UntilMin = long.Parse(a[8], Inv);
                r.Speed = float.Parse(a[9], Inv); r.Morale = float.Parse(a[10], Inv); r.Sleep = float.Parse(a[11], Inv); r.Heal = float.Parse(a[12], Inv); r.Price = a[13] == "1";
                long pg;
                if (a.Length > 14 && long.TryParse(a[14], NumberStyles.Integer, Inv, out pg)) r.PriceGain = Math.Max(0, pg);
                // sw-zajazdy: pole 16 - uid zajazdu przy szlaku (straznik dlugosci; zly - stan zdjety); bez pola - karczma miasta jak dotad
                if (a.Length > 15 && a[15].Length > 0) { if (a[15].Length > 64) return; r.RoadUid = a[15]; r.Price = false; }
                // n20-wioska: pole 17 - uid wlasnej wioski pobocznej (nocleg bez oplaty); oba pola naraz - zly zapis, stan zdjety
                if (a.Length > 16 && a[16].Length > 0) { if (a[16].Length > 64 || r.RoadUid != null) return; r.HostUid = a[16]; r.Price = false; }
                if (r.Phase < 1 || r.Phase > 2 || r.Q < 0 || r.Q > 2) return;
                // czasy (UntilMin, WakeMin) sprawdza start sesji (CheckTimes) - tu CampaignTime.Now bywa jeszcze niepewny
                // premie przyciete do projektu (zly zapis nie robi z noclegu dopalacza)
                r.Speed = Math.Max(0f, Math.Min(QSpeed[2], r.Speed)); r.Morale = Math.Max(0f, Math.Min(QMorale[2], r.Morale));
                r.Sleep = Math.Max(1f, Math.Min(QSleep[2], r.Sleep)); r.Heal = Math.Max(0f, Math.Min(QHeal[2], r.Heal));
                _rest = r;   // nazwa miasta przy pierwszym uzyciu (Rest.Town) - bez szukania osady w trakcie wczytywania
            }
            catch (Exception e) { _rest = null; Stumble("Import", e); }
        }

        // ------------------------------------------------------------ latki (raz na proces, przy starcie gry - SubModuleMain)
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var inv = AccessTools.Method(typeof(InventoryLogic), "GetItemPrice", new[] { typeof(EquipmentElement), typeof(bool) });
                var mkt = AccessTools.Method(typeof(TownMarketData), "GetPrice", new[] { typeof(EquipmentElement), typeof(MobileParty), typeof(bool), typeof(PartyBase) });
                if (inv != null && mkt != null)
                {
                    if (h.Patch(inv, prefix: new HarmonyMethod(typeof(TownInns), nameof(InvPrefix)), finalizer: new HarmonyMethod(typeof(TownInns), nameof(InvFinalizer))) != null) _priceHooks++;
                    if (h.Patch(mkt, postfix: new HarmonyMethod(typeof(TownInns), nameof(MarketPostfix)) { priority = Priority.Last }) != null) _priceHooks++;
                }
            }
            catch (Exception e) { Stumble("ApplyAll(cena)", e); }
            try
            {
                var t = typeof(DefaultPartyHealingModel);
                var post = new HarmonyMethod(typeof(TownInns), nameof(HealPostfix)) { priority = Priority.VeryLow };   // po SlowHealing (Low) i BK; przed StarvePostfix (Last)
                var m1 = AccessTools.Method(t, "GetDailyHealingForRegulars");
                var m2 = AccessTools.Method(t, "GetDailyHealingHpForHeroes");
                if (m1 != null && h.Patch(m1, postfix: post) != null) _healHooks++;
                if (m2 != null && h.Patch(m2, postfix: post) != null) _healHooks++;
            }
            catch (Exception e) { Stumble("ApplyAll(gojenie)", e); }
            Log.Info(P + "latki przy starcie gry - cena (ekran handlu: InventoryLogic.GetItemPrice + TownMarketData.GetPrice) " + _priceHooks + "/2, gojenie (DefaultPartyHealingModel) "
                     + _healHooks + "/2.");
        }
    }
}
