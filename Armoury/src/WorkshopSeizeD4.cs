using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// D4 (Jeff 14.11: "jak zdobywam miasto to warsztaty naleza do np. gang leader to wtedy ich nie zabieram, ale jak warsztat nalezy np. do wrogiego
    /// lorda ktory go kupil ... to wtedy mu zabieram i tak zdazyl wywiezc zloto to dobry pomysl"; wczesniej: "jak przejmuje wrogie warsztaty nikomu nic
    /// nie place, to wojna; warsztatow osob, z ktorymi nie tocze wojny, nie zabieram"). Klucze CapturedWorkshopsToTaker, RapidDevelopmentSavesCapital.
    ///
    /// Stan przed zmiana (dekompilacja):
    ///  - gra (WorkshopsCampaignBehavior.OnSettlementOwnerChanged, tw-campaign :315-330): warsztatow nikt nie przenosi; wlasciciel w wojnie z nowym panem
    ///    z perkiem Trade.RapidDevelopment dostaje 5 000 z niczego (510: placi nowy pan miasta - SmallGold/Perk510Gold); potem TransferPlayerWorkshopsIfNeeded
    ///    (:1074) - warsztaty GRACZA w miastach frakcji w wojnie z nim ida za darmo do notabla (ApplyByWar, cena 0);
    ///  - BK (BKWorkshopBehavior.OnOwnerChanged -> SeizeWorkshops, bk :65-181): przy kazdej zmianie pana miedzy frakcjami warsztaty bohaterow STAREJ frakcji
    ///    (wlasciciel.MapFaction == stara frakcja; notable maja frakcje osady, wiec nie) ida za darmo: najpierw do notabla (GetNotableOwnerForWorkshop), bez
    ///    notabla do glowy rodu pana miasta (do limitu), inaczej losowo do rodu krolestwa - zdobywca prawie nigdy go nie dostaje;
    ///  - lordowie AI KUPUJA warsztaty: BK BKLordPropertyBehavior.OnSettlementEntered (co tydzien, osada wlasnego krolestwa, krolestwo bez wojen) - placi
    ///    GetCostForPlayer obecnemu wlascicielowi; poza tym warsztaty lordow z BK SeizeWorkshops i z przywileju najemnika BK; nasze mody kupuja tylko notablami
    ///    (DebtLadder, WorkshopIdleRetool); kapital warsztatu zostaje przy kazdej zmianie wlasciciela (WorkshopTrade.OwnerPrefix).
    ///
    /// Teraz (jeden prefiks First na WorkshopsCampaignBehavior.OnSettlementOwnerChanged - przed petla perka gry, TransferPlayerWorkshopsIfNeeded i sluchaczem BK;
    /// BK OnOwnerChanged wylaczony, gdy klucz wlaczony): w miescie zdobytym oblezeniem (BySiege; inne zmiany pana - nic), warsztat bohatera z rodem (lord, gracz, czlonek rodu - nie notabl),
    /// ktorego frakcja jest W WOJNIE z frakcja zdobywcy, przechodzi BEZ zaplaty na zdobywce (glowa rodu bohatera, ktory zdobyl miasto; bez zdobywcy - nowy
    /// pan miasta). Warsztaty notabli (gang leader, kupiec, rzemieslnik...) i osob, z ktorymi zdobywca nie ma wojny, zostaja. Kapital warsztatu idzie z
    /// warsztatem (ApplyByDeath - kapital = w.Capital, cena 0), chyba ze dawny wlasciciel ma RapidDevelopment: wtedy "zdazyl wywiezc zloto" - caly kapital
    /// obrotowy przechodzi z warsztatu do jego kiesy (zloto ma platnika - warsztat - i odbiorce; nikt nie placi z niczego). 5 000 gry nie powstaje
    /// (po przejeciu zaden wlasciciel nie jest w wojnie z nowym panem). Recenzja D4: po wywiezieniu kapitalu zdobywca wklada kapital z wlasnej kiesy jak
    /// notabl po bankructwie (Refill), a BK OnWorkshopChange (-30 relacji jak przy kupnie) w trakcie przejecia jest pomijany.
    /// </summary>
    internal static class WorkshopSeizeD4
    {
        private const string P = "Warsztaty po zdobyciu (D4): ";
        private static Harmony _h;
        private static bool _tried;
        private static string _wire = "nie probowano";
        private static int _stumbles, _dTaken, _dRescued, _dKept, _dBkSkipped, _dFunded, _dUnfunded, _dRelSkipped, _dNotSiege;
        private static long _dCap, _dRescuedGold, _dFundGold;
        [ThreadStatic] private static bool _seizing;   // recenzja D4: przejecie wojenne w toku - BK OnWorkshopChange (-30 relacji jak przy kupnie) pomijany

        internal static bool On { get { var s = Settings.Current; return s != null && s.CapturedWorkshopsToTaker; } }

        internal static void Reset() { _dTaken = _dRescued = _dKept = _dBkSkipped = _dFunded = _dUnfunded = _dRelSkipped = _dNotSiege = 0; _dCap = _dRescuedGold = _dFundGold = 0; _seizing = false; }

        internal static void ApplyAll(Harmony h) { _h = h; }

        /// <summary>OnSessionLaunched: latki raz na proces (typ BK szukany po nazwie - BK zaladowany).</summary>
        internal static void EnsureHooks()
        {
            if (!_tried && _h != null)
            {
                _tried = true;
                var notes = new List<string>();
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(WorkshopsCampaignBehavior), "OnSettlementOwnerChanged");
                    if (m != null) { _h.Patch(m, prefix: new HarmonyMethod(typeof(WorkshopSeizeD4), nameof(OwnerChangedPrefix)) { priority = Priority.First }); notes.Add("gra OnSettlementOwnerChanged wpieta"); }
                    else notes.Add("gra OnSettlementOwnerChanged BRAK");
                }
                catch (Exception e) { notes.Add("gra OnSettlementOwnerChanged BLAD"); Log.Error("WorkshopSeizeD4.EnsureHooks(gra)", e); }
                try
                {
                    var t = AccessTools.TypeByName("BannerKings.Behaviours.Workshops.BKWorkshopBehavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "OnOwnerChanged") : null;
                    if (m != null) { _h.Patch(m, prefix: new HarmonyMethod(typeof(WorkshopSeizeD4), nameof(BkOwnerChangedPrefix))); notes.Add("BK OnOwnerChanged wpieta"); }
                    else notes.Add("BK OnOwnerChanged BRAK (" + (t == null ? "brak typu" : "brak metody") + ")");
                    // recenzja D4: BK OnWorkshopChange daje nowemu wlascicielowi -30 relacji z wlascicielami warsztatow tego typu (jak przy kupnie) - przy przejeciu wojennym pomijany
                    var mw = t != null ? AccessTools.DeclaredMethod(t, "OnWorkshopChange") : null;
                    if (mw != null) { _h.Patch(mw, prefix: new HarmonyMethod(typeof(WorkshopSeizeD4), nameof(BkWorkshopChangePrefix))); notes.Add("BK OnWorkshopChange wpieta"); }
                    else notes.Add("BK OnWorkshopChange BRAK (" + (t == null ? "brak typu" : "brak metody") + ")");
                }
                catch (Exception e) { notes.Add("BK OnOwnerChanged BLAD"); Log.Error("WorkshopSeizeD4.EnsureHooks(BK)", e); }
                _wire = string.Join(", ", notes.ToArray());
            }
            var s = Settings.Current;
            Log.Info(P + "start sesji | latki: " + _wire + " | klucze CapturedWorkshopsToTaker " + (s != null && s.CapturedWorkshopsToTaker ? "TAK" : "NIE")
                     + ", RapidDevelopmentSavesCapital " + (s != null && s.RapidDevelopmentSavesCapital ? "TAK" : "NIE")
                     + " | regula (tylko zdobycie oblezeniem - BySiege): warsztat bohatera z rodem w wojnie ze zdobywca -> zdobywca bez zaplaty; notable i osoby bez wojny - zostaja;"
                     + " Rapid Development wywozi kapital, zdobywca wklada kapital z kiesy (do polowy kiesy, jak notabl po bankructwie); BK SeizeWorkshops przy zmianie pana wylaczony,"
                     + " BK -30 relacji przy przejeciu wojennym pominiete.");
        }

        /// <summary>BK BKWorkshopBehavior.OnOwnerChanged: przy wlaczonym kluczu pomijany (reguly D4 zastepuja SeizeWorkshops przy zmianie pana; przy wypowiedzeniu wojny BK dziala dalej).</summary>
        public static bool BkOwnerChangedPrefix()
        {
            if (!On) return true;
            _dBkSkipped++;
            return false;
        }

        /// <summary>BK BKWorkshopBehavior.OnWorkshopChange: pomijany tylko w trakcie naszego przejecia wojennego (przejecie to nie kupno - bez -30 relacji z notablami).</summary>
        public static bool BkWorkshopChangePrefix()
        {
            if (!_seizing) return true;
            _dRelSkipped++;
            return false;
        }

        /// <summary>Prefiks First na WorkshopsCampaignBehavior.OnSettlementOwnerChanged (Settlement, openToClaim, newOwner, oldOwner, capturerHero, detail).
        /// Recenzja D4: regula tylko przy zdobyciu oblezeniem (BySiege) - Jeff: "jak zdobywam miasto". Barter / pokoj z oddaniem lenna, nadanie, decyzja krola,
        /// bunt, odejscie z frakcja, wydarzenia ROT (ApplyByDefault) - nic nie przejmujemy (BK SeizeWorkshops i tak pominiety - warsztaty zostaja u wlascicieli).</summary>
        public static void OwnerChangedPrefix(Settlement __0, Hero __2, Hero __4, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail __5)
        {
            if (!On) return;
            try
            {
                var st = __0;
                if (st == null || !st.IsTown || st.Town == null || st.Town.Workshops == null) return;
                if (__5 != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) { _dNotSiege++; return; }
                Hero taker = null;
                if (__4 != null && __4.IsAlive && __4.Clan != null && __4.Clan.Leader != null && __4.Clan.Leader.IsAlive) taker = __4.Clan.Leader;
                if (taker == null) taker = __2;
                if (taker == null || !taker.IsAlive || taker.MapFaction == null) return;
                var s = Settings.Current;
                var parts = new List<string>();
                int kept = 0;
                foreach (var w in st.Town.Workshops)
                {
                    try
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                        var owner = w.Owner;
                        if (owner == null || owner == taker) continue;
                        if (owner.IsNotable || owner.Clan == null || !owner.IsAlive) { kept++; continue; }   // notable (gang leader, kupiec...) - zostaje
                        var of = owner.MapFaction;
                        if (of == null || !of.IsAtWarWith(taker.MapFaction)) { kept++; continue; }        // bez wojny ze zdobywca - zostaje
                        int cap = w.Capital, rescued = 0;
                        if (s.RapidDevelopmentSavesCapital && cap > 0 && owner.GetPerkValue(DefaultPerks.Trade.RapidDevelopment))
                        {
                            // "zdazyl wywiezc zloto": kapital obrotowy warsztatu -> kiesa dawnego wlasciciela (warsztat placi, wlasciciel dostaje)
                            w.ChangeGold(-cap);
                            owner.ChangeHeroGold(cap);
                            CirculationWindows.NoteHeroGold(owner, cap);
                            rescued = cap;
                            _dRescued++; _dRescuedGold += cap;
                        }
                        string type = w.WorkshopType.StringId;
                        _seizing = true;
                        try { ChangeOwnerOfWorkshopAction.ApplyByDeath(w, taker); }   // cena 0, kapital = w.Capital (to, co zostalo), typ bez zmian
                        finally { _seizing = false; }
                        if (w.Owner != taker)
                        {
                            // wlasciciel bez zmiany (sluchacz rzucil przed zmiana) - kapital wraca do warsztatu
                            if (rescued > 0) { owner.ChangeHeroGold(-rescued); CirculationWindows.NoteHeroGold(owner, -rescued); w.ChangeGold(rescued); _dRescued--; _dRescuedGold -= rescued; }
                            parts.Add(type + " " + owner.Name + " - BEZ ZMIANY");
                            continue;
                        }
                        _dTaken++; _dCap += cap - rescued;
                        int fund = rescued > 0 ? Refill(w, taker) : 0;
                        parts.Add(type + " od " + owner.Name + " (kapital " + (cap - rescued) + (rescued > 0 ? ", Rapid Development wywiozl " + rescued
                                  + (fund > 0 ? ", zdobywca wlozyl z kiesy " + fund : ", zdobywca bez kiesy na kapital - warsztat padnie (bankructwo do notabla)") : "") + ")");
                        Notify(st, w, owner, taker, rescued, fund);
                    }
                    catch (Exception e) { Stumble("warsztat", e); }
                }
                _dKept += kept;
                if (parts.Count > 0 || kept > 0)
                    Log.Info(P + st.Name + " | zdobywca " + taker.Name + (__4 != null && __4 != taker ? " (zdobyl " + __4.Name + ")" : "") + ", nowy pan " + (__2 != null ? __2.Name.ToString() : "?")
                             + " | przejete " + parts.Count + (parts.Count > 0 ? ": " + string.Join("; ", parts.ToArray()) : "") + " | zostaja (notable / bez wojny) " + kept + ".");
            }
            catch (Exception e) { Stumble("OwnerChangedPrefix", e); }
        }

        /// <summary>
        /// Recenzja D4: po wywiezieniu kapitalu warsztat ma 0 - bez kapitalu nie kupi wsadu (gra CanNotableWorkshopProduceThisCycle: Capital &lt; koszt wsadu),
        /// a utrzymanie AI placi tylko z kapitalu (WorkshopTrade.PayToTown) - nazajutrz bankructwo i warsztat za darmo do notabla. Zdobywca wklada wiec
        /// kapital obrotowy z WLASNEJ kiesy - ta sama regula co notabl przejmujacy warsztat po bankructwie (WorkshopTrade.Fail): do kapitalu startowego,
        /// najwyzej polowa kiesy, co najmniej miesiac utrzymania; nie stac go - nic nie wklada, warsztat pada jak kazdy bez gotowki. Gracz i AI tak samo.
        /// Zloto: kiesa zdobywcy -> kapital jego warsztatu (nic z niczego).
        /// </summary>
        private static int Refill(Workshop w, Hero taker)
        {
            try
            {
                int need = Math.Max(0, w.InitialCapital - Math.Max(0, w.Capital));
                if (need <= 0) return 0;
                int least = Math.Min(need, (int)Math.Ceiling(WorkshopTrade.UpkeepOf(w) * 30f));
                int half = Math.Max(0, taker.Gold) / 2;
                if (half <= 0 || half < least) { _dUnfunded++; return 0; }
                int fund = Math.Min(need, half);
                taker.ChangeHeroGold(-fund);
                CirculationWindows.NoteHeroGold(taker, -fund);
                w.ChangeGold(fund);
                _dFunded++; _dFundGold += fund;
                return fund;
            }
            catch (Exception e) { Stumble("Refill", e); return 0; }
        }

        private static void Notify(Settlement st, Workshop w, Hero owner, Hero taker, int rescued, int fund)
        {
            try
            {
                string what = w.WorkshopType.Name.ToString(), town = st.Name.ToString();
                if (taker == Hero.MainHero)
                    Log.Player("You seize the " + what + " at " + town + " from " + owner.Name + " - it is war, you pay nothing."
                               + (rescued > 0 ? " " + owner.Name + " managed to carry off its " + rescued + " denars of working capital (Rapid Development)"
                                  + (fund > 0 ? " - you put " + fund + " denars of your own into it to keep it running." : " - you cannot afford new working capital, so it will soon go bankrupt.") : ""), false);
                else if (owner == Hero.MainHero)
                    Log.Player(taker.Name + " seizes your " + what + " at " + town + "."
                               + (rescued > 0 ? " You managed to carry off its " + rescued + " denars of working capital (Rapid Development)." : ""), true);
            }
            catch (Exception e) { Stumble("Notify", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("WorkshopSeizeD4." + where, e);   // potkniecie liczone, bez wylacznika
        }

        /// <summary>Linia doby (gdy cos sie dzialo).</summary>
        internal static void Daily(int day)
        {
            if (_dTaken > 0 || _dRescued > 0 || _dKept > 0 || _dBkSkipped > 0 || _dNotSiege > 0 || _dRelSkipped > 0)
                Log.Info(P + "dzien " + day + " | przejete przez zdobywcow " + _dTaken + " (kapital z warsztatem " + _dCap + ") | Rapid Development wywiozl kapital " + _dRescued
                         + " (" + _dRescuedGold + " zl; zdobywca wlozyl z kiesy " + _dFunded + " x, " + _dFundGold + " zl; bez kiesy " + _dUnfunded + ") | zostaly (notable / bez wojny) " + _dKept
                         + " | zmiany pana bez oblezenia (nic nie przejete) " + _dNotSiege + " | BK SeizeWorkshops pominiete " + _dBkSkipped + ", BK -30 relacji pominiete " + _dRelSkipped
                         + " | potkniecia razem " + _stumbles + ".");
            Reset();
        }
    }
}
