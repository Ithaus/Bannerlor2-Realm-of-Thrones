using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m104-wojny (3) - MLODE SMOKI DAENERYS (klucz DaenerysDragonsGrow; kazda kampania). TYLKO KAMPANIA - zadnej latki bitwy ani autobitwy.
    ///
    /// ROZPOZNANIE (ROT 8.1.8, dekompilacja; tor "smoki" journalu wf_d4f0c951-09f - wnioski zgodne):
    ///  - ROT-Dragon nie ma DLL - same zasoby (Monster "dragon" chodzacy i "dragonfly" latajacy). Smoki to przedmioty Type="Horse" (ROTassets.xml): dragon_black
    ///    "Drogon" (body_length 105, extra_health 1000, speed 45, difficulty 200), dragon_brown "Rhaegal" (85, speed 40), dragon_green "Viserion" (75, speed 35), dragon_gold,
    ///    dragon_smoke, dragon_red (quest Valyrian Thief) + latajace "...2". Gra skaluje agenta wierzchowca 0.01 x body_length (Mission.BuildAgent: SetInitialAgentScale) -
    ///    Viserion jest w bitwie o 29% mniejszy od Drogona.
    ///  - Daenerys (lord_1_14) dostaje zestaw ghi_bat_template_dany ze slotem Horse = dragon_black: dorosly Drogon od pierwszego dnia ROT (i od dnia -188 prologu).
    ///    W bitwie to jedyny smok: ROTDragonMissionBehavior (OnDeploymentFinished: SpawnMissingDragonMounts, forma latajaca, HP = ROTSettings.DragonHealth dla kazdego
    ///    smoka), ogien i szarza z globalnym ROTSettings.DragonDamageScaling; autobitwa ROTCombatSimulationModel.SimulateHit - kazdy cios bohatera ze smokiem w slocie 10
    ///    = plaskie DragonDamageScaling. ROT nie ma wieku ani wzrostu smokow. ROTGankBehavior.OnHeroPrisonerReleased: Daenerys wychodzaca z niewoli z PUSTYM slotem
    ///    konia dostaje pierwszego nieskradzionego smoka (black, brown, green).
    ///  - Nasze: CrashScribe Mends.DragonPurge (smoki tylko Daenerys i rod gracza; NoDragonGank blokuje zabieranie smoka), Armoury DragonUnmount (lord_1_14 zostaje na
    ///    smoku przy spawnie, smok bez wymogu Jazdy), DragonFeed (smok w slocie konia je 2 owce na dobe).
    /// CO DA SIE BEZPIECZNIE: tylko slot konia zestawu bojowego Daenerys - ten sam slot decyduje o polu bitwy i o autobitwie, a ROT i nasze filtry rozpoznaja smoki
    ///  po id dragon_* i MonsterUsage. Etapy wedlug dnia opowiesci (D = dzien najazdu Daenerys na osi Fabuli = 450 x skala, przy 4.33 ok. 1949):
    ///    jaja       - prolog (dzien opowiesci &lt; 0): bez smoka;
    ///    smoczeta   - 0 .. 0.40 D (ok. 779, Astapor - pierwszy ogien Drogona w ksiazce): bez smoka w bitwie (za male, by walczyc);
    ///    mlode      - 0.40 D .. 0.625 D (ok. 1218): najmniejszy smok ROT dragon_green (body_length 75, speed 35);
    ///    podrosle   - 0.625 D .. 0.85 D (ok. 1656, arena Daznaka - Drogon pierwszy raz niesie Daenerys): dragon_brown (85, speed 40);
    ///    dorosle    - od 0.85 D albo po DanyInvasionEvent: dragon_black - Drogon jak w ROT (105, extra_health 1000, speed 45).
    ///  Smok skradziony (ROTGankBehavior._stolenDragons, stare zapisy) - nastepny z kolejnosci etapu; wszystkie skradzione - bez smoka (jak ROT).
    ///  Bez smoka: najlepszy zwykly wierzchowiec (Stables.IsPlainMount, tier, potem wartosc) z taboru partii, ktora Daenerys prowadzi - do slotu (nic z niczego);
    ///  brak konia albo partii - pusty slot (pieszo). Powrot smoka: kon ze slotu wraca do taboru jej partii; uprzaz innej rodziny niz smok (AccessViolation w AddMountMesh
    ///  - Mends) tez do taboru; bez partii - zamiana czeka do doby, w ktorej Daenerys prowadzi partie.
    ///  Kiedy: start sesji, co dobe (ArmouryBehavior - PRZED DragonFeed, karmienie widzi biezacy wierzchowiec) i zaraz po wyjsciu Daenerys z niewoli (CampaignEvents.
    ///  HeroPrisonerReleased; sluchacz ROT, ktory daje smoka przy pustym slocie, zapisany wczesniej - widzimy jego skutek).
    /// CZEGO NIE ROBIMY (ryzyko, bez testu z bitwa): HP i sily ognia mlodego smoka w bitwie (ROT ustawia HP raz przy rozstawieniu, TransformDragonForm tworzy nowego
    ///  agenta z HP monstera; ogien i szarza czytaja globalne DragonDamageScaling - skala per smok wymagalaby latki na Agent.RegisterBlow, goraca sciezka kazdego ciosu),
    ///  autobitwy (ROT daje plaskie DragonDamageScaling kazdemu ciosowi bohatera ze smokiem - u Jeffa 10, wiec "slabszy mlody smok" w autobitwie i tak by nie rosl rowno),
    ///  karmienia DragonFeed wedlug wieku, wlasnych modeli mlodych smokow (XML). Patrz CHANGELOG.
    /// Klucz wylaczony: smok, ktorego zdjelismy, wraca raz (dorosly wedlug ROT), potem nic. Daenerys martwa, Inna (kultura whitewalker) albo gracz - bez zmian.
    /// Stan w zapisie (arm_m104): dm = wierzchowiec zmieniony przez nas, ds = ostatni etap (komunikat dla gracza tylko przy zmianie etapu w trakcie gry).
    /// </summary>
    internal static partial class Prologue
    {
        private const string DanyHeroId = "lord_1_14";
        private const float FracYoung = 0.40f, FracGrown = 0.625f, FracAdult = 0.85f;
        private static readonly string[] DStageName = { "jaja", "smoczeta", "mlode", "podrosle", "dorosle" };
        private const int DEggs = 0, DHatch = 1, DYoung = 2, DGrown = 3, DAdult = 4;
        // smoki Daenerys ROT (ROTGankBehavior._dragons) w kolejnosci etapu; dorosle - jak ROT (pierwszy nieskradziony: black, brown, green)
        private static readonly string[][] StageDragons =
        {
            null,
            null,
            new[] { "dragon_green", "dragon_brown", "dragon_black" },
            new[] { "dragon_brown", "dragon_green", "dragon_black" },
            new[] { "dragon_black", "dragon_brown", "dragon_green" },
        };

        private static bool DragonsKey { get { var s = S; return s != null && s.DaenerysDragonsGrow; } }

        // stan (zapis arm_m104)
        private static bool _dMod;
        private static int _dStage = -1;
        // sesja
        private static string _dState = "-";
        private static int _dChangesDay;

        private static void ResetDragons() { _dMod = false; _dStage = -1; _dState = "-"; _dChangesDay = 0; }

        private static string PackDragons() { return "dm=" + (_dMod ? "1" : "0") + ";ds=" + _dStage.ToString(Inv) + ";"; }

        private static void UnpackDragons(Dictionary<string, string> map)
        {
            ResetDragons();
            string v;
            _dMod = map.TryGetValue("dm", out v) && v == "1";
            _dStage = ParseInt(map, "ds", -1);
        }

        // ---------------------------------------------------------------- etapy

        /// <summary>Etap smokow wedlug dnia opowiesci; next - dzien opowiesci nastepnego etapu (-1 = brak).</summary>
        private static int DragonStage(double sd, out float next)
        {
            float d = DanyInvasionDay();
            next = -1f;
            if (DanyInvaded()) return DAdult;
            if (sd < 0) { next = 0f; return DEggs; }
            if (sd >= FracAdult * d) return DAdult;
            if (sd >= FracGrown * d) { next = FracAdult * d; return DGrown; }
            if (sd >= FracYoung * d) { next = FracGrown * d; return DYoung; }
            next = FracYoung * d;
            return DHatch;
        }

        private static bool _gankTried;
        private static Type _tGank;
        private static FieldInfo _fStolen;

        /// <summary>Smoki zabrane Daenerys (ROTGankBehavior._stolenDragons; CrashScribe blokuje dialog - tylko stare zapisy).</summary>
        private static List<string> StolenDragons()
        {
            var r = new List<string>();
            try
            {
                if (!_gankTried)
                {
                    _gankTried = true;
                    _tGank = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTGankBehavior");
                    _fStolen = _tGank != null ? AccessTools.Field(_tGank, "_stolenDragons") : null;
                }
                if (_tGank == null || _fStolen == null || Campaign.Current == null) return r;
                var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                var beh = mi != null ? mi.MakeGenericMethod(_tGank).Invoke(Campaign.Current, null) : null;
                var l = beh != null ? _fStolen.GetValue(beh) as System.Collections.IEnumerable : null;
                if (l != null) foreach (var x in l) if (x is string) r.Add((string)x);
            }
            catch (Exception e) { WStumble("StolenDragons", e); }
            return r;
        }

        /// <summary>Smok etapu (pierwszy nieskradziony z kolejnosci etapu); null - etap bez smoka albo wszystkie skradzione.</summary>
        private static ItemObject StageDragon(int stage, List<string> stolen)
        {
            var ids = stage >= 0 && stage < StageDragons.Length ? StageDragons[stage] : null;
            if (ids == null) return null;
            foreach (var id in ids)
            {
                if (stolen.Contains(id)) continue;
                var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                if (it != null) return it;
            }
            return null;
        }

        private static Hero _dany;
        private static Campaign _danyCamp;

        private static Hero Dany()
        {
            if (_dany == null || _danyCamp != Campaign.Current)
            {
                _danyCamp = Campaign.Current;
                _dany = HeroAny(DanyHeroId);
            }
            return _dany;
        }

        private static bool IsWalkerHero(Hero h)
        {
            try { return (h.Culture != null && h.Culture.StringId == "whitewalker") || (h.Clan != null && h.Clan.Culture != null && h.Clan.Culture.StringId == "whitewalker"); }
            catch { return false; }
        }

        /// <summary>Najlepszy zwykly wierzchowiec w taborze (Stables.IsPlainMount - bez slonia, mamuta, wielblada, smoka i jucznego): tier, potem wartosc.</summary>
        private static EquipmentElement BestMount(ItemRoster ro, out bool found)
        {
            found = false;
            var best = default(EquipmentElement);
            if (ro == null) return best;
            int bestTier = int.MinValue, bestValue = int.MinValue;
            for (int i = 0; i < ro.Count; i++)
            {
                if (ro.GetElementNumber(i) <= 0) continue;
                var el = ro.GetElementCopyAtIndex(i).EquipmentElement;
                var it = el.Item;
                if (it == null || !Stables.IsPlainMount(it)) continue;
                int tier = (int)it.Tier, value = it.Value;
                if (tier > bestTier || (tier == bestTier && value > bestValue)) { bestTier = tier; bestValue = value; best = el; found = true; }
            }
            return best;
        }

        private static bool HarnessFits(ItemObject harness, ItemObject mount)
        {
            try
            {
                var ac = harness != null ? harness.ArmorComponent : null;
                var mon = mount != null && mount.HorseComponent != null ? mount.HorseComponent.Monster : null;
                return ac != null && mon != null && ac.FamilyType == mon.FamilyType;
            }
            catch { return false; }
        }

        private static string Id(ItemObject it) { return it != null ? it.StringId ?? "?" : "pusto"; }

        /// <summary>Uprzaz innej rodziny niz nowy wierzchowiec (np. siodlo smoka na koniu) - do taboru partii (uprzaz konska na wielbladzie = AccessViolation w AddMountMesh, Mends).</summary>
        private static string HarnessToTrain(Equipment eq, ItemObject mount, MobileParty party)
        {
            var harn = eq[EquipmentIndex.HorseHarness];
            if (harn.Item == null || HarnessFits(harn.Item, mount) || party == null || party.ItemRoster == null) return "";
            party.ItemRoster.AddToCounts(harn, 1);
            eq[EquipmentIndex.HorseHarness] = default(EquipmentElement);
            return " (uprzaz " + Id(harn.Item) + " do taboru)";
        }

        // ---------------------------------------------------------------- zmiana wierzchowca

        /// <summary>Wierzchowiec Daenerys wedlug etapu (jedyne miejsce zmiany slotu). why - start sesji / doba / po niewoli.</summary>
        internal static void DragonsApply(string why)
        {
            try { DragonsApplyCore(why); }
            catch (Exception e) { WStumble("DragonsApply(" + why + ")", e); }
        }

        private static void DragonsApplyCore(string why)
        {
            bool key = DragonsKey;
            if (!key && !_dMod) { _dState = "klucz NIE - smok jak w ROT"; return; }
            var dany = Dany();
            if (dany == null || !dany.IsAlive) { _dState = "Daenerys " + (dany == null ? "nieobecna" : "nie zyje") + " - bez zmian"; _dMod = false; return; }
            if (dany == Hero.MainHero) { _dState = "Daenerys to gracz - bez zmian"; _dMod = false; return; }
            if (IsWalkerHero(dany)) { _dState = "Daenerys jest Innym - bez zmian"; _dMod = false; return; }
            double sd = StoryDays();
            float next = -1f;
            int stage = key ? DragonStage(sd, out next) : DAdult;
            var want = StageDragon(stage, StolenDragons());
            var eq = dany.BattleEquipment;
            if (eq == null) { _dState = "brak zestawu bojowego"; return; }
            var cur = eq[EquipmentIndex.Horse];
            var curItem = cur.Item;
            bool curDragon = curItem != null && DragonFeed.IsDragon(curItem);
            var party = dany.PartyBelongedTo;
            bool leads = party != null && party.LeaderHero == dany && party.ItemRoster != null;
            string change = null, wait = null;
            if (want == null)
            {
                if (curDragon)
                {
                    bool found = false;
                    var horse = leads ? BestMount(party.ItemRoster, out found) : default(EquipmentElement);
                    if (found)
                    {
                        party.ItemRoster.AddToCounts(horse, -1);
                        eq[EquipmentIndex.Horse] = horse;
                        change = Id(curItem) + " -> " + Id(horse.Item) + " (kon z taboru)" + HarnessToTrain(eq, horse.Item, party);
                    }
                    else
                    {
                        eq[EquipmentIndex.Horse] = default(EquipmentElement);
                        change = Id(curItem) + " -> pusto (" + (leads ? "brak konia w taborze" : "Daenerys bez wlasnej partii") + " - pieszo)";
                    }
                }
                else if (curItem == null && key && leads)
                {
                    // pusty slot w etapie bez smoka - jej wlasny kon z taboru (i ROT nie da jej smoka przy wyjsciu z niewoli)
                    bool found;
                    var horse = BestMount(party.ItemRoster, out found);
                    if (found)
                    {
                        party.ItemRoster.AddToCounts(horse, -1);
                        eq[EquipmentIndex.Horse] = horse;
                        change = "pusto -> " + Id(horse.Item) + " (kon z taboru)" + HarnessToTrain(eq, horse.Item, party);
                    }
                }
            }
            else if (curItem != want)
            {
                var harn = eq[EquipmentIndex.HorseHarness];
                bool harnOut = harn.Item != null && !HarnessFits(harn.Item, want);
                bool horseOut = curItem != null && !curDragon;
                if ((horseOut || harnOut) && !leads) wait = "czeka: " + (horseOut ? "kon " + Id(curItem) : "uprzaz " + Id(harn.Item)) + " nie ma dokad wrocic (Daenerys bez wlasnej partii)";
                else
                {
                    if (horseOut) party.ItemRoster.AddToCounts(cur, 1);
                    if (harnOut) { party.ItemRoster.AddToCounts(harn, 1); eq[EquipmentIndex.HorseHarness] = default(EquipmentElement); }
                    eq[EquipmentIndex.Horse] = new EquipmentElement(want);
                    change = Id(curItem) + " -> " + Id(want) + (horseOut ? " (kon do taboru)" : "") + (harnOut ? " (uprzaz " + Id(harn.Item) + " do taboru)" : "");
                }
            }
            if (wait == null) _dMod = key && stage < DAdult;
            string stageTxt = DStageName[stage] + (key ? "" : " (klucz NIE - powrot smoka ROT)");
            var now = eq[EquipmentIndex.Horse].Item;
            _dState = "etap " + stageTxt + " | siodlo " + Id(now) + (wait != null ? " | " + wait : "")
                      + (next >= 0 ? " | nastepny etap od dnia opowiesci " + ((int)Math.Ceiling(next)).ToString(Inv) : "") + " (D=" + ((int)DanyInvasionDay()).ToString(Inv) + ")";
            if (change != null)
            {
                _dChangesDay++;
                _warsEventDay = true;
                Log.Info(WTag + "smoki Daenerys - " + why + ": etap " + stageTxt + " (dzien opowiesci " + sd.ToString("0.0", Inv) + ") | siodlo " + change + ".");
            }
            else if (wait != null && why != "doba") Log.Info(WTag + "smoki Daenerys - " + why + ": etap " + stageTxt + " | " + wait + ".");
            // komunikat dla gracza tylko przy zmianie etapu w trakcie gry (nie przy pierwszym rozpoznaniu w kampanii)
            if (key && _dStage >= 0 && stage != _dStage && stage > _dStage) PlayerDragonNews(stage);
            if (key) _dStage = stage;
        }

        private static void PlayerDragonNews(int stage)
        {
            string t = stage == DHatch ? "Word from the east: three dragons have hatched from Daenerys Targaryen's eggs in the ashes of Khal Drogo's pyre. They are still far too small to fight."
                     : stage == DYoung ? "Word from Slaver's Bay: Daenerys' dragons have grown big enough to fight, and the first of them now carries her into battle, though he is still young."
                     : stage == DGrown ? "Word from Slaver's Bay: Daenerys' dragons are half-grown now, bigger than any warhorse."
                     : stage == DAdult ? "Word from Slaver's Bay: Drogon is full-grown, and Daenerys rides him to war."
                     : null;
            if (t != null) Log.Player(t);
        }

        /// <summary>CampaignEvents.HeroPrisonerReleased: Daenerys wychodzi z niewoli - etap od razu (ROT daje smoka przy pustym slocie).</summary>
        internal static void DragonsOnReleased(Hero prisoner)
        {
            try
            {
                if (prisoner == null || prisoner.StringId != DanyHeroId) return;
                DragonsApply("po niewoli");
            }
            catch (Exception e) { WStumble("DragonsOnReleased", e); }
        }

        /// <summary>Opis (3) do linii startu sesji i doby.</summary>
        private static string DragonsText()
        {
            return "klucz " + (DragonsKey ? "TAK" : "NIE") + ", " + _dState + (_dChangesDay > 0 ? ", zmian dzis " + _dChangesDay : "");
        }
    }
}
