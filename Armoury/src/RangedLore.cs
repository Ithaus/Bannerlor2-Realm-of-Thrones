using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// WIEDZA RZEMIESLNICZA, wedle slow Jeffa: "dopiero uczysz sie pancerzy
    /// i mozesz na poczatku tylko tier 1 kuc, im wiecej zrobisz, tym wiecej
    /// odblokowujesz". Na starcie znasz WYLACZNIE wzory tieru 1 - reszta
    /// jest widoczna, ale zamknieta, jak zablokowane czesci broni w wanilii.
    /// Robota uczy: kazda wykuta sztuka daje punkty nauki (tyle, ile jej tier),
    /// a nauka sama odkrywa KOLEJNY najtanszy wzor swojej szkoly - od dolu
    /// ku gorze, az po legendy na samym koncu.
    ///
    /// Trzy osobne szkoly, bo to trzy osobne rzemiosla:
    ///   LUK    - luki (strzaly ucza lucznictwa, bo to ta sama reka)
    ///   KUSZA  - kusze (belty ucza kusznictwa)
    ///   PLATNERZ - pancerze, helmy, rekawice, nogawice, plaszcze, tarcze i kropierze
    /// Odkrycia zapisuja sie w save.
    /// </summary>
    internal static class RangedLore
    {
        // GALEZIE RZEMIOSLA. Pancerz to nie jedno rzemioslo: platnerz od helmow
        // i rymarz od kropierzy uczyli sie czego innego. Kazda galaz ma WLASNA
        // kolejke wzorow i wlasne punkty - kucie helmow posuwa helmy, kucie
        // kirysow posuwa kirysy (Jeff: "zeby nie klepac rekawic przez pol kampanii").
        internal const int SchoolNone = 0, SchoolBow = 1, SchoolXbow = 2,
                           SchoolHelm = 3, SchoolBody = 4, SchoolLegs = 5, SchoolHands = 6,
                           SchoolCape = 7, SchoolShield = 8, SchoolBarding = 9;
        internal const int SchoolCount = 10;

        // odkryte wzory (StringId); "#seeded2" = tier 1 rozdany we WSZYSTKICH galeziach
        internal static readonly List<string> Known = new List<string>();
        internal static readonly float[] Research = new float[SchoolCount];

        internal static bool IsArmourBranch(int sc) { return sc >= SchoolHelm && sc <= SchoolBarding; }

        internal static string SchoolName(int sc)
        {
            switch (sc)
            {
                case SchoolBow: return "BOWS";
                case SchoolXbow: return "CROSSBOWS";
                case SchoolHelm: return "HELMETS";
                case SchoolBody: return "BODY ARMOUR";
                case SchoolLegs: return "LEG ARMOUR";
                case SchoolHands: return "GAUNTLETS";
                case SchoolCape: return "CLOAKS";
                case SchoolShield: return "SHIELDS";
                case SchoolBarding: return "BARDING";
                default: return "-";
            }
        }

        private const string SeedMark = "#seeded2";

        /// <summary>Ktora szkola uczy sie tego wyrobu. 0 = wyrob poza nauka (amunicja, bron biala).</summary>
        internal static int SchoolOf(ItemObject it)
        {
            if (it == null) return SchoolNone;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.Bow: return SchoolBow;
                case ItemObject.ItemTypeEnum.Crossbow: return SchoolXbow;
                case ItemObject.ItemTypeEnum.HeadArmor: return SchoolHelm;
                case ItemObject.ItemTypeEnum.BodyArmor: return SchoolBody;
                case ItemObject.ItemTypeEnum.LegArmor: return SchoolLegs;
                case ItemObject.ItemTypeEnum.HandArmor: return SchoolHands;
                case ItemObject.ItemTypeEnum.Cape: return SchoolCape;
                case ItemObject.ItemTypeEnum.Shield: return SchoolShield;
                case ItemObject.ItemTypeEnum.HorseHarness: return SchoolBarding;
                default: return SchoolNone;
            }
        }

        private static bool IsProgressClass(ItemObject it) { return SchoolOf(it) != SchoolNone; }

        /// <summary>Ktora galaz uczy sie NA tym wyrobie. Strzaly ucza lucznictwa, belty kusznictwa.</summary>
        internal static int BranchTaughtBy(ItemObject it)
        {
            if (it == null) return SchoolNone;
            if (it.ItemType == ItemObject.ItemTypeEnum.Arrows) return SchoolBow;
            if (it.ItemType == ItemObject.ItemTypeEnum.Bolts) return SchoolXbow;
            return SchoolOf(it);
        }

        internal static int TierOf(ItemObject it) { return Recipes.Grade(it); }

        /// <summary>Wzor nadaje sie do nauki? Machiny, sprzet cwiczebny i smiecie - nie.</summary>
        private static bool Teachable(ItemObject it)
        {
            if (it == null || !IsProgressClass(it)) return false;
            if (IsArmourBranch(SchoolOf(it))) return !it.NotMerchandise;   // pancerz spoza kramow to legenda
            return !SmithMenu.BannedRanged(it);
        }

        private static void Seed()
        {
            try
            {
                if (Known.Contains(SeedMark)) return;
                Known.Add(SeedMark);
                int n = 0;
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!Teachable(item)) continue;
                    if (TierOf(item) != 1) continue;
                    if (!Known.Contains(item.StringId)) { Known.Add(item.StringId); n++; }
                }
                Log.Info("Wiedza rzemieslnicza: rozdano wzory tieru 1 (" + n + " sztuk).");
            }
            catch (Exception e) { Log.Error("RangedLore.Seed", e); }
        }

        /// <summary>Czy gracz zna ten wzor. Amunicja i bron biala - zawsze.
        /// Unikaty imienne (UniqueGear): znane WYLACZNIE po przetopieniu
        /// zdobytego egzemplarza (ArmouryBehavior.UniqueLore).</summary>
        internal static bool KnownOf(ItemObject it)
        {
            try
            {
                if (UniqueGear.Is(it))
                    return ArmouryBehavior.UniqueLore != null && ArmouryBehavior.UniqueLore.Contains(it.StringId);
                if (!IsProgressClass(it)) return true;
                Seed();
                return Known.Contains(it.StringId);
            }
            catch { return true; }
        }

        /// <summary>Ile wzorow znasz / ile ich jest w danym tierze danego typu.</summary>
        internal static void CountTier(ItemObject.ItemTypeEnum type, int tier, out int known, out int total)
        {
            known = 0; total = 0;
            try
            {
                Seed();
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.ItemType != type) continue;
                    if (SmithMenu.BannedRanged(item)) continue;
                    if (TierOf(item) != tier) continue;
                    total++;
                    if (KnownOf(item)) known++;
                }
            }
            catch { }
        }

        /// <summary>Ile wzorow tej szkoly znasz na tle wszystkich - do meldunku dla gracza.</summary>
        internal static void CountSchool(int school, out int known, out int total)
        {
            known = 0; total = 0;
            try
            {
                Seed();
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!Teachable(item) || SchoolOf(item) != school) continue;
                    total++;
                    if (Known.Contains(item.StringId)) known++;
                }
            }
            catch { }
        }

        /// <summary>Czy z tej rzeczy da sie jeszcze zdjac wzor (znamy = nie ma po co).</summary>
        internal static bool CanLearnFrom(ItemObject it)
        {
            try
            {
                if (it == null) return false;
                // unikat imienny: nauczalny ze zdobytego egzemplarza, poki nieopanowany
                if (UniqueGear.Is(it))
                    return ArmouryBehavior.UniqueLore == null || !ArmouryBehavior.UniqueLore.Contains(it.StringId);
                return Teachable(it) && !KnownOf(it);
            }
            catch { return false; }
        }

        /// <summary>
        /// WZOR ZDJETY Z GOTOWEJ RZECZY. Rozlozyles luk na czesci i odrysowales
        /// - wzor wchodzi do ksiegi Z POMINIECIEM kolejki nauki (za to rzecz
        /// przepada). Zwraca false, jesli i tak juz go znales.
        /// </summary>
        internal static bool Learn(ItemObject it)
        {
            try
            {
                if (it == null) return false;
                // unikat imienny idzie do WLASNEJ ksiegi (UniqueLore) - obie
                // drogi Jeffa: przetop (OnSmelted) i rozbiorka u kowala
                // (TakeApart) trafiaja tutaj
                if (UniqueGear.Is(it))
                {
                    if (ArmouryBehavior.UniqueLore != null && ArmouryBehavior.UniqueLore.Contains(it.StringId)) return false;
                    ArmouryBehavior.LearnUnique(it);
                    return true;
                }
                if (!Teachable(it)) return false;
                Seed();
                if (Known.Contains(it.StringId)) return false;
                Known.Add(it.StringId);
                Log.Info("Wiedza: wzor " + it.StringId + " zdjety z gotowej sztuki (rozlozenie).");
                return true;
            }
            catch (Exception e) { Log.Error("RangedLore.Learn", e); return false; }
        }

        /// <summary>
        /// Punkty nauki z CUDZEJ sztuki (pocieszenie za spartaczone rozlozenie,
        /// przetop znanego wzoru). Odkrycie losowe, ale NIE WYZEJ niz tier tej
        /// sztuki - z gotowej rzeczy czlowiek uczy sie najwyzej jej poziomu.
        /// </summary>
        internal static void Study(ItemObject it, float pts)
        {
            try
            {
                int sc = BranchTaughtBy(it);
                if (sc == SchoolNone) return;
                Research[sc] += pts;
                int tIt = Math.Max(1, TierOf(it));
                TryUnlockRandom(sc, 1, tIt, tIt);
                var line = Ledger(sc);
                if (!string.IsNullOrEmpty(line))
                { Log.Info(line); InformationManager.DisplayMessage(new InformationMessage(line, Colors.Cyan)); }
            }
            catch (Exception e) { Log.Error("RangedLore.Study", e); }
        }

        /// <summary>
        /// PRZETOP. Nieznany wzor: rzecz idzie do tygla, ale jej budowa zostaje
        /// w glowie - wzor wchodzi do ksiegi W CALOSCI (Jeff: "jak przetapiam
        /// jakis pancerz to ucze sie tego dokladnie"). Znany wzor: punkty jak
        /// dotad (polowa stawki kucia) i losowe odkrycie nie wyzej niz tier
        /// przetapianej sztuki.
        /// </summary>
        internal static void OnSmelted(ItemObject item)
        {
            try
            {
                if (item == null) return;
                int sc = BranchTaughtBy(item);
                if (sc == SchoolNone) return;
                if (CanLearnFrom(item))
                {
                    if (Learn(item))
                    {
                        Log.Player("Melting the " + item.Name + " down laid its making bare - the pattern is yours now.");
                        ReportSchoolOf(item);
                    }
                    return;
                }
                Study(item, MathF.Max(0.5f, TierOf(item) * 0.5f));
            }
            catch (Exception e) { Log.Error("RangedLore.OnSmelted", e); }
        }

        /// <summary>Szkola rzeczy - do meldunku po rozlozeniu.</summary>
        internal static void ReportSchoolOf(ItemObject it)
        {
            try
            {
                int sc = SchoolOf(it);
                if (sc == SchoolNone) return;
                var line = Ledger(sc);
                if (!string.IsNullOrEmpty(line))
                { Log.Info(line); InformationManager.DisplayMessage(new InformationMessage(line, Colors.Cyan)); }
            }
            catch { }
        }

        /// <summary>
        /// KSIEGA WZOROW. Ile wzorow znasz w kazdej szkole i tierze, ile masz
        /// punktow i ILE JESZCZE ROBOTY do nastepnego wzoru. Jeff: "ile musze
        /// wykuc, zeby odblokowac tier 2?" - odpowiedz ma stac w grze, nie
        /// w mojej glowie.
        /// </summary>
        internal static string Ledger(int school)
        {
            try
            {
                Seed();
                if (school <= SchoolNone || school >= SchoolCount) return null;
                float pts = Research[school];
                int known, total;
                CountSchool(school, out known, out total);

                // najtanszy nieznany tier = najnizsza mozliwa cena nastepnego losowania
                int nextTier = 0;
                var perTier = new int[8]; var knownTier = new int[8];
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!Teachable(item) || SchoolOf(item) != school) continue;
                    int ti = TierOf(item); if (ti < 1) ti = 1; if (ti > 6) ti = 6;
                    perTier[ti]++;
                    if (Known.Contains(item.StringId)) { knownTier[ti]++; continue; }
                    if (nextTier == 0 || ti < nextTier) nextTier = ti;
                }

                var sb = new System.Text.StringBuilder();
                sb.Append(SchoolName(school)).Append(": ").Append(known).Append("/").Append(total).Append(" patterns");
                for (int t = 1; t <= 6; t++)
                    if (perTier[t] > 0) sb.Append(" | t").Append(t).Append(" ").Append(knownTier[t]).Append("/").Append(perTier[t]);
                sb.Append("  --  ").Append(pts.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(" pts");
                if (nextTier == 0) sb.Append(", nothing left to learn.");
                else
                {
                    // odkrycia sa losowe, cena = 3 x tier sztuki, przy ktorej
                    // pracujesz - obiecujemy tylko najnizsza mozliwa cene
                    float cost = 3f * nextTier;
                    float miss = cost - pts; if (miss < 0f) miss = 0f;
                    sb.Append(", next pattern: random, unlock costs 3 x the tier you work (cheapest ")
                      .Append((int)cost).Append(") - ").Append(miss.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))
                      .Append(" pts short");
                    int top = 1; for (int t = 6; t >= 1; t--) if (knownTier[t] > 0) { top = t; break; }
                    int per = top < 1 ? 1 : top;
                    int crafts = (int)Math.Ceiling(miss / per);
                    sb.Append(" = ").Append(crafts).Append(" more tier-").Append(top).Append(" crafts.");
                }
                return sb.ToString();
            }
            catch { return null; }
        }

        /// <summary>Trzy linijki do logu i na ekran - stan nauki wszystkich szkol.</summary>
        internal static void ReportLedger(bool onScreen)
        {
            try
            {
                for (int sc = SchoolBow; sc < SchoolCount; sc++)
                {
                    var line = Ledger(sc);
                    if (string.IsNullOrEmpty(line)) continue;
                    Log.Info(line);
                    if (onScreen) InformationManager.DisplayMessage(new InformationMessage(line, Colors.Cyan));
                }
            }
            catch { }
        }

        /// <summary>
        /// Po udanej robocie: punkty nauki i proba odkrycia. Odkrycie jest
        /// LOSOWE sposrod nieznanych wzorow szkoly o tierze ROWNYM LUB WYZSZYM
        /// niz wykuta sztuka (Jeff: "jak kuje, moge sie losowo nauczyc tieru
        /// danej dziedziny lub wyzej, jak przy broniach"). Kolejny wzor kosztuje
        /// 3 x jego tier punktow. Strzaly ucza lucznictwa, belty kusznictwa.
        /// </summary>
        internal static void OnCrafted(ItemObject item)
        {
            try
            {
                if (item == null) return;
                float pts = Math.Max(1, TierOf(item));
                int sc = BranchTaughtBy(item);
                if (sc == SchoolNone) return;
                Research[sc] += pts;
                int tThis = Math.Max(1, TierOf(item));
                TryUnlockRandom(sc, tThis, 6, tThis);
                // po kazdej robocie mowimy, ile jeszcze do nastepnego wzoru
                if (sc != SchoolNone)
                {
                    var line = Ledger(sc);
                    if (!string.IsNullOrEmpty(line))
                    { Log.Info(line); InformationManager.DisplayMessage(new InformationMessage(line, Colors.Cyan)); }
                }
            }
            catch (Exception e) { Log.Error("RangedLore.OnCrafted", e); }
        }

        /// <summary>
        /// Losowe odkrycie z zakresu tierow [minTier..maxTier]. CENA za kazde
        /// losowanie liczy sie od sztuki, PRZY KTOREJ sie uczysz (kutej albo
        /// przetapianej: 3 x costTier) - nie od wylosowanego wzoru. Stara cena
        /// wg wylosowanego przepuszczala tylko najtansze trafienia i odkrycia
        /// szly "od dolu jeden po drugim" (Jeff 27.08); teraz kazdy wzor
        /// z zakresu ma ROWNA szanse. MBRandom, spojnie z reszta kampanii.
        /// </summary>
        private static void TryUnlockRandom(int school, int minTier, int maxTier, int costTier)
        {
            try
            {
                Seed();
                float cost = 3f * Math.Max(1, costTier);
                var pool = new List<ItemObject>();
                while (true)
                {
                    if (Research[school] < cost) return;            // jeszcze sie ucz
                    pool.Clear();
                    foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    {
                        if (!Teachable(item) || SchoolOf(item) != school) continue;
                        if (Known.Contains(item.StringId)) continue;
                        int ti = TierOf(item);
                        if (ti < minTier || ti > maxTier) continue;
                        pool.Add(item);
                    }
                    if (pool.Count == 0) return;                    // nic do odkrycia w tym zakresie
                    var next = pool[MBRandom.RandomInt(pool.Count)];
                    int bestTier = Math.Max(1, TierOf(next));
                    Research[school] -= cost;
                    Known.Add(next.StringId);
                    // baner jak przy vanillowym odblokowaniu czesci broni (kuznia 1:1
                    // krok 2) + dotychczasowa zielona linijka w dzienniku czatu
                    try
                    {
                        MBInformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject(
                            "{=!}New pattern unlocked: " + next.Name), 0, null, null,
                            "event:/ui/notification/crafting");
                    }
                    catch { }
                    InformationManager.DisplayMessage(new InformationMessage(
                        "At the bench you worked out the making of the " + next.Name + " - new pattern unlocked.",
                        Colors.Green));
                    Log.Info("Wiedza: odkryto losowo " + next.StringId + " (" + SchoolName(school) + ", t" + bestTier
                             + ", zakres " + minTier + "-" + maxTier + ").");
                }
            }
            catch (Exception e) { Log.Error("RangedLore.TryUnlockRandom", e); }
        }

        // ---- zapis: "id|id|...;pkt1;pkt2;...;pkt9" (stary format 4-polowy wczytujemy nadal) ----
        internal static string Export()
        {
            try
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var sb = new System.Text.StringBuilder(string.Join("|", Known.ToArray()));
                for (int i = 1; i < SchoolCount; i++) sb.Append(";").Append(Research[i].ToString("0.#", ci));
                return sb.ToString();
            }
            catch { return ""; }
        }

        internal static void Import(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var nf = System.Globalization.NumberStyles.Float;
                var parts = data.Split(';');
                Known.Clear();
                foreach (var id in parts[0].Split('|'))
                    if (!string.IsNullOrEmpty(id)) Known.Add(id);
                for (int i = 1; i < SchoolCount; i++) Research[i] = 0f;
                if (parts.Length == 4)
                {
                    // STARY ZAPIS: jedna wspolna pula platnerska. Oddajemy ja kirysom -
                    // tam trafia najwiecej roboty, a rozbicie na galezie i tak zaczyna sie tu.
                    float f;
                    if (float.TryParse(parts[1], nf, ci, out f)) Research[SchoolBow] = f;
                    if (float.TryParse(parts[2], nf, ci, out f)) Research[SchoolXbow] = f;
                    if (float.TryParse(parts[3], nf, ci, out f)) Research[SchoolBody] = f;
                }
                else
                {
                    for (int i = 1; i < SchoolCount && i < parts.Length; i++)
                    {
                        float f;
                        if (float.TryParse(parts[i], nf, ci, out f)) Research[i] = f;
                    }
                }
                // stary zapis znal tylko luki i kusze - platnerstwo trzeba doseedowac
                if (Known.Contains("#seeded") && !Known.Contains(SeedMark)) Known.Remove("#seeded");
            }
            catch { }
        }
    }
}
