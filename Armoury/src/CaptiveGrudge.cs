using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 367d (Jeff 09.11: "Jopffrey nawet jak go wypuszcze ... bedzie uwazal ze go uporkzylem i bedzie chcial mnie zabic jak tylko nadarzy sie okazaj, czyli wypowie mi wojne,
    /// nie od razu bo walczy z polnoca ... no chyab ze krol umrze"). URAZA UWOLNIONEGO WLADCY. Klucz CaptiveGrudge.
    ///
    /// Powstanie: krol albo nastepca tronu (Ransom178.IsRulerOrHeir) uwolniony na warunkach gracza - T/V (podboje pod wlasna pieczecia) H = 50 + ziemie (do +30) + dni/2 (do +20),
    /// R (okup x2) H = 35 + dni/2 (do +20), zwykly okup jenca gracza (klucz CaptiveGrudgeRansom) H = 15 + dni/4 (do +10). Sila urazy G = H x M, M z cech (Mercy, Valor,
    /// Generosity, BK Humble; 0.4..2.0). Kolejna uraza dodaje sie do wiersza. Relacje raz, przy uwolnieniu: krol, dorosli jego rodu, glowy rodow krewnych (drzewo + KinClans),
    /// glowy rodow-wasali. Glowa rodu krewnych dostaje wlasny wiersz 40% G (dziala, gdy rzadzi krolestwem).
    /// Wygasanie: polowiczny okres z Mercy (-2 nigdy, -1 4 lata, 0 2 lata, +1 rok, +2 pol roku; Generosity >= 1 x0.75; rok Armoury 364 doby). Wiersz znika przy G &lt; 10.
    /// Wojna (doba, po CanonPeaceLock.Daily): wiersz bohatera zywego i rzadzacego krolestwem (Kingdom.Leader), G >= CaptiveGrudgeWarAt; bramki 0..10 (opis w Gates);
    /// wykonanie DeclareWarAction.ApplyByDefault (nie decyzja krolestwa - CanonThroneWars nie liczy jej jako wojny o tron). Cel: rod gracza (bez krolestwa), krolestwo gracza
    /// (wladca), krolestwo, ktoremu gracz sluzy (tylko G >= 2 x prog i po fabule). Pokoj w wojnie z urazy: G x 0.6 i przerwa 182 doby (nie przy pokoju z dolaczenia gracza do
    /// krolestwa). Smierc: wiersz znika; rzadzil krolestwem - nastepca z tego samego rodu albo rodu krewnych dziedziczy czesc (CaptiveGrudgeHeirShare x charakter).
    /// Zapis "arm_grudge367d"; Reset miedzy kampaniami; potkniecia liczone (bez globalnego wylacznika), 3 nieudane wypowiedzenia = wiersz czeka 30 dob.
    /// </summary>
    internal static class CaptiveGrudge
    {
        internal const string Tag = "URAZA (367d)";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double Year = 364.0;
        private const double KeepMin = 10.0;
        private const int PauseDays = 182;
        private const double KinPart = 0.4;
        private const double PeaceCut = 0.6;
        // ROT: Cersei (lord_4_2) bez ojca w danych - rod krola (Baratheonowie Krolewskiej Przystani) do Lannisterow recznie
        private static readonly Dictionary<string, string> KinClans = new Dictionary<string, string> { { "clan_nord_1", "clan_vlandia_1" } };

        private sealed class Row
        {
            public string Hero = "";
            public double G;
            public char Cond;          // T/V/R - rokowania, O - zwykly okup, K - rod krewnych, H - dziedzic
            public int Day;            // doba ostatniego upokorzenia (przysiega liczona od niej)
            public int PauseTo = -1;   // przerwa po pokoju w wojnie z urazy
            public string KinOf = "";  // wiersz krewnych / dziedzica: kogo upokorzono
            public string WarOn = "";  // cel naszej wojny (StringId frakcji), "" - brak
            public int Fails;
            public int WaitTo = -1;    // po 3 nieudanych wypowiedzeniach
            public string Ruled = "";  // krolestwo, ktorym rzadzil przy ostatniej dobie
            public int PeaceAt = -1;   // pokoj z rodem gracza - rozstrzygniecie w nastepnej dobie (dolaczenie do krolestwa?)
            public string LastWhy = "";
        }

        private sealed class Pending { public string Kingdom = ""; public double G; public string Dead = ""; public int Day; }

        private static readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>();
        private static readonly List<Pending> _pending = new List<Pending>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _stumbles;
        private static string _termsHero;   // znacznik TermsBegin: nasluch zwyklego okupu pomija to uwolnienie
        private static char _termsCond;
        private static double _termsH, _termsDays;
        private static string _termsParts = "-";
        private static bool _termsReleased;
        private static int _lastDay = -1;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CaptiveGrudge; } }
        private static float WarAt { get { var s = Settings.Current; float v = s != null ? s.CaptiveGrudgeWarAt : 50f; return v < 10f ? 10f : v > 200f ? 200f : v; } }
        private static float OathYears { get { var s = Settings.Current; float v = s != null ? s.CaptiveGrudgeOathYears : 2f; return v < 0f ? 0f : v > 10f ? 10f : v; } }
        private static float HeirShare { get { var s = Settings.Current; float v = s != null ? s.CaptiveGrudgeHeirShare : 0.5f; return v < 0f ? 0f : v > 1f ? 1f : v; } }
        private static bool RansomOn { get { var s = Settings.Current; return s != null && s.CaptiveGrudgeRansom; } }

        internal static void Reset()
        {
            _rows.Clear(); _pending.Clear(); _err.Clear(); _stumbles = 0; _termsHero = null; _termsReleased = false; _lastDay = -1;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CaptiveGrudge." + where, e); } catch { }
        }

        private static int Today { get { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } } }
        private static string F(double v) { return v.ToString("0.#", Inv); }
        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string NameF(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string He(Hero h) { return h != null && h.IsFemale ? "she" : "he"; }
        private static string His(Hero h) { return h != null && h.IsFemale ? "her" : "his"; }
        private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        private static int Trait(Hero h, TraitObject t) { try { return h != null && t != null ? h.GetTraitLevel(t) : 0; } catch { return 0; } }
        private static int BkTrait(Hero h, string id)
        {
            try
            {
                var t = Game.Current != null && Game.Current.ObjectManager != null ? Game.Current.ObjectManager.GetObject<TraitObject>(id) : null;   // BK BKTraits (Humble, Just)
                return t != null ? h.GetTraitLevel(t) : 0;
            }
            catch { return 0; }
        }

        private static Hero Find(string id) { try { return string.IsNullOrEmpty(id) ? null : Hero.Find(id); } catch { return null; } }

        private static Kingdom Ruled(Hero h)
        {
            var k = h != null ? h.MapFaction as Kingdom : null;
            return k != null && !k.IsEliminated && k.Leader == h ? k : null;
        }

        // ------------------------------------------------------------ sila urazy
        /// <summary>Temperament M (0.4..2.0) z cech; parts - skladniki do logu.</summary>
        private static double Temper(Hero h, out string parts)
        {
            int mercy = Trait(h, DefaultTraits.Mercy), valor = Trait(h, DefaultTraits.Valor), gen = Trait(h, DefaultTraits.Generosity), humble = BkTrait(h, "Humble");
            double m = 1 + 0.3 * Math.Max(0, -mercy) + 0.2 * Math.Max(0, valor) + 0.15 * Math.Max(0, -gen) + 0.2 * Math.Max(0, -humble)
                         - 0.2 * Math.Max(0, mercy) - 0.15 * Math.Max(0, gen);
            m = Math.Max(0.4, Math.Min(2.0, m));
            parts = "Mercy " + mercy + ", Valor " + valor + ", Generosity " + gen + ", Humble " + humble + ", Honor " + Trait(h, DefaultTraits.Honor);
            return m;
        }

        private static void CountTitles(List<object> titles, out int towns, out int castles, out int villages)
        {
            towns = castles = villages = 0;
            if (titles == null) return;
            foreach (var t in titles)
            {
                Settlement st = null;
                try { st = LandBk.Fief(t); } catch { st = null; }
                if (st == null) continue;
                if (st.IsTown) towns++; else if (st.IsCastle) castles++; else villages++;
            }
        }

        private static double DaysHeld(Hero h)
        {
            try { return h != null && h.IsPrisoner ? Math.Max(0, (CampaignTime.Now - h.CaptivityStartTime).ToDays) : 0; } catch { return 0; }
        }

        /// <summary>Upokorzenie H wedlug warunku (T/V/R/O).</summary>
        private static double Humiliation(char cond, int towns, int castles, int villages, double days, out string parts)
        {
            double land = Math.Min(30, 8 * towns + 5 * castles + 1 * villages);
            if (cond == 'T' || cond == 'V') { double d = Math.Min(20, 0.5 * days); parts = "pieczec 50 + ziemie " + F(land) + " (m " + towns + ", z " + castles + ", w " + villages + ") + niewola " + F(d); return 50 + land + d; }
            if (cond == 'R') { double d = Math.Min(20, 0.5 * days); parts = "okup x2 35 + niewola " + F(d); return 35 + d; }
            double o = Math.Min(10, 0.25 * days); parts = "okup 15 + niewola " + F(o); return 15 + o;
        }

        // ------------------------------------------------------------ podpowiedz w rozmowie (CaptiveTerms.HintCond)
        /// <summary>Dopisek do podpowiedzi rokowan (przed ')'); "" - bez dopisku. Nie rzuca.</summary>
        internal static string Preview(Hero h, char cond, List<object> titles)
        {
            try
            {
                if (!On || h == null || !Ransom178.IsRulerOrHeir(h)) return "";
                int t, c, v; CountTitles(cond == 'R' ? null : titles, out t, out c, out v);
                string p; double H = Humiliation(cond, t, c, v, DaysHeld(h), out p);
                double G = H * Temper(h, out p);
                Row r; if (_rows.TryGetValue(h.StringId, out r)) G += r.G;
                if (G < WarAt) return "";
                int honor = Trait(h, DefaultTraits.Honor);
                if (honor <= -1) return " " + Cap(He(h)) + " will remember this - and " + His(h) + " word is worth little.";
                if (honor == 0) return " " + Cap(He(h)) + " will remember this.";
                return " It will rankle, but " + He(h) + " is true to " + His(h) + " word.";
            }
            catch (Exception e) { Stumble("Preview", e); return ""; }
        }

        // ------------------------------------------------------------ powstanie
        /// <summary>Przed EndCaptivityAction w RunR/RunT/RunV (jeniec jeszcze w niewoli): liczy H, znacznik dla nasluchu okupu. titles - tytuly krola przeniesione (T/V).</summary>
        internal static void TermsBegin(Hero h, char cond, List<object> titles)
        {
            _termsHero = null; _termsReleased = false;
            try
            {
                if (h == null || h.StringId == null) return;
                _termsHero = h.StringId; _termsCond = cond;
                int t, c, v; CountTitles(cond == 'R' ? null : titles, out t, out c, out v);
                _termsDays = DaysHeld(h);
                _termsH = Humiliation(cond, t, c, v, _termsDays, out _termsParts);
            }
            catch (Exception e) { Stumble("TermsBegin", e); }
        }

        /// <summary>Wyjatek w EndCaptivityAction - znacznik zdjety (inaczej nastepny zwykly okup tego jenca bylby pominiety).</summary>
        internal static void TermsAbort() { _termsHero = null; _termsReleased = false; }

        /// <summary>Po uwolnieniu i komunikacie rokowan: wiersz urazy, relacje, komunikat. Tylko gdy nasluch potwierdzil uwolnienie.</summary>
        internal static void TermsEnd(Hero h)
        {
            try
            {
                if (h == null || _termsHero != h.StringId) return;
                bool rel = _termsReleased;
                _termsHero = null; _termsReleased = false;
                if (!rel || h.IsPrisoner) { Log.Info(Tag + ": " + Name(h) + " - rokowania bez uwolnienia, bez urazy."); return; }
                if (!On || !Ransom178.IsRulerOrHeir(h)) { Log.Info(Tag + ": " + Name(h) + " uwolniony (" + _termsCond + ") - klucz NIE albo nie krol/nastepca, bez urazy."); return; }
                Grow(h, _termsCond, _termsH, _termsParts, _termsDays, true);
            }
            catch (Exception e) { Stumble("TermsEnd", e); }
        }

        /// <summary>CampaignEvents.HeroPrisonerReleased: uwolnienie z rokowan (znacznik) albo zwykly okup jenca gracza. Nie rzuca.</summary>
        internal static void OnReleased(Hero h, PartyBase party, IFaction capturer, EndCaptivityDetail detail, bool show)
        {
            try
            {
                if (h == null || h.StringId == null) return;
                if (_termsHero == h.StringId) { _termsReleased = true; return; }
                if (detail != EndCaptivityDetail.Ransom || !On || !RansomOn || Clan.PlayerClan == null || h == Hero.MainHero) return;
                if (!h.IsLord || !Ransom178.IsRulerOrHeir(h)) return;
                Clan holder = Ransom178.CaptorClan(party);   // recenzja: ta sama logika trzymajacego co 178 (garnizon/milicja/patrol)
                if (holder != Clan.PlayerClan) return;
                double days = 0;
                try { days = Math.Max(0, (CampaignTime.Now - h.CaptivityStartTime).ToDays); } catch { }
                string p; double H = Humiliation('O', 0, 0, 0, days, out p);
                Grow(h, 'O', H, p, days, false);
            }
            catch (Exception e) { Stumble("OnReleased", e); }
        }

        private static readonly char[] Rank = { 'O', 'K', 'H', 'R', 'T', 'V' };
        private static int RankOf(char c) { int i = Array.IndexOf(Rank, c); return i < 0 ? 0 : i; }

        private static Row RowOf(Hero h)
        {
            Row r;
            if (!_rows.TryGetValue(h.StringId, out r)) { r = new Row { Hero = h.StringId }; _rows[h.StringId] = r; }
            return r;
        }

        /// <summary>Nowa uraza: G = H x M dodane do wiersza, relacje (raz), wiersze krewnych, komunikat.</summary>
        private static void Grow(Hero h, char cond, double H, string hParts, double days, bool terms)
        {
            string mParts;
            double M = Temper(h, out mParts);
            double G = H * M;
            var me = Hero.MainHero;
            var r = RowOf(h);
            double before = r.G;
            r.G += G;
            if (RankOf(cond) >= RankOf(r.Cond) || r.Cond == '\0') r.Cond = cond;
            if (cond != 'O' || r.Day <= 0) r.Day = Today;
            var rk = Ruled(h); r.Ruled = rk != null ? rk.StringId : r.Ruled;
            // relacje (raz, przy uwolnieniu)
            var sb = new StringBuilder(256);
            int dk = (int)Math.Min(30, Math.Round(G / 4));
            if (dk > 0) { ChangeRelationAction.ApplyPlayerRelation(h, -dk, false, true); sb.Append("krol -").Append(dk); }
            int dFam = (int)Math.Min(15, Math.Round(G / 8)), nFam = 0;
            var seen = new HashSet<Hero> { h };
            if (dFam > 0 && h.Clan != null)
                foreach (var m in h.Clan.Heroes)
                {
                    try
                    {
                        if (m == null || m == h || m == me || !m.IsAlive || m.IsChild || seen.Contains(m)) continue;
                        if (!Personal(m, me)) continue;   // gra bez BK: relacja glowy rodu - nie odejmujemy kilka razy
                        seen.Add(m);
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(m, me, -dFam, false);
                        nFam++;
                    }
                    catch (Exception e) { Stumble("Grow(rod)", e); }
                }
            if (nFam > 0) sb.Append(", rod ").Append(nFam).Append(" x -").Append(dFam);
            // rody krewnych
            int dKin = (int)Math.Min(12, Math.Round(G / 10));
            var kin = KinOf(h);
            var kinNames = new List<string>();
            foreach (var c in kin)
            {
                try
                {
                    var head = c.Leader;
                    if (head == null || !head.IsAlive || head == me || seen.Contains(head)) continue;
                    seen.Add(head);
                    if (dKin > 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(head, me, -dKin, false);
                    double kg = G * KinPart;
                    string note = "";
                    if (kg >= KeepMin)
                    {
                        var kr = RowOf(head);
                        kr.G += kg; if (kr.Cond == '\0' || RankOf(kr.Cond) < RankOf('K')) kr.Cond = 'K';
                        if (kr.Day <= 0) kr.Day = Today;
                        kr.KinOf = h.StringId;
                        var kk = Ruled(head); if (kk != null) kr.Ruled = kk.StringId;
                        note = " wiersz " + F(kr.G);
                    }
                    kinNames.Add(Name(head) + " (" + c.StringId + ") -" + dKin + note);
                }
                catch (Exception e) { Stumble("Grow(krewni)", e); }
            }
            if (kinNames.Count > 0) sb.Append(", krewni: ").Append(string.Join("; ", kinNames.ToArray()));
            // wasale jego krolestwa - hanba korony
            int dVas = (int)Math.Min(4, Math.Round(G / 25)), nVas = 0;
            var hk = h.MapFaction as Kingdom;
            if (dVas > 0 && hk != null)
                foreach (var c in hk.Clans)
                {
                    try
                    {
                        if (c == null || c == h.Clan || c == Clan.PlayerClan || c.IsEliminated || c.IsUnderMercenaryService || kin.Contains(c)) continue;
                        var head = c.Leader;
                        if (head == null || !head.IsAlive || head == me || seen.Contains(head)) continue;
                        seen.Add(head);
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(head, me, -dVas, false);
                        nVas++;
                    }
                    catch (Exception e) { Stumble("Grow(wasale)", e); }
                }
            if (nVas > 0) sb.Append(", wasale ").Append(nVas).Append(" x -").Append(dVas);
            int honor = Trait(h, DefaultTraits.Honor);
            Log.Info(Tag + ": POWSTANIE - " + Name(h) + " (" + (hk != null ? hk.StringId : "-") + (hk != null && hk.Leader == h ? ", krol" : ", nastepca") + ") | warunek " + cond
                     + " | H " + F(H) + " (" + hParts + ", dni " + F(days) + ") x M " + M.ToString("0.00", Inv) + " (" + mParts + ") = G " + F(G) + " | wiersz " + F(before) + " -> " + F(r.G)
                     + " (prog " + F(WarAt) + ") | relacje: " + (sb.Length > 0 ? sb.ToString() : "-") + ".");
            string who = Name(h);
            if (r.G >= WarAt)
            {
                if (honor <= -1) Log.Player(who + " will not forget this humiliation. " + (r.Cond == 'T' || r.Cond == 'V' ? "Whatever " + He(h) + " swore, " + He(h) : Cap(He(h))) + " will make war on you when " + He(h) + " can.", true);
                else if (honor == 0) Log.Player(who + " will not forget this humiliation.", true);
                else Log.Player(who + " keeps " + His(h) + " word, but " + He(h) + " will not forget this humiliation.", true);
            }
            else if (terms) Log.Player(who + " resents the terms, but bears them.");
        }

        /// <summary>Gra bez BK: relacja (m, gracz) liczona na glowe rodu - zmiana dla czlonka trafilaby w glowe (pomijamy). BK: osobista.</summary>
        private static bool Personal(Hero m, Hero me)
        {
            try
            {
                Hero e1, e2;
                Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(m, me, out e1, out e2);
                return e1 == m;
            }
            catch { return false; }
        }

        /// <summary>Rody krewnych: matki, ojca, malzonka, dziadka po matce i tabela KinClans (bez wlasnego rodu i rodu gracza).</summary>
        private static List<Clan> KinOf(Hero h)
        {
            var l = new List<Clan>();
            try
            {
                Action<Clan> add = c => { if (c != null && c != h.Clan && c != Clan.PlayerClan && !c.IsEliminated && !l.Contains(c)) l.Add(c); };
                if (h.Mother != null) { add(h.Mother.Clan); if (h.Mother.Father != null) add(h.Mother.Father.Clan); }
                if (h.Father != null) add(h.Father.Clan);
                if (h.Spouse != null) add(h.Spouse.Clan);
                string to;
                if (h.Clan != null && KinClans.TryGetValue(h.Clan.StringId, out to))
                    foreach (var c in Clan.All) if (c != null && c.StringId == to) { add(c); break; }
            }
            catch (Exception e) { Stumble("KinOf", e); }
            return l;
        }

        private static bool KinClan(Hero dead, Clan c)
        {
            if (dead == null || c == null) return false;
            if (c == dead.Clan) return true;
            return KinOf(dead).Contains(c);
        }

        // ------------------------------------------------------------ smierc
        /// <summary>CampaignEvents.HeroKilledEvent: wiersz zmarlego znika; rzadzil krolestwem - oczekujace dziedziczenie. Nie rzuca.</summary>
        internal static void OnKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool show)
        {
            try
            {
                if (victim == null || victim.StringId == null) return;
                Row r;
                if (!_rows.TryGetValue(victim.StringId, out r)) return;
                _rows.Remove(victim.StringId);
                var k = victim.MapFaction as Kingdom;
                string kid = k != null && k.Leader == victim ? k.StringId : r.Ruled;
                if (!string.IsNullOrEmpty(kid) && r.G >= KeepMin) _pending.Add(new Pending { Kingdom = kid, G = r.G, Dead = victim.StringId, Day = Today });
                Log.Info(Tag + ": SMIERC - " + Name(victim) + " | G " + F(r.G) + " | " + (!string.IsNullOrEmpty(kid) ? "rzadzil " + kid + " - nastepca odziedziczy czesc (nastepna doba)" : "nie rzadzil - uraza znika") + ".");
                if (r.G >= WarAt) Log.Player(Name(victim) + " is dead; " + His(victim) + " grudge against you dies with " + (victim.IsFemale ? "her" : "him") + ".");
            }
            catch (Exception e) { Stumble("OnKilled", e); }
        }

        private static void Inherit(int today)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                try
                {
                    if (today <= p.Day) continue;
                    Kingdom k = null;
                    foreach (var x in Kingdom.All) if (x != null && x.StringId == p.Kingdom) { k = x; break; }
                    var L = k != null && !k.IsEliminated ? k.Leader : null;
                    var dead = Find(p.Dead);
                    if (L == null || !L.IsAlive || L.StringId == p.Dead)
                    {
                        if (today - p.Day > 60 || k == null || k.IsEliminated) { _pending.RemoveAt(i); Log.Info(Tag + ": dziedziczenie po " + Name(dead) + " - brak nastepcy (" + p.Kingdom + "), uraza znika."); }
                        continue;
                    }
                    _pending.RemoveAt(i);
                    if (L == Hero.MainHero || L.Clan == Clan.PlayerClan) { Log.Info(Tag + ": dziedziczenie po " + Name(dead) + " - tron ma gracz, uraza znika."); continue; }
                    if (!KinClan(dead, L.Clan)) { Log.Info(Tag + ": dziedziczenie po " + Name(dead) + " - nowy wladca " + Name(L) + " z innej dynastii (" + (L.Clan != null ? L.Clan.StringId : "-") + "), uraza znika."); continue; }
                    int rel = 0; try { rel = dead != null ? L.GetRelation(dead) : 0; } catch { }
                    int mercy = Trait(L, DefaultTraits.Mercy), valor = Trait(L, DefaultTraits.Valor);
                    double f = Math.Max(0, Math.Min(1, 0.5 + rel / 100.0 + 0.3 * Math.Max(0, -mercy) - 0.3 * Math.Max(0, mercy) + 0.15 * Math.Max(0, valor)));
                    double share = HeirShare * f, g = p.G * share;
                    Row r;
                    bool had = _rows.TryGetValue(L.StringId, out r);
                    double was = had ? r.G : 0;
                    if (g >= KeepMin && g > was)
                    {
                        r = RowOf(L);
                        r.G = g; if (r.Cond == '\0' || r.Cond == 'O' || r.Cond == 'K') r.Cond = 'H';
                        r.KinOf = p.Dead; r.Ruled = k.StringId; if (r.Day <= 0) r.Day = today;
                    }
                    Log.Info(Tag + ": DZIEDZICZENIE - " + Name(L) + " po " + Name(dead) + " (" + k.StringId + ") | G zmarlego " + F(p.G) + " x udzial " + share.ToString("0.00", Inv)
                             + " (relacja " + rel + ", Mercy " + mercy + ", Valor " + valor + ") = " + F(g) + " | wiersz " + F(was) + " -> " + F(_rows.ContainsKey(L.StringId) ? _rows[L.StringId].G : 0) + ".");
                    if (g >= WarAt && g > was) Log.Player(Name(L) + " has not forgotten how you humbled " + Name(dead) + ".", true);
                }
                catch (Exception e) { Stumble("Inherit", e); _pending.RemoveAt(i); }
            }
        }

        // ------------------------------------------------------------ pokoj
        /// <summary>CampaignEvents.MakePeace: pokoj w wojnie z urazy - G x 0.6 i przerwa (pokoj z rodem gracza: rozstrzygniecie w dobie - dolaczenie do krolestwa bez ciecia). Nie rzuca.</summary>
        internal static void OnPeace(IFaction f1, IFaction f2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                if (f1 == null || f2 == null || _rows.Count == 0) return;
                foreach (var r in _rows.Values)
                {
                    if (string.IsNullOrEmpty(r.WarOn)) continue;
                    var h = Find(r.Hero);
                    var k = h != null ? h.MapFaction as Kingdom : null;
                    if (k == null) continue;
                    IFaction other = f1 == k ? f2 : f2 == k ? f1 : null;
                    if (other == null || other.StringId != r.WarOn) continue;
                    if (Clan.PlayerClan != null && other == Clan.PlayerClan) { r.PeaceAt = Today; Log.Info(Tag + ": pokoj " + Name(h) + " z rodem gracza (" + detail + ") - rozstrzygniecie w dobie (dolaczenie do krolestwa = bez ciecia)."); continue; }
                    Cut(r, h, "pokoj z " + NameF(other) + " (" + detail + ")");
                }
            }
            catch (Exception e) { Stumble("OnPeace", e); }
        }

        private static void Cut(Row r, Hero h, string why)
        {
            double was = r.G;
            r.G *= PeaceCut; r.PauseTo = Today + PauseDays; r.WarOn = ""; r.PeaceAt = -1;
            Log.Info(Tag + ": POKOJ - " + Name(h) + " | " + why + " | czesciowo pomszczony: G " + F(was) + " -> " + F(r.G) + ", przerwa do d." + r.PauseTo + ".");
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            int today = Today;
            if (today == _lastDay) return;
            _lastDay = today;
            try
            {
                if (_rows.Count == 0 && _pending.Count == 0) return;
                Inherit(today);
                var gone = new List<string>();
                int active = 0, wars = 0;
                var lines = new List<string>();
                foreach (var r in new List<Row>(_rows.Values))
                {
                    try
                    {
                        var h = Find(r.Hero);
                        if (h == null || !h.IsAlive) { gone.Add(r.Hero); continue; }
                        // wygasanie
                        int mercy = Trait(h, DefaultTraits.Mercy), gen = Trait(h, DefaultTraits.Generosity);
                        double half = mercy <= -2 ? 0 : mercy == -1 ? 4 * Year : mercy == 0 ? 2 * Year : mercy == 1 ? Year : 0.5 * Year;
                        if (half > 0 && gen >= 1) half *= 0.75;
                        if (half > 0) r.G *= Math.Pow(0.5, 1.0 / half);
                        if (r.G < KeepMin) { gone.Add(r.Hero); Log.Info(Tag + ": WYGASLA - " + Name(h) + " (G " + F(r.G) + ")."); continue; }
                        // pokoj z rodem gracza z wczoraj: dolaczenie do krolestwa - bez ciecia
                        if (r.PeaceAt >= 0)
                        {
                            if (Clan.PlayerClan != null && Clan.PlayerClan.Kingdom != null) { Log.Info(Tag + ": pokoj " + Name(h) + " z rodem gracza - gracz dolaczyl do " + NameF(Clan.PlayerClan.Kingdom) + ", bez ciecia G (" + F(r.G) + ")."); r.WarOn = ""; r.PeaceAt = -1; }
                            else Cut(r, h, "pokoj z rodem gracza");
                        }
                        var k = Ruled(h);
                        r.Ruled = k != null ? k.StringId : "";
                        if (k == null) { if (lines.Count < 10) lines.Add(Name(h) + " G " + F(r.G) + " | uspiony (nie rzadzi)"); continue; }
                        active++;
                        string why;
                        bool war = Try(r, h, k, today, out why);
                        if (war) wars++;
                        if (lines.Count < 10) lines.Add(Name(h) + " (" + k.StringId + ") G " + F(r.G) + " | " + why);
                    }
                    catch (Exception e) { Stumble("Daily(wiersz)", e); }
                }
                foreach (var id in gone) _rows.Remove(id);
                if (_rows.Count > 0 || gone.Count > 0 || _pending.Count > 0)
                    Log.Info(Tag + ": doba " + today + " | wiersze " + _rows.Count + " (rzadzi " + active + "), wojny dzis " + wars + ", usuniete " + gone.Count + ", dziedziczenia czekaja " + _pending.Count
                             + ", prog " + F(WarAt) + ", klucz " + (On ? "TAK" : "NIE") + (_stumbles > 0 ? ", potkniecia " + _stumbles : "") + (lines.Count > 0 ? " || " + string.Join(" || ", lines.ToArray()) : "") + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        /// <summary>Bramki 0..10 i wykonanie. why - opis do linii doby. Zwraca true, gdy wojna wypowiedziana.</summary>
        private static bool Try(Row r, Hero h, Kingdom k, int today, out string why)
        {
            why = "-";
            float at = WarAt;
            int honor = Trait(h, DefaultTraits.Honor), calc = Trait(h, DefaultTraits.Calculating), valor = Trait(h, DefaultTraits.Valor), mercy = Trait(h, DefaultTraits.Mercy);
            string ht = " (Honor " + honor + ")";
            if (!On) return Blocked(r, h, "0 klucz NIE", out why);
            if (r.G < at) { why = "ponizej progu"; return false; }
            if (Prologue.BeforeStory()) return Blocked(r, h, "1 przed fabula", out why);
            // cel
            var me = Hero.MainHero;
            var pc = Clan.PlayerClan;
            if (me == null || pc == null) { why = "brak gracza"; return false; }
            var pk = pc.Kingdom;
            IFaction target; bool shelter = false;
            if (pk == null) target = pc;
            else if (pk.RulingClan == pc) target = pk;
            else if (pk == k) return Blocked(r, h, "gracz w krolestwie urazonego - czeka", out why);
            else
            {
                if (r.G < 2 * at) return Blocked(r, h, "gracz pod oslona " + pk.StringId + " - G ponizej 2 x prog", out why);
                target = pk; shelter = true;
            }
            string tn = target == pc ? "rod gracza" : target.StringId;
            if (target.IsEliminated) return Blocked(r, h, "cel wyeliminowany", out why);
            // 2 wojna trwa
            if (FactionManager.IsAtWarAgainstFaction(k, target)) { why = "2 wojna trwa (" + tn + ")" + (r.WarOn == target.StringId ? " - z urazy" : ""); r.LastWhy = "2"; return false; }
            if (r.WaitTo > today) return Blocked(r, h, "po 3 nieudanych - czeka do d." + r.WaitTo, out why);
            // 3 przysiega
            double age = today - r.Day;
            bool oathT = r.Cond == 'T' || r.Cond == 'V';
            double breakF = 0;
            if (oathT)
            {
                if (honor >= 2) return Blocked(r, h, "3 przysiega - dotrzymuje do smierci" + ht, out why);
                if (honor == 1 && age < OathYears * Year) return Blocked(r, h, "3 przysiega - dotrzymuje do d." + (r.Day + (int)(OathYears * Year)) + ht, out why);
                if (honor == 0 && age < Year) return Blocked(r, h, "3 przysiega - dotrzymuje do d." + (r.Day + (int)Year) + ht, out why);
                breakF = honor <= -1 ? 1.0 : 0.5;
            }
            else if (r.Cond == 'R' && honor >= 1 && age < Year) return Blocked(r, h, "3 przyzwoitosc po okupie - czeka do d." + (r.Day + (int)Year) + ht, out why);
            // 4 przerwa po pokoju
            bool pause = r.PauseTo > today;
            double sincePeace = 9999;
            try { var link = k.GetStanceWith(target); if (link != null && link.PeaceDeclarationDate != CampaignTime.Zero) sincePeace = (CampaignTime.Now - link.PeaceDeclarationDate).ToDays; } catch { }
            bool truce = sincePeace < WarTruce.TruceDays;
            bool breakTruce = false;
            // recenzja: wlasna przerwa po pokoju w wojnie z urazy (Cut, 182 doby) jest twarda dla kazdego Honoru - krol sam zgodzil sie na ten pokoj; lamany moze byc tylko rozejm gry
            if (pause) return Blocked(r, h, "4 przerwa po wojnie z urazy do d." + r.PauseTo + ht, out why);
            if (truce)
            {
                if (honor >= 0) return Blocked(r, h, "4 przerwa po pokoju (pokoj " + F(sincePeace) + " dob temu < " + WarTruce.TruceDays + ")" + ht, out why);
                breakTruce = true; breakF = 1.0;
            }
            // 5 rozejm kanonu (WarTruce 330)
            if (WarTruce.BlocksWar(k, target) || (WarTruce.IsPair(k, target) && WarTruce.TruceHolds)) return Blocked(r, h, "5 rozejm kanonu (330)", out why);
            var tk = target as Kingdom;
            if (tk != null)
            {
                // 6 sojusz, pakt o nieagresji
                bool ally = false;
                try { ally = k.AlliedKingdoms != null && k.AlliedKingdoms.Contains(tk); } catch { }
                if (ally) return Blocked(r, h, "6 sojusz z " + tk.StringId, out why);
                if (HasNap(k, tk)) return Blocked(r, h, "6 pakt o nieagresji (Diplomacy)", out why);
                // 7 przerwa Diplomacy po pokoju
                if (DipCooldown(k, tk)) return Blocked(r, h, "7 rozejm Diplomacy (przerwa po pokoju)", out why);
                // 8 fabula - krolestwo AI (gracz pod oslona)
                // recenzja: jak 312c (CanonThroneWars.RefreshWorld) - bez warunku CanonHere; cisza tylko gdy zdarzenie ROT Krwawych Godow jest widoczne (bez niego OverNow = falsz na zawsze)
                if (shelter && CanonWarClock.RedWeddingState != -1)
                {
                    if (!CanonWarClock.OverNow()) return Blocked(r, h, "8 fabula - cisza do Krwawych Godow", out why);
                    if (tk.StringId == WarTruce.NorthId && !NorthOpen()) return Blocked(r, h, "8 fabula - Polnoc przed Bekartami", out why);
                }
            }
            // 9 wolne rece
            float own = k.CurrentTotalStrength;
            float enemies = 0f; string busy = null; float worstExh = -1f;
            try
            {
                foreach (var f in k.FactionsAtWarWith)
                {
                    var ek = f as Kingdom;
                    if (ek == null || ek.IsEliminated || ek == target || CrownGifts.IsWatch(ek) || CrownGifts.IsFreeFolk(ek)) continue;
                    float s = ek.CurrentTotalStrength;
                    enemies += s;
                    if (s >= 0.4f * own && busy == null) busy = ek.StringId + " sila " + (own > 1f ? (s / own).ToString("0.0", Inv) : "?");
                    float ex = WarTruce.Exh(k, ek);
                    if (ex > worstExh) worstExh = ex;
                }
            }
            catch (Exception e) { Stumble("Try(wolne rece)", e); }
            if (busy != null) return Blocked(r, h, "9 wolne rece NIE - wojna " + busy, out why);
            if (worstExh >= 50f) return Blocked(r, h, "9 wolne rece NIE - zmeczenie wojna " + F(worstExh), out why);
            // 10 sila
            float ts = 0f, allies = 0f;
            try
            {
                ts = target.CurrentTotalStrength;
                if (tk != null && tk.AlliedKingdoms != null) foreach (var a in tk.AlliedKingdoms) if (a != null && !a.IsEliminated && a != k) allies += a.CurrentTotalStrength;
            }
            catch (Exception e) { Stumble("Try(sila)", e); }
            double den = ts + 0.5 * allies + 0.5 * enemies;
            double ratio = den > 1 ? own / den : 9.9;
            double need = Math.Max(0.8, Math.Min(2.0, 1.2 + 0.25 * calc - 0.15 * Math.Max(0, valor) - 0.1 * Math.Max(0, -mercy)));
            if (ratio < need) return Blocked(r, h, "10 sila " + ratio.ToString("0.00", Inv) + " < " + need.ToString("0.00", Inv) + " (wlasna " + F(own) + ", cel " + F(ts) + ", sojusznicy celu " + F(allies) + ", inni wrogowie " + F(enemies) + ")", out why);
            // wykonanie
            bool ok = false;
            try { DeclareWarAction.ApplyByDefault(k, target); ok = FactionManager.IsAtWarAgainstFaction(k, target); }
            catch (Exception e) { Stumble("Try(DeclareWar)", e); ok = false; }
            if (!ok)
            {
                r.Fails++;
                string f3 = "";
                if (r.Fails >= 3) { r.WaitTo = today + 30; r.Fails = 0; f3 = " - 3 nieudane, wiersz czeka do d." + r.WaitTo; }
                Log.Info(Tag + ": wypowiedzenie NIEUDANE - " + Name(h) + " (" + k.StringId + ") na " + tn + f3 + " (ponowienie jutro).");
                why = "wypowiedzenie nieudane" + f3; r.LastWhy = "x";
                return false;
            }
            r.Fails = 0; r.WarOn = target.StringId; r.LastWhy = "w";
            string pen = breakF > 0 ? Penalty(h, k, breakF) : "-";
            Log.Info(Tag + ": WOJNA - " + Name(h) + " (" + k.StringId + ") wypowiada wojne: " + tn + (shelter ? " (oslona gracza)" : "") + " | G " + F(r.G) + ", warunek " + r.Cond
                     + " | sila " + ratio.ToString("0.00", Inv) + " >= " + need.ToString("0.00", Inv) + " | " + (breakF > 0 ? "ZLAMANIE " + (breakTruce ? "rozejmu" : "slowa") + " (x" + F(breakF) + "): " + pen : "bez zlamania slowa") + ".");
            string msg = "Remembering the humiliation of " + His(h) + " captivity, " + Name(h) + " declares war on " + (shelter ? NameF(target) + ", which shelters you." : "you.");
            if (breakF > 0 && oathT) msg += " " + Cap(He(h)) + " breaks the oath " + He(h) + " swore for " + His(h) + " freedom.";
            Log.Player(msg, true);
            if (pen != "-" && pen.Length > 0 && !pen.StartsWith("0", StringComparison.Ordinal)) Log.Player("The honourable lords of " + NameF(k) + " frown on their king's broken " + (oathT ? "oath." : "truce."));
            why = "WOJNA na " + tn;
            return true;
        }

        /// <summary>Kara za zlamane slowo: glowy rodow krolestwa z Honor >= 1 albo BK Just >= 1 traca do krola 4 x poziom x f. Zwraca "N lordow (-x..)".</summary>
        private static string Penalty(Hero king, Kingdom k, double f)
        {
            int n = 0, sum = 0;
            try
            {
                foreach (var c in k.Clans)
                {
                    if (c == null || c == king.Clan || c == Clan.PlayerClan || c.IsEliminated) continue;
                    var head = c.Leader;
                    if (head == null || !head.IsAlive || head == king || head == Hero.MainHero) continue;
                    int lvl = Math.Max(Trait(head, DefaultTraits.Honor), BkTrait(head, "Just"));
                    if (lvl < 1) continue;
                    int d = Math.Max(1, (int)Math.Round(4 * lvl * f));
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(head, king, -d, false);
                    n++; sum += d;
                }
            }
            catch (Exception e) { Stumble("Penalty", e); }
            return n + " lordow, razem -" + sum;
        }

        private static bool Blocked(Row r, Hero h, string reason, out string why)
        {
            why = reason;
            string key = reason.Length > 8 ? reason.Substring(0, 8) : reason;   // powod bez liczb (sila, dni) - linia przy zmianie powodu, nie co dobe
            if (r.LastWhy != key) { r.LastWhy = key; Log.Info(Tag + ": " + Name(h) + " G " + F(r.G) + " - wstrzymane: " + reason + "."); }
            return false;
        }

        private static bool NorthOpen()
        {
            try { return CanonPeaceLock.BastardsState330 >= 2 || KingInTheNorth.FightsOnEnd != null; } catch { return false; }
        }

        // ------------------------------------------------------------ Diplomacy (odbicie; bez moda - brak paktu i przerwy)
        private static bool _dipTried;
        private static MethodInfo _mNap, _mCool;

        private static void DipInit()
        {
            if (_dipTried) return;
            _dipTried = true;
            try
            {
                var tMgr = AccessTools.TypeByName("Diplomacy.DiplomaticAction.DiplomaticAgreementManager");
                if (tMgr != null)
                    foreach (var m in tMgr.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        if (m.Name == "HasNonAggressionPact" && m.GetParameters().Length == 3) { _mNap = m; break; }
                var tCool = AccessTools.TypeByName("Diplomacy.CooldownManager");
                if (tCool != null)
                    foreach (var m in tCool.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        if (m.Name == "HasDeclareWarCooldown" && m.GetParameters().Length == 3) { _mCool = m; break; }
                Log.Info(Tag + ": Diplomacy - pakty " + (_mNap != null ? "TAK" : "brak") + ", przerwa po pokoju " + (_mCool != null ? "TAK" : "brak") + ".");
            }
            catch (Exception e) { Stumble("DipInit", e); }
        }

        private static bool HasNap(Kingdom a, Kingdom b)
        {
            DipInit();
            if (_mNap == null) return false;
            try { var args = new object[] { a, b, null }; object r = _mNap.Invoke(null, args); return r is bool && (bool)r; }
            catch (Exception e) { Stumble("HasNap", e); return false; }
        }

        private static bool DipCooldown(Kingdom a, Kingdom b)
        {
            DipInit();
            if (_mCool == null) return false;
            try { var args = new object[] { a, b, 0f }; object r = _mCool.Invoke(null, args); return r is bool && (bool)r; }
            catch (Exception e) { Stumble("DipCooldown", e); return false; }
        }

        // ------------------------------------------------------------ zapis ("arm_grudge367d")
        private static readonly char[] Bad = { '|', ';', ':' };

        /// <summary>"v1|hero:G:cond:dzien:przerwaDo:kinOf:wojnaNa:nieudane:czekaDo:rzadzil:pokojD;...|krolestwo:G:zmarly:dzien;..."</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(128);
                sb.Append("v1|");
                bool f = true;
                foreach (var r in _rows.Values)
                {
                    if (r.Hero.IndexOfAny(Bad) >= 0 || r.KinOf.IndexOfAny(Bad) >= 0 || r.WarOn.IndexOfAny(Bad) >= 0 || r.Ruled.IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(r.Hero).Append(':').Append(r.G.ToString("0.###", Inv)).Append(':').Append(r.Cond == '\0' ? 'O' : r.Cond).Append(':').Append(r.Day.ToString(Inv))
                      .Append(':').Append(r.PauseTo.ToString(Inv)).Append(':').Append(r.KinOf).Append(':').Append(r.WarOn).Append(':').Append(r.Fails.ToString(Inv))
                      .Append(':').Append(r.WaitTo.ToString(Inv)).Append(':').Append(r.Ruled).Append(':').Append(r.PeaceAt.ToString(Inv));
                }
                sb.Append('|'); f = true;
                foreach (var p in _pending)
                {
                    if (p.Kingdom.IndexOfAny(Bad) >= 0 || p.Dead.IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(p.Kingdom).Append(':').Append(p.G.ToString("0.###", Inv)).Append(':').Append(p.Dead).Append(':').Append(p.Day.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _rows.Clear(); _pending.Clear(); _termsHero = null; _termsReleased = false; _lastDay = -1;
            int bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 3 || f[0] != "v1") { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                foreach (var p in f[1].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double g; int d, pt, fl, wt, pa;
                    if (x.Length == 11 && x[0].Length > 0 && x[2].Length == 1 && double.TryParse(x[1], NumberStyles.Float, Inv, out g) && int.TryParse(x[3], NumberStyles.Integer, Inv, out d)
                        && int.TryParse(x[4], NumberStyles.Integer, Inv, out pt) && int.TryParse(x[7], NumberStyles.Integer, Inv, out fl) && int.TryParse(x[8], NumberStyles.Integer, Inv, out wt)
                        && int.TryParse(x[10], NumberStyles.Integer, Inv, out pa))
                        _rows[x[0]] = new Row { Hero = x[0], G = g, Cond = x[2][0], Day = d, PauseTo = pt, KinOf = x[5], WarOn = x[6], Fails = fl, WaitTo = wt, Ruled = x[9], PeaceAt = pa };
                    else bad++;
                }
                foreach (var p in f[2].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double g; int d;
                    if (x.Length == 4 && double.TryParse(x[1], NumberStyles.Float, Inv, out g) && int.TryParse(x[3], NumberStyles.Integer, Inv, out d))
                        _pending.Add(new Pending { Kingdom = x[0], G = g, Dead = x[2], Day = d });
                    else bad++;
                }
                if (_rows.Count > 0 || _pending.Count > 0 || bad > 0) Log.Info(Tag + ": wczytano z zapisu - wiersze " + _rows.Count + ", dziedziczenia " + _pending.Count + (bad > 0 ? " (bledne " + bad + ")" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
