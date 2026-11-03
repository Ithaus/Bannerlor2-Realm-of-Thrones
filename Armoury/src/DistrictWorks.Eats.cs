using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 323c (Jeff 03.11: "tak i prawdziwa sprzedaz" - prace w Shady Glen 65 tys. placily 2-4%/rok, a 42 doby nic, bo okreg zjadal caly dodatek sam): udzial inwestora
    /// TAKZE w tym, co okreg zjada z dodatku prac (klucz Works Pay District Eats).
    /// Kto placi (zamknieta ekonomia, "nic z niczego"): pod KS okreg zjada Own = min(produkcja, potrzeba) (KsDeliver.Own), a reszte potrzeby (koszyk ZC4) wies kupuje
    /// w miescie z kiesy wsi. Dodatek, ktory okreg zjada (dodatek - dodatek na targ, StartCalibration.WorksMarketOf: WorksAll - WorksMkt), to jedzenie domow okregu,
    /// ktore inaczej musialyby kupic albo go nie miec - jego wlascicielem gospodarczym sa domy okregu, ich kiesa to kiesa wsi. Wies kupuje ten towar od inwestora:
    /// udzial (District Works Share, 0.5) x wartosc dnia zjedzonego dodatku po cenie polki miasta targowego (VillageCrafts.Price - ta sama cena co dodatek na targ i V).
    /// Bez r wozow (r = utarg / towar na targ - strata drogi i targu; zjedzone nie jedzie na targ). Druga polowa zostaje u domow (jedza ja za darmo) - jak przy targu.
    /// Placi kiesa wsi gry okregu (ta sama, z ktorej idzie wyplata przy wozie), najwyzej wolna kiesa ponad skrzynie na czarna godzine (DsVillage.Chest - jak listy
    /// soltysow); brak - nie placi (i nie dlug: brak liczony w linii), zjedzone zostaje zjedzone. Przelew zdarzeniem gry (GiveGoldAction kiesa wsi -> glowa rodu),
    /// nazwany w ksiedze pieniadza (Note169 "inne", jak wyplata z kiesy przy wozie).
    /// Raz na dobe (DistrictWorks.Daily, po KS doby), tylko z SWIEZEGO rachunku KS okregu (bez pamieci 28 dob - wyplata tylko za dzisiejsza produkcje), prace w rachunku
    /// (Inc), droga produkcji (PathOf), wies nie zlupiona, bez wojny rodu z krolestwem okregu (B9 jak przy wozie), wies i rod bez glowy - bez wyplaty. Czesc dodatku
    /// na targ dalej placa wozy (OnCartReturn) - zjedzone i na targ sie nie nakladaja (Ea - Em i Em). Wycena okna (QuotesOf: H = r x Phi + 1 x (1 - Phi)) i "still 12%
    /// on the last coin" (Sensible) licza z tego samego wzoru. Ulamki monety - przeniesienie na rod i okreg (pamiec sesji). Wyjatki per okreg liczone, nic sie nie gasi.
    /// </summary>
    internal static partial class DistrictWorks
    {
        internal static bool EatsOn { get { var s = Settings.Current; return MarketShareOn && s != null && s.WorksPayDistrictEats; } }

        private static readonly Dictionary<string, double> _eCarry = new Dictionary<string, double>(StringComparer.Ordinal);   // okreg|rod -> ulamek monety (sesja)
        private static int _eDist, _eWar, _eNoKs, _eLooted, _eNoPath, _eNoHead, _eStumble, _eStAll;
        private static double _eVal, _ePool;
        private static long _ePaid, _eShort;

        private static void EatsZero() { _eDist = _eWar = _eNoKs = _eLooted = _eNoPath = _eNoHead = _eStumble = 0; _eVal = _ePool = 0; _ePaid = _eShort = 0; }

        internal static void EatsReset() { lock (Gate) { _eCarry.Clear(); EatsZero(); _eStAll = 0; } }

        /// <summary>Oczekiwany udzial dnia w zjedzonym dodatku okregu (przed kiesa wsi) - do kontroli "oczekiwane" i Exp7 wierszy. Ten sam wzor co PayEaten.</summary>
        private static double EatenExpected(string id, DistVal dv, double share)
        {
            if (!EatsOn || dv == null || !dv.Mkt || !dv.Inc || dv.EaPart.Count == 0) return 0;
            double sum = 0;
            lock (Gate)
                foreach (var kv in dv.EaPart)
                {
                    if (!dv.EPart.ContainsKey(kv.Key)) continue;
                    double em; dv.EmPart.TryGetValue(kv.Key, out em);
                    double e = share * Math.Max(0, kv.Value - Math.Max(0, em));
                    if (!(e > 0) || double.IsInfinity(e)) continue;
                    sum += e;
                    int n = Math.Max(1, CountPart(id, kv.Key));
                    foreach (var r in _rows) if (r.Dist == id && r.Part == kv.Key) r.Exp7 += e / n;
                }
            return sum;
        }

        /// <summary>323c: wyplata dnia za dodatek zjedzony w okregu v - z kiesy wsi do glow rodow posiadaczy prac (wedlug jednostek czesci). Nie rzuca.</summary>
        private static void PayEaten(Village v, int today)
        {
            if (v == null || v.Settlement == null || !EatsOn) return;
            string id = v.Settlement.StringId;
            try
            {
                lock (Gate) { bool any = false; foreach (var r in _rows) if (r.Dist == id) { any = true; break; } if (!any) return; }
                if (!PathOf(v)) { lock (Gate) _eNoPath++; return; }
                if (v.VillageState != Village.VillageStates.Normal) { lock (Gate) _eLooted++; return; }
                double mk, em; bool inc;
                var pm = new Dictionary<string, double>(StringComparer.Ordinal); var pa = new Dictionary<string, double>(StringComparer.Ordinal);
                bool ok;
                try { ok = StartCalibration.WorksMarketOf(v, out mk, out em, out inc, pm, pa); } catch { ok = false; inc = false; }
                if (!ok || !inc || pa.Count == 0) { lock (Gate) _eNoKs++; return; }   // tylko swiezy rachunek KS z pracami - bez pamieci starych liczb
                var s = Settings.Current;
                double share = s != null ? Math.Max(0f, Math.Min(1f, s.DistrictWorksShare)) : 0.5;
                // zjedzone wedlug czesci (wartosc dnia, cena polki) i pula wedlug rodow: czesc dzielona na wiersze wedlug jednostek pracujacych (jak E przy wozie)
                var eatPart = new Dictionary<string, double>(StringComparer.Ordinal);
                double eatAll = 0;
                foreach (var kv in pa)
                {
                    double m; pm.TryGetValue(kv.Key, out m);
                    double e = Math.Max(0, kv.Value - Math.Max(0, m));
                    if (!(e > 0) || double.IsNaN(e) || double.IsInfinity(e)) continue;
                    eatPart[kv.Key] = e; eatAll += e;
                }
                if (!(eatAll > 0)) return;
                var byClan = new Dictionary<string, double>(StringComparer.Ordinal);
                var rowsOf = new Dictionary<string, List<KeyValuePair<Row, double>>>(StringComparer.Ordinal);
                lock (Gate)
                {
                    _eDist++; _eVal += eatAll;
                    var unitsPart = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var r in _rows) if (r.Dist == id) { double u; unitsPart.TryGetValue(r.Part, out u); unitsPart[r.Part] = u + r.Eff(today); }
                    foreach (var r in _rows)
                    {
                        if (r.Dist != id) continue;
                        double ep, up;
                        if (!eatPart.TryGetValue(r.Part, out ep) || !unitsPart.TryGetValue(r.Part, out up) || !(up > 0)) continue;
                        double w = share * ep * r.Eff(today) / up;
                        if (!(w > 0)) continue;
                        _ePool += w;
                        double c; byClan.TryGetValue(r.Clan, out c); byClan[r.Clan] = c + w;
                        List<KeyValuePair<Row, double>> l; if (!rowsOf.TryGetValue(r.Clan, out l)) { l = new List<KeyValuePair<Row, double>>(); rowsOf[r.Clan] = l; }
                        l.Add(new KeyValuePair<Row, double>(r, w));
                    }
                }
                double chest = 0;
                try { chest = DsVillage.Chest(v); } catch { chest = 0; }
                if (!(chest >= 0) || double.IsInfinity(chest)) chest = 0;
                long free = Math.Max(0L, (long)Math.Floor(v.Gold - chest));
                string pid = PlayerClanId();
                IFaction df = v.Settlement.MapFaction;
                foreach (var kv in byClan)
                {
                    var clan = kv.Key == VillageClan ? null : ClanOf(kv.Key);
                    var leader = clan != null && !clan.IsEliminated ? clan.Leader : null;
                    if (leader == null || !leader.IsAlive) { lock (Gate) _eNoHead++; continue; }   // wies albo rod bez glowy - zjedzone zostaje u domow
                    IFaction mf = clan.MapFaction;
                    if (mf != null && df != null && mf != df && FactionManager.IsAtWarAgainstFaction(mf, df)) { lock (Gate) _eWar++; continue; }   // B9 jak przy wozie
                    string ck = id + "|" + kv.Key;
                    double carry; lock (Gate) _eCarry.TryGetValue(ck, out carry);
                    double want = kv.Value + Math.Max(0, Math.Min(1, carry));
                    long whole = (long)Math.Floor(want + 1e-9);
                    long pay = Math.Min(whole, free);
                    lock (Gate) { _eCarry[ck] = Math.Max(0, want - whole); if (whole > pay) _eShort += whole - pay; }   // brak w kiesie nie przechodzi (nie dlug)
                    if (pay <= 0) continue;
                    GiveGoldAction.ApplyForSettlementToCharacter(v.Settlement, leader, (int)pay, true);   // kiesa wsi -> glowa rodu (zdarzenie gry)
                    MoneyLedger.Note169(MoneyLedger.N169Other, v.Settlement, -(int)pay);               // kiesa wsi poza tickiem - nazwane ("inne"), jak przy wozie
                    free -= pay;
                    lock (Gate)
                    {
                        _ePaid += pay;
                        // pay na wiersze rodu metoda najwiekszej reszty (jak OnCartReturn) - cale kwoty sumuja sie do pay
                        var l = rowsOf[kv.Key];
                        var sh = new double[l.Count]; var w = new long[l.Count]; long left = pay;
                        for (int i = 0; i < l.Count; i++) { sh[i] = pay * l[i].Value / kv.Value; w[i] = Math.Max(0L, (long)Math.Floor(sh[i] + 1e-9)); left -= w[i]; }
                        var ord = new List<int>(); for (int i = 0; i < l.Count; i++) ord.Add(i);
                        ord.Sort((a, b) => (sh[b] - w[b]).CompareTo(sh[a] - w[a]));
                        for (int k = 0; k < ord.Count && left > 0; k++, left--) w[ord[k]]++;
                        for (int i = 0; i < l.Count; i++)
                        {
                            var r = l[i].Key;
                            r.Out += w[i]; r.Paid7 += sh[i]; r.DEat += w[i]; r.EatAll += w[i];
                            if (pid != null && r.Clan == pid) r.AddPaid(today, w[i]);   // doba wyplaty (Fiefs, widok finansow) - jak przy wozie
                        }
                    }
                    if (pid != null && kv.Key == pid) ViewDirty();
                }
            }
            catch (Exception e) { lock (Gate) { _eStumble++; _eStAll++; } Stumble("PayEaten", e); }
        }

        /// <summary>323c: linia doby - zjedzone w okregu (wartosc, pula, wyplacone z kiesy, brak) i wedlug prac: z targu / ze zjedzonego (doba; od wczytania). Nie rzuca.</summary>
        private static void EatsLine(int day)
        {
            try
            {
                var sb = new StringBuilder(800);
                lock (Gate)
                {
                    if (_rows.Count == 0) { EatsZero(); return; }
                    bool any = _eDist + _eWar + _eNoKs + _eLooted + _eNoPath + _eNoHead + _eStumble > 0 || day % 7 == 0;
                    var per = new StringBuilder();
                    int shown = 0;
                    foreach (var r in _rows)
                    {
                        if (r.DMk > 0 || r.DEat > 0) any = true;
                        if (shown >= 12) continue;
                        shown++;
                        string pn; try { var st = Settlement.Find(r.Dist); pn = st != null && st.Village != null ? PartName(st.Village, r.Part) : r.Dist + "/" + r.Part; } catch { pn = r.Dist + "/" + r.Part; }
                        per.Append(per.Length > 0 ? "; " : "").Append(pn).Append(r.Clan == PlayerClanId() ? " [gracz]" : " [" + r.Clan + "]")
                           .Append(" targ ").Append(Math.Round(r.DMk).ToString("0", Inv)).Append(" / zjedzone ").Append(Math.Round(r.DEat).ToString("0", Inv))
                           .Append(" (od wczytania ").Append(Math.Round(r.MkAll).ToString("0", Inv)).Append(" / ").Append(Math.Round(r.EatAll).ToString("0", Inv)).Append(")");
                    }
                    if (any)
                        sb.Append(P).Append("zjedzone w okregu (323c, klucz Works Pay District Eats ").Append(EatsOn ? "TAK" : "NIE").Append(") dzien ").Append(day)
                          .Append(" | okregow z wycena ").Append(_eDist).Append(", dodatek zjedzony ").Append(_eVal.ToString("0.0", Inv)).Append(" zl/d (cena polki), pula (udzial) ")
                          .Append(_ePool.ToString("0.0", Inv)).Append(", wyplacone z kiesy wsi ").Append(_ePaid).Append(", brak w wolnej kiesie (ponad skrzynie) ").Append(_eShort)
                          .Append(" | bez wyplaty: wojna ").Append(_eWar).Append(", bez swiezego rachunku KS z pracami ").Append(_eNoKs).Append(", wies zlupiona ").Append(_eLooted)
                          .Append(", bez drogi produkcji ").Append(_eNoPath).Append(", rod bez glowy ").Append(_eNoHead)
                          .Append(" | wedlug prac (z targu / ze zjedzonego, doba; od wczytania; pokazanych ").Append(shown).Append(" z ").Append(_rows.Count).Append("): ").Append(per.Length > 0 ? per.ToString() : "-")
                          .Append(" | potkniecia ").Append(_eStumble).Append(" (od wczytania ").Append(_eStAll).Append(").");
                    foreach (var r in _rows) { r.DMk = 0; r.DEat = 0; }
                    EatsZero();
                }
                if (sb.Length > 0) Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("EatsLine", e); }
        }
    }
}
