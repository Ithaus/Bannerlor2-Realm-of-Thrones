using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E7/K (226, PLAN 7.7, projekt etapu 7 v2 rozdz. 3 "E7/K", zasada R8) - TURNIEJE, SOJUSZE I ROZEJM W ROKU 364.
    /// R8: stala gry, ktora w kalendarzu 84 oznacza czas KALENDARZA (rok, pol roku, pora), liczona x rok / 84 - rok = CampaignTime.DaysInYear, czyli
    /// kalendarz, ktory gra naprawde liczy (Armoury 364; bez dlugiego roku 84 = gra). Czasy TRWANIA zjawisk zostaja dniami (150 dni niecheci, trybut
    /// 100 dni, 15 dni miedzy turniejami miasta, koniec turnieju po 10 dniach - bez zmian).
    ///  1. TournamentsAllSeason - prefiks DefaultTournamentModel.GetTournamentStartChance: ta sama formula co gra (oblezenie - 0; 0.1 x (partie lordow
    ///     w miescie + bohaterowie bez partii zdatni do turnieju - prywatne SuitableForTournament gry) - 0.2), tylko tydzien = (int)ToWeeks % 3 zamiast
    ///     tygodnia pory. Gra: miasto zaczyna turniej tylko w tygodniu pory == hash % 3, czyli w tygodniach 0-2 z 13 (log 11.10: 61 dob bez turnieju).
    ///     Przy 3 tygodniach w porze prefiks oddaje metode grze (to samo); przy 13 kazde miasto co 3. tydzien caly rok - na dobe tyle, co w zwyklej grze.
    ///     Czynny model NavalDLC przekazuje te metode do BaseModel; nikt jej nie nadpisuje (lancuch konczy sie metoda DefaultTournamentModel - linia
    ///     startowa). AIInfluence (QuarantineTournamentPatch) ma prefiks na ConsiderStartOrEndTournament, ktory wola nas wewnatrz - bez zmian.
    ///  2. DiplomacyYearLong - postfiksy getterow DefaultAllianceModel.MaxDurationOfAlliance / MaxDurationOfWarParticipation: 84 -> 364, 42 -> 182
    ///     (przy odczycie). Gra zapisuje koniec przy zawarciu (AllianceCampaignBehavior.AddAlliance / AddCallToWarAgreement) - trwajace porozumienia
    ///     koncza sie wedlug starych dat. Cena wezwania i budzet wzywajacego gry (x dni) rosna tak samo - rata dzienna CrownCallToWar (cena / Days())
    ///     bez zmian, korona placi dluzej; prog gry CallToWarWallet < -100000 staly.
    ///  3. DiplomacyYearLong - postfiks KingdomDecisionProposalBehavior.GetRandomWarDecision (prywatna; Priority.Low - po postfiksie BK, ktory te sama
    ///     metode lata: rozejm BK i zamiana wyniku na BKDeclareWarDecision tego samego celu): gra wybiera krolestwo w pokoju > 20 dni; gdy wynik to
    ///     DeclareWarDecision (takze BK) przeciw krolestwu z pokojem <= 20 x rok / 84 dni (ok. 87) - null (brak propozycji w tej probie; gra losuje
    ///     znow przy nastepnej). Nigdy nowej decyzji. Rozejmy BK (YearsFromNow) licza juz rok 364; Diplomacy (plik ustawien Jeffa) - bez zmian (pytanie 3).
    /// Latki wpinane raz na proces w kampanii (wzor CrownCallToWar.EnsureHooks); wylaczniki czytane przy kazdym wywolaniu. Wyjatek = licznik potkniec,
    /// metoda gry dziala dalej (bez globalnego wylacznika). Koszt: ok. 100 wywolan prefiksu na dobe (tick dobowy miast), postfiks rozejmu przy
    /// propozycjach (kilka na dobe). Liczniki czyszczone w konstruktorze ArmouryBehavior (Reset); bez zapisu.
    /// </summary>
    internal static class WorldTime7
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double GameYear = 84.0;        // rok gry (7 x 3 x 4), w ktorym zapisane sa stale R8
        private const double GameTruceDays = 20.0;   // G KingdomDecisionProposalBehavior.GetRandomWarDecision: PeaceDeclarationDate.ElapsedDaysUntilNow > 20f

        private static bool TourOn { get { var s = Settings.Current; return s != null && s.TournamentsAllSeason; } }
        private static bool DiploOn { get { var s = Settings.Current; return s != null && s.DiplomacyYearLong; } }

        private static int _dHeld, _sHeld, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset() { _dHeld = _sHeld = 0; _stumbles = 0; _err.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("WorldTime7." + where, e); } catch { }
        }

        /// <summary>Rok, ktory gra naprawde liczy, przez rok gry 84 (Armoury 364/84 = 4.33; bez dlugiego roku 1).</summary>
        private static double YearFactor() { try { return Math.Max(28, CampaignTime.DaysInYear) / GameYear; } catch { return 1.0; } }

        /// <summary>Przerwa po pokoju przed propozycja wojny gry w roku Armoury (20 x rok / 84; przy 364 - 86.7 dnia).</summary>
        private static double TruceDays() { return GameTruceDays * YearFactor(); }

        private static CampaignTime Scale(CampaignTime t)
        {
            double f = YearFactor();
            return Math.Abs(f - 1.0) < 1e-9 ? t : CampaignTime.Days((float)(t.ToDays * f));
        }

        // ------------------------------------------------------------ 1. turnieje co 3. tydzien caly rok
        private static Func<DefaultTournamentModel, Hero, bool> _suitable;   // prywatne SuitableForTournament gry (wiek i bron jednoreczna/dwureczna > 100)

        public static bool TournamentPrefix(DefaultTournamentModel __instance, Town __0, ref float __result)
        {
            try
            {
                if (!TourOn || _suitable == null || __instance == null || __0 == null || CampaignTime.WeeksInSeason == 3) return true;   // 3 tygodnie w porze - dokladnie gra
                var st = __0.Settlement;
                if (st == null || __0.StringId == null) return true;
                if (st.SiegeEvent != null) { __result = 0f; return false; }
                if (Math.Abs(__0.StringId.GetHashCode() % 3) != (int)CampaignTime.Now.ToWeeks % 3) { __result = 0f; return false; }
                int n = 0;
                var ps = st.Parties;
                if (ps != null) for (int i = 0; i < ps.Count; i++) { var p = ps[i]; if (p != null && p.IsLordParty) n++; }
                var hs = st.HeroesWithoutParty;
                if (hs != null) for (int i = 0; i < hs.Count; i++) { var h = hs[i]; if (h != null && _suitable(__instance, h)) n++; }
                __result = 0.1f * (float)n - 0.2f;
                return false;
            }
            catch (Exception e) { Stumble("TournamentPrefix", e); return true; }
        }

        // ------------------------------------------------------------ 2. sojusz i wezwanie do wojny w roku Armoury
        public static void AlliancePostfix(ref CampaignTime __result)
        {
            try { if (DiploOn) __result = Scale(__result); } catch (Exception e) { Stumble("AlliancePostfix", e); }
        }

        public static void ParticipationPostfix(ref CampaignTime __result)
        {
            try { if (DiploOn) __result = Scale(__result); } catch (Exception e) { Stumble("ParticipationPostfix", e); }
        }

        // ------------------------------------------------------------ 3. przerwa po pokoju przed propozycja wojny gry
        public static void TrucePostfix(Clan __0, ref KingdomDecision __result)
        {
            if (__result == null) return;
            try
            {
                if (!DiploOn) return;
                var dw = __result as DeclareWarDecision;
                var own = __0 != null ? __0.Kingdom : null;
                var target = dw != null ? dw.FactionToDeclareWarOn as Kingdom : null;
                if (own == null || target == null) return;
                var link = own.GetStanceWith(target);
                if (link == null || link.IsAtWar) return;
                if (link.PeaceDeclarationDate.ElapsedDaysUntilNow <= TruceDays()) { __result = null; _dHeld++; _sHeld++; }
            }
            catch (Exception e) { Stumble("TrucePostfix", e); }
        }

        /// <summary>Dopisek linii dobowej "Wojna i pokoj (E7/0)" (WorldClock7.Daily) - liczba propozycji wstrzymanych od poprzedniej linii; zeruje dobe.</summary>
        internal static string TakeDaySegment()
        {
            int n = _dHeld; _dHeld = 0;
            if (!_truceWired) return "propozycje wojny gry wstrzymane rozejmem: - (postfiks rozejmu niewpiety)";
            if (!DiploOn) return "propozycje wojny gry wstrzymane rozejmem: - (DiplomacyYearLong off)";
            return "propozycje wojny gry wstrzymane rozejmem: " + n + " (sesja " + _sHeld + ", rozejm gry " + TruceDays().ToString("0", Inv) + " dni; E7/K potkniecia " + _stumbles + ")";
        }

        // ------------------------------------------------------------ latki gry (raz na proces, w kampanii)
        private static Harmony _harmony;
        private static bool _hooksTried, _tourWired, _allyWired, _partWired, _truceWired;
        private static MethodBase _truceTarget;
        private static readonly List<string> _missing = new List<string>();
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_harmony == null || Campaign.Current == null) return;
            if (!_hooksTried)
            {
                _hooksTried = true;
                try
                {
                    var su = AccessTools.Method(typeof(DefaultTournamentModel), "SuitableForTournament", new[] { typeof(Hero) });
                    if (su != null) _suitable = (Func<DefaultTournamentModel, Hero, bool>)Delegate.CreateDelegate(typeof(Func<DefaultTournamentModel, Hero, bool>), su);
                }
                catch (Exception e) { _suitable = null; _missing.Add("SuitableForTournament (blad: " + e.Message + ")"); }
                if (_suitable != null)
                    _tourWired = Wire("GetTournamentStartChance", AccessTools.Method(typeof(DefaultTournamentModel), "GetTournamentStartChance", new[] { typeof(Town) }), nameof(TournamentPrefix), null, Priority.Normal);
                else if (_missing.Count == 0) _missing.Add("SuitableForTournament");
                _allyWired = Wire("MaxDurationOfAlliance", AccessTools.PropertyGetter(typeof(DefaultAllianceModel), "MaxDurationOfAlliance"), null, nameof(AlliancePostfix), Priority.Normal);
                _partWired = Wire("MaxDurationOfWarParticipation", AccessTools.PropertyGetter(typeof(DefaultAllianceModel), "MaxDurationOfWarParticipation"), null, nameof(ParticipationPostfix), Priority.Normal);
                _truceTarget = AccessTools.Method(typeof(KingdomDecisionProposalBehavior), "GetRandomWarDecision", new[] { typeof(Clan) });
                _truceWired = Wire("GetRandomWarDecision", _truceTarget, null, nameof(TrucePostfix), Priority.Low);
            }
            SessionLine();
        }

        private static bool Wire(string label, MethodBase m, string pre, string post, int priority)
        {
            try
            {
                if (m == null) { _missing.Add(label); return false; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(WorldTime7), pre) { priority = priority } : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(WorldTime7), post) { priority = priority } : null);
                return true;
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); return false; }
        }

        /// <summary>Linia startowa sesji: stan wylacznikow i latek, lancuch modeli turniejow, odczyt modelu sojuszy, kolejnosc wobec postfiksu BK.</summary>
        private static void SessionLine()
        {
            try
            {
                var sb = new StringBuilder(420);
                sb.Append("Czas swiata (E7/K): turnieje ");
                if (!TourOn) sb.Append("wedlug gry - tydzien pory 0-2 (TournamentsAllSeason off; ");
                else if (CampaignTime.WeeksInSeason == 3) sb.Append("wedlug gry - 3 tygodnie w porze, to samo (");
                else sb.Append("co 3. tydzien (");
                sb.Append(_tourWired ? "prefiks wpiety" : "prefiks NIEWPIETY").Append("; czynny model: ").Append(TournamentChain()).Append(')');
                double ally = -1, part = -1;
                try { var am = Campaign.Current.Models.AllianceModel; ally = am.MaxDurationOfAlliance.ToDays; part = am.MaxDurationOfWarParticipation.ToDays; } catch { }
                sb.Append(", sojusz ").Append(ally.ToString("0", Inv)).Append(" / wezwanie ").Append(part.ToString("0", Inv))
                  .Append(DiploOn ? "" : " - wedlug gry (DiplomacyYearLong off)")
                  .Append(" (").Append(_allyWired && _partWired ? "postfiksy wpiete" : "postfiksy NIEWPIETE").Append("; odczyt modelu sojuszy gry)");
                sb.Append(", rozejm gry ").Append((DiploOn ? TruceDays() : GameTruceDays).ToString("0", Inv)).Append(" dni")
                  .Append(DiploOn ? "" : " - wedlug gry (DiplomacyYearLong off)")
                  .Append(" (").Append(_truceWired ? "postfiks wpiety" : "postfiks NIEWPIETY").Append(", po BK ").Append(AfterBk()).Append(')');
                sb.Append(" | rok ").Append(CampaignTime.DaysInYear).Append(" dni, ").Append(CampaignTime.WeeksInSeason).Append(" tygodni w porze");
                if (_missing.Count > 0) sb.Append(" | BRAK: ").Append(string.Join(", ", _missing.ToArray()));
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SessionLine", e); }
        }

        /// <summary>Lancuch modeli turniejow (BaseModel) i typ, w ktorym jest metoda wykonana na koncu (powinien byc DefaultTournamentModel - wtedy prefiks trafia).</summary>
        private static string TournamentChain()
        {
            try
            {
                object m = Campaign.Current.Models.TournamentModel;
                var names = new List<string>();
                object last = null;
                for (int i = 0; i < 6 && m != null; i++)
                {
                    names.Add(m.GetType().Name);
                    last = m;
                    PropertyInfo bp = null;
                    try { bp = AccessTools.Property(m.GetType(), "BaseModel"); } catch { bp = null; }
                    object next = null;
                    try { next = bp != null ? bp.GetValue(m, null) : null; } catch { next = null; }
                    if (ReferenceEquals(next, m)) break;
                    m = next;
                }
                string decl = "?";
                try { var mi = last != null ? last.GetType().GetMethod("GetTournamentStartChance", new[] { typeof(Town) }) : null; if (mi != null && mi.DeclaringType != null) decl = mi.DeclaringType.Name; } catch { }
                return string.Join(" -> ", names.ToArray()) + "; metoda na koncu lancucha z " + decl + (decl == nameof(DefaultTournamentModel) ? " (trafia)" : " (NIE TRAFIA)");
            }
            catch { return "?"; }
        }

        /// <summary>Czy nasz postfiks rozejmu biegnie po postfiksie BK (GetRandomWarDecisionPatch) - wyzszy priorytet albo ten sam i wczesniejszy indeks = BK pierwszy.</summary>
        private static string AfterBk()
        {
            try
            {
                var info = _truceTarget != null ? Harmony.GetPatchInfo(_truceTarget) : null;
                if (info == null || info.Postfixes == null) return "-";
                Patch ours = null, bk = null;
                foreach (var p in info.Postfixes)
                {
                    var t = p != null && p.PatchMethod != null ? p.PatchMethod.DeclaringType : null;
                    if (t == null) continue;
                    if (t == typeof(WorldTime7)) ours = p;
                    else if (t.Assembly.GetName().Name.StartsWith("BannerKings", StringComparison.Ordinal)) bk = p;
                }
                if (bk == null) return "brak postfiksu BK";
                if (ours == null) return "- (nasz niewpiety)";
                return ours.priority < bk.priority || (ours.priority == bk.priority && ours.index > bk.index) ? "tak" : "NIE";
            }
            catch { return "?"; }
        }
    }
}
