using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E8/D1 (256, PLAN 8.18 a; projekt etapu 8 v2 rozdz. 2 R1, R2, R11, R14 i rozdz. 3 "E8/D1"; krytyka W1, W2, W3, K2-12) - NAGRODY I WYPLATY W ZADANIACH.
    ///
    /// Dzis (sprawdzone skanem IL zywych bibliotek gry 1.4.8: 84 metody, 88 wywolan w TaleWorlds.CampaignSystem, SandBox, ROT, NavalDLC): prawie kazda
    /// nagroda zadania to GiveGoldAction.ApplyBetweenCharacters(null, gracz, kwota) - zloto z niczego, w starej skali (kryjowka 3 000 = 7 miesiecy
    /// dochodu rycerza), a to, co gracz placi w zadaniu (koszt rozwiazania zastepczego, lapowka, oddane podatki), idzie w nicosc; towar dostaw znika.
    ///
    /// Jak: przy starcie pierwszej sesji (w kampanii - nie w SubModuleMain: CLAUDE.md 7, konstruktory statyczne cudzych klas) skan typow IssueBase / QuestBase
    /// i ich domkniec we wszystkich zestawach; w metodach, ktorych IL wola GiveGoldAction.ApplyBetweenCharacters / ApplyForCharacterToParty albo
    /// Hero.ChangeHeroGold, transpiler zamienia SAMO wywolanie na wspolny wrapper etapu 8 (GiverPay.PayFromGiver / PayToParty / HeroGold - te same stosy),
    /// prefiks kladzie ramke z obiektem zadania ([ThreadStatic], zagniezdzenie przez Prev), finalizer ja zdejmuje (takze po wyjatku). Wrapper pyta
    /// QuestPay8.Route: poza ramka albo dla innego kierunku niz "nic -> gracz" / "gracz -> nic" wywolanie gry biegnie bez zmian.
    ///   nic -> gracz (QuestRewardFromGiver): placi zleceniodawca (IssueOwner / QuestGiver) ze swojej kiesy, najwyzej tyle, ile ma (brak = mniej, komunikat,
    ///     licznik - bez doplat); kontroferte placi ten, kto ja sklada (tabela Sites); RevenueFarming: wplata godzinowa z kiesy wsi (do jej wysokosci).
    ///   skala (QuestRewardInWageDays, R2): kwota = min(kwota gry, N ludzi x D dni x zold na czlowieka druzyny gracza x 1.25) - N i D z rozwiazania
    ///     zastepczego gry, dla zadan bez niego druzyna gracza x dni z tabeli Kinds; dostawy: skalowana tylko stala czesc "za fatyge" (K), wartosc towaru
    ///     zostaje. Liczona w wrapperze (obejmuje stale kwoty i kontroferty); getter RewardGold dostaje postfiks z ta sama liczba (oferta w rozmowie).
    ///     Kwota ZAMROZONA przy przyjeciu zadania albo wyslaniu ludzi (slownik zadanie -> kwota, zapis arm_e8_questpay z wersja); bez wpisu (stary zapis)
    ///     liczona przy wyplacie.
    ///   gracz -> nic (QuestPlayerPaysSomeone): koszt rozwiazania zastepczego i lapowki - kasa osady zadania; RevenueFarming (zasadzka, oplata zbrojnych)
    ///     - kiesa wsi; reszta - zleceniodawca. Kwota bez zmian.
    ///   towar (QuestGoodsReal, tabela GoodsSites): dostawa zdjeta graczowi idzie do taboru zleceniodawcy-lorda albo na polke osady zadania; rzecz na
    ///     wymiane (narzedzia - wyrob wsi, zwierzeta - mieso) z magazynu osady, brak - zloto od zleceniodawcy; bron zatrzymana przez straz - na polke miasta.
    /// Wylaczone klucze = stan sprzed (wrapper wola gre, liczniki licza "z niczego" / "w nicosc"). Wyjatek w naszym kodzie = wywolanie gry + licznik
    /// potkniec (pierwszy na miejsce do logu), nic nie gasimy. Stan: liczniki doby i slownik kwot (Reset z WorldMeasure.Reset, slownik w zapisie).
    /// </summary>
    internal static class QuestPay8
    {
        private const float Premium = 1.25f;      // narzut na robocizne (przyklady projektu: 10 x 7 x 5 -> 438)
        private const int FallbackRate = 5;       // zold na czlowieka, gdy druzyna gracza nie ma zoldu (sam bohater)
        private const int DefaultDays = 7;        // zadanie bez rozwiazania zastepczego i bez wpisu w tabeli: druzyna gracza x 7 dni
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const BindingFlags AnyDeclared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // ------------------------------------------------------------ tabele (R2: typ zadania -> N x D; kto placi / kto dostaje)

        /// <summary>K >= 0: dostawa - stala czesc nagrody K to praca (skalowana), reszta to wartosc towaru / ludzi (bez skali, placi odbiorca).
        /// PartyDays > 0: zadanie bez rozwiazania zastepczego - N = druzyna gracza, D = PartyDays. NoScale: kwota gry (sam platnik).</summary>
        private sealed class Kind { public int K = -1; public int PartyDays; public bool NoScale; }

        private static Kind Delivery(int k) { return new Kind { K = k }; }
        private static Kind Party(int days) { return new Kind { PartyDays = days }; }

        private static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>(StringComparer.Ordinal)
        {
            // dostawy (wzory gry: stala + wartosc towaru) - G VillageNeedsTools :RewardGold 500 + szt. x cena, LordNeedsHorses 500 + 1.5 x szt. x wartosc,
            // HeadmanVillageNeedsDraughtAnimals 500 + wartosc x szt., GangLeaderNeedsWeapons 500 + cena x szt., GangLeaderNeedsRecruits 2000 + 100 / rekruta,
            // LordNeedsGarrisonTroops 1500 + 1.5 x koszt werbunku, VillageNeedsCraftingMaterials 750 + cena x szt.
            { "VillageNeedsTools", Delivery(500) }, { "LordNeedsHorses", Delivery(500) }, { "HeadmanVillageNeedsDraughtAnimals", Delivery(500) },
            { "GangLeaderNeedsWeapons", Delivery(500) }, { "GangLeaderNeedsRecruits", Delivery(2000) }, { "LordNeedsGarrisonTroops", Delivery(1500) },
            { "VillageNeedsCraftingMaterials", Delivery(750) },
            // sama wartosc towaru / ludzi (bez czesci stalej): jency po 50, towar x 1.5 sredniej ceny, zboze i bydlo armii po wartosci
            { "LandLordNeedsManualLaborers", Delivery(0) }, { "ArtisanOverpricedGoods", Delivery(0) }, { "ArmyNeedsSupplies", Delivery(0) },
            // podatki wsi: wplata godzinowa z kiesy wsi, oddanie sumy zleceniodawcy - to nie nagroda za prace
            { "RevenueFarming", new Kind { NoScale = true } },
            // bez rozwiazania zastepczego: druzyna gracza x dni [szacunek projektu: rywal 14, podboj 30]
            { "LordWantsRivalCaptured", Party(14) }, { "TheConquestOfSettlement", Party(30) }, { "RaidAnEnemyTerritory", Party(10) },
            { "LandLordCompanyOfTrouble", Party(7) }, { "LadysKnightOut", Party(7) }, { "BettingFraud", Party(5) },
        };

        internal enum Who { Giver, Settlement, Here, Village, Field, Conv }

        /// <summary>Wyjatek od reguly domyslnej dla metody zadania (nazwa = poczatek nazwy metody; domkniecia maja nazwe "&lt;Metoda&gt;b__N").</summary>
        /// Mult > 1: kontroferta, ktora gra liczy jako wielokrotnosc kwoty zadania ("kwota zadania x 2" w tekscie rozmowy) - skala tej wyplaty to kwota
        /// zamrozona x Mult (recenzja E8/D1: bez tego gracz slyszal "2 x kwota po skali", a dostawal 1 x).
        internal sealed class Site { public string Stem, Method; public bool In; public Who Who; public string Field; public int Mult = 1; }

        private static Site In(string stem, string method, Who who, string field = null, int mult = 1) { return new Site { Stem = stem, Method = method, In = true, Who = who, Field = field, Mult = mult }; }
        private static Site Out(string stem, string method, Who who) { return new Site { Stem = stem, Method = method, In = false, Who = who }; }

        private static readonly Site[] Sites =
        {
            // nic -> gracz: placi ten, kto sklada oferte (nie zleceniodawca)
            In("LandLordCompanyOfTrouble", "persuasion_complete_with_company_of_trouble_on_consequence", Who.Conv),   // lord kupuje kompanie (G :641)
            In("LordWantsRivalCaptured", "QuestFailCounterOfferAccepted", Who.Field, "_targetHero", 2),               // rywal placi za wolnosc RewardGold x 2 (G :612, :619)
            In("GangLeaderNeedsToOffloadStolenGoods", "SucceedQuestByPayingAndGivingTheGoodsBack", Who.Field, "_counterOfferHero"),   // kupiec odkupuje towar (G :733)
            In("GangLeaderNeedsToOffloadStolenGoods", "FailQuestByGivingBackTheGoods", Who.Field, "_counterOfferHero"),               // (G :767)
            In("BettingFraud", "MainHeroAcceptsCounterOffer", Who.Field, "_counterOfferNotable"),                     // notabl placi za zdrade (G :797)
            In("RivalGangMovingIn", "OnBattleWonWithBetrayal", Who.Field, "_rivalGangLeader", 2),                     // rywal herszta placi _rewardGold x 2 (SB :861, :1432)
            In("SnareTheWealthy", "OnFail2", Who.Field, "_targetMerchantCharacter"),                                  // ostrzezony kupiec (SB)
            In("RevenueFarming", "ProgressRevenueCollectionForVillage", Who.Village),                                 // wplata godzinowa z kiesy wsi (G :611)
            // gracz -> nic
            Out("RevenueFarming", "<AddVillageEvents>", Who.Village),            // strata w zasadzce i oplata zbrojnych - do kiesy wsi (G :1041, :1077)
            Out("RuralNotableInnAndOut", "PlayerPaid1000QuestSuccess", Who.Here),   // wykup zastawu u karczmarza - kasa osady, w ktorej stoi gracz (SB)
        };

        // klasy zachowan (nie zadan), w ktorych wyplata siedzi w domknieciu menu - tylko metody z tabeli Sites
        private static readonly string[][] ExtraTypes = { new[] { "TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior", "RevenueFarming" } };

        private const byte GNone = 0, GSink = 1, GFromShelf = 2, GKept = 3, GExchange = 4;

        /// <summary>Towar zadan (typ zagniezdzony, metoda, regula, pole). Tylko miejsca, w ktorych towar ginie albo powstaje - przeniesienia gry
        /// (ArmyNeedsSupplies zboze, HeadmanNeedsToDeliverAHerd do osady, LordNeedsHorses.GiveMounts, GiveItemAction) zostaja.</summary>
        private static readonly string[][] GoodsSites =
        {
            new[] { "VillageNeedsToolsIssueQuest", "FinishQuestSuccess1", "1", null },                               // narzedzia -> polka wsi (G :594)
            new[] { "ArtisanOverpricedGoodsIssueQuest", "DeliverItemsPartiallyOnConsequence", "1", null },           // towar -> polka miasta rzemieslnika
            new[] { "ArtisanOverpricedGoodsIssueQuest", "DeliverItemsFullyOnConsequence", "1", null },
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssueQuest", "OnCompleteWithSuccess", "1", null },             // zwierzeta -> polka wsi
            new[] { "GangLeaderNeedsWeaponsIssueQuest", "QuestSuccessDeleteWeaponsFromPlayer", "1", null },          // bron oddana bandzie -> polka miasta (G :1136)
            new[] { "GangLeaderNeedsWeaponsIssueQuest", "OnSettlementLeft", "3", "_weaponsThatGuardTook" },          // bron zatrzymana przez straz (G :1118 -> :642-671)
            new[] { "GangLeaderNeedsWeaponsIssueQuest", "PlayerSuccessfullyDeliveredWeapons", "3", "_weaponsThatGuardTook" },   // (G :1179 Clear)
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssue", "AlternativeSolutionEndWithSuccessConsequence", "2", null },      // mieso na wymiane - z magazynu wsi
            new[] { "HeadmanVillageNeedsDraughtAnimalsIssueQuest", "ApplyRewards", "2", null },
            new[] { "VillageNeedsToolsIssueQuest", "GiveTradeOrExchangeRewardToMainParty", "4", null },              // wyrob wsi na wymiane (G :618-628)
        };

        // ------------------------------------------------------------ stan

        internal sealed class MRec
        {
            public MethodBase M; public string DeclStem, Label, Asm;
            public Site In, Out; public byte Goods; public string GoodsField;
            public int Gold, GoodsCalls; public bool Patched;
            public FieldInfo KeptFi; public bool KeptTried;
        }

        /// <summary>Ramka zalatanej metody zadania (stan prefiksu; zdejmowana w finalizerze).</summary>
        public sealed class Frame
        {
            internal MRec R; internal object Inst; internal Frame Prev; internal long Seq;
            internal bool Resolved; internal IssueBase Issue; internal QuestBase Quest; internal string Stem; internal Hero GiverOverride;
            internal Dictionary<EquipmentElement, int> Snap, Back;
        }

        /// <summary>Kwota obiecana przy przyjeciu. Stamp = godzina kampanii przyjecia (tylko pamiec; -1 po wczytaniu) - drugie zdarzenie tego samego
        /// przyjecia nie liczy od nowa, a ponowne przyjecie tej samej sprawy (po nieudanej probie) - tak.</summary>
        private sealed class Pledge { public string Key, Stem; public int Cap, Game, Labor, N; public float D, Rate; public int Stamp = -1; }

        [ThreadStatic] private static Frame _top;
        [ThreadStatic] private static bool _raw;

        private static readonly Dictionary<IntPtr, MRec> _recs = new Dictionary<IntPtr, MRec>();
        private static readonly List<MRec> _all = new List<MRec>();
        private static readonly Dictionary<string, Pledge> _pledges = new Dictionary<string, Pledge>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KeyValuePair<int, int>> _capNow = new Dictionary<string, KeyValuePair<int, int>>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, string> _stems = new Dictionary<Type, string>();
        private static readonly Dictionary<Type, FieldInfo> _thisField = new Dictionary<Type, FieldInfo>();
        private static readonly Dictionary<Type, PropertyInfo> _instProp = new Dictionary<Type, PropertyInfo>();
        private static readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);

        private static Harmony _h;
        private static bool _tried;
        private static MethodInfo _mAbc, _mAfcp, _mChg, _mAddItem, _mAddEq, _wPay, _wParty, _wHeroGold, _wGoods, _wGoodsEq;
        private static Func<IssueBase, int> _rawReward;
        private static int _candidates, _patched, _types, _getters, _gettersWanted, _goodsPatched, _scanErr;
        private static double _scanMs, _scanOnlyMs;
        private static string _wiredText = "", _missText = "-";

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("QuestPay8." + where, e); } catch { }
        }

        // liczniki doby
        private static int _inN, _inShortN, _inFrozen, _inComputed, _outN, _frozenToday, _shown;
        private static long _inGame, _inDue, _inPaidHero, _inPaidVillage, _inShort, _inNothing, _outAll, _outTown, _outHero, _outVillage, _outVoid;
        private static long _gSinkShelf, _gSinkTabor, _gVoid, _gFromShelf, _gMissing, _gGoldInstead, _gGoldShort, _gNothing;
        private static readonly Dictionary<string, long[]> _typeDay = new Dictionary<string, long[]>(StringComparer.Ordinal);   // typ -> [wyplat, kwota gry, po skali]
        private static readonly HashSet<string> _noPayer = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _shortSaid = new HashSet<string>(StringComparer.Ordinal);

        private static void ClearDay()
        {
            _stumblesDay = 0;
            _inN = _inShortN = _inFrozen = _inComputed = _outN = _frozenToday = _shown = 0;
            _inGame = _inDue = _inPaidHero = _inPaidVillage = _inShort = _inNothing = _outAll = _outTown = _outHero = _outVillage = _outVoid = 0;
            _gSinkShelf = _gSinkTabor = _gVoid = _gFromShelf = _gMissing = _gGoldInstead = _gGoldShort = _gNothing = 0;
            _typeDay.Clear(); _noPayer.Clear(); _shortSaid.Clear();
        }

        /// <summary>Nowa kampania i kazde wczytanie (WorldMeasure.Reset - konstruktor ArmouryBehavior, PRZED SyncData: slownik kwot wraca z zapisu).</summary>
        internal static void Reset()
        {
            _top = null; _raw = false;
            _stumbles = 0; _errSites.Clear();
            _pledges.Clear(); _capNow.Clear();
            ClearDay();
        }

        private static bool On(Func<Settings, bool> key) { var s = Settings.Current; return s != null && key(s); }

        // ------------------------------------------------------------ typ zadania, obiekt zadania, zleceniodawca, osada

        private static string StemOfName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "?";
            foreach (var suf in new[] { "IssueQuest", "Issue", "Quest" })
                if (n.Length > suf.Length && n.EndsWith(suf, StringComparison.Ordinal)) return n.Substring(0, n.Length - suf.Length);
            return n;
        }

        private static string StemOf(Type t)
        {
            string s;
            if (_stems.TryGetValue(t, out s)) return s;
            s = StemOfName(t.Name);
            _stems[t] = s;
            return s;
        }

        private static Kind KindOf(string stem) { Kind k; return stem != null && Kinds.TryGetValue(stem, out k) ? k : null; }

        private static bool IsQuestType(Type t) { return t != null && (typeof(IssueBase).IsAssignableFrom(t) || typeof(QuestBase).IsAssignableFrom(t)); }

        private static FieldInfo ThisField(Type t)
        {
            FieldInfo fi;
            if (_thisField.TryGetValue(t, out fi)) return fi;
            fi = null;
            try
            {
                foreach (var f in t.GetFields(AnyInstance))
                    if (IsQuestType(f.FieldType)) { fi = f; if (f.Name.IndexOf("this", StringComparison.Ordinal) >= 0) break; }
            }
            catch { fi = null; }
            _thisField[t] = fi;
            return fi;
        }

        /// <summary>Statyczne Instance klasy zachowania (RevenueFarmingIssueBehavior.Instance = biezace zadanie).</summary>
        private static QuestBase StaticInstance(Type decl)
        {
            for (var t = decl; t != null; t = t.DeclaringType)
            {
                PropertyInfo pi;
                if (!_instProp.TryGetValue(t, out pi))
                {
                    pi = null;
                    try { var p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); if (p != null && typeof(QuestBase).IsAssignableFrom(p.PropertyType)) pi = p; }
                    catch { pi = null; }
                    _instProp[t] = pi;
                }
                if (pi != null) return pi.GetValue(null, null) as QuestBase;
            }
            return null;
        }

        private static void Resolve(Frame f)
        {
            if (f.Resolved) return;
            f.Resolved = true;
            object o = f.Inst;
            var issue = o as IssueBase; var quest = o as QuestBase;
            if (issue == null && quest == null && o != null)
            {
                var fi = ThisField(o.GetType());
                if (fi != null) { var t = fi.GetValue(o); issue = t as IssueBase; quest = t as QuestBase; }
            }
            if (issue == null && quest == null)
            {
                for (var p = f.Prev; p != null; p = p.Prev)
                {
                    Resolve(p);
                    if (p.Issue != null || p.Quest != null) { issue = p.Issue; quest = p.Quest; if (f.GiverOverride == null) f.GiverOverride = p.GiverOverride; break; }
                }
                if (issue == null && quest == null) quest = StaticInstance(f.R.M.DeclaringType);
                if (issue == null && quest == null && f.GiverOverride != null) issue = f.GiverOverride.Issue;   // metoda statyczna wolana ze sprawy (zleceniodawca w argumencie)
            }
            f.Issue = issue; f.Quest = quest;
            f.Stem = issue != null ? StemOf(issue.GetType()) : quest != null ? StemOf(quest.GetType()) : f.R.DeclStem;
        }

        private static Hero Giver(Frame f)
        {
            if (f.GiverOverride != null) return f.GiverOverride;
            return f.Issue != null ? f.Issue.IssueOwner : f.Quest != null ? f.Quest.QuestGiver : null;
        }

        private static Hero HeroField(Frame f, string name)
        {
            object o = (object)f.Quest ?? f.Issue;
            if (o == null || name == null) return null;
            string key = o.GetType().FullName + "|" + name;
            FieldInfo fi;
            if (!_fields.TryGetValue(key, out fi)) { fi = AccessTools.Field(o.GetType(), name); _fields[key] = fi; }
            if (fi == null) return null;
            var v = fi.GetValue(o);
            var h = v as Hero;
            if (h != null) return h;
            var c = v as CharacterObject;
            return c != null ? c.HeroObject : null;
        }

        private static Settlement VillageHere()
        {
            var st = Settlement.CurrentSettlement ?? (MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null);
            return st != null && st.IsVillage && st.SettlementComponent != null ? st : null;
        }

        /// <summary>Osada zadania: osada notabla-zleceniodawcy; dla lorda - osada, w ktorej stoi, osada gracza, na koncu siedziba.</summary>
        private static Settlement QuestSettlement(Frame f)
        {
            var g = Giver(f);
            Settlement st = f.Issue != null ? f.Issue.IssueSettlement : null;
            if (st == null && g != null) st = g.CurrentSettlement;
            if (st == null) st = Settlement.CurrentSettlement;
            if (st == null && g != null) st = g.HomeSettlement;
            return st != null && st.SettlementComponent != null ? st : null;
        }

        private static string NameOf(Hero h) { return h != null && h.Name != null ? h.Name.ToString() : "?"; }
        private static string NameOf(Settlement s) { return s != null && s.Name != null ? s.Name.ToString() : "?"; }

        // ------------------------------------------------------------ skala (R2) i kwota zamrozona

        private static int PartyMen()
        {
            var mp = MobileParty.MainParty;
            return mp != null && mp.MemberRoster != null ? Math.Max(1, mp.MemberRoster.TotalManCount) : 1;
        }

        private static float Rate()
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (mp == null) return FallbackRate;
                // recenzja WW-J: stawka robocizny z PELNEGO zoldu partii (bez polowy "w lupie" zalog okretow Wysp - na zadaniu nie ma lupu; zleceniodawca placi
                // za prace ludzi, nie za uklad pana Wysp z zaloga); bez WW-J FullTotalWage == TotalWage
                float r = MountedWage.FullTotalWage(mp) / (float)PartyMen();
                return r > 0.01f ? r : FallbackRate;
            }
            catch (Exception e) { Stumble("Rate", e); return FallbackRate; }
        }

        internal static int RawReward(IssueBase issue)   // internal: linia logu QuestGangAxes (recenzja m105 R2 - nagroda gry bez skali)
        {
            if (issue == null || _rawReward == null) return 0;
            bool was = _raw;
            _raw = true;
            try { return _rawReward(issue); }
            catch (Exception e) { Stumble("RawReward", e); return 0; }
            finally { _raw = was; }
        }

        /// <summary>m102-tablica: kwota sprawy tak, jak widzi ja gracz w rozmowie (getter RewardGold z postfiksem RewardShown - po skali, gdy klucz QuestRewardInWageDays;
        /// _raw zostaje false). Brak gettera (EnsureHooks nie przeszedl) - -1 (tablica pisze "?" i liczy "nagroda nieczytelna").</summary>
        internal static int ShownReward(IssueBase issue)
        {
            if (issue == null || _rawReward == null) return -1;
            try { return _rawReward(issue); }
            catch (Exception e) { Stumble("ShownReward", e); return -1; }
        }

        /// <summary>m102-tablica ("Your tasks"): kwota obiecana zadania w toku - zamrozona przy przyjeciu (najwyzej kwota gry), bez wpisu - kwota gry.</summary>
        internal static int PromisedReward(QuestBase quest)
        {
            if (quest == null) return 0;
            int game = quest.RewardGold;
            try
            {
                if (!On(s => s.QuestRewardInWageDays)) return game;
                var p = Frozen(IssueOf(quest), quest);
                if (p != null && game > 0 && p.Cap > 0 && p.Cap < game) return p.Cap;
            }
            catch (Exception e) { Stumble("PromisedReward", e); }
            return game;
        }

        private static int CapOf(Kind kind, int game, int labor)
        {
            if (kind != null && kind.NoScale) return int.MaxValue;
            if (kind != null && kind.K >= 0)
            {
                if (game <= 0) return int.MaxValue;                           // nie znamy calej kwoty gry - bez skali (sam platnik)
                return game > kind.K ? (game - kind.K) + Math.Min(kind.K, labor) : Math.Min(game, labor);
            }
            return labor;
        }

        private static Pledge MakePledge(IssueBase issue, QuestBase quest, string stem, int gameHint)
        {
            var kind = KindOf(stem);
            int n = 0; float d = 0f;
            if (kind != null && kind.PartyDays > 0) { n = PartyMen(); d = kind.PartyDays; }
            else if (issue != null)
            {
                try { if (issue.IsThereAlternativeSolution) { n = issue.AlternativeSolutionBaseNeededMenCount; d = issue.GetBaseAlternativeSolutionDurationInDays(); } }
                catch (Exception e) { Stumble("MakePledge(rozwiazanie zastepcze)", e); n = 0; }
            }
            if (n <= 0 || d <= 0f) { n = PartyMen(); d = DefaultDays; }
            float rate = Rate();
            int labor = Math.Max(1, (int)Math.Round(n * d * rate * Premium));
            int game = gameHint > 0 ? gameHint : RawReward(issue);
            if (game <= 0 && quest != null) game = quest.RewardGold;
            return new Pledge { Stem = stem, N = n, D = d, Rate = rate, Labor = labor, Game = game, Cap = CapOf(kind, game, labor) };
        }

        private static IssueBase IssueOf(QuestBase quest)
        {
            if (quest == null || Campaign.Current == null) return null;
            try
            {
                var g = quest.QuestGiver;
                var gi = g != null ? g.Issue : null;
                // zadanie moze wystartowac, zanim sprawa dostanie IssueQuest (StartQuest w konstruktorze) - wtedy po id: sprawa + "_quest" (G IssueBase.cs:588)
                // albo po stanie: sprawa juz "rozwiazywana zadaniem", a zadania jeszcze nie ma (CaravanAmbush ma wlasne id zadania)
                if (gi != null && (ReferenceEquals(gi.IssueQuest, quest) || (gi.IssueQuest == null && gi.IsSolvingWithQuest)
                                   || (quest.StringId != null && gi.StringId != null && quest.StringId == gi.StringId + "_quest"))) return gi;
                return IssueManager.GetIssueOfQuest(quest);
            }
            catch (Exception e) { Stumble("IssueOf", e); return null; }
        }

        private static Pledge Frozen(IssueBase issue, QuestBase quest)
        {
            Pledge p;
            string id = issue != null ? issue.StringId : null;
            if (!string.IsNullOrEmpty(id) && _pledges.TryGetValue(id, out p)) return p;
            id = quest != null ? quest.StringId : null;
            if (string.IsNullOrEmpty(id)) return null;
            if (_pledges.TryGetValue(id, out p)) return p;
            if (id.EndsWith("_quest", StringComparison.Ordinal) && _pledges.TryGetValue(id.Substring(0, id.Length - 6), out p)) return p;
            return null;
        }

        /// <summary>Przyjecie zadania albo wyslanie ludzi: kwota obiecana zamrozona (klucz = id sprawy, dla zadan bez sprawy id zadania).</summary>
        private static void Freeze(IssueBase issue, QuestBase quest, string how)
        {
            try
            {
                if (!On(s => s.QuestRewardInWageDays)) return;
                if (issue == null && quest == null) return;
                string key = issue != null ? issue.StringId : quest.StringId;
                if (string.IsNullOrEmpty(key)) return;
                var old = Frozen(issue, quest);
                int hour = (int)CampaignTime.Now.ToHours;
                // zadanie z wlasnym id (nie "sprawa_quest"): ta sama kwota takze pod id zadania - wyplata po zamknieciu sprawy znajdzie ja bez sprawy
                string qid = quest != null ? quest.StringId : null;
                bool alias = !string.IsNullOrEmpty(qid) && qid != key && qid != key + "_quest";
                if (old != null && old.Stamp == hour) { if (alias && !_pledges.ContainsKey(qid)) _pledges[qid] = old; return; }   // drugie zdarzenie tego samego przyjecia
                string stem = issue != null ? StemOf(issue.GetType()) : StemOf(quest.GetType());
                var p = MakePledge(issue, quest, stem, 0);
                p.Key = key; p.Stamp = hour;
                _pledges[key] = p;                                             // nowe przyjecie (takze ponowne po nieudanej probie) - nowa kwota
                if (alias) _pledges[qid] = p;
                _frozenToday++;
                var giver = issue != null ? issue.IssueOwner : quest.QuestGiver;
                Log.Info("Nagrody zadan (E8/D1): przyjete (" + how + ") " + stem + " [" + key + "] od " + NameOf(giver) + " - kwota gry " + p.Game
                         + ", praca " + p.N + " ludzi x " + p.D.ToString("0.#", Inv) + " dni x zold " + p.Rate.ToString("0.00", Inv) + " x " + Premium.ToString("0.##", Inv) + " = " + p.Labor
                         + ", obiecane najwyzej " + (p.Cap == int.MaxValue ? "kwota gry (bez skali)" : p.Cap.ToString(Inv)) + ".");
            }
            catch (Exception e) { Stumble("Freeze", e); }
        }

        private static void OnIssueUpdated(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero solver)
        {
            if (details == IssueBase.IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution) Freeze(issue, issue != null ? issue.IssueQuest : null, "zadanie");
            else if (details == IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest) Freeze(issue, null, "wyslani ludzie");
        }

        private static void OnQuestStarted(QuestBase quest) { if (quest != null && !NoticeBoard.IsOurQuest(quest)) Freeze(IssueOf(quest), quest, "zadanie"); }   // m109-list: list gonczy (BountyQuest) placi NoticeBoard.Pay - bez zamrozonej kwoty

        /// <summary>Postfiks getterow RewardGold spraw: oferta w rozmowie i kwota wpisana do zadania pokazuja te sama liczbe, ktora wyplaci wrapper.</summary>
        public static void RewardShown(IssueBase __instance, ref int __result)
        {
            if (_raw || __result <= 0 || __instance == null) return;
            try
            {
                if (!On(s => s.QuestRewardInWageDays)) return;
                int cap;
                var p = Frozen(__instance, null);
                if (p != null && !__instance.IsOngoingWithoutQuest) cap = p.Cap;   // sprawa w trakcie rozwiazywania - kwota obiecana; znow w ofercie - liczona na nowo
                else
                {
                    string id = __instance.StringId ?? "";
                    int hour = (int)CampaignTime.Now.ToHours;
                    KeyValuePair<int, int> kv;
                    string ck = id + "|" + __result.ToString(Inv);
                    if (id.Length > 0 && _capNow.TryGetValue(ck, out kv) && kv.Key == hour) cap = kv.Value;
                    else
                    {
                        cap = MakePledge(__instance, null, StemOf(__instance.GetType()), __result).Cap;
                        if (id.Length > 0) { if (_capNow.Count > 256) _capNow.Clear(); _capNow[ck] = new KeyValuePair<int, int>(hour, cap); }
                    }
                }
                if (cap > 0 && cap < __result) { __result = cap; _shown++; }
            }
            catch (Exception e) { Stumble("RewardShown", e); }
        }

        // ------------------------------------------------------------ ramka (prefiks / finalizer zalatanych metod)

        public static void Enter(object __instance, MethodBase __originalMethod, out Frame __state)
        {
            __state = null;
            try
            {
                MRec r;
                if (__originalMethod == null || !_recs.TryGetValue(__originalMethod.MethodHandle.Value, out r)) return;
                var f = new Frame { R = r, Inst = __instance, Prev = _top, Seq = GiverPay.NextSeq() };
                if (r.Goods == GKept) f.Snap = KeptSnapshot(f);
                _top = f;
                __state = f;
            }
            catch (Exception e) { Stumble("Enter", e); }
        }

        public static void Leave(Frame __state)
        {
            if (__state == null) return;
            try { if (__state.Snap != null) KeptToShelf(__state); }
            catch (Exception e) { Stumble("Leave(bron strazy)", e); }
            if (ReferenceEquals(_top, __state)) { _top = __state.Prev; return; }
            for (var x = _top; x != null; x = x.Prev)
                if (ReferenceEquals(x.Prev, __state)) { x.Prev = __state.Prev; return; }
        }

        // ------------------------------------------------------------ zloto: nic -> gracz, gracz -> nic (wolane z GiverPay)

        private sealed class Plan
        {
            public int Amount, Due, GameAmount; public bool In, Handle;
            public Hero Hero; public Settlement St; public bool Village; public string Stem, What; public Pledge P; public bool FrozenP; public int Mult = 1;
        }

        /// <summary>Wrapper wyplat (GiverPay.PayFromGiver): true = wyplata zrobiona tutaj, false = wywolanie gry bez zmian.</summary>
        internal static bool Route(Hero giver, Hero recipient, int amount, bool disableNotification)
        {
            var f = _top;
            if (f == null || amount <= 0 || f.Seq < GiverPay.TopSeq) return false;
            var me = Hero.MainHero;
            if (me == null) return false;
            bool inDir = giver == null && ReferenceEquals(recipient, me);
            bool outDir = ReferenceEquals(giver, me) && recipient == null;
            if (!inDir && !outDir) return false;
            Plan p = null;
            try { p = inDir ? PlanIn(f, amount) : PlanOut(f, amount); }
            catch (Exception e) { Stumble(inDir ? "PlanIn" : "PlanOut", e); p = null; }
            if (p == null || !p.Handle) return false;
            if (inDir) DoIn(p, me, disableNotification); else DoOut(p, me, disableNotification);
            return true;
        }

        /// <summary>Zamiast GiveGoldAction.ApplyForCharacterToParty(gracz, null, kwota) (G VillageNeedsTools :272).</summary>
        internal static bool RouteParty(Hero giver, PartyBase party, int amount, bool disableNotification)
        {
            if (party != null) return false;
            return Route(giver, null, amount, disableNotification);
        }

        /// <summary>Zamiast Hero.MainHero.ChangeHeroGold(-kwota) (G LordNeedsHorses :264).</summary>
        internal static bool RouteHeroGold(Hero hero, int delta)
        {
            if (delta >= 0 || hero == null || !ReferenceEquals(hero, Hero.MainHero)) return false;
            return Route(hero, null, -delta, true);
        }

        private static void Tally(string stem, long game, long due)
        {
            long[] a;
            if (!_typeDay.TryGetValue(stem ?? "?", out a)) { a = new long[3]; _typeDay[stem ?? "?"] = a; }
            a[0]++; a[1] += game; a[2] += due;
        }

        private static Plan PlanIn(Frame f, int amount)
        {
            var s = Settings.Current;
            if (s == null) return null;
            Resolve(f);
            var site = f.R.In;
            var p = new Plan { In = true, Amount = amount, Due = amount, GameAmount = amount, Stem = f.Stem, What = f.R.Label, Mult = site != null && site.Mult > 1 ? site.Mult : 1 };
            if (s.QuestRewardInWageDays)
            {
                var issue = f.Issue ?? IssueOf(f.Quest);
                var pl = Frozen(issue, f.Quest);
                if (pl != null) { p.FrozenP = true; _inFrozen++; }
                else { pl = MakePledge(issue, f.Quest, f.Stem, 0); _inComputed++; }
                p.P = pl;
                // kontroferta "kwota zadania x Mult" (tabela Sites): skala i kwota gry tej wyplaty to wielokrotnosc kwoty zamrozonej - tyle pokazal tekst rozmowy
                long cap = Math.Min((long)int.MaxValue, (long)pl.Cap * p.Mult);
                long game = Math.Min((long)int.MaxValue, (long)pl.Game * p.Mult);
                if (cap > 0 && cap < p.Due) p.Due = (int)cap;
                if (game > amount && amount == Math.Min(game, cap)) p.GameAmount = (int)game;   // getter pokazal juz kwote po skali
            }
            _inN++; _inGame += p.GameAmount; _inDue += p.Due;
            Tally(f.Stem, p.GameAmount, p.Due);
            if (!s.QuestRewardFromGiver)
            {
                if (p.Due == amount) { _inNothing += amount; return p; }   // jak gra (Handle = false)
                p.Handle = true;                                             // sama skala: mniej, dalej z niczego
                return p;
            }
            p.Handle = true;
            var who = site != null ? site.Who : Who.Giver;
            if (who == Who.Village) { p.St = VillageHere(); p.Village = true; }
            else if (who == Who.Field) p.Hero = HeroField(f, site.Field);
            else if (who == Who.Conv) p.Hero = Hero.OneToOneConversationHero;
            else p.Hero = Giver(f);
            if (p.Hero != null && (ReferenceEquals(p.Hero, Hero.MainHero) || !p.Hero.IsAlive)) p.Hero = null;
            if (p.Hero == null && p.St == null) _noPayer.Add(f.Stem ?? "?");
            return p;
        }

        private static void DoIn(Plan p, Hero me, bool dn)
        {
            int paid; string payer;
            if (p.Hero != null)
            {
                paid = GiverPay.Transfer(p.Hero, me, p.Due, dn);
                _inPaidHero += paid; payer = NameOf(p.Hero);
            }
            else if (p.St != null)
            {
                long avail = p.Village ? p.St.SettlementComponent.Gold : Seal31.Spare(p.St);
                paid = (int)Math.Max(0L, Math.Min((long)p.Due, avail));
                if (paid > 0) GiveGoldAction.ApplyForSettlementToCharacter(p.St, me, paid, dn);
                _inPaidVillage += paid; payer = NameOf(p.St);
                if (paid < p.Due && _shortSaid.Add(p.St.StringId ?? "?"))
                    Log.Player(payer + " could pay only " + paid + " of the agreed " + p.Due + ".", true);
            }
            else
            {
                GiveGoldAction.ApplyBetweenCharacters(null, me, p.Due, dn);   // brak platnika (albo klucz platnika wylaczony): jak gra, licznik "z niczego"
                _inNothing += p.Due;
                if (!p.Village) LogPay(p, "z niczego (brak platnika)", p.Due);
                return;
            }
            if (paid < p.Due) { _inShortN++; _inShort += p.Due - paid; }
            if (!p.Village) LogPay(p, payer, paid);
        }

        private static void LogPay(Plan p, string payer, int paid)
        {
            try
            {
                var pl = p.P;
                Log.Info("Nagrody zadan (E8/D1): wyplata - " + p.Stem + " (" + p.What + "): kwota gry " + p.GameAmount + ", po skali " + p.Due
                         + (pl != null ? " (praca " + pl.N + " x " + pl.D.ToString("0.#", Inv) + " dni x " + pl.Rate.ToString("0.00", Inv) + " x " + Premium.ToString("0.##", Inv) + " = " + pl.Labor
                                         + (p.Mult > 1 ? ", kontroferta x " + p.Mult.ToString(Inv) : "")
                                         + (p.FrozenP ? ", kwota zamrozona przy przyjeciu" : ", liczona przy wyplacie") + ")" : " (skala wylaczona)")
                         + ", platnik " + payer + ", zaplacone " + paid + ".");
            }
            catch (Exception e) { Stumble("LogPay", e); }
        }

        private static Who DefaultOut(string method)
        {
            if (method == null) return Who.Giver;
            if (method == "AlternativeSolutionStartConsequence" || method.IndexOf("Bribe", StringComparison.Ordinal) >= 0) return Who.Settlement;
            return Who.Giver;
        }

        private static Plan PlanOut(Frame f, int amount)
        {
            var s = Settings.Current;
            if (s == null) return null;
            Resolve(f);
            var p = new Plan { In = false, Amount = amount, Due = amount, GameAmount = amount, Stem = f.Stem, What = f.R.Label };
            int can = Math.Min(amount, Math.Max(0, Hero.MainHero.Gold));
            _outN++; _outAll += can;
            if (!s.QuestPlayerPaysSomeone) { _outVoid += can; return p; }   // jak gra
            var site = f.R.Out;
            var who = site != null ? site.Who : DefaultOut(f.R.M.Name);
            if (who == Who.Village) { p.St = VillageHere(); p.Village = p.St != null; }
            else if (who == Who.Here) p.St = (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.SettlementComponent != null) ? Settlement.CurrentSettlement : QuestSettlement(f);
            else if (who == Who.Settlement) p.St = QuestSettlement(f);
            else p.Hero = Giver(f);
            if (p.St == null && p.Hero == null) p.Hero = Giver(f);
            if (p.Hero != null && (ReferenceEquals(p.Hero, Hero.MainHero) || !p.Hero.IsAlive)) p.Hero = null;
            if (p.St == null && p.Hero == null) p.St = QuestSettlement(f);
            if (p.St == null && p.Hero == null) { _outVoid += can; return p; }   // nikt nie odbierze: jak gra, licznik "w nicosc"
            p.Handle = true;
            if (p.Hero != null) _outHero += can; else if (p.St.IsVillage) _outVillage += can; else _outTown += can;
            return p;
        }

        private static void DoOut(Plan p, Hero me, bool dn)
        {
            if (p.Hero != null) GiveGoldAction.ApplyBetweenCharacters(me, p.Hero, p.Amount, dn);
            else GiveGoldAction.ApplyForCharacterToSettlement(me, p.St, p.Amount, dn);
            try
            {
                Log.Info("Nagrody zadan (E8/D1): gracz placi - " + p.Stem + " (" + p.What + "): " + p.Amount + " -> "
                         + (p.Hero != null ? "zleceniodawca " + NameOf(p.Hero) : (p.St.IsVillage ? "kiesa wsi " : "kasa osady ") + NameOf(p.St)) + ".");
            }
            catch (Exception e) { Stumble("DoOut(log)", e); }
        }

        // ------------------------------------------------------------ towar zadan

        private static int Count(ItemRoster r, EquipmentElement el)
        {
            int i = r.FindIndexOfElement(el);
            return i >= 0 ? r.GetElementNumber(i) : 0;
        }

        private static bool Mine(ItemRoster r) { var mp = MobileParty.MainParty; return mp != null && ReferenceEquals(r, mp.ItemRoster); }

        /// <summary>Zamiast ItemRoster.AddToCounts(ItemObject, int) w metodach z tabeli GoodsSites.</summary>
        public static int Goods(ItemRoster roster, ItemObject item, int n)
        {
            var f = _top;
            if (f == null || f.R.Goods == GNone || roster == null || item == null || n == 0) return roster.AddToCounts(item, n);
            return GoodsCore(f, roster, new EquipmentElement(item), n);
        }

        /// <summary>Zamiast ItemRoster.AddToCounts(EquipmentElement, int) w metodach z tabeli GoodsSites.</summary>
        public static int GoodsEq(ItemRoster roster, EquipmentElement el, int n)
        {
            var f = _top;
            if (f == null || f.R.Goods == GNone || roster == null || el.Item == null || n == 0) return roster.AddToCounts(el, n);
            return GoodsCore(f, roster, el, n);
        }

        private static int GoodsCore(Frame f, ItemRoster roster, EquipmentElement el, int n)
        {
            bool mine = false, on = false; byte rule = GNone; int before = 0;
            try { mine = Mine(roster); on = On(s => s.QuestGoodsReal); rule = f.R.Goods; if (mine && rule == GSink && n < 0) before = Count(roster, el); }
            catch (Exception e) { Stumble("GoodsCore(stan)", e); rule = GNone; }
            if (mine && rule == GFromShelf && n > 0)
            {
                if (!on) { _gNothing += n; return roster.AddToCounts(el, n); }
                int give = -1; Settlement st = null;
                try { Resolve(f); st = QuestSettlement(f); give = st != null && st.ItemRoster != null ? Math.Min(n, Count(st.ItemRoster, el)) : 0; }
                catch (Exception e) { Stumble("GoodsCore(magazyn)", e); give = -1; }
                if (give < 0) { _gNothing += n; return roster.AddToCounts(el, n); }
                int idx = -1;
                if (give > 0) { st.ItemRoster.AddToCounts(el, -give); idx = roster.AddToCounts(el, give); _gFromShelf += give; }
                int missing = n - give;
                if (missing > 0)
                {
                    if (On(s => s.QuestRewardFromGiver)) GoldInstead(f, el.Item, missing, (long)missing * el.Item.Value);
                    else { _gNothing += missing; idx = roster.AddToCounts(el, missing); }   // klucz platnika wylaczony: brakujace sztuki jak w grze (z niczego)
                }
                return idx;
            }
            int res = roster.AddToCounts(el, n);
            try
            {
                if (mine && rule == GSink && n < 0)
                {
                    int taken = before - Count(roster, el);
                    if (taken > 0) Deliver(f, el, taken, on);
                }
                else if (mine && rule == GKept && n > 0 && f.Snap != null)
                {
                    if (f.Back == null) f.Back = new Dictionary<EquipmentElement, int>();
                    int was; f.Back.TryGetValue(el, out was); f.Back[el] = was + n;
                }
            }
            catch (Exception e) { Stumble("GoodsCore(odbiorca)", e); }
            return res;
        }

        /// <summary>Sztuki zdjete graczowi w dostawie: tabor zleceniodawcy-lorda albo polka osady zadania.</summary>
        private static void Deliver(Frame f, EquipmentElement el, int n, bool on)
        {
            if (!on) { _gVoid += n; return; }
            Resolve(f);
            var g = Giver(f);
            var mp = g != null && g.IsLord ? g.PartyBelongedTo : null;
            if (mp != null && !ReferenceEquals(mp, MobileParty.MainParty) && mp.ItemRoster != null) { mp.ItemRoster.AddToCounts(el, n); _gSinkTabor += n; return; }
            var st = QuestSettlement(f);
            if (st != null && st.ItemRoster != null) { st.ItemRoster.AddToCounts(el, n); _gSinkShelf += n; return; }
            _gVoid += n;
        }

        /// <summary>Rzeczy na wymiane zabraklo w magazynie osady: zleceniodawca placi jej wartosc zlotem (do wysokosci kiesy).</summary>
        private static void GoldInstead(Frame f, ItemObject item, int missing, long value)
        {
            _gMissing += missing;
            var me = Hero.MainHero; var g = Giver(f);
            int due = (int)Math.Min(int.MaxValue, Math.Max(0L, value));
            int paid = 0;
            if (due > 0 && me != null && g != null && !ReferenceEquals(g, me) && g.IsAlive) paid = GiverPay.Transfer(g, me, due, false);
            _gGoldInstead += paid; _gGoldShort += due - paid;
            Log.Info("Nagrody zadan (E8/D1): rzecz na wymiane - " + (f.Stem ?? "?") + ": w magazynie osady zabraklo " + missing + " x " + (item != null ? item.StringId : "?")
                     + ", zamiast nich zloto od zleceniodawcy " + NameOf(g) + ": nalezne " + due + ", zaplacone " + paid + ".");
        }

        private static Dictionary<EquipmentElement, int> KeptDict(Frame f)
        {
            var r = f.R;
            if (f.Inst == null || r.GoodsField == null) return null;
            if (!r.KeptTried) { r.KeptTried = true; r.KeptFi = AccessTools.Field(r.M.DeclaringType, r.GoodsField); }   // raz na metode (OnSettlementLeft biegnie przy kazdym wyjsciu z osady)
            return r.KeptFi != null ? r.KeptFi.GetValue(f.Inst) as Dictionary<EquipmentElement, int> : null;
        }

        private static Dictionary<EquipmentElement, int> KeptSnapshot(Frame f)
        {
            var d = KeptDict(f);
            return d != null && d.Count > 0 ? new Dictionary<EquipmentElement, int>(d) : null;
        }

        /// <summary>GangLeaderNeedsWeapons: straz zabrala graczowi bron (slownik zadania); po wyjsciu z miasta czesc wraca, reszta przepadala - teraz na polke miasta.</summary>
        private static void KeptToShelf(Frame f)
        {
            var now = KeptDict(f);
            if (now == null || now.Count > 0) return;                      // gra nie rozliczyla slownika (inna osada / inna partia)
            bool on = On(s => s.QuestGoodsReal);
            Resolve(f);
            var st = QuestSettlement(f);
            foreach (var kv in f.Snap)
            {
                int back = 0;
                if (f.Back != null) f.Back.TryGetValue(kv.Key, out back);
                int kept = kv.Value - back;
                if (kept <= 0 || kv.Key.Item == null) continue;
                if (on && st != null && st.ItemRoster != null) { st.ItemRoster.AddToCounts(kv.Key, kept); _gSinkShelf += kept; }
                else _gVoid += kept;
            }
        }

        /// <summary>Prefiks VillageNeedsToolsIssueQuest.GiveTradeOrExchangeRewardToMainParty(zleceniodawca, zloto, rzecz, ile) - statyczna, wolana z zadania i ze
        /// sprawy. Gra dopisywala rzecz do magazynu wsi z niczego i przenosila ja graczowi; teraz rzecz schodzi z magazynu wsi (ile jest), a brak
        /// zleceniodawca placi zlotem (udzial w kwocie zadania). Bez rzeczy (sama kwota) - oryginal z wrapperem wyplaty.</summary>
        public static bool ExchangeEnter(Hero __0, int __1, ItemObject __2, int __3, MethodBase __originalMethod, out Frame __state)
        {
            Enter(null, __originalMethod, out __state);
            if (__state != null) __state.GiverOverride = __0;
            if (__2 == null || __3 <= 0 || __state == null) return true;
            bool done = false;
            try
            {
                Resolve(__state);
                if (!On(s => s.QuestGoodsReal)) { _gNothing += __3; return true; }
                var st = __0 != null ? __0.CurrentSettlement : null;
                if (st == null || st.ItemRoster == null || PartyBase.MainParty == null) { _gNothing += __3; return true; }
                int give = Math.Min(__3, Count(st.ItemRoster, new EquipmentElement(__2)));
                done = true;
                if (give > 0)
                {
                    var el = new ItemRosterElement(__2, give);
                    GiveItemAction.ApplyForParties(st.Party, PartyBase.MainParty, in el);
                    _gFromShelf += give;
                }
                int missing = __3 - give;
                if (missing > 0)
                {
                    if (On(s => s.QuestRewardFromGiver)) GoldInstead(__state, __2, missing, __1 > 0 ? (long)__1 * missing / __3 : (long)missing * __2.Value);
                    else { _gNothing += missing; PartyBase.MainParty.ItemRoster.AddToCounts(__2, missing); }   // klucz platnika wylaczony: brakujace sztuki jak w grze (z niczego)
                }
            }
            catch (Exception e) { Stumble("ExchangeEnter", e); }
            return !done;
        }

        // ------------------------------------------------------------ transpiler

        public static IEnumerable<CodeInstruction> Il(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            try
            {
                MRec r;
                if (original == null || !_recs.TryGetValue(original.MethodHandle.Value, out r)) return list;
                int gold = 0, goods = 0;
                bool g = r.Goods == GSink || r.Goods == GFromShelf || r.Goods == GKept;
                foreach (var ci in list)
                {
                    if (ci.opcode != OpCodes.Call && ci.opcode != OpCodes.Callvirt) continue;
                    var mi = ci.operand as MethodInfo;
                    if (mi == null) continue;
                    if (mi == _mAbc) { ci.opcode = OpCodes.Call; ci.operand = _wPay; gold++; }
                    else if (mi == _mAfcp) { ci.opcode = OpCodes.Call; ci.operand = _wParty; gold++; }
                    else if (mi == _mChg) { ci.opcode = OpCodes.Call; ci.operand = _wHeroGold; gold++; }
                    else if (g && mi == _mAddItem) { ci.opcode = OpCodes.Call; ci.operand = _wGoods; goods++; }
                    else if (g && mi == _mAddEq) { ci.opcode = OpCodes.Call; ci.operand = _wGoodsEq; goods++; }
                }
                r.Gold = gold; r.GoodsCalls = goods;
            }
            catch (Exception e) { Stumble("Il", e); }
            return list;
        }

        // ------------------------------------------------------------ skan i wpiecie (raz na proces, w kampanii)

        private static Type[] TypesOf(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { var l = new List<Type>(); foreach (var t in e.Types) if (t != null) l.Add(t); return l.ToArray(); }
            catch { return new Type[0]; }
        }

        private static Site FindSite(string stem, string method, bool inDir)
        {
            foreach (var s in Sites)
                if (s.In == inDir && s.Stem == stem && method.StartsWith(s.Method, StringComparison.Ordinal)) return s;
            return null;
        }

        private static readonly Dictionary<Module, Dictionary<int, bool>> _tokHit = new Dictionary<Module, Dictionary<int, bool>>();

        // TRIAZ 2 (13.10, etap8 b): rozmiary operandow instrukcji IL - _op1 dla opcode jednobajtowego, _op2 dla drugiego bajtu po 0xFE.
        // -1 = nieznany opcode, -2 = switch (4 bajty liczby + 4 x liczba). Budowane raz, leniwie, z pol System.Reflection.Emit.OpCodes.
        private static int[] _op1, _op2;

        private static void BuildOpSizes()
        {
            var a = new int[256]; var b = new int[256];
            for (int i = 0; i < 256; i++) { a[i] = -1; b[i] = -1; }
            foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType != typeof(OpCode)) continue;
                var oc = (OpCode)f.GetValue(null);
                int sz;
                switch (oc.OperandType)
                {
                    case OperandType.InlineNone: sz = 0; break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: sz = 1; break;
                    case OperandType.InlineVar: sz = 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: sz = 8; break;
                    case OperandType.InlineSwitch: sz = -2; break;
                    default: sz = 4; break;   // InlineBrTarget, InlineField, InlineI, InlineMethod, InlineSig, InlineString, InlineTok, InlineType, ShortInlineR
                }
                ushort v = (ushort)oc.Value;
                if (oc.Size == 1) a[v & 0xFF] = sz;
                else if ((v >> 8) == 0xFE) b[v & 0xFF] = sz;
            }
            _op2 = b; _op1 = a;
        }

        /// <summary>Przesiew: czy w IL jest call / callvirt (0x28 / 0x6F) z tokenem metody, ktory w tym module wskazuje jedna z trzech metod zlota.
        /// Token rozwiazywany raz na modul (pamiec). TRIAZ 2 (13.10): chodzenie PO GRANICACH INSTRUKCJI (tablica rozmiarow operandow) zamiast bajt
        /// po bajcie - dotad 4 bajty ze srodka innej instrukcji szly do ResolveMethod jako token (11 wyjatkow ArgumentOutOfRangeException przy kazdym
        /// starcie = 3 czerwone bloki w CrashScribe; zlapane, bez skutku w grze). Token po prawdziwym call / callvirt jest zawsze wazny.
        /// Anomalia chodzenia (nieznany opcode, operand albo switch poza koncem, brak tablicy) = true: rozstrzyga pelny odczyt instrukcji w ScanType.</summary>
        private static bool MayCallGold(MethodBase m, byte[] il)
        {
            if (_op1 == null) { try { BuildOpSizes(); } catch { return true; } }
            int[] op1 = _op1, op2 = _op2;
            if (op1 == null || op2 == null) return true;
            var mod = m.Module;
            Dictionary<int, bool> cache;
            if (!_tokHit.TryGetValue(mod, out cache)) { cache = new Dictionary<int, bool>(); _tokHit[mod] = cache; }
            int n = il.Length, i = 0;
            while (i < n)
            {
                byte op = il[i++];
                int sz; bool call = false;
                if (op == 0xFE) { if (i >= n) return true; sz = op2[il[i++]]; }   // 0xFE PRZED tablica: pole OpCodes.Prefix1 ma wartosc 0xFE i rozmiar 0
                else { sz = op1[op]; call = op == 0x28 || op == 0x6F; }
                if (sz == -1) return true;
                if (sz == -2)
                {
                    if (i + 4 > n) return true;
                    long cnt = (uint)(il[i] | (il[i + 1] << 8) | (il[i + 2] << 16) | (il[i + 3] << 24));
                    long next = i + 4L + 4L * cnt;
                    if (next > n) return true;
                    i = (int)next;
                    continue;
                }
                if (i + sz > n) return true;
                if (call)
                {
                    int tok = il[i] | (il[i + 1] << 8) | (il[i + 2] << 16) | (il[i + 3] << 24);
                    int table = (tok >> 24) & 0xFF;
                    if (table == 0x06 || table == 0x0A)                         // MethodDef albo MemberRef
                    {
                        bool hit;
                        if (!cache.TryGetValue(tok, out hit))
                        {
                            hit = false;
                            try { var mb = mod.ResolveMethod(tok); hit = mb != null && (mb == _mAbc || mb == _mAfcp || mb == _mChg); }
                            catch { hit = false; }
                            cache[tok] = hit;
                        }
                        if (hit) return true;
                    }
                }
                i += sz;
            }
            return false;
        }

        /// <summary>Metody jednego typu (zadanie, jego domkniecie albo klasa zachowania z tabeli ExtraTypes), ktore wolaja zloto albo sa miejscem towaru.</summary>
        private static void ScanType(Type t, Type root, string stem, bool onlySites, string asmName)
        {
            foreach (var m in t.GetMethods(AnyDeclared))
            {
                try
                {
                    if (m.IsAbstract || m.ContainsGenericParameters || m.GetMethodBody() == null) continue;
                    byte goods = GNone; string gf = null;
                    foreach (var gs in GoodsSites) if (gs[0] == t.Name && gs[1] == m.Name) { goods = byte.Parse(gs[2], Inv); gf = gs[3]; break; }
                    bool gold = false;
                    var il = m.GetMethodBody().GetILAsByteArray();
                    if (il != null && MayCallGold(m, il))                    // tani przesiew po bajtach IL, potem pewny odczyt instrukcji
                        foreach (var kv in PatchProcessor.ReadMethodBody(m))
                        {
                            var mi = kv.Value as MethodInfo;
                            if (mi != null && (mi == _mAbc || mi == _mAfcp || mi == _mChg)) { gold = true; break; }
                        }
                    if (!gold && goods == GNone) continue;
                    var sin = FindSite(stem, m.Name, true); var sout = FindSite(stem, m.Name, false);
                    if (onlySites && sin == null && sout == null) continue;
                    var r = new MRec { M = m, DeclStem = stem, Label = (root ?? t).Name + "." + m.Name, Asm = asmName, In = sin, Out = sout, Goods = goods, GoodsField = gf };
                    _recs[m.MethodHandle.Value] = r;
                    _all.Add(r);
                }
                catch { _scanErr++; }
            }
        }

        /// <summary>Typy zagniezdzone zadania, ktore same nie sa zadaniami (domkniecia rozmow i menu) - z typem zadania jako korzeniem.</summary>
        private static void ScanNested(Type owner, Type root, string stem, string asmName, int depth)
        {
            if (depth > 4) return;
            Type[] nested;
            try { nested = owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic); }
            catch { _scanErr++; return; }
            foreach (var n in nested)
            {
                try
                {
                    if (n == null || !n.IsClass || n.ContainsGenericParameters || IsQuestType(n)) continue;   // zagniezdzone zadanie jest wlasnym korzeniem
                    ScanType(n, root, stem, false, asmName);
                    ScanNested(n, root, stem, asmName, depth + 1);
                }
                catch { _scanErr++; }
            }
        }

        private static bool RefsCampaign(Assembly asm, string name)
        {
            if (name == "TaleWorlds.CampaignSystem") return true;
            foreach (var rn in asm.GetReferencedAssemblies()) if (rn.Name == "TaleWorlds.CampaignSystem") return true;
            return false;
        }

        private static void Scan()
        {
            var me = typeof(QuestPay8).Assembly;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic || ReferenceEquals(asm, me)) continue;
                    string an = asm.GetName().Name;
                    if (an == "StoryMode" || !RefsCampaign(asm, an)) continue;           // StoryMode: fabula gry bez zleceniodawcow, w ROT nieczynna
                    foreach (var t in TypesOf(asm))
                    {
                        try
                        {
                            if (t == null || !t.IsClass || t.ContainsGenericParameters || !IsQuestType(t)) continue;
                            string stem = StemOfName(t.Name);
                            ScanType(t, t, stem, false, an);
                            ScanNested(t, t, stem, an, 0);
                        }
                        catch { _scanErr++; }
                    }
                    foreach (var x in ExtraTypes)
                    {
                        var et = asm.GetType(x[0], false);
                        if (et != null) ScanType(et, null, x[1], true, an);
                    }
                }
                catch { _scanErr++; }
            }
        }

        private static string KeysText()
        {
            var s = Settings.Current;
            if (s == null) return "klucze: brak ustawien";
            return "Quest Reward From Giver " + (s.QuestRewardFromGiver ? "CZYNNE" : "WYLACZONE") + ", Quest Reward In Wage Days " + (s.QuestRewardInWageDays ? "CZYNNE" : "WYLACZONE")
                   + ", Quest Player Pays Someone " + (s.QuestPlayerPaysSomeone ? "CZYNNE" : "WYLACZONE") + ", Quest Goods Real " + (s.QuestGoodsReal ? "CZYNNE" : "WYLACZONE");
        }

        private static string StatusLine(string when)
        {
            return "Nagrody zadan (E8/D1): wpiete " + _patched + " metod w " + _types + " typach (" + when + "; kandydatow " + _candidates + "; " + _wiredText
                   + "; BRAK: " + _missText + "; typy nie do odczytu " + _scanErr + "; skan " + _scanOnlyMs.ToString("0", Inv) + " ms, skan i wpiecie "
                   + _scanMs.ToString("0", Inv) + " ms) | " + KeysText()
                   + " | kwot zamrozonych z zapisu " + _pledges.Count + " | potkniecia " + _stumbles + ".";
        }

        /// <summary>Start sesji (ArmouryBehavior.OnSessionLaunched, po McmSettings.Apply): nasluchy przyjecia zadan; skan i latki raz na proces.</summary>
        internal static void SessionStart(CampaignBehaviorBase owner)
        {
            try
            {
                CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(owner, OnIssueUpdated);
                CampaignEvents.OnQuestStartedEvent.AddNonSerializedListener(owner, OnQuestStarted);
            }
            catch (Exception e) { Stumble("SessionStart(nasluchy)", e); }
            EnsureHooks();
        }

        internal static void EnsureHooks()
        {
            if (_tried) { Log.Info(StatusLine("przy starcie kampanii")); return; }
            _tried = true;
            long t0 = Stopwatch.GetTimestamp();
            var miss = new List<string>();
            try
            {
                if (_h == null) _h = new Harmony("com.jeff.armoury.questpay8");
                var me = typeof(QuestPay8);
                _mAbc = AccessTools.Method(typeof(GiveGoldAction), "ApplyBetweenCharacters", new[] { typeof(Hero), typeof(Hero), typeof(int), typeof(bool) });
                _mAfcp = AccessTools.Method(typeof(GiveGoldAction), "ApplyForCharacterToParty", new[] { typeof(Hero), typeof(PartyBase), typeof(int), typeof(bool) });
                _mChg = AccessTools.Method(typeof(Hero), "ChangeHeroGold", new[] { typeof(int) });
                _mAddItem = AccessTools.Method(typeof(ItemRoster), "AddToCounts", new[] { typeof(ItemObject), typeof(int) });
                _mAddEq = AccessTools.Method(typeof(ItemRoster), "AddToCounts", new[] { typeof(EquipmentElement), typeof(int) });
                _wPay = AccessTools.Method(typeof(GiverPay), nameof(GiverPay.PayFromGiver));
                _wParty = AccessTools.Method(typeof(GiverPay), nameof(GiverPay.PayToParty));
                _wHeroGold = AccessTools.Method(typeof(GiverPay), nameof(GiverPay.HeroGold));
                _wGoods = AccessTools.Method(me, nameof(Goods));
                _wGoodsEq = AccessTools.Method(me, nameof(GoodsEq));
                if (_mAbc == null || _mAfcp == null || _mChg == null || _mAddItem == null || _mAddEq == null || _wPay == null || _wParty == null || _wHeroGold == null)
                {
                    _missText = "metody gry (GiveGoldAction / Hero.ChangeHeroGold / ItemRoster.AddToCounts) - nie znaleziono, nic nie wpiete";
                    Log.Info(StatusLine("przy starcie pierwszej kampanii"));
                    return;
                }
                try
                {
                    var getter = AccessTools.PropertyGetter(typeof(IssueBase), "RewardGold");
                    if (getter != null) _rawReward = (Func<IssueBase, int>)Delegate.CreateDelegate(typeof(Func<IssueBase, int>), getter);
                }
                catch (Exception e) { Stumble("EnsureHooks(getter)", e); _rawReward = null; }

                Scan();
                _tokHit.Clear();
                _scanOnlyMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                _candidates = _all.Count;
                var typesHit = new HashSet<Type>(); var perAsm = new SortedDictionary<string, int>(StringComparer.Ordinal);
                int cPay = 0, cGoods = 0, goodsWanted = 0;
                foreach (var r in _all)
                {
                    try
                    {
                        bool exch = r.Goods == GExchange;
                        _h.Patch(r.M, prefix: new HarmonyMethod(me, exch ? nameof(ExchangeEnter) : nameof(Enter)), transpiler: new HarmonyMethod(me, nameof(Il)),
                                 finalizer: new HarmonyMethod(me, nameof(Leave)));
                        bool goodsOk = r.Goods == GNone || exch || r.GoodsCalls > 0 || r.Goods == GKept;
                        r.Patched = (r.Gold > 0 || r.Goods != GNone) && goodsOk;
                        if (!r.Patched) { miss.Add(r.Label + " (wywolan 0)"); continue; }
                        _patched++; typesHit.Add(r.M.DeclaringType); cPay += r.Gold; cGoods += r.GoodsCalls;
                        if (r.Goods != GNone) _goodsPatched++;
                        int c; perAsm.TryGetValue(r.Asm, out c); perAsm[r.Asm] = c + 1;
                    }
                    catch (Exception e) { miss.Add(r.Label + " (" + e.GetType().Name + ")"); Stumble("EnsureHooks(" + r.Label + ")", e); }
                }
                _types = typesHit.Count;
                // miejsca towaru i wyjatki z tabel, ktorych skan nie znalazl (gra zmienila tresc)
                foreach (var gs in GoodsSites)
                {
                    goodsWanted++;
                    bool found = false;
                    foreach (var r in _all) if (r.Patched && r.Goods != GNone && r.M.DeclaringType.Name == gs[0] && r.M.Name == gs[1]) { found = true; break; }
                    if (!found) miss.Add("towar " + gs[0] + "." + gs[1]);
                }
                foreach (var s in Sites)
                {
                    bool found = false;
                    foreach (var r in _all) if (r.Patched && (ReferenceEquals(r.In, s) || ReferenceEquals(r.Out, s))) { found = true; break; }
                    if (!found) miss.Add("platnik " + s.Stem + "." + s.Method);
                }
                // gettery RewardGold spraw (oferta w rozmowie)
                if (_rawReward != null)
                {
                    var post = new HarmonyMethod(me, nameof(RewardShown));
                    var seen = new HashSet<IntPtr>();
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            if (asm.IsDynamic || ReferenceEquals(asm, me.Assembly)) continue;
                            string gan = asm.GetName().Name;
                            if (gan == "StoryMode" || !RefsCampaign(asm, gan)) continue;
                            foreach (var t in TypesOf(asm))
                            {
                                try
                                {
                                    if (t == null || !t.IsClass || t.IsAbstract || t.ContainsGenericParameters || !typeof(IssueBase).IsAssignableFrom(t)) continue;
                                    var pi = t.GetProperty("RewardGold", AnyDeclared);
                                    var g = pi != null ? pi.GetGetMethod(true) : null;
                                    if (g == null || g.IsAbstract || g.GetMethodBody() == null || g.ReturnType != typeof(int) || !seen.Add(g.MethodHandle.Value)) continue;
                                    _gettersWanted++;
                                    try { _h.Patch(g, postfix: post); _getters++; }
                                    catch (Exception e) { miss.Add("getter " + t.Name + " (" + e.GetType().Name + ")"); Stumble("EnsureHooks(getter " + t.Name + ")", e); }
                                }
                                catch { _scanErr++; }
                            }
                        }
                        catch { _scanErr++; }
                    }
                }
                else miss.Add("getter IssueBase.RewardGold");
                var sb = new StringBuilder();
                sb.Append("wywolan podmienionych: zloto ").Append(cPay).Append(", towar ").Append(cGoods)
                  .Append("; miejsc towaru ").Append(_goodsPatched).Append('/').Append(goodsWanted)
                  .Append("; gettery nagrody ").Append(_getters).Append('/').Append(_gettersWanted).Append("; zestawy:");
                foreach (var kv in perAsm) sb.Append(' ').Append(kv.Key).Append(' ').Append(kv.Value);
                _wiredText = sb.ToString();
            }
            catch (Exception e) { Stumble("EnsureHooks", e); miss.Add("wyjatek skanu"); }
            _missText = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
            _scanMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            Log.Info(StatusLine("przy starcie pierwszej kampanii"));
        }

        // ------------------------------------------------------------ takt doby

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po CraftOrders8.Daily): porzadek w slowniku kwot i linia "Nagrody zadan (E8/D1): dzien N".</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            int pruned = 0;
            try
            {
                if (_pledges.Count > 0)
                {
                    var live = new HashSet<string>(StringComparer.Ordinal);
                    var im = Campaign.Current.IssueManager;
                    // sprawa zywa = w trakcie rozwiazywania (zadanie, wyslani ludzie, wplyw lorda); sprawa znow w ofercie po nieudanej probie nie trzyma starej kwoty
                    if (im != null && im.Issues != null) foreach (var kv in im.Issues) if (kv.Value != null && kv.Value.StringId != null && !kv.Value.IsOngoingWithoutQuest) live.Add(kv.Value.StringId);
                    var qm = Campaign.Current.QuestManager;
                    if (qm != null && qm.Quests != null)
                        foreach (var q in qm.Quests)
                        {
                            if (q == null || q.StringId == null) continue;
                            live.Add(q.StringId);
                            if (q.StringId.EndsWith("_quest", StringComparison.Ordinal)) live.Add(q.StringId.Substring(0, q.StringId.Length - 6));
                        }
                    var dead = new List<string>();
                    foreach (var k in _pledges.Keys) if (!live.Contains(k)) dead.Add(k);
                    foreach (var k in dead) _pledges.Remove(k);
                    pruned = dead.Count;
                }
            }
            catch (Exception e) { Stumble("Daily(porzadek)", e); }
            try { Log.Info(DayLine(day, pruned, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency)); }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        /// <summary>Linia doby z licznikow (osobno - probe poza gra wola ja wprost, zeby narzedzie sprawdzalo prawdziwy format).</summary>
        private static string DayLine(int day, int pruned, double costMs)
        {
            var sb = new StringBuilder(1200);
            long inPaid = _inPaidHero + _inPaidVillage;
            sb.Append("Nagrody zadan (E8/D1): dzien ").Append(day).Append(" | ").Append(KeysText())
              .Append(" | wyplaty dla gracza (nic -> gracz): ").Append(_inN).Append(", kwota gry ").Append(_inGame).Append(", obiecane ").Append(_inDue)
              .Append(", zaplacone ").Append(inPaid).Append(" (zleceniodawcy ").Append(_inPaidHero).Append(", kiesy wsi ").Append(_inPaidVillage).Append(')')
              .Append(", niedoplata przy pustej kiesie ").Append(_inShort).Append(" w ").Append(_inShortN).Append(" wyplatach")
              .Append(", z niczego ").Append(_inNothing);
            if (_noPayer.Count > 0) { var l = new List<string>(_noPayer); l.Sort(string.CompareOrdinal); sb.Append(" [brak platnika: ").Append(string.Join(", ", l.ToArray())).Append(']'); }
            sb.Append(" | wyplaty gracza (gracz -> nic): ").Append(_outN).Append(", kwota ").Append(_outAll).Append(": do kas osad ").Append(_outTown)
              .Append(", do zleceniodawcow ").Append(_outHero).Append(", do kies wsi ").Append(_outVillage).Append(", w nicosc ").Append(_outVoid)
              .Append(" | towar: od gracza na polke osady ").Append(_gSinkShelf).Append(" szt., do taboru zleceniodawcy ").Append(_gSinkTabor).Append(" szt.")
              .Append(", dla gracza z magazynu osady ").Append(_gFromShelf).Append(" szt., zabraklo ").Append(_gMissing).Append(" szt. - zamiast nich zloto ").Append(_gGoldInstead)
              .Append(" (niedoplata ").Append(_gGoldShort).Append(')')
              .Append(", towar z niczego ").Append(_gNothing).Append(" szt. / w nicosc ").Append(_gVoid).Append(" szt.")
              .Append(" | kwoty zamrozone: w pamieci ").Append(_pledges.Count).Append(", przyjete dzis ").Append(_frozenToday).Append(", zamkniete ").Append(pruned)
              .Append(", wyplat z kwota zamrozona ").Append(_inFrozen).Append(", liczonych przy wyplacie ").Append(_inComputed)
              .Append(", oferta pokazana po skali ").Append(_shown).Append(" razy")
              .Append(" | typy (wyplat: kwota gry / po skali):");
            if (_typeDay.Count == 0) sb.Append(" -");
            else
            {
                var names = new List<string>(_typeDay.Keys); names.Sort(string.CompareOrdinal);
                for (int i = 0; i < names.Count; i++)
                {
                    var a = _typeDay[names[i]];
                    sb.Append(i > 0 ? "; " : " ").Append(names[i]).Append(' ').Append(a[0]).Append(": ").Append(a[1]).Append(" / ").Append(a[2]);
                }
            }
            sb.Append(" | latki wpiete ").Append(_patched).Append('/').Append(_candidates).Append(", gettery ").Append(_getters).Append('/').Append(_gettersWanted)
              .Append(_missText != "-" ? " (BRAK: " + _missText + ")" : "")
              .Append(" | koszt ").Append(costMs.ToString("0.00", Inv)).Append(" ms/dobe")
              .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        // ------------------------------------------------------------ zapis (R14: sekcja arm_e8_questpay z wersja; stary zapis bez niej = pusty slownik)

        /// <summary>"1|klucz:obiecane:kwota gry:praca:ludzi:dni x100:zold x100:typ;..." - kwoty zamrozone przy przyjeciu zadan w toku.</summary>
        internal static string Export()
        {
            if (_pledges.Count == 0) return "";
            var sb = new StringBuilder("1|");
            bool first = true;
            foreach (var kv in _pledges)
            {
                var p = kv.Value;
                if (p == null || string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ':', ';', '|' }) >= 0) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(kv.Key).Append(':').Append(p.Cap.ToString(Inv)).Append(':').Append(p.Game.ToString(Inv)).Append(':').Append(p.Labor.ToString(Inv)).Append(':')
                  .Append(p.N.ToString(Inv)).Append(':').Append(((int)Math.Round(p.D * 100f)).ToString(Inv)).Append(':').Append(((int)Math.Round(p.Rate * 100f)).ToString(Inv)).Append(':')
                  .Append((p.Stem ?? "?").Replace(':', '_').Replace(';', '_').Replace('|', '_'));
            }
            return first ? "" : sb.ToString();
        }

        internal static void Import(string s)
        {
            _pledges.Clear();
            if (string.IsNullOrEmpty(s) || !s.StartsWith("1|", StringComparison.Ordinal)) return;
            foreach (var rec in s.Substring(2).Split(';'))
            {
                try
                {
                    var a = rec.Split(':');
                    if (a.Length < 8 || a[0].Length == 0) continue;
                    int cap, game, labor, n, d100, r100;
                    if (!int.TryParse(a[1], NumberStyles.Integer, Inv, out cap) || !int.TryParse(a[2], NumberStyles.Integer, Inv, out game)
                        || !int.TryParse(a[3], NumberStyles.Integer, Inv, out labor) || !int.TryParse(a[4], NumberStyles.Integer, Inv, out n)
                        || !int.TryParse(a[5], NumberStyles.Integer, Inv, out d100) || !int.TryParse(a[6], NumberStyles.Integer, Inv, out r100) || cap <= 0) continue;
                    _pledges[a[0]] = new Pledge { Key = a[0], Cap = cap, Game = game, Labor = labor, N = n, D = d100 / 100f, Rate = r100 / 100f, Stem = a[7] };
                }
                catch (Exception e) { Stumble("Import", e); }
            }
        }
    }
}
