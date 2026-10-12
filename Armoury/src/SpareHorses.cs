using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E6/K2 (212, PLAN 6.13, R11; projekt etapu 6 v2 rozdz. 3 "E6/K2"; krytyka K1-04, KB K2-zla-sciezka) - JEDEN ZAPAS KONI WEDLUG KATEGORII; NADMIAR KONI AI
    /// NA TARG PRZEZ SAKIEWKE LUDZI; GRA NIE HANDLUJE KONMI LORDOW. Klucz AiSellsSpareHorses (+ AiSpareHorseShare).
    /// (0) Zapas: Stables.Reserve (jedna funkcja) - czyta ja kupno Stajni AI (Stables.OnSettlementEntered: kupno do zapasu, tylko kategorie ponizej zapasu)
    ///     i sprzedaz (a). Histereza: sprzedaz tylko ponad 1.5 x zapas kategorii (Keep >= Target), wiec zakup i sprzedaz tej samej kategorii na jednym postoju
    ///     sie nie spotkaja. Kolejnosc postoju w miescie: nasluch Stables (dopisany pozniej - MbEvent wola od ostatnio dopisanego) biegnie PRZED MenPurse.OnEntered:
    ///     Remount (nadmiar ze zbrojowni do taboru, dosadzenie jezdzcow) -> kupno do zapasu -> sprzedaz nadwyzek sprzetu -> sprzedaz koni ponad 1.5 x zapas.
    /// (a) Sprzedaz: MenPurse.SellArmorySurplus, osobna galaz koni (MenPurse.SellSpareHorses) tylko dla partii lordow (zalogi bez zmian - AiSurplusPlan
    ///     dalej bez koni): najgorsze konie kategorii najpierw (tabor z modyfikatorem i wolne konie zbrojowni), kasa miasta placi z nadwyzki ponad zapas kupcow
    ///     (CastlePurse.Spendable -> TownPurse), odbiorca jak dla innych nadwyzek (trzecia lordowi, reszta do sakiewki ludzi). Kon przechodzi z partii na polke
    ///     miasta - przeniesienie w ksiedze "Konie (E6/0)" (stan bez zmiany sumy).
    /// (b) Gra: PartiesBuyHorseCampaignBehavior.OnSettlementEntered (G :60-135) - prefiks ustawia kontekst; zakup koni gry (BuyHorses, :86) pominiety, gdy
    ///     kupuje Stajnia AI (CavalryNeedsMounts i AiBuysMounts); sprzedaz koni gry (:108-134, SellItemsAction.Apply na IsMountable) pominieta, gdy sprzedaje (a)
    ///     (MenPurseEnabled); sprzedaz towaru (:94-105) bez zmian - prefiks SellItemsAction.Apply w kontekscie przepuszcza wszystko poza wierzchowcami.
    ///     Petla sprzedazy koni gry bez zmiany rosteru konczy sie sama (najwyzej 10 obrotow).
    /// Linia: segment "zapas koni wedlug kategorii (E6/K2)" w linii doby "Konie AI (175)" (HorseCensus.Daily); liczniki zerowane w LinePart / DayReset.
    /// </summary>
    internal static class SpareHorses
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.AiSellsSpareHorses; } }
        /// <summary>Sprzedaz (a) czynna - gra nie sprzedaje koni lordow.</summary>
        internal static bool SellOn { get { return On && MenPurse.On; } }
        /// <summary>Kupno Stajni AI czynne - gra nie kupuje koni lordom.</summary>
        private static bool BuyOn { get { var s = Settings.Current; return On && s.CavalryNeedsMounts && s.AiBuysMounts; } }

        private const double PumpDays = 7.0;
        private static readonly Dictionary<string, double[]> _lastBuy = new Dictionary<string, double[]>();   // partia -> doba ostatniego zakupu do zapasu wedlug kategorii
        private static long _vis, _riders, _overLeft, _sold, _gold, _fromArm, _noBuyer, _pump, _gameBuy, _gameSell, _gameSellTries;
        private static readonly long[] _target = new long[3], _have = new long[3], _bought = new long[3], _soldK = new long[3];
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static string _hooks = "nie wpiete";
        [ThreadStatic] private static bool _ctx, _ctxBuy, _ctxSell;

        internal static void Reset()
        {
            _lastBuy.Clear();
            DayReset();
            _stumbles = 0;
            _errSites.Clear();
        }

        internal static void DayReset()
        {
            _vis = _riders = _overLeft = _sold = _gold = _fromArm = _noBuyer = _pump = _gameBuy = _gameSell = _gameSellTries = 0;
            Array.Clear(_target, 0, 3); Array.Clear(_have, 0, 3); Array.Clear(_bought, 0, 3); Array.Clear(_soldK, 0, 3);
            _stumblesDay = 0;
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("SpareHorses." + where, e);
        }

        /// <summary>Stables: kupno do zapasu kategorii k (pamiec pompy i licznik).</summary>
        internal static void NoteBuy(MobileParty mp, int k, int n)
        {
            if (mp == null || k < 0 || k > 2 || n <= 0) return;
            _bought[k] += n;
            var id = mp.StringId;
            if (id == null) return;
            double[] d;
            if (!_lastBuy.TryGetValue(id, out d)) { d = new[] { -1e9, -1e9, -1e9 }; _lastBuy[id] = d; }
            d[k] = CampaignTime.Now.ToDays;
        }

        /// <summary>MenPurse.SellSpareHorses: postoj lorda w miescie po sprzedazy (zapas, wolne po sprzedazy, nadwyzka, ktora zostala; kontrola pompy).</summary>
        internal static void NoteVisit(MobileParty mp, Stables.HorseReserve r, int[] soldK, int gold, int fromArm, bool noBuyer)
        {
            if (r == null || soldK == null) return;
            _vis++; _riders += r.Riders;
            double[] last = null;
            if (mp != null && mp.StringId != null) _lastBuy.TryGetValue(mp.StringId, out last);
            double now = CampaignTime.Now.ToDays;
            for (int k = 0; k < 3; k++)
            {
                int after = r.Have[k] - soldK[k];
                _target[k] += r.Target[k]; _have[k] += after;
                _overLeft += Math.Max(0, after - r.Keep(k));
                _soldK[k] += soldK[k]; _sold += soldK[k];
                if (soldK[k] > 0 && last != null && now - last[k] < PumpDays) _pump += soldK[k];
            }
            _gold += gold; _fromArm += fromArm;
            if (noBuyer) _noBuyer++;
        }

        // ------------------------------------------------------------ (b) gra nie handluje konmi lordow

        public static void GamePrefix(MobileParty __0)
        {
            _ctxBuy = _ctxSell = false;
            _ctx = __0 != null && __0 != MobileParty.MainParty && (BuyOn || SellOn);
        }

        public static Exception GameFinalizer(Exception __exception)
        {
            if (_ctx) { if (_ctxBuy) _gameBuy++; if (_ctxSell) _gameSell++; }
            _ctx = _ctxBuy = _ctxSell = false;
            return __exception;
        }

        public static bool BuyPrefix()
        {
            if (!_ctx || !BuyOn) return true;
            _ctxBuy = true;
            return false;
        }

        public static bool SellPrefix(ItemRosterElement __2)
        {
            try
            {
                if (!_ctx || !SellOn) return true;
                var it = __2.EquipmentElement.Item;
                if (it == null || !it.IsMountable) return true;
                _ctxSell = true; _gameSellTries++;
                return false;
            }
            catch (Exception e) { Stumble("SellPrefix", e); return true; }
        }

        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var t = typeof(PartiesBuyHorseCampaignBehavior);
                var ent = AccessTools.Method(t, "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) });
                var buy = AccessTools.Method(t, "BuyHorses", new[] { typeof(MobileParty), typeof(Town), typeof(float) });
                var sell = AccessTools.Method(typeof(SellItemsAction), "Apply", new[] { typeof(PartyBase), typeof(PartyBase), typeof(ItemRosterElement), typeof(int), typeof(Settlement) });
                if (ent == null) parts.Add("BRAK PartiesBuyHorseCampaignBehavior.OnSettlementEntered - gra handluje konmi lordow");
                else
                {
                    h.Patch(ent, prefix: new HarmonyMethod(typeof(SpareHorses), nameof(GamePrefix)), finalizer: new HarmonyMethod(typeof(SpareHorses), nameof(GameFinalizer)));
                    parts.Add("kontekst postoju gry wpiety");
                    if (buy != null) { h.Patch(buy, prefix: new HarmonyMethod(typeof(SpareHorses), nameof(BuyPrefix))); parts.Add("zakup koni gry wpiety"); }
                    else parts.Add("BRAK BuyHorses - gra kupuje konie lordom");
                    if (sell != null) { h.Patch(sell, prefix: new HarmonyMethod(typeof(SpareHorses), nameof(SellPrefix))); parts.Add("sprzedaz koni gry wpieta"); }
                    else parts.Add("BRAK SellItemsAction.Apply - gra sprzedaje konie lordow");
                }
            }
            catch (Exception e) { parts.Add("blad wpiecia " + e.GetType().Name); Log.Error("SpareHorses.ApplyAll", e); }
            _hooks = string.Join(", ", parts.ToArray());
            Log.Info("SpareHorses (E6/K2, 212): jeden zapas koni wedlug kategorii - " + _hooks + "; klucz AiSellsSpareHorses (teraz " + (On ? "TAK" : "NIE") + ").");
        }

        /// <summary>Segment linii "Konie AI (175)" (HorseCensus.Daily); zeruje liczniki doby i czysci pamiec pompy starsza niz 7 dob.</summary>
        internal static string LinePart()
        {
            var inv = CultureInfo.InvariantCulture;
            string s = " | zapas koni wedlug kategorii (E6/K2): " + (On ? "CZYNNE" : "WYLACZONE")
                       + "; postoje lordow w miescie " + _vis + " - cel " + _target[0] + "/" + _target[1] + "/" + _target[2]
                       + ", ma (wolne po sprzedazy) " + _have[0] + "/" + _have[1] + "/" + _have[2]
                       + ", ponad 1.5 x zapas zostalo " + _overLeft + " przy " + _riders + " konnych ("
                       + (_riders > 0 ? (100.0 * _overLeft / _riders).ToString("0.00", inv) : "0.00") + "%)"
                       + "; sprzedane z nadwyzki " + _sold + " za " + _gold + " (" + _soldK[0] + "/" + _soldK[1] + "/" + _soldK[2] + ", ze zbrojowni " + _fromArm
                       + "; kasa miasta bez nadwyzki " + _noBuyer + " postojow)"
                       + "; kupione do zapasu " + _bought[0] + "/" + _bought[1] + "/" + _bought[2]
                       + "; zakupy/sprzedaze gry pominiete " + _gameBuy + "/" + _gameSell + " postojow (proby sprzedazy " + _gameSellTries + ")"
                       + "; zakup i sprzedaz tej samej kategorii w 7 dobach " + _pump
                       + "; potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ")";
            try
            {
                double now = CampaignTime.Now.ToDays;
                var drop = new List<string>();
                foreach (var kv in _lastBuy) if (now - Math.Max(kv.Value[0], Math.Max(kv.Value[1], kv.Value[2])) >= PumpDays) drop.Add(kv.Key);
                foreach (var k in drop) _lastBuy.Remove(k);
            }
            catch { }
            DayReset();
            return s;
        }
    }
}
