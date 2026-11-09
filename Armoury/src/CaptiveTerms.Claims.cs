using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 367a (Jeff 09.11: "krol musi zrekompensowac lordom strate wiec zapyta ich ile chca zlota za swoje tytuly ... warunki sa bardzo wygorowane i zalezy od charyzmy
    /// i ilosci zlota, moze byc tez tak, ze pod warunkiem ze wezme slub z corka jakiegos lorda"). ROKOWANIA Z WIELKIM JENCEM - ETAP 2a: TYTULY WASALI (warunek T2, Cond 'V').
    /// Klucze CaptiveTermsVassalTitles (wylaczony = T jak w 366) i CaptiveTitleClaimYears (lata dochodu w cenie, domyslnie 5). Okup (R, 0.1 nagrody) bez zmian.
    ///
    /// ROSZCZENIE: tytuly de iure BK osad gracza (jego twierdze i wsie ich okregow), ktorych posiadacz zyje, jest w krolestwie jenca, ale nie z rodu krola ani gracza -
    /// jedno roszczenie na posiadacza. Posiadacz nie bierze z nich dzis ani grosza (354: wrogi posiadacz 0) - sprzedaje samo roszczenie ("quitclaim": zrzeczenie sie
    /// za odszkodowanie, jak Henryk III za Normandie w traktacie paryskim 1259). Placi ten, kto zyskuje ziemie (gracz), bezposrednio posiadaczowi; krol daje pieczec.
    /// CENA: Ask = round100( SUMA [ NetDay x rok x lata x premia ] x fCharm x fRel x fCechy x fKrol x fPotrzeba ). NetDay - LandValue.YearMean (zamek/miasto LR,
    /// wies VT; te same ksiegi co LandSale), podloga bez pomiaru: wies 20, zamek 150, miasto 450 zl/d. Rok - z zywego modelu czasu (4 pory; u Jeffa 364 doby).
    /// Premia: wies 1, zamek/miasto LandCastlePremium, siedziba rodu (HomeSettlement albo siedziba kanonu) LandSeatPremium.
    /// fCharm 1 - 0.3 x min(1, Charm/300); fRel 1 - 0.002 x relacja(posiadacz, gracz) w 0.8..1.2; fCechy 1 + 0.15 x Calculating(+) - 0.10 x Generosity(+);
    /// fKrol 1 - 0.0015 x relacja(posiadacz, krol) (wierny wasal pomaga wykupic pana); fPotrzeba 0.9 (G &lt; R albo dluznik drabiny 168).
    /// ZGODA (deterministyczna): S = 10 + 0.25 rel(gracz) + 0.15 rel(krol) + Charm/10 + Lordship/20 + 5 x Honor - 6 x Valor(+) - 20 siedziba + 10 potrzeba;
    /// wariant slubu S + 15 (siedziba odchodzi jako wiano corki). S &lt;= 0 - odmowa, tytuly wypadaja z pakietu, 7 dob przerwy ("jeniec:V:posiadacz").
    /// SLUB: panna - corka posiadacza (slub zastepuje cale odszkodowanie) albo inna niezamezna kobieta jego rodu (zostaje 40% Ask w zlocie); dorosla, bez meza (takze BK
    /// HasPrimarySpouse), CanMarry, nie glowa rodu, nie zareczona. Pan mlody - gracz bez zony, inaczej niezonaty dorosly mezczyzna rodu gracza (nie towarzysz); kazdy raz,
    /// przydzial zachlanny (najdrozsze roszczenie pierwsze). Para: MarriageModel.IsCoupleSuitableForMarriage i BK IsMarriageAdequate &gt; -500, takze same kary bez roznicy pozycji (wiara, pokrewienstwo, druga zona); bez postaci slubu kanonu.
    /// ZGODA KROLA: Evaluate 366 (T) + 4 za zamek (6 za miasto) i +1 za wies wasali w pakiecie (prosba do wlasnych lordow kosztuje go politycznie).
    /// WYKONANIE (kolejka 366, takt mapy, bez rozmowy i SwitchToMenu): wszystko od nowa; zmiana ceny &gt; 5%, zmiana pary albo odmowa - umowa przepada. Tytuly krola jak T;
    /// potem posiadacze: zloto (kiesa gracza -> posiadacz, pod LandSale.InPay - ClanIncomeBook liczy to jako cene ziemi), Inherit, sprawdzenie DeJure == gracz, zwrot
    /// czesci za tytuly, ktore nie przeszly (najwyzej tyle, ile posiadacz ma); posiadacz-glowa rodu - DebtLadder.LandProceeds (wierzyciele jak przy sprzedazy ziemi).
    /// Slub: para jeszcze raz, Inherit, slub dopiero gdy cos przeszlo; slub nie doszedl - tytuly wracaja, zloto wraca. Nic nie przeszlo - jeniec zostaje.
    /// Log "ROKOWANIA (367)": spis roszczen (raz na rozmowe), decyzja i WYKONANE. Potkniecia liczone (Stumble 366), nic nie rzuca.
    /// </summary>
    internal static partial class CaptiveTerms
    {
        internal const string Tag367 = "ROKOWANIA (367)";
        private const double WedScoreBonus = 15, KinWedShare = 0.4, PriceDrift = 0.05;

        internal static bool ClaimsOn { get { var s = Settings.Current; return On && s.CaptiveTermsVassalTitles && LandBk.Ready; } }

        private sealed class Claim
        {
            public Hero Holder;
            public readonly List<object> Titles = new List<object>();
            public readonly List<string> Ids = new List<string>();
            public readonly List<double> Base = new List<double>();   // wartosc tytulu przed czynnikami (udzial w zwrocie)
            public string Names = "-", SeatName = null;
            public int Towns, Castles, Villages;
            public double Sum, Factor = 1;
            public long Ask;
            public double SGold, SWed;
            public double CoolDays;
            public Hero Bride, Groom; public bool Daughter;
            public string Rows = "", FactorPl = "", ScorePl = "", WedPl = "brak panny";
            public bool GoldOk { get { return CoolDays <= 0 && SGold > 0 && Ask > 0; } }
            public bool WedOk { get { return CoolDays <= 0 && Bride != null && Groom != null && SWed > 0; } }
            public long WedGold { get { return Daughter ? 0 : R100(KinWedShare * Ask); } }
        }

        /// <summary>Wybrane roszczenie pakietu: tryb 'G' zloto, 'M' slub.</summary>
        private sealed class Pick367 { public Claim C; public char Mode; public long Gold { get { return Mode == 'M' ? C.WedGold : C.Ask; } } }

        // stan rozmowy 367
        private static List<Claim> _claims;
        private static List<Pick367> _pkg = new List<Pick367>();
        private static char _pkgKind = ' ';
        private static long _pkgGold;

        private static void ClearClaims() { _claims = null; _pkg = new List<Pick367>(); _pkgKind = ' '; _pkgGold = 0; }

        private static long R100(double v) { return v > 0 ? (long)(Math.Round(v / 100.0) * 100) : 0; }
        private static int YearDays() { return Math.Max(28, 4 * LandValue.DaysInSeason()); }

        /// <summary>Roszczenia wasali w biezacej rozmowie (raz na rozmowe; spis do logu).</summary>
        private static List<Claim> TalkClaims()
        {
            if (_claims != null) return _claims;
            _claims = new List<Claim>();
            if (_talk == null || !ClaimsOn) return _claims;
            try { _claims = ClaimsFor(_talk); LogClaims(_talk, _claims); }
            catch (Exception e) { Stumble("TalkClaims", e); _claims = new List<Claim>(); }
            return _claims;
        }

        private static bool HasClaims() { try { return ClaimsOn && _talk != null && TalkClaims().Count > 0; } catch { return false; } }

        // ------------------------------------------------------------ spis, cena, zgoda
        private static List<Claim> ClaimsFor(Hero king)
        {
            var by = new Dictionary<Hero, Claim>();
            var order = new List<Claim>();
            if (king == null || king.Clan == null || Clan.PlayerClan == null || !LandBk.Ready) return order;
            var s = Settings.Current;
            double years = Math.Max(1f, Math.Min(20f, s.CaptiveTitleClaimYears));
            int yd = YearDays();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    Clan holder = null;
                    if (st.IsTown || st.IsCastle) holder = st.OwnerClan;
                    else if (st.IsVillage && st.Village != null && st.Village.Bound != null) holder = st.Village.Bound.OwnerClan;
                    if (holder != Clan.PlayerClan) continue;
                    var t = LandBk.Title(st);
                    var dj = LandBk.DeJure(t);
                    if (dj == null || !dj.IsAlive || dj.Clan == null || dj.Clan == Clan.PlayerClan || dj == Hero.MainHero) continue;
                    if (dj == king || dj.Clan == king.Clan) continue;            // 366 T - pieczec krola, za darmo
                    if (dj.MapFaction == null || dj.MapFaction != king.MapFaction) continue;   // tylko wasale korony jenca
                    Claim c;
                    if (!by.TryGetValue(dj, out c)) { c = new Claim { Holder = dj }; by[dj] = c; order.Add(c); }
                    int d; double v; double floor, prem; string kind;
                    bool seat = false;
                    if (st.IsVillage) { v = LandValue.YearMean(st, LandValue.VT, out d); floor = 20; prem = 1; kind = "wies"; c.Villages++; }
                    else
                    {
                        d = 0; v = 0;
                        if (st.IsCastle) v = LandValue.YearMean(st, LandValue.LR, out d);   // miasta LandValue nie mierzy - podloga
                        floor = st.IsTown ? 450 : 150; kind = st.IsTown ? "miasto" : "zamek";
                        seat = st == dj.Clan.HomeSettlement || LandSale.CanonSeat(dj.Clan, st);
                        prem = seat ? Math.Max(1f, s.LandSeatPremium) : Math.Max(1f, s.LandCastlePremium);
                        if (st.IsTown) c.Towns++; else c.Castles++;
                        if (seat && c.SeatName == null) c.SeatName = st.Name.ToString();
                    }
                    string meas;
                    if (d <= 0 || !(v > 0)) { v = floor; meas = "podloga"; }
                    else meas = d < 28 ? "pomiar " + d + " dob (malo)" : "pomiar " + d + " dob";
                    double b = v * yd * years * prem;
                    c.Titles.Add(t); c.Ids.Add(st.StringId); c.Base.Add(b); c.Sum += b;
                    if (c.Rows.Length > 0) c.Rows += ", ";
                    c.Rows += st.Name + " (" + kind + (seat ? ", SIEDZIBA" : "") + ", NetDay " + F(v) + " " + meas + ", " + yd + " x " + F(years) + " x " + prem.ToString("0.##", Inv) + " = " + Math.Round(b) + ")";
                }
                catch (Exception e) { Stumble("ClaimsFor", e); }
            }
            foreach (var c in order)
            {
                try { NameList(c); PriceAndScore(king, c); }
                catch (Exception e) { Stumble("ClaimsFor(cena)", e); c.Ask = 0; c.SGold = c.SWed = -1; }
            }
            order.Sort((a, b) => b.Ask.CompareTo(a.Ask));
            try { Matchmake(order); } catch (Exception e) { Stumble("Matchmake", e); }
            return order;
        }

        private static void NameList(Claim c)
        {
            var n = new List<string>();
            foreach (var id in c.Ids) { var st = Settlement.Find(id); n.Add(st != null ? st.Name.ToString() : id); }
            c.Names = Join(n);
        }

        /// <summary>"A", "A and B", "A, B and C".</summary>
        private static string Join(List<string> n)
        {
            if (n == null || n.Count == 0) return "-";
            if (n.Count == 1) return n[0];
            return string.Join(", ", n.GetRange(0, n.Count - 1).ToArray()) + " and " + n[n.Count - 1];
        }

        private static bool InNeed(Clan c, out string pl)
        {
            pl = "nie";
            try
            {
                double G, R, D; int stk;
                bool ok = ClanBudget.NeedOf(c, out G, out R, out D, out stk);
                bool debtor = DebtLadder.IsDebtor(c);
                bool need = debtor || (ok && G < R);
                pl = need ? (debtor ? "dluznik" : "G " + Math.Round(G) + " < R " + Math.Round(R)) : "nie (G " + Math.Round(G) + ", R " + Math.Round(R) + ")";
                return need;
            }
            catch { return false; }
        }

        private static void PriceAndScore(Hero king, Claim c)
        {
            var me = Hero.MainHero;
            var h = c.Holder;
            int charm = 0; try { charm = me.GetSkillValue(DefaultSkills.Charm); } catch { }
            int lord = Lordship(me);
            int relMe = 0, relK = 0;
            try { relMe = h.GetRelation(me); } catch { }
            try { relK = h.GetRelation(king); } catch { }
            int calc = Trait(h, DefaultTraits.Calculating), gen = Trait(h, DefaultTraits.Generosity), honor = Trait(h, DefaultTraits.Honor), valor = Trait(h, DefaultTraits.Valor);
            string needPl;
            bool need = InNeed(h.Clan, out needPl);
            double fCharm = 1 - 0.30 * Math.Min(1.0, charm / 300.0);
            double fRel = Math.Max(0.8, Math.Min(1.2, 1 - 0.002 * relMe));
            double fCechy = 1 + 0.15 * Math.Max(0, calc) - 0.10 * Math.Max(0, gen);
            double fKrol = 1 - 0.0015 * Math.Max(-100, Math.Min(100, relK));
            double fNeed = need ? 0.9 : 1.0;
            c.Factor = fCharm * fRel * fCechy * fKrol * fNeed;
            c.Ask = R100(c.Sum * c.Factor);
            c.FactorPl = "fCharm " + fCharm.ToString("0.###", Inv) + " (Charm " + charm + "), fRel " + fRel.ToString("0.###", Inv) + " (" + relMe + "), fCechy " + fCechy.ToString("0.###", Inv)
                       + " (Calculating " + calc + ", Generosity " + gen + "), fKrol " + fKrol.ToString("0.###", Inv) + " (" + relK + "), fPotrzeba " + fNeed.ToString("0.##", Inv) + " (" + needPl + ")"
                       + " = x" + c.Factor.ToString("0.###", Inv);
            double S = 10 + 0.25 * relMe + 0.15 * relK + charm / 10.0 + lord / 20.0 + 5 * honor - 6 * Math.Max(0, valor) - (c.SeatName != null ? 20 : 0) + (need ? 10 : 0);
            c.SGold = S; c.SWed = S + WedScoreBonus;
            if (c.Ask > int.MaxValue) { c.Ask = 0; c.SGold = c.SWed = -1; }   // poza zasiegiem przelewu - ani zloto, ani slub krewnej za 40% (recenzja: bylby za darmo)
            c.ScorePl = "10 + rel " + F(0.25 * relMe) + " + krol " + F(0.15 * relK) + " + Charm " + F(charm / 10.0) + " + Lordship " + F(lord / 20.0) + " + Honor " + F(5 * honor)
                      + " - Valor " + F(6 * Math.Max(0, valor)) + (c.SeatName != null ? " - siedziba 20" : "") + (need ? " + potrzeba 10" : "");
            c.CoolDays = CoolLeftV(king, h);
        }

        private static double CoolLeftV(Hero king, Hero holder)
        {
            double end;
            if (king == null || holder == null || !_cool.TryGetValue(king.StringId + ":V:" + holder.StringId, out end)) return 0;
            double left = end - CampaignTime.Now.ToDays;
            return left > 0 ? left : 0;
        }

        // ------------------------------------------------------------ slub
        private static bool _bkTried, _bkMissLogged;
        private static MethodInfo _mPrimary, _mAdequate, _mScore;
        private static PropertyInfo _pCfgInst, _pCfgMarriage;

        private static void ResolveBk()
        {
            if (_bkTried) return;
            _bkTried = true;
            try
            {
                var ext = AccessTools.TypeByName("BannerKings.Utils.Extensions.HeroExtensions");
                if (ext != null) _mPrimary = AccessTools.Method(ext, "HasPrimarySpouse", new[] { typeof(Hero) });
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var mmT = AccessTools.TypeByName("BannerKings.Models.Vanilla.Abstract.MarriageModel");
                if (cfgT != null && mmT != null)
                {
                    _pCfgInst = AccessTools.Property(cfgT, "Instance");
                    _pCfgMarriage = AccessTools.Property(cfgT, "MarriageModel");
                    _mAdequate = AccessTools.Method(mmT, "IsMarriageAdequate", new[] { typeof(Hero), typeof(Hero), typeof(bool), typeof(bool) });
                    _mScore = AccessTools.Method(mmT, "GetSpouseScore", new[] { typeof(Hero), typeof(bool) });
                }
            }
            catch (Exception e) { Stumble("ResolveBk", e); }
        }

        /// <summary>Malzonek zywy albo BK PrimarySpouse (BK nie sprawdza Spouse w IsSuitableForMarriage - pozwala na druga zone).</summary>
        private static bool Married(Hero h)
        {
            if (h.Spouse != null && h.Spouse.IsAlive) return true;
            ResolveBk();
            try { if (_mPrimary != null) return (bool)_mPrimary.Invoke(null, new object[] { h }); } catch (Exception e) { Stumble("Married", e); return true; }
            return false;
        }

        /// <summary>BK IsMarriageAdequate (bk.cs:40681). Wynik BK = roznica pozycji (1.1 x GetSpouseScore pana - GetSpouseScore panny) + kultura/wiara + kary -1000
        /// (wroga albo nietolerowana wiara, pokrewienstwo doktryny, druga zona, zaloty). Recenzja: wysoka pozycja Jeffa (+500..+650) maskowala kare -1000 (Starzy Bogowie
        /// a Siedmiu), wiec kary licza sie osobno: pen = wynik - roznica pozycji. Para przechodzi, gdy wynik &gt; -500 (jak dotad) I pen &gt; -500. Bez BK - 0 (przechodzi,
        /// linia logu raz). Wyjatek - -10000 (nie przechodzi).</summary>
        private static double Adequate(Hero groom, Hero bride, out double pen)
        {
            pen = 0;
            ResolveBk();
            try
            {
                object mm = null;
                if (_mAdequate != null && _mScore != null && _pCfgInst != null && _pCfgMarriage != null)
                {
                    var cfg = _pCfgInst.GetValue(null, null);
                    mm = cfg != null ? _pCfgMarriage.GetValue(cfg, null) : null;
                }
                if (mm == null)
                {
                    if (!_bkMissLogged) { _bkMissLogged = true; Log.Info(Tag367 + ": BK IsMarriageAdequate/GetSpouseScore nieznalezione - sluby bez kontroli wiary BK (tylko gra: IsCoupleSuitableForMarriage)."); }
                    return 0;
                }
                var r = _mAdequate.Invoke(mm, new object[] { groom, bride, false, false });
                double raw = r is ExplainedNumber ? ((ExplainedNumber)r).ResultNumber : -10000;
                if (raw <= -9999) { pen = raw; return raw; }   // twarde odrzucenie BK (druga zona, CanMarry, rod) - bez roznicy pozycji
                var sg = _mScore.Invoke(mm, new object[] { groom, false });
                var sb = _mScore.Invoke(mm, new object[] { bride, false });
                double diff = (sg is ExplainedNumber && sb is ExplainedNumber) ? 1.1 * ((ExplainedNumber)sg).ResultNumber - ((ExplainedNumber)sb).ResultNumber : 0;
                pen = raw - diff;
                return raw;
            }
            catch (Exception e) { Stumble("Adequate", e); pen = -10000; return -10000; }
        }

        private static bool Engaged(Hero h)
        {
            try { var b = Campaign.Current.GetCampaignBehavior<IMarriageOfferCampaignBehavior>(); return b != null && b.IsHeroEngaged(h); }
            catch { return false; }
        }

        private static bool FreeToWed(Hero x, bool female)
        {
            return x != null && x.IsAlive && x.IsLord && x.IsFemale == female && x.Clan != null && x.Age >= 18 && !x.IsPrisoner && x.CompanionOf == null
                   && x.Clan.Leader != x && !Married(x) && !Engaged(x) && x.CanMarry() && !CanonCompanions.IsCanonMarriageRef(x);   // recenzja: slub kanonu (kronika)
        }

        /// <summary>Matka albo babka posiadacza (owdowiala) - nie panna do traktatu (recenzja).</summary>
        private static bool Elder(Hero b, Hero holder)
        {
            if (holder == null) return false;
            if (b == holder.Mother) return true;
            if (holder.Father != null && b == holder.Father.Mother) return true;
            if (holder.Mother != null && b == holder.Mother.Mother) return true;
            return false;
        }

        private static bool BrideOk(Hero b, Hero holder)
        {
            return FreeToWed(b, true) && holder != null && b.Clan == holder.Clan && b.Clan != Clan.PlayerClan && !Elder(b, holder);
        }

        private static bool GroomOk(Hero g)
        {
            if (g == null || g.Clan != Clan.PlayerClan) return false;
            if (g == Hero.MainHero) return g.IsAlive && !g.IsFemale && !g.IsPrisoner && !Married(g) && !Engaged(g) && g.CanMarry();
            return FreeToWed(g, false);
        }

        private static bool PairOk(Hero g, Hero b, out string adq)
        {
            adq = "-";
            try
            {
                if (!Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(g, b)) { adq = "gra: para niedozwolona"; return false; }
                double pen, raw = Adequate(g, b, out pen);
                adq = F(raw) + ", kary " + F(pen);
                return raw > -500 && pen > -500;
            }
            catch (Exception e) { Stumble("PairOk", e); adq = "wyjatek"; return false; }
        }

        private static List<Hero> Grooms()
        {
            var l = new List<Hero>();
            var me = Hero.MainHero;
            if (GroomOk(me)) l.Add(me);
            if (Clan.PlayerClan != null) foreach (var g in Clan.PlayerClan.Heroes) if (g != me && GroomOk(g)) l.Add(g);
            return l;
        }

        private static List<KeyValuePair<Hero, bool>> Brides(Hero h)
        {
            var l = new List<KeyValuePair<Hero, bool>>();
            var seen = new HashSet<Hero>();
            try { foreach (var b in h.Children) if (BrideOk(b, h) && seen.Add(b)) l.Add(new KeyValuePair<Hero, bool>(b, true)); } catch (Exception e) { Stumble("Brides(dzieci)", e); }
            var kin = new List<Hero>();
            try { if (h.Clan != null) foreach (var b in h.Clan.Heroes) if (!seen.Contains(b) && BrideOk(b, h)) kin.Add(b); } catch (Exception e) { Stumble("Brides(rod)", e); }
            kin.Sort((a, b) => a.Age.CompareTo(b.Age));
            foreach (var b in kin) l.Add(new KeyValuePair<Hero, bool>(b, false));
            return l;
        }

        /// <summary>Do logu: kobiety rodu posiadacza pominiete, bo maja slub kanonu (kronika).</summary>
        private static string CanonBrides(Hero h)
        {
            try
            {
                var n = new List<string>();
                if (h != null && h.Clan != null)
                    foreach (var b in h.Clan.Heroes)
                        if (b != null && b.IsAlive && b.IsFemale && b.Spouse == null && CanonCompanions.IsCanonMarriageRef(b)) n.Add(Name(b));
                return n.Count > 0 ? " (slub kanonu w kronice: " + string.Join(", ", n.ToArray()) + ")" : "";
            }
            catch { return ""; }
        }

        /// <summary>Przydzial par zachlanny: najdrozsze roszczenie pierwsze, corki przed reszta rodu, gracz przed krewnymi; kazdy pan mlody i panna raz.</summary>
        private static void Matchmake(List<Claim> claims)
        {
            var grooms = Grooms();
            var used = new HashSet<Hero>();
            foreach (var c in claims)
            {
                if (c.CoolDays > 0) { c.WedPl = "przerwa odmowy"; continue; }
                var brides = Brides(c.Holder);
                if (brides.Count == 0) { c.WedPl = "brak wolnej panny w rodzie" + CanonBrides(c.Holder); continue; }
                if (grooms.Count == 0) { c.WedPl = "brak wolnego pana mlodego w rodzie gracza"; continue; }
                string why = "para nie przechodzi (pokrewienstwo, zaloty, wiara albo pozycja)";
                bool done = false;
                foreach (var bp in brides)
                {
                    if (used.Contains(bp.Key)) continue;
                    foreach (var g in grooms)
                    {
                        if (used.Contains(g)) continue;
                        string adq;
                        if (!PairOk(g, bp.Key, out adq)) { why = "para " + Name(g) + " + " + Name(bp.Key) + " nie przechodzi (BK " + adq + ")"; continue; }
                        c.Bride = bp.Key; c.Groom = g; c.Daughter = bp.Value;
                        used.Add(g); used.Add(bp.Key);
                        c.WedPl = Name(bp.Key) + " (" + (bp.Value ? "corka" : "z rodu") + ", wiek " + Math.Floor(bp.Key.Age) + ") + " + Name(g) + (g == Hero.MainHero ? " (gracz)" : " (rod gracza)") + ", BK " + adq;
                        done = true; break;
                    }
                    if (done) break;
                }
                if (!done) c.WedPl = why;
            }
        }

        private static string LordOf(Hero h) { return (h != null && h.IsFemale ? "Lady " : "Lord ") + Name(h); }
        private static string HisOf(Hero h) { return h != null && h.IsFemale ? "her" : "his"; }

        private static string Kin(Hero b, Hero holder)
        {
            if (b.Father == holder || b.Mother == holder) return "daughter";
            if (holder.Father != null && b.Father == holder.Father) return "sister";
            if (holder.Father != null && b.Father != null && b.Father.Father == holder.Father) return "niece";
            return "kinswoman";
        }

        private static string GroomWord(Hero g)
        {
            var me = Hero.MainHero;
            if (g == me) return "you";
            if (g.Father == me || g.Mother == me) return "your son " + Name(g);
            if (me.Father != null && g.Father == me.Father) return "your brother " + Name(g);
            return "your kinsman " + Name(g);
        }

        // ------------------------------------------------------------ rozmowa 367
        private static bool T2Cond() { return _talk != null && HasClaims(); }
        private static bool T1Cond() { return _talk != null && !HasClaims(); }

        private static bool ClaimsLineCond()
        {
            try
            {
                var cl = TalkClaims();
                var sb = new StringBuilder(400);
                string own; var mine = TitlesFor(_talk, out own);
                sb.Append(mine.Count > 0 ? "My seal covers " + own + "." : "My own seal covers nothing you hold.");
                sb.Append(" As for my lords, I have put it to them:");
                foreach (var c in cl)
                {
                    sb.Append(' ');
                    string who = LordOf(c.Holder), his = HisOf(c.Holder), he = c.Holder.IsFemale ? "she" : "he";
                    if (c.CoolDays > 0) { sb.Append(who).Append(" will not hear of it again so soon (" + Math.Ceiling(c.CoolDays).ToString(Inv) + " days)."); continue; }
                    string wed = null;
                    if (c.WedOk)
                    {
                        string kin = Kin(c.Bride, c.Holder);
                        string g = GroomWord(c.Groom);
                        wed = (c.Groom == Hero.MainHero ? "if you take " + his + " " + kin + " " + Name(c.Bride) + " to wife" : "if " + g + " weds " + his + " " + kin + " " + Name(c.Bride))
                              + (c.Daughter ? "" : ", and " + c.WedGold.ToString("N0", Inv) + " denars");
                    }
                    if (c.GoldOk)
                    {
                        sb.Append(who).Append(" asks ").Append(c.Ask.ToString("N0", Inv)).Append(" denars for ").Append(his).Append(" claim to ").Append(c.Names);
                        sb.Append(wed != null ? " - or " + he + " would give it up " + wed + "." : ".");
                    }
                    else if (wed != null)
                        sb.Append(who).Append(" will not sell ").Append(his).Append(" claim to ").Append(c.Names).Append(" for gold").Append(c.SeatName != null ? " - " + c.SeatName + " is the seat of " + his + " fathers" : "")
                          .Append(" - but ").Append(he).Append(" would give it up ").Append(wed).Append('.');
                    else
                        sb.Append(who).Append(" refuses: ").Append(c.SeatName != null ? c.SeatName + " is the seat of " + his + " fathers." : he + " will not give up " + his + " claim to " + c.Names + ".");
                }
                MBTextManager.SetTextVariable("ARM367_CLAIMS", sb.ToString());
                long g0 = 0, g1 = 0; int nG = 0, nM = 0; var pairs = new List<string>();
                foreach (var c in cl)
                {
                    if (c.GoldOk) { g0 += c.Ask; nG++; }
                    if (c.WedOk) { g1 += c.WedGold; nM++; pairs.Add(c.Groom == Hero.MainHero ? "I will wed " + Name(c.Bride) : "my " + GroomWord(c.Groom).Replace("your ", "") + " will wed " + Name(c.Bride)); }
                    else if (c.GoldOk) g1 += c.Ask;
                }
                MBTextManager.SetTextVariable("ARM367_TOTAL", g0.ToString("N0", Inv));
                MBTextManager.SetTextVariable("ARM367_WED", "Seal it with marriage: " + Join(pairs) + ", and pay the rest (" + g1.ToString("N0", Inv) + " denars).");
                return true;
            }
            catch (Exception e) { Stumble("ClaimsLineCond", e); MBTextManager.SetTextVariable("ARM367_CLAIMS", "My lords will not answer now."); return true; }
        }

        private static List<Pick367> Package(char kind)
        {
            var l = new List<Pick367>();
            foreach (var c in TalkClaims())
            {
                if (kind == 'M' && c.WedOk) l.Add(new Pick367 { C = c, Mode = 'M' });
                else if (c.GoldOk) l.Add(new Pick367 { C = c, Mode = 'G' });
            }
            return l;
        }

        private static long GoldOf(List<Pick367> p) { long g = 0; foreach (var x in p) g += x.Gold; return g; }

        private static bool GoldPkgCond() { try { foreach (var c in TalkClaims()) if (c.GoldOk) return true; } catch { } return false; }
        private static bool WedPkgCond() { try { foreach (var c in TalkClaims()) if (c.WedOk) return true; } catch { } return false; }
        private static bool OwnSealCond() { try { string n; return _talk != null && TitlesFor(_talk, out n).Count > 0; } catch { return false; } }

        private static bool GoldPkgClick(out TextObject why) { return PkgClick('G', out why); }
        private static bool WedPkgClick(out TextObject why) { return PkgClick('M', out why); }
        private static bool PkgClick(char kind, out TextObject why)
        {
            why = null;
            try
            {
                long g = GoldOf(Package(kind));
                if (Hero.MainHero.Gold >= g) return true;
                why = new TextObject("{=!}You do not have " + g.ToString("N0", Inv) + " denars.");
                return false;
            }
            catch (Exception e) { Stumble("PkgClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        private static void PickGold() { PickV('G'); }
        private static void PickWed() { PickV('M'); }

        private static void PickV(char kind)
        {
            try
            {
                _pkg = Package(kind);
                _pkgKind = kind;
                _pkgGold = GoldOf(_pkg);
                Pick('V');
            }
            catch (Exception e) { Stumble("PickV", e); _sc = null; }
        }

        /// <summary>Dodatkowy koszt zgody krola za tytuly wasali w pakiecie (+4 zamek, +6 miasto, +1 wies).</summary>
        private static double VassalCost(out string pl)
        {
            int t = 0, c = 0, v = 0;
            foreach (var p in _pkg) { t += p.C.Towns; c += p.C.Castles; v += p.C.Villages; }
            pl = "wasale: miasta " + t + ", zamki " + c + ", wsie " + v;
            return 6 * t + 4 * c + v;
        }

        private static string PkgHint()
        {
            var parts = new List<string>();
            foreach (var p in _pkg)
                parts.Add(Name(p.C.Holder) + (p.Mode == 'M' ? " if " + (p.C.Groom == Hero.MainHero ? "you wed " : GroomWord(p.C.Groom) + " weds ") + Name(p.C.Bride) + (p.Gold > 0 ? " and " + p.Gold.ToString("N0", Inv) + " denars" : "")
                                                         : " for " + p.Gold.ToString("N0", Inv) + " denars"));
            return " His lords give up their claims: " + Join(parts) + ". You pay " + _pkgGold.ToString("N0", Inv) + " denars to them in all.";
        }

        private static string PkgPl()
        {
            var sb = new StringBuilder();
            foreach (var p in _pkg)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(Name(p.C.Holder)).Append(' ').Append(p.Mode == 'M' ? "slub " + Name(p.C.Groom) + " + " + Name(p.C.Bride) + (p.Gold > 0 ? " i " + p.Gold : "") : "zloto " + p.Gold)
                  .Append(" (").Append(p.C.Names).Append(')');
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>Odmowy w wybranym pakiecie - przerwa 7 dob na tego posiadacza.</summary>
        private static int CoolRefusers(Hero king)
        {
            int n = 0;
            foreach (var c in TalkClaims())
            {
                if (c.CoolDays > 0) continue;
                bool inPkg = false;
                foreach (var p in _pkg) if (p.C == c) { inPkg = true; break; }
                if (inPkg) continue;
                bool refused = !c.GoldOk && !c.WedOk;   // recenzja: odmowa = brak zgody na zloto i na slub (pakiet zlota nie karze lorda, ktory chcial slubu)
                if (!refused) continue;
                _cool[king.StringId + ":V:" + c.Holder.StringId] = CampaignTime.Now.ToDays + CooldownDays;
                n++;
            }
            return n;
        }

        /// <summary>"posiadacz=osada,osada/cena/G|M/panna/pan_mlody" rozdzielone '~'.</summary>
        private static string PkgCode()
        {
            var sb = new StringBuilder();
            foreach (var p in _pkg)
            {
                if (BadId(p.C.Holder.StringId)) continue;
                bool bad = false; foreach (var id in p.C.Ids) if (BadId(id)) bad = true;
                if (bad) continue;
                if (sb.Length > 0) sb.Append('~');
                sb.Append(p.C.Holder.StringId).Append('=').Append(string.Join(",", p.C.Ids.ToArray())).Append('/').Append(p.C.Ask.ToString(Inv)).Append('/').Append(p.Mode)
                  .Append('/').Append(p.Mode == 'M' && !BadId(p.C.Bride.StringId) ? p.C.Bride.StringId : "-").Append('/').Append(p.Mode == 'M' && !BadId(p.C.Groom.StringId) ? p.C.Groom.StringId : "-");
            }
            return sb.ToString();
        }

        private static readonly char[] BadV = { '|', ';', ':', '~', '/', '=', ',' };
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOfAny(BadV) >= 0; }

        private sealed class Want { public string Holder, Bride, Groom; public List<string> Ids = new List<string>(); public long Ask; public char Mode; }

        private static List<Want> ParseCode(string code)
        {
            var l = new List<Want>();
            if (string.IsNullOrEmpty(code)) return l;
            foreach (var part in code.Split('~'))
            {
                var eq = part.Split('=');
                if (eq.Length != 2) continue;
                var x = eq[1].Split('/');
                long a;
                if (x.Length != 5 || !long.TryParse(x[1], NumberStyles.Integer, Inv, out a) || x[2].Length != 1 || (x[2][0] != 'G' && x[2][0] != 'M')) continue;
                var w = new Want { Holder = eq[0], Ask = a, Mode = x[2][0], Bride = x[3], Groom = x[4] };
                foreach (var id in x[0].Split(',')) if (id.Length > 0) w.Ids.Add(id);
                l.Add(w);
            }
            return l;
        }

        internal static bool ValidCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return true;
            return ParseCode(code).Count == code.Split('~').Length;
        }

        private static void LogClaims(Hero king, List<Claim> cl)
        {
            var sb = new StringBuilder(600);
            string own; var mine = TitlesFor(king, out own);
            sb.Append(Tag367).Append(": spis roszczen - ").Append(king.MapFaction != null && king.MapFaction.Leader == king ? "krol " : "nastepca ").Append(Name(king))
              .Append(" | jego pieczec (366 T, za darmo): ").Append(mine.Count).Append(" (").Append(own).Append(") | rok ").Append(YearDays()).Append(" dob, lata ")
              .Append(F(Settings.Current.CaptiveTitleClaimYears)).Append(" | posiadaczy ").Append(cl.Count);
            foreach (var c in cl)
            {
                sb.Append(" || ").Append(Name(c.Holder)).Append(" (").Append(c.Holder.Clan != null ? c.Holder.Clan.Name.ToString() : "-").Append(") - ").Append(c.Rows)
                  .Append(" | suma ").Append(Math.Round(c.Sum)).Append(" x ").Append(c.FactorPl).Append(" | zada ").Append(c.Ask)
                  .Append(" | zgoda: zloto ").Append(F(c.SGold)).Append(c.GoldOk ? " PRZYJMUJE" : " ODMAWIA").Append(" (").Append(c.ScorePl).Append("), slub ")
                  .Append(F(c.SWed)).Append(c.WedOk ? " PRZYJMUJE: " : " - ").Append(c.WedPl)
                  .Append(c.WedOk ? (c.Daughter ? ", odszkodowanie 0" : ", zloto 40% = " + c.WedGold) : "")
                  .Append(c.CoolDays > 0 ? " | przerwa odmowy jeszcze " + F(c.CoolDays) + " dob" : "");
            }
            Log.Info(sb.Append('.').ToString());
        }

        // ------------------------------------------------------------ wykonanie T2
        /// <summary>Gold transfer pod LandSale.InPay; zwraca zaplacone (spadek kiesy placacego).</summary>
        private static long PayLand(Hero from, Hero to, long amount, string where)
        {
            if (from == null || to == null || amount <= 0) return 0;
            long g0 = from.Gold;
            int x = (int)Math.Min(int.MaxValue, Math.Max(0, Math.Min(amount, (long)from.Gold)));
            if (x <= 0) return 0;
            LandSale.InPay = true;
            try { GiveGoldAction.ApplyBetweenCharacters(from, to, x, true); }
            catch (Exception e) { Stumble(where, e); }
            finally { LandSale.InPay = false; }
            return g0 - from.Gold;
        }

        private sealed class Done { public string Pl; public int Moved, Failed; public long Paid, Back; }

        /// <summary>Roszczenia z umowy od nowa; null = umowa przepada (powod en/pl).</summary>
        private static List<Pick367> Recheck(Deal d, Hero king, out string en, out string pl)
        {
            en = pl = null;
            var wants = ParseCode(d.Claims);
            if (wants.Count == 0) { en = "the claims of his lords could not be read."; pl = "brak roszczen w umowie"; return null; }
            var now = ClaimsFor(king);
            var res = new List<Pick367>();
            foreach (var w in wants)
            {
                Claim c = null;
                foreach (var x in now) if (x.Holder != null && x.Holder.StringId == w.Holder) { c = x; break; }
                if (c == null) { en = "a lord's claim is no longer what it was - speak to him again."; pl = "roszczenie " + w.Holder + " juz nie istnieje"; return null; }
                if (w.Ask > 0 && Math.Abs(c.Ask - w.Ask) > PriceDrift * w.Ask) { en = LordOf(c.Holder) + " now asks " + c.Ask.ToString("N0", Inv) + " denars - speak to him again."; pl = "cena " + Name(c.Holder) + " zmienila sie: " + w.Ask + " -> " + c.Ask; return null; }
                // tylko tytuly z umowy (nowe doszle - poza pakietem, wypadle - poza nim)
                for (int i = c.Ids.Count - 1; i >= 0; i--)
                    if (!w.Ids.Contains(c.Ids[i])) { c.Sum -= c.Base[i]; c.Ids.RemoveAt(i); c.Titles.RemoveAt(i); c.Base.RemoveAt(i); }
                if (c.Titles.Count == 0) { en = LordOf(c.Holder) + " no longer holds those claims."; pl = "tytuly " + Name(c.Holder) + " juz nie u niego"; return null; }
                c.Ask = w.Ask;   // cena z umowy (w granicy 5%)
                if (w.Mode == 'M')
                {
                    var b = Hero.Find(w.Bride); var g = Hero.Find(w.Groom); string adq;
                    if (b == null || g == null || !BrideOk(b, c.Holder) || !GroomOk(g) || !PairOk(g, b, out adq) || !(c.SWed > 0))
                    { en = "the match with " + (b != null ? Name(b) : HisOf(c.Holder) + " kinswoman") + " can no longer be made - speak to " + (c.Holder.IsFemale ? "her" : "him") + " again."; pl = "slub " + w.Groom + " + " + w.Bride + " juz niemozliwy albo posiadacz odmawia"; return null; }
                    c.Bride = b; c.Groom = g; c.Daughter = b.Father == c.Holder || b.Mother == c.Holder;
                }
                else if (!(c.SGold > 0)) { en = LordOf(c.Holder) + " now refuses - speak to " + (c.Holder.IsFemale ? "her" : "him") + " again."; pl = Name(c.Holder) + " teraz odmawia (zloto " + F(c.SGold) + ")"; return null; }
                res.Add(new Pick367 { C = c, Mode = w.Mode });
            }
            return res;
        }

        private static Done RunClaim(Pick367 p)
        {
            var me = Hero.MainHero;
            var c = p.C; var h = c.Holder;
            var r = new Done();
            long price = p.Gold;
            if (price > 0)
            {
                long paid = PayLand(me, h, price, "RunClaim(zaplata)");
                if (paid != price)
                {
                    long back = paid > 0 ? PayLand(h, me, paid, "RunClaim(zwrot a)") : 0;
                    r.Back = back; r.Failed = c.Titles.Count;
                    r.Pl = Name(h) + ": zaplata nieudana (" + paid + " z " + price + "), zwrot " + back;
                    return r;
                }
                r.Paid = paid;
            }
            var moved = new List<object>(); double failBase = 0;
            for (int i = 0; i < c.Titles.Count; i++)
            {
                var t = c.Titles[i];
                var from = LandBk.DeJure(t);
                if (from == h && LandBk.Inherit(from, me, t) && LandBk.DeJure(t) == me) moved.Add(t); else { r.Failed++; failBase += c.Base[i]; }
            }
            r.Moved = moved.Count;
            string wed = "";
            if (p.Mode == 'M' && moved.Count > 0)
            {
                try { MarriageAction.Apply(c.Groom, c.Bride, true); } catch (Exception e) { Stumble("RunClaim(slub)", e); }
                if (c.Groom.Spouse != c.Bride)
                {
                    // recenzja: tytul, ktory nie wrocil, zostaje u gracza - liczy sie jako przeniesiony (bez zwrotu zlota za niego), zwrot tylko za te, ktore wrocily
                    int back = 0; double backBase = 0;
                    for (int i = 0; i < c.Titles.Count; i++)
                    {
                        var t = c.Titles[i];
                        if (!moved.Contains(t)) continue;
                        if (LandBk.Inherit(me, h, t) && LandBk.DeJure(t) == h) { back++; backBase += c.Base[i]; }
                    }
                    int notBack = moved.Count - back;
                    wed = "SLUB NIE DOSZEDL - tytuly zwrocone " + back + "/" + moved.Count + (notBack > 0 ? ", nie wrocily " + notBack + " - zostaja, bez zwrotu za nie" : "");
                    r.Failed += back; r.Moved = notBack; failBase += backBase;
                }
                else
                {
                    wed = "slub " + Name(c.Groom) + " + " + Name(c.Bride) + " (rod po slubie " + (c.Bride.Clan != null ? c.Bride.Clan.Name.ToString() : "-") + ")";
                    try { ChangeRelationAction.ApplyPlayerRelation(h, 10); } catch (Exception e) { Stumble("RunClaim(relacja)", e); }
                    BrideHome(c.Bride);
                }
            }
            // zwrot czesci zaplaty za tytuly, ktore nie przeszly (najwyzej tyle, ile posiadacz ma)
            if (r.Paid > 0 && r.Failed > 0)
            {
                long due = r.Moved == 0 ? r.Paid : (long)Math.Round(r.Paid * (c.Sum > 0 ? failBase / c.Sum : 0));
                if (due > 0) { r.Back = PayLand(h, me, due, "RunClaim(zwrot c)"); r.Paid -= r.Back; }
            }
            long cred = 0;
            if (r.Paid > 0 && h.Clan != null && h.Clan.Leader == h)
            {
                try { cred = DebtLadder.LandProceeds(h.Clan, r.Paid); } catch (Exception e) { Stumble("RunClaim(wierzyciele)", e); }
            }
            r.Pl = Name(h) + ": " + (p.Mode == 'M' ? "slub" : "zloto") + " " + r.Paid + " (kiesa gracza -> posiadacz" + (cred > 0 ? ", wierzycielom " + cred : "") + ")"
                 + (r.Back > 0 ? ", zwrot " + r.Back : "") + " | tytuly przeniesione " + r.Moved + "/" + c.Titles.Count + (wed.Length > 0 ? " | " + wed : "");
            return r;
        }

        /// <summary>Panna po slubie w rodzie gracza: gdy bez partii i w osadzie wroga (albo w drodze) - do twierdzy rodu gracza (nie oblezonej).</summary>
        private static void BrideHome(Hero b)
        {
            try
            {
                if (b == null || b.Clan != Clan.PlayerClan || b.PartyBelongedTo != null) return;
                var cur = b.CurrentSettlement;
                if (cur != null && !FactionManager.IsAtWarAgainstFaction(cur.MapFaction, Hero.MainHero.MapFaction)) return;
                Settlement to = null;
                foreach (var st in Clan.PlayerClan.Settlements) if (st != null && (st.IsTown || st.IsCastle) && !st.IsUnderSiege) { to = st; break; }
                if (to != null) TeleportHeroAction.ApplyImmediateTeleportToSettlement(b, to);
            }
            catch (Exception e) { Stumble("BrideHome", e); }
        }

        private static void RunV(Deal d, Hero h)
        {
            var me = Hero.MainHero;
            var oc = OtherCrown(h);
            if (oc != null) { Drop(d, h, "he is now the captive of " + oc.Name + ", a crown you no longer serve.", "jeniec korony " + oc.Name + ", ktorej gracz juz nie sluzy"); return; }
            if (!ClaimsOn) { Drop(d, h, "the claims of his lords can no longer be settled.", "klucz CaptiveTermsVassalTitles albo BK wylaczone"); return; }
            string en, pl;
            var pkg = Recheck(d, h, out en, out pl);
            if (pkg == null) { Drop(d, h, en, pl); return; }
            bool need; long owed, cut;
            var crown = LiegeOwed(h, out need, out owed, out cut);
            if (need && d.Mode == Liege.None) { Drop(d, h, "your liege now has a claim to his ransom - speak to him again.", "pan traci okup, a w rozmowie nie bylo wyboru (stan zmienil sie po rozmowie)"); return; }
            if (need && d.Mode == Liege.Pay)
            {
                if (d.Owed > 0 && Math.Abs(owed - d.Owed) > 0.05 * d.Owed) { Drop(d, h, "the ransom your liege is owed has changed (" + owed.ToString("N0", Inv) + " denars now) - speak to him again.", "dlug wobec pana zmienil sie: umowa " + d.Owed + ", teraz " + owed); return; }
                if (d.Owed > 0) owed = d.Owed;
            }
            long gold = GoldOf(pkg) + (need && d.Mode == Liege.Pay ? owed : 0);
            if (me.Gold < gold) { Drop(d, h, "you no longer have " + gold.ToString("N0", Inv) + " denars.", "za malo zlota gracza (" + me.Gold + " < " + gold + ")"); return; }
            long pg0 = me.Gold;
            // tytuly krola i jego rodu (366 T, za darmo)
            string names; var titles = TitlesFor(h, out names);
            int moved = 0, failed = 0;
            var movedL = new List<object>();   // 367d: tytuly pod wlasna pieczecia krola - sila urazy
            foreach (var t in titles) { var from = LandBk.DeJure(t); if (from != null && LandBk.Inherit(from, me, t) && LandBk.DeJure(t) == me) { moved++; movedL.Add(t); } else failed++; }
            var lines = new List<string>(); var got = new List<string>(); var wed = new List<string>();
            foreach (var p in pkg)
            {
                Done r;
                try { r = RunClaim(p); }
                catch (Exception e) { Stumble("RunV(posiadacz)", e); r = new Done { Pl = Name(p.C.Holder) + ": wyjatek", Failed = p.C.Titles.Count }; }
                lines.Add(r.Pl);
                moved += r.Moved; failed += r.Failed;
                if (r.Moved > 0)
                {
                    bool wedded = p.Mode == 'M' && p.C.Groom.Spouse == p.C.Bride;
                    got.Add(Name(p.C.Holder) + (p.Mode == 'M' ? (wedded ? " (" + (p.C.Groom == me ? "you wed " : GroomWord(p.C.Groom) + " weds ") + Name(p.C.Bride) + ")" : " (the match failed)")
                                                              : " (" + (r.Paid).ToString("N0", Inv) + " denars)"));
                    if (wedded) wed.Add(Name(p.C.Bride));
                }
            }
            if (moved == 0)
            {
                Drop(d, h, "neither his seal nor his lords could confirm your conquests. He stays your captive.", "BK nie przenioslo zadnego tytulu (" + failed + ") - zaplaty zwrocone, bez uwolnienia | " + string.Join("; ", lines.ToArray()));
                return;
            }
            string liege = SettleLiege(d, crown, need, owed);
            long cancelled = crown != null && crown.Leader != me ? Ransom178.CancelReward(me, crown, cut) : 0;
            CaptiveGrudge.TermsBegin(h, 'V', movedL);   // 367d
            try { EndCaptivityAction.ApplyByReleasedByChoice(h, me); } catch { CaptiveGrudge.TermsAbort(); throw; }
            Log.Info(Tag367 + ": WYKONANE - " + Name(h) + " | pieczec krola: " + titles.Count + " (" + names + ") | " + string.Join(" || ", lines.ToArray())
                     + " | razem tytulow " + moved + (failed > 0 ? ", nieudane " + failed : "") + " | kiesa gracza " + pg0 + " -> " + me.Gold + " | " + liege
                     + (cancelled > 0 ? " | nagroda gracza skreslona " + cancelled : "") + " | uwolniony (z wyboru).");
            Log.Player(Name(h) + " goes free. His seal and his lords' quitclaims confirm your conquests" + (got.Count > 0 ? " - " + Join(got) : "") + (failed > 0 ? " (" + failed + " titles could not be confirmed)" : "") + "."
                       + (wed.Count > 0 ? " The marriage is sealed: " + Join(wed) + " joins your house." : ""));
            CaptiveGrudge.TermsEnd(h);   // 367d
        }
    }
}
