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
    ///  2a) MendIdleHandsFirst (n22f-naprawy2, zastepuje 2): kazda godzina napraw bierze najpierw rece cechu bez roboty w tym miescie - caly warsztat
    ///     rzemieslnikow bez czynnej linii, linie z pelnym bankiem roboty (robota i tak przepada) - a dopiero potem wlasne rece cechu, ktore pracuja:
    ///     przeniesione do linii innych cechow (cech bez wlasnej linii) i przy jego liniach. Naprawy automatyczne biora z tych pracujacych rak razem
    ///     najwyzej AutoLineShare (15%) wlasnych rak cechu na dobe, reszta czeka. Godziny z rak bezczynnych i z linii z pelnym bankiem nie odchodza jutro
    ///     od linii (ManDaysYesterday, SmithFreeManDaysYesterday); rece przeniesione - jutro od rak przeniesionych, rece linii - od linii (jak dotad).
    ///  3) MendBulkEveryOrder: rabat hurtowy z polek wojska (TroopMendBulkDiscountPP/Max: 0.5% za sztuke, do 30%) przy kazdym zleceniu - od robocizny,
    ///     nie od materialu i nie od godzin. Zlecenie: jedno klikniecie gracza (lup z sakw, polki wojska, uprzaz "Mend everything you wear" -
    ///     ArmouryBehavior.RepairCost/PlanRepair); jeden dzien lorda w miescie; u Twoich ludzi - sztuki czekajace rano. Pojedyncza sztuka ("Pick a piece")
    ///     bez rabatu (0.5% od kilku pensow znika w zaokragleniu). Uprzaz i sztuka na grzbiecie: godziny ze stanu w ksiedze zuzycia (procenty).
    ///     Zamknieta ekonomia: placacy placi mniej i kasa miasta dostaje mniej o te sama kwote.
    /// Wylaczone klucze - jak dotad co do godziny i pensa. Stan tylko dzienny (pule jak SmithHours) - nic do zapisu.
    /// Poprawki po recenzji: (a) zlecenie gracza na wiecej niz dzisiejsze wolne rece rezerwuje godziny cechu CO GODZINE czekania (PlayerStart + HourTick),
    /// nie dopiero na koncu - AI i ludzie gracza widza zajete rece od razu; czego dzis nie ma (koniec roboty przed rezerwacja, AI zajelo rece w tym czasie) -
    /// przechodzi jako dlug cechu na nastepne dni (_carry), wiec zadna roboczogodzina nie powstaje z niczego. (b) Rabat lorda AI od sztuk, na ktore cech
    /// ma dzis godziny (Budget.Preview), nie od calej kolejki. (c) Kwatermistrz Spoils (SpoilsSeal) i stara regula ludzi (TroopSelfMend.Run, sakiewki
    /// wylaczone) - te same godziny cechow i rabat (Order*/RunBudget).
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
        /// <summary>n22f-naprawy2 (MendIdleHandsFirst): naprawy biora najpierw rece cechu bez roboty (caly warsztat bez linii, cech bez linii, linia
        /// z pelnym bankiem roboty), z rak przy liniach naprawy automatyczne najwyzej AutoLineShare na dobe. Zastepuje polowe rak (AutoMendHalfGuild).</summary>
        internal static bool IdleOn { get { var s = Settings.Current; return s != null && s.MendHoursByGuild && s.MendIdleHandsFirst; } }
        private const float AutoShare = 0.5f;   // projekt pkt 9 [K]: AI + ludzie razem najwyzej polowa rak cechu
        // n22f-naprawy2: z pracujacych wlasnych rak cechu (przy jego liniach i przeniesionych do linii innych cechow) naprawy automatyczne (AI + sakiewka
        // ludzi) razem najwyzej 15% na dobe. Szacunek z biegow 160 dob (n22f-naprawy2-tmp/sim2.py, CHANGELOG galezi "Poprawka n22f-naprawy2"): n22b siodlarze
        // 13.9% rak na naprawy = -13.5% zrobionych. Model (linie z pelnym bankiem jak w n22a / polowa tego; stop 2/3 wizyt / wszedzie): 10% - AI 670-1100 szt./d,
        // siodlarze ok. 2.5-3% rak; 15% - AI 800-1185, siodlarze ok. 3.7-4.4%; 20% - AI 930-1260, siodlarze ok. 5-5.8% (ponad 5%). 15% = oba cele naraz.
        private const float AutoLineShare = 0.15f;

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
            _carry.Clear(); ClearOrder();
            _plan.Clear(); _stuck.Clear(); _stuckPrev.Clear(); _stuckDay = -1; _src.Clear(); _srcYesterday.Clear();
            ResetStats();
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
            _srcYesterday = d == _day + 1 ? _src : new Dictionary<Town, float[]>();   // n22f-naprawy2: zrodlo rak wczoraj (bez ciecia linii dzis)
            _src = new Dictionary<Town, float[]>();
            _day = d;
            CarryIn();
        }

        // ------------------------------------------------------------ n22f-naprawy2: skad sa rece napraw (MendIdleHandsFirst)
        // plan doby ukrytego warsztatu rzemieslnikow miasta (WorkshopLaw.BuildPlan, ostatni zbudowany): [0..4] rece cechu bez czynnej linii (przeniesione
        // do innych cechow), [5..9] rece przy liniach cechu, [10] = 1: caly warsztat bez czynnej linii (rece bezczynne), [11] doba planu; roboczodni
        private static readonly Dictionary<Town, float[]> _plan = new Dictionary<Town, float[]>();
        // rece przy liniach z pelnym bankiem roboty (linia nic nie robi, robota przepada; WorkshopLaw.CyclePrefix) - dzis i wczoraj; roboczodni
        private static Dictionary<Town, float[]> _stuck = new Dictionary<Town, float[]>(), _stuckPrev = new Dictionary<Town, float[]>();
        private static int _stuckDay = -1;
        // godziny dzis wedlug zrodla: [0..4] bezczynne, [5..9] cech bez linii (rece przeniesione), [10..14] linia z pelnym bankiem, [15..19] rece linii,
        // [20..24] naprawy automatyczne z rak pracujacych (przeniesione + linie) - pod limitem AutoLineShare
        private static Dictionary<Town, float[]> _src = new Dictionary<Town, float[]>(), _srcYesterday = new Dictionary<Town, float[]>();
        private const int SrcN = 25;

        /// <summary>Cech warsztatu (WorkshopLaw.GuildOrder) -> cech napraw; -1 = inny.</summary>
        internal static int TradeOfGuild(string g)
        {
            switch (g)
            {
                case "platnerz": case "miecznik": return Smith;
                case "siodlarz": return Saddler;
                case "krawiec": return Tailor;
                case "lucznik": return Bowyer;
                case "tarczownik": return Shieldmaker;
                default: return -1;
            }
        }

        /// <summary>Wiersz planu miasta do wypelnienia przez WorkshopLaw.BuildPlan (ukryty warsztat rzemieslnikow, raz na dobe); null przy wylaczonym MendHoursByGuild.</summary>
        internal static float[] PlanRow(Town t)
        {
            if (!On || t == null) return null;
            float[] r;
            if (!_plan.TryGetValue(t, out r)) _plan[t] = r = new float[12];
            r[11] = (float)Math.Floor(CampaignTime.Now.ToDays);   // doba planu (WorkshopLaw czysci tylko [0..10])
            return r;
        }

        private static void RollStuck()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _stuckDay) return;
            _stuckPrev = d == _stuckDay + 1 ? _stuck : new Dictionary<Town, float[]>();
            _stuck = new Dictionary<Town, float[]>();
            _stuckDay = d;
        }

        /// <summary>WorkshopLaw.CyclePrefix: linia cechu w ukrytym warsztacie ma pelny bank roboty (dzisiejsza robota przepada) - jej rece (share
        /// roboczodni) sa dzis bez roboty. Raz na linie na dobe (tylko gdy cykl doliczyl robote).</summary>
        internal static void NoteStuck(Town t, string guild, float share)
        {
            if (!On || t == null || share <= 0f) return;
            int k = TradeOfGuild(guild);
            if (k < 0) return;
            RollStuck();
            Row(_stuck, t)[k] += share;
        }

        /// <summary>Rece cechu (roboczodni) wedlug zrodla z ostatniego planu miasta: bezczynne (caly warsztat bez linii), cech bez linii (wlasne rece
        /// przeniesione do linii innych cechow - pracuja), linie z pelnym bankiem (najwyzej wlasne rece linii), reszta wlasnych rak linii.
        /// Bez planu (miasto bez ukrytego warsztatu, pierwsza doba sesji) - wszystkie rece cechu jak rece linii (ostroznie).</summary>
        private static void Tiers(Town t, int k, out float idle, out float free, out float stuck, out float line)
        {
            float[] p;
            idle = free = stuck = 0f;
            if (!_plan.TryGetValue(t, out p) || (int)CampaignTime.Now.ToDays - (int)p[11] > 2) { line = Hands(t, k); return; }   // brak planu albo stary (warsztat bez linii zbrojnych)
            if (p[10] > 0.5f) { idle = Hands(t, k); line = 0f; return; }
            free = p[k]; line = p[5 + k];
            RollStuck();
            float[] s1, s0; _stuck.TryGetValue(t, out s1); _stuckPrev.TryGetValue(t, out s0);
            stuck = Math.Min(line, Math.Max(s1 != null ? s1[k] : 0f, s0 != null ? s0[k] : 0f));   // dzis albo wczoraj (cykle linii ida w ciagu doby)
            line -= stuck;
        }

        /// <summary>Godziny dla napraw automatycznych wedlug zrodla (dzis jeszcze wolne): bezczynne + linie z pelnym bankiem bez limitu; z rak pracujacych
        /// (cech bez linii - rece przeniesione, i rece linii) razem najwyzej AutoLineShare (liczone tylko naprawy automatyczne). Poprawka po recenzji:
        /// rece przeniesione nie sa bezczynne - jutro schodza z linii cechow, ktore je dostaly (glownie platnerzy), wiec ida pod ten sam limit.</summary>
        private static float AutoRoom(Town t, int k)
        {
            float idle, free, stuck, line; Tiers(t, k, out idle, out free, out stuck, out line);
            float[] u; _src.TryGetValue(t, out u);
            float pmd = PerManDay, lim = AutoLineShare * (free + line) * pmd;
            if (u == null) return (idle + stuck) * pmd + lim;
            return Math.Max(0f, idle * pmd - u[k]) + Math.Max(0f, stuck * pmd - u[10 + k]) + Math.Max(0f, lim - u[20 + k]);
        }

        /// <summary>Pelny dzienny przydzial napraw automatycznych cechu w miescie (bez dzisiejszego zuzycia) - tempo lawy ludzi gracza na godzine.</summary>
        private static float AutoDaily(Town t, int k)
        {
            float idle, free, stuck, line; Tiers(t, k, out idle, out free, out stuck, out line);
            return (idle + stuck + AutoLineShare * (free + line)) * PerManDay;
        }

        /// <summary>Kazda godzina napraw (AI, ludzie, gracz): najpierw rece bezczynne, potem linie z pelnym bankiem, cech bez linii (rece przeniesione),
        /// na koncu rece linii. Naprawy automatyczne z rak przeniesionych i linii - do licznika limitu ([20..24]). Przy wylaczonym MendIdleHandsFirst -
        /// tylko liczniki doby (zachowanie jak dotad).</summary>
        private static void Split(Town t, int k, float hours, bool auto)
        {
            if (hours <= 0f) return;
            float idle, free, stuck, line; Tiers(t, k, out idle, out free, out stuck, out line);
            var u = Row(_src, t, SrcN);
            float pmd = PerManDay, left = hours, x;
            x = Math.Min(left, Math.Max(0f, idle * pmd - u[k])); u[k] += x; left -= x; _stIdleH[k] += x;
            x = Math.Min(left, Math.Max(0f, stuck * pmd - u[10 + k])); u[10 + k] += x; left -= x; _stStuckH[k] += x;
            if (left <= 0f) return;
            if (auto) { u[20 + k] += left; _stAutoLineH[k] += left; }   // rece pracujace (przeniesione + linie) - limit AutoLineShare
            x = Math.Min(left, Math.Max(0f, free * pmd - u[5 + k])); u[5 + k] += x; left -= x; _stFreeH[k] += x;
            if (left <= 0f) return;
            u[15 + k] += left; _stLineH[k] += left;
        }

        /// <summary>Godziny cechu wczoraj z rak bezczynnych i linii z pelnym bankiem - te nie odchodza dzis od linii (robota i tak by przepadla);
        /// 0 przy wylaczonym MendIdleHandsFirst. Rece cechu bez linii odchodza jak dotad od rak przeniesionych (WorkshopLaw).</summary>
        private static float FreeHoursYesterday(Town t, int k)
        {
            if (!IdleOn || t == null) return 0f;
            float[] u;
            if (!_srcYesterday.TryGetValue(t, out u)) return 0f;
            return u[k] + u[10 + k];
        }

        /// <summary>To samo dla kowali w roboczodniach (WorkshopLaw: od SmithHours.ManDaysYesterday przed cieciem platnerzy i miecznikow).</summary>
        internal static float SmithFreeManDaysYesterday(Town t)
        {
            if (!IdleOn || t == null) return 0f;
            Roll();
            return FreeHoursYesterday(t, Smith) / PerManDay;
        }

        // ------------------------------------------------------------ dlug cechu (recenzja: zlecenie gracza ponad wolne rece dnia nie powstaje z niczego)
        private static readonly Dictionary<Town, float[]> _carry = new Dictionary<Town, float[]>();

        /// <summary>Nowy dzien: zalegle godziny zlecen gracza zajmuja najpierw rece cechu (najwyzej cala dzienna zmiana; reszta - nastepny dzien).</summary>
        private static void CarryIn()
        {
            if (_carry.Count == 0) return;
            var done = new List<Town>();
            foreach (var kv in _carry)
            {
                bool any = false;
                for (int k = 0; k < Trades; k++)
                {
                    float c = kv.Value[k];
                    if (c <= 0f) continue;
                    float x = Math.Min(c, Available(kv.Key, k));
                    if (x > 0f) { UseRaw(kv.Key, k, x, false); kv.Value[k] = c - x; _stCarryH += x; }
                    if (kv.Value[k] > 0.01f) any = true; else kv.Value[k] = 0f;
                }
                if (!any) done.Add(kv.Key);
            }
            foreach (var t in done) _carry.Remove(t);
        }

        private static float[] Row(Dictionary<Town, float[]> map, Town t, int n = Trades)
        {
            float[] r;
            if (!map.TryGetValue(t, out r)) map[t] = r = new float[n];
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

        /// <summary>Godziny dla napraw automatycznych (AI + sakiewka ludzi): wolne, ale razem najwyzej polowa dziennych godzin cechu (AutoMendHalfGuild);
        /// przy MendIdleHandsFirst - rece bez roboty bez limitu i najwyzej AutoLineShare rak linii (AutoRoom), polowa nie dziala.</summary>
        internal static float AutoAvailable(Town t, int trade)
        {
            float a = Available(t, trade);
            if (t != null && IdleOn) { Roll(); return Math.Min(a, AutoRoom(t, trade)); }
            if (!HalfOn || t == null) return a;
            Roll();
            float[] u; _auto.TryGetValue(t, out u);
            return Math.Max(0f, Math.Min(a, AutoShare * Capacity(t, trade) - (u != null ? u[trade] : 0f)));
        }

        internal static void Use(Town t, int trade, float hours, bool auto)
        {
            if (t == null || hours <= 0f) return;
            Roll();
            if (!auto)
            {
                // zlecenie gracza: dzis najwyzej wolne godziny cechu, reszta jako dlug cechu na nastepne dni (CarryIn) - nic z niczego
                float today = Math.Min(hours, Available(t, trade));
                if (hours - today > 0.001f) Row(_carry, t)[trade] += hours - today;
                hours = today;
                if (hours <= 0f) return;
            }
            UseRaw(t, trade, hours, auto);
        }

        private static void UseRaw(Town t, int trade, float hours, bool auto)
        {
            if (trade == Smith) SmithHours.Use(t, hours); else Row(_used, t)[trade] += hours;
            if (auto) Row(_auto, t)[trade] += hours;
            Split(t, trade, hours, auto);   // n22f-naprawy2: zrodlo rak (przy wylaczonym MendIdleHandsFirst - tylko liczniki)
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
            if (u == null) return 0f;
            return Math.Max(0f, u[k] - FreeHoursYesterday(t, k)) / PerManDay;   // n22f-naprawy2: rece bezczynne i linie z pelnym bankiem nie odchodza od linii
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
            /// <summary>Czesc zlecenia (done z planned sztuk) - godziny proporcjonalnie; done >= planned - cale.</summary>
            public Tally Scaled(int done, int planned)
            {
                if (planned <= 0 || done >= planned) return this;
                float f = Math.Max(0f, done) / (float)planned;
                var t = new Tally();
                for (int k = 0; k < Trades; k++) t.H[k] = H[k] * f;
                t.Pieces = Math.Max(0, done);
                return t;
            }

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

            /// <summary>Godziny z pul miasta za gotowe sztuki zlecenia; taken - godziny juz zarezerwowane w czasie czekania (null - zadne).</summary>
            public void Use(Town t, float[] taken)
            {
                for (int k = 0; k < Trades; k++)
                {
                    float c = Math.Max(0f, H[k] - (taken != null ? taken[k] : 0f));
                    GuildMend.Use(t, k, c, false);
                    _stPlayerH[k] += c;
                }
                _stPlayerPieces += Pieces;
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

        /// <summary>Koniec zlecenia gracza (gotowe sztuki): godziny tych sztuk minus to, co zlecenie juz zarezerwowalo w czasie czekania (godziny
        /// zarezerwowane na sztuki nieukonczone - przepracowane, nie wracaja).</summary>
        internal static void PlayerDone(Tally t)
        {
            var st = Settlement.CurrentSettlement;
            if (!On || t == null || st == null || st.Town == null) { ClearOrder(); return; }
            t.Use(st.Town, _poTown == st.Town ? _poTaken : null);
            ClearOrder();
        }

        /// <summary>Zlecenie gracza wykonane od reki (kwatermistrz Spoils - SpoilsSeal): godziny cechow za zrobione sztuki z pul miasta.</summary>
        internal static void OrderDone(Town t, Tally tally)
        {
            if (!On || t == null || tally == null) return;
            tally.Use(t, null);
        }

        // ------------------------------------------------------------ zlecenie gracza w toku: rezerwacja godzin co godzine czekania (recenzja)
        private static readonly float[] _poLeft = new float[Trades], _poTaken = new float[Trades];
        private static Town _poTown;
        private static int _poSerial = -1;

        private static void ClearOrder() { Array.Clear(_poLeft, 0, Trades); Array.Clear(_poTaken, 0, Trades); _poTown = null; _poSerial = -1; }

        /// <summary>Zamiast PlayerClock jako argument StartTimedWork: ile godzin zegara potrwa zlecenie, i poczatek rezerwacji jego godzin. Wolane
        /// zawsze wprost jako argument SmithMenu.StartTimedWork - ta praca dostanie numer WorkSerial + 1 (inna praca na czas konczy rezerwacje).</summary>
        internal static float PlayerStart(Tally t, float fallback)
        {
            float clock = PlayerClock(t, fallback);
            ClearOrder();
            var st = Settlement.CurrentSettlement;
            if (On && t != null && st != null && st.Town != null && t.Pieces > 0)
            {
                _poTown = st.Town; _poSerial = SmithMenu.WorkSerial + 1;
                for (int k = 0; k < Trades; k++) _poLeft[k] = t.H[k];
            }
            return clock;
        }

        /// <summary>Co godzine czekania: kazdy cech zlecenia bierze swoje wolne rece (najwyzej rece cechu na godzine i to, co zostalo do zrobienia).
        /// Praca skonczona, przerwana albo inna praca na czas - rezerwacja staje (zarezerwowane zostaje do rozliczenia w PlayerDone).</summary>
        private static void PlayerReserve()
        {
            if (_poTown == null) return;
            var st = Settlement.CurrentSettlement;
            if (!SmithMenu.WorkWaiting || SmithMenu.WorkSerial != _poSerial || st == null || st.Town != _poTown) { Array.Clear(_poLeft, 0, Trades); return; }
            for (int k = 0; k < Trades; k++)
            {
                if (_poLeft[k] <= 0f) continue;
                float x = Math.Min(_poLeft[k], Math.Min(Available(_poTown, k), Math.Max(1f, Hands(_poTown, k))));
                if (x <= 0f) continue;
                UseRaw(_poTown, k, x, false);
                _poLeft[k] -= x; _poTaken[k] += x; _stPlayerH[k] += x;
            }
        }

        // ------------------------------------------------------------ uprzaz gracza i sztuka na grzbiecie: stan z ksiegi zuzycia (procenty), nie z modyfikatora
        private static float SlotLoss(ArmouryBehavior b, int slot, EquipmentElement el)
        {
            float pm = el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f;
            float loss = 1f - Math.Max(0f, Math.Min(1f, pm));
            if (b != null && slot >= 0 && slot < 12)
            {
                float cond = b.ConditionQuietOf(slot);
                if (cond < 100f) loss = Math.Max(loss, (100f - Math.Max(0f, cond)) / 100f);
            }
            return loss;
        }

        /// <summary>Ile czesci uprzezy wezma kowale jednym zleceniem "Mend everything you wear" (stan ponizej 100, bez wrakow) - do rabatu hurtowego;
        /// 0 przy wylaczonym MendBulkEveryOrder (ArmouryBehavior.RepairCost / PlanRepair - robocizna jak dotad).</summary>
        internal static int HarnessBulkN(ArmouryBehavior b)
        {
            if (!BulkOn || b == null || Hero.MainHero == null) return 0;
            int n = 0;
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    float cond = b.ConditionQuietOf(slot);
                    if (cond >= 100f || LootPrices.HarnessWreck(el.ItemModifier, cond)) continue;
                    n++;
                }
            }
            catch (Exception e) { if (++_errHarness <= 3) Log.Error("GuildMend.HarnessBulkN (potkniecie " + _errHarness + ")", e); return 0; }
            return n;
        }
        private static int _errHarness;

        /// <summary>Godziny cechow zlecenia uprzezy: czesci z listy slots (plan kowali z materialem) albo - bez listy - kazda obita czesc bez wraku.</summary>
        internal static Tally HarnessTally(ArmouryBehavior b, List<int> slots)
        {
            var t = new Tally();
            if (!On || b == null || Hero.MainHero == null) return t;
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    if (slots != null) { if (!slots.Contains(slot)) continue; }
                    else { float cond = b.ConditionQuietOf(slot); if (cond >= 100f || LootPrices.HarnessWreck(el.ItemModifier, cond)) continue; }
                    t.AddHours(el.Item, HoursFor(el.Item, SlotLoss(b, slot, el)));
                }
            }
            catch (Exception e) { if (++_errHarness <= 3) Log.Error("GuildMend.HarnessTally (potkniecie " + _errHarness + ")", e); }
            return t;
        }

        /// <summary>Godziny jednej sztuki "Pick a piece": z sakw i magazynu - stan modyfikatora; na grzbiecie (slot >= 0) - gorszy z modyfikatora i ksiegi.</summary>
        internal static Tally PieceTally(EquipmentElement ee, int slot)
        {
            var t = new Tally();
            if (ee.Item == null) return t;
            if (slot >= 0 && On) { t.AddHours(ee.Item, HoursFor(ee.Item, SlotLoss(ArmouryBehavior.Instance, slot, ee))); return t; }
            t.Add(ee, 1);
            return t;
        }

        /// <summary>Robota kowala w tabeli godzin (bron, kolczuga, plyta, helm, metalowy kropierz) - platnerz w taborze; amunicja, sztandary,
        /// skora, sukno, luki i tarcze - nie.</summary>
        internal static bool IsSmithWork(ItemObject it) { int t; return KindOf(it, out t) >= 0 && t == Smith; }

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
            /// <summary>Ile sztuk kolejki (w tej kolejnosci) zmieszcza dzis godziny cechow - bez zmiany budzetu; do rabatu hurtowego zlecenia lorda
            /// (recenzja: rabat od sztuk, ktore cech naprawde wezmie, nie od calej zaleglosci partii).</summary>
            public int Preview(List<EquipmentElement> pieces)
            {
                var left = (float[])_left.Clone();
                int n = 0;
                foreach (var ee in pieces)
                {
                    if (ee.Item == null) continue;
                    int k = TradeOf(ee.Item); float h = Hours(ee);
                    if (left[k] + 0.0001f >= h) { left[k] -= h; n++; }
                }
                return n;
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

            /// <summary>To samo dla dziennej naprawy Twoich ludzi starej reguly (TroopSelfMend.Run, sakiewki wylaczone) - liczniki ludzi, nie AI.</summary>
            public void CommitMen(int pieces)
            {
                for (int k = 0; k < Trades; k++) { Use(_t, k, _take[k], true); _stMenH[k] += _take[k]; _take[k] = 0f; }
                _stMenPieces += pieces;
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

        /// <summary>Zlecenie dnia ludzi gracza do rabatu hurtowego: sztuki czekajace pierwszej godziny pracy w tym miescie (tez stara sciezka
        /// TroopSelfMend.Hourly przy wylaczonym MendHoursByGuild - recenzja). Rabat i tak 0 przy wylaczonym MendBulkEveryOrder (BulkCut).</summary>
        internal static int MenBulkN(Settlement st, List<ItemRosterElement> worn)
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (_bulkDay != day || _bulkTown != st.StringId)
            {
                int wornN = 0;
                foreach (var el in worn) wornN += el.Amount;
                _bulkDay = day; _bulkTown = st.StringId; _bulkN = wornN;
            }
            return _bulkN;
        }

        /// <summary>Godzina pracy cechow miasta nad obitym sprzetem Twoich ludzi (zastepuje stala 0.6 h na sztuke w TroopSelfMend.Hourly). Kazdy cech ma
        /// wlasna lawe godzin (rece cechu / WorkHoursPerManDay na godzine, przy AutoMendHalfGuild polowa); sztuka konczy sie, gdy na lawie jej cechu
        /// zbierze sie tyle godzin, ile trwa jej naprawa. Zaplata (robota po rabacie hurtowym + material) z sakiewki ludzi do kasy miasta - jak dotad.</summary>
        internal static void MenHourly(MobileParty main, Settlement st, ItemRoster armory, List<ItemRosterElement> worn)
        {
            var t = st.Town;
            int nowH = (int)CampaignTime.Now.ToHours;
            if (_menTown != st.StringId || nowH - _menLastHour > 8) { Array.Clear(_menBench, 0, Trades); _menTown = st.StringId; }
            _menLastHour = nowH;
            MenBulkN(st, worn);

            var has = new bool[Trades]; var share = new float[Trades]; var start = new float[Trades]; var doneH = new float[Trades]; var limited = new bool[Trades];
            foreach (var el in worn) if (el.EquipmentElement.Item != null) has[TradeOf(el.EquipmentElement.Item)] = true;
            float frac = HalfOn ? AutoShare : 1f;
            bool idleOn = IdleOn;
            for (int k = 0; k < Trades; k++)
            {
                start[k] = _menBench[k];
                if (!has[k]) { _menBench[k] = 0f; continue; }
                // n22f-naprawy2: przy MendIdleHandsFirst tempo na godzine = dzienny przydzial napraw automatycznych / godziny zmiany (rece bez roboty + 15% rak pracujacych)
                share[k] = Math.Min(AutoAvailable(t, k), (idleOn ? AutoDaily(t, k) : frac * Capacity(t, k)) / PerManDay);
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
        private static int _stAiPieces, _stAiCapN, _stAiCapVisits, _stMenPieces, _stPlayerPieces, _stBulkGold;
        private static float _stCarryH;
        // n22f-naprawy2: godziny doby wedlug zrodla rak (wszystkie naprawy: AI, ludzie, gracz) i z rak pracujacych (przeniesione + linie) tylko naprawy automatyczne
        private static readonly float[] _stIdleH = new float[Trades], _stFreeH = new float[Trades], _stStuckH = new float[Trades], _stLineH = new float[Trades],
                                        _stAutoLineH = new float[Trades];

        private static void ResetStats()
        {
            Array.Clear(_stIdleH, 0, Trades); Array.Clear(_stFreeH, 0, Trades); Array.Clear(_stStuckH, 0, Trades); Array.Clear(_stLineH, 0, Trades);
            Array.Clear(_stAutoLineH, 0, Trades);
            Array.Clear(_stAiH, 0, Trades); Array.Clear(_stMenH, 0, Trades); Array.Clear(_stPlayerH, 0, Trades);
            _stAiPieces = _stAiCapN = _stAiCapVisits = _stMenPieces = _stPlayerPieces = _stBulkGold = 0; _stCarryH = 0f;
        }

        /// <summary>Wolane co godzine (TroopSelfMend.Hourly): przelom doby pul + platnerz w polu.</summary>
        internal static void HourTick()
        {
            try { if (On) { Roll(); PlayerReserve(); } else ClearOrder(); }
            catch (Exception e) { if (++_errTick <= 3) Log.Error("GuildMend.HourTick (potkniecie " + _errTick + ")", e); }
            FieldArmourer.Hourly();
        }
        private static int _errTick;

        private static void LogDay()
        {
            if (_stAiPieces + _stMenPieces + _stPlayerPieces + _stAiCapN + _stBulkGold == 0 && _stCarryH <= 0f && _carry.Count == 0) { ResetStats(); return; }
            Log.Info("Naprawy wg cechow (R3-02): doba " + _day + " | AI: " + _stAiPieces + " szt., godz. " + Row5(_stAiH) + ", stop na polowie rak cechu " + _stAiCapN
                     + " szt. w " + _stAiCapVisits + " wizytach | ludzie gracza: " + _stMenPieces + " szt., godz. " + Row5(_stMenH) + " | gracz (zlecenia): "
                     + _stPlayerPieces + " szt., godz. " + Row5(_stPlayerH) + " | rabat hurtowy razem " + _stBulkGold + " zl | dlug cechow: splacono " + (int)Math.Round(_stCarryH)
                     + " h, zostaje " + (int)Math.Round(CarryLeft()) + " h w " + _carry.Count + " miastach (kowale/siodlarze/krawcy/lukarze/tarczownicy)"
                     + SourceText());
            ResetStats();
        }

        /// <summary>n22f-naprawy2: dopisek linii doby - skad szly godziny napraw (na cech) i rece cechow wedlug planow miast (raz na dobe, ok. 100 miast).</summary>
        private static string SourceText()
        {
            var pi = new float[Trades]; var pf = new float[Trades]; var ps = new float[Trades]; var pl = new float[Trades];
            int towns = 0, idleTowns = 0;
            try
            {
                foreach (var kv in _plan)
                {
                    if (kv.Key == null) continue;
                    towns++; if (kv.Value[10] > 0.5f) idleTowns++;
                    for (int k = 0; k < Trades; k++)
                    {
                        float idle, free, stuck, line; Tiers(kv.Key, k, out idle, out free, out stuck, out line);
                        pi[k] += idle; pf[k] += free; ps[k] += stuck; pl[k] += line;
                    }
                }
            }
            catch (Exception e) { if (++_errTick <= 3) Log.Error("GuildMend.SourceText (potkniecie " + _errTick + ")", e); }
            return " | zrodlo godzin (MendIdleHandsFirst " + (IdleOn ? "TAK" : "NIE") + ", k/s/k/l/t): bezczynne " + Row5(_stIdleH) + ", linie z pelnym bankiem "
                   + Row5(_stStuckH) + ", cech bez linii (rece przeniesione) " + Row5(_stFreeH) + ", rece linii " + Row5(_stLineH) + "; naprawy automatyczne z rak pracujacych "
                   + "(przeniesione + linie) " + Row5(_stAutoLineH) + ", limit " + (int)Math.Round(AutoLineShare * 100f) + "% tych rak"
                   + " | rece cechow wg planow " + towns + " miast (wlasne rece cechu, roboczodni, k/s/k/l/t): bezczynne "
                   + Row5(pi) + " (" + idleTowns + " miast), linie z pelnym bankiem " + Row5(ps) + ", cech bez linii " + Row5(pf) + ", pozostale rece linii " + Row5(pl);
        }

        private static float CarryLeft() { float x = 0f; foreach (var kv in _carry) for (int k = 0; k < Trades; k++) x += kv.Value[k]; return x; }

        private static string Row5(float[] h)
        {
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < Trades; k++) sb.Append(k > 0 ? "/" : "").Append(((int)Math.Round(h[k])).ToString());
            return sb.ToString();
        }

        internal static string F1(float v) { return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
