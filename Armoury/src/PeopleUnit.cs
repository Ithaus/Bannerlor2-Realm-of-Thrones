using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// JEDNA JEDNOSTKA LUDZI (paczka E4/3 = 108n; projekt etapu 4 rozdz. 2 R0, R6, R7, R18; stara paczka 108 `e548266` przeniesiona na dzisiejszy kod).
    /// Wies na mapie to symbol calej okolicy: PopulationLaw liczy jej ludzi jako hearth x k_v (k_v = ludzi na punkt hearth TEJ osady z dnia
    /// kalibracji - E4/3 U3; Reach ok. 444, Polnoc 132, Nocna Straz 3.7). Gra i nasz kod zdejmowaly jednak za JEDNEGO czlowieka pol punktu hearth:
    /// tabor 20 chlopow kosztowal wies Reach 10 hearth, czyli ok. 4 400 ludzi ksiegi. Odtad czlowiek, ktory wychodzi ze wsi albo do niej wraca, to 1/k_v hearth:
    ///  - wyrzutki (OutlawLaw: siew puli, bieda, rabunek, powrot do wsi) - OutlawLaw pyta o stawke `PerMan`;
    ///  - tabory wsi: gra (VillagerCampaignBehavior.AddVillagersToParty / CreateVillagerParty) i lodzie rybackie NavalDLC
    ///    (FishingPartyCampaignBehavior.OnDailySettlementTick / TryReinforceParty) zdejmuja (n + 1) / 2 hearth za n ludzi -
    ///    postfiks przywraca hearth sprzed metody i zdejmuje n / k_v (PeopleBook.Move);
    ///  - wymuszony pobor gracza (VillageHostileActionCampaignBehavior: n rekrutow, n / 2 hearth) - tak samo;
    ///  - zadania gracza we wsi (+20..+80 albo -30..-40 hearth za narzedzia, zwierzeta, stado, materialy): hearth gry to 2 ludzi, wiec zmiana x 2 / k_v;
    ///  - incydenty gracza (hearth +-5..30) ZOSTAJA w hearth gry - patrz IncidentPostfix; sa tylko liczone (nazwane w linii "Hearth (E4/0)").
    /// Kazda zmiana hearth za ludzi idzie przez PeopleBook.Move (jedyna droga, U23); `Shift` wola tylko Move. Kultura bez przelicznika (k = 0) zostaje przy
    /// stawce dotychczasowej, a stawka 1/k nigdy nie jest wyzsza od dotychczasowej (kraina o k ponizej 2 nie traci na czlowieku wiecej niz dotad).
    /// Latki nie zmieniaja liczby ludzi, zlota ani towaru - tylko to, ile hearth wies oddaje za tych samych ludzi.
    /// Przy wylaczonym `PeopleUnitEnabled` kazda metoda konczy na liczeniu do linii "Ludzie (ksiega)" - gra biegnie jak dotad.
    /// </summary>
    internal static class PeopleUnit
    {
        /// <summary>Stawka gry: pol punktu hearth za czlowieka taboru i wymuszonego rekruta.</summary>
        internal const float GamePerMan = 0.5f;
        private const float Tol = 0.01f;       // zgodnosc ubytku hearth z formula gry (odejmowanie liczby calkowitej od floata: blad ponizej 0.0001)

        internal static bool On { get { var s = Settings.Current; return s != null && s.PeopleUnitEnabled; } }
        /// <summary>Wyrzutkow liczymy od ludzi wsi, nie od jej hearth (tylko razem z jednostka ludzi).</summary>
        internal static bool ByPeople { get { var s = Settings.Current; return s != null && s.PeopleUnitEnabled && s.OutlawsCountedByPeople; } }

        // reszta hearth, ktorej float wsi nie przyjal (patrz Shift) - bez zapisu, najwyzej pol kroku floata na wies
        private static readonly Dictionary<Village, double> _carry = new Dictionary<Village, double>();

        // liczniki doby (do linii "Ludzie (ksiega)"); hearth: Game = ile zdjela albo dala gra, Ours = ile zostalo po przeliczeniu
        private static int _vFills, _vBorn, _vMen, _vOdd, _vNoK;
        private static double _vGame, _vOurs;
        private static int _pTimes, _pMen, _pOdd;
        private static double _pGame, _pOurs;
        private static int _qTimes;
        private static double _qGame, _qOurs;
        private static int _iTimes;
        private static double _iHearth;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();      // miejsca, ktorych pierwszy wyjatek juz jest w logu

        internal static void Reset()
        {
            _carry.Clear(); _errSites.Clear();
            NewDay();
        }

        internal static void NewDay()
        {
            _vFills = _vBorn = _vMen = _vOdd = _vNoK = 0; _vGame = _vOurs = 0.0;
            _pTimes = _pMen = _pOdd = 0; _pGame = _pOurs = 0.0;
            _qTimes = 0; _qGame = _qOurs = 0.0;
            _iTimes = 0; _iHearth = 0.0;
            _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;                                    // liczymy potkniecia, niczego nie gasimy
            try { if (_errSites.Add(where)) Log.Error(where, e); } catch { }   // pierwszy wyjatek kazdego miejsca do logu, kolejne tylko w liczniku doby
        }

        // ------------------------------------------------------------ stawka i ruch hearth
        /// <summary>
        /// Hearth za jednego czlowieka tej wsi. `legacy` to stawka dotychczasowa: zwracana, gdy jednostka jest wylaczona albo
        /// osada nie ma przelicznika (`unit` = false - wolajacy liczy wtedy dokladnie po staremu), i zarazem sufit stawki 1/k.
        /// </summary>
        internal static float PerMan(Village v, float legacy, out bool unit)
        {
            unit = false;
            if (!On) return legacy;
            float per = PopulationLaw.HearthPerMan(v);
            if (!(per > 0f) || !(legacy > 0f)) return legacy;
            unit = true;
            return Math.Min(per, legacy);
        }

        /// <summary>
        /// Podstawa, od ktorej liczy sie wyrzutkow wsi (ustawienia Outlaw...PerHearth). Dotad sam hearth: kraina o 4 ludziach
        /// na punkt hearth dawala wtedy tylu wyrzutkow z punktu, co kraina o 444. Przy liczeniu od ludzi: ludnosc wsi / srednia
        /// swiata ludzi na hearth - "hearth sredniej wsi swiata"; suma swiata zostaje, a kazda wies oddaje ten sam ulamek ludzi.
        /// Wies bez przelicznika: hearth jak dotad.
        /// </summary>
        internal static float Base(Village v)
        {
            if (v == null) return 0f;
            if (!ByPeople || v.Settlement == null) return v.Hearth;
            if (!(PopulationLaw.HearthPerMan(v) > 0f)) return v.Hearth;
            float world = PopulationLaw.WorldPeoplePerHearth();
            if (!(world > 0f)) return v.Hearth;
            return PopulationLaw.PeopleOf(v.Settlement) / world;
        }

        /// <summary>
        /// Zmienia hearth wsi o `delta` punktow - WOLA TYLKO PeopleBook.Move (U23). Hearth to float o kroku 0.00003-0.00006 przy 300-1000 punktach,
        /// a jeden czlowiek Reach to 0.0022 punktu - dzienny naplyw wyrzutkow regionu bywa mniejszy od kroku i ginalby w zaokragleniu
        /// (albo zaokraglal sie w gore) za kazdym razem. Reszta, ktorej float nie przyjal, czeka w `_carry` tej wsi i wchodzi
        /// przy nastepnej zmianie. Zwraca zmiane faktycznie przyjeta (z reszta; hearth nie schodzi ponizej 0).
        /// </summary>
        internal static double Shift(Village v, double delta)
        {
            if (v == null || delta == 0.0 || double.IsNaN(delta) || double.IsInfinity(delta)) return 0.0;
            double carry; _carry.TryGetValue(v, out carry);
            double was = (double)v.Hearth + carry;
            double want = was + delta;
            if (want < 0.0) want = 0.0;
            float set = (float)want;
            v.Hearth = set;
            double rest = want - (double)set;
            if (rest != 0.0) _carry[v] = rest; else _carry.Remove(v);
            return want - was;
        }

        // ------------------------------------------------------------ tabory wsi i lodzie rybackie
        /// <summary>Stan wsi sprzed metody gry (prefiks -> postfiks).</summary>
        internal sealed class Was
        {
            public Village V; public float Hearth; public MobileParty Party; public int Men; public object Comp;
        }

        private static Was Before(Village v, MobileParty party)
        {
            if (v == null) return null;
            if (On) PopulationLaw.EnsureCalibrated();        // k liczone z hearth sprzed pierwszego zdjecia
            return new Was
            {
                V = v, Hearth = v.Hearth, Party = party, Comp = v.VillagerPartyComponent,
                Men = party != null && party.MemberRoster != null ? party.MemberRoster.TotalManCount : 0
            };
        }

        /// <summary>
        /// Po metodzie gry, ktora dopisala `men` ludzi do taboru wsi i zdjela za nich (men + 1) / 2 hearth (nowy tabor: nie
        /// ponizej zera). Liczy do ksiegi; przy wlaczonej jednostce stawia hearth sprzed metody i zdejmuje men / k_v (PeopleBook.Move).
        /// Gdy ubytek nie zgadza sie z formula gry (inny mod zmienil metode albo hearth ruszylo cos jeszcze), niczego nie
        /// ruszamy - liczymy "niezgodne".
        /// </summary>
        private static void Settle(Was w, int men, bool born)
        {
            float took = w.Hearth - w.V.Hearth;
            if (men <= 0) { if (Math.Abs(took) > Tol) _vOdd++; return; }
            int game = (men + 1) / 2;
            if (Math.Abs(took - game) > Tol && Math.Abs(took - Math.Min(w.Hearth, game)) > Tol) { _vOdd++; return; }
            _vMen += men; _vGame += took;
            if (born) _vBorn++; else _vFills++;
            bool unit;
            PerMan(w.V, GamePerMan, out unit);
            if (!unit)
            {
                _vOurs += took; if (On) _vNoK += men;
                PeopleBook.Note(men, PeopleBook.MCart, -took);       // hearth zdjela gra - tylko licznik ksiegi
                return;
            }
            w.V.Hearth = w.Hearth;                           // cofniecie zapisu gry: stan sprzed, co do bitu (ludzie tylko przez Move ponizej)
            double moved = PeopleBook.Move(w.V, PeopleBook.ADom, PeopleBook.ACarts, men, PeopleBook.MCart, GamePerMan);
            _vOurs += -moved;
        }

        // VillagerCampaignBehavior.AddVillagersToParty(MobileParty villagerParty, int numberOfVillagersToAdd)
        public static void RefillPrefix(MobileParty __0, out Was __state)
        {
            __state = null;
            try
            {
                var home = __0 != null ? __0.HomeSettlement : null;
                __state = Before(home != null ? home.Village : null, __0);
            }
            catch (Exception e) { __state = null; Stumble("PeopleUnit.RefillPrefix", e); }
        }

        // NavalDLC FishingPartyCampaignBehavior.TryReinforceParty(FishingPartyComponent fishingParty) - typ NavalDLC, stad object
        public static void FishRefillPrefix(object __0, out Was __state)
        {
            __state = null;
            try
            {
                var comp = __0 as VillagerPartyComponent;
                if (comp == null) return;
                __state = Before(comp.Village, comp.MobileParty);
            }
            catch (Exception e) { __state = null; Stumble("PeopleUnit.FishRefillPrefix", e); }
        }

        public static void RefillPostfix(Was __state)
        {
            if (__state == null || __state.Party == null || __state.Party.MemberRoster == null) return;
            try { Settle(__state, __state.Party.MemberRoster.TotalManCount - __state.Men, false); }
            catch (Exception e) { Stumble("PeopleUnit.RefillPostfix", e); }
        }

        // VillagerCampaignBehavior.CreateVillagerParty(Village village)
        public static void CreatePrefix(Village __0, out Was __state)
        {
            __state = null;
            try { __state = Before(__0, null); }
            catch (Exception e) { __state = null; Stumble("PeopleUnit.CreatePrefix", e); }
        }

        public static void CreatePostfix(Was __state)
        {
            if (__state == null) return;
            try
            {
                var comp = __state.V.VillagerPartyComponent;
                var born = comp != null && !ReferenceEquals(comp, __state.Comp) ? comp.MobileParty : null;
                if (born == null || born.MemberRoster == null) { if (Math.Abs(__state.Hearth - __state.V.Hearth) > Tol) _vOdd++; return; }
                Settle(__state, born.MemberRoster.TotalManCount, true);
            }
            catch (Exception e) { Stumble("PeopleUnit.CreatePostfix", e); }
        }

        // NavalDLC FishingPartyCampaignBehavior.OnDailySettlementTick(Settlement settlement): co dobe dla kazdej osady,
        // lodz woduje rzadko - poza tym dniem postfiks konczy na porownaniu hearth
        public static void FishCreatePrefix(Settlement __0, out Was __state)
        {
            __state = null;
            try { __state = Before(__0 != null ? __0.Village : null, null); }
            catch (Exception e) { __state = null; Stumble("PeopleUnit.FishCreatePrefix", e); }
        }

        public static void FishCreatePostfix(Settlement __0, Was __state)
        {
            if (__state == null) return;
            try
            {
                if (__state.V.Hearth == __state.Hearth) return;
                // nowa lodz to ostatnia partia tej wsi na liscie taborow gry (lista rosnie od konca); tabor ladowy wsi to nie lodz.
                // Gdyby wybor byl zly, Settle zobaczy niezgodnosc z formula gry i niczego nie ruszy.
                MobileParty born = null;
                var all = MobileParty.AllVillagerParties;
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    var p = all[i];
                    if (p == null || p.PartyComponent == null || p.HomeSettlement != __0) continue;
                    if (ReferenceEquals(p.PartyComponent, __state.V.VillagerPartyComponent)) continue;
                    born = p; break;
                }
                if (born == null || born.MemberRoster == null) { _vOdd++; return; }
                Settle(__state, born.MemberRoster.TotalManCount, true);
            }
            catch (Exception e) { Stumble("PeopleUnit.FishCreatePostfix", e); }
        }

        // ------------------------------------------------------------ wymuszony pobor gracza
        /// <summary>Ilu rekrutow gra daje za wymuszony pobor (VillageHostileActionCampaignBehavior) - jej formula, na hearth sprzed.</summary>
        private static int PressedMen(Settlement st, Village v)
        {
            int n = (int)Math.Ceiling(v.Hearth / 30f);
            if (MobileParty.MainParty.HasPerk(DefaultPerks.Roguery.InBestLight)) n += st.Notables.Count;
            return n;
        }

        // VillageHostileActionCampaignBehavior.village_force_volunteers_ended_successfully_on_consequence(MenuCallbackArgs args)
        public static void PressPrefix(out Was __state)
        {
            __state = null;
            try
            {
                var st = Settlement.CurrentSettlement;
                var w = Before(st != null ? st.Village : null, null);
                if (w == null) return;
                w.Men = PressedMen(st, w.V);
                __state = w;
            }
            catch (Exception e) { __state = null; Stumble("PeopleUnit.PressPrefix", e); }
        }

        public static void PressPostfix(Was __state)
        {
            if (__state == null) return;
            try
            {
                var w = __state;
                float took = w.Hearth - w.V.Hearth;
                if (w.Men <= 0 || Math.Abs(took - w.Men / 2) > Tol) { if (Math.Abs(took) > Tol) _pOdd++; return; }      // gra: hearth -= n / 2 (calkowite)
                _pTimes++; _pMen += w.Men; _pGame += took;
                bool unit;
                PerMan(w.V, GamePerMan, out unit);
                if (!unit) { _pOurs += took; PeopleBook.Note(w.Men, PeopleBook.MPress, -took); return; }
                w.V.Hearth = w.Hearth;                       // cofniecie zapisu gry (ludzie tylko przez Move)
                double moved = PeopleBook.Move(w.V, PeopleBook.ADom, PeopleBook.AService, w.Men, PeopleBook.MPress, GamePerMan);
                _pOurs += -moved;
                // E4/7: zdjeci z DOM tej wsi - pochodzenie regionu wsi (wlasny try, po Move - U24). Recenzja E4/7 R1-5: gra otwiera ekran lupu (gracz bierze ilu chce),
                // wiec udzialy czekaja na zatwierdzenie tego ekranu (PeopleBook.ScreenPost -> PressTake) zamiast trafiac do druzyny od razu
                try { PeopleBook.PressPending(OutlawLaw.RegionFor(w.V.Settlement), w.Men); }
                catch (Exception e) { Stumble("PeopleUnit.PressPostfix(E4/7)", e); }
            }
            catch (Exception e) { Stumble("PeopleUnit.PressPostfix", e); }
        }

        // ------------------------------------------------------------ zadania gracza we wsi
        // 13 metod w 4 zachowaniach gry: kazda sama zmienia hearth wsi zleceniodawcy o stala (zadna nie wola innej z tej listy).
        private static readonly string[][] QuestSites =
        {
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssueBehavior", "HeadmanVillageNeedsDraughtAnimalsIssue", "AlternativeSolutionEndWithSuccessConsequence" },
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssueBehavior", "HeadmanVillageNeedsDraughtAnimalsIssueQuest", "QuestSuccessPlayerDeliveredAnimalsNormal" },
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssueBehavior", "HeadmanVillageNeedsDraughtAnimalsIssueQuest", "QuestSuccessPlayerDeliveredAnimalsWithAcceptingDiscount" },
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssueBehavior", "HeadmanVillageNeedsDraughtAnimalsIssueQuest", "QuestSuccessPlayerDeliveredAnimalsWithoutAcceptingDiscount" },
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssueBehavior", "HeadmanVillageNeedsDraughtAnimalsIssueQuest", "OnTimedOut" },
            new[] { "HeadmanNeedsToDeliverAHerdIssueBehavior", "HeadmanNeedsToDeliverAHerdIssue", "ApplySuccessRewards" },
            new[] { "HeadmanNeedsToDeliverAHerdIssueBehavior", "HeadmanNeedsToDeliverAHerdIssueQuest", "OnCompleteWithSuccess" },
            new[] { "VillageNeedsCraftingMaterialsIssueBehavior", "VillageNeedsCraftingMaterialsIssue", "AlternativeSolutionEndWithSuccessConsequence" },
            new[] { "VillageNeedsCraftingMaterialsIssueBehavior", "VillageNeedsCraftingMaterialsIssueQuest", "Success" },
            new[] { "VillageNeedsCraftingMaterialsIssueBehavior", "VillageNeedsCraftingMaterialsIssueQuest", "Fail" },
            new[] { "VillageNeedsToolsIssueBehavior", "VillageNeedsToolsIssue", "AlternativeSolutionEndWithSuccessConsequence" },
            new[] { "VillageNeedsToolsIssueBehavior", "VillageNeedsToolsIssueQuest", "OnTimedOut" },
            new[] { "VillageNeedsToolsIssueBehavior", "VillageNeedsToolsIssueQuest", "FinishQuestSuccess1" },
        };

        public static void QuestPrefix(object __instance, out Was __state)
        {
            __state = null;
            try
            {
                Hero giver = null;
                var issue = __instance as IssueBase;
                if (issue != null) giver = issue.IssueOwner;
                else { var quest = __instance as QuestBase; if (quest != null) giver = quest.QuestGiver; }
                var st = giver != null ? giver.CurrentSettlement : null;
                __state = Before(st != null ? st.Village : null, null);
            }
            catch (Exception e) { __state = null; Stumble("PeopleUnit.QuestPrefix", e); }
        }

        public static void QuestPostfix(Was __state)
        {
            if (__state == null) return;
            try
            {
                var w = __state;
                float moved = w.V.Hearth - w.Hearth;        // + nagroda, - kara
                if (Math.Abs(moved) < Tol) return;
                _qTimes++; _qGame += moved;
                double men = Math.Abs((double)moved) / GamePerMan;   // hearth gry = 2 ludzi
                bool unit;
                PerMan(w.V, GamePerMan, out unit);
                if (!unit) { _qOurs += moved; PeopleBook.Note(men, PeopleBook.MQuest, moved); return; }
                w.V.Hearth = w.Hearth;                       // cofniecie zapisu gry (ludzie tylko przez Move)
                double ours = moved > 0f
                    ? PeopleBook.Move(w.V, PeopleBook.AOutside, PeopleBook.ADom, men, PeopleBook.MQuest, GamePerMan)
                    : PeopleBook.Move(w.V, PeopleBook.ADom, PeopleBook.AOutside, men, PeopleBook.MQuest, GamePerMan);
                _qOurs += ours;
            }
            catch (Exception e) { Stumble("PeopleUnit.QuestPostfix", e); }
        }

        // ------------------------------------------------------------ incydenty gracza - tylko licznik
        // IncidentEffect.VillageHearthChange / TownBoundVillageHearthChange: skutek to zamkniecie (metoda generowana przez
        // kompilator) z kwota CALKOWITA, ktora gra pokazuje graczowi w tekscie incydentu ("Increased hearth of X by 20").
        // Przeliczenie rozjechaloby tekst z tym, co sie stalo, a kwoty ponizej jednosci nie da sie tam wpisac - zostaja w
        // hearth gry. Liczymy, ile hearth daly (ksiega: nazwane w linii "Hearth (E4/0)", poza RESZTA).
        public static void IncidentPostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                var t = __instance.GetType();
                var fa = t.GetField("amount");
                if (fa == null || fa.FieldType != typeof(int)) return;
                int amount = (int)fa.GetValue(__instance);
                int villages = 1;
                var ft = t.GetField("townGetter");
                if (ft != null)
                {
                    var get = ft.GetValue(__instance) as Func<Town>;
                    var town = get != null ? get() : null;
                    villages = town != null && town.Villages != null ? town.Villages.Count : 0;
                }
                _iTimes++; _iHearth += (double)amount * villages;
                PeopleBook.Note(Math.Abs((double)amount * villages) / GamePerMan, PeopleBook.MIncident, (double)amount * villages);
            }
            catch (Exception e) { Stumble("PeopleUnit.IncidentPostfix", e); }
        }

        // ------------------------------------------------------------ log
        private static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        private static string Sg(double v, string fmt) { return (v >= 0.0 ? "+" : "") + v.ToString(fmt, CultureInfo.InvariantCulture); }

        /// <summary>Dopisek do linii "Ludzie (ksiega)": tabory gry i gracz - ile hearth zdjela albo dala gra, ile zostalo po przeliczeniu.</summary>
        internal static string DayNote()
        {
            var sb = new StringBuilder(" | jednostka w latkach gry (PeopleUnit): ");
            sb.Append("tabory wsi i lodzie -").Append(F(_vOurs, "0.###")).Append(" hearth za ").Append(_vMen).Append(" ludzi (uzupelnien ").Append(_vFills)
              .Append(", nowych ").Append(_vBorn).Append("; gra zdjela ").Append(F(_vGame, "0.#")).Append(')');
            sb.Append(", pobor wymuszony gracza -").Append(F(_pOurs, "0.###")).Append(" za ").Append(_pMen).Append(" ludzi w ").Append(_pTimes)
              .Append(" (gra ").Append(F(_pGame, "0.#")).Append(')');
            sb.Append(", zadania gracza ").Append(Sg(_qOurs, "0.###")).Append(" w ").Append(_qTimes).Append(" (gra ").Append(Sg(_qGame, "0.#")).Append(')');
            sb.Append(", incydenty gracza ").Append(Sg(_iHearth, "0.#")).Append(" w ").Append(_iTimes).Append(" (bez przeliczenia)");
            int odd = _vOdd + _pOdd;
            if (odd > 0) sb.Append("; niezgodne z formula gry i zostawione: ").Append(odd);
            if (_vNoK > 0) sb.Append("; bez przelicznika osady: ").Append(_vNoK).Append(" ludzi taborow");
            if (_stumbles > 0) sb.Append("; potkniecia: ").Append(_stumbles);
            return sb.ToString();
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>();
            var miss = new List<string>();
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            // cel: metoda zadeklarowana w tym typie, bez wyniku, o podanej liczbie parametrow; pierwszy parametr musi pasowac do `first`
            Func<Type, string, int, Type, MethodInfo> find = (type, name, count, first) =>
            {
                if (type == null) return null;
                MethodInfo hit = null;
                foreach (var m in type.GetMethods(Own))
                {
                    if (m.Name != name || m.IsStatic || m.IsAbstract || m.ReturnType != typeof(void)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != count) continue;
                    if (first != null && (ps.Length == 0 || !first.IsAssignableFrom(ps[0].ParameterType))) continue;
                    if (hit != null) return null;           // dwie pasujace - nie zgadujemy
                    hit = m;
                }
                return hit;
            };
            Action<MethodInfo, string, string, string> patch = (m, pre, post, label) =>
            {
                try
                {
                    if (m == null) { miss.Add(label); return; }
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PeopleUnit), pre), postfix: new HarmonyMethod(typeof(PeopleUnit), post));
                    done.Add(label);
                }
                catch (Exception e) { miss.Add(label + "(" + e.Message + ")"); }
            };

            var vcb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.VillagerCampaignBehavior");
            var refill = find(vcb, "AddVillagersToParty", 2, typeof(MobileParty));
            if (refill != null && refill.GetParameters()[1].ParameterType != typeof(int)) refill = null;
            patch(refill, nameof(RefillPrefix), nameof(RefillPostfix), "tabor wsi - uzupelnienie");
            patch(find(vcb, "CreateVillagerParty", 1, typeof(Village)), nameof(CreatePrefix), nameof(CreatePostfix), "tabor wsi - nowy");

            var fish = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.FishingPartyCampaignBehavior");
            if (fish == null) done.Add("lodzie rybackie - brak NavalDLC");
            else
            {
                patch(find(fish, "TryReinforceParty", 1, typeof(VillagerPartyComponent)), nameof(FishRefillPrefix), nameof(RefillPostfix), "lodz rybacka - uzupelnienie");
                patch(find(fish, "OnDailySettlementTick", 1, typeof(Settlement)), nameof(FishCreatePrefix), nameof(FishCreatePostfix), "lodz rybacka - nowa");
            }

            var vha = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior");
            patch(find(vha, "village_force_volunteers_ended_successfully_on_consequence", 1, null), nameof(PressPrefix), nameof(PressPostfix), "pobor wymuszony gracza");

            int quests = 0;
            foreach (var q in QuestSites)
            {
                try
                {
                    var outer = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Issues." + q[0]);
                    var inner = outer != null ? outer.GetNestedType(q[1], BindingFlags.Public | BindingFlags.NonPublic) : null;
                    if (inner == null || !(typeof(IssueBase).IsAssignableFrom(inner) || typeof(QuestBase).IsAssignableFrom(inner))) { miss.Add("zadanie " + q[1] + "." + q[2]); continue; }
                    var m = find(inner, q[2], 0, null) ?? find(inner, q[2], 1, typeof(Hero));
                    if (m == null) { miss.Add("zadanie " + q[1] + "." + q[2]); continue; }
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PeopleUnit), nameof(QuestPrefix)), postfix: new HarmonyMethod(typeof(PeopleUnit), nameof(QuestPostfix)));
                    quests++;
                }
                catch (Exception e) { miss.Add("zadanie " + q[1] + "." + q[2] + "(" + e.Message + ")"); }
            }
            done.Add("zadania gracza " + quests + " z " + QuestSites.Length);

            int incidents = 0;
            try
            {
                foreach (var nt in typeof(IncidentEffect).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (nt.GetField("amount") == null) continue;
                    foreach (var m in nt.GetMethods(Own))
                    {
                        if (m.IsStatic || m.Name.IndexOf("VillageHearthChange", StringComparison.Ordinal) < 0) continue;      // takze TownBoundVillageHearthChange
                        if (m.GetParameters().Length != 0 || m.ReturnType != typeof(List<TextObject>)) continue;            // skutek, nie warunek i nie opis
                        h.Patch(m, postfix: new HarmonyMethod(typeof(PeopleUnit), nameof(IncidentPostfix)));
                        incidents++;
                    }
                }
            }
            catch (Exception e) { miss.Add("incydenty gracza(" + e.Message + ")"); }
            if (incidents == 2) done.Add("incydenty gracza - licznik"); else miss.Add("incydenty gracza - licznik (" + incidents + " z 2)");

            // stan z ustawien startowych (Armoury.settings.xml albo domyslne z kodu); MCM wchodzi dopiero przy starcie kampanii (OnGameStart) i co godzine gry -
            // stan w grze kazdej doby stoi w linii "Ludzie (ksiega)" (recenzja E4/3: linia startowa nie jest dowodem, ze jednostka dziala)
            Log.Info("PeopleUnit (E4/3): latki jednostki ludzi (1 czlowiek = 1/k osady zamiast 0.5 hearth); ustawienia startowe przed MCM (nie stan gry - ten podaje kazdej doby linia \"Ludzie (ksiega)\"): "
                     + (On ? "wlaczona" : "wylaczona")
                     + (On ? (ByPeople ? ", wyrzutki liczone od ludnosci wsi" : ", wyrzutki liczone od hearth wsi") : "")
                     + "; wpiete: " + string.Join(", ", done.ToArray())
                     + "; BRAK: " + (miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-") + ".");
        }
    }
}
