using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// A12 (paczka stare-a12; CO-ZOSTALO 14.10 wiersz A12, diagnoza drobne2 "Ludzie przy zniknieciu partii"): LUDZIE PARTII, KTORA ZNIKA Z LUDZMI.
    /// Dowod (bieg 6-letni 14/15.10, linie diagnozy ze stosem): (a) JeorMutinyEvent (fabula ROT) -> DestroyClanAction -> DestroyPartyAction z niszczycielem - 6 partii Strazy
    /// z zywymi ludzmi (sklad 177-489 = slownik), poza bitwa (DestroyClanAction zeruje MapEventSide partii w bitwie i podaje niszczyciela z drugiej strony), 1 422 ludzi
    /// jednej doby w nicosc; bieg dwuletni d130 921 ludzi (tej doby klanow 364 -> 363 - najpewniej zniknal rod, wniosek; HarrenhalEvent ROT partii nie usuwa - sprawdzone); (b) bandy po bitwie (CaptureDefeatedPartyMembers -> MakeHeroFugitiveAction
    /// -> DestroyPartyAction bez niszczyciela, sklad 1-4) i partie rodow po bitwie (sklad 0) - partia skasowana zanim okna bitwy E4/7 zdjely poleglych i jencow ze slownika:
    /// reszta slownika (35-266 ludzi) w nicosc. Swiat liczyl ja "w nicosc - przy zniknieciu partii", ale rownanie regionu (BuildViews) nie mialo licznika - reszta krainy
    /// ujemna (valyrian -0.216% w biegu dwuletnim).
    /// Dwie reguly, kazda za swoim kluczem:
    ///  1. PartyGoneMenGoHome - zywi ludzie partii usunietej poza bitwa (niszczyciel podany, ale partia bez MapEvent i bez oblezenia) ida ta sama droga co partia
    ///     rozwiazana bez bitwy (PeopleBook.ArmsPartyGone: min(slownik, sklad), weterani do karczmy, bracia Strazy na Mur, reszta do DOM pochodzenia) - ludzie zywi
    ///     zostaja w swiecie (DOM rosnie o tylu, ilu ubywa z SLUZBY; rownanie regionu bez zmian);
    ///  2. PartyGoneMenCounted - reszta slownika, ktora i tak idzie w nicosc (OriginPartyGone), dostaje licznik regionu pochodzenia "zp:" (od siewu) w rownaniu regionu
    ///     (jak POLEGLI) - ksiegowanie, nikogo nie dodaje ani nie zabiera. Udzialy "" (bez pochodzenia) - poza regionami jak dotad.
    /// Zapis: sekcja "zp:" w "arm_people" (region=ludzie od siewu; stary zapis bez sekcji - 0; starsza DLL sekcji nie zna i ja pomija; najwyzej ZpMax wpisow).
    /// Linia doby "Ludzie przy zniknieciu partii (A12)" po linii "Pochodzenie (E4/7)" - tozsamosc: przy zniknieciu partii (swiat) = w rownaniu krain + bez pochodzenia.
    /// Stan czyszczony w ResetOrigin (nowa kampania / wczytanie). Wyjatki per obiekt liczone (OStumble), bez globalnego wylacznika.
    /// </summary>
    internal static partial class PeopleBook
    {
        private const int ZpMax = 4000;     // straznik rozmiaru sekcji "zp:" (regionow ROT ok. 300)
        private static readonly Dictionary<string, double> _zp = new Dictionary<string, double>();   // region pochodzenia -> ludzie przy zniknieciu partii od siewu (rownanie regionu)
        private static double _zpDay, _zpNoneDay, _ghMen, _ghWall, _ghVet;   // doba: policzeni w rownaniu, bez pochodzenia; wrocili przy zniknieciu poza bitwa (w tym na Mur, do karczmy)
        private static int _ghParties;

        private static bool GoneHomeOn { get { var s = Settings.Current; return s != null && s.PartyGoneMenGoHome; } }
        private static bool GoneCountedOn { get { var s = Settings.Current; return s != null && s.PartyGoneMenCounted; } }

        private static void ResetGone() { _zp.Clear(); GoneNewDay(); }

        private static void GoneNewDay() { _zpDay = _zpNoneDay = _ghMen = _ghWall = _ghVet = 0; _ghParties = 0; }

        /// <summary>A12 (1): czy koniec partii z niszczycielem liczyc jak rozwiazanie bez bitwy - partia poza bitwa i oblezeniem (DestroyClanAction, koniec bitwy po oknach E4/7).</summary>
        private static bool GoneOutsideBattle(MobileParty mp, PartyBase destroyer, bool removed)
        {
            return destroyer != null && !removed && GoneHomeOn && mp != null && mp.MapEvent == null && mp.SiegeEvent == null;
        }

        /// <summary>A12 (1): licznik doby - ludzie partii usunietej poza bitwa, ktorzy wrocili (DOM pochodzenia, Mur, karczma - weterani E4/10).</summary>
        private static void NoteGoneHome(Arrive a, double vet)
        {
            _ghParties++;
            _ghMen += a.Here + a.Other + a.None + a.Wall + Math.Max(0.0, vet);
            _ghWall += a.Wall;
            _ghVet += Math.Max(0.0, vet);
        }

        /// <summary>A12 (2): reszta slownika znikajacej partii (sklad albo loch) - licznik regionu pochodzenia w rownaniu regionu. Udzialy "" - doba, poza regionami.</summary>
        private static void CountGone(Org o)
        {
            if (o == null || !GoneCountedOn) return;
            try
            {
                foreach (var kv in o.M)
                {
                    if (!(kv.Value > Eps)) continue;
                    if (kv.Key.Length == 0) { _zpNoneDay += kv.Value; continue; }
                    if (!_zp.ContainsKey(kv.Key) && _zp.Count >= ZpMax) { _zpNoneDay += kv.Value; continue; }
                    double v; _zp.TryGetValue(kv.Key, out v); _zp[kv.Key] = v + kv.Value;
                    _zpDay += kv.Value;
                }
            }
            catch (Exception e) { OStumble("CountGone(A12)", e); }
        }

        /// <summary>A12 (2): ludzie regionu przy zniknieciu partii od siewu - skladnik rownania regionu (BuildViews: + jak POLEGLI).</summary>
        private static double ZpOf(string region)
        {
            double v;
            return region != null && _zp.TryGetValue(region, out v) ? v : 0.0;
        }

        /// <summary>Zapis: sekcja "|zp:region=ludzie,..." (tylko gdy jest co zapisac).</summary>
        private static void ExportGone(StringBuilder sb)
        {
            bool first = true;
            int n = 0;
            foreach (var kv in _zp)
            {
                if (!(kv.Value > Eps) || BadKey(kv.Key)) continue;
                if (++n > ZpMax) break;
                sb.Append(first ? "|zp:" : ",").Append(kv.Key).Append('=').Append(N3(kv.Value));
                first = false;
            }
        }

        /// <summary>Odczyt sekcji "zp:" (cialo bez przedrostka); zepsuty wpis pomijany.</summary>
        private static void ImportGone(string body)
        {
            foreach (var item in body.Split(','))
            {
                int eq = item.IndexOf('=');
                if (eq <= 0 || _zp.Count >= ZpMax) continue;
                double v;
                if (!double.TryParse(item.Substring(eq + 1), NumberStyles.Float, Inv, out v) || !(v > 0.0) || double.IsInfinity(v)) continue;
                _zp[item.Substring(0, eq)] = v;
            }
        }

        /// <summary>Linia doby (po "Pochodzenie (E4/7)", przed zerowaniem licznikow doby) - przy ktoryms z kluczy; tozsamosc swiata i rownania krain.</summary>
        private static void GoneLine(int day)
        {
            bool home = GoneHomeOn, counted = GoneCountedOn;
            if (!home && !counted) return;
            double since = 0; foreach (var v in _zp.Values) since += v;
            var sb = new StringBuilder(400);
            sb.Append("Ludzie przy zniknieciu partii (A12): dzien ").Append(day)
              .Append(" | do domu po usunieciu partii poza bitwa (Party Gone Men Go Home ").Append(home ? "tak" : "nie").Append("): ").Append(M0(_ghMen)).Append(" ludzi z ")
              .Append(_ghParties).Append(" partii (w tym na Mur ").Append(M0(_ghWall)).Append(", do karczmy jako weterani ").Append(M0(_ghVet)).Append(")")
              .Append(" | w nicosc przy zniknieciu partii ").Append(M0(_gone)).Append(" = w rownaniu krain ").Append(M0(_zpDay)).Append(" + bez pochodzenia ").Append(M0(_zpNoneDay))
              .Append(" (Party Gone Men Counted ").Append(counted ? "tak" : "nie").Append("; roznica ").Append((_gone - _zpDay - _zpNoneDay).ToString("0.##", Inv)).Append(")")
              .Append(" | od siewu w rownaniu krain ").Append(M0(since)).Append(" ludzi w ").Append(_zp.Count).Append(" regionach.");
            Log.Info(sb.ToString());
        }
    }
}
