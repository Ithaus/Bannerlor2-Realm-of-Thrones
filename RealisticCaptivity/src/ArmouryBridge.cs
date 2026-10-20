using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace RealisticCaptivity
{
    /// <summary>
    /// 178 (projekt etapu 2, krok D - Armoury): okupy wedlug majatku licza sie w Armoury (D rodu, ksiega dlugow 168). RC pyta Armoury przez refleksje
    /// (bez twardej zaleznosci - Armoury moze nie byc wczytane): cena lorda (posrednik, ekran druzyny, jeniec u gracza) i gotowka okupu gracza.
    /// Gdy Armoury nie ma albo 178 jest wylaczone - RC liczy po swojemu, jak dotad.
    /// </summary>
    internal static class ArmouryBridge
    {
        private static bool _tried;
        private static MethodInfo _active, _lordPrice, _playerRansom;

        private static void Resolve()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var t = AccessTools.TypeByName("Armoury.Ransom178");
                if (t == null) return;
                _active = AccessTools.Method(t, "RcActive");
                _lordPrice = AccessTools.Method(t, "RcLordPrice", new[] { typeof(Hero), typeof(Hero), typeof(int) });
                _playerRansom = AccessTools.Method(t, "RcPlayerRansom");
            }
            catch (Exception e) { Log.Error("ArmouryBridge.Resolve", e); }
        }

        /// <summary>Czy okupy wedlug majatku (Armoury 178) sa czynne.</summary>
        internal static bool Active
        {
            get { Resolve(); try { return _active != null && (bool)_active.Invoke(null, null); } catch { return false; } }
        }

        /// <summary>Cena lorda wedlug Armoury albo -1 (RC liczy po swojemu).</summary>
        internal static int LordPrice(Hero h, Hero seller, int vanilla)
        {
            Resolve();
            try { return _lordPrice != null ? (int)_lordPrice.Invoke(null, new object[] { h, seller, vanilla }) : -1; }
            catch { return -1; }
        }

        /// <summary>Gotowka okupu gracza wedlug Armoury albo -1.</summary>
        internal static int PlayerRansom()
        {
            Resolve();
            try { return _playerRansom != null ? (int)_playerRansom.Invoke(null, null) : -1; }
            catch { return -1; }
        }

        // n20-niewola: unikaty (spis ROT i stal valyrianska) ida przy pojmaniu TYLKO zwyczajem wojny Armoury (UniqueSpoils.Take - zdobywca-bohater je
        // zaklada, pojmany dostaje zamiennik); RC ich nie bierze do jukow zdobywcy (stal valyrianska w taborze AI znika - UniqueSpoils 177-fix).
        // Armoury.UniqueSpoils.Is(ItemObject) - internal static, przez refleksje (bez twardej zaleznosci); bez Armoury = nic nie jest unikatem.
        private static bool _uTried;
        private static MethodInfo _isUnique;
        private static int _uStumbles;

        private static void ResolveUnique()
        {
            if (_uTried) return;
            _uTried = true;
            try
            {
                var t = AccessTools.TypeByName("Armoury.UniqueSpoils");
                if (t != null) _isUnique = AccessTools.DeclaredMethod(t, "Is", new[] { typeof(TaleWorlds.Core.ItemObject) });
            }
            catch (Exception e) { Log.Error("ArmouryBridge.ResolveUnique", e); }
        }

        internal static bool IsUnique(TaleWorlds.Core.ItemObject it)
        {
            ResolveUnique();
            if (it == null || _isUnique == null) return false;
            try { return (bool)_isUnique.Invoke(null, new object[] { it }); }
            catch (Exception e) { if (_uStumbles++ == 0) Log.Error("ArmouryBridge.IsUnique", e); return false; }
        }

        /// <summary>Czy Armoury z UniqueSpoils jest wczytane (linia logu n20).</summary>
        internal static bool UniqueKnown { get { ResolveUnique(); return _isUnique != null; } }
    }
}
