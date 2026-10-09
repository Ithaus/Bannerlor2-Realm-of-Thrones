using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRAWO KWATERMISTRZA. Zbrojownia DTE to szafa mundurowa wojska - a Jeff
    /// wyniosl z niej WSZYSTKO i sprzedal, po czym lucznicy stali w polu bez
    /// lukow jak piechota. Odtad kwatermistrz wydaje TYLKO NADWYZKI: bierzemy
    /// progi potrzeb wprost z DTE (EquipmentAndThresholds - ile sztuk danego
    /// typu wojsko musi miec na stanie przy tylu ludziach) i kazda proba
    /// zabrania ponizej progu zostaje odbita z komunikatem. Wkladanie do
    /// zbrojowni - zawsze wolne.
    /// </summary>
    internal static class QuartermasterLaw
    {
        private static FieldInfo _fRosters;           // InventoryLogic._rosters
        private static FieldInfo _fArmory;            // DTE ArmyArmory.Armory (static ItemRoster)
        private static FieldInfo _fThresholds;        // DTE EveryoneCampaignBehavior.EquipmentAndThresholds
        private static DateTime _lastShout = DateTime.MinValue;

        internal static ItemRoster DteArmory()
        {
            try { return _fArmory != null ? _fArmory.GetValue(null) as ItemRoster : null; }
            catch { return null; }
        }

        internal static System.Collections.IDictionary Thresholds()
        {
            try { return _fThresholds != null ? _fThresholds.GetValue(null) as System.Collections.IDictionary : null; }
            catch { return null; }
        }

        /// <summary>Ilu zolnierzy (bez bohaterow) nosi dzis barwy oddzialu.</summary>
        internal static int TroopCount()
        {
            try
            {
                int n = 0;
                var r = MobileParty.MainParty.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero) n += el.Number;
                }
                return n;
            }
            catch { return 0; }
        }

        /// <summary>
        /// REALNY stan noszony, nie magazynowe zachcianki DTE. Formuly DTE to
        /// max(2x wojsko, wojsko+100) sztuk KAZDEGO typu (broni az 4x+400!) -
        /// stad "brakuje 127 koni" przy 32 ludziach. My liczymy PO CZLOWIEKU
        /// I PO FACHU: kto wedle wzorca nosi luk, temu liczymy luk i kolczany;
        /// kto kusze - kusze i belty; dwureczna tylko tym, ktorzy nia robia.
        /// </summary>
        internal sealed class Needs
        {
            public int Troops, Mounted, Bows, Xbows, TwoH, Pole, Shields, OneH, Thrown;
            // STOSY AMUNICJI wedle wzorca (Jeff 03.10: "mam strzaly kazdego tieru, a kilka
            // nie chce przyjac"). Dotad WornFor liczyl sztywne x2 na strzelca, a FitFor
            // sloty wzorca (zmiana z 19.09 zostawila raport na starym x2). Raport braku
            // szedl wiec z innej liczby niz dopasowanie: log 03.10 "papier Arrows:
            // potrzeba 258 (dopasowanie liczy 249) ... bez sztuki 0" i na czerwono
            // "Arrows 249/258", choc KAZDY lucznik mial kolczan. Teraz ta sama regula.
            public int ArrowStacks, BoltStacks, ThrownStacks;
        }

        internal static Needs CountNeeds()
        {
            var n = new Needs();
            try
            {
                var r = MobileParty.MainParty.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero) continue;
                    int k = el.Number;
                    n.Troops += k;
                    if (c.IsMounted) n.Mounted += k;
                    bool bow = false, xb = false, th = false, twoh = false, pole = false, sh = false, oneh = false;
                    try
                    {
                        var eq = c.Equipment;   // wzorzec bojowy oddzialu - kto CO ma nosic
                        for (int s = 0; s < 4; s++)
                        {
                            var it = eq[(EquipmentIndex)s].Item;
                            if (it == null) continue;
                            switch (it.ItemType)
                            {
                                case ItemObject.ItemTypeEnum.Bow: bow = true; break;
                                case ItemObject.ItemTypeEnum.Crossbow: xb = true; break;
                                case ItemObject.ItemTypeEnum.Thrown: th = true; break;
                                case ItemObject.ItemTypeEnum.TwoHandedWeapon: twoh = true; break;
                                case ItemObject.ItemTypeEnum.Polearm: pole = true; break;
                                case ItemObject.ItemTypeEnum.Shield: sh = true; break;
                                case ItemObject.ItemTypeEnum.OneHandedWeapon: oneh = true; break;
                            }
                        }
                    }
                    catch { }
                    if (bow) n.Bows += k;
                    if (xb) n.Xbows += k;
                    if (th) n.Thrown += k;
                    if (twoh) n.TwoH += k;
                    if (pole) n.Pole += k;
                    if (sh) n.Shields += k;
                    if (oneh) n.OneH += k;
                    // dokladnie jak FitFor: kto wedle NeedsType nosi amunicje, ten ma
                    // tyle stosow, ile slotow wzorca (nigdy mniej niz 1)
                    if (NeedsType(c, ItemObject.ItemTypeEnum.Arrows)) n.ArrowStacks += k * SlotsOfType(c, ItemObject.ItemTypeEnum.Arrows);
                    if (NeedsType(c, ItemObject.ItemTypeEnum.Bolts)) n.BoltStacks += k * SlotsOfType(c, ItemObject.ItemTypeEnum.Bolts);
                    if (NeedsType(c, ItemObject.ItemTypeEnum.Thrown)) n.ThrownStacks += k * SlotsOfType(c, ItemObject.ItemTypeEnum.Thrown);
                }
            }
            catch { }
            return n;
        }

        internal static int WornFor(ItemObject.ItemTypeEnum type, Needs n)
        {
            switch (type)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return n.Troops;                                 // sztuka na czlowieka
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                    return n.OneH;                                   // wedle wzorca oddzialu
                case ItemObject.ItemTypeEnum.Shield:
                    return n.Shields;
                case ItemObject.ItemTypeEnum.Polearm:
                    return n.Pole;
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                    return n.TwoH;
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return n.Mounted;                                // kon na jezdzca, nie na papierze
                case ItemObject.ItemTypeEnum.Bow:
                    return n.Bows;
                case ItemObject.ItemTypeEnum.Crossbow:
                    return n.Xbows;
                case ItemObject.ItemTypeEnum.Arrows:
                    return n.ArrowStacks;                            // tyle kolczanow, ile slotow wzorca (jak FitFor)
                case ItemObject.ItemTypeEnum.Bolts:
                    return n.BoltStacks;
                case ItemObject.ItemTypeEnum.Thrown:
                    return n.ThrownStacks;
                default:
                    return 0;
            }
        }

        private static int NeedFor(ItemObject.ItemTypeEnum type)
        {
            return WornFor(type, CountNeeds());
        }

        /// <summary>To samo dla escrow: ile sztuk tego typu potrzebuje kompania.</summary>
        internal static int NeedForType(ItemObject.ItemTypeEnum type)
        {
            return NeedFor(type);
        }

        // ===== PORZADEK W SKARBCU (Jeff 14.09: "lucznicy stoja bez kolczanow,
        // a info mowi, ze wszystko okay; jak nie moze uzyc, to rozebrac
        // zolnierza i pokazac w stash") =====
        // Kwatermistrz liczyl SZTUKI po typie: 172 strzal T4-T6 = "komplet",
        // a tanie strzaly szly do gracza jako nadwyzka. Od teraz liczy sie
        // DOPASOWANIE po skillu (ItemReq - zasada nadrzedna) na WSZYSTKIM, co
        // lezy na polce (wojskowe I z listy gracza - DTE w bitwie rozdaje jedno
        // i drugie; Jeff 14.09 v2: "te luki maja Bow 140, a mowi przynies 140").
        // Po dopasowaniu ksiega jest wyrownywana tak, ze SKARBIEC WOJSKA =
        // dokladnie to, co ludzie nosza, a LISTA GRACZA (stash) = dokladnie to,
        // czego nikt nie nosi (ponad skill albo ponad potrzebe).
        internal sealed class Fit
        {
            public int Need;                 // ilu ludzi nosi ten typ
            public int Usable;               // ilu ma cos, co udzwignie
            public int Stock;                // sztuki, ktore bitwa W OGOLE moze wydac (bez unikatow/lore/umarlych)
            public int Barred;               // sztuki, ktorych straz bitewna nigdy nie wyda (unikaty, klingi lore, sprzet umarlych)
            public int UnfitMen;             // ilu zostaje bez uzytecznej sztuki
            public int UnfitMinSkill = -1, UnfitMaxSkill = -1;   // rozrzut ich skilla (co kupic: <= min)
            public string SkillName = "";
            public SkillObject Skill;
            // korekty ksiegi: +n = sztuki nie na ludziach na liste gracza, -n = sztuki gracza, ktore ludzie nosza, na stan wojska
            public List<KeyValuePair<EquipmentElement, int>> Adjust = new List<KeyValuePair<EquipmentElement, int>>();
            // KTO KONKRETNIE nie ma czym (Jeff 14.09: "czemu nie akceptuja tych strzal?!")
            public Dictionary<CharacterObject, int> UnfitByTroop = new Dictionary<CharacterObject, int>();
            // SZCZEGOL DOPASOWANIA (Jeff 16.09: "czemu lucznicy nie biora Weirwood?"):
            // kazda sztuka podazy w kolejnosci wyboru (wymog malejaco, potem skutecznosc)
            // z liczba uzytych, oraz rozklad skilla ludzi - dla broni strzeleckiej
            public List<string> Detail = new List<string>();
            public string SkillHist = "";
            // K1: egzemplarze po dopasowaniu (Used, OwnWorn, Back, Target) i wynik wymiany - PurgeUnusable, HoldReserve
            public List<SwapMath.Piece> Pieces = new List<SwapMath.Piece>();
            public SwapMath.SwapResult Swap;
            public bool OneForOne;
        }


        // ---------------------------------------------------- co bitwa wyklucza
        // PAPIER LICZY TO SAMO CO BITWA (Jeff 15.09, dwa zrzuty: bitwa "45 EMPTY
        // (HandArmor 14, ...)", zbrojownia chwile pozniej: zero brakow pancerza).
        // Diagnostyka z obu stron dala liczby: papier HandArmor polka 199 / udzwigna 199,
        // bitwa podaz grupy HandArmor 182 - 17 sztuk mniej. Straz bitewna (CrashScribe:
        // UniqueWard + SkillLawWard) wyrzuca z puli unikaty, klingi lore i sprzet
        // umarlych, a FitFor liczyl je jako pelnoprawna podaz. Stad "na papierze komplet,
        // w polu 14 ludzi z golymi rekami". Pytamy wiec straz o TE SAME trzy listy
        // (refleksja - CrashScribe moze nie byc zaladowany; wtedy nic nie wykluczamy).
        private static bool _barredLooked;
        private static MethodInfo _mUnique, _mLore, _mDead;
        internal static bool BarredInBattle(ItemObject it)
        {
            if (it == null) return false;
            if (!_barredLooked)
            {
                _barredLooked = true;
                try
                {
                    Type t = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            if (asm.GetName().Name != "CrashScribe") continue;
                            foreach (var ty in asm.GetTypes()) if (ty.Name == "Mends") { t = ty; break; }
                        }
                        catch { }
                        if (t != null) break;
                    }
                    if (t != null)
                    {
                        var bf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                        _mUnique = t.GetMethod("IsUniqueGear", bf, null, new[] { typeof(ItemObject) }, null);
                        _mLore = t.GetMethod("IsLoreBlade", bf, null, new[] { typeof(ItemObject) }, null);
                        _mDead = t.GetMethod("IsDeadGear", bf, null, new[] { typeof(ItemObject) }, null);
                    }
                    Log.Info("Kwatermistrz: listy strazy bitewnej " + (_mUnique != null && _mDead != null ? "podpiete" : "NIEDOSTEPNE")
                             + " - papier " + (_mUnique != null ? "wyklucza" : "NIE wyklucza") + " unikatow/lore/umarlych.");
                }
                catch (Exception e) { Log.Error("QuartermasterLaw.BarredInBattle", e); }
            }
            try
            {
                var a = new object[] { it };
                if (_mUnique != null && (bool)_mUnique.Invoke(null, a)) return true;
                if (_mLore != null && (bool)_mLore.Invoke(null, a)) return true;
                if (_mDead != null && (bool)_mDead.Invoke(null, a)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Ile slotow broni (0-3) wzorzec tego oddzialu trzyma na ten typ.
        /// Dla amunicji: tyle kolczanow albo pekow zolnierz realnie nosi. Nigdy mniej niz 1 -
        /// skoro NeedsType wpuscil go na liste, jeden stos mu sie nalezy (lucznik z lukiem,
        /// ale bez kolczana we wzorcu, dostaje go od DTE).</summary>
        internal static int SlotsOfType(CharacterObject c, ItemObject.ItemTypeEnum type)
        {
            int n = 0;
            try
            {
                var eq = c.Equipment;
                for (int s = 0; s < 4; s++)
                {
                    var it = eq[(EquipmentIndex)s].Item;
                    if (it != null && it.ItemType == type) n++;
                }
            }
            catch { }
            return n < 1 ? 1 : n;
        }

        internal static bool NeedsType(CharacterObject c, ItemObject.ItemTypeEnum type)
        {
            switch (type)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return true;
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return c.IsMounted;
            }
            try
            {
                var eq = c.Equipment;
                for (int s = 0; s < 4; s++)
                {
                    var it = eq[(EquipmentIndex)s].Item;
                    if (it == null) continue;
                    var tt = it.ItemType;
                    if (tt == type) return true;
                    if (type == ItemObject.ItemTypeEnum.Arrows && tt == ItemObject.ItemTypeEnum.Bow) return true;
                    if (type == ItemObject.ItemTypeEnum.Bolts && tt == ItemObject.ItemTypeEnum.Crossbow) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Jakosc egzemplarza po modyfikatorze (legendarny, popekany).
        /// Brak modyfikatora = 1.0. Sluzy WYLACZNIE do domkniecia porzadku w FitFor:
        /// przy identycznych statach lepszy egzemplarz idzie na ludzi, gorszy zostaje
        /// graczowi - zasada Jeffa z 19.09 ("lepsza idzie na ludzi, wyparta gorsza
        /// pokazuje sie w stashu").</summary>
        internal static float ModQuality(EquipmentElement el)
        {
            try { var m = el.ItemModifier; return m != null ? m.PriceMultiplier : 1f; }
            catch { return 1f; }
        }

        internal static string ModId(EquipmentElement el)
        {
            try { var m = el.ItemModifier; return m != null ? (m.StringId ?? "") : ""; }
            catch { return ""; }
        }

        /// <summary>Dopasowanie ludzi do sztuk tego typu na CALEJ polce: kazdy
        /// (od najzdolniejszego) bierze na papierze najlepsza sztuke, ktora
        /// udzwignie. Zwraca ilu ma cos uzytecznego, ilu nic (z rozrzutem
        /// skilla) i korekty ksiegi. K1: liczy SwapMath (jedna implementacja dla druzyny, zalog i K1-C); swap=true i wlaczone
        /// QuartermasterSwapOneForOne - wymiana 1:1 (B2: noszony wklad na ludzi, za wyparta sztuke ludzi ich najgorsza dla gracza,
        /// reszta wolnych sztuk ludzi zostaje LUDZI); inaczej korekty jak przed K1 (noszone -> wojsko, nienoszone -> gracz).</summary>
        internal static Fit FitFor(ItemRoster armory, ItemObject.ItemTypeEnum type) { return FitFor(armory, type, false); }

        internal static Fit FitFor(ItemRoster armory, ItemObject.ItemTypeEnum type, bool swap)
        {
            var f = new Fit();
            try
            {
                if (armory == null) return f;
                // podaz: CALA polka (wojskowe + z listy gracza), tylko to, co liczy sie jako kit; ksiega gracza na NAJGORSZE egzemplarze
                var pieces = KitPieces(armory, type, true);
                foreach (var p in pieces) { if (p.Barred) f.Barred += p.Total; else f.Stock += p.Total; }
                List<CharacterObject> troops; int[] men; SkillObject skill;
                MenOf(MobileParty.MainParty.MemberRoster, type, pieces, out troops, out men, out skill);
                f.Need = men.Length;
                f.SkillName = skill != null ? skill.Name.ToString() : "";
                f.Skill = skill;
                var s = Settings.Current;
                bool oneForOne = swap && s != null && s.QuartermasterSwapOneForOne;
                // PORZADEK CALKOWITY (Jeff 19.09): wymog malejaco, RangedRank (pod RBM naciag), jakosc modyfikatora, id, mod -
                // SwapMath.FitOrder; bitwa tego nie wyda (Barred) - papier tez nie
                var sw = SwapMath.Swap(men, troops.Count, MeetsOf(troops), pieces, oneForOne);
                f.Pieces = pieces; f.Swap = sw; f.OneForOne = oneForOne;
                f.Usable = men.Length - sw.Unfit;
                for (int m = 0; m < men.Length; m++)
                {
                    if (!sw.UnfitMan[m]) continue;
                    var man = troops[men[m]];
                    f.UnfitMen++;
                    int sk = skill != null ? man.GetSkillValue(skill) : 0;
                    if (f.UnfitMinSkill < 0 || sk < f.UnfitMinSkill) f.UnfitMinSkill = sk;
                    if (sk > f.UnfitMaxSkill) f.UnfitMaxSkill = sk;
                    int cnt; f.UnfitByTroop.TryGetValue(man, out cnt); f.UnfitByTroop[man] = cnt + 1;
                }
                // szczegol dopasowania dla broni strzeleckiej (patrz Fit.Detail)
                if (type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow
                    || type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts || type == ItemObject.ItemTypeEnum.Thrown)
                {
                    try
                    {
                        foreach (var p in pieces)
                        {
                            var el = ElOf(p);
                            var it = el.Item;
                            var mod = el.ItemModifier;
                            f.Detail.Add(it.StringId + (mod != null ? "(" + mod.StringId + ")" : "") + " wymog=" + it.Difficulty
                                         + " " + RangedRank.Describe(it) + " t" + ((int)it.Tier + 1)
                                         + " uzyte=" + p.Used + "/" + p.Total + (p.Barred ? " WYKL" : "") + (p.Own > 0 ? " (gracza " + p.Own + ")" : ""));
                        }
                        if (skill != null && men.Length > 0)
                        {
                            var hist = new SortedDictionary<int, int>();
                            foreach (var g in men) { int sk = troops[g].GetSkillValue(skill); int c; hist.TryGetValue(sk, out c); hist[sk] = c + 1; }
                            var parts = new List<string>();
                            foreach (var kv in hist) parts.Add(kv.Value + "x" + kv.Key);
                            f.SkillHist = string.Join(" ", parts.ToArray());
                        }
                    }
                    catch { }
                }
                // korekty ksiegi: docelowo gracz ma DOKLADNIE swoja czesc (Target) kazdego egzemplarza
                foreach (var p in pieces)
                {
                    int delta = p.Target - p.Own;
                    if (delta != 0) f.Adjust.Add(new KeyValuePair<EquipmentElement, int>(ElOf(p), delta));
                }
            }
            catch (Exception e) { Log.Error("QuartermasterLaw.FitFor", e); }
            return f;
        }

        // ---------------------------------------------------- K1: adaptery SwapMath (roster / zbrojownia AI -> egzemplarze, ludzie -> grupy)
        internal static EquipmentElement ElOf(SwapMath.Piece p) { return p != null && p.Tag is EquipmentElement ? (EquipmentElement)p.Tag : default(EquipmentElement); }

        internal static string KeyOf(EquipmentElement el) { return (el.Item != null ? el.Item.StringId ?? "" : "") + "|" + ModId(el); }

        /// <summary>K1: sztuka liczona przez kwatermistrza (typ z KitTypes; kon - tylko wierzchowiec).</summary>
        internal static bool IsKitItem(ItemObject it) { return it != null && Array.IndexOf(KitTypes, it.ItemType) >= 0 && CountsAsKit(it, it.ItemType); }

        /// <summary>K1: egzemplarz SwapMath z sztuki gry - tier 1..6, sila RangedRank.Key, wymog, jakosc i wrak z modyfikatora,
        /// wykluczenie strazy bitewnej, unikat.</summary>
        internal static SwapMath.Piece PieceOf(EquipmentElement el, int n)
        {
            var it = el.Item; var m = el.ItemModifier;
            return new SwapMath.Piece
            {
                Id = it.StringId ?? "", Mod = ModId(el), Tier = AiGear.TierOf(it), Power = RangedRank.Key(it), Req = it.Difficulty,
                Quality = ModQuality(el), Wreck = m != null && LootPrices.IsWreck(m), Barred = BarredInBattle(it), NoSell = ArmsPricing.IsUnique(it),
                Total = n, Tag = el
            };
        }

        /// <summary>K1: sztuki typu z rostera (tylko kit); own=true - ksiega wkladow gracza (per id) na NAJGORSZE egzemplarze.</summary>
        internal static List<SwapMath.Piece> KitPieces(ItemRoster roster, ItemObject.ItemTypeEnum type, bool own)
        {
            var list = new List<SwapMath.Piece>();
            if (roster == null) return list;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster[i];
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.ItemType != type || !CountsAsKit(it, type)) continue;
                list.Add(PieceOf(el.EquipmentElement, el.Amount));
            }
            if (own) SwapMath.AllocateOwn(list, ArmouryBehavior.StockOf);
            return list;
        }

        /// <summary>K1: ludzie rostera, ktorzy wedlug wzorca nosza ten typ (amunicja: tyle stosow, ile slotow wzorca - jak WornFor),
        /// jako grupy po oddziale; men = indeksy grup, od najwyzszego skilla (skill pierwszej sztuki ze skillem - jak dotad FitFor).</summary>
        internal static void MenOf(TroopRoster r, ItemObject.ItemTypeEnum type, List<SwapMath.Piece> pieces, out List<CharacterObject> troops, out int[] men, out SkillObject skill)
        {
            MenOf(r, type, pieces, out troops, out men, out skill, MenClassic, false);
        }

        /// <summary>K1 (przeglad): tryby liczenia ludzi. MenClassic - jak dotad (druzyna gracza: bron 1 na czlowieka, amunicja i oszczepy
        /// po slotach). MenPerSlot - lordowie AI i zalogi: KAZDA bron po slotach wzorca, jak koszyki zakupow brakow (AiGear.NeedBuckets) -
        /// dotad nadwyzki trzymaly 1 bron na czlowieka, wiec druga bron oddzialu szla do kupca, a BuyGaps odkupowal ja tego samego dnia;
        /// pancerz jak dotad u kazdego (DTE ubiera z zbrojowni takze sloty pancerza, ktorych wzorzec nie ma - AssignEquipmentType).
        /// MenTemplate - prog K1-C: jak MenPerSlot, ale pancerz tylko u oddzialow z tym slotem we wzorcu (plaszcz i rekawice spoza wzorca
        /// nie zanizaja pokrycia - zakupy zalogi ich nigdy nie kupia, a piechota bez nich miala najwyzej 5/7 = 71% przy progu 75%).</summary>
        internal const int MenClassic = 0, MenPerSlot = 1, MenTemplate = 2;

        /// <summary>healthy=true - bez rannych (DTE ubiera do bitwy tylko zdrowych).</summary>
        internal static void MenOf(TroopRoster r, ItemObject.ItemTypeEnum type, List<SwapMath.Piece> pieces, out List<CharacterObject> troops, out int[] men, out SkillObject skill,
                                   int mode, bool healthy)
        {
            bool armour = type == ItemObject.ItemTypeEnum.HeadArmor || type == ItemObject.ItemTypeEnum.BodyArmor || type == ItemObject.ItemTypeEnum.LegArmor
                          || type == ItemObject.ItemTypeEnum.HandArmor || type == ItemObject.ItemTypeEnum.Cape;
            bool horse = type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness;
            bool ammo = type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts || type == ItemObject.ItemTypeEnum.Thrown;
            troops = new List<CharacterObject>();
            var counts = new List<int>();
            skill = null;
            if (pieces != null) foreach (var p in pieces) { skill = ItemReq.SkillFor(ElOf(p).Item); if (skill != null) break; }
            if (skill == null) skill = SkillOfType(type);
            try
            {
                if (r != null)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var c = el.Character;
                        int num = healthy ? el.Number - el.WoundedNumber : el.Number;
                        if (c == null || c.IsHero || num <= 0) continue;
                        if (mode == MenTemplate && armour) { if (TemplateSlots(c, type) <= 0) continue; }
                        else if (!NeedsType(c, type)) continue;
                        int mult = (ammo || (mode != MenClassic && !armour && !horse)) ? SlotsOfType(c, type) : 1;
                        troops.Add(c); counts.Add(num * mult);
                    }
            }
            catch { }
            var order = new List<int>();
            for (int g = 0; g < troops.Count; g++) order.Add(g);
            var sk0 = skill; var tr = troops;
            if (sk0 != null)
                order.Sort((a, b) => { int d = tr[b].GetSkillValue(sk0).CompareTo(tr[a].GetSkillValue(sk0)); return d != 0 ? d : a.CompareTo(b); });
            int total = 0; foreach (var c in counts) total += c;
            men = new int[total];
            int k = 0;
            foreach (var g in order) for (int j = 0; j < counts[g]; j++) men[k++] = g;
        }

        /// <summary>K1 (przeglad): ile slotow wzorca (bron 0-3, pancerz, plaszcz) trzyma ten typ - 0, gdy wzorzec go nie ma.</summary>
        internal static int TemplateSlots(CharacterObject c, ItemObject.ItemTypeEnum type)
        {
            int n = 0;
            try
            {
                var eq = c.Equipment;
                for (int s = 0; s < 10; s++)
                {
                    var it = eq[(EquipmentIndex)s].Item;
                    if (it != null && it.ItemType == type) n++;
                }
            }
            catch { }
            return n;
        }

        /// <summary>Skill typu, gdy na polce nie ma ani jednej sztuki (kolejnosc ludzi i tak bez znaczenia - przydaje sie koszykom A7).</summary>
        internal static SkillObject SkillOfType(ItemObject.ItemTypeEnum type)
        {
            switch (type)
            {
                case ItemObject.ItemTypeEnum.Bow: case ItemObject.ItemTypeEnum.Arrows: return DefaultSkills.Bow;
                case ItemObject.ItemTypeEnum.Crossbow: case ItemObject.ItemTypeEnum.Bolts: return DefaultSkills.Crossbow;
                case ItemObject.ItemTypeEnum.Thrown: return DefaultSkills.Throwing;
                case ItemObject.ItemTypeEnum.OneHandedWeapon: case ItemObject.ItemTypeEnum.Shield: return DefaultSkills.OneHanded;
                case ItemObject.ItemTypeEnum.TwoHandedWeapon: return DefaultSkills.TwoHanded;
                case ItemObject.ItemTypeEnum.Polearm: return DefaultSkills.Polearm;
                case ItemObject.ItemTypeEnum.Horse: case ItemObject.ItemTypeEnum.HorseHarness: return DefaultSkills.Riding;
                default: return DefaultSkills.Athletics;
            }
        }

        /// <summary>K1: udzwig - ItemReq.Meets (zasada nadrzedna) dla oddzialu grupy g.</summary>
        internal static Func<int, SwapMath.Piece, bool> MeetsOf(List<CharacterObject> troops)
        {
            return (g, p) => g >= 0 && g < troops.Count && ItemReq.Meets(troops[g], ElOf(p).Item);
        }

        /// <summary>Ile sztuk tego typu wojsko REALNIE obsadzi (dopasowanie po skillu, cala polka).</summary>
        internal static int WarUsableOf(ItemRoster armory, ItemObject.ItemTypeEnum type)
        {
            return FitFor(armory, type).Usable;
        }

        /// <summary>PORZADEK przy kazdym otwarciu i zamknieciu zbrojowni: ksiega wyrownana do
        /// dopasowania. K1 (QuartermasterSwapOneForOne, Jeff 29.08 / 30.08 / 14.09 / 19.09 / 09.10): noszony wklad gracza przechodzi
        /// na ludzi; za kazdy, ktory WYPARL sztuke ludzi, gracz dostaje jedna ich najgorsza wolna uzyteczna sztuke tego typu; wklad,
        /// ktory wypelnil puste rece, przechodzi bez zwrotu; nienoszony wklad zostaje gracza; reszta wolnych sztuk ludzi jest LUDZI
        /// (gracz moze je od nich kupic). Wylaczone - jak przed K1: sztuki nie na ludziach na liste gracza, noszone na stan wojska.
        /// Rozkazy z ksiegi musztry nietykane. Zwraca liczbe sztuk oddanych graczowi.</summary>
        internal static int PurgeUnusable(ItemRoster armory)
        {
            int toPlayer = 0, toMen = 0;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.QuartermasterPurgeUnusable || armory == null) return 0;
                var names = new List<string>();
                var takenNames = new List<string>();   // co ZABRANO - dotad gracz nie dostawal ani jednej nazwy
                int nameKinds = 0, takenKinds = 0;     // ile ROZNYCH pozycji - wielokropek tylko gdy naprawde obcieto
                var perType = new List<string>();
                // K1: wymiana 1:1 - liczniki calego przebiegu, nazwy obu stron i podzial egzemplarzy dla ekranu (HoldReserve, BookPostfix)
                bool swapRule = false;
                int sWorn = 0, sX = 0, sFilled = 0, sKept = 0, sSpare = 0, sPlayerVis = 0, sNotBetter = 0;
                var wornNames = new List<string>(); var backNames = new List<string>();
                int wornKinds = 0, backKinds = 0;
                var split = new Dictionary<string, int[]>();   // klucz id|mod -> [noszone, czesc gracza, razem]
                // 16.09: rozkazy z ksiegi musztry do logu - dotad nie bylo po nich sladu
                var pinnedWorn = new List<string>();
                int pinnedKept = 0;
                try
                {
                    string pins = MusterBook.DescribePins(12);
                    // K1 (przeglad): porzadek biegnie teraz takze przy kazdym wjezdzie i dobie w miescie - linia tylko, gdy rozkazy sie zmienily
                    if (pins.Length > 0 && pins != _lastPins) Log.Info("Kwatermistrz: rozkazy z ksiegi musztry: " + pins + ".");
                    _lastPins = pins;
                }
                catch { }
                foreach (var type in KitTypes)
                {
                    var f = FitFor(armory, type, true);
                    int here = 0, taken = 0;
                    // K1: podzial egzemplarzy i wymiana tego typu
                    if (f.OneForOne) swapRule = true;
                    foreach (var p in f.Pieces)
                    {
                        int[] v; string key = p.Key;
                        if (!split.TryGetValue(key, out v)) split[key] = v = new int[3];
                        v[0] += p.Used; v[1] += p.Target; v[2] += p.Total;
                    }
                    if (f.Swap != null && f.OneForOne)
                    {
                        var sw = f.Swap;
                        sWorn += sw.Worn; sX += sw.X; sFilled += sw.Filled; sKept += sw.KeptOwn; sSpare += sw.MenSpare; sNotBetter += sw.NotBetter;
                        foreach (var p in f.Pieces) sPlayerVis += p.Target;
                        if (sw.Worn > 0 || sw.X > 0)
                        {
                            var back = new List<string>();
                            foreach (var p in f.Pieces)
                            {
                                var el = ElOf(p);
                                if (el.Item == null) continue;
                                string nm = el.Item.Name + (el.ItemModifier != null ? " (" + el.ItemModifier.Name + ")" : "");
                                if (p.OwnWorn > 0) { wornKinds++; if (wornNames.Count < 4) wornNames.Add(p.OwnWorn + "x " + nm); }
                                if (p.Back > 0)
                                {
                                    backKinds++; if (backNames.Count < 4) backNames.Add(p.Back + "x " + nm);
                                    if (back.Count < 8) back.Add(p.Id + (p.Mod.Length > 0 ? "(" + p.Mod + ")" : "") + " x" + p.Back);
                                }
                            }
                            Log.Info("Wymiana: " + type + ": wklady noszone " + sw.Worn + " (braki " + sw.Filled + ", wymiana " + sw.X
                                     + (sw.NotBetter > 0 ? ", gorsze od wypartej bez zwrotu " + sw.NotBetter : "") + "), graczowi: "
                                     + (back.Count > 0 ? string.Join(", ", back.ToArray()) : "nic") + "; wklady nienoszone " + sw.KeptOwn
                                     + " zostaja gracza; zapas ludzi " + sw.MenSpare + ".");
                        }
                    }
                    // KSIEGA JEST PER ID, dopasowanie per sztuka (modyfikator!) - sumujemy
                    // korekty per id, inaczej "Balanced X" vs "X" robily ping-pong +1/-1
                    var perId = new Dictionary<string, int>();
                    var nameOf = new Dictionary<string, string>();
                    foreach (var kv in f.Adjust)
                    {
                        var it = kv.Key.Item;
                        if (kv.Value == 0 || it == null) continue;
                        string id = it.StringId ?? "";
                        int acc; perId.TryGetValue(id, out acc); perId[id] = acc + kv.Value;
                        nameOf[id] = it.Name.ToString();
                    }
                    // ROZKAZ Z KSIEGI (Jeff 16.09; poprawka zapadki 19.09): najpierw suma korekt per id, rozkaz z ksiegi musztry chroni
                    // sprzet tylko przed PRZYCINANIEM magazynu (TrimWarStores) - o wlasnosci decyduje wylacznie dopasowanie (zasada 19.09)
                    if (perId.Count > 0)
                    {
                        foreach (var id2 in perId.Keys)
                        {
                            if (!MusterBook.IsPinnedItem(id2)) continue;
                            int net = perId[id2];
                            if (net > 0) pinnedKept += net;                                  // tylko do logu - sztuka i tak wraca do gracza
                            else if (net < 0 && !pinnedWorn.Contains(id2)) pinnedWorn.Add(id2);
                        }
                    }
                    foreach (var kv in perId)
                    {
                        if (kv.Value > 0)
                        {
                            ArmouryBehavior.StockDeposit(kv.Key, kv.Value);     // nie na ludziach -> lista gracza
                            toPlayer += kv.Value; here += kv.Value;
                            nameKinds++;
                            if (names.Count < 4) names.Add(kv.Value + "x " + nameOf[kv.Key]);
                        }
                        else if (kv.Value < 0)
                        {
                            ArmouryBehavior.StockWithdraw(kv.Key, -kv.Value);   // noszone -> stan wojska
                            toMen += -kv.Value; taken += -kv.Value;
                            takenKinds++;
                            if (takenNames.Count < 4) takenNames.Add((-kv.Value) + "x " + nameOf[kv.Key]);
                        }
                    }
                    if (here > 0 || taken > 0)
                    {
                        perType.Add(type + " +" + here + "/-" + taken);
                        // 15.09: rozbicie PER ID - wymiana czy nadwyzka wojska tej samej nazwy
                        var det = new List<string>();
                        foreach (var kv in perId)
                            if (kv.Value != 0 && det.Count < 10)
                                det.Add(kv.Key + (kv.Value > 0 ? " +" : " ") + kv.Value);
                        Log.Info("Kwatermistrz: porzadek " + type + ": " + string.Join(", ", det.ToArray())
                                 + (perId.Count > 10 ? ", ..." : ""));
                    }
                }
                _lastSplit = split; _lastX = sX; _xUnseen += sX;
                if (pinnedWorn.Count > 0 || pinnedKept > 0)
                    Log.Info("Kwatermistrz: rozkazy - noszone z rozkazem na stan wojska: "
                             + (pinnedWorn.Count > 0 ? string.Join(", ", pinnedWorn.ToArray()) : "(brak)")
                             + "; nienoszone z rozkazem WRACAJA na liste gracza: " + pinnedKept + " szt.");
                if (toPlayer > 0 || toMen > 0)
                    Log.Info("Kwatermistrz: porzadek w skarbcu - " + toPlayer + " szt. na liste gracza, " + toMen
                             + " szt. na stan wojska [" + string.Join(", ", perType.ToArray()) + "].");
                if (swapRule)
                {
                    if (sWorn > 0 || sX > 0)
                    {
                        Log.Info("Wymiana: razem ludzie wzieli " + sWorn + ", oddali " + sX + " gorszych, braki " + sFilled + ", gorsze od wypartej bez zwrotu " + sNotBetter
                                 + "; widoczne: gracza " + sPlayerVis + ", zapas ludzi " + sSpare + ".");
                        // K1 B3 (Jeff 29.08: "otwieram i zamiast moich lukow leza wymienione"): obie strony wymiany w jednym zdaniu
                        string took = "QM: the men took " + sWorn + " pcs of yours (" + string.Join(", ", wornNames.ToArray())
                                      + (wornKinds > wornNames.Count ? ", ..." : "") + "): " + sFilled + " filled empty hands, " + sX + " replaced worse kit"
                                      + (sNotBetter > 0 ? ", " + sNotBetter + " pushed out better kit of theirs (nothing comes back for those)" : "");
                        string back = sX > 0
                            ? " - those " + sX + " worse pcs are yours in the stash (" + string.Join(", ", backNames.ToArray()) + (backKinds > backNames.Count ? ", ..." : "") + ")"
                            : "";
                        Log.Player(took + back + "; " + sKept + " pcs of yours no man wears stay yours.");
                    }
                    else if (toPlayer > 0)
                        Log.Player("QM: " + toPlayer + " pcs the men may not carry into battle are yours in the stash ("
                                   + string.Join(", ", names.ToArray()) + (nameKinds > names.Count ? ", ..." : "") + ").");
                }
                else if (toPlayer > 0 || toMen > 0)
                {
                    // JEDNO ZDANIE O CALEJ WYMIANIE (Jeff 19.09, regula sprzed K1 - wylacznik QuartermasterSwapOneForOne)
                    string took = toMen > 0
                        ? "the men took " + toMen + " pcs of yours (" + string.Join(", ", takenNames.ToArray())
                          + (takenKinds > takenNames.Count ? ", ..." : "") + ")"
                        : "the men took nothing";
                    string back = toPlayer > 0
                        ? toPlayer + " pcs no man wears are yours in the stash ("
                          + string.Join(", ", names.ToArray()) + (nameKinds > names.Count ? ", ..." : "") + ")"
                        : "nothing was displaced - they had empty hands";
                    Log.Player("QM: " + took + "; " + back + ".");
                }
                // B7 (niezmienniki): czesc gracza w granicach egzemplarza
                foreach (var kv in split) if (kv.Value[1] < 0 || kv.Value[1] > kv.Value[2]) { Log.Info("Wymiana: NIEZMIENNIK - czesc gracza " + kv.Key + " = " + kv.Value[1] + " przy " + kv.Value[2] + " szt."); break; }
            }
            catch (Exception e) { Log.Error("QuartermasterLaw.PurgeUnusable", e); }
            return toPlayer;
        }

        // K1: wynik ostatniego porzadku - podzial egzemplarzy (id|mod -> noszone, czesc gracza, razem) i liczba zwrotow wymiany
        private static Dictionary<string, int[]> _lastSplit;
        private static int _lastX;
        internal static Dictionary<string, int[]> LastSplit { get { return _lastSplit; } }
        internal static int LastX { get { return _lastX; } }
        internal static void ClearSplit() { _lastSplit = null; _lastX = 0; }
        // K1 (przeglad): zwroty z wymiany od ostatniego otwarcia ekranu - wymiana liczy sie zwykle przy ZAMKNIECIU (albo przy wjezdzie do
        // miasta), a komunikat ekranu przy nastepnym otwarciu pokazywal X z porzadku otwarcia (prawie zawsze 0)
        private static int _xUnseen;
        private static string _lastPins;
        internal static int TakeUnseenX() { int x = _xUnseen; _xUnseen = 0; return x; }
        internal static void Reset() { _lastSplit = null; _lastX = 0; _xUnseen = 0; _lastPins = null; }

        /// <summary>Ile sztuk tego typu jest WLASNOSCIA WOJSKA (calosc polek
        /// minus ksiega wkladow gracza). Po tym liczymy, ile jeszcze uniosa
        /// na sobie - Jeff 30.08: "max znika 214, bo tyle jest na ludziach".</summary>
        internal static int WarOwnedOf(ItemRoster armory, ItemObject.ItemTypeEnum type)
        {
            int war = 0;
            try
            {
                var allowance = new Dictionary<string, int>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory[i];
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.ItemType != type) continue;
                    string id = it.StringId ?? "";
                    int left;
                    if (!allowance.TryGetValue(id, out left)) left = ArmouryBehavior.StockOf(id);
                    int mine = Math.Min(el.Amount, Math.Max(0, left));
                    allowance[id] = left - mine;
                    war += el.Amount - mine;
                }
            }
            catch { }
            return war;
        }

        internal static readonly ItemObject.ItemTypeEnum[] KitTypes =
        {
            ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.BodyArmor,
            ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor,
            ItemObject.ItemTypeEnum.Cape, ItemObject.ItemTypeEnum.OneHandedWeapon,
            ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm,
            ItemObject.ItemTypeEnum.Shield, ItemObject.ItemTypeEnum.Bow,
            ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Arrows,
            ItemObject.ItemTypeEnum.Bolts, ItemObject.ItemTypeEnum.Thrown,
            ItemObject.ItemTypeEnum.Horse, ItemObject.ItemTypeEnum.HorseHarness
        };

        /// <summary>Pelna lista brakow "Typ noszone/potrzebne" wzgledem stanu zbrojowni.</summary>
        internal static List<string> ShortageLines()
        {
            var lines = new List<string>();
            try
            {
                if (QuartermasterEscrow.Active) return lines;   // polki wlasnie schowane - nie liczyc na slepo
                var armory = DteArmory();
                if (armory == null) return lines;
                var needs = CountNeeds();
                foreach (var type in KitTypes)
                {
                    int need = WornFor(type, needs);
                    if (need <= 0) continue;
                    var fit = FitFor(armory, type);
                    int raw = fit.Stock;                    // polka BEZ unikatow/lore/umarlych - jak w bitwie (15.09)
                    int have = Math.Min(raw, fit.Usable);   // liczy sie to, co ludzie UDZWIGNA (Jeff 14.09)
                    // 15.09 DIAGNOSTYKA: bitwa (SkillLawWard) melduje puste sloty pancerza,
                    // a ten raport nie widzi braku - zapisujemy wynik dopasowania dla KAZDEGO
                    // typu pancerza, takze gdy brak nie wychodzi, zeby dalo sie porownac obie strony
                    // 16.09: papier dla KAZDEGO typu (dotad tylko pancerze) - Jeff: "czemu lucznicy
                    // nie biora Weirwood?" i z logu nie dalo sie odpowiedziec
                    Log.Info("Kwatermistrz: papier " + type + ": potrzeba " + need + " (dopasowanie liczy " + fit.Need
                             + "), polka " + raw + " (+" + fit.Barred + " wykluczonych: unikaty/lore/umarli), udzwigna "
                             + fit.Usable + ", bez sztuki " + fit.UnfitMen + "."
                             + (fit.SkillHist.Length > 0 ? " Skill " + fit.SkillName + " ludzi: " + fit.SkillHist + "." : ""));
                    if (fit.Detail.Count > 0)
                    {
                        var top = fit.Detail.Count > 14 ? fit.Detail.GetRange(0, 14) : fit.Detail;
                        Log.Info("Kwatermistrz: papier " + type + " po kolei wyboru (wymog malejaco, potem " + (RangedRank.RbmLoaded ? "naciag RBM" : "skutecznosc") + "): "
                                 + string.Join(", ", top.ToArray()) + (fit.Detail.Count > 14 ? ", ... (" + fit.Detail.Count + " pozycji)" : "") + ".");
                    }
                    if (have < need)
                    {
                        string line = type + " " + have + "/" + need;
                        // KROTKO (Jeff 14.09: "pisz skrotami, bo jak duzo tekstu, to nie widac")
                        // + KTO: dwa najliczniejsze oddzialy bez uzytecznej sztuki i ich skill
                        if (fit.UnfitMen > 0 && fit.UnfitMinSkill >= 0)
                        {
                            var who = new List<KeyValuePair<CharacterObject, int>>(fit.UnfitByTroop);
                            who.Sort((a, b) => b.Value.CompareTo(a.Value));
                            var parts = new List<string>();
                            var full = new List<string>();
                            for (int w = 0; w < who.Count; w++)
                            {
                                var co = who[w].Key;
                                int lvl = 0;
                                try { lvl = fit.Skill != null ? co.GetSkillValue(fit.Skill) : 0; } catch { }
                                string s1 = who[w].Value + "x " + co.Name + " " + lvl;
                                full.Add(s1);
                                if (w < 2) parts.Add(s1);
                            }
                            line += " (no fit: " + string.Join(", ", parts.ToArray()) + (who.Count > 2 ? ", ..." : "")
                                    + " - bring " + fit.SkillName + " <=" + fit.UnfitMinSkill + ")";
                            Log.Info("Kwatermistrz: " + type + " " + have + "/" + need + " - bez uzytecznej sztuki: "
                                     + string.Join(", ", full.ToArray()) + " (" + fit.SkillName + ").");
                        }
                        // 15.09: brak CZYSTO ILOSCIOWY (nikt nie jest "bez uzytecznej sztuki",
                        // po prostu sztuk jest mniej niz ludzi) dotad NIE trafial do logu -
                        // wpis wyzej powstaje tylko w galezi UnfitMen. Przez to nie dalo sie
                        // z logu odtworzyc "wrzucam - brak znika, wyjmuje - wraca" (Jeff 15.09).
                        else Log.Info("Kwatermistrz: " + type + " " + have + "/" + need
                                      + " - brak ilosciowy (polka " + raw + ", udzwigna " + fit.Usable + ").");
                        lines.Add(line);
                    }
                }
            }
            catch { }
            return lines;
        }

        /// <summary>
        /// Kwatermistrz MELDUJE braki na glos - po bitwie i co rano, nie tylko
        /// w ekranie zbrojowni ("czemu ja o tym nie wiem!" - Jeff, 2026).
        /// Zwraca true, gdy bylo co meldowac.
        /// </summary>
        internal static bool ShoutShortages(string headline)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.QuartermasterShouts) return false;
                var lines = ShortageLines();
                if (lines.Count == 0) return false;
                InformationManager.DisplayMessage(new InformationMessage(headline, Colors.Red));
                for (int i = 0; i < lines.Count; i += 5)
                {
                    int n = Math.Min(5, lines.Count - i);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "  " + string.Join(", ", lines.GetRange(i, n).ToArray()), Colors.Red));
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>Ile sztuk danego typu lezy jeszcze w zbrojowni.</summary>
        /// <summary>
        /// MUL TO NIE RUMAK (Jeff 13.09: "czy mozemy konie juczne i pociagowe zmienic,
        /// aby nie byly traktowane do jazdy? i mul"). W tej instalacji mul, kon juczny
        /// i pociagowy maja ItemType = Horse, wiec liczenie po samym typie wpisywalo je
        /// jako wierzchowce: juczne zwierze w zbrojowni po cichu "pokrywalo" rycerza,
        /// licznik pokazywal komplet, a jazda i tak wstawala piesza.
        /// Odsiewamy tak samo jak stajnie (Stables.IsPlainMount): musi miec
        /// HorseComponent.IsMount ORAZ kategorie Horse/WarHorse/NobleHorse - juczne
        /// siedza w kategorii sumpter_horse i wypadaja. Slonie i smoki tez.
        /// </summary>
        internal static bool CountsAsKit(ItemObject it, ItemObject.ItemTypeEnum type)
        {
            if (it == null || it.ItemType != type) return false;
            if (type != ItemObject.ItemTypeEnum.Horse) return true;
            return Stables.IsPlainMount(it);
        }

        internal static int HaveFor(ItemRoster armory, ItemObject.ItemTypeEnum type)
        {
            int n = 0;
            try
            {
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory[i];
                    if (CountsAsKit(el.EquipmentElement.Item, type)) n += el.Amount;
                }
            }
            catch { }
            return n;
        }

        /// <summary>
        /// ZBROJOWNIA TO NIE SMIETNIK (Jeff 27.08: "discarduje pancerz za XP
        /// i znika CALY ekwipunek"). Ekran zbrojowni DTE otwiera sie w trybie,
        /// w ktorym vanilla wlacza CanGainXpFromDiscarding - a wtedy przy
        /// zatwierdzeniu _rosters[0] (czyli CALA zbrojownia!) leci eventem
        /// OnItemsDiscardedByPlayer jako stos porzuconych rzeczy: XP liczy sie
        /// od wszystkiego, a magazyn potrafi wyparowac. Po kazdym przeliczeniu
        /// donacji gasimy flage, gdy lewa strona to zbrojownia. XP za discard
        /// dziala dalej na ekranach lupow - tam lewa strona to prawdziwy smietnik.
        /// </summary>
        public static void XpDonationsPostfix(InventoryLogic __instance)
        {
            try
            {
                if (_fRosters == null) return;
                var rosters = _fRosters.GetValue(__instance) as ItemRoster[];
                var armory = DteArmory();
                if (rosters == null || rosters.Length == 0 || rosters[0] == null) return;
                // K1 (B6): ekran "Hand kit to the garrison" (OpenScreenAsReceiveItems, tryb Default bez drugiej partii) tez wlacza
                // XP za discard - sprzet dla zalogi to nie smietnik, tak samo jak zbrojownia
                bool garrisonScreen = GarrisonKit.IsScreenRoster(rosters[0]);
                if ((armory == null || rosters[0] != armory) && !garrisonScreen) return;
                if (!__instance.CanGainXpFromDiscarding) return;
                var f = AccessTools.Field(typeof(InventoryLogic), "<CanGainXpFromDiscarding>k__BackingField");
                if (f != null)
                {
                    f.SetValue(__instance, false);
                    Log.Info("QuartermasterLaw: " + (garrisonScreen ? "ekran zalogi" : "ekran zbrojowni") + " - XP za discard zgaszone (magazyn to nie smietnik).");
                }
            }
            catch (Exception e) { Log.Error("XpDonationsPostfix", e); }
        }

        internal static bool Prefix(InventoryLogic __instance, ref TransferCommand transferCommand, ref List<TransferCommandResult> __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ArmouryProtectUsed) return true;
                if (_fRosters == null || _fArmory == null) return true;

                var rosters = _fRosters.GetValue(__instance) as ItemRoster[];
                var armory = DteArmory();
                if (rosters == null || rosters.Length == 0 || armory == null || rosters[0] != armory) return true;

                // K1 (przeglad): zapas LUDZI tylko za pieniadze, ktore masz - takze prosto na towarzysza (lewa strona -> slot ekwipunku)
                if (transferCommand.FromSide == InventoryLogic.InventorySide.OtherInventory && !CanPayMen(transferCommand)) { __result = new List<TransferCommandResult>(); return false; }

                if (QuartermasterEscrow.Active) return true;   // lista juz pokazuje tylko wklady gracza
                if (transferCommand.FromSide != InventoryLogic.InventorySide.OtherInventory) return true;   // wkladasz - wolno zawsze

                var item = transferCommand.ElementToTransfer.EquipmentElement.Item;
                if (item == null) return true;

                int take = Math.Max(1, transferCommand.Amount);
                // WLASNE sztuki zawsze do odebrania: gdy caly magazyn nalezy
                // do gracza, escrow nie startuje (Active=false) i prog
                // need/have blokowal odbior wlasnego wkladu
                if (ArmouryBehavior.StockOf(item.StringId) >= take) return true;

                int troops = TroopCount();
                int need = NeedFor(item.ItemType);
                if (need <= 0) return true;                                   // typ bez progu - wolny
                int have = HaveFor(armory, item.ItemType);
                if (have - take >= need) return true;                          // zostaje zapas - wydaj

                int surplus = Math.Max(0, have - need);
                if ((DateTime.Now - _lastShout).TotalMilliseconds > 700)
                {
                    _lastShout = DateTime.Now;
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Quartermaster: the men still use these - " + need + " " + item.ItemType +
                        " must stay for " + troops + " soldiers (" + (surplus > 0 ? surplus + " spare to take" : "no spares") + ").",
                        Colors.Red));
                }
                __result = new List<TransferCommandResult>();
                return false;                                                  // sprzet w uzyciu nie wychodzi
            }
            catch (Exception e) { Log.Error("QuartermasterLaw", e); return true; }
        }

        /// <summary>K1 (przeglad): wziecie z zapasu LUDZI (ponad Twoja darmowa czesc) przechodzi tylko, gdy kiesa pokryje juz wziete w tej
        /// sesji i te sztuki. Dotad przy braku zlota SettleBuys oddawal sztuke z sakw do zbrojowni, a sztuka zalozona od razu na towarzysza
        /// w sakwach nie lezy - zostawala za darmo (wpis 84; od K1 caly zapas ludzi jest na sprzedaz).</summary>
        private static bool CanPayMen(TransferCommand tc)
        {
            try
            {
                if (!MenPurse.On) return true;
                var el = tc.ElementToTransfer.EquipmentElement;
                var it = el.Item;
                if (it == null || it.StringId == null) return true;
                int n = Math.Max(1, tc.Amount), free;
                if (!QuartermasterEscrow.PeekFree(KeyOf(el), n, out free)) free = Math.Min(n, Math.Max(0, ArmouryBehavior.StockOf(it.StringId)));
                int paid = n - free;
                if (paid <= 0) return true;
                var st = Settlement.CurrentSettlement;
                long owe = MenPurse.PendingCost(st) + (long)MenPurse.SellPrice(el, st, MobileParty.MainParty) * paid;
                if (Hero.MainHero.Gold >= owe) return true;
                if ((DateTime.Now - _lastShout).TotalMilliseconds > 700)
                {
                    _lastShout = DateTime.Now;
                    InformationManager.DisplayMessage(new InformationMessage("Quartermaster: that is the men's kit - they want " + owe + " denars on closing for what you took, and your purse holds "
                                                                             + Hero.MainHero.Gold + ".", Colors.Red));
                }
                return false;
            }
            catch (Exception e) { Log.Error("QuartermasterLaw.CanPayMen", e); return true; }
        }

        /// <summary>
        /// KSIEGA WKLADOW - ksiegowanie PO wykonanym transferze. W prefixie
        /// ksiega rosla/malala nawet wtedy, gdy sam transfer zaraz potem
        /// blokowal nasz wlasny return false - a gdy blokowal prefix DTE
        /// (CommandersGreed) biegnacy przed nami, nasz bool-prefix bywal
        /// w ogole POMIJANY (Harmony 2.4: bool-prefix nie biegnie po czyims
        /// false, void-prefixy biegna zawsze) i ruch zostawal niezaksiegowany.
        /// Postfix z __runOriginal widzi prawde w kazdym uporzadkowaniu.
        /// </summary>
        public static void BookPostfix(InventoryLogic __instance, ref TransferCommand transferCommand, bool __runOriginal)
        {
            try
            {
                if (!__runOriginal) return;                     // transfer zablokowany - nic sie nie stalo
                var s = Settings.Current;
                if (s == null || !s.ArmouryProtectUsed) return;
                if (_fRosters == null || _fArmory == null) return;
                var rosters = _fRosters.GetValue(__instance) as ItemRoster[];
                var armory = DteArmory();
                if (rosters == null || rosters.Length == 0 || armory == null || rosters[0] != armory) return;

                var it = transferCommand.ElementToTransfer.EquipmentElement.Item;
                if (it == null || it.StringId == null) return;
                int n = Math.Max(1, transferCommand.Amount);
                if (transferCommand.FromSide == InventoryLogic.InventorySide.OtherInventory)
                {
                    // wpis 84: czesc ponad Twoj wlasny wklad to lup ludzi - kupujesz go (rozliczenie przy zamknieciu)
                    // K1 (B3): darmowe wyjecie liczone PER EGZEMPLARZ (id + stan) z podzialu z otwarcia ekranu - dotad per id, wiec mozna
                    // bylo zabrac sprawny egzemplarz ludzi zamiast swojego obitego; bez podzialu (porzadek wylaczony) - jak dotad per id
                    var el = transferCommand.ElementToTransfer.EquipmentElement;
                    int free;
                    if (!QuartermasterEscrow.TakeFree(KeyOf(el), n, out free))
                    {
                        free = Math.Min(n, Math.Max(0, ArmouryBehavior.StockOf(it.StringId)));
                    }
                    MenPurse.NoteBuy(el, n - free);
                    ArmouryBehavior.StockWithdraw(it.StringId, free);
                    // wycofanie wkladu w TEJ samej sesji ekranu kasuje tez
                    // jego wpis w rejestrze wymian - inaczej przy zamknieciu
                    // kwatermistrz rozliczy wklad, ktorego juz nie ma
                    QuartermasterEscrow.NoteWithdraw(it, free);
                }
                else if (transferCommand.ToSide == InventoryLogic.InventorySide.OtherInventory)
                {
                    // wpis 84: odlozone z powrotem to, co w tej sesji wziales od ludzi - zakup skasowany, to nie Twoj wklad
                    int undo = MenPurse.CancelBuy(transferCommand.ElementToTransfer.EquipmentElement, n);
                    n -= undo;
                    if (n <= 0) return;
                    ArmouryBehavior.StockDeposit(it.StringId, n);
                    QuartermasterEscrow.AddFree(KeyOf(transferCommand.ElementToTransfer.EquipmentElement), n, IsKitItem(it));   // K1: odlozony wklad mozna zabrac z powrotem za darmo
                    // wklad zapisany do WYMIANY BARTEROWEJ (rozliczy sie
                    // przy zamknieciu ekranu - Jeff: "wrzucam t6 luki,
                    // maja mi wydac gorsze, ktore sprzedam")
                    QuartermasterEscrow.NoteDeposit(it, n,
                        transferCommand.ElementToTransfer.EquipmentElement.ItemValue);
                }
            }
            catch (Exception e) { Log.Error("BookPostfix", e); }
        }

        /// <summary>
        /// RESET/CANCEL EKRANU (znalezisko przegladu): InventoryLogic.Reset
        /// przywraca rostery HURTEM z kopii zapasowej, BEZ TransferItem -
        /// BookPostfix nie widzi cofniecia i ksiega z rejestrem wymian
        /// zostaja ze stanem sprzed. Skutek: wymiana widma przy zamknieciu
        /// (darmowy sprzet wojska) albo strata wkladu. Cofamy ksiege
        /// do migawki z otwarcia ekranu i czyscimy rejestr.
        /// </summary>
        public static void ResetPostfix(InventoryLogic __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ArmouryProtectUsed) return;
                if (_fRosters == null || _fArmory == null) return;
                var rosters = _fRosters.GetValue(__instance) as ItemRoster[];
                var armory = DteArmory();
                if (rosters == null || rosters.Length == 0 || armory == null || rosters[0] != armory) return;
                QuartermasterEscrow.OnScreenReset();
            }
            catch (Exception e) { Log.Error("ResetPostfix", e); }
        }

        /// <summary>DLL DTE nazywa sie z numerem wersji (v1.4.7) - szukamy typu po WSZYSTKICH zestawach.</summary>
        internal static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tArm = FindType("DynamicTroopEquipmentReupload.ArmyArmory");
                var tEvery = FindType("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                if (tArm == null || tEvery == null) { Log.Info("QuartermasterLaw: DTE nieobecne."); return; }
                _fArmory = AccessTools.Field(tArm, "Armory");
                _fThresholds = AccessTools.Field(tEvery, "EquipmentAndThresholds");
                _fRosters = AccessTools.Field(typeof(InventoryLogic), "_rosters");
                if (_fArmory == null || _fRosters == null)
                { Log.Info("QuartermasterLaw: brak pol (_rosters/Armory)."); return; }

                var m = AccessTools.Method(typeof(InventoryLogic), "TransferItem");
                if (m == null) { Log.Info("QuartermasterLaw: brak InventoryLogic.TransferItem."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(QuartermasterLaw), "Prefix"),
                        postfix: new HarmonyMethod(typeof(QuartermasterLaw), "BookPostfix"));

                // Reset/Cancel ekranu cofa rostery bez TransferItem - ksiega
                // musi cofnac sie razem z nimi
                var mReset = AccessTools.Method(typeof(InventoryLogic), "Reset");
                if (mReset != null)
                    h.Patch(mReset, postfix: new HarmonyMethod(typeof(QuartermasterLaw), "ResetPostfix"));

                // zbrojownia to nie smietnik: po kazdym przeliczeniu donacji
                // gasimy XP-za-discard, gdy lewa strona ekranu to magazyn DTE
                var mXp = AccessTools.Method(typeof(InventoryLogic), "InitializeXpGainFromDonations");
                if (mXp != null)
                    h.Patch(mXp, postfix: new HarmonyMethod(typeof(QuartermasterLaw), "XpDonationsPostfix"));

                // PROSCIEJ, jak chcial Jeff: przed otwarciem ekranu zbrojowni sprzet
                // noszony przez ludzi jest CHOWANY - widzisz tylko wolne nadwyzki.
                var tBeh = FindType("DynamicTroopEquipmentReupload.ArmyArmoryBehavior");
                var mOpen = tBeh != null ? AccessTools.Method(tBeh, "OpenArmoryScreen") : null;
                if (mOpen != null)
                    h.Patch(mOpen, prefix: new HarmonyMethod(typeof(QuartermasterEscrow), "HoldPrefix"));
                var tHelp = Type.GetType("Helpers.InventoryScreenHelper, TaleWorlds.CampaignSystem");
                var mClose = tHelp != null ? AccessTools.Method(tHelp, "CloseInventoryPresentation") : null;
                if (mClose != null)
                    h.Patch(mClose, postfix: new HarmonyMethod(typeof(QuartermasterEscrow), "ReleasePostfix"));
                var mClose2 = tHelp != null ? AccessTools.Method(tHelp, "CloseScreen") : null;
                if (mClose2 != null)
                    h.Patch(mClose2, postfix: new HarmonyMethod(typeof(QuartermasterEscrow), "ReleasePostfix"));
                Log.Info("QuartermasterLaw: ekran zbrojowni pokazuje TYLKO nadwyzki (open=" + (mOpen != null)
                         + ", close=" + (mClose != null) + "/" + (mClose2 != null) + ").");
            }
            catch (Exception e) { Log.Error("QuartermasterLaw.ApplyAll", e); }
        }
    }

    /// <summary>
    /// Depozyt kwatermistrza. Na czas ekranu zbrojowni sprzet NOSZONY przez
    /// zolnierzy (najlepsze sztuki kazdego typu, wedle progow DTE) jest
    /// wyjmowany z widoku, a po zamknieciu ekranu wraca na polki. Gracz widzi
    /// wylacznie wolne nadwyzki - bo luk lucznika nie lezy w magazynie,
    /// tylko wisi mu na plecach.
    /// </summary>
    internal static class QuartermasterEscrow
    {
        internal static bool Active;
        private static readonly List<KeyValuePair<EquipmentElement, int>> _held =
            new List<KeyValuePair<EquipmentElement, int>>();
        // sesja ekranu zbrojowni trwa (miedzy OpenArmoryScreen a zamknieciem);
        // pasy bezpieczenstwa nie rozliczaja wymian, poki ekran wisi
        private static bool _screenOpen;
        // migawka ksiegi z otwarcia - Reset/Cancel ekranu cofa do niej
        private static Dictionary<string, int> _stockAtOpen;

        internal static void HoldPrefix() { HoldReserve(); }

        // K1 (B3): ile sztuk KAZDEGO egzemplarza (id|mod) gracz moze wziac za darmo w tej sesji ekranu (jego czesc z porzadku przy
        // otwarciu + to, co w sesji odlozyl); ponad to - zapas ludzi, ktory oplaca przy zamknieciu
        private static readonly Dictionary<string, int> _free = new Dictionary<string, int>();
        private static readonly HashSet<string> _kitKeys = new HashSet<string>();
        private static Dictionary<string, int> _freeAtOpen;
        private static bool _freeOn;

        /// <summary>K1: wyjecie z ekranu - ile z n sztuk egzemplarza jest darmowe (czesc gracza). false = poza podzialem (licz per id, jak dotad).</summary>
        internal static bool TakeFree(string key, int n, out int free)
        {
            free = 0;
            if (!_freeOn || key == null || !_kitKeys.Contains(key)) return false;
            int v; _free.TryGetValue(key, out v);
            free = Math.Min(n, Math.Max(0, v));
            _free[key] = v - free;
            return true;
        }

        /// <summary>K1 (przeglad): to samo co TakeFree, ale bez zdejmowania - do sprawdzenia przed transferem (QuartermasterLaw.CanPayMen).</summary>
        internal static bool PeekFree(string key, int n, out int free)
        {
            free = 0;
            if (!_freeOn || key == null || !_kitKeys.Contains(key)) return false;
            int v; _free.TryGetValue(key, out v);
            free = Math.Min(n, Math.Max(0, v));
            return true;
        }

        /// <summary>K1: wklad odlozony w sesji - mozna go zabrac z powrotem za darmo.</summary>
        internal static void AddFree(string key, int n, bool kit)
        {
            if (!_freeOn || key == null || n <= 0) return;
            if (!kit && !_kitKeys.Contains(key)) return;   // sztuki spoza kitu - dalej per id (ksiega), jak dotad
            _kitKeys.Add(key);
            int v; _free.TryGetValue(key, out v); _free[key] = v + n;
        }

        private static void ClearFree() { _free.Clear(); _kitKeys.Clear(); _freeAtOpen = null; _freeOn = false; }
        internal static void ReleasePostfix() { ReleaseReserve(); }

        internal static void HoldReserve()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ArmouryProtectUsed || Active) return;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                // najpierw ksiega-duch: przytnij ksiege gracza do realnych
                // polek, zanim policzymy co chowac i co pokazac
                ArmouryBehavior.ReconcileStock("armoury-open");
                // PORZADEK W SKARBCU (Jeff 14.09): co nikt nie udzwignie - do sakw gracza
                // K1: porzadek liczy tez wymiane 1:1 i podzial egzemplarzy (noszone / czesc gracza) dla tego ekranu
                QuartermasterLaw.ClearSplit();
                QuartermasterLaw.PurgeUnusable(armory);
                // LISTA WYKLUCZONYCH (Jeff 15.09: "daj mi liste, co to byly za przedmioty"):
                // wszystko, czego straz bitewna nie wyda (unikaty, klingi lore, sprzet umarlych),
                // po id i ilosci - raz na otwarcie, do logu
                try
                {
                    var barred = new Dictionary<string, int>();
                    int barredTotal = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !QuartermasterLaw.BarredInBattle(it)) continue;
                        int v; barred.TryGetValue(it.StringId, out v); barred[it.StringId] = v + el.Amount;
                        barredTotal += el.Amount;
                    }
                    if (barredTotal > 0)
                    {
                        var parts = new List<string>();
                        foreach (var kv in barred) { if (parts.Count >= 60) { parts.Add("..."); break; } parts.Add(kv.Key + " x" + kv.Value); }
                        Log.Info("Kwatermistrz: WYKLUCZONE z wydawania (" + barredTotal + " szt., " + barred.Count + " id): "
                                 + string.Join(", ", parts.ToArray()) + ".");
                    }
                }
                catch { }
                _screenOpen = true;
                _stockAtOpen = ArmouryBehavior.StockSnapshot();
                _pendingSwaps.Clear();   // swieza sesja ekranu = swiezy rejestr wymian
                var needs = QuartermasterLaw.CountNeeds();

                // meldunek brakow PRZED schowaniem polek (pelna lista, z amunicja)
                bool anyShort = QuartermasterLaw.ShoutShortages("QM short (have/need):");

                // info Jeffa: zuzyte sztuki na polkach naprawia kowal w miescie
                // (liczone PRZED depozytem - ludzie nosza najlepsze, takze zuzyte)
                if (s.TroopMendEnabled)
                {
                    int wornPieces = 0, wreckPieces = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory[i];
                        var it = el.EquipmentElement.Item;
                        var m = el.EquipmentElement.ItemModifier;
                        if (it != null && el.Amount > 0 && !ArmouryBehavior.NoWear(it)
                            && m != null && m.PriceMultiplier < 0.999f && m.PriceMultiplier > 0f)
                        {
                            wornPieces += el.Amount;
                            if (LootPrices.SmithRefuses(m)) wreckPieces += el.Amount;   // paczka 158: wraki na zlom
                        }
                    }
                    if (wornPieces > 0)
                        InformationManager.DisplayMessage(new InformationMessage(
                            "QM: " + wornPieces + " pcs battle-worn - the smith mends them (Work the forge)."
                            + (wreckPieces > 0 ? " " + wreckPieces + " of them are wrecks - scrap now, no smith restores them for coin." : ""),
                            Colors.Yellow));
                }

                // NOWY LAD (Jeff 29.08): lupy 60% to WLASNOSC WOJSKA - dla oczu
                // gracza skarbiec wojskowy znika; widac sztuki z ksiegi wkladow gracza.
                // K1 (B3): chowamy to, co ludzie NOSZA (per egzemplarz: id + stan, z podzialu porzadku); widac sztuki gracza (za darmo)
                // i wolny zapas LUDZI - wziety zapas oplacasz im przy zamknieciu po cenie kupca (wpis 84, MenPurse.SettleBuys).
                // Sztuki spoza kitu i ekran bez podzialu (porzadek wylaczony) - jak dotad: widac tylko ksiege gracza (per id).
                var split = QuartermasterLaw.LastSplit;
                _free.Clear(); _kitKeys.Clear(); _freeAtOpen = null; _freeOn = split != null;
                var usedLeft = new Dictionary<string, int>();
                int visOwn = 0, visSpare = 0;
                if (split != null)
                    foreach (var kv in split)
                    {
                        usedLeft[kv.Key] = kv.Value[0]; _free[kv.Key] = kv.Value[1]; _kitKeys.Add(kv.Key);
                        visOwn += kv.Value[1]; visSpare += Math.Max(0, kv.Value[2] - kv.Value[0] - kv.Value[1]);
                    }
                var allowance = new Dictionary<string, int>();
                for (int i = armory.Count - 1; i >= 0; i--)
                {
                    var el = armory[i];
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    int hide, ul;
                    string key = QuartermasterLaw.KeyOf(el.EquipmentElement);
                    if (split != null && usedLeft.TryGetValue(key, out ul))
                    {
                        hide = Math.Min(el.Amount, Math.Max(0, ul));
                        usedLeft[key] = ul - hide;
                    }
                    else
                    {
                        string id = it.StringId ?? "";
                        int allowLeft;
                        if (!allowance.TryGetValue(id, out allowLeft)) allowLeft = ArmouryBehavior.StockOf(id);
                        int visible = Math.Min(el.Amount, Math.Max(0, allowLeft));
                        allowance[id] = allowLeft - visible;
                        hide = el.Amount - visible;
                    }
                    if (hide <= 0) continue;
                    armory.AddToCounts(el.EquipmentElement, -hide);
                    _held.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, hide));
                }
                if (_freeOn) _freeAtOpen = new Dictionary<string, int>(_free);
                bool swapMsg = split != null && s.QuartermasterSwapOneForOne;
                int xBack = Math.Min(visOwn, QuartermasterLaw.TakeUnseenX());   // K1 (przeglad): zwroty od ostatniego otwarcia, nie z porzadku otwarcia
                if (swapMsg)
                {
                    Log.Info("Wymiana: ekran zbrojowni - widoczne sztuki gracza " + visOwn + " (w tym zwroty z wymiany od ostatniego otwarcia " + xBack + "), zapas ludzi " + visSpare + " (do kupienia).");
                    if (visOwn + visSpare > 0)
                        InformationManager.DisplayMessage(new InformationMessage(
                            "QM: free to take - " + visOwn + " pcs that are yours" + (xBack > 0 ? " (" + xBack + " of them handed back in exchange)" : "") + "; the men's spare ("
                            + visSpare + " pcs) is theirs - take it and you pay them the merchant's price on closing.", Colors.Yellow));
                }
                Active = _held.Count > 0;
                if (Active)
                {
                    int pieces = 0;
                    foreach (var kv in _held) pieces += kv.Value;
                    Log.Info("Kwatermistrz: skarbiec wojskowy (" + pieces + " szt.) schowany - na liscie tylko wklady gracza.");
                    // 15.09: WIDOCZNA lista per wpis rostera (id + modyfikator + ile) - ksiega jest
                    // per id i slepa na modyfikator, a widocznosc idzie kolejnoscia rostera,
                    // wiec gracz moze dostac do reki INNE fizycznie sztuki niz wlozyl
                    try
                    {
                        var vis = new List<string>();
                        for (int i = 0; i < armory.Count && vis.Count < 20; i++)
                        {
                            var el = armory[i];
                            var it = el.EquipmentElement.Item;
                            if (it == null || el.Amount <= 0) continue;
                            var m = el.EquipmentElement.ItemModifier;
                            vis.Add(it.StringId + (m != null ? "(" + m.StringId + ")" : "") + " x" + el.Amount);
                        }
                        Log.Info("Kwatermistrz: na liscie gracza: " + (vis.Count > 0 ? string.Join(", ", vis.ToArray()) : "(pusto)")
                                 + (armory.Count > 20 ? ", ..." : ""));
                    }
                    catch { }
                    if (!swapMsg)
                        InformationManager.DisplayMessage(new InformationMessage(
                            "QM: listed = what no man wears; the men's kit stays hidden.",
                            Colors.Yellow));
                }
                if (!anyShort)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "QM: every man fully kitted.", Colors.Green));
                }
            }
            catch (Exception e) { Log.Error("Escrow.Hold", e); }
        }

        // wklady tej sesji ekranu - kandydaci do wymiany barterowej
        private static readonly List<KeyValuePair<ItemObject, KeyValuePair<int, int>>> _pendingSwaps =
            new List<KeyValuePair<ItemObject, KeyValuePair<int, int>>>();   // item -> (ile, wartosc szt.)

        internal static void NoteDeposit(ItemObject item, int n, int value)
        {
            try
            {
                if (item == null || n <= 0) return;
                // strzaly i belty TEZ podlegaja wymianie (Jeff 29.08: "nie
                // przyjmuje mi kwatermistrz strzal tier 6" - wykluczenie
                // amunicji zostawialo wklad bez rozliczenia i bez komunikatu).
                // KONIE tak samo (Jeff 30.08: "wrzucam konie, kwatermistrz
                // mowi ze zielono, a jak zabieram - ze brakuje; to samo co
                // z lukami") - kon nie ma weapon/armor component i wypadal
                // z rejestru: wklad konski nie szedl ani do wymiany, ani
                // do kompletu, wisial wiecznie jako wlasnosc gracza
                bool weaponish = item.HasWeaponComponent;
                bool mounty = item.HasHorseComponent;
                if (!weaponish && !item.HasArmorComponent && !mounty) return;
                _pendingSwaps.Add(new KeyValuePair<ItemObject, KeyValuePair<int, int>>(
                    item, new KeyValuePair<int, int>(n, value)));
            }
            catch { }
        }

        /// <summary>Gracz wycofal wklad w tej samej sesji ekranu: zdejmij
        /// ilosc z rejestru wymian (od najnowszych wpisow). Bez tego przy
        /// zamknieciu ProcessSwaps rozliczal wklad, ktorego juz nie bylo -
        /// wydawal gorsze sztuki wojska za nic albo zdejmowal z ksiegi
        /// gracza sztuki dawno zabrane.</summary>
        internal static void NoteWithdraw(ItemObject item, int n)
        {
            try
            {
                if (item == null || n <= 0 || _pendingSwaps.Count == 0) return;
                string id = item.StringId ?? "";
                for (int i = _pendingSwaps.Count - 1; i >= 0 && n > 0; i--)
                {
                    var kv = _pendingSwaps[i];
                    if (kv.Key == null || (kv.Key.StringId ?? "") != id) continue;
                    int cut = Math.Min(kv.Value.Key, n);
                    n -= cut;
                    int left = kv.Value.Key - cut;
                    if (left <= 0) _pendingSwaps.RemoveAt(i);
                    else _pendingSwaps[i] = new KeyValuePair<ItemObject, KeyValuePair<int, int>>(
                        kv.Key, new KeyValuePair<int, int>(left, kv.Value.Value));
                }
            }
            catch { }
        }

        /// <summary>Prawdziwe zamkniecie ekranu zbrojowni: oddaj depozyt
        /// i rozlicz wymiany tej sesji.</summary>
        internal static void ReleaseReserve() { ReleaseCore(true); }

        /// <summary>Pas bezpieczenstwa (save/hourly/menu): oddaj depozyt,
        /// ale wymian NIE rozliczaj, poki ekran zbrojowni realnie wisi
        /// (np. mod zapisujacy gre spod otwartego ekranu) - inaczej sesja
        /// gracza rozliczylaby sie w polowie klikania.</summary>
        internal static void SafetyRelease() { ReleaseCore(false); }

        private static void ReleaseCore(bool fromScreenClose)
        {
            try
            {
                if (fromScreenClose) _screenOpen = false;
                // _pendingSwaps tez trzyma otwarta sprawe: gdy CALY magazyn
                // nalezy do gracza, nic nie bylo schowane (Active=false),
                // a wklady z sesji i tak musza sie rozliczyc przy zamknieciu
                // K1: zakupy od ludzi (wziety zapas) tez trzymaja sprawe otwarta - inaczej przy pustym schowku nikt by za nie nie zaplacil
                if (!Active && _held.Count == 0 && _pendingSwaps.Count == 0 && !MenPurse.PendingBuys) { if (!_screenOpen) ClearFree(); return; }
                var armory = QuartermasterLaw.DteArmory();
                if (armory != null)
                    foreach (var kv in _held) armory.AddToCounts(kv.Key, kv.Value);
                _held.Clear();
                Active = false;
                if (!_screenOpen) { ClearFree(); ProcessSwaps(armory); MenPurse.SettleBuys(); }
            }
            catch (Exception e) { Log.Error("Escrow.Release", e); }
        }

        /// <summary>Reset/Cancel ekranu: rostery wrocily hurtem do kopii
        /// zapasowej, wiec ksiega i rejestr wymian cofaja sie do migawki
        /// z otwarcia.</summary>
        internal static void OnScreenReset()
        {
            try
            {
                if (!_screenOpen) return;
                ArmouryBehavior.StockRestore(_stockAtOpen);
                _pendingSwaps.Clear();
                MenPurse.ClearBuys();
                if (_freeOn && _freeAtOpen != null)   // K1: darmowe sztuki gracza jak przy otwarciu
                {
                    _free.Clear(); _kitKeys.Clear();
                    foreach (var kv in _freeAtOpen) { _free[kv.Key] = kv.Value; _kitKeys.Add(kv.Key); }
                }
                Log.Info("Kwatermistrz: Reset ekranu - ksiega wkladow i rejestr wymian cofniete do stanu z otwarcia.");
            }
            catch (Exception e) { Log.Error("Escrow.OnScreenReset", e); }
        }

        /// <summary>
        /// WYMIANA BARTEROWA (Jeff 29.08: "wrzucam luki t6, lucznicy je biora,
        /// a mnie wydaja gorsze luki, ktore sprzedam"). Za kazda wlozona sztuke
        /// LEPSZA od najgorszej wojskowej tego samego TYPU kwatermistrz wydaje
        /// graczowi te najgorsza DO SAKW - a wklad przechodzi na wlasnosc
        /// wojska (ksiega wkladow -1). Sztuki przypisane w ksiedze musztry
        /// nie sa wydawane. Bez gorszej sztuki - wklad zostaje wkladem gracza
        /// (mozna cofnac przy nastepnym otwarciu).
        /// </summary>
        /// <summary>Czy ktokolwiek w kompanii udzwignie ten przedmiot
        /// (zasada nadrzedna). Wypluwa tez najlepszy posiadany skill.</summary>
        internal static bool AnyoneCanUse(ItemObject item, out int bestSkill, out string skillName)
        {
            bestSkill = 0; skillName = "";
            try
            {
                var skill = ItemReq.SkillFor(item);
                if (skill == null || item.Difficulty <= 0) return true;
                skillName = skill.Name.ToString();
                var roster = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    int have = el.Character.GetSkillValue(skill);
                    if (have > bestSkill) bestSkill = have;
                }
                return bestSkill >= item.Difficulty;
            }
            catch { return true; }
        }

        /// <summary>K1 (przeglad, B4): dostawa od kowala to Twoj wklad poza ekranem - ten sam komunikat co przy wkladzie na ekranie, gdy nikt
        /// jej nie udzwignie (zostaje Twoja); reszte rozliczy najblizszy porzadek (wjazd do miasta, ekran zbrojowni).</summary>
        internal static void ShoutIfNoUse(ItemObject it)
        {
            try
            {
                int best; string sk;
                if (it != null && !AnyoneCanUse(it, out best, out sk))
                    Log.Player("QM: no man can use " + it.Name + " (needs " + sk + " " + it.Difficulty + ", best " + best + ") - stays yours.", true);
            }
            catch (Exception e) { Log.Error("Escrow.ShoutIfNoUse", e); }
        }

        private static void ProcessSwaps(ItemRoster armory)
        {
            // NAJPIERW BRAKI, POTEM WYMIANA (Jeff 14.09: "wrzucam strzaly 105,
            // zabral nowe, wydal stare - a w pierwszej kolejnosci powinien
            // uzupelniac braki; jak wszyscy maja, dopiero wymienia na lepsze;
            // dotyczy wszystkich przedmiotow"). Stara wymiana 1:1 "nowe za
            // najgorsze" nie zmieniala LICZBY sztuk na ludziach - luki zostawaly.
            // Teraz: jedno dopasowanie CALEJ polki (FitFor) - wklad idzie do
            // ludzi wszedzie tam, gdzie go nosza (kto nie mial nic, dostaje),
            // a wyparte gorsze sztuki wracaja na liste gracza dopiero, gdy
            // wszyscy sa obsadzeni. Wklad, ktorego nikt nie udzwignie, zostaje
            // gracza - z krotkim komunikatem.
            try
            {
                if (armory == null) { _pendingSwaps.Clear(); return; }
                foreach (var dep in _pendingSwaps)
                {
                    var it = dep.Key;
                    if (it == null) continue;
                    int bestSkill; string skillName;
                    if (!AnyoneCanUse(it, out bestSkill, out skillName))
                        Log.Player("QM: no man can use " + it.Name + " (needs " + skillName + " " + it.Difficulty
                                   + ", best " + bestSkill + ") - stays yours.", true);
                }
                QuartermasterLaw.PurgeUnusable(armory);
            }
            catch (Exception e) { Log.Error("Escrow.ProcessSwaps", e); }
            finally { _pendingSwaps.Clear(); }
        }
    }
}
