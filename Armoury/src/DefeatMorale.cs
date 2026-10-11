using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// D11-1 (decyzja Jeffa 11.10 02:50: "zmniejsz kare o polowe") - KARA MORALE ZA PRZEGRANA O POLOWE, jedna regula gracz i AI. Wylacznik DefeatMoraleHalf.
    ///
    /// GRA (dekompilacja TaleWorlds.CampaignSystem 1.4.8) - wszystkie miejsca, gdzie przegrana obniza RecentEventsMorale:
    ///  - MapEvent.ApplyMoraleAndBehaviorUpdatesOfDefeatedParties (koniec bitwy, kazda partia strony pokonanej): RecentEventsMorale +=
    ///    PartyMoraleModel.GetDefeatMoraleChange(party) = -20 (DefaultPartyMoraleModel);
    ///  - MapEventParty.OnRoundEnd (kazda runda symulacji, MapEvent.ApplyRoundEffects - strona z wiekszymi stratami rundy przegrywa runde):
    ///    RecentEventsMorale += BattleRewardModel.CalculateMoraleChangeOnRoundVictory = -3, przy odwrocie strony -1 (DefaultBattleRewardModel);
    ///  - inne zrodla RecentEventsMorale (glod, rekrutacja jencow, oblezenie, kosci okretow 0, wydarzenia, brak zoldu) - nie zwiazane z przegrana, bez zmian.
    /// Lancuch modeli: NavalDLCPartyMoraleModel.GetDefeatMoraleChange i NavalDLCBattleRewardModel.CalculateMoraleChangeOnRoundVictory oddaja wynik
    /// BaseModel; BK, BKROTPatch, RealisticBannerlord, AIInfluence nie nadpisuja tych metod (brak nazwy w metadanych ich DLL - sprawdzone 11.10),
    /// ich modele dziedzicza po Default - latka na Default lapie kazde wywolanie. Wywolan spoza gry nie ma (StoryMode - tylko kampania fabularna).
    /// Mody nie lataja ApplyMoraleAndBehaviorUpdatesOfDefeatedParties ani OnRoundEnd.
    ///
    /// TERAZ: postfiksy (Priority.Last) na DefaultPartyMoraleModel.GetDefeatMoraleChange i DefaultBattleRewardModel.CalculateMoraleChangeOnRoundVictory -
    /// wynik ujemny x 0.5 (kara -20 -> -10, runda -3 -> -1.5, odwrot -1 -> -0.5); wynik >= 0 bez zmian. Prawo dezercji (DesertionLaw) bez zmian:
    /// mniejsza kara = morale pobitej druzyny rzadziej spada pod progi tierow (cel Jeffa: fala dezercji po klesce ok. 1/8-1/6 skladu zamiast 1/4-1/3).
    /// Zlota nie rusza. Linia logu "Kara za przegrana (D11-1)" raz na dobe (liczba kar i suma przed/po).
    /// </summary>
    internal static class DefeatMorale
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.DefeatMoraleHalf; } }
        private const float Factor = 0.5f;

        private static bool _defHooked, _roundHooked, _chainLogged;
        private static int _defN, _roundN, _stumbles;
        private static double _defBefore, _defAfter, _roundBefore, _roundAfter;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset() { _chainLogged = false; _err.Clear(); ClearDay(); }

        private static void ClearDay()
        {
            _defN = _roundN = _stumbles = 0;
            _defBefore = _defAfter = _roundBefore = _roundAfter = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("DefeatMorale." + where, e); } catch { }
        }

        /// <summary>Postfiks DefaultPartyMoraleModel.GetDefeatMoraleChange: kara za przegrana bitwe x 0.5.</summary>
        public static void DefeatPostfix(ref float __result)
        {
            try
            {
                if (!On || __result >= 0f) return;
                float b = __result;
                __result = b * Factor;
                _defN++; _defBefore += b; _defAfter += __result;
            }
            catch (Exception e) { Stumble("DefeatPostfix", e); }
        }

        /// <summary>Postfiks DefaultBattleRewardModel.CalculateMoraleChangeOnRoundVictory: kara za przegrana runde symulacji x 0.5.</summary>
        public static void RoundPostfix(ref float __result)
        {
            try
            {
                if (!On || __result >= 0f) return;
                float b = __result;
                __result = b * Factor;
                _roundN++; _roundBefore += b; _roundAfter += __result;
            }
            catch (Exception e) { Stumble("RoundPostfix", e); }
        }

        // ------------------------------------------------------------ wpiecie (start gry)
        internal static void ApplyAll(Harmony h)
        {
            var miss = new List<string>();
            try
            {
                var m = AccessTools.Method(typeof(DefaultPartyMoraleModel), "GetDefeatMoraleChange", new[] { typeof(PartyBase) });
                if (m != null && m.ReturnType == typeof(float) && m.DeclaringType == typeof(DefaultPartyMoraleModel))
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(DefeatMorale), nameof(DefeatPostfix)) { priority = Priority.Last });
                    _defHooked = true;
                }
                else miss.Add("DefaultPartyMoraleModel.GetDefeatMoraleChange");
            }
            catch (Exception e) { miss.Add("GetDefeatMoraleChange (" + e.Message + ")"); Stumble("ApplyAll(kara bitwy)", e); }
            try
            {
                var m = AccessTools.Method(typeof(DefaultBattleRewardModel), "CalculateMoraleChangeOnRoundVictory", new[] { typeof(PartyBase), typeof(MapEventSide), typeof(BattleSideEnum) });
                if (m != null && m.ReturnType == typeof(float) && m.DeclaringType == typeof(DefaultBattleRewardModel))
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(DefeatMorale), nameof(RoundPostfix)) { priority = Priority.Last });
                    _roundHooked = true;
                }
                else miss.Add("DefaultBattleRewardModel.CalculateMoraleChangeOnRoundVictory");
            }
            catch (Exception e) { miss.Add("CalculateMoraleChangeOnRoundVictory (" + e.Message + ")"); Stumble("ApplyAll(kara rundy)", e); }
            Log.Info("DefeatMorale (D11-1): kara morale za przegrana o polowe - latki: kara za bitwe " + (_defHooked ? "tak" : "NIE") + ", kara za runde " + (_roundHooked ? "tak" : "NIE")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "") + "; wylacznik Defeat Morale Half " + (On ? "WLACZONY" : "wylaczony") + ".");
        }

        /// <summary>Raz na sesje: lancuch czynnych modeli (BaseModel) - czy dochodzi do modelu Default (latka dziala tylko wtedy).</summary>
        private static string Chain(object m, Type def)
        {
            var names = new List<string>();
            bool hit = false;
            for (int i = 0; i < 8 && m != null; i++)
            {
                names.Add(m.GetType().Name);
                if (def.IsInstanceOfType(m)) { hit = true; break; }
                object next = null;
                try { var bp = AccessTools.Property(m.GetType(), "BaseModel"); next = bp != null ? bp.GetValue(m, null) : null; } catch { next = null; }
                if (ReferenceEquals(next, m)) break;
                m = next;
            }
            return (names.Count > 0 ? string.Join(" -> ", names.ToArray()) : "brak") + (hit ? " (dochodzi do " + def.Name + ")" : " (NIE dochodzi do " + def.Name + " - kara jak w grze)");
        }

        // ------------------------------------------------------------ raz na dobe
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                if (s == null || !s.LogEnabled) return;
                var inv = CultureInfo.InvariantCulture;
                if (!_chainLogged)
                {
                    _chainLogged = true;
                    string mor = "-", rew = "-";
                    try { mor = Chain(Campaign.Current.Models.PartyMoraleModel, typeof(DefaultPartyMoraleModel)); } catch (Exception e) { mor = "blad (" + e.Message + ")"; }
                    try { rew = Chain(Campaign.Current.Models.BattleRewardModel, typeof(DefaultBattleRewardModel)); } catch (Exception e) { rew = "blad (" + e.Message + ")"; }
                    Log.Info("Kara za przegrana (D11-1): modele - morale " + mor + "; lup i rundy " + rew + "; latki: bitwa " + (_defHooked ? "tak" : "NIE") + ", runda " + (_roundHooked ? "tak" : "NIE") + ".");
                }
                int day = (int)CampaignTime.Now.ToDays - 1;
                Log.Info("Kara za przegrana (D11-1): dzien " + day + (On ? "" : " | WYLACZONE (Defeat Morale Half) - kara jak w grze")
                         + " | kary za przegrana bitwe: " + _defN + " (suma przed " + _defBefore.ToString("0.0", inv) + ", po " + _defAfter.ToString("0.0", inv) + ")"
                         + " | kary za przegrana runde symulacji: " + _roundN + " (suma przed " + _roundBefore.ToString("0.0", inv) + ", po " + _roundAfter.ToString("0.0", inv) + ")"
                         + " | razem oszczedzone morale " + ((_defAfter - _defBefore) + (_roundAfter - _roundBefore)).ToString("0.0", inv)
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }
    }
}
