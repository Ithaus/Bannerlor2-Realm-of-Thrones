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
    /// WIOSKI POBOCZNE WEDLUG RODZAJU (E6/W2 = paczka 210, PLAN 6.18; projekt etapu 6 v2 rozdz. 3 "E6/W2", R7, D6-5, KB W2-ryby-z-zera).
    /// Kolumna "model" pliku arm_map_villages.tsv (fishing / mill / windmill / granary / farm / village) byla dotad tylko obrazkiem. Odtad okreg z wioskami rybackimi
    /// wysyla na targ ryby zamiast czesci zboza (tyle samo sztuk - zboze i ryby waza po 10 kg, oba to jedzenie); mlyn, wiatrak, spichlerz, farma - zboze jak typ wsi gry.
    ///  - u = udzial ludzi W DOMU wiosek rybackich w ludziach w domu okregu (VillageBook.FishersShare - te same osady co ksiega wiosek) x 0.5 [projekt];
    ///  - lista produkcji wsi BK (MineralOnce.Postfix, ostatni krok listy): przy u &gt; 0 i zbozu na liscie ryby stoja ZA zbozem (dopisane z waga 0 albo
    ///    przeniesione z wlasna waga, gdy typ wsi ma juz ryby) - wagi bez zmian (model BK dzieli nimi sile robocza); u zapamietane razem z lista (jak MineralOnce._dup);
    ///  - model produkcji (MaterialLaw.ProdPostfix, OSTATNI krok - po mnoznikach wydobycia, powtorzen BK, wielbladow, spustoszenia E4/5 i rak E4/7):
    ///    zboze -&gt; czynnik x (1 - u) liczony od wyniku koncowego, zdjete T = wynik przed - wynik po zapamietane (wies, doba);
    ///    ryby -&gt; dopisek bazy Add(T / (1 + suma czynnikow ryb)): wynik rosnie dokladnie o T (ExplainedNumber mnozy baze przez 1 + suma, po nas nikt juz nie mnozy).
    ///    Ryb NIE da sie zrobic czynnikiem: wies, ktorej typ nie ma ryb, dostaje z modelu gry 0 (G DefaultVillageProductionCalculatorModel :22-27), a czynnik x 0 = 0.
    ///    Tick BK (TickGoodProductionPatch) idzie po liscie w kolejnosci - zboze przed rybami, wiec pamiec zboza jest gotowa. Brak pamieci (inny wolajacy, np. okno
    ///    BK przed tickiem) - ryby bez dopisku, licznik "wsi bez pamieci zboza".
    /// Zamknieta ekonomia: zboze zdjete = ryby dodane w kazdej wsi i dobie (kontrola w linii). Wies bez wioski rybackiej, bez zboza na liscie, przy ksiedze wiosek
    /// nieczynnej albo bez listy BK - nic sie nie zmienia (u nie jest zapamietane, model zboza bez kroku). Spichlerz E4/6 i zywnosc warowni E4/11 - bez zmian.
    /// Wyjatki per wies liczone (potkniecia, pierwszy blad do logu) - bez globalnych wylacznikow (CLAUDE.md 7). Wylacznik: HamletKindsInDistrict.
    /// </summary>
    internal static class HamletKinds
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float FishersHalf = 0.5f;          // u = udzial ludzi wiosek rybackich x 0.5 [projekt]: polowa rak wioski rybackiej lowi, reszta orze
        private const float MinU = 0.0005f;              // ponizej - okreg bez rybakow (szum zaokraglen ksiegi)
        private static readonly object Gate = new object();
        private static readonly TextObject _txtGrain = new TextObject("{=!}Armoury: fishing hamlets of the district land fish in place of part of the grain");
        private static readonly TextObject _txtFish = new TextObject("{=!}Armoury: fish landed by the fishing hamlets of the district");

        private sealed class VillageRefEq : IEqualityComparer<Village>
        {
            public bool Equals(Village a, Village b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Village o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        // pamiec doby wsi: ostatnie wywolanie modelu dla zboza i ryb tego dnia (tick BK, magazyn wsi i okna licza to samo - nadpisujemy, nie sumujemy)
        private sealed class Rec { public int Day = int.MinValue; public float U, Taken, Landed; public bool Grain, Fish, NoMem; }

        // wies -> u z OSTATNIEGO zlozenia jej listy produkcji BK (ryby za zbozem); brak wpisu = lista bez kroku, zboze bez zmian
        private static readonly Dictionary<Village, float> _u = new Dictionary<Village, float>(new VillageRefEq());
        private static readonly Dictionary<Village, Rec> _rec = new Dictionary<Village, Rec>(new VillageRefEq());
        private static ItemObject _fish, _grain;
        private static bool _itemsTried;
        private static int _added, _moved, _stumbles, _stumblesAll;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        /// <summary>Klucz i wszystkie zrodla czynne: ksiega wiosek (rodzaj i ludzie wiosek), lista produkcji BK (ryby na liscie) i MaterialLaw w modelach
        /// produkcji (krok zboza i ryb) - bez ktoregokolwiek zboze nie moze zniknac (nic w nicosc).</summary>
        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.HamletKindsInDistrict && VillageBook.Active && MineralOnce.Wired && MaterialLaw.ProdModels > 0;
            }
        }

        internal static void Reset()
        {
            lock (Gate) { _u.Clear(); _rec.Clear(); _added = _moved = 0; _stumbles = _stumblesAll = 0; _errSites.Clear(); }
            _fish = _grain = null; _itemsTried = false;
        }

        private static void Stumble(string where, Exception e)
        {
            bool first;
            lock (Gate) { _stumbles++; _stumblesAll++; first = _errSites.Add(where); }
            if (first) Log.Error(where, e);
        }

        private static bool Items()
        {
            if (!_itemsTried)
            {
                _itemsTried = true;
                try
                {
                    var om = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                    _fish = om != null ? om.GetObject<ItemObject>("fish") : null;
                    _grain = om != null ? om.GetObject<ItemObject>("grain") : null;
                }
                catch { _fish = _grain = null; }
            }
            return _fish != null && _grain != null;
        }

        /// <summary>Lista wsi wraca z BK bez naszego kroku (potkniecie, klucz wylaczony) - zboze tej wsi bez zmian.</summary>
        internal static void Forget(Village v)
        {
            if (v == null) return;
            lock (Gate) _u.Remove(v);
        }

        /// <summary>
        /// MineralOnce.Postfix, ostatni krok listy produkcji wsi BK (po filtrze klimatu, wielbladach i powtorzeniach mineralow): przy u &gt; 0 i zbozu na liscie ryby
        /// za zbozem (dopisane z waga 0 albo przeniesione), wagi bez zmian. Inaczej - ta sama lista i u zapomniane.
        /// </summary>
        internal static List<(ItemObject, float)> ListStep(Village v, List<(ItemObject, float)> list)
        {
            if (v == null || list == null) return list;
            try
            {
                if (!On || !Items()) { Forget(v); return list; }
                int n = list.Count, gi = -1, fi = -1, fc = 0, gc = 0;
                for (int i = 0; i < n; i++)
                {
                    var it = list[i].Item1;
                    if (ReferenceEquals(it, _grain)) { if (gi < 0) gi = i; gc++; }
                    else if (ReferenceEquals(it, _fish)) { if (fi < 0) fi = i; fc++; }
                }
                // bez zboza nie ma czego zamienic; powtorzone zboze albo ryby (czesc mineralow wylaczona) - nie ruszamy (mnoznik powtorzen liczy sie osobno)
                if (gi < 0 || gc > 1 || fc > 1) { Forget(v); return list; }
                float u = (float)(VillageBook.FishersShare(v) * FishersHalf);
                if (float.IsNaN(u) || !(u >= MinU)) { Forget(v); return list; }
                if (u > FishersHalf) u = FishersHalf;
                // WAGI BEZ ZMIAN: BKVillageProductionModel dzieli sile robocza wsi wedlug wag listy (BK BKVillageProductionModel.cs:53-65) - przeniesienie wagi
                // ze zboza na ryby zrobiloby zamiane drugi raz, gdyby ten model byl czynny. Dopisane ryby maja wage 0 (model BK da im 0, model gry tez - typ bez ryb),
                // cala zamiana idzie krokiem Step; HandleExcessFood BK (wagi zywnosci) - bez zmian
                if (fi > gi) { lock (Gate) _u[v] = u; return list; }          // ryby typu wsi juz za zbozem - ta sama lista
                var res = new List<(ItemObject, float)>(n + 1);
                for (int i = 0; i < n; i++) if (i != fi) res.Add(list[i]);    // ryby typu wsi przed zbozem przechodza za nie (tick: pamiec zboza przed rybami)
                res.Add(fi >= 0 ? list[fi] : (_fish, 0f));
                lock (Gate) { _u[v] = u; if (fi >= 0) _moved++; else _added++; }
                return res;
            }
            catch (Exception e) { Forget(v); Stumble("HamletKinds.ListStep", e); return list; }
        }

        private static Rec RecFor(Village v, int day)
        {
            Rec r;
            if (!_rec.TryGetValue(v, out r)) { r = new Rec(); _rec[v] = r; }
            if (r.Day != day) { r.Day = day; r.U = 0f; r.Taken = 0f; r.Landed = 0f; r.Grain = r.Fish = r.NoMem = false; }
            return r;
        }

        /// <summary>
        /// MaterialLaw.ProdPostfix, OSTATNI krok (najbardziej zewnetrzny model - _depth 1): zboze x (1 - u) od wyniku koncowego i pamiec zdjetego (wies, doba);
        /// ryby + zdjete zboze tej wsi z tej doby (dopisek bazy). Nie czynnik.
        /// </summary>
        internal static void Step(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (v == null || item == null || !_itemsTried) return;
            bool grain = ReferenceEquals(item, _grain), fish = !grain && ReferenceEquals(item, _fish);
            if (!grain && !fish) return;
            try
            {
                if (!On) return;
                float u;
                lock (Gate) { if (!_u.TryGetValue(v, out u)) return; }
                int today = (int)CampaignTime.Now.ToDays;
                if (grain)
                {
                    float before = r.ResultNumber, sum = 1f + r.SumOfFactors, taken = 0f;
                    if (before > 0f && sum > 0f)
                    {
                        r.AddFactor(-u * sum, _txtGrain);                    // (1 + suma) x (1 - u): wynik koncowy x (1 - u)
                        taken = Math.Max(0f, before - r.ResultNumber);
                    }
                    lock (Gate) { var rec = RecFor(v, today); rec.U = u; rec.Taken = taken; rec.Grain = true; }
                    return;
                }
                float t;
                lock (Gate)
                {
                    var rec = RecFor(v, today);
                    rec.Fish = true;
                    if (!rec.Grain) { rec.NoMem = true; return; }          // ryby przed zbozem (okno BK) - bez dopisku; tick zawsze ma zboze pierwsze
                    rec.NoMem = false;                                      // licznik mowi o OSTATNIM wywolaniu ryb tej doby (tick po oknie BK - pamiec jest)
                    t = rec.Taken;
                }
                float landed = 0f;
                if (t > 0f)
                {
                    float old = Math.Max(0f, r.ResultNumber), fsum = 1f + r.SumOfFactors;
                    if (fsum > 0.001f) r.Add(t / fsum, _txtFish);
                    else
                    {
                        // czynniki ryb zjadaja baze (np. Dluga Noc na typie z rybami): zerujemy je i stawiamy baze tak, by wynik = dotychczasowy (nie mniej niz 0) + T
                        r.AddFactor(-r.SumOfFactors, _txtFish);
                        r.Add(old + t - r.BaseNumber, _txtFish);
                    }
                    landed = r.ResultNumber - old;
                }
                lock (Gate) { var rec = RecFor(v, today); rec.Landed = landed; }
            }
            catch (Exception e) { Stumble("HamletKinds.Step", e); }
        }

        /// <summary>ArmouryBehavior.OnDailyTick: linia "Wioski wedlug rodzaju (210)" za dobe wczorajsza (tick wsi rozlozony po godzinach doby). Tylko log.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            int day = (int)CampaignTime.Now.ToDays - 1;
            string state;
            if (!s.HamletKindsInDistrict) state = "WYLACZONE w ustawieniach (Hamlet Kinds In District) - zboze okregow jak dotad";
            else if (!VillageBook.Active) state = "NIECZYNNE - ksiega wiosek nieczynna (VillageBookEnabled albo okregi nierozwiazane)";
            else if (!MineralOnce.Wired) state = "NIECZYNNE - brak listy produkcji BK (postfiks GetProductions nie wpiety)";
            else if (MaterialLaw.ProdModels <= 0) state = "NIECZYNNE - MaterialLaw poza modelami produkcji wsi";
            else if (_itemsTried && (_fish == null || _grain == null)) state = "NIECZYNNE - gra nie zna przedmiotu fish albo grain";
            else state = "CZYNNE";
            int n = 0, c = 0, fishOnly = 0, listed;
            double a = 0.0, b = 0.0, su = 0.0;
            int added, moved, st, stAll;
            lock (Gate)
            {
                foreach (var r in _rec.Values)
                {
                    if (r.Day != day) continue;
                    if (r.Grain) { n++; a += r.Taken; su += r.U; }
                    if (r.Fish) { b += r.Landed; if (r.NoMem) c++; if (!r.Grain) fishOnly++; }
                }
                listed = _u.Count; added = _added; moved = _moved; st = _stumbles; stAll = _stumblesAll;
                _added = _moved = 0; _stumbles = 0;
            }
            Log.Info("Wioski wedlug rodzaju (210): dzien " + day + " | " + state
                     + " | okregow z rybakami " + n + " (u srednio " + (n > 0 ? (100.0 * su / n).ToString("0.0", Inv) : "0") + "%, wsi z rybami za zbozem na liscie BK " + listed + ")"
                     + ", zboza -" + a.ToString("0.0", Inv) + ", ryb +" + b.ToString("0.0", Inv) + ", kontrola a - b = " + (a - b).ToString("0.0", Inv)
                     + "; wsi bez pamieci zboza " + c + " (w tym bez zboza w dobie " + fishOnly + ")"
                     + " | listy BK: ryby dopisane " + added + ", przeniesione za zboze " + moved + " wywolan | potkniecia dzis " + st + ", od wczytania " + stAll);
        }
    }
}
