using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m117-ubranie - WLASNE UBRANIE GOLYCH (Jeff 24.10 ok. 23:05, po zrzucie bitwy pod Riverrun z polnagimi zolnierzami: "jak nie maja to wiadomo
    /// ze nikt nagi nie chodzi to niech dostana ubranie cywilne najtansze buty tunike ... chociaz niech maja danej kultury przecie nie chodza
    /// w gaciach"). Regula "tylko to, co ma" (sklad7b) zostawia slot pusty, gdy zbrojownia partii nie ma sztuki - pod Riverrun 19 ludzi Sylwy
    /// Paege bez pancerza tulowia (Mends "93 slotow PUSTYCH ... BodyArmor 19") = gole torsy. Teraz czlowiek objety ta regula, ktoremu zbrojownia
    /// nie dala tulowia albo nog, a wzorzec jednostki ma tam sztuke (Dothrakowie i dzicy z gola klata we wzorcu zostaja jak sa), idzie
    /// w NAJTANSZEJ cywilnej tunice / butach swojej kultury (GarrisonKit.CivilDress, z OwnKitSpawn - po wszystkich strazach).
    /// To jego ubranie z domu, POZA gospodarka zbrojowni: sztuka tymczasowa DTE (Assignment.MarkSlotAsTemporary) - spawn jej nie zdejmuje
    /// (CreateEquipmentForArmoryConsumption czysci sloty tymczasowe), zwrot po bitwie i lup z ciala jej nie oddaja (ProcessAgentEquipment-
    /// RespectingTemporarySlots), autorozstrzygniecie jej nie zna (bez spawnu). Jedyny kanal, ktory czyta sprzet agenta bez filtra tymczasowych -
    /// DTE AgentExtension.GetAgentArmors (czesc rozbita ciosem i "dodatkowo peknieta"; stad BattlefieldLaw.QueueWreck bierze wraki na ekran
    /// lupu) - dostaje liste bez ubrania (ArmorsPostfix); QueueWreck ma zapas (IsWorn). Bez tej latki nikogo nie ubieramy (ArmorsHooked).
    /// Wybor (leniwie, raz na kampanie; Reset): BodyArmor / LegArmor z ochrona w tym slocie, wartosc > 0, tier do 3 (Recipes.Grade - ubranie,
    /// nie zbroja: bez limitu zapas "kultura bez flagi" dawal skagosi_armor 915d i qarth_armor), w handlu, bez unikatow i klingi lore
    /// (ArmsPricing.IsUnique, UniqueGear; LegendaryLaw.IsLegend zna tylko bron), bez sprzetu olbrzymow i umarlych (UniqueLaw.IsDead), bez ubran kobiecych (NotUsableByMale albo nazwa - ROT tej flagi
    /// nie uzywa, a najtansze cywilne kilku kultur to suknie: andal_dress, rhoynar_dress, female_shoes). Kolejnosc: (a) kultura postaci
    /// z flaga Civilian, (b) kultura bez flagi, (c) neutralne z flaga, (d) dowolne z flaga; w kazdej najtansza (Value, potem mniej ochrony,
    /// potem id). Przy wystawieniu pierwsza, ktora czlowiek udzwignie (ItemReq.Meets - Atletyka; umiejetnosci sa swiete); zadna - slot pusty.
    /// </summary>
    internal static class Civvies
    {
        private static readonly ItemObject[] None = new ItemObject[0];
        // [0] tulow, [1] nogi; klucz - id kultury (bez neutralnej)
        private static readonly Dictionary<string, ItemObject[]>[] _civ = { new Dictionary<string, ItemObject[]>(), new Dictionary<string, ItemObject[]>() };
        private static readonly Dictionary<string, ItemObject[]>[] _plain = { new Dictionary<string, ItemObject[]>(), new Dictionary<string, ItemObject[]>() };
        private static readonly ItemObject[][] _neutral = { None, None }, _any = { None, None };
        private static bool _built;
        private static int _cutFemale, _cutTier, _cutBarred;

        // agent -> ubranie [0] tulow, [1] nogi (null - slot bez ubrania); wpis z GarrisonKit.RegisterPostfix, czyszczony na koniec misji
        private static readonly Dictionary<Agent, ItemObject[]> _agents = new Dictionary<Agent, ItemObject[]>();
        private static int _bCut, _bWreck, _bStumbles;

        /// <summary>Latka na DTE GetAgentArmors wpieta (GarrisonKit.ApplyAll) - bez niej nikogo nie ubieramy (ubranie trafialoby do wrakow).</summary>
        internal static bool ArmorsHooked;

        internal static void Reset()
        {
            for (int s = 0; s < 2; s++) { _civ[s].Clear(); _plain[s].Clear(); _neutral[s] = None; _any[s] = None; }
            _built = false; _cutFemale = _cutTier = _cutBarred = 0;
            ClearMission(); ClearCounters();
        }

        internal static void ClearMission() { _agents.Clear(); }

        internal static void ClearCounters() { _bCut = _bWreck = _bStumbles = 0; }

        private static void Stumble(string where, Exception e) { if (++_bStumbles <= 3) Log.Error("Civvies." + where, e); }

        private static int SlotIx(EquipmentIndex slot) { return slot == EquipmentIndex.Body ? 0 : slot == EquipmentIndex.Leg ? 1 : -1; }

        private static int Prot(ItemObject it, int s)
        {
            var ac = it != null ? it.ArmorComponent : null;
            return ac == null ? 0 : s == 0 ? ac.BodyArmor : ac.LegArmor;
        }

        /// <summary>Suknia i obuwie kobiece (ROT nie ma NotUsableByMale - heurystyka nazw, jak w rozpoznaniu m117).</summary>
        private static readonly string[] FemaleWords = { "dress", "female", "lady", "gown", "woman", "maiden", "wench", "bride" };

        private static bool Female(ItemObject it)
        {
            if ((it.ItemFlags & ItemFlags.NotUsableByMale) != 0) return true;
            string id = (it.StringId ?? "").ToLowerInvariant();
            string nm = it.Name != null ? it.Name.ToString().ToLowerInvariant() : "";
            foreach (var w in FemaleWords) if (id.Contains(w) || nm.Contains(w)) return true;
            return false;
        }

        /// <summary>Czy sztuka nadaje sie na ubranie dla slotu s (0 tulow, 1 nogi) - wspolne sito wszystkich poziomow wyboru.</summary>
        private static bool Fits(ItemObject it, int s)
        {
            if (it == null || it.StringId == null || it.Value <= 0 || it.NotMerchandise) return false;
            if (Prot(it, s) <= 0) return false;                                  // slot naprawde okrywa (baggy_trunks: b0)
            if (Recipes.Grade(it) > 3) { _cutTier++; return false; }               // ubranie, nie zbroja (pulapka Tier1 == 0 - Grade)
            if (ArmsPricing.IsUnique(it) || UniqueGear.Is(it) || GiantGear.Is(it) || UniqueLaw.IsDead(it)) { _cutBarred++; return false; }
            if (Female(it)) { _cutFemale++; return false; }
            return true;
        }

        private static ItemObject[] Sorted(List<ItemObject> l, int s)
        {
            l.Sort((a, b) =>
            {
                int c = a.Value.CompareTo(b.Value);
                if (c != 0) return c;
                c = Prot(a, s).CompareTo(Prot(b, s));
                return c != 0 ? c : string.CompareOrdinal(a.StringId, b.StringId);
            });
            return l.ToArray();
        }

        /// <summary>Listy wyboru - jeden przebieg po przedmiotach gry, raz na kampanie. Wywrotka: listy puste (nikogo nie ubieramy), linia bledu.</summary>
        private static void Build()
        {
            if (_built) return;
            _built = true;
            try
            {
                var civ = new[] { new Dictionary<string, List<ItemObject>>(), new Dictionary<string, List<ItemObject>>() };
                var plain = new[] { new Dictionary<string, List<ItemObject>>(), new Dictionary<string, List<ItemObject>>() };
                var neu = new[] { new List<ItemObject>(), new List<ItemObject>() };
                var any = new[] { new List<ItemObject>(), new List<ItemObject>() };
                int bad = 0;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null) continue;
                        int s = it.ItemType == ItemObject.ItemTypeEnum.BodyArmor ? 0 : it.ItemType == ItemObject.ItemTypeEnum.LegArmor ? 1 : -1;
                        if (s < 0 || !Fits(it, s)) continue;
                        string c = it.Culture != null ? it.Culture.StringId ?? "" : "";
                        bool neutral = c.Length == 0 || c == "neutral_culture";
                        bool isCiv = it.IsCivilian;
                        if (!neutral)
                        {
                            var d = isCiv ? civ[s] : plain[s];
                            List<ItemObject> l;
                            if (!d.TryGetValue(c, out l)) d[c] = l = new List<ItemObject>();
                            l.Add(it);
                        }
                        if (isCiv) { if (neutral) neu[s].Add(it); any[s].Add(it); }
                    }
                    catch { bad++; }
                }
                for (int s = 0; s < 2; s++)
                {
                    foreach (var kv in civ[s]) _civ[s][kv.Key] = Sorted(kv.Value, s);
                    foreach (var kv in plain[s]) _plain[s][kv.Key] = Sorted(kv.Value, s);
                    _neutral[s] = Sorted(neu[s], s);
                    _any[s] = Sorted(any[s], s);
                }
                StartLine(bad);
            }
            catch (Exception e) { Log.Error("Civvies.Build", e); }
        }

        /// <summary>Ubranie dla czlowieka i slotu (Body albo Leg): kultura -> zapasy, pierwsza, ktora udzwignie. skill - byly sztuki, ale zadna
        /// w jego Atletyce (slot zostaje pusty).</summary>
        internal static ItemObject Pick(CharacterObject co, EquipmentIndex slot, out bool skill)
        {
            skill = false;
            int s = SlotIx(slot);
            if (co == null || s < 0) return null;
            Build();
            string c = co.Culture != null ? co.Culture.StringId ?? "" : "";
            ItemObject[] l;
            ItemObject r;
            if (_civ[s].TryGetValue(c, out l) && (r = First(co, l, ref skill)) != null) return r;      // (a)
            if (_plain[s].TryGetValue(c, out l) && (r = First(co, l, ref skill)) != null) return r;    // (b)
            if ((r = First(co, _neutral[s], ref skill)) != null) return r;                              // (c)
            return First(co, _any[s], ref skill);                                                      // (d)
        }

        private static ItemObject First(CharacterObject co, ItemObject[] l, ref bool skill)
        {
            if (l == null) return null;
            bool female = co.IsFemale;
            foreach (var it in l)
            {
                if (female && (it.ItemFlags & ItemFlags.NotUsableByFemale) != 0) continue;
                if (!ItemReq.Meets(co, it)) { skill = true; continue; }
                return it;
            }
            return null;
        }

        /// <summary>Raz na kampanie (przy budowie list): co dostanie czlowiek kazdej kultury, przed wymogiem Atletyki - do sprawdzenia w logu.</summary>
        private static void StartLine(int bad)
        {
            var sb = new StringBuilder(4000);
            sb.Append("Ubranie cywilne (m117): wybor raz na kampanie (najtansze przed wymogiem Atletyki; a - kultura z flaga Civilian, b - kultura bez flagi, c - neutralne, d - dowolne) -");
            int n = 0;
            try
            {
                foreach (var cu in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                {
                    if (cu == null || cu.StringId == null || cu.StringId == "neutral_culture") continue;
                    sb.Append(n++ == 0 ? " " : ", ").Append(cu.StringId).Append(' ').Append(Top(cu.StringId, 0)).Append(" / ").Append(Top(cu.StringId, 1));
                }
            }
            catch (Exception e) { sb.Append(" [kultury: ").Append(e.GetType().Name).Append(']'); }
            sb.Append("; w puli: tulow cywilne ").Append(_any[0].Length).Append(" (neutralne ").Append(_neutral[0].Length).Append("), nogi cywilne ").Append(_any[1].Length)
              .Append(" (neutralne ").Append(_neutral[1].Length).Append("); odrzucone: kobiece ").Append(_cutFemale).Append(", ponad tier 3 ").Append(_cutTier)
              .Append(", unikaty/olbrzymy/umarli ").Append(_cutBarred).Append(bad > 0 ? ", bledy odczytu " + bad : "").Append('.');
            Log.Info(sb.ToString());
        }

        private static string Top(string c, int s)
        {
            ItemObject[] l;
            if (_civ[s].TryGetValue(c, out l) && l.Length > 0) return Short(l[0], "a");
            if (_plain[s].TryGetValue(c, out l) && l.Length > 0) return Short(l[0], "b");
            if (_neutral[s].Length > 0) return Short(_neutral[s][0], "c");
            return _any[s].Length > 0 ? Short(_any[s][0], "d") : "BRAK";
        }

        private static string Short(ItemObject it, string lvl) { return it.StringId + " " + it.Value + "d " + lvl; }

        // ------------------------------------------------------------ rejestr agentow w misji

        internal static void NoteAgent(Agent a, ItemObject[] worn) { if (a != null && worn != null) _agents[a] = worn; }

        /// <summary>Czy ta sztuka to wlasne ubranie tego agenta (m117) - zapas w BattlefieldLaw.QueueWreck.</summary>
        internal static bool IsWorn(Agent a, ItemObject it)
        {
            if (a == null || it == null || _agents.Count == 0) return false;
            ItemObject[] w;
            return _agents.TryGetValue(a, out w) && w != null && (ReferenceEquals(w[0], it) || ReferenceEquals(w[1], it));
        }

        /// <summary>QueueWreck zatrzymal wrak z ubrania (latka GetAgentArmors nie zadzialala dla tego agenta) - licznik linii bitwy.</summary>
        internal static void NoteWreckStopped() { _bWreck++; }

        /// <summary>Postfiks DTE AgentExtension.GetAgentArmors(Agent) - jedyne wywolanie: DynamicTroopMissionLogic.OnAgentRemoved (czesc rozbita
        /// ciosem i "dodatkowo peknieta" u wroga tieru 3+). Lista bez wlasnego ubrania agenta: ubranie nie zabiera trafienia prawdziwej sztuce
        /// (DTE liczylby ja wtedy do lupu zamiast do rozbitych) i nie idzie do wrakow BattlefieldLaw (mennica). Lista jest swieza (DTE tworzy ja
        /// przy kazdym wywolaniu), wiec usuwamy w miejscu.</summary>
        public static void ArmorsPostfix(Agent __0, List<ItemObject> __result)
        {
            if (__0 == null || __result == null || __result.Count == 0 || _agents.Count == 0) return;
            try
            {
                ItemObject[] w;
                if (!_agents.TryGetValue(__0, out w) || w == null) return;
                for (int k = 0; k < w.Length; k++)
                    if (w[k] != null && __result.Remove(w[k])) _bCut++;
            }
            catch (Exception e) { Stumble("ArmorsPostfix", e); }
        }

        /// <summary>Dopisek do linii bitwy GarrisonKit: ile razy lista zbroi DTE byla bez ubrania, ile wrakow zatrzymal zapas, potkniecia.</summary>
        internal static string BattleNote()
        {
            return "z listy zbroi DTE (rozbite ciosem) wyjete " + _bCut + ", wrak z ubrania zatrzymany (zapas) " + _bWreck + ", potkniecia listy " + _bStumbles;
        }
    }
}
