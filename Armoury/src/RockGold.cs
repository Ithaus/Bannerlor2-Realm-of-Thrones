using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 24d ZLOTO CASTERLY ROCK WYCZERPANE (kanon; Jeff 09.10 "OK po naprawie dochodow rodow" - naprawa jest; inwentarz B8; propozycja Z12 audytu 09.10
    /// "zloza Zachodu wyczerpane -> mniej rudy zlota z wsi Zachodu; dla zelaza i reszty bez zmian"). Kanon [H, pewnosc srednia]: kopalnie zlota Casterly
    /// Rock i Zlotego Zeba wyschly na kilka lat przed saga - Tywin zyl z dochodu i pozyczek dla Korony.
    /// W grze: ROT pisze w opisie Casterly Rock (castle_V5), ze "pod Skala jest wielka kopalnia zlota" - kopalnia to jedyna wies gornicza zamku, Rockford
    /// (castle_village_V5_2, typ silver_mine: gra nie ma typu kopalni zlota - ROT oddaje kruszce Skaly kopalnia srebra); ruda zlota (BK gold_ore) bierze
    /// sie tylko z losowej zyly BK w kopalniach srebra (swiat: ok. 5 szt./d).
    /// REGULA (jedna, wariant najprostszy zgodny z zapisem): (1) wsie zamku Casterly Rock nie wydobywaja kruszcow - srebra ani rudy zlota (Rockford: srebro
    /// ok. 7.5 ladunku/d -> 0); (2) zadna wies Zachodu (kultura z kalibracji vlandia = Westerlands, ROT ma osobna "reach") nie wydobywa rudy zlota. Reszta
    /// produkcji tych wsi (drewno lasu wsi, len High Rock, zelazo, sol...) bez zmian. Sufit 0 to OSTATNI krok modelu produkcji (MaterialLaw.ProdPostfix,
    /// przed ksiega rudy) - po mnoznikach, zimie, rekach, regulatorze; dziala w ticku produkcji, w magazynie wsi i w podpowiedziach (wszedzie ten sam model).
    /// Obieg: mniej wydobycia u zrodla (nic nie znika z polek ani kas); regulator swiata (HarvestValve, co 28 dob) moze lekko podniesc srebro innych kopaln,
    /// jesli srebra na swiecie zabraknie (dzis zapas rosnie +62/d - nie zabraknie). Kopalnie BK w budynkach zamku (RunMines) - poza regula (swiat: 27 zl/d).
    /// Wylacznik CasterlyGoldExhausted (off = 449d453). Linia doby "Zloto Casterly Rock (24d)" (tylko log).
    /// </summary>
    internal static class RockGold
    {
        internal const string RockId = "castle_V5";            // Casterly Rock (ROT-Map settlements.xml)
        private const string Westerlands = "vlandia";          // kultura ROT Zachodu (PopulationLaw: "vlandia" = Westerlands, Lannisport)
        private static readonly TextObject Text = new TextObject("{=!}The mines of Casterly Rock are exhausted");

        // ostatni wynik modelu PRZED sufitem (wies -> szt./d) - tylko log; nie sumujemy wywolan (model pytany tez spoza ticku)
        private static readonly Dictionary<string, float> _rockSilver = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> _rockGold = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> _westGold = new Dictionary<string, float>();
        private static int _stumbles;

        internal static void Reset() { _rockSilver.Clear(); _rockGold.Clear(); _westGold.Clear(); _stumbles = 0; }

        private static bool On { get { var s = Settings.Current; return s != null && s.CasterlyGoldExhausted; } }

        /// <summary>KS (kalibracja startu, Plan - recenzja 16.10): sufit 0 tej reguly dla towaru id we wsi v (srebro i ruda zlota wsi Skaly, ruda zlota wsi Zachodu) -
        /// Plan daje tym zlozom L = 0, jak wsi bez zloza. Ten sam warunek co Cap. Nie rzuca.</summary>
        internal static bool Blocks(Village v, string id)
        {
            try
            {
                if (v == null || !On || v.Settlement == null) return false;
                bool silver = id == "silver", gold = id == "gold_ore";
                if (!silver && !gold) return false;
                bool rock = v.Bound != null && v.Bound.StringId == RockId;
                return rock || (gold && PeopleBook.CultureAtCalibration(v.Settlement) == Westerlands);
            }
            catch { return false; }
        }

        /// <summary>Wolane z MaterialLaw.ProdPostfix (zewnetrzne wywolanie modelu, _depth 1) jako ostatni krok przed ksiega rudy: sufit 0 dla kruszcow Skaly
        /// i rudy zlota Zachodu.</summary>
        internal static void Cap(Village village, ItemObject item, ref ExplainedNumber result)
        {
            if (village == null || item == null || !On) return;
            try
            {
                string id = item.StringId;
                bool silver = id == "silver", gold = id == "gold_ore";
                if (!silver && !gold) return;
                var st = village.Settlement;
                if (st == null) return;
                var bound = village.Bound;
                bool rock = bound != null && bound.StringId == RockId;
                bool west = gold && !rock && PeopleBook.CultureAtCalibration(st) == Westerlands;
                if (!rock && !west) return;
                float before = result.ResultNumber;
                if (rock) (silver ? _rockSilver : _rockGold)[st.Name != null ? st.Name.ToString() : st.StringId] = before;
                else _westGold[st.Name != null ? st.Name.ToString() : st.StringId] = before;
                result.LimitMax(0f, Text);
            }
            catch { _stumbles++; }
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            var inv = CultureInfo.InvariantCulture;
            if (!s.CasterlyGoldExhausted) return;
            string owner = "-";
            try
            {
                var rock = Settlement.Find(RockId);
                if (rock != null && rock.OwnerClan != null) owner = rock.OwnerClan.Name.ToString();
            }
            catch { }
            float rs = 0f, rg = 0f, wg = 0f;
            foreach (var v in _rockSilver.Values) rs += v;
            foreach (var v in _rockGold.Values) rg += v;
            foreach (var v in _westGold.Values) wg += v;
            Log.Info("Zloto Casterly Rock (24d): dzien " + day + " | pan Skaly " + owner
                     + " | wsie Skaly - wydobycie modelu przed sufitem (ostatnie pytanie, szt./d) -> 0: srebro " + rs.ToString("0.00", inv)
                     + " (" + string.Join(", ", Names(_rockSilver)) + "), ruda zlota " + rg.ToString("0.00", inv)
                     + " | ruda zlota innych wsi Zachodu " + wg.ToString("0.00", inv) + " w " + _westGold.Count + " wsiach"
                     + " | potkniecia " + _stumbles + ".");
        }

        private static List<string> Names(Dictionary<string, float> d)
        {
            var l = new List<string>();
            foreach (var kv in d) l.Add(kv.Key + " " + kv.Value.ToString("0.00", CultureInfo.InvariantCulture));
            return l;
        }
    }
}
