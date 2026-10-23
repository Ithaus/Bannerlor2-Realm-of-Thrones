using System;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// m110-sam (Jeff 23.10 23:00, zrzut King's Landing, druzyna 1: "jak ja nie mam wojska, to co to za raport, ze wojsko cwiczylo"): komunikaty gracza o
    /// "ludziach" (sen, oboz, marsz, karczma, rabunek) mowia do gracza wedlug skladu jego druzyny - sam bohater: "you", sami bohaterowie (towarzysze, bez
    /// wojska): "you and your companions", z wojskiem: "the men" jak dotad. Komunikaty o rzeczach, ktorych bez wojska nie ma (musztra, kiesy i sprzet
    /// zolnierzy, kwatermistrz) - pomijane przez NoTroops. Blad odczytu skladu - stary tekst (jak z wojskiem).
    /// </summary>
    internal static class PartyWords
    {
        private const int Troops = 0, Alone = 1, Heroes = 2;

        /// <summary>0 - jest wojsko, 1 - sam bohater, 2 - sami bohaterowie (bez zolnierzy).</summary>
        internal static int Kind
        {
            get
            {
                try
                {
                    var mp = MobileParty.MainParty;
                    var r = mp != null ? mp.MemberRoster : null;
                    if (r == null || r.TotalRegulars > 0) return Troops;
                    return r.TotalManCount <= 1 ? Alone : Heroes;
                }
                catch { return Troops; }
            }
        }

        /// <summary>Druzyna gracza bez zolnierzy (sam albo z samymi towarzyszami).</summary>
        internal static bool NoTroops { get { return Kind != Troops; } }

        /// <summary>Sam bohater, bez nikogo.</summary>
        internal static bool IsAlone { get { return Kind == Alone; } }

        /// <summary>Podmiot zdania malymi literami: "the men" / "you" / "you and your companions".</summary>
        internal static string Men() { int k = Kind; return k == Troops ? "the men" : k == Alone ? "you" : "you and your companions"; }

        /// <summary>Podmiot na poczatku zdania: "The men" / "You" / "You and your companions".</summary>
        internal static string MenCap() { int k = Kind; return k == Troops ? "The men" : k == Alone ? "You" : "You and your companions"; }

        /// <summary>Wybor tekstu wedlug skladu: z wojskiem, sam, z samymi towarzyszami.</summary>
        internal static string Pick(string troops, string alone, string heroes) { int k = Kind; return k == Troops ? troops : k == Alone ? alone : heroes; }
    }
}
