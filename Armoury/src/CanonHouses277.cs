using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 277 - RODY KANONU ZACHODU I ZIEM KORONNYCH "PO KSIAZCE" (Jeff 27.10 ok. 19:15: "Czemu lannisterowie maja tak malo rodow, w ksiazce tez tak malo mieli?" -
    /// po odpowiedzi i propozycji poprawki wedlug ksiag: "ksiazce"). Klucze CanonHouses277 (glowny), CanonHouses277Rename, CanonHouses277LiegePay.
    ///
    /// DLACZEGO (pomiar Jeffa, doba 108997 / 109003): korona (vlandia) ma 17 rodow z lennem i 19 twierdz, strona Robba 34 rody i 38 twierdz; kazda twierdza Zachodu i ziem
    /// koronnych jest juz w rekach swojego rodu kanonu (ROT-Map settlements.xml) - lenn nie ma komu dac. W ksiegach Tywin wezwal KAZDY rod lordowski Zachodu (takze bez
    /// zamku na mapie ROT) i oplacil je zlotem Lannisterow; w ROT te rody nie istnieja albo siedza jako rycerze BK (mniejsze parostwo) z majatkiem, kiesa i bohaterami,
    /// ale bez druzyny (179) i z dochodem 21-54 zl na dobe. Korone ogranicza liczba druzyn, nie pieniadze (Lannister kiesa 1.18 mln, pulap zoldu 20.5 tys., placi 2.9 tys.).
    ///
    /// CO ROBI (raz na zapis, pierwsza doba po wczytaniu albo pierwsza doba nowej kampanii; tylko kampania kanonu i tylko gdy krolestwo vlandia istnieje):
    ///  (1) dane: rody lordowskie (kolumna status "lordly") krain Westerlands i Crownlands z arm_houses_canon.tsv bez klanu ROT - Estren, Falwell, Jast, Moreland, Plumm,
    ///      Prester, Spicer, Stackspear; Buckwell, Chelsted, Hayford, Stokeworth (statusy ziem koronnych dopisane w tools/herby/dane - paczka 277);
    ///  (2) rod rycerza BK korony z majatkiem w okregu tej krainy, ktory juz nosi nazwe rodu - zostaje; rod rycerza z nazwa spoza kanonu (zapas GentryNames: nazwa wsi)
    ///      albo rodu wymarlego przed startem (Reyne) dostaje nazwe brakujacego rodu (klucz CanonHouses277Rename): najpierw rod, ktorego siedziba w pliku JEST wsia majatku
    ///      (Prester - Feastfires), potem rody z punktem (siedziba / osada podkrainy - CanonHouses.RefPos) do najblizszego majatku, potem reszta w kolejnosci pliku;
    ///      nazwa zajeta przez jakikolwiek rod swiata - pominiety. Zostali kandydaci - brakujace rody RYCERSKIE tej krainy ("knightly"), zostaja rycerzami.
    ///      Herb z pliku (CanonHouses.Usable - sprawdzony kod), nazwa przez Clan.ChangeClanName (jak Prologue.Kings);
    ///  (3) rod lordowski -> PAN: pelne parostwo BK dokladnie tak, jak BK PeerageKingdomDecision (CouncilData.SetPeerage, odbiciem; BK zmienia parostwo tylko rodom bez
    ///      prawa glosu - BKClanBehavior.AddPeerage / OnClanChangedKingdom - wiec zostaje), wiec GentryService.IsKnightClanNow = NIE: 179 (blokada nowej druzyny, limit
    ///      zoldu 0, rycerz w druzynie pana) i zachowania majatku BK (odsylanie do majatku, RaiseManpower) przestaja go dotyczyc; tier 3 (najnizszy tier, jaki ROT daje
    ///      rodowi lordowskiemu kanonu z ziemia: Brax, Banefort, Serrett, Westerling, Farman, Rosby) przez renome (Clan.AddRenown - renoma to nie pieniadz).
    ///  Co dobe (glowny klucz): glowa rodu pana jedzie w cudzej druzynie (rycerz 179) - wraca do domu (TeleportHeroAction, nie w bitwie ani oblezeniu); stan NotSpawned ->
    ///  czynny (GentryService.cs:264); rod bez druzyny i wolna glowa - druzyna glowy przez prywatne HeroSpawnCampaignBehavior.SpawnLordParty + GiveInitialItemsToParty
    ///  (ta sama droga co 274 W2: 166 SpawnPrefix, 159, E4/8 - ludzie szablonu z wolnych rak wsi). ODCHYLENIE OD PROJEKTU: gra sama (ConsiderSpawningLordParties) nie
    ///  wystawi tej druzyny - glowy rodow rycerzy sa "niewalczace" (Jeff 26.10: niewalczacy w sluzbie 141 z 141), a ocena dowodcy daje -5000 (HeroSpawnCampaignBehavior
    ///  GetHeroPartyCommandScore); druga druzyna rodu tylko droga gry (dowodca walczacy i miejsce w pulapie wlasnym 166).
    ///  ZOLD OD PANA LENNEGO (klucz CanonHouses277LiegePay, plik CanonHouses277.Pay.cs) - ta sama regula co W5 (KingdomTreasury.NeedAid), platnikiem jest pan z kanonu.
    /// Zapis "arm_rody277" (ponizej 1 KB): doba przeniesienia i dla kazdego rodu: id, rodzaj, stara i nowa nazwa, pan lenny, dodana renoma, tier przed i po.
    /// Wylaczone: rody zostaja panami z nazwami (parostwo BK jest w zapisie BK), druzyny zostaja, pan nie placi, nowych druzyn 277 nie ma.
    /// Linie "Rody kanonu (277): start sesji | ...", "... rod <rod> (...) | przed: ... | po: ...", "... przeniesienie | ...", "... dzien N | ..." (doba).
    /// </summary>
    internal static partial class CanonHouses277
    {
        internal const string Tag = "Rody kanonu (277): ";
        internal const string CrownKingdomId = "vlandia";
        internal const string AreaWest = "Westerlands", AreaCrown = "Crownlands";
        internal const int LordTier = 3;               // najnizszy tier rodu lordowskiego kanonu z ziemia w ROT (spclans.xml: Brax, Banefort, Serrett, Westerling, Farman, Rosby)
        internal const int KindLord = 1, KindKnight = 2;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CanonHouses277; } }
        private static bool RenameOn { get { var s = Settings.Current; return s != null && s.CanonHouses277 && s.CanonHouses277Rename; } }

        // ================================================================ stan zapisany (arm_rody277)
        internal sealed class Rec
        {
            public string Clan = "", House = "", Old = "", New = "", Liege = "";
            public int Kind, Ren, T0, T1;
        }
        private static int _doneDay = -1;
        private static readonly List<Rec> _recs = new List<Rec>();

        // ================================================================ sesja
        private static readonly Dictionary<Clan, Rec> _lords = new Dictionary<Clan, Rec>();   // rody panow 277 (rodzaj 1) w tej sesji
        private static bool _linked;
        private static bool _offSaid, _deferSaid;
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        // doba (linia)
        private static int _dSpawn, _dSpawnBlock, _dHomeFromParty, _dWaitBattle, _dPrisoner, _dWoke, _dRegrant;

        internal static void Reset()
        {
            _doneDay = -1; _recs.Clear(); _lords.Clear(); _linked = false; _offSaid = false; _deferSaid = false;
            _stumbles = 0; _dStumbles = 0; _err.Clear();
            _bkTried = false; _spawnTried = false; _miSpawn = null; _miGifts = null;
            ClearDay();
            PayReset();
            CanonHouses277Test.Reset();
        }

        private static void ClearDay() { _dSpawn = _dSpawnBlock = _dHomeFromParty = _dWaitBattle = _dPrisoner = _dWoke = _dRegrant = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("CanonHouses277." + where, e); } catch { }
        }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string Nm(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>"House Spicer" -> "Spicer" (nazwa klanu gry - jak klany ROT i BK: bez slowa "House").</summary>
        internal static string Short(string houseBase)
        {
            if (string.IsNullOrEmpty(houseBase)) return "";
            string b = houseBase.Trim();
            return b.StartsWith("House ", StringComparison.Ordinal) ? b.Substring(6).Trim() : b;
        }

        internal static Kingdom Crown()
        {
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && string.Equals(k.StringId, CrownKingdomId, StringComparison.Ordinal)) return k;
            return null;
        }

        // ================================================================ BK parostwo (odbiciem; obiekty BK nowe po kazdym wczytaniu - menedzer czytany przy kazdym uzyciu)
        private static bool _bkTried;
        private static PropertyInfo _cfgInstance, _cfgCourt, _councilPeerage, _isFull, _isLesser;
        private static MethodInfo _getCouncil, _setPeerage;
        private static ConstructorInfo _peerCtor;
        private static string _bkState = "nie sprawdzane";

        private static bool BkResolve()
        {
            if (_peerCtor != null && _getCouncil != null && _setPeerage != null) return true;
            if (_bkTried) return false;
            _bkTried = true;
            try
            {
                var cfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var court = AccessTools.TypeByName("BannerKings.Managers.CourtManager");
                var council = AccessTools.TypeByName("BannerKings.Managers.Court.CouncilData");
                var peer = AccessTools.TypeByName("BannerKings.Managers.Court.Peerage");
                if (cfg == null || court == null || council == null || peer == null) { _bkState = "BRAK (typy BK)"; return false; }
                _cfgInstance = AccessTools.Property(cfg, "Instance");
                _cfgCourt = AccessTools.Property(cfg, "CourtManager");
                _getCouncil = AccessTools.Method(court, "GetCouncil", new[] { typeof(Clan) });
                _setPeerage = AccessTools.Method(council, "SetPeerage", new[] { peer });
                _councilPeerage = AccessTools.Property(council, "Peerage");
                _isFull = AccessTools.Property(peer, "IsFullPeerage");
                _isLesser = AccessTools.Property(peer, "IsLesserPeerage");
                _peerCtor = peer.GetConstructor(new[] { typeof(TextObject), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                bool ok = _cfgInstance != null && _cfgCourt != null && _getCouncil != null && _setPeerage != null && _councilPeerage != null && _isFull != null && _isLesser != null && _peerCtor != null;
                _bkState = ok ? "TAK" : "BRAK (skladowe BK)";
                if (!ok) { _peerCtor = null; _getCouncil = null; _setPeerage = null; }
                return ok;
            }
            catch (Exception e) { _bkState = "BLAD (" + e.GetType().Name + ")"; _peerCtor = null; Stumble("BkResolve", e); return false; }
        }

        private static object Council(Clan c)
        {
            if (!BkResolve() || c == null) return null;
            var inst = _cfgInstance.GetValue(null, null);
            var court = inst != null ? _cfgCourt.GetValue(inst, null) : null;
            return court != null ? _getCouncil.Invoke(court, new object[] { c }) : null;   // BK GetCouncil tworzy rade, gdy jej nie ma
        }

        /// <summary>Parostwo BK rodu slowami: "pelne", "mniejsze", "brak", "?" (BK nieznany).</summary>
        internal static string PeerageText(Clan c)
        {
            try
            {
                var council = Council(c);
                if (council == null) return "?";
                var p = _councilPeerage.GetValue(council, null);
                if (p == null) return "brak";
                if ((bool)_isFull.GetValue(p, null)) return "pelne";
                if ((bool)_isLesser.GetValue(p, null)) return "mniejsze";
                return "brak";
            }
            catch (Exception e) { Stumble("PeerageText", e); return "?"; }
        }

        /// <summary>Pelne parostwo - dokladnie wynik BK PeerageKingdomDecision (glos, wybory, pasowanie, lenna, rada; bez wzmocnienia glosu).</summary>
        private static bool GrantFullPeerage(Clan c)
        {
            try
            {
                var council = Council(c);
                if (council == null) return false;
                var p = _peerCtor.Invoke(new object[] { new TextObject("{=9OhMK2Wk}Full Peerage"), true, true, true, true, true, false });
                _setPeerage.Invoke(council, new[] { p });
                return PeerageText(c) == "pelne";
            }
            catch (Exception e) { Stumble("GrantFullPeerage", e); return false; }
        }

        // ================================================================ druzyna glowy (prywatne metody gry - jak 274 W2)
        private static bool _spawnTried;
        private static MethodInfo _miSpawn, _miGifts;

        private static string SpawnResolve()
        {
            if (_miSpawn != null && _miGifts != null) return "TAK";
            if (_spawnTried) return "BRAK";
            _spawnTried = true;
            try
            {
                var t = typeof(HeroSpawnCampaignBehavior);
                _miSpawn = AccessTools.Method(t, "SpawnLordParty", new[] { typeof(Hero), typeof(bool) });
                _miGifts = AccessTools.Method(t, "GiveInitialItemsToParty", new[] { typeof(MobileParty) });
                return _miSpawn != null && _miGifts != null ? "TAK" : "BRAK";
            }
            catch (Exception e) { Stumble("SpawnResolve", e); return "BLAD"; }
        }

        // ================================================================ start sesji
        /// <summary>OnSessionLaunched (po wczytaniu zapisu albo przy nowej kampanii): rody z zapisu po id, linia startu.</summary>
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                Link();
                string spawn = SpawnResolve();
                BkResolve();
                if (!s.LogEnabled) return;
                var k = Crown();
                Log.Info(Tag + "start sesji | klucz " + (s.CanonHouses277 ? "TAK" : "NIE") + ", nowe nazwy " + (s.CanonHouses277Rename ? "TAK" : "NIE")
                         + ", zold od pana " + (s.CanonHouses277LiegePay ? "TAK" : "NIE")
                         + " | zakres: " + (Prologue.CanonHere ? "kampania kanonu" : "kampania klasyczna - nie dotyczy") + " | korona " + CrownKingdomId + " " + (k != null ? "TAK" : "NIE")
                         + " | BK parostwo " + _bkState + ", gra SpawnLordParty " + spawn + ", plik rodow " + (CanonHouses.Loaded ? "TAK" : "NIE")
                         + " | stan z zapisu: " + (_doneDay >= 0 ? "przeniesione dnia " + _doneDay : "nie") + ", panow " + Count(KindLord) + ", rycerzy z nazwa kanonu " + Count(KindKnight)
                         + ", w tej sesji panow " + _lords.Count + "." + CanonHouses277Test.StartText());
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static int Count(int kind) { int n = 0; foreach (var r in _recs) if (r.Kind == kind) n++; return n; }

        /// <summary>Rody 277 z zapisu -> klany tej sesji (po id).</summary>
        private static void Link()
        {
            _lords.Clear();
            _linked = true;
            if (_recs.Count == 0) return;
            var byId = new Dictionary<string, Clan>(StringComparer.Ordinal);
            foreach (var c in Clan.All) if (c != null && c.StringId != null && !byId.ContainsKey(c.StringId)) byId[c.StringId] = c;
            foreach (var r in _recs)
            {
                Clan c;
                if (r.Kind == KindLord && byId.TryGetValue(r.Clan, out c)) _lords[c] = r;
            }
        }

        // ================================================================ doba
        /// <summary>Raz na dobe (ArmouryBehavior, po GentryService.Daily, przed ClanBudget.Daily - pulap rodu tej doby widzi zold od pana).</summary>
        internal static void Daily()
        {
            ClearDay(); _dStumbles = 0;
            PayRoll();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = Today;
            try
            {
                if (!_linked) Link();
                var k = Crown();
                string off = !s.CanonHouses277 ? "klucz NIE" : !Prologue.CanonHere ? "kampania klasyczna - nie dotyczy" : k == null ? "brak krolestwa " + CrownKingdomId : null;
                if (off != null)
                {
                    if (!_offSaid && s.LogEnabled) Log.Info(Tag + "dzien " + today + " | wylaczone (" + off + ") | rody z zapisu " + _recs.Count + " | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
                    _offSaid = true;
                    return;
                }
                _offSaid = false;
                if (_doneDay < 0) Migrate(k, today, s);
                Keep(k);
                try { Pay(k, s, today); } catch (Exception e) { Stumble("Pay", e); }
                if (s.LogEnabled) Log.Info(DayLine(k, today));
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        // ================================================================ przeniesienie (raz na zapis)
        private sealed class Cand
        {
            public Clan C; public Settlement Estate; public string Area; public bool HasPos; public Vec2 Pos; public bool Used;
        }

        private static void Migrate(Kingdom k, int today, Settings s)
        {
            if (!CanonHouses.Loaded)
            {
                if (!_deferSaid) Log.Info(Tag + "przeniesienie odlozone | dzien " + today + " | plik " + CanonHouses.FileName + " nie wczytany (" + CanonHouses.Note + ") - proba co dobe (linia raz na sesje).");
                _deferSaid = true;
                return;
            }
            if (!BkResolve())
            {
                if (!_deferSaid) Log.Info(Tag + "przeniesienie odlozone | dzien " + today + " | BK parostwo " + _bkState + " - bez BK rod rycerza zostalby rycerzem 179; proba w nastepnej sesji.");
                _deferSaid = true;
                return;
            }
            // nazwy rodow swiata (gra: nazwa rodu jest unikalna - FactionHelper.IsClanNameApplicable)
            var names = new Dictionary<string, Clan>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in Clan.All)
            {
                if (c == null || c.Name == null) continue;
                string n = c.Name.ToString();
                if (!names.ContainsKey(n) || (names[n].IsEliminated && !c.IsEliminated)) names[n] = c;
            }
            // rody kanonu wedlug rdzenia ("House X")
            var byShort = new Dictionary<string, List<CanonHouses.House>>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in CanonHouses.All())
            {
                string sh = Short(h.Base.Length > 0 ? h.Base : h.Name);
                if (sh.Length == 0) continue;
                List<CanonHouses.House> l;
                if (!byShort.TryGetValue(sh, out l)) byShort[sh] = l = new List<CanonHouses.House>(2);
                l.Add(h);
            }
            // rycerze BK korony z majatkiem w okregach Zachodu i ziem koronnych
            var knights = new List<Cand>();
            foreach (var c in k.Clans)
            {
                try
                {
                    Settlement est;
                    if (c == null || c == Clan.PlayerClan || c.IsEliminated || !GentryService.IsKnightClanNow(c, out est) || est == null) continue;
                    string area = CanonHouses.AreaOf(est.StringId);
                    if (area != AreaWest && area != AreaCrown) continue;
                    knights.Add(new Cand { C = c, Estate = est, Area = area, HasPos = true, Pos = est.GetPosition2D });
                }
                catch (Exception e) { Stumble("Migrate(rycerz)", e); }
            }
            knights.Sort((a, b) => string.CompareOrdinal(a.C.StringId, b.C.StringId));
            var skipped = new List<string>();
            var lords = new List<KeyValuePair<CanonHouses.House, Cand>>();       // rod -> rycerz z ta nazwa (istniejacy) albo kandydat do nowej nazwy
            var missLord = new List<CanonHouses.House>();
            var missKnight = new List<CanonHouses.House>();
            foreach (var h in CanonHouses.All())
            {
                if (h.Area != AreaWest && h.Area != AreaCrown) continue;
                if (h.Region != AreaWest && h.Region != AreaCrown) continue;                 // bez podkrain (Dragonstone, Crackclaw Point - strona Stannisa)
                if (!h.Holder || h.Clans.Length > 0) continue;                                // rod z klanem ROT (Banefort, Rosby ...) - jak w ROT
                bool lordly = h.Status.StartsWith("lordly", StringComparison.Ordinal), knightly = h.Status.StartsWith("knightly", StringComparison.Ordinal);
                if (!lordly && !knightly) continue;
                string sh = Short(h.Base.Length > 0 ? h.Base : h.Name);
                Clan has;
                if (names.TryGetValue(sh, out has))
                {
                    var cand = knights.Find(x => x.C == has);
                    if (lordly && cand != null && cand.Area == h.Area) { cand.Used = true; lords.Add(new KeyValuePair<CanonHouses.House, Cand>(h, cand)); }
                    else if (lordly) skipped.Add(h.Key + " (nazwe nosi " + has.StringId + (has.Kingdom != null ? ", " + has.Kingdom.StringId : "") + (has.IsEliminated ? ", wyeliminowany" : "") + ")");
                    continue;   // rod rycerski z nazwa w swiecie - jak jest
                }
                if (lordly) missLord.Add(h); else missKnight.Add(h);
            }
            // kandydaci do nowej nazwy: nazwa spoza kanonu (zapas GentryNames) albo rod wymarly przed startem (holder = no, status extinct)
            var cands = new List<Cand>();
            foreach (var x in knights)
            {
                if (x.Used) continue;
                string n = Nm(x.C);
                List<CanonHouses.House> hs;
                bool canon = byShort.TryGetValue(n, out hs);
                bool dead = canon && hs.TrueForAll(h => h.Status.StartsWith("extinct", StringComparison.Ordinal) && !h.Holder);
                if (!canon || dead) cands.Add(x);
            }
            var renamed = new List<KeyValuePair<CanonHouses.House, Cand>>();
            var renamedKnights = new List<KeyValuePair<CanonHouses.House, Cand>>();
            if (RenameOn)
            {
                foreach (string area in new[] { AreaWest, AreaCrown })
                {
                    var ca = cands.FindAll(x => x.Area == area);
                    var tl = missLord.FindAll(h => h.Area == area);
                    var pairs = Assign(ca, tl);
                    foreach (var p in pairs) { p.Value.Used = true; renamed.Add(p); }
                    foreach (var h in tl) if (!pairs.Exists(p => p.Key == h)) skipped.Add(h.Key + " (brak rycerza do nowej nazwy w krainie " + area + ")");
                    var left = ca.FindAll(x => !x.Used);
                    var tk = missKnight.FindAll(h => h.Area == area);
                    foreach (var p in Assign(left, tk)) { p.Value.Used = true; renamedKnights.Add(p); }
                }
            }
            else foreach (var h in missLord) skipped.Add(h.Key + " (nowe nazwy wylaczone)");
            // wykonanie
            int nExisting = 0, nRenamed = 0, nKnight = 0;
            foreach (var p in lords) { if (Promote(p.Key, p.Value, false, k, today)) nExisting++; }
            foreach (var p in renamed) { if (Promote(p.Key, p.Value, true, k, today)) nRenamed++; }
            foreach (var p in renamedKnights) { if (RenameKnight(p.Key, p.Value)) nKnight++; }
            var stay = new List<string>();
            foreach (var x in cands) if (!x.Used) stay.Add(Nm(x.C) + " (" + x.Area + ")");
            _doneDay = today;
            Log.Info(Tag + "przeniesienie | dzien " + today + " | panowie " + (nExisting + nRenamed) + " (istniejace nazwy " + nExisting + ", nowe nazwy " + nRenamed + "), rycerze z nazwa kanonu " + nKnight
                     + ", rycerzy BK korony w okregach Zachodu i ziem koronnych " + knights.Count + " | pominiete " + skipped.Count + (skipped.Count > 0 ? " [" + string.Join("; ", skipped.ToArray()) + "]" : "")
                     + " | bez rodu kanonu zostaja " + stay.Count + (stay.Count > 0 ? " [" + string.Join(", ", stay.ToArray()) + "]" : "") + ".");
        }

        /// <summary>Przydzial rycerzy do rodow (stala kolejnosc): (1) siedziba rodu w pliku = wies majatku, (2) rody z punktem (CanonHouses.RefPos) - najblizszy majatek,
        /// (3) reszta rodow w kolejnosci pliku - kandydaci w kolejnosci id.</summary>
        private static List<KeyValuePair<CanonHouses.House, Cand>> Assign(List<Cand> cands, List<CanonHouses.House> targets)
        {
            var res = new List<KeyValuePair<CanonHouses.House, Cand>>();
            var cx = new double[cands.Count]; var cy = new double[cands.Count]; var cpos = new bool[cands.Count]; var cid = new string[cands.Count];
            for (int i = 0; i < cands.Count; i++) { cx[i] = cands[i].Pos.x; cy[i] = cands[i].Pos.y; cpos[i] = cands[i].HasPos; cid[i] = cands[i].Estate != null ? cands[i].Estate.StringId : ""; }
            var seats = new string[targets.Count][]; var tx = new double[targets.Count]; var ty = new double[targets.Count]; var tpos = new bool[targets.Count];
            for (int j = 0; j < targets.Count; j++)
            {
                seats[j] = targets[j].Seats;
                Vec2 p;
                tpos[j] = CanonHouses.RefPos(targets[j], out p);
                tx[j] = p.x; ty[j] = p.y;
            }
            int[] pick = AssignCore(cid, cx, cy, cpos, seats, tx, ty, tpos);
            for (int j = 0; j < targets.Count; j++) if (pick[j] >= 0) res.Add(new KeyValuePair<CanonHouses.House, Cand>(targets[j], cands[pick[j]]));
            return res;
        }

        /// <summary>Czysty rdzen przydzialu (test T1): dla celu j indeks kandydata albo -1.</summary>
        internal static int[] AssignCore(string[] candEstate, double[] cx, double[] cy, bool[] cpos, string[][] seats, double[] tx, double[] ty, bool[] tpos)
        {
            int n = candEstate.Length, m = seats.Length;
            var used = new bool[n]; var pick = new int[m];
            for (int j = 0; j < m; j++) pick[j] = -1;
            for (int j = 0; j < m; j++)
                for (int i = 0; i < n && pick[j] < 0; i++)
                    if (!used[i] && seats[j] != null && Array.IndexOf(seats[j], candEstate[i]) >= 0) { pick[j] = i; used[i] = true; }
            for (int j = 0; j < m; j++)
            {
                if (pick[j] >= 0 || !tpos[j]) continue;
                int best = -1; double bd = double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (used[i] || !cpos[i]) continue;
                    double dx = cx[i] - tx[j], dy = cy[i] - ty[j], d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; best = i; }
                }
                if (best >= 0) { pick[j] = best; used[best] = true; }
            }
            for (int j = 0; j < m; j++)
            {
                if (pick[j] >= 0) continue;
                for (int i = 0; i < n; i++) if (!used[i]) { pick[j] = i; used[i] = true; break; }
            }
            return pick;
        }

        private static int PartiesOf(Clan c, out int men)
        {
            men = 0; int n = 0;
            var wps = c.WarPartyComponents;
            if (wps == null) return 0;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsActive) continue;
                n++; men += mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
            }
            return n;
        }

        private static string HeadState(Hero h)
        {
            if (h == null) return "brak glowy";
            if (!h.IsAlive) return "nie zyje";
            if (h.IsPrisoner) return "w niewoli";
            if (h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h) return "prowadzi druzyne";
            if (h.PartyBelongedTo != null) return "w cudzej druzynie";
            if (h.IsActive) return "czynna";
            if (h.IsNotSpawned) return "nie wystawiona (NotSpawned)";
            return h.HeroState.ToString();
        }

        private static bool SetArms(CanonHouses.House h, Clan c)
        {
            try
            {
                if (!CanonHouses.Usable(h) || h.Banner == null || string.IsNullOrEmpty(h.Banner.BannerCode)) return false;
                c.Banner = new Banner(h.Banner.BannerCode);   // wlasny obiekt - herb z pliku zostaje bez zmian barw krolestwa
                return true;
            }
            catch (Exception e) { Stumble("SetArms", e); return false; }
        }

        private static void Rename(Clan c, string name)
        {
            var t = new TextObject("{=!}" + name);
            c.ChangeClanName(t, t);
        }

        private static bool Promote(CanonHouses.House h, Cand x, bool rename, Kingdom k, int today)
        {
            var c = x.C;
            try
            {
                string old = Nm(c);
                int men0; int par0 = PartiesOf(c, out men0);
                string peer0 = PeerageText(c);
                int t0 = c.Tier;
                string nn = Short(h.Base.Length > 0 ? h.Base : h.Name);
                if (rename) Rename(c, nn);
                bool arms = SetArms(h, c);
                bool peer = GrantFullPeerage(c);
                int ren = 0;
                var tm = Campaign.Current.Models.ClanTierModel;
                if (c.Tier < LordTier)
                {
                    float need = tm.GetRequiredRenownForTier(LordTier) - c.Renown;
                    if (need > 0) { c.AddRenown(need, false); ren = (int)Math.Ceiling(need); }
                    if (c.Tier < LordTier) { c.AddRenown(1f, false); ren += 1; }
                }
                GentryService.Forget(c);                                   // pamiec rodow rycerzy 179 bez tego rodu juz dzis (nie dopiero od jutra)
                Clan liege = LiegeOf(h, k);
                var r = new Rec { Clan = c.StringId, House = h.Key, Old = old, New = Nm(c), Liege = liege != null ? liege.StringId : "", Kind = KindLord, Ren = ren, T0 = t0, T1 = c.Tier };
                _recs.Add(r);
                _lords[c] = r;
                Log.Info(Tag + "rod " + h.Key + " (" + c.StringId + ", " + h.Area + ") | przed: nazwa " + old + ", rycerz, parostwo " + peer0 + ", tier " + t0 + ", druzyn " + par0 + ", ludzi " + men0
                         + ", majatek " + (x.Estate != null ? x.Estate.Name + " (" + x.Estate.StringId + ")" : "-")
                         + " | po: nazwa " + Nm(c) + ", pan, parostwo " + PeerageText(c) + (peer ? "" : " (BK NIE PRZYJAL)") + ", tier " + c.Tier + " (renoma +" + ren + "), glowa " + HeadState(c.Leader)
                         + " (" + Nm(c.Leader) + "), herb kanonu " + (arms ? "TAK" : "NIE") + ", pan lenny " + (liege != null ? Nm(liege) + " (" + liege.StringId + ")" : "brak") + ".");
                return peer;
            }
            catch (Exception e) { Stumble("Promote " + h.Key, e); return false; }
        }

        private static bool RenameKnight(CanonHouses.House h, Cand x)
        {
            var c = x.C;
            try
            {
                string old = Nm(c);
                Rename(c, Short(h.Base.Length > 0 ? h.Base : h.Name));
                bool arms = SetArms(h, c);
                _recs.Add(new Rec { Clan = c.StringId, House = h.Key, Old = old, New = Nm(c), Kind = KindKnight, T0 = c.Tier, T1 = c.Tier });
                Log.Info(Tag + "rod " + h.Key + " (" + c.StringId + ", " + h.Area + ") | przed: nazwa " + old + ", rycerz, parostwo " + PeerageText(c) + ", tier " + c.Tier
                         + ", majatek " + (x.Estate != null ? x.Estate.Name + " (" + x.Estate.StringId + ")" : "-") + " | po: nazwa " + Nm(c) + ", rycerz (rod rycerski kanonu), herb kanonu " + (arms ? "TAK" : "NIE") + ".");
                return true;
            }
            catch (Exception e) { Stumble("RenameKnight " + h.Key, e); return false; }
        }

        /// <summary>Pan lenny z pliku: rod o nazwie "liege" (House Baratheon of King's Landing), inaczej rod o tym rdzeniu z klanem ROT - najpierw "great"
        /// (House Lannister -> Lannister of Casterly Rock); klan w tym samym krolestwie, nie gracz, z zywa glowa. null - bez pana (bez zoldu).</summary>
        internal static Clan LiegeOf(CanonHouses.House h, Kingdom k)
        {
            if (h == null) return null;
            Clan best = null; bool bestGreat = false;
            foreach (var x in CanonHouses.All())
            {
                bool exact = string.Equals(x.Name, h.Liege, StringComparison.Ordinal);
                bool sameBase = !exact && h.LiegeBase.Length > 0 && string.Equals(x.Base, h.LiegeBase, StringComparison.Ordinal);
                if (!exact && !sameBase) continue;
                var c = CanonHouses.LiveClan(x);
                if (c == null || c.IsEliminated || c == Clan.PlayerClan || c.Kingdom != k || c.Leader == null || !c.Leader.IsAlive) continue;
                if (exact) return c;
                bool great = x.Status.IndexOf("great", StringComparison.Ordinal) >= 0;
                if (best == null || (great && !bestGreat)) { best = c; bestGreat = great; }
            }
            return best;
        }

        // ================================================================ co dobe: glowa wolna, druzyna glowy
        private static bool Free(Hero h)
        {
            return h != null && h.IsAlive && (h.IsActive || h.IsNotSpawned) && !h.IsChild && !h.IsPrisoner && h.PartyBelongedTo == null && h.PartyBelongedToAsPrisoner == null
                   && h != Hero.MainHero && (h.CurrentSettlement == null || !h.CurrentSettlement.IsUnderSiege);
        }

        private static void Keep(Kingdom k)
        {
            var spawnable = SpawnResolve() == "TAK";
            var beh = Campaign.Current.GetCampaignBehavior<HeroSpawnCampaignBehavior>();
            foreach (var kv in _lords)
            {
                var c = kv.Key;
                try
                {
                    if (c == null || c.IsEliminated || c.Kingdom != k || c == Clan.PlayerClan) continue;
                    // BK zmienia parostwo tylko rodom bez glosu - tu powinno byc 0; gdyby BK cofnelo, ta sama regula co przy przeniesieniu
                    if (PeerageText(c) == "mniejsze" && c.Fiefs.Count == 0) { if (GrantFullPeerage(c)) { _dRegrant++; GentryService.Forget(c); } }
                    var h = c.Leader;
                    if (h == null || !h.IsAlive) continue;
                    if (h.IsPrisoner) { _dPrisoner++; continue; }
                    var mp = h.PartyBelongedTo;
                    if (mp != null && mp.LeaderHero != h)
                    {
                        // rycerz 179 jechal w druzynie pana - wraca do domu (nie w bitwie ani oblezeniu: wtedy jutro)
                        if (mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null || (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege)) { _dWaitBattle++; continue; }
                        var home = HomeOf(c, mp);
                        if (home == null) { _dWaitBattle++; continue; }
                        TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, home);
                        if (h.PartyBelongedTo == null) _dHomeFromParty++; else { _dWaitBattle++; continue; }
                    }
                    if (c.WarPartyComponents != null && c.WarPartyComponents.Count > 0) continue;
                    if (!spawnable || beh == null || !Free(h)) continue;
                    if (h.IsNotSpawned) { h.ChangeState(Hero.CharacterStates.Active); _dWoke++; }
                    var np = _miSpawn.Invoke(beh, new object[] { h, false }) as MobileParty;
                    if (np == null) { _dSpawnBlock++; continue; }                      // 166 SpawnPrefix albo 179 (nie powinno - glowa rodu przechodzi zawsze)
                    _miGifts.Invoke(beh, new object[] { np });
                    _dSpawn++;
                    Log.Info(Tag + "nowa druzyna | dzien " + Today + " | " + Nm(c) + " (" + c.StringId + "): " + Nm(h) + ", ludzi " + (np.MemberRoster != null ? np.MemberRoster.TotalRegulars : 0)
                             + ", limit " + (np.Party != null ? np.Party.PartySizeLimit : 0) + " - droga gry SpawnLordParty (166, 159, E4/8).");
                }
                catch (Exception e) { Stumble("Keep(rod)", e); }
            }
        }

        private static Settlement HomeOf(Clan c, MobileParty mp)
        {
            var hs = c.HomeSettlement;
            if (hs != null && hs.MapFaction == c.MapFaction && !hs.IsUnderSiege) return hs;
            var k = c.Kingdom;
            if (k == null) return null;
            Vec2 pos = mp != null ? mp.GetPosition2D : (hs != null ? hs.GetPosition2D : Vec2.Invalid);
            Settlement best = null; float bd = float.MaxValue;
            foreach (var f in k.Fiefs)
            {
                var st = f != null ? f.Settlement : null;
                if (st == null || st.IsUnderSiege) continue;
                float d = pos.IsValid ? pos.DistanceSquared(st.GetPosition2D) : 0f;
                if (d < bd) { bd = d; best = st; }
            }
            return best;
        }

        // ================================================================ linia doby
        private static string DayLine(Kingdom k, int today)
        {
            int n = 0, inK = 0, par = 0, men = 0, lim = 0, active = 0, fullPeer = 0; long wage = 0;
            foreach (var kv in _lords)
            {
                var c = kv.Key;
                if (c == null || c.IsEliminated) continue;
                n++;
                if (c.Kingdom != k) continue;
                inK++;
                if (PeerageText(c) == "pelne") fullPeer++;
                var h = c.Leader;
                if (h != null && h.IsAlive && h.IsActive) active++;
                var wps = c.WarPartyComponents;
                if (wps == null) continue;
                for (int i = 0; i < wps.Count; i++)
                {
                    var mp = wps[i] != null ? wps[i].MobileParty : null;
                    if (mp == null || !mp.IsActive || !mp.IsLordParty) continue;
                    par++; men += mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
                    lim += mp.Party != null ? Math.Max(0, mp.Party.PartySizeLimit) : 0;
                    wage += Math.Max(0, mp.TotalWage);
                }
            }
            var sb = new StringBuilder(700);
            sb.Append(Tag).Append("dzien ").Append(today)
              .Append(" | rody ").Append(n).Append(" (w koronie ").Append(inK).Append(", pelne parostwo ").Append(fullPeer).Append(')')
              .Append(", druzyn ").Append(par).Append(" (nowe dzis ").Append(_dSpawn).Append(", blokada 166/179 ").Append(_dSpawnBlock).Append(')')
              .Append(", ludzi ").Append(men).Append(" / limit ").Append(lim).Append(", zold druzyn ").Append(wage).Append(" zl")
              .Append(" | glowy: czynne ").Append(active).Append(", w niewoli ").Append(_dPrisoner).Append(", z cudzej druzyny do domu ").Append(_dHomeFromParty)
              .Append(", czeka (bitwa) ").Append(_dWaitBattle).Append(", NotSpawned -> czynna ").Append(_dWoke).Append(", parostwo przywrocone ").Append(_dRegrant)
              .Append(" | ").Append(PayText())
              .Append(" | stosunek B/A (274, wczoraj) ").Append(CanonWarBalance.LastRatio.ToString("0.000", Inv))
              .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        // ================================================================ zapis
        private static string Esc(string v) { return (v ?? "").Replace("|", "/").Replace(";", ",").Replace("~", "-"); }

        internal static string Export()
        {
            var sb = new StringBuilder(64 + _recs.Count * 80);
            sb.Append("v1|").Append(_doneDay.ToString(Inv)).Append('|');
            for (int i = 0; i < _recs.Count; i++)
            {
                var r = _recs[i];
                if (i > 0) sb.Append(';');
                sb.Append(Esc(r.Clan)).Append('~').Append(r.Kind.ToString(Inv)).Append('~').Append(Esc(r.House)).Append('~').Append(Esc(r.Old)).Append('~').Append(Esc(r.New))
                  .Append('~').Append(Esc(r.Liege)).Append('~').Append(r.Ren.ToString(Inv)).Append('~').Append(r.T0.ToString(Inv)).Append('~').Append(r.T1.ToString(Inv));
            }
            return sb.ToString();
        }

        /// <summary>Brak klucza (stary zapis) = przeniesienie jeszcze nie bylo. Zly napis - stan pusty (przeniesienie od nowa nie zmieni nazw juz zmienionych: rody
        /// z nazwa kanonu nie sa kandydatami, rody pana nie sa rycerzami).</summary>
        internal static void Import(string v)
        {
            _doneDay = -1; _recs.Clear(); _lords.Clear(); _linked = false;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var parts = v.Split('|');
                if (parts.Length < 3 || parts[0] != "v1") return;
                _doneDay = int.Parse(parts[1], Inv);
                if (parts[2].Length == 0) return;
                foreach (var row in parts[2].Split(';'))
                {
                    var f = row.Split('~');
                    if (f.Length < 9) continue;
                    _recs.Add(new Rec { Clan = f[0], Kind = int.Parse(f[1], Inv), House = f[2], Old = f[3], New = f[4], Liege = f[5], Ren = int.Parse(f[6], Inv), T0 = int.Parse(f[7], Inv), T1 = int.Parse(f[8], Inv) });
                }
            }
            catch (Exception e) { _doneDay = -1; _recs.Clear(); Stumble("Import", e); }
        }

        /// <summary>Test T4: rody panow 277 w koronie - pelne parostwo BK, tier >= 3, nie rycerze 179 (IsKnightClanNow). Przed przeniesieniem - POMINIETY.</summary>
        internal static void T4(Action<string, bool?> line)
        {
            if (_doneDay < 0 || _lords.Count == 0) { line("T4 stan rodow: przeniesienia jeszcze nie bylo albo brak rodow panow", null); return; }
            var k = Crown();
            int n = 0, bad = 0; var sb = new StringBuilder();
            foreach (var kv in _lords)
            {
                var c = kv.Key;
                if (c == null || c.IsEliminated || c.Kingdom != k) continue;
                n++;
                Settlement est;
                bool knight = GentryService.IsKnightClanNow(c, out est);
                string peer = PeerageText(c);
                if (knight || peer != "pelne" || c.Tier < LordTier) { bad++; if (sb.Length < 300) sb.Append(Nm(c)).Append(" (").Append(peer).Append(", tier ").Append(c.Tier).Append(knight ? ", rycerz" : "").Append(") "); }
            }
            line("T4 stan rodow: panow w koronie " + n + ", zlych " + bad + (bad > 0 ? " [" + sb.ToString().Trim() + "]" : ""), n > 0 ? (bool?)(bad == 0) : null);
        }

        /// <summary>Test T3: Export -> Import -> Export bez roznic (na kopii stanu - stan wraca).</summary>
        internal static bool RoundTrip(out string why)
        {
            why = "";
            string a = Export();
            int d0 = _doneDay; var keep = new List<Rec>(_recs);
            try
            {
                Import(a);
                string b = Export();
                if (a != b) { why = "rozne napisy"; return false; }
                return true;
            }
            finally { _doneDay = d0; _recs.Clear(); _recs.AddRange(keep); Link(); }
        }
    }
}
