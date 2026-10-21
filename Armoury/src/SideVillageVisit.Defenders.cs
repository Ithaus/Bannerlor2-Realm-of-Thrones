using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m97-wioski krok 2 - OBRONCY WIOSKI POBOCZNEJ (Jeff 20.10: "czy wioski poboczne moga miec tez obroncow?", "dopisz do kolejki po obozie", POPRAWKA: "ale ja chce, aby to
    /// byli ludzie z tej wioski pobocznej, a nie z wioski glownej"). Klucz SideVillageDefenders (z SideVillagesEnterable); NIE = regula z poprawki nr 3 (cala milicja wsi gry).
    ///  O1 KTO BRONI: wlasni ludzie wioski - 1 na 10 mieszkancow w domu (dorosli mezczyzni to ok. 1/4 ludzi, do walki staje ok. 2 z 5 z nich), najwyzej 60 (wioska bez pana
    ///     i bez muru nie wystawi wiecej), minus pamiec poleglych tej wioski (ubywa liniowo przez 60 dob: ranni wracaja do sil, chlopcy dorastaja, ludzie przychodza z okregu).
    ///     Sklad: 70% chlopi kultury wsi okregu (Culture.Villager, t1), 30% milicja kultury (2/3 wlocznicy, 1/3 lucznicy - MeleeMilitiaTroop / RangedMilitiaTroop, t2), bez
    ///     bohaterow. Milicja wsi glownej NIE broni juz wiosek pobocznych (broni tylko wsi glownej - bitwa rabunku gry).
    ///  O2 PODDANIE: zdrowi ludzie gracza (z armia, ktora prowadzi) >= 4 x obroncy - wioska poddaje sie bez walki (rabunek / okup od razu, komunikat).
    ///  O3 BITWA: mniej - "Raid the village" / "Demand tribute" wyprowadza obroncow przed wioske: tymczasowa druzyna (CustomPartyComponent - ta sama droga co druzyny zadan gry,
    ///     np. RivalGangMovingInIssue), pan okregu jako rod (wojna z graczem), stoi w miejscu, AI ja omija; PlayerEncounter.RestartPlayerEncounter + menu "encounter" (jak gra
    ///     przy wrogiej akcji we wsi: VillageHostileActionCampaignBehavior.StartHostileAction - SwitchToMenu("encounter") ze ZWYKLEGO menu; menu oczekiwania nie biora udzialu).
    ///     Ludzie druzyny to ludzie TEJ wioski (po recenzji): przy wyjsciu PeopleBook.TakeLevyHere - tylko DOM wsi okregu (ludzie wioski sa jego czescia), bez innych wsi
    ///     regionu i miasta (MLevyMilitia - ta sama droga co przyrost milicji, ArmsFromPeople.MilitiaDelta; skladnik ob KONTROLI E4/8), pochodzenie GiveNew; czego DOM nie
    ///     dal - mniej obroncow (nikt - jak poddanie). Po bitwie ocaleni (zdrowi i ranni, ktorych nikt nie wzial) wracaja do domow tam, skad ich zdjeto (PeopleBook.Take +
    ///     ArriveAt MBackMilitia - jak milicja), druzyna pusta i rozwiazana (DestroyPartyAction bez sprawcy, sklad wyczyszczony - nikt nie wraca drugi raz). Rozbici w bitwie
    ///     (RoutedInBattle - H3 / OutlawLaw odsyla ich do domow) nie sa strata. Polegli i pojmani (obroncy - ocaleni - rozbici) schodza z ludzi TEJ wioski: z DOM ubywa ich
    ///     raz, a brak rak wioski (_lack, VillageBook.LackOf) przesuwa ich udzial z reszty okregu na te wioske (polegli x (1 - udzial wioski w osadach okregu)); brak rak i
    ///     pamiec poleglych (polegli x 9/10 - dziesiata czesc juz zdejmuje mniejsza ludnosc) wracaja liniowo przez 60 dob. Wygrana gracza (MapEventEnded: strona zwyciezcy)
    ///     - rabunek / okup jak dotad (okup po rozbiciu - stawka gorna); przegrana, odwrot, "Leave" - wioska nietknieta.
    ///  O4 AI (lancuch rabunku wsi gry w ksiedze wiosek): napastnik (zdrowi ludzie partii i jego armii na starcie sesji) mniejszy niz obroncy wioski - lancuch ja pomija; wiekszy -
    ///     pali jak dotad, a obroncy tej wioski traca d x min(1, d / napastnicy) do pamieci poleglych (przy przewadze wiekszosc ucieka, przy rownych silach gina) - bez bitwy
    ///     i bez drugiego ubytku ludzi (ludzi trafionych liczy jak dotad Devastation).
    ///  O5 ZAPIS: wpis "f=" w arm_sidevil (wioska, StringId druzyny, rodzaj, obroncy, doba, wynik, rozbici), "d=" (pamiec poleglych) i "k=" (brak rak). Wczytanie: druzyna z wpisu odnaleziona i doprowadzona
    ///     do konca (straznik), bez wpisu - druzyny "Villagers of ..." CustomPartyComponent z naszym przedrostkiem nazwy sprzatane (sklad do domu, rozwiazanie).
    /// </summary>
    internal static partial class SideVillageVisit
    {
        private const double DefPeoplePer = 10.0;      // 1 obronca na 10 mieszkancow w domu
        private const int DefCap = 60;                  // pulap obroncow jednej wioski
        private const double SurrenderRatio = 4.0;      // poddanie bez walki przy 4:1
        private const double MilitiaShare = 0.30;       // czesc obroncow z milicji kultury (reszta - chlopi)
        private const int FallenDays = 60;              // pamiec poleglych - liczba wraca liniowo przez tyle dob
        private const string DefNamePre = "Villagers of ";
        private const int KRaid = 1, KTribute = 2;

        private sealed class Fight
        {
            public string Uid, Dist, Name, PartyId;
            public int Kind, Def, Men, Day, Won;        // Won: 0 - jeszcze nie, 1 - gracz wygral, 2 - nie wygral (przegrana, odwrot, Leave)
            public int Routed = -1, Dead = -1;          // z MapEventParty druzyny przy MapEventEnded (rozbici - H3 odsyla do domow; zabici); -1 - bez bitwy
            public bool Levy;                            // ludzie zdjeci z DOM (PeopleBook.TakeLevyHere) - ocaleni wracaja ArriveAt
            public double Got;
            public MobileParty Party;
        }
        private static Fight _fight;
        private static readonly Dictionary<string, double[]> _fallen = new Dictionary<string, double[]>(StringComparer.Ordinal);   // uid -> { ilu, doba }
        // recenzja (Jeff: "ludzie z tej wioski"): brak rak wioski po bitwie - polegli i pojmani x (1 - udzial wioski), uid -> { ilu, doba }; czyta VillageBook.LackOf
        private static readonly Dictionary<string, double[]> _lack = new Dictionary<string, double[]>(StringComparer.Ordinal);
        private const double FallenFighters = 1.0 - 1.0 / DefPeoplePer;   // pamiec poleglych po bitwie: 9/10 strat (1/10 zdejmuje juz mniejsza ludnosc wioski)
        private static int _dFights, _dSurr, _dWon, _dLostFights, _dOrphans, _dAiBurnNoted;
        private static double _dFallen, _dAiFallen, _dRouted, _dLack;
        private static readonly HashSet<string> _dAiSkip = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> _dDefEx = new List<string>();

        internal static bool DefOn { get { var s = Settings.Current; return s != null && s.SideVillagesEnterable && s.SideVillageDefenders; } }
        internal static bool FightActive { get { return _fight != null; } }

        private static void DefReset() { _fight = null; _fallen.Clear(); _lack.Clear(); DefDayZero(); }
        private static void DefDayZero() { _dFights = _dSurr = _dWon = _dLostFights = _dOrphans = _dAiBurnNoted = 0; _dFallen = _dAiFallen = _dRouted = _dLack = 0; _dAiSkip.Clear(); _dDefEx.Clear(); DefPrune(); }
        private static bool DefDayAny() { return _dFights + _dSurr + _dOrphans + _dAiBurnNoted + _dAiSkip.Count > 0 || _fight != null; }

        private static string DefDayText()
        {
            return "obroncy wiosek: starcia " + (_dFights + _dSurr) + " (poddania " + _dSurr + ", bitwy " + _dFights + ": wygrane gracza " + _dWon + ", przegrane / odwrot " + _dLostFights
                   + "), straty z ludnosci " + _dFallen.ToString("0", Inv) + " (brak rak wiosek +" + _dLack.ToString("0.0", Inv) + "), rozbici do domow " + _dRouted.ToString("0", Inv) + ", druzyny sprzatniete po wczytaniu " + _dOrphans + (_fight != null ? ", bitwa w toku " + _fight.Name : "")
                   + " | AI: wiosek pominietych (obroncow wiecej niz napastnikow) " + _dAiSkip.Count + ", spalonych z ubytkiem obroncow " + _dAiBurnNoted + " (ubytek " + _dAiFallen.ToString("0.0", Inv) + ")"
                   + " | pamiec poleglych " + _fallen.Count + " wiosek, brak rak " + _lack.Count + " wiosek (razem " + LackSum().ToString("0.0", Inv) + ")" + (_dDefEx.Count > 0 ? " | przyklady: " + string.Join("; ", _dDefEx.ToArray()) : "");
        }

        private static string DefSessionText()
        {
            return "obroncy wiosek: klucz Side Village Defenders " + (DefOn ? "TAK" : "WYL") + " (1 na " + DefPeoplePer.ToString("0", Inv) + " mieszkancow w domu, pulap " + DefCap + ", poddanie przy "
                   + SurrenderRatio.ToString("0", Inv) + ":1, sklad " + (100 * (1 - MilitiaShare)).ToString("0", Inv) + "% chlopi / " + (100 * MilitiaShare).ToString("0", Inv) + "% milicja, pamiec poleglych "
                   + FallenDays + " dob, wpisow " + _fallen.Count + ", brak rak wiosek " + _lack.Count + " (pobor tylko z DOM wsi okregu), bitwa w toku " + (_fight != null ? _fight.Name + " (" + (_fight.PartyId ?? "-") + ")" : "nie") + ")";
        }

        // ------------------------------------------------------------ liczba obroncow
        /// <summary>Pamiec poleglych wioski teraz (liniowo do 0 przez FallenDays dob).</summary>
        private static double Fallen(string uid)
        {
            double[] e;
            if (uid == null || !_fallen.TryGetValue(uid, out e) || e == null) return 0.0;
            int today = Today0();
            double age = today > 0 ? Math.Max(0.0, today - e[1]) : 0.0;
            return Math.Max(0.0, e[0] * (1.0 - age / FallenDays));
        }

        private static void AddFallen(string uid, double n)
        {
            if (uid == null || !(n > 0.0)) return;
            double now = Fallen(uid) + n;
            _fallen[uid] = new[] { now, (double)Today0() };
        }

        /// <summary>VillageBook.LackOf: jest jakis brak rak (bez bitew - zero kosztu w ksiedze).</summary>
        internal static bool LackAny { get { return _lack.Count > 0; } }

        /// <summary>Brak rak wioski uid teraz (ludzie; liniowo do 0 przez FallenDays dob). Wola VillageBook pod swoim Gate - sam odczyt, nie rzuca.</summary>
        internal static double LackNow(string uid)
        {
            try
            {
                double[] e;
                if (uid == null || !_lack.TryGetValue(uid, out e) || e == null) return 0.0;
                int today = Today0();
                double age = today > 0 ? Math.Max(0.0, today - e[1]) : 0.0;
                return Math.Max(0.0, e[0] * (1.0 - age / FallenDays));
            }
            catch { return 0.0; }
        }

        private static void AddLack(string uid, double n)
        {
            if (uid == null || !(n > 0.0) || double.IsInfinity(n)) return;
            _lack[uid] = new[] { LackNow(uid) + n, (double)Today0() };
        }

        private static double LackSum() { double t = 0.0; foreach (var k in _lack.Keys) t += LackNow(k); return t; }

        /// <summary>Doba: wpisy pamieci poleglych i braku rak, ktore juz wrocily do zera, wylatuja (zapis i tak je pomija).</summary>
        private static void DefPrune()
        {
            List<string> gone = null;
            foreach (var k in _lack.Keys) if (LackNow(k) < 0.01) (gone ?? (gone = new List<string>())).Add(k);
            if (gone != null) foreach (var k in gone) _lack.Remove(k);
            gone = null;
            foreach (var k in _fallen.Keys) if (Fallen(k) < 0.01) (gone ?? (gone = new List<string>())).Add(k);
            if (gone != null) foreach (var k in gone) _fallen.Remove(k);
        }

        /// <summary>Udzial wioski uid w stojacych osadach okregu (osady - puste) z ksiegi - ta sama miara, ktora ksiega dzieli DOM okregu; -1 - ksiega jej nie zna.</summary>
        private static double StandShareOf(Settlement st, string uid)
        {
            if (st == null || st.Village == null || uid == null) return -1.0;
            var pv = new List<VillageBook.PartView>();
            if (!VillageBook.PartsOf(st.Village, pv)) return -1.0;
            double all = 0.0, mine = -1.0;
            foreach (var p in pv)
            {
                if (p == null) continue;
                double h = Math.Max(0.0, p.Nw - p.Br - p.Bf);
                all += h;
                if (!p.Main && string.Equals(p.Uid, uid, StringComparison.Ordinal)) mine = h;
            }
            return mine >= 0.0 && all > 1e-9 ? Math.Min(1.0, mine / all) : -1.0;
        }

        /// <summary>Pulap ludnosci: 1 na 10 mieszkancow w domu, najwyzej 60.</summary>
        private static int DefCapOf(double home) { return Math.Max(0, Math.Min(DefCap, (int)Math.Floor(Math.Max(0.0, home) / DefPeoplePer))); }

        /// <summary>Obroncy wioski uid przy home ludziach w domu: pulap ludnosci minus pamiec poleglych. Takze ksiega wiosek (lancuch AI) - sam odczyt, nie rzuca.</summary>
        internal static int DefendersFor(string uid, double home)
        {
            try { return Math.Max(0, DefCapOf(home) - (int)Math.Round(Fallen(uid))); }
            catch { return 0; }
        }

        /// <summary>Zdrowi ludzie partii i partii jej armii (gdy prowadzi) - sila napastnika w lancuchu AI.</summary>
        internal static int PartyMen(MobileParty mp)
        {
            if (mp == null) return 0;
            int n = Healthy(mp);
            try
            {
                var a = mp.Army; var ap = mp.AttachedParties;
                if (a != null && a.LeaderParty == mp && ap != null) for (int i = 0; i < ap.Count; i++) if (ap[i] != null && ap[i] != mp) n += Healthy(ap[i]);
            }
            catch { }
            return n;
        }

        // ------------------------------------------------------------ ksiega wiosek (lancuch AI)
        /// <summary>VillageBook.Guarded: lancuch pominal wioske (obroncy &gt; napastnicy) - licznik doby (wioski bez powtorzen).</summary>
        internal static void NoteChainSkip(string uid) { if (uid != null) _dAiSkip.Add(uid); }

        /// <summary>VillageBook.Burn: lancuch zaczal palic wioske uid (raz na sesje rabunku) - obroncy traca d x min(1, d / napastnicy) do pamieci poleglych.</summary>
        internal static void NoteChainBurn(string uid, double home, int men)
        {
            try
            {
                if (!DefOn || uid == null || men <= 0) return;
                int d = DefendersFor(uid, home);
                if (d <= 0) return;
                double lost = d * Math.Min(1.0, d / (double)men);
                AddFallen(uid, lost);
                _dAiBurnNoted++; _dAiFallen += lost;
            }
            catch (Exception e) { Stumble("NoteChainBurn", e); }
        }

        // ------------------------------------------------------------ menu: tekst i decyzja
        /// <summary>Tekst menu wrogich akcji przy obroncach wioski (zamiast milicji wsi gry).</summary>
        private static void DefHostileText(StringBuilder sb, MapVillageRecord r, VillageBook.PartView me, int def)
        {
            int men = MenNow();
            double home = me != null ? me.Home : 0.0;
            sb.Append("The people of ").Append(NameOf(r)).Append(" watch you from behind their doors.");
            if (def <= 0) { sb.Append(" There is nobody left here to fight you. You have ").Append(men).Append(" fit men."); return; }
            sb.Append(" About ").Append(def).Append(def == 1 ? " man" : " men").Append(" of its ").Append(Math.Round(home).ToString("N0", Inv)).Append(" people would take up flails, spears and bows to defend it");
            if (Fallen(_uid) >= 0.5) sb.Append(" (fewer than before - it has buried and nursed its men of late)");
            sb.Append(". You have ").Append(men).Append(" fit men.");
            if (men >= SurrenderRatio * def) sb.Append(" They are too few to stand against you - they would give in without a fight.");
            else sb.Append(" They will come out and fight you by the village.");
        }

        /// <summary>Dymek opcji przy obroncach (rabunek / okup): kto wyjdzie do walki albo poddanie.</summary>
        private static string DefTip(int def, int men)
        {
            if (def <= 0) return "Nobody is left here to fight you.";
            if (men >= SurrenderRatio * def) return "Its " + def + " men will not fight your " + men + " - they give in at once.";
            return "Its " + def + " men will come out and fight you by the village (you have " + men + ") - beat them first.";
        }

        private static bool Surrenders(int def, int men) { return def <= 0 || men >= SurrenderRatio * def; }

        private static void LogSurrender(MapVillageRecord r, Settlement st, VillageBook.PartView me, int def, int men, int kind)
        {
            _dSurr++;
            double home = me != null ? me.Home : 0.0;
            Log.Info(P + "obroncy " + NameOf(r) + " (" + _uid + ", okreg " + st.StringId + "): ludnosc " + home.ToString("0", Inv) + ", obroncow " + def + " (pulap ludnosci " + DefCapOf(home)
                     + ", pamiec poleglych " + Fallen(_uid).ToString("0.0", Inv) + "), sila gracza " + men + " -> poddanie (" + (kind == KRaid ? "rabunek" : "okup") + ") | wynik: bez walki | straty z ludnosci 0.");
            if (def > 0) Log.Player("The villagers of " + NameOf(r) + " see your " + men + " men and lay down their tools - they will not fight.");
            if (_dDefEx.Count < 3) _dDefEx.Add(NameOf(r) + " poddanie " + def + ":" + men);
        }

        // ------------------------------------------------------------ bitwa
        /// <summary>Druzyna obroncow przy wiosce i spotkanie gry (menu "encounter"). Wolane z opcji ZWYKLEGO menu wrogich akcji (SwitchToMenu wolno). false - nie udalo sie
        /// (komunikat; nic nie zostaje: druzyna rozwiazana, ludzie z powrotem); nobody - DOM wsi okregu nie dal nikogo (wolajacy idzie dalej jak przy poddaniu).</summary>
        private static bool StartFight(Settlement st, MapVillageRecord r, VillageBook.PartView me, int def, int men, int kind, out bool nobody)
        {
            nobody = false;
            var mp = MobileParty.MainParty;
            if (mp == null || st == null || r == null || def <= 0) return false;
            var clan = st.OwnerClan;
            var cul = st.Culture ?? (clan != null ? clan.Culture : null);
            CharacterObject vil = cul != null ? cul.Villager : null, mel = cul != null ? cul.MeleeMilitiaTroop : null, rng = cul != null ? cul.RangedMilitiaTroop : null;
            if (vil == null) vil = mel ?? rng;
            if (clan == null || vil == null) { Log.Info(P + "obroncy " + NameOf(r) + ": bez rodu pana albo szablonu chlopa kultury - bez bitwy (rabunek niemozliwy)."); return false; }
            // recenzja: polowki w gore (Math.Round domyslnie do parzystej: 15 x 0.3 = 4.5 -> 4); 15 obroncow = 10 chlopow, 3 wlocznikow, 2 lucznikow
            int nm = (int)Math.Round(def * MilitiaShare, MidpointRounding.AwayFromZero);
            int nr = mel != null && rng != null ? (int)Math.Round(nm / 3.0, MidpointRounding.AwayFromZero) : 0, ns = mel != null ? nm - nr : 0;
            if (mel == null && rng != null) { nr = nm; ns = 0; }
            if (mel == null && rng == null) { nr = ns = 0; }
            int nv = def - ns - nr;
            var roster = TroopRoster.CreateDummyTroopRoster();
            if (nv > 0) roster.AddToCounts(vil, nv);
            if (ns > 0) roster.AddToCounts(mel, ns);
            if (nr > 0) roster.AddToCounts(rng, nr);
            MobileParty party = null;
            var f = new Fight { Uid = _uid, Dist = st.StringId, Name = NameOf(r), Kind = kind, Def = def, Men = men, Day = Today() };
            try
            {
                party = CustomPartyComponent.CreateCustomPartyWithTroopRoster(new CampaignVec2(mp.GetPosition2D, true), 0.3f, st, new TextObject("{=!}" + DefNamePre + NameOf(r)), clan,
                                                                               roster, TroopRoster.CreateDummyTroopRoster(), null, "", "", 0f, true);
                if (party == null) { Log.Info(P + "obroncy " + NameOf(r) + ": druzyna nie powstala - bez bitwy."); return false; }
                f.Party = party; f.PartyId = party.StringId;
                try { party.IgnoreByOtherPartiesTill(CampaignTime.Never); } catch { }
                try { party.Ai.SetDoNotMakeNewDecisions(true); party.SetMoveModeHold(); } catch { }
                // ludzie TEJ wioski: tylko DOM wsi okregu (wioska jest jego czescia), ta sama droga co przyrost milicji (DOM -> sluzba z pochodzeniem); bez innych wsi
                // regionu (recenzja: TakeLevy siegal do FromVillages - ludzie z innych okregow); czego DOM nie dal - mniej obroncow (nikt z niczego)
                if (PeopleBook.LevyLive)
                {
                    Settlement region;
                    double got = PeopleBook.TakeLevyHere(st, def, PeopleBook.MLevyMilitia, out region);
                    ArmsFromPeople.NoteVillageDefenders(got);   // KONTROLA E4/8: ruch MLevyMilitia z DOM = m1 + ... + ob
                    int keep = Math.Min(def, (int)Math.Ceiling(got - 1e-6));
                    if (keep < def) Strip(party.MemberRoster, def - keep, vil, mel, rng);
                    f.Levy = got > 1e-9; f.Got = got;
                    if (got > 1e-9) PeopleBook.GiveNew(party.MemberRoster, region, got, PeopleBook.FMilitiaLevy);
                    if (keep - got > 1e-6) PeopleBook.GiveNone(party.MemberRoster, keep - got, PeopleBook.FArmsFloor);
                    f.Def = keep;
                }
                if (f.Def <= 0)
                {
                    Disband(f, "DOM wsi okregu na podlodze (pobor 0)");
                    nobody = true;
                    Log.Info(P + "obroncy " + f.Name + ": DOM wsi okregu " + st.StringId + " na podlodze - nikt nie wychodzi do walki (jak poddanie).");
                    return false;
                }
                _fight = f;
                _dFights++;
                Log.Info(P + "obroncy " + f.Name + " (" + f.Uid + ", okreg " + st.StringId + "): ludnosc " + (me != null ? me.Home : 0.0).ToString("0", Inv) + ", obroncow " + f.Def + " (pulap ludnosci "
                         + DefCapOf(me != null ? me.Home : 0.0) + ", pamiec poleglych " + Fallen(f.Uid).ToString("0.0", Inv) + "; chlopi " + Math.Max(0, f.Def - ns - nr) + ", wlocznicy " + ns + ", lucznicy " + nr
                         + "), sila gracza " + men + " -> bitwa (" + (kind == KRaid ? "rabunek" : "okup") + ", druzyna " + f.PartyId + ", z DOM wsi okregu " + f.Got.ToString("0.00", Inv) + ").");
                RoadsideInnStay.Leave("bitwa z obroncami wioski pobocznej");
                PlayerEncounter.RestartPlayerEncounter(party.Party, PartyBase.MainParty, false);
                GameMenu.SwitchToMenu("encounter");   // zwykle menu wrogich akcji -> spotkanie gry (jak VillageHostileActionCampaignBehavior.StartHostileAction)
                return true;
            }
            catch (Exception e)
            {
                Stumble("StartFight", e);
                try { if (_fight == null) Disband(f, "wyjatek przy starcie"); else if (PlayerEncounter.Current == null) FinishFight("wyjatek przy starcie"); } catch { }
                return false;
            }
        }

        /// <summary>Zdejmuje n ludzi ze skladu (najpierw chlopi, potem wlocznicy, lucznicy).</summary>
        private static void Strip(TroopRoster r, int n, params CharacterObject[] order)
        {
            foreach (var c in order)
            {
                if (n <= 0 || c == null) continue;
                int i = r.FindIndexOfTroop(c);
                int have = i >= 0 ? r.GetElementNumber(i) : 0;
                int t = Math.Min(n, have);
                if (t > 0) { r.AddToCounts(c, -t); n -= t; }
            }
        }

        /// <summary>Ocaleni do domow (pochodzenie: ArriveAt MBackMilitia - jak milicja) i druzyna rozwiazana bez sprawcy z pustym skladem. Zwraca ilu wrocilo.</summary>
        private static int Disband(Fight f, string why)
        {
            int left = 0;
            var p = f != null ? f.Party : null;
            try
            {
                if (p == null || !p.IsActive) return 0;
                var r = p.MemberRoster;
                left = r != null ? r.TotalManCount : 0;
                var st = Settlement.Find(f.Dist);
                if (left > 0 && f.Levy && PeopleBook.OriginLive && st != null)
                {
                    var sh = PeopleBook.Take(r, left, PeopleBook.FMilitiaHome);
                    PeopleBook.ArriveAt(st, sh, PeopleBook.AService, PeopleBook.MBackMilitia, PeopleBook.MBackMilitiaNone, false);
                }
                if (r != null) r.Clear();
                try { FreePrisoners(p); } catch (Exception e) { Stumble("Disband(jency)", e); }
                DestroyPartyAction.Apply(null, p);
            }
            catch (Exception e) { Stumble("Disband(" + why + ")", e); }
            return left;
        }

        /// <summary>Jency druzyny obroncow (ludzie gracza wzieci po jego przegranej): bohaterowie uciekaja (jak FreeFolkRaids.Home), szeregowych wioska wypuszcza - wracaja do
        /// druzyny gracza jako ranni (udzialy pochodzenia z lochu do skladu, FTransfer); gracz w niewoli albo nieczynny - zostaja w lochu i znikaja z druzyna (jak w grze).</summary>
        private static void FreePrisoners(MobileParty p)
        {
            var pr = p != null ? p.PrisonRoster : null;
            if (pr == null || pr.Count == 0) return;
            for (int i = pr.Count - 1; i >= 0; i--)
            {
                var e = pr.GetElementCopyAtIndex(i);
                if (e.Character != null && e.Character.IsHero && e.Character.HeroObject != null && e.Character.HeroObject != Hero.MainHero)
                {
                    try { EndCaptivityAction.ApplyByEscape(e.Character.HeroObject, null, false); } catch (Exception ex) { Stumble("FreePrisoners(bohater)", ex); }
                }
            }
            var main = MobileParty.MainParty;
            if (main == null || !main.IsActive || PlayerCaptivity.IsCaptive) { pr.Clear(); return; }
            int n = 0;
            var back = new List<TroopRosterElement>();
            for (int i = 0; i < pr.Count; i++)
            {
                var e = pr.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                back.Add(e); n += e.Number;
            }
            if (n <= 0) { pr.Clear(); return; }
            var sh = PeopleBook.Take(pr, n, PeopleBook.FTransfer);
            foreach (var e in back) main.MemberRoster.AddToCounts(e.Character, e.Number, false, e.Number);
            if (sh != null) PeopleBook.Give(main.MemberRoster, sh, PeopleBook.FTransfer);
            pr.Clear();
            Log.Info(P + "obroncy: jency druzyny obroncow wypuszczeni - " + n + " ludzi gracza wraca do jego druzyny jako ranni.");
            Log.Player("The villagers let your " + n + " captured men go - they limp back to your camp.");
        }

        /// <summary>MapEventEnded: bitwa z druzyna obroncow - zwyciezca (1 gracz, 2 nie gracz albo bez zwyciezcy). Sam zapis wyniku; reszta w straznku po spotkaniu.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            var f = _fight;
            if (f == null || me == null || f.Party == null) return;
            try
            {
                bool mine = false;
                foreach (var p in me.InvolvedParties) if (p != null && ReferenceEquals(p, f.Party.Party)) { mine = true; break; }
                if (!mine) return;
                bool won = me.WinningSide != BattleSideEnum.None && me.WinningSide == me.PlayerSide && me.PlayerSide != BattleSideEnum.None;
                f.Won = won ? 1 : 2;
                // recenzja: straty z wyniku bitwy, nie z "obroncy - ocaleni" (przy "Send troops" H3 odsyla rozbitych do domow, a gra kasuje pusta druzyne). Ten nasluch
                // jest zapisany przed OutlawLaw.OnMapEventEnded (ArmouryBehavior) - rozbici jeszcze w RoutedInBattle
                try
                {
                    foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                    {
                        if (side == null || side.Parties == null) continue;
                        foreach (var mep in side.Parties)
                        {
                            if (mep == null || !ReferenceEquals(mep.Party, f.Party.Party)) continue;
                            f.Dead = mep.DiedInBattle != null ? mep.DiedInBattle.TotalManCount : 0;
                            f.Routed = mep.RoutedInBattle != null ? mep.RoutedInBattle.TotalManCount : 0;
                        }
                    }
                }
                catch (Exception e) { Stumble("OnMapEventEnded(straty)", e); }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        /// <summary>Straznik (Tick): spotkanie skonczone (druzyna poza bitwa i bez spotkania gracza) - koniec bitwy: ocaleni do domow, straty, dalszy ciag przy wygranej.</summary>
        private static void FightGuard()
        {
            var f = _fight;
            if (f == null) return;
            var p = f.Party;
            if (p == null && f.PartyId != null) { p = FindParty(f.PartyId); f.Party = p; }
            bool active = p != null && p.IsActive;
            if (active && p.MapEvent != null) return;          // bitwa trwa
            if (PlayerEncounter.Current != null) return;        // menu spotkania, ekrany po bitwie
            // gracz przegral i siedzi w lochu tej druzyny - druzyna zostaje do konca niewoli (ucieczka / wykup gry); dopiero potem rozwiazana
            try { if (active && Hero.MainHero != null && Hero.MainHero.PartyBelongedToAsPrisoner == p.Party) return; } catch { }
            FinishFight(active ? "spotkanie skonczone" : "druzyna rozbita w bitwie");
        }

        private static MobileParty FindParty(string id)
        {
            if (id == null) return null;
            foreach (var mp in MobileParty.All) if (mp != null && string.Equals(mp.StringId, id, StringComparison.Ordinal)) return mp;
            return null;
        }

        private static void FinishFight(string how)
        {
            var f = _fight;
            _fight = null;
            if (f == null) return;
            try
            {
                if (f.Name == null || f.Name == "?") f.Name = NameOf(Rec(f.Uid));   // po wczytaniu (ksiega czytana pozniej niz zapis)
                int left = Disband(f, how);
                int routed = Math.Max(0, Math.Min(f.Routed, f.Def - left));
                int lost = Math.Max(0, f.Def - left - routed);   // polegli i pojmani; rozbici wracaja do domow droga H3 (OutlawLaw / LosersFlee.SendHome)
                // ludzie TEJ wioski: z DOM wsi okregu ubylo lost, ksiega rozlozylaby to na caly okreg - brak rak przesuwa udzial reszty okregu na te wioske
                double sh = StandShareOf(Settlement.Find(f.Dist), f.Uid), lack = 0.0;
                if (lost > 0 && f.Levy && sh >= 0.0) { lack = lost * (1.0 - sh); AddLack(f.Uid, lack); }
                AddFallen(f.Uid, lost * FallenFighters);
                _dFallen += lost; _dRouted += routed; _dLack += lack;
                bool won = f.Won == 1;
                if (won) _dWon++; else _dLostFights++;
                var r = Rec(f.Uid); var st = Settlement.Find(f.Dist);
                if (st != null && (st.Village == null || !st.IsVillage)) st = null;
                string res = won ? "gracz wygral" : (f.Won == 2 ? "gracz nie wygral (przegrana, odwrot albo Leave)" : "bez wyniku bitwy (" + how + ")");
                Log.Info(P + "obroncy " + f.Name + " (" + f.Uid + ", okreg " + f.Dist + "): koniec bitwy - " + how + " | obroncow " + f.Def + ", wrocilo do domow " + left + ", rozbici do domow (H3) "
                         + routed + ", straty z ludnosci " + lost + " (polegli i pojmani; zabici w bitwie " + Math.Max(0, f.Dead) + ") | wynik: " + res + " | pamiec poleglych " + Fallen(f.Uid).ToString("0.0", Inv)
                         + ", brak rak wioski +" + lack.ToString("0.0", Inv) + " (udzial w osadach okregu " + (sh >= 0.0 ? (100 * sh).ToString("0", Inv) + "%" : "-") + ", razem " + LackNow(f.Uid).ToString("0.0", Inv) + ").");
                if (_dDefEx.Count < 3) _dDefEx.Add(f.Name + " bitwa " + f.Def + ":" + f.Men + " " + (won ? "W" : "P") + " -" + lost);
                var mp = MobileParty.MainParty;
                bool alive = mp != null && mp.IsActive && Hero.MainHero != null && Hero.MainHero.IsAlive && !PlayerCaptivity.IsCaptive;
                if (!won)
                {
                    if (alive) Log.Player("The villagers of " + f.Name + " held their homes - the village is spared.", true);
                    return;
                }
                if (!alive || st == null || r == null) return;
                var c = Campaign.Current;
                if (c == null || c.CurrentMenuContext != null || PlayerEncounter.Current != null || mp.MapEvent != null || mp.CurrentSettlement != null)
                {
                    Log.Info(P + "obroncy " + f.Name + ": wygrana, ale gracz nie stoi przy wiosce na mapie (menu albo osada) - bez dalszego ciagu.");
                    Log.Player("You beat the villagers of " + f.Name + ".");
                    return;
                }
                var pos = mp.GetPosition2D;
                if (pos.Distance(new Vec2(r.X, r.Y)) > 4.0f)
                {
                    Log.Info(P + "obroncy " + f.Name + ": wygrana, ale gracz " + pos.Distance(new Vec2(r.X, r.Y)).ToString("0.0", Inv) + " jedn. od wioski - bez dalszego ciagu.");
                    Log.Player("You beat the villagers of " + f.Name + ".");
                    return;
                }
                _uid = f.Uid;
                PlaceInn(f.Uid, pos.x, pos.y);
                VillageBook.PartView me; double homeAll, nAll;
                PartOf(st, f.Uid, out me, out homeAll, out nAll);
                if (f.Kind == KRaid)
                {
                    Log.Player("You beat the villagers of " + f.Name + " - your men go into the village.");
                    if (BeginRaid(st, r, me, nAll, "po bitwie z obroncami")) GameMenu.ActivateGameMenu(RaidMenu);   // z ticku na mapie (bez menu) - wolno
                    else GameMenu.ActivateGameMenu(Menu);
                }
                else
                {
                    PayTribute(st, r, me, homeAll, 5.0, "po bitwie z obroncami");
                    GameMenu.ActivateGameMenu(Menu);
                }
            }
            catch (Exception e) { Stumble("FinishFight", e); }
        }

        /// <summary>Po wczytaniu (pierwszy tick sesji): druzyny obroncow bez wpisu bitwy - sklad do domow, rozwiazanie. Druzyna z wpisu - odnaleziona dla straznika.</summary>
        internal static void SweepOrphans()
        {
            try
            {
                var keep = _fight != null ? _fight.PartyId : null;
                var dead = new List<MobileParty>();
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !(mp.PartyComponent is CustomPartyComponent)) continue;
                    if (keep != null && string.Equals(mp.StringId, keep, StringComparison.Ordinal)) { _fight.Party = mp; continue; }
                    string nm = Txt(mp.Name);
                    if (nm == null || !nm.StartsWith(DefNamePre, StringComparison.Ordinal) || mp.HomeSettlement == null || !mp.HomeSettlement.IsVillage) continue;
                    if (mp.MapEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounteredParty == mp.Party)) continue;
                    dead.Add(mp);
                }
                foreach (var mp in dead)
                {
                    var f = new Fight { Party = mp, PartyId = mp.StringId, Dist = mp.HomeSettlement.StringId, Name = Txt(mp.Name), Levy = true };
                    int left = Disband(f, "sierota po wczytaniu");
                    _dOrphans++;
                    Log.Info(P + "obroncy: druzyna " + f.PartyId + " (" + f.Name + ") bez wpisu bitwy po wczytaniu - rozwiazana, do domow " + left + ".");
                }
                if (_fight != null && _fight.Party == null)
                {
                    Log.Info(P + "obroncy: wpis bitwy " + _fight.Name + " bez druzyny " + (_fight.PartyId ?? "-") + " w swiecie - zamkniety bez strat.");
                    _fight.Def = 0; _fight = null;
                }
            }
            catch (Exception e) { Stumble("SweepOrphans", e); }
        }

        // ------------------------------------------------------------ zapis: "|f=uid,partia,rodzaj,obroncy,ludzie,doba,wynik,levy,got,okreg,rozbici", "|d=uid,ilu,doba;..." i "|k=uid,ilu,doba;..."
        private static void DefExport(StringBuilder sb)
        {
            bool first = true;
            foreach (var kv in _fallen)
            {
                if (kv.Value == null || Fallen(kv.Key) < 0.05) continue;
                if (first) { sb.Append("|d="); first = false; } else sb.Append(';');
                sb.Append(kv.Key).Append(',').Append(kv.Value[0].ToString("R", Inv)).Append(',').Append(((int)kv.Value[1]).ToString(Inv));
            }
            first = true;
            foreach (var kv in _lack)
            {
                if (kv.Value == null || LackNow(kv.Key) < 0.05) continue;
                if (first) { sb.Append("|k="); first = false; } else sb.Append(';');
                sb.Append(kv.Key).Append(',').Append(kv.Value[0].ToString("R", Inv)).Append(',').Append(((int)kv.Value[1]).ToString(Inv));
            }
            var f = _fight;
            if (f != null && f.PartyId != null)
                sb.Append("|f=").Append(f.Uid).Append(',').Append(f.PartyId).Append(',').Append(f.Kind.ToString(Inv)).Append(',').Append(f.Def.ToString(Inv)).Append(',').Append(f.Men.ToString(Inv))
                  .Append(',').Append(f.Day.ToString(Inv)).Append(',').Append(f.Won.ToString(Inv)).Append(',').Append(f.Levy ? "1" : "0").Append(',').Append(f.Got.ToString("R", Inv)).Append(',').Append(f.Dist ?? "").Append(',').Append(f.Routed.ToString(Inv));
        }

        /// <summary>Sekcje d=, k= i f= z zapisu (Import). false - to nie nasza sekcja.</summary>
        private static bool DefImport(string sec, ref int bad)
        {
            if (sec.StartsWith("d=", StringComparison.Ordinal) || sec.StartsWith("k=", StringComparison.Ordinal))
            {
                var into = sec[0] == 'k' ? _lack : _fallen;
                foreach (var e in sec.Substring(2).Split(';'))
                {
                    if (e.Length == 0) continue;
                    var a = e.Split(',');
                    double n; int day;
                    if (a.Length < 3 || a[0].Length == 0 || !double.TryParse(a[1], NumberStyles.Float, Inv, out n) || !int.TryParse(a[2], NumberStyles.Integer, Inv, out day)) { bad++; continue; }
                    into[a[0]] = new[] { Math.Max(0.0, n), (double)day };
                }
                return true;
            }
            if (sec.StartsWith("f=", StringComparison.Ordinal) && sec.Length > 2)
            {
                var a = sec.Substring(2).Split(',');
                int kind, def, men, day, won; double got;
                if (a.Length < 10 || !int.TryParse(a[2], NumberStyles.Integer, Inv, out kind) || !int.TryParse(a[3], NumberStyles.Integer, Inv, out def) || !int.TryParse(a[4], NumberStyles.Integer, Inv, out men)
                    || !int.TryParse(a[5], NumberStyles.Integer, Inv, out day) || !int.TryParse(a[6], NumberStyles.Integer, Inv, out won) || !double.TryParse(a[8], NumberStyles.Float, Inv, out got))
                { bad++; return true; }
                int routed;
                if (a.Length < 11 || !int.TryParse(a[10], NumberStyles.Integer, Inv, out routed)) routed = -1;   // wpis sprzed recenzji - bez rozbitych
                _fight = new Fight { Uid = a[0], PartyId = a[1], Kind = kind, Def = def, Men = men, Day = day, Won = won, Levy = a[7] == "1", Got = got, Dist = a[9].Length > 0 ? a[9] : null, Routed = routed };
                _fight.Name = NameOf(Rec(_fight.Uid));
                if (_fight.Dist == null) { var r = Rec(_fight.Uid); _fight.Dist = r != null ? r.VillageId : null; }
                _sweepPending = true;
                return true;
            }
            return false;
        }

        private static bool _sweepPending = true;   // pierwszy tick po wczytaniu / starcie - przeglad druzyn obroncow
    }
}
