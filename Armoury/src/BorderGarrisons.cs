using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// R3-08g (POMYSLY-35 nr 26, Jeff 20.10 "tak") - ZALOGI GRANICZNE W POKOJU. Klucz Border Garrisons Peace (domyslnie tak).
    /// Zrodlo: audyt 09.10 (12-DOCHODY-RODOW W-10 i krytyka pkt 32): po 60 dobach pokoju zalogi wracaja do stanu wojennego dopiero po ok. 20 dobach
    /// wojny, a oblezenia potrzebuja obroncow od 1. dnia - "do rozwazenia po autotescie: zamki przygraniczne 75% zalogi w pokoju".
    ///
    /// Regula: cel pokojowy zalogi rodu AI z budzetem 166 (ClanBudget.Garrisons, B4) = udzial x srednia zoldu zalogi z dob wojny. Twierdza GRANICZNA
    /// (zamek albo miasto) ma udzial GarrisonPeaceShareBorder (0.75), kazda inna - GarrisonPeaceShare (0.70) jak dotad. Graniczna nigdy mniej niz
    /// glab kraju (max z obu), udzial graniczny przyciety do 0..1. W wojnie bez zmian (GarrisonWarFull - pulap gry), Straz i Wolni Ludzie bez zoldu
    /// (b.Zero) i gracz - poza budzetem, wiec poza regula.
    /// Limit rodu (recenzja): cele 0.70 wszystkich twierdz rodu przycinane do GarrisonMaxShareOfBudgetPeace (0.80) pulapu jak dotad; nadwyzka graniczna
    /// (0.05 x srednia wojenna) dostaje tylko miejsce, ktore zostaje do tego limitu - glab kraju nigdy nie traci, rod na limicie nie ma nadwyzki wcale.
    ///
    /// GRANICA: w promieniu BorderGarrisonRadius (60 jedn. mapy, w linii prostej) stoi zamek albo miasto INNEJ frakcji mapy (inne krolestwo albo rod
    /// niezalezny), ktora nie jest sojusznikiem (Kingdom.IsAllyWith - sojusze gry 1.3+). Pomiar mapy ROT (ROT-Map settlements.xml, 227 twierdz,
    /// wlasciciele z XML -> krolestwo z spclans.xml, Surusbec przez pana do rodu Swyft = Zachod): mediana odleglosci do najblizszej twierdzy 44 jedn.,
    /// u 3/4 twierdz ponizej 60 - promien 60 = "sasiad za miedza"; graniczne 71 z 227 (31%): Dorzecze 8 z 14, Zachod 12 z 22, Polnoc 2 z 22
    /// (75 jedn. dawalo 49%, 90 - 67%). 60 jedn. to ok. doby marszu armii (gra 1.4.8: predkosc bazowa 4 jedn. na 1.2 h, mniej dla armii i noca).
    /// Morze: odleglosc w linii prostej - wyspa w promieniu od obcego brzegu jest graniczna (najazd z morza, NavalDLC); Morze Waskie jest szersze.
    /// Straz Nocy to osobna frakcja - zamek Polnocy przy Murze jest graniczny wobec Strazy (sasiad innej frakcji; zalogi Strazy same bez zmian).
    /// Zmiana wlasciciela: mapa granic liczona od nowa raz na dobe (pierwsze wolanie doby) - zdobyty zamek od razu robi z sasiadow twierdze graniczne,
    /// koniec sojuszu tez. Mapa trzyma id twierdz (napisy), nie obiekty - nic nie trzyma starej kampanii po wczytaniu innej; Reset przy kazdej kampanii.
    ///
    /// Co robi wyzszy cel W POKOJU (recenzja): BK wylacza w pokoju autowerbunek zalog AI (krolestwo bez wrogow), wiec nikt zalogi nie dobiera.
    /// Wyzszy cel dziala dwiema drogami: (a) mniej zwolnien - zaloga ponad cel jest cieta tylko do celu (ClanBudget.Releases krok 1, gdy rod ponad
    /// pulapem) i limit zoldu zalogi (SetGarrisonWagePaymentLimit, tylko w dol) jest wyzszy; (b) wiekszy "idealny rozmiar", przy ktorym lord zostawia
    /// ludzi w zalodze (model gry dzieli limit zoldu przez sredni zold). Zaloga graniczna, ktora weszla w pokoj ponizej celu (swiezo zdobyty zamek),
    /// sama nie podrosnie - tak jak przy 0.70. Linia logu pokazuje zold zalog granicznych i w glebi kraju wobec ich celow.
    ///
    /// Zloto (zamknieta ekonomia): platnik - rod pana (pulap pokoju 166), odbiorca - zolnierze zalogi (zold gry). Nic nie powstaje z niczego.
    /// Watek: wolane tylko z ClanBudget.Daily (watek glowny, raz na dobe); bez wolan z watkow AI.
    /// Potkniecia liczone per twierdza (udzial jak dotad dla tej jednej), funkcja nigdy sie nie gasi.
    /// </summary>
    internal static class BorderGarrisons
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MaxNames = 12;

        // mapa granic doby: id twierdz granicznych (napisy - bez trzymania obiektow kampanii)
        private static readonly HashSet<string> _border = new HashSet<string>();
        private static readonly List<string> _ids = new List<string>();
        private static readonly List<Vec2> _pos = new List<Vec2>();
        private static readonly List<IFaction> _fac = new List<IFaction>();
        private static readonly Dictionary<string, int[]> _realm = new Dictionary<string, int[]>();   // nazwa frakcji -> [graniczne, wszystkie]
        private static WeakReference _camp;
        private static int _mapDay = int.MinValue;
        private static float _mapR = -1f;
        private static int _mapForts, _mapBorder;

        // liczniki doby (rody AI w pokoju z celem zalogi; po przycieciu do limitu rodu - jak B4)
        private static int _dDay = int.MinValue;
        private static double _dExtra, _dKept;                       // nadwyzka graniczna przed i po przycieciu (zl/d)
        private static int _dBorderN, _dBorderAt, _dInlandN, _dInlandAt;
        private static long _dBorderTgt, _dBorderWage, _dInlandTgt, _dInlandWage;
        private static readonly List<string> _dNames = new List<string>();
        private static int _dNamesMore;

        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static WeakReference _realmCamp;   // pierwsza linia sesji - z podzialem wedlug frakcji

        /// <summary>Stan kampanii od zera (ClanBudget.Reset - konstruktor ArmouryBehavior przy kazdej kampanii).</summary>
        internal static void Reset()
        {
            _border.Clear(); _ids.Clear(); _pos.Clear(); _fac.Clear(); _realm.Clear();
            _camp = null; _mapDay = int.MinValue; _mapR = -1f; _mapForts = _mapBorder = 0;
            _dDay = int.MinValue; ClearDay();
            _stumbles = 0; _err.Clear(); _realmCamp = null;
        }

        private static float BorderShare(Settings s)
        {
            return Math.Max(Math.Max(0f, s.GarrisonPeaceShare), Math.Min(1f, Math.Max(0f, s.GarrisonPeaceShareBorder)));
        }

        /// <summary>Udzial pokojowy zalogi twierdzy st (ClanBudget.Garrisons, dla kazdej twierdzy rodu w pokoju; wr - srednia zoldu zalogi z dob wojny).</summary>
        internal static float PeaceShare(Settlement st, Settings s, float wr)
        {
            float baseShare = Math.Max(0f, s.GarrisonPeaceShare);
            if (!s.BorderGarrisonsPeace || st == null || st.StringId == null) return baseShare;
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                EnsureMap(today, s);
                NoteDay(today);
                if (!_border.Contains(st.StringId)) return baseShare;
                float b = BorderShare(s);
                if (wr <= 0) return b;
                _dExtra += (b - baseShare) * (double)wr;
                if (_dNames.Count < MaxNames)
                {
                    var oc = st.OwnerClan;
                    _dNames.Add(st.Name + (oc != null ? " (" + oc.Name + ")" : ""));
                }
                else _dNamesMore++;
                return b;
            }
            catch (Exception e) { Stumble("PeaceShare", e); return baseShare; }
        }

        /// <summary>Pomiar: zaloga z celem po przycieciu do limitu rodu (ClanBudget.Garrisons, te same zalogi co "zalog z celem" w B4).
        /// kept - nadwyzka graniczna, ktora zostala po przycieciu; t - cel; cur - zold zalogi dzis.</summary>
        internal static void NoteTarget(Settlement st, Settings s, double kept, int t, float cur)
        {
            if (!s.BorderGarrisonsPeace || st == null || st.StringId == null) return;
            try
            {
                NoteDay((int)CampaignTime.Now.ToDays);
                bool at = cur >= 0.95f * t;
                if (_border.Contains(st.StringId)) { _dBorderN++; _dBorderTgt += t; _dBorderWage += (long)cur; if (at) _dBorderAt++; _dKept += kept; }
                else { _dInlandN++; _dInlandTgt += t; _dInlandWage += (long)cur; if (at) _dInlandAt++; }
            }
            catch (Exception e) { Stumble("NoteTarget", e); }
        }

        /// <summary>Dopisek do odcinka B4 linii "Budzet rodow (166)": drugi udzial, gdy regula wlaczona (pusty, gdy wylaczona - B4 jak przed zmiana).</summary>
        internal static string B4Note(Settings s)
        {
            if (s == null || !s.BorderGarrisonsPeace) return "";
            return " (graniczne " + BorderShare(s).ToString("0.00", Inv) + " - linia R3-08g)";
        }

        private static void ClearDay()
        {
            _dExtra = _dKept = 0; _dBorderN = _dBorderAt = _dInlandN = _dInlandAt = 0;
            _dBorderTgt = _dBorderWage = _dInlandTgt = _dInlandWage = 0;
            _dNames.Clear(); _dNamesMore = 0;
        }

        private static void NoteDay(int today)
        {
            if (_dDay == today) return;
            _dDay = today; ClearDay();
        }

        private static bool SameCampaign(WeakReference w)
        {
            return w != null && w.IsAlive && ReferenceEquals(w.Target, Campaign.Current);
        }

        private static bool Allied(IFaction a, IFaction b)
        {
            var ka = a as Kingdom; var kb = b as Kingdom;
            return ka != null && kb != null && ka.IsAllyWith(kb);
        }

        /// <summary>Mapa granic: raz na dobe (i po zmianie promienia albo kampanii). 227 twierdz ROT = ok. 26 tys. par, raz na dobe.</summary>
        private static void EnsureMap(int today, Settings s)
        {
            float r = Math.Max(0f, s.BorderGarrisonRadius);
            if (_mapDay == today && _mapR == r && SameCampaign(_camp)) return;
            _camp = new WeakReference(Campaign.Current); _mapDay = today; _mapR = r;
            _border.Clear(); _realm.Clear(); _ids.Clear(); _pos.Clear(); _fac.Clear();
            try
            {
                foreach (var t in Town.AllFiefs)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || st.StringId == null) continue;
                    var f = st.MapFaction;
                    if (f == null) continue;
                    _ids.Add(st.StringId); _pos.Add(st.GetPosition2D); _fac.Add(f);
                }
                float r2 = r * r;
                int n = _ids.Count;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (_fac[i] == _fac[j]) continue;
                        if (_pos[i].DistanceSquared(_pos[j]) > r2) continue;
                        if (Allied(_fac[i], _fac[j])) continue;
                        _border.Add(_ids[i]); _border.Add(_ids[j]);
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    string fn = _fac[i].Name != null ? _fac[i].Name.ToString() : "?";
                    int[] c;
                    if (!_realm.TryGetValue(fn, out c)) { c = new int[2]; _realm[fn] = c; }
                    c[1]++; if (_border.Contains(_ids[i])) c[0]++;
                }
                _mapForts = n; _mapBorder = _border.Count;
            }
            catch (Exception e) { Stumble("Map", e); }
            finally { _pos.Clear(); _fac.Clear(); _ids.Clear(); }   // bez obiektow kampanii po przeliczeniu
        }

        private static void AppendAt(StringBuilder sb, string what, int at, int n, long wage, long tgt)
        {
            sb.Append(what).Append(' ').Append(at).Append(" z ").Append(n).Append(" (zold ").Append(wage).Append(" wobec celu ").Append(tgt)
              .Append(tgt > 0 ? ", " + (100.0 * wage / tgt).ToString("0", Inv) + "%" : "").Append(')');
        }

        /// <summary>Linia "Zalogi graniczne (R3-08g)" - po linii "Budzet rodow (166)" (ClanBudget.Daily); pomiar, ciche logi (LogGate) jak inne pomiary.</summary>
        internal static void Report(Settings s, int today)
        {
            try
            {
                if (s == null || !s.LogEnabled || !LogGate.FullDay(today)) return;
                if (!s.BorderGarrisonsPeace)
                {
                    Log.Info("Zalogi graniczne (R3-08g): dzien " + today + " | wylaczone (Border Garrisons Peace) - wszystkie zalogi w pokoju na udziale "
                             + Math.Max(0f, s.GarrisonPeaceShare).ToString("0.00", Inv));
                    return;
                }
                EnsureMap(today, s);
                NoteDay(today);
                float baseShare = Math.Max(0f, s.GarrisonPeaceShare);
                float b = BorderShare(s);
                var sb = new StringBuilder(640);
                sb.Append("Zalogi graniczne (R3-08g): dzien ").Append(today)
                  .Append(" | twierdze graniczne na mapie ").Append(_mapBorder).Append(" z ").Append(_mapForts)
                  .Append(" (zamek albo miasto innej frakcji, nie sojusznika, w promieniu ").Append(_mapR.ToString("0", Inv)).Append(" jedn.)")
                  .Append(" | rody AI w pokoju: zalog granicznych z celem ").Append(_dBorderN).Append(" (udzial ").Append(b.ToString("0.00", Inv))
                  .Append("), w glebi kraju ").Append(_dInlandN).Append(" (udzial ").Append(baseShare.ToString("0.00", Inv)).Append(")")
                  .Append("; cel granicznych wyzej o ").Append(_dExtra.ToString("0", Inv)).Append(" zl/d, po przycieciu ").Append(_dKept.ToString("0", Inv))
                  .Append(" zl/d (nadwyzka tylko z miejsca do ").Append(s.GarrisonMaxShareOfBudgetPeace.ToString("0.00", Inv)).Append(" pulapu rodu - glab kraju bez zmian)")
                  .Append(" | na celu (zold >= 95% celu): ");
                AppendAt(sb, "graniczne", _dBorderAt, _dBorderN, _dBorderWage, _dBorderTgt);
                AppendAt(sb, ", w glebi kraju", _dInlandAt, _dInlandN, _dInlandWage, _dInlandTgt);
                sb.Append(" - w pokoju zalog nikt nie dobiera (BK), cel to mniej zwolnien");
                if (_dNames.Count > 0)
                {
                    sb.Append(" | graniczne w pokoju: ");
                    for (int i = 0; i < _dNames.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(_dNames[i]); }
                    if (_dNamesMore > 0) sb.Append(" i ").Append(_dNamesMore).Append(" innych");
                }
                if (!SameCampaign(_realmCamp) || today % 28 == 0)
                {
                    _realmCamp = new WeakReference(Campaign.Current);
                    sb.Append(" | wedlug frakcji (graniczne/wszystkie): ");
                    bool first = true;
                    foreach (var kv in _realm)
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        sb.Append(kv.Key).Append(' ').Append(kv.Value[0]).Append('/').Append(kv.Value[1]);
                    }
                }
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles);
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Report", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("BorderGarrisons." + where, e); } catch { }
        }
    }
}
