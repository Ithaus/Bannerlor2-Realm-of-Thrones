using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// P4 AUDYTU MODOW 14.10 (docs/audyt-modow-2026-10-14/RAPORT-AUDYT-MODOW.md AM-13, AM-09, AM-19) - LANCUCHY MODELI POD BKROTPatch.
    ///
    /// DOWOD (zainstalowany BKROTPatch.dll 26.07, ilspycmd -il; dekompilacje NavalDLC, ROT, BEE, RBL, BK, gra 1.4.8):
    ///  - BKROTPatch.OnGameStart dodaje 12 modeli przez AddModel((GameModel)...) - przeciazenie bez BaseModel (CampaignGameStarter.AddModel(GameModel)
    ///    tylko dopisuje do listy; AddModel&lt;T&gt; wola Initialize(GetModel&lt;T&gt;())). BKROTPatch jest 41. modulem - jego modele sa ostatnie, wiec czynne.
    ///    Kazdy dziedziczy po Default* (Sandbox* w bitwie, BKPartyWageModel w zoldzie) i woluje base NIEWIRTUALNIE (IL: 2 x "call Default::M" -
    ///    w galezi straznika GlobalRecursionGuard i w glownej). Wszystko, co mody dodaly do lancucha przed nimi, nie biegnie.
    ///  - PREDKOSC (AM-13): lista modeli u Jeffa (LauncherData): Default (gra) -> BEE_PartySpeedModel(_baseModel = Default) -> NavalDLC
    ///    (BaseModel = BEE) -> ROTPartySpeedModel(_previousModel = NavalDLC) -> RBL RealisticPartySpeedModel -> BKROTPartySpeedModel (czynny).
    ///    BKROT nie deklaruje CalculateBaseSpeed - dziedziczy ladowa baze Default (ludzie, konie, ladunek) takze na morzu; CalculateFinalSpeed
    ///    woluje Default. Ginie NavalDLC: baza ze statkow i zalogi (zaloga za mala / za duza, flota > 3 statki, przeladowanie), pelne morze +44.8%,
    ///    rzeka +50%, plycizny, wiatr do +150%, perki, sufit 10, i latki BK na NavalDLC (DrakkarHelmsman +4%, podloga 1, OverCrew -0.5).
    ///  - RBL RealisticPartySpeedModel PSUJE lancuch: IL jego CalculateFinalSpeed to "call Default::CalculateFinalSpeed" (base), BaseModel
    ///    nieuzywany, CalculateBaseSpeed niezadeklarowany (baza ladowa Default). Przez RBL lancuch konczy sie na Default (jak dzis). Jego wlasny
    ///    wklad (pory roku, pogoda) dziala tylko przy SeasonsEnabled, a CrashScribe Mends.SeasonsOff gasi go na twardo - RBL dzis nic nie dodaje.
    ///    Dlatego regula ogolna: idac w dol listy od BKROT pomijamy modele "samotne" (klasa NIE dziedziczy wprost po abstrakcyjnym typie modelu,
    ///    tylko po konkretnym Default* - wolaja base, nie BaseModel); one zostaja martwe, jak sa dzis. Glowa = pierwszy owijajacy (wprost
    ///    po typie abstrakcyjnym, z polem / BaseModel na nastepny model).
    ///  - ZOLD (AM-09): Default -> BK BKPartyWageModel -> BEE_PartyWageModel(_baseModel = BK) -> NavalDLC (BaseModel = BEE) -> ROTPartyWageModel
    ///    (_previousModel = NavalDLC) -> BKROTPartyWageModel : BKPartyWageModel (czynny). Ginie ROT (Nieskalani 0, jednostki kultury whitewalker
    ///    = tier, partie i zalogi rodu kultury whitewalker 0), NavalDLC (na morzu -20%, konwoj -80%, perki bosmana, PopularCaptain) i oslona
    ///    wyjatkow BEE. BKROT nie deklaruje GetCharacterWage (Default) ani GetTroopRecruitmentCost (BK) - prefiks tylko dla obiektu typu
    ///    DOKLADNIE BKROTPartyWageModel (na dnie lancucha jest obiekt BK - prefiks go przepuszcza, bez rekursji).
    ///  - AM-19: BKROT "Princely Arbiter" AddFactor(-0.15 x MemberRoster.Count) - Count to liczba POZYCJI rostera (IL: callvirt TroopRoster::get_Count,
    ///    jedno w metodzie); od ok. 7 pozycji zold ujemny (dochod rodu). BK GetVanillaWage nie stawia LimitMin(0) (gra stawia).
    ///  - MAPA (raport: DROBNE): wzrok (NavalDLC: +30% na morzu, sztorm -40%, RavenEye; BK Sjofarandi), najazd (NavalDLC: cecha Nord, Forceful;
    ///    BK DrakkarRaidMaster), leczenie (NavalDLC Resilience na morzu; RBL RealisticHealingModel samotny - zostaje martwy, decyzja o balansie,
    ///    nie naprawa), morale partii (NavalDLC EfficientCaptain w porcie; RBL samotny). MORALE W BITWIE: NavalBattleMoraleModel (BaseModel =
    ///    Sandbox) - poczatkowe morale: figura Lwa i Commodore, perki morskie w bitwie morskiej, +20% z polityki FraternalFleetDoctrine (takze na
    ///    ladzie). Laczymy tylko metody ZADEKLAROWANE w BKROT (transpiler) - metody dziedziczone (np. GetPartySeeingRangeBase, GetRaidLootMultiplier,
    ///    GetBattleEndHealingAmount, morale za taranowanie / zatopienie) zostaja jak sa (cudze latki na Default liczylyby sie wtedy 2 razy).
    ///  - NIE LACZYMY (sprawdzone w IL i na liscie modeli; raport rozdz. 7): BKROTDeathModel, BKROTDisguiseDetectionModel, BKROTPersuasionModel
    ///    wolaja same siebie (callvirt na this), ale straznik przy drugim wejsciu oddaje base Default - wynik = Default + blogoslawienstwa, jak
    ///    zamierzone; innych modeli tych typow przed BKROT nie ma (StoryMode tylko w kampanii fabularnej). BKROTPregnancyModel i BKROTPriceModel
    ///    wolaja base (call Default); przed nimi tylko Default (BEE_ItemPriceFactorModel nie jest rejestrowany przy aktywnym BK: warunek
    ///    !BannerKingsAdapter.IsActive - ten sam co BEE_VillageProductionCalculatorModel, a log "Produkcja wsi" pokazuje NavalDLC -> Default).
    ///    Ciaze (CR / PregnancyYear) i kara handlowa BK (SellByCondition) nietkniete.
    ///
    /// NAPRAWA (jak FoodChain): w metodach BKROT transpiler podmienia OBA "call Default::M" na nasz pomocnik o tej samej sygnaturze (obiekt jako
    /// pierwszy argument) - pomocnik woluje glowe lancucha (kod BKROT z blogoslawienstwami zostaje na wyniku), a bez lancucha / przy wylaczonym
    /// kluczu / przy wyjatku - Default niewirtualnie (DynamicMethod z OpCodes.Call), czyli dokladnie jak dotad. Metody niezadeklarowane w BKROT
    /// (CalculateBaseSpeed, GetCharacterWage, GetTroopRecruitmentCost) - prefiks na metodzie bazowej tylko dla obiektu typu BKROT, i tylko gdy na
    /// tej metodzie nie ma cudzych latek (inaczej BRAK w linii startu). Default biegnie raz (na dnie lancucha) - latki BK na Default licza sie raz.
    /// Nasze postfiksy predkosci / zoldu / werbunku maja licznik zagniezdzenia (SpeedDepth, MountedWage, RecruitCost) - licza sie raz, na zewnatrz.
    /// Straznik rekursji: glowa nie moze prowadzic do obiektu BKROT (sprawdzane przy budowie stanu), licznik glebokosci na rodzaj (MaxDepth).
    /// Klucze czytane przy kazdym wywolaniu (wylaczenie dziala od razu); wpiecie raz na proces, przy pierwszym starcie kampanii (OnSessionLaunched -
    /// BKROTPatch laduje sie po Armoury, a modele BK czytaja singletony BK).
    /// KOLIZJA Z RecruitCost (cena werbunku = dni zoldu): ROT daje Nieskalanym zold 0, a RecruitCost liczylby wtedy cene 1 - cena werbunku bierze
    /// zold bez reguly ROT (PriceWage), czyli jak dotad (Nieskalanych sie kupuje, nie oplaca).
    /// </summary>
    internal static class BkrotChains
    {
        private enum K { Speed = 0, Sight = 1, Raid = 2, Heal = 3, Morale = 4, Battle = 5, Wage = 6 }
        private const int KN = 7;
        private const int MaxDepth = 4;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] KName = { "predkosc", "wzrok", "najazd", "leczenie", "morale partii", "morale w bitwie", "zold" };
        private static readonly string[] BkrotName =
        {
            "BKROTPatch.Models.BKROTPartySpeedModel", "BKROTPatch.Models.BKROTMapVisibilityModel", "BKROTPatch.Models.BKROTRaidModel",
            "BKROTPatch.Models.BKROTPartyHealingModel", "BKROTPatch.Models.BKROTPartyMoraleModel", "BKROTPatch.Models.BKROTBattleMoraleModel",
            "BKROTPatch.Models.BKROTPartyWageModel"
        };
        private static readonly Type[] Abstract =
        {
            typeof(PartySpeedModel), typeof(MapVisibilityModel), typeof(RaidModel), typeof(PartyHealingModel), typeof(PartyMoraleModel),
            typeof(BattleMoraleModel), typeof(PartyWageModel)
        };
        // nasze latki, ktore moga stac na metodach bazowych (wszystkie z licznikiem zagniezdzenia - licza sie raz)
        private static readonly string[] OurDepthPatches = { "Armoury.SpeedDepth", "Armoury.WorldPace", "Armoury.MountedWage", "Armoury.RecruitCost", "Armoury.BkrotChains" };

        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        private static readonly Type[] _bkrot = new Type[KN];
        private static bool _hooksTried;
        private static string _hookErr = "";

        // ------------------------------------------------------------ klucze
        private static bool KeyOn(K k)
        {
            var s = Settings.Current;
            if (s == null) return false;
            switch (k)
            {
                case K.Speed: return s.FixBkrotModelChains;
                case K.Battle: return s.FixBkrotBattleMoraleChain;
                case K.Wage: return s.FixBkrotWageChain;
                default: return s.FixBkrotMapModelChains;
            }
        }

        // ------------------------------------------------------------ znaczniki watku
        [ThreadStatic] private static bool _bypass;       // linia doby: "bez naprawy" (b47fecc3) - wszystkie wpiecia oddaja stary wynik
        [ThreadStatic] private static bool _noNaval;      // linia doby: zold bez obnizki NavalDLC (jak przy wylaczonym FixBkrotWageSeaCut)
        [ThreadStatic] private static bool _noRotWage;    // cena werbunku (PriceWage): zold jednostki bez reguly ROT
        [ThreadStatic] private static int _inWage;        // wewnatrz naszego lancucha zoldu (GetTotalWage) - prefiks NavalDLC dziala tylko tu
        [ThreadStatic] private static int[] _depth;

        private static bool Enter(K k)
        {
            var d = _depth ?? (_depth = new int[KN]);
            if (d[(int)k] >= MaxDepth) { Interlocked.Increment(ref _deep); return false; }
            d[(int)k]++;
            return true;
        }

        private static void Exit(K k)
        {
            var d = _depth;
            if (d != null && d[(int)k] > 0) d[(int)k]--;
        }

        // ------------------------------------------------------------ liczniki i potkniecia (bez globalnego wylacznika - CLAUDE.md 7)
        private static readonly long[] _calls = new long[KN * 2];   // [k*2] pomocnik transpilera, [k*2+1] prefiks metody niezadeklarowanej
        private static long _deep, _arbiter;
        private static int _stumbles;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();
        private static readonly object Gate = new object();

        private static void Count(K k, int i) { Interlocked.Increment(ref _calls[(int)k * 2 + i]); }

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            lock (Gate) { int v; _stumbleAt.TryGetValue(where, out v); _stumbleAt[where] = v + 1; }
            if (n <= 3) Log.Error("BkrotChains." + where, e);
        }

        // ------------------------------------------------------------ stan lancucha (raz na kampanie i obiekt BKROT)
        private sealed class St
        {
            public object Camp;
            public object Self;
            public object Head;
            public string Walk = "-";
            public string Why = "";
        }
        private static readonly St[] _st = new St[KN];

        private static St State(K k, object self)
        {
            var cur = Campaign.Current;
            var s = Volatile.Read(ref _st[(int)k]);
            if (s != null && ReferenceEquals(s.Self, self) && ReferenceEquals(s.Camp, cur)) return s;
            lock (Gate)
            {
                s = _st[(int)k];
                if (s != null && ReferenceEquals(s.Self, self) && ReferenceEquals(s.Camp, cur)) return s;
                s = new St { Camp = cur, Self = self };
                try
                {
                    var models = cur != null && cur.Models != null ? cur.Models.GetGameModels() : null;
                    if (models == null) s.Why = "brak listy modeli kampanii";
                    else Build(s, k, self, models);
                }
                catch (Exception e) { Stumble("stan(" + KName[(int)k] + ")", e); s.Head = null; s.Why = "wyjatek " + e.GetType().Name; }
                Volatile.Write(ref _st[(int)k], s);
                return s;
            }
        }

        /// <summary>Glowa lancucha pod obiektem BKROT: idac w dol listy modeli gry od BKROT - pierwszy model tego typu, ktory owija nastepny
        /// (klasa wprost po typie abstrakcyjnym); modele samotne (podklasy Default*) pominiete - koncza lancuch na Default, zostaja martwe jak dzis.
        /// Lancuch od glowy nie moze prowadzic do obiektu BKROT (petla) i musi sie skonczyc w 16 krokach. Statyczna lista - do testu poza gra.</summary>
        private static void Build(St s, K k, object self, IList<GameModel> models)
        {
            var abs = Abstract[(int)k];
            int idx = -1;
            for (int i = 0; i < models.Count; i++) if (ReferenceEquals(models[i], self)) { idx = i; break; }
            if (idx < 0) { s.Why = "obiekt BKROT spoza listy modeli tej kampanii"; return; }
            var skipped = new List<string>();
            object head = null;
            for (int j = idx - 1; j >= 0; j--)
            {
                var m = models[j];
                if (m == null || !abs.IsInstanceOfType(m) || IsBkrot(m)) continue;
                if (m.GetType().BaseType == abs) { head = m; break; }
                skipped.Add(m.GetType().Name);
            }
            string skip = skipped.Count > 0 ? " (pominiete - koncza lancuch na Default: " + string.Join(", ", skipped.ToArray()) + ")" : "";
            if (head == null) { s.Why = "pod BKROT tylko modele konczace lancuch" + skip + " - nic do przywrocenia"; return; }
            // model samej gry (Default* / Sandbox*: wprost po typie abstrakcyjnym, ale bez nastepnego) - to jest base BKROT, nic do przywrocenia
            var an = head.GetType().Assembly.GetName().Name ?? "";
            if (Next(head, abs) == null && (an.StartsWith("TaleWorlds.", StringComparison.Ordinal) || an.StartsWith("SandBox", StringComparison.Ordinal)))
            { s.Why = "pod BKROT tylko model gry " + head.GetType().Name + skip + " - nic do przywrocenia"; return; }
            var sb = new StringBuilder();
            var seen = new List<object>();
            object c = head;
            int n = 0;
            for (; c != null && n < 16; n++)
            {
                bool again = ReferenceEquals(c, self) || IsBkrot(c);
                foreach (var o in seen) if (ReferenceEquals(o, c)) again = true;
                if (again) { s.Why = "petla - lancuch wraca do " + c.GetType().Name + ": " + sb; return; }
                seen.Add(c);
                sb.Append(n > 0 ? " -> " : "").Append(c.GetType().Name);
                c = Next(c, abs);
            }
            if (c != null) { s.Why = "lancuch dluzszy niz 16: " + sb; return; }
            s.Head = head;
            s.Walk = self.GetType().Name + " -> " + sb + skip;
        }

        /// <summary>Nastepny model w lancuchu: model owijajacy (wprost po typie abstrakcyjnym) - pierwsze pole typu modelu (ROT _previousModel,
        /// BEE _baseModel, NavalDLC BaseModel gry); model po klasie konkretnej (Default*, BK, RBL) - koniec lancucha.</summary>
        private static object Next(object m, Type abs)
        {
            if (m == null) return null;
            var t = m.GetType();
            if (t.BaseType != abs) return null;
            for (var k = t; k != null && k != typeof(object); k = k.BaseType)
                foreach (var fi in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!abs.IsAssignableFrom(fi.FieldType)) continue;
                    var v = fi.GetValue(m);
                    if (v != null && !ReferenceEquals(v, m)) return v;
                }
            return null;
        }

        private static bool IsBkrot(object m) { var n = m != null ? m.GetType().FullName : null; return n != null && n.StartsWith("BKROTPatch.", StringComparison.Ordinal); }

        private static object Head(K k, object self)
        {
            if (_bypass || self == null || !KeyOn(k)) return null;
            try { return State(k, self).Head; }
            catch (Exception e) { Stumble("stan(" + KName[(int)k] + ")", e); return null; }
        }

        // ------------------------------------------------------------ wywolania niewirtualne (stary base BKROT)
        private static Func<DefaultPartySpeedCalculatingModel, MobileParty, ExplainedNumber, ExplainedNumber> _fSpeed;
        private static Func<DefaultMapVisibilityModel, MobileParty, bool, ExplainedNumber> _fSight;
        private static Func<DefaultRaidModel, MapEventSide, float, ExplainedNumber> _fRaid;
        private static Func<DefaultPartyHealingModel, PartyBase, bool, bool, ExplainedNumber> _fHealReg;
        private static Func<DefaultPartyHealingModel, PartyBase, CharacterObject, DamageTypes, bool, PartyBase, float> _fHealSurv;
        private static Func<DefaultPartyMoraleModel, MobileParty, bool, ExplainedNumber> _fMorale;
        private static Func<DefaultPartyMoraleModel, PartyBase, int> _fStarve;
        private static Func<SandboxBattleMoraleModel, Agent, float, float> _fBattle;
        private static Func<DefaultPartyWageModel, MobileParty, TroopRoster, bool, ExplainedNumber> _fWage;

        /// <summary>DynamicMethod: ldarg.0 (castclass, gdy typ pierwszego argumentu jest szerszy), ldarg.1..n, call target, ret - jak "base.M(...)".</summary>
        private static D NonVirtual<D>(MethodInfo target, Type selfType) where D : class
        {
            if (target == null) return null;
            var ps = target.GetParameters();
            var types = new Type[ps.Length + 1];
            types[0] = selfType;
            for (int i = 0; i < ps.Length; i++) types[i + 1] = ps[i].ParameterType;
            var dm = new DynamicMethod("BkrotChains_" + target.DeclaringType.Name + "_" + target.Name, target.ReturnType, types, typeof(BkrotChains).Module, true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (selfType != target.DeclaringType) il.Emit(OpCodes.Castclass, target.DeclaringType);
            for (int i = 1; i < types.Length; i++) il.Emit(OpCodes.Ldarg_S, (byte)i);
            il.Emit(OpCodes.Call, target);
            il.Emit(OpCodes.Ret);
            return dm.CreateDelegate(typeof(D)) as D;
        }

        // ------------------------------------------------------------ pomocniki transpilera (ta sama sygnatura co "call Default::M", obiekt pierwszy)

        public static ExplainedNumber SpeedFinal(DefaultPartySpeedCalculatingModel self, MobileParty party, ExplainedNumber finalSpeed)
        {
            var head = Head(K.Speed, self) as PartySpeedModel;
            if (head != null && Enter(K.Speed))
            {
                try { var r = head.CalculateFinalSpeed(party, finalSpeed); Count(K.Speed, 0); return r; }
                catch (Exception e) { Stumble("predkosc koncowa", e); }
                finally { Exit(K.Speed); }
            }
            return _fSpeed(self, party, finalSpeed);
        }

        public static ExplainedNumber SightSpot(DefaultMapVisibilityModel self, MobileParty party, bool desc)
        {
            var head = Head(K.Sight, self) as MapVisibilityModel;
            if (head != null && Enter(K.Sight))
            {
                try { var r = head.GetPartySpottingRange(party, desc); Count(K.Sight, 0); return r; }
                catch (Exception e) { Stumble("wzrok", e); }
                finally { Exit(K.Sight); }
            }
            return _fSight(self, party, desc);
        }

        public static ExplainedNumber RaidHit(DefaultRaidModel self, MapEventSide side, float hp)
        {
            var head = Head(K.Raid, self) as RaidModel;
            if (head != null && Enter(K.Raid))
            {
                try { var r = head.CalculateHitDamage(side, hp); Count(K.Raid, 0); return r; }
                catch (Exception e) { Stumble("najazd", e); }
                finally { Exit(K.Raid); }
            }
            return _fRaid(self, side, hp);
        }

        public static ExplainedNumber HealRegulars(DefaultPartyHealingModel self, PartyBase party, bool prisoners, bool desc)
        {
            var head = Head(K.Heal, self) as PartyHealingModel;
            if (head != null && Enter(K.Heal))
            {
                try { var r = head.GetDailyHealingForRegulars(party, prisoners, desc); Count(K.Heal, 0); return r; }
                catch (Exception e) { Stumble("leczenie", e); }
                finally { Exit(K.Heal); }
            }
            return _fHealReg(self, party, prisoners, desc);
        }

        public static float HealSurvival(DefaultPartyHealingModel self, PartyBase party, CharacterObject ch, DamageTypes dt, bool canKill, PartyBase enemy)
        {
            var head = Head(K.Heal, self) as PartyHealingModel;
            if (head != null && Enter(K.Heal))
            {
                try { var r = head.GetSurvivalChance(party, ch, dt, canKill, enemy); Count(K.Heal, 0); return r; }
                catch (Exception e) { Stumble("przezycie", e); }
                finally { Exit(K.Heal); }
            }
            return _fHealSurv(self, party, ch, dt, canKill, enemy);
        }

        public static ExplainedNumber MoraleEffective(DefaultPartyMoraleModel self, MobileParty party, bool desc)
        {
            var head = Head(K.Morale, self) as PartyMoraleModel;
            if (head != null && Enter(K.Morale))
            {
                try { var r = head.GetEffectivePartyMorale(party, desc); Count(K.Morale, 0); return r; }
                catch (Exception e) { Stumble("morale partii", e); }
                finally { Exit(K.Morale); }
            }
            return _fMorale(self, party, desc);
        }

        public static int MoraleStarve(DefaultPartyMoraleModel self, PartyBase party)
        {
            var head = Head(K.Morale, self) as PartyMoraleModel;
            if (head != null && Enter(K.Morale))
            {
                try { var r = head.GetDailyStarvationMoralePenalty(party); Count(K.Morale, 0); return r; }
                catch (Exception e) { Stumble("morale glodu", e); }
                finally { Exit(K.Morale); }
            }
            return _fStarve(self, party);
        }

        public static float BattleInitial(SandboxBattleMoraleModel self, Agent agent, float baseMorale)
        {
            var head = Head(K.Battle, self) as BattleMoraleModel;
            if (head != null && Enter(K.Battle))
            {
                try { var r = head.GetEffectiveInitialMorale(agent, baseMorale); Count(K.Battle, 0); return r; }
                catch (Exception e) { Stumble("morale w bitwie", e); }
                finally { Exit(K.Battle); }
            }
            return _fBattle(self, agent, baseMorale);
        }

        /// <summary>Zold partii: lancuch ROT -> NavalDLC -> BEE -> BK. Roster null (BKROT ma domyslny null; BK go nie czyta) - roster partii (NavalDLC
        /// liczy z niego perki). Wynik ponizej 0 przyciety do 0: BK nie stawia LimitMin (gra stawia), a NavalDLC na morzu -0.2 i konwoj -0.8 obok
        /// "Caravan" -0.25 BK zrobilby zold ujemny (zloto z niczego).</summary>
        public static ExplainedNumber WageTotal(DefaultPartyWageModel self, MobileParty party, TroopRoster roster, bool desc)
        {
            var head = party != null ? Head(K.Wage, self) as PartyWageModel : null;
            if (head != null && Enter(K.Wage))
            {
                _inWage++;
                try
                {
                    var r = head.GetTotalWage(party, roster ?? party.MemberRoster, desc);
                    if (r.LimitMinValue < 0f) r.LimitMin(0f);
                    Count(K.Wage, 0);
                    return r;
                }
                catch (Exception e) { Stumble("zold partii", e); }
                finally { _inWage--; Exit(K.Wage); }
            }
            return _fWage(self, party, roster, desc);
        }

        /// <summary>AM-19: w BKROT "-0.15 x MemberRoster.Count" - przy kluczu Count = 1, czyli -15% (jak opis bostwa). Woluje sie tylko, gdy wodz
        /// partii ma blogoslawienstwo Princely Arbiter.</summary>
        public static int ArbiterCount(TroopRoster roster)
        {
            if (!_bypass)
            {
                var s = Settings.Current;
                if (s != null && s.FixPrincelyArbiterWage) { Interlocked.Increment(ref _arbiter); return 1; }
            }
            return roster.Count;
        }

        // ------------------------------------------------------------ prefiksy metod niezadeklarowanych w BKROT

        /// <summary>Default.CalculateBaseSpeed dla obiektu BKROTPartySpeedModel: baza z lancucha (NavalDLC na morzu: statki i zaloga; ROT: zaciag +20%).
        /// WorldPace (postfiks na tej metodzie, licznik SpeedDepth) liczy sie raz - na zewnatrz.</summary>
        public static bool SpeedBasePrefix(DefaultPartySpeedCalculatingModel __instance, MobileParty __0, bool __1, int __2, int __3, ref ExplainedNumber __result)
        {
            if (__instance == null || _bkrot[(int)K.Speed] == null || __instance.GetType() != _bkrot[(int)K.Speed]) return true;
            var head = Head(K.Speed, __instance) as PartySpeedModel;
            if (head == null || !Enter(K.Speed)) return true;
            try { __result = head.CalculateBaseSpeed(__0, __1, __2, __3); Count(K.Speed, 1); return false; }
            catch (Exception e) { Stumble("predkosc bazowa", e); return true; }
            finally { Exit(K.Speed); }
        }

        /// <summary>Default.GetCharacterWage dla obiektu BKROTPartyWageModel: zold jednostki z lancucha (ROT: Nieskalani 0, kultura whitewalker = tier).
        /// Jednostka bez kultury - jak dotad (ROT czyta Culture.StringId bez sprawdzenia). Cena werbunku (_noRotWage) - jak dotad.</summary>
        public static bool WageCharPrefix(DefaultPartyWageModel __instance, CharacterObject __0, ref int __result)
        {
            if (_noRotWage || __instance == null || _bkrot[(int)K.Wage] == null || __instance.GetType() != _bkrot[(int)K.Wage]) return true;
            if (__0 == null || __0.Culture == null) return true;
            var head = Head(K.Wage, __instance) as PartyWageModel;
            if (head == null || !Enter(K.Wage)) return true;
            try { __result = head.GetCharacterWage(__0); Count(K.Wage, 1); return false; }
            catch (Exception e) { Stumble("zold jednostki", e); return true; }
            finally { Exit(K.Wage); }
        }

        /// <summary>BK GetTroopRecruitmentCost dla obiektu BKROTPartyWageModel: koszt z lancucha (NavalDLC PopularCaptain). RecruitCost (postfiks,
        /// licznik zagniezdzenia) liczy cene raz, na zewnatrz.</summary>
        public static bool RecruitPrefix(object __instance, CharacterObject __0, Hero __1, bool __2, ref ExplainedNumber __result)
        {
            if (__instance == null || _bkrot[(int)K.Wage] == null || __instance.GetType() != _bkrot[(int)K.Wage]) return true;
            var head = Head(K.Wage, __instance) as PartyWageModel;
            if (head == null || !Enter(K.Wage)) return true;
            try { __result = head.GetTroopRecruitmentCost(__0, __1, __2); Count(K.Wage, 1); return false; }
            catch (Exception e) { Stumble("koszt werbunku", e); return true; }
            finally { Exit(K.Wage); }
        }

        private static MethodInfo _wageBaseGetter;   // MBGameModel<PartyWageModel>.BaseModel (protected)

        /// <summary>NavalDLCPartyWageModel.GetTotalWage w NASZYM lancuchu przy wylaczonej obnizce morskiej: wynik modelu pod NavalDLC (BEE -> BK) bez
        /// czynnikow morza, konwoju i bosmana. Poza naszym lancuchem i przy wlaczonym kluczu - NavalDLC jak w DLC.</summary>
        public static bool NavalWagePrefix(object __instance, MobileParty __0, TroopRoster __1, bool __2, ref ExplainedNumber __result)
        {
            if (_inWage <= 0) return true;
            var s = Settings.Current;
            if (!_noNaval && s != null && s.FixBkrotWageSeaCut) return true;
            try
            {
                var below = _wageBaseGetter != null ? _wageBaseGetter.Invoke(__instance, null) as PartyWageModel : null;
                if (below == null || ReferenceEquals(below, __instance)) return true;
                __result = below.GetTotalWage(__0, __1, __2);
                return false;
            }
            catch (Exception e) { Stumble("zold bez obnizki morskiej", e); return true; }
        }

        /// <summary>AM-19 (klucz FixPrincelyArbiterWage): zold partii nigdy ponizej 0 (tylko podnosi dolny limit, gdy jest ujemny albo go brak).</summary>
        public static void WageMinPostfix(ref ExplainedNumber __result)
        {
            if (_bypass) return;
            var s = Settings.Current;
            if (s == null || !s.FixPrincelyArbiterWage) return;
            if (__result.LimitMinValue < 0f) __result.LimitMin(0f);
        }

        /// <summary>Zold jednostki do ceny werbunku (RecruitCost): bez reguly ROT - Nieskalanych sie kupuje po cenie jak dotad, a nie za 1.</summary>
        internal static int PriceWage(CharacterObject c)
        {
            var m = Campaign.Current != null ? Campaign.Current.Models.PartyWageModel : null;
            if (m == null || c == null) return 0;
            bool was = _noRotWage;
            _noRotWage = true;
            try { return m.GetCharacterWage(c); }
            finally { _noRotWage = was; }
        }

        // ------------------------------------------------------------ transpiler (wspolny; spec po metodzie oryginalnej)
        private sealed class TSpec
        {
            public K Kind;
            public string Label;
            public MethodBase Target;
            public string BaseDecl;
            public string BaseName;
            public int BaseArgc;
            public MethodInfo Helper;
            public int Expect = 2;
            public bool Arbiter;
            public int Hits = -1, ArbiterHits = -1;
            public bool Patched;
            public string State = "nie wpiety";
        }
        private static readonly List<TSpec> _specs = new List<TSpec>();
        private static readonly MethodInfo _mArbiter = AccessTools.Method(typeof(BkrotChains), nameof(ArbiterCount));

        private static TSpec SpecOf(MethodBase original)
        {
            if (original == null) return null;
            foreach (var t in _specs)
                if (t.Target != null && t.Target.Name == original.Name && t.Target.DeclaringType == original.DeclaringType) return t;
            return null;
        }

        /// <summary>Oba "call Default::M" (galaz straznika i glowna) -> pomocnik; inna liczba trafien - IL bez zmian (EnsureHooks zdejmie latke).
        /// W zoldzie dodatkowo jedyne "TroopRoster::get_Count" (AM-19) -> ArbiterCount. Harmony woluje go od nowa przy kazdym przelataniu metody.</summary>
        internal static IEnumerable<CodeInstruction> ChainTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            var t = SpecOf(original);
            if (t == null) return list;
            var hits = new List<int>();
            var arb = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                var mi = list[i].operand as MethodInfo;
                if (mi == null) continue;
                if (list[i].opcode == OpCodes.Call && mi.Name == t.BaseName && mi.DeclaringType != null && mi.DeclaringType.FullName == t.BaseDecl
                    && mi.GetParameters().Length == t.BaseArgc)
                    hits.Add(i);
                if (t.Arbiter && (list[i].opcode == OpCodes.Callvirt || list[i].opcode == OpCodes.Call) && mi.Name == "get_Count" && mi.DeclaringType == typeof(TroopRoster))
                    arb.Add(i);
            }
            t.Hits = hits.Count;
            t.ArbiterHits = t.Arbiter ? arb.Count : -1;
            if (t.Helper != null && hits.Count == t.Expect)
                foreach (var i in hits) { list[i].opcode = OpCodes.Call; list[i].operand = t.Helper; }   // etykiety i bloki wyjatkow zostaja na instrukcji
            if (t.Arbiter && arb.Count == 1) { list[arb[0]].opcode = OpCodes.Call; list[arb[0]].operand = _mArbiter; }
            return list;
        }

        // ------------------------------------------------------------ wpiecie (raz na proces, pierwszy OnSessionLaunched)
        private sealed class Route { public string Label; public MethodBase Target; public string State = "nie wpiety"; public bool On; }
        private static readonly List<Route> _routes = new List<Route>();

        internal static void EnsureHooks()
        {
            if (!_hooksTried && _harmony != null && Campaign.Current != null)
            {
                _hooksTried = true;
                try { Wire(); } catch (Exception e) { _hookErr = e.GetType().Name; Stumble("EnsureHooks", e); }
            }
            try { Log.Info(StartLine()); } catch (Exception e) { Stumble("linia startu", e); }
        }

        private static void Wire()
        {
            for (int k = 0; k < KN; k++) _bkrot[k] = AccessTools.TypeByName(BkrotName[k]);
            if (_bkrot.All(x => x == null)) { _hookErr = "brak BKROTPatch - nic do naprawy"; return; }
            _wageBaseGetter = AccessTools.PropertyGetter(typeof(MBGameModel<PartyWageModel>), "BaseModel");

            var dSpeed = typeof(DefaultPartySpeedCalculatingModel);
            var mSpeedF = AccessTools.DeclaredMethod(dSpeed, "CalculateFinalSpeed", new[] { typeof(MobileParty), typeof(ExplainedNumber) });
            _fSpeed = NonVirtual<Func<DefaultPartySpeedCalculatingModel, MobileParty, ExplainedNumber, ExplainedNumber>>(mSpeedF, dSpeed);
            var dSight = typeof(DefaultMapVisibilityModel);
            _fSight = NonVirtual<Func<DefaultMapVisibilityModel, MobileParty, bool, ExplainedNumber>>(
                AccessTools.DeclaredMethod(dSight, "GetPartySpottingRange", new[] { typeof(MobileParty), typeof(bool) }), dSight);
            var dRaid = typeof(DefaultRaidModel);
            _fRaid = NonVirtual<Func<DefaultRaidModel, MapEventSide, float, ExplainedNumber>>(
                AccessTools.DeclaredMethod(dRaid, "CalculateHitDamage", new[] { typeof(MapEventSide), typeof(float) }), dRaid);
            var dHeal = typeof(DefaultPartyHealingModel);
            _fHealReg = NonVirtual<Func<DefaultPartyHealingModel, PartyBase, bool, bool, ExplainedNumber>>(
                AccessTools.DeclaredMethod(dHeal, "GetDailyHealingForRegulars", new[] { typeof(PartyBase), typeof(bool), typeof(bool) }), dHeal);
            _fHealSurv = NonVirtual<Func<DefaultPartyHealingModel, PartyBase, CharacterObject, DamageTypes, bool, PartyBase, float>>(
                AccessTools.DeclaredMethod(dHeal, "GetSurvivalChance", new[] { typeof(PartyBase), typeof(CharacterObject), typeof(DamageTypes), typeof(bool), typeof(PartyBase) }), dHeal);
            var dMor = typeof(DefaultPartyMoraleModel);
            _fMorale = NonVirtual<Func<DefaultPartyMoraleModel, MobileParty, bool, ExplainedNumber>>(
                AccessTools.DeclaredMethod(dMor, "GetEffectivePartyMorale", new[] { typeof(MobileParty), typeof(bool) }), dMor);
            _fStarve = NonVirtual<Func<DefaultPartyMoraleModel, PartyBase, int>>(
                AccessTools.DeclaredMethod(dMor, "GetDailyStarvationMoralePenalty", new[] { typeof(PartyBase) }), dMor);
            var dBat = typeof(SandboxBattleMoraleModel);
            _fBattle = NonVirtual<Func<SandboxBattleMoraleModel, Agent, float, float>>(
                AccessTools.DeclaredMethod(dBat, "GetEffectiveInitialMorale", new[] { typeof(Agent), typeof(float) }), dBat);
            var bkWage = _bkrot[(int)K.Wage] != null ? _bkrot[(int)K.Wage].BaseType : null;   // BannerKings.Models.Vanilla.BKPartyWageModel
            var mBkTotal = bkWage != null ? AccessTools.DeclaredMethod(bkWage, "GetTotalWage", new[] { typeof(MobileParty), typeof(TroopRoster), typeof(bool) }) : null;
            _fWage = NonVirtual<Func<DefaultPartyWageModel, MobileParty, TroopRoster, bool, ExplainedNumber>>(mBkTotal, typeof(DefaultPartyWageModel));

            Spec(K.Speed, "predkosc CalculateFinalSpeed", "CalculateFinalSpeed", dSpeed.FullName, "CalculateFinalSpeed", 2, nameof(SpeedFinal), _fSpeed != null);
            Spec(K.Sight, "wzrok GetPartySpottingRange", "GetPartySpottingRange", dSight.FullName, "GetPartySpottingRange", 2, nameof(SightSpot), _fSight != null);
            Spec(K.Raid, "najazd CalculateHitDamage", "CalculateHitDamage", dRaid.FullName, "CalculateHitDamage", 2, nameof(RaidHit), _fRaid != null);
            Spec(K.Heal, "leczenie GetDailyHealingForRegulars", "GetDailyHealingForRegulars", dHeal.FullName, "GetDailyHealingForRegulars", 3, nameof(HealRegulars), _fHealReg != null);
            Spec(K.Heal, "leczenie GetSurvivalChance", "GetSurvivalChance", dHeal.FullName, "GetSurvivalChance", 5, nameof(HealSurvival), _fHealSurv != null);
            Spec(K.Morale, "morale GetEffectivePartyMorale", "ComputeEffectivePartyMorale", dMor.FullName, "GetEffectivePartyMorale", 2, nameof(MoraleEffective), _fMorale != null);
            Spec(K.Morale, "morale GetDailyStarvationMoralePenalty", "GetDailyStarvationMoralePenalty", dMor.FullName, "GetDailyStarvationMoralePenalty", 1, nameof(MoraleStarve), _fStarve != null);
            Spec(K.Battle, "bitwa GetEffectiveInitialMorale", "GetEffectiveInitialMorale", dBat.FullName, "GetEffectiveInitialMorale", 2, nameof(BattleInitial), _fBattle != null);
            var w = Spec(K.Wage, "zold GetTotalWage", "GetTotalWage", bkWage != null ? bkWage.FullName : "-", "GetTotalWage", 3, nameof(WageTotal), _fWage != null);
            if (w != null) w.Arbiter = true;

            var tr = AccessTools.Method(typeof(BkrotChains), nameof(ChainTranspiler));
            foreach (var t in _specs) PatchSpec(t, tr);

            // metody niezadeklarowane w BKROT - prefiks na metodzie bazowej (Priority.High: po prefiksach licznikow zagniezdzenia, Priority.First)
            if (_bkrot[(int)K.Speed] != null)
                RouteOn("predkosc CalculateBaseSpeed", AccessTools.Method(_bkrot[(int)K.Speed], "CalculateBaseSpeed", new[] { typeof(MobileParty), typeof(bool), typeof(int), typeof(int) }), nameof(SpeedBasePrefix));
            if (_bkrot[(int)K.Wage] != null)
            {
                RouteOn("zold GetCharacterWage", AccessTools.Method(_bkrot[(int)K.Wage], "GetCharacterWage", new[] { typeof(CharacterObject) }), nameof(WageCharPrefix));
                RouteOn("zold GetTroopRecruitmentCost", AccessTools.Method(_bkrot[(int)K.Wage], "GetTroopRecruitmentCost", new[] { typeof(CharacterObject), typeof(Hero), typeof(bool) }), nameof(RecruitPrefix));
                // obnizka morska osobnym kluczem: NavalDLC.GetTotalWage w naszym lancuchu (tylko przy wylaczonym FixBkrotWageSeaCut / w linii doby)
                var tNaval = AccessTools.TypeByName("NavalDLC.GameComponents.NavalDLCPartyWageModel");
                var mNaval = tNaval != null ? AccessTools.DeclaredMethod(tNaval, "GetTotalWage", new[] { typeof(MobileParty), typeof(TroopRoster), typeof(bool) }) : null;
                var rn = new Route { Label = "zold NavalDLC (obnizka morska)", Target = mNaval };
                _routes.Add(rn);
                if (mNaval == null) rn.State = "BRAK - brak NavalDLCPartyWageModel.GetTotalWage";
                else if (_wageBaseGetter == null) rn.State = "BRAK - brak MBGameModel.BaseModel";
                else
                {
                    _harmony.Patch(mNaval, prefix: new HarmonyMethod(typeof(BkrotChains), nameof(NavalWagePrefix)) { priority = Priority.High });
                    rn.On = true; rn.State = "TAK";
                }
                // AM-19: zold partii BKROT nigdy ponizej 0
                var mTot = AccessTools.DeclaredMethod(_bkrot[(int)K.Wage], "GetTotalWage", new[] { typeof(MobileParty), typeof(TroopRoster), typeof(bool) });
                var rm = new Route { Label = "zold LimitMin(0)", Target = mTot };
                _routes.Add(rm);
                if (mTot == null) rm.State = "BRAK - brak BKROTPartyWageModel.GetTotalWage";
                else
                {
                    _harmony.Patch(mTot, postfix: new HarmonyMethod(typeof(BkrotChains), nameof(WageMinPostfix)) { priority = Priority.Low });
                    rm.On = true; rm.State = "TAK";
                }
            }
        }

        private static TSpec Spec(K k, string label, string bkrotMethod, string baseDecl, string baseName, int argc, string helper, bool fallbackOk)
        {
            var bt = _bkrot[(int)k];
            var t = new TSpec { Kind = k, Label = label, BaseDecl = baseDecl, BaseName = baseName, BaseArgc = argc, Helper = AccessTools.Method(typeof(BkrotChains), helper) };
            _specs.Add(t);
            if (bt == null) { t.State = "BRAK - brak typu " + BkrotName[(int)k]; return t; }
            t.Target = AccessTools.DeclaredMethod(bt, bkrotMethod);
            if (t.Target == null) { t.State = "BRAK - brak metody " + bt.Name + "." + bkrotMethod; return t; }
            if (!fallbackOk) { t.State = "BRAK - brak metody bazowej " + baseDecl + "." + baseName + " (wywolanie niewirtualne)"; t.Target = null; }
            return t;
        }

        private static void PatchSpec(TSpec t, MethodInfo tr)
        {
            if (t.Target == null) return;
            try
            {
                t.Hits = -1;
                _harmony.Patch(t.Target, transpiler: new HarmonyMethod(tr));
                bool chainOk = t.Hits == t.Expect;
                bool arbOk = t.Arbiter && t.ArbiterHits == 1;
                if (!chainOk && !arbOk)
                {
                    try { _harmony.Unpatch(t.Target, tr); } catch (Exception e) { Stumble("Unpatch(" + t.Label + ")", e); }
                    t.State = "BRAK - wywolan base " + t.Hits + " (oczekiwane " + t.Expect + ") - zdjety, jak dotad";
                    return;
                }
                t.Patched = chainOk;
                t.State = (chainOk ? "TAK " + t.Hits + "/" + t.Expect : "BRAK - wywolan base " + t.Hits + " (oczekiwane " + t.Expect + ")")
                          + (t.Arbiter ? (arbOk ? ", Wielki Ksiaze 1/1" : ", Wielki Ksiaze BRAK (" + t.ArbiterHits + ")") : "");
                if (t.Arbiter) _arbiterWired = arbOk;
            }
            catch (Exception e) { t.State = "BRAK - wyjatek " + e.GetType().Name; Stumble("Patch(" + t.Label + ")", e); }
        }
        private static bool _arbiterWired;

        /// <summary>Prefiks na metodzie bazowej (zadeklarowanej w Default / BK), tylko dla obiektu BKROT. Cudze latki na tej metodzie liczylyby sie
        /// 2 razy (obiekt BKROT na zewnatrz + obiekt na dnie lancucha) - wtedy BRAK; nasze latki z licznikiem zagniezdzenia sa w porzadku.</summary>
        private static void RouteOn(string label, MethodInfo m, string prefix)
        {
            m = Declared(m);
            var r = new Route { Label = label, Target = m };
            _routes.Add(r);
            if (m == null) { r.State = "BRAK - brak metody"; return; }
            var foreign = Foreign(m);
            if (foreign.Length > 0) { r.State = "BRAK - cudze latki na " + m.DeclaringType.Name + "." + m.Name + ": " + foreign; return; }
            try
            {
                _harmony.Patch(m, prefix: new HarmonyMethod(typeof(BkrotChains), prefix) { priority = Priority.High });
                r.On = true;
                r.State = "TAK (" + m.DeclaringType.Name + ")";
            }
            catch (Exception e) { r.State = "BRAK - wyjatek " + e.GetType().Name; Stumble("Patch(" + label + ")", e); }
        }

        /// <summary>Metoda w typie, ktory ja deklaruje (AccessTools.Method na typie BKROT oddaje metode dziedziczona z ReflectedType = BKROT).</summary>
        private static MethodInfo Declared(MethodInfo m)
        {
            if (m == null || m.DeclaringType == null) return m;
            var d = m.DeclaringType.GetMethod(m.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                                              m.GetParameters().Select(p => p.ParameterType).ToArray(), null);
            return d ?? m;
        }

        private static string Foreign(MethodBase m)
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return "";
            var bad = new List<string>();
            foreach (var p in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
            {
                var dt = p != null && p.PatchMethod != null ? p.PatchMethod.DeclaringType : null;
                bool ours = p != null && _harmony != null && p.owner == _harmony.Id && dt != null && OurDepthPatches.Contains(dt.FullName);
                if (!ours) bad.Add((p != null ? p.owner : "?") + ":" + (dt != null ? dt.Name : "?"));
            }
            return string.Join(", ", bad.Distinct().ToArray());
        }

        // ------------------------------------------------------------ linia startu (kazdy start sesji)
        private static string YesNo(bool b) { return b ? "TAK" : "NIE"; }

        private static string Keys()
        {
            var s = Settings.Current;
            if (s == null) return "-";
            return "predkosc " + YesNo(s.FixBkrotModelChains) + ", mapa " + YesNo(s.FixBkrotMapModelChains) + ", bitwa " + YesNo(s.FixBkrotBattleMoraleChain)
                   + ", zold " + YesNo(s.FixBkrotWageChain) + ", morze " + YesNo(s.FixBkrotWageSeaCut) + ", Wielki Ksiaze " + YesNo(s.FixPrincelyArbiterWage);
        }

        private static GameModel Active(K k)
        {
            var cur = Campaign.Current;
            var models = cur != null && cur.Models != null ? cur.Models.GetGameModels() : null;
            if (models == null) return null;
            for (int i = models.Count - 1; i >= 0; i--) if (models[i] != null && Abstract[(int)k].IsInstanceOfType(models[i])) return models[i];
            return null;
        }

        private static object BkrotInstance(K k)
        {
            var t = _bkrot[(int)k];
            var cur = Campaign.Current;
            var models = t != null && cur != null && cur.Models != null ? cur.Models.GetGameModels() : null;
            if (models == null) return null;
            foreach (var m in models) if (m != null && m.GetType() == t) return m;
            return null;
        }

        private static string StartLine()
        {
            var sb = new StringBuilder(2500);
            sb.Append("Lancuchy modeli BKROT (P4): start | klucze: ").Append(Keys());
            if (_hookErr.Length > 0 && _specs.Count == 0) return sb.Append(" | ").Append(_hookErr).Append('.').ToString();
            int all = 0, ok = 0, brak = 0;
            foreach (var t in _specs) { all++; if (t.Patched) ok++; else brak++; }
            foreach (var r in _routes) { all++; if (r.On) ok++; else brak++; }
            sb.Append(" | wpiecia ").Append(ok).Append('/').Append(all).Append(" (BRAK ").Append(brak).Append("): ");
            bool first = true;
            foreach (var t in _specs) { sb.Append(first ? "" : "; ").Append(t.Label).Append(' ').Append(t.State); first = false; }
            foreach (var r in _routes) { sb.Append(first ? "" : "; ").Append(r.Label).Append(' ').Append(r.State); first = false; }
            sb.Append(" | lancuchy:");
            for (int k = 0; k < KN; k++)
            {
                var self = BkrotInstance((K)k);
                var act = Active((K)k);
                sb.Append(k > 0 ? ";" : "").Append(' ').Append(KName[k]).Append(": ");
                if (self == null) { sb.Append("brak obiektu ").Append(BkrotName[k].Substring(BkrotName[k].LastIndexOf('.') + 1)); }
                else
                {
                    var st = State((K)k, self);
                    sb.Append(st.Head != null ? st.Walk : "BRAK (" + st.Why + ")");
                }
                if (act != null && !ReferenceEquals(act, self)) sb.Append(" [czynny model to ").Append(act.GetType().Name).Append(", nie BKROT]");
            }
            sb.Append(" | Wielki Ksiaze (AM-19): ").Append(_arbiterWired ? "wpiety" : "BRAK");
            sb.Append(" | potkniecia od startu sesji ").Append(Volatile.Read(ref _stumbles));
            if (_hookErr.Length > 0) sb.Append(" | ").Append(_hookErr);
            return sb.Append('.').ToString();
        }

        // ------------------------------------------------------------ linia doby (tylko odczyt)
        private static int _lastDay = -1;
        private static readonly long[] _callsAt = new long[KN * 2];
        private static long _arbiterAt, _deepAt;

        private static string F(float v) { return float.IsNaN(v) || float.IsInfinity(v) ? "-" : v.ToString("0.00", Inv); }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "?" : s.Replace("|", "/").Replace(";", ",").Replace("[", "(").Replace("]", ")"); }

        private static float SpeedOf(PartySpeedModel m, MobileParty mp, bool old, bool desc, out ExplainedNumber en)
        {
            bool was = _bypass;
            _bypass = old;
            try { en = m.CalculateFinalSpeed(mp, m.CalculateBaseSpeed(mp, desc, 0, 0)); return en.ResultNumber; }
            finally { _bypass = was; }
        }

        private static float WageOf(PartyWageModel m, MobileParty mp, bool old, bool noNaval)
        {
            bool w1 = _bypass, w2 = _noNaval;
            _bypass = old; _noNaval = noNaval;
            try { return m.GetTotalWage(mp, mp.MemberRoster, false).ResultNumber; }
            finally { _bypass = w1; _noNaval = w2; }
        }

        private static int CharWageOf(PartyWageModel m, CharacterObject c, bool old)
        {
            bool was = _bypass;
            _bypass = old;
            try { return m.GetCharacterWage(c); }
            finally { _bypass = was; }
        }

        internal static void Daily()
        {
            var cur = Campaign.Current;
            if (cur == null || _specs.Count == 0) return;
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _lastDay) return;
            _lastDay = day;
            try { Log.Info(DayLine(cur, day)); }
            catch (Exception e) { Stumble("linia doby", e); }
        }

        private static string DayLine(Campaign cur, int day)
        {
            var sb = new StringBuilder(3000);
            sb.Append("Lancuchy modeli BKROT (P4): dzien ").Append(day).Append(" | klucze: ").Append(Keys());

            // --- predkosc na morzu
            var sm = cur.Models.PartySpeedCalculatingModel;
            var sea = new List<MobileParty>();
            int seaAll = 0, seaShips = 0;
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive || !mp.IsCurrentlyAtSea || mp.AttachedTo != null) continue;
                seaAll++;
                if (mp.Ships != null && mp.Ships.Count > 0) seaShips++;
                if (sea.Count < 200) sea.Add(mp);
            }
            double sNow = 0, sOld = 0;
            int sN = 0;
            foreach (var mp in sea)
            {
                try
                {
                    ExplainedNumber e1, e2;
                    float a = SpeedOf(sm, mp, false, false, out e1), b = SpeedOf(sm, mp, true, false, out e2);
                    sNow += a; sOld += b; sN++;
                }
                catch (Exception e) { Stumble("linia(predkosc)", e); }
            }
            sb.Append(" | predkosc: na morzu partii ").Append(seaAll).Append(" (ze statkami ").Append(seaShips).Append(", policzone ").Append(sN).Append("): srednio teraz ")
              .Append(sN > 0 ? F((float)(sNow / sN)) : "-").Append(", bez naprawy ").Append(sN > 0 ? F((float)(sOld / sN)) : "-");
            if (sN > 0 && sOld > 0) sb.Append(" (x").Append((sNow / sOld).ToString("0.00", Inv)).Append(')');
            // probki: wodz z flota, karawana, reszta - do 3
            var picks = new List<MobileParty>();
            foreach (var mp in sea) if (picks.Count < 1 && mp.IsLordParty && mp.Ships != null && mp.Ships.Count > 0) picks.Add(mp);
            foreach (var mp in sea) if (picks.Count < 2 && mp.IsCaravan && !picks.Contains(mp)) picks.Add(mp);
            foreach (var mp in sea) if (picks.Count < 3 && !picks.Contains(mp) && mp.Ships != null && mp.Ships.Count > 0) picks.Add(mp);
            sb.Append("; probki:");
            if (picks.Count == 0) sb.Append(" -");
            foreach (var mp in picks)
            {
                try
                {
                    ExplainedNumber en, eo;
                    float a = SpeedOf(sm, mp, false, true, out en), b = SpeedOf(sm, mp, true, true, out eo);
                    string terr = "?";
                    try { terr = cur.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace).ToString(); } catch { }
                    sb.Append(" [").Append(Clean(mp.Name != null ? mp.Name.ToString() : mp.StringId)).Append(", statki ").Append(mp.Ships != null ? mp.Ships.Count : 0)
                      .Append(", ludzi ").Append(mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0).Append(", teren ").Append(terr)
                      .Append(": teraz ").Append(F(a)).Append(" = ");
                    var names = new Dictionary<string, int>();
                    bool f1 = true;
                    foreach (var ln in en.GetLines())
                    {
                        sb.Append(f1 ? "" : ", ").Append(Clean(ln.name)).Append(' ').Append(ln.number.ToString("+0.00;-0.00;0", Inv));
                        f1 = false;
                        int c; names.TryGetValue(ln.name ?? "", out c); names[ln.name ?? ""] = c + 1;
                    }
                    sb.Append("; bez naprawy ").Append(F(b)).Append("; powtorzone wpisy ").Append(names.Values.Count(v => v > 1)).Append(']');
                }
                catch (Exception e) { Stumble("linia(probka)", e); }
            }
            try
            {
                var main = MobileParty.MainParty;
                if (main != null && main.IsActive && !main.IsCurrentlyAtSea)
                {
                    ExplainedNumber e1, e2;
                    sb.Append("; lad (gracz): teraz ").Append(F(SpeedOf(sm, main, false, false, out e1))).Append(", bez naprawy ").Append(F(SpeedOf(sm, main, true, false, out e2)));
                }
                else sb.Append("; lad (gracz): -");
            }
            catch (Exception e) { Stumble("linia(lad)", e); sb.Append("; lad (gracz): blad"); }

            // --- zold
            var wm = cur.Models.PartyWageModel;
            int othP = 0, unsN = 0, unsP = 0, seaW = 0, seaCar = 0, neg = 0;
            double othNow = 0, othOld = 0, seaNow = 0, seaFull = 0;
            CharacterObject uns = null;
            try { uns = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<CharacterObject>("unsullied") : null; } catch { }
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive || mp.MemberRoster == null) continue;
                try
                {
                    if (uns != null)
                    {
                        var ro = mp.MemberRoster;
                        int n = 0;
                        for (int i = 0; i < ro.Count; i++) if (ReferenceEquals(ro.GetCharacterAtIndex(i), uns)) n += ro.GetElementNumber(i);
                        if (n > 0) { unsN += n; unsP++; }
                    }
                    bool oth = OthersRise.IsWhiteWalkerClan(mp.ActualClan) || (mp.IsGarrison && mp.CurrentSettlement != null && OthersRise.IsWhiteWalkerClan(mp.CurrentSettlement.OwnerClan));
                    bool atSea = mp.IsCurrentlyAtSea && mp.AttachedTo == null;
                    if (!oth && !atSea) continue;
                    float now = WageOf(wm, mp, false, false);
                    if (now < 0f) neg++;
                    if (oth) { othP++; othNow += now; othOld += WageOf(wm, mp, true, false); }
                    if (atSea) { seaW++; if (mp.IsCaravan) seaCar++; seaNow += now; seaFull += WageOf(wm, mp, false, true); }
                }
                catch (Exception e) { Stumble("linia(zold)", e); }
            }
            sb.Append(" | zold: Inni: partii ").Append(othP).Append(", zold teraz ").Append(othNow.ToString("0", Inv)).Append(", bez naprawy ").Append(othOld.ToString("0", Inv));
            sb.Append("; Nieskalani: ludzi ").Append(unsN).Append(" w ").Append(unsP).Append(" partiach, zold jednostki teraz ");
            if (uns != null)
            {
                try { sb.Append(CharWageOf(wm, uns, false)).Append(", bez naprawy ").Append(CharWageOf(wm, uns, true)).Append(", do ceny werbunku ").Append(PriceWage(uns)); }
                catch (Exception e) { Stumble("linia(Nieskalani)", e); sb.Append("-, bez naprawy -, do ceny werbunku -"); }
            }
            else sb.Append("-, bez naprawy -, do ceny werbunku -");
            sb.Append("; na morzu partii ").Append(seaW).Append(" (karawan ").Append(seaCar).Append("): zold teraz ").Append(seaNow.ToString("0", Inv))
              .Append(", bez obnizki morskiej ").Append(seaFull.ToString("0", Inv)).Append(", roznica ").Append((seaFull - seaNow).ToString("0", Inv)).Append(" na dobe");
            sb.Append("; ujemny zold partii ").Append(neg);
            try
            {
                var main = MobileParty.MainParty;
                if (main != null && main.IsActive) sb.Append("; gracz teraz ").Append(WageOf(wm, main, false, false).ToString("0", Inv)).Append(", bez naprawy ").Append(WageOf(wm, main, true, false).ToString("0", Inv));
            }
            catch (Exception e) { Stumble("linia(zold gracza)", e); }
            long arb = Interlocked.Read(ref _arbiter);
            sb.Append("; Wielki Ksiaze (AM-19) zadzialal od wczoraj ").Append(arb - _arbiterAt).Append(" razy");
            _arbiterAt = arb;

            // --- wzrok na morzu
            try
            {
                var vm = cur.Models.MapVisibilityModel;
                double vNow = 0, vOld = 0; int vN = 0;
                foreach (var mp in sea)
                {
                    if (vN >= 50) break;
                    bool was = _bypass;
                    try
                    {
                        _bypass = false; float a = vm.GetPartySpottingRange(mp, false).ResultNumber;
                        _bypass = true; float b = vm.GetPartySpottingRange(mp, false).ResultNumber;
                        vNow += a; vOld += b; vN++;
                    }
                    finally { _bypass = was; }
                }
                sb.Append(" | wzrok na morzu: policzone ").Append(vN).Append(", srednio teraz ").Append(vN > 0 ? F((float)(vNow / vN)) : "-").Append(", bez naprawy ").Append(vN > 0 ? F((float)(vOld / vN)) : "-");
            }
            catch (Exception e) { Stumble("linia(wzrok)", e); }

            // --- wywolania lancuchow od wczoraj
            sb.Append(" | wywolania lancucha od wczoraj:");
            for (int k = 0; k < KN; k++)
            {
                long a = Interlocked.Read(ref _calls[k * 2]), b = Interlocked.Read(ref _calls[k * 2 + 1]);
                sb.Append(k > 0 ? "," : "").Append(' ').Append(KName[k]).Append(' ').Append(a - _callsAt[k * 2]);
                if (k == (int)K.Speed || k == (int)K.Wage) sb.Append(" (+").Append(b - _callsAt[k * 2 + 1]).Append(k == (int)K.Speed ? " bazowa)" : " jednostka/werbunek)");
                _callsAt[k * 2] = a; _callsAt[k * 2 + 1] = b;
            }
            long dp = Interlocked.Read(ref _deep);
            sb.Append("; glebokosc ponad ").Append(MaxDepth).Append(": ").Append(dp - _deepAt);
            _deepAt = dp;
            sb.Append(" | potkniecia od startu sesji ").Append(Volatile.Read(ref _stumbles));
            lock (Gate)
            {
                if (_stumbleAt.Count > 0)
                {
                    sb.Append(" (");
                    bool f = true;
                    foreach (var kv in _stumbleAt) { sb.Append(f ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); f = false; }
                    sb.Append(')');
                }
            }
            return sb.Append('.').ToString();
        }
    }
}
