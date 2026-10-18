using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E3-D3 (noc-ekonomia3 16.10, czesc 2; klucz WorkshopRetoolByPrice) - WARSZTAT ZMIENIA BRANZE ZA CENA.
    /// Dowod [L] (test noc-ekonomia2 21-25-46 d109195, "Warsztaty towarowe"): pracowalo 170 z 291; tracace albo stojace: kopalnie 8, olejarnie 8, garbarnie 13
    /// (1 pracuje), tkalnie lnu 16 (3), garncarnie 6 (2), a kuznie zarabiaja +128 d na warsztat, tkalnie welny +114.5 d. Typ warsztatu zmienia sie dzis tylko
    /// przy bankructwie (losowo z wag wsadu gry, WorkshopsCampaignBehavior.DecideBestWorkshopType :1278), wiec warsztat bez wsadu i bez zysku stoi latami.
    /// Regula (b), raz na dobe: warsztat NOTABLA w miescie (nie gracza, nie ukryty, bez linii broni, nie kopalnia), ktory ma w sredniej >= 60 dob obecnego typu
    /// (WorkshopTrade.Rec - w zapisie arm_wstrade, wiec przetrwa wczytanie) i traci w obu pamieciach (miesiac Avg < 0 i rok Slow < 0), przechodzi na typ o
    /// najlepszym wyniku z cen targu tego miasta PO PLACACH cyklu (NoTownMines.MarketScore net - ta sama miara dla typu obecnego i nowego, uwaga recenzji),
    /// gdy: wsad typu jest w okregu (WorkshopInputs.InputsNear: wsie TradeBound jak w grze, polka, kontrakt 174), wynik > 0 i >= max(obecny + 10 d, obecny x 1.3),
    /// wlascicielowi po zaplacie sprzetu zostaje >= 4 500 (pas kies notabli gry). Najwyzej 3 na dobe na swiat (najgorszy Slow najpierw), 1 na miasto na 30 dob,
    /// warsztat najwyzej raz na 120 dob (pamiec sesji; po wczytaniu chroni 60 dob sredniej nowego typu). Kuznia moze byc celem - jedyny wytworca narzedzi;
    /// liczona tylko z linii towarowych, a jej bron dalej pod WorkshopLaw i hamulcem N5.
    /// Regula (a) (NoTownMines.Postfix): przy bankructwie i warsztacie odebranym graczowi typ z cen targu (ta sama BestByPrice), gdy zarabia > 1.25 x wybor gry/BK.
    /// Kto placi, kto dostaje: koszt sprzetu (GetConvertProductionCost x WorkshopTradeEquipmentScale) wlasciciel -> kasa miasta - istniejaca latka
    /// WorkshopTrade.ConvertPrefix/Postfix przy ChangeProductionTypeOfWorkshopAction.Apply(w, t, false); bez niej (ConvertWired) i bez paczki warsztatow
    /// (WorkshopTrade.On) regula (b) nie dziala - gra kasowalaby koszt w nicosc. Zmiana typu jest akcja gry i jest trwala; wylaczenie klucza tylko zatrzymuje dalsze.
    /// Nic w zapisie. Wyjatek per warsztat - licznik potkniec.
    /// F4 (noc 17/18.10, paczka f4-warsztaty, klucz WorkshopRetoolFairScore; dowod fin86: spodziewane 16 603 d/d kuzni srebra przy 0 cykli): obecny typ = wlasny zmierzony
    /// wynik warsztatu (srednia roku >= 60 dob + utrzymanie), nowy typ = WorkshopFairScore (cena sprawiedliwa cyklu, cykle z wsadu regionu dzielonego miedzy warsztaty,
    /// typ obecny w regionie - najwyzej jego zmierzony wynik), kara x0.8 za dubel w regionie rynku; progi (x1.3, +10 d, kiesa, limity) bez zmian. Tydzien pomiaru wsadu
    /// po starym zapisie - zmiany odlozone (licznik "odlozone (pomiar w toku)"). Linia dnia: spodziewany vs rzeczywisty po 30 dobach i liczby typow swiata.
    /// </summary>
    internal static class WorkshopRetool
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MinDays = 60, TownGapDays = 30, ShopGapDays = 120, PerDay = 3, PurseFloor = 4500;
        private const float GainShare = 1.3f, GainMin = 10f, BankruptGain = 1.25f;

        private static readonly Dictionary<Town, int> _townLast = new Dictionary<Town, int>();
        private static readonly Dictionary<Workshop, int> _shopLast = new Dictionary<Workshop, int>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<string> _dDone = new List<string>(), _dBankSamples = new List<string>();
        private static int _dShops, _dCands, _dChanged, _dNoInput, _dNoGain, _dNoPurse, _dLimit, _dBankPicks, _dBankSwaps, _dStumbles, _sessStumbles, _sessChanged;
        private static int _dBankWarm;   // F4: bankructwa w tygodniu pomiaru wsadu (wybor gry/BK zostal)
        private static long _dGear;
        private static bool _startDone;

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopRetoolByPrice; } }

        internal static void Reset()
        {
            _townLast.Clear(); _shopLast.Clear(); _err.Clear(); _sessStumbles = 0; _sessChanged = 0; _startDone = false;
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _dDone.Clear(); _dBankSamples.Clear();
            _dShops = _dCands = _dChanged = _dNoInput = _dNoGain = _dNoPurse = _dLimit = _dBankPicks = _dBankSwaps = _dStumbles = 0;
            _dBankWarm = 0;
            _dGear = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _sessStumbles++;
            try { if (_err.Add(where)) Log.Error("WorkshopRetool." + where, e); } catch { }
        }

        private static string Name(Settlement st) { return st == null ? "?" : (st.Name != null ? st.Name.ToString() : st.StringId); }
        private static string F1(float v) { return v <= -1e8f ? "-" : v.ToString("0.0", Inv); }

        /// <summary>Typ ma linie broni (wyroby nie-towarowe: bron, zbroje, war_horse) - kuznia i warsztaty zbrojne nie zmieniaja branzy ta regula.</summary>
        private static bool HasArmsLine(WorkshopType t)
        {
            foreach (var p in t.Productions) if (p.Outputs != null && p.Outputs.Count > 0 && !WorkshopTrade.IsTradeLine(p)) return true;
            return false;
        }

        /// <summary>(a) - NoTownMines.Postfix przy wyborze typu poza startem gry (bankructwo, warsztat odebrany graczowi): typ z cen, gdy zarabia > 1.25 x wybor.</summary>
        internal static void BankruptStep(Town town, WorkshopType exclude, ref WorkshopType result)
        {
            if (!On || town == null) return;
            try
            {
                _dBankPicks++;
                float sb, sr;
                WorkshopType best;
                if (WorkshopFairScore.On)
                {
                    // F4 (noc 17/18.10): ta sama ocena co krok (b); pierwszy tydzien pomiaru wsadu - wybor gry/BK zostaje
                    if (!WorkshopFairScore.Ready) { _dBankWarm++; return; }
                    WorkshopFairScore.Quote fq;
                    best = WorkshopFairScore.Best(town, exclude, WorkshopInputs.InputsNear(town), null, out sb, out fq);
                    if (best == null || best == result) return;
                    sr = result != null ? WorkshopFairScore.Score(town, result, null).Score : -1e9f;
                }
                else
                {
                    best = NoTownMines.BestByPrice(town, exclude, WorkshopInputs.InputsNear(town), out sb);
                    if (best == null || best == result) return;
                    sr = result != null ? NoTownMines.MarketScore(town, result, true) : -1e9f;
                }
                if (!(sb > Math.Max(0f, BankruptGain * sr))) return;
                if (_dBankSamples.Count < 6) _dBankSamples.Add(Name(town.Settlement) + " " + (result != null ? result.StringId : "?") + " (" + F1(sr) + ") -> " + best.StringId + " (" + F1(sb) + ")");
                result = best;
                _dBankSwaps++;
            }
            catch (Exception e) { Stumble("BankruptStep", e); }
        }

        private sealed class Cand { public Workshop W; public Town T; public float Slow, Avg; }

        /// <summary>Raz na dobe (WorkshopTradeBehavior, DailyTickEvent): krok (b) i linia "Warsztaty za cena (E3-D3): dzien N".</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) { ZeroDay(); return; }
            if (!_startDone) SessionStart();
            int today = (int)CampaignTime.Now.ToDays;
            // F4 (noc 17/18.10, klucz WorkshopRetoolFairScore): dzisiejsza produkcja wsadu do sredniej (przed decyzjami; wylaczony klucz - tylko czysci licznik), czas reguly do linii
            bool f4 = WorkshopFairScore.On;
            long f4t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            WorkshopFairScore.DayFold();
            bool live = On && WorkshopTrade.On && WorkshopTrade.ConvertWired;
            try
            {
                if (live)
                {
                    var cands = new List<Cand>();
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || t.Settlement == null || !t.Settlement.IsTown || t.Workshops == null) continue;
                        foreach (var w in t.Workshops)
                        {
                            try
                            {
                                if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Owner == null) continue;
                                var o = w.Owner;
                                if (o == Hero.MainHero || !o.IsNotable || (o.Clan != null && o.Clan == Clan.PlayerClan)) continue;
                                if (w.WorkshopType.StringId == "mines" || HasArmsLine(w.WorkshopType)) continue;
                                _dShops++;
                                float avg, slow; int days;
                                if (!WorkshopTrade.RecOf(w, out avg, out slow, out days) || days < MinDays || !(avg < 0f) || !(slow < 0f)) continue;
                                cands.Add(new Cand { W = w, T = t, Slow = slow, Avg = avg });
                            }
                            catch (Exception e) { Stumble("Daily(kandydat)", e); }
                        }
                    }
                    _dCands = cands.Count;
                    cands.Sort((a, b) => a.Slow.CompareTo(b.Slow));
                    var near = new Dictionary<Town, Dictionary<ItemCategory, float>>();
                    int done = 0;
                    foreach (var c in cands)
                    {
                        if (done >= PerDay) { _dLimit++; continue; }
                        try
                        {
                            int last;
                            if ((_townLast.TryGetValue(c.T, out last) && today - last < TownGapDays) || (_shopLast.TryGetValue(c.W, out last) && today - last < ShopGapDays)) { _dLimit++; continue; }
                            if (f4 && !WorkshopFairScore.Ready) { WorkshopFairScore.NoteWarm(); continue; }   // F4: pierwszy tydzien pomiaru wsadu - bez zmian typu
                            Dictionary<ItemCategory, float> dict;
                            if (!near.TryGetValue(c.T, out dict)) { dict = WorkshopInputs.InputsNear(c.T); near[c.T] = dict; }
                            var cur = c.W.WorkshopType;
                            float sc, sb;
                            WorkshopType best;
                            WorkshopFairScore.Quote fq = null;
                            if (f4)
                            {
                                // F4: obecny typ - wlasny zmierzony wynik (>= 60 dob, jak warunek kandydata); nowy - cena sprawiedliwa, wsad regionu, zmierzony wynik regionu
                                sc = WorkshopFairScore.OwnScore(c.W);
                                best = WorkshopFairScore.Best(c.T, cur, dict, c.W, out sb, out fq);
                            }
                            else
                            {
                                sc = NoTownMines.MarketScore(c.T, cur, true);
                                best = NoTownMines.BestByPrice(c.T, cur, dict, out sb);
                            }
                            if (best == null) { _dNoInput++; continue; }
                            float need = sc > -1e8f ? Math.Max(sc + GainMin, sc * GainShare) : GainMin;
                            if (!(sb > 0f) || sb < need) { _dNoGain++; continue; }
                            int cost = Math.Max(0, Campaign.Current.Models.WorkshopModel.GetConvertProductionCost(best));
                            var o = c.W.Owner;
                            if (o.Gold - cost < PurseFloor) { _dNoPurse++; continue; }
                            int g0 = o.Gold;
                            string from = cur.StringId ?? "?";
                            ChangeProductionTypeOfWorkshopAction.Apply(c.W, best, false);   // koszt sprzetu: wlasciciel -> kasa miasta (WorkshopTrade.ConvertPrefix/Postfix)
                            if (c.W.WorkshopType != best) continue;
                            done++; _dChanged++; _sessChanged++;
                            _dGear += Math.Max(0, g0 - o.Gold);
                            _townLast[c.T] = today; _shopLast[c.W] = today;
                            float raw = fq != null ? fq.Score : sb;
                            if (f4) WorkshopFairScore.NoteRetool(c.W, best, raw, today);   // F4: spodziewany wynik (przed kara za dubel) do porownania po 30 dobach
                            if (_dDone.Count < 8) _dDone.Add(Name(c.T.Settlement) + " " + from + " -> " + best.StringId + " (wynik " + F1(sc) + " -> " + F1(sb) + " d/d, srednia " + c.Avg.ToString("0.0", Inv)
                                                         + " / rok " + c.Slow.ToString("0.0", Inv) + ", sprzet " + Math.Max(0, g0 - o.Gold) + (f4 ? ", " + WorkshopFairScore.QuoteText(fq, raw) : "") + ")");
                        }
                        catch (Exception e) { Stumble("Daily(zmiana)", e); }
                    }
                }
                // poprawki (uwaga recenzji 2): miasta z 3 i wiecej warsztatami jednego typu (dubel typow - INFO NE3-21)
                int dupTowns = 0;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Workshops == null) continue;
                    var cnt = new Dictionary<WorkshopType, int>();
                    foreach (var w in t.Workshops)
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                        int n0; cnt.TryGetValue(w.WorkshopType, out n0); cnt[w.WorkshopType] = n0 + 1;
                    }
                    foreach (var n1 in cnt.Values) if (n1 >= 3) { dupTowns++; break; }
                }
                var sb2 = new StringBuilder(760);
                sb2.Append("Warsztaty za cena (E3-D3): dzien ").Append(today - 1).Append(" | ")
                   .Append(!On ? "WYLACZONE (MCM)" : !WorkshopTrade.On ? "NIECZYNNE (paczka warsztatow towarowych wylaczona)" : !WorkshopTrade.ConvertWired ? "NIECZYNNE (brak latki zmiany produkcji)" : "CZYNNE")
                   .Append(" | warsztatow notabli pod regula ").Append(_dShops).Append(", kandydaci (tracace od ").Append(MinDays).Append(" dob w obu pamieciach) ").Append(_dCands)
                   .Append(" | zmiany ").Append(_dChanged).Append(_dDone.Count > 0 ? " [" + string.Join("; ", _dDone.ToArray()) + "]" : "")
                   .Append(" | odlozone: bez typu z wsadem i zyskiem ").Append(_dNoInput).Append(", za maly zysk (< obecny + 10 d i x1.3) ").Append(_dNoGain)
                   .Append(", bez kiesy (po sprzecie < ").Append(PurseFloor).Append(") ").Append(_dNoPurse).Append(", limit (").Append(PerDay).Append(" na dobe, miasto ").Append(TownGapDays).Append(" dob, warsztat ").Append(ShopGapDays).Append(" dob) ").Append(_dLimit)
                   .Append(" | sprzet do kas miast ").Append(_dGear)
                   .Append(" | bankructwa i warsztaty odebrane graczowi: wyborow ").Append(_dBankPicks).Append(", typ z cen zamiast wyboru gry ").Append(_dBankSwaps)
                   .Append(_dBankSamples.Count > 0 ? " [" + string.Join("; ", _dBankSamples.ToArray()) + "]" : "")
                   .Append(" | zmian w sesji ").Append(_sessChanged)
                   .Append(f4 ? " | kara za typ juz obecny w regionie rynku x0.8 na warsztat (F4); miast z 3+ warsztatami jednego typu " : " | kara za typ juz obecny w miescie x0.8 na warsztat; miast z 3+ warsztatami jednego typu ").Append(dupTowns)
                   .Append(" | wplyw na skrzynie: notable -> kasy miast (sprzet)");
                if (f4)
                {
                    if (_dBankWarm > 0) sb2.Append(" | bankructwa w tygodniu pomiaru wsadu (wybor gry zostal) ").Append(_dBankWarm);
                    double f4ms = (System.Diagnostics.Stopwatch.GetTimestamp() - f4t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    sb2.Append(WorkshopFairScore.DayText(today, f4ms));
                }
                sb2.Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_sessStumbles).Append(").");
                Log.Info(sb2.ToString());
            }
            catch (Exception e) { Log.Error("WorkshopRetool.Daily", e); }
            finally { ZeroDay(); WorkshopFairScore.EndDay(); }
        }

        /// <summary>Linia startu (WorkshopTradeBehavior.OnSessionLaunched, takze leniwie z Daily).</summary>
        internal static void SessionStart()
        {
            _startDone = true;
            var s = Settings.Current;
            try
            {
                Log.Info("Warsztaty za cena (E3-D3): start sesji | klucz Workshop Retool By Price " + (s != null && s.WorkshopRetoolByPrice ? "tak" : "nie")
                         + " | paczka warsztatow towarowych " + (WorkshopTrade.On ? "czynna" : "NIECZYNNA") + ", latka zmiany produkcji (sprzet do kasy miasta) " + (WorkshopTrade.ConvertWired ? "wpieta" : "BRAK")
                         + ", wybor typu przy bankructwie (NoTownMines.Postfix) " + NoTownMines.Hook
                         + " | regula: tracacy od " + MinDays + " dob warsztat notabla -> typ z najlepszym wynikiem po placach z cen targu (x" + GainShare.ToString("0.0", Inv) + " i +" + GainMin.ToString("0", Inv) + " d), "
                         + PerDay + " na dobe, miasto co " + TownGapDays + " dob, warsztat co " + ShopGapDays + " dob; bankructwo - typ z cen przy > x" + BankruptGain.ToString("0.00", Inv) + " wyboru gry"
                         + (WorkshopFairScore.On ? " | " + WorkshopFairScore.StartText() : "") + " | potkniecia 0.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }
    }
}
