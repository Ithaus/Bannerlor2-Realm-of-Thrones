using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174b.0 - POMIARY RYNKU ZBROI I ZAKUPOW (docs/PROJEKT-174B-DOWOZ-2026-10-09.md rozdz. 3.0 i "Krytyka i odpowiedzi"; sam log,
    /// bez zmiany rozgrywki).
    ///  M1 "Zbroja na polkach (174b)" (codziennie): zbroja tulowia, glowy, nog i rak na polkach MIAST w pasmach t1-2 / t3-4 / t5-6 (bez unikatow) -
    ///     sztuk, miast z co najmniej 1 sztuka i (krytyka 174b, P5) miast, do ktorych w ostatnich 7 dobach trafila NOWA sztuka pasma: wyrob warsztatu,
    ///     dostawa kupcow (SupplyDemand.DailyTrade), odsprzedaz z sakiewek ludzi, nadwyzka albo zawrocony towar zalogi, a takze kazdy przyrost polki
    ///     pasma miedzy spisami (np. lup sprzedany przez gre). Sztuki w rezerwie kramu (174b.4) osobno, bez progu. Poprawka 174b (recenzja): to samo
    ///     "bez przerzutu" - bez sztuk, ktore przyjechaly bez partii na mapie (SupplyDemand.DailyTrade, zawrocone wozy zamkow GarrisonCarts) - zeby P5 pokazal,
    ///     ile nowych sztuk dla gracza daje sam przerzut (rozdz. 9 projektu: przerzut uzbrojenia bez partii - do decyzji po 174b).
    ///  M2 "ZakupyAI wedlug kupujacego (174b)" (codziennie): sztuki i zloto wedlug kupujacego (lordowie, zalogi miast, zalogi zamkow z wlasnej polki,
    ///     zamowienia zamkow w miescie, notable dla ochotnikow, ludzie gracza z sakiewki) x grupa (korpus, helm, reszta zbroi, tarcza, bron biala,
    ///     bron strzelecka, amunicja, konie i rzedy, inne); M3 w tej samej linii - nadwyzki sprzedane z sakiewek ludzi wedlug grup.
    /// Liczniki biegna miedzy dwoma taktami doby gry (DailyTickEvent) - linia "ZakupyAI: dzien" gry zamyka sie przy pierwszym zakupie nastepnej
    /// doby, wiec obie linie roznia sie o kilka godzin gry (sumy z wielu dob sa te same).
    /// Stan czyszczony w konstruktorze ArmouryBehavior (Reset).
    /// </summary>
    internal static class Measure174b
    {
        internal const int BLord = 0, BTownGarrison = 1, BCastleOwn = 2, BCastleOrder = 3, BNotable = 4, BPlayerMen = 5, Buyers = 6;
        private static readonly string[] BuyerNames = { "lordowie", "zalogi miast", "zalogi zamkow (wlasna polka)", "zamowienia zamkow (w miescie)", "notable dla ochotnikow", "ludzie gracza (sakiewka)" };
        internal const int Groups = 9;
        private static readonly string[] GroupNames = { "korpus", "helm", "reszta zbroi", "tarcza", "bron biala", "bron strzelecka", "amunicja", "konie i rzedy", "inne" };
        private static readonly long[,] _buyN = new long[Buyers, Groups], _buyG = new long[Buyers, Groups];
        private static readonly long[] _sellN = new long[Groups], _sellG = new long[Groups];
        private static readonly long[] _held = new long[Buyers];   // 174b.4: zakupy hurtowe zatrzymane na rezerwie kramu (sztuk)

        internal const int ArrWorkshop = 0, ArrTrade = 1, ArrPurse = 2, ArrGarrison = 3, ArrNet = 4, ArrKinds = 5;
        private static readonly string[] ArrNames = { "warsztaty", "kupcy (handel dzienny)", "sakiewki ludzi", "zalogi (nadwyzki i zawrocony towar)", "przyrost polki (inne, np. lup sprzedany przez gre)" };
        private static readonly long[] _arr = new long[ArrKinds];
        private static readonly string[] KindNames = { "korpus", "helm", "nogi", "rece" };
        private static readonly string[] BandNames = { "t1-2", "t3-4", "t5-6" };
        private const int Slots = 12;   // 4 rodzaje zbroi x 3 pasma
        private static readonly Dictionary<Town, int[]> _prev = new Dictionary<Town, int[]>();
        private static readonly Dictionary<Town, int[]> _lastNew = new Dictionary<Town, int[]>();
        private static readonly Dictionary<Town, int[]> _lastOwn = new Dictionary<Town, int[]>();   // poprawka 174b: nowa sztuka bez przerzutu bez partii
        private static readonly Dictionary<Town, int[]> _xfer = new Dictionary<Town, int[]>();      // sztuki przerzutu bez partii od ostatniego spisu (miasto x pasmo)
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            Array.Clear(_buyN, 0, _buyN.Length); Array.Clear(_buyG, 0, _buyG.Length); Array.Clear(_sellN, 0, Groups); Array.Clear(_sellG, 0, Groups);
            Array.Clear(_held, 0, Buyers); Array.Clear(_arr, 0, ArrKinds); _prev.Clear(); _lastNew.Clear(); _stumbles = 0; _errSites.Clear();
            _lastOwn.Clear(); _xfer.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("Measure174b." + where, e);
        }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }

        /// <summary>Rodzaj zbroi M1 i rezerwy kramu: 0 tulow, 1 glowa, 2 nogi, 3 rece; -1 = nie zbroja.</summary>
        internal static int ArmourKind(ItemObject it)
        {
            if (it == null) return -1;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.BodyArmor: return 0;
                case ItemObject.ItemTypeEnum.HeadArmor: return 1;
                case ItemObject.ItemTypeEnum.LegArmor: return 2;
                case ItemObject.ItemTypeEnum.HandArmor: return 3;
                default: return -1;
            }
        }

        /// <summary>Pasmo tieru: 0 = t1-2, 1 = t3-4, 2 = t5-6.</summary>
        internal static int Band(int tier) { return Math.Max(0, Math.Min(2, (tier - 1) / 2)); }
        internal static int Band(ItemObject it) { return Band(TierOf(it)); }

        private static int Group(ItemObject it)
        {
            if (it == null) return 8;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.BodyArmor: return 0;
                case ItemObject.ItemTypeEnum.HeadArmor: return 1;
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape: return 2;
                case ItemObject.ItemTypeEnum.Shield: return 3;
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm: return 4;
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Thrown: return 5;
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts: return 6;
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.HorseHarness: return 7;
                default: return 8;
            }
        }

        // ------------------------------------------------------------ liczniki (wolane z miejsc zakupu i dodania na polke)
        /// <summary>M2: zakup z polki targu (sztuk n po cenie unit).</summary>
        internal static void NoteBuy(int buyer, ItemObject it, int n, int unit)
        {
            if (buyer < 0 || buyer >= Buyers || it == null || n <= 0) return;
            int g = Group(it);
            _buyN[buyer, g] += n; _buyG[buyer, g] += (long)Math.Max(0, unit) * n;
        }

        /// <summary>Scalenie sklad8 (ceny hurtu 09.10): zakup z polki, gdy kazda sztuka ma swoja cene - cost to suma cen n sztuk (ShelfBuy.Take).</summary>
        internal static void NoteBuySum(int buyer, ItemObject it, int n, int cost)
        {
            if (buyer < 0 || buyer >= Buyers || it == null || n <= 0) return;
            int g = Group(it);
            _buyN[buyer, g] += n; _buyG[buyer, g] += Math.Max(0, cost);
        }

        /// <summary>M3: nadwyzka sprzedana z sakiewki ludzi (zbrojownia partii -> polka miasta).</summary>
        internal static void NoteSurplusSale(ItemObject it, int n, int unit)
        {
            if (it == null || n <= 0) return;
            int g = Group(it);
            _sellN[g] += n; _sellG[g] += (long)Math.Max(0, unit) * n;
        }

        /// <summary>174b.4: zakup hurtowy zatrzymany na rezerwie kramu (sztuka zostala na straganie).</summary>
        internal static void NoteHeld(int buyer, int n) { if (buyer >= 0 && buyer < Buyers && n > 0) _held[buyer] += n; }

        /// <summary>M1: nowa sztuka zbroi na polce miasta (zrodlo kind). Unikaty i zamki pomijane. unseen (poprawka 174b) - sztuka przyjechala bez partii na
        /// mapie (wozy zamkow GarrisonCarts); handel dzienny kupcow (ArrTrade) liczy sie tak zawsze.</summary>
        internal static void NoteArrival(Settlement st, ItemObject it, int n, int kind, bool unseen = false)
        {
            try
            {
                if (st == null || !st.IsTown || st.Town == null || it == null || n <= 0) return;
                int k = ArmourKind(it);
                if (k < 0 || ArmsPricing.IsUnique(it)) return;
                if (kind >= 0 && kind < ArrKinds) _arr[kind] += n;
                int slot = k * 3 + Band(it), day = (int)CampaignTime.Now.ToDays;
                Mark(_lastNew, st.Town, slot, day);
                if (kind == ArrTrade || unseen)
                {
                    int[] x; if (!_xfer.TryGetValue(st.Town, out x)) { x = new int[Slots]; _xfer[st.Town] = x; }
                    x[slot] += n;
                }
                else Mark(_lastOwn, st.Town, slot, day);
            }
            catch (Exception e) { Stumble("NoteArrival", e); }
        }

        private static void Mark(Dictionary<Town, int[]> d, Town t, int slot, int day)
        {
            int[] last;
            if (!d.TryGetValue(t, out last)) { last = new int[Slots]; for (int i = 0; i < Slots; i++) last[i] = int.MinValue / 2; d[t] = last; }
            last[slot] = day;
        }

        // ------------------------------------------------------------ linie doby
        /// <summary>Z ArmouryBehavior.OnDailyTick: spis M1 (jedno przejscie polek miast) i linie M1, M2/M3.</summary>
        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays;
            try { Census(day); } catch (Exception e) { Stumble("Census", e); }
            try { BuyLine(day); } catch (Exception e) { Stumble("BuyLine", e); }
            Array.Clear(_buyN, 0, _buyN.Length); Array.Clear(_buyG, 0, _buyG.Length); Array.Clear(_sellN, 0, Groups); Array.Clear(_sellG, 0, Groups);
            Array.Clear(_held, 0, Buyers); Array.Clear(_arr, 0, ArrKinds); _stumbles = 0;
        }

        private static void Census(int day)
        {
            var total = new long[Slots]; var towns = new int[Slots]; var fresh = new int[Slots]; var freshOwn = new int[Slots];
            var reserve = new long[4];
            int keep = ShopReserve.Pieces;   // 0 = rezerwa wylaczona
            int nTowns = 0;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null) continue;
                    nTowns++;
                    var r = t.Owner.ItemRoster;
                    var c = new int[Slots];
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || it == null) continue;
                        int k = ArmourKind(it);
                        if (k < 0 || ArmsPricing.IsUnique(it)) continue;
                        c[k * 3 + Band(it)] += el.Amount;
                    }
                    int[] prev, xf;
                    _xfer.TryGetValue(t, out xf);
                    if (_prev.TryGetValue(t, out prev))
                        for (int i = 0; i < Slots; i++)
                            if (c[i] > prev[i])
                            {
                                Mark(_lastNew, t, i, day); _arr[ArrNet] += c[i] - prev[i];
                                if (c[i] - prev[i] > (xf != null ? xf[i] : 0)) Mark(_lastOwn, t, i, day);   // przyrost ponad przerzut bez partii
                            }
                    _prev[t] = c;
                    if (xf != null) Array.Clear(xf, 0, Slots);
                    int[] last, own; _lastNew.TryGetValue(t, out last); _lastOwn.TryGetValue(t, out own);
                    for (int i = 0; i < Slots; i++)
                    {
                        total[i] += c[i];
                        if (c[i] > 0) towns[i]++;
                        if (last != null && day - last[i] < 7) fresh[i]++;
                        if (own != null && day - own[i] < 7) freshOwn[i]++;
                        if (keep > 0) reserve[i / 3] += Math.Min(c[i], keep);
                    }
                }
                catch (Exception e) { Stumble("Census(miasto)", e); }
            }
            var sb = new StringBuilder();
            sb.Append("Zbroja na polkach (174b): dzien ").Append(day).Append(" - miast ").Append(nTowns);
            for (int k = 0; k < 4; k++)
            {
                sb.Append("; ").Append(KindNames[k]).Append(':');
                for (int b = 0; b < 3; b++)
                {
                    int i = k * 3 + b;
                    sb.Append(b == 0 ? " " : ", ").Append(BandNames[b]).Append(' ').Append(total[i]).Append(" szt./").Append(towns[i]).Append(" miast/nowa w 7 dob ").Append(fresh[i]);
                }
            }
            sb.Append("; nowa w 7 dob bez przerzutu bez partii (kupcy dzienni, wozy zamkow) t1-2/t3-4/t5-6:");   // poprawka 174b (recenzja): P5 bez przerzutu
            for (int k = 0; k < 4; k++) sb.Append(k == 0 ? " " : ", ").Append(KindNames[k]).Append(' ').Append(freshOwn[k * 3]).Append('/').Append(freshOwn[k * 3 + 1]).Append('/').Append(freshOwn[k * 3 + 2]);
            if (keep > 0) sb.Append("; rezerwa kramu: sztuk ").Append(reserve[0] + reserve[1] + reserve[2] + reserve[3]).Append(" (korpus ").Append(reserve[0]).Append(", helm ").Append(reserve[1])
                             .Append(", nogi ").Append(reserve[2]).Append(", rece ").Append(reserve[3]).Append(')');
            else sb.Append("; rezerwa kramu: wylaczona");
            sb.Append("; nowe sztuki zbroi wedlug zrodla:");
            for (int a = 0; a < ArrKinds; a++) sb.Append(a == 0 ? " " : ", ").Append(ArrNames[a]).Append(' ').Append(_arr[a]);
            sb.Append("; potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static void BuyLine(int day)
        {
            var sb = new StringBuilder();
            sb.Append("ZakupyAI wedlug kupujacego (174b): dzien ").Append(day).Append(" -");
            bool anyB = false;
            for (int b = 0; b < Buyers; b++)
            {
                var part = Groups0(b);
                if (part == null) continue;
                sb.Append(anyB ? "; " : " ").Append(BuyerNames[b]).Append(" [").Append(part).Append(']');
                anyB = true;
            }
            if (!anyB) sb.Append(" brak zakupow");
            sb.Append("; nadwyzki sprzedane z sakiewek ludzi [");
            bool anyS = false;
            for (int g = 0; g < Groups; g++)
            {
                if (_sellN[g] <= 0) continue;
                sb.Append(anyS ? ", " : "").Append(GroupNames[g]).Append(' ').Append(_sellN[g]).Append('/').Append(_sellG[g]).Append(" zl");
                anyS = true;
            }
            if (!anyS) sb.Append('-');
            sb.Append(']');
            long held = 0; foreach (var h in _held) held += h;
            if (ShopReserve.Pieces > 0 || held > 0)
            {
                sb.Append("; zatrzymane na rezerwie kramu ").Append(held).Append(" szt. [");
                bool anyH = false;
                for (int b = 0; b < Buyers; b++) { if (_held[b] <= 0) continue; sb.Append(anyH ? ", " : "").Append(BuyerNames[b]).Append(' ').Append(_held[b]); anyH = true; }
                if (!anyH) sb.Append('-');
                sb.Append(']');
            }
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        private static string Groups0(int b)
        {
            StringBuilder sb = null;
            for (int g = 0; g < Groups; g++)
            {
                if (_buyN[b, g] <= 0) continue;
                if (sb == null) sb = new StringBuilder(); else sb.Append(", ");
                sb.Append(GroupNames[g]).Append(' ').Append(_buyN[b, g]).Append('/').Append(_buyG[b, g]).Append(" zl");
            }
            return sb != null ? sb.ToString() : null;
        }
    }
}
