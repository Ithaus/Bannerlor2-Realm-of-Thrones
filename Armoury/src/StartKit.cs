using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// SPRZET STARTOWY, KTORY DA SIE NOSIC (Jeff 04.10: "dostalem mase sprzetu, jak zaczalem
    /// gre, ale nie moge go nosic - nie powinienem dostawac rzeczy, ktorych nie moge nosic").
    /// Kreator postaci (pochodzenie ROT/BK) daje komplet wedle pochodzenia - np. plyte rycerza-
    /// rabusia - a Prawo Wagi CrashScribe (od 09.10 Atletyka = waga / 0.333 kg) stawia mu wymagania, ktorych nowa
    /// postac nie ma. Gra zrzuca to do taboru na czerwono.
    ///
    /// Raz, w pierwszej godzinie po kreatorze (wszystkie mody juz rozdaly swoje): kazda sztuke
    /// w taborze i na postaci, ktorej bohater nie moze uzyc, zamieniamy na NAJLEPSZA sztuke
    /// tego samego typu, ktora moze nosic - tej samej kultury albo neutralna, nie unikat, nie
    /// drozsza od oryginalu. Nic nie przybywa z niczego: tansze za drozsze. Brak zamiennika =
    /// sztuka zostaje (log).
    /// </summary>
    internal static class StartKit
    {
        internal static void Reset() { _pending = false; _newCampaign = false; _cutDone = false; _tableLogged = false; }
        private static bool _pending;

        /// <summary>r4-zloto: koniec tworzenia postaci widziany w tej sesji = nowa kampania (zdarzenie gry OnCharacterCreationIsOver biegnie tylko
        /// w nowej kampanii; BK RunStartOption z okna startu przychodzi PO nim, bo okno BK otwiera sie w tym samym zdarzeniu i czeka na gracza).</summary>
        internal static void OnCharacterCreationOver() { _pending = true; _newCampaign = true; }

        internal static void Hourly()
        {
            if (!_pending) return;
            _pending = false;
            try { var ss = Settings.Current; if (ss != null && ss.StartKitEnabled) Run(); } catch (Exception e) { Log.Error("StartKit", e); }
            try { EnforceStartGold(); } catch (Exception e) { Log.Error("StartKit.Gold", e); }
        }

        /// <summary>sklad9-p: jedno zrodlo prawdy - sito bohaterow ItemReq.MeetsHero (ItemReq.SkillFor: bron i kon z danych, pancerz -
        /// Atletyka, strzaly Luk, belty Kusza; ladry konskie - Jazda, jak na ekranie gry z ROT ExtraGearRequirements - r2-pancerz,
        /// wylacznik HeroBardingNeedsRiding). Dotad wlasna kopia bez galezi ladr: ladry (ArmorComponent) mierzone Atletyka wobec Difficulty z XML ROT.</summary>
        private static bool CanUse(Hero h, ItemObject it)
        {
            try
            {
                if (h == null || it == null || it.Difficulty <= 0) return true;
                return ItemReq.MeetsHero(h.CharacterObject, it);
            }
            catch { return true; }
        }

        private static float Worth(ItemObject it)
        {
            try { return it.Effectiveness > 0f ? it.Effectiveness : it.Value; } catch { return 0f; }
        }

        private static ItemObject Substitute(Hero h, ItemObject orig)
        {
            var culture = orig.Culture;
            ItemObject best = null;
            foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (it == null || it == orig || it.ItemType != orig.ItemType || it.NotMerchandise) continue;
                if (it.Value > orig.Value) continue;
                if (it.Culture != null && culture != null && it.Culture != culture && !it.Culture.StringId.StartsWith("neutral")) continue;
                if (ArmsPricing.IsUnique(it)) continue;
                if (orig.ItemType == ItemObject.ItemTypeEnum.Horse && it.HorseComponent != null && it.HorseComponent.IsPackAnimal) continue;
                // r2-pancerz: ladry odtad sprawdzane Jazda - zamiennik tylko tej samej rodziny (kon/wielblad/slon), inaczej nie pasuje do konia
                if (orig.ItemType == ItemObject.ItemTypeEnum.HorseHarness
                    && (it.ArmorComponent == null || orig.ArmorComponent == null || it.ArmorComponent.FamilyType != orig.ArmorComponent.FamilyType)) continue;
                if (!CanUse(h, it)) continue;
                if (best == null || Worth(it) > Worth(best)) best = it;
            }
            return best;
        }

        private static void Run()
        {
            var h = Hero.MainHero;
            var party = MobileParty.MainParty;
            if (h == null || party == null) return;
            var swaps = new List<string>();
            var kept = new List<string>();

            // 1. na postaci (bojowy i cywilny komplet)
            foreach (var eq in new[] { h.BattleEquipment, h.CivilianEquipment })
            {
                if (eq == null) continue;
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var el = eq[i];
                    if (el.IsEmpty || el.Item == null || CanUse(h, el.Item)) continue;
                    var sub = Substitute(h, el.Item);
                    if (sub == null) { kept.Add(el.Item.StringId); continue; }
                    swaps.Add(el.Item.StringId + " -> " + sub.StringId);
                    eq[i] = new EquipmentElement(sub);
                }
            }

            // 2. w taborze
            var roster = party.ItemRoster;
            var todo = new List<KeyValuePair<EquipmentElement, int>>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !(it.HasArmorComponent || it.WeaponComponent != null || it.HorseComponent != null)) continue;
                if (CanUse(h, it)) continue;
                todo.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
            }
            foreach (var kv in todo)
            {
                var sub = Substitute(h, kv.Key.Item);
                if (sub == null) { kept.Add(kv.Key.Item.StringId + " x" + kv.Value); continue; }
                roster.AddToCounts(kv.Key, -kv.Value);
                roster.AddToCounts(new EquipmentElement(sub), kv.Value);
                swaps.Add(kv.Key.Item.StringId + " x" + kv.Value + " -> " + sub.StringId);
            }

            Log.Info("StartKit: sprzet startowy dopasowany do umiejetnosci - zamienione " + swaps.Count
                     + (swaps.Count > 0 ? " [" + string.Join(", ", swaps.ToArray()) + "]" : "")
                     + "; bez zamiennika " + kept.Count + (kept.Count > 0 ? " [" + string.Join(", ", kept.ToArray()) + "]" : "") + ".");
            if (swaps.Count > 0)
                Log.Player("Your starting gear has been matched to what you can actually wear.");
        }

        // ------------------------------------------------------------ zloto startowe BK
        // Jeff 04.10: "nie moge miec na starcie 1000 denarow". BK (DefaultStartOptions.Adventurer) daje awanturnikowi
        // 1000 d = ok. 4 funty, ponad rok pracy robotnika. Wpis 46: prefix na StartOption.Initialize (wpis 44) nie
        // zadzialal w grze (test 14:05 - 1000 d), wiec ustawiamy zloto PO BKCampaignStartBehavior.RunStartOption, ktore
        // robi ChangeHeroGold(option.Gold - gold): postfix czyta wybrana opcje i dla "start_adventurer" daje
        // StartGoldAdventurer (nadwyzka znika - to nie jest zloto, ktore ktos mial).
        public static void RunStartPostfix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || s.StartGoldAdventurer < 0 || Hero.MainHero == null) return;
                var opt = HarmonyLib.Traverse.Create(__instance).Field("option").GetValue();
                var id = opt != null ? HarmonyLib.Traverse.Create(opt).Property("StringId").GetValue() as string : null;
                float r;
                if (opt != null && _newCampaign && CutRatio(s, out r))
                {
                    // r4-zloto: BK zrobil ChangeHeroGold(option.Gold - gold) - zloto gracza = Gold opcji (+ to, co dalaby Action; dzis zadna nie daje zlota)
                    int had = Hero.MainHero.Gold;
                    int keep = Cut(had, r);
                    if (keep != had) Hero.MainHero.ChangeHeroGold(keep - had);
                    _cutDone = true;
                    LogTableOnce(s, r);
                    LogCut(s, r, "start BK '" + (id ?? "?") + "' (Gold opcji BK " + OptGold(opt) + ")", had, Hero.MainHero.Gold);
                    return;
                }
                if (opt == null && _newCampaign && CutRatio(s, out r))
                {
                    // BK bez wybranej opcji wychodzi z RunStartOption przed zlotem - zostaje zloto gry; tnie je pierwsza godzina (albo juz uciela)
                    Log.Info(CutTag + " - start BK bez wybranej opcji: BK nie zmienil zlota (" + Hero.MainHero.Gold + " d), "
                             + (_cutDone ? "juz przeciete w pierwszej godzinie." : "zloto gry przetnie pierwsza godzina."));
                    return;
                }
                if (id != "start_adventurer") { Log.Info("StartKit: start BK '" + (id ?? "brak") + "' - zloto bez zmian (" + Hero.MainHero.Gold + " d)."); return; }
                int was = Hero.MainHero.Gold;
                Hero.MainHero.ChangeHeroGold(s.StartGoldAdventurer - was);
                Log.Info("StartKit: awanturnik BK - zloto startowe " + was + " -> " + Hero.MainHero.Gold + " d.");
            }
            catch (Exception e) { Log.Error("StartKit.RunStartPostfix", e); }
        }

        // wpis 58: test 15:31 - mimo latki dalej 1000 d: okno startu BK nie wywolalo RunStartOption (w logu brak linii "awanturnik"
        // ani "start BK"; CrashScribe: wyjatek BK w OnCharacterCreationOver -> CorrectPlayerEducation). W pierwszej godzinie gry:
        // jesli w BK nie wybrano innego startu niz awanturnik - zloto = StartGoldAdventurer (nadwyzka znika, nikt jej nie mial).
        private static void EnforceStartGold()
        {
            var s = Settings.Current;
            var h = Hero.MainHero;
            float r;
            if (s != null && h != null && _newCampaign && CutRatio(s, out r))
            {
                // r4-zloto: pierwsza godzina nowej kampanii - zloto, ktorego nie przecial juz start BK (gra: Campaign.OnAfterNewGameCreatedInternal
                // Hero.MainHero.Gold = 1000; tworzenie postaci gry/ROT/NavalDLC nie daje zlota - GoldToAdd bez settera; inne mody - cokolwiek doszlo)
                LogTableOnce(s, r);
                if (_cutDone) { Log.Info(CutTag + " - pierwsza godzina: juz przeciete przy starcie BK, w sakiewce " + h.Gold + " d."); return; }
                string bkId = ChosenBkStart();
                int had = h.Gold;
                int keep = Cut(had, r);
                if (keep != had) h.ChangeHeroGold(keep - had);
                _cutDone = true;
                LogCut(s, r, bkId != null ? "start BK '" + bkId + "' (bez latki RunStartOption - przeciete w pierwszej godzinie)" : "gra (start bez wyboru BK: 1000 d gry przy tworzeniu kampanii)", had, h.Gold);
                return;
            }
            if (s == null || s.StartGoldAdventurer < 0 || h == null || h.Gold <= s.StartGoldAdventurer) return;
            string id = ChosenBkStart();
            if (id != null && id != "start_adventurer") { Log.Info("StartKit: start BK '" + id + "' - zloto bez zmian (" + h.Gold + " d)."); return; }
            int was = h.Gold;
            h.ChangeHeroGold(s.StartGoldAdventurer - was);
            Log.Info("StartKit: zloto startowe " + was + " -> " + h.Gold + " d (start " + (id ?? "bez wyboru BK") + ").");
        }

        /// <summary>Wybrany start BK (BKCampaignStartBehavior.option) albo null - brak BK albo bez wyboru.</summary>
        private static string ChosenBkStart()
        {
            try
            {
                var t = HarmonyLib.AccessTools.TypeByName("BannerKings.Behaviours.BKCampaignStartBehavior");
                if (t != null && Campaign.Current != null)
                {
                    var beh = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(t).Invoke(Campaign.Current, null);
                    var opt = beh != null ? HarmonyLib.Traverse.Create(beh).Field("option").GetValue() : null;
                    return opt != null ? HarmonyLib.Traverse.Create(opt).Property("StringId").GetValue() as string : null;
                }
            }
            catch { }
            return null;
        }

        // ------------------------------------------------------------ r4-zloto: ZLOTO STARTU OBCIETE (klucz StartGoldCut)
        // Jeff 20.10: "obcinamy kazde zloto w starcie" (PLAN 3.10: start "zeglarz" BK daje 10 000 zl). Zrodla zlota gracza na starcie
        // nowej kampanii (dekompilacja 20.10):
        //  - gra: Campaign.InitializeGamePlayReferences +1000 (GiveGoldAction z niczego) i Campaign.OnAfterNewGameCreatedInternal
        //    Hero.MainHero.Gold = 1000 - po tworzeniu kampanii gracz ma 1000;
        //  - tworzenie postaci gry / ROT (ROTCharacterCreationCampaignBehavior) / NavalDLC: CharacterCreationContent.ApplySkillAndAttributeEffects
        //    daje goldToAdd z NarrativeMenuOptionArgs.GoldToAdd - wlasnosc z prywatnym setterem, nikt jej nie ustawia: 0;
        //  - BK BKCampaignStartBehavior.RunStartOption: ChangeHeroGold(option.Gold - gold) = zloto gracza ROWNE Gold opcji
        //    (DefaultStartOptions: start_adventurer 1000, start_lord 0, start_mercenary 250, start_outlaw 50, start_caravaneer 0,
        //    start_mariner 10000, start_gladiator 0); Action opcji daje ludzi, konie, towar, statek - zlota nie; GiveClansResources
        //    pomija rod gracza; BKROTPatch i ROT nie ruszaja zlota startu; nasza wioska poboczna ("Indebted Lord", SideVillageFief)
        //    nie daje zlota (dochod tylko z licznika pana, przy wozie).
        // PRZELICZNIK: ceny historyczne nie maja jednego mnoznika - kazda sztuka ma wlasny (HistoricalPrices: uzbrojenie suma
        // 34.3 mln -> 1.04 mln, x0.03; jedzenie ok. x0.3; zwykly kon x0.4; zold i werbunek bez zmian - "1 denar = 1 pens").
        // Jedyny stosunek, ktorego mod uzywa dla zlota gracza, to start awanturnika (wpis 46, Jeff 04.10): 1000 gry -> StartGoldAdventurer
        // (100) = x0.1 - "Vanilla start" BK. Ten sam stosunek dla KAZDEGO zrodla: kazdy start zostaje w tej samej proporcji do startu gry
        // co w zwyklej grze (zeglarz = 10 startow gry, najemnik 1/4, banita 1/20), tylko w monecie tego swiata; awanturnik dalej 100.
        // Bez zlota z niczego: przelicznik najwyzej 1 (tylko w dol), reszta znika - nikt jej nie mial. Tylko nowa kampania
        // (_newCampaign); wczytany zapis - dawne zachowanie (awanturnik BK -> StartGoldAdventurer). StartGoldAdventurer < 0 = BK bez zmian.
        private const string CutTag = "StartKit: zloto startowe (Start Gold Cut)";
        internal const int GameStartGold = 1000;   // Campaign.OnAfterNewGameCreatedInternal: Hero.MainHero.Gold = 1000
        private static bool _newCampaign, _cutDone, _tableLogged;

        private static bool CutRatio(Settings s, out float r)
        {
            r = 1f;
            if (s == null || !s.StartGoldCut || s.StartGoldAdventurer < 0) return false;
            r = Math.Min(1f, s.StartGoldAdventurer / (float)GameStartGold);
            return true;
        }

        private static int Cut(int gold, float r)
        {
            if (gold <= 0) return gold;
            return Math.Min(gold, (int)Math.Round(gold * (double)r, MidpointRounding.AwayFromZero));
        }

        /// <summary>Opcje startu BK: DefaultTypeInitializer&lt;DefaultStartOptions, StartOption&gt;.Instance.All (BK wypelnia je w OnSessionLaunched); null - brak BK.</summary>
        private static System.Collections.IEnumerable BkOptions()
        {
            var t = HarmonyLib.AccessTools.TypeByName("BannerKings.Managers.CampaignStart.DefaultStartOptions");
            var ip = t != null ? HarmonyLib.AccessTools.Property(t, "Instance") : null;
            var inst = ip != null ? ip.GetValue(null, null) : null;
            var ap = inst != null ? HarmonyLib.AccessTools.Property(inst.GetType(), "All") : null;
            return ap != null ? ap.GetValue(inst, null) as System.Collections.IEnumerable : null;
        }

        private static string OptId(object opt)
        {
            try { return HarmonyLib.Traverse.Create(opt).Property("StringId").GetValue() as string ?? "?"; } catch { return "?"; }
        }

        // Furtka autotestu (r4-zloto): DLL autotestu CrashScribe (LogGate.Mode 1/2) + plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-start-zloto.flag
        // (tresc: id opcji BK, pusty = start_mariner). Autotest konczy okno BK "Finish" bez wybranej opcji - prefiks RunStartOption ustawia wtedy opcje z pliku
        // (pole option, jak SetStartOption), zeby przejsc droge BK (Gold opcji -> ciecie w postfiksie). Bez pliku albo w zwyklej grze - nic.
        internal const string TestFlagName = "armoury-start-zloto.flag";

        public static void RunStartPrefix(object __instance)
        {
            try
            {
                int m = LogGate.Mode;
                if ((m != 1 && m != 2) || !_newCampaign || __instance == null) return;
                var tr = HarmonyLib.Traverse.Create(__instance);
                if (tr.Field("option").GetValue() != null) return;
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), TestFlagName);
                if (!System.IO.File.Exists(p)) return;
                string want = (System.IO.File.ReadAllText(p) ?? "").Trim();
                if (want.Length == 0) want = "start_mariner";
                object pick = null;
                var all = BkOptions();
                if (all != null) foreach (var o in all) if (o != null && string.Equals(OptId(o), want, StringComparison.OrdinalIgnoreCase)) { pick = o; break; }
                if (pick == null) { Log.Info(CutTag + " - test (autotest, plik " + TestFlagName + "): brak opcji BK '" + want + "' - start bez opcji."); return; }
                tr.Field("option").SetValue(pick);
                Log.Info(CutTag + " - test (autotest, plik " + TestFlagName + "): opcja BK '" + OptId(pick) + "' (Gold " + OptGold(pick) + ") ustawiona przed RunStartOption.");
            }
            catch (Exception e) { Log.Error("StartKit.RunStartPrefix", e); }
        }

        private static int OptGold(object opt)
        {
            try { var g = HarmonyLib.Traverse.Create(opt).Property("Gold").GetValue(); return g is int ? (int)g : -1; } catch { return -1; }
        }

        private static string Ratio(Settings s, float r)
        {
            return "x" + r.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " = Start Gold Adventurer " + s.StartGoldAdventurer + " / " + GameStartGold + " d startu gry";
        }

        private static void LogCut(Settings s, float r, string source, int had, int now)
        {
            Log.Info(CutTag + " - zrodlo: " + source + " | dala gra/BK " + had + " d, zostaje " + now + " d (znika " + (had - now) + " d - nikt go nie mial)"
                     + " | przelicznik " + Ratio(s, r) + " | tylko nowa kampania.");
        }

        /// <summary>Raz na nowa kampanie: kazda opcja startu BK i start gry - ile daja i ile zostanie (opcje BK z DefaultStartOptions.Instance.All).</summary>
        private static void LogTableOnce(Settings s, float r)
        {
            if (_tableLogged) return;
            _tableLogged = true;
            var parts = new List<string>();
            string bk;
            try
            {
                var all = BkOptions();
                if (all != null)
                    foreach (var o in all)
                    {
                        if (o == null) continue;
                        int g = OptGold(o);
                        parts.Add(OptId(o) + " " + g + " -> " + Cut(g, r));
                    }
                bk = all == null ? "brak BK albo listy opcji BK" : string.Join(", ", parts.ToArray());
            }
            catch (Exception e) { bk = "blad odczytu (" + e.GetType().Name + ")"; }
            Log.Info(CutTag + " - nowa kampania, przelicznik " + Ratio(s, r) + " | gra (bez wyboru BK) " + GameStartGold + " -> " + Cut(GameStartGold, r)
                     + " | opcje BK: " + bk + ".");
        }

        internal static void ApplyAll(HarmonyLib.Harmony h)
        {
            try
            {
                var t = HarmonyLib.AccessTools.TypeByName("BannerKings.Behaviours.BKCampaignStartBehavior");
                var m = t != null ? HarmonyLib.AccessTools.DeclaredMethod(t, "RunStartOption") : null;
                if (m != null) h.Patch(m, prefix: new HarmonyLib.HarmonyMethod(typeof(StartKit), nameof(RunStartPrefix)), postfix: new HarmonyLib.HarmonyMethod(typeof(StartKit), nameof(RunStartPostfix)));
                var s = Settings.Current;
                float r;
                bool cut = CutRatio(s, out r);
                Log.Info("StartKit: zloto startowe awanturnika BK = " + s.StartGoldAdventurer + " d " + (m != null ? "wpiete (po RunStartOption)" : "(brak BK RunStartOption)") + "."
                         + " " + CutTag + ": " + (cut ? "CZYNNE, przelicznik " + Ratio(s, r) + " - kazdy start nowej kampanii (gra i kazda opcja BK)" : "wylaczone (" + (s.StartGoldCut ? "Start Gold Adventurer < 0 - BK bez zmian" : "klucz wylaczony - tylko awanturnik") + ")") + ".");
            }
            catch (Exception e) { Log.Error("StartKit.ApplyAll", e); }
        }
    }
}
