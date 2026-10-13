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
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// M6a-3 (raport roczny 13.10, tabela 6.2 wiersz 19; DECYZJA z pelnymi uprawnieniami): POKOJ NA ZERO DNI NIE WYPUSZCZA JENCOW-LORDOW. Klucz
    /// PeaceReleaseAfterDay.
    /// Bieg roczny: 440 z 528 lordow uwolnionych "przez pokoj" wyszlo w dobach pokoju fabularnego, po ktorym wojna wrocila tej samej doby (73 pokoje,
    /// 73 wojny wznowione; tlo poza takimi dobami 0.18 uwolnienia dziennie).
    /// Przyczyna (kod gry): PrisonerReleaseCampaignBehavior.OnMakePeaceEvent (nasluch CampaignEvents.MakePeace) -> ReleasePrisonersInternal obu stron:
    /// kazdy jeniec-bohater w lochach osad, druzynach, karawanach, taborach wsi i zalogach strony, ktorego krolestwo NIE jest w wojnie z ta strona ->
    /// EndCaptivityAction.ApplyByPeace - od razu, w tej samej klatce. Pokoj zawiera decyzja krolestwa (KingdomDecisionProposalBehavior.DailyTick,
    /// "ByKingdomDecision"), a ROT ROTStorylineWars.EnforceWars (DailyTickEvent) w tej samej dobie wypowiada wojne ponownie (linie E7/0: pokoj i wojna
    /// tej samej sekundy, "dni od poprzedniego zdarzenia 0"). Wojna trwa dalej, a jency sa juz wolni - za darmo.
    /// Regula (bezpieczna droga w kodzie gry): prefiks na OnMakePeaceEvent, tylko gdy OBIE strony to krolestwa - uwolnienie gry jest odlozone o 24 godziny
    /// (do najblizszej godziny po dobie; w tym czasie na pewno przeszla doba ROT EnforceWars, niezaleznie od godziny pokoju). Po 24 h: para znow w wojnie
    /// -> uwolnienie odwolane (jency zostaja jencami - okup, ucieczka, odbicie jak w wojnie); para w pokoju -> ta sama metoda gry ReleasePrisonersInternal
    /// dla obu stron (pokoj prawdziwy - jency wolni dobe pozniej niz dotad). Pokoj rodu (najemnik odchodzi, rod spoza krolestwa) i zmiana krolestwa rodu
    /// (ClanChangedKingdom) - bez zmian. Gracz w niewoli - jak w grze (gra go tu nie uwalnia).
    /// Klucz wylaczony = stan 4d33042 (prefiks przepuszcza; czekajace pary z zapisu sa i tak sprawdzane po 24 h). Zloto: brak. Zapis: czekajace pary
    /// (segment 3 klucza "arm_m6a"). Linie "Pokoj a jency (M6a-3)": przy kazdym odlozeniu i rozstrzygnieciu oraz linia doby.
    /// </summary>
    internal static class PeaceHold
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double WaitHours = 24.0;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PeaceReleaseAfterDay; } }

        private sealed class Wait { public string A, B; public long Hour; public int Lords; }
        private static readonly List<Wait> _wait = new List<Wait>();

        // liczniki doby i sesji
        private static int _dHeld, _dHeldLords, _dKept, _dKeptLords, _dFreed, _dFreedLords, _dGone;
        private static int _sHeld, _sKept, _sKeptLords, _sFreed, _sFreedLords;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static MethodInfo _mRelease;
        private static string _wired = "-";

        internal static void Reset()
        {
            _wait.Clear(); ZeroDay(); _sHeld = _sKept = _sKeptLords = _sFreed = _sFreedLords = 0;
            _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
        }

        private static void ZeroDay() { _dHeld = _dHeldLords = _dKept = _dKeptLords = _dFreed = _dFreedLords = _dGone = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("PeaceHold." + where, e); } catch { }
        }

        private static string Name(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        // ------------------------------------------------------------ ilu jencow-bohaterow uwolnilaby gra dla strony f (ten sam przeglad co
        // PrisonerReleaseCampaignBehavior.ReleasePrisonersInternal - tylko odczyt)
        private static bool Freed(TroopRosterElement el, IFaction f)
        {
            var c = el.Character;
            return c != null && c.IsHero && c.HeroObject != null && c.HeroObject != Hero.MainHero && c.HeroObject.MapFaction != null && !c.HeroObject.MapFaction.IsAtWarWith(f.MapFaction);
        }

        private static int CountRoster(PartyBase p, IFaction f, bool partyOnly)
        {
            int n = 0;
            if (p == null || p.PrisonRoster == null) return 0;
            var r = p.PrisonRoster;
            for (int i = r.Count - 1; i >= 0; i--)
            {
                if (r.GetElementNumber(i) <= 0) continue;
                var el = r.GetElementCopyAtIndex(i);
                if (!Freed(el, f)) continue;
                if (partyOnly && el.Character.HeroObject.PartyBelongedToAsPrisoner != p) continue;   // gra: wpis bez jenca tylko zdejmuje z listy
                n++;
            }
            return n;
        }

        private static int CountMobile(MobileParty mp, IFaction f) { return mp != null ? CountRoster(mp.Party, f, true) : 0; }

        private static int CountClan(Clan clan, IFaction f)
        {
            int n = 0;
            foreach (var h in clan.AliveLords) foreach (var cc in h.OwnedCaravans) n += CountMobile(cc != null ? cc.MobileParty : null, f);
            foreach (var h in clan.Companions) foreach (var cc in h.OwnedCaravans) n += CountMobile(cc != null ? cc.MobileParty : null, f);
            foreach (var wp in clan.WarPartyComponents) n += CountMobile(wp != null ? wp.MobileParty : null, f);
            foreach (var st in clan.Settlements)
            {
                if (st == null) continue;
                if (st.IsVillage && st.Village != null && st.Village.VillagerPartyComponent != null) n += CountMobile(st.Village.VillagerPartyComponent.MobileParty, f);
                else if ((st.IsCastle || st.IsTown) && st.Town != null && st.Town.GarrisonParty != null) n += CountMobile(st.Town.GarrisonParty, f);
            }
            return n;
        }

        private static int Releasable(IFaction f)
        {
            int n = 0;
            try
            {
                if (f == null) return 0;
                foreach (var st in f.Settlements) if (st != null && st.Party != null) n += CountRoster(st.Party, f, false);
                if (f.IsClan || f.IsMinorFaction) n += CountClan((Clan)f, f);
                else if (f.IsKingdomFaction) foreach (var c in ((Kingdom)f).Clans) if (c != null) n += CountClan(c, f);
            }
            catch (Exception e) { Stumble("Releasable", e); }
            return n;
        }

        // ------------------------------------------------------------ latka gry
        /// <summary>Prefiks PrisonerReleaseCampaignBehavior.OnMakePeaceEvent(IFaction, IFaction, MakePeaceDetail): pokoj dwoch krolestw - uwolnienie odlozone
        /// o 24 h (false = gra teraz nikogo nie uwalnia). Inne pokoje, klucz wylaczony, brak metody uwolnienia - gra jak dotad.</summary>
        public static bool PeacePrefix(IFaction __0, IFaction __1)
        {
            try
            {
                if (!On || _mRelease == null || __0 == null || __1 == null || !__0.IsKingdomFaction || !__1.IsKingdomFaction) return true;
                var a = (Kingdom)__0; var b = (Kingdom)__1;
                if (a.StringId == null || b.StringId == null || a.StringId.IndexOfAny(Bad) >= 0 || b.StringId.IndexOfAny(Bad) >= 0) return true;
                int lords = Releasable(a) + Releasable(b);
                _wait.Add(new Wait { A = a.StringId, B = b.StringId, Hour = (long)Math.Floor(CampaignTime.Now.ToHours), Lords = lords });
                _dHeld++; _dHeldLords += lords; _sHeld++;
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                    Log.Info("Pokoj a jency (M6a-3): dzien " + (int)CampaignTime.Now.ToDays + " | " + Name(a) + " - " + Name(b) + ": pokoj - uwolnienie jencow odlozone o "
                             + WaitHours.ToString("0", Inv) + " h (gra uwolnilaby teraz " + lords + " jencow-bohaterow); po tym czasie para w wojnie = jency zostaja, w pokoju = gra ich uwalnia.");
                return false;
            }
            catch (Exception e) { Stumble("PeacePrefix", e); return true; }
        }

        // ------------------------------------------------------------ co godzine: rozstrzygniecie par, ktore czekaja 24 h (takze przy kluczu wylaczonym)
        internal static void Hourly()
        {
            if (_wait.Count == 0 || Campaign.Current == null) return;
            try
            {
                long now = (long)Math.Floor(CampaignTime.Now.ToHours);
                var s = Settings.Current;
                for (int i = 0; i < _wait.Count; i++)
                {
                    var w = _wait[i];
                    if (now - w.Hour < (long)WaitHours) continue;
                    _wait.RemoveAt(i); i--;
                    try
                    {
                        var a = KingdomById(w.A); var b = KingdomById(w.B);
                        if (a == null || b == null || a.IsEliminated || b.IsEliminated) { _dGone++; continue; }   // krolestwo upadlo - jency rozliczone przy zmianie krolestwa rodow (gra)
                        if (a.IsAtWarWith(b))
                        {
                            int kept = Releasable(a) + Releasable(b);   // w wojnie przeglad gry zwraca 0 dla tej pary - licznik z chwili pokoju ponizej
                            _dKept++; _dKeptLords += w.Lords; _sKept++; _sKeptLords += w.Lords;
                            if (s != null && s.LogEnabled)
                                Log.Info("Pokoj a jency (M6a-3): dzien " + (int)CampaignTime.Now.ToDays + " | " + Name(a) + " - " + Name(b) + ": znow w wojnie - uwolnienie odwolane, "
                                         + w.Lords + " jencow-bohaterow z chwili pokoju zostaje w niewoli (gra uwolnilaby dzis " + kept + ").");
                            continue;
                        }
                        if (_mRelease == null) { _dGone++; continue; }   // metoda gry niedostepna (latki nie wpiete po wczytaniu) - nie ma czym uwolnic
                        int n = Releasable(a) + Releasable(b);
                        _mRelease.Invoke(null, new object[] { a });
                        _mRelease.Invoke(null, new object[] { b });
                        _dFreed++; _dFreedLords += n; _sFreed++; _sFreedLords += n;
                        if (s != null && s.LogEnabled)
                            Log.Info("Pokoj a jency (M6a-3): dzien " + (int)CampaignTime.Now.ToDays + " | " + Name(a) + " - " + Name(b) + ": nadal pokoj - gra uwalnia "
                                     + n + " jencow-bohaterow (w chwili pokoju " + w.Lords + ").");
                    }
                    catch (Exception e) { Stumble("Hourly(para)", e); }
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                if (s.LogEnabled)
                    Log.Info("Pokoj a jency (M6a-3): dzien " + (int)CampaignTime.Now.ToDays + " | klucz Peace Release After Day " + (On ? "tak" : "nie") + " | latka " + _wired
                             + " | dzis: pokoje krolestw z odlozonym uwolnieniem " + _dHeld + " (jencow-bohaterow " + _dHeldLords + ")"
                             + ", rozstrzygniete: znow w wojnie - jency zostaja " + _dKept + " (jencow-bohaterow " + _dKeptLords + "), nadal pokoj - uwolnieni " + _dFreed
                             + " (jencow-bohaterow " + _dFreedLords + "), krolestwo upadlo " + _dGone + " | czeka " + _wait.Count
                             + " | sesja: odlozone " + _sHeld + ", odwolane " + _sKept + " (jencow " + _sKeptLords + "), uwolnione po dobie " + _sFreed + " (jencow " + _sFreedLords + ")"
                             + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + (_importN >= 0 ? " | wczytano par " + _importN + " (bledne " + _importBad + ")" : "") + ".");
                _importN = -1;
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay();
        }

        private static bool _hooksTried;
        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            string a = "BRAK", b = "BRAK";
            try
            {
                _mRelease = AccessTools.Method(typeof(PrisonerReleaseCampaignBehavior), "ReleasePrisonersInternal", new[] { typeof(IFaction) });
                if (_mRelease != null) b = "jest";
                var m = AccessTools.Method(typeof(PrisonerReleaseCampaignBehavior), "OnMakePeaceEvent", new[] { typeof(IFaction), typeof(IFaction), typeof(MakePeaceAction.MakePeaceDetail) });
                if (m != null && _mRelease != null) { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(PeaceHold), nameof(PeacePrefix))); a = "wpiety"; }
            }
            catch (Exception e) { Log.Error("PeaceHold.EnsureHooks", e); }
            _wired = "pokoj (OnMakePeaceEvent) " + a + ", uwolnienie gry (ReleasePrisonersInternal) " + b;
            Log.Info("Pokoj a jency (M6a-3): latki - " + _wired + "; klucz Peace Release After Day " + (On ? "tak" : "nie") + ".");
        }

        // ------------------------------------------------------------ zapis (segment 3 klucza "arm_m6a"): "idA:idB:godzina:jency;..."
        private static readonly char[] Bad = { '|', ';', ':' };

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_wait.Count * 40 + 4);
                bool first = true;
                foreach (var w in _wait)
                {
                    if (w.A == null || w.B == null || w.A.IndexOfAny(Bad) >= 0 || w.B.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(w.A).Append(':').Append(w.B).Append(':').Append(w.Hour.ToString(Inv)).Append(':').Append(w.Lords.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _wait.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                foreach (var p in data.Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); long h; int n;
                    if (x.Length == 4 && x[0].Length > 0 && x[1].Length > 0 && long.TryParse(x[2], NumberStyles.Integer, Inv, out h) && int.TryParse(x[3], NumberStyles.Integer, Inv, out n))
                    { _wait.Add(new Wait { A = x[0], B = x[1], Hour = h, Lords = n }); _importN++; }
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
