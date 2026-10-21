using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m99-markietani (Jeff 20.10, lista obozowa pkt 7 "miasteczko obozowe z markietankami" i decyzja 14:20 pkt 4 "uslugi, rozrywka i sklep - tylko w duzych
    /// obozach (armia)") - RZAD MARKIETANOW w menu obozu gracza (arm_camp_wait - ZWYKLE menu, nie menu oczekiwania: SwitchToMenu z jego opcji jest bezpieczne,
    /// jak CampDrill "Train the men"). Klucz CampSutlersRow (domyslnie TAK). AI bez zmian - obieg AI liczy Sutlers.cs (E5/A3).
    /// Poprawki po recenzji: (a) CUDZA ARMIA - ta sama opcja w menu oczekiwania gry army_wait (flaga + CampaignEvents.TickEvent przelacza, jak gra; powrot "Back" do
    /// menu stanu gry); (b) PLATNERZE POLOWI - naprawa zuzytego sprzetu ludzi za stawke kowali miasta-zrodla + narzut (material z jego polki przy naprawach z materialem);
    /// (c) zabawa z PULAPEM SutlerRevelMoraleCap (10) i odstepem 24 h; (d) gotowka na skup - limit na DOBE (zapis), nie na otwarcie ekranu; (e) Spread jak Sutlers.One
    /// z udzialem korony DS od czesci wsi i nazwami w ksiedze przeplywow osad (MoneyLedger.Note169).
    ///
    /// KIEDY: oboz gracza poza osada, na ladzie, bez bitwy i spotkania; oboz = kolumna (druzyna gracza, a w armii - wodz i wszystkie doczepione partie, jak CampWalk);
    /// rzad stoi od SutlerRowMinMen (150) ludzi w obozie (zdrowi i ranni). Kramy: 1 na SutlerRowMenPerStall (100) ludzi, najwyzej SutlerRowMaxStalls (12):
    /// 150 ludzi - 1 kram, 400 - 4, 1000 - 10. Kramy wyznaczaja dzienna oferte (cyrulicy, zapas towaru, gotowka na skup).
    /// ZRODLO: najblizsze miasto niebedace w wojnie z graczem (Sutlers.NearestTown - to samo, do ktorego plynie obieg markietanow AI; brak - najblizsze w ogole).
    /// USLUGI (zaplata gracza = obieg markietanow jak Sutlers.One): czesc SutlerTownShare (85%) - kasa tego miasta, z hakiem 75/25 (TownFolk.EarnService KLife);
    /// reszta - kiesa najblizszej niewrogiej wsi w stanie Normal (z danina pana VillageTakings.Split, gdy Village Tribute From Sutlers; wlasna wies rodu gracza bez
    /// daniny); brak wsi - calosc do miasta.
    ///  1. Cyrulicy: opatruja rannych zolnierzy (bez bohaterow) - wraca do sluzby najwyzej SutlerSurgeonPerStall (8) na kram na dobe, najpierw najwyzszy tier;
    ///     cena za czlowieka SutlerSurgeonFee (15) x (tier + 1) - rekrut T1 30, T3 60, T5 90. Natychmiastowy powrot zamiast dni leczenia - bez zmian w modelu
    ///     leczenia gry (zadnej latki).
    ///  1b. Platnerze polowi: zuzyte sztuki ludzi (TroopSelfMend.MenWorn), najwyzej SutlerArmourerPerStall (6) na kram na dobe, najgorsze najpierw.
    ///  2. Zabawa (ale, kosci, minstrele): co 24 h, SutlerRevelPerMan (2) denary na czlowieka druzyny gracza; morale +SutlerRevelMorale (6) przez
    ///     RecentEventsMorale, najwyzej do SutlerRevelMoraleCap (10) - cena w proporcji do dodatku (gra zdejmuje 10% na dobe). Tylko druzyna gracza (to on placi).
    /// SKLEP (ekran handlu gry, wzorzec SideVillageVisit handel koszykiem): koszyk liczony przy kliknieciu z polki miasta-zrodla - jedzenie, piwo, wino, narzedzia,
    /// strzaly i belty; kazdego rodzaju najwyzej SutlerRowStockPerStall (5) x kramy sztuk na dobe (mniej to, co gracz juz kupil dzis; nie wiecej niz na polce).
    /// Cena kupna = cena miasta x (1 + SutlerRowMarkupPercent 30%), skup = cena skupu miasta x (1 - 30%). Gotowka markietanow na skup = min(kasa miasta,
    /// SutlerRowCashPerStall 1500 x kramy NA DOBE - mniej wydane dzis). Po "Done": kupione schodzi z polki miasta (dokladnie roznica koszyka), sprzedane wchodzi na polke. Pieniadz: gracz
    /// zaplacil netto D (zmiana jego kiesy); narzut kupionego M = wartosc kupionego po cenie markietanow - po cenie miasta (w 0..D) idzie obiegiem uslug; reszta
    /// D - M do kasy miasta (towar po cenie miasta; ujemna = miasto placi za skup). Suma = D - nic z niczego. Anulowanie - gra cofa koszyk, nic sie nie zmienia.
    /// Stan (zapis arm_sutlerrow przez SaveText.Sync): doba, godzina zabawy, opatrzeni dzis, kupione dzis wedlug rodzaju, skup dzis, naprawione dzis. Wyjatek w menu - licznik potkniec, menu dziala.
    /// m103-drobne (kolejka Jeffa pkt 7): 4. ODZIEZ (klucz SutlerRowClothing, wymaga ArmyClothingEnabled) - gracz z kiesy kupuje cale sztuki potrzeby odziezy swojej
    /// druzyny (ArmyClothing: skora, sukno, plotno) z polki miasta-zrodla po cenie miasta x (1 + narzut), najwyzej SutlerRowStockPerStall x kramy sztuk rodzaju na dobe
    /// (ten sam licznik dnia co sklep - _boughtToday); towar po cenie miasta do kasy miasta (ArmyClothing.RowSettle: NCloth, MixCloth), narzut obiegiem uslug (Spread);
    /// potrzeba druzyny maleje o kupione sztuki (nie kupi ich drugi raz u markietanow T3-K1 ani przy wyjezdzie z miasta). 5. PRACZKI (klucz SutlerLaundry) - przez
    /// SutlerLaundryDays dob narastanie potrzeby odziezy druzyny gracza (ArmyClothing.OnDailyTickParty) jest mniejsze o SutlerLaundryCutPercent; oplata = praczki
    /// (1 na SutlerLaundryMenPerWasher ludzi, w gore) x doby x dniowka praczki (2/3 Workshop Wage Per Day) obiegiem uslug. Termin praczek - pole 8 zapisu arm_sutlerrow.
    /// </summary>
    internal static class CampSutlers
    {
        internal const string P = "Markietani obozu (m99): ";
        private const string Menu = "arm_camp_sutlers";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CampSutlersRow; } }

        // ------------------------------------------------------------ stan dnia (zapis arm_sutlerrow)
        private static int _day = -1;                 // doba, do ktorej naleza liczniki ponizej
        private static double _lastRevel = NoRevel;   // godzina kampanii ostatniej zabawy (poprawka po recenzji: odstep 24 h zamiast flagi doby kalendarzowej)
        private const double NoRevel = -1e9;
        private static int _treatedToday;
        private static int _soldToday;                // poprawka po recenzji: gotowka markietanow wydana dzis na skup (limit kramow na DOBE, nie na otwarcie ekranu)
        private static int _mendedToday;              // poprawka po recenzji: sztuki naprawione dzis przez platnerzy polowych
        private static readonly Dictionary<string, int> _boughtToday = new Dictionary<string, int>();
        private static bool _openFromArmy;            // opcja w army_wait (menu oczekiwania gry) podnosi flage, TickEvent przelacza (CLAUDE.md 7)
        private const double NoLaundry = -1e9;
        private static double _laundryUntil = NoLaundry;   // m103-drobne: godzina kampanii konca pracy praczek (zapis - pole 8)

        // ------------------------------------------------------------ liczniki doby (linia doby) i potkniecia
        private static int _dVisits, _dTreated, _dRevels, _dTrades, _dPcsBought, _dPcsSold, _dMended, _dArmyVisits;
        private static long _dSurgeonGold, _dRevelGold, _dBuyNet, _dGoodsTown, _dMarkup, _dToTown, _dToFolk, _dToVill, _dToTax, _dPaid, _dMendGold, _dMendMat, _dCrown;
        private static int _dClothe, _dClothePcs, _dLaundry;                   // m103-drobne: odziez z rzedu (zakupy, sztuki), praczki (najecia)
        private static long _dClotheGold, _dClotheTown, _dLaundryGold;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CampSutlers." + where, e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static void Roll()
        {
            int t = Today();
            if (t == _day) return;
            _day = t; _treatedToday = 0; _soldToday = 0; _mendedToday = 0; _boughtToday.Clear();
        }

        private static void Reset()
        {
            _day = -1; _lastRevel = NoRevel; _treatedToday = 0; _soldToday = 0; _mendedToday = 0; _boughtToday.Clear(); _openFromArmy = false;
            _laundryUntil = NoLaundry;   // m103-drobne: praczki nie przechodza do nowej kampanii
            DayZero();
        }

        private static void DayZero()
        {
            _dVisits = _dTreated = _dRevels = _dTrades = _dPcsBought = _dPcsSold = _dMended = _dArmyVisits = 0;
            _dSurgeonGold = _dRevelGold = _dBuyNet = _dGoodsTown = _dMarkup = _dToTown = _dToFolk = _dToVill = _dToTax = _dPaid = _dMendGold = _dMendMat = _dCrown = 0;
            _dClothe = _dClothePcs = _dLaundry = 0; _dClotheGold = _dClotheTown = _dLaundryGold = 0;   // m103-drobne
        }

        private static double HoursSinceRevel() { return _lastRevel <= NoRevel / 2 ? 1e9 : CampaignTime.Now.ToHours - _lastRevel; }

        // ------------------------------------------------------------ zapis: "2|doba|godzina zabawy|opatrzeni|id:n,id:n|skup dzis|naprawione dzis|koniec praczek" (stary "1|doba|zabawa 0/1|..." czytany;
        // m103-drobne: pole 8 - godzina konca praczek; zapis bez pola = bez praczek, stary kod pole pomija)
        internal static string Export()
        {
            var sb = new StringBuilder(64);
            sb.Append("2|").Append(_day.ToString(Inv)).Append('|').Append(_lastRevel.ToString("R", Inv)).Append('|').Append(_treatedToday.ToString(Inv)).Append('|');
            bool first = true;
            foreach (var kv in _boughtToday)
            {
                if (kv.Key == null || kv.Key.IndexOfAny(new[] { '|', ',', ':' }) >= 0) continue;
                if (!first) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
                first = false;
            }
            sb.Append('|').Append(_soldToday.ToString(Inv)).Append('|').Append(_mendedToday.ToString(Inv));
            sb.Append('|').Append(_laundryUntil <= NoLaundry / 2 ? "-" : _laundryUntil.ToString("0.##", Inv));   // m103-drobne
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            Reset();
            try
            {
                if (string.IsNullOrEmpty(s)) return;
                var a = s.Split('|');
                if (a.Length < 5 || (a[0] != "1" && a[0] != "2")) return;
                int v; double h;
                if (int.TryParse(a[1], NumberStyles.Integer, Inv, out v)) _day = v;
                if (a[0] == "1") { if (a[2] == "1" && _day >= 0) _lastRevel = _day * 24.0; }   // stary zapis: zabawa tej doby - liczona od poczatku doby
                else if (double.TryParse(a[2], NumberStyles.Float, Inv, out h)) _lastRevel = h;
                if (int.TryParse(a[3], NumberStyles.Integer, Inv, out v)) _treatedToday = Math.Max(0, v);
                foreach (var e in a[4].Split(','))
                {
                    int c = e.LastIndexOf(':');
                    if (c > 0 && int.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out v) && v > 0) _boughtToday[e.Substring(0, c)] = v;
                }
                if (a.Length >= 7)
                {
                    if (int.TryParse(a[5], NumberStyles.Integer, Inv, out v)) _soldToday = Math.Max(0, v);
                    if (int.TryParse(a[6], NumberStyles.Integer, Inv, out v)) _mendedToday = Math.Max(0, v);
                }
                if (a.Length >= 8 && a[7] != "-" && double.TryParse(a[7], NumberStyles.Float, Inv, out h) && !double.IsNaN(h) && !double.IsInfinity(h)) _laundryUntil = h;   // m103-drobne
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ------------------------------------------------------------ wpiecie (ArmouryBehavior.RegisterEvents - jedna linia)
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(b, delegate { try { Reset(); } catch (Exception e) { Stumble("Reset", e); } });
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { _stumbles = 0; _err.Clear(); DayZero(); if (s != null) AddMenus(s); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
            CampaignEvents.TickEvent.AddNonSerializedListener(b, delegate (float dt) { if (_openFromArmy) { try { ArmyTick(); } catch (Exception e) { _openFromArmy = false; Stumble("ArmyTick", e); } } });
        }

        /// <summary>Gracz doczepiony do CUDZEJ armii (wodz to nie on) - stoi w menu oczekiwania gry army_wait, klawisz O (arm_camp_wait) tam nie dziala.</summary>
        private static bool InForeignArmy()
        {
            var mp = MobileParty.MainParty;
            return mp != null && mp.Army != null && mp.Army.LeaderParty != null && mp.Army.LeaderParty != mp && mp.AttachedTo == mp.Army.LeaderParty;
        }

        /// <summary>Poprawka po recenzji (cudza armia): army_wait to menu OCZEKIWANIA - z jego opcji SwitchToMenu = CTD (CLAUDE.md 7). Opcja tylko podnosi flage,
        /// a przelaczenie robi CampaignEvents.TickEvent - ta sama droga, ktora gra sama zmienia menu z army_wait (PlayerArmyWaitBehavior.OnTick w TickEvent:
        /// StartSiegePreparation; ArmyWaitMenuTick: EndWait + SwitchToMenu). Tylko, gdy wciaz stoimy w army_wait i rzad jest dostepny.</summary>
        private static void ArmyTick()
        {
            _openFromArmy = false;
            var mc = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
            if (mc == null || mc.GameMenu == null || mc.GameMenu.StringId != "army_wait" || !InForeignArmy()) return;
            int men, stalls; string why;
            if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
            mc.GameMenu.EndWait();
            _dVisits++; _dArmyVisits++;
            GameMenu.SwitchToMenu(Menu);
        }

        private static void AddMenus(CampaignGameStarter starter)
        {
            // poprawka po recenzji: wejscie z menu oczekiwania armii gry (gracz w cudzej armii) - flaga + TickEvent (wyzej), bez SwitchToMenu z opcji
            starter.AddGameMenuOption("army_wait", "arm_army_sutlers_row", "{=!}Visit the sutlers' row",
                delegate (MenuCallbackArgs a)
                {
                    try
                    {
                        if (!On || !InForeignArmy()) return false;
                        a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        int men, stalls; string why;
                        if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                        return true;
                    }
                    catch (Exception e) { Stumble("ArmyRowCondition", e); return false; }
                },
                delegate (MenuCallbackArgs a)
                {
                    try
                    {
                        _openFromArmy = true;   // TickEvent staje z pauza - ruszamy zegar jak CampDrill "Train the men" (z opcji menu oczekiwania wolno)
                        if (Campaign.Current.TimeControlMode == CampaignTimeControlMode.Stop) Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
                    }
                    catch (Exception e) { Stumble("ArmyRowOpen", e); }
                }, false);

            starter.AddGameMenuOption("arm_camp_wait", "arm_camp_sutlers_row", "{=!}Visit the sutlers' row",
                delegate (MenuCallbackArgs a)
                {
                    try
                    {
                        if (!On) return false;
                        a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        int men, stalls; string why;
                        if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                        return true;
                    }
                    catch (Exception e) { Stumble("RowCondition", e); return false; }
                },
                delegate (MenuCallbackArgs a) { try { _dVisits++; GameMenu.SwitchToMenu(Menu); } catch (Exception e) { Stumble("RowOpen", e); } }, false, 3);

            starter.AddGameMenu(Menu, "{=!}{ARM_ROW_TEXT}", RowInit);
            starter.AddGameMenuOption(Menu, "arm_row_surgeon", "{=!}{ARM_ROW_SURGEON}", SurgeonCondition, delegate (MenuCallbackArgs a) { Surgeon(); }, false, 0);
            starter.AddGameMenuOption(Menu, "arm_row_revel", "{=!}{ARM_ROW_REVEL}", RevelCondition, delegate (MenuCallbackArgs a) { Revel(); }, false, 2);
            starter.AddGameMenuOption(Menu, "arm_row_mend", "{=!}{ARM_ROW_MEND}", MendCondition, delegate (MenuCallbackArgs a) { Mend(); }, false, 1);
            starter.AddGameMenuOption(Menu, "arm_row_trade", "{=!}Trade with the sutlers", TradeCondition, delegate (MenuCallbackArgs a) { Trade(); }, false, 3);
            starter.AddGameMenuOption(Menu, "arm_row_clothe", "{=!}{ARM_ROW_CLOTHE}", ClotheCondition, delegate (MenuCallbackArgs a) { Clothe(); }, false, 4);      // m103-drobne
            starter.AddGameMenuOption(Menu, "arm_row_laundry", "{=!}{ARM_ROW_LAUNDRY}", LaundryCondition, delegate (MenuCallbackArgs a) { Laundry(); }, false, 5);  // m103-drobne
            starter.AddGameMenuOption(Menu, "arm_row_back", "{=!}Back to the camp",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                delegate (MenuCallbackArgs a)
                {
                    try
                    {
                        // zwykle menu - SwitchToMenu wolno; w cudzej armii powrot do menu stanu gry (army_wait), jak gra (EncounterGameMenuBehavior: SwitchToMenu("army_wait"))
                        if (InForeignArmy())
                        {
                            string g = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
                            if (!string.IsNullOrEmpty(g)) GameMenu.SwitchToMenu(g); else GameMenu.ExitToLast();
                        }
                        else GameMenu.SwitchToMenu("arm_camp_wait");
                    }
                    catch (Exception e) { Stumble("Back", e); }
                }, true, 9);

            var s = Settings.Current;
            Log.Info(P + "start sesji | opcja \"Visit the sutlers' row\" w menu obozu (arm_camp_wait) i w menu cudzej armii (army_wait) | klucz Camp Sutlers Row " + (On ? "TAK" : "NIE")
                     + (s == null ? "" : " | prog obozu " + MinMen() + " ludzi, kram na " + PerStall() + " ludzi, najwyzej " + MaxStalls() + " kramow | narzut " + Markup().ToString("0.00", Inv)
                     + " | cyrulik " + SurgeonPer() + " rannych na kram na dobe, " + SurgeonFee() + " x (tier+1) za czlowieka | platnerze " + MendPer() + " sztuk na kram na dobe"
                     + (MendMaterial.MenAndLordsOn ? " (robota + material z polki miasta)" : " (robota)") + " | zabawa " + RevelPer() + " na czlowieka, morale +" + RevelMorale()
                     + " do pulapu " + RevelCap() + ", co 24 h | towar " + StockPer() + " sztuk rodzaju na kram na dobe | gotowka skupu " + CashPer() + " na kram na dobe")
                     + " | stan dnia: doba " + _day + ", od zabawy " + (HoursSinceRevel() >= 1e8 ? "-" : HoursSinceRevel().ToString("0.0", Inv) + " h") + ", opatrzeni " + _treatedToday
                     + ", naprawione " + _mendedToday + ", skup " + _soldToday + ", rodzajow kupionych " + _boughtToday.Count
                     // m103-drobne: odziez i praczki
                     + " | odziez z rzedu (Sutler Row Clothing) " + (ClotheOn ? "TAK" : "NIE") + ", praczki (Sutler Laundry) " + (LaundryOn ? "TAK - zuzycie -" + LaundryCut() + "% przez " + LaundryDays()
                       + " dob, 1 praczka na " + MenPerWasher() + " ludzi, dniowka " + WasherWage().ToString("0.##", Inv) : "NIE")
                     + (ArmyClothing.On ? "" : " (Army Clothing Enabled wylaczone - obie uslugi nieczynne)")
                     + ", praczki czynne " + (LaundryLeftH() > 0 ? "jeszcze " + LaundryLeftH().ToString("0.0", Inv) + " h" : "nie") + ".");
        }

        // ------------------------------------------------------------ liczby (przyciete jak suwaki MCM)
        private static int MinMen() { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(5000, s.SutlerRowMinMen)) : 150; }
        private static int PerStall() { var s = Settings.Current; return s != null ? Math.Max(10, Math.Min(1000, s.SutlerRowMenPerStall)) : 100; }
        private static int MaxStalls() { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(50, s.SutlerRowMaxStalls)) : 12; }
        private static float Markup() { var s = Settings.Current; return s != null ? MBMath.ClampFloat(s.SutlerRowMarkupPercent / 100f, 0f, 1f) : 0.3f; }
        private static int SurgeonPer() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(100, s.SutlerSurgeonPerStall)) : 8; }
        private static int SurgeonFee() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(1000, s.SutlerSurgeonFee)) : 15; }
        private static int RevelPer() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(50, s.SutlerRevelPerMan)) : 2; }
        private static int RevelMorale() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(50, s.SutlerRevelMorale)) : 6; }
        private static int StockPer() { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(100, s.SutlerRowStockPerStall)) : 5; }
        private static int CashPer() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(100000, s.SutlerRowCashPerStall)) : 1500; }
        private static int RevelCap() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(100, s.SutlerRevelMoraleCap)) : 10; }
        private static int MendPer() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(100, s.SutlerArmourerPerStall)) : 6; }

        /// <summary>Ludzie obozu: druzyna gracza, w armii - wodz i doczepione partie (jak CampWalk). Zdrowi i ranni.</summary>
        private static int CampMen(out bool army)
        {
            var mp = MobileParty.MainParty;
            army = false;
            if (mp == null) return 0;
            var ar = mp.Army;
            var lead = ar != null ? ar.LeaderParty : null;
            if (lead != null && (lead == mp || mp.AttachedTo == lead))
            {
                army = true;
                int n = lead.MemberRoster != null ? lead.MemberRoster.TotalManCount : 0;
                var att = lead.AttachedParties;
                if (att != null) for (int i = 0; i < att.Count; i++) { var p = att[i]; if (p != null && p != lead && p.MemberRoster != null) n += p.MemberRoster.TotalManCount; }
                return n;
            }
            return mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
        }

        private static int Stalls(int men) { return Math.Max(1, Math.Min(MaxStalls(), men / PerStall())); }

        private static bool Can(out int men, out int stalls, out string why)
        {
            why = null; men = 0; stalls = 0;
            var mp = MobileParty.MainParty;
            bool army;
            if (Campaign.Current == null || mp == null || Hero.MainHero == null) why = "No campaign is running.";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) why = "Not while in a battle or siege.";
            else if (mp.CurrentSettlement != null) why = "Leave the settlement first.";
            else if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) why = "No sutlers follow you at sea.";
            else
            {
                men = CampMen(out army);
                if (men < MinMen()) why = "Only a big camp draws sutlers - " + MinMen() + " men or more (your camp: " + men + ").";
                else stalls = Stalls(men);
            }
            return why == null;
        }

        private static Settlement Source()
        {
            var mp = MobileParty.MainParty;
            var p = mp.GetPosition2D;
            var t = Sutlers.NearestTown(p, mp.MapFaction);
            if (t == null) t = Sutlers.NearestTown(p, null);
            return t != null && t.Town != null ? t : null;
        }

        private static string CampText(int men, int stalls)
        {
            bool army; CampMen(out army);
            return men + " men" + (army ? " in the army's camp" : " in your camp");
        }

        private static void RowInit(MenuCallbackArgs a)
        {
            try
            {
                Roll();
                try { NightRest.SetCampBackground(a); } catch { }
                int men, stalls; string why;
                string text;
                if (!Can(out men, out stalls, out why)) text = why;
                else
                {
                    var src = Source();
                    text = "Behind the tents the sutlers have set up their row - " + stalls + (stalls == 1 ? " stall" : " stalls") + " for " + CampText(men, stalls)
                           + ": barber-surgeons, field armourers, an ale tent with dice and a minstrel, and carts of food, drink, tools and arrows"
                           + (src != null ? " brought from " + src.Name + "." : ".")
                           + " Their prices are the town's, with " + (Markup() * 100f).ToString("0", Inv) + "% for the cartage.";
                    if (HoursSinceRevel() < 24) text += " The men had their revel " + (int)HoursSinceRevel() + " hours ago.";
                }
                MBTextManager.SetTextVariable("ARM_ROW_TEXT", text, false);
            }
            catch (Exception e) { Stumble("RowInit", e); MBTextManager.SetTextVariable("ARM_ROW_TEXT", "The sutlers' row.", false); }
        }

        // ------------------------------------------------------------ obieg zaplaty uslug (jak Sutlers.One)
        /// <summary>Zloto zdjete juz z kiesy gracza: miasto (z hakiem 75/25) i wies (z danina pana). Zwraca opis odbiorcow do logu. Tylko arytmetyka i ChangeGold.</summary>
        private static string Spread(Settlement town, int gold, out int toTown, out int toFolk, out int toVill, out int toTax)
        {
            toTown = toFolk = toVill = toTax = 0;
            if (gold <= 0 || town == null || town.Town == null) return "-";
            var s = Settings.Current;
            float townShare = s != null ? MBMath.ClampFloat(s.SutlerTownShare, 0f, 1f) : 0.85f;
            int villPart = (int)(gold * (double)(1f - townShare));
            Settlement vill = null;
            if (villPart > 0)
            {
                var mp = MobileParty.MainParty;
                vill = Sutlers.NearestVillage(mp.GetPosition2D, mp.MapFaction);
                if (vill != null && (vill.SettlementComponent == null || vill.Village == null)) vill = null;
                if (vill == null) villPart = 0;
            }
            int townPart = gold - villPart;
            int folk = townPart > 0 ? TownFolk.EarnService(town, townPart, TownFolk.KLife) : 0;
            if (townPart - folk != 0) town.Town.ChangeGold(townPart - folk);
            toTown = townPart - folk; toFolk = folk;
            try { TownFolk.Note(town, townPart, TownFolk.KLife); } catch { }   // jak Sutlers.Pay: wydatek ludzi w kasie miasta (tylko licznik)
            // poprawka po recenzji (drobne): wplata do kasy miasta poza tickiem dobowym nazwana w ksiedze przeplywow osad (tylko licznik, wlasny try)
            if (toTown != 0) MoneyLedger.Note169(MoneyLedger.N169Other, town, toTown);
            int crown = 0;
            if (villPart > 0)
            {
                var vv = vill.Village;
                if (s != null && s.VillageTributeFromSutlers && !ReferenceEquals(vill.OwnerClan, Clan.PlayerClan))
                {
                    int vtax, vpurse;
                    VillageTakings.Split(vv, villPart, villPart, out vtax, out vpurse);
                    toVill = vpurse; toTax = vtax;
                }
                else { vv.ChangeGold(villPart); toVill = villPart; }
                MoneyLedger.Note169(MoneyLedger.N169Other, vill, toVill);   // kiesa wsi poza tickiem dobowym (tylko licznik)
                // poprawka po recenzji (drobne): jak Sutlers.One - udzial korony DS od czesci wsi w kiesie (wlasny try; wies spoza DS - 0); zabiera z kiesy wsi, suma bez zmian
                if (toVill > 0)
                {
                    crown = DsVillage.WarShareSutlers(vv, toVill);
                    if (crown > 0) { toVill -= crown; MoneyLedger.Note169(MoneyLedger.N169Other, vill, -crown); }
                }
            }
            toTax += crown;   // danina pana + udzial korony (oba poza kiesa wsi) - kontrola sumy bez zmian
            _dToTown += toTown; _dToFolk += toFolk; _dToVill += toVill; _dToTax += toTax; _dPaid += gold; _dCrown += crown;
            return "kasa miasta " + town.StringId + " " + toTown + " + lud " + toFolk + (vill != null ? " + kiesa wsi " + vill.StringId + " " + toVill + " + danina " + toTax + " (w tym korona DS " + crown + ")" : " (bez wsi - calosc do miasta)");
        }

        private static bool TakeFromPlayer(int gold)
        {
            if (gold <= 0 || Hero.MainHero == null || Hero.MainHero.Gold < gold) return false;
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, gold, PayNotes.Quiet(false));
            return true;
        }

        // ------------------------------------------------------------ 1. cyrulicy
        private static int TreatPlan(int stalls, out int price, out int wounded)
        {
            price = 0; wounded = 0;
            var roster = MobileParty.MainParty.MemberRoster;
            int cap = Math.Max(0, stalls * SurgeonPer() - _treatedToday);
            var list = new List<TroopRosterElement>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero || el.WoundedNumber <= 0) continue;
                wounded += el.WoundedNumber;
                list.Add(el);
            }
            list.Sort((x, y) => y.Character.Tier.CompareTo(x.Character.Tier));
            int n = 0;
            foreach (var el in list)
            {
                if (n >= cap) break;
                int k = Math.Min(el.WoundedNumber, cap - n);
                n += k; price += k * SurgeonFee() * (Math.Max(0, el.Character.Tier) + 1);
            }
            return n;
        }

        private static bool SurgeonCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Manage;
                Roll();
                int men, stalls, price, wounded; string why;
                if (!Can(out men, out stalls, out why))
                {
                    MBTextManager.SetTextVariable("ARM_ROW_SURGEON", "Pay the barber-surgeons to tend the wounded", false);
                    a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true;
                }
                int n = TreatPlan(stalls, out price, out wounded);
                MBTextManager.SetTextVariable("ARM_ROW_SURGEON", n > 0 ? "Pay the barber-surgeons to tend " + n + " wounded (" + price + " denars)" : "Pay the barber-surgeons to tend the wounded", false);
                if (wounded <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}None of your soldiers is wounded."); }
                else if (n <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The surgeons have done all they can today (" + stalls * SurgeonPer() + " men a day at " + stalls + (stalls == 1 ? " stall" : " stalls") + ")."); }
                else if (Hero.MainHero.Gold < price) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You cannot pay " + price + " denars."); }
                else a.Tooltip = new TextObject("{=!}The surgeons dress the wounds of " + n + " of your " + wounded + " wounded soldiers, the most seasoned first, and they return to duty. " + SurgeonFee() + " denars x (tier + 1) a man; at most " + stalls * SurgeonPer() + " men a day.");
                return true;
            }
            catch (Exception e) { Stumble("SurgeonCondition", e); return false; }
        }

        private static void Surgeon()
        {
            try
            {
                Roll();
                int men, stalls, price, wounded; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                var src = Source();
                if (src == null) { Log.Player("No sutlers can reach you here.", true); return; }
                int n = TreatPlan(stalls, out price, out wounded);
                if (n <= 0 || price <= 0 || !TakeFromPlayer(price)) { Log.Player("The surgeons cannot help you now.", true); return; }
                // ---- zloto zdjete: najpierw obieg (arytmetyka), potem leczenie
                int tt, tf, tv, tx;
                string to = Spread(src, price, out tt, out tf, out tv, out tx);
                var roster = MobileParty.MainParty.MemberRoster;
                var list = new List<TroopRosterElement>();
                for (int i = 0; i < roster.Count; i++) { var el = roster.GetElementCopyAtIndex(i); if (el.Character != null && !el.Character.IsHero && el.WoundedNumber > 0) list.Add(el); }
                list.Sort((x, y) => y.Character.Tier.CompareTo(x.Character.Tier));
                int left = n, healed = 0;
                foreach (var el in list)
                {
                    if (left <= 0) break;
                    int k = Math.Min(el.WoundedNumber, left);
                    roster.AddToCounts(el.Character, 0, false, -k);
                    left -= k; healed += k;
                }
                _treatedToday += healed; _dTreated += healed; _dSurgeonGold += price;
                if (PayNotes.On) PayNotes.Paid("markietani-cyrulik", price, " to the barber-surgeons for tending " + healed + " wounded.");
                Log.Info(P + "cyrulik | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | opatrzeni " + healed + " z " + wounded + " rannych (dzis " + _treatedToday + " z " + stalls * SurgeonPer()
                         + ") | gracz zaplacil " + price + " -> " + to + " | kontrola: " + price + " = " + (tt + tf + tv + tx) + " (roznica " + (price - tt - tf - tv - tx) + ") | zrodlo " + src.StringId + ".");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Stumble("Surgeon", e); }
        }

        // ------------------------------------------------------------ 1b. platnerze polowi (poprawka po recenzji: usluga naprawy - Jeff "uslugi")
        // Zuzyte sztuki LUDZI z magazynu kwatermistrza (TroopSelfMend.MenWorn - bez Twojej czesci, bez wrakow i zwierzat), najgorsze najpierw, najwyzej SutlerArmourerPerStall (6)
        // na kram na dobe. Robota = stawka kowali miasta-zrodla (TroopSelfMend.UnitCost(ee, miasto) - ta sama co w miescie) x (1 + narzut). Przy naprawach z materialem
        // (MendMaterial.MenAndLordsOn) material z polki TEGO miasta (MendMaterial.Order/Bench - ta sama regula co kowale miasta: brak materialu - sztuka czeka, bez receptury - nie).
        // Pieniadz: material - do kasy miasta (towar z jego polki), robota z narzutem - obiegiem uslug (Spread). Gracz placi z kiesy; sakiewka ludzi nietknieta.
        private static string _mendKey, _mendLeft;
        private static int _mendN, _mendPrice, _mendMat, _mendWorn;

        private static int RowLabor(EquipmentElement ee, Settlement src, float up)
        {
            return Math.Max(1, (int)Math.Ceiling(TroopSelfMend.UnitCost(ee, src) * (1.0 + up) - 1e-6));
        }

        private static int MendPlan(Settlement src, int stalls, List<KeyValuePair<EquipmentElement, int>> jobs, out int labor, out int matGold, out int wornN, out MendMaterial.Order order)
        {
            labor = 0; matGold = 0; wornN = 0; order = null;
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null || src == null || src.Town == null || Hero.MainHero == null) return 0;
            var worn = TroopSelfMend.MenWorn(armory);
            foreach (var el in worn) wornN += el.Amount;
            int cap = Math.Max(0, stalls * MendPer() - _mendedToday);
            if (cap <= 0 || wornN <= 0) return 0;
            worn.Sort((x, y) => x.EquipmentElement.ItemModifier.PriceMultiplier.CompareTo(y.EquipmentElement.ItemModifier.PriceMultiplier));   // MenWorn: modyfikator zawsze jest
            float up = Markup();
            long limit = Math.Max(0, Hero.MainHero.Gold);
            if (MendMaterial.MenAndLordsOn)
            {
                var o = new MendMaterial.Order(src);
                if (!o.Ok) return 0;
                foreach (var el in worn)
                {
                    if (o.Pieces >= cap) break;
                    var ee = el.EquipmentElement;
                    o.AddLot(ee, MendMaterial.Needs(ee), RowLabor(ee, src, up), el.Amount, limit, cap);
                    if (o.Poor > 0) break;
                }
                foreach (var j in o.Jobs) jobs.Add(new KeyValuePair<EquipmentElement, int>(j.El, j.N));
                labor = o.Labor; matGold = o.MatGold; order = o;
                return o.Pieces;
            }
            int n = 0; long spent = 0;
            foreach (var el in worn)
            {
                if (n >= cap) break;
                int lab = RowLabor(el.EquipmentElement, src, up);
                int k = Math.Min(el.Amount, cap - n);
                k = (int)Math.Min(k, Math.Max(0L, (limit - spent) / lab));
                if (k <= 0) break;
                jobs.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, k));
                n += k; spent += (long)k * lab;
            }
            labor = (int)spent;
            return n;
        }

        private static bool MendCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Craft;
                Roll();
                int men, stalls; string why;
                MBTextManager.SetTextVariable("ARM_ROW_MEND", "Have the field armourers mend the men's worn gear", false);
                if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                var src = Source();
                if (src == null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}No town is near enough to supply the armourers."); return true; }
                // podglad z pamieci na te sama chwile (godzina, kiesa, limit, kramy, miasto) - warunek moze byc wolany przy kazdym odswiezeniu menu, lawa liczy polke
                string key = ((long)CampaignTime.Now.ToHours) + "|" + Hero.MainHero.Gold + "|" + _mendedToday + "|" + stalls + "|" + src.StringId;
                if (key != _mendKey)
                {
                    var jobs = new List<KeyValuePair<EquipmentElement, int>>();
                    int lab0, mat0, worn0; MendMaterial.Order o0;
                    _mendN = MendPlan(src, stalls, jobs, out lab0, out mat0, out worn0, out o0);
                    _mendPrice = lab0 + mat0; _mendMat = mat0; _mendWorn = worn0; _mendLeft = o0 != null ? o0.LeftEn(src.Name.ToString()) : null; _mendKey = key;
                }
                int n = _mendN, price = _mendPrice, mat = _mendMat, worn = _mendWorn;
                string left = _mendLeft;
                if (n > 0) MBTextManager.SetTextVariable("ARM_ROW_MEND", "Have the field armourers mend " + n + (n == 1 ? " piece" : " pieces") + " of the men's gear (" + price + " denars)", false);
                if (worn <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your men's kit needs no mending (wrecks and your own pieces aside)."); }
                else if (_mendedToday >= stalls * MendPer()) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The armourers have done all they can today (" + stalls * MendPer() + " pieces a day at " + stalls + (stalls == 1 ? " stall" : " stalls") + ")."); }
                else if (n <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The armourers cannot mend anything now." + (left != null ? left : " You cannot pay for the work.")); }
                else a.Tooltip = new TextObject("{=!}The field armourers mend " + n + " of " + worn + " worn pieces of your men's kit, the worst first - the smiths' rate of " + src.Name + " plus "
                                                + (Markup() * 100f).ToString("0", Inv) + "%" + (mat > 0 ? ", and " + mat + " denars of materials from its market" : "") + ". At most " + stalls * MendPer() + " pieces a day."
                                                + (left ?? ""));
                return true;
            }
            catch (Exception e) { Stumble("MendCondition", e); return false; }
        }

        private static void Mend()
        {
            try
            {
                Roll();
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                var src = Source();
                var armory = QuartermasterLaw.DteArmory();
                if (src == null || armory == null) { Log.Player("No armourers can reach you here.", true); return; }
                var jobs = new List<KeyValuePair<EquipmentElement, int>>();
                int labor, mat, worn; MendMaterial.Order o;
                int n = MendPlan(src, stalls, jobs, out labor, out mat, out worn, out o);
                int price = labor + mat;
                if (n <= 0 || price <= 0 || !TakeFromPlayer(price)) { Log.Player("The armourers cannot help you now.", true); return; }
                // ---- zloto zdjete: material z polki miasta (Commit), sztuki w magazynie, pieniadz (arytmetyka)
                if (o != null) o.Bench.Commit();
                int done = 0;
                foreach (var j in jobs)
                {
                    if (j.Value <= 0) continue;
                    armory.AddToCounts(j.Key, -j.Value);
                    armory.AddToCounts(new EquipmentElement(j.Key.Item), j.Value);   // czysty stan
                    done += j.Value;
                }
                if (mat > 0) { src.Town.ChangeGold(mat); MoneyLedger.Note169(MoneyLedger.N169Repair, src, mat); }   // material z polki miasta - zaplata do jego kasy
                int tt = 0, tf = 0, tv = 0, tx = 0;
                string to = labor > 0 ? Spread(src, labor, out tt, out tf, out tv, out tx) : "-";
                _mendedToday += done; _dMended += done; _dMendGold += price; _dMendMat += mat; _mendKey = null;
                if (PayNotes.On) PayNotes.Paid("markietani-platnerz", price, " to the field armourers for mending " + done + " pieces of your men's gear" + (mat > 0 ? " (" + mat + " of it for materials)." : "."));
                Log.Info(P + "platnerz | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | naprawione " + done + " z " + worn + " zuzytych sztuk ludzi (dzis " + _mendedToday + " z " + stalls * MendPer()
                         + ") | robota " + labor + " (stawka kowali " + src.StringId + " x " + (1f + Markup()).ToString("0.00", Inv) + ") + material " + mat
                         + (o != null ? " (" + o.LogPl() + ")" : " (bez materialu)") + " | gracz zaplacil " + price + " -> material do kasy miasta " + mat + " + robota -> " + to
                         + " | kontrola: " + price + " = " + mat + " + " + (tt + tf + tv + tx) + " (roznica " + (price - mat - tt - tf - tv - tx) + ") | zrodlo " + src.StringId + ".");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Stumble("Mend", e); }
        }

        // ------------------------------------------------------------ 2. zabawa
        // poprawka po recenzji: PULAP - zabawa podnosi morale z ostatnich zdarzen (RecentEventsMorale) najwyzej do SutlerRevelMoraleCap (10); dodatek = min(+6, pulap - teraz),
        // cena w tej samej proporcji (placisz za to, co dostajesz). Bez pulapu codzienna zabawa zbiegala do +60 (gra zdejmuje 10% na dobe). Odstep 24 h zamiast doby
        // kalendarzowej (23:00 i 01:00 to nie dwie zabawy). Ponizej +1 opcja szara ("ludzie jeszcze sie bawia").
        private static float RevelGain(MobileParty mp)
        {
            if (mp == null) return 0f;
            float cap = RevelCap();
            return Math.Max(0f, Math.Min((float)RevelMorale(), cap - mp.RecentEventsMorale));
        }

        private static int RevelPrice(float gain)
        {
            var mp = MobileParty.MainParty;
            if (mp == null || mp.MemberRoster == null || RevelMorale() <= 0) return 0;
            double full = mp.MemberRoster.TotalHealthyCount * (double)RevelPer();
            return (int)Math.Ceiling(full * Math.Min(1f, gain / RevelMorale()) - 1e-6);
        }

        private static bool RevelCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Wait;
                Roll();
                int men, stalls; string why;
                var mp = MobileParty.MainParty;
                float gain = RevelGain(mp);
                int price = RevelPrice(gain);
                double since = HoursSinceRevel();
                MBTextManager.SetTextVariable("ARM_ROW_REVEL", "Stand the men a round - ale, dice and a minstrel (" + price + " denars)", false);
                if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                else if (since < 24) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The men had their revel " + (int)since + " hours ago - once a day (again in " + (int)Math.Ceiling(24 - since) + " hours)."); }
                else if (gain < 1f) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The men are still merry - morale from recent events is +" + mp.RecentEventsMorale.ToString("0.0", Inv) + ", and a revel lifts it to at most +" + RevelCap() + "."); }
                else if (price <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}There is nobody to drink with."); }
                else if (Hero.MainHero.Gold < price) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You cannot pay " + price + " denars."); }
                else a.Tooltip = new TextObject("{=!}" + RevelPer() + " denars a man for your " + mp.MemberRoster.TotalHealthyCount + " men for a full round. Morale +" + gain.ToString("0.0", Inv)
                                                + " (revels lift morale from recent events to at most +" + RevelCap() + "), fading over the following days. Once a day.");
                return true;
            }
            catch (Exception e) { Stumble("RevelCondition", e); return false; }
        }

        private static void Revel()
        {
            try
            {
                Roll();
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                if (HoursSinceRevel() < 24) return;
                var src = Source();
                var mp = MobileParty.MainParty;
                float gain = RevelGain(mp);
                int price = RevelPrice(gain);
                if (src == null || gain < 1f || price <= 0 || !TakeFromPlayer(price)) { Log.Player("The ale tent cannot serve you now.", true); return; }
                int tt, tf, tv, tx;
                string to = Spread(src, price, out tt, out tf, out tv, out tx);
                float m0 = mp.RecentEventsMorale;
                mp.RecentEventsMorale += gain;
                _lastRevel = CampaignTime.Now.ToHours; _dRevels++; _dRevelGold += price;
                if (PayNotes.On) PayNotes.Paid("markietani-zabawa", price, " for a round of ale, dice and a minstrel for your men.");
                Log.Player("The men drink, dice and sing late into the night. (Morale +" + gain.ToString("0.0", Inv) + ")");
                Log.Info(P + "zabawa | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | druzyna gracza " + mp.MemberRoster.TotalHealthyCount + " zdrowych x " + RevelPer()
                         + " x " + Math.Min(1f, gain / Math.Max(1, RevelMorale())).ToString("0.00", Inv) + " | morale RecentEvents " + m0.ToString("0.0", Inv) + " -> " + mp.RecentEventsMorale.ToString("0.0", Inv)
                         + " (dodatek " + gain.ToString("0.0", Inv) + ", pulap " + RevelCap() + ") | gracz zaplacil " + price + " -> " + to
                         + " | kontrola: " + price + " = " + (tt + tf + tv + tx) + " (roznica " + (price - tt - tf - tv - tx) + ") | zrodlo " + src.StringId + ".");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Stumble("Revel", e); }
        }

        // ------------------------------------------------------------ 4. odziez (m103-drobne, klucz SutlerRowClothing)
        // Cale sztuki potrzeby odziezy druzyny gracza (ArmyClothing) z polki miasta-zrodla po cenie miasta x (1 + narzut), najwyzej SutlerRowStockPerStall x kramy sztuk
        // kazdego towaru na dobe (licznik _boughtToday - ten sam co sklep; skora/filc/plotno nie sa w towarze sklepu, wiec limity sie nie mieszaja). Gracz placi z kiesy;
        // towar po cenie miasta do kasy miasta (ArmyClothing.RowSettle), narzut obiegiem uslug (Spread). Potrzeba druzyny maleje o kupione sztuki.
        private static bool ClotheOn { get { var s = Settings.Current; return s != null && s.SutlerRowClothing && ArmyClothing.On; } }

        private static int[] ClotheCap(int stalls)
        {
            var cap = new int[3];
            int per = stalls * StockPer();
            for (int g = 0; g < 3; g++)
            {
                string id = ArmyClothing.IdOf(g);
                int b = 0;
                if (id != null) _boughtToday.TryGetValue(id, out b);
                cap[g] = Math.Max(0, per - b);
            }
            return cap;
        }

        private static string Frac(float[] n)
        {
            return "leather " + n[0].ToString("0.##", Inv) + ", felt " + n[1].ToString("0.##", Inv) + ", linen " + n[2].ToString("0.##", Inv);
        }

        private static bool ClotheCondition(MenuCallbackArgs a)
        {
            try
            {
                if (!ClotheOn) return false;
                a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                Roll();
                MBTextManager.SetTextVariable("ARM_ROW_CLOTHE", "Clothe the men from the sutlers' carts", false);
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                var src = Source();
                if (src == null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}No town is near enough to supply the sutlers."); return true; }
                var need = ArmyClothing.PlayerNeed();
                int whole = 0;
                for (int g = 0; g < 3; g++) whole += (int)Math.Floor(Math.Max(0f, need[g]));
                var cap = ClotheCap(stalls);
                var got = new int[3]; long price; bool lacked;
                int n = ArmyClothing.RowPreview(src, Markup(), cap, Math.Max(0, Hero.MainHero.Gold), got, out price, out lacked);
                if (n > 0) MBTextManager.SetTextVariable("ARM_ROW_CLOTHE", "Clothe the men from the sutlers' carts (" + n + (n == 1 ? " piece" : " pieces") + ", about " + price + " denars)", false);
                if (whole <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your men have not yet worn out a whole piece of anything (" + Frac(need) + " of a piece). The sutlers also bring them what they wear out, from the men's own purse, once a day."); }
                else if (n <= 0 && cap[0] + cap[1] + cap[2] <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The carts have brought all the cloth and leather they could today (" + stalls * StockPer() + " pieces of each a day)."); }
                else if (n <= 0 && lacked) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The market of " + src.Name + " has none of the leather, felt or linen your men need."); }
                else if (n <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You cannot pay for it."); }
                else a.Tooltip = new TextObject("{=!}Leather, felt and linen from the market of " + src.Name + " at its prices plus " + (Markup() * 100f).ToString("0", Inv) + "% replace what your men have worn out: "
                                                + ArmyClothing.ListEn(got) + ". Paid from your purse - the men's own purse is spared, and they need not buy it in the next town. At most "
                                                + stalls * StockPer() + " pieces of each a day.");
                return true;
            }
            catch (Exception e) { Stumble("ClotheCondition", e); return false; }
        }

        private static void Clothe()
        {
            try
            {
                Roll();
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                var src = Source();
                if (src == null || !ClotheOn) { Log.Player("No sutlers can reach you here.", true); return; }
                var need0 = ArmyClothing.PlayerNeed();
                var cap = ClotheCap(stalls);
                var got = new int[3];
                var undo = new List<EquipmentElement>();
                int paidTown, paidRow;
                int gold0 = Hero.MainHero.Gold;
                int n = ArmyClothing.RowBuy(src, Markup(), cap, Math.Max(0, gold0), got, out paidTown, out paidRow, undo);
                if (n <= 0 || paidRow <= 0) { ArmyClothing.RowUndo(src, undo); Log.Player("The sutlers have nothing for your men now.", true); return; }
                if (!TakeFromPlayer(paidRow)) { ArmyClothing.RowUndo(src, undo); Log.Player("You cannot pay the sutlers.", true); return; }
                // ---- zloto zdjete: towar po cenie miasta do kasy miasta, narzut obiegiem uslug
                int markup = Math.Max(0, paidRow - paidTown);
                ArmyClothing.RowSettle(src, paidTown, markup, got);
                int tt = 0, tf = 0, tv = 0, tx = 0;
                string to = markup > 0 ? Spread(src, markup, out tt, out tf, out tv, out tx) : "-";
                for (int g = 0; g < 3; g++)
                {
                    string id = ArmyClothing.IdOf(g);
                    if (id == null || got[g] <= 0) continue;
                    int b; _boughtToday.TryGetValue(id, out b); _boughtToday[id] = b + got[g];
                }
                _dClothe++; _dClothePcs += n; _dClotheGold += paidRow; _dClotheTown += paidTown;
                if (PayNotes.On) PayNotes.Paid("markietani-odziez", paidRow, " to the sutlers for " + ArmyClothing.ListEn(got) + " for your men.");
                Log.Player("The sutlers hand out " + ArmyClothing.ListEn(got) + " - your men are shod and clothed again.");
                Log.Info(P + "odziez | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | druzyna gracza: potrzeba " + ArmyClothing.ListPl(need0) + " -> " + ArmyClothing.ListPl(ArmyClothing.PlayerNeed())
                         + " | kupione " + ArmyClothing.ListPl(got) + " (najwyzej " + stalls * StockPer() + " rodzaju na dobe) | cena markietanow " + paidRow + ", miasta " + paidTown
                         + " | gracz zaplacil " + paidRow + " (kiesa " + gold0 + " -> " + Hero.MainHero.Gold + ") -> towar do kasy miasta " + paidTown + " + narzut " + markup + " -> " + to
                         + " | kontrola: " + paidRow + " = " + paidTown + " + " + (tt + tf + tv + tx) + " (roznica " + (paidRow - paidTown - tt - tf - tv - tx) + ") | zrodlo " + src.StringId + ".");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Stumble("Clothe", e); }
        }

        // ------------------------------------------------------------ 5. praczki (m103-drobne, klucz SutlerLaundry)
        // Przez SutlerLaundryDays dob narastanie potrzeby odziezy druzyny gracza (ArmyClothing.OnDailyTickParty, share) mniejsze o SutlerLaundryCutPercent - pranie i cerowanie
        // przedluzaja zycie odziezy. Oplata = praczki (ceil(ludzie druzyny bez bohaterow / SutlerLaundryMenPerWasher)) x doby x dniowka praczki (2/3 Workshop Wage Per Day -
        // dzien pracy kobiety ok. 2/3 dniowki rzemieslnika) obiegiem uslug (Spread). Nie da sie najac drugi raz, dopoki pracuja.
        private static bool LaundryOn { get { var s = Settings.Current; return s != null && s.SutlerLaundry && ArmyClothing.On; } }
        private static int LaundryCut() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(75, s.SutlerLaundryCutPercent)) : 25; }
        private static int LaundryDays() { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(28, s.SutlerLaundryDays)) : 7; }
        private static int MenPerWasher() { var s = Settings.Current; return s != null ? Math.Max(5, Math.Min(100, s.SutlerLaundryMenPerWasher)) : 20; }
        private static float WasherWage() { var s = Settings.Current; return Math.Max(0f, (s != null ? s.WorkshopWagePerDay : 3f) * 2f / 3f); }
        private static double LaundryLeftH() { return _laundryUntil <= NoLaundry / 2 ? 0.0 : Math.Max(0.0, _laundryUntil - CampaignTime.Now.ToHours); }

        /// <summary>m103-drobne: czesc doby zuzycia odziezy druzyny gracza (ArmyClothing.OnDailyTickParty) - 1 bez praczek, 1 - SutlerLaundryCutPercent, gdy pracuja.</summary>
        internal static float LaundryShare()
        {
            try
            {
                if (!LaundryOn || LaundryLeftH() <= 0.0) return 1f;
                return 1f - LaundryCut() / 100f;
            }
            catch (Exception e) { Stumble("LaundryShare", e); return 1f; }
        }

        private static int LaundryPrice(out int men, out int washers)
        {
            var mp = MobileParty.MainParty;
            men = mp != null && mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
            washers = men > 0 ? (men + MenPerWasher() - 1) / MenPerWasher() : 0;
            return (int)Math.Ceiling(washers * LaundryDays() * (double)WasherWage() - 1e-6);
        }

        private static bool LaundryCondition(MenuCallbackArgs a)
        {
            try
            {
                if (!LaundryOn) return false;
                a.optionLeaveType = GameMenuOption.LeaveType.Manage;
                int men, stalls; string why;
                int pm, washers;
                int price = LaundryPrice(out pm, out washers);
                MBTextManager.SetTextVariable("ARM_ROW_LAUNDRY", "Hire the washerwomen for " + LaundryDays() + " days (" + price + " denars)", false);
                if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                double left = LaundryLeftH();
                if (left > 0.0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The washerwomen are already at work for your men - " + Math.Ceiling(left / 24.0).ToString("0", Inv) + " more days."); return true; }
                if (pm <= 0 || price <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You have no men for the washerwomen to look after."); return true; }
                if (Hero.MainHero.Gold < price) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You cannot pay " + price + " denars."); return true; }
                var wear = ArmyClothing.PlayerWearPerDay();
                float saved = 0f;
                for (int g = 0; g < 3; g++) saved += wear[g] * LaundryDays() * LaundryCut() / 100f;
                a.Tooltip = new TextObject("{=!}" + washers + (washers == 1 ? " washerwoman washes" : " washerwomen wash") + " and darn" + (washers == 1 ? "s" : "") + " the clothes of your " + pm + " men for " + LaundryDays()
                                           + " days: they wear out their shoes, clothes and linen " + LaundryCut() + "% more slowly - about " + saved.ToString("0.#", Inv) + " pieces of leather, felt and linen fewer to buy. "
                                           + WasherWage().ToString("0.#", Inv) + " denars a day for each washerwoman, one for every " + MenPerWasher() + " men.");
                return true;
            }
            catch (Exception e) { Stumble("LaundryCondition", e); return false; }
        }

        private static void Laundry()
        {
            try
            {
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                if (!LaundryOn || LaundryLeftH() > 0.0) return;
                var src = Source();
                int pm, washers;
                int price = LaundryPrice(out pm, out washers);
                if (src == null || pm <= 0 || price <= 0 || !TakeFromPlayer(price)) { Log.Player("The washerwomen cannot work for you now.", true); return; }
                int tt, tf, tv, tx;
                string to = Spread(src, price, out tt, out tf, out tv, out tx);
                _laundryUntil = CampaignTime.Now.ToHours + LaundryDays() * 24.0;
                _dLaundry++; _dLaundryGold += price;
                var wear = ArmyClothing.PlayerWearPerDay();
                if (PayNotes.On) PayNotes.Paid("markietani-praczki", price, " to " + washers + (washers == 1 ? " washerwoman" : " washerwomen") + " for " + LaundryDays() + " days.");
                Log.Player("The washerwomen take up their tubs and needles - for " + LaundryDays() + " days your men's clothes will last longer.");
                Log.Info(P + "praczki | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | druzyna gracza " + pm + " ludzi (bez bohaterow) -> praczek " + washers + " (1 na " + MenPerWasher() + ") x "
                         + LaundryDays() + " dob x dniowka " + WasherWage().ToString("0.##", Inv) + " = " + price + " | zuzycie odziezy -" + LaundryCut() + "% do godziny " + _laundryUntil.ToString("0.#", Inv)
                         + " (zuzycie dzienne bez praczek: " + ArmyClothing.ListPl(wear) + ") | gracz zaplacil " + price + " -> " + to
                         + " | kontrola: " + price + " = " + (tt + tf + tv + tx) + " (roznica " + (price - tt - tf - tv - tx) + ") | zrodlo " + src.StringId + ".");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Stumble("Laundry", e); }
        }

        // ------------------------------------------------------------ 3. sklep
        private static bool Sold(ItemObject it)
        {
            if (it == null) return false;
            if (it.IsFood) return true;
            if (it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts) return true;
            var c = it.ItemCategory != null ? it.ItemCategory.StringId : null;
            return c == "beer" || c == "wine" || c == "tools";
        }

        private static ItemRoster Basket(Settlement town, int stalls, out int kinds, out long pieces)
        {
            kinds = 0; pieces = 0;
            var basket = new ItemRoster();
            var shelf = town.ItemRoster;
            if (shelf == null) return basket;
            int per = stalls * StockPer();
            var used = new Dictionary<string, int>();
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || !Sold(it) || it.StringId == null) continue;
                int b; _boughtToday.TryGetValue(it.StringId, out b);
                int u; used.TryGetValue(it.StringId, out u);
                int n = Math.Min(el.Amount, per - b - u);
                if (n <= 0) continue;
                basket.AddToCounts(el.EquipmentElement, n);
                used[it.StringId] = u + n;
                kinds++; pieces += n;
            }
            return basket;
        }

        private static bool TradeCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                var src = Source();
                if (src == null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}No town is near enough to supply the sutlers."); return true; }
                a.Tooltip = new TextObject("{=!}Food, ale, wine, tools, arrows and bolts from the market of " + src.Name + " at its prices plus " + (Markup() * 100f).ToString("0", Inv)
                                           + "% - up to " + stalls * StockPer() + " of each kind a day. They buy from you at the town's price less the same share, with at most "
                                           + CashLeft(src.Town, stalls) + " denars left in hand today (" + stalls * CashPer() + " a day).");
                return true;
            }
            catch (Exception e) { Stumble("TradeCondition", e); return false; }
        }

        private static void Trade()
        {
            try
            {
                Roll();
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                var src = Source();
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (src == null || gsm == null || Settlement.CurrentSettlement != null) return;   // z osady gra placi sama - tu tylko z pola
                int kinds; long pieces;
                var basket = Basket(src, stalls, out kinds, out pieces);
                var before = new ItemRoster(basket);
                float up = Markup();
                var market = new RowMarket(src.Town, up);
                var listener = new RowListener(src.Town, CashLeft(src.Town, stalls));   // poprawka po recenzji: limit skupu na dobe (mniej wydane dzis), nie na otwarcie ekranu
                var state = gsm.CreateState<InventoryState>();
                state.InventoryMode = InventoryScreenHelper.InventoryMode.Trade;
                var logic = new InventoryLogic(src.Party);
                logic.Initialize(basket, PartyBase.MainParty.ItemRoster, PartyBase.MainParty.MemberRoster, true, true, CharacterObject.PlayerCharacter,
                                 InventoryScreenHelper.InventoryCategoryType.None, market, false, state.InventoryMode, new TextObject("{=!}Sutlers' row"));
                logic.SetInventoryListener(listener);
                state.InventoryLogic = logic;
                int gold0 = Hero.MainHero.Gold, town0 = src.Town.Gold;
                state.DoneLogicExtrasDelegate = delegate { TradeDone(src, men, stalls, market, basket, before, gold0, town0); };
                Log.Info(P + "sklep otwarty | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | zrodlo " + src.StringId + " | koszyk z polki miasta: " + kinds + " rodzajow, " + pieces
                         + " sztuk (najwyzej " + stalls * StockPer() + " rodzaju na dobe) | narzut " + up.ToString("0.00", Inv) + " | gotowka skupu " + listener.GetGold() + " (dzis wydane " + _soldToday + " z " + stalls * CashPer() + ") | kiesa gracza " + gold0 + ", kasa miasta " + town0 + ".");
                gsm.PushState(state);
            }
            catch (Exception e) { Stumble("Trade", e); }
        }

        /// <summary>Po udanym DoneLogic (gra zmienila juz kiese gracza - bez osady i bez spotkania tylko ja): polka miasta o roznice koszyka, pieniadz po stronie markietanow.</summary>
        private static void TradeDone(Settlement src, int men, int stalls, RowMarket market, ItemRoster basket, ItemRoster before, int gold0, int town0)
        {
            try
            {
                Roll();
                var shelf = src.ItemRoster;
                long buyRow = 0, buyTown = 0, sellRow = 0;
                int pcsOut = 0, pcsIn = 0, short_ = 0;
                // ceny liczone PRZED zmiana polki (ta sama chwila co ekran handlu)
                var moves = new List<KeyValuePair<EquipmentElement, int>>();
                for (int i = 0; i < basket.Count; i++)
                {
                    var el = basket.GetElementCopyAtIndex(i);
                    int bi = before.FindIndexOfElement(el.EquipmentElement);
                    int d = el.Amount - (bi >= 0 ? before.GetElementNumber(bi) : 0);
                    if (d != 0) moves.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, d));
                }
                for (int i = 0; i < before.Count; i++)
                {
                    var el = before.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && basket.FindIndexOfElement(el.EquipmentElement) < 0) moves.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, -el.Amount));
                }
                var mp = MobileParty.MainParty;
                foreach (var mv in moves)
                {
                    if (mv.Value < 0) { int k = -mv.Value; buyRow += (long)k * market.GetPrice(mv.Key, mp, false, src.Party); buyTown += (long)k * src.Town.MarketData.GetPrice(mv.Key, mp, false, src.Party); }
                    else sellRow += (long)mv.Value * market.GetPrice(mv.Key, mp, true, src.Party);
                }
                foreach (var mv in moves)
                {
                    if (mv.Value < 0)
                    {
                        int si = shelf.FindIndexOfElement(mv.Key);
                        int have = si >= 0 ? shelf.GetElementNumber(si) : 0;
                        int k = Math.Min(-mv.Value, Math.Max(0, have));
                        if (k < -mv.Value) short_ += -mv.Value - k;
                        if (k > 0) shelf.AddToCounts(mv.Key, -k);
                        pcsOut += -mv.Value;
                        var id = mv.Key.Item != null ? mv.Key.Item.StringId : null;
                        if (id != null) { int b; _boughtToday.TryGetValue(id, out b); _boughtToday[id] = b + (-mv.Value); }
                    }
                    else { shelf.AddToCounts(mv.Key, mv.Value); pcsIn += mv.Value; }
                }
                // ---- pieniadz: D = to, co gracz oddal netto (gra zdjela/dala juz zloto graczowi)
                int paid = gold0 - Hero.MainHero.Gold;
                long mk = Math.Max(0L, buyRow - buyTown);
                if (mk > Math.Max(0, paid)) mk = Math.Max(0, paid);
                int markup = (int)mk;
                int goods = paid - markup;                        // towar po cenie miasta (ujemne - miasto placi za skup)
                if (goods != 0) { src.Town.ChangeGold(goods); MoneyLedger.Note169(MoneyLedger.N169Other, src, goods); }   // poprawka po recenzji (drobne): nazwa w ksiedze przeplywow osad
                if (paid < 0) _soldToday += -paid;                // gotowka markietanow wydana na skup (netto) - limit dnia
                int tt = 0, tf = 0, tv = 0, tx = 0;
                string to = markup > 0 ? Spread(src, markup, out tt, out tf, out tv, out tx) : "-";
                _dTrades++; _dPcsBought += pcsOut; _dPcsSold += pcsIn; _dBuyNet += paid; _dGoodsTown += goods; _dMarkup += markup;
                // komunikat zaplaty daje sama gra (DoneLogic: GiveGoldAction z powiadomieniem, jak w kazdym handlu) - bez drugiego naszego
                Log.Info(P + "sklep zamkniety | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | zrodlo " + src.StringId + " | kupione " + pcsOut + " sztuk (cena markietanow "
                         + buyRow + ", miasta " + buyTown + "), z polki miasta zdjete " + (pcsOut - short_) + " (brak na polce " + short_ + "), sprzedane " + pcsIn + " sztuk (skup " + sellRow
                         + ") na polke miasta " + pcsIn + " | gracz netto " + paid + " (kiesa " + gold0 + " -> " + Hero.MainHero.Gold + ") = towar do kasy miasta " + goods + " + narzut " + markup
                         + " | narzut -> " + to + " | kontrola: " + paid + " = " + goods + " + " + (tt + tf + tv + tx) + " (roznica " + (paid - goods - tt - tf - tv - tx) + ") | kasa miasta " + town0 + " -> " + src.Town.Gold
                         + " | skup dzis " + _soldToday + " z limitu " + stalls * CashPer() + ".");
            }
            catch (Exception e) { Stumble("TradeDone", e); }
        }

        /// <summary>Ceny markietanow: cena miasta-zrodla x (1 + narzut) przy kupnie, x (1 - narzut) przy skupie.</summary>
        private sealed class RowMarket : IMarketData
        {
            private readonly Town _t; private readonly float _up;
            internal RowMarket(Town t, float up) { _t = t; _up = up; }
            public int GetPrice(ItemObject item, MobileParty tradingParty, bool isSelling, PartyBase merchantParty) { return GetPrice(new EquipmentElement(item), tradingParty, isSelling, merchantParty); }
            public int GetPrice(EquipmentElement e, MobileParty tradingParty, bool isSelling, PartyBase merchantParty)
            {
                int b = _t.MarketData.GetPrice(e, tradingParty, isSelling, merchantParty);
                return isSelling ? Math.Max(1, (int)(b * (1.0 - _up))) : Math.Max(1, (int)Math.Ceiling(b * (1.0 + _up)));
            }
        }

        private static int CashLeft(Town t, int stalls) { return Math.Max(0, Math.Min(t != null ? t.Gold : 0, stalls * CashPer() - _soldToday)); }

        /// <summary>Gotowka markietanow na skup = pieniadz miasta-zrodla, najwyzej to, co zostalo z dziennego limitu kramow. SetGold gra wola tylko w osadzie - tu nie.</summary>
        private sealed class RowListener : InventoryListener
        {
            private readonly Town _t; private readonly int _cash;
            internal RowListener(Town t, int cash) { _t = t; _cash = Math.Max(0, cash); }
            public override int GetGold() { return Math.Max(0, Math.Min(_t.Gold, _cash)); }
            public override TextObject GetTraderName() { return new TextObject("{=!}Sutlers' row"); }
            public override void SetGold(int gold) { }
            public override PartyBase GetOppositeParty() { return _t.Settlement != null ? _t.Settlement.Party : null; }
            public override void OnTransaction() { }
        }

        // ------------------------------------------------------------ doba
        private static void Daily()
        {
            bool any = _dVisits + _dTreated + _dRevels + _dTrades + _dMended + _dClothe + _dLaundry > 0 || _stumbles > 0 || LaundryLeftH() > 0.0;   // m103-drobne: odziez, praczki
            if (any && Settings.Current != null && Settings.Current.LogEnabled)
                Log.Info(P + "dzien " + (Today() - 1) + " | klucz " + (On ? "TAK" : "WYL") + " | wizyty " + _dVisits + " (z cudzej armii " + _dArmyVisits + ") | cyrulik: opatrzeni " + _dTreated + " za " + _dSurgeonGold
                         + " | platnerze: naprawione " + _dMended + " za " + _dMendGold + " (material do kas miast " + _dMendMat + ")"
                         + " | zabawa " + _dRevels + " za " + _dRevelGold + " | sklep " + _dTrades + ": kupione " + _dPcsBought + " sztuk, sprzedane " + _dPcsSold + ", gracz netto " + _dBuyNet
                         + " = towar do kas miast " + _dGoodsTown + " + narzut " + _dMarkup
                         + " | odziez " + _dClothe + ": sztuk " + _dClothePcs + " za " + _dClotheGold + " (towar do kas miast " + _dClotheTown + " + narzut " + (_dClotheGold - _dClotheTown) + ")"   // m103-drobne
                         + " | praczki: najete " + _dLaundry + " za " + _dLaundryGold + ", czynne " + (LaundryLeftH() > 0.0 ? "jeszcze " + LaundryLeftH().ToString("0.0", Inv) + " h (zuzycie -" + LaundryCut() + "%)" : "nie")
                         + " | obrot uslug i narzutu " + _dPaid + " -> kasy miast " + _dToTown + " + lud " + _dToFolk
                         + " + kiesy wsi " + _dToVill + " + danina " + _dToTax + " (w tym korona DS " + _dCrown + ") (roznica " + (_dPaid - _dToTown - _dToFolk - _dToVill - _dToTax) + ") | potkniecia " + _stumbles + ".");
            DayZero();
        }
    }
}
