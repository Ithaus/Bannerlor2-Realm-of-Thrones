using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 578 (Jeff 16.11, odpowiedz na "czego 577 nie obejmuje": "1. wyrownac 2. tez z postojem 3. teraz bedzie 4. to dodaj 1/4 aby bylo dokladnie"):
    ///  (1) AI - NOCNY POSTOJ PRZY RABUNKU LORDA AI (klucz RaidNightHaltAi): rabunek wsi gry, ktory prowadzi partia lorda AI (sam lord albo wodz armii AI, takze z graczem
    ///      w armii), w nocy obozu (BedNight zegara - te same godziny co 577, przy 21-3 godziny 21:00-2:59) stoi: RaidHaltHolds zwraca true dla tego komponentu, wiec te same
    ///      prefiksy co 577 (DistrictRaid.UpdatePrefix, Devastation.RaidPrefix, SmallGold.RaidPre) nie robia postepu. Decyzja co godzine (AiHourly, PRZED ksiega AI) - wpis
    ///      _aiHalt[komponent].On; granice nocy dokladnie z zegara (RaidHaltHolds czyta BedNight godziny zegara), wiec wpis zakladany juz godzine przed zmierzchem.
    ///      AI DECYDUJE DALEJ: postoj blokuje tylko postep rabunku - AI wodza nie jest wylaczane (NocnyMarsz pomija partie z MapEvent, AiPartyThinkBehavior mysli co godzine
    ///      i moze skonczyc rabunek, FinalizeEvent), wiec lord moze przerwac rabunek i uciec jak dotad.
    ///      BEZ POSTOJU (jak w NocnyMarsz - "AI walczy o zycie", zagrozenie przed snem): lord ucieka (IsFleeing) albo ALARM NocnyMarsz (AlarmThreat, ratio 1: silniejszy,
    ///      czuwajacy wrog idzie na niego w promieniu AiCampDangerRadius - ta sama ocena sily co NocnyMarsz) - rabuje dalej / AI decyduje, godziny to praca (bez snu);
    ///      walka (DefenderSide.TroopCount > 0) - RaidHaltHolds i tak falszywe; Nieumarli; partia bez ksiegi snu AI (nie lord - bandy, Prologue.World.Raiders - maja
    ///      wlasne pory AiBanditRest, ksiegi snu nie znaja); gracz w tej armii z wylaczonym RaidNightHalt (klucz gracza: jego kolumna rabuje cala noc).
    ///      KSIEGA SNU AI (NightMarch.AiSleepLedger): godzina partii lorda w rabunku po stronie napastnika - postoj trzyma (RaidHaltHolds) = sen jak dotad (stoi przy wsi);
    ///      rabunek idzie = praca (bez odpoczynku tej godziny), gdy RaidNightHaltAi i RaidCountsAsWork (ta sama regula co gracz) i postoj AI czynny (AiHaltLive).
    ///      Dotyczy tez lordow w armii gracza (rabunek gracza - postoj 577 = sen, dzien = praca).
    ///  (3) GRACZ W CUDZEJ ARMII: rabunek wodza AI stoi na noc - gracz dostaje sen (PlayerRaidHeld w OnHourly, PlayerRaidingNow falszywe), komunikaty FollowFrame.
    ///  (2) WIOSKI POBOCZNE (SideVillageVisit.RaidTick - menu oczekiwania bez MapEvent): SideRaidNight - w nocy obozu postep (HoursDone, uderzenia) stoi, godziny to sen
    ///      (PlayerRaidHeld), komunikaty jak 577; o swicie dalej. Klucz RaidNightHalt (gracz).
    ///  (4) Szacunek czasu (okienko E7/R2 "about N days", podpowiedz rabunku wioski pobocznej): RaidWallDays - godziny pracy rozlozone na zegar z pominieciem nocy obozu.
    /// Tylko pamiec: wczytanie - wpisy od nowa w najblizszym ticku godzinowym (do godziny postepu), gracz - jak 577. Wyjatek w ocenie partii liczony (AiStumble), bez wylacznika.
    /// </summary>
    internal static partial class NightRest
    {
        private sealed class AiHalt
        {
            public bool On;           // postoj nocny czynny (ocena godziny); RaidHaltHolds dodatkowo: zegar w nocy obozu i faza lupienia
            public double From = -1;  // poczatek postoju (godzina kampanii) - do linii
            public string Name, Lead;
            public int Parties;
        }

        private static readonly Dictionary<RaidEventComponent, AiHalt> _aiHalt = new Dictionary<RaidEventComponent, AiHalt>();
        private static readonly HashSet<RaidEventComponent> _aiHaltSeen = new HashSet<RaidEventComponent>();
        private static readonly List<RaidEventComponent> _aiHaltDrop = new List<RaidEventComponent>();
        private static bool _aiHaltLive;   // ostatnia ocena godzinowa z czynnym kluczem (ksiega AI: dzien rabunku = praca tylko przy czynnym postoju)
        // liczniki doby (linia switu NocnyMarsz): postoje, przerwane przez zagrozenie, wznowione o swicie, skonczone w postoju, maks. rabunkow/partii w postoju naraz,
        // partio-godziny ksiegi AI: sen z postoju, rabunek jako praca
        private static int _rhStarts, _rhThreat, _rhResumes, _rhEnds, _rhMaxRaids, _rhMaxParties, _rhSleepH, _rhWorkH, _rhExN;
        private static float _rhSleepRest;
        // gracz w cudzej armii (3)
        private static RaidEventComponent _follow;
        private static string _followName;
        private static double _followFrom = -1;
        // wioska poboczna (2)
        private static bool _sideHalt, _sideOff;
        private static string _sideName;
        private static double _sideFrom = -1;
        private static int _sideStumbles;

        private static void ResetRaidHaltAi()
        {
            _aiHalt.Clear(); _aiHaltSeen.Clear(); _aiHaltDrop.Clear(); _aiHaltLive = false;
            _rhStarts = _rhThreat = _rhResumes = _rhEnds = _rhMaxRaids = _rhMaxParties = _rhSleepH = _rhWorkH = _rhExN = 0; _rhSleepRest = 0f;
            _follow = null; _followName = null; _followFrom = -1;
            _sideHalt = false; _sideOff = false; _sideName = null; _sideFrom = -1; _sideStumbles = 0;
        }

        /// <summary>578: postoj AI czynny w ustawieniach (klucz, ksiega snu, oboz swiata, latka kroku rabunku).</summary>
        private static bool AiHaltOn(Settings s)
        {
            return s != null && s.RaidNightHaltAi && s.NightRestEnabled && LedgerOn(s) && DistrictRaid.UpdWired;
        }

        /// <summary>578: RaidHaltHolds - czesc AI (wolane tylko przy niepustym slowniku). Zegar w nocy obozu i faza lupienia.</summary>
        private static bool AiHaltHolds(RaidEventComponent comp)
        {
            AiHalt h;
            if (!_aiHalt.TryGetValue(comp, out h) || !h.On) return false;
            if (!BedNight(CampaignTime.Now.GetHourOfDay)) return false;
            var def = comp.DefenderSide;
            return def != null && def.TroopCount == 0;
        }

        /// <summary>578: postoj AI tego rabunku czynny teraz (bez warunku walki) - komunikaty gracza w cudzej armii.</summary>
        private static bool AiHaltNight(RaidEventComponent comp)
        {
            AiHalt h;
            return comp != null && _aiHalt.Count > 0 && _aiHalt.TryGetValue(comp, out h) && h.On && BedNight(CampaignTime.Now.GetHourOfDay);
        }

        /// <summary>578: ksiega snu AI - partia lorda w rabunku po stronie napastnika: 0 - nie (albo regula nieczynna), 1 - postoj trzyma (sen), 2 - rabunek idzie (praca).</summary>
        private static int AiRaidKind(MobileParty mp, Settings s)
        {
            var me = mp.MapEvent;
            if (me == null || !me.IsRaid) return 0;
            var side = mp.MapEventSide;
            if (side == null || side != me.AttackerSide) return 0;
            if (RaidHaltHolds(me.Component as RaidEventComponent)) return 1;
            if (!_aiHaltLive || !s.RaidCountsAsWork) return 0;
            return 2;
        }

        /// <summary>578: licznik ksiegi AI (AiSleepLedger) - partio-godzina snu z postoju (rest - odpoczynek dopisany) albo rabunku jako pracy.</summary>
        private static void AiRaidCount(int kind, float rest)
        {
            if (kind == 1) { _rhSleepH++; _rhSleepRest += rest; }
            else if (kind == 2) _rhWorkH++;
        }

        /// <summary>578: gracz jest w rabunku wsi gry po stronie napastnika, a rabunek stoi na noc (577 albo wodz AI), albo jego wioska poboczna stoi na noc.</summary>
        internal static bool PlayerRaidHeld()
        {
            if (_sideHalt && SideVillageVisit.RaidActive) return true;
            var mp = MobileParty.MainParty;
            var me = mp != null ? mp.MapEvent : null;
            if (me == null || !me.IsRaid || mp.MapEventSide == null || mp.MapEventSide != me.AttackerSide) return false;
            return RaidHaltHolds(me.Component as RaidEventComponent);
        }

        /// <summary>578: druzyna gracza w rabunku wsi gry po stronie napastnika, ktorego NIE prowadzi (cudza armia); null - brak.</summary>
        private static RaidEventComponent PlayerFollowedRaid()
        {
            var mp = MobileParty.MainParty;
            var me = mp != null ? mp.MapEvent : null;
            if (me == null || !me.IsRaid || mp.MapEventSide == null || mp.MapEventSide != me.AttackerSide) return null;
            var att = me.AttackerSide;
            if (att.LeaderParty == null || att.LeaderParty == mp.Party) return null;
            return me.Component as RaidEventComponent;
        }

        // ------------------------------------------------------------ (1) ocena godzinowa rabunkow AI (AiHourly, przed ksiega AI)
        private static void AiRaidHaltHour(Settings s)
        {
            if (!AiHaltOn(s))
            {
                _aiHaltLive = false;
                if (_aiHalt.Count > 0)
                {
                    int n = 0; foreach (var kv in _aiHalt) if (kv.Value.On) n++;
                    _aiHalt.Clear();
                    Log.Info("Rabunek AI - postoj nocny (578): wylaczony (klucz Raid Night Halt AI, ksiega snu, oboz swiata albo latka kroku rabunku) - " + n + " rabunkow rusza od reki.");
                }
                return;
            }
            _aiHaltLive = true;
            var c = Campaign.Current;
            var mgr = c != null ? c.MapEventManager : null;
            if (mgr == null) return;
            int hc = CampaignTime.Now.GetHourOfDay;
            bool window = BedNight(hc) || BedNight(hc + 1);   // godzine przed zmierzchem wpis juz stoi - postoj zaczyna sie z zegarem (RaidHaltHolds)
            if (!window && _aiHalt.Count == 0) return;
            double now = CampaignTime.Now.ToHours;
            var main = MobileParty.MainParty;
            int raids = 0, held = 0, parties = 0, threat = 0, flee = 0, undead = 0, noLedger = 0, playerKey = 0, fight = 0, starts = 0, cuts = 0;
            _aiHaltSeen.Clear();
            if (window)
            {
                var list = mgr.MapEvents;
                for (int i = 0; list != null && i < list.Count; i++)
                {
                    var me = list[i];
                    if (me == null || !me.IsRaid) continue;
                    var comp = me.Component as RaidEventComponent;
                    var att = me.AttackerSide;
                    var lp = att != null ? att.LeaderParty : null;
                    var lead = lp != null ? lp.MobileParty : null;
                    if (comp == null || lead == null || lead == main) continue;   // rabunek gracza - 577 (RaidHaltFrame)
                    raids++;
                    _aiHaltSeen.Add(comp);
                    AiHalt h;
                    bool had = _aiHalt.TryGetValue(comp, out h);
                    bool ok = false, isThreat = false; string how = null;
                    try
                    {
                        if (!lead.IsActive || !lead.IsLordParty) noLedger++;            // bez ksiegi snu AI (bandy - wlasne pory AiBanditRest)
                        else if (Undead.Party(lead)) undead++;                           // umarli nie spia
                        else if (main != null && main.MapEvent == me && main.MapEventSide == att && !s.RaidNightHalt) playerKey++;   // gracz w tej armii bez Raid Night Halt
                        else if (lead.IsFleeing()) { flee++; isThreat = true; how = "ucieka"; }
                        else
                        {
                            float ts, ms;
                            var th = AlarmThreat(lead, s, null, 1f, out ts, out ms, out how);
                            if (th != null) { threat++; isThreat = true; }
                            else ok = true;
                        }
                    }
                    catch (Exception ex) { AiStumble("AiRaidHaltHour", lead, ex); ok = false; }
                    if (!had && !ok) continue;
                    if (!had)
                    {
                        string vn = "?"; try { var st = comp.MapEventSettlement; if (st != null) vn = st.Name.ToString(); } catch { }
                        string ln = "?"; try { ln = lead.LeaderHero != null ? lead.LeaderHero.Name.ToString() : lead.Name.ToString(); } catch { }
                        h = new AiHalt { Name = vn, Lead = ln };
                        _aiHalt[comp] = h;
                    }
                    if (ok)
                    {
                        if (!h.On) { h.On = true; if (h.From < 0) h.From = now; starts++; _rhStarts++; }
                        int np = 1; try { np = att.Parties != null ? att.Parties.Count : 1; } catch { }
                        h.Parties = np;
                        held++; parties += np;
                        try { var def = comp.DefenderSide; if (def != null && def.TroopCount > 0) fight++; } catch { }
                    }
                    else if (h.On)
                    {
                        h.On = false;
                        if (isThreat)
                        {
                            cuts++; _rhThreat++;
                            if (_rhExN++ < 3)
                                Log.Info("Rabunek AI - postoj nocny (578): " + h.Lead + " przy " + h.Name + " - zagrozenie: " + (how ?? "?") + " - postoj zdjety, AI decyduje jak dotad"
                                         + " (ucieczka albo rabunek dalej, godziny jako praca).");
                        }
                    }
                }
            }
            // rabunki skonczone (nie ma ich w liscie) albo koniec okna nocy - wpis znika
            _aiHaltDrop.Clear();
            foreach (var kv in _aiHalt) if (!window || !_aiHaltSeen.Contains(kv.Key)) _aiHaltDrop.Add(kv.Key);
            int resumes = 0, ends = 0;
            for (int i = 0; i < _aiHaltDrop.Count; i++)
            {
                AiHalt h;
                if (_aiHalt.TryGetValue(_aiHaltDrop[i], out h) && h.On) { if (window) { ends++; _rhEnds++; } else { resumes++; _rhResumes++; } }
                _aiHalt.Remove(_aiHaltDrop[i]);
            }
            _aiHaltDrop.Clear(); _aiHaltSeen.Clear();
            if (held > _rhMaxRaids) _rhMaxRaids = held;
            if (parties > _rhMaxParties) _rhMaxParties = parties;
            if (raids > 0 || resumes > 0 || ends > 0)
                Log.Info("Rabunek AI - postoj nocny (578): " + Hm(CampaignTime.Now.CurrentHourInDay) + (window ? (BedNight(hc) ? " (noc obozu)" : " (godzina przed zmierzchem - postoj od " + BedNightStart + ":00)") : " (swit)")
                         + " | rabunkow AI " + raids + ": w postoju " + held + " (partii " + parties + (fight > 0 ? ", w walce teraz " + fight : "") + "), nowe " + starts
                         + " | bez postoju: zagrozenie " + threat + ", ucieczka " + flee + " (zdjete teraz " + cuts + "), Nieumarli " + undead + ", bez ksiegi snu " + noLedger
                         + ", gracz w armii bez Raid Night Halt " + playerKey + " | wznowione o swicie " + resumes + ", skonczone w postoju " + ends + ".");
        }

        /// <summary>578: czesc linii switu NocnyMarsz - postoj nocny AI z doby; liczniki od zera.</summary>
        private static string RaidHaltAiDawnText()
        {
            string t = " | postoj nocny przy rabunku AI (578, Raid Night Halt AI " + (AiHaltOn(Settings.Current) ? "[WL]" : "[wyl]") + "): postojow " + _rhStarts
                       + ", maks. naraz rabunkow " + _rhMaxRaids + " (partii " + _rhMaxParties + "), przerwane przez zagrozenie " + _rhThreat + ", wznowione o swicie " + _rhResumes
                       + ", skonczone w postoju " + _rhEnds + "; ksiega AI: partio-godzin snu z postoju " + _rhSleepH + " (odpoczynek " + F1(_rhSleepRest) + " h, sr. "
                       + (_rhSleepH > 0 ? F1(_rhSleepRest / _rhSleepH) : "-") + "), partio-godzin rabunku jako praca " + _rhWorkH;
            _rhStarts = _rhThreat = _rhResumes = _rhEnds = _rhMaxRaids = _rhMaxParties = _rhSleepH = _rhWorkH = _rhExN = 0; _rhSleepRest = 0f;
            return t;
        }

        // ------------------------------------------------------------ klatka (OnTickCore, po RaidHaltFrame): wylacznik AI, gracz w cudzej armii, koniec wioski pobocznej
        private static void RaidHaltAiFrame(Settings s)
        {
            if (_aiHalt.Count == 0 && _follow == null && !_sideHalt) return;
            try
            {
                if (Campaign.Current == null) { ResetRaidHaltAi(); return; }
                // klucz albo ksiega wylaczone w trakcie gry - postoj AI zdjety od reki (OnHourly konczy sie przed AiHourly przy wylaczonej ksiedze)
                if (_aiHalt.Count > 0 && !AiHaltOn(s)) { _aiHalt.Clear(); _aiHaltLive = false; Log.Info("Rabunek AI - postoj nocny (578): klucz albo ksiega snu wylaczone - postoje AI zdjete od reki."); }
                // (2) wioska poboczna skonczona w postoju (Stop raiding, przerwanie, koniec) - bez wznowienia
                if (_sideHalt && !SideVillageVisit.RaidActive) EndSide("rabunek wioski pobocznej skonczony (Stop raiding albo przerwany) - bez wznowienia");
                // (3) gracz w cudzej armii
                var cur = PlayerFollowedRaid();
                if (_follow != null && !ReferenceEquals(cur, _follow))
                {
                    _haltEndsDay++;
                    Log.Info("Nocleg: gracz - postoj nocny armii przy rabunku (578, cudza armia) zakonczony: " + (_followName ?? "?") + " - rabunek skonczony albo gracz poza nim; postoj "
                             + F1((float)(CampaignTime.Now.ToHours - _followFrom)) + " h, " + HaltNightCounts() + ".");
                    _follow = null; _followName = null; _followFrom = -1;
                }
                if (cur == null) return;
                bool held = s.RaidNightHalt && AiHaltNight(cur);
                if (_follow == null)
                {
                    if (!held) return;
                    _follow = cur; _followFrom = CampaignTime.Now.ToHours; _haltStartsDay++;
                    string vn = "?"; try { var st = cur.MapEventSettlement; if (st != null) vn = st.Name.ToString(); } catch { }
                    _followName = vn;
                    Msg("Night falls - the army stops raiding " + vn + " and sleeps by the village. The raid goes on at dawn.", Colors.White);
                    Log.Info("Nocleg: gracz - postoj nocny przy rabunku w cudzej armii (578): " + vn + " o " + Hm(CampaignTime.Now.CurrentHourInDay) + ", pasek " + HaltPct(cur)
                             + " - wodz AI stoi do switu " + PlayerDawn + ":00, godziny gracza licza sie jak sen (nie Raid Counts As Work); odpoczynek doby " + F1(_restTonight) + " h, dlug " + Debt + ".");
                }
                else if (!held)
                {
                    bool night = BedNight(CampaignTime.Now.GetHourOfDay);
                    string name = _followName; double from = _followFrom;
                    _follow = null; _followName = null; _followFrom = -1;
                    if (night)
                    {
                        _haltEndsDay++;
                        Msg(s.RaidNightHalt ? "Alarm in the night - the army is roused at " + name + ": enemies are near." : "The army takes up the raid on " + name + " again.", Colors.White);
                        Log.Info("Nocleg: gracz - postoj nocny armii przy rabunku (578, cudza armia) przerwany w nocy: " + name + " (wodz: zagrozenie albo ucieczka, albo klucz Raid Night Halt wylaczony); postoj "
                                 + F1((float)(CampaignTime.Now.ToHours - from)) + " h, " + HaltNightCounts() + ".");
                    }
                    else
                    {
                        _haltResumesDay++;
                        Msg("Dawn - the army takes up the raid on " + name + " again.", Colors.White);
                        Log.Info("Nocleg: gracz - wznowienie rabunku o swicie w cudzej armii (578): " + name + " o " + Hm(CampaignTime.Now.CurrentHourInDay) + ", postoj "
                                 + F1((float)(CampaignTime.Now.ToHours - from)) + " h, pasek " + HaltPct(cur) + "; " + HaltNightCounts() + "; odpoczynek doby " + F1(_restTonight) + " h, dlug " + Debt + ".");
                    }
                }
            }
            catch (Exception e)
            {
                if (_haltErrLogged++ < 3) Log.Error("NightRest.RaidHaltAiFrame", e);   // klatka nastepna od nowa (bez stanu do zdjecia - postoj AI decyduje tick godzinowy)
            }
        }

        // ------------------------------------------------------------ (2) wioska poboczna (SideVillageVisit.RaidTick, co klatke menu oczekiwania)
        /// <summary>578: rabunek wioski pobocznej w nocy obozu - true = postep stoi (wolajacy nie dolicza godzin). Komunikaty jak 577; o swicie dalej sam.</summary>
        internal static bool SideRaidNight(string name, double frac)
        {
            try
            {
                var s = Settings.Current;
                bool night = BedNight(CampaignTime.Now.GetHourOfDay);
                if (!night) _sideOff = false;
                bool want = s != null && s.RaidNightHalt && s.NightRestEnabled && night && !_sideOff;
                if (want && !_sideHalt && (ForcedMarch.Active || Undead.Party(MobileParty.MainParty))) want = false;
                if (want == _sideHalt) return _sideHalt;
                string pct = (Math.Max(0.0, Math.Min(1.0, frac)) * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";
                if (want)
                {
                    _sideHalt = true; _sideName = name ?? "?"; _sideFrom = CampaignTime.Now.ToHours; _haltStartsDay++;
                    Msg(PartyWords.Pick("Night falls - the men stop raiding " + _sideName + " and sleep by the village. The raid goes on at dawn.",
                                        "Night falls - you stop raiding " + _sideName + " and sleep by the village. The raid goes on at dawn.",
                                        "Night falls - you and your companions stop raiding " + _sideName + " and sleep by the village. The raid goes on at dawn."), Colors.White);
                    Log.Info("Nocleg: gracz - postoj nocny przy rabunku wioski pobocznej (578, Raid Night Halt): " + _sideName + " o " + Hm(CampaignTime.Now.CurrentHourInDay) + ", pasek " + pct
                             + " - postep stoi do switu " + PlayerDawn + ":00, godziny licza sie jak sen w obozie (nie Raid Counts As Work); odpoczynek doby " + F1(_restTonight) + " h, dlug " + Debt + ".");
                    return true;
                }
                // swit albo klucz zdjety
                string nm = _sideName; double from = _sideFrom;
                _sideHalt = false; _sideName = null; _sideFrom = -1;
                if (!night)
                {
                    _haltResumesDay++;
                    Msg(PartyWords.Pick("Dawn - the men take up the raid on " + nm + " again.", "Dawn - you take up the raid on " + nm + " again.",
                                        "Dawn - you and your companions take up the raid on " + nm + " again."), Colors.White);
                    Log.Info("Nocleg: gracz - wznowienie rabunku wioski pobocznej o swicie (578): " + nm + " o " + Hm(CampaignTime.Now.CurrentHourInDay) + ", postoj " + F1((float)(CampaignTime.Now.ToHours - from))
                             + " h, pasek " + pct + "; " + HaltNightCounts() + "; odpoczynek doby " + F1(_restTonight) + " h, dlug " + Debt + ".");
                }
                else
                {
                    _haltEndsDay++;
                    Log.Info("Nocleg: gracz - postoj nocny przy rabunku wioski pobocznej (578) zdjety w nocy: " + nm + " - klucz Raid Night Halt albo ksiega snu wylaczone; postoj "
                             + F1((float)(CampaignTime.Now.ToHours - from)) + " h, " + HaltNightCounts() + ".");
                }
                return false;
            }
            catch (Exception e)
            {
                // potkniecia liczone; po MaxHaltStumbles postoj zdjety do switu (rabunek idzie), funkcja dalej czynna
                if (_haltErrLogged++ < 3) Log.Error("NightRest.SideRaidNight", e);
                if (++_sideStumbles >= MaxHaltStumbles)
                {
                    _sideStumbles = 0;
                    if (!_sideOff) { _sideOff = true; Log.Info("Nocleg: gracz - postoj nocny przy rabunku wioski pobocznej zdjety do switu po " + MaxHaltStumbles + " potknieciach (rabunek idzie dalej)."); }
                    _sideHalt = false;
                }
                return false;
            }
        }

        private static void EndSide(string why)
        {
            string nm = _sideName; double from = _sideFrom;
            _sideHalt = false; _sideName = null; _sideFrom = -1; _haltEndsDay++;
            Log.Info("Nocleg: gracz - postoj nocny przy rabunku wioski pobocznej (578) zakonczony: " + (nm ?? "?") + " - " + why + "; postoj " + F1((float)(CampaignTime.Now.ToHours - from)) + " h, " + HaltNightCounts() + ".");
        }

        // ------------------------------------------------------------ (4) szacunek czasu rabunku z nocnym postojem
        /// <summary>
        /// 578 (4): doby zegara rabunku gracza, ktory wymaga workDays dob czystej pracy (po 24 h), przy nocnym postoju (RaidNightHalt): godziny pracy rozlozone na zegar od teraz,
        /// noc obozu (BedNight) bez postepu. Srednio D x 24 / (24 - noc) - przy nocy 21-3 (6 h) D x 24/18 = D + D/3: rabunek trwa o 1/3 dluzej niz sama praca, bo 1/4 doby to noc.
        /// Postoj wylaczony (klucz, ksiega snu, marsz forsowny) - bez zmian.
        /// </summary>
        internal static double RaidWallDays(double workDays)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.RaidNightHalt || !s.NightRestEnabled || ForcedMarch.Active || Undead.Party(MobileParty.MainParty) || !(workDays > 0.0) || double.IsInfinity(workDays)) return workDays;
                int nightH = 0;
                for (int h = 0; h < 24; h++) if (BedNight(h)) nightH++;
                if (nightH <= 0 || nightH >= 24) return workDays;
                double need = workDays * 24.0, wall = 0.0, t = CampaignTime.Now.ToHours;
                int guard = 0;
                while (need > 1e-9 && guard++ < 24 * 400)
                {
                    double hod = t % 24.0; if (hod < 0) hod += 24.0;
                    int hi = (int)Math.Floor(hod);
                    double step = Math.Max(1e-6, (hi + 1) - hod);
                    if (!BedNight(hi)) { double use = Math.Min(step, need); need -= use; wall += use; t += use; }
                    else { wall += step; t += step; }
                }
                if (need > 1e-9) wall += need * 24.0 / (24 - nightH);   // ponad 400 dob - reszta srednia
                return wall / 24.0;
            }
            catch { return workDays; }
        }
    }
}
