using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace Armoury
{
    /// <summary>
    /// 311c - WYCENA STALA I RANKING WARSZTATOW (31.10, rozpoznanie rozp311w branze-krytyk punkt 5: Draconys silversmithy cena 2 673 693 w dobie 108878 przy
    /// pamieci miesiaca 171.2 i roku 220.0 d/d - 208 dob historii, polowa ceny targu 6629.7 = 3314.8 d/d; P307-4b: cena do x3.8 z doby na dobe).
    /// Klucz WorkshopQuoteSteady (tylko cena dla gracza - Build z sellerAsks; odkup notabla przyciety ta sama cena przez QuoteFor):
    ///  - mediana branzy = mediana pamieci miesiaca (rec.Avg, prawdziwy wynik po placach i utrzymaniu) warsztatow tego typu z pamiecia >= Span dob, co najmniej 3
    ///    warsztaty; liczona raz na dobe (EnsureInd) - ta sama miara co "wedle typu" w linii "Warsztaty towarowe", ale z miesiaca, nie z jednej doby;
    ///  - cena targu (Expected, w calosci albo polowa) liczy sie najwyzej do WorkshopQuoteMarketCap x wiekszej z: pamiec warsztatu, mediana branzy;
    ///  - krotka historia (Mem < WorkshopBuyerShortHistoryDays): cena targu ponad pamiec liczy sie najwyzej do waga pamieci (Mem / dni) x pamiec + reszta x mediana
    ///    branzy (recenzja: nigdy ponizej wlasnej pamieci i nigdy ponad wiekszy z pamieci i targu - mediana tnie tylko czesc z targu);
    ///  - krok doby: zysk wyceny ponad podstawe (pamiec warsztatu; bez historii / same linie towarowe - mediana branzy) rosnie najwyzej x WorkshopQuoteDayStep wobec
    ///    poprzedniej doby wyceny (od 10 d/d; Daily wycenia kazdy warsztat co dobe). Do podstawy cena wraca od razu (recenzja: inaczej warsztat po zlym dniu targu szedl
    ///    graczowi przez kilka dob za ulamek wartosci), w dol krok nie trzyma (spadek do pamieci ogranicza samo przyciecie). Bez historii i bez mediany branzy - bez
    ///    reguly (dzisiejszy targ jak dotad). Zmiana typu (Get) albo wlasciciela miedzy notablami / lordami - od nowa (kupno i odsprzedaz przez gracza -
    ///    ten sam lancuch, zeby odkup notabla, przyciety cena gracza w CostNotablePostfix, nie przeskoczyl ceny kupna).
    /// Regula ceny bez zmian: 3 x roczny zysk po podatku kupujacego (nie mniej niz sprzet) + kapital + robota w toku. Zadne zloto sie nie rusza - sama wycena.
    /// Linie: "Warsztaty - wycena stala (311c)" co dobe, "Warsztaty ranking (311c)" w dobie linii (LogGate.Lines, klucz WorkshopRankingLog).
    /// </summary>
    internal static partial class WorkshopTrade
    {
        private const float StepMin = 10f;   // od tego zysku wyceny (d/d) dziala krok doby - ponizej ulamek "x1.5" nic nie znaczy

        private static bool SteadyOn { get { var s = Settings.Current; return s != null && s.WorkshopQuoteSteady; } }
        private static float QStep { get { var s = Settings.Current; return s == null ? 1.5f : Math.Max(1f, Math.Min(4f, s.WorkshopQuoteDayStep)); } }
        private static float QCap { get { var s = Settings.Current; return s == null ? 1.5f : Math.Max(1f, Math.Min(10f, s.WorkshopQuoteMarketCap)); } }

        // mediany branz (sesja; raz na dobe)
        private static readonly Dictionary<string, float> _ind = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _indN = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _indDay = -1;

        // doba (tylko z petli Daily - jedna wycena na warsztat)
        private static int _sN, _sShort, _sCap, _sStep, _sStepPairs;
        private static float _sMaxRaw = 1f, _sMaxFin = 1f; private static string _sMaxRawW = "-", _sMaxFinW = "-";
        private static int _sBigBefore = -1, _sBigAfter = -1; private static float _sBigRaw, _sBigFin; private static string _sBigW = "-";
        private sealed class RankRow { public string Name; public float Net, Tax; public int Price; }
        private static readonly List<RankRow> _rank = new List<RankRow>();

        private static void Reset311() { _ind.Clear(); _indN.Clear(); _indDay = -1; Clear311Day(); }

        private static void Clear311Day()
        {
            _sN = _sShort = _sCap = _sStep = _sStepPairs = 0;
            _sMaxRaw = _sMaxFin = 1f; _sMaxRawW = _sMaxFinW = "-";
            _sBigBefore = _sBigAfter = -1; _sBigRaw = _sBigFin = 0f; _sBigW = "-";
            _rank.Clear();
        }

        private static int DayNow311() { return (int)CampaignTime.Now.ToDays; }

        /// <summary>Mediana pamieci miesiaca warsztatow tego typu (z pamiecia >= Span dob) - raz na dobe dla wszystkich typow.</summary>
        private static void EnsureInd()
        {
            int d = DayNow311();
            if (_indDay == d) return;
            // recenzja: doba odhaczona dopiero po przeliczeniu (finally) - wyjatek zostawia mediany z wczoraj, nie puste; bez ponawiania co wywolanie
            try
            {
                var by = new Dictionary<string, List<float>>(StringComparer.Ordinal);
                int span = Span;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Workshops == null) continue;
                    foreach (var w in t.Workshops)
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Owner == null) continue;
                        Rec r;
                        // recenzja: rekord ze zmienionym typem pomijamy (Get zrobilby SyncInitialCapital cudzego warsztatu w trakcie rozmowy - zostaje petli Daily);
                        // brak rekordu - Get (sam odczyt pamieci z zapisu, bez skutkow ubocznych)
                        if (_rec.TryGetValue(w, out r)) { if (r.Type != TypeId(w)) continue; }
                        else r = Get(w);
                        if (r == null || r.Mem < span) continue;
                        List<float> l;
                        if (!by.TryGetValue(r.Type, out l)) { l = new List<float>(); by[r.Type] = l; }
                        l.Add(r.Avg);
                    }
                }
                _ind.Clear(); _indN.Clear();
                foreach (var kv in by)
                {
                    if (kv.Value.Count < 3) continue;
                    kv.Value.Sort();
                    _ind[kv.Key] = kv.Value[kv.Value.Count / 2];
                    _indN[kv.Key] = kv.Value.Count;
                }
            }
            catch (Exception e) { Stumble("WorkshopTrade.EnsureInd", e); }
            finally { _indDay = d; }
        }

        private static bool Industry(string type, out float v)
        {
            v = 0f;
            if (type == null) return false;
            EnsureInd();
            return _ind.TryGetValue(type, out v);
        }

        /// <summary>311c: zysk wyceny dla gracza - przyciecie ceny targu, krotka historia ku branzy, krok doby. Tylko q i pamiec wyceny (rec.Q*).</summary>
        private static void Steady(Workshop w, Rec rec, Quote q)
        {
            var inv = CultureInfo.InvariantCulture;
            float raw = q.Profit; q.RawP = raw;
            float ind; bool hasInd = Industry(rec.Type, out ind);
            q.Ind = hasInd ? ind : float.NaN;
            int shortD = Math.Max(1, Settings.Current.WorkshopBuyerShortHistoryDays);
            bool hist = rec.Days > 0 && !float.IsNaN(q.MemP);
            bool shortH = false;
            float p, stable, capV, shortV = float.MaxValue;
            if (q.ArmsIdle || !hist)
            {
                // bez historii / same linie towarowe: dzisiejszy targ, ale nie ponad przyciecie; podstawa kroku = mediana branzy (bez niej - bez reguly)
                float baseV = hist ? Math.Max(q.MemP, hasInd ? ind : q.MemP) : (hasInd ? ind : raw);
                capV = Math.Max(0f, baseV) * QCap;
                p = hasInd ? Math.Min(raw, capV) : raw;
                stable = hasInd ? Math.Min(p, ind) : p;
            }
            else
            {
                // pamiec warsztatu; cena targu ponad nia najwyzej do przyciecia (krotka historia - do mieszanki pamieci i mediany branzy, nie ponizej pamieci)
                capV = Math.Max(0f, Math.Max(q.MemP, hasInd ? ind : q.MemP)) * QCap;
                if (rec.Mem < shortD && hasInd)
                {
                    float k = Math.Max(0f, Math.Min(1f, rec.Mem / (float)shortD));
                    shortV = Math.Max(q.MemP, k * q.MemP + (1f - k) * ind); shortH = true;
                }
                p = q.Exp ? Math.Max(q.MemP, Math.Min(raw, Math.Min(capV, shortV))) : q.MemP;
                stable = q.MemP;
            }
            var why = new StringBuilder();
            if (p < raw - 0.05f)
            {
                if (shortH && shortV < capV)
                {
                    q.SteadyKind |= 1;
                    why.Append("My records are short, so I count today's market prices only as far as shops of this trade earn across the land - about ").Append(ind.ToString("0.#", inv)).Append(" a day. ");
                }
                else
                {
                    q.SteadyKind |= 2;
                    why.Append("Today's market prices promise about ").Append(raw.ToString("0.#", inv)).Append(" a day, but they jump from day to day; I count them only up to what this shop and shops of its trade really earn. ");
                }
            }
            // krok doby
            int d = DayNow311();
            // nowy wlasciciel - od nowa; kupno i odsprzedaz przez gracza ciagna ten sam lancuch (odkup notabla przyciety cena gracza z tej samej doby - bez petli zysku)
            if (!ReferenceEquals(rec.QOwner, w.Owner))
            {
                if (rec.QOwner != null && rec.QOwner != Hero.MainHero && w.Owner != Hero.MainHero) rec.QDay = rec.QPrevDay = -1;
                rec.QOwner = w.Owner;
            }
            if (rec.QDay != d)
            {
                if (rec.QDay >= 0) { rec.QPrevDay = rec.QDay; rec.QPrevProfit = rec.QProfit; rec.QPrevRaw = rec.QRaw; }
                rec.QDay = d;
            }
            if (rec.QPrevDay >= 0 && rec.QPrevDay < d)
            {
                int gap = Math.Max(1, Math.Min(10, d - rec.QPrevDay));
                q.Gap = gap; q.PrevP = rec.QPrevProfit; q.PrevRaw = rec.QPrevRaw;
                float st = QStep;
                if (st > 1.0001f)
                {
                    // recenzja: gorny krok liczony od wiekszej z (wczoraj, 10 d/d) i nigdy ponizej podstawy - po zlym dniu targu cena wraca do pamieci / mediany od razu;
                    // dolnego kroku nie ma (trzymal cene ponad pamiecia i targiem przy zalamaniu wsadu - gracz przeplacal)
                    float k = (float)Math.Pow(st, gap), pv = rec.QPrevProfit, before = p;
                    float hi = Math.Max(Math.Max(pv, StepMin) * k, stable);
                    if (p > hi) p = hi;
                    if (Math.Abs(p - before) > 0.05f)
                    {
                        q.SteadyKind |= 4;
                        why.Append("Prices have moved since ").Append(gap == 1 ? "yesterday" : "I last priced it").Append(", and I move my price only step by step. ");
                    }
                }
            }
            rec.QProfit = p; rec.QRaw = raw;
            q.Profit = p; q.Stable = stable;
            if (why.Length > 0) q.SteadyWhy = why.ToString();
        }

        /// <summary>Cena przy danym zysku tym samym wzorem co Build (po Build - pola q wypelnione).</summary>
        private static int PriceAt(Quote q, float profit)
        {
            float net = Math.Max(0f, profit) * (1f - q.Tax);
            int going = (int)Math.Round(net * Math.Max(1, CampaignTime.DaysInYear) * q.Years);
            return Math.Max(going, q.Equip) + q.Capital + q.Wip;
        }

        private static string ShopName(Workshop w)
        {
            string town = "?", type = "?", owner = "?";
            try { town = w.Settlement != null ? w.Settlement.Name.ToString() : "?"; type = TypeId(w); owner = w.Owner == null ? "-" : w.Owner == Hero.MainHero ? "gracz" : w.Owner.IsNotable ? "notabl" : "lord"; } catch { }
            return town + " " + type + " (" + owner + ")";
        }

        private static float Ratio(float a, float b) { return a >= b ? a / b : b / a; }

        /// <summary>Petla Daily: jedna wycena na warsztat - liczniki poprawek, najwiekszy skok doby przed i po regule, wiersz rankingu.</summary>
        private static void Note311(Workshop w, Quote q)
        {
            Rec rec;
            if (!_rec.TryGetValue(w, out rec)) return;
            if (SteadyOn && !float.IsNaN(q.RawP))
            {
                _sN++;
                if ((q.SteadyKind & 1) != 0) _sShort++;
                if ((q.SteadyKind & 2) != 0) _sCap++;
                if ((q.SteadyKind & 4) != 0) _sStep++;
                if (q.Gap > 0)
                {
                    double inv = 1.0 / q.Gap;
                    // recenzja: po regule mierzymy to, co regula gwarantuje - wzrost ponad podstawe (pamiec / mediana) wobec max(wczoraj, 10 d/d); powrot do podstawy i spadek bez limitu
                    if (!float.IsNaN(q.Stable) && !float.IsNaN(q.PrevP) && q.Profit > q.Stable + 0.05f && q.Profit >= StepMin)
                    {
                        _sStepPairs++;
                        float r = (float)Math.Pow(Math.Max(1f, q.Profit / Math.Max(q.PrevP, StepMin)), inv);
                        if (r > _sMaxFin) { _sMaxFin = r; _sMaxFinW = ShopName(w); }
                    }
                    if (q.PrevRaw >= StepMin && q.RawP >= StepMin)
                    {
                        float r = (float)Math.Pow(Ratio(q.RawP, q.PrevRaw), inv);
                        if (r > _sMaxRaw) { _sMaxRaw = r; _sMaxRawW = ShopName(w); }
                    }
                }
                int before = PriceAt(q, q.RawP);
                if (before != q.Price && Math.Abs(before - q.Price) > Math.Abs(_sBigBefore - _sBigAfter))
                { _sBigBefore = before; _sBigAfter = q.Price; _sBigRaw = q.RawP; _sBigFin = q.Profit; _sBigW = ShopName(w); }
            }
            // recenzja: ranking bez warsztatow gracza (jego "cena kupna" nic nie mowi) i tylko przy wlaczonym kluczu
            var st = Settings.Current;
            if (st != null && st.WorkshopRankingLog && w.Owner != Hero.MainHero && rec.Mem >= Span && q.Price > 0) _rank.Add(new RankRow { Name = ShopName(w), Net = rec.Avg, Tax = q.Tax, Price = q.Price });
        }

        private static string RankText(RankRow r, int i)
        {
            var inv = CultureInfo.InvariantCulture;
            double yr = r.Price > 0 ? r.Net * (1.0 - r.Tax) * Math.Max(1, CampaignTime.DaysInYear) / r.Price * 100.0 : 0.0;
            return (i + 1) + ". " + r.Name + " " + r.Net.ToString("0.0", inv) + " d/d, cena " + r.Price + ", zwrot " + yr.ToString("0.0", inv) + "%/rok";
        }

        /// <summary>Linia doby "Warsztaty - wycena stala (311c)" i w dobie linii "Warsztaty ranking (311c)".</summary>
        private static void Daily311c(int day)
        {
            var inv = CultureInfo.InvariantCulture;
            var s = Settings.Current;
            var sb = new StringBuilder();
            sb.Append("Warsztaty - wycena stala (311c): dzien ").Append(day).Append(" | klucz ").Append(SteadyOn ? "TAK" : "NIE").Append(", krok doby w gore x").Append(QStep.ToString("0.00", inv))
              .Append(" (od ").Append(StepMin.ToString("0", inv)).Append(" d/d), cena targu najwyzej x").Append(QCap.ToString("0.00", inv)).Append(" wiekszej z pamieci i mediany branzy, krotka historia ")
              .Append(Math.Max(1, s.WorkshopBuyerShortHistoryDays)).Append(" dob ku medianie branzy");
            if (SteadyOn)
            {
                sb.Append(" | wycen dzis ").Append(_sN).Append(": krotka historia ku branzy ").Append(_sShort).Append(", cena targu przycieta ").Append(_sCap).Append(", krok doby przyciety ").Append(_sStep)
                  .Append(" | najwiekszy skok zysku wyceny doba do doby (przed regula: par od ").Append(StepMin.ToString("0", inv)).Append(" d/d; po regule: wzrost ponad pamiec / mediane, wycen ").Append(_sStepPairs).Append("): przed regula x").Append(_sMaxRaw.ToString("0.00", inv))
                  .Append(" (").Append(_sMaxRawW).Append("), po regule x").Append(_sMaxFin.ToString("0.00", inv)).Append(" (").Append(_sMaxFinW).Append(")")
                  .Append(" | najwieksza poprawka: ").Append(_sBigBefore < 0 ? "-" : _sBigW + " cena przed " + _sBigBefore + " po " + _sBigAfter + " (zysk/d " + _sBigRaw.ToString("0.0", inv) + " -> " + _sBigFin.ToString("0.0", inv) + ")");
            }
            EnsureInd();
            var keys = new List<string>(_ind.Keys);
            keys.Sort((a, b) => _ind[b].CompareTo(_ind[a]));
            var parts = new List<string>();
            foreach (var k in keys) parts.Add(k + " " + _ind[k].ToString("0.0", inv) + " (n " + _indN[k] + ")");
            sb.Append(" | mediana branzy (pamiec miesiaca, warsztaty >= ").Append(Span).Append(" dob, od 3): ").Append(parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "brak")
              .Append(" | potkniecia ").Append(_dStumbles).Append('.');
            Log.Info(sb.ToString());
            if (!s.WorkshopRankingLog || !LogGate.Lines) return;
            var all = new List<RankRow>(_rank);
            all.Sort((a, b) => b.Net.CompareTo(a.Net));
            var top = new List<string>(); var bot = new List<string>();
            for (int i = 0; i < Math.Min(10, all.Count); i++) top.Add(RankText(all[i], i));
            for (int i = 0; i < Math.Min(10, all.Count - top.Count); i++) bot.Add(RankText(all[all.Count - 1 - i], i));   // recenzja: bez warsztatow z top 10
            Log.Info("Warsztaty ranking (311c): dzien " + day + " | warsztatow z pamiecia >= " + Span + " dob (bez warsztatow gracza) " + all.Count + " | top 10 (zysk/d z pamieci miesiaca, po placach, wsadzie i utrzymaniu, przed podatkiem): "
                     + (top.Count > 0 ? string.Join("; ", top.ToArray()) : "-") + " | bottom 10: " + (bot.Count > 0 ? string.Join("; ", bot.ToArray()) : "-")
                     + " | cena = cena kupna dla gracza dzis (wycena " + (SteadyOn ? "stala 311c" : "bez 311c") + "), zwrot = zysk/d x (1 - podatek gracza) x " + Math.Max(1, CampaignTime.DaysInYear) + " / cena.");
        }
    }
}
