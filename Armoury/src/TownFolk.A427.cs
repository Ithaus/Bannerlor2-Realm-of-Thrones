using System;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// 427 (audyt ekonomii 11.11, A1) - odczyty licznikow kiesy ludu dla pomiarow EcoAudit427 i dopisek linii "Straznik obiegu (obieg2)". Niczego nie zmienia:
    /// same odczyty stanu tej doby (klucz Economy Audit Lines wylaczony - dopisek pusty, linia co do znaku jak dotad).
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>427: danina od skarbca ludu tej doby z kies ludu miast i podzamczy oraz (dawna regula obieg2) wsi - to samo co pozycja "kiesa2 i straznik" linii "Pompy".</summary>
        internal static long A427HoardTake
        {
            get { return LastHoardLord + LastHoardTown + LastHoardCastle + LastHoardCrown + LastVillHoardLord + LastVillHoardTown + LastVillHoardCastle; }
        }

        /// <summary>427: danina od skarbca wsi DS (Ds Hoard Due Villages) od ostatniej linii "Akumulator kies" - pobrano, do panow, do kas osad macierzystych.</summary>
        internal static long A427VillHoardTake { get { return _dvTake; } }
        internal static long A427VillHoardLord { get { return _dvLord; } }
        internal static long A427VillHoardCoffer { get { return _dvCoffer; } }

        /// <summary>
        /// 427 A1: dopisek linii "Straznik obiegu (obieg2)" - ktora stawka danin od skarbca ludu NAPRAWDE obowiazuje tej doby (przy firmach DS straznik jest wylaczony:
        /// GuardOn = false, PayHoard bierze DaRateNow; "mnoznik" i "stawka doby" straznika to wtedy cien), stawka faktyczna = pobrane / nadwyzka ponad poziom normalny,
        /// oraz gracz osobno od lordow AI (posiadacze ksiegi pieniadza z konca poprzedniej doby - te same co udzialy w tej linii). Nie rzuca.
        /// </summary>
        private static string A427GuardNote(Settings s)
        {
            if (s == null || !EcoAudit427.On) return "";
            try
            {
                var sb = new StringBuilder(520);
                bool ds = DaDueOn, guard = GuardOn && _gdReady;
                double rate = ds ? DaRateNow(s) : HoardRateNow(s);
                long take = _hdTake[0] + _hdTake[1] + _hdTake[2] + _hdTake[3];
                sb.Append(" | 427 stawka OBOWIAZUJACA danin od skarbca ludu miast i podzamczy dzis ").Append((100 * rate).ToString("0.000", Inv)).Append("%/d - regula: ")
                  .Append(ds ? "danina przy firmach DS (Ds Hoard Due; klasy z placa wedlug koszyka maja stawke nizsza - linia 'Akumulator kies')"
                             : guard ? "straznik obiegu (stawka doby tej linii)" : "stawka podstawowa (Folk Hoard Tax Rate)")
                  .Append(guard ? "" : "; straznik NIE steruje poborem (przy firmach DS wylaczony) - 'mnoznik' i 'stawka doby' tej linii to cien")
                  .Append("; faktycznie pobrano ").Append(take).Append(" z nadwyzki ponad poziom normalny ").Append(N0(_hdE0)).Append(" = ")
                  .Append(_hdE0 > 0 ? (100.0 * take / _hdE0).ToString("0.000", Inv) + "%/d" : "-");
                long player, ai;
                if (_gdShares && MoneyLedger.A427LordsSplit(out player, out ai))
                    sb.Append(" | 427 lordowie w rozbiciu: gracz ").Append(player).Append(" (").Append(Pct(player, _gdWorld)).Append(" swiata), lordowie AI ").Append(ai)
                      .Append(" (").Append(Pct(ai, _gdWorld)).Append("); kontrola: gracz + AI - lordowie = ").Append(player + ai - _gdLords);
                return sb.ToString();
            }
            catch (Exception e) { EcoAudit427.Stumble("GuardNote", e); return ""; }
        }
    }
}
