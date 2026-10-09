using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174, PYTANIE 4 JEFFA - STARY NADMIAR NA ZLOM (wylacznik OldStockToScrap, DOMYSLNIE WYLACZONY - czeka na decyzje Jeffa).
    /// Kampania Jeffa: ok. 1 mln sztuk na polkach, w ok. 2/3 z dawnych darmowych kompletow (glownie bron 1H t1-t2 i buty); 174.0 zamyka ujscia, ktore
    /// ten stos powoli zjadaly - bez decyzji zostanie na zawsze. Regula (historia: stare kolczugi w Anglii lat 1330. szly na zlom za 7-14% ceny nowej):
    /// nadmiar koszyka (typ x tier) na polce miasta ponad rok popytu (364 x srednia dziennych zakupow lordow, zalog i notabli w tym miescie, zmierzona
    /// w tej sesji) i ponad popyt polki (SupplyDemand.Demand) kowale miasta skupuja na zlom po OldStockScrapDailyShare dziennie (1%), najgorsze
    /// sztuki najpierw. Zlom to metal: OldStockScrapYield (0.5) rudy, z ktorej sztuke wykuto (WorkshopLaw.Needs), wraca na polke tego miasta jako ruda -
    /// nic z niczego (sztuka znika, metal zostaje w czesci). Bez zlota: kowale i kupcy to ta sama kasa miasta (jak strzelarze 172). Rozgrzewka:
    /// regula rusza po 30 dobach pomiaru zakupow w sesji (srednia od zera nie moze udawac braku popytu); do 30 dob srednia arytmetyczna dni pomiaru
    /// (recenzja 174). Pomiar to zakupy wojska (AiGear, notable) - gracz, handel dzienny i karawany nie wchodza; reszte popytu polki daje
    /// SupplyDemand.Demand. Linia "Zlom z nadmiaru (174, pytanie 4)".
    /// 174b.6: licznik dob pomiaru, ostatnia doba, srednie zakupow, dzisiejsze zakupy, ulamki skupu i ulamki rudy ze zlomu w zapisie gry (klucz "arm_scrap",
    /// SaveText.Sync). Klucze wedlug StringId osady + koszyk (MBGUID.InternalValue z Key() zyje tylko w sesji), po wczytaniu przeliczone na klucze sesji
    /// (ResolvePending w OnSessionLaunched albo na poczatku pierwszej doby). Przy kazdym zapisie probny odczyt napisu tym samym parserem (krytyka 174b, P10).
    /// Brak klucza (zapis sprzed 174b) - rozgrzewka od zera, jak dotad.
    /// </summary>
    internal static class ArmsScrap
    {
        private static readonly Dictionary<long, float> _ema = new Dictionary<long, float>();     // (miasto, koszyk) -> srednia dziennych zakupow
        private static readonly Dictionary<long, float> _today = new Dictionary<long, float>();
        private static readonly Dictionary<long, float> _acc = new Dictionary<long, float>();     // ulamki skupu
        private static readonly Dictionary<Town, float> _oreAcc = new Dictionary<Town, float>();
        private static int _days, _lastDay = -1, _stumbles;
        private static ItemObject _ore;

        internal static bool On { get { var s = Settings.Current; return s != null && s.OldStockToScrap; } }

        internal static void Reset() { _ema.Clear(); _today.Clear(); _acc.Clear(); _oreAcc.Clear(); _keys.Clear(); _days = 0; _lastDay = -1; _stumbles = 0; _ore = null; _errSites.Clear(); _pending = null; _loaded = 0; }

        // 174b.6: klucz sesji -> (osada, koszyk) - do zapisu po StringId (bez rozkladania liczby klucza)
        private static readonly Dictionary<long, KeyValuePair<Settlement, int>> _keys = new Dictionary<long, KeyValuePair<Settlement, int>>();
        private static string _pending;
        private static int _loaded;   // 0 nowa gra (nic nie wczytano), 1 brak klucza w zapisie, 2 napis czeka na rozwiazanie, 3 rozwiazany, 4 nieczytelny

        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static void Stumble(string where, Exception e)   // recenzja 174: Log.Error raz na miejsce, reszta w liczniku linii
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("ArmsScrap." + where, e);
        }

        private static long Key(Settlement st, int basket) { return ((long)st.Id.InternalValue << 8) ^ (uint)basket; }

        /// <summary>Zakup uzbrojenia z polki (AiGear, notable) - pomiar popytu koszyka w miescie (tylko licznik).</summary>
        internal static void NoteBuy(Settlement market, ItemObject it, int n)
        {
            try
            {
                if (market == null || it == null || n <= 0 || !ArmsLeaks.ArmsPiece(it)) return;
                int basket = (int)it.ItemType * 10 + Math.Max(1, Math.Min(6, (int)it.Tier + 1));
                long k = Key(market, basket);
                if (!_keys.ContainsKey(k)) _keys[k] = new KeyValuePair<Settlement, int>(market, basket);
                float v; _today.TryGetValue(k, out v); _today[k] = v + n;
            }
            catch { _stumbles++; }
        }

        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (_pending != null) { try { ResolvePending("doba przed startem sesji"); } catch (Exception e) { Stumble("ResolvePending(doba)", e); } }
            if (day == _lastDay) return;   // 174b.6: _lastDay z zapisu - pierwsza doba po wczytaniu moze miec ten sam numer co ostatnia przed zapisem (Campaign.CreateCampaignEvents)
            _lastDay = day;
            // srednia zakupow (ok. 30 dob); recenzja 174: w pierwszych 30 dobach sesji srednia arytmetyczna dni pomiaru (krok 1/n), nie EMA od zera -
            // EMA 1/30 po 30 dobach rozgrzewki miala ok. 63% prawdziwej wartosci, a skup bral wiecej niz "ponad rok popytu"
            try
            {
                float div = Math.Min(_days + 1, 30);
                var keys = new HashSet<long>(_ema.Keys); foreach (var k in _today.Keys) keys.Add(k);
                foreach (var k in keys) { float e, t; _ema.TryGetValue(k, out e); _today.TryGetValue(k, out t); _ema[k] = e + (t - e) / div; }
                _today.Clear(); _days++;
            }
            catch (Exception e) { Stumble("Daily(srednia)", e); }
            if (!On) return;
            var s = Settings.Current;
            if (_days < 30) { Log.Info("Zlom z nadmiaru (174, pytanie 4): dzien " + day + " - rozgrzewka " + _days + "/30 dob pomiaru zakupow w tej sesji, skup jeszcze nie rusza."); return; }
            if (_ore == null) { try { _ore = MBObjectManager.Instance.GetObject<ItemObject>("iron"); } catch { } }
            float share = MBMath.ClampFloat(s.OldStockScrapDailyShare, 0f, 1f), yield = MBMath.ClampFloat(s.OldStockScrapYield, 0f, 1f);
            long pieces = 0, worth = 0, excessAll = 0; int towns = 0, oreBack = 0;
            var byType = new Dictionary<int, long>();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null) continue;
                var gf = GoodsLedger.Begin(GoodsLedger.FScrap, t);
                try
                {
                    var roster = t.Owner.ItemRoster;
                    var stock = new Dictionary<int, List<ItemRosterElement>>();
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !ArmsLeaks.ArmsPiece(it) || ArmsPricing.IsUnique(it) || it.IsCraftedByPlayer) continue;
                        int b = (int)it.ItemType * 10 + Math.Max(1, Math.Min(6, (int)it.Tier + 1));
                        List<ItemRosterElement> l; if (!stock.TryGetValue(b, out l)) { l = new List<ItemRosterElement>(); stock[b] = l; }
                        l.Add(el);
                    }
                    int made = 0;
                    foreach (var kv in stock)
                    {
                        int have = 0; foreach (var el in kv.Value) have += el.Amount;
                        long k = Key(t.Settlement, kv.Key);
                        float ema; _ema.TryGetValue(k, out ema);
                        float keep = 364f * ema + SupplyDemand.Demand(t.Settlement, (ItemObject.ItemTypeEnum)(kv.Key / 10), kv.Key % 10);
                        float excess = have - keep;
                        if (excess <= 0f) continue;
                        excessAll += (long)excess;
                        float a; _acc.TryGetValue(k, out a);
                        a += excess * share;
                        int n = (int)a; _acc[k] = a - n;
                        // poprawka 174b (recenzja): klucz zapisu takze dla koszyka bez zakupow (stary nadmiar bez popytu - wlasnie ten idzie na zlom); bez tego
                        // Export pomijal jego ulamek skupu, a probny odczyt mowil "zgodny" (zapis i odczyt pomijaly to samo)
                        if (!_keys.ContainsKey(k)) _keys[k] = new KeyValuePair<Settlement, int>(t.Settlement, kv.Key);
                        if (n <= 0) continue;
                        kv.Value.Sort((x, y) => x.EquipmentElement.ItemValue.CompareTo(y.EquipmentElement.ItemValue));   // najgorsze sztuki najpierw
                        foreach (var el in kv.Value)
                        {
                            if (n <= 0) break;
                            int take = Math.Min(n, el.Amount);
                            roster.AddToCounts(el.EquipmentElement, -take);
                            n -= take; pieces += take; made += take; worth += (long)el.EquipmentElement.Item.Value * take;
                            long bt; byType.TryGetValue(kv.Key, out bt); byType[kv.Key] = bt + take;
                            float d; var need = WorkshopLaw.Needs(el.EquipmentElement.Item, out d);
                            if (need != null && yield > 0f) { float o; _oreAcc.TryGetValue(t, out o); _oreAcc[t] = o + need[0] * take * yield; }
                        }
                    }
                    if (made > 0) towns++;
                    float oa;
                    if (_ore != null && _oreAcc.TryGetValue(t, out oa) && oa >= 1f)
                    {
                        int ore = (int)oa; _oreAcc[t] = oa - ore;
                        roster.AddToCounts(_ore, ore);
                        OreLedger.NoteScrap(_ore, ore);
                        oreBack += ore;
                    }
                }
                catch (Exception e) { Stumble("Daily", e); }
                finally { GoodsLedger.End(gf); }
            }
            var parts = new List<string>();
            var bl = new List<KeyValuePair<int, long>>(byType); bl.Sort((x, y) => y.Value.CompareTo(x.Value));
            for (int i = 0; i < bl.Count && i < 8; i++) parts.Add((ItemObject.ItemTypeEnum)(bl[i].Key / 10) + " t" + (bl[i].Key % 10) + " " + bl[i].Value);
            Log.Info("Zlom z nadmiaru (174, pytanie 4): dzien " + day + " - skupiono na zlom " + pieces + " szt. (wartosc nowych " + worth + " d) w " + towns + " miastach [" + string.Join(", ", parts.ToArray())
                     + "]; ruda ze zlomu " + oreBack + " ladunkow (wydajnosc " + yield.ToString("0.##", CultureInfo.InvariantCulture) + "); nadmiar swiata ponad rok popytu ok. " + excessAll + " szt.; potkniecia " + _stumbles + ".");
            _stumbles = 0;
        }

        // ------------------------------------------------------------ 174b.6: zapis
        // Format: "1|dob|ostatnia doba~osada:koszyk=srednia,dzis,ulamek;...~osada=ruda;..." (liczby: 4 cyfry znaczace, kultura niezmienna).
        private sealed class Parsed
        {
            public int Days, LastDay;
            public readonly List<string> Sid = new List<string>(); public readonly List<int> Basket = new List<int>();
            public readonly List<float> Ema = new List<float>(), Today = new List<float>(), Acc = new List<float>();
            public readonly List<string> OreSid = new List<string>(); public readonly List<float> Ore = new List<float>();
        }

        private static string G4(float v) { return v.ToString("G4", CultureInfo.InvariantCulture); }
        private static readonly char[] Seps = { '|', '~', ';', ':', '=', ',' };
        private static bool Bad(string sid) { return string.IsNullOrEmpty(sid) || sid.IndexOfAny(Seps) >= 0; }

        private static float F(string s)
        {
            float v;
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || float.IsNaN(v) || float.IsInfinity(v)) throw new FormatException("liczba: " + s);
            return v;
        }

        /// <summary>Parser napisu zapisu - ten sam dla probnego odczytu przy zapisie i dla wczytania; zly napis = wyjatek.</summary>
        private static Parsed Parse(string s)
        {
            var p = new Parsed();
            var parts = s.Split('~');
            if (parts.Length < 3) throw new FormatException("czesci " + parts.Length);
            var head = parts[0].Split('|');
            if (head.Length < 3 || head[0] != "1") throw new FormatException("naglowek");
            p.Days = int.Parse(head[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
            p.LastDay = int.Parse(head[2], NumberStyles.Integer, CultureInfo.InvariantCulture);
            foreach (var rec in parts[1].Split(';'))
            {
                if (rec.Length == 0) continue;
                int eq = rec.LastIndexOf('='), colon = rec.LastIndexOf(':', eq < 0 ? rec.Length - 1 : eq);
                if (eq <= 0 || colon <= 0) throw new FormatException("para: " + rec);
                var v = rec.Substring(eq + 1).Split(',');
                if (v.Length != 3) throw new FormatException("wartosci: " + rec);
                p.Sid.Add(rec.Substring(0, colon));
                p.Basket.Add(int.Parse(rec.Substring(colon + 1, eq - colon - 1), NumberStyles.Integer, CultureInfo.InvariantCulture));
                p.Ema.Add(F(v[0])); p.Today.Add(F(v[1])); p.Acc.Add(F(v[2]));
            }
            foreach (var rec in parts[2].Split(';'))
            {
                if (rec.Length == 0) continue;
                int eq = rec.LastIndexOf('=');
                if (eq <= 0) throw new FormatException("ruda: " + rec);
                p.OreSid.Add(rec.Substring(0, eq)); p.Ore.Add(F(rec.Substring(eq + 1)));
            }
            return p;
        }

        /// <summary>Zapis (tylko przy zapisie gry): napis + probny odczyt w pamieci (liczba par, doby, suma srednich i rudy).</summary>
        internal static string Export()
        {
            try { if (_pending != null) ResolvePending("zapis przed startem sesji"); }
            catch (Exception e) { Stumble("Export(ResolvePending)", e); }
            var sb = new StringBuilder();
            sb.Append("1|").Append(_days).Append('|').Append(_lastDay).Append('~');
            int pairs = 0; double sumEma = 0, sumOre = 0;
            var seen = new HashSet<Settlement>();
            var all = new HashSet<long>(_ema.Keys); foreach (var k in _today.Keys) all.Add(k); foreach (var k in _acc.Keys) all.Add(k);
            foreach (var k in all)
            {
                try
                {
                    KeyValuePair<Settlement, int> sk;
                    if (!_keys.TryGetValue(k, out sk) || sk.Key == null || !sk.Key.IsTown || Bad(sk.Key.StringId)) continue;   // zamki nie maja skupu
                    float e, t, a; _ema.TryGetValue(k, out e); _today.TryGetValue(k, out t); _acc.TryGetValue(k, out a);
                    if (e < 0.001f && t <= 0f && a < 0.001f) continue;   // <= 0.36 sztuki roku popytu - pomijalne
                    string se = G4(e), st = G4(t), sa = G4(a);
                    sb.Append(sk.Key.StringId).Append(':').Append(sk.Value).Append('=').Append(se).Append(',').Append(st).Append(',').Append(sa).Append(';');
                    pairs++; seen.Add(sk.Key); sumEma += F(se);
                }
                catch (Exception ex) { Stumble("Export(para)", ex); }
            }
            sb.Append('~');
            int ores = 0;
            foreach (var kv in _oreAcc)
            {
                if (kv.Key == null || kv.Key.Settlement == null || Bad(kv.Key.Settlement.StringId) || kv.Value <= 0f) continue;
                string so = G4(kv.Value);
                sb.Append(kv.Key.Settlement.StringId).Append('=').Append(so).Append(';');
                ores++; sumOre += F(so);
            }
            string s = sb.ToString();
            // probny odczyt (krytyka 174b, P10): ten sam parser co przy wczytaniu, porownanie z tym, co wlasnie zapisalismy
            string probe;
            try
            {
                var p = Parse(s);
                double pe = 0, po = 0; foreach (var v in p.Ema) pe += v; foreach (var v in p.Ore) po += v;
                bool ok = p.Sid.Count == pairs && p.OreSid.Count == ores && p.Days == _days && p.LastDay == _lastDay
                          && Math.Abs(pe - sumEma) <= 1e-3 * Math.Max(1.0, Math.Abs(sumEma)) && Math.Abs(po - sumOre) <= 1e-3 * Math.Max(1.0, Math.Abs(sumOre));
                probe = ok ? "probny odczyt zgodny (" + pairs + " par, ruda " + ores + " miast)"
                           : "probny odczyt ROZJAZD (par " + p.Sid.Count + "/" + pairs + ", ruda " + p.OreSid.Count + "/" + ores + ", doby " + p.Days + "/" + _days + ", srednie " + pe.ToString("0.###", CultureInfo.InvariantCulture) + "/" + sumEma.ToString("0.###", CultureInfo.InvariantCulture) + ")";
            }
            catch (Exception ex) { Stumble("Export(probny odczyt)", ex); probe = "probny odczyt ROZJAZD (wyjatek parsera)"; }
            Log.Info("Zlom z nadmiaru (zapis 174b): zapisano " + pairs + " par z " + seen.Count + " miast, ruda ze zlomu w toku w " + ores + " miastach, rozgrzewka " + Math.Min(_days, 30) + "/30, napis "
                     + s.Length + " znakow; " + probe + ".");
            return s;
        }

        /// <summary>Wczytanie (SyncData): napis odlozony do OnSessionLaunched - w SyncData osad nie szukamy. Brak klucza = stary zapis.</summary>
        internal static void Import(string s)
        {
            _ema.Clear(); _today.Clear(); _acc.Clear(); _oreAcc.Clear(); _keys.Clear(); _days = 0; _lastDay = -1;
            _pending = string.IsNullOrEmpty(s) ? null : s;
            _loaded = _pending != null ? 2 : 1;
        }

        internal static void ImportFailed()
        {
            _ema.Clear(); _today.Clear(); _acc.Clear(); _oreAcc.Clear(); _keys.Clear(); _days = 0; _lastDay = -1; _pending = null; _loaded = 4;
        }

        /// <summary>OnSessionLaunched (albo pierwsza doba / zapis, gdyby przyszly wczesniej): napis -> klucze sesji. Linia "Zlom z nadmiaru (zapis 174b)".</summary>
        internal static void ResolvePending(string why)
        {
            if (_loaded == 1) { _loaded = 3; Log.Info("Zlom z nadmiaru (zapis 174b): " + why + " - brak klucza w zapisie (zapis sprzed 174b) - rozgrzewka od zera (0/30)."); return; }
            if (_loaded == 4) { _loaded = 3; Log.Info("Zlom z nadmiaru (zapis 174b): " + why + " - klucz zapisu nieczytelny - rozgrzewka od zera (0/30)."); return; }
            var s = _pending;
            _pending = null;
            if (string.IsNullOrEmpty(s)) return;
            _loaded = 3;
            Parsed p;
            try { p = Parse(s); }
            catch (Exception e)
            {
                Stumble("ResolvePending(napis)", e);
                Log.Info("Zlom z nadmiaru (zapis 174b): " + why + " - klucz zapisu nieczytelny - rozgrzewka od zera (0/30).");
                return;
            }
            var om = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
            var cache = new Dictionary<string, Settlement>();
            Func<string, Settlement> find = sid =>
            {
                Settlement st;
                if (cache.TryGetValue(sid, out st)) return st;
                try { st = om != null ? om.GetObject<Settlement>(sid) : null; } catch { st = null; }
                cache[sid] = st;
                return st;
            };
            int ok = 0, skipped = 0, ores = 0;
            var towns = new HashSet<Settlement>();
            for (int i = 0; i < p.Sid.Count; i++)
            {
                var st = find(p.Sid[i]);
                if (st == null || !st.IsTown) { skipped++; continue; }
                long k = Key(st, p.Basket[i]);
                _keys[k] = new KeyValuePair<Settlement, int>(st, p.Basket[i]);
                if (p.Ema[i] > 0f) _ema[k] = p.Ema[i];
                if (p.Today[i] > 0f) _today[k] = p.Today[i];
                if (p.Acc[i] > 0f) _acc[k] = p.Acc[i];
                ok++; towns.Add(st);
            }
            for (int i = 0; i < p.OreSid.Count; i++)
            {
                var st = find(p.OreSid[i]);
                if (st == null || st.Town == null || p.Ore[i] <= 0f) { skipped++; continue; }
                _oreAcc[st.Town] = p.Ore[i]; ores++;
            }
            _days = Math.Max(0, Math.Min(9999, p.Days));
            _lastDay = p.LastDay;
            Log.Info("Zlom z nadmiaru (zapis 174b): " + why + " - odtworzono " + ok + " par z " + towns.Count + " miast, ruda ze zlomu w toku w " + ores + " miastach (pominieto " + skipped
                     + " - osady juz nie ma), rozgrzewka " + Math.Min(_days, 30) + "/30, ostatnia doba " + _lastDay + ".");
        }
    }
}
