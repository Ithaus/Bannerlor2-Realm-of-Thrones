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
    /// 317a (Jeff 02.11: "klute przed cietymi tak"): STRZALY I BELTY pod RBM tez z runtime - ROT podmienia
    /// je w locie tak samo (ravens_teeth_arrows -> 120 Pierce speed 120 "Ravens' Teeth Arrows 100 gram"),
    /// a stare Effectiveness (papier 01.11 02:19: GRE_hardened_100 skut=2327 uzyte 6/6, ravens_teeth_arrows
    /// skut=146 uzyte 0/24) wypychalo klute na koniec. RBM (RBMCombat.RBMComputeDamage) dla Pierce ze
    /// strzaly/beltu polowi skutecznosc zbroi metalowej (material > 0 i != 4) - dlatego klucz amunicji:
    /// TYP OBRAZEN (Pierce 3, Cut 2, Blunt 1, inny 0), potem ThrustDamage, potem MissileSpeed, potem stara
    /// skutecznosc. Jedna regula dla papieru kwatermistrza, druzyny, dozbrajania ludzi, wzorcow SkillsDecide
    /// i strazy bitewnej CrashScribe (Mends.RangedKey - trzymac w zgodzie).
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

        internal static bool IsAmmo(ItemObject it)
        {
            return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts);
        }

        /// <summary>317a: ranga typu obrazen amunicji pod RBM - Pierce 3, Cut 2, Blunt 1, inny 0.</summary>
        internal static int AmmoTypeRank(DamageTypes t)
        {
            return t == DamageTypes.Pierce ? 3 : t == DamageTypes.Cut ? 2 : t == DamageTypes.Blunt ? 1 : 0;
        }

        private static string TypeShort(DamageTypes t)
        {
            return t == DamageTypes.Pierce ? "P" : t == DamageTypes.Cut ? "C" : t == DamageTypes.Blunt ? "B" : "?";
        }

        /// <summary>Im wyzej, tym lepsza sztuka (przy tym samym wymogu).</summary>
        internal static long Key(ItemObject it)
        {
            if (it == null) return long.MinValue;
            try
            {
                if (!RbmLoaded || it.PrimaryWeapon == null || !(IsBowLike(it) || IsAmmo(it))) return (long)(it.Effectiveness * 100f);
                var w = it.PrimaryWeapon;
                float eff = it.Effectiveness; if (eff < 0f) eff = 0f; if (eff > 999f) eff = 999f;
                if (IsAmmo(it))
                {
                    // 317a: typ obrazen, dmg, predkosc (0..999 kazde), stara skutecznosc
                    long dmg = Math.Max(0, Math.Min(999, w.ThrustDamage)), spd = Math.Max(0, Math.Min(999, w.MissileSpeed));
                    return AmmoTypeRank(w.ThrustDamageType) * 1000000000L + dmg * 1000000L + spd * 1000L + (long)eff;
                }
                return (long)w.MissileSpeed * 1000000L + (long)w.ThrustDamage * 1000L + (long)eff;
            }
            catch { return 0; }
        }

        /// <summary>Do logu: naciag i dmg pod RBM, skutecznosc poza nim; amunicja pod RBM (317a) - "dmg=120P speed=120".</summary>
        internal static string Describe(ItemObject it)
        {
            try
            {
                if (RbmLoaded && IsAmmo(it) && it.PrimaryWeapon != null)
                    return "dmg=" + it.PrimaryWeapon.ThrustDamage + TypeShort(it.PrimaryWeapon.ThrustDamageType) + " speed=" + it.PrimaryWeapon.MissileSpeed;
                if (RbmLoaded && IsBowLike(it) && it.PrimaryWeapon != null)
                    return "naciag=" + it.PrimaryWeapon.MissileSpeed + "lb dmg=" + it.PrimaryWeapon.ThrustDamage;
                return "skut=" + it.Effectiveness.ToString("0");
            }
            catch { return "?"; }
        }
    }
}
