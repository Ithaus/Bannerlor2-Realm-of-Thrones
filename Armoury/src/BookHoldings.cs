using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// f19-kanon-b (noc 18.10) - JEDNORAZOWE PRZYWROCENIE WLOSCI Z KSIAZKI w trwajacym zapisie (Ironman Jeffa, dzien ~109056). Jeff 18.10: "czy mozesz w savie
    /// przywrocic kazdej ze stron posiadlosci jakie powinni miec, czyli oddac wlosci tak jak to wygladalo w ksiazce"; "naprawa tych wydarzen i cofniecie polnocy".
    /// Decyzja prowadzacego (analiza-kanon.json, tabela czesci 4): 8 twierdz - King's Landing (+ Red Keep - blizniak ROT) do Joffreya, Sharp Point / Stonedance /
    /// Sweetport / Cape Wrath / Fellwood do lennikow Stannisa, Starfall do Daynow; Harrenhal zostaje u Boltona; Essos nietkniete. Do tego zwrot 1 125 434 zl, ktore
    /// Polnoc wziela ze skarbca Korony przy zdobyciu Przystani (L5b-1, Armoury-2026-10-18_13-50-06.log l.1636, dzien 109046) - przelew zamkniety, tyle, ile jest.
    ///
    /// KIEDY: klucz BookHoldingsRestore (domyslnie TAK), raz na zapis - flaga w zapisie "arm_book_holdings" (BookHoldingsBehavior). Nowa kampania (OnNewGameCreated)
    /// dostaje flage "new" od razu: startuje z wlosci ROT, wiec nie ma czego przywracac (zdobycze w niej sa juz gra, nie bledem). Brak klucza w zapisie = zapis sprzed
    /// paczki - przywrocenie czeka na pierwsza godzine gry, w ktorej kampania jest gotowa, a partia gracza stoi na mapie: nie w osadzie, nie w menu, nie w spotkaniu,
    /// nie w bitwie ani oblezeniu, nie w rozmowie, nie w armii, nie w niewoli, stan gry = mapa (PartyDiplomaticHandlerCampaignBehavior.CheckSettlementSuitabilityForParties
    /// wola GameMenu.SwitchToMenu, gdy gracz siedzi w osadzie albo w armii w niej - pulapka CTD z CLAUDE.md 7; spotkanie przy wsi napadanej - ActivateGameMenu).
    /// Inaczej czeka do kolejnej godziny (linia "czeka" raz na dobe). Nigdy GameMenu.SwitchToMenu.
    ///
    /// JAK (dla kazdej osady z tabeli, kazda we wlasnym try - potkniecia liczone, 3 wyjatki na tej samej osadzie = pominieta, bez globalnego wylacznika):
    ///  (b) warunki: osada istnieje; rod docelowy zywy, nie wyeliminowany, glowa zywa i wolna, rod w oczekiwanym krolestwie; obecny pan inny niz docelowy, ma glowe
    ///      rodu (ROT/Diplomacy/NavalDLC/ClanVariables czytaja oldOwner.Clan bez sprawdzenia) i nie jest graczem; brak oblezenia, bitwy w osadzie, w zalodze,
    ///      w jej wsiach i w promieniu 5 jedn. od bramy. Warunki trwale (rod, krolestwo, gracz) - osada pominieta na stale; chwilowe - ponowna proba co godzine,
    ///      po 30 dobach czekania - pominieta ("czekanie").
    ///  (c) przed zmiana pana, dla osady i jej blizniaka ROT: bohaterowie bez druzyny, ktorych gra wzielaby do niewoli (PrisonerCaptureCampaignBehavior.HandleSettlementHeroes
    ///      - ten sam warunek SettlementHeroCaptureCommonCondition, liczony wobec frakcji NOWEGO pana: nie gracz, nie wedrowiec, nie notabl, nie jeniec, zywy, frakcja
    ///      w wojnie z nowym panem) - TeleportHeroAction.ApplyImmediateTeleportToSettlement do najblizszej twierdzy wlasnej frakcji (nieoblezonej, spoza zmiany),
    ///      bez niej - najblizszej nie wrogiej; zarzadca najpierw zdjety (ChangeGovernorAction.RemoveGovernorOf). Zlapany Robb w lochu Joffreya psulby Krwawe Gody.
    ///      Zaloga starego pana (szeregowi, ranni razem z nimi) - do najblizszych partii lordow i zalog krolestwa starego pana do ich wolnego miejsca (limit gry),
    ///      reszta do druzyny glowy starego rodu, bez niej do najblizszego odbiorcy ponad limit (model dezercji zdejmie nadmiar, OverflowHome W4c odesle go do wsi - jak kazde przepelnienie);
    ///      zadnego odbiorcy (po recenzji) - szeregowi do domu droga przepelnienia W4c (ClanBudget.OverflowVillage + ToVillagePop, ReleaseHome) i zmiana idzie dalej;
    ///      bez wsi z danymi BK - osada czeka. Ludzie prawdziwi: nic nie powstaje, nic nie znika; ksiega ludzi (PeopleBook.Take/Give, "zaloga->partia") i sprzet
    ///      ze zbrojowni zalogi (GarrisonArmory.KitsWithMen - ta sama regula co nowa druzyna z zalogi) ida z ludzmi. Gracz i jego partie nigdy nie sa odbiorca.
    ///      Stary pan z krolestwa docelowego (inny rod tego samego krolestwa; po recenzji) - zaloga zostaje z osada i nie ma korekty fabuly (e): to wlasni ludzie nowego pana.
    ///      Zwykli jency w lochu - zostaja przy osadzie (jency osady; ChangeOwnerOfSettlementAction ich nie rusza), linia podaje ich liczbe; bohaterow strony nowego
    ///      pana wypuszcza sama gra (PrisonerReleaseCampaignBehavior.OnSettlementOwnerChanged). Uwolnienie zwyklych "swoich" do zalogi - decyzja Jeffa 19.10 "7. a"
    ///      (f23-spichlerze, klucz BookHoldingsFreeOwnPrisoners) - punkt (g0) nizej.
    ///  (d) ChangeOwnerOfSettlementAction.ApplyByDefault(glowa rodu docelowego, osada) - NIE BySiege (niszczy zaloge, kary relacji i wplywu, ekspansjonizm Diplomacy,
    ///      nasze L5b-1 / KingdomBalance / WarLedger / Rumours / SmallGold / CitySack), NIE ByGift / ByBarter (BKTitleBehavior przenosi wtedy tytuly). Default: bez
    ///      glosowania o lenno (openToClaim false), decyzje o tej osadzie gra usuwa sama, kolejka budow czyszczona, zarzadca zdjety. King's Landing tylko jako
    ///      miasto - Red Keep przenosi ROT (ROTOthersCampaignBehavior.OnSettlementOwnerChanged, ApplyByDefault(newOwner, RedKeep), gdy frakcje sie roznia); potem
    ///      sprawdzamy pana Red Keep i dopiero wtedy ewentualnie zmieniamy go sami.
    ///      Strategic Campaign AI (StrategicAiState.MarkLostClaim) zapisuje kazda utracona twierdze jako "roszczenie" krolestwa starego pana (cel wojny ReclaimLostFief,
    ///      x1.65 i x1.65 do celu oblezenia) - nasza korekta kroniki to nie strata w wojnie: wpis roszczenia krolestwa starego pana wraca do stanu sprzed zmiany
    ///      (refleksja na StrategicAiState.LostSettlementClaimByFaction; brak typu - linia, bez skutku).
    ///  (e) po oddaniu King's Landing (wiersz z promieniem): partie lordow krolestwa starego pana w promieniu 60 jedn. od bramy (armia z wodzem w promieniu - cala,
    ///      takze partie idace do niej; samodzielne partie rodu gracza - nie) - do najblizszej wlasnej twierdzy poza promieniem (Position = brama, SetMoveGoToSettlement - wzor Mends/Harrenhal z CrashScribe); partie na morzu pomijane. Warunek liczony
    ///      PRZED zmiana: zadna z nich nie jest w bitwie ani oblezeniu, inaczej King's Landing czeka (jednorazowa korekta fabuly, zeby miasto nie padlo znow za godzine).
    ///  (f) zwrot skarbca (wiersz King's Landing, tylko gdy pan zmienil sie wlasnie teraz ze stolicy krolestwa "battania"): min(1 125 434, skarbiec Polnocy) ze skarbca
    ///      Polnocy do skarbca krolestwa nowego pana; zdjete = dopisane (kontrola w linii); migawka korony 165 przesunieta u obu stron (CrownIncome.ShiftSnapshot - jak
    ///      L5b-1: to nie "wplyw dnia" ani "ujemny wplyw"), po recenzji tak samo historia skarbca miary biedy E1 w CrashScribe (PovertyPeace.ShiftHistory - inaczej
    ///      Polnoc przez 7 dob w falszywej biedzie). Raz na zapis (stan w zapisie).
    ///  (g0) f23-spichlerze (decyzja Jeffa 19.10 "7. a": jency z Korony od razu wracaja do zalogi miasta; klucz BookHoldingsFreeOwnPrisoners, domyslnie TAK): zaraz po
    ///      zmianie pana, dla kazdej twierdzy, ktora zmienila pana w tej probie (osada i blizniak ROT; kazdy wiersz tabeli), PRZED obsada (g) - zwykli jency z lochu osady
    ///      (Settlement.Party.PrisonRoster; takze loch partii zalogi, gdyby gra cos w nim trzymala), ktorzy sa zolnierzami krolestwa nowego pana, do zalogi. "Zolnierz
    ///      krolestwa" = kultura oddzialu jest kultura krolestwa, rodu nowego pana albo jednego z rodow krolestwa (bez najemnikow i wyeliminowanych) - z tych drzew
    ///      oddzialow lordowie krolestwa werbuja; nigdy bandyci (kultura IsBandit), wighty (Inni) ani bohaterowie (tych wypuszcza gra). Reszta zostaje w lochu.
    ///      Cale stosy (ranni z nimi), TroopRoster.AddToCounts loch -> zaloga; ksiega ludzi: Take z lochu i Give do zalogi z ta sama przyczyna FLooted ("odbici jency" -
    ///      ta sama droga, ktora gra uwalnia jencow zwyciezcy przy zdobyciu twierdzy). Bez sprzetu (zabrany przy pojmaniu) - zbrojownia zalogi dokupi braki jak
    ///      kazdemu nowemu. Ponad limit zalogi gry - nadmiar odejdzie przepelnieniem W4c do domu (linia mowi ile). Linia: wypuszczeni, zaloga przed -> po, loch przed ->
    ///      po, kto zostal (obcy, bandyci, Inni), kontrola zdjete z lochu - dodane do zalogi = 0. Klucz NIE - jak dotad (zwykli zostaja w lochu).
    ///  (g) zaloga nowego pana - zadnych ludzi z niczego. f21-zaloga (klucz BookHoldingsGarrison, domyslnie TAK; decyzja Jeffa po recenzji calosci: obsadzic pusta
    ///      zaloge ludzmi krolestwa nowego pana): zaraz po zmianie pana, dla kazdej twierdzy, ktora zmienila pana w tej probie (osada i blizniak ROT), szeregowi
    ///      z partii lordow - najpierw rodu nowego pana, potem pozostalych rodow jego krolestwa, kazda grupa od najblizszej bramy - do zalogi az do celu: miasto 400,
    ///      zamek 200, nie ponad limit zalogi gry (PartySizeLimit zalogi - ponad nim nadmiar odszedlby przepelnieniem W4c) i nie ponad cel zalogi wedlug gry
    ///      (GarrisonTroopsCampaignBehavior.CalculateSettlementGarrisonPartySizeLimitWithFoodAndWage w widelkach gry 125-750 / 75-500 - ile osada wyzywi i ile
    ///      krolestwo i kiesy jego rodow udzwigna; ta sama miara, wedlug ktorej lordowie gry zostawiaja ludzi w zalodze). Dawca: partia lorda na ladzie z wodzem,
    ///      nie gracz i nie rod gracza, nie najemnicy, nie w bitwie, oblezeniu (oblegajacy i obleganej osadzie), przejsciu lad-morze ani rozwiazaniu; zostawia
    ///      >= 50% i >= 20 szeregowych wzgledem liczby z pierwszego uzycia w sesji (kilka twierdz z rzedu nie tnie jej po 50% od nowa). Z kazdego stosu proporcjonalnie
    ///      (ranni proporcjonalnie, sklad dawcy zostaje), wight (Inni) nie idzie. Ksiega ludzi (PeopleBook Take/Give, FToGarrison - jak latka LeaveTroopsToGarrison)
    ///      i sprzet (GarrisonArmory.KitsToGarrison - MoveKits "leave", jak LeavePostfix) ida z ludzmi. Bez wplywu za ludzi dla dawcy (gra daje go w LeaveTroopsToGarrison
    ///      - tu to nie decyzja lorda) i bez zdarzenia OnTroopGivenToSettlement (tylko pomiary). Za malo ludzi - zaloga dostaje tyle, ile jest, linia podaje brak.
    ///      Linia osady: cel, limit gry, zaloga przed -> po, dawcy (rod, odleglosc, ludzie), brak, pominieci dawcy; potem zaloga i milicja po zmianie.
    ///      Klucz NIE - jak dotad: pusta zaloga zostaje werbunkowi pana i naszym regulom zalog.
    ///  (h) wiadomosc dla gracza po angielsku raz na partie zmian (zwykle jedna: wszystko w pierwszej godzinie).
    ///  (i) linia na osade + linia konca; Essos nietkniete (nie ma go w tabeli); idempotentne - flaga w zapisie, osada juz u rodu docelowego = "bez zmian".
    /// </summary>
    internal static class BookHoldings
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Prefix = "Wlosci z kronik (f19-kanon-b): ";
        private const string Ver = "bh1";
        private const int GiveUpDays = 30;          // tyle dob czekania na warunki chwilowe - potem osada pominieta ("czekanie")
        private const int MaxRowErrors = 3;         // wyjatki na jednej osadzie - potem pominieta ("blad"); nic nie gasnie globalnie
        private const float BattleNear = 5f;        // bitwa w tym promieniu od bramy = osada czeka
        private const float ThreatRadius = 40f;     // recenzja calosci 18/19.10: promien pomiaru sil przy przywroconej twierdzy (tylko log; wiersz z wiekszym promieniem korekty - jego promien)
        private const int GarTownTarget = 400;      // f21-zaloga: cel obsady miasta (nie ponad limit zalogi gry)
        private const int GarCastleTarget = 200;    // f21-zaloga: cel obsady zamku
        private const double DonorKeepShare = 0.5;  // f21-zaloga: dawca zostawia co najmniej tyle swoich szeregowych (wzgledem pierwszego uzycia w sesji)
        private const int DonorKeepMin = 20;        // f21-zaloga: i nigdy mniej niz tylu

        /// <summary>Wiersz tabeli: osada -> rod docelowy (i krolestwo, w ktorym rod ma byc), blizniak ROT, promien korekty fabuly, zwrot skarbca.</summary>
        private sealed class Row
        {
            internal readonly string Id, Clan, Kingdom, Twin, RefundFrom;
            internal readonly float ClearRadius;
            internal readonly long Refund;
            internal Row(string id, string clan, string kingdom, string twin = null, float clearRadius = 0f, string refundFrom = null, long refund = 0)
            { Id = id; Clan = clan; Kingdom = kingdom; Twin = twin; ClearRadius = clearRadius; RefundFrom = refundFrom; Refund = refund; }
        }

        // tabela z analizy (wlasciciele startu ROT: ROT-Map/ModuleData/settlements.xml; rody: ROT-Content/ModuleData/spclans.xml; id osad: ROT.Misc.ROTSettlements)
        private static readonly Row[] Rows =
        {
            new Row("town_EW4", "clan_nord_1", "vlandia", "ROT_castle61", 60f, "battania", 1125434),   // King's Landing (+ Red Keep) -> Baratheon, Joffrey; zwrot L5b-1
            new Row("ROT_town31", "ROTclan_15", "dragonstone"),            // Sharp Point -> bar Emmon
            new Row("castle_EW5", "ROTclan_53", "dragonstone"),            // Stonedance -> Massey (RenlyDeathEvent 112-145: Buckler odszedlby z nim do Korony)
            new Row("castle_EW8", "ROTclan_138", "dragonstone"),           // Sweetport -> Sunglass
            new Row("ROT_castle18", "clan_empire_west_9", "dragonstone"),  // Cape Wrath -> Seaworth
            new Row("ROT_castle17", "ROTclan_17", "dragonstone"),          // Fellwood -> Fell
            new Row("ROT_town9", "clan_sturgia_4", "aserai"),              // Starfall -> Dayne
        };

        // ------------------------------------------------------------ stan w zapisie
        private static string _state = "";          // "" - czeka (zapis sprzed paczki), "done" - zrobione, "new" - kampania zaczeta z paczka
        private static readonly Dictionary<string, string> _res = new Dictionary<string, string>(StringComparer.Ordinal);   // osada -> wynik koncowy
        private static string _refund = "";          // "" - nie bylo, "done:kwota", "none:powod"
        private static int _firstDay = -1;           // doba pierwszej proby (licznik czekania)
        private static int _doneDay = -1;

        // ------------------------------------------------------------ sesja (tylko pamiec)
        private static int _stumbles;
        private static readonly Dictionary<string, int> _rowErr = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static int _waitLogDay = -1;
        private static string _waitLogText;
        private static readonly List<string> _batchDone = new List<string>();      // osady zmienione w tej godzinie (wiadomosc)
        private static readonly List<string> _batchMoved = new List<string>();     // korekta fabuly w tej godzinie (wiadomosc)
        private static long _batchRefund;
        private static string _batchRefundText;
        private static readonly Dictionary<MobileParty, int> _donorBase = new Dictionary<MobileParty, int>();   // f21-zaloga: szeregowi dawcy przy pierwszym uzyciu w sesji

        internal static bool On { get { var s = Settings.Current; return s != null && s.BookHoldingsRestore; } }
        private static bool GarrisonOn { get { var s = Settings.Current; return s != null && s.BookHoldingsGarrison; } }
        private static bool FreeOn { get { var s = Settings.Current; return s != null && s.BookHoldingsFreeOwnPrisoners; } }

        internal static void Reset()
        {
            _state = ""; _res.Clear(); _refund = ""; _firstDay = -1; _doneDay = -1;
            _stumbles = 0; _rowErr.Clear(); _errSites.Clear(); _waitLogDay = -1; _waitLogText = null;
            _batchDone.Clear(); _batchMoved.Clear(); _batchRefund = 0; _batchRefundText = null;
            _donorBase.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("BookHoldings." + where, e); } catch { }
        }

        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(MobileParty mp)
        {
            try { return mp == null ? "-" : (mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : (mp.Name != null ? mp.Name.ToString() : mp.StringId)); }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var sb = new StringBuilder(256);
            sb.Append(Ver).Append('|').Append(_state).Append('|');
            bool first = true;
            foreach (var kv in _res) { if (!first) sb.Append(';'); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value); }
            sb.Append('|').Append(_refund).Append('|').Append(_firstDay.ToString(Inv)).Append('|').Append(_doneDay.ToString(Inv));
            return sb.ToString();
        }

        /// <summary>Brak klucza (zapis sprzed paczki) = czeka. Napis nieczytelny = "done" (ostroznie: klucz byl, wiec przywrocenie najpewniej juz bieglo).</summary>
        internal static void Import(string v)
        {
            Reset();
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var p = v.Split('|');
                if (p.Length < 6 || p[0] != Ver) { _state = "done"; Log.Info(Prefix + "zapis w nieznanym formacie (\"" + (v.Length > 80 ? v.Substring(0, 80) : v) + "\") - uznane za zrobione, nic nie zmieniam."); return; }
                _state = p[1] == "done" || p[1] == "new" ? p[1] : "";
                if (p[2].Length > 0)
                    foreach (var e in p[2].Split(';'))
                    {
                        int i = e.IndexOf('=');
                        if (i > 0) _res[e.Substring(0, i)] = e.Substring(i + 1);
                    }
                _refund = p[3];
                int d;
                _firstDay = int.TryParse(p[4], NumberStyles.Integer, Inv, out d) ? d : -1;
                _doneDay = int.TryParse(p[5], NumberStyles.Integer, Inv, out d) ? d : -1;
            }
            catch (Exception e) { _state = "done"; Stumble("Import", e); }
        }

        /// <summary>Nowa kampania: wlosci startu ROT - nic do przywracania (flaga od razu).</summary>
        internal static void OnNewGame() { _state = "new"; }

        internal static void SessionStart()
        {
            try
            {
                string st = _state == "new" ? "nowa kampania z ta paczka - nic do przywracania"
                          : _state == "done" ? "zrobione (dzien " + _doneDay + ": " + ResText() + ", zwrot skarbca " + (_refund.Length > 0 ? _refund : "-") + ")"
                          : "czeka (zapis sprzed paczki) - ruszy w pierwszej godzinie na mapie" + (_res.Count > 0 ? "; juz rozstrzygniete: " + ResText() : "");
                Log.Info(Prefix + "start sesji | klucz Book Holdings Restore " + (On ? "TAK" : "NIE") + ", Book Holdings Garrison (f21-zaloga) " + (GarrisonOn ? "TAK" : "NIE")
                         + ", Book Holdings Free Own Prisoners (f23) " + (FreeOn ? "TAK" : "NIE") + " | stan zapisu: " + st + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static string ResText()
        {
            if (_res.Count == 0) return "-";
            var sb = new StringBuilder(200);
            foreach (var kv in _res) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
            return sb.ToString();
        }

        // ------------------------------------------------------------ gotowosc: gracz na mapie
        private static bool PlayerOnMap(out string why)
        {
            why = null;
            var c = Campaign.Current;
            if (c == null || !c.GameStarted) { why = "kampania nie gotowa"; return false; }
            var ms = Game.Current != null && Game.Current.GameStateManager != null ? Game.Current.GameStateManager.ActiveState as MapState : null;
            if (ms == null) { why = "stan gry to nie mapa"; return false; }
            var me = Hero.MainHero; var mp = MobileParty.MainParty;
            if (me == null || mp == null || !me.IsAlive) { why = "brak gracza"; return false; }
            if (me.IsPrisoner || PlayerCaptivity.IsCaptive) { why = "gracz w niewoli"; return false; }
            // Autotest (tylko LogGate 1/2, plik armoury-wlosci-test.flag): gracz w osadzie spoza tabeli (i nie blizniaku) nie blokuje proby - w autotescie druzyna stoi
            // caly czas w miescie, a sciezka zmiany pana musi byc sprawdzona przed gra Jeffa. W grze Jeffa (bez pliku) warunek jak dotad.
            bool testInTown = mp.CurrentSettlement != null && AutotestFlag && !IsTableSite(mp.CurrentSettlement);
            if (mp.CurrentSettlement != null && !testInTown) { why = "gracz w osadzie (" + Nm(mp.CurrentSettlement) + ")"; return false; }
            if (mp.MapEvent != null) { why = "gracz w bitwie"; return false; }
            if (mp.SiegeEvent != null || mp.BesiegerCamp != null || mp.BesiegedSettlement != null) { why = "gracz w oblezeniu"; return false; }
            if (mp.Army != null) { why = "gracz w armii"; return false; }
            if (PlayerEncounter.Current != null && !testInTown) { why = "spotkanie w toku"; return false; }   // autotest: spotkanie z osada spoza tabeli (menu miasta)
            if (c.CurrentMenuContext != null && !testInTown) { why = "menu otwarte"; return false; }
            var cm = c.ConversationManager;
            if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) { why = "rozmowa w toku"; return false; }
            return true;
        }

        /// <summary>Osada z tabeli albo jej blizniak (Red Keep) - przy niej gracz zawsze blokuje probe (takze w autotescie).</summary>
        private static bool IsTableSite(Settlement s)
        {
            if (s == null) return false;
            foreach (var row in Rows) if (row.Id == s.StringId || (row.Twin != null && row.Twin == s.StringId)) return true;
            return false;
        }

        private static int _testFlag = -1;
        /// <summary>Plik Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-wlosci-test.flag - tylko w trybie autotestu CrashScribe (LogGate 1/2), nigdy w grze Jeffa;
        /// sprawdzany raz na proces po starcie kampanii.</summary>
        private static bool AutotestFlag
        {
            get
            {
                if (_testFlag >= 0) return _testFlag == 1;
                if (Campaign.Current == null) return false;
                int f = 0;
                try
                {
                    int m = LogGate.Mode;
                    if (m == 1 || m == 2)
                    {
                        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string path = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), "armoury-wlosci-test.flag");
                        if (System.IO.File.Exists(path)) f = 1;
                    }
                }
                catch { f = 0; }
                _testFlag = f;
                if (f == 1) Log.Info(Prefix + "AUTOTEST: plik armoury-wlosci-test.flag - gracz w osadzie spoza tabeli nie blokuje przywrocenia (tylko ten bieg).");
                return f == 1;
            }
        }

        // ------------------------------------------------------------ godzina
        /// <summary>HourlyTickEvent (BookHoldingsBehavior). Przy kluczu i stanie "czeka" - proba przywrocenia; nie rzuca.</summary>
        internal static void Hourly()
        {
            if (_state.Length > 0 || !On) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                string why;
                if (!PlayerOnMap(out why)) { WaitLog(day, why); return; }
                if (_firstDay < 0) _firstDay = day;
                _batchDone.Clear(); _batchMoved.Clear(); _batchRefund = 0; _batchRefundText = null;
                var waits = new List<string>();
                foreach (var row in Rows)
                {
                    if (_res.ContainsKey(row.Id)) continue;
                    string wait = null, res = null;
                    try { res = TryRow(row, day, out wait); }
                    catch (Exception e)
                    {
                        Stumble("TryRow(" + row.Id + ")", e);
                        int n; _rowErr.TryGetValue(row.Id, out n); _rowErr[row.Id] = ++n;
                        if (n >= MaxRowErrors) { res = "blad"; Log.Info(Prefix + "dzien " + day + " | " + row.Id + " - " + n + " wyjatki z rzedu, osada pominieta (blad w logu wyzej)."); }
                        else wait = "wyjatek " + n + "/" + MaxRowErrors;
                    }
                    if (res != null) _res[row.Id] = res;
                    else if (wait != null) waits.Add(row.Id + ": " + wait);
                }
                Notify();
                if (waits.Count == 0) { Finish(day, false); return; }
                if (day - _firstDay >= GiveUpDays)
                {
                    foreach (var row in Rows) if (!_res.ContainsKey(row.Id)) _res[row.Id] = "czekanie";
                    Log.Info(Prefix + "dzien " + day + " | po " + (day - _firstDay) + " dobach czekania pominiete: " + string.Join("; ", waits.ToArray()) + ".");
                    Finish(day, true);
                    return;
                }
                WaitLog(day, string.Join("; ", waits.ToArray()));
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static void WaitLog(int day, string why)
        {
            try
            {
                if (day == _waitLogDay && why == _waitLogText) return;
                if (day == _waitLogDay && _waitLogText != null && why != null && why.StartsWith("gracz", StringComparison.Ordinal) && _waitLogText.StartsWith("gracz", StringComparison.Ordinal)) return;
                _waitLogDay = day; _waitLogText = why;
                Log.Info(Prefix + "czeka - dzien " + day + " | " + why + " (ponowna proba co godzine; po " + GiveUpDays + " dobach czekania na osade - pominieta).");
            }
            catch { }
        }

        private static void Finish(int day, bool gaveUp)
        {
            _state = "done"; _doneDay = day;
            int ok = 0, same = 0, skip = 0;
            foreach (var kv in _res) { if (kv.Value.StartsWith("ok", StringComparison.Ordinal)) ok++; else if (kv.Value == "bez-zmian") same++; else skip++; }
            Log.Info(Prefix + "KONIEC - dzien " + day + " | zmienione " + ok + ", juz u rodu docelowego " + same + ", pominiete " + skip + (gaveUp ? " (w tym po czekaniu)" : "")
                     + " z " + Rows.Length + " | " + ResText() + " | zwrot skarbca: " + (_refund.Length > 0 ? _refund : "nie dotyczy") + " | potkniecia " + _stumbles
                     + " | flaga w zapisie: zrobione (nie powtorzy sie).");
        }

        // ------------------------------------------------------------ osada
        private static Clan FindClan(string id)
        {
            foreach (var c in Clan.All) if (c != null && c.StringId == id) return c;
            return null;
        }

        private static Kingdom FindKingdom(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        /// <summary>Warunki chwilowe miejsca: oblezenie, bitwa w osadzie / zalodze / wsiach / przy bramie. null = wolne.</summary>
        private static string SiteBusy(Settlement st)
        {
            if (st.IsUnderSiege || st.SiegeEvent != null) return Nm(st) + " oblegana";
            if (st.Party != null && st.Party.MapEvent != null) return "bitwa w " + Nm(st);
            var g = st.Town != null ? st.Town.GarrisonParty : null;
            if (g != null && (g.MapEvent != null || g.SiegeEvent != null)) return "zaloga " + Nm(st) + " w bitwie";
            foreach (var v in st.BoundVillages)
                if (v != null && v.Settlement != null && (v.Settlement.IsUnderRaid || (v.Settlement.Party != null && v.Settlement.Party.MapEvent != null))) return "bitwa we wsi " + Nm(v.Settlement);
            var mem = Campaign.Current.MapEventManager;
            if (mem != null && mem.MapEvents != null)
            {
                var gate = st.GatePosition.ToVec2();
                foreach (var me in mem.MapEvents)
                    if (me != null && !me.IsFinalized && me.Position.ToVec2().Distance(gate) <= BattleNear) return "bitwa przy " + Nm(st);
            }
            return null;
        }

        /// <summary>Jedna osada z tabeli. Zwraca wynik koncowy albo null (wtedy `wait` - powod czekania).</summary>
        private static string TryRow(Row row, int day, out string wait)
        {
            wait = null;
            var st = Settlement.Find(row.Id);
            if (st == null || !st.IsFortification || st.Town == null) { LogSkip(day, row, null, "osady nie ma w swiecie"); return "brak-osady"; }
            var clan = FindClan(row.Clan);
            if (clan == null || clan.IsEliminated) { LogSkip(day, row, st, "rodu " + row.Clan + " nie ma albo wyeliminowany"); return "brak-rodu"; }
            var leader = clan.Leader;
            if (leader == null || !leader.IsAlive) { LogSkip(day, row, st, "rod " + clan.Name + " bez zywej glowy"); return "brak-glowy"; }
            if (clan.Kingdom == null || clan.Kingdom.StringId != row.Kingdom)
            { LogSkip(day, row, st, "rod " + clan.Name + " nie w krolestwie " + row.Kingdom + " (jest w " + (clan.Kingdom != null ? clan.Kingdom.StringId : "-") + ")"); return "inne-krolestwo"; }
            if (clan == Clan.PlayerClan) { LogSkip(day, row, st, "rod docelowy to rod gracza"); return "gracz"; }
            IFaction newF = clan.MapFaction;
            Settlement twin = row.Twin != null ? Settlement.Find(row.Twin) : null;
            if (twin != null && (!twin.IsFortification || twin.Town == null)) twin = null;
            // osada zmienia pana, gdy nie jest u rodu docelowego; blizniak ROT - gdy jest poza krolestwem docelowym (wtedy ROT przenosi go razem z miastem:
            // ROTOthersCampaignBehavior - RedKeep.MapFaction != KingsLanding.MapFaction; blizniak u innego rodu tego samego krolestwa zostaje, jak u ROT)
            bool stNeeds = st.OwnerClan != clan;
            bool twinNeeds = twin != null && twin.MapFaction != newF;
            if (!stNeeds && !twinNeeds) { LogSkip(day, row, st, "juz u rodu " + clan.Name + (twin != null ? " (blizniak " + Nm(twin) + " w krolestwie " + Nm(newF) + ")" : "")); return "bez-zmian"; }
            var sites = new List<Settlement>(2);
            if (stNeeds) sites.Add(st);
            if (twinNeeds) sites.Add(twin);
            foreach (var s in sites)
                if (s.OwnerClan == Clan.PlayerClan) { LogSkip(day, row, st, Nm(s) + " nalezy do gracza - nie zabieram"); return "gracz"; }
            foreach (var s in sites)
            {
                // ROT, Diplomacy, NavalDLC i ClanVariables czytaja oldOwner.Clan bez sprawdzenia - stary pan musi miec glowe rodu
                if (s.OwnerClan == null || s.OwnerClan.Leader == null || s.MapFaction == null) { wait = Nm(s) + ": stary pan bez glowy rodu"; return null; }
                string b = SiteBusy(s);
                if (b != null) { wait = b; return null; }
            }
            if (leader.IsPrisoner) { wait = "glowa rodu " + Nm(leader) + " w niewoli"; return null; }
            var main = stNeeds ? st : twin;
            IFaction oldF = main.MapFaction;

            // (e) warunek korekty fabuly PRZED zmiana: partie krolestwa starego pana w promieniu nie moga byc w bitwie ani oblezeniu.
            // Po recenzji: tylko przy zmianie krolestwa - stary pan z krolestwa docelowego to wlasne partie nowego pana, ich nie odsylamy
            bool clear = stNeeds && row.ClearRadius > 0f && oldF != newF;
            if (clear)
            {
                string busy;
                RadiusGroups(st, oldF, row.ClearRadius, true, out busy);
                if (busy != null) { wait = busy; return null; }
            }

            // (c) plan: bohaterowie i odbiorcy zalog - wszystko sprawdzone, zanim cokolwiek ruszy
            var heroPlan = new List<KeyValuePair<Hero, Settlement>>();
            foreach (var s in sites)
                foreach (var h in new List<Hero>(s.HeroesWithoutParty))
                {
                    if (!WouldBeCaptured(h, newF)) continue;
                    var dest = NearestRefuge(h, s, sites);
                    if (dest == null) { wait = "brak dokad wyprowadzic " + Nm(h); return null; }
                    heroPlan.Add(new KeyValuePair<Hero, Settlement>(h, dest));
                }
            var recips = new Dictionary<Settlement, List<MobileParty>>();
            var heads = new Dictionary<Settlement, MobileParty>();
            var homes = new Dictionary<Settlement, Settlement>();     // po recenzji: brak odbiorcy - wies, do ktorej zaloga idzie do domu (droga W4c)
            foreach (var s in sites)
            {
                // po recenzji: stary pan z krolestwa docelowego (inny rod tego samego krolestwa) - zaloga zostaje z osada, jak przy nadaniu lenna w grze
                // (ApplyByDefault jej nie rusza); wyprowadzamy tylko zaloge krolestwa, ktore twierdze oddaje
                if (s.MapFaction == newF) continue;
                int men = Men(s.Town.GarrisonParty);
                if (men <= 0) continue;
                var l = Recipients(s, s.MapFaction, sites);
                var hp = HeadParty(s.OwnerClan);
                if (l.Count == 0 && hp == null)
                {
                    // po recenzji (zamowienie c): zadnej partii krolestwa starego pana i glowa rodu bez druzyny - ludzie do domu, a zmiana idzie dalej.
                    // Bez wsi z danymi BK nie ma dokad ich odeslac - osada czeka (zaloga starego pana nie moze przejsc na strone nowego)
                    var hv = ClanBudget.OverflowVillage(s.Town.GarrisonParty);
                    if (hv == null) { wait = "brak odbiorcy zalogi " + Nm(s) + " (" + men + " ludzi) w " + Nm(s.MapFaction) + " i brak wsi z danymi BK do odeslania jej do domu"; return null; }
                    homes[s] = hv;
                    continue;
                }
                recips[s] = l; heads[s] = hp;
            }

            // --- od tej chwili zmiana ---
            var sb = new StringBuilder(1500);
            sb.Append(Prefix).Append("dzien ").Append(day).Append(" | ").Append(Nm(st)).Append(" (").Append(st.StringId).Append(')');
            foreach (var s in sites)
                sb.Append(" | ").Append(Nm(s)).Append(": ").Append(s.OwnerClan.Name).Append(" (").Append(Nm(s.MapFaction)).Append(") -> ").Append(clan.Name).Append(" (").Append(Nm(newF)).Append(')');
            sb.Append(", glowa rodu ").Append(Nm(leader));

            // bohaterowie bez druzyny
            int movedH = 0;
            var hs = new StringBuilder();
            foreach (var kv in heroPlan)
            {
                var h = kv.Key;
                try
                {
                    if (h.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(h);
                    TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, kv.Value);
                    movedH++;
                    if (hs.Length > 0) hs.Append(", ");
                    hs.Append(Nm(h)).Append(" -> ").Append(Nm(kv.Value));
                }
                catch (Exception e) { Stumble("Hero(" + row.Id + ")", e); }
            }
            sb.Append(" | bohaterowie bez druzyny wyprowadzeni ").Append(movedH).Append(movedH > 0 ? " [" + hs + "]" : "");

            // zalogi starego pana
            foreach (var s in sites)
            {
                var g = s.Town.GarrisonParty;
                int n0 = Men(g);
                sb.Append(" | zaloga ").Append(Nm(s)).Append(' ').Append(n0);
                if (n0 <= 0) continue;
                if (s.MapFaction == newF) { sb.Append(" (stary pan z krolestwa docelowego - zostaje z osada)"); continue; }
                List<MobileParty> l; Settlement hv;
                if (recips.TryGetValue(s, out l)) sb.Append(" -> ").Append(MoveGarrison(s, g, l, heads[s], row.Id)).Append(", zostalo ").Append(Men(g));
                else if (homes.TryGetValue(s, out hv)) sb.Append(" -> ").Append(ReleaseHome(g, hv, row.Id)).Append(", zostalo ").Append(Men(g));
            }

            // jency w lochu (zostaja przy osadzie)
            foreach (var s in sites)
            {
                int reg = 0, her = 0;
                try
                {
                    var pr = s.Party != null ? s.Party.PrisonRoster : null;
                    if (pr != null) for (int i = 0; i < pr.Count; i++) { var c = pr.GetCharacterAtIndex(i); if (c == null) continue; if (c.IsHero) her++; else reg += pr.GetElementNumber(i); }
                }
                catch (Exception e) { Stumble("Prison(" + row.Id + ")", e); }
                sb.Append(" | loch ").Append(Nm(s)).Append(": zwykli ").Append(reg).Append(", bohaterowie ").Append(her);
            }
            sb.Append(FreeOn ? " (zwykli zolnierze krolestwa nowego pana - do zalogi zaraz po zmianie pana (f23), reszta zostaje przy osadzie; bohaterow strony nowego pana wypuszcza gra)"
                             : " (zwykli zostaja przy osadzie; bohaterow strony nowego pana wypuszcza gra)");

            // roszczenia Strategic Campaign AI krolestw starych panow - migawka przed zmiana
            var sca = ScaClaims();
            var scaK = new List<Kingdom>(2); var scaHad = new List<bool>(2); var scaPrev = new List<string>(2);
            foreach (var s in sites)
            {
                var k = s.MapFaction as Kingdom;
                if (k == null || scaK.Contains(k)) continue;
                bool had; string prev;
                ScaSnap(sca, k, out had, out prev);
                scaK.Add(k); scaHad.Add(had); scaPrev.Add(prev);
            }

            // (d) zmiana pana: osada (blizniak przenosi ROT), potem kontrola blizniaka
            bool stOk = !stNeeds, twinOk = !twinNeeds;
            if (stNeeds)
            {
                // wlasny try: wyjatek cudzego nasluchu (zdarzenie biegnie po ustawieniu pana) nie moze zgubic krokow po zmianie - o sukcesie mowi pan osady
                try { ChangeOwnerOfSettlementAction.ApplyByDefault(leader, st); }
                catch (Exception e) { Stumble("ApplyByDefault(" + row.Id + ")", e); sb.Append(" | WYJATEK w zdarzeniu zmiany pana (log wyzej)"); }
                stOk = st.OwnerClan == clan;
                if (stOk) _batchDone.Add(st.StringId);
            }
            if (twinNeeds)
            {
                if (twin.MapFaction == newF) sb.Append(" | blizniak ").Append(Nm(twin)).Append(": przeniesiony przez ROT, pan ").Append(twin.OwnerClan != null ? twin.OwnerClan.Name.ToString() : "-");
                else
                {
                    try { ChangeOwnerOfSettlementAction.ApplyByDefault(leader, twin); }
                    catch (Exception e) { Stumble("ApplyByDefault(" + row.Twin + ")", e); sb.Append(" | WYJATEK w zdarzeniu zmiany pana blizniaka (log wyzej)"); }
                    sb.Append(" | blizniak ").Append(Nm(twin)).Append(": ROT nie przeniosl - zmieniony przez nas, pan ").Append(twin.OwnerClan != null ? twin.OwnerClan.Name.ToString() : "-");
                }
                twinOk = twin.MapFaction == newF;
                if (twinOk) _batchDone.Add(twin.StringId);
            }
            for (int i = 0; i < scaK.Count; i++) ScaRestore(sca, scaK[i], scaHad[i], scaPrev[i], sb);

            // (g0) f23-spichlerze: zwykli jency - zolnierze krolestwa nowego pana - z lochu do zalogi; tylko twierdze, ktore zmienily pana w tej probie (ten sam warunek co
            // obsada (g) - nieudana zmiana czeka na ponowna probe), PRZED obsada: cel obsady liczy sie od zalogi z uwolnionymi (mniej ludzi od dawcow). Kazda twierdza we wlasnym try
            if (FreeOn)
                foreach (var s in sites)
                {
                    bool changed = s == st ? stOk : twinOk;
                    if (!changed || s.MapFaction != newF || s.OwnerClan == null) continue;
                    try { sb.Append(" | ").Append(FreeOwnPrisoners(s, row.Id)); }
                    catch (Exception e) { Stumble("Free(" + s.StringId + ")", e); sb.Append(" | jency do zalogi ").Append(Nm(s)).Append(": BLAD (log wyzej)"); }
                }

            // (g) f21-zaloga: obsada zalogi nowego pana ludzmi jego krolestwa - tylko twierdze, ktore zmienily pana w tej probie (stOk / twinOk);
            // zmiana, ktora nie weszla, czeka na ponowna probe (wtedy obsada razem z nia). Kazda twierdza we wlasnym try - potkniecie nie zatrzymuje reszty wiersza.
            // Po recenzji: samo "osada w krolestwie docelowym" nie wystarcza - stary pan z tego samego krolestwa (inny rod) jest w nim juz przed zmiana,
            // wiec nieudane ApplyByDefault obsadzaloby co godzine twierdze starego pana (dawcy liczeni wzgledem starego rodu)
            bool fill = GarrisonOn;
            if (fill)
                foreach (var s in sites)
                {
                    bool changed = s == st ? stOk : twinOk;
                    if (!changed || s.MapFaction != newF || s.OwnerClan == null) continue;
                    try { sb.Append(" | ").Append(FillGarrison(s, row.Id)); }
                    catch (Exception e) { Stumble("Fill(" + s.StringId + ")", e); sb.Append(" | obsada ").Append(Nm(s)).Append(": BLAD (log wyzej)"); }
                }

            foreach (var s in sites)
            {
                bool changed = s == st ? stOk : twinOk;
                sb.Append(" | po zmianie ").Append(Nm(s)).Append(": pan ").Append(s.OwnerClan != null ? s.OwnerClan.Name.ToString() : "-")
                  .Append(", zaloga ").Append(Men(s.Town.GarrisonParty))
                  .Append(!fill ? " (pusta zostaje werbunkowi pana - bez ludzi z niczego)"
                          : changed ? " (obsada z krolestwa nowego pana - przeniesieni, bez ludzi z niczego)"
                          : " (zmiana pana nie weszla - bez obsady, obsada razem z ponowna proba)")
                  .Append(", milicja ").Append((int)s.Militia);
            }

            if (stNeeds && stOk)
            {
                // (e) korekta fabuly
                if (clear)
                {
                    try { sb.Append(" | ").Append(ClearRadius(st, oldF, row.ClearRadius, sites)); }
                    catch (Exception e) { Stumble("ClearRadius(" + row.Id + ")", e); sb.Append(" | korekta fabuly: BLAD (log wyzej)"); }
                }
                // (f) zwrot skarbca - raz na zapis
                if (row.Refund > 0 && _refund.Length == 0)
                {
                    try { sb.Append(" | ").Append(RefundTreasury(row, oldF as Kingdom, clan.Kingdom)); }
                    catch (Exception e) { Stumble("Refund(" + row.Id + ")", e); sb.Append(" | zwrot skarbca: BLAD (log wyzej)"); }
                }
            }
            // recenzja calosci 18/19.10 (tylko pomiar, decyzja Jeffa w CHANGELOG): zaloga nowego pana jest pusta albo obsadzona z krolestwa (g, f21-zaloga), a korekta fabuly (e) jest tylko pod KL i tylko wobec
            // krolestwa starego pana - linia mowi, jakie sily wrogow nowego pana (wszystkie krolestwa w wojnie z nim) i jakie jego wlasne stoja przy bramie po zmianie
            if (st.MapFaction == newF)
            {
                try { sb.Append(" | ").Append(Threat(st, newF, Math.Max(ThreatRadius, row.ClearRadius))); }
                catch (Exception e) { Stumble("Threat(" + row.Id + ")", e); }
            }
            if (!stOk || !twinOk)
            {
                sb.Append(" | ZMIANA NIE WESZLA w calosci (").Append(Nm(st)).Append(" pan ").Append(st.OwnerClan != null ? st.OwnerClan.Name.ToString() : "-");
                if (twin != null) sb.Append(", ").Append(Nm(twin)).Append(" pan ").Append(twin.OwnerClan != null ? twin.OwnerClan.Name.ToString() : "-");
                sb.Append(") - ponowna proba za godzine.");
                Log.Info(sb.ToString());
                wait = "zmiana pana nie weszla w calosci";
                return null;
            }
            sb.Append('.');
            Log.Info(sb.ToString());
            return "ok:" + day.ToString(Inv);
        }

        private static void LogSkip(int day, Row row, Settlement st, string why)
        {
            Log.Info(Prefix + "dzien " + day + " | " + (st != null ? Nm(st) + " (" + row.Id + ")" : row.Id) + " - " + why + "; bez zmian.");
        }

        private static int Men(MobileParty mp)
        {
            if (mp == null || mp.MemberRoster == null) return 0;
            int n = 0;
            var r = mp.MemberRoster;
            for (int i = 0; i < r.Count; i++) { var c = r.GetCharacterAtIndex(i); if (c != null && !c.IsHero) n += r.GetElementNumber(i); }
            return n;
        }

        // ------------------------------------------------------------ (c) bohaterowie
        /// <summary>Ten sam warunek co PrisonerCaptureCampaignBehavior.SettlementHeroCaptureCommonCondition, liczony wobec frakcji NOWEGO pana.</summary>
        private static bool WouldBeCaptured(Hero h, IFaction newF)
        {
            return h != null && h != Hero.MainHero && !h.IsWanderer && !h.IsNotable && h.HeroState != Hero.CharacterStates.Prisoner
                   && h.HeroState != Hero.CharacterStates.Dead && h.MapFaction != null && h.CurrentSettlement != null && h.MapFaction.IsAtWarWith(newF);
        }

        /// <summary>Najblizsza twierdza wlasnej frakcji bohatera (nieoblegana, spoza zmiany); bez niej - najblizsza nie wroga.</summary>
        private static Settlement NearestRefuge(Hero h, Settlement from, List<Settlement> exclude)
        {
            var p = from.GatePosition.ToVec2();
            Settlement own = null, neutral = null; float od = float.MaxValue, nd = float.MaxValue;
            foreach (var s in Settlement.All)
            {
                if (s == null || !s.IsFortification || s.Town == null || exclude.Contains(s) || s.IsUnderSiege || s.MapFaction == null) continue;
                float d = s.GatePosition.ToVec2().Distance(p);
                if (s.MapFaction == h.MapFaction) { if (d < od) { od = d; own = s; } }
                else if (!s.MapFaction.IsAtWarWith(h.MapFaction) && d < nd) { nd = d; neutral = s; }
            }
            return own ?? neutral;
        }

        // ------------------------------------------------------------ (c) zaloga
        private static bool RecipientOk(MobileParty mp)
        {
            return mp != null && mp.IsActive && !mp.IsMainParty && !mp.IsDisbanding && mp.MapEvent == null && mp.SiegeEvent == null && mp.BesiegerCamp == null
                   && !mp.IsCurrentlyAtSea && mp.ActualClan != Clan.PlayerClan && mp.MemberRoster != null && mp.Party != null;
        }

        /// <summary>Partie lordow i zalogi frakcji starego pana (bez miejsc zmiany i bez gracza), od najblizszej osady.</summary>
        private static List<MobileParty> Recipients(Settlement st, IFaction oldF, List<Settlement> sites)
        {
            var p = st.GatePosition.ToVec2();
            var list = new List<KeyValuePair<float, MobileParty>>();
            foreach (var w in oldF.WarPartyComponents)
            {
                var mp = w != null ? w.MobileParty : null;
                if (!RecipientOk(mp) || !mp.IsLordParty) continue;        // partia w osadzie zmiany tez - gra wyprowadzi ja przy zmianie pana, ludzie ida z nia
                list.Add(new KeyValuePair<float, MobileParty>(mp.Position.ToVec2().Distance(p), mp));
            }
            foreach (var s in oldF.Settlements)
            {
                if (s == null || !s.IsFortification || s.Town == null || sites.Contains(s) || s.IsUnderSiege || s.OwnerClan == Clan.PlayerClan) continue;
                var g = s.Town.GarrisonParty;
                if (!RecipientOk(g)) continue;
                list.Add(new KeyValuePair<float, MobileParty>(s.GatePosition.ToVec2().Distance(p), g));
            }
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            var res = new List<MobileParty>(list.Count);
            foreach (var kv in list) res.Add(kv.Value);
            return res;
        }

        /// <summary>Druzyna glowy rodu starego pana (odbiorca reszty ponad wolne miejsca) - tylko gdy glowa ja prowadzi.</summary>
        private static MobileParty HeadParty(Clan oldClan)
        {
            var h = oldClan != null ? oldClan.Leader : null;
            var mp = h != null ? h.PartyBelongedTo : null;
            return RecipientOk(mp) && mp.LeaderHero == h ? mp : null;
        }

        private static int Free(MobileParty mp)
        {
            try { return Math.Max(0, mp.Party.PartySizeLimit - mp.MemberRoster.TotalManCount); } catch { return 0; }
        }

        /// <summary>Szeregowi zalogi do odbiorcow: najpierw wolne miejsca od najblizszego, reszta - druzyna glowy starego rodu, bez niej najblizszy odbiorca
        /// (ponad limit). Ksiega ludzi i sprzet zbrojowni ida z ludzmi. Zwraca opis do linii.</summary>
        private static string MoveGarrison(Settlement s, MobileParty g, List<MobileParty> recips, MobileParty headParty, string rowId)
        {
            var parts = new List<string>();
            int left = Men(g), over = 0;
            foreach (var r in recips)
            {
                if (left <= 0) break;
                int f = Free(r);
                if (f <= 0) continue;
                int m = MoveMen(s, g, r, Math.Min(f, left), rowId);
                if (m > 0) { parts.Add(Nm(r) + (r.IsGarrison ? " (zaloga)" : "") + " " + m); left -= m; }
            }
            if (left > 0)
            {
                var last = headParty ?? (recips.Count > 0 ? recips[0] : null);
                if (last != null)
                {
                    int m = MoveMen(s, g, last, left, rowId);
                    if (m > 0) { parts.Add(Nm(last) + (last.IsGarrison ? " (zaloga)" : "") + " " + m + " ponad limit"); left -= m; over += m; }
                }
            }
            return (parts.Count > 0 ? "[" + string.Join(", ", parts.ToArray()) + "]" : "[nikt]") + (over > 0 ? ", ponad limit razem " + over : "");
        }

        /// <summary>Do `max` szeregowych (ranni proporcjonalnie w kazdym stosie) z zalogi `g` do `to`. Zwraca przeniesionych.</summary>
        private static int MoveMen(Settlement s, MobileParty g, MobileParty to, int max, string rowId)
        {
            if (max <= 0 || g == null || to == null) return 0;
            var fr = g.MemberRoster;
            var moved = new Dictionary<CharacterObject, int>();
            var wounded = new Dictionary<CharacterObject, int>();
            int left = max;
            for (int i = fr.Count - 1; i >= 0 && left > 0; i--)
            {
                var c = fr.GetCharacterAtIndex(i);
                if (c == null || c.IsHero) continue;
                int n = fr.GetElementNumber(i), w = fr.GetElementWoundedNumber(i);
                if (n <= 0) continue;
                int k = Math.Min(n, left);
                int wk = n > 0 ? (int)Math.Round((double)k * w / n) : 0;
                wk = Math.Max(0, Math.Min(wk, Math.Min(w, k)));
                if (k - wk > n - w) wk = k - (n - w);                 // zdrowych nie wiecej niz jest
                int v;
                moved.TryGetValue(c, out v); moved[c] = v + k;
                wounded.TryGetValue(c, out v); wounded[c] = v + wk;
                left -= k;
            }
            int total = 0;
            foreach (var kv in moved)
            {
                int wk; wounded.TryGetValue(kv.Key, out wk);
                fr.AddToCounts(kv.Key, -kv.Value, false, -wk);
                to.MemberRoster.AddToCounts(kv.Key, kv.Value, false, wk);
                total += kv.Value;
            }
            if (total <= 0) return 0;
            try { PeopleBook.Give(to.MemberRoster, PeopleBook.Take(fr, total, PeopleBook.FFromGarrison), to.IsGarrison ? PeopleBook.FToGarrison : PeopleBook.FFromGarrison); }
            catch (Exception e) { Stumble("PeopleBook(" + rowId + ")", e); }
            try { GarrisonArmory.KitsWithMen(g, to, s, moved); }
            catch (Exception e) { Stumble("Kits(" + rowId + ")", e); }
            return total;
        }

        /// <summary>Po recenzji (zamowienie c): brak odbiorcy zalogi (krolestwo starego pana bez partii lordow i zalog, glowa rodu bez druzyny) - szeregowi
        /// do domu ta sama droga co przepelnienie zalogi W4c (OverflowHome.SendHome): wies `village` = ClanBudget.OverflowVillage zalogi, ClanBudget.ToVillagePop
        /// z przyczyna MW4c (ludnosc BK wsi i ksiega ludzi z udzialem pochodzenia - znani pochodzeniem do DOM swojego regionu, weterani E4/10 jak W4c).
        /// Kolejnosc jak ClanBudget.ReleaseFrom: udzialy zdjete przed dopisaniem (Take, wyjscie "rozwiazane bez bitwy do domu"), roster po; wies nie przyjela -
        /// Untake, ludzie zostaja w zalodze (linia "zostalo"). Wight (Inni) nie idzie do ludnosci BK - zostaje (jak w SendHome). Sprzet zostaje w zbrojowni
        /// osady, sakiewki ludzi bez zmian (jak przy przepelnieniu W4c). Zwraca opis do linii.</summary>
        private static string ReleaseHome(MobileParty g, Settlement village, string rowId)
        {
            var r = g.MemberRoster;
            var stacks = new List<TroopRosterElement>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Character != null && !el.Character.IsHero && el.Number > 0) stacks.Add(el);
            }
            int gone = 0, kept = 0;
            foreach (var el in stacks)
            {
                var ch = el.Character;
                int n = el.Number;
                if (Undead.Character(ch)) { kept += n; continue; }
                PeopleBook.Shares org = null;
                try { if (PeopleBook.OriginLive) org = PeopleBook.Take(r, n, PeopleBook.FDisbandHome); }
                catch (Exception e) { Stumble("Home.Take(" + rowId + ")", e); org = null; }
                bool ok = false;
                try { ok = ClanBudget.ToVillagePop(village, ch, n, PeopleBook.MW4c, org, g); }
                catch (Exception e) { Stumble("Home(" + rowId + ")", e); ok = false; }
                if (!ok)
                {
                    try { if (org != null) PeopleBook.Untake(r, org, PeopleBook.FDisbandHome); } catch (Exception e) { Stumble("Home.Untake(" + rowId + ")", e); }
                    kept += n;
                    continue;
                }
                r.AddToCounts(ch, -n, false, -el.WoundedNumber);
                gone += n;
            }
            return "[do domu " + gone + " - wies " + Nm(village) + " (droga przepelnienia W4c)" + (kept > 0 ? ", wies nie przyjela " + kept : "") + "]";
        }

        // ------------------------------------------------------------ (g) f21-zaloga: obsada zalogi nowego pana ludzmi jego krolestwa
        /// <summary>Dawca: partia lorda na ladzie z wodzem; nie gracz, nie rod gracza, nie najemnicy; nie w bitwie, oblezeniu (oblegajacy - BesiegerCamp, broniacy -
        /// osada oblegana), przejsciu lad-morze ani rozwiazaniu. `why` - powod pominiecia do licznikow linii (null = dawca).</summary>
        private static bool DonorOk(MobileParty mp, out string why)
        {
            why = null;
            if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.IsDisbanding || mp.MemberRoster == null || mp.Party == null || mp.LeaderHero == null) { why = "-"; return false; }
            if (mp.IsMainParty || mp.ActualClan == null || mp.ActualClan == Clan.PlayerClan) { why = "gracz"; return false; }
            if (mp.ActualClan.IsUnderMercenaryService) { why = "najemni"; return false; }
            var cs = mp.CurrentSettlement;
            var al = mp.Army != null ? mp.Army.LeaderParty : null;      // partia armii, ktorej wodz walczy albo oblega - tez nie (ludzie przy wodzu)
            if (mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegerCamp != null || mp.BesiegedSettlement != null || (cs != null && (cs.IsUnderSiege || cs.SiegeEvent != null))
                || (al != null && al != mp && (al.MapEvent != null || al.BesiegerCamp != null)))
            { why = "bitwa/oblezenie"; return false; }
            if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) { why = "morze"; return false; }
            return true;
        }

        /// <summary>Szeregowi do przeniesienia (bez bohaterow i wightow).</summary>
        private static int Movable(MobileParty mp)
        {
            if (mp == null || mp.MemberRoster == null) return 0;
            int n = 0;
            var r = mp.MemberRoster;
            for (int i = 0; i < r.Count; i++) { var c = r.GetCharacterAtIndex(i); if (c != null && !c.IsHero && !Undead.Character(c)) n += r.GetElementNumber(i); }
            return n;
        }

        /// <summary>Ile dawca moze oddac: szeregowi teraz minus to, co zostaje (>= 50% i >= 20 wzgledem liczby z pierwszego uzycia w sesji - kolejne twierdze
        /// tego samego przywrocenia nie tna go po polowie od nowa), nie wiecej niz do przeniesienia.</summary>
        private static int Spare(MobileParty mp)
        {
            int cur = Men(mp);
            int b;
            if (!_donorBase.TryGetValue(mp, out b)) { b = cur; _donorBase[mp] = cur; }
            int keep = Math.Max(DonorKeepMin, (int)Math.Ceiling(b * DonorKeepShare));
            return Math.Max(0, Math.Min(cur - keep, Movable(mp)));
        }

        /// <summary>Lista dawcow: partie rodu nowego pana (od najblizszej bramy), potem pozostale partie lordow jego krolestwa (od najblizszej). Liczniki pominietych.</summary>
        private static List<MobileParty> Donors(Settlement s, Clan owner, Kingdom k, Dictionary<string, int> skipped)
        {
            var p = s.GatePosition.ToVec2();
            var own = new List<KeyValuePair<float, MobileParty>>();
            var rest = new List<KeyValuePair<float, MobileParty>>();
            var seen = new HashSet<MobileParty>();
            IEnumerable<WarPartyComponent> src = k != null ? (IEnumerable<WarPartyComponent>)k.WarPartyComponents : owner.WarPartyComponents;
            foreach (var w in src)
            {
                var mp = w != null ? w.MobileParty : null;
                if (mp == null || !seen.Add(mp)) continue;
                string why;
                if (!DonorOk(mp, out why))
                {
                    if (why != "-") { int v; skipped.TryGetValue(why, out v); skipped[why] = v + 1; }
                    continue;
                }
                var kv = new KeyValuePair<float, MobileParty>(mp.Position.ToVec2().Distance(p), mp);
                if (mp.ActualClan == owner) own.Add(kv); else rest.Add(kv);
            }
            own.Sort((a, b) => a.Key.CompareTo(b.Key));
            rest.Sort((a, b) => a.Key.CompareTo(b.Key));
            var res = new List<MobileParty>(own.Count + rest.Count);
            foreach (var kv in own) res.Add(kv.Value);
            foreach (var kv in rest) res.Add(kv.Value);
            return res;
        }

        /// <summary>Obsada twierdzy `s` (pan juz zmieniony): szeregowi dawcow do zalogi az do celu (miasto 400, zamek 200, nie ponad limit zalogi gry ani cel zalogi wedlug gry). Zwraca opis do linii.</summary>
        private static string FillGarrison(Settlement s, string rowId)
        {
            var owner = s.OwnerClan;
            var k = owner.Kingdom;
            var town = s.Town;
            var g = town.GarrisonParty;
            int n0 = Men(g);
            int cap = s.IsTown ? GarTownTarget : GarCastleTarget;
            var sb = new StringBuilder(400);
            sb.Append("obsada ").Append(Nm(s)).Append(" (f21-zaloga, ").Append(k != null ? Nm(k) : owner.Name.ToString()).Append("): cel ").Append(cap);
            var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
            var donors = Donors(s, owner, k, skipped);
            int avail = 0;
            foreach (var d in donors) avail += Spare(d);
            if (g == null && avail > 0)
            {
                // zaloga bez partii (ROT/gra jej nie maja) - ta sama droga, ktora gra tworzy ja w LeaveTroopsToGarrison
                s.AddGarrisonParty();
                g = town.GarrisonParty;
                if (g != null) sb.Append(", partia zalogi utworzona (AddGarrisonParty)");
            }
            if (g == null || g.MemberRoster == null || g.Party == null)
            {
                sb.Append(", zaloga ").Append(n0).Append(" - ").Append(avail > 0 ? "brak partii zalogi, bez obsady" : "brak partii zalogi i wolnych ludzi u dawcow");
                return sb.ToString();
            }
            int lim = g.Party.PartySizeLimit;
            int game = GameGarrisonTarget(s);
            int target = Math.Max(0, Math.Min(cap, lim));
            if (game >= 0) target = Math.Min(target, game);
            sb.Append(", limit zalogi gry ").Append(lim).Append(", cel zalogi wedlug gry ")
              .Append(game >= 0 ? game.ToString(Inv) + " (zywnosc, sila krolestwa, kiesy rodow; widelki gry " + (s.IsTown ? "125-750" : "75-500") + ")" : "brak (metoda gry nieczytelna - tylko limit)")
              .Append(" -> cel ").Append(target);
            int need = Math.Max(0, target - g.MemberRoster.TotalManCount);
            var parts = new List<string>();
            int got = 0, usedDonors = 0;
            foreach (var d in donors)
            {
                if (need <= 0) break;
                int sp = Spare(d);
                if (sp <= 0) continue;
                int before = Men(d);
                int m = MoveToGarrison(s, d, g, Math.Min(sp, need), rowId);
                if (m <= 0) continue;
                need -= m; got += m; usedDonors++;
                parts.Add(Nm(d) + " (" + (d.ActualClan == owner ? "rod pana" : d.ActualClan.Name.ToString()) + ", odl. " + d.Position.ToVec2().Distance(s.GatePosition.ToVec2()).ToString("0", Inv)
                          + ") " + m + " z " + before);
            }
            sb.Append(", zaloga przed ").Append(n0).Append(" -> po ").Append(Men(g)).Append(" (przeniesieni ").Append(got).Append(" od ").Append(usedDonors).Append(" partii");
            sb.Append(parts.Count > 0 ? ": " + string.Join("; ", parts.ToArray()) : "").Append(')');
            if (need > 0)
                sb.Append(", BRAK ").Append(need).Append(" do celu (dawcow ").Append(donors.Count).Append(", wolnych ludzi u nich bylo ").Append(avail)
                  .Append(" - kazdy zostawia >= ").Append((int)(DonorKeepShare * 100)).Append("% i >= ").Append(DonorKeepMin).Append(')');
            if (skipped.Count > 0)
            {
                var sk = new List<string>();
                foreach (var kv in skipped) sk.Add(kv.Key + " " + kv.Value);
                sb.Append(", pominiete partie: ").Append(string.Join(", ", sk.ToArray()));
            }
            sb.Append(", milicja ").Append((int)s.Militia).Append(", zywnosc ").Append((int)town.FoodStocks);
            return sb.ToString();
        }

        private static bool _gtTried;
        private static MethodInfo _gtIdeal;

        /// <summary>Cel zalogi wedlug gry: GarrisonTroopsCampaignBehavior.CalculateSettlementGarrisonPartySizeLimitWithFoodAndWage (prywatna, dekompilacja 1.4.8 - min z limitu
        /// zalogi, zalogi, ktora osada wyzywi, i sily krolestwa na twierdze x zamoznosc rodow krolestwa x dobrobyt x wsie) w widelkach, ktorych gra uzywa, gdy lord zostawia
        /// ludzi (CollectPartyGarrisonTransferData: miasto 125-750, zamek 75-500). -1 = brak metody, zachowania albo wyjatek (wtedy sam limit). Nie rzuca.</summary>
        private static int GameGarrisonTarget(Settlement s)
        {
            try
            {
                if (!_gtTried)
                {
                    _gtTried = true;
                    _gtIdeal = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.GarrisonTroopsCampaignBehavior).GetMethod("CalculateSettlementGarrisonPartySizeLimitWithFoodAndWage",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly, null, new[] { typeof(Settlement) }, null);
                }
                var c = Campaign.Current;
                var beh = c != null ? c.GetCampaignBehavior<TaleWorlds.CampaignSystem.CampaignBehaviors.GarrisonTroopsCampaignBehavior>() : null;
                if (_gtIdeal == null || beh == null) return -1;
                object r = _gtIdeal.Invoke(beh, new object[] { s });
                if (!(r is int)) return -1;
                int lo = s.IsTown ? 125 : 75, hi = s.IsTown ? 750 : 500;
                return Math.Max(lo, Math.Min(hi, (int)r));
            }
            catch (Exception e) { Stumble("GameGarrisonTarget", e); return -1; }
        }

        /// <summary>Do `max` szeregowych z partii `from` do zalogi `g` - z kazdego stosu proporcjonalnie (sklad dawcy zostaje), ranni proporcjonalnie w stosie;
        /// wight nie idzie. Ksiega ludzi (FToGarrison, jak latka LeaveTroopsToGarrison) i sprzet (KitsToGarrison - jak LeavePostfix) ida z ludzmi. Zwraca przeniesionych.</summary>
        private static int MoveToGarrison(Settlement s, MobileParty from, MobileParty g, int max, string rowId)
        {
            if (max <= 0 || from == null || g == null) return 0;
            var fr = from.MemberRoster;
            var chars = new List<CharacterObject>();
            var num = new List<int>();
            var wnd = new List<int>();
            int tot = 0;
            for (int i = 0; i < fr.Count; i++)
            {
                var el = fr.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || c.IsHero || el.Number <= 0 || Undead.Character(c)) continue;
                chars.Add(c); num.Add(el.Number); wnd.Add(Math.Max(0, Math.Min(el.WoundedNumber, el.Number))); tot += el.Number;
            }
            if (tot <= 0) return 0;
            int want = Math.Min(max, tot);
            var take = new int[chars.Count];
            var frac = new double[chars.Count];
            int sum = 0;
            for (int j = 0; j < chars.Count; j++)
            {
                double x = (double)num[j] * want / tot;
                take[j] = Math.Min(num[j], (int)Math.Floor(x));
                frac[j] = x - take[j];
                sum += take[j];
            }
            for (int rest = want - sum; rest > 0; rest--)                       // reszta po najwiekszych ulamkach
            {
                int best = -1; double bf = -1.0;
                for (int j = 0; j < chars.Count; j++) if (take[j] < num[j] && frac[j] > bf) { bf = frac[j]; best = j; }
                if (best < 0) break;
                take[best]++; frac[best] = -1.0;
            }
            var moved = new Dictionary<CharacterObject, int>();
            int total = 0;
            for (int j = 0; j < chars.Count; j++)
            {
                int k = take[j];
                if (k <= 0) continue;
                int n = num[j], w = wnd[j];
                int wk = (int)Math.Round((double)k * w / n);
                wk = Math.Max(0, Math.Min(wk, Math.Min(w, k)));
                if (k - wk > n - w) wk = k - (n - w);                         // zdrowych nie wiecej niz jest
                fr.AddToCounts(chars[j], -k, false, -wk);
                g.MemberRoster.AddToCounts(chars[j], k, false, wk);
                int v; moved.TryGetValue(chars[j], out v); moved[chars[j]] = v + k;
                total += k;
            }
            if (total <= 0) return 0;
            try { PeopleBook.Give(g.MemberRoster, PeopleBook.Take(fr, total, PeopleBook.FToGarrison), PeopleBook.FToGarrison); }
            catch (Exception e) { Stumble("Fill.PeopleBook(" + rowId + ")", e); }
            try { GarrisonArmory.KitsToGarrison(from, g, moved); }
            catch (Exception e) { Stumble("Fill.Kits(" + rowId + ")", e); }
            return total;
        }

        // ------------------------------------------------------------ (g0) f23-spichlerze: jency - zolnierze krolestwa nowego pana - z lochu do zalogi
        /// <summary>Kultury krolestwa nowego pana: kultura krolestwa, rodu nowego pana i kazdego rodu krolestwa (bez wyeliminowanych i najemnikow) - drzewa oddzialow,
        /// z ktorych lordowie krolestwa werbuja. Bez kultur bandytow (IsBandit).</summary>
        private static List<CultureObject> RealmCultures(Clan owner)
        {
            var res = new List<CultureObject>(6);
            Action<CultureObject> add = c => { if (c != null && !c.IsBandit && !res.Contains(c)) res.Add(c); };
            add(owner.Culture);
            var k = owner.Kingdom;
            if (k != null)
            {
                add(k.Culture);
                foreach (var c in k.Clans)
                    if (c != null && !c.IsEliminated && !c.IsUnderMercenaryService) add(c.Culture);
            }
            return res;
        }

        private static string CultureId(CultureObject c) { try { return c != null ? c.StringId : "-"; } catch { return "?"; } }

        private static int Regulars(TroopRoster r)
        {
            if (r == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++) { var c = r.GetCharacterAtIndex(i); if (c != null && !c.IsHero) n += r.GetElementNumber(i); }
            return n;
        }

        /// <summary>Twierdza `s` (pan juz zmieniony): zwykli jency, ktorzy sa zolnierzami krolestwa nowego pana (kultura z RealmCultures; bez bandytow, wightow
        /// i bohaterow), z lochu osady (i lochu partii zalogi) do zalogi - cale stosy z rannymi, ksiega ludzi FLooted (Take z lochu, Give do zalogi). Reszta zostaje.
        /// Zwraca opis do linii (kontrola: zdjete z lochu - dodane do zalogi = 0).</summary>
        private static string FreeOwnPrisoners(Settlement s, string rowId)
        {
            var owner = s.OwnerClan;
            var town = s.Town;
            var cult = RealmCultures(owner);
            var sb = new StringBuilder(500);
            sb.Append("jency do zalogi ").Append(Nm(s)).Append(" (f23, kultury krolestwa nowego pana: ");
            for (int i = 0; i < cult.Count; i++) sb.Append(i > 0 ? ", " : "").Append(CultureId(cult[i]));
            sb.Append(cult.Count == 0 ? "brak" : "").Append("): ");

            // rostery: loch osady (tam gra trzyma jencow osady) i loch partii zalogi, gdyby cos w nim bylo
            var rosters = new List<TroopRoster>(2);
            if (s.Party != null && s.Party.PrisonRoster != null) rosters.Add(s.Party.PrisonRoster);
            var g = town.GarrisonParty;
            if (g != null && g.PrisonRoster != null && !rosters.Contains(g.PrisonRoster)) rosters.Add(g.PrisonRoster);

            // plan - nic jeszcze nie rusza
            var plan = new List<KeyValuePair<TroopRoster, TroopRosterElement>>();
            int own = 0, ownW = 0, foreign = 0, bandits = 0, wights = 0, reg0 = 0;
            foreach (var pr in rosters)
            {
                reg0 += Regulars(pr);
                for (int i = 0; i < pr.Count; i++)
                {
                    var el = pr.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero || el.Number <= 0) continue;
                    if (Undead.Character(c)) { wights += el.Number; continue; }
                    var cc = c.Culture;
                    if (cc == null || cc.IsBandit) { bandits += el.Number; continue; }
                    if (!cult.Contains(cc)) { foreign += el.Number; continue; }
                    plan.Add(new KeyValuePair<TroopRoster, TroopRosterElement>(pr, el));
                    own += el.Number; ownW += Math.Max(0, Math.Min(el.WoundedNumber, el.Number));
                }
            }
            string stay = "obcy " + foreign + ", bandyci " + bandits + ", Inni " + wights;
            if (own <= 0)
            {
                sb.Append("brak zolnierzy krolestwa nowego pana w lochu (zwykli ").Append(reg0).Append(" zostaja: ").Append(stay).Append(')');
                return sb.ToString();
            }
            if (g == null)
            {
                // zaloga bez partii (po ApplyByDefault zawsze jest - zabezpieczenie, ta sama droga co FillGarrison)
                s.AddGarrisonParty();
                g = town.GarrisonParty;
                if (g != null) sb.Append("partia zalogi utworzona (AddGarrisonParty); ");
            }
            if (g == null || g.MemberRoster == null || g.Party == null)
            {
                sb.Append("brak partii zalogi - ").Append(own).Append(" zolnierzy krolestwa zostaje w lochu (zwykli ").Append(reg0).Append(", poza nimi ").Append(stay).Append(')');
                return sb.ToString();
            }

            int g0 = Men(g);
            var movedBy = new Dictionary<TroopRoster, int>();
            var types = new Dictionary<CharacterObject, int>();
            int moved = 0, movedW = 0;
            foreach (var kv in plan)
            {
                var pr = kv.Key; var el = kv.Value; var c = el.Character;
                int n = el.Number, w = Math.Max(0, Math.Min(el.WoundedNumber, el.Number));
                pr.AddToCounts(c, -n, false, -w);
                g.MemberRoster.AddToCounts(c, n, false, w);
                int v; movedBy.TryGetValue(pr, out v); movedBy[pr] = v + n;
                types.TryGetValue(c, out v); types[c] = v + n;
                moved += n; movedW += w;
            }
            // ksiega ludzi: udzialy pochodzenia z lochu do zalogi (te same ludzie; przyczyna "odbici jency" - wejscie i wyjscie tej samej przenosiny)
            foreach (var kv in movedBy)
            {
                try { PeopleBook.Give(g.MemberRoster, PeopleBook.Take(kv.Key, kv.Value, PeopleBook.FLooted), PeopleBook.FLooted); }
                catch (Exception e) { Stumble("Free.PeopleBook(" + rowId + ")", e); }
            }
            int g1 = Men(g), reg1 = 0;
            foreach (var pr in rosters) reg1 += Regulars(pr);
            int outD = reg0 - reg1, inD = g1 - g0;

            var tl = new List<KeyValuePair<CharacterObject, int>>(types);
            tl.Sort((a, b) => b.Value.CompareTo(a.Value));
            var tp = new List<string>();
            for (int i = 0; i < tl.Count && i < 6; i++) tp.Add(Nm(tl[i].Key) + " " + tl[i].Value);
            if (tl.Count > 6) tp.Add("i " + (tl.Count - 6) + " innych typow");
            sb.Append("wypuszczeni do zalogi ").Append(moved).Append(" (ranni ").Append(movedW).Append(") [").Append(string.Join(", ", tp.ToArray())).Append(']')
              .Append(", zaloga ").Append(g0).Append(" -> ").Append(g1).Append(", loch zwykli ").Append(reg0).Append(" -> ").Append(reg1).Append(" (zostaja: ").Append(stay).Append(')')
              .Append("; kontrola jencow do zalogi: zdjete z lochu ").Append(outD).Append(" - dodane do zalogi ").Append(inD).Append(" = ").Append(outD - inD)
              .Append(", bez sprzetu (zabrany przy pojmaniu - zbrojownia zalogi dokupi braki)");
            int lim = g.Party.PartySizeLimit, all = g.MemberRoster.TotalManCount;
            if (all > lim) sb.Append(", ponad limit zalogi gry ").Append(lim).Append(" o ").Append(all - lim).Append(" (przepelnienie W4c odesle nadmiar do domu)");
            return sb.ToString();
        }

        private static string Nm(CharacterObject c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ (e) korekta fabuly: partie starego krolestwa przy osadzie
        private static bool LandParty(MobileParty mp) { return mp != null && mp.IsActive && !mp.IsMainParty && !mp.IsDisbanding; }

        /// <summary>Grupa do przeniesienia: wodz (pierwszy na liscie) i reszta. Armia z wodzem w promieniu - cala (wszystkie partie armii, takze idace do niej);
        /// partia armii z wodzem poza promieniem, nieprzypieta - sama (idzie dalej do swojej armii).</summary>
        private sealed class Group { internal MobileParty Lead; internal bool WholeArmy; internal readonly List<MobileParty> All = new List<MobileParty>(); }

        /// <summary>Grupy partii lordow frakcji `f` w promieniu `r` od bramy. `busy` - ktos z nich w bitwie albo oblezeniu (wtedy osada czeka).</summary>
        private static List<Group> RadiusGroups(Settlement st, IFaction f, float r, bool stopOnBusy, out string busy)
        {
            busy = null;
            var p = st.GatePosition.ToVec2();
            var groups = new List<Group>();
            var seenArmies = new HashSet<Army>();
            foreach (var w in f.WarPartyComponents)
            {
                var mp = w != null ? w.MobileParty : null;
                if (!LandParty(mp) || !mp.IsLordParty) continue;
                if (mp.Position.ToVec2().Distance(p) > r) continue;
                var grp = new Group();
                var army = mp.Army;
                var lead = army != null ? army.LeaderParty : null;
                if (army != null && lead != null && lead != mp && lead.Position.ToVec2().Distance(p) > r)
                {
                    if (mp.AttachedTo != null) continue;                  // przypieta stoi przy wodzu - tu nie trafi
                    grp.Lead = mp; grp.All.Add(mp);
                }
                else if (army != null && lead != null)
                {
                    if (!seenArmies.Add(army)) continue;
                    if (lead.IsMainParty) continue;                       // armia gracza - nie ruszamy (gracz w armii i tak czeka)
                    grp.Lead = lead; grp.WholeArmy = true;
                    if (LandParty(lead)) grp.All.Add(lead);
                    foreach (var a in army.Parties) if (a != lead && LandParty(a)) grp.All.Add(a);
                }
                else
                {
                    if (mp.ActualClan == Clan.PlayerClan) continue;           // samodzielna partia rodu gracza - nie ruszamy (w armii AI idzie z armia)
                    grp.Lead = mp; grp.All.Add(mp);
                }
                bool b = false;
                foreach (var a in grp.All)
                    if (a.MapEvent != null || a.SiegeEvent != null || a.BesiegerCamp != null || a.BesiegedSettlement != null)
                    {
                        b = true;
                        if (busy == null) busy = "partia " + Nm(a) + " (" + Nm(f) + ") przy " + Nm(st) + " w bitwie albo oblezeniu";
                        if (stopOnBusy) return groups;
                        break;
                    }
                if (!b && grp.All.Count > 0) groups.Add(grp);
            }
            return groups;
        }

        /// <summary>Najblizsza twierdza frakcji `f` poza promieniem korekty (nieoblegana, bez bitwy, spoza zmiany).</summary>
        private static Settlement Fallback(Vec2 from, Vec2 center, IFaction f, float r, List<Settlement> sites)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var s in f.Settlements)
            {
                if (s == null || !s.IsFortification || s.Town == null || sites.Contains(s) || s.IsUnderSiege || (s.Party != null && s.Party.MapEvent != null)) continue;
                var gp = s.GatePosition.ToVec2();
                if (gp.Distance(center) <= r) continue;
                float d = gp.Distance(from);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        private static void Teleport(MobileParty mp, Settlement dest)
        {
            if (mp.CurrentSettlement != null && (mp.Army == null || mp.Army.LeaderParty == mp || mp.AttachedTo == null)) LeaveSettlementAction.ApplyForParty(mp);
            if (mp.IsTransitionInProgress) mp.CancelNavigationTransition();
            mp.Position = dest.GatePosition;
        }

        /// <summary>Po zmianie pana: grupy z promienia (liczone od nowa - gra wyprowadzila partie z osady) do najblizszej wlasnej twierdzy poza promieniem.</summary>
        private static string ClearRadius(Settlement st, IFaction f, float r, List<Settlement> sites)
        {
            string busy;
            var groups = RadiusGroups(st, f, r, false, out busy);                 // po zmianie: grupa w bitwie zostaje, reszta idzie
            var center = st.GatePosition.ToVec2();
            int nGroups = 0, nParties = 0, nMen = 0, nSea = 0, nNoDest = 0;
            var names = new List<string>();
            var dests = new List<string>();
            foreach (var grp in groups)
            {
                var lead = grp.Lead;
                if (lead == null || lead.IsCurrentlyAtSea) { nSea += grp.All.Count; continue; }
                var dest = Fallback(lead.Position.ToVec2(), center, f, r, sites);
                if (dest == null) { nNoDest += grp.All.Count; continue; }
                int men = 0, moved = 0;
                foreach (var a in grp.All)                                // wodz pierwszy: jego wyjscie z osady zabiera przypietych
                {
                    if (a.IsCurrentlyAtSea) { nSea++; continue; }
                    try { Teleport(a, dest); moved++; men += a.MemberRoster != null ? a.MemberRoster.TotalManCount : 0; }
                    catch (Exception e) { Stumble("Teleport", e); }
                }
                if (moved == 0) continue;
                if (lead.Army == null || lead.Army.LeaderParty == lead)     // partia idaca do swojej armii - jej cel zostaje (armia)
                    try { lead.SetMoveGoToSettlement(dest, MobileParty.NavigationType.Default, false); } catch (Exception e) { Stumble("Teleport.Go", e); }
                nGroups++; nParties += moved; nMen += men;
                names.Add((grp.WholeArmy ? "armia " : "") + Nm(lead) + " (" + moved + " partii, " + men + " ludzi) -> " + Nm(dest));
                if (!dests.Contains(Nm(dest))) dests.Add(Nm(dest));
            }
            if (nParties > 0)
                _batchMoved.Add("The forces of " + Nm(f) + " near " + Nm(st) + " fall back to " + Join(dests));
            return "korekta fabuly (promien " + r.ToString("0", Inv) + "): przeniesione grupy " + nGroups + ", partie " + nParties + ", ludzie " + nMen
                   + (names.Count > 0 ? " [" + string.Join("; ", names.ToArray()) + "]" : "") + (nSea > 0 ? ", na morzu pominiete " + nSea : "")
                   + (nNoDest > 0 ? ", bez twierdzy poza promieniem " + nNoDest : "") + (busy != null ? ", zostaly (bitwa albo oblezenie po zmianie): " + busy : "");
        }

        // ------------------------------------------------------------ pomiar: sily przy bramie po zmianie (recenzja calosci 18/19.10, tylko log)
        /// <summary>Partie lordow na ladzie w promieniu `r` od bramy: krolestw w wojnie z nowym panem (wrogowie - ryzyko odbicia twierdzy z pusta zaloga) i krolestwa
        /// nowego pana (wlasne); ludzie zdrowi. Najwieksza wroga grupa: armia (partie tej samej armii w promieniu razem) albo samodzielna partia. Nic nie zmienia.</summary>
        private static string Threat(Settlement st, IFaction newF, float r)
        {
            var p = st.GatePosition.ToVec2();
            int eP = 0, eM = 0, oP = 0, oM = 0;
            var groups = new Dictionary<object, int>();
            var names = new Dictionary<object, string>();
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated) continue;
                bool own = (IFaction)k == newF;
                if (!own && !k.IsAtWarWith(newF)) continue;
                foreach (var w in k.WarPartyComponents)
                {
                    var mp = w != null ? w.MobileParty : null;
                    if (!LandParty(mp) || !mp.IsLordParty || mp.IsCurrentlyAtSea) continue;
                    if (mp.Position.ToVec2().Distance(p) > r) continue;
                    int men = mp.MemberRoster != null ? mp.MemberRoster.TotalHealthyCount : 0;
                    if (own) { oP++; oM += men; continue; }
                    eP++; eM += men;
                    object key = mp.Army != null ? (object)mp.Army : mp;
                    int g0; groups.TryGetValue(key, out g0); groups[key] = g0 + men;
                    if (!names.ContainsKey(key))
                        names[key] = (mp.Army != null ? "armia " + Nm(mp.Army.LeaderParty) : Nm(mp)) + " (" + Nm(k) + ")";
                }
            }
            string big = "-"; int bigM = -1;
            foreach (var kv in groups) if (kv.Value > bigM) { bigM = kv.Value; big = names[kv.Key] + " " + kv.Value + " ludzi"; }
            return "sily w promieniu " + r.ToString("0", Inv) + " po zmianie (tylko pomiar - ryzyko odbicia twierdzy): wrogowie " + Nm(newF) + " " + eP + " partii lordow, "
                   + eM + " ludzi zdrowych (najwieksza grupa: " + big + "), wlasne partie " + Nm(newF) + " " + oP + ", " + oM + " ludzi";
        }

        // ------------------------------------------------------------ (f) zwrot skarbca
        private static string RefundTreasury(Row row, Kingdom from, Kingdom to)
        {
            if (from == null || to == null || from == to) { _refund = "none:brak-krolestw"; return "zwrot skarbca: nie dotyczy (krolestwo starego albo nowego pana nieczytelne)"; }
            if (from.StringId != row.RefundFrom) { _refund = "none:" + from.StringId; return "zwrot skarbca: nie dotyczy (stary pan z " + from.StringId + ", nie " + row.RefundFrom + ")"; }
            long w0 = from.KingdomBudgetWallet, v0 = to.KingdomBudgetWallet;
            long take = Math.Max(0, Math.Min(row.Refund, w0));
            take = Math.Min(take, Math.Max(0, (long)int.MaxValue - Math.Max(0, v0)));   // skarbiec odbiorcy to int
            long moved = 0, got = 0;
            string e1 = "";
            if (take > 0)
            {
                from.KingdomBudgetWallet -= (int)take;                 // najpierw platnik, potem odbiorca: odbiorca dostaje dokladnie to, co zeszlo
                moved = w0 - from.KingdomBudgetWallet;
                to.KingdomBudgetWallet += (int)moved;
                got = (long)to.KingdomBudgetWallet - v0;
                CrownIncome.ShiftSnapshot(to, got);                    // 165: zwrot skarbu, nie wplyw dnia
                CrownIncome.ShiftSnapshot(from, -moved);               // 165: ubytek skarbu, nie "ujemny wplyw"
                // po recenzji: E1 CrashScribe (PovertyPeace) liczy ubytek z 7 dob historii skarbca i migawki 165 nie czyta - ta sama zasada co 165: przeniesienie
                // skarbu, nie ubytek ani wplyw doby (inaczej Polnoc przez 7 dob "w biedzie": ubytek ok. 160 tys./d, zapas ponizej 28 dob)
                e1 = ", historia skarbca E1 (CrashScribe): " + E1Text(E1Shift(to, got), Nm(to)) + ", " + E1Text(E1Shift(from, -moved), Nm(from));
            }
            _refund = "done:" + moved.ToString(Inv);
            if (moved > 0) { _batchRefund = moved; _batchRefundText = Nm(from) + " pays back " + moved.ToString("N0", Inv) + " denars to the royal treasury of " + Nm(to); }
            return "zwrot skarbca (L5b-1, dzien 109046): nalezne " + row.Refund + ", z " + Nm(from) + " (" + from.StringId + ") " + w0 + " -> " + from.KingdomBudgetWallet
                   + " do " + Nm(to) + " (" + to.StringId + ") " + v0 + " -> " + to.KingdomBudgetWallet + " | zdjete " + moved + " - dopisane " + got + " = " + (moved - got)
                   + (moved < row.Refund ? " (skarbiec platnika nie starczyl - oddane tyle, ile bylo)" : "") + ", migawka 165 przesunieta u obu stron" + e1;
        }

        private static bool _e1Tried;
        private static MethodInfo _e1Shift;

        /// <summary>Po recenzji: CrashScribe PovertyPeace.ShiftHistory(Kingdom, long) - cala historia skarbca krolestwa w mierze biedy E1 przesunieta o delta
        /// (refleksja, jak inne wywolania CrashScribe z Armoury). Zwraca liczbe przesunietych dob; -1 - E1 bez stanu krolestwa (nastepny pomiar zacznie od skarbca
        /// po przelewie), -2 - brak metody (CrashScribe nie wczytany albo starsza DLL), -3 - wyjatek. Nie rzuca.</summary>
        private static int E1Shift(Kingdom k, long delta)
        {
            try
            {
                if (!_e1Tried)
                {
                    _e1Tried = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "CrashScribe") continue;
                        var t = asm.GetType("CrashScribe.PovertyPeace", false);
                        _e1Shift = t != null ? t.GetMethod("ShiftHistory", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(Kingdom), typeof(long) }, null) : null;
                        break;
                    }
                }
                if (_e1Shift == null) return -2;
                object r = _e1Shift.Invoke(null, new object[] { k, delta });
                return r is int ? (int)r : -3;
            }
            catch (Exception e) { Stumble("E1Shift", e); return -3; }
        }

        private static string E1Text(int r, string who)
        {
            return who + (r >= 0 ? " przesunieta (dob " + r + ")" : r == -1 ? " bez stanu E1 (pomiar od skarbca po przelewie)"
                        : r == -2 ? " NIEPRZESUNIETA (brak CrashScribe PovertyPeace.ShiftHistory - stara DLL?)" : " NIEPRZESUNIETA (blad w logu wyzej)");
        }

        // ------------------------------------------------------------ Strategic Campaign AI: roszczenie krolestwa starego pana
        private static bool _scaTried;
        private static FieldInfo _scaField;

        private static IDictionary ScaClaims()
        {
            try
            {
                if (!_scaTried)
                {
                    _scaTried = true;
                    var t = QuartermasterLaw.FindType("StrategicCampaignAI145.StrategicAiState");
                    _scaField = t != null ? t.GetField("LostSettlementClaimByFaction", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static) : null;
                }
                return _scaField != null ? _scaField.GetValue(null) as IDictionary : null;
            }
            catch (Exception e) { Stumble("ScaClaims", e); return null; }
        }

        private static void ScaSnap(IDictionary d, IFaction f, out bool had, out string prev)
        {
            had = false; prev = null;
            try
            {
                var k = f as Kingdom;
                if (d == null || k == null) return;
                had = d.Contains(k.StringId);
                prev = had ? d[k.StringId] as string : null;
            }
            catch (Exception e) { Stumble("ScaSnap", e); }
        }

        private static void ScaRestore(IDictionary d, IFaction f, bool had, string prev, StringBuilder sb)
        {
            try
            {
                var k = f as Kingdom;
                if (k == null) return;
                if (d == null) { sb.Append(" | Strategic Campaign AI: brak (roszczenie nieruszane)"); return; }
                string now = d.Contains(k.StringId) ? d[k.StringId] as string : null;
                if (had) d[k.StringId] = prev; else if (d.Contains(k.StringId)) d.Remove(k.StringId);
                sb.Append(" | roszczenie Strategic Campaign AI ").Append(Nm(k)).Append(": ").Append(now ?? "-").Append(" -> ").Append(had ? (prev ?? "-") : "brak").Append(" (jak przed zmiana)");
            }
            catch (Exception e) { Stumble("ScaRestore", e); }
        }

        // ------------------------------------------------------------ (h) wiadomosc dla gracza
        private static string Join(List<string> xs)
        {
            if (xs.Count == 0) return "";
            if (xs.Count == 1) return xs[0];
            return string.Join(", ", xs.GetRange(0, xs.Count - 1).ToArray()) + " and " + xs[xs.Count - 1];
        }

        /// <summary>Po godzinie ze zmianami: "The chronicles are set right: ..." - osady wedlug krolestwa nowego pana, korekta fabuly i zwrot skarbca.</summary>
        private static void Notify()
        {
            try
            {
                if (_batchDone.Count == 0 && _batchRefund <= 0) return;
                // grupy wedlug krolestwa docelowego (kolejnosc tabeli); blizniak (Red Keep) idzie z miastem - nazwy tylko osad z tabeli
                var order = new List<string>();
                var byK = new Dictionary<string, List<Row>>(StringComparer.Ordinal);
                foreach (var row in Rows)
                {
                    if (!_batchDone.Contains(row.Id) && (row.Twin == null || !_batchDone.Contains(row.Twin))) continue;
                    List<Row> l;
                    if (!byK.TryGetValue(row.Kingdom, out l)) { l = new List<Row>(); byK[row.Kingdom] = l; order.Add(row.Kingdom); }
                    l.Add(row);
                }
                var parts = new List<string>();
                foreach (var kid in order)
                {
                    var rows = byK[kid];
                    var names = new List<string>();
                    var clans = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var row in rows)
                    {
                        if (_batchDone.Contains(row.Id)) names.Add(Nm(Settlement.Find(row.Id)));
                        if (row.Twin != null && _batchDone.Contains(row.Twin)) names.Add(Nm(Settlement.Find(row.Twin)));
                        clans.Add(row.Clan);
                    }
                    var k = FindKingdom(kid);
                    Hero ruler = k != null ? k.Leader : null;
                    string owner;
                    if (clans.Count == 1)
                    {
                        var c = FindClan(rows[0].Clan);
                        owner = c != null && ruler != null && c.Leader == ruler ? Nm(ruler) : "House " + (c != null ? c.Name.ToString() : rows[0].Clan);
                    }
                    else owner = ruler != null ? "the bannermen of " + Nm(ruler) : "the lords of " + Nm(k);
                    parts.Add(Join(names) + (names.Count > 1 ? " return to " : " returns to ") + owner);
                }
                var msg = new StringBuilder("The chronicles are set right: ");
                msg.Append(parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "the realms keep their lands").Append('.');
                foreach (var m in _batchMoved) msg.Append(' ').Append(m).Append('.');
                if (_batchRefundText != null) msg.Append(' ').Append(_batchRefundText).Append('.');
                Log.Player(msg.ToString());
                Log.Info(Prefix + "wiadomosc dla gracza: \"" + msg + "\"");
            }
            catch (Exception e) { Stumble("Notify", e); }
        }
    }

    /// <summary>f19-kanon-b: flaga przywrocenia wlosci w zapisie (arm_book_holdings), proba co godzine, nowa kampania = nic do przywracania.</summary>
    internal sealed class BookHoldingsBehavior : CampaignBehaviorBase
    {
        public BookHoldingsBehavior() { BookHoldings.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => BookHoldings.OnNewGame());
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => BookHoldings.SessionStart());
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, BookHoldings.Hourly);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? BookHoldings.Export() : null;
                SaveText.Sync(dataStore, "arm_book_holdings", ref v);
                if (dataStore.IsLoading) BookHoldings.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_book_holdings", e); }
        }
    }
}
