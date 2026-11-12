using System;
using System.Collections.Generic;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// 435-L1 (lancuch podazy, krok 1 - SAM POMIAR; klucz Supply Chain Log): dlaczego prace okregu gracza nie placa. Przy kazdym powrocie wozu okregu z pracami
    /// (OnCartReturn) zapamietany jest OSTATNI powod kazdego wiersza rodu gracza - wyplata albo miejsce, w ktorym rachunek wyplaty sie skonczyl. Zadnej zmiany rachunku:
    /// same wywolania LwDist / LwRow / LwClan w istniejacych galeziach. Pamiec sesji (bez zapisu; po wczytaniu "brak powrotu wozu od wczytania" do pierwszego wozu).
    /// Linia "LP prace gracza" - SupplySignal co 7 dob.
    /// </summary>
    internal static partial class DistrictWorks
    {
        private const int LwPaid = 0, LwNoPath = 1, LwNoE = 2, LwEaten = 3, LwNoWindow = 4, LwPoolSmall = 5, LwPartNoBonus = 6, LwNoUnits = 7, LwShareSmall = 8, LwWar = 9,
                          LwEmptyPurse = 10, LwNoTakings = 11, LwKinds = 12;
        private static readonly string[] LwName =
        {
            "wyplata", "okreg bez drogi produkcji dodatku (rampa KS albo produkcja wsi niewpieta)", "okreg bez dodatku w wycenie doby (E = 0 albo V = 0)",
            "caly dodatek zjedzony w okregu (na targ 0)", "nic nie weszlo w oknie wozu (licznik pana i kiesa wsi bez przyrostu)", "pula ponizej 1 zl (maly utarg wozu albo maly udzial dodatku)",
            "czesc bez dodatku (inne zajecie niz prace, dodatek 0 albo czesc poza wycena)", "wiersz bez czynnych jednostek (rampa, bezczynne, spalone)", "udzial rodu ponizej 1 zl",
            "wojna z krolestwem okregu", "licznik pana i kiesa wsi puste przy wyplacie", "woz wrocil bez utargu"
        };
        private static readonly Dictionary<Row, int[]> _lwLast = new Dictionary<Row, int[]>();   // wiersz gracza -> [powod, doba, powroty wozu od wczytania]

        private static void LwReset() { lock (Gate) _lwLast.Clear(); }

        private static void LwSet(Row r, int code, int today)
        {
            int[] a;
            if (!_lwLast.TryGetValue(r, out a)) { a = new int[3]; _lwLast[r] = a; }
            a[0] = code; a[1] = today; a[2]++;
        }

        /// <summary>Powod dla wszystkich wierszy gracza w okregu (rachunek skonczyl sie przed podzialem na wiersze). Sam licznik, nie rzuca.</summary>
        private static void LwDist(string dist, int code)
        {
            if (!SupplySignal.On || dist == null) return;
            try
            {
                string pid = PlayerClanId();
                if (pid == null) return;
                int today = Today();
                lock (Gate) foreach (var r in _rows) if (r.Dist == dist && r.Clan == pid) LwSet(r, code, today);
            }
            catch (Exception e) { Stumble("LwDist", e); }
        }

        /// <summary>Powod jednego wiersza (wolane pod Gate; wiersz innego rodu - nic). Sam licznik, nie rzuca.</summary>
        private static void LwRow(Row r, int code)
        {
            if (!SupplySignal.On || r == null) return;
            try
            {
                string pid = PlayerClanId();
                if (pid == null || r.Clan != pid) return;
                lock (Gate) LwSet(r, code, Today());
            }
            catch (Exception e) { Stumble("LwRow", e); }
        }

        /// <summary>Powod wierszy rodu `clan` z podzialu puli (tylko rod gracza). Sam licznik, nie rzuca.</summary>
        private static void LwClan(Dictionary<Row, double> er, string clan, int code)
        {
            if (!SupplySignal.On || er == null || clan == null) return;
            try
            {
                string pid = PlayerClanId();
                if (pid == null || clan != pid) return;
                int today = Today();
                lock (Gate) foreach (var kv in er) if (kv.Key != null && kv.Key.Clan == pid) LwSet(kv.Key, code, today);
            }
            catch (Exception e) { Stumble("LwClan", e); }
        }

        /// <summary>SupplySignal (co 7 dob): tresc linii "LP prace gracza" - wiersze rodu gracza, wyplata 28 dob i ostatni powod; null - gracz bez prac. Sam odczyt, nie rzuca.</summary>
        internal static string Lp1Text(int today)
        {
            try
            {
                string pid = PlayerClanId();
                if (pid == null) return null;
                var sb = new StringBuilder(900);
                lock (Gate)
                {
                    var live = new HashSet<Row>(_rows);
                    var dead = new List<Row>();
                    foreach (var k in _lwLast.Keys) if (!live.Contains(k)) dead.Add(k);
                    foreach (var k in dead) _lwLast.Remove(k);
                    int n = 0, paidN = 0, noCart = 0;
                    double units = 0, paid28 = 0;
                    var by = new int[LwKinds];
                    var list = new List<string>();
                    foreach (var r in _rows)
                    {
                        if (r.Clan != pid) continue;
                        n++;
                        double eff = r.Eff(today), p = r.PaidSince(today - 27);
                        units += eff; paid28 += p;
                        int[] a; _lwLast.TryGetValue(r, out a);
                        string why;
                        if (p >= 1) { paidN++; why = "placi"; }
                        else if (a == null) { noCart++; why = "brak powrotu wozu okregu od wczytania"; }
                        else { by[a[0] >= 0 && a[0] < LwKinds ? a[0] : LwPartNoBonus]++; why = LwName[a[0] >= 0 && a[0] < LwKinds ? a[0] : LwPartNoBonus] + " (doba " + a[1] + ", powrotow " + a[2] + ")"; }
                        if (list.Count < 20)
                            list.Add(r.Dist + "/" + r.Part + " " + (r.Craft >= 0 && r.Craft < KsTab.Crafts.Length ? KsTab.Crafts[r.Craft] : "?") + ": jednostek czynnych " + eff.ToString("0.##", Inv)
                                     + " (trzymane " + r.Held().ToString("0.##", Inv) + "), wyplata 28 dob " + Math.Round(p).ToString("0", Inv) + " - " + why);
                    }
                    if (n == 0) return null;
                    sb.Append("prac gracza ").Append(n).Append(" (jednostek czynnych ").Append(units.ToString("0.##", Inv)).Append("), z wyplata w 28 dobach ").Append(paidN)
                      .Append(" (razem ").Append(Math.Round(paid28).ToString("0", Inv)).Append(" zl), bez wyplaty ").Append(n - paidN).Append(": brak powrotu wozu okregu od wczytania ").Append(noCart);
                    for (int c = 0; c < LwKinds; c++) if (by[c] > 0) sb.Append(", ").Append(c == LwPaid ? "wyplata ponizej 1 zl w 28 dobach albo starsza" : LwName[c]).Append(' ').Append(by[c]);
                    int sum = paidN + noCart; for (int c = 0; c < LwKinds; c++) sum += by[c];
                    sb.Append(" | tozsamosc licznikow (nie kontrola ksiag): wiersze - (placace + powody) = ").Append(n - sum).Append(" | wiersze: ").Append(string.Join("; ", list.ToArray()));
                    if (n > list.Count) sb.Append("; ... (").Append(n - list.Count).Append(" dalszych)");
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Lp1Text", e); return null; }
        }
    }
}
