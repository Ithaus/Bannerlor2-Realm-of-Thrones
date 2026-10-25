using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// p9 - NAJPIERW DOZBROIC (Jeff 25.10: "powinna zanim pojdzie do bitwy AI najpierw przygotowac wojsko i dobrac pancerz, bo zolnierz taki nie nadaje sie do
    /// bitwy"). Klucz AiArmsBeforeBattle (MCM "AI Arms Before Battle"). Gotowosc R i stan "niegotowa" liczy ArmedStrength (FillPercent, histereza
    /// AiArmsBelowPercent 50 -> AiArmsReadyPercent 60; 60 = prog "chudych" partii rodu ClanFillOnce, 50 = polowa slotow wzorca pusta).
    ///
    /// NIEGOTOWA = partia lorda w regule (GarrisonKit.LordRule - takze towarzysze i partie Twojego rodu; nie gracz) poza armia, bitwa i oblezeniem, z R ponizej progu.
    ///  (a) ZAKAZ ZACZEPIANIA: postfiks DefaultMobilePartyAIModel.ShouldConsiderAttacking (DeclaredMethod - czynny NavalDLCMobilePartyAIModel przekazuje do bazy; wzor
    ///      FreeFolkRaids): niegotowa nie atakuje partii poza bandami (bandy - AiArmsBlockBanditAttacks, domyslnie wolno: rozstrzyga sila z uzbrojeniem). To samo
    ///      sprawdzenie gra robi dla dolaczenia do toczacej sie bitwy (CalculateInitiativeScoresForEnemy po kazdej wrogiej partii). Wyjatek: cel oblega osade rodu
    ///      partii albo jego bitwa toczy sie o osade rodu (obrona wlasnej osady). Ucieczka (ShouldConsiderAvoiding) bez zmian.
    ///  (b) CELE: postfiks Priority.VeryLow na CampaignEventDispatcher.AiHourlyTick (po ROT AIThinkPatch.Patch1 - Normal - i po BesiegedRelief/WatchLaw - Low).
    ///      Ruchy "jedz kupic" przez DisableForHours / SetDoNotMakeNewDecisions odpadaja: wylaczaja tez ucieczke (TickInternal pomija GetBehaviors), a
    ///      DoNotMakeNewDecisions idzie do zapisu gry - dlatego waga w ocenie celow, jak BesiegedRelief i WatchLaw. Glod - najpierw jedzenie (bez zmian).
    ///      - opcje bitwy (oblezenie, szturm, rabunek, obrona cudzej osady, poscig GoAroundParty/EngageParty za partia nie-banda) dostaja -1 (SetBehaviorScore, jak WatchLaw);
    ///      - obrona osady WLASNEGO rodu zostaje (wyjatek) - i wtedy bez wyjazdu po sprzet;
    ///      - stac go (AiGear.BudgetPreview - ten sam wzor co TryBuyCore - >= 10 sztuk po wczorajszej sredniej cenie zakupow AI, najmniej 300): najblizsze przyjazne
    ///        MIASTO droga ladowa najwyzej AiArmsTripMaxDays dob marszu (nie w wojnie, bez oblezenia, po tej samej stronie Muru, poza wytchnieniem) - GoToSettlement
    ///        z ocena max(0.5, 1.2 x najlepszej innej) + 0.01 (wzor BesiegedRelief: 0.5 to prog zmiany celu gry). Na miejscu kupuje dotychczasowa droga (wejscie:
    ///        MenPurse.OnEntered -> AiGear.TryBuy; doba postoju: AiGear.OnDailyTickParty) - nic nowego w ekonomii ("nic z niczego": placi pan i sakiewka ludzi,
    ///        zloto do kasy miasta). Po dobie w miescie bez osiagniecia progu (pusta polka albo brak zlota) - wytchnienie AiArmsTripCooldownDays dla tego miasta
    ///        i nastepne miasto w zasiegu (bez petli "jade - nic nie ma - jade"); za dlugo w drodze (2 x zasieg + 12 h) - tak samo;
    ///      - nie stac go albo brak miasta: tylko -1 opcjom bitwy - patrol i dojazd do wlasnych osad z gry zostaja; czeka, az dochod rodu da budzet
    ///        (DECYZJA Jeffa, czy pozyczka na sprzet albo zwolnienie ludzi - nie w tym kroku).
    ///  (c) ARMIE: postfiksy DefaultArmyManagementCalculationModel.CanLordCreateArmy (baza dla StrategicCampaignAI145 i BK) i BKArmyManagementModel.CanLordCreateArmy
    ///      (BK dokleja partie rodu PO bazie i patrzy tylko na liczbe >= 2): niegotowy wodz - false i pusta lista, niegotowi kandydaci wypadaja; BK MobilePartyExtensions.
    ///      IsAvailableForArmies - false (wezwania BK gracza i AI); czynny CheckPartyEligibility (SCA) - false z napisem po angielsku (okno armii gracza). Czlonek juz
    ///      w armii zostaje.
    /// Stan wyjazdow bez zapisu - po wczytaniu liczony od nowa. Liczniki do linii "Gotowosc do bitwy (p9): dzien" (ArmedStrength.Daily). Potkniecia per wywolanie.
    /// </summary>
    internal static class ArmsFirst
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double StayHours = 26.0;   // zakup przy wjezdzie i jedna doba postoju (AiGear kupuje raz na dobe)
        private const float FloorScore = 0.5f;   // najnizsza ocena wyjazdu - prog zmiany celu gry (jak BesiegedRelief.FloorBase)
        private const float Edge = 1.2f;         // ponad najlepsza inna opcje
        private const int MinNeed = 300;         // najmniejszy budzet "stac go"
        private static readonly TextObject NotArmedText = new TextObject("{=!}Its men lack arms and armour - it must arm them before joining an army.");

        private sealed class Trip { internal MobileParty P; internal Settlement Town; internal double StartH, ArriveH = -1; internal int R0; }
        private static readonly Dictionary<MBGUID, Trip> _trip = new Dictionary<MBGUID, Trip>();
        private static readonly Dictionary<string, double> _cool = new Dictionary<string, double>(StringComparer.Ordinal);   // partia|miasto -> godzina konca wytchnienia

        // latki (raz na proces)
        private static Harmony _h;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private static string _hThink = "nie wpieta", _hAttack = "nie wpieta", _hArmy = "nie wpieta", _hArmyBk = "nie wpieta", _hAvail = "nie wpieta", _hElig = "nie wpieta";
        private static MethodBase _thinkM;
        private static Func<Vec2, bool> _beyond;
        private static bool _beyondTried;

        // liczniki doby (mysl i armie - watek glowny; atak - dowolny watek, pod zamkiem)
        private static readonly object Gate = new object();
        private static int _dTrips, _dArrived, _dArmed, _dPart, _dNoGain, _dTimeout, _dOff, _dOwnDef, _dStarve, _stumbles;
        private static int _vAttackCalls;
        private static readonly HashSet<MBGUID> _vAttack = new HashSet<MBGUID>(), _vLead = new HashSet<MBGUID>(), _vMember = new HashSet<MBGUID>(),
                                                _vAvail = new HashSet<MBGUID>(), _vElig = new HashSet<MBGUID>(), _dPoor = new HashSet<MBGUID>(), _dNoTown = new HashSet<MBGUID>();

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            try { if (n <= 5 || n % 100 == 0) Log.Error("ArmsFirst." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        internal static void Reset() { _trip.Clear(); _cool.Clear(); ClearDay(); _stumbles = 0; }

        internal static void ClearDay()
        {
            _dTrips = _dArrived = _dArmed = _dPart = _dNoGain = _dTimeout = _dOff = _dOwnDef = _dStarve = 0;
            lock (Gate) { _vAttackCalls = 0; _vAttack.Clear(); _vLead.Clear(); _vMember.Clear(); _vAvail.Clear(); _vElig.Clear(); }
            _dPoor.Clear(); _dNoTown.Clear();
        }

        internal static void Forget(MobileParty mp) { if (mp != null) _trip.Remove(mp.Id); }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        internal static int TripDays(Settings s) { return Clamp(s.AiArmsTripMaxDays, 1, 14); }
        internal static int CoolDays(Settings s) { return Clamp(s.AiArmsTripCooldownDays, 0, 30); }

        private static void Note(HashSet<MBGUID> set, MobileParty mp) { if (mp == null) return; lock (Gate) set.Add(mp.Id); }

        // ------------------------------------------------------------ predykaty
        /// <summary>Niegotowa partia lorda AI (dowolny watek - tylko odczyt publikacji ArmedStrength): poza armia, bitwa i oblezeniem, klucz wlaczony.</summary>
        internal static bool Unready(MobileParty mp)
        {
            var s = Settings.Current;
            if (s == null || !s.AiArmsBeforeBattle || mp == null || mp.IsMainParty) return false;
            if (mp.Army != null || mp.MapEvent != null || mp.BesiegedSettlement != null) return false;
            return ArmedStrength.IsLow(mp);
        }

        /// <summary>Stac go na sprzet: podglad budzetu zakupow brakow (AiGear.BudgetPreview - wzor TryBuyCore) >= 10 sztuk po wczorajszej sredniej cenie zakupow AI
        /// (najmniej MinNeed). Zakupy AI wylaczone - nie stac (nikt nie kupuje).</summary>
        internal static bool CanAfford(MobileParty mp, out int budget, out int need)
        {
            budget = 0; need = MinNeed;
            try
            {
                need = Math.Max(MinNeed, 10 * Math.Max(0, AiGear.AvgPiecePriceYesterday));
                budget = AiGear.BudgetPreview(mp);
                return AiGear.On && budget >= need;
            }
            catch (Exception e) { Stumble("CanAfford", e); return false; }
        }

        private static bool OwnClanSettlement(Settlement st, Clan clan) { return st != null && clan != null && st.OwnerClan == clan; }

        /// <summary>Obrona ZAGROZONEJ osady wlasnego rodu (oblezenie albo bitwa o nia - rabunek wsi, szturm). Gra dodaje DefendSettlement takze dla osad bez
        /// zagrozenia (patrol przy swoich) - te nie sa wyjatkiem i nie wstrzymuja wyjazdu po sprzet.</summary>
        private static bool IsOwnDefend(in AIBehaviorData d, Clan clan)
        {
            if (d.AiBehavior != AiBehavior.DefendSettlement) return false;
            var st = d.Party as Settlement;
            return OwnClanSettlement(st, clan) && (st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null));
        }

        /// <summary>Opcja oceny celow, ktora szuka bitwy: oblezenie, szturm, rabunek, obrona cudzej osady, poscig za partia (bandy - wedlug klucza).</summary>
        private static bool IsBattle(in AIBehaviorData d, Clan clan, Settings s)
        {
            switch (d.AiBehavior)
            {
                case AiBehavior.BesiegeSettlement:
                case AiBehavior.AssaultSettlement:
                case AiBehavior.RaidSettlement:
                    return true;
                case AiBehavior.DefendSettlement:
                    return !OwnClanSettlement(d.Party as Settlement, clan);
                case AiBehavior.GoAroundParty:
                case AiBehavior.EngageParty:
                    var t = d.Party as MobileParty;
                    return t == null || !t.IsBandit || s.AiArmsBlockBanditAttacks;
                default:
                    return false;
            }
        }

        /// <summary>Cel przy osadzie rodu partii: oblega ja albo jego bitwa toczy sie o nia (rabunek wsi rodu, szturm) - obrona wlasnej osady.</summary>
        private static bool DefendsOwn(MobileParty party, MobileParty target)
        {
            var clan = party.ActualClan;
            if (clan == null) return false;
            if (OwnClanSettlement(target.BesiegedSettlement, clan)) return true;
            var me = target.MapEvent;
            return me != null && OwnClanSettlement(me.MapEventSettlement, clan);
        }

        private static Func<Vec2, bool> Beyond()
        {
            if (_beyondTried) return _beyond;
            _beyondTried = true;
            try
            {
                var t = AccessTools.TypeByName("ROT.Misc.ROTUtilities");
                var m = t != null ? AccessTools.Method(t, "IsBeyondTheWall", new[] { typeof(Vec2) }) : null;
                if (m != null && m.IsStatic && m.ReturnType == typeof(bool)) _beyond = (Func<Vec2, bool>)Delegate.CreateDelegate(typeof(Func<Vec2, bool>), m);
            }
            catch (Exception e) { _beyond = null; Stumble("Beyond", e); }
            return _beyond;
        }

        // ------------------------------------------------------------ (a) atak
        /// <summary>Postfiks ShouldConsiderAttacking (watki AI): niegotowa partia nie zaczepia partii poza bandami; wyjatek - obrona osady wlasnego rodu. Nie rzuca.</summary>
        public static void AttackPostfix(MobileParty __0, MobileParty __1, ref bool __result)
        {
            if (!__result || __0 == null || __1 == null) return;
            try
            {
                if (!Unready(__0)) return;
                var s = Settings.Current;
                if (__1.IsBandit && !s.AiArmsBlockBanditAttacks) return;
                if (DefendsOwn(__0, __1)) return;
                __result = false;
                lock (Gate) { _vAttackCalls++; _vAttack.Add(__0.Id); }
            }
            catch { }
        }

        // ------------------------------------------------------------ (b) cele
        /// <summary>Postfiks VeryLow CampaignEventDispatcher.AiHourlyTick: niegotowa partia lorda AI - opcje bitwy -1, wyjazd do miasta po sprzet (opis klasy). Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiArmsBeforeBattle || !party.IsLordParty || party.IsMainParty) return;
                var info = ArmedStrength.InfoOf(party);
                if (info == null || !info.Low) return;
                if (party.Army != null || party.MapEvent != null || party.BesiegedSettlement != null) return;
                if (party.CurrentSettlement != null && party.CurrentSettlement.SiegeEvent != null) return;
                if (partyThinkParams.DoNotChangeBehavior) return;
                if (party.Party.IsStarving) { _dStarve++; return; }   // najpierw jedzenie - droga gry
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                var clan = party.ActualClan;
                float other = 0f, own = -1f;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (IsOwnDefend(e.Item1, clan)) { if (e.Item2 > own) own = e.Item2; }
                    else if (e.Item2 > other) other = e.Item2;
                }
                Settlement town = null;
                if (own >= 0f) { _dOwnDef++; Forget(party); }   // obrona zagrozonej osady wlasnego rodu - wyjatek, bez wyjazdu (ta opcja zostaje, IsBattle jej nie gasi)
                else
                {
                    int b, need;
                    if (CanAfford(party, out b, out need)) town = TripTown(party, info, CampaignTime.Now.ToHours, s);
                    else { _dPoor.Add(party.Id); Forget(party); }
                }
                // opcje bitwy -1 (kopia listy - SetBehaviorScore zmienia elementy)
                var copy = new List<(AIBehaviorData, float)>(scores.Count);
                for (int i = 0; i < scores.Count; i++) copy.Add(scores[i]);
                foreach (var it in copy)
                {
                    if (!(it.Item2 > -1f)) continue;
                    var d = it.Item1;
                    if (!IsBattle(d, clan, s)) continue;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                    _dOff++;
                }
                if (town == null) return;
                float nv = Math.Max(FloorScore, Edge * other) + 0.01f;
                bool set = false;
                for (int i = 0; i < scores.Count; i++)
                {
                    var d = scores[i].Item1;
                    if (d.AiBehavior != AiBehavior.GoToSettlement || d.WillGatherArmy || d.NavigationType != MobileParty.NavigationType.Default || !ReferenceEquals(d.Party, town)) continue;   // droga ladowa (Nearest)
                    if (scores[i].Item2 < nv) partyThinkParams.SetBehaviorScore(in d, nv);
                    set = true;
                    break;
                }
                if (!set)
                {
                    var a = new AIBehaviorData(town, AiBehavior.GoToSettlement, MobileParty.NavigationType.Default, false, false, false);
                    partyThinkParams.AddBehaviorScore((a, nv));
                }
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        private static string Key(MobileParty mp, Settlement st) { return mp.StringId + "|" + st.StringId; }

        private static bool Cooling(MobileParty mp, Settlement st, double now) { double u; return _cool.TryGetValue(Key(mp, st), out u) && u > now; }

        private static void Cool(MobileParty mp, Settlement st, double now, Settings s)
        {
            int d = CoolDays(s);
            if (d > 0 && st != null) _cool[Key(mp, st)] = now + d * 24.0;
        }

        private static bool Valid(MobileParty mp, Settlement st, double now)
        {
            return st != null && st.IsTown && st.Town != null && st.ItemRoster != null && !st.IsUnderSiege
                   && !FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction) && !Cooling(mp, st, now);
        }

        /// <summary>Cel wyjazdu po sprzet: biezacy (do konca wizyty) albo najblizsze wazne miasto w zasiegu; null - brak.</summary>
        private static Settlement TripTown(MobileParty party, ArmedStrength.Info info, double now, Settings s)
        {
            Trip t;
            if (_trip.TryGetValue(party.Id, out t))
            {
                if (!Valid(party, t.Town, now)) _trip.Remove(party.Id);
                else
                {
                    bool inTown = party.CurrentSettlement == t.Town;
                    if (inTown && t.ArriveH < 0) { t.ArriveH = now; _dArrived++; }
                    bool done = t.ArriveH >= 0 && (!inTown || now - t.ArriveH >= StayHours);
                    bool late = t.ArriveH < 0 && now - t.StartH > TripDays(s) * 48.0 + 12.0;
                    if (!done && !late) return t.Town;
                    // wizyta skonczona, a partia dalej niegotowa (albo za dlugo w drodze) - wytchnienie tego miasta i nastepne w zasiegu
                    if (late) _dTimeout++; else if (info.R > t.R0) _dPart++; else _dNoGain++;
                    Cool(party, t.Town, now, s);
                    _trip.Remove(party.Id);
                }
            }
            var town = Nearest(party, now, s);
            if (town == null) { _dNoTown.Add(party.Id); return null; }
            _trip[party.Id] = new Trip { P = party, Town = town, StartH = now, R0 = info.R };
            _dTrips++;
            return town;
        }

        /// <summary>Najblizsze droga ladowa wazne miasto w zasiegu AiArmsTripMaxDays dob marszu (EstimatedAverageLordPartySpeed x 24 h), po tej samej stronie Muru.</summary>
        private static Settlement Nearest(MobileParty party, double now, Settings s)
        {
            if (party.IsCurrentlyAtSea) return null;
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.MapDistanceModel : null;
            if (model == null) return null;
            float range = TripDays(s) * 24f * Math.Max(0.5f, c.EstimatedAverageLordPartySpeed);
            var at = party.CurrentSettlement;
            var pos = party.GetPosition2D;
            var bw = Beyond();
            bool pBeyond = false;
            if (bw != null) { try { pBeyond = bw(pos); } catch { bw = null; } }
            Settlement best = null; float bd = float.MaxValue;
            foreach (var tw in Town.AllTowns)
            {
                var st = tw != null ? tw.Settlement : null;
                if (st == null || !Valid(party, st, now)) continue;
                if (pos.Distance(st.GetPosition2D) > range) continue;   // w linii prostej dalej niz zasieg - droga tym bardziej
                float d;
                if (at == st) d = 0f;
                else
                {
                    float lr;
                    d = at != null ? model.GetDistance(at, st, false, false, MobileParty.NavigationType.Default)
                                   : model.GetDistance(party, st, false, MobileParty.NavigationType.Default, out lr);
                    if (CartGuard.NoLandRoad(d)) continue;
                }
                if (d > range || d >= bd) continue;
                if (bw != null) { try { if (bw(st.GetPosition2D) != pBeyond) continue; } catch { } }
                best = st; bd = d;
            }
            return best;
        }

        internal static string TargetName(MobileParty mp)
        {
            try { Trip t; return mp != null && _trip.TryGetValue(mp.Id, out t) && t.Town != null ? t.Town.Name.ToString() + (t.ArriveH >= 0 ? " (na miejscu)" : "") : "-"; }
            catch { return "?"; }
        }

        /// <summary>Godzina (z ArmedStrength.Hourly): wyjazdy partii juz gotowych (dozbrojone), w armii albo nieczynnych - koniec; wygasle wytchnienia.</summary>
        internal static void Hourly()
        {
            if (_trip.Count > 0)
            {
                List<MBGUID> rm = null;
                foreach (var kv in _trip)
                {
                    var t = kv.Value; var p = t.P;
                    bool end = p == null || !p.IsActive || p.Army != null;
                    if (!end && !ArmedStrength.IsLow(p)) { _dArmed++; end = true; }
                    if (end) { if (rm == null) rm = new List<MBGUID>(); rm.Add(kv.Key); }
                }
                if (rm != null) foreach (var k in rm) _trip.Remove(k);
            }
            if (_cool.Count > 0)
            {
                double now = CampaignTime.Now.ToHours;
                List<string> rm = null;
                foreach (var kv in _cool) if (kv.Value <= now) { if (rm == null) rm = new List<string>(); rm.Add(kv.Key); }
                if (rm != null) foreach (var k in rm) _cool.Remove(k);
            }
        }

        // ------------------------------------------------------------ (c) armie
        /// <summary>Postfiks CanLordCreateArmy (Default - baza SCA i BK; BK - po doklejeniu partii rodu): niegotowy wodz - false i pusta lista; niegotowi kandydaci wypadaja.</summary>
        public static void CreatePostfix(MobileParty __0, ref MBList<MobileParty> __1, ref bool __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiArmsBeforeBattle) return;
                if (Unready(__0))
                {
                    if (__result || (__1 != null && __1.Count > 0)) Note(_vLead, __0);
                    __result = false;
                    if (__1 != null) __1.Clear();
                    return;
                }
                if (__1 == null || __1.Count == 0) return;
                for (int i = __1.Count - 1; i >= 0; i--)
                {
                    var m = __1[i];
                    if (m != null && Unready(m)) { __1.RemoveAt(i); Note(_vMember, m); }
                }
                if (__result && __1.Count == 0) __result = false;
            }
            catch (Exception e) { Stumble("CreatePostfix", e); }
        }

        /// <summary>Postfiks BK MobilePartyExtensions.IsAvailableForArmies: niegotowa partia nie idzie na wezwanie (BK CallBannersGoal gracza i AI, partie rodu BK).</summary>
        public static void AvailPostfix(MobileParty __0, ref bool __result)
        {
            if (!__result) return;
            try { if (Unready(__0)) { __result = false; Note(_vAvail, __0); } } catch (Exception e) { Stumble("AvailPostfix", e); }
        }

        /// <summary>Postfiks czynnego CheckPartyEligibility (okno armii gracza): niegotowa partia - false z napisem po angielsku.</summary>
        public static void EligPostfix(MobileParty __0, ref TextObject __1, ref bool __result)
        {
            if (!__result) return;
            try { if (Unready(__0)) { __result = false; __1 = NotArmedText; Note(_vElig, __0); } } catch (Exception e) { Stumble("EligPostfix", e); }
        }

        // ------------------------------------------------------------ start sesji i linia
        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "-";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "-";
                var o = info.Owners.Distinct().ToList();
                return o.Count > 0 ? string.Join("/", o.ToArray()) : "-";
            }
            catch { return "?"; }
        }

        private static string Patch(MethodInfo m, string what, Type t, string post, int prio)
        {
            if (m == null) return "BRAK (" + what + ")";
            if (_patched.Contains(m)) return "TAK (juz wczesniej w tym procesie)";
            _h.Patch(m, postfix: new HarmonyMethod(t, post) { priority = prio });
            _patched.Add(m);
            return "TAK";
        }

        /// <summary>Latki (raz na proces; w kampanii - modele armii BK czytaja singletony BK). Zwraca opis do linii startu ArmedStrength.</summary>
        internal static string SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.armsfirst");
            var me = typeof(ArmsFirst);
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _thinkM = m;
                _hThink = Patch(m, "CampaignEventDispatcher.AiHourlyTick", me, nameof(ThinkPostfix), Priority.VeryLow);
            }
            catch (Exception e) { _hThink = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Think", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultMobilePartyAIModel), "ShouldConsiderAttacking", new[] { typeof(MobileParty), typeof(MobileParty) });
                _hAttack = Patch(m, "DefaultMobilePartyAIModel.ShouldConsiderAttacking", me, nameof(AttackPostfix), Priority.Last);
            }
            catch (Exception e) { _hAttack = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Attack", e); }
            var armyArgs = new[] { typeof(MobileParty), typeof(MBList<MobileParty>).MakeByRefType() };
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultArmyManagementCalculationModel), "CanLordCreateArmy", armyArgs);
                _hArmy = Patch(m, "DefaultArmyManagementCalculationModel.CanLordCreateArmy", me, nameof(CreatePostfix), Priority.Last);
            }
            catch (Exception e) { _hArmy = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Army", e); }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKArmyManagementModel");
                var m = t != null ? AccessTools.DeclaredMethod(t, "CanLordCreateArmy", armyArgs) : null;
                _hArmyBk = t == null ? "BRAK (BK nieobecny)" : Patch(m, "BKArmyManagementModel.CanLordCreateArmy", me, nameof(CreatePostfix), Priority.Last);
            }
            catch (Exception e) { _hArmyBk = "wyjatek " + e.GetType().Name; Stumble("SessionStart.ArmyBk", e); }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Extensions.MobilePartyExtensions");
                var m = t != null ? AccessTools.DeclaredMethod(t, "IsAvailableForArmies", new[] { typeof(MobileParty) }) : null;
                _hAvail = t == null ? "BRAK (BK nieobecny)" : Patch(m, "MobilePartyExtensions.IsAvailableForArmies", me, nameof(AvailPostfix), Priority.Last);
            }
            catch (Exception e) { _hAvail = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Avail", e); }
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ArmyManagementCalculationModel : null;
                var m = model != null ? PatchTarget.OfActive(model, "CheckPartyEligibility", new[] { typeof(MobileParty), typeof(TextObject).MakeByRefType() }, "armia gracza: gotowosc czlonka (p9)") : null;
                _hElig = model == null ? "BRAK (model armii nieznany)" : Patch(m, model.GetType().FullName + ".CheckPartyEligibility", me, nameof(EligPostfix), Priority.Last)
                         + (m != null ? " (" + m.DeclaringType.Name + ")" : "");
            }
            catch (Exception e) { _hElig = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Elig", e); }
            return "najpierw dozbroic: mysl AI (CampaignEventDispatcher.AiHourlyTick, postfiks VeryLow) " + _hThink + ", wlasciciele latek celu: " + Owners(_thinkM)
                   + "; atak (DefaultMobilePartyAIModel.ShouldConsiderAttacking) " + _hAttack
                   + "; armia gry (DefaultArmyManagementCalculationModel.CanLordCreateArmy) " + _hArmy + "; armia BK (BKArmyManagementModel.CanLordCreateArmy) " + _hArmyBk
                   + "; wezwania BK (MobilePartyExtensions.IsAvailableForArmies) " + _hAvail + "; okno armii gracza (CheckPartyEligibility) " + _hElig
                   + "; Mur (ROT IsBeyondTheWall) " + (Beyond() != null ? "TAK" : "BRAK");
        }

        /// <summary>Czesc linii doby "Gotowosc do bitwy (p9)" (ArmedStrength.Daily).</summary>
        internal static string DayText()
        {
            int a, aCalls, lead, mem, avail, elig;
            lock (Gate) { a = _vAttack.Count; aCalls = _vAttackCalls; lead = _vLead.Count; mem = _vMember.Count; avail = _vAvail.Count; elig = _vElig.Count; }
            int onWay = 0, there = 0;
            foreach (var t in _trip.Values) { if (t.ArriveH >= 0) there++; else onWay++; }
            return "jada po sprzet: nowe wyjazdy " + _dTrips + ", teraz w drodze " + onWay + " i na miejscu " + there + ", dojechaly " + _dArrived + ", dozbrojone " + _dArmed
                   + ", po wizycie czesciowo " + _dPart + " i bez poprawy " + _dNoGain + ", za dlugo w drodze " + _dTimeout + ", bez miasta w zasiegu " + _dNoTown.Count
                   + " partii, w wytchnieniu " + _cool.Count + " par partia-miasto"
                   + " | odmowy: atak " + a + " partii (" + aCalls + " razy), opcje bitwy zgaszone " + _dOff + " (mysli bez pieniedzy " + _dPoor.Count + " partii)"
                   + ", obrona wlasnej osady zostawiona " + _dOwnDef + ", glod (bez zmian) " + _dStarve
                   + ", tworzenie armii " + lead + " partii, kandydaci armii " + mem + ", wezwania BK " + avail + ", okno armii gracza " + elig
                   + " | potkniecia ArmsFirst " + _stumbles;
        }
    }
}
