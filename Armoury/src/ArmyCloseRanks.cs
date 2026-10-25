using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m118 - SZYK NA POSTOJU (Jeff 25.10: "sciagaj figurki do wodza na postoju, bo to jest ta sama armia a nie dochodzacy lordowie"). Klucz MCM
    /// ArmyClosesRanksAtHalt (domyslnie tak). Tylko obraz: miejsce figurki doczepionej druzyny w szyku armii, ktora stoi na NASZYM postoju.
    ///
    /// JAK GRA STAWIA FIGURKI DOCZEPIONYCH (dekompilacja 1.4.8, TaleWorlds.CampaignSystem):
    ///   figurka = Position + EventPositionAdder + ArmyPositionAdder (MobileParty.VisualPosition2DWithoutError); logiczna Position doczepionej = pozycja
    ///   wodza (TryToMoveThePartyWithCurrentTickMoveData). Co klatke, gdy doczepiona jest "w ruchu" (variables.IsMoving = IsMoving || wodz.IsMoving),
    ///   DoUpdatePosition prowadzi figurke do miejsca w szyku: wodz.NextTargetPosition + facing.TransformToParentUnitF(Army.GetRelativePositionForParty)
    ///   (facing = kierunek do NextTargetPosition, przy |..|^2 < 0.0025 - Bearing wodza), predkosc = predkosc wodza x clamp(1 + ..., 0.7, 1.3).
    ///   DLACZEGO NIE ZAPIS ArmyPositionAdder (projekt z rozpoznania): AI doczepionej nie mysli (MobilePartyAi.TickInternal:444 pomija doczepione),
    ///   wiec jej MoveTargetPoint zostaje z chwili doczepienia, a Position jedzie z wodzem - po pierwszym marszu IsMoving (Position != MoveTargetPoint)
    ///   jest juz zawsze prawda, takze gdy armia stoi. Gra prowadzi wiec figurke do jej miejsca w szyku w kazdej klatce (lista "moving" w
    ///   CampaignTickCacheDataStore.ParallelArrangePartyIndices) i kazdy nasz zapis addera cofnelaby w nastepnej klatce.
    ///   DLACZEGO ROZJECHANE (zrzut Jeffa, armia Clegane'a): SetMoveModeHold (ArmyRestDay, NightRest) nie rusza NextTargetPosition, a AI wodza jest u nas
    ///   wylaczone (DisableForHours), wiec SetNavigationModeHold (jedyne miejsce, ktore robi NextTargetPosition = Position) nie przychodzi - szyk gry
    ///   stoi wokol NASTEPNEGO PUNKTU DROGI wodza, nie wokol wodza (figurki 1.5-2 jedn. w bok, na kierunku marszu).
    ///
    /// ZMIANA: postfiks Army.GetRelativePositionForParty (wola go tylko MobileParty.DoUpdatePosition i MobileParty.CalculateSpeed - sprawdzone w grze,
    ///   SandBox.View i NavalDLC; zaden z ~40 modow go nie wola ani nie lata). Dla armii na naszym postoju zwraca
    ///   facing.TransformToLocalUnitF(Pozycja wodza - NextTargetPosition) + k x miejsce gry, czyli miejsce = Pozycja wodza + facing.T(k x miejsce gry):
    ///   szyk gry (ta sama funkcja, te same rzedy, ten sam staly rozrzut RandomValue druzyny) sciesniony k razy i postawiony na wodzu. Gra sama prowadzi
    ///   figurki (plynnie, jej predkoscia, z animacja marszu) i sama trzyma je na miejscu. Nic nie zapisujemy - ani addera, ani pozycji, ani rozkazow.
    ///   Przesuniecie bazy wchodzi do obu wywolan tak samo jak w grze (w grze DoUpdatePosition liczy od NextTargetPosition, a CalculateSpeed i obrot
    ///   kierunku od Pozycji wodza - ta sama roznica "Pozycja - NextTargetPosition", tylko po drugiej stronie), wiec zbieznosc ruchu jak w grze.
    ///   SKALA: k = clamp(R0 / najdalsze miejsce w szyku gry, KMin 0.5, 1); R0 = EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty (0.5 -
    ///   odleglosc, na ktorej gra uznaje dwie druzyny za "w jednym miejscu", czytana z modelu w biegu). KMin 0.5 = polowa odstepow gry (bok 0.5 -> 0.25,
    ///   rzad 0.375 -> 0.19) to JEDYNA liczba bez zrodla w modelu gry - dobrana na oko (figurka PartyScale 0.3, z koniem ok. 0.7 jedn. dlugosci - ciasniej
    ///   wchodza na siebie; niesprawdzone w grze), DO OCENY JEFFA na zrzucie. R0 decyduje tylko przy 2-3 doczepionych; od 4 druzyn k = KMin zawsze, czyli
    ///   w praktyce "polowa szyku gry". Symulacja szyku gry z rozrzutem: 2 doczepione 0.56 -> 0.50, 4: 1.01 -> 0.50,
    ///   6: 1.26 -> 0.63, 10: 1.61 -> 0.80, 15: 2.00 -> 1.00, 30: 2.84 -> 1.42 jedn. (najdalsza figura) - do 30 druzyn w promieniu zamku ROT
    ///   (srodek -> brama, ROT-Map: mediana 1.89, p25 1.50).
    /// POSTOJ = jednoczesnie: wodz armii czynny, nie doczepiony, bez bitwy (MapEvent), poza osada, bez obozu oblezenia i oblezenia, nie na morzu, bez
    ///   przejscia lad/morze, stoi (IsMoving == false), ma doczepione druzyny, nie Nieumarly; oraz: armia AI - ArmyRestDay.Resting (dzien odpoczynku)
    ///   albo NightRest.FieldCampHolds (oboz nocy w polu, legowisko, sen dlugu); armia gracza - NightRest.PlayerCamped (namiot). Alarm nocy i osada
    ///   (NightRest._alarmed / _townHold) celowo nie: alarm = wodz moze ruszyc, w twierdzy gra sama zeruje adder.
    ///   Skan co 0.1 h gry (jak straznik ArmyRestDay) wykrywa POCZATEK postoju - raz liczy k, raz zapisuje liczniki i przyklad. Jeden postoj = wodz w tym
    ///   samym miejscu (+- Drill.RestStep 0.35, prog "ruszyl sie" ksiegi snu i musztry): oboz nocy -> dzien odpoczynku o swicie i chwilowe zejscie z Hold
    ///   (cudzy rozkaz cofniety przez straznika) to ten sam postoj. Postfiks sprawdza dodatkowo w kazdym wywolaniu, czy wodz nie ruszyl (IsMoving, bitwa,
    ///   morze, przejscie, oboz oblezenia) - armia, ktora rusza, od razu wraca do szyku gry, a gra sama prowadzi druzyny do zwyklych miejsc (predkosc
    ///   0.7-1.3 x wodza; figury stoja blizej wodza niz ich miejsce, wiec nie zostaja w tyle).
    ///   Druzyna gracza doczepiona do armii AI - bez zmian (kamera i ruch gracza zostaja w rekach gry). Druzyny dochodzace (AttachedTo == null) - bez zmian
    ///   (gra nie liczy dla nich szyku).
    ///   DRUZYNY STOJACE (recenzja 25.10): doczepiona w czasie postoju (albo armia zebrana na stojaco) ma MoveTargetPoint = pozycja stojacego wodza
    ///   (EscortParty/JoinParty: MoveTargetPoint = Position celu), wiec po pierwszym kroku Position == MoveTargetPoint, IsMoving false i gra jej nie prowadzi
    ///   (lista stacjonarnych - TickForStationaryMobileParty nie wola TryToMove) - stalaby tam, gdzie sie doczepila (<= ok. 1.22 jedn., prog Army.Tick).
    ///   Jeff: "to jest ta sama armia" - wiec TYLKO takie druzyny (IsMoving false, wodz stoi) dociagamy sami: co skan (0.1 h gry) krok ArmyPositionAdder
    ///   (prywatny setter, jeden MethodInfo sprawdzony przy latce) w strone TEGO SAMEGO miejsca, do ktorego gra prowadzi druzyny "w ruchu"
    ///   (wodz.NextTargetPosition + facing.T(GetRelativePositionForParty) - z postfiksem: wodz + facing.T(k x miejsce gry)), dlugosc kroku = predkosc
    ///   wodza x dt gry od ostatniego skanu (gra prowadzi doczepione 0.7-1.3 x predkosci wodza), nie dalej niz cel. Gra tych druzyn nie rusza, dopoki
    ///   wodz stoi (zapis nie jest cofany); gdy wodz ruszy, TryToMove liczy od figurki (Position := wodz, adder += krok) - bez skoku. Adder zapisuje gra
    ///   (SaveableProperty 1005). Druzyn "w ruchu" nie dotykamy (gra trzyma je sama). Brak settera - dociaganie wylaczone na sesje z linia logu.
    /// WATKI: postfiks biegnie w watkach ruchu gry (TWParallel.For w CampaignTickCacheDataStore.RealTick) - czyta tylko pola wodza (nikt ich wtedy nie pisze:
    ///   wodzowie ruszaja w osobnej fazie przed druzynami) i slownik k publikowany kopia przy zmianie (volatile, nigdy modyfikowany po publikacji).
    ///   Wyjatek postfiksu - licznik (Interlocked), wynik gry bez zmian; log pierwszego wyjatku z watku glownego. Bez globalnego wylacznika.
    /// Latka: wlasna instancja Harmony, raz na proces, w kampanii (pierwszy tick mapy z kluczem); cel przez AccessTools.DeclaredMethod na Army (metoda
    ///   zadeklarowana w Army - pulapka CLAUDE.md 7 "dziedziczona" nie dotyczy); sukces sprawdzony w Harmony.GetPatchInfo (finalizer BKROTPatch polyka
    ///   wyjatki Patch). Brak latki = funkcja wylaczona na sesje z linia logu (jedyny wylacznik - brak API, nie potkniecie).
    /// Zapis: nic wlasnego. ArmyPositionAdder zapisuje gra (SaveableProperty 1005) - po wczytaniu figurki stoja tam, gdzie staly; skan liczy postoj od nowa.
    /// Log: "Szyk na postoju (m118):" - linia latki, do 3 przykladow poczatku i do 3 pomiarow po 2 h na dobe, linia doby (armie, druzyny, odleglosci
    ///   figurki od wodza przed / cel / po 2 h, baza szyku gry od wodza, potkniecia). Bez linii co klatke.
    /// </summary>
    internal static class ArmyCloseRanks
    {
        private const string HarmonyId = "com.jeff.armoury.armycloseranks";
        private const string P = "Szyk na postoju (m118): ";
        private const double ScanStep = 0.1;        // h gry - skan postojow (jak straznik ArmyRestDay)
        private const float KMin = 0.5f;            // polowa odstepow szyku gry - liczba na oko, bez zrodla w modelu gry (do oceny Jeffa na zrzucie)
        private const double MeasureAfterH = 2.0;   // pomiar "po" (h gry od poczatku postoju; 1 h gry = 0.83 s realnej przy x1 - z MinimumSpeed 1 x 0.7 to >= 1.1 jedn. drogi)
        private const double ResumeH = 1.0;         // h gry przerwy, po ktorych postoj w tym samym miejscu liczy sie jako nowy (godzina ksiegi - jak takt ArmyRestDay)
        private const int ExMax = 3;                // przykladow na dobe
        private const byte KindAi = 0, KindNight = 1, KindPlayer = 2;

        private sealed class Halt
        {
            public Vec2 At;
            public float K;
            public bool On, Measured, Ex;
            public byte Kind;
            public CampaignTime Start, OffAt;
        }

        private static readonly Dictionary<Army, Halt> _halts = new Dictionary<Army, Halt>();
        private static readonly HashSet<Army> _seen = new HashSet<Army>();
        private static readonly List<Army> _gone = new List<Army>();
        private static readonly List<Vec2> _rels = new List<Vec2>();
        private static readonly Dictionary<Army, float> Empty = new Dictionary<Army, float>();
        // armie na postoju -> k; publikowane kopia przy kazdej zmianie, czytane bez blokady z watkow ruchu gry (postfiks)
        private static volatile Dictionary<Army, float> _tight = Empty;
        private static CampaignTime _lastScan = CampaignTime.Zero;
        private static float _dtAcc;                // dt gry od ostatniego skanu (krok dociagania stojacych = predkosc wodza x dt, jak ruch gry)
        private static bool _hookTried, _hooked;
        private static string _hookNote = "nie probowano";
        private static Action<MobileParty, Vec2> _setAdder;   // prywatny setter MobileParty.ArmyPositionAdder (null - stojacych nie dociagamy)
        private static string _adderNote = "nie probowano";
        private static int _stumbles;
        private static int _postStumbles;           // Interlocked - watki ruchu gry
        private static Exception _postErr;          // pierwszy wyjatek postfiksu (log z watku glownego)
        private static bool _postErrLogged;
        private static int _logDay = -1, _dEx, _dExM;
        // liczniki doby
        private static int _dAi, _dNight, _dPlayer, _dResumed, _dParties, _dLed, _dStill, _dMeasured, _dMeasuredParties, _dPullSteps, _dPullDone;
        private static float _dBeforeSum, _dBeforeMax, _dTargetSum, _dTargetMax, _dAfterSum, _dAfterMax, _dBaseSum, _dBaseMax;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Reset()
        {
            _halts.Clear(); _seen.Clear(); _gone.Clear(); _rels.Clear();
            _tight = Empty;
            _lastScan = CampaignTime.Zero;
            _dtAcc = 0f;
            _logDay = -1;
            ResetDay();
        }

        private static void ResetDay()
        {
            _dEx = _dExM = 0;
            _dAi = _dNight = _dPlayer = _dResumed = _dParties = _dLed = _dStill = _dMeasured = _dMeasuredParties = _dPullSteps = _dPullDone = 0;
            _dBeforeSum = _dBeforeMax = _dTargetSum = _dTargetMax = _dAfterSum = _dAfterMax = _dBaseSum = _dBaseMax = 0f;
        }

        private static void Stumble(string where, Army a, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0)
                Log.Error("ArmyCloseRanks." + where + " (" + Who(a) + ", potkniecie " + _stumbles + ")", e);
        }

        // ------------------------------------------------------------ latka (raz na proces, w kampanii)
        private static void EnsureHook()
        {
            _hookTried = true;
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(Army), "GetRelativePositionForParty", new[] { typeof(MobileParty), typeof(Vec2) });
                if (m == null) { _hookNote = "BRAK (Army.GetRelativePositionForParty)"; }
                else
                {
                    new Harmony(HarmonyId).Patch(m, postfix: new HarmonyMethod(typeof(ArmyCloseRanks), nameof(RelPostfix)));
                    bool ok = false;
                    var info = Harmony.GetPatchInfo(m);
                    if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == HarmonyId) { ok = true; break; }
                    _hooked = ok;
                    _hookNote = ok ? "wpieta" : "BRAK (latka nie weszla - slad w BKROTLog.txt)";
                }
            }
            catch (Exception e) { _hooked = false; _hookNote = "BRAK (" + e.GetType().Name + ")"; Log.Error("ArmyCloseRanks.EnsureHook", e); }
            // druzyny stojace (gra ich nie prowadzi): prywatny setter ArmyPositionAdder, zadeklarowany w MobileParty - jeden MethodInfo na sesje
            _setAdder = null;
            try
            {
                var set = AccessTools.PropertySetter(typeof(MobileParty), "ArmyPositionAdder");
                if (set == null || set.DeclaringType != typeof(MobileParty)) _adderNote = "BRAK (setter MobileParty.ArmyPositionAdder) - stojace druzyny zostaja, gdzie staly";
                else { _setAdder = AccessTools.MethodDelegate<Action<MobileParty, Vec2>>(set); _adderNote = "jest - stojace druzyny dociagane krokami"; }
            }
            catch (Exception e) { _setAdder = null; _adderNote = "BRAK (" + e.GetType().Name + ") - stojace druzyny zostaja, gdzie staly"; Log.Error("ArmyCloseRanks.EnsureHook(setter)", e); }
            Log.Info(P + "postfiks Army.GetRelativePositionForParty - " + _hookNote + (_hooked ? "" : ", funkcja wylaczona na sesje")
                     + "; setter ArmyPositionAdder - " + _adderNote + ".");
        }

        /// <summary>Postfiks Army.GetRelativePositionForParty (watki ruchu gry). Armia na naszym postoju: miejsce = Pozycja wodza + facing.T(k x miejsce gry)
        /// (gra dodaje wynik do wodz.NextTargetPosition - stad przesuniecie bazy). Nie rzuca; nic nie pisze poza __result.</summary>
        public static void RelPostfix(Army __instance, MobileParty mobileParty, Vec2 armyFacing, ref Vec2 __result)
        {
            var map = _tight;
            if (map.Count == 0) return;
            try
            {
                float k;
                if (__instance == null || !map.TryGetValue(__instance, out k)) return;
                if (mobileParty == null || mobileParty.IsMainParty) return;
                var L = __instance.LeaderParty;
                if (L == null || L.MapEvent != null || L.BesiegerCamp != null || L.IsCurrentlyAtSea || L.IsTransitionInProgress || L.IsMoving) return;
                Vec2 off = L.Position.ToVec2() - L.NextTargetPosition.ToVec2();
                Vec2 r = armyFacing.TransformToLocalUnitF(off) + __result * k;
                if (!r.IsValid) return;
                __result = r;
            }
            catch (Exception e)
            {
                if (Interlocked.Increment(ref _postStumbles) == 1) _postErr = e;
            }
        }

        // ------------------------------------------------------------ skan co 0.1 h gry (ArmouryBehavior TickEvent)
        internal static void OnTick(float dt)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (!s.ArmyClosesRanksAtHalt)
                {
                    // wylaczone w trakcie - armie od razu wracaja do szyku gry (gra sama prowadzi figurki), niczego nie cofamy
                    if (_halts.Count > 0 || _tight.Count > 0) { _halts.Clear(); _tight = Empty; }
                    return;
                }
                if (!_hookTried) EnsureHook();
                if (!_hooked) return;
                if (dt > 0f) _dtAcc += dt;
                var now = CampaignTime.Now;
                double dh = (now - _lastScan).ToHours;
                if (dh < ScanStep && dh >= 0) return;
                _lastScan = now;
                float acc = _dtAcc;
                _dtAcc = 0f;
                FlushDay((int)now.ToDays);
                Scan(now, acc);
                if (_postErr != null && !_postErrLogged)
                {
                    _postErrLogged = true;
                    Log.Error("ArmyCloseRanks.RelPostfix (watek ruchu gry - liczone, miejsce w szyku jak w grze)", _postErr);
                }
            }
            catch (Exception e) { Stumble("OnTick", null, e); }
        }

        private static void Scan(CampaignTime now, float dtAcc)
        {
            bool changed = false;
            _seen.Clear();
            float touch = 0.5f;
            try { touch = Campaign.Current.Models.EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty; } catch { }
            foreach (var kg in Kingdom.All)
            {
                if (kg == null || kg.IsEliminated) continue;
                var armies = kg.Armies;
                for (int i = 0; i < armies.Count; i++)
                {
                    var a = armies[i];
                    if (a == null) continue;
                    try { if (One(a, now, touch)) changed = true; }
                    catch (Exception e) { Stumble("Scan", a, e); }
                }
            }
            // armie rozwiazane - wpis znika
            _gone.Clear();
            foreach (var kv in _halts) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            for (int i = 0; i < _gone.Count; i++)
            {
                Halt h;
                if (_halts.TryGetValue(_gone[i], out h) && h.On) changed = true;
                _halts.Remove(_gone[i]);
            }
            if (changed) Publish();
            // po publikacji (postfiks juz liczy szyk na wodzu): druzyny stojace, ktorych gra nie prowadzi - krok w strone miejsca
            if (_setAdder == null || dtAcc <= 0f) return;
            foreach (var kv in _halts)
            {
                if (!kv.Value.On) continue;
                try { Pull(kv.Key, dtAcc); }
                catch (Exception e) { Stumble("Pull", kv.Key, e); }
            }
        }

        /// <summary>Druzyny doczepione, ktore stoja (IsMoving false - gra ich nie prowadzi, bo Position == MoveTargetPoint): krok ArmyPositionAdder
        /// w strone miejsca, do ktorego gra prowadzi druzyny "w ruchu" (ten sam wzor co MobileParty.DoUpdatePosition, z postfiksem: wodz + facing.T(k x
        /// miejsce gry)); krok = predkosc wodza x dt gry od ostatniego skanu, nie dalej niz cel. Druzyn "w ruchu" nie dotyka.</summary>
        private static void Pull(Army a, float dtAcc)
        {
            var L = a.LeaderParty;
            if (L == null || !L.IsActive || L.IsMoving || L.MapEvent != null || L.CurrentSettlement != null || L.BesiegerCamp != null
                || L.IsCurrentlyAtSea || L.IsTransitionInProgress) return;
            var att = L.AttachedParties;
            if (att.Count == 0) return;
            float reach = L.Speed * dtAcc;
            if (!(reach > 0f) || float.IsInfinity(reach)) return;
            var F = Facing(L);
            Vec2 baseAt = L.NextTargetPosition.ToVec2();
            for (int i = 0; i < att.Count; i++)
            {
                var p = att[i];
                if (p == null || !p.IsActive || p.IsMainParty || p.AttachedTo != L || p.IsMoving) continue;
                if (p.MapEvent != null || p.CurrentSettlement != null || p.BesiegedSettlement != null || p.IsCurrentlyAtSea || p.IsTransitionInProgress) continue;
                Vec2 target = baseAt + F.TransformToParentUnitF(a.GetRelativePositionForParty(p, F));
                Vec2 d = target - p.VisualPosition2DWithoutError;
                float dist = d.Length;
                if (!(dist > 0.001f) || float.IsInfinity(dist)) continue;    // juz na miejscu (albo NaN)
                float step = reach < dist ? reach : dist;
                Vec2 add = p.ArmyPositionAdder + d * (step / dist);
                if (!add.IsValid) continue;
                _setAdder(p, add);
                _dPullSteps++;
                if (step >= dist) _dPullDone++;
            }
        }

        private static void Publish()
        {
            var d = new Dictionary<Army, float>();
            foreach (var kv in _halts) if (kv.Value.On) d[kv.Key] = kv.Value.K;
            _tight = d.Count == 0 ? Empty : d;
        }

        /// <summary>Jedna armia: zwraca true, gdy zmienil sie zbior armii sciagnietych (publikacja slownika).</summary>
        private static bool One(Army a, CampaignTime now, float touch)
        {
            var L = a.LeaderParty;
            if (L == null) return false;
            _seen.Add(a);
            Halt h;
            _halts.TryGetValue(a, out h);
            byte kind;
            bool halted = Halted(a, L, out kind);
            var pos = L.GetPosition2D;
            bool same = h != null && pos.Distance(h.At) <= Drill.RestStep;
            if (!halted)
            {
                bool was = h != null && h.On;
                if (h != null)
                {
                    if (was) { h.On = false; h.OffAt = now; }
                    // wpis zostaje na chwile (swit: oboz nocy -> dzien odpoczynku, cudzy rozkaz cofniety przez straznika) - dluzsza przerwa albo wodz dalej = nowy postoj
                    if (!same || (now - h.OffAt).ToHours > ResumeH) _halts.Remove(a);
                }
                return was;
            }
            if (same)
            {
                bool flip = !h.On;
                if (flip) { h.On = true; _dResumed++; }
                if (!h.Measured && (now - h.Start).ToHours >= MeasureAfterH) Measure(a, L, h);
                return flip;
            }
            // nowy postoj w innym miejscu przy wlaczonym starym (wodz zjechal > 0.35 miedzy skanami): k liczone z szyku GRY - najpierw zdjac stare k
            if (h != null && h.On) { h.On = false; Publish(); }
            _halts[a] = Begin(a, L, kind, pos, now, touch);
            return true;
        }

        private static bool Halted(Army a, MobileParty L, out byte kind)
        {
            kind = KindAi;
            if (!L.IsActive || L.AttachedTo != null || L.MapEvent != null || L.CurrentSettlement != null) return false;
            if (L.BesiegerCamp != null || L.SiegeEvent != null) return false;
            if (L.IsCurrentlyAtSea || L.IsTransitionInProgress || L.IsMoving) return false;
            if (L.AttachedParties.Count == 0 || Undead.Party(L)) return false;
            if (L == MobileParty.MainParty) { kind = KindPlayer; return NightRest.PlayerCamped; }
            if (ArmyRestDay.Resting(a)) { kind = KindAi; return true; }
            if (NightRest.FieldCampHolds(L)) { kind = KindNight; return true; }
            return false;
        }

        /// <summary>Kierunek szyku gry (MobileParty.DoUpdatePosition:3857): do NextTargetPosition, przy |..|^2 < 0.0025 - Bearing wodza.</summary>
        private static Vec2 Facing(MobileParty L)
        {
            var v = L.NextTargetPosition.ToVec2() - L.Position.ToVec2();
            return v.LengthSquared < 0.0025000002f ? L.Bearing.Normalized() : v.Normalized();
        }

        /// <summary>Poczatek postoju: k z najdalszego miejsca w szyku gry (przed publikacja - postfiks jeszcze nie dziala dla tej armii), liczniki, przyklad.</summary>
        private static Halt Begin(Army a, MobileParty L, byte kind, Vec2 pos, CampaignTime now, float touch)
        {
            var att = L.AttachedParties;
            var F = Facing(L);
            _rels.Clear();
            float m = 0f;
            for (int i = 0; i < att.Count; i++)
            {
                var p = att[i];
                Vec2 rel = Vec2.Zero;
                if (p != null && p.IsActive) { rel = a.GetRelativePositionForParty(p, F); float len = rel.Length; if (len > m) m = len; }
                _rels.Add(rel);
            }
            float k = MBMath.ClampFloat(touch / Math.Max(m, 0.01f), KMin, 1f);
            int n = 0, led = 0, still = 0;
            float bSum = 0f, bMax = 0f, tSum = 0f, tMax = 0f;
            for (int i = 0; i < att.Count; i++)
            {
                var p = att[i];
                if (p == null || !p.IsActive || p.IsMainParty) continue;
                float before = (p.VisualPosition2DWithoutError - pos).Length;
                float target = _rels[i].Length * k;   // obrot facing nie zmienia dlugosci; postfiks stawia szyk na wodzu
                n++; bSum += before; tSum += target;
                if (before > bMax) bMax = before;
                if (target > tMax) tMax = target;
                if (p.IsMoving) led++; else still++;   // gra prowadzi tylko druzyne "w ruchu" (stary MoveTargetPoint) - reszta stoi, gdzie stala
            }
            float baseOff = (L.NextTargetPosition.ToVec2() - pos).Length;
            var h = new Halt { At = pos, K = k, On = true, Kind = kind, Start = now };
            if (kind == KindPlayer) _dPlayer++; else if (kind == KindNight) _dNight++; else _dAi++;
            _dParties += n; _dLed += led; _dStill += still;
            _dBeforeSum += bSum; _dTargetSum += tSum; _dBaseSum += baseOff;
            if (bMax > _dBeforeMax) _dBeforeMax = bMax;
            if (tMax > _dTargetMax) _dTargetMax = tMax;
            if (baseOff > _dBaseMax) _dBaseMax = baseOff;
            if (_dEx < ExMax)
            {
                _dEx++; h.Ex = true;
                Log.Info(P + Who(a) + " staje (" + KindName(kind) + ") - " + n + " druzyn (gra prowadzi " + led + ", stoja " + still + (still > 0 ? (_setAdder != null ? " - dociagane krokami" : " - zostaja, BRAK settera") : "") + "), k=" + F2(k)
                         + ", najdalsze miejsce szyku gry " + F2(m) + " | figura od wodza: srednio " + F2(n > 0 ? bSum / n : 0f) + " -> cel " + F2(n > 0 ? tSum / n : 0f)
                         + ", najdalej " + F2(bMax) + " -> " + F2(tMax) + " jedn. | baza szyku gry (nastepny punkt drogi wodza) " + F2(baseOff) + " jedn. od wodza.");
            }
            return h;
        }

        /// <summary>Pomiar po MeasureAfterH h postoju: gdzie figury stoja naprawde (raz na postoj).</summary>
        private static void Measure(Army a, MobileParty L, Halt h)
        {
            h.Measured = true;
            var pos = L.GetPosition2D;
            var att = L.AttachedParties;
            int n = 0;
            float sum = 0f, max = 0f;
            for (int i = 0; i < att.Count; i++)
            {
                var p = att[i];
                if (p == null || !p.IsActive || p.IsMainParty) continue;
                float d = (p.VisualPosition2DWithoutError - pos).Length;
                n++; sum += d; if (d > max) max = d;
            }
            if (n == 0) return;
            _dMeasured++; _dMeasuredParties += n; _dAfterSum += sum;
            if (max > _dAfterMax) _dAfterMax = max;
            if (h.Ex && _dExM < ExMax)
            {
                _dExM++;
                Log.Info(P + Who(a) + " - po " + F1((float)MeasureAfterH) + " h postoju " + n + " druzyn, figura od wodza srednio " + F2(sum / n) + ", najdalej " + F2(max) + " jedn. (k=" + F2(h.K) + ").");
            }
        }

        // ------------------------------------------------------------ log
        private static void FlushDay(int day)
        {
            if (day == _logDay) return;
            bool had = _logDay >= 0;
            int prev = _logDay;
            _logDay = day;
            if (!had) { ResetDay(); return; }
            int on = 0;
            foreach (var kv in _halts) if (kv.Value.On) on++;
            int started = _dAi + _dNight + _dPlayer;
            if (started + _dResumed + _dMeasured + on + _dPullSteps == 0 && _postStumbles == 0) { ResetDay(); return; }
            Log.Info(P + "dzien " + prev + " | armie sciagniete " + started + " (postoj AI " + _dAi + ", oboz nocy " + _dNight + ", gracz " + _dPlayer + "), wznowione (ten sam postoj) " + _dResumed
                     + ", trwa " + on + " | druzyn " + _dParties + " (gra prowadzi " + _dLed + ", stoja " + _dStill + ")"
                     + " | stojace dociagane (" + (_setAdder != null ? "setter jest" : "BRAK settera") + "): krokow " + _dPullSteps + ", doszly na miejsce " + _dPullDone
                     + " | figura od wodza: przed srednio " + F2(_dParties > 0 ? _dBeforeSum / _dParties : 0f) + ", najdalej " + F2(_dBeforeMax)
                     + " -> cel srednio " + F2(_dParties > 0 ? _dTargetSum / _dParties : 0f) + ", najdalej " + F2(_dTargetMax)
                     + " | po " + F1((float)MeasureAfterH) + " h (armii " + _dMeasured + ", druzyn " + _dMeasuredParties + ") srednio " + F2(_dMeasuredParties > 0 ? _dAfterSum / _dMeasuredParties : 0f) + ", najdalej " + F2(_dAfterMax)
                     + " jedn. | baza szyku gry od wodza srednio " + F2(started > 0 ? _dBaseSum / started : 0f) + ", najdalej " + F2(_dBaseMax)
                     + " jedn. | potkniecia " + _stumbles + ", postfiksu " + Volatile.Read(ref _postStumbles) + ".");
            ResetDay();
        }

        private static string KindName(byte kind) { return kind == KindPlayer ? "armia gracza, namiot" : kind == KindNight ? "oboz nocy" : "dzien odpoczynku"; }

        private static string Who(Army a)
        {
            try
            {
                if (a == null) return "armia ?";
                var l = a.LeaderParty;
                string kn = a.Kingdom != null ? a.Kingdom.StringId : "?";
                return "armia " + (l != null && l.LeaderHero != null ? l.LeaderHero.Name.ToString() : l != null ? l.StringId : "?") + " (" + kn + ")";
            }
            catch { return "armia ?"; }
        }

        private static string F1(float v) { return v.ToString("0.#", Inv); }
        private static string F2(float v) { return v.ToString("0.00", Inv); }
    }
}
