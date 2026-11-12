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
    /// Tylko odczyt: zadnej latki, zadnego zapisu w grze, zadnego zapisu w save (pamiec 471 po wczytaniu od nowa, zegar od wieku oblezenia).
    /// Oblezenia gracza, Innych i skryptu fabuly - tylko liczone jako "poza regula". Wyjatek per oblezenie = Stumble i pominiecie tego oblezenia.
    /// </summary>
    internal static partial class SiegeWalls
    {
        /// <summary>Wielkie twierdze (ROT-Map 8.1.8 settlements.xml): Casterly Rock, King's Landing + Red Keep, Winterfell, Riverrun, The Twins, Storm's End,
        /// Highgarden, Dragonstone, The Eyrie, Sunspear, Pyke, Oldtown.</summary>
        private static readonly HashSet<string> TwierdzaIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "castle_V5", "town_EW4", "ROT_castle61", "town_B2", "town_V2", "ROT_town3", "town_EW3", "town_V5", "town_EW1", "town_EN1", "town_A1", "town_S3", "town_V6"
        };
        private static readonly string[] CatName = { "maly zamek", "duzy zamek", "miasto", "wielka twierdza" };
        private static readonly int[] CatLo = { 20, 40, 60, 60 }, CatHi = { 40, 90, 150, 150 };
        private const double Limit471 = 1.5;
        private const double Q471Lo = 0.5, Q471Hi = 0.9;   // 471-p: rozciagniecie q na pelny cel kategorii (kalibracja z testu)

        private sealed class T471
        {
            public string St = "-", Why; public int Cat, Lvl, Seen = -1; public double Start, Clock, R0, R, Q0, S0; public bool Over, OverLimit, Paused, PlayerClan;
            public int PausedDays;
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
        private static double R471(Settlement st, SiegeEvent ev, int cat, int lvl, out double q, out double sq, out bool paused, out string txt)
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

        /// <summary>Z Daily (po LogOn): start (raz na oblezenie), zegar doby i linia doby 471. 471-p U2: w 471a zegar zyje tylko z wlaczonym logiem
        /// (nic poza logiem go nie czyta); 471b, ktory na nim dziala, MUSI wyniesc zegar i R przed LogOn i zapisywac zegar w save (U8).</summary>
        private static void Time471Daily()
        {
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
                        T471 r;
                        bool fresh = !_t471.TryGetValue(ev, out r);
                        if (fresh)
                        {
                            r = new T471 { St = st.StringId, Start = ev.SiegeStartTime.ToDays };
                            r.Cat = Cat471(st, out r.Lvl);
                            r.PlayerClan = st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan;   // 471-p U4: tylko znacznik (D5 czeka na Jeffa)
                            r.Clock = Math.Max(0.0, ev.SiegeStartTime.ElapsedDaysUntilNow);   // po wczytaniu: zegar od wieku oblezenia (przerwy odsieczy sprzed wczytania nieznane)
                            _t471[ev] = r;
                        }
                        if (r.Seen == today) continue;
                        r.Seen = today;
                        bool newOut = false;
                        if (r.Why == null) { r.Why = Out471(ev, st, lp); newOut = r.Why != null; }   // 471-p U3: co dobe; raz poza regula - zostaje
                        if (r.Why != null)
                        {
                            outside++;
                            if (newOut)
                            {
                                _d471Out++;
                                Log.Info("Czas oblezenia (471) " + (fresh ? "start" : "poza regula od teraz") + ": dzien " + today + " | osada " + st.StringId + " (" + Nm(st) + ") | poza regula (" + r.Why + ")"
                                    + (fresh ? "" : " | wiek " + F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0") + " dob, zegar " + F(r.Clock, "0") + ", R " + F(r.R, "0.0")) + ".");
                            }
                            continue;
                        }
                        going++; perCat[r.Cat]++;
                        double q, sq; bool pz; string txt;
                        double R = R471(st, ev, r.Cat, r.Lvl, out q, out sq, out pz, out txt);
                        if (!fresh && !pz) r.Clock += 1.0;                           // 471-p U8: odsiecz sprawdzona dzis, przed doliczeniem doby
                        r.R = R; r.Paused = pz;
                        if (pz) { r.PausedDays++; paused++; }
                        double age = ev.SiegeStartTime.ElapsedDaysUntilNow, lim = Limit471 * CatHi[r.Cat];
                        if (fresh)
                        {
                            r.R0 = R; r.Q0 = q; r.S0 = sq; _d471Start++;
                            Log.Info("Czas oblezenia (471) start: dzien " + today + " | osada " + st.StringId + " (" + Nm(st) + ")" + (r.PlayerClan ? " [osada klanu gracza - D5]" : "")
                                + " | kategoria " + CatName[r.Cat] + " (mury " + r.Lvl
                                + (r.Cat <= 1 ? ", wariant D1 'maly = mury <= 2': " + CatName[r.Lvl <= 2 ? 0 : 1] : "") + "), cel " + CatLo[r.Cat] + "-" + CatHi[r.Cat] + " dob, limit " + F(lim, "0")
                                + " | wodz " + Hn(lp) + " | wiek " + F(age, "0.0") + " dob"
                                + " | termin obrony R " + F(R, "0.0") + " dob (q " + F(q, "0.00") + " -> s " + F(sq, "0.00") + ") | " + txt + " | TYLKO PRZEWIDYWANIE - zachowanie bez zmian.");
                        }
                        if (r.Clock >= R) { over++; if (!r.Over) { r.Over = true; Log.Info("Czas oblezenia (471) termin minal: dzien " + today + " | osada " + st.StringId + " (" + Nm(st) + ", " + CatName[r.Cat] + ")" + (r.PlayerClan ? " [osada klanu gracza - D5]" : "") + " | zegar " + F(r.Clock, "0") + " >= R " + F(R, "0.0") + " (wiek " + F(age, "0.0") + ") | " + txt + " | wedlug 471b tu bylaby kapitulacja z terminu (przy s > 1)."); } }
                        if (age >= lim) { overLim++; r.OverLimit = true; }
                        if (sbList.Length < 3000) sbList.Append(sbList.Length > 0 ? "; " : "").Append(st.StringId).Append(' ').Append(CatName[r.Cat]).Append(' ').Append(F(age, "0")).Append('/').Append(F(R, "0")).Append(" dob").Append(pz ? " (odsiecz)" : "");
                    }
                    catch (Exception e) { Stumble("Time471." + (st.StringId ?? "?"), e); }
                }
                Log.Info("Czas oblezen (471 doba): dzien " + today + " | trwajace w regule " + going + " (maly zamek " + perCat[0] + ", duzy zamek " + perCat[1] + ", miasto " + perCat[2] + ", wielka twierdza " + perCat[3]
                    + "), poza regula " + outside + " | po terminie R " + over + ", ponad limitem " + overLim + ", zegar stoi (odsiecz) " + paused
                    + " | dzis: start " + _d471Start + ", koniec " + _d471End + ", poza regula " + _d471Out + " | wiek/R: " + (sbList.Length == 0 ? "-" : sbList.ToString()) + ".");
            }
            catch (Exception e) { Stumble("Time471Daily", e); }
            finally { _d471Start = _d471End = _d471Out = 0; }
        }

        /// <summary>Z OnSiegeEnded: faktyczny czas wobec przewidywanego R (przyczyna konca - linia KO oblezen 3 tuz przed).</summary>
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
                    + " | R na starcie " + F(r.R0, "0.0") + " (q " + F(r.Q0, "0.00") + " -> s " + F(r.S0, "0.00") + "), R ostatnie " + F(r.R, "0.0") + ", zegar " + F(r.Clock, "0") + (r.Over ? " (termin minal)" : "")
                    + ", dob z odsiecza " + r.PausedDays + (r.OverLimit ? ", PONAD LIMITEM " + F(Limit471 * CatHi[c], "0") : "") + " | HP murow " + F(WallHp(st) * 100, "0") + "%"
                    + " | wlasciciel w chwili konca " + (st != null && st.OwnerClan != null ? st.OwnerClan.StringId : "-")
                    + " (gra zmienia wlasciciela PO tym zdarzeniu - zdobycie widac w linii KO)"   // 471-p U10: SiegeCompleted: RemoveAllSiegeParties -> koniec oblezenia -> ApplyBySiege
                    + (r.PlayerClan ? " [osada klanu gracza - D5]" : "") + ".");
            }
            catch (Exception e) { Stumble("Time471End", e); }
        }
    }
}
