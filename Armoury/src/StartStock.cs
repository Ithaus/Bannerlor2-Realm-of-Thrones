using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZAPAS STARTOWY W LADUNKACH (docs/EKONOMIA-FUNDAMENT-2026-10-05.md, ustalenie C1 i krok K3).
    /// Ticki startowe nowej kampanii (0-5 dob produkcji kazdej wsi, 5 przebiegow dla osad nadrzednych, rozdanie towaru miastom:
    /// vanilla VillageGoodProductionCampaignBehavior i latki BK na nim) biegna w OnNewGameCreated, a HistoricalPrices.Apply
    /// (ruda i drewno z 10 kg na ladunek 100 kg) dopiero w OnSessionLaunched. BulkScale zwraca przedtem 1, wiec swiat dostawal
    /// sztuki liczone po 10 kg, ktore chwile pozniej wazyly po 100 kg - dziesiec razy wiecej rudy i drewna niz zamierzone
    /// (log 15:22: 6974 i 23997 ladunkow). Magazyn wsi liczy SZTUKI, wiec zapchany wstrzymywal cala produkcje wsi, takze zboze.
    ///
    /// Raz na kampanie, zaraz po HistoricalPrices.Apply: liczbe sztuk iron i hardwood w rosterach osad (miasta, zamki, wsie,
    /// kryjowki) i partii (wiesniacy, karawany, lordowie, bandy, reszta) dzielimy przez przelicznik ladunku (BulkScale = waga
    /// teraz / waga z gry). To zmiana JEDNOSTKI, nie ilosci - kilogramow jest tyle, ile rozdaly ticki startowe.
    /// Zaokraglenie sprawiedliwe (metoda najwiekszych reszt): stos dostaje czesc calkowita, a ladunki zlozone z ulamkow ida do
    /// stosow o najwiekszej reszcie (remisy losowo); suma swiata = zaokraglone (przed / przelicznik) - nic nie powstaje i nie znika.
    ///
    /// Kiedy (flaga w sejwie, klucz arm_startstock - jak ColdStart):
    ///  - sesja, ktora ZALOZYLA kampanie, albo wczytana kampania bez flagi nie starsza niz MaxAgeDays (zapis z pierwszej doby
    ///    sprzed tej zmiany - jej zapas to nadal ticki startowe): przeliczamy i zapisujemy flage;
    ///  - wczytana kampania bez flagi starsza niz MaxAgeDays albo z wiekiem ujemnym (data startu z innego kalendarza): flaga
    ///    "done" BEZ przeliczenia - jej zapas dawno przeszedl przez produkcje, zuzycie i handel liczone juz w ladunkach;
    ///  - poczatek kampanii przy wylaczniku (MCM) albo przeliczniku 1 (ceny historyczne wylaczone): nic nie robimy i flagi nie
    ///    zapisujemy - wlaczenie w pierwszej dobie jeszcze zadziala.
    /// Nie ruszamy: partii gracza, skrytek osad (Settlement.Stash - BK odklada tam materialy budow i urobek kopaln dopiero
    /// w dobowych tickach, czyli juz w ladunkach) ani magazynow warsztatow gracza - przy zalozeniu kampanii sa puste.
    /// </summary>
    internal static class StartStock
    {
        private const double MaxAgeDays = 1.0;
        private static readonly string[] Ids = { "iron", "hardwood" };
        private static readonly string[] Names = { "ruda", "drewno" };
        private static readonly string[] KindNames = { "miasta", "zamki", "wsie", "inne osady", "wiesniacy", "karawany", "lordowie", "bandy", "inne partie" };

        private static bool _done;
        internal static void Reset() { _done = false; }
        internal static string Export() { return _done ? "done" : ""; }
        internal static void Import(string s) { _done = s == "done"; }

        private sealed class Stack
        {
            public ItemRoster Roster;
            public EquipmentElement El;
            public int Amount, New;
            public double Frac;
            public float Tie;
        }

        /// <summary>Wolane z OnSessionLaunched po HistoricalPrices.Apply (przelicznik juz obowiazuje) i przed ColdStart.Run.</summary>
        internal static void Run()
        {
            if (_done) return;
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;

                // 1. czy to poczatek kampanii
                bool fresh = false;
                try { fresh = Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.NewCampaign; } catch { }
                double age = double.NaN;
                try { age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
                catch (Exception e) { Log.Error("StartStock.Age", e); }
                if (!fresh)
                {
                    if (double.IsNaN(age))
                    {
                        Log.Info("StartStock: wieku wczytanej kampanii nie da sie odczytac - zapas rudy i drewna bez przeliczenia (flaga nie zapisana).");
                        return;
                    }
                    if (age < -0.01 || age > MaxAgeDays)
                    {
                        _done = true;
                        Log.Info("StartStock: wczytana kampania ma " + age.ToString("0.0", CultureInfo.InvariantCulture) + " dni (granica " + MaxAgeDays.ToString("0.#", CultureInfo.InvariantCulture)
                                 + ") - zapas rudy i drewna BEZ przeliczenia na ladunki (zapis sprzed tej zmiany albo kampania zalozona bez przeliczenia; flaga zapisana).");
                        return;
                    }
                }
                if (!s.StartStockInLoads)
                {
                    Log.Info("StartStock: przeliczenie zapasu startowego na ladunki WYLACZONE (MCM) - ruda i drewno zostaja w sztukach z tickow startowych; flaga nie zapisana (wlaczenie w pierwszej dobie kampanii jeszcze zadziala).");
                    return;
                }

                // 2. przelicznik: to, co HistoricalPrices faktycznie zrobil z waga w tej sesji
                var items = new ItemObject[Ids.Length];
                var factors = new float[Ids.Length];
                bool any = false;
                for (int k = 0; k < Ids.Length; k++)
                {
                    try { items[k] = MBObjectManager.Instance.GetObject<ItemObject>(Ids[k]); } catch { }
                    factors[k] = items[k] != null ? HistoricalPrices.BulkScale(items[k]) : 1f;
                    if (factors[k] > 1.01f) any = true;
                }
                if (!any)
                {
                    Log.Info("StartStock: przelicznik ladunku 1 (ceny historyczne wylaczone albo HistBulkUnitFactor = 1) - zapas startowy bez zmian, flaga nie zapisana.");
                    return;
                }

                // 3. flaga PRZED robota: przerwanej roboty nie wolno powtorzyc (drugie dzielenie zabraloby 99% zapasu)
                _done = true;
                Log.Info("StartStock: " + (fresh ? "nowa kampania" : "wczytana kampania z pierwszej doby (" + age.ToString("0.00", CultureInfo.InvariantCulture) + " dnia, bez flagi)")
                         + " - zapas startowy rudy i drewna przeliczany na ladunki: ticki startowe rozdaly go w sztukach z gry, zanim ladunek zaczal obowiazywac.");
                int stumbles = 0, changed = 0;
                for (int k = 0; k < Ids.Length; k++)
                {
                    if (items[k] == null || factors[k] <= 1.01f) continue;
                    try { changed += One(items[k], Names[k], factors[k], ref stumbles); }
                    catch (Exception e) { stumbles++; Log.Error("StartStock." + Ids[k], e); }
                }

                // 4. targi miast: zapas i jego wartosc w danych rynku (TownMarketData) licza sie przyrostowo, ze sztuk i z Value w chwili
                // zmiany - po zmianie cen i jednostek przeliczamy je od nowa z polek. To samo robi gra chwile wczesniej w tym samym
                // zdarzeniu (TradeCampaignBehavior.OnSessionLaunched, jeszcze na starych cenach) i co dobe w DailyTickTown; jak gra -
                // tylko miasta (zamki dostaja sam przyrost z AddToCounts, ich dane rynku zostaja jak dotad)
                int markets = 0;
                if (changed > 0)
                    foreach (var st in Settlement.All)
                    {
                        try { if (st != null && st.IsTown && st.Town != null) { st.Town.MarketData.UpdateStores(); markets++; } }
                        catch (Exception e) { if (stumbles++ < 3) Log.Error("StartStock.Market", e); }
                    }
                Log.Info("StartStock: gotowe - dane rynku przeliczone z polek w " + markets + " miastach; potkniecia " + stumbles
                         + ". Partia gracza, skrytki osad i magazyny warsztatow gracza nie byly ruszane. Flaga zapisze sie w sejwie (arm_startstock).");
            }
            catch (Exception e) { Log.Error("StartStock", e); }
        }

        /// <summary>Jeden przedmiot: spis stosow, podzial z najwiekszymi resztami, zapis, kontrolny spis po. Zwraca liczbe zmienionych stosow.</summary>
        private static int One(ItemObject it, string name, float f, ref int stumbles)
        {
            var stacks = new List<Stack>();
            var before = new long[KindNames.Length];
            int rosters, player;
            Scan(it, stacks, before, out rosters, out player, ref stumbles);
            int changed = Divide(stacks, f, ref stumbles);

            // kontrolny spis PO - do logu idzie to, co faktycznie lezy w rosterach
            var after = new long[KindNames.Length];
            int rostersAfter, playerAfter;
            Scan(it, null, after, out rostersAfter, out playerAfter, ref stumbles);
            long sumBefore = 0, sumAfter = 0;
            var parts = new List<string>();
            for (int i = 0; i < KindNames.Length; i++)
            {
                sumBefore += before[i]; sumAfter += after[i];
                if (before[i] > 0 || after[i] > 0) parts.Add(KindNames[i] + " " + before[i] + " -> " + after[i]);
            }
            float kg = Math.Max(0.1f, it.Weight);
            string ledger = OreLedger.TakeStartTicks(it);
            Log.Info("StartStock: " + name + " - " + sumBefore + " szt. po " + (kg / f).ToString("0.#", CultureInfo.InvariantCulture) + " kg -> " + sumAfter + " ladunkow po "
                     + kg.ToString("0.#", CultureInfo.InvariantCulture) + " kg (przelicznik " + f.ToString("0.##", CultureInfo.InvariantCulture) + ", cel " + Math.Round(sumBefore / (double)f, MidpointRounding.AwayFromZero)
                     + "; " + (sumBefore * (kg / f) / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " t -> " + (sumAfter * kg / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " t) w "
                     + rosters + " rosterach (stosow " + stacks.Count + ", zmienionych " + changed + ", rosterow z towarem po " + rostersAfter + ") [" + string.Join(", ", parts.ToArray()) + "]"
                     + (player > 0 ? "; partia gracza pominieta (" + player + " szt.)" : "")
                     + (ledger.Length > 0 ? "; ticki startowe w ksiedze (sztuki z gry, wyzerowane - pierwsza linia ksiegi liczy sama pierwsza dobe): " + ledger : "") + ".");
            return changed;
        }

        /// <summary>Dzieli stosy przez przelicznik metoda najwiekszych reszt i zapisuje wynik w rosterach. Kazdy stos dostaje czesc
        /// calkowita; ladunki zlozone z ulamkow (suma reszt zaokraglona) ida po jednym do stosow o najwiekszej reszcie, remisy
        /// losowo. Suma po = zaokraglone (suma przed / przelicznik). Wyjatek jednego stosu liczymy i idziemy dalej.</summary>
        private static int Divide(List<Stack> stacks, float f, ref int stumbles)
        {
            if (stacks.Count == 0 || f <= 1.01f) return 0;
            int whole = (int)Math.Round(f);
            bool integral = whole >= 2 && Math.Abs(f - whole) < 0.001f;
            long remSum = 0; double fracSum = 0.0;
            foreach (var s in stacks)
            {
                if (integral) { s.New = s.Amount / whole; int rem = s.Amount % whole; remSum += rem; s.Frac = rem / (double)whole; }
                else { double q = s.Amount / (double)f; s.New = (int)Math.Floor(q + 1e-9); s.Frac = Math.Max(0.0, q - s.New); fracSum += s.Frac; }
                s.Tie = MBRandom.RandomFloat;
            }
            int extra = integral ? (int)((2L * remSum + whole) / (2L * whole)) : (int)Math.Round(fracSum, MidpointRounding.AwayFromZero);
            if (extra > 0)
            {
                var order = new List<Stack>(stacks);
                order.Sort(delegate (Stack a, Stack b) { int c = b.Frac.CompareTo(a.Frac); return c != 0 ? c : a.Tie.CompareTo(b.Tie); });
                for (int i = 0; i < extra && i < order.Count; i++) if (order[i].Frac > 0.0) order[i].New++;
            }
            int changed = 0;
            foreach (var s in stacks)
            {
                int delta = s.New - s.Amount;
                if (delta == 0) continue;
                try { s.Roster.AddToCounts(s.El, delta); changed++; }      // AddToCounts: targ miasta i cache rostera dostaja zdarzenie
                catch (Exception e) { if (stumbles++ < 3) Log.Error("StartStock.Stack", e); }
            }
            return changed;
        }

        /// <summary>Stosy przedmiotu w rosterach swiata, bez partii gracza. Wyjatek jednego rostera nie zatrzymuje spisu.
        /// Kazdy roster wchodzi do spisu RAZ (seen): dzielenie jest jednorazowe i nieodwracalne, wiec roster widziany dwa razy
        /// (ta sama partia dwa razy na liscie, partia z rosterem osady) nie moze dostac dwoch stosow do podzialu. Proba recenzenta:
        /// bez tego gra odrzucala drugie odjecie wyjatkiem (ilosc ponizej zera), ale zdarzenie rostera szlo dwa razy, a "cel" w logu
        /// liczyl ten zapas podwojnie.</summary>
        private static void Scan(ItemObject it, List<Stack> stacks, long[] byKind, out int rosters, out int player, ref int stumbles)
        {
            rosters = 0; player = 0;
            var seen = new HashSet<ItemRoster>();
            try
            {
                // roster gracza idzie do "seen" PIERWSZY - zaden inny wpis na listach swiata go juz nie ruszy
                var main = MobileParty.MainParty;
                if (main != null && main.ItemRoster != null && seen.Add(main.ItemRoster)) player = Collect(main.ItemRoster, it, 0, null, null);
            }
            catch (Exception e) { if (stumbles++ < 3) Log.Error("StartStock.Scan", e); }
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    var r = st.ItemRoster;
                    if (r == null || !seen.Add(r)) continue;
                    int kind = st.IsTown ? 0 : st.IsCastle ? 1 : st.IsVillage ? 2 : 3;
                    if (Collect(r, it, kind, stacks, byKind) > 0) rosters++;
                }
                catch (Exception e) { if (stumbles++ < 3) Log.Error("StartStock.Scan", e); }
            }
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || mp.IsMainParty) continue;
                    var r = mp.ItemRoster;
                    if (r == null || !seen.Add(r)) continue;
                    int kind = mp.IsVillager ? 4 : mp.IsCaravan ? 5 : mp.IsLordParty ? 6 : mp.IsBandit ? 7 : 8;
                    if (Collect(r, it, kind, stacks, byKind) > 0) rosters++;
                }
                catch (Exception e) { if (stumbles++ < 3) Log.Error("StartStock.Scan", e); }
            }
        }

        private static int Collect(ItemRoster r, ItemObject it, int kind, List<Stack> stacks, long[] byKind)
        {
            int sum = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.EquipmentElement.Item != it || el.Amount <= 0) continue;
                sum += el.Amount;
                if (byKind != null) byKind[kind] += el.Amount;
                if (stacks != null) stacks.Add(new Stack { Roster = r, El = el.EquipmentElement, Amount = el.Amount });
            }
            return sum;
        }
    }
}
