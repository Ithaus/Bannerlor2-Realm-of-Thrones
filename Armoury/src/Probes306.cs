using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Helpers;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 306b - SONDY AUTOTESTU dla zmian 305, ktorych autotest 141b nie pokryl (CHANGELOG WGRANIE 141: "NIE pokryte w autotescie: kucie (nikt nie kul), karawana gracza
    /// (brak w zapisie), zmiana produkcji warsztatu"). TYLKO DLL autotestu CrashScribe (LogGate.Mode 1/2), klucz Autotest306Probes i pliki w
    /// Documents\Mount and Blade II Bannerlord\CrashScribe (tylko czytane). Bez pliku - nic (nawet linii), w zwyklej grze nic.
    ///  (1) armoury-kowal305.flag - pierwsza doba sesji: XP kowalstwa dla probek. Luki / kusze / amunicja: KOPIA WYRAZENIA z Forge.Smith (SmithXpRule.WorkXp;
    ///      klucz NIE - Forge.ProjectXp), nie wywolanie Forge.Smith/FletchForge - sprawdza stale przepisu i wzor, nie sama droge kucia (recenzja 306b). Bron, pancerz i zlecenie przez czynny SmithingModel
    ///      (GetSkillXpForSmithingInFreeBuildMode / InCraftingOrderMode - z latkami HistoricalPrices.XpPostfix, SmithXpRule, WeaponXpPatch), przetop
    ///      (GetSkillXpForSmelting - XpPostfix, sufit SmithAudit) i rafinacja (GetSkillXpForRefining - MaterialLaw.RefineXpPostfix, sufit RefineXpCap).
    ///      Podglad modelu nie dodaje XP i nie bierze staminy; liczniki doby SmithXpRule / HistoricalPrices.RoundFixed przywracane, slowo zrodla SmithXp zdjete.
    ///      Kontrola: XP umiejetnosci, TotalXp, stamina i zloto bohatera przed == po. Tabela, wzor niezaleznie (stamina x stawka x tier x nauka, sufity) i werdykt
    ///      wobec 305d (luk surowe 238/546/980 +-15%, miecz > 50, przetop > 1.5 sredni).
    ///  (2) armoury-karawana305.flag - pierwsza doba sesji: karawana gracza ta sama sekwencja co konsekwencja rozmowy z kupcem (CaravanConversationsCampaignBehavior
    ///      .conversation_magistrate_form_a_caravan_accept_on_consequence, dekompilacja 1.4.8): LeaveSettlementAction.ApplyForCharacterOnly(wodz), szablon
    ///      CaravanHelper.GetRandomCaravanTemplate(kultura miasta, zwykla, !port), CaravanPartyComponent.CreateCaravanParty(gracz, miasto, szablon, false, wodz, null, false),
    ///      GiveGoldAction.ApplyForCharacterToSettlement(gracz, miasto, CaravanModel.GetCaravanFormingCost(false, port)) - w kontekscie E3/7 KPlayer
    ///      (CaravanCapital.OpenPlayerFor / CtxFin - jak latka na rozmowie: kapital z zaplaty, miasto -> karawana). Wodz: wolny towarzysz z druzyny
    ///      (warunki FindSuitableCompanionsToLeadCaravan), inaczej czlonek rodu; bez wodza - NIE bez tworzenia (rozmowa go wymaga). Druzyna w bitwie/oblezeniu/niewoli -
    ///      POMINIETY i proba nastepnej doby (najwyzej 5). Kasa miasta sprawdzana: + zaplata - kapital E3/7. Raz na kampanie testu: znacznik w zapisie
    ///      (klucz arm_306_sondy, zapisywany tylko gdy jest) i nic, gdy gracz juz ma karawane. Co dobe: podglad CalculateClanExpenses gracza z opisem
    ///      bez rozbicia 305a i z nim (CaravanSettle.ExpProbe306).
    ///  (3) armoury-warsztat305.flag - doba N+3 sesji: zmiana produkcji warsztatu gracza bez linii broni o najmniejszym ProfitMade (nie kuznia - ta jest firma
    ///      305b i reszta testu ja mierzy) na typ bez linii broni o najnizszym kapitale startowym (StartCapitalFor) - ChangeProductionTypeOfWorkshopAction.Apply(w, typ)
    ///      jak rozmowa (WorkshopsCharactersCampaignBehavior.conversation_player_workshop_player_changed_production_on_consequence). Obietnica rozmowy
    ///      (WorkshopTrade.RetoolPromise - te same liczby co RetoolCondition) wobec przelewu RetoolSettle, kapitalu, znacznika, kapitalu wlasnego 259 i zlota gracza.
    ///  307c: (3) pomija warsztaty z linia zbytku BK (srebrnik Seagard - reszta testu mierzy jego zarobek); jedyny taki kandydat - przestawiony z powrotem
    ///      nastepnej doby ("warsztat - powrot produkcji"). (3b) doba N+4: kupno najtanszego warsztatu notabla bez linii broni i luksusu i natychmiastowa
    ///      odsprzedaz ("warsztat - kupno i odsprzedaz": cena = GetCostForPlayer, odkup <= cena, zloto gracza i kiesy zgodne, kapital bez zmian).
    /// Potkniecia liczone (bez wylacznika). Linie "Sondy autotestu (306b): ..." (grupa sprawdz_logi sondy306).
    /// </summary>
    internal static class Probes306
    {
        internal const string P = "Sondy autotestu (306b): ";
        internal const string FlagKowal = "armoury-kowal305.flag", FlagCar = "armoury-karawana305.flag", FlagWs = "armoury-warsztat305.flag";
        private const string SaveKey = "arm_306_sondy";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int _flagDay = -1, _firstDay = -1, _stumbles, _carTries;
        private static bool _fK, _fC, _fW, _startDone, _kowalDone, _carDone, _wsDone;
        private static string _carMark;   // znacznik karawany testu z zapisu albo z tej sesji ("id@doba"); null = brak
        // 307c: (3a) warsztat z linia luksusu przestawiony w braku innego - powrot nastepnej doby; (3b) kupno i natychmiastowa odsprzedaz (doba N+4)
        private static Workshop _revW; private static WorkshopType _revType; private static int _revDay = -1; private static bool _saleDone;
        private static readonly string[] LuxCats = { "silver", "velvet", "jewelry", "spice", "fur" };   // jak TownFolk.Probe.LuxIds (zbytki BK)

        internal static void Reset()
        {
            _flagDay = -1; _firstDay = -1; _stumbles = 0; _carTries = 0;
            _fK = _fC = _fW = false; _startDone = _kowalDone = _carDone = _wsDone = false;
            _carMark = null;
            _revW = null; _revType = null; _revDay = -1; _saleDone = false;   // 307c
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5 || _stumbles % 50 == 0) Log.Error("Probes306." + where + " (potkniecie " + _stumbles + ")", e);
        }

        private static string Tn(bool b) { return b ? "TAK" : "NIE"; }
        private static string F1(double v) { return v.ToString("0.#", Inv); }

        /// <summary>ArmouryBehavior.SyncData: znacznik karawany testu - zapisywany tylko, gdy jest (bez flagi testu zapis bez zmian), czytany zawsze (brak = null).</summary>
        internal static void Sync(IDataStore ds)
        {
            if (ds.IsSaving)
            {
                if (_carMark == null) return;
                string v = _carMark;
                ds.SyncData(SaveKey, ref v);
                return;
            }
            if (ds.IsLoading)
            {
                string v = null;
                try { ds.SyncData(SaveKey, ref v); } catch { v = null; }
                _carMark = string.IsNullOrEmpty(v) ? null : v;
            }
        }

        private static bool Flag(string name)
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), name));
        }

        private static void ReadFlags(int day)
        {
            if (_flagDay == day) return;
            _flagDay = day;
            _fK = _fC = _fW = false;
            int m = LogGate.Mode;
            var s = Settings.Current;
            if ((m != 1 && m != 2) || s == null || !s.Autotest306Probes) return;
            try { _fK = Flag(FlagKowal); _fC = Flag(FlagCar); _fW = Flag(FlagWs); }
            catch { _fK = _fC = _fW = false; }
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner (po CaravanSettle.Daily): sondy wedlug plikow. day = CampaignTime.Now.ToDays - 1 (jak reszta linii doby).</summary>
        internal static void Daily(int day)
        {
            ReadFlags(day);
            if (!_fK && !_fC && !_fW) return;
            if (Campaign.Current == null || Hero.MainHero == null) return;
            if (_firstDay < 0) _firstDay = day;
            if (!_startDone)
            {
                _startDone = true;
                Log.Info(P + "start sesji | doba " + day + " | klucz Autotest306Probes TAK | flagi: kowal305 " + Tn(_fK) + ", karawana305 " + Tn(_fC) + ", warsztat305 " + Tn(_fW)
                         + " | karawana testu z zapisu " + (_carMark ?? "brak") + " | zmiana produkcji w dobie " + (_firstDay + 3) + ".");
            }
            if (_fK && !_kowalDone) { _kowalDone = true; try { Kowal(); } catch (Exception e) { Stumble("Kowal", e); Log.Info(P + "kowal - werdykt | wyjatek " + e.GetType().Name + " -> wynik NIE."); } }
            if (_fC && !_carDone)
            {
                _carDone = true;
                try { if (!Caravan(day) && ++_carTries < 5) _carDone = false; }   // recenzja 306b: druzyna w bitwie / oblezeniu / niewoli - proba nastepnej doby (najwyzej 5)
                catch (Exception e) { Stumble("Caravan", e); Log.Info(P + "karawana - wyjatek " + e.GetType().Name + ": " + e.Message + " -> wynik NIE."); }
            }
            if (_fC) { try { CaravanDay(day); } catch (Exception e) { Stumble("CaravanDay", e); Log.Info(P + "karawana - wydatki doby " + day + " | wyjatek " + e.GetType().Name + " -> wynik NIE."); } }
            if (_fW && !_wsDone && day >= _firstDay + 3) { _wsDone = true; try { Retool(day); } catch (Exception e) { Stumble("Retool", e); Log.Info(P + "warsztat - zmiana produkcji doba " + day + " | wyjatek " + e.GetType().Name + ": " + e.Message + " -> wynik NIE."); } }
            if (_revW != null && day > _revDay) { try { Revert(day); } catch (Exception e) { Stumble("Revert", e); Log.Info(P + "warsztat - powrot produkcji doba " + day + " | wyjatek " + e.GetType().Name + " -> wynik NIE."); } finally { _revW = null; _revType = null; } }
            if (_fW && !_saleDone && day >= _firstDay + 4) { _saleDone = true; try { BuyResell(day); } catch (Exception e) { Stumble("BuyResell", e); Log.Info(P + "warsztat - kupno i odsprzedaz doba " + day + " | wyjatek " + e.GetType().Name + ": " + e.Message + " -> wynik NIE."); } }
        }

        // ============================================================ (1) kowalstwo
        private sealed class Row { public string Kind, Id, Path, Note; public int Grade, Stamina = -1, Diff = -1, Xp, Exp = -1, Raw = -1, Table = -1; public double Learn = -1; public bool? Ok; }

        private static IEnumerable<ItemObject> Items()
        {
            var list = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObjectTypeList<ItemObject>() : null;
            if (list == null) yield break;
            foreach (var it in list) if (it != null && it.StringId != null) yield return it;
        }

        private static bool Legend(ItemObject it) { try { return Recipes.IsLegendary(it); } catch { return true; } }

        /// <summary>Probka: najmniejszy StringId (stala miedzy biegami) - najpierw towar targowy, gdy go brak (bron kuta ROT bywa NotMerchandise) - kazdy nielegendarny.</summary>
        private static ItemObject First(Func<ItemObject, bool> pred)
        {
            ItemObject best = null, any = null;
            foreach (var it in Items())
            {
                if (!pred(it) || Legend(it)) continue;
                if (!it.NotMerchandise) { if (best == null || string.CompareOrdinal(it.StringId, best.StringId) < 0) best = it; }
                else if (any == null || string.CompareOrdinal(it.StringId, any.StringId) < 0) any = it;
            }
            return best ?? any;
        }

        private static WeaponClass Cls(ItemObject it)
        {
            var w = it.WeaponComponent != null ? it.WeaponComponent.PrimaryWeapon : null;
            return w != null ? w.WeaponClass : WeaponClass.Undefined;
        }

        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "brak metody";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "bez latek";
                var o = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var p in info.Prefixes) o.Add(p.owner);
                foreach (var p in info.Postfixes) o.Add(p.owner);
                foreach (var p in info.Transpilers) o.Add(p.owner);
                foreach (var p in info.Finalizers) o.Add(p.owner);
                return o.Count == 0 ? "bez latek" : string.Join(", ", new List<string>(o).ToArray());
            }
            catch (Exception e) { return "blad " + e.GetType().Name; }
        }

        private static void Kowal()
        {
            var s = Settings.Current;
            var hero = Hero.MainHero;
            var dev = hero.HeroDeveloper;
            var model = Campaign.Current.Models.SmithingModel;
            var craft = DefaultSkills.Crafting;
            int skill = hero.GetSkillValue(craft);
            float xp0 = dev.GetSkillXp(craft); int tot0 = dev.TotalXp, st0 = Forge.Stamina(hero), g0 = hero.Gold;
            bool rule = SmithXpRule.On;
            int rate = s.SmithXpPerStaminaTier;
            bool perk = false;
            try { perk = hero.GetPerkValue(DefaultPerks.Crafting.PracticalSmith); } catch { }
            // liczniki doby (linia 'Kowalstwo - zrodla XP (305d)') - podglad ich nie moze ruszyc
            int dF = SmithXpRule.DayFree, dA = SmithXpRule.DayArmour, dO = SmithXpRule.DayOrder, dOB = SmithXpRule.DayOrderBonus, rf = HistoricalPrices.RoundFixed;
            double xF = SmithXpRule.DayFreeXp, xA = SmithXpRule.DayArmourXp, xO = SmithXpRule.DayOrderXp, xOB = SmithXpRule.DayOrderBonusXp;
            var rows = new List<Row>();
            int stumbles0 = _stumbles;
            try
            {
                // --- luki, kusza, amunicja: wyrazenie Forge.Smith (udane kucie)
                Action<string, ItemObject, int> ranged = (kind, it, table) =>
                {
                    var row = new Row { Kind = kind, Id = it != null ? it.StringId : "-", Path = "Forge.Smith (" + (rule ? "SmithXpRule.WorkXp" : "Forge.ProjectXp") + ")", Table = table };
                    rows.Add(row);
                    if (it == null) { row.Note = "brak probki"; return; }
                    try
                    {
                        var r = Recipes.For(it);
                        int g = Recipes.Grade(it);
                        row.Grade = g; row.Stamina = r.Stamina; row.Diff = r.Difficulty; row.Learn = Forge.Learning(skill, r.Difficulty);
                        float v = rule ? SmithXpRule.WorkXp(r.Stamina, g, r.Difficulty, hero) : Forge.ProjectXp(r) * (1f - s.XpShareWhileWorking) * Forge.AmmoXpMul(it);
                        row.Xp = (int)Math.Round(v);
                        row.Raw = r.Stamina * rate * g;
                        if (rule)
                        {
                            double e = Math.Min(Math.Max(50, s.XpCapPerTier * g), r.Stamina * (double)rate * g * row.Learn);
                            row.Exp = (int)Math.Round(e);
                            row.Ok = Math.Abs(row.Xp - row.Exp) <= 1;
                        }
                        row.Note = "pekniecie " + F1(v * 0.3f) + (rule ? "" : " (klucz SmithXpOneRule NIE - stara droga)");
                    }
                    catch (Exception e) { Stumble("Kowal(" + kind + ")", e); row.Note = "wyjatek " + e.GetType().Name; row.Ok = false; }
                };
                int[] bowT = { 0, 0, 238, 546, 980 };
                for (int g = 2; g <= 4; g++) { int gg = g; ranged("luk t" + g, First(it => it.ItemType == ItemObject.ItemTypeEnum.Bow && Recipes.Grade(it) == gg), bowT[g]); }
                var xb = First(it => it.ItemType == ItemObject.ItemTypeEnum.Crossbow && Recipes.Grade(it) == 3) ?? First(it => it.ItemType == ItemObject.ItemTypeEnum.Crossbow);
                ranged("kusza", xb, -1);
                for (int g = 2; g <= 4; g++) { int gg = g; ranged("strzaly t" + g, First(it => it.ItemType == ItemObject.ItemTypeEnum.Arrows && Recipes.Grade(it) == gg), -1); }

                // --- bron kuta, pancerz, zlecenie: czynny model kowalstwa (latki jak w grze)
                Func<ItemObject, bool, int> expectPiece = (it, armour) =>
                {
                    int g = Recipes.Grade(it);
                    int st = armour ? Recipes.For(it).Stamina : (perk ? -1 : model.GetEnergyCostForSmithing(it, hero));
                    if (st < 0) return -1;
                    double e = Math.Min(Math.Max(50, s.XpCapPerTier * g), st * (double)rate * g * Forge.Learning(skill, Recipes.For(it).Difficulty));
                    int v = (int)Math.Round(e);
                    if (s.WeaponXpFromValueCapped)
                    {
                        int cap = Math.Max(50, (armour ? s.XpCapPerTier : s.WeaponXpCapPerTier) * g);
                        if (v > cap) v = cap;
                    }
                    return v;
                };
                Action<string, ItemObject, bool, bool> piece = (kind, it, armour, order) =>
                {
                    var row = new Row { Kind = kind, Id = it != null ? it.StringId : "-", Path = order ? "SmithingModel.GetSkillXpForSmithingInCraftingOrderMode" : "SmithingModel.GetSkillXpForSmithingInFreeBuildMode" };
                    rows.Add(row);
                    if (it == null) { row.Note = "brak probki"; return; }
                    try
                    {
                        int g = Recipes.Grade(it);
                        var r = Recipes.For(it);
                        row.Grade = g; row.Diff = r.Difficulty; row.Learn = Forge.Learning(skill, r.Difficulty);
                        row.Stamina = armour ? r.Stamina : model.GetEnergyCostForSmithing(it, hero);
                        row.Xp = order ? model.GetSkillXpForSmithingInCraftingOrderMode(it) : model.GetSkillXpForSmithingInFreeBuildMode(it);
                        row.Raw = row.Stamina * rate * g;
                        if (rule)
                        {
                            row.Exp = expectPiece(it, armour);
                            row.Ok = row.Exp < 0 ? (bool?)null : Math.Abs(row.Xp - row.Exp) <= 1;
                            if (row.Exp < 0) row.Note = "perk Practical Smith - stamina bez perka (wzor niezaleznie pominiety)";
                        }
                        else row.Note = "klucz SmithXpOneRule NIE - od ceny (wartosc " + it.Value + ", dawna " + HistoricalPrices.Orig(it) + ")";
                        if (order) row.Note = (row.Note != null ? row.Note + "; " : "") + "bez premii zlecenia (CraftOrders8 - potrzebne zlecenie)";
                    }
                    catch (Exception e) { Stumble("Kowal(" + kind + ")", e); row.Note = "wyjatek " + e.GetType().Name; row.Ok = false; }
                };
                Func<ItemObject, bool> crafted = it => it.WeaponDesign != null && it.IsCraftedWeapon;
                var swords = new List<ItemObject>();
                for (int g = 2; g <= 5; g++)
                {
                    int gg = g;
                    var a = First(it => crafted(it) && Cls(it) == WeaponClass.OneHandedSword && Recipes.Grade(it) == gg);
                    piece("miecz 1r t" + g, a, false, false);
                    if (a != null) swords.Add(a);
                }
                for (int g = 2; g <= 5; g++)
                {
                    int gg = g;
                    var a = First(it => crafted(it) && Cls(it) == WeaponClass.TwoHandedSword && Recipes.Grade(it) == gg);
                    piece("miecz 2r t" + g, a, false, false);
                    if (a != null) swords.Add(a);
                }
                for (int g = 3; g <= 4; g++) { int gg = g; piece("pancerz korpusu t" + g, First(it => it.ItemType == ItemObject.ItemTypeEnum.BodyArmor && Recipes.Grade(it) == gg), true, false); }
                piece("zlecenie miecz 1r t3", First(it => crafted(it) && Cls(it) == WeaponClass.OneHandedSword && Recipes.Grade(it) == 3), false, true);

                // --- przetop (bron kuta z probek) i rafinacja
                foreach (var it in swords)
                {
                    var row = new Row { Kind = "przetop", Id = it.StringId, Path = "SmithingModel.GetSkillXpForSmelting", Grade = Recipes.Grade(it) };
                    rows.Add(row);
                    try
                    {
                        row.Xp = model.GetSkillXpForSmelting(it);
                        int orig = HistoricalPrices.Orig(it);
                        int e = TaleWorlds.Library.MathF.Round(0.02f * (float)(s.SmithXpRoundFix && orig > 0 ? orig : it.Value));
                        if (s.WeaponXpFromValueCapped) e = Math.Min(e, Math.Max(25, s.WeaponXpCapPerTier * row.Grade / 2));
                        row.Exp = e; row.Ok = s.SmithXpRoundFix ? Math.Abs(row.Xp - e) <= 1 : (bool?)null;   // recenzja 306b: klucz NIE - gra skaluje po staremu (XpPostfix)
                        row.Note = "wartosc " + it.Value + ", dawna " + orig + (s.SmithXpRoundFix ? "" : " (klucz SmithXpRoundFix NIE - stare skalowanie, wzor tylko informacyjnie)");
                    }
                    catch (Exception e) { Stumble("Kowal(przetop)", e); row.Note = "wyjatek " + e.GetType().Name; row.Ok = false; }
                }
                try
                {
                    foreach (var f0 in model.GetRefiningFormulas(hero))
                    {
                        var f = f0;
                        var outIt = Recipes.MaterialItem(f.Output);
                        var row = new Row { Kind = "rafinacja", Id = (outIt != null ? outIt.StringId : f.Output.ToString()) + " x" + f.OutputCount, Path = "SmithingModel.GetSkillXpForRefining" };
                        rows.Add(row);
                        row.Xp = model.GetSkillXpForRefining(ref f);
                        if (outIt != null)
                        {
                            int orig = MaterialLaw.Orig(outIt);   // jak MaterialLaw.RefineXpPostfix (ceny surowcow sprzed prawa surowcow)
                            int e = TaleWorlds.Library.MathF.Round(0.3f * (float)((s.SmithXpRoundFix && orig > 0 ? orig : outIt.Value) * f.OutputCount));
                            if (s.WeaponXpFromValueCapped) e = Math.Min(e, s.RefineXpCap);
                            row.Exp = e; row.Ok = s.SmithXpRoundFix ? Math.Abs(row.Xp - e) <= 1 : (bool?)null;   // recenzja 306b: jak przetop
                            row.Note = "wartosc " + outIt.Value + ", dawna " + orig + (s.SmithXpRoundFix ? "" : " (klucz SmithXpRoundFix NIE - stare skalowanie, wzor tylko informacyjnie)");
                        }
                    }
                }
                catch (Exception e) { Stumble("Kowal(rafinacja)", e); rows.Add(new Row { Kind = "rafinacja", Id = "-", Path = "SmithingModel.GetSkillXpForRefining", Note = "wyjatek " + e.GetType().Name, Ok = false }); }
            }
            finally
            {
                SmithXpRule.DayFree = dF; SmithXpRule.DayArmour = dA; SmithXpRule.DayOrder = dO; SmithXpRule.DayOrderBonus = dOB; HistoricalPrices.RoundFixed = rf;
                SmithXpRule.DayFreeXp = xF; SmithXpRule.DayArmourXp = xA; SmithXpRule.DayOrderXp = xO; SmithXpRule.DayOrderBonusXp = xOB;
                SmithXp.ClearTag();
            }
            // tabela
            foreach (var r in rows)
                Log.Info(P + "kowal - " + r.Kind + " | " + r.Id + (r.Grade > 0 ? " t" + r.Grade : "") + " | stamina " + (r.Stamina >= 0 ? r.Stamina.ToString(Inv) : "-") + ", trudnosc "
                         + (r.Diff >= 0 ? r.Diff.ToString(Inv) : "-") + ", nauka " + (r.Learn >= 0 ? r.Learn.ToString("0.000", Inv) : "-") + " | droga " + r.Path + ": XP " + r.Xp
                         + " | wzor niezaleznie " + (r.Exp >= 0 ? r.Exp.ToString(Inv) : "-") + " | surowe (nauka 1, bez sufitu) " + (r.Raw >= 0 ? r.Raw.ToString(Inv) : "-")
                         + (r.Table > 0 ? " | tabela 305d " + r.Table : "") + (r.Note != null ? " | " + r.Note : "") + " -> " + (r.Ok == null ? "INFO" : r.Ok.Value ? "TAK" : "NIE") + ".");
            // werdykt
            int n = 0, nOk = 0, nBad = 0;
            foreach (var r in rows) { if (r.Ok == null) continue; n++; if (r.Ok.Value) nOk++; else nBad++; }
            var bows = new int[5];
            bool bowsOk = rule;
            for (int g = 2; g <= 4; g++)
            {
                Row br = rows.Find(x => x.Kind == "luk t" + g);
                bows[g] = br != null && br.Raw >= 0 ? br.Raw : -1;
                int t = g == 2 ? 238 : g == 3 ? 546 : 980;
                if (bows[g] < 0 || Math.Abs(bows[g] - t) > 0.15 * t) bowsOk = false;
            }
            int swordMin = int.MaxValue;
            foreach (var r in rows) if (r.Kind.StartsWith("miecz", StringComparison.Ordinal) && r.Id != "-") swordMin = Math.Min(swordMin, r.Xp);
            bool swordOk = swordMin != int.MaxValue && swordMin > 50;
            double smSum = 0; int smN = 0, refMin = int.MaxValue, refN = 0;
            foreach (var r in rows)
            {
                if (r.Kind == "przetop") { smSum += r.Xp; smN++; }
                else if (r.Kind == "rafinacja" && r.Id != "-") { refN++; refMin = Math.Min(refMin, r.Xp); }
            }
            double smAvg = smN > 0 ? smSum / smN : -1;
            bool smOk = smN > 0 && smAvg > 1.5;
            float xp1 = dev.GetSkillXp(craft); int tot1 = dev.TotalXp, st1 = Forge.Stamina(hero), g1 = hero.Gold;
            bool same = Math.Abs(xp1 - xp0) < 0.001f && tot1 == tot0 && st1 == st0 && g1 == g0;
            int st = _stumbles - stumbles0;
            bool ok = rule && nBad == 0 && n > 0 && bowsOk && swordOk && smOk && same && st == 0;
            string patches = "wolne kucie [" + Owners(AccessTools.Method(model.GetType(), "GetSkillXpForSmithingInFreeBuildMode", new[] { typeof(ItemObject) }))
                             + "], przetop [" + Owners(AccessTools.Method(model.GetType(), "GetSkillXpForSmelting", new[] { typeof(ItemObject) }))
                             + "], rafinacja [" + Owners(AccessTools.Method(model.GetType(), "GetSkillXpForRefining")) + "]";
            Log.Info(P + "kowal - werdykt | probek " + rows.Count + ", zgodnych z wzorem " + nOk + "/" + n + " (niezgodnych " + nBad + ")"
                     + ", luk surowe t2/t3/t4 " + bows[2] + "/" + bows[3] + "/" + bows[4] + " wobec 238/546/980 +-15% (" + Tn(bowsOk) + ")"
                     + ", miecz najmniej " + (swordMin == int.MaxValue ? -1 : swordMin) + " (wymog > 50: " + Tn(swordOk) + ")"
                     + ", przetop srednio " + (smAvg >= 0 ? smAvg.ToString("0.0", Inv) : "-1") + " w " + smN + " (wymog > 1.5: " + Tn(smOk) + ")"
                     + ", rafinacja " + refN + " formul, najmniej " + (refN > 0 ? refMin : -1)
                     + " | kowal " + hero.Name + ": Smithing " + skill + ", perk Practical Smith " + Tn(perk) + " | klucze SmithXpOneRule " + Tn(rule) + ", SmithXpPerStaminaTier " + rate
                     + ", SmithXpRoundFix " + Tn(s.SmithXpRoundFix) + ", WeaponXpFromValueCapped " + Tn(s.WeaponXpFromValueCapped) + " | model " + model.GetType().Name + ", latki: " + patches
                     + " | stan bohatera bez zmian " + Tn(same) + " (XP umiejetnosci " + F1(xp0) + " -> " + F1(xp1) + ", TotalXp " + tot0 + " -> " + tot1 + ", stamina " + st0 + " -> " + st1
                     + ", zloto " + g0 + " -> " + g1 + ") | potkniecia " + st + " -> wynik " + (ok ? "TAK" : "NIE") + ".");
        }

        // ============================================================ (2) karawana gracza
        private static bool Free(Hero h)
        {
            return h != null && h != Hero.MainHero && h.Clan == Clan.PlayerClan && h.GovernorOf == null && h.CanLeadParty();
        }

        private static Hero PickLeader()
        {
            var mp = MobileParty.MainParty;
            if (mp == null) return null;
            Hero fam = null;
            foreach (var e in mp.MemberRoster.GetTroopRoster())
            {
                var h = e.Character != null ? e.Character.HeroObject : null;
                if (!Free(h)) continue;
                if (h.IsPlayerCompanion) return h;   // najpierw towarzysz (jak Jeff: karawany dowodza towarzysze)
                if (fam == null) fam = h;
            }
            return fam;   // inaczej czlonek rodu w druzynie (rozmowa tez go dopuszcza)
        }

        private static bool TownOk(Settlement st)
        {
            if (st == null || !st.IsTown || st.Town == null || st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null)) return false;
            var mf = Hero.MainHero.MapFaction;
            if (mf != null && st.MapFaction != null && FactionManager.IsAtWarAgainstFaction(st.MapFaction, mf)) return false;
            return CaravanHelper.GetRandomCaravanTemplate(st.Culture, false, !st.HasPort) != null;
        }

        private static Settlement PickTown()
        {
            var cur = Settlement.CurrentSettlement;
            if (TownOk(cur)) return cur;   // gracz w miescie - jak rozmowa
            Settlement best = null; float bd = float.MaxValue; bool bestLand = false;
            var p = MobileParty.MainParty.GetPosition2D;
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (!TownOk(st)) continue;
                bool land = !st.HasPort;   // ladowa karawana - bez konwoju morskiego, gdy sie da
                float d = p.DistanceSquared(st.GetPosition2D);
                if (best == null || (land && !bestLand) || (land == bestLand && d < bd)) { best = st; bd = d; bestLand = land; }
            }
            return best;
        }

        private static int PlayerCaravans()
        {
            int n = 0;
            var h = Hero.MainHero;
            if (h == null || h.OwnedCaravans == null) return 0;
            foreach (var c in h.OwnedCaravans) if (c != null && c.MobileParty != null && c.MobileParty.IsActive && !Measure31.IsDteConvoy(c.MobileParty)) n++;
            return n;
        }

        /// <summary>false = proba odlozona (druzyna gracza w bitwie / oblezeniu / niewoli) - Daily ponawia nastepnej doby; true = rozstrzygnieta.</summary>
        private static bool Caravan(int day)
        {
            var mp0 = MobileParty.MainParty;
            if (Hero.MainHero.IsPrisoner || mp0 == null || mp0.MapEvent != null || mp0.SiegeEvent != null)
            {
                Log.Info(P + "karawana - bez tworzenia: druzyna gracza w bitwie/oblezeniu/niewoli (rozmowa z kupcem jest tylko w miescie, poza bitwa), proba " + (_carTries + 1) + "/5"
                         + (_carTries + 1 < 5 ? ", nastepna doba" : ", ostatnia") + " -> wynik POMINIETY.");
                return false;
            }
            if (_carMark != null) { Log.Info(P + "karawana - bez tworzenia: karawana testu juz utworzona w tej kampanii (zapis: " + _carMark + "), karawan gracza " + PlayerCaravans() + " -> wynik POMINIETY."); return true; }
            int have = PlayerCaravans();
            if (have > 0) { Log.Info(P + "karawana - bez tworzenia: gracz ma juz karawan " + have + " (linie 305a i 259 maja co liczyc) -> wynik POMINIETY."); return true; }
            var town = PickTown();
            if (town == null) { Log.Info(P + "karawana - bez tworzenia: brak miasta (bez oblezenia, bitwy i wojny z graczem, z szablonem karawany) -> wynik NIE."); return true; }
            var hero = Hero.MainHero;
            int cost = Campaign.Current.Models.CaravanModel.GetCaravanFormingCost(false, town.HasPort);
            if (hero.Gold < cost) { Log.Info(P + "karawana - bez tworzenia: zloto gracza " + hero.Gold + " < koszt rozmowy " + cost + " (" + town.Name + ") -> wynik POMINIETY."); return true; }
            var leader = PickLeader();
            // recenzja 306b: rozmowa 1.4.8 wymaga wodza (FindSuitableCompanionsToLeadCaravan().Count > 0) - karawana bez wodza to droga, ktorej gracz nie ma
            if (leader == null) { Log.Info(P + "karawana - bez tworzenia: brak wolnego towarzysza ani czlonka rodu w druzynie (rozmowa z kupcem go wymaga) -> wynik NIE."); return true; }
            string patches = Owners(AccessTools.Method(typeof(CaravanConversationsCampaignBehavior), "conversation_magistrate_form_a_caravan_accept_on_consequence"));
            int g0 = hero.Gold, t0 = town.Town.Gold;
            var tpl = CaravanHelper.GetRandomCaravanTemplate(town.Culture, false, !town.HasPort);
            var ctx = CaravanCapital.OpenPlayerFor(town);
            MobileParty mp = null;
            try
            {
                if (leader.CurrentSettlement != null) LeaveSettlementAction.ApplyForCharacterOnly(leader);
                mp = CaravanPartyComponent.CreateCaravanParty(hero, town, tpl, false, leader, null, false);
                GiveGoldAction.ApplyForCharacterToSettlement(hero, town, cost);
            }
            finally { if (ctx != null) CaravanCapital.CtxFin(ctx); }
            int g1 = hero.Gold, t1 = town.Town.Gold;
            if (mp != null) _carMark = mp.StringId + "@" + day;
            long paid = g0 - g1;
            long k = ctx != null ? ctx.K : -1;
            int purse = mp != null ? mp.PartyTradeGold : -1;
            long want = ctx != null && k >= 0 ? Math.Min(k, paid) : -1;
            bool own = mp != null && mp.IsActive && mp.IsCaravan && mp.CaravanPartyComponent != null && mp.CaravanPartyComponent.Owner == hero && mp.ActualClan == Clan.PlayerClan;
            bool goldOk = paid == cost;
            bool purseOk = want < 0 ? purse > 0 : purse == want;
            // recenzja 306b: zamknieta ekonomia po stronie miasta - zaplata wchodzi do kasy, kapital E3/7 z niej wychodzi (bez E3/7 kiesa z modelu, kasa + zaplata)
            long tWant = (long)t0 + paid - (ctx != null ? Math.Max(0, purse) : 0);
            bool townOk = t1 == tWant;
            bool ok = own && goldOk && purseOk && townOk;
            Log.Info(P + "karawana - utworzona | '" + (mp != null && mp.Name != null ? mp.Name.ToString() : "-") + "' (" + (mp != null ? mp.StringId : "-") + ") w " + town.Name + " (port " + Tn(town.HasPort)
                     + ", gracz w miescie " + Tn(Settlement.CurrentSettlement == town) + "), wodz " + leader.Name + (leader.IsPlayerCompanion ? " (towarzysz)" : " (rod)")
                     + ", szablon " + (tpl != null ? tpl.StringId : "-") + " | koszt rozmowy " + cost + ", zloto gracza " + g0 + " -> " + g1 + " (zaplacone " + paid + ", " + Tn(goldOk) + ")"
                     + " | kiesa karawany " + purse + " (E3/7 " + (ctx != null ? "kontekst gracza: kapital modelu " + k + ", oczekiwane min(kapital, zaplacone) " + want : "WYLACZONE (Caravan Capital Real) - kapital z modelu") + ", " + Tn(purseOk) + ")"
                     + ", kasa miasta " + t0 + " -> " + t1 + " (oczekiwane " + tWant + (ctx != null ? " = przed + zaplacone - kiesa" : " = przed + zaplacone") + ", " + Tn(townOk) + ") | wlasciciel gracz, rod gracza " + Tn(own) + " | latki na konsekwencji rozmowy (sonda idzie sekwencja z dekompilacji, kontekst E3/7 wprost): " + patches
                     + " | znacznik zapisu " + (_carMark ?? "-") + " -> wynik " + (ok ? "TAK" : "NIE") + ".");
            return true;
        }

        private static void CaravanDay(int day)
        {
            int n = PlayerCaravans();
            bool ok, skip;
            string body = CaravanSettle.ExpProbe306(n, out ok, out skip);
            Log.Info(P + "karawana - wydatki doby " + day + " | karawan gracza " + n + " | " + body + " -> wynik " + (skip ? "POMINIETY" : ok ? "TAK" : "NIE") + ".");
            // 307a: wynik karawan gracza z rozbiciem (konie osobno, pierwsza partia towaru i wynik bez niej) - tylko odczyt ksiegi 259
            try { Log.Info(P + "karawana - wynik doby " + day + " (307a) | " + CaravanSettle.Res307() + "."); } catch (Exception e) { Stumble("Res307", e); }
        }

        // ============================================================ (3) zmiana produkcji warsztatu gracza
        private static void Retool(int day)
        {
            var hero = Hero.MainHero;
            var s = Settings.Current;
            if (!WorkshopTrade.On || !s.WorkshopRetoolSettlesCapital)
            { Log.Info(P + "warsztat - zmiana produkcji doba " + day + " | bez proby: WorkshopTrade " + Tn(WorkshopTrade.On) + ", WorkshopRetoolSettlesCapital " + Tn(s.WorkshopRetoolSettlesCapital) + " -> wynik POMINIETY."); return; }
            Workshop pick = null; bool armsOnly = false, luxPicked = false; int luxSkipped = 0;
            foreach (var w in hero.OwnedWorkshops)
            {
                if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Settlement == null || w.Settlement.Town == null) continue;
                if (WorkshopTrade.ProbeHasArmsLine(w.WorkshopType)) continue;
                if (HasLuxLine(w.WorkshopType)) { luxSkipped++; continue; }   // 307c: warsztat z linia luksusu gracza zostaje (reszta testu mierzy jego zarobek)
                if (pick == null || w.ProfitMade < pick.ProfitMade) pick = w;
            }
            if (pick == null)
            {
                foreach (var w in hero.OwnedWorkshops)
                {
                    if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Settlement == null || w.Settlement.Town == null || WorkshopTrade.ProbeHasArmsLine(w.WorkshopType)) continue;
                    if (pick == null || w.ProfitMade < pick.ProfitMade) pick = w;
                }
                luxPicked = pick != null;   // 307c: jedyny kandydat z linia luksusu - przestawiony z powrotem nastepnej doby
            }
            if (pick == null)
            {
                foreach (var w in hero.OwnedWorkshops)
                    if (w != null && w.WorkshopType != null && !w.WorkshopType.IsHidden && w.Settlement != null && w.Settlement.Town != null && (pick == null || w.ProfitMade < pick.ProfitMade)) pick = w;
                armsOnly = pick != null;
            }
            if (pick == null) { Log.Info(P + "warsztat - zmiana produkcji doba " + day + " | gracz nie ma warsztatu -> wynik POMINIETY."); return; }
            WorkshopType to = null; int toMark = int.MaxValue;
            foreach (var t in WorkshopType.All)
            {
                if (t == null || t.IsHidden || t == pick.WorkshopType || WorkshopTrade.ProbeHasArmsLine(t)) continue;
                if (Campaign.Current.Models.WorkshopModel.GetConvertProductionCost(t) > hero.Gold) continue;
                int m = WorkshopTrade.StartCapitalFor(t);
                if (to == null || m < toMark || (m == toMark && string.CompareOrdinal(t.StringId, to.StringId) < 0)) { to = t; toMark = m; }
            }
            if (to == null) { Log.Info(P + "warsztat - zmiana produkcji doba " + day + " | brak typu docelowego (bez linii broni, sprzet w zasiegu kiesy " + hero.Gold + ") -> wynik POMINIETY."); return; }
            var town = pick.Settlement;
            string from = pick.WorkshopType.StringId;
            int g0 = hero.Gold, cap0 = pick.Capital, mark0 = pick.InitialCapital, tg0 = town.Town.Gold;
            long b0 = CaravanSettle.WsBasis306(pick);
            int tools, mark, cap, back, add;
            string talk = WorkshopTrade.RetoolPromise(pick, to, g0, out tools, out mark, out cap, out back, out add);
            long rb0, ra0, rs0; int rn0;
            WorkshopTrade.ProbeRetoolCounters(out rb0, out ra0, out rs0, out rn0);
            ChangeProductionTypeOfWorkshopAction.Apply(pick, to);   // jak rozmowa (ignoreCost domyslnie false - ConvertPrefix przejmuje sprzet)
            long rb1, ra1, rs1; int rn1;
            WorkshopTrade.ProbeRetoolCounters(out rb1, out ra1, out rs1, out rn1);
            int g1 = hero.Gold, cap1 = pick.Capital, mark1 = pick.InitialCapital, tg1 = town.Town.Gold;
            long b1 = CaravanSettle.WsBasis306(pick);
            long dBack = rb1 - rb0, dAdd = ra1 - ra0, dShort = rs1 - rs0;
            bool typeOk = pick.WorkshopType == to;
            bool settleOk = dBack == back && dAdd == add && rn1 - rn0 == 1;
            int goldWant = g0 - tools + back - add;
            bool goldOk = g1 == goldWant;
            int capWant = cap0 - back + add;
            bool capOk = cap1 == capWant;
            bool markOk = mark1 == mark;
            long bWant = b0;
            if (back > 0 || add > 0)
            {
                long start = b0 >= 0 ? b0 : Math.Min((long)mark0, cap0);
                bWant = Math.Max(0L, start - back) + add;
            }
            bool basisOk = b1 == bWant;
            // recenzja 306b: kasa miasta + sprzet (ConvertPostfix: gracz -> kasa miasta; RetoolSettle miasta nie rusza)
            long tgWant = (long)tg0 + tools;
            bool townOk = tg1 == tgWant;
            bool ok = typeOk && settleOk && goldOk && capOk && markOk && basisOk && townOk;
            Log.Info(P + "warsztat - zmiana produkcji doba " + day + " | " + town.Name + " " + from + " -> " + to.StringId + (armsOnly ? " (gracz ma tylko warsztaty z linia broni)" : "")
                     + " (ProfitMade " + pick.ProfitMade + ") | rozmowa: sprzet " + tools + ", nowy znacznik " + mark + ", kapital " + cap + ", zwrot " + back + ", doplata " + add
                     + " | '" + talk + "' | przelew RetoolSettle: zwrot " + dBack + ", doplata " + dAdd + ", brak " + dShort + ", zmian " + (rn1 - rn0) + " (" + Tn(settleOk) + ")"
                     + " | kapital " + cap0 + " -> " + cap1 + " (oczekiwane " + capWant + ", " + Tn(capOk) + "), znacznik " + mark0 + " -> " + mark1 + " (" + Tn(markOk) + ")"
                     + ", kapital wlasny (259) " + b0 + " -> " + b1 + " (oczekiwane " + bWant + ", " + Tn(basisOk) + ")"
                     + " | zloto gracza " + g0 + " -> " + g1 + " (oczekiwane " + goldWant + " = przed - sprzet + zwrot - doplata, " + Tn(goldOk) + "), kasa miasta " + tg0 + " -> " + tg1 + " (oczekiwane " + tgWant + " = przed + sprzet, " + Tn(townOk) + ")"
                     + " | typ zmieniony " + Tn(typeOk) + " | 307c: z linia luksusu pominiete " + luxSkipped + (luxPicked ? ", jedyny kandydat z luksusem - powrot nastepnej doby" : "")
                     + " -> wynik " + (ok ? "TAK" : "NIE") + ".");
            if (luxPicked && typeOk) { _revW = pick; _revType = WorkshopType.Find(from); _revDay = day; }
        }

        /// <summary>307c: typ ma linie z wyjsciem zbytku BK (srebro, aksamit, klejnoty, przyprawy, futra).</summary>
        private static bool HasLuxLine(WorkshopType t)
        {
            if (t == null) return false;
            foreach (var p in t.Productions)
                if (p.Outputs != null)
                    foreach (var o in p.Outputs)
                        if (o.Item1 != null && Array.IndexOf(LuxCats, (o.Item1.StringId ?? "").ToLowerInvariant()) >= 0) return true;
            return false;
        }

        /// <summary>307c (3a): powrot produkcji warsztatu z linia luksusu (jedyny kandydat sondy) - ta sama akcja co rozmowa, z kosztem sprzetu.</summary>
        private static void Revert(int day)
        {
            var w = _revW; var t = _revType;
            if (w == null || t == null || w.Owner != Hero.MainHero) { Log.Info(P + "warsztat - powrot produkcji doba " + day + " | warsztat nie jest juz gracza albo brak typu -> wynik POMINIETY."); return; }
            int g0 = Hero.MainHero.Gold; string from = w.WorkshopType != null ? w.WorkshopType.StringId : "?";
            int tools, mark, cap, back, add;
            WorkshopTrade.RetoolPromise(w, t, g0, out tools, out mark, out cap, out back, out add);   // recenzja: zloto wedlug tej samej obietnicy co zmiana produkcji
            ChangeProductionTypeOfWorkshopAction.Apply(w, t);
            bool typeOk = w.WorkshopType == t;
            int g1 = Hero.MainHero.Gold, goldWant = g0 - tools + back - add;
            bool goldOk = g1 == goldWant;
            bool ok = typeOk && goldOk;
            Log.Info(P + "warsztat - powrot produkcji doba " + day + " | " + w.Settlement.Name + " " + from + " -> " + t.StringId + " | zloto gracza " + g0 + " -> " + g1
                     + " (oczekiwane " + goldWant + " = przed - sprzet " + tools + " + zwrot " + back + " - doplata " + add + ", " + Tn(goldOk) + ")"
                     + " | typ przywrocony " + Tn(typeOk) + " -> wynik " + (ok ? "TAK" : "NIE") + ".");
        }

        /// <summary>
        /// 307c (3b): kupno warsztatu bez linii broni od notabla i natychmiastowa odsprzedaz - ta sama droga co rozmowy (WorkshopsCharactersCampaignBehavior, dekompilacja 1.4.8):
        /// ChangeOwnerOfWorkshopAction.ApplyByPlayerBuying(w) (cena = WorkshopModel.GetCostForPlayer, gracz -> wlasciciel), potem GetNotableOwnerForWorkshop
        /// i ApplyByPlayerSelling(w, kupiec, typ) (cena = GetCostForNotable; WorkshopTrade.SalePrefix/SalePostfix: placi kupiec z kiesy, najwyzej tyle, ile ma).
        /// Sprawdza: cena kupna = GetCostForPlayer, zloto gracza i kiesa sprzedawcy o te cene, odkup <= cena kupna (bez petli zysku), zaplata odkupu = kiesa kupca,
        /// kapital warsztatu po obu zmianach bez zmian (OwnerPrefix). Najtanszy warsztat notabla bez linii broni i luksusu, na ktory gracza stac (z zapasem 2x).
        /// </summary>
        private static void BuyResell(int day)
        {
            var hero = Hero.MainHero;
            var wm = Campaign.Current.Models.WorkshopModel;
            if (!WorkshopTrade.On) { Log.Info(P + "warsztat - kupno i odsprzedaz doba " + day + " | WorkshopTrade NIE -> wynik POMINIETY."); return; }
            if (hero.OwnedWorkshops.Count >= wm.GetMaxWorkshopCountForClanTier(Clan.PlayerClan.Tier))
            { Log.Info(P + "warsztat - kupno i odsprzedaz doba " + day + " | gracz ma juz najwiecej warsztatow (" + hero.OwnedWorkshops.Count + ") -> wynik POMINIETY."); return; }
            Workshop pick = null; int price = int.MaxValue;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Workshops == null || t.Settlement == null || t.Settlement.IsUnderSiege || t.Settlement.MapFaction == null) continue;
                if (hero.MapFaction != null && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, t.Settlement.MapFaction)) continue;
                foreach (var w in t.Workshops)
                {
                    if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Owner == null || w.Owner == hero || !w.Owner.IsNotable || !w.Owner.IsAlive) continue;
                    if (WorkshopTrade.ProbeHasArmsLine(w.WorkshopType) || HasLuxLine(w.WorkshopType)) continue;
                    int c = wm.GetCostForPlayer(w);
                    if (c <= 0 || (long)c * 2 > hero.Gold || c >= price) continue;
                    pick = w; price = c;
                }
            }
            if (pick == null) { Log.Info(P + "warsztat - kupno i odsprzedaz doba " + day + " | brak warsztatu notabla bez linii broni w zasiegu kiesy " + hero.Gold + " (z zapasem 2x) -> wynik POMINIETY."); return; }
            var seller = pick.Owner; var town = pick.Settlement; string type = pick.WorkshopType.StringId;
            int g0 = hero.Gold, s0 = seller.Gold, cap0 = pick.Capital;
            ChangeOwnerOfWorkshopAction.ApplyByPlayerBuying(pick);
            int g1 = hero.Gold, s1 = seller.Gold, cap1 = pick.Capital;
            bool own = pick.Owner == hero;
            bool buyOk = own && g0 - g1 == price && s1 - s0 == price && cap1 == cap0;
            int back = -1, paid = -1, b0 = -1, b1 = -1, g2 = g1, g3 = g1, cap2 = cap1; string buyerName = "-"; bool sold = false;
            if (own)
            {
                back = wm.GetCostForNotable(pick);
                var buyer = wm.GetNotableOwnerForWorkshop(pick);
                if (buyer != null)
                {
                    buyerName = buyer.Name.ToString();
                    b0 = buyer.Gold; g2 = hero.Gold;
                    ChangeOwnerOfWorkshopAction.ApplyByPlayerSelling(pick, buyer, pick.WorkshopType);
                    b1 = buyer.Gold; g3 = hero.Gold; cap2 = pick.Capital;
                    sold = pick.Owner == buyer;
                    paid = g3 - g2;
                }
            }
            string left = "";
            if (pick.Owner == hero && !sold)
            {
                // recenzja: bez kupca warsztat nie zostaje u gracza do konca testu (przeklamalby pomiary 305b) - odsprzedaz sprzedawcy, ta sama akcja
                try
                {
                    if (seller != null && seller.IsAlive && seller.IsNotable) { ChangeOwnerOfWorkshopAction.ApplyByPlayerSelling(pick, seller, pick.WorkshopType); }
                    left = pick.Owner == hero ? " | warsztat zostal u gracza" : " | bez kupca - odsprzedany sprzedawcy " + seller.Name + " (zloto gracza " + g1 + " -> " + hero.Gold + ")";
                }
                catch (Exception e) { left = " | odsprzedaz sprzedawcy: wyjatek " + e.GetType().Name + " - warsztat zostal u gracza"; }
            }
            bool noLoop = back >= 0 && back <= price;
            bool payOk = sold && paid == Math.Min(back, Math.Max(0, b0)) && b0 - b1 == paid;
            bool capOk = cap2 == cap0;
            bool ok = buyOk && sold && noLoop && payOk && capOk;
            Log.Info(P + "warsztat - kupno i odsprzedaz doba " + day + " | " + town.Name + " " + type + " od " + seller.Name + " | cena kupna (GetCostForPlayer) " + price
                     + " | zloto gracza " + g0 + " -> " + g1 + ", kiesa sprzedawcy " + s0 + " -> " + s1 + ", kapital " + cap0 + " -> " + cap1 + " (" + Tn(buyOk) + ")"
                     + " | odkup (GetCostForNotable) " + back + " <= cena kupna " + Tn(noLoop) + " | kupiec " + buyerName + ": kiesa " + b0 + " -> " + b1 + ", zloto gracza " + g2 + " -> " + g3
                     + " (zaplata " + paid + ", " + Tn(payOk) + "), kapital po odsprzedazy " + cap2 + " (" + Tn(capOk) + ") | strata gracza na obrocie " + (g0 - g3) + left
                     + " -> wynik " + (ok ? "TAK" : "NIE") + ".");
        }
    }
}
