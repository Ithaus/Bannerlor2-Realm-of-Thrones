using System;
using System.Collections.Generic;
using System.Globalization;
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
    }
}
