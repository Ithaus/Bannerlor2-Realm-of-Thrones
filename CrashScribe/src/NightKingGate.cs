using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CrashScribe
{
    /// <summary>
    /// KALENDARZ INNYCH - BLOKADA OBLEZEN (T2c noc 08/09.10; Jeff: "Nieumarli powinni zaczynac podboj dopiero po kilku latach,
    /// tak jak w ksiazkach").
    ///
    /// Dowod (autotest 120 dob, sesja 2026-10-08_20-07-56): Nocny Krol oblegal The Wall (dzien z kalendarza 2184) przez 44 doby,
    /// pierwszy raz w 55. dobie; TurnBack z T2 zawrocil go 19 razy, a ROT w ciagu kilku godzin dawal ten sam rozkaz ponownie.
    /// Gerrick dwa razy oblegal Mur, Lenyl dwa razy Craster's Keep (728). Pod Murem Nocna Straz stracila 400 i 1003 zabitych.
    ///
    /// Skad oblezenia (dekompilacja ROT 8.1.8 i gry 1.4.8; StrategicCampaignAI rozkazuje tylko armiom krolestw, BK - nic dla Innych):
    ///  - ROT.CampaignBehaviors.ROTOthersCampaignBehavior.OnAiHourlyTick (zdarzenie AiHourlyTick, ok. co 6 h na partie): banda z
    ///    >= 500 zdrowymi dostaje BesiegeSettlement z wynikiem 999 na cel FindMostNorthernWesterosiFortification. Mur nie ma kajdan
    ///    ROT (dla Nocnego Krola to jedyny cel poza miastami, a miasta maja kajdany x4.33), Piesc i Craster tez nie. Banda juz
    ///    oblegajaca dostaje ten sam cel ponownie (l.955-975 -> IL_029e). Rabunek, obrona i patrol ROT maja willGatherArmy = true
    ///    i dla rodu bez krolestwa nie sa wykonywane (AIThinkPatch l.417-428).
    ///  - ROT.HarmonyPatches.Core.AIThinkPatch.PartyHourlyAiTick (prefiks zamiast AiPartyThinkBehavior.PartyHourlyAiTick) wola
    ///    CampaignEventDispatcher.AiHourlyTick, bierze najlepszy wynik i wola SetPartyAiAction.GetActionForBesiegingSettlement.
    ///  - Oblezenie zaczyna sie, gdy banda z takim rozkazem dojdzie do osady: Settlement.OnPartyInteraction ->
    ///    EncounterManager.StartSettlementEncounter -> SiegeEventManager.StartSiegeEvent (albo dolaczenie do obozu). Ta sama
    ///    metoda zaczyna szturm (ShortTermBehavior AssaultSettlement -> StartBattleAction.ApplyStartAssaultAgainstWalls)
    ///    i rabunek wioski (ShortTermBehavior RaidSettlement -> StartBattleAction.ApplyStartRaid).
    ///
    /// Latki - tylko partie Innych (ActualClan = klan ROTclan_126 albo kultura whitewalker; bez gracza i jego klanu); cel zamkniety
    /// = osada nie Innych przed NightKingCall.OpenDay (wioska - dniem swojego zamku / miasta, Mur 2184, reszta jak w T2):
    ///  1. ROZKAZ AI - postfiks CampaignEventDispatcher.AiHourlyTick (Priority.Last, po filtrze ROT AIThinkPatch.Patch1):
    ///     z listy wynikow znikaja BesiegeSettlement / AssaultSettlement / RaidSettlement na zamkniety cel. Nic nie dodajemy -
    ///     banda zostaje przy dotychczasowym rozkazie (patrol). Partia w bitwie (MapEvent) - bez zmian (inaczej AIThinkPatch l.393
    ///     zakonczylby bitwe w polowie przez FinalizeEvent).
    ///  2. BRAMA - prefiks EncounterManager.StartSettlementEncounter: partia Innych z rozkazem oblezenia / szturmu / rabunku TEJ
    ///     osady, zamknietej - spotkanie nie zachodzi (ani nowe oblezenie, ani dolaczenie do obozu, ani szturm, ani rabunek),
    ///     banda dostaje patrol przy siedzibie klanu (jak TurnBack z T2; brak siedziby - postoj). Lapie rozkazy spoza AI
    ///     (z zapisu, z innych modow). Gdy banda juz oblegala (szturm z obozu) - oblezenie przerwane (gra zwija oboz).
    ///  3. TRWAJACE OBLEZENIE - NightKingCall.TurnBack (raz na dobe i w 1. godzinie po wczytaniu): banda oblegajaca zamkniety cel,
    ///     bez bitwy - ten sam patrol; gra sama zwija jej oboz (SiegeEvent.Tick -> BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave:
    ///     partia z rozkazem innym niz oblezenie odchodzi, bez ostatniej partii oblezenie sie konczy) - bez strat w ludziach.
    /// Wylacznik: NightKingCalendarSiegeGate (CrashScribe.settings.xml), dziala tylko przy NightKingCalendarEnabled.
    /// Oblezen innych frakcji (Nocna Straz, Wolni Ludzie, krolestwa) nie dotyka. Bez zapisu w grze.
    /// </summary>
    internal static class NightKingGate
    {
        private const string OthersClanId = "ROTclan_126";   // ROT ROTClans.WhiteWalkers => GetClanByID("ROTclan_126")

        // dzis (od ostatniej linii dobowej)
        private static int _aiSieges, _aiRaids, _gateSieges, _gateRaids, _broken;
        // od wczytania (sesja kampanii)
        private static int _totBlocked, _totRaids, _totBroken;
        private static int _stumbles, _stumblesDay;
        // linia logu raz na dobe na pare banda-osada-rodzaj (rozkaz AI wraca co ok. 6 h)
        private static readonly HashSet<string> _told = new HashSet<string>();

        /// <summary>Blokada czynna: kalendarz T2 i jej wlasny wylacznik.</summary>
        internal static bool Active { get { return Config.NightKingCalendarEnabled && Config.NightKingCalendarSiegeGate; } }

        /// <summary>Oblezenia przerwane od ostatniej linii dobowej (do linii "przeglad band po wczytaniu").</summary>
        internal static int BrokenToday { get { return _broken; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        internal static void Install(Harmony harmony)
        {
            try
            {
                if (!Active)
                {
                    Scribe.Line("Kalendarz Innych - blokada oblezen: wylaczona ("
                                + (!Config.NightKingCalendarEnabled ? "NightKingCalendarEnabled" : "NightKingCalendarSiegeGate") + " = false).");
                    return;
                }
                var parts = new List<string>();
                int hooked = 0;

                var mThink = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (mThink == null) parts.Add("rozkazy AI NIEZNALEZIONE");
                else
                {
                    try
                    {
                        harmony.Patch(mThink, postfix: new HarmonyMethod(typeof(NightKingGate), nameof(ThinkPostfix)) { priority = Priority.Last });
                        hooked++; parts.Add("rozkazy AI wpiete");
                    }
                    catch (Exception e) { parts.Add("rozkazy AI BLAD"); Stumble("NightKingGate.Install(think)", e); }
                }

                var mGate = AccessTools.Method(typeof(EncounterManager), "StartSettlementEncounter", new[] { typeof(MobileParty), typeof(Settlement) });
                if (mGate == null) parts.Add("brama NIEZNALEZIONA");
                else
                {
                    try
                    {
                        harmony.Patch(mGate, prefix: new HarmonyMethod(typeof(NightKingGate), nameof(EncounterPrefix)));
                        hooked++; parts.Add("brama wpieta");
                    }
                    catch (Exception e) { parts.Add("brama BLAD"); Stumble("NightKingGate.Install(gate)", e); }
                }

                Scribe.Line("Kalendarz Innych - blokada oblezen: " + string.Join(", ", parts.ToArray()) + " - wpiete " + hooked
                            + "/2. Trwajace oblezenia zamknietych celow przerywa przeglad band (raz na dobe i po wczytaniu).");
            }
            catch (Exception e) { Stumble("NightKingGate.Install", e); }
        }

        /// <summary>Partia Innych: klan ROTclan_126 albo kultura whitewalker (jak ROT IsWhiteWalker); bez gracza i jego klanu.</summary>
        internal static bool IsOthersParty(MobileParty mp)
        {
            if (mp == null || mp.IsMainParty) return false;
            var c = mp.ActualClan;
            if (c == null || c == Clan.PlayerClan) return false;
            return c.StringId == OthersClanId || NightKingCall.IsOthers(c);
        }

        /// <summary>Wedlug czego liczyc dzien: wioska - wedlug swojego zamku / miasta, reszta - wedlug siebie.</summary>
        private static Settlement CalendarTarget(Settlement s)
        {
            try { if (s != null && s.IsVillage && s.Village != null && s.Village.Bound != null) return s.Village.Bound; }
            catch { }
            return s;
        }

        internal static int OpenDayOf(Settlement s) { return NightKingCall.OpenDay(CalendarTarget(s)); }

        /// <summary>Zamkniety cel dla Innych: osada nie Innych (zdobytych nie ruszamy), dzien z kalendarza jeszcze nie nadszedl.</summary>
        internal static bool GateClosed(Settlement s, double day)
        {
            if (s == null || !Config.NightKingCalendarEnabled) return false;
            if (NightKingCall.IsOthers(s.OwnerClan)) return false;
            return NightKingCall.Closed(CalendarTarget(s), day);
        }

        /// <summary>Cel wyniku AI: osada (IMapPoint to Settlement albo MobileParty; wynik "z pozycji" ma Party == null -
        /// ROT tak zapisuje rabunek i patrol, ale dla rodu bez krolestwa ich nie wykonuje).</summary>
        private static Settlement AsSettlement(IMapPoint p)
        {
            return p as Settlement;
        }

        /// <summary>Banda Innych odchodzi spod zamknietego celu: patrol przy siedzibie klanu (jak TurnBack z T2),
        /// a gdy siedziby brak albo to ta sama osada - postoj (AI wybierze cos innego, bez oblezenia). Zwraca opis do logu.</summary>
        internal static string Redirect(MobileParty mp, Settlement from)
        {
            Settlement home = null;
            try { var c = mp.ActualClan; if (c != null) home = c.HomeSettlement; } catch { }
            if (home == null) { try { home = mp.HomeSettlement; } catch { } }
            if (home != null && home != from)
            {
                SetPartyAiAction.GetActionForPatrollingAroundSettlement(mp, home, MobileParty.NavigationType.Default, false, false);
                return "patrol przy " + home.Name;
            }
            mp.SetMoveModeHold();
            return "postoj (brak siedziby do patrolu)";
        }

        internal static void CountBroken() { _broken++; _totBroken++; }

        private static void Tell(MobileParty mp, Settlement s, string kind, string tag, double day, string what)
        {
            try
            {
                string who = mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : mp.Name.ToString();
                if (!_told.Add(who + "|" + s.StringId + "|" + kind + "|" + tag)) return;
                Scribe.Line("Kalendarz Innych: " + who + " - " + kind + " " + s.Name + " przed terminem (otwarta od dnia " + OpenDayOf(s)
                            + ", dzis " + (int)day + "): " + what + ".");
            }
            catch { }
        }

        /// <summary>Postfiks CampaignEventDispatcher.AiHourlyTick(partia, PartyThinkParams): partia Innych nie dostaje
        /// oblezenia / szturmu / rabunku zamknietego celu - wyniki znikaja z listy, zanim AIThinkPatch wybierze najlepszy.</summary>
        public static void ThinkPostfix(MobileParty __0, PartyThinkParams __1)
        {
            try
            {
                if (!Active || __1 == null || !IsOthersParty(__0)) return;
                if (__0.MapEvent != null) return;   // bitwa w toku - bez zmian
                var list = __1.AIBehaviorScores;   // MBReadOnlyList<T> : List<T> - ta sama lista, ktora czyta AIThinkPatch
                if (list == null || list.Count == 0) return;
                double day = 0.0; bool dayKnown = false;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var d = list[i].Item1;
                    var b = d.AiBehavior;
                    if (b != AiBehavior.BesiegeSettlement && b != AiBehavior.AssaultSettlement && b != AiBehavior.RaidSettlement) continue;
                    var s = AsSettlement(d.Party);
                    if (s == null) continue;
                    if (!dayKnown) { day = NightKingCall.Day(); dayKnown = true; }
                    if (!GateClosed(s, day)) continue;
                    list.RemoveAt(i);
                    bool raid = b == AiBehavior.RaidSettlement;
                    if (raid) { _aiRaids++; _totRaids++; } else { _aiSieges++; _totBlocked++; }
                    Tell(__0, s, raid ? "rabunek" : "oblezenie", "ai", day, "rozkaz AI zablokowany");
                }
            }
            catch (Exception e) { Stumble("NightKingGate.Think", e); }
        }

        /// <summary>Prefiks EncounterManager.StartSettlementEncounter(partia, osada): banda Innych u bram zamknietej osady
        /// z rozkazem oblezenia / szturmu / rabunku - spotkanie nie zachodzi, banda odchodzi na patrol.</summary>
        public static bool EncounterPrefix(MobileParty __0, Settlement __1)
        {
            try
            {
                if (!Active || __1 == null || !IsOthersParty(__0)) return true;
                var mp = __0; var s = __1;
                bool siege = mp.DefaultBehavior == AiBehavior.BesiegeSettlement && mp.TargetSettlement == s;
                bool assault = mp.ShortTermBehavior == AiBehavior.AssaultSettlement && mp.ShortTermTargetSettlement == s;
                bool raid = (mp.ShortTermBehavior == AiBehavior.RaidSettlement && mp.ShortTermTargetSettlement == s)
                            || (mp.DefaultBehavior == AiBehavior.RaidSettlement && mp.TargetSettlement == s);
                if (!siege && !assault && !raid) return true;
                double day = NightKingCall.Day();
                if (!GateClosed(s, day)) return true;

                bool inSiege = mp.BesiegerCamp != null;
                string where = Redirect(mp, s);
                if (inSiege)
                {
                    CountBroken();
                    Tell(mp, s, "oblezenie", "brama-przerwane", day, "szturm zablokowany u bram, oblezenie przerwane - " + where + "; gra zwija oboz bez strat");
                }
                else if (raid && !siege && !assault)
                {
                    _gateRaids++; _totRaids++;
                    Tell(mp, s, "rabunek", "brama", day, "zablokowany u bram - " + where);
                }
                else
                {
                    _gateSieges++; _totBlocked++;
                    Tell(mp, s, "oblezenie", "brama", day, "zablokowane u bram - " + where);
                }
                return false;
            }
            catch (Exception e) { Stumble("NightKingGate.Encounter", e); return true; }
        }

        /// <summary>Raz na dobe (NightKingCall.Daily): "Kalendarz Innych: oblezen zablokowanych N, przerwanych M (...)".</summary>
        internal static string DailyLine()
        {
            string line;
            try
            {
                line = "Kalendarz Innych: oblezen zablokowanych " + (_aiSieges + _gateSieges) + ", przerwanych " + _broken
                       + " (dzis: rozkazy AI na oblezenie " + _aiSieges + ", u bram " + _gateSieges
                       + "; rabunkow zablokowanych " + (_aiRaids + _gateRaids)
                       + "; od wczytania: oblezen zablokowanych " + _totBlocked + ", przerwanych " + _totBroken + ", rabunkow " + _totRaids + ")"
                       + (_stumblesDay > 0 ? "; potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ", pierwsze w raporcie)" : "")
                       + ".";
            }
            catch { line = "Kalendarz Innych: oblezen zablokowanych ?, przerwanych ? (blad linii)."; }
            _aiSieges = _aiRaids = _gateSieges = _gateRaids = _broken = 0;
            _stumblesDay = 0;
            _told.Clear();
            return line;
        }

        /// <summary>Nowa gra / wczytanie: liczniki od zera (latki zostaja - zakladane raz przy starcie gry).</summary>
        internal static void ResetSession()
        {
            _aiSieges = _aiRaids = _gateSieges = _gateRaids = _broken = 0;
            _totBlocked = _totRaids = _totBroken = 0;
            _stumblesDay = 0;
            _told.Clear();
        }
    }
}
