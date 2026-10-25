using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P12 "przeglad armii v2" (klucz ArmyReviewPinTroopTypes). Jeff 25.10: "gdzie mozna stworzyc choragwie i przenosic wojsko ... i potem jak sie
    /// uruchamia bitwa to tak tez podzielona jest od razu armia"; 06:35 "no ale miales to zrobic". PRZYPIECIE TYPU WOJSKA DO CHORAGWI.
    ///
    /// Dane: StringId typu (szeregowy druzyny gracza, nie bohater) -> indeks formacji 0..7. Zapis w arm_army_review obok nazw (wpisy "p~typ~indeks",
    /// naglowek dalej "ar1" - stary kod pomija je jako zle wpisy i czyta nazwy). Kopia robocza w oknie choragwi, zapis razem z choragwiami ("Save");
    /// zwiniecie choragwi i "Return to the game's own order" zdejmuja przypiecia do niej / wszystkie.
    ///
    /// Udzialy (wagi) choragwi licza tylko NIEPRZYPIETYCH ludzi rodzaju (okno, Write, spawn i ekran Order of Battle - jedna zasada); przypieci stoja ponad udzialem.
    ///
    /// Bitwa (plan na misje - PinPlan, budowany przy pierwszym spawnie): bitwa polowa kampanii gracza (IsFieldBattle - nie oblezenie, wypad, morze; nie
    /// kryjowka), gracz nie w cudzej armii, zapis Order of Battle gry ma 8 wpisow, a choragiew przypiecia jest w nim wystawiona (klasa i waga) - inaczej
    /// przypiecie "czeka" (ludzie ida wedlug rodzaju). Tylko ludzie DRUZYNY GRACZA (PartyAgentOrigin.Party == MainParty; armie gracza dziela dalej wagi).
    ///  S1 spawn (latka czynnego BattleSpawnModel - zwykle SandboxBattleSpawnModel; metoda z klasy deklarujacej, pulapka 15.10): prefiks wyjmuje przypietych
    ///     z listy (podstawienie argumentu), model gry dzieli reszte wagami, postfiks dopisuje przypietych z indeksem ich choragwi. To samo dla posilkow
    ///     (GetReinforcementAssignments - MissionReinforcementsHelper). Postfiks zamienia zamiast dublowac, gdyby podstawienie nie weszlo (Dictionary.Add
    ///     w MissionBattleSideSpawnContext rzucilby na dublu).
    ///  S2 ekran Order of Battle (OrderOfBattleVM.Initialize -> DistributeAllTroops, zmiana wag i klas): postfiks OrderOfBattleUIHelper.GetExcludedAgentsForTransfer
    ///     dopisuje przypietych stojacych w swojej choragwi do wylaczonych z przenoszenia (jak kapitanow i chorazych gry), a postfiks
    ///     GetTotalCountOfUnitsInClass odejmuje ich z liczby klasy (wagi ekranu = wagi spawnu, RefreshWeights i SaveConfiguration bez dryfu).
    ///  S3 po rozstawieniu (ArmyReviewCheck.OnDeploymentFinished - PRZED BannerCompanies i widokami): przypieci, ktorych ekran przeniosl (zmiana klasy
    ///     choragwi), wracaja do swojej choragwi, jesli nie zostala pusta (gracz ja zwinal na ekranie - jego wybor); chorazych nie ruszamy.
    /// Druzyna AI gracza: RBM ManageFormationCounts i taktyki gry ruszaja tylko formacje IsAIControlled (gracz dowodzi - nie; "delegate command" - tak,
    /// to wybor gracza). Wyjatki liczone per miejsce (Stumble), bez wylacznika; latka nie wchodzi -> linia startu "NIE" i przypiecia nie dzialaja w bitwie.
    /// Linie: "przypiecia" w linii bitwy (spawn / ekran / po rozstawieniu / czekajace / typy bez przypiecia) i "przypiecia - koniec bitwy" (posilki).
    /// </summary>
    internal static partial class ArmyReview
    {
        // przypiecia zapisane (StringId typu -> indeks formacji) i kopia robocza okna choragwi (null - okno zamkniete)
        private static readonly Dictionary<string, int> _pins = new Dictionary<string, int>(StringComparer.Ordinal);
        private static Dictionary<string, int> _workPins;
        private static string _pinHooks = "nie zakladane";
        private static bool _pinSpawnHooked, _pinOobHooked;
        private static int _dPinSaves;

        internal static bool PinOn { get { var s = Settings.Current; return s != null && s.ArmyReviewEnabled && s.ArmyReviewPinTroopTypes; } }
        private static bool PinEdit { get { return PinOn && Writes; } }

        private static void PinReset()
        {
            _pins.Clear();
            _workPins = null;
            _plan = null;
            _dPinSaves = 0;
        }

        internal static int PinCount { get { return _pins.Count; } }

        /// <summary>StringId nadaje sie do zapisu (bez separatorow i znakow sterujacych).</summary>
        private static bool PinIdOk(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 120) return false;
            foreach (char c in id) if (c < ' ' || c == '|' || c == '~') return false;
            return true;
        }

        private static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> d)
        {
            var l = new List<KeyValuePair<string, int>>(d);
            l.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return l;
        }

        private static CharacterObject Troop(string id)
        {
            try { return MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<CharacterObject>(id) : null; } catch { return null; }
        }

        // ------------------------------------------------------------ liczenie
        internal sealed class TypeRow { public CharacterObject Ch; public int Fit, Wounded; }

        /// <summary>Typy szeregowych druzyny gracza: rodzaj, tier malejaco, nazwa.</summary>
        internal static List<TypeRow> PartyTypes()
        {
            var l = new List<TypeRow>();
            var r = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
            if (r == null) return l;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0 || !PinIdOk(ch.StringId)) continue;
                int w = Math.Max(0, Math.Min(el.Number, el.WoundedNumber));
                l.Add(new TypeRow { Ch = ch, Fit = el.Number - w, Wounded = w });
            }
            l.Sort((a, b) =>
            {
                int c = KindOf(a.Ch).CompareTo(KindOf(b.Ch));
                if (c != 0) return c;
                c = b.Ch.Tier.CompareTo(a.Ch.Tier);
                return c != 0 ? c : string.Compare(a.Ch.Name.ToString(), b.Ch.Name.ToString(), StringComparison.OrdinalIgnoreCase);
            });
            return l;
        }

        /// <summary>Zdrowi szeregowi druzyny gracza przypieci do WYSTAWIONEJ choragwi (slots), wedlug rodzaju 0..3; perBanner - ilu w kazdej choragwi.</summary>
        internal static int[] PinnedFit(Dictionary<string, int> pins, Slot[] slots, int[] perBanner)
        {
            var fit = new int[4];
            if (pins == null || pins.Count == 0 || slots == null) return fit;
            var r = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
            if (r == null) return fit;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                int idx;
                if (!pins.TryGetValue(ch.StringId ?? "", out idx) || idx < 0 || idx >= Slots || slots[idx] == null) continue;
                int n = el.Number - Math.Max(0, Math.Min(el.Number, el.WoundedNumber));
                fit[KindOf(ch)] += n;
                if (perBanner != null) perBanner[idx] += n;
            }
            return fit;
        }

        /// <summary>Zdrowi szeregowi wedlug rodzaju do UDZIALOW choragwi: wszyscy, ktorymi gracz dowodzi, minus przypieci do wystawionych choragwi.</summary>
        internal static int[] ShareFit(Slot[] slots, Dictionary<string, int> pins)
        {
            var fit = CommandedFit();
            var p = PinnedFit(pins, slots, null);
            for (int k = 0; k < 4; k++) fit[k] = Math.Max(0, fit[k] - p[k]);
            return fit;
        }

        private static int PinnedIn(int i)
        {
            if (_workPins == null || _work == null) return 0;
            var per = new int[Slots];
            PinnedFit(_workPins, _work, per);
            return per[i];
        }

        private static List<string> PinNames(Dictionary<string, int> pins, int i)
        {
            var l = new List<string>();
            if (pins == null) return l;
            foreach (var kv in Sorted(pins))
            {
                if (kv.Value != i) continue;
                var ch = Troop(kv.Key);
                l.Add(ch != null ? ch.Name.ToString() : kv.Key);
            }
            return l;
        }

        /// <summary>Zapis przypiec po udanym Write: przypiecia do zwinietych choragwi odpadaja (liczone), reszta zastepuje zapisane.</summary>
        private static void CommitPins(WriteResult r, Dictionary<string, int> pins)
        {
            if (r == null || !r.Ok || pins == null) return;
            _pins.Clear();
            if (r.AsReset) { r.PinDropped += pins.Count; return; }
            foreach (var kv in pins)
            {
                if (!PinIdOk(kv.Key) || kv.Value < 0 || kv.Value >= Slots || r.Banners == null || r.Banners[kv.Value] == null) { r.PinDropped++; continue; }
                _pins[kv.Key] = kv.Value;
            }
            r.PinTypes = _pins.Count;
            var per = new int[Slots];
            var f = PinnedFit(_pins, r.Banners, per);
            r.PinMen = f[0] + f[1] + f[2] + f[3];
            r.PinCommitted = true;
        }

        private static string PinSummary(Dictionary<string, int> pins)
        {
            if (pins == null || pins.Count == 0) return "brak";
            var sb = new StringBuilder();
            foreach (var kv in Sorted(pins))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key).Append("->").Append(kv.Value < Slots && kv.Value >= 0 ? Roman[kv.Value] : "?");
            }
            return pins.Count + " (" + sb + ")";
        }

        // ------------------------------------------------------------ okno: typy wojska
        private static void OpenPins()
        {
            if (_work == null) { OpenBanners(true); return; }
            if (_workPins == null) _workPins = new Dictionary<string, int>(_pins, StringComparer.Ordinal);
            var types = PartyTypes();
            var rows = new List<InquiryElement>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in types)
            {
                seen.Add(t.Ch.StringId);
                rows.Add(new InquiryElement(t.Ch.StringId, TypeTitle(t.Ch, t.Fit, t.Wounded), TroopPortrait(t.Ch), true, TypeHint(t.Ch)));
            }
            // przypiecia typow, ktorych nie ma teraz w druzynie - do odpiecia
            foreach (var kv in Sorted(_workPins))
            {
                if (seen.Contains(kv.Key)) continue;
                var ch = Troop(kv.Key);
                rows.Add(new InquiryElement(kv.Key, (ch != null ? ch.Name.ToString() : kv.Key) + " - none in your party now - " + Where(kv.Key), ch != null ? TroopPortrait(ch) : null, true,
                                            "The pin waits for men of this type. Pick it to move it or to send the type back to sorting by kind."));
            }
            if (rows.Count == 0)
            {
                InformationManager.ShowInquiry(new InquiryData("Move troop types into banners", "You have no troops in your party to place - only companions, who lead banners or stand in their ranks.",
                    true, false, "Back", null, delegate { try { OpenBanners(false); } catch (Exception e) { Stumble("Pins.Empty", e); } }, null), true);
                return;
            }
            int placed = 0;
            foreach (var kv in _workPins) if (kv.Value >= 0 && kv.Value < Slots && _work[kv.Value] != null) placed++;
            string d = "Pick one or more troop types of your own party, then the banner they stand in. Men of a type you place stand in that banner when a field battle"
                       + " opens, and their reinforcements join it; the banners' shares count only the men you have not placed. Types left by kind follow the shares."
                       + " Companions keep their places as commanders and in the ranks. " + placed + (placed == 1 ? " type is" : " types are") + " placed now."
                       + (_dirty ? " Save the banners to keep the changes." : "");
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Move troop types into banners", d, rows, true, 1, rows.Count, "Choose the banner", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        var ids = new List<string>();
                        if (picked != null) foreach (var e in picked) { var s = e.Identifier as string; if (s != null && !ids.Contains(s)) ids.Add(s); }
                        if (ids.Count == 0) { OpenPins(); return; }
                        OpenPinTarget(ids);
                    }
                    catch (Exception e) { Stumble("Pins.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenBanners(false); } catch (Exception e) { Stumble("Pins.Back", e); } }), true);
        }

        private static ImageIdentifier TroopPortrait(CharacterObject ch)
        {
            try { return new CharacterImageIdentifier(CharacterCode.CreateFrom(ch)); } catch { return null; }
        }

        private static string TypeTitle(CharacterObject ch, int fit, int wounded)
        {
            return ch.Name + " - tier " + ch.Tier + ", " + KindName(KindOf(ch) + 1).ToLowerInvariant() + (ch.IsMounted ? ", mounted" : ", on foot")
                   + ", x" + fit + (wounded > 0 ? " (+" + wounded + " wounded)" : "") + " - " + Where(ch.StringId);
        }

        private static string TypeHint(CharacterObject ch)
        {
            string w = Where(ch.StringId);
            return "Kind: " + KindName(KindOf(ch) + 1) + ". Now: " + w + "."
                   + (ch.IsMounted ? "\nRiders keep their horses in any banner; a rider without a horse or harness still fights on foot." : "");
        }

        /// <summary>Gdzie stoi typ w kopii roboczej: "in banner II (Name)", "banner III is not raised - by kind", "by kind".</summary>
        private static string Where(string id)
        {
            int idx;
            if (_workPins == null || !_workPins.TryGetValue(id, out idx) || idx < 0 || idx >= Slots) return "by kind";
            if (_work == null || _work[idx] == null) return "banner " + Roman[idx] + " is not raised - by kind until it is";
            return "in banner " + Label(idx);
        }

        private static void OpenPinTarget(List<string> ids)
        {
            var fit = ShareFit(_work, _workPins);
            var w1 = new int[Slots]; var w2 = new int[Slots];
            Weights(_work, fit, w1, w2);
            var rows = new List<InquiryElement>();
            int mounted = 0, foot = 0;
            foreach (var id in ids) { var ch = Troop(id); if (ch == null) continue; if (ch.IsMounted) mounted++; else foot++; }
            for (int i = 0; i < Slots; i++)
            {
                if (_work[i] == null) continue;
                var s = _work[i];
                bool mix = (s.Cls == 1 || s.Cls == 2 || s.Cls == 5) ? mounted > 0 : (s.Cls >= 3 ? foot > 0 : false);
                rows.Add(new InquiryElement(i, Label(i) + " " + Approx(i, fit, w1) + PinnedTail(i), null, true,
                                            "Kind: " + KindName(s.Cls) + "." + (mix ? "\nThe men you picked are of another kind - they stand and fight with this banner all the same." : "")));
            }
            rows.Add(new InquiryElement(-1, "By kind - no banner of their own", null, true, "The types go back to the banners of their kind and their shares."));
            string q = ids.Count == 1 ? "Where does " + TypeName(ids[0]) + " stand?" : "Where do these " + ids.Count + " troop types stand?";
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Move troop types into banners", q, rows, true, 1, 1, "Place them", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        if (picked != null && picked.Count > 0 && picked[0].Identifier is int)
                        {
                            int idx = (int)picked[0].Identifier;
                            int changed = 0;
                            foreach (var id in ids)
                            {
                                int cur;
                                bool had = _workPins.TryGetValue(id, out cur);
                                if (idx < 0) { if (had) { _workPins.Remove(id); changed++; } }
                                else if (!had || cur != idx) { _workPins[id] = idx; changed++; }
                            }
                            if (changed > 0) _dirty = true;
                        }
                        OpenPins();
                    }
                    catch (Exception e) { Stumble("PinTarget.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenPins(); } catch (Exception e) { Stumble("PinTarget.Back", e); } }), true);
        }

        private static string TypeName(string id) { var ch = Troop(id); return ch != null ? ch.Name.ToString() : id; }

        /// <summary>" + N placed" za przyblizeniem choragwi (kopia robocza okna).</summary>
        private static string PinnedTail(int i)
        {
            int n = PinnedIn(i);
            return n > 0 ? " + " + n + " placed" : "";
        }

        /// <summary>Przypiecia do choragwi zwinietej w oknie odpadaja od razu (komunikat dla gracza).</summary>
        private static void DropPinsTo(int i)
        {
            if (_workPins == null) return;
            var gone = new List<string>();
            foreach (var kv in _workPins) if (kv.Value == i) gone.Add(kv.Key);
            foreach (var id in gone) _workPins.Remove(id);
            if (gone.Count > 0) Log.Player(gone.Count + (gone.Count == 1 ? " troop type placed in banner " : " troop types placed in banner ") + Roman[i] + " go back to sorting by kind.");
        }

        // ------------------------------------------------------------ latki (NightHooks.InCampaign, raz na proces)
        internal static string ApplyPinHooks(Harmony h)
        {
            if (h == null) { _pinHooks = "BRAK instancji Harmony"; return _pinHooks; }
            string init = "NIE", reinf = "NIE", excl = "NIE", count = "NIE", model = "?";
            var args = new[] { typeof(BattleSideEnum), typeof(List<IAgentOriginBase>) };
            object active = null;
            try { active = MissionGameModels.Current != null ? MissionGameModels.Current.BattleSpawnModel : null; } catch { active = null; }
            model = active != null ? active.GetType().FullName : "brak modelu - SandboxBattleSpawnModel";
            try { init = PatchSpawn(h, active, "GetInitialSpawnAssignments", args, nameof(InitPrefix), nameof(InitPostfix)); }
            catch (Exception e) { init = "NIE (" + e.GetType().Name + ")"; Stumble("Hooks.Init", e); }
            try { reinf = PatchSpawn(h, active, "GetReinforcementAssignments", args, nameof(ReinfPrefix), nameof(ReinfPostfix)); }
            catch (Exception e) { reinf = "NIE (" + e.GetType().Name + ")"; Stumble("Hooks.Reinf", e); }
            _pinSpawnHooked = init == "TAK";
            try
            {
                var t = AccessTools.TypeByName("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleUIHelper");
                var tItem = AccessTools.TypeByName("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleFormationItemVM");
                _pFormation = tItem != null ? AccessTools.Property(tItem, "Formation") : null;
                if (t == null || tItem == null || _pFormation == null) excl = count = "NIE (brak typow ekranu Order of Battle)";
                else
                {
                    var mEx = AccessTools.DeclaredMethod(t, "GetExcludedAgentsForTransfer", new[] { tItem, typeof(FormationClass) });
                    excl = Hook(h, mEx, new[] { "formationVM", "formationClass" }, null, nameof(ExcludePostfix));
                    var mCnt = AccessTools.DeclaredMethod(t, "GetTotalCountOfUnitsInClass", new[] { typeof(Formation), typeof(FormationClass) });
                    count = Hook(h, mCnt, new[] { "formation", "fc" }, null, nameof(CountPostfix));
                }
            }
            catch (Exception e) { excl = "NIE (" + e.GetType().Name + ")"; Stumble("Hooks.Oob", e); }
            _pinOobHooked = excl == "TAK" && count == "TAK";
            _pinHooks = "spawn " + init + ", posilki " + reinf + " (model " + model + "), ekran Order of Battle: wylaczeni z przenoszenia " + excl + ", liczba klasy " + count;
            return _pinHooks;
        }

        private static string PatchSpawn(Harmony h, object active, string name, Type[] args, string pre, string post)
        {
            var m = PatchTarget.OfActiveOr(active, typeof(SandBox.GameComponents.SandboxBattleSpawnModel), name, args, "ArmyReview.Pin." + name);
            if (m == null) return "NIE (brak metody " + name + ")";
            if (m.IsAbstract) return "NIE (metoda abstrakcyjna w " + m.DeclaringType.Name + ")";
            return Hook(h, m, new[] { "battleSide", "troopOrigins" }, pre, post);
        }

        /// <summary>Latka z kontrola nazw parametrow (prefiks/postfiks wiaza je po nazwie) i odczytem Harmony.GetPatchInfo na metodzie deklarujacej.</summary>
        private static string Hook(Harmony h, MethodInfo m, string[] names, string pre, string post)
        {
            if (m == null) return "NIE (brak metody)";
            var ps = m.GetParameters();
            if (ps.Length != names.Length) return "NIE (inne parametry " + m.DeclaringType.Name + "." + m.Name + ")";
            for (int i = 0; i < names.Length; i++)
                if (names[i] != null && ps[i].Name != names[i]) return "NIE (parametr " + i + " to '" + ps[i].Name + "', nie '" + names[i] + "')";
            h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(ArmyReview), pre) : null, postfix: post != null ? new HarmonyMethod(typeof(ArmyReview), post) : null);
            var info = Harmony.GetPatchInfo(m);
            bool okPre = pre == null, okPost = post == null;
            if (info != null)
            {
                foreach (var p in info.Prefixes) if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(ArmyReview) && p.PatchMethod.Name == pre) okPre = true;
                foreach (var p in info.Postfixes) if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(ArmyReview) && p.PatchMethod.Name == post) okPost = true;
            }
            return okPre && okPost ? "TAK" : "NIE (latka nie weszla - slad w BKROTLog.txt)";
        }

        internal static string PinHooksNote { get { return _pinHooks; } }

        // ------------------------------------------------------------ plan bitwy
        internal sealed class PinPlan
        {
            public Mission M;
            public bool Active;
            public string Why = "-";
            public PartyBase Main;
            public BattleSideEnum Side = BattleSideEnum.None;
            public readonly Dictionary<string, int> Target = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly HashSet<string> Waiting = new HashSet<string>(StringComparer.Ordinal);
            public readonly int[] Spawn = new int[Slots], Reinf = new int[Slots], Deploy = new int[Slots];
            public int SpawnCalls, ReinfCalls, WaitMen, Replaced, DeployEmpty, Excluded, Counted;
        }

        private static PinPlan _plan;
        private static PropertyInfo _pFormation;

        /// <summary>Plan przypiec tej misji (budowany raz; null - brak misji).</summary>
        internal static PinPlan PlanFor(Mission m)
        {
            if (m == null) return null;
            if (_plan != null && ReferenceEquals(_plan.M, m)) return _plan;
            var p = new PinPlan { M = m };
            try { Build(p, m); }
            catch (Exception e) { p.Active = false; p.Why = "blad planu (" + e.GetType().Name + ")"; Stumble("Plan", e); }
            _plan = p;
            return p;
        }

        private static void Build(PinPlan p, Mission m)
        {
            var mp = MobileParty.MainParty;
            if (!PinOn) { p.Why = "klucz Army Review Pin Troop Types NIE"; return; }
            if (_pins.Count == 0) { p.Why = "brak przypiec"; return; }
            if (Campaign.Current == null || mp == null || MapEvent.PlayerMapEvent == null) { p.Why = "nie bitwa kampanii gracza"; return; }
            if (!m.IsFieldBattle || HideoutAlarm.IsHideout(m)) { p.Why = "nie bitwa polowa"; return; }
            if (mp.Army != null && mp.Army.LeaderParty != mp) { p.Why = "gracz w cudzej armii (dowodzi wodz armii)"; return; }
            var b = Behavior();
            if (b == null) { p.Why = "brak OrderOfBattleCampaignBehavior"; return; }
            bool army = mp.Army != null;
            for (int i = 0; i < Slots; i++) if (b.GetFormationDataAtIndex(i, false, army) == null) { p.Why = "zapis Order of Battle gry bez 8 wpisow (porzadek gry)"; foreach (var kv in _pins) p.Waiting.Add(kv.Key); return; }
            foreach (var kv in _pins)
            {
                var d = kv.Value >= 0 && kv.Value < Slots ? b.GetFormationDataAtIndex(kv.Value, false, army) : null;
                int cls = d != null ? (int)d.FormationClass : 0;
                if (d != null && cls >= 1 && cls <= 6 && (d.PrimaryClassWeight > 0 || d.SecondaryClassWeight > 0)) p.Target[kv.Key] = kv.Value;
                else p.Waiting.Add(kv.Key);
            }
            p.Main = mp.Party;
            try { p.Side = MapEvent.PlayerMapEvent.PlayerSide; } catch { p.Side = BattleSideEnum.None; }
            p.Active = p.Target.Count > 0 && p.Main != null;
            p.Why = p.Active ? "czynne" : "choragwie przypiec nie wystawione w zapisie gry";
        }

        /// <summary>Plan testu (bez misji): tylko mapa typ -> indeks i druzyna gracza.</summary>
        internal static PinPlan TestPlan(Dictionary<string, int> targets)
        {
            var p = new PinPlan { Active = true, Why = "test", Main = MobileParty.MainParty != null ? MobileParty.MainParty.Party : null };
            foreach (var kv in targets) p.Target[kv.Key] = kv.Value;
            return p;
        }

        private static PinPlan Live(BattleSideEnum side)
        {
            if (Campaign.Current == null || _pins.Count == 0) return null;   // bez planu (i bez trzymania misji) poza kampania i bez przypiec
            var p = PlanFor(Mission.Current);
            if (p == null || !p.Active) return null;
            if (p.Side != BattleSideEnum.None && side != p.Side) return null;
            return p;
        }

        /// <summary>Indeks choragwi przypietego czlowieka DRUZYNY GRACZA (-1 - nie przypiety albo nie z druzyny gracza).</summary>
        internal static int PinOf(PinPlan p, IAgentOriginBase o)
        {
            if (p == null || o == null) return -1;
            var t = o.Troop as CharacterObject;
            if (t == null || t.IsHero) return -1;
            var party = o.BattleCombatant as PartyBase;
            if (party == null || !ReferenceEquals(party, p.Main)) return -1;
            int idx;
            if (!p.Target.TryGetValue(t.StringId ?? "", out idx)) return -1;
            return idx >= 0 && idx < Slots ? idx : -1;
        }

        /// <summary>Czlowiek druzyny gracza typu, ktorego choragiew nie jest wystawiona (przypiecie czeka - idzie wedlug rodzaju).</summary>
        private static bool Waits(PinPlan p, IAgentOriginBase o)
        {
            if (p == null || o == null || p.Waiting.Count == 0) return false;
            var t = o.Troop as CharacterObject;
            return t != null && !t.IsHero && ReferenceEquals(o.BattleCombatant as PartyBase, p.Main) && p.Waiting.Contains(t.StringId ?? "");
        }

        private sealed class RefEq : IEqualityComparer<IAgentOriginBase>
        {
            internal static readonly RefEq I = new RefEq();
            public bool Equals(IAgentOriginBase a, IAgentOriginBase b) { return ReferenceEquals(a, b); }
            public int GetHashCode(IAgentOriginBase o) { return RuntimeHelpers.GetHashCode(o); }
        }

        /// <summary>Rozdzial listy: reszta (dla modelu gry) i przypieci z indeksem. false - nikt nie przypiety (lista bez zmian, bez alokacji).</summary>
        internal static bool Split(PinPlan p, List<IAgentOriginBase> origins, out List<IAgentOriginBase> rest, out List<(IAgentOriginBase origin, int formationIndex)> pinned)
        {
            rest = null; pinned = null;
            if (p == null || origins == null || origins.Count == 0) return false;
            for (int i = 0; i < origins.Count; i++)
            {
                var o = origins[i];
                int idx = PinOf(p, o);
                if (idx >= 0)
                {
                    if (rest == null)
                    {
                        rest = new List<IAgentOriginBase>(origins.Count);
                        for (int j = 0; j < i; j++) rest.Add(origins[j]);
                        pinned = new List<(IAgentOriginBase, int)>();
                    }
                    pinned.Add((o, idx));
                }
                else
                {
                    if (rest != null) rest.Add(o);
                    if (Waits(p, o)) p.WaitMen++;
                }
            }
            return rest != null;
        }

        /// <summary>Dopisanie przypietych do wyniku modelu gry; czlowiek juz w wyniku (podstawienie argumentu nie weszlo) - zamiana indeksu zamiast dubla.</summary>
        internal static int Merge(List<(IAgentOriginBase origin, int formationIndex)> result, List<(IAgentOriginBase origin, int formationIndex)> pinned)
        {
            int replaced = 0;
            var pos = new Dictionary<IAgentOriginBase, int>(RefEq.I);
            for (int i = 0; i < result.Count; i++) { var o = result[i].origin; if (o != null && !pos.ContainsKey(o)) pos[o] = i; }
            foreach (var x in pinned)
            {
                int at;
                if (x.origin != null && pos.TryGetValue(x.origin, out at)) { result[at] = x; replaced++; }
                else { if (x.origin != null) pos[x.origin] = result.Count; result.Add(x); }
            }
            return replaced;
        }

        // prefiksy / postfiksy spawnu: blad = lista gry bez zmian (podstawienie dopiero po udanym podziale)
        private static void InitPrefix(BattleSideEnum battleSide, ref List<IAgentOriginBase> troopOrigins, out List<(IAgentOriginBase origin, int formationIndex)> __state)
        {
            __state = null;
            try
            {
                var p = Live(battleSide);
                if (p == null) return;
                p.SpawnCalls++;
                List<IAgentOriginBase> rest;
                List<(IAgentOriginBase, int)> pinned;
                if (!Split(p, troopOrigins, out rest, out pinned)) return;
                __state = pinned;
                troopOrigins = rest;
            }
            catch (Exception e) { __state = null; Stumble("Spawn.Prefix", e); }
        }

        private static void InitPostfix(ref List<(IAgentOriginBase origin, int formationIndex)> __result, List<(IAgentOriginBase origin, int formationIndex)> __state)
        {
            if (__state == null) return;
            try
            {
                if (__result == null) __result = new List<(IAgentOriginBase, int)>();
                var p = _plan;
                int rep = Merge(__result, __state);
                if (p != null) { p.Replaced += rep; foreach (var x in __state) p.Spawn[x.formationIndex]++; }
            }
            catch (Exception e) { Stumble("Spawn.Postfix", e); }
        }

        private static void ReinfPrefix(BattleSideEnum battleSide, ref List<IAgentOriginBase> troopOrigins, out List<(IAgentOriginBase origin, int formationIndex)> __state)
        {
            __state = null;
            try
            {
                var p = Live(battleSide);
                if (p == null) return;
                p.ReinfCalls++;
                List<IAgentOriginBase> rest;
                List<(IAgentOriginBase, int)> pinned;
                if (!Split(p, troopOrigins, out rest, out pinned)) return;
                __state = pinned;
                troopOrigins = rest;
            }
            catch (Exception e) { __state = null; Stumble("Reinf.Prefix", e); }
        }

        private static void ReinfPostfix(ref List<(IAgentOriginBase origin, int formationIndex)> __result, List<(IAgentOriginBase origin, int formationIndex)> __state)
        {
            if (__state == null) return;
            try
            {
                if (__result == null) __result = new List<(IAgentOriginBase, int)>();
                var p = _plan;
                int rep = Merge(__result, __state);
                if (p != null) { p.Replaced += rep; foreach (var x in __state) p.Reinf[x.formationIndex]++; }
            }
            catch (Exception e) { Stumble("Reinf.Postfix", e); }
        }

        /// <summary>Agent druzyny gracza przypietego typu (plan tej misji czynny) - linia bitwy liczy go poza zgodnoscia klasy choragwi.</summary>
        internal static bool PinnedAgent(Mission m, Agent a)
        {
            var p = _plan;
            if (p == null || !p.Active || !ReferenceEquals(p.M, m) || a == null) return false;
            return PinOf(p, a.Origin) >= 0;
        }

        /// <summary>Przypiety czlowiek stojacy w swojej choragwi (agent druzyny gracza, nie chorazy).</summary>
        private static bool PinnedHere(PinPlan p, Agent a, int formationIndex)
        {
            if (a == null || !a.IsHuman || a.Banner != null) return false;
            return PinOf(p, a.Origin) == formationIndex;
        }

        private static void ExcludePostfix(object formationVM, List<Agent> __result)
        {
            try
            {
                var p = _plan;
                if (p == null || !p.Active || __result == null || formationVM == null || _pFormation == null) return;
                var m = Mission.Current;
                if (m == null || !ReferenceEquals(p.M, m)) return;
                var f = _pFormation.GetValue(formationVM, null) as Formation;
                if (f == null || f.Team == null || !f.Team.IsPlayerTeam) return;
                int idx = f.Index;
                var add = new List<Agent>();
                f.ApplyActionOnEachUnit(delegate (Agent a) { if (PinnedHere(p, a, idx) && !__result.Contains(a)) add.Add(a); });
                if (add.Count > 0) { __result.AddRange(add); p.Excluded += add.Count; }
            }
            catch (Exception e) { Stumble("Oob.Exclude", e); }
        }

        private static void CountPostfix(Formation formation, FormationClass fc, ref int __result)
        {
            try
            {
                var p = _plan;
                if (p == null || !p.Active || __result <= 0 || formation == null || formation.Team == null || !formation.Team.IsPlayerTeam) return;
                var m = Mission.Current;
                if (m == null || !ReferenceEquals(p.M, m)) return;
                int idx = formation.Index, n = 0;
                formation.ApplyActionOnEachUnit(delegate (Agent a) { if (PinnedHere(p, a, idx) && InClass(a, fc)) n++; });
                if (n > 0) { __result = Math.Max(0, __result - n); p.Counted++; }
            }
            catch (Exception e) { Stumble("Oob.Count", e); }
        }

        /// <summary>Te same predykaty co OrderOfBattleUIHelper.IsAgentInFormationClass.</summary>
        private static bool InClass(Agent a, FormationClass fc)
        {
            switch (fc)
            {
                case FormationClass.Infantry: return QueryLibrary.IsInfantry(a);
                case FormationClass.Ranged: return QueryLibrary.IsRanged(a);
                case FormationClass.Cavalry: return QueryLibrary.IsCavalry(a);
                case FormationClass.HorseArcher: return QueryLibrary.IsRangedCavalry(a);
                default: return false;
            }
        }

        // ------------------------------------------------------------ po rozstawieniu i koniec bitwy
        /// <summary>S3: przypieci poza swoja choragwia (ekran ich przeniosl) wracaja do niej; pusta choragiew (zwinieta na ekranie) - zostaja, liczone.</summary>
        internal static void PinDeploy(Mission m)
        {
            var p = _plan != null && ReferenceEquals(_plan.M, m) ? _plan : null;
            if (p == null || !p.Active) return;
            var team = m.PlayerTeam;
            if (team == null) return;
            var moves = new List<KeyValuePair<Agent, Formation>>();
            foreach (var a in team.ActiveAgents)
            {
                if (a == null || !a.IsHuman || a.Banner != null || a.IsMainAgent) continue;
                int idx = PinOf(p, a.Origin);
                if (idx < 0) continue;
                var f = a.Formation;
                if (f != null && f.Index == idx) continue;
                var t = team.GetFormation((FormationClass)idx);
                if (t == null) continue;
                if (t.CountOfUnits == 0) { p.DeployEmpty++; continue; }
                moves.Add(new KeyValuePair<Agent, Formation>(a, t));
            }
            if (moves.Count == 0) return;
            var touched = new List<Formation>();
            foreach (var mv in moves)
            {
                if (mv.Key.Formation != null && !touched.Contains(mv.Key.Formation)) touched.Add(mv.Key.Formation);
                if (!touched.Contains(mv.Value)) touched.Add(mv.Value);
            }
            foreach (var f in touched) f.OnMassUnitTransferStart();
            try
            {
                foreach (var mv in moves)
                {
                    try { mv.Key.Formation = mv.Value; p.Deploy[mv.Value.Index]++; }
                    catch (Exception e) { Stumble("Deploy.Move", e); }
                }
            }
            finally
            {
                foreach (var f in touched)
                {
                    try { team.TriggerOnFormationsChanged(f); f.OnMassUnitTransferEnd(); } catch (Exception e) { Stumble("Deploy.End", e); }
                }
            }
        }

        private static string PerBanner(int[] a)
        {
            var sb = new StringBuilder();
            int sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] <= 0) continue;
                sum += a[i];
                if (sb.Length > 0) sb.Append(',');
                sb.Append(i).Append(':').Append(a[i]);
            }
            return sum + (sb.Length > 0 ? " (" + sb + ")" : "");
        }

        /// <summary>Segment linii bitwy: plan, ilu stanelo gdzie, typy bez przypiecia (ludzie druzyny gracza na polu).</summary>
        internal static string PinBattleSegment(Mission m, Team team)
        {
            var p = _plan != null && ReferenceEquals(_plan.M, m) ? _plan : null;
            if (p == null) return " | przypiecia: " + (PinOn ? (_pins.Count == 0 ? "brak" : "bez spawnu w tej misji") : "klucz NIE");
            var sb = new StringBuilder(" | przypiecia: ").Append(p.Why);
            if (!p.Active) { if (p.Waiting.Count > 0) sb.Append(", czeka typow ").Append(p.Waiting.Count); return sb.ToString(); }
            int free = 0, freeMen = 0, pinMen = 0, outside = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var where = new int[Slots];
            if (team != null)
                foreach (var a in team.ActiveAgents)
                {
                    if (a == null || !a.IsHuman) continue;
                    var party = a.Origin != null ? a.Origin.BattleCombatant as PartyBase : null;
                    var t = a.Character as CharacterObject;
                    if (party == null || !ReferenceEquals(party, p.Main) || t == null || t.IsHero) continue;
                    int idx;
                    if (p.Target.TryGetValue(t.StringId ?? "", out idx))
                    {
                        pinMen++;
                        if (a.Formation != null && a.Formation.Index == idx) where[idx]++; else outside++;
                    }
                    else { freeMen++; if (seen.Add(t.StringId ?? "")) free++; }
                }
            sb.Append(" (typow ").Append(p.Target.Count).Append(", czeka ").Append(p.Waiting.Count).Append(") | spawn przypietych ").Append(PerBanner(p.Spawn))
              .Append(", ekran: wylaczeni z przenoszenia ").Append(p.Excluded).Append(", odjecia z liczby klasy ").Append(p.Counted)
              .Append(", po rozstawieniu przeniesiono ").Append(PerBanner(p.Deploy)).Append(" (choragiew pusta - zostali ").Append(p.DeployEmpty).Append(')')
              .Append(" | na polu przypietych ").Append(pinMen).Append(" - w swojej choragwi ").Append(PerBanner(where)).Append(", poza nia ").Append(outside)
              .Append(" | typow bez przypiecia ").Append(free).Append(" (ludzi ").Append(freeMen).Append("), ludzi typow czekajacych ").Append(p.WaitMen);
            if (p.Replaced > 0) sb.Append(" | ZAMIANY w wyniku modelu ").Append(p.Replaced).Append(" (podstawienie listy nie weszlo)");
            return sb.ToString();
        }

        /// <summary>Koniec misji: linia posilkow (gdy plan czynny) i zwolnienie planu (bez trzymania misji w pamieci).</summary>
        internal static void PinMissionEnd(Mission m)
        {
            var p = _plan;
            if (p == null || !ReferenceEquals(p.M, m)) return;
            _plan = null;
            if (!p.Active) return;
            Log.Info(P + "przypiecia - koniec bitwy | spawn przypietych " + PerBanner(p.Spawn) + " (wywolan " + p.SpawnCalls + "), posilki przypietych " + PerBanner(p.Reinf)
                     + " (wywolan " + p.ReinfCalls + "), po rozstawieniu " + PerBanner(p.Deploy) + ", ludzi typow czekajacych " + p.WaitMen
                     + (p.Replaced > 0 ? ", ZAMIANY " + p.Replaced : "") + ".");
        }
    }
}
