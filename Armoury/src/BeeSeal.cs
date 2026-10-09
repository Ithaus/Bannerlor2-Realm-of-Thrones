using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// BETTERECONOMY (BEE v1.4.5 "Living Economy") DOMKNIETE - paczka 170 (decyzja Jeffa 08.10, wiazaca: "jak znika to zamykamy, ma byc
    /// logiczny system ekonomii, ze wszystko z czegos wynika"). Specyfikacja: docs/paczki/170-bee-domkniecie.md (decyzje D1-D15,
    /// tabele 2.2 / 2.3, kod 3.x, krytyka rozdz. 11).
    /// Strone AI i swiata zamyka plik ustawien BEE (13 kluczy skryptu tools/bee/zamknij-ujscia-bee.ps1 -ListaZFundamentu + wpisy 18 i 49) -
    /// ta klasa tego nie dubluje. Domyka to, czego klucze nie siegaja; 16 zaczepow Harmony (15 domykajacych + 1 tylko do logu), wszystko
    /// przez refleksje (bez BEE nic sie nie wpina; inna wersja BEE niz v1.4.5 - nic sie nie wpina i log mowi to glosno):
    ///  - 8 postfiksow na CanPlayer* (akcje gracza wrzucajace zloto w nicosc: wplata do skarbca zamku i miasta, zbrojownia,
    ///    inwestycja w miasto 10/50/100 tys., inwestycja we wies 5/15/30 tys., dostep do targu 5 000, oboz szkoleniowy, szkolenie
    ///    wojsk) - wynik false i powod po angielsku; kazde wejscie (menu BEE, pickery BEE, ekrany BK, konsola BK) pyta najpierw Can*,
    ///    a zloto schodzi dopiero po Can* (spec. 4.2), wiec nic nie znika;
    ///  - 1 postfiks na SettlementMenuBehavior.OnSessionLaunched - owija warunki 12 opcji menu BEE: opcja WIDOCZNA i NIEAKTYWNA
    ///    z podpowiedzia (sam postfiks Can* schowalby 7 z nich bez slowa, bo ich warunek = wynik Can*);
    ///  - 1 prefiks + postfiks na LedgerVM.AddToggle (klucz "lordwealth") - przelacznik "Lord wealth realism" w ksiedze BEE pokazany
    ///    jako OFF z powodem, klik nic nie wlacza (user.cfg nietkniety);
    ///  - 5 prefiksow return false (Priority.Last - okna pomiaru 169 biegna przed nimi) na biernych zrodlach z niczego / w nicosc:
    ///    produkcja gotowych zbrojowni (wsad z targu w nicosc, bron z niczego), XP gotowych obozow AI, odblokowana druga produkcja
    ///    wsi, oplata 5 000 za dostep do targu (ogon AI), zdejmowanie zlota panom (WealthAudit);
    ///    oplate AI blokujemy TYLKO, gdy plik BEE zamyka odplyw towaru wsi (K4/K5) - bez nich ta oplata jest jedynym, co odplyw tlumi
    ///    (VDev:628 -> :489), wiec wtedy ja przepuszczamy (mniejsze zlo) i linia kampanii mowi to glosno;
    ///  - 1 postfiks tylko do logu na CompletePlayerTrainingSession - dokonczenie sesji szkolenia oplaconej w starym zapisie (D9,
    ///    nie blokowane) widac w linii doby.
    /// Przelacznik w ksiedze zamykamy tylko, gdy B5 (zdejmowanie zlota panom) jest wpiety - inaczej ksiega zostaje jak w BEE.
    /// Swiadome odstepstwo (D5): ekranow BK (Demesne) i konsoli BK nie latamy - przyciski BK nie maja wiazania IsEnabled, wiec
    /// zostaja klikalne; okno pyta o cene albo kwote, odmowa z powodem pada po zatwierdzeniu, zloto nie schodzi.
    /// Nasz kod NIGDY nie pisze stanu BEE (jedyny zapis do obiektu BEE: RowVM.OnClick - obiekt ekranu); zero kluczy zapisu.
    /// Wylacznik MCM LivingEconomySealed (domyslnie wlaczony): wylaczony = BEE dokladnie jak dawniej.
    /// Log: linia startowa (co wpiete), linia kampanii (menu, wylacznik, tryb zgodnosci BK w BEE, 19 wartosci pliku BEE, user.cfg),
    /// linia doby tylko wtedy, gdy prefiksy cos zablokowaly. Wyjatki: licznik potkniec, latka dziala dalej.
    /// </summary>
    internal static class BeeSeal
    {
        private const string BeeVersion = "v1.4.5";

        // teksty dla gracza (krotkie - w pickerach BEE trafiaja do tytulu "Contribute 10000g - {STATUS}"); bez nawiasow klamrowych
        private const string T_TREASURY = "Closed: gold paid into this treasury would simply vanish - in this economy gold must go to someone.";
        private const string T_ARMORY = "Closed: this armory would make weapons out of nothing - arms come from smiths and workshops.";
        private const string T_TOWN_INVEST = "Closed: this gold would vanish and conjure prosperity and goods out of nothing - invest through the town's buildings instead.";
        private const string T_VILLAGE_INVEST = "Closed: this gold would vanish and conjure homes and peasants out of nothing - in this economy gold must go to someone.";
        private const string T_MARKET = "Closed: the brokers' fee would simply vanish - in this economy gold must go to someone.";
        private const string T_CAMP = "Closed: the camp's cost would simply vanish - in this economy gold must go to someone.";
        private const string T_TRAIN = "Closed: paid drill would burn gold and make experience out of nothing - troops learn by fighting.";
        private const string T_WEALTH = "Closed: gold taken from lords would simply vanish - in this economy a lord's purse changes only by real payments.";
        private const string T_WEALTH_CLICK = "Lord wealth realism is closed: gold taken from lords would simply vanish.";

        private const string L_WEALTH = "zdejmowanie zlota panom";   // etykieta celu B5 (linia kampanii sprawdza, czy wpiety)
        private const string L_MARKET = "oplata za dostep do targu";  // etykieta celu B4 (linia kampanii: wstrzymana przy otwartych K4/K5)
        private const int HookCount = 16;                            // 15 domykajacych + 1 tylko do logu (dokonczenie starej sesji szkolenia)

        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static bool _wealthWired;   // B5 wpiety - tylko wtedy zamykamy przelacznik w ksiedze (inaczej ksiega jak w BEE)

        // pola i wlasciwosci BEE czytane przez refleksje (pobierane raz w ApplyAll; brak = licznik / raport "?", latka dziala)
        private static FieldInfo _fArmoryLevel, _fSecondItem, _fAllRows, _fOnClick, _fBkMode, _fRtLordWealth;
        private static PropertyInfo _pBkActive, _pRtInstance;
        private static Type _tSettings;
        private static readonly Dictionary<string, FieldInfo> _keyFields = new Dictionary<string, FieldInfo>();

        private static readonly Action<bool> _noApply = delegate { ClosedClick(); };

        // liczniki od poprzedniej linii doby (wywolania zablokowane przez prefiksy B1-B5; przepuszczone oplaty AI przy otwartych K4/K5;
        // dokonczone stare sesje szkolenia - tylko log)
        private static int _day = -1, _armory, _camps, _second, _market, _marketPlayer, _wealthN, _stumbles;
        private static int _marketOpen, _trainDone, _trainTroops;
        private static long _wealthGold, _trainXp;
        private static readonly HashSet<string> _stumbleLogged = new HashSet<string>();

        /// <summary>Wylacznik czytany przy kazdym wywolaniu - MCM przepisuje ustawienia co godzine gry (ArmouryBehavior).</summary>
        private static bool On { get { var s = Settings.Current; return s != null && s.LivingEconomySealed; } }

        // ---------------------------------------------------------------- menu BEE: 12 opcji widocznych i nieaktywnych

        /// <summary>Opcja menu BEE do owiniecia. Kind = regula widocznosci przy zamknieciu: 0 = wynik oryginalu (warunek tylko
        /// o wlasnosci), 1 = miasto rodu gracza, 2 = wies nie wroga (BEE IsHostileToPlayer), 3 = wies wlasna albo wies miasta
        /// gracza (BEE IsOwnerEligible). Group: 0 miasto, 1 wies, 2 zamek (do licznika w linii kampanii).</summary>
        private sealed class MenuSpec
        {
            public readonly string Menu, Option, Why;
            public readonly int Kind, Group;
            public MenuSpec(string menu, string option, int kind, int group, string why) { Menu = menu; Option = option; Kind = kind; Group = group; Why = why; }
        }

        private static readonly MenuSpec[] Menus =
        {
            new MenuSpec("bee_town_menu", "bee_town_treasury_contribute", 0, 0, T_TREASURY),
            new MenuSpec("bee_town_menu", "bee_town_armory", 0, 0, T_ARMORY),
            new MenuSpec("bee_town_menu", "bee_town_invest_10k", 1, 0, T_TOWN_INVEST),
            new MenuSpec("bee_town_menu", "bee_town_invest_50k", 1, 0, T_TOWN_INVEST),
            new MenuSpec("bee_town_menu", "bee_town_invest_100k", 1, 0, T_TOWN_INVEST),
            new MenuSpec("bee_village_menu", "bee_village_market_access", 3, 1, T_MARKET),
            new MenuSpec("bee_village_menu", "bee_village_invest_5k", 2, 1, T_VILLAGE_INVEST),
            new MenuSpec("bee_village_menu", "bee_village_invest_15k", 2, 1, T_VILLAGE_INVEST),
            new MenuSpec("bee_village_menu", "bee_village_invest_30k", 2, 1, T_VILLAGE_INVEST),
            new MenuSpec("bee_castle_menu", "bee_castle_treasury_contribute", 0, 2, T_TREASURY),
            new MenuSpec("bee_castle_menu", "bee_castle_training_camp", 0, 2, T_CAMP),
            new MenuSpec("bee_castle_menu", "bee_castle_train_troops", 0, 2, T_TRAIN),
        };

        /// <summary>Owijka warunku opcji menu BEE: oryginal biegnie zawsze (ustawia optionLeaveType); przy wlaczonym wylaczniku opcja
        /// widoczna wedle reguly Kind jest nieaktywna z powodem. Wylacznik wylaczony = dokladnie wynik BEE.</summary>
        private sealed class MenuGate
        {
            private readonly GameMenuOption.OnConditionDelegate _orig;
            private readonly int _kind;
            private readonly TextObject _why;
            private readonly string _id;

            internal MenuGate(GameMenuOption.OnConditionDelegate orig, int kind, TextObject why, string id)
            {
                _orig = orig; _kind = kind; _why = why; _id = id;
            }

            internal bool Condition(MenuCallbackArgs args)
            {
                bool vis = false;
                try { vis = _orig != null && _orig(args); }
                catch (Exception e) { Stumble("menu " + _id, e); vis = false; }
                if (!On) return vis;
                try
                {
                    if (!vis) vis = Visible(_kind);
                    if (vis && args != null) { args.IsEnabled = false; args.Tooltip = _why; }
                }
                catch (Exception e) { Stumble("menu " + _id, e); }
                return vis;
            }
        }

        private static bool Visible(int kind)
        {
            if (kind == 0) return false;
            var s = Settlement.CurrentSettlement;
            var pc = Clan.PlayerClan;
            if (s == null || pc == null) return false;
            switch (kind)
            {
                case 1:
                    return s.IsTown && s.OwnerClan == pc;
                case 2:
                    {
                        if (!s.IsVillage) return false;
                        IFaction mine = pc.MapFaction, theirs = s.MapFaction;
                        return !(mine != null && theirs != null && theirs.IsAtWarWith(mine));
                    }
                case 3:
                    return s.IsVillage && (s.OwnerClan == pc || (s.Village != null && s.Village.Bound != null && s.Village.Bound.OwnerClan == pc));
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- wpiecie (raz na uruchomienie gry)

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear(); _wealthWired = false;
            var tSub = QuartermasterLaw.FindType("BetterEconomy.BetterEconomySubModule");
            if (tSub == null) { Log.Info("BEE domkniecie (170): BetterEconomy nieobecny - nie ma czego domykac."); return; }

            // bramka wersji (D12) - odczyt odporny na zmiane const -> static readonly; wszystko inne niz "v1.4.5" = nic nie wpinamy
            string ver = null;
            try
            {
                var f = tSub.GetField("Version", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                object v = f == null ? null : (f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null));
                ver = v as string;
            }
            catch { ver = null; }
            if (ver != BeeVersion)
            {
                Log.Info("BEE domkniecie (170): BetterEconomy w wersji " + (ver ?? "nieczytelna") + " (latki pisane pod " + BeeVersion
                         + ") - NIC nie wpiete; akcje gracza i bierne zrodla BEE OTWARTE - sprawdzic dekompilacje i klucze pliku (tools/bee/OPIS.md rozdz. 7)");
                return;
            }

            var tTown = QuartermasterLaw.FindType("BetterEconomy.Behaviors.TownEconomyCampaignBehavior");
            var tCastle = QuartermasterLaw.FindType("BetterEconomy.Behaviors.CastleEconomyCampaignBehavior");
            var tVInv = QuartermasterLaw.FindType("BetterEconomy.Behaviors.VillageInvestmentCampaignBehavior");
            var tVDev = QuartermasterLaw.FindType("BetterEconomy.Behaviors.VillageDevelopmentCampaignBehavior");
            var tSmb = QuartermasterLaw.FindType("BetterEconomy.Behaviors.SettlementMenuBehavior");
            var tWealth = QuartermasterLaw.FindType("BetterEconomy.Behaviors.WealthAuditCampaignBehavior");
            var tLedger = QuartermasterLaw.FindType("BetterEconomy.UI.Gauntlet.LedgerVM");
            var tRow = QuartermasterLaw.FindType("BetterEconomy.UI.Gauntlet.RowVM");
            var tTownState = QuartermasterLaw.FindType("BetterEconomy.Core.TownEconomyState");
            var tCastleState = QuartermasterLaw.FindType("BetterEconomy.Core.CastleEconomyState");
            var tVilState = QuartermasterLaw.FindType("BetterEconomy.Core.VillageDevelopmentState");
            var tPop = QuartermasterLaw.FindType("BetterEconomy.Core.SettlementPopulation");
            var tLink = QuartermasterLaw.FindType("BetterEconomy.Core.VillageSupplyLink");
            _tSettings = QuartermasterLaw.FindType("BetterEconomy.Config.BetterEconomySettings");
            var tRt = QuartermasterLaw.FindType("BetterEconomy.Config.RuntimeSettings");
            var tBkAdapter = QuartermasterLaw.FindType("BetterEconomy.Compatibility.BannerKingsAdapter");

            // pola pomocnicze - tylko odczyt (wyjatek: RowVM.OnClick, obiekt ekranu); brak nie blokuje latki
            try
            {
                _fArmoryLevel = tTownState != null ? AccessTools.Field(tTownState, "ArmoryLevel") : null;
                _fSecondItem = tVilState != null ? AccessTools.Field(tVilState, "SecondaryItemId") : null;
                _fAllRows = tLedger != null ? AccessTools.Field(tLedger, "_allRows") : null;
                _fOnClick = tRow != null ? AccessTools.Field(tRow, "OnClick") : null;
                _pBkActive = tBkAdapter != null ? AccessTools.Property(tBkAdapter, "IsActive") : null;
                _fBkMode = _tSettings != null ? AccessTools.Field(_tSettings, "BannerKingsCompatibilityMode") : null;
                _pRtInstance = tRt != null ? AccessTools.Property(tRt, "Instance") : null;
                _fRtLordWealth = tRt != null ? AccessTools.Field(tRt, "LordWealthRealism") : null;
                _keyFields.Clear();
                if (_tSettings != null)
                {
                    foreach (var k in Keys) _keyFields[k.Name] = AccessTools.Field(_tSettings, k.Name);
                    _keyFields[ArmoryList] = AccessTools.Field(_tSettings, ArmoryList);
                    _keyFields[ArmoryAiOnly] = AccessTools.Field(_tSettings, ArmoryAiOnly);
                }
            }
            catch (Exception e) { Log.Error("BeeSeal pola pomocnicze", e); }

            Type S = typeof(Settlement), H = typeof(Hero), I = typeof(int), B = typeof(bool), STR = typeof(string);
            Type RS = typeof(string).MakeByRefType(), RI = typeof(int).MakeByRefType();

            // 8 akcji gracza - postfiksy Can* (Priority.Last)
            Wire(h, tCastle, "CanPlayerContributeTreasury", new[] { S, H, I, RS }, null, "TreasuryPost", true, "wplata do skarbca zamku");
            Wire(h, tTown, "CanPlayerContributeTreasury", new[] { S, H, I, RS }, null, "TreasuryPost", true, "wplata do skarbca miasta");
            Wire(h, tTown, "CanPlayerBuildOrUpgradeArmory", new[] { S, H, RS }, null, "ArmoryPost", true, "zbrojownia gracza");
            Wire(h, tTown, "CanPlayerInvest", new[] { S, H, I, RS }, null, "TownInvestPost", true, "inwestycja w miasto");
            Wire(h, tVInv, "CanPlayerInvest", new[] { S, H, I, RS }, null, "VillageInvestPost", true, "inwestycja we wies");
            Wire(h, tVDev, "CanPlayerNegotiateMarketAccess", new[] { S, H, RS }, null, "MarketPost", true, "dostep do targu (gracz)");
            Wire(h, tCastle, "CanPlayerBuildOrUpgradeTrainingCamp", new[] { S, H, RS }, null, "CampPost", true, "oboz szkoleniowy");
            Wire(h, tCastle, "CanPlayerStartTraining", new[] { S, H, RS }, null, "TrainPost", true, "szkolenie wojsk");

            // menu BEE (12 opcji) i przelacznik w ksiedze - priorytet domyslny
            Wire(h, tSmb, "OnSessionLaunched", new[] { typeof(CampaignGameStarter) }, null, "SessionPost", false, "menu BEE (12 opcji)");
            Wire(h, tLedger, "AddToggle", new[] { STR, STR, STR, B, typeof(Action<bool>), STR }, "TogglePre", "TogglePost", false, "przelacznik Lord wealth realism");

            // 5 biernych zrodel - prefiksy return false (Priority.Last)
            Wire(h, tTown, "TickArmoryProduction", new[] { S, tTownState, I }, "ArmoryPre", null, true, "produkcja gotowych zbrojowni");
            Wire(h, tCastle, "ApplyAiTrainingCampPassive", new[] { S, tCastleState }, "CampPre", null, true, "XP gotowych obozow AI");
            Wire(h, tVDev, "TickSecondaryProduction", new[] { S, tVilState, tPop, tLink, I }, "SecondPre", null, true, "druga produkcja wsi");
            Wire(h, tVDev, "ApplyMarketAccess", new[] { S, H, B }, "MarketPre", null, true, L_MARKET);
            Wire(h, tWealth, "TryRemoveHeroGold", new[] { H, I }, "WealthPre", null, true, L_WEALTH);
            _wealthWired = _wired.Contains(L_WEALTH);

            // tylko log (D9 - nie blokujemy): dokonczenie sesji szkolenia oplaconej w starym zapisie (XP z niczego, Castle:474 -> :879)
            Wire(h, tCastle, "CompletePlayerTrainingSession", new[] { S, tCastleState, RI, RI, RI, RI }, null, "TrainDonePost", false,
                 "dokonczenie starej sesji szkolenia (tylko log)");

            Log.Info("BEE domkniecie (170): BetterEconomy " + ver + " - wpiete " + _wired.Count + "/" + HookCount + ": " + string.Join(", ", _wired.ToArray())
                     + (_missing.Count > 0 ? " | BRAK: " + string.Join(", ", _missing.ToArray()) + " (te sciezki BEE BEZ ZMIAN)" : "")
                     + "; stan wylacznika i trybu zgodnosci BK - w linii \"BEE domkniecie (170): kampania\" (zmiana wylacznika w MCM dziala w ciagu godziny gry); linie dnia \"BEE domkniecie (170) doba\".");
        }

        private static void Wire(Harmony h, Type t, string method, Type[] args, string prefix, string postfix, bool last, string label)
        {
            try
            {
                if (t == null) { _missing.Add(label + " (brak typu)"); return; }
                foreach (var a in args) if (a == null) { _missing.Add(label + " (brak typu parametru " + method + ")"); return; }
                var m = AccessTools.Method(t, method, args);
                if (m == null) { _missing.Add(label + " (brak " + t.Name + "." + method + ")"); return; }
                HarmonyMethod pre = null, post = null;
                if (prefix != null) { pre = new HarmonyMethod(typeof(BeeSeal), prefix); if (last) pre.priority = Priority.Last; }
                if (postfix != null) { post = new HarmonyMethod(typeof(BeeSeal), postfix); if (last) post.priority = Priority.Last; }
                h.Patch(m, prefix: pre, postfix: post);
                _wired.Add(label);
            }
            catch (Exception e)
            {
                _missing.Add(label + " (" + e.Message + ")");
                Log.Error("BeeSeal.Wire " + label, e);
            }
        }

        // ---------------------------------------------------------------- akcje gracza: postfiksy Can* (nic nie licza)

        private static void Close(ref bool result, ref string reason, string why)
        {
            if (!On) return;
            try { result = false; reason = why; }
            catch (Exception e) { Stumble("Can*", e); }
        }

        private static void TreasuryPost(ref bool __result, ref string reason) { Close(ref __result, ref reason, T_TREASURY); }
        private static void ArmoryPost(ref bool __result, ref string reason) { Close(ref __result, ref reason, T_ARMORY); }
        private static void TownInvestPost(ref bool __result, ref string reason) { Close(ref __result, ref reason, T_TOWN_INVEST); }
        private static void VillageInvestPost(ref bool __result, ref string reason) { Close(ref __result, ref reason, T_VILLAGE_INVEST); }
        private static void MarketPost(ref bool __result, ref string reason) { Close(ref __result, ref reason, T_MARKET); }
        private static void CampPost(ref bool __result, ref string reason) { Close(ref __result, ref reason, T_CAMP); }
        private static void TrainPost(ref bool __result, ref string reason) { Close(ref __result, ref reason, T_TRAIN); }

        // ---------------------------------------------------------------- menu BEE: owiniecie po dodaniu opcji (start / wczytanie kampanii)

        private static void SessionPost()
        {
            try
            {
                Reset();
                int[] per = new int[3];
                var miss = new List<string>();
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                foreach (var spec in Menus)
                {
                    try
                    {
                        var menu = mgr != null ? mgr.GetGameMenu(spec.Menu) : null;
                        GameMenuOption opt = null;
                        if (menu != null)
                            foreach (var o in menu.MenuOptions)
                                if (o != null && o.IdString == spec.Option) { opt = o; break; }
                        if (opt == null) { miss.Add(spec.Option); continue; }
                        // ochrona przed podwojnym owinieciem
                        if (!(opt.OnCondition != null && opt.OnCondition.Target is MenuGate))
                            opt.OnCondition = new GameMenuOption.OnConditionDelegate(new MenuGate(opt.OnCondition, spec.Kind, new TextObject(spec.Why), spec.Option).Condition);
                        per[spec.Group]++;
                    }
                    catch (Exception e) { miss.Add(spec.Option + " (" + e.Message + ")"); Stumble("menu " + spec.Option, e); }
                }
                Log.Info(CampaignLine(per, miss));
            }
            catch (Exception e) { Stumble("menu BEE", e); }
        }

        // ---------------------------------------------------------------- ksiega BEE: przelacznik "Lord wealth realism"

        /// <summary>Zamykamy przelacznik tylko przy wpietym B5 - bez niego BEE dalej zdejmuje zloto panom, a ksiega musi pokazywac
        /// prawdziwy stan i dawac go wylaczyc (linia kampanii mowi wtedy "NIEWPIETA").</summary>
        private static void TogglePre(string key, ref string hint, ref bool current, ref Action<bool> apply)
        {
            if (key != "lordwealth" || !_wealthWired || !On) return;
            try { hint = T_WEALTH; current = false; apply = _noApply; }
            catch (Exception e) { Stumble("ksiega lordwealth", e); }
        }

        private static void TogglePost(object __instance, string key)
        {
            if (key != "lordwealth" || !_wealthWired || !On) return;
            try
            {
                // wiersz dodany wlasnie przez AddToggle - klik pokazuje komunikat i NIE przestawia napisu na ON
                var rows = _fAllRows != null ? _fAllRows.GetValue(__instance) as IList : null;
                if (rows != null && rows.Count > 0 && _fOnClick != null) _fOnClick.SetValue(rows[rows.Count - 1], (Action)ClosedClick);
            }
            catch (Exception e) { Stumble("ksiega lordwealth", e); }
        }

        /// <summary>Klik przelacznika: tylko komunikat; RuntimeSettings.Save nie jest wolane - user.cfg nietkniety.</summary>
        private static void ClosedClick()
        {
            try { Log.Player(T_WEALTH_CLICK); } catch { }
        }

        // ---------------------------------------------------------------- bierne zrodla: prefiksy return false (Priority.Last)

        /// <summary>B1: gotowa zbrojownia - wsad z targu w nicosc i bron z niczego. Licznik = wywolania w miastach z gotowa zbrojownia
        /// (oryginal i tak wychodzi przy poziomie 0, braku wsadu albo gdy nic nie zuzyl).</summary>
        private static bool ArmoryPre(object state)
        {
            if (!On) return true;
            try
            {
                Tick();
                if (state != null && _fArmoryLevel != null && Convert.ToInt32(_fArmoryLevel.GetValue(state), CultureInfo.InvariantCulture) > 0) _armory++;
            }
            catch (Exception e) { Stumble("produkcja zbrojowni", e); }
            return false;
        }

        /// <summary>B2: XP z niczego dla garnizonu zamku AI z gotowym obozem. Licznik = wywolania (takze pusty garnizon).</summary>
        private static bool CampPre()
        {
            if (!On) return true;
            try { Tick(); _camps++; }
            catch (Exception e) { Stumble("XP obozow AI", e); }
            return false;
        }

        /// <summary>B3: druga produkcja wsi - do 8 szt. na tick z niczego. Licznik = wywolania we wsiach z odblokowana druga produkcja
        /// (bez warunku IsSecondaryActive - jego wywolanie moze tworzyc stan BEE).</summary>
        private static bool SecondPre(object state)
        {
            if (!On) return true;
            try
            {
                Tick();
                if (state != null && _fSecondItem != null && !string.IsNullOrEmpty(_fSecondItem.GetValue(state) as string)) _second++;
            }
            catch (Exception e) { Stumble("druga produkcja wsi", e); }
            return false;
        }

        /// <summary>B4: oplata 5 000 za dostep do targu w nicosc + relacje z niczego. Wolajacy AI i tak ustawia swoj cooldown.
        /// Oplate AI blokujemy tylko przy zamknietym odplywie towaru wsi (K4/K5 w pliku BEE): bez nich jedynym hamulcem odplywu (towar
        /// z niczego na targ obcego miasta, VDev:511 -> :551) jest wlasnie ta oplata (DiversionSuppressedUntilDay, VDev:628 -> :484-489),
        /// wiec wtedy ja przepuszczamy i liczymy osobno. Gracz dochodzi tu tylko po Can* (G6 zamkniety) - jego wywolanie blokujemy zawsze.
        /// K4/K5 czytane przy kazdym wywolaniu (rzadkie: cooldown AI) - Ctrl+Shift+M w BEE moze przeladowac plik w trakcie gry.</summary>
        private static bool MarketPre(bool player)
        {
            if (!On) return true;
            try
            {
                Tick();
                if (!player && !DiversionClosed()) { _marketOpen++; return true; }
                _market++; if (player) _marketPlayer++;
            }
            catch (Exception e) { Stumble("oplata za dostep do targu", e); }
            return false;
        }

        /// <summary>Tylko log (D9, nie blokujemy): BEE dokonczyl sesje szkolenia gracza oplacona przed zamknieciem - XP z niczego.
        /// Liczone zawsze (takze przy wylaczonym wylaczniku), bo to prawdziwy skutek w armii gracza.</summary>
        private static void TrainDonePost(ref int trained, ref int xpTotal)
        {
            try
            {
                Tick();
                _trainDone++;
                if (trained > 0) _trainTroops += trained;
                if (xpTotal > 0) _trainXp += xpTotal;
            }
            catch (Exception e) { Stumble("dokonczenie starej sesji szkolenia", e); }
        }

        /// <summary>B5: WealthAudit - zloto panow AI w nicosc. Wynik false: wolajacy nie liczy pana jako "affected".</summary>
        private static bool WealthPre(ref bool __result, int amount)
        {
            if (!On) return true;
            try { Tick(); _wealthN++; if (amount > 0) _wealthGold += amount; __result = false; }
            catch (Exception e) { Stumble(L_WEALTH, e); __result = false; }
            return false;
        }

        // ---------------------------------------------------------------- liczniki i linie logu

        /// <summary>Nowa kampania albo wczytanie (SessionPost): liczniki od zera, bez linii za poprzednia kampanie.</summary>
        private static void Reset()
        {
            _day = -1;
            Zero();
        }

        private static void Zero()
        {
            _armory = _camps = _second = _market = _marketPlayer = _wealthN = _stumbles = 0;
            _marketOpen = _trainDone = _trainTroops = 0;
            _wealthGold = _trainXp = 0;
        }

        /// <summary>Leniwa linia doby: pierwsze wywolanie prefiksu w nowej dobie wypisuje poprzednia (tylko gdy cos zablokowano).</summary>
        private static void Tick()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _day) { Flush(); _day = d; }
        }

        private static void Flush()
        {
            try
            {
                long sum = (long)_armory + _camps + _second + _market + _wealthN + _marketOpen + _trainDone;
                if (sum > 0 || _stumbles > 0)
                    Log.Info("BEE domkniecie (170) doba " + _day + ": wywolania zablokowane - produkcja zbrojowni: " + _armory
                             + " (miasta z gotowa zbrojownia), XP obozow AI: " + _camps + " (zamki AI z gotowym obozem), druga produkcja: " + _second
                             + " (wsie z odblokowana druga produkcja), oplata za dostep do targu: " + _market + " (w tym gracz " + _marketPlayer
                             + "), zdejmowanie zlota panom: " + _wealthN + " razy, " + _wealthGold + " zl"
                             + (_marketOpen > 0 ? "; PRZEPUSZCZONE oplaty AI za dostep do targu (K4/K5 otwarte - tylko oplata tlumi odplyw towaru z niczego): " + _marketOpen : "")
                             + (_trainDone > 0 ? "; dokonczone stare sesje szkolenia (D9, nie blokowane, XP z niczego): " + _trainDone + ", zolnierzy " + _trainTroops + ", XP " + _trainXp : "")
                             + (_stumbles > 0 ? "; potkniecia " + _stumbles : ""));
            }
            catch { }
            Zero();
        }

        /// <summary>Licz potkniecia, nie gas funkcji: pierwszy wyjatek kazdego miejsca do logu bledow, kolejne tylko licznik.</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_stumbleLogged.Add(where)) Log.Error("BeeSeal " + where, e); } catch { }
        }

        // ---------------------------------------------------------------- linia kampanii: 19 wartosci pliku BEE (tylko odczyt)

        private const int Ge = 0, Le = 1, Eq = 2;
        private const string ArmoryList = "ArmoryRequiredArtisans", ArmoryAiOnly = "ArmoryAiCheckCooldownDays";

        private sealed class KeyRule
        {
            public readonly string Name, Shown;
            public readonly int Op;
            public readonly double Target;
            public KeyRule(string name, int op, double target, string shown) { Name = name; Op = op; Target = target; Shown = shown; }
        }

        // K4/K5 - odplyw towaru wsi z niczego (VDev:489); od nich zalezy tez B4 (MarketPre). Deklarowane przed Keys (kolejnosc inicjalizacji).
        private static readonly KeyRule K4 = new KeyRule("VillageDiversionRelationThreshold", Le, -101, "-101");
        private static readonly KeyRule K5 = new KeyRule("VillageDiversionGrievanceThreshold", Ge, 101, "101");

        // 18 wartosci + zbrojownia (wariant b / a) = 19; progi jak w skrypcie zamknij-ujscia-bee.ps1 i wpisach 18 / 49
        private static readonly KeyRule[] Keys =
        {
            new KeyRule("CastleAiLeaderReserveGold", Ge, 1e9, "1000000000"),
            new KeyRule("LordInvestmentReserveFlat", Ge, 1e9, "1000000000"),
            new KeyRule("VillageSecondaryRequiredStableDays", Ge, 1e9, "1000000000"),
            new KeyRule("CaravanDeliveryMinGold", Ge, 1e9, "1000000000"),
            new KeyRule("CaravanEscortHireMinGold", Ge, 1e9, "1000000000"),
            K4,
            K5,
            new KeyRule("CaravanRecruitPromotionEnabled", Eq, 0, "0"),
            new KeyRule("RouteDangerMaxLossRatio", Le, 0, "0"),
            new KeyRule("TradeAgreementCustomsMin", Le, 0, "0"),
            new KeyRule("TradeAgreementCorridorProsperityPerDay", Le, 0, "0"),
            new KeyRule("RaidPeasantFlightFraction", Le, 0, "0"),
            new KeyRule("WorkshopDailyInputDrawPerShop", Le, 0, "0"),
            new KeyRule("VillageSecondaryBoundTownShare", Le, 0, "0"),
            new KeyRule("TownTreasurySurplusPayoutFraction", Le, 0, "0"),
            new KeyRule("TownTreasuryWarPayoutFraction", Le, 0, "0"),
            new KeyRule("TradeAgreementCustomsRate", Le, 0, "0"),
            new KeyRule("EstateOwnerPayoutFraction", Le, 0, "0"),
        };

        /// <summary>Wartosc pola statycznego BEE jako liczba (null = brak pola albo blad odczytu).</summary>
        private static double? ReadKey(string name, out string shown)
        {
            shown = "?";
            try
            {
                FieldInfo f;
                if (!_keyFields.TryGetValue(name, out f) || f == null) return null;
                object v = f.GetValue(null);
                if (v == null) return null;
                shown = Convert.ToString(v, CultureInfo.InvariantCulture);
                return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        /// <summary>Wartosc klucza spelnia warunek zamkniecia (brak pola albo blad odczytu = NIE).</summary>
        private static bool KeyClosed(KeyRule k, out string shown)
        {
            double? v = ReadKey(k.Name, out shown);
            return v.HasValue && (k.Op == Ge ? v.Value >= k.Target : k.Op == Le ? v.Value <= k.Target : v.Value == k.Target);
        }

        /// <summary>Odplyw towaru wsi z niczego zamkniety plikiem BEE (K4 i K5) - warunek blokowania oplaty AI w B4.</summary>
        private static bool DiversionClosed()
        {
            string s;
            return KeyClosed(K4, out s) && KeyClosed(K5, out s);
        }

        private static string KeysText()
        {
            if (_tSettings == null) return "? (brak BetterEconomySettings)";
            int closed = 0;
            var open = new List<string>();
            foreach (var k in Keys)
            {
                string shown;
                bool ok = KeyClosed(k, out shown);
                if (ok) closed++;
                else open.Add(k.Name + "=" + shown + " (ma byc " + k.Shown + ")");
            }
            string variant;
            string sList, sAi;
            double? list = ReadKey(ArmoryList, out sList), ai = ReadKey(ArmoryAiOnly, out sAi);
            if (list.HasValue && list.Value >= 1e9) { variant = "b"; closed++; }
            else if (ai.HasValue && ai.Value >= 1e9) { variant = "a"; closed++; }
            else { variant = "brak"; open.Add(ArmoryList + "=" + sList + " (ma byc 1000000000)"); }
            return "zamkniete " + closed + "/19 (wariant zbrojowni: " + variant + ")" + (open.Count > 0 ? "; OTWARTE: " + string.Join(", ", open.ToArray()) : "");
        }

        private static string BkModeText()
        {
            if (_pBkActive == null) return "?";
            try
            {
                object v = _pBkActive.GetValue(null, null);
                if (!(v is bool)) return "?";
                if ((bool)v) return "TAK";
                string mode = "?";
                try { if (_fBkMode != null) mode = Convert.ToString(_fBkMode.GetValue(null), CultureInfo.InvariantCulture); } catch { }
                return "NIE - OTWARTE: LordInvestment AI z zaniedbaniem, WorkshopProductionPatch, Population/Migration i modele BEE czynne (BannerKingsCompatibilityMode="
                       + mode + ") - sprawdzic kolejnosc ladowania: BK przed BEE";
            }
            catch (Exception e) { Stumble("tryb zgodnosci BK", e); return "?"; }
        }

        private static string WealthCfgText()
        {
            try
            {
                if (_pRtInstance == null || _fRtLordWealth == null) return "?";
                object inst = _pRtInstance.GetValue(null, null);   // RuntimeSettings.Load tylko czyta user.cfg
                object v = inst != null ? _fRtLordWealth.GetValue(inst) : null;
                return v is bool ? ((bool)v ? "1" : "0") : "?";
            }
            catch (Exception e) { Stumble("user.cfg", e); return "?"; }
        }

        private static string CampaignLine(int[] per, List<string> miss)
        {
            var sb = new StringBuilder("BEE domkniecie (170): kampania - menu BEE: zamkniete z powodem ");
            sb.Append(per[0] + per[1] + per[2]).Append("/12 (bee_town_menu ").Append(per[0]).Append("/5, bee_village_menu ").Append(per[1])
              .Append("/4, bee_castle_menu ").Append(per[2]).Append("/3)");
            if (miss.Count > 0) sb.Append("; BRAK: ").Append(string.Join(", ", miss.ToArray()));
            sb.Append("; wylacznik: ").Append(On ? "wlaczony" : "wylaczony");
            sb.Append("; tryb zgodnosci BK w BEE: ").Append(BkModeText());
            sb.Append("; klucze pliku BEE: ").Append(KeysText());
            if (_wired.Contains(L_MARKET) && !DiversionClosed())
                sb.Append("; B4 WSTRZYMANA - K4/K5 otwarte: oplata AI 5 000 za dostep do targu PRZEPUSZCZANA (zloto w nicosc), bo bez K4/K5 tylko ona tlumi odplyw towaru wsi z niczego - naprawa: skrypt kluczy, nie kod");
            sb.Append("; user.cfg LordWealthRealism=").Append(WealthCfgText());
            if (!_wealthWired) sb.Append(" (latka 170 na zdejmowanie zlota NIEWPIETA - przy 1 BEE zdejmuje zloto panom w nicosc; przelacznik w ksiedze BEE zostaje jak w BEE)");
            else if (!On) sb.Append(" (latka 170 wylaczona wylacznikiem - przy 1 BEE zdejmuje zloto panom w nicosc)");
            else sb.Append(" (efekt i tak zamyka latka 170)");
            return sb.ToString();
        }
    }
}
