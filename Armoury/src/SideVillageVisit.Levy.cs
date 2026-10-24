using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m113-pobor - POBOR Z WIOSKI POBOCZNEJ (Jeff 24.10: "pula ma byc z tej konkretnej wioski ... wioska gry to nie WIOSKA, tylko okreg wiejski: 4000 ludzi to duzo jak
    /// na wioske - to wioska z okolica, farmami i zagrodami. Z tej ludnosci maks - sprawdz historycznie - ok. 10%, np. 400 chlopa, mozna zwerbowac; potem pula odnawia sie
    /// tylko jak ludzie sie rodza (ile sie rodzi na miesiac itd.), a gospodarka na tym cierpi"). Klucz SideVillageRecruit; zapis arm_sidelevy.
    /// DOWOD (log Armoury-2026-10-24_01-21-43): wioski poboczne 10 302 472 ludzi w 1957 wioskach (srednio 5 264), wsie gry 23 979 944, wsie BK 986 949 w 571.
    /// Srednia wioska: pulap 10% = ok. 526 ludzi, ok. 4 chlopcow dorasta na miesiac (1% ludzi na rok), pelna pula wraca po ok. 10 latach.
    /// REGULY (liczby Claude'a):
    ///  P1 LUDZIE: P = me.Home + me.Away z PartOf (VillageBook.PartsOf) - ludzie TEJ wioski w ksiedze wiosek. Pulap cap = SideRecruitPoolPercent (10) / 100 x P.
    ///  P2 WPIS LvRec {Uid, Pool, P, Grown, Day, Stage, Today, Total}; brak wpisu = pula pelna (Pool = cap). Wpis powstaje przy pierwszym werbunku albo pierwszej stracie
    ///     obroncow (Pool = cap, Grown = dzis).
    ///  P3 ROZLICZENIE (Settle): m = (dzis - Grown) / 28 pelnych miesiecy; Pool = min(cap, Pool + m x P x SideRecruitYouthPercent/100 x 28/dni roku), Grown += 28m
    ///     (dni roku = max(28, CampaignTime.DaysInYear) - 364 w kalendarzu Armoury); urodzenia od mniejszego z P zapisanego i P dzis - wzrost P nigdy nie dodaje do puli.
    ///     Pool = min(Pool, cap dzis), P = P dzis. Nowa doba: Today = 0, etap z f = Pool/cap (f > 0.6 - 1 ochotnicy, f > 0.3 - 2 pobor, inaczej 3 pospolite ruszenie);
    ///     kraina z hamulcem wolnych rak (FreeHands.Braked) - etap co najmniej 2. Etap stoi przez cala dobe (cena nie skacze w trakcie dnia).
    ///  P4 ZAUFANIE idx 0-6: wlasna wioska gracza albo ziemia rodu gracza - 6; inaczej najwyzszy VolunteerModel.MaximumIndexHeroCanRecruitFromHero(gracz, notabl) po
    ///     notablach wsi okregu (zywi, na miejscu) - z BK to CalculateMaximumRecruitmentIndex (relacja, prawa poboru); okreg bez notabli - 2.
    ///  P5 DZIENNY NAPLYW: muster = idx &lt;= 0 ? 0 : ceil(cap x SideRecruitDailyPercent (3) / 100 x idx/6 x F[etap]), F = 1 / 0.5 / 0.25. Callable = floor(Pool x
    ///     min(1, Home/P)) - ludzie poza domem (uciekinierzy) nie przyjda; ready = max(0, min(Callable, muster - Today)). Cena = koszt rekruta gry dla gracza x M[etap],
    ///     M = 1 / SideRecruitLevyPrice (1.5) / SideRecruitGeneralLevyPrice (2.0) - gdy chetni odeszli, rodziny chca wiecej.
    ///  P6 RODZAJ: notabl-soltys wsi okregu (albo pierwszy zywy notabl z CanHaveRecruits) - VolunteerModel.GetBasicVolunteer; pula BK osady &lt; 1 albo brak notabla - BasicTroop
    ///     kultury osady. Bohater, tier &gt; 1 albo konny - BasicTroop (z jednej wioski ida chlopi, nie wyszkoleni). Brak BasicTroop - opcja ukryta. Oferta (wioska, doba)
    ///     tylko w pamieci.
    ///  P7 BEZ PODWOJNEGO LICZENIA: werbunek gry, pula BK, VolunteerTypes, RecruitKit, VolunteerKit i szanse Levy/PeopleLevy bez zmian; BEZ DeduceManpower. Kazdy czlowiek
    ///     schodzi z DOM okregu dokladnie raz (TakeLevy dla rekrutow notabla - tu TakeLevyHere). Ksiega rozklada rekruta notabla na wszystkie osady okregu (ok. 0.3% ludzi
    ///     na rok) - to moze tylko obnizyc pulap wioski pobocznej, nikogo nie liczy drugi raz.
    ///  P8 BRAK RAK: LevyAddLack(uid, zdjeci x (1 - udzial wioski w stojacych osadach okregu)) - jak brak rak obroncow (VillageBook.LackOf przesuwa udzial reszty okregu na te
    ///     wioske); wraca liniowo przez SideRecruitResettleDays (364): zagrody zasiedla reszta okregu. Osobny magazyn kopiowany przy zapisie (czytany pod Gate ksiegi).
    ///     Klucz wylaczony - brak rak 0.
    ///  P9 OBRONCY - jedna regula: DefendersFor = max(0, min(pulap obroncow - polegli, LevyCallableFor)). LevyCallableFor czyta tylko wpis (bez PartsOf, nie rzuca): brak wpisu
    ///     - bez limitu, inaczej floor(Pool x min(1, home/P)). LevyNoteLost (FinishFight, NoteChainBurn) obniza pule - polegli obroncy to ci sami mlodzi.
    ///  P10 AI: tylko gracz - AI nie wjezdza do wiosek pobocznych (werbuje u notabli wsi gry jak dotad).
    ///  P11 WARUNKI (kolejnosc, kazdy wylacza opcje z powodem; ukryta tylko przy wylaczonym kluczu): klucz, ksiega pochodzenia (LevyLive), sluzba u lorda (ROT enlisted),
    ///     Straz (partia Strazy albo kultura nightswatch), wioska nieznana, wojna, rabunek okregu, spalona, przy wiosce, zaufanie, pula, koniec dnia, miejsce w druzynie, zloto.
    ///  WERBUNEK (LevyDo): DOM -k (TakeLevyHere MLevyVillage - przed pieniedzmi), ludzie do druzyny (AddToCounts), zdarzenie OnUnitRecruited (XP przywodztwa gry, PeopleLedger),
    ///     komplet DTE tieru 1 (PlayerRecruit.SideVillageKit - jak u AI), pochodzenie GiveNew; zaplata tylko za przybylych - GiveGoldAction gracz -&gt; kiesa wsi okregu (zdarzenie
    ///     gry - ksiega pieniedzy widzi platnika i odbiorce). NIE OnTroopRecruited z graczem (DTE dodalby komplet wzorca drugi raz).
    /// ZRODLA:
    ///  - fyrd, 1 czlowiek z 5 hide: https://en.wikipedia.org/wiki/Hide_(unit)
    ///  - kapitularz 808 (1 czlowiek z 3-4 mansi): https://www.deremilitari.org/RESOURCES/SOURCES/charlemagne6.htm
    ///  - smiertelnosc dzieci: https://ourworldindata.org/child-mortality-in-the-past
    ///  - urodzenia: Clark, https://faculty.econ.ucdavis.edu/faculty/gclark/210a/readings/chapter9.pdf (Anglia ok. 35 na 1000 w 1750-1770) i Macfarlane,
    ///    https://alanmacfarlane.com/savage/A-FERT.PDF (Europa ok. 40 na 1000 przed 1750)
    ///  - mobilizacja 7.5-12%: https://mythicscribes.com/miscellaneous/size-of-armies-part-2/ (blog, szacunek)
    /// Zapis arm_sidelevy "sl1|p=uid,pool,grown,day,stage,today,total,P;...|k=uid,brak,doba;..." - tylko wpisy niepelne i braki &gt;= 0.01; brak klucza (stary zapis) = pule pelne;
    /// starsza DLL klucz gubi przy nastepnym zapisie (pule pelne, braki rak zniknely; ubytek DOM zostaje). Linie logu "Pobor w wioskach (m113): ...".
    /// </summary>
    internal static partial class SideVillageVisit
    {
        private const string LP = "Pobor w wioskach (m113): ";
        internal const string LevyOption = "arm_sidevil_recruit";
        private const int LvMonth = 28;
        private const int LvOfferMax = 40;
        private static readonly double[] LvF = { 1.0, 0.5, 0.25 };

        /// <summary>PeopleLedger.OnUnitRecruited: osada werbunku z wioski pobocznej (gracz zwykle na mapie - Settlement.CurrentSettlement null). Tylko na czas LevyDo.</summary>
        internal static Settlement RecruitingAt;

        private sealed class LvRec { public string Uid; public double Pool, P; public int Grown, Day = -1, Stage = 1, Today, Total; }

        // magazyny kopiowane przy zapisie (czytane takze pod Gate ksiegi wiosek): nowy slownik przy dodaniu / usunieciu wpisu, pola wpisu zmieniane w miejscu na watku glownym
        private static volatile Dictionary<string, LvRec> _lv = new Dictionary<string, LvRec>(StringComparer.Ordinal);
        private static volatile Dictionary<string, double[]> _lvLack = new Dictionary<string, double[]>(StringComparer.Ordinal);   // uid -> { ilu, doba }

        private sealed class LvQuote
        {
            public string Uid; public int Hour = -1, Day = -1;
            public bool Hide, Burned;
            public double P, Home, Cap, Pool, Y;
            public int Callable, Muster, Ready, Stage = 1, Idx, Price, Today;
            public CharacterObject Troop;
        }
        private static LvQuote _lvQ;
        private static readonly Dictionary<string, KeyValuePair<int, CharacterObject>> _lvOffer = new Dictionary<string, KeyValuePair<int, CharacterObject>>(StringComparer.Ordinal);

        // liczniki
        private static int _lvSt, _lvStDay;
        private static int _ldRec, _ldMen, _ldDue, _ldDrop, _ldLostN;
        private static long _ldGold;
        private static double _ldGot, _ldFloor, _ldLack, _ldGrown, _ldLost;
        private static long _ldTicks;
        private static readonly HashSet<string> _ldRefused = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _ldWhy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly List<string> _ldEx = new List<string>();

        // ------------------------------------------------------------ klucze
        internal static bool LevyKey { get { var s = Settings.Current; return s != null && s.SideVillageRecruit; } }
        private static double LvPoolPct { get { var s = Settings.Current; return s == null ? 10.0 : Clamp(s.SideRecruitPoolPercent, 1.0, 20.0); } }
        private static double LvYouthPct { get { var s = Settings.Current; return s == null ? 1.0 : Clamp(s.SideRecruitYouthPercent, 0.0, 3.0); } }
        private static double LvDailyPct { get { var s = Settings.Current; return s == null ? 3.0 : Clamp(s.SideRecruitDailyPercent, 0.5, 10.0); } }
        private static double LvLevyPrice { get { var s = Settings.Current; return s == null ? 1.5 : Clamp(s.SideRecruitLevyPrice, 1.0, 4.0); } }
        private static double LvGeneralPrice { get { var s = Settings.Current; return s == null ? 2.0 : Clamp(s.SideRecruitGeneralLevyPrice, 1.0, 5.0); } }
        private static int LvResettleDays { get { var s = Settings.Current; return s == null ? 364 : Math.Max(28, Math.Min(1092, s.SideRecruitResettleDays)); } }
        private static double Clamp(double v, double lo, double hi) { return double.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v)); }
        private static int LvYearDays() { try { return Math.Max(28, CampaignTime.DaysInYear); } catch { return 364; } }

        /// <summary>Opcja istnieje: klucz, ksiega wiosek czynna, a dla menu wioski pobocznej - takze klucz wjazdu.</summary>
        private static bool LevyOn(bool fief)
        {
            var s = Settings.Current;
            return s != null && s.SideVillageRecruit && VillageBook.Active && (fief || s.SideVillagesEnterable);
        }

        private static void LvStumble(string where, Exception e) { _lvSt++; _lvStDay++; Stumble("Levy." + where, e); }

        private static void LevyReset()
        {
            _lv = new Dictionary<string, LvRec>(StringComparer.Ordinal);
            _lvLack = new Dictionary<string, double[]>(StringComparer.Ordinal);
            _lvQ = null; _lvOffer.Clear(); RecruitingAt = null;
            _lvSt = 0;
            LevyDayZero();
            LevyProbeReset();
        }

        private static void LevyDayZero()
        {
            _ldRec = _ldMen = _ldDue = _ldDrop = _ldLostN = 0; _ldGold = 0; _ldGot = _ldFloor = _ldLack = _ldGrown = _ldLost = 0.0; _ldTicks = 0; _lvStDay = 0;
            _ldRefused.Clear(); _ldWhy.Clear(); _ldEx.Clear(); _lvOffer.Clear();
        }

        // ------------------------------------------------------------ wpisy
        private static bool UidOk(string uid) { return !string.IsNullOrEmpty(uid) && uid.IndexOfAny(new[] { ',', ';', '|', '=' }) < 0; }

        private static LvRec LvGet(string uid)
        {
            LvRec r; var d = _lv;
            return uid != null && d != null && d.TryGetValue(uid, out r) ? r : null;
        }

        private static bool LvPut(LvRec r)
        {
            if (r == null || !UidOk(r.Uid)) return false;
            var d = new Dictionary<string, LvRec>(_lv, StringComparer.Ordinal);
            d[r.Uid] = r;
            _lv = d;
            return true;
        }

        private static int StageOf(double f, bool braked)
        {
            int s = f > 0.6 ? 1 : (f > 0.3 ? 2 : 3);
            return braked ? Math.Max(2, s) : s;
        }

        /// <summary>P3: przyrost mlodych za pelne miesiace (urodzenia od pBirth), pulap cap; zwraca, ile przybylo.</summary>
        private static double LvGrow(LvRec r, double pBirth, double cap, int today)
        {
            int m = (today - r.Grown) / LvMonth;
            if (m <= 0) return 0.0;
            double before = r.Pool;
            double add = m * Math.Max(0.0, pBirth) * LvYouthPct / 100.0 * LvMonth / LvYearDays();
            if (r.Pool < cap) r.Pool = Math.Min(cap, r.Pool + add);
            r.Grown += LvMonth * m;
            return Math.Max(0.0, r.Pool - before);
        }

        /// <summary>P3 Settle: przyrost, pulap dzis, P dzis, nowa doba (Today 0, etap z f).</summary>
        private static void LvSettle(LvRec r, double pNow, int today, bool braked)
        {
            double cap = LvPoolPct / 100.0 * Math.Max(0.0, pNow);
            LvGrow(r, Math.Min(r.P, pNow), cap, today);
            r.Pool = Math.Max(0.0, Math.Min(r.Pool, cap));
            r.P = Math.Max(0.0, pNow);
            if (r.Day != today)
            {
                r.Day = today; r.Today = 0;
                r.Stage = StageOf(cap > 1e-9 ? r.Pool / cap : 0.0, braked);
            }
        }

        private static bool BrakedSafe(Settlement st) { try { return FreeHands.Braked(st); } catch { return false; } }

        // ------------------------------------------------------------ zaufanie i rodzaj
        /// <summary>P4: indeks zaufania 0-6 (wlasna wioska / ziemia rodu - 6; inaczej najwyzszy indeks modelu ochotnikow po notablach wsi okregu; bez notabli - 2).</summary>
        private static int TrustIdx(Settlement st, string uid)
        {
            if (SideVillageFief.IsMine(uid) || OwnLands(st)) return 6;
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.VolunteerModel : null;
            int best = -1, ok = 0; bool any = false;
            if (st.Notables != null && model != null && Hero.MainHero != null)
                foreach (var n in st.Notables)
                {
                    if (n == null || !n.IsAlive || n.CurrentSettlement != st) continue;
                    any = true;
                    try { int i = model.MaximumIndexHeroCanRecruitFromHero(Hero.MainHero, n); best = Math.Max(best, Math.Max(0, Math.Min(6, i))); ok++; }
                    catch (Exception e) { LvStumble("TrustIdx", e); }
                }
            if (!any || ok == 0) return 2;
            return Math.Max(0, best);
        }

        /// <summary>P6: rodzaj rekruta wioski na te dobe (oferta w pamieci). null - brak BasicTroop (opcja ukryta).</summary>
        private static CharacterObject TroopFor(Settlement st, string uid, int today)
        {
            KeyValuePair<int, CharacterObject> o;
            if (_lvOffer.TryGetValue(uid, out o) && o.Key == today && o.Value != null) return o.Value;
            var cul = st.Culture ?? (st.OwnerClan != null ? st.OwnerClan.Culture : null);
            CharacterObject basic = cul != null ? cul.BasicTroop : null, t = null;
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.VolunteerModel : null;
            Hero h = null;
            if (st.Notables != null)
            {
                foreach (var n in st.Notables) if (n != null && n.IsAlive && n.IsHeadman) { h = n; break; }
                if (h == null && model != null)
                    foreach (var n in st.Notables)
                    {
                        if (n == null || !n.IsAlive) continue;
                        bool can = false;
                        try { can = model.CanHaveRecruits(n); } catch (Exception e) { LvStumble("CanHaveRecruits", e); }
                        if (can) { h = n; break; }
                    }
            }
            float mp;
            bool bkLow = Levy.BkManpower(st, out mp) && mp < 1f;
            if (!bkLow && h != null && model != null)
            {
                try { t = model.GetBasicVolunteer(h); } catch (Exception e) { LvStumble("GetBasicVolunteer", e); t = null; }
            }
            if (t == null || t.IsHero || t.Tier > 1 || t.IsMounted) t = basic;
            if (t != null && t.IsHero) t = null;
            if (t != null) _lvOffer[uid] = new KeyValuePair<int, CharacterObject>(today, t);
            return t;
        }

        private static int PriceOf(CharacterObject t, int stage)
        {
            int baseCost = 1;
            try { baseCost = Math.Max(1, Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(t, Hero.MainHero).RoundedResultNumber); }
            catch (Exception e) { LvStumble("Price", e); }
            double m = stage >= 3 ? LvGeneralPrice : (stage == 2 ? LvLevyPrice : 1.0);
            return Math.Max(1, (int)Math.Round(baseCost * m));
        }

        // ------------------------------------------------------------ wycena (raz na godzine gry i po werbunku)
        private static LvQuote LvQuoteFor(Settlement st, string uid)
        {
            int hour = (int)CampaignTime.Now.ToHours;
            var q = _lvQ;
            if (q != null && q.Hour == hour && string.Equals(q.Uid, uid, StringComparison.Ordinal)) return q;
            q = new LvQuote { Uid = uid, Hour = hour };
            _lvQ = q;
            VillageBook.PartView me; double ha, na;
            if (st == null || uid == null || !PartOf(st, uid, out me, out ha, out na)) { q.Hide = true; return q; }
            int today = Today();
            q.Day = today;
            q.Burned = Burned(me);
            q.P = Math.Max(0.0, me.Home + me.Away); q.Home = Math.Max(0.0, me.Home);
            q.Cap = LvPoolPct / 100.0 * q.P;
            bool braked = BrakedSafe(st);
            var r = LvGet(uid);
            if (r != null) { LvSettle(r, q.P, today, braked); q.Pool = r.Pool; q.Stage = r.Stage; q.Today = r.Today; }
            else { q.Pool = q.Cap; q.Stage = StageOf(1.0, braked); q.Today = 0; }
            q.Callable = q.P > 1e-9 ? (int)Math.Floor(q.Pool * Math.Min(1.0, q.Home / q.P) + 1e-9) : 0;
            q.Idx = TrustIdx(st, uid);
            q.Muster = q.Idx <= 0 ? 0 : (int)Math.Ceiling(q.Cap * LvDailyPct / 100.0 * q.Idx / 6.0 * LvF[Math.Max(1, Math.Min(3, q.Stage)) - 1] - 1e-9);
            q.Ready = Math.Max(0, Math.Min(q.Callable, q.Muster - q.Today));
            q.Y = q.P * LvYouthPct / 100.0 * LvMonth / LvYearDays();
            q.Troop = TroopFor(st, uid, today);
            if (q.Troop == null) { q.Hide = true; return q; }
            q.Price = PriceOf(q.Troop, q.Stage);
            return q;
        }

        private static int RoomNow()
        {
            var mp = MobileParty.MainParty;
            if (mp == null || mp.Party == null || mp.MemberRoster == null) return 0;
            return Math.Max(0, mp.Party.PartySizeLimit - mp.MemberRoster.TotalManCount);
        }

        private static bool WatchNow(Settlement st)
        {
            if (CrownGifts.IsWatchParty(MobileParty.MainParty)) return true;
            return st != null && st.Culture != null && st.Culture.StringId == "nightswatch";
        }

        private static string StageWords(int stage)
        {
            if (stage >= 3) return "a general levy - the last men, dearly paid";
            if (stage == 2) return "a levy - the willing are gone and families want more";
            return "willing young men";
        }

        private static string Num(double v) { return Math.Round(Math.Max(0.0, v)).ToString("N0", Inv); }

        /// <summary>P11: powod odmowy (angielski dymek) i kod do linii doby; null - mozna werbowac. hide - opcja ukryta (wioska nieznana, bez rodzaju).</summary>
        private static string LevyGate(Settlement st, string uid, bool fief, out bool hide, out string code, out LvQuote q)
        {
            hide = false; code = null; q = null;
            if (NightRest.EnlistedNow) { code = "zaciag"; return "Your lord decides who joins his ranks."; }
            if (WatchNow(st) || _pbForce == "Straz") { code = "Straz"; return "The Watch takes no levies from these villages."; }
            if (st == null || st.Village == null || DistOf(Rec(uid)) == null) { hide = true; code = "nieznana"; return null; }
            string dn = Txt(st.Name);
            if (AtWar(st) || _pbForce == "wojna") { code = "wojna"; return "No man here will take up arms for an enemy of " + (st.MapFaction != null ? Txt(st.MapFaction.Name) : dn) + "."; }
            if (st.Village.VillageState != Village.VillageStates.Normal) { code = "rabunek"; return "Raiders have been through " + dn + " - nobody leaves home now."; }
            q = LvQuoteFor(st, uid);
            if (q == null || q.Hide) { hide = true; code = "nieznana"; return null; }
            if (q.Burned || _pbForce == "spalona") { code = "spalona"; return "The village is burned - its people have no men to spare."; }
            if (!fief && !AtVillage()) { code = "miejsce"; return "You are not at the village."; }
            if (q.Idx <= 0) { code = "zaufanie"; return "The folk of " + dn + " do not know you well enough to follow you."; }
            if (q.Callable < 1) { code = "pula"; return "Every man who could be spared is gone. About " + Math.Max(0.0, q.Y).ToString("0", Inv) + " young men come of age here each month."; }
            if (q.Ready < 1) { code = "koniec dnia"; return "No more men will come today - come back tomorrow."; }
            if (RoomNow() < 1) { code = "druzyna"; return "Your party is full."; }
            if (Hero.MainHero == null || Hero.MainHero.Gold < q.Price) { code = "zloto"; return "You cannot pay their families."; }
            return null;
        }

        private static void NoteRefusal(string uid, string code)
        {
            if (code == null || uid == null) return;
            if (!_ldRefused.Add(uid + "|" + code)) return;
            int n; _ldWhy.TryGetValue(code, out n); _ldWhy[code] = n + 1;
        }

        /// <summary>Warunek opcji "Recruit men of the village" (menu wioski pobocznej i wioski gracza).</summary>
        internal static bool LevyCondition(MenuCallbackArgs a, Settlement st, string uid, bool fief)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Recruit;
            try
            {
                if (!LevyOn(fief)) return false;
                if (!PeopleBook.LevyLive) return false;
                bool hide; string code; LvQuote q;
                string why = LevyGate(st, uid, fief, out hide, out code, out q);
                if (hide) return false;
                if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); NoteRefusal(uid, code); return true; }
                a.Tooltip = new TextObject("{=!}" + q.Ready + (q.Ready == 1 ? " man" : " men") + " of this village will follow you today, at " + q.Price + " denars each (" + StageWords(q.Stage)
                                           + "). " + q.Callable.ToString("N0", Inv) + " of its " + Num(q.P) + " people could still be spared; about " + Math.Max(0.0, q.Y).ToString("0", Inv)
                                           + " young men come of age here each month. Every man you take leaves its fields.");
                return true;
            }
            catch (Exception e) { LvStumble("Condition", e); return false; }
        }

        private static bool LevyMenuCondition(MenuCallbackArgs a) { return LevyCondition(a, DistOf(Rec(_uid)), _uid, false); }

        private static void LevyMenuConsequence(MenuCallbackArgs a) { LevyOpen(DistOf(Rec(_uid)), _uid, false); }

        /// <summary>Linia tekstu menu wioski: ilu mlodych mozna jeszcze wziac, ilu dorasta, etap. null - klucz wylaczony albo ksiega jej nie zna.</summary>
        private static string LevyLine(Settlement st, string uid, VillageBook.PartView me)
        {
            try
            {
                if (!LevyKey || me == null || st == null) return null;
                var q = LvQuoteFor(st, uid);
                if (q == null || q.Hide) return null;
                var sb = new StringBuilder(200);
                sb.Append("About ").Append(q.Callable.ToString("N0", Inv)).Append(" young men could still be called to arms here; about ").Append(Math.Max(0.0, q.Y).ToString("0", Inv))
                  .Append(" come of age each month.");
                if (q.Stage == 2) sb.Append(" The willing are gone - what is left is a levy, and families want more for their sons.");
                else if (q.Stage >= 3) sb.Append(" Only the last men are left - a general levy.");
                return sb.ToString();
            }
            catch (Exception e) { LvStumble("Line", e); return null; }
        }

        // ------------------------------------------------------------ okno
        /// <summary>Okno werbunku: jeden wiersz na czlowieka (najwyzej 40), wybor 1..min(ready, miejsce, zloto / cena).</summary>
        internal static void LevyOpen(Settlement st, string uid, bool fief)
        {
            try
            {
                if (!LevyOn(fief) || !PeopleBook.LevyLive) return;
                _lvQ = null;
                bool hide; string code; LvQuote q;
                string why = LevyGate(st, uid, fief, out hide, out code, out q);
                if (hide || q == null) return;
                if (why != null) { Log.Player(why, true); return; }
                int max = Math.Min(Math.Min(q.Ready, RoomNow()), Hero.MainHero.Gold / Math.Max(1, q.Price));
                max = Math.Min(max, LvOfferMax);
                if (max < 1) return;
                string vn = NameOf(Rec(uid)), dn = Txt(st.Name), tn = Txt(q.Troop.Name);
                var list = new List<InquiryElement>();
                int rows = Math.Min(LvOfferMax, q.Ready);
                for (int i = 0; i < rows; i++)
                {
                    ImageIdentifier img = null;
                    try { img = new CharacterImageIdentifier(CharacterCode.CreateFrom(q.Troop)); } catch { img = null; }
                    list.Add(new InquiryElement(i, tn + " - " + q.Price + " d", img, true, null));
                }
                string stage = q.Stage >= 3 ? "Only the last men are left - a general levy. Their families ask dearly."
                             : q.Stage == 2 ? "The willing are gone; the rest are a levy - their families want more for them."
                             : "Young men of " + vn + " are willing to follow you.";
                string desc = stage + " Each man costs " + q.Price + " denars - the money goes to the families (the purse of " + dn + ").\n\nEvery man you take leaves its fields: fewer hands"
                              + " for its harvest, and he does not come back if he falls. About " + Math.Max(0.0, q.Y).ToString("0", Inv) + " young men come of age here each month.";
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Men of " + vn, desc, list, true, 1, max, "Recruit", GameTexts.FindText("str_cancel").ToString(),
                    delegate (List<InquiryElement> sel)
                    {
                        try { if (sel != null && sel.Count > 0) LevyDo(st, uid, sel.Count, fief ? "okno (wioska gracza)" : "okno"); }
                        catch (Exception e) { LvStumble("Pick", e); }
                    }, null));
            }
            catch (Exception e) { LvStumble("Open", e); }
        }

        internal static void LevyOpenFief(MenuCallbackArgs a)
        {
            try { LevyOpen(Settlement.Find(SideVillageFief.DistId ?? ""), SideVillageFief.Uid, true); }
            catch (Exception e) { LvStumble("OpenFief", e); }
        }

        // ------------------------------------------------------------ werbunek
        /// <summary>Wynik jednego werbunku (dla linii logu i sondy testu).</summary>
        private sealed class LvDone
        {
            public int Asked, Keep, Price, Total, Stage, Idx, DtePcs, HeroGold0, HeroGold1, Purse0, Purse1, Men0, Men1;
            public double Got, Pool0, Pool1, Cap, P, Home, Lack, Share;
            public CharacterObject Troop; public Settlement Region; public string Why;
        }

        /// <summary>A3: werbunek k ludzi z wioski uid (watek glowny, okno albo sonda testu). Kolejnosc: DOM (TakeLevyHere) -&gt; ludzie -&gt; pochodzenie -&gt; zaplata za przybylych.</summary>
        private static LvDone LevyDo(Settlement st, string uid, int k, string how)
        {
            var res = new LvDone { Asked = k };
            try
            {
                if (!LevyKey || !VillageBook.Active || !PeopleBook.LevyLive) { res.Why = "klucz albo ksiega"; return res; }
                _lvQ = null;
                bool hide; string code; LvQuote q;
                // przy wiosce sprawdza okno (warunek menu); tu - wioska gracza i sonda: bez warunku miejsca
                string why = LevyGate(st, uid, true, out hide, out code, out q);
                if (hide || q == null) { res.Why = "wioska nieznana"; return res; }
                if (why != null) { res.Why = code; NoteRefusal(uid, code); Log.Player(why, true); return res; }
                var mp = MobileParty.MainParty; var hero = Hero.MainHero;
                k = Math.Min(Math.Min(k, q.Ready), Math.Min(RoomNow(), hero.Gold / Math.Max(1, q.Price)));
                if (k < 1) { res.Why = "nic do wziecia"; return res; }
                res.Asked = k; res.Price = q.Price; res.Stage = q.Stage; res.Idx = q.Idx; res.Troop = q.Troop; res.Cap = q.Cap; res.P = q.P; res.Home = q.Home;
                var r0 = LvGet(uid);
                res.Pool0 = r0 != null ? r0.Pool : q.Cap;
                res.HeroGold0 = hero.Gold; res.Purse0 = st.Village.Gold;
                var roster = mp.MemberRoster;
                res.Men0 = roster.GetTroopCount(q.Troop);
                // 2. DOM okregu - ludzie TEJ wioski (przed pieniedzmi)
                Settlement region;
                double got = PeopleBook.TakeLevyHere(st, k, PeopleBook.MLevyVillage, out region);
                res.Got = got; res.Region = region;
                int keep = Math.Min(k, (int)Math.Ceiling(got - 1e-6));
                if (keep <= 0)
                {
                    res.Why = "DOM na podlodze";
                    Log.Info(LP + NameOf(Rec(uid)) + " (" + uid + ", okreg " + st.StringId + "): prosba " + k + ", DOM wsi okregu nie dal nikogo (podloga 50 hearth) - bez ludzi i bez zaplaty.");
                    Log.Player("The district has no one left to spare - its last homesteads keep their men.", true);
                    return res;
                }
                res.Keep = keep;
                // 3. ludzie do druzyny, zdarzenie gry, komplet DTE tieru 1
                RecruitingAt = st;
                try
                {
                    roster.AddToCounts(q.Troop, keep);
                    try { CampaignEventDispatcher.Instance.OnUnitRecruited(q.Troop, keep); } catch (Exception e) { LvStumble("OnUnitRecruited", e); }
                    try { res.DtePcs = PlayerRecruit.SideVillageKit(q.Troop, keep); } catch (Exception e) { LvStumble("Kit", e); }
                }
                finally { RecruitingAt = null; }
                res.Men1 = roster.GetTroopCount(q.Troop);
                // 4. pochodzenie
                try
                {
                    PeopleBook.GiveNew(roster, region, got, PeopleBook.FLevyPlayer);
                    if (keep - got > 1e-6) PeopleBook.GiveNone(roster, keep - got, PeopleBook.FNoneFloor);
                }
                catch (Exception e) { LvStumble("Origin", e); }
                // 5. zaplata tylko za przybylych - zdarzenie gry gracz -> kiesa wsi okregu
                res.Total = keep * q.Price;
                if (res.Total > 0) GiveGoldAction.ApplyForCharacterToSettlement(hero, st, res.Total, true);
                res.HeroGold1 = hero.Gold; res.Purse1 = st.Village.Gold;
                // 6. brak rak wioski i wpis puli
                double sh = StandShareOf(st, uid);
                res.Share = sh;
                if (sh >= 0.0 && got > 1e-9) { res.Lack = got * (1.0 - sh); LevyAddLack(uid, res.Lack); }
                int today = Today();
                var r = LvGet(uid);
                if (r == null)
                {
                    r = new LvRec { Uid = uid, P = q.P, Pool = q.Cap, Grown = today, Day = today, Stage = q.Stage, Today = 0 };
                    if (!LvPut(r)) r = null;
                }
                if (r != null) { r.Pool = Math.Max(0.0, r.Pool - keep); r.Today += keep; r.Total += keep; res.Pool1 = r.Pool; }
                _lvQ = null;
                _ldRec++; _ldMen += keep; _ldGold += res.Total; _ldGot += got; _ldFloor += Math.Max(0.0, keep - got); _ldLack += res.Lack;
                if (_ldEx.Count < 3) _ldEx.Add(NameOf(Rec(uid)) + " " + keep + "x" + res.Price);
                // 7. linie
                string vn = NameOf(Rec(uid));
                Log.Info(LP + vn + " (" + uid + ", okreg " + Txt(st.Name) + " " + st.StringId + ") - " + how + ": " + keep + " ludzi typu " + Txt(q.Troop.Name) + " (" + q.Troop.StringId + ", tier "
                         + q.Troop.Tier + ") z prosby " + k + ", etap " + q.Stage + " (cena x" + (q.Stage >= 3 ? LvGeneralPrice : q.Stage == 2 ? LvLevyPrice : 1.0).ToString("0.##", Inv) + "), cena "
                         + q.Price + " x " + keep + " = " + res.Total + " | gracz " + res.HeroGold0 + " -> " + res.HeroGold1 + ", kiesa wsi okregu " + res.Purse0 + " -> " + res.Purse1
                         + " | DOM -" + got.ToString("0.##", Inv) + " (podloga " + Math.Max(0.0, keep - got).ToString("0.##", Inv) + ", region " + (region != null ? region.StringId : "-") + ")"
                         + " | pula " + res.Pool0.ToString("0.##", Inv) + " -> " + res.Pool1.ToString("0.##", Inv) + " z pulapu " + q.Cap.ToString("0.#", Inv) + " (ludzi " + q.P.ToString("0", Inv)
                         + ", w domu " + q.Home.ToString("0", Inv) + "), dzis " + (r != null ? r.Today : keep) + ", razem " + (r != null ? r.Total : keep) + " | brak rak +" + res.Lack.ToString("0.##", Inv)
                         + " (udzial w osadach " + (sh >= 0.0 ? (100 * sh).ToString("0.#", Inv) + "%" : "-") + ") | zaufanie " + q.Idx + " | DTE tier 1 szt. " + res.DtePcs + ".");
                Log.Player(keep + (keep == 1 ? " man" : " men") + " of " + vn + " join your party. Their families got " + res.Total.ToString("N0", Inv) + " denars.");
            }
            catch (Exception e) { RecruitingAt = null; res.Why = "wyjatek"; LvStumble("Do", e); }
            return res;
        }

        // ------------------------------------------------------------ brak rak (czyta VillageBook.LackOf pod Gate)
        internal static bool LevyLackAny { get { var d = _lvLack; return d != null && d.Count > 0 && LevyKey; } }

        /// <summary>P8: brak rak wioski uid po poborze (ludzie; liniowo do 0 przez SideRecruitResettleDays). Sam odczyt, nie rzuca; klucz wylaczony - 0.</summary>
        internal static double LevyLackNow(string uid)
        {
            try
            {
                if (uid == null || !LevyKey) return 0.0;
                double[] e; var d = _lvLack;
                if (d == null || !d.TryGetValue(uid, out e) || e == null) return 0.0;
                return LvLackAt(e, Today0());
            }
            catch { return 0.0; }
        }

        private static double LvLackAt(double[] e, int today)
        {
            double age = today > 0 ? Math.Max(0.0, today - e[1]) : 0.0;
            return Math.Max(0.0, e[0] * (1.0 - age / LvResettleDays));
        }

        private static void LevyAddLack(string uid, double n)
        {
            if (!UidOk(uid) || !(n > 0.0) || double.IsInfinity(n)) return;
            double now = 0.0; double[] e; var d0 = _lvLack;
            if (d0.TryGetValue(uid, out e) && e != null) now = LvLackAt(e, Today0());
            var d = new Dictionary<string, double[]>(d0, StringComparer.Ordinal);
            d[uid] = new[] { now + n, (double)Today0() };
            _lvLack = d;
        }

        // ------------------------------------------------------------ obroncy (jedna regula)
        /// <summary>P9: ilu ludzi wioska moze jeszcze wystawic (obroncy): brak wpisu albo klucz wylaczony - bez limitu; inaczej floor(pula x min(1, home/P)).
        /// Sam odczyt wpisu (przyrost mlodych liczony w pamieci, bez zmiany wpisu), bez PartsOf, nie rzuca.</summary>
        internal static int LevyCallableFor(string uid, double home)
        {
            try
            {
                if (uid == null || !LevyKey) return int.MaxValue;
                var r = LvGet(uid);
                if (r == null) return int.MaxValue;
                double P = r.P, pool = r.Pool;
                if (!(P > 1e-9)) return 0;
                int today = Today0();
                int m = today > 0 ? (today - r.Grown) / LvMonth : 0;
                if (m > 0) pool = Math.Min(LvPoolPct / 100.0 * P, pool + m * P * LvYouthPct / 100.0 * LvMonth / LvYearDays());
                return (int)Math.Floor(Math.Max(0.0, pool) * Math.Min(1.0, Math.Max(0.0, home) / P) + 1e-9);
            }
            catch { return int.MaxValue; }
        }

        /// <summary>P9: polegli / pojmani obroncy wioski obnizaja jej pule (ci sami mlodzi). pHint - ludzie wioski do zalozenia wpisu (brak wpisu i pHint &lt;= 0 - nic).
        /// Nie rzuca.</summary>
        internal static void LevyNoteLost(string uid, double lost, double pHint)
        {
            try
            {
                if (!LevyKey || uid == null || !(lost > 0.0)) return;
                var r = LvGet(uid);
                if (r == null)
                {
                    if (!(pHint > 0.0)) return;
                    int today = Today0();
                    r = new LvRec { Uid = uid, P = pHint, Pool = LvPoolPct / 100.0 * pHint, Grown = today };
                    if (!LvPut(r)) return;
                }
                r.Pool = Math.Max(0.0, r.Pool - lost);
                _ldLost += lost; _ldLostN++;
                _lvQ = null;
            }
            catch (Exception e) { LvStumble("NoteLost", e); }
        }

        /// <summary>FinishFight: ludzie wioski (Home + Away) z ksiegi do zalozenia wpisu.</summary>
        private static double LvPeopleOf(string distId, string uid)
        {
            try
            {
                var st = distId != null ? Settlement.Find(distId) : null;
                VillageBook.PartView me; double h, n;
                return st != null && PartOf(st, uid, out me, out h, out n) ? me.Home + me.Away : 0.0;
            }
            catch { return 0.0; }
        }

        // ------------------------------------------------------------ doba i sesja
        /// <summary>Doba: przyrost mlodych wpisow (P zapisane - bez PartsOf), pelne wpisy wylatuja, wygasle braki rak wylatuja; linia tylko z ruchem. Zwraca ruch.</summary>
        private static int LevyDaily()
        {
            long t0 = Stopwatch.GetTimestamp();
            int act = 0;
            try
            {
                int today = Today();
                var d = _lv;
                List<string> drop = null;
                foreach (var r in d.Values)
                {
                    if (r == null) continue;
                    double cap = LvPoolPct / 100.0 * r.P;
                    if ((today - r.Grown) / LvMonth > 0) { double g = LvGrow(r, r.P, cap, today); _ldDue++; _ldGrown += g; }
                    if (r.Pool >= cap - 1e-6 && (r.Day != today || r.Today == 0)) (drop ?? (drop = new List<string>())).Add(r.Uid);
                }
                if (drop != null)
                {
                    var nd = new Dictionary<string, LvRec>(d, StringComparer.Ordinal);
                    foreach (var u in drop) nd.Remove(u);
                    _lv = nd; _ldDrop = drop.Count;
                }
                var lk = _lvLack; List<string> gone = null; double lackSum = 0.0;
                foreach (var kv in lk) { double v = kv.Value != null ? LvLackAt(kv.Value, today) : 0.0; if (v < 0.01) (gone ?? (gone = new List<string>())).Add(kv.Key); else lackSum += v; }
                if (gone != null)
                {
                    var nl = new Dictionary<string, double[]>(lk, StringComparer.Ordinal);
                    foreach (var u in gone) nl.Remove(u);
                    _lvLack = nl;
                }
                try { LevyProbeDaily(today); } catch (Exception e) { LvStumble("Probe", e); }
                act = _ldRec + _ldRefused.Count + _ldDue + _ldDrop + _ldLostN + _lvStDay;
                _ldTicks += Stopwatch.GetTimestamp() - t0;
                if (act > 0)
                {
                    var sb = new StringBuilder(400);
                    foreach (var kv in _ldWhy) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
                    double ms = _ldTicks * 1000.0 / Stopwatch.Frequency;
                    Log.Info(LP + "dzien " + (today - 1) + " | klucz " + (LevyKey ? "TAK" : "WYL") + " | werbunki " + _ldRec + " (ludzi " + _ldMen + ", zlota do kies wsi " + _ldGold + ", z DOM "
                             + _ldGot.ToString("0.##", Inv) + ", podloga " + _ldFloor.ToString("0.##", Inv) + ", brak rak +" + _ldLack.ToString("0.#", Inv) + ")"
                             + " | odmowy (wioska x powod, bez powtorzen): " + (sb.Length > 0 ? sb.ToString() : "-")
                             + " | pule: wpisow " + _lv.Count + ", przyrost mlodych w " + _ldDue + " (+" + _ldGrown.ToString("0.##", Inv) + "), pelne usuniete " + _ldDrop
                             + ", straty obroncow " + _ldLostN + " (" + _ldLost.ToString("0.#", Inv) + ") | braki rak " + _lvLack.Count + " wiosek (razem " + lackSum.ToString("0.#", Inv) + ")"
                             + (_ldEx.Count > 0 ? " | przyklady: " + string.Join("; ", _ldEx.ToArray()) : "") + " | czas doby " + ms.ToString("0.000", Inv) + " ms | potkniecia doby " + _lvStDay
                             + ", sesji " + _lvSt + ".");
                }
            }
            catch (Exception e) { LvStumble("Daily", e); act++; }
            return act;
        }

        /// <summary>Odcinek linii "Wioski poboczne (WP): start sesji".</summary>
        private static string LevySessionText()
        {
            double sum = 0.0; foreach (var r in _lv.Values) if (r != null) sum += r.Pool;
            return LP + "klucz Side Village Recruit " + (LevyKey ? "TAK" : "WYL") + " (pula " + LvPoolPct.ToString("0.#", Inv) + "% ludzi wioski, mlodzi " + LvYouthPct.ToString("0.##", Inv)
                   + "% ludzi na rok co " + LvMonth + " dob (rok " + LvYearDays() + " dob), dziennie " + LvDailyPct.ToString("0.#", Inv) + "% pulapu x zaufanie/6 x 1/0.5/0.25, ceny x1/x"
                   + LvLevyPrice.ToString("0.##", Inv) + "/x" + LvGeneralPrice.ToString("0.##", Inv) + ", brak rak wraca przez " + LvResettleDays + " dob; ksiega pochodzenia LevyLive "
                   + (PeopleBook.LevyLive ? "TAK" : "NIE") + ") | wpisy puli " + _lv.Count + " (suma " + sum.ToString("0.##", Inv) + "), braki rak " + _lvLack.Count
                   + " | Straz: bez poboru | AI: bez zmian (nie wjezdza do wiosek pobocznych) | potkniecia " + _lvSt;
        }

        // ------------------------------------------------------------ zapis arm_sidelevy
        internal static string LevyExport()
        {
            try
            {
                int today = Today0();
                var sb = new StringBuilder(256);
                sb.Append("sl1|p=");
                bool first = true; int n = 0; double sum = 0.0;
                foreach (var r in _lv.Values)
                {
                    if (r == null || !UidOk(r.Uid)) continue;
                    if (r.Pool >= LvPoolPct / 100.0 * r.P - 1e-6) continue;   // pelny - brak wpisu = pelna pula
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(r.Uid).Append(',').Append(r.Pool.ToString("R", Inv)).Append(',').Append(r.Grown.ToString(Inv)).Append(',').Append(r.Day.ToString(Inv)).Append(',')
                      .Append(r.Stage.ToString(Inv)).Append(',').Append(r.Today.ToString(Inv)).Append(',').Append(r.Total.ToString(Inv)).Append(',').Append(r.P.ToString("R", Inv));
                    n++; sum += r.Pool;
                }
                sb.Append("|k=");
                first = true; int nk = 0;
                foreach (var kv in _lvLack)
                {
                    if (kv.Value == null || !UidOk(kv.Key) || LvLackAt(kv.Value, today) < 0.01) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(',').Append(kv.Value[0].ToString("R", Inv)).Append(',').Append(((int)kv.Value[1]).ToString(Inv));
                    nk++;
                }
                Log.Info(LP + "zapis arm_sidelevy - wpisy puli " + n + " (suma " + sum.ToString("0.##", Inv) + "), braki rak " + nk + ".");
                return n + nk == 0 ? "" : sb.ToString();
            }
            catch (Exception e) { LvStumble("Export", e); return ""; }
        }

        internal static void LevyImport(string data)
        {
            LevyReset();
            try
            {
                if (string.IsNullOrEmpty(data)) { Log.Info(LP + "wczytanie arm_sidelevy - wpisy puli 0, braki rak 0 (brak klucza: stary zapis albo pule pelne)."); return; }
                var f = data.Split('|');
                if (f.Length < 1 || f[0] != "sl1") { Log.Info(LP + "zapis arm_sidelevy nieznany (" + data.Length + " znakow) - pominiety"); return; }
                int bad = 0;
                var recs = new Dictionary<string, LvRec>(StringComparer.Ordinal);
                var lacks = new Dictionary<string, double[]>(StringComparer.Ordinal);
                for (int i = 1; i < f.Length; i++)
                {
                    string sec = f[i];
                    if (sec.StartsWith("p=", StringComparison.Ordinal))
                        foreach (var e in sec.Substring(2).Split(';'))
                        {
                            if (e.Length == 0) continue;
                            var a = e.Split(',');
                            double pool, p; int grown, day, stage, tod, tot;
                            if (a.Length < 8 || !UidOk(a[0]) || !double.TryParse(a[1], NumberStyles.Float, Inv, out pool) || !int.TryParse(a[2], NumberStyles.Integer, Inv, out grown)
                                || !int.TryParse(a[3], NumberStyles.Integer, Inv, out day) || !int.TryParse(a[4], NumberStyles.Integer, Inv, out stage)
                                || !int.TryParse(a[5], NumberStyles.Integer, Inv, out tod) || !int.TryParse(a[6], NumberStyles.Integer, Inv, out tot)
                                || !double.TryParse(a[7], NumberStyles.Float, Inv, out p) || double.IsNaN(pool) || double.IsNaN(p) || double.IsInfinity(pool) || double.IsInfinity(p))
                            { bad++; continue; }
                            recs[a[0]] = new LvRec { Uid = a[0], Pool = Math.Max(0.0, pool), Grown = grown, Day = day, Stage = Math.Max(1, Math.Min(3, stage)), Today = Math.Max(0, tod), Total = Math.Max(0, tot), P = Math.Max(0.0, p) };
                        }
                    else if (sec.StartsWith("k=", StringComparison.Ordinal))
                        foreach (var e in sec.Substring(2).Split(';'))
                        {
                            if (e.Length == 0) continue;
                            var a = e.Split(',');
                            double n; int day;
                            if (a.Length < 3 || !UidOk(a[0]) || !double.TryParse(a[1], NumberStyles.Float, Inv, out n) || !int.TryParse(a[2], NumberStyles.Integer, Inv, out day)
                                || double.IsNaN(n) || double.IsInfinity(n) || n < 0.0) { bad++; continue; }
                            lacks[a[0]] = new[] { n, (double)day };
                        }
                    // nieznane sekcje - pominiete (nowsza DLL)
                }
                _lv = recs; _lvLack = lacks;
                double sum = 0.0; foreach (var r in recs.Values) sum += r.Pool;
                double ls = 0.0; foreach (var v in lacks.Values) ls += v[0];
                Log.Info(LP + "wczytanie arm_sidelevy - wpisy puli " + recs.Count + " (suma " + sum.ToString("0.##", Inv) + "), braki rak " + lacks.Count + " (zapisane " + ls.ToString("0.##", Inv) + ")"
                         + (bad > 0 ? ", zlych wpisow " + bad + " (pominiete)" : "") + ", " + data.Length + " znakow.");
            }
            catch (Exception e) { LevyReset(); LvStumble("Import", e); }
        }
    }
}
