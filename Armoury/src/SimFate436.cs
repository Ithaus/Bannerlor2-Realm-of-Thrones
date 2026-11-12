using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 436 (Jeff 11.11 o bitwach symulowanych vs rozgrywanych: "tak wyrownaj"): SMIERC CZY RANA W BITWIE SYMULOWANEJ jak w rozgrywanej (421).
    ///
    /// Jak jest (cs_full.cs:114613-114660, MapEventSide.ApplySimulationDamageToSelectedTroop; wolajacy MapEvent.SimulateSingleTroopHit :111705-111733):
    /// szeregowy trafiony w symulacji pada, gdy RandomInt(MaxHitPoints) &lt; obrazenia; wtedy przezycie = PartyHealingModel.GetSurvivalChance(partia,
    /// postac, typ, false, partia bijacego) - TA SAMA metoda (ten sam lancuch BKROT -> BkrotChains -> Default z prefiksem RBM bez warunku "obuch nie
    /// zabija"), z ktorej misja bierze szanse smierci (1 - przezycie) przed postfiksem 421. Typ obrazen w symulacji to los: Blunt z szansa
    /// CombatSimulationModel.GetBluntDamageChance (Default :57331: 0.3 w bitwie gracza, 0.1 w AI-AI; ROT i NavalDLC oddaja dalej), inaczej Cut.
    /// Czesci ciala ani glebokosci ciosu symulacja nie zna. 421 mnozy szanse smierci w misji przez TYP x CZESC x GLEBOKOSC (srednio 0.42-0.48 w
    /// bitwach w polu 11-12.11: "srednia szansa smierci gry 0.36 -> po regule 0.16" itd.), symulacja nie - stad bitwa symulowana ok. 2.2x
    /// smiertelniejsza od rozgrywanej przy tych samych ludziach i chirurgach.
    ///
    /// Regula (tylko szeregowi, tylko symulacja; bohaterowie przez 422 / HeroFate jak dotad):
    ///   smierc = szansa gry x TYP (obuch: BlowDeathBluntFactor 421, ostrze 1.0) x SimulatedBattlesDeathFactor (srednia CZESC x GLEBOKOSC z misji, 0.50)
    /// Przy domyslnych: AI-AI 0.50 x (0.9 + 0.1 x 0.35) = 0.47, bitwa gracza 0.50 x (0.7 + 0.3 x 0.35) = 0.40 szansy gry - jak w misjach 421 (0.42-0.48).
    /// Dziala tylko przy obu kluczach (SimulatedBattlesMatchPlayed i BlowDecidesDeathEnabled 421) - bez 421 misja liczy po staremu, wiec i symulacja.
    ///
    /// Gdzie: transpiler na MapEventSide.ApplySimulationDamageToSelectedTroop (klasa deklarujaca; nikt inny jej nie lata: BK, ROT, RBM, NavalDLC,
    /// BKROT sprawdzone) - callvirt GetSurvivalChance zamieniony na Survival (wola ten sam wirtualny model, potem regula), a call OnTroopKilled /
    /// OnTroopWounded na Killed / Wounded (wolaja oryginal, potem licznik). Lancuch modeli i jego latki (RBM, BKROT, BkrotChains) biegna bez zmian,
    /// regula mnozy gotowa szanse smierci - nic nie jest liczone drugi raz. Rzut losowy gry, skille chirurgii (OnSurgeryApplied) i liczniki strat gry
    /// zostaja; zmienia sie tylko proporcja zabici / ranni (kazdy powalony jest dalej albo zabity, albo ranny - ludzi nie przybywa ani nie ubywa).
    /// Klucz NIE (albo 421 NIE): Survival zwraca wynik modelu co do bitu. Latka w kampanii (NightHooks.InCampaign, raz na proces) - jak 421.
    /// </summary>
    internal static class SimFate436
    {
        private static int _patched, _wanted, _repS, _repK, _repW;
        private static string _note = "nie zakladano";
        private static bool _tried;

        // 1 = szeregowy ciete, 2 = szeregowy obuch; 0 = nic (bohater albo brak rzutu) - ustawiane w Survival, zuzywane w Killed / Wounded
        [ThreadStatic] private static int _pend;

        // doba / sesja: [0] ciete, [1] obuch
        private static readonly int[] _dK = new int[2], _dW = new int[2], _sK = new int[2], _sW = new int[2];
        private static int _dRolls, _sRolls, _dEdge, _dStum, _dOff;
        private static double _dBase, _dNew, _sBase, _sNew;
        private static int _lastDay = -1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>ArmouryBehavior: nowa kampania - liczniki doby i sesji (takze suma misji 421 tej sesji).</summary>
        internal static void Reset()
        {
            _pend = 0;
            Array.Clear(_dK, 0, 2); Array.Clear(_dW, 0, 2); Array.Clear(_sK, 0, 2); Array.Clear(_sW, 0, 2);
            _dRolls = 0; _sRolls = 0; _dEdge = 0; _dStum = 0; _dOff = 0;
            _dBase = 0; _dNew = 0; _sBase = 0; _sNew = 0;
            _lastDay = -1;
            try { BlowFate421.ResetSession(); } catch { }
        }

        /// <summary>NightHooks.InCampaign (raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (_tried) return _note;
            _tried = true;
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 1;
            string miss = "";
            MethodInfo mi = null;
            try { mi = AccessTools.DeclaredMethod(typeof(MapEventSide), "ApplySimulationDamageToSelectedTroop", new[] { typeof(int), typeof(DamageTypes), typeof(PartyBase) }); }
            catch { }
            if (mi == null || mi.ReturnType != typeof(bool)) miss += " brak MapEventSide.ApplySimulationDamageToSelectedTroop;";
            else
            {
                try
                {
                    h.Patch(mi, transpiler: new HarmonyMethod(typeof(SimFate436), nameof(Transpiler)));
                    if (Owned(mi, h.Id) && _repS > 0) _patched++;
                    else miss += " transpiler NIE wszedl (zamian przezycia " + _repS + ");";
                }
                catch (Exception e) { miss += " BLAD " + e.GetType().Name + ";"; Log.Error("SimFate436.Patch", e); }
            }
            var s = Settings.Current;
            _note = "smierc czy rana w symulacji " + _patched + "/" + _wanted + " (zamian: przezycie " + _repS + "/2, zabity " + _repK + "/2, ranny " + _repW + "/2)"
                    + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Smierc czy rana w symulacji (436): latka raz na proces - " + _note + ". Klucz SimulatedBattlesMatchPlayed " + (s != null && s.SimulatedBattlesMatchPlayed ? "TAK" : "NIE")
                     + " (dziala przy 421 BlowDecidesDeathEnabled " + (s != null && s.BlowDecidesDeathEnabled ? "TAK" : "NIE") + "): smierc szeregowego w symulacji = szansa gry x typ (obuch "
                     + F(s != null ? s.BlowDeathBluntFactor : 0f) + ") x " + F(s != null ? s.SimulatedBattlesDeathFactor : 0f) + "; bohaterowie bez zmian.");
            return _note;
        }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Transpilers)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(SimFate436)) return true;
            }
            catch { }
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var mSurv = AccessTools.Method(typeof(SimFate436), nameof(Survival));
            var mKill = AccessTools.Method(typeof(SimFate436), nameof(Killed));
            var mWound = AccessTools.Method(typeof(SimFate436), nameof(Wounded));
            _repS = 0; _repK = 0; _repW = 0;
            foreach (var ci in instructions)
            {
                var m = ci.operand as MethodInfo;
                if (m != null && (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call))
                {
                    if (m.Name == "GetSurvivalChance" && typeof(PartyHealingModel).IsAssignableFrom(m.DeclaringType) && m.GetParameters().Length == 5)
                    { ci.opcode = OpCodes.Call; ci.operand = mSurv; _repS++; }
                    else if (m.DeclaringType == typeof(MapEventSide) && m.Name == "OnTroopKilled" && m.GetParameters().Length == 1)
                    { ci.opcode = OpCodes.Call; ci.operand = mKill; _repK++; }
                    else if (m.DeclaringType == typeof(MapEventSide) && m.Name == "OnTroopWounded" && m.GetParameters().Length == 1)
                    { ci.opcode = OpCodes.Call; ci.operand = mWound; _repW++; }
                }
                yield return ci;
            }
        }

        /// <summary>Zamiast PartyHealingModel.GetSurvivalChance w ApplySimulationDamageToSelectedTroop: ten sam wirtualny model, potem regula 436
        /// (tylko szeregowy). Wynik = szansa PRZEZYCIA.</summary>
        public static float Survival(PartyHealingModel model, PartyBase party, CharacterObject ch, DamageTypes dt, bool canKill, PartyBase striker)
        {
            float s0 = model.GetSurvivalChance(party, ch, dt, canKill, striker);
            _pend = 0;
            if (ch == null || ch.IsHero) return s0;
            int t = dt == DamageTypes.Blunt ? 1 : 0;
            _pend = t + 1;
            float d0 = 1f - s0;
            if (d0 < 0f) d0 = 0f; else if (d0 > 1f) d0 = 1f;
            _dRolls++; _sRolls++; _dBase += d0; _sBase += d0;
            var s = Settings.Current;
            if (s == null || !s.SimulatedBattlesMatchPlayed || !s.BlowDecidesDeathEnabled) { _dOff++; _dNew += d0; _sNew += d0; return s0; }
            if (!(d0 > 0f)) { _dEdge++; return s0; }                                   // gra: pewne przezycie (np. obuch bez RBM) - zostaje
            float f = s.SimulatedBattlesDeathFactor;
            if (t == 1) f *= s.BlowDeathBluntFactor;
            if (!(f >= 0f)) { _dStum++; _dNew += d0; _sNew += d0; return s0; }           // NaN z ustawien - szansa gry zostaje
            if (f > 1f) f = 1f;
            float d = d0 * f;
            _dNew += d; _sNew += d;
            return 1f - d;
        }

        public static void Killed(MapEventSide side, UniqueTroopDescriptor d)
        {
            int p = _pend; _pend = 0;
            side.OnTroopKilled(d);
            if (p > 0) { _dK[p - 1]++; _sK[p - 1]++; }
        }

        public static void Wounded(MapEventSide side, UniqueTroopDescriptor d)
        {
            int p = _pend; _pend = 0;
            side.OnTroopWounded(d);
            if (p > 0) { _dW[p - 1]++; _sW[p - 1]++; }
        }

        private static string F(float v) { return v.ToString("0.00", Inv); }
        private static string F(double v) { return v.ToString("0.00", Inv); }

        private static string Ratio(int k, int w)
        {
            if (k <= 0) return w > 0 ? "sami ranni" : "-";
            return ((double)w / k).ToString("0.0", Inv) + ":1";
        }

        private static string Block(string head, int k, int w, int[] kk, int[] ww, int rolls, double b, double n)
        {
            return head + " powaleni " + (k + w) + ": zabici " + k + ", ranni " + w + ", ranni do zabitych " + Ratio(k, w)
                   + ", zabici/powaleni " + (k + w > 0 ? ((double)k / (k + w)).ToString("0.00", Inv) : "-")
                   + " (ciete " + kk[0] + "/" + ww[0] + ", obuch " + kk[1] + "/" + ww[1] + ")"
                   + " | rzuty " + rolls + ": srednia szansa smierci gry " + (rolls > 0 ? F(b / rolls) : "-") + " -> po regule " + (rolls > 0 ? F(n / rolls) : "-");
        }

        /// <summary>ArmouryBehavior.OnDailyTick: linia "Smierc czy rana w symulacji (436): dzien N" - doba, sesja i dla porownania misje 421 tej sesji.</summary>
        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _lastDay) return;
            _lastDay = day;
            var s = Settings.Current;
            int dk = _dK[0] + _dK[1], dw = _dW[0] + _dW[1], sk = _sK[0] + _sK[1], sw = _sW[0] + _sW[1];
            int mk = BlowFate421.SessKilled, mw = BlowFate421.SessWounded;
            Log.Info("Smierc czy rana w symulacji (436): dzien " + day + " | klucz " + (s != null && s.SimulatedBattlesMatchPlayed ? "TAK" : "NIE")
                     + " (421 " + (s != null && s.BlowDecidesDeathEnabled ? "TAK" : "NIE") + ") wspolczynnik " + F(s != null ? s.SimulatedBattlesDeathFactor : 0f)
                     + " obuch " + F(s != null ? s.BlowDeathBluntFactor : 0f) + " | latka " + _patched + "/" + _wanted
                     + " | SYMULACJA doby (szeregowi)" + Block("", dk, dw, _dK, _dW, _dRolls, _dBase, _dNew)
                     + ", bez reguly (klucz NIE) " + _dOff + ", szansa gry 0 " + _dEdge + (_dStum > 0 ? ", potkniecia " + _dStum : "")
                     + " | SYMULACJA sesji" + Block("", sk, sw, _sK, _sW, _sRolls, _sBase, _sNew)
                     + " | dla porownania MISJE 421 sesji (bitwy w polu i kryjowki, " + BlowFate421.SessMissions + "): powaleni " + (mk + mw) + ", zabici " + mk + ", ranni " + mw
                     + ", ranni do zabitych " + Ratio(mk, mw) + ", zabici/powaleni " + (mk + mw > 0 ? ((double)mk / (mk + mw)).ToString("0.00", Inv) : "-")
                     + ", srednia szansa smierci gry " + (BlowFate421.SessRolls > 0 ? F(BlowFate421.SessBase / BlowFate421.SessRolls) : "-")
                     + " -> po regule " + (BlowFate421.SessRolls > 0 ? F(BlowFate421.SessNew / BlowFate421.SessRolls) : "-") + ".");
            Array.Clear(_dK, 0, 2); Array.Clear(_dW, 0, 2);
            _dRolls = 0; _dEdge = 0; _dStum = 0; _dOff = 0; _dBase = 0; _dNew = 0;
        }
    }
}
