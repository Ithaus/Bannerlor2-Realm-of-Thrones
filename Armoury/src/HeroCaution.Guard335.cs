using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 335 OCHRONA WODZA - BITWA (Jeff 06.11: "tywin jak idiota jezdzi sobie sam wokol wojsk, dopadlem go i zabilem; nie mial kawalerii ... jak nie mial kawalerii to
    /// nie jezdzil by sam, tylko trzyma sie swoich ludzi wlasnie po to, aby nie stracic dowodcy; dowodcy maja byc chronieni i jak nie maja obstawy kawalerii to dowodza
    /// z tylu blisko swoich wojsk, a jak go ktos atakuje to wycofuje sie do swoich wojsk, a jego wojska biegna go chronic"). Klucz CommandersKeepCommandPost (dziala
    /// z HeroesKeepBehindTheirMen - ta sama misja, ten sam watek, te same pozycje skryptowe 149; NIE = 149 jak dotad, tylko pomiar).
    /// Przyczyna (log Jeffa Armoury-2026-11-06_11-51-21, bitwa pod Sarswood 12:39; dekompilacja dek335 i RBM dek333): RBM wylacza szyk wodza
    /// (CanTeamHaveGeneralsFormationPatch) - nie ma BehaviorGeneral ani obstawy; RBM ManageFormationCounts wpycha kazdego konnego bez broni dystansowej (takze wodza) do
    /// szyku 2 (Cavalry) - Tywin byl w 1-3 osobowym szyku jazdy, ktory dostawal rozkazy calej jazdy (skrzydlo, szarza, FrontalCavalryCharge) i krazyl z dala od piechoty;
    /// 149 nie zna wodza i reaguje dopiero na 2 wrogow w 10 m (Jeff byl jeden) - "samotni 0"; ranny ponizej 45% jechal sam przez pole do glownej sily i padl w drodze.
    ///
    /// Kto (rola, co pass 0.5 s): DOWODCA = wodz druzyny (Team.GeneralAgent) albo kapitan szyku (Formation.Captain) - bohater AI; GLOWA RODU = glowa rodu, wladca albo
    /// nastepca (Ransom178.IsRulerOrHeir) bez dowodzenia; reszta bohaterow - 149 bez zmian. Gracz nigdy; szyk pod rozkazami gracza (HorsePursuit.PlayerForm - takze cala
    /// druzyna gracza-wodza: "gracz-wodz nie") - rola 0.
    /// Macierz (zagrozenie x eskorta x stan bitwy x rola):
    ///  - ZAGROZENIE (dowodca i glowa rodu): wrog (nie uciekajacy, nie w PANICE) w 10 m (FoeRadius 149) albo jezdziec wroga w 20 m (ThreatRider = 2 x 10 m - konny
    ///    pokonuje 10 m w ok. 1 s: jeden pass i reakcja), a swoich w 5 m (AllyRadius 149) mniej niz tych wrogow - bez przerwy 2 passow (149 czeka 1 s na
    ///    "przejazd obok"; dowodca nie czeka). Wycofanie (droga 149: pozycja skryptowa bez NoAttack - broni sie, bez paniki i okrzyku): cel = NAJBLIZSZY wlasny szyk
    ///    (>= 3 ludzi, nie w odwrocie): srodek, a gdy przy srodku wrog - tyl szyku; dopiero bez niego tyl wlasnego szyku / glowna sila / sztandar jak 149. Na miejscu
    ///    postoj AloneHold (6 s, przedluzany, dopoki wrog jest przy nim), potem powrot (dowodca - na stanowisko). Przerwa 1 s (ThreatCool), nie 10 s jak samotny.
    ///    STRAZ: w chwili zagrozenia ludzie jego druzyny w 80 m (BannerRange 149) - szeregowi AI, nie chorazowie, nie w odwrocie / PANICE / odwrocie rannego 143,
    ///    nie jezdzcy 148/204, nie w szyku gracza, bez cudzej pozycji skryptowej - najblizsi (jezdziec liczony z polowa odleglosci) biegna do niego: tylu, ile gra
    ///    daje obstawy wodza (GeneralsAndCaptainsAssignmentLogic: kandydaci / 10, najwyzej 20), najmniej 3; pozycja skryptowa = miejsce dowodcy, co 0.25 s przy
    ///    przesunieciu o 2 m; koniec, gdy dowodca wyjdzie z zagrozenia (albo ranny dotrze), po TravelMax 25 s, panika / odwrot czlowieka, koniec bitwy.
    ///  - BEZ ZAGROZENIA, DOWODCA:
    ///    a) Z ESKORTA KONNA (konny, w jego szyku co najmniej 10 jezdzcow - szeregowych AI; gra: obstawa = kandydaci / 10, czyli 10 = pierwszy straznik): trzyma sie
    ///       eskorty - dalej niz max(szerokosc, glebokosc)/2 + 10 m od srodka szyku = pozycja skryptowa za szykiem (149 Rear konno), blizej - bez pozycji (jedzie z nimi);
    ///    b) W PIESZYM SZYKU >= 8 ludzi (MainMin 149) i w jego obrysie (max(szer., gleb.)/2 + 5 m) - bez pozycji (149 stawia go w ostatnich szeregach);
    ///    c) INACZEJ (konny bez eskorty - jak Tywin - albo maly szyk): STANOWISKO za glownym szykiem wzorem BehaviorGeneral gry: mediana glownego szyku (AI.IsMainFormation,
    ///       bez niego najwiekszy pieszy szyk >= 8 - MainOf 149) - kierunek do mediany celu druzyny (TeamQuerySystem.MedianTargetFormationPosition) x (glebokosc + 3 m);
    ///       co 1.5 s (RefreshAlone 149), nowa pozycja przy przesunieciu o 5 m; cel sprawdzany jak 149 (pole, 10 m od granicy, bez wroga w 10 m, droga). Bez glownego
    ///       szyku (zostal sam) - bez stanowiska: walczy (BehaviorGeneral: szarza, gdy innych szykow brak). 40 m gry (konny przy >= 33% jazdy) dotyczy wodza z obstawa -
    ///       bez eskorty Jeff kaze "blisko swoich" (3 m).
    ///  - RANNY (149, ponizej 45%): cel najpierw najblizszy wlasny szyk (dowodca i glowa rodu), straz biegnie dalej, dopoki jest w drodze.
    ///  - PANIKA, odwrot rannego 143, odwrot na rozkaz, gracz przejal, klucz NIE w trakcie - jak 149 (pozycja zdjeta).
    /// Recenzja 335 (335b) - zmiany wzgledem opisu wyzej:
    ///  - ZAGROZENIE: swoich liczymy w 10 m (nie 5 m) - dowodca 3 m za linia w zwarciu nie jest zagrozony; jezdziec 10-20 m tylko, gdy jedzie na dowodce (cel = on
    ///    albo > 2 m/s w 45 st. od kierunku na niego); warunek samotnego 149 (0 swoich w 5 m i >= 2 wrogow w 10 m) zostaje;
    ///  - STRAZ: 3 ludzi na napastnika (3..20), nie kandydaci / 10; nie z walki wrecz (cel w 3 m) ani z szyku w szarzy (Charge / ChargeToTarget); straznik wziety przez
    ///    148/204/333 (lowca, jezdziec, test) - zwolniony bez ruszania jego pozycji;
    ///  - STANOWISKO c): wlasny szyk glowny albo pieszy >= 8 - za WLASNYM szykiem (kapitan, ktory wyszedl z obrysu, wraca do swoich);
    ///  - CEL WYCOFANIA: najblizszy szyk pieszy nie w szarzy (konny tylko bez pieszego); ranny - tyl szyku, nie srodek.
    /// Pomiar (zawsze, takze klucz NIE): samotnosc dowodcy = pass bez swojego czlowieka w 30 m (LoneGap - konny 3 s), sekundy obecnosci; na sucho zagrozenia.
    /// Linia "Ochrona wodza (335): bitwa ..." raz na bitwe po "bohaterowie (149)". Test autotestu (klucz CommanderHuntInAutotest, tylko LogGate.Mode 1, plan "slaba"):
    /// LOWCA - jezdziec (albo pieszy) druzyny gracza 8 s po rozstawieniu jedzie prosto na wodza wroga (jak Jeff na Tywina) - linia "Ochrona wodza (335) TEST".
    /// </summary>
    internal static partial class HeroCaution
    {
        internal const float ThreatRider = 20f;     // jezdziec wroga w 20 m (2 x FoeRadius)
        internal const float ThreatCool = 1f;       // nastepne zagrozenie dowodcy po 1 s
        internal const float CommandBack = 3f;      // BehaviorGeneral: glebokosc glownego szyku + 3 m
        internal const int EscortMin = 10;          // GeneralsAndCaptainsAssignmentLogic: obstawa = kandydaci / 10 - pierwszy straznik od 10 jezdzcow
        internal const int GuardsMax = 20;          // ... najwyzej 20
        internal const int GuardsMin = 3;           // najmniej 3 (kilku ludzi na jednego napastnika)
        internal const int GuardsPerFoe = 3;        // recenzja 335: straz wedlug napastnikow - 3 na wroga (3..20), nie kandydaci / 10
        internal const float MeleeBusy = 3f;        // w walce wrecz: cel czlowieka w 3 m (HuntContact) - nie schodzi z linii na straz
        internal const float ComingSpeed = 2f;      // jezdziec 10-20 m liczy sie, gdy jedzie na dowodce (> 2 m/s w 45 st. od kierunku na niego) albo ma go za cel
        internal const float GuardReach = BannerRange;
        internal const float GuardRepath = 2f;      // straz: nowa pozycja przy przesunieciu dowodcy o 2 m (jak TestRepath 283a)
        internal const float GuardReached = 5f;     // straz dotarla: w 5 m od dowodcy (AllyRadius)
        internal const float LoneGap = 30f;         // samotny dowodca: zaden swoj w 30 m
        internal const float HuntPickAfter = 8f, HuntPickUntil = 30f, HuntMax = 150f, HuntContact = 3f;
        private const int ModePost = 3, ModeThreat = 4;
        private const int RoleNone = 0, RoleBig = 1, RoleCmd = 2;
        private const int PKPost = 1, PKEscort = 2;

        private sealed class Guard { public Agent A; public Man Of; public float Since; public Vec2 Set = Vec2.Invalid; public bool Reached; }

        private static bool _gKey;
        private static bool _gCharge340;   // 340 (e): klucz CommanderGuardCharging340 - szyki w szarzy jako ostatnie wyjscie (cel wycofania i straz)
        private static int _gAttacked340, _gChargeDest340, _gChargeGuard340;
        private static readonly List<Guard> Guards = new List<Guard>();
        private static readonly HashSet<Agent> GuardSet = new HashSet<Agent>();
        private static readonly List<KeyValuePair<float, Agent>> _gCand = new List<KeyValuePair<float, Agent>>();
        private static readonly List<Formation> _gForms = new List<Formation>();
        private static readonly MBList<Agent> _gNear = new MBList<Agent>();

        // liczniki misji
        private static int _gCmd, _gGen, _gCap, _gBig, _gMounted, _gEscortForms;
        private static readonly HashSet<int> GCmdPeople = new HashSet<int>(), GBigPeople = new HashSet<int>(), GEscPeople = new HashSet<int>(), GCovPeople = new HashSet<int>(),
                                              GPostPeople = new HashSet<int>(), GThreatPeople = new HashSet<int>(), GMountedPeople = new HashSet<int>();
        private static readonly int[] GPostSends = new int[3];
        private static int _gPostMoves, _gPostRestored, _gPostNoSpot, _gPostNoMain, _gPostLimit;
        private static int _gThreats, _gThreatsCmd, _gThreatsBig, _gThreatFoes, _gThreatRiders, _gFromPost, _gDry;
        private static int _gSends, _gArrived, _gBack, _gTimeout, _gGaveUp;
        private static float _gTravelSum;
        private static readonly int[] GKindSend = new int[KKinds];
        private static readonly int[] GEnd = new int[EKinds];
        private static int _gGuardEps, _gGuardMen, _gGuardReached, _gGuardNone;
        private static readonly int[] GGuardEnd = new int[6];   // 0 koniec zagrozenia, 1 czas, 2 panika / odwrot, 3 czlowiek padl / dowodca padl, 4 koniec bitwy, 5 przejety (148/204/333)
        private static float _gLoneSum, _gPresentSum, _gLoneMax;
        private static string _gLoneWho = "-";
        private static int _gCmdKilled, _gCmdKo, _gFateThreat, _gFatePost, _gFateOther, _gStum;

        // test lowcy (autotest)
        private static bool _htOn;
        private static string _htWhy = "-", _htEnd = "-", _htStartMode = "-";
        private static int _htState, _htGuards, _htGuardsReached, _htTEsc, _htFate;   // _htFate: 0 zyje, 1 zabity, 2 nieprzytomny, 3 uszedl
        private static Agent _htA, _htT, _htRun;
        private static Man _htTM;
        private static float _htDeploy = -1f, _htStart, _htNear = -1f, _htThr = -1f, _htContact = -1f, _htNext, _htLast, _htGapAtContact = -1f, _htMor0 = -1f, _htLone0;
        private static bool _htTMounted, _htTGen;
        private static int _htSeq;

        private static void GStumble(string where, Exception e)
        {
            _gStum++;
            Stumble(where, e);
        }

        // ------------------------------------------------------------ zycie misji

        private static void GuardBegin(Settings c)
        {
            _gKey = _key && c != null && c.CommandersKeepCommandPost;
            _gCharge340 = _gKey && c.CommanderGuardCharging340;
            HuntTestBegin(c);
        }

        private static void GuardClear()
        {
            Guards.Clear(); GuardSet.Clear(); _gCand.Clear(); _gForms.Clear(); _gNear.Clear();
            _gCmd = _gGen = _gCap = _gBig = _gMounted = _gEscortForms = 0;
            GCmdPeople.Clear(); GBigPeople.Clear(); GEscPeople.Clear(); GCovPeople.Clear(); GPostPeople.Clear(); GThreatPeople.Clear(); GMountedPeople.Clear();
            Array.Clear(GPostSends, 0, GPostSends.Length);
            _gPostMoves = _gPostRestored = _gPostNoSpot = _gPostNoMain = _gPostLimit = 0;
            _gThreats = _gThreatsCmd = _gThreatsBig = _gThreatFoes = _gThreatRiders = _gFromPost = _gDry = 0;
            _gSends = _gArrived = _gBack = _gTimeout = _gGaveUp = 0;
            _gTravelSum = 0f;
            Array.Clear(GKindSend, 0, GKindSend.Length);
            Array.Clear(GEnd, 0, GEnd.Length);
            _gGuardEps = _gGuardMen = _gGuardReached = _gGuardNone = 0;
            _gAttacked340 = _gChargeDest340 = _gChargeGuard340 = 0;
            Array.Clear(GGuardEnd, 0, GGuardEnd.Length);
            _gLoneSum = _gPresentSum = _gLoneMax = 0f;
            _gLoneWho = "-";
            _gCmdKilled = _gCmdKo = _gFateThreat = _gFatePost = _gFateOther = _gStum = 0;
            HuntClear();
        }

        // ------------------------------------------------------------ rola

        /// <summary>Rola bohatera AI: 2 wodz druzyny / kapitan szyku, 1 glowa rodu / wladca / nastepca, 0 reszta. Szyk gracza i druzyna gracza-wodza - 0.</summary>
        private static int RoleOf(Man man)
        {
            var a = man.A;
            var t = a.Team;
            if (!MissionTeams.Valid(t)) return RoleNone;
            if (t.IsPlayerTeam && t.IsPlayerGeneral) return RoleNone;
            var f = a.Formation;
            if (f != null && (f.HasPlayerControlledTroop || HorsePursuit.PlayerForm(f))) return RoleNone;
            bool gen = false, cap = false;
            try { gen = ReferenceEquals(t.GeneralAgent, a); } catch { }
            try { cap = f != null && ReferenceEquals(f.Captain, a); } catch { }
            if (gen || cap) return RoleCmd;
            if (!man.BigKnown)
            {
                man.BigKnown = true;
                try
                {
                    var co = a.Character as CharacterObject;
                    var h = co != null ? co.HeroObject : null;
                    man.Big = h != null && ((h.Clan != null && h.Clan.Leader == h) || Ransom178.IsRulerOrHeir(h));
                }
                catch { man.Big = false; }
            }
            return man.Big ? RoleBig : RoleNone;
        }

        /// <summary>Co pass (takze klucz NIE): rola, liczniki rol, samotnosc dowodcy (zaden swoj w 30 m), na sucho - zagrozenie, ktore by ruszylo.</summary>
        private static void GuardMeasure(Man man, Mission m, float now)
        {
            var a = man.A;
            int role = RoleOf(man);
            man.Role = role;
            if (role == RoleNone) return;
            if (role == RoleCmd && GCmdPeople.Add(a.Index))
            {
                _gCmd++;
                bool g = false;
                try { g = ReferenceEquals(a.Team.GeneralAgent, a); } catch { }
                if (g) _gGen++; else _gCap++;
            }
            else if (role == RoleBig && GBigPeople.Add(a.Index)) _gBig++;
            if (role > man.EverRole) man.EverRole = role;
            if (man.Mounted && GMountedPeople.Add(a.Index)) _gMounted++;
            if (role != RoleCmd) return;
            // samotnosc - jedno zapytanie natywne o sasiadow w 30 m
            Vec2 c = a.Position.AsVec2;
            if (!c.IsValid) return;
            int side = SideOf(a.Team);
            bool lone = true;
            try
            {
                m.GetNearbyAgents(c, LoneGap, _gNear);
                for (int i = 0; i < _gNear.Count; i++)
                {
                    var x = _gNear[i];
                    if (x == null || ReferenceEquals(x, a) || !x.IsHuman || !x.IsActive() || x.IsRunningAway) continue;
                    if (SideOf(x.Team) == side) { lone = false; break; }
                }
            }
            finally { _gNear.Clear(); }
            man.PresentSec += PassEvery;
            _gPresentSum += PassEvery;
            if (lone)
            {
                man.LoneSec += PassEvery;
                _gLoneSum += PassEvery;
                if (man.LoneSec > _gLoneMax) { _gLoneMax = man.LoneSec; _gLoneWho = Who(a); }
            }
            if (!_gKey && now >= man.DryUntil)
            {
                int foes, riders;
                Agent att;
                if (Threat(a, m, out foes, out riders, out att)) { _gDry++; man.DryUntil = now + AloneHold + ThreatCool; }
            }
        }

        private static string Who(Agent a)
        {
            if (a == null) return "?";
            string id = "?";
            try { id = a.Character != null ? (a.Character.StringId ?? "?") : "?"; } catch { }
            return id + "#" + a.Index;
        }

        // ------------------------------------------------------------ zagrozenie

        /// <summary>Zagrozony: wrogow (nie uciekajacych, nie w PANICE) w 10 m plus jezdzcow wroga w 20 m wiecej niz swoich (nie w PANICE / odwrocie na rozkaz) w 5 m.</summary>
        private static bool Threat(Agent a, Mission m, out int foes, out int riders, out Agent att)
        {
            foes = 0; riders = 0; att = null;
            int side = SideOf(a.Team);
            if (side < 0) return false;
            Vec2 c = a.Position.AsVec2;
            if (!c.IsValid) return false;
            int allies5 = 0, allies10 = 0, foes10 = 0, onHim = 0;
            float best = float.MaxValue, fr2 = FoeRadius * FoeRadius, ar2 = AllyRadius * AllyRadius;
            try
            {
                m.GetNearbyAgents(c, ThreatRider, _gNear);
                for (int i = 0; i < _gNear.Count; i++)
                {
                    var x = _gNear[i];
                    if (x == null || ReferenceEquals(x, a) || !x.IsHuman || !x.IsActive()) continue;
                    int s = SideOf(x.Team);
                    if (s < 0) continue;
                    float d2 = x.Position.AsVec2.DistanceSquared(c);
                    if (s == side)
                    {
                        if (d2 <= fr2 && !SurvivalMode.InPanic(x) && !SurvivalMode.OrderedRetreat(x, x.CommonAIComponent)) { allies10++; if (d2 <= ar2) allies5++; }
                        continue;
                    }
                    if (x.IsRunningAway || SurvivalMode.InPanic(x)) continue;
                    bool rider = x.MountAgent != null;
                    if (d2 > fr2) { if (!rider || !Coming(x, a, c)) continue; }   // jezdziec 10-20 m tylko, gdy jedzie na niego (nie przejazd obok)
                    else
                    {
                        foes10++;
                        try { if (ReferenceEquals(x.GetTargetAgent(), a)) onHim++; } catch { }
                    }
                    foes++;
                    if (rider) riders++;
                    if (d2 < best) { best = d2; att = x; }
                }
            }
            finally { _gNear.Clear(); }
            // recenzja 335: swoich liczymy w 10 m (dowodca 3 m za linia w zwarciu nie jest "zagrozony"); warunek samotnego 149 (0 swoich w 5 m, >= 2 wrogow w 10 m) zostaje
            if (foes > 0 && (allies10 < foes || (allies5 == 0 && foes10 >= 2))) return true;
            // 340 (e): wrog w 10 m ma dowodce za cel, a swoich tylu co wrogow - remis go nie chroni (test lowcy 334f: kontakt 3 m przy swoim 5.6 m, bez reakcji)
            if (_gCharge340 && onHim > 0 && allies10 <= foes) { _gAttacked340++; return true; }
            return false;
        }

        /// <summary>Wrog jedzie na dowodce: ma go za cel albo predkosc > 2 m/s w 45 st. od kierunku na niego.</summary>
        private static bool Coming(Agent x, Agent a, Vec2 c)
        {
            try { if (ReferenceEquals(x.GetTargetAgent(), a)) return true; } catch { }
            Vec2 v;
            try { v = (x.MountAgent ?? x).GetRealGlobalVelocity().AsVec2; } catch { return false; }
            Vec2 to = c - x.Position.AsVec2;
            float vl = v.Length, tl = to.Length;
            if (vl < ComingSpeed || tl < 0.01f) return false;
            return Vec2.DotProduct(v, to) >= 0.7071f * vl * tl;
        }

        /// <summary>Szyk w szarzy (rozkaz Charge / ChargeToTarget).</summary>
        private static bool Charging(Formation f)
        {
            if (f == null) return false;
            try
            {
                var o = f.GetReadonlyMovementOrderReference().OrderEnum;
                return o == MovementOrder.MovementOrderEnum.Charge || o == MovementOrder.MovementOrderEnum.ChargeToTarget;
            }
            catch { return false; }
        }

        /// <summary>Czlowiek w walce wrecz (cel w 3 m) albo w szyku w szarzy - nie schodzi na straz.</summary>
        private static bool Busy(Agent x)
        {
            if (Charging(x.Formation)) return true;
            try
            {
                var tg = x.GetTargetAgent();
                if (tg != null && tg.IsActive() && tg.Position.AsVec2.DistanceSquared(x.Position.AsVec2) <= MeleeBusy * MeleeBusy) return true;
            }
            catch { }
            return false;
        }

        /// <summary>340 (e): czlowiek w walce wrecz (cel w 3 m) - bez warunku szarzy szyku.</summary>
        private static bool InMelee(Agent x)
        {
            try
            {
                var tg = x.GetTargetAgent();
                return tg != null && tg.IsActive() && tg.Position.AsVec2.DistanceSquared(x.Position.AsVec2) <= MeleeBusy * MeleeBusy;
            }
            catch { return false; }
        }

        /// <summary>PassOne przy kluczu TAK (bez rany): zagrozenie (start / trwa - krok jak samotny 149) albo stanowisko dowodcy. false = zwykla droga 149.</summary>
        private static bool GuardPass(Man man, Mission m, float now)
        {
            var a = man.A;
            if (man.Role == RoleNone && man.Mode != ModeThreat)
            {
                if (man.Mode == ModePost) { Release(man); PostOff(man); }
                else if (man.Mode == ModeRally) { Release(man); RallyOff(man, now, Rally359End.Other); }   // 359: juz nie dowodzi
                return false;
            }
            int foes = 0, riders = 0;
            Agent att = null;
            bool thr = now >= man.CoolUntil && Threat(a, m, out foes, out riders, out att);
            if (man.Mode == ModeThreat)
            {
                if (thr && man.St == StHold) man.HoldUntil = now + AloneHold;   // wrog dalej przy nim - stoi przy swoich
                StepOne(man, m, now);
                return true;
            }
            if (thr)
            {
                if (man.Mode == ModeRally) RallyOff(man, now, Rally359End.Threat);   // 359: zagrozenie ma pierwszenstwo (pozycje nadpisze cel wycofania)
                StartThreat(man, m, now, foes, riders, att);
                StepOne(man, m, now);
                return true;
            }
            // 359: wyjazd do uciekajacych - trwa (ocena co 0.5 s) albo start (z eskorta, grupa za linia daleko od wroga); inaczej stanowisko 335
            if (man.Mode == ModeRally)
            {
                RallyStep(man, m, now, true);
                if (man.Mode == ModeRally) return true;
            }
            if (man.Role == RoleCmd)
            {
                if (RallyTry(man, m, now)) return true;
                PostPass(man, m, now);
                return true;
            }
            if (man.Mode == ModePost) { Release(man); PostOff(man); }
            return false;
        }

        private static void StartThreat(Man man, Mission m, float now, int foes, int riders, Agent att)
        {
            if (man.Mode == ModePost) _gFromPost++;
            man.Mode = ModeThreat;
            man.PostKind = 0;
            man.Since = now;
            man.NextEval = now;
            man.St = StTravel;
            man.EpArrived = false;
            man.TravelStart = -100f;
            man.Kind = -1;
            man.Target = Vec2.Invalid;
            man.Wp = WorldPosition.Invalid;                               // pozycja stanowiska zostaje do pierwszego celu (Evaluate nadpisze)
            man.EverThreat = true;
            man.EverWithdrawn = true;
            _gThreats++;
            if (man.Role == RoleCmd) _gThreatsCmd++; else _gThreatsBig++;
            GThreatPeople.Add(man.A.Index);
            _gThreatFoes += foes;
            _gThreatRiders += riders;
            if (ReferenceEquals(man, _htTM) && _htThr < 0f) _htThr = now;
            Summon(man, m, now, foes);
        }

        // ------------------------------------------------------------ najblizszy wlasny szyk

        /// <summary>Cel wycofania dowodcy / glowy rodu: najblizszy wlasny szyk (>= 3 ludzi, nie w odwrocie, nie w szarzy; pieszy przed konnym - konny tylko, gdy
        /// pieszego brak): zagrozony - srodek, przy wrogu tyl; ranny (stoi do konca bitwy) - od razu tyl szyku. Dwa najblizsze szyki.</summary>
        private static Vec2 NearestOwn(Man man, Mission m, float back, bool wounded, out int kind, out WorldPosition wp)
        {
            kind = -1;
            wp = WorldPosition.Invalid;
            var a = man.A;
            var t = a.Team;
            if (!MissionTeams.Valid(t)) return Vec2.Invalid;
            Vec2 c = a.Position.AsVec2;
            Formation f1 = null, f2 = null;
            float d1 = float.MaxValue, d2 = float.MaxValue;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            int passes = _gCharge340 ? 4 : 2;   // 340 (e): przejscia 2-3 - szyki w szarzy (cala druzyna w szarzy - 334f "bez miejsca")
            bool chargeUsed = false;
            for (int pass = 0; pass < passes && f1 == null; pass++)
            {
                bool wantCharge = pass >= 2;
                chargeUsed = wantCharge;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits < 3) continue;
                    try { if (f.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Retreat) continue; } catch { }
                    if (Charging(f) != wantCharge || !f.CurrentPosition.IsValid) continue;
                    if ((pass % 2) == 0 && !Foot(f)) continue;                // najpierw piesi; jazda dopiero, gdy pieszego szyku brak
                    float d = f.CurrentPosition.DistanceSquared(c);
                    if (d < d1) { f2 = f1; d2 = d1; f1 = f; d1 = d; }
                    else if (d < d2) { f2 = f; d2 = d; }
                }
            }
            for (int j = 0; j < 2; j++)
            {
                var f = j == 0 ? f1 : f2;
                if (f == null) break;
                Vec2 p = f.CurrentPosition;
                int r;
                if (!wounded && !chargeUsed)   // 340: szyk w szarzy - tylko tyl (srodek to zwarcie)
                {
                    r = Check(a, m, p, out wp);
                    if (r == CheckLimit) { kind = KindLimit; return Vec2.Invalid; }
                    if (r == CheckOk) { kind = KNear; return p; }
                }
                if (Rear(f, back, out p))
                {
                    r = Check(a, m, p, out wp);
                    if (r == CheckLimit) { kind = KindLimit; return Vec2.Invalid; }
                    if (r == CheckOk) { kind = KNear; if (chargeUsed) _gChargeDest340++; return p; }
                }
            }
            wp = WorldPosition.Invalid;
            return Vec2.Invalid;
        }

        // ------------------------------------------------------------ stanowisko dowodcy

        private static bool Foot(Formation f)
        {
            var q = f.QuerySystem;
            return q != null && !q.IsCavalryFormation && !q.IsRangedCavalryFormation && (q.IsInfantryFormation || q.IsRangedFormation);
        }

        /// <summary>Jezdzcy-szeregowi AI w szyku (eskorta konna).</summary>
        private static int Escort(Formation f, Agent hero)
        {
            int n = 0;
            f.ApplyActionOnEachUnit(delegate (Agent x)
            {
                if (x != null && !ReferenceEquals(x, hero) && !x.IsHero && x.IsActive() && x.MountAgent != null && x.IsAIControlled && !x.IsRunningAway) n++;
            });
            return n;
        }

        /// <summary>Glowny szyk druzyny jak BehaviorGeneral (AI.IsMainFormation, nie wlasny), bez niego najwiekszy pieszy szyk >= 8 (MainOf 149).</summary>
        private static Formation MainForm(Team t, Formation own)
        {
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || ReferenceEquals(f, own) || f.CountOfUnits <= 0) continue;
                bool main = false;
                try { main = f.AI != null && f.AI.IsMainFormation; } catch { }
                if (main) return f;
            }
            return MainOf(t, own);
        }

        /// <summary>Stanowisko dowodcy (co 0.25 s, nowa ocena co 1.5 s): eskorta konna / w pieszym szyku / za glownym szykiem (opis klasy).</summary>
        private static void PostPass(Man man, Mission m, float now)
        {
            var a = man.A;
            if (!_gKey || man.Role != RoleCmd) { if (man.Mode == ModePost) { Release(man); PostOff(man); } return; }
            if (man.Mode == ModePost && now < man.NextEval)
            {
                bool lost = false;
                if (man.Wp.IsValid) { try { lost = (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { lost = false; } }
                if (lost) { var o = man.Wp; a.SetScriptedPosition(ref o, false, Agent.AIScriptedFrameFlags.None); _gPostRestored++; }
                return;
            }
            man.NextEval = now + RefreshAlone;
            var t = a.Team;
            var f = a.Formation;
            if (!MissionTeams.Valid(t)) return;
            Vec2 c = a.Position.AsVec2;
            // a) eskorta konna
            if (f != null && man.Mounted)
            {
                int esc = Escort(f, a);
                if (esc >= EscortMin)
                {
                    if (GEscPeople.Add(a.Index)) _gEscortForms++;
                    if (ReferenceEquals(man, _htTM) && _htTEsc < esc) _htTEsc = esc;
                    float slack = Math.Max(f.Width, f.Depth) * 0.5f + RearMounted;
                    Vec2 cen = f.CurrentPosition;
                    if (!cen.IsValid || c.DistanceSquared(cen) <= slack * slack) { if (man.Mode == ModePost) { Release(man); PostOff(man); } return; }
                    Vec2 rp;
                    if (Rear(f, RearMounted, out rp)) PostSet(man, m, now, rp, PKEscort);
                    return;
                }
            }
            // b) w pieszym szyku
            if (f != null && f.CountOfUnits >= MainMin && Foot(f))
            {
                Vec2 cen = f.CurrentPosition;
                float r = Math.Max(f.Width, f.Depth) * 0.5f + RearFoot;
                if (cen.IsValid && c.DistanceSquared(cen) <= r * r)
                {
                    GCovPeople.Add(a.Index);
                    if (man.Mode == ModePost) { Release(man); PostOff(man); }
                    return;
                }
            }
            // c) stanowisko za glownym szykiem (BehaviorGeneral)
            // recenzja 335: wlasny szyk glowny albo pieszy >= 8 - stanowisko za WLASNYM szykiem (kapitan, ktory wyszedl z obrysu, wraca do swoich, nie za cudzy szyk)
            bool ownMain = false;
            if (f != null && f.CountOfUnits > 0)
            {
                try { ownMain = f.AI != null && f.AI.IsMainFormation; } catch { }
                if (!ownMain && f.CountOfUnits >= MainMin && Foot(f)) ownMain = true;
            }
            var main = ownMain ? f : MainForm(t, f);
            if (main == null) { _gPostNoMain++; if (man.Mode == ModePost) { Release(man); PostOff(man); } return; }
            Vec2 med = main.CachedMedianPosition.AsVec2;
            Vec2 dir = Vec2.Invalid;
            try { dir = main.QuerySystem.Team.MedianTargetFormationPosition.AsVec2 - med; } catch { dir = Vec2.Invalid; }
            if (!dir.IsValid || dir.LengthSquared < 1f) dir = main.Direction;
            if (!med.IsValid || !dir.IsValid || dir.LengthSquared <= 0.0001f) return;
            Vec2 p = med - dir.Normalized() * (Math.Max(0f, main.Depth) + CommandBack);
            PostSet(man, m, now, p, PKPost);
        }

        private static void PostSet(Man man, Mission m, float now, Vec2 p, int kind)
        {
            var a = man.A;
            bool lost = false;
            if (man.Mode == ModePost && man.Wp.IsValid) { try { lost = (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { lost = false; } }
            bool same = man.Mode == ModePost && man.PostKind == kind && man.Target.IsValid && man.Target.DistanceSquared(p) <= MoveMin * MoveMin;
            if (same && !lost) return;
            WorldPosition wp;
            int r = Check(a, m, p, out wp);
            if (r == CheckLimit) { _gPostLimit++; man.NextEval = now + StepEvery; return; }
            if (r != CheckOk)
            {
                _gPostNoSpot++;
                if (lost && man.Wp.IsValid) { var o = man.Wp; a.SetScriptedPosition(ref o, false, Agent.AIScriptedFrameFlags.None); _gPostRestored++; }
                return;
            }
            bool first = man.Mode != ModePost || man.PostKind != kind;
            man.Mode = ModePost;
            man.PostKind = kind;
            man.St = StTravel;
            man.Target = p;
            man.Wp = wp;
            man.EverPost = true;
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);   // bez NoAttack (broni sie), bez DoNotRun
            if (first) { GPostSends[kind]++; GPostPeople.Add(a.Index); if (ReferenceEquals(man, _htTM) && _htStartMode == "-") _htStartMode = kind == PKEscort ? "przy eskorcie" : "stanowisko"; }
            else if (lost) _gPostRestored++;
            else _gPostMoves++;
        }

        private static void PostOff(Man man)
        {
            man.Mode = ModeNone;
            man.St = StIdle;
            man.PostKind = 0;
            man.Wp = WorldPosition.Invalid;
            man.Target = Vec2.Invalid;
        }

        // ------------------------------------------------------------ straz

        private static bool GuardOk(Agent x, Agent hero, Team t) { return GuardOk(x, hero, t, false); }

        /// <summary>340 (e): charge - drugie przejscie strazy: czlowiek z szyku w szarzy dozwolony, walka wrecz (cel w 3 m) dalej wyklucza.</summary>
        private static bool GuardOk(Agent x, Agent hero, Team t, bool charge)
        {
            if (x == null || ReferenceEquals(x, hero) || !x.IsHuman || x.IsHero || x.IsMainAgent || !x.IsAIControlled || !x.IsActive()) return false;
            if (!ReferenceEquals(x.Team, t) || x.IsRunningAway || SurvivalMode.InPanic(x) || SurvivalMode.OrderedRetreat(x, x.CommonAIComponent) || Withdrawing(x)) return false;
            if (GuardSet.Contains(x) || HorsePursuit.Banner(x) || HorsePursuit.HuntAnyThread(x) || HorsePursuit.RidingAnyThread(x) || HorsePursuit.TestRunAnyThread(x)
                || Doctrine.H447AnyThread(x)) return false;   // 447: jezdziec polowania na gracza-wodza
            var rk = RoutHold.Live;
            if (rk != null && rk.InRally(x)) return false;                                                            // 359: zawrocony w zbiorce (przed szykiem)
            var f = x.Formation;
            if (f == null || f.HasPlayerControlledTroop || HorsePursuit.PlayerForm(f)) return false;
            if (charge ? InMelee(x) : Busy(x)) return false;                                                           // recenzja 335: nie z walki wrecz ani z szarzy (340: szarza w 2. przejsciu)
            try { if ((x.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) return false; } catch { return false; }   // cudza pozycja skryptowa
            return true;
        }

        /// <summary>Straz: najblizsi wolni ludzie druzyny w 80 m (nie w walce wrecz, nie w szarzy; jezdziec z polowa odleglosci) - 3 na napastnika (3..20);
        /// pozycja skryptowa na dowodcy.</summary>
        private static void Summon(Man man, Mission m, float now, int foes)
        {
            if (!_gKey) return;
            var a = man.A;
            var t = a.Team;
            Vec2 c = a.Position.AsVec2;
            if (!c.IsValid || !MissionTeams.Valid(t)) return;
            int have = 0;
            for (int i = 0; i < Guards.Count; i++) if (ReferenceEquals(Guards[i].Of, man)) have++;
            _gCand.Clear();
            try
            {
                m.GetNearbyAgents(c, GuardReach, _gNear);
                for (int pass = 0; pass < (_gCharge340 ? 2 : 1) && _gCand.Count == 0; pass++)   // 340 (e): 2. przejscie - szyki w szarzy (bez walki wrecz)
                {
                    for (int i = 0; i < _gNear.Count; i++)
                    {
                        var x = _gNear[i];
                        if (!GuardOk(x, a, t, pass == 1)) continue;
                        float d = x.Position.AsVec2.Distance(c);
                        if (x.MountAgent != null) d *= 0.5f;
                        _gCand.Add(new KeyValuePair<float, Agent>(d, x));
                    }
                    if (pass == 1 && _gCand.Count > 0) _gChargeGuard340++;
                }
            }
            finally { _gNear.Clear(); }
            int want = Math.Max(GuardsMin, Math.Min(GuardsMax, GuardsPerFoe * Math.Max(1, foes))) - have;
            if (want <= 0) { _gCand.Clear(); return; }
            if (_gCand.Count == 0) { _gGuardNone++; return; }
            _gCand.Sort((x, y) => x.Key.CompareTo(y.Key));
            int n = Math.Min(want, _gCand.Count);
            var wp = a.GetWorldPosition();
            for (int i = 0; i < n; i++)
            {
                var x = _gCand[i].Value;
                var g = new Guard { A = x, Of = man, Since = now, Set = c };
                Guards.Add(g);
                GuardSet.Add(x);
                if (wp.IsValid) { var w = wp; x.SetScriptedPosition(ref w, false, Agent.AIScriptedFrameFlags.None); }
            }
            _gCand.Clear();
            _gGuardEps++;
            _gGuardMen += n;
            if (ReferenceEquals(man, _htTM)) _htGuards += n;
        }

        /// <summary>Co 0.25 s: straz idzie za dowodca; koniec przy wyjsciu z zagrozenia (ranny - po dotarciu), po TravelMax, przy panice / odwrocie, smierci.</summary>
        private static void GuardsStep(float now)
        {
            if (Guards.Count == 0) return;
            for (int i = Guards.Count - 1; i >= 0; i--)
            {
                var g = Guards[i];
                var x = g.A;
                var o = g.Of;
                int why = -1;
                if (x == null || !x.IsActive()) why = 3;
                else if (o == null || o.Out || o.A == null || !o.A.IsActive()) why = 3;
                else if (x.IsRunningAway || SurvivalMode.InPanic(x) || Withdrawing(x)) why = 2;
                else if (HorsePursuit.HuntAnyThread(x) || HorsePursuit.RidingAnyThread(x) || HorsePursuit.TestRunAnyThread(x) || Doctrine.H447AnyThread(x)) why = 5;   // inny mechanizm wzial czlowieka - jego pozycja
                else if (!(o.Mode == ModeThreat || (o.Mode == ModeWounded && o.St == StTravel) || o.Mode == ModeRally)) why = 0;   // 359: straz jedzie z dowodca do uciekajacych
                else if (now - g.Since > (o.Mode == ModeRally ? RideMax + RideHoldMax + 1f : TravelMax)) why = 1;   // przeglad 359: straz do konca postoju przy uciekajacych
                if (why >= 0)
                {
                    GGuardEnd[why]++;
                    ReleaseGuard(g, why != 2 && why != 5);
                    Guards.RemoveAt(i);
                    GuardSet.Remove(x);
                    continue;
                }
                Vec2 c = o.A.Position.AsVec2;
                Vec2 xp = x.Position.AsVec2;
                if (!g.Reached && xp.IsValid && c.IsValid && xp.DistanceSquared(c) <= GuardReached * GuardReached)
                {
                    g.Reached = true;
                    _gGuardReached++;
                    if (ReferenceEquals(o, _htTM)) _htGuardsReached++;
                }
                bool lost = false;
                try { lost = (x.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { }
                if (!lost && g.Set.IsValid && c.DistanceSquared(g.Set) <= GuardRepath * GuardRepath) continue;
                var wp = o.A.GetWorldPosition();
                if (!wp.IsValid) continue;
                x.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
                g.Set = c;
            }
        }

        private static void ReleaseGuard(Guard g, bool touch)
        {
            var x = g.A;
            if (!touch || x == null || !x.IsActive()) return;
            try { if ((x.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) x.DisableScriptedMovement(); } catch { }
        }

        private static void GuardsReleaseAll()
        {
            for (int i = 0; i < Guards.Count; i++)
            {
                GGuardEnd[4]++;
                try { ReleaseGuard(Guards[i], !(Guards[i].A != null && (Guards[i].A.IsRunningAway || Withdrawing(Guards[i].A)))); } catch { }
            }
            Guards.Clear();
            GuardSet.Clear();
        }

        // ------------------------------------------------------------ los

        private static void GuardRemoved(Man man, Agent a, bool killed, bool ko)
        {
            if (ReferenceEquals(a, _htT) && _htFate == 0) _htFate = killed ? 1 : (ko ? 2 : 3);
            if (man.EverRole == RoleNone) return;
            if (killed) _gCmdKilled++;
            else if (ko) _gCmdKo++;
            else return;
            if (man.Mode == ModeThreat) _gFateThreat++;
            else if (man.Mode == ModePost) _gFatePost++;
            else _gFateOther++;
        }

        // ------------------------------------------------------------ test lowcy (autotest)

        /// <summary>MarchToContact / FieldCraft (watki robocze, przez HorsePursuit.TestRunAnyThread): lowca testu 335 - bez capu marszu i oddechu szyku.</summary>
        internal static bool HunterAnyThread(Agent a)
        {
            if (a == null) return false;
            var r = Volatile.Read(ref _htRun);
            return r != null && ReferenceEquals(r, a);
        }

        private static void HuntClear()
        {
            _htOn = false;
            _htState = 0;
            _htWhy = "-"; _htEnd = "-"; _htStartMode = "-";
            _htGuards = _htGuardsReached = _htTEsc = _htFate = 0;
            _htA = null; _htT = null; _htTM = null;
            Volatile.Write(ref _htRun, null);
            _htDeploy = -1f; _htStart = 0f; _htNear = -1f; _htThr = -1f; _htContact = -1f; _htNext = 0f; _htLast = 0f; _htGapAtContact = -1f; _htMor0 = -1f; _htLone0 = 0f;
            _htTMounted = _htTGen = false;
        }

        private static void HuntTestBegin(Settings c)
        {
            HuntClear();
            if (c == null || !c.CommanderHuntInAutotest) { _htWhy = "klucz NIE"; return; }
            if (LogGate.Mode != 1) { _htWhy = "nie autotest"; return; }
            if (!_field) { _htWhy = "nie bitwa polowa"; return; }
            string plan = AutotestBattleKit.TestPlan();
            if (plan != "Weak") { _htWhy = "plan " + (plan ?? "?"); return; }   // mocna - nekajacy 204/283/333, odwrot - gracz ucieka; slaba - armia gracza stoi 150 s (333t)
            _htOn = true;
            _htWhy = "czynny";
            _htSeq++;
        }

        private static void HuntTestTick(Mission m, float now)
        {
            if (!_htOn || _htState == 2) return;
            if (_htDeploy < 0f) _htDeploy = now;
            if (_htState == 0)
            {
                if (now - _htDeploy < HuntPickAfter) return;
                if (HuntPick(m, now)) return;
                if (now - _htDeploy > HuntPickUntil) { _htState = 2; _htEnd = "brak lowcy albo wodza wroga"; HuntLine(now); }
                return;
            }
            _htLast = now;
            if (now < _htNext) return;
            _htNext = now + StepEvery;
            var a = _htA;
            var t = _htT;
            string end = null;
            if (a == null || !a.IsActive()) end = "lowca padl";
            else if (t == null || !t.IsActive()) end = _htFate == 1 ? "wodz zabity" : _htFate == 2 ? "wodz nieprzytomny" : _htFate == 3 ? "wodz uszedl" : "wodz zniknal";
            else if (m.MissionEnded) end = "koniec bitwy";
            else if (now - _htStart > HuntMax) end = "czas " + F0(HuntMax) + " s";
            if (end != null) { HuntEnd(end, now); return; }
            // lowca do konca jak gracz: HP i morale (bez paniki i odwrotu rannego przed kontaktem), cel - wodz
            try
            {
                if (_htContact < 0f && a.Health < a.HealthLimit) a.Health = a.HealthLimit;
                if (_htMor0 >= 0f && a.GetMorale() < _htMor0) a.SetMorale(_htMor0);
            }
            catch { }
            Vec2 tp = t.Position.AsVec2, ap = a.Position.AsVec2;
            float d = ap.Distance(tp);
            if (_htNear < 0f && d <= ThreatRider) _htNear = now;
            if (_htContact < 0f && d <= HuntContact)
            {
                _htContact = now;
                _htGapAtContact = NearestOwnMan(m, t);
            }
            var wp = t.GetWorldPosition();
            if (wp.IsValid) a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            try { a.SetTargetAgent(t); } catch { }
        }

        private static float NearestOwnMan(Mission m, Agent t)
        {
            float best = -1f;
            Vec2 c = t.Position.AsVec2;
            int side = SideOf(t.Team);
            try
            {
                m.GetNearbyAgents(c, GuardReach, _gNear);
                for (int i = 0; i < _gNear.Count; i++)
                {
                    var x = _gNear[i];
                    if (x == null || ReferenceEquals(x, t) || !x.IsHuman || !x.IsActive() || SideOf(x.Team) != side) continue;
                    float d = x.Position.AsVec2.Distance(c);
                    if (best < 0f || d < best) best = d;
                }
            }
            finally { _gNear.Clear(); }
            return best;
        }

        private static bool HuntPick(Mission m, float now)
        {
            Team pt = m.PlayerTeam;
            if (!MissionTeams.Valid(pt)) return false;
            // cel: wodz druzyny wroga (AI, bohater), bez niego kapitan-bohater wroga
            Agent tgt = null;
            bool gen = false;
            foreach (var t in m.Teams)
            {
                if (!MissionTeams.Valid(t) || !t.IsEnemyOf(pt)) continue;
                var g = t.GeneralAgent;
                if (g != null && g.IsActive() && g.IsHero && g.IsAIControlled && !g.IsMainAgent) { tgt = g; gen = true; break; }
            }
            if (tgt == null)
            {
                for (int i = 0; i < List.Count && tgt == null; i++)
                {
                    var mm = List[i];
                    if (mm.Out || mm.Excl || mm.A == null || !mm.A.IsActive() || mm.Mine || mm.Role != RoleCmd) continue;
                    tgt = mm.A;
                }
            }
            if (tgt == null) return false;
            // lowca: czlowiek AI druzyny gracza, bron wrecz, konno najpierw (jak Jeff), najblizszy celu
            Agent best = null;
            float bd = float.MaxValue;
            bool bm = false;
            Vec2 tp = tgt.Position.AsVec2;
            var ags = m.Agents;
            int n = ags != null ? ags.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var a = ags[i];
                if (a == null || !a.IsHuman || a.IsHero || a.IsMainAgent || !a.IsAIControlled || !a.IsActive() || !ReferenceEquals(a.Team, pt)) continue;
                if (!a.HasMeleeWeaponCached || a.IsRunningAway || SurvivalMode.InPanic(a) || HorsePursuit.Banner(a) || HorsePursuit.TestRunAnyThread(a)) continue;
                bool mt = a.MountAgent != null;
                float d = a.Position.AsVec2.Distance(tp);
                if ((mt && !bm) || (mt == bm && d < bd)) { best = a; bd = d; bm = mt; }
            }
            if (best == null) return false;
            Man tm;
            Men.TryGetValue(tgt, out tm);
            _htA = best; _htT = tgt; _htTM = tm;
            _htTGen = gen;
            _htTMounted = tgt.MountAgent != null;
            if (tm != null && tm.Mode == ModePost) _htStartMode = tm.PostKind == PKEscort ? "przy eskorcie" : "stanowisko";
            _htLone0 = tm != null ? tm.LoneSec : 0f;
            _htState = 1;
            _htStart = now;
            Volatile.Write(ref _htRun, best);
            try { _htMor0 = best.GetMorale(); } catch { _htMor0 = -1f; }
            try { best.SetAutomaticTargetSelection(false); best.SetTargetAgent(tgt); } catch { }
            Log.Info("Ochrona wodza (335) TEST start: lowca " + Who(best) + " (" + (bm ? "konno" : "pieszo") + ") na " + (gen ? "wodza" : "kapitana") + " wroga " + Who(tgt)
                     + " (" + (_htTMounted ? "konno" : "pieszo") + ", szyk " + (tgt.Formation != null ? tgt.Formation.CountOfUnits.ToString() : "-") + " ludzi), odleglosc " + F0(bd)
                     + " m | klucz Commanders Keep Command Post " + (_gKey ? "TAK" : "NIE") + " | bitwa slaba nr " + _htSeq + ".");
            return true;
        }

        private static void HuntEnd(string why, float now)
        {
            _htEnd = why;
            _htState = 2;
            var a = _htA;
            Volatile.Write(ref _htRun, null);
            if (a != null && a.IsActive())
            {
                try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); } catch { }
                try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
            }
            HuntLine(now);
        }

        private static void HuntLine(float now)
        {
            if (_htA == null)
            {
                Log.Info("Ochrona wodza (335) TEST koniec: lowca brak, wynik " + _htEnd + ".");
                return;
            }
            var tm = _htTM;
            float lone = tm != null ? tm.LoneSec - _htLone0 : 0f;
            string fate = _htFate == 1 ? "zabity" : _htFate == 2 ? "nieprzytomny" : _htFate == 3 ? "uszedl" : "zyje";
            Log.Info("Ochrona wodza (335) TEST koniec: lowca " + Who(_htA) + ", wynik " + _htEnd + " po " + F1(now - _htStart) + " s | wodz " + Who(_htT) + " ("
                     + (_htTGen ? "wodz" : "kapitan") + ", " + (_htTMounted ? "konno" : "pieszo") + ", eskorta konna " + _htTEsc + ", na starcie " + _htStartMode
                     + ") | zblizenie 20 m " + (_htNear >= 0f ? F1(_htNear - _htStart) + " s" : "-") + ", zagrozenie wodza " + (_htThr >= 0f ? F1(_htThr - _htStart) + " s" : "-")
                     + ", kontakt 3 m " + (_htContact >= 0f ? F1(_htContact - _htStart) + " s" : "-") + ", najblizszy swoj przy kontakcie " + (_htGapAtContact >= 0f ? F1(_htGapAtContact) + " m" : "-")
                     + " | straz wezwana " + _htGuards + ", dotarla " + _htGuardsReached + " | wodz " + fate + ", samotnie (bez swojego w " + F0(LoneGap) + " m) " + F1(lone) + " s.");
        }

        // ------------------------------------------------------------ linia

        private static void GuardWrite(Settings c)
        {
            if (!_field) return;
            if (_htOn && _htState == 1) HuntEnd("koniec bitwy", _htLast);
            var sb = new StringBuilder(1600);
            sb.Append("Ochrona wodza (335): bitwa misja ").Append(_kind).Append(", klucz ").Append(_gKey ? "TAK" : (_key ? "NIE (tylko pomiar)" : "NIE (149 NIE)"))
              .Append(" (dowodca = wodz / kapitan AI, glowa rodu = glowa rodu / wladca; zagrozenie: wrog w ").Append(F0(FoeRadius)).Append(" m albo jezdziec nadjezdzajacy w ").Append(F0(ThreatRider))
              .Append(" m, swoich w ").Append(F0(FoeRadius)).Append(" m mniej albo 0 w ").Append(F0(AllyRadius)).Append(" m; eskorta konna od ").Append(EscortMin).Append(" jezdzcow; stanowisko glebokosc + ").Append(F0(CommandBack))
              .Append(" m za glownym / wlasnym szykiem; straz ").Append(GuardsPerFoe).Append(" na wroga (").Append(GuardsMin).Append("..").Append(GuardsMax).Append(") w ").Append(F0(GuardReach)).Append(" m)");
            sb.Append(" | dowodcow ").Append(_gCmd).Append(" (wodzowie ").Append(_gGen).Append(", kapitanowie ").Append(_gCap).Append("), glow rodu ").Append(_gBig)
              .Append(", konnych ").Append(_gMounted).Append(", z eskorta konna ").Append(_gEscortForms).Append(", w pieszym szyku ").Append(GCovPeople.Count);
            sb.Append(" | stanowisko: dowodcow ").Append(GPostPeople.Count).Append(" (za glownym szykiem ").Append(GPostSends[PKPost]).Append(", przy eskorcie ").Append(GPostSends[PKEscort])
              .Append(", przesuniec ").Append(_gPostMoves).Append(", przywrocen ").Append(_gPostRestored).Append(", bez miejsca ").Append(_gPostNoSpot).Append(", bez glownego szyku ").Append(_gPostNoMain)
              .Append(", limit zapytan ").Append(_gPostLimit).Append(')');
            float tn = Math.Max(1, _gThreats);
            sb.Append(" | zagrozenia ").Append(_gThreats).Append(" (ludzi ").Append(GThreatPeople.Count).Append("; dowodcy ").Append(_gThreatsCmd).Append(", glowy rodu ").Append(_gThreatsBig)
              .Append(", ze stanowiska ").Append(_gFromPost).Append("; sr. wrogow ").Append(_gThreats > 0 ? F1(_gThreatFoes / tn) : "-").Append(", jezdzcow wsrod nich ").Append(_gThreatRiders)
              .Append("; na sucho ").Append(_gDry).Append(')');
            sb.Append(" | cel: najblizszy swoj szyk ").Append(GKindSend[KNear]).Append(" / tyl wlasnego ").Append(GKindSend[KRear]).Append(" / glowna sila ").Append(GKindSend[KMain])
              .Append(" / sztandar ").Append(GKindSend[KBanner]).Append(", wyslan ").Append(_gSends).Append(", dotarli ").Append(_gArrived).Append(" (sr. ")
              .Append(_gArrived > 0 ? F1(_gTravelSum / _gArrived) : "-").Append(" s); powrot po postoju ").Append(_gBack).Append(", w drodze ponad ").Append(F0(TravelMax)).Append(" s ")
              .Append(_gTimeout).Append(", bez miejsca ").Append(_gGaveUp);
            sb.Append(" | przejeci: odwrot rannego 143 ").Append(GEnd[E143]).Append(", PANIKA ").Append(GEnd[EPain] + GEnd[EGame] + GEnd[EPanicOther]).Append(", odwrot na rozkaz ")
              .Append(GEnd[EOrder]).Append(", gracz ").Append(GEnd[EPlayer]).Append(", klucz NIE ").Append(GEnd[EKey]);
            sb.Append(" | straz: wezwan ").Append(_gGuardEps).Append(" (ludzi ").Append(_gGuardMen).Append(", sr. ").Append(_gGuardEps > 0 ? F1(_gGuardMen / (float)_gGuardEps) : "-")
              .Append(", bez kandydatow ").Append(_gGuardNone).Append("), dotarlo do dowodcy ").Append(_gGuardReached).Append("; koniec: zagrozenie minelo ").Append(GGuardEnd[0])
              .Append(", czas ").Append(GGuardEnd[1]).Append(", panika/odwrot ").Append(GGuardEnd[2]).Append(", padl ").Append(GGuardEnd[3]).Append(", koniec bitwy ").Append(GGuardEnd[4])
              .Append(", przejety (148/204/333) ").Append(GGuardEnd[5]);
            float share = _gPresentSum > 0f ? _gLoneSum / _gPresentSum : 0f;
            sb.Append(" | samotnie (bez swojego w ").Append(F0(LoneGap)).Append(" m): ").Append(F1(_gLoneSum)).Append(" s z ").Append(F1(_gPresentSum)).Append(" s obecnosci dowodcow (")
              .Append(F0(share * 100f)).Append("%), najdluzej ").Append(F1(_gLoneMax)).Append(" s ").Append(_gLoneWho);
            sb.Append(" | los dowodcow i glow rodu: zabici ").Append(_gCmdKilled).Append(", nieprzytomni ").Append(_gCmdKo).Append(" (w zagrozeniu ").Append(_gFateThreat)
              .Append(", na stanowisku ").Append(_gFatePost).Append(", inaczej ").Append(_gFateOther).Append(')');
            sb.Append(" | test lowcy ").Append(_htOn ? "TAK" : "NIE (" + _htWhy + ")");
            sb.Append(" | 340: atak na dowodce (remis swoich) ").Append(_gAttacked340).Append(", cel za szykiem w szarzy ").Append(_gChargeDest340).Append(", straz z szarzy ").Append(_gChargeGuard340);
            sb.Append(" | potkniecia 335 ").Append(_gStum).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
