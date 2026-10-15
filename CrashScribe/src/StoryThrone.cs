using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>
    /// TRON FABULARNY (15.10, test 6-letni: "Denerys napraw"). Dowod:
    ///  - log Armoury "Skarbce": House Targaryen krol Targaryen -> Selmy, Barristan w dobie 109202 (d366) i z powrotem w 109566 (d730) - w tej samej
    ///    dobie co wszystkie 8 republik (Lorath, Volantis, Braavos, Pentos, Norvos, Myr, Lys, Tyrosh); w d2 zmienily sie tylko republiki. Okna
    ///    Diplomacy "A Monarch Abdicates ... With the election of a new monarch" (CivilWarBehavior.NewKing po KingSelectionKingdomDecision) w tych dobach.
    ///  - BK BKRepublicBehavior: w dniu roku 1 kazde krolestwo z ustrojem Republic dostaje RepublicElectionDecision(RulingClan, clanToExclude:
    ///    RulingClan); kandydaci (SuccessionRegistry "Republic") - glowy rodow z pelnym parostwem BEZ obecnego wladcy, wynik BKKingElectionDecision
    ///    -> KingdomActions.SetRulerWithTitle (tez tytul krolestwa). Stad naprzemiennie Daenerys / Barristan co rok 364 dob.
    ///  - empire_s w naszym pliku tytulow: Imperial / Dictatorship / Seniority / Cognatic. Zapis autotest-6lat-d720: kontraktow Republic/Republic/
    ///    Seniority/Cognatic 9 (plik: 8 republik), Imperial/Dictatorship/Seniority - 0. Czyli BK (EnablePoliticsRework, BKDiplomacyBehavior
    ///    .ConsiderGovernmentTransition -> ChangeRealmGovernment, Imperial -> Republic: lojalnosc legionow < 12 albo populisci przy minimum Crown
    ///    Authority) zmienil ustroj House Targaryen w pierwszym roku (dokladnej doby nie ma - LogPolitics BK wylaczony).
    ///  - ROT nie ma regencji Barristana ani zniknecia Daenerys (ROT.Events; DanyInvasion czeka); ROT wrecz premiuje rod panujacy w wyborze krola
    ///    (KingdomDecisionPatches x999), czego BKKingElectionDecision nie uzywa. Wydarzenia ROT zmieniaja wladce tylko po smierci wladcy ze startu
    ///    (JonEvent - Robb martwy, JeorMutiny - Jeor zabity, BastardsEvent - rod Stark).
    ///  - Bez tronu rod Daenerys byl zwyklym wasalem: bunt grupy radykalnej BK -> S9 (Mends.BkSecede, LeaveWithRebellion) w d1243 z Meereen i Vaes Efe.
    /// Naprawa (klucz StoryThroneGuard, domyslnie true) - monarchie ROT (ustroj w naszym pliku tytulow inny niz Republic; wladca ze startu = owner
    /// krolestwa z XML ROT, ten sam odczyt co tools/bk_titles_rot818.py), dopoki wladca ze startu zyje i rzadzi:
    ///  (1) prefiks BKDiplomacyBehavior.ConsiderGovernmentTransition - bez zmian ustroju i uzurpacji BK (Imperial -> Republic, UsurpRealm);
    ///  (2) prefiks Kingdom.AddDecision - bez wymuszonego wyboru wladcy: BK RepublicElectionDecision (wybory republiki) i KingSelectionKingdomDecision
    ///      z wnioskiem rodu innego niz panujacy (abdykacja po wojnie domowej Diplomacy), chyba ze wnioskuje rod gracza. Wybor zglaszany przez rod panujacy
    ///      przechodzi - to sukcesja po smierci wladcy (BK SuccessionHelper.ResolveSuccession w BeforeHeroKilledEvent i gra KillCharacterAction wola
    ///      AddDecision, zanim bohater jest martwy - recenzja 15.10);
    ///  (3) doba: wladca ze startu zywy, wolny, glowa rodu, rod w swoim krolestwie, a tron ma inny rod (stary zapis, droga spoza 1-2) - powrot na tron
    ///      przez BK KingdomActions.SetRulerWithTitle (z tytulem), bez BK - ChangeRulingClanAction; rodu gracza na tronie nie ruszamy; tronu oddanego
    ///      przez fabule ROT nie cofamy (RotHandover: JonEvent daje Nocna Straz Halfhandowi bez zabicia Jeora); nowy wladca wychodzi z frakcji buntu
    ///      Diplomacy swojego krolestwa (jak Diplomacy CivilWarBehavior.NewKing po wyborze krola);
    ///  (4) S9: rod wladcy ze startu nie odchodzi ze swojego krolestwa w secesji BK (Mends.BkSecede).
    /// Republiki z pliku (Wolne Miasta, Qohor) - bez zmian (wybory co rok to zamysl BK i pliku tytulow). Bez zapisu w save.
    /// </summary>
    internal static class StoryThrone
    {
        private const string HarmonyId = "com.jeff.crashscribe";

        private sealed class Seat
        {
            public string Kingdom;
            public string Ruler;
            public Hero HeroRef;   // pamiec bohatera w tej kampanii (czyszczona w ResetSession - po wczytaniu obiekty sa nowe)
            public string Gov;
        }

        private static List<Seat> _seats;
        private static Dictionary<string, Seat> _byKingdom;
        private static int _republics;
        private static string _seatsNote = "nie czytane";
        private static bool _installed;
        private static string _state = "nie wpiete";
        private static int _stumbles;
        private static int _dGov, _dVote, _dBack, _dS9, _tGov, _tVote, _tBack, _tS9;
        private static MethodInfo _bkSetRuler;
        private const int MaxBackPerSession = 3;   // powrotow na tron na krolestwo w sesji - straz przed przepychanka z inna droga (potem linia i spokoj)
        private static readonly Dictionary<string, int> _backTries = new Dictionary<string, int>();
        private static bool _bkSetRulerResolved;
        private static bool _dipResolved;
        private static MethodInfo _dipGet, _dipRemove;
        private static PropertyInfo _dipClans;

        // Recenzja 15.10: ROT sam oddaje tron innemu rodowi (ROT.Events, ChangeRulingClanAction): Nocna Straz - JeorMutinyEvent -> Thorne (ROTclan_125, Jeor
        // ginie w tej samej chwili), JonEvent -> Halfhand (clan_nord_3) BEZ zabicia Jeora (gdy JeorMutiny nie doszlo); Polnoc - JonEvent -> Snow (clan_battania_6,
        // warunek: Robb martwy). Taki tron zostaje - powrot na tron go nie cofa (fabula ROT), linia doby liczy go osobno.
        private static readonly Dictionary<string, string[]> RotHandover = new Dictionary<string, string[]>
        {
            { "nightswatch", new[] { "clan_nord_3", "ROTclan_125" } },
            { "battania", new[] { "clan_battania_6" } },
        };

        private static bool IsRotHandover(Kingdom k)
        {
            string[] ids;
            if (k == null || k.RulingClan == null || !RotHandover.TryGetValue(k.StringId, out ids)) return false;
            return Array.IndexOf(ids, k.RulingClan.StringId) >= 0;
        }

        /// <summary>BK RepublicElectionDecision (BKRepublicBehavior, dzien roku 1) - po nazwie typu, bez odwolania do BK.</summary>
        private static bool IsBkRepublicVote(KingdomDecision d)
        {
            for (var t = d.GetType(); t != null; t = t.BaseType)
                if (t.FullName == "BannerKings.Managers.Kingdoms.Succession.RepublicElectionDecision") return true;
            return false;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5 || _stumbles % 100 == 0) { try { Scribe.Report("CrashScribe", e, "StoryThrone." + where + " (potkniecie " + _stumbles + ")", null); } catch { } }
        }

        internal static void ResetSession()
        {
            _dGov = _dVote = _dBack = _dS9 = 0; _tGov = _tVote = _tBack = _tS9 = 0; _backTries.Clear();
            if (_seats != null) foreach (var s in _seats) s.HeroRef = null;
        }

        private static int Day() { return (int)CampaignTime.Now.ToDays; }

        private static string Txt(object o) { return o != null ? o.ToString() : "-"; }

        // ------------------------------------------------------------ wladcy ze startu (raz na proces)
        private static void ReadSeats()
        {
            if (_seats != null) return;
            _seats = new List<Seat>();
            _byKingdom = new Dictionary<string, Seat>();
            var gov = new Dictionary<string, string>();
            try
            {
                using (var s = typeof(StoryThrone).Assembly.GetManifestResourceStream(BkTitles.Resource))
                {
                    if (s != null)
                        using (var r = new StreamReader(s, Encoding.UTF8))
                            foreach (Match m in Regex.Matches(r.ReadToEnd(), "<kingdom\\b([^>]*)>"))
                            {
                                var f = Regex.Match(m.Groups[1].Value, "\\bfaction=\"([^\"]+)\"");
                                var g = Regex.Match(m.Groups[1].Value, "\\bgovernment=\"([^\"]+)\"");
                                if (f.Success && g.Success) gov[f.Groups[1].Value] = g.Groups[1].Value;
                            }
                }
            }
            catch (Exception e) { Stumble("ReadSeats.titles", e); }
            // owner krolestw jak tools/bk_titles_rot818.py: SandBox, potem ROT-Content (xslt i ROT_spkingdoms.xml) - pozniejszy wygrywa
            var owner = new Dictionary<string, string>();
            int files = 0;
            foreach (var rel in new[] { "Modules/SandBox/ModuleData/spkingdoms.xml", "Modules/ROT-Content/ModuleData/spkingdoms.xslt",
                                        "Modules/ROT-Content/ModuleData/kingdoms.xslt", "Modules/ROT-Content/ModuleData/ROT_spkingdoms.xml" })
            {
                try
                {
                    var p = BasePath.Name + rel;
                    if (!File.Exists(p)) continue;
                    files++;
                    foreach (Match m in Regex.Matches(File.ReadAllText(p), "<Kingdom\\s+id=\"([^\"]+)\"([^>]*)>"))
                    {
                        var o = Regex.Match(m.Groups[2].Value, "owner=\"Hero\\.([^\"]+)\"");
                        if (o.Success) owner[m.Groups[1].Value] = o.Groups[1].Value;
                    }
                }
                catch (Exception e) { Stumble("ReadSeats." + rel, e); }
            }
            foreach (var kv in gov)
            {
                if (string.Equals(kv.Value, "Republic", StringComparison.OrdinalIgnoreCase)) { _republics++; continue; }
                string h;
                if (!owner.TryGetValue(kv.Key, out h) || string.IsNullOrEmpty(h)) continue;
                var seat = new Seat { Kingdom = kv.Key, Ruler = h, Gov = kv.Value };
                _seats.Add(seat);
                _byKingdom[kv.Key] = seat;
            }
            _seatsNote = "plik tytulow: krolestw " + gov.Count + ", monarchii z wladca ROT " + _seats.Count + ", republik bez ochrony " + _republics
                         + "; XML ROT: plikow " + files + ", wladcow " + owner.Count;
        }

        // 15.10 (Jeff "tron Daenerys napraw"): MBObjectManager.GetObject<Kingdom> dawal null w kazdej kampanii (linia doby: "krolestwo upadlo 20" z 20,
        // takze w nowej kampanii fin42) - ochrona nie dzialala wcale. Szukamy jak gra: Kingdom.All po StringId, bohater Hero.FindFirst (z pamiecia na kampanie).
        private static Kingdom KingdomOf(Seat s)
        {
            try
            {
                var all = Kingdom.All;
                if (all != null) foreach (var k in all) if (k != null && k.StringId == s.Kingdom) return k;
            }
            catch { }
            return null;
        }

        private static Hero HeroOf(Seat s)
        {
            try
            {
                if (s.HeroRef != null && s.HeroRef.StringId == s.Ruler) return s.HeroRef;
                var id = s.Ruler;
                var h = Hero.FindFirst(x => x != null && x.StringId == id);
                s.HeroRef = h;
                return h;
            }
            catch { return null; }
        }

        /// <summary>Wladca ze startu tego krolestwa, gdy zyje i rzadzi (glowa rodu panujacego, rod w krolestwie); inaczej null.</summary>
        private static Hero RulingStartRuler(Kingdom k)
        {
            if (k == null || _byKingdom == null) return null;
            Seat s;
            if (!_byKingdom.TryGetValue(k.StringId, out s)) return null;
            var h = HeroOf(s);
            if (h == null || !h.IsAlive || h.Clan == null || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) return null;
            if (h.Clan.Kingdom != k || k.RulingClan != h.Clan || h.Clan.Leader != h) return null;
            return h;
        }

        /// <summary>S9 (Mends.BkSecede): rod zywego wladcy ze startu nie odchodzi ze swojego krolestwa w secesji BK.</summary>
        internal static bool ShieldsClan(Clan c, Kingdom original)
        {
            try
            {
                if (!Config.StoryThroneGuard || c == null || original == null || _byKingdom == null) return false;
                Seat s;
                if (!_byKingdom.TryGetValue(original.StringId, out s)) return false;
                var h = HeroOf(s);
                if (h == null || !h.IsAlive || h.Clan != c) return false;
                _dS9++; _tS9++;
                Scribe.Line("Tron fabularny (DY): secesja S9 pominieta - dzien " + Day() + " | " + Txt(original.Name) + " (" + original.StringId + ") | rod " + Txt(c.Name) + " (" + c.StringId
                            + ") wladcy ze startu " + Txt(h.Name) + " (" + h.StringId + ") zostaje w swoim krolestwie.");
                return true;
            }
            catch (Exception e) { Stumble("ShieldsClan", e); return false; }
        }

        // ------------------------------------------------------------ wpiecie (raz na proces) i linia startu sesji
        internal static void OnSession()
        {
            try { ReadSeats(); } catch (Exception e) { Stumble("ReadSeats", e); }
            try { Install(); } catch (Exception e) { _state = "wyjatek " + e.GetType().Name; Stumble("Install", e); }
            try
            {
                Scribe.Line("Tron fabularny (DY): start sesji | " + _state + " | klucz StoryThroneGuard " + (Config.StoryThroneGuard ? "TAK" : "NIE") + " | " + _seatsNote
                            + " | " + Census() + ".");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            if (!Config.StoryThroneGuard) { _state = "WYLACZONE (klucz)"; return; }
            if (_seats == null || _seats.Count == 0) { _state = "BRAK wladcow ze startu (plik tytulow / XML ROT) - nic nie wpiete"; return; }
            var h = new Harmony(HarmonyId);
            int ok = 0, want = 0;
            var tDip = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.BKDiplomacyBehavior");
            var mGov = tDip != null ? AccessTools.Method(tDip, "ConsiderGovernmentTransition", new[] { typeof(Kingdom) }) : null;
            if (mGov != null)
            {
                want++;
                try { h.Patch(mGov, prefix: new HarmonyMethod(typeof(StoryThrone), nameof(GovPrefix)) { priority = Priority.First }); ok++; }
                catch (Exception e) { Stumble("Patch(ConsiderGovernmentTransition)", e); }
            }
            var mAdd = AccessTools.Method(typeof(Kingdom), "AddDecision", new[] { typeof(KingdomDecision), typeof(bool) });
            if (mAdd != null)
            {
                want++;
                try { h.Patch(mAdd, prefix: new HarmonyMethod(typeof(StoryThrone), nameof(AddDecisionPrefix)) { priority = Priority.First }); ok++; }
                catch (Exception e) { Stumble("Patch(Kingdom.AddDecision)", e); }
            }
            _state = "wpiete " + ok + "/" + want + (tDip == null ? " (BRAK BannerKings - tylko wybory / abdykacje)" : (mGov == null ? " (BRAK BKDiplomacyBehavior.ConsiderGovernmentTransition)" : ""));
        }

        /// <summary>Prefiks BK BKDiplomacyBehavior.ConsiderGovernmentTransition: monarchia ROT z rzadzacym wladca ze startu - bez zmian ustroju i uzurpacji.</summary>
        public static bool GovPrefix(Kingdom __0)
        {
            try
            {
                if (!Config.StoryThroneGuard || RulingStartRuler(__0) == null) return true;
                _dGov++; _tGov++;
                return false;
            }
            catch (Exception e) { Stumble("GovPrefix", e); return true; }
        }

        /// <summary>Prefiks Kingdom.AddDecision: wymuszony wybor wladcy w monarchii ROT, w ktorej rzadzi zywy wladca ze startu - pominiety (wybory republiki BK,
        /// wniosek rodu innego niz panujacy, np. abdykacja Diplomacy; nie: wniosek rodu gracza i rodu panujacego - sukcesja po smierci wladcy).</summary>
        public static bool AddDecisionPrefix(Kingdom __instance, KingdomDecision __0)
        {
            try
            {
                if (!Config.StoryThroneGuard || !(__0 is KingSelectionKingdomDecision)) return true;
                var k = __0.Kingdom ?? __instance;
                var h = RulingStartRuler(k);
                if (h == null) return true;
                var prop = __0.ProposerClan;
                if (prop != null && prop == Clan.PlayerClan) return true;
                bool republic = IsBkRepublicVote(__0);
                if (!republic && prop != null && prop == k.RulingClan) return true;   // recenzja: sukcesja po smierci wladcy (BK BeforeHeroKilled, gra KillCharacterAction) - jak dotad
                _dVote++; _tVote++;
                Scribe.Line("Tron fabularny (DY): wybor wladcy zablokowany - dzien " + Day() + " | " + Txt(k.Name) + " (" + k.StringId + ") | " + __0.GetType().Name
                            + (republic ? " (wybory republiki BK)" : " (wniosek rodu spoza tronu, np. abdykacja Diplomacy)") + ", wnioskuje "
                            + (prop != null ? Txt(prop.Name) + " (" + prop.StringId + ")" : "-") + " | wladca ze startu " + Txt(h.Name) + " (" + h.StringId + ") zostaje na tronie.");
                return false;
            }
            catch (Exception e) { Stumble("AddDecisionPrefix", e); return true; }
        }

        // ------------------------------------------------------------ doba: powrot na tron i linia
        internal static void Daily()
        {
            if (!Config.StoryThroneGuard || _seats == null) return;
            try { Restore(); } catch (Exception e) { Stumble("Restore", e); }
            try
            {
                Scribe.Line("Tron fabularny (DY): dzien " + Day() + " | " + Probe() + " | " + Census() + " | dzis: zmiany ustroju BK pominiete (wywolania) " + _dGov + ", wybory wladcy zablokowane "
                            + _dVote + ", powroty na tron " + _dBack + ", secesje S9 pominiete " + _dS9 + " | od wczytania: " + _tGov + " / " + _tVote + " / " + _tBack + " / " + _tS9
                            + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dGov = _dVote = _dBack = _dS9 = 0;
        }

        private static void Restore()
        {
            foreach (var s in _seats)
            {
                try
                {
                    var k = KingdomOf(s);
                    var h = HeroOf(s);
                    if (k == null || k.IsEliminated || h == null || !h.IsAlive || h.IsPrisoner || h.Clan == null || h.Clan.IsEliminated) continue;
                    if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) continue;
                    if (h.Clan.Kingdom != k || h.Clan.Leader != h || h.Clan.IsUnderMercenaryService) continue;
                    if (k.RulingClan == h.Clan) continue;
                    if (k.RulingClan == Clan.PlayerClan || h.Clan == Clan.PlayerClan) continue;
                    if (IsRotHandover(k)) continue;   // recenzja: tron oddany przez fabule ROT (JonEvent - Halfhand w Nocnej Strazy) zostaje
                    int tries;
                    _backTries.TryGetValue(s.Kingdom, out tries);
                    if (tries >= MaxBackPerSession)
                    {
                        if (tries == MaxBackPerSession)
                            Scribe.Line("Tron fabularny (DY): powrot na tron WSTRZYMANY - dzien " + Day() + " | " + Txt(k.Name) + " (" + k.StringId + ") | " + MaxBackPerSession
                                        + " powroty w tej sesji, a tron znow ma " + (k.Leader != null ? Txt(k.Leader.Name) + " (" + k.Leader.StringId + ")" : "-")
                                        + " - inna droga zmienia wladce; do konca sesji bez dalszych prob (linia doby pokazuje stan).");
                        _backTries[s.Kingdom] = tries + 1;
                        continue;
                    }
                    _backTries[s.Kingdom] = tries + 1;
                    var prev = k.Leader;
                    var prevClan = k.RulingClan;
                    string how = SetRuler(h, k);
                    bool done = k.RulingClan == h.Clan;
                    if (done) { _dBack++; _tBack++; how += "; " + DropFromRebelFactions(k, h.Clan); }
                    Scribe.Line("Tron fabularny (DY): " + (done ? "powrot na tron" : "powrot na tron NIEUDANY") + " - dzien " + Day() + " | " + Txt(k.Name) + " (" + k.StringId + ") | "
                                + (prev != null ? Txt(prev.Name) + " (" + prev.StringId + ")" : "-") + ", rod " + (prevClan != null ? prevClan.StringId : "-") + " -> wladca ze startu "
                                + Txt(h.Name) + " (" + h.StringId + ", rod " + h.Clan.StringId + ") | " + how + ".");
                }
                catch (Exception e) { Stumble("Restore." + s.Kingdom, e); }
            }
        }

        private static string SetRuler(Hero h, Kingdom k)
        {
            if (!_bkSetRulerResolved)
            {
                _bkSetRulerResolved = true;
                var t = AccessTools.TypeByName("BannerKings.Actions.KingdomActions");
                _bkSetRuler = t != null ? AccessTools.Method(t, "SetRulerWithTitle", new[] { typeof(Hero), typeof(Kingdom) }) : null;
            }
            string note = null;
            if (_bkSetRuler != null)
            {
                try { _bkSetRuler.Invoke(null, new object[] { h, k }); note = "BK KingdomActions.SetRulerWithTitle (tron i tytul krolestwa)"; }
                catch (Exception e)
                {
                    var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                    note = "BK SetRulerWithTitle wyjatek " + ie.GetType().Name;
                    Stumble("SetRulerWithTitle", ie);
                }
            }
            if (k.RulingClan != h.Clan)
            {
                ChangeRulingClanAction.Apply(k, h.Clan);
                note = (note != null ? note + "; " : "") + "gra ChangeRulingClanAction (bez tytulu BK)";
            }
            return note;
        }

        /// <summary>Recenzja 15.10: jak Diplomacy CivilWarBehavior.NewKing po wyborze krola (RemoveClan nowego wladcy z frakcji buntu jego krolestwa).
        /// Diplomacy nie pozwala wladcy zakladac ani wstepowac do frakcji, a powrot na tron to nie decyzja krolestwa - bez tego rod wracajacy na tron, ktory
        /// byl w frakcji (np. sponsor abdykacji przeciw poprzednikowi), przy wybuchu wojny domowej wyszedlby z wlasnego krolestwa (StartRebellionAction).</summary>
        private static string DropFromRebelFactions(Kingdom k, Clan c)
        {
            try
            {
                if (!_dipResolved)
                {
                    _dipResolved = true;
                    var tm = AccessTools.TypeByName("Diplomacy.CivilWar.RebelFactionManager");
                    var tf = AccessTools.TypeByName("Diplomacy.CivilWar.Factions.RebelFaction");
                    _dipGet = tm != null ? AccessTools.Method(tm, "GetRebelFaction", new[] { typeof(Kingdom) }) : null;
                    _dipRemove = tf != null ? AccessTools.Method(tf, "RemoveClan", new[] { typeof(Clan) }) : null;
                    _dipClans = tf != null ? AccessTools.Property(tf, "Clans") : null;
                }
                if (_dipGet == null || _dipRemove == null || _dipClans == null) return "frakcje Diplomacy: brak typow";
                int seen = 0, left = 0;
                var list = _dipGet.Invoke(null, new object[] { k }) as System.Collections.IEnumerable;   // kopia listy - RemoveClan / DestroyRebelFaction jej nie zmienia
                if (list != null)
                    foreach (var f in list)
                    {
                        if (f == null) continue;
                        seen++;
                        bool member = false;
                        var cl = _dipClans.GetValue(f, null) as System.Collections.IEnumerable;
                        if (cl != null) foreach (var o in cl) if (o == c) { member = true; break; }
                        if (!member) continue;
                        _dipRemove.Invoke(f, new object[] { c });
                        left++;
                    }
                return "frakcje Diplomacy: " + seen + ", rod wyszedl z " + left;
            }
            catch (Exception e)
            {
                var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                Stumble("DropFromRebelFactions", ie);
                return "frakcje Diplomacy: wyjatek " + ie.GetType().Name;
            }
        }

        private const string ProbeKingdom = "empire_s";   // wglad do testu: House Targaryen (Daenerys)

        /// <summary>Wglad do testu: kto rzadzi House Targaryen i co z wladczynia ze startu (Daenerys).</summary>
        private static string Probe()
        {
            Seat s;
            if (_byKingdom == null || !_byKingdom.TryGetValue(ProbeKingdom, out s)) return "wglad " + ProbeKingdom + ": brak na liscie";
            var k = KingdomOf(s);
            var h = HeroOf(s);
            if (k == null || k.IsEliminated) return "wglad " + ProbeKingdom + ": krolestwo upadlo";
            string now = k.Leader != null ? Txt(k.Leader.Name) + " (" + k.Leader.StringId + ")" : "-";
            string st;
            if (h == null) st = "brak bohatera " + s.Ruler;
            else if (!h.IsAlive) st = "zmarl";
            else if (h.Clan == null || h.Clan.Kingdom != k) st = "rod poza krolestwem (krolestwo rodu " + (h.Clan != null && h.Clan.Kingdom != null ? h.Clan.Kingdom.StringId : "brak") + ")";
            else if (h.Clan.Leader != h) st = "nie glowa rodu";
            else if (k.RulingClan == h.Clan) st = "rzadzi" + (h.IsPrisoner ? " (w niewoli)" : "");
            else st = "nie rzadzi" + (h.IsPrisoner ? " (w niewoli)" : " (na wolnosci)");
            return "wglad " + ProbeKingdom + ": wladca " + now + ", start " + (h != null ? Txt(h.Name) + " (" + h.StringId + ")" : s.Ruler) + " " + st;
        }

        /// <summary>Stan monarchii ROT: ile rzadzi wladca ze startu, ile nie (z powodem) i lista tych, gdzie zywy wladca ze startu w swoim krolestwie nie rzadzi.</summary>
        private static string Census()
        {
            if (_seats == null) return "brak listy";
            int rules = 0, dead = 0, away = 0, notHead = 0, fallen = 0, captive = 0, rot = 0;
            var off = new StringBuilder();
            foreach (var s in _seats)
            {
                var k = KingdomOf(s);
                var h = HeroOf(s);
                if (k == null || k.IsEliminated) { fallen++; continue; }
                if (h == null || !h.IsAlive) { dead++; continue; }
                if (h.Clan == null || h.Clan.Kingdom != k) { away++; continue; }
                if (h.Clan.Leader != h) { notHead++; continue; }
                if (k.RulingClan == h.Clan)
                {
                    rules++;
                    if (h.IsPrisoner) captive++;
                    continue;
                }
                if (IsRotHandover(k)) { rot++; continue; }   // tron oddany przez fabule ROT - nie liczy sie jako utrata tronu
                if (off.Length > 0) off.Append("; ");
                off.Append(s.Kingdom).Append(": start ").Append(Txt(h.Name)).Append(" (").Append(h.StringId).Append(h.IsPrisoner ? ", w niewoli" : ", na wolnosci")
                   .Append("), teraz ").Append(k.Leader != null ? Txt(k.Leader.Name) + " (" + k.Leader.StringId + ")" : "-");
            }
            return "monarchii ROT " + _seats.Count + ": rzadzi wladca ze startu " + rules + " (w niewoli " + captive + "), zmarl " + dead + ", rod poza krolestwem " + away
                   + ", nie glowa rodu " + notHead + ", krolestwo upadlo " + fallen + ", tron oddany fabula ROT " + rot + ", nie rzadzi [" + off + "]";
        }
    }

    /// <summary>15.10: tron fabularny - wpiecie przy starcie sesji, doba (powrot na tron i linia). Bez zapisu.</summary>
    internal sealed class StoryThroneBehavior : CampaignBehaviorBase
    {
        public StoryThroneBehavior() { StoryThrone.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { StoryThrone.OnSession(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { StoryThrone.Daily(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
