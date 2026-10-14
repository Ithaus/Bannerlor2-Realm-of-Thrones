using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Armoury
{
    /// <summary>
    /// A14 / G14 (audyt integracji 14.10, obieg Z2; drobne2) - SKARBIEC KROLESTWA, KTORE UPADA. Klucz FallenCrownTreasury (wylaczony = stan 449d453: skarbiec
    /// wypada ze swiata z krolestwem - linia "w cieniu" mowi, ile i komu by poszlo).
    ///
    /// DOWOD: gra konczy krolestwo bez osad w tym samym zdarzeniu zmiany pana ostatniej twierdzy (FactionDiscontinuationCampaignBehavior.OnSettlementOwnerChanged ->
    /// DiscontinueKingdom: rody wychodza, DestroyKingdomAction) albo Diplomacy przy pokoju (SoftlyDestroyKingdomAction); KingdomBudgetWallet zostaje na martwym
    /// krolestwie, ksiega liczy go jako ujscie ("skarbce upadlych krolestw", CirculationWindows.OnKingdomDestroyed), dlug Banku przepada (CrownBorrow, "Bank stracil").
    /// Bieg dwuletni W50: House Targaryen, Aegon d109294 (pokoj z Volantis) -682 021, Stormlands d109371 -575 987, dlug 90 826; razem 7 upadkow, 1.27 mln zl
    /// i 107 929 dlugu. Nasluchy gry biegna od OSTATNIO dopisanego (MbEvent), wiec przy szturmie ostatniej twierdzy-stolicy nasz L5b-1 (CapitalTreasury) bierze
    /// 1/3 nadwyzki, a zapas wojny i reszta i tak przepadaja chwile pozniej.
    ///
    /// REGULA (nasluch KingdomDestroyedEvent dopisany PO ksiedze obiegu - biegnie PRZED nia, ksiega widzi skarbiec juz pusty):
    ///  1. dlug korony w Banku pierwszy (CrownBorrow.RepayOnFall: skarbiec -> kapital Banku, do wysokosci skarbca);
    ///  2. reszta - komu: (a) krolestwo upada w oknie szturmu swojej twierdzy (CapitalTreasury.SiegeWinnerOf) - do zdobywcy: skarbiec jego krolestwa (migawka 165
    ///     przesunieta jak L5b-1), zdobywca bez krolestwa - kiesa glowy jego rodu; Inni - nie (trup monety nie bierze, idzie (b)); (b) inaczej (pokoj Diplomacy,
    ///     smierc wladcy, bunt, upadek bez szturmu) - do kiesy glowy rodu panujacego (rod wychodzi z krolestwa zywy - dwor wywozi skarbiec); (c) brak zywego
    ///     odbiorcy - do kapitalu Banku (nic nie znika).
    /// OBIEG: skarbiec -> Bank / skarbiec / kiesa w jednej chwili, zdjete = dopisane (kontrola w linii); "Pieniadz swiata" bez zmiany (odbiorcy sa w swiecie, skarbiec
    /// wypada pusty). Linia "Skarbiec upadlego krolestwa (G14)" przy kazdym upadku z niepustym skarbcem. Stan: pamiec rodow panujacych (pamiec sesji, bez zapisu).
    /// Wyjatek per zdarzenie - licznik potkniec w linii, bez globalnego wylacznika.
    /// </summary>
    internal static class FallenCrown
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Dictionary<string, Clan> _ruler = new Dictionary<string, Clan>(StringComparer.Ordinal);   // id krolestwa -> rod panujacy w chwili wyjscia
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.FallenCrownTreasury; } }

        internal static void Reset() { _ruler.Clear(); _stumbles = 0; _err.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("FallenCrown." + where, e); } catch { }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Nasluch OnClanChangedKingdomEvent: rod panujacy wychodzi z krolestwa (gra przy DiscontinueKingdom zeruje RulingClan dopiero PO wyjsciu rodow) -
        /// zapamietany jako dwor krolestwa. Tylko pamiec.</summary>
        internal static void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool notify)
        {
            try
            {
                if (clan != null && oldKingdom != null && oldKingdom.StringId != null && ReferenceEquals(oldKingdom.RulingClan, clan)) _ruler[oldKingdom.StringId] = clan;
            }
            catch (Exception e) { Stumble("OnClanChangedKingdom", e); }
        }

        private static Hero LiveHead(Clan c)
        {
            try { return c != null && !c.IsEliminated && c.Leader != null && c.Leader.IsAlive && !CirculationWindows.OutOfWorld(c.Leader) ? c.Leader : null; }
            catch { return null; }
        }

        /// <summary>Nasluch KingdomDestroyedEvent (dopisany po CirculationWindows.OnKingdomDestroyed - biegnie przed nim). Nie rzuca.</summary>
        internal static void OnKingdomDestroyed(Kingdom k)
        {
            try
            {
                if (k == null) return;
                long w0 = k.KingdomBudgetWallet;
                if (w0 <= 0) return;
                int day = (int)CampaignTime.Now.ToDays;
                bool on = On;
                Clan royal = null;
                try { royal = k.RulingClan; } catch { royal = null; }
                Clan mem;
                if ((royal == null || royal.IsEliminated) && k.StringId != null && _ruler.TryGetValue(k.StringId, out mem)) royal = mem;
                IFaction wf = CapitalTreasury.SiegeWinnerOf(k);
                bool undead = false;
                try { var wc = wf as Clan; undead = wc != null && ClanIncomeBook.IsUndeadClan(wc); } catch { undead = false; }
                var wk = wf as Kingdom;
                if (wk != null && (wk.IsEliminated || ReferenceEquals(wk, k))) wk = null;
                Hero wHead = wf != null && wk == null && !undead ? LiveHead(wf as Clan) : null;
                Hero rHead = LiveHead(royal);
                double debt = CrownBorrow.DebtOf(k);
                // komu reszta (ten sam wybor przy kluczu i w cieniu)
                string to;
                if (wk != null) to = "zdobywca - skarbiec krolestwa " + Nm(wk);
                else if (wHead != null) to = "zdobywca bez krolestwa - kiesa glowy rodu " + wHead.Name;
                else if (rHead != null) to = "dwor - kiesa glowy rodu panujacego " + Nm(royal) + " (" + rHead.Name + ")";
                else to = "kapital Banku (brak zywego odbiorcy)";
                string why = wf != null ? "szturm ostatniej twierdzy (zdobywca " + Nm(wf) + (undead ? ", Inni - monety nie biora" : "") + ")" : "upadek bez szturmu (pokoj, smierc wladcy, bunt)";
                var sb = new StringBuilder(600);
                sb.Append("Skarbiec upadlego krolestwa (G14): dzien ").Append(day).Append(" | ").Append(Nm(k)).Append(" (").Append(k.StringId).Append(") | ").Append(why)
                  .Append(" | skarbiec ").Append(w0).Append(", dlug Banku ").Append((long)Math.Ceiling(debt));
                if (!on)
                {
                    sb.Append(" | W CIENIU (klucz Fallen Crown Treasury wylaczony) - skarbiec wypada ze swiata jak dotad; z kluczem: Bankowi ").Append(Math.Min(w0, (long)Math.Ceiling(debt)))
                      .Append(", reszta ").Append(Math.Max(0, w0 - (long)Math.Ceiling(debt))).Append(" -> ").Append(to).Append('.');
                    Log.Info(sb.ToString());
                    return;
                }
                // 1. dlug Banku
                long paid = CrownBorrow.RepayOnFall(k, w0);
                if (paid > 0) k.KingdomBudgetWallet -= (int)paid;
                long rest = Math.Max(0, (long)k.KingdomBudgetWallet);
                long moved = 0, got = 0;
                bool pGot = false;
                if (rest > 0)
                {
                    int ir = (int)Math.Min(int.MaxValue, rest);
                    k.KingdomBudgetWallet -= ir;                                  // najpierw skarbiec, potem odbiorca: odbiorca dostaje dokladnie to, co zeszlo
                    moved = ir;
                    if (wk != null)
                    {
                        long v0 = wk.KingdomBudgetWallet;
                        wk.KingdomBudgetWallet += ir;
                        got = (long)wk.KingdomBudgetWallet - v0;
                        CrownIncome.ShiftSnapshot(wk, got);                       // 165: skarb przejety, nie wplyw dnia (jak L5b-1)
                        pGot = Clan.PlayerClan != null && ReferenceEquals(Clan.PlayerClan.Kingdom, wk);
                    }
                    else
                    {
                        Hero h = wHead ?? rHead;
                        if (h != null)
                        {
                            long v0 = h.Gold;
                            h.ChangeHeroGold(ir);
                            got = (long)h.Gold - v0;
                            CirculationWindows.NoteHeroGold(h, got);
                            if (h != Hero.MainHero) ClanIncomeBook.NoteInflow(h, (int)got, ClanIncomeBook.KRansom);   // jednorazowy wplyw od prawdziwego platnika
                            pGot = h == Hero.MainHero;
                        }
                        else { IronBank.CapitalAdd(ir); got = ir; }
                    }
                }
                long w1 = k.KingdomBudgetWallet;
                sb.Append(" | splacone Bankowi ").Append(paid).Append(" | reszta ").Append(moved).Append(" -> ").Append(to)
                  .Append(" | skarbiec po ").Append(w1).Append(" | kontrola: zdjete ").Append(paid + moved).Append(" - dopisane ").Append(paid + got).Append(" = ").Append(moved - got)
                  .Append(" | gracz ").Append(pGot ? "odbiorca" : "-").Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
                if (pGot && got > 0)
                {
                    try
                    {
                        Log.Player("The realm of " + Nm(k) + " is no more: " + got.ToString("N0", Inv) + " denars from its royal treasury "
                                   + (wk != null ? "pass to the treasury of " + Nm(wk) + "." : "are now in your purse."));
                    }
                    catch (Exception e) { Stumble("OnKingdomDestroyed(wiadomosc)", e); }
                }
            }
            catch (Exception e) { Stumble("OnKingdomDestroyed", e); }
        }
    }
}
