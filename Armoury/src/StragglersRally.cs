using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// N7 - ROZBICI WRACAJA POD CHORAGIEW (decyzja Jeffa 13.10 "Proporcje pasuja": "zolnierze z pobitej armii wracaja do armii, ale nie wszyscy; wiekszosc boi sie
    /// dezercji, wiec wraca; czesc wybiera zycie banity, a czesc wraca do domow" - takze bitwy w polu). Wylacznik StragglersRally.
    ///
    /// Kogo dotyczy: szeregowi DRUZYN LORDOW (partia rodu z wodzem, takze kompanie najemne i druzyna gracza) pobitych w bitwie objetej H3 (pole, autobitwa
    /// gracza F1, pod murami N3) oraz w walce gracza w polu (misja - H3 jej nie rusza): druzyna gracza i druzyny lordow pobitej strony (recenzja O2 - jedna
    /// zasada dla jednego zjawiska; "jego rozbici czekaja tak samo"). Bandy, karawany, tabory wsi, zalogi, milicje, Inni - bez zmian. Zabici i jency - bez zmian
    /// (H3). Zmienia sie tylko los ROZBITYCH tych druzyn (RoutedInBattle przy MapEventEnded - OutlawLaw.RoutedH3Body):
    ///  - podzial (Jeff): 55% pod choragiew, 30% do domu, 15% do lasu; pod choragiew weterani (tier 4+) 70%, tier 3 55%, rekruci (tier 1-2) 45%, reszta dom : las
    ///    = 2 : 1; kraj WLASNY (wlasciciel warowni regionu bitwy = frakcja pobitej druzyny): pod choragiew -5, dom +10, las -5 pkt; kraj OBCY: +5 / -10 / +5;
    ///    zalegly zold (WarLedger - doby zaleglosci druzyny, najdluzsza w rodzie): -10 pkt powrotu za kazdy pelny tydzien (najwyzej do 20%), odjeci - do lasu;
    ///    Nocna Straz: 80% pod choragiew, nikt do domu, reszta do lasu;
    ///  - do domu i do lasu - te same drogi co dotad (karczma E4/10, SendHome, pula wyrzutkow regionu, bez prawa wyrzutkow las = dom);
    ///  - pod choragiew: ZBIORKA w najblizszej bitwie twierdzy rodu (bez twierdzy - miasto, potem zamek krolestwa; potem siedziba rodu, potem najblizsze
    ///    niewrogie miasto; nic - podzial dom : las 2 : 1, Straz - las) po 3-10 dobach (droga piesza z miejsca bitwy, MarketCarts.PerDay); potem czekaja
    ///    najwyzej StragglersRallyWaitDays dob na pana. Raz na dobe kazda gotowa grupa szuka najblizszej druzyny SWOJEGO rodu (pan wrocil z niewoli, nowa
    ///    druzyna, inna druzyna rodu; gracz - jego druzyna i druzyny jego rodu) w zasiegu StragglersRallyReachDays dob marszu od osady zbiorki - druzyna poza
    ///    bitwa, nie na morzu, z wodzem na wolnosci, nie w oblezonej osadzie - i MASZERUJE do niej tyle dob, ile wynosi droga (recenzja K8); po dojsciu druzyny
    ///    w zasiegu przyjmuja najwyzej tylu, na ilu maja miejsce w limicie (PartySizeLimit) i w zoldzie: gdy druzyna ma limit zoldu (pulap ClanBudget przez
    ///    SetWagePaymentLimit) - GetAvailableWageBudget / zold czlowieka, ta sama miara co werbunek gry (RecruitmentCampaignBehavior :490); Straz bez zoldu - pula
    ///    Strazy w ludziach (ClanBudget.WatchRoom, czytana na zywo). Weterani pierwsi. Reszta czeka dalej; gdy druzyny w zasiegu juz nie ma - wracaja do osady.
    ///    Osada zbiorki oblezona - tej doby bez odbioru; zdobyta przez wroga rodu - grupa idzie do nowej osady zbiorki (FindRally od tej osady), a gdy takiej
    ///    nie ma - rozchodzi sie (recenzja K2). Po czasie czekania: polowa do domu, polowa do lasu (Straz - las Polnocy, WatchLaw.NorthRegionAt) tymi samymi drogami.
    /// Ksiega ludzi: nikt nie powstaje i nie znika. Udzialy pochodzenia zdjete z druzyny przy rozbiciu (Take FRouted - jak dotad) jada z grupa (dokad: "do zbiorki
    /// pod choragiew (N7)"), przy odbiorze wracaja do druzyny (Give FRally), przy rozejsciu - PoolShares / SendHome jak dotad (i schodza z "do zbiorki" - netto,
    /// RallyOut). W rownaniu regionu czekajacy maja wlasne konto Rally (menu: "Stragglers waiting to rejoin their lords"). Kontrola linii doby:
    /// czekali wczoraj + nowi - odebrani - rozeszli sie - zgubieni = czekaja dzis. Zloto: nic - czekajacy nie sa w druzynie, wiec nie dostaja zoldu; sakiewka
    /// ludzi zostaje przy druzynie (MenPurse, jak dotad); konie zostaja w zbiorowni druzyny (DTE) - wracajacy jezdziec jest jak nowy rekrut konny.
    /// Zapis: klucz "arm_n7_rally" (SaveText.Sync), wersja 1; brak klucza = pusta pula. Linia doby "Rozbici wracaja (N7): dzien" (blad linii - linia "BLAD LINII"
    /// z potknieciami), wpis w bitwy.log "Bitwa: N7 rozbici dzien" na kazda pobita druzyne lorda z rozbitymi. Potkniecia liczone per obiekt (bez globalnego wylacznika).
    /// </summary>
    internal static class StragglersRally
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.StragglersRally; } }

        // ---------------------------------------------------------------- regula (parametry Jeffa 13.10 - nie zmieniac bez jego slowa)
        private const double BackRecruit = 0.45, BackRegular = 0.55, BackVeteran = 0.70;   // tier 1-2 / 3 / 4+
        private const double HomeOfRest = 2.0 / 3.0;                                        // reszta: dom 30 : las 15
        private const double ArrearsPerWeek = 0.10, BackFloor = 0.20;                       // -10 pkt za tydzien zaleglego zoldu, najwyzej do 20%
        private const double WatchBack = 0.80;                                              // Nocna Straz: 80% wraca, nikt do domu
        // propozycja N7 (kraj): wlasny - blizej domu; obcy - dom daleko, wiecej trzyma sie choragwi albo idzie w las
        private const double OwnBack = -0.05, OwnHome = 0.10, ForeignBack = 0.05, ForeignHome = -0.10;
        private const int GatherMin = 3, GatherMax = 10;
        private const int VeteranTier = 4, RegularTier = 3;
        private const string Ver = "1";

        // ---------------------------------------------------------------- znacznik pobitej druzyny (wynik bitwy -> rozbici przy MapEventEnded)
        internal sealed class Mark
        {
            public MapEvent Me; public MobileParty Mp; public Clan Clan; public IFaction Fac;
            public bool Own, Watch, Player; public int Weeks, Day; public Settlement Rally; public int Gather; public string Name;
            public int Routed, Back, Home, Woods; public double EB, EH, EW;
        }
        private static readonly List<Mark> _marks = new List<Mark>();

        // ---------------------------------------------------------------- grupy czekajacych (stan w zapisie)
        private sealed class TN { public string Id; public CharacterObject C; public int N; }
        private sealed class G
        {
            public string ClanId, RallyId; public Clan Clan; public Settlement Rally;
            public int Day, Ready; public bool Watch, Announced;
            public int Arrive;                      // recenzja K8: doba dojscia do druzyny rodu (0 - w osadzie zbiorki; > dzis - w marszu)
            public readonly List<TN> T = new List<TN>();
            public PeopleBook.Shares O = new PeopleBook.Shares();
            public int Men { get { int n = 0; for (int i = 0; i < T.Count; i++) n += Math.Max(0, T[i].N); return n; } }
        }
        private static readonly List<G> _groups = new List<G>();
        private static bool _needResolve;
        private static int _lastW = 0;              // czekajacy z konca poprzedniej linii (po wczytaniu - z zapisu)
        private static int _importN = -1, _importBad, _badIds;
        private static readonly StringBuilder _xsb = new StringBuilder(4096);   // recenzja K9: jeden bufor zapisu (linia doby liczy znaki bez nowego napisu)

        // ---------------------------------------------------------------- liczniki doby
        private static int _dParties, _dBattles, _dRouted, _dBack, _dHome, _dWoods, _dNoRally, _dPlayerBack, _dBadMark;
        private static double _dEB, _dEH, _dEW;
        private static MapEvent _lastMe;
        private static int _dPicked, _dPickParties, _dPickPlayer, _dReady, _dAdded, _dMarchStart, _dMarchBack, _dMoved;
        private static int _dDispHome, _dDispWoods, _dDispTavern, _dDispLost, _dExpire, _dOff, _dNoClan, _dLostRally, _dLoadLost, _dStale;
        private static int _dWaitNoParty, _dWaitFar, _dWaitFull, _dWaitWage, _dWaitSiege;
        private static double _dFarDays; private static int _dFarN;
        private static int _stMark, _stSplit, _stPick, _stDisp, _stSave, _stLine;
        private static long _ticks;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            _marks.Clear(); _groups.Clear(); _needResolve = false; _lastW = 0; _importN = -1; _importBad = 0; _badIds = 0;
            _forts = null; _fortsDay = -1; _err.Clear(); _xsb.Length = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            _dParties = _dBattles = _dRouted = _dBack = _dHome = _dWoods = _dNoRally = _dPlayerBack = _dBadMark = 0; _dEB = _dEH = _dEW = 0; _lastMe = null;
            _dPicked = _dPickParties = _dPickPlayer = _dReady = _dAdded = _dMarchStart = _dMarchBack = _dMoved = 0;
            _dDispHome = _dDispWoods = _dDispTavern = _dDispLost = _dExpire = _dOff = _dNoClan = _dLostRally = _dLoadLost = _dStale = 0;
            _dWaitNoParty = _dWaitFar = _dWaitFull = _dWaitWage = _dWaitSiege = 0; _dFarDays = 0; _dFarN = 0;
            _stMark = _stSplit = _stPick = _stDisp = _stSave = _stLine = 0;
            _ticks = 0;
        }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static double PerDay { get { return Math.Max(1.0, MarketCarts.PerDay(Settings.Current)); } }

        private static void Stumble(ref int counter, string where, Exception e)
        {
            counter++;
            if (_err.Add(where)) Log.Error("StragglersRally." + where, e);
        }

        // ================================================================ znacznik (LosersFlee.Apply - wynik bitwy; partia jeszcze cala)
        /// <summary>Pobita druzyna lorda z rozbitymi w bitwie objetej H3 - znacznik dla rozbitych przy MapEventEnded. Nie rzuca.</summary>
        internal static void MarkBeaten(MobileParty mp, MapEvent me, Vec2 pos)
        {
            if (!On || mp == null || me == null) return;
            var sw = Stopwatch.StartNew();
            try
            {
                var m = MakeMark(mp, me, pos);
                if (m != null) _marks.Add(m);
            }
            catch (Exception e) { Stumble(ref _stMark, "MarkBeaten", e); }
            finally { _ticks += sw.ElapsedTicks; }
        }

        private static Mark MakeMark(MobileParty mp, MapEvent me, Vec2 pos)
        {
            bool main = mp == MobileParty.MainParty;
            var c = main ? Clan.PlayerClan : mp.ActualClan;
            if (c == null || c.IsBanditFaction || c.IsEliminated) return null;
            if (!main && (!mp.IsLordParty || Undead.Party(mp))) return null;
            var m = new Mark { Me = me, Mp = mp, Clan = c, Fac = mp.MapFaction ?? c.MapFaction, Player = c == Clan.PlayerClan, Day = Today };
            try { m.Name = mp.Name != null ? mp.Name.ToString() : "?"; } catch { m.Name = "?"; }
            var reg = OutlawLaw.RegionAt(pos);
            m.Own = reg != null && m.Fac != null && reg.MapFaction == m.Fac;
            m.Watch = IsWatch(mp);
            m.Weeks = ArrearsDays(mp, c) / 7;
            m.Rally = FindRally(c, pos);
            // recenzja K7: id rodu / osady ze znakiem separatora zapisu - grupa nie przezylaby zapisu (ludzie znikneliby bez nazwy); bez zbiorki - dom : las jak przy braku osady
            if (m.Rally != null && (Bad(c.StringId) || Bad(m.Rally.StringId))) { m.Rally = null; _dBadMark++; }
            if (m.Rally != null) m.Gather = Math.Max(GatherMin, Math.Min(GatherMax, (int)Math.Ceiling(pos.Distance(m.Rally.GetPosition2D) / PerDay)));
            return m;
        }

        /// <summary>Znacznik dla rozbitych tej partii w tej bitwie (zdejmowany przy uzyciu); walka gracza w polu (misja; H3 jej nie rusza) - liczony tu dla
        /// druzyny gracza i druzyn lordow pobitej strony (recenzja O2). null - jak dotad.</summary>
        internal static Mark For(MapEvent me, MobileParty mp, BattleSideEnum side)
        {
            if (!On || me == null || mp == null) return null;
            try
            {
                for (int i = 0; i < _marks.Count; i++)
                {
                    var m = _marks[i];
                    if (ReferenceEquals(m.Mp, mp) && ReferenceEquals(m.Me, me)) { _marks.RemoveAt(i); return m; }
                }
                // walka gracza w polu - "jego rozbici czekaja tak samo" (decyzja Jeffa); lordowie pobici przez gracza i towarzysze gracza - ta sama zasada
                if (me.IsPlayerMapEvent && me.IsFieldBattle && !me.IsNavalMapEvent && me.DefeatedSide != BattleSideEnum.None && side == me.DefeatedSide)
                    return MakeMark(mp, me, me.Position.ToVec2());
            }
            catch (Exception e) { Stumble(ref _stMark, "For", e); }
            return null;
        }

        private static bool IsWatch(MobileParty mp)
        {
            try { return PeopleBook.OriginLive ? PeopleBook.ServesWatch(mp) : CrownGifts.IsWatchParty(mp); }
            catch { return false; }
        }

        /// <summary>Doby zaleglego zoldu (WarLedger, ta sama miara co dezercja z zaleglosci): najdluzsza z pobitej druzyny i druzyn jej rodu.</summary>
        private static int ArrearsDays(MobileParty mp, Clan c)
        {
            int d = WarLedger.UnpaidDays(mp);
            try
            {
                var wps = c != null ? c.WarPartyComponents : null;
                if (wps != null) for (int i = 0; i < wps.Count; i++) { var p = wps[i] != null ? wps[i].MobileParty : null; if (p != null) d = Math.Max(d, WarLedger.UnpaidDays(p)); }
            }
            catch { }
            return Math.Max(0, d);
        }

        // ---------------------------------------------------------------- osada zbiorki
        private static List<Settlement> _forts;
        private static int _fortsDay = -1;

        private static List<Settlement> Forts()
        {
            int day = Today;
            if (_forts != null && _fortsDay == day) return _forts;
            _fortsDay = day;
            _forts = new List<Settlement>();
            foreach (var st in Settlement.All) if (st != null && st.IsFortification) _forts.Add(st);
            return _forts;
        }

        private static bool Hostile(Settlement st, Clan c)
        {
            var f = st.MapFaction; var cf = c.MapFaction;
            return f != null && cf != null && f != cf && FactionManager.IsAtWarAgainstFaction(f, cf);
        }

        /// <summary>Twierdza rodu najblizsza punktowi (nieoblegana); brak - miasto, potem zamek krolestwa; potem siedziba rodu (wies - jej twierdza), gdy niewroga;
        /// potem najblizsze niewrogie miasto. null - brak.</summary>
        private static Settlement FindRally(Clan c, Vec2 pos)
        {
            var forts = Forts();
            Settlement best = null; float bd = float.MaxValue;
            foreach (var st in forts)
            {
                if (st.OwnerClan != c || st.IsUnderSiege) continue;
                float d = pos.DistanceSquared(st.GetPosition2D);
                if (d < bd) { bd = d; best = st; }
            }
            if (best != null) return best;
            var k = c.Kingdom;
            if (k != null)
            {
                for (int pass = 0; pass < 2 && best == null; pass++)
                    foreach (var st in forts)
                    {
                        if (st.MapFaction != k || st.IsUnderSiege || (pass == 0 ? !st.IsTown : st.IsTown)) continue;
                        float d = pos.DistanceSquared(st.GetPosition2D);
                        if (d < bd) { bd = d; best = st; }
                    }
                if (best != null) return best;
            }
            var hs = c.HomeSettlement;
            try { if (hs != null && hs.IsVillage && hs.Village != null) hs = hs.Village.Bound; } catch { hs = null; }
            if (hs != null && hs.IsFortification && !hs.IsUnderSiege && !Hostile(hs, c)) return hs;
            foreach (var st in forts)
            {
                if (!st.IsTown || st.IsUnderSiege || Hostile(st, c)) continue;
                float d = pos.DistanceSquared(st.GetPosition2D);
                if (d < bd) { bd = d; best = st; }
            }
            return best;
        }

        // ================================================================ podzial rozbitych (OutlawLaw.RoutedH3Body, typ po typie)
        /// <summary>Udzialy: pod choragiew b, do domu h, do lasu w (suma 1) dla typu t przy znaczniku m (bez osady zbiorki - b rozdzielone dom : las 2 : 1).</summary>
        private static void Parts(Mark m, CharacterObject t, bool noRally, out double b, out double h, out double w)
        {
            if (m.Watch) { b = WatchBack; h = 0.0; }
            else
            {
                int tier = t.Tier;
                b = tier >= VeteranTier ? BackVeteran : (tier >= RegularTier ? BackRegular : BackRecruit);
                h = (1.0 - b) * HomeOfRest;
                if (m.Own) { b += OwnBack; h += OwnHome; } else { b += ForeignBack; h += ForeignHome; }
            }
            if (m.Weeks > 0 && b > BackFloor) b = Math.Max(BackFloor, b - ArrearsPerWeek * m.Weeks);    // odjeci - do lasu (w = reszta)
            w = Math.Max(0.0, 1.0 - b - h);
            if (m.Rally == null || noRally)
            {
                if (m.Watch) w += b; else { h += b * HomeOfRest; w += b * (1.0 - HomeOfRest); }
                b = 0.0;
            }
        }

        /// <summary>n rozbitych typu t: ilu idzie do zbiorki (dopisani do grupy z udzialami wycietymi ze slice) i ilu do lasu (toWoods); reszta - do domu u wolajacego.
        /// Blad przed dopisaniem do grupy - 0 / 0 i udzialy wracaja do slice (wolajacy wysyla wszystkich do domu jak dotad); blad po dopisaniu - wynik jak bez bledu
        /// (recenzja O7: ludzie nie moga byc naraz w grupie i w domu).</summary>
        internal static int Split(Mark m, CharacterObject t, int n, PeopleBook.Shares slice, out int toWoods)
        {
            toWoods = 0;
            if (m == null || t == null || n <= 0) return 0;
            var sw = Stopwatch.StartNew();
            PeopleBook.Shares cut = null;
            bool committed = false;
            int nb = 0, nw = 0;
            try
            {
                double b, h, w;
                Parts(m, t, Bad(t.StringId), out b, out h, out w);
                nb = Math.Max(0, Math.Min(n, MBRandom.RoundRandomized((float)(n * b))));
                nw = Math.Max(0, Math.Min(n - nb, MBRandom.RoundRandomized((float)(n * w))));
                if (nb > 0)
                {
                    cut = slice != null ? slice.Cut(nb) : null;
                    AddGroup(m, t, nb, cut, ref committed);
                }
                m.EB += n * b; m.EH += n * h; m.EW += n * w;
                toWoods = nw;
                return nb;
            }
            catch (Exception e)
            {
                Stumble(ref _stSplit, "Split", e);
                if (committed) { toWoods = nw; return nb; }
                if (cut != null && slice != null) { try { slice.AddAll(cut); } catch { } }
                toWoods = 0; return 0;
            }
            finally { _ticks += sw.ElapsedTicks; }
        }

        /// <summary>Po typie: ile faktycznie poszlo (las wedlug wolajacego - bez prawa wyrzutkow las = dom).</summary>
        internal static void Note(Mark m, int n, int back, int woods)
        {
            if (m == null || n <= 0) return;
            m.Routed += n; m.Back += back; m.Woods += woods; m.Home += Math.Max(0, n - back - woods);
        }

        /// <summary>Dopisanie n ludzi typu t do grupy (rod, osada, doba bitwy, doba gotowosci, Straz - recenzja K8: czas zbiorki i znacznik Strazy wlasne dla grupy).
        /// Najpierw wszystko, co moze rzucic (wyszukanie, nowe obiekty); potem sam zapis stanu - committed = true tuz przed nim (recenzja O7).</summary>
        private static void AddGroup(Mark m, CharacterObject t, int n, PeopleBook.Shares sh, ref bool committed)
        {
            int day = Today;
            int ready = day + m.Gather;
            G g = null;
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                var x = _groups[i];
                if (x.Day == day && x.Ready == ready && x.Watch == m.Watch && ReferenceEquals(x.Clan, m.Clan) && ReferenceEquals(x.Rally, m.Rally)) { g = x; break; }
            }
            bool newG = g == null;
            if (newG) g = new G { Clan = m.Clan, ClanId = m.Clan.StringId, Rally = m.Rally, RallyId = m.Rally.StringId, Day = day, Ready = ready, Watch = m.Watch };
            TN tn = null;
            for (int i = 0; i < g.T.Count; i++) if (ReferenceEquals(g.T[i].C, t)) { tn = g.T[i]; break; }
            bool newT = tn == null;
            if (newT) tn = new TN { C = t, Id = t.StringId };
            if (sh == null) sh = PeopleBook.Shares.None(n);
            // od tego miejsca tylko zapis stanu - ludzie i udzialy wchodza do grupy razem
            committed = true;
            if (newG) _groups.Add(g);
            if (newT) g.T.Add(tn);
            tn.N += n;
            g.O.AddAll(sh);
            if (g.O.Total < g.Men - 1e-6) g.O.Add("", g.Men - g.O.Total);
            PeopleBook.RallyShares(sh);
            _dAdded += n;
        }

        /// <summary>Wpis w bitwy.log dla pobitej druzyny lorda z rozbitymi (po calym rosterze rozbitych tej partii); gracz i jego rod - komunikat (recenzja K11).</summary>
        internal static void BattleLine(Mark m, MapEvent me)
        {
            if (m == null || m.Routed <= 0) return;
            try
            {
                if (!ReferenceEquals(_lastMe, me)) { _lastMe = me; _dBattles++; }
                _dParties++; _dRouted += m.Routed; _dBack += m.Back; _dHome += m.Home; _dWoods += m.Woods; _dEB += m.EB; _dEH += m.EH; _dEW += m.EW;
                if (m.Rally == null) _dNoRally += m.Routed;
                if (m.Player) _dPlayerBack += m.Back;
                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(400);
                sb.Append("Bitwa: N7 rozbici dzien ").Append(Today).Append(" - ").Append(me.EventType).Append(Where(me)).Append(": druzyna ").Append(m.Name)
                  .Append(" (rod ").Append(m.Clan != null && m.Clan.Name != null ? m.Clan.Name.ToString() : "?").Append(", ").Append(m.Fac != null && m.Fac.Name != null ? m.Fac.Name.ToString() : "?")
                  .Append(") rozbici ").Append(m.Routed).Append(" -> pod choragiew ").Append(m.Back)
                  .Append(m.Rally != null ? " (zbiorka " + m.Rally.Name + ", gotowi za " + m.Gather + " dob)" : " (bez osady zbiorki - jak dom / las)")
                  .Append(", do domu ").Append(m.Home).Append(", do lasu ").Append(m.Woods)
                  .Append(" | oczekiwane ").Append(m.EB.ToString("0.0", ci)).Append(" / ").Append(m.EH.ToString("0.0", ci)).Append(" / ").Append(m.EW.ToString("0.0", ci))
                  .Append(" | kraj ").Append(m.Own ? "wlasny" : "obcy").Append(", zalegly zold ").Append(m.Weeks).Append(" tyg., Straz ").Append(m.Watch ? "tak" : "nie")
                  .Append(m.Player ? ", rod gracza" : "").Append(me.IsPlayerMapEvent ? ", bitwa gracza" : "").Append('.');
                Log.Info(sb.ToString());
                if (m.Player && m.Back > 0 && m.Rally != null)
                    Log.Player(m.Mp == MobileParty.MainParty ? "Stragglers: " + m.Back + " of your men fled toward " + m.Rally.Name + "."
                                                            : "Stragglers: " + m.Back + " men of " + m.Name + " fled toward " + m.Rally.Name + ".");
            }
            catch (Exception e) { Stumble(ref _stLine, "BattleLine", e); }
        }

        private static string Where(MapEvent me)
        {
            try
            {
                if (me.MapEventSettlement != null) return " pod " + me.MapEventSettlement.Name;
                var r = OutlawLaw.RegionAt(me.Position.ToVec2());
                return r != null ? " w regionie " + r.Name : "";
            }
            catch { return ""; }
        }

        // ================================================================ doba: gotowosc, odbior, rozejscie
        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            var sw = Stopwatch.StartNew();
            int day = Today;
            bool lined = false;
            try
            {
                Resolve();
                bool on = On;
                for (int i = _marks.Count - 1; i >= 0; i--) if (_marks[i].Day < day - 1) { _marks.RemoveAt(i); _dStale++; }   // znaczniki bez konca bitwy (nie powinno byc)
                if (_groups.Count > 0)
                {
                    _groups.Sort((a, b) => a.Ready != b.Ready ? a.Ready.CompareTo(b.Ready) : a.Day.CompareTo(b.Day));
                    var cand = new Dictionary<Clan, List<MobileParty>>();
                    double per = PerDay;
                    double reach = Math.Max(0.0, Settings.Current.StragglersRallyReachDays) * per;
                    int wait = Math.Max(1, Settings.Current.StragglersRallyWaitDays);
                    for (int i = 0; i < _groups.Count; i++)
                    {
                        var g = _groups[i];
                        try
                        {
                            if (g.Men <= 0) continue;
                            if (!on) { _dOff += Disperse(g); continue; }
                            if (g.Clan == null || g.Clan.IsEliminated) { _dNoClan += Disperse(g); continue; }
                            // recenzja K2: osada zbiorki zdobyta przez wroga rodu (albo rod zmienil strone) - nowa osada zbiorki od tej; brak - rozejscie stad
                            if (g.Rally != null && Hostile(g.Rally, g.Clan) && !Move(g, day, per)) { _dLostRally += Disperse(g); continue; }
                            if (day < g.Ready) continue;
                            if (!g.Announced)
                            {
                                g.Announced = true; _dReady += g.Men;
                                if (g.Clan == Clan.PlayerClan && g.Rally != null) Log.Player("Stragglers: " + g.Men + " of your men have gathered at " + g.Rally.Name + ".");
                            }
                            if (day >= g.Ready + wait) { _dExpire += Disperse(g); continue; }
                            // recenzja K2: osada oblezona - nikt nie wychodzi do druzyny tej doby (marsz przerwany - wracaja za mury)
                            if (g.Rally != null && g.Rally.IsUnderSiege) { MarchBack(g); _dWaitSiege += g.Men; continue; }
                            Pickup(g, cand, reach, per, day);
                        }
                        catch (Exception e) { Stumble(ref _stPick, "Daily(grupa)", e); }
                    }
                    _groups.RemoveAll(x => x.Men <= 0);
                }
                _ticks += sw.ElapsedTicks;
                Line(day, on);
                lined = true;
            }
            catch (Exception e) { Stumble(ref _stLine, "Daily", e); }
            finally
            {
                if (!lined) FailLine(day);           // recenzja K1: bez linii doby narzedzie widzi dziure (N7-0) i potkniecia (N7-5)
                ClearDay();
            }
        }

        private static bool Usable(MobileParty mp)
        {
            if (mp == null || !mp.IsActive || mp.Party == null || mp.MemberRoster == null || mp.MapEvent != null || mp.IsDisbanding) return false;
            if (mp.IsCurrentlyAtSea) return false;
            var h = mp.LeaderHero;
            if (h == null || h.IsPrisoner) return false;
            if (Undead.Party(mp)) return false;
            var cs = mp.CurrentSettlement;
            if (cs != null && cs.IsUnderSiege) return false;
            return true;
        }

        private static List<MobileParty> Candidates(Clan c)
        {
            var l = new List<MobileParty>();
            var main = MobileParty.MainParty;
            if (c == Clan.PlayerClan && Usable(main)) l.Add(main);
            var wps = c.WarPartyComponents;
            if (wps != null)
                for (int i = 0; i < wps.Count; i++)
                {
                    var mp = wps[i] != null ? wps[i].MobileParty : null;
                    if (mp == null || mp == main || !mp.IsLordParty || !Usable(mp)) continue;
                    l.Add(mp);
                }
            return l;
        }

        /// <summary>Osada zbiorki zdobyta: grupa idzie do nowej osady zbiorki (FindRally od tej osady; droga piesza 1-10 dob, czekanie liczone od dojscia). false - brak.</summary>
        private static bool Move(G g, int day, double per)
        {
            var old = g.Rally;
            var op = old.GetPosition2D;
            var nr = FindRally(g.Clan, op);
            if (nr == null || ReferenceEquals(nr, old) || Bad(nr.StringId)) return false;
            int men = g.Men;
            int dd = Math.Max(1, Math.Min(GatherMax, (int)Math.Ceiling(op.Distance(nr.GetPosition2D) / per)));
            g.Rally = nr; g.RallyId = nr.StringId; g.Ready = Math.Max(g.Ready, day + dd); g.Arrive = 0; g.Announced = false;
            _dMoved += men;
            if (g.Clan == Clan.PlayerClan) Log.Player("Stragglers: " + old.Name + " has fallen to the enemy - " + men + " of your men are making for " + nr.Name + ".");
            return true;
        }

        /// <summary>Marsz do druzyny przerwany (druzyny w zasiegu juz nie ma, osada oblezona) - grupa wraca do osady zbiorki; nastepny marsz od nowa.</summary>
        private static void MarchBack(G g)
        {
            if (g.Arrive <= 0) return;
            _dMarchBack += g.Men;
            g.Arrive = 0;
        }

        private static void Pickup(G g, Dictionary<Clan, List<MobileParty>> cand, double reach, double per, int day)
        {
            if (g.Rally == null) return;
            if (g.Arrive > day) return;                                   // w marszu (linia: "w marszu do druzyny")
            List<MobileParty> ps;
            if (!cand.TryGetValue(g.Clan, out ps)) { ps = Candidates(g.Clan); cand[g.Clan] = ps; }
            if (ps.Count == 0) { MarchBack(g); _dWaitNoParty += g.Men; return; }
            var rp = g.Rally.GetPosition2D;
            var near = new List<KeyValuePair<float, MobileParty>>();
            float nearest = float.MaxValue;
            foreach (var mp in ps)
            {
                float d = rp.Distance(mp.Position.ToVec2());
                if (d < nearest) nearest = d;
                if (d <= reach) near.Add(new KeyValuePair<float, MobileParty>(d, mp));
            }
            if (near.Count == 0)
            {
                MarchBack(g);
                _dWaitFar += g.Men;
                _dFarDays += nearest / per; _dFarN++;
                return;
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));
            // recenzja K8: odbior nie jest natychmiastowy - grupa idzie do najblizszej druzyny tyle dob, ile wynosi droga od osady zbiorki
            if (g.Arrive == 0)
            {
                int md = (int)Math.Ceiling(near[0].Key / per);
                if (md >= 1) { g.Arrive = day + md; _dMarchStart += g.Men; return; }
                g.Arrive = day;
            }
            bool full = false, wage = false;
            foreach (var kv in near)
            {
                int why;
                TakeInto(g, kv.Value, out why);
                if (why == 1) full = true; else if (why == 2) wage = true;
                if (g.Men <= 0) break;
            }
            if (g.Men > 0) { if (wage && !full) _dWaitWage += g.Men; else _dWaitFull += g.Men; }
        }

        /// <summary>Odbior do druzyny mp: najwyzej miejsce w limicie i w zoldzie (weterani pierwsi); typ po typie: roster, grupa, udzialy (Give FRally) i liczniki
        /// razem (recenzja O7). why: 0 - przyjal albo nic do przyjecia, 1 - brak miejsca, 2 - brak zoldu.</summary>
        private static int TakeInto(G g, MobileParty mp, out int why)
        {
            why = 0;
            int taken = 0;
            try
            {
                int room = mp.Party.PartySizeLimit - mp.MemberRoster.TotalManCount;
                // Straz bez zoldu: pula Strazy w ludziach czytana na zywo (odebrani wczesniej tej doby juz sa w skladzie - recenzja K4/O6, bez drugiego odejmowania)
                int wr = ClanBudget.WatchRoom(g.Clan);
                if (wr >= 0) room = Math.Min(room, wr);
                if (room <= 0) { why = 1; return 0; }
                bool lim = mp.HasLimitedWage();
                int budget = lim ? mp.GetAvailableWageBudget() : int.MaxValue;
                if (lim && budget <= 0) { why = 2; return 0; }
                var wm = Campaign.Current.Models.PartyWageModel;
                g.T.Sort((a, b) => (b.C != null ? b.C.Tier : 0).CompareTo(a.C != null ? a.C.Tier : 0));
                bool wageShort = false, first = true;
                foreach (var tn in g.T)
                {
                    if (tn.N <= 0 || tn.C == null) continue;
                    int k = Math.Min(tn.N, room), w = 0;
                    if (lim) { w = wm.GetCharacterWage(tn.C); if (w > 0) { int kw = budget / w; if (kw < k) { k = kw; wageShort = true; } } }
                    if (k <= 0) continue;
                    mp.MemberRoster.AddToCounts(tn.C, k);
                    tn.N -= k; room -= k; taken += k; _dPicked += k;
                    if (first) { first = false; _dPickParties++; }
                    if (mp == MobileParty.MainParty) _dPickPlayer += k;
                    if (lim && w > 0) budget -= k * w;
                    PeopleBook.Give(mp.MemberRoster, g.O.Cut(k), PeopleBook.FRally);
                    if (room <= 0) break;
                }
                if (taken <= 0) { why = wageShort ? 2 : 1; return 0; }
                if (g.Clan == Clan.PlayerClan)
                {
                    string from = g.Rally != null ? " from " + g.Rally.Name : "";
                    Log.Player(mp == MobileParty.MainParty ? "Stragglers: " + taken + " of your men rejoined you" + from + "."
                                                          : "Stragglers: " + taken + " of your men rejoined " + mp.Name + from + ".");
                }
                if (g.Men > 0) why = room <= 0 ? 1 : (wageShort ? 2 : 0);
                return taken;
            }
            catch (Exception e) { Stumble(ref _stPick, "TakeInto", e); return taken; }
        }

        /// <summary>Udzialy, ktore opuszczaja grupe bez odbioru i bez drogi docelowej - nicosc nazwana (i zejscie z "do zbiorki" netto).</summary>
        private static void LoseShares(PeopleBook.Shares s)
        {
            if (s == null || !(s.Total > 1e-6)) return;
            PeopleBook.RallyOut(s);
            PeopleBook.Lost(s);
        }

        /// <summary>Rozejscie grupy: polowa do domu (karczma E4/10, SendHome, bez domu - las), polowa do lasu (Straz - wszyscy do lasu Polnocy; bez prawa wyrzutkow -
        /// dom). Typ po typie we wlasnym try: kto nie trafil do domu / lasu / karczmy - zgubieni nazwani (PeopleBook.Lost, recenzja O7). Zwraca, ilu odeszlo.</summary>
        private static int Disperse(G g)
        {
            int men = g.Men;
            if (men <= 0) return 0;
            int home = 0, woods = 0, tavern = 0, lost = 0;
            if (g.Rally == null)
            {
                foreach (var tn in g.T) tn.N = 0;
                try { LoseShares(g.O.Cut(g.O.Total)); } catch { }
                _dDispLost += men;
                return men;
            }
            var pos = g.Rally.GetPosition2D;
            Settlement region = null; IFaction fac = null;
            try
            {
                // recenzja K5/O5: Straz "do lasu" - pula najblizszego regionu Polnocy (jak rozbici Strazy w bitwie, E4/9 4.5); reszta - region osady zbiorki
                if (OutlawLaw.On) region = (g.Watch ? WatchLaw.NorthRegionAt(pos) : null) ?? OutlawLaw.PoolRegionAt(pos);
                fac = g.Clan != null ? g.Clan.MapFaction : null;
                LosersFlee.BeginBattle();                  // pamiec osad domowych na grupe (miejsce zbiorki)
            }
            catch (Exception e) { Stumble(ref _stDisp, "Disperse(osada)", e); }
            foreach (var tn in g.T)
            {
                int n = tn.N;
                if (n <= 0) continue;
                PeopleBook.Shares slice = null;
                int done = 0;
                try
                {
                    slice = g.O.Cut(n);
                    PeopleBook.RallyOut(slice);
                    if (tn.C == null) continue;            // finally: zgubieni nazwani
                    int nw = g.Watch ? n : n - MBRandom.RoundRandomized(n * 0.5f);
                    nw = Math.Max(0, Math.Min(n, nw));
                    if (region == null) nw = 0;            // bez prawa wyrzutkow - do domu (jak rozbici dotad)
                    if (nw > 0)
                    {
                        OutlawLaw.RallyWoods(region, tn.C, nw); done += nw; woods += nw;
                        LosersFlee.NoteWoods(region, nw); PeopleBook.PoolShares(region, slice.Cut(nw));
                    }
                    int nh = n - nw;
                    if (nh <= 0) continue;
                    int vt = PeopleBook.VetDivert(null, tn.C, nh, pos, PeopleBook.VH3, slice);
                    if (vt > 0) { done += vt; tavern += vt; LosersFlee.NoteTavern(vt, LosersFlee.SrcRouted); }
                    int rest = nh - vt;
                    if (rest <= 0) continue;
                    int cat;
                    int got = LosersFlee.SendHome(null, tn.C, rest, pos, fac, LosersFlee.SrcRouted, out cat, slice);
                    done += got; home += got;
                    int left = rest - got;
                    if (left <= 0) continue;
                    if (region != null)
                    {
                        OutlawLaw.RallyWoods(region, tn.C, left); done += left; woods += left;
                        LosersFlee.NoteWoods(region, left); LosersFlee.NoteHomeless(left, true);
                        var s2 = slice; slice = null; PeopleBook.PoolShares(region, s2);
                    }
                    else LosersFlee.NoteHomeless(left, false);   // finally: zgubieni nazwani (jak "znikneli" H3 bez prawa wyrzutkow)
                }
                catch (Exception e) { Stumble(ref _stDisp, "Disperse", e); }
                finally
                {
                    tn.N = 0;
                    int miss = n - done;
                    if (miss > 0) lost += miss;
                    if (slice != null && slice.Total > 1e-6) { try { PeopleBook.Lost(slice); } catch { } }    // reszta udzialow typu (zgubieni albo zaokraglenie)
                }
            }
            try
            {
                if (g.O.Total > 1e-6) LoseShares(g.O.Cut(g.O.Total));     // reszty udzialow grupy (nie powinno byc)
                if (g.Clan == Clan.PlayerClan)
                    Log.Player("Stragglers: " + men + " of your men at " + g.Rally.Name + " gave up waiting - " + (home + tavern) + " went home, " + woods + " took to the woods.");
            }
            catch (Exception e) { Stumble(ref _stDisp, "Disperse(koniec)", e); }
            _dDispHome += home + tavern; _dDispWoods += woods; _dDispTavern += tavern; _dDispLost += lost;
            return home + tavern + woods + lost;
        }

        // ================================================================ linia doby
        private static int StumblesToday { get { return _stMark + _stSplit + _stPick + _stDisp + _stSave + _stLine; } }

        private static string StumbleText()
        {
            return " | potkniecia " + StumblesToday + " (znacznik " + _stMark + ", podzial " + _stSplit + ", odbior " + _stPick + ", rozejscie " + _stDisp + ", zapis " + _stSave
                   + ", linia " + _stLine + ")";
        }

        private static void Line(int day, bool on)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1400);
            int w = 0, wz = 0, wg = 0, wm = 0; double sh = 0, shk = 0, shDiff = 0;
            var rallies = new HashSet<string>();
            foreach (var g in _groups)
            {
                int m = g.Men; if (m <= 0) continue;
                w += m; if (day < g.Ready) wz += m; else { wg += m; if (g.Arrive > day) wm += m; }
                rallies.Add(g.RallyId ?? "?");
                sh += g.O.Total; shk += g.O.Known; shDiff += Math.Abs(g.O.Total - m);
            }
            int disp = _dDispHome + _dDispWoods + _dDispLost;
            int ctl = _lastW + _dAdded - _dPicked - disp - _dLoadLost - w;
            sb.Append("Rozbici wracaja (N7): dzien ").Append(day);
            if (!on) sb.Append(" - WYLACZONE (Stragglers Rally; rozbici jak dotad - dom albo las; czekajacy z zapisu rozchodza sie)");
            sb.Append(" - rozbici druzyn lordow ").Append(_dRouted).Append(" (druzyn ").Append(_dParties).Append(", bitew ").Append(_dBattles).Append("): pod choragiew ").Append(_dBack)
              .Append(" / do domu ").Append(_dHome).Append(" / do lasu ").Append(_dWoods).Append("; nowi do zbiorki ").Append(_dAdded).Append(", bez osady zbiorki ").Append(_dNoRally)
              .Append(", rod gracza pod choragiew ").Append(_dPlayerBack);
            double er = _dEB + _dEH + _dEW;
            sb.Append(" | oczekiwany podzial ").Append(_dEB.ToString("0.0", ci)).Append(" / ").Append(_dEH.ToString("0.0", ci)).Append(" / ").Append(_dEW.ToString("0.0", ci))
              .Append(" (").Append(er > 0 ? (100.0 * _dEB / er).ToString("0.0", ci) + "% / " + (100.0 * _dEH / er).ToString("0.0", ci) + "% / " + (100.0 * _dEW / er).ToString("0.0", ci) + "%" : "-")
              .Append("; regula: weterani t4+ 70, t3 55, rekruci 45, reszta dom : las 2 : 1; kraj wlasny -5/+10/-5, obcy +5/-10/+5; zalegly zold -10 pkt/tydz., min. 20; Straz 80/0/20)");
            sb.Append(" | czekaja ").Append(w).Append(" ludzi w ").Append(_groups.Count).Append(" grupach w ").Append(rallies.Count).Append(" osadach (zbieraja sie ").Append(wz)
              .Append(", gotowi ").Append(wg).Append(", w marszu do druzyny ").Append(wm).Append("; gotowi bez druzyny rodu ").Append(_dWaitNoParty).Append(", druzyna dalej niz zasieg ").Append(_dWaitFar)
              .Append(_dFarN > 0 ? " (srednio " + (_dFarDays / _dFarN).ToString("0.0", ci) + " dob marszu)" : "").Append(", druzyny bez miejsca ").Append(_dWaitFull)
              .Append(", bez zoldu ").Append(_dWaitWage).Append(", osada oblezona ").Append(_dWaitSiege).Append(')');
            sb.Append(" | gotowi dzis ").Append(_dReady).Append(", wyruszyli do druzyny ").Append(_dMarchStart).Append(", odebrani ").Append(_dPicked).Append(" (druzyn ").Append(_dPickParties)
              .Append(", gracz ").Append(_dPickPlayer).Append(')')
              .Append(", rozeszli sie ").Append(disp).Append(" (dom ").Append(_dDispHome).Append(" - w tym karczma ").Append(_dDispTavern).Append(" / las ").Append(_dDispWoods)
              .Append(" / zgubieni ").Append(_dDispLost).Append("; po czasie czekania ").Append(_dExpire).Append(", wylaczone ").Append(_dOff).Append(", rod upadl ").Append(_dNoClan)
              .Append(", osada zdobyta bez nowej ").Append(_dLostRally).Append(')')
              .Append(", przeniesieni (osada zdobyta) ").Append(_dMoved).Append(", wrocili z marszu (druzyna odeszla) ").Append(_dMarchBack)
              .Append(", zgubieni przy wczytaniu ").Append(_dLoadLost);
            sb.Append(" | kontrola: czekali wczoraj ").Append(_lastW).Append(" + nowi ").Append(_dAdded).Append(" - odebrani ").Append(_dPicked).Append(" - rozeszli sie ").Append(disp)
              .Append(" - zgubieni przy wczytaniu ").Append(_dLoadLost).Append(" - czekaja ").Append(w).Append(" = ").Append(ctl);
            sb.Append(" | ksiega pochodzenia: udzialy czekajacych ").Append(sh.ToString("0.0", ci)).Append(" (z pochodzeniem ").Append(shk.ToString("0.0", ci))
              .Append("), roznica udzialy - ludzie ").Append(shDiff.ToString("0.00", ci));
            int len = -1;
            try { _badIds = ExportInto(_xsb); len = _xsb.Length; } catch (Exception e) { Stumble(ref _stSave, "Export(linia)", e); }
            sb.Append(" | zapis: znakow ").Append(len).Append(_importN >= 0 ? ", wczytano grup " + _importN + " (zlych " + _importBad + ")" : "")
              .Append(_badIds > 0 ? ", pominiete (znaki w id) " + _badIds : "").Append(_dBadMark > 0 ? ", druzyn bez zbiorki (znaki w id) " + _dBadMark : "");
            sb.Append(StumbleText())
              .Append(_dStale > 0 ? " | znaczniki bez konca bitwy " + _dStale : "")
              .Append(" | zasieg ").Append(Settings.Current.StragglersRallyReachDays.ToString("0.0", ci)).Append(" dob marszu, czekanie ").Append(Settings.Current.StragglersRallyWaitDays).Append(" dob")
              .Append(" | czas ").Append((_ticks * 1000.0 / Stopwatch.Frequency).ToString("0.00", ci)).Append(" ms.");
            _importN = -1;
            _lastW = w;
            Log.Info(sb.ToString());
        }

        /// <summary>Linia doby, gdy Daily albo Line rzucily (recenzja K1): czekajacy policzeni od nowa (kontrola nastepnej doby od tego stanu) i potkniecia doby.</summary>
        private static void FailLine(int day)
        {
            try
            {
                int w = 0;
                foreach (var g in _groups) { try { w += g.Men; } catch { } }
                _lastW = w;
                Log.Info("Rozbici wracaja (N7): dzien " + day + " - BLAD LINII (wyjatek - log bledow StragglersRally) | czekaja " + w + " ludzi" + StumbleText() + ".");
            }
            catch { }
        }

        /// <summary>Udzialy pochodzenia czekajacych wedlug regionu (bez "") - konto Rally rownania regionu ksiegi pochodzenia (PeopleBook.RegionNow).</summary>
        internal static void KnownByRegion(Dictionary<string, double> into)
        {
            if (into == null) return;
            foreach (var g in _groups)
                foreach (var kv in g.O.M)
                {
                    if (kv.Key.Length == 0 || !(kv.Value > 0.0)) continue;
                    double v; into.TryGetValue(kv.Key, out v); into[kv.Key] = v + kv.Value;
                }
        }

        // ================================================================ zapis (SaveText.Sync, klucz arm_n7_rally)
        private static bool Bad(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf(':') >= 0; }

        /// <summary>"1|klan,osada,doba,gotowi,flagi,marsz;typ:n,typ:n;region:ludzie,~:ludzie;W|..." (flagi: w - Straz, a - ogloszona; marsz - doba dojscia do druzyny,
        /// 0 - w osadzie). Pusta pula - "".</summary>
        internal static string Export()
        {
            _badIds = ExportInto(_xsb);
            return _xsb.ToString();
        }

        /// <summary>Zapis do bufora sb (czyszczony); zwraca liczbe grup pominietych przez znak separatora w id.</summary>
        private static int ExportInto(StringBuilder sb)
        {
            sb.Length = 0;
            if (_groups.Count == 0) return 0;
            var ci = CultureInfo.InvariantCulture;
            sb.Append(Ver);
            int bad = 0;
            foreach (var g in _groups)
            {
                if (g.Men <= 0) continue;
                if (Bad(g.ClanId) || Bad(g.RallyId)) { bad++; continue; }
                sb.Append('|').Append(g.ClanId).Append(',').Append(g.RallyId).Append(',').Append(g.Day).Append(',').Append(g.Ready).Append(',')
                  .Append(g.Watch ? "w" : "").Append(g.Announced ? "a" : "").Append(',').Append(g.Arrive).Append(';');
                bool f = true;
                foreach (var tn in g.T)
                {
                    if (tn.N <= 0 || Bad(tn.Id)) continue;
                    sb.Append(f ? "" : ",").Append(tn.Id).Append(':').Append(tn.N); f = false;
                }
                sb.Append(';');
                f = true;
                foreach (var kv in g.O.M)
                {
                    if (!(kv.Value > 1e-6)) continue;
                    string k = kv.Key.Length == 0 ? "~" : kv.Key;
                    if (k != "~" && Bad(k)) continue;
                    sb.Append(f ? "" : ",").Append(k).Append(':').Append(kv.Value.ToString("0.###", ci)); f = false;
                }
                sb.Append(';').Append(g.O.W.ToString("0.###", ci));
            }
            return bad;
        }

        internal static void Import(string s)
        {
            _groups.Clear(); _needResolve = false; _lastW = 0; _importN = 0; _importBad = 0;
            if (string.IsNullOrEmpty(s)) return;
            var ci = CultureInfo.InvariantCulture;
            var parts = s.Split('|');
            if (parts.Length == 0 || parts[0] != Ver) { _importBad = parts.Length; Log.Info("Rozbici wracaja (N7): nieznana wersja zapisu (" + (parts.Length > 0 ? parts[0] : "-") + ") - pula pusta."); return; }
            for (int i = 1; i < parts.Length; i++)
            {
                try
                {
                    var f = parts[i].Split(';');
                    var h = f[0].Split(',');
                    var g = new G { ClanId = h[0], RallyId = h[1], Day = int.Parse(h[2], ci), Ready = int.Parse(h[3], ci) };
                    string fl = h.Length > 4 ? h[4] : "";
                    g.Watch = fl.IndexOf('w') >= 0; g.Announced = fl.IndexOf('a') >= 0;
                    if (h.Length > 5 && h[5].Length > 0) g.Arrive = Math.Max(0, int.Parse(h[5], ci));
                    if (f.Length > 1 && f[1].Length > 0)
                        foreach (var x in f[1].Split(','))
                        {
                            var kv = x.Split(':');
                            int n = int.Parse(kv[1], ci);
                            if (n > 0) g.T.Add(new TN { Id = kv[0], N = n });
                        }
                    if (f.Length > 2 && f[2].Length > 0)
                        foreach (var x in f[2].Split(','))
                        {
                            var kv = x.Split(':');
                            double v = double.Parse(kv[1], NumberStyles.Float, ci);
                            g.O.Add(kv[0] == "~" ? "" : kv[0], v);
                        }
                    if (f.Length > 3 && f[3].Length > 0) g.O.W = Math.Min(g.O.Total, double.Parse(f[3], NumberStyles.Float, ci));
                    int men = g.Men;
                    if (men <= 0) continue;
                    if (g.O.Total < men - 1e-6) g.O.Add("", men - g.O.Total);
                    else if (g.O.Total > men + 1e-6) g.O.Cut(g.O.Total - men);
                    _groups.Add(g);
                    _lastW += men;
                    _importN++;
                }
                catch { _importBad++; }
            }
            _needResolve = _groups.Count > 0;
        }

        /// <summary>Po wczytaniu (pierwsza doba): rod, osada i typy wedlug id. Typ nieznany - zgubieni nazwani (udzialy do nicosci nazwanej); rod nieznany -
        /// grupa rozchodzi sie tej doby; osada nieznana - zgubieni.</summary>
        private static void Resolve()
        {
            if (!_needResolve) return;
            _needResolve = false;
            var clans = new Dictionary<string, Clan>();
            foreach (var c in Clan.All) if (c != null && c.StringId != null) clans[c.StringId] = c;
            foreach (var g in _groups)
            {
                try
                {
                    Clan c; clans.TryGetValue(g.ClanId ?? "", out c); g.Clan = c;
                    try { g.Rally = MBObjectManager.Instance.GetObject<Settlement>(g.RallyId); } catch { g.Rally = null; }
                    foreach (var tn in g.T)
                    {
                        if (tn.N <= 0) continue;
                        tn.C = LosersFlee.TroopOf(tn.Id);
                        if (tn.C == null || tn.C.IsHero) { _dLoadLost += tn.N; LoseShares(g.O.Cut(tn.N)); tn.N = 0; }
                    }
                    if (g.Rally == null && g.Men > 0) { int m = g.Men; _dLoadLost += m; LoseShares(g.O.Cut(m)); foreach (var tn in g.T) tn.N = 0; }
                }
                catch (Exception e) { Stumble(ref _stSave, "Resolve", e); }
            }
            _groups.RemoveAll(x => x.Men <= 0);
        }
    }
}
