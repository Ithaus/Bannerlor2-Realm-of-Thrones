using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 307a (krytyk 307 karawany waga 8) - KARAWANA MORSKA TRZYMA CEL DO DOPLYNIECIA.
    /// Dowod [L] (autotest 142a/142b, dziennik 305a): karawana gracza na morzu od 108892 zmienia cel bez wizyty w miescie - Driftwood Hall -> Qarth -> Astapor ->
    /// Pebbleton -> Lys -> Yunkai -> Tyrosh -> Lorath -> Pentos; "bez handlu z rzedu 76 dob" (142a), 43 doby (142b); na starcie 18 dob do Pentos; zachowanie
    /// GoToSettlement przez caly czas; zold rodu idzie dalej.
    /// Przyczyna [K] (dekompilacja BannerKings.dll, BKCaravansBehavior.HourlyTickPartyImpl :975-1161): w drodze BK uznaje, ze trzeba nowej decyzji, gdy cel jest
    /// pusty, oblezony, wrogi ALBO "randomFloat &lt; 0.01" (1% na tik) - i wtedy ThinkNextDestination wybiera od nowa z calego swiata (cele na dalekim Essos).
    /// Dlawik BKROT (BKCaravansHourlyThrottlePatch, E8/G1 CaravanFlow) puszcza tik BK karawanie AI raz na dobe, ale karawanie RODU GRACZA co godzine:
    /// 24 tiki x 1% = ok. 21% na dobe, przy 18 dobach morza szansa doplyniecia bez zmiany celu 0.99^(24 x 18) = ok. 1.3% (AI: 0.99^18 = 83%).
    /// Regula (klucz CaravanSeaHoldTarget; jedna dla wszystkich karawan morskich swiata - gracza i AI): prefiks HourlyTickPartyImpl - karawana ze statkami
    /// (HasNavalNavigationCapability) w drodze (bez osady, bez bitwy, handel czynny, AI czynne, GoToSettlement, nie Hold) z celem = miasto z portem, nie oblezone
    /// i nie wrogie (te same warunki BK) trzyma cel: tik BK pominiety (jedyne, co by zrobil, to losowa nowa decyzja - ogon metody nic nie zmienia, gdy cel jest
    /// miastem). Wyjscia: wojna albo oblezenie celu (BK decyduje jak dotad), cel nie miasto z portem (BK flag4 - jak dotad), przekroczenie dni drogi z decyzji:
    /// limit = Caravan Sea Hold Factor x dni drogi do celu w chwili pierwszego tiku z tym celem (CaravanEscort.RoadDaysTo - prosta / dzien drogi x zmierzony
    /// wspolczynnik odcinka), w granicach Caravan Sea Hold Min Days .. Max Days. 307e: po limicie od razu decyzja BK (ThinkNextDestination - NewTargetNow; ten sam
    /// cel albo brak - wpis 'po limicie' bez trzymania, BK jak dotad 1% na tik); Factor 3.0 (143a-probyN: rejs / droga ladowa srednio 1.78, przy 2.0 28% po limicie). Nowy cel - zawsze w miescie albo po
    /// wyjsciu z trzymania - wybiera ThinkNextDestination z regulami 294c / 139 C / D1-D2 (odcinek K294-19/31) jak dotad.
    /// Pomiar (takze przy kluczu NIE): postfiks MobileParty.SetMoveGoToSettlement - zmiana celu karawany w drodze (stary / nowy cel, dni od decyzji, zrodlo:
    /// BK tik w drodze z powodem albo inna droga ze stosem dla karawany gracza). Linia doby "Karawany morskie - cel do doplyniecia (307a): dzien N".
    /// </summary>
    internal static class CaravanSeaHold307
    {
        private sealed class Hold { public Settlement T; public double Start, Limit, Days0; public bool Expired; }   // 307e: Expired - limit minal, BK juz decydowal
        private static readonly Dictionary<MobileParty, Hold> _h = new Dictionary<MobileParty, Hold>();
        private static bool _wImpl, _wMove;
        private static MobileParty _inBk; private static int _inWhy;   // karawana w tiku BK w drodze i powod wpuszczenia (0 brak / 1 wojna / 2 oblezenie / 3 limit / 4 cel nie port / 5 klucz NIE / 6 inne)
        private static long _ticks, _held, _newHold, _exWar, _exSiege, _exLimit, _exNotPort, _exOff, _exIdle;   // 307d: _exIdle - ShortTermBehavior None (BK zwalnia karawane)
        // 307e (K307-8): po limicie decyzja BK od razu (ThinkNextDestination) - nowy cel / ten sam albo brak (BK dalej jak dotad, wpis bez trzymania)
        private static long _limNew, _limSame;
        private static System.Reflection.MethodInfo _think; private static bool _thinkLooked;
        // recenzja 307e: lista celow porzuconych przez BK (BKCaravansBehavior._previouslyChangedCaravanTargetsDueToEnemyOnWay) - jak BK w bloku flag3 (:1072-1081)
        private static System.Reflection.FieldInfo _prevF;
        private static readonly HashSet<MobileParty> _heldCars = new HashSet<MobileParty>();
        private static long _chSea, _chLand, _chSeaPlayer, _chOther;
        // recenzja 307a (pomiar trafnosci limitu): doplyniecia do trzymanego celu - liczba, suma i najwiekszy stosunek dni rejsu / droga z decyzji, w tym ponad limit
        private static long _arr, _arrOver; private static double _arrRatioSum, _arrRatioMax;
        private static readonly List<string> _arrDet = new List<string>();
        private static readonly long[] _chWhy = new long[7];
        private static readonly List<string> _det = new List<string>();
        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private const int DetMax = 8;
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        private static readonly string[] WhyName = { "-", "wojna", "oblezenie", "limit dni (potem losowa 1% na tik)", "cel nie miasto z portem", "losowa 1% na tik (klucz NIE)", "inne (Hold, nie GoToSettlement, zachowanie None)" };

        internal static void Reset()
        {
            _h.Clear(); _inBk = null; _inWhy = 0;
            _ticks = _held = _newHold = _exWar = _exSiege = _exLimit = _exNotPort = _exOff = _exIdle = 0; _heldCars.Clear();
            _limNew = _limSame = 0;
            _chSea = _chLand = _chSeaPlayer = _chOther = 0; Array.Clear(_chWhy, 0, _chWhy.Length); _det.Clear();
            _arr = _arrOver = 0; _arrRatioSum = _arrRatioMax = 0; _arrDet.Clear();
            _stumbles = _stumbleSess = 0; _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanSeaHold307." + where, e); } catch { }
        }

        private static bool Naval(MobileParty mp) { try { return mp.HasNavalNavigationCapability; } catch { return false; } }

        /// <summary>Recenzja 307a (tylko pomiar): karawana w osadzie z wpisem trzymania - doplynela do trzymanego celu? Faktyczne dni rejsu wobec drogi z decyzji
        /// (Days0, CaravanEscort.RoadDaysTo - miara ladowa) - do doboru Caravan Sea Hold Factor / Min Days po autotescie.</summary>
        private static void Arrived(MobileParty mp, Hold h)
        {
            try
            {
                if (mp == null || h == null || h.T == null || mp.CurrentSettlement != h.T) return;
                double days = CampaignTime.Now.ToDays - h.Start;
                _arr++;
                if (h.Expired || days > h.Limit) _arrOver++;
                if (h.Days0 > 0)
                {
                    double q = days / h.Days0;
                    _arrRatioSum += q; if (q > _arrRatioMax) _arrRatioMax = q;
                }
                if (mp.ActualClan == Clan.PlayerClan && _arrDet.Count < DetMax)
                    _arrDet.Add("'" + (mp.Name != null ? mp.Name.ToString() : mp.StringId) + "' w " + Nm(h.T) + " po " + days.ToString("0.0", Inv) + " dob (droga z decyzji "
                                + h.Days0.ToString("0.0", Inv) + ", limit " + h.Limit.ToString("0.0", Inv) + ")");
            }
            catch (Exception e) { Stumble("Arrived", e); }
        }

        private static double LimitDays(MobileParty mp, Settlement t, Settings s, out double d0)
        {
            d0 = 0;
            try { d0 = CaravanEscort.RoadDaysTo(mp, t); } catch { d0 = 0; }
            double lo = Math.Max(1, s.CaravanSeaHoldMinDays), hi = Math.Max(lo, s.CaravanSeaHoldMaxDays);
            double v = d0 > 0 && !double.IsInfinity(d0) ? d0 * Math.Max(1.0, s.CaravanSeaHoldFactor) : lo;
            return Math.Max(lo, Math.Min(hi, v));
        }

        /// <summary>Prefiks BKCaravansBehavior.HourlyTickPartyImpl(MobileParty): karawana morska w drodze z waznym celem - tik BK pominiety (cel trzymany).</summary>
        public static bool ImplPrefix(object __instance, MobileParty __0)
        {
            _inBk = null; _inWhy = 0;
            try
            {
                var mp = __0;
                if (mp == null || !mp.IsCaravan) return true;
                if (mp.CurrentSettlement != null) { Hold ha; if (_h.Count > 0 && _h.TryGetValue(mp, out ha)) { Arrived(mp, ha); _h.Remove(mp); } return true; }   // w osadzie: decyzja BK jak dotad, trzymanie od nowa
                if (mp.MapEvent != null || !mp.IsPartyTradeActive || mp.Ai == null || mp.Ai.DoNotMakeNewDecisions) return true;
                if (!Naval(mp)) return true;
                _ticks++;
                _inBk = mp; _inWhy = 6;
                if (mp.DefaultBehavior != AiBehavior.GoToSettlement || mp.ShortTermBehavior == AiBehavior.Hold || mp.DefaultBehavior == AiBehavior.Hold) return true;
                // 307d (przeglad scalenia, dekompilacja BK HourlyTickPartyImpl): ShortTermBehavior None (karawana stoi - np. po bitwie albo ucieczce) -> BK wola
                // ReleaseCaravanFromHold (RethinkAtNextHourlyTick, ruch do celu); tego tiku nie wolno pominac
                if (mp.ShortTermBehavior == AiBehavior.None) { _exIdle++; return true; }
                var t = mp.TargetSettlement;
                if (t == null || t.Town == null || !t.IsTown || !t.HasPort) { _exNotPort++; _inWhy = 4; return true; }
                if (t.IsUnderSiege) { _exSiege++; _inWhy = 2; return true; }
                if (mp.MapFaction != null && t.MapFaction != null && mp.MapFaction.IsAtWarWith(t.MapFaction)) { _exWar++; _inWhy = 1; return true; }
                var s = Settings.Current;
                if (s == null || !s.CaravanSeaHoldTarget) { _exOff++; _inWhy = 5; return true; }
                double now = CampaignTime.Now.ToDays;
                Hold h;
                if (!_h.TryGetValue(mp, out h) || h.T != t)
                {
                    double d0;
                    h = new Hold { T = t, Start = now };
                    h.Limit = LimitDays(mp, t, s, out d0); h.Days0 = d0;
                    _h[mp] = h; _newHold++;
                }
                if (h.Expired) { _inWhy = 3; return true; }                       // 307e: po limicie - BK jak dotad (ten sam cel wybrany ponownie albo brak celu)
                if (now - h.Start > h.Limit)
                {
                    // 307e (K307-8, 143a-probyN: 148 z 523 doplyniec po limicie, wpisy trzymane do 59 dob przy limicie 30): limit minal - jedna decyzja BK
                    // od razu (ta sama metoda, ktora BK wola przy nowej decyzji w drodze, z naszymi regulami celu 294c / 139 C / D1-D2), zamiast czekac na 1% na tik
                    h.Expired = true; _exLimit++; _inWhy = 3;
                    return !NewTargetNow(__instance, mp, t);
                }
                _held++; _heldCars.Add(mp);
                _inBk = null; _inWhy = 0;
                return false;
            }
            catch (Exception e) { Stumble("ImplPrefix", e); _inBk = null; return true; }
        }

        /// <summary>307e: limit trzymania minal - BK ThinkNextDestination (jak blok "flag3" HourlyTickPartyImpl w drodze, bez zakupow - karawana nie w miescie)
        /// i ruch do nowego miasta tym samym SetMoveGoToSettlement co BK (morska: Naval, do portu). true - nowy cel ustawiony (tik BK pominiety);
        /// false - ten sam cel albo brak (tik BK jak dotad). Zmiana celu liczy sie w pomiarze jako "BK limit" (_inBk / _inWhy = 3).</summary>
        private static bool NewTargetNow(object bk, MobileParty mp, Settlement old)
        {
            try
            {
                if (bk == null || mp == null) { _limSame++; return false; }
                if (!_thinkLooked)
                {
                    _thinkLooked = true;
                    _think = AccessTools.DeclaredMethod(bk.GetType(), "ThinkNextDestination", new[] { typeof(MobileParty) });
                    _prevF = AccessTools.DeclaredField(bk.GetType(), "_previouslyChangedCaravanTargetsDueToEnemyOnWay");
                    if (_think == null)
                        Log.Info("Karawany morskie - cel do doplyniecia (307e): brak metody BK ThinkNextDestination(MobileParty) w " + bk.GetType().FullName
                                 + " - po limicie BK jak dotad (1% na tik), licznik 'ten sam albo brak'.");
                }
                if (_think == null) { _limSame++; return false; }
                var town = _think.Invoke(bk, new object[] { mp }) as Town;
                var st = town != null ? town.Settlement : null;
                if (st == null || st == old) { _limSame++; return false; }
                // recenzja 307e: porzucony cel na liste BK (jak BK po ThinkNextDestination != null) - nastepna decyzja BK go pomija;
                // BK czysci liste przy wejsciu do miasta (OnSettlementEntered) i gdy ThinkNextDestination nie da celu. Bez pola - tylko bez tego wpisu.
                try
                {
                    var dict = _prevF != null ? _prevF.GetValue(bk) as Dictionary<MobileParty, List<Settlement>> : null;
                    if (dict != null && old != null)
                    {
                        List<Settlement> l;
                        if (!dict.TryGetValue(mp, out l) || l == null) { l = new List<Settlement>(); dict[mp] = l; }
                        l.Add(old);
                    }
                }
                catch (Exception e) { Stumble("NewTargetNow(lista BK)", e); }
                _inBk = mp; _inWhy = 3;
                mp.SetMoveGoToSettlement(st, MobileParty.NavigationType.Naval, true);
                _limNew++;
                return true;
            }
            catch (Exception e) { Stumble("NewTargetNow", e); _limSame++; return false; }
        }

        /// <summary>Finalizer HourlyTickPartyImpl: koniec tiku BK (zrodlo zmiany celu w pomiarze).</summary>
        public static void ImplFinalizer() { _inBk = null; _inWhy = 0; }

        public struct MoveState { public bool On; public Settlement Old; }

        /// <summary>Prefiks MobileParty.SetMoveGoToSettlement: karawana w drodze - stary cel (tylko pomiar).</summary>
        public static void MovePre(MobileParty __instance, out MoveState __state)
        {
            __state = default(MoveState);
            try
            {
                if (__instance == null || !__instance.IsCaravan || __instance.CurrentSettlement != null) return;
                __state.On = true; __state.Old = __instance.TargetSettlement;
            }
            catch { __state.On = false; }
        }

        /// <summary>Postfiks SetMoveGoToSettlement: zmiana celu karawany w drodze - liczniki i szczegoly karawany gracza (tylko pomiar).</summary>
        public static void MovePost(MobileParty __instance, Settlement __0, MoveState __state)
        {
            if (!__state.On) return;
            try
            {
                var mp = __instance;
                var neu = mp.TargetSettlement ?? __0;
                if (__state.Old == null || neu == null || neu == __state.Old) return;
                bool sea = Naval(mp);
                if (!sea) { _chLand++; return; }
                _chSea++;
                bool bk = _inBk == mp;
                int why = _inWhy >= 1 && _inWhy <= 6 ? _inWhy : 6;
                if (bk) _chWhy[why]++; else _chOther++;
                if (mp.ActualClan != Clan.PlayerClan) return;
                _chSeaPlayer++;
                if (_det.Count >= DetMax) return;
                Hold h; double since = -1, lim = -1, d0 = -1;
                if (_h.TryGetValue(mp, out h) && h.T == __state.Old) { since = CampaignTime.Now.ToDays - h.Start; lim = h.Limit; d0 = h.Days0; }
                string src;
                if (bk) src = "BK tik w drodze (" + WhyName[why] + ")";
                else
                {
                    var sb = new StringBuilder(120);
                    try
                    {
                        var st = new StackTrace(2, false);
                        for (int i = 0; i < st.FrameCount && i < 3; i++)
                        {
                            var m = st.GetFrame(i).GetMethod();
                            if (m == null) continue;
                            sb.Append(i > 0 ? " <- " : "").Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append('.').Append(m.Name);
                        }
                    }
                    catch { sb.Append("?"); }
                    src = "inna droga: " + sb;
                }
                _det.Add("'" + (mp.Name != null ? mp.Name.ToString() : mp.StringId) + "' " + Nm(__state.Old) + " -> " + Nm(neu) + ", dni od decyzji "
                         + (since >= 0 ? since.ToString("0.0", Inv) : "-") + " (limit " + (lim >= 0 ? lim.ToString("0.0", Inv) : "-") + ", droga z decyzji "
                         + (d0 >= 0 ? d0.ToString("0.0", Inv) : "-") + " dob), zrodlo " + src);
            }
            catch (Exception e) { Stumble("MovePost", e); }
        }

        private static string Nm(Settlement s) { return s == null ? "-" : s.Name != null ? s.Name.ToString() : s.StringId; }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var m = bkc != null ? AccessTools.DeclaredMethod(bkc, "HourlyTickPartyImpl", new[] { typeof(MobileParty) }) : null;
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CaravanSeaHold307), nameof(ImplPrefix)), finalizer: new HarmonyMethod(typeof(CaravanSeaHold307), nameof(ImplFinalizer)));
                    _wImpl = true;
                }
                var mv = AccessTools.DeclaredMethod(typeof(MobileParty), "SetMoveGoToSettlement", new[] { typeof(Settlement), typeof(MobileParty.NavigationType), typeof(bool) });
                if (mv != null)
                {
                    h.Patch(mv, prefix: new HarmonyMethod(typeof(CaravanSeaHold307), nameof(MovePre)), postfix: new HarmonyMethod(typeof(CaravanSeaHold307), nameof(MovePost)));
                    _wMove = true;
                }
                Log.Info("Karawany morskie - cel do doplyniecia (307a): BK HourlyTickPartyImpl " + (_wImpl ? "wpiety" : "NIE (brak metody BK)") + ", pomiar SetMoveGoToSettlement "
                         + (_wMove ? "wpiety" : "NIE") + ".");
            }
            catch (Exception e) { Log.Error("CaravanSeaHold307.ApplyAll", e); }
        }

        /// <summary>ArmouryBehavior (po CaravanGoods.Daily): linia doby i zerowanie licznikow; czyszczenie wpisow partii nieczynnych.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                if (_h.Count > 0)
                {
                    List<MobileParty> gone = null;
                    foreach (var kv in _h) if (kv.Key == null || !kv.Key.IsActive || kv.Key.CurrentSettlement != null) { if (gone == null) gone = new List<MobileParty>(); gone.Add(kv.Key); }
                    if (gone != null) foreach (var g in gone) { Hold ha; if (g != null && g.IsActive && _h.TryGetValue(g, out ha)) Arrived(g, ha); _h.Remove(g); }
                }
                // recenzja 307a (tylko pomiar): wpisy trzymane dluzej niz droga z decyzji i najstarszy wpis (brak postepu wychodzi tu, zanim zmieni sie regula)
                int hOver = 0, hExp = 0; double hMax = 0, nowD = CampaignTime.Now.ToDays;
                foreach (var kv in _h)
                {
                    var hh = kv.Value; if (hh == null) continue;
                    if (hh.Expired) { hExp++; continue; }                     // 307e: po limicie bez trzymania (BK decyduje) - nie liczy sie do najstarszego trzymania
                    double age = nowD - hh.Start;
                    if (age > hMax) hMax = age;
                    if (hh.Days0 > 0 && age > hh.Days0) hOver++;
                }
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder(700);
                sb.Append("Karawany morskie - cel do doplyniecia (307a): dzien ").Append(day)
                  .Append(" | klucz Caravan Sea Hold Target ").Append(s.CaravanSeaHoldTarget ? "TAK" : "NIE").Append(" (limit ").Append(s.CaravanSeaHoldFactor.ToString("0.0#", Inv))
                  .Append(" x droga z decyzji, ").Append(s.CaravanSeaHoldMinDays).Append("-").Append(s.CaravanSeaHoldMaxDays).Append(" dob)")
                  .Append(" | latki: tik BK ").Append(_wImpl ? "wpiety" : "NIE").Append(", pomiar celu ").Append(_wMove ? "wpiety" : "NIE")
                  .Append(" | tiki BK karawan morskich w drodze ").Append(_ticks).Append(", cel trzymany ").Append(_held).Append(" tikow u ").Append(_heldCars.Count).Append(" karawan, nowych celow trzymanych ")
                  .Append(_newHold).Append(", w trzymaniu teraz ").Append(_h.Count)
                  .Append(" | wpuszczone do BK: wojna ").Append(_exWar).Append(", oblezenie ").Append(_exSiege).Append(", limit dni ").Append(_exLimit).Append(", cel nie miasto z portem ")
                  .Append(_exNotPort).Append(", klucz NIE ").Append(_exOff).Append(", stoi (zachowanie None - BK zwalnia, 307d) ").Append(_exIdle)
                  .Append(", limit minal (307e: decyzja BK od razu) - nowy cel ").Append(_limNew).Append(", ten sam albo brak ").Append(_limSame)
                  .Append(" | zmiany celu w drodze: morskie ").Append(_chSea).Append(" (BK: wojna ").Append(_chWhy[1]).Append(", oblezenie ").Append(_chWhy[2])
                  .Append(", limit ").Append(_chWhy[3]).Append(", nie port ").Append(_chWhy[4]).Append(", losowa przy kluczu NIE ").Append(_chWhy[5]).Append(", inne ").Append(_chWhy[6])
                  .Append("; inna droga ").Append(_chOther).Append("), w tym karawany gracza ").Append(_chSeaPlayer).Append(", ladowe ").Append(_chLand)
                  .Append(" | gracza: ").Append(_det.Count == 0 ? "-" : string.Join("; ", _det.ToArray()))
                  .Append(" | doplyniecia do trzymanego celu ").Append(_arr).Append(" (po limicie ").Append(_arrOver).Append("), dni rejsu / droga z decyzji: srednio ")
                  .Append((_arr > 0 ? _arrRatioSum / _arr : 0).ToString("0.00", Inv)).Append(", najwiecej ").Append(_arrRatioMax.ToString("0.00", Inv))
                  .Append("; trzymane dluzej niz droga z decyzji ").Append(hOver).Append(", najstarsze trzymanie ").Append(hMax.ToString("0.0", Inv)).Append(" dob")
                  .Append(" (307e: wpisow po limicie bez trzymania ").Append(hExp).Append(')')
                  .Append("; gracza: ").Append(_arrDet.Count == 0 ? "-" : string.Join("; ", _arrDet.ToArray()))
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            _ticks = _held = _newHold = _exWar = _exSiege = _exLimit = _exNotPort = _exOff = _exIdle = 0; _heldCars.Clear();
            _limNew = _limSame = 0;
            _chSea = _chLand = _chSeaPlayer = _chOther = 0; Array.Clear(_chWhy, 0, _chWhy.Length); _det.Clear(); _stumbles = 0;
            _arr = _arrOver = 0; _arrRatioSum = _arrRatioMax = 0; _arrDet.Clear();
        }
    }
}
