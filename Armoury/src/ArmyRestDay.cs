using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// R3-08a - DZIEN ODPOCZYNKU ARMII (Jeff 20.10: "28 - nie, 29 - nie, pozostale tak"; docs/POMYSLY-35-2026-10-20.md pkt 21; audyt 07 tabela 4.4 N-2).
    /// Klucz MCM ArmyRestDay (domyslnie tak).
    ///   DOBA ARMII (swit-swit, swit = NightRest.PlayerDawn, ta sama godzina, o ktorej oboz swiata budzi armie): co godzine pozycja wodza; godzina "w marszu",
    ///   gdy wodz przesunal sie > Drill.RestStep (0.35 jedn., jak ksiega snu) poza osada (tick nadrabiany powtarza poprzednia godzine - NightClock.CatchUp).
    ///   O swicie: >= 4 h marszu = doba marszu (seria +1); <= 2 h marszu i zadnej godziny bitwy/oblezenia = doba odpoczynku (seria 0; po serii >= progu -
    ///   premia); reszta (oblezenie, bitwa, krotki marsz) serii nie rusza - "co 7. dzien MARSZU". Pierwsza niepelna doba (< 18 h w ksiedze) sie nie liczy.
    ///   POSTOJ AI: po serii >= ArmyRestDayAfterMarchDays (6) armia AI staje od tego switu do nastepnego (w srodku noc obozu swiata - wtedy trzyma ja NightRest,
    ///   my nie ruszamy): AI wodza wstrzymane na godzine (DisableForHours(1), odnawiane co godzine), rozkaz Hold, straznik co 0.1 h gry cofa cudzy rozkaz
    ///   i zjazd (StrategicCampaignAI daje rozkazy co 4 h). Rozkaz sprzed postoju nie wraca - po postoju AI decyduje od nowa (jak swit obozu przy Hold).
    ///   Nie staje (odklada do nastepnego switu): bitwa, oblezenie (oboz obleznikow, oblezona osada), morze, odsiecz/obrona (DefendSettlement), szturm, cel
    ///   oblezenia/najazdu blizej niz promien zagrozenia, cudza blokada AI (uczta / gentry BK - termin > 1.5 h, jak NightRest.ForeignHold), alarm nocy,
    ///   Nieumarli, licznik bezczynnosci gry >= InactLimit (46 - godziny nocy obozu, sufit 44; oboz 0-6 = 40: noc w srodku postoju dolicza sie do licznika),
    ///   silniejszy wrog blisko, oblezona albo lupiona osada wlasnego krolestwa w 3 x promieniu, w polu jedzenie na < 3 dni u wodza albo doczepionej druzyny
    ///   (AI jest wylaczone - armia nie pojdzie po zywnosc; w osadzie zakupy ida co godzine bez AI). Te same powody (bez celu, jedzenie < 2 dni) przerywaja
    ///   postoj co godzine.
    ///   Sen dlugu (NightRest T10) to tez odpoczynek - wtedy nic nie trzymamy, doba liczy sie z pozycji jak kazda.
    ///   BEZCZYNNOSC GRY [K Army.CheckInactivity]: kazda godzina wodza na Hold to +1 do licznika, 48 = rozwiazanie armii (Inactivity). Dzienny postoj dalby +18;
    ///   licznik z chwili poczatku postoju jest przywracany w godzinach, w ktorych to my trzymamy armie (noc liczy sie jak zawsze) - odpoczynek nie jest bezczynnoscia.
    ///   PREMIA (czysta doba odpoczynku po serii >= progu - postoj AI albo postoj sam z siebie, takze armia gracza): spojnosc + ArmyRestDayCohesion (2),
    ///   morale RecentEventsMorale + ArmyRestDayMorale (5) wodza i kazdej doczepionej druzyny (gra zdejmuje 10% tego dodatku na dobe). Liczby: spadek spojnosci
    ///   z AC (x 84/364) to dzis srednio -0.52 na dobe (log 20.10, 17 armii), armia 5 druzyn ok. -0.8 u siebie, -1.6 na ziemi wroga - +2 to 1-2.5 doby
    ///   spadku (audyt dawal +2 przy -3.6 na dobe; przeliczone: zostawiam +2, bo inaczej premia bylaby mniejsza niz jedna doba spadku, a postoj kosztuje dobe drogi).
    ///   Gracz: nikt go nie zatrzymuje - o swicie po serii komunikat, a czysta doba postoju daje te sama premie.
    /// Zapis: arm_armyrest (SaveText.Sync) - wpis na armie po StringId wodza: seria, liczniki doby, postoj, licznik bezczynnosci z poczatku postoju.
    /// Log: linia "Dzien odpoczynku armii (R3-08a):" o swicie (liczniki doby) + przyklady poczatku i przerwania postoju (do 3 na dobe).
    /// Watki: tylko tick godzinowy i tick mapy (watek glowny); przeglad AI co 0.1 h gry tylko dla stojacych armii.
    /// </summary>
    internal static class ArmyRestDay
    {
        private const int MarchHoursMin = 4;       // godzin marszu w dobie = doba marszu
        private const int RestMovedMax = 2;        // najwyzej tyle godzin ruchu w dobie odpoczynku (straznik cofa zjazd do 0.1 h - godzina z przesunieciem > 0.35 to wyjatek)
        private const int InactSkip = 44;          // licznik bezczynnosci gry (prog 48) - od tylu armia i tak sie rozwiaze, postoju nie zaczynamy (sufit; minus noc obozu - InactLimit)
        private const float FoodDaysStart = 3f;    // recenzja 20.10: w polu postoj zaczyna sie tylko z jedzeniem na >= 3 dni u wodza i kazdej doczepionej druzyny (prog BK uczty)
        private const float FoodDaysKeep = 2f;     // ... i trwa, dopoki kazda ma >= 2 dni (doba postoju zjada 1 dzien - przerwanie tylko przy nowych gebach albo stracie)
        private const float ThreatRadiusMin = 6f;  // promien zagrozenia (jedn. mapy, 1 jedn. ok. 4.75 km) - co najmniej tyle, albo AiCampDangerRadius
        private const float ThreatRatio = 0.8f;    // wrog co najmniej 0.8 sily armii = zagrozenie (armia AI sama ocenia walke albo ucieczke)
        private const double GuardStep = 0.1;      // przeglad straznika w h gry

        private sealed class Rec
        {
            public string LeaderId;
            public int Streak, Hours, MovedH, BusyH;
            public Vec2 Pos; public bool HasPos, LastMoved;
            public bool Resting, Held, Told;
            public int InactStart = -1;
            public int LastDawnDay = -1;
            public Vec2 Bed;
        }

        private static readonly Dictionary<Army, Rec> _recs = new Dictionary<Army, Rec>();
        private static readonly HashSet<Army> _seen = new HashSet<Army>();
        private static readonly List<Army> _gone = new List<Army>();
        private static string _pending;
        private static int _resting;
        private static CampaignTime _lastGuard = CampaignTime.Zero;
        private static AccessTools.FieldRef<Army, int> _inact;
        private static bool _inactTried;
        private static int _stumbles;
        // liczniki doby (do linii switu)
        private static int _dMarch, _dRest, _dOther, _dShort, _dBonusAi, _dBonusSelf, _dBonusPlayer, _dStartField, _dStartTown, _dStartDebt, _dReheld, _dEx, _dParties;
        private static float _dCoh, _dMor;
        private static int _dInactBack;
        private static readonly SortedDictionary<string, int> _dPost = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private static readonly SortedDictionary<string, int> _dCut = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private static int _lastLogDay = -1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Reset()
        {
            _recs.Clear(); _seen.Clear(); _gone.Clear(); _pending = null; _resting = 0;
            _lastGuard = CampaignTime.Zero; _lastLogDay = -1;
            ResetDay();
        }

        private static void ResetDay()
        {
            _dMarch = _dRest = _dOther = _dShort = _dBonusAi = _dBonusSelf = _dBonusPlayer = _dStartField = _dStartTown = _dStartDebt = _dReheld = _dEx = _dParties = 0;
            _dCoh = 0f; _dMor = 0f; _dInactBack = 0;
            _dPost.Clear(); _dCut.Clear();
        }

        private static void Stumble(string where, Army a, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0)
            {
                string who = "?";
                try { who = a != null && a.LeaderParty != null ? a.LeaderParty.StringId : "?"; } catch { }
                Log.Error("ArmyRestDay." + where + " (armia " + who + ", potkniecie " + _stumbles + ")", e);
            }
        }

        private static void Bump(SortedDictionary<string, int> d, string k) { int v; d.TryGetValue(k, out v); d[k] = v + 1; }

        private static int Need(Settings s) { return Math.Max(2, Math.Min(14, s.ArmyRestDayAfterMarchDays)); }

        private static float Radius(Settings s) { return Math.Max(ThreatRadiusMin, s.AiCampDangerRadius); }

        // ------------------------------------------------------------ bezczynnosc gry (Army._inactivityCounter)
        private static bool InactReady()
        {
            if (_inact == null && !_inactTried)
            {
                _inactTried = true;
                try { _inact = AccessTools.FieldRefAccess<Army, int>("_inactivityCounter"); }
                catch (Exception e) { _inact = null; Log.Error("ArmyRestDay: brak Army._inactivityCounter - postoj bez ochrony przed rozwiazaniem z bezczynnosci", e); }
            }
            return _inact != null;
        }

        private static int Inact(Army a) { try { return InactReady() ? _inact(a) : -1; } catch { return -1; } }

        private static void RestoreInact(Army a, Rec r)
        {
            if (r.InactStart < 0 || !InactReady()) return;
            try
            {
                ref int c = ref _inact(a);
                if (c > r.InactStart) { _dInactBack += c - r.InactStart; c = r.InactStart; }
            }
            catch { }
        }

        // ------------------------------------------------------------ tick godzinowy (ArmouryBehavior, po NightRest.OnHourly)
        internal static void Hourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (!s.ArmyRestDay)
                {
                    if (_recs.Count > 0) { ReleaseAll("klucz Army Rest Day wylaczony"); _recs.Clear(); }
                    _pending = null;   // zapis wczytany przy wylaczonym kluczu - po wlaczeniu serie od nowa
                    return;
                }
                ResolvePending();
                int h = NightClock.NomH >= 0 ? NightClock.Hour : CampaignTime.Now.GetHourOfDay;
                bool catchUp = NightClock.CatchUp;
                int dawn = NightRest.PlayerDawn;
                int day = (int)CampaignTime.Now.ToDays;
                bool dawnTick = h == dawn;
                _seen.Clear();
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    var armies = k.Armies;
                    for (int i = 0; i < armies.Count; i++)
                    {
                        var a = armies[i];
                        try { One(a, s, catchUp, dawnTick, day); }
                        catch (Exception e) { Stumble("Hourly", a, e); }
                    }
                }
                // armie rozwiazane - wpis znika (wstrzymany wodz dostaje AI z powrotem)
                _gone.Clear();
                foreach (var kv in _recs) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
                for (int i = 0; i < _gone.Count; i++)
                {
                    var a = _gone[i];
                    Rec r;
                    if (_recs.TryGetValue(a, out r) && r.Resting) { try { End(a, r, "armia rozwiazana", false); } catch (Exception e) { Stumble("Gone", a, e); } }
                    _recs.Remove(a);
                }
                CountResting();
                if (dawnTick && day != _lastLogDay) { _lastLogDay = day; LogDay(s, day); }
            }
            catch (Exception e) { Stumble("Hourly", null, e); }
        }

        private static void One(Army a, Settings s, bool catchUp, bool dawnTick, int day)
        {
            var lead = a != null ? a.LeaderParty : null;
            if (lead == null || !lead.IsActive || Undead.Party(lead)) return;   // Inni nie odpoczywaja
            _seen.Add(a);
            Rec r;
            if (!_recs.TryGetValue(a, out r)) { r = new Rec { LeaderId = lead.StringId }; _recs[a] = r; }
            bool player = lead == MobileParty.MainParty;
            if (player && r.Resting) End(a, r, "wodzem gracz", false);
            // pomiar godziny, ktora wlasnie minela
            var pos = lead.GetPosition2D;
            bool moved;
            if (r.HasPos && catchUp) moved = r.LastMoved;
            else moved = r.HasPos && lead.CurrentSettlement == null && pos.Distance(r.Pos) > Drill.RestStep;
            r.Pos = pos; r.HasPos = true; r.LastMoved = moved;
            r.Hours++;
            if (moved) r.MovedH++;
            if (Busy(lead)) r.BusyH++;
            bool dawnNow = dawnTick && r.LastDawnDay != day;
            if (r.Resting && !dawnNow) Keep(a, lead, r, s);   // swit i tak konczy postoj (Dawn)
            if (dawnNow) { r.LastDawnDay = day; Dawn(a, lead, r, s, player); }
        }

        private static bool Busy(MobileParty lead)
        {
            return lead.MapEvent != null || lead.BesiegerCamp != null || lead.SiegeEvent != null
                   || (lead.CurrentSettlement != null && lead.CurrentSettlement.SiegeEvent != null);
        }

        private static void Dawn(Army a, MobileParty lead, Rec r, Settings s, bool player)
        {
            int need = Need(s);
            if (r.Hours >= 18)
            {
                if (r.MovedH >= MarchHoursMin) { r.Streak++; _dMarch++; }
                else if (r.MovedH <= RestMovedMax && r.BusyH == 0)
                {
                    _dRest++;
                    if (r.Streak >= need) Bonus(a, lead, r, s, player);
                    r.Streak = 0; r.Told = false;
                }
                else _dOther++;
            }
            else _dShort++;
            r.Hours = 0; r.MovedH = 0; r.BusyH = 0;
            if (r.Resting) End(a, r, null, true);
            if (r.Streak < need) return;
            if (player)
            {
                if (!r.Told)
                {
                    r.Told = true;
                    Msg("Your army has marched " + r.Streak + " days in a row. A day of rest - a whole day without marching - would restore its cohesion (+"
                        + F1(Coh(s)) + ") and its men's morale (+" + F1(Mor(s)) + ").", Colors.White);
                    Log.Info("Dzien odpoczynku armii (R3-08a): armia gracza - " + r.Streak + " dob marszu z rzedu, postoj nalezny (komunikat).");
                }
                return;
            }
            Start(a, lead, r, s);
        }

        private static float Coh(Settings s) { return Math.Max(0f, Math.Min(20f, s.ArmyRestDayCohesion)); }
        private static float Mor(Settings s) { return Math.Max(0f, Math.Min(30f, s.ArmyRestDayMorale)); }

        private static void Bonus(Army a, MobileParty lead, Rec r, Settings s, bool player)
        {
            float coh = Coh(s), mor = Mor(s);
            float before = a.Cohesion;
            a.Cohesion = Math.Min(100f, a.Cohesion + coh);
            _dCoh += a.Cohesion - before;
            int n = 0;
            if (mor > 0f)
            {
                lead.RecentEventsMorale += mor; n++;
                var att = lead.AttachedParties;
                for (int i = 0; i < att.Count; i++) { var p = att[i]; if (p != null && p.IsActive) { p.RecentEventsMorale += mor; n++; } }
            }
            _dMor += mor * n; _dParties += n;
            if (player) _dBonusPlayer++; else if (r.Resting) _dBonusAi++; else _dBonusSelf++;
            if (player)
                Msg("A day of rest: your army's cohesion +" + F1(a.Cohesion - before) + ", and the morale of every party in its camp +" + F1(mor) + ".", Colors.Green);
            if (player || _dEx < 3)
            {
                _dEx++;
                Log.Info("Dzien odpoczynku armii (R3-08a): " + Who(a) + " - doba odpoczynku po " + r.Streak + " dobach marszu (" + (player ? "armia gracza" : r.Resting ? "postoj AI" : "postoj sam z siebie")
                         + ") - spojnosc " + F1(before) + " -> " + F1(a.Cohesion) + ", morale +" + F1(mor) + " x " + n + " druzyn.");
            }
        }

        // ------------------------------------------------------------ postoj AI
        private static string Blocker(Army a, MobileParty lead, Settings s, bool atStart)
        {
            if (lead.MapEvent != null) return "bitwa";
            if (lead.BesiegerCamp != null || lead.SiegeEvent != null) return "oblezenie";
            if (lead.CurrentSettlement != null && lead.CurrentSettlement.SiegeEvent != null) return "oblezona osada";
            if (lead.IsCurrentlyAtSea) return "morze";
            if (lead.AttachedTo != null) return "doczepiony";
            if (atStart)
            {
                var b = lead.DefaultBehavior;
                if (b == AiBehavior.DefendSettlement) return "odsiecz";
                if (b == AiBehavior.AssaultSettlement) return "szturm";
                var ts = lead.TargetSettlement;
                float R = Radius(s);
                if ((b == AiBehavior.BesiegeSettlement || b == AiBehavior.RaidSettlement) && ts != null && ts.GetPosition2D.DistanceSquared(lead.GetPosition2D) <= R * R)
                    return "cel blisko";
                if (lead.IsFleeing()) return "ucieczka";
                if (NightRest.NightAlarmed(lead)) return "alarm nocy";
                // recenzja 20.10: wylaczone AI z terminem <= 1.5 h to nasz albo NightRest termin (oboz swiata - pobudka w ticku AI po zdarzeniach godzinowych)
                if (!NightRest.NightHolds(lead) && NightRest.ForeignAiHold(lead)) return "cudza blokada AI";
                int ic = Inact(a);
                if (ic >= InactLimit()) return "bezczynnosc";
            }
            if (lead.CurrentSettlement == null && Hungry(lead, atStart ? FoodDaysStart : FoodDaysKeep)) return "jedzenie";
            if (Threat(a, lead, s)) return "wrog";
            if (KinInDanger(a, lead, s)) return "osada w potrzebie";
            return null;
        }

        /// <summary>Prog licznika bezczynnosci gry na starcie postoju: noc obozu w srodku postoju idzie do licznika (Keep przyjmuje ja jak zawsze), wiec
        /// start <= 48 - noc - 2 (zapas na godzine przejscia); bez obozu swiata sufit InactSkip.</summary>
        private static int InactLimit()
        {
            int st = NightRest.CampStart, e = NightRest.CampEnd;
            int night = st == e ? 0 : ((e - st) % 24 + 24) % 24;
            return Math.Max(24, Math.Min(InactSkip, 46 - night));
        }

        /// <summary>Wodz albo ktoras doczepiona druzyna ma jedzenia na mniej niz days dni (jak MobileParty.GetNumDaysForFoodToLast, bez dzielenia przez 0).</summary>
        private static bool Hungry(MobileParty lead, float days)
        {
            if (FoodShort(lead, days)) return true;
            var att = lead.AttachedParties;
            for (int i = 0; i < att.Count; i++) { var p = att[i]; if (p != null && p.IsActive && FoodShort(p, days)) return true; }
            return false;
        }

        private static bool FoodShort(MobileParty p, float days)
        {
            float ch = p.FoodChange;   // ujemne = zjadaja
            if (ch > -0.01f) return false;
            return p.ItemRoster.TotalFood < days * -ch;
        }

        /// <summary>Silniejszy (>= 0.8 sily armii) wrogi lord albo armia w promieniu, poza osada.</summary>
        private static bool Threat(Army a, MobileParty lead, Settings s)
        {
            var mf = lead.MapFaction;
            if (mf == null) return false;
            float R = Radius(s), R2 = R * R;
            var pos = lead.GetPosition2D;
            float mine = a.EstimatedStrength;
            var all = MobileParty.AllLordParties;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null || p == lead || !p.IsActive || p.AttachedTo != null || p.CurrentSettlement != null) continue;
                var pf = p.MapFaction;
                if (pf == null || pf == mf || !pf.IsAtWarWith(mf)) continue;
                if (p.GetPosition2D.DistanceSquared(pos) > R2) continue;
                float str = p.Army != null && p.Army.LeaderParty == p ? p.Army.EstimatedStrength : p.Party.EstimatedStrength;
                if (str >= mine * ThreatRatio) return true;
            }
            return false;
        }

        /// <summary>Oblezona albo lupiona osada wlasnego krolestwa w 3 x promieniu - armia nie odpoczywa, gdy trzeba isc z pomoca.</summary>
        private static bool KinInDanger(Army a, MobileParty lead, Settings s)
        {
            var k = a.Kingdom;
            if (k == null) return false;
            float R = 3f * Radius(s), R2 = R * R;
            var pos = lead.GetPosition2D;
            var sts = k.Settlements;
            for (int i = 0; i < sts.Count; i++)
            {
                var st = sts[i];
                if (st == null || !(st.IsUnderSiege || st.IsUnderRaid)) continue;
                if (st.GetPosition2D.DistanceSquared(pos) <= R2) return true;
            }
            return false;
        }

        private static void Start(Army a, MobileParty lead, Rec r, Settings s)
        {
            string why = Blocker(a, lead, s, true);
            if (why != null) { Bump(_dPost, why); return; }
            r.Resting = true;
            r.Bed = lead.GetPosition2D;
            r.InactStart = Inact(a);
            string where;
            if (NightRest.InDebtSleep(lead)) { r.Held = false; _dStartDebt++; where = "snem dlugu (trzyma NightRest)"; }
            else
            {
                r.Held = true;
                lead.Ai.DisableForHours(1);
                if (lead.CurrentSettlement == null) { lead.SetMoveModeHold(); _dStartField++; where = "w polu"; }
                else { _dStartTown++; where = "w osadzie " + lead.CurrentSettlement.Name; }
            }
            if (_dEx < 3)
            {
                _dEx++;
                Log.Info("Dzien odpoczynku armii (R3-08a): " + Who(a) + " staje na dobe po " + r.Streak + " dobach marszu - " + where + ", spojnosc " + F1(a.Cohesion)
                         + ", bezczynnosc gry " + r.InactStart + "/48.");
            }
        }

        /// <summary>Co godzine w trakcie postoju: powody przerwania; w nocy obozu (NightRest trzyma wodza) nic nie robimy - noc liczy sie do bezczynnosci jak zawsze.</summary>
        private static void Keep(Army a, MobileParty lead, Rec r, Settings s)
        {
            // noc obozu / sen dlugu / alarm: rzadzi NightRest (takze alarmem od wroga) - postoj trwa, my nic nie ruszamy
            if (NightRest.NightHolds(lead))
            {
                r.Held = false;
                int ic = Inact(a);
                if (ic >= 0) r.InactStart = ic;
                return;
            }
            string why = Blocker(a, lead, s, false);
            if (why != null) { End(a, r, why, false); return; }
            Hold(a, lead, r, true);
        }

        private static void Hold(Army a, MobileParty lead, Rec r, bool hourly)
        {
            r.Held = true;
            if (hourly) lead.Ai.DisableForHours(1);
            if (lead.CurrentSettlement == null)
            {
                bool ordered = lead.DefaultBehavior != AiBehavior.Hold || lead.TargetSettlement != null || lead.TargetParty != null;
                if (ordered || lead.GetPosition2D.Distance(r.Bed) > 0.3f)
                {
                    if (!hourly) lead.Ai.DisableForHours(1);
                    lead.SetMoveModeHold();
                    r.Bed = lead.GetPosition2D;
                    _dReheld++;
                }
            }
            RestoreInact(a, r);
        }

        private static void End(Army a, Rec r, string why, bool done)
        {
            r.Resting = false;
            var lead = a != null ? a.LeaderParty : null;
            // AI z powrotem tylko, gdy to my je trzymamy (w nocy obozu i w snie dlugu trzyma NightRest - jego termin, jego pobudka)
            if (r.Held && lead != null && lead.IsActive && lead.Ai != null && !NightRest.NightHolds(lead))
            {
                try { lead.Ai.EnableAi(); } catch { }
            }
            r.Held = false;
            r.InactStart = -1;
            if (done || why == null) return;
            Bump(_dCut, why);
            if (_dEx < 3)
            {
                _dEx++;
                Log.Info("Dzien odpoczynku armii (R3-08a): " + Who(a) + " - postoj przerwany: " + why + ".");
            }
        }

        private static void ReleaseAll(string why)
        {
            foreach (var kv in _recs)
                if (kv.Value.Resting) { try { End(kv.Key, kv.Value, why, false); } catch (Exception e) { Stumble("ReleaseAll", kv.Key, e); } }
            _resting = 0;
        }

        private static void CountResting()
        {
            int n = 0;
            foreach (var kv in _recs) if (kv.Value.Resting) n++;
            _resting = n;
        }

        // ------------------------------------------------------------ straznik co 0.1 h gry (ArmouryBehavior TickEvent)
        internal static void OnTick()
        {
            if (_resting == 0) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ArmyRestDay || Campaign.Current == null) return;
                var now = CampaignTime.Now;
                double dh = (now - _lastGuard).ToHours;
                if (dh < GuardStep && dh >= 0) return;
                _lastGuard = now;
                foreach (var kv in _recs)
                {
                    var r = kv.Value;
                    if (!r.Resting) continue;
                    var a = kv.Key;
                    try
                    {
                        var lead = a.LeaderParty;
                        if (lead == null || !lead.IsActive || lead.MapEvent != null || lead == MobileParty.MainParty) continue;
                        // recenzja 20.10: armia rozwiazana w srodku godziny (wpis znika dopiero w Hourly) - byly wodz nie stoi do konca godziny
                        if (lead.Army != a || lead.AttachedTo != null) { End(a, r, "armia rozwiazana", false); continue; }
                        if (NightRest.NightHolds(lead)) continue;
                        Hold(a, lead, r, false);
                    }
                    catch (Exception e) { Stumble("OnTick", a, e); }
                }
            }
            catch (Exception e) { Stumble("OnTick", null, e); }
        }

        // ------------------------------------------------------------ log
        private static string Who(Army a)
        {
            try
            {
                var l = a.LeaderParty;
                string kn = a.Kingdom != null ? a.Kingdom.StringId : "?";
                return "armia " + (l != null && l.LeaderHero != null ? l.LeaderHero.Name.ToString() : l != null ? l.StringId : "?") + " (" + kn + ")";
            }
            catch { return "armia ?"; }
        }

        private static string F1(float v) { return v.ToString("0.#", Inv); }

        private static void Msg(string t, Color c)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(t, c)); } catch { }
        }

        private static string Join(SortedDictionary<string, int> d)
        {
            if (d.Count == 0) return "0";
            int sum = 0; var sb = new StringBuilder();
            foreach (var kv in d) { sum += kv.Value; sb.Append(sb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value); }
            return sum + " (" + sb + ")";
        }

        private static void LogDay(Settings s, int day)
        {
            int ai = 0, pl = 0, due = 0, longest = 0;
            foreach (var kv in _recs)
            {
                bool p = kv.Key.LeaderParty == MobileParty.MainParty;
                if (p) pl++; else ai++;
                if (kv.Value.Streak >= Need(s)) due++;
                if (kv.Value.Streak > longest) longest = kv.Value.Streak;
            }
            Log.Info("Dzien odpoczynku armii (R3-08a): dzien " + day + " | armie " + (ai + pl) + " (AI " + ai + ", gracz " + pl + ") | doby: marsz " + _dMarch + ", odpoczynek " + _dRest
                     + " (z premia " + (_dBonusAi + _dBonusSelf + _dBonusPlayer) + ": po postoju AI " + _dBonusAi + ", sam z siebie " + _dBonusSelf + ", gracz " + _dBonusPlayer + "), inne " + _dOther
                     + ", niepelne " + _dShort + " | seria >= " + Need(s) + ": " + due + ", najdluzsza " + longest
                     + " | postoje zaczete " + (_dStartField + _dStartTown + _dStartDebt) + " (w polu " + _dStartField + ", w osadzie " + _dStartTown + ", snem dlugu " + _dStartDebt + "), trwa " + _resting
                     + " | odlozone " + Join(_dPost) + " | przerwane " + Join(_dCut)
                     + " | spojnosc +" + F1(_dCoh) + ", morale +" + F1(_dMor) + " (druzyn " + _dParties + ") | bezczynnosc gry cofnieta " + _dInactBack + " h | wstrzymania straznika " + _dReheld
                     + " | potkniecia " + _stumbles + ".");
            ResetDay();
        }

        // ------------------------------------------------------------ zapis (arm_armyrest, SaveText.Sync)
        internal static string Export()
        {
            try
            {
                // recenzja 20.10: zapis przed pierwszym tickiem godzinowym po wczytaniu (szybki zapis, autozapis) - wpisy jeszcze nieprzypisane, idzie stary napis
                if (_pending != null) return _pending;
                var sb = new StringBuilder();
                foreach (var kv in _recs)
                {
                    var r = kv.Value;
                    string id = kv.Key.LeaderParty != null ? kv.Key.LeaderParty.StringId : r.LeaderId;
                    if (string.IsNullOrEmpty(id) || id.IndexOf(',') >= 0 || id.IndexOf('|') >= 0) continue;
                    if (sb.Length > 0) sb.Append('|');
                    sb.Append(id).Append(',').Append(r.Streak.ToString(Inv)).Append(',').Append(r.Hours.ToString(Inv)).Append(',').Append(r.MovedH.ToString(Inv))
                      .Append(',').Append(r.BusyH.ToString(Inv)).Append(',').Append(r.Resting ? '1' : '0').Append(',').Append(r.Held ? '1' : '0')
                      .Append(',').Append(r.InactStart.ToString(Inv)).Append(',').Append(r.Told ? '1' : '0').Append(',').Append(r.LastDawnDay.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("ArmyRestDay.Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _recs.Clear(); _resting = 0;
            _pending = string.IsNullOrEmpty(data) ? null : data;
        }

        /// <summary>Wpisy z zapisu wiazane z armiami przy pierwszym ticku (armie juz istnieja); wodz bez armii - wpis przepada.</summary>
        private static void ResolvePending()
        {
            if (_pending == null) return;
            string data = _pending;
            _pending = null;
            try
            {
                var byId = new Dictionary<string, Army>(StringComparer.Ordinal);
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    foreach (var a in k.Armies) if (a != null && a.LeaderParty != null && a.LeaderParty.StringId != null) byId[a.LeaderParty.StringId] = a;
                }
                int ok = 0, lost = 0, rest = 0;
                foreach (var e in data.Split('|'))
                {
                    var f = e.Split(',');
                    if (f.Length < 10) { lost++; continue; }
                    Army a;
                    if (!byId.TryGetValue(f[0], out a)) { lost++; continue; }
                    var r = new Rec { LeaderId = f[0] };
                    int.TryParse(f[1], NumberStyles.Integer, Inv, out r.Streak);
                    int.TryParse(f[2], NumberStyles.Integer, Inv, out r.Hours);
                    int.TryParse(f[3], NumberStyles.Integer, Inv, out r.MovedH);
                    int.TryParse(f[4], NumberStyles.Integer, Inv, out r.BusyH);
                    r.Resting = f[5] == "1"; r.Held = f[6] == "1";
                    int.TryParse(f[7], NumberStyles.Integer, Inv, out r.InactStart);
                    r.Told = f[8] == "1";
                    int.TryParse(f[9], NumberStyles.Integer, Inv, out r.LastDawnDay);
                    r.Streak = Math.Max(0, Math.Min(999, r.Streak));
                    if (r.Resting) { r.Bed = a.LeaderParty.GetPosition2D; rest++; }
                    _recs[a] = r; ok++;
                }
                Log.Info("Dzien odpoczynku armii (R3-08a): wczytany zapis - armii " + ok + " (w postoju " + rest + "), wpisow bez armii " + lost + ".");
            }
            catch (Exception e) { Log.Error("ArmyRestDay.ResolvePending", e); }
        }
    }

    internal static partial class NightRest
    {
        /// <summary>R3-08a: wodza armii trzyma teraz NightRest (oboz nocy, legowisko, sen dlugu, alarm, osada w godzinach obozu) - postoj go nie rusza.</summary>
        internal static bool NightHolds(MobileParty mp)
        {
            if (mp == null) return false;
            return _bedPos.ContainsKey(mp) || _debtSleep.ContainsKey(mp) || _alarmed.ContainsKey(mp) || _townHold.Contains(mp) || _camping.Contains(mp);
        }

        /// <summary>R3-08a: wodz spi snem dlugu (T10) - to tez odpoczynek.</summary>
        internal static bool InDebtSleep(MobileParty mp) { return mp != null && _debtSleep.ContainsKey(mp); }

        /// <summary>R3-08a: wodz w alarmie nocy (AI ocenia ucieczke) - postoj nie zaczyna sie.</summary>
        internal static bool NightAlarmed(MobileParty mp) { return mp != null && _alarmed.ContainsKey(mp); }

        /// <summary>R3-08a (recenzja 20.10): AI wylaczone cudzym terminem (> 1.5 h - uczta / gentry BK, ForeignHold); termin <= 1.5 h to nasz albo oboz swiata.
        /// Bez odczytu terminu (brak wlasciwosci gry) ostroznie: kazde wylaczone AI jest cudze (jak przed recenzja).</summary>
        internal static bool ForeignAiHold(MobileParty mp)
        {
            if (mp == null || mp.Ai == null || !mp.Ai.IsDisabled) return false;
            if (ForeignHold(mp)) return true;
            return _enableAtProp == null;
        }
    }
}
