using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// OBCE GLOSY BITWY (16.10, przeglad modow wf_2219635c-21e: ROT Battle Atmosphere 1.5.0 i Immersive Battlefields 3.6.2 - Jeff je wgrywa).
    /// Nasze okrzyki szarzy (BattleCries) ustepuja tym modom, zeby nic nie krzyczalo podwojnie. Wykrycie raz na misje w BattleCries.Begin
    /// (AfterStart - wszystkie OnMissionBehaviorInitialize juz przeszly, wiec IB_Core i logika BA sa w Mission.MissionBehaviors):
    ///  - ibCore: zachowanie "Immersive_Battlefields.IB_Core" (klasa internal); ibShout: jego pole "Setting_Shout" (okrzyk szarzy IB kazdym
    ///    zolnierzem 2 razy - u Jeffa wylaczony w MCM); blad odczytu = tak (ustap);
    ///  - ba: zachowanie "ROTBattleAtmosphere.BattleAtmosphereMissionLogic" (BA nie dziala w misjach z rozmowa, np. kryjowki);
    ///    baAiVolley / baChorus: ModConfig.Instance.Enabled i EnableAiChargeVoices / EnableChargeChorus; blad = tak;
    ///  - ibPatch: prefiks wlasciciela "Immersive_Battlefields_Harmony" na OrderController.PlayOrderGestures (IB wycina natywny okrzyk szarzy gracza);
    ///    sprawdzane na starcie misji, bo hak menu IB idzie po naszym.
    /// Tryb szyku AI: IB krzyczy (ibCore i ibShout) - nic; salwa BA - tylko kapitan; inaczej pelny (jak bez obcych modow).
    /// Szyk gracza: krzyczymy tylko, gdy IB zabral natywny okrzyk, a nie ma choru BA ani okrzyku IB (np. kryjowka) - zastepujemy to, co IB wycial.
    /// Wyjatek przy wykryciu = stan "nieznany" i tryb zachowawczy (AI: tylko kapitan, gracz: nic), licznik w linii. Nic w zapisie.
    /// noc 25.10 P2 (dzwiek bitwy, BattleSound): dodatkowo rog szarzy IB (Settings1.Instance.EnableHorn / EnableHorn2 i progi SmallHorn / SmallHorn2 -
    /// IB_Library.PlayChargeHornSoundVanilla gra plaski rog 2D przy szarzy: druzyna 0 = strona gracza, 1 = wrog; tryb dodatku rogow IB
    /// SettingsLoader1.Custom_Addon_Mode - rogi przy wielu rozkazach) oraz ROTBA OrderVoiceChance (rozkazy gracza glosem zolnierzy), EnableHouseWords
    /// i EnablePostBattleCheering. Blad odczytu = tryb zachowawczy: IB gra rog szarzy obu stron (nasz rog szarzy milczy), BA mowi rozkazy.
    /// Segment linii NB2 "obce glosy" bez znaku '|' (wzor sprawdz_logi NBW_LINE: "obce glosy: [^|]+").
    /// </summary>
    internal static class ForeignVoices
    {
        internal const int ModeNone = 0, ModeCaptain = 1, ModeFull = 2;

        private static bool _ibCore, _ibShout, _ba, _baAiVolley, _baChorus, _ibPatch, _failed;
        private static int _stumbles;
        // noc 25.10 P2: rog szarzy IB (strona gracza / wrog, progi ludzi), dodatek rogow IB, ROTBA: rozkazy, slowa rodow, wiwat po bitwie
        private static bool _ibHorn, _ibHorn2, _ibAddon, _baOrders, _baWords, _baPostCheer, _hornFailed;
        private static int _ibSmall = 1, _ibSmall2 = 1;

        private static bool _typesTried;
        private static Type _tBaCfg;
        private static PropertyInfo _pInst, _pEnabled, _pAiVoices, _pChorus;
        private static PropertyInfo _pOrderChance, _pHouseWords, _pPostCheer;   // noc 25.10 P2
        private static Type _tIbSet, _tIbLoader;                                  // noc 25.10 P2: Immersive_Battlefields.Settings1 / SettingsLoader1
        private static PropertyInfo _pIbInst, _pIbHorn, _pIbHorn2, _pIbSmall, _pIbSmall2;
        private static MemberInfo _mIbAddon;
        private static MethodBase _gestures;

        /// <summary>noc 25.10 P2: IB zagra wlasny plaski rog przy szarzy szyku tej strony (playerSide - druzyna gracza i sojusznicy) i tej liczebnosci.
        /// Tylko gdy IB jest w misji; blad odczytu ustawien IB = tak (ustepujemy).</summary>
        internal static bool IbChargeHorn(bool playerSide, int men)
        {
            if (!_ibCore) return false;
            if (_hornFailed || _ibAddon) return true;
            return playerSide ? (_ibHorn && men >= _ibSmall) : (_ibHorn2 && men >= _ibSmall2);
        }

        /// <summary>noc 25.10 P2: ROTBA mowi rozkazy gracza glosami zolnierzy (OrderVoiceChance > 0) - nasze rozkazy gracza co drugi raz.</summary>
        internal static bool BaOrderVoices { get { return _ba && _baOrders; } }

        /// <summary>noc 25.10 P2: ROTBA wiwatuje po bitwie (EnablePostBattleCheering) - informacja do linii dzwieku.</summary>
        internal static bool BaPostCheer { get { return _ba && _baPostCheer; } }

        /// <summary>noc 25.10 P2: ROTBA krzyczy slowa rodow ustami zolnierzy (EnableHouseWords) - informacja do linii dzwieku.</summary>
        internal static bool BaHouseWords { get { return _ba && _baWords; } }

        internal static bool IbPresent { get { return _ibCore; } }

        internal static int AiMode { get; private set; } = ModeFull;
        internal static int PlayerMode { get; private set; } = ModeNone;

        internal static void Detect(Mission m)
        {
            _ibCore = _ibShout = _ba = _baAiVolley = _baChorus = _ibPatch = _failed = false;
            _ibHorn = _ibHorn2 = _ibAddon = _baOrders = _baWords = _baPostCheer = _hornFailed = false;
            _ibSmall = _ibSmall2 = 1;
            AiMode = ModeFull; PlayerMode = ModeNone;
            try
            {
                var list = m != null ? m.MissionBehaviors : null;
                int n = list != null ? list.Count : 0;
                for (int i = 0; i < n; i++)
                {
                    var b = list[i];
                    if (b == null) continue;
                    string fn = b.GetType().FullName;
                    if (fn == "Immersive_Battlefields.IB_Core")
                    {
                        _ibCore = true;
                        _ibShout = true;
                        try
                        {
                            var f = AccessTools.Field(b.GetType(), "Setting_Shout");
                            if (f != null && f.FieldType == typeof(bool)) _ibShout = (bool)f.GetValue(b);
                        }
                        catch { _ibShout = true; }
                    }
                    else if (fn == "ROTBattleAtmosphere.BattleAtmosphereMissionLogic") _ba = true;
                }
                if (_ba) ReadBaConfig();
                if (_ibCore) ReadIbHorns();   // noc 25.10 P2: rog szarzy IB
                _ibPatch = IbPatched();
            }
            catch (Exception e)
            {
                _failed = true; _stumbles++;
                if (_stumbles <= 3) Log.Error("ForeignVoices.Detect", e);
            }
            if (_failed) { AiMode = ModeCaptain; PlayerMode = ModeNone; return; }
            AiMode = (_ibCore && _ibShout) ? ModeNone : (_ba && _baAiVolley) ? ModeCaptain : ModeFull;
            PlayerMode = (_ibPatch && !(_ba && _baChorus) && !(_ibCore && _ibShout)) ? ModeFull : ModeNone;
        }

        private static void ResolveTypes()
        {
            if (_typesTried) return;
            _typesTried = true;
            _tBaCfg = AccessTools.TypeByName("ROTBattleAtmosphere.ModConfig");
            if (_tBaCfg != null)
            {
                _pInst = AccessTools.Property(_tBaCfg, "Instance");
                _pEnabled = AccessTools.Property(_tBaCfg, "Enabled");
                _pAiVoices = AccessTools.Property(_tBaCfg, "EnableAiChargeVoices");
                _pChorus = AccessTools.Property(_tBaCfg, "EnableChargeChorus");
                _pOrderChance = AccessTools.Property(_tBaCfg, "OrderVoiceChance");   // noc 25.10 P2
                _pHouseWords = AccessTools.Property(_tBaCfg, "EnableHouseWords");
                _pPostCheer = AccessTools.Property(_tBaCfg, "EnablePostBattleCheering");
            }
            // noc 25.10 P2: ustawienia IB (klasy publiczne; Settings1.Instance statyczne, wlasciwosci czytaja MCM albo XML IB)
            _tIbSet = AccessTools.TypeByName("Immersive_Battlefields.Settings1");
            if (_tIbSet != null)
            {
                _pIbInst = AccessTools.Property(_tIbSet, "Instance");
                _pIbHorn = AccessTools.Property(_tIbSet, "EnableHorn");
                _pIbHorn2 = AccessTools.Property(_tIbSet, "EnableHorn2");
                _pIbSmall = AccessTools.Property(_tIbSet, "SmallHorn");
                _pIbSmall2 = AccessTools.Property(_tIbSet, "SmallHorn2");
            }
            _tIbLoader = AccessTools.TypeByName("Immersive_Battlefields.SettingsLoader1");
            if (_tIbLoader != null)
                _mIbAddon = (MemberInfo)AccessTools.Field(_tIbLoader, "Custom_Addon_Mode") ?? AccessTools.Property(_tIbLoader, "Custom_Addon_Mode");
            _gestures = AccessTools.Method(typeof(OrderController), "PlayOrderGestures");
        }

        /// <summary>noc 25.10 P2: rog szarzy IB (IB_Library.PlayChargeHornSoundVanilla: druzyna 0 gracz EnableHorn + SmallHorn, druzyna 1 wrog EnableHorn2 +
        /// SmallHorn2). Blad albo brak typu = tryb zachowawczy (IB gra - nasz rog szarzy milczy).</summary>
        private static void ReadIbHorns()
        {
            ResolveTypes();
            _hornFailed = true;
            try
            {
                if (_mIbAddon is FieldInfo fa && fa.FieldType == typeof(bool)) _ibAddon = (bool)fa.GetValue(null);
                else if (_mIbAddon is PropertyInfo pa && pa.PropertyType == typeof(bool)) _ibAddon = (bool)pa.GetValue(null, null);
                object cfg = _pIbInst != null ? _pIbInst.GetValue(null, null) : null;
                if (cfg == null || _pIbHorn == null || _pIbHorn2 == null) return;
                _ibHorn = (bool)_pIbHorn.GetValue(cfg, null);
                _ibHorn2 = (bool)_pIbHorn2.GetValue(cfg, null);
                if (_pIbSmall != null) _ibSmall = Math.Max(1, (int)_pIbSmall.GetValue(cfg, null));
                if (_pIbSmall2 != null) _ibSmall2 = Math.Max(1, (int)_pIbSmall2.GetValue(cfg, null));
                _hornFailed = false;
            }
            catch { _hornFailed = true; }
        }

        private static void ReadBaConfig()
        {
            ResolveTypes();
            _baAiVolley = _baChorus = true;
            _baOrders = _baWords = true; _baPostCheer = false;   // noc 25.10 P2: blad = BA mowi rozkazy (nasze co drugi raz)
            try
            {
                object cfg = _pInst != null ? _pInst.GetValue(null, null) : null;
                if (cfg == null) return;
                bool on = _pEnabled == null || (bool)_pEnabled.GetValue(cfg, null);
                _baAiVolley = on && (_pAiVoices == null || (bool)_pAiVoices.GetValue(cfg, null));
                _baChorus = on && (_pChorus == null || (bool)_pChorus.GetValue(cfg, null));
                // noc 25.10 P2: rozkazy gracza glosem BA, slowa rodow, wiwat po bitwie
                _baOrders = on && (_pOrderChance == null || Convert.ToSingle(_pOrderChance.GetValue(cfg, null)) > 0f);
                _baWords = on && (_pHouseWords == null || (bool)_pHouseWords.GetValue(cfg, null));
                _baPostCheer = on && _pPostCheer != null && (bool)_pPostCheer.GetValue(cfg, null);
            }
            catch { _baAiVolley = _baChorus = true; _baOrders = _baWords = true; _baPostCheer = false; }
        }

        private static bool IbPatched()
        {
            ResolveTypes();
            if (_gestures == null) return false;
            var info = Harmony.GetPatchInfo(_gestures);
            if (info == null || info.Prefixes == null) return false;
            foreach (var p in info.Prefixes)
                if (p != null && p.owner == "Immersive_Battlefields_Harmony") return true;
            return false;
        }

        internal static void Append(StringBuilder sb)
        {
            sb.Append(" | obce glosy: IB ").Append(_ibCore ? "tak" : "nie").Append(" (okrzyk ").Append(_ibShout ? "tak" : "nie")
              .Append(", latka rozkazow ").Append(_ibPatch ? "tak" : "nie").Append("), BA ").Append(_ba ? "tak" : "nie")
              .Append(" (salwa AI ").Append(_baAiVolley ? "tak" : "nie").Append(", chor ").Append(_baChorus ? "tak" : "nie")
              .Append(") -> nasze szarze AI ").Append(AiMode == ModeFull ? "pelne" : AiMode == ModeCaptain ? "kapitan" : "brak")
              .Append(", gracza ").Append(PlayerMode == ModeFull ? "tak" : "nie");
            sb.Append("; ");
            AppendHorns(sb);   // noc 25.10 P2 (bez znaku '|' - wzor NBW_LINE)
            if (_failed || _stumbles > 0) sb.Append(", potkniecia wykrycia ").Append(_stumbles);
        }

        /// <summary>noc 25.10 P2: rog szarzy IB i ustawienia BA, ktorym ustepuje dzwiek bitwy - takze do linii "Dzwiek bitwy" (bez znaku '|').</summary>
        internal static void AppendHorns(StringBuilder sb)
        {
            sb.Append("rog szarzy IB ");
            if (!_ibCore) sb.Append("brak");
            else if (_hornFailed) sb.Append("nieznany (ustepujemy)");
            else sb.Append("gracz ").Append(_ibHorn ? "tak od " + _ibSmall : "nie").Append(", wrog ").Append(_ibHorn2 ? "tak od " + _ibSmall2 : "nie")
                   .Append(_ibAddon ? ", dodatek rogow tak" : "");
            sb.Append(", BA rozkazy ").Append(BaOrderVoices ? "tak" : "nie").Append(", slowa rodow ").Append(BaHouseWords ? "tak" : "nie")
              .Append(", wiwat po bitwie ").Append(BaPostCheer ? "tak" : "nie");
        }
    }
}
