using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// JEDNA ROBOTA NA EKRAN KUZNI (m96-czaskuzni; Jeff 20.10: "jak kuje bron, to jestem w menu z paskiem, a teraz czasami jestem, czasami
    /// nie jestem; ma liczyc calkowity czas: wytapiam, potem kuje miecz, wychodzi 23 godziny - odpala sie pasek i leci 23 godziny; jak wyjde
    /// i nie skoncze kucia, biore tylko to, co wykulem, a reszta czeka, az wroce").
    ///
    /// Bylo (wpis 83 + poprawka 15.10): dwa zegary. Kute sztuki szly do kolejki kowala (ForgeClock.Queue / OnNewItemCrafted "van"), a godziny
    /// wytopu i rafinacji (i zlecen, i spartaczonych sztuk) odsiadywal osobny pasek Banner Kings (StartCraftingMenu z reszta godzin). Skutki:
    /// pasek BK pokazywal tylko reszte (ekran 14.8 h -> pasek 8.8 h), kolejka sztuk szla w tym czasie rownolegle; nasz pasek wlaczal sie tylko,
    /// gdy reszty nie bylo; wyjscie klawiszem Esc nie wolalo StartCraftingMenu wcale (zadnego paska, a licznik godzin kolejki zostawal na
    /// nastepny raz i zjadal godziny wytopu); BK liczy na ekranie tylko stamine gracza - praca towarzysza przy kowadle = 0 h i brak paska.
    ///
    /// Teraz jedna zasada: kazda czynnosc na ekranie kuzni (wytop, rafinacja, kucie broni, CRAFT zbroi, lukow i amunicji, zlecenie) dopisuje
    /// swoje godziny do JEDNEJ roboty w tej kuzni - to jest zwykla kolejka kowala tej osady (ArmouryBehavior._projects, jeden zegar, po kolei).
    /// Godziny = zuzyta stamina WSZYSTKICH kowali z ekranu / 6 (miara BK), sztuki wedlug swojej reguly (C5 przy ForgeLikeTownSmith). Material
    /// z wytopu i rafinacji trafia do sakw od razu (ekran potrzebuje go do nastepnego kucia), ale jego godziny staja w kolejce jako "praca bez
    /// sztuki" PRZED sztukami z tej wizyty. Wyjscie z ekranu (Done albo Esc) zawsze otwiera jeden pasek (arm_project_wait) na lacznym czasie tej
    /// kuzni; pasek BK nie startuje. Sztuka wychodzi, gdy pasek ja przejdzie. Odejscie przed koncem: masz to, co gotowe; reszta czeka w tej
    /// kuzni (zapis jak dotad - arm_projects) i idzie dalej po powrocie (znow ekran kuzni albo "Stay until the work is done"). Noc: jak dotad
    /// (WorkshopNightRest) - 23-5 kuznia stoi, pasek czeka do switu. Cena, materialy i XP bez zmian.
    /// </summary>
    internal static class ForgeJob
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.ForgeOneJob && ForgeClock.On; } }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string WaitMenu = "arm_project_wait";

        // sesja ekranu kuzni (watek glowny; czas gry stoi, gdy ekran jest otwarty - kolejka sie nie rusza)
        private static bool _open;
        private static string _town = "";
        private static string _back;                                  // menu, z ktorego gracz otworzyl ekran (powrot po pasku)
        private static readonly Dictionary<Hero, int> _stam0 = new Dictionary<Hero, int>();
        private static float _pieceOld, _pieceNew;                    // godziny sztuk z ekranu: miara BK (stamina / 6) i godziny w kolejce
        private static int _pieceActs, _pieceItems;
        private static int _linesAt;                                  // dlugosc kolejki przy otwarciu - sztuki z ekranu dochodza na jej koniec
        private static bool _tookOver;                                // zamkniecie obsluzone - odsiadka BK pominieta

        internal static string ReturnMenu;                            // null = menu "Work the forge" (SmithMenu)
        private static bool _waitOpen;
        private static string _waitTown = "";
        private static float _textBefore;                             // godziny sprzed wizyty w napisie paska (raz, po wyjsciu z ekranu)

        // liczniki od startu sesji gry (tylko log)
        private static int _jobs, _done, _stepped, _broken, _prepLines, _piecesAll;
        private static double _hoursAll, _prepAll;

        internal static void Reset()
        {
            _open = false; _town = ""; _back = null; _stam0.Clear();
            _pieceOld = _pieceNew = 0f; _pieceActs = _pieceItems = 0; _linesAt = 0; _tookOver = false;
            ReturnMenu = null; _waitOpen = false; _waitTown = ""; _textBefore = 0f;
            _jobs = _done = _stepped = _broken = _prepLines = _piecesAll = 0; _hoursAll = _prepAll = 0.0;
        }

        // ------------------------------------------------------------ ekran kuzni: otwarcie, sztuki, napis, wyjscie

        /// <summary>Postfiks konstruktora CraftingVM: ekran kuzni otwarty - stamina wszystkich kowali z listy ekranu na starcie.</summary>
        public static void VmCtorPost(CraftingVM __instance)
        {
            try
            {
                if (_open) Close(false, "nowy ekran bez wyjscia z poprzedniego");   // poprzednia wizyta bez ExecuteCancel - jej godziny do kolejki, bez paska
                _tookOver = false;
                if (!On || __instance == null || Campaign.Current == null) return;
                var here = Settlement.CurrentSettlement;
                var b = ArmouryBehavior.Instance;
                if (here == null || b == null) return;   // poza osada - jak dotad (pasek BK)
                if (_waitOpen) { _waitOpen = false; _broken++; }   // poprzedni pasek zamknelo inne menu (napad, wczytanie)
                _open = true;
                _town = here.StringId;
                _back = CurrentMenuId();
                _stam0.Clear();
                var list = __instance.AvailableCharactersForSmithing;
                if (list != null)
                    foreach (var hv in list)
                    {
                        var h = hv != null ? hv.Hero : null;
                        if (h != null && !_stam0.ContainsKey(h)) _stam0[h] = Forge.Stamina(h);
                    }
                if (Hero.MainHero != null && !_stam0.ContainsKey(Hero.MainHero)) _stam0[Hero.MainHero] = Forge.Stamina(Hero.MainHero);
                _pieceOld = _pieceNew = 0f; _pieceActs = _pieceItems = 0;
                _linesAt = b.ProjectLines.Count;
                ForgeClock.ResetBkQueued();
            }
            catch (Exception e) { Log.Error("ForgeJob.Begin", e); _open = false; }
        }

        /// <summary>ForgeClock.NoteQueued: sztuka z ekranu poszla do kolejki tej kuzni (hours - w kolejce, oldHours - miara BK).</summary>
        internal static void NotePiece(int count, float hours, float oldHours)
        {
            if (!_open) return;
            try
            {
                var here = Settlement.CurrentSettlement;
                if (here == null || here.StringId != _town) return;
                _pieceOld += Math.Max(0f, oldHours);
                _pieceNew += Math.Max(0f, hours);
                _pieceActs++;
                _pieceItems += Math.Max(1, count);
            }
            catch { }
        }

        /// <summary>Godziny pracy na ekranie: zuzyta stamina wszystkich kowali / 6 (ta sama miara co "Hours spent" BK).</summary>
        private static float SpentHours()
        {
            float st = 0f;
            foreach (var kv in _stam0)
            {
                int now = Forge.Stamina(kv.Key);
                if (kv.Value > now) st += kv.Value - now;
            }
            return st / 6f;
        }

        private static float PrepHours() { return Math.Max(0f, SpentHours() - _pieceOld); }

        /// <summary>Godziny tej wizyty: praca bez sztuki + sztuki wedlug kolejki.</summary>
        private static float SessionHours() { return PrepHours() + _pieceNew; }

        /// <summary>Postfiks BK CraftingMixin.GetSpentHours: napis "Hours spent for all actions" = godziny tej wizyty, ktore odliczy pasek.</summary>
        public static void SpentHoursPost(ref float __result)
        {
            try { if (_open && On) __result = SessionHours(); } catch { }
        }

        /// <summary>Postfiks CraftingVM.ExecuteCancel (przycisk Done przez BK CloseWithWait i klawisz Esc): ekran zamkniety - robota do kolejki i pasek.</summary>
        public static void CancelPost()
        {
            try
            {
                if (!_open) return;
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm != null && gsm.ActiveState is TaleWorlds.CampaignSystem.GameState.CraftingState) return;   // zamkniete tylko okienko - ekran dalej otwarty
                Close(true, "wyjscie z ekranu");
            }
            catch (Exception e) { Log.Error("ForgeJob.CancelPost", e); }
        }

        /// <summary>ForgeClock.SkipBkWait: true = zamkniecie ekranu otworzylo juz nasz pasek albo ekran jest dalej otwarty (Done zamknal tylko okienko) -
        /// odsiadka BK nie startuje (robota pojdzie do paska przy prawdziwym wyjsciu).</summary>
        internal static bool TookOverBkWait()
        {
            bool t = _tookOver || _open;
            _tookOver = false;
            return t && On;
        }

        private static void Close(bool openBar, string why)
        {
            _open = false;
            try
            {
                float spent = SpentHours();
                float prep = Math.Max(0f, spent - _pieceOld);
                float session = prep + _pieceNew;
                ForgeClock.ResetBkQueued();
                var b = ArmouryBehavior.Instance;
                var here = Settlement.CurrentSettlement;
                if (b == null || here == null || here.StringId != _town)
                {
                    Log.Info("Robota w kuzni (m96): " + why + " poza kuznia wizyty (" + _town + ") - godzin " + F1(session) + " pominietych.");
                    return;
                }
                var lines = b.ProjectLines;
                bool prepIn = false;
                if (prep >= 0.05f)
                {
                    // praca bez sztuki PRZED sztukami z tej wizyty (zwykle: najpierw wytop, potem kucie z tego metalu); za robota sprzed wizyty
                    var p = new Project { Item = null, DaysLeft = prep / 24f, Tempo = 1, SettlementId = _town, Kind = Project.PrepKind, ModifierId = "", Count = 1 };
                    lines.Insert(Math.Max(0, Math.Min(_linesAt, lines.Count)), p.Serialize());
                    prepIn = true;
                    _prepLines++; _prepAll += prep;
                }
                if (session > 0.01f) { _jobs++; _hoursAll += session; _piecesAll += _pieceItems; }
                float total = b.ProjectHoursLeftHere(here);
                float before = Math.Max(0f, total - (prepIn ? prep : 0f) - _pieceNew);
                _tookOver = openBar;
                bool menu = Campaign.Current != null && Campaign.Current.CurrentMenuContext != null;   // bez menu (np. ekran z misji) - robota czeka, pasek z "Stay until the work is done"
                bool bar = openBar && total > 0.01f && menu;
                Log.Info("Robota w kuzni (m96): " + here.Name + " - " + why + " | ta wizyta: godzin " + F1(session) + " (praca bez sztuki - wytop, rafinacja, zlecenia, spartaczone - "
                         + F1(prep) + (prepIn ? " h w kolejce przed sztukami" : " h, za malo na wpis") + "; sztuk " + _pieceItems + " w " + _pieceActs + " czynnosciach, " + F1(_pieceNew)
                         + " h, miara BK " + F1(_pieceOld) + " h), stamina kowali z ekranu " + _stam0.Count + " / 6 = " + F1(spent) + " h | robota sprzed wizyty w tej kuzni " + F1(before)
                         + " h | razem w tej kuzni " + F1(total) + " h - pasek " + (bar ? "TAK" : (menu ? "NIE" : "NIE (brak menu gry)")) + ", odsiadka BK pominieta | " + Summary(b));
                if (bar)
                {
                    ReturnMenu = _back;
                    _textBefore = before;
                    GameMenu.SwitchToMenu(WaitMenu);
                }
            }
            catch (Exception e) { Log.Error("ForgeJob.Close", e); }
        }

        private static string CurrentMenuId()
        {
            try
            {
                var mc = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
                return mc != null && mc.GameMenu != null ? mc.GameMenu.StringId : null;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ kolejka i pasek

        /// <summary>AdvanceProjects: godzina pracy bez sztuki minela (DaysLeft juz zmniejszone) - zapis w miejscu albo koniec wpisu. Bez XP i bez wydania.</summary>
        internal static void TickedPrep(List<string> lines, string line, Project p)
        {
            try
            {
                int at = lines.IndexOf(line);
                if (at < 0) return;
                if (p.DaysLeft > 0f) lines[at] = p.Serialize();
                else lines.RemoveAt(at);
            }
            catch (Exception e) { Log.Error("ForgeJob.TickedPrep", e); }
        }

        /// <summary>Napis paska (po angielsku): laczny czas tej kuzni, kolejnosc, noc, odejscie.</summary>
        internal static string WaitText(float hours)
        {
            var s = Settings.Current;
            float before = _textBefore;
            _textBefore = 0f;
            return "Your work at the forge: about " + (int)Math.Ceiling(hours) + " hours in all"
                   + (before >= 0.5f ? " (" + (int)Math.Ceiling(before) + " of them left from before)" : "")
                   + " - smelting, refining and each piece, one after another. Each piece comes off the anvil when its turn is done."
                   + (s != null && s.WorkshopNightRest ? " The forge rests at night, from 23:00 to 5:00." : "")
                   + " Step away, and you keep what is finished; the rest waits here for you.";
        }

        internal static void WaitOpened(float hours)
        {
            if (!On) return;
            try
            {
                var here = Settlement.CurrentSettlement;
                string id = here != null ? here.StringId : "";
                if (_waitOpen && id != _waitTown) _broken++;   // poprzedni pasek w innej kuzni zamknelo inne menu (ten sam - powrot do paska, np. z ekwipunku)
                _waitOpen = true;
                _waitTown = id;
            }
            catch { }
        }

        /// <summary>SmithMenu.ProjWaitTick: pasek doszedl do konca albo gracz odszedl od kowadla.</summary>
        internal static void WaitEnded(bool finished, float left)
        {
            if (!On || !_waitOpen) return;
            _waitOpen = false;
            try
            {
                if (finished) _done++; else _stepped++;
                var st = Settlement.Find(_waitTown ?? "");
                var b = ArmouryBehavior.Instance;
                Log.Info("Robota w kuzni (m96): " + (st != null ? st.Name.ToString() : _waitTown) + " - " + (finished ? "pasek doszedl do konca" : "odejscie od kowadla (Step away), zostalo " + F1(left) + " h")
                         + " | " + Summary(b));
                if (!finished && left > 0.01f)
                    Log.Player("You step away from the forge. What is finished is yours; " + (int)Math.Ceiling(left) + " hours of work wait here for you.");
            }
            catch (Exception e) { Log.Error("ForgeJob.WaitEnded", e); }
        }

        /// <summary>Menu po pasku: to, z ktorego gracz otworzyl ekran kuzni (np. podmenu kuzni), inaczej fallback ("Work the forge").</summary>
        internal static string BackMenu(string fallback)
        {
            string id = ReturnMenu;
            ReturnMenu = null;
            try
            {
                if (!On || string.IsNullOrEmpty(id) || id == WaitMenu || id.IndexOf("wait", StringComparison.OrdinalIgnoreCase) >= 0) return fallback;
                var gmm = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                if (gmm == null || gmm.GetGameMenu(id) == null) return fallback;
                return id;
            }
            catch { return fallback; }
        }

        // ------------------------------------------------------------ log

        /// <summary>Liczniki sesji gry i robota czekajaca w kuzniach (godziny, sztuki, gotowe).</summary>
        private static string Summary(ArmouryBehavior b)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("roboty gracza w tej sesji gry: " + _jobs + " (godzin " + F1((float)_hoursAll) + ", w tym bez sztuki " + F1((float)_prepAll) + " h w " + _prepLines + " wpisach; sztuk " + _piecesAll
                      + "), paskow do konca " + _done + ", odejsc " + _stepped + ", przerwanych inaczej " + _broken + " | czekaja w kuzniach: ");
            try
            {
                var by = new Dictionary<string, float[]>();   // osada -> godziny, sztuki w toku, gotowe, wpisy bez sztuki
                if (b != null)
                    foreach (var line in b.ProjectLines)
                    {
                        var p = Project.Parse(line);
                        if (!p.Valid) continue;
                        float[] a;
                        if (!by.TryGetValue(p.SettlementId ?? "", out a)) { a = new float[4]; by[p.SettlementId ?? ""] = a; }
                        a[0] += Math.Max(0f, p.DaysLeft) * 24f;
                        if (p.IsPrep) a[3]++;
                        else if (p.DaysLeft > 0f) a[1] += Math.Max(1, p.Count);
                        else a[2] += Math.Max(1, p.Count);
                    }
                if (by.Count == 0) sb.Append("nic");
                else
                    sb.Append(string.Join("; ", by.OrderByDescending(kv => kv.Value[0]).Select(kv =>
                    {
                        var st = Settlement.Find(kv.Key);
                        return (st != null ? st.Name.ToString() : kv.Key) + " " + F1(kv.Value[0]) + " h (sztuk w toku " + (int)kv.Value[1] + ", gotowych " + (int)kv.Value[2]
                               + ", wpisow bez sztuki " + (int)kv.Value[3] + ")";
                    }).ToArray()));
            }
            catch (Exception e) { sb.Append("blad spisu (" + e.GetType().Name + ")"); }
            return sb.ToString() + ".";
        }

        private static string F1(float v) { return v.ToString("0.0", Inv); }

        // ------------------------------------------------------------ latki

        internal static void ApplyAll(Harmony h)
        {
            string a = "BRAK", c = "BRAK", t = "BRAK";
            try
            {
                System.Reflection.ConstructorInfo ctor = null;
                foreach (var ci in AccessTools.GetDeclaredConstructors(typeof(CraftingVM)))
                    if (!ci.IsStatic && (ctor == null || ci.GetParameters().Length > ctor.GetParameters().Length)) ctor = ci;
                if (ctor != null) { h.Patch(ctor, postfix: new HarmonyMethod(typeof(ForgeJob), nameof(VmCtorPost))); a = "wpiete"; }
            }
            catch (Exception e) { Log.Error("ForgeJob.ApplyAll(ctor)", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CraftingVM), "ExecuteCancel");
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(ForgeJob), nameof(CancelPost)) { priority = Priority.Last }); c = "wpiete"; }
            }
            catch (Exception e) { Log.Error("ForgeJob.ApplyAll(cancel)", e); }
            try
            {
                var tMixin = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                var m = tMixin != null ? AccessTools.DeclaredMethod(tMixin, "GetSpentHours") : null;
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(ForgeJob), nameof(SpentHoursPost))); t = "wpiety"; }
            }
            catch (Exception e) { Log.Error("ForgeJob.ApplyAll(BK)", e); }
            var s = Settings.Current;
            Log.Info("Robota w kuzni (m96-czaskuzni): jedna robota na ekran kuzni " + (On ? "CZYNNA" : "wylaczona (Forge One Job / Forge One Clock)")
                     + " - otwarcie ekranu " + a + ", wyjscie z ekranu (Done i Esc, ExecuteCancel) " + c + ", napis godzin BK " + t
                     + " | wytop, rafinacja, zlecenia i spartaczone: godziny w kolejce kuzni przed sztukami z tej wizyty, material od razu; jeden pasek po wyjsciu, odsiadka BK pominieta"
                     + " | noc: " + (s != null && s.WorkshopNightRest ? "23-5 kuznia stoi (Workshop Night Rest)" : "praca ciagiem") + ".");
        }
    }
}
