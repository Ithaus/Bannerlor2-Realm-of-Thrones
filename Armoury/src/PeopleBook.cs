using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
    /// Do tego licznik POLEGLI (R3) z rak Innych wedlug miejsca - tylko log do E4/7 (smierc zolnierza zdejmuje z DOM dopiero razem z werbunkiem,
    /// ktory zdejmuje rekruta z DOM - R3/R4 w E4/7; trwaly licznik POLEGLI w zapisie - w E4/7).
    /// E4/3 (108n): konta DOM (wsie: hearth x k_v - stan w hearth gry; miasta: stan od E4/4), WYRZUTKI (pula OutlawLaw - stan w "arm_outlaws"),
    /// TABOR (tabory wsi i lodzie - zdjete z DOM przez gre) i SLUZBA (partie, zalogi, milicje, bandy - konto od E4/7) oraz jedyna droga zmiany
    /// hearth za ludzi: Move (PeopleUnit.Shift wola tylko Move). k_v na osade z dnia kalibracji ("v:" w "arm_people", U3) - zmiana kultury osady
    /// nie zmienia ani liczby ludzi, ani wagi czlowieka. Trzy punkty powrotu do DOM (U2): LosersFlee.SendHome, ClanBudget.ToVillage, OutlawLaw.ReturnHome.
    /// Kolejne konta (SLUZBA z pochodzeniem, JENCY, UCHODZCY...) - kazde w swojej paczce, kazde przez te klase.
    /// Zapis tylko przez SaveText.Sync (napis w kawalkach). Stary zapis bez klucza: pula od 0 (bez bilansu otwarcia - bandy w polu zostaja);
    /// zapis bez "v:" - k_v z kultury osady w dniu pierwszego wczytania tej wersji (bez skoku).
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

        // ---------------------------------------------------------------- E4/3 (108n): konta ludzi i jedna droga ruchu hearth
        // konta: DOM (wsie: hearth x k_v), WYRZUTKI (pula OutlawLaw), TABOR (tabory wsi i lodzie), SLUZBA (wojsko - konto od E4/7), poza ksiega (zadania gracza)
        internal const int ADom = 0, AOutlaws = 1, ACarts = 2, AService = 3, AOutside = 4;
        // przyczyny ruchu ludzi (indeksy licznikow)
        internal const int MSeed = 0, MMisery = 1, MRaidFlee = 2, MPoolHome = 3, MCart = 4, MH3Cart = 5, MDisband = 6, MCommon = 7, MSoldier = 8,
                           M166 = 9, MW4c = 10, M179 = 11, MPress = 12, MQuest = 13, MIncident = 14, MKinds = 15;
        private static readonly string[] MName = { "siew puli wyrzutkow (bilans otwarcia)", "bieda i wojna do lasu", "spalona wies do lasu", "z puli do domu (OutlawLaw.ReturnHome)",
            "tabory wsi i lodzie (gra, okna E4/0)", "rozbite tabory do swojej wsi (LosersFlee.SendHome)", "rozwiazane tabory i lodzie do swojej wsi (R18)",
            "prosci i bandyci do wsi (LosersFlee.SendHome -> OutlawLaw.ReturnHome)", "zolnierze do ludnosci BK (LosersFlee.SendHome: rozbici, uwolnieni, I1, pula)",
            "zwolnieni 166 (ClanBudget.ToVillage)", "przepelnienie W4c (ClanBudget.ToVillage)", "rycerze 179 (ClanBudget.ToVillage)",
            "pobor wymuszony gracza", "zadania gracza (hearth gry = 2 ludzi)", "incydenty gracza (zostaja w hearth gry)" };
        // czy przyczyna rusza hearth wsi. SLUZBA -> DOM (zolnierze do BK, 166, W4c, 179) tylko liczona: werbunek nie zdejmuje jeszcze czlowieka z DOM
        // (R4 w E4/7), wiec powrot dopisany do hearth bylby ludzmi z niczego (i zaklocalby prog przyrostu E4/4); incydenty - hearth zmienila gra (licznik)
        private static readonly bool[] MHearth = { true, true, true, true, true, true, true, true, false, false, false, false, true, true, false };
        private static readonly object Gate = new object();
        private static readonly double[] _mMen = new double[MKinds], _mHearth = new double[MKinds];   // doba linii "Ludzie (ksiega)"
        private static readonly double[] _h40 = new double[MKinds];                                     // od ostatniej linii "Hearth (E4/0)" (Measure40 czyta i zeruje)
        private static long _disbandMen40;                                                              // R18 od ostatniej linii "Partie (E4/0)" (kontrola: wrocili = sklad)
        private static int _disbandNoVillage;
        // k na osade (U3): id osady -> ludzi na punkt hearth (wies) albo dobrobytu (miasto) z dnia kalibracji i kultura z tego dnia - stan w zapisie ("v:")
        private static readonly Dictionary<string, float> _kv = new Dictionary<string, float>();
        private static readonly Dictionary<string, string> _cv = new Dictionary<string, string>();
        private static bool _kvSet;
        private static string _kvNote;

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
            lock (Gate) { Array.Clear(_mMen, 0, MKinds); Array.Clear(_mHearth, 0, MKinds); Array.Clear(_h40, 0, MKinds); _disbandMen40 = 0; _disbandNoVillage = 0; }
            _kv.Clear(); _cv.Clear(); _kvSet = false; _kvNote = null;
        }

        // ---------------------------------------------------------------- zapis ("v1|s:<zalozony 0/1>;<bilans otwarcia>|c:<id>=<ciala>,...|v:<id osady>=<k>;<kultura>,...")
        internal static string Export()
        {
            var sb = new StringBuilder(64 + _bodies.Count * 24 + (_kvSet ? _kv.Count * 40 : 0));
            sb.Append("v1|s:").Append(_seeded ? 1 : 0).Append(';').Append(_opening.ToString(Inv)).Append("|c:");
            bool first = true;
            foreach (var kv in _bodies)
            {
                if (kv.Value <= 0) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
            }
            // E4/3 (U3): k na osade z dnia kalibracji - tylko gdy zalozone (bez sekcji = stary zapis, k_v z pierwszego wczytania)
            if (_kvSet)
            {
                sb.Append("|v:");
                first = true;
                foreach (var kv in _kv)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    string c; _cv.TryGetValue(kv.Key, out c);
                    sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("R", Inv)).Append(';').Append(c ?? "");
                }
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _bodies.Clear(); _seeded = false; _opening = 0;
            _kv.Clear(); _cv.Clear(); _kvSet = false; _kvNote = null;
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
                    else if (part.StartsWith("v:", StringComparison.Ordinal))
                    {
                        _kvSet = true;
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.IndexOf('=');
                            if (eq <= 0) continue;
                            string rest = item.Substring(eq + 1);
                            int sc = rest.IndexOf(';');
                            float k;
                            if (!float.TryParse(sc >= 0 ? rest.Substring(0, sc) : rest, NumberStyles.Float, Inv, out k) || !(k > 0f) || float.IsInfinity(k)) continue;
                            string id = item.Substring(0, eq);
                            _kv[id] = k;
                            if (sc >= 0 && sc + 1 < rest.Length) _cv[id] = rest.Substring(sc + 1);
                        }
                        _kvNote = "z zapisu: " + _kv.Count + " osad";
                    }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ---------------------------------------------------------------- E4/3: k na osade (U3)
        /// <summary>PopulationLaw.Calibrate (nowa kampania): k_v kazdej wsi i miasta z tabeli = k jej kultury z dnia kalibracji. Zapis z "v:" - bez zmian.</summary>
        internal static void SetUnitsFromCalibration()
        {
            if (_kvSet) return;
            FillUnits("kalibracja nowej kampanii");
        }

        private static void FillUnits(string how)
        {
            _kv.Clear(); _cv.Clear();
            int villages = 0, towns = 0;
            foreach (var s in Settlement.All)
            {
                try
                {
                    if (s == null || s.Culture == null || !(s.IsVillage || s.IsTown)) continue;
                    float k = PopulationLaw.KOf(s.Culture.StringId, !s.IsVillage);
                    if (!(k > 0f) || float.IsInfinity(k)) continue;
                    _kv[s.StringId] = k; _cv[s.StringId] = s.Culture.StringId;
                    if (s.IsVillage) villages++; else towns++;
                }
                catch (Exception e) { Stumble("FillUnits(osada)", e); }
            }
            _kvSet = true;
            _kvNote = how + ": wsie " + villages + ", miasta " + towns;
            Log.Info("Ludzie (ksiega): k na osade zalozone (" + how + ") - wsie " + villages + " (ludzi na punkt hearth), miasta " + towns
                     + " (ludzi na punkt dobrobytu); odtad zmiana kultury osady nie zmienia ani liczby ludzi, ani wagi czlowieka w hearth (E4/3, U3).");
        }

        /// <summary>k_v osady (ludzi na punkt hearth wsi / dobrobytu miasta) z dnia kalibracji. Stary zapis bez "v:" - zakladane leniwie z kultury osady
        /// w dniu pierwszego wczytania tej wersji (tylko po kalibracji - sami jej nie wolamy). false = brak (PopulationLaw liczy wtedy po kulturze, jak dotad).</summary>
        internal static bool TryUnit(Settlement s, out float k)
        {
            k = 0f;
            if (s == null) return false;
            if (!_kvSet)
            {
                if (!PopulationLaw.Calibrated) return false;
                try { FillUnits("stary zapis bez k na osade - k kultury osady z dnia wczytania"); }
                catch (Exception e) { _kvSet = true; Stumble("TryUnit", e); }       // nieudane zalozenie - PopulationLaw liczy po kulturze (jak dotad), bez prob co wywolanie
            }
            return s.StringId != null && _kv.TryGetValue(s.StringId, out k) && k > 0f;
        }

        /// <summary>Kultura osady z dnia kalibracji (regula geografii i demografii - E4/1 zima, E4/7 prog wolnych rak); brak - biezaca.</summary>
        internal static string CultureAtCalibration(Settlement s)
        {
            if (s == null) return "?";
            string c;
            if (s.StringId != null && _cv.TryGetValue(s.StringId, out c) && !string.IsNullOrEmpty(c)) return c;
            return s.Culture != null ? s.Culture.StringId : "?";
        }

        // ---------------------------------------------------------------- E4/3: jedna droga ruchu hearth za ludzi
        /// <summary>
        /// JEDYNA droga zmiany hearth wsi za ludzi (R0, U23): men ludzi z konta `from` na konto `to` z przyczyny `cause`. Gdy przyczyna rusza hearth
        /// (MHearth), wies traci albo dostaje men x stawka: 1/k_v tej osady (PeopleUnit.Shift - reszta ponizej kroku floata czeka w pamieci wsi), a przy
        /// wylaczonej jednostce albo braku przelicznika - stawka dotychczasowa `legacy`, ta sama arytmetyka co stary kod. Wolajacy pilnuje swoich podlog
        /// (wies nie ponizej 50 hearth itd.); Move nie schodzi ponizej 0. Zwraca hearth faktycznie przesuniety (+ gdy DOM zyskuje). Nie rzuca (U24).
        /// </summary>
        internal static double Move(Village v, int from, int to, double men, int cause, float legacy)
        {
            if (v == null || !(men > 0.0) || double.IsInfinity(men) || cause < 0 || cause >= MKinds) return 0.0;
            double moved = 0.0;
            try
            {
                int sign = to == ADom ? 1 : (from == ADom ? -1 : 0);
                if (MHearth[cause] && sign != 0)
                {
                    bool unit;
                    float per = PeopleUnit.PerMan(v, legacy, out unit);
                    if (unit) moved = PeopleUnit.Shift(v, sign * men * per);
                    else
                    {
                        float before = v.Hearth;
                        if (sign < 0) v.Hearth -= (float)men * per; else v.Hearth += (float)men * per;   // dotychczasowa arytmetyka (float)
                        moved = (double)v.Hearth - before;
                    }
                }
            }
            catch (Exception e) { Stumble("Move", e); }
            Count(men, cause, moved);
            return moved;
        }

        /// <summary>Ruch ludzi bez zmiany hearth przez nas: SLUZBA -> DOM (zolnierze do ludnosci BK, 166, W4c, 179 - konto SLUZBA od E4/7) albo hearth
        /// zmieniony przez gre (tabory i pobor przy braku przelicznika, incydenty gracza). hearth - zmiana zrobiona przez gre (0 = zadna).</summary>
        internal static void Note(double men, int cause, double hearth)
        {
            if (cause < 0 || cause >= MKinds || double.IsNaN(men) || double.IsNaN(hearth)) return;
            Count(Math.Max(0.0, men), cause, hearth);
        }

        private static void Count(double men, int cause, double hearth)
        {
            try { lock (Gate) { _mMen[cause] += men; _mHearth[cause] += hearth; _h40[cause] += hearth; } }
            catch (Exception e) { Stumble("Count", e); }
        }

        /// <summary>Measure40 (linia "Hearth (E4/0)"): hearth przesuniety przez ksiege wedlug przyczyny od ostatniego odczytu (kopiuje i zeruje).</summary>
        internal static void TakeHearth40(double[] dst)
        {
            try { lock (Gate) { for (int i = 0; i < MKinds && i < dst.Length; i++) dst[i] = _h40[i]; Array.Clear(_h40, 0, MKinds); } }
            catch (Exception e) { Stumble("TakeHearth40", e); }
        }

        /// <summary>PeopleLedger (linia "Partie (E4/0)"): ludzie rozwiazanych taborow i lodzi, ktorzy wrocili do swojej wsi od ostatniej linii.</summary>
        internal static long Disband40 { get { lock (Gate) return _disbandMen40; } }
        internal static void ZeroDisband40() { lock (Gate) _disbandMen40 = 0; }

        /// <summary>
        /// R18 (U15e): tabor wsi albo lodz rybacka rozwiazana przez gre bez bitwy (MobilePartyDestroyed bez niszczyciela - VillagerCampaignBehavior: 0 zdrowych,
        /// sierota; NavalDLC: lodz ponad limit) - caly sklad (zdrowi i ranni) wraca do DOM swojej wsi (+n/k_v). Zdarzenie gry biegnie PRZED RemoveParty
        /// (DestroyPartyAction.ApplyInternal), wiec sklad jeszcze jest. Dotad ci ludzie znikali.
        /// </summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null || destroyer != null || !mp.IsVillager) return;
                var s = Settings.Current;
                if (s == null || !s.DisbandedVillagersGoHome) return;
                var r = mp.MemberRoster;
                int men = r != null ? r.TotalRegulars : 0;
                if (men <= 0) return;
                var home = mp.HomeSettlement;
                var v = home != null ? home.Village : null;
                if (v == null) { lock (Gate) _disbandNoVillage += men; return; }
                Move(v, ACarts, ADom, men, MDisband, PeopleUnit.GamePerMan);
                lock (Gate) _disbandMen40 += men;
            }
            catch (Exception e) { Stumble("OnPartyDestroyed(R18)", e); }
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
                    sb.Append(" | polegli z rak Innych dzis (R3, region miejsca do E4/7; tylko licznik do E4/7 - smierc zdejmuje z DOM razem z werbunkiem) ").Append(dd);
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
                    try { sb.Append(UnitSegment()); } catch (Exception e) { Stumble("Daily(E4/3)", e); }
                    try { sb.Append(PeopleUnit.DayNote()); } catch (Exception e) { Stumble("Daily(PeopleUnit)", e); }
                    if (_stumblesDay > 0) sb.Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(", pierwszy blad kazdego miejsca w logu)");
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            Array.Clear(_in, 0, CKinds); Array.Clear(_out, 0, CKinds); Array.Clear(_dead, 0, _dead.Length);
            _stumblesDay = 0; _openingNote = null;
            lock (Gate) { Array.Clear(_mMen, 0, MKinds); Array.Clear(_mHearth, 0, MKinds); _disbandNoVillage = 0; }
            try { PeopleUnit.NewDay(); } catch { }
        }

        private static string M0(double v) { return v.ToString("0.#", Inv); }
        private static string H3d(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.###", Inv); }

        /// <summary>E4/3: odcinek linii "Ludzie (ksiega)" - konta DOM i WYRZUTKI, ruch DOM wedlug przyczyny (ludzie / hearth), trzy punkty powrotu, k na osade.</summary>
        private static string UnitSegment()
        {
            var s = Settings.Current;
            double vil = 0, vilH = 0, town = 0; int noK = 0, cultMoved = 0;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsVillage || st.IsTown)) continue;
                    float k = PopulationLaw.KFor(st);
                    if (!(k > 0f)) { noK++; continue; }
                    if (st.IsVillage && st.Village != null) { float h = Math.Max(0f, st.Village.Hearth); vilH += h; vil += (double)h * k; }
                    else if (st.IsTown && st.Town != null) town += (double)Math.Max(0f, st.Town.Prosperity) * k;
                    string c; if (st.StringId != null && _cv.TryGetValue(st.StringId, out c) && st.Culture != null && c != st.Culture.StringId) cultMoved++;
                }
                catch (Exception e) { Stumble("UnitSegment(osada)", e); }
            }
            double[] men = new double[MKinds], hh = new double[MKinds]; int noVil;
            lock (Gate) { Array.Copy(_mMen, men, MKinds); Array.Copy(_mHearth, hh, MKinds); noVil = _disbandNoVillage; }
            var sb = new StringBuilder(1600);
            sb.Append(" | E4/3 jednostka ludzi (1 czlowiek = 1/k osady): ").Append(PeopleUnit.On ? "CZYNNA" : "wylaczona - stawki dotychczasowe")
              .Append(", wyrzutki liczone od ludnosci: ").Append(PeopleUnit.ByPeople ? "TAK" : "NIE")
              .Append(", rozwiazane tabory do wsi (R18): ").Append(s != null && s.DisbandedVillagersGoHome ? "TAK" : "NIE")
              .Append(" | konto DOM: wsie ").Append(vil.ToString("0", Inv)).Append(" ludzi (hearth ").Append(vilH.ToString("0.0", Inv)).Append(" x k osady), miasta ")
              .Append(town.ToString("0", Inv)).Append(" (dobrobyt x k osady; stan miast od E4/4)")
              .Append(" | konto WYRZUTKI (pula OutlawLaw, linia \"Pula wyrzutkow (E4/3)\") ").Append(OutlawLaw.PoolTotal().ToString("0", Inv))
              .Append(" | ruch dzis (ludzie / hearth DOM):");
            bool any = false;
            for (int i = 0; i < MKinds; i++)
            {
                if (men[i] == 0 && hh[i] == 0) continue;
                sb.Append(any ? "; " : " ").Append(MName[i]).Append(' ').Append(M0(men[i])).Append(" / ").Append(MHearth[i] ? H3d(hh[i]) : (i == MIncident ? "gra " + H3d(hh[i]) : "bez hearth do E4/7"));
                any = true;
            }
            if (!any) sb.Append(" brak");
            // recenzja E4/3: prosci i bandyci z LosersFlee (MCommon) przechodza przez OutlawLaw.ReturnHome, ale licza sie raz - pod punktem (1);
            // punkt (3) pokazuje tylko powrot z puli, a "razem" to suma bez podwojenia
            double back = men[MH3Cart] + men[MCommon] + men[MSoldier] + men[M166] + men[MW4c] + men[M179] + men[MPoolHome];
            sb.Append(" | trzy punkty powrotu do DOM, razem ").Append(M0(back)).Append(" (bez podwojenia): LosersFlee.SendHome ").Append(M0(men[MH3Cart] + men[MCommon] + men[MSoldier]))
              .Append(" (rozbite tabory ").Append(M0(men[MH3Cart])).Append(", prosci i bandyci ").Append(M0(men[MCommon])).Append(" - do wsi dalej przez OutlawLaw.ReturnHome, zolnierze do ludnosci BK ").Append(M0(men[MSoldier]))
              .Append("), ClanBudget.ToVillage ").Append(M0(men[M166] + men[MW4c] + men[M179])).Append(" (166 ").Append(M0(men[M166])).Append(", W4c ").Append(M0(men[MW4c]))
              .Append(", 179 ").Append(M0(men[M179])).Append("; SLUZBA -> DOM tylko liczona do E4/7 - werbunek jeszcze nie zdejmuje z DOM), OutlawLaw.ReturnHome ")
              .Append(M0(men[MPoolHome])).Append(" (tylko z puli; prosci i bandyci z LosersFlee policzeni w punkcie LosersFlee.SendHome)");
            if (noVil > 0) sb.Append(" | rozwiazane tabory bez wsi domowej (znikneli) ").Append(noVil);
            sb.Append(" | k na osade: ").Append(_kvSet ? (_kvNote ?? (_kv.Count + " osad")) : "jeszcze nie zalozone (przed kalibracja)")
              .Append(", osad z kultura inna niz w dniu kalibracji ").Append(cultMoved).Append(" (ludzie bez zmian - U3), osad bez przelicznika ").Append(noK);
            return sb.ToString();
        }
    }
}
