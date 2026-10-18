using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS RUNDA 4 - PACZKA r4-bkwies (PLAN-KS-RUNDA4 rozdz. 6 B1-B4; Jeff 17.10 "ok": rynek wsi BK wedlug potrzeb - sol, zelazo na narzedzia, sukno/len, garnki; zbytki
    /// tylko wies bogata; tylko z nadwyzki ponad skrzynie; diagnoza fin80 "sol": krok 5 DS kupowal sol i narzedzia bez limitu potrzeby i poza ramka ksiegi towarow).
    ///
    /// ZAPAS POTRZEB WSI (jedna zasada na jedno zjawisko). Wies zuzywa sol, narzedzia, plotno, sukno i garnki wedlug norm JEDNEJ tabeli swiata, stan chlopow:
    /// sol 0.013 kg na glowe na dobe (DailyLife.Basket Base[chlopi][S]); plotno 1.2 i sukno 0.8 kg na rok - polowa z targu, polowa przedziona w domu (KsTab.NonFood,
    /// KsCore.NeedsOfKeep); narzedzia 0.4 i garnki 1.0 kg na rok (KsTab.NonFood); zelazo kowala wiejskiego (towar "iron") liczy sie jako narzedzia / 1.2 (KsTab.ToolsIron).
    /// Glowy - te same wolne glowy co koszyk dnia i czynsz (TownFolk.NeedHeads = VillHeads). Licznik "w domu" (kg) kazdej wsi: kazdy zakup tych towarow (koszyk dnia
    /// i woz E3-W1 - TownFolk.BuyGoods, lista dostatku P5 - BuyLadder, krok 5, rynek BK) dodaje, kazda doba zjada potrzebe dnia (leniwie, przy odczycie - bez petli
    /// po wsiach). Krok 5 (DsSurplusNeedLimit) i rynek BK (KsBkVillageNeeds) kupuja najwyzej do VillageNeedCoverDays dni potrzeby. Licznik tylko na sesje (pierwsze
    /// dotkniecie wsi w sesji = zapas pelny, bez skoku zakupow po wczytaniu), bez zapisu. To licznik potrzeby, nie towar: towar znika z polki przy zakupie (zjedli go
    /// i zuzyli wiesniacy - zaplacili), jak dotad.
    /// Linia doby "Potrzeby wsi (B1): dzien N" (z BkSeal34.Daily, po ksiedze pieniadza) - tozsamosci: kiesy wsi -> kasy = potrzeby + zbytki, kontrola platnicy - odbiorcy = 0.
    /// Wyjatki liczone per obiekt (potkniecia), bez globalnych wylacznikow.
    /// </summary>
    internal static class VillNeeds
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int GSalt = 0, GTools = 1, GLinen = 2, GFelt = 3, GPots = 4, NG = 5;
        private static readonly string[] GName = { "sol", "narzedzia", "plotno", "sukno", "garnki" };
        private const double SaltKgDay = 0.013;                 // DailyLife.Basket Base[chlopi][S] (kg/glowe/dobe)
        // zbytki (tylko wies bogata): wino, przyprawy, klejnoty, aksamit, futra
        private static readonly HashSet<string> LuxIds = new HashSet<string>(StringComparer.Ordinal) { "wine", "spice", "jewelry", "velvet", "fur" };

        // ------------------------------------------------------------ przelaczniki
        internal static bool BkOn { get { var s = Settings.Current; return s != null && s.KsBkVillageNeeds; } }
        internal static bool SkipBasketOn { get { var s = Settings.Current; return s != null && s.KsSurplusSkipsBasket; } }
        internal static bool LimitOn { get { var s = Settings.Current; return s != null && s.DsSurplusNeedLimit; } }
        internal static bool FrameOn { get { var s = Settings.Current; return s != null && s.DsSurplusLedgerFrame; } }
        internal static bool ChestKsOn { get { var s = Settings.Current; return s != null && s.KsChestPaidBasket; } }
        internal static int CoverDays { get { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(91, s.VillageNeedCoverDays)) : 7; } }
        internal static double RichChests { get { var s = Settings.Current; return s != null && !float.IsNaN(s.KsBkRichChests) ? Math.Max(1.0, Math.Min(20.0, (double)s.KsBkRichChests)) : 3.0; } }
        /// <summary>Licznik zapasu potrzeb czynny (ktorys z kluczy, ktore go czytaja, i kiesa ludu) - inaczej zakupy go nie karmia (stara droga bez zmian).</summary>
        internal static bool Active { get { var s = Settings.Current; return s != null && (s.KsBkVillageNeeds || s.DsSurplusNeedLimit) && TownFolk.Live; } }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        private static double NormKg(int g)
        {
            var nf = KsTab.NonFood[KsTab.EPeasant];   // plotno, sukno, skora, narzedzia, garnki, ... (kg na rok)
            switch (g)
            {
                case GSalt: return SaltKgDay;
                case GTools: return nf[3] / 364.0;
                case GLinen: return nf[0] * 0.5 / 364.0;
                case GFelt: return nf[1] * 0.5 / 364.0;
                case GPots: return nf[4] / 364.0;
            }
            return 0;
        }

        /// <summary>Grupa potrzeb towaru (-1 - poza potrzebami) i kg potrzeby na sztuke (waga towaru; zelazo / 1.2 - narzedzia z kowala).</summary>
        internal static int GroupOf(ItemObject it, out double kgPc)
        {
            kgPc = 10.0;
            var c = it != null ? it.ItemCategory : null;
            if (c == null || c.StringId == null) return -1;
            int g;
            switch (c.StringId)
            {
                case "salt": g = GSalt; break;
                case "tools": g = GTools; break;
                case "linen": g = GLinen; break;
                case "felt": g = GFelt; break;
                case "pottery": g = GPots; break;
                case "iron": if (it.StringId != "iron") return -1; g = GTools; break;   // tylko surowiec zelaza (bez sztab kuzni)
                default: return -1;
            }
            double w = it.Weight > 0.01f ? it.Weight : 10.0;
            kgPc = c.StringId == "iron" ? w / KsTab.ToolsIron : w;
            return g;
        }

        /// <summary>Towar zbytku (wino, przyprawy, klejnoty, aksamit, futra) - rynek BK tylko dla wsi bogatej.</summary>
        internal static bool IsLux(ItemObject it)
        {
            if (it == null) return false;
            var c = it.ItemCategory;
            return (c != null && c.StringId != null && LuxIds.Contains(c.StringId)) || (it.StringId != null && LuxIds.Contains(it.StringId));
        }

        // ------------------------------------------------------------ licznik "w domu" (sesja)
        private sealed class Cover { public readonly double[] Kg = new double[NG]; public int Day = -1; }
        private static readonly Dictionary<Village, Cover> _cov = new Dictionary<Village, Cover>();
        private static Campaign _camp;
        private static int _stumbles, _stumblesSess;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSess++;
            try { if (_errOnce.Add(where)) Log.Error("VillNeeds." + where, e); } catch { }
        }

        private static Cover CoverOf(Village v, double heads, int today)
        {
            var cur = Campaign.Current;
            if (!ReferenceEquals(cur, _camp)) { _cov.Clear(); _camp = cur; _stumblesSess = 0; _errOnce.Clear(); }
            Cover c;
            if (!_cov.TryGetValue(v, out c))
            {
                c = new Cover { Day = today };
                int cd = CoverDays;
                for (int g = 0; g < NG; g++) c.Kg[g] = cd * heads * NormKg(g);   // pierwsze dotkniecie w sesji - zapas pelny (bez skoku zakupow po wczytaniu)
                _cov[v] = c;
                return c;
            }
            int dt = today - c.Day;
            if (dt > 0)
            {
                for (int g = 0; g < NG; g++) c.Kg[g] = Math.Max(0.0, c.Kg[g] - dt * heads * NormKg(g));
                c.Day = today;
            }
            else if (dt < 0) c.Day = today;
            return c;
        }

        /// <summary>Zakup towaru potrzeb przez wies (dowolna droga) - do licznika "w domu". Klucze wylaczone - nic. Nie rzuca.</summary>
        internal static void Note(Village v, ItemObject it, int n)
        {
            if (v == null || it == null || n <= 0) return;
            try
            {
                if (!Active) return;
                double kgPc; int g = GroupOf(it, out kgPc);
                if (g < 0) return;
                double heads = TownFolk.NeedHeads(v.Settlement);
                if (!(heads > 0)) return;
                var c = CoverOf(v, heads, Today);
                c.Kg[g] += n * kgPc;
            }
            catch (Exception e) { Stumble("Note", e); }
        }

        /// <summary>Ile sztuk towaru potrzeb wies moze dzis kupic ponad to, co ma w domu (do VillageNeedCoverDays dni potrzeby; sztuka w gore - narzedzia zuzywaja
        /// sie powoli). int.MaxValue - towar poza potrzebami; 0 - zapas pelny albo wies bez glow (nieskalibrowana).</summary>
        internal static int Room(Village v, ItemObject it)
        {
            try
            {
                double kgPc; int g = GroupOf(it, out kgPc);
                if (g < 0) return int.MaxValue;
                if (v == null) return 0;
                double heads = TownFolk.NeedHeads(v.Settlement);
                if (!(heads > 0)) return 0;
                var c = CoverOf(v, heads, Today);
                double want = CoverDays * heads * NormKg(g) - c.Kg[g];
                if (!(want > 1e-9) || !(kgPc > 1e-9)) return 0;
                return (int)Math.Min(100000.0, Math.Ceiling(want / kgPc - 1e-9));
            }
            catch (Exception e) { Stumble("Room", e); return 0; }
        }

        // ------------------------------------------------------------ skrzynia z koszyka Planu (B2 / M9)
        /// <summary>DsVillage.ChestOf / RentPay (klucz Ks Chest Paid Basket): koszyk dnia wsi do skrzyni - przy KS (wies z koszykiem ZC4) lambda x koszyk zaplacony z Planu
        /// (zl/d, Zc4VillagePaid) + (1 - lambda) x stary koszyk dnia (w rampie stara lista idzie x (1 - lambda)); inaczej `nvOld` bez zmian. Nie rzuca.</summary>
        internal static double ChestBasket(Settlement st, double nvOld)
        {
            try
            {
                if (st == null || !ChestKsOn) return nvOld;
                var v = st.Village;
                if (v == null) return nvOld;
                double lam;
                if (StartCalibration.Zc4VillageNeed(v, out lam) == null || !(lam > 0)) return nvOld;
                double bud;
                if (StartCalibration.Zc4VillagePaid(v, out bud) == null || !(bud >= 0) || double.IsInfinity(bud)) return nvOld;
                lam = Math.Min(1.0, lam);
                return lam * bud + (1.0 - lam) * Math.Max(0.0, nvOld);
            }
            catch (Exception e) { Stumble("ChestBasket", e); return nvOld; }
        }

        // ------------------------------------------------------------ liczniki doby
        private static int _bTicks, _bTicksDs, _bTicksKs, _bRich, _bLuxVil, _bSkipBasket, _bPoor, _bChestStop, _bViol, _bFoodZc4;
        private static long _bSpent, _bNeedZl, _bLuxZl, _bLuxPcs, _bRefused, _bOut, _bIn, _bTk;
        private static double _bBudget; private static long _bMinMargin = long.MaxValue;
        private static readonly long[] _bPcs = new long[NG];
        private static int _sSkipN, _sLimN, _sRefused, _sFrames; private static double _sSkipZl, _sKeep; private static long _sZl, _sTk;
        private static readonly long[] _sPcs = new long[NG];
        private static int _cN; private static double _cOld, _cNew;

        private static void Clear()
        {
            _bTicks = _bTicksDs = _bTicksKs = _bRich = _bLuxVil = _bSkipBasket = _bPoor = _bChestStop = _bViol = _bFoodZc4 = 0;
            _bSpent = _bNeedZl = _bLuxZl = _bLuxPcs = _bRefused = _bOut = _bIn = _bTk = 0; _bBudget = 0; _bMinMargin = long.MaxValue;
            Array.Clear(_bPcs, 0, NG);
            _sSkipN = _sLimN = _sRefused = _sFrames = 0; _sSkipZl = _sKeep = 0; _sZl = _sTk = 0;
            Array.Clear(_sPcs, 0, NG);
            _cN = 0; _cOld = _cNew = 0;
            _stumbles = 0;
        }

        /// <summary>BkSeal34.RunVillageNeeds: tick rynku BK wsi pod nowa regula (budzet dnia, wies bogata).</summary>
        internal static void BkTick(bool ds, bool ks, double budget, bool rich)
        {
            _bTicks++; if (ds) _bTicksDs++; else if (ks) _bTicksKs++;
            if (budget > 0 && !double.IsInfinity(budget)) _bBudget += budget;
            if (rich) _bRich++;
        }

        /// <summary>BkSeal34.RunVillageNeeds: zakup (n sztuk za zl; g - grupa potrzeb albo -1 dla zbytku; foodZc4 - jedzenie u wsi z koszykiem ZC4 - bramka 0).</summary>
        internal static void BkBought(Village v, ItemObject it, int g, int n, long zlOut, long zlIn, bool foodZc4)
        {
            _bSpent += zlOut; _bOut += zlOut; _bIn += zlIn;
            if (g >= 0) { _bNeedZl += zlOut; _bPcs[g] += n; Note(v, it, n); }
            else { _bLuxZl += zlOut; _bLuxPcs += n; }
            if (foodZc4) _bFoodZc4++;
        }
        internal static void BkLuxVillage() { _bLuxVil++; }
        internal static void BkRefused(int n) { if (n > 0) _bRefused += n; }
        internal static void BkSkipBasket() { _bSkipBasket++; }
        internal static void BkPoor() { _bPoor++; }
        internal static void BkChestStop() { _bChestStop++; }
        /// <summary>Po zakupach wsi w ticku: zapas kiesy ponad skrzynie (bramka NKS-29: nigdy ponizej) - najmniejszy zapas doby i liczba naruszen.</summary>
        internal static void BkMargin(long margin) { if (margin < 0) _bViol++; if (margin < _bMinMargin) _bMinMargin = margin; }
        internal static void BkTime(long ticks) { if (ticks > 0) _bTk += ticks; }

        /// <summary>TownFolk.DsSurplus: wies z koszykiem ZC4 - krok 5 bez listy wsi (budzet dalej do robot gminy / kiesy).</summary>
        internal static void S5Skip(double zl) { _sSkipN++; if (zl > 0) _sSkipZl += zl; }
        /// <summary>TownFolk.DsBuyNeeds: krok 5 do potrzeby - sztuki wedlug grupy, zl, odmowione ponad zapas i zl zostawione w kiesie.</summary>
        internal static void S5Limit(int[] pcs, long zl, int refused, double keep)
        {
            _sLimN++; _sZl += Math.Max(0, zl); _sRefused += Math.Max(0, refused); if (keep > 0) _sKeep += keep;
            if (pcs != null) for (int g = 0; g < NG && g < pcs.Length; g++) _sPcs[g] += pcs[g];
        }
        internal static void S5Frame() { _sFrames++; }
        internal static void S5Time(long ticks) { if (ticks > 0) _sTk += ticks; }
        /// <summary>TownFolk.DsSurplus: koszyk dnia do skrzyni stary i z koszyka Planu (licznik linii; raz na wies i dobe).</summary>
        internal static void NoteChest(double nvOld, double nvNew) { if (Math.Abs(nvNew - nvOld) > 1e-9) { _cN++; _cOld += nvOld; _cNew += nvNew; } }

        private static string N0(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v).ToString("0", Inv); }
        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        /// <summary>BkSeal34.Daily (po ksiedze pieniadza): linia "Potrzeby wsi (B1)" za dobe i zerowanie licznikow. Klucze wylaczone - bez linii.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                bool any = s.KsBkVillageNeeds || s.KsSurplusSkipsBasket || s.DsSurplusNeedLimit || s.DsSurplusLedgerFrame || s.KsChestPaidBasket;
                if (!any) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                var sb = new StringBuilder(1400);
                sb.Append("Potrzeby wsi (B1): dzien ").Append(day)
                  .Append(" | klucze: rynek BK wedlug potrzeb ").Append(YN(s.KsBkVillageNeeds)).Append(", krok 5 bez koszyka ZC4 ").Append(YN(s.KsSurplusSkipsBasket))
                  .Append(", krok 5 do potrzeby ").Append(YN(s.DsSurplusNeedLimit)).Append(", ramka ksiegi kroku 5 ").Append(YN(s.DsSurplusLedgerFrame))
                  .Append(", skrzynia z koszyka Planu ").Append(YN(s.KsChestPaidBasket)).Append(", zapas potrzeb ").Append(CoverDays).Append(" dni, wies bogata od ")
                  .Append(RichChests.ToString("0.0", Inv)).Append(" skrzyn, wsi z licznikiem zapasu ").Append(_cov.Count)
                  .Append(" | rynek wsi BK wedlug potrzeb: ticki ").Append(_bTicks).Append(" (DS ").Append(_bTicksDs).Append(", KS bez DS ").Append(_bTicksKs).Append("), budzet ").Append(N0(_bBudget))
                  .Append(" zl, kiesy wsi -> kasy ").Append(_bSpent).Append(" zl = potrzeby ").Append(_bNeedZl).Append(" zl + zbytki ").Append(_bLuxZl).Append(" zl; potrzeby szt.:");
                for (int g = 0; g < NG; g++) sb.Append(g == 0 ? " " : ", ").Append(GName[g]).Append(' ').Append(_bPcs[g]);
                sb.Append("; zbytki ").Append(_bLuxPcs).Append(" szt. w ").Append(_bLuxVil).Append(" wsiach (tickow wsi bogatych ").Append(_bRich).Append(")")
                  .Append("; zapas potrzeb w domu pelny ").Append(_bRefused).Append(" wierszy; pominiete: towary koszyka ZC4 ").Append(_bSkipBasket).Append(" wierszy, za drogo na budzet ").Append(_bPoor)
                  .Append(", stop na skrzyni ").Append(_bChestStop).Append("; jedzenie kupione u wsi ZC4 ").Append(_bFoodZc4)
                  .Append("; kiesa ponizej skrzyni po zakupie ").Append(_bViol).Append("; najmniejszy zapas ponad skrzynie ").Append(_bMinMargin == long.MaxValue ? "-" : _bMinMargin.ToString(Inv)).Append(" zl")
                  .Append("; kontrola kiesy wsi - kasy = ").Append(_bOut - _bIn)
                  .Append(" | krok 5 (DS): wsi z koszykiem ZC4 bez listy wsi ").Append(_sSkipN).Append(" (budzet dalej do robot gminy ").Append(N0(_sSkipZl)).Append(" zl)")
                  .Append(", wsi do potrzeby ").Append(_sLimN).Append(": sol ").Append(_sPcs[GSalt]).Append(" + narzedzia ").Append(_sPcs[GTools]).Append(" szt. za ").Append(_sZl).Append(" zl")
                  .Append(", ponad potrzebe odmowione ").Append(_sRefused).Append(" szt., zostalo w kiesach ").Append(N0(_sKeep)).Append(" zl, ramek ksiegi ").Append(_sFrames)
                  .Append(" | skrzynia z koszyka Planu: wsi ").Append(_cN).Append(", koszyk dnia stary ").Append(N0(_cOld)).Append(" zl -> skrzyniowy ").Append(N0(_cNew)).Append(" zl")
                  .Append(" | czas: rynek BK ").Append((_bTk * f).ToString("0.00", Inv)).Append(" ms, krok 5 ").Append((_sTk * f).ToString("0.00", Inv)).Append(" ms");
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumblesSess).Append(')');
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { Clear(); }
        }
    }
}
