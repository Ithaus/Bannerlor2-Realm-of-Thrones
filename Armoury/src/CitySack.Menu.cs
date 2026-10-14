using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// L5 - OKNO GRACZA (projekt lupu miast rozdz. 3.5, 7 ryzyko 7). Klucz SackWindowRealCoin; dziala przy SackTakesRealCoin (L2) - bez L2 kwoty gry sa prawdziwe (RealPayer).
    ///  - Podpowiedzi trzech opcji menu po zdobyciu (Devastate / Pillage / Show Mercy) i podsumowanie po wyborze: postfiks prywatnej metody gry
    ///    GetSiegeAftermathConsequencesText - podmiana TYLKO zmiennych ARMY_GOLD_TEXT i PARTY_GOLD_TEXT zwroconego tekstu (linie gry "Army Gold Gain" /
    ///    "Party Gold Gain" -> prawdziwe kwoty z zywych zbiornikow w chwili otwarcia okna; podsumowanie - kwoty wziete, z wyniku L2). Zadnego przelaczania menu
    ///    (CLAUDE.md 7: nigdy GameMenu.SwitchToMenu z naszego kodu) - przelacza gra, my zmieniamy tylko tekst.
    ///  - D9: pole gry _settlementProsperityCache ustawione na -1 przy otwarciu menu gracza-wodza (prefiks menu_settlement_taken_player_leader_on_init) - linia
    ///    "Prosperity Lost" liczy od tej osady, nie od ostatniej zlupionej gdziekolwiek na swiecie.
    ///  - Okno mieszkancow ("People of this land", PeopleMenu): przez 30 dob po lupie zdanie "Sacked N days ago: the merchants lost X denars, the townsfolk Y."
    ///  - Po audycie integracji (13/14.10): zamek opisany jako zamek ("Castle treasury", "Castle folk's coin"), pieniadz w napisach lupu zawsze "denars" (N0).
    /// Napisy po angielsku. Wyjatek - tekst gry bez zmian (licznik potkniec). Linia logu "Lup miast (L5)" przy kazdej zmianie liczb (bez powtorzen co klatke).
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool WindowOn { get { var s = Settings.Current; return s != null && s.SackWindowRealCoin && s.SackTakesRealCoin; } }

        private const string Dot = "{newline} \u2022 ";
        private static AccessTools.FieldRef<SiegeAftermathCampaignBehavior, Dictionary<MobileParty, float>> _contribRef;
        private static AccessTools.FieldRef<SiegeAftermathCampaignBehavior, float> _cacheRef;
        private static bool _refsTried;
        private static string _l5Last;
        private static readonly HashSet<string> _l5Seen = new HashSet<string>();   // napisy juz w logu tej doby (podpowiedzi trzech opcji liczone przy kazdym odswiezeniu menu)
        private static int _dL5;

        private static void Refs()
        {
            if (_refsTried) return;
            _refsTried = true;
            try { _contribRef = AccessTools.FieldRefAccess<SiegeAftermathCampaignBehavior, Dictionary<MobileParty, float>>("_siegeEventPartyContributions"); }
            catch (Exception e) { _contribRef = null; Stumble("Refs.contrib", e); }
            try { _cacheRef = AccessTools.FieldRefAccess<SiegeAftermathCampaignBehavior, float>("_settlementProsperityCache"); }
            catch (Exception e) { _cacheRef = null; Stumble("Refs.cache", e); }
        }

        /// <summary>D9: przed menu gracza-wodza linia "Prosperity Lost" ma liczyc od tej osady (pole gry nigdy nie wracalo do -1).</summary>
        public static void LeaderMenuPre(SiegeAftermathCampaignBehavior __instance)
        {
            try
            {
                if (!WindowOn || __instance == null) return;
                Refs();
                if (_cacheRef != null) _cacheRef(__instance) = -1f;
            }
            catch (Exception e) { Stumble("LeaderMenuPre", e); }
        }

        private static string ShareWords(double share)
        {
            if (Math.Abs(share - 1.0 / 3.0) < 0.005) return "a third";
            if (Math.Abs(share - 2.0 / 3.0) < 0.005) return "two thirds";
            if (Math.Abs(share - 0.5) < 0.005) return "half";
            if (share >= 0.995) return "all";
            return ((int)Math.Round(share * 100)).ToString(Inv) + " parts in a hundred";
        }

        private static string DaysWords(int d) { return d == 1 ? "one day" : d == 2 ? "two days" : d == 3 ? "three days" : d + " days"; }

        /// <summary>Postfiks GetSiegeAftermathConsequencesText(atakujacy, osada, wybor, isTooltip): prawdziwe kwoty w miejsce linii zlota gry.</summary>
        public static void TextPost(SiegeAftermathCampaignBehavior __instance, MobileParty __0, Settlement __1, SiegeAftermathAction.SiegeAftermath __2, bool __3, TextObject __result)
        {
            try
            {
                if (!WindowOn || !Live || __result == null || __1 == null || __1.Town == null || __0 == null) return;
                Ensure();
                int kind = KindOf(__2);
                string town = __1.Name != null ? __1.Name.ToString() : __1.StringId;
                string text = __3 ? Preview(__instance, __0, __1, kind, town) : Summary(__1, kind, town);
                if (text == null) return;
                __result.SetTextVariable("ARMY_GOLD_TEXT", new TextObject("{=!}" + text));
                __result.SetTextVariable("PARTY_GOLD_TEXT", "");
            }
            catch (Exception e) { Stumble("TextPost", e); }
        }

        private static string Preview(SiegeAftermathCampaignBehavior inst, MobileParty a, Settlement st, int kind, string town)
        {
            Refs();
            var contrib = _contribRef != null && inst != null ? _contribRef(inst) : null;
            var k = Read(st);
            double share = ShareOf(kind);
            long m = Fl(share, k.Kasa), f = 0;
            for (int c = 0; c < 4; c++) f += Fl(share, k.Folk[c]);
            long pool = m + f;
            int men, uMen;
            var recs = Recipients(a, contrib, out men, out uMen);
            // udzial druzyny gracza - ta sama arytmetyka co L2 (wedlug ludzi, reszta zaokraglen do najwiekszej)
            Rec me = null, big = null;
            long split = 0;
            foreach (var r in recs)
            {
                if (r.Undead) continue;
                r.Share = men > 0 ? pool * r.Men / men : 0;
                split += r.Share;
                if (big == null || r.Men > big.Men) big = r;
                if (r.Mp == MobileParty.MainParty) me = r;
            }
            if (big != null) big.Share += pool - split;
            long pShare = me != null ? me.Share : 0;
            long pLead = MenPurse.On ? pShare / 3 : pShare, pMen = pShare - pLead;
            long pCrown = me != null && me.Lead != null ? CrownPart(me.Lead, pLead) : 0;
            var sb = new StringBuilder(800);
            string goodsKey = "";
            Track t;
            if (_track.TryGetValue(st, out t) && t != null && t.Kind != KMercy && Now - t.Day0 < TrackDays)
            {
                int ago = (int)Math.Floor(Now - t.Day0);
                sb.Append(Dot).Append("Little is left here: ").Append(town).Append(" was sacked ").Append(ago <= 0 ? "today" : ago == 1 ? "yesterday" : ago + " days ago").Append('.');
            }
            bool castle = k.Castle;                     // poprawka po audycie (gracz 8): zamek - skarbiec zamku i ludzie podzamcza, nie kupcy i mieszczanie
            if (kind == KMercy)
            {
                sb.Append(Dot).Append(castle ? "No sack: the castle treasury keeps " : "No sack: the merchants keep ").Append(N(k.Kasa));
                if (k.FolkLive) sb.Append(castle ? " and the castle folk " : " and the townsfolk ").Append(N(k.FolkSum));
                sb.Append(castle ? ". Your men get nothing from the castle." : ". Your men get nothing from the town - they will spend their pay here as in any town.");
            }
            else
            {
                sb.Append(Dot).Append("The sack: ").Append(ShareWords(share)).Append(" of all the denars in ").Append(town).Append('.');
                sb.Append(Dot).Append(castle ? "Castle treasury: " : "Merchants' coin: ").Append(N(k.Kasa)).Append(" - the army takes ").Append(N(m)).Append('.');
                if (k.FolkLive) sb.Append(Dot).Append(castle ? "Castle folk's coin: " : "Townsfolk's coin: ").Append(N(k.FolkSum)).Append(" - the army takes ").Append(N(f)).Append('.');
                sb.Append(Dot).Append("Your party (").Append(N(me != null ? me.Men : 0)).Append(" of ").Append(N(men)).Append(" men): ").Append(N(pShare));
                if (MenPurse.On) sb.Append(" - your men ").Append(N(pMen)).Append(", you ").Append(N(pLead));
                else sb.Append(" - all of it yours");
                if (pCrown > 0) sb.Append(" (the crown will claim ").Append(N(pCrown)).Append(" of your part)");
                sb.Append('.');
                sb.Append(Dot).Append(castle ? "Afterwards: the castle treasury keeps " : "Afterwards: the merchants keep ").Append(N(k.Kasa - m));
                if (k.FolkLive) sb.Append(castle ? ", the castle folk " : ", the townsfolk ").Append(N(k.FolkSum - f));
                sb.Append('.');
                if (NoSpendOn && WindowDays > 0) sb.Append(" For ").Append(DaysWords(WindowDays)).Append(" your men will take what they want here and pay for nothing.");
                // poprawka po audycie (gracz 9, drobne2): towar z polek (L7) - ta sama czesc co moneta; liczby z tego samego planu co SackGoods (tylko odczyt)
                long gp, gv, gw;
                if (GoodsOn && GoodsPreview(st, share, out gp, out gv, out gw))
                {
                    goodsKey = "; towar polek " + gp + " szt. za " + gv + " (konie i zwierzeta " + gw + ")";
                    sb.Append(Dot).Append("Goods: ").Append(ShareWords(share)).Append(castle ? " of what lies in the castle's stores - about " : " of what lies on the shelves - about ")
                      .Append(N(gp)).Append(" pieces worth about ").Append(N(gv)).Append(" denars");
                    if (gw > 0) sb.Append(", ").Append(N(gw)).Append(" of them horses and beasts");
                    sb.Append(". Horses go only as far as your army can lead them, food only up to ten days' rations, the rest as much as your men can carry")
                      .Append(kind == KDevastate ? " - what they cannot carry burns." : " - what they cannot carry stays.");
                }
            }
            string key = "okno gracza - " + KName[kind] + " " + st.StringId + ": kupcy " + k.Kasa + " -> armia " + m + "; lud " + k.FolkSum + " -> armia " + f
                         + "; druzyna gracza " + pShare + " (ludzie " + pMen + ", gracz " + pLead + ", korona " + pCrown + "); ludzie " + (me != null ? me.Men : 0) + " z " + men + goodsKey;
            L5Log(key);
            return sb.ToString();
        }

        private static string Summary(Settlement st, int kind, string town)
        {
            var r = Last;
            bool castle = !st.IsTown;
            if (kind == KMercy)
            {
                var k = Read(st);
                L5Log("podsumowanie - laska " + st.StringId + ": kupcy " + k.Kasa + ", lud " + k.FolkSum);
                if (castle) return Dot + "No sack: the castle treasury keeps " + N(k.Kasa) + (k.FolkLive ? " and the castle folk " + N(k.FolkSum) : "") + ".";
                return Dot + "No sack: the merchants keep " + N(k.Kasa) + (k.FolkLive ? " and the townsfolk " + N(k.FolkSum) : "") + ". Your men will spend their pay here as in any town.";
            }
            if (r == null || r.St != st || r.Kind != kind) return null;            // brak wyniku L2 tej osady - tekst gry bez zmian
            L5Log("podsumowanie - " + KName[kind] + " " + st.StringId + ": wziete " + r.Pool + " (kupcy " + r.Merch + ", lud " + r.Folk + "), ludzie gracza " + r.PlayerMen + ", gracz " + r.PlayerLead
                  + ", korona " + r.PlayerCrown);
            return Dot + "Your army took " + N(r.Pool) + " denars from " + town + ": " + N(r.Merch) + (castle ? " from its treasury, " : " from its merchants, ") + N(r.Folk)
                   + (castle ? " from its castle folk. " : " from its townsfolk. ")
                   + PartText(r.PlayerMen, r.PlayerLead, r.PlayerCrown);
        }

        private static void L5Log(string key)
        {
            if (key == _l5Last || _l5Seen.Count > 200 || !_l5Seen.Add(key)) return;
            _l5Last = key;
            _dL5++;
            if (MeasureOn) Log.Info("Lup miast (L5): dzien " + (int)CampaignTime.Now.ToDays + " | " + key + ".");
        }

        /// <summary>PeopleMenu ("People of this land"): zdanie o lupie z ostatnich 30 dob albo null. Tylko odczyt.</summary>
        internal static string MenuLine(Settlement st)
        {
            try
            {
                if (!WindowOn || st == null) return null;
                Ensure();
                Track t;
                if (!_track.TryGetValue(st, out t) || t == null || t.Kind == KMercy || t.Merch + t.FolkTaken <= 0) return null;
                int ago = (int)Math.Floor(Now - t.Day0);
                if (ago > TrackDays) return null;
                bool castle = !st.IsTown;
                return "Sacked " + (ago <= 0 ? "today" : ago == 1 ? "yesterday" : ago + " days ago") + (castle ? ": the castle treasury lost " : ": the merchants lost ") + N(t.Merch)
                       + (castle ? " denars, the castle folk " : " denars, the townsfolk ") + N(t.FolkTaken) + ".";
            }
            catch (Exception e) { Stumble("MenuLine", e); return null; }
        }

        private static void L5Part(StringBuilder sb)
        {
            sb.Append(" | L5: napisy okna gracza ").Append(_dL5);
            if (!WindowOn) sb.Append(" [WYLACZONE Sack Window Real Coin / Sack Takes Real Coin]");
        }

        private static void L5Clear() { _dL5 = 0; _l5Seen.Clear(); }

        private static bool L5Any { get { return _dL5 > 0; } }

        private static void L5Patch()
        {
            var t = typeof(SiegeAftermathCampaignBehavior);
            Seal31.Patch("lup miast L5: napisy okna gracza (GetSiegeAftermathConsequencesText)", AccessTools.Method(t, "GetSiegeAftermathConsequencesText"), typeof(CitySack),
                         null, nameof(TextPost), null, Priority.Normal, Priority.Low);
            Seal31.Patch("lup miast L5: D9 dobrobyt w podpowiedzi (menu_settlement_taken_player_leader_on_init)", AccessTools.Method(t, "menu_settlement_taken_player_leader_on_init"),
                         typeof(CitySack), nameof(LeaderMenuPre), null, null, Priority.Normal);
        }
    }
}
