using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// r4-glodbunt (noc 17/18.10, Jeff: "jak nie ma jedzenia to bedzie bunt; w interesie miasta lorda jest aby dowiesc jedzenie") - ODCZYT koszyka ZC4 miasta dla
    /// glodu miast (KsHunger) i pana placacego za dowoz (KsLordFood). Tylko odczyt mapy koszyka doby (Zc4Build) i polki miasta - nic tu nie zmienia stanu gry.
    /// </summary>
    internal static partial class StartCalibration
    {
        /// <summary>Posilek miasta (postfiks UpdateDemandShift - tuz przed konsumpcja doby tego miasta): kcal koszyka mieszczan na dzis (koszyk ZC4 zaplacony z Planu
        /// x lambda rynku - jedzenie i napoje, KsBasket.Kcal > 0) i kcal tych samych towarow na polce teraz (kazda sztuka pozycji koszyka, takze z modyfikatorem).
        /// Kcal sa wymienne (tansze jedzenie zamiast drozszego - Jeff 17.10): polka liczy wszystkie pozycje razem. lam - lambda rynku miasta.
        /// false - poza ZC4 (kalibracja nie zastosowana, klucz wylaczony, miasto bez mapy koszyka, lambda 0).</summary>
        internal static bool KsTownMeal(Town town, out double needKcal, out double shelfKcal, out double lam)
        {
            needKcal = 0; shelfKcal = 0; lam = 0;
            ZTown zt;
            if (!ZTownOf(town, out zt)) return false;
            Zc4EnsureMap();
            lam = zt.Lam;
            var kg = _zKg; var ix = _zIx;
            if (kg == null || ix == null) return false;
            for (int z = 0; z < ZN; z++)
            {
                double k = KsBasket.Kcal[z];
                double q = zt.Q[z];
                if (!(k > 0) || !(q > 0) || double.IsNaN(q)) continue;
                needKcal += q * lam * kg[z] * k;
            }
            var ro = town.Settlement != null ? town.Settlement.ItemRoster : null;
            if (ro != null)
                for (int i = 0; i < ro.Count; i++)
                {
                    var el = ro.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    int z;
                    if (it == null || el.Amount <= 0 || !ix.TryGetValue(it, out z)) continue;
                    double k = KsBasket.Kcal[z];
                    if (!(k > 0)) continue;
                    shelfKcal += el.Amount * kg[z] * k;
                }
            return true;
        }

        /// <summary>Ziarno na polce miasta: kcal jednej sztuki (kg sztuki x kcal na kg) i cena sztuki z mapy doby (Zc4Build), bez niej - cena targu gry.
        /// false - poza ZC4 albo brak ziarna w swiecie gry.</summary>
        internal static bool KsTownGrain(Town town, out double kcalPiece, out int price)
        {
            kcalPiece = 0; price = 0;
            ZTown zt;
            if (!ZTownOf(town, out zt)) return false;
            Zc4EnsureMap();
            var it = _zItems != null ? _zItems[ZGrain] : null;
            if (it == null || _zKg == null) return false;
            kcalPiece = _zKg[ZGrain] * KsBasket.Kcal[ZGrain];
            price = zt.Px[ZGrain];
            if (price <= 0) { try { price = town.MarketData.GetPrice(it, null, false, null); } catch { price = 0; } }
            return kcalPiece > 0 && price > 0;
        }
    }
}
