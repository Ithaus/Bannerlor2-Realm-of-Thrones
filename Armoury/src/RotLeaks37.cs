using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KROK 3.7 (PLAN-DO-KONCA-MODA-2026-10-09, etap 3 - szczelnosc): zloto z niczego / w nicosc przy podmianie rekruta ROT i przy rebelii.
    ///  - PODMIANA ROT (RotSwapNoGold): ROTTroopRecruiter.ExchangeClanTroops zamienia zolnierza lorda AI (nie rodu gracza - IsHeroManageable) na czlowieka
    ///    rodu; przy innym tierze albo elitarnosci robi GiveGoldAction.ApplyBetweenCharacters(null, pan, koszt_stary - koszt_nowy): dodatnia = zloto
    ///    z niczego, ujemna = w nicosc (ApplyInternal bez dajacego nie przycina kwoty; kiesa nie schodzi ponizej 0). Raz na porcje zamiany, nie razy liczba
    ///    ludzi. Wola to: werbunek u notabla (echo OnTroopRecruited), dzienny tick partii (ExchangeRoster) i podzial lupu.
    ///    DODATNIA (tanszy zamiennik - zwrot roznicy) blokowana: ten sam czlowiek (RotSwapSameMan), zwrotu nie ma od kogo (gra nie zwraca nic za nizszy
    ///    tier). UJEMNA (drozszy zamiennik = w praktyce awans) zostaje jak w grze (w nicosc) - ta sama regula co oplata za awans gry
    ///    (PartyUpgraderCampaignBehavior.ApplyEffects -> GiveGoldAction(pan, null), pozycja "awanse" ksiegi); obie zamknac razem, gdy awanse dostana odbiorce
    ///    (3.1). Poprawka po recenzji: wczesniej blokowane obie strony = awans przez podmiane ROT za darmo, a zwykly awans platny.
    ///    Transpiler podmienia to jedno wywolanie (asercja: dokladnie 1 w metodzie) na SwapGold - licznik; wylacznik wylaczony = wywolanie gry.
    ///  - REBELIA (RebelPurseFromTown): RebellionsCampaignBehavior.CreateRebelPartyAndClan daje przywodcy 50 000 przez Hero.ChangeHeroGold (z niczego,
    ///    bez zdarzenia - siedzi w reszcie ksiegi pieniadza). Rebelie wola gra (miasta, DailyTickSettlement) i BK (zamki, BKSettlementBehavior.CheckRebellion),
    ///    obie przez StartRebellionEvent. Transpiler (asercja: dokladnie 1 ChangeHeroGold w metodzie): ta sama kwota z kasy zbuntowanej osady, najwyzej tyle,
    ///    ile w niej jest (GiveGoldAction osada -> przywodca; brak w kasie - nie dosypujemy).
    /// Platnik -> odbiorca: kasa osady -> przywodca rebelii; zwrot podmiany ROT - bez przeplywu; doplata za drozszego - jak awans gry (pan -> nicosc, 3.1).
    /// Wpiecie raz na proces (OnSessionLaunched - typ ROT juz wczytany).
    /// Wyjatki per wywolanie liczone (potkniecia), pierwszy na miejsce do logu; nic nie gasimy. Stan tylko licznikowy (doba i sesja), zerowany w EnsureHooks.
    /// </summary>
    internal static class RotLeaks37
    {
        private static Harmony _h;
        private static bool _tried, _swapHooked, _rebelHooked;
        private static int _swapCalls, _rebelCalls;                 // ile wywolan znalazl transpiler (asercja: 1)
        private static int _stumbles, _stumblesShown;           // potkniecia (od startu procesu) i ile juz pokazala linia doby
        private static readonly HashSet<string> _errLogged = new HashSet<string>();

        // liczniki doby i sesji: [0] doba, [1] sesja
        private static readonly int[] _fromN = new int[2], _toN = new int[2], _rebelN = new int[2];
        private static readonly long[] _from = new long[2], _to = new long[2], _rebelPaid = new long[2], _rebelShort = new long[2];

        private static bool SwapOn { get { var s = Settings.Current; return s != null && s.RotSwapNoGold; } }
        private static bool RebelOn { get { var s = Settings.Current; return s != null && s.RebelPurseFromTown; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errLogged.Add(where)) Log.Error("RotLeaks37." + where, e); } catch { }
        }

        // ------------------------------------------------------------ podmiana ROT
        /// <summary>Zamiast GiveGoldAction.ApplyBetweenCharacters w ROTTroopRecruiter.ExchangeClanTroops (ta sama sygnatura - stos bez zmian).</summary>
        public static void SwapGold(Hero giver, Hero recipient, int amount, bool disableNotification)
        {
            bool skip = false, countPaid = false;
            int before = 0;
            try
            {
                // tylko przypadek ROT: brak dajacego; bez odbiorcy (zaloga, owner null) gra i tak nic nie robi
                if (SwapOn && giver == null)
                {
                    if (amount > 0)
                    {
                        // zwrot za tanszego zamiennika - z niczego: blokujemy
                        skip = true;
                        if (recipient != null) for (int i = 0; i < 2; i++) { _fromN[i]++; _from[i] += amount; }
                    }
                    else if (amount < 0 && recipient != null)
                    {
                        // doplata za drozszego (awans) - jak w grze, tylko liczymy faktycznie zdjete (kiesa nie schodzi ponizej 0)
                        countPaid = true;
                        before = recipient.Gold;
                    }
                }
            }
            catch (Exception e) { Stumble("SwapGold", e); skip = false; countPaid = false; }
            if (skip) return;
            GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, disableNotification);
            if (!countPaid) return;
            try
            {
                long paid = (long)before - recipient.Gold;
                if (paid > 0) for (int i = 0; i < 2; i++) { _toN[i]++; _to[i] += paid; }
            }
            catch (Exception e) { Stumble("SwapGold.licznik", e); }
        }

        public static IEnumerable<CodeInstruction> SwapTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var target = AccessTools.Method(typeof(GiveGoldAction), nameof(GiveGoldAction.ApplyBetweenCharacters), new[] { typeof(Hero), typeof(Hero), typeof(int), typeof(bool) });
            var ours = AccessTools.Method(typeof(RotLeaks37), nameof(SwapGold));
            int n = 0;
            foreach (var c in list) if (target != null && c.Calls(target)) n++;
            _swapCalls = n;
            if (n != 1 || ours == null) return list;                // nie ten ksztalt metody (inna wersja ROT) - bez zmian
            foreach (var c in list)
                if (c.Calls(target)) { c.opcode = OpCodes.Call; c.operand = ours; }   // etykiety i bloki zostaja na instrukcji
            return list;
        }

        // ------------------------------------------------------------ rebelia
        /// <summary>Zamiast Hero.ChangeHeroGold(50 000) w RebellionsCampaignBehavior.CreateRebelPartyAndClan (stos: bohater, kwota).</summary>
        public static void RebelGold(Hero hero, int amount)
        {
            bool ours = false, noSettlement = false;
            try
            {
                if (RebelOn && hero != null && amount > 0)
                {
                    // partia przywodcy weszla juz do osady (EnterSettlementAction przed ChangeHeroGold); osada domowa rodu rebeliantow - ta sama
                    Settlement st = hero.CurrentSettlement;
                    if (st == null || st.SettlementComponent == null) st = hero.Clan != null ? hero.Clan.HomeSettlement : null;
                    if (st != null && st.SettlementComponent != null)
                    {
                        int take = Math.Max(0, Math.Min(amount, st.SettlementComponent.Gold));
                        ours = true;                                // od tej chwili zadnego zlota gry - takze przy wyjatku nizej (bez podwojnej wyplaty)
                        if (take > 0) GiveGoldAction.ApplyForSettlementToCharacter(st, hero, take, true);
                        for (int i = 0; i < 2; i++) { _rebelN[i]++; _rebelPaid[i] += take; _rebelShort[i] += amount - take; }
                        Log.Info("ROT szczelnosc (3.7): rebelia w " + st.Name + " - przywodca " + hero.Name + " bierze " + take + " z kasy osady (gra dawala "
                                 + amount + " z niczego" + (take < amount ? "; w kasie bylo mniej - brak " + (amount - take) + " nie dosypany" : "") + ").");
                    }
                    else noSettlement = true;
                }
            }
            catch (Exception e) { Stumble("RebelGold", e); }
            if (!ours)
            {
                // wylacznik wylaczony, brak osady (nie powinno sie zdarzyc - liczone jako potkniecie) albo wyjatek przed wyplata - jak w grze
                if (noSettlement) _stumbles++;
                try { if (hero != null) hero.ChangeHeroGold(amount); } catch (Exception e) { Stumble("RebelGold.gra", e); }
            }
        }

        public static IEnumerable<CodeInstruction> RebelTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var target = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeHeroGold), new[] { typeof(int) });
            var ours = AccessTools.Method(typeof(RotLeaks37), nameof(RebelGold));
            int n = 0;
            foreach (var c in list) if (target != null && c.Calls(target)) n++;
            _rebelCalls = n;
            if (n != 1 || ours == null) return list;
            foreach (var c in list)
                if (c.Calls(target)) { c.opcode = OpCodes.Call; c.operand = ours; }
            return list;
        }

        // ------------------------------------------------------------ wpiecie
        /// <summary>Z OnSessionLaunched: raz na proces wpina oba transpilery (typ ROT juz wczytany); zeruje liczniki sesji.</summary>
        internal static void EnsureHooks()
        {
            for (int i = 0; i < 2; i++) { _fromN[i] = _toN[i] = _rebelN[i] = 0; _from[i] = _to[i] = _rebelPaid[i] = _rebelShort[i] = 0; }
            if (_tried) { Log.Info(StatusLine("przy starcie kampanii")); return; }
            _tried = true;
            try { if (_h == null) _h = new Harmony("com.jeff.armoury.rotleaks37"); } catch (Exception e) { Stumble("Harmony", e); return; }
            try
            {
                var t = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTTroopRecruiter");
                var m = t != null ? AccessTools.Method(t, "ExchangeClanTroops", new[] { typeof(Hero), typeof(TaleWorlds.CampaignSystem.Roster.TroopRoster), typeof(CharacterObject), typeof(int), typeof(bool), typeof(Settlement) }) : null;
                if (m != null)
                {
                    _h.Patch(m, transpiler: new HarmonyMethod(typeof(RotLeaks37), nameof(SwapTranspiler)));
                    _swapHooked = _swapCalls == 1;
                }
            }
            catch (Exception e) { Stumble("EnsureHooks(ROT)", e); }
            try
            {
                var m = AccessTools.Method(typeof(RebellionsCampaignBehavior), "CreateRebelPartyAndClan", new[] { typeof(Settlement) });
                if (m != null)
                {
                    _h.Patch(m, transpiler: new HarmonyMethod(typeof(RotLeaks37), nameof(RebelTranspiler)));
                    _rebelHooked = _rebelCalls == 1;
                }
            }
            catch (Exception e) { Stumble("EnsureHooks(rebelia)", e); }
            Log.Info(StatusLine("wpiecie"));
        }

        private static string StatusLine(string when)
        {
            return "ROT szczelnosc (3.7) " + when + ": podmiana rekruta ROT bez zwrotu z niczego (doplata za drozszego jak awans gry) "
                   + (_swapHooked ? "wpieta" : "BRAK (ROTTroopRecruiter.ExchangeClanTroops: wywolan GiveGoldAction " + _swapCalls + ", oczekiwane 1)")
                   + " (wylacznik Rot Swap No Gold " + (SwapOn ? "wl." : "wyl.") + "); 50 000 przywodcy rebelii z kasy osady "
                   + (_rebelHooked ? "wpiete" : "BRAK (CreateRebelPartyAndClan: wywolan ChangeHeroGold " + _rebelCalls + ", oczekiwane 1)")
                   + " (wylacznik Rebel Purse From Town " + (RebelOn ? "wl." : "wyl.") + ").";
        }

        /// <summary>Raz na dobe: linia "ROT szczelnosc (3.7): dzien" - tylko gdy cos sie zdarzylo (podmiany z roznica, rebelia, potkniecia).</summary>
        internal static void Daily()
        {
            try
            {
                if (_fromN[0] + _toN[0] + _rebelN[0] == 0 && _stumbles == _stumblesShown) return;
                _stumblesShown = _stumbles;
                Log.Info("ROT szczelnosc (3.7): dzien | podmiana ROT: zwrotow z niczego zablokowanych " + _fromN[0] + " (" + _from[0] + " zl; ten sam czlowiek, bez przeplywu), doplat za drozszego jak w grze "
                         + _toN[0] + " (" + _to[0] + " zl w nicosc, jak oplata za awans - do zamkniecia razem z awansami, 3.1) | rebelie " + _rebelN[0] + ": z kas osad do przywodcow " + _rebelPaid[0]
                         + " zl, brak w kasie (nie dosypany) " + _rebelShort[0] + " zl | sesja: podmiana - zwroty zablokowane " + _from[1] + " zl w " + _fromN[1] + " porcjach, doplaty jak w grze " + _to[1] + " zl w " + _toN[1]
                         + " porcjach, rebelie " + _rebelN[1] + " (" + _rebelPaid[1] + " zl z kas) | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                _fromN[0] = _toN[0] = _rebelN[0] = 0; _from[0] = _to[0] = _rebelPaid[0] = _rebelShort[0] = 0;
            }
        }
    }
}
