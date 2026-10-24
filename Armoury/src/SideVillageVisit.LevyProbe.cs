using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
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
    /// (etapy, odmowa 'koniec dnia'), 0 &lt;= pula &lt;= pulap, przyrost mlodych wobec oczekiwanego, brak rak, obroncy &lt;= pula. Po wczytaniu z wpisami - tylko kontrole.
    /// Test n36: dopoki zaden werbunek sondy nie przeszedl kontroli - pelna druzyna albo za malo zlota nie zatrzymuja sondy (zdjeci ludzie / dotacja testu, linia w logu,
    /// poza bilansem); odmowa po werbunku - kazdy prawidlowy powod bramki to OK (z powodem), BLAD tylko werbunek ponad dzis albo wyjatek; linia doby konczy sie TAK / NIE / NIEPOKRYTY.
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
        // test n36: prawidlowe odmowy po werbunku - kody LevyGate i powody LevyDo (miejsce nie wystepuje: sonda wola bramke jak wioska gracza)
        private static readonly string[] PbRefusals = { "koniec dnia", "pula", "zloto", "druzyna", "podloga", "rabunek", "wojna", "zaciag", "Straz", "spalona", "zaufanie", "nic do wziecia", "DOM na podlodze" };
        private static readonly Dictionary<string, double> _pbPrev = new Dictionary<string, double>(StringComparer.Ordinal);

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

        private static void LevyProbeReset() { _pbDay0 = -1; _pbAfterLoad = _pbLoaded = false; _pbA = _pbB = _pbC = null; _pbForce = null; _pbOk = _pbBad = 0; _pbRec = 0; _pbPrev.Clear(); }

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
            ProbeDay(today - _pbDay0);
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
            double bestP = -1, minP = double.MaxValue; int cScore = -1;
            var mf = Hero.MainHero.MapFaction;
            for (int i = 0; i < near.Count && i < 120; i++)
            {
                var r = near[i].Value; var st = DistOf(r);
                if (st == null || AtWar(st) || st.Village.VillageState != Village.VillageStates.Normal || WatchNow(st)) continue;
                VillageBook.PartView me; double h, n;
                if (!PartOf(st, r.Uid, out me, out h, out n) || Burned(me)) continue;
                double P = me.Home + me.Away;
                if (P > bestP) { bestP = P; _pbA = r.Uid; }
                if (P >= 30 && P < minP) { minP = P; _pbB = r.Uid; }
                int sc = BrakedSafe(st) ? 2 : (mf != null && st.MapFaction != null && !ReferenceEquals(mf, st.MapFaction) ? 1 : 0);
                if (sc > cScore && sc > 0) { cScore = sc; _pbC = r.Uid; }
            }
            if (_pbB == _pbA) _pbB = null;
            if (_pbC == _pbA || _pbC == _pbB) _pbC = null;
            Log.Info(LP + "TEST wioski: najwieksza " + (_pbA ?? "-") + " (ludzi " + bestP.ToString("0", Inv) + "), przysiolek " + (_pbB ?? "-") + " (" + (minP < double.MaxValue ? minP.ToString("0", Inv) : "-")
                     + "), hamulec/obca " + (_pbC ?? "-") + " (rodzaj " + (cScore == 2 ? "hamulec" : cScore == 1 ? "obce krolestwo" : "-") + "), kandydatow " + near.Count + ".");
            ProbeRecruit("najwieksza", _pbA, 5);
            ProbeRecruit("przysiolek", _pbB, int.MaxValue);
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
            Log.Info(LP + "TEST start - werbunkow sondy z kontrolami " + _pbRec + (_pbRec == 0 ? " - NIEPOKRYTY (sprobuje w kolejnych dobach przysiolkiem)" : "") + ".");
        }

        /// <summary>Test n36: miejsce w druzynie dla sondy - zdejmuje z druzyny gracza n zdrowych zwyklych ludzi (najnizszy tier, najliczniejszy rodzaj), linia w logu.
        /// Tylko sonda, dopoki zaden werbunek nie przeszedl kontroli. Zwraca zdjetych.</summary>
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
            if (_pbRec == 0)
            {
                // test n36: dopoki nie bylo werbunku z kontrolami - pelna druzyna albo pusta kiesa nie zatrzymuja sondy
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

        private static void ProbeDay(int d)
        {
            foreach (var uid in new[] { _pbA, _pbB, _pbC })
            {
                if (uid == null) continue;
                var st = DistOf(Rec(uid));
                if (st == null) continue;
                _lvQ = null;
                var q = LvQuoteFor(st, uid);
                if (q == null || q.Hide) continue;
                var sb = new StringBuilder(500);
                sb.Append(LP).Append("TEST doba ").Append(d).Append(' ').Append(NameOf(Rec(uid))).Append(": pula ").Append(q.Pool.ToString("0.##", Inv)).Append(" z ").Append(q.Cap.ToString("0.##", Inv))
                  .Append(", etap ").Append(q.Stage).Append(", gotowych ").Append(q.Ready).Append(", dzis ").Append(q.Today).Append(", mozna ").Append(q.Callable)
                  .Append(", brak rak ").Append(LevyLackNow(uid).ToString("0.##", Inv));
                PbCheck(sb, "0<=pula<=pulap", q.Pool >= 0.0 && q.Pool <= q.Cap + 1e-6, "");
                int def = DefendersFor(uid, q.Home), call = LevyCallableFor(uid, q.Home);
                PbCheck(sb, "obroncy<=pula", def <= call, def + "/" + (call == int.MaxValue ? "bez wpisu" : call.ToString(Inv)));
                double prev;
                if (_pbPrev.TryGetValue(uid, out prev) && q.Pool > prev + 1e-9)
                {
                    double exp = Math.Min(q.Cap, prev + q.P * LvYouthPct / 100.0 * LvMonth / LvYearDays()) - prev;
                    PbCheck(sb, "przyrost mlodych", Math.Abs(q.Pool - prev - exp) <= Math.Max(0.05, 0.02 * exp), "+" + (q.Pool - prev).ToString("0.###", Inv) + "/+" + exp.ToString("0.###", Inv));
                }
                Log.Info(sb.ToString());
                if (!_pbAfterLoad && uid == _pbB && d <= 40)
                {
                    if (q.Ready > 0) ProbeRecruit("przysiolek doba " + d, uid, int.MaxValue);
                    var res = LevyDo(st, uid, 1, "TEST sonda ponad dzis");
                    var s2 = new StringBuilder(LP + "TEST doba " + d + " odmowa po werbunku: " + (res != null ? res.Why ?? "-" : "-"));
                    // test n36: kazdy prawidlowy powod bramki (podloga, rabunek, wojna w trakcie biegu, ...) to OK z powodem; BLAD - werbunek ponad dzis, wyjatek, inny powod
                    string rw = res != null ? res.Why : null;
                    bool legit = rw != null && Array.IndexOf(PbRefusals, rw) >= 0;
                    PbCheck(s2, "odmowa bramki (powod " + (rw ?? "-") + ")", res != null && res.Keep == 0 && legit,
                            res == null ? "brak wyniku" : res.Keep > 0 ? "werbunek " + res.Keep + " ponad dzis" : legit ? "" : "nieoczekiwany powod");
                    Log.Info(s2.ToString());
                }
                var r = LvGet(uid);
                _pbPrev[uid] = r != null ? r.Pool : q.Cap;
            }
            Log.Info(LP + "TEST doba " + d + " - razem OK " + _pbOk + ", BLAD " + _pbBad + ", werbunkow sondy z kontrolami " + _pbRec
                     + " = " + (_pbBad > 0 ? "NIE" : _pbRec == 0 && !_pbAfterLoad ? "NIEPOKRYTY" : "TAK") + (_pbAfterLoad ? " (po wczytaniu - tylko kontrole)" : "") + ".");
        }
    }
}
