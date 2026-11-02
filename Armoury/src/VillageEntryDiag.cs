using System;
using System.Diagnostics;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 316e - DIAGNOSTYKA wjazdu do wsi gry (tylko log, zero zmian zachowania). Jeff 01.11 ~21:35: "nie da sie wejsc do wiosek glownych, do pobocznych sie da,
    /// ale jak klikam w wioske glowna pojawia sie tylko popup i nie moge wejsc". W logach brak sladu: wejscia gracza to tylko wioski poboczne (WP) i zajazdy (ZJ),
    /// "Postoj w osadzie (m109)" co dobe "wsie 0". Sprawdzone w kodzie: klik w wies gry bierze SettlementVisualManager (Priority 40) przed MapVillagesView (100)
    /// i RoadsideInnsView (101); prefiks RaidTribute.EncounterPrefix pomija partie gracza. Przyczyny nie ustalono - stad linie "Wjazd do wsi gry: ...":
    ///  - proba: partia gracza dostala za cel ruchu wies gry (TargetSettlement) - nazwa, odleglosc od bramy, menu, ruch, godzina gry;
    ///  - spotkanie z wsia (PlayerEncounter.EncounterSettlement) i wjazd (CurrentSettlement) z id menu, zmiany menu w srodku (najwyzej MenuMax na pobyt), wyjazd;
    ///  - STOI U WSI: cel = wies gry, partia stoi przy bramie (MaxGateD), bez spotkania i bez wjazdu przez StuckTicks sprawdzen (ok. 2 s zegara) - stan: okno,
    ///    rozmowa, zachowanie ruchu, widocznosc, tryb czasu;
    ///  - cel porzucony: wies gry przestala byc celem bez wjazdu (nowy cel / postoj);
    ///  - popup: kazde nasze okno przy postoju na mapie (pytanie zajazdu, wioski gracza, Night Falls) - tytul, cel ruchu, najblizsza wies gry.
    /// Tick 4 razy na sekunde (zegar rzeczywisty) z CampaignEvents.TickEvent - kilka odczytow pol jednej partii. Potkniecia liczone, nic nie gasimy.
    /// </summary>
    internal static class VillageEntryDiag
    {
        private const string P = "Wjazd do wsi gry: ";
        private const float MaxGateD = 1.5f;
        private const int StuckTicks = 8;
        private const int MenuMax = 6;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static long _last;
        private static Settlement _aim, _enc, _in;
        private static string _inMenu;
        private static int _inMenus, _stuck, _stumbles;
        private static bool _stuckLogged;
        private static Vec2 _lastPos;

        /// <summary>Start sesji kampanii - stan od nowa (tylko pamiec, nic w zapisie).</summary>
        internal static void Reset() { _aim = _enc = _in = null; _inMenu = null; _inMenus = 0; _stuck = 0; _stuckLogged = false; }

        internal static void Tick()
        {
            long t = Stopwatch.GetTimestamp();
            if (t - _last < Stopwatch.Frequency / 4) return;
            _last = t;
            try
            {
                if (Campaign.Current == null) return;
                var mp = MobileParty.MainParty;
                if (mp == null) return;
                string menu = MenuId();
                var cur = mp.CurrentSettlement;
                // w wiosce gry: wjazd i zmiany menu
                if (cur != null && cur.IsVillage)
                {
                    if (!ReferenceEquals(cur, _in)) { _in = cur; _inMenu = menu; _inMenus = 0; _aim = null; _enc = null; Line(cur, "wjazd | menu " + menu, mp); }
                    else if (menu != _inMenu && _inMenus < MenuMax) { _inMenu = menu; _inMenus++; Line(cur, "menu " + menu + (_inMenus == MenuMax ? " (dalsze zmiany menu bez linii)" : ""), mp); }
                    return;
                }
                if (_in != null) { Line(_in, "wyjazd | menu " + menu, mp); _in = null; _inMenu = null; }
                // spotkanie z wsia bez wjazdu (menu spotkania, wies wroga itp.)
                Settlement enc = null;
                try { enc = PlayerEncounter.Current != null ? PlayerEncounter.EncounterSettlement : null; } catch { enc = null; }
                if (enc != null && enc.IsVillage && !ReferenceEquals(enc, _enc)) { _enc = enc; Line(enc, "spotkanie (bez wjazdu) | menu " + menu, mp); }
                if (enc == null) _enc = null;
                if (cur != null) { _aim = null; return; }   // inna osada (miasto, zamek)
                // cel ruchu: wies gry
                var tgt = mp.TargetSettlement;
                if (tgt != null && tgt.IsVillage && !ReferenceEquals(tgt, _aim))
                {
                    _aim = tgt; _stuck = 0; _stuckLogged = false;
                    Line(tgt, "proba (cel ruchu) | odleglosc od bramy " + GateD(mp, tgt).ToString("0.00", Inv) + " | menu " + menu + " | ruch " + Move(mp), mp);
                    return;
                }
                if (_aim == null) return;
                if (!ReferenceEquals(tgt, _aim))
                {
                    if (enc == null)
                        Line(_aim, "cel porzucony bez wjazdu | nowy cel " + (tgt != null ? tgt.Name.ToString() : "-") + " | odleglosc od bramy " + GateD(mp, _aim).ToString("0.00", Inv)
                                   + " | menu " + menu + " | ruch " + Move(mp), mp);
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
                        Line(_aim, "STOI U WSI bez spotkania i wjazdu | odleglosc od bramy " + GateD(mp, _aim).ToString("0.00", Inv) + " | okno " + YN(SafeInquiry())
                                   + " | rozmowa " + YN(ConversationOn()) + " | menu " + menu + " | ruch " + Move(mp) + " | widoczna " + YN(mp.IsVisible)
                                   + " | czas " + Campaign.Current.TimeControlMode, mp);
                    }
                }
                else _stuck = 0;
            }
            catch (Exception e) { if (++_stumbles <= 3 || _stumbles % 500 == 0) Log.Error("VillageEntryDiag.Tick (potkniecie " + _stumbles + ")", e); }
        }

        /// <summary>Nasze okno przy postoju na mapie (zajazd, wioska gracza, Night Falls) - linia z tytulem, celem ruchu i najblizsza wsia gry. Nie rzuca.</summary>
        internal static void Popup(string title)
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || mp == null) return;
                Settlement near = null;
                float nd = float.MaxValue;
                var pos = mp.GetPosition2D;
                foreach (var v in Village.All)
                {
                    if (v == null || v.Settlement == null) continue;
                    float d = pos.Distance(v.Settlement.GatePosition.ToVec2());
                    if (d < nd) { nd = d; near = v.Settlement; }
                }
                var tgt = mp.TargetSettlement;
                Log.Info(P + (tgt != null && tgt.IsVillage ? tgt.Name.ToString() : "-") + " | popup '" + title + "' | cel ruchu " + (tgt != null ? tgt.Name + " (" + tgt.StringId + ")" : "-")
                         + " | najblizsza wies gry " + (near != null ? near.Name + " (" + near.StringId + ") " + nd.ToString("0.00", Inv) + " jedn. od bramy" : "-")
                         + " | menu " + MenuId() + " | ruch " + Move(mp) + " | godzina " + Hour() + ".");
            }
            catch (Exception e) { if (++_stumbles <= 3 || _stumbles % 500 == 0) Log.Error("VillageEntryDiag.Popup (potkniecie " + _stumbles + ")", e); }
        }

        private static void Line(Settlement st, string what, MobileParty mp)
        {
            Log.Info(P + st.Name + " (" + st.StringId + ", wlasciciel " + (st.OwnerClan != null ? st.OwnerClan.StringId : "-") + ", wojna z graczem "
                     + YN(st.MapFaction != null && mp.MapFaction != null && FactionManager.IsAtWarAgainstFaction(st.MapFaction, mp.MapFaction))
                     + ", stan " + (st.Village != null ? st.Village.VillageState.ToString() : "-") + ") | " + what + " | godzina " + Hour() + ".");
        }

        private static float GateD(MobileParty mp, Settlement st) { return mp.GetPosition2D.Distance(st.GatePosition.ToVec2()); }

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
