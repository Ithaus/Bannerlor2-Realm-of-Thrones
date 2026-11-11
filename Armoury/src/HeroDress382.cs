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
                var k = Fill(src, co, hero, bareBody, noWeapon, over, Over);
                if (k != null)
                {
                    var eq = k.Eq;
                    int n = k.N;
                    var got = k.Got;
                    bool swapped = k.Swapped;
                    string from = k.From, afrom = k.AFrom;
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

        /// <summary>389: wynik doboru - klon zestawu z wypelnionymi slotami (N sztuk), zrodla wzorca broni (From) i pancerza (AFrom).</summary>
        internal sealed class Kit
        {
            public Equipment Eq;
            public int N;
            public List<string> Got;
            public bool Swapped;
            public string From, AFrom;
            /// <summary>389c: 0 bron ze zwyklego wzorca (albo brak), 1 zapasowa z szablonu lorda kultury, 2 z szablonu gentry BK, 3 z puli kultury.</summary>
            public int Spare;
            /// <summary>389c: bron, ktora odpadla (id:powod - unikat / sito / bez pary), do 12 wpisow; null = nic nie odpadlo.</summary>
            public List<string> Rej;
        }

        /// <summary>Dobor 382 / 382b / 382c wydzielony z One (389: ten sam dobor dla trwalego "spoznionego dorosniecia" - HeroArms389).
        /// src nietkniete (klon). null = brak wzorca (ani wlasnego, ani lorda kultury). overKey = klucz AiHeroesArmourOverDress (389 podaje true).</summary>
        internal static Kit Fill(Equipment src, CharacterObject co, Hero hero, bool bareBody, bool noWeapon, bool over, bool overKey)
        {
            Equipment tpl = OwnTemplate(co, true), wtpl = OwnTemplate(co, false);
            // 389c-p: cywil z danych (postac z XML z WLASNYM zestawem bojowym bez broni - Varys, Baelish) zostaje bez broni takze w bitwie gracza:
            // przed 389c wzorzec "nie mial czym wypelnic", lancuch zapasowy nie jest dla niego (decyzja 389c, opis klucza)
            bool civByData = false;
            try { civByData = co != null && co.IsOriginalCharacter && tpl != null && wtpl == null; } catch { civByData = false; }
            string from = "wlasny szablon";
            if (tpl == null && wtpl == null)
            {
                tpl = CultureTemplate(hero, hero.IsFemale);
                wtpl = tpl;
                from = "szablon lorda kultury";
            }
            string afrom = from;
            if (overKey && (bareBody || over))
            {
                // 382c: pancerz z lancucha bez cywilnego tulowia; nagi tulow bez takiego wzorca - stary wzorzec (suknia lepsza niz nic)
                string f2;
                var a = ArmourTemplate(co, hero, out f2);
                if (a != null) { tpl = a; afrom = f2; }
                else if (over) tpl = null;
            }
            bool spare = noWeapon && SpareOn && !civByData;   // 389c: bez wzorca tez probujemy broni zapasowej; 389c-p: nie dla cywila z danych
            if (tpl == null && wtpl == null && !spare) return null;
            bool noTpl = tpl == null && wtpl == null;
            var eq = src.Clone();
            int n = 0, spareKind = 0;
            var got = new List<string>();
            List<string> rej = null;
            bool req = Settings.Current != null && Settings.Current.HeroGearRequirements;
            if (noWeapon && wtpl != null) n += Weapons(wtpl, co, req, eq, got, ref rej);
            if (spare && n == 0)
            {
                // 389c: wzorzec nie dal ZADNEJ broni (brak zestawu bojowego w szablonie, sito umiejetnosci, luk bez strzal) - lancuch zapasowy
                string sf;
                int sn = SpareArms(co, hero, req, eq, got, wtpl, ref rej, out spareKind, out sf);
                if (sn > 0) { n += sn; from = sf; if (noTpl) afrom = "-"; }
            }
            if (noTpl && n == 0) return null;                 // jak dotad: brak wzorca (i zapas nic nie dal)
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
            return new Kit { Eq = eq, N = n, Got = got, Swapped = swapped, From = from, AFrom = afrom, Spare = spareKind, Rej = rej };
        }

        /// <summary>382b (wydzielone w 389c bez zmiany reguly): komplet broni z jednego zestawu przez to samo sito co reszta drog bohatera
        /// (ItemReq.MeetsHero - umiejetnosc broni i Prawo Tieru przez Difficulty), a dystansowa bez pary (luk bez strzal / strzaly bez luku) wypada -
        /// bez polowek kompletu. Sloty 0-3 w eq sa puste (wolane tylko przy noWeapon). Zwraca liczbe wlozonych sztuk.</summary>
        private static int Weapons(Equipment w, CharacterObject co, bool req, Equipment eq, List<string> got, ref List<string> rej)
        {
            var kit = new ItemObject[4];
            for (int i = 0; i < 4; i++)
            {
                var it = w[(EquipmentIndex)i].Item;
                if (it == null) continue;
                if (Banned(it)) { Rejected(ref rej, it, "unikat"); continue; }
                if (req && !ItemReq.MeetsHero(co, it)) { Rejected(ref rej, it, "sito"); continue; }
                kit[i] = it;
            }
            var pre = (ItemObject[])kit.Clone();
            Pair(kit);
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                if (kit[i] == null) { if (pre[i] != null) Rejected(ref rej, pre[i], "bez pary"); continue; }
                eq[(EquipmentIndex)i] = new EquipmentElement(kit[i]);
                n++;
                got.Add(kit[i].StringId);
            }
            return n;
        }

        private static void Rejected(ref List<string> rej, ItemObject it, string why)
        {
            if (rej == null) rej = new List<string>();
            if (rej.Count < 12) rej.Add(it.StringId + ":" + why);
        }

        // ------------------------------------------------------------ 389c: bron zapasowa
        private static readonly Dictionary<string, List<CharacterObject>> _gentry = new Dictionary<string, List<CharacterObject>>();
        private static readonly Dictionary<string, List<ItemObject>[]> _pool = new Dictionary<string, List<ItemObject>[]>();
        private static readonly string[] PoolSkip = { "practice", "wooden", "training", "tournament", "blunt", "torch", "dummy", "test", "tool", "sickle", "pitchfork", "hoe", "pickaxe", "hammer", "scythe" };

        private static bool SpareOn { get { var s = Settings.Current; return s != null && s.AiHeroesSpareArms; } }

        /// <summary>389c: pamiec szablonow gentry i puli broni od nowa (nowa kampania / wczytanie - obiekty gry sa nowe).</summary>
        internal static void ResetSpare() { _gentry.Clear(); _pool.Clear(); }

        /// <summary>389c (Jeff 11.11 "tak, rob wszystko 2-4", pkt 3 "202 bohaterow nadal bez broni (187 kobiet) - ich wzorzec nie ma broni"; klucz
        /// AiHeroesSpareArms). Lancuch, gdy zwykly wzorzec nie dal ZADNEJ broni - kazde ogniwo przez to samo sito (Banned, ItemReq.MeetsHero, Pair):
        ///  1. bron z szablonu lorda kultury i plci bohatera, u kobiet potem z szablonu lordow-mezczyzn (gdy wlasny szablon mial bron ponad umiejetnosci);
        ///  2. bron z szablonu postaci gentry BK tej kultury (Occupation.Lord, mezczyzna, id z "bannerkings_gentry_") - ten sam miecz, ktory dostali bracia
        ///     (corki gentry i dworki BK maja szablon "_spouse_" z samymi zestawami cywilnymi - MBEquipmentRoster nie ma tam zestawu bojowego);
        ///  3. pula kultury: jedna bron jednoreczna (dopiero potem dwureczna / drzewcowa) kultury bohatera albo bez kultury, w granicy umiejetnosci
        ///     (sito ZAWSZE, takze przy wylaczonym HeroGearRequirements - inaczej kazdy dostalby najwyzszy tier), najwyzszy tier, miecz przed sztyletem
        ///     przed reszta, wybor staly dla bohatera (hash StringId). Bron Dothrakow dla obcych nie wchodzi z definicji (pula = kultura bohatera / bez kultury).
        /// Zwraca liczbe wlozonych sztuk; kind 1 / 2 / 3 = ogniwo, from = opis do logu.</summary>
        private static int SpareArms(CharacterObject co, Hero hero, bool req, Equipment eq, List<string> got, Equipment tried, ref List<string> rej, out int kind, out string from)
        {
            kind = 0; from = null;
            try
            {
                int n;
                var e = CultureTemplate(hero, hero.IsFemale);
                if (e != null && !ReferenceEquals(e, tried))
                {
                    n = Weapons(e, co, req, eq, got, ref rej);
                    if (n > 0) { kind = 1; from = "bron zapasowa: szablon lorda kultury"; return n; }
                }
                if (hero.IsFemale)
                {
                    e = CultureTemplate(hero, false);
                    if (e != null && !ReferenceEquals(e, tried))
                    {
                        n = Weapons(e, co, req, eq, got, ref rej);
                        if (n > 0) { kind = 1; from = "bron zapasowa: szablon lorda kultury (m)"; return n; }
                    }
                }
                var gl = Gentry(hero);
                if (gl != null && gl.Count > 0)
                {
                    int start = (int)((uint)(hero.StringId ?? "").GetHashCode() % (uint)gl.Count);
                    for (int j = 0; j < gl.Count; j++)
                    {
                        e = OwnTemplate(gl[(start + j) % gl.Count], false);
                        if (e == null || ReferenceEquals(e, tried)) continue;
                        n = Weapons(e, co, req, eq, got, ref rej);
                        if (n > 0) { kind = 2; from = "bron zapasowa: szablon gentry"; return n; }
                    }
                }
                var it = PoolArm(co, hero);
                if (it != null)
                {
                    eq[EquipmentIndex.WeaponItemBeginSlot] = new EquipmentElement(it);
                    got.Add(it.StringId);
                    kind = 3; from = "bron zapasowa: pula kultury";
                    return 1;
                }
            }
            catch (Exception ex) { Stumble("SpareArms", ex); }
            return 0;
        }

        private static List<CharacterObject> Gentry(Hero hero)
        {
            if (hero.Culture == null) return null;
            string cid = hero.Culture.StringId ?? "";
            List<CharacterObject> l;
            if (_gentry.TryGetValue(cid, out l)) return l;
            l = new List<CharacterObject>();
            foreach (var c in CharacterObject.All)
            {
                if (c == null || c.IsHero || c.IsFemale || c.Occupation != Occupation.Lord || c.Culture == null || c.Culture.StringId != cid) continue;
                if ((c.StringId ?? "").IndexOf("bannerkings_gentry_", StringComparison.Ordinal) < 0) continue;
                l.Add(c);
            }
            _gentry[cid] = l;
            return l;
        }

        /// <summary>Pula kultury: [0] jednoreczne, [1] dwureczne i drzewcowe; kazda lista od najwyzszego tieru, w tierze miecz, sztylet, reszta.</summary>
        private static ItemObject PoolArm(CharacterObject co, Hero hero)
        {
            if (hero.Culture == null) return null;
            string cid = hero.Culture.StringId ?? "";
            List<ItemObject>[] p;
            if (!_pool.TryGetValue(cid, out p))
            {
                p = new[] { new List<ItemObject>(), new List<ItemObject>() };
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !it.HasWeaponComponent || it.PrimaryWeapon == null) continue;
                    int li = it.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon ? 0
                           : it.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon || it.ItemType == ItemObject.ItemTypeEnum.Polearm ? 1 : -1;
                    if (li < 0 || it.NotMerchandise || it.IsCraftedByPlayer || Banned(it) || GiantGear.Is(it)) continue;   // 389c-p: bez wyrobow gracza (jak gra, Drill, CastleSteel, ArmsScrap)
                    string ic = it.Culture != null ? it.Culture.StringId : null;
                    if (!string.IsNullOrEmpty(ic) && ic != "neutral_culture" && ic != cid) continue;
                    string id = it.StringId ?? "";
                    if (id.StartsWith("dragon_", StringComparison.Ordinal)) continue;
                    bool skip = false;
                    foreach (var w in PoolSkip) if (id.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) { skip = true; break; }
                    if (skip) continue;
                    p[li].Add(it);
                }
                foreach (var l in p) l.Sort((a, b) => { int c = PoolRank(b).CompareTo(PoolRank(a)); return c != 0 ? c : string.CompareOrdinal(a.StringId, b.StringId); });
                _pool[cid] = p;
                Log.Info("Bezbronni (389c): pula broni zapasowej kultury " + cid + " - jednorecznych " + p[0].Count + ", dwurecznych / drzewcowych " + p[1].Count + ".");
            }
            foreach (var l in p)
            {
                int rank = int.MinValue;
                List<ItemObject> band = null;
                foreach (var it in l)
                {
                    int r = PoolRank(it);
                    if (band != null && r != rank) break;                 // lista posortowana: pierwsze pasmo w granicy umiejetnosci
                    if (!ItemReq.MeetsHero(co, it)) continue;
                    if (band == null) { band = new List<ItemObject>(); rank = r; }
                    band.Add(it);
                }
                if (band != null) return band[(int)((uint)(hero.StringId ?? "").GetHashCode() % (uint)band.Count)];
            }
            return null;
        }

        private static int PoolRank(ItemObject it)
        {
            var wc = it.PrimaryWeapon.WeaponClass;
            int cls = wc == WeaponClass.OneHandedSword || wc == WeaponClass.TwoHandedSword ? 2 : wc == WeaponClass.Dagger ? 1 : 0;
            return (int)it.Tier * 4 + cls;
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
            // 423: prog 30 w starej skali (49 sztuk *_garb itd. nie zmienia klasy); 418-p: stroje Varysa / Tyriona / Baelisha (99 -> 14 po 418) licza sie
            // wartoscia sprzed 418 - zostaja "zbroja" jak w naglowku 382c (382c ich nie przebiera, HeroArms389 nie doposaza)
            if (it.IsCivilian && (ac == null || RotData418.BookBody(it, ArmourLadder423.BookBody(it, ac.BodyArmor)) < 30)) return 2;
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

        internal static bool Sex(ItemObject it, Hero hero)
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

        internal static bool Banned(ItemObject it)
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

        internal static string RosterId(CharacterObject co)
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
