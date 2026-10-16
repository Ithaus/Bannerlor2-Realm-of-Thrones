using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// P1b (OBIEG KIESY LUDU, docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md rozdz. 3.1 szczebel 4, 3.2, 7, 8 wiersz P1b; za PROJEKT-POZYCZKI R3;
    /// decyzja Jeffa 13.10 "P1b kara lojalnosci TAK - osobny krok po pomiarze") - KARA LOJALNOSCI ZA DANINE Z OSZCZEDNOSCI. Klucz FolkWarAidLoyalty
    /// (wylaczony = bez kary i bez progu - stan sprzed P1b co do punktu).
    ///  - KARA: miasto albo zamek, ktorego lud oddal o swicie danine P1, traci tej doby lojalnosc do pana: FolkWarAidLoyaltyCost (1.0) x czesc CALEJ kiesy ludu
    ///    zabrana w daninie / FolkWarAidShare (1/40), najwyzej koszt (DECYZJA pelne uprawnienia: proporcjonalnie do tego, jak mocno lud zubozal - miasto, ktore
    ///    oddalo kilka monet z wielkiej kiesy, nie burzy sie jak miasto, ktoremu wzieto czterdziesta czesc wszystkiego; projekt liczyl sama stawke s / 3%,
    ///    co karalo pelna kara takze za grosze). Dryf gry i BK ciagnie lojalnosc do 50 o 0.1 x odleglosc na dobe, wiec stala kara x daje trwale -10x (do -10).
    ///  - PODLOGA WSPOLNA Z W7 (stawka wojenna pana zdejmuje lojalnosc wprost - KingdomTreasury.LordLevy.cs): kara P1b tej doby najwyzej
    ///    0.1 x (lojalnosc - FolkWarAidLoyaltyFloor) minus lojalnosc zdjeta przez W7 tej osadzie tej doby, najmniej 0. Przy podlodze (60) kara P1b znika, wiec
    ///    danina sama nie sprowadzi lojalnosci ponizej 60. Recenzja (9): przy kluczu ta sama granica obejmuje koszt W7 (W7Cost - W7 zdejmuje najwyzej
    ///    0.1 x (lojalnosc - podloga)), wiec razem rownowaga osady o naturalnej lojalnosci L wynosi co najmniej (L + 60) / 2, a osada z L < 60 nie traci nic
    ///    (dotad sam W7 dawal L - 5); korupcja podatku gry zaczyna sie ponizej 50. Klucz wylaczony - W7 jak dotad.
    ///  - PROG POBORU: osada z lojalnoscia <= FolkWarAidMinLoyalty (30) nie placi daniny (KingdomTreasury.WaRate - powod WaLowLoy), wiec i kary nie ma.
    ///  - GDZIE: postfiks na CalculateLoyaltyChange(Town, bool) CZYNNEGO modelu lojalnosci (u Jeffa BannerKings BKLoyaltyModel), zakladany dopiero w kampanii
    ///    (EnsureHooks z OnSessionLaunched - BKLoyaltyModel ma pola statyczne z GameTexts, CLAUDE.md rozdz. 7); pozycja "War aid from the people's savings"
    ///    w dymku lojalnosci. Gra dodaje wynik modelu do lojalnosci raz na dobe w Town.DailyTick (ticker osad rozlozony w ciagu doby); migawka doby (Daily -
    ///    z ArmouryBehavior zaraz po kroku W7) trzyma kare nominalna i koszt W7 kazdej osady do nastepnego bloku doby; kara liczona przy kazdym wywolaniu od
    ///    biezacej lojalnosci (podloga). Migawka to nowy slownik co dobe (odczyt z dowolnego watku bez zamka). Pierwsza doba po wczytaniu - bez kary (pamiec sesji).
    ///  - Gracz: te same reguly w jego osadach; komunikat, gdy poborcy wyszli z jego osady (raz, do powrotu lojalnosci ponad prog).
    ///  - M4 (danina pokoju, KingdomTreasury.PeaceTribute.cs, klucz FolkPeaceTribute): ta sama kara i podloga dla osady, ktora oddala danine pokoju - w dymku
    ///    "Peace tribute from the people's savings"; prog poboru M4 liczy linia M4 (bez komunikatu "war aid" ponizej).
    /// Zapis gry: nic nowego. Wyjatek per osada / per wywolanie - licznik potkniec (CLAUDE.md 7). Linia doby "Lojalnosc za danine (P1b): dzien N".
    /// </summary>
    internal static class FolkAidLoyalty
    {
        private const double Drift = 0.1;   // dryf lojalnosci do 50: -0.1 x (L - 50) na dobe (BKLoyaltyModel.GetSettlementLoyaltyChangeDueToLoyaltyDrift; model gry tak samo)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static Harmony _harmony;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private static RuntimeMethodHandle _active;
        private static bool _wired;
        private static string _model = "-";
        private static TextObject _txt;     // leniwie (bez TextObject w konstruktorze statycznym)
        private static TextObject _txtPeace; // M4: danina pokoju - napis w dymku lojalnosci

        private sealed class Row { public float Nom; public float Lord; public bool Peace; }   // Peace - M4: danina pokoju (ta sama kara, inny napis w dymku)
        private static Dictionary<Town, Row> _snap = new Dictionary<Town, Row>();   // migawka doby - podmieniana w calosci (watek glowny), czytana z dowolnego

        private static int _cTick, _cDesc;                                           // wywolania modelu z kara od poprzedniej linii (Interlocked)
        private static int _w7Cut; private static double _w7CutPts;                   // recenzja (9): koszt W7 obciety podloga tej doby (osady, punkty)
        private static readonly HashSet<Settlement> _gate = new HashSet<Settlement>(); // osady z nadwyzka, ktorych danine zatrzymal prog lojalnosci (pobor tej doby)
        private static int _gateN;                                                    // to samo w klaso-osadach
        private static readonly HashSet<Settlement> _told = new HashSet<Settlement>(); // gracz: osady, o ktorych komunikat "poborcy wyszli" juz byl
        private static readonly List<KeyValuePair<Settlement, double>> _buf = new List<KeyValuePair<Settlement, double>>();
        private static readonly List<Settlement> _tmp = new List<Settlement>();
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly object _lock = new object();

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        /// <summary>Nowa kampania i wczytanie (z KingdomTreasury.WarAidReset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset()
        {
            _snap = new Dictionary<Town, Row>();
            _cTick = _cDesc = 0; _gate.Clear(); _gateN = 0; _told.Clear(); _buf.Clear(); _w7Cut = 0; _w7CutPts = 0;
            lock (_lock) { _stumbles = 0; _err.Clear(); }
        }

        private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static bool On(Settings s) { return s != null && s.FolkWarAidLoyalty && s.FolkWarAid; }
        private static double Cost(Settings s) { return Clamp(s.FolkWarAidLoyaltyCost, 0, 5); }
        private static double Floor(Settings s) { return Clamp(s.FolkWarAidLoyaltyFloor, 0, 100); }
        private static double MinLoy(Settings s) { return Clamp(s.FolkWarAidMinLoyalty, 0, 100); }

        private static void Stumble(string where, Exception e)
        {
            lock (_lock)
            {
                _stumbles++;
                try { if (_err.Add(where)) Log.Error("FolkAidLoyalty." + where, e); } catch { }
            }
        }

        /// <summary>KingdomTreasury.WaRate: prog poboru - osada na skraju buntu (lojalnosc <= FolkWarAidMinLoyalty) nie placi daniny. Klucz wylaczony - false.
        /// Sam odczyt (sciezka poboru i okna mieszkancow).</summary>
        internal static bool GateHolds(Settlement st, Settings s)
        {
            try { return On(s) && st != null && st.Town != null && st.Town.Loyalty <= MinLoy(s); }
            catch { return false; }
        }

        /// <summary>KingdomTreasury.FolkWarAid: klasa osady z nadwyzka bez poboru z powodu progu lojalnosci (licznik linii i komunikat gracza). Watek glowny.</summary>
        internal static void NoteGate(Settlement st)
        {
            if (st == null) return;
            _gate.Add(st); _gateN++;
        }

        /// <summary>Kara P1b tej doby przy lojalnosci loy: najwyzej nominalna, najwyzej 0.1 x (loy - podloga) minus koszt W7 tej doby, najmniej 0.</summary>
        private static double Take(Row r, double loy, Settings s)
        {
            double allow = Drift * (loy - Floor(s)) - r.Lord;
            double x = Math.Min(r.Nom, allow);
            return x > 0 ? x : 0;
        }

        /// <summary>KingdomTreasury.LvCollect (W7, watek glowny, przed Daily tej doby) - recenzja (9): koszt lojalnosci W7 przy kluczu FolkWarAidLoyalty najwyzej
        /// 0.1 x (lojalnosc - podloga), najmniej 0 (wspolna podloga z P1b - rownowaga >= (L + 60) / 2); klucz wylaczony - want bez zmian (stan sprzed P1b).</summary>
        internal static double W7Cost(Town t, double want)
        {
            try
            {
                var s = Settings.Current;
                if (!(want > 0) || t == null || !On(s)) return want;
                double room = Math.Max(0, Drift * (t.Loyalty - Floor(s)));
                if (want <= room) return want;
                _w7Cut++; _w7CutPts += want - room;
                return room;
            }
            catch (Exception e) { Stumble("W7Cost", e); return want; }
        }

        // ------------------------------------------------------------ latka (kampania)
        /// <summary>Z ArmouryBehavior.OnSessionLaunched (kampania istnieje, modele zarejestrowane) - postfiks na metode CZYNNEGO modelu lojalnosci; ta sama metoda
        /// najwyzej raz na proces. Postfiks liczy tylko na metodzie czynnego modelu tej sesji (BK wola base, gdy osada bez danych BK - tamta metoda nie ma latki).</summary>
        internal static void EnsureHooks()
        {
            if (_harmony == null || Campaign.Current == null) return;
            string why = null;
            _wired = false;
            try
            {
                var model = Campaign.Current.Models != null ? Campaign.Current.Models.SettlementLoyaltyModel : null;
                var m = model != null ? PatchTarget.OfActive(model, "CalculateLoyaltyChange", new[] { typeof(Town), typeof(bool) }, "lojalnosc za danine (P1b)") : null;   // HT: klasa deklarujaca (uchwyt ten sam)
                if (model == null) why = "brak modelu lojalnosci";
                else if (m == null) why = "brak metody CalculateLoyaltyChange(Town, bool) w " + model.GetType().FullName;
                else
                {
                    if (!_patched.Contains(m))
                    {
                        _harmony.Patch(m, postfix: new HarmonyMethod(typeof(FolkAidLoyalty), nameof(LoyaltyPostfix)));
                        _patched.Add(m);
                    }
                    _active = m.MethodHandle;
                    _model = (m.DeclaringType != null ? m.DeclaringType.FullName : "?") + " (czynny model " + model.GetType().FullName + ")";
                    _wired = true;
                }
            }
            catch (Exception e) { _wired = false; why = "wyjatek"; Stumble("EnsureHooks", e); }
            Log.Info("Lojalnosc za danine (P1b): start sesji | latka " + (_wired ? "wpieta: " + _model + ".CalculateLoyaltyChange" : "BRAK (" + why + ") - kara nie dziala, prog poboru dziala")
                     + " | Folk War Aid Loyalty " + (On(Settings.Current) ? "tak" : "nie") + ".");
        }

        /// <summary>Postfiks CalculateLoyaltyChange(Town, bool) czynnego modelu: pozycja "War aid from the people's savings" = -kara P1b tej doby (migawka Daily).</summary>
        public static void LoyaltyPostfix(MethodBase __originalMethod, Town __0, bool __1, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || __originalMethod == null || !__originalMethod.MethodHandle.Equals(_active)) return;
                var snap = _snap;
                Row r;
                if (snap == null || snap.Count == 0 || !snap.TryGetValue(__0, out r)) return;
                var s = Settings.Current;
                if (!On(s)) return;
                double x = Take(r, __0.Loyalty, s);
                if (!(x > 0)) return;
                if (r.Peace)
                {
                    if (_txtPeace == null) _txtPeace = new TextObject("Peace tribute from the people's savings");
                    __result.Add((float)-x, _txtPeace);
                }
                else
                {
                    if (_txt == null) _txt = new TextObject("War aid from the people's savings");
                    __result.Add((float)-x, _txt);
                }
                if (__1) Interlocked.Increment(ref _cDesc); else Interlocked.Increment(ref _cTick);
            }
            catch (Exception e) { Stumble("LoyaltyPostfix", e); }
        }

        // ------------------------------------------------------------ doba: migawka i linia - z ArmouryBehavior zaraz po KingdomTreasury.LordLevyDay (W7)
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            bool on = On(s);
            var snap = new Dictionary<Town, Row>();
            int n = 0, nT = 0, nC = 0, nLord = 0, nCut = 0, nZero = 0, ctlGate = 0;
            double nom = 0, take = 0, lord = 0, loySum = 0, loyMin = double.NaN, share = Clamp(s.FolkWarAidShare, 0, 0.2);
            string lowName = "-";
            try
            {
                if (on)
                {
                    TownFolk.AidPlaces(_buf);
                    double cost = Cost(s), gate = MinLoy(s);
                    for (int i = 0; i < _buf.Count; i++)
                    {
                        try
                        {
                            var st = _buf[i].Key;
                            var t = st != null ? st.Town : null;
                            if (t == null) continue;
                            double q = share > 0 ? _buf[i].Value / share : 0;
                            var r = new Row { Nom = (float)(cost * Math.Min(1.0, q)), Lord = (float)Math.Max(0, KingdomTreasury.LordLevyLoyaltyAt(st)), Peace = KingdomTreasury.PeaceTributeFrom(st, day) };
                            snap[t] = r;
                            double loy = t.Loyalty, x = Take(r, loy, s);
                            n++; if (t.IsCastle) nC++; else nT++;
                            nom += r.Nom; take += x; lord += r.Lord;
                            if (r.Lord > 0) nLord++;
                            if (x < r.Nom - 1e-6) { nCut++; if (x <= 0) nZero++; }
                            loySum += loy;
                            if (double.IsNaN(loyMin) || loy < loyMin) { loyMin = loy; lowName = st.Name != null ? st.Name.ToString() : st.StringId; }
                            if (loy + r.Lord <= gate) ctlGate++;   // kontrola: danina od osady z lojalnoscia przed W7 na progu albo nizej (oczekiwane 0)
                        }
                        catch (Exception e) { Stumble("Daily(osada)", e); }
                    }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            _snap = snap;
            try { TellPlayer(s, on); } catch (Exception e) { Stumble("TellPlayer", e); }
            int cT = Interlocked.Exchange(ref _cTick, 0), cD = Interlocked.Exchange(ref _cDesc, 0);
            if (s.LogEnabled)
            {
                try
                {
                    var sb = new StringBuilder(900);
                    sb.Append("Lojalnosc za danine (P1b): dzien ").Append(day)
                      .Append(" | wylacznik Folk War Aid Loyalty ").Append(s.FolkWarAidLoyalty ? "tak" : "nie (bez kary i bez progu - jak przed P1b)")
                      .Append(", Folk War Aid ").Append(s.FolkWarAid ? "tak" : "nie (danina 0 - kara 0)")
                      .Append(" | koszt ").Append(Cost(s).ToString("0.00", Inv)).Append("/d przy danine = ").Append((100 * share).ToString("0.0", Inv))
                      .Append("% calej kiesy ludu (proporcjonalnie, najwyzej tyle); podloga ").Append(Floor(s).ToString("0", Inv))
                      .Append(" (kara najwyzej 0.1 x (lojalnosc - podloga) - koszt W7 tej doby); prog poboru ").Append(MinLoy(s).ToString("0", Inv))
                      .Append(" | latka: ").Append(_wired ? "wpieta " + _model : "BRAK")
                      .Append(" | osady z danina dzis ").Append(n).Append(" (miasta ").Append(nT).Append(", zamki ").Append(nC).Append("): kara nominalna razem ")
                      .Append(nom.ToString("0.00", Inv)).Append(" (srednio ").Append((n > 0 ? nom / n : 0).ToString("0.000", Inv)).Append("), po podlodze i W7 razem ")
                      .Append(take.ToString("0.00", Inv)).Append(" (srednio ").Append((n > 0 ? take / n : 0).ToString("0.000", Inv)).Append(")")
                      .Append("; z W7 tej doby ").Append(nLord).Append(" osad, W7 razem ").Append(lord.ToString("0.00", Inv))
                      .Append("; obciete podloga ").Append(nCut).Append(" (do zera ").Append(nZero).Append(')')
                      .Append("; W7 obciety ta sama podloga ").Append(_w7Cut).Append(" osad (").Append(_w7CutPts.ToString("0.00", Inv)).Append(" pkt)")
                      .Append(" | lojalnosc osad z danina: srednio ").Append(n > 0 ? (loySum / n).ToString("0.0", Inv) : "-")
                      .Append(", najnizsza ").Append(double.IsNaN(loyMin) ? "-" : lowName + " " + loyMin.ToString("0.0", Inv))
                      .Append(" | prog poboru: osad z nadwyzka bez daniny ").Append(_gate.Count).Append(" (klaso-osad ").Append(_gateN).Append(")")
                      .Append("; kontrola - danina przy lojalnosci <= prog ").Append(ctlGate)
                      .Append(" | wywolania modelu z kara od poprzedniej linii: doba (bez opisu) ").Append(cT).Append(", dymek (z opisem) ").Append(cD)
                      .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.000", Inv)).Append(" ms | potkniecia ");
                    lock (_lock) { sb.Append(_stumbles).Append('.'); _stumbles = 0; }
                    Log.Info(sb.ToString());
                }
                catch (Exception e) { Stumble("Daily(linia)", e); }
            }
            _gate.Clear(); _gateN = 0; _w7Cut = 0; _w7CutPts = 0;
        }

        /// <summary>Komunikat gracza (po angielsku): poborcy wyszli z jego osady na skraju buntu - raz, do powrotu lojalnosci ponad prog albo utraty osady.</summary>
        private static void TellPlayer(Settings s, bool on)
        {
            var pc = Clan.PlayerClan;
            if (!on || pc == null) { _told.Clear(); return; }
            _tmp.Clear();
            foreach (var st in _told)
                if (st == null || st.OwnerClan != pc || st.Town == null || st.Town.Loyalty > MinLoy(s)) _tmp.Add(st);
            foreach (var st in _tmp) _told.Remove(st);
            _tmp.Clear();
            foreach (var st in _gate)
                if (st != null && st.OwnerClan == pc && !_told.Contains(st)) _tmp.Add(st);
            if (_tmp.Count == 0) return;
            var names = new List<string>();
            foreach (var st in _tmp) { _told.Add(st); names.Add(st.Name != null ? st.Name.ToString() : st.StringId); }
            string who = names.Count == 1 ? names[0] : names.Count == 2 ? names[0] + " and " + names[1] : names[0] + ", " + names[1] + " and " + (names.Count - 2) + " more";
            Log.Player("The tax gatherers have left " + who + ": " + (names.Count == 1 ? "the place is" : "they are") + " too close to revolt to pay the war aid.", true);
            if (s.LogEnabled) Log.Info("Lojalnosc za danine (P1b): komunikat gracza - poborcy wyszli z " + string.Join(", ", names.ToArray()) + ".");
        }
    }
}
