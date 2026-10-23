using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// AUTOTEST RACHUNKU KUZNI (m109-kuznia) - tylko pod plikiem CrashScribe\armoury-kuznia.flag i DLL autotestu (LogGate 1/2); w zwyklej grze nic nie robi.
    /// (b) pierwsza doba sesji: dla luku, kuszy, zbroi, broni z ekranu gry i strzal - rachunek ForgeBill, potem zuzycie TA SAMA DROGA co klikniecie
    ///     (luk, kusza, strzaly: Forge.TakeFor; zbroja: FletchForge.SpendBill + MassSpend - kwit BK i kg otwartych ladunkow; bron: prywatne
    ///     CraftingCampaignBehavior.SpendMaterials z latkami SmithWood i SmithCoal), pomiar sztuk z sakw i kg z pamieci, porownanie z rachunkiem.
    ///     Brak w sakwach - dolozenie brakujacych sztuk na probe. Po kazdym przedmiocie stan sprzed proby: sakwy (roznica na elementach), pamiec drewna, wegla,
    ///     lnu i skory, skrzynka metalu. Linie "test - <id> ..." i "test - wynik: ...".
    /// (c) test ekranu (OnApplicationTick, najwyzej 3 proby na sesje, >= 30 s po (b)): gracz w menu miasta bez okien, bez misji i rozmowy, bez roboty w tej
    ///     kuzni (ProjectHoursLeftHere - wyjscie z ekranu nie otworzy paska ForgeJob ani doby kuzni) - CraftingHelper.OpenCrafting, co 2 s krok ForgeView
    ///     (ForgeBillMixin.TestStep: bron, zbroja, luk, strzaly - kafle VM wobec rachunku), zdjecie silnika, na koncu ExecuteCancel. HIPOTEZA: PushState z taktu
    ///     aplikacji przejdzie; dowodem jest odczyt VM, zdjecie - dodatek. Recenzja K1: kazde przerwanie (ekran bez mixinu ForgeView, wyjatek, limit 60 s na
    ///     ekranie) i koniec zamykaja ekran takze bez ForgeView (CloseScreen - ExecuteCancel na VM zlapanym przy otwarciu przez ForgeJob.VmCtorPost) - inaczej
    ///     otwarty ekran kucia stalby do konca biegu (czas kampanii stoi, Blocker nie widzi MapScreen).
    /// KUZ-5: kucia probne (b) przechodza prawdziwa droga zuzycia - ich linie "Drewno/Wegiel/Surowce w kuzni" i komunikaty kuzni stoja miedzy "test - zuzycie
    ///     probne: poczatek" a "test - wynik", a liczniki doby drewna, wegla i przepisow z masy pierwszej doby je licza (sakwy i pamiec wracaja).
    /// </summary>
    internal static class ForgeBillTest
    {
        private const string P = ForgeBill.P;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static float _clock, _billsAt = -1f, _at, _acc, _openedAt;
        private static bool _billsDone, _screenDone;
        private static int _stumbles, _tries, _stage, _step, _screenN, _screenOk;
        private static string _pendingLine, _pendingMode;
        private static MethodInfo _testStep;
        private static bool _stepLooked;
        private static WeakReference _vm;          // VM ekranu otwartego przez test (ForgeJob.VmCtorPost -> NoteVm) - wyjscie bez ForgeView
        private const float ScreenLimit = 60f;     // najdluzej na ekranie (kroki ok. 15 s) - potem wyjscie i nastepna proba

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("ForgeBillTest." + where + " (potkniecie " + _stumbles + ")", e);
        }

        private static string YN(bool v) { return v ? "TAK" : "NIE"; }
        private static string Kg(double v) { return ForgeBill.Kg(v); }

        /// <summary>ForgeBill.SessionStart: test od nowa w kazdej sesji.</summary>
        internal static void Reset()
        {
            _billsDone = false; _screenDone = false; _billsAt = -1f;
            _tries = 0; _stage = 0; _step = 0; _screenN = 0; _screenOk = 0; _pendingLine = null; _vm = null;
        }

        // ============================================================ (b) rachunek wobec zuzycia
        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: raz na sesje pod plikiem flagi.</summary>
        internal static void Daily(int day)
        {
            if (_billsDone || !ForgeBill.TestWanted()) return;
            _billsDone = true;
            try { RunBills(); }
            catch (Exception e) { Stumble("RunBills", e); Log.Info(P + "test - wynik: przerwany wyjatkiem " + e.GetType().Name + ", potkniecia " + _stumbles + "."); }
            _billsAt = _clock;
        }

        private struct Snap
        {
            public EquipmentElement El;
            public int Amount;
        }

        private static string KeyOf(EquipmentElement el)
        {
            return (el.Item != null ? el.Item.StringId : "?") + "|" + (el.ItemModifier != null ? el.ItemModifier.StringId : "");
        }

        private static Dictionary<string, Snap> Take(ItemRoster roster)
        {
            var d = new Dictionary<string, Snap>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.EquipmentElement.Item == null) continue;
                string k = KeyOf(el.EquipmentElement);
                Snap s;
                if (d.TryGetValue(k, out s)) { s.Amount += el.Amount; d[k] = s; }
                else d[k] = new Snap { El = el.EquipmentElement, Amount = el.Amount };
            }
            return d;
        }

        private sealed class State
        {
            public Dictionary<string, Snap> Bags;
            public float Wood, Coal;
            public string Stock, Scraps;
        }

        private static State Save(ItemRoster roster)
        {
            return new State { Bags = Take(roster), Wood = SmithWood.OpenKg, Coal = SmithCoal.OpenKg, Stock = SmithStock.Export(), Scraps = SmeltScraps.Export() };
        }

        /// <summary>Stan sprzed proby: roznica na elementach sakw, pamiec drewna i wegla, len i skora (SmithStock), skrzynka metalu. true = zgodny po odtworzeniu.</summary>
        private static bool Restore(ItemRoster roster, State s)
        {
            var now = Take(roster);
            var keys = new HashSet<string>(s.Bags.Keys);
            foreach (var k in now.Keys) keys.Add(k);
            foreach (var k in keys)
            {
                Snap a, b;
                int before = s.Bags.TryGetValue(k, out a) ? a.Amount : 0;
                int cur = now.TryGetValue(k, out b) ? b.Amount : 0;
                if (before == cur) continue;
                roster.AddToCounts(before > 0 ? a.El : b.El, before - cur);
            }
            SmithWood.OpenKg = s.Wood;
            SmithCoal.OpenKg = s.Coal;
            SmithStock.Import(s.Stock);
            SmeltScraps.Import(s.Scraps);
            var check = Take(roster);
            bool ok = check.Count == s.Bags.Count;
            if (ok)
                foreach (var kv in s.Bags)
                {
                    Snap c;
                    if (!check.TryGetValue(kv.Key, out c) || c.Amount != kv.Value.Amount) { ok = false; break; }
                }
            return ok && SmithStock.Export() == s.Stock && SmeltScraps.Export() == s.Scraps && SmithWood.OpenKg == s.Wood && SmithCoal.OpenKg == s.Coal;
        }

        private static ForgeBill.Bill BillOf(string kind, ItemObject it)
        {
            return ForgeBill.SampleBill(kind, it);
        }

        private static void RunBills()
        {
            var party = MobileParty.MainParty;
            var roster = party != null ? party.ItemRoster : null;
            if (roster == null || Campaign.Current == null) { Log.Info(P + "test - wynik: pominiety (brak sakw gracza)."); return; }
            var state = Save(roster);
            Log.Info(P + "test - zuzycie probne: poczatek | linie 'Drewno w kuzni (kg)', 'Wegiel w kuzni (kg)', 'Surowce w kuzni (kg) (m96-receptury)' i komunikaty kuzni "
                     + "do linii 'test - wynik' to kucia probne; liczniki doby drewna, wegla i przepisow z masy licza je, sakwy i pamiec wracaja do stanu sprzed testu.");
            int items = 0, ok = 0, added = 0;
            bool restored = true;
            foreach (var kv in ForgeBill.Samples())
            {
                items++;
                try { if (One(kv.Key, kv.Value, roster, ref added)) ok++; }
                catch (Exception e) { Stumble("One(" + kv.Value.StringId + ")", e); Log.Info(P + "test - " + kv.Value.StringId + " (" + kv.Key + ") | wyjatek " + e.GetType().Name + " - zgodnosc NIE."); }
                finally
                {
                    try { if (!Restore(roster, state)) restored = false; }
                    catch (Exception e) { restored = false; Stumble("Restore", e); }
                }
            }
            Log.Info(P + "test - wynik: przedmiotow " + items + ", zgodnych " + ok + ", dolozone na probe " + added + " szt., stan przywrocony " + YN(restored)
                     + ", potkniecia " + (_stumbles + ForgeBill.Stumbles) + ".");
        }

        private static bool One(string kind, ItemObject item, ItemRoster roster, ref int added)
        {
            var b = BillOf(kind, item);
            // brak w sakwach - dolozenie brakujacych sztuk na probe (zdejmie je kucie albo odtworzenie stanu)
            int add = 0;
            var addList = new List<string>();
            foreach (var w in b.Rows)
            {
                if (!w.Short || w.Item == null) continue;
                int n = w.ByKg ? w.Opens - w.Bags : (int)Math.Round(w.Need) - w.Bags;
                if (n <= 0) continue;
                roster.AddToCounts(w.Item, n);
                add += n;
                addList.Add(n + " x " + w.Item.StringId);
            }
            if (add > 0)
            {
                Log.Info(P + "test - " + item.StringId + ": dolozone na probe " + add + " szt. (" + string.Join(", ", addList.ToArray()) + ").");
                b = BillOf(kind, item);
            }
            added += add;
            if (b.Rows.Count == 0) { Log.Info(P + "test - " + item.StringId + " (" + kind + ") | rachunek pusty - zgodnosc NIE."); return false; }
            var bags0 = new int[b.Rows.Count];
            var open0 = new float[b.Rows.Count];
            for (int i = 0; i < b.Rows.Count; i++) { bags0[i] = Recipes.CountInInventory(b.Rows[i].Item); open0[i] = ForgeBill.OpenNow(b.Rows[i]); }
            // zuzycie ta sama droga co klikniecie
            string path;
            if (kind == "zbroja")
            {
                if (!FletchForge.SpendBill(item)) { Log.Info(P + "test - " + item.StringId + " (" + kind + ") | kwit BK nieznany (brak Banner Kings) - zgodnosc NIE."); return false; }
                FletchForge.MassSpend(item);
                Recipes.MassBkClear();
                path = "kwit BK + kg";
            }
            else if (kind == "bron")
            {
                var m = AccessTools.DeclaredMethod(typeof(CraftingCampaignBehavior), "SpendMaterials", new[] { typeof(WeaponDesign) });
                if (m == null) { Log.Info(P + "test - " + item.StringId + " (" + kind + ") | brak CraftingCampaignBehavior.SpendMaterials - zgodnosc NIE."); return false; }
                m.Invoke(null, new object[] { b.Design });
                path = "SpendMaterials gry";
            }
            else
            {
                Forge.TakeFor(item, b.Recipe);
                path = "Forge.TakeFor";
            }
            var parts = new List<string>();
            var diffs = new List<string>();
            for (int i = 0; i < b.Rows.Count; i++)
            {
                var w = b.Rows[i];
                int taken = bags0[i] - Recipes.CountInInventory(w.Item);
                string id = w.Item != null ? w.Item.StringId : "?";
                if (w.ByKg)
                {
                    float fromOpen = open0[i] - ForgeBill.OpenNow(w);
                    float used = taken * w.UnitKg + fromOpen;
                    float tol = Math.Max(0.001f, w.Need * 0.005f);
                    parts.Add(id + " potrzeba " + Kg(w.Need) + " kg, otwarcie " + w.Opens + " -> zuzyte " + Kg(used) + " kg (z sakw " + taken + " x " + Kg(w.UnitKg)
                              + ", z otwartych " + (fromOpen < 0 ? "-" + Kg(-fromOpen) : Kg(fromOpen)) + ")");
                    if (taken != w.Opens) diffs.Add(id + " otwarcie " + taken + " zamiast " + w.Opens);
                    if (Math.Abs(used - w.Need) > tol) diffs.Add(id + " kg " + used.ToString("0.####", Inv) + " zamiast " + w.Need.ToString("0.####", Inv));
                }
                else
                {
                    int need = (int)Math.Round(w.Need);
                    parts.Add(id + " potrzeba " + need + " szt. -> zuzyte " + taken + " szt.");
                    if (taken != need) diffs.Add(id + " " + taken + " szt. zamiast " + need);
                }
            }
            bool ok = diffs.Count == 0;
            Log.Info(P + "test - " + item.StringId + " (" + kind + ") | " + string.Join("; ", parts.ToArray()) + " | droga " + path + " - zgodnosc " + YN(ok)
                     + (ok ? "" : " (" + string.Join("; ", diffs.ToArray()) + ")") + ".");
            return ok;
        }

        // ============================================================ (c) test ekranu
        private static readonly string[] Kinds = { "bron", "zbroja", "luk", "strzaly" };

        private static string CrashScribeDir()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe");
        }

        /// <summary>ForgeView.ForgeBillMixin.TestStep (refleksja, raz na proces); false = brak ForgeView albo starszy.</summary>
        private static bool LookStep()
        {
            if (!_stepLooked)
            {
                _stepLooked = true;
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("ForgeView.ForgeBillMixin", false);
                        if (t != null) { _testStep = t.GetMethod("TestStep", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); break; }
                    }
                }
                catch (Exception e) { _testStep = null; Stumble("LookStep", e); }
            }
            return _testStep != null;
        }

        private static string StepRaw(int step, string id)
        {
            if (!LookStep()) return null;
            return _testStep.Invoke(null, new object[] { step, id }) as string;
        }

        /// <summary>ForgeJob.VmCtorPost (postfiks konstruktora CraftingVM, wpiety zawsze): VM ekranu otwartego przez test - tylko miedzy OpenCrafting a wyjsciem.</summary>
        internal static void NoteVm(CraftingVM vm)
        {
            if (_stage == 0 || _screenDone || vm == null) return;
            try { _vm = new WeakReference(vm); } catch { }
        }

        /// <summary>Wyjscie z ekranu kucia bez ForgeView (recenzja K1): ExecuteCancel na VM z otwarcia - ten sam koniec co Done/Esc (ForgeJob.CancelPost;
        /// najpierw zamyka okienka, stad kilka prob). Bez VM albo gdy ekran dalej stoi (np. etap koncowy kucia) - PopState: lepsze niz ekran do konca biegu
        /// (ForgeJob bez CancelPost zamknie swoja wizyte przy nastepnym otwarciu ekranu - "nowy ekran bez wyjscia z poprzedniego").</summary>
        private static string CloseScreen()
        {
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (gsm == null || !(gsm.ActiveState is CraftingState)) return "ekran zamkniety";
            var vm = _vm != null ? _vm.Target as CraftingVM : null;
            if (vm != null)
            {
                for (int i = 0; i < 4 && gsm.ActiveState is CraftingState; i++) vm.ExecuteCancel();
                if (!(gsm.ActiveState is CraftingState)) return "zamkniety (Armoury ExecuteCancel)";
            }
            gsm.PopState();
            return (gsm.ActiveState is CraftingState ? "BLAD: ekran dalej otwarty" : "zamkniety (Armoury PopState" + (vm == null ? ", brak VM" : "") + ")");
        }

        private static string SafeClose()
        {
            try { return CloseScreen(); }
            catch (Exception e) { Stumble("CloseScreen", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Powod, dla ktorego teraz nie otwieramy ekranu (null = mozna).</summary>
        private static string Blocker()
        {
            if (Mission.Current != null) return "misja";
            var ms = ScreenManager.TopScreen as MapScreen;
            if (ms == null || !ms.IsInMenu) return "nie menu osady";
            if (ms.IsEscapeMenuOpened || ms.IsInTownManagement || ms.IsInRecruitment || ms.IsInArmyManagement || ms.IsMapIncidentActive) return "okno";
            if (InformationManager.IsAnyInquiryActive()) return "okno";
            var cm = Campaign.Current.ConversationManager;
            if (cm != null && cm.IsConversationInProgress) return "rozmowa";
            if (!(Game.Current != null && Game.Current.GameStateManager != null && Game.Current.GameStateManager.ActiveState is MapState)) return "stan gry";
            var st = Settlement.CurrentSettlement;
            if (st == null || !st.IsTown) return "nie miasto";
            var beh = ArmouryBehavior.Instance;
            if (beh == null || beh.ProjectHoursLeftHere(st) > 0.01f) return "robota w tej kuzni";
            if (Hero.MainHero == null || Hero.MainHero.IsPrisoner) return "gracz";
            return null;
        }

        private static string IdFor(string kind)
        {
            foreach (var kv in ForgeBill.Samples()) if (kv.Key == kind) return kv.Value.StringId;
            return null;
        }

        /// <summary>SubModuleMain.OnApplicationTick (wlasny blok z licznikiem bledow ladowania typu): takt testu ekranu, co 0,5 s; bez pliku flagi - od razu wyjscie.</summary>
        internal static void AppTick(float dt)
        {
            _clock += dt;
            if (!_billsDone || _billsAt < 0f || _screenDone) return;
            _acc += dt;
            if (_acc < 0.5f) return;
            _acc = 0f;
            try { Step(); }
            catch (Exception e)
            {
                Stumble("AppTick", e);
                if (_stage != 0)
                {
                    string cl = SafeClose();
                    Log.Info(P + "test ekranu - pominiety (wyjatek " + e.GetType().Name + "; wyjscie z ekranu: " + cl + "), proba " + _tries + "/3.");
                }
                _stage = 0; _tries++; _at = _clock + 60f;
                if (_tries >= 3) _screenDone = true;
            }
        }

        private static void Step()
        {
            if (Campaign.Current == null) return;
            if (_stage == 0)
            {
                if (_tries >= 3) { _screenDone = true; Log.Info(P + "test ekranu - koniec: probek " + _screenN + ", zgodnych " + _screenOk + "."); return; }
                if (_clock - _billsAt < 30f || _clock < _at) return;
                if (!LookStep()) { _screenDone = true; Log.Info(P + "test ekranu - pominiety (brak ForgeView.ForgeBillMixin.TestStep)."); return; }
                if (Blocker() != null) return;
                CraftingTemplate tpl = null;
                foreach (var t in CraftingTemplate.All) { if (t != null && t.StringId == "OneHandedSword") { tpl = t; break; } }
                if (tpl == null && CraftingTemplate.All.Count > 0) tpl = CraftingTemplate.All[0];
                if (tpl == null) { _screenDone = true; Log.Info(P + "test ekranu - pominiety (brak szablonu broni)."); return; }
                _tries++;
                _stage = 1; _step = 0; _at = _clock + 2f; _openedAt = _clock; _pendingLine = null; _vm = null;   // _stage przed otwarciem - NoteVm lapie VM
                Helpers.CraftingHelper.OpenCrafting(tpl);
                return;
            }
            if (_clock - _openedAt > ScreenLimit)
            {
                string cl = SafeClose();
                Log.Info(P + "test ekranu - pominiety (limit " + ScreenLimit.ToString("0", Inv) + " s na ekranie; wyjscie z ekranu: " + cl + "), proba " + _tries + "/3.");
                _stage = 0; _at = _clock + 60f; _pendingLine = null;
                return;
            }
            if (_clock < _at) return;
            if (_pendingLine != null)
            {
                // zdjecie silnika po klatce z nowym rzedem, potem linia
                string shot;
                try
                {
                    string dir = Path.Combine(CrashScribeDir(), "zdjecia");
                    Directory.CreateDirectory(dir);
                    string name = "kuznia-m109-" + _pendingMode + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", Inv) + ".png";
                    TaleWorlds.Engine.Utilities.TakeScreenshot(Path.Combine(dir, name).Replace('\\', '/'));
                    shot = "zdjecia\\" + name;
                }
                catch (Exception e) { shot = "BLAD (" + e.GetType().Name + ")"; Stumble("Shot", e); }
                Log.Info(P + "test ekranu - " + _pendingLine + " | zdjecie " + shot + ".");
                _pendingLine = null;
                _at = _clock + 1f;
                return;
            }
            if (_step < Kinds.Length)
            {
                string kind = Kinds[_step];
                string id = kind == "bron" ? null : IdFor(kind);
                string res = StepRaw(_step, id);
                if (res == null || res.StartsWith("BLAD: ekran kucia nie otwarty", StringComparison.Ordinal))
                {
                    if (_step == 0 && _clock - _openedAt < 6f) { _at = _clock + 1f; return; }   // ekran moze ladowac sie kilka klatek
                    string cl = SafeClose();   // recenzja K1: ekran mogl sie otworzyc bez mixinu ForgeView - zamkniety tu, inaczej stoi do konca biegu
                    Log.Info(P + "test ekranu - pominiety (ekran kucia nie otworzyl sie: " + (res ?? "null") + "; wyjscie z ekranu: " + cl + "), proba " + _tries + "/3.");
                    _stage = 0; _at = _clock + 60f;
                    return;
                }
                _screenN++;
                if (res.IndexOf("| zgodne TAK", StringComparison.Ordinal) >= 0) _screenOk++;
                _pendingMode = kind;
                _pendingLine = kind + " | " + res + (res.StartsWith("BLAD", StringComparison.Ordinal) ? " | zgodne NIE" : "");
                _step++;
                _at = _clock + 1f;
                return;
            }
            // koniec: wyjscie z ekranu
            string close = null;
            try { close = StepRaw(9, null); } catch (Exception e) { Stumble("Close", e); }
            string after = SafeClose();   // recenzja K1: ForgeView nie zamknal (brak mixinu, wyjatek) - zamyka Armoury
            Log.Info(P + "test ekranu - koniec: probek " + _screenN + ", zgodnych " + _screenOk + " (wyjscie z ekranu: " + (close ?? "null") + " | po wyjsciu: " + after + ").");
            _screenDone = true;
            _stage = 0;
        }
    }
}
