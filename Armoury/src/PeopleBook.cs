using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// KSIEGA LUDZI (projekt etapu 4, R0, U23) - jedyne miejsce stanu ksiegi (klucz zapisu "arm_people") i jedyna droga przesuniecia.
    /// Powstaje w paczce E4/2 (190) z jednym kontem: CIALA - pula Innych wedlug regionu (warownia regionu OutlawLaw, ta sama geografia co
    /// pula wyrzutkow). Konto nie-ludzi: zasilane tylko cialami poleglych w miejscach R13 (za Murem, przy Murze) - nadwyzka ponad miejsce
    /// w bandach z nekromancji, z odbitych jencow i z jencow, ktorych ROT kasuje - oraz bilansem otwarcia nowej kampanii (616 "dawnych umarlych",
    /// Jeff 07.10, STAN-PRAC pyt. 2; RK 4.2 B pkt 8). Wydawane tylko na narodziny bandy Innych (szablon 53 + wodz -> z puli; pusta pula = sam wodz).
    /// Nigdy nie wraca do ludzi (wight nie jest czlowiekiem z niczego).
    /// Do tego licznik POLEGLI (R3) z rak Innych wedlug miejsca - w E4/2 tylko log (konto DOM regionu, z ktorego mozna by ich zdjac, przychodzi
    /// w E4/3; trwaly licznik POLEGLI w zapisie - w E4/7). Kolejne konta (DOM, WYRZUTKI, SLUZBA, JENCY...) - kazde w swojej paczce, kazde przez te klase.
    /// Zapis tylko przez SaveText.Sync (napis w kawalkach). Stary zapis bez klucza: pula od 0 (bez bilansu otwarcia - bandy w polu zostaja).
    /// </summary>
    internal static class PeopleBook
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // przyczyny ruchu na koncie CIALA (indeksy licznikow doby)
        internal const int COpening = 0, CNecro = 1, CRescued = 2, CMilitia = 3, CBirth = 4, CKinds = 5;
        private static readonly string[] CName = { "bilans otwarcia", "nadwyzka nekromancji (polegli i jency)", "odbici jency", "milicja przejetej osady", "narodziny band" };

        // przyczyny smierci (R3) z rak Innych
        internal const int DCaptives = 0, DRescued = 1, DMilitia = 2, DKinds = 3;
        private static readonly string[] DName = { "jency Innych", "odbici przez Innych", "milicja osady przejetej przez Innych" };
        private static readonly string[] PlaceName = { "za Murem", "przy Murze", "Westeros", "Essos", "miejsce nieznane" };

        /// <summary>Bilans otwarcia puli cial nowej kampanii (Jeff 07.10: 616 dawnych umarlych na start - dzisiejsze 4 bandy x 154).</summary>
        internal const int OpeningBodies = 616;
        private const float OpeningDays = 2f;   // ten sam zegar i ta sama granica co bilans otwarcia CrashScribe (OthersStartDowryDays, RK 4.2 A pkt 1)

        // ---------------------------------------------------------------- stan w zapisie
        private static readonly Dictionary<string, long> _bodies = new Dictionary<string, long>();   // id warowni regionu -> ciala ("" = region nieznany)
        private static bool _seeded;          // bilans otwarcia zalozony (albo stary zapis: od 0)
        private static long _opening;         // ile dal bilans otwarcia (616 albo 0)

        // ---------------------------------------------------------------- liczniki doby (bez zapisu)
        private static readonly long[] _in = new long[CKinds], _out = new long[CKinds];
        private static readonly long[,] _dead = new long[DKinds, 5];
        private static long _deadSess;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static string _openingNote;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("PeopleBook." + where, e); } catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior: stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset()
        {
            _bodies.Clear(); _seeded = false; _opening = 0;
            Array.Clear(_in, 0, CKinds); Array.Clear(_out, 0, CKinds); Array.Clear(_dead, 0, _dead.Length);
            _deadSess = 0; _stumbles = _stumblesDay = 0; _errSites.Clear(); _openingNote = null;
        }

        // ---------------------------------------------------------------- zapis ("v1|s:<zalozony 0/1>;<bilans otwarcia>|c:<id>=<ciala>,...")
        internal static string Export()
        {
            var sb = new StringBuilder(64 + _bodies.Count * 24);
            sb.Append("v1|s:").Append(_seeded ? 1 : 0).Append(';').Append(_opening.ToString(Inv)).Append("|c:");
            bool first = true;
            foreach (var kv in _bodies)
            {
                if (kv.Value <= 0) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _bodies.Clear(); _seeded = false; _opening = 0;
            if (string.IsNullOrEmpty(data)) return;   // stary zapis - bilans otwarcia przy pierwszym uzyciu (po dwoch dobach kampanii: od 0)
            try
            {
                foreach (var part in data.Split('|'))
                {
                    if (part.StartsWith("s:", StringComparison.Ordinal))
                    {
                        var a = part.Substring(2).Split(';');
                        _seeded = a.Length > 0 && a[0] == "1";
                        long o; if (a.Length > 1 && long.TryParse(a[1], NumberStyles.Integer, Inv, out o)) _opening = o;
                    }
                    else if (part.StartsWith("c:", StringComparison.Ordinal))
                    {
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.LastIndexOf('=');
                            if (eq < 0) continue;
                            long n; if (!long.TryParse(item.Substring(eq + 1), NumberStyles.Integer, Inv, out n) || n <= 0) continue;
                            _bodies[item.Substring(0, eq)] = n;
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ---------------------------------------------------------------- konto CIALA
        internal static long Bodies { get { long s = 0; foreach (var v in _bodies.Values) s += v; return s; } }

        private static string Key(Settlement region) { return region != null && region.StringId != null ? region.StringId : ""; }

        /// <summary>Bilans otwarcia - leniwie przy pierwszym uzyciu (pierwsze bandy rodza sie przed pierwszym tickiem dobowym): w nowej kampanii
        /// (mniej niz 2 doby od startu) 616 cial w regionie siedziby rodu Innych; w starszej kampanii (stary zapis bez klucza) - od 0.</summary>
        internal static void EnsureOpening()
        {
            if (_seeded) return;
            try
            {
                if (Campaign.Current == null) return;
                _seeded = true;
                float elapsed = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
                if (elapsed < OpeningDays)
                {
                    Settlement region = null;
                    try
                    {
                        var clan = Clan.FindFirst(c => c != null && c.StringId == "ROTclan_126");
                        var home = clan != null ? clan.HomeSettlement : null;
                        if (home != null) region = OutlawLaw.RegionFor(home);
                    }
                    catch (Exception e) { region = null; Stumble("EnsureOpening(region)", e); }   // bez regionu: pula w "regionie nieznanym" - bilans otwarcia i tak jest
                    _bodies[Key(region)] = (_bodies.TryGetValue(Key(region), out var v) ? v : 0) + OpeningBodies;
                    _opening = OpeningBodies; _in[COpening] += OpeningBodies;
                    _openingNote = "bilans otwarcia " + OpeningBodies + " cial w regionie " + (region != null ? region.Name.ToString() : "nieznanym") + " (nowa kampania, doba " + elapsed.ToString("0.0", Inv) + ")";
                }
                else
                {
                    _opening = 0;
                    _openingNote = "stary zapis bez ksiegi (doba kampanii " + elapsed.ToString("0", Inv) + ") - pula od 0, bandy w polu zostaja";
                }
                Log.Info("Ludzie (ksiega): konto CIALA zalozone - " + _openingNote + ".");
            }
            catch (Exception e) { Stumble("EnsureOpening", e); }
        }

        /// <summary>Doplyw do puli cial w regionie (tylko z miejsc R13 - wola OthersRise).</summary>
        internal static void ToBodies(Settlement region, long n, int cause)
        {
            if (n <= 0) return;
            try
            {
                EnsureOpening();
                string k = Key(region);
                long v; _bodies.TryGetValue(k, out v); _bodies[k] = v + n;
                if (cause >= 0 && cause < CKinds) _in[cause] += n;
            }
            catch (Exception e) { Stumble("ToBodies", e); }
        }

        /// <summary>Wydatek z puli cial: do want cial, od regionow najblizszych miejscu pos (region nieznany - na koncu). Zwraca, ile wzieto.</summary>
        internal static long FromBodies(Vec2 pos, long want, int cause)
        {
            if (want <= 0) return 0;
            long got = 0;
            try
            {
                EnsureOpening();
                if (_bodies.Count == 0) return 0;
                var keys = _bodies.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                var dist = new Dictionary<string, float>(keys.Count);
                foreach (var k in keys)
                {
                    float d = float.MaxValue;
                    if (k.Length > 0) { try { var s = Settlement.Find(k); if (s != null) d = pos.DistanceSquared(s.GetPosition2D); } catch { } }
                    dist[k] = d;
                }
                keys.Sort((a, b) => dist[a].CompareTo(dist[b]));
                foreach (var k in keys)
                {
                    if (got >= want) break;
                    long have = _bodies[k];
                    long take = Math.Min(have, want - got);
                    if (take <= 0) continue;
                    if (have - take > 0) _bodies[k] = have - take; else _bodies.Remove(k);
                    got += take;
                }
                if (cause >= 0 && cause < CKinds) _out[cause] += got;
            }
            catch (Exception e) { Stumble("FromBodies", e); }
            return got;
        }

        /// <summary>R3: polegly z rak Innych (jeniec skasowany, odbity jeniec, milicja przejetej osady) - region miejsca do E4/7, potem pochodzenie.
        /// W E4/2 tylko licznik w logu (bez konta DOM nie ma z czego zdjac).</summary>
        internal static void Die(Settlement region, long n, int cause, int place)
        {
            if (n <= 0) return;
            try
            {
                int p = place >= 0 && place < 4 ? place : 4;
                if (cause >= 0 && cause < DKinds) _dead[cause, p] += n;
                _deadSess += n;
            }
            catch (Exception e) { Stumble("Die", e); }
        }

        // ---------------------------------------------------------------- linia doby
        /// <summary>Raz na dobe (po linii "Inni z poleglych (E4/2)"): stan konta CIALA z rownaniem i polegli z rak Innych.</summary>
        internal static void Daily(int day, bool log)
        {
            try
            {
                EnsureOpening();
                if (log)
                {
                    long now = Bodies, inn = 0, outn = 0;
                    for (int i = 0; i < CKinds; i++) { inn += _in[i]; outn += _out[i]; }
                    var sb = new StringBuilder(600);
                    sb.Append("Ludzie (ksiega): dzien ").Append(day).Append(" | konto CIALA (pula Innych, nie-ludzie; E4/2): ").Append(now)
                      .Append(" w ").Append(_bodies.Count(kv => kv.Value > 0)).Append(" regionach = wczoraj ").Append(now - inn + outn)
                      .Append(" + doplyw ").Append(inn).Append(" (");
                    bool f = true;
                    for (int i = 0; i < CKinds; i++) { if (i == CBirth) continue; sb.Append(f ? "" : ", ").Append(CName[i]).Append(' ').Append(_in[i]); f = false; }
                    sb.Append(") - narodziny band ").Append(_out[CBirth]);
                    sb.Append(" | ").Append(_openingNote ?? ("bilans otwarcia w zapisie " + _opening));
                    long dd = 0; for (int c = 0; c < DKinds; c++) for (int p = 0; p < 5; p++) dd += _dead[c, p];
                    sb.Append(" | polegli z rak Innych dzis (R3, region miejsca do E4/7; tylko licznik do E4/3) ").Append(dd);
                    if (dd > 0)
                    {
                        sb.Append(" (");
                        bool g = true;
                        for (int c = 0; c < DKinds; c++)
                        {
                            long s = 0; for (int p = 0; p < 5; p++) s += _dead[c, p];
                            if (s <= 0) continue;
                            sb.Append(g ? "" : "; ").Append(DName[c]).Append(' ').Append(s).Append(':');
                            g = false;
                            for (int p = 0; p < 5; p++) if (_dead[c, p] > 0) sb.Append(' ').Append(PlaceName[p]).Append(' ').Append(_dead[c, p]);
                        }
                        sb.Append(')');
                    }
                    sb.Append(", od wczytania ").Append(_deadSess);
                    if (_stumblesDay > 0) sb.Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(", pierwszy blad kazdego miejsca w logu)");
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            Array.Clear(_in, 0, CKinds); Array.Clear(_out, 0, CKinds); Array.Clear(_dead, 0, _dead.Length);
            _stumblesDay = 0; _openingNote = null;
        }
    }
}
