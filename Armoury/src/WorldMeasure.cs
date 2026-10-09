using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// MIARA SWIATA - CZESC 1: MARSZ (paczka T6, noc 08/09.10; audyt 2026-10-09, raporty 07, 09 P-1, uwagi S3, S7). Sam log - niczego
    /// nie zmienia w grze i nie ma stanu w zapisie (po wczytaniu pierwsza doba liczy od pierwszej godziny po wczytaniu).
    ///
    /// Co godzine: dla kazdej partii lorda AI (MobileParty.AllLordParties, bez gracza) zapamietujemy pozycje; jesli godzina byla
    /// godzina RUCHU (partia poza osada, bez starcia, bez obozu oblezniczego, nie dolaczona do armii, nie na morzu, nie w trakcie
    /// wsiadania / zejscia na lad, nie na tratwie) i przesuniecie od poprzedniej godziny > 0.01 jedn., dodajemy je do sumy doby.
    /// Skok > 20 jedn. w godzine (uwolnienie z niewoli, wczytanie, nowa partia w innym miejscu) nie jest marszem - tylko licznik.
    ///
    /// Raz na dobe jedna linia "Miara: marsz": km/dobe = suma x Wayfinder.KmPerUnit (4.75) dla partii z co najmniej 12 h ruchu
    /// tej doby - mediana i p90, osobno: (1) partie z ponad 300 ludzmi albo wodzowie armii (probka glowna), (2) wodzowie armii,
    /// (3) partie czysto konne (kazdy czlowiek na koniu; kazda wielkosc - wsrod partii > 300 ludzi takich prawie nie ma);
    /// do tego czynne WorldPacePercent (S3: linia "WorldPace: mapa" przy starcie pokazuje domyslna z kodu, zanim MCM wczyta plik).
    /// Pierwotne "przesuniecie netto miedzy dobami" odpadlo (S7): stojacy w osadach, oblegajacy i patrolujacy w kolko zanizaliby mediane.
    ///
    /// Koszt: godzinny tik - jedna petla po partiach lordow (O(1) na partie: pozycja i slownik), dobowy - sortowanie probki.
    /// Kazda partia w try, licznik potkniec (CLAUDE.md: nie gasic funkcji).
    ///
    /// Poprawki po recenzji T6: godzina liczy sie jako marsz tylko, gdy partia byla w ruchu na OBU koncach godziny (Track.WasMoving) -
    /// odpada godzina po zejsciu na lad (odcinek po morzu), godzina wyjscia z osady i godzina wejscia do osady / bitwy / oblezenia;
    /// km/dobe to wiec dolna granica (kilka procent), ale kazda policzona godzina to czysty marsz. Numer godziny rosnie takze przy
    /// wylaczonym przelaczniku (ciaglosc pek, stara pozycja nie wejdzie jako jedna godzina). W linii: mediana ludzi w kazdej grupie
    /// (porownanie wodzow z konnymi przy roznej wielkosci) i koszt (Stopwatch: godzinne razem i maks, dobowe).
    /// </summary>
    internal static class WorldMeasure
    {
        private const float MinStep = 0.01f;       // przesuniecie godzinowe ponizej - partia stoi
        private const float MaxStep = 20f;         // przesuniecie godzinowe powyzej - przeniesienie, nie marsz
        private const int MinMoveHours = 12;       // do probki tylko partie z co najmniej 12 h ruchu tej doby
        private const int BigParty = 300;          // probka glowna: ponad 300 ludzi albo wodz armii

        private sealed class Track
        {
            public Vec2 Pos; public int Stamp = -1;          // pozycja i numer godziny ostatniego odczytu
            public bool WasMoving;                           // czy przy ostatnim odczycie partia byla w ruchu (nie w osadzie, nie na morzu ...)
            public float Sum;                                // suma przesuniec w godzinach ruchu tej doby (jedn. mapy)
            public int MoveH, LeadH, MountH, Men;            // godziny ruchu, w tym jako wodz armii i czysto konno; najwiecej ludzi w ruchu
            public int CampH;                                // T10: godziny ruchu w oknie obozu swiata jako samotny lord (nie wodz armii), kazda wielkosc
        }

        private static readonly Dictionary<MobileParty, Track> _t = new Dictionary<MobileParty, Track>();
        private static int _stamp;                 // numer godziny (od startu sesji)
        private static int _jumps, _stumbles;      // liczniki doby
        private static long _hourTicks, _hourMax;  // koszt godzinnych tikow doby (Stopwatch: suma i maksimum)

        internal static void Reset() { _t.Clear(); _stamp = 0; _jumps = 0; _stumbles = 0; _hourTicks = 0; _hourMax = 0; }

        private static bool On() { var s = Settings.Current; return s != null && s.WorldMeasureLog; }

        internal static void Hourly()
        {
            long t0 = 0;
            try
            {
                _stamp++;                                   // takze przy wylaczonym przelaczniku - ciaglosc godzin pek po wylaczeniu
                if (!On() || Campaign.Current == null) return;
                t0 = Stopwatch.GetTimestamp();
                var all = MobileParty.AllLordParties;
                if (all == null) return;
                var main = MobileParty.MainParty;
                // T10 (uwaga krytyki 4): godzina, ktora wlasnie minela, byla godzina obozu swiata - ruch samotnych lordow kazdej wielkosci
                bool campHour = NightRest.InCamp(CampaignTime.Now.GetHourOfDay - 1);
                for (int i = 0; i < all.Count; i++)
                {
                    try
                    {
                        var mp = all[i];
                        if (mp == null || mp == main || !mp.IsActive) continue;
                        var pos = mp.GetPosition2D;
                        Track t;
                        if (!_t.TryGetValue(mp, out t)) { t = new Track(); _t[mp] = t; }
                        bool moving = mp.CurrentSettlement == null && mp.MapEvent == null && mp.BesiegerCamp == null && mp.AttachedTo == null
                                      && !mp.IsCurrentlyAtSea && !mp.IsTransitionInProgress && !mp.IsInRaftState;
                        if (moving && t.WasMoving && t.Stamp == _stamp - 1)   // ruch na obu koncach godziny
                        {
                            float d = pos.Distance(t.Pos);
                            if (d > MaxStep) _jumps++;
                            else if (d > MinStep)
                            {
                                t.Sum += d; t.MoveH++;
                                if (mp.Army != null && mp.Army.LeaderParty == mp) t.LeadH++;
                                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                                if (men > t.Men) t.Men = men;
                                if (men > 0 && mp.Party != null && mp.Party.NumberOfMenWithoutHorse == 0) t.MountH++;
                                if (campHour && !(mp.Army != null && mp.Army.LeaderParty == mp)) t.CampH++;
                            }
                        }
                        t.Pos = pos; t.Stamp = _stamp; t.WasMoving = moving;
                    }
                    catch { _stumbles++; }
                }
            }
            catch (Exception e) { Log.Error("WorldMeasure.Hourly", e); }
            finally
            {
                if (t0 != 0) { long dt = Stopwatch.GetTimestamp() - t0; _hourTicks += dt; if (dt > _hourMax) _hourMax = dt; }
            }
        }

        private static string Ms(long ticks) { return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.00", CultureInfo.InvariantCulture); }

        // mediana liczby ludzi (najwiecej w godzinach ruchu) w grupie
        private static string MenMed(List<int> men)
        {
            if (men.Count == 0) return "";
            men.Sort();
            int n = men.Count;
            float med = (n % 2 == 1) ? men[n / 2] : 0.5f * (men[n / 2 - 1] + men[n / 2]);
            return ", ludzie mediana " + F(med);
        }

        private static string F(float v) { return v.ToString("0", CultureInfo.InvariantCulture); }

        // mediana i p90 (najblizsza ranga) z posortowanej listy
        private static string Stats(List<float> km)
        {
            if (km.Count == 0) return "0 partii";
            km.Sort();
            int n = km.Count;
            float med = (n % 2 == 1) ? km[n / 2] : 0.5f * (km[n / 2 - 1] + km[n / 2]);
            float p90 = km[Math.Max(0, Math.Min(n - 1, (int)Math.Ceiling(0.9 * n) - 1))];
            return n + " partii, km/dobe mediana " + F(med) + ", p90 " + F(p90) + ", min " + F(km[0]) + ", maks " + F(km[n - 1]);
        }

        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!On() || Campaign.Current == null) { _t.Clear(); _jumps = 0; _stumbles = 0; _hourTicks = 0; _hourMax = 0; return; }
                int day = (int)CampaignTime.Now.ToDays - 1;      // doba, ktora sie skonczyla (jak "Ludzie:")
                var big = new List<float>(); var lead = new List<float>(); var mounted = new List<float>();
                var bigMen = new List<int>(); var leadMen = new List<int>(); var mountedMen = new List<int>();
                int tracked = 0, few = 0; long hoursBig = 0;
                int campH = 0, campParties = 0;   // T10: ruch samotnych lordow w oknie obozu (kazda wielkosc, takze < 12 h ruchu)
                var dead = new List<MobileParty>();
                foreach (var kv in _t)
                {
                    var mp = kv.Key; var t = kv.Value;
                    try
                    {
                        if (mp == null || !mp.IsActive || t.Stamp < _stamp) { dead.Add(mp); continue; }   // martwa albo zniknela z listy lordow
                        tracked++;
                        if (t.CampH > 0) { campH += t.CampH; campParties++; }
                        if (t.MoveH >= MinMoveHours)
                        {
                            float km = t.Sum * Wayfinder.KmPerUnit;
                            bool isLead = t.LeadH * 2 >= t.MoveH;
                            if (t.Men > BigParty || isLead) { big.Add(km); bigMen.Add(t.Men); hoursBig += t.MoveH; }
                            if (isLead) { lead.Add(km); leadMen.Add(t.Men); }
                            if (t.MountH * 2 >= t.MoveH) { mounted.Add(km); mountedMen.Add(t.Men); }
                        }
                        else if (t.MoveH > 0) few++;
                    }
                    catch { _stumbles++; }
                    t.Sum = 0f; t.MoveH = 0; t.LeadH = 0; t.MountH = 0; t.Men = 0; t.CampH = 0;   // pozycja i numer godziny zostaja - ciaglosc przez polnoc
                }
                foreach (var mp in dead) _t.Remove(mp);

                var s = Settings.Current;
                int pace = s != null ? s.WorldPacePercent : 100;
                var sb = new StringBuilder();
                sb.Append("Miara: marsz dzien ").Append(day)
                  .Append(" | WorldPace z MCM ").Append(pace).Append('%').Append(pace >= 100 || pace < 5 ? " (suwak poza 5-99 - bez zmiany predkosci)" : "")
                  .Append(" | partie lordow AI z >= ").Append(MinMoveHours).Append(" h ruchu (suma przesuniec godzinowych tylko w pelnych godzinach ruchu x ")
                  .Append(Wayfinder.KmPerUnit.ToString("0.00", CultureInfo.InvariantCulture)).Append(" km/jedn.)")
                  .Append(" | ponad ").Append(BigParty).Append(" ludzi albo wodzowie armii: ").Append(Stats(big)).Append(MenMed(bigMen));
                if (big.Count > 0) sb.Append(", srednio ").Append((hoursBig / (float)big.Count).ToString("0.0", CultureInfo.InvariantCulture)).Append(" h ruchu");
                sb.Append(" | wodzowie armii: ").Append(Stats(lead)).Append(MenMed(leadMen))
                  .Append(" | czysto konne (kazda wielkosc): ").Append(Stats(mounted)).Append(MenMed(mountedMen))
                  .Append(" | sledzonych ").Append(tracked).Append(", w ruchu ponizej ").Append(MinMoveHours).Append(" h: ").Append(few)
                  .Append("; skoki > ").Append(F(MaxStep)).Append(" jedn./h pominiete: ").Append(_jumps)
                  .Append(" | okno obozu ").Append(NightRest.CampStart).Append(":00-").Append(NightRest.CampEnd).Append(":00 (samotni lordowie, kazda wielkosc): ")
                  .Append(campH).Append(" h ruchu, partii ").Append(campParties).Append('.');
                if (_stumbles > 0) sb.Append(" Potkniecia miary: ").Append(_stumbles).Append('.');
                sb.Append(" Koszt: godzinne razem ").Append(Ms(_hourTicks)).Append(" ms (maks ").Append(Ms(_hourMax))
                  .Append(" ms), dobowe ").Append(Ms(Stopwatch.GetTimestamp() - t0)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("WorldMeasure.Daily", e); }
            finally { _jumps = 0; _stumbles = 0; _hourTicks = 0; _hourMax = 0; }
        }
    }
}
