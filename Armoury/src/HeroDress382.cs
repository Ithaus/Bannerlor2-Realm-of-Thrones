using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 382 (Jeff 09.11 "czemu czesc dowodcow i czemu sansa stark biega nago i inni niektorzy dowodcy"). NADZY BOHATEROWIE AI W BITWIE.
    /// Co sprawdzone w kodzie (wpis CHANGELOG 382): nasze straze sprzetu (sklad7b "nic z niczego", GearFit, Straz unikatow, 365, DressCode, Civvies) i DTE
    /// NIE dotykaja bohaterow - dzialaja na przydzialach DTE zolnierzy (DTE pomija IsHero). Bohater w bitwie idzie w Hero.BattleEquipment
    /// (Mission.SpawnTroop -> SpawnAgent: Equipment.GetRandomEquipmentElements(postac) - u bohatera CharacterObject.BattleEquipments = jego jeden zestaw).
    /// Dane: spis Z16 przy wczytaniu 09.11 - 2658 doroslych bohaterow AI: 383 BEZ ZADNEJ BRONI (sloty 0-3), 2544 pustych slotow pancerza 5-9 (bez imion);
    /// Sansa (lord_5_13_1) ma w ROT <![CDATA[<Equipments><EquipmentRoster/>]]> - pusty zestaw bojowy PIERWSZY (MBEquipmentRoster.InitEquipment dodaje go),
    /// potem bat_bat_template_sansa (suknia fm_noble_dress1 na tulowiu, miecz, luk). Gra przy starcie (Hero.SetInitialValuesFromCharacter) bierze
    /// losowy NIEPUSTY zestaw, ale Hero.ResetEquipments i kazda droga przez Template.FirstBattleEquipment daje ten pusty - takie postacie moga chodzic nago.
    /// Pewnego dowodu, ktora droga rozebrala bohaterow tej sesji, w logach nie ma - stad pomiar przy spawnie (ponizej) i naprawa tylko w misji.
    /// Regula (klucz AiHeroesNeverFightBare; bitwa kampanii z MapEvent gracza, sprzet bojowy - nie cywilny):
    ///  - bohater AI (nie gracz, nie rod gracza - im sprzet daje gracz; nie umarli Innych - ich nagosc jest z danych) przy spawnie: z KLONU zestawu,
    ///    ktory dostalby od gry (nadpis innego moda albo Hero.BattleEquipment), wypelniamy WYLACZNIE puste sloty:
    ///      bron 0-3 - tylko gdy wszystkie cztery puste (bez broni) - caly komplet broni z jednego zestawu wzorca (luk ze strzalami razem);
    ///      pancerz 5-9 - kazdy pusty slot osobno, jesli wzorzec ma tam sztuke;
    ///    (382b: kazda sztuka przez ItemReq.MeetsHero przy HeroGearRequirements, dystansowa tylko w parze z amunicja; pancerz tylko przy pustym tulowiu);
    ///    konie 10-11 bez zmian. Zrodlo: wlasny szablon XML postaci (BasicCharacterObject._equipmentRoster - jak UniqueLaw.InTemplate; bez pustych
    ///    zestawow), a gdy go brak - szablon lorda (IsLordTemplate) jego kultury i plci (bez szablonow wladcow, dzieci i mlodziezy), wybor staly dla bohatera.
    ///    Bez unikatow, legend, koron i sprzetu umarlych (UniqueGear.Is, LegendaryLaw.IsLegend, UniqueLaw.IsDead). Pancerz w granicy Atletyki
    ///    (HeroGearRequirements: ItemReq.MeetsHero; inaczej najlepsza sztuka tego typu w granicy - SkillsDecide.TopArmor, jak DressCode).
    ///  - 382c (Jeff 09.11 "zamieniac im przed bitwa suknie na zbroje? - tak"; klucz AiHeroesArmourOverDress): CYWILNY TULOW = suknia
    ///    (Civvies.Female i nie Civvies.Armour - ta sama regula co Civvies; w ROT suknie maja body_armor 10-60, wiec bez progu ochrony) albo
    ///    stroj meski (flaga Civilian, nie Civvies.Armour, body_armor < 30 - w ROT kaftany / "Noble Clothes" lordow maja 10-25; 45+ to Tyrion / Varys /
    ///    Baelish i master surcoat - zostaja). Taki tulow w bitwie -> zbroja tulowia ze wzorca, a glowa / nogi / rece - gdy puste albo cywilne
    ///    (plaszcz tylko pusty). Wzorzec pancerza: wlasny szablon bojowy BEZ cywilnego tulowia (u Sansy bat_bat_template_sansa to suknia - odpada),
    ///    potem szablon lordow jej kultury i plci, a u kobiet na koncu szablon lordow-mezczyzn kultury (sztuki NotUsableByFemale odpadaja).
    ///    Mezczyzni tez (ta sama regula, liczeni osobno): to samo zjawisko - ROT daje lordom z danych kaftan zamiast zbroi.
    ///    Gdy zbroi tulowia nie da sie dobrac (granica Atletyki, brak wzorca) - stroj zostaje caly (bez helmu do sukni).
    ///    Przy kluczu TAK takze nagi tulow bierze zbroje z tego lancucha (wzorzec-suknia tylko, gdy innego nie ma).
    ///  - Hero.BattleEquipment NIETKNIETE: sztuka istnieje tylko w misji (nadpis AgentBuildData.Equipment na klonie), lup z ciala bohatera i zwrot DTE
    ///    nie dotycza bohaterow - nic nie powstaje w swiecie; zamknieta ekonomia zolnierzy bez zmian. Wypelnienie gubi losowe modyfikatory jakosci,
    ///    ktore gra losuje bohaterom przy spawnie (GetRandomEquipmentElements z randomEquipmentModifier) - tylko u wypelnionych.
    /// Klucz NIE: ten sam pomiar, bez wypelniania. Linie: 'Nadzy (382): <imie> ...' (do 40 na misje) i 'Nadzy (382): koniec misji ...' (suma, imiona).
    /// Wyjatek na bohaterze = potkniecie (log 3 pierwszych), bez globalnego wylacznika.
    /// </summary>
    internal static class HeroDress382
    {
        private const int LinesMax = 40, NamesMax = 30, ErrMax = 3;
        private static readonly EquipmentIndex[] Armour = { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
        private static readonly System.Reflection.FieldInfo FRoster = AccessTools.Field(typeof(BasicCharacterObject), "_equipmentRoster");

        // pomiar jednej misji (glowny watek)
        private static Mission _m;
        private static int _heroes, _bareBody, _noWeapon, _dress, _civil, _fixed, _slots, _left, _lines, _stumbles, _errs, _swDress, _swCivil, _swLeft;
        private static readonly List<string> _nBare = new List<string>(), _nFixed = new List<string>(), _nDress = new List<string>(), _nSwap = new List<string>();
        private const int SwapNamesMax = 20;

        private static bool On { get { var s = Settings.Current; return s != null && s.AiHeroesNeverFightBare; } }
        /// <summary>382c: suknia / stroj cywilny na tulowiu -> zbroja (wymaga tez klucza AiHeroesNeverFightBare - to ta sama droga wypelniania).</summary>
        private static bool Over { get { var s = Settings.Current; return s != null && s.AiHeroesNeverFightBare && s.AiHeroesArmourOverDress; } }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(Mission), "SpawnAgent");
                if (m == null) { Log.Info("Nadzy (382): brak Mission.SpawnAgent - bez pomiaru i bez wypelniania."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(HeroDress382), nameof(Prefix)) { priority = Priority.Last });
                Log.Info("Nadzy (382): pomiar bohaterow AI przy spawnie w bitwie (bez zbroi tulowia / bez broni)" + (On ? " i wypelnianie pustych slotow ze wzorca (klucz AiHeroesNeverFightBare)." : " - wypelnianie WYLACZONE (klucz AiHeroesNeverFightBare)."));
            }
            catch (Exception e) { Log.Error("HeroDress382.ApplyAll", e); }
        }

        public static void Prefix(Mission __instance, AgentBuildData agentBuildData)
        {
            try
            {
                if (agentBuildData == null || __instance == null) return;
                var co = agentBuildData.AgentCharacter as CharacterObject;
                if (co == null || !co.IsHero) return;
                var hero = co.HeroObject;
                if (hero == null || hero == Hero.MainHero || (hero.Clan != null && hero.Clan == Clan.PlayerClan)) return;
                if (agentBuildData.AgentCivilianEquipment || MapEvent.PlayerMapEvent == null) return;
                if (Undead.Character(co)) return;                     // jedna regula na umarlych (Undead.Character, 382b)
                if (!ReferenceEquals(__instance, _m)) Begin(__instance);
                One(agentBuildData, co, hero);
            }
            catch (Exception e) { Stumble("Prefix", e); }
        }

        private static void One(AgentBuildData bd, CharacterObject co, Hero hero)
        {
            var src = bd.AgentOverridenSpawnEquipment ?? hero.BattleEquipment;
            if (src == null) return;
            _heroes++;
            bool bareBody = src[EquipmentIndex.Body].IsEmpty;
            bool noWeapon = true;
            for (int i = 0; i < 4; i++) if (!src[(EquipmentIndex)i].IsEmpty) { noWeapon = false; break; }
            int civ = bareBody ? 0 : CivilBody(src[EquipmentIndex.Body].Item);   // 382c: 1 suknia, 2 stroj cywilny
            if (civ == 1) { _dress++; if (_nDress.Count < NamesMax) _nDress.Add(Nm(hero)); }
            else if (civ == 2) _civil++;
            int emptyArm = 0;
            foreach (var s in Armour) if (src[s].IsEmpty) emptyArm++;
            bool over = civ != 0 && Over;
            if (!bareBody && !noWeapon && !over) return;                // pelny bohater (puste rekawice / plaszcz same nie sa "nago") - bez linii
            if (bareBody) _bareBody++;
            if (noWeapon) _noWeapon++;

            string done = "-";
            if (On)
            {
                Equipment tpl = OwnTemplate(co, true), wtpl = OwnTemplate(co, false);
                string from = "wlasny szablon";
                if (tpl == null && wtpl == null)
                {
                    tpl = CultureTemplate(hero, hero.IsFemale);
                    wtpl = tpl;
                    from = "szablon lorda kultury";
                }
                string afrom = from;
                if (Over && (bareBody || over))
                {
                    // 382c: pancerz z lancucha bez cywilnego tulowia; nagi tulow bez takiego wzorca - stary wzorzec (suknia lepsza niz nic)
                    string f2;
                    var a = ArmourTemplate(co, hero, out f2);
                    if (a != null) { tpl = a; afrom = f2; }
                    else if (over) tpl = null;
                }
                if (tpl != null || wtpl != null)
                {
                    var eq = src.Clone();
                    int n = 0;
                    var got = new List<string>();
                    bool req = Settings.Current != null && Settings.Current.HeroGearRequirements;
                    if (noWeapon && wtpl != null)
                    {
                        // 382b: komplet broni przez to samo sito co reszta drog bohatera (ItemReq.MeetsHero - umiejetnosc broni i Prawo Tieru
                        // przez Difficulty), a dystansowa bez pary (luk bez strzal / strzaly bez luku) wypada - bez polowek kompletu
                        var kit = new ItemObject[4];
                        for (int i = 0; i < 4; i++)
                        {
                            var it = wtpl[(EquipmentIndex)i].Item;
                            if (it == null || Banned(it)) continue;
                            if (req && !ItemReq.MeetsHero(co, it)) continue;
                            kit[i] = it;
                        }
                        Pair(kit);
                        for (int i = 0; i < 4; i++)
                        {
                            if (kit[i] == null) continue;
                            eq[(EquipmentIndex)i] = new EquipmentElement(kit[i]);
                            n++;
                            got.Add(kit[i].StringId);
                        }
                    }
                    int ath = 0;
                    try { ath = co.GetSkillValue(DefaultSkills.Athletics); } catch { }
                    bool swapped = false;
                    if (tpl != null && over)
                    {
                        // 382c: najpierw zbroja tulowia - bez niej stroj zostaje caly (bez helmu do sukni)
                        var body = Pick(tpl[EquipmentIndex.Body].Item, co, hero, req, ath);
                        if (body != null && CivilBody(body) == 0)
                        {
                            eq[EquipmentIndex.Body] = new EquipmentElement(body);
                            n++;
                            got.Add(body.StringId);
                            swapped = true;
                            foreach (var s in Armour)
                            {
                                if (s == EquipmentIndex.Body) continue;
                                if (!eq[s].IsEmpty && (s == EquipmentIndex.Cape || !CivilPiece(eq[s].Item))) continue;
                                var it = Pick(tpl[s].Item, co, hero, req, ath);
                                if (it == null || CivilPiece(it)) continue;
                                eq[s] = new EquipmentElement(it);
                                n++;
                                got.Add(it.StringId);
                            }
                        }
                    }
                    else if (tpl != null && bareBody)                // 382b: pancerz tylko gdy brak zbroi tulowia (opis MCM / CHANGELOG)
                    {
                        foreach (var s in Armour)
                        {
                            if (!eq[s].IsEmpty) continue;
                            var it = Pick(tpl[s].Item, co, hero, req, ath);
                            if (it == null) continue;
                            eq[s] = new EquipmentElement(it);
                            n++;
                            got.Add(it.StringId);
                        }
                    }
                    if (over)
                    {
                        if (swapped)
                        {
                            if (civ == 1) _swDress++; else _swCivil++;
                            if (_nSwap.Count < SwapNamesMax) _nSwap.Add(Nm(hero) + (civ == 1 ? "" : " (m)"));
                        }
                        else _swLeft++;
                    }
                    if (n > 0)
                    {
                        bd.Equipment(eq);
                        _fixed++;
                        _slots += n;
                        if (_nFixed.Count < NamesMax) _nFixed.Add(Nm(hero));
                        bool stillBody = eq[EquipmentIndex.Body].IsEmpty, stillW = true;
                        for (int i = 0; i < 4; i++) if (!eq[(EquipmentIndex)i].IsEmpty) { stillW = false; break; }
                        if (stillBody || (noWeapon && stillW)) _left++;
                        done = (swapped ? (civ == 1 ? "SUKNIA -> ZBROJA, " : "STROJ CYWILNY -> ZBROJA, ") : "") + "wypelniono " + n + " slotow z: " + (afrom != from ? "bron " + from + ", pancerz " + afrom : from) + " [" + string.Join(", ", got) + "]" + (stillBody ? ", TULOW NADAL PUSTY" : "") + (noWeapon && stillW ? ", NADAL BEZ BRONI" : "");
                    }
                    else { if (bareBody || noWeapon) _left++; done = "wzorzec (" + afrom + ") nie ma czym wypelnic - zostaje" + (over ? " (" + (civ == 1 ? "suknia" : "stroj cywilny") + " zostaje)" : ""); }
                }
                else { if (bareBody || noWeapon) _left++; if (over) _swLeft++; done = "brak wzorca (ani wlasnego, ani lorda kultury) - zostaje"; }
            }
            else done = "klucz AiHeroesNeverFightBare NIE - bez zmian";
            if ((bareBody || noWeapon) && _nBare.Count < NamesMax) _nBare.Add(Nm(hero));   // 382c: sama suknia to nie "nago"
            if (_lines++ < LinesMax)
                Log.Info("Nadzy (382): " + Nm(hero) + " (" + hero.StringId + ", " + (hero.Culture != null ? hero.Culture.StringId : "-") + ", " + (hero.IsFemale ? "k" : "m")
                         + ", strona " + (bd.AgentTeam != null ? bd.AgentTeam.Side.ToString() : "-") + ") | " + (bareBody ? "BEZ ZBROI TULOWIA" : "tulow " + src[EquipmentIndex.Body].Item.StringId + (civ == 1 ? " (SUKNIA)" : civ == 2 ? " (STROJ CYWILNY)" : ""))
                         + ", " + (noWeapon ? "BEZ BRONI" : "bron jest") + ", pustych slotow pancerza " + emptyArm + "/5 | zrodlo zestawu: " + (bd.AgentOverridenSpawnEquipment != null ? "nadpis spawnu" : "Hero.BattleEquipment")
                         + ", w Hero.BattleEquipment tulow " + (hero.BattleEquipment != null && !hero.BattleEquipment[EquipmentIndex.Body].IsEmpty ? "jest" : "PUSTY")
                         + " | szablon " + RosterId(co) + " | " + done + ".");
        }

        /// <summary>382b: bron dystansowa tylko w parze z amunicja (luk - strzaly, kusza - belty, palna - kule); bez pary obie strony wypadaja.</summary>
        private static void Pair(ItemObject[] kit)
        {
            Fix(kit, ItemObject.ItemTypeEnum.Arrows, ItemObject.ItemTypeEnum.Bow);
            Fix(kit, ItemObject.ItemTypeEnum.Bolts, ItemObject.ItemTypeEnum.Crossbow);
            Fix(kit, ItemObject.ItemTypeEnum.Bullets, ItemObject.ItemTypeEnum.Musket, ItemObject.ItemTypeEnum.Pistol);
        }

        private static void Fix(ItemObject[] kit, ItemObject.ItemTypeEnum ammo, params ItemObject.ItemTypeEnum[] launchers)
        {
            bool l = false, a = false;
            for (int i = 0; i < kit.Length; i++)
            {
                if (kit[i] == null) continue;
                if (Array.IndexOf(launchers, kit[i].ItemType) >= 0) l = true;
                else if (kit[i].ItemType == ammo) a = true;
            }
            if (l == a) return;
            for (int i = 0; i < kit.Length; i++)
                if (kit[i] != null && (Array.IndexOf(launchers, kit[i].ItemType) >= 0 || kit[i].ItemType == ammo)) kit[i] = null;
        }

        /// <summary>382c: cywilny tulow - 1 suknia (Civvies.Female, nie Civvies.Armour), 2 stroj (flaga Civilian, nie Armour, body_armor &lt; 30), 0 zbroja.</summary>
        internal static int CivilBody(ItemObject it)
        {
            if (it == null) return 0;
            if (Civvies.Armour(it)) return 0;
            if (Civvies.Female(it)) return 1;
            var ac = it.ArmorComponent;
            if (it.IsCivilian && (ac == null || ac.BodyArmor < 30)) return 2;
            return 0;
        }

        /// <summary>382c: cywilna sztuka glowy / nog / rak (suknia-obuwie damskie albo flaga Civilian, bez slow zbroi).</summary>
        private static bool CivilPiece(ItemObject it)
        {
            if (it == null || Civvies.Armour(it)) return false;
            return Civvies.Female(it) || it.IsCivilian;
        }

        /// <summary>Sztuka wzorca dla bohatera: bez unikatow, dla jego plci (NotUsableByFemale / Male), w granicy Atletyki (inaczej TopArmor jak 382).</summary>
        private static ItemObject Pick(ItemObject it, CharacterObject co, Hero hero, bool req, int ath)
        {
            if (it == null || Banned(it) || !Sex(it, hero)) return null;
            if (req && !ItemReq.MeetsHero(co, it))
            {
                it = SkillsDecide.TopArmor(it.ItemType, ath, hero.Culture);
                if (it == null || Banned(it) || !Sex(it, hero)) return null;
            }
            return it;
        }

        private static bool Sex(ItemObject it, Hero hero)
        {
            var f = it.ItemFlags;
            return hero.IsFemale ? (f & ItemFlags.NotUsableByFemale) == 0 : (f & ItemFlags.NotUsableByMale) == 0;
        }

        /// <summary>382c: wzorzec pancerza bez cywilnego tulowia - wlasny szablon bojowy, szablon lordow kultury i plci, u kobiet szablon lordow-mezczyzn kultury.</summary>
        private static Equipment ArmourTemplate(CharacterObject co, Hero hero, out string from)
        {
            var e = OwnTemplate(co, true, true);
            if (e != null) { from = "wlasny szablon bojowy (bez sukni)"; return e; }
            e = CultureTemplate(hero, hero.IsFemale, true);
            if (e != null) { from = "szablon lorda kultury" + (hero.IsFemale ? " (kobiety)" : ""); return e; }
            if (hero.IsFemale)
            {
                e = CultureTemplate(hero, false, true);
                if (e != null) { from = "szablon lorda kultury (mezczyzni)"; return e; }
            }
            from = "-";
            return null;
        }

        private static bool Banned(ItemObject it)
        {
            try
            {
                if (UniqueGear.Is(it) || LegendaryLaw.IsLegend(it) || UniqueLaw.IsDead(it)) return true;
                return (it.StringId ?? "").EndsWith("_crown", StringComparison.Ordinal);
            }
            catch { return true; }
        }

        /// <summary>Wlasny szablon XML: pierwszy niepusty zestaw bojowy z pancerzem (armour=true) albo z bronia (armour=false); puste zestawy pominiete.</summary>
        private static Equipment OwnTemplate(CharacterObject co, bool armour, bool armouredBody = false)
        {
            try
            {
                var ro = FRoster != null ? FRoster.GetValue(co) as MBEquipmentRoster : null;
                if (ro == null) return null;
                foreach (var e in ro.AllEquipments)
                {
                    if (e == null || !e.IsBattle || e.IsEmpty()) continue;
                    if (armouredBody && (e[EquipmentIndex.Body].IsEmpty || CivilBody(e[EquipmentIndex.Body].Item) != 0)) continue;   // 382c
                    if (armour)
                    {
                        foreach (var s in Armour) if (!e[s].IsEmpty) return e;
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++) if (!e[(EquipmentIndex)i].IsEmpty) return e;
                    }
                }
            }
            catch (Exception ex) { Stumble("OwnTemplate", ex); }
            return null;
        }

        private static string RosterId(CharacterObject co)
        {
            try
            {
                var ro = FRoster != null ? FRoster.GetValue(co) as MBEquipmentRoster : null;
                if (ro == null) return "brak";
                int all = 0, empty = 0;
                foreach (var e in ro.AllEquipments) { if (e == null || !e.IsBattle) continue; all++; if (e.IsEmpty()) empty++; }
                return ro.StringId + " (zestawow bojowych " + all + ", pustych " + empty + ")";
            }
            catch { return "?"; }
        }

        /// <summary>Szablon lorda kultury i plci bohatera (bez wladcow, dzieci, mlodziezy), wybor staly dla bohatera (hash StringId).</summary>
        private static Equipment CultureTemplate(Hero h, bool female, bool armouredBody = false)
        {
            try
            {
                if (h.Culture == null) return null;
                string cid = h.Culture.StringId ?? "";
                var cands = new List<Equipment>();
                var rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();
                if (rosters == null) return null;
                foreach (var ro in rosters)
                {
                    if (ro == null || ro.EquipmentCulture == null || ro.EquipmentCulture.StringId != cid) continue;
                    var c = ro.EquipmentCategories;
                    if ((c & EquipmentCategories.IsLordTemplate) == 0) continue;
                    if ((c & (EquipmentCategories.IsKingdomRulerTemplate | EquipmentCategories.IsChildEquipmentTemplate | EquipmentCategories.IsTeenagerEquipmentTemplate)) != 0) continue;
                    if (((c & EquipmentCategories.IsFemaleTemplate) != 0) != female) continue;
                    foreach (var e in ro.AllEquipments)
                        if (e != null && e.IsBattle && !e.IsEmpty() && !e[EquipmentIndex.Body].IsEmpty
                            && (!armouredBody || CivilBody(e[EquipmentIndex.Body].Item) == 0)) { cands.Add(e); break; }
                }
                if (cands.Count == 0) return null;
                int k = (int)((uint)(h.StringId ?? "").GetHashCode() % (uint)cands.Count);
                return cands[k];
            }
            catch (Exception e) { Stumble("CultureTemplate", e); return null; }
        }

        private static string Nm(Hero h)
        {
            try { return h.Name != null ? h.Name.ToString() : h.StringId; } catch { return "?"; }
        }

        private static void Begin(Mission m)
        {
            Flush();
            _m = m;
        }

        /// <summary>Linia sumy poprzedniej misji (wolana przy pierwszym spawnie bohatera nastepnej misji i z Logic.OnEndMission).</summary>
        internal static void Flush()
        {
            try
            {
                if (_m != null && _heroes > 0)
                    Log.Info("Nadzy (382): koniec misji | bohaterow AI przy spawnie " + _heroes + " | bez zbroi tulowia " + _bareBody + ", bez broni " + _noWeapon
                             + (_nBare.Count > 0 ? " [" + string.Join(", ", _nBare) + "]" : "")
                             + " | w sukni / stroju damskim na tulowiu " + _dress + (_nDress.Count > 0 ? " [" + string.Join(", ", _nDress) + "]" : "") + ", w stroju cywilnym " + _civil
                             + " | " + (Over ? "suknie zamienione " + _swDress + ", stroje cywilne zamienione " + _swCivil + (_nSwap.Count > 0 ? " [" + string.Join(", ", _nSwap) + "]" : "") + ", zostaly (brak zbroi we wzorcu / w granicy) " + _swLeft
                                             : "zamiana sukni wylaczona (AiHeroesArmourOverDress NIE)")
                             + " | " + (On ? "wypelniono " + _fixed + " bohaterow (" + _slots + " slotow)" + (_nFixed.Count > 0 ? " [" + string.Join(", ", _nFixed) + "]" : "") + ", nadal nago / bez broni " + _left
                                          : "wypelnianie wylaczone (AiHeroesNeverFightBare NIE)") + " | potkniecia " + _stumbles + ".");
            }
            catch { }
            _m = null;
            _heroes = _bareBody = _noWeapon = _dress = _civil = _fixed = _slots = _left = _lines = _stumbles = _errs = _swDress = _swCivil = _swLeft = 0;
            _nBare.Clear(); _nFixed.Clear(); _nDress.Clear(); _nSwap.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Info("Nadzy (382): potkniecie " + where + " - " + e.GetType().Name + ": " + e.Message); } catch { }
        }

        /// <summary>Linia sumy na koncu misji.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            protected override void OnEndMission()
            {
                try { if (ReferenceEquals(_m, Mission)) Flush(); } catch { }
                base.OnEndMission();
            }
        }
    }
}
