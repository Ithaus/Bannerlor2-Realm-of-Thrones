using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Armoury
{
    /// <summary>
    /// m116-fabula (4) - DORZECZE W WOJNIE OD KONCA PROLOGU (Jeff 24.10: "dorzecze jest w pokoju co to za bzdura to bylo pierwsze co zostalo zaatakowane").
    /// Kanon: po pojmaniu Tyriona przez Catelyn (prolog ok. dnia 110) Tywin wysyla Gregora Clegane'a, by palil Dorzecze (Brod Komediantow - Ned wysyla Berica), wojsko
    /// Jaimego rozbija lordow rzecznych pod Zlotym Zebem i oblega Riverrun, Edmure trafia do niewoli - wszystko PRZED egzekucja Neda i przed Szepczacym Lasem. Korona
    /// (vlandia - z Lannisterami i Clegane'ami) jest w wojnie z Dorzeczem (riverlands, rod Tully) od konca prologu; gdy Robb wstaje, Polnoc i Dorzecze walcza razem
    /// (Robb to wnuk Hostera; lordowie rzeczni oglaszaja go Krolem Polnocy i Tridentu).
    /// Dotad: wojna fabularna ROT Westerlands-Riverlands (vlandia-riverlands, StartDay 30) po rozciagnieciu osi Fabuli CrashScribe (x4.33) zaczynala sie ok. dnia
    /// opowiesci 130, a okno ROT RiverlandsDeclareWarEvent (25 x skala, ok. 108) ogladalo "The Riverlands declare war against the crown" ok. 100 dni po prawdziwym
    /// poczatku wojny. W kampanii Jeffa (dzien opowiesci ok. 33): wojna korona-Polnoc od dnia 8.7, Dorzecze w pokoju, Jaime w lochu Riverrun od dnia 15.
    ///
    /// (A1) POSTFIKS ROTStorylineWar.Enforced (klucz CanonRiverlandsWarAtPrologueEnd; wpinany z prefiksem Essos w InstallEssos - raz na proces): kampania z prologiem,
    ///      krol Robert martwy (_kingFallen), wpis ROT korona-Dorzecze (nie wlasny gracza - IsCustom), wlaczony w oknie ROT "Edit Storyline Wars" (IsEnabled),
    ///      bez niespelnionego wyzwalacza, strony istnieja, zadna nie jest krolestwem gracza -> wynik true (powod pusty). ROT EnforceWars wypowiada wtedy wojne sam
    ///      (od dnia opowiesci 1 = dzien prologu 189 - po smierci Roberta 186 i przed NedEvent ok. 8.7, kolejnosc jak w ksiazce); IsWarForced tej pary true - pokoj
    ///      ROT, PovertyPeace, StorylineReparations i rozejm Kroniki jej nie koncza. Finalizer CrashScribe (StartDay 130 z powrotem) dziala po postfiksie - bez
    ///      sprzecznosci. Sojusz prologu korona-Dorzecze zgasl w EndPacts (dzien 186) - bez kary za zerwany sojusz.
    /// (A2) NADROBIENIE W ZAPISIE JEFFA: kf=1 juz w zapisie, wiec postfiks dziala od wczytania; ROT EnforceWars wypowiada w pierwszej dobie (ta sama droga, co
    ///      Polnoc-korona 20:11:51). RiverDaily (doba m104, po CrownDaily): wojna jest, a w zapisie jej nie ma (rw=0) -> rw=1, linia WYPOWIEDZIANA, komunikat gracza
    ///      raz (nowa kampania / nadrobienie). Zapas: postfiks dziala, Enforced = true, dzien opowiesci >= 1, a wojny dalej nie ma 2 dni -> wypowiedzenie jak
    ///      EssosDaily (CallingDeclareWar ROT na czas DeclareWarAction, finally), potem odczyt IsAtWar i linia.
    /// (A3) SOJUSZ POLNOC + DORZECZE (klucz CanonNorthRiverlandsAlliance, z wojna wyzej): krol martwy, obie strony w wojnie z korona, Robb (lord_5_1) zyje, Krwawe Gody
    ///      nie ruszyly, krolestwa istnieja, gracz nie rzadzi zadnym, sojuszu jeszcze nie ma. StartAlliance gry sklada wnioski wezwan do wojny dla kazdej wojny
    ///      krolestw jednej strony, w ktorej druga nie walczy - wiec najpierw obie strony maja te same wojny krolestw (inaczej czekamy, linia raz na 7 dni).
    ///      Koniec sojuszu przesuwany na +364 dni, gdy zostalo < 182 (UpdateAllianceEndTime - jak Prologue.Crown). Sojusz zgubiony przy spelnionych warunkach -
    ///      od nowa najwyzej raz na 7 dni (licznik). KONIEC: Krwawe Gody (RedWeddingEvent >= 1), smierc Robba albo krolestwo zniszczone -> EndAlliance (tylko nasz /
    ///      przejety sojusz, ra=1), rae=1; tylko linia (ROT pokazuje swoje okno). Zastany sojusz AI tej pary - przejety (pilnowany od dzis).
    /// (A4) OKNO ROT RiverlandsDeclareWarEvent (prefiks StartEvent, ten sam klucz co A1): wojna korona-Dorzecze juz trwa -> tytul i teksty okna (wlasciwosci EventBase,
    ///      protected set - AccessTools.PropertySetter) mowia o holdzie lordow rzecznych, nie o wypowiedzeniu wojny. Data okna bez zmian (okna Kroniki 002.1 / 002.2 sa
    ///      do niej przypiete); relacje +80 ROT bez zmian. Lektor szuka nagrania po tytule - nowy tytul = bez glosu (lepiej niz glos o wypowiedzeniu wojny).
    /// Stan w zapisie arm_m104 (mapa PackWars/UnpackWars: rw, rwd, ra, rad, rae, raed - starsza DLL je pomija). Linie: "Wojny kanonu (m104): ... (4) Dorzecze ...".
    /// Bledy: CwStumble (licznik m104), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Prologue
    {
        internal const string RiverId = "riverlands";   // 264: internal - tabela par CanonPeaceLock
        internal const double RiverNetDays = 2.0;     // zapas A2: ROT nie wypowiedzial mimo wymuszenia - po tylu dniach wypowiada doba Armoury; 264: tez najdluzsze czekanie M6a-3 na wznowienie ROT
        private const int RiverAllyRetryDays = 7;    // sojusz zgubiony - ponowna proba najwyzej raz na tyle dni
        private const int RiverWaitLineDays = 7;     // linia "sojusz czeka" najwyzej raz na tyle dni

        private static bool RiverWarKey { get { var s = S; return s != null && s.CanonRiverlandsWarAtPrologueEnd; } }
        private static bool RiverAllyKey { get { var s = S; return s != null && s.CanonNorthRiverlandsAlliance; } }

        // stan (zapis arm_m104)
        private static bool _rwOn, _raOn, _raEnded;
        private static int _rwDay, _raDay, _raEndDay;
        // sesja / doba (bez zapisu)
        private static int _rwForcedDay, _rwForced, _rwNetDeclared, _raRenewed, _raExtended, _rlPopups;
        private static int _raRetryDay, _raWaitSaidDay;
        private static double _rwNoWarSince, _raLeft;
        private static string _rwState = "-", _raState = "-", _rlWire = "nie wpieta";
        private static double _rwSeenStart = double.NaN;   // 264 (3): poczatek biezacej wojny korona-Dorzecze juz widziany w tej sesji (dzien opowiesci; bez zapisu)

        private static void ResetRiver()
        {
            _rwOn = false; _raOn = false; _raEnded = false;
            _rwDay = NoDay; _raDay = NoDay; _raEndDay = NoDay;
            _rwForcedDay = 0; _rwForced = 0; _rwNetDeclared = 0; _raRenewed = 0; _raExtended = 0; _rlPopups = 0;
            _raRetryDay = NoDay; _raWaitSaidDay = NoDay;
            _rwNoWarSince = -1.0; _raLeft = -1.0;
            _rwState = "-"; _raState = "-";
            _rwSeenStart = double.NaN;
        }

        private static string PackRiver()
        {
            return "rw=" + (_rwOn ? "1" : "0") + ";rwd=" + _rwDay.ToString(Inv) + ";ra=" + (_raOn ? "1" : "0") + ";rad=" + _raDay.ToString(Inv)
                   + ";rae=" + (_raEnded ? "1" : "0") + ";raed=" + _raEndDay.ToString(Inv) + ";";
        }

        private static void UnpackRiver(Dictionary<string, string> map)
        {
            ResetRiver();
            string v;
            _rwOn = map.TryGetValue("rw", out v) && v == "1";
            _raOn = map.TryGetValue("ra", out v) && v == "1";
            _raEnded = map.TryGetValue("rae", out v) && v == "1";
            _rwDay = ParseInt(map, "rwd", NoDay);
            _raDay = ParseInt(map, "rad", NoDay);
            _raEndDay = ParseInt(map, "raed", NoDay);
        }

        // ---------------------------------------------------------------- wpis ROT korona-Dorzecze (strony wpisu ROT sie nie zmieniaja - wynik w pamieci na obiekt)
        private static readonly ConditionalWeakTable<object, object> _riverOf = new ConditionalWeakTable<object, object>();
        private static readonly ConditionalWeakTable<object, object>.CreateValueCallback _riverCalc = RiverCalc;
        private static PropertyInfo _pWHasTrig;
        private static bool _hasTrigTried;

        private static object RiverCalc(object war)
        {
            try
            {
                var a = WF1(war); var b = WF2(war);
                if (a == null || b == null || a.StringId == null || b.StringId == null) return No;
                if (_pWCustom != null && (bool)_pWCustom.GetValue(war, null)) return No;   // wojna wlasna gracza (okno ROT) - nie nasza
                return (a.StringId == Crown && b.StringId == RiverId) || (a.StringId == RiverId && b.StringId == Crown) ? Yes : No;
            }
            catch { return No; }
        }

        private static bool IsRiverWar(object war) { return war != null && RotWarsReady() && (bool)_riverOf.GetValue(war, _riverCalc); }

        /// <summary>Wpis ma wyzwalacz, ktory nie zaszedl (ROT HasTrigger &amp;&amp; !IsTriggered) - wtedy nie wymuszamy (wpis korona-Dorzecze ROT go nie ma).</summary>
        private static bool RiverTrigBlocks(object war)
        {
            if (!_hasTrigTried)
            {
                _hasTrigTried = true;
                try { _pWHasTrig = AccessTools.Property(war.GetType(), "HasTrigger"); } catch { _pWHasTrig = null; }
            }
            if (_pWHasTrig == null || _pWTriggered == null) return false;
            return (bool)_pWHasTrig.GetValue(war, null) && !(bool)_pWTriggered.GetValue(war, null);
        }

        private static object RiverWarEntry()
        {
            foreach (var w in RotWars()) if (IsRiverWar(w)) return w;
            return null;
        }

        /// <summary>A1: postfiks ROTStorylineWar.Enforced - wojna korona-Dorzecze wymuszona od smierci krola Roberta (warunki w opisie klasy). Nie rzuca.</summary>
        public static void RiverWarEnforcedPostfix(object __instance, ref bool __result, ref string disabledReason)
        {
            try
            {
                if (__result || !Active || !_kingFallen || __instance == null || !RiverWarKey) return;
                if (!IsRiverWar(__instance)) return;
                if (_pWEnabled != null && !(bool)_pWEnabled.GetValue(__instance, null)) return;   // wylaczona w oknie ROT "Edit Storyline Wars" - decyduje gracz
                if (RiverTrigBlocks(__instance)) return;
                var f1 = WF1(__instance); var f2 = WF2(__instance);
                if (f1 == null || f2 == null || f1.IsEliminated || f2.IsEliminated || PlayerRules(f1) || PlayerRules(f2)) return;
                __result = true;
                disabledReason = string.Empty;
                _rwForcedDay++; _rwForced++;
            }
            catch (Exception e) { CwStumble("RiverWarEnforcedPostfix", e); }
        }

        // ---------------------------------------------------------------- A4: okno ROT RiverlandsDeclareWarEvent
        private static MethodInfo _sTitle, _sUpper, _sLower;

        private static string InstallRiverPopup(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.Events.RiverlandsDeclareWarEvent");
                var tb = AccessTools.TypeByName("ROT.Events.EventBase");
                var m = t != null ? AccessTools.DeclaredMethod(t, "StartEvent") : null;
                if (m == null || tb == null) return "BRAK (ROT.Events.RiverlandsDeclareWarEvent.StartEvent)";
                _sTitle = AccessTools.PropertySetter(tb, "Title");
                _sUpper = AccessTools.PropertySetter(tb, "UpperText");
                _sLower = AccessTools.PropertySetter(tb, "LowerText");
                if (_sTitle == null || _sUpper == null || _sLower == null) return "BRAK (EventBase Title/UpperText/LowerText set)";
                return h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(RiverPopupPrefix))) != null ? "TAK" : "BRAK";
            }
            catch (Exception e) { CwStumble("InstallRiverPopup", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        private static int SetPopup(MethodInfo setter, object ev, string text)
        {
            try { setter.Invoke(ev, new object[] { text }); return 1; }
            catch (Exception e) { CwStumble("RiverPopup.Set", e); return 0; }
        }

        /// <summary>A4: prefiks RiverlandsDeclareWarEvent.StartEvent (PRZED OpenPopup w StartEvent): wojna korona-Dorzecze trwa - okno mowi o holdzie lordow rzecznych.</summary>
        public static void RiverPopupPrefix(object __instance)
        {
            try
            {
                if (!Active || !RiverWarKey || __instance == null) return;
                var crown = K(Crown); var river = K(RiverId);
                if (!Alive(crown) || !Alive(river) || !crown.IsAtWarWith(river)) return;
                // recenzja m116: hold dla Robba tylko, gdy Robb zyje, Polnoc stoi, w wojnie z korona, a sojusz Polnoc+Dorzecze nie zakonczony (Krwawe Gody,
                // smierc Robba) - inaczej neutralny tekst wojny Dorzecza (bez przysiegi martwemu albo upadlemu krolowi)
                var robb = HeroAny(RobbId); var north = K(NorthId);
                bool oath = robb != null && robb.IsAlive && Alive(north) && north.IsAtWarWith(crown) && !_raEnded;
                int n = oath
                    ? SetPopup(_sTitle, __instance, "The River Lords Bend the Knee")
                      + SetPopup(_sUpper, __instance, "Before Riverrun the river lords renew their oaths to Robb Stark.")
                      + SetPopup(_sLower, __instance, "House Tully pledges fealty to the King in the North.")
                    : SetPopup(_sTitle, __instance, "Riverrun Stands")
                      + SetPopup(_sUpper, __instance, "The river lords hold the fords and keep Riverrun against the crown.")
                      + SetPopup(_sLower, __instance, "House Tully fights on.");
                _rlPopups++; _warsEventDay = true;
                Log.Info(CwTag + "(4) Dorzecze - okno ROT RiverlandsDeclareWarEvent (dzien opowiesci " + StoryDays().ToString("0.0", Inv) + "): " + (oath ? "tekst holdu lordow rzecznych" : "tekst wojny Dorzecza bez holdu (Robb "
                         + (robb == null ? "brak" : robb.IsAlive ? "zyje" : "nie zyje") + ", Polnoc " + (Alive(north) ? "jest" : "BRAK") + ", sojusz " + (_raEnded ? "zakonczony" : "nie zakonczony") + ")")
                         + " zamiast wypowiedzenia wojny (wojna z korona trwa " + WarSinceText(crown, river, _rwDay) + "); ustawione " + n + "/3, relacje ROT bez zmian.");
            }
            catch (Exception e) { CwStumble("RiverPopupPrefix", e); }
        }

        // ---------------------------------------------------------------- doba (CanonWarsBehavior.OnDaily -> WarsDaily, po CrownDaily)

        private const string RiverNewText = "King Robert is dead and the Lannisters press their quarrel with House Tully. Ser Gregor's riders burn the Red Fork, Ser Jaime's host breaks the river lords at the Golden Tooth, and Riverrun calls its banners. The Riverlands are at war with the crown.";
        private const string RiverCatchUpText = "The fires along the Red Fork were no outlaws' work. House Tully stands openly against King Joffrey: the Riverlands are at war with the crown, and their lords ride with Robb Stark.";
        // recenzja m116: bez okrzykniecia Robba krolem - to robi Kronika 002.2 (Greatjon) i okno holdu ~108; sojusz wchodzi zaraz po smierci Roberta (przed 002.1)
        private const string RiverAllyText = "Robb Stark and the river lords of House Tully join their banners against the crown. The North and the Riverlands now fight as allies.";

        private static void RiverDaily()
        {
            if (!Active) { _rwState = _raState = "kampania klasyczna - nie dotyczy"; return; }
            double sd = StoryDays();
            int day = (int)Math.Floor(sd);
            var crown = K(Crown); var river = K(RiverId); var north = K(NorthId);
            try { RiverWar(crown, river, sd, day); }
            catch (Exception e) { CwStumble("RiverWar", e); _rwState = "BLAD (" + e.GetType().Name + ")"; }
            try { _raState = RiverAlly(crown, river, north, sd, day); }
            catch (Exception e) { CwStumble("RiverAlly", e); _raState = "BLAD (" + e.GetType().Name + ")"; }
        }

        private static void RiverWar(Kingdom crown, Kingdom river, double sd, int day)
        {
            bool war = Alive(crown) && Alive(river) && crown.IsAtWarWith(river);
            if (!RiverWarKey) { _rwNoWarSince = -1.0; _rwState = "klucz NIE (wojna z korona " + (war ? "TAK - z innej przyczyny" : "NIE") + ")"; return; }
            if (!Alive(crown) || !Alive(river)) { _rwState = "brak krolestwa (korona " + (Alive(crown) ? "jest" : "BRAK") + ", Dorzecze " + (Alive(river) ? "jest" : "BRAK") + ")"; return; }
            if (!_kingFallen) { _rwState = "czeka (koniec prologu - smierc krola Roberta w dniu " + KingFallsDay + ")"; return; }
            if (PlayerRules(crown) || PlayerRules(river)) { _rwNoWarSince = -1.0; _rwState = "krolestwo gracza - poza regula"; return; }
            if (war)
            {
                _rwNoWarSince = -1.0;
                if (!_rwOn)
                {
                    _rwOn = true; _rwDay = day; _warsEventDay = true;
                    Log.Info(CwTag + "(4) Dorzecze - wojna korona-Dorzecze WYPOWIEDZIANA przez ROT EnforceWars (dzien opowiesci " + sd.ToString("0.0", Inv) + ", dzien kalendarza "
                             + ((int)PrologueDay()).ToString(Inv) + ") | wymuszen Enforced przez latke dzis " + _rwForcedDay + " (w sesji " + _rwForced + ", zapas Armoury " + _rwNetDeclared + ")"
                             + " | korona w wojnie z " + Wars(crown) + "; Dorzecze w wojnie z " + Wars(river) + " | komunikat gracza: " + (sd < 5.0 ? "nowa kampania" : "nadrobienie") + ".");
                    Log.Player(sd < 5.0 ? RiverNewText : RiverCatchUpText, true);
                }
                RiverResumedLine(crown, river, sd);
                _rwState = "wojna z korona TAK (" + WarSinceText(crown, river, _rwDay) + ")";
                return;
            }
            if (sd < 1.0) { _rwNoWarSince = -1.0; _rwState = "czeka (bramka ROT EnforceWars - dzien opowiesci 1)"; return; }
            if (!RotWarsReady() || _essosWire != "TAK") { _rwState = "NIE (latka Enforced " + _essosWire + ")"; return; }
            var entry = RiverWarEntry();
            if (entry == null) { _rwState = "NIE (brak wpisu ROT korona-Dorzecze)"; return; }
            var args = new object[] { null };
            bool enf = (bool)_mWEnforced.Invoke(entry, args);   // takze nasz postfiks
            if (!enf)
            {
                _rwNoWarSince = -1.0;
                _rwState = "NIE (ROT nie wymusza: " + (args[0] as string ?? "") + (_pWEnabled != null && !(bool)_pWEnabled.GetValue(entry, null) ? "wylaczona w Edit Storyline Wars" : "") + ")";
                return;
            }
            if (_rwNoWarSince < 0) _rwNoWarSince = sd;
            if (sd - _rwNoWarSince < RiverNetDays) { _rwState = "czeka na ROT EnforceWars (wymuszona od dnia " + _rwNoWarSince.ToString("0.0", Inv) + ")"; return; }
            // zapas: ROT nie wypowiedzial przez RiverNetDays - jak EssosDaily (CallingDeclareWar ROT na czas wywolania)
            var beh = RotWarsBehavior();
            var f1 = WF1(entry); var f2 = WF2(entry);
            bool prev = false;
            try
            {
                if (_fCalling != null && beh != null) { prev = (bool)_fCalling.GetValue(beh); _fCalling.SetValue(beh, true); }
                DeclareWarAction.ApplyByDefault(f1, f2);
            }
            finally { if (_fCalling != null && beh != null) _fCalling.SetValue(beh, prev); }
            bool now = FactionManager.IsAtWarAgainstFaction(f1, f2);
            _warsEventDay = true;
            if (now) _rwNetDeclared++;
            Log.Info(CwTag + "(4) Dorzecze - zapas Armoury: wojna korona-Dorzecze " + (now ? "WYPOWIEDZIANA" : "NIE weszla (powod: " + (args[0] as string ?? "DeclareWarAction bez skutku") + ")")
                     + " (ROT EnforceWars nie wypowiedzial przez " + RiverNetDays.ToString("0", Inv) + " dni mimo wymuszenia; dzien opowiesci " + sd.ToString("0.0", Inv) + ").");
            _rwNoWarSince = now ? -1.0 : sd;
            _rwState = now ? "wojna z korona TAK (zapas Armoury, dzien " + day.ToString(Inv) + ")" : "NIE (zapas bez skutku)";
            // rw=1 i komunikat gracza - w nastepnej dobie (wojna jest, rw=0) - jedna droga dla ROT i zapasu
        }

        /// <summary>Wojny krolestw jednej strony, w ktorych druga nie walczy (bez siebie nawzajem) - null, gdy takich nie ma (StartAlliance nie zlozy wnioskow wezwan).</summary>
        private static string WarDiff(Kingdom a, Kingdom b)
        {
            var l = new List<string>();
            foreach (var f in a.FactionsAtWarWith) if (f != null && f.IsKingdomFaction && f != b && !f.IsAtWarWith(b) && l.Count < 6) l.Add(a.StringId + " z " + f.StringId);
            foreach (var f in b.FactionsAtWarWith) if (f != null && f.IsKingdomFaction && f != a && !f.IsAtWarWith(a) && l.Count < 6) l.Add(b.StringId + " z " + f.StringId);
            return l.Count == 0 ? null : "wojny tylko jednej strony: " + string.Join(", ", l.ToArray());
        }

        private static string RiverKeepEnd(IAllianceCampaignBehavior beh, Kingdom a, Kingdom b)
        {
            try
            {
                double left = (beh.GetAllianceEndDate(a, b) - CampaignTime.Now).ToDays;
                if (left < AllianceRenewBelow && UpdateEnd() != null && beh is AllianceCampaignBehavior)
                {
                    _mUpdateEnd.Invoke(beh, new object[] { a, b, CampaignTime.Now + CampaignTime.Days(AllianceKeepDays) });
                    double l2 = (beh.GetAllianceEndDate(a, b) - CampaignTime.Now).ToDays;
                    if (l2 > left + 0.5) _raExtended++;
                    left = l2;
                }
                _raLeft = left;
                return "koniec za " + left.ToString("0", Inv) + " dni";
            }
            catch (Exception e) { CwStumble("RiverKeepEnd", e); return "koniec ?"; }
        }

        private static string RiverAlly(Kingdom crown, Kingdom river, Kingdom north, double sd, int day)
        {
            if (!RiverAllyKey) return "klucz NIE";
            if (_raEnded) return "zakonczony (dzien opowiesci " + (_raEndDay != NoDay ? _raEndDay.ToString(Inv) : "?") + ")";
            if (!_kingFallen) return "czeka (koniec prologu)";
            var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>() : null;
            if (beh == null) return "brak AllianceCampaignBehavior";
            var robb = HeroAny(RobbId);
            string endWhy = null;
            if (RotEventState("RedWeddingEvent") >= 1) endWhy = "Krwawe Gody (RedWeddingEvent ruszyl)";
            else if (robb != null && !robb.IsAlive) endWhy = "Robb Stark nie zyje";
            else if (KingInTheNorth.Done) endWhy = "unia - Dorzecze w krolestwie Polnocy (272)";
            else if (!Alive(north) || !Alive(river)) endWhy = "krolestwo zniszczone (Polnoc " + (Alive(north) ? "jest" : "BRAK") + ", Dorzecze " + (Alive(river) ? "jest" : "BRAK") + ")";
            bool allied = Alive(north) && Alive(river) && beh.IsAllyWithKingdom(north, river);
            if (endWhy != null)
            {
                string what;
                if (_raOn && allied)
                {
                    beh.EndAlliance(north, river);
                    what = "sojusz zerwany " + (beh.IsAllyWithKingdom(north, river) ? "NIE (EndAlliance bez skutku)" : "TAK");
                }
                else what = _raOn ? "sojuszu juz nie bylo" : "nasz sojusz nie byl zalozony" + (allied ? " (sojusz AI zostaje)" : "");
                _raEnded = true; _raEndDay = day; _raOn = false; _warsEventDay = true;
                Log.Info(CwTag + "(4) Dorzecze - KONIEC sojuszu Polnoc+Dorzecze (" + endWhy + ", dzien opowiesci " + sd.ToString("0.0", Inv) + "): " + what + " - dalej bez pilnowania.");
                return "zakonczony (" + endWhy + ")";
            }
            if (robb == null) return "czeka (brak bohatera Robba " + RobbId + ")";
            if (PlayerRules(north) || PlayerRules(river)) return "krolestwo gracza - poza regula" + (allied ? " (sojusz trwa do swojego terminu)" : "");
            if (allied)
            {
                if (!_raOn)
                {
                    _raOn = true; _raDay = day; _warsEventDay = true;
                    Log.Info(CwTag + "(4) Dorzecze - SOJUSZ Polnoc+Dorzecze zastany (AI) - pilnowany od dzis (dzien opowiesci " + sd.ToString("0.0", Inv) + ").");
                }
                return "SOJUSZ TAK od dnia " + (_raDay != NoDay ? _raDay.ToString(Inv) : "?") + " (" + RiverKeepEnd(beh, north, river) + ")";
            }
            bool warN = Alive(crown) && north.IsAtWarWith(crown), warR = Alive(crown) && river.IsAtWarWith(crown);
            if (!warN || !warR) return "czeka (wojna z korona: Polnoc " + (warN ? "TAK" : "NIE") + ", Dorzecze " + (warR ? "TAK" : "NIE") + ")";
            if (_raOn && _raRetryDay != NoDay && day - _raRetryDay < RiverAllyRetryDays)
                return "zgubiony - ponowna proba za " + (RiverAllyRetryDays - (day - _raRetryDay)).ToString(Inv) + " dni";
            string diff = WarDiff(north, river);
            if (diff != null)
            {
                if (_raWaitSaidDay == NoDay || day - _raWaitSaidDay >= RiverWaitLineDays)
                {
                    _raWaitSaidDay = day; _warsEventDay = true;
                    Log.Info(CwTag + "(4) Dorzecze - sojusz Polnoc+Dorzecze czeka: " + diff + " (StartAlliance gry zlozylby wnioski wezwan do wojny; linia raz na "
                             + RiverWaitLineDays + " dni, dzien opowiesci " + sd.ToString("0.0", Inv) + ").");
                }
                return "czeka (" + diff + ")";
            }
            bool renew = _raOn;
            beh.StartAlliance(north, river);
            _raRetryDay = day; _warsEventDay = true;
            if (!beh.IsAllyWithKingdom(north, river))
            {
                Log.Info(CwTag + "(4) Dorzecze - SOJUSZ Polnoc+Dorzecze NIE wszedl (StartAlliance bez skutku, dzien opowiesci " + sd.ToString("0.0", Inv) + ").");
                return "SOJUSZ NIE wszedl";
            }
            if (renew) _raRenewed++;
            else { _raOn = true; _raDay = day; }
            string end = RiverKeepEnd(beh, north, river);
            Log.Info(CwTag + "(4) Dorzecze - SOJUSZ Polnoc+Dorzecze " + (renew ? "ODNOWIONY (" + _raRenewed + " w sesji)" : "TAK wszedl") + " (dzien opowiesci " + sd.ToString("0.0", Inv)
                     + ", " + end + ") | Polnoc w wojnie z " + Wars(north) + "; Dorzecze w wojnie z " + Wars(river) + ".");
            if (!renew) Log.Player(RiverAllyText);
            return "SOJUSZ TAK od dnia " + _raDay.ToString(Inv) + " (" + end + ")";
        }

        /// <summary>Opis (4) do linii startu sesji (bez dzialania).</summary>
        private static string RiverStartText()
        {
            if (!Active) return "kampania klasyczna - nie dotyczy";
            string war = "-", ally = "-";
            try
            {
                var crown = K(Crown); var river = K(RiverId); var north = K(NorthId);
                bool w = Alive(crown) && Alive(river) && crown.IsAtWarWith(river);
                war = w ? "TAK" + (_rwOn && _rwDay != NoDay ? " (" + WarSinceText(crown, river, _rwDay) + ")" : " (jeszcze nie zapisana)") : "NIE" + (_kingFallen ? "" : " (krol zyje)");
                var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>() : null;
                bool a = beh != null && Alive(north) && Alive(river) && beh.IsAllyWithKingdom(north, river);
                if (a)
                {
                    double left = (beh.GetAllianceEndDate(north, river) - CampaignTime.Now).ToDays;
                    ally = "TAK (koniec za " + left.ToString("0", Inv) + " dni" + (_raOn ? "" : ", AI") + ")";
                }
                else ally = "NIE (" + (_raEnded ? "zakonczony w dniu " + _raEndDay.ToString(Inv) : !RiverAllyKey ? "klucz NIE" : !w ? "brak wojny z korona" : "doba zalozy") + ")";
            }
            catch (Exception e) { CwStumble("RiverStartText", e); }
            return "klucz wojny " + (RiverWarKey ? "TAK" : "NIE") + ", klucz sojuszu " + (RiverAllyKey ? "TAK" : "NIE") + ", okno ROT " + _rlWire
                   + ", wojna z korona " + war + ", sojusz z Polnoca " + ally;
        }

        // ---------------------------------------------------------------- 264 (3): data poczatku BIEZACEJ wojny z rekordu postawy gry
        // Dotad linie mowily "od dnia 34" (rwd - pierwszy poczatek) takze po pokoju i wznowieniu przez ROT (dzien 108982/108983 w logu Jeffa 26.10: pokoj
        // Diplomacy, nastepnej doby ROT EnforceWars wypowiada znowu). Gra trzyma w StanceLink WarStartDate (ustawiany przy kazdym wejsciu w wojne)
        // i PeaceDeclarationDate (przy wyjsciu z wojny; nie zerowany przy nowej wojnie) - z nich biezacy poczatek i przerwa. rwd zostaje pierwszym poczatkiem.

        /// <summary>264 (3): poczatek biezacej wojny pary (dzien opowiesci, StanceLink.WarStartDate - StoryStart); NaN - brak wojny albo rekordu.
        /// peaceStory - ostatni pokoj tej pary przed tym poczatkiem (PeaceDeclarationDate), NaN gdy go nie bylo.</summary>
        internal static double WarStartStory(Kingdom a, Kingdom b, out double peaceStory)
        {
            peaceStory = double.NaN;
            try
            {
                if (a == null || b == null || a.IsEliminated || b.IsEliminated || !a.IsAtWarWith(b)) return double.NaN;
                var link = a.GetStanceWith(b);
                if (link == null) return double.NaN;
                var st = StoryStart();
                var ws = link.WarStartDate;
                var pd = link.PeaceDeclarationDate;
                if (pd != CampaignTime.Zero && pd <= ws) peaceStory = (pd - st).ToDays;
                return (ws - st).ToDays;
            }
            catch (Exception e) { CwStumble("WarStartStory", e); return double.NaN; }
        }

        /// <summary>264 (3): "od dnia 147.4, wznowiona po pokoju z dnia 146.4; pierwsza od dnia 34" albo "od dnia 34.0" - firstDay (rwd) NoDay = nieznany pierwszy
        /// poczatek (para bez pamieci): wznowiona, gdy rekord gry ma pokoj po starcie opowiesci przed biezacym poczatkiem.</summary>
        internal static string WarSinceText(Kingdom a, Kingdom b, int firstDay)
        {
            double peace;
            double w = WarStartStory(a, b, out peace);
            if (double.IsNaN(w)) return "od dnia " + (firstDay != NoDay ? firstDay.ToString(Inv) : "?");
            bool resumed = firstDay != NoDay ? w >= firstDay + 1.0 : !double.IsNaN(peace) && peace >= 0.0;
            string s = "od dnia " + w.ToString("0.0", Inv);
            if (resumed)
                s += ", wznowiona po pokoju" + (double.IsNaN(peace) ? "" : " z dnia " + peace.ToString("0.0", Inv)) + (firstDay != NoDay ? "; pierwsza od dnia " + firstDay.ToString(Inv) : "");
            return s;
        }

        /// <summary>264 (3): linia raz na nowy poczatek wojny korona-Dorzecze widziany w tej sesji (wznowiona po pokoju; poczatek mlodszy niz 2 dni - po
        /// wczytaniu starsze wznowienie tylko zapamietane, bez linii).</summary>
        private static void RiverResumedLine(Kingdom crown, Kingdom river, double sd)
        {
            double peace;
            double w = WarStartStory(crown, river, out peace);
            if (double.IsNaN(w) || (!double.IsNaN(_rwSeenStart) && Math.Abs(w - _rwSeenStart) < 0.01)) return;
            _rwSeenStart = w;
            if (_rwDay == NoDay || w < _rwDay + 1.0 || sd - w > RiverNetDays) return;
            _warsEventDay = true;
            Log.Info(CwTag + "(4) Dorzecze - wojna korona-Dorzecze WZNOWIONA (dzien opowiesci " + w.ToString("0.0", Inv) + ", po pokoju z dnia "
                     + (double.IsNaN(peace) ? "?" : peace.ToString("0.0", Inv)) + "; pierwsza od dnia " + _rwDay.ToString(Inv) + ") | korona w wojnie z " + Wars(crown)
                     + "; Dorzecze w wojnie z " + Wars(river) + ".");
            RiverResumedSeen++;
        }

        internal static int RiverResumedSeen;   // 264 autotest T6: ile wznowien zobaczyla doba m104 w tej sesji (bez zapisu)

        // ---------------------------------------------------------------- 264: dostep tylko do odczytu dla CanonPeaceLock / CanonWarWatch
        internal static Kingdom KingdomById(string id) { return K(id); }
        internal static bool RulesRealm(IFaction f) { return PlayerRules(f); }
        internal static Hero HeroById(string id) { return H(id); }   // Hero.Find, zapas FindFirst
        internal static int RiverFirstDay { get { return _rwDay; } }
        internal const int NoDayValue = NoDay;   // pierwszy poczatek nieznany (para bez pamieci m104)

        private static string RiverDayText()
        {
            return "wojna: " + _rwState + "; sojusz z Polnoca: " + _raState + "; wymuszen Enforced dzis " + _rwForcedDay + " (w sesji " + _rwForced + ")"
                   + (_rwNetDeclared > 0 ? ", zapas Armoury " + _rwNetDeclared : "") + (_raExtended > 0 ? ", koniec sojuszu przesuniety " + _raExtended + "x" : "")
                   + (_rlPopups > 0 ? ", okno ROT z tekstem holdu " + _rlPopups : "");
        }
    }
}
