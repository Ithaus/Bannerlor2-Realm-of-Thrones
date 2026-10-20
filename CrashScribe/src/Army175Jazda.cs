using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>
    /// R3-08e (POMYSLY-35 nr 24, audyt 14 pkt 4.5) i R3-08f (nr 25, audyt 4.12) - Jeff 20.10: "pozostale tak".
    ///
    /// R3-08e LEPSZA JAZDA DOLINY I REACH (klucz Army175ValeReachKnights, dom. TAK):
    /// - vale_rider t3, vale_lancer t4, reach_rider t4: +20 Jazdy i +20 broni glownej (jak NorthHardy: ponad
    ///   max(obecna, wymog wlasnego sprzetu), bron glowna = MainMelee, sufit bez przeskoku tieru TierCap jak NorthHardy;
    ///   Jazda bez sufitu jak DothrakiRiders). Po SkillSinew (ten tylko podnosi - wczesniej dodatek bylby wchloniety).
    /// - vale_rider i vale_lancer: korpus o stopien ciezszy W GRANICY TIERU (decyzja 8b - nic ponad tier jednostki):
    ///   najciezszy korpus (suma ochrony) sposrod ciezszych niz dzis, tier od dzisiejszego do tieru jednostki,
    ///   kultura jednostki albo ta sama strona Waskiego Morza / bez kultury (bez zbroi z Essos), najpierw kultura jednostki.
    ///   Jak Qohor (QohorHeavier): w SESJI, po ArmorSanity, przed prawami i SkillSinew (Atletyke pod nowa wage podniesie SkillSinew).
    ///
    /// R3-08f KONNI LUCZNICY FREYOW (klucz Army175FreyHorseArchers, dom. TAK):
    /// - frey_rider t4 (z frey_cutthroat, awans do frey_horseman t5 - kopijnika - bez zmian): szyk HorseArcher
    ///   (DefaultFormationClass/Group, _isRanged - tak gra czyta default_group z XML), zestaw: krotki luk do jazdy
    ///   (MountOk, nie "long") i strzaly z kaskady 175.2 w tierze jednostki (BestOfType), 2 kolczany, bron zapasowa =
    ///   bron reczna z wlasnego zestawu uzywalna z siodla (Mends.MountOk = DTE IsSuitableForMount), najpierw klasy najwyzszej
    ///   umiejetnosci recznej (MainMelee), inaczej pierwsza inna (frey_rider: kopia przy Drzewcach 110, gdy MountOk, inaczej miecz),
    ///   bez tarczy. Nowy roster (InstallRoster) - stary roster i inne postacie nietkniete (DTE jak przy 175.2).
    ///   W bitwie z graczem bron i tak dobiera Armoury SkillsDecide (glowna = najwyzsza umiejetnosc, stad Luk ponad bron reczna),
    ///   a DTE daje jezdzcowi tylko bron uzywalna z siodla (PartyEquipmentDistributor.IsWeaponSuitable: !IsMounted | IsSuitableForMount).
    /// - Luk: max(obecny, wymog wlasnego luku i strzal, najwyzsza inna bron + 10), sufit bez przeskoku tieru; zawsze
    ///   co najmniej najwyzsza inna bron + 1 - SkillsDecide wybiera bron glowna po najwyzszej umiejetnosci (inaczej kopia).
    /// - Zestaw przy wczytaniu (AfterRegister, gdy 175.2 zadzialalo - TroopFit/ColdStart widza juz luk), zapasowo w sesji.
    ///
    /// Dziala tylko w bitwach z graczem i w wyborze sprzetu (wzorce: co AI kupuje, czego DTE szuka na polce); autobitwa liczy
    /// moc z tieru, a konni lucznicy Freyow dostaja w niej dodatki terenu konnych lucznikow (IsMounted i IsRanged).
    /// Danych ROT (XML) nie zmieniamy - wszystko w pamieci, od nowa przy kazdym wczytaniu.
    /// </summary>
    internal static partial class Army175
    {
        private const int KnightsBonus = 20;                                             // +20 Jazdy i +20 broni (audyt 4.5)
        private static readonly string[] KnightsSkillIds = { "vale_rider", "vale_lancer", "reach_rider" };
        private static readonly string[] ValeHeavyIds = { "vale_rider", "vale_lancer" };
        private const string FreyArcherId = "frey_rider";

        // _freySkillsDone osobno: zestaw Freyow moze wejsc dopiero w przebiegu zapasowym DailyCatchUp (doby 1-3), juz po starcie sesji
        private static bool _freyGearDone, _valeArmorDone, _knightsSkillsDone, _freySkillsDone, _freyConverted;
        private static string _valeArmorNote = "";
        private static readonly ConditionalWeakTable<MBCharacterSkills, object> _knightMark = new ConditionalWeakTable<MBCharacterSkills, object>();
        private static readonly ConditionalWeakTable<MBCharacterSkills, object> _freyMark = new ConditionalWeakTable<MBCharacterSkills, object>();

        private static readonly System.Reflection.FieldInfo FIsRanged = AccessTools.Field(typeof(BasicCharacterObject), "_isRanged");
        private static readonly System.Reflection.MethodInfo MSetFormationClass = AccessTools.PropertySetter(typeof(BasicCharacterObject), "DefaultFormationClass");

        /// <summary>Stan sesji (z Reset).</summary>
        internal static void ResetJazda()
        {
            _freyGearDone = false; _valeArmorDone = false; _knightsSkillsDone = false; _freySkillsDone = false; _freyConverted = false; _valeArmorNote = "";
        }

        /// <summary>Zestawy: Freyowie przy wczytaniu i w sesji (pierwszy udany przebieg), korpus Doliny tylko w sesji (po ArmorSanity).
        /// Wolane z OnObjectsRegistered (session false) i po QohorHeavier w TierGearCheck / DailyCatchUp (session true).</summary>
        internal static void JazdaGear(bool session)
        {
            try { if (!_freyGearDone) FreyGear(); } catch (Exception e) { _freyGearDone = true; try { Scribe.Report("CrashScribe", e, "Army175.FreyGear", null); } catch { } }
            if (!session) return;
            try { if (!_valeArmorDone) ValeArmor(); } catch (Exception e) { _valeArmorDone = true; try { Scribe.Report("CrashScribe", e, "Army175.ValeArmor", null); } catch { } }
        }

        // ---------------- R3-08f: zestaw i szyk konnego lucznika Freyow ----------------

        private static void FreyGear()
        {
            const string P = "Mends: konni lucznicy Freyow (R3-08f) - ";
            if (!Wanted("Army175FreyHorseArchers", 1f)) { _freyGearDone = true; Scribe.Line(P + "wylaczone (Army175FreyHorseArchers) - frey_rider zostaje kopijnikiem."); return; }
            if (!TierGearWanted()) { _freyGearDone = true; Scribe.Line(P + "pominiete (" + _tgOffWhy + "; luk i strzaly z kaskady sprzetu wedlug tieru - dziala tylko razem z 175.2)."); return; }
            if (!TierGearApplied) return;   // zestawy jeszcze puste - przebieg zapasowy w sesji zawola ponownie
            _freyGearDone = true;
            if (MSetFormationClass == null || FIsRanged == null || FRoster == null || FEquipments == null)
            {
                Scribe.Line(P + "OSTRZEZENIE: brak pol refleksji (DefaultFormationClass / _isRanged / roster) - pominiete, frey_rider bez zmian.");
                return;
            }
            var co = MBObjectManager.Instance.GetObject<CharacterObject>(FreyArcherId);
            if (co == null) { Scribe.Line(P + "brak jednostki " + FreyArcherId + " - pominiete."); return; }
            if (co.IsHero || !co.IsMounted) { Scribe.Line(P + FreyArcherId + " nie jest konnym zolnierzem (IsMounted " + co.IsMounted + ") - pominiete."); return; }
            if (_pool == null || _choice == null) PreparePass();
            var u = MakeUnit(co, true);
            var bow = BestOfType(ItemObject.ItemTypeEnum.Bow, c => Mends.MountOk(c) && !IsLongBow(c), u.T, u);
            var arrows = BestOfType(ItemObject.ItemTypeEnum.Arrows, c => true, u.T, u);
            if (bow == null || arrows == null)
            {
                Scribe.Line(P + "brak krotkiego luku do jazdy albo strzal w tierze " + u.T + " (luk " + (bow != null ? bow.StringId : "-") + ", strzaly "
                            + (arrows != null ? arrows.StringId : "-") + ") - pominiete, frey_rider bez zmian.");
                return;
            }
            var mainMelee = MainMelee(co);   // przy wczytaniu umiejetnosci z XML (SkillSinew jeszcze nie biegl)
            int sets = 0, changed = 0, already = 0;
            var removed = new List<string>();
            ItemObject keptSide = null;
            Rewrite(co, eq =>
            {
                ItemObject side = null, anyMelee = null; bool hasBow = false;
                for (int s = 0; s <= 3; s++)
                {
                    var x = eq[s].Item;
                    if (x == null) continue;
                    if (x.ItemType == ItemObject.ItemTypeEnum.Bow) { hasBow = true; continue; }
                    bool melee = x.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon || x.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon
                                 || x.ItemType == ItemObject.ItemTypeEnum.Polearm;
                    if (melee && Mends.MountOk(x))
                    {
                        if (side == null && x.RelevantSkill == mainMelee) side = x;
                        if (anyMelee == null) anyMelee = x;
                    }
                }
                if (hasBow) { already++; return null; }   // idempotentnie (drugi przebieg w tej samej grze)
                if (side == null) side = anyMelee;
                for (int s = 0; s <= 3; s++)
                {
                    var x = eq[s].Item;
                    if (x != null && x != side && removed.Count < 6) removed.Add(x.StringId);
                }
                var e = new Equipment(eq);
                e[0] = new EquipmentElement(bow);
                e[1] = new EquipmentElement(arrows);
                e[2] = new EquipmentElement(arrows);
                e[3] = side != null ? new EquipmentElement(side) : new EquipmentElement(null);
                if (keptSide == null) keptSide = side;
                changed++;
                return e;
            }, ref sets);
            if (changed == 0 && already == 0) { Scribe.Line(P + FreyArcherId + " bez zestawu bojowego (" + sets + ") - pominiete, szyk bez zmian."); return; }
            // szyk jak z XML default_group="HorseArcher": DefaultFormationClass, DefaultFormationGroup, _isRanged (_isMounted juz true)
            MSetFormationClass.Invoke(co, new object[] { FormationClass.HorseArcher });
            co.DefaultFormationGroup = (int)FormationClass.HorseArcher;
            FIsRanged.SetValue(co, true);
            _freyConverted = co.IsRanged && co.IsMounted && co.GetFormationClass() == FormationClass.HorseArcher;
            Scribe.Line(P + FreyArcherId + " t" + u.T + " (" + co.Name + "): szyk " + co.GetFormationClass() + " (IsMounted " + co.IsMounted + ", IsRanged " + co.IsRanged
                        + "), zestawow bojowych zmienionych " + changed + " z " + sets + (already > 0 ? " (juz z lukiem " + already + ")" : "")
                        + "; luk " + bow.StringId + " t" + TierOf(bow) + " (z siodla), strzaly " + arrows.StringId + " t" + TierOf(arrows) + " x2, bron zapasowa "
                        + (keptSide != null ? keptSide.StringId + " (" + mainMelee.StringId + ")" : "brak") + "; zdjete: " + (removed.Count > 0 ? string.Join(", ", removed.ToArray()) : "-")
                        + "; awans do frey_horseman (kopijnik t5) bez zmian" + (_freyConverted ? "." : " - OSTRZEZENIE: szyk nie przyjal sie."));
        }

        // ---------------- R3-08e: korpus Doliny o stopien ciezszy (w tierze) ----------------

        private static void ValeArmor()
        {
            const string P = "Mends: jazda Doliny i Reach (R3-08e) - ";
            if (!Wanted("Army175ValeReachKnights", 1f)) { _valeArmorDone = true; _valeArmorNote = "korpus wyl."; return; }   // linie "wylaczone" pisze JazdaSkills
            if (!TierGearWanted()) { _valeArmorDone = true; _valeArmorNote = "korpus pominiety (" + _tgOffWhy + ")"; Scribe.Line(P + "korpus pominiety (" + _tgOffWhy + "; ciezszy korpus w granicy tieru - tylko razem z 175.2)."); return; }
            if (!TierGearApplied) return;   // zestawy jeszcze puste - przebieg zapasowy zawola ponownie
            _valeArmorDone = true;
            if (_pool == null || _choice == null) PreparePass();
            _tierCache = new Dictionary<ItemObject, int>();   // tiery po rozsadku (jak QohorHeavier)
            List<ItemObject> lst;
            if (!_pool.TryGetValue(((int)ItemObject.ItemTypeEnum.BodyArmor).ToString(), out lst)) { _valeArmorNote = "korpus: brak puli"; Scribe.Line(P + "korpus: brak puli korpusow - pominiete."); return; }
            int slots = 0, sets = 0, stumbles = 0;
            var ex = new List<string>();
            foreach (var id in ValeHeavyIds)
            {
                try
                {
                    var co = MBObjectManager.Instance.GetObject<CharacterObject>(id);
                    if (co == null) { ex.Add(id + " brak"); continue; }
                    var u = MakeUnit(co, true);
                    int T = u.T, before = slots;
                    string side = Side(u.Cult);
                    Rewrite(co, eq =>
                    {
                        var it = eq[6].Item;   // korpus
                        if (it == null || it.ItemType != ItemObject.ItemTypeEnum.BodyArmor) return null;
                        int curSum = ArmorSum(it), curT = TierOf(it);
                        ItemObject best = null; bool bestOwn = false; int bestSum = -1; float bestW = -1f;
                        foreach (var c in lst)
                        {
                            if (c == it) continue;
                            int g = TierOf(c);
                            if (g > T || g < curT) continue;                 // w granicy tieru jednostki, nie nizszy tier niz dzis
                            int sum = ArmorSum(c);
                            if (sum <= curSum) continue;                     // tylko ciezszy niz dzis
                            if (Reject(it, c, u, 2) != 0) continue;           // plec, bandyckie, cywilne
                            bool own = SameTroopCult(c, u.Cult);
                            string sc = Side(CultOf(c));
                            if (!own && sc != "" && sc != side) continue;    // bez zbroi z drugiej strony Waskiego Morza
                            float w = c.Weight;
                            bool better = best == null || (own && !bestOwn)
                                || (own == bestOwn && (sum > bestSum || (sum == bestSum && (w > bestW || (w == bestW && string.CompareOrdinal(c.StringId, best.StringId) < 0)))));
                            if (better) { best = c; bestOwn = own; bestSum = sum; bestW = w; }
                        }
                        if (best == null) return null;
                        var clone = new Equipment(eq);
                        clone[6] = new EquipmentElement(best);
                        slots++;
                        if (ex.Count < 6) ex.Add(id + " t" + T + ": " + it.StringId + " t" + curT + " (ochrona " + curSum + ") -> " + best.StringId + " t" + TierOf(best) + " (" + bestSum + ")");
                        return clone;
                    }, ref sets);
                    if (slots == before) ex.Add(id + " t" + T + ": bez ciezszego korpusu w tierze");
                }
                catch { stumbles++; }
            }
            _valeArmorNote = "korpus ciezszy " + slots + " zestawow";
            Scribe.Line(P + "korpus o stopien ciezszy w granicy tieru (kultura jednostki, potem Westeros/bez kultury): " + slots + " zestawow z " + sets
                        + "; " + string.Join("; ", ex.ToArray()) + "; potkniecia " + stumbles + ".");
        }

        // ---------------- umiejetnosci (po SkillSinew) ----------------

        /// <summary>Raz w sesji, po SkillSinew (OnSessionLaunched po DothrakiRiders, zapasowo DailyTick). Dolina/Reach raz;
        /// Luk Freyow, gdy zestaw Freyow juz rozstrzygniety (przy wczytaniu albo w przebiegu zapasowym 175.2, ktory biegnie w tej
        /// samej dobie przed tym wywolaniem) - do tego czasu tylko dwa odczyty pol bool na dobe.</summary>
        internal static void JazdaSkills()
        {
            if (!Mends.SinewApplied) return;
            if (!_knightsSkillsDone)
            {
                _knightsSkillsDone = true;
                try { KnightsSkills(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.KnightsSkills", null); } catch { } }
            }
            if (!_freySkillsDone && _freyGearDone)
            {
                _freySkillsDone = true;
                try { FreySkills(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.FreySkills", null); } catch { } }
            }
        }

        private static float WeaponStep()
        {
            float wStep = Mends.ArmouryFloat("WeaponSkillPerTier", 35f);
            return wStep < 0.5f ? 0f : (wStep > 100f ? 100f : wStep);
        }

        private static void KnightsSkills()
        {
            const string P = "Mends: jazda Doliny i Reach (R3-08e) - ";
            if (!Wanted("Army175ValeReachKnights", 1f)) { Scribe.Line(P + "wylaczone (Army175ValeReachKnights)."); return; }
            float wStep = WeaponStep();
            int done = 0, shared = 0, stumbles = 0, capped = 0;
            var ex = new List<string>();
            foreach (var id in KnightsSkillIds)
            {
                try
                {
                    var co = MBObjectManager.Instance.GetObject<CharacterObject>(id);
                    if (co == null || co.IsHero) { ex.Add(id + " brak"); continue; }
                    var skills = co.GetDefaultCharacterSkills();
                    if (skills == null) { stumbles++; continue; }
                    object o;
                    if (_knightMark.TryGetValue(skills, out o)) { shared++; continue; }
                    var main = MainMelee(co);
                    int T = UnitTier(co);
                    int curR = co.GetSkillValue(DefaultSkills.Riding), curM = co.GetSkillValue(main);
                    int baseR = Math.Max(curR, OwnNeed(co, DefaultSkills.Riding)), baseM = Math.Max(curM, OwnNeed(co, main));
                    int tgtR = baseR + KnightsBonus;
                    int tgtM = TierCap(baseM + KnightsBonus, baseM, T, wStep);
                    if (tgtM < baseM + KnightsBonus) capped++;
                    if ((tgtR != curR && !SetSkill(co, DefaultSkills.Riding, tgtR)) || (tgtM != curM && !SetSkill(co, main, tgtM))) { stumbles++; continue; }
                    _knightMark.Add(skills, Mark);
                    done++;
                    ex.Add(id + " t" + T + " Riding " + curR + "->" + co.GetSkillValue(DefaultSkills.Riding) + " " + main.StringId + " " + curM + "->" + co.GetSkillValue(main));
                }
                catch { stumbles++; }
            }
            Scribe.Line(P + done + " z " + KnightsSkillIds.Length + " jednostek +" + KnightsBonus + " Jazdy i +" + KnightsBonus + " broni glownej (ponad wymog wlasnego sprzetu; bron z sufitem bez przeskoku tieru, przyciete "
                        + capped + "): " + string.Join(", ", ex.ToArray()) + "; " + (_valeArmorNote.Length > 0 ? _valeArmorNote : "korpus nie liczony (175.2 nie zadzialalo)")
                        + "; wspolne umiejetnosci pominiete " + shared + ", potkniecia " + stumbles + ".");
        }

        private static void FreySkills()
        {
            if (!_freyConverted) return;   // linia zestawu powiedziala juz, dlaczego nie
            const string P = "Mends: konni lucznicy Freyow (R3-08f) - ";
            var co = MBObjectManager.Instance.GetObject<CharacterObject>(FreyArcherId);
            if (co == null) return;
            var skills = co.GetDefaultCharacterSkills();
            if (skills == null) { Scribe.Line(P + "OSTRZEZENIE: brak umiejetnosci " + FreyArcherId + " - Luk bez zmian (potkniecie 1)."); return; }
            object o;
            if (_freyMark.TryGetValue(skills, out o)) return;
            int T = UnitTier(co);
            int curB = co.GetSkillValue(DefaultSkills.Bow), needB = OwnNeed(co, DefaultSkills.Bow);
            int maxOther = Math.Max(MaxMelee(co), Math.Max(co.GetSkillValue(DefaultSkills.Crossbow), co.GetSkillValue(DefaultSkills.Throwing)));
            int baseB = Math.Max(curB, needB);
            int tgt = TierCap(Math.Max(baseB, maxOther + 10), baseB, T, WeaponStep());
            if (tgt <= maxOther) tgt = maxOther + 1;   // luk MUSI byc najwyzsza umiejetnoscia (SkillsDecide), inaczej bron glowna = kopia
            if (tgt != curB && !SetSkill(co, DefaultSkills.Bow, tgt)) { Scribe.Line(P + "OSTRZEZENIE: zapis Luku " + FreyArcherId + " nieudany (potkniecie 1)."); return; }
            _freyMark.Add(skills, Mark);
            int now = co.GetSkillValue(DefaultSkills.Bow);
            Scribe.Line(P + FreyArcherId + " t" + T + " Luk " + curB + "->" + now + " (wymog wlasnego luku i strzal " + needB + ", najwyzsza inna bron " + maxOther
                        + "), Jazda " + co.GetSkillValue(DefaultSkills.Riding) + ", szyk " + co.GetFormationClass() + "; luk bronia glowna " + (now > maxOther ? "TAK" : "NIE - OSTRZEZENIE") + ".");
        }
    }
}
