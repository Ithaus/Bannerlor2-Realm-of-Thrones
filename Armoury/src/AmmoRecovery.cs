using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// STRZALY I BELTY SIE ZUZYWAJA (Jeff 04.10: "ok" na regule z docs/HISTORIA-STRZALY.md).
    /// Dotad amunicja byla niewyczerpalna: DTE oddaje kolczan do zbrojowni w CALOSCI, gdy nie jest pusty
    /// (Global.IsAmmoAndEmpty), a latka CrashScribe (Mends.QuiversComeBack) zwracala nawet puste.
    ///
    /// Historycznie (Poitiers, Towton, Bretania 1343; zakupy korony setkami tysiecy): strzaly zbieral ten,
    /// kto utrzymal pole; duzo wymagalo naprawy (pioro, grot), czesc przepadala. Regula (szacunek - zadne zrodlo
    /// nie podaje procentu): z WYSTRZELONYCH strzal zwyciezca odzyskuje `AmmoRecoverPercent` (45%) od razu i
    /// `AmmoRepairPercent` (20%) po naprawie przez fletcherow przy wojsku (wliczone w zold); reszta (~35%) przepada.
    /// Pokonany traci wszystko, co wystrzelil. Niewystrzelone zostaja. DTE liczy kolczan jako jedna sztuke, wiec
    /// kolczan wraca z prawdopodobienstwem: 1 - wystrzelona czesc x (1 - odzysk).
    /// Postfix na IsAmmoAndEmpty (Priority.Last - po latce CrashScribe): true = kolczan przepada.
    /// </summary>
    internal static class AmmoRecovery
    {
        private static readonly Random _rng = new Random();
        private static int _kept, _lost;

        public static void Postfix(MissionWeapon? __0, ref bool __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AmmoRecoveryEnabled || !__0.HasValue) return;
                var mw = __0.Value;
                if (mw.IsEmpty || mw.Item == null) return;
                var t = mw.Item.ItemType;
                if (t != ItemObject.ItemTypeEnum.Arrows && t != ItemObject.ItemTypeEnum.Bolts && t != ItemObject.ItemTypeEnum.Thrown) return;
                int max = Math.Max(1, (int)mw.ModifiedMaxAmount);
                float used = 1f - Math.Max(0, (int)mw.Amount) / (float)max;
                if (used <= 0.001f) { __result = false; return; }        // nic nie wystrzelil - kolczan wraca
                float recover = (s.AmmoRecoverPercent + s.AmmoRepairPercent) / 100f;
                bool? won = PlayerWon();
                if (won.HasValue && !won.Value) recover = 0f;             // pole w rekach wroga
                float lose = used * (1f - Math.Max(0f, Math.Min(1f, recover)));
                bool lost = _rng.NextDouble() < lose;
                __result = lost;
                if (lost) _lost++; else _kept++;
            }
            catch { }
        }

        /// <summary>Wynik bitwy gracza, gdy juz znany (przy zwrocie po bitwie); null w trakcie.</summary>
        private static bool? PlayerWon()
        {
            try
            {
                var m = Mission.Current;
                var r = m != null ? m.MissionResult : null;
                if (r == null) return null;
                return r.PlayerVictory;
            }
            catch { return null; }
        }

        internal static void AfterBattle()
        {
            if (_kept + _lost == 0) return;
            Log.Info("AmmoRecovery: po bitwie kolczanow/stosow wrocilo " + _kept + ", przepadlo " + _lost + " (wystrzelone, nieodzyskane).");
            _kept = _lost = 0;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.Global");
                var m = t != null ? AccessTools.Method(t, "IsAmmoAndEmpty", new[] { typeof(MissionWeapon?) }) : null;
                if (m != null) h.Patch(m, postfix: new HarmonyMethod(typeof(AmmoRecovery), nameof(Postfix)) { priority = Priority.Last });
                Log.Info("AmmoRecovery: zuzycie amunicji wedle historii " + (m != null ? "wpiete" : "BRAK DTE Global.IsAmmoAndEmpty") + ".");
            }
            catch (Exception e) { Log.Error("AmmoRecovery.ApplyAll", e); }
        }
    }
}
