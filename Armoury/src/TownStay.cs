using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m109-musztra (Jeff 22.10 ok. 21:50, zatwierdzone 23.10 "Ok"): "jak czekam w miescie, wojsko ma rozkaz drill, ale mniej, bo wydaja kase, pija, chodza
    /// na kobiety itd., wiec nie w glowie im musztra". POSTOJ W OSADZIE - jedna zasada dla wszystkich partii pod zasada obozu m97 (gracz, strona gracza, lordowie AI):
    ///  - musztra w osadzie liczy sie sama, tylko w godzinach cwiczen (CampDrill.DayHour, 3:00-17:59), we wspolnym limicie 8 h od switu, z limitem godzin
    ///    w osadzie malejacym z dobami postoju: miasto 4, 3, 2, od 4. doby 1 (poranny apel); zamek i wies 6, 5, od 3. doby 4 (mniej pokus) - klucz DrillTownStay;
    ///  - doba postoju liczy sie od chwili wejscia; wypad za brame krotszy niz 24 h nie odnawia dyscypliny, dopiero doba w polu zeruje licznik;
    ///  - oblezona twierdza (karczmy zamkniete, ludzie na murach) i wies rabowana - bez postoju (Kind 0): jak oboz oblezenia, do 8 h, bez wydatkow;
    ///  - dotad (44c03a05): gracz czekajacy w miescie 0 h (CampDrill.AutoHour tylko oboz oblezenia i cudza armia), lord AI w tym samym miescie pelne 8 h
    ///    (Drill.DayRestHours) - dwie sprzeczne zasady na jedno zjawisko.
    /// Bez latek Harmony: Drill.Observe (AI - maska godzin w osadzie i doba postoju), CampDrill.AutoHour (gracz), Drill.CtxOf (godziny AI z limitem), blok dobowy.
    /// Zapis: stan gracza w napisie arm_campdrill (CampDrill.Export, pola 5-9 - stara DLL czyta 4 pierwsze); AI tylko w pamieci - po wczytaniu doba postoju
    /// z MobileParty.StationaryStartTime (gra ja zapisuje; partia AI w osadzie nie jest w ruchu, wiec to chwila wejscia albo wczesniejszy postoj pod brama -
    /// linia "wejscia AI" mierzy roznice). Dyscypliny jako osobnego systemu nie ma w kodzie ani w grze - wyrazaja ja mniejsze godziny musztry.
    /// WYDATKI (klucz TownStaySpend, blok dobowy ArmouryBehavior po zalogach m96, pozycja ksiegi "markietani" MSutlers): partia rodu (AI i gracz) w miescie albo
    /// zamku wydaje co dobe czesc wolnej sakiewki - ta sama miara co markietani w polu (Sutlers.One, N6) i zalogi (SavingsByNeed.Garrison): SutlerDailyShare
    /// x min(3, dni wolnej sakiewki / 10) przy SutlersByPurse, z tego, co zostaje po rezerwach markietanow (MenPurse.CampFree: naprawy, odziez, oszczednosci K1).
    /// Odbiorca jak przy wyjezdzie z miasta (MenPurse.LifeTo): kasa osady i kiesa ludu (75/25, TownFolk.EarnService), te same liczniki (169c, K3, tarcza zoldu, L0).
    /// Kazda partia rodu jest w danej dobie albo w polu (markietani pomijaja partie w osadzie), albo w osadzie (tu) - nic nie liczy sie dwa razy; w miescie reszte
    /// wydaja dalej przy wyjezdzie (OnLeft). Wies, oblezona twierdza, okno lupu (CitySack.StayWindow), bitwa - nic. Morale: +3 na dobe przy >= 1 zl na glowe
    /// (miara zabawy markietanow: 2 zl -> +6; mniej pieniedzy - mniej), ostatnie zdarzenia najwyzej do +5 (polowa pulapu uczty 10; gra zdejmuje 10% na dobe).
    /// Zloto nie powstaje i nie znika: sakiewka ludzi -> kasa osady + kiesa ludu; kontrola w linii z odczytow przed/po (zeszlo z sakiewek = przybylo w kasach + kiesy).
    /// </summary>
    internal static class TownStay
    {
        internal const string P = "Postoj w osadzie (m109): ";
        internal const int KNone = 0, KTown = 1, KCastle = 2, KVillage = 3;
        // limit godzin w osadzie na dobe postoju: miasto max(1, 5 - doba) = 4/3/2/1; zamek i wies max(4, 7 - doba) = 6/5/4
        private const int TownTop = 5, TownFloor = 1, KeepTop = 7, KeepFloor = 4;
        internal const int FieldResetHours = 24;   // doba w polu zeruje licznik dob postoju (krotszy wypad - postoj trwa)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool DrillOn(Settings s) { return s != null && s.DrillTownStay; }

        // ------------------------------------------------------------ zasada
        /// <summary>Rodzaj postoju w osadzie: miasto, zamek, wies; 0 - bez postoju (brak osady, oblezona twierdza, rabowana wies, kryjowka).</summary>
        internal static int Kind(Settlement st)
        {
            if (st == null) return KNone;
            if (st.IsTown) return st.IsUnderSiege ? KNone : KTown;
            if (st.IsCastle) return st.IsUnderSiege ? KNone : KCastle;
            if (st.IsVillage) return st.IsUnderRaid ? KNone : KVillage;
            return KNone;
        }

        /// <summary>Oblezona twierdza - ludzie na murach cwicza jak oboz oblezenia (CampDrill.AutoHour, klucz DrillSiegeArmy).</summary>
        internal static bool Besieged(Settlement st) { return st != null && st.IsFortification && st.IsUnderSiege; }

        /// <summary>Limit godzin cwiczen w osadzie w dobie postoju `day` (od 1).</summary>
        internal static int Cap(int kind, int day)
        {
            if (kind == KTown) return Math.Max(TownFloor, TownTop - day);
            if (kind == KCastle || kind == KVillage) return Math.Max(KeepFloor, KeepTop - day);
            return (int)CampDrill.FullHours;
        }

        /// <summary>Doba postoju (od 1) w pelnej godzinie `now` dla postoju od godziny `from`.</summary>
        internal static int Day(long from, long now) { return from < 0 || now < from ? 1 : (int)((now - from) / 24 + 1); }

        /// <summary>Licznik dob postoju jednej partii, raz na godzine (takze noca i w karze): wejscie - nowy postoj, gdy go nie bylo albo wypad trwal >= 24 h;
        /// wyjscie - chwila wyjscia; po 24 h w polu - licznik zerowany. Zwraca true, gdy ta godzina zaczela nowy postoj.</summary>
        internal static bool Step(ref long from, ref long outAt, bool inStay, long now)
        {
            if (inStay)
            {
                bool fresh = from < 0 || (outAt >= 0 && now - outAt >= FieldResetHours);
                if (fresh) from = now;
                outAt = -1;
                return fresh;
            }
            if (from < 0) { outAt = -1; return false; }
            if (outAt < 0) outAt = now;
            if (now - outAt >= FieldResetHours) { from = -1; outAt = -1; }
            return false;
        }

        /// <summary>Poczatek postoju partii AI widzianej pierwszy raz w osadzie (po wczytaniu albo nowa partia): StationaryStartTime gry (zapisywane), gdy jest
        /// i nie lezy w przyszlosci; inaczej teraz.</summary>
        internal static long FromStationary(MobileParty mp, long now, out bool never)
        {
            never = true;
            if (mp == null) return now;
            var sst = mp.StationaryStartTime;
            if (sst == CampaignTime.Never) return now;
            never = false;
            double h = sst.ToHours;
            if (h > CampaignTime.Now.ToHours) return now;
            return Math.Min(now, (long)Math.Floor(h));
        }

        internal static string KindName(int kind) { return kind == KTown ? "miasto" : kind == KCastle ? "zamek" : kind == KVillage ? "wies" : "brak"; }

        /// <summary>Napis dnia musztry w grze (Drill.DayMessage): gdzie byly godziny w osadzie - wedlug osady, w ktorej gracz jest teraz.</summary>
        internal static string WhereText()
        {
            var mp = MobileParty.MainParty;
            switch (Kind(mp != null ? mp.CurrentSettlement : null))
            {
                case KTown: return "in town - the men spend the rest of the day in the taverns";
                case KCastle: return "at the castle - the men spend the rest of the day in the taverns below the walls";
                case KVillage: return "in the village - the men idle away the rest of the day";
                default: return "while you waited in a settlement - the men spent the rest of those days in the taverns";
            }
        }

        // ------------------------------------------------------------ pomiar musztry doby (Count z CampDrill.Count, linia DrillLine z CampDrill.Daily)
        private static readonly int[] _tN = new int[4], _tMax = new int[4];      // miasto: doba 1 / 2 / 3 / 4+
        private static readonly double[] _tSum = new double[4];
        private static readonly int[] _kN = new int[3], _kMax = new int[3];      // zamek i wies: doba 1 / 2 / 3+
        private static readonly double[] _kSum = new double[3];
        private static int _dParties, _dSetParties, _dSetTown, _dSetCastle, _dSetVill, _dSideN;
        private static double _dS0, _dS1, _dField, _dX0, _dX1, _dSideCut;
        private static int _dEntries, _dRestored, _dRestoredNever;
        private static double _dDiffSum, _dDiffMax;
        private static int _stumbles;
        private static readonly Dictionary<string, int> _err = new Dictionary<string, int>();

        internal static void Reset()
        {
            _stumbles = 0; _err.Clear();
            ClearDrill(); ClearSpend();
            TestClear(); _tOn = false;   // autotest: nowa kampania albo wczytanie - decyzja od nowa w SessionStart (blokada zegara zyje w starej kampanii)
        }

        private static void ClearDrill()
        {
            Array.Clear(_tN, 0, _tN.Length); Array.Clear(_tMax, 0, _tMax.Length); Array.Clear(_tSum, 0, _tSum.Length);
            Array.Clear(_kN, 0, _kN.Length); Array.Clear(_kMax, 0, _kMax.Length); Array.Clear(_kSum, 0, _kSum.Length);
            _dParties = _dSetParties = _dSetTown = _dSetCastle = _dSetVill = _dSideN = 0;
            _dS0 = _dS1 = _dField = _dX0 = _dX1 = _dSideCut = 0;
            _dEntries = _dRestored = _dRestoredNever = 0; _dDiffSum = _dDiffMax = 0;
        }

        /// <summary>Wyjatek na jednej partii albo w jednym miejscu - licznik potkniec (bez wylacznika); do logu pierwsze 3 z miejsca i potem co 50.</summary>
        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int n; _err.TryGetValue(where, out n); _err[where] = ++n;
                if (n <= 3 || n % 50 == 0) Log.Error("TownStay." + where + " (" + n + ". raz)", e);
            }
            catch { }
        }

        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        /// <summary>Drill.Observe: partia AI widziana w osadzie od nowa (wejscie) - roznica do StationaryStartTime gry (hipoteza H1: postoj pod brama wczesniej).</summary>
        internal static void NoteEntry(MobileParty mp, long now)
        {
            try
            {
                bool never;
                long sst = FromStationary(mp, now, out never);
                if (never) return;
                double d = now - sst;
                _dEntries++; _dDiffSum += d; if (d > _dDiffMax) _dDiffMax = d;
            }
            catch (Exception e) { Stumble("NoteEntry", e); }
        }

        /// <summary>Drill.Observe: partia AI widziana pierwszy raz (po wczytaniu, nowa partia) juz w osadzie - doba postoju odtworzona ze StationaryStartTime.</summary>
        internal static void NoteRestored(bool never) { _dRestored++; if (never) _dRestoredNever++; }

        /// <summary>Raz na element oddzialu partii AI pod zasada obozu (CampDrill.Count): XP zasady obozu bez limitu i z limitem (wzor m97 jest liniowy w godzinach -
        /// D i perki proporcjonalne do H, wiec XP przy innych godzinach = XP x H' / H, bez szacunku); partia - raz (first): koszyki dob postoju i godziny.</summary>
        internal static void Count(Drill.Ctx c, bool first, double campXp)
        {
            try
            {
                if (c == null || c.Main) return;
                if (c.NoBase)
                {
                    if (first && c.Hset > 0) { _dSideN++; _dSideCut += Math.Max(0f, c.Hraw - c.Hcap); }
                    return;
                }
                if (c.H > 0f) { _dX0 += campXp * c.Hraw / c.H; _dX1 += campXp * c.Hcap / c.H; }
                if (!first) return;
                _dParties++;
                _dField += c.Hfield;
                if (c.Hset <= 0) return;
                _dSetParties++;
                int capped = Math.Min(c.Hset, c.SCap);
                int used = DrillOn(Settings.Current) ? capped : c.Hset;   // godziny w osadzie, ktore zasada naprawde liczy (NIE - bez limitu, pomiar)
                _dS0 += c.Hset; _dS1 += capped;
                if (c.SKind == KTown)
                {
                    _dSetTown++;
                    int b = Math.Min(4, Math.Max(1, c.SDay)) - 1;
                    _tN[b]++; _tSum[b] += used; if (used > _tMax[b]) _tMax[b] = used;
                }
                else
                {
                    if (c.SKind == KCastle) _dSetCastle++; else _dSetVill++;
                    int b = Math.Min(3, Math.Max(1, c.SDay)) - 1;
                    _kN[b]++; _kSum[b] += used; if (used > _kMax[b]) _kMax[b] = used;
                }
            }
            catch (Exception e) { Stumble("Count", e); }
        }

        /// <summary>Linia musztry postoju doby (CampDrill.Daily, pod DrillLog) - zawsze zeruje liczniki doby.</summary>
        internal static void DrillLine()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.DrillLog) return;
                var sb = new StringBuilder(1200);
                sb.Append(P).Append("musztra dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | zasada ").Append(DrillOn(s) ? "TAK" : "NIE (sam pomiar - godziny w osadzie bez limitu, jak dotad)")
                  .Append(" | partii pod pomiarem ").Append(_dParties).Append(", z godzinami w osadzie ").Append(_dSetParties)
                  .Append(" (miasto ").Append(_dSetTown).Append(", zamek ").Append(_dSetCastle).Append(", wies ").Append(_dSetVill).Append(')')
                  .Append(" | miasto: ");
                for (int b = 0; b < 4; b++)
                    sb.Append(b > 0 ? "; " : "").Append("doba ").Append(b + 1).Append(b == 3 ? "+" : "").Append(" (limit ").Append(Cap(KTown, b + 1)).Append(") ").Append(_tN[b])
                      .Append(" partii, srednio ").Append(F2(_tN[b] > 0 ? _tSum[b] / _tN[b] : 0)).Append(", najwiecej ").Append(_tMax[b]);
                sb.Append(" | zamek i wies: ");
                for (int b = 0; b < 3; b++)
                    sb.Append(b > 0 ? "; " : "").Append("doba ").Append(b + 1).Append(b == 2 ? "+" : "").Append(" (limit ").Append(Cap(KCastle, b + 1)).Append(") ").Append(_kN[b])
                      .Append(" partii, srednio ").Append(F2(_kN[b] > 0 ? _kSum[b] / _kN[b] : 0)).Append(", najwiecej ").Append(_kMax[b]);
                sb.Append(" | godzin w osadzie bez limitu ").Append(F1(_dS0)).Append(" -> z limitem ").Append(F1(_dS1)).Append(", w polu ").Append(F1(_dField))
                  .Append(" | XP zasady obozu bez limitu ").Append((long)_dX0).Append(" -> z limitem ").Append((long)_dX1).Append(" (x").Append(F2(_dX0 > 0 ? _dX1 / _dX0 : 1.0)).Append(')')
                  .Append(" | strona gracza: partii ").Append(_dSideN).Append(", odjete ").Append(F1(_dSideCut)).Append(" h")
                  .Append(" | wejscia AI ").Append(_dEntries).Append(", roznica do StationaryStartTime srednio ").Append(F1(_dEntries > 0 ? _dDiffSum / _dEntries : 0))
                  .Append(" h, najwiecej ").Append(F1(_dDiffMax)).Append(" h; odtworzone ze StationaryStartTime ").Append(_dRestored).Append(" (Never ").Append(_dRestoredNever).Append(')')
                  .Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("DrillLine", e); }
            finally { ClearDrill(); }
        }

        // ------------------------------------------------------------ wydatki doby postoju (klucz TownStaySpend)
        private const double ByPurseCap = 3.0, ByPurseDays = 10.0;   // ta sama miara co markietani N6 (Sutlers) i zalogi m96 (SavingsByNeed)
        private const float MoraleFull = 3f, MoraleCap = 5f;          // +3 na dobe przy >= 1 zl na glowe; ostatnie zdarzenia najwyzej do +5 (polowa pulapu uczty 10)
        private static int _sP, _sTownN, _sCastleN, _sVillN, _sPaid, _sVill, _sSiege, _sSack, _sBattle, _sEmpty, _sNoFree, _sInactive, _sEscrow, _sResBroken, _sMorN, _sMorOver;
        private static long _sTaken, _sTown, _sTownFolk, _sCastle, _sCastleFolk, _sOut, _sCoffer, _sFolk;
        private static double _sMorG;
        private static int _pX; private static float _pM0, _pM1, _pGain;   // gracz tej doby: wydane, morale przed i po, dodane

        internal static bool SpendOn(Settings s) { return s != null && s.TownStaySpend && MenPurse.On; }

        private static void ClearSpend()
        {
            _sP = _sTownN = _sCastleN = _sVillN = _sPaid = _sVill = _sSiege = _sSack = _sBattle = _sEmpty = _sNoFree = _sInactive = _sEscrow = _sResBroken = _sMorN = _sMorOver = 0;
            _sTaken = _sTown = _sTownFolk = _sCastle = _sCastleFolk = _sOut = _sCoffer = _sFolk = 0;
            _sMorG = 0; _pX = 0; _pM0 = _pM1 = _pGain = 0f;
        }

        /// <summary>Raz na dobe (ArmouryBehavior, blok dobowy: po markietanach i zalogach m96, przed Mark(MSutlers)): partie rodow w osadach - wydatki, morale,
        /// komunikat gracza i linia doby. Lista jak Sutlers.Daily: AllLordParties bez gracza i gracz osobno (jedna regula).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (Campaign.Current == null || s == null) return;
            ClearSpend();
            var sw = Stopwatch.StartNew();
            bool on = SpendOn(s);
            float rate = MBMath.ClampFloat(s.SutlerDailyShare, 0f, 1f);
            if (on && rate > 0f)
            {
                var main = MobileParty.MainParty;
                foreach (var mp in MobileParty.AllLordParties) if (mp != null && mp != main) OneSafe(mp, rate, s);
                if (main != null) OneSafe(main, rate, s);
            }
            sw.Stop();
            if (_tOn && _tPhase > 0 && _tPhase < PDone) _tSpent += _pX;   // autotest: wydatki gracza w trakcie testu (MUSZT-6)
            try { PlayerMessage(s); } catch (Exception e) { Stumble("PlayerMessage", e); }
            try { if (s.LogEnabled) Log.Info(SpendLine(s, on, rate, sw.Elapsed.TotalMilliseconds)); } catch (Exception e) { Stumble("SpendLine", e); }
        }

        private static void OneSafe(MobileParty mp, float rate, Settings s)
        {
            try { One(mp, rate, s); }
            catch (Exception e) { Stumble("One", e); }   // wyjatek jednej partii - licznik, reszta biegnie dalej (bez globalnego wylacznika)
        }

        /// <summary>Jedna partia: warunki, wolna sakiewka (rezerwy markietanow), kwota (miara N6), zdjecie i wplata (MenPurse.LifeTo), kontrola, morale.</summary>
        private static void One(MobileParty mp, float rate, Settings s)
        {
            var st = mp.CurrentSettlement;
            if (st == null) return;                                         // w polu - markietani (Sutlers.One), tu nie liczone
            if (!mp.IsActive || mp.IsDisbanding) { _sInactive++; return; }
            int kind = Kind(st);
            if (kind == KNone) { if (st.IsVillage) _sVill++; else if (Besieged(st)) _sSiege++; return; }   // oblezona twierdza (karczmy zamkniete), rabowana wies, kryjowka
            _sP++;
            if (kind == KTown) _sTownN++; else if (kind == KCastle) _sCastleN++; else _sVillN++;
            if (kind == KVillage) { _sVill++; return; }                     // we wsi nic nie wydaja (jak dotad)
            if (mp.MapEvent != null) { _sBattle++; return; }
            if (MenPurse.Get(mp) <= 0) { _sEmpty++; return; }
            if (mp.IsMainParty && QuartermasterEscrow.Active) { _sEscrow++; return; }   // ekran zbrojowni gracza otwarty - rozliczenie wkladow w toku
            if (st.Town == null) { _sInactive++; return; }
            if (CitySack.StayWindow(st, mp)) { _sSack++; return; }        // okno lupu: wojsko zdobywcy nie placi w lupionej osadzie (bramka L3, bez jej licznika)
            long mend = MenPurse.CampMend(mp);
            int res, kept;
            int free = MenPurse.CampFree(mp, mend, 1f, out res, out kept);
            if (free <= 0) { _sNoFree++; return; }
            double fac = s.SutlersByPurse ? Math.Min(ByPurseCap, free / (double)Math.Max(1, MenPurse.WageOf(mp)) / ByPurseDays) : 1.0;
            int want = (int)Math.Min(free, (long)(free * Math.Min(1.0, rate * fac)));
            if (want <= 0) { _sNoFree++; return; }
            int men = mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
            // ---- od zdjecia do wplaty tylko arytmetyka i wplata (MenPurse.LifeTo - ta sama droga co "zycie" przy wyjezdzie); kontrola z odczytow przed/po
            int p0 = MenPurse.Get(mp), g0 = st.Town.Gold;
            int x = MenPurse.Take(mp, want);
            if (x <= 0) return;
            int folk = MenPurse.LifeTo(mp, st, x);
            long outd = p0 - MenPurse.Get(mp), cof = (long)st.Town.Gold - g0;
            // ---- liczniki (zloto juz na miejscu)
            _sPaid++; _sTaken += x; _sOut += outd; _sCoffer += cof; _sFolk += folk;
            if (kind == KTown) { _sTown += x; _sTownFolk += folk; } else { _sCastle += x; _sCastleFolk += folk; }
            if (MenPurse.Get(mp) < (long)res + kept) _sResBroken++;         // rezerwy (naprawy, odziez, oszczednosci K1) nietkniete - ma byc 0
            // ---- morale: +3 na dobe przy >= 1 zl na glowe (mniej - mniej), ostatnie zdarzenia najwyzej do +5 (gra zdejmuje 10% na dobe)
            float m0 = mp.RecentEventsMorale;
            float gain = Math.Min(MoraleFull, Math.Min(MoraleFull * x / Math.Max(1, men), Math.Max(0f, MoraleCap - m0)));
            if (gain > 0f)
            {
                mp.RecentEventsMorale = m0 + gain;
                _sMorN++; _sMorG += gain;
                if (mp.RecentEventsMorale > MoraleCap + 0.001f) _sMorOver++;
            }
            if (mp.IsMainParty) { _pX = x; _pM0 = m0; _pM1 = mp.RecentEventsMorale; _pGain = gain; }
        }

        /// <summary>Najblizszy swit gracza (pelna godzina) - limit "rano" w komunikacie i w linii.</summary>
        private static long NextMorning(long now) { return now + (((NightRest.PlayerDawn - (int)(now % 24)) % 24) + 24) % 24; }

        /// <summary>Komunikat gracza raz na dobe, gdy czeka w osadzie z postojem (EN): wydatki i morale, limit musztry rano.</summary>
        private static void PlayerMessage(Settings s)
        {
            var mp = MobileParty.MainParty;
            var st = mp != null ? mp.CurrentSettlement : null;
            int kind = Kind(st);
            if (kind == KNone) return;
            bool drill = DrillOn(s) && CampDrill.PlayerRuleOn;
            bool spent = _pX > 0;
            if (!drill && !spent) return;
            if (kind == KVillage && !drill) return;
            long now = (long)Math.Floor(CampaignTime.Now.ToHours), morning = NextMorning(now);
            long from, outAt; float daySet;
            CampDrill.PlayerStay(out from, out outAt, out daySet);
            int day = Day(from, morning), cap = Cap(kind, day);
            bool fewer = Cap(kind, day + 1) < cap;
            string hours = cap == 1 ? "1 hour" : cap + " hours";
            var sb = new StringBuilder(220);
            sb.Append(drill ? "Day " + day + " in " : "In ").Append(st.Name).Append(": ");
            if (kind == KVillage)
                sb.Append("the men idle about the village - they drill only ").Append(hours).Append(" each morning here (8 in camp)").Append(fewer ? ", and fewer each day they stay." : ".");
            else
            {
                if (spent) sb.Append("your men spent ").Append(_pX).Append(" denars on drink, dice and company").Append(_pGain >= 0.05f ? " (morale +" + _pGain.ToString("0.#", Inv) + ")" : "").Append('.');
                if (drill) sb.Append(spent ? " Here they" : "your men").Append(" drill only ").Append(hours).Append(" each morning (8 in camp)").Append(fewer ? ", and fewer each day they stay." : ".");
            }
            Log.Player(sb.ToString());
        }

        private static string SpendLine(Settings s, bool on, float rate, double ms)
        {
            var sb = new StringBuilder(1100);
            sb.Append(P).Append("wydatki dzien ").Append((int)CampaignTime.Now.ToDays);
            if (!on)
                sb.Append(" | wylaczone (Town Stay Spend / Men Purse Enabled) - zold wydaja tylko przy wyjezdzie z miasta, jak dotad");
            else
            {
                long ctl = _sOut - (_sCoffer + _sFolk);
                sb.Append(" | stawka ").Append((rate * 100f).ToString("0.0", Inv)).Append('%').Append(s.SutlersByPurse ? " x min(3, dni wolnej / 10)" : " (Sutlers By Purse wylaczone - bez mnoznika)")
                  .Append(" | partii w osadach ").Append(_sP).Append(" (miasta ").Append(_sTownN).Append(", zamki ").Append(_sCastleN).Append(", wsie ").Append(_sVillN).Append(')')
                  .Append(", wydaly ").Append(_sPaid).Append(": z sakiewek ").Append(_sTaken).Append(" -> kasy miast ").Append(_sTown).Append(" (w tym kiesy ludu ").Append(_sTownFolk)
                  .Append("), kasy zamkow ").Append(_sCastle).Append(" (").Append(_sCastleFolk).Append(')')
                  .Append(" | kontrola: zeszlo ").Append(_sOut).Append(" = przybylo w kasach ").Append(_sCoffer).Append(" + kiesy ludu ").Append(_sFolk).Append(" (roznica ").Append(ctl).Append(')')
                  .Append(" | rezerwy naruszone ").Append(_sResBroken)
                  .Append(" | morale: partii ").Append(_sMorN).Append(", dodane ").Append(F1(_sMorG)).Append(", ponad pulap ").Append((int)MoraleCap).Append(" po dodaniu ").Append(_sMorOver)
                  .Append(" | pominiete: wsie ").Append(_sVill).Append(", oblezone ").Append(_sSiege).Append(", okno lupu ").Append(_sSack).Append(", w bitwie ").Append(_sBattle)
                  .Append(", puste ").Append(_sEmpty).Append(", nic wolnego ").Append(_sNoFree).Append(", nieczynne ").Append(_sInactive).Append(", ekran zbrojowni gracza ").Append(_sEscrow);
            }
            sb.Append(" | gracz: ").Append(PlayerPart(on));
            if (on) sb.Append(" | koszt ").Append(F1(ms)).Append(" ms");
            sb.Append(" | potkniecia ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        private static string PlayerPart(bool on)
        {
            var mp = MobileParty.MainParty;
            var st = mp != null ? mp.CurrentSettlement : null;
            int kind = Kind(st);
            long now = (long)Math.Floor(CampaignTime.Now.ToHours);
            long from, outAt; float daySet;
            CampDrill.PlayerStay(out from, out outAt, out daySet);
            float capMax = CampDrill.PlayerDayCap;
            string drilled = ", w osadzie od switu " + F2(daySet) + " h (limit rano " + (capMax > 0f ? ((int)capMax).ToString(Inv) : "-");
            if (kind == KNone)
            {
                if (from >= 0) return "poza osada od " + (outAt >= 0 ? now - outAt : 0) + " h (doba postoju " + Day(from, now) + " trwa do " + FieldResetHours + " h w polu)" + drilled + ")";
                return (st != null ? "w osadzie bez postoju (" + st.Name + ")" : "w polu") + drilled + ")";
            }
            int dNext = Day(from, NextMorning(now));
            return KindName(kind) + " " + st.Name + ", doba postoju " + Day(from, now) + drilled + ", jutro " + Cap(kind, dNext) + ")"
                   + (on ? ", wydali " + _pX + ", morale +" + F1(_pGain) + " (" + F1(_pM0) + " -> " + F1(_pGain > 0f ? _pM1 : _pM0) + ")" : "");
        }

        // ------------------------------------------------------------ start sesji
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                long now = (long)Math.Floor(CampaignTime.Now.ToHours);
                int n = 0, never = 0; double days = 0;
                var main = MobileParty.MainParty;
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null || mp == main || !mp.IsActive || Kind(mp.CurrentSettlement) == KNone) continue;
                    bool nv;
                    long from = FromStationary(mp, now, out nv);
                    n++; if (nv) never++; days += Day(from, now);
                }
                long pFrom, pOut; float pSet;
                CampDrill.PlayerStay(out pFrom, out pOut, out pSet);
                TestClear();
                _tOn = TestWanted();
                if (_tOn) _tArmAt = now;
                Log.Info(P + "start sesji | musztra w osadzie (Drill Town Stay) " + (DrillOn(s) ? "TAK" : "NIE") + " | limit godzin w osadzie na dobe postoju: miasto "
                         + Cap(KTown, 1) + "/" + Cap(KTown, 2) + "/" + Cap(KTown, 3) + "/" + Cap(KTown, 4) + ", zamek i wies " + Cap(KCastle, 1) + "/" + Cap(KCastle, 2) + "/" + Cap(KCastle, 3)
                         + " (razem z obozem najwyzej " + (int)CampDrill.FullHours + " h od switu) | oblezona - jak oboz oblezenia | wypad < " + FieldResetHours + " h nie zeruje"
                         + " | wydatki (Town Stay Spend) " + (SpendOn(s) ? "TAK: stawka " + (MBMath.ClampFloat(s.SutlerDailyShare, 0f, 1f) * 100f).ToString("0.0", Inv) + "%"
                                                                 + (s.SutlersByPurse ? " x min(3, dni wolnej / 10)" : " (bez mnoznika sakiewki)") + ", miasto i zamek"
                                                               : "NIE (" + (s.TownStaySpend ? "Men Purse Enabled wylaczone" : "klucz wylaczony") + ")")
                         + " | morale +" + (int)MoraleFull + "/dobe do +" + (int)MoraleCap
                         + " | AI odtworzone ze StationaryStartTime " + n + " (srednio doba " + F1(n > 0 ? days / n : 0) + ", Never " + never + ")"
                         + " | gracz: postoj od " + (pFrom >= 0 ? pFrom.ToString(Inv) + " (doba " + Day(pFrom, now) + ")" : "-") + ", poza od " + (pOut >= 0 ? pOut.ToString(Inv) : "-")
                         + ", dzis w osadzie " + F2(pSet) + " h | autotest postoju " + (_tOn ? "TAK (plik " + AutoFlagName + ")" : "nie") + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>CampDrill.RegisterEvents: takt autotestu postoju (TickEvent - tylko przy plynacym czasie; nieuzbrojony - od razu wyjscie).</summary>
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(b, delegate (float dt) { if (_tOn) TestTick(); });
        }

        // ------------------------------------------------------------ autotest postoju (plik CrashScribe\armoury-postoj.flag + DLL autotestu, LogGate.Mode 1/2)
        // DLL autotestu sama osadza gracza w miescie i czeka tam (town_wait_menus); poza miastem zatrzymuje zegar, a na czystej mapie przenosi gracza do miasta.
        // Driver: F1 postoj w tym miescie >= 100 h, wyjscie o 12:00; F2 6 h w polu, powrot do tego samego miasta na 26 h (doba postoju trwa); F3 >= 24 h w polu
        // z cwiczeniami z menu obozu (Train the men, 8 h) i powrot o 2:00 (przed switem); F4 nowy postoj - trzy poranki (limit 4, 3, 2), podsumowanie, gracz
        // zostaje w miescie. W polu gracz stoi w menu obozu arm_camp_wait z zegarem plynacym i zablokowanym (SetTimeControlModeLock) - inaczej DLL zatrzymalaby
        // zegar i przeniosla gracza; blokada zdjeta przy kazdym wejsciu do miasta i przy przerwaniu. Kroki tylko z TickEvent (nigdy z opcji menu oczekiwania).
        // Kazda faza ma limit czasu; po przekroczeniu albo po 3 wyjatkach - linia "przerwany" i rozbrojenie drivera (funkcja gry dziala dalej).
        internal const string AutoFlagName = "armoury-postoj.flag";
        private const int PWait = 0, PF1 = 1, PF2Field = 2, PF2Town = 3, PF3Field = 4, PF4Town = 5, PDone = 6;
        private static bool _tOn, _tLocked, _tEntering, _tDrillOn, _tDrillDone, _tF2Cont, _tF4New;
        private static int _tPhase, _tFails, _tDayLast, _tF2Day, _tF4Mornings;
        private static long _tArmAt, _tAt, _tHour = -1, _tDawn = long.MinValue, _tFrom = -1, _tF4From = -1, _tRowFrom = -1, _tSpent;
        private static float _tSetLast, _tCapLast, _tF3Menu = -1f;
        private static Settlement _tTown;
        private static readonly List<string> _tF1 = new List<string>(), _tF4 = new List<string>();
        private static readonly List<int> _tF1Caps = new List<int>(), _tF1Days = new List<int>(), _tF4Caps = new List<int>();
        private static readonly List<float> _tF4Set = new List<float>();

        private static void TestClear()
        {
            _tLocked = _tEntering = _tDrillOn = _tDrillDone = _tF2Cont = _tF4New = false;
            _tPhase = PWait; _tFails = 0; _tDayLast = 0; _tF2Day = 0; _tF4Mornings = 0;
            _tArmAt = _tAt = 0; _tHour = -1; _tDawn = long.MinValue; _tFrom = _tF4From = _tRowFrom = -1; _tSpent = 0;
            _tSetLast = _tCapLast = 0f; _tF3Menu = -1f; _tTown = null;
            _tF1.Clear(); _tF4.Clear(); _tF1Caps.Clear(); _tF1Days.Clear(); _tF4Caps.Clear(); _tF4Set.Clear();
        }

        private static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
            }
            catch { return false; }
        }

        private static string PhaseName(int p)
        {
            switch (p)
            {
                case PWait: return "F0";
                case PF1: return "F1";
                case PF2Field: case PF2Town: return "F2";
                case PF3Field: return "F3";
                case PF4Town: return "F4";
                default: return "koniec";
            }
        }

        /// <summary>Czesc wspolna linii autotestu: menu, osada, godzina, doba postoju, limit, godziny w osadzie od switu.</summary>
        private static string Where(string menu, long now)
        {
            var mp = MobileParty.MainParty;
            var st = mp != null ? mp.CurrentSettlement : null;
            long from, outAt; float set;
            CampDrill.PlayerStay(out from, out outAt, out set);
            int kind = Kind(st);
            int d = Day(from, now);
            return "menu " + (menu ?? "-") + ", osada " + (st != null ? st.Name.ToString() : "-") + ", godzina " + (now % 24) + ", doba postoju " + (from >= 0 ? d.ToString(Inv) : "-")
                   + ", limit " + (kind != KNone && from >= 0 ? Cap(kind, d).ToString(Inv) : "-") + ", godzin w osadzie " + F2(set);
        }

        private static void TestLine(string what, string menu, long now, string extra)
        {
            Log.Info(P + "autotest - " + what + " | " + Where(menu, now) + (extra != null ? " | " + extra : "") + ".");
        }

        private static void Unlock()
        {
            if (!_tLocked) return;
            _tLocked = false;
            try { if (Campaign.Current != null) Campaign.Current.SetTimeControlModeLock(false); } catch (Exception e) { Stumble("Autotest.Unlock", e); }
        }

        private static void Lock()
        {
            var c = Campaign.Current;
            if (c == null) return;
            if (c.TimeControlModeLock && !_tLocked) return;   // cudza blokada (okno portretu) - nie ruszamy
            if (!c.TimeControlModeLock) { c.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward; c.SetTimeControlModeLock(true); }
            _tLocked = true;
        }

        /// <summary>Przerwanie drivera: linia, zdjecie blokady zegara; w polu wyjscie z menu obozu (DLL autotestu osadzi gracza w miescie).</summary>
        private static void TestAbort(string why, string menu, long now)
        {
            try { TestLine("przerwany", menu, now, "faza " + PhaseName(_tPhase) + ", powod: " + why); } catch { }
            _tOn = false;
            Unlock();
            try
            {
                var mp = MobileParty.MainParty;
                if (mp != null && mp.CurrentSettlement == null && (menu == "arm_camp_wait" || menu == "arm_camp_drill_wait") && Campaign.Current.CurrentMenuContext != null) GameMenu.ExitToLast();
            }
            catch (Exception e) { Stumble("Autotest.Abort", e); }
        }

        private static void TestTick()
        {
            long now = 0; string menu = null;
            try
            {
                var c = Campaign.Current;
                var mp = MobileParty.MainParty;
                if (c == null || mp == null) return;
                if (!(TaleWorlds.Core.GameStateManager.Current != null && TaleWorlds.Core.GameStateManager.Current.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState)) return;   // misja, inne stany
                if (c.ConversationManager != null && c.ConversationManager.IsConversationInProgress) return;
                now = (long)Math.Floor(CampaignTime.Now.ToHours);
                var ctx = c.CurrentMenuContext;
                menu = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : null;
                bool field = _tPhase == PF2Field || _tPhase == PF3Field;
                if (mp.MapEvent != null) { if (field) TestAbort("bitwa w polu", menu, now); return; }
                if (field && !_tEntering && PlayerEncounter.Current != null) { TestAbort("spotkanie w polu", menu, now); return; }
                if (_tEntering) { Entering(mp, menu, now); return; }
                // w polu gracz stoi w menu obozu - na czystej mapie DLL autotestu przenioslaby go do miasta (z ticku kampanii wolno otworzyc menu)
                if (field && menu == null) { GameMenu.ActivateGameMenu("arm_camp_wait"); return; }
                Mornings(menu, now);
                if (now == _tHour) return;   // reszta - raz na godzine gry
                _tHour = now;
                if (field && !_tLocked) Lock();   // ktos zdjal blokade (nie powinno sie zdarzyc) - z powrotem
                switch (_tPhase)
                {
                    case PWait: StepWait(mp, menu, now); break;
                    case PF1: StepTown(mp, menu, now, 100, true); break;
                    case PF2Field: if (now - _tAt >= 6) Enter(mp, menu, now); break;   // krotki wypad: 6 h w polu
                    case PF2Town: StepTown(mp, menu, now, 26, false); break;
                    case PF3Field: StepField(mp, menu, now); break;
                    case PF4Town: StepF4(mp, menu, now); break;
                }
            }
            catch (Exception e)
            {
                Stumble("Autotest", e);
                if (++_tFails >= 3) TestAbort("3 wyjatki drivera (pierwszy w logu jako ERROR)", menu, now);
            }
        }

        /// <summary>F0: czeka, az DLL autotestu osadzi gracza w miescie (town_wait_menus) i licznik postoju ruszy; menu "town" - czekanie jak opcja gry town_wait.</summary>
        private static void StepWait(MobileParty mp, string menu, long now)
        {
            var st = mp.CurrentSettlement;
            if (now - _tArmAt > 72) { TestAbort("F0 - gracz nie czeka w miescie po 72 h", menu, now); return; }
            if (st == null || Kind(st) != KTown || mp.Army != null) return;
            if (menu == "town" && PlayerEncounter.Current != null) { PlayerEncounter.Current.IsPlayerWaiting = true; GameMenu.SwitchToMenu("town_wait_menus"); return; }
            if (menu != "town_wait_menus") return;
            long from, outAt; float set;
            CampDrill.PlayerStay(out from, out outAt, out set);
            if (from < 0) return;   // licznik postoju rusza w godzinie gry (Drill.Hourly -> CampDrill.AutoHour)
            _tTown = st; _tFrom = from; _tAt = now; _tPhase = PF1; _tDayLast = Day(from, now);
            TestLine("F1 wejscie", menu, now, "okno lupu " + (CitySack.StayWindow(st, mp) ? "TAK (wydatki wstrzymane)" : "nie") + ", sakiewka ludzi " + MenPurse.Get(mp)
                     + ", zasada gracza " + (CampDrill.PlayerRuleOn ? "TAK" : "NIE") + ", Drill Town Stay " + (DrillOn(Settings.Current) ? "TAK" : "NIE"));
        }

        /// <summary>F1 i F2 w miescie: postoj w miescie testu; wyjscie po `hours` h (F1 - dopiero o 12:00).</summary>
        private static void StepTown(MobileParty mp, string menu, long now, int hours, bool noon)
        {
            if (mp.CurrentSettlement != _tTown || menu != "town_wait_menus")
            {
                if (now - _tAt > hours + 48) TestAbort(PhaseName(_tPhase) + " - gracz nie czeka w miescie testu (" + (mp.CurrentSettlement != null ? mp.CurrentSettlement.Name.ToString() : "pole") + ")", menu, now);
                return;
            }
            if (now - _tAt > hours + 48) { TestAbort(PhaseName(_tPhase) + " - limit czasu w miescie", menu, now); return; }
            if (now - _tAt < hours) return;
            if (noon && now % 24 != 12) return;
            TestLine(PhaseName(_tPhase) + " wyjscie", menu, now, "w miescie " + (now - _tAt) + " h");
            Leave(mp, now);
            _tPhase = _tPhase == PF1 ? PF2Field : PF3Field;
            _tAt = now; _tDrillOn = _tDrillDone = false;
        }

        /// <summary>Wyjscie z miasta w jednym takcie (jak opcja gry "Leave" - game_menu_settlement_leave_on_consequence) z zegarem zablokowanym na biegu i od razu
        /// menu obozu (gracz w polu w menu - DLL autotestu go nie przenosi). Z ticku kampanii, nie z opcji menu.</summary>
        private static void Leave(MobileParty mp, long now)
        {
            Lock();
            if (PlayerEncounter.Current != null) PlayerEncounter.Current.IsPlayerWaiting = false;
            var st = mp.CurrentSettlement;
            if (st != null) mp.Position = st.GatePosition;
            PlayerEncounter.LeaveSettlement();
            PlayerEncounter.Finish();
            mp.SetMoveModeHold();
            GameMenu.ActivateGameMenu("arm_camp_wait");
        }

        /// <summary>Powrot do miasta testu: wyjscie z menu obozu, pod brame i zwykle spotkanie z osada (jak DLL autotestu); dalej Entering.</summary>
        private static void Enter(MobileParty mp, string menu, long now)
        {
            if (_tTown == null) { TestAbort("brak miasta testu", menu, now); return; }
            if (Campaign.Current.CurrentMenuContext != null) GameMenu.ExitToLast();
            mp.SetMoveModeHold();
            mp.Position = _tTown.GatePosition;
            EncounterManager.StartSettlementEncounter(mp, _tTown);
            _tEntering = true; _tAt = now;
        }

        /// <summary>Po spotkaniu z miastem: menu "town" - zdjecie blokady zegara i czekanie (jak opcja gry town_wait); town_wait_menus - linia wejscia i faza miasta.</summary>
        private static void Entering(MobileParty mp, string menu, long now)
        {
            if (menu != null && menu.EndsWith("_outside", StringComparison.Ordinal)) { TestAbort("nie wpuszczono do miasta (menu " + menu + ")", menu, now); return; }
            if (now - _tAt > 6) { TestAbort("wejscie do miasta trwa ponad 6 h", menu, now); return; }
            if (mp.CurrentSettlement != _tTown) return;
            if (menu == "town")
            {
                Unlock();   // czekanie w miescie - zegar prowadzi menu oczekiwania (StartWait), jak u DLL autotestu
                if (PlayerEncounter.Current != null) PlayerEncounter.Current.IsPlayerWaiting = true;
                GameMenu.SwitchToMenu("town_wait_menus");
                return;
            }
            if (menu != "town_wait_menus") return;
            Unlock();
            long from, outAt; float set;
            CampDrill.PlayerStay(out from, out outAt, out set);
            if (from < 0) return;   // nowy postoj (F4) - licznik rusza w godzinie gry (Drill.Hourly -> CampDrill.AutoHour)
            _tEntering = false; _tHour = now;
            if (_tPhase == PF2Field)
            {
                _tPhase = PF2Town; _tAt = now;
                _tF2Day = Day(from, now); _tF2Cont = from >= 0 && from == _tFrom && _tF2Day > 1;
                TestLine("F2 wejscie", menu, now, "po " + 6 + " h w polu, kontynuacja postoju " + (_tF2Cont ? "TAK" : "NIE") + " (poczatek postoju " + from + ", w F1 " + _tFrom + ")");
            }
            else
            {
                _tPhase = PF4Town; _tAt = now; _tF4From = from;
                _tF4New = from >= 0 && from != _tFrom && Day(from, now) == 1;
                TestLine("F4 wejscie", menu, now, "po polu (F3), nowy postoj " + (_tF4New ? "TAK" : "NIE") + " (poczatek postoju " + from + ")");
            }
        }

        /// <summary>F3: >= 24 h w polu w menu obozu, w dzien cwiczenia z menu (arm_camp_drill_wait - pelny dzien 8 h), powrot o 2:00 (przed switem).</summary>
        private static void StepField(MobileParty mp, string menu, long now)
        {
            if (now - _tAt > 60) { TestAbort("F3 - limit czasu w polu", menu, now); return; }
            int hod = (int)(now % 24);
            if (menu == "arm_camp_wait" && _tDrillOn && !_tDrillDone)
            {
                _tDrillDone = true; _tF3Menu = CampDrill.PlayerDayHours;
                TestLine("F3 cwiczenia koniec", menu, now, "z menu obozu dzis od switu " + F2(_tF3Menu) + " h");
            }
            if (menu == "arm_camp_wait" && !_tDrillOn && hod >= 6 && hod < 12 && CampDrill.DayHour(hod) && NightRest.DawnDebtOf(mp) < 1 && CampDrill.PlayerDayHours < 0.01f)
            {
                _tDrillOn = true;
                GameMenu.SwitchToMenu("arm_camp_drill_wait");   // zwykle menu obozu -> menu cwiczen (z ticku kampanii; menu cwiczen samo startuje czekanie)
                TestLine("F3 cwiczenia start", "arm_camp_drill_wait", now, "w polu od " + (now - _tAt) + " h");
                return;
            }
            if (_tDrillDone && now - _tAt >= FieldResetHours && hod == 2) { TestLine("F3 powrot", menu, now, "w polu " + (now - _tAt) + " h"); Enter(mp, menu, now); }
        }

        /// <summary>F4: nowy postoj w miescie testu - trzy poranki (Mornings), potem podsumowanie; gracz zostaje w miescie.</summary>
        private static void StepF4(MobileParty mp, string menu, long now)
        {
            if (now - _tAt > 96) { TestAbort("F4 - limit czasu w miescie", menu, now); return; }
            if (_tF4Mornings >= 3) Summary(menu, now);
        }

        /// <summary>Raz na dobe od switu gracza: godziny w osadzie zakonczonej doby i limit jej poranka (najwyzszy limit tej doby - z pierwszej godziny w osadzie),
        /// przypisane do pobytu wedlug poczatku postoju: pobyt 1 (F1 i F2 - linie "F1 rano"), pobyt 2 (F4 - "F4 rano").</summary>
        private static void Mornings(string menu, long now)
        {
            long dawn = (long)Math.Floor((CampaignTime.Now.ToHours - NightRest.PlayerDawn) / 24.0);
            if (_tDawn == long.MinValue) { _tDawn = dawn; return; }
            if (dawn != _tDawn)
            {
                if (_tCapLast > 0f && _tRowFrom >= 0 && (_tRowFrom == _tFrom || _tRowFrom == _tF4From))
                {
                    bool f4 = _tRowFrom == _tF4From;
                    string row = "doba " + _tDayLast + " limit " + (int)_tCapLast + " godzin " + F2(_tSetLast);
                    if (f4) { _tF4.Add(row); _tF4Caps.Add((int)_tCapLast); _tF4Set.Add(_tSetLast); _tF4Mornings++; }
                    else { _tF1.Add(row); _tF1Caps.Add((int)_tCapLast); _tF1Days.Add(_tDayLast); }
                    Log.Info(P + "autotest - " + (f4 ? "F4" : "F1") + " rano | doba postoju " + _tDayLast + ", limit " + (int)_tCapLast + ", godzin w osadzie " + F2(_tSetLast) + ".");
                }
                _tDawn = dawn; _tCapLast = 0f; _tSetLast = 0f; _tRowFrom = -1;
            }
            if (_tPhase == PWait || _tPhase == PDone) return;
            long from, outAt; float set;
            CampDrill.PlayerStay(out from, out outAt, out set);
            float cap = CampDrill.PlayerDayCap;
            if (cap > 0f && _tCapLast <= 0f) { _tDayLast = Day(from, now); _tRowFrom = from; }   // doba postoju poranka - z pierwszej godziny w osadzie tej doby
            _tSetLast = set; _tCapLast = cap;
        }

        private static void Summary(string menu, long now)
        {
            bool f1Down = true; for (int i = 1; i < _tF1Caps.Count; i++) if (_tF1Caps[i] > _tF1Caps[i - 1]) f1Down = false;
            bool f1Floor = true; for (int i = 0; i < _tF1Caps.Count; i++) if (_tF1Days[i] >= 4 && _tF1Caps[i] != Cap(KTown, 4)) f1Floor = false;
            bool f4 = _tF4Caps.Count >= 3 && _tF4Caps[0] == Cap(KTown, 1) && _tF4Caps[1] == Cap(KTown, 2) && _tF4Caps[2] == Cap(KTown, 3);
            bool f4h = true; for (int i = 0; i < _tF4Set.Count; i++) if (_tF4Set[i] > _tF4Caps[i] + 0.01f || _tF4Set[i] <= 0f) f4h = false;
            bool f3 = _tF3Menu >= CampDrill.FullHours - 0.01f;
            bool ok = f1Down && f1Floor && _tF2Cont && f3 && _tF4New && f4 && f4h;
            Log.Info(P + "autotest - podsumowanie | F1 poranki: " + string.Join("; ", _tF1.ToArray()) + " - limity nierosnace " + (f1Down ? "TAK" : "NIE") + ", od 4. doby limit 1 " + (f1Floor ? "TAK" : "NIE")
                     + " | F2 kontynuacja " + (_tF2Cont ? "TAK" : "NIE") + " (doba " + _tF2Day + ") | F3 z menu obozu " + F2(_tF3Menu) + " h - " + (f3 ? "TAK" : "NIE")
                     + " | F4 nowy postoj " + (_tF4New ? "TAK" : "NIE") + ", poranki: " + string.Join("; ", _tF4.ToArray()) + " - limity 4/3/2 " + (f4 ? "TAK" : "NIE") + ", godziny w limicie i > 0 " + (f4h ? "TAK" : "NIE")
                     + " | wydatki gracza w tescie " + _tSpent + " | " + Where(menu, now) + " | wynik " + (ok ? "TAK" : "NIE") + ".");
            _tPhase = PDone; _tOn = false;
            Unlock();
        }
    }
}
