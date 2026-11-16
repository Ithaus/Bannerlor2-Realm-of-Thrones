using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 586 (16.11, Jeff: "i jak moja karawana? zarabia" -> "3. tak") - DZIENNIK KIESY KARAWANY GRACZA (klucz CaravanPlayerDiary - ten sam co dziennik 305a; tylko log).
    /// Dowod (log Armoury-2026-11-15_23-13-58, Convoy of Amanda): bilans kiesy z linii 259 (kiesa poprzedniej doby + sprzedaz - myto - zakupy) nie zgadza sie
    /// o 10 799 w 4 dobach z handlem: 109232 Pebbleton 9 544 (statek NavalDLC - 584), 109233 Blacktyde 848, 109235 Pebbleton 114, 109239 Lonely Light 293;
    /// dziennik 305a pokazywal tylko handel. Kazdy odplyw i doplyw kiesy karawany rodu gracza zapisany z nazwa (znak wzgledem kiesy):
    ///   sprzedaz / zakupy / myto od sprzedazy - CaravanGoods.TradePostfix (SellItemsAction) przez NoteTrade; gabka BK przy wyjezdzie - cale okno
    ///   BKPartyBehavior.OnSettlementLeft netto po zwrotach 284 / 585 / CaravanBulk / CaravanAmmo (CaravanGoods.SpongePost 307a, kiesa przed - po; wpisy
    ///   pozycji z BookBuy307 bez ruchu - inaczej dwa razy); statek - ShipsToPort181.T_PartyToChar (ChangeShipOwnerAction.ApplyByTrade, takze bez 584);
    ///   oplata BK za wjazd - prefiks / postfiks BKPartyBehavior.AddCaravanFees (BK :784-793: PartyTradeGold -= CaravanFee, do TradeTaxAccumulated miasta;
    ///   CaravanFee = ludzie + 200 x merkantylizm, obca frakcja bez paktu handlowego - EconomicData :138-161); jedzenie na droge - CaravanEscort (288);
    ///   myto Tollgates - RealPayer.TollPost; Salvage - Perk510Gold.NavalEnteredPrefix; najem strazy - CaravanGuards285; doplata kowali 554 - CaravanSilver492;
    ///   wplaty do wlasciciela - NoteDeposit (BK, E3/9, rozwiazanie) i E3-T2 (CaravanGoods.ProfitToOwners); wyplata 259 - PlayerCalc;
    ///   rozliczenie rodu (zold z kiesy / doplata BK do 5 000) - reszta zmiany kiesy w oknie DailyTickClan rodu gracza (TickPre / TickPost) po odjeciu wyplaty 259.
    /// KONTROLA: punkty kontrolne - wjazd do miasta (OnEntered305), wyjazd (OnLeft586), przed i po rozliczeniu rodu, linia doby; w kazdym zmiana kiesy od
    /// poprzedniego punktu minus suma zapisanych ruchow = "nieznane X" (oczekiwane 0) - wpis w transakcjach dnia 305a z kiesa i odcinkiem (od - do) oraz suma
    /// w linii 259. Kolejnosc nasluchow nie ma znaczenia: kazdy ruch miedzy punktami jest albo zapisany, albo w "nieznane".
    /// Linia 259: "kiesa (586) dzis: ..." i "od doby N: ..." - sumy wedlug rodzaju od zakupu (zapis arm_259_majatek, wiersz "l586"; stary zapis - od wczytania).
    /// Bez zmian zachowania (tylko odczyt kiesy i liczniki). Wyjatek per wywolanie - licznik potkniec (linia 305a).
    /// </summary>
    internal static partial class CaravanSettle
    {
        // rodzaje ruchow kiesy (kolejnosc = kolejnosc w linii; kody zapisu nie moga sie zmieniac)
        internal const int LedSale = 0, LedToll = 1, LedBuy = 2, LedSponge = 3, LedShip = 4, LedBkFee = 5, LedFood = 6, LedTollgates = 7, LedSalvage = 8,
                           LedGuards = 9, LedBonus = 10, LedDep = 11, LedPay259 = 12, LedClanOut = 13, LedClanIn = 14, LedN = 15;
        private static readonly string[] LedName = { "sprzedaz", "myto od sprzedazy", "zakupy", "gabka BK netto", "statki", "oplata BK za wjazd", "jedzenie na droge",
                                                     "myto Tollgates", "Salvage", "najem strazy", "doplata kowali 554", "wplaty do wlasciciela", "wyplata 259",
                                                     "zold z kiesy (rozliczenie rodu)", "doplata do kiesy (rozliczenie rodu)" };
        private static readonly string[] LedCode = { "sale", "toll", "buy", "sponge", "ship", "bkfee", "food", "tollg", "salv", "guard", "bonus", "dep", "pay259", "clanout", "clanin" };

        private static bool _w586Fee;
        private static string _w586Miss = "-";
        private static int _s586FeeN, _s586FeePl, _s586St, _s586StSess;   // doba (linia 305a): wywolania AddCaravanFees, z oplata karawany gracza; potkniecia
        private static long _s586FeeG;

        private static void Stumble586(string where, Exception e)
        {
            lock (_lk) { _s586St++; _s586StSess++; }
            Stumble(where, e);
        }

        private static string Sg(long v) { return (v > 0 ? "+" : "") + v.ToString(CultureInfo.InvariantCulture); }

        /// <summary>Z EnsureHooks (kampania, raz na proces): prefiks / postfiks BKPartyBehavior.AddCaravanFees (DeclaredMethod na klasie deklarujacej).</summary>
        private static void EnsureHooks586(List<string> miss)
        {
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var m = t != null ? AccessTools.DeclaredMethod(t, "AddCaravanFees") : null;
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && ps.Length == 3 && ps[0].ParameterType == typeof(MobileParty) && ps[1].ParameterType == typeof(Settlement))
                {
                    _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CaravanSettle), nameof(FeePre586)), postfix: new HarmonyMethod(typeof(CaravanSettle), nameof(FeePost586)));
                    _w586Fee = true;
                }
                else { _w586Miss = "BK AddCaravanFees"; miss.Add("BK AddCaravanFees (586)"); }
            }
            catch (Exception e) { _w586Miss = "BK AddCaravanFees (blad)"; miss.Add("BK AddCaravanFees (586, blad: " + e.Message + ")"); Stumble586("EnsureHooks586", e); }
        }

        public static void FeePre586(MobileParty __0, out int __state)
        {
            __state = int.MinValue;
            try { if (__0 != null && __0.IsCaravan) __state = __0.PartyTradeGold; } catch { __state = int.MinValue; }
        }

        public static void FeePost586(MobileParty __0, int __state)
        {
            if (__state == int.MinValue || __0 == null) return;
            try
            {
                long fee = (long)__state - __0.PartyTradeGold;
                lock (_lk) _s586FeeN++;
                if (fee == 0) return;
                if (__0.ActualClan == Clan.PlayerClan) lock (_lk) { _s586FeePl++; _s586FeeG += fee; }
                Flow586(__0, LedBkFee, -fee);
            }
            catch (Exception e) { Stumble586("FeePost586", e); }
        }

        /// <summary>Ruch kiesy karawany (amt ze znakiem wzgledem kiesy). Wolaja miejsca, ktore zmieniaja kiese karawan - liczy tylko karawane rodu gracza.</summary>
        internal static void Flow586(MobileParty mp, int kind, long amt)
        {
            if (amt == 0 || mp == null) return;
            try
            {
                if (!mp.IsCaravan || mp.ActualClan != Clan.PlayerClan || !Live || !DiaryOn || Measure31.IsDteConvoy(mp)) return;
                Rec586(Car(mp, true), kind, amt);
            }
            catch (Exception e) { Stumble586("Flow586", e); }
        }

        private static void Rec586(CarRec r, int kind, long amt)
        {
            if (r == null || amt == 0 || kind < 0 || kind >= LedN || !DiaryOn) return;
            if (r.LedDay < 0) r.LedDay = Today;
            r.LedAll[kind] += amt; r.LedDayV[kind] += amt; r.RecSum += amt;
        }

        /// <summary>Punkt kontrolny: zmiana kiesy od poprzedniego punktu minus zapisane ruchy = nieznane (wpis w transakcjach dnia 305a).</summary>
        private static void Check586(MobileParty mp, CarRec r, string where)
        {
            if (r == null || mp == null) return;
            if (!DiaryOn) { r.CkPurse = int.MinValue; return; }
            int purse = mp.PartyTradeGold;
            if (r.CkPurse != int.MinValue)
            {
                long rec = r.RecSum - r.CkRec;
                long unk = (long)purse - r.CkPurse - rec;
                if (unk != 0)
                {
                    r.Unk += unk; r.DUnk += unk; r.UnkN++;
                    string txt = "NIEZNANE " + Sg(unk) + " (586: kiesa " + r.CkPurse + " -> " + purse + ", zapisane ruchy " + Sg(rec) + "; od: " + r.CkWhere + ", do: " + where + ")";
                    if (r.DTx.Count < TxMax) r.DTx.Add(txt); else r.DTxDrop++;
                }
            }
            r.CkPurse = purse; r.CkRec = r.RecSum; r.CkWhere = where + " (doba " + Today + ")";
        }

        /// <summary>SettlementLeft (nasluch, tylko karawana rodu gracza i miasto): punkt kontrolny wyjazdu.</summary>
        internal static void OnLeft586(MobileParty mp, Settlement st)
        {
            try
            {
                if (mp == null || st == null || !st.IsTown || !mp.IsCaravan || !Live || !DiaryOn) return;
                if (mp.ActualClan != Clan.PlayerClan || Measure31.IsDteConvoy(mp)) return;
                Check586(mp, Car(mp, true), "wyjazd z " + Nm(st.Name, st.StringId));
            }
            catch (Exception e) { Stumble586("OnLeft586", e); }
        }

        private static void LedList(StringBuilder sb, long[] v, long unk)
        {
            int n = 0;
            for (int i = 0; i < LedN; i++)
            {
                if (v[i] == 0) continue;
                sb.Append(n++ > 0 ? ", " : "").Append(LedName[i]).Append(' ').Append(Sg(v[i]));
            }
            sb.Append(n > 0 ? ", " : "").Append("nieznane ").Append(Sg(unk));
        }

        /// <summary>PlayerDaily (po DiaryLine): ruchy kiesy dnia i od doby LedDay; zeruje licznik dnia.</summary>
        private static void LedLine586(StringBuilder sb, CarRec r)
        {
            if (!DiaryOn) return;
            long net = r.DUnk; for (int i = 0; i < LedN; i++) net += r.LedDayV[i];
            sb.Append(" | kiesa (586) dzis: ");
            LedList(sb, r.LedDayV, r.DUnk);
            sb.Append(" (razem ").Append(Sg(net)).Append(") | kiesa (586) od doby ").Append(r.LedDay >= 0 ? r.LedDay : Today).Append(": ");
            LedList(sb, r.LedAll, r.Unk);
            sb.Append(" (wpisow nieznanych ").Append(r.UnkN).Append(')');
            Array.Clear(r.LedDayV, 0, LedN); r.DUnk = 0;
        }

        /// <summary>Linia zakonczonej karawany: sumy ruchow kiesy.</summary>
        private static string LedEnd586(CarRec r)
        {
            try
            {
                if (!DiaryOn) return "";
                var sb = new StringBuilder(300);
                sb.Append(" | kiesa (586) od doby ").Append(r.LedDay >= 0 ? r.LedDay : Today).Append(": ");
                LedList(sb, r.LedAll, r.Unk);
                return sb.ToString();
            }
            catch (Exception e) { Stumble586("LedEnd586", e); return ""; }
        }

        /// <summary>Wiersz zapisu "l586": doba|kod=wartosc,...|unk=..|unkn=..</summary>
        private static string LedExport(CarRec r)
        {
            var sb = new StringBuilder(160);
            sb.Append("day=").Append(r.LedDay.ToString(Inv));
            for (int i = 0; i < LedN; i++) if (r.LedAll[i] != 0) sb.Append(',').Append(LedCode[i]).Append('=').Append(r.LedAll[i].ToString(Inv));
            sb.Append(",unk=").Append(r.Unk.ToString(Inv)).Append(",unkn=").Append(r.UnkN.ToString(Inv));
            return sb.ToString();
        }

        private static void LedImport(CarRec r, string s)
        {
            foreach (var kv in s.Split(','))
            {
                int e = kv.IndexOf('=');
                if (e <= 0) continue;
                string k = kv.Substring(0, e);
                long v;
                if (!long.TryParse(kv.Substring(e + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) continue;
                if (k == "day") r.LedDay = (int)v;
                else if (k == "unk") r.Unk = v;
                else if (k == "unkn") r.UnkN = (int)v;
                else { int i = Array.IndexOf(LedCode, k); if (i >= 0) r.LedAll[i] = v; }
            }
        }

        /// <summary>Linia 305a (Daily305): stan latki oplaty BK i liczniki doby 586; zeruje liczniki.</summary>
        private static string Daily586()
        {
            lock (_lk)
            {
                string t = " | dziennik kiesy (586): latka BK AddCaravanFees " + (_w586Fee ? "wpieta" : "BRAK (" + _w586Miss + ")") + ", wywolan " + _s586FeeN
                           + ", oplat karawan gracza " + _s586FeePl + " (" + _s586FeeG + " zl), potkniecia " + _s586St + " (sesja " + _s586StSess + ")";
                _s586FeeN = _s586FeePl = _s586St = 0; _s586FeeG = 0;
                return t;
            }
        }
    }
}
