using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m96-wzory (20.10, Jeff: "moge stworzyc luki tier 6 od poczatku gry, zobacz, czy nie ma wiecej takich bledow") - bramy wiedzy kuzni:
    ///  1. LOSOWANIE WEDLUG REKI (PatternDrawBySkill): nowy wzor z nauki najwyzej o tier ponad to, co kowal juz moze wykuc
    ///     (prog SmithingSkillPerTier), legenda tylko blisko LegendarySkillNeeded. Bylo: od tieru sztuki do 6 z rowna szansa -
    ///     po 3 robotach tieru 1 wzor tieru 6 (log 13-28-19 linia 6239: GRE_vlandic_arrows t4 po 3 lukach t1).
    ///  2. PROG TARCZ W CRAFT (ShieldCraftSkillGate): tarcza w zakladce CRAFT BK wymaga tego samego Smithing co w naszym menu
    ///     kowala (Recipes.For -> Need(tier, 0)); bylo: brama ArmourQuality tylko dla HasArmorComponent, tarcza szla droga BK bez progu.
    ///  3. UNIKATY ROT TYLKO Z ORYGINALU (ForgeUniquesByStudy): przedmiot z RotUniques w klasie nauki (luki, strzaly, zbroje)
    ///     jest wzorem UniqueLore jak UniqueGear - nie rozdaje sie, nie losuje, uczy sie z przetopu/rozbiorki zdobytego egzemplarza.
    ///     Bylo: wielkie luki ROT w puli nauki (Teachable dla luku nie patrzyl na unikaty), a imienne zbroje spoza UniqueGear
    ///     (lyanna_armor, loras_*...) zamkniete na zawsze, bo Teachable (NotMerchandise) i UniqueGear.Is - obie drogi - je odrzucaly.
    ///  4. KAFELEK CRAFT (PatternTileShowsSkill): BlockText dla ForgeView - "znany, ale za trudny" (prog i jedyna legenda),
    ///     ta sama regula co Forge.Smith / ArmourQuality.Begin / brama tarcz.
    ///  5. (po recenzjach) KUSZA WEDLUG SILY W KSIEDZE (XbowPatternTierByPower): tier wzoru kuszy = tier, o ktory kuznia pyta Smithing
    ///     (XbowPrice.ForgeTier - luk tej samej sily, XbowForgeEquivTier). Log Jeffa 13-28-19 "Kusze wedlug luku": crossbow_g/d/e t1->t6,
    ///     c t1->t5, b t1->t3, a t1->t2, f t2->t6 - szesc z nich rozdawane jako tier 1 i znane od dnia 1, jak Ravens' Teeth.
    ///  6. (po recenzjach) LUKI Z RZADKIEGO MATERIALU (RareBowsNotForged): czardrzewo, smocza kosc, zlotoserce, luk olbrzyma, strzaly
    ///     Ravens' Teeth - wycenione jak skonczony zasob (HistExoticBows, np. ravens_teeth_longbow 5000 d), a kwit kuzni to drewno i kilka
    ///     sztabek; nie ma wzoru, kowadlo odmawia (Forge.LegendAllowed - wspolna brama obu drog kucia), kafelek mowi dlaczego.
    /// Wszystko na watku glownym (UI i kampania), bez alokacji w petlach listy (prog z pamieci podrecznej na sesje i ustawienia).
    /// </summary>
    internal static class PatternGate
    {
        private static Settings S { get { return Settings.Current; } }
        internal static bool DrawOn { get { var s = S; return s != null && s.PatternDrawBySkill; } }
        internal static bool ShieldOn { get { var s = S; return s != null && s.ShieldCraftSkillGate; } }
        internal static bool UniquesOn { get { var s = S; return s != null && s.ForgeUniquesByStudy; } }
        internal static bool TileOn { get { var s = S; return s != null && s.PatternTileShowsSkill; } }
        internal static bool XbowTierOn { get { var s = S; return s != null && s.XbowPatternTierByPower; } }

        // ------------------------------------------------------------ 5. tier wzoru kuszy wedlug sily
        /// <summary>Tier wzoru (ksiega, rozdanie, losowanie, etykieta): kusza - tier, o ktory kuznia pyta Smithing (XbowPrice.ForgeTier, 0 = brak
        /// przeliczenia albo XbowForgeEquivTier NIE), reszta - tier gry (Recipes.Grade). Klucz wylaczony: tier gry jak dotad.</summary>
        internal static int LoreTier(ItemObject it)
        {
            int g = Recipes.Grade(it);
            if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Crossbow || !XbowTierOn) return g;
            int f = XbowPrice.ForgeTier(it);
            if (f > g) return f > 6 ? 6 : f;
            return g;
        }

        // ------------------------------------------------------------ 6. luki z rzadkiego materialu
        internal const string RareWhy = "No smith can make this any more - the stuff it is made of cannot be had. Only the ones already in the world remain.";
        private static readonly HashSet<string> _rare = new HashSet<string>(StringComparer.Ordinal);
        private static string _rareFrom;

        /// <summary>Przedmiot z listy RareBowsIds przy kluczu RareBowsNotForged (lista z ustawien, przeliczana tylko przy zmianie napisu).</summary>
        internal static bool RareStuff(ItemObject it)
        {
            try
            {
                if (it == null || it.StringId == null) return false;
                var s = S;
                string src = s != null && s.RareBowsNotForged ? s.RareBowsIds : null;
                if (string.IsNullOrEmpty(src)) return false;
                if (!ReferenceEquals(src, _rareFrom))
                {
                    _rare.Clear();
                    foreach (var id in src.Split(','))
                    {
                        var t = id.Trim();
                        if (t.Length > 0) _rare.Add(t);
                    }
                    _rareFrom = src;
                }
                return _rare.Contains(it.StringId);
            }
            catch { return false; }
        }

        // liczniki od startu sesji (linia LoreSeedFix + linia doby nie jest potrzebna - zdarzenia rzadkie, kazde z wlasna linia do 20 sztuk)
        internal static int Draws, DrawsCapped, ShieldRefused, ShieldPassed, Stumbles;
        private static int _drawLines, _shieldLines;

        internal static void ResetCounters()
        {
            Draws = 0; DrawsCapped = 0; ShieldRefused = 0; ShieldPassed = 0; Stumbles = 0; _drawLines = 0; _shieldLines = 0;
            _need.Clear(); _needFor = null;
        }

        private static void Stumble(string where, Exception e)
        {
            Stumbles++;
            if (Stumbles <= 5) Log.Error(where, e);
        }

        // ------------------------------------------------------------ 3. unikaty
        private static bool RotUnique(ItemObject it)
        {
            return it != null && it.StringId != null && RotUniques.Ids.Contains(it.StringId) && RangedLore.SchoolOf(it) != RangedLore.SchoolNone;
        }

        /// <summary>Unikat kuzni: wzor tylko z ksiegi UniqueLore. UniqueGear jak dotad + (klucz) unikaty ROT z klasy nauki.</summary>
        internal static bool IsForgeUnique(ItemObject it)
        {
            if (UniqueGear.Is(it)) return true;
            return UniquesOn && RotUnique(it);
        }

        /// <summary>Teachable = false: unikat ROT nie wchodzi do rozdania, losowania ani licznikow szkol.</summary>
        internal static bool StudyOnly(ItemObject it) { return UniquesOn && RotUnique(it); }

        // ------------------------------------------------------------ 1. losowanie wedlug reki
        private static int SkillOf(Hero smith)
        {
            try { var h = smith ?? Hero.MainHero; return h != null ? h.GetSkillValue(DefaultSkills.Crafting) : 0; }
            catch { return 0; }
        }

        private static int PerTier() { var s = S; return s != null ? Math.Max(1, s.SmithingSkillPerTier) : 35; }

        /// <summary>Najwyzszy tier, jaki moze wypasc z nauki: tier, ktory reka kowala juz unosi (skill / prog na tier + 1), i jeden wyzej.
        /// Smithing 30 przy progu 35: tier 2; 35: tier 3; 140: tier 6. Nigdy ponizej tieru wykutej sztuki. Klucz wylaczony: 6 (jak dotad).</summary>
        internal static int DrawCap(int minTier, Hero smith)
        {
            if (!DrawOn) return 6;
            int skill = SkillOf(smith);
            int cap = SkillCap(skill);
            if (cap < minTier) cap = minTier;
            if (cap > 6) cap = 6;
            Draws++;
            if (cap < 6)
            {
                DrawsCapped++;
                if (_drawLines < 20)
                {
                    _drawLines++;
                    Log.Info("Wiedza (m96-wzory): losowanie wedlug reki - kowal " + Who(smith) + " Smithing " + skill + ", prog na tier " + PerTier()
                             + " -> zakres tierow " + minTier + "-" + cap + " (bylo " + minTier + "-6) | robot z nauka od startu sesji " + Draws + ", z przycietym zakresem " + DrawsCapped + ".");
                }
            }
            return cap;
        }

        /// <summary>Gorna granica losowania dla reki o tym Smithing (bez dolnej granicy tieru wykutej sztuki): skill / prog na tier + 2, najwyzej 6.
        /// Jedna regula dla DrawCap i naprawy zapisu 2 (LoreSeedFix.RepairReach, m96-wzory2).</summary>
        internal static int SkillCap(int skill)
        {
            int cap = (skill < 0 ? 0 : skill) / PerTier() + 2;
            return cap > 6 ? 6 : cap;
        }

        internal static int PerTierNow() { return PerTier(); }

        /// <summary>Smithing kowala dla filtra legend w puli (int.MaxValue = bez filtra: klucz wylaczony albo nauka z cudzej sztuki).</summary>
        internal static int LegendReach(Hero smith)
        {
            if (!DrawOn) return int.MaxValue;
            return SkillOf(smith);
        }

        /// <summary>Legenda (Recipes.IsLegendary) do puli dopiero, gdy kowalowi brakuje najwyzej jednego progu tieru do LegendarySkillNeeded.</summary>
        internal static bool LegendOutOfReach(ItemObject it, int skill)
        {
            try
            {
                if (it == null || !it.NotMerchandise) return false;   // tanio: legenda to zawsze towar spoza kramow
                if (!Recipes.IsLegendary(it)) return false;
                var s = S;
                int need = s != null ? s.LegendarySkillNeeded : 250;
                return skill + PerTier() < need;
            }
            catch { return false; }
        }

        private static string Who(Hero h)
        {
            try { var x = h ?? Hero.MainHero; return x != null ? x.StringId : "-"; } catch { return "-"; }
        }

        // ------------------------------------------------------------ 2. prog tarcz w CRAFT
        /// <summary>Kowal wybrany w ekranie CRAFT (CraftingMixin.crafting.CurrentCraftingHero.Hero); null = nie wiadomo (wtedy gracz).</summary>
        internal static Hero SmithOf(object mixin)
        {
            try
            {
                if (mixin == null) return null;
                var vm = Traverse.Create(mixin).Field("crafting").GetValue();
                var hv = vm != null ? Traverse.Create(vm).Property("CurrentCraftingHero").GetValue() : null;
                return hv != null ? Traverse.Create(hv).Property("Hero").GetValue<Hero>() : null;
            }
            catch { return null; }
        }

        /// <summary>Wolane z ArmourQuality.CraftPrefix dla tarczy (tryb pancerzy, CraftOn). false = odmowa: BK nie kuje, nic nie schodzi,
        /// ArmourQuality.Refused = true (postfiks FletchForge nie uczy wzorow i nie pokazuje okna wyniku).</summary>
        internal static bool ShieldCraftPrefix(object mixin, ItemObject item)
        {
            try
            {
                if (!ShieldOn || item == null || item.ItemType != ItemObject.ItemTypeEnum.Shield) return true;
                var smith = SmithOf(mixin);
                int skill = SkillOf(smith);
                int need = NeedOf(item);
                if (skill >= need) { ShieldPassed++; return true; }
                ShieldRefused++;
                ArmourQuality.Refused = true;
                Log.Player("That is beyond your hand. You need " + need + " Smithing.", true);
                if (_shieldLines < 20)
                {
                    _shieldLines++;
                    Log.Info("Wiedza (m96-wzory): tarcza w CRAFT - odmowa " + item.StringId + " t" + Recipes.Grade(item) + " wymaga Smithing " + need
                             + ", kowal " + Who(smith) + " ma " + skill + " (nic nie zeszlo) | odmow od startu sesji " + ShieldRefused + ", przepuszczonych " + ShieldPassed + ".");
                }
                return false;
            }
            catch (Exception e) { Stumble("PatternGate.ShieldCraftPrefix", e); return true; }
        }

        // ------------------------------------------------------------ 4. kafelek CRAFT (ForgeView przez refleksje)
        private static readonly Dictionary<ItemObject, int> _need = new Dictionary<ItemObject, int>();
        private static Campaign _needFor;
        private static long _needStamp = -1;
        private static readonly Dictionary<int, string> _needText = new Dictionary<int, string>();

        /// <summary>Prog umiejetnosci wzoru (Recipes.For().SkillNeeded, z legenda i kusza wedlug luku) - raz na sztuke w sesji; pamiec od nowa przy
        /// innej kampanii albo zmianie w MCM ustawien, z ktorych liczy sie prog (SmithingSkillPerTier, LegendarySkillNeeded, XbowForgeEquivTier).</summary>
        private static int NeedOf(ItemObject item)
        {
            var c = Campaign.Current;
            var s = S;
            long stamp = s == null ? 0 : ((long)s.SmithingSkillPerTier * 100000L + (long)s.LegendarySkillNeeded * 10L + (s.XbowForgeEquivTier ? 1L : 0L));
            if (!ReferenceEquals(c, _needFor) || stamp != _needStamp) { _need.Clear(); _needFor = c; _needStamp = stamp; }
            int n;
            if (_need.TryGetValue(item, out n)) return n;
            n = Recipes.For(item).SkillNeeded;
            _need[item] = n;
            return n;
        }

        /// <summary>
        /// Dla ForgeView (ArmourItemMixin, refleksja): null = znany wzor, ktory ten kowal moze zaczac; napis = znany, ale kowadlo odmowi.
        /// Ta sama regula co droga kucia w CRAFT: strzeleckie - Forge.Smith (jedyna legenda, prog); zbroja - ArmourQuality.Begin (prog, gdy CraftOn);
        /// tarcza - ShieldCraftPrefix (prog, gdy klucz i CraftOn). Materialow nie sprawdzamy - BK pokazuje je sam pod przyciskiem.
        /// </summary>
        public static string BlockText(ItemObject item, Hero smith)
        {
            try
            {
                if (!TileOn || item == null) return null;
                if (RareStuff(item)) return "RARE STUFF - CANNOT BE FORGED";   // po recenzjach: takze bez wzoru - kafelek nie obiecuje nauki
                if (!RangedLore.KnownOf(item)) return null;   // nieznany - zostaje "PATTERN NOT LEARNED"
                bool ranged = FletchForge.RangedType(item);
                bool gated = ranged
                    || (item.HasArmorComponent && ArmourQuality.CraftOn)
                    || (item.ItemType == ItemObject.ItemTypeEnum.Shield && ShieldOn && ArmourQuality.CraftOn);
                if (!gated) return null;
                if (ranged && item.NotMerchandise)
                {
                    string why;
                    if (!Forge.LegendAllowed(item, out why)) return "PATTERN KNOWN - LEGEND ALREADY FORGED";
                }
                int need = NeedOf(item);
                if (SkillOf(smith) >= need) return null;
                string t;
                if (!_needText.TryGetValue(need, out t)) { t = "PATTERN KNOWN - NEEDS " + need + " SMITHING"; _needText[need] = t; }
                return t;
            }
            catch (Exception e) { Stumble("PatternGate.BlockText", e); return null; }
        }

        internal static string Keys()
        {
            return "losowanie wg reki " + (DrawOn ? "TAK" : "NIE") + ", prog tarcz w CRAFT " + (ShieldOn ? (ArmourQuality.CraftOn ? "TAK" : "TAK (uspiony - CraftOn NIE)") : "NIE")
                   + ", unikaty ROT tylko z oryginalu " + (UniquesOn ? "TAK" : "NIE") + ", kafelek z progiem " + (TileOn ? "TAK" : "NIE")
                   + ", kusza wedlug sily w ksiedze " + (XbowTierOn ? (XbowPrice.Active ? "TAK" : "TAK (uspiony - kusze nie przeliczone w tej sesji)") : "NIE")
                   + ", rzadki material bez kucia " + RareCount();
        }

        /// <summary>Do linii startu: ile przedmiotow z listy RareBowsIds istnieje w grze (0 = lista pusta albo nieznane id).</summary>
        private static string RareCount()
        {
            try
            {
                var s = S;
                if (s == null || !s.RareBowsNotForged) return "NIE";
                if (string.IsNullOrEmpty(s.RareBowsIds)) return "TAK (lista pusta)";
                int n = 0, all = 0;
                foreach (var id in s.RareBowsIds.Split(','))
                {
                    var t = id.Trim();
                    if (t.Length == 0) continue;
                    all++;
                    if (TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(t) != null) n++;
                }
                return "TAK (" + n + " z " + all + " id w grze)";
            }
            catch { return "?"; }
        }
    }
}
