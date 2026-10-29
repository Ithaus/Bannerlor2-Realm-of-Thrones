using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P1 "marsz-doktryny" (projekt ai-marsz-taktyki.json B/C; klucze CultureDoctrines, ShootBeforeAttacking, ShootFirstSeconds, CavalrySeeksFlanks,
    /// ArchersFallBackBehindInfantry, ArcherFallBackDistance). Jeff 25.10: "zastosuj taktyki sredniowiecznej bitwy dla armii, pod wzgledem kultury np. Dothrakowie
    /// walcza inaczej niz Lannisterowie ... jesli jedna strona widzi, ze druga nie ma lucznikow, to najpierw wykorzystuje lucznikow".
    ///
    /// KONTRAKT (sygnatury ustalone w P0):
    ///  - ApplyMissionPatches(h) - z NightHooks.FirstBattle (raz na proces); opis do linii "Latki nocy (K3)".
    ///  - Begin / Tick / End     - z BattleSense (Begin PRZED WarCouncil.Council - tablice gotowe, zanim One wola ApplyList; Tick po MarchToContact, przed BattleSound).
    ///  - ResetCounters / Activity / Append - segment 'doktryna' linii "Bitwa (NB2)" (pod RoutHold.Gate).
    ///  - Zdarzenia dla dzwieku: Doctrine.ArchersFellBack(f) i Doctrine.FlankRide(f) sa w Doctrine.Events.cs (plik integratora) - P1 je WOLA, nie pisze.
    /// Potkniecia: RoutHold.Stumble(RoutHold.SiteDoctrine, ...).
    ///
    /// DOWOD [K]: TeamAIComponent.MakeDecision (co ok. 5 s, tick druzyn) wybiera MaxBy(GetTacticWeight x 1.5 dla biezacej), zmiana tylko gdy nowa > 1.5 x biezaca.
    /// RBM w EarlyStart daje liste wedlug kultury (Embolon, splity z waga 10, RangedHarrass tylko khuzait) i mnozy RangedHarrass x1.1 (khuzait) / x0.6 (reszta),
    /// FrontalCavalryCharge x0.75. Gra: RangedHarrass = udzial strzelcow x clamp(nasz / wroga dystans, 0.5..2) - przy wrogu bez lucznikow x2.
    ///
    /// ZASADY v1:
    ///  - kultura = BasicCulture strony (kultura KROLESTWA - ta sama, ktora bierze RBM i WarCouncil; jedna zasada), Skagos ma kulture freefolk;
    ///  - bramka Tactics z WarCouncil: ponizej 30 tlum - bez doktryny; od 30 mnozniki wag i dodatki listy: RangedHarrassmentOffensive (atak i obrona - waga gry
    ///    sama spada do zera bez strzelcow), TacticCharge dla dzikich (freefolk / wildlings / skagosi); druzyna GRACZA bez doktryny, sojusznicy AI tak;
    ///  - doktryna = mnozniki wag taktyk gry (postfiksy GetTacticWeight, Priority.Last po com.rbmai): Shock (FullScaleAttack, Charge), Shot (RangedHarrass),
    ///    Horse (FrontalCavalryCharge), Hold (DefensiveLine, DefensiveEngagement, DefensiveRing, HoldChokePoint); splity RBM (waga 10) i CoordinatedRetreat
    ///    bez zmian (prefiks RBM rozbija strone);
    ///  - S1 Shoot Before Attacking: nasze strzelectwo >= 15% i wroga <= max(5%, 1/3 naszego) -> Shot x2.5 (piechota w RangedHarrass idzie ostroznie jako
    ///    zaslona - BehaviorCautiousAdvance); licznik rusza, gdy wrog podejdzie na 200 m; koniec po ShootFirstSeconds (x ShootSec kultury / 90), przy stracie
    ///    ponad polowy strzelcow albo w zwarciu - wtedy Shot x0.3 do konca bitwy i MakeDecision przechodzi na glowny atak (histereza gry x1.5);
    ///  - druzyny DynamicReinforcements (powstaja w trakcie bitwy z kopia taktyk) - WarCouncil.One + doktryna przy pierwszym takcie, w ktorym je widac;
    ///  - postfiksy (tick druzyn) tylko czytaja tablice float pisana w OnMissionTick (glowny watek, nie biegnie rownolegle z tickiem druzyn).
    ///  - S3 Cavalry Seeks Flanks (Jeff: "kawaleria stara sie flankowac"): postfiks Attack (FullScaleAttack, RangedHarrass; po RBM, ktory w swoim postfiksie
    ///    zdejmuje konnicy BehaviorFlank gry) - lewa i prawa konnica dostaje z powrotem BehaviorFlank z waga Flank doktryny (bez doktryny 1.0, jak gra);
    ///    BehaviorFlank gry dziala tylko, gdy wrog stoi frontem do kogos innego (zwiazany). Przy Flank >= 1.2 i lucznikach wroga >= 5%
    ///    RBMBehaviorCavalryCharge.ChargeInfantry = false - najpierw lucznicy (bez nich RBM sam bierze najblizszy duzy szyk). Zdarzenie FlankRide, gdy
    ///    konnica AI naprawde wchodzi w BehaviorFlank (glowny watek, raz na wejscie);
    ///  - S4 Archers Fall Back Behind Infantry: szyk lucznikow AI, ktory sie ostrzeliwuje (aktywna waga ktoregos ze skirmish), gdy piechota albo konnica wroga
    ///    podejdzie na ArcherFallBackDistance, a nasza piechota glowna liczy co najmniej polowe lucznikow - BehaviorScreenedSkirmish 2, reszta wag 0 (zapisane);
    ///    powrot ponad dystans + 25 m (zapisane wagi wracaja, chyba ze taktyka przestawila je sama); co 1 s, glowny watek; taktyka, ktora przestawi wagi
    ///    (zmiana fazy) - przejmujemy od nowa, gdy dalej sie ostrzeliwuje, inaczej puszczamy.
    ///    Recenzja 25.10: kopia wag WEDLUG OBIEKTU zachowania (para zachowanie - waga), nie wedlug pozycji - FormationAI.FindBestBehavior (co 0.5 s)
    ///    przenosi wybrane zachowanie na indeks 0 listy _behaviors (Remove + Insert(0)), wiec po przejeciu kolejnosc listy jest inna niz przy zapisie.
    ///  - S3 i S4 to v1.1 w ZAKRESIE krytyka (noc/projekty/krytyk.txt:253) - kod jest, klucze domyslnie NIE (wlacza Jeff albo integrator w MCM); 124 (noc-final): domyslnie TAK.
    ///  - RangedHarrass dodany przez nas (nie lista RBM khuzait) liczy sie od 10% strzelcow druzyny (ZAKRES v1 "RangedHarrass na liscie od 10% strzelcow"):
    ///    progu nie da sie sprawdzic w ApplyList (AfterStart - ludzi jeszcze nie ma, rozstawienie wystawia ich w OnSetupTeamsOfSide,
    ///    BattleDeploymentMissionController.cs:30-44, TeamQuerySystem.RangedRatio = 0), wiec postfiks wagi daje 0, gdy strzelcow ponizej 10%.
    /// v1.1 (NIE w tej wersji): Rush (bieg dzikich z daleka / Nieskalani od 45 m), specjalne taktyki RBM z doktryny
    /// (Embolon, SplitArchers od 100 - decyzja Jeffa). S2 (obronca pod deszczem strzal) - 141, Doctrine.S2.cs (klucz DefendersAdvanceUnderFire).
    /// </summary>
    internal static partial class Doctrine
    {
        internal const int HordeBelow = 30;
        internal const float S1OursMin = 0.15f, S1EnemyFloor = 0.05f, S1Boost = 2.5f, S1Fade = 0.3f, S1Range = 200f, S1ContactGap = 25f, BaseShootSec = 90f;
        internal const float TickEvery = 2f;
        private const int MaxTeams = 16, K = 4;   // 0 Shock, 1 Shot, 2 Horse, 3 Hold
        private const int KShock = 0, KShot = 1, KHorse = 2, KHold = 3;

        private sealed class Profile
        {
            public string Name;
            public float Shock = 1f, Shot = 1f, Horse = 1f, Hold = 1f, Flank = 1f, ShootSec = BaseShootSec;
            public bool Charge;
            public Profile(string name, float shock, float shot, float horse, float hold, float flank, float shootSec = BaseShootSec, bool charge = false)
            {
                Name = name; Shock = shock; Shot = shot; Horse = horse; Hold = hold; Flank = flank; ShootSec = shootSec; Charge = charge;
            }
        }

        private static readonly Profile Neutral = new Profile("neutral", 1f, 1f, 1f, 1f, 1f);
        private static Dictionary<string, Profile> _profiles;

        /// <summary>Profile kultur ROT (kanon + sklad drzew ROT-Troops; propozycja do oceny Jeffa). Kolejnosc: Shock, Shot, Horse, Hold, Flank.</summary>
        private static Dictionary<string, Profile> Profiles()
        {
            if (_profiles != null) return _profiles;
            var d = new Dictionary<string, Profile>(StringComparer.Ordinal);
            d["vlandia"] = new Profile("Westerlands", 1.0f, 1.0f, 1.2f, 1.0f, 1.1f);
            d["battania"] = new Profile("The North", 1.15f, 0.9f, 1.0f, 1.1f, 1.1f);
            d["river"] = new Profile("Riverlands", 1.0f, 1.15f, 1.0f, 1.3f, 1.0f);
            d["vale"] = new Profile("The Vale", 0.9f, 1.0f, 1.4f, 1.0f, 1.3f);
            d["reach"] = new Profile("The Reach", 1.0f, 0.9f, 1.3f, 1.0f, 1.3f);
            d["stormlands"] = new Profile("Stormlands", 1.2f, 1.0f, 1.1f, 0.8f, 1.0f);
            d["dragonstone"] = new Profile("Dragonstone", 1.0f, 1.0f, 1.0f, 1.0f, 1.3f);
            d["crownlands"] = new Profile("Crownlands", 1.0f, 1.2f, 0.8f, 1.3f, 1.0f);
            d["aserai"] = new Profile("Dorne", 0.8f, 1.3f, 1.0f, 1.0f, 1.4f, 120f);
            d["sturgia"] = new Profile("Iron Islands", 1.3f, 0.7f, 0.5f, 0.7f, 1.0f);
            var wild = new Profile("Free Folk", 1.4f, 0.8f, 1.0f, 0.6f, 1.0f, BaseShootSec, true);
            d["freefolk"] = wild; d["wildlings"] = wild; d["skagosi"] = wild;
            d["nightswatch"] = new Profile("Night's Watch", 0.8f, 1.4f, 1.0f, 1.5f, 1.0f);
            d["khuzait"] = new Profile("Dothraki", 1.0f, 1.3f, 1.3f, 0.3f, 1.5f);
            d["empire"] = new Profile("Braavos", 1.0f, 1.1f, 1.0f, 1.1f, 1.0f);
            d["nord"] = new Profile("Lorath", 1.1f, 1.0f, 1.0f, 1.2f, 1.0f);
            d["volantine"] = new Profile("Volantis", 1.0f, 1.0f, 1.2f, 1.1f, 1.0f);
            d["ghiscari"] = new Profile("Slaver's Bay", 1.0f, 1.0f, 0.5f, 1.3f, 1.0f);
            d["qartheen"] = new Profile("Qarth", 1.0f, 1.1f, 1.0f, 1.4f, 1.0f);
            d["yiti"] = new Profile("Yi Ti", 1.0f, 1.3f, 1.0f, 1.2f, 1.0f);
            d["sarnor"] = new Profile("Sarnor", 1.0f, 1.0f, 1.3f, 1.0f, 0.8f);
            d["ibbenese"] = new Profile("Ib", 1.3f, 0.8f, 1.0f, 1.0f, 1.0f);
            d["summer"] = new Profile("Summer Isles", 0.7f, 1.6f, 1.0f, 1.0f, 1.0f, 150f);
            d["qohorik"] = new Profile("Qohor", 1.0f, 1.0f, 1.0f, 1.3f, 1.0f);
            d["norvos"] = new Profile("Norvos", 1.2f, 1.0f, 1.0f, 1.0f, 1.0f);
            d["myrish"] = new Profile("Myr", 1.0f, 1.3f, 1.0f, 1.0f, 1.0f);
            var free = new Profile("Free City", 1.0f, 1.0f, 1.0f, 1.1f, 1.0f);
            d["pentoshi"] = free; d["lyseni"] = new Profile("Lys", 1.0f, 1.0f, 1.0f, 1.1f, 1.0f); d["tyroshi"] = new Profile("Tyrosh", 1.0f, 1.0f, 1.0f, 1.1f, 1.0f);
            d["valyrian"] = new Profile("Targaryen host", 1.0f, 1.0f, 1.2f, 1.1f, 1.0f);
            _profiles = d;
            return d;
        }

        private static Profile Find(string culture)
        {
            Profile p;
            if (culture != null && Profiles().TryGetValue(culture, out p)) return p;
            return null;
        }

        // ---- stan czytany przez postfiksy (tick druzyn); zapis tylko z glownego watku
        private static volatile bool _on;
        private static readonly float[] _mul = new float[MaxTeams * K];
        private static readonly Team[] _teamRef = new Team[MaxTeams];

        // ---- stan glownego watku per druzyna
        private static readonly Team[] _seen = new Team[MaxTeams];
        private static readonly Profile[] _prof = new Profile[MaxTeams];
        private static readonly bool[] _doct = new bool[MaxTeams];       // doktryna wpieta (Tactics >= 30, druzyna AI)
        private static readonly int[] _tacOf = new int[MaxTeams];
        private static readonly string[] _culOf = new string[MaxTeams];
        private static readonly byte[] _s1 = new byte[MaxTeams];         // 0 nie, 1 ostrzal, 2 po ostrzale
        private static readonly float[] _s1Start = new float[MaxTeams];  // pierwsza chwila stanu 1
        private static readonly float[] _s1Clock = new float[MaxTeams];  // start licznika (wrog na 200 m), -1 = jeszcze nie
        private static readonly float[] _s1Ranged = new float[MaxTeams]; // strzelcow na starcie ostrzalu
        private static readonly TacticComponent[] _cur = new TacticComponent[MaxTeams];
        private static readonly StringBuilder[] _hist = new StringBuilder[MaxTeams];
        private static WeakReference _m;
        private static float _acc;

        // ---- latki
        private static bool _tried;
        private static int _patched, _wanted;
        private static AccessTools.FieldRef<TeamAIComponent, TacticComponent> _fCur;

        // ---- S3 konnica na flanke (postfiks Attack, tick druzyn) i S4 lucznicy za piechote (glowny watek co 1 s)
        internal const float S4Back = 25f, S4Every = 1f, S3ArchersMin = 0.05f, S3HuntFrom = 1.2f, S4Weight = 2f;
        private const int Slots = MaxTeams * 10;
        private static readonly float[] _flankW = new float[MaxTeams];       // 0 = S3 wylaczone dla druzyny
        private static readonly float[] _enemyRanged = new float[MaxTeams];
        private static int _patchedAttack, _wantedAttack;
        private static AccessTools.FieldRef<TacticComponent, Formation> _fLeftCav, _fRightCav;
        private static AccessTools.FieldRef<FormationAI, List<BehaviorComponent>> _fBehaviors;
        private static Type _tRbmCavCharge, _tRbmArcher;
        private static FieldInfo _fChargeInf;
        private static readonly Formation[] _sWho = new Formation[Slots];
        private static readonly bool[] _sBack = new bool[Slots];
        private static readonly BehaviorComponent[][] _sSnapB = new BehaviorComponent[Slots][];   // kopia wag wedlug obiektu: zachowanie ...
        private static readonly float[][] _sSnap = new float[Slots][];                            // ... i jego waga (ten sam indeks pary)
        private static readonly bool[] _sFlank = new bool[Slots];
        private static float _acc1;
        private static int _s3Set, _s3Hunt, _s3NoBehavior;                    // Interlocked (tick druzyn)
        private static int _s3Rides, _s4Back, _s4Return, _s4Lost, _s4NoScreen, _s4Other;   // pod Gate

        // ---- prog RangedHarrass dodanego przez nas (postfiks wagi, tick druzyn czyta; zapis tylko z glownego watku w ApplyList / Clear)
        internal const float RhMinRanged = 0.10f;
        private static readonly bool[] _shotGate = new bool[MaxTeams];
        private static int _rhGated;                                           // Interlocked (tick druzyn)

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _teams, _horde, _noProfile, _late, _addShot, _addCharge, _s1On, _s1Limit, _s1Loss, _s1Contact, _s1Back, _switches, _stum;
        private static string _sampleA, _sampleD, _histA, _histD;
        private static int _sampleTeamA = -1, _sampleTeamD = -1;
        private static int _calls, _postErr;

        // ------------------------------------------------------------ latki (raz na proces, pierwsza bitwa)

        internal static string ApplyMissionPatches(Harmony h)
        {
            if (_tried) return "juz wpiete";
            _tried = true;
            try { _fCur = AccessTools.FieldRefAccess<TeamAIComponent, TacticComponent>("_currentTactic"); } catch { _fCur = null; }
            var miss = new StringBuilder(80);
            Patch(h, typeof(TacticFullScaleAttack), nameof(PostShock), miss);
            Patch(h, typeof(TacticCharge), nameof(PostShock), miss);
            Patch(h, typeof(TacticRangedHarrassmentOffensive), nameof(PostShot), miss);
            Patch(h, typeof(TacticFrontalCavalryCharge), nameof(PostHorse), miss);
            Patch(h, typeof(TacticDefensiveLine), nameof(PostHold), miss);
            Patch(h, typeof(TacticDefensiveEngagement), nameof(PostHold), miss);
            Patch(h, typeof(TacticDefensiveRing), nameof(PostHold), miss);
            Patch(h, typeof(TacticHoldChokePoint), nameof(PostHold), miss);
            // S3: konnica skrzydel z pol bazowych TacticComponent; typy RBM po nazwie (brak = pominiete i policzone)
            try { _fLeftCav = AccessTools.FieldRefAccess<TacticComponent, Formation>("_leftCavalry"); _fRightCav = AccessTools.FieldRefAccess<TacticComponent, Formation>("_rightCavalry"); }
            catch { _fLeftCav = _fRightCav = null; }
            try { _fBehaviors = AccessTools.FieldRefAccess<FormationAI, List<BehaviorComponent>>("_behaviors"); } catch { _fBehaviors = null; }
            try { _tRbmCavCharge = AccessTools.TypeByName("RBMAI.RBMBehaviorCavalryCharge") ?? AccessTools.TypeByName("RBMBehaviorCavalryCharge"); } catch { _tRbmCavCharge = null; }
            try { _tRbmArcher = AccessTools.TypeByName("RBMAI.RBMBehaviorArcherSkirmish") ?? AccessTools.TypeByName("RBMBehaviorArcherSkirmish"); } catch { _tRbmArcher = null; }
            if (_tRbmCavCharge != null && !typeof(BehaviorComponent).IsAssignableFrom(_tRbmCavCharge)) _tRbmCavCharge = null;
            if (_tRbmArcher != null && !typeof(BehaviorComponent).IsAssignableFrom(_tRbmArcher)) _tRbmArcher = null;
            try { _fChargeInf = _tRbmCavCharge != null ? AccessTools.Field(_tRbmCavCharge, "ChargeInfantry") : null; } catch { _fChargeInf = null; }
            if (_fChargeInf != null && _fChargeInf.FieldType != typeof(bool)) _fChargeInf = null;
            var missA = new StringBuilder(60);
            if (_fLeftCav != null && _fRightCav != null)
            {
                PatchAttack(h, typeof(TacticFullScaleAttack), missA);
                PatchAttack(h, typeof(TacticRangedHarrassmentOffensive), missA);
            }
            else missA.Append(" brak pol konnicy");
            string def291d = DhPatchDefense(h);   // 291d: obrona gry w trzymaniu (Doctrine.Hold291.cs)
            return "GetTacticWeight " + _patched + "/" + _wanted + " (Priority.Last po com.rbmai" + (miss.Length > 0 ? "; NIE:" + miss.ToString() : "")
                   + "), Attack " + _patchedAttack + "/" + _wantedAttack + (missA.Length > 0 ? " (NIE:" + missA.ToString() + ")" : "")
                   + ", biezaca taktyka " + (_fCur != null ? "TAK" : "NIE") + ", typy RBM: RBMBehaviorCavalryCharge " + (_fChargeInf != null ? "TAK" : "NIE")
                   + ", RBMBehaviorArcherSkirmish " + (_tRbmArcher != null ? "TAK" : "NIE") + ", wagi zachowan " + (_fBehaviors != null ? "TAK" : "NIE") + ", obrona gry 291d " + def291d;
        }

        private static void PatchAttack(Harmony h, Type t, StringBuilder miss)
        {
            _wantedAttack++;
            try
            {
                var target = AccessTools.DeclaredMethod(t, "Attack");
                if (target == null) { miss.Append(' ').Append(t.Name); return; }
                h.Patch(target, postfix: new HarmonyMethod(typeof(Doctrine), nameof(PostAttack)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                bool ok = false;
                var info = Harmony.GetPatchInfo(target);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(Doctrine)) ok = true;
                if (ok) _patchedAttack++; else miss.Append(' ').Append(t.Name);
            }
            catch (Exception e)
            {
                miss.Append(' ').Append(t.Name).Append('(').Append(e.GetType().Name).Append(')');
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.LatkaAttack " + t.Name, e);
            }
        }

        /// <summary>S3 (tick druzyn, przy zmianie fazy taktyki na atak - po postfiksie RBM): konnica skrzydel z BehaviorFlank wedlug doktryny.</summary>
        private static void PostAttack(object __instance)
        {
            if (!_on) return;
            try
            {
                var tc = __instance as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_teamRef[ti], team)) return;
                float w = _flankW[ti];
                if (w <= 0f) return;
                bool hunt = w >= S3HuntFrom && _enemyRanged[ti] >= S3ArchersMin;
                var lf = _fLeftCav; var rf = _fRightCav;
                if (lf != null) Flank(lf(tc), w, hunt);
                if (rf != null) Flank(rf(tc), w, hunt);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        private static void Flank(Formation f, float w, bool hunt)
        {
            if (f == null || f.CountOfUnits <= 0 || !f.IsAIControlled) return;
            var ai = f.AI;
            if (ai == null) return;
            var b = ai.GetBehavior<BehaviorFlank>();
            if (b == null) { Interlocked.Increment(ref _s3NoBehavior); return; }
            b.WeightFactor = w;
            Interlocked.Increment(ref _s3Set);
            if (!hunt || _fChargeInf == null || _fBehaviors == null) return;
            var lst = _fBehaviors(ai);
            if (lst == null) return;
            for (int i = 0; i < lst.Count; i++)
            {
                var x = lst[i];
                if (x != null && x.GetType() == _tRbmCavCharge) { _fChargeInf.SetValue(x, false); Interlocked.Increment(ref _s3Hunt); }
            }
        }

        private static void Patch(Harmony h, Type t, string post, StringBuilder miss)
        {
            _wanted++;
            try
            {
                var target = AccessTools.DeclaredMethod(t, "GetTacticWeight");
                if (target == null) { miss.Append(' ').Append(t.Name); return; }
                h.Patch(target, postfix: new HarmonyMethod(typeof(Doctrine), post) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                bool ok = false;
                var info = Harmony.GetPatchInfo(target);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(Doctrine)) ok = true;
                if (ok) _patched++; else miss.Append(' ').Append(t.Name);
            }
            catch (Exception e)
            {
                miss.Append(' ').Append(t.Name).Append('(').Append(e.GetType().Name).Append(')');
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Latka " + t.Name, e);
            }
        }

        // 291: DhMul - obronca trzyma pozycje (wagi ataku 0, Doctrine.Hold291.cs); DhNote - ostatnia waga obrony druzyny obroncy
        private static void PostShock(object __instance, ref float __result) { Mul(__instance, KShock, ref __result); DhMul(__instance, ref __result); }
        private static void PostShot(object __instance, ref float __result) { Mul(__instance, KShot, ref __result); DhMul(__instance, ref __result); }
        private static void PostHorse(object __instance, ref float __result) { Mul(__instance, KHorse, ref __result); DhMul(__instance, ref __result); }
        private static void PostHold(object __instance, ref float __result) { Mul(__instance, KHold, ref __result); S2Mul(__instance, ref __result); DhNote(__instance, __result); }   // 141: S2 (Doctrine.S2.cs)

        /// <summary>Postfiks GetTacticWeight (tick druzyn): waga x mnoznik doktryny druzyny. Tylko odczyt tablicy, bez alokacji.</summary>
        private static void Mul(object inst, int k, ref float r)
        {
            if (!_on) return;
            try
            {
                var tc = inst as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_teamRef[ti], team)) return;
                if (k == KShot && _shotGate[ti] && r > 0f)
                {
                    // RangedHarrass z naszej listy: od 10% strzelcow (ta sama miara co gra w tej wadze - QuerySystem druzyny, w tym samym wywolaniu)
                    var q = team.QuerySystem;
                    if (q != null && q.RangedRatio + q.RangedCavalryRatio < RhMinRanged) { r = 0f; Interlocked.Increment(ref _rhGated); return; }
                }
                float f = _mul[ti * K + k];
                if (f == 1f || float.IsNaN(r) || float.IsInfinity(r)) return;
                r *= f;
                Interlocked.Increment(ref _calls);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ misja

        internal static void Begin(Mission m)
        {
            Clear();
            S2ResetCounters(m);   // 141: liczniki linii "S2 (141)" od zera przed WarCouncil.Council (ApplyList liczy druzyny S2)
            _m = m != null ? new WeakReference(m) : null;
            _acc = 0f;
            // druzyny obecne przy AfterStart obsluguje WarCouncil.Council (zaraz po nas); pozniejsze (DR) - Tick
            try
            {
                var teams = m != null ? m.Teams : null;
                int n = teams != null ? teams.Count : 0;
                for (int i = 0; i < n; i++)
                {
                    var t = teams[i];
                    if (t == null) continue;
                    int ti = t.TeamIndex;
                    if (ti >= 0 && ti < MaxTeams) _seen[ti] = t;
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Begin druzyny", e); }
            var c = Settings.Current;
            _on = AnyKey(c) && m != null && m.IsFieldBattle && _patched + _patchedAttack > 0;
        }

        /// <summary>283 (HorsePursuit.ReactHalt, glowny watek): szyk trzymany teraz przez S4 (lucznicy za piechota - wagi zachowan przejete) - postoj 283 go
        /// nie bierze i puszcza swoj, gdy S4 wejdzie (dwa przejecia wag jednego szyku nigdy naraz).</summary>
        internal static bool S4Held(Formation f)
        {
            if (f == null) return false;
            for (int i = 0; i < Slots; i++)
                if (_sBack[i] && ReferenceEquals(_sWho[i], f)) return true;
            return false;
        }

        /// <summary>283 (HorsePursuit.ReactHalt, glowny watek): S2 (141) dziala dla druzyny - obronca idzie pod strzalami do zwarcia; armia nie staje (R5).</summary>
        internal static bool S2On(Team t)
        {
            if (t == null) return false;
            int ti = t.TeamIndex;
            return ti >= 0 && ti < MaxTeams && _s2Live[ti] && ReferenceEquals(_s2Ref[ti], t);
        }

        /// <summary>Ktorykolwiek klucz doktryny i manewrow (WarCouncil.Council biegnie takze przy CommanderTactics NIE, gdy ten jest TAK).</summary>
        internal static bool AnyKey(Settings c)
        {
            return c != null && (c.CultureDoctrines || c.ShootBeforeAttacking || c.CavalrySeeksFlanks || c.ArchersFallBackBehindInfantry
                                 || c.DefendersAdvanceUnderFire     // 141: S2 sam tez potrzebuje rady wojennej (ApplyList) i ticku doktryny
                                 || c.DefendersHoldTheirGround);    // 291: obronca trzyma pozycje - tak samo (Doctrine.Hold291.cs)
        }

        internal static void End(Mission m)
        {
            _on = false;
            // historia taktyk probek do linii NB2 (RoutKeeper moze ja pisac po nas) - potem stan bez druzyn (skonczona misja nie wisi w pamieci)
            try { lock (RoutHold.Gate) { if (_sampleTeamA >= 0) _histA = Hist(_sampleTeamA); if (_sampleTeamD >= 0) _histD = Hist(_sampleTeamD); } } catch { }
            // 141: linia "S2 (141)" raz na misje (przed Clear - stan druzyn; _m zerowane nizej, drugie wywolanie End nic nie pisze)
            try { S2End(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.S2End", e); }
            try { DhEnd(); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.DhEnd", e); }   // 291: probka do linii 'Bitwa (291)'
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            _on = false;
            for (int i = 0; i < MaxTeams; i++)
            {
                for (int k = 0; k < K; k++) _mul[i * K + k] = 1f;
                _teamRef[i] = null; _seen[i] = null; _prof[i] = null; _doct[i] = false; _tacOf[i] = 0; _culOf[i] = null;
                _s1[i] = 0; _s1Start[i] = 0f; _s1Clock[i] = -1f; _s1Ranged[i] = 0f; _cur[i] = null; _hist[i] = null;
                _flankW[i] = 0f; _enemyRanged[i] = 0f; _shotGate[i] = false;
                S2ClearTeam(i);   // 141
                DhClearTeam(i);   // 291
            }
            for (int i = 0; i < Slots; i++) { _sWho[i] = null; _sBack[i] = false; Drop(i); _sFlank[i] = false; }
            _sampleTeamA = _sampleTeamD = -1;
            _acc1 = 0f;
        }

        /// <summary>WarCouncil.One (glowny watek, AfterStart albo pozna druzyna DR): doktryna druzyny AI i dodatki do listy taktyk. have/note - lista typow
        /// i opis WarCouncil (bez dubli). Zwraca krotki opis do probki.</summary>
        internal static void ApplyList(Mission m, Team team, int tac, string culture, List<Type> have, StringBuilder note, bool late)
        {
            var c = Settings.Current;
            if (c == null || team == null || !AnyKey(c)) return;
            // tylko misja, dla ktorej biegl Begin (BattleSense: bitwa, nie pojedynek - takze nie flaga testu "bitwa jako pojedynek")
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            int ti = team.TeamIndex;
            if (ti < 0 || ti >= MaxTeams) return;
            _seen[ti] = team;
            _tacOf[ti] = tac;
            _culOf[ti] = culture;
            if (tac < HordeBelow)
            {
                S2Register(team, ti, have, late);   // 141: S2 to zasada przetrwania, nie doktryna - tlum tez (lista po radzie)
                DhRegister(team, ti, have, c);      // 291: obronca trzyma pozycje - tlum tez
                lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _teams++; _horde++; if (late) _late++; }
                return;
            }
            Profile p = c.CultureDoctrines ? Find(culture) : null;
            bool noProfile = c.CultureDoctrines && p == null;
            if (p == null) p = Neutral;
            _prof[ti] = p;
            _doct[ti] = true;
            _hist[ti] = new StringBuilder(120);
            int addShot = 0, addCharge = 0;
            // RangedHarrass (atak i obrona): bez strzelcow waga gry = 0 (TacticRangedHarrassmentOffensive.GetTacticWeight), wiec dodanie jest bezpieczne
            if ((c.CultureDoctrines || c.ShootBeforeAttacking) && WarCouncil.TryAdd(team, have, typeof(TacticRangedHarrassmentOffensive), note)) addShot++;
            if (c.CultureDoctrines && p.Charge && WarCouncil.TryAdd(team, have, typeof(TacticCharge), note)) addCharge++;
            // prog 10% strzelcow tylko dla RangedHarrass dodanego tutaj (lista RBM khuzait bez zmian); zapis przed _teamRef - postfiks widzi calosc
            _shotGate[ti] = addShot > 0;
            SetBase(ti, c);
            _teamRef[ti] = team;
            S2Register(team, ti, have, late);   // 141: po dodatkach doktryny (+Charge dzikich) - lista taktyk ataku pelna
            DhRegister(team, ti, have, c);      // 291
            var s = new StringBuilder(160);
            s.Append('T').Append(tac).Append('/').Append(culture ?? "?").Append(' ').Append(p.Name)
             .Append(" (Shock ").Append(F(p.Shock)).Append(", Shot ").Append(F(p.Shot)).Append(", Horse ").Append(F(p.Horse)).Append(", Hold ").Append(F(p.Hold)).Append(')');
            if (addShot > 0) s.Append(" +RangedHarrass");
            if (addCharge > 0) s.Append(" +Charge");
            if (late) s.Append(" (DR)");
            bool att = team.Side == BattleSideEnum.Attacker;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _teams++;
                if (noProfile) _noProfile++;
                if (late) _late++;
                _addShot += addShot;
                _addCharge += addCharge;
                if (att) { if (_sampleA == null) { _sampleA = s.ToString(); _sampleTeamA = ti; } }
                else if (_sampleD == null) { _sampleD = s.ToString(); _sampleTeamD = ti; }
            }
            if (m != null && m.IsFieldBattle && _patched + _patchedAttack > 0) _on = true;
        }

        private static string F(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        private static void SetBase(int ti, Settings c)
        {
            var p = _prof[ti] ?? Neutral;
            bool d = c.CultureDoctrines;
            _mul[ti * K + KShock] = d ? p.Shock : 1f;
            _mul[ti * K + KShot] = d ? p.Shot : 1f;
            _mul[ti * K + KHorse] = d ? p.Horse : 1f;
            _mul[ti * K + KHold] = d ? p.Hold : 1f;
            _flankW[ti] = c.CavalrySeeksFlanks ? (d ? p.Flank : 1f) : 0f;
        }

        internal static void Tick(Mission m, float dt)
        {
            if (m == null) return;
            _acc += dt;
            _acc1 += dt;
            if (_acc1 >= S4Every)
            {
                _acc1 = 0f;
                try { Manoeuvres(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Manewry", e); }
            }
            if (_acc < TickEvery) return;
            _acc = 0f;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;   // Begin nie biegl dla tej misji - nic
            var c = Settings.Current;
            if (c == null || !m.IsFieldBattle || m.MissionEnded) return;
            bool keys = AnyKey(c);
            _on = keys && _patched + _patchedAttack > 0;
            if (!keys) return;
            float now = m.CurrentTime;
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                try
                {
                    if (!ReferenceEquals(_seen[ti], t))
                    {
                        // pozna druzyna (DynamicReinforcements) - rada wojenna i doktryna przy pierwszym takcie, w ktorym ja widac
                        _seen[ti] = t;
                        WarCouncil.LateTeam(m, t);
                    }
                    if (_doct[ti] && ReferenceEquals(_teamRef[ti], t)) TeamTick(m, t, ti, c, now);
                }
                catch (Exception e) { _stumLocal++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Druzyna", e); }
                // 141: S2 obronca pod ostrzalem - wlasny try (potkniecie doktryny nie gasi S2 i odwrotnie), po rozstawieniu
                if (ReferenceEquals(_s2Ref[ti], t) && m.IsDeploymentFinished)
                {
                    try { S2Tick(m, t, ti, c, now); }
                    catch (Exception e) { _s2Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.S2", e); }
                }
                // 291: obronca trzyma pozycje - wlasny try, po S2 (stan S2 tego taktu)
                if (ReferenceEquals(_dhRef[ti], t))
                {
                    try { DhTick(m, t, ti, c, now); }
                    catch (Exception e) { _dhStum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hold291", e); }
                }
            }
            if (_stumLocal > 0) { lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _stum += _stumLocal; } _stumLocal = 0; }
        }

        private static int _stumLocal;

        private static void TeamTick(Mission m, Team t, int ti, Settings c, float now)
        {
            SetBase(ti, c);
            var p = _prof[ti] ?? Neutral;
            float baseShot = c.CultureDoctrines ? p.Shot : 1f;
            var q = t.QuerySystem;
            if (q != null) { try { _enemyRanged[ti] = q.EnemyRangedRatio; } catch { _enemyRanged[ti] = 0f; } }
            if (q != null && c.ShootBeforeAttacking && m.IsDeploymentFinished)
            {
                float ours = q.RangedRatio + q.RangedCavalryRatio;
                float enemy = q.EnemyRangedRatio + q.EnemyRangedCavalryRatio;
                int members = q.MemberCount;
                float rangedNow = ours * members;
                byte st = _s1[ti];
                if (st == 0)
                {
                    if (ours >= S1OursMin && enemy <= Math.Max(S1EnemyFloor, ours / 3f))
                    {
                        st = 1;
                        _s1Start[ti] = now;
                        _s1Clock[ti] = -1f;
                        _s1Ranged[ti] = rangedNow;
                        lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _s1On++; }
                        Note(ti, "ostrzal od s. ", now);
                    }
                }
                else if (st == 1)
                {
                    float gap = TeamGap(t);
                    if (_s1Clock[ti] < 0f && gap <= S1Range) _s1Clock[ti] = now;
                    float sec = Math.Max(0f, c.ShootFirstSeconds) * (p.ShootSec / BaseShootSec);
                    if (!c.CultureDoctrines) sec = Math.Max(0f, c.ShootFirstSeconds);
                    int why = 0;
                    if (_s1Clock[ti] >= 0f && now - _s1Clock[ti] >= sec) why = 1;
                    else if (_s1Ranged[ti] > 0f && rangedNow < 0.5f * _s1Ranged[ti]) why = 2;
                    else if (gap <= S1ContactGap) why = 3;
                    else if (enemy > Math.Max(S1EnemyFloor, ours / 3f) * 1.5f) why = 4;   // wrog jednak ma lucznikow (posilki) - bez wygaszenia
                    if (why > 0)
                    {
                        st = why == 4 ? (byte)0 : (byte)2;
                        lock (RoutHold.Gate)
                        {
                            RoutHold.EnsureLocked(m);
                            if (why == 1) _s1Limit++; else if (why == 2) _s1Loss++; else if (why == 3) _s1Contact++; else _s1Back++;
                        }
                        Note(ti, why == 1 ? "ostrzal koniec (limit) s. " : (why == 2 ? "ostrzal koniec (straty) s. " : (why == 3 ? "ostrzal koniec (zwarcie) s. " : "ostrzal cofniety s. ")), now);
                    }
                }
                _s1[ti] = st;
                if (st == 1) _mul[ti * K + KShot] = baseShot * S1Boost;
                else if (st == 2) _mul[ti * K + KShot] = baseShot * S1Fade;
            }
            // wybor taktyki gry (do linii: zmiany biezacej taktyki)
            if (_fCur != null)
            {
                TacticComponent cur = null;
                try { var ai = t.TeamAI; if (ai != null) cur = _fCur(ai); } catch { cur = null; }
                if (cur != null && !ReferenceEquals(cur, _cur[ti]))
                {
                    _cur[ti] = cur;
                    string nm = cur.GetType().Name;
                    if (nm.StartsWith("Tactic", StringComparison.Ordinal)) nm = nm.Substring(6);
                    var h = _hist[ti];
                    if (h != null && h.Length < 300) h.Append(h.Length > 0 ? ", " : "").Append(nm).Append(" s. ").Append(((int)now).ToString());
                    lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _switches++; }
                }
            }
        }

        private static void Note(int ti, string what, float now)
        {
            var h = _hist[ti];
            if (h != null && h.Length < 300) h.Append(h.Length > 0 ? ", " : "").Append(what).Append(((int)now).ToString());
        }

        /// <summary>Najmniejsza odleglosc od szyku druzyny do najblizszego szyku wroga (mediana - mediana minus polowy glebokosci).</summary>
        private static float TeamGap(Team t)
        {
            float best = float.MaxValue;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || f.CountOfUnits <= 0) continue;
                var eq = f.CachedClosestEnemyFormation;
                var ef = eq != null ? eq.Formation : null;
                if (ef == null || ef.CountOfUnits <= 0) continue;
                float d = f.CachedMedianPosition.AsVec2.Distance(ef.CachedMedianPosition.AsVec2) - 0.5f * (f.Depth + ef.Depth);
                if (d < best) best = d;
            }
            return best < 0f ? 0f : best;
        }

        // ------------------------------------------------------------ S3 zdarzenie flanki, S4 lucznicy za piechote (glowny watek co 1 s)

        private static void Manoeuvres(Mission m)
        {
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            var c = Settings.Current;
            if (c == null || !m.IsFieldBattle || m.MissionEnded || !m.IsDeploymentFinished) return;
            bool s3 = c.CavalrySeeksFlanks, s4 = c.ArchersFallBackBehindInfantry && _fBehaviors != null;
            if (!s3 && !s4) { ReleaseAllS4(); return; }
            float dist = Math.Max(20f, Math.Min(100f, c.ArcherFallBackDistance));
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !_doct[ti] || !ReferenceEquals(_teamRef[ti], t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf && j < 10; j++)
                {
                    var f = fl[j];
                    if (f == null) continue;
                    int idx = ti * 10 + j;
                    if (!ReferenceEquals(_sWho[idx], f)) { _sWho[idx] = f; _sBack[idx] = false; Drop(idx); _sFlank[idx] = false; }
                    if (f.CountOfUnits <= 0 || !f.IsAIControlled) { _sBack[idx] = false; Drop(idx); continue; }
                    try
                    {
                        var q = f.QuerySystem;
                        if (q == null) continue;
                        if (q.IsCavalryFormation)
                        {
                            bool fr = s3 && f.AI != null && f.AI.ActiveBehavior is BehaviorFlank;
                            if (fr && !_sFlank[idx])
                            {
                                lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _s3Rides++; }
                                FlankRide(f);
                            }
                            _sFlank[idx] = fr;
                            continue;
                        }
                        // lucznicy jako glowny szyk druzyny (BehaviorScreenedSkirmish da im wtedy wage 0) - bez S4
                        if (!q.IsRangedFormation || (f.AI != null && f.AI.IsMainFormation && !_sBack[idx])) continue;
                        if (!s4) { if (_sBack[idx]) Restore(m, f, idx); continue; }
                        float gap = MeleeGap(t, f);
                        bool screen = Screen(t, f);
                        if (!_sBack[idx])
                        {
                            if (gap <= dist && screen) TakeOver(m, f, idx);
                        }
                        else if (gap > dist + S4Back || !screen) Restore(m, f, idx);
                        else Keep(m, f, idx);
                    }
                    catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Szyk", e); }
                }
            }
        }

        /// <summary>Najblizszy szyk wroga walczacy wrecz (piechota albo konnica, nie strzelcy) - mediana do mediany minus polowy glebokosci.</summary>
        private static float MeleeGap(Team team, Formation f)
        {
            float best = float.MaxValue;
            var my = f.CachedMedianPosition.AsVec2;
            var teams = team.Mission != null ? team.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Enemies(team, t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits <= 0) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || eq.IsRangedFormation || eq.IsRangedCavalryFormation || !(eq.IsInfantryFormation || eq.IsCavalryFormation)) continue;
                    float d = my.Distance(e.CachedMedianPosition.AsVec2) - 0.5f * (f.Depth + e.Depth);
                    if (d < best) best = d;
                }
            }
            return best < 0f ? 0f : best;
        }

        /// <summary>Zaslona: glowny szyk piechoty druzyny (IsMainFormation - tego szuka BehaviorScreenedSkirmish) z co najmniej polowa liczby lucznikow.</summary>
        private static bool Screen(Team team, Formation archers)
        {
            var fl = team.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            for (int j = 0; j < nf; j++)
            {
                var g = fl[j];
                if (g == null || ReferenceEquals(g, archers) || g.CountOfUnits <= 0 || g.AI == null || !g.AI.IsMainFormation) continue;
                var gq = g.QuerySystem;
                if (gq != null && gq.IsInfantryFormation && g.CountOfUnits * 2 >= archers.CountOfUnits) return true;
            }
            return false;
        }

        private static bool IsSkirmish(BehaviorComponent b)
        {
            return b is BehaviorSkirmish || b is BehaviorSkirmishLine || b is BehaviorScreenedSkirmish || b is BehaviorSkirmishBehindFormation
                   || (_tRbmArcher != null && b.GetType() == _tRbmArcher);
        }

        /// <summary>Wagi to juz nasze (ScreenedSkirmish 2, reszta 0).</summary>
        private static bool Ours(List<BehaviorComponent> lst)
        {
            for (int i = 0; i < lst.Count; i++)
            {
                var b = lst[i];
                if (b == null) continue;
                if (b is BehaviorScreenedSkirmish) { if (Math.Abs(b.WeightFactor - S4Weight) > 0.001f) return false; }
                else if (b.WeightFactor > 1E-07f) return false;
            }
            return true;
        }

        /// <summary>Przejecie: tylko szyk, ktory sie ostrzeliwuje (aktywna waga skirmish) i ma BehaviorScreenedSkirmish; wagi zapisane do powrotu.
        /// 0 = przejety, 1 = brak BehaviorScreenedSkirmish, 2 = szyk sie nie ostrzeliwuje (inny rozkaz taktyki).</summary>
        private static int Apply(List<BehaviorComponent> lst, int idx)
        {
            bool screened = false, skirmish = false;
            for (int i = 0; i < lst.Count; i++)
            {
                var b = lst[i];
                if (b == null) continue;
                if (b is BehaviorScreenedSkirmish) screened = true;
                if (b.WeightFactor > 1E-07f && IsSkirmish(b)) skirmish = true;
            }
            if (!screened) return 1;
            if (!skirmish) return 2;
            // kopia wedlug obiektu (recenzja 25.10): FindBestBehavior gry przestawia kolejnosc listy (wybrane zachowanie na indeks 0)
            var who = new BehaviorComponent[lst.Count];
            var snap = new float[lst.Count];
            for (int i = 0; i < lst.Count; i++) { who[i] = lst[i]; snap[i] = lst[i] != null ? lst[i].WeightFactor : 0f; }
            _sSnapB[idx] = who;
            _sSnap[idx] = snap;
            for (int i = 0; i < lst.Count; i++)
            {
                var b = lst[i];
                if (b == null) continue;
                b.WeightFactor = b is BehaviorScreenedSkirmish ? S4Weight : 0f;
            }
            return 0;
        }

        private static void TakeOver(Mission m, Formation f, int idx)
        {
            var lst = _fBehaviors(f.AI);
            if (lst == null) return;
            int r = Apply(lst, idx);
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                if (r == 0) _s4Back++; else if (r == 1) _s4NoScreen++; else _s4Other++;
            }
            if (r != 0) return;
            _sBack[idx] = true;
            ArchersFellBack(f);
        }

        /// <summary>Szyk dalej za piechota: taktyka przestawila wagi (zmiana fazy) - przejecie od nowa, gdy dalej sie ostrzeliwuje, inaczej puszczamy.</summary>
        private static void Keep(Mission m, Formation f, int idx)
        {
            var lst = _fBehaviors(f.AI);
            if (lst == null) return;
            if (Ours(lst) && _sSnapB[idx] != null && _sSnap[idx] != null) return;
            if (Apply(lst, idx) == 0) return;
            _sBack[idx] = false;
            Drop(idx);
            lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _s4Lost++; }
        }

        private static void Restore(Mission m, Formation f, int idx)
        {
            var lst = f.AI != null ? _fBehaviors(f.AI) : null;
            PutBack(lst, idx);
            _sBack[idx] = false;
            Drop(idx);
            lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _s4Return++; }
        }

        /// <summary>Zapisane wagi wracaja do TYCH SAMYCH obiektow zachowan (ReferenceEquals), gdziekolwiek sa teraz na liscie; zachowania spoza kopii
        /// bez zmian. Tylko gdy wagi to dalej nasze (taktyka ich nie przestawila).</summary>
        private static void PutBack(List<BehaviorComponent> lst, int idx)
        {
            var who = _sSnapB[idx];
            var snap = _sSnap[idx];
            if (lst == null || who == null || snap == null || who.Length != snap.Length || !Ours(lst)) return;
            for (int k = 0; k < who.Length; k++)
            {
                var b = who[k];
                if (b == null) continue;
                for (int i = 0; i < lst.Count; i++)
                    if (ReferenceEquals(lst[i], b)) { b.WeightFactor = snap[k]; break; }
            }
        }

        private static void Drop(int idx)
        {
            _sSnapB[idx] = null;
            _sSnap[idx] = null;
        }

        /// <summary>Klucz S4 wylaczony w trakcie bitwy: zapisane wagi wracaja.</summary>
        private static void ReleaseAllS4()
        {
            for (int i = 0; i < Slots; i++)
            {
                if (!_sBack[i]) continue;
                var f = _sWho[i];
                try
                {
                    var lst = f != null && f.AI != null && _fBehaviors != null ? _fBehaviors(f.AI) : null;
                    PutBack(lst, i);
                }
                catch { }
                _sBack[i] = false;
                Drop(i);
            }
        }

        // ------------------------------------------------------------ linia misji

        internal static void ResetCounters()
        {
            _teams = _horde = _noProfile = _late = _addShot = _addCharge = _s1On = _s1Limit = _s1Loss = _s1Contact = _s1Back = _switches = _stum = 0;
            _s3Rides = _s4Back = _s4Return = _s4Lost = _s4NoScreen = _s4Other = 0;
            Interlocked.Exchange(ref _s3Set, 0);
            Interlocked.Exchange(ref _s3Hunt, 0);
            Interlocked.Exchange(ref _s3NoBehavior, 0);
            Interlocked.Exchange(ref _rhGated, 0);
            _sampleA = _sampleD = _histA = _histD = null;
            Interlocked.Exchange(ref _calls, 0);
            Interlocked.Exchange(ref _postErr, 0);
        }

        internal static int Activity { get { return _teams + _s1On + _stum + _s3Rides + _s4Back; } }

        /// <summary>Segment ' | doktryna ...' (bez znaku '|' w srodku). Wolajacy trzyma RoutHold.Gate. Historia taktyk probek czytana na koniec misji
        /// (glowny watek - Tick nie biegnie rownolegle).</summary>
        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | doktryna ").Append(c != null && c.CultureDoctrines ? "TAK" : "NIE").Append(" (najpierw ostrzal ").Append(c != null && c.ShootBeforeAttacking ? "TAK" : "NIE")
              .Append("): druzyn ").Append(_teams).Append(" (tlum ").Append(_horde).Append(", bez profilu kultury ").Append(_noProfile).Append(", pozne DR ").Append(_late)
              .Append("), lista +RangedHarrass ").Append(_addShot).Append(" (waga 0 ponizej 10% strzelcow ").Append(Volatile.Read(ref _rhGated)).Append(')')
              .Append(", +Charge ").Append(_addCharge)
              .Append("; najpierw ostrzal ").Append(_s1On).Append(" (koniec: limit czasu ").Append(_s1Limit).Append(", straty ").Append(_s1Loss)
              .Append(", zwarcie ").Append(_s1Contact).Append(", wrog jednak strzela ").Append(_s1Back).Append("); zmian taktyki ").Append(_switches)
              .Append("; flanka ").Append(c != null && c.CavalrySeeksFlanks ? "TAK" : "NIE").Append(": wagi Flank ").Append(Volatile.Read(ref _s3Set))
              .Append(" (najpierw lucznicy ").Append(Volatile.Read(ref _s3Hunt)).Append(", bez BehaviorFlank ").Append(Volatile.Read(ref _s3NoBehavior))
              .Append("), konnica w BehaviorFlank ").Append(_s3Rides)
              .Append("; lucznicy za piechote ").Append(c != null && c.ArchersFallBackBehindInfantry ? "TAK" : "NIE").Append(": cofniecia ").Append(_s4Back)
              .Append(", powroty ").Append(_s4Return).Append(", puszczone po zmianie taktyki ").Append(_s4Lost).Append(", bez ScreenedSkirmish ").Append(_s4NoScreen)
              .Append(", nie ostrzeliwal sie ").Append(_s4Other)
              .Append("; probka atak ").Append(_sampleA ?? "-").Append(_histA ?? Hist(_sampleTeamA)).Append(", obrona ").Append(_sampleD ?? "-").Append(_histD ?? Hist(_sampleTeamD))
              .Append("; latki ").Append(_patched).Append('/').Append(_wanted).Append(", wywolan z mnoznikiem ").Append(Volatile.Read(ref _calls))
              .Append(", bledy latek ").Append(Volatile.Read(ref _postErr)).Append(", potkniec ").Append(_stum);
        }

        private static string Hist(int ti)
        {
            if (ti < 0 || ti >= MaxTeams) return "";
            var h = _hist[ti];
            if (h == null || h.Length == 0) return "";
            return " [" + h.ToString().Replace('|', '/') + "]";
        }
    }
}
