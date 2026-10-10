using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace Armoury
{
    /// <summary>
    /// W4c (noc 10/11.10) - PRZEPELNIENIE: DO DOMU, NIE DO LASU. Jedno zjawisko ("za duzo ludzi na miejsce"), jedno wyjscie - jak zwolnieni
    /// budzetu rodow 166 (ClanBudget.ReleaseFrom, linia "zwolnieni: do wsi"), a nie jak dezerterzy.
    ///
    /// Gra (DefaultPartyDesertionModel.GetTroopsToDesertDueToWageAndPartySize, dekompilacja 1.4.8): nadwyzka = NumberOfAllMembers - juz
    /// odchodzacy - PartySizeLimit; z niej b = max(1, 25% nadwyzki) dziennie; limit zoldu a (min 20); odchodzi max(a, b), zaloga z zaleglym
    /// zoldem jeszcze + min(zdrowi, 5). Wszystkich zdejmuje DesertionCampaignBehavior i podaje TEN SAM roster do CampaignEvents.OnTroopsDeserted,
    /// a OutlawLaw.OnTroopsDeserted wrzucal kazdego do puli wyrzutkow regionu (bieg 457: zalogi 26-47 ludzi/d w 2. polowie roku - glownie powrot
    /// patroli BK do zalogi bez sprawdzenia miejsca, BKPartyBehavior.AddPatrolBehavior; partie lordow 17/d).
    ///
    /// Tutaj: prefiks + postfiks na tej metodzie (kazdy wolajacy - nasz TierDesertionModel przez refleksje i model gry dla partii poza prawem)
    /// zapamietuja, ktorzy z dopisanych do rosteru odchodzacych to przepelnienie: pierwszych b ludzi z tych, ktorych metoda dopisala (a > b -
    /// reszta to limit zoldu; zalegly zold zalogi - zawsze reszta). Notatka wisi na OBIEKCIE rosteru (ConditionalWeakTable) - podpowiedz
    /// garnizonu w UI (SettlementHelper.GetGarrisonChangeExplainedNumber tez wola GetTroopsToDesert) tworzy roster, ktory nigdy nie trafia do
    /// zdarzenia, i znika z nim. OutlawLaw.OnTroopsDeserted dla tego samego rosteru wysyla tych ludzi do ludnosci BK wsi (ClanBudget.OverflowVillage:
    /// zaloga - wsie jej twierdzy, partia - najblizsza wies rodu, potem krolestwa), reszte do puli jak dotad. Bez wsi z danymi BK - do puli (licznik).
    /// Tylko partie lordow (takze gracz - jedna regula) i zalogi; karawany jak dotad. Inni - do niczego (OutlawLaw, W4b). Sakiewki ludzi bez zmian
    /// (dezerterzy do puli tez nic nie biora; ReleaseFrom daje wsi czesc sakiewki - roznica do decyzji Jeffa). Zloto: zero.
    /// </summary>
    internal static class OverflowHome
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.OverflowGoesHome; } }

        private sealed class Snap { public int Over; public List<KeyValuePair<CharacterObject, int>> Before; }
        private sealed class Note { public readonly List<KeyValuePair<CharacterObject, int>> Stacks = new List<KeyValuePair<CharacterObject, int>>(); public int Men; }
        private static ConditionalWeakTable<TroopRoster, Note> _notes = new ConditionalWeakTable<TroopRoster, Note>();

        // liczniki doby - dopisek do linii "Wyrzutki:" (OutlawLaw.Daily)
        private static int _homeGar, _homePar, _homePlayer, _noVillage, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static Harmony _harmony;
        private static bool _hooksTried, _hooked;

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void Reset() { _notes = new ConditionalWeakTable<TroopRoster, Note>(); NewDay(); _err.Clear(); }

        internal static void NewDay() { _homeGar = _homePar = _homePlayer = _noVillage = _stumbles = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("OverflowHome." + where, e); } catch { }
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (kampania istnieje, jak ClanBudget.EnsureHooks) - raz na proces.</summary>
        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            try
            {
                var m = AccessTools.Method(typeof(DefaultPartyDesertionModel), "GetTroopsToDesertDueToWageAndPartySize", new[] { typeof(MobileParty), typeof(TroopRoster) });
                if (m != null)
                {
                    _harmony.Patch(m, prefix: new HarmonyMethod(typeof(OverflowHome), nameof(OverPre)), postfix: new HarmonyMethod(typeof(OverflowHome), nameof(OverPost)));
                    _hooked = true;
                }
            }
            catch (Exception e) { Log.Error("OverflowHome.EnsureHooks", e); }
            Log.Info("OverflowHome (W4c): przepelnienie partii lordow i zalog - do domu, do ludnosci BK wsi (MCM Overflow Goes Home: " + (On ? "TAK" : "NIE")
                     + "); okno GetTroopsToDesertDueToWageAndPartySize " + (_hooked ? "wpiete" : "BRAK - przepelnienie idzie do puli wyrzutkow jak dotad") + ".");
        }

        /// <summary>Prefiks: nadwyzka ponad limit partii (ten sam wzor co gra, przed dopisaniem) i stan rosteru odchodzacych.</summary>
        public static void OverPre(MobileParty __0, TroopRoster __1, out object __state)
        {
            __state = null;
            try
            {
                if (!On || __0 == null || __1 == null || __0.Party == null) return;
                if (!(__0.IsLordParty || __0.IsGarrison) || Undead.Party(__0)) return;   // Inni nie maja domu (OutlawLaw W4b)
                int over = __0.Party.NumberOfAllMembers - __1.TotalManCount - __0.Party.PartySizeLimit;
                if (over <= 0) return;
                var before = new List<KeyValuePair<CharacterObject, int>>(__1.Count);
                for (int i = 0; i < __1.Count; i++)
                {
                    var e = __1.GetElementCopyAtIndex(i);
                    if (e.Character != null && e.Number > 0) before.Add(new KeyValuePair<CharacterObject, int>(e.Character, e.Number));
                }
                __state = new Snap { Over = Math.Max(1, (int)((float)over * 0.25f)), Before = before };
            }
            catch (Exception e) { __state = null; Stumble("OverPre", e); }
        }

        /// <summary>Postfiks: z dopisanych przez gre - pierwszych b ludzi (przepelnienie) zapamietanych na obiekcie rosteru.</summary>
        public static void OverPost(TroopRoster __1, object __state)
        {
            var snap = __state as Snap;
            if (snap == null || __1 == null) return;
            try
            {
                var note = new Note();
                int left = snap.Over;
                for (int i = 0; i < __1.Count && left > 0; i++)
                {
                    var e = __1.GetElementCopyAtIndex(i);
                    if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                    int was = 0;
                    foreach (var kv in snap.Before) if (kv.Key == e.Character) { was = kv.Value; break; }
                    int add = e.Number - was;
                    if (add <= 0) continue;
                    int take = Math.Min(add, left);
                    note.Stacks.Add(new KeyValuePair<CharacterObject, int>(e.Character, take));
                    note.Men += take; left -= take;
                }
                _notes.Remove(__1);
                if (note.Men > 0) _notes.Add(__1, note);
            }
            catch (Exception e) { Stumble("OverPost", e); }
        }

        /// <summary>Z OutlawLaw.OnTroopsDeserted: ludzie z przepelnienia TEGO rosteru do ludnosci BK wsi. Zwraca, ilu kazdego typu wies przyjela
        /// (null - nikt albo roster bez notatki: limit zoldu, zalegly zold, WarLedger, karawany, Inni). Wight nigdy do ludnosci BK (wybiera OutlawLaw).</summary>
        internal static Dictionary<CharacterObject, int> SendHome(MobileParty party, TroopRoster roster)
        {
            Note note;
            if (roster == null || !_notes.TryGetValue(roster, out note)) return null;
            _notes.Remove(roster);
            if (!On || party == null || note == null || note.Men <= 0) return null;
            Dictionary<CharacterObject, int> sent = null;
            try
            {
                var village = ClanBudget.OverflowVillage(party);
                foreach (var kv in note.Stacks)
                {
                    var ch = kv.Key;
                    if (ch == null) continue;
                    int n = Math.Min(kv.Value, roster.GetTroopCount(ch));
                    if (n <= 0) continue;
                    if (Undead.Character(ch)) continue;          // wight w partii ludzi (ROT occupation="Soldier") - nie do ludnosci BK, niezaleznie od W4b
                    if (village != null && ClanBudget.ToVillagePop(village, ch, n))
                    {
                        if (sent == null) sent = new Dictionary<CharacterObject, int>();
                        int v; sent.TryGetValue(ch, out v); sent[ch] = v + n;
                        if (party.IsGarrison) _homeGar += n; else _homePar += n;
                        if (party.IsMainParty) _homePlayer += n;
                    }
                    else _noVillage += n;
                }
            }
            catch (Exception e) { Stumble("SendHome", e); }
            return sent;
        }

        /// <summary>Dopisek do linii "Wyrzutki:" (liczniki od poprzedniej linii).</summary>
        internal static string Segment()
        {
            return "przepelnienie - do domu " + (_homeGar + _homePar) + " (zalogi " + _homeGar + ", partie " + _homePar + (_homePlayer > 0 ? ", w tym gracz " + _homePlayer : "") + ")"
                   + ", bez wsi z danymi BK (jak dezerterzy) " + _noVillage
                   + (On ? "" : " [Overflow Goes Home wylaczone]") + (_hooked ? "" : " [okno nie wpiete]")
                   + (_stumbles > 0 ? ", potkniecia " + _stumbles : "");
        }
    }
}
