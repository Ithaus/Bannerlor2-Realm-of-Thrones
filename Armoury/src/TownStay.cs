using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
            ClearDrill();
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
                Log.Info(P + "start sesji | musztra w osadzie (Drill Town Stay) " + (DrillOn(s) ? "TAK" : "NIE") + " | limit godzin w osadzie na dobe postoju: miasto "
                         + Cap(KTown, 1) + "/" + Cap(KTown, 2) + "/" + Cap(KTown, 3) + "/" + Cap(KTown, 4) + ", zamek i wies " + Cap(KCastle, 1) + "/" + Cap(KCastle, 2) + "/" + Cap(KCastle, 3)
                         + " (razem z obozem najwyzej " + (int)CampDrill.FullHours + " h od switu) | oblezona - jak oboz oblezenia | wypad < " + FieldResetHours + " h nie zeruje"
                         + " | AI odtworzone ze StationaryStartTime " + n + " (srednio doba " + F1(n > 0 ? days / n : 0) + ", Never " + never + ")"
                         + " | gracz: postoj od " + (pFrom >= 0 ? pFrom.ToString(Inv) + " (doba " + Day(pFrom, now) + ")" : "-") + ", poza od " + (pOut >= 0 ? pOut.ToString(Inv) : "-")
                         + ", dzis w osadzie " + F2(pSet) + " h.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }
    }
}
