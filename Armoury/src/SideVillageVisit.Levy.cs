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
    /// Srednia wioska: pulap 10% = ok. 526 ludzi; od n36 pula wraca z przyrostu naturalnego jej wsi okregu - przy 0.5% rocznie ok. 2 chlopcow dorasta na miesiac
    /// (pelna pula po ok. 20 latach), przy pulapie 1.3% ok. 5 (ok. 8 lat); glod, wojna, bieda, rabunek - nic.
    /// REGULY (liczby Claude'a):
    ///  P1 LUDZIE: P = me.Home + me.Away z PartOf (VillageBook.PartsOf) - ludzie TEJ wioski w ksiedze wiosek. Pulap cap = SideRecruitPoolPercent (10) / 100 x P.
    ///  P2 WPIS LvRec {Uid, Pool, P, Grown, Day, Stage, Today, Total}; brak wpisu = pula pelna (Pool = cap). Wpis powstaje przy pierwszym werbunku albo pierwszej stracie
    ///     obroncow (Pool = cap, Grown = dzis).
    ///  P3 ODNOWA (n36, Jeff 24.10: "przyrost bierze sie z realnego wzrostu tej wioski z jedzenia i gospodarki itd. - juz ustalilismy, jaki jest wzrost i jego
    ///     zakresy"; dotad stale 1% ludzi na rok): co dobe (LevyDaily, raz na dobe na wpis) Acc += P x max(0, g) / 100 / dni roku; g [% rocznie] = przyrost naturalny
    ///     wsi okregu (wies gry, ktorej ludzi ksiega dzieli na wioski poboczne) - PopulationLaw.RateOf, ta sama stopa, ktora dopisuje jej dobowy tick E4/4 (dobrobyt,
    ///     glod, niebezpieczenstwo, premie gry, korytarz do GrowthMaxPercent; spalona, lupiona, pod przymusem, glod ponad prog - 0). Ujemne g - bez odnowy (ksiega i tak
    ///     zmniejsza P, wiec pulap spada). Przyrost naturalny wylaczony (GrowthOn false) albo nieznany - zapas: stara regula g = SideRecruitYouthPercent (log: "zapas").
    ///     Stopa raz na dobe na wies okregu (pamiec doby _lvG). Nowy wpis - Acc 0 (odnowa od nastepnej doby).
    ///     ROZLICZENIE (Settle): m = (dzis - Grown) / 28 pelnych miesiecy; m &gt; 0: Pool = min(cap, Pool + Acc), Acc = 0, Grown += 28m (dni roku = max(28,
    ///     CampaignTime.DaysInYear) - 364 w kalendarzu Armoury). Pool = min(Pool, cap dzis), P = P dzis. Mlodzi na miesiac (q.Y, YouthWords) = P x max(0, g) / 100
    ///     x 28 / dni roku z tej samej stopy; g &lt;= 0 - tekst mowi, czemu wies nie rosnie. Nowa doba: Today = 0, etap z f = Pool/cap (f > 0.6 - 1 ochotnicy, f > 0.3 - 2 pobor, inaczej 3 pospolite ruszenie);
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
    ///     na rok): wioska z wpisem (pula ponizej pulapu) traci z puli swoj udzial (LevyNoteDistrictDraw z PeopleBook.FromVillage, MLevyVillage / MLevyA5 - to mlodzi);
    ///     wioska bez wpisu (pula pelna) - tylko nizszy pulap. Wlasny pobor (TakeLevyHere) tej drogi nie uzywa - bierze swoje k z wlasnej puli.
    ///  P8 BRAK RAK: LevyAddLack(uid, zdjeci x (1 - udzial wioski w stojacych osadach okregu)) - jak brak rak obroncow (VillageBook.LackOf przesuwa udzial reszty okregu na te
    ///     wioske); wraca liniowo przez SideRecruitResettleDays (364): zagrody zasiedla reszta okregu. Osobny magazyn kopiowany przy zapisie (czytany pod Gate ksiegi).
    ///     Klucz wylaczony - brak rak 0.
    ///  P9 OBRONCY - jedna regula: DefendersFor = max(0, min(pulap obroncow - polegli, LevyCallableFor)). LevyCallableFor czyta tylko wpis (bez PartsOf, nie rzuca): brak wpisu
    ///     - bez limitu, inaczej floor((Pool + Acc po pelnym miesiacu, do pulapu) x min(1, home/P)). LevyNoteLost (FinishFight) obniza pule - polegli obroncy to ci sami mlodzi.
    ///     Lancuch AI (NoteChainBurn) puli nie obniza - nie zdejmuje ludzi z ksiegi (tylko pamiec poleglych, wraca przez 2 miesiace).
    ///  P10 AI: tylko gracz - AI nie wjezdza do wiosek pobocznych (werbuje u notabli wsi gry jak dotad).
    ///  P11 WARUNKI (kolejnosc, kazdy wylacza opcje z powodem; ukryta tylko przy wylaczonym kluczu): klucz, ksiega pochodzenia (LevyLive), sluzba u lorda (ROT enlisted),
    ///     Straz (partia Strazy albo kultura nightswatch), wioska nieznana, wojna, rabunek okregu, spalona, przy wiosce, zaufanie, podloga 50 hearth DOM wsi okregu (Room =
    ///     PeopleBook.LevyRoomHere - ready nie wiecej niz Room, wiec TakeLevyHere zawsze daje k), pula, koniec dnia, miejsce w druzynie, zloto.
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
    /// Zapis arm_sidelevy "sl1|p=uid,pool,grown,day,stage,today,total,P,acc;...|k=uid,brak,doba;..." - wszystkie wpisy (pelne usuwa doba) i braki &gt;= 0.01; brak klucza (stary zapis) = pule pelne;
    /// n36: 9. pole acc - przyrost w toku (Acc); wpis bez niego (sl1 sprzed n36) - Acc 0; starsza DLL czyta 8 pol i 9. pomija (a.Length &gt;= 8); inny naglowek niz sl1 - pominiety;
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

        private sealed class LvRec
        {
            public string Uid; public double Pool, P; public int Grown, Day = -1, Stage = 1, Today, Total;
            public double Acc;              // n36 (P3): przyrost w toku od ostatniego rozliczenia (ludzie) - w zapisie
            // n36: tylko w pamieci (linia doby, sonda): doba i skladowe ostatniego dopisania do Acc, ostatnie rozliczenie
            public int AccDay = -1, AccN, SettleDay = -1, LastSettleN;
            public double AccP, LastG, LastAdd, LastSettle; public bool LastFb; public string LastWhy;
        }

        // magazyny kopiowane przy zapisie (czytane takze pod Gate ksiegi wiosek): nowy slownik przy dodaniu / usunieciu wpisu, pola wpisu zmieniane w miejscu na watku glownym
        private static volatile Dictionary<string, LvRec> _lv = new Dictionary<string, LvRec>(StringComparer.Ordinal);
        private static volatile Dictionary<string, double[]> _lvLack = new Dictionary<string, double[]>(StringComparer.Ordinal);   // uid -> { ilu, doba }

        private sealed class LvQuote
        {
            public string Uid; public int Hour = -1, Day = -1;
            public bool Hide, Burned, Own;   // n36: wlasnosc wioski przy wycenie - zmiana (kupno / odkup / zamek) uniewaznia wycene w tej samej godzinie
            public double P, Home, Cap, Pool, Y;
            public double G; public string GWhy; public bool GFb;   // n36 (P3): stopa odnowy wsi okregu (% rocznie, ze znakiem), kod przyczyny, zapas
            public int Callable, Muster, Ready, Stage = 1, Idx, Price, Today, Room;
            public CharacterObject Troop;
        }
        private static LvQuote _lvQ;
        private static readonly Dictionary<string, KeyValuePair<int, CharacterObject>> _lvOffer = new Dictionary<string, KeyValuePair<int, CharacterObject>>(StringComparer.Ordinal);

        // liczniki
        private static int _lvSt, _lvStDay;
        private static int _ldRec, _ldMen, _ldDue, _ldDrop, _ldLostN;
        private static long _ldGold;
        private static double _ldGot, _ldFloor, _ldLack, _ldGrown, _ldLost, _ldVan;
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
            _lvGDay = int.MinValue; _lvG.Clear();
            LevyDayZero();
            LevyProbeReset();
        }

        private static void LevyDayZero()
        {
            _ldRec = _ldMen = _ldDue = _ldDrop = _ldLostN = 0; _ldGold = 0; _ldGot = _ldFloor = _ldLack = _ldGrown = _ldLost = _ldVan = 0.0; _ldTicks = 0; _lvStDay = 0;
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

        // n36 (P3): stopa odnowy - pamiec doby na wies okregu (wiele wiosek pobocznych ma te sama wies; jedno pytanie modelu na wies i dobe)
        private struct LvG { public double G; public string Why; public bool Fb; }
        private static int _lvGDay = int.MinValue;
        private static readonly Dictionary<Settlement, LvG> _lvG = new Dictionary<Settlement, LvG>();

        /// <summary>P3 (n36): stopa odnowy puli g [% ludzi rocznie, ze znakiem] wioski pobocznej o wsi okregu st - przyrost naturalny tej wsi (PopulationLaw.RateOf,
        /// E4/4); przyrost wylaczony albo nieznany - zapas SideRecruitYouthPercent (fb, why "wyl" / "blad"). Raz na dobe na wies okregu (stopa z modelu). Nie rzuca.</summary>
        private static double LvRate(Settlement st, out string why, out bool fb)
        {
            int day = Today0();
            if (day != _lvGDay) { _lvGDay = day; _lvG.Clear(); }
            LvG x;
            if (st != null && _lvG.TryGetValue(st, out x)) { why = x.Why; fb = x.Fb; return x.G; }
            x = new LvG { G = LvYouthPct, Why = !PopulationLaw.GrowthOn ? "wyl" : (st == null ? "nieznana" : "blad"), Fb = true };   // nieznana - wioska bez wsi okregu w ukladzie
            try
            {
                float g; string w = null;
                if (st != null && st.Village != null && PopulationLaw.RateOf(st.Village, out g, out w)) x = new LvG { G = g, Why = w, Fb = false };
                else if (w != null) x.Why = w;
            }
            catch (Exception e) { LvStumble("Rate", e); }
            if (st != null && !x.Fb) _lvG[st] = x;   // zapas nie do pamieci doby - latka przyrostu wpieta pozniej w tej dobie (start sesji) liczy juz od nastepnego pytania
            why = x.Why; fb = x.Fb;
            return x.G;
        }

        /// <summary>P3 (n36): dobowe dopisanie do przyrostu w toku - Acc += P x max(0, g) / 100 / dni roku, raz na dobe na wpis. Zwraca g (ze znakiem); fb - zapas.</summary>
        private static double LvAccrue(LvRec r, int today, out bool fb)
        {
            string why;
            double g = LvRate(DistOf(Rec(r.Uid)), out why, out fb);
            if (r.AccDay == today) return g;
            double add = Math.Max(0.0, r.P) * Math.Max(0.0, g) / 100.0 / LvYearDays();
            if (double.IsNaN(add) || double.IsInfinity(add)) add = 0.0;
            r.Acc += add; r.AccDay = today; r.AccN++;
            r.AccP = r.P; r.LastG = g; r.LastAdd = add; r.LastFb = fb; r.LastWhy = why;
            return g;
        }

        /// <summary>P3: rozliczenie pelnych miesiecy - przyrost w toku (Acc) do puli, pulap cap; Acc = 0. Zwraca, ile przybylo.</summary>
        private static double LvGrow(LvRec r, double cap, int today)
        {
            int m = (today - r.Grown) / LvMonth;
            if (m <= 0) return 0.0;
            double before = r.Pool;
            double add = r.Acc > 0.0 && !double.IsInfinity(r.Acc) ? r.Acc : 0.0;   // takze NaN -> 0
            if (r.Pool < cap) r.Pool = Math.Min(cap, r.Pool + add);
            r.LastSettle = add; r.LastSettleN = r.AccN; r.SettleDay = today;
            r.Acc = 0.0; r.AccN = 0;
            r.Grown += LvMonth * m;
            return Math.Max(0.0, r.Pool - before);
        }

        /// <summary>P3 Settle: przyrost, pulap dzis, P dzis, nowa doba (Today 0, etap z f).</summary>
        private static void LvSettle(LvRec r, double pNow, int today, bool braked)
        {
            double cap = LvPoolPct / 100.0 * Math.Max(0.0, pNow);
            LvGrow(r, cap, today);
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
            bool own = SideVillageFief.IsMine(uid) || OwnLands(st);   // n36: zaufanie 6 zalezy od wlasnosci - kupno z menu wioski w tej samej godzinie nie moze trzymac starego indeksu
            var q = _lvQ;
            if (q != null && q.Hour == hour && q.Own == own && string.Equals(q.Uid, uid, StringComparison.Ordinal)) return q;
            q = new LvQuote { Uid = uid, Hour = hour, Own = own };   // recenzja: do pamieci (_lvQ) dopiero gotowa - wyjatek w srodku nie zostawia polowy wyceny na godzine
            VillageBook.PartView me; double ha, na;
            if (st == null || uid == null || !PartOf(st, uid, out me, out ha, out na)) { q.Hide = true; _lvQ = q; return q; }
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
            q.Room = PeopleBook.LevyRoomHere(st);   // podloga 50 hearth DOM wsi okregu - nikt z niczego, nikt "na podlodze"
            q.Ready = Math.Max(0, Math.Min(Math.Min(q.Callable, q.Room), q.Muster - q.Today));
            string gw; bool gfb;
            q.G = LvRate(st, out gw, out gfb); q.GWhy = gw; q.GFb = gfb;
            q.Y = q.P * Math.Max(0.0, q.G) / 100.0 * LvMonth / LvYearDays();   // P3 (n36): mlodzi na miesiac z przyrostu wsi okregu
            q.Troop = TroopFor(st, uid, today);
            if (q.Troop == null) { q.Hide = true; _lvQ = q; return q; }
            q.Price = PriceOf(q.Troop, q.Stage);
            _lvQ = q;
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

        /// <summary>Odnowa puli slowami gracza (n36, P3): wies rosnie - dorastanie z jej przyrostu (przysiolek: ponizej pol chlopca na miesiac - "co N miesiecy", nie
        /// "about 0"); wies nie rosnie - dlaczego (kod PopulationLaw.RateOf). Zapas (przyrost naturalny wylaczony) - samo dorastanie. Jedno zdanie bez kropki.</summary>
        private static string YouthWords(LvQuote q)
        {
            double y = q != null ? q.Y : 0.0;
            bool fb = q == null || q.GFb;
            string tail = fb ? "" : " as the village grows";
            if (y >= 1.5) return "about " + Math.Round(y).ToString("0", Inv) + " young men come of age here each month" + tail;
            if (y >= 0.75) return "about one young man comes of age here each month" + tail;
            if (y > 1e-6) return "about one young man comes of age here every " + Math.Max(2, (int)Math.Round(1.0 / y)).ToString(Inv) + " months" + tail;
            if (fb) return "no young men come of age here now";
            switch (q.GWhy)
            {
                case "spalona": return "the village lies burned and is not growing";
                case "lupiona": return "the village is not growing while raiders are about";
                case "przymus": return "the village is not growing after it was squeezed by force";
                case "glod": return "the village is not growing now, its people go hungry";
                case "wojna": return "the village is not growing now, for war, raids and lawlessness";
                case "bieda": return "the village is not growing now, its land is too poor";
                default: return "the village is not growing now (hunger, war or lawlessness)";
            }
        }

        /// <summary>P11: powod odmowy (angielski dymek) i kod do linii doby; null - mozna werbowac. hide - opcja ukryta (wioska nieznana, bez rodzaju).</summary>
        private static string LevyGate(Settlement st, string uid, bool fief, out bool hide, out string code, out LvQuote q)
        {
            hide = false; code = null; q = null;
            if (NightRest.EnlistedNow) { code = "zaciag"; return "Your lord decides who joins his ranks."; }
            if (WatchNow(st) || _pbForce == "Straz") { code = "Straz"; return "The Watch takes no levies from these villages."; }
            if (st == null || st.Village == null || DistOf(Rec(uid)) == null) { hide = true; code = "nieznana"; return null; }
            string dn = Txt(st.Name);
            if (AtWar(st) || _pbForce == "wojna")
            {
                code = "wojna";
                return fief && SideVillageFief.IsMine(uid) ? "Your village lies in a realm at war with you - its men will not march against their lord."
                            : "No man here will take up arms for an enemy of " + (st.MapFaction != null ? Txt(st.MapFaction.Name) : dn) + ".";
            }
            if (st.Village.VillageState != Village.VillageStates.Normal) { code = "rabunek"; return "Raiders have been through " + dn + " - nobody leaves home now."; }
            q = LvQuoteFor(st, uid);
            if (q == null || q.Hide) { hide = true; code = "nieznana"; return null; }
            if (q.Burned || _pbForce == "spalona") { code = "spalona"; return "The village is burned - its people have no men to spare."; }
            if (!fief && !AtVillage()) { code = "miejsce"; return "You are not at the village."; }
            if (q.Idx <= 0) { code = "zaufanie"; return "The folk of " + NameOf(Rec(uid)) + " do not know you well enough to follow you - win the trust of the notables of " + dn + "."; }
            if (q.Room < 1) { code = "podloga"; return "The district has no one left to spare - its last homesteads keep their men."; }
            if (q.Callable < 1) { code = "pula"; return "Every man who could be spared is gone - " + YouthWords(q) + "."; }
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
                                           + "). " + q.Callable.ToString("N0", Inv) + " of its " + Num(q.P) + " people could still be spared; " + YouthWords(q)
                                           + ". Every man you take leaves its fields.");
                return true;
            }
            catch (Exception e) { LvStumble("Condition", e); return false; }
        }

        // recenzja: wyszukanie osady tez w try - wyjatek nigdy nie wychodzi z menu
        private static bool LevyMenuCondition(MenuCallbackArgs a)
        {
            try { return LevyCondition(a, DistOf(Rec(_uid)), _uid, false); }
            catch (Exception e) { LvStumble("MenuCondition", e); return false; }
        }

        private static void LevyMenuConsequence(MenuCallbackArgs a)
        {
            try { LevyOpen(DistOf(Rec(_uid)), _uid, false); }
            catch (Exception e) { LvStumble("MenuConsequence", e); }
        }

        /// <summary>Warunek opcji w menu wioski gracza (SideVillageFief): osada okregu szukana w try.</summary>
        internal static bool LevyConditionFief(MenuCallbackArgs a, string distId, string uid)
        {
            try
            {
                bool r = LevyCondition(a, Settlement.Find(distId ?? ""), uid, true);
                // n36: partia u tej wioski (w wsi gry okregu albo przy obrazku) - pobor nigdy z dalekiej wioski, nawet gdy menu wskazuje zla
                if (r && a.IsEnabled && !TownInns.HostNearNow(distId, uid)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You are not at the village."); NoteRefusal(uid, "miejsce"); }
                return r;
            }
            catch (Exception e) { LvStumble("ConditionFief", e); return false; }
        }

        /// <summary>Linia tekstu menu wioski: ilu mlodych mozna jeszcze wziac, ilu dorasta, etap. null - opcja nieczynna (klucz, ksiega, Straz), spalona albo ksiega jej nie zna.</summary>
        private static string LevyLine(Settlement st, string uid, VillageBook.PartView me, bool fief = false)
        {
            try
            {
                if (!LevyOn(fief) || !PeopleBook.LevyLive || me == null || st == null || WatchNow(st)) return null;
                var q = LvQuoteFor(st, uid);
                if (q == null || q.Hide || q.Burned) return null;   // spalona - zdanie o spaleniu jest w tekscie menu
                var sb = new StringBuilder(200);
                sb.Append(q.Callable == 1 ? "About one young man could still be called to arms here; " : "About " + q.Callable.ToString("N0", Inv) + " young men could still be called to arms here; ")
                  .Append(YouthWords(q)).Append('.');
                if (q.Stage == 2) sb.Append(" The willing are gone - what is left is a levy, and families want more for their sons.");
                else if (q.Stage >= 3) sb.Append(" Only the last men are left - a general levy.");
                return sb.ToString();
            }
            catch (Exception e) { LvStumble("Line", e); return null; }
        }

        /// <summary>Linia puli dla menu wioski gracza (SideVillageFief.MenuText). Nie rzuca.</summary>
        internal static string LevyLineFor(Settlement st, string uid)
        {
            try { VillageBook.PartView me; double h, n; return st != null && PartOf(st, uid, out me, out h, out n) ? LevyLine(st, uid, me, true) : null; }
            catch (Exception e) { LvStumble("LineFor", e); return null; }
        }

        // ------------------------------------------------------------ okno
        /// <summary>Okno werbunku: wiersze ilosci (1, 5, 10, 20, wszyscy - najwyzej min(ready, miejsce, zloto / cena, 40)), jeden wybor; po werbunku menu od nowa
        /// (tekst puli swiezy).</summary>
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
                var counts = new List<int>();
                foreach (int c in new[] { 1, 5, 10, 20 }) if (c < max) counts.Add(c);
                counts.Add(max);
                foreach (int c in counts)
                {
                    ImageIdentifier img = null;
                    try { img = new CharacterImageIdentifier(CharacterCode.CreateFrom(q.Troop)); } catch { img = null; }
                    list.Add(new InquiryElement(c, (c == max && c > 1 ? "All " : "") + c + (c == 1 ? " man" : " men") + " (" + tn + ") - " + (c * q.Price).ToString("N0", Inv) + " denars", img, true, null));
                }
                string stage = q.Stage >= 3 ? "Only the last men are left - a general levy. Their families ask dearly."
                             : q.Stage == 2 ? "The willing are gone; the rest are a levy - their families want more for them."
                             : "Young men of " + vn + " are willing to follow you.";
                string desc = stage + " Each man costs " + q.Price + " denars - the money goes to the families (the purse of " + dn + ").\n\nEvery man you take leaves its fields: fewer hands"
                              + " for its harvest, and he does not come back if he falls. " + Cap1(YouthWords(q)) + ".";
                if (max < q.Ready)
                    desc += " Now you can take " + max + " (" + (RoomNow() <= max ? "room in your party" : Hero.MainHero.Gold / Math.Max(1, q.Price) <= max ? "what you can pay" : "at most " + LvOfferMax + " at a time") + ").";
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Men of " + vn, desc, list, true, 1, 1, "Recruit", GameTexts.FindText("str_cancel").ToString(),
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0 || !(sel[0].Identifier is int)) return;
                            var res = LevyDo(st, uid, (int)sel[0].Identifier, fief ? "okno (wioska gracza)" : "okno");
                            // recenzja: tekst menu (pula, etap, ludzie) od nowa - ten sam wzor co okup (Raid.cs PayTribute)
                            if (res != null && res.Keep > 0 && Campaign.Current != null && Campaign.Current.CurrentMenuContext != null)
                                GameMenu.SwitchToMenu(fief ? SideVillageFief.Menu : Menu);
                        }
                        catch (Exception e) { LvStumble("Pick", e); }
                    }, null));
            }
            catch (Exception e) { LvStumble("Open", e); }
        }

        private static string Cap1(string t) { return string.IsNullOrEmpty(t) ? t : char.ToUpperInvariant(t[0]) + t.Substring(1); }

        internal static void LevyOpenFief(MenuCallbackArgs a)
        {
            try
            {
                string d = SideVillageFief.MenuDistId, u = SideVillageFief.MenuUid;   // n36: wioska otwartego menu (m113-zakup - kilka wiosek gracza)
                if (!TownInns.HostNearNow(d, u)) { Log.Player("You are not at the village.", true); return; }
                LevyOpen(Settlement.Find(d ?? ""), u, true);
            }
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
                try { res.Men1 = roster.GetTroopCount(q.Troop); } catch (Exception e) { LvStumble("Men1", e); }
                // 4. brak rak wioski i wpis puli - zaraz po zejsciu ludzi z DOM (recenzja: wyjatek w zaplacie nie zostawia puli bez ubytku)
                double sh = -1.0; LvRec r = null;
                try
                {
                    sh = StandShareOf(st, uid);
                    res.Share = sh;
                    if (sh >= 0.0 && got > 1e-9) { res.Lack = got * (1.0 - sh); LevyAddLack(uid, res.Lack); }
                    int today = Today();
                    r = LvGet(uid);
                    if (r == null)
                    {
                        r = new LvRec { Uid = uid, P = q.P, Pool = q.Cap, Grown = today, Day = today, Stage = q.Stage, Today = 0 };
                        if (!LvPut(r)) r = null;
                    }
                    if (r != null) { r.Pool = Math.Max(0.0, r.Pool - keep); r.Today += keep; r.Total += keep; res.Pool1 = r.Pool; }
                }
                catch (Exception e) { LvStumble("Pool", e); }
                _lvQ = null;
                // 5. pochodzenie
                try
                {
                    PeopleBook.GiveNew(roster, region, got, PeopleBook.FLevyPlayer);
                    if (keep - got > 1e-6) PeopleBook.GiveNone(roster, keep - got, PeopleBook.FNoneFloor);   // zabezpieczenie - Room w LevyGate trzyma k <= DOM ponad podloga
                }
                catch (Exception e) { LvStumble("Origin", e); }
                // 6. zaplata tylko za przybylych - zdarzenie gry gracz -> kiesa wsi okregu (osobny try - wyjatek sluchacza nie cofa werbunku)
                res.Total = keep * q.Price;
                try { if (res.Total > 0) GiveGoldAction.ApplyForCharacterToSettlement(hero, st, res.Total, true); }
                catch (Exception e) { LvStumble("Pay", e); }
                try { res.HeroGold1 = hero.Gold; res.Purse1 = st.Village.Gold; } catch (Exception e) { LvStumble("Gold1", e); }
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
                Log.Player((keep == 1 ? "One man of " + vn + " joins" : keep + " men of " + vn + " join") + " your party. " + (keep == 1 ? "His family got " : "Their families got ")
                           + res.Total.ToString("N0", Inv) + " denars.");
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
                if (m > 0 && r.Acc > 0.0 && !double.IsInfinity(r.Acc)) pool = Math.Min(LvPoolPct / 100.0 * P, pool + r.Acc);   // n36 (P3): przyrost w toku z dobowej stopy wsi
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

        /// <summary>P7 (recenzja): pobor gry z DOM wsi okregu (rekruci notabli MLevyVillage, A5) - ksiega rozklada ubytek na wszystkie czesci okregu wedlug ludzi w domu;
        /// wioska z wpisem (pula ponizej pulapu) traci z puli swoj udzial (to mlodzi). Bez wpisow w tym okregu - nic (bez PartsOf). PeopleBook.FromVillage po udanym Move,
        /// watek glowny. Nie rzuca.</summary>
        internal static void LevyNoteDistrictDraw(Village v, double got)
        {
            try
            {
                var d = _lv;
                if (!LevyKey || v == null || v.Settlement == null || !(got > 0.0) || d == null || d.Count == 0) return;
                string id = v.Settlement.StringId;
                bool any = false;
                foreach (var r0 in d.Values) { var rec = r0 != null ? Rec(r0.Uid) : null; if (rec != null && string.Equals(rec.VillageId, id, StringComparison.Ordinal)) { any = true; break; } }
                if (!any) return;
                var pv = new List<VillageBook.PartView>();
                if (!VillageBook.PartsOf(v, pv)) return;
                double all = 0.0;
                foreach (var p in pv) if (p != null) all += p.Home;
                if (!(all > 1e-9)) return;
                foreach (var p in pv)
                {
                    if (p == null || p.Main) continue;
                    var r = LvGet(p.Uid);
                    if (r == null) continue;
                    double dd = got * p.Home / all;
                    double was = r.Pool;
                    r.Pool = Math.Max(0.0, r.Pool - dd);
                    _ldVan += was - r.Pool;
                }
                _lvQ = null;
            }
            catch (Exception e) { LvStumble("DistrictDraw", e); }
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
        /// <summary>Doba: odnowa wpisow (n36, P3: dobowe dopisanie z przyrostu naturalnego wsi okregu, rozliczenie co 28 dob; P zapisane - bez PartsOf), pelne wpisy
        /// wylatuja, wygasle braki rak wylatuja; linia tylko z ruchem. Zwraca ruch.</summary>
        private static int LevyDaily()
        {
            long t0 = Stopwatch.GetTimestamp();
            int act = 0;
            try
            {
                int today = Today();
                var d = _lv;
                List<string> drop = null;
                var rs = new LvRateSum();
                foreach (var r in d.Values)
                {
                    if (r == null) continue;
                    double cap = LvPoolPct / 100.0 * r.P;
                    // n36 (P3): najpierw dzisiejsze dopisanie z przyrostu wsi okregu, potem rozliczenie miesiaca (doba 28. wchodzi do tego miesiaca)
                    bool fb; double gr = LvAccrue(r, today, out fb);
                    rs.Add(gr, fb, r.AccDay == today ? r.LastAdd : 0.0, r.LastWhy);
                    if ((today - r.Grown) / LvMonth > 0) { double g = LvGrow(r, cap, today); _ldDue++; _ldGrown += g; }
                    rs.Pending += r.Acc;
                    // recenzja: P z ostatniej wyceny bywa stare (brak rak wraca, P rosnie) - przed uznaniem za pelny P dzis z ksiegi (tylko wpisy prawie pelne)
                    VillageBook.VState vs;
                    if (r.Pool >= cap - 1e-6 && VillageBook.StateOf(r.Uid, out vs)) { r.P = Math.Max(0.0, vs.Home + vs.Away); cap = LvPoolPct / 100.0 * r.P; }
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
                _ldTicks += Stopwatch.GetTimestamp() - t0;   // czas doby bez sondy testu (POB-5)
                try { LevyProbeDaily(today); } catch (Exception e) { LvStumble("Probe", e); }
                act = _ldRec + _ldRefused.Count + _ldDue + _ldDrop + _ldLostN + _lvStDay;
                if (act > 0)
                {
                    var sb = new StringBuilder(400);
                    foreach (var kv in _ldWhy) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
                    double ms = _ldTicks * 1000.0 / Stopwatch.Frequency;
                    Log.Info(LP + "dzien " + (today - 1) + " | klucz " + (LevyKey ? "TAK" : "WYL") + " | werbunki " + _ldRec + " (ludzi " + _ldMen + ", zlota do kies wsi " + _ldGold + ", z DOM "
                             + _ldGot.ToString("0.##", Inv) + ", podloga " + _ldFloor.ToString("0.##", Inv) + ", brak rak +" + _ldLack.ToString("0.#", Inv) + ")"
                             + " | odmowy (wioska x powod, bez powtorzen): " + (sb.Length > 0 ? sb.ToString() : "-")
                             + " | pule: wpisow " + _lv.Count + ", przyrost mlodych w " + _ldDue + " (+" + _ldGrown.ToString("0.##", Inv) + "), odnowa: " + rs.Text() + ", pelne usuniete " + _ldDrop
                             + ", straty obroncow " + _ldLostN + " (" + _ldLost.ToString("0.#", Inv) + "), pobor gry z okregu -" + _ldVan.ToString("0.##", Inv) + " | braki rak " + _lvLack.Count + " wiosek (razem " + lackSum.ToString("0.#", Inv) + ")"
                             + (_ldEx.Count > 0 ? " | przyklady: " + string.Join("; ", _ldEx.ToArray()) : "") + " | czas doby " + ms.ToString("0.000", Inv) + " ms | potkniecia doby " + _lvStDay
                             + ", sesji " + _lvSt + ".");
                }
            }
            catch (Exception e) { LvStumble("Daily", e); act++; }
            return act;
        }

        /// <summary>n36 (P3): zbior stop odnowy wpisow do linii logu - zrodlo (przyrost naturalny wsi E4/4 albo zapas), g srednio / min / max, dopisane dzis, w toku.</summary>
        private sealed class LvRateSum
        {
            public int N, Fb, Err; public double Sum, Min = double.MaxValue, Max = double.MinValue, Added, Pending;
            public void Add(double g, bool fb, double added, string why)
            {
                Added += added;
                if (fb) { Fb++; if (why == "blad") Err++; return; }
                N++; Sum += g; if (g < Min) Min = g; if (g > Max) Max = g;
            }
            public string Text()
            {
                var s = Settings.Current;
                string cap = s != null ? Math.Max(0f, s.GrowthMaxPercent).ToString("0.##", Inv) : "?";
                var sb = new StringBuilder(160);
                if (N > 0)
                    sb.Append("przyrost naturalny wsi E4/4: g srednio ").Append((Sum / N).ToString("0.###", Inv)).Append("% (min ").Append(Min.ToString("0.###", Inv)).Append(", max ")
                      .Append(Max.ToString("0.###", Inv)).Append(") w ").Append(N).Append(" wpisach, pulap +").Append(cap).Append("%/rok");
                if (Fb > 0)
                    sb.Append(N > 0 ? "; " : "").Append("zapas SideRecruitYouthPercent ").Append(LvYouthPct.ToString("0.##", Inv)).Append("%/rok w ").Append(Fb)
                      .Append(" wpisach (").Append(!PopulationLaw.GrowthOn ? "przyrost naturalny wylaczony" : Err > 0 ? "przyrost naturalny nie policzyl wsi w " + Err : "wioska bez wsi okregu").Append(')');
                if (N + Fb == 0) sb.Append(PopulationLaw.GrowthOn ? "przyrost naturalny wsi E4/4 (pulap +" + cap + "%/rok), brak wpisow" : "zapas SideRecruitYouthPercent " + LvYouthPct.ToString("0.##", Inv) + "%/rok (przyrost naturalny wylaczony), brak wpisow");
                sb.Append(", dopisane dzis +").Append(Added.ToString("0.###", Inv)).Append(", w toku ").Append(Pending.ToString("0.##", Inv));
                return sb.ToString();
            }
        }

        /// <summary>Odcinek linii "Wioski poboczne (WP): start sesji".</summary>
        private static string LevySessionText()
        {
            double sum = 0.0, acc = 0.0; foreach (var r in _lv.Values) if (r != null) { sum += r.Pool; acc += r.Acc; }
            // n36 (P3): stopy odnowy wpisow z tej chwili (pamiec doby LvRate; bez dopisywania do Acc)
            var rs = new LvRateSum();
            try { foreach (var r in _lv.Values) if (r != null) { string w; bool fb; double g = LvRate(DistOf(Rec(r.Uid)), out w, out fb); rs.Add(g, fb, 0.0, w); } }
            catch (Exception e) { LvStumble("SessionRate", e); }
            rs.Pending = acc;
            return LP + "klucz Side Village Recruit " + (LevyKey ? "TAK" : "WYL") + " (pula " + LvPoolPct.ToString("0.#", Inv) + "% ludzi wioski, odnowa co " + LvMonth
                   + " dob z dobowej stopy wsi okregu: " + rs.Text() + " (rok " + LvYearDays() + " dob), dziennie " + LvDailyPct.ToString("0.#", Inv) + "% pulapu x zaufanie/6 x 1/0.5/0.25, ceny x1/x"
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
                bool first = true; int n = 0; double sum = 0.0, acc = 0.0;
                foreach (var r in _lv.Values)
                {
                    if (r == null || !UidOk(r.Uid)) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(r.Uid).Append(',').Append(r.Pool.ToString("R", Inv)).Append(',').Append(r.Grown.ToString(Inv)).Append(',').Append(r.Day.ToString(Inv)).Append(',')
                      .Append(r.Stage.ToString(Inv)).Append(',').Append(r.Today.ToString(Inv)).Append(',').Append(r.Total.ToString(Inv)).Append(',').Append(r.P.ToString("R", Inv))
                      .Append(',').Append(Math.Max(0.0, r.Acc).ToString("R", Inv));   // n36 (P3): przyrost w toku - starsza DLL czyta 8 pol i to pomija
                    n++; sum += r.Pool; acc += Math.Max(0.0, r.Acc);
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
                Log.Info(LP + "zapis arm_sidelevy - wpisy puli " + n + " (suma " + sum.ToString("0.##", Inv) + "), przyrost w toku " + acc.ToString("0.##", Inv) + ", braki rak " + nk + ".");
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
                            // n36 (P3): 9. pole - przyrost w toku; brak (sl1 sprzed n36) albo zle - 0 (pula pelna wraca i tak przez dobowe dopisanie)
                            double acc0 = 0.0;
                            if (a.Length >= 9 && double.TryParse(a[8], NumberStyles.Float, Inv, out acc0)) { if (double.IsNaN(acc0) || double.IsInfinity(acc0) || acc0 < 0.0) acc0 = 0.0; }
                            else acc0 = 0.0;
                            recs[a[0]] = new LvRec { Uid = a[0], Pool = Math.Max(0.0, pool), Grown = grown, Day = day, Stage = Math.Max(1, Math.Min(3, stage)), Today = Math.Max(0, tod), Total = Math.Max(0, tot), P = Math.Max(0.0, p), Acc = acc0 };
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
                _pbLoaded = recs.Count > 0;   // sonda: tryb "po wczytaniu" tylko z wpisami z zapisu
                double sum = 0.0, acc = 0.0; foreach (var r in recs.Values) { sum += r.Pool; acc += r.Acc; }
                double ls = 0.0; foreach (var v in lacks.Values) ls += v[0];
                Log.Info(LP + "wczytanie arm_sidelevy - wpisy puli " + recs.Count + " (suma " + sum.ToString("0.##", Inv) + "), przyrost w toku " + acc.ToString("0.##", Inv) + ", braki rak " + lacks.Count + " (zapisane " + ls.ToString("0.##", Inv) + ")"
                         + (bad > 0 ? ", zlych wpisow " + bad + " (pominiete)" : "") + ", " + data.Length + " znakow.");
            }
            catch (Exception e) { LevyReset(); LvStumble("Import", e); }
        }
    }
}
