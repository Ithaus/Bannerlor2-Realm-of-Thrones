using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRZETOP WEDLUG MATERIALU (n20-kuznia, Jeff 19.10: "crude nie powinno byc potrzebne do wytapiania ... z broni z crude robi sie stal").
    /// DOWOD (dekompilacja BannerKings.dll, BKSmithingModel.GetSmeltingOutputForItem): BK bierze wynik gry (DefaultSmithingModel - materialy czesci,
    /// najwyzszy gatunek traci stopien, 1 wegiel), potem przycina metal do GetMetalMax(klasa broni) = 1-3 sztaby, odejmujac po jednej sztabie OD NAJNIZSZEGO
    /// gatunku (petla j = 2..6) - z broni z czesciami z zelaza kutego i jednym okuciem ze stali zostaje stal; na koniec "if (out[1] == 0) out[1]++" - sztaba
    /// surowki z niczego przy kazdej broni (takze przy palce bez metalu i przy sztyletcie z 1 sztaby). Log Jeffa 19.10 15:22 / 15:56: przetop Arakh (czesci:
    /// 3 stal szlachetna + 1 zelazo kute) -> BK: 1 surowka + 2 stal szlachetna; Broad Kaskara (1 zelazo, 4 stal, 5 stal szl.) -> 1 surowka + 2 stal + 1 stal szl.
    /// REGULA (klucz SmeltWeaponByParts): metal wraca w GATUNKACH czesci broni i w ich proporcji (najwieksze reszty, remis - nizszy gatunek), w ilosci jak przy
    /// przetopie pancerza (udzial SmeltingReturnShare + Smithing x SmeltingSkillBonus, najwyzej polowa - Jeff 30.08), nie wiecej niz sufit BK dla tej klasy broni
    /// (GetMetalMax, gdy czynny model go ma); co najmniej 1 sztaba, gdy bron ma metal. Bez surowki z niczego i bez obnizania gatunku. Reszta metalu idzie w zgorzeline
    /// (ujscie jak w BK i grze). Drewno (SmeltNoWood) i wegiel (SmithCoal) bez zmian.
    /// LICZENIE RAZ (klucz SmeltArmourOnce): SmeltTab.OutputPostfix jest wpiety w KAZDY model z wlasna metoda (DefaultSmithingModel i BKSmithingModel), a BK wola
    /// metode bazowa - postfiks bazy dokladal odzysk pancerza, a postfiks BK dokladal go drugi raz (2 x odzysk), a BK do kazdej tarczy dokladal surowke z niczego.
    /// Teraz tylko wywolanie najbardziej zewnetrzne (metoda czynnego modelu, ktora wpielismy) przypisuje odzysk; wewnetrzne nie dokladaja nic.
    /// Wylaczone klucze = jak dotad (BK / podwojnie).
    /// </summary>
    internal static class SmeltTruth
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Names = { "ore", "crude", "wrought", "iron", "steel", "fine", "castle" };
        private static Type _modelType, _outerType;
        private static MethodInfo _metalMax; private static Type _metalMaxFor;
        private static int _errors;
        private static readonly HashSet<string> _logged = new HashSet<string>();
        internal static int WeaponsDone;                      // przetopy broni od startu sesji (linie przetopu)

        internal static bool WeaponOn { get { var s = Settings.Current; return s != null && s.SmeltWeaponByParts; } }
        internal static bool ArmourOnceOn { get { var s = Settings.Current; return s != null && s.SmeltArmourOnce; } }

        private static void Stumble(string where, Exception e)
        {
            _errors++;
            if (_errors <= 5 || _errors % 100 == 0) Log.Error("SmeltTruth." + where + " (potkniecie " + _errors + ")", e);
        }

        /// <summary>Czy to wywolanie metody czynnego modelu (najbardziej zewnetrzne z wpietych przez SmeltTab) - wewnetrzne (wolanie bazy przez BK) nie dokladaja odzysku.</summary>
        internal static bool IsOuter(MethodBase orig)
        {
            try
            {
                if (orig == null) return true;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                if (model == null) return true;
                var mt = model.GetType();
                if (mt != _modelType)
                {
                    Type outer = null;
                    for (var t = mt; t != null && outer == null; t = t.BaseType) if (SmeltTab.PatchedTypes.Contains(t)) outer = t;
                    _outerType = outer; _modelType = mt;
                }
                return _outerType == null || orig.DeclaringType == _outerType;
            }
            catch (Exception e) { Stumble("IsOuter", e); return true; }
        }

        /// <summary>Metal czesci broni wedlug gatunku (0 ruda .. 6 stal zamkowa) - to, co kosztowalo wykucie (GetSmithingCostsForWeaponDesign).</summary>
        internal static int[] PartsMetal(ItemObject item)
        {
            var raw = new int[7];
            if (item == null || item.WeaponDesign == null || item.WeaponDesign.UsedPieces == null) return raw;
            foreach (var el in item.WeaponDesign.UsedPieces)
            {
                if (el == null || !el.IsValid || el.CraftingPiece == null || el.CraftingPiece.MaterialsUsed == null) continue;
                foreach (var m in el.CraftingPiece.MaterialsUsed)
                {
                    int i = (int)m.Item1;
                    if (i >= 1 && i <= 6 && m.Item2 > 0) raw[i] += m.Item2;
                }
            }
            return raw;
        }

        /// <summary>Sufit BK dla klasy broni (BKSmithingModel.GetMetalMax, 1-3 sztaby); -1, gdy czynny model go nie zna (wtedy bez sufitu).</summary>
        private static int ClassCap(object model, ItemObject item)
        {
            try
            {
                if (model == null || item == null || item.WeaponComponent == null || item.WeaponComponent.PrimaryWeapon == null) return -1;
                var mt = model.GetType();
                if (mt != _metalMaxFor)
                {
                    _metalMax = mt.GetMethod("GetMetalMax", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(WeaponClass) }, null);
                    _metalMaxFor = mt;
                }
                if (_metalMax == null) return -1;
                return (int)_metalMax.Invoke(model, new object[] { item.WeaponComponent.PrimaryWeapon.WeaponClass });
            }
            catch (Exception e) { Stumble("ClassCap", e); return -1; }
        }

        /// <summary>Udzial odzysku jak przy przetopie pancerza (SmeltTab.YieldFor): SmeltingReturnShare + Smithing x bonus, najwyzej 0.5.</summary>
        internal static float Share()
        {
            var s = Settings.Current;
            int skill = 0;
            try { skill = Hero.MainHero != null ? Hero.MainHero.GetSkillValue(DefaultSkills.Crafting) : 0; } catch { }
            float share = s != null ? s.SmeltingReturnShare + skill * s.SmeltingSkillBonus : 0.45f;
            return MathF.Max(0f, MathF.Min(share, 0.5f));
        }

        /// <summary>Metal z przetopu broni wedlug czesci (indeksy 1..6). cap - sufit klasy (-1 bez sufitu).</summary>
        internal static int[] WeaponMetal(int[] raw, int cap, float share)
        {
            var outp = new int[7];
            int total = 0;
            for (int g = 1; g <= 6; g++) total += raw[g];
            if (total <= 0) return outp;
            int n = Math.Max(1, (int)Math.Floor(total * share + 1e-6));
            if (cap > 0 && n > cap) n = cap;
            if (n > total) n = total;
            // najwieksze reszty: czesc calkowita, potem reszta - przy remisie nizszy gatunek (strata idzie w jakosc, nie odwrotnie)
            int given = 0;
            var rem = new long[7];
            for (int g = 1; g <= 6; g++)
            {
                long num = (long)raw[g] * n;
                outp[g] = (int)(num / total);
                rem[g] = num % total;
                given += outp[g];
            }
            while (given < n)
            {
                int best = -1;
                for (int g = 1; g <= 6; g++)
                    if (raw[g] > outp[g] && (best < 0 || rem[g] > rem[best])) best = g;
                if (best < 0) break;
                outp[best]++; rem[best] = -1; given++;
            }
            return outp;
        }

        /// <summary>Z SmeltTab.OutputPostfix (wywolanie zewnetrzne, bron z projektem): metal wyniku wedlug czesci.</summary>
        internal static void ApplyWeapon(ItemObject item, int[] result)
        {
            try
            {
                if (!WeaponOn || item == null || item.WeaponDesign == null || result == null || result.Length < 7) return;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                var m = WeaponMetal(PartsMetal(item), ClassCap(model, item), Share());
                for (int g = 1; g <= 6; g++) result[g] = m[g];
            }
            catch (Exception e) { Stumble("ApplyWeapon", e); }
        }

        /// <summary>Metal wyniku przetopu slowami logu (crude 1, wrought 2 ...).</summary>
        internal static string Metal(int[] a) { return a != null ? Fmt(a, 1, Math.Min(6, a.Length - 1)) : "-"; }

        private static string Fmt(int[] a, int from, int to)
        {
            var sb = new StringBuilder();
            for (int g = from; g <= to && g < a.Length; g++)
            {
                if (a[g] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Names[g]).Append(' ').Append(a[g].ToString(Inv));
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>Linia po prawdziwym przetopie broni (SmeltTab.DoSmeltingPostfix, raz na wzor w sesji): czesci -> wynik.</summary>
        internal static void OnWeaponSmelted(ItemObject item)
        {
            try
            {
                if (!WeaponOn || item == null || item.WeaponDesign == null) return;
                WeaponsDone++;
                if (!_logged.Add(item.StringId ?? "")) return;
                var model = Campaign.Current.Models.SmithingModel;
                var raw = PartsMetal(item);
                var o = model.GetSmeltingOutputForItem(item);
                Log.Info("Przetop broni (n20-kuznia): " + item.StringId + " | czesci: " + Fmt(raw, 1, 6) + " -> wynik: " + Fmt(o, 1, 6)
                         + " | udzial " + (Share() * 100f).ToString("0", Inv) + "%, sufit klasy " + ClassCap(model, item).ToString(Inv)
                         + " | przetopow broni w sesji " + WeaponsDone.ToString(Inv) + " (kolejne sztuki tego wzoru bez linii)");
            }
            catch (Exception e) { Stumble("OnWeaponSmelted", e); }
        }

        /// <summary>
        /// Start sesji (po SmithCoal.SessionStart): samosprawdzenie na wszystkich broniach z projektem - wynik czynnego modelu (z naszymi latkami) wobec czesci:
        /// gatunek wyzszy niz najwyzszy w czesciach (ma byc 0), surowka bez surowki w czesciach (ma byc 0), wiecej metalu niz polowa czesci (ma byc 0 poza
        /// broniami z 1 sztaba); pancerz i tarcza: odzysk z modelu = odzysk z naszej receptury (liczony raz).
        /// </summary>
        internal static void SessionStart()
        {
            WeaponsDone = 0; _logged.Clear(); _modelType = null; _metalMaxFor = null;
            var sb = new StringBuilder();
            try
            {
                var s = Settings.Current;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                sb.Append("Kuznia przetop (n20-kuznia): start sesji | klucze Smelt Weapon By Parts ").Append(WeaponOn ? "TAK" : "NIE")
                  .Append(", Smelt Armour Once ").Append(ArmourOnceOn ? "TAK" : "NIE")
                  .Append(", Smelt Cap From Forge Bill ").Append(s != null && s.SmeltCapFromForgeBill ? "TAK" : "NIE")
                  .Append(", Bk Shield Bill By Recipe ").Append(s != null && s.BkShieldBillByRecipe ? "TAK" : "NIE")
                  .Append(" | model ").Append(model != null ? model.GetType().Name : "brak")
                  .Append(", wyjscie wpiete w ").Append(SmeltTab.PatchedTypes.Count.ToString(Inv)).Append(" modelach");
                if (model == null) { Log.Info(sb.ToString()); return; }
                int weapons = 0, higher = 0, crudeFromNothing = 0, overHalf = 0, noMetal = 0; long partsSum = 0, outSum = 0;
                string exHigher = null, exCrude = null;
                var samples = new List<string>();
                ItemObject armourProbe = null, shieldProbe = null;
                foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null) continue;
                    if (it.WeaponDesign == null)
                    {
                        if (armourProbe == null && it.HasArmorComponent && SmeltTab.OurSmeltable(it)) armourProbe = it;
                        else if (shieldProbe == null && it.ItemType == ItemObject.ItemTypeEnum.Shield && it.WeaponComponent != null && it.WeaponComponent.PrimaryWeapon != null
                                 && it.WeaponComponent.PrimaryWeapon.PhysicsMaterial != "shield_metal" && SmeltTab.OurSmeltable(it)) shieldProbe = it;   // drewniana - kwit BK bez zelaza
                        continue;
                    }
                    if (it.WeaponDesign.UsedPieces == null || ValyrianBlades.Is(it)) continue;
                    var raw = PartsMetal(it);
                    int pTot = 0, pTop = 0;
                    for (int g = 1; g <= 6; g++) { pTot += raw[g]; if (raw[g] > 0) pTop = g; }
                    var o = model.GetSmeltingOutputForItem(it);
                    if (o == null || o.Length < 7) continue;
                    weapons++;
                    int oTot = 0, oTop = 0;
                    for (int g = 1; g <= 6; g++) { oTot += Math.Max(0, o[g]); if (o[g] > 0) oTop = g; }
                    partsSum += pTot; outSum += oTot;
                    if (pTot == 0) noMetal++;
                    if (oTop > pTop) { higher++; if (exHigher == null) exHigher = it.StringId + " [" + Fmt(raw, 1, 6) + " -> " + Fmt(o, 1, 6) + "]"; }
                    if (o[1] > 0 && raw[1] == 0) { crudeFromNothing++; if (exCrude == null) exCrude = it.StringId + " [" + Fmt(raw, 1, 6) + " -> " + Fmt(o, 1, 6) + "]"; }
                    if (pTot >= 2 && oTot * 2 > pTot) overHalf++;
                    if (samples.Count < 4 && pTot >= 4) samples.Add(it.StringId + " [" + Fmt(raw, 1, 6) + " -> " + Fmt(o, 1, 6) + "]");
                }
                sb.Append(" | bronie z projektem ").Append(weapons.ToString(Inv))
                  .Append(": gatunek wyzszy niz w czesciach ").Append(higher.ToString(Inv)).Append(exHigher != null ? " (np. " + exHigher + ")" : "")
                  .Append(", surowka bez surowki w czesciach ").Append(crudeFromNothing.ToString(Inv)).Append(exCrude != null ? " (np. " + exCrude + ")" : "")
                  .Append(", ponad polowe metalu czesci ").Append(overHalf.ToString(Inv))
                  .Append(", bez metalu w czesciach ").Append(noMetal.ToString(Inv))
                  .Append(", metal wyniku / czesci ").Append(outSum.ToString(Inv)).Append('/').Append(partsSum.ToString(Inv))
                  .Append(" (przy kluczu TAK: 0, 0, 0)")
                  .Append(" | przyklady: ").Append(samples.Count > 0 ? string.Join("; ", samples.ToArray()) : "-");
                sb.Append(" | pancerz: ").Append(Once(model, armourProbe)).Append(" | tarcza: ").Append(Once(model, shieldProbe));
                sb.Append(" | kwit tarczy w CRAFT BK: ").Append(FletchForge.ShieldBillCheck(shieldProbe));
            }
            catch (Exception e) { sb.Append(" | wyjatek ").Append(e.GetType().Name); Stumble("SessionStart", e); }
            Log.Info(sb.ToString());
        }

        /// <summary>Odzysk z modelu wobec naszej receptury (SmeltTab.YieldFor) - przy kluczu ma byc rowny (x1), bez klucza bywal x2.</summary>
        private static string Once(object modelObj, ItemObject it)
        {
            try
            {
                var model = modelObj as TaleWorlds.CampaignSystem.ComponentInterfaces.SmithingModel;
                if (model == null || it == null) return "brak proby";
                int ours = 0; foreach (var p in SmeltTab.YieldFor(it)) ours += p.Count;
                var o = model.GetSmeltingOutputForItem(it);
                int got = 0; for (int g = 1; g <= 6 && g < o.Length; g++) got += Math.Max(0, o[g]);
                return it.StringId + " model " + got.ToString(Inv) + " / receptura " + ours.ToString(Inv) + " (" + Fmt(o, 1, 6) + ")" + (got == ours ? " = raz" : (got == 2 * ours ? " = DWA RAZY" : " = INNE"));
            }
            catch (Exception e) { Stumble("Once", e); return "wyjatek " + e.GetType().Name; }
        }
    }
}
