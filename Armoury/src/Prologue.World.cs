using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m104-e5 - SCENY ZMIENIAJACE SWIAT PROLOGU 298 (projekt PROJEKT-PROLOG-298 pkt 2, rodzaj "S"; Jeff 21.10 "rob to teraz"). Tylko kampania z prologiem (Active),
    /// kazda scena pod swoim kluczem MCM (czytany co dobe), wszystko konczy sie najpozniej przy koncu prologu (dzien 186-188); kampania klasyczna - latki sie nawet nie
    /// wpinaja (InstallRest leniwie przy pierwszej kampanii z prologiem), a zapis arm_prologue jest pusty.
    ///
    /// STRAZ NIEWOLI (pkt 3 i 4): jency kanonu z tabeli arm_canon_moves.tsv (CanonCompanions.HeldRow - ostatni wykonany wiersz "prisoner", pozniejszy "free" jeszcze
    /// nie zapadl): #p01 Tyrion w lochu Orlego Gniazda (dzien 110 -> #p02 w dniu 132, klucz PrologueTyrionHeld) i #p10 Jaime w Riverrun (wojna Polnocy +5 dni ->
    /// #m02 po Czarnym Nurcie, klucz PrologueJaimeAtRiverrun, najwyzej JaimeHoldMaxDays dni). Rozpoznanie m104 (dekompilacja 1.4.8): RealisticCaptivity dotyka tylko
    /// niewoli gracza; jeniec AI w lochu wychodzi przez (a) ucieczke 4%/dobe (PrisonerReleaseCampaignBehavior.DailyHeroTick), (b) zwolnienie przy pokoju / zmianie
    /// krolestwa rodu (ReleasePrisonersInternal - Tyrion i Dolina, Jaime i Dorzecze nie sa w wojnie z jego krolestwem), (c) okup AI-AI (RansomOfferCampaignBehavior.
    /// DailyTickHero -> ConsiderRansomPrisoner, u nas Ransom178 - zloto plynie PRZED EndCaptivityAction.ApplyByRansom), (d) wypuszczenie przez porywacza i wykup.
    /// Dwie latki: prefiks EndCaptivityAction.ApplyInternal (ucieczka, pokoj, okup, wypuszczenie, wykup - pomijane; smierc i "po bitwie" (upadek osady, zmiana pana)
    /// zawsze przechodza; gracz jako sprawca (rozbicie lochu, jego okup) przechodzi - nic nie placi za darmo) i prefiks RansomOfferCampaignBehavior.DailyTickHero
    /// (okup nie zaczyna sie wcale - zloto nie plynie za jenca, ktory zostanie w lochu). Straz tylko w lochu OSADY (jeniec w druzynie - zwykle zasady gry: rozwiazanie
    /// druzyny i tratwa uwalniaja go, inaczej zostalby w rozwiazanej partii) i tylko przy kluczu CanonCompanionsFollowStory (bez niego wiersz "free" nie przyjdzie).
    /// Nasze uwolnienie (akcja "free") przechodzi (CanonCompanions.Freeing).
    ///
    /// Zapis: arm_prologue (klucze "w*"). Linie logu: "Prolog E5 (m104): ..." - start sesji, doba (kazda scena: stan / pozycja / postep), zdarzenia scen.
    /// Bledy: Stumble (licznik prologu), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Prologue
    {
        internal const string WTag = "Prolog E5 (m104): ";
        private const string TyrionTpl = "ROTwanderer8", JaimeTpl = "ROTwanderer1";
        private const string TyrionHoldRow = "p01", JaimeHoldRow = "p10";
        private const int TyrionHoldLastDay = 140;    // dzien prologu - zapas po #p02 (132); straz Tyriona gasnie najpozniej wtedy
        private const int JaimeHoldMaxDays = 364;     // dni od #p10 - Jaime w Riverrun najwyzej rok (dlugi rok Armoury), gdyby #m02 nie przyszedl
        private const int WorldLogAfterStory = 20;    // linia doby E5 jeszcze tyle dni po starcie opowiesci (jak linia doby prologu), potem co 10 dni przy strazy

        // ---------------------------------------------------------------- stan (zapis)
        // straz niewoli: od poczatku - ucieczki, okupy (dni bez okupu), pokoj, wypuszczenie przez porywacza, wykup; przepuszczone (gracz, smierc, po bitwie)
        private static int _hbEsc, _hbRan, _hbPeace, _hbChoice, _hbComp, _hbPass;
        // sesja / doba
        private static int _hbDay, _hbPassDay, _wStumbles;
        private static readonly HashSet<string> _hbSaid = new HashSet<string>(StringComparer.Ordinal);
        private static string _worldWire = "nie wpiete";
        private static bool _worldDirty;      // po wczytaniu: partie scen odbudowane w pierwszej godzinie (WorldHourly)

        private static void ResetWorld()
        {
            _hbEsc = _hbRan = _hbPeace = _hbChoice = _hbComp = _hbPass = 0;
            _hbDay = _hbPassDay = _wStumbles = 0; _hbSaid.Clear();
            _worldDirty = false;
        }

        private static void WStumble(string where, Exception e) { _wStumbles++; Stumble("World." + where, e); }

        private static string PackWorld()
        {
            return "whb=" + string.Join(",", new[] { _hbEsc, _hbRan, _hbPeace, _hbChoice, _hbComp, _hbPass }.Select(x => x.ToString(Inv))) + ";";
        }

        private static void UnpackWorld(Dictionary<string, string> map)
        {
            ResetWorld();
            string v;
            if (map.TryGetValue("whb", out v))
            {
                var a = v.Split(',');
                int[] r = new int[6];
                for (int i = 0; i < 6 && i < a.Length; i++) int.TryParse(a[i], System.Globalization.NumberStyles.Integer, Inv, out r[i]);
                _hbEsc = r[0]; _hbRan = r[1]; _hbPeace = r[2]; _hbChoice = r[3]; _hbComp = r[4]; _hbPass = r[5];
            }
            _worldDirty = true;
        }

        // ---------------------------------------------------------------- latki (raz na proces, przy pierwszej kampanii z prologiem - InstallRest)

        private static string ApplyWorld(Harmony h)
        {
            string g = "BRAK", r = "BRAK";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(EndCaptivityAction), "ApplyInternal");
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(CaptivityPrefix))) != null) g = "TAK";
            }
            catch (Exception e) { WStumble("ApplyWorld.Captivity", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(RansomOfferCampaignBehavior), "DailyTickHero", new[] { typeof(Hero) });
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(RansomDailyPrefix))) != null) r = "TAK";
            }
            catch (Exception e) { WStumble("ApplyWorld.Ransom", e); }
            _worldWire = "straz niewoli " + g + ", okup " + r;
            return _worldWire;
        }

        // ---------------------------------------------------------------- straz niewoli (pkt 3 i 4)

        private static bool HoldKey(string row)
        {
            var s = S;
            if (s == null || !s.CanonCompanionsFollowStory) return false;
            if (row == TyrionHoldRow) return s.PrologueTyrionHeld;
            if (row == JaimeHoldRow) return s.PrologueJaimeAtRiverrun;
            return false;
        }

        private static bool HoldInTime(string row, Hero h)
        {
            if (row == TyrionHoldRow) return PrologueDay() < TyrionHoldLastDay;
            if (row == JaimeHoldRow)
            {
                int since = CanonCompanions.HeldDay(h);
                return since < 0 || (int)CampaignTime.Now.ToDays - since <= JaimeHoldMaxDays;
            }
            return false;
        }

        /// <summary>Wiersz niewoli, ktorej pilnuje scena (null - straz nie dziala dla tego bohatera): kampania z prologiem, klucz, czas, loch osady.</summary>
        internal static string HeldByScene(Hero h)
        {
            if (!Active || h == null) return null;
            var row = CanonCompanions.HeldRow(h);
            if (row == null || !HoldKey(row) || !HoldInTime(row, h)) return null;
            var pp = h.PartyBelongedToAsPrisoner;
            if (pp == null || !pp.IsSettlement) return null;
            return row;
        }

        private static string WhoS(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        private static string WhereS(Hero h)
        {
            try
            {
                if (h == null) return "-";
                if (!h.IsAlive) return "nie zyje";
                var pp = h.PartyBelongedToAsPrisoner;
                if (h.IsPrisoner && pp != null) return "jeniec " + (pp.IsSettlement ? pp.Settlement.StringId : pp.Name.ToString());
                var p = h.PartyBelongedTo;
                if (p != null) return (p.LeaderHero == h ? "prowadzi druzyne" : "w druzynie " + p.Name) + (p.CurrentSettlement != null ? " w " + p.CurrentSettlement.StringId : "");
                return h.CurrentSettlement != null ? "w " + h.CurrentSettlement.StringId : "bez miejsca";
            }
            catch { return "?"; }
        }

        /// <summary>Prefiks EndCaptivityAction.ApplyInternal(prisoner, detail, facilitator, showNotification): jeniec trzymany przez scene nie wychodzi przez ucieczke,
        /// pokoj, okup, wypuszczenie ani wykup. Smierc i "po bitwie" (upadek osady, zmiana pana), gracz jako sprawca i nasze uwolnienie - przechodza.</summary>
        public static bool CaptivityPrefix(Hero __0, EndCaptivityDetail __1, Hero __2)
        {
            try
            {
                if (!Active || __0 == null || __0 == Hero.MainHero) return true;
                string row = HeldByScene(__0);
                if (row == null) return true;
                string what;
                bool pass = __1 == EndCaptivityDetail.Death || __1 == EndCaptivityDetail.ReleasedAfterBattle || CanonCompanions.Freeing(__0) || (__2 != null && __2 == Hero.MainHero);
                if (pass)
                {
                    if (CanonCompanions.Freeing(__0)) return true;   // nasze uwolnienie (#p02 / #m02) - linie pisze CanonCompanions
                    _hbPass++; _hbPassDay++;
                    Log.Info(WTag + "straz niewoli: PRZEPUSZCZONE - " + WhoS(__0) + " (#" + row + ", " + WhereS(__0) + ") - " + __1 + (__2 == Hero.MainHero ? " (sprawca gracz)" : "")
                             + " (dzien prologu " + PrologueDay().ToString("0.0", Inv) + ").");
                    return true;
                }
                switch (__1)
                {
                    case EndCaptivityDetail.ReleasedAfterEscape: _hbEsc++; what = "ucieczka"; break;
                    case EndCaptivityDetail.ReleasedAfterPeace: _hbPeace++; what = "zwolnienie przy pokoju"; break;
                    case EndCaptivityDetail.Ransom: _hbRan++; what = "okup"; break;
                    case EndCaptivityDetail.ReleasedByCompensation: _hbComp++; what = "wykup"; break;
                    default: _hbChoice++; what = "wypuszczenie przez porywacza"; break;
                }
                _hbDay++;
                string key = __0.StringId + "|" + what;
                if (_hbSaid.Add(key))
                    Log.Info(WTag + "straz niewoli: ZABLOKOWANE - " + WhoS(__0) + " (#" + row + ", " + WhereS(__0) + ") - " + what + (__2 != null ? " (sprawca " + WhoS(__2) + ")" : "")
                             + " (dzien prologu " + PrologueDay().ToString("0.0", Inv) + "; linia raz na dobe na rodzaj).");
                return false;
            }
            catch (Exception e) { WStumble("CaptivityPrefix", e); return true; }
        }

        /// <summary>Prefiks RansomOfferCampaignBehavior.DailyTickHero(hero): jeniec trzymany przez scene - okup nie zaczyna sie wcale (zloto nie plynie przed zwolnieniem,
        /// ktore straz i tak by zatrzymala). Liczone jako dni bez okupu.</summary>
        public static bool RansomDailyPrefix(Hero __0)
        {
            try
            {
                if (!Active || __0 == null || !__0.IsPrisoner) return true;
                if (HeldByScene(__0) == null) return true;
                _hbRan++; _hbDay++;
                return false;
            }
            catch (Exception e) { WStumble("RansomDailyPrefix", e); return true; }
        }

        private static string HoldText(string tpl, string row, string freeRow)
        {
            var h = CanonCompanions.TemplateHero(tpl);
            if (h == null) return "brak postaci";
            string held = HeldByScene(h);
            string tab = CanonCompanions.HeldRow(h);
            return WhoS(h) + " " + WhereS(h) + (held != null ? " (#" + held + ", straz TAK)"
                   : tab != null ? " (#" + tab + ", straz NIE: " + (!HoldKey(tab) ? "klucz" : !HoldInTime(tab, h) ? "po czasie" : "nie w lochu osady") + ")"
                   : " (poza niewola #" + row + "/#" + freeRow + ")");
        }

        // ---------------------------------------------------------------- doba, godzina, zapis gry

        /// <summary>Prologue.Daily (kazda doba kampanii z prologiem, takze po starcie opowiesci - straz Jaimego trwa w wojnie): sceny po kolei, linia doby.</summary>
        private static void DailyWorld()
        {
            if (!Active) return;
            double day = PrologueDay();
            int d = (int)day;
            bool holdOn = CanonCompanions.HeldCount > 0;
            if (day > PrologueDays + WorldLogAfterStory && !(holdOn && d % 10 == 0)) { _hbDay = 0; _hbPassDay = 0; _hbSaid.Clear(); return; }
            var sb = new StringBuilder(600);
            sb.Append(WTag).Append("dzien ").Append(d.ToString(Inv));
            sb.Append(" | Tyrion: ").Append(HoldText(TyrionTpl, TyrionHoldRow, "p02"));
            sb.Append(" | Jaime: ").Append(HoldText(JaimeTpl, JaimeHoldRow, "m02"));
            sb.Append(" | straz niewoli: zablokowane dzis ").Append(_hbDay).Append(", od poczatku ucieczki ").Append(_hbEsc).Append(", dni bez okupu i okupy ").Append(_hbRan)
              .Append(", pokoj ").Append(_hbPeace).Append(", wypuszczenie ").Append(_hbChoice).Append(", wykup ").Append(_hbComp).Append(" | przepuszczone (gracz, smierc, po bitwie) dzis ")
              .Append(_hbPassDay).Append(", od poczatku ").Append(_hbPass);
            sb.Append(" | potkniecia E5 ").Append(_wStumbles).Append('.');
            Log.Info(sb.ToString());
            _hbDay = 0; _hbPassDay = 0; _hbSaid.Clear();
        }

        /// <summary>OnSessionLaunched: linia startu E5 (tylko kampania z prologiem).</summary>
        internal static void WorldStartLine()
        {
            if (!Active) return;
            var s = S;
            Func<bool, string> yn = b => b ? "TAK" : "NIE";
            Log.Info(WTag + "start sesji | dzien prologu " + PrologueDay().ToString("0.0", Inv)
                     + " | klucze: Tyrion " + yn(s != null && s.PrologueTyrionHeld) + ", Jaime " + yn(s != null && s.PrologueJaimeAtRiverrun)
                     + " (zmiany stron CanonCompanionsFollowStory " + yn(s != null && s.CanonCompanionsFollowStory) + ")"
                     + " | latki: " + _worldWire
                     + " | straz niewoli od poczatku: ucieczki " + _hbEsc + ", okup " + _hbRan + ", pokoj " + _hbPeace + ", wypuszczenie " + _hbChoice + ", wykup " + _hbComp
                     + ", przepuszczone " + _hbPass + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>HourlyTickEvent: po wczytaniu - partie scen odbudowane i zamrozone od nowa (flaga gry nie idzie do zapisu - WorldBeforeSave).</summary>
        internal static void WorldHourly()
        {
            if (!Active) return;
            if (_worldDirty) _worldDirty = false;
        }

        internal static void WorldBeforeSave() { }

        internal static void WorldSaveOver() { }
    }
}
