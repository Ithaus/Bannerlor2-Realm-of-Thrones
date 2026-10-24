using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m113-pobor TEST (sonda autotestu): tylko z plikiem Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-pobor.flag i DLL autotestu (LogGate 1/2); w zwyklej
    /// grze nic nie robi. Pierwsza doba: kolejnosc opcji menu, trzy wioski blisko gracza (najwieksza, przysiolek, kraina z hamulcem albo obce krolestwo bez wojny) - werbunek
    /// ta sama droga co okno (LevyDo) i kontrola OK/BLAD: druzyna +k, gracz -suma, kiesa wsi +suma, DOM -zdjeci (+-0.01), dom wioski -k (+-1), reszta okregu 0 (+-0.5),
    /// pula -k, cena wedlug etapu, sztuki DTE tieru 1, VillOut regionu +zdjeci; wymuszone odmowy (wojna, spalona, Straz). Kolejne doby: przysiolek oprozniany do konca
    /// (etapy, odmowa 'koniec dnia'), 0 &lt;= pula &lt;= pulap, brak rak, obroncy &lt;= pula. Po wczytaniu z wpisami - tylko kontrole.
    /// n36 (P3, Jeff 24.10 "przyrost z realnego wzrostu tej wioski"): odnowa doby - dopisanie do Acc = P x max(0, g) / 100 / dni roku, g z PopulationLaw.RateOf TEJ
    /// wsi okregu (sonda pyta model sama, bez pamieci doby LvRate; RateOf false - zapas SideRecruitYouthPercent); odnowa miesiaca - rozliczone Acc = suma dob sondy
    /// (tylko pelny miesiac widziany przez sonde, inaczej INFO); przyrost mlodych w dobie rozliczenia = min(pulap, pula wczoraj + rozliczone Acc) - pula wczoraj.
    /// Test n36: dopoki zaden werbunek sondy nie przeszedl kontroli - pelna druzyna albo za malo zlota nie zatrzymuja sondy (zdjeci ludzie / dotacja testu, linia w logu,
    /// poza bilansem); odmowa po werbunku - kazdy prawidlowy powod bramki to OK (z powodem), BLAD tylko werbunek ponad dzis albo wyjatek; linia doby konczy sie TAK / NIE / NIEPOKRYTY.
    /// Po recenzji: przysiolek dostaje miejsce i dotacje kazdego dnia (inaczej pelna druzyna konczy werbunek przed limitem dnia), odmowa ponad dzis liczy sie
    /// tylko po werbunku przysiolka tej doby i tylko z powodow, ktore moga stac za oproznionym przysiolkiem (druzyna, zloto - INFO); bez takiej odmowy - NIEPOKRYTY.
    /// 113b (bieg 113b-pobor na zapisie Jeffa: kazda proba "gotowych 0 (zaufanie 0)" - obcy dla notabli okregu, VolunteerModel / BK CalculateMaximumRecruitmentIndex):
    /// warunek testu - przy wyborze wiosek (i przysiolka w kolejnych dobach) sonda podnosi relacje gracza z notablami wsi okregu po +10 do zaufania &gt;= 3 albo
    /// maksimum modelu (linia "TEST warunek testu: relacja z ..."); okreg z zaufaniem 0 mimo relacji 100 (prawo BK, kultura) - wioska pominieta z powodem, nastepna.
    /// Linia doby TAK tylko przy co najmniej 1 werbunku z kontrolami, 1 odmowie ponad dzis, 1 kontroli odnowy doby i 1 kontroli przyrostu mlodych (doba rozliczenia).
    /// </summary>
    internal static partial class SideVillageVisit
    {
        private static int _pbFlag = -1;
        private static int _pbDay0 = -1;
        private static bool _pbAfterLoad, _pbLoaded;   // _pbLoaded - wczytany zapis mial wpisy puli (LevyImport); wpisy z bitew przed pierwsza doba nie zmieniaja trybu
        private static string _pbA, _pbB, _pbC;
        private static string _pbForce;   // wymuszona odmowa w LevyGate (tylko sonda)
        private static int _pbOk, _pbBad;
        private static int _pbRec;   // test n36: werbunki sondy z kontrolami (0 - sonda nic nie sprawdzila: NIEPOKRYTY)
        private static int _pbOver;  // po recenzji: odmowy ponad dzis sprawdzone po werbunku przysiolka (0 - limit dnia nie sprawdzony: NIEPOKRYTY)
        private static int _pbYouth;   // 113b: kontrole odnowy doby (ProbeYouth: dopisanie wobec RateOf tej wsi okregu) - 0: NIEPOKRYTY
        private static int _pbSettle;  // 113b: kontrole przyrostu mlodych w dobie rozliczenia (co 28 dob) - 0: NIEPOKRYTY; recenzja 113b: tylko przyrost ponad tolerancje z kontrola odnowy miesiaca
        private const int PbTrustWant = 3;   // 113b: zaufanie, do ktorego warunek testu podnosi relacje z notablami okregu (albo maksimum modelu)
        private sealed class PbCand { public string Uid; public Settlement St; public double P; public int Sc, Ord; }
        // test n36: prawidlowe odmowy po werbunku - kody LevyGate i powody LevyDo (miejsce nie wystepuje: sonda wola bramke jak wioska gracza).
        // Po recenzji: tylko powody, ktore moga stac za oproznionym przysiolkiem (limit dnia, pula, podloga DOM) i bramki przed pula (wojna, rabunek, ...);
        // druzyna, zloto i nic do wziecia przychodza tylko przy gotowych >= 1 - werbunek nie oproznil przysiolka, wiec limit nie byl sprawdzony (INFO).
        private static readonly string[] PbRefusals = { "koniec dnia", "pula", "podloga", "DOM na podlodze", "rabunek", "wojna", "zaciag", "Straz", "spalona", "zaufanie" };
        private static readonly string[] PbNotTested = { "druzyna", "zloto", "nic do wziecia" };
        private static readonly Dictionary<string, double> _pbPrev = new Dictionary<string, double>(StringComparer.Ordinal);
        // n36 (P3): suma dobowych dopisan policzona przez sonde od ostatniego rozliczenia wpisu (nowy wpis - od nowa)
        private sealed class PbAcc { public object R; public double Sum; public int Days; }
        private static readonly Dictionary<string, PbAcc> _pbAcc = new Dictionary<string, PbAcc>(StringComparer.Ordinal);
        // recenzja 113b: pobor gry z puli wpisu od ostatniej linii sondy (LevyNoteDistrictDraw - rekruci notabli, LevyNoteLost - polegli obroncy) - { przed rozliczeniem, po nim }
        private static readonly Dictionary<string, double[]> _pbDraw = new Dictionary<string, double[]>(StringComparer.Ordinal);

        private static bool ProbeFlag()
        {
            if (_pbFlag >= 0) return _pbFlag == 1;
            _pbFlag = 0;
            try
            {
                int mode = LogGate.Mode;
                if (mode != 1 && mode != 2) return false;
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), "armoury-pobor.flag"))) _pbFlag = 1;
            }
            catch { }
            return _pbFlag == 1;
        }

        private static void LevyProbeReset() { _pbDay0 = -1; _pbAfterLoad = _pbLoaded = false; _pbA = _pbB = _pbC = null; _pbForce = null; _pbOk = _pbBad = 0; _pbRec = 0; _pbOver = 0; _pbYouth = _pbSettle = 0; _pbPrev.Clear(); _pbAcc.Clear(); _pbDraw.Clear(); }

        /// <summary>Recenzja 113b: ubytek z puli wpisu r poza sonda (gone - ile naprawde zeszlo), osobno przed rozliczeniem tej doby i po nim. Tylko w czasie sondy. Nie rzuca.</summary>
        private static void PbNoteDraw(LvRec r, double gone)
        {
            try
            {
                if (_pbDay0 < 0 || r == null || r.Uid == null || !(gone > 0.0)) return;
                double[] a;
                if (!_pbDraw.TryGetValue(r.Uid, out a)) _pbDraw[r.Uid] = a = new double[2];
                a[r.SettleDay == Today0() ? 1 : 0] += gone;
            }
            catch { }
        }

        private static void PbCheck(StringBuilder sb, string what, bool ok, string val)
        {
            if (ok) _pbOk++; else _pbBad++;
            sb.Append(" | ").Append(what).Append(' ').Append(val).Append(ok ? " OK" : " BLAD");
        }

        private static void LevyProbeDaily(int today)
        {
            if (!ProbeFlag() || !LevyKey || Campaign.Current == null || MobileParty.MainParty == null || Hero.MainHero == null) return;
            if (_pbDay0 < 0)
            {
                _pbDay0 = today;
                _pbAfterLoad = _pbLoaded;
                if (_pbAfterLoad)
                {
                    int i = 0;
                    foreach (var r in _lv.Values) { if (i == 0) _pbA = r.Uid; else if (i == 1) _pbB = r.Uid; else if (i == 2) _pbC = r.Uid; i++; }
                    Log.Info(LP + "TEST start po wczytaniu - wpisy puli " + _lv.Count + ", sonda tylko sprawdza (" + (_pbA ?? "-") + ", " + (_pbB ?? "-") + ", " + (_pbC ?? "-") + ").");
                }
                else ProbeStart();
                return;
            }
            ProbeDay(today - _pbDay0, today);
        }

        private static string MenuOrder(string menu)
        {
            try
            {
                var gm = Campaign.Current.GameMenuManager.GetGameMenu(menu);
                if (gm == null) return null;
                var l = new List<string>();
                foreach (var o in gm.MenuOptions) if (o != null) l.Add(o.IdString);
                return string.Join(",", l.ToArray());
            }
            catch { return null; }
        }

        private static void ProbeStart()
        {
            var sb = new StringBuilder(600);
            sb.Append(LP).Append("TEST start");
            string a = MenuOrder(Menu) ?? "-", f = MenuOrder(SideVillageFief.Menu) ?? "-";
            var al = new List<string>(a.Split(',')); var fl = new List<string>(f.Split(','));
            int ir = al.IndexOf(LevyOption), it = al.IndexOf("arm_sidevil_trade");
            PbCheck(sb, "menu arm_sidevil [" + a + "] pobor przed Trade", ir >= 0 && it == ir + 1, ir + "/" + it);
            int fr = fl.IndexOf("arm_side_fief_recruit"), fv = fl.IndexOf("arm_side_fief_leave");
            PbCheck(sb, "menu arm_side_fief [" + f + "] pobor przed Leave", fr >= 0 && fv == fr + 1, fr + "/" + fv);
            Log.Info(sb.ToString());
            // wybor wiosek
            var recs = VillageBook.LayoutRecords();
            if (recs == null) { Log.Info(LP + "TEST: uklad wiosek nieznany - sonda pominieta."); return; }
            var p = MobileParty.MainParty.GetPosition2D;
            var near = new List<KeyValuePair<float, MapVillageRecord>>();
            foreach (var r in recs) if (r != null && r.Uid != null) { float d = p.Distance(new Vec2(r.X, r.Y)); if (d <= 150f) near.Add(new KeyValuePair<float, MapVillageRecord>(d, r)); }
            near.Sort((x, y) => x.Key.CompareTo(y.Key));
            var mf = Hero.MainHero.MapFaction;
            var cand = new List<PbCand>();
            for (int i = 0; i < near.Count && i < 120; i++)
            {
                var r = near[i].Value; var st = DistOf(r);
                if (st == null || AtWar(st) || st.Village.VillageState != Village.VillageStates.Normal || WatchNow(st)) continue;
                VillageBook.PartView me; double h, n;
                if (!PartOf(st, r.Uid, out me, out h, out n) || Burned(me)) continue;
                int sc = BrakedSafe(st) ? 2 : (mf != null && st.MapFaction != null && !ReferenceEquals(mf, st.MapFaction) ? 1 : 0);
                cand.Add(new PbCand { Uid = r.Uid, St = st, P = me.Home + me.Away, Sc = sc, Ord = cand.Count });
            }
            // 113b: kazda rola bierze pierwsza wioske ze swojej kolejnosci (najwieksza - najwiecej ludzi, przysiolek - najmniej od 30, hamulec / obce krolestwo - najblizsza),
            // w ktorej warunek testu (PbLiftTrust - relacja z notablami okregu) daje zaufanie >= 1; okreg bez zaufania mimo relacji - jego wioski pominiete z powodem
            var used = new HashSet<string>(StringComparer.Ordinal); var dead = new HashSet<Settlement>();
            var la = new List<PbCand>(cand); la.Sort((x, y) => x.P != y.P ? y.P.CompareTo(x.P) : x.Ord.CompareTo(y.Ord));
            var lb = cand.FindAll(x => x.P >= 30); lb.Sort((x, y) => x.P != y.P ? x.P.CompareTo(y.P) : x.Ord.CompareTo(y.Ord));
            var lc = cand.FindAll(x => x.Sc > 0); lc.Sort((x, y) => x.Sc != y.Sc ? y.Sc.CompareTo(x.Sc) : x.Ord.CompareTo(y.Ord));
            var ca = PbPick("najwieksza", la, used, dead); var cb = PbPick("przysiolek", lb, used, dead); var cc = PbPick("hamulec/obca", lc, used, dead);
            _pbA = ca != null ? ca.Uid : null; _pbB = cb != null ? cb.Uid : null; _pbC = cc != null ? cc.Uid : null;
            int cScore = cc != null ? cc.Sc : -1;
            Log.Info(LP + "TEST wioski: najwieksza " + (_pbA ?? "-") + " (ludzi " + (ca != null ? ca.P.ToString("0", Inv) : "-") + "), przysiolek " + (_pbB ?? "-") + " (" + (cb != null ? cb.P.ToString("0", Inv) : "-")
                     + "), hamulec/obca " + (_pbC ?? "-") + " (rodzaj " + (cScore == 2 ? "hamulec" : cScore == 1 ? "obce krolestwo" : "-") + "), kandydatow " + near.Count + ", po filtrach " + cand.Count + ".");
            ProbeRecruit("najwieksza", _pbA, 5);
            var rb = ProbeRecruit("przysiolek", _pbB, int.MaxValue);
            ProbeOverToday(0, _pbB, rb);   // po recenzji: limit dnia sprawdzony juz w pierwszej dobie (przysiolek oprozniony do konca dnia)
            ProbeRecruit(cScore == 2 ? "hamulec" : "obca", _pbC, 3);
            // wymuszone odmowy
            if (_pbA != null)
            {
                var st = DistOf(Rec(_pbA));
                foreach (var w in new[] { "wojna", "spalona", "Straz" })
                {
                    _pbForce = w;
                    bool hide; string code; LvQuote q;
                    string why;
                    try { _lvQ = null; why = LevyGate(st, _pbA, true, out hide, out code, out q); } finally { _pbForce = null; _lvQ = null; }
                    var s2 = new StringBuilder(LP + "TEST odmowa wymuszona " + w + ": '" + (why ?? "-") + "'");
                    PbCheck(s2, "kod", code == w, code ?? "-");
                    Log.Info(s2.ToString());
                }
            }
            Log.Info(LP + "TEST start - werbunkow sondy z kontrolami " + _pbRec + ", odmow ponad dzis po werbunku " + _pbOver
                     + (_pbRec == 0 || _pbOver == 0 ? " - NIEPOKRYTY (sprobuje w kolejnych dobach przysiolkiem)" : "") + ".");
        }

        /// <summary>113b: pierwsza wioska z kolejnosci roli, w ktorej zaufanie po warunku testu (PbLiftTrust) &gt;= 1. Okreg z zaufaniem 0 mimo relacji - jego wioski
        /// pomijane (te same notable, ten sam model). Najwyzej 6 okregow na role. null - brak wioski (werbunek pisze "brak wioski - INFO").</summary>
        private static PbCand PbPick(string role, List<PbCand> order, HashSet<string> used, HashSet<Settlement> dead)
        {
            int tried = 0;
            foreach (var c in order)
            {
                if (used.Contains(c.Uid) || dead.Contains(c.St)) continue;
                if (tried++ >= 6) { Log.Info(LP + "TEST " + role + ": 6 okregow z zaufaniem 0 mimo warunku testu - dalsze wioski pominiete INFO."); break; }
                int idx = PbLiftTrust(c.St, c.Uid, role);
                if (idx >= 1) { used.Add(c.Uid); return c; }
                dead.Add(c.St);
                Log.Info(LP + "TEST " + role + " " + NameOf(Rec(c.Uid)) + " (" + c.Uid + ", okreg " + Txt(c.St.Name) + ") pominieta: zaufanie 0 po warunku testu - " + PbTrustWhy(c.St) + "; nastepna wioska.");
            }
            return null;
        }

        /// <summary>113b: warunek testu - zaufanie wioski uid (wies okregu st) co najmniej PbTrustWant: relacja gracza z notablami okregu (na miejscu, soltys pierwszy)
        /// po +10 do 100 (ChangeRelationAction.ApplyPlayerRelation bez krewnych i okienka; mnoznik gry dal mniej niz krok - Hero.SetPersonalRelation na cel), po kazdym
        /// kroku TrustIdx od nowa i linia "TEST warunek testu: relacja z ...". Tylko sonda (flaga + LogGate 1/2). Wycena (_lvQ) uniewazniona - nastepna liczy nowe
        /// zaufanie. Zwraca zaufanie po zmianach (0 - relacja nic nie daje: prawo BK, kultura, wojna).</summary>
        private static int PbLiftTrust(Settlement st, string uid, string role)
        {
            if (st == null || Hero.MainHero == null) return 0;
            int idx = TrustIdx(st, uid);
            if (idx >= PbTrustWant) return idx;
            var me = Hero.MainHero;
            var ns = new List<Hero>();
            if (st.Notables != null) foreach (var n in st.Notables) if (n != null && n.IsAlive && n.CurrentSettlement == st) ns.Add(n);
            ns.Sort((x, y) => (y.IsHeadman ? 1 : 0).CompareTo(x.IsHeadman ? 1 : 0));
            int idx0 = idx, steps = 0;
            foreach (var n in ns)
            {
                while (idx < PbTrustWant && steps < 40)
                {
                    int a = (int)n.GetRelationWithPlayer();
                    if (a >= 100) break;
                    int b = Math.Min(100, a + 10);
                    string how = "ApplyPlayerRelation";
                    try { ChangeRelationAction.ApplyPlayerRelation(n, b - a, false, false); } catch (Exception e) { LvStumble("ProbeTrust", e); }
                    int r = (int)n.GetRelationWithPlayer();
                    if (r < b)
                    {
                        // mnoznik gry (charyzma, cechy) dal mniej niz krok - wprost na cel
                        try { me.SetPersonalRelation(n, b); how = "SetPersonalRelation, gra dala " + r; } catch (Exception e) { LvStumble("ProbeTrust.Set", e); }
                        r = (int)n.GetRelationWithPlayer();
                    }
                    _lvQ = null;
                    int i1 = TrustIdx(st, uid);
                    steps++;
                    Log.Info(LP + "TEST warunek testu: relacja z " + Txt(n.Name) + " (" + NameOf(Rec(uid)) + ") " + a + " -> " + r + ", zaufanie " + idx + " -> " + i1
                             + " (" + how + ", rola " + role + ", okreg " + Txt(st.Name) + ", tylko ten test).");
                    idx = i1;
                    if (r <= a) break;   // relacja nie rosnie - nastepny notabl
                }
                if (idx >= PbTrustWant) break;
            }
            _lvQ = null;
            if (idx >= 1 && idx < PbTrustWant)
                Log.Info(LP + "TEST warunek testu: " + NameOf(Rec(uid)) + " zaufanie " + idx0 + " -> " + idx + " - ponizej " + PbTrustWant + ", maksimum modelu przy relacji z notablami okregu ("
                         + PbTrustWhy(st) + "), werbunek mozliwy INFO.");
            return idx;
        }

        private static string PbTrustWhy(Settlement st)
        {
            var c = Campaign.Current;
            var m = c != null && c.Models != null ? c.Models.VolunteerModel : null;
            string mn = m != null ? m.GetType().Name : "-";
            int nn = 0;
            if (st != null && st.Notables != null) foreach (var n in st.Notables) if (n != null && n.IsAlive && n.CurrentSettlement == st) nn++;
            return "model " + mn + (mn.StartsWith("BK", StringComparison.Ordinal) ? " (Banner Kings: prawo poboru tytulu okregu - wasalstwo / hidage dla obcych, kultura, perki)" : "")
                   + ", notabli na miejscu " + nn + (nn == 0 ? " (bez notabli zaufanie 2)" : "") + " - tego gracz relacja nie zmieni";
        }

        /// <summary>Test n36: miejsce w druzynie dla sondy - zdejmuje z druzyny gracza n zdrowych zwyklych ludzi (najnizszy tier, najliczniejszy rodzaj), linia w logu.
        /// Tylko sonda, dopoki zaden werbunek nie przeszedl kontroli, i werbunki przysiolka (kazda doba). Zwraca zdjetych.</summary>
        private static int PbFreeRoom(int n)
        {
            var mp = MobileParty.MainParty;
            if (mp == null || mp.MemberRoster == null || n < 1) return 0;
            var roster = mp.MemberRoster;
            int freed = 0; var sb = new StringBuilder();
            for (int guard = 0; freed < n && guard < 50; guard++)
            {
                CharacterObject pick = null; int healthy = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    int h = el.Number - el.WoundedNumber;
                    if (ch == null || ch.IsHero || h <= 0) continue;
                    if (pick == null || ch.Tier < pick.Tier || (ch.Tier == pick.Tier && h > healthy)) { pick = ch; healthy = h; }
                }
                if (pick == null) break;
                int take = Math.Min(n - freed, healthy);
                roster.AddToCounts(pick, -take);
                freed += take;
                sb.Append(sb.Length > 0 ? ", " : "").Append(pick.StringId).Append(" x").Append(take);
            }
            Log.Info(LP + "TEST miejsce w druzynie: zdjeci z druzyny gracza " + freed + " z " + n + " (" + (sb.Length > 0 ? sb.ToString() : "-") + ") - tylko sonda, poza bilansem ludzi.");
            return freed;
        }

        private static double PbHomes(Settlement st, string uid, out double mine)
        {
            mine = 0.0; double all = 0.0;
            var pv = new List<VillageBook.PartView>();
            if (!VillageBook.PartsOf(st.Village, pv)) return -1.0;
            foreach (var x in pv) { if (x == null) continue; all += x.Home; if (!x.Main && string.Equals(x.Uid, uid, StringComparison.Ordinal)) mine = x.Home; }
            return all;
        }

        private static LvDone ProbeRecruit(string label, string uid, int want)
        {
            if (uid == null) { Log.Info(LP + "TEST " + label + ": brak wioski - INFO."); return null; }
            var st = DistOf(Rec(uid));
            if (st == null) return null;
            _lvQ = null;
            var q0 = LvQuoteFor(st, uid);
            int k = Math.Min(want, q0 != null ? q0.Ready : 0);
            if (k < 1) { Log.Info(LP + "TEST " + label + " " + NameOf(Rec(uid)) + ": gotowych 0 (zaufanie " + (q0 != null ? q0.Idx : -1) + ") - INFO."); return null; }
            if (_pbRec == 0 || label.StartsWith("przysiolek", StringComparison.Ordinal))
            {
                // test n36: dopoki nie bylo werbunku z kontrolami - pelna druzyna albo pusta kiesa nie zatrzymuja sondy;
                // po recenzji: przysiolek zawsze (kazda doba) - werbunek ma oproznic gotowych do konca dnia, zeby odmowa ponad dzis sprawdzala limit
                var mp = MobileParty.MainParty;
                int over = mp != null && mp.Party != null && mp.MemberRoster != null ? mp.MemberRoster.TotalManCount + k - mp.Party.PartySizeLimit : 0;
                if (over > 0) PbFreeRoom(over);
                long needG = (long)k * Math.Max(1, q0.Price) - Hero.MainHero.Gold;
                if (needG > 0 && needG < int.MaxValue)
                {
                    int g0 = Hero.MainHero.Gold;
                    Hero.MainHero.ChangeHeroGold((int)needG);
                    Log.Info(LP + "TEST dotacja testu " + needG + " denarow (poza bilansem) - werbunek sondy " + label + ": " + k + " x " + q0.Price + ", zloto gracza " + g0 + " -> " + Hero.MainHero.Gold + ".");
                }
            }
            double mine0, mine1;
            double all0 = PbHomes(st, uid, out mine0);
            var region = OutlawLaw.RegionFor(st);
            double vo0 = PeopleBook.VillOutOf(region);
            var res = LevyDo(st, uid, k, "TEST sonda " + label);
            double all1 = PbHomes(st, uid, out mine1);
            double vo1 = PeopleBook.VillOutOf(region);
            var sb = new StringBuilder(700);
            sb.Append(LP).Append("TEST ").Append(label).Append(' ').Append(NameOf(Rec(uid))).Append(" (").Append(uid).Append("): prosba ").Append(k);
            if (res == null || res.Keep <= 0) { sb.Append(" - bez werbunku (").Append(res != null ? res.Why : "-").Append(") INFO."); Log.Info(sb.ToString()); return res; }
            int kk = res.Keep;
            _pbRec++;
            PbCheck(sb, "druzyna", res.Men1 - res.Men0 == kk, "+" + (res.Men1 - res.Men0) + "/" + kk);
            PbCheck(sb, "gracz", res.HeroGold1 - res.HeroGold0 == -res.Total, (res.HeroGold1 - res.HeroGold0) + "/" + (-res.Total));
            PbCheck(sb, "kiesa wsi", res.Purse1 - res.Purse0 == res.Total, "+" + (res.Purse1 - res.Purse0) + "/" + res.Total);
            if (all0 >= 0 && all1 >= 0)
            {
                double dd = all1 - all0, dm = mine1 - mine0, rest = dd - dm;
                PbCheck(sb, "DOM", Math.Abs(dd + res.Got) <= 0.01, dd.ToString("0.###", Inv) + "/-" + res.Got.ToString("0.###", Inv));
                PbCheck(sb, "dom wioski", Math.Abs(dm + kk) <= 1.0, dm.ToString("0.##", Inv) + "/-" + kk);
                PbCheck(sb, "reszta okregu", Math.Abs(rest) <= 0.5 + Math.Abs(kk - res.Got), rest.ToString("0.##", Inv));
            }
            else sb.Append(" | DOM nieznany INFO");
            PbCheck(sb, "pula", Math.Abs(res.Pool0 - res.Pool1 - kk) <= 1e-6 || (res.Pool1 <= 1e-9 && res.Pool0 <= kk), (res.Pool1 - res.Pool0).ToString("0.##", Inv) + "/-" + kk);
            int baseCost = 1;
            try { baseCost = Math.Max(1, Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(res.Troop, Hero.MainHero).RoundedResultNumber); } catch { }
            double mult = res.Stage >= 3 ? LvGeneralPrice : res.Stage == 2 ? LvLevyPrice : 1.0;
            PbCheck(sb, "cena etapu " + res.Stage, res.Price == Math.Max(1, (int)Math.Round(baseCost * mult)), res.Price + "/" + baseCost + "x" + mult.ToString("0.##", Inv));
            if (res.Troop != null && res.Troop.Tier <= 1) { if (res.DtePcs > 0) PbCheck(sb, "DTE tier 1 szt.", true, res.DtePcs.ToString(Inv)); else sb.Append(" | DTE tier 1 szt. 0 INFO"); }
            PbCheck(sb, "VillOut regionu", Math.Abs(vo1 - vo0 - res.Got) <= 0.01, "+" + (vo1 - vo0).ToString("0.###", Inv) + "/" + res.Got.ToString("0.###", Inv));
            sb.Append(" | razem OK ").Append(_pbOk).Append(", BLAD ").Append(_pbBad).Append('.');
            Log.Info(sb.ToString());
            return res;
        }

        /// <summary>n36 (P3): odnowa wpisu uid w dobie today wobec RateOf tej samej wsi okregu (bez pamieci doby LvRate) i suma dob do rozliczenia miesiaca.
        /// Recenzja 113b: true - w tej dobie byla kontrola odnowy miesiaca (sonda widziala wszystkie dopisania rozliczenia) - tylko wtedy przyrost mlodych liczy sie do pokrycia.</summary>
        private static bool ProbeYouth(StringBuilder sb, Settlement st, string uid, int today)
        {
            var rr = LvGet(uid);
            if (rr == null || rr.AccDay != today) return false;   // brak wpisu (pula pelna) albo wpis z dzisiaj - doba go nie dopisywala
            bool month = false;
            float g0 = 0f; string w0 = null; bool ok = false;
            try { ok = st != null && st.Village != null && PopulationLaw.RateOf(st.Village, out g0, out w0); } catch { ok = false; }
            double gExp = ok ? g0 : LvYouthPct;
            double addExp = Math.Max(0.0, rr.AccP) * Math.Max(0.0, gExp) / 100.0 / LvYearDays();
            _pbYouth++;
            PbCheck(sb, "odnowa doby (" + (ok ? "przyrost wsi " + (w0 ?? "-") : "zapas") + ", g " + rr.LastG.ToString("0.####", Inv) + "/" + gExp.ToString("0.####", Inv) + ")",
                    rr.LastFb == !ok && Math.Abs(rr.LastG - gExp) <= 1e-4 && Math.Abs(rr.LastAdd - addExp) <= 1e-6 + 1e-4 * addExp,
                    "+" + rr.LastAdd.ToString("0.#####", Inv) + "/+" + addExp.ToString("0.#####", Inv));
            PbAcc pa;
            if (!_pbAcc.TryGetValue(uid, out pa) || !ReferenceEquals(pa.R, rr)) _pbAcc[uid] = pa = new PbAcc { R = rr };
            if (rr.SettleDay == today)
            {
                // dzisiejsze dopisanie weszlo do rozliczenia, gdy doba dopisala przed rozliczeniem (AccN 0 po nim); rozliczenie z wyceny przed doba - dopisanie juz w nowym miesiacu
                bool inSettle = rr.AccN == 0;
                double sum = pa.Sum + (inSettle ? addExp : 0.0); int days = pa.Days + (inSettle ? 1 : 0);
                if (days == LvMonth && rr.LastSettleN == LvMonth)
                {
                    month = true;
                    PbCheck(sb, "odnowa miesiaca (suma " + days + " dob)", Math.Abs(rr.LastSettle - sum) <= 1e-5 + 1e-4 * sum, "+" + rr.LastSettle.ToString("0.###", Inv) + "/+" + sum.ToString("0.###", Inv));
                }
                else sb.Append(" | odnowa miesiaca +").Append(rr.LastSettle.ToString("0.###", Inv)).Append(" (dob sondy ").Append(days).Append(", wpisu ").Append(rr.LastSettleN).Append(" z ").Append(LvMonth).Append(") INFO");
                pa.Sum = inSettle ? 0.0 : addExp; pa.Days = inSettle ? 0 : 1;
            }
            else { pa.Sum += addExp; pa.Days++; }
            return month;
        }

        private static void ProbeDay(int d, int today)
        {
            foreach (var uid in new[] { _pbA, _pbB, _pbC })
            {
                if (uid == null) continue;
                var st = DistOf(Rec(uid));
                if (st == null) continue;
                _lvQ = null;
                var q = LvQuoteFor(st, uid);
                if (q == null || q.Hide) continue;
                if (!_pbAfterLoad && uid == _pbB && q.Idx < 1)
                {
                    // 113b: przysiolek stracil zaufanie (relacja spadla, nowy notabl) - warunek testu jeszcze raz, wycena od nowa
                    PbLiftTrust(st, uid, "przysiolek doba " + d);
                    _lvQ = null;
                    q = LvQuoteFor(st, uid);
                    if (q == null || q.Hide) continue;
                }
                var sb = new StringBuilder(500);
                sb.Append(LP).Append("TEST doba ").Append(d).Append(' ').Append(NameOf(Rec(uid))).Append(": pula ").Append(q.Pool.ToString("0.##", Inv)).Append(" z ").Append(q.Cap.ToString("0.##", Inv))
                  .Append(", etap ").Append(q.Stage).Append(", gotowych ").Append(q.Ready).Append(", dzis ").Append(q.Today).Append(", mozna ").Append(q.Callable)
                  .Append(", brak rak ").Append(LevyLackNow(uid).ToString("0.##", Inv));
                PbCheck(sb, "0<=pula<=pulap", q.Pool >= 0.0 && q.Pool <= q.Cap + 1e-6, "");
                int def = DefendersFor(uid, q.Home), call = LevyCallableFor(uid, q.Home);
                PbCheck(sb, "obroncy<=pula", def <= call, def + "/" + (call == int.MaxValue ? "bez wpisu" : call.ToString(Inv)));
                bool month = ProbeYouth(sb, st, uid, today);
                double prev; var rq = LvGet(uid);
                bool hp = _pbPrev.TryGetValue(uid, out prev);
                double[] dw; _pbDraw.TryGetValue(uid, out dw);
                double pre = dw != null ? dw[0] : 0.0, post = dw != null ? dw[1] : 0.0;
                // n36 (P3): pula rosnie tylko w dobie rozliczenia - o rozliczony przyrost w toku (z dobowej stopy wsi okregu), do pulapu. Recenzja n36: kontrola z doby
                // rozliczenia, nie z samego wzrostu puli - rozliczenie policzone, a niedopisane do puli, to BLAD, nie cisza. 113b: takze rozliczenie 0 (wies okregu
                // nie rosla) - pula nie moze wtedy wzrosnac. Recenzja 113b: pobor gry od wczorajszej linii (rekruci notabli okregu, polegli) odjety - przed rozliczeniem
                // od puli wyjsciowej, po nim od wyniku (inaczej pobor dnia przed rozliczeniem = falszywy BLAD); do pokrycia (TAK) liczy sie tylko kontrola, ktora moze
                // zawiesc: przyrost ponad tolerancje i w tej samej dobie kontrola odnowy miesiaca (suma dob sondy) - rozliczenie 0 / drobne zostaje kontrola bez pokrycia
                if (hp && rq != null && rq.SettleDay == today)
                {
                    double b0 = Math.Max(0.0, prev - pre);
                    double exp = Math.Min(q.Cap, b0 + Math.Max(0.0, rq.LastSettle)) - b0;
                    double tol = Math.Max(0.05, 0.02 * Math.Abs(exp));
                    bool cover = month && exp > tol;
                    if (cover) _pbSettle++;
                    double seen = q.Pool + post - b0;
                    PbCheck(sb, "przyrost mlodych" + (pre + post > 1e-9 ? " (pobor gry przed " + pre.ToString("0.###", Inv) + ", po " + post.ToString("0.###", Inv) + ")" : "")
                                + (cover ? "" : " (bez pokrycia: " + (month ? "przyrost do " + tol.ToString("0.###", Inv) : "bez odnowy miesiaca") + ")"),
                            Math.Abs(seen - exp) <= tol, "+" + seen.ToString("0.###", Inv) + "/+" + exp.ToString("0.###", Inv));
                }
                else if (hp && q.Pool > prev + 1e-9) sb.Append(" | pula +").Append((q.Pool - prev).ToString("0.###", Inv)).Append(" poza rozliczeniem (nowy wpis albo pulap wzrosl) INFO");
                Log.Info(sb.ToString());
                if (!_pbAfterLoad && uid == _pbB && d <= 40)
                {
                    LvDone dr = null;
                    if (q.Ready > 0) dr = ProbeRecruit("przysiolek doba " + d, uid, int.MaxValue);
                    ProbeOverToday(d, uid, dr);
                }
                var r = LvGet(uid);
                _pbPrev[uid] = r != null ? r.Pool : q.Cap;
                _pbDraw.Remove(uid);   // recenzja 113b: pobor gry liczony od tej puli
            }
            // 113b: TAK tylko, gdy byl werbunek z kontrolami, odmowa ponad dzis, kontrola odnowy doby i kontrola przyrostu mlodych (doba rozliczenia, 28 dob od wpisu)
            Log.Info(LP + "TEST doba " + d + " - razem OK " + _pbOk + ", BLAD " + _pbBad + ", werbunkow sondy z kontrolami " + _pbRec + ", odmow ponad dzis po werbunku " + _pbOver
                     + ", kontroli odnowy doby " + _pbYouth + ", kontroli rozliczenia " + _pbSettle
                     + " = " + (_pbBad > 0 ? "NIE" : (_pbRec == 0 || _pbOver == 0 || _pbYouth == 0 || _pbSettle == 0) && !_pbAfterLoad ? "NIEPOKRYTY" : "TAK") + (_pbAfterLoad ? " (po wczytaniu - tylko kontrole)" : "") + ".");
        }

        /// <summary>Po recenzji: odmowa ponad dzis (przysiolek). Liczy sie tylko po werbunku przysiolka tej doby (sonda dala miejsce i zloto na wszystkich gotowych,
        /// wiec przysiolek jest oprozniony do konca dnia): wtedy werbunek 1 musi byc odmowiony powodem z PbRefusals (OK), werbunek = BLAD. Bez werbunku tej doby
        /// (gotowych 0, odmowa werbunku) - INFO i bez proby (proba moglaby wziac czlowieka poza kontrolami). Druzyna / zloto / nic do wziecia - INFO.</summary>
        private static void ProbeOverToday(int d, string uid, LvDone took)
        {
            if (uid == null) return;
            var s2 = new StringBuilder(LP + "TEST doba " + d + " odmowa po werbunku: ");
            if (took == null || took.Keep <= 0)
            {
                s2.Append("- | bez werbunku przysiolka tej doby (").Append(took != null ? took.Why ?? "-" : "gotowych 0").Append(") - limit dnia niesprawdzony INFO.");
                Log.Info(s2.ToString());
                return;
            }
            var st = DistOf(Rec(uid));
            if (st == null) return;
            var res = LevyDo(st, uid, 1, "TEST sonda ponad dzis");
            string rw = res != null ? res.Why : null;
            s2.Append(rw ?? "-");
            if (res != null && res.Keep == 0 && rw != null && Array.IndexOf(PbNotTested, rw) >= 0)
            {
                s2.Append(" | odmowa bramki (powod ").Append(rw).Append(") - przysiolek nieoprozniony, limit dnia niesprawdzony INFO.");
                Log.Info(s2.ToString());
                return;
            }
            bool legit = rw != null && Array.IndexOf(PbRefusals, rw) >= 0;
            _pbOver++;
            PbCheck(s2, "odmowa bramki (powod " + (rw ?? "-") + ")", res != null && res.Keep == 0 && legit,
                    res == null ? "brak wyniku" : res.Keep > 0 ? "werbunek " + res.Keep + " ponad dzis" : legit ? "" : "nieoczekiwany powod");
            Log.Info(s2.ToString());
        }
    }
}
