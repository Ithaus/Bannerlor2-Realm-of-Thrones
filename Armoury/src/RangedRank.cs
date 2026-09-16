using System;
using HarmonyLib;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// RANGA BRONI STRZELECKIEJ POD RBM (Jeff 16.09: "mam lucznikow z Bow 140-170,
    /// a nie chca brac Weirwood"). Papier kwatermistrza i straz skilli sortowaly
    /// przy rownym wymogu po vanillowym ItemObject.Effectiveness. Gra liczy je RAZ,
    /// przy wczytaniu danych z XML - a ROT przy zaladowanym RBM nadpisuje potem
    /// w locie statystyki swoich lukow (ROTRBMCompatibility.ModifyBowsAndArrows:
    /// weirwood_bow MissileSpeed 93 -> 170, ravens_teeth_longbow 87 -> 210, nazwy
    /// "170 Pound...", wartosci). Effectiveness zostaje ze starych liczb: papier
    /// 16.09 11:30 - training_longbow skut=609, woodland_longbow 537, training_bow
    /// 515, Weirwood 91, Ravens Teeth 96 - i najlepsi strzelcy dostawali
    /// training longbow zamiast Ravens Teeth.
    /// W RBM sila luku to NACIAG: RBMCombat.RangedRework bierze
    /// CurrentUsageItem.MissileSpeed jako drawWeight w funtach i z niego liczy
    /// predkosc strzaly (Utilities.calculateMissileSpeed) - RBM-owe luki maja
    /// 60-200 lb, ROT-owe po nadpisaniu 170/180/200/210/500. Dlatego pod RBM
    /// klucz rangi luku/kuszy = MissileSpeed z RUNTIME (naciag), potem ThrustDamage,
    /// potem stara skutecznosc. Bez RBM - vanillowa skutecznosc jak dawniej.
    /// Wymog (Difficulty) zostaje pierwszym kryterium - to prawo tieru i zasada
    /// nadrzedna skilli; RBM za skill ponizej naciagu +9 daje tylko lagodna kare
    /// (UnloadWhenSheathed), nie zakaz.
    /// </summary>
    internal static class RangedRank
    {
        private static int _rbm = -1;

        internal static bool RbmLoaded
        {
            get
            {
                if (_rbm < 0)
                {
                    try { _rbm = AccessTools.TypeByName("RBM.SubModule") != null ? 1 : 0; } catch { _rbm = 0; }
                }
                return _rbm == 1;
            }
        }

        internal static bool IsBowLike(ItemObject it)
        {
            return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Bow || it.ItemType == ItemObject.ItemTypeEnum.Crossbow);
        }

        /// <summary>Im wyzej, tym lepsza sztuka (przy tym samym wymogu).</summary>
        internal static long Key(ItemObject it)
        {
            if (it == null) return long.MinValue;
            try
            {
                if (!RbmLoaded || !IsBowLike(it) || it.PrimaryWeapon == null) return (long)(it.Effectiveness * 100f);
                var w = it.PrimaryWeapon;
                float eff = it.Effectiveness; if (eff < 0f) eff = 0f; if (eff > 999f) eff = 999f;
                return (long)w.MissileSpeed * 1000000L + (long)w.ThrustDamage * 1000L + (long)eff;
            }
            catch { return 0; }
        }

        /// <summary>Do logu: naciag i dmg pod RBM, skutecznosc poza nim.</summary>
        internal static string Describe(ItemObject it)
        {
            try
            {
                if (RbmLoaded && IsBowLike(it) && it.PrimaryWeapon != null)
                    return "naciag=" + it.PrimaryWeapon.MissileSpeed + "lb dmg=" + it.PrimaryWeapon.ThrustDamage;
                return "skut=" + it.Effectiveness.ToString("0");
            }
            catch { return "?"; }
        }
    }
}
