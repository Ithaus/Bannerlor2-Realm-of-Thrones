using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// OB5 - ODSIECZ OBLEZONEJ TWIERDZY MA PIERWSZENSTWO (decyzja Jeffa 14.10: "jesli oblezona jest stolica, to jak AI pozwala, zeby armia szla gdzie indziej -
    /// stolicy i miast trzeba bronic za wszelka cene, jak idzie obca armia"; klucze BesiegedRelief*).
    ///
    /// DLACZEGO DZIS NIE BRONIA (dekompilacja gry 1.4.8, BK, StrategicCampaignAI145 - sprawdzone, nie zalozone):
    ///  1. Ocena obrony lorda w pojedynke (DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction, missionType Defender) to stosunek sil:
    ///     (nasza sila + juz idacy) / sila wroga pod murami, x0.9 gdy mniej niz 1.1 x wrog, x (nasza / wrog)^0.25 gdy slabszy. Lord 300 ludzi wobec armii
    ///     Robba (18 druzyn, 4484 ludzi, sila 7700) dostaje ok. 0.04 x 0.9 x 0.45 = 0.016 "uczciwej walki", a najazd na wies albo poscig za taborem
    ///     (AiEngagePartyBehavior) - wynik przyciety do 2. Wygrywa wszystko inne.
    ///  2. Gra nie zwoluje armii do obrony: AiMilitaryBehavior.AiHourlyTick daje WillGatherAnArmy tylko misji Besieger (petla po 4 typach, warunek
    ///     "flag &amp;&amp; armyTypes == Besieger"), choc AiPartyThinkBehavior (i jego kopia w ROT AIThinkPatch) umie zalozyc armie Defender.
    ///  3. BK (BKArmyBehavior.EvaluateCreateArmy, raz na dobe, AIArmyFormation = true u Jeffa) zaklada armie Defender dla NAJBLIZSZEJ oblezonej twierdzy w 350
    ///     jednostkach - nie najwazniejszej; w d516-530 fin30 obok Krolewskiej Przystani oblegano zamki KP Pinkmaiden (d520) i Banefort (d515) [P CZAS Aneks A].
    ///     Armia typu Defender moze tez scigac partie wroga (AiEngagePartyBehavior wpuszcza tylko armie Defender) - 42 z 59 bitew lordow KP to druzyny po 2-5
    ///     partii bijace tabory, karawany i lordow Dorzecza (bitwy.log, FieldBattle).
    ///  4. StrategicCampaignAI145 (StrategicTargetScoreModel) ocen obrony nie rusza (wlasna osada = !IsEnemy -> wynik bazowy); mnozy tylko cele wroga.
    ///  5. Zasieg obrony gry: 8.02 doby marszu (AiMilitaryBehavior.GetDistanceScoreForDefending) - dalej ocena 0.
    ///
    /// REGULA (jedna dla wszystkich krolestw, takze krolestwa gracza; druzyna gracza nigdy nie jest ruszana - gracz decyduje sam):
    ///  - Twierdza krolestwa oblegana przez wroga (nie Innych) jest "w potrzebie", dopoki zebrana odsiecz C (druzyny krolestwa z celem DefendSettlement tej
    ///    osady i armie krolestwa zbierane / idace do niej - ten sam rachunek, co "juz idacy" w ocenie gry) jest mniejsza niz wymagana R = mnoznik klasy x sila
    ///    obozu (suma EstimatedStrength stron obozu). Mnozniki: stolica 2.0 (gra przycina przewage w ocenie celu do 2 - wiecej nie nagradza), miasto 1.5,
    ///    zamek 1.2 (gra rusza na oboz, gdy zebrani pod murami maja 1.2 x sile obozu - MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement).
    ///  - Druzyna lorda (albo wodz armii) krolestwa, ktora ma ocene obrony tej osady od gry (czyli jest w zasiegu gry), dostaje ocene
    ///    P_klasy x B + 0.01 x ocena gry, gdzie B = max(najlepszy inny cel, 0.5, najlepsza ocena gry obrony oblezonych twierdz krolestwa), P: stolica 1.5,
    ///    miasto 1.3, zamek 1.1 - kazda > 1, wiec obrona wygrywa z kazdym innym celem (najazd, lupienie, oblezenie gdzie indziej, poscig, patrol, wizyta),
    ///    a stolica z miastem i zamkiem. 0.5 - prog gry na zmiane celu (0.1, 6 rzutow na godzine mysli) przechodzi wtedy z pewnoscia 1 - 0.5^6 = 98%.
    ///    Druzyna, bez ktorej zebrana odsiecz i tak >= R, nie jest podbijana (bez nadmiaru i bez przeskokow).
    ///  - Recenzja 14.10: druzyna (wodz armii), ktora sama oblega twierdze wroga, nie jest podbijana do odsieczy ZAMKU - tylko stolicy i miasta (decyzja
    ///    Jeffa wymienia "oblezenia gdzie indziej" przy stolicy i miescie); inaczej dwa krolestwa oblegajace nawzajem swoje zamki w zasiegu zwijalyby
    ///    oba obozy co kilka godzin (petla odsieczy). Najazdy, poscigi, patrole i wizyty ustepuja takze odsieczy zamku.
    ///  - Nie podbijamy: druzyny glodujacej (PartyBase.IsStarving - najpierw jedzenie, droga gry), druzyny w bitwie innej niz rabunek, czlonka cudzej armii,
    ///    wodza armii czekajacej na czlonkow (DoNotChangeBehavior), druzyn w oblezonej osadzie (gra ich nie pyta).
    ///  - Wladca krolestwa (wolny, poza armia, ze swoja druzyna w zasiegu i z druzynami do wezwania - CanLordCreateArmy przeszlo w tej mysli) dostaje dodatkowa
    ///    opcje "zbierz armie i bron" (WillGatherArmy, DefendSettlement) z ocena 1.05 x ocena obrony - gra zaklada wtedy armie Defender (Kingdom.CreateArmy,
    ///    zwykly koszt wplywu), chyba ze armia krolestwa juz zbiera sie albo idzie na odsiecz tej osady.
    ///  - Odsiecz pod murami - droga gry: czekaja w pozycji obrony, ruszaja na oboz, gdy razem maja 1.2 x jego sile (a SCA145 zdejmuje oblezenie, gdy wrogie
    ///    druzyny w 95 jedn. maja 1.25 x sile obozu).
    ///  - f12-odsiecz (18.10, klucz BesiegedReliefOnlyWhenReachable; diagnoza logu Jeffa WGRANIE 80: Krolewska Przystan wymagana 24.2k, zebrane 11.2k = cala armia
    ///    Korony, 14 dob pod murami, werbunek regionow Korony 70 -> 3.5 na dobe): ODSIECZ BEZ SZANS ZWALNIA DRUZYNY. Zasieg krolestwa Z = zebrana odsiecz C + sila
    ///    druzyn krolestwa, ktore przy swojej ostatniej mysli (najwyzej 24 h temu) dostaly od gry ocene obrony tej osady, a nie sa w zebranej odsieczy (czlonek armii
    ///    liczony w armii wodza; przy zamku bez druzyn oblegajacych twierdze wroga - tych OB5 do zamku nie podbija). Prog P = min(R, 1.2 x sila obozu) - 1.2 to
    ///    prog gry na atak odsieczy na oboz (MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement, num15 = 1.2f dla twierdzy). Gdy Z < P, krolestwo nie zbierze
    ///    odsieczy, ktora gra rzuci na oboz - OB5 nie podbija ocen obrony tej osady (ani opcji armii wladcy): wracaja oceny gry (werbunek w osadzie, jedzenie,
    ///    odsiecz innej oblezonej twierdzy krolestwa, ktora jest w zasiegu). Gdy krolestwo urosnie (Z >= P), regula wlacza sie sama. Pierwsze 13 h od wykrycia
    ///    oblezenia bez tej bramki (wolna druzyna mysli co 6 h zegara AI - AiPartyThinkBehavior / ROT AIThinkPatch, DefaultThinkingPeriodInHours = 6 - a w oboz
    ///    swiata 0-6 jej AI spi; po 13 h kazda druzyna w zasiegu juz raz pomyslala). Wodz armii mysli co 1 h, takze w czasie zbiorki (DoNotChangeBehavior -
    ///    bez podbicia, ale wpis zasiegu odswiezany; recenzja f12: inaczej po 24 h zbiorki Z tracilo cala armie). Zwolniona = bramka zamknela co najmniej
    ///    jedna osade, a OB5 nie zostawil druzynie zadnej odsieczy. Wylaczony klucz = jak przed f12 (podbicie az do zebrania R).
    /// Latka: postfiks Low na CampaignEventDispatcher.AiHourlyTick (po wszystkich nasluchach i po filtrze ROT AIThinkPatch.Patch1; tak jak WatchLaw) - wpieta
    /// zawsze, dziala przy kluczu. Stan oblezen liczony raz na godzine gry (lista oblezen gry + partie wojenne krolestw z oblezona twierdza). Bez zapisu.
    /// Linie: "Odsiecz (OB5): start sesji", "Odsiecz (OB5) poczatek", "Odsiecz (OB5) armia krola", "Odsiecz (OB5) koniec", "Odsiecz (OB5): dzien" (doba).
    /// </summary>
    internal static class BesiegedRelief
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int CCapital = 0, CTown = 1, CCastle = 2;
        private static readonly string[] ClassName = { "stolica", "miasto", "zamek" };
        private static readonly float[] Prio = { 1.5f, 1.3f, 1.1f };   // pierwszenstwo klasy (kazde > 1)
        private const float FloorBase = 0.5f;                            // najnizsza podstawa B (prog zmiany celu gry)
        private const float KingArmyEdge = 1.05f;                        // opcja armii wladcy ponad jego wlasna obrone
        // f12-odsiecz: bramka zasiegu krolestwa
        private const float GameEngage = 1.2f;                           // gra rzuca odsiecz na oboz twierdzy przy 1.2 x jego sile (MobilePartyAi, num15)
        private const double ReachWindowHours = 24.0;                    // ocena obrony od gry starsza niz doba - druzyna poza zasiegiem
        private const double ReachWarmupHours = 13.0;                    // 6 h zegara mysli AI + 6 h obozu swiata + 1 h zapasu: pierwsze godziny bez bramki

        private sealed class Siege
        {
            public SiegeEvent Ev; public Settlement St; public Kingdom K; public int Cls;
            public float Besiegers, Need, Committed;
            public readonly HashSet<MobileParty> In = new HashSet<MobileParty>();   // zebrani przy przeliczeniu godziny (+ zarezerwowani w tej godzinie)
            public bool ArmyOnWay;                                                   // armia krolestwa zbiera sie / idzie na odsiecz
            public Track T;
            public float Reach, Floor;   // f12: zasieg krolestwa (zebrana + druzyny z ocena obrony od gry) i prog min(R, 1.2 x oboz) - przeliczane raz na godzine
            public bool Ready;           // f12: minelo ReachWarmupHours od wykrycia (zasieg policzony ze wszystkich druzyn w zasiegu)
        }

        private sealed class Track
        {
            public string Id, Name, KName, BesFaction, BesLeader; public Kingdom K; public Settlement St; public int Cls, Day0;
            public float Bes0, BesMax, Need0, CMax, LastC, LastNeed;
            public bool Captured;
            public double Hour0;                                                          // f12: godzina wykrycia (pelna godzina gry)
            public readonly Dictionary<MobileParty, double> EligAt = new Dictionary<MobileParty, double>();   // f12: druzyna -> godzina ostatniej mysli z ocena obrony od gry
            public readonly HashSet<MobileParty> DayRel = new HashSet<MobileParty>();    // f12: druzyny zwolnione dzis bramka zasiegu (linia doby)
            public readonly HashSet<MobileParty> Eligible = new HashSet<MobileParty>();   // druzyny z ocena obrony od gry (w zasiegu)
            public readonly HashSet<MobileParty> Pulled = new HashSet<MobileParty>();     // druzyny podbite przez OB5
            public readonly HashSet<MobileParty> Seen = new HashSet<MobileParty>();       // druzyny kiedykolwiek w zebranej odsieczy
            public readonly HashSet<MobileParty> Redirected = new HashSet<MobileParty>(); // podbite, a potem w odsieczy
            public int Armies, Battles, ReliefWon, Raised;
            public double KingOfferHour = -10.0;
        }

        private static readonly Dictionary<Kingdom, List<Siege>> _byK = new Dictionary<Kingdom, List<Siege>>();
        private static readonly Dictionary<SiegeEvent, Track> _tracks = new Dictionary<SiegeEvent, Track>();
        // armie wladcy zalozone z opcji OB5 -> osada odsieczy (przy zbiorce Army.AiBehaviorObject to MIEJSCE ZBIORKI - Army.FindBestGatheringSettlementAndMoveTheLeader)
        private static readonly Dictionary<Army, Settlement> _armyFor = new Dictionary<Army, Settlement>();
        // oblezenia 3 (KO): odsiecz w chwili konca oblezenia - Finish zdejmuje Track PRZED nasluchem SiegeWalls (ReliefBehavior dodany po ArmouryBehavior, a MbEvent
        // wola ostatnio dopisanego sluchacza pierwszy), wiec ostatni stan (zebrana, wymagana) zostaje tu do konca doby
        private static readonly Dictionary<SiegeEvent, float[]> _endedRelief = new Dictionary<SiegeEvent, float[]>();
        private static readonly List<int> _tmpIdx = new List<int>();
        private static readonly List<Siege> _tmpSg = new List<Siege>();
        private static readonly List<Siege> _tmpGated = new List<Siege>();   // f12: osady, ktorym bramka zasiegu zamknela podbicie w tej mysli
        private static readonly HashSet<MobileParty> _dParties = new HashSet<MobileParty>();
        private static readonly HashSet<MobileParty> _dRelParties = new HashSet<MobileParty>();   // f12: druzyny zwolnione dzis bramka zasiegu (bez zadnej odsieczy od OB5)
        private static readonly HashSet<MobileParty> _dUnreadyParties = new HashSet<MobileParty>();   // recenzja p9: niegotowe (ArmsFirst) w mysli OB5 dzis
        private static readonly List<MobileParty> _tmpGone = new List<MobileParty>();
        private static double _hour = -1.0;
        private static int _dRaised, _dStarve, _dKingOffer, _dKingNoCall, _dArmies, _dStarted, _dEnded, _dCaptured, _undeadNow, _offClassNow, _stumbles, _dStumbles, _dOwnSiege, _dRel, _dUnready;
        private static bool _patched; private static string _missing; private static MethodBase _target;

        private static Settings S { get { return Settings.Current; } }
        internal static bool On { get { var s = S; return s != null && s.BesiegedReliefFirst; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        internal static void Reset()
        {
            _byK.Clear(); _tracks.Clear(); _armyFor.Clear(); _tmpIdx.Clear(); _tmpSg.Clear(); _dParties.Clear(); _hour = -1.0; _endedRelief.Clear();
            _dRelParties.Clear(); _tmpGone.Clear(); _tmpGated.Clear(); _dUnreadyParties.Clear();
            _dRaised = _dStarve = _dKingOffer = _dKingNoCall = _dArmies = _dStarted = _dEnded = _dCaptured = _undeadNow = _offClassNow = _stumbles = _dStumbles = _dOwnSiege = _dRel = _dUnready = 0;
        }

        private static readonly HashSet<string> _err = new HashSet<string>();
        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("BesiegedRelief." + where, e); } catch { }
        }

        private static float ForceOf(int cls)
        {
            var s = S;
            if (s == null) return 0f;
            float f = cls == CCapital ? s.BesiegedReliefCapitalForce : cls == CTown ? s.BesiegedReliefTownForce : s.BesiegedReliefCastleForce;
            return float.IsNaN(f) || f < 0f ? 0f : f;
        }

        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "-"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "-"; } }
        private static string F0(float v) { return v.ToString("0", Inv); }

        // ------------------------------------------------------------ latka
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _patched) return;
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _target = m;
                if (m == null) { _missing = "CampaignEventDispatcher.AiHourlyTick"; return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(BesiegedRelief), nameof(ThinkPostfix)) { priority = Priority.Low });
                _patched = true;
            }
            catch (Exception e) { _missing = "CampaignEventDispatcher.AiHourlyTick (" + e.GetType().Name + ")"; Log.Error("BesiegedRelief.ApplyAll", e); }
        }

        /// <summary>Sila wnoszona do odsieczy: druzyna sama albo - dla wodza armii - cala armia z druzynami w drodze do niej (wezwane czlonkowie juz naleza do armii;
        /// Army.EstimatedStrength liczy tylko przylaczonych, wiec przy zbiorce zanizalby odsiecz i OB5 wolalby dodatkowych lordow).</summary>
        private static float Contrib(MobileParty mp)
        {
            try
            {
                var a = mp.Army;
                if (a == null || a.LeaderParty != mp) return mp.Party.EstimatedStrength;
                float s = 0f; bool lead = false;
                var ps = a.Parties;
                for (int i = 0; i < ps.Count; i++)
                {
                    var p = ps[i];
                    if (p == null || p.Party == null) continue;
                    if (ReferenceEquals(p, mp)) lead = true;
                    s += p.Party.EstimatedStrength;
                }
                if (!lead) s += mp.Party.EstimatedStrength;
                return s;
            }
            catch { return 0f; }
        }

        // ------------------------------------------------------------ stan oblezen (raz na godzine gry)
        private static void EnsureHour()
        {
            double h = Math.Floor(CampaignTime.Now.ToHours);
            if (h == _hour) return;
            _hour = h;
            Rebuild();
        }

        private static void Rebuild()
        {
            _byK.Clear();
            _undeadNow = _offClassNow = 0;
            var mgr = Campaign.Current != null ? Campaign.Current.SiegeEventManager : null;
            if (mgr == null) return;
            var live = new HashSet<SiegeEvent>();
            var evs = mgr.SiegeEvents;
            for (int i = 0; i < evs.Count; i++)
            {
                var ev = evs[i];
                try
                {
                    if (ev == null || ev.ReadyToBeRemoved) continue;
                    var st = ev.BesiegedSettlement;
                    if (st == null || !st.IsFortification || st.Town == null) continue;
                    var k = st.MapFaction as Kingdom;
                    if (k == null || k.IsEliminated) continue;
                    var camp = ev.BesiegerCamp;
                    var lead = camp != null ? camp.LeaderParty : null;
                    if (lead == null || camp.MapFaction == null || !camp.MapFaction.IsAtWarWith(k)) continue;
                    bool und;
                    try { und = Undead.Party(lead) || (st.OwnerClan != null && ClanIncomeBook.IsUndeadClan(st.OwnerClan)); } catch { und = false; }
                    if (und) { _undeadNow++; continue; }   // Inni - oblezeniami rzadzi CrashScribe (T2c)
                    int cls = CapitalTreasury.IsCapital(st) ? CCapital : st.IsTown ? CTown : CCastle;
                    float force = ForceOf(cls);
                    if (force <= 0f) { _offClassNow++; continue; }
                    float bes = 0f;
                    foreach (var pb in camp.GetInvolvedPartiesForEventType()) if (pb != null) bes += pb.EstimatedStrength;
                    var sg = new Siege { Ev = ev, St = st, K = k, Cls = cls, Besiegers = bes, Need = force * bes };
                    List<Siege> list;
                    if (!_byK.TryGetValue(k, out list)) { list = new List<Siege>(2); _byK[k] = list; }
                    list.Add(sg);
                    live.Add(ev);
                    Track t;
                    if (!_tracks.TryGetValue(ev, out t))
                    {
                        t = new Track
                        {
                            Id = st.StringId, Name = Nm(st), KName = Nm(k), K = k, St = st, Cls = cls, Day0 = Today,
                            BesFaction = Nm(camp.MapFaction), BesLeader = lead.LeaderHero != null ? Nm(lead.LeaderHero) : (lead.Name != null ? lead.Name.ToString() : "-"),
                            Bes0 = bes, Need0 = sg.Need, Hour0 = _hour
                        };
                        _tracks[ev] = t;
                        _dStarted++;
                        try
                        {
                            Log.Info("Odsiecz (OB5) poczatek: dzien " + Today + " | " + t.Name + " (" + t.Id + ", " + ClassName[cls] + ") krolestwa " + t.KName
                                     + " | oblegajacy " + t.BesFaction + ", wodz " + t.BesLeader + ", druzyn " + CountParties(camp) + ", sila " + F0(bes)
                                     + " | wymagana odsiecz " + F0(sg.Need) + " (" + force.ToString("0.0#", Inv) + " x sila obozu); oblezenie od "
                                     + (ev.SiegeStartTime.ElapsedDaysUntilNow).ToString("0.0", Inv) + " dob.");
                        }
                        catch { }
                    }
                    sg.T = t;
                    if (bes > t.BesMax) t.BesMax = bes;
                    t.LastNeed = sg.Need;
                }
                catch (Exception e) { Stumble("Rebuild.siege", e); }
            }
            // zebrana odsiecz: partie wojenne krolestw z oblezona twierdza (ten sam rachunek co "juz idacy" w ocenie obrony gry)
            foreach (var kv in _byK)
            {
                try
                {
                    var k = kv.Key; var list = kv.Value;
                    var wpcs = k.WarPartyComponents;
                    for (int i = 0; i < wpcs.Count; i++)
                    {
                        var mp = wpcs[i] != null ? wpcs[i].MobileParty : null;
                        if (mp == null || !mp.IsActive) continue;
                        Settlement tgt = null; bool army = false;
                        if (mp.Army != null)
                        {
                            var a = mp.Army;
                            if (a.LeaderParty != mp) continue;
                            var obj = a.AiBehaviorObject as Settlement;
                            Settlement mine;
                            if (a.IsWaitingForArmyMembers()) tgt = _armyFor.TryGetValue(a, out mine) ? mine : obj;   // zbiorka armii OB5 - liczy sie dla osady odsieczy
                            else if (mp.DefaultBehavior == AiBehavior.DefendSettlement) tgt = mp.TargetSettlement ?? obj;
                            army = true;
                        }
                        else if (mp.AttachedTo == null && mp.DefaultBehavior == AiBehavior.DefendSettlement) tgt = mp.TargetSettlement;
                        if (tgt == null) continue;
                        for (int j = 0; j < list.Count; j++)
                        {
                            var sg = list[j];
                            if (!ReferenceEquals(sg.St, tgt)) continue;
                            sg.Committed += Contrib(mp);
                            sg.In.Add(mp);
                            if (army) sg.ArmyOnWay = true;
                            var t = sg.T;
                            if (t != null)
                            {
                                t.Seen.Add(mp);
                                if (t.Pulled.Contains(mp)) t.Redirected.Add(mp);
                            }
                            break;
                        }
                    }
                    for (int j = 0; j < list.Count; j++)
                    {
                        var t = list[j].T;
                        if (t == null) continue;
                        t.LastC = list[j].Committed;
                        if (list[j].Committed > t.CMax) t.CMax = list[j].Committed;
                    }
                    // f12: zasieg krolestwa i prog (liczone zawsze - przy wylaczonym kluczu tylko nieuzywane)
                    for (int j = 0; j < list.Count; j++) Reach(list[j]);
                }
                catch (Exception e) { Stumble("Rebuild.relief", e); }
            }
            // oblezenia, ktorych juz nie ma (koniec bez zdarzenia - np. wczytanie)
            if (_tracks.Count > live.Count)
            {
                var gone = new List<SiegeEvent>();
                foreach (var kv in _tracks) if (!live.Contains(kv.Key) && (kv.Key == null || kv.Key.ReadyToBeRemoved || !Active(kv.Key))) gone.Add(kv.Key);
                foreach (var ev in gone) Finish(ev, "bez zdarzenia konca (lista oblezen gry)");
            }
        }

        /// <summary>f12-odsiecz: zasieg krolestwa Z = zebrana C + sila druzyn krolestwa z ocena obrony tej osady od gry przy ostatniej mysli (najwyzej ReachWindowHours
        /// temu), spoza zebranej odsieczy; czlonek armii liczy sie w armii wodza (Contrib), przy zamku bez druzyn oblegajacych twierdze wroga (OB5 ich do zamku nie
        /// podbija). Prog P = min(R, 1.2 x sila obozu). Wpisy starsze niz okno, druzyny nieczynne i z innej frakcji wypadaja ze slownika. Blad - Ready = false
        /// (bez bramki, droga sprzed f12), potkniecie liczone. Nie rzuca.</summary>
        private static void Reach(Siege sg)
        {
            sg.Floor = Math.Min(sg.Need, GameEngage * sg.Besiegers);
            sg.Reach = sg.Committed;
            sg.Ready = false;
            var t = sg.T;
            if (t == null) return;
            try
            {
                float r = sg.Committed;
                _tmpGone.Clear();
                foreach (var kv in t.EligAt)
                {
                    var mp = kv.Key;
                    if (!mp.IsActive || _hour - kv.Value > ReachWindowHours || !ReferenceEquals(mp.MapFaction, t.K)) { _tmpGone.Add(mp); continue; }
                    if (sg.In.Contains(mp)) continue;                                  // juz w zebranej odsieczy (C)
                    if (mp.Army != null && mp.Army.LeaderParty != mp) continue;        // czlonek armii - w sile wodza albo idzie z armia gdzie indziej
                    if (sg.Cls == CCastle && mp.BesiegedSettlement != null) continue;  // recenzja 14.10: oblegajacy twierdze wroga nie idzie do zamku
                    r += Contrib(mp);
                }
                for (int i = 0; i < _tmpGone.Count; i++) t.EligAt.Remove(_tmpGone[i]);
                _tmpGone.Clear();
                sg.Reach = r;
                sg.Ready = _hour - t.Hour0 >= ReachWarmupHours;
            }
            catch (Exception e) { sg.Ready = false; Stumble("Reach", e); }
        }

        private static bool Active(SiegeEvent ev)
        {
            try
            {
                var evs = Campaign.Current.SiegeEventManager.SiegeEvents;
                for (int i = 0; i < evs.Count; i++) if (ReferenceEquals(evs[i], ev)) return true;
            }
            catch { }
            return false;
        }

        private static int CountParties(BesiegerCamp camp)
        {
            int n = 0;
            try { foreach (var pb in camp.GetInvolvedPartiesForEventType()) if (pb != null) n++; } catch { }
            return n;
        }

        // ------------------------------------------------------------ postfiks mysli partii
        /// <summary>Postfiks Low CampaignEventDispatcher.AiHourlyTick: oceny obrony oblezonych twierdz krolestwa ponad inne cele (regula w opisie klasy). Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            try
            {
                if (!party.IsLordParty || party.IsMainParty) return;
                var s = S;
                if (s == null || !s.BesiegedReliefFirst) return;
                var k = party.MapFaction as Kingdom;
                if (k == null) return;
                EnsureHour();
                List<Siege> list;
                if (!_byK.TryGetValue(k, out list) || list.Count == 0) return;
                if (party.Army != null && party.Army.LeaderParty != party) return;
                // f12 recenzja: DoNotChangeBehavior (gra 1.4.8 ustawia je tylko w AiMilitaryBehavior.AiHourlyTick - wodz armii czekajacej na czlonkow, oceny celow
                // i tak liczone) sprawdzane nizej, PO odswiezeniu zasiegu - inaczej wpis wodza w EligAt starzeje sie przez cala zbiorke (do 72 h i dluzej)
                bool dnc = partyThinkParams.DoNotChangeBehavior;
                if (party.MapEvent != null && !party.MapEvent.IsRaid) return;
                if (party.CurrentSettlement != null && party.CurrentSettlement.SiegeEvent != null) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) return;

                float other = 0f, dMax = 0f;
                _tmpIdx.Clear(); _tmpSg.Clear();
                // recenzja p9 (uwaga 5): niegotowa partia (ArmsFirst - jedna regula z ArmsFirst.IsBattle) idzie tylko na odsiecz osady WLASNEGO rodu: odsiecz cudzej
                // osady nie trafia do zasiegu krolestwa (EligAt / Eligible) ani do podbicia - ArmsFirst i tak dalby jej -1, a zasieg bylby zawyzony
                bool unready = ArmsFirst.Unready(party);
                var pc = unready ? party.ActualClan : null;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    Siege sg = null;
                    if (e.Item1.AiBehavior == AiBehavior.DefendSettlement && !e.Item1.WillGatherArmy)
                    {
                        var st = e.Item1.Party as Settlement;
                        if (st != null) for (int j = 0; j < list.Count; j++) if (ReferenceEquals(list[j].St, st)) { sg = list[j]; break; }
                        if (sg != null && unready && !(pc != null && ReferenceEquals(st.OwnerClan, pc))) { _dUnready++; _dUnreadyParties.Add(party); continue; }   // ani odsiecz, ani "inna opcja"
                    }
                    if (sg != null) { _tmpIdx.Add(i); _tmpSg.Add(sg); if (e.Item2 > dMax) dMax = e.Item2; }
                    else if (e.Item2 > other) other = e.Item2;
                }
                // f12: ocena obrony od gry przy tej mysli - wpis do zasiegu krolestwa (godzina); oblezona twierdza krolestwa bez tej oceny - druzyna poza zasiegiem.
                // Wodz armii w zbiorce (dnc) tez odswieza wpis (mysli co 1 h), ale nie wchodzi do Eligible (linia "koniec" liczy jak przed f12).
                for (int j = 0; j < list.Count; j++)
                {
                    var t = list[j].T;
                    if (t == null) continue;
                    if (_tmpSg.Contains(list[j])) { if (!dnc) t.Eligible.Add(party); t.EligAt[party] = _hour; }
                    else t.EligAt.Remove(party);
                }
                if (dnc) return;   // wodz armii czekajacej na czlonkow - bez podbicia (jak przed f12)
                if (_tmpIdx.Count == 0) return;
                if (party.Party.IsStarving) { _dStarve++; return; }   // najpierw jedzenie - droga gry

                float b = Math.Max(Math.Max(other, FloorBase), dMax);
                bool ownSiege = party.BesiegedSettlement != null;   // druzyna (wodz armii) sama oblega twierdze wroga
                bool gateOn = s.BesiegedReliefOnlyWhenReachable;
                Siege top = null; float topScore = -1f; AIBehaviorData topData = default(AIBehaviorData);
                _tmpGated.Clear();
                for (int j = 0; j < _tmpIdx.Count; j++)
                {
                    var sg = _tmpSg[j];
                    // recenzja 14.10: odsiecz ZAMKU nie zdejmuje wlasnego oblezenia (decyzja Jeffa wymienia "oblezenia gdzie indziej" przy stolicy i miescie);
                    // bez tego dwa krolestwa oblegajace nawzajem swoje zamki w zasiegu zwijalyby oba obozy co kilka godzin (petla odsieczy)
                    if (ownSiege && sg.Cls == CCastle) { _dOwnSiege++; continue; }
                    float rest = sg.Committed - (sg.In.Contains(party) ? Contrib(party) : 0f);
                    if (rest >= sg.Need) continue;   // odsiecz wystarczy bez tej druzyny
                    // f12: krolestwo nie zbierze odsieczy, ktora gra rzuci na oboz (zasieg < min(R, 1.2 x oboz)) - bez podbicia, wracaja oceny gry
                    if (gateOn && sg.Ready && sg.Reach < sg.Floor)
                    {
                        _tmpGated.Add(sg);
                        continue;
                    }
                    var e = scores[_tmpIdx[j]];
                    float nv = Prio[sg.Cls] * b + 0.01f * e.Item2;
                    if (nv > e.Item2)
                    {
                        var d = e.Item1;
                        partyThinkParams.SetBehaviorScore(in d, nv);
                        _dRaised++; _dParties.Add(party);
                        if (sg.T != null) { sg.T.Raised++; if (!sg.In.Contains(party)) sg.T.Pulled.Add(party); }
                    }
                    else nv = e.Item2;
                    if (nv > topScore) { topScore = nv; top = sg; topData = e.Item1; }
                }
                // f12: zwolniona = bramka zamknela co najmniej jedna osade, a OB5 nie zostawil druzynie zadnej odsieczy (top == null). Recenzja: przy top != null
                // druzyna idzie na inna odsiecz (podbita albo z ocena gry juz >= nv - i tak najwyzsza) - to nie zwolnienie; DayRel tylko dla zwolnionych
                if (_tmpGated.Count > 0 && top == null)
                {
                    _dRel++; _dRelParties.Add(party);
                    for (int j = 0; j < _tmpGated.Count; j++) if (_tmpGated[j].T != null) _tmpGated[j].T.DayRel.Add(party);
                }
                _tmpGated.Clear();
                if (top == null) return;

                // armia wladcy na odsiecz (gra zaklada ja sama w AiPartyThinkBehavior: WillGatherArmy + DefendSettlement -> Kingdom.CreateArmy, typ Defender)
                if (s.BesiegedReliefKingsArmy && !top.ArmyOnWay && party.Army == null && party.LeaderHero != null && ReferenceEquals(party.LeaderHero, k.Leader)
                    && party.LeaderHero.Clan != null && !party.LeaderHero.Clan.IsUnderMercenaryService)
                {
                    var members = partyThinkParams.PossibleArmyMembersUponArmyCreation;
                    if (members != null && members.Count > 0)
                    {
                        var a = new AIBehaviorData(top.St, AiBehavior.DefendSettlement, topData.NavigationType, true, topData.IsFromPort, topData.IsTargetingPort);
                        partyThinkParams.AddBehaviorScore((a, topScore * KingArmyEdge));
                        _dKingOffer++;
                        if (top.T != null) top.T.KingOfferHour = CampaignTime.Now.ToHours;
                    }
                    else _dKingNoCall++;
                }
                // rezerwacja na reszte godziny: inne druzyny tej godziny licza te druzyne jako idaca
                if (!top.In.Contains(party)) { top.In.Add(party); top.Committed += Contrib(party); }
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        // ------------------------------------------------------------ zdarzenia
        internal static void OnArmyCreated(Army army)
        {
            try
            {
                if (!On || army == null || army.LeaderParty == null || army.Kingdom == null) return;
                var lh = army.LeaderParty.LeaderHero;
                if (lh == null || !ReferenceEquals(lh, army.Kingdom.Leader) || army.ArmyType != Army.ArmyTypes.Defender) return;
                // AiBehaviorObject armii to tu miejsce zbiorki, nie osada odsieczy - przypisanie po ostatniej opcji OB5 tego krolestwa (najwyzej 2 h temu)
                double now = CampaignTime.Now.ToHours;
                Track best = null;
                foreach (var kv in _tracks)
                {
                    var c = kv.Value;
                    if (!ReferenceEquals(c.K, army.Kingdom) || now - c.KingOfferHour > 2.0) continue;
                    if (best == null || c.KingOfferHour > best.KingOfferHour) best = c;
                }
                if (best != null)
                {
                    var t = best;
                    _armyFor[army] = t.St;
                    t.Armies++; _dArmies++;
                    int called = 0;
                    try { foreach (var p in army.Parties) if (p != null && !ReferenceEquals(p, army.LeaderParty)) called++; } catch { }
                    Log.Info("Odsiecz (OB5) armia krola: dzien " + Today + " | " + t.Name + " (" + ClassName[t.Cls] + ") krolestwa " + t.KName + " | wodz " + Nm(lh)
                             + " | druzyn wezwanych " + called + ", sila armii (z wezwanymi) " + F0(Contrib(army.LeaderParty)) + " | oblegajacy " + t.BesFaction + " sila " + F0(t.BesMax)
                             + ", wymagana odsiecz " + F0(t.LastNeed) + ", zebrana " + F0(t.LastC) + " | miejsce zbiorki " + Nm(army.AiBehaviorObject as Settlement) + ".");
                }
            }
            catch (Exception e) { Stumble("OnArmyCreated", e); }
        }

        /// <summary>Armia rozwiazana - wpis "armia OB5 -> osada odsieczy" usuwany (slownik nie rosnie przez kampanie). Nie rzuca.</summary>
        internal static void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
        {
            try { if (army != null) _armyFor.Remove(army); }
            catch (Exception e) { Stumble("OnArmyDispersed", e); }
        }

        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (st == null || _tracks.Count == 0) return;
                foreach (var kv in _tracks) if (ReferenceEquals(kv.Value.St, st) && !ReferenceEquals(st.MapFaction, kv.Value.K)) kv.Value.Captured = true;
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || _tracks.Count == 0) return;
                var st = me.MapEventSettlement;
                if (st == null || me.EventType == MapEvent.BattleTypes.Siege || me.EventType == MapEvent.BattleTypes.Raid) return;
                foreach (var kv in _tracks)
                {
                    var t = kv.Value;
                    if (!ReferenceEquals(t.St, st)) continue;
                    MapEventSide ks = null;
                    var dl = me.DefenderSide != null && me.DefenderSide.LeaderParty != null ? me.DefenderSide.LeaderParty.MapFaction : null;
                    var al = me.AttackerSide != null && me.AttackerSide.LeaderParty != null ? me.AttackerSide.LeaderParty.MapFaction : null;
                    if (ReferenceEquals(dl, t.K)) ks = me.DefenderSide; else if (ReferenceEquals(al, t.K)) ks = me.AttackerSide;
                    if (ks == null) break;
                    t.Battles++;
                    if (me.WinningSide == ks.MissionSide) t.ReliefWon++;
                    break;
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        internal static void OnSiegeEnded(SiegeEvent ev)
        {
            try { if (ev != null && _tracks.ContainsKey(ev)) Finish(ev, "koniec oblezenia"); }
            catch (Exception e) { Stumble("OnSiegeEnded", e); }
        }

        private static void Finish(SiegeEvent ev, string how)
        {
            Track t;
            if (ev == null || !_tracks.TryGetValue(ev, out t)) { if (ev != null) _tracks.Remove(ev); return; }
            _tracks.Remove(ev);
            try
            {
                // oblezenia 3: gra zwija oboz (KingdomManager.SiegeCompleted) PRZED zmiana pana - zdobycie ze znacznika MapEventEnded (Cape Wrath 15.10 "utrzymane" przy zdobyciu)
                bool cap = t.Captured || (t.St != null && !ReferenceEquals(t.St.MapFaction, t.K)) || SiegeWalls2.CapturedNow(t.St);
                if (ev != null) _endedRelief[ev] = new[] { t.LastC, Math.Max(t.Need0, t.LastNeed) };
                _dEnded++; if (cap) _dCaptured++;
                float pct = t.LastNeed > 0f ? 100f * t.CMax / Math.Max(t.Need0, t.LastNeed) : 0f;
                var sb = new StringBuilder(600);
                sb.Append("Odsiecz (OB5) koniec: dzien ").Append(Today).Append(" | ").Append(t.Name).Append(" (").Append(t.Id).Append(", ").Append(ClassName[t.Cls]).Append(") krolestwa ").Append(t.KName)
                  .Append(" | oblegajacy ").Append(t.BesFaction).Append(", wodz ").Append(t.BesLeader).Append(", sila na starcie ").Append(F0(t.Bes0)).Append(", najwiecej ").Append(F0(t.BesMax))
                  .Append(" | dob od wykrycia ").Append(Today - t.Day0)
                  .Append(" | odsiecz: wymagana ").Append(F0(Math.Max(t.Need0, t.LastNeed))).Append(", najwiecej zebrana ").Append(F0(t.CMax)).Append(" (").Append(pct.ToString("0", Inv)).Append("% wymaganej)")
                  .Append(", druzyn w zasiegu (z ocena obrony) ").Append(t.Eligible.Count).Append(", druzyn w odsieczy ").Append(t.Seen.Count)
                  .Append(", podbitych przez OB5 ").Append(t.Pulled.Count).Append(" (potem w odsieczy ").Append(t.Redirected.Count).Append("), podbic ").Append(t.Raised)
                  .Append(", armie krola ").Append(t.Armies)
                  .Append(" | bitwy pod murami (poza szturmem) ").Append(t.Battles).Append(", wygrala strona krolestwa ").Append(t.ReliefWon)
                  .Append(" | wynik: ").Append(cap ? "ZDOBYTE" : "utrzymane").Append(" (").Append(how).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Finish", e); }
        }

        /// <summary>KO / SK (oblezenia 3): zebrana odsiecz i wymagana dla tego oblezenia (ostatnie przeliczenie godziny OB5) albo "- (poza OB5)". Nie rzuca.</summary>
        /// <summary>m116-fabula (SiegeWalls2.HoldFresh): odsiecz zebrana w drodze co najmniej rowna wymaganej (OB5) - oblegajacy nie jest trzymany pod murami.
        /// Oblezenie poza OB5 - false. Tylko odczyt.</summary>
        internal static bool ReliefReady(SiegeEvent ev)
        {
            try
            {
                Track t;
                if (ev == null || !_tracks.TryGetValue(ev, out t)) return false;
                float need = Math.Max(t.Need0, t.LastNeed);
                return need > 0f && t.LastC >= need;
            }
            catch (Exception e) { Stumble("ReliefReady", e); return false; }
        }

        internal static string ReliefOnWay(SiegeEvent ev)
        {
            try
            {
                if (ev == null) return "-";
                Track t; float[] v;
                if (_tracks.TryGetValue(ev, out t)) return F0(t.LastC) + " / wymagana " + F0(Math.Max(t.Need0, t.LastNeed));
                if (_endedRelief.TryGetValue(ev, out v)) return F0(v[0]) + " / wymagana " + F0(v[1]);
                return "- (poza OB5)";
            }
            catch (Exception e) { Stumble("ReliefOnWay", e); return "?"; }
        }

        // ------------------------------------------------------------ linie
        internal static void SessionStart()
        {
            try
            {
                var s = S;
                string owners = "-";
                try
                {
                    if (_target != null)
                    {
                        var info = Harmony.GetPatchInfo(_target);
                        if (info != null) { var o = info.Owners.Distinct().ToList(); if (o.Count > 0) owners = string.Join("/", o.ToArray()); }
                    }
                }
                catch { owners = "?"; }
                string tsm = "-", amm = "-";
                try { tsm = Campaign.Current.Models.TargetScoreCalculatingModel.GetType().FullName; } catch { }
                try { amm = Campaign.Current.Models.ArmyManagementCalculationModel.GetType().FullName; } catch { }
                Log.Info("Odsiecz (OB5): start sesji | " + (On ? "CZYNNE" : "WYLACZONE (BesiegedReliefFirst=false)")
                         + " | latka CampaignEventDispatcher.AiHourlyTick (postfiks Low) " + (_patched ? "TAK" : "BRAK (" + (_missing ?? "?") + ")")
                         + "; wlasciciele latek celu: " + owners
                         + " | wymagana odsiecz x sila obozu: stolica " + (s != null ? s.BesiegedReliefCapitalForce : 0f).ToString("0.0#", Inv)
                         + ", miasto " + (s != null ? s.BesiegedReliefTownForce : 0f).ToString("0.0#", Inv)
                         + ", zamek " + (s != null ? s.BesiegedReliefCastleForce : 0f).ToString("0.0#", Inv) + " (0 = klasa poza regula)"
                         + "; pierwszenstwo 1.5 / 1.3 / 1.1, podstawa >= 0.5; armia wladcy " + (s != null && s.BesiegedReliefKingsArmy ? "TAK" : "NIE")
                         + "; odsiecz bez szans zwalnia druzyny (f12: zasieg krolestwa < min(wymagana, 1.2 x oboz), od 13 h po wykryciu) "
                         + (s != null && s.BesiegedReliefOnlyWhenReachable ? "TAK" : "NIE")
                         + " | model oceny celu: " + tsm + ", model armii: " + amm + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        internal static void Daily()
        {
            try
            {
                if (!On) { ClearDay(); return; }
                EnsureHour();
                bool gateOn = S != null && S.BesiegedReliefOnlyWhenReachable;
                int cap = 0, town = 0, castle = 0;
                var sb = new StringBuilder(900);
                var rows = new StringBuilder(600);
                int shown = 0;
                foreach (var kv in _byK)
                {
                    foreach (var sg in kv.Value)
                    {
                        if (sg.Cls == CCapital) cap++; else if (sg.Cls == CTown) town++; else castle++;
                        if (shown < 12)
                        {
                            if (shown > 0) rows.Append("; ");
                            rows.Append(Nm(sg.St)).Append(" (").Append(ClassName[sg.Cls]).Append(", ").Append(Nm(sg.K)).Append(") zebrane ").Append(F0(sg.Committed))
                                .Append(" / wymagane ").Append(F0(sg.Need)).Append(" (druzyn ").Append(sg.In.Count).Append(sg.ArmyOnWay ? ", z armia" : "").Append(")");
                            // f12: zasieg krolestwa wobec progu (tylko przy kluczu; bez znakow ";" i "|" - czytniki dziela wiersze po nich)
                            if (gateOn)
                            {
                                rows.Append(", zasieg krolestwa ").Append(F0(sg.Reach)).Append(" / prog ").Append(F0(sg.Floor));
                                if (!sg.Ready) rows.Append(" (rozruch - pierwsze 13 h)");
                                else if (sg.Reach < sg.Floor) rows.Append(" BEZ SZANS - zwolnionych dzis ").Append(sg.T != null ? sg.T.DayRel.Count : 0);
                            }
                            shown++;
                        }
                    }
                }
                int redir = 0;
                foreach (var kv in _tracks) redir += kv.Value.Redirected.Count;
                sb.Append("Odsiecz (OB5): dzien ").Append(Today).Append(" | CZYNNE | oblezone twierdze krolestw teraz: stolice ").Append(cap).Append(", miasta ").Append(town).Append(", zamki ").Append(castle)
                  .Append(" (pominiete: Inni ").Append(_undeadNow).Append(", klasa z mnoznikiem 0 ").Append(_offClassNow).Append(")")
                  .Append(" | dzis: oblezen nowych ").Append(_dStarted).Append(", skonczonych ").Append(_dEnded).Append(" (zdobytych ").Append(_dCaptured).Append(")")
                  .Append(", oceny obrony podbite ").Append(_dRaised).Append(" razy u ").Append(_dParties.Count).Append(" druzyn, pominiete z glodu ").Append(_dStarve)
                  .Append(", opcje armii wladcy ").Append(_dKingOffer).Append(" (wladca bez druzyn do wezwania ").Append(_dKingNoCall).Append("), armie wladcy zalozone ").Append(_dArmies)
                  .Append(" (odsiecz zamku pominieta przy wlasnym oblezeniu druzyny ").Append(_dOwnSiege).Append(")")
                  .Append(", odsiecz bez szans (zasieg krolestwa < min(wymagana, 1.2 x oboz)): zwolnione ").Append(_dRel).Append(" razy u ").Append(_dRelParties.Count).Append(" druzyn")
                  .Append(gateOn ? "" : " (Besieged Relief Only When Reachable NIE - bez zwalniania)")
                  .Append(", niegotowe (p9 - bez sprzetu, tylko odsiecz osady wlasnego rodu): pominiete odsiecze cudzych osad ").Append(_dUnready).Append(" razy u ").Append(_dUnreadyParties.Count).Append(" druzyn")
                  .Append(" | trwajace oblezenia: podbite druzyny potem w odsieczy ").Append(redir)
                  .Append(" | ").Append(shown > 0 ? rows.ToString() : "-")
                  .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }

        private static void ClearDay()
        {
            _dRaised = _dStarve = _dKingOffer = _dKingNoCall = _dArmies = _dStarted = _dEnded = _dCaptured = _dStumbles = _dOwnSiege = _dRel = _dUnready = 0;
            _dParties.Clear(); _endedRelief.Clear(); _dRelParties.Clear(); _dUnreadyParties.Clear();
            foreach (var kv in _tracks) kv.Value.DayRel.Clear();
        }
    }

    /// <summary>OB5 (odsiecz) i S11 (rody zmieniajace krolestwo z lennem) - nasluchy kampanii. Bez zapisu; stan sesji od zera przy kazdej nowej grze i wczytaniu.</summary>
    internal sealed class ReliefBehavior : CampaignBehaviorBase
    {
        public ReliefBehavior() { BesiegedRelief.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { BesiegedRelief.SessionStart(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, BesiegedRelief.Daily);
            CampaignEvents.ArmyCreated.AddNonSerializedListener(this, BesiegedRelief.OnArmyCreated);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, BesiegedRelief.OnArmyDispersed);
            CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(this, BesiegedRelief.OnSiegeEnded);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, BesiegedRelief.OnMapEventEnded);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, BesiegedRelief.OnOwnerChanged);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, KingdomBalance.OnClanChangedKingdom);   // S11 (tylko linia)
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
