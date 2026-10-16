using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// OWOCE I MIOD WE WSIACH: WAGI Z LISTY BANNER KINGS, WZOR PLONU GRY (decyzja Jeffa 15.10 TAK - "placek i marchew"; DECYZJA CLAUDE noc 15/16.10: liczby BK, bez ula).
    /// Doprecyzowanie po recenzji 16.10 [K]: wlasny model BK (BKVillageProductionModel.cs:86, num3 = waga / suma wag listy) dzieli sile robocza wsi na wszystkie
    /// pozycje listy - tam owoce bralyby udzial od zboza. U Jeffa ten model NIE liczy (czynny jest model gry przez NavalDLC - log 11:02:32), a w modelu gry
    /// (DefaultVillageProductionCalculatorModel.cs:22-36) kazda pozycja typu wsi daje plon niezaleznie: waga x (poziom ognisk + 1) x 0.5. Owoce sa wiec PLONEM
    /// DODATKOWYM (jakby staly na liscie typu wsi), nie dzielonym z glownym: farma zboza dostaje +4 sztuki owocow na swoje 50 zboza (ok. +8% sztuk), drwale i
    /// trzebiciele miod ponad swoje dotychczasowe plony - te plony bez zmian (DECYZJA: nie tniemy plonu zboza - glowne jedzenie swiata; kontrola - wiersz INFO
    /// NC-12b w grupie nceny: zboze farm z linii "Towary: ... grain ... wsie (wheat_farm N)").
    /// Dowod (log 2026-10-15_10-56-30, 12:10:56): "Towary: dzien 108945 - carrot (2 d): ... produkcja 208 [inne ticki dobowe 208 (osady 208)] | zuzycie 318
    /// [przepadlo z rozbitymi partiami 318]" - zadne miasto nie ma marchwi, jablek, pomaranczy ani miodu ("apple ... na polkach 0 szt., pusto w 97 miastach").
    /// Przyczyna [K]: BK PopulationManager.GetProductions (dec-bk PopulationManager.cs:337-355) dokleja do listy wsi: farma zboza - jablka 2 i marchew 2, gaj
    /// oliwny - pomarancze 2, drwale i trzebiciele - miod 0.5 (+1 za poziom ula); tick BK (EconomyPatches.TickGoodProductionPatch) pyta czynny model o kazda
    /// pozycje listy, a model gry (NavalDLC -> DefaultVillageProductionCalculatorModel) liczy tylko towary z VillageType.Productions - wychodzi 0.
    /// Regula: w MaterialLaw.ProdPostfix (najbardziej zewnetrzny model, raz), zaraz po bazie wielblada Qarthu i PRZED mnoznikami (ludzie wsi P1, spustoszenie,
    /// wolne rece, regulator, zima - czynniki ExplainedNumber): baza = waga BK x (poziom ognisk + 1) x 0.5 - ten sam wzor, ktorym model gry liczy kazdy plon,
    /// tylko wies Normal z targiem (TradeBound) i tylko gdy przedmiotu NIE MA na liscie typu wsi (inny mod mogl go dopisac - wtedy gra liczy sama).
    /// Plon idzie do magazynu wsi (tick BK, OnItemProduced - ksiega towarow "wsie"), wozy wsi wioza caly magazyn na targ (paczka 122). Ul BK (Skeps) pominiety -
    /// jego poziom siedzi w danych wsi BK, a waga 0.5 to projekt BK dla wsi bez ula. Chleb, kury i gesi z listy BK - poza ta zmiana.
    /// Bez listy BK (bez Banner Kings) nikt o te towary nie pyta - nic sie nie dzieje. Nic w zapisie. Wyjatek - potkniecie (licznik, pierwsze do Log.Error).
    /// </summary>
    internal static class VillageFruit
    {
        private static readonly object Gate = new object();
        private static readonly TextObject Txt = new TextObject("{=!}Armoury: village fruit (Banner Kings)");
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _stumbles, _calls;
        private static bool _errLogged;
        private static double _carrot, _apple, _orange, _honey;   // baza dopisana od poprzedniej linii doby (wszystkie pytania modelu)

        /// <summary>Waga BK towaru w typie wsi (PopulationManager.GetProductions); 0 = BK go tam nie dopisuje.</summary>
        internal static float WeightFor(string villageType, string item)
        {
            switch (villageType)
            {
                case "wheat_farm": return item == "carrot" || item == "apple" ? 2f : 0f;
                case "olive_trees": return item == "orange" ? 2f : 0f;
                case "lumberjack":
                case "trapper": return item == "honey" ? 0.5f : 0f;
                default: return 0f;
            }
        }

        private static bool Fruit(string id) { return id == "carrot" || id == "apple" || id == "orange" || id == "honey"; }

        private static bool InTypeList(VillageType vt, ItemObject item)
        {
            foreach (var p in vt.Productions) if (p.Item1 == item) return true;
            return false;
        }

        private static void Stumble(Exception e)
        {
            lock (Gate) _stumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("VillageFruit", e); }
        }

        /// <summary>MaterialLaw.ProdPostfix, przed mnoznikami: baza owocow i miodu wsi wedlug wag BK.</summary>
        internal static void Base(Village v, ItemObject item, ref ExplainedNumber r)
        {
            try
            {
                if (item == null || v == null) return;
                string id = item.StringId;
                if (id == "Egg") { EggBase(v, item, ref r); return; }   // N2-E1 (noc-ekonomia2): jaja wsi rolniczych wedlug wag drobiu BK
                if (id == null || !Fruit(id)) return;
                var s = Settings.Current;
                if (s == null || !s.VillageFruitFromBk) return;
                if (v.VillageState != Village.VillageStates.Normal || v.TradeBound == null || v.VillageType == null) return;
                float w = WeightFor(v.VillageType.StringId, id);
                if (!(w > 0f) || InTypeList(v.VillageType, item)) return;
                float x = w * (v.GetHearthLevel() + 1) * 0.5f;
                if (!(x > 0f)) return;
                r.Add(x, Txt);
                lock (Gate)
                {
                    _calls++;
                    if (id == "carrot") _carrot += x; else if (id == "apple") _apple += x; else if (id == "orange") _orange += x; else _honey += x;
                }
            }
            catch (Exception e) { Stumble(e); }
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (po VillageClimate / GoodsRates / VineOlive - typy wsi tej sesji): linia startu.</summary>
        internal static void SessionStart()
        {
            lock (Gate) { _stumbles = 0; _calls = 0; _carrot = _apple = _orange = _honey = 0; _errLogged = false; }
            var s = Settings.Current;
            const string Head = "Owoce wsi (VillageFruitFromBk): start sesji | ";
            if (s == null || !s.VillageFruitFromBk) { Log.Info(Head + "klucz VillageFruitFromBk NIE | WYLACZONE (MCM) - marchew, jablka, pomarancze i miod tylko z niczego BK, jak dotad | potkniecia 0."); return; }
            int wheat = 0, olive = 0, lumber = 0, trap = 0, skipped = 0, noMarket = 0;
            double bc = 0, ba = 0, bo = 0, bh = 0;
            try
            {
                foreach (var v in Village.All)
                {
                    try
                    {
                        if (v == null || v.VillageType == null) continue;
                        string t = v.VillageType.StringId;
                        bool any = false;
                        foreach (var id in new[] { "carrot", "apple", "orange", "honey" })
                        {
                            float w = WeightFor(t, id);
                            if (!(w > 0f)) continue;
                            var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(id);
                            if (it == null) continue;
                            if (InTypeList(v.VillageType, it)) { skipped++; continue; }
                            any = true;
                            if (v.TradeBound == null) continue;
                            double x = w * (v.GetHearthLevel() + 1) * 0.5;
                            if (id == "carrot") bc += x; else if (id == "apple") ba += x; else if (id == "orange") bo += x; else bh += x;
                        }
                        if (!any) continue;
                        if (v.TradeBound == null) noMarket++;
                        if (t == "wheat_farm") wheat++; else if (t == "olive_trees") olive++; else if (t == "lumberjack") lumber++; else if (t == "trapper") trap++;
                    }
                    catch (Exception e) { Stumble(e); }
                }
            }
            catch (Exception e) { Stumble(e); }
            Log.Info(Head + "klucz VillageFruitFromBk TAK | CZYNNE | wheat_farm " + wheat + " wsi (marchew 2, jablka 2), olive_trees " + olive + " (pomarancze 2), lumberjack " + lumber + " + trapper " + trap + " (miod 0.5)"
                     + " | baza na dobe przed mnoznikami (waga x (poziom ognisk + 1) x 0.5): marchew " + bc.ToString("0", Inv) + ", jablka " + ba.ToString("0", Inv) + ", pomarancze " + bo.ToString("0", Inv) + ", miod " + bh.ToString("0", Inv)
                     + " | juz na liscie typu (pominiete) " + skipped + " | bez targu (bez plonu) " + noMarket
                     + " | lista produkcji BK " + (MineralOnce.Wired ? "jest (tick BK pyta model o owoce)" : "BEZ latki GetProductions - zalezy od BK") + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Owoce wsi (VillageFruitFromBk): dzien" (tylko log; sztuki - linia "Towary: ... wsie").</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.VillageFruitFromBk) return;
            int n, st; double c, a, o, h;
            lock (Gate) { n = _calls; st = _stumbles; c = _carrot; a = _apple; o = _orange; h = _honey; _calls = 0; _carrot = _apple = _orange = _honey = 0; }
            Log.Info("Owoce wsi (VillageFruitFromBk): dzien " + (int)CampaignTime.Now.ToDays + " | baza dopisana w " + n + " pytaniach modelu (tick BK, okna wsi): marchew " + c.ToString("0", Inv)
                     + ", jablka " + a.ToString("0", Inv) + ", pomarancze " + o.ToString("0", Inv) + ", miod " + h.ToString("0", Inv) + " (sztuki z ticku - linia \"Towary\", wsie) | potkniecia sesji " + st + ".");
        }

        // ============================================================ N2-E1: JAJA ZE WSI ROLNICZYCH (noc 16.10, galaz noc-ekonomia2)
        // Raport audytu ekonomii 15.10 rozdz. 3.2 P1 (czesc, ktorej noc-ceny nie zrobila) i tabela 1.8: jaja 26 331 d na dobe budzetu ludu bez towaru (najwieksza pozycja).
        // Dowod (log Jeffa 2026-10-15_10-56-30, 12:10:56): "Egg (8 d): zapas 2090 | produkcja 76 [wsie 42 (cattle_farm 42); inne ticki dobowe 34]" i w "Ceny surowcow":
        // "Eggs ... 3.20/10.00/10.00, na polkach 56 szt., pusto w 88 miastach, budzet mieszczan 28812 d (przy dzisiejszych polkach do 2481 d)" - cena na suficie x10.
        // Przyczyna [K]: BK PopulationManager.GetProductions (dec-bk :304-336) dokleja wsi rolniczej (IsFarmingVillage: klasa Cropland albo FibreFarm - farmy zboza,
        // gaje oliwne, winnice, daktyle, len, jedwab) kure i ges z waga kultury (aserai 0.15, sturgia/khuzait 0.45, empire/battania 0.75, reszta 1.0), ale jaj nie;
        // model gry daje kurze i gesi 0 (spoza VillageType.Productions), a jaja BK robi tylko z drobiu w partiach (BKPartyBehavior.OnDailyTick: 5% na ptaka na dobe).
        // Jaja jako plon wsi BK zna tylko na hodowli bydla (BKVillageTypes: CattleRange Egg 1.5 w VillageType.Productions - liczy gra, te 42 sztuki).
        // Regula (klucz VillageEggsFromBk; DECYZJA CLAUDE noc 16.10: waga jaj = suma wag drobiu BK z listy tej wsi, kura + ges - w partiach BK oba ptaki niosa
        // jaja tak samo): MineralOnce.Postfix dopisuje do listy BK wsi z drobiem pozycje Egg z ta waga (nowa lista - BK-owej nie ruszamy; jaja juz na liscie - nic),
        // tick BK (EconomyPatches.TickGoodProduction) pyta model o jaja, a EggBase dopisuje baze = waga x (poziom ognisk + 1) x 0.5 - ten sam wzor co owoce wyzej
        // i kazdy plon gry, przed mnoznikami (P1 ludzie wsi, rece, zima). Plon dodatkowy: zboze i reszta listy bez zmian; kura i ges dalej 0.
        // GL1 (VillageByPeople): g(typ) = suma((poziom + 1) / 2) / suma(u_v) po wsiach typu - z poziomow ognisk i ludzi, nie z towarow, wiec nowa pozycja go nie
        // rusza; linia GL1 "surowce wobec kalibracji" liczy z VillageType.Productions (jaj z listy BK tam nie ma - jak owocow). Regulator 221 jedzenia nie tnie
        // (HarvestValve.Eligible: !IsFood). Magazyn wsi: gra liczy go z VillageType.Productions (Village.GetWarehouseCapacity = 5 dob produkcji typu), a bramka
        // produkcji (TickProductions: suma WSZYSTKICH sztuk magazynu >= 1.5 W - cala wies stoi) liczy tez jaja i owoce - POPRAWKA PO RECENZJI 16.10: CapPostfix nizej.
        // Obieg: jaja powstaja z pracy wsi (magazyn wsi, ksiega towarow "wsie"), wozy wioza je na targ ta sama droga co reszte plonu - zloto tylko za sprzedaz.
        // Nic w zapisie (lista skladana przez BK co tick). Wyjatek - potkniecie (licznik), lista tej wsi jak z BK.
        private static readonly TextObject TxtEgg = new TextObject("{=!}Armoury: village poultry (Banner Kings)");
        private static readonly Dictionary<Village, float> _eggW = new Dictionary<Village, float>();   // wies -> waga jaj z ostatniego zlozenia jej listy BK (kura + ges)
        private static readonly HashSet<Village> _eggVill = new HashSet<Village>();                     // wsie z jajami na liscie od poprzedniej linii doby
        private static int _eggLists, _eggCalls;
        private static double _eggBase;
        private static ItemObject _egg; private static Campaign _eggFor;

        /// <summary>Klucz VillageEggsFromBk (sam odczyt ustawien - z kazdego watku).</summary>
        internal static bool EggsOn { get { var s = Settings.Current; return s != null && s.VillageEggsFromBk; } }

        /// <summary>Przedmiot BK "Egg" (BKItems: kosz jaj) tej kampanii; null - brak BK.</summary>
        private static ItemObject EggItem()
        {
            var c = Campaign.Current;
            if (!ReferenceEquals(_eggFor, c))
            {
                ItemObject e = null;
                try { e = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("Egg"); } catch { e = null; }
                _egg = e; _eggFor = c;
            }
            return _egg;
        }

        /// <summary>Waga kury (i gesi) BK wedlug kultury wsi - BK PopulationManager.GetProductions :306-332 (do linii startu; w ticku liczy sie waga z listy BK).</summary>
        internal static float PoultryWeight(string culture)
        {
            switch (culture)
            {
                case "aserai": return 0.15f;
                case "sturgia": case "khuzait": return 0.45f;
                case "empire": case "battania": return 0.75f;
                default: return 1f;
            }
        }

        /// <summary>Typ wsi rolniczej BK (DefaultVillageClasses.GetClass: Cropland - farma zboza, gaj oliwny, winnica, daktyle; FibreFarm - len, jedwab). Do linii startu.</summary>
        private static bool FarmType(VillageType t)
        {
            return t != null && (t == DefaultVillageTypes.WheatFarm || t == DefaultVillageTypes.OliveTrees || t == DefaultVillageTypes.VineYard || t == DefaultVillageTypes.DateFarm
                                 || t == DefaultVillageTypes.FlaxPlant || t == DefaultVillageTypes.SilkPlant);
        }

        /// <summary>MineralOnce.Postfix (lista produkcji wsi BK, po filtrze klimatu i wielbladach): lista z jajami o wadze kury + gesi BK; bez drobiu albo z jajami juz
        /// na liscie - ta sama lista. Nowa lista (BK-owej nie ruszamy). Rzuca - wolajacy liczy potkniecie i oddaje liste BK.</summary>
        internal static List<(ItemObject, float)> EggStep(Village v, List<(ItemObject, float)> list)
        {
            if (v == null || list == null) return list;
            var egg = EggItem();
            if (egg == null) return list;
            float w = 0f; bool has = false;
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i].Item1;
                if (it == null) continue;
                if (ReferenceEquals(it, egg)) { has = true; break; }
                string id = it.StringId;
                if (id == "chicken" || id == "goose") w += list[i].Item2;
            }
            if (has || !(w > 0f)) { lock (Gate) _eggW.Remove(v); return list; }
            var res = new List<(ItemObject, float)>(list.Count + 1);
            res.AddRange(list);
            res.Add((egg, w));
            lock (Gate) { _eggW[v] = w; _eggLists++; _eggVill.Add(v); }
            return res;
        }

        /// <summary>Lista tej wsi wrocila do postaci BK (potkniecie postfiksu) - model nie dopisuje jaj do nastepnego zlozenia listy.</summary>
        internal static void ForgetEgg(Village v)
        {
            if (v == null) return;
            lock (Gate) _eggW.Remove(v);
        }

        /// <summary>Base (MaterialLaw.ProdPostfix, przed mnoznikami): jaja wsi, ktorej lista BK dostala jaja (EggStep) - waga x (poziom ognisk + 1) x 0.5.</summary>
        private static void EggBase(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (!EggsOn) return;
            if (v.VillageState != Village.VillageStates.Normal || v.TradeBound == null || v.VillageType == null) return;
            if (InTypeList(v.VillageType, item)) return;                      // jaja w typie wsi (hodowla bydla BK) - liczy gra
            float w;
            lock (Gate) { if (!_eggW.TryGetValue(v, out w)) return; }
            float x = w * (v.GetHearthLevel() + 1) * 0.5f;
            if (!(x > 0f)) return;
            r.Add(x, TxtEgg);
            lock (Gate) { _eggCalls++; _eggBase += x; }
        }

        // ------------------------------------------------------------ magazyn wsi (poprawka po recenzji noc-ekonomia2 16.10)
        // Dowod [K]: Village.GetWarehouseCapacity (twall Village.cs:248) = ceil(5 x (jedzenie + suma modelu po VillageType.Productions)); bramka produkcji gry
        // (VillageGoodProductionCampaignBehavior.TickProductions :143-151) liczy sume WSZYSTKICH sztuk magazynu, wiec plon spoza typu (owoce i miod, jaja) zjadal
        // magazyn bez jego powiekszenia: farma zboza ok. +8% sztuk owocow i ok. +4% jaj - wies stawala o tyle wczesniej, a woz wyjezdzal przy W liczonym bez nich
        // (MarketCarts.Choose liczy kurs tym samym W). Regula (wzorem lasu wsi - VillageWoodlot.CapPostfix: magazyn = 5 dob produkcji, wiec i 5 dob dodatku):
        // W + ceil(5 x dobowy plon dodatkow) - owoce i miod z wag BK (klucz VillageFruitFromBk, lista BK wpieta), jaja z wagi drobiu listy tej wsi (klucz
        // VillageEggsFromBk, wies z jajami na liscie - _eggW); plon = baza (waga x (poziom ognisk + 1) x 0.5, ten sam wzor co Base / EggBase) x czynnik ludzi wsi
        // P1 (VillageByPeople.TryFactor - jak las wsi); zima, rece i spustoszenie pominiete (W w zimie o ulamek doby wiekszy). Tylko wies Normal z targiem i
        // czynnym modelem produkcji z naszym postfiksem (MaterialLaw.ProdModels) - tam, gdzie te plony naprawde powstaja. Bez pytan modelu (bez skutkow ubocznych
        // ksiegi rudy i W2). Klucze obu dodatkow wylaczone - W jak w grze. Wyjatek - W bez zmian (licznik potkniec).
        private static readonly string[] FruitIds = { "carrot", "apple", "orange", "honey" };
        private static readonly Dictionary<Village, int> _capLast = new Dictionary<Village, int>();   // wies -> ostatni dopisek do W (od poprzedniej linii doby)
        private static bool _capWired;
        private static int _capStumbles;

        private static bool InTypeListId(VillageType vt, string id)
        {
            foreach (var p in vt.Productions) if (p.Item1 != null && p.Item1.StringId == id) return true;
            return false;
        }

        /// <summary>Postfiks Village.GetWarehouseCapacity(): + 5 dob plonu owocow, miodu i jaj z listy BK (opis wyzej).</summary>
        public static void CapPostfix(Village __instance, ref int __result)
        {
            try
            {
                var v = __instance;
                if (v == null || v.VillageState != Village.VillageStates.Normal || v.TradeBound == null || v.VillageType == null) return;
                var s = Settings.Current;
                if (s == null || MaterialLaw.ProdModels <= 0) return;
                float lvl = (v.GetHearthLevel() + 1) * 0.5f;
                float x = 0f;
                if (s.VillageFruitFromBk && MineralOnce.Wired)
                {
                    string ty = v.VillageType.StringId;
                    foreach (var id in FruitIds)
                    {
                        float w = WeightFor(ty, id);
                        if (w > 0f && !InTypeListId(v.VillageType, id)) x += w * lvl;
                    }
                }
                if (s.VillageEggsFromBk)
                {
                    float w;
                    lock (Gate) { if (!_eggW.TryGetValue(v, out w)) w = 0f; }
                    if (w > 0f) { var egg = EggItem(); if (egg != null && !InTypeList(v.VillageType, egg)) x += w * lvl; }
                }
                if (!(x > 0f)) return;
                float f, u, g;
                if (VillageByPeople.TryFactor(v, out f, out u, out g)) x *= f;
                if (!(x > 0f) || float.IsNaN(x) || float.IsInfinity(x)) return;
                int add = (int)Math.Ceiling(5f * x - 1e-4f);
                if (add <= 0) return;
                __result += add;
                lock (Gate) _capLast[v] = add;
            }
            catch (Exception e) { lock (Gate) _capStumbles++; if (!_errLogged) { _errLogged = true; Log.Error("VillageFruit.CapPostfix", e); } }
        }

        /// <summary>Z SubModuleMain (latki przy starcie gry): postfiks Village.GetWarehouseCapacity (magazyn wsi z dodatkami listy BK).</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var cap = AccessTools.Method(typeof(Village), "GetWarehouseCapacity", Type.EmptyTypes);
                if (cap == null || cap.ReturnType != typeof(int)) { Log.Info("Magazyn wsi (owoce i jaja BK): BRAK Village.GetWarehouseCapacity() - magazyn jak w grze."); return; }
                h.Patch(cap, postfix: new HarmonyMethod(typeof(VillageFruit), nameof(CapPostfix)));
                _capWired = true;
                Log.Info("Magazyn wsi (owoce i jaja BK): postfiks GetWarehouseCapacity wpiety - W + 5 dob plonu owocow, miodu i jaj z listy BK (klucze VillageFruitFromBk, VillageEggsFromBk).");
            }
            catch (Exception e) { Log.Error("VillageFruit.ApplyAll", e); }
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (zaraz po SessionStart owocow): linia "Jaja wsi (N2-E1): start sesji" - liczba wsi rolniczych i oczekiwany plon.</summary>
        internal static void EggSessionStart()
        {
            lock (Gate) { _eggW.Clear(); _eggVill.Clear(); _eggLists = 0; _eggCalls = 0; _eggBase = 0; _capLast.Clear(); _capStumbles = 0; }
            var s = Settings.Current;
            const string Head = "Jaja wsi (N2-E1): start sesji | ";
            if (s == null || !s.VillageEggsFromBk)
            {
                Log.Info(Head + "klucz VillageEggsFromBk NIE | WYLACZONE (MCM) - jaja tylko z hodowli bydla BK i z drobiu w partiach BK, jak dotad | potkniecia " + _stumbles + ".");
                return;
            }
            int farms = 0, inType = 0, noMarket = 0;
            double basis = 0;
            var byType = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var egg = EggItem();
            try
            {
                foreach (var v in Village.All)
                {
                    try
                    {
                        if (v == null || v.VillageType == null || v.Settlement == null || !FarmType(v.VillageType)) continue;
                        if (egg != null && InTypeList(v.VillageType, egg)) { inType++; continue; }
                        farms++;
                        string t = v.VillageType.StringId ?? "?";
                        int k; byType.TryGetValue(t, out k); byType[t] = k + 1;
                        if (v.TradeBound == null) { noMarket++; continue; }
                        var c = v.Settlement.Culture;
                        float w = 2f * PoultryWeight(c != null ? c.StringId : null);
                        basis += w * (v.GetHearthLevel() + 1) * 0.5;
                    }
                    catch (Exception e) { Stumble(e); }
                }
            }
            catch (Exception e) { Stumble(e); }
            var parts = new List<string>();
            foreach (var kv in byType) parts.Add(kv.Key + " " + kv.Value);
            Log.Info(Head + "klucz VillageEggsFromBk TAK | CZYNNE | wsi rolniczych BK (klasa uprawy i wlokna typu wsi) " + farms + " [" + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "-") + "]"
                     + " | waga jaj = kura + ges z listy BK (kultura: aserai 0.15 + 0.15, sturgia i khuzait 0.45 + 0.45, empire i battania 0.75 + 0.75, reszta 1 + 1)"
                     + " | oczekiwany plon na dobe przed mnoznikami (waga x (poziom ognisk + 1) x 0.5): jaja " + basis.ToString("0", Inv) + " (dotad ze wsi tylko hodowla bydla BK)"
                     + " | jaja juz w typie wsi (pominiete) " + inType + " | bez targu (bez plonu) " + noMarket
                     + " | Egg w grze " + (egg != null ? "jest" : "BRAK - nic sie nie dzieje")
                     + " | lista produkcji BK " + (MineralOnce.Wired ? "jest (postfiks GetProductions dopisze jaja)" : "BEZ latki GetProductions - jaj nie bedzie")
                     + " | magazyn wsi + 5 dob jaj i owocow (GetWarehouseCapacity) " + (_capWired ? "wpiety" : "NIE wpiety")
                     + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick (po linii owocow): linia "Jaja wsi (N2-E1): dzien" (tylko log; sztuki - linia "Towary: ... Egg", wsie).</summary>
        internal static void EggDaily()
        {
            if (!EggsOn) return;
            int lists, calls, vill, st, capN, capSt; double b; long capSum = 0;
            lock (Gate)
            {
                lists = _eggLists; calls = _eggCalls; b = _eggBase; vill = _eggVill.Count; st = _stumbles;
                _eggLists = 0; _eggCalls = 0; _eggBase = 0; _eggVill.Clear();
                capN = _capLast.Count; capSt = _capStumbles;
                foreach (var kv in _capLast) capSum += kv.Value;
                _capLast.Clear();
            }
            Log.Info("Jaja wsi (N2-E1): dzien " + (int)CampaignTime.Now.ToDays + " | listy BK z jajami " + lists + " (wsi " + vill + ")"
                     + " | baza dopisana w " + calls + " pytaniach modelu (tick BK, okna wsi): jaja " + b.ToString("0", Inv)
                     + " (sztuki z ticku - linia \"Towary\", Egg, wsie)"
                     + " | magazyn wsi powiekszony o jaja i owoce: wsi " + capN + ", razem +" + capSum + " szt. (srednio +" + (capN > 0 ? ((double)capSum / capN).ToString("0.0", Inv) : "0") + "), potkniecia " + capSt
                     + " | potkniecia sesji " + st + ".");
        }
    }
}
