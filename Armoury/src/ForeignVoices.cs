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
    /// </summary>
    internal static class ForeignVoices
    {
        internal const int ModeNone = 0, ModeCaptain = 1, ModeFull = 2;

        private static bool _ibCore, _ibShout, _ba, _baAiVolley, _baChorus, _ibPatch, _failed;
        private static int _stumbles;

        private static bool _typesTried;
        private static Type _tBaCfg;
        private static PropertyInfo _pInst, _pEnabled, _pAiVoices, _pChorus;
        private static MethodBase _gestures;

        internal static int AiMode { get; private set; } = ModeFull;
        internal static int PlayerMode { get; private set; } = ModeNone;

        internal static void Detect(Mission m)
        {
            _ibCore = _ibShout = _ba = _baAiVolley = _baChorus = _ibPatch = _failed = false;
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
            }
            _gestures = AccessTools.Method(typeof(OrderController), "PlayOrderGestures");
        }

        private static void ReadBaConfig()
        {
            ResolveTypes();
            _baAiVolley = _baChorus = true;
            try
            {
                object cfg = _pInst != null ? _pInst.GetValue(null, null) : null;
                if (cfg == null) return;
                bool on = _pEnabled == null || (bool)_pEnabled.GetValue(cfg, null);
                _baAiVolley = on && (_pAiVoices == null || (bool)_pAiVoices.GetValue(cfg, null));
                _baChorus = on && (_pChorus == null || (bool)_pChorus.GetValue(cfg, null));
            }
            catch { _baAiVolley = _baChorus = true; }
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
            if (_failed || _stumbles > 0) sb.Append(", potkniecia wykrycia ").Append(_stumbles);
        }
    }
}
