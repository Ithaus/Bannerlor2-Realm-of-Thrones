using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// N5-E (poprawka 5 po biegu rocznym 364 doby, decyzja Jeffa 13.10; RAPORT-ROCZNY-2026-10-13 niespojnosc 11) - BUDOWLE CYWILNE Z DALA OD FRONTU.
    /// Dotad BuildFunding wstrzymywal kazda budowle cywilna, gdy krolestwo pana bylo w wojnie z jakimkolwiek krolestwem (flaga wojny). W swiecie Realm of
    /// Thrones wojna fabularna nie konczy sie w pierwszym roku (25-26 z 29 krolestw w wojnie od doby 23), wiec budowy cywilne staly wszedzie i zawsze:
    /// doba 364 - idzie 29 z 227 osad, 189 wstrzymanych wojna, 144 stoja co najmniej 300 dni.
    /// Regula: o budowie cywilnej w wojnie decyduje PRAWDZIWE zagrozenie warowni, nie flaga krolestwa. Warownia (miasto, zamek) jest "przy froncie", gdy
    /// w ostatnich CivilWorksCalmDays dobach:
    ///  (0) ona sama albo warownia jej krolestwa w zasiegu byla oblezona,
    ///  (1) ktoras z jej wsi byla najezdzana, przymuszana albo lezala spalona (stan wsi inny niz Normal),
    ///  (2) druzyna lorda albo armia krolestwa, z ktorym jej krolestwo jest w wojnie, stala w zasiegu CivilWorksFrontRange (skan raz na dobe),
    ///  (3) w zasiegu byla bitwa lordow, rabunek, wymuszenie albo szturm miedzy krolestwami w wojnie, w ktorym jej krolestwo bralo udzial albo jest w wojnie
    ///      z jedna ze stron (zdarzenia gry o kazdej porze doby - ta sama klasyfikacja co styk krolestw w rentach korony 180, tu z miejscem zdarzenia).
    /// Poza frontem budowla cywilna idzie jak w pokoju: pan placi z tego samego przydzialu budow (ClanBudget.BuildShare) i z wlasnej kiesy, materialy z targu.
    /// Dwa bezpieczniki wojska (BuildFunding): rod z budowla wojskowa w toku cywilnej nie zaczyna (mury pierwsze - dzialka murow bez zmian), a pan, ktorego
    /// zold dochodzi do 95% pulapu (ClanBudget.WageRoom), cywilnej nie oplaca (CivilWorksNeedWageRoom) - zloto zostaje na wojsko.
    /// Stan: tylko pamiec sesji (ostatnia doba zagrozenia na warownie) - bez pol zapisu. Po wczytaniu pamiec zdarzen jest pusta; skan (0)-(2) dziala od
    /// pierwszej doby (oblezenia, wsie spalone - stan trwa wiele dob - i wrogie druzyny w zasiegu), wiec warownia przy czynnym froncie nie rusza z budowa.
    /// Koszt: raz na dobe wsie 227 warowni i druzyny lordow x 227 odleglosci; zdarzenie - 227 odleglosci. Wylaczony klucz - nic nie liczymy.
    /// </summary>
    internal static class WarFront
    {
        private const int NSrc = 4;
        private static readonly string[] SrcName = { "oblezenie", "wies najezdzana albo spalona", "wroga druzyna", "bitwa, rabunek albo szturm" };
        private static readonly Dictionary<Settlement, int> _last = new Dictionary<Settlement, int>();   // warownia -> ostatnia doba zagrozenia (pamiec sesji)
        private static readonly List<Settlement> _forts = new List<Settlement>();
        private static readonly List<Vec2> _pos = new List<Vec2>();
        private static readonly HashSet<Settlement>[] _srcDay = { new HashSet<Settlement>(), new HashSet<Settlement>(), new HashSet<Settlement>(), new HashSet<Settlement>() };
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _built;
        private static int _evtDay, _stumbles, _firstDay = -1;
        // liczniki doby z BuildFunding.Daily (budowle cywilne w krolestwie w wojnie)
        private static int _civil, _go, _heldFront, _heldPurse, _heldWalls;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CivilWorksAwayFromFront; } }
        private static float Range { get { var s = Settings.Current; return s != null ? MBMath.ClampFloat(s.CivilWorksFrontRange, 10f, 300f) : 60f; } }
        private static int CalmDays { get { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(60, s.CivilWorksCalmDays)) : 14; } }

        internal static void Reset()
        {
            _last.Clear(); _forts.Clear(); _pos.Clear(); _built = false; _err.Clear();
            for (int i = 0; i < NSrc; i++) _srcDay[i].Clear();
            _evtDay = 0; _stumbles = 0; _firstDay = -1;
            _civil = _go = _heldFront = _heldPurse = _heldWalls = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("WarFront." + where, e); } catch { }
        }

        private static void Build()
        {
            _forts.Clear(); _pos.Clear();
            foreach (var st in Settlement.All)
            {
                if (st == null || st.Town == null || (!st.IsTown && !st.IsCastle)) continue;
                _forts.Add(st); _pos.Add(st.GetPosition2D);
            }
            _built = true;
        }

        private static void Mark(Settlement st, int day, int src)
        {
            int d;
            if (!_last.TryGetValue(st, out d) || d < day) _last[st] = day;
            _srcDay[src].Add(st);
        }

        // ------------------------------------------------------------ zdarzenia gry (bitwy, rabunki, szturmy) - o kazdej porze doby
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.MapEventStarted.AddNonSerializedListener(owner, (me, a, d) => OnMapEvent(me));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(owner, OnMapEvent);
        }

        private static Kingdom KingdomOf(MapEventSide side)
        {
            try { return side != null ? side.MapFaction as Kingdom : null; } catch { return null; }   // strona bez wodza - bez frakcji
        }

        private static bool HasLord(MapEventSide side)
        {
            var ps = side != null ? side.Parties : null;
            if (ps == null) return false;
            for (int i = 0; i < ps.Count; i++)
            {
                var mp = ps[i] != null && ps[i].Party != null ? ps[i].Party.MobileParty : null;
                if (mp != null && (mp.IsLordParty || mp.IsMainParty)) return true;
            }
            return false;
        }

        private static void OnMapEvent(MapEvent me)
        {
            try
            {
                if (me == null || !On || Campaign.Current == null) return;
                Kingdom ka = KingdomOf(me.AttackerSide), kd = KingdomOf(me.DefenderSide);
                if (ka == null || kd == null || ka == kd || !ka.IsAtWarWith(kd)) return;   // bandyci, Inni, piraci, karawany bez krolestwa - nie front
                bool land = me.IsRaid || me.IsForcingVolunteers || me.IsForcingSupplies || me.IsSiegeAssault || me.IsSiegeOutside || me.IsSallyOut
                            || me.IsBlockade || me.IsBlockadeSallyOut;
                if (!land && !(HasLord(me.AttackerSide) && HasLord(me.DefenderSide))) return;   // potyczka z karawana albo chlopami - nie front
                if (!_built) Build();
                int day = (int)CampaignTime.Now.ToDays;
                var p = me.Position.ToVec2();
                float r2 = Range * Range;
                _evtDay++;
                for (int i = 0; i < _forts.Count; i++)
                {
                    if (_pos[i].DistanceSquared(p) > r2) continue;
                    var f = _forts[i].MapFaction;
                    if (f == null) continue;
                    if (f == ka || f == kd || f.IsAtWarWith(ka) || f.IsAtWarWith(kd)) Mark(_forts[i], day, 3);
                }
            }
            catch (Exception e) { Stumble("OnMapEvent", e); }
        }

        // ------------------------------------------------------------ doba: skan (przed BuildFunding.Daily)
        /// <summary>Raz na dobe, przed BuildFunding.Daily: oblezenia, wsie warowni, wrogie druzyny w zasiegu. Zeruje liczniki doby BuildFunding.</summary>
        internal static void Daily()
        {
            _civil = _go = _heldFront = _heldPurse = _heldWalls = 0;
            if (!On || Campaign.Current == null) { if (_last.Count > 0) _last.Clear(); return; }
            int day = (int)CampaignTime.Now.ToDays;
            if (_firstDay < 0) _firstDay = day;
            try
            {
                if (!_built) Build();
                float r2 = Range * Range;
                for (int i = 0; i < _forts.Count; i++)
                {
                    var st = _forts[i];
                    try
                    {
                        // (0) oblezenie: warownia i warownie jej krolestwa w zasiegu
                        if (st.IsUnderSiege)
                        {
                            Mark(st, day, 0);
                            var f = st.MapFaction;
                            if (f != null)
                                for (int j = 0; j < _forts.Count; j++)
                                    if (j != i && _forts[j].MapFaction == f && _pos[j].DistanceSquared(_pos[i]) <= r2) Mark(_forts[j], day, 0);
                        }
                        // (1) wsie warowni: najezdzana, przymuszana albo spalona
                        var vs = st.BoundVillages;
                        if (vs != null)
                            for (int k = 0; k < vs.Count; k++)
                                if (vs[k] != null && vs[k].VillageState != Village.VillageStates.Normal) { Mark(st, day, 1); break; }
                    }
                    catch (Exception e) { Stumble("Daily(warownia)", e); }
                }
                // (2) druzyna lorda albo armia wroga w zasiegu (partie w polu i na morzu; w osadzie - nie)
                foreach (var mp in MobileParty.AllLordParties)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.CurrentSettlement != null || mp.MemberRoster == null || mp.MemberRoster.TotalManCount <= 0) continue;
                        var pf = mp.MapFaction;
                        if (pf == null || !pf.IsKingdomFaction) continue;
                        var p = mp.GetPosition2D;
                        for (int i = 0; i < _forts.Count; i++)
                        {
                            if (_pos[i].DistanceSquared(p) > r2) continue;
                            var f = _forts[i].MapFaction;
                            if (f != null && f != pf && f.IsAtWarWith(pf)) Mark(_forts[i], day, 2);
                        }
                    }
                    catch (Exception e) { Stumble("Daily(druzyna)", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        /// <summary>Warownia poza frontem: bez zagrozenia od CivilWorksCalmDays dob (pamiec sesji; brak wpisu - spokoj).</summary>
        private static bool Calm(Settlement st, int day)
        {
            int d;
            return !_last.TryGetValue(st, out d) || day - d > CalmDays;
        }

        /// <summary>
        /// BuildFunding.Daily: czy budowla cywilna tej warowni moze isc mimo wojny krolestwa. false - klucz wylaczony (stan sprzed paczki), warownia przy
        /// froncie albo pan bez luzu w pulapie zoldu (CivilWorksNeedWageRoom; gracz decyduje sam - bez tego warunku). Liczy osady doby do linii.
        /// </summary>
        internal static bool CivilAllowed(Settlement st)
        {
            if (!On || st == null) return false;
            _civil++;
            try
            {
                if (!Calm(st, (int)CampaignTime.Now.ToDays)) { _heldFront++; return false; }
                var s = Settings.Current;
                var clan = st.OwnerClan;
                if (s.CivilWorksNeedWageRoom && clan != Clan.PlayerClan && !ClanBudget.WageRoom(clan)) { _heldPurse++; return false; }
                return true;
            }
            catch (Exception e) { Stumble("CivilAllowed", e); _heldFront++; return false; }
        }

        /// <summary>BuildFunding.Daily: budowla cywilna dopuszczona, ale rod ma budowe wojskowa w toku - mury pierwsze (dzialka murow bez zmian).</summary>
        internal static void NoteWallsFirst() { _heldWalls++; }
        /// <summary>BuildFunding.Daily: budowla cywilna w wojnie weszla do rozliczenia doby.</summary>
        internal static void NoteGo() { _go++; }

        /// <summary>Dopisek do linii "Budowy oplacone": stan klucza, osady z budowla cywilna w wojnie (dopuszczone / wstrzymane wedlug przyczyny), warownie przy froncie.</summary>
        internal static string LineTail()
        {
            try
            {
                if (!On) return " | N5-E budowle cywilne z dala od frontu: WYLACZONE (Civil Works Away From Front) - w wojnie tylko budowle wojskowe, jak dotad.";
                int day = (int)CampaignTime.Now.ToDays, front = 0;
                for (int i = 0; i < _forts.Count; i++) if (!Calm(_forts[i], day)) front++;
                var inv = CultureInfo.InvariantCulture;
                var s = Settings.Current;
                var sb = new StringBuilder();
                sb.Append(" | N5-E budowle cywilne z dala od frontu: CZYNNE - osad z budowla cywilna w krolestwie w wojnie ").Append(_civil)
                  .Append(": dopuszczone ").Append(_go).Append(", wstrzymane przy froncie ").Append(_heldFront)
                  .Append(", pan bez luzu zoldu ").Append(s.CivilWorksNeedWageRoom ? _heldPurse.ToString(inv) : "- (Civil Works Need Wage Room wylaczone)")
                  .Append(", rod buduje mury ").Append(_heldWalls)
                  .Append("; warownie przy froncie ").Append(front).Append(" z ").Append(_forts.Count).Append(" (zagrozenie w ostatnich ").Append(CalmDays)
                  .Append(" dobach, zasieg ").Append(Range.ToString("0", inv)).Append("); dzis zagrozone wedlug zrodla: ");
                for (int i = 0; i < NSrc; i++) sb.Append(i > 0 ? ", " : "").Append(SrcName[i]).Append(' ').Append(_srcDay[i].Count);
                sb.Append("; zdarzen wojny od wczoraj ").Append(_evtDay).Append("; pamiec sesji od doby ").Append(_firstDay).Append(" (").Append(Math.Max(0, day - _firstDay))
                  .Append(" dob); potkniecia ").Append(_stumbles).Append('.');
                return sb.ToString();
            }
            catch (Exception e) { Stumble("LineTail", e); return " | N5-E budowle cywilne z dala od frontu: blad linii."; }
            finally { for (int i = 0; i < NSrc; i++) _srcDay[i].Clear(); _evtDay = 0; }
        }
    }
}
