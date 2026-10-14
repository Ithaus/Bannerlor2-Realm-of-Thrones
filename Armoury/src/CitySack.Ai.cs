using System;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// L4 - AI LUPI Z POTRZEBY (projekt lupu miast rozdz. 3.4, decyzja Jeffa 13.10 pkt 4). Klucz AiSacksFromNeed.
    /// Prefiks SiegeAftermathCampaignBehavior.DetermineAISiegeAftermath - kolejnosc dopisana do wyboru gry, granice gry zostaja (Rule34, CitySack.cs):
    ///  1. osada tej samej kultury co krolestwo zdobywcy -> laska (jak gra);
    ///  2. wodz zabity albo ranny w bitwie -> wybor gry (DetermineSiegeAftermathOnEncounterLeaderDeath);
    ///  3. "nie ma czego brac": lup z grabiezy wedlug reguly L2 mniejszy niz jedna doba zoldu armii -> laska (gdy laska kosztuje wplywy za okrutnych lordow armii,
    ///     a rod ich nie ma - wybor gry);
    ///  4. "armia w potrzebie": ktoras druzyna armii ma zalegly zold, ktorego jej rod nie pokryje (ta sama regula co WarLedger), albo kiesa RODU wodza (placi zold) nie
    ///     starcza na 14 dob zoldu druzyn rodu (po recenzji; dotad kiesa samego wodza) -> grabiez; wodz z ujemna cecha Mercy, gdy gra pozwala na spustoszenie (wplywy > 2 x koszt, morale < 90) -> spustoszenie;
    ///  5. reszta -> wybor gry (cecha Mercy, wplywy, morale - losowanie gry).
    /// Wyjatek - wybor gry (licznik potkniec). Liczniki doby w linii "Lup miast" (czesc L4); powod kazdego wyboru w linii L0 ("regula 3.4").
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool AiNeedOn { get { var s = Settings.Current; return s != null && s.AiSacksFromNeed; } }

        private static int _dAiCulture, _dAiEmpty, _dAiNeedP, _dAiNeedD, _dAiGame;
        private static readonly int[] _dAiGameKind = new int[3];
        private static bool _aiGame;

        public static bool AiPre(SiegeAftermathCampaignBehavior __instance, MobileParty __0, Settlement __1, ref SiegeAftermathAction.SiegeAftermath __result)
        {
            _aiGame = false;
            try
            {
                if (!AiNeedOn || !Live || __0 == null || __1 == null || __1.Town == null) return true;
                string why;
                int r = Rule34(__instance, __0, __1, Read(__1), NeedOf(__0), out why);
                if (r < 0) { _aiGame = true; _dAiGame++; return true; }
                __result = r == KMercy ? SiegeAftermathAction.SiegeAftermath.ShowMercy : r == KPillage ? SiegeAftermathAction.SiegeAftermath.Pillage : SiegeAftermathAction.SiegeAftermath.Devastate;
                if (why == "ta sama kultura") _dAiCulture++;
                else if (r == KMercy) _dAiEmpty++;
                else if (r == KPillage) _dAiNeedP++;
                else _dAiNeedD++;
                return false;
            }
            catch (Exception e) { Stumble("AiPre", e); _aiGame = false; return true; }
        }

        public static void AiPost(SiegeAftermathAction.SiegeAftermath __result)
        {
            try { if (_aiGame) _dAiGameKind[KindOf(__result)]++; }
            catch { }
            finally { _aiGame = false; }
        }

        private static void L4Part(StringBuilder sb)
        {
            sb.Append(" | L4: ");
            if (!AiNeedOn) sb.Append("WYLACZONE (AI Sacks From Need) - ");
            sb.Append("decyzje AI: ta sama kultura - laska ").Append(_dAiCulture).Append(", nie ma czego brac - laska ").Append(_dAiEmpty)
              .Append(", potrzeba - grabiez ").Append(_dAiNeedP).Append(", potrzeba - spustoszenie ").Append(_dAiNeedD)
              .Append(", wybor gry ").Append(_dAiGame).Append(" (laska ").Append(_dAiGameKind[KMercy]).Append(", grabiez ").Append(_dAiGameKind[KPillage])
              .Append(", spustoszenie ").Append(_dAiGameKind[KDevastate]).Append(')');
        }

        private static void L4Clear() { _dAiCulture = _dAiEmpty = _dAiNeedP = _dAiNeedD = _dAiGame = 0; Array.Clear(_dAiGameKind, 0, 3); _aiGame = false; }

        private static bool L4Any { get { return _dAiCulture + _dAiEmpty + _dAiNeedP + _dAiNeedD + _dAiGame > 0; } }

        private static void L4Patch()
        {
            Seal31.Patch("lup miast L4: AI lupi z potrzeby (DetermineAISiegeAftermath)", AccessTools.Method(typeof(SiegeAftermathCampaignBehavior), "DetermineAISiegeAftermath"),
                         typeof(CitySack), nameof(AiPre), nameof(AiPost), null, Priority.Normal, Priority.Normal);
        }
    }
}
