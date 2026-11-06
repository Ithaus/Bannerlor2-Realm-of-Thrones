using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334e - MYSL GODZINOWA. JEDNO wpiecie: SiegeWalls2.ThinkPost (postfiks Last CampaignEventDispatcher.AiHourlyTick - po wetach teatru (48), 274 (50), ArmsFirst p9
    /// i OB5; dlatego nasze NOWE opcje (armie lupia, Zelazni) same sprawdzaja weta: WarFront334.VetoedFor334e = teatr, 274, 326b). Obozy: ThinkPost -> Judge (przed F2
    /// i m116); poza obozem: ThinkPost -> ThinkField. Bez wlasnego postfiksu (jedno ocenianie oblezenia na godzine).
    ///  - OBLEZENIE W TOKU (klucz LocalSiegeJudge334e, rozdz. 4): wodz obozu (i samodzielna druzyna-wodz), nie gracz, nie szturm (F2), nie morze, nie skrypt, nie Inni.
    ///    Oboz = sila obozu + nasi idacy na te osade, ktorzy dojda przed przewaga wroga; odsiecz = wrogowie NADCHODZACY w horyzoncie 12.03 doby (zamiar OBRONY tej osady
    ///    albo w zasiegu wzroku obozu i blizej niz godzine temu; trzecia strona - nie), zaloga = zaloga + milicja + lordowie w osadzie (do ODSTAP najwyzej 0.8 x oboz).
    ///    Pozostaly czas oblezenia z postepu w grze: przygotowania (SiegePreparations.Progress) i tempo burzenia murow od konca przygotowan (model ostrzalu gry + mury
    ///    SiegeWalls2); bez danych - srednia gry 8.02 doby od startu. Macierz JudgeRule: KONTYNUUJ (q &lt;= 0.8 i jedzenie &gt;= 3 doby, albo przewaga odsieczy dojdzie
    ///    po zdobyciu) - wpis oblezenia = max(1.05 x najlepszy + 0.01, 0.5), chyba ze najlepszy to obrona wlasnej twierdzy (OB5), wpis zawetowany / rozejm (&lt;= 0),
    ///    wodz chroniony 335; SRODEK - gra; ODSTAP (przewaga dojdzie przed koncem, 2 kolejne godziny) - tylko gdy ucieczka zdazy (droga do wlasnej twierdzy dalej od
    ///    odsieczy krotsza niz przyjazd przewagi): oblezenie -1, GoToSettlement, flaga odwrotu 24 h (BK stick i m116 Hold jej nie trzymaja), blokada celu 1 doba.
    ///    Ucieczka nie zdazy - zostaje w obozie (bitwa przy obozie z wypadem zalogi lepsza niz poscig w polu).
    ///  - RABUNEK W TOKU (klucz LocalRaidBreakOff334e, rozdz. 4.3): rabujacy wodz - wrogowie nadchodzacy w pozostalym czasie rabunku silniejsi niz 1.25 x my, 2 godziny
    ///    - wpis rabunku tej wsi -1 (gra konczy rabunek, ucieczke prowadzi 223), blokada wsi 1 doba.
    ///  - ARMIE LUPIA (.Score.cs ArmyRaid) i ZELAZNI OBLEGAJA WYBRZEZE (ponizej) - tylko gdy mysl nie ma oblezenia z wynikiem &gt; 0.
    /// </summary>
    internal static partial class LocalFront334e
    {
        private sealed class CampRec
        {
            public int Hour = -1, Bad, State = -1, SaidDay = -1, StaySaid = -1; public double Q; public double PrepH = -1, Wall0 = 1;
            public double BadH = -1;   // 334f: godzina ostatniej oceny ODSTAP (histereza z mysli dowodcy, nie z godzin zegara)
            public int TLeftH = -1; public float TLeft;   // 334g (W3): pozostaly czas - raz na godzine zegara
            public readonly Dictionary<MobileParty, float> Dist = new Dictionary<MobileParty, float>();
            public readonly Dictionary<MobileParty, KeyValuePair<double, float>> Mem = new Dictionary<MobileParty, KeyValuePair<double, float>>();   // 334f: odsiecz z zamiarem -> (godzina, odleglosc)
            public readonly List<MobileParty> RelLeads = new List<MobileParty>();   // 340 (c): odsiecz policzona w ostatnim sadzie (do pamieci po odwrocie)
        }
        /// <summary>340 (c): pamiec odsieczy po odwrocie krolestwa spod osady - druzyny, ktore wywolaly odwrot.</summary>
        /// <summary>340 (c): Since - doba odwrotu (twardy koniec Since + HorizonDays); Seen - druzyna -> godzina ostatnio widzianego zamiaru obrony celu (pamiec MemHours,
        /// jak sad oblezenia); H/E - wpisy na godzine (zywa droga / plotka x2 z migawki), poprawka zwiadu - przy ocenie.</summary>
        private sealed class KRel
        {
            public readonly List<MobileParty> L = new List<MobileParty>(); public double Since;
            public readonly Dictionary<MobileParty, double> Seen = new Dictionary<MobileParty, double>();
            public int H = int.MinValue; public readonly List<KE> E = new List<KE>();
        }
        /// <summary>340 (c): wpis odsieczy z pamieci - wodz (druzyna albo wodz armii), zamiar (teraz albo w MemHours), droga i sila na zywo, droga plotki (migawka x 2).</summary>
        private struct KE { public MobileParty Lead; public bool Now, Mem; public float LD, LS, RD, RS; }
        private struct PS : IEquatable<PS>
        {
            public readonly MobileParty P; public readonly Settlement S;
            public PS(MobileParty p, Settlement s) { P = p; S = s; }
            public bool Equals(PS o) { return ReferenceEquals(P, o.P) && ReferenceEquals(S, o.S); }
            public override bool Equals(object o) { return o is PS && Equals((PS)o); }
            public override int GetHashCode() { unchecked { return ((P != null ? P.GetHashCode() : 0) * 397) ^ (S != null ? S.GetHashCode() : 0); } }
        }
        private static readonly Dictionary<SiegeEvent, CampRec> _camps = new Dictionary<SiegeEvent, CampRec>();
        private static readonly Dictionary<MobileParty, double> _retreat = new Dictionary<MobileParty, double>();   // flaga odwrotu do godziny
        private static readonly Dictionary<PS, double> _block = new Dictionary<PS, double>();                    // (druzyna, osada) -> doba konca
        // 334g (petla odstapienie -> ponowne oblezenie): po odwrocie (krolestwo, osada) do przyjazdu odsieczy + pol doby (co najmniej 1 doba) - start tylko przy
        // tej samej mierze co odwrot (odsiecz + zaloga do 0.8 x oboz, okno max(dojscie + 8.02, 12.03)) <= 1.0
        private static readonly Dictionary<SK, double> _kblock = new Dictionary<SK, double>();
        private static int _dKBlock;
        // 340 (c, L15b): po odwrocie start pod ta sama osada zablokowany takze PO czasie _kblock, dopoki odsiecz, ktora wywolala odwrot, jest w zasiegu (zywa droga
        // w oknie max(nasze dojscie + 8.02, 12.03)) i razem z zaloga (do 0.8 x my) ma nad nami przewage (> 1.0). Histereza: odwrot przy > 1.25, powrot dopiero <= 1.0
        // albo gdy odsiecz wyjdzie z zasiegu (czas dojscia dluzszy niz nasze oblezenie)
        private static readonly Dictionary<SK, KRel> _krel = new Dictionary<SK, KRel>();
        private static int _dKRelHold, _dKRelFree;
        private static readonly Dictionary<MapEvent, int[]> _raidBad = new Dictionary<MapEvent, int[]>();        // [godzina, kolejne zle]
        private static int _dJudge, _dKeep, _dMid, _dRetreat, _dRetreatCand, _dStay, _dKeepVeto, _dBreak, _dStickYield, _dFreshYield, _dIronOffers, _dIronVeto, _dSiegeLines;
        private static readonly int[] _dStates = new int[6];
        private static readonly List<string> _dRetreatNotes = new List<string>();
        private static int _sRetreat;

        private static void ResetSiege() { _camps.Clear(); _retreat.Clear(); _block.Clear(); _kblock.Clear(); _krel.Clear(); _raidBad.Clear(); _ironNav.Clear(); _musterDay.Clear(); _musterTry.Clear(); _musterFail.Clear(); _sRetreat = 0; ZeroSiegeDay(); }
        private static void ZeroSiegeDay()
        {
            _dJudge = _dKeep = _dMid = _dRetreat = _dRetreatCand = _dStay = _dKeepVeto = _dBreak = _dStickYield = _dFreshYield = _dIronOffers = _dIronVeto = _dSiegeLines = _dKBlock = 0;
            _dKRelHold = _dKRelFree = 0; _dIronMuster = _dIronKeepSea = _dIronPrio = _dIronMusterLines = _dIronMusterFail = _dIronMusterBan = 0; Array.Clear(_dIronWhy, 0, _dIronWhy.Length);
            Array.Clear(_dStates, 0, _dStates.Length); _dRetreatNotes.Clear();
            Array.Clear(_dIronX, 0, _dIronX.Length); _dIronMinQ = -1; _dIronMinT = "-"; _dIronMinStr = 0;
            Array.Clear(_dIronMx, 0, _dIronMx.Length); Array.Clear(_dIronC, 0, _dIronC.Length); Array.Clear(_dIronCBest, 0, _dIronCBest.Length);
            for (int i = 0; i < _dIronCQ.Length; i++) { _dIronCQ[i].Clear(); _dIronCDist[i] = -1f; }
        }

        /// <summary>342 (Z4): linia doby "Zelazni cele (342)" - kazdy cel kanonu wybrzeza: oceny, odrzucenia, q (min / mediana), weta, model, oferty. Wolajacy trzyma _lk.</summary>
        private static string IronCanonLine(int today)
        {
            var sb = new System.Text.StringBuilder(600);
            sb.Append(Tag).Append("Zelazni cele (342) | dzien ").Append(today).Append(" | klucz ").Append(IronCoastOn ? "TAK" : "NIE");
            for (int i = 0; i < IronCanonIds.Length; i++)
            {
                var ql = _dIronCQ[i];
                double mn = -1, md = -1;
                if (ql.Count > 0) { var c = new List<double>(ql); c.Sort(); mn = c[0]; md = c[c.Count / 2]; }
                sb.Append(" | ").Append(IronCanonIds[i]).Append(": ocen ").Append(_dIronC[i, 0]).Append(", bez drogi ").Append(_dIronC[i, 2]).Append(", weta ").Append(_dIronC[i, 3])
                  .Append(", blokada ").Append(_dIronC[i, 4]).Append(", q ").Append(ql.Count).Append(" (min ").Append(mn < 0 ? "-" : F2(mn)).Append(", mediana ").Append(md < 0 ? "-" : F2(md))
                  .Append("), q>1 ").Append(_dIronC[i, 5]).Append(", model 0 ").Append(_dIronC[i, 6]).Append(", najlepszy ").Append(_dIronCBest[i].ToString("0.000", Inv))
                  .Append(", droga ").Append(_dIronCDist[i] < 0 ? "-" : F0(_dIronCDist[i])).Append(", oferty ").Append(_dIronC[i, 7]);
            }
            return sb.Append('.').ToString();
        }

        /// <summary>Flaga odwrotu (BK stick, m116 Hold): druzyna albo wodz jej armii odstepuje od oblezenia.</summary>
        internal static bool Retreating(MobileParty p)
        {
            if (p == null) return false;
            lock (_lk)
            {
                if (_retreat.Count == 0) return false;
                double until;
                double now = CampaignTime.Now.ToHours;
                if (_retreat.TryGetValue(p, out until) && until > now) return true;
                var a = p.Army;
                return a != null && a.LeaderParty != null && _retreat.TryGetValue(a.LeaderParty, out until) && until > now;
            }
        }

        /// <summary>Blokada celu po odwrocie / przerwanym rabunku (druzyna albo wodz armii). Wolajacy trzyma _lk.</summary>
        private static bool Blocked(MobileParty p, Settlement t)
        {
            if (_block.Count == 0 || p == null || t == null) return false;
            double until, now = CampaignTime.Now.ToDays;
            if (_block.TryGetValue(new PS(p, t), out until) && until > now) return true;
            var a = p.Army;
            return a != null && a.LeaderParty != null && _block.TryGetValue(new PS(a.LeaderParty, t), out until) && until > now;
        }

        /// <summary>334g: blokada (krolestwo, osada) po odwrocie - start oblezenia tylko, gdy miara odwrotu (wrog w polu w oknie max(dojscie + 8.02, 12.03) + zaloga
        /// najwyzej 0.8 x my) / my &lt;= 1.0. 340 (c): po czasie blokady - dalej zakaz, dopoki odsiecz z odwrotu (czlonek armii - jej wodz; zamiar obrony celu teraz albo
        /// w MemHours, albo zwiad; zywa droga przy zamiarze teraz / zwiadzie, plotka x2 przy pamietanym zamiarze) jest w tym oknie i z zaloga ma &gt; 1.0 x (my + sojusznicy);
        /// odsieczy brak - pamiec znika; twardy koniec: doba odwrotu + HorizonDays. o - nasza sila w mierze q. true - zakaz. Wolajacy trzyma _lk.</summary>
        private static bool KBlocked(IFaction k, Settlement t, MobileParty p, double o, float our, out double qr)
        {
            qr = -1;
            if ((_kblock.Count == 0 && _krel.Count == 0) || k == null || t == null || p == null) return false;
            var key = new SK(k, t);
            double until, nowD = CampaignTime.Now.ToDays;
            bool timed = _kblock.TryGetValue(key, out until) && until > nowD;
            KRel kr;
            bool rel = _krel.TryGetValue(key, out kr);
            // 340 (recenzja): twardy koniec pamieci - doba odwrotu + HorizonDays (dalej odsiecz nie zdazy nigdy)
            if (rel && nowD > kr.Since + HorizonDays) { _krel.Remove(key); _dKRelFree++; rel = false; }
            if (!timed && !rel) return false;
            float w = Math.Max(Math.Max(our, 0f) + SiegeDays, HorizonDays);
            float first; double ally;
            double z = ZFor(t, k, p, w, out first, out ally);
            double oo = o + ally;   // 340 (recenzja): ta sama nasza sila (z sojusznikami w drodze) w obu galeziach
            double gar = GarOf(t);
            if (timed)
            {
                qr = Ratio(z + GarCap(gar, oo), oo);
                if (qr > SiegeBanQ) { _dKBlock++; return true; }
            }
            if (rel)
            {
                // 340 (c): odsiecz z odwrotu ta sama wiedza co reszta modelu - zamiar obrony celu (teraz albo widziany w MemHours) albo zwiad; zywa droga tylko przy
                // zamiarze teraz / zwiadzie, pamietany zamiar - plotka (migawka x 2, jak ZFor); czlonek armii - jej wodz; w osadzie - w zalodze (GarOf)
                var sc = ScoutOf(p);
                var es = KRelEntries(kr, k, t);
                double s = 0;
                for (int i = 0; i < es.Count; i++)
                {
                    var e = es[i];
                    float d, st;
                    if (e.Now || sc.Contains(e.Lead)) { d = e.LD; st = e.LS; }   // zamiar teraz / zwiad - na zywo
                    else if (e.Mem) { d = e.RD; st = e.RS; }                      // pamietany zamiar - plotka
                    else continue;
                    if (d < 0f || d > w) continue;
                    s += st;
                }
                double qrel = Ratio(s + GarCap(gar, oo), oo);
                if (s > 0 && qrel > SiegeBanQ) { _dKRelHold++; if (qrel > qr) qr = qrel; return true; }
                // 340 (recenzja): zwolnienie tylko, gdy odsieczy nie ma (poza zasiegiem / bez zamiaru i niewidziana / martwa) - slabszy lord nie zdejmuje blokady silniejszym
                if (s <= 0 && !timed) { _krel.Remove(key); _dKRelFree++; }
            }
            return false;
        }

        /// <summary>340 (c): wpisy pamieci odsieczy na godzine (czlonkowie armii -> wodz, bez powtorzen; zamiar obrony celu teraz albo w MemHours). Wolajacy trzyma _lk.</summary>
        private static List<KE> KRelEntries(KRel kr, IFaction k, Settlement t)
        {
            int h = NowH;
            if (kr.H == h) return kr.E;
            kr.H = h; kr.E.Clear();
            double nowH = CampaignTime.Now.ToHours;
            int ki = FI(k);
            var seen = _kreSeen; seen.Clear();
            foreach (var m0 in kr.L)
            {
                if (m0 == null || !m0.IsActive) continue;
                var m = m0;
                try { var a = m0.Army; if (a != null && a.LeaderParty != null) m = a.LeaderParty; } catch { }
                if (m == null || !m.IsActive || !seen.Add(m)) continue;
                var v = LVOf(m);
                if (!v.Active || v.F == null || v.Cur == t || !War(k, ki, v.F, v.Fi)) continue;
                bool now = RelievesV(v, t), mem = false;
                double last;
                if (now) kr.Seen[m] = nowH;
                else mem = (kr.Seen.TryGetValue(m, out last) && nowH - last <= MemHours) || (m0 != m && kr.Seen.TryGetValue(m0, out last) && nowH - last <= MemHours);
                var e = new KE { Lead = m, Now = now, Mem = mem, LD = -1f, RD = -1f };
                var u = LiveUnit(m);
                if (u != null) { e.LD = DaysTo(u, t); e.LS = u.Str; }
                // pamietany zamiar (nie teraz) - plotka: migawka x 2; zamiar teraz - zywa droga (jak wpis Tg w ZFor)
                Unit su;
                if (mem && _snap.ByLead.TryGetValue(m, out su) && su != null)
                {
                    float d0 = DaysTo(su, t);
                    if (d0 >= 0f) { e.RD = d0 * 2f; e.RS = su.Str; }
                }
                kr.E.Add(e);
            }
            return kr.E;
        }
        private static readonly HashSet<MobileParty> _kreSeen = new HashSet<MobileParty>();

        /// <summary>Zaloga + milicja + lordowie w osadzie (te same druzyny, co obrona oblezenia gry).</summary>
        private static double GarOf(Settlement t)
        {
            double g = 0;
            try { foreach (var pb in t.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) g += PStr(pb); } catch { }
            return g;
        }

        /// <summary>Prefiks First BannerKings ArmySiegePersistencePatches.Postfix(MobileParty): odwrot 334e - BK nie trzyma armii pod oblezeniem. Nie rzuca.</summary>
        public static bool BkStickPre(MobileParty __0)
        {
            try
            {
                if (__0 == null || !StickYieldOn || !Retreating(__0)) return true;
                lock (_lk) _dStickYield++;
                return false;
            }
            catch { return true; }
        }

        /// <summary>SiegeWalls2.HoldFresh (m116): odwrot 334e nie jest trzymany jako swieze oblezenie.</summary>
        internal static bool FreshYields(MobileParty p)
        {
            if (!JudgeOn || !Retreating(p)) return false;
            lock (_lk) _dFreshYield++;
            return true;
        }

        // ================================================================ poza obozem (z SiegeWalls2.ThinkPost, Last)
        /// <summary>SiegeWalls2.ThinkPost dla druzyn poza obozem oblezenia: rabunek przerwany, armie lupia, Zelazni oblegaja wybrzeze. Nie rzuca.</summary>
        internal static void ThinkField(MobileParty party, PartyThinkParams partyThinkParams)
        {
            try
            {
                if (party == null || partyThinkParams == null || party.IsMainParty) return;
                if (!party.IsLordParty || !(party.MapFaction is Kingdom)) return;
                if (!PowerOn && !BreakOn) return;
                if (WithPlayer(party) || partyThinkParams.DoNotChangeBehavior) return;
                if (party.BesiegerCamp != null) return;
                long t0 = Ts();
                int g0 = GcN();
                _modelT = 0; _tWait = 0;
                var me = party.MapEvent;
                if (me != null) { if (BreakOn && me.IsRaid && me.AttackerSide != null && me.AttackerSide.LeaderParty == party.Party) RaidBreak(party, me, partyThinkParams); }
                else
                {
                    if (ArmyRaidOn && party.Army != null && party.Army.LeaderParty == party) ArmyRaid(party, partyThinkParams);
                    if (PowerOn && WarFront334.IronOn && party.MapFaction.StringId == WarFront334.IronId) IronSiege(party, partyThinkParams);
                }
                long mt = _modelT, wt = _tWait;
                double ms = TMs(Ts() - t0 - mt - wt);   // 334f: model gry (ocena celu, droga) poza kosztem 334e - osobny licznik; 340: czekanie na zamek tez poza
                bool gc = GcN() != g0;
                if (ms > 0.05 || mt > 0 || wt > 0) lock (_lk) { if (ms > 0.05) Cost(ms, gc, PField); _dModelT += mt; _dWaitT += wt; }
            }
            catch (Exception e) { Stumble("ThinkField", e); }
        }

        private static int Best(PartyThinkParams pt, out float best)
        {
            best = float.MinValue; int bi = -1;
            var s = pt.AIBehaviorScores;
            for (int i = 0; i < s.Count; i++) if (s[i].Item2 > best) { best = s[i].Item2; bi = i; }
            return bi;
        }

        private static float PStr(PartyBase pb) { try { return pb != null ? pb.EstimatedStrength : 0f; } catch { return 0f; } }

        internal const double MemHours = 12.0;   // 334f: 2 x okres mysli gry (DefaultThinkingPeriodInHours 6, jak FarCacheHours 274)

        /// <summary>334f (L14): licznik ODSTAP z ocen dowodcy. Nowe ODSTAP liczy sie, gdy od poprzedniego minela co najmniej godzina (osobna mysl); ostatnie ODSTAP
        /// sprzed ponad 12 h - od nowa; KONTYNUUJ / GLOD (odsieczy brak albo glod) zeruja; SRODEK i NIE-ZDAZY (migotanie) - licznik zostaje, nie rosnie.</summary>
        internal static int BadStep(int bad, double badH, int state, double nowH, out double newBadH)
        {
            newBadH = badH;
            if (badH >= 0 && nowH - badH > MemHours) bad = 0;
            if (state == 4) { if (bad == 0 || nowH - badH >= 1.0) { bad++; newBadH = nowH; } }   // 334g: ODSTAP w tej samej godzinie nie przesuwa badH
            else if (state == 1 || state == 5) { bad = 0; newBadH = -1; }
            return bad;
        }

        /// <summary>Pozostaly czas oblezenia (doby) z modelu gry i bramy OB1 (334f, L14): przygotowania (reszta / tempo budowy modelu gry), potem taran i wieza
        /// (reszta / tempo budowy) albo droga murow (tempo burzenia), potem szturm z szansa OB1 na mysl (1 / szansa godzin, co najmniej pol doby). Szturm
        /// niemozliwy (s &lt;= 1) albo brak mierzalnej drogi - HorizonDays: oblezenie sie nie konczy, liczy sie kazda odsiecz w horyzoncie. Bez podlogi 1.0 (334e
        /// przyklejal "pozostalo 1.00" na wiele dob). Wyjatek modelu - stara srednia gry od startu. Wolajacy trzyma _lk.</summary>
        private static float TimeLeft(BesiegerCamp camp, SiegeEvent ev, Settlement st, CampRec r)
        {
            double el = ev.SiegeStartTime.ElapsedDaysUntilNow;
            try
            {
                double wall = 1.0;
                try { wall = SiegeWalls.WallHpMin(st); } catch { }
                if (wall <= 0.0) return 0.5f;   // wyrwa - szturm najblizszy
                var sem = Campaign.Current.Models.SiegeEventModel;
                var se = camp.SiegeEngines;
                double nowH = CampaignTime.Now.ToHours;
                double prepH = 0;
                bool prepared = camp.IsPreparationComplete;
                if (!prepared)
                {
                    float p = 0f;
                    if (se != null && se.SiegePreparations != null) p = se.SiegePreparations.Progress;
                    float rate = sem.GetConstructionProgressPerHour(DefaultSiegeEngineTypes.Preparations, ev, camp);
                    if (!(rate > 0f)) return HorizonDays;
                    prepH = Math.Max(0.0, 1.0 - p) / rate;
                }
                var m = SiegeWalls.Measure(camp);
                if (m == null) return HorizonDays;
                // OB1: szturm dopiero przy s > 1. 334g (W5): HorizonDays tylko przy gotowych machinach / wyrwie - przed gotowoscia s rosnie z budowa (Adv maleje),
                // wiec liczymy przygotowania + machiny + szturm
                if (m.Ready && !m.Empty && !(m.S > 1.0)) return HorizonDays;
                double stageH = 0;
                if (!m.Ready)
                {
                    // droga machin: brakujacy taran / wieza - reszta budowy w tempie modelu gry (budowane rownolegle - dluzsza z brakujacych)
                    double eng = 0; bool engOk = true;
                    if (!m.Ram) { double x = EngineHours(sem, ev, camp, se, DefaultSiegeEngineTypes.Ram, DefaultSiegeEngineTypes.ImprovedRam); if (x < 0) engOk = false; else eng = Math.Max(eng, x); }
                    if (!m.Tower) { double x = EngineHours(sem, ev, camp, se, DefaultSiegeEngineTypes.SiegeTower, null); if (x < 0) engOk = false; else eng = Math.Max(eng, x); }
                    // droga murow: tempo burzenia od konca przygotowan (ostrzal gry)
                    double wallH = -1;
                    if (prepared)
                    {
                        if (r.PrepH < 0) { r.PrepH = nowH; r.Wall0 = wall; }
                        double wEl = nowH - r.PrepH, lost = r.Wall0 - wall;
                        if (lost > 0.02 && wEl >= 6.0) wallH = wEl * wall / lost;
                    }
                    if (engOk && wallH >= 0) stageH = Math.Min(eng, wallH);
                    else if (engOk) stageH = eng;
                    else if (wallH >= 0) stageH = wallH;
                    else return HorizonDays;
                }
                double assaultH = Math.Max(12.0, 1.0 / Math.Max(m.POurs, 0.01));   // szansa OB1 na mysl (godzina)
                if (m.Empty || !(m.S > 1.0)) assaultH = 12.0;   // 334g: przed gotowoscia szansy po budowie nie znamy - najkrotszy szturm (mniej odwrotow)
                double days = (prepH + stageH + assaultH) / CampaignTime.HoursInDay;
                return (float)Math.Max(0.5, Math.Min(HorizonDays, days));
            }
            catch (Exception e)
            {
                Stumble("TimeLeft", e);
                return Math.Max(0.5f, SiegeDays - (float)el);
            }
        }

        /// <summary>Godziny do konca budowy machiny typu a (albo b): reszta postepu rozstawionej / cala budowa nierozstawionej, tempo modelu gry. -1 - tempo 0.</summary>
        private static double EngineHours(SiegeEventModel sem, SiegeEvent ev, BesiegerCamp camp, SiegeEvent.SiegeEnginesContainer se, SiegeEngineType a, SiegeEngineType b)
        {
            float prog = 0f; SiegeEngineType typ = a;
            if (se != null && se.DeployedMeleeSiegeEngines != null)
                foreach (var x in se.DeployedMeleeSiegeEngines)
                {
                    if (x == null || x.SiegeEngine == null || x.IsConstructed) continue;
                    if (x.SiegeEngine == a || (b != null && x.SiegeEngine == b)) { if (x.Progress > prog) { prog = x.Progress; typ = x.SiegeEngine; } }
                }
            float rate = sem.GetConstructionProgressPerHour(typ, ev, camp);
            if (!(rate > 0f)) return -1;
            return Math.Max(0.0, 1.0 - prog) / rate;
        }

        /// <summary>Z SiegeWalls2.ThinkPost (Last): sad oblezenia w toku. Zwraca stan (JudgeRule) albo -1 (bez sadu); 4 - TYLKO gdy odwrot wykonany teraz
        /// (m116 Fresh wtedy nie trzyma). Nie rzuca.</summary>
        internal static int Judge(MobileParty party, BesiegerCamp camp, SiegeEvent ev, Settlement st, PartyThinkParams pt, bool skip)
        {
            try
            {
                if (skip || party == null || camp == null || ev == null || st == null || pt == null) return -1;
                if (!JudgeOn || party.IsMainParty || WithPlayer(party) || pt.DoNotChangeBehavior) return -1;
                if (!party.IsLordParty || !(party.MapFaction is Kingdom)) return -1;
                if (party.AttachedTo != null || (party.Army != null && party.Army.LeaderParty != party)) return -1;
                if (camp.LeaderParty != party) return -1;   // decyduje wodz obozu
                if (!st.IsFortification || st.Town == null) return -1;
                if (party.MapEvent != null || party.IsCurrentlyAtSea) return -1;
                try { if (party.Ai != null && party.Ai.DoNotMakeNewDecisions) return -1; } catch { }
                try { if (Undead.Party(party)) return -1; } catch { }
                long t0 = Ts(), wait;
                int g0 = GcN();
                int res = JudgeCore(party, camp, ev, st, pt, out wait);
                lock (_lk) { _dWaitT += wait; Cost(TMs(Ts() - t0 - wait), GcN() != g0, PJudge); }   // 334g (D2): czekanie na zamek poza kosztem, jak ScorePost
                return res;
            }
            catch (Exception e) { Stumble("Judge", e); return -1; }
        }

        private static int JudgeCore(MobileParty party, BesiegerCamp camp, SiegeEvent ev, Settlement st, PartyThinkParams pt, out long wait)
        {
            wait = 0;
            var k = party.MapFaction;
            double camp0 = 0;
            foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) camp0 += PStr(pb);
            double gar = 0;
            foreach (var pb in st.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) gar += PStr(pb);
            float food = SiegeWalls2.FoodDays(party);
            int state, bad; double q, qr = 0, relief = 0, campS = camp0, allyIn = 0; float arrival = float.MaxValue, tLeft; int nGo = 0, nScout = 0, nMem = 0;
            long w0 = Ts();
            lock (_lk)
            {
                wait = Ts() - w0;
                if (!SnapFresh) return -1;
                _dJudge++;
                CampRec r;
                if (!_camps.TryGetValue(ev, out r)) { r = new CampRec(); _camps[ev] = r; }
                // 334g (W3): pozostaly czas raz na godzine zegara (Measure + model budowy - drogie; mury i postep zmieniaja sie wolniej)
                int th = NowH;
                if (r.TLeftH == th) tLeft = r.TLeft;
                else { tLeft = TimeLeft(camp, ev, st, r); r.TLeft = tLeft; r.TLeftH = th; }
                var l = ListOf(st);
                var sc = ScoutOf(party);
                var rel = new List<KeyValuePair<float, float>>();
                var allies = new List<KeyValuePair<float, float>>();
                var seen = new HashSet<MobileParty>();
                var newDist = new Dictionary<MobileParty, float>();
                var relL = new List<MobileParty>();
                double nowH = CampaignTime.Now.ToHours;
                Action<MobileParty> consider = lead =>
                {
                    if (lead == null || !seen.Add(lead) || lead == party) return;
                    var v = LVOf(lead);
                    if (!v.Active || v.Cur == st || lead.BesiegerCamp == camp) return;
                    var lf = v.F;
                    if (lf == null) return;
                    if (!FactionManager.IsAtWarAgainstFaction(k, lf))
                    {
                        // nasi w drodze na te osade (dolacza do obozu)
                        if (!SameSide(k, lf) || !GoingToV(v, st)) return;
                        var ua = LiveUnit(lead);
                        if (ua == null) return;
                        float da = DaysTo(ua, st);
                        if (da >= 0f && da <= HorizonDays) allies.Add(new KeyValuePair<float, float>(da, ua.Str));
                        return;
                    }
                    if (!Foe(k, lf, st)) return;   // trzecia strona - nie odsiecz
                    bool intent = RelievesV(v, st);
                    bool spotted = sc.Contains(lead);
                    KeyValuePair<double, float> mv = default(KeyValuePair<double, float>);
                    // 334f (L14): pamiec dowodcy - zamiar obrony zmienia sie przy kazdej mysli lorda ("miga"); widziany zamiar pamietamy 12 h (2 x okres mysli gry)
                    bool mem = !intent && r.Mem.TryGetValue(lead, out mv) && nowH - mv.Key <= MemHours;
                    if (!intent && !spotted && !mem) return;
                    var u = LiveUnit(lead);
                    if (u == null) return;
                    float dd = u.Pos.Distance(st.GetPosition2D);
                    newDist[lead] = dd;
                    bool memUsed = false;
                    if (intent) r.Mem[lead] = new KeyValuePair<double, float>(nowH, dd);
                    else
                    {
                        bool ok = false;
                        if (mem && dd <= mv.Value + 0.5f * u.Land) { ok = true; memUsed = true; }   // pamietany zamiar: liczy sie, dopoki sie nie oddala (pol godziny drogi zapasu)
                        if (!ok && spotted)
                        {
                            float prev;
                            ok = r.Dist.TryGetValue(lead, out prev) && dd < prev - 0.01f;   // widziany zwiadem: tylko gdy sie zbliza (stoi albo sie oddala - nie wywoluje odwrotu)
                        }
                        if (!ok) return;
                    }
                    float d = DaysTo(u, st);
                    if (d < 0f || d > HorizonDays) return;
                    rel.Add(new KeyValuePair<float, float>(d, u.Str));
                    relL.Add(lead);
                    if (intent) nGo++; else if (memUsed) nMem++; else nScout++;
                };
                for (int i = 0; i < l.E.Count; i++) consider(l.E[i].U.Lead);
                foreach (var m in sc) consider(m);
                if (r.Mem.Count > 0)
                {
                    var keys = new List<MobileParty>(r.Mem.Keys);
                    foreach (var m in keys) consider(m);
                    foreach (var m in keys)
                    {
                        KeyValuePair<double, float> mv;
                        if (!r.Mem.TryGetValue(m, out mv)) continue;
                        bool drop = m == null || !m.IsActive || nowH - mv.Key > MemHours;
                        if (!drop) { var mf = m.MapFaction; drop = mf == null || !FactionManager.IsAtWarAgainstFaction(k, mf); }
                        if (drop) r.Mem.Remove(m);
                    }
                }
                r.Dist.Clear(); foreach (var kv in newDist) r.Dist[kv.Key] = kv.Value;
                r.RelLeads.Clear(); r.RelLeads.AddRange(relL);
                rel.Sort((a, b) => a.Key.CompareTo(b.Key));
                foreach (var kv in rel) relief += kv.Value;
                // nasi w drodze: doliczeni, jesli dojda przed przewaga wroga (bez przewagi - przed koncem oblezenia)
                float a0 = Arrival(rel, GarCap(gar, camp0), camp0);
                float lim = a0 == float.MaxValue ? tLeft : a0;
                foreach (var kv in allies) if (kv.Key <= lim) allyIn += kv.Value;
                campS = camp0 + allyIn;
                arrival = Arrival(rel, GarCap(gar, campS), campS);
                state = JudgeRule(campS, relief, gar, arrival, tLeft, food, out q);
                qr = Ratio(relief + GarCap(gar, campS), campS);
                int h = NowH;
                // 334f (L14): histereza z ocen dowodcy (mysl obozu co ok. 3 h), nie z dwoch kolejnych godzin zegara - ta nie spelniala sie nigdy
                r.Bad = BadStep(r.Bad, r.BadH, state, nowH, out r.BadH);
                bad = r.Bad;
                r.Hour = h; r.Q = q;
                if (state >= 0 && state < _dStates.Length) _dStates[state]++;
                bool change = r.State != state;
                r.State = state;
                if ((change || r.SaidDay != Today) && S != null && S.LogEnabled && _dSiegeLines < 60)
                {
                    r.SaidDay = Today; _dSiegeLines++;
                    Log.Info(Tag + "oblezenie | dzien " + Today + " | " + st.StringId + " (" + Nm(st) + ") | " + Nm(party) + " (" + Id(k) + ") | oboz " + F0(campS) + (allyIn > 0 ? " (w drodze " + F0(allyIn) + ")" : "")
                             + " | odsiecz " + F0(relief) + " (idzie " + nGo + ", pamiec " + nMem + ", zwiad " + nScout + ", zaloga " + F0(gar) + ") | q_obl " + F2(q) + " | q_odw " + F2(qr) + " | przyjazd " + (arrival == float.MaxValue ? "-" : F2(arrival))
                             + " | pozostalo " + F2(tLeft) + " | jedzenie " + (food >= 999f ? "-" : F2(food)) + " | stan " + StateName[state] + (state == 4 ? " (" + bad + "/2 mysli)" : "") + ".");
                }
            }
            var scores = pt.AIBehaviorScores;
            if (scores == null) return state;
            float best; int bi = Best(pt, out best);
            int si = -1;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && !e.Item1.WillGatherArmy && ReferenceEquals(e.Item1.Party, st) && (si < 0 || e.Item2 > scores[si].Item2)) si = i;
            }
            if (state == 1 || state == 3)
            {
                try { if (LordEscort.Guard335Busy(party)) return state; } catch { }   // wodz chroniony 335 - decyduje ochrona
                if (si >= 0 && scores[si].Item2 <= 0f) { lock (_lk) _dKeepVeto++; return state; }   // weto (teatr, 274, 326b, zakaz m116) albo rozejm - nie wskrzeszamy
                if (si < 0)
                {
                    string why;
                    var kk = k as Kingdom;
                    if (st.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(k, st.MapFaction) || (kk != null && WarFront334.VetoedFor334e(kk, st, out why)))
                    { lock (_lk) _dKeepVeto++; return state; }
                }
                if (bi >= 0)
                {
                    var bd = scores[bi].Item1;
                    if (bd.AiBehavior == AiBehavior.DefendSettlement) { var ds = bd.Party as Settlement; if (ds != null && ds.IsFortification && ds.MapFaction == party.MapFaction) return state; }   // OB5 pierwsza
                    if (ReferenceEquals(bd.Party, st) && bd.AiBehavior == AiBehavior.BesiegeSettlement && best >= HoldFloor) { lock (_lk) _dKeep++; return state; }
                }
                float v = Math.Max((bi >= 0 ? best : 0f) * 1.05f + 0.01f, HoldFloor);
                if (si >= 0) { var d = scores[si].Item1; pt.SetBehaviorScore(in d, v); }
                else pt.AddBehaviorScore((new AIBehaviorData(st, AiBehavior.BesiegeSettlement, MobileParty.NavigationType.Default, false, false, false), v));
                lock (_lk) _dKeep++;
                return state;
            }
            if (state == 2) { lock (_lk) _dMid++; return 2; }
            if (state != 4) return state;
            lock (_lk) _dRetreatCand++;
            if (bad < 2) return 2;   // kandydat - 2 osobne mysli w 12 h
            return Retreat(party, camp, st, pt, best, q, qr, relief, gar, campS, arrival) ? 4 : 2;
        }

        /// <summary>Odstapienie: tylko gdy ucieczka zdazy przed przewaga. true - wykonane.</summary>
        private static bool Retreat(MobileParty party, BesiegerCamp camp, Settlement st, PartyThinkParams pt, float best, double q, double qr, double relief, double gar, double campS, float arrival)
        {
            // dokad: wlasna nieoblegana twierdza dalej od odsieczy (najblizszy nadchodzacy wrog) niz oboz, do ktorej dojdziemy przed przyjazdem przewagi
            Vec2 cp = party.GetPosition2D;
            Vec2 foe = st.GetPosition2D; float foeD = float.MaxValue;
            var cands = new List<KeyValuePair<Settlement, float>>();
            lock (_lk)
            {
                CampRec r;
                if (_camps.TryGetValue(camp.SiegeEvent, out r))
                    foreach (var kv in r.Dist) { var u = LiveUnit(kv.Key); if (u != null && kv.Value < foeD) { foeD = kv.Value; foe = u.Pos; } }
            }
            float campFoe = cp.Distance(foe);
            foreach (var s in party.MapFaction.Settlements)
            {
                if (s == null || !s.IsFortification || s.IsUnderSiege) continue;
                var sp = s.GetPosition2D;
                if (sp.Distance(foe) <= campFoe) continue;
                cands.Add(new KeyValuePair<Settlement, float>(s, sp.DistanceSquared(cp)));
            }
            cands.Sort((a, b) => a.Value.CompareTo(b.Value));
            Settlement dest = null; float destDays = -1f;
            lock (_lk)
            {
                for (int i = 0; i < cands.Count && i < 6; i++)
                {
                    float our = OurDays(party, cands[i].Key);
                    if (our >= 0f && our < arrival) { dest = cands[i].Key; destDays = our; break; }
                }
            }
            MobileParty.NavigationType nav = MobileParty.NavigationType.None; float dist; bool fromPort = false;
            if (dest != null)
            {
                Helpers.AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, dest, false, out nav, out dist, out fromPort);
                if (nav == MobileParty.NavigationType.None) dest = null;
            }
            // bez twierdzy, do ktorej zdazymy: odstepujemy tylko przy zapasie co najmniej pol doby (223 prowadzi ucieczke w pole); inaczej zostajemy w obozie
            bool go = dest != null || arrival >= 0.5f;
            if (!go)
            {
                lock (_lk)
                {
                    _dStay++;
                    CampRec r;
                    if (_camps.TryGetValue(camp.SiegeEvent, out r) && r.StaySaid != Today && S != null && S.LogEnabled)
                    {
                        r.StaySaid = Today;
                        Log.Info(Tag + "zostaje w obozie | dzien " + Today + " | " + st.StringId + " (" + Nm(st) + ") | " + Nm(party) + " (" + Id(party.MapFaction) + ") | oboz " + F0(campS) + " vs odsiecz "
                                 + F0(relief) + "+zaloga " + F0(gar) + " (q_obl " + F2(q) + ", przyjazd " + (arrival == float.MaxValue ? "-" : F2(arrival)) + ") | ucieczka nie zdazy - bitwa przy obozie.");
                    }
                }
                return false;
            }
            var scores = pt.AIBehaviorScores;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if ((e.Item1.AiBehavior == AiBehavior.BesiegeSettlement || e.Item1.AiBehavior == AiBehavior.AssaultSettlement) && ReferenceEquals(e.Item1.Party, st) && e.Item2 > -1f)
                { var d = e.Item1; pt.SetBehaviorScore(in d, -1f); }
            }
            if (dest != null)
            {
                float v = Math.Max(best, 0.01f) * 1.05f;
                pt.AddBehaviorScore((new AIBehaviorData(dest, AiBehavior.GoToSettlement, nav, false, fromPort, false), v));
            }
            double kUntil = CampaignTime.Now.ToDays + Math.Max(1.0, Math.Min((double)HorizonDays, arrival) + 0.5);
            lock (_lk)
            {
                double now = CampaignTime.Now.ToHours;
                _retreat[party] = now + 24.0;
                _block[new PS(party, st)] = CampaignTime.Now.ToDays + 1.0;
                // 334g: blokada krolestwa do przyjazdu odsieczy + pol doby (start tylko przy mierze odwrotu <= 1.0 - KBlocked)
                var kk = new SK(party.MapFaction, st);
                double prevK;
                if (!_kblock.TryGetValue(kk, out prevK) || prevK < kUntil) _kblock[kk] = kUntil;
                // 334g (D4 logiki): nowy wodz obozu tego samego oblezenia - znow 2 mysli
                CampRec rr;
                if (_camps.TryGetValue(camp.SiegeEvent, out rr)) { rr.Bad = 0; rr.BadH = -1; }
                // 340 (c): pamiec odsieczy - blokada trwa, dopoki ona jest w zasiegu i silniejsza (KBlocked)
                if (S != null && S.LocalReliefHold340 && rr != null && rr.RelLeads.Count > 0)
                {
                    var krr = new KRel { Since = CampaignTime.Now.ToDays };
                    krr.L.AddRange(rr.RelLeads);
                    foreach (var rl in rr.RelLeads) if (rl != null) krr.Seen[rl] = now;   // policzone w sadzie odwrotu - zamiar od teraz (pamiec MemHours)
                    _krel[kk] = krr;
                }
                _dRetreat++;
                _sRetreat++;
                if (_dRetreatNotes.Count < 6) _dRetreatNotes.Add(Nm(st) + " q " + F2(q));
                if (S != null && S.LogEnabled)
                    Log.Info(Tag + "odstapienie | dzien " + Today + " | " + st.StringId + " (" + Nm(st) + ") | " + Nm(party) + " (" + Id(party.MapFaction) + ") | oboz " + F0(campS) + " vs odsiecz "
                             + F0(relief) + "+zaloga " + F0(gar) + " (q_obl " + F2(q) + ", przyjazd " + (arrival == float.MaxValue ? "-" : F2(arrival)) + ") | q_odw " + F2(qr) + " | dokad " + (dest != null ? dest.StringId + " (" + Nm(dest) + ", " + F2(destDays) + " doby)" : "- (223)")
                             + " | ucieczka zdazy " + (dest != null ? "TAK" : "pole") + " | blokada do doby " + F2(CampaignTime.Now.ToDays + 1.0) + ", krolestwa do " + F2(kUntil)
                             + (_krel.ContainsKey(kk) ? " i dalej, dopoki odsiecz (" + _krel[kk].L.Count + " druzyn) w zasiegu (340)" : "") + ".");
            }
            return true;
        }

        /// <summary>Rabunek w toku: przerwanie przed silniejszym, ktory zdazy (rozdz. 4.3).</summary>
        private static void RaidBreak(MobileParty party, MapEvent me, PartyThinkParams pt)
        {
            var v = me.MapEventSettlement;
            if (v == null || !v.IsVillage || party.IsCurrentlyAtSea) return;
            int men = 0; double ours = 0;
            foreach (var mep in me.AttackerSide.Parties) { if (mep == null || mep.Party == null) continue; ours += PStr(mep.Party); men += mep.Party.NumberOfHealthyMembers; }
            float left = RaidDays(v.SettlementHitPoints, men);
            double z; float first; double ally;
            int cnt;
            long w0 = Ts();
            lock (_lk)
            {
                _tWait += Ts() - w0;
                if (!SnapFresh) return;
                z = ZFor(v, party.MapFaction, party, left, out first, out ally);
                double qq = Ratio(z, ours + ally);
                bool bad = qq > AbandonQ;
                int[] r;
                if (!_raidBad.TryGetValue(me, out r)) { r = new int[] { -1, 0 }; _raidBad[me] = r; }
                int h = NowH;
                if (!bad) r[1] = 0;
                else if (r[0] == h - 1) r[1]++;
                else if (r[0] != h || r[1] == 0) r[1] = 1;
                r[0] = h; cnt = r[1];
            }
            double q = Ratio(z, ours + ally);
            if (cnt < 2) return;
            var scores = pt.AIBehaviorScores;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.RaidSettlement && ReferenceEquals(e.Item1.Party, v) && e.Item2 > -1f) { var d = e.Item1; pt.SetBehaviorScore(in d, -1f); }
            }
            lock (_lk)
            {
                _dBreak++;
                _block[new PS(party, v)] = CampaignTime.Now.ToDays + 1.0;
                if (S != null && S.LogEnabled && _dBreak <= 30)
                    Log.Info(Tag + "rabunek przerwany | dzien " + Today + " | " + v.StringId + " (" + Nm(v) + ") | " + Nm(party) + " (" + Id(party.MapFaction) + ") | nasi " + F0(ours) + " | wrog " + F0(z)
                             + " | q " + F2(q) + " | przyjazd " + (first == float.MaxValue ? "-" : F2(first)) + " | pozostalo " + F2(left) + ".");
            }
        }

        // ================================================================ Zelazni: oblezenia wybrzeza (dopisek testu 334c: 83 doby bez armii i bez opcji oblezenia)
        // 334f (B334-12): wyjscia liczone (linia koszt), cele kanonu Wysp (Polnoc wlasciwa - kultura battania) do Krwawych Godow, odleglosc skorygowana gry (morze)
        // zamiast linii prostej, limit 4 liczy tylko cele z q <= 1.0 (osobny limit 8 ocen q), opcja gry poza teatrem kanonu nie wylacza naszej oferty.
        // 342 (B1): 10 - czlonek cudzej armii (cel wybiera wodz armii - jego mysl ma galaz "wodz armii"); nie liczy sie jako "bez zbiorki"
        private static readonly int[] _dIronX = new int[11];   // 0 zajeci (bitwa/oboz/morze), 1 bez wodza i zbiorki, 2 odpoczynek, 3 opcja gry na cel kanonu, 4 bez kandydatow, 5 q > 1, 6 bez drogi / poza zasiegiem, 7 model 0, 8 oferta, 9 weto (334g), 10 w armii (342)
        internal static readonly string[] IronXName = { "zajeci", "bez zbiorki", "odpoczynek", "opcja gry kanonu", "bez kandydatow", "q>1", "bez drogi", "model 0", "oferta", "weto", "w armii - decyduje wodz" };
        private static double _dIronMinQ = -1; private static string _dIronMinT = "-"; private static double _dIronMinStr;
        private sealed class IronNav { public int Hour; public readonly Dictionary<Settlement, KeyValuePair<float, KeyValuePair<MobileParty.NavigationType, bool>>> D = new Dictionary<Settlement, KeyValuePair<float, KeyValuePair<MobileParty.NavigationType, bool>>>(); }
        private static readonly Dictionary<MobileParty, IronNav> _ironNav = new Dictionary<MobileParty, IronNav>();

        private static void IronX(int i) { long w = Ts(); lock (_lk) { _tWait += Ts() - w; _dIronX[i]++; } }

        // 340 (d): powody "bez zbiorki" (warunki CanLordCreateArmy gry, czytane - nic nie zmieniamy) i dzwignie kanonu Wysp
        // 342: "czlonek armii" (0) zostaje tylko jako zabezpieczenie - czlonkowie wychodza wczesniej (IronX 10); "nie glowa rodu" (1) tylko przy wylaczonej regule 342
        // (AnyLordArmy342), z regula: 11 - limit wplywu rodu (100 x armie rodu + 1), 12 - czeka na kolejke rodu (jedna druzyna rodu na godzine)
        private static readonly int[] _dIronWhy = new int[13];
        internal static readonly string[] IronWhyName = { "czlonek armii", "nie glowa rodu", "wplyw<=100", "jedzenie", "maly oddzial", "najem", "kandydaci 0", "inne", "prog 1000", "limit 0.7", "sito SCA", "limit wplywu rodu", "kolejka rodu" };
        private static int _dIronMuster, _dIronKeepSea, _dIronPrio, _dIronMusterLines, _dIronMusterFail, _dIronMusterBan;
        private static readonly Dictionary<MobileParty, int> _musterDay = new Dictionary<MobileParty, int>();
        // 340 (recenzja): zbiorka w porcie - (port, doba wyslania); w porcie bez czlonkow gry -> doba nieudanej proby (bez ponownej oferty przez 2 doby - bez petli port-morze-port)
        private static readonly Dictionary<MobileParty, KeyValuePair<Settlement, int>> _musterTry = new Dictionary<MobileParty, KeyValuePair<Settlement, int>>();
        private static readonly Dictionary<MobileParty, int> _musterFail = new Dictionary<MobileParty, int>();
        private const int MusterFailDays = 2;
        private static void IronWhy(int i) { long w = Ts(); lock (_lk) { _tWait += Ts() - w; _dIronWhy[i]++; } }

        /// <summary>340 (d): dlaczego gra nie dala zbiorki (CanLordCreateArmy - te same warunki, tylko odczyt; morze liczone osobno jako "zajeci").</summary>
        private static int IronWhyOf(MobileParty party)
        {
            try
            {
                if (party.Army != null) return 0;
                var h = party.LeaderHero; var c = h != null ? h.Clan : null;
                if (h == null || c == null) return 7;
                if (!AnyLordArmy342.MayLead(party)) return AnyLordArmy342.On && c.Leader != h ? (c.Influence > 100f ? 11 : 2) : 1;
                if (!(c.Influence > 100f)) return 2;
                if (!AnyLordArmy342.LeadsNow(party)) return 12;
                if (c.IsUnderMercenaryService) return 5;
                if (!((float)party.GetNumDaysForFoodToLast() > Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege)) return 3;
                if (!(party.PartySizeRatio > Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy)) return 4;
                var k = party.MapFaction as Kingdom;
                if (k == null) return 7;
                float est; int n, why;
                long m0 = Ts();
                try { why = IronCands(party, k, null, out est, out n); } finally { _modelT += Ts() - m0; }
                return why >= 0 ? why : 7;
            }
            catch { return 7; }
        }

        // 342: warunek wodza zbiorki - AnyLordArmy342.MayLead / LeadsNow (glowa rodu, pierwsza druzyna rodu bez glowy w polu, regula 342 "kazdy lord" z limitem wplywu i kolejka rodu)

        /// <summary>340 (recenzja): kandydaci zbiorki - te same sita co CanLordCreateArmy gry (DefaultArmyManagementCalculationModel: wolne druzyny lordow, nie wladca, mysl, oblezenie,
        /// jedzenie, liczebnosc, CanLeadParty, tratwa, droga gry &lt; MaximumDistanceToCallToArmy, kolejnosc gry, limit ceil(0.7 x druzyn - w armiach), prog sily oblezenia 1000),
        /// przy SCA - jego sito (wodz 22 zdrowych i 80 sily, czlonek 10 / 35, co najmniej 2, razem 260). from - port zbiorki (null - miejsce druzyny). Zwraca -1 (zbiorka
        /// mozliwa) albo powod (IronWhyName); est / n - sila (miara ZFor) i liczba kandydatow.</summary>
        private static int IronCands(MobileParty party, Kingdom k, Settlement from, out float est, out int n)
        {
            est = PStr(party.Party); n = 0;
            var am = Campaign.Current.Models.ArmyManagementCalculationModel;
            float sizeMin = am.AIMobilePartySizeRatioToCallToArmy, foodMin = am.MinimumNeededFoodInDaysToCallToArmy, reach = am.MaximumDistanceToCallToArmy;
            var pos = from != null ? from.GetPosition2D : party.GetPosition2D;
            var list = new List<KeyValuePair<MobileParty, float>>();
            foreach (var wp in k.WarPartyComponents)
            {
                var m = wp != null ? wp.MobileParty : null;
                if (m == null || m == party || !m.IsActive || !m.IsLordParty || m.Army != null || m.IsMainParty) continue;
                var lh = m.LeaderHero;
                if (lh == null || lh == lh.MapFaction.Leader) continue;
                if (m.Ai.DoNotMakeNewDecisions || (m.CurrentSettlement != null && m.CurrentSettlement.SiegeEvent != null) || m.IsDisbanding) continue;
                if (!((float)m.GetNumDaysForFoodToLast() > foodMin) || !(m.PartySizeRatio > sizeMin) || !lh.CanLeadParty() || m.IsInRaftState || m.MapEvent != null || m.BesiegedSettlement != null) continue;
                if (m.GetPosition2D.DistanceSquared(pos) >= reach * reach) continue;   // droga gry nigdy krotsza niz linia prosta
                float d;
                if (from != null) d = Helpers.DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(m, from, m.NavigationCapability);
                else { float lr; d = Helpers.DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(m, party, m.NavigationCapability, out lr); }
                if (!(d < reach)) continue;
                int cost = am.CalculatePartyInfluenceCost(party, m);
                var ro = m.Party.MemberRoster;
                float hr = ro.TotalManCount > 0 ? 1f - (float)ro.TotalWounded / ro.TotalManCount : 0f;
                list.Add(new KeyValuePair<MobileParty, float>(m, m.Party.EstimatedStrength / (cost + 0.1f) * hr));
            }
            list.Sort((a, b) => a.Value.CompareTo(b.Value));   // kolejnosc gry (OrderByQ rosnaco)
            int inArmies = 0;
            foreach (var a in k.Armies) if (a != null) inArmies += a.Parties.Count;
            int lim = (int)Math.Ceiling(k.WarPartyComponents.Count * 0.7f - inArmies);
            if (lim <= 0) return 9;
            if (lim < list.Count) list.RemoveRange(lim, list.Count - lim);
            if (list.Count == 0) return 6;
            if (k.Settlements.Count > 0)
            {
                float cs = party.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                foreach (var kv in list) cs += kv.Key.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                if (cs < 1000f) return 8;
            }
            if (_wSca == "TAK")
            {
                // SCA (StrategicArmyManagementModel): IsRecoveredEnoughToLeadArmy / ToJoinArmy, co najmniej 2 czlonkow, sila razem 260
                if (party.MemberRoster.TotalHealthyCount < 22 || party.Party.EstimatedStrength < 80f) return 10;
                list.RemoveAll(kv => kv.Key.MemberRoster.TotalHealthyCount < 10 || kv.Key.Party.EstimatedStrength < 35f);
                float tot = party.Party.EstimatedStrength;
                foreach (var kv in list) tot += kv.Key.Party.EstimatedStrength;
                if (list.Count < 2 || tot < 260f) return 10;
            }
            foreach (var kv in list) { est += PStr(kv.Key.Party); n++; }
            return -1;
        }

        /// <summary>342 (Z1, Jeff 06.11): cele kanonu Wysp na wybrzezu Polnocy - Moat Cailin (castle_B1), Deepwood Motte (ROT_town2), Torrhen's Square (castle_S8),
        /// Stony Shore (castle_N7 The Rills - opis ROT-Map "Stony Shore, north of Blazewater Bay").</summary>
        internal static readonly string[] IronCanonIds = { "castle_B1", "ROT_town2", "castle_S8", "castle_N7" };
        private static int IronCanonIdx(Settlement st)
        {
            var id = st != null ? st.StringId : null;
            if (id == null) return -1;
            for (int i = 0; i < IronCanonIds.Length; i++) if (id == IronCanonIds[i]) return i;
            return -1;
        }
        private static bool IronCoastOn { get { var s = S; return s != null && s.IronCanonCoast342; } }

        /// <summary>Cel kanonu Wysp. 342 (klucz IronCanonCoast342): lista z ksiazki (IronCanonIds); wylaczony - jak 340: Polnoc wlasciwa (kultura battania;
        /// Settlement.Culture ROT-Map nie zmienia sie po zdobyciu). Po Krwawych Godach IronSiege bierze i tak wszystkie twierdze w wojnie.</summary>
        private static bool IronCanon(Settlement st) { return IronCoastOn ? IronCanonIdx(st) >= 0 : WarFront334.CultureOf(st) == "battania"; }

        // 342 (Z4): diagnoza celow kanonu na dobe - [0] ocen, [1] (rezerwa), [2] bez drogi gry, [3] weta, [4] blokada / bez migawki, [5] q > 1, [6] model 0,
        // [7] oferty (ten cel wybrany); q - lista q (min i mediana), best - najlepszy wynik modelu x odleglosc, dist - ostatnia droga gry
        private static readonly int[,] _dIronC = new int[4, 8];
        private static readonly List<double>[] _dIronCQ = { new List<double>(), new List<double>(), new List<double>(), new List<double>() };
        private static readonly float[] _dIronCBest = new float[4];
        private static readonly float[] _dIronCDist = { -1f, -1f, -1f, -1f };
        private static void IronC(Settlement st, int what) { int i = IronCanonIdx(st); if (i < 0) return; long w = Ts(); lock (_lk) { _tWait += Ts() - w; _dIronC[i, what]++; } }
        // 342 (C1): wyjscia zbiorki w porcie (IronMuster) - kazde wyjscie liczone
        private static readonly int[] _dIronMx = new int[13];
        internal static readonly string[] IronMxName = { "DoNotChange", "bez krolestwa", "nie wodz", "wplyw<=100", "najem", "wstrzymane", "jedzenie/liczebnosc", "brak wojny", "brak portu",
                                                          "kandydaci (powod w 340)", "brak celu kanonu q<=1", "bez drogi do portu", "odsiecz wlasnej" };
        private static void IronMx(int i) { long w = Ts(); lock (_lk) { _tWait += Ts() - w; _dIronMx[i]++; } }

        /// <summary>Gra daje opcje oblezenia tylko twierdzom sasiadujacym z wlasnymi (AiMilitaryBehavior.CalculateDistanceScoreForBesieging: udzial wlasnych sasiadow
        /// &lt; 0.1 -> 0); Wyspy nie maja sasiadow na ladzie Polnocy. Najezdzca z morza: wodz Wysp, ktory moze zebrac armie (gra dala czlonkow - CanLordCreateArmy,
        /// nie na morzu), albo wodz armii Wysp - dostaje oblezenie najlepszej z 4 twierdz krolestw w wojnie z q &lt;= 1.0 (sila przyszlej armii) bez weta (teatr Wysp,
        /// 274, 326b), do Krwawych Godow tylko Polnoc wlasciwa (kultura battania - cel kanonu); kandydaci po odleglosci skorygowanej gry (morze), wynik = model gry
        /// (Besieger, ta sila) x odleglosc jak rabunek gry. Opcja gry > 0 na cel kanonu - zostaje gra; opcja gry na inny cel - oba wyniki rywalizuja.
        /// 340 (d, klucz IronMuster340): (1) ZBIORKA Z LADU - wodz Wysp na morzu, ktory poza morzem spelnia warunki zbiorki gry (glowa rodu, wplyw &gt; 100, jedzenie, liczebnosc,
        /// nie najem), gdy Wyspy nie maja armii, sa wolne druzyny Wysp w zasiegu wezwania gry, a cel kanonu ma q &lt;= 1.0 z ich sila - idzie do najblizszego przyjaznego
        /// portu (zbiorka w porcie, potem gra sama daje czlonkow; ocena 1.05 x max(najlepsza, 0.5) jak Z1); (2) ARMIA NA MORZU trzyma cel kanonu (oblezenie celu armii
        /// z q &lt;= 1.25 - histereza biezacego celu; gra na morzu daje mu 0 - brak sasiadow); (3) PIERWSZENSTWO KANONU - cel kanonu w oknie (q &lt;= 0.8) z wynikiem
        /// modelu &gt; 0 wygrywa z rabunkami (1.05 x max(najlepszy inny, 0.5)); opcja gry na cel kanonu nie konczy juz oceny. Weta, model 0 i rozejm zostaja.</summary>
        private static void IronSiege(MobileParty party, PartyThinkParams pt)
        {
            // 342 (B1): czlonek cudzej armii nie decyduje - cel armii wybiera wodz (galaz "wodz armii" w jego mysli); to nie "bez zbiorki"
            if (party.Army != null && party.Army.LeaderParty != party) { IronX(10); return; }
            if (party.MapEvent != null || party.BesiegerCamp != null) { IronX(0); return; }
            var s340 = S;
            bool on340 = s340 != null && s340.IronMuster340;
            bool atSea = party.IsCurrentlyAtSea;
            bool lead = party.Army != null && party.Army.LeaderParty == party;
            if (atSea && !lead)
            {
                if (on340 && party.Army == null && IronMuster(party, pt)) return;
                IronX(0); return;
            }
            if (atSea && !on340) { IronX(0); return; }
            var members = pt.PossibleArmyMembersUponArmyCreation;
            bool gather = !lead && party.Army == null && members != null && members.Count > 0;
            if (!lead && !gather) { IronX(1); int iw = IronWhyOf(party); IronWhy(iw); if (on340) IronMusterFailed(party, iw); return; }
            if (lead) { try { if (ArmyRestDay.Resting(party.Army) || NightRest.FieldCampHolds(party)) { IronX(2); return; } } catch { } }
            var scores = pt.AIBehaviorScores;
            int gameCanon = -1;
            float bestOther = 0f;
            bool ownDef = false;   // 340: odsiecz wlasnej oblezonej twierdzy - pierwszenstwo kanonu jej nie przebija (OB5 / 274)
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                var gs = e.Item1.Party as Settlement;
                if (e.Item1.AiBehavior == AiBehavior.DefendSettlement && e.Item2 > 0f && gs != null && gs.MapFaction == party.MapFaction && gs.IsUnderSiege) ownDef = true;
                bool canonSiege = e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && e.Item2 > 0f && gs != null && IronCanon(gs);
                if (canonSiege) { if (gameCanon < 0 || e.Item2 > scores[gameCanon].Item2) gameCanon = i; }
                else if (e.Item2 > bestOther) bestOther = e.Item2;
            }
            if (gameCanon >= 0 && !on340) { IronX(3); return; }   // gra sama daje cel kanonu
            var k = party.MapFaction as Kingdom;
            if (k == null) { IronX(1); return; }
            if (lead && atSea && !ownDef && IronKeepSea(party, pt, k, bestOther)) return;
            bool afterRw = false;
            try { afterRw = CanonWarClock.OverNow(); } catch { }
            long ms0 = Ts();
            float strength = party.GetTotalLandStrengthWithFollowers(false);   // sila dla modelu gry (jego miara)
            if (gather) foreach (var m in members) if (m != null && m.Party != null) strength += m.Party.GetCustomStrength(TaleWorlds.Core.BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
            _modelT += Ts() - ms0;
            // 334g: sila do q ta sama miara co wrog w ZFor (PartyBase.EstimatedStrength) - nie mieszamy dwoch miar w jednym q
            float qStr = 0f;
            if (lead) { foreach (var ap in party.Army.Parties) if (ap != null && ap.IsActive && ap.Party != null) qStr += PStr(ap.Party); }
            else { qStr = PStr(party.Party); foreach (var m in members) if (m != null && m != party && m.Party != null) qStr += PStr(m.Party); }
            float range = 5f * Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
            // odleglosc skorygowana gry (morze / lad) - raz na kandydata, pamiec 2 h na druzyne (334g D4: druzyna sie przesuwa); czas modelu poza kosztem 334e
            IronNav nv;
            int nowH = NowH;
            var pos = party.GetPosition2D;
            long w0 = Ts();
            lock (_lk)
            {
                _tWait += Ts() - w0;
                if (!_ironNav.TryGetValue(party, out nv) || nowH - nv.Hour >= 2) { nv = new IronNav { Hour = nowH }; _ironNav[party] = nv; }
            }
            var cand = new List<KeyValuePair<Settlement, float>>();
            bool coast = IronCoastOn;
            float canonMin = float.MaxValue;   // 342 (Z3): najblizszy cel kanonu tej oceny (droga gry)
            long m0 = Ts();
            foreach (var f in k.FactionsAtWarWith)
            {
                if (f == null || !f.IsKingdomFaction || ScaNoBandits320.Skip(f) != 0) continue;
                foreach (var st in f.Settlements)
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.IsUnderSiege || (st.Party != null && st.Party.MapEvent != null)) continue;
                    if (!afterRw && !IronCanon(st)) continue;
                    // 342 (Z2): cel kanonu wybrzeza - bez limitu zasiegu (sa 4, droga gry morzem z ladowaniem, pamiec 2 h w _ironNav)
                    bool cc = coast && IronCanonIdx(st) >= 0;
                    if (cc) IronC(st, 0);
                    if (!cc && pos.DistanceSquared(st.GetPosition2D) > range * range) continue;   // 334g (W2): droga nigdy krotsza niz linia prosta - bez modelu dla dalekich
                    KeyValuePair<float, KeyValuePair<MobileParty.NavigationType, bool>> dv;
                    if (!nv.D.TryGetValue(st, out dv))
                    {
                        MobileParty.NavigationType nav; float dist; bool fromPort;
                        Helpers.AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, st, false, out nav, out dist, out fromPort);
                        dv = new KeyValuePair<float, KeyValuePair<MobileParty.NavigationType, bool>>(nav == MobileParty.NavigationType.None ? -1f : dist, new KeyValuePair<MobileParty.NavigationType, bool>(nav, fromPort));
                        nv.D[st] = dv;
                    }
                    if (dv.Key < 0f || (!cc && dv.Key > range)) { if (cc) IronC(st, 2); continue; }
                    if (cc && dv.Key < canonMin) canonMin = dv.Key;
                    cand.Add(new KeyValuePair<Settlement, float>(st, dv.Key));
                }
            }
            _modelT += Ts() - m0;
            if (cand.Count == 0) { IronX(4); return; }
            cand.Sort((a, b) => a.Value.CompareTo(b.Value));
            var model = Campaign.Current.Models.TargetScoreCalculatingModel;
            Settlement bestT = null; float bestS = 0f; double bestQ = -1; MobileParty.NavigationType bestNav = MobileParty.NavigationType.None; bool bestPort = false;
            double minQ = -1; Settlement minT = null; int qBad = 0, vet = 0, qn = 0;
            for (int i = 0, tried = 0; i < cand.Count && tried < 4 && qn < 8; i++)
            {
                var t = cand[i].Key;
                string why;
                int ci = coast ? IronCanonIdx(t) : -1;
                if (WarFront334.VetoedFor334e(k, t, out why)) { vet++; long wv = Ts(); lock (_lk) { _tWait += Ts() - wv; _dIronVeto++; if (ci >= 0) _dIronC[ci, 3]++; } continue; }
                double q;
                long wq = Ts();
                lock (_lk)
                {
                    _tWait += Ts() - wq;
                    if (!SnapFresh || Blocked(party, t)) { vet++; if (ci >= 0) _dIronC[ci, 4]++; continue; }
                    float our = OurDays(party, t);
                    if (our < 0f) continue;
                    float first; double ally;
                    double z = ZFor(t, k, party, our + SiegeDays, out first, out ally);
                    q = Ratio(z, qStr + ally);
                    double qk;
                    if (q <= SiegeBanQ && KBlocked(k, t, party, qStr, our, out qk)) q = qk;   // 334g: po odstapieniu - miara odwrotu
                }
                qn++;
                if (ci >= 0) { long wc = Ts(); lock (_lk) { _tWait += Ts() - wc; if (_dIronCQ[ci].Count < 2000) _dIronCQ[ci].Add(q); } }
                if (minQ < 0 || q < minQ) { minQ = q; minT = t; }
                if (q > SiegeBanQ) { qBad++; if (ci >= 0) IronC(t, 5); continue; }
                tried++;   // limit 4 - tylko cele z q <= 1.0
                var dv = nv.D[t];
                // 342 (Z3): cel kanonu - odleglosc wzgledna (najblizszy cel kanonu / ten), nie wzor rabunku z dnem 0.1 (wszystkie cele kanonu leza 230-450 od wysp)
                float ds = ci >= 0 && canonMin < float.MaxValue && dv.Key > 0f ? Math.Min(1f, canonMin / dv.Key) : MBMath.Map(0.75f - dv.Key / range, 0f, 1f, 0.1f, 1f);
                float sc;
                long m1 = Ts();
                _inOwn = true;
                try { sc = model.GetTargetScoreForFaction(t, Army.ArmyTypes.Besieger, party, strength) * ds; }
                finally { _inOwn = false; _modelT += Ts() - m1; }
                if (ci >= 0) { long wc = Ts(); lock (_lk) { _tWait += Ts() - wc; if (!(sc > 0f)) _dIronC[ci, 6]++; if (sc > _dIronCBest[ci]) _dIronCBest[ci] = sc; _dIronCDist[ci] = dv.Key; } }
                if (sc > bestS) { bestS = sc; bestT = t; bestQ = q; bestNav = dv.Value.Key; bestPort = dv.Value.Value; }
            }
            long wm = Ts();
            lock (_lk)
            {
                _tWait += Ts() - wm;
                if (minT != null && (_dIronMinQ < 0 || minQ < _dIronMinQ)) { _dIronMinQ = minQ; _dIronMinT = minT.StringId; _dIronMinStr = qStr; }
            }
            if (bestT == null || !(bestS > 0f))
            {
                if (gameCanon >= 0) { IronX(3); return; }   // 340: nasza ocena nic nie dala - zostaje opcja gry na cel kanonu
                IronX(qBad > 0 && minQ > SiegeBanQ ? 5 : (minT == null ? (qn == 0 && vet > 0 ? 9 : 6) : 7)); return;
            }
            // 340 (d3): pierwszenstwo kanonu - cel w oknie (q <= 0.8) wygrywa z rabunkami i innymi opcjami (nie z obrona wlasnej oblezonej twierdzy - ta zostaje wyzej w 274/OB5)
            float v = bestS;
            bool prio = false;
            if (on340 && !ownDef && bestQ <= OpenQ)
            {
                float pv = CanonWarBalance.Edge * Math.Max(bestOther, CanonWarBalance.FloorBase);
                if (pv > v) { v = pv; prio = true; }
            }
            int same = -1;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && ReferenceEquals(e.Item1.Party, bestT) && e.Item1.WillGatherArmy == gather) { same = i; break; }
            }
            if (same >= 0) { if (scores[same].Item2 < v) { var d = scores[same].Item1; pt.SetBehaviorScore(in d, v); } }
            else pt.AddBehaviorScore((new AIBehaviorData(bestT, AiBehavior.BesiegeSettlement, bestNav, gather, bestPort, false), v));
            long wl = Ts();
            lock (_lk)
            {
                _tWait += Ts() - wl;
                _dIronOffers++; _dIronX[8]++;
                if (prio) _dIronPrio++;
                int bi = coast ? IronCanonIdx(bestT) : -1;
                if (bi >= 0) _dIronC[bi, 7]++;
                if (_dIronOffers <= 20 && S != null && S.LogEnabled)
                    Log.Info(Tag + "Zelazni oblegaja | dzien " + Today + " | " + Nm(party) + (gather ? " (zbiorka armii, czlonkow " + members.Count + ")" : " (wodz armii" + (atSea ? ", na morzu" : "") + ")") + " | " + bestT.StringId + " (" + Nm(bestT)
                             + ", " + Id(bestT.MapFaction) + ") | sila " + F0(qStr) + " (model " + F0(strength) + ") | q " + F2(bestQ) + " | wynik " + v.ToString("0.000", Inv)
                             + (prio ? " (pierwszenstwo kanonu, model " + bestS.ToString("0.000", Inv) + ")" : "") + ".");
            }
        }

        /// <summary>340 (d2): wodz armii Wysp na morzu - cel armii (oblezenie twierdzy kanonu) zostaje, dopoki q &lt;= 1.25 (histereza biezacego celu) i bez weta.
        /// true - wpis dodany / podbity (koniec mysli 334e).</summary>
        private static bool IronKeepSea(MobileParty party, PartyThinkParams pt, Kingdom k, float bestOther)
        {
            Settlement t = null;
            try
            {
                if (party.DefaultBehavior == AiBehavior.BesiegeSettlement) t = party.TargetSettlement;
                if (t == null && party.Army != null) t = party.Army.AiBehaviorObject as Settlement;
            }
            catch { t = null; }
            if (t == null || !t.IsFortification || t.Town == null || !IronCanon(t) || t.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(k, t.MapFaction)) return false;
            string why;
            if (WarFront334.VetoedFor334e(k, t, out why)) return false;
            float qStr = 0f;
            foreach (var ap in party.Army.Parties) if (ap != null && ap.IsActive && ap.Party != null) qStr += PStr(ap.Party);
            double q;
            long wq = Ts();
            lock (_lk)
            {
                _tWait += Ts() - wq;
                if (!SnapFresh || Blocked(party, t)) return false;
                float our = OurDays(party, t);
                if (our < 0f) return false;
                float first; double ally;
                double z = ZFor(t, k, party, our + SiegeDays, out first, out ally);
                q = Ratio(z, qStr + ally);
                double qk;
                if (KBlocked(k, t, party, qStr, our, out qk)) return false;   // 340 (recenzja): blokada krolestwa - armia na morzu nie trzyma celu
            }
            if (q > AbandonQ) return false;
            MobileParty.NavigationType nav; float dist; bool fromPort;
            long m0 = Ts();
            Helpers.AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, t, false, out nav, out dist, out fromPort);
            _modelT += Ts() - m0;
            if (nav == MobileParty.NavigationType.None) return false;
            var scores = pt.AIBehaviorScores;
            float v = CanonWarBalance.Edge * Math.Max(bestOther, CanonWarBalance.FloorBase);
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && ReferenceEquals(e.Item1.Party, t))
                {
                    if (e.Item2 <= 0f) return false;   // weto / rozejm na ten cel - nie wskrzeszamy
                    if (e.Item2 < v) { var d = e.Item1; pt.SetBehaviorScore(in d, v); }
                    long w1 = Ts(); lock (_lk) { _tWait += Ts() - w1; _dIronKeepSea++; }
                    return true;
                }
            }
            pt.AddBehaviorScore((new AIBehaviorData(t, AiBehavior.BesiegeSettlement, nav, false, fromPort, false), v));
            long w2 = Ts(); lock (_lk) { _tWait += Ts() - w2; _dIronKeepSea++; }
            return true;
        }

        /// <summary>340 (d1): zbiorka z ladu - wodz Wysp na morzu idzie do portu, gdy w porcie gra da mu zbiorke (warunki CanLordCreateArmy poza morzem), Wyspy nie maja armii,
        /// sa wolne druzyny Wysp w zasiegu wezwania gry (linia prosta - droga nie krotsza), a najblizszy cel kanonu ma q &lt;= 1.0 z ich sila (ZFor, jak oferta). true - wpis dodany.</summary>
        private static bool IronMuster(MobileParty party, PartyThinkParams pt)
        {
            if (pt.DoNotChangeBehavior) { IronMx(0); return false; }
            var k = party.MapFaction as Kingdom;
            // 342 (C2): istniejaca armia Wysp nie blokuje juz zbiorki - limit z modelu gry (ceil(0.7 x druzyn - w armiach) w IronCands) i limit wplywu rodu (AnyLordArmy342)
            if (k == null) { IronMx(1); return false; }
            var h = party.LeaderHero; var c = h != null ? h.Clan : null;
            if (h == null || c == null || !AnyLordArmy342.MayLead(party)) { IronMx(2); return false; }
            if (!(c.Influence > 100f)) { IronMx(3); return false; }
            if (c.IsUnderMercenaryService) { IronMx(4); return false; }
            float sizeMin = Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy;
            int today = Today;
            long wf0 = Ts();
            lock (_lk)
            {
                _tWait += Ts() - wf0;
                int fd;
                if (_musterFail.TryGetValue(party, out fd) && today - fd < MusterFailDays) { _dIronMusterBan++; _dIronMx[5]++; return false; }   // 340: nieudana zbiorka w porcie - bez petli
            }
            long mf = Ts();
            bool ok = (float)party.GetNumDaysForFoodToLast() > Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege && party.PartySizeRatio > sizeMin;
            _modelT += Ts() - mf;
            if (!ok) { IronMx(6); return false; }
            bool war = false;
            foreach (var f in k.FactionsAtWarWith) if (f != null && f.Fiefs != null && f.Fiefs.Count > 0) { war = true; break; }
            if (!war) { IronMx(7); return false; }
            // 340 (recenzja): port najpierw - kandydaci gry liczeni od portu zbiorki (droga gry, limit 0.7, prog 1000, sito SCA - IronCands)
            var port = WarFront334.NearestPort(party, 0f, NowH);
            if (port == null) { IronMx(8); return false; }
            var pos = party.GetPosition2D;
            float o; int n, cw;
            long mc = Ts();
            try { cw = IronCands(party, k, port, out o, out n); } finally { _modelT += Ts() - mc; }
            if (cw >= 0) { IronWhy(cw); IronMx(9); return false; }
            // najblizszy cel kanonu (linia prosta) z q <= 1.0 przy tej sile
            bool afterRw = false;
            try { afterRw = CanonWarClock.OverNow(); } catch { }
            var cand = new List<KeyValuePair<Settlement, float>>();
            foreach (var f in k.FactionsAtWarWith)
            {
                if (f == null || !f.IsKingdomFaction || ScaNoBandits320.Skip(f) != 0) continue;
                foreach (var st in f.Settlements)
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.IsUnderSiege) continue;
                    if (!afterRw && !IronCanon(st)) continue;
                    cand.Add(new KeyValuePair<Settlement, float>(st, pos.DistanceSquared(st.GetPosition2D)));
                }
            }
            if (cand.Count == 0) { IronMx(10); return false; }
            cand.Sort((a, b) => a.Value.CompareTo(b.Value));
            Settlement tgt = null; double tq = -1;
            for (int i = 0; i < cand.Count && i < 4; i++)
            {
                var t = cand[i].Key;
                string why;
                if (WarFront334.VetoedFor334e(k, t, out why)) continue;
                double q;
                long wq = Ts();
                lock (_lk)
                {
                    _tWait += Ts() - wq;
                    if (!SnapFresh) return false;
                    float our = OurDays(party, t);
                    if (our < 0f) continue;
                    float first; double ally;
                    double z = ZFor(t, k, party, our + SiegeDays, out first, out ally);
                    q = Ratio(z, o + ally);
                    double qk;
                    if (q <= SiegeBanQ && KBlocked(k, t, party, o, our, out qk)) q = qk;
                }
                if (q <= SiegeBanQ) { tgt = t; tq = q; break; }
            }
            if (tgt == null) { IronMx(10); return false; }
            MobileParty.NavigationType nav; float dist; bool fromPort;
            long m0 = Ts();
            Helpers.AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, port, false, out nav, out dist, out fromPort);
            _modelT += Ts() - m0;
            if (nav == MobileParty.NavigationType.None) { IronMx(11); return false; }
            var scores = pt.AIBehaviorScores;
            float best = 0f;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.DefendSettlement && e.Item1.Party is Settlement ds && ds.MapFaction == party.MapFaction && ds.IsUnderSiege) { IronMx(12); return false; }   // odsiecz wlasnej twierdzy wygrywa
                if (e.Item2 > best) best = e.Item2;
            }
            float v = CanonWarBalance.Edge * Math.Max(best, CanonWarBalance.FloorBase);
            pt.AddBehaviorScore((new AIBehaviorData(port, AiBehavior.GoToSettlement, nav, false, fromPort, false), v));
            // 342: w porcie zbiorki ta druzyna ma pierwszenstwo kolejki rodu (regula "kazdy lord") do konca okna proby - gra da zbiorke w pierwszej mysli w porcie
            AnyLordArmy342.Prefer(party, (MusterFailDays + 1) * 24);
            long wl = Ts();
            lock (_lk)
            {
                _tWait += Ts() - wl;
                _musterTry[party] = new KeyValuePair<Settlement, int>(port, today);
                int last;
                bool first = !_musterDay.TryGetValue(party, out last) || last != today;
                _musterDay[party] = today;
                if (first)
                {
                    _dIronMuster++;
                    if (_dIronMusterLines < 20 && S != null && S.LogEnabled)
                    {
                        _dIronMusterLines++;
                        Log.Info(Tag + "Zelazni zbiorka w porcie | dzien " + today + " | " + Nm(party) + " (wplyw rodu " + F0(c.Influence) + ") -> " + port.StringId + " (" + Nm(port) + ") | wolnych druzyn Wysp w zasiegu "
                                 + n + ", sila z nimi " + F0(o) + " | cel kanonu " + tgt.StringId + " (" + Nm(tgt) + ") q " + F2(tq) + " | ocena " + v.ToString("0.000", Inv) + " - zbiorke w porcie robi gra (CanLordCreateArmy).");
                    }
                }
            }
            return true;
        }

        /// <summary>340 (recenzja): wodz wyslany na zbiorke stoi w porcie, a gra nie dala czlonkow - nieudana proba (bez nowej oferty przez MusterFailDays), linia w logu.</summary>
        private static void IronMusterFailed(MobileParty party, int why)
        {
            var cs = party.CurrentSettlement;
            if (cs == null || party.Army != null) return;
            if (why == 12) return;   // 342: nie jego godzina w kolejce rodu - gra jeszcze nie pytala (Prefer daje pierwszenstwo, wiec rzadkie)
            long w = Ts();
            lock (_lk)
            {
                _tWait += Ts() - w;
                KeyValuePair<Settlement, int> tr;
                if (!_musterTry.TryGetValue(party, out tr) || tr.Key != cs || Today - tr.Value > MusterFailDays) return;
                _musterTry.Remove(party);
                _musterFail[party] = Today;
                _dIronMusterFail++;
                if (S != null && S.LogEnabled)
                    Log.Info(Tag + "Zelazni zbiorka nieudana | dzien " + Today + " | " + Nm(party) + " w " + cs.StringId + " (" + Nm(cs) + ") | gra nie dala czlonkow (powod "
                             + IronWhyName[Math.Max(0, Math.Min(IronWhyName.Length - 1, why))] + ") | bez nowej zbiorki w porcie przez " + MusterFailDays + " doby.");
            }
        }

        /// <summary>Linia koszt: wyjscia IronSiege i najnizsze q doby.</summary>
        private static string IronXLine()
        {
            var sb = new System.Text.StringBuilder(" [wyjscia:");
            for (int i = 0; i < _dIronX.Length; i++) { if (i == 8) continue; sb.Append(i == 0 ? " " : ", ").Append(IronXName[i]).Append(' ').Append(_dIronX[i]); }
            sb.Append("; min q ").Append(_dIronMinQ < 0 ? "-" : F2(_dIronMinQ)).Append(" @").Append(_dIronMinT).Append(" (sila ").Append(F0(_dIronMinStr)).Append(")]");
            // 340 (d): powody braku zbiorki (odczyt warunkow gry) i dzwignie kanonu
            sb.Append(" [340: bez zbiorki -");
            for (int i = 0; i < _dIronWhy.Length; i++) sb.Append(i == 0 ? " " : ", ").Append(IronWhyName[i]).Append(' ').Append(_dIronWhy[i]);
            sb.Append("; zbiorka w porcie ").Append(_dIronMuster).Append(", armia na morzu trzyma cel ").Append(_dIronKeepSea).Append(", pierwszenstwo kanonu ").Append(_dIronPrio)
              .Append(", w porcie bez armii ").Append(_dIronMusterFail).Append(" (wstrzymane ").Append(_dIronMusterBan).Append(")]");
            // 342 (C1): wyjscia zbiorki w porcie
            sb.Append(" [342 zbiorka w porcie - wyjscia:");
            for (int i = 0; i < _dIronMx.Length; i++) sb.Append(i == 0 ? " " : ", ").Append(IronMxName[i]).Append(' ').Append(_dIronMx[i]);
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Doba (Daily, pod _lk): porzadek pamieci obozow, flag, blokad, rabunkow i przydzialow obrony.</summary>
        private static void SiegeDayClean()
        {
            double nowH = CampaignTime.Now.ToHours, nowD = CampaignTime.Now.ToDays;
            var deadC = new List<SiegeEvent>();
            foreach (var kv in _camps) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) deadC.Add(kv.Key);
            foreach (var e in deadC) _camps.Remove(e);
            var deadR = new List<MobileParty>();
            foreach (var kv in _retreat) if (kv.Value <= nowH || kv.Key == null || !kv.Key.IsActive) deadR.Add(kv.Key);
            foreach (var p in deadR) _retreat.Remove(p);
            var deadB = new List<PS>();
            foreach (var kv in _block) if (kv.Value <= nowD || kv.Key.P == null || !kv.Key.P.IsActive) deadB.Add(kv.Key);
            foreach (var s in deadB) _block.Remove(s);
            var deadK = new List<SK>();
            foreach (var kv in _kblock) if (kv.Value <= nowD) deadK.Add(kv.Key);
            foreach (var s in deadK) _kblock.Remove(s);
            // 340 (c): pamiec odsieczy bez zywej druzyny w wojnie z krolestwem - znika (reszte sprawdza KBlocked przy ocenie)
            var deadKr = new List<SK>();
            foreach (var kv in _krel)
            {
                bool any = false;
                var kf = kv.Key.A as IFaction;
                foreach (var m in kv.Value.L)
                {
                    if (m == null || !m.IsActive || kf == null) continue;
                    var mf = m.MapFaction;
                    try { if (mf != null && FactionManager.IsAtWarAgainstFaction(kf, mf)) { any = true; break; } } catch { }
                }
                if (!any || nowD > kv.Value.Since + HorizonDays) deadKr.Add(kv.Key);   // 340: twardy koniec pamieci
            }
            foreach (var s in deadKr) _krel.Remove(s);
            var deadM = new List<MapEvent>();
            foreach (var kv in _raidBad) if (kv.Key == null || kv.Key.IsFinalized || kv.Value[0] < NowH - 2) deadM.Add(kv.Key);
            foreach (var m in deadM) _raidBad.Remove(m);
            var deadI = new List<MobileParty>();
            foreach (var kv in _ironNav) if (kv.Key == null || !kv.Key.IsActive || NowH - kv.Value.Hour >= 6) deadI.Add(kv.Key);
            foreach (var p in deadI) _ironNav.Remove(p);
            var deadMu = new List<MobileParty>();
            foreach (var kv in _musterDay) if (kv.Key == null || !kv.Key.IsActive || kv.Value < Today - 1) deadMu.Add(kv.Key);
            foreach (var p in deadMu) _musterDay.Remove(p);
            var deadTr = new List<MobileParty>();
            foreach (var kv in _musterTry) if (kv.Key == null || !kv.Key.IsActive || kv.Value.Value < Today - MusterFailDays) deadTr.Add(kv.Key);
            foreach (var p in deadTr) _musterTry.Remove(p);
            var deadF = new List<MobileParty>();
            foreach (var kv in _musterFail) if (kv.Key == null || !kv.Key.IsActive || Today - kv.Value >= MusterFailDays) deadF.Add(kv.Key);
            foreach (var p in deadF) _musterFail.Remove(p);
            DefenceDayClean();
        }
    }
}
