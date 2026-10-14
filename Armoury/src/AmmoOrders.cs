using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// tb (14.10, inwentarz "strzaly AI"; test fin27 SZ-A9a-4: pokrycie strzal partii rodow AI 52% przy progu 59, W53 59%, W50 74%) - ZAMOWIENIE AMUNICJI = CALY BRAK.
    /// Klucz AiAmmoOrderFull.
    ///
    /// Dowod (logi D:\Backup-Bannerlord\logs\2026-10-14-noc, linie "Pokrycie zbrojowni AI (171)", "Zuzycie AI (E8/W)", "Strzelarze (172)", "ZakupyAI wedlug kupujacego (174b)"):
    ///  - bez AiAmmoUse (W50 fin21, fin20, W52 fin25) pokrycie strzal lordow AI po spadku w d20-40 (wojsko 55 -> 91 tys. ludzi) wraca do 69-74% w d120; z AiAmmoUse (W53 fin26,
    ///    fin27, fin29) zostaje na 52-59% - zuzycie 129-145 strzal/d, a zakupy amunicji lordow 245-268/d wobec 238-250/d bez zuzycia (prawie bez wzrostu);
    ///  - podazy nie brakuje w sumie: strzaly na polkach 96 z 97 miast (2 500-2 900 szt.), rece strzelarzy zajete w 20-26%, koniec pracy "bez zysku" w 56-62 z 97 miast w d120;
    ///  - brakuje towaru W MIEJSCU zakupu: "AI bez towaru" 28-35 zamowien/d (kupiec wyszedl z brakiem i bez sztuki tego tieru na polce). Polka miasta stoi na popycie
    ///    cywilnym (SupplyDemandBase 4 szt. na koszyk x zamoznosc x waga tieru) plus zamowienia AI - a zamowienie z wizyty bylo najwyzej 10 szt. na koszyk (wpis 67),
    ///    raz na SupplyDemandOrderRepeatDays i wygasa 15%/d; brak partii lorda to dziesiatki kolczanow, wiec strzelarze widzieli ulamek popytu armii;
    ///  - C3 VetKit (weteran bierze tez kolczany) - wplyw maly: amunicja to ok. 7% sztuk zbrojowni AI (E6/0), wiec ok. 15 z 211 szt./d kompletow, z czego czesc wraca
    ///    na polki ("do domu") i do najemnikow - ok. 1/10 zuzycia AiAmmoUse (szacunek; linia C3 nie dzieli sztuk na typy). Roznica fin27 - W53 (4-6 pkt) miesci sie
    ///    w rozrzucie kampanii (W52 - fin20 3-4 pkt przy tych samych regulach strzal).
    /// Regula (z biezacego zysku, ta sama dla lordow AI, zalog miast i zamowien zamkow): kupiec, ktory po zakupach ma brak strzal albo beltow, a na polce nie ma juz
    /// sztuki tego tieru ani o jeden nizszej (AiGear.OnShelf - jak dotad), zostawia zamowienie na CALY ten brak (najwyzej SupplyDemandOrderCap = sufit otwartych
    /// zamowien koszyka miasta). Popyt koszyka rosnie, cena polki rosnie, strzelarze pracuja wolnymi rekami, dopoki cena pokrywa rude, drewno i dniowke
    /// (WorkshopMinProfitPercent) - zadnej sztuki z niczego, placi kupiec przy zakupie. Inne towary - 10 jak dotad (zbroja i bron kupuje sie raz na czlowieka,
    /// amunicja schodzi w kazdej bitwie). Wylaczony klucz = 10 jak przed tb.
    /// Linia doby "Amunicja AI - zamowienia (tb)": lordowie AI / zalogi miast / zamowienia zamkow - wizyty z brakiem amunicji po zakupach, brak, czy na polce zostala
    /// sztuka (budzet albo limit sztuk wizyty) czy polka byla pusta (zamowienie), otwarte zamowienia amunicji w miastach (tylko licznik).
    /// </summary>
    internal static class AmmoOrders
    {
        internal const int WLord = 0, WGarrison = 1, WCastle = 2, Who = 3;
        private static readonly string[] WhoName = { "lordowie AI", "zalogi miast", "zamowienia zamkow (w miescie)" };

        internal static bool On { get { var s = Settings.Current; return s != null && s.AiAmmoOrderFull; } }

        internal static bool IsAmmo(ItemObject.ItemTypeEnum t) { return t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts; }

        // liczniki doby (tylko log)
        private static readonly int[] _visits = new int[Who], _visitsShort = new int[Who], _emptyB = new int[Who], _leftB = new int[Who], _ordered = new int[Who], _repeat = new int[Who];
        private static readonly long[] _short = new long[Who], _emptyPcs = new long[Who], _leftPcs = new long[Who], _orderedPcs = new long[Who], _over10 = new long[Who];
        private static int _stumbles;

        internal static void Reset() { ClearDay(); _stumbles = 0; }

        private static void ClearDay()
        {
            Array.Clear(_visits, 0, Who); Array.Clear(_visitsShort, 0, Who); Array.Clear(_emptyB, 0, Who); Array.Clear(_leftB, 0, Who); Array.Clear(_ordered, 0, Who); Array.Clear(_repeat, 0, Who);
            Array.Clear(_short, 0, Who); Array.Clear(_emptyPcs, 0, Who); Array.Clear(_leftPcs, 0, Who); Array.Clear(_orderedPcs, 0, Who); Array.Clear(_over10, 0, Who);
        }

        /// <summary>Ile zamowic przy braku towaru na polce: strzaly i belty przy kluczu - caly brak (najwyzej SupplyDemandOrderCap); reszta i klucz wylaczony - najwyzej 10 (wpis 67).</summary>
        internal static float Amount(ItemObject.ItemTypeEnum ty, int deficit)
        {
            if (deficit <= 0) return 0f;
            if (!IsAmmo(ty) || !On) return Math.Min(10, deficit);
            var s = Settings.Current;
            float cap = Math.Max(1f, s != null ? s.SupplyDemandOrderCap : 60f);
            return Math.Min(cap, (float)deficit);
        }

        /// <summary>Wizyta kupca AI doszla do zamowien po zakupach (shortAmmo - zostal brak strzal albo beltow). Tylko licznik.</summary>
        internal static void NoteVisit(int who, bool shortAmmo)
        {
            if (who < 0 || who >= Who) return;
            _visits[who]++;
            if (shortAmmo) _visitsShort[who]++;
        }

        /// <summary>Koszyk amunicji z brakiem po zakupach: onShelf - na polce zostala sztuka tieru t albo t-1 (nie kupiona: budzet albo limit sztuk wizyty);
        /// inaczej polka pusta - zamowienie amount (ordered = weszlo; false - ten kupiec zamowil to w ostatnich dobach). Tylko licznik.</summary>
        internal static void NoteBasket(int who, int deficit, bool onShelf, bool ordered, float amount)
        {
            try
            {
                if (who < 0 || who >= Who || deficit <= 0) return;
                _short[who] += deficit;
                if (onShelf) { _leftB[who]++; _leftPcs[who] += deficit; return; }
                _emptyB[who]++; _emptyPcs[who] += deficit;
                if (ordered) { _ordered[who]++; _orderedPcs[who] += (long)Math.Round(amount); if (amount > 10f) _over10[who] += (long)Math.Round(amount - 10f); }
                else _repeat[who]++;
            }
            catch (Exception e) { _stumbles++; if (_stumbles == 1) Log.Error("AmmoOrders.NoteBasket", e); }
        }

        /// <summary>Linia doby "Amunicja AI - zamowienia (tb)" i zerowanie licznikow (po linii "Uprzaz (215)").</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                var sb = new StringBuilder(900);
                sb.Append("Amunicja AI - zamowienia (tb): dzien ").Append(day).Append(" | zamowienie strzal i beltow przy pustej polce ")
                  .Append(On ? "[WL caly brak, najwyzej " + Math.Max(1f, s.SupplyDemandOrderCap).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " szt.]" : "W CIENIU (AiAmmoOrderFull) - najwyzej 10 szt.");
                for (int w = 0; w < Who; w++)
                {
                    sb.Append(" | ").Append(WhoName[w]).Append(": wizyt ").Append(_visits[w]).Append(", z brakiem amunicji po zakupach ").Append(_visitsShort[w])
                      .Append(" (brak ").Append(_short[w]).Append(" szt.); polka bez tego tieru i nizszego ").Append(_emptyB[w]).Append(" koszykow / ").Append(_emptyPcs[w])
                      .Append(" szt. - zamowione ").Append(_ordered[w]).Append(" (").Append(_orderedPcs[w]).Append(" szt., ponad dawne 10: ").Append(_over10[w])
                      .Append("), juz zamowione w ostatnich dobach ").Append(_repeat[w]).Append("; na polce zostala sztuka (budzet albo limit sztuk wizyty) ")
                      .Append(_leftB[w]).Append(" koszykow / ").Append(_leftPcs[w]).Append(" szt.");
                }
                int ba, bb;
                float oa = SupplyDemand.OpenOrders(ItemObject.ItemTypeEnum.Arrows, out ba), ob = SupplyDemand.OpenOrders(ItemObject.ItemTypeEnum.Bolts, out bb);
                sb.Append(" | otwarte zamowienia w osadach: strzaly ").Append((long)Math.Round(oa)).Append(" szt. (").Append(ba).Append(" koszykow), belty ")
                  .Append((long)Math.Round(ob)).Append(" szt. (").Append(bb).Append(" koszykow) | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { _stumbles++; Log.Error("AmmoOrders.Daily", e); }
            ClearDay();
        }
    }
}
