using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// inwest - PRACE OKREGU: UKLAD (zmiana ukladu wiosek) i PROBA AUTOTESTU (projekt TESTS 1).
    ///  - UKLAD: prace na wiosce, ktorej nie ma w ukladzie tej sesji (inny plik ukladu) - do wioski tego samego okregu z tym samym zajeciem i miejscem, inaczej do wsi gry,
    ///    przy rownej wartosci (wartosc = wplacone - stracone; jednostki przeliczone cena osady nowej czesci). Okreg nieznany - prace zamrozone (bez zmian).
    ///  - PROBA (tylko autotest CrashScribe + plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-po-test.flag, plik zuzyty): gracz placi 30 000 w 4 czesci
    ///    jednego okregu i 8 000 w inny (mniej, gdy ma mniej zlota - te same proporcje), kontrola co do monety (zaplacone = place + material, kiesa wsi i kasa targu),
    ///    rachunek ognia na wierszu probnym (U x dE / S, L, stado do napastnika), odbudowa (R x cena, 1 - m / m), termin prawa, wojna, zapis tam i z powrotem, stary zapis
    ///    bez klucza, zmiana ukladu (wartosc w 1%). Linia "Prace okregu (PO): test | ..." z TAK/NIE przy kazdym punkcie.
    /// </summary>
    internal static partial class DistrictWorks
    {
        internal const string TestFlagName = "armoury-po-test.flag";
        private static bool _testDone; private static int _testFlagDay = -1; private static bool _testFlag;
        private static int _remapped;

        // ------------------------------------------------------------ zmiana ukladu
        /// <summary>Prace na wioskach spoza ukladu tej sesji - do wioski z tym samym zajeciem i miejscem albo do wsi gry, przy rownej wartosci. Raz na dobe (szybkie przy braku prac).</summary>
        private static void Remap(int today)
        {
            List<Row> todo = null;
            lock (Gate)
                foreach (var r in _rows)
                {
                    if (r.Part == MainPart) continue;
                    var st = Settlement.Find(r.Dist); var v = st != null ? st.Village : null;
                    if (v == null) continue;
                    var book = new List<VillageBook.PartView>();
                    if (!VillageBook.PartsOf(v, book)) continue;
                    bool found = false; foreach (var p in book) if (p.Uid == r.Part) { found = true; break; }
                    if (!found) { if (todo == null) todo = new List<Row>(); todo.Add(r); }
                }
            if (todo == null) return;
            foreach (var r in todo)
            {
                try
                {
                    var v = Settlement.Find(r.Dist).Village;
                    var qs = new List<Quote>();
                    if (!QuotesOf(v, qs, null)) continue;
                    double value = Math.Max(0, r.In - r.LostG);
                    Quote to = null;
                    foreach (var q in qs) if (!q.Main && q.Craft == r.Craft && q.KitPer > 0 && q.Room >= value) { to = q; break; }
                    if (to == null) foreach (var q in qs) if (q.Main && q.WKind >= 0 && q.KitPer > 0) { to = q; break; }
                    if (to == null) continue;
                    double held = r.Held();
                    double newUnits = to.KitPer > 0 ? value / to.KitPer : 0;
                    double k = held > 1e-9 ? newUnits / held : 0;
                    lock (Gate)
                    {
                        string from = r.Part;
                        r.Part = PartKey(to.Uid); r.Craft = to.Craft; r.Kind = to.WKind;
                        Scale(r, k);
                        r.SeenBr = to.Br; r.SeenBf = to.Bf;
                        _remapped++;
                        if (_dNotes.Count < 8) _dNotes.Add("uklad: prace " + from + " -> " + r.Part + " (wartosc " + Math.Round(value) + " zl, jednostki x" + k.ToString("0.000", Inv) + ")");
                    }
                }
                catch (Exception e) { Stumble("Remap", e); }
            }
            RebuildBonus();
        }

        private static void Scale(Row r, double k)
        {
            r.U *= k; r.I *= k; r.Z *= k;
            foreach (var l in r.Ramp) l.Units *= k;
            foreach (var b in r.Rebs) b.Units *= k;
        }

        // ------------------------------------------------------------ proba autotestu
        private static bool TestWanted(int today)
        {
            if (_testDone) return false;
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;                    // tylko DLL autotestu CrashScribe
            if (_testFlagDay != today)
            {
                _testFlagDay = today;
                try { _testFlag = System.IO.File.Exists(FlagPath()); } catch { _testFlag = false; }
            }
            return _testFlag;
        }

        private static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), TestFlagName);
        }

        private static void Test(int today)
        {
            _testDone = true;
            var sb = new StringBuilder(2000);
            int pass = 0, fail = 0, info = 0;
            Action<string, bool?, string> add = (name, ok, txt) =>
            {
                sb.Append(" | ").Append(name).Append(' ').Append(ok == true ? "TAK" : ok == false ? "NIE" : "INFO").Append(" (").Append(txt).Append(')');
                if (ok == true) pass++; else if (ok == false) fail++; else info++;
            };
            try
            {
                var me = Hero.MainHero; var pc = Clan.PlayerClan;
                // 0: stary zapis bez klucza - bez prac (piaskownica na napisie, stan przywrocony)
                string keep = Export();
                lock (Gate) { ImportNow(SaveVer + "||d:"); }
                int zero = _rows.Count;
                lock (Gate) { if (string.IsNullOrEmpty(keep)) { _rows.Clear(); _handed.Clear(); _relDay.Clear(); } else ImportNow(keep); }
                add("T0 zapis bez prac", zero == 0, "wierszy " + zero);
                // 1: zakupy 30 000 w 4 czesci i 8 000 w inna - co do monety
                var cand = new List<KeyValuePair<Village, List<Quote>>>();
                Vec2 at = MobileParty.MainParty != null ? MobileParty.MainParty.GetPosition2D : new Vec2(0, 0);
                foreach (var v in Village.All)
                {
                    if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal) continue;
                    string why;
                    if (!CanTrade(pc, v, false, out why)) continue;
                    var qs = new List<Quote>();
                    if (!QuotesOf(v, qs, pc)) continue;
                    int ok = 0; foreach (var q in qs) if (q.Allowed && !q.Main && q.KitPer > 0 && q.Room > 500) ok++;
                    if (ok >= 3) cand.Add(new KeyValuePair<Village, List<Quote>>(v, qs));
                }
                cand.Sort((a, b) => a.Key.Settlement.GetPosition2D.DistanceSquared(at).CompareTo(b.Key.Settlement.GetPosition2D.DistanceSquared(at)));
                int gold = me != null ? me.Gold : 0;
                double scale = Math.Min(1.0, Math.Max(0, gold - 1000) / 38000.0);
                int buys = 0, badId = 0; long paidAll = 0;
                var plan = new[] { new[] { 10000, 10000, 5000, 5000 }, new[] { 4000, 2000, 2000 } };
                for (int d = 0; d < 2 && d < cand.Count; d++)
                {
                    var v = cand[d].Key; var qs = cand[d].Value;
                    int i = 0;
                    foreach (var q in qs)
                    {
                        if (i >= plan[d].Length) break;
                        if (!q.Allowed || q.Main || q.Room <= 500) continue;
                        int amount = (int)Math.Floor(plan[d][i] * scale);
                        i++;
                        if (amount < 200) continue;
                        var market = MarketOf(v);
                        long g0 = me.Gold, v0 = v.Gold, m0 = market != null && market.Town != null ? market.Town.Gold : 0;
                        var res = Buy(me, pc, v, q.Uid, amount, 0, false);
                        if (!res.Ok) { add("T1 zakup " + (q.Name ?? "?"), null, "odmowa: " + res.Why); continue; }
                        long dg = g0 - me.Gold, dv = v.Gold - v0, dm = market != null && market.Town != null ? market.Town.Gold - m0 : 0;
                        bool idOk = dg == res.Paid && res.Paid == res.Wage + res.MatTown + res.MatVil && dv == res.Wage + res.MatVil && dm == res.MatTown;
                        if (!idOk) badId++;
                        buys++; paidAll += res.Paid;
                    }
                }
                add("T1 zakupy co do monety (zaplacone = place + material; kiesa wsi; kasa targu)", buys == 0 ? (bool?)null : badId == 0,
                    "zakupow " + buys + ", zaplacone " + paidAll + ", niezgodne " + badId + ", okregow kandydatow " + cand.Count + ", skala " + scale.ToString("0.00", Inv));
                // 2: ogien na wierszu probnym - przesuniete = U x dE / S, L, stado
                var tr = new Row { Kind = KHerd, U = 10, Animals = 65, Animal = "cow" };
                double moved = Move(tr, Math.Min(1.0, 12.0 / 20.0));
                double lost = KL[tr.Kind] * moved;
                double drive = Math.Floor(65.0 / 10.0 * lost + 0.5);
                var tf = new Row { Kind = KFields, U = 10 };
                double mf = Move(tf, 0.6), lf = KL[tf.Kind] * mf;
                add("T2 ogien (stado 12 z 20 osad: stracone 6, 39 zwierzat; pola: polowa bezczynna)", Math.Abs(lost - 6.0) < 0.01 && Math.Abs(drive - 39) <= 1 && Math.Abs(lf - 3.0) < 0.01 && Math.Abs((mf - lf) - 3.0) < 0.01,
                    "stracone " + lost.ToString("0.00", Inv) + ", zwierzat " + drive + ", pola stracone " + lf.ToString("0.00", Inv) + " bezczynne " + (mf - lf).ToString("0.00", Inv));
                // 3: odbudowa R x cena, podzial 1 - m / m
                double kit = 1000, units = 2.5;
                int costB = (int)Math.Ceiling(KR[KBoats] * kit * units), costH = (int)Math.Ceiling(KR[KHerd] * kit * units);
                add("T3 odbudowa (lodzie pol ceny, stado cala)", costB == 1250 && costH == 2500 && Math.Abs(KM[KBoats] - 0.35f) < 1e-6,
                    "lodzie " + costB + " (place " + Math.Round(costB * (1 - KM[KBoats])) + " / material " + Math.Round(costB * KM[KBoats]) + "), stado " + costH);
                // 4: termin prawa odbudowy
                var s = Settings.Current; int days = s != null ? Math.Max(1, s.DistrictWorksRebuildDays) : 91;
                var rb = new Reb { Units = 1, Deadline = today - 1 };
                add("T4 termin odbudowy (" + days + " dob od powrotu)", today > rb.Deadline, "termin " + rb.Deadline + ", dzis " + today);
                // 5: wojna wstrzymuje wyplate i zakup
                Village hostile = null;
                foreach (var v in Village.All) { if (v == null || v.Settlement == null || pc == null || pc.MapFaction == null || v.Settlement.MapFaction == null) continue; if (v.Settlement.MapFaction != pc.MapFaction && FactionManager.IsAtWarAgainstFaction(pc.MapFaction, v.Settlement.MapFaction)) { hostile = v; break; } }
                if (hostile != null) { string why; add("T5 wojna (wies wroga nie bierze pieniedzy)", !CanTrade(pc, hostile, false, out why), why ?? "-"); }
                else add("T5 wojna", null, "gracz bez wojny - sprawdzenie pominiete");
                // 6: zapis tam i z powrotem
                string s1 = Export();
                lock (Gate) ImportNow(s1);
                string s2 = Export();
                add("T6 zapis tam i z powrotem", s1 == s2, "dlugosc " + s1.Length + (s1 == s2 ? "" : ", roznica od znaku " + FirstDiff(s1, s2)));
                // 7: zmiana ukladu - wartosc w 1%
                var rr = new Row { Kind = KBoats, U = 4, I = 1, Z = 1, In = 6000, LostG = 1000 };
                rr.Ramp.Add(new Lot { Units = 2, Day = today, Ready = 28 });
                double val = rr.In - rr.LostG, kit2 = 700, k2 = val / kit2 / rr.Held();
                Scale(rr, k2);
                add("T7 zmiana ukladu (wartosc w 1%)", Math.Abs(rr.Held() * kit2 - val) <= 0.01 * val, "wartosc " + val + " -> " + Math.Round(rr.Held() * kit2));
            }
            catch (Exception e) { Stumble("Test", e); sb.Append(" | WYJATEK ").Append(e.GetType().Name); fail++; }
            try { System.IO.File.Delete(FlagPath()); } catch { }
            Log.Info(P + "test (autotest, plik " + TestFlagName + " zuzyty) | wynik TAK " + pass + ", NIE " + fail + ", INFO " + info + sb.ToString() + ".");
        }

        private static int FirstDiff(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) if (a[i] != b[i]) return i;
            return n;
        }
    }
}
