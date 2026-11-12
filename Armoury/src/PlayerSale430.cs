using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 430 (audyt ekonomii 11.11, poprawka A6; decyzja Jeffa 3 "tak ale rozsadnie"): SPRZEDAZ GRACZA - PREMIA PUSTEJ POLKI MALEJE Z KAZDA SZTUKA, PULAP WARTOSCI.
    /// Dowod (Logs/2026-11-09_21-17-29/handel.log 23:17:35, Hornvale): 11 rydwanow (wartosc 22 000) sprzedane za 430 829 zl = 178% wartosci
    /// (48 328 / 44 311 / 28 154 za sztuke = 220% / 201% / 128%). Tego samego dnia zwykly lup szedl w tym miescie za 13-50% wartosci (polka x0.45-1.0).
    /// Przyczyna (SupplyDemand.PricePostfix): cena sprzedazy = ulamek targu (0.42 wartosci) x mnoznik polki x surowce; mnoznik pustej polki przy duzym popycie
    /// stoi na zacisku Supply Demand Max Factor (x4) i schodzi z niego dopiero, gdy (popyt + 1) / (polka + 1) spadnie ponizej 16 - pierwsze sztuki nie ruszaja ceny,
    /// a pamiec polki (299b) dodatkowo splaszcza spadek. Kupiec placil wiec jednemu sprzedajacemu premie niedoboru za kazda z jedenastu sztuk.
    ///
    /// REGULA (klucz Player Sale Saturation; tylko gracz w ekranie handlu, miasto albo zamek pod prawem podazy, tylko sprzet - towary handlowe maja wlasny rynek gry):
    ///  1. SUFIT PREMII: k = sztuki tego koszyka (typ x tier), ktore gracz sprzedal temu miastu (pamiec miasta, topnieje o Player Sale Recover Per Day na dobe)
    ///     + sztuki dodane na polke w tej wizycie (polka zywa - migawka z otwarcia ekranu; odkup i kupno z koszyka zdejmuja). Pierwsze Player Sale Free Pieces
    ///     sztuk: bez zmian. Potem sufit mnoznika polki = 1 + (Supply Demand Max Factor - 1) / (1 + (k - wolne + 1) / Player Sale Fade Pieces).
    ///     Mnoznik sprzedazy = MNIEJSZA z dwoch liczb (mnoznik polki, sufit) - nigdy iloczyn: spadek ceny z rosnacym zapasem (prawo podazy) nie liczy sie dwa razy,
    ///     a polka <= 1 (zwykly, masowy lup na pelnej polce) nie zmienia sie wcale. Sufit nie schodzi ponizej 1 - regula zabiera tylko premie niedoboru.
    ///  2. PULAP WARTOSCI: kupiec nie da graczowi za sztuke wiecej niz Player Sale Ceil Percent (100) jej wartosci ze stanem (0 = bez pulapu).
    /// Zloto: ta sama droga (kasa miasta -> gracz, InventoryLogic.DoneLogic), mniejsza kwota - nic nie powstaje i nic nie znika. Kupno, AI, karawany, partie rodu,
    /// sprzedaz ludzi (MenPurse) i sondy cen (_quiet) - bez zmian. Klucz NIE = cena jak przed 430 (pamiec prowadzona dalej, bez skutku).
    /// Pamiec: zapis "arm_sprzedaz430" ("miasto;koszyk:sztuki,...|..."), brak klucza = pusto; Reset w konstruktorze ArmouryBehavior.
    /// Linia doby "Sprzedaz gracza (430 A6)" z kontrola pamieci (start + sprzedane - stopione - stan = 0).
    /// </summary>
    internal static class PlayerSale430
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PlayerSaleSaturation && SupplyDemand.Active; } }

        // miasto -> koszyk (typ x 8 + tier) -> sztuki sprzedane przez gracza (topnieja)
        private static readonly Dictionary<string, Dictionary<int, float>> _sold = new Dictionary<string, Dictionary<int, float>>(StringComparer.Ordinal);
        private static double _dayStart, _dAdded, _dMelted;
        private static int _dVisits, _dCutMoves, _dCeilMoves, _stumbles, _importN = -1, _importBad;
        private static long _dCut, _dCeilCut;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            _sold.Clear(); _dayStart = _dAdded = _dMelted = 0; _dVisits = _dCutMoves = _dCeilMoves = _stumbles = 0; _dCut = _dCeilCut = 0;
            _importN = -1; _importBad = 0; _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("PlayerSale430." + where, e); } catch { }
        }

        private static float Stored(string town, int bucket)
        {
            Dictionary<int, float> d; float n;
            if (town == null || !_sold.TryGetValue(town, out d) || !d.TryGetValue(bucket, out n)) return 0f;
            return n;
        }

        private static double Total()
        {
            double t = 0;
            foreach (var kv in _sold) foreach (var x in kv.Value) t += x.Value;
            return t;
        }

        /// <summary>Mnoznik polki sprzedazy gracza: min(mnoznik polki, sufit premii przy k sztukach). k = pamiec miasta + sztuki tej wizyty. Nie rzuca.</summary>
        internal static float CapShelf(Settlement st, int bucket, float shelfF, int visit, out float cap, out float k)
        {
            cap = 0f; k = Math.Max(0, visit);
            try
            {
                var s = Settings.Current;
                k += Stored(st != null ? st.StringId : null, bucket);
                float hi = Math.Max(1f, s.SupplyDemandMaxFactor);
                float g = Fade(k);
                cap = 1f + (hi - 1f) * g;
                return shelfF > cap ? cap : shelfF;
            }
            catch (Exception e) { Stumble("CapShelf", e); return shelfF; }
        }

        /// <summary>Zanik premii przy k sztukach: 1 dla pierwszych Player Sale Free Pieces, potem 1 / (1 + (k - wolne + 1) / Player Sale Fade Pieces). Jedna krzywa dla sufitu
        /// mnoznika polki i dla pulapu wartosci (430-p).</summary>
        internal static float Fade(float k)
        {
            var s = Settings.Current;
            float free = Math.Max(0, s.PlayerSaleFreePieces), fade = Math.Max(0.25f, s.PlayerSaleFadePieces);
            return k < free ? 1f : 1f / (1f + (k - free + 1f) / fade);
        }

        /// <summary>430-p (recenzja A6-2, decyzja Jeffa 3 "cena spada z kazda sztuka"): pulap wartosci tez maleje ze sztukami. plain = cena tej sztuki bez premii
        /// niedoboru (polka najwyzej x1); pulap = plain + (pulap pelny - plain) x zanik(k) - ta sama krzywa co sufit premii, nigdy ponizej ceny bez premii.
        /// W przypadku z logu (rydwany, Hornvale) sam sufit mnoznika polki nie wiazal do ok. 19. sztuki - wszystkie 11 szly po pulapie 100%.
        /// Klucz Player Sale Ceil Fades NIE - pulap staly jak w 430. 0 = bez pulapu.</summary>
        internal static int CeilAt(EquipmentElement el, int plain, float k)
        {
            int full = Ceil(el);
            try
            {
                var s = Settings.Current;
                if (full <= 0 || s == null || !s.PlayerSaleCeilFades || plain >= full) return full;
                float g = Fade(Math.Max(0f, k));
                if (g >= 1f) return full;
                return Math.Max(1, (int)Math.Round(plain + (full - plain) * (double)g));
            }
            catch (Exception e) { Stumble("CeilAt", e); return full; }
        }

        /// <summary>Pulap ceny sprzedazy sztuki (zl): Player Sale Ceil Percent wartosci ze stanem; 0 = bez pulapu albo klucz NIE.</summary>
        internal static int Ceil(EquipmentElement el)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !On || s.PlayerSaleCeilPercent <= 0 || el.Item == null) return 0;
                return Math.Max(1, (int)((long)el.ItemValue * s.PlayerSaleCeilPercent / 100L));
            }
            catch (Exception e) { Stumble("Ceil", e); return 0; }
        }

        // liczniki ruchow w ekranie handlu (takze pozniej cofnietych - sama miara skali)
        internal static void NoteCut(long zl) { if (zl > 0) { _dCutMoves++; _dCut += zl; } }
        internal static void NoteCeil(long zl) { if (zl > 0) { _dCeilMoves++; _dCeilCut += zl; } }

        /// <summary>Dopisek do linii "PodazPopyt: ... SPRZEDAZ" gracza (handel.log).</summary>
        internal static string Explain(float k, float cap, bool binds, int ceilP, bool ceilBinds)
        {
            try
            {
                return ", 430: sprzedane tu z tego koszyka " + k.ToString("0.#", Inv) + " szt., sufit premii polki x" + cap.ToString("0.00", Inv) + (binds ? " (wiaze)" : "")
                       + ", pulap wartosci " + (ceilP > 0 ? ceilP.ToString(Inv) + (ceilBinds ? " (wiaze)" : "") : "-");
            }
            catch { return ""; }
        }

        /// <summary>Zamkniecie ekranu handlu gracza (PlayerInventoryExchangeEvent, w srodku DoneLogic - ekran jeszcze otwarty, polka juz po transakcji): przyrost koszykow
        /// sprzetu na polce miasta wobec migawki z otwarcia idzie do pamieci miasta. Pamiec prowadzona takze przy kluczu NIE (jak RansomRepeat).</summary>
        internal static void OnPlayerExchange(List<(ItemRosterElement, int)> purchased, List<(ItemRosterElement, int)> sold, bool isTrading)
        {
            // 430-p (recenzja A6-3): migawka polki z otwarcia ekranu zdejmowana po kazdym zamknieciu handlu (Done) - takze bez sprzedazy. Bez tego druga wizyta
            // pod rzad (bez zadnej wyceny przy zamknietym ekranie pomiedzy) dostawala stara migawke: sztuki pierwszej wizyty liczone drugi raz w k i w pamieci miasta.
            Settlement here = null;
            try
            {
                if (!isTrading) return;
                here = Settlement.CurrentSettlement ?? (MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null);
                if (QuartermasterEscrow.Active || !SupplyDemand.Active || sold == null || sold.Count == 0) return;
                if (here == null || here.StringId == null || (!here.IsTown && !here.IsCastle) || here.ItemRoster == null) return;
                var add = new Dictionary<int, int>();
                SupplyDemand.VisitDeltas(here.ItemRoster, add);
                if (add.Count == 0) return;
                Dictionary<int, float> d;
                if (!_sold.TryGetValue(here.StringId, out d)) { d = new Dictionary<int, float>(); _sold[here.StringId] = d; }
                int pieces = 0;
                foreach (var kv in add)
                {
                    float n; d.TryGetValue(kv.Key, out n);
                    d[kv.Key] = n + kv.Value; pieces += kv.Value;
                }
                _dAdded += pieces; _dVisits++;
                if (Settings.Current != null && Settings.Current.LogEnabled)
                    Log.Info("Sprzedaz gracza (430 A6): " + here.Name + " - do pamieci miasta " + pieces + " szt. w " + add.Count + " koszykach (netto tej wizyty); pamiec miasta razem "
                             + SumOf(d).ToString("0.#", Inv) + " szt.");
            }
            catch (Exception e) { Stumble("OnPlayerExchange", e); }
            finally { try { if (here != null && here.ItemRoster != null) SupplyDemand.DropSnapshot(here.ItemRoster); } catch (Exception e) { Stumble("OnPlayerExchange.Drop", e); } }
        }

        private static double SumOf(Dictionary<int, float> d) { double t = 0; foreach (var x in d) t += x.Value; return t; }

        /// <summary>Takt doby: pamiec topnieje o Player Sale Recover Per Day sztuk na koszyk, potem linia doby z kontrola pamieci.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                float eat = Math.Max(0f, s.PlayerSaleRecoverPerDay);
                if (eat > 0f && _sold.Count > 0)
                {
                    var deadTowns = new List<string>();
                    var keys = new List<int>();
                    foreach (var kv in _sold)
                    {
                        keys.Clear(); keys.AddRange(kv.Value.Keys);
                        foreach (var b in keys)
                        {
                            float n = kv.Value[b], m = Math.Min(n, eat);
                            _dMelted += m;
                            if (n - m <= 0.0001f) kv.Value.Remove(b); else kv.Value[b] = n - m;
                        }
                        if (kv.Value.Count == 0) deadTowns.Add(kv.Key);
                    }
                    foreach (var t in deadTowns) _sold.Remove(t);
                }
                double now = Total();
                int buckets = 0; foreach (var kv in _sold) buckets += kv.Value.Count;
                if (s.LogEnabled && (now > 0 || _dAdded > 0 || _dMelted > 0 || _dCutMoves > 0 || _dCeilMoves > 0 || _stumbles > 0 || _importN >= 0))
                {
                    double diff = _dayStart + _dAdded - _dMelted - now;
                    var sb = new StringBuilder(512);
                    sb.Append("Sprzedaz gracza (430 A6): dzien ").Append((int)CampaignTime.Now.ToDays)
                      .Append(" | klucz Player Sale Saturation ").Append(On ? "tak" : "nie").Append(" (wolne ").Append(Math.Max(0, s.PlayerSaleFreePieces))
                      .Append(" szt., zanik premii ").Append(Math.Max(0.25f, s.PlayerSaleFadePieces).ToString("0.##", Inv)).Append(" szt., odnowa ").Append(eat.ToString("0.##", Inv))
                      .Append(" szt./d, pulap ").Append(s.PlayerSaleCeilPercent).Append("% wartosci ze stanem").Append(s.PlayerSaleCeilFades ? ", maleje ze sztukami do ceny bez premii (430-p)" : ", staly").Append("; sufit polki gry x").Append(Math.Max(1f, s.SupplyDemandMaxFactor).ToString("0.##", Inv)).Append(')')
                      .Append(" | ruchy sprzedazy w ekranie handlu dzis (takze cofniete): z obnizona premia polki ").Append(_dCutMoves).Append(" (o ").Append(_dCut)
                      .Append(" zl przed granicami), na pulapie wartosci ").Append(_dCeilMoves).Append(" (o ").Append(_dCeilCut).Append(" zl)")
                      .Append(" | pamiec: wizyt z przyrostem ").Append(_dVisits).Append(", start ").Append(_dayStart.ToString("0.##", Inv)).Append(" + sprzedane ").Append(_dAdded.ToString("0.##", Inv))
                      .Append(" - stopione ").Append(_dMelted.ToString("0.##", Inv)).Append(" = stan ").Append(now.ToString("0.##", Inv)).Append(" szt. (miast ").Append(_sold.Count)
                      .Append(", koszykow ").Append(buckets).Append("); KONTROLA pamieci roznica ").Append((Math.Abs(diff) < 0.01 ? 0.0 : diff).ToString("0.##", Inv))
                      .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "")
                      .Append(_importN >= 0 ? " | wczytano wpisow " + _importN + " (bledne " + _importBad + ")" : "").Append('.');
                    Log.Info(sb.ToString());
                }
                _importN = -1;
                _dayStart = now;
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dAdded = _dMelted = 0; _dVisits = _dCutMoves = _dCeilMoves = 0; _dCut = _dCeilCut = 0;
        }

        // ------------------------------------------------------------ zapis "arm_sprzedaz430": "miasto;koszyk:sztuki,koszyk:sztuki|miasto2;..."
        private static readonly char[] Bad = { '|', ';', ':', ',' };

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_sold.Count * 32 + 4);
                foreach (var kv in _sold)
                {
                    if (kv.Key.IndexOfAny(Bad) >= 0 || kv.Value.Count == 0) continue;
                    if (sb.Length > 0) sb.Append('|');
                    sb.Append(kv.Key).Append(';');
                    bool first = true;
                    foreach (var x in kv.Value)
                    {
                        if (x.Value <= 0f) continue;
                        if (!first) sb.Append(','); first = false;
                        sb.Append(x.Key.ToString(Inv)).Append(':').Append(x.Value.ToString("0.##", Inv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _sold.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (!string.IsNullOrEmpty(data))
                    foreach (var part in data.Split('|'))
                    {
                        if (part.Length == 0) continue;
                        var half = part.Split(';');
                        if (half.Length != 2 || half[0].Length == 0) { _importBad++; continue; }
                        var d = new Dictionary<int, float>();
                        foreach (var pair in half[1].Split(','))
                        {
                            if (pair.Length == 0) continue;
                            var tv = pair.Split(':');
                            int b; float n;
                            if (tv.Length == 2 && int.TryParse(tv[0], NumberStyles.Integer, Inv, out b) && float.TryParse(tv[1], NumberStyles.Float, Inv, out n) && n > 0f) { d[b] = n; _importN++; }
                            else _importBad++;
                        }
                        if (d.Count > 0) _sold[half[0]] = d;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
            _dayStart = Total();
        }
    }
}
