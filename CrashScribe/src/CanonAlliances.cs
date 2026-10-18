using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace CrashScribe
{
    /// <summary>
    /// SOJUSZE JAK W KRONIKACH (noc 17/18.10, paczka f19-kanon-a; Jeff: "stannis nie ma sojuszu z dorzeczem to bzdura!",
    /// "naprawa tych wydarzen i cofniecie polnocy"; analiza kanonu 18.10, temat B).
    ///
    /// Skad sojusz Dorzecze - Stannis (dekompilacja gry 1.4.8): AI sojuszy gry (od 1.3) - KingdomDecisionProposalBehavior.DailyTickClan ->
    /// GetRandomStartingAllianceDecision (losowe krolestwo) -> StartAllianceDecision.CanMakeDecision -> AllianceModel.CanMakeAlliance
    /// (sasiedzi, grozny sasiad, wynik >= 50 obu stron, najwyzej 2 sojusze) -> glos rodow -> AllianceCampaignBehavior.StartAlliance.
    /// Kanonu gra nie sprawdza. ROT, Diplomacy 1.4.7, BK, BKROTPatch, StrategicCampaignAI, RealisticBannerlord, BetterEconomy, DiplomacyUI -
    /// zadnego kodu sojuszy (grep dekompilacji 18.10). KingdomDecisionPermissionModel gra sprawdza tylko w ShouldBeCancelled - AI go omija.
    ///
    /// Czesci (kazda pod wlasnym kluczem, domyslnie true):
    ///  (1) linia "SOJUSZE" w KRONICE WOJEN (WarReport, bez klucza - tylko odczyt): kazda para sojuszu, dzien konca, porozumienia wezwan.
    ///  (2) CanonAlliances - postfiks AccessTools.DeclaredMethod(DefaultAllianceModel, "CanMakeAlliance"), wpinany w OnSessionLaunched
    ///      (pulapka 15.10 - metoda deklarowana w DefaultAllianceModel). To jedyna brama: wola ja StartAllianceDecision.CanMakeDecision
    ///      (propozycja AI, odnowienie po wygasnieciu w AllianceCampaignBehavior.DailyTickClan -> AddAllianceDecision, anulowanie wniosku
    ///      ShouldBeCancelledInternal) i ekran dyplomacji. Gdy gra mowi TAK, a para dotyka krolestwa Westeros i nie jest na liscie - NIE,
    ///      z wlasnym powodem po angielsku (nowy TextObject; wspolny _allianceNotFormedExplanationText modelu nietkniety).
    ///      Lista: Polnoc (battania) + Dorzecze (riverlands) zawsze; Stormlands + Reach (empire_w) dopoki Renly zyje; Korona (vlandia)
    ///      + Reach po jego smierci. Stannis (dragonstone), Nocna Straz i reszta Westeros - zadnych sojuszy. Para bez krolestwa Westeros
    ///      (Essos, krolestwa buntu z wlasnym id) - jak w grze. Krolestwo, ktorym rzadzi gracz (RulingClan == PlayerClan) - poza regula.
    ///  (3) CanonAlliancesBreakOnLoad (tylko z (2)) - w pierwszej godzinie po wczytaniu (wszystkie OnSessionLaunched juz przeszly, takze
    ///      latki Armoury na EndCallToWarAgreement) kazdy sojusz AI spoza listy zerwany: najpierw kazde porozumienie wezwania tej pary
    ///      osobno przez interfejs IAllianceCampaignBehavior.EndCallToWarAgreement (wywolanie przez interfejs nie wkleja sie w kod
    ///      wolajacego - latki CrownCallToWar.EndPostfix i Measure31 EndFin widza kazde; w EndAlliance gry krotka metoda moze byc
    ///      wklejona, por. CrownCallToWar P3/W4), potem EndAlliance gry (bez kary relacji - kara -100 jest tylko w OnWarDeclared).
    ///      Wojen nie ruszamy. Powtorne uruchomienie nic nie robi (sojuszy spoza listy juz nie ma).
    ///  (4) CanonReachNorthWar - ROT ma wojne fabularna FIVE_KINGS_REACH_NORTH (Reach - Polnoc, "The Reach allied with the Westerlands after
    ///      Renly's death"), ktora tylko OnTheRedWedding wylacza, a nic nie wlacza. Postfiks ROTStorylineWars.OnRenlysDeath (ten sam moment,
    ///      w ktorym ROT wlacza FIVE_KINGS_REACH_RIVERLANDS): Trigger(true) i tekst _reachAlliedRenly - wypowiada ROT w EnforceWars
    ///      (DeclareWarAction.ApplyByDefault z CallingDeclareWar, tylko gdy obie strony istnieja i nie sa w wojnie) i pilnuje jej do Krwawych
    ///      Godow. Nadrobienie w dobie (stary zapis): RenlyDeathEvent zakonczone, Krwawe Gody nie odpalily, FIVE_KINGS_REACH_RIVERLANDS
    ///      wlaczona przez ROT (dowod, ze OnRenlysDeath przeszedl), FIVE_KINGS_REACH_NORTH nie. Raz: stan Trigger jest w zapisie ROT.
    /// Bledy: per wywolanie (wynik gry zostaje), potkniecia liczone, pierwsze w raporcie; zadnego globalnego wylacznika.
    /// Bez zapisu w grze (stan sesji od wczytania).
    /// </summary>
    internal static class CanonAlliances
    {
        private const string HarmonyId = "com.jeff.crashscribe";
        private static Harmony _harmony;
        private static bool _hooksTried, _ruleOn, _rotOk;
        private static string _ruleState = "nie wpiete", _renlyState = "nie wpiete";

        // krolestwa Westeros: id z ROT-Content (spkingdoms.xslt dla id gry, ROT_spkingdoms.xml dla wlasnych) i ROT.Misc.ROTKingdoms
        private static readonly HashSet<string> Westeros = new HashSet<string>
        {
            "battania", "vlandia", "riverlands", "dragonstone", "stormlands", "empire_w", "empire", "sturgia", "aserai", "nightswatch", "freefolk", "skagosi"
        };
        private const string North = "battania", River = "riverlands", Storm = "stormlands", Reach = "empire_w", Crown = "vlandia";
        private const string RenlyHeroId = "lord_1_9";   // ROT.Misc.ROTLords.RenlyBaratheon (GetLordByID), wladca stormlands w ROT_spkingdoms.xml

        // ROT (refleksja): wojny fabularne
        private static FieldInfo _fSubWars;        // ROT.SubModule.StorylineWars (internal static)
        private static MethodInfo _mGetWar;        // ROTStorylineWars.GetWar(WarID) (internal)
        private static MethodInfo _mTrigger;       // ROTStorylineWar.Trigger(bool)
        private static PropertyInfo _pTriggered, _pF1, _pF2, _pFlavor;
        private static FieldInfo _fReachAllied;    // ROTStorylineWars._reachAlliedRenly
        private static object _idReachNorth, _idReachRiver;

        // stan sesji
        private static bool _cleanDone, _rnDone;
        private static IFaction _rnA, _rnB; private static int _rnDays;   // wlaczona wojna czeka na wypowiedzenie przez ROT
        private static double _renlyAt = -1; private static bool _renlyGone, _renlyLooked;
        private static Hero _renly;
        private static readonly Dictionary<string, int> _dBlocked = new Dictionary<string, int>();
        private static int _tBlocked, _stumbles, _stumblesDay;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles <= 3) { try { Scribe.Report("CrashScribe", e, "CanonAlliances." + where + " (potkniecie " + _stumbles + ")", null); } catch { } }
        }

        internal static void ResetSession()
        {
            _cleanDone = false; _rnDone = false; _rnA = _rnB = null; _rnDays = 0;
            _renlyAt = -1; _renlyGone = false; _renlyLooked = false; _renly = null;
            _dBlocked.Clear(); _tBlocked = 0; _stumbles = 0; _stumblesDay = 0;
        }

        // ------------------------------------------------------------------ zasada

        private static bool Is(string x, string y, string a, string b) { return (x == a && y == b) || (x == b && y == a); }

        private static bool PlayerRules(Kingdom k)
        {
            try { return k != null && k.RulingClan != null && k.RulingClan == Clan.PlayerClan; } catch { return false; }
        }

        /// <summary>Renly nie zyje: bohater ROT lord_1_9 martwy; bez bohatera (wyrejestrowany) - zakonczone RenlyDeathEvent. Pamiec na godzine gry.</summary>
        private static bool RenlyGone()
        {
            double now = CampaignTime.Now.ToHours;
            if (Math.Abs(now - _renlyAt) < 0.5) return _renlyGone;
            bool gone = false;
            Hero h = _renly;
            if (h == null && !_renlyLooked)
            {
                _renlyLooked = true;   // jak RotEventFx / StoryThrone: Hero.FindFirst (zywi i martwi), raz na sesje
                try { h = _renly = Hero.FindFirst(x => x != null && x.StringId == RenlyHeroId); } catch { h = null; }
            }
            if (h != null) gone = !h.IsAlive;
            else gone = Fabula.EventState("RenlyDeathEvent") == 2;
            _renlyAt = now; _renlyGone = gone;
            return gone;
        }

        /// <summary>Czy para moze byc w sojuszu wedlug kronik. why: "lista" (pary nie ma na liscie), "renly" (Stormlands + Reach po
        /// smierci Renly'ego), "renly-alive" (Korona + Reach za zycia Renly'ego).</summary>
        internal static bool Allowed(Kingdom a, Kingdom b, out string why)
        {
            why = null;
            if (a == null || b == null || a == b) return true;
            if (PlayerRules(a) || PlayerRules(b)) return true;
            string x = a.StringId ?? "", y = b.StringId ?? "";
            if (!Westeros.Contains(x) && !Westeros.Contains(y)) return true;
            if (Is(x, y, North, River)) return true;
            if (Is(x, y, Storm, Reach)) { if (!RenlyGone()) return true; why = "renly"; return false; }
            if (Is(x, y, Crown, Reach)) { if (RenlyGone()) return true; why = "renly-alive"; return false; }
            why = "lista";
            return false;
        }

        private static string WhyPl(string why)
        {
            return why == "renly" ? "Renly nie zyje - Reach z Renlym tylko za jego zycia"
                 : why == "renly-alive" ? "Renly zyje - Reach z Korona dopiero po jego smierci"
                 : "pary nie ma na liscie kronik";
        }

        private static TextObject ReasonText(string why)
        {
            string r = why == "renly" ? "The Reach stood with Renly Baratheon only while he lived."
                     : why == "renly-alive" ? "The Reach stands with Renly Baratheon while he lives."
                     : "No such alliance was made in the chronicles of the War of the Five Kings.";
            return new TextObject("{=!}An alliance cannot be formed due to:{newline}{newline}" + r);
        }

        /// <summary>Postfiks DefaultAllianceModel.CanMakeAlliance(kingdom, targetKingdom, evaluatingFaction, out reason, includeReason).
        /// Tylko gdy gra mowi TAK; przy wyjatku wynik gry bez zmian.</summary>
        public static void CanMakePostfix(Kingdom __0, Kingdom __1, ref bool __result, ref TextObject __3, bool __4)
        {
            if (!__result || !_ruleOn || !Config.CanonAlliances) return;
            try
            {
                string why;
                if (Allowed(__0, __1, out why)) return;
                __result = false;
                if (__4) __3 = ReasonText(why);
                string key = N(__0) + " + " + N(__1);
                int n; _dBlocked.TryGetValue(key, out n); _dBlocked[key] = n + 1; _tBlocked++;
            }
            catch (Exception e) { Stumble("CanMakePostfix", e); }
        }

        // ------------------------------------------------------------------ start sesji

        internal static void OnSession()
        {
            try { EnsureHooks(); } catch (Exception e) { Stumble("EnsureHooks", e); }
            try
            {
                string model = "?";
                try
                {
                    var am = Campaign.Current.Models.AllianceModel;
                    var t = am != null ? am.GetType() : null;
                    model = t != null ? t.FullName : "brak";
                    if (t != null && !typeof(DefaultAllianceModel).IsAssignableFrom(t)) model += " (NIE dziedziczy z DefaultAllianceModel - regula (2) NIE dziala)";
                    else
                        for (var b = t; b != null && b != typeof(DefaultAllianceModel); b = b.BaseType)
                            if (AccessTools.DeclaredMethod(b, "CanMakeAlliance") != null) { model += " (UWAGA: " + b.Name + " nadpisuje CanMakeAlliance - regula (2) moze nie dzialac)"; break; }
                }
                catch { }
                int rd = Fabula.EventState("RenlyDeathEvent");
                bool gone = RenlyGone();
                string pl = "?";
                try
                {
                    var pk = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                    pl = pk == null ? "bez krolestwa" : N(pk) + (PlayerRules(pk) ? " (gracz wladca - krolestwo poza regula)" : " (gracz nie wladca - regula obejmuje to krolestwo)");
                }
                catch { }
                Scribe.Line("Sojusze kanonu (SK): start sesji | (2) regula CanonAlliances " + (Config.CanonAlliances ? "TAK" : "NIE") + " - postfiks DefaultAllianceModel.CanMakeAlliance: " + _ruleState
                            + ", model sojuszy gry " + model
                            + " | (3) zrywanie przy wczytaniu " + (Config.CanonAlliances && Config.CanonAlliancesBreakOnLoad ? "TAK (pierwsza godzina)" : "NIE")
                            + " | (4) wojna Reach - Polnoc po smierci Renly'ego " + (Config.CanonReachNorthWar ? "TAK" : "NIE") + " - postfiks ROTStorylineWars.OnRenlysDeath: " + _renlyState
                            + " | Renly " + (gone ? "NIE ZYJE" : "zyje") + ", RenlyDeathEvent " + EvName(rd) + " | krolestwo gracza: " + pl
                            + " | dozwolone w Westeros: The North + Riverlands; Stormlands + The Reach (do smierci Renly'ego, teraz " + (gone ? "NIE" : "TAK")
                            + "); King's Landing + The Reach (po smierci Renly'ego, teraz " + (gone ? "TAK" : "NIE") + "); Stannis, Nocna Straz i reszta Westeros - zadne; Essos jak w grze; krolestwo gracza poza regula.");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static string EvName(int s) { return s == 0 ? "czeka" : s == 1 ? "trwa" : s == 2 ? "zakonczone" : "nie widac"; }

        private static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            if (_harmony == null) _harmony = new Harmony(HarmonyId);

            // (2) brama sojuszy - metoda deklarowana w DefaultAllianceModel (DeclaredMethod, pulapka 15.10), w kampanii
            if (!Config.CanonAlliances) _ruleState = "WYLACZONE (klucz)";
            else
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultAllianceModel), "CanMakeAlliance",
                    new[] { typeof(Kingdom), typeof(Kingdom), typeof(IFaction), typeof(TextObject).MakeByRefType(), typeof(bool) });
                if (m == null) _ruleState = "BRAK (DefaultAllianceModel.CanMakeAlliance)";
                else
                {
                    try { _harmony.Patch(m, postfix: new HarmonyMethod(typeof(CanonAlliances), nameof(CanMakePostfix))); _ruleOn = true; _ruleState = "wpiety"; }
                    catch (Exception e) { _ruleState = "BLAD " + e.GetType().Name; Stumble("Patch(CanMakeAlliance)", e); }
                }
            }

            // ROT: wojny fabularne (do (4) i do nadrabiania)
            try
            {
                var tWars = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWars");
                var tWar = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWar");
                var tId = AccessTools.TypeByName("ROT.CampaignBehaviors.WarID");
                if (tWars != null && tWar != null && tId != null && tId.IsEnum)
                {
                    var tSub = tWars.Assembly.GetType("ROT.SubModule");
                    _fSubWars = tSub != null ? AccessTools.Field(tSub, "StorylineWars") : null;
                    _mGetWar = AccessTools.DeclaredMethod(tWars, "GetWar", new[] { tId });
                    _mTrigger = AccessTools.DeclaredMethod(tWar, "Trigger", new[] { typeof(bool) });
                    _pTriggered = AccessTools.Property(tWar, "IsTriggered");
                    _pF1 = AccessTools.Property(tWar, "Faction1");
                    _pF2 = AccessTools.Property(tWar, "Faction2");
                    _pFlavor = AccessTools.Property(tWar, "FlavorText");
                    _fReachAllied = AccessTools.Field(tWars, "_reachAlliedRenly");
                    if (Enum.IsDefined(tId, "FIVE_KINGS_REACH_NORTH")) _idReachNorth = Enum.Parse(tId, "FIVE_KINGS_REACH_NORTH");
                    if (Enum.IsDefined(tId, "FIVE_KINGS_REACH_RIVERLANDS")) _idReachRiver = Enum.Parse(tId, "FIVE_KINGS_REACH_RIVERLANDS");
                    _rotOk = _mGetWar != null && _mTrigger != null && _pTriggered != null && _pF1 != null && _pF2 != null && _idReachNorth != null;
                }
                if (!Config.CanonReachNorthWar) _renlyState = "WYLACZONE (klucz)";
                else if (!_rotOk) _renlyState = "BRAK (ROT: ROTStorylineWars.GetWar / ROTStorylineWar.Trigger / WarID.FIVE_KINGS_REACH_NORTH)";
                else
                {
                    var mR = AccessTools.DeclaredMethod(tWars, "OnRenlysDeath");
                    if (mR == null) _renlyState = "BRAK (ROTStorylineWars.OnRenlysDeath) - zostaje nadrabianie w dobie";
                    else
                    {
                        try { _harmony.Patch(mR, postfix: new HarmonyMethod(typeof(CanonAlliances), nameof(RenlyPostfix))); _renlyState = "wpiety"; }
                        catch (Exception e) { _renlyState = "BLAD " + e.GetType().Name + " - zostaje nadrabianie w dobie"; Stumble("Patch(OnRenlysDeath)", e); }
                    }
                }
            }
            catch (Exception e) { _renlyState = "BLAD " + e.GetType().Name; Stumble("ResolveRot", e); }
        }

        // ------------------------------------------------------------------ (3) zrywanie przy wczytaniu

        /// <summary>Pierwsza godzina po wczytaniu: sojusze AI spoza listy zerwane (raz na sesje).</summary>
        internal static void Hourly()
        {
            if (_cleanDone) return;
            _cleanDone = true;
            try
            {
                if (!Config.CanonAlliances || !Config.CanonAlliancesBreakOnLoad) return;
                if (!_ruleOn) { Scribe.Line("Sojusze kanonu (SK): zrywanie przy wczytaniu POMINIETE - regula (2) nie wpieta (" + _ruleState + "), sojusz zawiazalby sie od nowa."); return; }
                Cleanup("pierwsza godzina po wczytaniu");
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static void Cleanup(string when)
        {
            var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>() : null;
            if (beh == null) { Scribe.Line("Sojusze kanonu (SK): " + when + " - brak AllianceCampaignBehavior, nic nie zrywam."); return; }
            var ks = Kingdom.All.Where(k => k != null && !k.IsEliminated).ToList();
            int pairs = 0, off = 0, ended = 0;
            var kept = new List<string>();
            for (int i = 0; i < ks.Count; i++)
                for (int j = i + 1; j < ks.Count; j++)
                {
                    var a = ks[i]; var b = ks[j];
                    bool ally = false;
                    try { ally = beh.IsAllyWithKingdom(a, b); } catch { }
                    if (!ally) continue;
                    pairs++;
                    string why;
                    if (Allowed(a, b, out why)) { kept.Add(N(a) + " + " + N(b)); continue; }
                    off++;
                    try { if (EndPair(beh, a, b, why)) ended++; }
                    catch (Exception e) { Stumble("EndPair", e); }
                }
            Scribe.Line("Sojusze kanonu (SK): " + when + " - sojuszy " + pairs + ", poza kanonem " + off + ", zerwanych " + ended
                        + (kept.Count > 0 ? "; zostaja (kanon albo krolestwo gracza): " + string.Join(", ", kept.ToArray()) : "") + ".");
        }

        private static bool EndPair(IAllianceCampaignBehavior beh, Kingdom a, Kingdom b, string why)
        {
            int endDay = -1;
            try { endDay = DayOf(beh.GetAllianceEndDate(a, b)); } catch { }
            var calls = new List<string>();
            foreach (var dir in new[] { new[] { a, b }, new[] { b, a } })
            {
                List<Kingdom> targets = null;
                try { targets = beh.GetKingdomsToCallToWarAgainst(dir[0], dir[1]); } catch { }
                if (targets == null) continue;
                foreach (var t in targets.ToList())
                {
                    if (t == null) continue;
                    bool war = false;
                    try { war = dir[1].IsAtWarWith(t); } catch { }
                    // przez interfejs: latki Armoury (CrownCallToWar.EndPostfix, Measure31 EndFin) widza kazde porozumienie
                    beh.EndCallToWarAgreement(dir[0], dir[1], t);
                    calls.Add(N(dir[0]) + "->" + N(dir[1]) + " przeciw " + N(t) + (war ? " (wezwany dalej w wojnie - wojna zostaje)" : " (bez wojny)"));
                }
            }
            beh.EndAlliance(a, b);
            bool still = true;
            try { still = beh.IsAllyWithKingdom(a, b); } catch { }
            Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - ZERWANY sojusz " + N(a) + " + " + N(b) + " (" + WhyPl(why)
                        + (endDay >= 0 ? "; trwalby do dnia " + endDay : "") + ") - EndAlliance gry, bez kary relacji; porozumienia wezwan zamkniete "
                        + calls.Count + (calls.Count > 0 ? ": " + string.Join(", ", calls.ToArray()) : "")
                        + (still ? " | UWAGA: sojusz dalej istnieje po EndAlliance!" : "") + ".");
            return !still;
        }

        // ------------------------------------------------------------------ (4) wojna Reach - Polnoc

        /// <summary>Postfiks ROTStorylineWars.OnRenlysDeath - ten sam moment, w ktorym ROT wlacza FIVE_KINGS_REACH_RIVERLANDS.</summary>
        public static void RenlyPostfix(object __instance)
        {
            try
            {
                if (!Config.CanonReachNorthWar || !_rotOk || __instance == null) return;
                TriggerReachNorth(__instance, "ROT OnRenlysDeath");
            }
            catch (Exception e) { Stumble("RenlyPostfix", e); }
        }

        private static object GetWar(object wars, object id)
        {
            return wars != null && id != null ? _mGetWar.Invoke(wars, new[] { id }) : null;
        }

        private static void TriggerReachNorth(object wars, string src)
        {
            if (_rnDone) return;
            // raz na sesje (_rnDone przy kazdym rozstrzygnieciu; wyjatek - bez znacznika, nadrabianie w dobie sprobuje znowu); stan Trigger zostaje w zapisie ROT
            object war = GetWar(wars, _idReachNorth);
            if (war == null) { _rnDone = true; Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - wojny ROT FIVE_KINGS_REACH_NORTH brak na liscie (ROT skresla wojne zniszczonego krolestwa) - nic do wlaczenia (" + src + ")."); return; }
            var f1 = _pF1.GetValue(war, null) as IFaction;
            var f2 = _pF2.GetValue(war, null) as IFaction;
            string names = NF(f1) + " - " + NF(f2);
            if (Convert.ToBoolean(_pTriggered.GetValue(war, null))) { _rnDone = true; Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - wojna ROT FIVE_KINGS_REACH_NORTH (" + names + ") juz wlaczona - bez zmian (" + src + ")."); return; }
            if (f1 == null || f2 == null || f1.IsEliminated || f2.IsEliminated) { _rnDone = true; Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - wojna ROT FIVE_KINGS_REACH_NORTH (" + names + ") NIE wlaczona - krolestwo nie istnieje (" + src + ")."); return; }
            _mTrigger.Invoke(war, new object[] { true });
            _rnDone = true;
            try { var txt = _fReachAllied != null ? _fReachAllied.GetValue(wars) as TextObject : null; if (txt != null && _pFlavor != null) _pFlavor.SetValue(war, txt, null); } catch { }
            bool atWar = false;
            try { atWar = FactionManager.IsAtWarAgainstFaction(f1, f2); } catch { }
            if (!atWar) { _rnA = f1; _rnB = f2; _rnDays = 0; }
            Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - wojna fabularna ROT FIVE_KINGS_REACH_NORTH (" + names + ") WLACZONA (" + src
                        + ") - jak FIVE_KINGS_REACH_RIVERLANDS przy smierci Renly'ego; " + (atWar ? "strony juz w wojnie - ROT jej pilnuje (pokoj zablokowany do Krwawych Godow)"
                        : "wypowie ja ROT w najblizszym EnforceWars (DeclareWarAction.ApplyByDefault), potem pilnuje do Krwawych Godow") + ".");
        }

        /// <summary>Nadrobienie w dobie: RenlyDeath zakonczone, ROT przeszedl OnRenlysDeath (Reach - Dorzecze wlaczona), Krwawe Gody
        /// jeszcze nie odpalily, a Reach - Polnoc wylaczona (zapis sprzed paczki albo latka OnRenlysDeath nie weszla).</summary>
        private static void CatchUp()
        {
            if (!Config.CanonReachNorthWar || !_rotOk || _rnDone || _fSubWars == null || _idReachRiver == null) return;
            if (Fabula.EventState("RenlyDeathEvent") != 2) return;
            if (Fabula.EventState("RedWeddingEvent") != 0) return;   // gody byly, trwaja albo nie widac - ROT moglo wojne wylaczyc
            object wars = _fSubWars.GetValue(null);
            if (wars == null) return;
            object river = GetWar(wars, _idReachRiver);
            if (river == null || !Convert.ToBoolean(_pTriggered.GetValue(river, null))) return;
            object north = GetWar(wars, _idReachNorth);
            if (north == null || Convert.ToBoolean(_pTriggered.GetValue(north, null))) { _rnDone = true; return; }   // juz wlaczona (albo skreslona) - nic do nadrobienia w tej sesji
            TriggerReachNorth(wars, "nadrobione w dobie: RenlyDeath zakonczone, ROT wlaczyl Reach - Dorzecze, Krwawe Gody nie odpalily");
        }

        private static void WatchWar()
        {
            if (_rnA == null || _rnB == null) return;
            _rnDays++;
            bool atWar = false;
            try { atWar = FactionManager.IsAtWarAgainstFaction(_rnA, _rnB); } catch { }
            if (atWar)
            {
                Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - wojna " + NF(_rnA) + " - " + NF(_rnB) + " TRWA (wypowiedziana po wlaczeniu FIVE_KINGS_REACH_NORTH, doba " + _rnDays + ").");
                _rnA = _rnB = null;
            }
            else if (_rnDays >= 3)
            {
                Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - wojna " + NF(_rnA) + " - " + NF(_rnB) + " NIE wypowiedziana 3 doby po wlaczeniu FIVE_KINGS_REACH_NORTH"
                            + ((_rnA.IsEliminated || _rnB.IsEliminated) ? " (krolestwo zniszczone)" : " - ROT EnforceWars jej nie wypowiedzial (sprawdz linie FABULA i ROT)") + ".");
                _rnA = _rnB = null;
            }
        }

        // ------------------------------------------------------------------ doba

        internal static void Daily()
        {
            try { CatchUp(); } catch (Exception e) { Stumble("CatchUp", e); }
            try { WatchWar(); } catch (Exception e) { Stumble("WatchWar", e); }
            try
            {
                if (_dBlocked.Count > 0 || _stumblesDay > 0)
                    Scribe.Line("Sojusze kanonu (SK): dzien " + Day() + " - zablokowane proby sojuszu (CanMakeAlliance) dzis " + _dBlocked.Values.Sum()
                                + (_dBlocked.Count > 0 ? " (" + string.Join(", ", _dBlocked.OrderByDescending(p => p.Value).Take(10).Select(p => p.Key + " x" + p.Value).ToArray()) + ")" : "")
                                + ", od wczytania " + _tBlocked + (_stumblesDay > 0 ? "; potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ", pierwsze w raporcie; przy potknieciu wynik gry bez zmian)" : "") + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dBlocked.Clear(); _stumblesDay = 0;
        }

        // ------------------------------------------------------------------ (1) czesc "SOJUSZE" kroniki wojen

        /// <summary>Kazda para sojuszu: dzien konca, porozumienia wezwan (wzywajacy->wezwany przeciw komu, "bez wojny" gdy wezwany juz z nim
        /// nie walczy), przy regule - znacznik "poza kanonem". Tylko odczyt. Bez " vs " i bez srednikow poza granica par - parsery kroniki
        /// (tools/sprawdz_logi.py: _kron_set, SD_KW) licza tylko czesci z " vs ".</summary>
        internal static string AlliancesPart()
        {
            try
            {
                var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>() : null;
                if (beh == null) return "SOJUSZE: ? (brak AllianceCampaignBehavior)";
                var ks = Kingdom.All.Where(k => k != null && !k.IsEliminated).ToList();
                var parts = new List<string>();
                for (int i = 0; i < ks.Count; i++)
                    for (int j = i + 1; j < ks.Count; j++)
                    {
                        var a = ks[i]; var b = ks[j];
                        if (!beh.IsAllyWithKingdom(a, b)) continue;
                        var sb = new StringBuilder();
                        sb.Append(N(a)).Append(" + ").Append(N(b)).Append(" (do dnia ").Append(DayOf(beh.GetAllianceEndDate(a, b)));
                        int c = 0;
                        foreach (var dir in new[] { new[] { a, b }, new[] { b, a } })
                        {
                            var targets = beh.GetKingdomsToCallToWarAgainst(dir[0], dir[1]);
                            if (targets == null) continue;
                            foreach (var t in targets)
                            {
                                if (t == null) continue;
                                sb.Append(c++ == 0 ? ", wezwania: " : ", ").Append(N(dir[0])).Append("->").Append(N(dir[1])).Append(" przeciw ").Append(N(t));
                                if (!dir[1].IsAtWarWith(t)) sb.Append(" bez wojny");
                            }
                        }
                        sb.Append(")");
                        string why;
                        if (Config.CanonAlliances && !Allowed(a, b, out why)) sb.Append(" [poza kanonem]");
                        parts.Add(sb.ToString());
                    }
                return "SOJUSZE: " + (parts.Count == 0 ? "brak" : string.Join("; ", parts.ToArray()));
            }
            catch (Exception e) { Stumble("AlliancesPart", e); return "SOJUSZE: ? (blad odczytu)"; }
        }

        // ------------------------------------------------------------------ pomocnicze

        private static int DayOf(CampaignTime t)
        {
            try { return (int)(t - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; } catch { return -1; }
        }

        private static int Day() { try { return DayOf(CampaignTime.Now); } catch { return -1; } }

        private static string N(Kingdom k)
        {
            try { return k == null ? "?" : (k.Name != null ? k.Name.ToString() : k.StringId); } catch { return "?"; }
        }

        private static string NF(IFaction f)
        {
            try { return f == null ? "?" : (f.Name != null ? f.Name.ToString() : f.StringId); } catch { return "?"; }
        }
    }

    /// <summary>f19-kanon-a: wpiecie latek przy starcie sesji, zrywanie sojuszy spoza kanonu w pierwszej godzinie, linia doby
    /// "Sojusze kanonu (SK)" (tylko gdy cos zablokowano), nadrabianie wojny Reach - Polnoc. Bez zapisu.</summary>
    internal sealed class CanonAlliancesBehavior : CampaignBehaviorBase
    {
        public CanonAlliancesBehavior() { CanonAlliances.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { CanonAlliances.OnSession(); });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, CanonAlliances.Hourly);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, CanonAlliances.Daily);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
