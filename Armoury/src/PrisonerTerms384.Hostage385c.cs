using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 385c (Jeff 11.11 "tak, rob wszystko 2-4", pkt 2 "zakladnik jako czesc oferty"). ZAKLADNIK reczy za slowo GRACZA: tylko gdy gracz cos przysiega -
    /// zadanie A (pakt wzajemny) albo przysiega w ofercie (L / P / M); nigdy przy T (pokoj i tak otwiera lochy). Klucze PrisonerTermsHostage (TAK) i
    /// PrisonerTermsHostageDeath (TAK od 393 - Jeff 11.11 "tak, maja go sciac"; do 392 NIE).
    /// KTO: bohater rodu gracza z DRUZYNY GRACZA (nie gracz, nie dziecko, nie wodz partii ani gubernator - ci nie stoja w szeregu druzyny; nie postac kanonu z
    ///   wierszami #move / trzymana scena; nie krol ani nastepca - Ransom178 zrobilby z niego "wielkiego jenca" korony). Do 4 linii rozmowy, jeden wybor.
    /// GDZIE: zwykly jeniec gry (stan Prisoner, TakePrisonerAction.Apply ze zdarzeniem - role w partii zdejmuje gra i BK) w LOCHU OSADY rodu rozmowcy
    ///   (najblizsza warownia rodu, inaczej frakcji; nie oblezona, nie Bialych Wedrowcow). Loch osady nie ma wodza - UniqueSpoils nie zabiera unikatow, nie ma
    ///   ucieczek nadmiaru co godzine ani rozbicia druzyny. Bez naszego moda zakladnik jest zwyklym jencem (4%/dobe ucieczki, kurier okupu) - nic nie wisi w zapisie.
    /// STRAZ (tylko stan H - slowo dotrzymywane): (1) prefiks PrisonerReleaseCampaignBehavior.DailyHeroTick(Hero) - bez losowej ucieczki; pokoj, zmiana pana
    ///   osady, smierc, odbicie z lochu i nasze zwolnienie ida droga gry bez zmian; (2) Ransom178.ConsiderPrefix - bez kuriera okupu (zloto nie rusza);
    ///   (3) prefiks RealisticCaptivity.CaptivityBehavior.StripCompanionGear na czas samego przekazania (_handing) - RC rozebralby towarzysza na stale
    ///   (sprzet wraca tylko przy wyjsciu GRACZA z niewoli). DLL RealisticCaptivity bez zmian; RC obecny, a latka nie weszla -> linia zakladnika nieczynna.
    /// WARTOSC: 0.5 x okup (ManPrice) x k (malzonek / dziecko / rodzic / rodzenstwo gracza 2.0, inny czlonek rodu 1.5, towarzysz 1.0); z zakladnikiem przysiega
    ///   gracza nie traci polowy przy Honor gracza < 0 (ma porecznie).
    /// KONIEC: doba Until -> EndCaptivityAction.ApplyByReleasedByCompensation (ta sama droga co "moi ludzie"), relacja z lordem +3; przysiega zdjeta przez gre
    ///   przed terminem (tamci uderzyli pierwsi) -> zwolnienie bez +3; juz nie jeniec tej osady / tej frakcji (pokoj, upadek osady, odbicie) -> wpis precz.
    /// ZDRADA LORDA (wylosowana przy zgodzie, niejawna): bierze zakladnika jak reszte zaplaty, stan Z - bez strazy (moze uciec, kurier okupu przychodzi);
    ///   termin mija, a on nadal trzyma -> klamca (374) i kara jak za zdrade. Zdrada jawna od razu (nie uwolnil "moich ludzi") - zakladnik nie jest przekazywany.
    /// GRACZ LAMIE SLOWO (BrokeWord): stan B - straz zdjeta od razu, zostaje zwyklym jencem; egzekucja tylko z kluczem PrisonerTermsHostageDeath i u lorda z
    ///   Mercy < 0 (w dobie, nie w srodku zdarzenia bitwy). 393: kat = lord przysiegi, a gdy martwy / w niewoli / poza frakcja - pan lochu; bitwa przy lochu ->
    ///   rozliczenie nastepnej doby (wpis B zostaje); zakladnik, ktory zostal bohaterem gracza (dziedzic) - wpis precz bez egzekucji.
    /// Zapis: pole 17 rekordu umowy (id zakladnika) i piata sekcja "arm_k384": bohater:frakcja:wodz:osada:do:stan:przysiega;... (starszy DLL czyta 4 sekcje).
    /// </summary>
    internal static partial class PrisonerTerms384
    {
        private const int ShowHost = 4;
        private const string RcType = "RealisticCaptivity.CaptivityBehavior";

        private static bool KHost { get { var s = Settings.Current; return s != null && s.PrisonerExchangeForTerms && s.PrisonerTermsHostage; } }
        private static bool KHostDeath { get { var s = Settings.Current; return s != null && s.PrisonerTermsHostageDeath; } }

        /// <summary>State: H - slowo dotrzymywane (straz), Z - zdrada lorda (bez strazy, pilnowany termin), B - gracz zlamal slowo (do rozliczenia w dobie).</summary>
        private sealed class HostRec { public string Hero = "", Fac = "", Lord = "", St = ""; public int Until; public char State = 'H'; public bool Oath; }

        private static readonly List<HostRec> _hosts = new List<HostRec>();
        // stan rozmowy
        private static readonly List<Hero> _hostCand = new List<Hero>();
        private static readonly Dictionary<Hero, double> _hostVal = new Dictionary<Hero, double>();
        private static readonly Dictionary<Hero, double> _hostK = new Dictionary<Hero, double>();
        private static Hero _host;
        private static Settlement _hostHall;
        // latki (raz na proces - nie zerowane w Reset)
        private static string _escHook = "nie wpinana", _rcHook = "nie wpinana";
        private static bool _escOk, _rcPresent, _rcOk;
        private static bool _handing;
        private static int _hEsc, _hCour, _hStripCut, _hBlocked;

        private static void HostReset()
        {
            _hosts.Clear(); _handing = false; _hEsc = 0; _hCour = 0; _hStripCut = 0; _hBlocked = 0;
            HostTalkClear();
        }

        private static void HostTalkClear() { _host = null; _hostHall = null; _hostCand.Clear(); _hostVal.Clear(); _hostK.Clear(); }

        /// <summary>Straz i przebranie dzialaja: latka ucieczki weszla, a RealisticCaptivity albo go nie ma, albo jego rozbieranie towarzysza jest wyciete.</summary>
        private static bool HostHooksOk { get { return _escOk && (!_rcPresent || _rcOk); } }

        /// <summary>Zakladnik reczy za slowo gracza: pakt wzajemny (A) albo przysiega w ofercie (L / P / M).</summary>
        private static bool HostWantOk() { return _want == 'A' || (_oath && OathAllowed(_want)); }

        private static bool HostOn { get { return _host != null && KHost && HostWantOk(); } }

        private static double HostValue() { double v; return _host != null && _hostVal.TryGetValue(_host, out v) ? v : 0; }

        // ------------------------------------------------------------ latki
        /// <summary>Z ApplyPact (menu glowne, raz na proces). Obie metody prywatne, zadeklarowane w swoich klasach (DeclaredMethod).</summary>
        private static void ApplyHostage(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(PrisonerReleaseCampaignBehavior), "DailyHeroTick", new[] { typeof(Hero) });
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(PrisonerTerms384), nameof(HostEscapePre))); _escOk = true; _escHook = "wpieta"; }
                else _escHook = "BRAK PrisonerReleaseCampaignBehavior.DailyHeroTick";
            }
            catch (Exception e) { _escOk = false; _escHook = "blad " + e.GetType().Name; Log.Error("PrisonerTerms384.ApplyHostage(ucieczka)", e); }
            try
            {
                var t = AccessTools.TypeByName(RcType);
                _rcPresent = t != null;
                if (t == null) _rcHook = "RealisticCaptivity nieobecny - niepotrzebna";
                else
                {
                    var m = AccessTools.DeclaredMethod(t, "StripCompanionGear", new[] { typeof(Hero) });
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(PrisonerTerms384), nameof(HostStripPre))); _rcOk = true; _rcHook = "wpieta"; }
                    else _rcHook = "BRAK CaptivityBehavior.StripCompanionGear";
                }
            }
            catch (Exception e) { _rcOk = false; _rcHook = "blad " + e.GetType().Name; Log.Error("PrisonerTerms384.ApplyHostage(RC)", e); }
            try
            {
                Log.Info(Tag + ": zakladnik (385c) - latka ucieczki (prefiks PrisonerReleaseCampaignBehavior.DailyHeroTick): " + _escHook + ", latka rozbierania RealisticCaptivity (prefiks CaptivityBehavior.StripCompanionGear, tylko na czas przekazania): " + _rcHook
                         + ", kurier okupu: bramka w Ransom178.ConsiderPrefix | linia zakladnika " + (HostHooksOk ? "czynna" : "NIECZYNNA (latki)") + ".");
            }
            catch { }
        }

        /// <summary>Start sesji (AddDialogs): RealisticCaptivity zaladowany po latkach z menu glownego -> rozbieranie nie wyciete -> linia nieczynna.</summary>
        private static string HostStartLine()
        {
            try { if (!_rcPresent && AccessTools.TypeByName(RcType) != null) { _rcPresent = true; _rcOk = false; _rcHook = "RealisticCaptivity zaladowany PO latkach - nie wpieta"; } }
            catch (Exception e) { Stumble("HostStartLine", e); }
            return " | 385c: zakladnik " + (KHost ? "TAK" : "NIE") + ", egzekucja po zlamaniu slowa " + (KHostDeath ? "TAK" : "NIE") + ", latki: ucieczka " + _escHook + " / RC " + _rcHook + (HostHooksOk ? "" : " -> linia NIECZYNNA") + ", zakladnicy " + _hosts.Count;
        }

        private static HostRec HostFind(Hero h)
        {
            if (h == null || h.StringId == null) return null;
            foreach (var r in _hosts) if (r.Hero == h.StringId) return r;
            return null;
        }

        /// <summary>Straz: wpis H, bohater nadal jencem w lochu osady tej samej frakcji (385c-p: nie tylko osady z wpisu). Tylko odczyt.</summary>
        private static bool HostGuarded(Hero h)
        {
            var r = HostFind(h);
            if (r == null || r.State != 'H' || !h.IsPrisoner) return false;
            var pb = h.PartyBelongedToAsPrisoner;
            // 385c-p: loch DOWOLNEJ osady tej samej frakcji (gra / NorthConquest.MovePrisoners przenosi jencow miedzy twierdzami frakcji; r.St poprawia HostDaily)
            if (pb == null || !pb.IsSettlement || pb.Settlement == null) return false;
            var f = pb.MapFaction;
            return f != null && f.StringId == r.Fac;
        }

        /// <summary>Prefiks PrisonerReleaseCampaignBehavior.DailyHeroTick(Hero): zakladnik ze straza nie losuje ucieczki. Bez zakladnikow - jedno porownanie.</summary>
        public static bool HostEscapePre(Hero __0)
        {
            if (_hosts.Count == 0) return true;
            try { if (HostGuarded(__0)) { _hEsc++; return false; } }
            catch (Exception e) { Stumble("HostEscapePre", e); }
            return true;
        }

        /// <summary>Prefiks RealisticCaptivity.CaptivityBehavior.StripCompanionGear(Hero): wyciety tylko na czas naszego przekazania zakladnika.</summary>
        public static bool HostStripPre()
        {
            if (!_handing) return true;
            _hStripCut++;
            return false;
        }

        /// <summary>Ransom178.ConsiderPrefix (takze przy 178 wylaczonym): kurier okupu nie przychodzi po zakladnika ze straza.</summary>
        internal static bool HostageHeld(Hero h)
        {
            if (_hosts.Count == 0) return false;
            try { if (HostGuarded(h)) { _hCour++; return true; } }
            catch (Exception e) { Stumble("HostageHeld", e); }
            return false;
        }

        // ------------------------------------------------------------ rozmowa
        private static double HostKin(Hero h)
        {
            var me = Hero.MainHero;
            try
            {
                if (me.Spouse == h || h.Father == me || h.Mother == me || me.Father == h || me.Mother == h
                    || (h.Father != null && h.Father == me.Father) || (h.Mother != null && h.Mother == me.Mother)) return 2.0;
                if (h.IsPlayerCompanion) return 1.0;
            }
            catch { return 1.0; }
            return 1.5;
        }

        private static string HostKinName(Hero h)
        {
            double k; if (!_hostK.TryGetValue(h, out k)) k = 1.0;
            return k >= 2.0 ? "your close kin" : k >= 1.5 ? "of your house" : "your companion";
        }

        /// <summary>Bohater nadaje sie na zakladnika TERAZ: w szeregu druzyny gracza, zywy, dorosly, rodu gracza, nie jeniec.</summary>
        private static bool HostFit(Hero h)
        {
            if (h == null || h == Hero.MainHero || !h.IsAlive || h.IsChild || h.IsPrisoner || h.StringId == null) return false;
            if (h.Clan == null || h.Clan != Clan.PlayerClan) return false;
            if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) return false;
            var mp = MobileParty.MainParty;
            return mp != null && h.PartyBelongedTo == mp && mp.MemberRoster != null && mp.MemberRoster.Contains(h.CharacterObject);
        }

        /// <summary>Loch dla zakladnika: najblizsza warownia rodu lorda, inaczej frakcji; nie oblezona, bez bitwy, nie Bialych Wedrowcow.</summary>
        private static Settlement HostHall(Hero lord)
        {
            try
            {
                var f = lord != null ? lord.MapFaction : null;
                var mp = MobileParty.MainParty;
                if (f == null || lord.Clan == null || mp == null) return null;
                var p = mp.GetPosition2D;
                Settlement own = null, fac = null; float dOwn = float.MaxValue, dFac = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsFortification || s.Party == null || s.StringId == null || s.OwnerClan == null || s.MapFaction != f) continue;
                    if (s.SiegeEvent != null || s.Party.MapEvent != null || OthersRise.IsWhiteWalkerClan(s.OwnerClan) || s.OwnerClan == Clan.PlayerClan) continue;
                    float d = s.GetPosition2D.Distance(p);
                    if (s.OwnerClan == lord.Clan) { if (d < dOwn) { dOwn = d; own = s; } }
                    else if (d < dFac) { dFac = d; fac = s; }
                }
                return own ?? fac;
            }
            catch (Exception e) { Stumble("HostHall", e); return null; }
        }

        /// <summary>Begin: kandydaci z szeregu druzyny gracza (najcenniejsi pierwsi, najwyzej ShowHost) i loch rozmowcy.</summary>
        private static void HostBegin(Hero lord)
        {
            try
            {
                if (!KHost || lord == null) return;
                _hostHall = HostHall(lord);
                var r = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (r == null) return;
                for (int i = 0; i < r.Count; i++)
                {
                    var c = r.GetCharacterAtIndex(i);
                    var h = c != null && c.IsHero ? c.HeroObject : null;
                    if (!HostFit(h) || !Ok(h.StringId) || _hostVal.ContainsKey(h)) continue;
                    // 385c-p: juz obiecany w umowie czekajacej na wykonanie (kolejka / oferta P) - druga umowa padlaby w Payable (przy P z kara)
                    string hid = h.StringId;
                    if ((_offer != null && _offer.Host == hid) || _queue.Exists(q => q.Host == hid)) continue;
                    try { if (CanonCompanions.HasMoveRows(h) || CanonCompanions.HeldRow(h) != null) continue; } catch (Exception e) { Stumble("HostBegin.Canon", e); continue; }
                    if (Ransom178.IsRulerOrHeir(h)) continue;   // "wielki jeniec" korony (178) - nie na zakladnika
                    double k = HostKin(h);
                    _hostK[h] = k; _hostVal[h] = 0.5 * ManPrice(h) * k;
                    _hostCand.Add(h);
                }
                _hostCand.Sort((a, b) => _hostVal[b].CompareTo(_hostVal[a]));
                if (_hostCand.Count > ShowHost) _hostCand.RemoveRange(ShowHost, _hostCand.Count - ShowHost);
            }
            catch (Exception e) { Stumble("HostBegin", e); }
        }

        private static bool HCond(int i)
        {
            try
            {
                if (!KHost || _talk == null || i >= _hostCand.Count || !HostWantOk()) return false;
                var h = _hostCand[i];
                if (_host != null && _host != h) return false;   // jeden zakladnik
                MBTextManager.SetTextVariable("ARM384_H" + i, _host == h ? "Take back my hostage (" + Name(h) + " stays with me)."
                    : "I will leave " + Name(h) + " (" + HostKinName(h) + ") in your keeping" + (_hostHall != null ? " at " + Name(_hostHall) : "") + " as surety for my word, for " + Days + " days.");
                return true;
            }
            catch (Exception e) { Stumble("HCond", e); return false; }
        }

        private static bool HClick(int i, out TextObject why)
        {
            why = null;
            try
            {
                if (i < _hostCand.Count && _host == _hostCand[i]) return true;   // cofnac wolno zawsze
                if (!HostHooksOk) { why = new TextObject("{=!}A hostage cannot be kept safely in this game session (see Armoury.log, line 385c)."); return false; }
                if (_hostHall == null) { why = new TextObject("{=!}He has no hall to keep a hostage in."); return false; }
            }
            catch (Exception e) { Stumble("HClick", e); }
            return true;
        }

        private static void HToggle(int i)
        {
            if (i >= _hostCand.Count) return;
            var h = _hostCand[i];
            _host = _host == h ? null : h;
        }

        // ------------------------------------------------------------ wykonanie
        /// <summary>385c-p (Run, przed zaplata; nie dla P - tam usluga juz wykonana): zakladnik policzony w wartosci oferty, a przekazac go sie nie da
        /// (brak lochu rodu / frakcji rozmowcy w tej chwili albo latki strazy nieczynne) - umowa przepada bez zaplaty zamiast "warunki za nic". Powod po polsku albo null.</summary>
        private static string HostBlocked(Deal d, Hero lord)
        {
            if (string.IsNullOrEmpty(d.Host)) return null;
            try
            {
                if (d.Betray && (d.Men.Count > 0 || d.SolN > 0)) return null;   // zdrada jawna - i tak nie przekazywany
                if (!HostHooksOk) return "zakladnik: latki strazy nieczynne (ucieczka " + _escHook + ", RC " + _rcHook + ")";
                var st = HostHall(lord);
                if (st == null || st.Party == null || st.MapFaction == null || !Ok(st.MapFaction.StringId) || !Ok(st.StringId) || !Ok(lord.StringId)) return "zakladnik: brak lochu rodu / frakcji rozmowcy";
            }
            catch (Exception e) { Stumble("HostBlocked", e); }
            return null;
        }

        /// <summary>After (po zaplacie, moich ludziach i przysiedze): przekazanie zakladnika do lochu. Nie wyszlo - log, bez wpisu, bez kary.</summary>
        private static void HostHand(Deal d, Hero lord, StringBuilder sb)
        {
            if (string.IsNullOrEmpty(d.Host)) return;
            try
            {
                var h = Hero.Find(d.Host);
                if (!HostFit(h)) { sb.Append(" | zakladnik " + (h != null ? Name(h) : d.Host) + ": juz nie w szeregu druzyny gracza - NIE przekazany"); return; }
                if (d.Betray && (d.Men.Count > 0 || d.SolN > 0))   // 385c-p: takze zdrada jawna 385d (sami zwykli zolnierze - After juz oglosil klamce)
                {
                    sb.Append(" | zakladnik " + Name(h) + ": zdrada jawna (" + (d.Men.Count > 0 ? "moi ludzie" : "moi zolnierze, " + d.SolN) + " nie uwolnieni) - NIE przekazany");
                    return;
                }
                if (!HostHooksOk) { sb.Append(" | zakladnik " + Name(h) + ": latki strazy nieczynne (ucieczka " + _escHook + ", RC " + _rcHook + ") - NIE przekazany"); return; }
                var st = HostHall(lord);
                if (st == null || st.Party == null || st.MapFaction == null || !Ok(st.MapFaction.StringId) || !Ok(st.StringId) || !Ok(lord.StringId))
                {
                    sb.Append(" | zakladnik " + Name(h) + ": brak lochu rodu / frakcji - NIE przekazany");
                    Log.Player(Name(lord) + " has no hall to keep " + Name(h) + " in. Your hostage stays with you.");
                    return;
                }
                _handing = true;
                try { TakePrisonerAction.Apply(st.Party, h); }
                finally { _handing = false; }
                if (!h.IsPrisoner || h.PartyBelongedToAsPrisoner != st.Party)
                {
                    sb.Append(" | zakladnik " + Name(h) + ": przekazanie NIE wyszlo (stan " + h.HeroState + ") - bez wpisu");
                    return;
                }
                int until = (int)Today + d.Days;
                _hosts.RemoveAll(x => x.Hero == h.StringId);
                _hosts.Add(new HostRec { Hero = h.StringId, Fac = st.MapFaction.StringId, Lord = lord.StringId, St = st.StringId, Until = until, State = d.Betray ? 'Z' : 'H', Oath = d.Oath && !d.Betray });
                sb.Append(" | zakladnik " + Name(h) + " -> loch " + Name(st) + " [" + st.StringId + "] do dnia " + until
                          + (d.Betray ? " (ZDRADA wylosowana: stan Z - bez strazy, zwykly jeniec, termin pilnowany)" : " (stan H - straz: bez ucieczki i bez kuriera okupu" + (d.Oath ? ", reczy za przysiege" : ", reczy za pakt") + ")"));
                Log.Player(Name(h) + " rides to " + Name(st) + " as your hostage, to be kept there for " + d.Days + " days.");
            }
            catch (Exception e) { Stumble("HostHand", e); }
        }

        /// <summary>BrokeWord: gracz zlamal slowo wobec tych przysiegajacych - ich zakladnicy traca straz od razu (stan B), rozliczenie w dobie.</summary>
        private static string HostBroke(HashSet<string> lords)
        {
            if (_hosts.Count == 0 || lords == null) return null;
            var names = new List<string>();
            try
            {
                foreach (var r in _hosts)
                {
                    if (r.State != 'H' || !lords.Contains(r.Lord)) continue;
                    r.State = 'B';
                    var h = Hero.Find(r.Hero); var lord = Hero.Find(r.Lord);
                    names.Add(h != null ? Name(h) : r.Hero);
                    // 393-p: przy kluczu egzekucji tekst nie moze obiecywac "zwyklego jenca" (HostDaily tej samej doby moze go stracic); klucz NIE - tekst jak dotad
                    Log.Player("Your broken oath falls on " + (h != null ? Name(h) : "your hostage") + ": " + (KHostDeath ? "the hostage's life is in the hands of " + (lord != null ? Name(lord) : "the lord you swore to") + " now."
                               : (lord != null ? Name(lord) : "the lord you swore to") + " holds your hostage as a common prisoner now."), true);
                }
            }
            catch (Exception e) { Stumble("HostBroke", e); }
            return names.Count > 0 ? "zakladnicy bez strazy (stan B): " + string.Join(", ", names.ToArray()) : null;
        }

        private static bool HostFree(Hero h)
        {
            try { EndCaptivityAction.ApplyByReleasedByCompensation(h); }
            catch (Exception e) { Stumble("HostFree", e); }
            return !h.IsPrisoner;
        }

        /// <summary>Doba: przeglad wpisow - martwi / poza rodem / juz nie w tym lochu precz, termin -> zwolnienie, przysiega zdjeta przez gre -> zwolnienie,
        /// zdrada po terminie -> klamca, zlamane slowo gracza -> zwykly jeniec albo (klucz) egzekucja - 393-p: bez bramki Mercy. Wolane z Daily; wyjatek wpisu nie gasi reszty.</summary>
        private static void HostDaily(int today)
        {
            if (_hosts.Count == 0 && _hEsc + _hCour + _hStripCut == 0) return;
            int nH = 0, nZ = 0, nB = 0, nBWait = 0, freed = 0, gone = 0;
            for (int i = _hosts.Count - 1; i >= 0; i--)
            {
                var r = _hosts[i];
                try
                {
                    var h = Hero.Find(r.Hero);
                    var lord = Hero.Find(r.Lord);
                    string who = h != null ? Name(h) : r.Hero;
                    if (h == null || !h.IsAlive) { _hosts.RemoveAt(i); gone++; Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " nie zyje - wpis zdjety."); continue; }
                    // 393: zakladnik zostal GRACZEM (dziedzic po smierci gracza) - niewola gracza to droga gry, nie nasza; nigdy egzekucja
                    if (h == Hero.MainHero) { _hosts.RemoveAt(i); gone++; Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " jest teraz bohaterem gracza - wpis zdjety (stan " + r.State + "), bez egzekucji."); continue; }
                    if (h.Clan != Clan.PlayerClan) { _hosts.RemoveAt(i); gone++; Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " poza rodem gracza - wpis zdjety (dalej zwykly jeniec gry)."); continue; }
                    var pb = h.IsPrisoner ? h.PartyBelongedToAsPrisoner : null;
                    if (pb == null) { _hosts.RemoveAt(i); gone++; Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " juz nie jest jencem (pokoj, odbicie, zwolnienie, ucieczka bez strazy) - wpis zdjety, stan " + r.State + ", termin byl do dnia " + r.Until + "."); continue; }
                    var f = pb.MapFaction;
                    // 385c-p: ta sama frakcja, inna twierdza (przenosiny jencow, np. NorthConquest.MovePrisoners) - wpis idzie za zakladnikiem
                    if (pb.IsSettlement && pb.Settlement != null && f != null && f.StringId == r.Fac && pb.Settlement.StringId != r.St && Ok(pb.Settlement.StringId))
                    {
                        Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " przeniesiony do innej twierdzy tej samej frakcji (" + r.St + " -> " + pb.Settlement.StringId + ") - wpis poprawiony, stan " + r.State + " i termin bez zmian.");
                        r.St = pb.Settlement.StringId;
                    }
                    if (!pb.IsSettlement || pb.Settlement == null || pb.Settlement.StringId != r.St || f == null || f.StringId != r.Fac)
                    {
                        _hosts.RemoveAt(i); gone++;
                        Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " w innych rekach (" + (pb.Name != null ? pb.Name.ToString() : "-") + ", " + (f != null ? f.StringId : "-") + "; wpis: " + r.St + ", " + r.Fac + ") - wpis zdjety, dalej zwykly jeniec gry.");
                        continue;
                    }
                    if (r.State == 'B')
                    {
                        // 393: bitwa / szturm przy tym lochu wlasnie trwa - rozliczenie jutro (wpis B zostaje, strazy i tak juz nie ma)
                        if (KHostDeath && pb.MapEvent != null) { nBWait++; continue; }
                        _hosts.RemoveAt(i); nB++;
                        if (!KHostDeath) { Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " po zlamaniu slowa gracza: zwykly jeniec gry (ucieczka i kurier okupu jak w grze), klucz egzekucji NIE - wpis zdjety."); continue; }
                        // 393: kat = lord, ktoremu gracz przysiegal; martwy / sam w niewoli / juz poza ta frakcja -> pan lochu, w ktorym zakladnik siedzi
                        Hero ex = lord != null && lord.IsAlive && !lord.IsPrisoner && lord.MapFaction == f ? lord : null;
                        string exHow = "lord przysiegi";
                        if (ex == null)
                        {
                            var oc = pb.Settlement.OwnerClan; var ol = oc != null ? oc.Leader : null;
                            if (ol != null && ol.IsAlive && !ol.IsPrisoner && !ol.IsChild && ol != Hero.MainHero && oc != Clan.PlayerClan) { ex = ol; exHow = "pan lochu (lord przysiegi " + (lord != null ? Name(lord) : r.Lord) + " martwy / w niewoli / poza frakcja)"; }
                        }
                        int mercy = ex != null ? Trait(ex, DefaultTraits.Mercy) : 0;
                        // 393-p (Jeff 11.11, odp. 6 "tak, maja go sciac" - bez zastrzezen): bramka Mercy < 0 zdjeta - scina kazdy kat (w ROT_lords.xml Mercy < 0 ma ok. 13%
                        // postaci, wiec z bramka zakladnik ginalby w 1 przypadku na 8); Mercy zostaje tylko w logu
                        string whyNot = ex == null ? "brak kata (lord przysiegi i pan lochu martwi / w niewoli / dziecko)"
                            : !h.CanDie(KillCharacterAction.KillCharacterActionDetail.Executed) ? "smierc zablokowana (CanHeroDie)" : null;
                        if (whyNot == null)
                        {
                            KillCharacterAction.ApplyByExecution(h, ex);
                            if (!h.IsAlive)
                            {
                                Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " STRACONY przez " + Name(ex) + " [" + exHow + "] w lochu " + r.St + " (gracz zlamal slowo; Mercy " + mercy + ", klucz Prisoner Terms Hostage Death TAK) - wpis zdjety.");
                                Log.Player(Name(ex) + " has " + who + " put to death for your broken oath.", true);
                                continue;
                            }
                            whyNot = "KillCharacterAction.ApplyByExecution bez skutku (stan " + h.HeroState + ", znak smierci " + h.DeathMark + ")";
                        }
                        Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " po zlamaniu slowa gracza: zwykly jeniec gry (ucieczka i kurier okupu jak w grze), bez egzekucji - " + whyNot + " - wpis zdjety.");
                        continue;
                    }
                    if (r.State == 'Z')
                    {
                        if (r.Until > today) { nZ++; continue; }
                        _hosts.RemoveAt(i);
                        bool known = lord != null && SiegeSurrender374.IsLiar(lord);
                        int n = lord != null && lord.IsAlive && !known ? Punish(lord) : 0;
                        Log.Info(Tag + ": doba " + today + " | ZDRADA zakladnika: termin minal (dzien " + r.Until + "), " + (lord != null ? Name(lord) : r.Lord) + " nadal trzyma " + who + " - "
                                 + (known ? "klamca juz znany, bez drugiej kary" : "klamca, relacja -30, kara " + n + " glow rodow") + "; dalej zwykly jeniec gry.");
                        Log.Player((lord != null ? Name(lord) : "The lord you swore to") + " does not release " + who + " though the term is served. His word was worth nothing.", true);
                        continue;
                    }
                    // stan H
                    bool term = r.Until <= today;
                    bool freedByGame = !term && r.Oath && f.NotAttackableByPlayerUntilTime.IsPast;   // gra zdjela przysiege: tamci uderzyli na gracza pierwsi
                    if (!term && !freedByGame) { nH++; continue; }
                    if (!HostFree(h)) { nH++; _hBlocked++; Log.Info(Tag + ": doba " + today + " | zakladnik " + who + ": zwolnienie zablokowane (stan " + h.HeroState + ") - ponowie jutro."); continue; }
                    _hosts.RemoveAt(i); freed++;
                    if (term)
                    {
                        if (lord != null && lord.IsAlive) { try { ChangeRelationAction.ApplyPlayerRelation(lord, 3); } catch (Exception e) { Stumble("HostDaily.Rel", e); } }
                        Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " ZWOLNIONY - termin odsluzony (dzien " + r.Until + ", loch " + r.St + ") | relacja z " + (lord != null ? Name(lord) : r.Lord) + " +3.");
                        Log.Player(who + " is released; the term is served.");
                    }
                    else
                    {
                        Log.Info(Tag + ": doba " + today + " | zakladnik " + who + " ZWOLNIONY przed terminem (dzien " + r.Until + "): gra zdjela przysiege gracza wobec " + r.Fac + " (tamci uderzyli pierwsi).");
                        Log.Player(who + " is released: " + Realm(f) + " struck at you first, and your oath is void.");
                    }
                }
                catch (Exception e) { Stumble("HostDaily.Row", e); }
            }
            Log.Info(Tag + ": doba " + today + " | 385c zakladnicy " + _hosts.Count + " (H " + nH + " / Z " + nZ + "; B rozliczone dzis " + nB + ", B odlozone - bitwa przy lochu " + nBWait + "), zwolnieni dzis " + freed + ", wpisy zdjete " + gone + ", zwolnienia zablokowane " + _hBlocked
                     + " | losowania ucieczki wstrzymane " + _hEsc + ", rozwazenia okupu wstrzymane " + _hCour + ", rozbierania RC wyciete " + _hStripCut + ".");
            _hEsc = 0; _hCour = 0; _hStripCut = 0; _hBlocked = 0;
        }

        // ------------------------------------------------------------ zapis (piata sekcja "arm_k384")
        private static void HostExport(StringBuilder sb)
        {
            bool f = true;
            foreach (var r in _hosts)
            {
                if (!Ok(r.Hero) || !Ok(r.Fac) || !Ok(r.Lord) || !Ok(r.St) || r.Hero.Length == 0) continue;
                if (!f) sb.Append(';'); f = false;
                sb.Append(r.Hero).Append(':').Append(r.Fac).Append(':').Append(r.Lord).Append(':').Append(r.St).Append(':').Append(r.Until.ToString(Inv)).Append(':').Append(r.State).Append(':').Append(r.Oath ? '1' : '0');
            }
        }

        private static int HostImport(string sec)
        {
            int bad = 0;
            foreach (var p in sec.Split(';'))
            {
                if (p.Length == 0) continue;
                var x = p.Split(':'); int u;
                if (x.Length == 7 && x[0].Length > 0 && x[5].Length == 1 && (x[5][0] == 'H' || x[5][0] == 'Z' || x[5][0] == 'B') && int.TryParse(x[4], System.Globalization.NumberStyles.Integer, Inv, out u))
                    _hosts.Add(new HostRec { Hero = x[0], Fac = x[1], Lord = x[2], St = x[3], Until = u, State = x[5][0], Oath = x[6] == "1" });
                else bad++;
            }
            return bad;
        }
    }
}
