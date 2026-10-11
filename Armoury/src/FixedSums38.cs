using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KROK 3.8 - CENY HISTORYCZNE: STALE KWOTY W SKALI; ZLOTY SMOK = 36-80 ZL (PLAN-DO-KONCA-MODA 3.8, PLAN-CENY-HISTORYCZNE rozdz. 3 i 5,
    /// decyzja Jeffa 09.10 nr 16: zloty smok = 36-80 zl, wariant a). Spis stalych kwot: docs/SPIS-CEN-GRY.md 2.1-2.3 (dopisany 10.10).
    ///
    /// Wynik spisu: kwoty, ktore sa CENA albo PLACA (kapital i koszt karawany, kapital warsztatu, pasmo notabli, okupy, nagrody zadan,
    /// sakiewki turniejow, Bank, cel kasy miasta) sa w skali pensa - zold i dochody gry byly w niej od poczatku - albo naleza do innych
    /// krokow (platnik: 3.1; Bank: 2.12; okupy: 2.13 / Ransom178; statki: 3.3; ROT: 3.7). Poza skala zostaly kwoty w STAREJ monecie, ktore nie
    /// sa cena, tylko KLASA przedmiotu albo miara nauki, porownywane z Value - a ceny historyczne zmienily wartosc uzbrojenia srednio 21 razy
    /// (suma 34 337 341 -> 1 601 561, log "HistoricalPrices: surowce kuzni"):
    ///  - przedzial nagrody turniejowej gry: FightTournamentGame wola DefaultTournamentModel.GetRegularRewardItems(town, 1600, 5000); dotad
    ///    przedzial dzielony przez HistTournamentScale (4) - miecz t5 kosztuje dzis ok. 100 d, wiec w 400-1250 byly prawie same konie i zbroje;
    ///  - przedzial Tournaments XPanded (czynny u Jeffa: EnablePrizeGeneration, prefiks GetTournamentPrize zastepuje losowanie gry):
    ///    ponad 1600 do 2100 + 100 x poziom gracza + 100 x lord - w nowej monecie same plyty t5-t6, barde i rumaki, nigdy bron;
    ///  - nagroda lokalna GrandTourney (400 - LocalPrizeMaxValue) - GrandTourney pyta o Worth przez WorthLink38;
    ///  - pulap zamowien lordow (OrderMaxItemValue 12 000) - w nowej monecie martwy (najdrozsza zbroja ok. 11 400);
    ///  - XP pomocnika przy kuzni (0.08 x Value) - po przeliczeniu ok. 21 razy mniej (XP kowalstwa gracza juz od dawnej wartosci).
    /// Regula - ta sama co progi legend i XP kowalstwa od 04.10 (wpis 42), a przed nimi PriceLaw (8baa95d, cofniety razem z PriceLaw):
    /// kwota nie-cenowa w starej monecie porownywana z wartoscia przedmiotu SPRZED przeliczenia (HistoricalPrices.Orig). Sztuka zostaje przy
    /// cenie historycznej (targ, lup, skup). Zloto: zaden nowy przeplyw (nagroda i zamowienie to przedmiot).
    ///
    /// ZLOTY SMOK nie wystepuje w grze jako moneta ani kwota: ROT nazywa monete gry "Stags" (str_gold, str_clan_finance_current_gold w
    /// ROT_module_strings.xml), smoki sa tylko w dwoch dialogach bez liczb (wand54, lordrecruit4), a "Gold Dragon" to wierzchowiec-smok
    /// (dragon_gold, dragon_gold2). Wiec 1 zl = 1 jelen = 1 pens, a dla kwot z ksiazek 1 smok = GoldDragonPence.
    /// </summary>
    internal static class FixedSums38
    {
        /// <summary>
        /// Zloty smok w pensach (decyzja Jeffa: 36-80). 80 = angielski noble 1344 (6 s 8 d, 3 noble = 1 funt) - wlasna zlota moneta krolestwa,
        /// jak smok Targaryenow; ok. 7.8-9 g zlota, czyli 74-85 zl po naszej cenie sztaby (goldingot 9 500 zl/kg). Floren (36 zl, 3.5 g)
        /// to moneta obca - jego odpowiednikiem bylyby monety Wolnych Miast. Kon Dunka (750 jeleni) zostaje 750 zl; turniej Reki
        /// (40 000 smokow) = 3.2 mln zl - kwot z ksiazek nie wpisujemy 1:1 (raport 09 P-7), smok sluzy do porownania.
        /// </summary>
        internal const int GoldDragonPence = 80;

        internal static bool On { get { var s = Settings.Current; return s != null && s.HistWorthWindowsOldCoin && HistoricalPrices.On; } }

        /// <summary>Przedzial gry 1600-5000 wpiety (transpiler na GetRegularRewardItems) i czynny - wtedy HistoricalPrices.PrizeRangePrefix nie dzieli przez 4.</summary>
        internal static bool GameWindowOldCoin { get { return _gameHooked && On; } }

        private static Harmony _h;
        private static bool _gameHooked, _txpHooked, _txpTried;
        private static int _swaps, _stumbles;
        private static readonly List<string> _done = new List<string>(), _miss = new List<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("FixedSums38." + where, e);
        }

        /// <summary>
        /// Wartosc przedmiotu do kwot nie-cenowych w starej monecie: z wlacznikiem - sprzed cen historycznych (HistoricalPrices.Orig;
        /// konie, sztuki nieprzeliczone i wykute po starcie bez zapisu - Value), bez wlacznika - Value. Publiczna: wola ja IL gry i TXP
        /// po transpilerze oraz GrandTourney (WorthLink38, refleksja).
        /// </summary>
        public static int Worth(ItemObject it)
        {
            if (it == null) return 0;
            try { return On ? HistoricalPrices.Orig(it) : it.Value; }
            catch (Exception e) { Stumble("Worth", e); return it.Value; }
        }

        /// <summary>Kazdy odczyt ItemObject.Value w metodzie -> Worth(ItemObject) (ten sam stos: obiekt -> int).</summary>
        public static IEnumerable<CodeInstruction> ValueToWorth(IEnumerable<CodeInstruction> instructions)
        {
            var getValue = AccessTools.PropertyGetter(typeof(ItemObject), "Value");
            var worth = AccessTools.Method(typeof(FixedSums38), nameof(Worth));
            foreach (var ci in instructions)
            {
                if (getValue != null && worth != null && (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && Equals(ci.operand, getValue))
                {
                    ci.opcode = OpCodes.Call; ci.operand = worth; _swaps++;
                }
                yield return ci;
            }
        }

        private static bool Hook(MethodBase m, string label)
        {
            if (_h == null) return false;
            if (m == null) { _miss.Add(label + " BRAK metody"); return false; }
            int before = _swaps;
            try { _h.Patch(m, transpiler: new HarmonyMethod(typeof(FixedSums38), nameof(ValueToWorth))); }
            catch (Exception e) { _miss.Add(label + " BLAD (" + e.Message + ")"); return false; }
            int n = _swaps - before;
            if (n > 0) { _done.Add(label + " (odczytow wartosci " + n + ")"); return true; }
            _miss.Add(label + " (0 odczytow wartosci)");
            return false;
        }

        /// <summary>Z SubModuleMain (po HistoricalPrices.ApplyAll): przedzial nagrody gry; TXP - gdy jego zestaw juz zaladowany (inaczej przy starcie sesji).</summary>
        internal static void ApplyAll(Harmony h)
        {
            _h = h;
            try
            {
                var tm = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel);
                _gameHooked = Hook(AccessTools.Method(tm, "GetRegularRewardItems"), "nagroda turnieju gry (DefaultTournamentModel.GetRegularRewardItems)");
            }
            catch (Exception e) { Stumble("ApplyAll(gra)", e); }
            HookTxp(false);
            Log.Info("FixedSums38 (3.8): okna wartosci w starej monecie - wpiete: " + (_done.Count > 0 ? string.Join(", ", _done.ToArray()) : "nic")
                     + (_miss.Count > 0 ? "; brak: " + string.Join(", ", _miss.ToArray()) : "")
                     + (_txpHooked || _txpTried ? "" : "; Tournaments XPanded - sprawdze przy starcie sesji") + ".");
        }

        /// <summary>Tournaments XPanded: przedzial nagrody (min 1600, max 2100 + 100 x poziom + 100 x lord) w CachePossibleRegularRewardItems.</summary>
        private static void HookTxp(bool final)
        {
            if (_txpHooked || _txpTried) return;
            try
            {
                var t = AccessTools.TypeByName("TournamentsXPanded.Patches.GetTournamentPrizePatch");
                if (t == null) { if (final) { _txpTried = true; _miss.Add("Tournaments XPanded nie zaladowany"); } return; }
                _txpTried = true;
                _txpHooked = Hook(AccessTools.Method(t, "CachePossibleRegularRewardItems"), "nagroda Tournaments XPanded (GetTournamentPrizePatch.CachePossibleRegularRewardItems)");
            }
            catch (Exception e) { _txpTried = true; Stumble("HookTxp", e); }
        }

        // ------------------------------------------------------------ start sesji: linia spisu (tylko log)
        private sealed class Pool { public int W, A, H, O; public List<int> V = new List<int>(); }

        private static void Add(Pool p, ItemObject it)
        {
            if (it.HorseComponent != null) p.H++;
            else if (it.ArmorComponent != null) p.A++;
            else if (it.WeaponComponent != null) p.W++;
            else p.O++;
            p.V.Add(it.Value);
        }

        private static string Txt(Pool p)
        {
            int n = p.W + p.A + p.H + p.O;
            if (n == 0) return "0 sztuk";
            p.V.Sort();
            return n + " sztuk (bron " + p.W + ", zbroje " + p.A + ", konie " + p.H + (p.O > 0 ? ", inne " + p.O : "") + "; mediana ceny " + p.V[p.V.Count / 2] + " zl)";
        }

        private static string Dragons(long zl)
        {
            return (zl / (double)GoldDragonPence).ToString("0", CultureInfo.InvariantCulture) + " smokow";
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (PO HistoricalPrices.Apply - wartosci sprzed przeliczenia zapisane): TXP drugi raz i linia "Stale kwoty (3.8)".</summary>
        internal static void SessionStart()
        {
            HookTxp(true);
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                var sb = new StringBuilder("Stale kwoty (3.8): okna wartosci w starej monecie ");
                sb.Append(On ? "CZYNNE" : (s.HistWorthWindowsOldCoin ? "NIECZYNNE (ceny historyczne wylaczone)" : "WYLACZONE (MCM)"));
                sb.Append(" - wpiete: ").Append(_done.Count > 0 ? string.Join(", ", _done.ToArray()) : "nic");
                if (_miss.Count > 0) sb.Append("; brak: ").Append(string.Join(", ", _miss.ToArray()));
                if (_stumbles > 0) sb.Append("; potkniecia ").Append(_stumbles);

                // pule nagrod: wedlug wartosci sprzed przeliczenia (regula 3.8) i wedlug dzisiejszej (bez reguly)
                var gOld = new Pool(); var gNew = new Pool(); var gQuart = new Pool();
                var xOld = new Pool(); var xNew = new Pool();
                var oOld = new Pool(); var oNew = new Pool();
                float k = Math.Max(1f, s.HistTournamentScale);
                int qMin = Math.Max(1, (int)(1600 / k)), qMax = Math.Max(qMin + 1, (int)(5000 / k));
                List<ItemObject.ItemTypeEnum> txpTypes = null; int txpMin = 1600, txpMax = -1;
                try
                {
                    var tt = AccessTools.TypeByName("TournamentsXPanded.TXPManager");
                    if (tt != null)
                    {
                        txpTypes = AccessTools.Field(tt, "ValidItemTypes")?.GetValue(null) as List<ItemObject.ItemTypeEnum>;
                        var inst = AccessTools.Property(tt, "Instance")?.GetValue(null, null);
                        var mMin = AccessTools.Method(tt, "GetMinPrizeValue"); var mMax = AccessTools.Method(tt, "GetMaxPrizeValue");
                        if (inst != null && mMin != null) txpMin = (int)mMin.Invoke(inst, new object[] { 0 });
                        if (inst != null && mMax != null && mMax.GetParameters().Length == 3)
                            txpMax = (int)mMax.Invoke(inst, new object[] { 0, false, SettlementComponent.ProsperityLevel.Mid });
                    }
                }
                catch (Exception e) { Stumble("SessionStart(TXP)", e); }
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null || it.Value <= 0 || it.IsCraftedByPlayer) continue;
                        int w = Worth(it), v = it.Value;
                        bool gameKind = !it.NotMerchandise && (it.IsCraftedWeapon || it.IsMountable || it.ArmorComponent != null);
                        if (gameKind)
                        {
                            if (w > 1600 && w < 5000) Add(gOld, it);
                            if (v > 1600 && v < 5000) Add(gNew, it);
                            if (v > qMin && v < qMax) Add(gQuart, it);
                        }
                        if (txpTypes != null && txpMax > 0 && txpTypes.Contains(it.ItemType) && !it.IsTradeGood && !it.IsBannerItem)
                        {
                            if (w > txpMin && w <= txpMax) Add(xOld, it);
                            if (v > txpMin && v <= txpMax) Add(xNew, it);
                        }
                        if (it.HasArmorComponent && !it.NotMerchandise)
                        {
                            int tier = (int)it.Tier + 1;
                            if (tier >= s.OrderMinTier && tier <= s.OrderMaxTier)
                            {
                                if (w <= s.OrderMaxItemValue) Add(oOld, it);
                                if (v <= s.OrderMaxItemValue) Add(oNew, it);
                            }
                        }
                    }
                    catch (Exception e) { Stumble("SessionStart(przedmiot)", e); }
                }
                sb.Append(" | nagroda turnieju gry 1600-5000: wedlug wartosci sprzed przeliczenia ").Append(Txt(gOld))
                  .Append(", wedlug dzisiejszej ").Append(Txt(gNew)).Append(", dawny podzial /").Append(k.ToString("0.#", CultureInfo.InvariantCulture))
                  .Append(" (").Append(qMin).Append("-").Append(qMax).Append(") ").Append(Txt(gQuart));
                if (txpMax > 0)
                    sb.Append(" | Tournaments XPanded ponad ").Append(txpMin).Append(" do ").Append(txpMax).Append(" (bez lordow): sprzed przeliczenia ")
                      .Append(Txt(xOld)).Append(", dzisiejsza ").Append(Txt(xNew));
                else sb.Append(" | Tournaments XPanded: brak (przedzial nieznany)");
                sb.Append(" | zamowienia lordow (zbroje t").Append(s.OrderMinTier).Append("-t").Append(s.OrderMaxTier).Append(", pulap ").Append(s.OrderMaxItemValue)
                  .Append("): sprzed przeliczenia ").Append(Txt(oOld)).Append(", dzisiejsza ").Append(Txt(oNew));
                sb.Append(" | XP pomocnika kuzni: 0.08 x wartosc ").Append(On ? "sprzed przeliczenia" : "dzisiejsza");

                // stale kwoty-ceny (zostaja; spis 2.1-2.3) i zloty smok
                sb.Append(" | zloty smok = ").Append(GoldDragonPence).Append(" zl (1 zl = 1 jelen ROT = 1 pens)");
                try
                {
                    var cm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.CaravanModel : null;
                    if (cm != null)
                    {
                        Hero owner = null;
                        foreach (var hh in Hero.AllAliveHeroes) if (hh != null && hh.IsMerchant && hh != Hero.MainHero) { owner = hh; break; }
                        int c1 = cm.GetInitialTradeGold(owner, false, false), c2 = cm.GetInitialTradeGold(owner, false, true);
                        int c3 = cm.GetInitialTradeGold(owner, true, false), c4 = cm.GetInitialTradeGold(owner, true, true);
                        int f1 = cm.GetCaravanFormingCost(false, false), f2 = cm.GetCaravanFormingCost(true, false);
                        sb.Append(": kapital nowej karawany kupca ").Append(c1).Append("/").Append(c2).Append(" (").Append(Dragons(c1)).Append(" / ").Append(Dragons(c2))
                          .Append("), morskiej ").Append(c3).Append("/").Append(c4).Append(", koszt karawany gracza ").Append(f1).Append("/").Append(f2)
                          .Append(" (model ").Append(cm.GetType().Name).Append(")");
                    }
                }
                catch (Exception e) { Stumble("SessionStart(karawany)", e); }
                sb.Append(", Bank ").Append(s.IronBankCapital).Append(" (").Append(Dragons(s.IronBankCapital)).Append(")")
                  .Append("; z ksiazek: turniej Reki 40 000 smokow = ").Append(40000L * GoldDragonPence).Append(" zl, dlug korony 6 mln smokow = ")
                  .Append(6000000L * GoldDragonPence).Append(" zl (nie wpisujemy 1:1).");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }
    }
}
