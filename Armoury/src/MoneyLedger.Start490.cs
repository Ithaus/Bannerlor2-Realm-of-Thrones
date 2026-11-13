using System;
using System.Collections.Generic;
using System.Text;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// 490 (audyt 13.11, ekonomia pkt 6 "duzy przelew na poczatku sesji": kasy miast 25.57 -> 6.61 mln w 39 dobach, kiesy wsi 1.45 -> 8.54 mln, skarbce 58.84 -> 67.93 mln):
    /// linia "Przelew startowy (490)" - stan kas miast, kiesy wsi, kas zamkow i skarbcow koron przy pierwszej dobie ksiegi po wczytaniu (baza) i w dobach sesji 1..40
    /// (potem co 7 dob), ze zmiana od bazy rozbita na pozycje linii "Przeplywy osad (klasa)" i nowe pozycje 169 ("przyczyny") - narastajaco od wczytania, najwieksze
    /// co do wartosci bezwzglednej. Te same liczniki doby co tamte linie (odczyt przed ClearDay), te same sumy co "known" ClassLine. Tylko log - niczego nie zmienia.
    /// </summary>
    internal static partial class MoneyLedger
    {
        private static readonly Dictionary<string, long>[] _c490 = { new Dictionary<string, long>(StringComparer.Ordinal), new Dictionary<string, long>(StringComparer.Ordinal), new Dictionary<string, long>(StringComparer.Ordinal) };
        private static readonly long[] _c490Crown = new long[3];   // pozycje korony z klas osad (zmiana skarbcow = minus zmiana klas): korona, udzial z zaworu miast, z zaworu zamkow
        private static long[] _c490Base;                           // [CTown, CCastle, CVill, skarbce] przy bazie; null - bez bazy
        private static int _c490Day0 = -1, _c490Days, _c490St;
        private const int C490Top = 12, C490Daily = 40;

        private static void C490Reset()
        {
            for (int c = 0; c < _c490.Length; c++) _c490[c].Clear();
            Array.Clear(_c490Crown, 0, _c490Crown.Length);
            _c490Base = null; _c490Day0 = -1; _c490Days = 0; _c490St = 0;
        }

        private static void C490(int c, string key, long v)
        {
            if (v == 0 || c < 0 || c >= _c490.Length || _c490Base == null) return;
            long o; _c490[c].TryGetValue(key, out o); _c490[c][key] = o + v;
        }

        /// <summary>490: pozycje linii "Przeplywy osad (klasa)" tej doby (ten sam rachunek "known" co ClassLine) i reszta [R] = zmiana - pozycje. Nie rusza licznikow.</summary>
        private static void C490Class(int c, long delta)
        {
            if (_c490Base == null) return;
            long known = 0;
            void A(string k, long v) { known += v; C490(c, k, v); }
            if (c == CVill) A("z utargu taborow wsi", _vKept);
            else
            {
                A("zaplata taborom wsi", -_vPaid[c]);
                A("\"zakupy\" mieszkancow (konsumpcja)", _cons[c]);
                A("lud za towar (E5/B1)", _consFolk[c]);
                A("regulator kasy (GetTownGoldChange)", _regIn[c] - _regOut[c]);
            }
            A("przelewy gry: z niczego / w nicosc", _fromNothing[c] - _toNothing[c]);
            for (int p = 0; p < Sides; p++) A("przelewy gry (GiveGoldAction): " + PName[p], _goldIn[c, p] - _goldOut[c, p]);
            for (int k = 0; k < Notes; k++) A("poza tickiem: " + NName[k], _noteIn[c, k] - _noteOut[c, k]);
            A("warsztaty towarowe - place i utrzymanie", _shopWage[c] + _shopKeep[c]);
            A("warsztaty towarowe - nadwyzka ceny polki (241)", _shopFair[c]);
            A("rzemieslnicy BK (147)", _artIn[c] + _artBack[c]);
            A("rzemieslnicy BK - wyroby uboczne (E8/B)", -_artBy[c]);
            for (int k = 0; k < Marks; k++) A("tick dobowy: " + MarkShort(k), _mark[c, k]);
            C490(c, "reszta [R] (przed pozycjami 169)", delta - known);
            _c490Crown[0] -= _mark[c, MCrown]; _c490Crown[1] -= _mark[c, MTownCrown]; _c490Crown[2] -= _mark[c, MCastleCrown];
        }

        /// <summary>490: nowe pozycje 169 tej doby (te same co "nowe pozycje" ClassCausesLine) - narastajaco; zdejmowane z reszty [R].</summary>
        private static void C490Causes(int c)
        {
            if (_c490Base == null) return;
            var C = CW.Cls;
            long fresh = 0;
            void A(string k, long v) { fresh += v; C490(c, "169: " + k, v); }
            A(c == CVill ? "prowizja od zakupow we wsi" : "prowizja od sprzedazy partiom", C[c, CW.LCommission]);
            A("warsztaty BK - kasa placi za wyroby", C[c, CW.LWorkshopOut]);
            A("warsztaty placa za wsad", C[c, CW.LWorkshopIn]);
            A("BK porty", C[c, CW.LPorts]);
            A("BK kopalnie", C[c, CW.LMines]);
            A("BK konwoje ludnosci", C[c, CW.LConvoys]);
            A("BK rynek osady", C[c, CW.LMarket]);
            A("BK rynek wsi", C[c, CW.LVillageMarket]);
            for (int k = 0; k < N169; k++) A(N169Name[k], _n169In[c, k] - _n169Out[c, k]);
            C490(c, "reszta [R] (przed pozycjami 169)", -fresh);
        }

        /// <summary>490: koniec Daily (po liniach klas, przed ClearDay): pierwsza doba po wczytaniu - baza; dalej doby sesji 1..40 i co 7 dob - linia. Nie rzuca.</summary>
        private static void Start490(int day, long[] end, long[] hold)
        {
            try
            {
                if (end == null || hold == null) return;
                long crowns = hold[HKingdoms];
                if (_c490Base == null)
                {
                    _c490Base = new[] { end[CTown], end[CCastle], end[CVill], crowns }; _c490Day0 = day; _c490Days = 0;
                    Log.Info("Przelew startowy (490): baza - pierwsza doba ksiegi po wczytaniu, dzien " + day + " | kasy miast " + M(end[CTown]) + ", kiesy wsi " + M(end[CVill])
                             + ", kasy zamkow " + M(end[CCastle]) + ", skarbce koron " + M(crowns) + " (mln) | linia w dobach sesji 1.." + C490Daily + ", potem co 7 dob | gra bez zmian.");
                    return;
                }
                _c490Days++;
                if (_c490Days > C490Daily && _c490Days % 7 != 0) return;
                var sb = new StringBuilder(3000);
                sb.Append("Przelew startowy (490): doba sesji ").Append(_c490Days).Append(" (dzien ").Append(day).Append(", baza dzien ").Append(_c490Day0)
                  .Append("; sam pomiar, zl; pozycje linii \"Przeplywy osad\" i \"przyczyny\" (169) narastajaco od bazy, najwieksze ").Append(C490Top).Append(")");
                int[] cls = { CTown, CVill, CCastle };
                foreach (int c in cls)
                {
                    long d = end[c] - _c490Base[c];
                    sb.Append(" | ").Append(CName[c]).Append(": ").Append(M(_c490Base[c])).Append(" -> ").Append(M(end[c])).Append(" mln (").Append(S(d)).Append(")");
                    var l = new List<KeyValuePair<string, long>>(_c490[c]);
                    long sum = 0; foreach (var kv in l) sum += kv.Value;
                    l.Sort((x, y) => Math.Abs(y.Value).CompareTo(Math.Abs(x.Value)));
                    int top = c == CCastle ? 5 : C490Top;
                    for (int i = 0; i < l.Count && i < top; i++) sb.Append(i == 0 ? ": " : ", ").Append(l[i].Key).Append(' ').Append(S(l[i].Value));
                    if (l.Count > top) sb.Append(" (+").Append(l.Count - top).Append(" pozycji)");
                    sb.Append("; kontrola: suma pozycji ").Append(S(sum)).Append(" wobec zmiany ").Append(S(d)).Append(" (roznica = doba bazy i doby bez linii klas)");
                }
                long dk = crowns - _c490Base[3], ck = _c490Crown[0] + _c490Crown[1] + _c490Crown[2];
                sb.Append(" | skarbce koron: ").Append(M(_c490Base[3])).Append(" -> ").Append(M(crowns)).Append(" mln (").Append(S(dk)).Append("): z kas osad przez pozycje korony ")
                  .Append(S(ck)).Append(" (").Append(MarkShort(MCrown)).Append(' ').Append(S(_c490Crown[0])).Append(", ").Append(MarkShort(MTownCrown)).Append(' ').Append(S(_c490Crown[1]))
                  .Append(", ").Append(MarkShort(MCastleCrown)).Append(' ').Append(S(_c490Crown[2])).Append("), reszta (rozliczenia rodow, wojny, pozyczki, inne) ").Append(S(dk - ck));
                sb.Append(" | potkniecia pomiaru ").Append(_c490St).Append(" | gra bez zmian.");
                Log.Info(sb.ToString());
            }
            catch { _c490St++; }
        }

        private static string M(long v) { return (v / 1e6).ToString("0.00", Inv); }
    }
}
