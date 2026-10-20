using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// R3-02 (pomysly 35, Jeff 20.10 "tak"; projekt docs/PROJEKT-KOSZT-NAPRAWY-2026-10-07.md, pkt 5, 6, 9 i tabela godzin). CENA NAPRAWY BEZ ZMIAN
    /// (decyzja Jeffa 07.10 "nasza robocizna jest lepsza") - zmieniaja sie tylko godziny, rece i rabat:
    ///  1) MendHoursByGuild: sztuka zajmuje tyle roboczogodzin, ile naprawde trwa jej naprawa: udzial (rodzaj, ubytek) z tabeli projektu x dni
    ///     wykonania sztuki (HistoricalPrices.HistDays - te same, co w cenie nowej sztuki) x WorkHoursPerManDay. Robi ja wlasciwy cech miasta:
    ///     kowale (platnerze + miecznicy, SmithHours - jak dotad), siodlarze (skora, siodla), krawcy (sukno, przeszywanice, plaszcze), lukarze
    ///     (luki, kusze), tarczownicy (tarcze). Rece cechu = rece rzemieslnikow miasta x waga cechu (WorkshopLaw.GuildWeight - ta sama co
    ///     w warsztatach). Godziny zuzyte wczoraj na naprawy odchodza dzis od linii tego cechu w warsztacie zbrojnym (WorkshopLaw - jak dla kowali).
    ///     Dotad kazda sztuka 0.6 h (MendLootHoursPerPiece) i wszystko u kowali.
    ///  2) AutoMendHalfGuild: naprawy automatyczne (lordowie AI + sakiewka Twoich ludzi) biora razem najwyzej polowe dziennych godzin kazdego cechu
    ///     w miescie - warsztaty zawsze maja co najmniej pol rak. Zlecenia gracza (lawa, sztuka, polki) moga brac wszystko, co wolne.
    ///  3) MendBulkEveryOrder: rabat hurtowy z polek wojska (TroopMendBulkDiscountPP/Max: 0.5% za sztuke, do 30%) przy kazdym zleceniu - od robocizny,
    ///     nie od materialu i nie od godzin. Zlecenie: jedno klikniecie gracza; jeden dzien lorda w miescie; u Twoich ludzi - sztuki czekajace rano.
    ///     Zamknieta ekonomia: placacy placi mniej i kasa miasta dostaje mniej o te sama kwote.
    /// Wylaczone klucze - jak dotad co do godziny i pensa. Stan tylko dzienny (pule jak SmithHours) - nic do zapisu.
    /// </summary>
    internal static class GuildMend
    {
        internal const int Smith = 0, Saddler = 1, Tailor = 2, Bowyer = 3, Shieldmaker = 4, Trades = 5;
        private static readonly string[] Guild = { null, "siodlarz", "krawiec", "lucznik", "tarczownik" };
        internal static readonly string[] TradePl = { "kowale", "siodlarze", "krawcy", "lukarze", "tarczownicy" };
        internal static readonly string[] TradeEn = { "smiths", "saddlers", "tailors", "bowyers", "shieldmakers" };

        internal static bool On { get { var s = Settings.Current; return s != null && s.MendHoursByGuild; } }
        internal static bool HalfOn { get { var s = Settings.Current; return s != null && s.MendHoursByGuild && s.AutoMendHalfGuild; } }
        internal static bool BulkOn { get { var s = Settings.Current; return s != null && s.MendBulkEveryOrder; } }
        private const float AutoShare = 0.5f;   // projekt pkt 9 [K]: AI + ludzie razem najwyzej polowa rak cechu

        // ------------------------------------------------------------ tabela udzialow dni wykonania (projekt, "Tabela godzin")
        // kotwice ubytku 0.45 (Plundered) / 0.60 (Damaged) / 0.75 (Battered) / 0.90 (Mangled, RBM 0.1); liniowo od 0, powyzej 0.9 - wrak
        private const int KBlade = 0, KPole = 1, KBow = 2, KMail = 3, KPlate = 4, KHelm = 5, KLeather = 6, KCloth = 7, KShield = 8;
        private static readonly float[][] Pts =
        {
            new[] { 0.025f, 0.06f, 0.15f, 0.40f },   // ostrze: miecz, topor, buzdygan, dwureczne (kowale)
            new[] { 0.03f, 0.08f, 0.20f, 0.50f },    // drzewcowa i miotana (kowale)
            new[] { 0.02f, 0.05f, 0.12f, 0.60f },    // luk, kusza (lukarze)
            new[] { 0.01f, 0.03f, 0.08f, 0.30f },    // kolczuga: koszula, kaptur, nogawice (kowale)
            new[] { 0.007f, 0.025f, 0.06f, 0.30f },  // plyta i brygantyna, kropierz metalowy (kowale)
            new[] { 0.02f, 0.05f, 0.15f, 0.40f },    // helm plytowy (kowale)
            new[] { 0.08f, 0.18f, 0.35f, 0.60f },    // skora: buty, rekawice, kaftany, siodla (siodlarze)
            new[] { 0.04f, 0.10f, 0.30f, 0.60f },    // sukno: przeszywanica, plaszcze (krawcy)
            new[] { 0.05f, 0.15f, 0.35f, 0.70f },    // tarcza (tarczownicy)
        };

        /// <summary>Rodzaj sztuki w tabeli i cech, ktory ja naprawia; -1 = poza tabela (amunicja, sztandar) - stala liczba godzin u kowali.</summary>
        private static int KindOf(ItemObject it, out int trade)
        {
            trade = Smith;
            if (it == null) return -1;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon: return KBlade;
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Thrown: return KPole;
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow: trade = Bowyer; return KBow;
                case ItemObject.ItemTypeEnum.Shield: trade = Shieldmaker; return KShield;
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    {
                        var mat = it.ArmorComponent != null ? it.ArmorComponent.MaterialType : ArmorComponent.ArmorMaterialTypes.None;
                        if (mat == ArmorComponent.ArmorMaterialTypes.Plate) return it.ItemType == ItemObject.ItemTypeEnum.HeadArmor ? KHelm : KPlate;
                        if (mat == ArmorComponent.ArmorMaterialTypes.Chainmail) return KMail;
                        if (mat == ArmorComponent.ArmorMaterialTypes.Leather || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) { trade = Saddler; return KLeather; }   // siodla bez materialu - siodlarze
                        trade = Tailor; return KCloth;
                    }
                default: return -1;
            }
        }

        internal static int TradeOf(ItemObject it) { int t; KindOf(it, out t); return t; }

        private static float ShareOf(int kind, float loss)
        {
            var p = Pts[kind];
            if (loss <= 0f) return 0f;
            if (loss < 0.45f) return p[0] * loss / 0.45f;
            if (loss < 0.60f) return p[0] + (p[1] - p[0]) * (loss - 0.45f) / 0.15f;
            if (loss < 0.75f) return p[1] + (p[2] - p[1]) * (loss - 0.60f) / 0.15f;
            if (loss < 0.90f) return p[2] + (p[3] - p[2]) * (loss - 0.75f) / 0.15f;
            return p[3];
        }

        private static float PerManDay { get { var s = Settings.Current; return Math.Max(1f, s != null ? s.WorkHoursPerManDay : 16f); } }
        private static float FlatHours { get { var s = Settings.Current; return Math.Max(0.05f, s != null ? s.MendLootHoursPerPiece : 0.6f); } }

        /// <summary>Roboczogodziny naprawy sztuki ze stanem w modyfikatorze (ubytek = 1 - PriceMultiplier - jak cena); przy wylaczonym
        /// MendHoursByGuild - stale MendLootHoursPerPiece, jak dotad.</summary>
        internal static float Hours(EquipmentElement ee)
        {
            float pm = ee.ItemModifier != null ? ee.ItemModifier.PriceMultiplier : 1f;
            if (pm < 0f) pm = 0f; if (pm > 1f) pm = 1f;
            return HoursFor(ee.Item, 1f - pm);
        }

        /// <summary>To samo przy ubytku podanym wprost (0..1). Sztuka spoza tabeli albo bez rachunku kosztu - stale godziny.</summary>
        internal static float HoursFor(ItemObject it, float loss)
        {
            if (!On || it == null) return FlatHours;
            try
            {
                int trade; int k = KindOf(it, out trade);
                if (k < 0) return FlatHours;
                var c = ArmsPricing.CostOf(it);
                if (c == null) return FlatHours;
                float days = HistoricalPrices.On ? HistoricalPrices.HistDays(it, c) : c.Days;
                if (days <= 0f) return FlatHours;
                return Math.Max(0.1f, ShareOf(k, Math.Max(0f, Math.Min(1f, loss))) * days * PerManDay);
            }
            catch (Exception e) { if (++_errHours <= 3) Log.Error("GuildMend.Hours (potkniecie " + _errHours + ")", e); return FlatHours; }   // per sztuka, bez wylacznika (CLAUDE.md 7)
        }
        private static int _errHours;

        // ------------------------------------------------------------ pule godzin cechow (kowale - SmithHours, reszta tutaj)
        private static Dictionary<Town, float[]> _used = new Dictionary<Town, float[]>();
        private static Dictionary<Town, float[]> _yesterday = new Dictionary<Town, float[]>();
        private static Dictionary<Town, float[]> _auto = new Dictionary<Town, float[]>();
        private static int _day = -1;

        internal static void Reset()
        {
            _used.Clear(); _yesterday.Clear(); _auto.Clear(); _day = -1;
            Array.Clear(_menBench, 0, Trades); _menTown = null; _menLastHour = -999; _bulkDay = -1; _bulkTown = null; _bulkN = 0;
            ResetStats(); _statDay = -1;
            FieldArmourer.Reset();
        }

        private static void Roll()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _day) return;
            if (_day >= 0) LogDay();
            _yesterday = d == _day + 1 ? _used : new Dictionary<Town, float[]>();
            _used = new Dictionary<Town, float[]>();
            _auto = new Dictionary<Town, float[]>();
            _day = d;
        }

        private static float[] Row(Dictionary<Town, float[]> map, Town t)
        {
            float[] r;
            if (!map.TryGetValue(t, out r)) map[t] = r = new float[Trades];
            return r;
        }

        /// <summary>Rece cechu w miescie (ludzie): kowale jak SmithHours.Smiths; reszta - rece rzemieslnikow x waga cechu / wszystkie cechy.</summary>
        internal static float Hands(Town t, int trade)
        {
            if (t == null) return 0f;
            if (trade == Smith) return SmithHours.Smiths(t);
            float wsum = 0f;
            foreach (var g in AllGuilds) wsum += WorkshopLaw.GuildWeight(g);
            return WorkshopLaw.TownHands(t) * WorkshopLaw.GuildWeight(Guild[trade]) / Math.Max(0.01f, wsum);
        }
        private static readonly string[] AllGuilds = { "krawiec", "platnerz", "miecznik", "siodlarz", "lucznik", "tarczownik" };

        internal static float Capacity(Town t, int trade) { return Hands(t, trade) * PerManDay; }

        /// <summary>Godziny cechu jeszcze wolne dzis (dla zlecen gracza - bez limitu polowy).</summary>
        internal static float Available(Town t, int trade)
        {
            if (t == null) return 0f;
            if (trade == Smith) return SmithHours.Available(t);
            Roll();
            float[] u; _used.TryGetValue(t, out u);
            return Math.Max(0f, Capacity(t, trade) - (u != null ? u[trade] : 0f));
        }

        /// <summary>Godziny dla napraw automatycznych (AI + sakiewka ludzi): wolne, ale razem najwyzej polowa dziennych godzin cechu (AutoMendHalfGuild).</summary>
        internal static float AutoAvailable(Town t, int trade)
        {
            float a = Available(t, trade);
            if (!HalfOn || t == null) return a;
            Roll();
            float[] u; _auto.TryGetValue(t, out u);
            return Math.Max(0f, Math.Min(a, AutoShare * Capacity(t, trade) - (u != null ? u[trade] : 0f)));
        }

        internal static void Use(Town t, int trade, float hours, bool auto)
        {
            if (t == null || hours <= 0f) return;
            Roll();
            if (trade == Smith) SmithHours.Use(t, hours); else Row(_used, t)[trade] += hours;
            if (auto) Row(_auto, t)[trade] += hours;
        }

        /// <summary>Roboczodni cechu warsztatu (krawiec / siodlarz / lucznik / tarczownik) zjedzone wczoraj przez naprawy - tyle mniej rak w warsztacie
        /// zbrojnym dzis (WorkshopLaw, jak SmithHours.ManDaysYesterday dla platnerzy i miecznikow). 0 przy wylaczonym MendHoursByGuild.</summary>
        internal static float ManDaysYesterday(Town t, string guild)
        {
            if (t == null || !On) return 0f;
            int k = Array.IndexOf(Guild, guild);
            if (k <= 0) return 0f;
            Roll();
            float[] u; _yesterday.TryGetValue(t, out u);
            return u != null ? u[k] / PerManDay : 0f;
        }

        // ------------------------------------------------------------ rabat hurtowy przy kazdym zleceniu
        /// <summary>Ulamek rabatu za zlecenie na pieces sztuk (0.5% za sztuke, do 30% - klucze rabatu polek wojska); 0 przy wylaczonym kluczu.</summary>
        internal static float BulkCut(int pieces)
        {
            var s = Settings.Current;
            if (s == null || !s.MendBulkEveryOrder || pieces <= 0) return 0f;
            return Math.Min(Math.Max(0f, s.TroopMendBulkDiscountMax), pieces * Math.Max(0f, s.TroopMendBulkDiscountPP)) / 100f;
        }

        /// <summary>Robocizna jednej sztuki po rabacie hurtowym zlecenia (najmniej 1 pens, gdy byla).</summary>
        internal static int Bulk(int labor, int pieces)
        {
            float cut = BulkCut(pieces);
            if (cut <= 0f || labor <= 0) return labor;
            return Math.Max(1, (int)Math.Round(labor * (1f - cut)));
        }

        /// <summary>Do linii doby: ile robocizny zlecenia odpuscil rabat (szacunek z zaplaconej robocizny po rabacie).</summary>
        internal static void NoteBulk(int laborPaid, int pieces)
        {
            float cut = BulkCut(pieces);
            if (cut > 0f && cut < 1f && laborPaid > 0) _stBulkGold += (int)Math.Round(laborPaid * cut / (1f - cut));
        }

        /// <summary>Opis rabatu dla gracza ("" bez rabatu).</summary>
        internal static string BulkEn(int pieces)
        {
            float cut = BulkCut(pieces);
            return cut > 0f ? " Bulk rate for " + pieces + " pieces: " + (int)Math.Round(cut * 100f) + "% off the work." : "";
        }

        // ------------------------------------------------------------ zlecenie gracza: godziny cechow i czas oczekiwania
        /// <summary>Godziny zlecenia gracza wedlug cechow: Add na kazda sztuke, Clock - ile godzin zegara potrwa (cechy pracuja rownolegle, kazdy
        /// wszystkimi wolnymi rekami; czego nie zmieszcza dzis wolne godziny - kolejne dni po pelnej zmianie), Use - godziny z pul miasta.</summary>
        internal sealed class Tally
        {
            public readonly float[] H = new float[Trades];
            public int Pieces;
            public void Add(EquipmentElement ee, int n) { if (n <= 0 || ee.Item == null) return; H[TradeOf(ee.Item)] += Hours(ee) * n; Pieces += n; }
            public void AddHours(ItemObject it, float hours) { if (it == null || hours <= 0f) return; H[TradeOf(it)] += hours; Pieces++; }
            public float Total { get { float x = 0f; for (int k = 0; k < Trades; k++) x += H[k]; return x; } }

            public float Clock(Town t)
            {
                float worst = 0f;
                for (int k = 0; k < Trades; k++)
                {
                    if (H[k] <= 0f) continue;
                    float hands = Math.Max(1f, Hands(t, k)), cap = Math.Max(1f, Capacity(t, k)), avail = Available(t, k);
                    float c = H[k] <= avail ? H[k] / hands : avail / hands + 24f * (H[k] - avail) / cap;
                    if (c > worst) worst = c;
                }
                return Math.Max(0.5f, worst);
            }

            public void Use(Town t)
            {
                for (int k = 0; k < Trades; k++) GuildMend.Use(t, k, H[k], false);
                _stPlayerPieces += Pieces;
                for (int k = 0; k < Trades; k++) _stPlayerH[k] += H[k];
            }

            public string LogPl()
            {
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < Trades; k++) if (H[k] > 0f) sb.Append(sb.Length > 0 ? ", " : "").Append(TradePl[k]).Append(' ').Append(F1(H[k])).Append(" h");
                return sb.Length > 0 ? sb.ToString() : "0 h";
            }
        }

        /// <summary>Godziny zegara dla zlecenia gracza w biezacym miescie albo fallback (stara regula), gdy klucz wylaczony lub brak miasta.</summary>
        internal static float PlayerClock(Tally t, float fallback)
        {
            var st = Settlement.CurrentSettlement;
            if (!On || t == null || st == null || st.Town == null || t.Pieces <= 0) return fallback;
            return t.Clock(st.Town);
        }

        internal static void PlayerDone(Tally t)
        {
            var st = Settlement.CurrentSettlement;
            if (!On || t == null || st == null || st.Town == null) return;
            t.Use(st.Town);
        }

        // ------------------------------------------------------------ AI: zlecenie lorda na jeden dzien w miescie
        /// <summary>Budzet godzin cechow na zlecenie lorda AI (naprawa automatyczna - limit polowy rak): Fits/HowMany przed zaplata, Take po zaplanowaniu,
        /// Commit po wykonaniu (godziny tylko za gotowe sztuki).</summary>
        internal sealed class Budget
        {
            private readonly Town _t;
            private readonly float[] _left = new float[Trades], _take = new float[Trades];
            public bool CapHit;
            public int CapN;
            internal Budget(Town t) { _t = t; for (int k = 0; k < Trades; k++) _left[k] = AutoAvailable(t, k); }
            public int HowMany(EquipmentElement ee, int n)
            {
                if (ee.Item == null || n <= 0) return 0;
                float h = Hours(ee);
                int k = TradeOf(ee.Item);
                int fit = Math.Min(n, (int)Math.Floor(_left[k] / Math.Max(0.01f, h) + 0.0001f));
                if (fit < n) { CapHit = true; CapN += n - fit; }
                return Math.Max(0, fit);
            }
            public void Take(EquipmentElement ee, int n)
            {
                if (ee.Item == null || n <= 0) return;
                float h = Hours(ee) * n; int k = TradeOf(ee.Item);
                _left[k] = Math.Max(0f, _left[k] - h); _take[k] += h;
            }
            public void Commit(int pieces)
            {
                for (int k = 0; k < Trades; k++) { Use(_t, k, _take[k], true); _stAiH[k] += _take[k]; }
                _stAiPieces += pieces; _stAiCapN += CapN; if (CapHit) _stAiCapVisits++;
            }
        }

        // ------------------------------------------------------------ ludzie gracza: godzinowo z sakiewki, kazdy cech osobno
        private static readonly float[] _menBench = new float[Trades];
        private static string _menTown;
        private static int _menLastHour = -999;
        private static int _bulkDay = -1, _bulkN;
        private static string _bulkTown;
        private static int _menDayMended, _menDayPaid, _menDayMat, _menDayWait, _menDayWaitMask;

        internal static void MenLeftTown() { Array.Clear(_menBench, 0, Trades); _menTown = null; }

        /// <summary>Godzina pracy cechow miasta nad obitym sprzetem Twoich ludzi (zastepuje stala 0.6 h na sztuke w TroopSelfMend.Hourly). Kazdy cech ma
        /// wlasna lawe godzin (rece cechu / WorkHoursPerManDay na godzine, przy AutoMendHalfGuild polowa); sztuka konczy sie, gdy na lawie jej cechu
        /// zbierze sie tyle godzin, ile trwa jej naprawa. Zaplata (robota po rabacie hurtowym + material) z sakiewki ludzi do kasy miasta - jak dotad.</summary>
        internal static void MenHourly(MobileParty main, Settlement st, ItemRoster armory, List<ItemRosterElement> worn)
        {
            var t = st.Town;
            int nowH = (int)CampaignTime.Now.ToHours;
            if (_menTown != st.StringId || nowH - _menLastHour > 8) { Array.Clear(_menBench, 0, Trades); _menTown = st.StringId; }
            _menLastHour = nowH;
            int day = (int)CampaignTime.Now.ToDays, wornN = 0;
            foreach (var el in worn) wornN += el.Amount;
            if (_bulkDay != day || _bulkTown != st.StringId) { _bulkDay = day; _bulkTown = st.StringId; _bulkN = wornN; }   // zlecenie dnia: sztuki czekajace rano

            var has = new bool[Trades]; var share = new float[Trades]; var start = new float[Trades]; var doneH = new float[Trades]; var limited = new bool[Trades];
            foreach (var el in worn) if (el.EquipmentElement.Item != null) has[TradeOf(el.EquipmentElement.Item)] = true;
            float frac = HalfOn ? AutoShare : 1f;
            for (int k = 0; k < Trades; k++)
            {
                start[k] = _menBench[k];
                if (!has[k]) { _menBench[k] = 0f; continue; }
                share[k] = Math.Min(AutoAvailable(t, k), frac * Capacity(t, k) / PerManDay);
                _menBench[k] += share[k];
            }
            bool broke = false;
            int mended = 0, paid = 0, matGold = 0, wait = 0, waitMask = 0;
            if (MendMaterial.MenAndLordsOn)
            {
                var o = new MendMaterial.Order(st);
                int purse = MenPurse.Get(main);
                foreach (var el in worn)
                {
                    var ee = el.EquipmentElement;
                    int k = TradeOf(ee.Item);
                    if (limited[k]) continue;   // ten cech nie ma dzis godzin na nastepne sztuki - czekaja w kolejce (najgorsze najpierw)
                    float h = Hours(ee);
                    int n = Math.Min(el.Amount, (int)Math.Floor(_menBench[k] / h + 0.0001f));
                    if (n < el.Amount) limited[k] = true;
                    if (n <= 0) continue;
                    int poor0 = o.Poor;
                    int planned = o.AddLot(ee, MendMaterial.Needs(ee), Bulk(TroopSelfMend.UnitCost(ee, st), _bulkN), n, purse, int.MaxValue);
                    _menBench[k] -= planned * h; doneH[k] += planned * h;
                    if (o.Poor > poor0) { broke = true; break; }
                }
                if (o.Pieces > 0)
                {
                    o.Bench.Commit();
                    foreach (var j in o.Jobs)
                    {
                        armory.AddToCounts(j.El, -j.N);
                        armory.AddToCounts(new EquipmentElement(j.El.Item), j.N);
                        mended += j.N;
                    }
                    paid = MenPurse.Take(main, o.Total);
                    MendMaterial.PaySmiths(st, paid, o.Labor);   // robocizna do kiesy ludu, material do kasy miasta - jak TroopSelfMend.HourlyWithMaterial
                    matGold = o.MatGold;
                    NoteBulk(o.Labor, _bulkN);
                }
                wait = o.Wait; waitMask = o.WaitMask;
            }
            else
            {
                foreach (var el in worn)
                {
                    var ee = el.EquipmentElement;
                    int k = TradeOf(ee.Item);
                    if (limited[k]) continue;
                    float h = Hours(ee);
                    int left = el.Amount, fixedN = 0;
                    while (left > 0 && _menBench[k] >= h - 0.0001f)
                    {
                        int unit = Bulk(TroopSelfMend.UnitCost(ee, st), _bulkN);
                        if (MenPurse.Get(main) < unit) { broke = true; break; }
                        MenPurse.Take(main, unit);
                        MendMaterial.PaySmiths(st, unit, unit);
                        _menBench[k] -= h; doneH[k] += h; left--; fixedN++; paid += unit;
                        NoteBulk(unit, _bulkN);
                    }
                    if (left > 0 && !broke) limited[k] = true;
                    if (fixedN > 0)
                    {
                        armory.AddToCounts(ee, -fixedN);
                        armory.AddToCounts(new EquipmentElement(ee.Item), fixedN);
                        mended += fixedN;
                    }
                    if (broke) break;
                }
            }
            // godziny: cech pracowal cala godzine (kolejka czeka na jego godziny) - cala porcja; inaczej (pusto, sakiewka, material) - tylko gotowe
            // sztuki ponad to, co juz bylo na lawie, i lawa od zera (cech nie trzyma godzin na zapas)
            for (int k = 0; k < Trades; k++)
            {
                if (!has[k]) continue;
                float used = limited[k] && !broke ? share[k] : Math.Max(0f, doneH[k] - start[k]);
                if (!(limited[k] && !broke)) _menBench[k] = 0f;
                Use(t, k, used, true);
                _stMenH[k] += used;
            }
            _stMenPieces += mended;
            _menDayMended += mended; _menDayPaid += paid; _menDayMat += matGold; _menDayWait = wait; _menDayWaitMask |= waitMask;
            int hour = CampaignTime.Now.GetHourOfDay;
            if (_menDayMended > 0 && (hour == 22 || broke))
            {
                Log.Player("The town trades of " + st.Name + " mended " + _menDayMended + " pieces of your men's kit today for " + _menDayPaid
                           + " denars from the men's purse." + (broke ? " The men's purse is empty - the rest waits (or pay the smith yourself)." : "")
                           + (_menDayWait > 0 ? " " + _menDayWait + " wait for materials - the market has not enough " + MendMaterial.KindsEn(_menDayWaitMask) + "." : ""), true);
                Log.Info("GuildMend ludzie: " + st.Name + " - naprawiono " + _menDayMended + " szt. za " + _menDayPaid + " z sakiewki ludzi (material " + _menDayMat
                         + "), rabat hurtowy " + (int)Math.Round(BulkCut(_bulkN) * 100f) + "% przy " + _bulkN + " szt. rano; lawy cechow: " + Bench() + (broke ? " (sakiewka pusta)" : "")
                         + "; czeka na material " + _menDayWait + ".");
                _menDayMended = 0; _menDayPaid = 0; _menDayMat = 0; _menDayWait = 0; _menDayWaitMask = 0;
            }
        }

        private static string Bench()
        {
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < Trades; k++) sb.Append(k > 0 ? "/" : "").Append(F1(_menBench[k]));
            return sb.ToString() + " h";
        }

        // ------------------------------------------------------------ linia doby (log_grep: "Naprawy wg cechow (R3-02)")
        private static readonly float[] _stAiH = new float[Trades], _stMenH = new float[Trades], _stPlayerH = new float[Trades];
        private static int _stAiPieces, _stAiCapN, _stAiCapVisits, _stMenPieces, _stPlayerPieces, _stBulkGold, _statDay = -1;

        private static void ResetStats()
        {
            Array.Clear(_stAiH, 0, Trades); Array.Clear(_stMenH, 0, Trades); Array.Clear(_stPlayerH, 0, Trades);
            _stAiPieces = _stAiCapN = _stAiCapVisits = _stMenPieces = _stPlayerPieces = _stBulkGold = 0;
        }

        /// <summary>Wolane co godzine (TroopSelfMend.Hourly): przelom doby pul + platnerz w polu.</summary>
        internal static void HourTick()
        {
            try { if (On) Roll(); }
            catch (Exception e) { if (++_errTick <= 3) Log.Error("GuildMend.HourTick (potkniecie " + _errTick + ")", e); }
            FieldArmourer.Hourly();
        }
        private static int _errTick;

        private static void LogDay()
        {
            if (_stAiPieces + _stMenPieces + _stPlayerPieces + _stAiCapN + _stBulkGold == 0) { ResetStats(); return; }
            Log.Info("Naprawy wg cechow (R3-02): doba " + _day + " | AI: " + _stAiPieces + " szt., godz. " + Row5(_stAiH) + ", stop na polowie rak cechu " + _stAiCapN
                     + " szt. w " + _stAiCapVisits + " wizytach | ludzie gracza: " + _stMenPieces + " szt., godz. " + Row5(_stMenH) + " | gracz (zlecenia): "
                     + _stPlayerPieces + " szt., godz. " + Row5(_stPlayerH) + " | rabat hurtowy razem " + _stBulkGold + " zl (kowale/siodlarze/krawcy/lukarze/tarczownicy)");
            ResetStats();
        }

        private static string Row5(float[] h)
        {
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < Trades; k++) sb.Append(k > 0 ? "/" : "").Append(((int)Math.Round(h[k])).ToString());
            return sb.ToString();
        }

        internal static string F1(float v) { return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
