using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
    /// - vale_rider i vale_lancer: zbroja o stopien ciezsza W GRANICY TIERU (decyzja 8b - nic ponad tier jednostki), TYLKO
    ///   rzeczy Doliny (kultura vale albo noszone przez zolnierzy Doliny - SameTroopCult; bez obcych barw): w kazdym slocie
    ///   pancerza 5-9 (glowa, korpus, nogi, rece, ramiona) najciezsza czesc w tierze jednostki ciezsza niz dzis (wiecej ochrony
    ///   i nie lzejsza w kg), pusty slot (rece, ramiona) dostaje czesc Doliny; najpierw rzeczy z targu (lord je kupi).
    ///   Korpusu Doliny ciezszego w tierze nie ma (t3: najwyzej 104, t4: tylko grafton_armor 123, nastepny royce_armor t5) -
    ///   korpus zostaje, a linia logu podaje najblizszy ciezszy ponad tier. Recenzja 20.10: poprzednia wersja (dowolna
    ///   kultura Westeros) dawala kolczuge Polnocy i dornijska zbroje lzejsza w kg - odrzucona.
    ///   Jak Qohor (QohorHeavier): w SESJI, po ArmorSanity, przed prawami i SkillSinew (Atletyke pod nowa wage podniesie SkillSinew).
    ///
    /// R3-08f KONNI LUCZNICY FREYOW (klucz Army175FreyHorseArchers, dom. TAK):
    /// - frey_rider t4 (z frey_cutthroat, awans do frey_horseman t5 - kopijnika - bez zmian): szyk HorseArcher
    ///   (DefaultFormationClass/Group, _isRanged - tak gra czyta default_group z XML), zestaw: krotki luk do jazdy
    ///   (MountOk, nie "long") i strzaly z kaskady 175.2 w tierze jednostki (BestOfType), 2 kolczany, bron zapasowa =
    ///   bron reczna z wlasnego zestawu uzywalna z siodla (Mends.MountOk = DTE IsSuitableForMount), najpierw klasy najwyzszej
    ///   umiejetnosci recznej (MainMelee), inaczej pierwsza inna; frey_rider: zawsze miecz (narrow_sword_t3) - kopia nie przechodzi
    ///   MountOk (uzycie polearm_bracing ma requires_no_mount), wiec wzorzec i zakupy AI (AiGear.Deficit z 1. zestawu) to miecz;
    ///   bez tarczy. Nowy roster (InstallRoster) - stary roster i inne postacie nietkniete (DTE jak przy 175.2).
    ///   W bitwie z graczem bron i tak dobiera Armoury SkillsDecide (glowna = najwyzsza umiejetnosc, stad Luk ponad bron reczna;
    ///   zapas = druga umiejetnosc, Drzewce 110 > 1H, czyli w bitwie drzewce zamiast miecza ze wzorca), a DTE daje jezdzcowi
    ///   tylko bron uzywalna z siodla (PartyEquipmentDistributor.IsWeaponSuitable: !IsMounted | IsSuitableForMount).
    /// - Cel skladu ROT (recenzja 20.10): ROTTroopRecruiter.GetTemplateComposition liczy cel rodu z DefaultFormationClass stosow,
    ///   a sklad partii (GetPartyComposition, ShouldRecruit, szansa awansu ROTTroopUpgradeModel, zamiany ExchangeClanTroops)
    ///   z klasy NAJWYZSZEGO awansu w szablonie - frey_rider liczy sie tam jako frey_horseman (Cavalry). Bez poprawki cel Freyow
    ///   Cav 21% -> 7%, KL 14% (nigdy do wypelnienia) i ROT odciagalby Freyow od jazdy. Postfiks RotTemplateFrey liczy stos
    ///   frey_rider w klasie jego najwyzszego awansu, tak jak ROT liczy sklad partii - cel jak przed zmiana (Cav 21%, KL 0).
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

        // sloty pancerza 5-9 i rodzaj dla pustego slotu (R3-08e)
        private static readonly int[] ValeSlots = { 5, 6, 7, 8, 9 };
        private static readonly ItemObject.ItemTypeEnum[] ValeSlotTypes = { ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.BodyArmor,
            ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.Cape };
        private static readonly string[] ValeSlotNames = { "glowa", "korpus", "nogi", "rece", "ramiona" };

        // R3-08f: cel skladu ROT - frey_rider liczony w klasie najwyzszego awansu (ustawiane w FreyGear, na watku glownym)
        private static CharacterObject _freyCo;
        private static FormationClass _freyTopClass = FormationClass.Cavalry;
        private static string _freyTopId = "";
        private static bool _rotTplPatched;
        private static Type _rotCompType;
        private static System.Reflection.PropertyInfo _pCompHa, _pCompTop;
        private static int _rotTplCalls, _rotTplFixed, _rotTplStumbles, _rotTplLastCalls, _rotTplLastStumbles, _rotTplFirst, _rotTplStumbleLogged;

        /// <summary>Stan sesji (z Reset).</summary>
        internal static void ResetJazda()
        {
            _freyGearDone = false; _valeArmorDone = false; _knightsSkillsDone = false; _freySkillsDone = false; _freyConverted = false; _valeArmorNote = "";
            _freyCo = null; _freyTopClass = FormationClass.Cavalry; _freyTopId = "";
            _rotTplCalls = _rotTplFixed = _rotTplStumbles = _rotTplLastCalls = _rotTplLastStumbles = _rotTplFirst = _rotTplStumbleLogged = 0;
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
            // cel skladu ROT: klasa najwyzszego awansu (jak ROT GetPartyComposition) - frey_rider -> frey_horseman (Cavalry)
            var top = co;
            for (int d = 0; d < 8 && top.UpgradeTargets != null && top.UpgradeTargets.Length > 0 && top.UpgradeTargets[0] != null; d++) top = top.UpgradeTargets[0];
            _freyTopClass = top != co ? top.DefaultFormationClass.FallbackClass() : FormationClass.HorseArcher;
            _freyTopId = top.StringId ?? "";
            _freyCo = _freyConverted ? co : null;
            Scribe.Line(P + FreyArcherId + " t" + u.T + " (" + co.Name + "): szyk " + co.GetFormationClass() + " (IsMounted " + co.IsMounted + ", IsRanged " + co.IsRanged
                        + "), zestawow bojowych zmienionych " + changed + " z " + sets + (already > 0 ? " (juz z lukiem " + already + ")" : "")
                        + "; luk " + bow.StringId + " t" + TierOf(bow) + " (z siodla), strzaly " + arrows.StringId + " t" + TierOf(arrows) + " x2, bron zapasowa "
                        + (keptSide != null ? keptSide.StringId + " (" + mainMelee.StringId + ")" : "brak") + "; zdjete: " + (removed.Count > 0 ? string.Join(", ", removed.ToArray()) : "-")
                        + "; awans do frey_horseman (kopijnik t5) bez zmian" + (_freyConverted ? "" : " - OSTRZEZENIE: szyk nie przyjal sie")
                        + "; cel skladu ROT: stos " + FreyArcherId + " liczony jako " + _freyTopClass + " (najwyzszy awans " + _freyTopId + "), postfiks "
                        + (_rotTplPatched ? "wpiety" : "NIE wpiety - OSTRZEZENIE: cel jazdy Freyow w ROT spadnie z ok. 21% do 7%") + ".");
        }

        // ---------------- R3-08f: cel skladu ROT (postfiks ROTTroopRecruiter.GetTemplateComposition) ----------------

        /// <summary>Wpiecie postfiksu (raz na proces, z Army175.Install). Typ ROT po nazwie (internal), metoda na klasie deklarujacej.</summary>
        internal static void InstallJazda(Harmony harmony)
        {
            const string P = "Mends: konni lucznicy Freyow (R3-08f) - ";
            try
            {
                if (_rotTplPatched) return;
                var t = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTTroopRecruiter");
                var m = t != null ? AccessTools.DeclaredMethod(t, "GetTemplateComposition", new[] { typeof(PartyTemplateObject) }) : null;
                if (m == null) { Scribe.Line(P + "ROTTroopRecruiter.GetTemplateComposition nieznaleziony - cel skladu ROT bez poprawki (frey_rider liczony jako konny lucznik)."); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(Army175), "RotTemplateFrey"));
                _rotTplPatched = true;
                Scribe.Line(P + "postfiks celu skladu ROT (ROTTroopRecruiter.GetTemplateComposition) wpiety: stos frey_rider liczony w klasie najwyzszego awansu, jak sklad partii ROT.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.InstallJazda", null); } catch { } }
        }

        /// <summary>Postfiks: cel rodu (udzialy P/S/J/KL stosow t3+, wagi MaxValue) - stos przerobionego frey_rider przenosi swoj
        /// udzial z HorseArcher do klasy najwyzszego awansu (Cavalry). Wynik ROT liczy raz na bohatera/osade (pamiec Settings), wiec
        /// to nie goraca sciezka; bez alokacji poza refleksja wlasciwosci. Liczniki Interlocked; potkniecia licz, nie gas.</summary>
        public static void RotTemplateFrey(PartyTemplateObject template, object __result)
        {
            try
            {
                var frey = _freyCo;
                if (!_freyConverted || frey == null || template == null || __result == null || _freyTopClass == FormationClass.HorseArcher) return;
                var stacks = template.Stacks;
                if (stacks == null) return;
                int mine = 0; float num = 0f;
                for (int i = 0; i < stacks.Count; i++)
                {
                    var ch = stacks[i].Character;
                    if (ch == null || ch.Tier < 3) continue;   // jak ROT: tylko stosy t3+
                    num += stacks[i].MaxValue;
                    if (ch == frey) mine += stacks[i].MaxValue;
                }
                if (mine <= 0 || num <= 0f) return;
                Interlocked.Increment(ref _rotTplCalls);
                if (frey.DefaultFormationClass != FormationClass.HorseArcher) return;   // ROT liczy go juz w dawnej klasie
                var rt = __result.GetType();
                if (rt != _rotCompType)
                {
                    _pCompHa = AccessTools.Property(rt, "HorseArcher");
                    _pCompTop = AccessTools.Property(rt, _freyTopClass == FormationClass.Infantry ? "Infantry" : _freyTopClass == FormationClass.Ranged ? "Ranged" : "Cavalry");
                    _rotCompType = rt;
                }
                if (_pCompHa == null || _pCompTop == null) { RotTplStumble("brak wlasciwosci HorseArcher/Cavalry w " + rt.FullName); return; }
                float d = mine / num;
                float ha = (float)_pCompHa.GetValue(__result, null), tp = (float)_pCompTop.GetValue(__result, null);
                _pCompHa.SetValue(__result, ha - d > 0f ? ha - d : 0f, null);
                _pCompTop.SetValue(__result, tp + d, null);
                Interlocked.Increment(ref _rotTplFixed);
                if (Interlocked.CompareExchange(ref _rotTplFirst, 1, 0) == 0)
                    Scribe.Line("Mends: konni lucznicy Freyow (R3-08f) - cel skladu ROT " + template.StringId + ": stos " + FreyArcherId + " (" + mine + " z " + num
                                + " ludzi t3+) przeniesiony z HorseArcher do " + _freyTopClass + " - " + _freyTopClass + " " + Math.Round(100.0 * tp) + "% -> "
                                + Math.Round(100.0 * (tp + d)) + "%, HorseArcher " + Math.Round(100.0 * ha) + "% -> " + Math.Round(100.0 * (ha - d > 0f ? ha - d : 0f)) + "%.");
            }
            catch (Exception e) { RotTplStumble(e.GetType().Name); }
        }

        private static void RotTplStumble(string why)
        {
            Interlocked.Increment(ref _rotTplStumbles);
            if (Interlocked.CompareExchange(ref _rotTplStumbleLogged, 1, 0) != 0) return;
            try
            {
                Scribe.Line("Mends: konni lucznicy Freyow (R3-08f) - OSTRZEZENIE: postfiks celu skladu ROT potknal sie (" + why
                            + ") - cel jazdy Freyow w ROT moze spasc; dalsze potkniecia tylko w liczniku dobowym.");
            }
            catch { }
        }

        /// <summary>Raz na dobe (z JazdaSkills), gdy przybylo wywolan albo potkniec: licznik postfiksu celu skladu ROT.</summary>
        private static void FreyRotDaily()
        {
            if (!_freyConverted) return;
            int calls = _rotTplCalls, st = _rotTplStumbles;
            if (calls == _rotTplLastCalls && st == _rotTplLastStumbles) return;
            _rotTplLastCalls = calls; _rotTplLastStumbles = st;
            Scribe.Line("Mends: konni lucznicy Freyow (R3-08f) - cel skladu ROT od startu sesji: szablonow z " + FreyArcherId + " " + calls
                        + ", poprawionych (" + FreyArcherId + " jako " + _freyTopClass + ") " + _rotTplFixed + ", potkniecia " + st + ".");
        }

        // ---------------- R3-08e: zbroja Doliny o stopien ciezsza (w tierze, tylko rzeczy Doliny) ----------------

        private static void ValeArmor()
        {
            const string P = "Mends: jazda Doliny i Reach (R3-08e) - ";
            if (!Wanted("Army175ValeReachKnights", 1f)) { _valeArmorDone = true; _valeArmorNote = "zbroja wyl."; return; }   // linie "wylaczone" pisze JazdaSkills
            if (!TierGearWanted()) { _valeArmorDone = true; _valeArmorNote = "zbroja pominieta (" + _tgOffWhy + ")"; Scribe.Line(P + "zbroja pominieta (" + _tgOffWhy + "; ciezsza zbroja w granicy tieru - tylko razem z 175.2)."); return; }
            if (!TierGearApplied) return;   // zestawy jeszcze puste - przebieg zapasowy zawola ponownie
            _valeArmorDone = true;
            if (_pool == null || _choice == null) PreparePass();
            _tierCache = new Dictionary<ItemObject, int>();   // tiery po rozsadku (jak QohorHeavier)
            int pieces = 0, setsChanged = 0, sets = 0, stumbles = 0;
            var ex = new List<string>();
            var bodyNote = new List<string>();
            foreach (var id in ValeHeavyIds)
            {
                try
                {
                    var co = MBObjectManager.Instance.GetObject<CharacterObject>(id);
                    if (co == null) { ex.Add(id + " brak"); continue; }
                    var u = MakeUnit(co, true);
                    int T = u.T, before = pieces;
                    if (u.Cult.Length == 0) { ex.Add(id + " bez kultury - pominiete"); continue; }
                    Rewrite(co, eq =>
                    {
                        Equipment clone = null;
                        for (int k = 0; k < ValeSlots.Length; k++)
                        {
                            int s = ValeSlots[k];
                            var it = eq[s].Item;
                            if (it != null && Cat(it) != 3) continue;
                            var ty = it != null ? it.ItemType : ValeSlotTypes[k];
                            List<ItemObject> lst;
                            if (_pool == null || !_pool.TryGetValue(((int)ty).ToString(), out lst)) continue;
                            int curSum = it != null ? ArmorSum(it) : 0;
                            float curW = it != null ? it.Weight : 0f;
                            ItemObject best = null, up = null; bool bestMerch = false; int bestSum = -1, upT = 99, upSum = -1; float bestW = -1f;
                            foreach (var c in lst)
                            {
                                if (c == it) continue;
                                int sum = ArmorSum(c); float w = c.Weight;
                                if (sum <= curSum || w < curW) continue;         // ciezsza = wiecej ochrony i nie lzejsza w kg
                                if (!SameTroopCult(c, u.Cult)) continue;          // tylko rzeczy Doliny (kultura albo noszone przez zolnierzy Doliny)
                                if (Reject(it, c, u, 2) != 0) continue;          // plec, bandyckie, cywilne
                                int g = TierOf(c);
                                if (g > T)
                                {
                                    if (s == 6 && (g < upT || (g == upT && sum > upSum))) { up = c; upT = g; upSum = sum; }   // tylko do logu
                                    continue;                                    // decyzja 8b: nic ponad tier jednostki
                                }
                                bool merch = !c.NotMerchandise;                  // najpierw rzeczy z targu - lord je kupi
                                bool better = best == null || (merch && !bestMerch)
                                    || (merch == bestMerch && (sum > bestSum || (sum == bestSum && (w > bestW || (w == bestW && string.CompareOrdinal(c.StringId, best.StringId) < 0)))));
                                if (better) { best = c; bestMerch = merch; bestSum = sum; bestW = w; }
                            }
                            if (best == null)
                            {
                                if (s == 6 && it != null && bodyNote.Count < 4)
                                    bodyNote.Add(id + " " + it.StringId + " t" + TierOf(it) + " (" + curSum + ")" + (up != null ? ", najblizszy ciezszy " + up.StringId + " t" + upT + " (" + upSum + ")" : ""));
                                continue;
                            }
                            if (clone == null) clone = new Equipment(eq);
                            clone[s] = new EquipmentElement(best);
                            pieces++;
                            if (ex.Count < 12)
                                ex.Add(id + " t" + T + " " + ValeSlotNames[k] + ": " + (it != null ? it.StringId + " t" + TierOf(it) + " (" + curSum + ")" : "pusto")
                                       + " -> " + best.StringId + " t" + TierOf(best) + " (" + bestSum + ")");
                        }
                        if (clone != null) setsChanged++;
                        return clone;
                    }, ref sets);
                    if (pieces == before) ex.Add(id + " t" + T + ": bez ciezszych rzeczy Doliny w tierze");
                }
                catch { stumbles++; }
            }
            _valeArmorNote = "zbroja ciezsza " + pieces + " czesci w " + setsChanged + " zestawach";
            Scribe.Line(P + "zbroja o stopien ciezsza w granicy tieru (tylko rzeczy Doliny, glowa-ramiona): " + pieces + " czesci w " + setsChanged + " z " + sets
                        + " zestawow; " + string.Join("; ", ex.ToArray())
                        + (bodyNote.Count > 0 ? "; korpus bez zmian - brak ciezszego korpusu Doliny w tierze (decyzja 8b): " + string.Join(", ", bodyNote.ToArray()) : "")
                        + "; potkniecia " + stumbles + ".");
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
            try { FreyRotDaily(); } catch { }   // licznik postfiksu celu skladu ROT (tylko gdy cos sie dzialo)
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
                        + capped + "): " + string.Join(", ", ex.ToArray()) + "; " + (_valeArmorNote.Length > 0 ? _valeArmorNote : "zbroja nie liczona (175.2 nie zadzialalo)")
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
