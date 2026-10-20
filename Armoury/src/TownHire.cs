using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// p6-a7 (fala 2 pomyslow, Jeff 20.10: "A7 TAK razem z odplywem pieniedzy z kas miast") - ROBOTY MIAST Z RAK OKREGU (odplyw kas miast do kies wsi).
    /// PRZYCZYNA (kod + test fin103 19.10, WGRANIE 88 + A7, 160 dob x8: NAKUM-7 kasy miast +69 tys./d, NAKUM-12 +65 tys./d wobec fin102 bez A7 +32 tys./d):
    ///  - regula A7 (StartCalibration.Rule.cs Progress -> A7sPay -> VillageCrafts.PayRebuild -> TownFolk.CraftsRebuildPay) placi przebudowe zajecia 28 dob kosztu K
    ///    za przenoszone rece Z KIESY WSI: material z polki kasy miasta targowego (do kasy) i placa rzemieslnikow (lud miasta - przy kiesach ludu ponad pasmem f5 czesc
    ///    "oddane kasom miast", _a7sCof) - nowy, staly przeplyw kiesy wsi -> kasa miasta, ktorego bez A7 nie ma;
    ///  - przy czynnej A7 zajecia wiosek pobocznych tez ida za sygnalem KS (SideVillageCraftKsSignal - ta sama przebudowa z kiesy wsi do kasy), a rece jedzenia
    ///    f20-d4 (KsFoodHandsFollowMarket, F20Active: !A7RuleOn) staja - jedna regula zajec naraz;
    ///  - istniejace ujscia kas (M6 do kupcow P/R, firma DS i jej przeglad wedlug nadwyzki co 14 dob +25%, gersuma f7) placa PANOM i KORONIE albo ludowi miasta ponad
    ///    pasmem (kappa f5 zatrzymuje to w kasie) - zadne nie oddaje pieniadza wsiom, z ktorych A7 go zabiera (kiesy wsi bez A7 i tak -0.24%/d - NAKUM-5).
    ///  Zmiennosc biegow bez A7 jest duza (NAKUM-12: fin102 +32 tys., n20 -35 tys., n20c -55 tys., n21160 -21 tys., r3160 -3 tys./d), wiec regula nie celuje w A7,
    ///  tylko w sam przyrost kas ponad ich poziom.
    /// REGULA (sredniowiecze: rada miasta z nadwyzki oplaca mury, bruk, mosty i studnie - furmanki z kamieniem i drewnem i robotnicy z wsi okregu; zloto do kies wsi):
    ///  - od dnia, w ktorym firmy DS sa ustalone (DsTowns.FarmsLive - wczesniej kasy jeszcze sie ustawiaja), kazde miasto ma PULAP = wieksze z: kasa w dniu startu reguly
    ///    (zapisana, H) i prog wydatkow (DsTowns.HireKeep: 1.5 x zapas kupcow, z firma DS ten sam prog co gersuma + reszta gersumy + zaleglosci firm) + otwarte rachunki
    ///    zywnosci miasta (KsFoodCredit - kupcy splacaja dlugi najpierw);
    ///  - co dobe miasto ponad pulapem wydaje 1/28 nadwyzki ponad pulap (ok. miesiac - jak M6) na roboty: zloto z kasy do kies wsi, ktorych miastem targowym
    ///    jest (TradeBound, inaczej Bound - ta sama wies, ktora placi mu przebudowe A7), wedlug ognisk; wies spladrowana / najezdzana nie bierze. Przy stalym doplywie I
    ///    kasa stoi na pulap + 28 x I, a wyplata = I - kasa nie rosnie ponad poziom startu (cel: z A7 nie szybciej niz bez A7);
    ///  - bez wyplaty: oblezenie / swiezo zdobyte (DsTowns.RemitOf), "domy najpierw" (KsFolkIncome.HousesFirst - R/P zbiedniali, nadwyzka najpierw do ich domow przez M6,
    ///    jak gersuma), miasto bez wsi do najecia; kasa ponizej pulapu rosnie swobodnie (odbudowa po gersumie, lupie, oblezeniu).
    ///  - wszystkie miasta (AI i gracza) i wszystkie ich wsie; zamki - nie (kasy zamkow maja zawor do panow, 110).
    /// PIENIADZ: przelew kasa miasta -> kiesy wsi (oba w "Pieniadz swiata" - suma swiata bez zmiany); w ksiedze "Przeplywy osad" osobna pozycja naszego ticku
    /// (MoneyLedger.MTownHire, Mark zaraz po tej regule); kontrola linii: zdjete z kas (zmierzone na kasie) = do kies wsi (zmierzone u wsi).
    /// Wyplata to wydatek kasy (TownPurse.NoteTake, jak danina i clo z kasy): doplyw netto kupcow, z ktorego DS ustala i odnawia firmy, bez zmiany; przeglad wedlug
    /// nadwyzki (f5) i gersuma (f7) widza prawdziwa kase - dziela sie przyrostem (ponizej pulapu zostaje im caly).
    /// ZAPIS arm_townhire ("th1|doba startu|id:H;..."), SaveText.Sync; brak klucza (stary zapis, kampania Jeffa) = start reguly od pierwszej doby z firmami DS po wczytaniu.
    /// Reset przy nowej kampanii / przed wczytaniem (ArmouryBehavior, osobna linia _p6a7Reset). Wyjatki per miasto liczone (potkniecia), bez globalnego wylacznika.
    /// Tylko watek glowny (tick dobowy) - bez blokad.
    /// </summary>
    internal static class TownHire
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Ver = "th1";
        private const int SpendDays = 28;              // nadwyzke ponad pulap miasto wydaje w ok. miesiac (1/28 dziennie, jak M6)

        private static readonly Dictionary<string, long> _cap = new Dictionary<string, long>(StringComparer.Ordinal);   // id miasta -> H (kasa w dniu startu reguly)
        private static int _start = -1, _lastDay = -1;
        private static readonly Dictionary<Settlement, List<Village>> _vil = new Dictionary<Settlement, List<Village>>();
        private static int _vilDay = -1;

        // liczniki od ostatniej linii
        private static long _took, _gave, _sessTook;
        private static int _days, _paidTD, _paidV, _siege, _houses, _noVil, _stum, _stumSess, _newCap;
        private static double _msSum, _msMax;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Key { get { var s = Settings.Current; return s != null && s.TownWorksHireDistrict; } }

        internal static void Reset()
        {
            _cap.Clear(); _start = -1; _lastDay = -1; _vil.Clear(); _vilDay = -1; _sessTook = 0; _stumSess = 0; _err.Clear();
            Zero();
        }

        private static void Zero()
        {
            _took = _gave = 0; _days = _paidTD = _paidV = _siege = _houses = _noVil = _stum = _newCap = 0; _msSum = _msMax = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++; _stumSess++;
            try { if (_err.Add(where)) Log.Error("TownHire." + where, e); } catch { }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            try
            {
                if (_start < 0 || _cap.Count == 0) return null;          // nic do zapisania - bez klucza
                var sb = new StringBuilder(_cap.Count * 24 + 16);
                sb.Append(Ver).Append('|').Append(_start).Append('|');
                foreach (var kv in _cap) sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv)).Append(';');
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        internal static void Import(string t)
        {
            try
            {
                _cap.Clear(); _start = -1;
                if (string.IsNullOrEmpty(t)) return;
                var p = t.Split('|');
                if (p.Length < 3 || p[0] != Ver) return;
                int st; if (!int.TryParse(p[1], NumberStyles.Integer, Inv, out st)) return;
                foreach (var e in p[2].Split(';'))
                {
                    int c = e.LastIndexOf(':');
                    if (c <= 0) continue;
                    long h; if (long.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out h) && h >= 0) _cap[e.Substring(0, c)] = h;
                }
                if (_cap.Count > 0) _start = st;
            }
            catch (Exception e) { Stumble("Import", e); _cap.Clear(); _start = -1; }
        }

        // ------------------------------------------------------------ wsie okregu (raz na dobe)
        private static void Villages(int today)
        {
            if (_vilDay == today) return;
            _vilDay = today;
            foreach (var l in _vil.Values) l.Clear();
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null) continue;
                    var mk = v.TradeBound ?? v.Bound;                  // jak miasto targowe przebudowy A7 (StartCalibration.Rule: TradeBound, inaczej Bound)
                    if (mk == null || !mk.IsTown) continue;
                    List<Village> l;
                    if (!_vil.TryGetValue(mk, out l)) { l = new List<Village>(8); _vil[mk] = l; }
                    l.Add(v);
                }
                catch (Exception e) { Stumble("Villages", e); }
            }
        }

        /// <summary>Pulap kasy miasta: max(H, prog wydatkow + rachunki zywnosci). NaN - brak danych (bez wyplaty). g - kasa teraz: rachunki zywnosci liczone tylko,
        /// gdy kasa jest ponad max(H, prog) (rachunki tylko podnosza pulap; long.MaxValue - zawsze, linia).</summary>
        private static double CapOf(Settlement st, Town t, long h, long g)
        {
            double keep = DsTowns.HireKeep(st, t);
            if (double.IsNaN(keep)) return double.NaN;
            if (g <= Math.Max(h, keep)) return Math.Max(h, keep);
            long bills = 0;
            try { bills = Math.Max(0, KsFoodCredit.OlderThanOf(st, -1)); } catch (Exception e) { Stumble("Bills", e); }
            return Math.Max(h, keep + bills);
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior: po KS i VillageCrafts - przebudowy A7 tej doby juz w kasach; potem Mark MTownHire)
        internal static void Daily()
        {
            if (!Key) return;
            int today = (int)CampaignTime.Now.ToDays;
            if (_lastDay == today) return;
            _lastDay = today;
            if (!DsTowns.FarmsLive) return;                              // kasy jeszcze sie ustawiaja (firmy DS nie ustalone) - start reguly pozniej
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                bool first = _start < 0;
                if (first) _start = today;
                Villages(today);
                _days++;
                foreach (var t in Town.AllTowns)
                {
                    try
                    {
                        var st = t != null ? t.Settlement : null;
                        if (st == null || st.StringId == null) continue;
                        long g = t.Gold, h;
                        if (!_cap.TryGetValue(st.StringId, out h)) { _cap[st.StringId] = g; _newCap++; continue; }   // pierwsza doba miasta w regule: H = kasa dzis
                        double cap = CapOf(st, t, h, g);
                        if (double.IsNaN(cap) || g <= cap) continue;
                        if (DsTowns.RemitOf(st) != 0) { _siege++; continue; }
                        if (KsFolkIncome.HousesFirst(st)) { _houses++; continue; }
                        long want = (long)Math.Floor((g - cap) / SpendDays);
                        if (want < 1) continue;
                        Pay(st, t, want);
                    }
                    catch (Exception e) { Stumble("Town", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _msSum += ms; if (ms > _msMax) _msMax = ms;
        }

        private static void Pay(Settlement st, Town t, long want)
        {
            List<Village> l;
            if (!_vil.TryGetValue(st, out l) || l.Count == 0) { _noVil++; return; }
            double w = 0; Village top = null; double topW = -1;
            foreach (var v in l)
            {
                if (v == null || v.VillageState != Village.VillageStates.Normal) continue;
                double x = Math.Max(1.0, v.Hearth);
                w += x;
                if (x > topW) { topW = x; top = v; }
            }
            if (!(w > 0) || top == null) { _noVil++; return; }
            int ask = (int)Math.Min(int.MaxValue / 2, want);
            int g0 = t.Gold;
            t.ChangeGold(-ask);
            long took = (long)g0 - t.Gold;                                // zmierzone na kasie
            if (took <= 0) return;
            long left = took, gave = 0; int nv = 0;
            foreach (var v in l)
            {
                if (v == null || v.VillageState != Village.VillageStates.Normal || v == top) continue;
                long x = (long)Math.Floor(took * Math.Max(1.0, v.Hearth) / w);
                if (x <= 0) continue;
                int v0 = v.Gold;
                v.ChangeGold((int)x);
                long got = (long)v.Gold - v0;
                gave += got; left -= x; nv++;
            }
            if (left > 0)
            {
                int v0 = top.Gold;
                top.ChangeGold((int)left);
                gave += (long)top.Gold - v0; nv++;
            }
            // wydatek kasy, nie spadek zarobku kupcow: doplyw netto kupcow (TownPurse.NetOf -> ustalenie i odnowa firm DS) bez zmiany, jak danina i clo z kasy
            try { TownPurse.NoteTake(st, (int)Math.Min(int.MaxValue, took)); } catch (Exception e) { Stumble("NoteTake", e); }
            _took += took; _gave += gave; _sessTook += took; _paidTD++; _paidV += nv;
        }

        // ------------------------------------------------------------ linia
        internal static void Line()
        {
            if (!Key || !LogGate.Lines) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                long gold = 0, capSum = 0, over = 0; int n = 0, nOver = 0, nCap = 0;
                if (_start >= 0)
                {
                    foreach (var t in Town.AllTowns)
                    {
                        try
                        {
                            var st = t != null ? t.Settlement : null;
                            if (st == null || st.StringId == null) continue;
                            n++; gold += t.Gold;
                            long h; if (!_cap.TryGetValue(st.StringId, out h)) continue;
                            double cap = CapOf(st, t, h, long.MaxValue);
                            if (double.IsNaN(cap)) continue;
                            nCap++; capSum += (long)cap;
                            if (t.Gold > cap) { over += (long)(t.Gold - cap); nOver++; }
                        }
                        catch (Exception e) { Stumble("Line(miasto)", e); }
                    }
                }
                var sb = new StringBuilder(700);
                sb.Append("Roboty miast z rak okregu (p6-a7): dzien ").Append(day)
                  .Append(" | klucz Town Works Hire District TAK, ");
                if (_start < 0) sb.Append("czeka na firmy DS (FarmsLive ").Append(DsTowns.FarmsLive ? "TAK" : "NIE").Append(")");
                else sb.Append("od doby ").Append(_start).Append(" (pulap = max(kasa w dniu startu, prog wydatkow + rachunki zywnosci); 1/").Append(SpendDays).Append(" nadwyzki dziennie)");
                sb.Append(" | od linii (dob ").Append(_days).Append("): zdjete z kas miast ").Append(_took).Append(" = do kies wsi ").Append(_gave)
                  .Append(" (kontrola zdjete - do wsi = ").Append(_took - _gave).Append("; wyplat miasto-doby ").Append(_paidTD).Append(", wsi ").Append(_paidV).Append(")")
                  .Append(", na dobe ").Append(_days > 0 ? (_took / _days).ToString(Inv) : "0")
                  .Append(" | bez wyplaty (miasto-doby): oblezenie / zdobycie ").Append(_siege).Append(", domy najpierw ").Append(_houses).Append(", bez wsi ").Append(_noVil)
                  .Append(", nowe pulapy ").Append(_newCap)
                  .Append(" | miasta ").Append(n).Append(": kasy razem ").Append(gold).Append(", pulapy razem ").Append(capSum).Append(" (").Append(nCap).Append(" miast)")
                  .Append(", ponad pulapem ").Append(over).Append(" (").Append(nOver).Append(" miast)")
                  .Append(" | od startu sesji do kies wsi ").Append(_sessTook)
                  .Append(" | czas ").Append((_days > 0 ? _msSum / _days : 0).ToString("0.00", Inv)).Append(" ms (najwiecej ").Append(_msMax.ToString("0.00", Inv)).Append(")")
                  .Append(" | potkniecia ").Append(_stum).Append(" (sesja ").Append(_stumSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
            Zero();
        }
    }
}
