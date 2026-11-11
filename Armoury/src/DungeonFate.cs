using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 402 (Jeff 11.11 "rob 3" - los jencow w lochu): DungeonFate 1 - TLOK LOCHU, zgony szeregowych, choroba bohaterow BEZ zgonow.
    ///
    /// Zakres: lochy twierdz i miast RODU GRACZA (OwnerClan == Clan.PlayerClan, IsFortification). Lochy AI - tylko licznik "ponad prog"
    /// w linii doby. Druzyna gracza poza v1. Zero latek Harmony: nasluchy DailyTickSettlementEvent, HeroPrisonerReleased, HeroKilledEvent.
    ///
    /// NACISK: f = jency / limit lochu; c = clamp((f - DungeonCrowdingFrom) / (1 - DungeonCrowdingFrom), 0, 2). Osada oblezona z czynna
    /// zaraza obozowa (CampFever.Active, nie oblezenie fabuly ROT) -> c = max(c, 1). MNOZNIK (dodawany, jak czynnik zarazy 399):
    /// 1 + ZIMA (CampFever.WinterTerm osady) + TRUPY W MURACH (SiegeMisery.CorpseTerm, tylko w oblezeniu), najwyzej CampFever.FactorMax.
    /// c == 0 -> sam pomiar. Osada w bitwie (st.Party.MapEvent != null) -> ta doba bez choroby.
    ///
    /// SZEREGOWI (ten sam model co oboz): chorzy doby = RoundRandomized(szeregowi x DungeonRegularSickPercent x c x mnoznik x (1 - MedicCut
    /// gubernatora)); z chorych CampFever.DeathShare() umiera (ranni pierwsi), reszta idzie w rannych. Zmarli: PeopleBook (FDiePlague, loch
    /// osady = KDungeon), w oblezonej twierdzy takze SiegeMisery.AddCorpses (w murach, zrodlo "loch"). Inni (wighty) nie choruja.
    ///
    /// BOHATEROWIE: jeniec IsLord z rodem, nie rod gracza, nie towarzysz, nie zakladnik 385c, bez czynnej rany Grave (HeroWounds).
    /// Zachorowanie na dobe: DungeonHeroSickChancePercent x c x mnoznik x (wiek >= 50: 1.5). Stan choroby WLASNY w zapisie (doba, koniec
    /// przebiegu DungeonSickDays); HP tylko znak - przy zachorowaniu HP = min(HP, WoundedHealthLimit), zadnej petli (gra leczy jenca do
    /// progu rany). Wczesniej zdrowieje, gdy loch 3 doby z rzedu ponizej progu, albo wpis znika przy uwolnieniu / przenosinach / smierci.
    /// Koniec przebiegu: rzut zgonu tylko LICZONY ("umarlby") - w 402 NIKT z bohaterow nie umiera (zgony = 404, przez HeroFate/StoryGuard);
    /// potem odpornosc 30 dob.
    ///
    /// 404 (DungeonFate 2): ZGONY BOHATEROW i KURIER ODPRAWIONY. Koniec przebiegu choroby: rzut zgonu = Dungeon Hero Death Share Percent
    /// (wiek >= 60: x2) x (1 - MedicCut gubernatora). Trafiony przy Dungeon Hero Deaths TAK -> bramki lochu (StoryGuard.Ready, sufit Dungeon
    /// Hero Deaths Per Week na loch na 7 dob), potem JEDNO API smierci: HeroFate.TryKill(h, Murdered, "dungeon", null, false) - tam straz fabuly,
    /// CanHeroDie, dziecko, wladca, ostatni z rodu, sufit roku LordToll, odroczenie (bitwa, misja, rozmowa, gracz w menu lochu). Kazda odmowa i
    /// kazde odroczenie -> stan "gravely ill": przebieg +5 dob BEZ drugiego rzutu, potem zdrowieje (nigdy petla smierci); powod liczony w linii doby.
    /// Zgon: relacja gracza z glowa rodu ofiary -30 i z wladca jej frakcji -5 (po jednym wywolaniu), zloto ofiary do glowy rodu droga gry.
    /// Grozba rodu: przy zachorowaniu glowy rodu / czlonka rodu panujacego jeden komunikat (raz na niewole jenca).
    /// Kurier: przelacznik w menu lochu per osada (klucz Ransom Courier Standing Refusal); bramka CourierRefused w Ransom178.ConsiderPrefix.
    /// Zapis dalej "v1": wiersz S ma pola 8-9 (odmowa kuriera, doby zgonow bohaterow), wiersz H pole 8 (gravely ill), nowe wiersze "T|bohater"
    /// (grozba wyslana) - zapis 402 czyta sie bez zmian (brak pol = puste).
    ///
    /// 409 (DungeonFate 3, plik DungeonFate.Talk.cs): WYROK GRACZA - rozmowa z jencem (slowo rodu, egzekucja), kolejka wykonywana w takcie mapy.
    /// Zapis dalej "v1": nowe wiersze "F|rod" (wasn), "P|bohater|doba" (przerwa po odmowie slowa), "Q|bohater|rodzaj|doba" (kolejka wyrokow).
    ///
    /// Zapis "arm_dungeon" v1 (brak klucza w starym zapisie = pusto). Reset w konstruktorze ArmouryBehavior i w OnGameEnd.
    /// Potkniecia liczone per miejsce - nic sie nie gasi.
    /// </summary>
    internal static partial class DungeonFate
    {
        internal const string Tag = "Loch (DF)";
        private const int ImmuneDays = 30;       // odpornosc po przebytej goraczce
        private const int BelowDaysToClear = 3;  // doby z rzedu ponizej progu - goraczka wygasa
        private const int CrowdWarnEvery = 30;   // ostrzezenie o tloku najwyzej raz na tyle dob na loch
        private const float CrowdWarnBelow = 0.10f;   // 402-p: ostrzezenie juz tyle PONIZEJ progu choroby (goraczka rusza od progu bez zwloki - ostrzezenie na progu przychodzilo razem z nia)
        private const int DeadMsgEvery = 7;      // komunikat o zmarlych szeregowych najwyzej raz na tyle dob na loch
        private const int GraveExtraDays = 5;    // 404: "gravely ill" - przebieg przedluzony o tyle dob, potem zdrowieje (bez drugiego rzutu)
        private const int DeathWindowDays = 7;   // 404: okno sufitu zgonow bohaterow na loch
        private const int KinRelation = -30, RulerRelation = -5;   // 404: relacja gracza z glowa rodu ofiary / wladca jej frakcji po zgonie w lochu
        private const string OptTown = "arm402_dungeon_state_town", OptCastle = "arm402_dungeon_state_castle";
        private const string OptCourierTown = "arm404_dungeon_courier_town", OptCourierCastle = "arm404_dungeon_courier_castle";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class HRow
        {
            public string Id, St;        // bohater, osada lochu (dla chorego)
            public bool Sick;            // true = choruje; false = sama odpornosc
            public bool Grave;           // 404: "gravely ill" - smierc odmowiona / odroczona, przebieg przedluzony; na koncu zdrowieje bez rzutu
            public int Day, End, Immune; // doba zachorowania, koniec przebiegu, odpornosc do (doba)
        }

        private sealed class SRec
        {
            public int WarnDay = -100000;     // doba ostatniego ostrzezenia o tloku
            public int Below;                 // doby z rzedu ponizej progu
            public bool Fever;                // goraczka ogloszona w tym epizodzie tloku
            public int DeadAcc;               // zmarli szeregowi od ostatniego komunikatu
            public int DeadMsgDay = -100000;  // doba ostatniego komunikatu o zmarlych
            public bool NoCourier;            // 404: kurierzy z okupem odprawiani w tym lochu (przelacznik gracza)
            public List<int> HeroDeaths;      // 404: doby zgonow bohaterow w tym lochu (tylko ostatnie DeathWindowDays) - sufit na tydzien
        }

        private static readonly Dictionary<string, HRow> _rows = new Dictionary<string, HRow>(StringComparer.Ordinal);
        private static readonly Dictionary<string, SRec> _recs = new Dictionary<string, SRec>(StringComparer.Ordinal);
        private static readonly List<string> _tmp = new List<string>();
        private static readonly List<string> _die = new List<string>();     // 404: trafieni rzutem zgonu w tej dobie lochu (wykonanie PO petli rosteru)
        private static readonly HashSet<string> _threat = new HashSet<string>(StringComparer.Ordinal);   // 404: jency, o ktorych rod juz przyslal grozbe (raz na niewole)
        private static int _noCourier;   // 404: liczba lochow z odprawianym kurierem (szybkie wyjscie CourierRefused)
        private static readonly HashSet<string> _present = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        // linia doby (tylko sesja)
        private static readonly StringBuilder _frag = new StringBuilder(256);
        private static int _dEsc, _dRansom, _dAiOver, _dStumbles, _dBattle, _dGone, _dWould, _dRegDead, _dNewSick, _dHealed;
        // 404: zgony bohaterow i powody wstrzymania (linia doby)
        private static int _dHeroDead, _dGrave, _hStory, _hRuler, _hLast, _hCanDie, _hCeil, _hBattle, _hPlayer, _hOther, _dCourier;
        private static int _stumbles;
        private static string _hookMenu = "-";

        // ---------------------------------------------------------------- cykl zycia

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie - przed SyncData) oraz OnGameEnd: stan i liczniki od zera.</summary>
        internal static void Reset()
        {
            _rows.Clear(); _recs.Clear(); _tmp.Clear(); _present.Clear(); _err.Clear(); _frag.Length = 0;
            _die.Clear(); _threat.Clear(); _noCourier = 0;
            _dEsc = _dRansom = _dAiOver = _dStumbles = _dBattle = _dGone = _dWould = _dRegDead = _dNewSick = _dHealed = 0;
            _dHeroDead = _dGrave = _hStory = _hRuler = _hLast = _hCanDie = _hCeil = _hBattle = _hPlayer = _hOther = _dCourier = 0;
            _stumbles = 0;
            TalkReset();   // 409
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("DungeonFate." + where, e); } catch { }
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }
        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static bool Mine(Settlement st) { return st != null && st.IsFortification && Clan.PlayerClan != null && st.OwnerClan == Clan.PlayerClan; }
        private static float From(Settings s) { return MathF.Clamp(s.DungeonCrowdingFrom, 0.05f, 0.99f); }

        private static SRec RecOf(string id)
        {
            SRec r;
            if (!_recs.TryGetValue(id, out r)) { r = new SRec(); _recs[id] = r; }
            return r;
        }

        // ---------------------------------------------------------------- odczyt dla innych

        /// <summary>Tylko odczyt: bohater choruje w lochu rodu gracza (wpis czynny).</summary>
        internal static bool Sick(Hero h)
        {
            HRow r;
            return _rows.Count != 0 && h != null && h.StringId != null && _rows.TryGetValue(h.StringId, out r) && r.Sick;
        }

        /// <summary>404: bramka kuriera (Ransom178.ConsiderPrefix, obok HoldCourier): true = jeniec siedzi w lochu osady rodu gracza, w ktorym gracz
        /// kazal odprawiac kurierow z okupem. Jeniec z rodu gracza nigdy. Bez odprawianych lochow - wyjscie na pierwszym porownaniu. Nie rzuca.</summary>
        internal static bool CourierRefused(Hero h)
        {
            try
            {
                if (_noCourier == 0 || h == null || h == Hero.MainHero || h.Clan == null || h.Clan == Clan.PlayerClan) return false;
                var s = Settings.Current;
                if (s == null || !s.RansomCourierStandingRefusal) return false;
                var cap = h.PartyBelongedToAsPrisoner;
                var st = cap != null && cap.IsSettlement ? cap.Settlement : null;
                if (!Mine(st) || st.StringId == null) return false;
                SRec r;
                if (!_recs.TryGetValue(st.StringId, out r) || !r.NoCourier) return false;
                _dCourier++;
                return true;
            }
            catch (Exception e) { Stumble("CourierRefused", e); return false; }
        }

        private static void CountNoCourier() { int n = 0; foreach (var r in _recs.Values) if (r.NoCourier) n++; _noCourier = n; }

        /// <summary>404: zgony bohaterow w tym lochu w ostatnich DeathWindowDays dobach (stare doby wypadaja z listy).</summary>
        private static int WeekDeaths(SRec rec, int day)
        {
            if (rec.HeroDeaths == null) return 0;
            rec.HeroDeaths.RemoveAll(d => day - d >= DeathWindowDays || d > day);
            return rec.HeroDeaths.Count;
        }

        private static Hero FindHero(TroopRoster pr, string heroId)
        {
            for (int k = 0; k < pr.Count; k++)
            {
                var ch = pr.GetCharacterAtIndex(k);
                if (ch != null && ch.IsHero && ch.HeroObject != null && ch.HeroObject.StringId == heroId) return ch.HeroObject;
            }
            return null;
        }

        private static int SickIn(string stId)
        {
            int n = 0;
            foreach (var r in _rows.Values) if (r.Sick && r.St == stId) n++;
            return n;
        }

        // ---------------------------------------------------------------- nasluchy

        /// <summary>CampaignEvents.HeroPrisonerReleased: pomiar ucieczek i okupow z lochow rodu gracza; wpis choroby znika (odpornosc zostaje). Nie rzuca.</summary>
        internal static void OnReleased(Hero h, PartyBase party, IFaction capturer, EndCaptivityDetail detail, bool notify)
        {
            try
            {
                if (h == null) return;
                if (party != null && party.IsSettlement && Mine(party.Settlement))
                {
                    if (detail == EndCaptivityDetail.ReleasedAfterEscape) _dEsc++;
                    else if (detail == EndCaptivityDetail.Ransom) _dRansom++;
                }
                if (detail != EndCaptivityDetail.Death && h.IsAlive) Drop(h, false);   // 404-p: zgon w lochu to nie "wpis zdjety (uwolniony / przeniesiony)" - wpis zmarlego zdejmuje OnKilled
                if (_threat.Count != 0 && h.StringId != null) _threat.Remove(h.StringId);   // 404: koniec niewoli - nastepna niewola moze dostac nowa grozbe
            }
            catch (Exception e) { Stumble("OnReleased", e); }
        }

        /// <summary>CampaignEvents.HeroKilledEvent: wpis zmarlego znika (takze odpornosc). Nie rzuca.</summary>
        internal static void OnKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                Drop(victim, true);
                if (_threat.Count != 0 && victim != null && victim.StringId != null) _threat.Remove(victim.StringId);
            }
            catch (Exception e) { Stumble("OnKilled", e); }
        }

        private static void Drop(Hero h, bool whole)
        {
            if (_rows.Count == 0 || h == null || h.StringId == null) return;
            HRow r;
            if (!_rows.TryGetValue(h.StringId, out r)) return;
            if (whole) { _rows.Remove(h.StringId); return; }
            if (!r.Sick) return;
            _dGone++;
            _rows.Remove(h.StringId);   // uwolniony w trakcie choroby: bez odpornosci (nie przebyl jej do konca)
        }

        /// <summary>CampaignEvents.DailyTickSettlementEvent: loch jednej osady. Wies / osada bez lochu - wyjscie od razu; loch AI - jedno porownanie. Nie rzuca.</summary>
        internal static void OnSettlementDay(Settlement st)
        {
            try
            {
                if (st == null || !st.IsFortification) return;
                var s = Settings.Current;
                if (s == null || !s.DungeonSickness) return;
                var party = st.Party;
                var pr = party != null ? party.PrisonRoster : null;
                if (pr == null) return;
                int total = pr.TotalManCount;
                bool mine = Mine(st);
                if (total <= 0 && !(mine && (_rows.Count != 0 || _recs.Count != 0))) return;
                int limit = Math.Max(1, party.PrisonerSizeLimit);
                float from = From(s);
                float f = total / (float)limit;
                if (!mine) { if (f >= from) _dAiOver++; return; }
                TickMine(s, st, pr, total, limit, f, from);
            }
            catch (Exception e) { Stumble("OnSettlementDay", e); }
        }

        // ---------------------------------------------------------------- doba lochu rodu gracza

        private static void TickMine(Settings s, Settlement st, TroopRoster pr, int total, int limit, float f, float from)
        {
            string id = st.StringId;
            if (string.IsNullOrEmpty(id)) return;
            int day = Today();

            if (st.Party.MapEvent != null)   // szturm / wypad: roster nalezy do bitwy, a jeniec umarlby bez odroczenia - ta doba bez choroby
            {
                _dBattle++;
                if (total > 0) Frag(id + ": jency " + total + "/" + limit + ", bitwa - doba pominieta");
                return;
            }

            var rec = RecOf(id);
            bool warn = s.DungeonWarnings;

            // nacisk i mnoznik
            float c = MathF.Clamp((f - from) / (1f - from), 0f, 2f);
            float winter = CampFever.WinterTerm(st);
            float corpse = 0f; bool siegeFever = false;
            var siege = st.SiegeEvent;
            bool storySiege = siege != null && CampFever.Spared(siege) != null;   // 402-p: jedna bramka dla czlonu trupow i dla dopisywania trupow
            if (siege != null && !storySiege)   // oblezenie fabuly ROT: bez zarazy, wiec i loch bez dodatku
            {
                if (CampFever.Active(st)) { siegeFever = true; if (c < 1f) c = 1f; }
                corpse = SiegeMisery.CorpseTerm(st, true, WallMen(st));
            }
            float mult = MathF.Min(CampFever.FactorMax, 1f + Math.Max(0f, winter) + Math.Max(0f, corpse));

            // seria dob ponizej progu
            if (f < from && !siegeFever) rec.Below++; else rec.Below = 0;
            bool clear = rec.Below >= BelowDaysToClear;
            if (clear) rec.Fever = false;

            var gov = st.Town != null ? st.Town.Governor : null;
            int med = 0;
            try { med = gov != null ? gov.GetSkillValue(DefaultSkills.Medicine) : 0; } catch { med = 0; }
            float cut = CampFever.MedicCut(med);

            // spis lochu
            int heroes = 0, regulars = 0, wounded = 0;
            for (int i = 0; i < pr.Count; i++)
            {
                var ch = pr.GetCharacterAtIndex(i);
                if (ch == null) continue;
                if (ch.IsHero) { heroes++; continue; }
                if (PeopleBook.IsWight(ch)) continue;
                regulars += pr.GetElementNumber(i);
                wounded += pr.GetElementWoundedNumber(i);
            }

            // ostrzezenie o tloku (zanim goraczka wybuchnie). 402-p: juz od progu - CrowdWarnBelow, zeby gracz mial czas rozlozyc jencow
            if (warn && f >= from - CrowdWarnBelow && !rec.Fever && day - rec.WarnDay >= CrowdWarnEvery)
            {
                rec.WarnDay = day;
                if (f >= from) Log.Player("The dungeon of " + Nm(st) + " is crowded (" + total + " held, room for " + limit + "). Fever will come if it stays so.", true);
                else Log.Player("The dungeon of " + Nm(st) + " is filling up (" + total + " held, room for " + limit + "). Above " + ((int)Math.Round(from * 100f)) + "% of its room fever will come.", true);
                Log.Info(Tag + ": ostrzezenie o tloku w lochu " + id + " (jency " + total + "/" + limit + ", prog choroby " + from.ToString("0.00", Inv) + ", ostrzezenie od " + (from - CrowdWarnBelow).ToString("0.00", Inv) + ").");
            }

            // --- szeregowi ---
            int sick = 0, dead = 0;
            if (c > 0f && regulars > 0)
            {
                try
                {
                    float exp = regulars * Math.Max(0f, s.DungeonRegularSickPercent) / 100f * c * mult * (1f - cut);
                    sick = Math.Min(regulars, MBRandom.RoundRandomized(exp));
                    if (sick > 0)
                    {
                        int want = Math.Min(sick, MBRandom.RoundRandomized(sick * CampFever.DeathShare()));
                        for (int n = 0; n < want; n++) if (HitRandom(pr, true)) dead++;
                        for (int n = sick - want; n > 0; n--) if (!HitRandom(pr, false)) break;   // brak zdrowych - reszta chorych juz lezy
                        if (dead > 0)
                        {
                            PeopleBook.DieShares(PeopleBook.Take(pr, dead, PeopleBook.FDiePlague), PeopleBook.FDiePlague);   // ksiega ludzi: polegli regionu pochodzenia (wzor CampFever)
                            if (siege != null && !storySiege) SiegeMisery.AddCorpses(st, true, dead, SiegeMisery.SrcDungeon);   // 399: znacznik sanitarny w murach (nie rzuca; nie ksiega). 402-p: nie w oblezeniu fabuly ROT
                            rec.DeadAcc += dead; _dRegDead += dead;
                        }
                    }
                }
                catch (Exception e) { Stumble("Regulars", e); }
            }

            // --- bohaterowie ---
            int hSick = 0, hNew = 0, hHealed = 0, hWould = 0;
            _present.Clear();
            _tmp.Clear();
            for (int i = 0; i < pr.Count; i++)
            {
                var ch = pr.GetCharacterAtIndex(i);
                var h = ch != null && ch.IsHero ? ch.HeroObject : null;
                if (h == null || h.StringId == null) continue;
                _present.Add(h.StringId);
                try
                {
                    HRow row;
                    _rows.TryGetValue(h.StringId, out row);
                    if (row != null && row.Sick)
                    {
                        if (row.St != id) { _rows.Remove(h.StringId); _dGone++; continue; }   // przenosiny (takze miedzy lochami rodu gracza) koncza chorobe - jedna regula
                        if (clear)
                        {
                            _rows.Remove(h.StringId); hHealed++;
                            if (warn) Log.Player(Nm(h) + " has recovered from the fever.");
                            continue;
                        }
                        if (day >= row.End)
                        {
                            if (row.Grave)
                            {
                                // 404: "gravely ill" dobiegl konca - zdrowieje BEZ drugiego rzutu (nigdy petla smierci)
                                row.Sick = false; row.Grave = false; row.Immune = day + ImmuneDays; hHealed++;
                                Log.Info(Tag + ": " + Nm(h) + " [" + h.StringId + "] w lochu " + id + " - wyszedl z ciezkiej goraczki po " + (day - row.Day) + " dobach (bez rzutu) | odpornosc do doby " + row.Immune + ".");
                                if (warn) Log.Player(Nm(h) + " has recovered from the fever.");
                                continue;
                            }
                            // koniec przebiegu: rzut zgonu (404). Trafiony przy wlaczonych zgonach idzie na liste - wykonanie PO petli (smierc zdejmuje jenca z rosteru).
                            bool elig = Eligible(h);   // zakladnik 385c / rana Grave / rod gracza nabyte w trakcie choroby: bez rzutu
                            float p = MathF.Clamp(Math.Max(0f, s.DungeonHeroDeathSharePercent) / 100f * (h.Age >= 60f ? 2f : 1f) * (1f - cut), 0f, 1f);
                            float roll = MBRandom.RandomFloat;
                            bool would = elig && roll < p;
                            if (would && s.DungeonHeroDeaths)
                            {
                                _die.Add(h.StringId);
                                Log.Info(Tag + ": " + Nm(h) + " [" + h.StringId + "] w lochu " + id + " - koniec goraczki po " + (day - row.Day) + " dobach, rzut " + roll.ToString("0.00", Inv)
                                         + " przy p " + p.ToString("0.00", Inv) + " (wiek " + ((int)h.Age) + ", medyk -" + (cut * 100f).ToString("0", Inv) + "%) -> TRAFIONY, pytam bramki.");
                                hSick++;
                                continue;
                            }
                            if (would) hWould++;
                            row.Sick = false; row.Immune = day + ImmuneDays; hHealed++;
                            string why = null; int guard = 0;
                            try { guard = StoryGuard.State(h, out why); } catch { guard = 0; }
                            Log.Info(Tag + ": " + Nm(h) + " [" + h.StringId + "] w lochu " + id + " - koniec goraczki po " + (day - row.Day) + " dobach, rzut " + roll.ToString("0.00", Inv)
                                     + " przy p " + p.ToString("0.00", Inv) + " -> " + (would ? "UMARLBY (Dungeon Hero Deaths NIE)" : elig ? "przezyl" : "przezyl (bez rzutu: zakladnik / rana Grave / rod gracza)")
                                     + " | straz fabuly: " + (guard == 2 ? "tak (" + why + ")" : "nie") + " | odpornosc do doby " + row.Immune + ".");
                            if (warn) Log.Player(Nm(h) + " has recovered from the fever.");
                            continue;
                        }
                        hSick++;
                        continue;
                    }
                    if (row != null && day >= row.Immune) { _rows.Remove(h.StringId); row = null; }   // odpornosc minela
                    if (row != null || c <= 0f || !Eligible(h)) continue;
                    float chance = Math.Max(0f, s.DungeonHeroSickChancePercent) / 100f * c * mult * (h.Age >= 50f ? 1.5f : 1f);
                    if (!(MBRandom.RandomFloat < chance)) continue;
                    _tmp.Add(h.StringId);   // zachoruje po petli (seter HP rusza wersje rosteru)
                }
                catch (Exception e) { Stumble("Hero", e); }
            }
            // 404: trafieni rzutem zgonu - bramki lochu, potem jedno API smierci (HeroFate.TryKill). Poza petla rosteru: smierc zdejmuje jenca z listy,
            // a HeroKilledEvent wola nasz OnKilled (wpis znika) - tu nie iterujemy ani rosteru, ani _rows.
            int hDead = 0, hGrave = 0;
            for (int i = 0; i < _die.Count; i++)
            {
                HRow row = null;
                bool done = false;   // 404-p: wiersz rozstrzygniety (zgon albo gravely ill) - potkniecie PRZED tym tez konczy sie gravely ill, nigdy drugim rzutem
                try
                {
                    string hid = _die[i];
                    if (!_rows.TryGetValue(hid, out row) || !row.Sick) { row = null; continue; }
                    Hero h = FindHero(pr, hid);
                    if (h == null)
                    {
                        // trafiony zniknal z rosteru miedzy petlami (nie powinno sie zdarzyc): bez rzutu jutro
                        row.Grave = true; row.End = day + GraveExtraDays; done = true; hGrave++; _hOther++;
                        Log.Info(Tag + ": [" + hid + "] w lochu " + id + " - trafiony rzutem, ale nie ma go w rosterze -> ciezko chory do doby " + row.End + " (bez drugiego rzutu).");
                        continue;
                    }
                    string why;
                    bool killed = false;
                    Clan clan = h.Clan;
                    string name = Nm(h), clanName = clan != null && clan.Name != null ? clan.Name.ToString() : "?";
                    Hero ruler = null;
                    try { ruler = clan != null && clan.Kingdom != null ? clan.Kingdom.Leader : null; } catch { ruler = null; }
                    int cap = Math.Max(0, s.DungeonHeroDeathsPerWeek), week = WeekDeaths(rec, day);
                    if (!StoryGuard.Ready) { why = "straz fabuly niegotowa (Story Guard wylaczony albo lista wydarzen ROT niewidoczna)"; _hStory++; }
                    else if (week >= cap) { why = "sufit lochu (" + week + "/" + cap + " na " + DeathWindowDays + " dob)"; _hCeil++; }
                    else
                    {
                        var fate = HeroFate.TryKill(h, KillCharacterAction.KillCharacterActionDetail.Murdered, LordToll.Dungeon, null, false, out why);
                        killed = fate == HeroFate.Fate.Killed;
                        if (!killed) CountHold(fate, why);
                    }
                    if (killed)
                    {
                        hDead++; hSick--;
                        if (rec.HeroDeaths == null) rec.HeroDeaths = new List<int>(2);
                        rec.HeroDeaths.Add(day);
                        _rows.Remove(hid); _threat.Remove(hid);   // OnKilled juz to zrobil; drugi raz nie szkodzi (takze przy znaku smierci zamiast smierci)
                        done = true;
                        string rel = AfterDeath(h, clan, ruler);
                        // 404-p: nekrolog gry dla Murdered mowi "was assassinated" (cs_full.cs:227446) - nadpisany zdaniem o goraczce (Hero.EncyclopediaText ma publiczny seter)
                        try { if (!h.IsAlive) h.EncyclopediaText = new TextObject("{=!}" + (h.IsFemale ? "She" : "He") + " died of fever in captivity at " + Nm(st) + " in " + CampaignTime.Now.GetYear.ToString(Inv) + " at the age of " + ((int)h.Age).ToString(Inv) + "."); }
                        catch (Exception e) { Stumble("Obituary", e); }
                        Log.Info(Tag + ": " + name + " [" + hid + "] ZMARL od goraczki w lochu " + id + " (" + why + ") | rod " + clanName + " | relacje: " + rel
                                 + " | zgony boh. w tym lochu w " + DeathWindowDays + " dobach " + rec.HeroDeaths.Count + "/" + cap + " | LordToll rok " + LordToll.Year + ": loch " + LordToll.Count(LordToll.Dungeon) + ", wspolny " + LordToll.Total + "/" + LordToll.MaxAll + ".");
                        Log.Player(name + " has died of fever in your dungeon at " + Nm(st) + ". House " + clanName + " will not forget it.", true);   // zawsze (takze przy Dungeon Warnings NIE): smierc jenca gracza
                        continue;
                    }
                    // odmowa / odroczenie: "gravely ill" - przebieg +5 dob, potem zdrowieje bez rzutu
                    row.Grave = true; row.End = day + GraveExtraDays; hGrave++; done = true;
                    Log.Info(Tag + ": " + name + " [" + hid + "] w lochu " + id + " - smierc WSTRZYMANA: " + (why ?? "?") + " -> ciezko chory do doby " + row.End + ", potem zdrowieje (bez drugiego rzutu).");
                    if (warn) Log.Player(name + " lies gravely ill in your dungeon at " + Nm(st) + ".", true);
                }
                catch (Exception e)
                {
                    Stumble("HeroDeath", e);
                    // 404-p: blad przed rozstrzygnieciem = "gravely ill" jak kazda odmowa (inaczej wiersz Sick z minionym koncem losowalby zgon co dobe)
                    if (!done && row != null && row.Sick) { row.Grave = true; row.End = day + GraveExtraDays; hGrave++; _hOther++; }
                }
            }
            _die.Clear();
            _dHeroDead += hDead; _dGrave += hGrave;

            for (int i = 0; i < _tmp.Count; i++)
            {
                try
                {
                    Hero h = FindHero(pr, _tmp[i]);
                    if (h == null) continue;
                    int days = Math.Max(1, s.DungeonSickDays);
                    _rows[h.StringId] = new HRow { Id = h.StringId, St = id, Sick = true, Day = day, End = day + days, Immune = 0 };
                    hNew++; hSick++;
                    int lim = h.WoundedHealthLimit;
                    if (h.HitPoints > lim) h.HitPoints = lim;   // tylko znak "ranny" - bez petli; gra leczy jenca tylko do progu rany
                    string why = null; int guard = 0;
                    try { guard = StoryGuard.State(h, out why); } catch { guard = 0; }
                    Log.Info(Tag + ": " + Nm(h) + " [" + h.StringId + "] zachorowal w lochu " + id + " (wiek " + ((int)h.Age) + ", nacisk " + c.ToString("0.00", Inv) + ", mnoznik " + mult.ToString("0.00", Inv)
                             + "), przebieg do doby " + (day + days) + " | straz fabuly: " + (guard == 2 ? "tak (" + why + ")" : "nie") + ".");
                    if (warn) Log.Player(Nm(h) + " has fallen ill in your dungeon at " + Nm(st) + ".", true);
                    Threat(s, h, id);
                }
                catch (Exception e) { Stumble("HeroSick", e); }
            }
            _tmp.Clear();

            // chorzy z wpisem tego lochu, ktorych juz tu nie ma (przenosiny poza lochy rodu gracza, zdjecie z listy bez zdarzenia): wpis znika
            if (_rows.Count != 0)
            {
                foreach (var kv in _rows) if (kv.Value.Sick && kv.Value.St == id && !_present.Contains(kv.Key)) _tmp.Add(kv.Key);
                for (int i = 0; i < _tmp.Count; i++) { _rows.Remove(_tmp[i]); _dGone++; }
                _tmp.Clear();
            }
            _present.Clear();

            // komunikaty lochu
            if (!rec.Fever && (sick > 0 || hNew > 0))
            {
                rec.Fever = true;
                if (warn) Log.Player("Fever has broken out in the dungeon of " + Nm(st) + ".", true);
                Log.Info(Tag + ": goraczka wybuchla w lochu " + id + " (jency " + total + "/" + limit + ", nacisk " + c.ToString("0.00", Inv) + ").");
            }
            if (rec.DeadAcc > 0 && day - rec.DeadMsgDay >= DeadMsgEvery)
            {
                if (warn) Log.Player(rec.DeadAcc + (rec.DeadAcc == 1 ? " prisoner died" : " prisoners died") + " of fever in the dungeon of " + Nm(st) + ".", true);
                rec.DeadAcc = 0; rec.DeadMsgDay = day;
            }

            _dNewSick += hNew; _dHealed += hHealed; _dWould += hWould;
            if (total > 0 || hHealed > 0)
                Frag(id + ": jency " + total + "/" + limit + " (boh. " + heroes + ", szer. " + regulars + ", ranni " + wounded + "), nacisk " + c.ToString("0.00", Inv)
                     + " (zima " + winter.ToString("0.00", Inv) + ", oblezenie " + (siege == null ? "nie" : storySiege ? "fabuly ROT (bez dodatku)" : siegeFever ? "zaraza" : "tak") + ", trupy " + corpse.ToString("0.00", Inv) + ", medyk -" + (cut * 100f).ToString("0", Inv) + "%)"
                     + ", chorych boh. " + hSick + " (+" + hNew + ", zdrowi " + hHealed + "), szer. chorzy " + sick + ", zgony szer. " + dead + ", zgony boh. " + hDead + ", ciezko chorzy (smierc wstrzymana) +" + hGrave
                     + ", umarlby " + hWould + (rec.NoCourier ? (s.RansomCourierStandingRefusal ? ", kurier odprawiany" : ", kurier: odmowa zapisana, klucz NIE (bramka nie dziala)") : ""));
        }

        /// <summary>404: powod wstrzymania smierci (napis HeroFate.TryKill / StoryGuard.MayDie) do licznikow linii doby.</summary>
        private static void CountHold(HeroFate.Fate fate, string why)
        {
            string w = why ?? "";
            if (fate == HeroFate.Fate.Deferred)
            {
                if (w.IndexOf("bitwa", StringComparison.Ordinal) >= 0 || w.IndexOf("oblez", StringComparison.Ordinal) >= 0) _hBattle++;
                else _hPlayer++;   // misja, rozmowa, spotkanie gracza, gracz w menu lochu, blad oceny
                return;
            }
            if (w.StartsWith("straz fabuly", StringComparison.Ordinal)) _hStory++;
            else if (w.IndexOf("(wladca)", StringComparison.Ordinal) >= 0) _hRuler++;
            else if (w.IndexOf("(ostatni z rodu)", StringComparison.Ordinal) >= 0) _hLast++;
            else if (w.IndexOf("CanHeroDie", StringComparison.Ordinal) >= 0) _hCanDie++;
            else if (w.StartsWith("sufit roku", StringComparison.Ordinal)) _hCeil++;
            else _hOther++;
        }

        /// <summary>404: skutki zgonu jenca w lochu gracza - relacja gracza z glowa rodu ofiary (-30) i z wladca jej frakcji (-5), po jednym wywolaniu,
        /// bez petli po przyjaciolach i bez Honoru. Glowa rodu czytana PO smierci (gra wybrala juz nastepce, gdy zmarl wodz rodu). Zwraca opis do logu.</summary>
        private static string AfterDeath(Hero victim, Clan clan, Hero rulerBefore)
        {
            var sb = new StringBuilder(96);
            try
            {
                Hero head = clan != null && !clan.IsEliminated ? clan.Leader : null;
                if (head != null && head != victim && head.IsAlive && head != Hero.MainHero)
                {
                    int before = (int)head.GetRelationWithPlayer();
                    ChangeRelationAction.ApplyPlayerRelation(head, KinRelation, false, true);
                    sb.Append("glowa rodu ").Append(Nm(head)).Append(' ').Append(before).Append("->").Append((int)head.GetRelationWithPlayer());
                }
                else sb.Append("glowa rodu - brak (rod bez zywej glowy)");
                Hero ruler = null;
                try { ruler = clan != null && !clan.IsEliminated && clan.Kingdom != null ? clan.Kingdom.Leader : null; } catch { ruler = null; }
                if (ruler == null || ruler == victim || !ruler.IsAlive) ruler = rulerBefore;
                if (ruler != null && ruler != victim && ruler.IsAlive && ruler != head && ruler != Hero.MainHero)
                {
                    int before = (int)ruler.GetRelationWithPlayer();
                    ChangeRelationAction.ApplyPlayerRelation(ruler, RulerRelation, false, false);
                    sb.Append(", wladca ").Append(Nm(ruler)).Append(' ').Append(before).Append("->").Append((int)ruler.GetRelationWithPlayer());
                }
                else sb.Append(", wladca - brak / ten sam co glowa rodu");
            }
            catch (Exception e) { Stumble("AfterDeath", e); sb.Append(" (blad - patrz log bledow)"); }
            return sb.ToString();
        }

        /// <summary>404: grozba rodu - przy zachorowaniu glowy rodu albo czlonka rodu panujacego jeden komunikat (raz na niewole jenca).
        /// Sam komunikat; skutkiem jest kara relacji przy zgonie (AfterDeath). Nie rzuca.</summary>
        private static void Threat(Settings s, Hero h, string stId)
        {
            try
            {
                if (s == null || !s.DungeonHeroDeaths || !s.DungeonWarnings || h == null || h.StringId == null) return;
                var clan = h.Clan;
                if (clan == null || clan == Clan.PlayerClan || clan.IsEliminated) return;
                bool head = clan.Leader == h;
                bool royal = clan.Kingdom != null && clan.Kingdom.RulingClan == clan;
                if (!head && !royal) return;
                if (!_threat.Add(h.StringId)) return;
                string cn = clan.Name != null ? clan.Name.ToString() : "?";
                Log.Player("House " + cn + " sends word: if " + Nm(h) + " dies in your cells, there will be no friendship between us.", true);
                Log.Info(Tag + ": grozba rodu " + cn + " za " + Nm(h) + " [" + h.StringId + "] w lochu " + stId + " (" + (head ? "glowa rodu" : "rod panujacy") + ").");
            }
            catch (Exception e) { Stumble("Threat", e); }
        }

        private static void Frag(string t)
        {
            if (_frag.Length > 6000) return;   // linia doby ma granice (wielu lochow rodu gracza nie bedzie; bezpiecznik)
            if (_frag.Length > 0) _frag.Append(" | ");
            _frag.Append(t);
        }

        /// <summary>Bohater, ktory moze zachorowac: lord z rodem, nie rod gracza, nie towarzysz, nie dziecko, nie Inny (404-p), nie zakladnik 385c, bez czynnej rany Grave.</summary>
        private static bool Eligible(Hero h)
        {
            if (h == null || !h.IsAlive || !h.IsPrisoner || h == Hero.MainHero) return false;
            if (!h.IsLord || h.Clan == null || h.Clan == Clan.PlayerClan || h.IsPlayerCompanion || h.IsChild) return false;
            if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) return false;
            // 404-p: Inni (Bialy Wedrowiec Race 1 / 2, rod kultury whitewalker) nie choruja i nie umieraja od goraczki - jak szeregowi Inni (PeopleBook.IsWight)
            // i jak rany (HeroWounds). StoryGuard celowo ZWALNIA Wedrowcow, a ROT CanHeroDie przepuszcza Murdered po starcie inwazji (rot_all.cs:31279-31295).
            var co = h.CharacterObject;
            if ((co != null && (co.Race == 1 || co.Race == 2)) || OthersRise.IsWhiteWalkerClan(h.Clan)) return false;
            if (PrisonerTerms384.HostageGuardedNow(h)) return false;
            if (HeroWounds.GradeOf(h) == HeroWounds.Grave) return false;   // ranny Grave ma wlasne przesilenie (403) - jedna regula smierci rannego jenca
            return true;
        }

        /// <summary>Ludzie w murach (zaloga + milicja + druzyny lordow w osadzie) - mianownik czlonu trupow, jak w CampFever.</summary>
        private static int WallMen(Settlement st)
        {
            int n = 0;
            try
            {
                var town = st.Town;
                if (town != null && town.GarrisonParty != null && town.GarrisonParty.MemberRoster != null) n += town.GarrisonParty.MemberRoster.TotalManCount;
                var mc = st.MilitiaPartyComponent;
                var militia = mc != null ? mc.MobileParty : null;
                if (militia != null && militia.IsActive && militia.MemberRoster != null) n += militia.MemberRoster.TotalManCount;
                foreach (var mp in st.Parties)
                    if (mp != null && mp.IsLordParty && mp.CurrentSettlement == st && mp.MemberRoster != null) n += mp.MemberRoster.TotalManCount;
            }
            catch (Exception e) { Stumble("WallMen", e); }
            return n;
        }

        /// <summary>Losowy szeregowy jeniec (bez bohaterow i Innych), wazony liczebnoscia. kill: ranni pierwsi (potem zdrowi), zdjety z lochu;
        /// inaczej zdrowy idzie w rannych. false = nie bylo kogo.</summary>
        private static bool HitRandom(TroopRoster roster, bool kill)
        {
            int poolW = 0, poolH = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || PeopleBook.IsWight(ch)) continue;
                int w = roster.GetElementWoundedNumber(i);
                poolW += w; poolH += roster.GetElementNumber(i) - w;
            }
            bool fromWounded = kill && poolW > 0;
            int pool = fromWounded ? poolW : poolH;
            if (pool <= 0) return false;
            int pick = MBRandom.RandomInt(pool);
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || PeopleBook.IsWight(ch)) continue;
                int w = roster.GetElementWoundedNumber(i);
                int k = fromWounded ? w : roster.GetElementNumber(i) - w;
                if (k <= 0) continue;
                if (pick >= k) { pick -= k; continue; }
                if (kill) roster.AddToCounts(ch, -1, false, fromWounded ? -1 : 0);
                else roster.WoundTroop(ch);
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- linia doby (z doby ArmouryBehavior, po glodzie i zwijaniu oblezen)

        /// <summary>Linia "Loch (DF): doba" z tego, co zebraly osady od poprzedniej linii; porzadki wpisow (loch juz nie rodu gracza, bohater
        /// martwy / wolny, odpornosc miniona, rekord pustego lochu). Bez tresci - bez linii. Nie rzuca.</summary>
        internal static void DailyLine()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                int day = Today();
                TalkDaily(day);   // 409: minione przerwy po odmowie slowa precz
                if (_rows.Count != 0)
                {
                    _tmp.Clear();
                    foreach (var kv in _rows)
                    {
                        var r = kv.Value;
                        try
                        {
                            if (!r.Sick) { if (day >= r.Immune) _tmp.Add(kv.Key); continue; }
                            Settlement st = null;
                            try { st = Settlement.Find(r.St); } catch { st = null; }
                            if (!s.DungeonSickness || !Mine(st)) { _tmp.Add(kv.Key); _dGone++; }
                        }
                        catch (Exception e) { Stumble("DailyLine.Row", e); }
                    }
                    for (int i = 0; i < _tmp.Count; i++) _rows.Remove(_tmp[i]);
                    _tmp.Clear();
                }
                if (_recs.Count != 0)
                {
                    _tmp.Clear();
                    foreach (var kv in _recs)
                    {
                        Settlement st = null;
                        try { st = Settlement.Find(kv.Key); } catch { st = null; }
                        if (!Mine(st)) _tmp.Add(kv.Key);
                    }
                    for (int i = 0; i < _tmp.Count; i++) _recs.Remove(_tmp[i]);
                    if (_tmp.Count > 0) CountNoCourier();   // utracona twierdza: odmowa kuriera znika razem z rekordem
                    _tmp.Clear();
                }

                if (!s.DungeonSickness)
                {
                    // 404-p: sama bramka kuriera (choroba wylaczona) - krotka linia zamiast pelnej z zerami
                    if (_dCourier > 0)
                        Log.Info(Tag + ": doba " + day + " | Dungeon Sickness NIE | kurier odprawiony " + _dCourier + " (jencow-dob; lochy z odmowa " + _noCourier + ") | ucieczki z lochow gracza dzis " + _dEsc + ", okupy " + _dRansom
                                 + " | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
                }
                else if (_frag.Length > 0 || _dEsc + _dRansom + _dAiOver + _dStumbles + _dGone + _dCourier > 0)
                    Log.Info(Tag + ": doba " + day + " | " + (_frag.Length > 0 ? _frag.ToString() : "lochy rodu gracza puste")
                             + " | razem: nowi chorzy boh. " + _dNewSick + ", zdrowi " + _dHealed + ", umarlby " + _dWould + ", zgony szer. " + _dRegDead + ", wpisy zdjete (uwolnieni / przeniesieni) " + _dGone
                             + ", lochy w bitwie " + _dBattle
                             + " | zgony boh. " + _dHeroDead + " (LordToll rok " + LordToll.Year + ": loch " + LordToll.Count(LordToll.Dungeon) + ", wspolny " + LordToll.Total + "/" + LordToll.MaxAll + "), ciezko chorzy +" + _dGrave
                             + " | wstrzymane: fabula " + _hStory + ", wladca " + _hRuler + ", ostatni z rodu " + _hLast + ", CanHeroDie " + _hCanDie + ", sufit " + _hCeil + ", bitwa " + _hBattle + ", gracz na miejscu " + _hPlayer + ", inne " + _hOther
                             + " | ucieczki z lochow gracza dzis " + _dEsc + ", okupy " + _dRansom + ", kurier odprawiony " + _dCourier + " (jencow-dob; lochy z odmowa " + _noCourier + ")"
                             + " | lochy AI ponad prog: " + _dAiOver
                             + " | ksiega: chorych " + CountSick() + ", odpornych " + (_rows.Count - CountSick())
                             + " | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("DailyLine", e); }
            _frag.Length = 0;
            _dEsc = _dRansom = _dAiOver = _dStumbles = _dBattle = _dGone = _dWould = _dRegDead = _dNewSick = _dHealed = 0;
            _dHeroDead = _dGrave = _hStory = _hRuler = _hLast = _hCanDie = _hCeil = _hBattle = _hPlayer = _hOther = _dCourier = 0;
        }

        private static int CountSick() { int n = 0; foreach (var r in _rows.Values) if (r.Sick) n++; return n; }

        // ---------------------------------------------------------------- menu lochu (linia stanu, nieczynna)

        /// <summary>CampaignEvents.OnAfterSessionLaunchedEvent: linia stanu w menu lochu miasta i zamku (przed "Back") oraz linia startu. Nie rzuca.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            int added = 0;
            try
            {
                added += AddLine(starter, "town_keep_dungeon", OptTown, "{=!}{ARM_DF_STATE}", StateCondition, StateConsequence) ? 1 : 0;
                added += AddLine(starter, "castle_dungeon", OptCastle, "{=!}{ARM_DF_STATE}", StateCondition, StateConsequence) ? 1 : 0;
                added += AddLine(starter, "town_keep_dungeon", OptCourierTown, "{=!}{ARM_DF_COURIER}", CourierCondition, CourierConsequence) ? 1 : 0;   // 404: przelacznik kuriera (po linii stanu, przed "Back")
                added += AddLine(starter, "castle_dungeon", OptCourierCastle, "{=!}{ARM_DF_COURIER}", CourierCondition, CourierConsequence) ? 1 : 0;
                _hookMenu = added + "/4";
            }
            catch (Exception e) { _hookMenu = "BRAK (blad: " + e.Message + ")"; Stumble("AddMenus", e); }
            try
            {
                var s = Settings.Current;
                int mine = 0;
                try { foreach (var st in Settlement.All) if (Mine(st)) mine++; } catch { }
                bool ready = false;
                try { ready = StoryGuard.Ready; } catch { ready = false; }
                Log.Info(Tag + " (402): start sesji | klucze: choroba " + YN(s != null && s.DungeonSickness) + ", prog tloku " + (s != null ? From(s).ToString("0.00", Inv) : "-")
                         + ", szeregowi " + (s != null ? s.DungeonRegularSickPercent.ToString("0.0", Inv) : "-") + "%/d, bohater " + (s != null ? s.DungeonHeroSickChancePercent.ToString("0.0", Inv) : "-")
                         + "%/d, przebieg " + (s != null ? s.DungeonSickDays : 0) + " dob, ostrzezenia " + YN(s != null && s.DungeonWarnings)
                         + " | lochy rodu gracza " + mine + " | StoryGuard gotowy " + YN(ready)
                         + " | 404 zgony bohaterow " + YN(s != null && s.DungeonHeroDeaths) + (s != null && s.DungeonHeroDeaths && !ready ? " (WSTRZYMANE - straz fabuly niegotowa)" : "")
                         + ", udzial zgonow " + (s != null ? s.DungeonHeroDeathSharePercent.ToString("0.0", Inv) : "-") + "% (wiek 60+ x2), sufit " + (s != null ? s.DungeonHeroDeathsPerWeek : 0) + " na loch na " + DeathWindowDays + " dob"
                         + ", LordToll rok " + LordToll.Year + ": loch " + LordToll.Count(LordToll.Dungeon) + ", wspolny " + LordToll.Total + "/" + LordToll.MaxAll
                         + " | kurier: przelacznik " + YN(s != null && s.RansomCourierStandingRefusal) + ", bramka " + (Ransom178.CourierGateWired ? "TAK" : "NIE (latka ConsiderRansomPrisoner nie weszla)") + ", lochy z odmowa " + _noCourier
                         + " | menu lochu " + _hookMenu + " | latki 0 | z zapisu: chorych " + CountSick() + ", odpornych " + (_rows.Count - CountSick()) + ", lochy " + _recs.Count + ", grozby " + _threat.Count + ".");
            }
            catch { }
        }

        private static bool AddLine(CampaignGameStarter starter, string menuId, string optId, string text, GameMenuOption.OnConditionDelegate cond, GameMenuOption.OnConsequenceDelegate cons)
        {
            var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
            var menu = mgr != null ? mgr.GetGameMenu(menuId) : null;
            if (starter == null || menu == null) return false;
            int idx = -1, i = 0;
            foreach (var o in menu.MenuOptions)
            {
                if (o != null && o.IdString == optId) return true;   // juz jest
                if (o != null && o.IdString == "town_keep_dungeon_back" && idx < 0) idx = i;
                i++;
            }
            starter.AddGameMenuOption(menuId, optId, text, cond, cons, false, idx);
            return true;
        }

        /// <summary>Linia stanu: widoczna w lochu osady rodu gracza przy wlaczonym kluczu, zawsze nieczynna (sam odczyt).</summary>
        private static bool StateCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Default;
                var s = Settings.Current;
                var st = Settlement.CurrentSettlement;
                if (s == null || !s.DungeonSickness || !Mine(st) || st.Party == null || st.Party.PrisonRoster == null) return false;
                int total = st.Party.PrisonRoster.TotalManCount;
                int limit = Math.Max(1, st.Party.PrisonerSizeLimit);
                int sickN = SickIn(st.StringId);
                MBTextManager.SetTextVariable("ARM_DF_STATE", "Dungeon: " + total + " of " + limit + " held, " + sickN + " sick", false);
                args.IsEnabled = false;
                int pct = (int)Math.Round(From(s) * 100f);
                args.Tooltip = new TextObject("{=!}Above " + pct + "% of its room a dungeon breeds fever: common prisoners sicken and die, lords fall ill. Winter and a siege make it worse; a governor skilled in medicine helps.");
                return true;
            }
            catch (Exception e) { Stumble("StateCondition", e); return false; }
        }

        private static void StateConsequence(MenuCallbackArgs args) { }   // linia nieczynna - nic nie robi (i nigdy nie przelacza menu)

        /// <summary>404: przelacznik kuriera - widoczny w lochu osady rodu gracza przy kluczu Ransom Courier Standing Refusal.</summary>
        private static bool CourierCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Default;
                var s = Settings.Current;
                var st = Settlement.CurrentSettlement;
                if (s == null || !s.RansomCourierStandingRefusal || !Mine(st) || st.StringId == null) return false;
                SRec r;
                bool off = _recs.TryGetValue(st.StringId, out r) && r.NoCourier;
                MBTextManager.SetTextVariable("ARM_DF_COURIER", off ? "Ransom couriers: turned away" : "Ransom couriers: received", false);
                args.Tooltip = new TextObject(off
                    ? "{=!}Couriers with ransom offers for the lords held in this dungeon are turned away at the gate. Click to receive them again."
                    : "{=!}Couriers with ransom offers for the lords held in this dungeon are received. Click to turn them away: no ransom offers will come for prisoners held here until you change it back.");
                return true;
            }
            catch (Exception e) { Stumble("CourierCondition", e); return false; }
        }

        /// <summary>404: zmiana flagi lochu + odswiezenie menu lochu. Menu lochu NIE jest menu oczekiwania (AddGameMenu, cs_full.cs:205772 / :205840) -
        /// przelaczenie na to samo menu to droga gry (np. :206637); zakaz SwitchToMenu dotyczy menu oczekiwania.</summary>
        private static void CourierConsequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (!Mine(st) || st.StringId == null) return;
                var r = RecOf(st.StringId);
                r.NoCourier = !r.NoCourier;
                CountNoCourier();
                Log.Info(Tag + ": kurierzy z okupem w lochu " + st.StringId + " - " + (r.NoCourier ? "ODPRAWIANI" : "przyjmowani") + " (decyzja gracza; lochy z odmowa " + _noCourier + ").");
                string menu = args != null && args.MenuContext != null && args.MenuContext.GameMenu != null ? args.MenuContext.GameMenu.StringId : null;
                if (menu == "town_keep_dungeon" || menu == "castle_dungeon") GameMenu.SwitchToMenu(menu);
            }
            catch (Exception e) { Stumble("CourierConsequence", e); }
        }

        // ---------------------------------------------------------------- zapis

        private static bool Ok(string id) { return !string.IsNullOrEmpty(id) && id.IndexOf('|') < 0 && id.IndexOf('\n') < 0 && id.IndexOf('\r') < 0; }

        /// <summary>"v1" + wiersze "S|osada|dobaOstrzezenia|ponizej|goraczka|zmarliDoKomunikatu|dobaKomunikatu" i "H|bohater|osada|chory|doba|koniec|odpornosc".</summary>
        internal static string Export()
        {
            try
            {
                if (_rows.Count == 0 && _recs.Count == 0 && _threat.Count == 0 && TalkEmpty) return "";
                var sb = new StringBuilder(64 + 48 * (_rows.Count + _recs.Count));
                sb.Append("v1");
                foreach (var kv in _recs)
                {
                    if (!Ok(kv.Key)) continue;
                    var r = kv.Value;
                    sb.Append('\n').Append("S|").Append(kv.Key).Append('|').Append(r.WarnDay.ToString(Inv)).Append('|').Append(r.Below.ToString(Inv)).Append('|').Append(r.Fever ? '1' : '0')
                      .Append('|').Append(r.DeadAcc.ToString(Inv)).Append('|').Append(r.DeadMsgDay.ToString(Inv))
                      .Append('|').Append(r.NoCourier ? '1' : '0').Append('|');   // 404: pola 8-9
                    if (r.HeroDeaths != null)
                        for (int i = 0; i < r.HeroDeaths.Count; i++) { if (i > 0) sb.Append(','); sb.Append(r.HeroDeaths[i].ToString(Inv)); }
                }
                foreach (var r in _rows.Values)
                {
                    if (!Ok(r.Id) || (r.Sick && !Ok(r.St))) continue;
                    sb.Append('\n').Append("H|").Append(r.Id).Append('|').Append(r.Sick ? r.St : "").Append('|').Append(r.Sick ? '1' : '0').Append('|').Append(r.Day.ToString(Inv))
                      .Append('|').Append(r.End.ToString(Inv)).Append('|').Append(r.Immune.ToString(Inv)).Append('|').Append(r.Sick && r.Grave ? '1' : '0');   // 404: pole 8
                }
                foreach (var hid in _threat) if (Ok(hid)) sb.Append('\n').Append("T|").Append(hid);   // 404
                TalkExport(sb);   // 409: wiersze F / P / Q
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (brak klucza w starym zapisie = pusto). Zly wiersz pominiety; wpisy nieistniejacych bohaterow / osad sprzata pierwsza doba.</summary>
        internal static void Import(string data)
        {
            _rows.Clear(); _recs.Clear(); _threat.Clear(); _noCourier = 0;
            TalkReset();   // 409
            int bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var lines = data.Split('\n');
                if (lines.Length == 0 || lines[0].Trim() != "v1") { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                for (int i = 1; i < lines.Length; i++)
                {
                    var p = lines[i].Trim().Split('|');
                    if (p.Length == 0 || p[0].Length == 0) continue;
                    try
                    {
                        if (p[0] == "S" && p.Length >= 7 && p[1].Length > 0)
                        {
                            var rec = new SRec { WarnDay = int.Parse(p[2], Inv), Below = int.Parse(p[3], Inv), Fever = p[4] == "1", DeadAcc = Math.Max(0, int.Parse(p[5], Inv)), DeadMsgDay = int.Parse(p[6], Inv) };
                            if (p.Length >= 8) rec.NoCourier = p[7] == "1";   // 404 (zapis 402: brak pol = kurier przyjmowany, bez zgonow)
                            if (p.Length >= 9 && p[8].Length > 0)
                            {
                                rec.HeroDeaths = new List<int>(2);
                                foreach (var d in p[8].Split(',')) { int v; if (int.TryParse(d, NumberStyles.Integer, Inv, out v)) rec.HeroDeaths.Add(v); }
                            }
                            _recs[p[1]] = rec;
                        }
                        else if (p[0] == "H" && p.Length >= 7 && p[1].Length > 0)
                        {
                            var r = new HRow { Id = p[1], St = p[2], Sick = p[3] == "1", Day = int.Parse(p[4], Inv), End = int.Parse(p[5], Inv), Immune = int.Parse(p[6], Inv) };
                            if (r.Sick && r.St.Length == 0) { bad++; continue; }
                            if (p.Length >= 8) r.Grave = r.Sick && p[7] == "1";   // 404
                            _rows[r.Id] = r;
                        }
                        else if (p[0] == "T" && p.Length >= 2 && p[1].Length > 0) _threat.Add(p[1]);   // 404
                        else if (TalkImport(p)) { }   // 409: F / P / Q
                        else bad++;
                    }
                    catch { bad++; }
                }
                CountNoCourier();
                if (bad > 0) Log.Info(Tag + ": wczytano z zapisu - bledne wiersze pominiete: " + bad + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
