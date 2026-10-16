using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KROK 3.9 PLANU (docs/PLAN-DO-KONCA-MODA-2026-10-09.md) - DROBNE W NICOSC, czesc Armoury (lapowka i rynsztunek gracza w niewoli - RealisticCaptivity
    /// Drobne39.cs). Kazde zjawisko: platnik -> odbiorca, licznik; kazde za wlasnym wylacznikiem (domyslnie wlaczonym, wylaczony = jak dotad).
    ///  (1) TRZECIA LORDA OD SUMY WIZYTY (LordThirdOnTotal; audyt 05.10 wojna/W9). MenPurse.SellArmorySurplus liczyl trzecia od kazdej sztuki osobno:
    ///      Round(cena x 33%) - przy sredniej cenie nadwyzki 1.33 zl/szt. sztuka za 1 zl dawala 0, za 2 zl - 1 (50%). Log dlugiego biegu (458 dob):
    ///      sprzedane 1 835 487 szt. za 2 439 897 zl, trzecia lordow i panow osad 255 468 zl = 10.5% zamiast 33%. Zloto nie znikalo (reszta szla do
    ///      sakiewki ludzi), ale podzial byl falszywy: ok. 2.1 tys. zl/dobe (ostatnie 30 dob) szlo do ludzi zamiast do lorda, a korona (prawo trzecich
    ///      178, 1/3 trzeciej lorda) brala z za malej podstawy. Teraz trzecia = Round(suma wizyty x pct) - oddane juz w tej wizycie: dokladnie pct od
    ///      sumy, blad najwyzej pol monety na wizyte. Platnik bez zmian (kasa miasta), odbiorcy bez zmian (lord albo pan osady, sakiewka ludzi).
    ///  (2) "BANDYCI UKRADLI" W SPOILS (SpoilsBanditsToBand; TOWARY 3 Z4). Zdarzenie Spoils przy zbieraniu lupu zdejmowalo sztuke z lupu do nikad.
    ///      Teraz sztuka trafia do jukow najblizszej czynnej bandy w zasiegu wzroku druzyny gracza (MobileParty.SeeingRange - banda, ktora mogla
    ///      podejsc do obozu; nie nieumarli, nie banda w bitwie, ten sam lad albo morze); bez bandy w zasiegu zdarzenie nie zachodzi (nic nie ginie).
    ///      Dziala w replice zdarzen SpoilsSeal (wylacznik SpoilsNoFreeGold wlaczony); przy wylaczonym SpoilsNoFreeGold Spoils gra po swojemu.
    ///  (3) ZLOMOWANIE ZBROJOWNI WOJSKA GRACZA PRZEZ DTE (DteScrapKeep; AUDYT-PELNY-LOGISTYKA N7). DTE ArmyArmoryBehavior.OnDailyTick co 3 doby
    ///      kasuje najtansze sztuki ponad ScrapCapPerCategory - 1 (600 - 1) na kategorie - w nicosc. Biegnie tylko przy CommandersGreed WYLACZONYM
    ///      (u Jeffa wlaczone - dzis martwe). Prefiks ScrapArmyArmoryByCategory: sztuki zostaja w zbrojowni (wlasnosc wojska), a nadwyzke ponad
    ///      komplet ludzie i tak sprzedaja kupcom przy wjezdzie do miasta (MenPurse.SellPlayerSurplus: kasa miasta -> sakiewka ludzi); konie zostawaly w
    ///      zbrojowni - kupcom ich nikt nie sprzedawal (PlayerSurplusPlan pomijal konie, Stables tylko doklada konie z awansow; recenzja 3.9). Od 15.10
    ///      (Jeff (b), PlayerMenSellSpareHorses) ludzie sprzedaja tez zbedne konie (ponad 1 na jezdzca + SurplusKeepPercent) ta sama droga.
    /// Liczniki tylko do logu: (1) w linii "Sakiewka ludzi", (2) w linii "Spoils of War (128)", (3) linia przy zablokowanym zlomowaniu. Bez zapisu.
    /// </summary>
    internal static class Drobne39
    {
        internal static bool ThirdOn { get { var s = Settings.Current; return s != null && s.LordThirdOnTotal; } }
        internal static bool BanditsOn { get { var s = Settings.Current; return s != null && s.SpoilsBanditsToBand; } }
        internal static bool ScrapKeepOn { get { var s = Settings.Current; return s != null && s.DteScrapKeep; } }

        private static long _thirdOld;                      // (1) ile dalaby dzis trzecia liczona po staremu (od sztuki)
        private static int _toBand, _noBand, _toNothing;    // (2) do bandy / bez bandy w zasiegu (nic nie zginelo) / po staremu w nicosc
        private static bool _scrapWired;
        private static int _stumbles;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        /// <summary>SpoilsSeal.Reset (konstruktor ArmouryBehavior) - stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset() { _thirdOld = 0; _toBand = _noBand = _toNothing = 0; _stumbles = 0; _errWhere.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errWhere.Add(where)) Log.Error("Drobne39." + where, e); } catch { }
        }

        // ------------------------------------------------------------ (1) trzecia lorda od sumy wizyty

        /// <summary>MenPurse.SellArmorySurplus: trzecia za te sztuke. goldBefore - suma cen sztuk sprzedanych juz w tej wizycie, cutSoFar - trzecia juz
        /// oddana w tej wizycie. Wlaczone: Round((goldBefore + unit) x pct) - cutSoFar (w granicach 0..unit); wylaczone - Round(unit x pct) jak dotad.</summary>
        internal static int ThirdCut(int unit, long goldBefore, int cutSoFar, float pct)
        {
            int old = (int)Math.Round(unit * pct / 100f);
            _thirdOld += old;
            if (!ThirdOn) return old;
            long c = (long)Math.Round((goldBefore + (long)unit) * (double)pct / 100.0) - cutSoFar;
            if (c < 0) c = 0;
            if (c > unit) c = unit;
            return (int)c;
        }

        /// <summary>Dopisek do linii "Sakiewka ludzi" (po trzeciej lordow) - zeruje licznik doby.</summary>
        internal static string ThirdNote()
        {
            string r = ThirdOn ? " [3.9: od sumy wizyty; liczona od sztuki - po staremu - bylaby " + _thirdOld + "]"
                               : " [3.9 wylaczone (Lord Third On Total): od sztuki, jak dotad]";
            _thirdOld = 0;
            return r;
        }

        // ------------------------------------------------------------ (2) "bandyci ukradli" w Spoils

        /// <summary>SpoilsSeal.Bandits: kradziez sztuki `ee` z lupu. Wlaczone - sztuka przechodzi do jukow najblizszej bandy w zasiegu wzroku gracza;
        /// bez bandy - false (zdarzenie nie zachodzi, nic nie zdjete). Wylaczone - sztuka schodzi w nicosc jak u Spoils (true).</summary>
        internal static bool Steal(ItemRoster loot, EquipmentElement ee)
        {
            if (!BanditsOn) { loot.AddToCounts(ee, -1); _toNothing++; return true; }
            MobileParty band = null;
            try { band = NearBand(); } catch (Exception e) { Stumble("NearBand", e); band = null; }
            if (band == null) { _noBand++; return false; }
            loot.AddToCounts(ee, -1);
            try { band.ItemRoster.AddToCounts(ee, 1); }
            catch (Exception e) { loot.AddToCounts(ee, 1); Stumble("Steal", e); return false; }   // nieudane dolozenie cofa zdjecie - nic nie ginie
            _toBand++;
            return true;
        }

        /// <summary>Najblizsza czynna banda w zasiegu wzroku druzyny gracza (bez nieumarlych, bez band w bitwie, ten sam lad albo morze).</summary>
        private static MobileParty NearBand()
        {
            var main = MobileParty.MainParty;
            if (main == null) return null;
            var at = main.GetPosition2D;
            float range = Math.Max(0f, main.SeeingRange);
            MobileParty best = null;
            float bestD = float.MaxValue;
            foreach (var p in MobileParty.AllBanditParties)
            {
                if (p == null || !p.IsActive || p.MapEvent != null || p.ItemRoster == null || p.IsCurrentlyAtSea != main.IsCurrentlyAtSea) continue;
                if (Undead.Party(p)) continue;                 // umarli nie kradna lupu
                float d = p.GetPosition2D.Distance(at);
                if (d > range || d >= bestD) continue;
                best = p; bestD = d;
            }
            return best;
        }

        /// <summary>Dopisek do linii "Spoils of War (128)" (po liczbie "bandyci ukradli") - zeruje liczniki doby.</summary>
        internal static string BanditsNote()
        {
            string r = BanditsOn
                ? "(3.9: do jukow bandy w zasiegu wzroku gracza " + _toBand + ", bez bandy w poblizu zdarzenie nie zaszlo " + _noBand + " razy - nic nie zginelo"
                  + (_toNothing > 0 ? "; po staremu w nicosc " + _toNothing : "") + ")"
                : "(wylacznik Spoils Bandits To Band wylaczony - jak u Spoils, w nicosc: " + _toNothing + ")";
            if (_stumbles > 0) r += " [potkniecia 3.9: " + _stumbles + "]";
            _toBand = _noBand = _toNothing = 0;
            return r;
        }

        // ------------------------------------------------------------ (3) zlomowanie zbrojowni wojska gracza przez DTE

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmoryBehavior");
                var m = t != null ? AccessTools.Method(t, "ScrapArmyArmoryByCategory") : null;
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(Drobne39), nameof(ScrapPrefix)));
                    _scrapWired = true;
                }
                Log.Info("Drobne (3.9): zlomowanie zbrojowni wojska gracza przez DTE (ScrapArmyArmoryByCategory) - "
                         + (_scrapWired ? "latka wpieta, wylacznik Dte Scrap Keep " + (ScrapKeepOn ? "wlaczony (sztuki zostaja)" : "WYLACZONY (DTE zlomuje jak dotad)")
                                        : (t == null ? "DTE nieobecny - nie ma czego blokowac" : "BRAK metody w DTE - zlomowanie DTE bez zmian"))
                         + " | trzecia lorda od sumy wizyty: " + (ThirdOn ? "wlaczona" : "WYLACZONA")
                         + " | 'bandyci ukradli' w Spoils do bandy w zasiegu wzroku: " + (BanditsOn ? "wlaczone" : "WYLACZONE") + ".");
            }
            catch (Exception e) { Log.Error("Drobne39.ApplyAll", e); }
        }

        /// <summary>Prefiks DTE ArmyArmoryBehavior.ScrapArmyArmoryByCategory(int target): liczy, co DTE skasowalby (ten sam porzadek - najtansze
        /// pierwsze, kategoria = ItemType) i nie pozwala kasowac. Wylaczone - DTE jak dotad.</summary>
        public static bool ScrapPrefix(int __0)
        {
            if (!ScrapKeepOn) return true;
            int pcs = 0; long val = 0;
            try
            {
                var arm = QuartermasterLaw.DteArmory();
                if (arm != null && __0 > 0)
                {
                    var byType = new Dictionary<ItemObject.ItemTypeEnum, List<KeyValuePair<EquipmentElement, int>>>();
                    for (int i = 0; i < arm.Count; i++)
                    {
                        var e = arm.GetElementCopyAtIndex(i);
                        var it = e.EquipmentElement.Item;
                        if (it == null || e.Amount <= 0) continue;
                        List<KeyValuePair<EquipmentElement, int>> l;
                        if (!byType.TryGetValue(it.ItemType, out l)) { l = new List<KeyValuePair<EquipmentElement, int>>(); byType[it.ItemType] = l; }
                        l.Add(new KeyValuePair<EquipmentElement, int>(e.EquipmentElement, e.Amount));
                    }
                    foreach (var kv in byType)
                    {
                        int total = 0;
                        foreach (var x in kv.Value) total += x.Value;
                        int over = total - __0;
                        if (over <= 0) continue;
                        kv.Value.Sort((a, b) => a.Key.ItemValue.CompareTo(b.Key.ItemValue));
                        foreach (var x in kv.Value)
                        {
                            if (over <= 0) break;
                            int n = Math.Min(over, x.Value);
                            pcs += n; val += (long)x.Key.ItemValue * n; over -= n;
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("ScrapPrefix", e); }
            Log.Info("Zbrojownia wojska gracza (3.9): DTE zlomowalby " + pcs + " szt. (wartosc " + val + " zl) ponad " + __0 + " na kategorie - ZABLOKOWANE: sztuki zostaja"
                     + " w zbrojowni, nadwyzke ponad komplet ludzie sprzedaja kupcom przy wjezdzie do miasta (kasa miasta -> sakiewka ludzi); konie zostaja w zbrojowni"
                     + " (kupcom ich nikt nie sprzedaje).");
            return false;
        }
    }
}
