using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 471a ZO "czas oblezenia z czynnikow" - KROK 1: SAM LOG (przewidywanie), BEZ ZMIANY ZACHOWANIA.
    /// Jeff 12.11: "tak ustaw takie cele i zrob test 90 dob ale co toznaczy maly zmaek lub duzy zamek??" oraz "i od czego zalezne beda doby??".
    /// Cele: maly zamek 20-40 dob, duzy zamek 40-90, miasto 60-150, wielka twierdza 60-150; limit bezpieczenstwa 1.5 x gorna granica.
    /// Termin obrony R = lo + (hi - lo) x s, s = (q - 0.5) / (0.9 - 0.5) obciete do 0..1, q = 0.35 qJ + 0.25 qM + 0.25 qS + 0.15 qMo:
    ///   qJ  jedzenie zalogi = min(1, FoodStocks / -FoodChange / limit kategorii) (bez ubytku = 1, zapas 0 = 0) - 471-p: wzgledem limitu, nie 365;
    ///   qM  mury = srednie HP odcinkow x poziom murow / 3;
    ///   qS  sily = 1 - ln(ludzie obozu / obroncy) / ln 40, obciete do 0..1 (471-p: ln 40 zamiast ln 20 - przewaga 20-40:1 jeszcze cos znaczy);
    ///       obroncy liczeni jak w grze: Settlement.GetInvolvedPartiesForEventType(Siege) (zaloga, milicja, druzyny strony obroncy);
    ///   qMo morale obroncow = Md / 100 (wzor SZ3 DefMorale: 50 + jedzenie + lojalnosc + zold + mury) BEZ skladnika odsieczy - odsiecz dziala tylko
    ///       przez zatrzymanie zegara (471-p: jedno zjawisko, jeden skutek).
    /// 471-p rozciagniecie q -> s (recenzja U1): w ROT 8.1.8 jedzenie zamku starcza na 486-807 dob (qJ = 1 zawsze), mury poziomu 2 bez trebuszy
    /// (qM = 0.67 stale) - surowe q siedzialo w 0.52-0.75 i dolna polowa celu byla nieosiagalna. Stale 0.5 / 0.9 do sprawdzenia na liniach
    /// "(471) start" z testu (linia podaje q i s).
    /// Odsiecz w drodze (ReliefStrength - ta sama miara co SZ3/OB5) >= polowy sily obozu = zegar R stoi (sprawdzana PRZED doliczeniem doby).
    /// Kategorie (D1 czeka na Jeffa): wielka twierdza = lista kanoniczna id ROT-Map 8.1.8 (TwierdzaIds); miasto = kazde inne miasto; zamek: mury
    /// poziom 1 = maly, 2-3 = duzy (dokladnie slowa zadania). W ROT 8.1.8 zaden zamek nie ma murow 1 na starcie - linia pokazuje tez wariant D1
    /// "maly = mury <= 2", zeby test pokazal oba podzialy.
    /// 471a byl tylko odczytem. 471b-4 (Jeff 13.11 D4 "wedlug twojej rady"; klucz SiegeTimeByFactors, MCM "Siege Time By Factors") - regula DZIALA na oblezeniach
    /// AI (Acts471: nie gracz, nie Inni, nie skrypt fabuly, nie osada klanu gracza do 471b-5):
    ///  (a) kapitulacja z terminu: zegar >= R, odsiecz nie stoi, oboz liczniejszy od obroncow (ludzie zdrowi, z milicja) -> Yield471;
    ///  (b) glod: pierwsza doba glodu po tygodniu laski GL, bez odsieczy, oboz liczniejszy -> Yield471 (StarveYield471 z SiegeWalls2.StarveOne);
    ///  (c) bramka szturmu w Veto (Gate471): przed dolna granica kategorii bez szturmu, potem maly zamek jak dotad, wiekszy tylko przez wyrwe (bez wyrwy - plan
    ///      trebuszy TryBreach); zawsze wolno przy pustych murach i glodzie obroncow; stoi PRZED 307c, DR i OB1 (MB/Unblock konczy w Veto, wiec tez jej podlega);
    ///  (d) odstapienie - bez zmian (glod obozu m116, odsiecz OB5/334e);
    ///  (e) limit: wiek >= SiegeTimeLimitFactor x gorna granica - oboz liczniejszy: Yield471, inaczej Lift471 (RemoveAllSiegeParties + rozejm 30 dob, droga 384).
    /// Yield471 (D4, kapitulacja z honorami): partie lordow obroncow wychodza, zaloga do swoich (BookHoldings.YieldOut), potem KingdomManager.SiegeCompleted -
    /// droga 374 "slowo dotrzymane" bez relacji gracza. Szturmem zdobyte - niewola jak w grze (bez zmian).
    /// U2: zegar i R licza sie przed LogOn (akcje takze bez logu). U8: zegar i doby z odsiecza w zapisie (dopisek "T|" do arm_siege_walls; brak = zegar od wieku).
    /// Klucz NIE = zachowanie co do bitu jak przed (471a: tylko linie). Oblezenia gracza, Innych i skryptu fabuly - tylko "poza regula".
    /// Wyjatek per oblezenie = Stumble i pominiecie tego oblezenia.
    /// </summary>
    internal static partial class SiegeWalls
    {
        /// <summary>Wielkie twierdze (ROT-Map 8.1.8 settlements.xml): Casterly Rock, King's Landing + Red Keep, Winterfell, Riverrun, The Twins, Storm's End,
        /// Highgarden, Dragonstone, The Eyrie, Sunspear, Pyke, Oldtown.</summary>
        private static readonly HashSet<string> TwierdzaIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "castle_V5", "town_EW4", "ROT_castle61", "town_B2", "town_V2", "ROT_town3", "town_EW3", "town_V5", "town_EW1", "town_EN1", "town_A1", "town_S3", "town_V6"
        };
        /// <summary>471b-1: wielka twierdza z listy 471 (SmallCastles471 jej nie obniza).</summary>
        internal static bool IsTwierdza471(Settlement st) { return st != null && st.StringId != null && TwierdzaIds.Contains(st.StringId); }
        private static readonly string[] CatName = { "maly zamek", "duzy zamek", "miasto", "wielka twierdza" };
        private static readonly int[] CatLo = { 20, 40, 60, 60 }, CatHi = { 40, 90, 150, 150 };
        private const double Limit471 = 1.5;   // qJ: jedzenie zalogi wzgledem 1.5 x gornej granicy (kalibracja 471-p, niezalezna od klucza limitu)
        // 471b-4 (D4 i bramka/limit): klucz SiegeTimeByFactors, limit = SiegeTimeLimitFactor x gorna granica (1..3, domyslnie 1.5)
        internal static bool TimeOn { get { var s = Settings.Current; return s != null && s.SiegeTimeByFactors; } }
        private static bool PlayerHoldingsOn { get { var s = Settings.Current; return s != null && s.SiegeTimeRulesPlayerHoldings; } }   // 471b-5 (D5)
        private static double LimitK { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeTimeLimitFactor) ? 1.5 : Clamp(s.SiegeTimeLimitFactor, 1.0, 3.0); } }
        internal const int LiftTruceDays = 30;   // 471b-4 (e): oboz slabszy niz obroncy odchodzi po limicie - rozejm tej osady dla jego frakcji i rodow (droga 384)
        private static string _yield471;          // okno SiegeCompleted kapitulacji 471b (BesiegedRelief: wynik "ZDOBYTE" bez bitwy)
        private static int _d471Yield, _d471Lift, _d471Gate, _d471Refused, _d471Starve;
        private static readonly List<string[]> _pend471 = new List<string[]>();   // U8: zegar z zapisu (osada, start w godzinach, zegar, doby z odsiecza) - do pierwszej doby
        private const double Q471Lo = 0.5, Q471Hi = 0.9;   // 471-p: rozciagniecie q na pelny cel kategorii (kalibracja z testu)

        private sealed class T471
        {
            public string St = "-", Why; public int Cat, Lvl, Seen = -1; public double Start, Clock, R0, R, Q0, S0; public bool Over, OverLimit, Paused, PlayerClan;
            public int PausedDays;
            public double Ratio = -1; public string EndWay;   // 471b-4: stosunek ludzi oboz / obroncy (ostatnia doba), droga konca (a-e) do linii konca
            public bool Started;                              // 471b-4: pierwsza doba juz byla (rekord moze powstac wczesniej w bramce szturmu)
        }
        private static readonly Dictionary<SiegeEvent, T471> _t471 = new Dictionary<SiegeEvent, T471>();
        private static int _d471Start, _d471End, _d471Out;

        private static int Cat471(Settlement st, out int lvl)
        {
            lvl = 0;
            try { if (st.Town != null) lvl = st.Town.GetWallLevel(); } catch { }
            if (st.StringId != null && TwierdzaIds.Contains(st.StringId)) return 3;
            if (st.IsTown) return 2;
            return lvl <= 1 ? 0 : 1;
        }

        /// <summary>Czynniki i termin R dla jednego oblezenia; txt - czynniki do linii.</summary>
        private static double R471(Settlement st, SiegeEvent ev, int cat, int lvl, out double q, out double sq, out bool paused, out string txt, out double ratioOut)
        {
            var t = st.Town;
            // (1) jedzenie zalogi
            double fs = 0, ch = 0, foodDays = -1, qJ;
            try { fs = t.FoodStocks; ch = t.FoodChange; } catch { }
            if (fs <= 0) qJ = 0;
            else if (ch >= 0) qJ = 1;
            else { foodDays = fs / -ch; qJ = Clamp(foodDays / (Limit471 * CatHi[cat]), 0, 1); }
            // (2) mury
            double hp = WallHp(st), qM = Clamp(hp * lvl / 3.0, 0, 1);
            // (3) sily: ludzie obozu / obroncy z milicja
            long att = 0, def = 0; double campFood = 0, campCh = 0; int trebs = 0;
            var camp = ev.BesiegerCamp;
            if (camp != null)
                foreach (var pb in camp.GetInvolvedPartiesForEventType(TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes.Siege))
                {
                    if (pb == null || pb.MemberRoster == null) continue;
                    att += pb.MemberRoster.TotalHealthyCount;
                    try { if (pb.IsMobile) { campFood += pb.MobileParty.Food; campCh += pb.MobileParty.FoodChange; } } catch { }
                }
            foreach (var pb in st.GetInvolvedPartiesForEventType(TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes.Siege))   // 471-p U6: strona obroncy jak w grze
                if (pb != null && pb.MemberRoster != null) def += pb.MemberRoster.TotalHealthyCount;
            try
            {
                var side = ev.GetSiegeEventSide(BattleSideEnum.Attacker);
                if (side != null && side.SiegeEngines != null)
                    foreach (var x in side.SiegeEngines.AllSiegeEngines())
                        if (x != null && x.IsConstructed && x.SiegeEngine == DefaultSiegeEngineTypes.Trebuchet) trebs++;
            }
            catch { }
            double ratio = def <= 0 ? 999.0 : att / (double)def;
            ratioOut = ratio;
            double qS = Clamp(1.0 - Math.Log(Math.Max(1.0, ratio)) / Math.Log(40.0), 0, 1);
            // (4) odsiecz w drodze - wspolna funkcja SZ3/OB5 (471-p U7: me == null = bez warunku bitwy)
            double bes, rel = ReliefStrength(st, null, out bes);
            paused = rel > 0 && bes > 0 && rel >= 0.5 * bes;
            // morale obroncow (wzor SZ3 DefMorale)
            double mFood = fs <= 0 ? -25 : ch >= 0 ? 10 : foodDays >= 28.0 ? 10.0 : foodDays < 7.0 ? -10.0 * (7.0 - foodDays) / 7.0 : 0.0;
            double mLoy = 0, mPay = 0;
            try { mLoy = (Clamp(t.Loyalty, 0.0, 100.0) - 50.0) * 0.4; } catch { }
            try { var g = t.GarrisonParty; mPay = g != null ? -20.0 * Clamp(g.HasUnpaidWages, 0.0, 1.0) : 0.0; } catch { }
            double md = Clamp(50.0 + mFood + mLoy + mPay - 15.0 * (1.0 - hp), 0, 100), qMo = md / 100.0;   // 471-p U5: bez odsieczy (ta stawia zegar)
            q = 0.35 * qJ + 0.25 * qM + 0.25 * qS + 0.15 * qMo;
            sq = Clamp((q - Q471Lo) / (Q471Hi - Q471Lo), 0, 1);
            double r = CatLo[cat] + (CatHi[cat] - CatLo[cat]) * sq;
            double campDays = campCh < 0 ? campFood / -campCh : -1;
            txt = "qJ " + F(qJ, "0.00") + " (jedzenie zalogi " + (fs <= 0 ? "0 - glod" : ch >= 0 ? "bez ubytku" : F(foodDays, "0") + " dob") + ", " + F(fs, "0") + " / " + F(ch, "0.0") + "/d)"
                + ", qM " + F(qM, "0.00") + " (mury poziom " + lvl + ", HP " + F(hp * 100, "0") + "%)"
                + ", qS " + F(qS, "0.00") + " (oboz " + att + " / obroncy (strona gry, z milicja) " + def + " = " + (def <= 0 ? "pusta" : F(ratio, "0.0") + ":1") + ")"
                + ", qMo " + F(qMo, "0.00") + " (Md bez odsieczy " + F(md, "0") + ")"
                + " | odsiecz w drodze " + F(rel, "0") + " / sila obozu " + F(bes, "0") + (paused ? " -> ZEGAR STOI" : "")
                + " | jedzenie obozu " + (campCh < 0 ? F(campDays, "0.0") + " dob" : "bez ubytku") + " (" + F(campFood, "0") + ", " + F(campCh, "0.0") + "/d)"
                + " | trebusze obozu " + trebs + ", najslabszy odcinek " + F(WallHpMin(st) * 100, "0") + "%";
            return r;
        }

        /// <summary>471-p U3: powod "poza regula" albo null. Gracz: wodz obozu, PlayerSiege, druzyna gracza w obozie, oblega te osade albo w niej stoi.
        /// Bez IsPlayerSiegeEvent (rzuca NRE przy LeaderParty == null - tu lp sprawdzony wyzej).</summary>
        private static string Out471(SiegeEvent ev, Settlement st, MobileParty lp)
        {
            var main = MobileParty.MainParty;
            if (lp.IsMainParty || PlayerSiege.PlayerSiegeEvent == ev) return "gracz";
            if (main != null && (main.BesiegedSettlement == st || main.CurrentSettlement == st)) return "gracz";
            var camp = ev.BesiegerCamp;
            if (camp != null && main != null)
                foreach (var pb in camp.GetInvolvedPartiesForEventType(TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes.Siege))
                    if (pb != null && pb.IsMobile && pb.MobileParty == main) return "gracz";
            if (Und(lp, st)) return "Inni";
            if (Scripted(lp)) return "skrypt fabuly";
            return null;
        }

        /// <summary>471b: czy regula 471b DZIALA na tym oblezeniu (furaz 471b-3, kapitulacja / bramka / limit 471b-4). Powod pominiecia albo null:
        /// brak wodza obozu, gracz / Inni / skrypt fabuly (Out471), osada klanu gracza przy kluczu SiegeTimeRulesPlayerHoldings NIE (471b-5, D5). Nie rzuca.</summary>
        internal static string Acts471(SiegeEvent ev)
        {
            try
            {
                var st = ev != null ? ev.BesiegedSettlement : null;
                if (st == null || st.SiegeEvent != ev || st.Town == null) return "brak oblezenia";
                var lp = ev.BesiegerCamp != null ? ev.BesiegerCamp.LeaderParty : null;
                if (lp == null) return "oboz bez wodza";
                string why = Out471(ev, st, lp);
                if (why != null) return why;
                if (st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan && !PlayerHoldingsOn) return "osada klanu gracza (klucz D5 NIE)";   // 471b-5: D5 "tak" - te same reguly
                return null;
            }
            catch (Exception e) { Stumble("Acts471", e); return "blad"; }
        }

        /// <summary>Rekord 471 oblezenia (tworzony przy pierwszym uzyciu: doba albo bramka szturmu). U8: zegar z zapisu, jesli jest (osada i start zgodne).</summary>
        private static T471 Rec471(SiegeEvent ev, Settlement st, out bool fresh)
        {
            T471 r;
            fresh = !_t471.TryGetValue(ev, out r);
            if (!fresh) return r;
            r = new T471 { St = st.StringId, Start = ev.SiegeStartTime.ToDays };
            r.Cat = Cat471(st, out r.Lvl);
            r.PlayerClan = st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan;   // 471-p U4: znacznik (D5)
            r.Clock = Math.Max(0.0, ev.SiegeStartTime.ElapsedDaysUntilNow);         // bez zapisu: zegar od wieku oblezenia
            for (int i = 0; i < _pend471.Count; i++)
            {
                var a = _pend471[i];
                double h;
                if (a[0] != st.StringId || !double.TryParse(a[1], System.Globalization.NumberStyles.Float, Inv, out h) || Math.Abs(h - ev.SiegeStartTime.ToHours) > 0.05) continue;
                double c; int pd;
                if (double.TryParse(a[2], System.Globalization.NumberStyles.Float, Inv, out c) && c >= 0.0) r.Clock = Math.Min(r.Clock, c);
                if (int.TryParse(a[3], System.Globalization.NumberStyles.Integer, Inv, out pd)) r.PausedDays = pd;
                _pend471.RemoveAt(i);
                break;
            }
            _t471[ev] = r;
            return r;
        }

        /// <summary>Z Daily (471b-4: PRZED LogOn - zegar, R i kapitulacje dzialaja takze bez logu; recenzja U2): start (raz na oblezenie), zegar doby, linia doby 471
        /// i drogi konca 471b: (a) kapitulacja z terminu R (zegar >= R, odsiecz nie stoi, oboz liczniejszy), (e) limit (wiek >= LimitK x gorna granica: oboz liczniejszy -
        /// kapitulacja, inaczej odejscie z rozejmem). Akcje po petli (SiegeCompleted / RemoveAllSiegeParties zmieniaja oblezenia).</summary>
        private static void Time471Daily()
        {
            bool log = LogOn, act = TimeOn;
            var todo = new List<KeyValuePair<SiegeEvent, string>>();
            try
            {
                List<SiegeEvent> dead = null;
                foreach (var kv in _t471) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) _t471.Remove(ev);
                int today = Today, going = 0, over = 0, overLim = 0, paused = 0, outside = 0;
                var perCat = new int[4];
                var sbList = new StringBuilder(600);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsFortification || st.SiegeEvent == null) continue;
                    var ev = st.SiegeEvent;
                    try
                    {
                        var lp = ev.BesiegerCamp != null ? ev.BesiegerCamp.LeaderParty : null;
                        if (lp == null) continue;                                    // 471-p U3: oboz bez wodza (gra konczy takie oblezenie) - pomijam bez potkniecia
                        bool made;
                        T471 r = Rec471(ev, st, out made);
                        if (r.Seen == today) continue;
                        bool fresh = !r.Started;                                     // pierwsza doba rekordu (takze gdy powstal w bramce szturmu)
                        r.Started = true;
                        r.Seen = today;
                        bool newOut = false;
                        if (r.Why == null) { r.Why = Out471(ev, st, lp); newOut = r.Why != null; }   // 471-p U3: co dobe; raz poza regula - zostaje
                        if (r.Why != null)
                        {
                            outside++;
                            if (newOut)
                            {
                                _d471Out++;
                                if (log) Log.Info("Czas oblezenia (471) " + (fresh ? "start" : "poza regula od teraz") + ": dzien " + today + " | osada " + st.StringId + " (" + Nm(st) + ") | poza regula (" + r.Why + ")"
                                    + (fresh ? "" : " | wiek " + F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0") + " dob, zegar " + F(r.Clock, "0") + ", R " + F(r.R, "0.0")) + ".");
                            }
                            continue;
                        }
                        going++; perCat[r.Cat]++;
                        double q, sq, ratio; bool pz; string txt;
                        double R = R471(st, ev, r.Cat, r.Lvl, out q, out sq, out pz, out txt, out ratio);
                        if (!fresh && !pz) r.Clock += 1.0;                           // 471-p U8: odsiecz sprawdzona dzis, przed doliczeniem doby
                        r.R = R; r.Paused = pz; r.Ratio = ratio;
                        if (pz) { r.PausedDays++; paused++; }
                        double age = ev.SiegeStartTime.ElapsedDaysUntilNow, lim = LimitK * CatHi[r.Cat];
                        string acts = act ? Acts471(ev) : "klucz Siege Time By Factors NIE";   // 471b: osada klanu gracza (D5) - zegar i linie tak, akcje nie
                        if (fresh)
                        {
                            r.R0 = R; r.Q0 = q; r.S0 = sq; _d471Start++;
                            if (log) Log.Info("Czas oblezenia (471) start: dzien " + today + " | osada " + st.StringId + " (" + Nm(st) + ")" + (r.PlayerClan ? " [osada klanu gracza - D5]" : "")
                                + " | kategoria " + CatName[r.Cat] + " (mury " + r.Lvl + "), cel " + CatLo[r.Cat] + "-" + CatHi[r.Cat] + " dob, limit " + F(lim, "0")
                                + " | wodz " + Hn(lp) + " | wiek " + F(age, "0.0") + " dob"
                                + " | termin obrony R " + F(R, "0.0") + " dob (q " + F(q, "0.00") + " -> s " + F(sq, "0.00") + ") | " + txt + " | " + SiegeCamp471.StartText(ev)
                                + " | 471b: " + (acts == null ? "regula DZIALA (szturm od " + CatLo[r.Cat] + " doby" + (r.Cat == 0 ? "" : ", potem tylko przez wyrwe") + ", kapitulacja z terminu / glodu, limit)" : "bez akcji (" + acts + ") - tylko przewidywanie") + ".");
                        }
                        if (r.Clock >= R)
                        {
                            over++;
                            if (!r.Over)
                            {
                                r.Over = true;
                                if (log) Log.Info("Czas oblezenia (471) termin minal: dzien " + today + " | osada " + st.StringId + " (" + Nm(st) + ", " + CatName[r.Cat] + ")" + (r.PlayerClan ? " [osada klanu gracza - D5]" : "")
                                    + " | zegar " + F(r.Clock, "0") + " >= R " + F(R, "0.0") + " (wiek " + F(age, "0.0") + ") | " + txt
                                    + " | 471b: " + (acts != null ? "bez akcji (" + acts + ")" : pz ? "odsiecz w drodze - czeka" : ratio > 1.0 ? "KAPITULACJA z terminu" : "oboz nie liczniejszy od obroncow - czeka (limit)") + ".");
                            }
                        }
                        if (age >= lim) { overLim++; r.OverLimit = true; }
                        if (acts == null)
                        {
                            if (age >= lim) todo.Add(new KeyValuePair<SiegeEvent, string>(ev, ratio > 1.0 ? "e" : "e-odejscie"));
                            else if (r.Clock >= R && !pz && ratio > 1.0) todo.Add(new KeyValuePair<SiegeEvent, string>(ev, "a"));
                        }
                        if (sbList.Length < 3000) sbList.Append(sbList.Length > 0 ? "; " : "").Append(st.StringId).Append(' ').Append(CatName[r.Cat]).Append(' ').Append(F(age, "0")).Append('/').Append(F(R, "0")).Append(" dob").Append(pz ? " (odsiecz)" : "");
                    }
                    catch (Exception e) { Stumble("Time471." + (st.StringId ?? "?"), e); }
                }
                // 471b-4: drogi konca (po petli)
                foreach (var kv in todo)
                {
                    try
                    {
                        var ev = kv.Key;
                        if (ev.BesiegedSettlement == null || ev.BesiegedSettlement.SiegeEvent != ev) continue;
                        string info;
                        if (kv.Value == "e-odejscie") Lift471(ev, out info);
                        else Yield471(ev, kv.Value == "a" ? "a - termin R minal" : "e - limit " + F(LimitK, "0.0") + " x gorna granica", out info);
                    }
                    catch (Exception e) { Stumble("Time471.Act", e); }
                }
                if (log)
                    Log.Info("Czas oblezen (471 doba): dzien " + today + " | klucz 471b " + (act ? "TAK" : "NIE") + " | trwajace w regule " + going + " (maly zamek " + perCat[0] + ", duzy zamek " + perCat[1] + ", miasto " + perCat[2] + ", wielka twierdza " + perCat[3]
                        + "), poza regula " + outside + " | po terminie R " + over + ", ponad limitem " + overLim + ", zegar stoi (odsiecz) " + paused
                        + " | dzis: start " + _d471Start + ", koniec " + _d471End + ", poza regula " + _d471Out
                        + " | 471b: kapitulacje " + _d471Yield + " (z glodu " + _d471Starve + "), odejscia po limicie " + _d471Lift + ", wstrzymane " + _d471Refused + ", szturmy zatrzymane bramka " + _d471Gate
                        + " | wiek/R: " + (sbList.Length == 0 ? "-" : sbList.ToString()) + ".");
            }
            catch (Exception e) { Stumble("Time471Daily", e); }
            finally { _d471Start = _d471End = _d471Out = _d471Yield = _d471Lift = _d471Gate = _d471Refused = _d471Starve = 0; }
        }

        // ------------------------------------------------------------ 471b-4: drogi konca
        /// <summary>Ludzie zdrowi obozu / obroncow (strona gry, z milicja); 999 - mury puste.</summary>
        private static double Ratio471(SiegeEvent ev, Settlement st)
        {
            long att = 0, def = 0;
            var camp = ev.BesiegerCamp;
            if (camp != null)
                foreach (var pb in camp.GetInvolvedPartiesForEventType(TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes.Siege))
                    if (pb != null && pb.MemberRoster != null) att += pb.MemberRoster.TotalHealthyCount;
            foreach (var pb in st.GetInvolvedPartiesForEventType(TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes.Siege))
                if (pb != null && pb.MemberRoster != null) def += pb.MemberRoster.TotalHealthyCount;
            return def <= 0 ? 999.0 : att / (double)def;
        }

        private static bool InBattle471(Settlement st, MobileParty lp)
        {
            if (lp != null && lp.MapEvent != null) return true;
            if (st.Party != null && st.Party.MapEvent != null) return true;
            var g = st.Town != null ? st.Town.GarrisonParty : null;
            if (g != null && g.MapEvent != null) return true;
            var mc = st.MilitiaPartyComponent;
            var m = mc != null ? mc.MobileParty : null;
            return m != null && m.MapEvent != null;
        }

        /// <summary>BesiegedRelief: oblezenie zwijane przez kapitulacje 471b (SiegeCompleted bez bitwy) - twierdza przechodzi do oblegajacych.</summary>
        internal static bool Yielding471(Settlement st) { return _yield471 != null && st != null && st.StringId == _yield471; }

        /// <summary>471b-4 (D4): kapitulacja z honorami - partie lordow obroncow wychodza (SiegeSurrender374.LeadOutLords), zaloga do swoich (BookHoldings.YieldOut:
        /// najblizsze druzyny i zalogi starego pana, bez nich do domu; ksiega ludzi i zbrojownia z ludzmi), bohaterowie bez druzyny do najblizszej wlasnej twierdzy,
        /// potem KingdomManager.SiegeCompleted (ta sama droga co 374 "slowo dotrzymane": koniec oblezenia, ApplyBySiege, bez bitwy - bez lupu i rzezi).
        /// true - osada przeszla. Nie rzuca.</summary>
        private static bool Yield471(SiegeEvent ev, string way, out string info)
        {
            info = "-";
            var st = ev != null ? ev.BesiegedSettlement : null;
            try
            {
                var lp = ev != null && ev.BesiegerCamp != null ? ev.BesiegerCamp.LeaderParty : null;
                if (st == null || lp == null || !lp.IsActive) { info = "brak obozu"; _d471Refused++; return false; }
                if (InBattle471(st, lp)) { info = "bitwa w toku"; _d471Refused++; return false; }
                if (lp.MapFaction == null || st.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(lp.MapFaction, st.MapFaction)) { info = "pokoj miedzy stronami"; _d471Refused++; return false; }
                T471 r;
                if (_t471.TryGetValue(ev, out r)) r.EndWay = "KAPITULACJA (" + way + ")";
                var oldClan = st.OwnerClan;
                double age = ev.SiegeStartTime.ElapsedDaysUntilNow;
                double ratio = Ratio471(ev, st);
                int lords = SiegeSurrender374.LeadOutLords(st, lp);
                string out1 = BookHoldings.YieldOut(st, lp.MapFaction, null, "471-" + st.StringId);
                _yield471 = st.StringId;
                try { Campaign.Current.KingdomManager.SiegeCompleted(st, lp, true, TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes.Siege); }
                finally { _yield471 = null; }
                bool ok = st.SiegeEvent != ev && st.MapFaction == lp.MapFaction;
                _d471Yield++;
                if (oldClan != null && oldClan == Clan.PlayerClan)   // 471b-5 (D5): osada rodu gracza
                    try { Log.Player(Nm(st) + " has yielded to " + Hn(lp) + " after " + F(age, "0") + " days of siege. The garrison marches out under arms to the lords of your realm.", true); } catch { }
                info = (ok ? "osada przeszla" : "SiegeCompleted bez zmiany pana") + " | partie lordow wyprowadzone " + lords + " | " + out1;
                if (LogOn)
                    Log.Info("Czas oblezenia (471b) kapitulacja: dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ") | droga " + way + " | po " + F(age, "0.0") + " dob"
                             + (r != null ? ", " + CatName[r.Cat] + " (cel " + CatLo[r.Cat] + "-" + CatHi[r.Cat] + "), zegar " + F(r.Clock, "0") + ", R " + F(r.R, "0.0") : "")
                             + " | oboz / obroncy " + F(ratio, "0.0") + ":1 | zdobywca " + Hn(lp) + " (" + lp.MapFaction.StringId + "), pan " + (oldClan != null ? oldClan.StringId : "-") + " -> " + (st.OwnerClan != null ? st.OwnerClan.StringId : "-")
                             + " | " + info + " | wyjscie z honorami (bez niewoli, bez bitwy).");
                return ok;
            }
            catch (Exception e) { Stumble("Yield471", e); _yield471 = null; info = "blad"; return false; }
        }

        /// <summary>471b-4 (e): limit przy obozie nie liczniejszym od obroncow - oboz zwiniety droga gry (SiegeRansom.Lift384 = RemoveAllSiegeParties) i rozejm tej
        /// osady dla frakcji i rodow obozu na LiftTruceDays dob (SiegeRansom.Truce384 - ocena celu 0, zawrocenie druzyn w drodze). Nie rzuca.</summary>
        private static bool Lift471(SiegeEvent ev, out string info)
        {
            info = "-";
            var st = ev != null ? ev.BesiegedSettlement : null;
            try
            {
                var camp = ev != null ? ev.BesiegerCamp : null;
                var lp = camp != null ? camp.LeaderParty : null;
                if (st == null || camp == null || lp == null) { info = "brak obozu"; _d471Refused++; return false; }
                if (InBattle471(st, lp)) { info = "bitwa w toku"; _d471Refused++; return false; }
                T471 r;
                if (_t471.TryGetValue(ev, out r)) r.EndWay = "ODEJSCIE po limicie";
                double age = ev.SiegeStartTime.ElapsedDaysUntilNow, ratio = Ratio471(ev, st);
                var keys = SiegeRansom.CampKeys384(camp);
                string li;
                bool ended = SiegeRansom.Lift384(st, out li);
                int turned = ended ? SiegeRansom.Truce384(st, keys, Today + LiftTruceDays) : 0;
                _d471Lift++;
                if (ended && st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan)   // 471b-5 (D5): osada rodu gracza
                    try { Log.Player("The siege of " + Nm(st) + " is lifted: " + Hn(lp) + "'s host gives up and marches away.", false); } catch { }
                info = li + (ended ? ", rozejm " + LiftTruceDays + " dob (" + string.Join(", ", keys.ToArray()) + "), zawrocone " + turned : "");
                if (LogOn)
                    Log.Info("Czas oblezenia (471b) odejscie po limicie: dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ") | po " + F(age, "0.0") + " dob (limit " + (r != null ? F(LimitK * CatHi[r.Cat], "0") : "-") + ")"
                             + " | oboz / obroncy " + F(ratio, "0.0") + ":1 (nie liczniejszy) | wodz " + Hn(lp) + " | " + info + ".");
                return ended;
            }
            catch (Exception e) { Stumble("Lift471", e); info = "blad"; return false; }
        }

        /// <summary>471b-4 (b): z SiegeWalls2.StarveOne - pierwsza doba glodu PO tygodniu laski GL: zaloga kapituluje z honorami (zamiast ok. 45 dob umierania), gdy
        /// regula dziala (Acts471), odsiecz nie stoi (miara zegara R) i oboz jest liczniejszy od obroncow. Opis do linii GL albo null (glod GL jak dotad). Nie rzuca.</summary>
        internal static string StarveYield471(SiegeEvent ev)
        {
            try
            {
                if (!TimeOn || ev == null || Acts471(ev) != null) return null;
                var st = ev.BesiegedSettlement;
                double bes, rel = ReliefStrength(st, null, out bes);
                if (rel > 0 && bes > 0 && rel >= 0.5 * bes) return null;               // odsiecz w drodze - zaloga czeka (jak zegar R)
                if (Ratio471(ev, st) <= 1.0) return null;
                string info;
                if (!Yield471(ev, "b - glod po tygodniu laski", out info)) return null;
                _d471Starve++;
                return "KAPITULACJA Z HONORAMI (471b, glod) - " + info;
            }
            catch (Exception e) { Stumble("StarveYield471", e); return null; }
        }

        /// <summary>471b-4 (c): bramka szturmu z SiegeWalls.Veto (za przerwa po odparciu, przed 307c / DR / OB1). Powod wstrzymania albo null (decyzja dalej jak dotad):
        /// przed dolna granica kategorii - bez szturmu; od niej maly zamek jak dotad, wiekszy tylko przez wyrwe (bez wyrwy: zmiana planu na trebusze - TryBreach).
        /// Zawsze wolno: puste mury (obroncy 0) i glod obroncow (Food 0). Nie rzuca (wyjatek = null).</summary>
        private static string Gate471(BesiegerCamp camp, SiegeEvent ev, Settlement st)
        {
            try
            {
                if (!TimeOn || Acts471(ev) != null) return null;
                bool fresh;
                var r = Rec471(ev, st, out fresh);
                if (Ratio471(ev, st) >= 999.0) return null;                            // puste mury
                if (st.Town.FoodStocks <= 0f) return null;                              // glod obroncow
                double age = ev.SiegeStartTime.ElapsedDaysUntilNow;
                if (age < CatLo[r.Cat])
                {
                    _d471Gate++;
                    return "471b: przed dolna granica kategorii " + CatName[r.Cat] + " (" + F(age, "0.0") + " < " + CatLo[r.Cat] + " dob) - szturm dopiero od niej, przy glodzie obroncow albo pustych murach";
                }
                if (r.Cat == 0 || WallHpMin(st) <= 0.0) return null;
                _d471Gate++;
                string sw = TryBreach(camp, Measure(camp));
                return "471b: " + CatName[r.Cat] + " bez wyrwy (najslabszy odcinek " + F(WallHpMin(st) * 100.0, "0") + "%) - szturm tylko przez wyrwe albo przy glodzie obroncow" + (sw != null ? " | ZMIANA: " + sw : "");
            }
            catch (Exception e) { Stumble("Gate471", e); return null; }
        }

        // ------------------------------------------------------------ U8: zegar 471 w zapisie (dopisek do arm_siege_walls)
        private static void Export471(StringBuilder sb)
        {
            foreach (var kv in _t471)
            {
                var ev = kv.Key; var r = kv.Value;
                if (ev == null || r == null || r.Why != null || ev.BesiegedSettlement == null || ev.BesiegedSettlement.SiegeEvent != ev) continue;
                sb.Append(";T|").Append(r.St).Append('|').Append(ev.SiegeStartTime.ToHours.ToString("R", Inv)).Append('|').Append(r.Clock.ToString("R", Inv)).Append('|').Append(r.PausedDays.ToString(Inv));
            }
        }

        /// <summary>Z SiegeWalls.Import: wpis "T|osada|start|zegar|doby z odsiecza" - true, gdy rozpoznany.</summary>
        private static bool Import471(string[] a)
        {
            if (a.Length < 5 || a[0] != "T") return false;
            _pend471.Add(new[] { a[1], a[2], a[3], a[4] });
            return true;
        }

        /// <summary>Z OnSiegeEnded: faktyczny czas wobec przewidywanego R (przyczyna konca - droga 471b albo linia KO oblezen 3 tuz przed).</summary>
        private static void Time471End(SiegeEvent ev)
        {
            try
            {
                T471 r;
                if (!_t471.TryGetValue(ev, out r)) return;
                _t471.Remove(ev);
                if (!LogOn || r.Why != null) return;
                _d471End++;
                var st = ev.BesiegedSettlement;
                double age = 0; try { age = ev.SiegeStartTime.ElapsedDaysUntilNow; } catch { }
                int c = r.Cat;
                string vs = age < CatLo[c] ? "PONIZEJ celu" : age > CatHi[c] ? "POWYZEJ celu" : "w celu";
                Log.Info("Czas oblezenia (471) koniec: dzien " + Today + " | osada " + r.St + " (" + Nm(st) + ", " + CatName[c] + ", mury " + r.Lvl + ") | trwalo " + F(age, "0.0") + " dob, " + vs + " " + CatLo[c] + "-" + CatHi[c]
                    + " | droga 471b: " + (r.EndWay ?? "inna (szturm / odejscie / gra - patrz linia KO)")
                    + " | R na starcie " + F(r.R0, "0.0") + " (q " + F(r.Q0, "0.00") + " -> s " + F(r.S0, "0.00") + "), R ostatnie " + F(r.R, "0.0") + ", zegar " + F(r.Clock, "0") + (r.Over ? " (termin minal)" : "")
                    + ", dob z odsiecza " + r.PausedDays + (r.OverLimit ? ", PONAD LIMITEM " + F(LimitK * CatHi[c], "0") : "") + ", oboz / obroncy ostatnio " + (r.Ratio < 0 ? "-" : F(r.Ratio, "0.0") + ":1") + " | HP murow " + F(WallHp(st) * 100, "0") + "%"
                    + " | wlasciciel w chwili konca " + (st != null && st.OwnerClan != null ? st.OwnerClan.StringId : "-")
                    + " (gra zmienia wlasciciela PO tym zdarzeniu - zdobycie widac w linii KO)"   // 471-p U10: SiegeCompleted: RemoveAllSiegeParties -> koniec oblezenia -> ApplyBySiege
                    + (r.PlayerClan ? " [osada klanu gracza - D5]" : "") + " | " + SiegeCamp471.EndText(ev) + " | " + SiegeForage471.EndText(ev) + ".");
            }
            catch (Exception e) { Stumble("Time471End", e); }
        }
    }
}
