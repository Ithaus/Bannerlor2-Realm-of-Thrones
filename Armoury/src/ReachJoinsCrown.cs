using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA 332 - REACH DO KORONY PO CZARNYM NURCIE (decyzja Jeffa 05.11: "Reach do korony po Czarnym Nurcie"; klucz ReachJoinsCrownAfterBlackwater, domyslnie TAK).
    /// KANON: po smierci Renly'ego Tyrellowie sa sojusznikami Joffreya (m104 (2) - sojusz korona-Reach i pakt Reach-Dorne, bez zmian do Czarnego Nurtu); pod Czarnym Nurtem
    /// rozbijaja Stannisa, potem sa wasalami Zelaznego Tronu (Mace w malej radzie, Margaery krolowa). W modzie: po bitwie wszystkie rody krolestwa Reach (empire_w) z
    /// zamkami, miastami, wsiami, partiami i armiami wstepuja do krolestwa korony (vlandia - House Baratheon of King's Landing); Tyrell zostaje glowa swojego rodu i ziem
    /// (tytuly BannerKings bez zmian, jak w 272); krolestwo Reach znika (DestroyKingdomAction bez rodow). Raz na kampanie (stan w zapisie arm_r332).
    /// ZNACZNIK: ROT BlackwaterEvent zakonczone (stan 2) i StannisWon == false (ROT HandleDoneButton przenosi wtedy rody Stannisa do Reach - wejda do korony razem z
    /// Reach, bo ConditionsHold=false konczy wydarzenie w nastepnej dobie, a my sprawdzamy stan 2); zapas: wydarzenie niewidoczne / nieodpalone (stan -1/0) i dzien
    /// opowiesci >= oblezenie Czarnego Nurtu na osi Fabuli (177 x skala) + 150 (jak straz Jaimego, Prologue.World); flaga autotestu. StannisWon == true - nigdy.
    /// WARUNKI (co dobe, doba m104 przed HoldReach): klucz, kampania kanonu (Prologue.CanonHere - decyzja Jeffa nr 5), Reach i korona istnieja, King's Landing (town_EW4)
    /// w koronie, Reach nie w wojnie z korona i bez sojuszu ze Smocza Skala, gracz nie rzadzi ani Reach, ani korona (linia "poza regula"; gracz-wasal albo najemnik
    /// Reach przechodzi razem z rodami, jak w 272), bez wojny domowej Diplomacy w Reach. Bezpieczna chwila (partia / twierdza Reach w bitwie) - czekanie najwyzej
    /// 7 dob, potem i tak (armie zachowane latka 272).
    /// WOJNY (decyzja kanoniczna): wojny korony rody Reach przejmuja przy wstapieniu (gra: AdjustFactionStancesForClanJoiningKingdom) - w ksiazce Tyrellowie walcza
    /// potem za korone. Wojny TYLKO Reach (korona ich nie prowadzi, np. z Dorne albo Zelaznymi Wyspami) - pokoj przed wstapieniem: Zelazny Tron nie bierze na siebie
    /// cudzych sporow, a w ksiazce Reach po Czarnym Nurcie nie prowadzi wlasnej wojny (najazd Eurona na Reach to juz wojna korony, w "Uczcie dla wron"). Pakty
    /// Diplomacy Reach (nasz Reach-Dorne i inne) wygaszone przed zniszczeniem; pakt korona-Dorne m104 chroni teraz rody Reach. Sojusz korona-Reach (m104) zakonczony
    /// w transakcji - po wstapieniu m104 (2) Reach nie pilnuje i nie odnawia sojuszu: zadnego "konca sojuszu" w dniu opowiesci ~900.
    /// KRONIKA: po wstapieniu "empire_w" w warunkach politycznych (war, peace, kexists, leads, prisoner, p:vassal - KingInTheNorth.AliasKingdom) = korona; ziemia
    /// (region, p:region, own w CanonCompanions) = osada korony z listy twierdz z chwili wstapienia (wies - jej twierdza); relk - glowy rodow z listy dalej w koronie;
    /// plotka - siedziba rodu panujacego Reach z chwili wstapienia (Highgarden). Termin cw:reach - prawda (Reach przy koronie).
    /// Linie "Reach do korony (332): " (po polsku), komunikat gracza po angielsku. Bledy: licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static class ReachJoinsCrown
    {
        internal const string P = "Reach do korony (332): ";
        internal const string ReachId = "empire_w", CrownId = "vlandia";
        private const string TyrellClanId = "clan_empire_west_1", DragonstoneId = "dragonstone", KingsLandingId = "town_EW4";
        private const string BwEvent = "BlackwaterEvent", SiegeEvent = "BlackwaterSiegeEvent";
        private const float BlackwaterSiegeRotDay = 177f;
        private const int SpareDays = 150, UnsafeMaxDays = 7;
        internal const string AutoFlagName = "armoury-reach332.flag";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- stan (zapis arm_r332)
        private static int _st;                     // 0 czeka, 1 zrobione, 2 nigdy
        private static int _day = int.MinValue, _dayC = -1;
        private static string _wy = "", _player = "-", _seat = "", _never = "", _ruler = "";
        private static double _waitSince = -1.0;     // dzien kampanii pierwszego czekania na bezpieczna chwile
        private static readonly List<string> _clans = new List<string>(), _forts = new List<string>();
        private static readonly HashSet<string> _clanSet = new HashSet<string>(StringComparer.Ordinal), _fortSet = new HashSet<string>(StringComparer.Ordinal);

        // ---------------------------------------------------------------- sesja
        private static int _sessionDays, _stumbles, _importBad, _foldCount;
        private static string _lastWait = "";
        private static int _lastWaitDay = int.MinValue;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.ReachJoinsCrownAfterBlackwater; } }

        /// <summary>Wstapienie zrobione (aliasy Kroniki, m104 (2) bez pilnowania sojuszu).</summary>
        internal static bool Done { get { return _st == 1; } }

        internal static int DoneDay { get { return _day; } }

        internal static void Reset()
        {
            _st = 0; _day = int.MinValue; _dayC = -1; _wy = ""; _player = "-"; _seat = ""; _never = ""; _ruler = ""; _waitSince = -1.0;
            _clans.Clear(); _forts.Clear(); _clanSet.Clear(); _fortSet.Clear();
            _sessionDays = 0; _stumbles = 0; _importBad = 0; _foldCount = 0; _lastWait = ""; _lastWaitDay = int.MinValue; _err.Clear();
            TestReset();
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("ReachJoinsCrown." + where, e); } catch { }
        }

        private static Kingdom K(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static bool Alive(Kingdom k) { return k != null && !k.IsEliminated; }

        private static bool PlayerRules(Kingdom k)
        {
            try { return k != null && k.RulingClan != null && k.RulingClan == Clan.PlayerClan; } catch { return false; }
        }

        private static double StoryDay() { try { return Prologue.StoryDays(); } catch { return 0.0; } }
        private static string F1(double d) { return d.ToString("0.0", Inv); }
        private static string Clean(string s) { return (s ?? "").Replace('|', '/').Replace(',', ';'); }

        // ---------------------------------------------------------------- Kronika (aliasy po wstapieniu)

        /// <summary>Warunek polityczny Kroniki: po wstapieniu "empire_w" = korona (wolane z KingInTheNorth.AliasKingdom). Inaczej id bez zmian.</summary>
        internal static string AliasKingdom(string id) { return _st == 1 && id == ReachId ? CrownId : id; }

        /// <summary>Po wstapieniu id "empire_w" w warunku ziemi / grupie / miejscu plotki.</summary>
        internal static bool IsAlias(string id) { return _st == 1 && id == ReachId; }

        /// <summary>Osada korony na ziemi Reach (twierdza z listy z chwili wstapienia; wies - jej twierdza).</summary>
        internal static bool HeldAsReach(Settlement s)
        {
            try
            {
                if (s == null) return false;
                var b = s.IsVillage && s.Village != null && s.Village.Bound != null ? s.Village.Bound : s;
                return b.StringId != null && _fortSet.Contains(b.StringId) && s.MapFaction != null && s.MapFaction.StringId == CrownId;
            }
            catch { return false; }
        }

        /// <summary>Rody z wstapienia dalej w koronie (bez najemnikow i wyeliminowanych) - grupa "empire_w" w relk po wstapieniu.</summary>
        internal static List<Clan> ReachLords()
        {
            var l = new List<Clan>();
            try
            {
                foreach (var c in Clan.All)
                    if (c != null && c.StringId != null && _clanSet.Contains(c.StringId) && !c.IsEliminated && !c.IsUnderMercenaryService && c.Kingdom != null && c.Kingdom.StringId == CrownId) l.Add(c);
            }
            catch (Exception e) { Stumble("ReachLords", e); }
            return l;
        }

        /// <summary>Rod z listy wstapienia (relk - pary Reach-Reach pomijane po aliasie).</summary>
        internal static bool IsReachClan(Clan c) { return _st == 1 && c != null && c.StringId != null && _clanSet.Contains(c.StringId); }

        /// <summary>Siedziba rodu panujacego Reach z chwili wstapienia (miejsce plotki "empire_w"); pusty - brak.</summary>
        internal static string SeatId { get { return _seat; } }

        // ---------------------------------------------------------------- zapis
        internal static string Export()
        {
            var sb = new StringBuilder(160 + _clans.Count * 24 + _forts.Count * 12);
            sb.Append("v1|").Append(_st.ToString(Inv)).Append('|').Append(_day.ToString(Inv)).Append('|').Append(_dayC.ToString(Inv)).Append('|').Append(_wy).Append('|')
              .Append(_player).Append('|').Append(_waitSince.ToString("0.###", Inv)).Append('|').Append(_seat).Append('|').Append(_ruler).Append('|')
              .Append(string.Join(",", _clans.ToArray())).Append('|').Append(string.Join(",", _forts.ToArray())).Append('|').Append(_never);
            return sb.ToString();
        }

        internal static void Import(string v)
        {
            _st = 0; _day = int.MinValue; _dayC = -1; _wy = ""; _player = "-"; _seat = ""; _never = ""; _ruler = ""; _waitSince = -1.0;
            _clans.Clear(); _forts.Clear(); _clanSet.Clear(); _fortSet.Clear(); _importBad = 0;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var f = v.Split('|');
                if (f.Length < 12 || f[0] != "v1") { _importBad++; return; }
                int.TryParse(f[1], NumberStyles.Integer, Inv, out _st);
                if (_st < 0 || _st > 2) { _st = 0; _importBad++; }
                if (!int.TryParse(f[2], NumberStyles.Integer, Inv, out _day)) _day = int.MinValue;
                if (!int.TryParse(f[3], NumberStyles.Integer, Inv, out _dayC)) _dayC = -1;
                _wy = f[4]; _player = f[5].Length > 0 ? f[5] : "-";
                if (!double.TryParse(f[6], NumberStyles.Float, Inv, out _waitSince)) _waitSince = -1.0;
                _seat = f[7]; _ruler = f[8];
                foreach (var x in f[9].Split(',')) if (x.Length > 0) { _clans.Add(x); _clanSet.Add(x); }
                foreach (var x in f[10].Split(',')) if (x.Length > 0) { _forts.Add(x); _fortSet.Add(x); }
                _never = f[11];
            }
            catch (Exception e) { _importBad++; Stumble("Import", e); }
        }

        /// <summary>CanonWarsBehavior.SyncData: klucz arm_r332 (zapis bez klucza - stan pusty; starsza DLL go pomija).</summary>
        internal static void Sync(IDataStore ds)
        {
            string v = "";
            try { if (ds.IsSaving) v = Export(); } catch (Exception e) { Stumble("Export", e); v = ""; }
            SaveText.Sync(ds, "arm_r332", ref v);
            if (ds.IsLoading) { try { Import(v); } catch (Exception e) { Stumble("Import", e); } }
        }

        // ---------------------------------------------------------------- ROT Czarny Nurt
        private static bool _bwTried;
        private static FieldInfo _fBwInst, _fBwWon;

        /// <summary>ROT BlackwaterEvent.StannisWon: 1 = tak, 0 = nie, -1 = brak odczytu.</summary>
        private static int StannisWon()
        {
            try
            {
                if (!_bwTried)
                {
                    _bwTried = true;
                    var t = AccessTools.TypeByName("ROT.Events.BlackwaterEvent");
                    if (t != null) { _fBwInst = AccessTools.Field(t, "Instance"); _fBwWon = AccessTools.Field(t, "StannisWon"); }
                }
                if (_fBwInst == null || _fBwWon == null) return -1;
                var inst = _fBwInst.GetValue(null);
                if (inst == null) return -1;
                return (bool)_fBwWon.GetValue(inst) ? 1 : 0;
            }
            catch (Exception e) { Stumble("StannisWon", e); return -1; }
        }

        private static int BwState() { try { return Prologue.RotEventState(BwEvent); } catch { return -9; } }
        private static int SiegeState() { try { return Prologue.RotEventState(SiegeEvent); } catch { return -9; } }

        /// <summary>Osada krolestwa k, w ktorej stoi gracz (CurrentSettlement albo osada spotkania); null - na mapie / gdzie indziej.</summary>
        private static string PlayerInSettlementOf(Kingdom k)
        {
            try
            {
                var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                Settlement s = mp != null ? mp.CurrentSettlement : null;
                if (s == null && TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null) s = TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.EncounterSettlement;
                if (s == null || s.MapFaction != k) return null;
                return s.StringId;
            }
            catch { return null; }
        }

        /// <summary>Zapas: dzien opowiesci oblezenia Czarnego Nurtu na osi Fabuli (177 x skala) + 150.</summary>
        private static double SpareDay() { return BlackwaterSiegeRotDay * Prologue.StoryScale() + SpareDays; }

        // ---------------------------------------------------------------- start sesji
        internal static void StartLine()
        {
            _sessionDays = 0; _lastWait = ""; _lastWaitDay = int.MinValue;
            string st = _st == 1 ? "zrobione (dzien opowiesci " + (_day != int.MinValue ? _day.ToString(Inv) : "?") + ", dzien kampanii " + _dayC + ", rodow " + _clans.Count + ", twierdz " + _forts.Count + ", wyzwalacz " + _wy + ")"
                       : _st == 2 ? "nigdy (" + _never + ")" : "czeka";
            string reach = "-", role = "-", kl = "-";
            int bw = -9, won = -9;
            try
            {
                bw = BwState(); won = StannisWon();
                var rk = K(ReachId);
                if (Alive(rk))
                {
                    int n = 0; foreach (var c in rk.Clans) if (c != null && !c.IsEliminated) n++;
                    reach = "wladca " + (rk.RulingClan != null ? KingInTheNorth.Name(rk.RulingClan) + " (" + rk.RulingClan.StringId + ")" : "-") + ", rodow " + n + ", twierdz " + rk.Fiefs.Count
                            + ", wojna z korona " + (Alive(K(CrownId)) && rk.IsAtWarWith(K(CrownId)) ? "TAK" : "NIE");
                }
                else reach = rk == null ? "brak krolestwa" : "zniszczone";
                var s = Settlement.Find(KingsLandingId);
                kl = s != null && s.MapFaction != null ? s.MapFaction.StringId : "-";
                var pc = Clan.PlayerClan;
                role = pc == null || pc.Kingdom == null ? "bez krolestwa" : (pc.IsUnderMercenaryService ? "najemnik " : PlayerRules(pc.Kingdom) ? "wladca " : "wasal ") + pc.Kingdom.StringId;
            }
            catch (Exception e) { Stumble("StartLine", e); }
            Log.Info(P + "start sesji | klucz " + (On ? "TAK" : "NIE") + " | zakres " + (Prologue.CanonHere ? "kampania kanonu" : "NIE (zapis sprzed m104 bez prologu)") + " | stan " + st
                     + " | ROT " + BwEvent + " stan " + bw + ", StannisWon " + (won == 1 ? "TAK" : won == 0 ? "NIE" : "brak odczytu") + ", zapas od dnia opowiesci " + ((int)SpareDay()).ToString(Inv)
                     + " | King's Landing " + kl + " | Reach: " + reach + " | gracz: " + role + (_st == 1 ? " (w chwili wstapienia: " + _player + ")" : "")
                     + " | dzien opowiesci " + F1(StoryDay()) + (_importBad > 0 ? " | bledy zapisu " + _importBad : "") + ".");
        }

        // ---------------------------------------------------------------- doba (Prologue.CrownDaily, przed m104 (2) Reach)
        internal static void Daily()
        {
            _sessionDays++;
            if (Campaign.Current == null) return;
            if (_st == 1) { TestAfterFold(); return; }
            if (_st == 0) { try { Tick(); } catch (Exception e) { Stumble("Tick", e); } }
            TestAfter();
        }

        /// <summary>Linia "czeka": najwyzej raz na dobe opowiesci; ten sam rodzaj powodu - najwyzej raz na 7 dob.</summary>
        private static void Wait(string why, double sd)
        {
            int day = (int)Math.Floor(sd);
            int br = why.IndexOf(" (", StringComparison.Ordinal);
            string key = br > 0 ? why.Substring(0, br) : why;
            if (_lastWaitDay != int.MinValue && day == _lastWaitDay) return;
            if (key == _lastWait && _lastWaitDay != int.MinValue && day - _lastWaitDay < 7) return;
            _lastWait = key; _lastWaitDay = day;
            Log.Info(P + "czeka - " + why + " | dzien opowiesci " + F1(sd) + ".");
        }

        private static void Never(string why, double sd)
        {
            _st = 2; _never = Clean(why);
            Log.Info(P + "nigdy - " + why + " (dzien opowiesci " + F1(sd) + ") - Reach nie wstapi do korony w tej kampanii.");
        }

        private static void Tick()
        {
            if (!On || _st != 0) return;
            double sd = StoryDay();
            if (!Prologue.CanonHere) { Wait("zakres - kampania klasyczna bez prologu i bez znacznika m104 (decyzja Jeffa nr 5)", sd); return; }
            var reach = K(ReachId); var crown = K(CrownId);
            if (reach == null) { Never("brak krolestwa Reach (" + ReachId + ")", sd); return; }
            if (reach.IsEliminated) { Never("Reach zniszczone przed Czarnym Nurtem", sd); return; }
            if (!Alive(crown)) { Never("korona (" + CrownId + ") nie istnieje", sd); return; }
            // znacznik
            // ROT: BlackwaterSiegeEvent (oblezenie Przystani) w toku (stan 1) - zapas nie rusza (Stannis moze jeszcze zdobyc miasto); StannisWon bez odczytu - nie liczy sie
            // jak "przegral": czeka do dnia zapasu, potem jak zapas (King's Landing w koronie pilnuje, ze Stannis nie wygral).
            int bw = BwState(), won = StannisWon(), sg = SiegeState();
            string kind = null;
            if (bw == 2 && won == 1) { Never("Stannis wygral pod Czarnym Nurtem (ROT StannisWon)", sd); return; }
            if (bw == 2 && won == 0) kind = "rot";
            else if (sg != 1 && bw != 1 && sd >= SpareDay()) kind = "plan";
            if (kind == null && TestForce()) kind = "test";
            if (kind == null)
            {
                Wait(bw == 2 ? "brak odczytu ROT StannisWon (" + BwEvent + " zakonczone) - czeka do zapasu od dnia opowiesci " + ((int)SpareDay()).ToString(Inv) + " (" + SiegeEvent + " stan " + sg + ")"
                     : "Czarny Nurt jeszcze nie rozstrzygniety (ROT " + BwEvent + " stan " + bw + ", " + SiegeEvent + " stan " + sg + ", zapas od dnia opowiesci " + ((int)SpareDay()).ToString(Inv)
                       + (sg == 1 ? " - oblezenie w toku, zapas wstrzymany" : "") + ")", sd);
                return;
            }
            // kanon
            string why = null;
            var kl = Settlement.Find(KingsLandingId);
            var ds = K(DragonstoneId);
            var ab = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
            if (kl != null && (kl.MapFaction == null || kl.MapFaction.StringId != CrownId)) why = "King's Landing nie w koronie (" + (kl.MapFaction != null ? kl.MapFaction.StringId : "-") + ")";
            else if (PlayerRules(reach)) why = "gracz rzadzi Reach - poza regula";
            else if (PlayerRules(crown)) why = "gracz rzadzi korona - poza regula";
            else if (reach.IsAtWarWith(crown)) why = "Reach w wojnie z korona";
            else if (Alive(ds) && ab != null && ab.IsAllyWithKingdom(reach, ds)) why = "Reach w sojuszu ze Smocza Skala";
            else if (KingInTheNorth.CivilWarIn(reach)) why = "wojna domowa Diplomacy w Reach";
            else if (KingInTheNorth.CivilWarIn(crown)) why = "wojna domowa Diplomacy w koronie";
            if (why != null) { _waitSince = -1.0; Wait(why, sd); return; }
            // bezpieczna chwila (najwyzej UnsafeMaxDays dob)
            double now = CampaignTime.Now.ToDays;
            if (_waitSince < 0) _waitSince = now;
            bool cap = now - _waitSince >= UnsafeMaxDays;
            string unsafeWhy = KingInTheNorth.FoldUnsafeWhy(reach, "Reach");
            if (unsafeWhy != null && !cap) { Wait("bezpieczna chwila (" + unsafeWhy + "; najwyzej " + UnsafeMaxDays + " dob, minelo " + F1(now - _waitSince) + ")", sd); return; }
            // gracz w osadzie Reach (menu miasta / zamku / wsi) - zmiana krolestwa osady przy otwartym GameMenu: czekamy az wyjdzie (bez limitu 7 dob)
            string inSt = PlayerInSettlementOf(reach);
            if (inSt != null) { Wait("bezpieczna chwila (gracz w osadzie Reach: " + inSt + " - wstapienie po wyjsciu na mape)", sd); return; }
            DoFold(reach, crown, kind, cap ? unsafeWhy : null, sd, bw, won);
        }

        // ostatnie liczby (test)
        private static KingInTheNorth.FoldResult _last;
        private static string _pactText = "-";
        private static int _holdCallsAtFold = -1;

        private static void DoFold(Kingdom reach, Kingdom crown, string kind, string forcedWhy, double sd, int bw, int won)
        {
            _foldCount++;
            int dayC = (int)CampaignTime.Now.ToDays;
            Log.Info(P + "WYZWALACZ | dzien opowiesci " + F1(sd) + " (dzien kampanii " + dayC + ") | " + (kind == "rot" ? "ROT " + BwEvent + " zakonczone, StannisWon " + (won == 0 ? "NIE" : "brak odczytu")
                     : kind == "plan" ? "zapas osi Fabuli (ROT " + BwEvent + " stan " + bw + ", dzien >= " + ((int)SpareDay()).ToString(Inv) + ")" : "test (flaga " + AutoFlagName + ")")
                     + (forcedWhy != null ? " | po " + UnsafeMaxDays + " dobach czekania mimo: " + forcedWhy : "") + " | korona w wojnie z: " + Wars(crown) + " | Reach w wojnie z: " + Wars(reach) + ".");
            // pakty Diplomacy Reach i stan m104 (2) - przed zniszczeniem
            try { _pactText = Prologue.ReachFoldPrep(reach); } catch (Exception e) { Stumble("ReachFoldPrep", e); _pactText = "BLAD"; }
            _holdCallsAtFold = Prologue.HoldReachCalls;
            // migawka rodow i twierdz (wyjatek w srodku Fold - stan nie moze zostac "czeka", gdy rody sa juz w koronie)
            var preClans = new List<string>(); var preForts = new List<string>(); string preSeat = "", preRuler = "";
            try
            {
                preRuler = reach.RulingClan != null ? reach.RulingClan.StringId ?? "" : "";
                preSeat = reach.RulingClan != null && reach.RulingClan.HomeSettlement != null ? reach.RulingClan.HomeSettlement.StringId ?? "" : "";
                foreach (var c in reach.Clans)
                {
                    if (c == null || c.IsEliminated || c.StringId == null) continue;
                    preClans.Add(c.StringId);
                    foreach (var t in c.Fiefs) if (t != null && t.Settlement != null && !preForts.Contains(t.Settlement.StringId)) preForts.Add(t.Settlement.StringId);
                }
            }
            catch (Exception e) { Stumble("DoFold(migawka)", e); }
            KingInTheNorth.FoldResult r = null;
            try { r = KingInTheNorth.Fold(reach, crown, P, "Reach", "korona"); }
            catch (Exception e)
            {
                Stumble("Fold", e);
                int moved = 0;
                try { foreach (var id in preClans) { var c = FindClan(id); if (c != null && c.Kingdom == crown) moved++; } } catch { }
                if (moved == 0 && !reach.IsEliminated)
                {
                    Log.Info(P + "BLAD w srodku wstapienia (" + e.GetType().Name + ") - zaden rod nie przeszedl, Reach istnieje: proba znowu w nastepnej dobie | potkniecia " + _stumbles + ".");
                    return;
                }
                r = new KingInTheNorth.FoldResult { Clans = preClans.Count, Moved = moved, RulerId = preRuler, Seat = preSeat, SrcGone = reach.IsEliminated };
                r.ClanIds.AddRange(preClans); r.FortIds.AddRange(preForts);
                Log.Info(P + "BLAD w srodku wstapienia (" + e.GetType().Name + ") - rodow juz w koronie " + moved + "/" + preClans.Count + ", Reach zniszczone " + (reach.IsEliminated ? "TAK" : "NIE")
                         + " - stan ZROBIONE (aliasy Kroniki i m104 (2) jak po wstapieniu) | potkniecia " + _stumbles + ".");
            }
            _last = r;
            _st = 1; _day = (int)Math.Floor(sd); _dayC = dayC; _wy = kind; _player = r.Role; _waitSince = -1.0; _ruler = Clean(r.RulerId);
            _clans.Clear(); _clanSet.Clear(); _forts.Clear(); _fortSet.Clear();
            foreach (var x in r.ClanIds) { _clans.Add(Clean(x)); _clanSet.Add(x); }
            foreach (var x in r.FortIds) { _forts.Add(Clean(x)); _fortSet.Add(x); }
            _seat = r.Seat.Length > 0 ? Clean(r.Seat) : "";
            Clan tyrell = null;
            try { foreach (var c in Clan.All) if (c != null && c.StringId == TyrellClanId) { tyrell = c; break; } } catch { }
            string tyTxt = tyrell == null ? "brak rodu " + TyrellClanId : (tyrell.IsEliminated ? "wyeliminowany" : "w " + (tyrell.Kingdom != null ? tyrell.Kingdom.StringId : "-") + ", glowa " + KingInTheNorth.Name(tyrell.Leader) + ", lenna " + tyrell.Fiefs.Count);
            bool popup = false;
            try { popup = Tell(r); } catch (Exception e) { Stumble("Tell", e); }
            Log.Info(P + "ZROBIONE | Reach zniszczone " + (r.SrcGone ? "TAK" : "NIE") + " | rodow w koronie " + r.Moved + "/" + r.Clans + ", zniszczonych " + r.Destroyed + " | lenna " + r.Fiefs1 + "/" + r.Fiefs0
                     + " | Tyrell " + tyTxt + " | pokoj z wrogami tylko Reach " + r.Peace + (r.PeaceWith.Count > 0 ? " [" + string.Join(", ", r.PeaceWith.ToArray()) + "]" : "") + " | pakty Diplomacy: " + _pactText
                     + " | wojna Reach-korona " + (reach.IsAtWarWith(crown) ? "TAK" : "NIE") + " | siedziba (plotka) " + (_seat.Length > 0 ? _seat : "-") + " | komunikat gracza " + (popup ? "TAK" : "NIE")
                     + " | potkniecia " + _stumbles + ".");
            TestRun(reach, crown);
        }

        private static string Wars(Kingdom k)
        {
            try
            {
                var l = new List<string>();
                foreach (var f in k.FactionsAtWarWith) if (f != null && f.IsKingdomFaction && l.Count < 8) l.Add(f.StringId);
                return l.Count == 0 ? "nikim" : string.Join(", ", l.ToArray());
            }
            catch { return "?"; }
        }

        private const string PopupTitle = "The Reach Bends the Knee";
        private const string PopupBody = "After the Blackwater, House Tyrell and the lords of the Reach swear fealty to the Iron Throne. The Reach is no longer a realm of its own: "
                                         + "its lords keep their castles, towns and hosts and now serve the crown in King's Landing, and House Tyrell still rules its lands in the king's name.";
        private const string PopupVassal = "Your house goes with them and is now a vassal of the crown.";
        private const string PopupMerc = "Your company keeps its contract, now with the crown, on the same terms.";

        private static bool Tell(KingInTheNorth.FoldResult r)
        {
            string extra = r.Role == "wasal" ? PopupVassal : r.Role == "najemnik" ? PopupMerc : null;
            string text = PopupBody + (extra != null ? "\n\n" + extra : "");
            InformationManager.ShowInquiry(new InquiryData(PopupTitle, text, true, false, "Continue", null, null, null), true);
            Log.Player("After the Blackwater, House Tyrell and the lords of the Reach swear fealty to the Iron Throne." + (extra != null ? " " + extra : ""));
            return true;
        }

        // ---------------------------------------------------------------- proba autotestu (tylko DLL autotestu - LogGate 1/2 - i flaga armoury-reach332.flag)
        // Flaga wymusza tylko ZNACZNIK Czarnego Nurtu od 1. doby sesji (kanon i bezpieczna chwila - jak w grze). Linie "Reach do korony (332): test - Tn ... - wynik TAK|NIE|POMINIETY":
        // T1 migawka; T2 rody / lenna / partie / ludzie przed = po, zniszczonych 0; T3 armie; T4 Reach zniszczone, rody w koronie, Tyrell z lennami, brak wojen tylko Reach,
        // brak paktow Reach; T5 kontrola pieniedzy; T6 zapis tam i z powrotem; T7 Kronika (kexists, war, relk, plotka przez alias); T8 nastepna doba - m104 (2) bez
        // HoldReach (bez ODNOWIONY / konca sojuszu); T9 drugie wyzwolenie nic nie robi; T10 gracz (rola, jency). Bez wstapienia do 10. doby sesji - T0 POMINIETY.
        private const int TestLastDay = 10;
        private static bool _testDone, _testFlag, _t8Done, _flagSaid;
        private static int _testFlagDay = -1, _foldSession = -1;

        private static void TestReset() { _testDone = false; _testFlag = false; _testFlagDay = -1; _t8Done = false; _flagSaid = false; _foldSession = -1; _last = null; _holdCallsAtFold = -1; }

        private static void TLine(string t, string what, string res) { Log.Info(P + "test - " + t + " " + what + " - wynik " + res + "."); }

        private static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            if (_testFlagDay != _sessionDays)
            {
                _testFlagDay = _sessionDays;
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _testFlag = File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
                }
                catch { _testFlag = false; }
            }
            return _testFlag;
        }

        private static bool TestForce()
        {
            if (_testDone || !TestWanted() || _sessionDays < 1) return false;
            if (!_flagSaid) { _flagSaid = true; Log.Info(P + "test - flaga " + AutoFlagName + ": znacznik Czarnego Nurtu wymuszony w " + _sessionDays + ". dobie sesji (kanon i bezpieczna chwila jak w grze)."); }
            return true;
        }

        private static void TestAfter()
        {
            if (_testDone || _sessionDays < TestLastDay || !TestWanted()) return;
            _testDone = true;
            TLine("T0", "bez wstapienia do " + TestLastDay + ". doby sesji (stan " + (_st == 2 ? "nigdy: " + _never : "czeka: " + _lastWait) + ")", "POMINIETY");
        }

        private static void TestRun(Kingdom reach, Kingdom crown)
        {
            _foldSession = _sessionDays;
            if (_testDone || !TestWanted() || _last == null) return;
            var r = _last;
            try
            {
                TLine("T1", "migawka: rodow " + r.Clans + ", twierdz " + r.Fiefs0 + ", partii " + r.Parties0 + ", ludzi " + r.Men0, r.Clans > 0 ? "TAK" : "NIE");
                bool t2 = r.Moved == r.Clans && r.Destroyed == 0 && r.Fiefs1 == r.Fiefs0 && r.Parties1 == r.Parties0 && r.Men1 == r.Men0;
                TLine("T2", "rody " + r.Moved + "/" + r.Clans + " (zniszczonych " + r.Destroyed + "), lenna " + r.Fiefs1 + "/" + r.Fiefs0 + ", partie " + r.Parties1 + "/" + r.Parties0 + ", ludzie " + r.Men1 + "/" + r.Men0, t2 ? "TAK" : "NIE");
                TLine("T3", "armie: " + r.Armies + ", z tymi samymi czlonkami " + r.ArmiesOk + ", z ta sama spojnoscia " + r.CohOk, r.Armies == 0 ? "POMINIETY" : (r.ArmiesOk == r.Armies && r.CohOk == r.Armies ? "TAK" : "NIE"));
                int inCrown = 0; foreach (var id in _clans) { var c = FindClan(id); if (c != null && !c.IsEliminated && c.Kingdom == crown) inCrown++; }
                Clan ty = FindClan(TyrellClanId);
                bool tyOk = ty == null || (!ty.IsEliminated && ty.Kingdom == crown && (r.RulerId != TyrellClanId || ty.Fiefs.Count > 0));
                int reachOnly = 0; try { reachOnly = reach.IsEliminated ? 0 : KingInTheNorth.SourceOnlyWars(reach, crown).Count; } catch { }
                bool t4 = r.SrcGone && inCrown == r.Clans && tyOk && reachOnly == 0 && r.AllyGone && !reach.IsAtWarWith(crown);
                TLine("T4", "Reach zniszczone " + (r.SrcGone ? "TAK" : "NIE") + ", rodow w koronie " + inCrown + "/" + r.Clans + ", Tyrell " + (ty == null ? "brak" : (ty.Kingdom != null ? ty.Kingdom.StringId : "-") + " lenna " + ty.Fiefs.Count)
                      + ", wojen tylko Reach " + reachOnly + ", sojusz korona+Reach " + (r.AllyGone ? "brak" : "JEST") + ", pakty " + _pactText, t4 ? "TAK" : "NIE");
                TLine("T5", "kontrola pieniedzy (zdjete - dopisane) " + (r.Control == long.MinValue ? "brak" : r.Control.ToString(Inv)), r.Control == 0 ? "TAK" : "NIE");
                string e1 = Export(); Import(e1); string e2 = Export();
                TLine("T6", "zapis tam i z powrotem (" + e1.Length + " znakow)", e1 == e2 && _st == 1 ? "TAK" : "NIE");
                bool kx = Chronicle.Terms.Eval("kexists:empire_w");
                bool w1 = Chronicle.Terms.Eval("war:empire_w:battania"), w2 = Chronicle.Terms.Eval("war:vlandia:battania");
                int lords = ReachLords().Count;
                bool seatOk = _seat.Length == 0 || Settlement.Find(_seat) != null;
                TLine("T7", "Kronika: kexists:empire_w " + (kx ? "TAK" : "NIE") + ", war:empire_w:battania " + (w1 ? "TAK" : "NIE") + " = war:vlandia:battania " + (w2 ? "TAK" : "NIE")
                      + ", relk - glow rodow Reach w koronie " + lords + ", plotka w " + (_seat.Length > 0 ? _seat : "-"), kx && w1 == w2 && lords > 0 && seatOk ? "TAK" : "NIE");
                int c0 = _foldCount, n0 = crown.Clans.Count;
                Tick();
                bool t9 = _foldCount == c0 && crown.Clans.Count == n0 && _st == 1;
                TLine("T9", "drugie wyzwolenie: wstapien " + _foldCount + " (przed " + c0 + "), rodow korony " + crown.Clans.Count + " (przed " + n0 + ")", t9 ? "TAK" : "NIE");
                TLine("T10", "gracz (" + r.Role + ") rola i mnoznik " + (r.PlayerOk ? "zachowane" : "ZMIENIONE") + ", jency " + (r.PrisOk ? "ci sami" : "INNI") + ", " + r.Career, r.PlayerOk && r.PrisOk && r.CareerOk ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("TestRun", e); TLine("T?", "wyjatek " + e.GetType().Name, "NIE"); }
        }

        /// <summary>T8 w nastepnej dobie po wstapieniu (ta sama sesja): m104 (2) Reach nie wola HoldReach (bez odnowienia sojuszu i konca w dniu ~900).</summary>
        private static void TestAfterFold()
        {
            if (_testDone || _t8Done || _foldSession < 0 || _sessionDays <= _foldSession || !TestWanted()) return;
            _t8Done = true; _testDone = true;
            try
            {
                // doba m104 (2) biegnie po nas w tej samej CrownDaily - sprawdzamy liczbe wywolan HoldReach od chwili wstapienia (poprzednia doba) i stan Reach
                int calls = Prologue.HoldReachCalls - _holdCallsAtFold;
                string state = Prologue.ReachStateText;
                bool ok = calls == 0 && state.IndexOf("332", StringComparison.Ordinal) >= 0;
                TLine("T8", "doba po wstapieniu: wywolan HoldReach (sojusz, ODNOWIONY, koniec sojuszu) " + calls + ", stan m104 (2) Reach: " + state, ok ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("TestAfterFold", e); TLine("T8", "wyjatek " + e.GetType().Name, "NIE"); }
        }

        private static Clan FindClan(string id)
        {
            try { foreach (var c in Clan.All) if (c != null && c.StringId == id) return c; } catch { }
            return null;
        }
    }
}
