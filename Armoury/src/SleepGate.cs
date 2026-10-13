using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E7/O1 (228, PLAN 7.6, projekt etapu 7 v2 rozdz. 3 "E7/O1", R5): SEN DO SWITU. Dwie rzeczy pod jednym wylacznikiem (SleepersHoldOrders):
    /// (1) FAZA SNU - wlasne AI spiacego (oboz swiata, sen dlugu, lord wstrzymany w osadzie) wylaczone do konca snu: nasz termin DisableForHours(1)
    ///     odnawiany w przegladach co 0.1 h gry, gdy zostalo < 0.6 h (AI wraca w MobilePartyAi.Tick po terminie - G MobilePartyAi.cs:437-442; przy
    ///     szybkiej grze jedna klatka moze trwac ok. 0.3 h gry); cudza blokada (termin > 1.0 h - uczta / gentry BK, DisableAi) nietknieta; o swicie
    ///     EnableAi dla obozu i osady (sen dlugu konczy ReleaseDebt jak dotad);
    /// (2) BRAMKA ROZKAZOW - prefiksy Priority.First na 10 MobileParty.SetMove* (bez SetMoveModeHold, SetMoveToNearestLand, SetMoveGoToInteractablePoint):
    ///     spiacy w obozie albo w snie dlugu (poza osada i bitwa) nie dostaje rozkazu ruchu - oryginal nie biegnie, a rozkaz zapisuje sie tam, gdzie
    ///     trzyma go dany sen: oboz -> _orders (oddaje swit), sen dlugu -> _debtSleep[mp].Order (oddaje ReleaseDebt). Nasze oddanie rozkazu (ApplyOrder)
    ///     przechodzi flaga _gatePass. 79% obudzen to karawany budzone przez BK (ReleaseCaravanFromHold -> SetMoveGoToSettlement bez patrzenia na AI).
    /// Harmony 2.4.2 (u Jeffa): po naszym false prefiksy bool / z parametrem referencyjnym sa pomijane (MethodCreator AffectsOriginal), postfiksy biegna -
    /// BK FreezeWatchdog.Exit tylko zeruje pola. Stan bez zapisu (jak _orders) - stary zapis wczytuje sie bez zmian.
    /// F7 (test zbiorczy 5-8, TRIAZ zb58 "E7/O1 T3"): JEDEN KONIEC SNU = TICK SWITU. Tick godzinowy gry nie bije o pelnej godzinie (nowa kampania: 0.5 h
    /// po starcie - G Campaign.cs:1012 initialWait2 = Hours(0.5f), w biegu 120 dob ok. 6:25-6:30 - z licznika przegladow; po wczytaniu faza inna), a pobudka (DawnWake, oddanie
    /// rozkazow, czyszczenie _camping / _bedPos) biegnie dopiero w ticku h = CampEnd. Bramka i straznik pytaly zegar, wiec od 6:00 do ticku switu partia
    /// lezala w obozie (AI wylaczone, namiot), ale brala cudze rozkazy i nikt jej nie kladl; w nowej kampanii ok. 0.5 h przed tickiem switu bije tick
    /// dobowy (wezwania GrandTourney - omijaly bramke, teraz ida przez wejscie metody). Teraz oboz trwa do ticku switu (CampHolds: flaga _campNight
    /// z galezi nocy / switu AiNightCamp); cudzy rozkaz zlapany przez straznika czeka do switu jak rozkaz zatrzymany bramka (KeepCaughtOrder).
    /// Pod tym samym kluczem (SleepersHoldOrders); wylaczony = zegar jak dotad.
    /// </summary>
    internal static partial class NightRest
    {
        internal const int SkNone = 0, SkCamp = 1, SkDebt = 2, SkTown = 3;

        // nasze oddanie rozkazu (ApplyOrder: swit, alarm, koniec snu dlugu, wylacznik) - bramka przepuszcza
        [ThreadStatic] private static bool _gatePass;

        // liczniki od poprzedniej linii AiNightCamp / NightRest swit (zerowane w ResetHourCounters, czyli razem z wypisaniem)
        private static int _gLord, _gLordSet, _gLordPt, _gLordEng, _gCar, _gCarBk, _gOther, _gDebt, _gDebtNew, _gGivenDebt;
        private static int _rnCamp, _rnDebt, _rnTown, _awLord, _awCar, _awOther, _wokenLord, _wokenCar;
        private static long _gateCalls; private static double _gateUsSum;
        private static int _dawnAiCamp, _dawnAiTown, _dawnGated, _dawnDebtors;
        private static int _gateStumbles;   // w sesji
        private static CampaignTime _lastTownSweep = CampaignTime.Zero;
        private static double _townDhMax;

        // odczyt terminu AI (prywatna wlasciwosc gry) - skompilowany delegat, raz na proces; tylko dla fazy snu (ForeignHold bez zmian - etap 8)
        private static Func<MobilePartyAi, CampaignTime> _enableAtGet; private static bool _enableAtGetTried;

        private static bool GateOn(Settings s) { return s != null && s.SleepersHoldOrders && s.NightRestEnabled; }

        // F7: oboz swiata trwa (stawiana w galezi nocy AiNightCamp, zdejmowana na poczatku galezi switu; ResetGate i MasterSwitch - od zera). Bez zapisu.
        private static bool _campNight;
        // F7: cudze rozkazy zlapane przez straznika i zachowane do switu - od poprzedniej linii AiNightCamp / NightRest swit (ResetGateHour)
        private static int _kLord, _kCar, _kOther;

        /// <summary>F7: czy oboz swiata trwa o godzinie zegara hourNow. Godzina obozu - tak (jak dotad). Pierwsza godzina PO obozie - tak, dopoki tick switu
        /// jeszcze nie przyszedl (_campNight) i klucz SleepersHoldOrders jest wlaczony: bramka, straznik i alarmy trzymaja do tego samego ticku, ktory budzi,
        /// niezaleznie od fazy ticku godzinowego. Zegar zostaje jako bezpiecznik: najdalej godzine po koncu obozu sen sie konczy, nawet gdyby tick switu
        /// w ogole nie doszedl do AiNightCamp (wyjatek wczesniej w AiHourly) i flaga zostala - jedna wywrotka nie usypia swiata na stale (CLAUDE.md 7).
        /// Wywrotke w samej galezi switu zalatwia kolejnosc: flaga schodzi jako pierwsza rzecz galezi. Recenzja F7: przy fazie ticku tuz przed pelna
        /// godzina i dlugiej klatce (szybka gra) tick switu moze przyjsc chwile po tej godzinie - bramka jest wtedy otwarta najwyzej jedna klatke.</summary>
        private static bool CampHolds(Settings s, int hourNow)
        {
            if (InCamp(hourNow)) return true;
            return _campNight && GateOn(s) && InCamp(hourNow - 1);
        }

        /// <summary>Jedna funkcja snu (R5): oboz swiata (legowisko _bedPos, dopoki oboz trwa - F7: do ticku switu; _bedPos zawsze razem z _camping), sen dlugu,
        /// lord wstrzymany w osadzie, brak. Bramka czyta ja przy kazdym rozkazie spiacego; przeglady odnawiaja AI wedlug stanu, ktory same prowadza.</summary>
        internal static int SleepKind(MobileParty mp)
        {
            if (mp == null) return SkNone;
            if (_bedPos.Count > 0 && _bedPos.ContainsKey(mp) && CampHolds(Settings.Current, CampaignTime.Now.GetHourOfDay)) return SkCamp;
            if (_debtSleep.Count > 0 && _debtSleep.ContainsKey(mp)) return SkDebt;
            if (_townHold.Count > 0 && _townHold.Contains(mp)) return SkTown;
            return SkNone;
        }

        /// <summary>Ile godzin gry zostalo do terminu AI (MobilePartyAi._enableAgainAtHour); false = brak gettera (faza snu nieczynna).</summary>
        private static bool EnableLeft(MobilePartyAi ai, out double leftH)
        {
            leftH = 0;
            if (!_enableAtGetTried)
            {
                _enableAtGetTried = true;
                try
                {
                    var mi = AccessTools.PropertyGetter(typeof(MobilePartyAi), "_enableAgainAtHour");
                    if (mi != null) _enableAtGet = (Func<MobilePartyAi, CampaignTime>)Delegate.CreateDelegate(typeof(Func<MobilePartyAi, CampaignTime>), mi);
                }
                catch (Exception e) { Log.Error("SleepGate.EnableLeft", e); }
                if (_enableAtGet == null) Log.Info("SleepGate (E7/O1): brak MobilePartyAi._enableAgainAtHour - faza snu nieczynna (bramka rozkazow dziala).");
            }
            var g = _enableAtGet;
            if (g == null || ai == null) return false;
            leftH = (g(ai) - CampaignTime.Now).ToHours;
            return true;
        }

        /// <summary>Faza snu jednej partii (kind: SkCamp / SkDebt / SkTown): AI wstalo przed switem - liczone i wylaczone od nowa; nasz termin
        /// (< 0.6 h do konca) - od nowa na godzine. Cudza blokada (dluzszy termin) nietknieta.</summary>
        private static void KeepAsleep(MobileParty mp, int kind)
        {
            var ai = mp.Ai;
            if (ai == null) return;
            if (!ai.IsDisabled)
            {
                if (mp.IsLordParty) _awLord++; else if (mp.IsCaravan) _awCar++; else _awOther++;
                ai.DisableForHours(1);
                Renewed(kind);
                return;
            }
            double left;
            if (!EnableLeft(ai, out left)) return;
            if (left < 0.6) { ai.DisableForHours(1); Renewed(kind); }
        }

        private static void Renewed(int kind)
        {
            if (kind == SkCamp) _rnCamp++; else if (kind == SkDebt) _rnDebt++; else _rnTown++;
        }

        /// <summary>Przeglad lordow wstrzymanych w osadzie (co 0.1 h gry, OnTick) - tylko faza snu; zwolnienia (oblezenie, doczepienie, alarm we wsi,
        /// powod) robi jak dotad TownNight o pelnej godzinie, tu tylko nie odnawiamy AI tym, ktorych TownNight i tak by puscil.</summary>
        private static void KeepTownAsleep()
        {
            if (_townHold.Count == 0) return;
            var list = new List<MobileParty>(_townHold);
            foreach (var mp in list)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.MapEvent != null || mp.AttachedTo != null) continue;
                    var st = mp.CurrentSettlement;
                    if (st == null || st.SiegeEvent != null) continue;   // wyjechal (AiNightCamp go wypisze) albo oblezenie (TownNight zwolni)
                    KeepAsleep(mp, SkTown);
                }
                catch (Exception e) { GuardStumble("KeepTownAsleep", mp, e); }
            }
        }

        /// <summary>Swit (galaz switu AiNightCamp, PRZED czyszczeniem _camping i oddaniem rozkazow): EnableAi dla obozu i osady, ktorym AI wylaczylismy
        /// MY (termin <= 1.0 h); liczniki do linii switu: rozkazy zatrzymane bramka i oddawane teraz, dluznicy wsrod oddawanych (ma byc 0).</summary>
        private static void DawnWake(Settings s)
        {
            if (!GateOn(s)) return;
            foreach (var mp in _camping)
            {
                try { if (WakeOurs(mp)) _dawnAiCamp++; } catch (Exception e) { GuardStumble("DawnWake", mp, e); }
            }
            foreach (var mp in _townHold)
            {
                try { if (WakeOurs(mp)) _dawnAiTown++; } catch (Exception e) { GuardStumble("DawnWake", mp, e); }
            }
            foreach (var kv in _orders)
            {
                if (kv.Value != null && kv.Value.Gated) _dawnGated++;
                // dluznik w polu z wpisem w _orders - galaz switu oddalaby mu rozkaz (nasze oddanie przechodzi bramke) i budzila go o 6:00; ma byc 0
                if (kv.Key != null && _debtSleep.ContainsKey(kv.Key) && kv.Key.IsActive && kv.Key.CurrentSettlement == null && kv.Key.MapEvent == null) _dawnDebtors++;
            }
        }

        private static bool WakeOurs(MobileParty mp)
        {
            if (mp == null || !mp.IsActive || mp.Ai == null || !mp.Ai.IsDisabled) return false;
            double left;
            if (!EnableLeft(mp.Ai, out left) || left > 1.0) return false;   // cudza blokada (uczta / gentry BK) - nie nasza sprawa
            mp.Ai.EnableAi();
            return true;
        }

        // ------------------------------------------------------------ bramka rozkazow (prefiksy SetMove*)
        public static bool GateEngage(MobileParty __instance, MobileParty __0, MobileParty.NavigationType __1)
        { return Gate(__instance, AiBehavior.EngageParty, null, __0, default(CampaignVec2), false, __1, false); }

        public static bool GateGoAround(MobileParty __instance, MobileParty __0, MobileParty.NavigationType __1)
        { return Gate(__instance, AiBehavior.GoAroundParty, null, __0, default(CampaignVec2), false, __1, false); }

        public static bool GateGoToSettlement(MobileParty __instance, Settlement __0, MobileParty.NavigationType __1, bool __2)
        { return Gate(__instance, AiBehavior.GoToSettlement, __0, null, default(CampaignVec2), false, __1, __2); }

        public static bool GateGoToPoint(MobileParty __instance, CampaignVec2 __0, MobileParty.NavigationType __1)
        { return Gate(__instance, AiBehavior.GoToPoint, null, null, __0, true, __1, false); }

        public static bool GateEscort(MobileParty __instance, MobileParty __0, MobileParty.NavigationType __1, bool __2)
        { return Gate(__instance, AiBehavior.EscortParty, null, __0, default(CampaignVec2), false, __1, __2); }

        public static bool GatePatrolPoint(MobileParty __instance, CampaignVec2 __0, MobileParty.NavigationType __1)
        { return Gate(__instance, AiBehavior.PatrolAroundPoint, null, null, __0, true, __1, false); }

        public static bool GatePatrolSettlement(MobileParty __instance, Settlement __0, MobileParty.NavigationType __1, bool __2)
        { return Gate(__instance, AiBehavior.PatrolAroundPoint, __0, null, default(CampaignVec2), false, __1, __2); }

        public static bool GateRaid(MobileParty __instance, Settlement __0, MobileParty.NavigationType __1, bool __2)
        { return Gate(__instance, AiBehavior.RaidSettlement, __0, null, default(CampaignVec2), false, __1, __2); }

        public static bool GateBesiege(MobileParty __instance, Settlement __0, MobileParty.NavigationType __1)
        { return Gate(__instance, AiBehavior.BesiegeSettlement, __0, null, default(CampaignVec2), false, __1, false); }

        public static bool GateDefend(MobileParty __instance, Settlement __0, bool __1, MobileParty.NavigationType __2)
        { return Gate(__instance, AiBehavior.DefendSettlement, __0, null, default(CampaignVec2), false, __2, __1); }

        /// <summary>Warunek (kolejnosc = koszt): nasze oddanie -> dzien bez spiacych -> klucz -> nie gracz -> SleepKind oboz / dlug -> poza bitwa i osada.
        /// true = rozkaz idzie jak w grze; false = zapisany tam, gdzie trzyma go sen (oryginal i prefiksy BK z parametrem ref pominiete).</summary>
        private static bool Gate(MobileParty mp, AiBehavior b, Settlement st, MobileParty tp, CampaignVec2 pt, bool hasPt, MobileParty.NavigationType nav, bool port)
        {
            if (_gatePass || mp == null) return true;
            if (_bedPos.Count == 0 && _debtSleep.Count == 0) return true;   // nikt nie spi w obozie ani snem dlugu
            var s = Settings.Current;
            if (!GateOn(s) || mp == MobileParty.MainParty) return true;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                int k = SleepKind(mp);
                if (k != SkCamp && k != SkDebt) return true;
                // bitwa - bez bramki; osada - gra nie wypusci partii z wylaczonym AI (CheckExitingSettlementParallel), rozkaz czeka w partii (R5)
                if (mp.MapEvent != null || mp.CurrentSettlement != null) return true;
                var o = new NightOrder { Behavior = b, Settlement = st, Party = tp, Point = pt, HasPoint = hasPt, Nav = nav, Port = port, Gated = true };
                if (k == SkCamp)
                {
                    _orders[mp] = o;   // nadpisuje zapamietany przed snem - swit odda najnowszy (ze sprawdzeniem waznosci w ApplyOrder)
                    if (mp.IsLordParty)
                    {
                        _gLord++;
                        if (b == AiBehavior.GoToSettlement) _gLordSet++; else if (b == AiBehavior.GoToPoint) _gLordPt++; else if (b == AiBehavior.EngageParty) _gLordEng++;
                    }
                    else if (mp.IsCaravan)
                    {
                        _gCar++;
                        if (b == AiBehavior.GoToSettlement && mp.DefaultBehavior == AiBehavior.Hold) _gCarBk++;   // zwolnienie z Hold (BK ReleaseCaravanFromHold)
                    }
                    else _gOther++;
                }
                else
                {
                    DebtSleeper ds;
                    if (!_debtSleep.TryGetValue(mp, out ds) || ds == null) return true;
                    if (ds.Order == null || !ds.Order.Gated) _gDebtNew++;
                    ds.Order = o;      // oddaje ReleaseDebt przy dlugu 0 (nie swit)
                    _gDebt++;
                }
                return false;
            }
            catch (Exception e)
            {
                // potkniecie = rozkaz idzie jak w grze (straznik po fakcie zostaje); liczone, bez gaszenia bramki
                _gateStumbles++;
                if (_gateStumbles <= 3 || _gateStumbles % 100 == 0)
                {
                    string who = "?";
                    try { who = mp.StringId; } catch { }
                    Log.Error("SleepGate (partia " + who + ", potkniecie " + _gateStumbles + " w sesji)", e);
                }
                return true;
            }
            finally
            {
                _gateCalls++;
                _gateUsSum += (Stopwatch.GetTimestamp() - t0) * 1000000.0 / Stopwatch.Frequency;
            }
        }

        /// <summary>F7 (siatka, TRIAZ zb58 "E7/O1 T3" pkt 3): cudzy rozkaz, ktory ominal bramke (wolajacy skompilowany przed wpieciem latki - JIT wkleja wtedy
        /// kopie krotkiej metody gry i prefiks nie biegnie) i zostal zlapany przez straznika HoldSleepers - zapisany do _orders jak rozkaz zatrzymany bramka
        /// (swit odda najnowszy, ze sprawdzeniem waznosci w ApplyOrder), ZANIM straznik polozy partie na Hold. Tylko rozkaz, ktory ApplyOrder umie oddac;
        /// reszta (Hold / None, MoveToNearestLandOrPort, punkt z obiektem mapy, brak celu) niczego nie nadpisuje - zostaje rozkaz sprzed snu, jak dotad.
        /// Stan czytany z partii tak, jak go ustawia gra (G MobileParty.SetMove*: DefaultBehavior, TargetSettlement, TargetParty, TargetPosition,
        /// DesiredAiNavigationType, IsTargetingPort).</summary>
        private static void KeepCaughtOrder(MobileParty mp)
        {
            var b = mp.DefaultBehavior;
            var st = mp.TargetSettlement; var tp = mp.TargetParty;
            bool hasPt = false;
            switch (b)
            {
                case AiBehavior.GoToSettlement:
                case AiBehavior.BesiegeSettlement:
                case AiBehavior.RaidSettlement:
                case AiBehavior.DefendSettlement:
                    if (st == null) return;
                    break;
                case AiBehavior.EngageParty:
                case AiBehavior.EscortParty:
                case AiBehavior.GoAroundParty:
                    if (tp == null) return;
                    break;
                case AiBehavior.PatrolAroundPoint:
                    hasPt = st == null;   // z osada - ApplyOrder oddaje patrol wokol osady
                    break;
                case AiBehavior.GoToPoint:
                    if (mp.Ai != null && mp.Ai.AiBehaviorInteractable != null) return;   // cel z obiektem mapy (SetMoveGoToInteractablePoint) - sam punkt to inny rozkaz
                    hasPt = true;
                    break;
                default:
                    return;
            }
            var nav = mp.DesiredAiNavigationType;
            if (nav == MobileParty.NavigationType.None) nav = MobileParty.NavigationType.Default;   // jak wpis z RememberOrder
            _orders[mp] = new NightOrder { Behavior = b, Settlement = st, Party = tp, Point = hasPt ? mp.TargetPosition : default(CampaignVec2), HasPoint = hasPt,
                                           Nav = nav, Port = mp.IsTargetingPort, Gated = true };
            if (mp.IsLordParty) _kLord++; else if (mp.IsCaravan) _kCar++; else _kOther++;
        }

        // ------------------------------------------------------------ linie i stan
        private static void ResetGateHour()
        {
            _kLord = 0; _kCar = 0; _kOther = 0;   // F7
            _gLord = 0; _gLordSet = 0; _gLordPt = 0; _gLordEng = 0; _gCar = 0; _gCarBk = 0; _gOther = 0; _gDebt = 0; _gDebtNew = 0; _gGivenDebt = 0;
            _rnCamp = 0; _rnDebt = 0; _rnTown = 0; _awLord = 0; _awCar = 0; _awOther = 0; _wokenLord = 0; _wokenCar = 0;
            _gateCalls = 0; _gateUsSum = 0; _townDhMax = 0;
            _dawnAiCamp = 0; _dawnAiTown = 0; _dawnGated = 0; _dawnDebtors = 0;
        }

        /// <summary>Nowa gra albo wczytanie (ResetWorld): zegar przegladu osad od zera.</summary>
        private static void ResetGate()
        {
            _lastTownSweep = CampaignTime.Zero;
            _gatePass = false;
            _campNight = false;   // F7: oboz swiata nie trwa, dopoki nie przyjdzie pierwszy tick nocy
        }

        /// <summary>Dopisek E7/O1 do linii godzinowej "AiNightCamp" i do linii "NightRest swit" (wartosci od poprzedniej z tych linii).</summary>
        private static string GateText()
        {
            var ci = CultureInfo.InvariantCulture;
            var s = Settings.Current;
            return " | E7/O1 (SleepersHoldOrders " + (s != null && s.SleepersHoldOrders ? "on" : "off") + "): obudzeni cudza reka: lordowie " + _wokenLord
                   + ", karawany " + _wokenCar + " | rozkazy zatrzymane u spiacych: lordowie " + _gLord + " (GoToSettlement " + _gLordSet + ", GoToPoint " + _gLordPt
                   + ", Engage " + _gLordEng + ", inne " + (_gLord - _gLordSet - _gLordPt - _gLordEng) + "), karawany " + _gCar + " (zwolnienie z Hold przez BK " + _gCarBk
                   + ", inne " + (_gCar - _gCarBk) + "), inne partie " + _gOther + "; do snu dlugu " + _gDebt + " (nowych dluznikow " + _gDebtNew
                   + "), oddane przy koncu snu dlugu " + _gGivenDebt + " | AI odnowione " + (_rnCamp + _rnDebt + _rnTown) + " (oboz " + _rnCamp + "/dlug " + _rnDebt
                   + "/osada " + _rnTown + ") | AI wstalo przed switem: lordowie " + _awLord + ", karawany " + _awCar + ", inne " + _awOther
                   + " | SleepGate " + _gateCalls + " x, sr. " + (_gateCalls > 0 ? _gateUsSum / _gateCalls : 0).ToString("0.0", ci) + " us, razem "
                   + (_gateUsSum / 1000.0).ToString("0.000", ci) + " ms, potkniecia w sesji " + _gateStumbles + "; przeglad osad maks. odstep "
                   + _townDhMax.ToString("0.000", ci) + " h"
                   // F7: dopisek NA KONCU (wzorce narzedzia czytaja poczatek dopisku E7/O1 - bez zmian)
                   + " | F7: cudze rozkazy zlapane przez straznika i zachowane do switu: lordowie " + _kLord + ", karawany " + _kCar + ", inne " + _kOther;
        }

        /// <summary>Dopisek E7/O1 do linii switu (DawnWake liczy przed wypisaniem).</summary>
        private static string DawnText()
        {
            return " | E7/O1 swit: AI wlaczone o swicie " + (_dawnAiCamp + _dawnAiTown) + " (oboz " + _dawnAiCamp + "/osada " + _dawnAiTown
                   + "), rozkazy zatrzymane i oddane o swicie " + _dawnGated + ", dluznicy obudzeni o 6:00 " + _dawnDebtors;
        }

        // ------------------------------------------------------------ wpiecie
        /// <summary>Prefiksy bramki na 10 MobileParty.SetMove* - wpiete zawsze (menu glowne, jak Wayfinder - przed JIT wolajacych), dzialaja przy kluczu.</summary>
        internal static void ApplySleepGate(Harmony h)
        {
            int ok = 0;
            var miss = new List<string>();
            var nt = typeof(MobileParty.NavigationType);
            GatePatch(h, "SetMoveEngageParty", new[] { typeof(MobileParty), nt }, "GateEngage", ref ok, miss);
            GatePatch(h, "SetMoveGoAroundParty", new[] { typeof(MobileParty), nt }, "GateGoAround", ref ok, miss);
            GatePatch(h, "SetMoveGoToSettlement", new[] { typeof(Settlement), nt, typeof(bool) }, "GateGoToSettlement", ref ok, miss);
            GatePatch(h, "SetMoveGoToPoint", new[] { typeof(CampaignVec2), nt }, "GateGoToPoint", ref ok, miss);
            GatePatch(h, "SetMoveEscortParty", new[] { typeof(MobileParty), nt, typeof(bool) }, "GateEscort", ref ok, miss);
            GatePatch(h, "SetMovePatrolAroundPoint", new[] { typeof(CampaignVec2), nt }, "GatePatrolPoint", ref ok, miss);
            GatePatch(h, "SetMovePatrolAroundSettlement", new[] { typeof(Settlement), nt, typeof(bool) }, "GatePatrolSettlement", ref ok, miss);
            GatePatch(h, "SetMoveRaidSettlement", new[] { typeof(Settlement), nt, typeof(bool) }, "GateRaid", ref ok, miss);
            GatePatch(h, "SetMoveBesiegeSettlement", new[] { typeof(Settlement), nt }, "GateBesiege", ref ok, miss);
            GatePatch(h, "SetMoveDefendSettlement", new[] { typeof(Settlement), typeof(bool), nt }, "GateDefend", ref ok, miss);
            Log.Info("SleepGate (E7/O1): bramka rozkazow spiacych wpieta na " + ok + "/10 metodach MobileParty.SetMove*"
                     + (miss.Count > 0 ? " - NIE wpiete: " + string.Join("; ", miss.ToArray()) : "") + "; dziala przy SleepersHoldOrders.");
        }

        private static void GatePatch(Harmony h, string name, Type[] args, string prefix, ref int ok, List<string> miss)
        {
            try
            {
                var m = AccessTools.Method(typeof(MobileParty), name, args);
                var p = typeof(NightRest).GetMethod(prefix, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (m == null || p == null) { miss.Add(name + (m == null ? " (brak metody gry)" : " (brak prefiksu)")); return; }
                h.Patch(m, prefix: new HarmonyMethod(p) { priority = Priority.First });
                ok++;
            }
            catch (Exception e) { miss.Add(name + " (" + e.GetType().Name + ")"); Log.Error("SleepGate.ApplyAll " + name, e); }
        }
    }
}
