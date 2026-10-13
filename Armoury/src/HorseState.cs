using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/K0 (209, PLAN 6.15, R13; projekt etapu 6 v2 rozdz. 3 "E6/K0"; krytyka KB K0-model-gry, KB K0-koszt, K1-12) - KON ZE STANEM I KON ZBROJOWNI
    /// NIESIE PIECHURA; STAN KONI PARTII; PASZA Z TEGO SAMEGO STANU.
    /// (a) LICZNIK GRY U ZRODLA (SpareHorsesCarryFootmen): gra liczy NumberOfMounts / NumberOfPackAnimals przyrostowo w ItemRoster.OnRosterUpdated tylko
    ///     dla sztuk BEZ modyfikatora (G ItemRoster.cs:431-456), a pelne przeliczenie po wczytaniu (CalculateCachedStats :546-576) liczy wszystkie - wynik
    ///     zalezal od historii rosteru (kon kulawy z lupu niosl piechura tylko po wczytaniu). Postfiks dolicza wierzchowce i juczne z modyfikatorem dokladnie
    ///     tak jak pelne przeliczenie (juczny -> juczne, wierzchowiec -> wierzchowce; bydlo gra liczy sama) - jedna liczba dla predkosci gry ("Footmen on
    ///     horses"), udzwigu gry, MarchPace i reszty gry (stado ponad ludzi, zakupy koni gry, lup XP). Goraca sciezka: wyjscie od razu bez flagi albo bez
    ///     modyfikatora/konia. Flaga _count zmienia sie TYLKO w Sync (start sesji, doba): przy zmianie wszystkie rostery partii i osad przeliczone pelnie
    ///     (CalculateCachedStats) - bez tego wylacznik przelaczony w polowie sesji dawalby liczniki ujemne (kon dodany bez doliczenia, zdjety z odjeciem).
    /// (b) STAN KONI PARTII W PAMIECI z pieczatka doby: partia AI ze zbrojownia DTE (PartyArmories; bez gracza - jego zbrojownia to ArmyArmory DTE) - wolne
    ///     konie zbrojowni wedlug kategorii, konni szeregowi, jezdzcy bez konia (Stables.Balance - ta sama regula przydzialu co awanse 175). Liczony raz na
    ///     dobe (po ksiegach dnia - ArmouryBehavior.OnDailyTick po Stage6Measure) i przy przesunieciu koni w Stables.Remount oraz po oplaconym awansie
    ///     (postfiks UpgradeTroop po prefiksie Stables.PayInHorses - awans zachodzi PO prefiksie). MarchPace.SpeedPostfix (wolany przy kazdej zmianie
    ///     sciany siatki - G MobileParty.cs:3056-3070, takze z watkow rownoleglych tiku partii) tylko CZYTA: ConcurrentDictionary, stan partii podmieniany
    ///     w calosci, slownik doby podmieniany w calosci. Partia bez wpisu (bez zbrojowni, nowa w tej dobie, gracz) - jak dotad: wolnych 0, bez konia 0.
    ///     Te sama pamiec czytaja pasza (c), a w nastepnych paczkach K2 (Stables.Reserve) i K5.
    /// (c) PASZA (FodderFromHorseState): WinterRealm.AnimalNeed (zima E4/1 i pasza BK po glowie, wariant a) - konni bohaterowie + konni szeregowi poza
    ///     "bez konia" ze stanu (b) + wolne konie zbrojowni, zamiast konnych po glowie (jezdzcy bez konia nie jedza, wolne konie zbrojowni jedza).
    /// Linia doby "Marsz - konie partii (E6/K0)": luzaki (bez modyfikatora / ze stanem / kulawe), wolne w zbrojowni, kontrola licznika gry wobec pelnego
    /// przeliczenia w kazdej partii marszu, sufit tempa AI bez K0 / z K0 na sucho w tym samym wywolaniu, koszt (OnRosterUpdated, stan doby, odswiezenia,
    /// SpeedPostfix) i konie porzucone przez gre (DiscardItemsCampaignBehavior - stado ponad ludzi i ciezar: licznik gry z (a) jest ich wejsciem; tylko log).
    /// </summary>
    internal static class HorseState
    {
        internal sealed class State
        {
            public int Day;                              // pieczatka doby (CampaignTime.Now.ToDays)
            public readonly int[] Free = new int[3];     // wolne konie zbrojowni: zwykle, bojowe, szlachetne
            public int FreeAll, Riders, Unhorsed;        // konni szeregowi w chwili liczenia, w tym bez konia
        }

        private static ConcurrentDictionary<MobileParty, State> _st = new ConcurrentDictionary<MobileParty, State>();

        // (a) licznik gry
        private static AccessTools.FieldRef<ItemRoster, int> _mountsRef, _packsRef;
        private static MethodInfo _recalc;
        private static volatile bool _count;
        private static bool _hooked, _upHooked, _discHooked;
        private static string _hookNote = "nie wpiety";
        private static int _recounts;

        // liczniki doby (tylko log; pola dzielone z watkami tiku bez blokad - poza nizszym sufitem, ktory jest progiem 0)
        private static long _rCalls, _rSampled, _rTicks, _rMounts, _rPacks;
        private static long _sCalls, _sSampled, _sTicks;
        private static long _capN, _capRaised, _capLowered;
        private static double _capNo, _capK0;
        private static long _refreshN, _refreshTicks;
        private static long _discHerd, _discHerdMod, _discWeight, _discWeightMod;
        private static int _stumbles, _stumblesDay;
        private static readonly object Gate = new object();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        [ThreadStatic] private static MobileParty _pend;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Reset()
        {
            _st = new ConcurrentDictionary<MobileParty, State>();
            _count = false;   // Sync przy starcie sesji wlacza i przelicza rostery pelnie (nowa gra: rostery z tiku przed startem sesji)
            _recounts = 0;
            ResetDay();
            _stumbles = 0;
            lock (Gate) _errSites.Clear();
        }

        private static void ResetDay()
        {
            _rCalls = _rSampled = _rTicks = _rMounts = _rPacks = 0;
            _sCalls = _sSampled = _sTicks = 0;
            _capN = _capRaised = 0; Interlocked.Exchange(ref _capLowered, 0); _capNo = _capK0 = 0;
            Interlocked.Exchange(ref _refreshN, 0); Interlocked.Exchange(ref _refreshTicks, 0);
            _discHerd = _discHerdMod = _discWeight = _discWeightMod = 0;
            _stumblesDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles); Interlocked.Increment(ref _stumblesDay);
            bool first; lock (Gate) first = _errSites.Add(where);
            if (first) Log.Error("HorseState." + where, e);
        }

        /// <summary>Czy ktos czyta stan (b): kolumna marszu albo pasza.</summary>
        private static bool Wanted(Settings s) { return s != null && (s.SpareHorsesCarryFootmen || s.FodderFromHorseState); }

        // ------------------------------------------------------------ odczyt (MarchPace, pasza, K2/K5)

        /// <summary>Stan koni partii z pamieci doby (null - partia bez zbrojowni DTE, nowa w tej dobie albo gracz).</summary>
        internal static State Of(MobileParty mp)
        {
            if (mp == null) return null;
            State st;
            return _st.TryGetValue(mp, out st) ? st : null;
        }

        /// <summary>Wolne konie zbrojowni partii (pamiec doby; bez stanu 0) - MarchPace: niosa piechure jak luzaki w taborze.</summary>
        internal static int FreeOf(MobileParty mp)
        {
            var st = Of(mp);
            return st != null ? st.FreeAll : 0;
        }

        /// <summary>(c) Konie partii do paszy: konni bohaterowie + konni szeregowi poza "bez konia" ze stanu + wolne konie zbrojowni. heads - konni po glowie
        /// (dawna liczba: bohaterowie i szeregowi z IsMounted, z rannymi), free / unhorsed - skladniki ze stanu (partia bez stanu: 0, wynik = heads).
        /// "Bez konia" przyciete do konnych szeregowych w tej chwili (sklad mogl sie zmienic od pieczatki).</summary>
        internal static int FodderMounts(MobileParty mp, out int heads, out int free, out int unhorsed)
        {
            heads = 0; free = 0; unhorsed = 0;
            if (mp == null) return 0;
            int heroes = 0, troops = 0;
            var mr = mp.MemberRoster;
            if (mr != null)
                for (int i = 0; i < mr.Count; i++)
                {
                    var c = mr.GetCharacterAtIndex(i);
                    if (c == null || !c.IsMounted) continue;
                    if (c.IsHero) heroes += mr.GetElementNumber(i); else troops += mr.GetElementNumber(i);
                }
            heads = heroes + troops;
            var st = Of(mp);
            if (st == null) return heads;
            unhorsed = Math.Min(Math.Max(0, st.Unhorsed), troops);
            free = st.FreeAll;
            return heads - unhorsed + free;
        }

        // ------------------------------------------------------------ (b) stan doby i odswiezenia

        private static State Build(MobileParty mp, Dictionary<MBGUID, Dictionary<ItemObject, int>> all)
        {
            if (mp == null || all == null || mp.IsMainParty || !mp.IsActive || mp.MemberRoster == null || !all.ContainsKey(mp.Id)) return null;
            var b = Stables.Balance(mp);
            var st = new State { Day = (int)CampaignTime.Now.ToDays, FreeAll = b.FreeAll, Riders = b.Riders, Unhorsed = b.Unhorsed };
            Array.Copy(b.Free, st.Free, 3);
            return st;
        }

        /// <summary>Stan jednej partii od nowa (Stables.Remount po przesunieciu koni, postfiks UpgradeTroop po oplaconym awansie).</summary>
        internal static void Refresh(MobileParty mp)
        {
            if (mp == null || !Wanted(Settings.Current)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var st = Build(mp, AiGear.Armories());
                var d = _st;
                if (st != null) d[mp] = st;
                else { State x; d.TryRemove(mp, out x); }
            }
            catch (Exception e) { Stumble("Refresh", e); }
            Interlocked.Increment(ref _refreshN);
            Interlocked.Add(ref _refreshTicks, Stopwatch.GetTimestamp() - t0);
        }

        /// <summary>Stables.PayInHorses (prefiks UpgradeTroop) po oplaceniu awansu koniem: stan partii odswiezany w postfiksie, gdy nowy jezdziec juz jest w skladzie.</summary>
        internal static void AfterPay(PartyBase party)
        {
            if (party != null && party.MobileParty != null && Wanted(Settings.Current)) _pend = party.MobileParty;
        }

        public static void UpgradePostfix(PartyBase __0)
        {
            var mp = _pend;
            if (mp == null) return;
            _pend = null;
            try { if (__0 != null && __0.MobileParty == mp) Refresh(mp); }
            catch (Exception e) { Stumble("UpgradePostfix", e); }
        }

        // ------------------------------------------------------------ (a) licznik gry u zrodla

        /// <summary>Postfiks ItemRoster.OnRosterUpdated: wierzchowiec i juczny Z modyfikatorem liczone jak w CalculateCachedStats (:566-573).</summary>
        public static void RosterPostfix(ItemRoster __instance, ref ItemRosterElement itemRosterElement, int count)
        {
            if (!_count) return;
            long t = (++_rCalls & 15) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                var ee = itemRosterElement.EquipmentElement;
                if (ee.ItemModifier != null)
                {
                    var it = ee.Item;
                    if (it != null && it.HasHorseComponent)
                    {
                        var hc = it.HorseComponent;
                        if (hc.IsPackAnimal) { _packsRef(__instance) += count; _rPacks += count; }
                        else if (hc.IsMount) { _mountsRef(__instance) += count; _rMounts += count; }
                    }
                }
            }
            catch (Exception e) { Stumble("RosterPostfix", e); }
            if (t != 0) { _rTicks += Stopwatch.GetTimestamp() - t; _rSampled++; }
        }

        /// <summary>Flaga licznika wedlug klucza; przy zmianie - pelne przeliczenie wszystkich rosterow partii i osad (watek gry, poza tikami rownoleglymi).</summary>
        private static void Sync(Settings s)
        {
            bool want = _hooked && s != null && s.SpareHorsesCarryFootmen;
            if (want == _count) return;
            _count = want;
            if (_recalc == null) return;
            int n = 0;
            foreach (var mp in MobileParty.All)
            {
                try { var r = mp != null ? mp.ItemRoster : null; if (r != null) { _recalc.Invoke(r, null); n++; } }
                catch (Exception e) { Stumble("Sync(partia)", e); }
            }
            foreach (var st in Settlement.All)
            {
                try { var r = st != null ? st.ItemRoster : null; if (r != null) { _recalc.Invoke(r, null); n++; } }
                catch (Exception e) { Stumble("Sync(osada)", e); }
            }
            _recounts++;
            Log.Info("HorseState (E6/K0): licznik koni gry " + (want ? "liczy wierzchowce i juczne ze stanem (jak pelne przeliczenie gry)" : "jak w grze (przyrost bez stanu)")
                     + " - pelne przeliczenie " + n + " rosterow partii i osad.");
        }

        internal static void OnSessionLaunched()
        {
            var s = Settings.Current;
            Sync(s);
            Rebuild(s);   // odbudowa pamieci przy wczytaniu (zbrojownie DTE juz w pamieci - OnGameLoaded DTE przed startem sesji); nowa gra - pusto do pierwszej doby
            Log.Info("HorseState (E6/K0): licznik koni gry u zrodla (SpareHorsesCarryFootmen " + (s != null && s.SpareHorsesCarryFootmen ? "TAK" : "NIE") + ", latka " + _hookNote
                     + ", liczy " + (_count ? "TAK" : "NIE") + "); kon zbrojowni niesie piechura w kolumnie (MarchPace); stan koni partii " + _st.Count
                     + " partii ze zbrojownia DTE; pasza z tego stanu (FodderFromHorseState " + (s != null && s.FodderFromHorseState ? "TAK" : "NIE")
                     + "); odswiezenie po awansie " + (_upHooked ? "wpiete" : "NIE wpiete") + "; konie porzucone przez gre (pomiar) " + (_discHooked ? "wpiete" : "NIE wpiete") + ".");
        }

        private static int Rebuild(Settings s)
        {
            var fresh = new ConcurrentDictionary<MobileParty, State>();
            if (Wanted(s))
            {
                var all = AiGear.Armories();
                if (all != null)
                    foreach (var mp in MobileParty.All)
                    {
                        try { var st = Build(mp, all); if (st != null) fresh[mp] = st; }
                        catch (Exception e) { Stumble("Rebuild(partia)", e); }
                    }
            }
            _st = fresh;   // podmiana w calosci - czytelnicy biora jedna referencje
            return fresh.Count;
        }

        // ------------------------------------------------------------ MarchPace: koszt i sufit na sucho

        internal static long SpeedBegin() { return (++_sCalls & 15) == 1 ? Stopwatch.GetTimestamp() : 0L; }
        internal static void SpeedEnd(long t) { if (t != 0) { _sTicks += Stopwatch.GetTimestamp() - t; _sSampled++; } }

        /// <summary>Sufit kolumny partii AI (jednostki mapy, po tempie swiata) bez wolnych koni zbrojowni i z nimi - w tym samym wywolaniu.</summary>
        internal static void NoteCap(float without, float with)
        {
            _capN++; _capNo += without; _capK0 += with;
            if (with > without + 0.001f) _capRaised++;
            else if (with < without - 0.001f) Interlocked.Increment(ref _capLowered);
        }

        // ------------------------------------------------------------ pomiar: konie porzucone przez gre (lordowie AI)

        public static void DiscardAnimalPrefix(ItemRosterElement itemRosterElement, ref int numberOfAnimalsToDiscard)
        {
            try
            {
                var it = itemRosterElement.EquipmentElement.Item;
                int cat = it != null ? HorseBook.Cat(it) : -1;
                if (cat < 0) return;
                int n = Math.Min(itemRosterElement.Amount, numberOfAnimalsToDiscard);   // jak DiscardAnimal :124
                if (n <= 0) return;
                _discHerd += n;
                HorseBook.NoteDiscard(cat, n, true);   // F3: ujscie w ksiedze koni (wedlug kategorii)
                if (itemRosterElement.EquipmentElement.ItemModifier != null) _discHerdMod += n;
            }
            catch (Exception e) { Stumble("DiscardAnimal", e); }
        }

        public static void DiscardWeightPrefix(ItemRosterElement itemRosterElement, ref float weightLeftToDiscard, int discardLimit)
        {
            try
            {
                var it = itemRosterElement.EquipmentElement.Item;
                int cat = it != null ? HorseBook.Cat(it) : -1;
                if (cat < 0) return;
                float w = itemRosterElement.EquipmentElement.GetEquipmentElementWeight();   // jak DiscardNecessaryAmountOfItems :116-117
                int n = MBMath.ClampInt(itemRosterElement.GetRosterElementWeight() <= weightLeftToDiscard ? itemRosterElement.Amount
                                        : (w > 0f ? MathF.Ceiling(weightLeftToDiscard / w) : itemRosterElement.Amount), 0, discardLimit);
                if (n <= 0) return;
                _discWeight += n;
                HorseBook.NoteDiscard(cat, n, false);   // F3: ujscie w ksiedze koni (wedlug kategorii)
                if (itemRosterElement.EquipmentElement.ItemModifier != null) _discWeightMod += n;
            }
            catch (Exception e) { Stumble("DiscardWeight", e); }
        }

        // ------------------------------------------------------------ doba

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick po Stage6Measure - stan po wszystkich przeplywach dnia): flaga licznika, stan koni partii od nowa,
        /// linia "Marsz - konie partii (E6/K0)" za mijajaca dobe.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            try { Sync(s); } catch (Exception e) { Stumble("Sync", e); }
            long t0 = Stopwatch.GetTimestamp();
            int n = 0;
            try { n = Rebuild(s); } catch (Exception e) { Stumble("Rebuild", e); }
            long rebuild = Stopwatch.GetTimestamp() - t0;
            try { Log.Info(Line(s, n, rebuild)); } catch (Exception e) { Stumble("Line", e); }
            ResetDay();
        }

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static double Sampled(long ticks, long sampled, long calls) { return sampled > 0 ? Ms(ticks) * ((double)calls / sampled) : 0.0; }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        private static bool IsLame(ItemModifier m)
        {
            var id = m != null ? m.StringId : null;
            return id != null && id.IndexOf("lame", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Line(Settings s, int withArmory, long rebuildTicks)
        {
            long tl = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            // luzaki i kontrola licznika gry - partie, ktore zna kolumna marszu (bez wsi, karawan, zalog i milicji)
            int parties = 0, plain = 0, mod = 0, lame = 0, packMod = 0, diffM = 0, diffMp = 0, diffP = 0, diffPp = 0;
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.IsVillager || mp.IsCaravan || mp.IsGarrison || mp.IsMilitia) continue;
                    var r = mp.ItemRoster;
                    if (r == null) continue;
                    parties++;
                    int m = 0, p = 0;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || !it.HasHorseComponent) continue;
                        var hc = it.HorseComponent;
                        var md = el.EquipmentElement.ItemModifier;
                        if (hc.IsPackAnimal) { p += el.Amount; if (md != null) packMod += el.Amount; }   // jak CalculateCachedStats
                        else if (hc.IsMount)
                        {
                            m += el.Amount;
                            if (md == null) plain += el.Amount;
                            else { mod += el.Amount; if (IsLame(md)) lame += el.Amount; }
                        }
                    }
                    int dm = r.NumberOfMounts - m, dp = r.NumberOfPackAnimals - p;
                    if (dm != 0) { diffMp++; diffM += Math.Abs(dm); }
                    if (dp != 0) { diffPp++; diffP += Math.Abs(dp); }
                }
                catch (Exception e) { Stumble("Line(partia)", e); }
            }
            int riders = 0, unhorsed = 0, free = 0;
            var d = _st;
            foreach (var kv in d) { var st = kv.Value; if (st == null) continue; riders += st.Riders; unhorsed += st.Unhorsed; free += st.FreeAll; }

            double rosterMs = Sampled(_rTicks, _rSampled, _rCalls), speedMs = Sampled(_sTicks, _sSampled, _sCalls);
            double refreshMs = Ms(Interlocked.Read(ref _refreshTicks)), rebuildMs = Ms(rebuildTicks);
            double k0Ms = rosterMs + refreshMs + rebuildMs;
            long capN = _capN;

            var sb = new StringBuilder(900);
            sb.Append("Marsz - konie partii (E6/K0): dzien ").Append(day);
            sb.Append(" | licznik gry u zrodla (SpareHorsesCarryFootmen ").Append(s != null && s.SpareHorsesCarryFootmen ? "TAK" : "NIE").Append(", latka ").Append(_hookNote)
              .Append("): liczy ").Append(_count ? "TAK" : "NIE").Append(", doliczone netto wierzchowce ").Append(_rMounts).Append(", juczne ").Append(_rPacks)
              .Append("; pelnych przeliczen przy zmianie wylacznika (sesja) ").Append(_recounts);
            sb.Append(" | luzaki w partiach marszu (").Append(parties).Append(" partii, bez wsi, karawan, zalog i milicji): bez modyfikatora ").Append(plain)
              .Append(", ze stanem ").Append(mod).Append(" (w tym kulawe ").Append(lame).Append("), juczne ze stanem ").Append(packMod)
              .Append(", wolne w zbrojowni ").Append(free).Append(" (stan K0: ").Append(withArmory).Append(" partii ze zbrojownia DTE, konni szeregowi ").Append(riders)
              .Append(", bez konia ").Append(unhorsed).Append(")");
            sb.Append(" | kontrola licznik gry = pelne przeliczenie (CalculateCachedStats) w ").Append(parties).Append(" partiach: wierzchowce roznica ").Append(diffM)
              .Append(" (partii ").Append(diffMp).Append("), juczne ").Append(diffP).Append(" (partii ").Append(diffPp).Append(")");
            sb.Append(" | sufit tempa AI na sucho (to samo wywolanie): ").Append(capN).Append(" wywolan, srednio bez K0 ").Append(capN > 0 ? F2(_capNo / capN) : "-")
              .Append(" / z K0 ").Append(capN > 0 ? F2(_capK0 / capN) : "-").Append(", z K0 wyzszy ").Append(_capRaised).Append(", nizszy ").Append(Interlocked.Read(ref _capLowered)).Append(" (prog 0)");
            sb.Append(" | koszt K0: OnRosterUpdated ").Append(_rCalls).Append(" wywolan ").Append(F2(rosterMs)).Append(" ms, stan doby ").Append(F2(rebuildMs))
              .Append(" ms, odswiezenia ").Append(Interlocked.Read(ref _refreshN)).Append(" (").Append(F2(refreshMs)).Append(" ms), razem ").Append(F2(k0Ms))
              .Append(" ms (prog 2 ms/dobe); SpeedPostfix ").Append(F1(speedMs)).Append(" ms/").Append(_sCalls).Append(" wywolan");
            sb.Append(" | konie porzucone przez gre (lordowie AI, DiscardItemsCampaignBehavior, pomiar ").Append(_discHooked ? "wpiety" : "NIE wpiety")
              .Append("): nadmiar stada ").Append(_discHerd).Append(" (ze stanem ").Append(_discHerdMod).Append("), ponad udzwig ").Append(_discWeight)
              .Append(" (ze stanem ").Append(_discWeightMod).Append(")");
            sb.Append(" | pasza ze stanu koni (FodderFromHorseState) ").Append(s != null && s.FodderFromHorseState ? "TAK" : "NIE");
            sb.Append(" | potkniecia dzis ").Append(Volatile.Read(ref _stumblesDay)).Append(", od startu sesji ").Append(Volatile.Read(ref _stumbles));
            lock (Gate) { if (_errSites.Count > 0) sb.Append(" (").Append(string.Join(", ", _errSites)).Append(')'); }
            sb.Append(" | linia ").Append(F1(Ms(Stopwatch.GetTimestamp() - tl))).Append(" ms.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ latki

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                _mountsRef = AccessTools.FieldRefAccess<ItemRoster, int>("<NumberOfMounts>k__BackingField");
                _packsRef = AccessTools.FieldRefAccess<ItemRoster, int>("<NumberOfPackAnimals>k__BackingField");
                _recalc = AccessTools.Method(typeof(ItemRoster), "CalculateCachedStats");
                var m = AccessTools.Method(typeof(ItemRoster), "OnRosterUpdated");
                if (m == null || _recalc == null || _mountsRef == null || _packsRef == null) _hookNote = "NIE wpieta (brak OnRosterUpdated / CalculateCachedStats / pol licznika)";
                else { h.Patch(m, postfix: new HarmonyMethod(typeof(HorseState), nameof(RosterPostfix))); _hooked = true; _hookNote = "wpieta"; }
            }
            catch (Exception e) { _hooked = false; _hookNote = "NIE wpieta (wyjatek " + e.GetType().Name + ")"; Log.Error("HorseState.ApplyAll(licznik)", e); }
            try
            {
                var mUp = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.PartyUpgraderCampaignBehavior), "UpgradeTroop");
                if (mUp != null) { h.Patch(mUp, postfix: new HarmonyMethod(typeof(HorseState), nameof(UpgradePostfix))); _upHooked = true; }
            }
            catch (Exception e) { _upHooked = false; Log.Error("HorseState.ApplyAll(awans)", e); }
            try
            {
                var tD = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.DiscardItemsCampaignBehavior);
                var mA = AccessTools.Method(tD, "DiscardAnimal");
                var mW = AccessTools.Method(tD, "DiscardNecessaryAmountOfItems");
                if (mA != null) h.Patch(mA, prefix: new HarmonyMethod(typeof(HorseState), nameof(DiscardAnimalPrefix)));
                if (mW != null) h.Patch(mW, prefix: new HarmonyMethod(typeof(HorseState), nameof(DiscardWeightPrefix)));
                _discHooked = mA != null && mW != null;
            }
            catch (Exception e) { _discHooked = false; Log.Error("HorseState.ApplyAll(porzucone)", e); }
            Log.Info("HorseState (E6/K0): licznik koni gry u zrodla (postfiks ItemRoster.OnRosterUpdated) " + _hookNote + "; odswiezenie stanu po awansie (postfiks UpgradeTroop) "
                     + (_upHooked ? "wpiete" : "NIE wpiete") + "; pomiar koni porzuconych przez gre " + (_discHooked ? "wpiety" : "NIE wpiety") + " (wylaczniki czytane w kampanii).");
        }
    }
}
