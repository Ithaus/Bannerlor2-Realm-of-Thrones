using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// ZELAZNE WYSPY Z RABUNKU, WOLNI LUDZIE BEZ ZOLDU (decyzja Jeffa 14.10; docs/wyspy-wolni/PROJEKT-WYSPY-WOLNI.md) - czesc wspolna najezdzcow.
    /// Najezdzcy nowych regul: krolestwa ReaverRealms = Zelazne Wyspy ("sturgia") i Wolni Ludzie ("freefolk"); Dothrakowie ("khuzait") poza regulami
    /// (Jeff o nich nie decydowal - zostaja na W-1, lup w D).
    ///  - WW0 (bez klucza, sam pomiar): postfiks Low na CampaignEventDispatcher.AiHourlyTick (ten sam punkt co H1 Strazy - WatchLaw.AiTickPostfix, inna frakcja)
    ///    liczy dla partii lordow AI najezdzcow decyzje AI: ile mysli z opcja rabunku, ile opcji, najlepszy wynik rabunku wobec najlepszego wyniku innej opcji,
    ///    ile razy rabunek byl na gorze listy; raz na dobe stan partii (armia, oblezenie, rabunek, obrona, postoj), dni jedzenia (prog gry
    ///    NeededFoodsInDaysThresholdForRaid czytany z modelu) i wrogowie w wojnie. Raz na sesje progi zasiegu najazdu (5 x srednia odleglosc dwoch najblizszych
    ///    miast - AiMilitaryBehavior.GetDistanceScoreForRaiding) i odleglosci Pyke -> Flint's Finger, Seagard, Lannisport, Greenshield (Wyspy Tarczowe).
    ///    Linie: "Najezdzcy - decyzje AI (WW0)" (doba) i "Najezdzcy - zasieg (WW0)" (raz na sesje). Po co: dlaczego Wyspy po d405 i Wolni Ludzie po Q1 nie
    ///    rabuja - przyczyn nie zakladamy, poprawka dopiero po tescie.
    ///  - WW1 (ReaverIronPrice): zelazna cena - udzial lupu najezdzcow max(s, Q x postep kroku) z kiesy i magazynu wsi (DistrictRaid.Loot, osobny przelew ponad s
    ///    tym samym platnikiem SmallGold.PayRaidLoot); z morza wsi portu - Qw nadwyzki kiesy W portu (Waterfront, TownFolk.WaterfrontRoom/LevyTake). Linia "Zelazna cena (WW1)".
    ///  - WW2 (ReaverLootToCrews): lup ponad s, nabrzeze i dziesiecina korsarzy -> okno 28 dob rodu (NoteLoot/Roll/Window) -> ClanBudget podnosi pulap druzyn 1:1,
    ///    ksiega dochodu poza D (ClanIncomeBook KReaver/QReaver). Kolumna lup_na_zaloge_ww2.
    ///  - WW3 (ReaverRaidByProfit): opcje rabunku lordow AI x M = 1 + min(2, E / (C x T)) w tym samym postfiksie co WW0 (Profit).
    ///  - WW4 (SeaRaidersIronHarbour): porty Wysp jako dom band Sea Raiders u pasera i w zyciu (OutlawLaw), dziesiecina panu portu (HarbourTitheOf/PayHarbourTithe).
    /// WL1-WL3 (Wolni Ludzie) - CrownGifts.IsUnpaidParty, ClanBudget (pula _ff, RecruitPrefix, GarRecruitPrefix). Projekt: docs/wyspy-wolni/PROJEKT-WYSPY-WOLNI.md.
    /// Wyjatek per partia - licznik potkniec (bez globalnych wylacznikow, CLAUDE.md 7). Stan tylko w pamieci sesji; Reset w konstruktorze ArmouryBehavior.
    /// </summary>
    internal static class ReaverLaw
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string IronId = "sturgia", FreeId = "freefolk";
        private const int GIron = 0, GFree = 1, Groups = 2;
        private static readonly string[] GName = { "Iron Islands", "Free Folk" };

        /// <summary>Grupa najezdzcow frakcji: 0 Zelazne Wyspy, 1 Wolni Ludzie, -1 inna (takze Dothrakowie).</summary>
        internal static int GroupOf(IFaction f)
        {
            if (f == null || f.StringId == null) return -1;
            if (f.StringId == IronId) return GIron;
            if (f.StringId == FreeId) return GFree;
            return -1;
        }

        internal static bool IsReaver(IFaction f) { return GroupOf(f) >= 0; }
        internal static bool IsFreeFolk(IFaction f) { return f != null && f.StringId == FreeId; }

        // ---------------------------------------------------------------- WW0: liczniki decyzji AI (od ostatniej linii doby)
        private sealed class Think
        {
            public int Thinks, WithRaid, Options, RaidTop, RaidTop0, Multiplied, X3, Burned;   // RaidTop0 - rabunek na gorze przed WW3 (wyniki gry); Burned - opcje z okregiem wypalonym (E = 0)
            public readonly List<float> Mult = new List<float>();                       // WW3: mnozniki opcji
            public readonly HashSet<MobileParty> PartiesWithRaid = new HashSet<MobileParty>();
            public readonly HashSet<Settlement> Villages = new HashSet<Settlement>();
            public readonly List<float> RaidBest = new List<float>(), OtherBest = new List<float>();
            public void Clear() { Thinks = WithRaid = Options = RaidTop = RaidTop0 = Multiplied = X3 = Burned = 0; PartiesWithRaid.Clear(); Villages.Clear(); RaidBest.Clear(); OtherBest.Clear(); Mult.Clear(); }
        }
        private static readonly Think[] _think = { new Think(), new Think() };
        private const int MaxSamples = 4096;

        private static bool _patched, _rangeLogged;
        private static string _missing;
        private static MethodBase _target;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // ---------------------------------------------------------------- WW1: zelazna cena (liczniki od ostatniej linii doby; licza zawsze - takze przy kluczu off)
        private sealed class Iron
        {
            public int Steps, Sea, Port;                    // kroki rabunku (u AI krok = sesja) z lupem, z nich z morza i przy wsi portu (recenzja R1-1: kontrolka wykrycia morza)
            public long GoldS, GoldExtra, Folk, Goods, GoodsValue;
            public int FolkPorts, FolkBelow, VillageNeg; public double Bal, Captives;
            public readonly HashSet<Settlement> Ports = new HashSet<Settlement>();
            public readonly Dictionary<string, long[]> Victims = new Dictionary<string, long[]>();   // krolestwo ofiary -> [krokow, zloto]
            public void Clear() { Steps = Sea = Port = 0; GoldS = GoldExtra = Folk = Goods = GoodsValue = 0; FolkPorts = FolkBelow = VillageNeg = 0; Bal = Captives = 0; Ports.Clear(); Victims.Clear(); }
        }
        private static readonly Iron[] _iron = { new Iron(), new Iron() };
        /// <summary>WW1 (Obieg): kiesy ludu nabrzeza -> wodzowie najezdzcow od ostatniej linii doby (MoneyLedger.Daily biegnie przed ReaverLaw.Daily).</summary>
        internal static long DayWaterfront { get { return _iron[GIron].Folk + _iron[GFree].Folk; } }

        internal static void Reset()
        {
            for (int g = 0; g < Groups; g++) { _think[g].Clear(); _iron[g].Clear(); }
            _loot.Clear(); _dLootIn = 0; _lastLootIn = 0;   // WW2: okno lupu tylko w pamieci sesji
            _eCache.Clear(); _done.Clear(); _copy.Clear();   // WW3
            _harb.Clear(); _harbDay = int.MinValue;          // WW4
            _rangeLogged = false; _stumbles = 0;
            lock (_err) _err.Clear();
        }

        // ---------------------------------------------------------------- WW1: zelazna cena (DistrictRaid.Loot)
        private static Settings S { get { return Settings.Current; } }
        /// <summary>WW1 czynne (klucz ReaverIronPrice).</summary>
        internal static bool IronPriceOn { get { var s = S; return s != null && s.ReaverIronPrice; } }
        internal static double Q { get { var s = S; return s == null ? 0.0 : Math.Max(0f, Math.Min(1f, s.ReaverIronPriceShare)); } }
        internal static double Qw { get { var s = S; return s == null ? 0.0 : Math.Max(0f, Math.Min(1f, s.ReaverWaterfrontShare)); } }

        /// <summary>Grupa najezdzcow wodza rabunku (strona napastnika: partia wodza - AI albo gracz jako wasal); -1 - nie najezdzca regul WW.</summary>
        internal static int RaidGroup(PartyBase leaderParty)
        {
            try { return leaderParty != null ? GroupOf(leaderParty.MapFaction) : -1; }
            catch { return -1; }
        }

        /// <summary>WW1: udzial lupu kroku - max(s, Q x u) dla najezdzcow przy kluczu; inaczej s (stan 63aaaedf). u - postep kroku (1 po pelnym kroku).</summary>
        internal static double IronShare(int group, double s, double u)
        {
            if (group < 0 || !IronPriceOn) return s;
            double q = Q * Math.Max(0.0, Math.Min(1.0, u));
            return q > s ? q : s;
        }

        /// <summary>WW1: lup zlota kroku z kiesy wsi (paid - udzial s, extra - przyrost zelaznej ceny ponad s; bal - bilans v2 tej wyplaty: zloto wodza - kiesa wsi).</summary>
        internal static void NoteVillageGold(int group, Hero leader, Village v, long paid, long extra, double bal)
        {
            try
            {
                if (group < 0) return;
                var x = _iron[group];
                x.Steps++; x.GoldS += paid; x.GoldExtra += extra; x.Bal += bal;
                if (v != null && v.Gold < 0) x.VillageNeg++;
                string vk = v != null && v.Settlement != null && v.Settlement.MapFaction != null ? v.Settlement.MapFaction.Name.ToString() : "-";
                long[] a; if (!x.Victims.TryGetValue(vk, out a)) { a = new long[2]; x.Victims[vk] = a; }
                a[0]++; a[1] += paid + extra;
            }
            catch (Exception e) { Stumble("NoteVillageGold", e); }
        }

        /// <summary>WW1: przyrost zelaznej ceny ponad s z kiesy wsi zaplacony wodzowi (przelew gry - ksiega dochodu widzi go jako zdarzenie od osady).
        /// WW2: do kosza lupu na zalogi rodu wodza (znacznik w ksiedze dochodu - przy kluczu WW2 poza jednorazowymi i D) i do okna 28 dob.</summary>
        internal static void NoteIronGold(Hero leader, long amount)
        {
            if (leader == null || amount <= 0) return;
            try { ClanIncomeBook.NoteReaverEvt(leader, amount); NoteLoot(leader.Clan, amount); }
            catch (Exception e) { Stumble("NoteIronGold", e); }
        }

        // ---------------------------------------------------------------- WW2: lup na zalogi okretow (okno 28 dob na rod, tylko w pamieci sesji)
        /// <summary>WW2 czynne: klucz ReaverLootToCrews, wymaga WW1 (zelazna cena), budzetu rodow 166 i D stalego (recenzja R1-8: ClanBudgetStableD i ksiega
        /// StableDOn - bez nich D budzetu to srednia CALEGO wplywu ksiegi razem z lupem, a okno dodaloby go drugi raz).</summary>
        internal static bool LootToCrewsOn { get { var s = S; return s != null && s.ReaverLootToCrews && s.ReaverIronPrice && s.ClanBudgetEnabled && s.ClanBudgetStableD && ClanIncomeBook.StableDOn; } }
        private const int LootDays = 28;   // to samo okno co D staly 169c: lup jednym kawalkiem rozlozony na miesiac zoldu

        /// <summary>WW2 (recenzja R1-5 / R2-6): rod, ktorego lup idzie do okna na zalogi - najezdzca regul WW (Wyspy, Wolni Ludzie), nie kompania najemna i nie rod
        /// bez zoldu (b.Zero: Wolni Ludzie przy WL1). Ta sama miara w budzecie (lootNow, gracz W5/W6), w ksiedze (StableDWithDays, OnceToday) i w NoteLoot -
        /// lup pozostalych rodow (WL przy WL1, najemnicy Wysp) zostaje w jednorazowych i idzie do D jak w 63aaaedf (W-1 "najezdzcy - lup w D").</summary>
        internal static bool WindowClan(Clan c)
        {
            try
            {
                var s = S;
                if (c == null || !LootToCrewsOn || !IsReaver(c.Kingdom) || c.IsUnderMercenaryService) return false;
                return !(s.UnpaidTroopsCapInMen && CrownGifts.IsUnpaidClan(c));
            }
            catch { return false; }
        }

        /// <summary>Recenzja R1-7: lup okna rodu jeszcze nierozpisany na zold (wpis sprzed j dob rozpisany w j/28 - zostaje (28 - j)/28; najnowszy wpis j = 1).
        /// ClanBudget odejmuje go od G w pulapie (skrzynia wojenna, f biedy), w skrzyni sprzetu i nadwyzce pokoju - ten sam denar nie podnosi pulapu dwa razy.</summary>
        internal static double Unreleased(Clan c)
        {
            LootRing r;
            if (c == null || !_loot.TryGetValue(c, out r)) return 0.0;
            double u = 0;
            for (int k = 1; k < LootDays; k++) u += Math.Max(0L, r.V[(r.Head + k) % LootDays]) * (double)k / LootDays;   // k = 0 - najstarszy (rozpisany do konca)
            return u;
        }

        private sealed class LootRing
        {
            public readonly long[] V = new long[LootDays];
            public int Head; public long Today, Sum;
            public void Push() { Sum += Today - V[Head]; V[Head] = Today; Head = (Head + 1) % LootDays; Today = 0; }
        }
        private static readonly Dictionary<Clan, LootRing> _loot = new Dictionary<Clan, LootRing>();
        private static long _dLootIn;           // lup do kosza WW2 od ostatniego Roll (linia 166)

        /// <summary>WW2: zloto lupu do kosza rodu (zelazna cena ponad s, nabrzeze, dziesiecina korsarzy) - okno 28 dob.</summary>
        internal static void NoteLoot(Clan c, long amount)
        {
            if (c == null || amount <= 0 || !WindowClan(c)) return;   // recenzja: tylko rody z oknem (reszta - jednorazowe, D)
            try
            {
                LootRing r;
                if (!_loot.TryGetValue(c, out r)) { r = new LootRing(); _loot[c] = r; }
                r.Today += amount; _dLootIn += amount;
            }
            catch (Exception e) { Stumble("NoteLoot", e); }
        }

        /// <summary>WW2: koniec doby kosza (ArmouryBehavior - przed ClanBudget.Daily): dzisiejszy lup do pierscienia; pierscienie bez lupu przez 28 dob znikaja.</summary>
        internal static long Roll()
        {
            long d = _dLootIn; _dLootIn = 0;
            try
            {
                List<Clan> drop = null;
                foreach (var kv in _loot)
                {
                    kv.Value.Push();
                    if (kv.Value.Sum <= 0 || kv.Key == null || kv.Key.IsEliminated) (drop ?? (drop = new List<Clan>())).Add(kv.Key);
                }
                if (drop != null) foreach (var c in drop) _loot.Remove(c);
            }
            catch (Exception e) { Stumble("Roll", e); }
            _lastLootIn = d;
            return d;
        }
        private static long _lastLootIn;
        /// <summary>WW2: lup do kosza w dobie przed ostatnim Roll (linia 166).</summary>
        internal static long LastLootIn { get { return _lastLootIn; } }

        /// <summary>WW2: kolumna budzet-rodow.csv (ostatnia - czytniki po nazwach): lup na zalogi w pulapie rodu tej doby (zl/d, 0 - bez lupu albo bez budzetu).</summary>
        internal const string LootCsvHeader = ";lup_na_zaloge_ww2";
        internal static string LootCsv(Clan c)
        {
            try { return ";" + Math.Round(ClanBudget.LootOf(c)).ToString("0", Inv); }
            catch { return ";"; }
        }

        /// <summary>WW2: lup rodu na dobe - suma 28 dob / 28 (po wczytaniu okno rosnie od 0 - bez pola zapisu). 0 - brak lupu.</summary>
        internal static double Window(Clan c)
        {
            LootRing r;
            return c != null && _loot.TryGetValue(c, out r) ? Math.Max(0L, r.Sum) / (double)LootDays : 0.0;
        }

        /// <summary>WW1: krok rabunku najezdzcy z morza (zdarzenie morskie).</summary>
        internal static void NoteSea(int group) { if (group >= 0) _iron[group].Sea++; }

        /// <summary>Recenzja R1-1: pierwszy krok sesji najezdzcy na wies, ktorej osada macierzysta ma port (mianownik kontrolki "z morza").</summary>
        internal static void NotePort(int group) { if (group >= 0) _iron[group].Port++; }

        /// <summary>Recenzja R1-1: rabunek z morza - tak jak gra i NavalDLC (MapEventHelper.GetRaidContext "raiderHasSeaPresence", NavalDLCRaidModel): choc jedna
        /// partia strony napastnika ze zdrowymi ludzmi jest na morzu (MobileParty.IsCurrentlyAtSea). MapEvent.IsNavalMapEvent dla rabunku jest zawsze false
        /// (pozycja zdarzenia = pozycja wsi, a ta jest na ladzie - Settlement.Deserialize isOnLand: true).</summary>
        internal static bool RaiderAtSea(TaleWorlds.CampaignSystem.MapEvents.MapEventSide att)
        {
            try
            {
                if (att == null || att.Parties == null) return false;
                foreach (var mep in att.Parties)
                {
                    var pb = mep != null ? mep.Party : null;
                    var mp = pb != null ? pb.MobileParty : null;
                    if (mp != null && mp.IsCurrentlyAtSea && pb.NumberOfHealthyMembers > 0) return true;
                }
            }
            catch (Exception e) { Stumble("RaiderAtSea", e); }
            return false;
        }

        /// <summary>WW1: towar z magazynu wsi w kroku najezdzcy (sztuki i wartosc wedlug cen bazowych).</summary>
        internal static void NoteGoods(int group, long pieces, long value) { if (group >= 0) { _iron[group].Goods += pieces; _iron[group].GoodsValue += value; } }

        /// <summary>WW1: jency z sesji najezdzcy (DistrictRaid.EndFin - jency E4/9 przy koncu rabunku).</summary>
        internal static void NoteCaptives(int group, double n) { if (group >= 0 && n > 0) _iron[group].Captives += n; }

        /// <summary>
        /// WW1 - nabrzeze: rabunek z morza (RaiderAtSea - pierwszy krok sesji; recenzja R2-3: raz na sesje, jak zelazna cena) wsi, ktorej osada macierzysta ma port - Qw x u kiesy klasy W tej osady ponad zapas dni i oferte na jutro
        /// (TownFolk.WaterfrontRoom) do wodza najezdzcy: kiesa ludu -> glowa (TownFolk.LevyTake - ksiega kies _ctlOut; ChangeHeroGold, wyjatek - zloto wraca do kiesy).
        /// Zwraca wziete (0 - nic). Tylko przy kluczu WW1, dla najezdzcow, z wodzem.
        /// </summary>
        internal static long Waterfront(int group, Hero leader, Village v, double u)
        {
            if (group < 0 || leader == null || v == null || !IronPriceOn) return 0;
            try
            {
                var port = v.Bound;
                if (port == null || !port.HasPort) return 0;
                double qw = Qw * Math.Max(0.0, Math.Min(1.0, u));
                if (!(qw > 0.0)) return 0;
                long room = TownFolk.WaterfrontRoom(port);
                if (room <= 0) return 0;
                long want = (long)Math.Floor(qw * room);
                if (want <= 0) return 0;
                long took = TownFolk.LevyTake(port, 0, Math.Min(want, int.MaxValue));
                if (took <= 0) return 0;
                long h0 = leader.Gold;
                try { leader.ChangeHeroGold((int)took); }
                catch { TownFolk.LevyBack(port, 0, took); throw; }
                CirculationWindows.NoteHeroGold(leader, (int)took);   // paczka 169b: glowa poza swiatem - zloto wyszlo ze swiata (tylko licznik)
                // WW2: wplyw rodu wodza bez zdarzenia gry - do ksiegi dochodu (kosz KReaver: przy kluczu WW2 poza D, bez klucza - jednorazowe jak caly lup) i okna
                ClanIncomeBook.NoteInflow(leader, (int)took, ClanIncomeBook.KReaver);
                NoteLoot(leader.Clan, took);
                var x = _iron[group];
                x.Folk += took; x.Ports.Add(port); x.FolkPorts++;
                x.Bal += (leader.Gold - h0) - took;
                if (TownFolk.LevyBelowReserve(port, 0)) x.FolkBelow++;
                return took;
            }
            catch (Exception e) { Stumble("Waterfront", e); return 0; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("ReaverLaw." + where, e); } catch { } }
        }

        // ---------------------------------------------------------------- wpiecie (SubModuleMain - po WatchLaw.ApplyAll; klasa gry bez pol statycznych z tekstow)
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _patched) return;
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _target = m;
                if (m == null) { _missing = "CampaignEventDispatcher.AiHourlyTick"; return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(ReaverLaw), nameof(AiTickPostfix)) { priority = Priority.Low });
                _patched = true;
            }
            catch (Exception e) { _missing = "CampaignEventDispatcher.AiHourlyTick (" + e.GetType().Name + ")"; Log.Error("ReaverLaw.ApplyAll", e); }
            Log.Info("Najezdzcy (WW0): postfiks decyzji AI (CampaignEventDispatcher.AiHourlyTick, Low) " + (_patched ? "WPIETY" : "BRAK (" + (_missing ?? "?") + ")") + ".");
        }

        /// <summary>
        /// Postfiks Low (po wszystkich odbiorcach zdarzenia - opcje gry, BK, ROT, NavalDLC sa juz na liscie): partie lordow AI najezdzcow. WW0 - liczy surowe
        /// wyniki gry; WW3 (przy kluczu) - potem mnozy opcje rabunku (Profit).
        /// </summary>
        public static void AiTickPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            try
            {
                if (!party.IsLordParty || party.IsMainParty) return;
                int g = GroupOf(party.MapFaction);
                if (g < 0) return;
                var list = partyThinkParams.AIBehaviorScores;
                var t = _think[g];
                t.Thinks++;
                if (list == null || list.Count == 0) return;
                float bestRaid = float.MinValue, bestOther = float.MinValue;
                int nRaid = 0;
                foreach (var it in list)
                {
                    if (it.Item1.AiBehavior == AiBehavior.RaidSettlement)
                    {
                        nRaid++;
                        if (it.Item2 > bestRaid) bestRaid = it.Item2;
                        var st = it.Item1.Party as Settlement;
                        if (st != null && t.Villages.Count < MaxSamples) t.Villages.Add(st);
                    }
                    else if (it.Item2 > bestOther) bestOther = it.Item2;
                }
                if (nRaid <= 0) return;
                t.WithRaid++; t.Options += nRaid; t.PartiesWithRaid.Add(party);
                if (t.RaidBest.Count < MaxSamples) { t.RaidBest.Add(bestRaid); t.OtherBest.Add(bestOther == float.MinValue ? -1f : bestOther); }
                if (bestRaid > bestOther) t.RaidTop0++;
                // WW3: opcje rabunku z rachunku (po pomiarze WW0 - wyniki gry wyzej sa surowe); "rabunek na gorze" - po mnozniku (to wybiera petla gry)
                if (ProfitOn && (g != GFree || ProfitFreeOn)) bestRaid = Profit(party, partyThinkParams, t, bestRaid);
                if (bestRaid > bestOther) t.RaidTop++;
            }
            catch (Exception e) { Stumble("AiTickPostfix", e); }
        }

        // ---------------------------------------------------------------- WW4: port korsarzy (OutlawLaw - paser, zycie w miastach, dziesiecina)
        internal const string SeaRaidersId = "northern_pirates";   // NavalDLC + ROT spclans "Sea Raiders" (strefy kolo Pyke, Old Wyk, Flint's Finger, Wyspy Niedzwiedziej)
        internal const float HarbourRadius = 600f;                   // Wyspa Niedzwiedzia lezy 501 od Pyke - strefy Sea Raiders koncza sie na niej; dalej port Wysp nie jest "domem"
        /// <summary>WW4 czynne (klucz SeaRaidersIronHarbour).</summary>
        internal static bool HarbourOn { get { var s = S; return s != null && s.SeaRaidersIronHarbour; } }
        internal static double HarbourTithe { get { var s = S; return s == null ? 0.0 : Math.Max(0f, Math.Min(1f, s.SeaRaidersHarbourTithe)); } }

        /// <summary>WW4: banda klanu Sea Raiders.</summary>
        internal static bool IsSeaRaider(MobileParty p)
        {
            try { var c = p != null ? p.ActualClan : null; return c != null && c.StringId == SeaRaidersId; }
            catch { return false; }
        }

        private static readonly List<Settlement> _harb = new List<Settlement>();
        private static int _harbDay = int.MinValue;

        /// <summary>WW4: porty Zelaznych Wysp - miasta krolestwa "sturgia" z portem (lista raz na dobe; oblezone odpadaja przy uzyciu).</summary>
        internal static List<Settlement> Harbours()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _harbDay) return _harb;
            _harbDay = d; _harb.Clear();
            try
            {
                foreach (var st in Settlement.All)
                    if (st != null && st.IsTown && st.Town != null && st.HasPort && st.MapFaction != null && st.MapFaction.StringId == IronId) _harb.Add(st);
            }
            catch (Exception e) { Stumble("Harbours", e); }
            return _harb;
        }

        /// <summary>WW4: najblizszy nieoblezony port Wysp do HarbourRadius od pozycji (linia prosta - ta sama miara co promien pasera); null - brak.</summary>
        internal static Settlement NearestHarbour(Vec2 pos, out float dist)
        {
            dist = float.MaxValue;
            Settlement best = null;
            foreach (var st in Harbours())
            {
                if (st == null || st.IsUnderSiege || st.Town == null) continue;
                float d = (float)Math.Sqrt(pos.DistanceSquared(st.GetPosition2D));
                if (d <= HarbourRadius && d < dist) { dist = d; best = st; }
            }
            return best;
        }

        /// <summary>WW4: dziesiecina z zaplaty pasera portu Wysp dla bandy Sea Raiders - kiesa bandy (czesc zaplaty) -> glowa rodu pana portu (gracz-pan tak samo).
        /// Ksiega dochodu: KReaver (kosz WW2 przy kluczu; bez klucza - jednorazowe), okno lupu WW2. Zwraca dziesiecine (0 - bez pana, klucz off).</summary>
        internal static int HarbourTitheOf(Settlement port, int pay)
        {
            if (port == null || pay <= 0 || !HarbourOn) return 0;
            var owner = port.OwnerClan;
            var head = owner != null ? owner.Leader : null;
            if (head == null || !head.IsAlive) return 0;
            int x = (int)Math.Floor(pay * HarbourTithe);
            return x > 0 ? Math.Min(x, pay) : 0;
        }

        internal static void PayHarbourTithe(Settlement port, int tithe)
        {
            var owner = port != null ? port.OwnerClan : null;
            var head = owner != null ? owner.Leader : null;
            if (head == null || tithe <= 0) return;
            head.ChangeHeroGold(tithe);
            CirculationWindows.NoteHeroGold(head, tithe);   // paczka 169b: glowa poza swiatem - tylko licznik
            try { ClanIncomeBook.NoteInflow(head, tithe, ClanIncomeBook.KReaver); NoteLoot(owner, tithe); } catch (Exception e) { Stumble("PayHarbourTithe(ksiega)", e); }
        }

        // ---------------------------------------------------------------- WW3: najazd z rachunku (AI z biezacego zysku)
        /// <summary>WW3 czynne: klucz ReaverRaidByProfit, wymaga WW1 (E = lup zelaznej ceny).</summary>
        internal static bool ProfitOn { get { var s = S; return s != null && s.ReaverRaidByProfit && s.ReaverIronPrice; } }
        /// <summary>Recenzja R2-4: WW3 takze dla Wolnych Ludzi (klucz ReaverRaidByProfitFreeFolk; off = pierwszy krok wycofania przy Darze - WW3 tylko dla Wysp).</summary>
        internal static bool ProfitFreeOn { get { var s = S; return s != null && s.ReaverRaidByProfitFreeFolk; } }
        private static double PayDays { get { var s = S; return s == null ? 4.0 : Math.Max(1f, Math.Min(28f, s.ReaverRaidPayDays)); } }
        private const double MaxExtra = 2.0;   // M = 1 + min(2, E / (C x T)): najwyzej x3 - AI nie porzuca obrony wlasnych twierdz dla bogatej wsi

        private struct ECache { public int Hour; public double Base; public bool Burned; }
        private static readonly Dictionary<Settlement, ECache> _eCache = new Dictionary<Settlement, ECache>();
        private static readonly HashSet<AIBehaviorData> _done = new HashSet<AIBehaviorData>();
        private static readonly List<(AIBehaviorData, float)> _copy = new List<(AIBehaviorData, float)>(64);

        /// <summary>WW3: kazda opcja RaidSettlement z wynikiem > 0 dostaje wynik x M = 1 + min(2, E / (C x T)); E - lup, ktory WW1 dalby dzis z tej wsi (Q x kiesa
        /// wsi + Q x wartosc magazynu; opcja morzem (NavigationType.Naval) na wies portu - + Qw x nadwyzka nabrzeza; okreg wypalony - 0), C - koszt dnia partii (zold z rycerzami 179 + jedzenie: zuzycie
        /// x cena zboza najblizszego miasta), T = ReaverRaidPayDays. Opcji nie dodaje (wojna, zasieg, 8 dni jedzenia, prog sily - z gry); M >= 1 (nigdy kara);
        /// raz na opcje (lista swieza co mysl - mnoznik nie narasta). Zwraca najlepszy wynik rabunku po mnozniku.</summary>
        private static float Profit(MobileParty party, PartyThinkParams p, Think t, float bestRaid)
        {
            float best = bestRaid;
            try
            {
                double cost = DayCost(party) * PayDays;
                if (!(cost > 1.0)) cost = 1.0;
                _copy.Clear(); _done.Clear();
                foreach (var it in p.AIBehaviorScores) _copy.Add(it);
                best = float.MinValue;
                for (int i = 0; i < _copy.Count; i++)
                {
                    var it = _copy[i];
                    if (it.Item1.AiBehavior != AiBehavior.RaidSettlement) continue;
                    float sc = it.Item2;
                    if (sc > 0f && _done.Add(it.Item1))
                    {
                        var st = it.Item1.Party as Settlement;
                        bool burned;
                        double e = LootEstimate(st, it.Item1.NavigationType, out burned);
                        if (burned) t.Burned++;
                        double m = 1.0 + Math.Min(MaxExtra, Math.Max(0.0, e) / cost);
                        if (m > 1.0 + 1e-6)
                        {
                            var d = it.Item1;
                            float ns = (float)(sc * m);
                            p.SetBehaviorScore(in d, ns);
                            sc = ns;
                            t.Multiplied++;
                            if (m >= 1.0 + MaxExtra - 1e-6) t.X3++;
                        }
                        if (t.Mult.Count < MaxSamples) t.Mult.Add((float)m);
                    }
                    if (sc > best) best = sc;
                }
            }
            catch (Exception e) { Stumble("Profit", e); return bestRaid; }
            return best == float.MinValue ? bestRaid : best;
        }

        /// <summary>WW3: lup zelaznej ceny z wsi teraz (E). Kiesa i magazyn wsi - pamiec na godzine gry (wiele partii mysli o tych samych wsiach).
        /// Recenzja R2-7: okreg wypalony (DistrictRaid.BurnedOutNow - ta sama arytmetyka co Plan kroku: Z = 0 osad w zasiegu) - E = 0, bo krok nic nie spali (u = 0,
        /// lup 0). Recenzja R1-1: nabrzeze tylko dla opcji, ktora prowadzi morzem (NavigationType.Naval - partia dotrze i rabuje z morza), nie wedlug tego, czy
        /// partia jest na morzu w chwili mysli.</summary>
        private static double LootEstimate(Settlement st, MobileParty.NavigationType nav, out bool burned)
        {
            burned = false;
            var v = st != null ? st.Village : null;
            if (v == null) return 0.0;
            int hour = (int)CampaignTime.Now.ToHours;
            ECache c;
            if (!_eCache.TryGetValue(st, out c) || c.Hour != hour)
            {
                double stock = 0;
                var r = st.ItemRoster;
                if (r != null)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        if (el.Amount > 0 && el.EquipmentElement.Item != null) stock += (double)el.Amount * el.EquipmentElement.Item.Value;
                    }
                bool bo = false;
                try { bo = DistrictRaid.BurnedOutNow(v); } catch (Exception ex) { Stumble("LootEstimate(okreg)", ex); }
                c = new ECache { Hour = hour, Base = Math.Max(0, v.Gold) + stock, Burned = bo };
                _eCache[st] = c;
            }
            if (c.Burned) { burned = true; return 0.0; }
            double e = Q * c.Base;
            try
            {
                var port = v.Bound;
                if (nav == MobileParty.NavigationType.Naval && port != null && port.HasPort)
                {
                    long room = TownFolk.WaterfrontRoom(port);
                    if (room > 0) e += Qw * room;
                }
            }
            catch (Exception ex) { Stumble("LootEstimate(nabrzeze)", ex); }
            return e;
        }

        /// <summary>WW3: koszt dnia partii - zold (TotalWage + rycerze 179) + jedzenie (dzienne zuzycie x cena zboza w najblizszym miescie albo wartosc zboza).</summary>
        private static double DayCost(MobileParty mp)
        {
            double wage = Math.Max(0, mp.TotalWage) + GentryService.KnightWage(mp);
            double use = Math.Max(0f, -mp.FoodChange);
            double price = 0;
            var grain = DefaultItems.Grain;
            if (grain != null && use > 0)
            {
                price = grain.Value;
                try
                {
                    var pos = mp.GetPosition2D;
                    Town best = null; float bd = float.MaxValue;
                    foreach (var tw in Town.AllTowns)
                    {
                        if (tw == null || tw.Settlement == null) continue;
                        float d = pos.DistanceSquared(tw.Settlement.GetPosition2D);
                        if (d < bd) { bd = d; best = tw; }
                    }
                    if (best != null) price = best.GetItemPrice(grain, null, false);
                }
                catch { price = grain.Value; }
            }
            return wage + use * Math.Max(0, price);
        }

        /// <summary>Dni jedzenia partii (jak MobileParty.GetNumDaysForFoodToLast, bez dzielenia przez 0) przy `more` ludziach wiecej niz teraz (zuzycie na
        /// czlowieka z FoodChange partii); partia bez zuzycia - 999.</summary>
        internal static float FoodDays(MobileParty mp, int more)
        {
            if (mp == null || mp.ItemRoster == null) return 0f;
            double use = Math.Max(0f, -mp.FoodChange);
            if (!(use > 1e-6)) return 999f;
            if (more > 0)
            {
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                int pris = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
                int all = men + pris;
                if (all > 0) use = use * (all + more) / all;
            }
            return (float)(mp.ItemRoster.TotalFood / use);
        }

        // ---------------------------------------------------------------- linie doby
        private static string F(double v, string f) { return v.ToString(f, Inv); }

        private static double Median(List<float> xs)
        {
            if (xs == null || xs.Count == 0) return double.NaN;
            var a = new List<float>(xs); a.Sort();
            int n = a.Count;
            return (n % 2) == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        /// <summary>ArmouryBehavior.OnDailyTick po DistrictRaid.Daily: linie WW0 (decyzje AI i - raz na sesje - zasieg), zerowanie licznikow doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            try
            {
                if (s == null || Campaign.Current == null || !s.LogEnabled) return;
                if (!_rangeLogged) { _rangeLogged = true; try { RangeLine(); } catch (Exception e) { Stumble("RangeLine", e); } }
                try { DecisionLine(); } catch (Exception e) { Stumble("DecisionLine", e); }
                try { IronLine(); } catch (Exception e) { Stumble("IronLine", e); }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { for (int g = 0; g < Groups; g++) { _think[g].Clear(); _iron[g].Clear(); } }
        }

        // krainy wsi do odcinka "kiesy wsi wedlug krainy" (kultura wsi ROT - CLAUDE.md 2: vlandia = Zachod, battania = Polnoc, nightswatch = Dar)
        private static readonly string[] PurseCultures = { "vlandia", "battania", "nightswatch", "river", "reach" };
        private static readonly string[] PurseNames = { "Zachod", "Polnoc", "Dar", "Dorzecze", "Reach" };

        private static void IronLine()
        {
            var s = S;
            int day = (int)CampaignTime.Now.ToDays - 1;
            var sum = new long[PurseCultures.Length]; var cnt = new int[PurseCultures.Length];
            long all = 0; int allN = 0;
            var seen = new HashSet<Village>();   // Village.All ma duble (B6 pomiar) - kazda wies raz
            foreach (var v in Village.All)
            {
                try
                {
                    var st = v != null ? v.Settlement : null;
                    if (st == null || !seen.Add(v)) continue;
                    long g = v.Gold; all += g; allN++;
                    string c = st.Culture != null ? st.Culture.StringId : null;
                    int i = c != null ? Array.IndexOf(PurseCultures, c) : -1;
                    if (i >= 0) { sum[i] += g; cnt[i]++; }
                }
                catch (Exception e) { Stumble("IronLine(wies)", e); }
            }
            var sb = new StringBuilder(1400);
            sb.Append("Zelazna cena (WW1): dzien ").Append(day).Append(" | klucz Reaver Iron Price ").Append(s != null && s.ReaverIronPrice ? "tak" : "nie (udzial s jak dotad)")
              .Append(", Q ").Append(F(Q, "0.00")).Append(", nabrzeze Qw ").Append(F(Qw, "0.00"));
            long neg = 0, below = 0; double bal = 0;
            for (int g = 0; g < Groups; g++)
            {
                var x = _iron[g];
                neg += x.VillageNeg; below += x.FolkBelow; bal += x.Bal;
                sb.Append(" | ").Append(GName[g]).Append(": krokow rabunku z lupem ").Append(x.Steps).Append(" (z morza ").Append(x.Sea).Append(", przy wsi portu ").Append(x.Port).Append(')')
                  .Append(", zloto z kies wsi ").Append(x.GoldS + x.GoldExtra).Append(" (w tym ponad s: ").Append(x.GoldExtra).Append(')')
                  .Append(", z nabrzeza ").Append(x.Folk).Append(" (wyplat ").Append(x.FolkPorts).Append(", portow ").Append(x.Ports.Count).Append(')')
                  .Append(", towar ").Append(x.Goods).Append(" szt. (").Append(x.GoodsValue).Append(" zl)")
                  .Append(", jency (thralle) ").Append(F(x.Captives, "0.#"))
                  .Append(", ofiary wedlug krolestwa: ");
                if (x.Victims.Count == 0) sb.Append('-');
                else
                {
                    bool first = true;
                    foreach (var kv in x.Victims) { if (!first) sb.Append(", "); first = false; sb.Append(kv.Key).Append(' ').Append(kv.Value[0]).Append('/').Append(kv.Value[1]).Append(" zl"); }
                }
            }
            sb.Append(" | kiesy wsi wedlug krainy (stan, zl / wsi): ");
            for (int i = 0; i < PurseCultures.Length; i++) sb.Append(i > 0 ? ", " : "").Append(PurseNames[i]).Append(" (").Append(PurseCultures[i]).Append(") ").Append(sum[i]).Append('/').Append(cnt[i]);
            sb.Append(", swiat ").Append(all).Append('/').Append(allN)
              .Append(" | kontrolki: kiesa wsi < 0 po wyplacie ").Append(neg).Append(", nabrzeze ponizej zapasu ").Append(below).Append(", bilans v2 (zloto wodza - kiesa wsi - kiesa nabrzeza) ").Append(F(bal, "0"))
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static void DecisionLine()
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            float need = 8f;
            try { need = Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForRaid; } catch { need = 8f; }
            var sb = new StringBuilder(1400);
            sb.Append("Najezdzcy - decyzje AI (WW0): dzien ").Append(day).Append(" | prog jedzenia do najazdu (gra) ").Append(F(need, "0.#")).Append(" dni");
            for (int g = 0; g < Groups; g++)
            {
                int parties = 0, army = 0, siege = 0, raid = 0, defend = 0, hold = 0, hungry = 0;
                var food = new List<float>();
                Kingdom k = null;
                foreach (var kk in Kingdom.All) if (kk != null && !kk.IsEliminated && GroupOf(kk) == g) { k = kk; break; }
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.IsMainParty || GroupOf(mp.MapFaction) != g) continue;
                        parties++;
                        if (mp.Army != null) army++;
                        if (mp.BesiegedSettlement != null || mp.SiegeEvent != null) siege++;
                        else if ((mp.MapEvent != null && mp.MapEvent.IsRaid) || mp.DefaultBehavior == AiBehavior.RaidSettlement) raid++;
                        else if (mp.DefaultBehavior == AiBehavior.DefendSettlement) defend++;
                        else if (mp.DefaultBehavior == AiBehavior.Hold || mp.DefaultBehavior == AiBehavior.None) hold++;
                        float fd = FoodDays(mp, 0);
                        if (food.Count < MaxSamples) food.Add(fd);
                        if (fd < need) hungry++;
                    }
                    catch (Exception e) { Stumble("DecisionLine(partia)", e); }
                }
                food.Sort();
                int enemies = 0;
                try
                {
                    if (k != null && k.FactionsAtWarWith != null)
                        foreach (var f in k.FactionsAtWarWith) if (f != null && f.IsKingdomFaction && f.Leader != null) enemies++;
                }
                catch (Exception e) { Stumble("DecisionLine(wojny)", e); }
                var t = _think[g];
                double mr = Median(t.RaidBest), mo = Median(t.OtherBest);
                sb.Append(" | ").Append(GName[g]).Append(": partii ").Append(parties).Append(" (armia ").Append(army).Append(", oblezenie ").Append(siege)
                  .Append(", rabunek ").Append(raid).Append(", obrona ").Append(defend).Append(", postoj ").Append(hold).Append(')')
                  .Append(", jedzenie min/med ").Append(food.Count > 0 ? F(food[0], "0") : "-").Append('/').Append(food.Count > 0 ? F(food[food.Count / 2], "0") : "-")
                  .Append(" dni (< prog: ").Append(hungry).Append(')')
                  .Append(", mysli ").Append(t.Thinks).Append(", z opcja rabunku ").Append(t.WithRaid).Append(" (partii ").Append(t.PartiesWithRaid.Count)
                  .Append(", opcji ").Append(t.Options).Append(", wsi ").Append(t.Villages.Count).Append(')')
                  .Append(", wynik rabunku/innej (mediana) ").Append(double.IsNaN(mr) ? "-" : F(mr, "0.000")).Append('/').Append(double.IsNaN(mo) ? "-" : F(mo, "0.000"))
                  .Append(", rabunek na gorze ").Append(t.RaidTop).Append(" (przed WW3 ").Append(t.RaidTop0).Append(')')
                  .Append("; wrogow w wojnie ").Append(enemies);
                double mm = Median(t.Mult);
                sb.Append(" | WW3 (").Append(GName[g]).Append(", Reaver Raid By Profit ").Append(ProfitOn && (g != GFree || ProfitFreeOn) ? "tak" : "nie").Append("): mnoznik mediana ").Append(double.IsNaN(mm) ? "-" : F(mm, "0.00"))
                  .Append(", x3 ").Append(t.X3).Append(", opcji pomnozonych ").Append(t.Multiplied).Append(" z ").Append(t.Mult.Count).Append(", okreg wypalony (E 0) ").Append(t.Burned);
            }
            sb.Append(" | postfiks ").Append(_patched ? "wpiety" : "BRAK").Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static string Dist(Settlement a, Settlement b)
        {
            try
            {
                if (a == null || b == null) return "-";
                float d = Campaign.Current.Models.MapDistanceModel.GetDistance(a, b, a.HasPort, b.HasPort, MobileParty.NavigationType.All);
                return d >= 0f && d < 1e7f ? F(d, "0") : "-";
            }
            catch { return "?"; }
        }

        private static void RangeLine()
        {
            var c = Campaign.Current;
            float dd = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
            float dn = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Naval);
            float da = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
            var pyke = Settlement.Find("town_S3");
            string[] ids = { "ROT_town34", "ROT_town33", "town_V9", "ROT_town27_village2" };
            var sb = new StringBuilder(400);
            sb.Append("Najezdzcy - zasieg (WW0): srednia odleglosc dwoch najblizszych miast Default/Naval/All ").Append(F(dd, "0.0")).Append('/').Append(F(dn, "0.0")).Append('/').Append(F(da, "0.0"))
              .Append(", prog najazdu (5x) ").Append(F(5 * dd, "0")).Append('/').Append(F(5 * dn, "0")).Append('/').Append(F(5 * da, "0"))
              .Append(" | ").Append(pyke != null ? pyke.Name.ToString() : "Pyke (brak town_S3)").Append(" -> ");
            for (int i = 0; i < ids.Length; i++)
            {
                var st = Settlement.Find(ids[i]);
                if (i > 0) sb.Append(", ");
                sb.Append(st != null ? st.Name.ToString() : ids[i]).Append(' ').Append(Dist(pyke, st));
            }
            sb.Append(" (droga MapDistanceModel, All).");
            Log.Info(sb.ToString());
        }
    }
}
