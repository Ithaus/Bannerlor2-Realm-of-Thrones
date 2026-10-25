using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// Paczka 133c (Jeff 25.10 ok. 12:57: "w widoku armii tez ma pokazywac: jakiego sprzetu brakuje zolnierzom, bo teraz ja nie wiem ... jesli sprzet jest to
    /// komunikat czemu nie zalozy bo np. za maly skill, de facto przeniesmy logi do zakladki armii"; projekt + krytyka K1-K16 w raport-armii-133.txt).
    /// PODGLAD NASTEPNEJ BITWY - JEDNA ZASADA (decyzja (a)): liczby sprzetu w oknie "Review the army" licza TEN SAM rozdzielacz DTE co bitwa
    /// (PartyEquipmentDistributor.DoAssignAsync w trybie podgladu - prywatny konstruktor, _isReadinessPreview = true, jak pasek DTE) z flagami bitwy
    /// (dodatki DTE tak, zestaw awaryjny = !TroopsFightWithOwnKitOnly, kara Underequipped tak, bez losowej broni bezbronnych) i ze wszystkimi naszymi strazami
    /// (CrashScribe UniqueWard, SuitableWard, SkillLawWard, RealmWard, CamelCulling; Armoury SkillsDecide, GearFit.FloorPostfix) - nie papier kwatermistrza.
    /// Dalej to, co bitwa robi przy wystawieniu: DragonUnmount.WouldStrip (K3 - ta sama funkcja), straz uprzezy (GarrisonKit.SaddleMissing), jezdziec bez
    /// konia (RiderOnFoot), puste sloty GarrisonKit.Missing i rozbicie KitGaps.Compare (te same co linia bitwy i "Last battle").
    /// Przydzial: tylko zdrowi (jak RunAsync), wzorzec BattleEquipments[i % n] (jak pasek DTE; w bitwie RandomBattleEquipment), kolejnosc jak w bitwie
    /// (Sort((x, y) => y.CompareTo(x))). Pula: trzy migawki (K4) - A zbrojownia po normalizacji DTE, B pula po AddEquipmentToAssign (A - B: czarna lista DTE),
    /// C pula po przebiegu (B bez klucza w C: UniqueWard; kon i uprzaz - B minus przydzielone, bo DTE nie zdejmuje ich z puli).
    /// Przyczyny braku (katalog projektu 2.1, w kolejnosci potoku) liczone tymi samymi predykatami co przydzial: zakazane (BarredInBattle, legendy, olbrzymy),
    /// umiejetnosc (ItemReq - tekst "Requires Bow 175 - this troop has 80."), siodlo (MenUpgrade.MountOk), kultura (GearFit.CultureCode), klasa broni (DTE
    /// Global.HaveSameWeaponClass wobec sztuki wzorca), za malo, dodatki u innych, brak; uprzaz bez wymogu Jazdy (K5 - w bitwie nikt jej nie sprawdza);
    /// porada z sakw tymi samymi predykatami (K6). Kara morale: Assignment.UnderEquippedMoralePenalty (K7 - napis "worse than the kit their skill and rank
    /// call for"). Pamiec po wersjach zbrojowni, skladu i sakw oraz dniu - bez godziny (K7).
    /// K15: ten sam podglad na starcie misji (GarrisonKit.DistributorsPostfix, bitwa w polu) z wzorcami i kolejnoscia przydzialow bitwy - na koniec misji
    /// BattleLine porownuje go z OwnKitSpawn (pierwsi N kazdego typu - tak wystawia DTE). Czas w ms w linii (prog 50 ms - decyzja o kluczu MCM po tescie).
    /// Tylko odczyt: osobny rozdzielacz i osobna pula (zadnej sztuki ani zlota), GearFit.Quiet na czas przebiegu, CrashScribe milczy (Mends.IsPreview, 133a).
    /// Potkniecia liczone per miejsce, bez wylacznika. Stan: ostatni podglad okna i podglad misji - Reset (ArmyReview.Reset), ClearMission (GarrisonKit.ClearBattle).
    /// </summary>
    internal static class KitPreview
    {
        private const string P = "Sprzet ludzi (133c): ";

        // przyczyny braku (kod, kolejnosc potoku); 0 - sztuka pasuje
        internal const int RBarred = 1, RLegend = 2, RGiant = 3, RSkill = 4, RSaddle = 5, RTier = 6, RCulture = 7, RClass = 8, RFew = 9, RExtra = 10, RNone = 11,
                           RFits = 12, RHarness = 13, RNoKit = 14, RSpare = 15, RCodes = 16;
        private static readonly string[] CodePl = { "-", "zakazane", "legenda", "olbrzymie", "umiejetnosc", "siodlo", "tier", "kultura", "klasa broni", "za malo",
                                                    "dodatki u innych", "brak", "pasuje - nie wydane", "uprzaz", "poza wzorcami", "zapas" };

        // ---------------------------------------------------------------- model
        /// <summary>Jeden czlowiek (K15 - porownanie z bitwa): puste sloty (Missing), braki wedlug rodzaju, pieszo.</summary>
        internal sealed class Man { internal int Missing; internal int[] Short; internal bool Foot, NoSad; }

        internal sealed class Reason { internal int Code; internal string Text; internal int Count; internal ItemObject Sample; internal int SampleN; }

        internal sealed class TypeRow
        {
            internal CharacterObject Troop; internal string Id, Name;
            internal int Men, Wounded, Missing, Armour, Weapons, Extra, Replaced, Riders, NoHorse, NoSaddle, Bare, MoraleMen, Stripped;
            internal float MoraleSum;
            internal readonly int[] Short = new int[KitGaps.Cats], Need = new int[KitGaps.Cats], Bag = new int[KitGaps.Cats];
            internal readonly int[] StripWhy = new int[6];   // kody DragonUnmount.WouldStrip > 0
            internal readonly Dictionary<int, Dictionary<ItemObject, int>> Lost = new Dictionary<int, Dictionary<ItemObject, int>>();   // rodzaj -> sztuki wzorca bez pary
            internal readonly Dictionary<ItemObject, int> Kit = new Dictionary<ItemObject, int>();   // sztuki wzorcow (sloty 0-3, 5-11) -> ilu ludzi
            internal readonly List<Reason>[] Why = new List<Reason>[KitGaps.Cats];
            internal readonly List<Reason> HorseWhy = new List<Reason>(), SaddleWhy = new List<Reason>();
            internal bool Mounted { get { return Riders > 0 && Riders * 2 >= Men; } }
            internal int Foot { get { return NoHorse + NoSaddle; } }
        }

        internal sealed class Left { internal ItemObject Item; internal int Count; internal int Code; internal string Text; }

        internal sealed class Result
        {
            internal bool Ok; internal string Why;   // Ok false - Why: zdanie po angielsku (wiersz okna wyszarzony)
            internal string Key; internal bool InMission;
            internal int Men, Wounded, Missing, Replaced, Extra, Riders, NoHorse, NoSaddle, Bare, Stripped, MoraleMen, Bad, Drift;
            internal float MoraleSum; internal long Ms;
            internal readonly int[] Short = new int[KitGaps.Cats], Need = new int[KitGaps.Cats];
            internal readonly List<TypeRow> Rows = new List<TypeRow>();
            internal readonly Dictionary<ItemObject, int> A = new Dictionary<ItemObject, int>(), Given = new Dictionary<ItemObject, int>(), Rem = new Dictionary<ItemObject, int>(),
                                                          Black = new Dictionary<ItemObject, int>(), Barred = new Dictionary<ItemObject, int>(), Spares = new Dictionary<ItemObject, int>();
            internal readonly int[] OffWhy = new int[6];   // zdjete przy wystawieniu wedlug kodu WouldStrip
            internal readonly int[] WhyCount = new int[RCodes];
            // 133d: wierzchowce i uprzaz na nich PO strazach wystawienia (WouldStrip, SaddleGuard, RiderOnFoot) - "w uzyciu w bitwie" strony "Horses and harness"
            internal readonly Dictionary<ItemObject, int> Rode = new Dictionary<ItemObject, int>(), Saddles = new Dictionary<ItemObject, int>();
            internal Mounts MountsCache;                // 133d: potrzeby koni i uprzezy (MountsOf - leniwie, sakwy w kluczu pamieci)
            internal int BagPieces;
            internal List<Left> LeftList;               // resztka z przyczynami - leniwie (strona "Unused in the troops' armoury")
            internal Dictionary<string, List<Man>> PerMan;   // K15: wedlug typu, w kolejnosci przydzialow (tak wystawia DTE)
            internal List<object> Asg;                       // K15: przydzialy podgladu w misji (porownanie z bitwa), potem null
            internal int LeftPieces { get { int n = 0; foreach (var v in Rem.Values) n += v; return n; } }
            internal int Foot { get { return NoHorse + NoSaddle; } }
        }

        // ---------------------------------------------------------------- stan
        private static Result _last;                      // ostatni podglad okna (pamiec po kluczu - K7)
        private static Result _mission;                   // K15: podglad tej misji
        private static string _missionNote;
        private static int _mMen, _mDiffMen, _mDiffSlots;
        private static int _stumbles;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Nowa kampania i kazde wczytanie (ArmyReview.Reset).</summary>
        internal static void Reset() { _last = null; ClearMission(); _stumbles = 0; _stumbleAt.Clear(); }

        /// <summary>Nowa bitwa i koniec misji (GarrisonKit.ClearBattle).</summary>
        internal static void ClearMission() { _mission = null; _missionNote = null; _mMen = _mDiffMen = _mDiffSlots = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            int n; _stumbleAt.TryGetValue(where, out n); _stumbleAt[where] = ++n;
            if (n <= 3 || n % 50 == 0) { try { Log.Error("KitPreview." + where + " (potkniecie " + n + ")", e); } catch { } }
        }

        // ---------------------------------------------------------------- wpiecia (refleksja raz; pulapka 15.10 - Declared*)
        private static bool _looked, _wired;
        private static string _wiring = "nie sprawdzane";
        private static ConstructorInfo _ctorDist, _ctorAsg;
        private static MethodInfo _add, _run, _norm, _getRef, _getChar, _getPenalty, _isTemp, _getWc, _sameWc;
        private static FieldInfo _fPool, _fAssigns, _fEq;

        /// <summary>Stan wpiec do linii startu sesji (ArmyReview.AddMenus).</summary>
        internal static string Wiring { get { Wire(); return _wiring; } }

        private static bool Wire()
        {
            if (_looked) return _wired;
            _looked = true;
            try
            {
                var tDist = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.PartyEquipmentDistributor");
                var tAsg = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.Assignment");
                var tArm = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.ArmyArmory");
                var tGlob = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.Global");
                if (tDist == null || tAsg == null || tArm == null) { _wiring = "BRAK (DTE nieobecne)"; return false; }
                _ctorDist = AccessTools.DeclaredConstructor(tDist, new[] { typeof(MobileParty), typeof(ItemRoster), typeof(bool) });
                _ctorAsg = AccessTools.DeclaredConstructor(tAsg, new[] { typeof(CharacterObject), typeof(Equipment), typeof(bool) });
                _add = AccessTools.DeclaredMethod(tDist, "AddEquipmentToAssign", new[] { typeof(EquipmentElement), typeof(int) });
                _run = AccessTools.DeclaredMethod(tDist, "DoAssignAsync", new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                _norm = AccessTools.DeclaredMethod(tArm, "TryNormalizeArmoryElement", new[] { typeof(EquipmentElement), typeof(EquipmentElement).MakeByRefType() });
                _fPool = AccessTools.DeclaredField(tDist, "_equipmentToAssign");
                _fAssigns = AccessTools.DeclaredField(tDist, "Assignments");
                _fEq = AccessTools.DeclaredField(tAsg, "Equipment");
                _getRef = AccessTools.DeclaredPropertyGetter(tAsg, "ReferenceEquipment");
                _getChar = AccessTools.DeclaredPropertyGetter(tAsg, "Character");
                // nieobowiazkowe: kara morale, sloty tymczasowe (zestaw awaryjny), klasa broni DTE (bez niej - grupa typ + klasa jak Mends.WardGroup)
                _getPenalty = AccessTools.DeclaredPropertyGetter(tAsg, "UnderEquippedMoralePenalty");
                if (_getPenalty != null && _getPenalty.ReturnType != typeof(float)) _getPenalty = null;
                _isTemp = AccessTools.DeclaredMethod(tAsg, "IsTemporarySlot", new[] { typeof(EquipmentIndex), typeof(ItemObject) });
                if (tGlob != null)
                {
                    _getWc = AccessTools.DeclaredMethod(tGlob, "GetWeaponClass", new[] { typeof(ItemObject) });
                    _sameWc = AccessTools.DeclaredMethod(tGlob, "HaveSameWeaponClass", new[] { typeof(List<WeaponClass>), typeof(List<WeaponClass>) });
                    if (_getWc == null || _sameWc == null || _getWc.ReturnType != typeof(List<WeaponClass>)) { _getWc = null; _sameWc = null; }
                }
                var need = new KeyValuePair<string, object>[]
                {
                    Kv("konstruktor podgladu", _ctorDist), Kv("Assignment(3)", _ctorAsg), Kv("AddEquipmentToAssign", _add), Kv("DoAssignAsync", _run),
                    Kv("TryNormalizeArmoryElement", _norm), Kv("_equipmentToAssign", _fPool), Kv("Assignments", _fAssigns), Kv("Equipment", _fEq),
                    Kv("ReferenceEquipment", _getRef), Kv("Character", _getChar)
                };
                var miss = new List<string>();
                foreach (var kv in need) if (kv.Value == null) miss.Add(kv.Key);
                _wired = miss.Count == 0;
                _wiring = "wpiecia " + (need.Length - miss.Count) + "/" + need.Length + (miss.Count > 0 ? ", BRAK " + string.Join(", ", miss.ToArray()) : "")
                          + "; kara morale " + (_getPenalty != null ? "TAK" : "BRAK") + ", sloty tymczasowe " + (_isTemp != null ? "TAK" : "BRAK")
                          + ", klasa broni DTE " + (_sameWc != null ? "TAK" : "BRAK (grupa typ + klasa)");
                return _wired;
            }
            catch (Exception e) { _wiring = "BRAK (" + e.GetType().Name + ")"; Stumble("Wire", e); return false; }
        }

        private static KeyValuePair<string, object> Kv(string k, object v) { return new KeyValuePair<string, object>(k, v); }

        // ---------------------------------------------------------------- wejscia
        /// <summary>Podglad okna (pamiec po kluczu bez godziny - K7). force - liczyc od nowa. Nigdy null.</summary>
        internal static Result Get(bool force)
        {
            var mp = MobileParty.MainParty;
            string why = Gate(mp);
            if (why != null) return new Result { Why = why };
            string key = Key(mp);
            if (!force && _last != null && _last.Key == key) return _last;
            Result r;
            try { r = Compute(mp, null, true, true); r.Ok = r.Why == null; }
            catch (Exception e) { Stumble("Compute", e); r = new Result { Why = "The hand-out could not be worked out now - the log has the details." }; }
            r.Key = key;
            _last = r;   // takze nieudany - bez powtarzania przy kazdym powrocie z podstron
            return r;
        }

        private static string Gate(MobileParty mp)
        {
            if (Campaign.Current == null || mp == null || mp.MemberRoster == null) return "No campaign is running.";
            if (!GarrisonKit.OwnKitOn) return "Troops Fight With Own Kit Only is off - your men fight in their troop's full kit, so no slot goes empty.";
            if (!Wire()) return "Dynamic Troop Equipment's hand-out cannot be reached in this game - nothing to work out.";
            if (QuartermasterEscrow.Active) return "The troops' armoury is open - look again once it is closed.";
            if (Mission.Current != null) return "Not during a battle.";
            if (QuartermasterLaw.DteArmory() == null) return "The troops' armoury (Dynamic Troop Equipment) cannot be read now.";
            return null;
        }

        private static string Key(MobileParty mp)
        {
            var arm = QuartermasterLaw.DteArmory();
            var bag = mp.ItemRoster;
            return (arm != null ? arm.VersionNo : -1) + "|" + mp.MemberRoster.VersionNo + "|" + (bag != null ? bag.VersionNo : -1) + "|" + (int)CampaignTime.Now.ToDays
                   + "|" + (SaddleLaw.On ? 1 : 0) + (GearFit.CultureOn ? 1 : 0) + (GearFit.FloorOn ? 1 : 0) + (GearFit.FloorOverSkillOn ? 1 : 0) + (GearFit.FloorForeignOn ? 1 : 0);
        }

        /// <summary>Typ z podgladu po StringId (null - brak).</summary>
        internal static TypeRow Find(Result r, string id)
        {
            if (r == null || id == null) return null;
            foreach (var t in r.Rows) if (t.Id == id) return t;
            return null;
        }

        // ---------------------------------------------------------------- liczenie
        private static Dictionary<ItemObject, List<WeaponClass>> _wc;
        private static Dictionary<ItemObject, Dictionary<ItemObject, bool>> _same;

        /// <summary>Jeden przebieg rozdzielacza DTE w trybie podgladu. mirror == null - mapa (sklad partii, wzorce BattleEquipments, sortowanie jak w bitwie);
        /// mirror - przydzialy bitwy tej misji (K15: te same wzorce i ta sama kolejnosc). Wyjatek wychodzi do wolajacego (licznik potkniec tam).</summary>
        private static Result Compute(MobileParty mp, IList mirror, bool canMount, bool bag)
        {
            var res = new Result { InMission = mirror != null };
            var sw = Stopwatch.StartNew();
            _wc = new Dictionary<ItemObject, List<WeaponClass>>();
            _same = new Dictionary<ItemObject, Dictionary<ItemObject, bool>>();
            var arm = QuartermasterLaw.DteArmory();
            if (arm == null) { res.Why = "The troops' armoury (Dynamic Troop Equipment) cannot be read now."; return res; }
            // migawka A: zbrojownia po normalizacji DTE (jak pasek DTE MeasureMainPartyArmoryReadiness i RunAsync bitwy)
            var snap = new ItemRoster();
            for (int i = 0; i < arm.Count; i++)
            {
                var el = arm.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                var args = new object[] { el.EquipmentElement, null };
                bool ok;
                try { ok = (bool)_norm.Invoke(null, args); } catch { ok = false; }
                if (!ok || !(args[1] is EquipmentElement)) continue;
                var ne = (EquipmentElement)args[1];
                if (ne.Item == null) continue;
                snap.AddToCounts(ne, el.Amount);
                Add(res.A, ne.Item, el.Amount);
            }
            var dist = _ctorDist.Invoke(new object[] { mp, snap, canMount });
            var list = _fAssigns.GetValue(dist) as IList;
            if (list == null) { res.Why = "Dynamic Troop Equipment's hand-out cannot be reached in this game - nothing to work out."; return res; }
            var made = new List<object>();
            if (mirror == null)
            {
                var r = mp.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    int w = Math.Max(0, Math.Min(el.Number, el.WoundedNumber));
                    res.Wounded += w;
                    int fit = el.Number - w;   // tylko zdrowi - jak RunAsync bitwy (pasek DTE liczy tez rannych)
                    var sets = new List<Equipment>();
                    foreach (var e in ch.BattleEquipments) if (e != null && e.IsBattle && !e.IsEmpty()) sets.Add(e);
                    if (sets.Count == 0) continue;
                    for (int k = 0; k < fit; k++) made.Add(_ctorAsg.Invoke(new object[] { ch, sets[k % sets.Count], canMount }));
                    if (w > 0) RowOf(res, ch).Wounded += w;
                }
                made.Sort((x, y) => ((IComparable)y).CompareTo(x));   // kolejnosc rozdzielania jak w bitwie
            }
            else
            {
                foreach (var ba in mirror)
                {
                    if (ba == null) continue;
                    var ch = _getChar.Invoke(ba, null) as CharacterObject;
                    var bref = _getRef.Invoke(ba, null) as Equipment;
                    if (ch == null || bref == null) continue;
                    var a = _ctorAsg.Invoke(new object[] { ch, bref, canMount });
                    // K15: wzorzec bitwy 1:1 (postfiks SkillsDecide na konstruktorze przestawia kopie i losuje pancerz)
                    var pref = _getRef.Invoke(a, null) as Equipment;
                    if (pref != null) for (int s = 0; s < 12; s++) pref[(EquipmentIndex)s] = bref[(EquipmentIndex)s];
                    made.Add(a);
                }
                res.Asg = made;
            }
            foreach (var a in made) list.Add(a);
            for (int i = 0; i < snap.Count; i++)
            {
                var el = snap.GetElementCopyAtIndex(i);
                if (el.Amount > 0) _add.Invoke(dist, new object[] { el.EquipmentElement, el.Amount });
            }
            var pool = _fPool.GetValue(dist) as IDictionary;
            // migawka B: pula po AddEquipmentToAssign (normalizacja i czarna lista DTE ItemBlackList.Test); UniqueWard dziala dopiero w przebiegu (prefiks)
            var B = ReadPool(pool, null);
            Dictionary<Equipment, int> kept;
            bool q = GearFit.Quiet;
            GearFit.Quiet = true;   // odmowy kultury z podgladu nie ida do linii doby GearFit
            kept = GearFit.PreviewKeptBegin();
            try { _run.Invoke(dist, new object[] { true, !GarrisonKit.OwnKitOn, true, false }); }
            finally { GearFit.Quiet = q; GearFit.PreviewKeptEnd(); }
            // migawka C: pula po przebiegu (zwroty strazy na polke i pobrania wliczone)
            var inC = new HashSet<ItemObject>();
            var C = ReadPool(pool, inC);

            // przydzielone przed wystawieniem (bez slotow tymczasowych - zestaw awaryjny DTE nie jest z polki)
            foreach (var a in made)
            {
                var eq = _fEq.GetValue(a) as Equipment;
                if (eq == null) continue;
                for (int s = 0; s < 12; s++)
                {
                    if (s == 4) continue;
                    var it = eq[(EquipmentIndex)s].Item;
                    if (it == null || Temp(a, s, it)) continue;
                    Add(res.Given, it, 1);
                }
            }
            // resztka zbrojowni po rozdaniu
            foreach (var kv in B)
            {
                var it = kv.Key;
                int g; res.Given.TryGetValue(it, out g);
                if (IsMountKit(it)) { if (kv.Value - g > 0) res.Rem[it] = kv.Value - g; continue; }   // kon i uprzaz: DTE nie zdejmuje ich z puli
                if (!inC.Contains(it)) { Add(res.Barred, it, kv.Value); continue; }                  // UniqueWard: unikaty, lore, umarli, smoki
                int c; C.TryGetValue(it, out c);
                if (c > 0) res.Rem[it] = c;
                if (c + g != kv.Value) res.Drift++;                                                  // ksiegowosc puli (tylko log)
            }
            foreach (var kv in C) if (kv.Value > 0 && !B.ContainsKey(kv.Key) && !IsMountKit(kv.Key)) { res.Rem[kv.Key] = kv.Value; res.Drift++; }
            foreach (var kv in res.A) { int b; B.TryGetValue(kv.Key, out b); if (kv.Value > b) Add(res.Black, kv.Key, kv.Value - b); }

            // czlowiek po czlowieku: straze przy wystawieniu, potem puste sloty
            if (mirror != null) res.PerMan = new Dictionary<string, List<Man>>(StringComparer.Ordinal);
            foreach (var a in made)
            {
                var ch = _getChar.Invoke(a, null) as CharacterObject;
                var eq = _fEq.GetValue(a) as Equipment;
                var rf = _getRef.Invoke(a, null) as Equipment;
                if (ch == null || ch.IsHero || eq == null || rf == null) continue;
                var row = RowOf(res, ch);
                var e2 = eq.Clone(false);
                // K3: straz przy wystawieniu - DragonUnmount.WouldStrip, ta sama decyzja co w bitwie
                for (int s = 0; s <= 10; s++)
                {
                    if (s == 4) continue;
                    var it = e2[(EquipmentIndex)s].Item;
                    if (it == null) continue;
                    ItemObject repl; string why;
                    int code = DragonUnmount.WouldStrip(ch, s, it, GearFit.PreviewKept(kept, eq, s), out repl, out why);
                    if (code <= 0) continue;
                    // zamiennik jest z niczego - straz "nic z niczego" zostawia go tylko z pokryciem w zbrojowni (przyblizenie: resztka po rozdaniu)
                    bool cov = repl != null && Take(res.Rem, repl);
                    e2[(EquipmentIndex)s] = cov ? new EquipmentElement(repl) : default(EquipmentElement);
                    if (s == 10 && !cov) e2[EquipmentIndex.HorseHarness] = default(EquipmentElement);
                    row.Stripped++;
                    if (code < row.StripWhy.Length) { row.StripWhy[code]++; res.OffWhy[code]++; }
                }
                // E8/H: kon bez uprzezy swojej rodziny schodzi (SaddleGuard), potem jezdziec bez konia (RiderOnFoot) - jak OwnKitSpawn
                bool noSad = GarrisonKit.SaddleMissing(a, e2, canMount);
                if (noSad) { e2[EquipmentIndex.Horse] = default(EquipmentElement); e2[EquipmentIndex.HorseHarness] = default(EquipmentElement); }
                bool foot = !noSad && GarrisonKit.RiderOnFoot(a, e2, rf, canMount);
                // 133d: kon i uprzaz, na ktorych czlowiek wyjedzie (po strazach; zestaw awaryjny DTE nie jest z polki) - tylko odczyt
                var mh = e2[EquipmentIndex.Horse].Item;
                if (canMount && mh != null && !Temp(a, 10, mh))
                {
                    Add(res.Rode, mh, 1);
                    var mhr = e2[EquipmentIndex.HorseHarness].Item;
                    if (mhr != null && !Temp(a, 11, mhr)) Add(res.Saddles, mhr, 1);
                }
                var g = KitGaps.Compare(rf, e2, true);
                int miss = GarrisonKit.Missing(a, e2);
                if (g.Missing != miss) res.Bad++;
                row.Men++; row.Missing += Math.Max(0, miss);
                row.Armour += g.Armour; row.Weapons += g.Weapons; row.Extra += g.Extra; row.Replaced += g.Replaced;
                for (int c = 0; c < KitGaps.Cats; c++) { row.Short[c] += g.Short[c]; row.Need[c] += g.Need[c]; }
                foreach (var kv in g.Lost)
                {
                    Dictionary<ItemObject, int> d;
                    if (!row.Lost.TryGetValue(kv.Key, out d)) { d = new Dictionary<ItemObject, int>(); row.Lost[kv.Key] = d; }
                    Add(d, kv.Value, 1);
                }
                foreach (var sp in g.Spare) Add(res.Spares, sp, 1);
                if (!GearFit.Armed(e2)) row.Bare++;
                var rh = rf[EquipmentIndex.Horse];
                if (canMount && !rh.IsEmpty && rh.Item != null) row.Riders++;
                if (foot) row.NoHorse++;
                if (noSad) row.NoSaddle++;
                if (_getPenalty != null)
                {
                    float pen = 0f;
                    try { pen = (float)_getPenalty.Invoke(a, null); } catch { pen = 0f; }
                    if (pen > 0f) { row.MoraleMen++; row.MoraleSum += pen; }
                }
                for (int s = 0; s < 12; s++) { if (s == 4) continue; var ri = rf[(EquipmentIndex)s].Item; if (ri != null) Add(row.Kit, ri, 1); }
                if (res.PerMan != null)
                {
                    List<Man> l;
                    if (!res.PerMan.TryGetValue(row.Id, out l)) { l = new List<Man>(); res.PerMan[row.Id] = l; }
                    l.Add(new Man { Missing = Math.Max(0, miss), Short = (int[])g.Short.Clone(), Foot = foot, NoSad = noSad });
                }
            }
            // sumy
            foreach (var t in res.Rows)
            {
                res.Men += t.Men; res.Missing += t.Missing; res.Replaced += t.Replaced; res.Extra += t.Extra - t.Replaced; res.Riders += t.Riders;
                res.NoHorse += t.NoHorse; res.NoSaddle += t.NoSaddle; res.Bare += t.Bare; res.Stripped += t.Stripped; res.MoraleMen += t.MoraleMen; res.MoraleSum += t.MoraleSum;
                for (int c = 0; c < KitGaps.Cats; c++) { res.Short[c] += t.Short[c]; res.Need[c] += t.Need[c]; }
            }
            res.Rows.RemoveAll(t => t.Men <= 0 && t.Wounded <= 0);
            res.Rows.Sort(ByGaps);
            if (mirror == null)
            {
                foreach (var t in res.Rows) Explain(res, t);
                if (bag) Baggage(res, mp);
            }
            res.Ms = sw.ElapsedMilliseconds;
            return res;
        }

        private static TypeRow RowOf(Result res, CharacterObject ch)
        {
            string id = ch.StringId ?? "?";
            foreach (var t in res.Rows) if (t.Id == id) return t;
            var row = new TypeRow { Troop = ch, Id = id, Name = ch.Name != null ? ch.Name.ToString() : id };
            res.Rows.Add(row);
            return row;
        }

        private static int ByGaps(TypeRow a, TypeRow b)
        {
            int c = b.Missing.CompareTo(a.Missing);
            if (c != 0) return c;
            c = b.Foot.CompareTo(a.Foot);
            if (c != 0) return c;
            c = b.Men.CompareTo(a.Men);
            return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
        }

        private static Dictionary<ItemObject, int> ReadPool(IDictionary pool, HashSet<ItemObject> present)
        {
            var d = new Dictionary<ItemObject, int>();
            if (pool == null) return d;
            foreach (DictionaryEntry kv in pool)
            {
                if (!(kv.Key is EquipmentElement)) continue;
                var it = ((EquipmentElement)kv.Key).Item;
                if (it == null) continue;
                if (present != null) present.Add(it);
                int n; try { n = Convert.ToInt32(kv.Value); } catch { continue; }
                if (n > 0) Add(d, it, n);
            }
            return d;
        }

        private static bool Temp(object a, int s, ItemObject it)
        {
            if (_isTemp == null) return false;
            try { return (bool)_isTemp.Invoke(a, new object[] { (EquipmentIndex)s, it }); } catch { return false; }
        }

        private static void Add(Dictionary<ItemObject, int> d, ItemObject it, int n) { int v; d.TryGetValue(it, out v); d[it] = v + n; }

        private static bool Take(Dictionary<ItemObject, int> d, ItemObject it)
        {
            int v;
            if (!d.TryGetValue(it, out v) || v <= 0) return false;
            if (v == 1) d.Remove(it); else d[it] = v - 1;
            return true;
        }

        private static bool IsMountKit(ItemObject it) { return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Horse || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness); }
        private static bool IsDragon(ItemObject it) { return it != null && it.StringId != null && it.StringId.StartsWith("dragon_", StringComparison.Ordinal); }

        // ---------------------------------------------------------------- predykaty (te same co przydzial)
        /// <summary>Ten sam rodzaj co sztuka wzorca R - jak DTE: bron - Global.HaveSameWeaponClass (IsWeaponSuitable), pancerz, kon i uprzaz - ten sam typ.</summary>
        private static bool SameKind(ItemObject r, ItemObject R)
        {
            if (r == null || R == null) return false;
            if (IsMountKit(R) || !R.HasWeaponComponent) return r.ItemType == R.ItemType;
            if (!r.HasWeaponComponent || IsMountKit(r)) return false;
            Dictionary<ItemObject, bool> m;
            if (!_same.TryGetValue(r, out m)) { m = new Dictionary<ItemObject, bool>(); _same[r] = m; }
            bool v;
            if (m.TryGetValue(R, out v)) return v;
            v = false;
            if (_sameWc != null)
            {
                try { v = (bool)_sameWc.Invoke(null, new object[] { Wc(r), Wc(R) }); }
                catch { v = KitGaps.Group(r) == KitGaps.Group(R); }
            }
            else v = KitGaps.Group(r) == KitGaps.Group(R);
            m[R] = v;
            return v;
        }

        private static List<WeaponClass> Wc(ItemObject it)
        {
            List<WeaponClass> l;
            if (_wc.TryGetValue(it, out l)) return l;
            try { l = _getWc.Invoke(null, new object[] { it }) as List<WeaponClass>; } catch { l = null; }
            if (l == null) l = new List<WeaponClass>();
            _wc[it] = l;
            return l;
        }

        /// <summary>Pierwszy predykat potoku, ktorego sztuka r nie przechodzi u zolnierza typu t (R - sztuka wzorca, mounted - jezdziec); 0 - pasuje.</summary>
        private static int Fail(CharacterObject t, ItemObject r, ItemObject R, bool mounted, out string text)
        {
            text = null;
            if (QuartermasterLaw.BarredInBattle(r) || IsDragon(r)) { text = "Never handed out to a soldier: an heirloom, a lore blade, kit of the dead or a dragon."; return RBarred; }
            if (LegendaryLaw.IsLegend(r)) { text = "A legendary blade - taken off any soldier as he takes the field."; return RLegend; }
            if (GiantGear.Is(r) && !GiantGear.MayWear(t)) { text = "Giant's gear - only for troops that carry it in their own kit."; return RGiant; }
            if (r.ItemType != ItemObject.ItemTypeEnum.HorseHarness)   // K5: uprzezy w bitwie nikt nie sprawdza pod katem Jazdy - tu tez nie
            {
                string why;
                if (!ItemReq.Meets(t, r, out why)) { text = why; return RSkill; }
            }
            if (mounted && r.HasWeaponComponent && !IsMountKit(r) && !MenUpgrade.MountOk(r)) { text = "Cannot be used from the saddle."; return RSaddle; }
            var s = Settings.Current;
            if (s != null && !s.SkillsDecideEnabled && R != null && (int)r.Tier > Math.Min(6, (int)R.Tier + 2)) { text = "Above the tier the hand-out gives this troop."; return RTier; }
            int cc = CultureOf(t, r);
            if (cc != GearFit.CultureFine) { text = CultureText(cc, r); return RCulture; }
            return 0;
        }

        private static int CultureOf(CharacterObject t, ItemObject r)
        {
            bool q = GearFit.Quiet;
            GearFit.Quiet = true;
            try { return GearFit.CultureCode(t, r); }
            finally { GearFit.Quiet = q; }
        }

        private static string CultureText(int code, ItemObject r)
        {
            if (code == GearFit.CultureDoth) return "Dothraki weapons are for the Dothraki only.";
            if (code == GearFit.CultureMount) return "Not a mount their people ride (" + MountName(r) + ").";
            string c = r != null && r.Culture != null && r.Culture.Name != null ? r.Culture.Name.ToString() : null;
            return "From across the Narrow Sea" + (string.IsNullOrEmpty(c) ? "" : " (" + c + ")") + " - not for this troop.";
        }

        private static string MountName(ItemObject r)
        {
            switch (MountLaw.FamilyOf(r))
            {
                case MountLaw.Fam.Camel: return "a camel";
                case MountLaw.Fam.Chariot: return "a chariot";
                case MountLaw.Fam.Elephant: return "an elephant";
                case MountLaw.Fam.Mammoth: return "a mammoth";
                default: return r != null && r.Name != null ? r.Name.ToString() : "this mount";
            }
        }

        /// <summary>Krotka nazwa rodzaju broni sztuki wzorca (przyczyna "klasa broni").</summary>
        private static string KindWord(ItemObject R)
        {
            var wc = R != null && R.PrimaryWeapon != null ? R.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
            switch (wc)
            {
                case WeaponClass.OneHandedSword: case WeaponClass.TwoHandedSword: return "a sword";
                case WeaponClass.OneHandedAxe: case WeaponClass.TwoHandedAxe: return "an axe";
                case WeaponClass.Mace: case WeaponClass.TwoHandedMace: return "a mace";
                case WeaponClass.OneHandedPolearm: case WeaponClass.TwoHandedPolearm: case WeaponClass.LowGripPolearm: return "a spear";
                case WeaponClass.Dagger: return "a dagger";
                case WeaponClass.Pick: return "a pick";
                case WeaponClass.Javelin: return "javelins";
                case WeaponClass.ThrowingAxe: return "throwing axes";
                case WeaponClass.ThrowingKnife: return "throwing knives";
                case WeaponClass.SmallShield: case WeaponClass.LargeShield: return "a shield";
                default: return R != null && R.Name != null ? R.Name.ToString() : "this kind";
            }
        }

        private static ItemObject Top(Dictionary<ItemObject, int> d)
        {
            ItemObject best = null; int bn = -1;
            foreach (var kv in d) if (kv.Value > bn || (kv.Value == bn && best != null && string.CompareOrdinal(kv.Key.StringId, best.StringId) < 0)) { best = kv.Key; bn = kv.Value; }
            return best;
        }

        // ---------------------------------------------------------------- przyczyny
        /// <summary>Przyczyny brakow typu (wedlug rodzaju) i jezdzcow pieszo - tymi samymi predykatami co przydzial (katalog projektu 2.1).</summary>
        private static void Explain(Result res, TypeRow t)
        {
            foreach (int cat in KitGaps.Order)
            {
                Dictionary<ItemObject, int> refs;
                if (t.Short[cat] <= 0 || !t.Lost.TryGetValue(cat, out refs) || refs.Count == 0) continue;
                var R = Top(refs);
                var l = Why(res, t, R, cat == KitGaps.Shield || cat >= KitGaps.Melee);
                t.Why[cat] = l;
                if (l.Count > 0) res.WhyCount[l[0].Code]++;
            }
            if (t.NoHorse > 0)
            {
                ItemObject R = null; int bn = -1;
                foreach (var kv in t.Kit) if (kv.Key.ItemType == ItemObject.ItemTypeEnum.Horse && kv.Value > bn) { R = kv.Key; bn = kv.Value; }
                if (R != null) { t.HorseWhy.AddRange(Why(res, t, R, false)); if (t.HorseWhy.Count > 0) res.WhyCount[t.HorseWhy[0].Code]++; }
            }
            if (t.NoSaddle > 0) { t.SaddleWhy.AddRange(HarnessWhy(res, t)); if (t.SaddleWhy.Count > 0) res.WhyCount[t.SaddleWhy[0].Code]++; }
        }

        /// <summary>Czemu typ t nie ma sztuki rodzaju R: resztka zbrojowni tego rodzaju z pierwszym niespelnionym predykatem; bez resztki - inna klasa broni,
        /// wszystkie wydane, zakazane, brak; do tego dodatki u innych.</summary>
        private static List<Reason> Why(Result res, TypeRow t, ItemObject R, bool weapon)
        {
            var l = new List<Reason>();
            var by = new Dictionary<int, Reason>();
            int fits = 0, cand = 0;
            foreach (var kv in res.Rem)
            {
                if (!SameKind(kv.Key, R)) continue;
                cand += kv.Value;
                string text;
                int code = Fail(t.Troop, kv.Key, R, t.Mounted, out text);
                if (code == 0) { fits += kv.Value; continue; }
                Reason rs;
                if (!by.TryGetValue(code, out rs)) { rs = new Reason { Code = code }; by[code] = rs; }
                rs.Count += kv.Value;
                if (kv.Value > rs.SampleN) { rs.SampleN = kv.Value; rs.Sample = kv.Key; rs.Text = text; }
            }
            var codes = new List<int>(by.Keys);
            codes.Sort((a, b) => { int c = by[b].Count.CompareTo(by[a].Count); return c != 0 ? c : a.CompareTo(b); });
            foreach (int c in codes)
            {
                var rs = by[c];
                string name = rs.Sample != null && rs.Sample.Name != null ? rs.Sample.Name.ToString() : "pieces";
                rs.Text = "In the troops' armoury, not taken: " + rs.Count + " " + (rs.Count == rs.SampleN ? name : name + " and others") + " - " + rs.Text;
                l.Add(rs);
            }
            if (fits > 0) l.Add(new Reason { Code = RFits, Count = fits, Text = fits + " in the troops' armoury would fit them but stayed there - the hand-out fills each slot once, in order of rank,"
                                                                             + " and the checks after it do not refill this kind." });
            if (cand == 0)
            {
                // DTE trzyma sie klasy broni wzorca (IsWeaponSuitable: HaveSameWeaponClass) - inna klasa tego samego typu przedmiotu zostaje na polce
                if (weapon && R.HasWeaponComponent)
                {
                    int other = 0, sn = 0; ItemObject sample = null;
                    foreach (var kv in res.Rem)
                        if (kv.Key.ItemType == R.ItemType && !SameKind(kv.Key, R)) { other += kv.Value; if (kv.Value > sn) { sn = kv.Value; sample = kv.Key; } }
                    if (other > 0)
                        l.Add(new Reason { Code = RClass, Count = other, Text = "Their kit carries " + KindWord(R) + "; the troops' armoury holds " + other + " of another kind ("
                                                                             + sample.Name + ") - the hand-out keeps to the kit's kind of weapon." });
                }
                int given = 0, barred = 0;
                foreach (var kv in res.Given) if (SameKind(kv.Key, R)) given += kv.Value;
                foreach (var kv in res.Barred) if (SameKind(kv.Key, R)) barred += kv.Value;
                foreach (var kv in res.Black) if (SameKind(kv.Key, R)) barred += kv.Value;
                if (given > 0) l.Add(new Reason { Code = RFew, Count = given, Text = "All " + given + " in the troops' armoury were handed out - not enough for every man (the higher ranks are served first)." });
                if (barred > 0) l.Add(new Reason { Code = RBarred, Count = barred, Text = barred + " more are never handed out to soldiers (heirlooms, lore blades, kit of the dead, Dynamic Troop Equipment's black list)." });
                if (given == 0 && barred == 0 && (l.Count == 0 || l[l.Count - 1].Code != RClass)) l.Add(new Reason { Code = RNone, Text = "None in the troops' armoury." });
            }
            if (weapon)
            {
                int sp = 0;
                foreach (var kv in res.Spares) if (SameKind(kv.Key, R)) sp += kv.Value;
                if (sp > 0) l.Add(new Reason { Code = RExtra, Count = sp, Text = sp + " went to other men as spare kit their own kit does not have." });
            }
            return l;
        }

        /// <summary>K5: kon bez uprzezy - tylko "brak wolnej uprzezy / inna rodzina / zapas", nigdy "Requires Riding" (w bitwie nikt nie sprawdza uprzezy pod katem Jazdy).</summary>
        private static List<Reason> HarnessWhy(Result res, TypeRow t)
        {
            var l = new List<Reason>();
            ItemObject horse = null; int bn = -1;
            foreach (var kv in t.Kit) if (kv.Key.ItemType == ItemObject.ItemTypeEnum.Horse && kv.Value > bn) { horse = kv.Key; bn = kv.Value; }
            int fit = 0, other = 0;
            foreach (var kv in res.Rem)
            {
                if (kv.Key.ItemType != ItemObject.ItemTypeEnum.HorseHarness) continue;
                if (horse != null && SaddleLaw.Fits(kv.Key, horse)) fit += kv.Value; else other += kv.Value;
            }
            if (fit > 0) l.Add(new Reason { Code = RHarness, Count = fit, Text = fit + " harness in the troops' armoury would fit, but the hand-out did not pair them with their horses." });
            if (other > 0) l.Add(new Reason { Code = RHarness, Count = other, Text = "The troops' armoury's " + other + " spare harness are for camels or other beasts - none fits their horses." });
            if (fit == 0 && other == 0) l.Add(new Reason { Code = RHarness, Text = "No spare harness in the troops' armoury - the horse stays in the armoury and the rider fights on foot." });
            return l;
        }

        /// <summary>K6: sakwy - sztuki tego samego rodzaju (predykat DTE wobec sztuki wzorca), ktore przejda wszystkie predykaty przydzialu (siodlo bez wyjatku
        /// wlasnego wzorca - DTE go nie zna). Liczba na typ i rodzaj najwyzej brakow; BagPieces - rozne sztuki z sakw (do logu).</summary>
        private static void Baggage(Result res, MobileParty mp)
        {
            var bag = mp.ItemRoster;
            if (bag == null) return;
            var items = new List<KeyValuePair<ItemObject, int>>();
            for (int i = 0; i < bag.Count; i++)
            {
                var el = bag.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || (!it.HasWeaponComponent && !it.HasArmorComponent && it.ItemType != ItemObject.ItemTypeEnum.Horse)) continue;
                items.Add(new KeyValuePair<ItemObject, int>(it, el.Amount));
            }
            if (items.Count == 0) return;
            var useful = new Dictionary<ItemObject, int>();
            foreach (var t in res.Rows)
                foreach (int cat in KitGaps.Order)
                {
                    Dictionary<ItemObject, int> refs;
                    if (t.Short[cat] <= 0 || !t.Lost.TryGetValue(cat, out refs) || refs.Count == 0) continue;
                    var R = Top(refs);
                    int n = 0;
                    foreach (var kv in items)
                    {
                        string text;
                        if (!SameKind(kv.Key, R) || Fail(t.Troop, kv.Key, R, t.Mounted, out text) != 0) continue;
                        n += kv.Value;
                        useful[kv.Key] = kv.Value;
                    }
                    t.Bag[cat] = Math.Min(n, t.Short[cat]);
                }
            foreach (var v in useful.Values) res.BagPieces += v;
        }

        // ---------------------------------------------------------------- 133d: konie i uprzaz
        /// <summary>133d (strona "Horses and harness"): czego brakuje jezdzcom podgladu, ktorzy pojda pieszo, i skad to wziac. Bez nowej reguly: braki to
        /// NoHorse / NoSaddle podgladu (te same straze co bitwa), konie pokrywa resztka zbrojowni i sakwy tymi samymi predykatami co przydzial (Fail: Jazda,
        /// kraj wierzchowca), uprzaz - rodzina konia (SaddleLaw.Fits) bez Jazdy (K5: w bitwie nikt jej nie sprawdza).</summary>
        internal sealed class Mounts
        {
            internal int NeedHorse;      // jezdzcy bez konia w bitwie
            internal int NeedHarness;    // kon bez uprzezy + bez konia, ktorych typ wymaga uprzezy (z koniem beda jej potrzebowac) - przy RiderNeedsHarness
            internal int RemHorseFit;    // konie resztki zbrojowni, na ktorych ci jezdzcy moga jechac (przyciete do braku typu; kazdy kon raz)
            internal int BagHorseFit;    // to samo z sakw (po resztce)
            internal int RemHarness;     // uprzaz rodziny konia w resztce zbrojowni
            internal int BagHarness;     // uprzaz rodziny konia w sakwach (SaddleLaw.BagFit)
            internal int UseHarness, MoveHarness, BuyHarness, BuyHorse;
            internal int HardTypes;      // typy, ktorym pierwsza przyczyna braku konia to umiejetnosc (Requires Riding)
        }

        /// <summary>Wierzchowiec pod jezdzca (jak licznik gry ItemRoster: juczne najpierw, bydlo osobno).</summary>
        internal static bool RidingMount(ItemObject it)
        {
            var hc = it != null && it.ItemType == ItemObject.ItemTypeEnum.Horse ? it.HorseComponent : null;
            return hc != null && !hc.IsPackAnimal && hc.IsMount;
        }

        /// <summary>Potrzeby koni i uprzezy podgladu (leniwie, raz na podglad - sakwy sa w kluczu pamieci).</summary>
        internal static Mounts MountsOf(Result res, MobileParty mp)
        {
            if (res == null || !res.Ok) return new Mounts();
            if (res.MountsCache != null) return res.MountsCache;
            var m = new Mounts();
            var needy = new List<TypeRow>();
            foreach (var t in res.Rows)
            {
                if (t.NoHorse > 0)
                {
                    needy.Add(t);
                    if (t.HorseWhy.Count > 0 && t.HorseWhy[0].Code == RSkill) m.HardTypes++;
                }
                m.NeedHorse += t.NoHorse;
                m.NeedHarness += t.NoSaddle + (SaddleLaw.On && SaddleLaw.Needs(t.Troop) ? t.NoHorse : 0);
            }
            var rem = new List<KeyValuePair<ItemObject, int>>();
            foreach (var kv in res.Rem)
            {
                if (kv.Value <= 0) continue;
                if (RidingMount(kv.Key)) rem.Add(kv);
                else if (kv.Key.ItemType == ItemObject.ItemTypeEnum.HorseHarness && SaddleLaw.Fits(kv.Key)) m.RemHarness += kv.Value;
            }
            var bag = new List<KeyValuePair<ItemObject, int>>();
            var br = mp != null ? mp.ItemRoster : null;
            if (br != null)
            {
                for (int i = 0; i < br.Count; i++)
                {
                    var el = br.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && RidingMount(el.EquipmentElement.Item)) bag.Add(new KeyValuePair<ItemObject, int>(el.EquipmentElement.Item, el.Amount));
                }
                m.BagHarness = SaddleLaw.BagFit(br);
            }
            var left = new Dictionary<TypeRow, int>();
            foreach (var t in needy) left[t] = t.NoHorse;
            m.RemHorseFit = Cover(needy, left, rem);
            m.BagHorseFit = Cover(needy, left, bag);
            m.BuyHorse = Math.Max(0, m.NeedHorse - m.RemHorseFit - m.BagHorseFit);
            m.UseHarness = Math.Min(m.NeedHarness, m.RemHarness);
            m.MoveHarness = Math.Min(m.BagHarness, m.NeedHarness - m.UseHarness);
            m.BuyHarness = m.NeedHarness - m.UseHarness - m.MoveHarness;
            res.MountsCache = m;
            return m;
        }

        /// <summary>Konie z zapasu dla typow z jezdzcami bez konia (po kolei jak lista okna, sztuki wedlug StringId): pierwszy predykat przydzialu (Fail) wobec
        /// konia wzorca typu; kazdy kon raz. left - brak typu, pomniejszany. Wynik - ile pokryto.</summary>
        private static int Cover(List<TypeRow> needy, Dictionary<TypeRow, int> left, List<KeyValuePair<ItemObject, int>> stock)
        {
            var have = new Dictionary<ItemObject, int>();
            foreach (var kv in stock) Add(have, kv.Key, kv.Value);
            var keys = new List<ItemObject>(have.Keys);
            keys.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
            int got = 0;
            foreach (var t in needy)
            {
                int need = left[t];
                if (need <= 0) continue;
                ItemObject R = null; int bn = -1;
                foreach (var kv in t.Kit) if (kv.Key.ItemType == ItemObject.ItemTypeEnum.Horse && kv.Value > bn) { R = kv.Key; bn = kv.Value; }
                foreach (var it in keys)
                {
                    if (need <= 0) break;
                    int n = have[it];
                    if (n <= 0) continue;
                    string why;
                    if (Fail(t.Troop, it, R, false, out why) != 0) continue;
                    int take = Math.Min(n, need);
                    have[it] = n - take; need -= take; got += take;
                }
                left[t] = need;
            }
            return got;
        }

        /// <summary>Resztka zbrojowni z przyczyna (leniwie - strona "Unused in the troops' armoury"): wobec typow, ktorych wzorzec ma ten rodzaj.</summary>
        private static List<Left> LeftOf(Result res)
        {
            if (res.LeftList != null) return res.LeftList;
            var l = new List<Left>();
            if (_same == null) _same = new Dictionary<ItemObject, Dictionary<ItemObject, bool>>();
            if (_wc == null) _wc = new Dictionary<ItemObject, List<WeaponClass>>();
            foreach (var kv in res.Rem)
            {
                var r = kv.Key;
                var x = new Left { Item = r, Count = kv.Value };
                try { x.Code = LeftWhy(res, r, out x.Text); }
                catch (Exception e) { Stumble("LeftWhy", e); x.Code = 0; x.Text = "-"; }
                l.Add(x);
            }
            l.Sort((a, b) => { int c = b.Count.CompareTo(a.Count); return c != 0 ? c : string.CompareOrdinal(a.Item.StringId, b.Item.StringId); });
            res.LeftList = l;
            return l;
        }

        private static int LeftWhy(Result res, ItemObject r, out string text)
        {
            text = null;
            int kinds = 0, firstCode = -1, allSkill = 0, best = -1;
            string firstText = null, firstName = null;
            SkillObject sk = ItemReq.SkillFor(r);
            foreach (var t in res.Rows)
            {
                if (t.Men <= 0) continue;
                ItemObject R = null;
                foreach (var kv in t.Kit) if (SameKind(r, kv.Key)) { R = kv.Key; break; }
                if (R == null) continue;
                kinds++;
                string why;
                int code = r.ItemType == ItemObject.ItemTypeEnum.HorseHarness ? HarnessFail(t, r, out why) : Fail(t.Troop, r, R, t.Mounted, out why);
                if (code == 0)
                {
                    bool shortOf = false;
                    foreach (var lk in t.Lost) foreach (var ri in lk.Value.Keys) if (SameKind(r, ri)) { shortOf = true; break; }
                    if (r.ItemType == ItemObject.ItemTypeEnum.Horse && t.NoHorse > 0) shortOf = true;
                    text = shortOf ? "fits " + t.Name + ", but stayed in the armoury after the hand-out." : "spare - every " + t.Name + " who needs one already has one.";
                    return shortOf ? RFits : RSpare;
                }
                if (code == RSkill) { allSkill++; if (sk != null) best = Math.Max(best, t.Troop.GetSkillValue(sk)); }
                if (firstCode < 0) { firstCode = code; firstText = why; firstName = t.Name; }
            }
            if (kinds == 0) { text = "no kind of your men carries this in their kit."; return RNoKit; }
            if (allSkill == kinds && sk != null && best >= 0)
            {
                text = "too hard for all: Requires " + sk.Name + " " + r.Difficulty + " - the best of your men who carry this kind have " + best + ".";
                return RSkill;
            }
            text = firstText + " (" + firstName + (kinds > 1 ? " and " + (kinds - 1) + (kinds == 2 ? " other kind" : " other kinds") : "") + ")";
            return firstCode;
        }

        private static int HarnessFail(TypeRow t, ItemObject h, out string why)
        {
            why = null;
            foreach (var kv in t.Kit)
                if (kv.Key.ItemType == ItemObject.ItemTypeEnum.Horse && SaddleLaw.Fits(h, kv.Key)) return 0;
            why = "for camels or other beasts - fits none of their horses.";
            return RHarness;
        }

        // ---------------------------------------------------------------- teksty okna (po angielsku)
        /// <summary>Tytul wiersza glownego okna.</summary>
        internal static string MainTitle(Result r)
        {
            if (r == null || !r.Ok) return "Kit for the next battle (not available)";
            if (r.Men <= 0) return "Kit for the next battle: no fit soldiers in your party";
            if (r.Missing <= 0 && r.Foot <= 0) return "Kit for the next battle: full kit (" + r.Men + (r.Men == 1 ? " man)" : " men)");
            var parts = new List<string>();
            if (r.Missing > 0)
            {
                string k = KitGaps.TopKinds(r.Short, 3, true);
                parts.Add(r.Missing + (r.Missing == 1 ? " slot empty" : " slots empty") + (k.Length > 0 ? " - " + k : ""));
            }
            if (r.Foot > 0) parts.Add(r.Foot + (r.Foot == 1 ? " rider on foot" : " riders on foot"));
            return "Kit for the next battle: " + string.Join("; ", parts.ToArray());
        }

        /// <summary>Podpowiedz wiersza glownego okna.</summary>
        internal static string MainHint(Result r)
        {
            if (r == null || !r.Ok) return r != null && r.Why != null ? r.Why : "Not available now.";
            return "Worked out by the same hand-out as the battle: the troops' armoury, each man's skill, culture and saddle, and the checks as they take the field. "
                   + r.Wounded + " wounded do not fight and are not counted. Shown for a battle in the field; armour of the kit is picked at random within the troop's"
                   + " band, so single pieces can differ from one look to the next. The armoury readiness bar on the map counts the wounded too and leaves out spare kit,"
                   + " so its numbers differ.";
        }

        /// <summary>Opis okna listy typow (najwyzej ok. 5 linii - lekcja 109.1).</summary>
        internal static string ListDesc(Result r)
        {
            int kinds = 0;
            foreach (var t in r.Rows) if (t.Men > 0) kinds++;
            return r.Men + " fit men of " + kinds + (kinds == 1 ? " kind" : " kinds") + ", handed out from the troops' armoury as for a battle in the field: "
                   + r.Missing + (r.Missing == 1 ? " kit slot empty, " : " kit slots empty, ") + r.Foot + (r.Foot == 1 ? " rider on foot." : " riders on foot.")
                   + " Pick a kind of troop to see what they lack and why.";
        }

        /// <summary>Wiersz typu na liscie.</summary>
        internal static string RowTitle(TypeRow t)
        {
            string head = t.Name + " x" + t.Men;
            if (t.Men <= 0) return head + " - all wounded";
            if (t.Missing <= 0 && t.Foot <= 0) return head + " - full kit";
            var parts = new List<string>();
            if (t.Missing > 0) { string k = KitGaps.TopKinds(t.Short, 2, true); parts.Add("short: " + (k.Length > 0 ? k : t.Missing + " slots")); }
            if (t.Foot > 0) parts.Add(t.Foot + (t.Foot == 1 ? " rider on foot" : " riders on foot"));
            return head + " - " + string.Join("; ", parts.ToArray());
        }

        /// <summary>Podpowiedz wiersza typu: pierwsza przyczyna najwiekszego braku.</summary>
        internal static string RowHint(TypeRow t)
        {
            if (t.Men <= 0) return "All men of this kind are wounded - they do not fight.";
            int bc = -1;
            foreach (int c in KitGaps.Order) if (t.Short[c] > 0 && (bc < 0 || t.Short[c] > t.Short[bc])) bc = c;
            if (bc >= 0 && t.Why[bc] != null && t.Why[bc].Count > 0) return Cap(KitGaps.NameEn(bc)) + ": " + t.Why[bc][0].Text;
            if (t.NoHorse > 0 && t.HorseWhy.Count > 0) return "Horses: " + t.HorseWhy[0].Text;
            if (t.NoSaddle > 0 && t.SaddleWhy.Count > 0) return "Harness: " + t.SaddleWhy[0].Text;
            return t.Missing <= 0 && t.Foot <= 0 ? "Every man gets each piece of the kit his skill and rank call for." : "Look for the reasons.";
        }

        internal static string UnusedTitle(Result r)
        {
            int n = r.LeftPieces;
            return n <= 0 ? "Unused in the troops' armoury: nothing" : "Unused in the troops' armoury: " + n + (n == 1 ? " piece" : " pieces");
        }

        /// <summary>Strony typu (akapity rozdzielone pusta linia - Paginate; zaden akapit nie dluzszy niz strona).</summary>
        internal static string TypeText(Result res, TypeRow t)
        {
            var sb = new StringBuilder();
            sb.Append(t.Name).Append(" - ").Append(t.Men).Append(" fit to fight");
            if (t.Wounded > 0) sb.Append(", ").Append(t.Wounded).Append(" wounded stay out of the battle");
            sb.Append('.');
            string sk = Skills(t);
            if (sk.Length > 0) sb.Append("\nTheir skills: ").Append(sk).Append('.');
            if (t.Missing <= 0 && t.Foot <= 0) sb.Append("\nEvery man gets each piece of the kit his skill and rank call for.");
            else if (t.Missing > 0)
            {
                sb.Append('\n').Append(t.Missing).Append(t.Missing == 1 ? " kit slot goes empty." : " kit slots go empty.");
                if (t.Replaced > 0) sb.Append(' ').Append(t.Replaced).Append(t.Replaced == 1 ? " weapon gap is" : " weapon gaps are").Append(" filled with another kind of weapon and not counted.");
            }
            foreach (int c in KitGaps.Order)
            {
                if (t.Short[c] <= 0) continue;
                sb.Append("\n\n").Append(Cap(KitGaps.NameEn(c))).Append(": ").Append(t.Short[c]).Append(" of ").Append(t.Need[c]).Append(" missing.");
                if (c == KitGaps.SideArm) sb.Append(" (The second weapon of the kit.)");
                var why = t.Why[c];
                if (why != null) { int n = 0; foreach (var w in why) { if (n++ >= 3) break; sb.Append("\n- ").Append(w.Text); } }
                if (t.Bag[c] > 0) sb.Append("\n- In your baggage: ").Append(t.Bag[c]).Append(" they could use - put them in the troops' armoury.");
            }
            if (t.Riders > 0)
            {
                sb.Append("\n\nRiders: ").Append(t.Riders);
                if (t.Foot <= 0) sb.Append(" - every one on a horse").Append(SaddleLaw.On ? " with harness." : ".");
                else
                {
                    sb.Append(", ").Append(t.Foot).Append(" on foot.");
                    if (t.NoHorse > 0)
                    {
                        sb.Append("\nNo horse: ").Append(t.NoHorse).Append('.');
                        int n = 0; foreach (var w in t.HorseWhy) { if (n++ >= 2) break; sb.Append("\n- ").Append(w.Text); }
                    }
                    if (t.NoSaddle > 0)
                    {
                        sb.Append("\nA horse but no harness: ").Append(t.NoSaddle).Append('.');
                        int n = 0; foreach (var w in t.SaddleWhy) { if (n++ >= 2) break; sb.Append("\n- ").Append(w.Text); }
                    }
                }
            }
            var more = new List<string>();
            if (t.Stripped > 0) more.Add("Taken off as they take the field: " + t.Stripped + " (" + OffText(t.StripWhy) + ").");
            if (t.Bare > 0) more.Add(t.Bare + (t.Bare == 1 ? " man would" : " men would") + " take the field with no weapon at all.");
            if (t.MoraleMen > 0)
                more.Add("Morale at the start of battle: -" + (t.MoraleSum / t.MoraleMen).ToString("0.#", CultureInfo.InvariantCulture) + " on average for " + t.MoraleMen
                         + (t.MoraleMen == 1 ? " man whose kit is" : " men whose kit is") + " worse than the kit their skill and rank call for.");
            if (more.Count > 0) sb.Append("\n\n").Append(string.Join("\n", more.ToArray()));
            sb.Append("\n\nThe men handed out last get the gaps; in a siege or a hideout every rider fights on foot.");
            return sb.ToString();
        }

        private static string Skills(TypeRow t)
        {
            var seen = new List<SkillObject>();
            var parts = new List<string>();
            foreach (int c in KitGaps.Order)
            {
                Dictionary<ItemObject, int> refs;
                if (t.Short[c] <= 0 || !t.Lost.TryGetValue(c, out refs) || refs.Count == 0) continue;
                var s = ItemReq.SkillFor(Top(refs));
                if (s == null || seen.Contains(s)) continue;
                seen.Add(s);
                parts.Add(s.Name + " " + t.Troop.GetSkillValue(s));
            }
            if (t.NoHorse > 0 && !seen.Contains(DefaultSkills.Riding)) parts.Add(DefaultSkills.Riding.Name + " " + t.Troop.GetSkillValue(DefaultSkills.Riding));
            return string.Join(", ", parts.ToArray());
        }

        private static string OffText(int[] why)
        {
            var parts = new List<string>();
            if (why[DragonUnmount.StripSkill] > 0) parts.Add(why[DragonUnmount.StripSkill] + " over their skill");
            if (why[DragonUnmount.StripLegend] > 0) parts.Add(why[DragonUnmount.StripLegend] + " legendary");
            if (why[DragonUnmount.StripGiant] > 0) parts.Add(why[DragonUnmount.StripGiant] + " giant's gear");
            if (why[DragonUnmount.StripMountLand] > 0) parts.Add(why[DragonUnmount.StripMountLand] + " mounts not of their land");
            if (why[DragonUnmount.StripMountSkill] > 0) parts.Add(why[DragonUnmount.StripMountSkill] + " mounts too hard to ride");
            return string.Join(", ", parts.ToArray());
        }

        private const int LeftShown = 48;   // najwyzej tyle rodzajow resztki na stronach (ok. 3-4 strony), reszta jedna linia

        /// <summary>Strony "Unused in the troops' armoury".</summary>
        internal static string UnusedText(Result res)
        {
            var sb = new StringBuilder();
            var l = LeftOf(res);
            int n = res.LeftPieces;
            if (n <= 0) sb.Append("Nothing is left in the troops' armoury after the hand-out - every piece went to a man.");
            else sb.Append("Left in the troops' armoury after the hand-out: ").Append(n).Append(n == 1 ? " piece" : " pieces").Append(" of ").Append(l.Count)
                   .Append(l.Count == 1 ? " kind" : " kinds").Append(", each with the reason no man took it.");
            int shown = Math.Min(l.Count, LeftShown);
            for (int i = 0; i < shown; i += 6)
            {
                sb.Append("\n\n");
                for (int k = i; k < shown && k < i + 6; k++)
                {
                    if (k > i) sb.Append('\n');
                    var x = l[k];
                    sb.Append(x.Count).Append(" x ").Append(x.Item.Name).Append(" - ").Append(x.Text);
                }
            }
            if (l.Count > shown)
            {
                int rest = 0;
                for (int k = shown; k < l.Count; k++) rest += l[k].Count;
                sb.Append("\n\nAnd ").Append(l.Count - shown).Append(" more kinds of fewer pieces (").Append(rest).Append(" in all).");
            }
            int barred = 0, black = 0;
            foreach (var v in res.Barred.Values) barred += v;
            foreach (var v in res.Black.Values) black += v;
            if (barred > 0 || black > 0)
            {
                sb.Append("\n\nNever handed out to a soldier: ");
                var parts = new List<string>();
                if (barred > 0) parts.Add(barred + " heirlooms, lore blades, kit of the dead and dragons");
                if (black > 0) parts.Add(black + " on Dynamic Troop Equipment's black list");
                sb.Append(string.Join("; ", parts.ToArray())).Append('.');
            }
            int off = 0; foreach (var v in res.OffWhy) off += v;
            if (off > 0) sb.Append("\n\nHanded out, but taken off as they take the field: ").Append(off).Append(" (").Append(OffText(res.OffWhy)).Append(") - these stay in the troops' armoury.");
            sb.Append("\n\nThe hand-out serves the higher ranks first, and each man only takes the kind of piece his kit carries.");
            return sb.ToString();
        }

        private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // ---------------------------------------------------------------- log i kwatermistrz
        /// <summary>Linia logu przy otwarciu okna: te same liczby co okno.</summary>
        internal static string LogLine(Result r)
        {
            if (r == null || !r.Ok) return P + "podglad niedostepny - " + (r != null ? r.Why : "?") + " | " + _wiring;
            int need = 0; foreach (var v in r.Need) need += v;
            var sb = new StringBuilder(P);
            sb.Append("podglad - typow ").Append(r.Rows.Count).Append(", zdrowych ").Append(r.Men).Append(" (rannych ").Append(r.Wounded).Append("), slotow wzorca ").Append(need)
              .Append(", pustych ").Append(r.Missing).Append(" (").Append(KitGaps.KindsOf(r.Short, false)).Append("; zastapione innym rodzajem ").Append(r.Replaced)
              .Append(", dodatki ponad wzorzec ").Append(r.Extra).Append("), jezdzcy ").Append(r.Riders).Append(" - pieszo bez konia ").Append(r.NoHorse).Append(", bez uprzezy ").Append(r.NoSaddle)
              .Append(", zdjete przy wystawieniu ").Append(r.Stripped).Append(", bez broni ").Append(r.Bare);
            sb.Append(" | przyczyny (typ x rodzaj, pierwsza): ").Append(WhyLine(r));
            int black = 0, barred = 0;
            foreach (var v in r.Black.Values) black += v;
            foreach (var v in r.Barred.Values) barred += v;
            sb.Append(" | nieuzyte ").Append(r.LeftPieces).Append(" szt. (").Append(r.Rem.Count).Append(" rodzajow), czarna lista DTE ").Append(black).Append(", UniqueWard ").Append(barred)
              .Append(" | w sakwach dla brakow ").Append(r.BagPieces).Append(" szt.");
            sb.Append(" | kara morale ").Append(r.MoraleMen > 0 ? "sr. " + (r.MoraleSum / r.MoraleMen).ToString("0.#", CultureInfo.InvariantCulture) + " u " + r.MoraleMen : "0");
            sb.Append(" | tozsamosc z Missing ").Append(r.Bad == 0 ? "zgodna" : "ROZJAZD u " + r.Bad).Append(", ksiegowosc puli ").Append(r.Drift == 0 ? "zgodna" : "rozjazd " + r.Drift + " pozycji");
            sb.Append(" | czas ").Append(r.Ms).Append(" ms | uprzaz ").Append(SaddleLaw.On ? "TAK" : "NIE").Append(", kultura GearFit ").Append(GearFit.CultureOn ? "TAK" : "NIE")
              .Append(", podloga ").Append(GearFit.FloorOn ? "TAK" : "NIE");
            int n = 0, more = 0;
            var ex = new StringBuilder();
            foreach (var t in r.Rows)
            {
                if (t.Missing <= 0 && t.Foot <= 0) continue;
                if (n >= 8) { more++; continue; }
                n++;
                if (ex.Length > 0) ex.Append("; ");
                ex.Append(t.Id).Append(" x").Append(t.Men).Append(" - puste ").Append(t.Missing);
                string k = KitGaps.KindsOf(t.Short, false);
                if (k.Length > 0) ex.Append(" (").Append(k).Append(')');
                if (t.Foot > 0) ex.Append(", pieszo ").Append(t.Foot);
                foreach (int c in KitGaps.Order) if (t.Why[c] != null && t.Why[c].Count > 0) { ex.Append(" [").Append(CodePl[t.Why[c][0].Code]).Append(']'); break; }
            }
            sb.Append(" | typy z brakami: ").Append(n == 0 ? "brak" : ex.ToString()).Append(more > 0 ? "; i " + more + " innych" : "");
            if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles);
            return sb.Append('.').ToString();
        }

        private static string WhyLine(Result r)
        {
            var parts = new List<string>();
            for (int c = 1; c < RCodes; c++) if (r.WhyCount[c] > 0) parts.Add(CodePl[c] + " " + r.WhyCount[c]);
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "brak";
        }

        /// <summary>Segment linii raportu okna (ArmyReview.ReportLine).</summary>
        internal static string Brief(Result r)
        {
            if (r == null || !r.Ok) return "niedostepny";
            return "ludzi " + r.Men + ", puste " + r.Missing + (r.Missing > 0 ? " (" + KitGaps.KindsOf(r.Short, false) + ")" : "") + ", pieszo " + r.Foot + ", nieuzyte " + r.LeftPieces + " szt., " + r.Ms + " ms";
        }

        /// <summary>Decyzja Jeffa (a) / K14: kwatermistrz (ShoutShortages - po bitwie, rano, przy otwarciu zbrojowni) na ekranie JEDNA linia z podgladu bitwy.
        /// false - podglad niedostepny (wolajacy zostaje przy papierze, jak dotad). any - czy jest co meldowac (puste sloty albo jezdzcy pieszo).</summary>
        internal static bool ShoutLine(string headline, int paperShort, out string line, out bool any)
        {
            line = null; any = false;
            Result r;
            try { r = Get(false); }
            catch (Exception e) { Stumble("ShoutLine", e); return false; }
            if (r == null || !r.Ok) return false;
            any = r.Missing > 0 || r.Foot > 0;
            if (any)
            {
                var parts = new List<string>();
                if (r.Missing > 0) parts.Add(r.Missing + (r.Missing == 1 ? " kit slot empty" : " kit slots empty"));
                if (r.Foot > 0) parts.Add(r.Foot + (r.Foot == 1 ? " rider on foot" : " riders on foot"));
                line = "QM: " + string.Join(" and ", parts.ToArray()) + " for the next battle - see Review the army in camp.";
            }
            try
            {
                Log.Info(P + "kwatermistrz (\"" + headline + "\") - na ekranie " + (any ? "jedna linia z podgladu: pustych " + r.Missing + " (" + KitGaps.KindsOf(r.Short, false) + "), pieszo " + r.Foot
                                                                                        : "nic do meldowania (podglad: komplet)")
                         + "; papier (QM short) - typow z brakami " + paperShort + " (tylko log, linie 'Kwatermistrz: papier' wyzej); podglad " + r.Ms + " ms.");
            }
            catch { }
            return true;
        }

        // ---------------------------------------------------------------- K15: podglad w misji
        /// <summary>K15 (GarrisonKit.DistributorsPostfix, bitwa w polu, TroopsFightWithOwnKitOnly): podglad na tej samej zbrojowni z wzorcami i kolejnoscia
        /// przydzialow bitwy, przed pierwszym wystawieniem. Linia logu z roznica przydzialu i czasem; porownanie z bitwa - BattleSegment na koniec misji.</summary>
        internal static void InMission(IDictionary dists, Mission mission)
        {
            ClearMission();
            var main = MobileParty.MainParty;
            if (main == null || dists == null) return;
            if (!Wire()) { _missionNote = "BRAK wpiec (" + _wiring + ")"; return; }
            object bd = dists.Contains(main.Id) ? dists[main.Id] : null;
            var bl = bd != null ? _fAssigns.GetValue(bd) as IList : null;
            if (bl == null || bl.Count == 0) { _missionNote = "brak przydzialow druzyny gracza"; return; }
            Result r;
            try { r = Compute(main, bl, true, false); }
            catch (Exception e) { Stumble("InMission", e); _missionNote = "blad (potkniecie)"; return; }
            if (r.Why != null) { _missionNote = "niepoliczony (" + r.Why + ")"; return; }
            int men = 0, diffMen = 0, diffSlots = 0;
            var asg = r.Asg ?? new List<object>();
            int bi = 0;
            for (int i = 0; i < asg.Count; i++)
            {
                // te same przydzialy w tej samej kolejnosci (Compute pomija tylko puste wpisy - tak samo tutaj)
                object ba = null;
                while (bi < bl.Count && ba == null) { var x = bl[bi++]; if (x != null && _getChar.Invoke(x, null) != null && _getRef.Invoke(x, null) != null) ba = x; }
                if (ba == null) break;
                var be = _fEq.GetValue(ba) as Equipment;
                var pe = _fEq.GetValue(asg[i]) as Equipment;
                if (be == null || pe == null) continue;
                men++;
                int d = 0;
                for (int s = 0; s < 12; s++) { if (s == 4) continue; if (be[(EquipmentIndex)s].Item != pe[(EquipmentIndex)s].Item) d++; }
                if (d > 0) { diffMen++; diffSlots += d; }
            }
            r.Asg = null; r.Ok = true;
            _mission = r; _mMen = men; _mDiffMen = diffMen; _mDiffSlots = diffSlots;
            try
            {
                Log.Info(P + "podglad w misji (K15, ta sama zbrojownia i te same wzorce co bitwa, przed wystawieniem) - ludzi " + men + ", przydzial podgladu rozny od bitwy: "
                         + diffSlots + " slotow u " + diffMen + " ludzi (bez losowej broni bezbronnych DTE); po strazach wystawienia pustych " + r.Missing
                         + (r.Missing > 0 ? " (" + KitGaps.KindsOf(r.Short, false) + ")" : "") + ", pieszo " + r.Foot + "; czas " + r.Ms + " ms"
                         + (r.Ms > 50 ? " - PONAD 50 ms (do decyzji: klucz MCM)" : "") + ".");
            }
            catch { }
        }

        /// <summary>K15 (GarrisonKit.BattleLine, przed KitGaps.Close): podglad tej misji wobec tego, co zebral OwnKitSpawn - u wystawionych, pierwszych N
        /// kazdego typu (DTE wystawia pierwszy wolny przydzial typu). Pusty, gdy podgladu w tej misji nie bylo.</summary>
        internal static string BattleSegment()
        {
            try
            {
                if (_mission == null) return _missionNote != null ? " | podglad w misji (133c): " + _missionNote : "";
                var cur = KitGaps.CurrentTypes();
                if (cur.Count == 0) return " | podglad w misji (133c): nikt z Twoich nie wyszedl - bez porownania; czas podgladu " + _mission.Ms + " ms";
                int men = 0, pvMiss = 0, btMiss = 0, pvFoot = 0, btFoot = 0, diffTypes = 0, outside = 0;
                var pvS = new int[KitGaps.Cats];
                var btS = new int[KitGaps.Cats];
                var diffs = new List<string>();
                foreach (var row in cur)
                {
                    List<Man> l;
                    if (_mission.PerMan == null || !_mission.PerMan.TryGetValue(row.Id, out l)) { outside += row.Men; continue; }
                    int n = Math.Min(row.Men, l.Count);
                    if (row.Men > l.Count) outside += row.Men - l.Count;
                    int pm = 0, pf = 0;
                    for (int k = 0; k < n; k++)
                    {
                        pm += l[k].Missing;
                        if (l[k].Foot || l[k].NoSad) pf++;
                        for (int c = 0; c < KitGaps.Cats; c++) pvS[c] += l[k].Short[c];
                    }
                    int bf = row.Foot + row.NoSaddle;
                    men += row.Men; pvMiss += pm; btMiss += row.Missing; pvFoot += pf; btFoot += bf;
                    for (int c = 0; c < KitGaps.Cats; c++) btS[c] += row.Short[c];
                    if (pm != row.Missing || pf != bf)
                    {
                        diffTypes++;
                        if (diffs.Count < 5) diffs.Add(row.Id + " x" + row.Men + " podglad " + pm + "/" + pf + ", bitwa " + row.Missing + "/" + bf);
                    }
                }
                bool same = diffTypes == 0 && outside == 0;
                return " | podglad w misji (133c, K15 - ta sama zbrojownia i wzorce, pierwsi N kazdego typu jak przy wystawieniu): ludzi " + men + ", puste podglad " + pvMiss
                       + " (" + KitGaps.KindsOf(pvS, false) + ") wobec bitwy " + btMiss + " (" + KitGaps.KindsOf(btS, false) + "), pieszo " + pvFoot + " wobec " + btFoot + " - "
                       + (same ? "ZGODNE" : "ROZNICA" + (diffTypes > 0 ? " u " + diffTypes + " typow (puste/pieszo: " + string.Join("; ", diffs.ToArray()) + ")" : "")
                                            + (outside > 0 ? ", poza podgladem " + outside + " ludzi" : ""))
                       + "; przydzial przed wystawieniem rozny w " + _mDiffSlots + " slotach u " + _mDiffMen + " z " + _mMen + " ludzi; czas podgladu " + _mission.Ms + " ms";
            }
            catch (Exception e) { Stumble("BattleSegment", e); return " | podglad w misji (133c): blad (potkniecie)"; }
        }
    }
}
