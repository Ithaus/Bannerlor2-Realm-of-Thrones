using System;
using System.Diagnostics;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 316e/331 - DIAGNOSTYKA wjazdu do osad gry (wies, miasto, zamek; tylko log, zero zmian zachowania). Jeff 01.11 ~21:35 (wsie: "klikam w wioske glowna - nie moge
    /// wejsc") i 05.11 ~19:27 (Riverrun: "druzyna podjezdza pod miasto i staje, ale menu miasta sie nie otwiera; jak sie postaram, to w koncu wejde").
    /// 331 - BLAD 316e: Tick wisial na CampaignEvents.TickEvent, a gra wola go tylko przy _dt > 0 (Campaign.Tick, dekompilat 1.4.8 Campaign.cs:956-958). W menu osady
    /// czas stoi, wiec 316e nie widzial wjazdu ani wyjazdu - po wyjsciu z wioski widzial tylko nowy rozkaz (GoToPoint przy bramie) i pisal "cel porzucony bez
    /// wjazdu | odleglosc od bramy 0.01". Log 04/05.11: kazda taka linia ma w rgl_log gry "Player has entered castle_village_EN7_x" (wjazd byl). Teraz takt idzie
    /// z SubModuleMain.OnApplicationTick (4 razy na sekunde zegara, tylko na mapie kampanii - MapState, takze w menu i przy zatrzymanym czasie).
    /// Linie "Wjazd do osady gry: ...":
    ///  - proba: cel ruchu gracza (TargetSettlement) = wies / miasto / zamek gry - odleglosc od bramy, menu, ruch, godzina gry;
    ///  - spotkanie z osada (PlayerEncounter.EncounterSettlement) i wjazd (CurrentSettlement) z id menu, zmiany menu w srodku (najwyzej MenuMax na pobyt), wyjazd;
    ///  - STOI U OSADY: cel = osada gry, partia stoi do MaxGateD od bramy bez spotkania i wjazdu przez StuckTicks sprawdzen (ok. 2 s zegara) - stan: okno, rozmowa,
    ///    ruch (dlugi/krotki), krotki cel, port/morze/przejscie, czeka (IsMainPartyWaiting - gra nie przesuwa czasu, gdy partia stoi u celu), tryb czasu,
    ///    promien spotkania gry (EncounterModel) - to sa warunki EncounterManager.HandleEncounterForMobileParty i PartyBase.CanPartyInteract;
    ///  - cel porzucony: osada przestala byc celem bez wjazdu (nowy cel / postoj); menu "arm_*" = kolumne zatrzymal nasz oboz (cwiczenia 311e, sen, oboz o zmierzchu);
    ///  - popup: kazde nasze okno przy postoju na mapie (pytanie zajazdu, wioski gracza, Night Falls) - tytul, cel ruchu, najblizsza osada gry.
    /// Kilka odczytow pol jednej partii co takt. Potkniecia liczone, nic nie gasimy.
    /// </summary>
    internal static class VillageEntryDiag
    {
        private const string P = "Wjazd do osady gry: ";
        private const float MaxGateD = 1.5f;
        private const int StuckTicks = 8;
        private const int MenuMax = 6;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static long _last;
        private static Campaign _camp;
        private static Settlement _aim, _enc, _in;
        private static string _inMenu;
        private static int _inMenus, _stuck, _stumbles;
        private static bool _stuckLogged;
        private static Vec2 _lastPos;

        /// <summary>Start sesji kampanii - stan od nowa (tylko pamiec, nic w zapisie). Takt dziala tylko dla kampanii z ostatniego Reset.</summary>
        internal static void Reset()
        {
            _aim = _enc = _in = null; _inMenu = null; _inMenus = 0; _stuck = 0; _stuckLogged = false;
            _camp = Campaign.Current;
        }

        /// <summary>SubModuleMain.OnApplicationTick - co klatke, sam ogranicza sie do 4 razy na sekunde; tylko mapa kampanii (MapState, takze menu osady i czas stoi).</summary>
        internal static void AppTick()
        {
            long t = Stopwatch.GetTimestamp();
            if (t - _last < Stopwatch.Frequency / 4) return;
            _last = t;
            try
            {
                var camp = Campaign.Current;
                if (camp == null || !ReferenceEquals(camp, _camp)) return;
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm == null || !(gsm.ActiveState is MapState)) return;   // misja (scena miasta), ekrany, wczytywanie - bez taktu
                Tick();
            }
            catch (Exception e) { if (++_stumbles <= 3 || _stumbles % 500 == 0) Log.Error("VillageEntryDiag.AppTick (potkniecie " + _stumbles + ")", e); }
        }

        private static bool Game3(Settlement st) { return st != null && (st.IsVillage || st.IsTown || st.IsCastle); }

        private static void Tick()
        {
            var mp = MobileParty.MainParty;
            if (mp == null) return;
            string menu = MenuId();
            var cur = mp.CurrentSettlement;
            // w osadzie gry: wjazd i zmiany menu
            if (Game3(cur))
            {
                if (!ReferenceEquals(cur, _in)) { _in = cur; _inMenu = menu; _inMenus = 0; _aim = null; _enc = null; Line(cur, "wjazd | menu " + menu, mp); }
                else if (menu != _inMenu && _inMenus < MenuMax) { _inMenu = menu; _inMenus++; Line(cur, "menu " + menu + (_inMenus == MenuMax ? " (dalsze zmiany menu bez linii)" : ""), mp); }
                return;
            }
            if (_in != null) { Line(_in, "wyjazd | menu " + menu + " | ruch " + Move(mp), mp); _in = null; _inMenu = null; _aim = null; _enc = null; }
            // spotkanie z osada bez wjazdu (menu spotkania, osada wroga itp.)
            Settlement enc = null;
            try { enc = PlayerEncounter.Current != null ? PlayerEncounter.EncounterSettlement : null; } catch { enc = null; }
            if (Game3(enc) && !ReferenceEquals(enc, _enc)) { _enc = enc; Line(enc, "spotkanie (bez wjazdu) | menu " + menu, mp); }
            if (enc == null) _enc = null;
            if (cur != null) { _aim = null; return; }   // inna osada (kryjowka itp.)
            // cel ruchu: osada gry
            var tgt = mp.TargetSettlement;
            if (Game3(tgt) && !ReferenceEquals(tgt, _aim))
            {
                _aim = tgt; _stuck = 0; _stuckLogged = false; _lastPos = mp.GetPosition2D;
                Line(tgt, "proba (cel ruchu) | odleglosc od bramy " + GateD(mp, tgt).ToString("0.00", Inv) + " | menu " + menu + " | ruch " + Move(mp), mp);
                return;
            }
            if (_aim == null) return;
            if (!ReferenceEquals(tgt, _aim))
            {
                if (enc == null)
                    Line(_aim, "cel porzucony bez wjazdu | nowy cel " + (tgt != null ? tgt.Name.ToString() : "-") + " | odleglosc od bramy " + GateD(mp, _aim).ToString("0.00", Inv)
                               + " | menu " + menu + (menu != null && menu.StartsWith("arm_", StringComparison.Ordinal) ? " (kolumne zatrzymal oboz Armoury)" : "")
                               + " | ruch " + Move(mp), mp);
                _aim = null;
                return;
            }
            // przy bramie, stoi, bez spotkania i wjazdu
            var pos = mp.GetPosition2D;
            bool still = pos.Distance(_lastPos) < 0.01f;
            _lastPos = pos;
            if (enc == null && still && GateD(mp, _aim) <= MaxGateD)
            {
                if (++_stuck >= StuckTicks && !_stuckLogged)
                {
                    _stuckLogged = true;
                    var stt = mp.ShortTermTargetSettlement;
                    Line(_aim, "STOI U OSADY bez spotkania i wjazdu | odleglosc od bramy " + GateD(mp, _aim).ToString("0.00", Inv) + " (promien spotkania gry " + Need(_aim) + ")"
                               + " | okno " + YN(SafeInquiry()) + " | rozmowa " + YN(ConversationOn()) + " | menu " + menu + " | ruch " + Move(mp)
                               + " | krotki cel " + (stt != null ? stt.StringId : "-") + " | port " + YN(mp.IsTargetingPort) + ", morze " + YN(mp.IsCurrentlyAtSea)
                               + ", przejscie " + YN(mp.IsTransitionInProgress) + " | czeka " + YN(Campaign.Current.IsMainPartyWaiting) + " | widoczna " + YN(mp.IsVisible)
                               + " | armia " + (mp.Army != null ? (mp.Army.LeaderParty == mp ? "wodz" : "czlonek") : "-") + " | bitwa " + YN(mp.MapEvent != null)
                               + " | czas " + Campaign.Current.TimeControlMode, mp);
                }
            }
            else _stuck = 0;
        }

        /// <summary>Nasze okno przy postoju na mapie (zajazd, wioska gracza, Night Falls) - linia z tytulem, celem ruchu i najblizsza osada gry. Nie rzuca.</summary>
        internal static void Popup(string title)
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || mp == null) return;
                Settlement near = null;
                float nd = float.MaxValue;
                var pos = mp.GetPosition2D;
                foreach (var st in Settlement.All)
                {
                    if (!Game3(st)) continue;
                    float d = pos.Distance(st.GatePosition.ToVec2());
                    if (d < nd) { nd = d; near = st; }
                }
                var tgt = mp.TargetSettlement;
                Log.Info(P + (Game3(tgt) ? tgt.Name.ToString() : "-") + " | popup '" + title + "' | cel ruchu " + (tgt != null ? tgt.Name + " (" + tgt.StringId + ")" : "-")
                         + " | najblizsza osada gry " + (near != null ? near.Name + " (" + near.StringId + ", " + Kind(near) + ") " + nd.ToString("0.00", Inv) + " jedn. od bramy" : "-")
                         + " | menu " + MenuId() + " | ruch " + Move(mp) + " | godzina " + Hour() + ".");
            }
            catch (Exception e) { if (++_stumbles <= 3 || _stumbles % 500 == 0) Log.Error("VillageEntryDiag.Popup (potkniecie " + _stumbles + ")", e); }
        }

        private static string Kind(Settlement st) { return st.IsTown ? "miasto" : st.IsCastle ? "zamek" : st.IsVillage ? "wies" : "inna"; }

        private static void Line(Settlement st, string what, MobileParty mp)
        {
            Log.Info(P + st.Name + " (" + st.StringId + ", " + Kind(st) + ", wlasciciel " + (st.OwnerClan != null ? st.OwnerClan.StringId : "-") + ", wojna z graczem "
                     + YN(st.MapFaction != null && mp.MapFaction != null && FactionManager.IsAtWarAgainstFaction(st.MapFaction, mp.MapFaction))
                     + ", stan " + (st.Village != null ? st.Village.VillageState.ToString() : st.IsUnderSiege ? "oblezenie" : "-") + ") | " + what + " | godzina " + Hour() + ".");
        }

        private static float GateD(MobileParty mp, Settlement st) { return mp.GetPosition2D.Distance(st.GatePosition.ToVec2()); }

        /// <summary>Promien spotkania z osada wedlug gry (PartyBase.GetEncounterTargetPoint: miasto - Town, reszta - Village).</summary>
        private static string Need(Settlement st)
        {
            try
            {
                var em = Campaign.Current.Models.EncounterModel;
                return (st.IsTown ? em.NeededMaximumDistanceForEncounteringTown : em.NeededMaximumDistanceForEncounteringVillage).ToString("0.00", Inv);
            }
            catch { return "?"; }
        }

        private static string Move(MobileParty mp) { return mp.DefaultBehavior + "/" + mp.ShortTermBehavior; }

        private static string MenuId()
        {
            var c = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
            return c != null && c.GameMenu != null ? c.GameMenu.StringId : "-";
        }

        private static string Hour()
        {
            double h = CampaignTime.Now.ToHours % 24.0;
            if (h < 0) h += 24.0;
            int m = (int)Math.Floor(h * 60.0);
            return (m / 60).ToString("00", Inv) + ":" + (m % 60).ToString("00", Inv);
        }

        private static bool SafeInquiry() { try { return InformationManager.IsAnyInquiryActive(); } catch { return false; } }

        private static bool ConversationOn()
        {
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            return cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive);
        }

        private static string YN(bool b) { return b ? "tak" : "nie"; }
    }
}
