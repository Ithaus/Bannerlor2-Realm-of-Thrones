using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// E3-D2 (noc-ekonomia3 16.10, czesc 2; klucz WorkshopPaceByMarket) - TEMPO LINII WARSZTATU: LUDZIE RYNKU I CENA.
    /// Dowod [L] (test noc-ekonomia2 21-25-46 d109195): "Warsztaty towarowe" - 291 warsztatow, pracowalo 170, piekarnie 73 (324 cykle), kuznie 29 (44 cykle
    /// towarowe); "Towary": narzedzia produkcja 184/d (smithy 176) przy zuzyciu 210/d, chleb 888/d przy zuzyciu 921/d. Tempo linii jest takie samo w miescie
    /// na 5 tys. i na 60 tys. ludzi i przy kazdej cenie wyrobu - tylko rece broni ida za ludzmi (WorkshopLaw.Hands :821, m_t z PeopleMarket.WorkersScale).
    /// Regula: kazda linia TOWAROWA (WorkshopTrade.IsTradeLine) z wsadem warsztatu notabla w MIESCIE biegnie w tempie gry x F,
    ///   F = clamp(m_t / srednia m_t warsztatow towarowych miast, 0.3, 4) x clamp(sqrt(indeks glownego wyrobu linii w tym miescie), 0.7, 1.4), najwyzej 4.
    /// Mnoznik NA LINIE, nie na typ: kuznia (smithy, SandBox spworkshops.xml :782) ma w jednym typie bron, zbroje, narzedzia i war_horse - linie broni
    /// (nie towarowe) i ukryci rzemieslnicy bez zmian. Bez zmian tez: linia bez wsadu (kopalnie - WorkshopLaw.BulkLineSkip, NoTownMines), linia strzal zamknieta
    /// (TownFletchers.ClosesLine), zamki (BK TickCastle wola RunTownWorkshop sam - DayProfile.cs:40), warsztaty gracza (decyzja: "warsztatow gracza nie
    /// przestawiamy" - ekran magazynu GetInputDailyChange pokazuje tempo bez F).
    /// Kod [K]: gra wola GetEffectiveConversionSpeedOfProduction(warsztat, szybkosc, false) RAZ na linie w RunTownWorkshop, po kolei od linii 0
    /// (twall WorkshopsCampaignBehavior.cs:1390-1398), a petla while dopuszcza kilka cykli na dobe. WorkshopTrade.RunPrefix/RunPostfix (prefiks i postfiks
    /// RunTownWorkshop) ustawiaja tu warsztat i licznik linii; postfiks na metode szybkosci KLASY DEKLARUJACEJ czynnego modelu (NavalDLCWorkshopModel wola
    /// BaseModel raz - latka tylko na zewnetrznej klasie, bez podwojenia), zakladany w kampanii (model znany dopiero w kampanii). Licznik linii po RunTownWorkshop
    /// musi sie zgadzac z liczba linii typu - inaczej (cudzy kod wola szybkosc w srodku) warsztat wypada z reguly na sesje i licznik "niezgodne" rosnie.
    /// Kto placi, kto dostaje: bez nowego przeplywu. Wiecej cykli = wiecej wsadu kupionego z polki (kapital warsztatu -> kasa miasta, latka BK) i wiecej plac cyklu
    /// (WorkshopTrade.CyclePostfix: kapital -> kasa miasta / kiesa ludu) - place cyklu bez zmian na cykl, wiec place dnia x F (jak wiecej najetych ludzi).
    /// Nic w zapisie (F liczone od nowa z dzisiejszych cen i ludzi). Wyjatek per warsztat - licznik potkniec, warsztat w tempie gry.
    /// </summary>
    internal static class WorkshopPace
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject Txt = new TextObject("{=!}Armoury: hands and prices of the market");
        private const float MMin = 0.3f, MMax = 4f, PMin = 0.7f, PMax = 1.4f, FMax = 4f;

        // biezacy warsztat w RunTownWorkshop (gra wola cykle w jednym watku, po kolei)
        private static Workshop _shop;
        private static int _idx;
        private static float[] _f = new float[0];
        private static string _type;

        private static readonly HashSet<Workshop> _bad = new HashSet<Workshop>();          // niezgodny licznik linii - poza regula do konca sesji
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static MethodBase _target; private static string _targetName = "nie wpieta"; private static bool _tried;
        private static int _avgDay = int.MinValue; private static float _avgM = 1f; private static int _avgN;

        // liczniki doby
        private sealed class TStat { public int Shops, Lines; public double Before, After; }
        private static readonly Dictionary<string, TStat> _dType = new Dictionary<string, TStat>();
        private static readonly int[] _bins = new int[6];                                  // F: <0.7, 0.7-0.95, 0.95-1.05, 1.05-1.5, 1.5-2.5, >2.5
        private static int _dShops, _dLines, _dSame, _dCastle, _dPlayer, _dMismatch, _dStumbles, _sessMismatch, _sessStumbles;
        private static double _dFSum, _dFMin = double.NaN, _dFMax = double.NaN, _dBefore, _dAfter, _dMSum, _dPSum;
        private static bool _startDone;

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopPaceByMarket; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        internal static void Reset()
        {
            _shop = null; _idx = 0; _type = null; _bad.Clear(); _err.Clear();
            _avgDay = int.MinValue; _avgM = 1f; _avgN = 0; _sessMismatch = _sessStumbles = 0; _startDone = false;
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _dType.Clear(); for (int i = 0; i < _bins.Length; i++) _bins[i] = 0;
            _dShops = _dLines = _dSame = _dCastle = _dPlayer = _dMismatch = _dStumbles = 0;
            _dFSum = 0; _dFMin = _dFMax = double.NaN; _dBefore = _dAfter = _dMSum = _dPSum = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _sessStumbles++;
            try { if (_err.Add(where)) Log.Error("WorkshopPace." + where, e); } catch { }
        }

        /// <summary>Srednia m_t warsztatow towarowych miast (raz na dobe, leniwie): warsztat nieukryty z choc jedna linia towarowa z wsadem.</summary>
        private static void EnsureAvg()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _avgDay) return;
            _avgDay = day;
            double sum = 0; int n = 0;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null || !t.Settlement.IsTown || t.Workshops == null) continue;
                float m = PeopleMarket.WorkersScale(t);
                if (!(m > 0f) || float.IsInfinity(m)) m = 1f;
                foreach (var w in t.Workshops)
                {
                    if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                    if (!HasTradeLine(w.WorkshopType)) continue;
                    sum += m; n++;
                }
            }
            _avgN = n;
            _avgM = n > 0 && sum > 0 ? (float)(sum / n) : 1f;
        }

        private static bool HasTradeLine(WorkshopType t)
        {
            foreach (var p in t.Productions) if (Paced(p)) return true;
            return false;
        }

        /// <summary>Linia pod regula: towarowa i z wsadem (bez kopalni - linia bez wsadu).</summary>
        private static bool Paced(WorkshopType.Production p)
        {
            return p.Inputs != null && p.Inputs.Count > 0 && WorkshopTrade.IsTradeLine(p);
        }

        /// <summary>Z WorkshopTrade.RunPrefix (przed RunTownWorkshop): mnozniki linii tego warsztatu na ten tick (raz na warsztat i dobe - gra wola RunTownWorkshop raz na dobe).</summary>
        internal static void Begin(Workshop w)
        {
            _shop = null; _idx = 0; _type = null;
            try
            {
                if (!On || !Live || _target == null || w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) return;
                var st = w.Settlement;
                if (st == null) return;
                if (!st.IsTown || st.Town == null) { if (st.IsCastle) _dCastle++; return; }
                if (w.Owner == null) return;
                if (w.Owner == Hero.MainHero) { _dPlayer++; return; }
                if (_bad.Contains(w)) return;
                var prods = w.WorkshopType.Productions;
                int n = prods.Count;
                if (n <= 0) return;
                EnsureAvg();
                var town = st.Town;
                float m = PeopleMarket.WorkersScale(town);
                if (!(m > 0f) || float.IsInfinity(m)) m = 1f;
                float mp = MBMath.ClampFloat(m / Math.Max(0.01f, _avgM), MMin, MMax);
                if (_f.Length < n) _f = new float[n];
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    var p = prods[i];
                    float f = 1f;
                    if (Paced(p) && !TownFletchers.ClosesLine(p, w))
                    {
                        float idx = 1f;
                        try { var o = p.Outputs[0].Item1; if (o != null) idx = town.GetItemCategoryPriceIndex(o); } catch { idx = 1f; }
                        if (!(idx > 0f) || float.IsInfinity(idx)) idx = 1f;
                        float pp = MBMath.ClampFloat((float)Math.Sqrt(idx), PMin, PMax);
                        f = MBMath.ClampFloat(mp * pp, MMin, FMax);   // razem 0.3-4 (progi NE3-23)
                        _dMSum += mp; _dPSum += pp;
                        any = true;
                    }
                    _f[i] = f;
                }
                if (!any) return;
                _shop = w; _type = w.WorkshopType.StringId ?? "?";
                _dShops++;
                TStat ts;
                if (!_dType.TryGetValue(_type, out ts)) { ts = new TStat(); _dType[_type] = ts; }
                ts.Shops++;
            }
            catch (Exception e) { _shop = null; Stumble("Begin", e); }
        }

        /// <summary>Z WorkshopTrade.RunPostfix: koniec ticku warsztatu - licznik linii musi byc rowny liczbie linii typu.</summary>
        internal static void End(Workshop w)
        {
            try
            {
                if (_shop == null) return;
                var shop = _shop; _shop = null;
                if (!ReferenceEquals(shop, w) || shop.WorkshopType == null) return;
                if (_idx != shop.WorkshopType.Productions.Count)
                {
                    _dMismatch++; _sessMismatch++;
                    if (_bad.Add(shop) && _sessMismatch <= 3)
                        Log.Info("Tempo warsztatow (E3-D2): NIEZGODNY licznik linii w " + (shop.Settlement != null ? shop.Settlement.StringId : "?") + " (" + _type + "): wywolan szybkosci " + _idx
                                 + ", linii " + shop.WorkshopType.Productions.Count + " - warsztat w tempie gry do konca sesji.");
                }
            }
            catch (Exception e) { Stumble("End", e); }
        }

        /// <summary>Postfiks GetEffectiveConversionSpeedOfProduction (klasa deklarujaca czynnego modelu): w ticku warsztatu pod regula - linia _idx x F.</summary>
        public static void SpeedPostfix(Workshop __0, ref ExplainedNumber __result)
        {
            if (_shop == null || !ReferenceEquals(__0, _shop)) return;
            try
            {
                int i = _idx++;
                if (i >= _f.Length || i >= _shop.WorkshopType.Productions.Count) return;   // nadmiar - End policzy niezgodnosc
                float f = _f[i];
                if (Paced(_shop.WorkshopType.Productions[i]) && !TownFletchers.ClosesLine(_shop.WorkshopType.Productions[i], _shop))
                {
                    double before = __result.ResultNumber;
                    if (Math.Abs(f - 1f) >= 0.001f)
                    {
                        float sum = 1f + __result.SumOfFactors;
                        if (sum > 0f) __result.AddFactor((f - 1f) * sum, Txt);
                    }
                    double after = __result.ResultNumber;
                    _dLines++; _dBefore += before; _dAfter += after;
                    _dFSum += f;
                    if (double.IsNaN(_dFMin) || f < _dFMin) _dFMin = f;
                    if (double.IsNaN(_dFMax) || f > _dFMax) _dFMax = f;
                    _bins[f < 0.7f ? 0 : f < 0.95f ? 1 : f <= 1.05f ? 2 : f <= 1.5f ? 3 : f <= 2.5f ? 4 : 5]++;
                    TStat ts;
                    if (_type != null && _dType.TryGetValue(_type, out ts)) { ts.Lines++; ts.Before += before; ts.After += after; }
                }
                else _dSame++;
            }
            catch (Exception e) { Stumble("SpeedPostfix", e); }
        }

        /// <summary>Z WorkshopTradeBehavior.OnSessionLaunched (raz na proces): postfiks na metode szybkosci klasy, ktora ja DEKLARUJE w lancuchu czynnego modelu
        /// (AccessTools.DeclaredMethod - latka przez typ pochodny na metode bazowa jest po cichu polykana).</summary>
        internal static void ApplyInCampaign(Harmony h)
        {
            if (_tried || h == null) return;
            try
            {
                var c = Campaign.Current;
                var model = c != null && c.Models != null ? c.Models.WorkshopModel : null;
                if (model == null) return;   // jeszcze bez modelu - sprobujemy przy nastepnym starcie sesji
                _tried = true;
                var args = new[] { typeof(Workshop), typeof(float), typeof(bool) };
                for (var t = model.GetType(); t != null && t != typeof(object); t = t.BaseType)
                {
                    var m = AccessTools.DeclaredMethod(t, "GetEffectiveConversionSpeedOfProduction", args);
                    if (m == null || m.IsAbstract) continue;
                    h.Patch(m, postfix: new HarmonyMethod(typeof(WorkshopPace), nameof(SpeedPostfix)) { priority = Priority.Last });
                    _target = m; _targetName = "wpieta (" + t.Name + ".GetEffectiveConversionSpeedOfProduction)";
                    break;
                }
                if (_target == null) _targetName = "BRAK GetEffectiveConversionSpeedOfProduction w lancuchu " + model.GetType().Name;
            }
            catch (Exception e) { _targetName = "wyjatek " + e.GetType().Name; Log.Error("WorkshopPace.ApplyInCampaign", e); }
        }

        /// <summary>Z WorkshopTradeBehavior.OnSessionLaunched (po ApplyInCampaign): linia startu.</summary>
        internal static void SessionStart()
        {
            _startDone = true;
            var s = Settings.Current;
            string head = "Tempo warsztatow (E3-D2): start sesji | klucz Workshop Pace By Market " + (s != null && s.WorkshopPaceByMarket ? "tak" : "nie");
            try
            {
                if (s == null || !s.WorkshopPaceByMarket) { Log.Info(head + " | WYLACZONE (MCM) - tempo gry | latka " + _targetName + " | potkniecia 0."); return; }
                _avgDay = int.MinValue; EnsureAvg();
                Log.Info(head + " | latka " + _targetName + " | linie towarowe z wsadem warsztatow notabli w miastach x F = clamp(m_t / srednia, 0.3, 4) x clamp(sqrt(indeks wyrobu), 0.7, 1.4), najwyzej 4"
                         + " | srednia m_t " + _avgM.ToString("0.000", Inv) + " z " + _avgN + " warsztatow towarowych miast | bez zmian: linie broni, bez wsadu, zamkniete strzaly, ukryci rzemieslnicy, zamki, gracz | potkniecia 0.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static string F2(double v) { return double.IsNaN(v) ? "-" : v.ToString("0.00", Inv); }

        /// <summary>Raz na dobe (WorkshopTrade.Daily, po linii "Warsztaty towarowe"): linia "Tempo warsztatow (E3-D2): dzien N".</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (!_startDone) SessionStart();
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder(900);
                sb.Append("Tempo warsztatow (E3-D2): dzien ").Append(day).Append(" | ").Append(s.WorkshopPaceByMarket ? (_target != null ? "CZYNNE" : "NIECZYNNE (brak latki)") : "WYLACZONE (MCM)")
                  .Append(" | warsztatow z tempem ").Append(_dShops).Append(" (linii ").Append(_dLines).Append("), linie bez zmian w tych warsztatach (bron, bez wsadu, zamkniete) ").Append(_dSame)
                  .Append(", zamki pominiete ").Append(_dCastle).Append(", gracz pominiety ").Append(_dPlayer)
                  .Append(" | F srednio ").Append(F2(_dLines > 0 ? _dFSum / _dLines : double.NaN)).Append(", min ").Append(F2(_dFMin)).Append(", max ").Append(F2(_dFMax))
                  .Append(", rozklad <0.7 ").Append(_bins[0]).Append(", 0.7-0.95 ").Append(_bins[1]).Append(", 0.95-1.05 ").Append(_bins[2]).Append(", 1.05-1.5 ").Append(_bins[3])
                  .Append(", 1.5-2.5 ").Append(_bins[4]).Append(", >2.5 ").Append(_bins[5])
                  .Append("; czesc ludzi srednio ").Append(F2(_dLines > 0 ? _dMSum / _dLines : double.NaN)).Append(", czesc ceny srednio ").Append(F2(_dLines > 0 ? _dPSum / _dLines : double.NaN))
                  .Append(" (srednia m_t ").Append(_avgM.ToString("0.000", Inv)).Append(" z ").Append(_avgN).Append(" warsztatow)")
                  .Append(" | cykle linii na dobe (szybkosc) swiata przed ").Append(_dBefore.ToString("0.0", Inv)).Append(" / po ").Append(_dAfter.ToString("0.0", Inv))
                  .Append(" (x").Append(_dBefore > 0 ? (_dAfter / _dBefore).ToString("0.000", Inv) : "-").Append(")");
                var keys = new List<string>(_dType.Keys);
                keys.Sort((a, b) => _dType[b].After.CompareTo(_dType[a].After));
                var parts = new List<string>();
                foreach (var k in keys)
                {
                    var t = _dType[k];
                    if (parts.Count >= 12) break;
                    parts.Add(k + " " + t.Shops + " (linii " + t.Lines + ", przed " + t.Before.ToString("0.0", Inv) + " / po " + t.After.ToString("0.0", Inv) + ")");
                }
                sb.Append(" | wedlug typu: ").Append(parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "-")
                  .Append(" | niezgodne (licznik linii) ").Append(_dMismatch).Append(" (sesja ").Append(_sessMismatch).Append(", warsztatow poza regula ").Append(_bad.Count).Append(")")
                  .Append(" | latka ").Append(_targetName)
                  .Append(" | wplyw na skrzynie: bez nowego przeplywu (wiecej wsadu z polek do kas miast i plac cykli do kas i kies ludu)")
                  .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_sessStumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("WorkshopPace.Daily", e); }
            finally { _shop = null; ZeroDay(); }
        }
    }
}
