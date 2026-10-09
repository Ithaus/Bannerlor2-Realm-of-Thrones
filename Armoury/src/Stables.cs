using System;
using System.Collections;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// KON DLA RYCERZA, wedle slow Jeffa: "jak awansuje na konnice, to musi
    /// byc kon - im wyzszy poziom, tym lepszy rumak - i AI tez, warunki
    /// globalne". DTE wycina vanilla wymog wierzchowca (kategoria = null),
    /// wiec konnica rodzila sie z powietrza. Przywracamy go i skalujemy:
    /// awans na jezdnego = kon w TABORZE druzyny, jeden na czlowieka,
    /// znika przy awansie. Tier niski - zwykly kon; wyzej - kon bojowy;
    /// najwyzej - rumak szlachetny. Gracza pilnuje ekran druzyny (vanilla
    /// sam sprawdza i zabiera konie), AI - nasze latki na awans w polu:
    /// lord bez koni w taborze nie wystawi jazdy (konie bierze z lupow).
    /// </summary>
    internal static class Stables
    {
        /// <summary>
        /// PODWOJNY RACHUNEK ZA JEDNEGO JEZDZCA (Jeff 13.09: "awansowalem piechote
        /// na jezdzca, kosztowalo mnie to konia, kon zniknal, a potem Armoury
        /// pokazuje, ze brakuje mi konia - czy nie jest to podwojne liczenie?").
        /// BYLO, i Jeff mial racje. Dwa mody mialy dwie filozofie:
        ///  - DTE kasuje vanillowy koszt awansu (getter UpgradeRequiresItemFromCategory
        ///    zwraca null), bo chce, zeby konie placilo sie WYLACZNIE ze zbrojowni;
        ///  - my ten koszt przywracamy (RanksNeedHorses), bo Jeff chcial, zeby jazda
        ///    nie rodzila sie z powietrza.
        /// Skutek: vanilla przy awansie kasowala konia z TABORU (PartyScreenLogic
        /// -> RemoveItemFromItemRoster robi AddToCounts(-n) i nic wiecej - kon
        /// przepada bez sladu), a zaraz potem swiezy jezdziec podnosil zapotrzebowanie
        /// zbrojowni DTE o kolejnego konia. Dwa rumaki na jednego czlowieka,
        /// z czego jeden spalony.
        /// TERAZ: konie zaplacone za awans GRACZA laduja w zbrojowni DTE - tej samej,
        /// z ktorej DTE sadza jezdzca na koniu w bitwie. Jeden kon, jedna zaplata.
        /// CZEMU W DoneLogic, A NIE PRZY SAMYM AWANSIE: tabor traci konia juz przy
        /// kliknieciu strzalki, ale ANULOWANIE ekranu go zwraca (PartyScreenData
        /// .ResetUsing odtwarza roster ze zrzutu). Ksiegowanie przy awansie bilooby
        /// konia z powietrza po kazdym Cancel. DoneLogic z wynikiem true to jedyny
        /// punkt, w ktorym zmiana jest naprawde zatwierdzona. Liste trzeba zlapac
        /// w PREFIXIE, bo DoneLogic czysci ja przed zwroceniem wartosci.
        /// </summary>
        public static void GrabPaidHorses(PartyScreenLogic __instance, ref object __state)
        {
            __state = null;
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) return;
                if (__instance == null || __instance.CurrentData == null) return;
                var hist = __instance.CurrentData.UsedUpgradeHorsesHistory;
                if (hist == null || hist.Count == 0) return;
                // kopia, nie referencja - oryginal zaraz zostanie podmieniony na pusty
                __state = new System.Collections.Generic.List<Tuple<EquipmentElement, int>>(hist);
            }
            catch { __state = null; }
        }

        /// <summary>Po ZATWIERDZONYM ekranie druzyny gracza: zaplacone konie ida na polke zbrojowni.</summary>
        public static void BankPaidHorses(PartyScreenLogic __instance, bool __result, object __state)
        {
            try
            {
                if (!__result) return;                                   // anulowane - tabor dostal konie z powrotem
                var paid = __state as System.Collections.Generic.List<Tuple<EquipmentElement, int>>;
                if (paid == null || paid.Count == 0) return;
                if (__instance == null || __instance.RightOwnerParty != PartyBase.MainParty) return;  // nie cudze garnizony
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;

                int moved = 0;
                foreach (var pair in paid)
                {
                    if (pair == null || pair.Item2 <= 0 || pair.Item1.Item == null) continue;
                    armory.AddToCounts(pair.Item1, pair.Item2);
                    moved += pair.Item2;
                }
                if (moved > 0)
                {
                    Log.Info("Stajnia: awans gracza - " + moved + " koni z taboru przeszlo do zbrojowni (zamiast przepasc).");
                    Log.Player(moved + (moved == 1 ? " horse goes" : " horses go") + " from the baggage to the troop armoury - the new riders will find them there.", true);
                }
            }
            catch (Exception e) { Log.Error("Stables.BankPaidHorses", e); }
        }

        /// <summary>
        /// Getter CharacterObject.UpgradeRequiresItemFromCategory: DTE zeruje go
        /// prefixem, my dopisujemy postfixem (postfix biegnie PO prefiksach),
        /// wiec ostatnie slowo nalezy do stajni.
        /// </summary>
        public static void RanksNeedHorses(CharacterObject __instance, ref ItemCategory __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) return;
                if (__instance == null || __instance.IsHero || !__instance.IsMounted) return;
                // juz siedzi w siodle - awans nie sadza go na koniu drugi raz
                if (RiderOnlyTarget(__instance)) { __result = null; return; }
                // getter nie zna partii - vanilla pyta o niego przy awansach
                // GRACZA (ekran druzyny), wiec heurystyka wg taboru gracza;
                // AI idzie przez RequiredMountFor (Filter/Pay), nie tedy
                __result = RequiredMountFor(TaleWorlds.CampaignSystem.Party.PartyBase.MainParty, __instance);
            }
            catch { }
        }

        // ---------------------------------------------------------- mapa jezdzcow
        // cel awansu -> czy KAZDE zrodlo prowadzace do niego jest juz konne
        private static System.Collections.Generic.Dictionary<CharacterObject, bool> _riderOnly;
        private static bool _riderMapBuilt;

        /// <summary>
        /// KON TYLKO WTEDY, GDY AWANS REALNIE SADZA W SIODLE (Jeff 14.09: "dlaczego
        /// musze ponownie tracic konia na kazdy awans kawalerii, przeciez on juz
        /// dostal konia, siedzi na bojowym koniu").
        /// Mial racje. Wymog wierzchowca to wlasciwosc CELU awansu
        /// (CharacterObject.UpgradeRequiresItemFromCategory) i ZADNE vanillowe miejsce,
        /// ktore ja czyta, nie zna ZRODLA: ani bramka w PartyScreenLogic.ValidateCommand
        /// (:666), ani zaplata w UpgradeTroop (:923-925), ani model
        /// DefaultPartyTroopUpgradeModel.DoesPartyHaveRequiredItemsForUpgrade (:107).
        /// Sam getter tez nie ma jak - to zwykla wlasciwosc instancji. Wiec nasz postfix
        /// RanksNeedHorses traktowal tak samo piechura wsiadajacego pierwszy raz na konia
        /// i rycerza, ktory juz od dawna siedzi w siodle. Kazdy awans w obrebie kawalerii
        /// kasowal kolejnego rumaka, a zapotrzebowanie zbrojowni DTE liczy sie PER JEZDZIEC
        /// i przy awansie konny->konny nie rosnie ani o sztuke. Czysty podwojny rachunek -
        /// ten sam blad co 13.09 z podwojnym koniem, tylko pietro wyzej.
        /// ROZWIAZANIE: raz, po wczytaniu kampanii, przechodzimy CALE drzewko awansow
        /// i dla kazdego celu zapamietujemy, czy WSZYSTKIE prowadzace do niego zrodla sa
        /// juz konne. Jesli tak - awans na ten cel nie potrzebuje rumaka.
        /// Cel o nieznanym zrodle (korzen linii) zostaje platny - ostroznie, nie hojnie.
        /// CZEMU MAPA, A NIE ODCZYT STANU TABORU: odpowiedz gettera dla danego celu MUSI
        /// byc STALA w obrebie sesji ekranu druzyny. UpgradeRequirementsVM.SetItemRequirement
        /// ma cale cialo pod "if (category != null)" i nie ma sciezki czyszczacej, a wolane
        /// jest DOKLADNIE RAZ, w konstruktorze UpgradeTargetVM - ikona i napis "Requirement"
        /// sa wiec ZATRZASKIWANE. Na dodatek latka ROT DisableUpgradeIfOnlyDragons gasi na
        /// ich podstawie strzalke awansu. Kategoria zmienna w trakcie ekranu = martwy przycisk.
        /// Mapa liczona z samego drzewka jest deterministyczna i nie zaglada do rostera.
        /// SWIADOMA CENA: cala linia werbowana od razu jako konna (np. casterly_squire)
        /// jest darmowa na calej dlugosci - i u gracza, i u AI. Ten czlowiek dostal konia
        /// przy werbunku, nie przy awansie, wiec zbrojownia juz go policzyla.
        /// </summary>
        internal static bool RiderOnlyTarget(CharacterObject target)
        {
            if (target == null) return false;
            if (!_riderMapBuilt) BuildRiderMap();
            var map = _riderOnly;
            bool v;
            return map != null && map.TryGetValue(target, out v) && v;
        }

        /// <summary>Jeden przelot po spisie jednostek; wolany po wczytaniu kampanii.</summary>
        internal static void BuildRiderMap()
        {
            _riderMapBuilt = true;          // ZAWSZE, takze gdy ponizej rzuci - inaczej
                                            // kazde wywolanie gettera bilo by pelny przelot
            var map = new System.Collections.Generic.Dictionary<CharacterObject, bool>();
            int troops = 0, freeTargets = 0;
            try
            {
                var all = CharacterObject.All;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        var src = all[i];
                        if (src == null || src.IsHero) continue;
                        var targets = src.UpgradeTargets;
                        if (targets == null || targets.Length == 0) continue;
                        troops++;
                        bool srcMounted = src.IsMounted;
                        for (int t = 0; t < targets.Length; t++)
                        {
                            var tg = targets[t];
                            if (tg == null) continue;
                            bool had;
                            map[tg] = map.TryGetValue(tg, out had) ? (had && srcMounted) : srcMounted;
                        }
                    }
                    foreach (var kv in map) if (kv.Value) freeTargets++;
                }
            }
            catch (Exception e) { Log.Error("Stables.BuildRiderMap", e); }
            _riderOnly = map;
            Log.Info("Stajnia: spis awansow zbudowany - " + troops + " oddzialow z awansem, "
                     + map.Count + " celow, w tym " + freeTargets
                     + " osiagalnych wylacznie z siodla (te nie kosztuja juz rumaka).");
        }

        /// <summary>
        /// NAJWYZSZY TIER: WAR LUB NOBLE (Jeff 30.08, screen "Required: Noble
        /// Mount (You have none)" przy 39 koniach): rycerstwo t6+ siada na
        /// szlachetnym rumaku, JESLI stajnia go ma - a gdy nie ma, wystarczy
        /// porzadny kon bojowy. Twarde "tylko noble" blokowalo awanse na
        /// ostatni tier calymi tygodniami.
        /// </summary>
        internal static ItemCategory RequiredMountFor(PartyBase party, CharacterObject target)
        {
            var c = Settings.Current;
            int tier = (int)target.Tier;
            // 175.1-wolne (AiFreeArmoryHorsesFirst): u AI szlachetny rumak moze tez lezec wolny w zbrojowni (Have liczy tabor + wolne tej kategorii)
            if (tier >= c.NobleHorseFromTier)
                return (party != null && Have(party, DefaultItemCategories.NobleHorse) > 0)
                    ? DefaultItemCategories.NobleHorse : DefaultItemCategories.WarHorse;
            if (tier >= c.WarHorseFromTier) return DefaultItemCategories.WarHorse;
            return DefaultItemCategories.Horse;
        }

        // ------------------------------------------------------------ 175.1: zbrojownia AI (decyzja Jeffa 09.10 pkt 2)
        /// <summary>
        /// KON ZA AWANS AI TRAFIA DO ZBROJOWNI NOWEGO JEZDZCA, JAK U GRACZA (Jeff 09.10: "naprawic: kon, ktory lord AI oddaje
        /// za awans, ma trafic do zbrojowni nowego jezdzca, nie znikac"). Dotad PayInHorses zdejmowal konia z taboru i na tym
        /// koniec (AddToCounts(-n)) - kon znikal, a u gracza ten sam kon idzie do zbrojowni DTE (BankPaidHorses). Teraz u AI
        /// (lordowie, partie towarzyszy, zalogi - wszystko, co awansuje przez PartyUpgraderCampaignBehavior) kon idzie do
        /// zbrojowni DTE partii (AiGear.AddToArmory). Jedna zasada: kon musi istniec w partii (30.08). Brak DTE / zbrojowni -> kon
        /// schodzi z taboru jak dotad (licznik "przepadlo"), nigdy nie wraca do taboru (awans bylby darmowy). Wylacznik
        /// AiUpgradeHorseToArmory (dom. TAK).
        /// WOLNE KONIE NAJPIERW to OSOBNY wylacznik AiFreeArmoryHorsesFirst (dom. NIE, przeglad 175): wolne = konie danej kategorii
        /// w zbrojowni ponad jezdzcow, ktorym ta kategoria przysluguje (Balance). W tej bazie (bez 171) wolnym staje sie tez kon
        /// jezdzca zostawionego zywym w zalodze (jezdziec dalej jezdzi w zalodze, kon zostaje u lorda), kon dezertera (wedlug 160
        /// kon jest wlasnoscia zolnierza) i kon z kompletu wzorca, ktory RecruitKit daje zastepcy przy echu werbunku ROT (kon
        /// z niczego) - jeden kon obslugiwalby dwoch jezdzcow. Do scalenia 171 (MoveKits przenosi konie z ludzmi, echo ROT bez
        /// kompletu) i do zabrania konia przez dezertera - wylaczone; HorseCensus mierzy te przecieki (wolne_po_*, konie_z_echa_rot).
        /// </summary>
        internal static bool AiFix(PartyBase party)
        {
            var s = Settings.Current;
            // sklad9 (scalenie 175 + sklad7b-p): jedna droga konia za awans AI do zbrojowni. Przy TroopsFightWithOwnKitOnly (sklad7b-p, w grze)
            // kon MUSI byc w zbrojowni (jezdziec bez konia walczy pieszo), wiec tam AiUpgradeHorseToArmory nie moze go wylaczyc.
            return s != null && (s.AiUpgradeHorseToArmory || GarrisonKit.OwnKitOn) && party != null && party != PartyBase.MainParty && party.MobileParty != null;
        }

        /// <summary>175.1-wolne: czy partia AI bierze najpierw wolne konie zbrojowni (awans, lista celow, zakupy, straz konia).</summary>
        internal static bool FreeFirst(PartyBase party)
        {
            var s = Settings.Current;
            return s != null && s.AiFreeArmoryHorsesFirst && party != null && party != PartyBase.MainParty && party.MobileParty != null;
        }

        /// <summary>Kategoria konia jako stopien: 0 zwykly, 1 bojowy, 2 szlachetny, -1 inna.</summary>
        internal static int Level(ItemCategory cat)
        {
            if (cat == null) return -1;
            if (cat == DefaultItemCategories.NobleHorse) return 2;
            if (cat == DefaultItemCategories.WarHorse) return 1;
            if (cat == DefaultItemCategories.Horse) return 0;
            return -1;
        }

        internal static ItemCategory CatOfLevel(int level)
        {
            return level == 2 ? DefaultItemCategories.NobleHorse : level == 1 ? DefaultItemCategories.WarHorse : DefaultItemCategories.Horse;
        }

        /// <summary>Konie pod siodlo (IsPlainMount) w zbrojowni DTE partii; cat != null - tylko tej kategorii.</summary>
        internal static int ArmoryMounts(MobileParty mp, ItemCategory cat = null)
        {
            int n = 0;
            try
            {
                var all = AiGear.Armories();
                System.Collections.Generic.Dictionary<ItemObject, int> arm;
                if (all == null || mp == null || !all.TryGetValue(mp.Id, out arm) || arm == null) return 0;
                foreach (var kv in arm)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    if (cat != null && kv.Key.ItemCategory != cat) continue;
                    if (!IsPlainMount(kv.Key)) continue;
                    n += kv.Value;
                }
            }
            catch { }
            return n;
        }

        /// <summary>Rachunek koni zbrojowni wobec jezdzcow partii, wedlug kategorii (Balance).</summary>
        internal sealed class ArmoryBalance
        {
            public readonly int[] Horses = new int[3];   // konie pod siodlo w zbrojowni: zwykle, bojowe, szlachetne
            public readonly int[] Free = new int[3];     // wolne po przydziale jezdzcom
            public int Riders, RidersHigh, Unhorsed;     // konni szeregowi, w tym z prawem do bojowego (tier >= WarHorseFromTier), bez zadnego konia
            public int HorsesAll { get { return Horses[0] + Horses[1] + Horses[2]; } }
            public int FreeAll { get { return Free[0] + Free[1] + Free[2]; } }
        }

        /// <summary>
        /// WOLNE KONIE WEDLUG KATEGORII (przeglad 175: "wolne dla kategorii = min(wolne wszystkich, konie tej kategorii)" liczylo jako
        /// wolne konie bojowe, pod ktorymi siedza obecni rycerze, gdy wolne byly tylko zwykle - awans t4+ przechodzil "z koniem bojowym",
        /// ktorego nie ma, wbrew 30.08). Przydzial jak DTE sadza ludzi: jezdzcy z prawem do bojowego (tier >= WarHorseFromTier; t6
        /// "szlachetny albo bojowy") biora bojowe, potem szlachetne; pozostali konni - zwykle, potem bojowe, potem szlachetne (lepszy kon
        /// zastapi gorszy, nie odwrotnie); rycerz bez bojowego i szlachetnego siada na zwyklym, ktory zostal (DTE da mu, co jest).
        /// Wolne = co zostaje w danej kategorii. adjust = poprawka liczby konnych wedlug typu (ujemna: jezdzcy, ktorzy wlasnie weszli
        /// i nie maja jeszcze konia - straz konia ROT; dodatnia: jezdzcy, ktorzy wlasnie odeszli - spis koni uwolnionych).
        /// </summary>
        internal static ArmoryBalance Balance(MobileParty mp, System.Collections.Generic.Dictionary<CharacterObject, int> adjust = null)
        {
            var b = new ArmoryBalance();
            if (mp == null) return b;
            try
            {
                var all = AiGear.Armories();
                System.Collections.Generic.Dictionary<ItemObject, int> arm;
                if (all != null && all.TryGetValue(mp.Id, out arm) && arm != null)
                    foreach (var kv in arm)
                    {
                        if (kv.Key == null || kv.Value <= 0 || !IsPlainMount(kv.Key)) continue;
                        int lv = Level(kv.Key.ItemCategory);
                        if (lv >= 0) b.Horses[lv] += kv.Value;
                    }
                var c = Settings.Current;
                int highFrom = c != null ? Math.Min(c.WarHorseFromTier, c.NobleHorseFromTier) : 4;
                var men = new System.Collections.Generic.Dictionary<CharacterObject, int>();
                var r = mp.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0 || !ch.IsMounted) continue;
                    int n0; men.TryGetValue(ch, out n0); men[ch] = n0 + el.Number;
                }
                if (adjust != null)
                    foreach (var kv in adjust)
                    {
                        if (kv.Key == null || kv.Key.IsHero || !kv.Key.IsMounted) continue;
                        int n0; men.TryGetValue(kv.Key, out n0); men[kv.Key] = n0 + kv.Value;
                    }
                int low = 0, high = 0;
                foreach (var kv in men)
                {
                    if (kv.Value <= 0) continue;
                    if (kv.Key.Tier >= highFrom) high += kv.Value; else low += kv.Value;
                }
                b.Riders = low + high; b.RidersHigh = high;
                var h = new int[3]; Array.Copy(b.Horses, h, 3);
                int rh = high, rl = low, t;
                t = Math.Min(rh, h[1]); h[1] -= t; rh -= t;               // rycerz: bojowy
                t = Math.Min(rh, h[2]); h[2] -= t; rh -= t;               //         albo szlachetny
                for (int lv = 0; lv < 3; lv++) { t = Math.Min(rl, h[lv]); h[lv] -= t; rl -= t; }   // reszta: zwykly, bojowy, szlachetny
                t = Math.Min(rh, h[0]); h[0] -= t; rh -= t;               // rycerz bez bojowego siada na zwyklym
                b.Unhorsed = rh + rl;
                Array.Copy(h, b.Free, 3);
            }
            catch { }
            return b;
        }

        /// <summary>Wolne konie zbrojowni danej kategorii (Balance; cat == null - wszystkie). adjust - patrz Balance.</summary>
        internal static int FreeArmory(MobileParty mp, ItemCategory cat, System.Collections.Generic.Dictionary<CharacterObject, int> adjust = null)
        {
            if (mp == null) return 0;
            var b = Balance(mp, adjust);
            if (cat == null) return b.FreeAll;
            int lv = Level(cat);
            return lv >= 0 ? b.Free[lv] : 0;
        }

        /// <summary>Konie kategorii, ktore partia ma do awansu: tabor, a u AI z AiFreeArmoryHorsesFirst takze wolne tej kategorii w zbrojowni.</summary>
        internal static int Have(PartyBase party, ItemCategory cat)
        {
            int n = CountInRoster(party, cat);
            if (FreeFirst(party)) n += FreeArmory(party.MobileParty, cat);
            return n;
        }

        /// <summary>Konie kategorii w taborze. Przeglad 175: przy AiUpgradeHorseToArmory (AiFix) tylko wierzchowce pod siodlo (IsPlainMount) -
        /// ROT daje item_category horse/war_horse/noble_horse sloniom, mamutom, rydwanom, wielbladom i smokom; Consume zdejmowalby je do
        /// zbrojowni AI, z ktorej DTE wydaje wierzchowce w bitwach z graczem (i ArmoryMounts ich nie liczy). Do 175 taki wierzchowiec znikal -
        /// przy wylaczniku NIE jak dotad. Gracz - jak dotad (ekran druzyny vanilla placi czymkolwiek tej kategorii).</summary>
        internal static int CountInRoster(PartyBase party, ItemCategory cat)
        {
            int n = 0;
            try
            {
                bool plain = AiFix(party);
                var r = party.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r[i];
                    var it = el.EquipmentElement.Item;
                    if (it != null && it.ItemCategory == cat && (!plain || IsPlainMount(it))) n += el.Amount;
                }
            }
            catch { }
            return n;
        }

        /// <summary>Najtansze konie ida pod siodlo pierwsze - lepsze rumaki czekaja na wyzsze awanse.
        /// 175.1: taken != null - dopisuje, co zdjal z taboru (z modyfikatorem), zeby kon trafil do zbrojowni.
        /// Przeglad 175: przy AiFix tylko IsPlainMount (jak CountInRoster).</summary>
        internal static void Consume(PartyBase party, ItemCategory cat, int count,
                                     System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<EquipmentElement, int>> taken = null)
        {
            try
            {
                bool plain = AiFix(party);
                var r = party.ItemRoster;
                while (count > 0)
                {
                    int best = -1; int bestVal = int.MaxValue;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.ItemCategory != cat || el.Amount <= 0) continue;
                        if (plain && !IsPlainMount(it)) continue;
                        if (it.Value < bestVal) { bestVal = it.Value; best = i; }
                    }
                    if (best < 0) return;
                    var elBest = r[best];
                    int take = Math.Min(count, elBest.Amount);
                    var ee = elBest.EquipmentElement;
                    r.AddToCounts(ee, -take);
                    if (taken != null) taken.Add(new System.Collections.Generic.KeyValuePair<EquipmentElement, int>(ee, take));
                    count -= take;
                }
            }
            catch { }
        }

        // ------------------------------------------------------------ sklad7b-p: kon AI jako sztuka zbrojowni (jak u gracza)
        // Przeglad sklad7b (uwagi 3, 11, 15; Jeff 09.10 07:35 wariant c: "walcza tylko tym, co maja" - bez pozyczki na bitwe): kon jezdzca jest sztuka
        // zbrojowni DTE jego partii, jedna regula z graczem (BankPaidHorses). Dotad kon zaplacony przy awansie AI znikal z taboru w nicosc, a w bitwie
        // jezdziec AI dostawal konia ze wzorca (sklad7b: "pozyczonego" - wiecznego, nigdy w lupie). Teraz: (1) kon za awans AI -> zbrojownia partii;
        // (2) Remount - jezdzcy bez konia w zbrojowni dostaja konie z taboru lorda, nadmiar koni w zbrojowni (polegli jezdzcy) wraca do taboru
        // (na awanse); (3) Stajnia AI dokupuje konie takze dla jezdzcow bez konia (nie tylko na awanse). Tylko przy TroopsFightWithOwnKitOnly.
        // sklad9: (1) robi jedna droga 175.1 - PayInHorses -> Consume -> BankToArmory (AiFix = AiUpgradeHorseToArmory albo TroopsFightWithOwnKitOnly);
        // dawne BankAiHorses (sklad7b-p) usuniete, jego regula odmowy DTE (kon wraca do taboru) i liczniki tej linii przeszly do BankToArmory.
        // (2) i (3) - tylko przy TroopsFightWithOwnKitOnly, jak w grze.
        private static int _dStamp = -1, _dPaid, _dPaidRefused, _dToArm, _dToBag, _dNoHorse;

        private static void DayFlush()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _dStamp) return;
            if (_dStamp >= 0 && _dPaid + _dPaidRefused + _dToArm + _dToBag + _dNoHorse > 0)
                Log.Info("Stajnia AI (sklad7b-p): dzien " + _dStamp + " - konie za awans do zbrojowni partii " + _dPaid + (_dPaidRefused > 0 ? " (DTE nie przyjal " + _dPaidRefused + " - wrocily do taboru)" : "")
                         + ", z taboru pod jezdzcow bez konia " + _dToArm + ", nadmiar ze zbrojowni do taboru " + _dToBag + "; jezdzcy bez konia po dosadzeniu (suma przy postojach) " + _dNoHorse + ".");
            _dPaid = _dPaidRefused = _dToArm = _dToBag = _dNoHorse = 0;
            _dStamp = d;
        }

        /// <summary>sklad7b-p: ilu jezdzcow (nie-bohaterowie, ktorych wzorzec ma zwyklego wierzchowca - IsPlainMount; takze ranni) ma partia.
        /// Wielblady, slonie i smoki wzorca - poza Stajnia (MountLaw), jak w zakupach.</summary>
        private static int Riders(MobileParty mp)
        {
            int n = 0;
            var r = mp.MemberRoster;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                bool mounted = false;
                try { var eq = ch.Equipment; mounted = eq != null && IsPlainMount(eq[EquipmentIndex.Horse].Item); } catch { }
                if (mounted) n += el.Number;
            }
            return n;
        }

        /// <summary>sklad7b-p: jezdzcy bez konia w zbrojowni DTE partii lorda AI dostaja konie z jej taboru (najtansze najpierw - lepsze czekaja na
        /// awanse); nadmiar koni w zbrojowni (polegli jezdzcy) wraca do taboru, skad placi sie konmi za awanse. Wierzchowiec jak w zbrojowni gracza
        /// (IsPlainMount - QuartermasterLaw.CountsAsKit). Zwraca, ilu jezdzcom dalej brakuje konia (Stajnia dokupi). Tylko przy
        /// TroopsFightWithOwnKitOnly; partie lordow z wodzem (takze Twojego rodu), nie gracz, nie umarli.</summary>
        internal static int Remount(MobileParty mp)
        {
            try
            {
                if (!GarrisonKit.OwnKitOn || mp == null || mp.IsMainParty || !mp.IsLordParty || mp.LeaderHero == null || mp.MapEvent != null || Undead.Party(mp)) return 0;
                var all = AiGear.Armories();
                if (all == null) return 0;
                DayFlush();
                System.Collections.Generic.Dictionary<ItemObject, int> arm; all.TryGetValue(mp.Id, out arm);
                int have = 0;
                if (arm != null) foreach (var kv in arm) if (kv.Value > 0 && IsPlainMount(kv.Key)) have += kv.Value;
                int riders = Riders(mp);
                var bag = mp.ItemRoster;
                if (bag == null) return Math.Max(0, riders - have);
                if (have > riders && arm != null)
                {
                    // nadmiar - najtansze do taboru (lepsze zostaja pod jezdzcami)
                    int extra = have - riders;
                    var keys = new System.Collections.Generic.List<ItemObject>();
                    foreach (var kv in arm) if (kv.Value > 0 && IsPlainMount(kv.Key)) keys.Add(kv.Key);
                    keys.Sort((a, b) => a.Value.CompareTo(b.Value));
                    foreach (var it in keys)
                    {
                        if (extra <= 0) break;
                        int c; if (!arm.TryGetValue(it, out c) || c <= 0) continue;
                        int k = Math.Min(extra, c);
                        if (c - k > 0) arm[it] = c - k; else arm.Remove(it);
                        bag.AddToCounts(new EquipmentElement(it), k);
                        extra -= k; _dToBag += k;
                    }
                    return 0;
                }
                int lack = riders - have;
                while (lack > 0)
                {
                    int best = -1, bestVal = int.MaxValue;
                    for (int i = 0; i < bag.Count; i++)
                    {
                        var el = bag[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !IsPlainMount(it)) continue;
                        if (it.Value < bestVal) { bestVal = it.Value; best = i; }
                    }
                    if (best < 0) break;
                    var e = bag[best];
                    int take = Math.Min(lack, e.Amount);
                    if (!AiGear.AddToArmory(mp, e.EquipmentElement.Item, take)) break;   // DTE nie przyjal - zostaje w taborze
                    bag.AddToCounts(e.EquipmentElement, -take);
                    lack -= take; _dToArm += take;
                }
                _dNoHorse += Math.Max(0, lack);
                return Math.Max(0, lack);
            }
            catch (Exception e) { Log.Error("Stables.Remount", e); return 0; }
        }

        /// <summary>
        /// 175.1 + sklad7b-p (jedna droga od scalenia sklad9): konie zdjete z taboru AI ida do zbrojowni DTE partii (jak u gracza). Zwraca, ile
        /// weszlo. bank (AiFix) false - kon przepada jak przed 175 (lost). Odmowa DTE przy bank true - kon WRACA DO TABORU (regula sklad7b-p,
        /// w grze: nic w nicosc; po filtrze IsPlainMount w Consume odmowa zostaje praktycznie tylko przy braku DTE) - licznik "DTE nie przyjal"
        /// w linii "Stajnia AI (sklad7b-p)". modNeg / modPos - konie z modyfikatorem (kulawy, stary / rasowy): zbrojownia AI trzyma ItemObject
        /// bez modyfikatora (znane odstepstwo od gracza, ktory zachowuje EquipmentElement).
        /// </summary>
        internal static int BankToArmory(MobileParty mp, System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<EquipmentElement, int>> taken,
                                         bool bank, out int lost, out int modNeg, out int modPos)
        {
            int banked = 0; lost = 0; modNeg = 0; modPos = 0;
            if (taken == null) return 0;
            if (bank) DayFlush();
            foreach (var kv in taken)
            {
                if (kv.Value <= 0 || kv.Key.Item == null) continue;
                if (bank && mp != null && AiGear.AddToArmory(mp, kv.Key.Item, kv.Value))
                {
                    banked += kv.Value; _dPaid += kv.Value;
                    var mod = kv.Key.ItemModifier;   // tylko konie, ktore naprawde weszly do zbrojowni (tam bez modyfikatora)
                    if (mod != null) { if (mod.PriceMultiplier < 1f) modNeg += kv.Value; else if (mod.PriceMultiplier > 1f) modPos += kv.Value; }
                }
                else if (bank && mp != null && mp.ItemRoster != null) { mp.ItemRoster.AddToCounts(kv.Key, kv.Value); _dPaidRefused += kv.Value; }
                else lost += kv.Value;
            }
            if (banked > 0) HorseCensus.OnBanked(mp, banked);   // przeglad 175: ile weszlo do zbrojowni, ktorej DTE nie zapisuje / kasuje co dobe
            return banked;
        }

        /// <summary>
        /// Czy DTE trzyma zbrojownie tej partii trwale - ta sama regula co DTE EveryoneCampaignBehavior.ShouldPersistParty i
        /// GarbageCollectParties (dekompilacja): wodz-bohater (nie gracz) zywy, czynny i wodzem partii, wlasciciel (nie gracz) zywy
        /// i czynny, w partii sa bohaterowie i ludzie, partia sie nie rozwiazuje. Partie bez wodza-bohatera (karawany bez wodza,
        /// tabory wsi, bandy, milicje, patrole) DTE kasuje co dobe; zalogi chroni AiGear.KeepGarrisonArmory, ale DTE ich nie zapisuje
        /// (bez 171 znikaja przy wczytaniu). Tylko do licznika "do zbrojowni nietrwalej" (przeglad 175).
        /// </summary>
        internal static bool PersistentArmory(MobileParty mp)
        {
            try
            {
                if (mp == null) return false;
                var lh = mp.LeaderHero;
                if (lh == null || lh.CharacterObject == null || lh.CharacterObject.IsPlayerCharacter || lh.IsHumanPlayerCharacter
                    || !lh.IsPartyLeader || !lh.IsAlive || !lh.IsActive) return false;
                var ow = mp.Owner;
                if (ow == null || ow.CharacterObject == null || ow.CharacterObject.IsPlayerCharacter || ow.IsHumanPlayerCharacter
                    || !ow.IsActive || !ow.IsAlive) return false;
                var r = mp.MemberRoster;
                return r != null && r.TotalHeroes > 0 && r.TotalManCount > 0 && !mp.IsDisbanding;
            }
            catch { return false; }
        }

        /// <summary>
        /// AI, krok 1 (lista celow awansu): cel na jezdnego bez ani jednego
        /// wlasciwego konia w taborze ODPADA (oddzial z rozwidleniem drzewka
        /// naturalnie skreca w piechote); przy garstce koni liczba awansow
        /// zostaje przycieta do stanu stajni.
        /// </summary>
        public static void FilterTargets(PartyBase party, object __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) return;
                var list = __result as IList;
                if (list == null || list.Count == 0 || party == null) return;
                int refused = 0, trimmed = 0;   // 175.0: pomiar (HorseCensus) - proby odrzucone i przyciete z braku konia
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var boxed = list[i];
                    if (boxed == null) continue;
                    var tr = Traverse.Create(boxed);
                    var target = tr.Field("UpgradeTarget").GetValue<CharacterObject>();
                    if (target == null) continue;
                    // zrodlo juz konne = awans niczego nie sadza w siodle, wiec za darmo
                    var srcT = tr.Field("Target").GetValue<CharacterObject>();
                    if (srcT != null && !srcT.IsHero && srcT.IsMounted) continue;
                    ItemCategory cat = null;
                    // wprost po TABORZE TEJ partii (getter mierzy stajnia gracza); 175.1-wolne: u AI tabor + wolne konie zbrojowni tej kategorii
                    try { cat = target.IsMounted && !target.IsHero ? RequiredMountFor(party, target) : target.UpgradeRequiresItemFromCategory; } catch { }
                    if (cat == null) continue;
                    int have = Have(party, cat);
                    int need = tr.Field("PossibleUpgradeCount").GetValue<int>();
                    if (have <= 0) { refused += Math.Max(0, need); list.RemoveAt(i); continue; }
                    if (need <= have) continue;
                    // stajnia na czesc awansow: przytnij liczbe do stanu koni
                    var ctor = boxed.GetType().GetConstructor(new[]
                    {
                        typeof(CharacterObject), typeof(CharacterObject),
                        typeof(int), typeof(int), typeof(int), typeof(float)
                    });
                    if (ctor == null) continue;
                    trimmed += need - have;
                    list[i] = ctor.Invoke(new object[]
                    {
                        tr.Field("Target").GetValue<CharacterObject>(), target, have,
                        tr.Field("UpgradeGoldCost").GetValue<int>(),
                        tr.Field("UpgradeXpCost").GetValue<int>(),
                        tr.Field("UpgradeChance").GetValue<float>()
                    });
                }
                // 175.0: proba odrzucona = "skret w inna droge", gdy lista celow zostala niepusta, inaczej "czeka"
                if (refused + trimmed > 0) HorseCensus.OnFiltered(party, refused, list.Count == 0, trimmed);
            }
            catch (Exception e) { Log.Error("Stables.Filter", e); }
        }

        /// <summary>
        /// AI, krok 2 (sam awans): kon za czlowieka. Gdyby w miedzyczasie stajnia
        /// oprozniala (inny oddzial tej samej druzyny zdazyl wybrac konie) - awans
        /// w ogole nie zachodzi.
        /// 175.1 (AiUpgradeHorseToArmory): kon z taboru idzie do zbrojowni partii (AiGear.AddToArmory), nie znika; wylacznik
        /// wylaczony - jak dotad (kon przepada). 175.1-wolne (AiFreeArmoryHorsesFirst, dom. NIE): najpierw wolne konie zbrojowni
        /// tej kategorii (kon zostaje na miejscu - nic sie nie przesuwa, nowy jezdziec po prostu go ma), potem tabor.
        /// </summary>
        public static bool PayInHorses(PartyBase party, object upgradeArgs)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) return true;
                if (party == null || upgradeArgs == null) return true;
                var tr = Traverse.Create(upgradeArgs);
                var target = tr.Field("UpgradeTarget").GetValue<CharacterObject>();
                if (target == null) return true;
                // zrodlo juz konne - patrz RiderOnlyTarget; awans bez kosztu rumaka
                var srcP = tr.Field("Target").GetValue<CharacterObject>();
                if (srcP != null && !srcP.IsHero && srcP.IsMounted) return true;
                ItemCategory cat = null;
                // wprost po TABORZE TEJ partii (getter mierzy stajnia gracza)
                try { cat = target.IsMounted && !target.IsHero ? RequiredMountFor(party, target) : target.UpgradeRequiresItemFromCategory; } catch { }
                if (cat == null) return true;
                int need = tr.Field("PossibleUpgradeCount").GetValue<int>();
                if (need <= 0) return true;
                bool fix = AiFix(party);
                int free = FreeFirst(party) ? FreeArmory(party.MobileParty, cat) : 0;
                int inRoster = CountInRoster(party, cat);
                if (inRoster + free < need) { HorseCensus.OnPayRefused(party, need); return false; }   // konie wybrane - czekaja (XP zostaje)
                int fromFree = Math.Min(need, free);
                int fromRoster = need - fromFree;
                var taken = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<EquipmentElement, int>>();
                if (fromRoster > 0) Consume(party, cat, fromRoster, taken);
                int lost, modNeg, modPos;
                int banked = BankToArmory(party.MobileParty, taken, fix, out lost, out modNeg, out modPos);
                HorseCensus.OnUpgradePaid(party, cat, need, fromFree, banked, lost, modNeg, modPos);
                return true;
            }
            catch (Exception e) { Log.Error("Stables.Pay", e); return true; }
        }

        // ------------------------------------------------------------ stajnia AI
        /// <summary>
        /// LORD KUPUJE KONIE, wedle slow Jeffa: "nie no maja miec konie, czemu
        /// mieliby nie miec, i tez moga kupowac". Sama zasada "awans na jezdnego
        /// wymaga rumaka" powoli rozbroilaby konnice AI - bo lord bral konie
        /// wylacznie z lupow. Odtad kazdy lord, ktory wjezdza do osady, uzupelnia
        /// stajnie: najpierw z tamtejszego targu (uczciwy handel - konie schodza
        /// z rynku, zloto idzie do miasta), a gdy na targu pusto, zamawia je
        /// u miejscowego hodowcy z narzutem. Placi z wlasnej sakiewki i nigdy
        /// nie wydaje na to wiecej niz czesc majatku.
        /// </summary>
        // kiedy ktora druzyna ostatnio uzupelniala stajnie (dzien kampanii)
        private static readonly System.Collections.Generic.Dictionary<string, double> _lastBuy =
            new System.Collections.Generic.Dictionary<string, double>();

        internal static void OnSettlementEntered(MobileParty party, TaleWorlds.CampaignSystem.Settlements.Settlement settlement)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || party == null || settlement == null || party.IsMainParty) return;
                if (!party.IsLordParty || party.LeaderHero == null) return;
                // sklad7b-p: kon jest sztuka zbrojowni - przy kazdym postoju lord dosadza jezdzcow bez konia koniami z taboru (nadmiar do taboru);
                // ilu dalej brakuje konia - Stajnia dokupi razem z konmi na awanse
                int lack = Remount(party);
                if (!c.CavalryNeedsMounts || !c.AiBuysMounts) return;
                if (!settlement.IsTown && !settlement.IsVillage) return;

                // KONIOKRADZTWO GOSPODARCZE: bez przerwy miedzy zakupami powstawala
                // pompa - lord kupowal konie, AI sprzedawalo je jako zwykly towar
                // przy nastepnym postoju, lord kupowal znowu. W jeden dzien gry
                // przez ten obieg przeplynely miliony. Stajnie uzupelnia sie
                // najwyzej raz na kilka dni i najwyzej o garstke sztuk naraz.
                double today = CampaignTime.Now.ToDays;
                double last;
                string pid = party.StringId ?? "";
                if (_lastBuy.TryGetValue(pid, out last)
                    && today - last < Math.Max(0f, c.AiMountBuyCooldownDays)) return;

                var lord = party.LeaderHero;

                // PO CO TE KONIE? Wylacznie po to, zeby awansowac ludzi, ktorzy
                // WLASNIE czekaja na awans na jezdnego - z wysluzonym
                // doswiadczeniem i bez rumaka w taborze. Pierwsza wersja kupowala
                // procent STANU OSOBOWEGO, wiec lord z pieciuset piechurami bral
                // sto koni "na zapas", odsprzedawal je jako zwykly towar
                // i kupowal znowu. Teraz liczymy glowy, nie procenty: nikt nie
                // czeka na awans - nikt nie kupuje ani jednego konia.
                // PRZEGLAD 175: zakup zostaje jak przed 175 (potrzeba NeedForUpgrades, lord kupuje najtansze konie, tabor liczony bez
                // kategorii - kupowanie wedlug kategorii to osobna decyzja po pomiarze), ale wolne konie zbrojowni (175.1-wolne) licza sie
                // tylko w KATEGORII, ktorej ktos potrzebuje (NeedByCategory: t2-t3 zwykly, t4-t5 bojowy, t6 szlachetny albo bojowy) -
                // inaczej zwykle konie po poleglych t2-t3 blokowalyby zakupy na zawsze, a awanse t4+ odpadalyby w FilterTargets co dobe.
                // Wizyta, w ktorej brakuje koni wlasciwej kategorii, a lord nie kupuje, bo tabor ma dosc koni INNEJ kategorii, idzie do
                // pomiaru (blad sprzed 175 widoczny przed decyzja 4.3).
                int want = NeedForUpgrades(party.Party) + lack;   // sklad7b-p: + jezdzcy bez konia w zbrojowni
                if (want <= 0) return;
                HorseCensus.OnLordVisit(party);                  // 175.0: wizyta z potrzeba (pomiar)
                var needK = NeedByCategory(party.Party);
                bool ff = FreeFirst(party.Party);
                var bal = ff ? Balance(party) : null;
                int deficit = 0, freeNeeded = 0;
                for (int k = 0; k < 3; k++)
                {
                    if (needK[k] <= 0) continue;
                    int hk = CountInRoster(party.Party, CatOfLevel(k)) + (bal != null ? bal.Free[k] : 0);
                    deficit += Math.Max(0, needK[k] - hk);
                    if (bal != null) freeNeeded += bal.Free[k];
                }
                want += Math.Max(0, c.AiMountSpareBuffer);      // kilka luzem na straty
                int have = CountAnyMounts(party.Party) + freeNeeded;
                int need = want - have;
                if (need <= 0) { if (deficit > 0) HorseCensus.OnLordSkipOtherCategory(party); return; }
                int cap = Math.Max(1, c.AiMountMaxPerVisit);
                if (need > cap) need = cap;              // jeden postoj to kilka koni, nie stado

                int purse = lord.Gold;
                int budget = (int)(purse * MBMath.ClampFloat(c.AiMountPurseShare, 0f, 1f));
                if (budget < 50) { HorseCensus.OnLordBuyFailed(party, HorseCensus.FailNoGold); return; }

                int bought = 0, paid = 0, boughtMarket = 0;
                bool tooDear = false;   // przeglad 175: byly konie (dozwolona czesc polki albo wsie), ale najtanszy ponad budzet
                string what = null;   // id kupionych ras do logu (diagnoza "skad kamele")
                // TARG NIE JEST STAJNIA LORDA (Jeff 15.09: "praktycznie nigdzie nie ma
                // koni do kupienia"). Log 14.09: 676 koni w 235 zakupach w JEDNEJ
                // sesji - lordowie zdejmowali z polek wszystko, po 10 sztuk na
                // wizyte, co 4 dni, a zima tnie hodowle o polowe. Od teraz z targu
                // schodzi najwyzej CZESC polki (dom. 25%) i nigdy ostatnie sztuki
                // (dom. 4 zostaja); reszte lord zamawia u hodowcy - ta sciezka
                // nie drenuje targu, a gold i tak plynie do osady.
                var shelf = settlement.ItemRoster;
                int shelfMounts = 0;
                if (shelf != null)
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var e0 = shelf[i];
                        if (e0.EquipmentElement.Item != null && e0.Amount > 0 && IsPlainMount(e0.EquipmentElement.Item)) shelfMounts += e0.Amount;
                    }
                int marketAllow = (int)(shelfMounts * MBMath.ClampFloat(c.AiMountMarketSharePercent / 100f, 0f, 1f));
                marketAllow = Math.Min(marketAllow, Math.Max(0, shelfMounts - Math.Max(0, c.AiMountShelfFloor)));
                int fromMarket = Math.Min(need, marketAllow);
                int needMarket = fromMarket;
                // 1. z targu osady - najtansze najpierw (w granicach dozwolonej czesci polki)
                while (needMarket > 0 && shelf != null)
                {
                    int best = -1, bestPrice = int.MaxValue;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !IsPlainMount(it)) continue;
                        // KLIMAT (Jeff 30.08: "kamele w zimowym srodowisku?!"):
                        // wielblad/rydwan schodzi z targu tylko u swoich -
                        // lord Polnocy nie wsadzi jazdy na dromadery
                        var iid = it.StringId ?? "";
                        if ((iid.Contains("camel") || iid.Contains("chariot"))
                            && (settlement.Culture == null || it.Culture != settlement.Culture)) continue;
                        int price = PriceOf(settlement, el.EquipmentElement);
                        if (price < bestPrice) { bestPrice = price; best = i; }
                    }
                    if (best >= 0 && bestPrice > budget - paid) tooDear = true;
                    if (best < 0 || bestPrice > budget - paid) break;
                    var chosen = shelf[best];
                    var cel = chosen.EquipmentElement;
                    // ceny hurtu (Jeff 09.10 08:00): kazdy kon po swojej cenie - po zdjeciu konia z polki nastepny wyceniany od nowa (ShelfBuy);
                    // dotad cena pierwszego x liczba koni. Pierwszy - cena z przegladu polki (ta sama polka).
                    int cost, first, lastP;
                    int take = ShelfBuy.Take(shelf, cel, Math.Min(needMarket, chosen.Amount), budget - paid, () => PriceOf(settlement, cel), out cost, out first, out lastP, bestPrice);
                    if (take <= 0) break;
                    party.ItemRoster.AddToCounts(cel, take);
                    paid += cost; bought += take; need -= take; needMarket -= take; boughtMarket += take;
                    var cid = chosen.EquipmentElement.Item != null ? chosen.EquipmentElement.Item.StringId : "?";
                    if (what == null) what = cid; else if (!what.Contains(cid)) what += "," + cid;
                }

                // 2. targ pusty, a lord potrzebuje - zamawia u hodowcy (narzut za fatyge)
                // AUDYT 04.10 (C1): wczesniej kon "od hodowcy" powstawal z niczego. Teraz hodowca to wsie
                // tej osady: kon schodzi z ich zapasu, zloto (z narzutem za fatyge) idzie do wsi.
                int toVillages = 0;
                if (need > 0 && c.AiMountBreederFallback && settlement.BoundVillages != null)
                {
                    float markup = Math.Max(1f, c.AiMountBreederMarkup);
                    bool byMarket = c.HorsesAtMarketPrice;
                    foreach (var v in settlement.BoundVillages)
                    {
                        if (need <= 0) break;
                        var vs = v != null ? v.Settlement : null;
                        if (vs == null || vs.ItemRoster == null) continue;
                        var vr = vs.ItemRoster;
                        for (int i = vr.Count - 1; i >= 0 && need > 0; i--)
                        {
                            var el = vr.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (it == null || el.Amount <= 0 || !IsPlainMount(it)) continue;
                            int price = BreederPrice(settlement, el.EquipmentElement, markup, byMarket);
                            int take = 0;
                            while (take < el.Amount && need > 0 && paid + toVillages + price <= budget) { take++; need--; toVillages += price; }
                            if (take <= 0) { tooDear = true; continue; }
                            vr.AddToCounts(el.EquipmentElement, -take);
                            party.ItemRoster.AddToCounts(el.EquipmentElement, take);
                            TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyForCharacterToSettlement(lord, vs, price * take);
                            bought += take;
                            var nid = it.StringId ?? "?";
                            if (what == null) what = nid; else if (!what.Contains(nid)) what += "," + nid;
                        }
                    }
                }

                if (bought <= 0)
                {
                    // 175.0 + przeglad 175: brak koni rozdzielony (decyzja 4.3 rozroznia "brakuje koni na rynku" i "lordowie za biedni"):
                    // za drogie (byly konie, najtanszy ponad budzet), polka zarezerwowana (konie na polce, ale AiMountMarketSharePercent /
                    // AiMountShelfFloor nie dopuszczaja ani sztuki, a wsie nic nie daly), pusto (ani na polce, ani we wsiach)
                    HorseCensus.OnLordBuyFailed(party, tooDear ? HorseCensus.FailTooDear : (shelfMounts > 0 && marketAllow <= 0) ? HorseCensus.FailShelfReserved : HorseCensus.FailEmpty);
                    return;
                }
                _lastBuy[pid] = today;
                if (lack > 0) Remount(party);   // sklad7b-p: kupione konie najpierw pod jezdzcow bez konia, reszta czeka w taborze na awanse
                HorseCensus.OnLordBought(party, bought, paid + toVillages, boughtMarket, bought - boughtMarket);
                if (paid > 0) TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyForCharacterToSettlement(lord, settlement, paid);
                Log.Info("Stajnia AI: " + lord.Name + " kupil " + bought + " koni ["
                         + (what ?? "?") + "] w " + settlement.Name
                         + " za " + paid + (toVillages > 0 ? " (+ " + toVillages + " wsiom-hodowcom" + (c.HorsesAtMarketPrice ? " po cenie targu" : "") + ")" : "") + " (czekalo na awans " + (want - Math.Max(0, c.AiMountSpareBuffer) - lack)
                         + ", jezdzcow bez konia " + lack
                         + ", mial " + have + "; z targu " + fromMarket + " przy polce " + shelfMounts + ").");
            }
            catch (Exception e) { Log.Error("Stables.AiBuy", e); }
        }

        /// <summary>
        /// Ilu ludzi w tej druzynie STOI GOTOWYCH do awansu na jezdnego i czeka
        /// tylko na rumaka. Liczymy tak samo, jak liczy sama gra: zdrowi z tego
        /// szczebla, przyciecie przez zebrane doswiadczenie (Xp / koszt awansu),
        /// i tylko te sciezki awansu, ktore wymagaja konia.
        /// PRZEGLAD 175: wedlug KATEGORII konia, ktorej zada awans (RequiredMountFor tej partii - jak FilterTargets i PayInHorses:
        /// t2-t3 zwykly, t4-t5 bojowy, t6 szlachetny albo bojowy) - [0] zwykly, [1] bojowy, [2] szlachetny. Zrodlo juz konne pomijane
        /// (FilterTargets/PayInHorses daja mu awans bez rumaka - dotad liczylo sie tu jako potrzeba konia); cel pieszy - bez konia.
        /// Przy kilku konnych celach czlowiek liczy sie raz, w kategorii najtanszego z najliczniejszych gotowych.
        /// </summary>
        internal static int[] NeedByCategory(PartyBase party)
        {
            var need = new int[3];
            try
            {
                var r = party.MemberRoster;
                if (r == null) return need;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || ch.IsMounted) continue;
                    int healthy = el.Number - el.WoundedNumber;
                    if (healthy <= 0) continue;
                    var targets = ch.UpgradeTargets;
                    if (targets == null || targets.Length == 0) continue;

                    int bestForThisRank = 0, bestLevel = -1;
                    for (int t = 0; t < targets.Length; t++)
                    {
                        var tg = targets[t];
                        if (tg == null || tg.IsHero || !tg.IsMounted) continue;   // ta sciezka nie sadza w siodle
                        ItemCategory cat = null;
                        try { cat = RequiredMountFor(party, tg); } catch { }
                        int lv = Level(cat);
                        if (lv < 0) continue;
                        int ready = healthy;
                        try
                        {
                            int xpCost = ch.GetUpgradeXpCost(party, t);
                            if (xpCost > 0) ready = Math.Min(healthy, el.Xp / xpCost);
                        }
                        catch { }
                        if (ready <= 0) continue;
                        if (ready > bestForThisRank || (ready == bestForThisRank && lv < bestLevel)) { bestForThisRank = ready; bestLevel = lv; }
                    }
                    if (bestLevel >= 0) need[bestLevel] += bestForThisRank;   // jeden czlowiek = jeden kon, nie po jednym na sciezke
                }
            }
            catch { }
            return need;
        }

        /// <summary>
        /// Potrzeba do ZAKUPOW Stajni AI - rachunek sprzed 175 bez zmian (getter UpgradeRequiresItemFromCategory, kazde zrodlo), zeby przy
        /// wylacznikach 175 zakupy szly dokladnie jak dotad (bieg bazowy B0); pomiar i niedobor wedlug kategorii licza NeedByCategory.
        /// </summary>
        internal static int NeedForUpgrades(PartyBase party)
        {
            int need = 0;
            try
            {
                var r = party.MemberRoster;
                if (r == null) return 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero) continue;
                    int healthy = el.Number - el.WoundedNumber;
                    if (healthy <= 0) continue;
                    var targets = ch.UpgradeTargets;
                    if (targets == null || targets.Length == 0) continue;

                    int bestForThisRank = 0;
                    for (int t = 0; t < targets.Length; t++)
                    {
                        var tg = targets[t];
                        if (tg == null) continue;
                        ItemCategory cat = null;
                        try { cat = tg.UpgradeRequiresItemFromCategory; } catch { }
                        if (cat == null) continue;                 // ta sciezka nie potrzebuje konia
                        int ready = healthy;
                        try
                        {
                            int xpCost = ch.GetUpgradeXpCost(party, t);
                            if (xpCost > 0) ready = Math.Min(healthy, el.Xp / xpCost);
                        }
                        catch { }
                        if (ready > bestForThisRank) bestForThisRank = ready;
                    }
                    need += bestForThisRank;   // jeden czlowiek = jeden kon, nie po jednym na sciezke
                }
            }
            catch { }
            return need;
        }

        /// <summary>Wierzchowiec pod siodlo - nie juczny mul i nie bydlo.</summary>
        /// <summary>Prawdziwy wierzchowiec: nie mul, nie kon juczny/pociagowy, nie slon, nie smok.</summary>
        internal static bool IsPlainMount(ItemObject it)
        {
            try
            {
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) return false;
                // ROT daje sloniowi item_category="horse" - stajnia kupowala
                // lordom SLONIE jako zwykle konie i tak trafialy na Polnoc
                // (Jeff 29.08: "skad slonie na polnocy?!"). Smoki tak samo.
                var id = it.StringId ?? "";
                if (id == "elephant" || id.StartsWith("rot_elephant") || id.StartsWith("dragon_")) return false;
                var hc = it.HorseComponent;
                if (hc == null || !hc.IsMount) return false;
                // MAMUT MA item_category=horse (14.09: "Stajnia AI: Barristan Selmy
                // kupil 9 koni [mammoth]") - zwykly wierzchowiec to tylko rodzina
                // konia (Monster horse/horse_2); wielblady, rydwany, mamuty, slonie
                // rzadzi MountLaw, nie stajnia
                var mon = hc.Monster;
                if (mon == null || mon.StringId == null || !mon.StringId.StartsWith("horse")) return false;
                var cat = it.ItemCategory;
                return cat == DefaultItemCategories.Horse || cat == DefaultItemCategories.WarHorse
                    || cat == DefaultItemCategories.NobleHorse;
            }
            catch { return false; }
        }

        internal static int CountAnyMounts(PartyBase party)
        {
            int n = 0;
            try
            {
                var r = party.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                    if (IsPlainMount(r[i].EquipmentElement.Item)) n += r[i].Amount;
            }
            catch { }
            return n;
        }

        /// <summary>
        /// KON PO CENIE TARGU (paczka 143, Jeff 07.10: "konie: rekrut konny placi cene konia z targu, hodowca po cenie targu").
        /// Cena konia na targu, na ktorym ta osada kupuje i sprzedaje konie - ten sam, na ktorym notabl kupuje konia ochotnikowi
        /// (VolunteerKit.MarketOf: miasto - swoj targ; wies - jej miasto, inaczej najblizsze; zamek - najblizsze miasto), liczona
        /// ShelfPrice - tym samym wzorem, ktorym notabl kupuje konia ochotnikowi, gracz na straganie i lord z polki (PriceOf).
        /// 0 = brak targu albo konia (wolajacy bierze wtedy stara cene). Jedna regula dla rekruta (RecruitCost) i hodowcy (BreederPrice).
        /// </summary>
        internal static int MarketPrice(TaleWorlds.CampaignSystem.Settlements.Settlement where, EquipmentElement horse)
        {
            try
            {
                if (horse.Item == null) return 0;
                var m = VolunteerKit.MarketOf(where);
                if (m == null || m.Town == null) return 0;
                return ShelfPrice(m, horse);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Cena kupna konia na targu tego miasta - JEDNA dla wszystkich (przeglad 07.10): wartosc sztuki x podaz i popyt Armoury
        /// (SupplyDemand) x marza kupca, tak jak liczy targ, gdy znany jest kupiec-miasto (MarketData.GetPrice z partia miasta) -
        /// ten sam wzor co notabl (VolunteerKit), gracz na straganie i lordowie przy sprzecie (AiGear). Dotad konie lorda z polki,
        /// rekrut konny i hodowca szly przez Town.GetItemPrice bez kupca - wtedy podaz i popyt Armoury nie dzialaja, a liczy
        /// lancuch cudzych modeli cen (ok. 2x wartosci), wiec ten sam kon mial na jednym targu trzy ceny.
        /// </summary>
        internal static int ShelfPrice(TaleWorlds.CampaignSystem.Settlements.Settlement market, EquipmentElement el)
        {
            return Math.Max(1, market.Town.MarketData.GetPrice(el, null, false, market.Party));
        }

        /// <summary>
        /// Cena konia od wsi-hodowcy (zamowienie lorda, gdy targ pusty). Dotad stala wartosc x AiMountBreederMarkup (1.3) bez
        /// wzgledu na podaz i popyt - log 07.10 03:23: Tattered Prince w Braavos 5 koni z targu za 3620 (ok. 720 za sztuke)
        /// i 2 od hodowcow za 9360 (4680 za sztuke). HorsesAtMarketPrice: hodowca bierze cene tego konia na targu miasta
        /// (MarketPrice); zloto dalej idzie do wsi, kon dalej schodzi z jej zapasu. Bez targu - stara cena.
        /// </summary>
        internal static int BreederPrice(TaleWorlds.CampaignSystem.Settlements.Settlement settlement, EquipmentElement el, float markup, bool byMarket)
        {
            int price = byMarket ? MarketPrice(settlement, el) : 0;
            if (price <= 0) price = (int)((el.Item != null ? el.Item.Value : 0) * markup);
            return price;
        }

        private static int PriceOf(TaleWorlds.CampaignSystem.Settlements.Settlement st, EquipmentElement el)
        {
            try
            {
                // przeglad 07.10: przy HorsesAtMarketPrice ta sama cena co gracz i notabl na tym targu (ShelfPrice)
                if (st.Town != null) return Settings.Current.HorsesAtMarketPrice ? ShelfPrice(st, el) : Math.Max(1, st.Town.GetItemPrice(el, null, false));
            }
            catch { }
            return Math.Max(1, el.Item != null ? el.Item.Value : 1);
        }

        /// <summary>Najtanszy kon pod siodlo, jakiego zna swiat - z kultury osady, jak sie da.</summary>
        private static ItemObject _cheapAny;

        private static ItemObject CheapestMount(TaleWorlds.CampaignSystem.Settlements.Settlement st)
        {
            try
            {
                ItemObject local = null, any = null;
                foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!IsPlainMount(it) || it.NotMerchandise || it.Value <= 0) continue;
                    if (it.ItemCategory != DefaultItemCategories.Horse) continue;      // hodowca ma zwykle konie
                    // KLIMAT (Jeff 30.08: "kamele w zimowym srodowisku?!"):
                    // wielblady i rydwany kupuje sie WYLACZNIE tam, gdzie to
                    // miejscowa rasa - hodowca w Winterfell nie trzyma dromadera
                    var iid = it.StringId ?? "";
                    if ((iid.Contains("camel") || iid.Contains("chariot"))
                        && (st.Culture == null || it.Culture != st.Culture)) continue;
                    if (any == null || it.Value < any.Value) any = it;
                    if (st.Culture != null && it.Culture == st.Culture
                        && (local == null || it.Value < local.Value)) local = it;
                }
                _cheapAny = any;
                // miejscowa rasa TYLKO, jesli nie zdziera - inaczej hodowca w Braavos
                // liczyl 1500 za sztuke, bo najtanszy kon jego kultury tyle wart
                if (local != null && any != null && local.Value > any.Value * 1.4f) return any;
                return local ?? any;
            }
            catch { return _cheapAny; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) { Log.Info("Stables: wylaczone."); return; }

                var g = AccessTools.PropertyGetter(typeof(CharacterObject), "UpgradeRequiresItemFromCategory");
                if (g == null) { Log.Info("Stables: brak gettera UpgradeRequiresItemFromCategory."); return; }
                h.Patch(g, postfix: new HarmonyMethod(typeof(Stables), "RanksNeedHorses") { priority = Priority.Last });

                // KON ZA AWANS TRAFIA DO ZBROJOWNI, A NIE W NICOSC (Jeff 13.09)
                var mDone = AccessTools.Method(typeof(PartyScreenLogic), "DoneLogic");
                if (mDone != null)
                    h.Patch(mDone, prefix: new HarmonyMethod(typeof(Stables), "GrabPaidHorses"),
                                   postfix: new HarmonyMethod(typeof(Stables), "BankPaidHorses"));
                else Log.Info("Stables: brak PartyScreenLogic.DoneLogic - konie z awansow beda przepadac.");

                var tU = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.PartyUpgraderCampaignBehavior);
                var mList = AccessTools.Method(tU, "GetPossibleUpgradeTargets");
                var mUp = AccessTools.Method(tU, "UpgradeTroop");
                if (mList != null) h.Patch(mList, postfix: new HarmonyMethod(typeof(Stables), "FilterTargets"));
                if (mUp != null) h.Patch(mUp, prefix: new HarmonyMethod(typeof(Stables), "PayInHorses"));

                Log.Info("Stables: awans na jezdnego wymaga rumaka - gracz przez ekran druzyny (vanilla sam zabiera konie), "
                         + "AI przez tabor (lista=" + (mList != null) + ", zaplata=" + (mUp != null) + "). "
                         + "Kon bojowy od tieru " + c.WarHorseFromTier + ", szlachetny od " + c.NobleHorseFromTier + ".");
            }
            catch (Exception e) { Log.Error("Stables.ApplyAll", e); }
        }
    }
}
