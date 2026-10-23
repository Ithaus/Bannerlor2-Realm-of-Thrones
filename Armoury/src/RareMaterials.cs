using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m97-surowce (Jeff 20.10: "luk z czardrzewa okay, trzeba miec czardrzewo, smoczej kosci - smocza kosc ... aby dalo sie wszystko wykuc,
    /// nawet jesli to bedzie trudne") - DWA RZADKIE SUROWCE dla lukow z listy RareBowsIds (m96-wzory pkt 7).
    ///  - Towary: arm_weirwood (klepka czardrzewa, 2.5 kg) i arm_dragonbone (kosc smoka, 2 kg) z pliku ModuleData/arm_items_rare.xml.
    ///    W grze, ROT i BK takiego surowca nie ma (grep ModuleData wszystkich modulow: weirwood/dragonbone tylko jako luki i scena Muru).
    ///  - Kategoria wlasna arm_rare_stuff (rejestracja w InitializeSubModuleGameObjects - przed wczytaniem XML przedmiotow, jak NavalDLC
    ///    walrus_tusk): NIE towar handlowy (cena w granicach gry 0.8-1.3 x wartosci, nie 0.1-10 jak towary masowe), zaden warsztat jej nie
    ///    przerabia (AI nie kupuje do warsztatow), popyt bazowy maly i DODATNI (BKEconomyModel dzieli przez BaseDemand - przy 0 wyszloby NaN).
    ///  - Doplyw (zamknieta ekonomia - surowiec idzie z konkretnej ziemi, nie z niczego): raz na dobe los na region; trafienie = 1 sztuka do
    ///    magazynu (rynku) jednej osady zrodla, gdy zapas regionu jest ponizej pulapu i zapas swiata (osady + wszystkie partie) ponizej pulapu
    ///    swiata. Czardrzewo - wsie Wolnych Ludzi za Murem w lesie (las i traperzy: Whitetree, Milkwater, Icecave, Hauntwood, Walrus Bay, Myre);
    ///    smocza kosc - ruiny Valyrii (Usek i Esme pod Oros, miasto Elyria) i stare targi Volantis i Qarth. Lista id osad, nie kultura -
    ///    kultura osady zmienia sie z wlascicielem (BK), drzewa i ruiny nie. Dalej: wiesniacy moga zabrac sztuke ze wsi na targ swojego miasta
    ///    (VillagerCampaignBehavior bierze kazdy przedmiot); karawany gry i BK jej NIE kupuja (kupuja tylko kategorie handlowe i zwierzeta) - sztuka
    ///    czeka tam, gdzie sie pojawila. Mieszczanie kupuja ja rzadko (maly popyt) - to jedyny odplyw poza kuznia gracza.
    ///  - Nic z niczego (recenzja 20.10): losowe nagrody gry z puli "kazdy towar handlowy" omijalyby zrodlo - nagroda zadania Caravan Ambush
    ///    (3 losowe towary z niczego) i zdarzenie Trade Proposal (2 x wartosc zlotem z niczego za 1 sztuke z sakw). Klucz RareMaterialOnlyFromSource:
    ///    wylosowany nasz surowiec podmieniany na inny towar z tej samej puli (zadanie) albo pomijany w wyborze (zdarzenie) - latki raz na proces.
    ///  - Kucie (Forge.LegendAllowed - wspolna brama naszego menu i CRAFT BK): luk z listy, ktorego surowiec istnieje, nie jest juz "rzadkim
    ///    materialem" (PatternGate.RareStuff = false - wzor i nauka jak kazdego unikatu ROT: z oryginalu), ale kowadlo zada sztuk surowca w
    ///    sakwach gracza: klepka = 2 x masa luku (t5-6 x2 - ta sama regula co przepis z masy), najmniej 1 cala sztuka (klepke tnie sie z jednego
    ///    kawalka; reszta to odpad). Pekniecie przy kuciu niszczy klepke (surowiec przepada; w robocie z czasem klepka schodzi na starcie, a pekniecie
    ///    przy odbiorze - Forge.Finish - liczy ja jako przepadla: NoteLost). Drewno i len z przepisu zostaja (raczka, cieciwa).
    ///  - Ksiega towarow (GoodsLedger): doplyw i zuzycie w ramce "drogi towarow (ZC1)" z opisem - nie "bez wyjasnienia".
    /// Stan w zapisie: brak (los dobowy bez pamieci; zapas lezy w magazynach osad i partii - zapis gry). Watek glowny.
    /// </summary>
    internal static class RareMaterials
    {
        internal const string CategoryId = "arm_rare_stuff";

        private sealed class Mat
        {
            internal string Id, Key, Name, Where;
            internal string[] Sources;
            internal float PerDay;
            internal int RegionCap, WorldCap;
            internal ItemObject Item;
            internal readonly List<Settlement> Found = new List<Settlement>();
            internal int Rolls, Hits, Added, CapRegion, CapWorld, NoPlace, Used, Lost;
            internal int LastRegion = -1, LastWorld = -1;
        }

        // regiony i pulapy (projekt: Jeff nie podaje liczb). Czardrzewo: 1 sztuka na ok. 15 dob (24 na rok 364 dni), zapas regionu 3, swiata 10.
        // Smocza kosc: 1 na ok. 30 dob (12 na rok), zapas regionu 2, swiata 6 - kosci smokow sa w ruinach, nie odrastaja; malo i drogo.
        private static readonly Mat[] _mats =
        {
            new Mat { Id = "arm_weirwood", Key = "weirwood", Name = "weirwood",
                      Where = "it is found only beyond the Wall, in the Free Folk villages of the haunted forest (Whitetree, Milkwater, Icecave, Hauntwood, Walrus Bay, Myre)",
                      Sources = new[] { "castle_village_S5_2", "castle_village_S6_1", "village_S6_3", "castle_village_N6_2", "ROT_castle45_village1", "ROT_castle45_village2" },
                      PerDay = 1f / 15f, RegionCap = 3, WorldCap = 10 },
            new Mat { Id = "arm_dragonbone", Key = "dragon", Name = "dragonbone",
                      Where = "it is found only in the ruins of Valyria (Usek, Esme, Elyria) and the old markets of Volantis and Qarth",
                      Sources = new[] { "castle_village_K1_1", "castle_village_K1_2", "ROT_town20", "town_ES2", "ROT_town35" },
                      PerDay = 1f / 30f, RegionCap = 2, WorldCap = 6 },
        };

        private static object _for;   // kampania, dla ktorej rozwiazano przedmioty i osady
        private static int _stumbles, _takeLines, _days, _badCat;

        private static Settings S { get { return Settings.Current; } }
        /// <summary>Droga surowca czynna: luki z listy RareBowsIds kuje sie z surowca (bez klucza - blokada jak w m96-wzory).</summary>
        internal static bool On { get { var s = S; return s != null && s.RareBowsNotForged && s.RareBowsNeedMaterial; } }
        internal static bool SupplyOn { get { var s = S; return On && S.RareMaterialSupply; } }
        /// <summary>Recenzja 20.10: losowe nagrody gry (Caravan Ambush, Trade Proposal) nie biora naszego surowca - przedmioty sa w XML niezaleznie od kluczy kucia.</summary>
        internal static bool GuardOn { get { var s = S; return s != null && s.RareMaterialOnlyFromSource; } }

        // latki "nic z niczego" (raz na proces) i liczniki sesji
        private static bool _hooksTried;
        private static string _hookAmbush = "nie zakladana", _hookTrade = "nie zakladana";
        private static FieldInfo _fReward, _fSeed;
        private static int _ambushSwaps, _ambushDrop, _tradeSkips, _tradeNone, _guardLines;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error(where, e);
        }

        // ------------------------------------------------------------ kategoria (przed XML przedmiotow)
        /// <summary>SubModuleMain.InitializeSubModuleGameObjects: kategoria arm_rare_stuff, zanim gra wczyta XML przedmiotow (item_category w arm_items_rare.xml).</summary>
        internal static void RegisterCategory(Game game)
        {
            try
            {
                if (game == null || !(game.GameType is Campaign) || game.ObjectManager == null) return;
                var c = game.ObjectManager.GetObject<ItemCategory>(CategoryId) ?? game.ObjectManager.RegisterPresumedObject(new ItemCategory(CategoryId));
                // NIE towar handlowy, popyt bazowy 0.02 (int 20 x 0.001), bez luksusu i wlasciwosci; IsValid TAK (rynek miasta liczy kategorie)
                c.InitializeObject(false, 20, 0, ItemCategory.Property.None, null, 0f, false, true);
            }
            catch (Exception e) { Stumble("RareMaterials.RegisterCategory", e); }
        }

        // ------------------------------------------------------------ rozwiazanie przedmiotow i osad
        private static void Resolve()
        {
            var c = Campaign.Current;
            if (c == null || ReferenceEquals(c, _for)) return;
            _for = c;
            foreach (var m in _mats)
            {
                m.Found.Clear();
                m.Item = null;
                try { m.Item = MBObjectManager.Instance.GetObject<ItemObject>(m.Id); } catch { m.Item = null; }
                // bez wlasnej kategorii (rejestracja nie weszla) przedmiot nie trafia na rynek - kategoria null wywrocilaby konsumpcje miasta
                if (m.Item != null && (m.Item.ItemCategory == null || m.Item.ItemCategory.StringId != CategoryId)) { _badCat++; m.Item = null; }
                foreach (var id in m.Sources)
                {
                    try { var st = Settlement.Find(id); if (st != null) m.Found.Add(st); } catch { }
                }
                m.Rolls = m.Hits = m.Added = m.CapRegion = m.CapWorld = m.NoPlace = m.Used = m.Lost = 0;
                m.LastRegion = -1; m.LastWorld = -1;
            }
            _takeLines = 0; _days = 0;
        }

        private static Mat MatFor(ItemObject bow)
        {
            if (bow == null || bow.StringId == null) return null;
            Resolve();
            var id = bow.StringId;
            foreach (var m in _mats) if (id.IndexOf(m.Key, StringComparison.OrdinalIgnoreCase) >= 0) return m;
            return null;
        }

        /// <summary>Luk z listy RareBowsIds, ktory przy kluczu kuje sie z surowca (surowiec jest w grze). PatternGate.RareStuff = false dla niego.</summary>
        internal static bool Covers(ItemObject bow)
        {
            try
            {
                if (!On || !PatternGate.InRareList(bow)) return false;
                var m = MatFor(bow);
                return m != null && m.Item != null;
            }
            catch (Exception e) { Stumble("RareMaterials.Covers", e); return false; }
        }

        /// <summary>Sztuk surowca na jeden luk: klepka 2 x masa luku (t5-6 jeszcze x2 - stara regula przepisu z masy), najmniej 1 cala sztuka. m110-luki: celowo
        /// po staremu - przy BowTierMaterials ten surowiec JEST klepka (przepis z masy nie dolicza wtedy wyboru klepki z drewna, tylko majdan, uchwyt i cieciwe).</summary>
        private static int PiecesFor(ItemObject bow, Mat m)
        {
            float stave = bow.Weight * 2f * (Recipes.Grade(bow) >= 5 ? 2f : 1f);
            float piece = m.Item != null && m.Item.Weight > 0.01f ? m.Item.Weight : 1f;
            int n = (int)Math.Ceiling(stave / piece - 1e-4);
            return n < 1 ? 1 : n;
        }

        private static int InBags(ItemObject it)
        {
            var p = MobileParty.MainParty;
            return p != null && p.ItemRoster != null ? p.ItemRoster.GetItemNumber(it) : 0;
        }

        /// <summary>Forge.LegendAllowed: false + powod (EN), gdy luk z surowca, a w sakwach go za malo.</summary>
        internal static bool Ready(ItemObject bow, out string why)
        {
            why = null;
            try
            {
                if (!Covers(bow)) return true;
                var m = MatFor(bow);
                int need = PiecesFor(bow, m), have = InBags(m.Item);
                if (have >= need) return true;
                why = "Needs " + (need > 1 ? need + " pieces of " : "") + m.Name + " - " + m.Where + ". You carry " + (have > 0 ? have.ToString() : "none") + ".";
                return false;
            }
            catch (Exception e) { Stumble("RareMaterials.Ready", e); return true; }
        }

        /// <summary>m109-kuznia (ForgeBill): surowiec luku i ile go trzeba / ile w sakwach - te same PiecesFor i InBags co Ready i Take. false = luk bez surowca.</summary>
        internal static bool NeedOf(ItemObject bow, out ItemObject mat, out int need, out int have)
        {
            mat = null; need = 0; have = 0;
            try
            {
                if (!Covers(bow)) return false;
                var m = MatFor(bow);
                mat = m.Item;
                need = PiecesFor(bow, m);
                have = InBags(m.Item);
                return true;
            }
            catch (Exception e) { Stumble("RareMaterials.NeedOf", e); return false; }
        }

        /// <summary>m109-kuznia (ForgeBill): gdzie surowiec luku sie znajduje (napis podpowiedzi) - null bez surowca.</summary>
        internal static string WhereOf(ItemObject bow)
        {
            try { return Covers(bow) ? MatFor(bow).Where : null; }
            catch { return null; }
        }

        /// <summary>Kafelek CRAFT (PatternGate.BlockText): znany wzor, brak surowca.</summary>
        internal static string TileText(ItemObject bow)
        {
            try
            {
                if (!Covers(bow)) return null;
                var m = MatFor(bow);
                if (InBags(m.Item) >= PiecesFor(bow, m)) return null;
                return m.Key == "dragon" ? "PATTERN KNOWN - NEEDS DRAGONBONE" : "PATTERN KNOWN - NEEDS WEIRWOOD";
            }
            catch (Exception e) { Stumble("RareMaterials.TileText", e); return null; }
        }

        /// <summary>Forge.Smith / Forge.Begin po zdjeciu materialow: klepka schodzi z sakw (pekniecie - tez, klepka peka z lukiem).</summary>
        internal static void Take(ItemObject bow, bool cracked)
        {
            try
            {
                if (!Covers(bow)) return;
                var m = MatFor(bow);
                int need = PiecesFor(bow, m), have = InBags(m.Item);
                int take = Math.Min(need, have);
                if (take > 0)
                {
                    var f = Fz(MobileParty.MainParty, "surowce rzadkie (m97): klepka w luku gracza");   // ksiega towarow: ujscie z opisem
                    try { MobileParty.MainParty.ItemRoster.AddToCounts(m.Item, -take); }
                    finally { GoodsLedger.End(f); }
                }
                if (cracked) m.Lost += take; else m.Used += take;
                Log.Player(cracked ? "The " + m.Name + " stave split with the bow - it is lost." : "The " + m.Name + " went into the bow (" + take + " piece" + (take > 1 ? "s" : "") + ").", cracked);
                _takeLines++;
                if (_takeLines <= 40 || _takeLines % 25 == 0)
                    Log.Info("Surowce rzadkie (m97-surowce): kucie " + bow.StringId + " | zuzyte " + take + " x " + m.Id + " z sakw gracza (potrzeba " + need + ", bylo " + have
                             + "), zostalo " + InBags(m.Item) + " | pekniecie " + (cracked ? "TAK - surowiec przepada" : "NIE") + " | od startu sesji: " + m.Name + " w lukach " + m.Used
                             + ", przepadlo " + m.Lost + ", potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("RareMaterials.Take", e); }
        }

        /// <summary>Forge.Finish, pekniecie po czasie (recenzja 20.10): klepka zdjeta przy Forge.Begin peka z lukiem - z "w lukach" do "przepadlo", napis i linia.</summary>
        internal static void NoteLost(ItemObject bow)
        {
            try
            {
                if (!Covers(bow)) return;
                var m = MatFor(bow);
                int n = PiecesFor(bow, m);
                int back = Math.Min(n, m.Used);   // robota mogla ruszyc w poprzedniej sesji (licznik od zera) - wtedy tylko "przepadlo"
                m.Used -= back; m.Lost += n;
                Log.Player("The " + m.Name + " stave split with the bow - it is lost.", true);
                _takeLines++;
                if (_takeLines <= 40 || _takeLines % 25 == 0)
                    Log.Info("Surowce rzadkie (m97-surowce): kucie " + bow.StringId + " | pekniecie po czasie (Forge.Finish) - " + n + " x " + m.Id + " zdjete przy starcie roboty przepada"
                             + " | od startu sesji: " + m.Name + " w lukach " + m.Used + ", przepadlo " + m.Lost + ", potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("RareMaterials.NoteLost", e); }
        }

        /// <summary>Ramka ksiegi towarow "drogi towarow (ZC1)" z opisem (null, gdy ksiega wylaczona).</summary>
        private static GoodsLedger.Frame Fz(object at, string label)
        {
            var f = GoodsLedger.Begin(GoodsLedger.FZc1, at);
            if (f != null) f.Info = label;
            return f;
        }

        // ------------------------------------------------------------ doplyw dobowy
        private static bool CanReceive(Settlement st)
        {
            if (st == null || st.ItemRoster == null) return false;
            if (st.IsVillage && st.Village != null && st.Village.VillageState != Village.VillageStates.Normal) return false;   // spalona / grabiona wies nic nie daje
            if (st.IsUnderSiege) return false;
            return true;
        }

        private static int RegionStock(Mat m)
        {
            int n = 0;
            foreach (var st in m.Found) if (st != null && st.ItemRoster != null) n += st.ItemRoster.GetItemNumber(m.Item);
            return n;
        }

        /// <summary>Zapas swiata: magazyny i schowki (Stash, jak GoodsLedger) wszystkich osad i magazyny wszystkich partii (liczone tylko przy trafieniu
        /// losu i co 7 dob - nie co zdarzenie).</summary>
        private static int WorldStock(Mat m)
        {
            int n = 0;
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                if (st.ItemRoster != null) n += st.ItemRoster.GetItemNumber(m.Item);
                var sh = st.Stash;
                if (sh != null && !ReferenceEquals(sh, st.ItemRoster)) n += sh.GetItemNumber(m.Item);
            }
            foreach (var p in MobileParty.All) if (p != null && p.ItemRoster != null) n += p.ItemRoster.GetItemNumber(m.Item);
            return n;
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: los na region, sztuka do magazynu osady zrodla, linia doby.</summary>
        internal static void Daily()
        {
            if (!SupplyOn) return;
            Resolve();
            _days++;
            var sb = new StringBuilder(256);
            sb.Append("Surowce rzadkie (m97-surowce): dzien ").Append((int)CampaignTime.Now.ToDays);
            int addedToday = 0;
            foreach (var m in _mats)
            {
                string where = null;
                try
                {
                    if (m.Item == null || m.Found.Count == 0) { sb.Append(" | ").Append(m.Name).Append(": BRAK (").Append(m.Item == null ? "przedmiotu" : "osad zrodla").Append(")"); continue; }
                    m.Rolls++;
                    bool hit = TaleWorlds.Core.MBRandom.RandomFloat < m.PerDay;
                    int region = RegionStock(m);
                    m.LastRegion = region;
                    bool weekly = _days % 7 == 1;
                    if (hit)
                    {
                        m.Hits++;
                        if (region >= m.RegionCap) m.CapRegion++;
                        else
                        {
                            int world = WorldStock(m);
                            m.LastWorld = world;
                            if (world >= m.WorldCap) m.CapWorld++;
                            else
                            {
                                var cand = new List<Settlement>(m.Found.Count);
                                foreach (var st in m.Found) if (CanReceive(st)) cand.Add(st);
                                if (cand.Count == 0) m.NoPlace++;
                                else
                                {
                                    var st = cand[TaleWorlds.Core.MBRandom.RandomInt(cand.Count)];
                                    var f = Fz(st, "surowce rzadkie (m97): " + m.Name + " z ziemi zrodla");   // ksiega towarow: zrodlo z opisem
                                    try { st.ItemRoster.AddToCounts(m.Item, 1); }
                                    finally { GoodsLedger.End(f); }
                                    m.Added++; addedToday++;
                                    m.LastRegion = region + 1; m.LastWorld = world + 1;
                                    where = st.Name != null ? st.Name.ToString() : st.StringId;
                                }
                            }
                        }
                    }
                    else if (weekly) m.LastWorld = WorldStock(m);
                    sb.Append(" | ").Append(m.Name).Append(": przybylo ").Append(where != null ? "1 (" + where + ")" : "0")
                      .Append(", zapas regionu ").Append(m.LastRegion).Append('/').Append(m.RegionCap)
                      .Append(", swiata ").Append(m.LastWorld >= 0 ? m.LastWorld.ToString() : "?").Append('/').Append(m.WorldCap)
                      .Append(" (od startu sesji: losowan ").Append(m.Rolls).Append(", trafien ").Append(m.Hits).Append(", przybylo ").Append(m.Added)
                      .Append(", pulap regionu ").Append(m.CapRegion).Append(", pulap swiata ").Append(m.CapWorld).Append(", bez miejsca ").Append(m.NoPlace).Append(")");
                }
                catch (Exception e) { Stumble("RareMaterials.Daily(" + m.Id + ")", e); }
            }
            sb.Append(" | dzis przybylo ").Append(addedToday)
              .Append(" | nagrody gry bez surowca (od startu sesji): Caravan Ambush podmienione ").Append(_ambushSwaps).Append(", zdjete ").Append(_ambushDrop)
              .Append(", Trade Proposal pominiete (wywolan wyboru) ").Append(_tradeSkips).Append(" (bez innego towaru ").Append(_tradeNone).Append(")")
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ nic z niczego: losowe nagrody gry (recenzja 20.10)
        private static bool IsOurs(ItemObject it)
        {
            var id = it != null ? it.StringId : null;
            if (id == null) return false;
            foreach (var m in _mats) if (id == m.Id) return true;
            return false;
        }

        /// <summary>Raz na proces (z linii startu - w kampanii): Caravan Ambush (prefiks OnQuestSucceeded) i Trade Proposal (postfiks funkcji lokalnej
        /// GetTradeProposalGood w IncidentsCampaignBehaviour.InitializeIncidents - nazwa kompilatora szukana po fragmencie, jedna metoda).</summary>
        private static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            Harmony h;
            try { h = new Harmony("com.jeff.armoury.m97surowce"); }
            catch (Exception e) { _hookAmbush = _hookTrade = "BLAD (Harmony)"; Stumble("RareMaterials.EnsureHooks", e); return; }
            try
            {
                var tq = typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest);
                _fReward = AccessTools.DeclaredField(tq, "_rewardItems");
                var mq = AccessTools.DeclaredMethod(tq, "OnQuestSucceeded");
                if (mq == null || _fReward == null || _fReward.FieldType != typeof(List<ItemObject>)) _hookAmbush = "BRAK (" + (mq == null ? "OnQuestSucceeded" : "_rewardItems") + ")";
                else { h.Patch(mq, prefix: new HarmonyMethod(typeof(RareMaterials), nameof(AmbushPre))); _hookAmbush = "wpieta"; }
            }
            catch (Exception e) { _hookAmbush = "BLAD"; Stumble("RareMaterials.EnsureHooks(CaravanAmbush)", e); }
            try
            {
                var ti = typeof(IncidentsCampaignBehaviour);
                _fSeed = AccessTools.DeclaredField(ti, "_activeIncidentSeed");
                string name = null; int n = 0;
                foreach (var mi in AccessTools.GetDeclaredMethods(ti))
                    if (mi.Name.Contains("g__GetTradeProposalGood|") && !mi.IsStatic && mi.ReturnType == typeof(ItemRosterElement) && mi.GetParameters().Length == 0) { name = mi.Name; n++; }
                var mt = n == 1 ? AccessTools.DeclaredMethod(ti, name) : null;
                if (mt == null || _fSeed == null || _fSeed.FieldType != typeof(long)) _hookTrade = "BRAK (" + (n != 1 ? "GetTradeProposalGood x" + n : mt == null ? name : "_activeIncidentSeed") + ")";
                else { h.Patch(mt, postfix: new HarmonyMethod(typeof(RareMaterials), nameof(TradePost))); _hookTrade = "wpieta"; }
            }
            catch (Exception e) { _hookTrade = "BLAD"; Stumble("RareMaterials.EnsureHooks(TradeProposal)", e); }
        }

        /// <summary>Caravan Ambush: przed wyplata nagrody nasz surowiec z listy (3 losowe towary handlowe z niczego) zamieniony na inny towar z tej samej puli
        /// gry (IsTradeGood i nie NotMerchandise) - lista jest w zapisie zadania, wiec dziala tez dla zadan wylosowanych przed paczka.</summary>
        public static void AmbushPre(object __instance)
        {
            try
            {
                if (!GuardOn || _fReward == null || __instance == null) return;
                var list = _fReward.GetValue(__instance) as List<ItemObject>;
                if (list == null) return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var old = list[i];
                    if (!IsOurs(old)) continue;
                    var pool = new List<ItemObject>(64);
                    foreach (var t in TaleWorlds.CampaignSystem.Extensions.Items.All)
                        if (t != null && t.IsTradeGood && !t.NotMerchandise && !IsOurs(t)) pool.Add(t);
                    string to;
                    if (pool.Count > 0) { list[i] = pool[TaleWorlds.Core.MBRandom.RandomInt(pool.Count)]; _ambushSwaps++; to = list[i].StringId; }
                    else { list.RemoveAt(i); _ambushDrop++; to = "nic (pula pusta)"; }
                    _guardLines++;
                    if (_guardLines <= 20)
                        Log.Info("Surowce rzadkie (m97-surowce): nagroda zadania Caravan Ambush - wylosowany " + old.StringId + " zamieniony na " + to
                                 + " (surowiec tylko z ziemi zrodla) | od startu sesji: podmienione " + _ambushSwaps + ", zdjete " + _ambushDrop + ".");
                }
            }
            catch (Exception e) { Stumble("RareMaterials.AmbushPre", e); }
        }

        /// <summary>Trade Proposal: gdy wybor gry (losowy stos towaru handlowego z sakw, ziarno zdarzenia) trafil nasz surowiec - wybor z tej samej listy bez
        /// naszych surowcow i z tym samym ziarnem (warunek, podpowiedz i skutek zdarzenia wolaja wybor kilka razy - wynik ten sam); brak innego towaru =
        /// pusty wybor (warunek zdarzenia zwraca wtedy false - zdarzenie nie wypada).</summary>
        public static void TradePost(object __instance, ref ItemRosterElement __result)
        {
            try
            {
                if (!GuardOn || !IsOurs(__result.EquipmentElement.Item)) return;
                var old = __result.EquipmentElement.Item.StringId;
                var list = new List<ItemRosterElement>();
                var p = MobileParty.MainParty;
                if (p != null && p.ItemRoster != null)
                    foreach (var x in p.ItemRoster) { var it = x.EquipmentElement.Item; if (it != null && it.IsTradeGood && !IsOurs(it)) list.Add(x); }
                long seed = _fSeed != null && __instance != null ? (long)_fSeed.GetValue(__instance) : 0L;
                __result = list.Count > 0 ? Helpers.IncidentHelper.GetSeededRandomElement(list, seed) : default(ItemRosterElement);
                if (list.Count > 0) _tradeSkips++; else _tradeNone++;
                _guardLines++;
                if (_guardLines <= 20)
                    Log.Info("Surowce rzadkie (m97-surowce): zdarzenie Trade Proposal - wybor " + old + " pominiety, zamiast niego "
                             + (__result.EquipmentElement.Item != null ? __result.EquipmentElement.Item.StringId : "nic (zdarzenie nie wypada)")
                             + " (2 x wartosc zlotem bez platnika - nie za nasz surowiec) | od startu sesji: pominiete " + _tradeSkips + ", bez innego towaru " + _tradeNone + ".");
            }
            catch (Exception e) { Stumble("RareMaterials.TradePost", e); }
        }

        // ------------------------------------------------------------ linia startu
        /// <summary>OnAfterSessionLaunched: surowce (id, cena, waga, kategoria), zrodla, doplyw, luki i zapas.</summary>
        internal static void StartLine()
        {
            try
            {
                _for = null; _stumbles = 0; _badCat = 0;
                _ambushSwaps = _ambushDrop = _tradeSkips = _tradeNone = _guardLines = 0;
                Resolve();
                try { EnsureHooks(); } catch (Exception e) { Stumble("RareMaterials.EnsureHooks", e); }
                var s = S;
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(512);
                sb.Append("Surowce rzadkie (m97-surowce): start sesji | klucze RareBowsNeedMaterial ").Append(s != null && s.RareBowsNeedMaterial ? "TAK" : "NIE")
                  .Append(", RareMaterialSupply ").Append(s != null && s.RareMaterialSupply ? "TAK" : "NIE")
                  .Append(", RareBowsNotForged ").Append(s != null && s.RareBowsNotForged ? "TAK" : "NIE")
                  .Append(", RareMaterialOnlyFromSource ").Append(GuardOn ? "TAK" : "NIE")
                  .Append(" | nagrody gry bez surowca: Caravan Ambush ").Append(_hookAmbush).Append(", Trade Proposal ").Append(_hookTrade)
                  .Append(" | surowce:");
                int found = 0;
                foreach (var m in _mats)
                {
                    if (m.Item == null) { sb.Append(' ').Append(m.Id).Append(_badCat > 0 ? " BRAK (przedmiot bez kategorii arm_rare_stuff - wylaczony);" : " BRAK (plik arm_items_rare.xml nie wgrany?);"); continue; }
                    found++;
                    var cat = m.Item.ItemCategory;
                    sb.Append(' ').Append(m.Id).Append(" (").Append(m.Item.Name).Append(") cena ").Append(m.Item.Value).Append(" d, waga ")
                      .Append(m.Item.Weight.ToString("0.0", inv)).Append(" kg, kategoria ").Append(cat != null ? cat.StringId : "BRAK")
                      .Append(" (towar handlowy ").Append(cat != null && cat.IsTradeGood ? "TAK" : "NIE").Append(", popyt bazowy ")
                      .Append(cat != null ? cat.BaseDemand.ToString("0.000", inv) : "?").Append(");");
                }
                sb.Append(" istnieje ").Append(found).Append(" z ").Append(_mats.Length).Append(" | zrodla:");
                foreach (var m in _mats)
                {
                    sb.Append(' ').Append(m.Name).Append(' ').Append(m.Found.Count).Append(" z ").Append(m.Sources.Length).Append(" osad [");
                    for (int i = 0; i < m.Found.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(m.Found[i].Name).Append(m.Found[i].IsVillage ? " (wies)" : m.Found[i].IsTown ? " (miasto)" : ""); }
                    sb.Append("];");
                }
                sb.Append(" | doplyw dobowy ").Append(SupplyOn ? "TAK" : "NIE").Append(':');
                foreach (var m in _mats)
                    sb.Append(' ').Append(m.Name).Append(" 1 na ").Append((1f / m.PerDay).ToString("0", inv)).Append(" dob (pulap regionu ").Append(m.RegionCap)
                      .Append(", swiata ").Append(m.WorldCap).Append(");");
                sb.Append(" | luki z listy:");
                int bows = 0;
                if (s != null && !string.IsNullOrEmpty(s.RareBowsIds))
                    foreach (var raw in s.RareBowsIds.Split(','))
                    {
                        var id = raw.Trim();
                        if (id.Length == 0) continue;
                        ItemObject bow = null;
                        try { bow = MBObjectManager.Instance.GetObject<ItemObject>(id); } catch { }
                        if (bow == null) { sb.Append(' ').Append(id).Append(" BRAK w grze;"); continue; }
                        bows++;
                        var m = MatFor(bow);
                        if (Covers(bow)) sb.Append(' ').Append(id).Append(" <- ").Append(PiecesFor(bow, m)).Append(" x ").Append(m.Id).Append(" (luk ")
                                              .Append(bow.Weight.ToString("0.00", inv)).Append(" kg, t").Append(Recipes.Grade(bow)).Append(");");
                        else sb.Append(' ').Append(id).Append(" ZABLOKOWANY (").Append(!On ? "klucz" : m == null ? "brak surowca dla tego luku" : "surowiec nie w grze").Append(");");
                    }
                sb.Append(" | zapas teraz:");
                foreach (var m in _mats)
                {
                    if (m.Item == null) continue;
                    m.LastRegion = RegionStock(m); m.LastWorld = WorldStock(m);
                    sb.Append(' ').Append(m.Name).Append(" region ").Append(m.LastRegion).Append(", swiat ").Append(m.LastWorld).Append(", sakwy gracza ").Append(InBags(m.Item)).Append(';');
                }
                sb.Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("RareMaterials.StartLine", e); }
        }
    }
}
