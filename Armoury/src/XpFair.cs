using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// E8/X (248, PLAN 8.1b-d, 8.2c; docs/etap8/PROJEKT-ETAP8-... rozdz. 3 "E8/X", regula R12) - DOSWIADCZENIE ZA SKUTEK, nie za wypuszczony
    /// pocisk, zaplacona kwote ani dlugosc kolumny. Piec spraw, kazda z wlasnym kluczem (wylaczony = regula gry / RBM / RBL jak dotad):
    ///  1) ShotXpFair - STRZAL. RBM (RBMCombat.CampaignChanges+OverrideOnAgentShootMissile.Postfix, latka na Mission.OnAgentShootMissile) placi
    ///     bohaterowi 30 (luk, proca) / 60 (kusza) / 50 (bron miotana) za KAZDY wypuszczony pocisk - takze chybiony, na arenie, w turnieju i na
    ///     cwiczeniach. Prefiks na tej metodzie RBM (druga klasa o tej nazwie, RangedRework - predkosc pocisku - nietknieta) zdejmuje ja i placi
    ///     po naszemu: tylko w prawdziwej bitwie (Mission.CombatType == Combat i MapEvent.PlayerMapEvent != null) i w 1/3 stawki: luk 10, kusza 20,
    ///     miotana 17, proca 10. Poza bitwa 0 - trafienie i tak uczy (FairXp: arena 20%, turniej 50% stawki bitwy).
    ///  2) StewardFoodXpFair - ZARZADCA Z JEDZENIA. Gra (DefaultSkillLevelingManager.OnFoodConsumed; NavalDLC tylko przekazuje): XP = zjedzone x 100
    ///     x (rodzaje - 2) / 3 od 4 rodzajow - liniowo od liczby ludzi, zima +50% tylko dlatego, ze WinterBite podnosi zuzycie, a jedno jablko
    ///     to "rodzaj". Po naszemu: 50 x pierwiastek(ludzie) x (rodzaje - 2) / 3; rodzaj liczy sie od pol dnia zapasu dla calej druzyny; zima
    ///     x1.25 jako premia za trudnosc - z pory roku (WinterBite.WinterNow) i czynnika krainy E4/1 (WinterRealm.OfParty: Wyspy Letnie 0 -
    ///     bez premii, cieple poludnie 0.4 - x1.10, reszta x1.25); WinterBite i WinterRealm tylko czytane. 100 ludzi i 5 rodzajow = 500 jak w grze.
    ///  3) ScoutXpFair - ZWIAD Z MARSZU. Gra (OnTraverseTerrain): predkosc x (1 + ludzie^0.66) x 0.15 (las, pustynia, wydmy, snieg 0.25), placone
    ///     od 5, karawany polowa - rosnie z dlugoscia kolumny, maly oddzial nie dostaje nic; do tego premia tylko dla AI 3-5 na godzine
    ///     (OnAIPartiesTravel). Po naszemu: stale 10 na godzine marszu (16 w trudnym terenie - ta sama lista co w grze), karawany polowa, bez
    ///     progu, premia tylko-AI 0. Warunek "partia naprawde idzie" (predkosc > 1, pole gry _lastCalculatedSpeed) zostaje jak w grze.
    ///  4) SmallXpFair - PASER I SAMONAPRAWA (nasz kod): paser 25 Roguery za udana sprzedaz zamiast 5% zaplaty (SpoilsSeal.FencePrefix);
    ///     naprawa wlasnymi rekami: XP x udzial przywroconego stanu, najmniej 25% (SmithMenu.DoMendOne - obie galezie, Forge.SelfRepair).
    ///  5) RblPaidXpFair - RBL: 0 XP za zaplate (Charm 100 za kupiony glos w radzie, Roguery 50 za podstawionego szpiega i 150 za wynajetych
    ///     sabotazystow) - transpiler podmienia Hero.AddSkillXp na bramke w dwoch metodach RBL; relacja z kupionego glosu dalej uczy Charm.
    /// Wyplata XP z jedzenia i marszu idzie przez metode gry DefaultSkillLevelingManager.OnPartySkillExercised (delegat) - ta sama droga co w
    /// grze, wiec latka ROT (EnlistmentPatches.patch38: gracz na sluzbie u lorda nie uczy sie Steward / Scouting ze swojej ukrytej druzyny)
    /// dziala dalej. Znaczniki zrodla ksiegi E8/P1 (prefiksy First na tych samych metodach, biegna takze przy zdjetej metodzie) widza nasza
    /// wyplate jako "jedzenie" / "marsz" / "strzal RBM"; "marsz premia AI" spada do 0.
    /// R10 (dwie drogi w jednej linii): co dobe "Doswiadczenie (E8/X): dzien N" - dla kazdej sprawy "gra X / po Y" z tego samego biegu
    /// (gra = ile zaplacilaby regula gry / RBM / RBL dla tych samych wywolan), jedzenie i zwiad osobno dla gracza, lordow AI 300+ i ponizej 300.
    /// Latki raz na proces, w kampanii (OnSessionLaunched), tylko dla spraw wlaczonych w tej chwili; wylaczenie na zywo = prefiks przepuszcza
    /// regule gry. Wyjatek = licznik potkniec (bez wylacznika; przy wyjatku przed wyplata placi regula gry). Stan sesji czyszczony z
    /// SkillLedger.Reset (konstruktor ArmouryBehavior); bez zapisu. Linia startu: "FairXp (E8/X): start sesji - strzal RBM wpiety | ...".
    /// </summary>
    internal static class XpFair
    {
        // ---- stawki (projekt E8/X; liczby wybiera projekt, bez suwakow)
        internal const float ShotBow = 10f, ShotCrossbow = 20f, ShotThrown = 17f, ShotSling = 10f;   // 1/3 stawek RBM 30 / 60 / 50 / 30
        internal const float FoodBase = 50f, FoodWinter = 0.25f, FoodHalfDay = 0.5f;
        // TRIAZ 2 (13.10, X-3): 15 / 25 -> 10 / 16. Stare 15 liczono dla predkosci 4.55 (samotny konny gracz); kolumna z piechota ma sufit 3.0
        // (MarchFootPace 4.0 x WorldPace 75%): regula gry dla 100 ludzi daje wtedy 3.0 x 21.89 x 0.15 = 9.85 i x 0.25 = 16.4. Bieg 2: lordowie 300+
        // po/gra 0.79 (pasmo 0.4-0.6), caly swiat +20% zamiast spadku.
        internal const float ScoutPlain = 10f, ScoutHard = 16f, ScoutCaravan = 0.5f;
        internal const float FenceFlat = 25f, MendFloor = 0.25f;
        internal const int BigParty = 300;                                                           // progi X-2 / X-3: partie lordow 300+

        // ---- wiersze pomiaru jedzenia i zwiadu (wedlug partii)
        private const int RPlayer = 0, RLordBig = 1, RLordSmall = 2, RRest = 3, Rows = 4;
        private static readonly string[] RowNames = { "gracz", "lordowie AI 300+", "lordowie AI ponizej 300", "pozostale" };

        private static Clan _pClan;
        private static readonly long[] _shN = new long[2];                                           // strzal: [0] w bitwie, [1] poza bitwa
        private static readonly double[] _shGame = new double[2], _shFair = new double[2];
        private static readonly long[] _fdN = new long[Rows];
        private static readonly double[] _fdGame = new double[Rows], _fdFair = new double[Rows];
        private static long _fdKindsGame, _fdKinds, _fdWinter, _fdTicks;
        private static double _fdBigRoot;                                                            // lordowie AI 300+: sam pierwiastek (rodzaje gry, bez zimy) - rozklad progu X-2
        private static readonly long[] _scN = new long[Rows];
        private static readonly double[] _scGame = new double[Rows], _scFair = new double[Rows], _aiGame = new double[Rows];
        private static long _scCalls, _scSampled, _scTicks, _scIdle;
        private static long _fenceN, _mendN, _rblN;
        private static double _fenceGame, _fenceFair, _mendGame, _mendFair, _rblGame;
        private static int _stumbles, _lastDay = -1;
        private static bool _had;

        // ---- latki (raz na proces)
        private delegate void PartyXpFn(MobileParty party, SkillObject skill, float skillXp, PartyRole partyRole);
        private static Harmony _h;
        private static PartyXpFn _partyXp;                                                           // DefaultSkillLevelingManager.OnPartySkillExercised (z latka ROT w srodku)
        private static AccessTools.FieldRef<MobileParty, float> _speed;                              // MobileParty._lastCalculatedSpeed
        private static MethodInfo _mHeroXp;
        private static bool _baseTried, _shotTried, _foodTried, _scoutTried, _rblTried;
        private static bool _shotWired, _foodWired, _scoutWired;
        private static int _scoutCount, _rblCount, _rblSeen, _rblCalls;
        private const int RblCallsWant = 3;
        private static readonly List<string> _missing = new List<string>();

        /// <summary>Nowa gra / wczytanie (z SkillLedger.Reset - konstruktor ArmouryBehavior): liczniki okresu od zera. Latki zostaja (raz na proces).</summary>
        internal static void Reset()
        {
            _pClan = null;
            ClearPeriod();
            _stumbles = 0; _lastDay = -1; _had = false;
        }

        private static void ClearPeriod()
        {
            Array.Clear(_shN, 0, 2); Array.Clear(_shGame, 0, 2); Array.Clear(_shFair, 0, 2);
            Array.Clear(_fdN, 0, Rows); Array.Clear(_fdGame, 0, Rows); Array.Clear(_fdFair, 0, Rows);
            _fdKindsGame = _fdKinds = _fdWinter = _fdTicks = 0; _fdBigRoot = 0;
            Array.Clear(_scN, 0, Rows); Array.Clear(_scGame, 0, Rows); Array.Clear(_scFair, 0, Rows); Array.Clear(_aiGame, 0, Rows);
            _scCalls = _scSampled = _scTicks = _scIdle = 0;
            _fenceN = _mendN = _rblN = 0;
            _fenceGame = _fenceFair = _mendGame = _mendFair = _rblGame = 0;
        }

        private static int RowOf(MobileParty mp, int men)
        {
            if (mp == null) return RRest;
            if (mp.IsMainParty) return RPlayer;
            if (!mp.IsLordParty) return RRest;
            Clan c = mp.ActualClan;
            if (c == null || (object)c == (object)_pClan) return RRest;
            return men >= BigParty ? RLordBig : RLordSmall;
        }

        // ------------------------------------------------------------ 1) strzal (prefiks na metodzie RBM)
        /// <summary>Prefiks RBMCombat.CampaignChanges+OverrideOnAgentShootMissile.Postfix (te same nazwy parametrow; "ref Agent" RBM czytany jako
        /// wartosc). Wlaczony klucz: metoda RBM nie biegnie (robi TYLKO XP), nasza stawka tylko w bitwie kampanii. Wylaczony: RBM po swojemu.</summary>
        public static bool ShotPrefix(Agent shooterAgent, EquipmentIndex weaponIndex)
        {
            var s = Settings.Current;
            if (s == null || !s.ShotXpFair) return true;
            try
            {
                if (shooterAgent == null || !shooterAgent.IsHero || Campaign.Current == null) return false;   // RBM tez nic by nie zaplacil
                var ch = shooterAgent.Character as CharacterObject;
                Hero hero = ch != null ? ch.HeroObject : null;
                if (hero == null) return false;
                MissionWeapon w = shooterAgent.Equipment[weaponIndex];
                WeaponComponentData use = w.CurrentUsageItem;
                if (use == null) return false;
                float game, fair;
                switch (use.WeaponClass)
                {
                    case WeaponClass.Bow: game = 30f; fair = ShotBow; break;
                    case WeaponClass.Crossbow: game = 60f; fair = ShotCrossbow; break;
                    case WeaponClass.ThrowingAxe: case WeaponClass.ThrowingKnife: case WeaponClass.Javelin: game = 50f; fair = ShotThrown; break;
                    case WeaponClass.Sling: game = 30f; fair = ShotSling; break;
                    default: return false;                                                                    // RBM tez nic (kamien, proch)
                }
                Mission m = shooterAgent.Mission ?? Mission.Current;
                bool battle = m != null && m.CombatType == Mission.MissionCombatType.Combat && MapEvent.PlayerMapEvent != null;
                int row = battle ? 0 : 1;
                _shN[row]++; _shGame[row] += game;
                if (!battle) return false;                                                                    // arena, turniej, cwiczenia, bojka: 0
                SkillObject skill = Campaign.Current.Models.CombatXpModel.GetSkillForWeapon(use, false);
                if (skill == null) return false;
                hero.AddSkillXp(skill, fair);
                _shFair[0] += fair;
            }
            catch { _stumbles++; }
            return false;
        }

        // ------------------------------------------------------------ 2) zarzadca z jedzenia (prefiks na metodzie gry)
        /// <summary>Prefiks DefaultSkillLevelingManager.OnFoodConsumed. Warunki gry bez zmian (nie gloduje, ponad 3 rodzaje, jest kwatermistrz);
        /// zmienia sie kwota. BaseFoodChange czytane raz - tyle samo wywolan modelu co w grze. Wyjatek przed wyplata = placi regula gry.</summary>
        public static bool FoodPrefix(MobileParty mobileParty, bool wasStarving)
        {
            var s = Settings.Current;
            var pay = _partyXp;
            if (s == null || !s.StewardFoodXpFair || !_foodWired || pay == null || mobileParty == null) return true;
            bool mine = false;
            try
            {
                if (wasStarving) return false;
                long t0 = Stopwatch.GetTimestamp();
                ItemRoster bag = mobileParty.ItemRoster;
                int kindsGame = bag.FoodVariety;
                if (kindsGame <= 3 || mobileParty.EffectiveQuartermaster == null) return false;                // gra tez nic
                float need = -mobileParty.BaseFoodChange;                                                     // jednostek jedzenia na dobe
                float game = (float)TaleWorlds.Library.MathF.Round(need * 100f) * ((float)kindsGame - 2f) / 3f;
                int men = mobileParty.MemberRoster.TotalManCount;
                float half = need * FoodHalfDay;                                                              // rodzaj liczy sie od pol dnia zapasu calej druzyny
                int kinds = 0;
                for (int i = 0, n = bag.Count; i < n; i++)
                {
                    ItemRosterElement el = bag[i];
                    ItemObject it = el.EquipmentElement.Item;
                    if (it != null && it.IsFood && el.Amount > 0 && (float)el.Amount >= half) kinds++;
                }
                float fair = 0f;
                bool winter = false;
                if (kinds > 3)
                {
                    fair = FoodBase * (float)Math.Sqrt(Math.Max(0, men)) * ((float)kinds - 2f) / 3f;
                    if (WinterBite.WinterNow())
                    {
                        float realm = WinterRealm.OfParty(mobileParty);                                       // E4/1: zima wedlug krain (tylko odczyt)
                        if (realm > 0f) { fair *= 1f + FoodWinter * Math.Min(1f, realm); winter = true; }
                    }
                }
                int row = RowOf(mobileParty, men);
                _fdN[row]++; _fdGame[row] += game; _fdFair[row] += fair;
                _fdKindsGame += kindsGame; _fdKinds += kinds;
                if (row == RLordBig) _fdBigRoot += FoodBase * Math.Sqrt(Math.Max(0, men)) * (kindsGame - 2.0) / 3.0;
                if (winter) _fdWinter++;
                _fdTicks += Stopwatch.GetTimestamp() - t0;
                mine = true;
                if (fair > 0f) pay(mobileParty, DefaultSkills.Steward, fair, PartyRole.Quartermaster);
            }
            catch { _stumbles++; }
            return !mine;
        }

        // ------------------------------------------------------------ 3) zwiad z marszu (dwa prefiksy na metodach gry)
        /// <summary>Prefiks DefaultSkillLevelingManager.OnTraverseTerrain (co godzine dla kazdej idacej partii z wodzem i zwiadowca, nie na morzu):
        /// stala stawka zamiast predkosc x (1 + ludzie^0.66). Goracy punkt - bez alokacji; stoper probkowany 1/64 (bez nauki gry).</summary>
        public static bool TraversePrefix(MobileParty mobileParty, TerrainType currentTerrainType)
        {
            var s = Settings.Current;
            var pay = _partyXp;
            var speedOf = _speed;
            if (s == null || !s.ScoutXpFair || !_scoutWired || pay == null || speedOf == null || mobileParty == null) return true;
            bool mine = false;
            try
            {
                long t0 = (++_scCalls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
                float speed = speedOf(mobileParty);
                bool hard = currentTerrainType == TerrainType.Desert || currentTerrainType == TerrainType.Dune
                            || currentTerrainType == TerrainType.Forest || currentTerrainType == TerrainType.Snow;
                bool caravan = mobileParty.IsCaravan;
                int men = mobileParty.MemberRoster.TotalManCount;
                float game = 0f, fair = 0f;
                if (speed > 1f)
                {
                    game = speed * (1f + (float)Math.Pow(men, 0.66)) * (hard ? 0.25f : 0.15f);
                    fair = hard ? ScoutHard : ScoutPlain;
                }
                else _scIdle++;                                                                               // stoi (np. noc w obozie) - jak w grze: nic
                if (caravan) { game *= 0.5f; fair *= ScoutCaravan; }
                if (game < 5f) game = 0f;                                                                     // prog gry
                int row = RowOf(mobileParty, men);
                _scN[row]++; _scGame[row] += game; _scFair[row] += fair;
                if (t0 != 0L) { _scTicks += Stopwatch.GetTimestamp() - t0; _scSampled++; }
                mine = true;
                if (fair > 0f) pay(mobileParty, DefaultSkills.Scouting, fair, PartyRole.Scout);
            }
            catch { _stumbles++; }
            return !mine;
        }

        /// <summary>Prefiks DefaultSkillLevelingManager.OnAIPartiesTravel: premia tylko dla AI (3, w lesie 5, karawany polowa) = 0.</summary>
        public static bool AiTravelPrefix(Hero hero, bool isCaravanParty, TerrainType currentTerrainType)
        {
            var s = Settings.Current;
            if (s == null || !s.ScoutXpFair || !_scoutWired) return true;
            try
            {
                float game = currentTerrainType == TerrainType.Forest ? 5f : 3f;
                if (isCaravanParty) game *= 0.5f;
                MobileParty mp = hero != null ? hero.PartyBelongedTo : null;
                _aiGame[RowOf(mp, mp != null ? mp.MemberRoster.TotalManCount : 0)] += game;
            }
            catch { _stumbles++; }
            return false;
        }

        // ------------------------------------------------------------ 4) paser i samonaprawa (nasz kod)
        /// <summary>Paser (SpoilsSeal.FencePrefix, po udanej sprzedazy): 25 Roguery zamiast 5% zaplaty. Wylaczony klucz = dawna kwota.</summary>
        internal static float FenceXp(int paid)
        {
            float game = (float)paid * 0.05f;
            var s = Settings.Current;
            if (s == null || !s.SmallXpFair) return game;
            _fenceN++; _fenceGame += game; _fenceFair += FenceFlat;
            return FenceFlat;
        }

        /// <summary>Udzial stanu, ktory naprawa przywraca sztuce (0..1) - ze stanu sztuki, jak material napraw (MendMaterial.SelfPartsOf).</summary>
        internal static float Restored(EquipmentElement el)
        {
            float c = ConditionScaling.ConditionOf(el.ItemModifier);
            if (!(c > 0f)) c = 0f;
            if (c > 1f) c = 1f;
            return 1f - c;
        }

        /// <summary>Naprawa wlasnymi rekami: pelne XP x udzial przywroconego stanu, najmniej 25%. Wylaczony klucz = pelne XP jak dotad.</summary>
        internal static float SelfMendXp(float full, float restored)
        {
            var s = Settings.Current;
            if (s == null || !s.SmallXpFair) return full;
            float share = restored;
            if (!(share > MendFloor)) share = MendFloor;                                                      // takze NaN
            if (share > 1f) share = 1f;
            float xp = full * share;
            _mendN++; _mendGame += full; _mendFair += xp;
            return xp;
        }

        // ------------------------------------------------------------ 5) RBL: 0 XP za zaplate (transpiler + bramka)
        /// <summary>Wstawiane przez transpiler w miejsce Hero.AddSkillXp w PoliticsDialogueBehavior.consequence_alliance_success i
        /// EspionageMenuBehavior.ResolveSabotageDialogue (stos ten sam: bohater, umiejetnosc, XP). Wylaczony klucz = XP jak w RBL.</summary>
        public static void RblXp(Hero hero, SkillObject skill, float xp)
        {
            var s = Settings.Current;
            if (s != null && s.RblPaidXpFair) { _rblN++; _rblGame += xp; return; }
            if (hero != null) hero.AddSkillXp(skill, xp);
        }

        public static IEnumerable<CodeInstruction> RblTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo gate = AccessTools.Method(typeof(XpFair), nameof(RblXp));
            foreach (var c in instructions)
            {
                if (gate != null && _mHeroXp != null && (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && c.operand is MethodInfo && Equals(c.operand, _mHeroXp))
                { c.opcode = OpCodes.Call; c.operand = gate; _rblSeen++; }
                yield return c;
            }
        }

        // ------------------------------------------------------------ takt doby i linia
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string L(double v) { return ((long)Math.Round(v)).ToString(Inv); }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static string Tag(bool key, bool wired) { return !key ? "[WYLACZONE]" : wired ? "[wl]" : "[nie wpiete - regula gry]"; }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia za dobe od poprzedniego taktu (etykieta jak "Umiejetnosci (E8/P1)"); pierwszy takt sesji bez linii.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays;
            try { _pClan = Clan.PlayerClan; } catch { }
            bool any = s != null && (s.ShotXpFair || s.StewardFoodXpFair || s.ScoutXpFair || s.SmallXpFair || s.RblPaidXpFair);
            if (!any) { _had = false; ClearPeriod(); return; }
            try { if (_had) Line(s); }
            catch (Exception e) { Log.Error("XpFair.Daily", e); }
            ClearPeriod();
            _had = true; _lastDay = day;
        }

        private static void Line(Settings s)
        {
            var sb = new StringBuilder(1200);
            sb.Append("Doswiadczenie (E8/X): dzien ").Append(_lastDay);
            sb.Append(" | strzal ").Append(Tag(s.ShotXpFair, _shotWired))
              .Append(": w bitwie ").Append(_shN[0]).Append(" strzalow gra ").Append(L(_shGame[0])).Append(" / po ").Append(L(_shFair[0]))
              .Append(", poza bitwa ").Append(_shN[1]).Append(" strzalow gra ").Append(L(_shGame[1])).Append(" / po ").Append(L(_shFair[1]));
            sb.Append(" | jedzenie ").Append(Tag(s.StewardFoodXpFair, _foodWired)).Append(": ");
            long fdAll = 0;
            for (int r = 0; r < Rows; r++)
            {
                fdAll += _fdN[r];
                sb.Append(r == 0 ? "" : ", ").Append(RowNames[r]).Append(' ').Append(_fdN[r]).Append(" partii gra ").Append(L(_fdGame[r])).Append(" / po ").Append(L(_fdFair[r]));
            }
            sb.Append("; lordowie AI 300+ sam pierwiastek ").Append(L(_fdBigRoot));
            sb.Append("; rodzaje na partie gra ").Append(fdAll > 0 ? F1((double)_fdKindsGame / fdAll) : "-").Append(" / po ").Append(fdAll > 0 ? F1((double)_fdKinds / fdAll) : "-")
              .Append("; zima ").Append(_fdWinter).Append(" partii");
            sb.Append(" | zwiad ").Append(Tag(s.ScoutXpFair, _scoutWired)).Append(": ");
            for (int r = 0; r < Rows; r++)
                sb.Append(r == 0 ? "" : ", ").Append(RowNames[r]).Append(' ').Append(_scN[r]).Append(" godzin gra ").Append(L(_scGame[r] + _aiGame[r]))
                  .Append(" (w tym premia AI ").Append(L(_aiGame[r])).Append(") / po ").Append(L(_scFair[r]));
            sb.Append("; stoi ").Append(_scIdle).Append(" godzin");
            sb.Append(" | drobne ").Append(Tag(s.SmallXpFair, true))
              .Append(": paser ").Append(_fenceN).Append(" sprzedazy gra ").Append(L(_fenceGame)).Append(" / po ").Append(L(_fenceFair))
              .Append(", samonaprawa ").Append(_mendN).Append(" sztuk gra ").Append(L(_mendGame)).Append(" / po ").Append(L(_mendFair));
            sb.Append(" | RBL ").Append(Tag(s.RblPaidXpFair, _rblCount == 2 && _rblCalls == RblCallsWant))
              .Append(": ").Append(_rblN).Append(" wyplat gra ").Append(L(_rblGame)).Append(" / po 0");
            double fMs = Ms(_fdTicks), sMs = _scSampled > 0 ? Ms(_scTicks) * ((double)_scCalls / _scSampled) : 0.0;
            sb.Append(" | potkniecia ").Append(_stumbles);
            sb.Append(" | koszt wlasny ~").Append(F1(fMs + sMs)).Append(" ms (jedzenie ").Append(F1(fMs)).Append(" ms/").Append(fdAll)
              .Append(", zwiad ~").Append(F1(sMs)).Append(" ms/").Append(_scCalls).Append("; bez nauki gry)");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ latki (raz na proces, w kampanii)
        private static MethodBase Declared(Type t, string name)
        {
            if (t == null) return null;
            try
            {
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (m.Name == name && !m.IsAbstract && !m.IsGenericMethodDefinition) return m;
            }
            catch { }
            return null;
        }

        private static bool Wire(string label, MethodBase m, string prefix, string transpiler)
        {
            try
            {
                if (m == null) { _missing.Add(label); return false; }
                _h.Patch(m, prefix: prefix != null ? new HarmonyMethod(typeof(XpFair), prefix) : null,
                            transpiler: transpiler != null ? new HarmonyMethod(typeof(XpFair), transpiler) : null);
                return true;
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); return false; }
        }

        /// <summary>Wspolne dla jedzenia i zwiadu: wyplata przez metode gry (latka ROT na niej dziala dalej) i odczyt predkosci partii.</summary>
        private static void HookBase()
        {
            try
            {
                var m = AccessTools.Method(typeof(DefaultSkillLevelingManager), "OnPartySkillExercised",
                                           new[] { typeof(MobileParty), typeof(SkillObject), typeof(float), typeof(PartyRole) });
                if (m != null && m.IsStatic) _partyXp = (PartyXpFn)Delegate.CreateDelegate(typeof(PartyXpFn), m);
            }
            catch { _partyXp = null; }
            try { _speed = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); }
            catch { _speed = null; }
        }

        private static void HookShot()
        {
            Type t = null;
            try { t = AccessTools.TypeByName("RBMCombat.CampaignChanges+OverrideOnAgentShootMissile"); } catch { }
            _shotWired = Wire("strzal RBM (RBMCombat.CampaignChanges+OverrideOnAgentShootMissile.Postfix)", Declared(t, "Postfix"), nameof(ShotPrefix), null);
        }

        private static void HookFood()
        {
            const string label = "jedzenie (DefaultSkillLevelingManager.OnFoodConsumed)";
            if (_partyXp == null) { _missing.Add(label + " - brak DefaultSkillLevelingManager.OnPartySkillExercised"); return; }
            MethodBase m = null;
            try { m = AccessTools.Method(typeof(DefaultSkillLevelingManager), "OnFoodConsumed", new[] { typeof(MobileParty), typeof(bool) }); } catch { }
            _foodWired = Wire(label, m, nameof(FoodPrefix), null);
        }

        private static void HookScout()
        {
            const string label = "zwiad (DefaultSkillLevelingManager.OnTraverseTerrain / OnAIPartiesTravel)";
            if (_partyXp == null) { _missing.Add(label + " - brak DefaultSkillLevelingManager.OnPartySkillExercised"); return; }
            if (_speed == null) { _missing.Add(label + " - brak MobileParty._lastCalculatedSpeed"); return; }
            MethodBase a = null, b = null;
            try { a = AccessTools.Method(typeof(DefaultSkillLevelingManager), "OnTraverseTerrain", new[] { typeof(MobileParty), typeof(TerrainType) }); } catch { }
            try { b = AccessTools.Method(typeof(DefaultSkillLevelingManager), "OnAIPartiesTravel", new[] { typeof(Hero), typeof(bool), typeof(TerrainType) }); } catch { }
            if (a == null || b == null)
            {
                _missing.Add("zwiad (DefaultSkillLevelingManager." + (a == null ? "OnTraverseTerrain" : "OnAIPartiesTravel") + ")");
                return;                                                                                       // jedna regula albo zadna - bez polowy
            }
            if (Wire("zwiad (DefaultSkillLevelingManager.OnTraverseTerrain)", a, nameof(TraversePrefix), null)) _scoutCount++;
            if (Wire("zwiad (DefaultSkillLevelingManager.OnAIPartiesTravel)", b, nameof(AiTravelPrefix), null)) _scoutCount++;
            _scoutWired = _scoutCount == 2;                                                                   // prefiksy przepuszczaja regule gry, dopoki nie ma obu
        }

        private static void HookRbl()
        {
            try { _mHeroXp = AccessTools.Method(typeof(Hero), "AddSkillXp", new[] { typeof(SkillObject), typeof(float) }); } catch { _mHeroXp = null; }
            if (_mHeroXp == null) { _missing.Add("RBL - brak Hero.AddSkillXp"); return; }
            string[][] targets =
            {
                new[] { "RealisticBannerlord.Systems.Politics.PoliticsDialogueBehavior", "consequence_alliance_success" },
                new[] { "RealisticBannerlord.Systems.Espionage.EspionageMenuBehavior", "ResolveSabotageDialogue" }
            };
            foreach (var tg in targets)
            {
                Type t = null;
                try { t = AccessTools.TypeByName(tg[0]); } catch { }
                int before = _rblSeen;
                if (Wire("RBL " + tg[0].Substring(tg[0].LastIndexOf('.') + 1) + "." + tg[1], Declared(t, tg[1]), null, nameof(RblTranspiler)))
                {
                    _rblCount++;
                    _rblCalls += _rblSeen - before;                                                           // pozniejsze przebiegi transpilera (cudza latka na tej metodzie) nie licza sie drugi raz
                }
            }
            if (_rblCount == 2 && _rblCalls != RblCallsWant) _missing.Add("RBL - wyplat XP w kodzie " + _rblCalls + " zamiast " + RblCallsWant);
        }

        /// <summary>Z OnSessionLaunched: wpina (raz na proces) latki spraw wlaczonych w tej chwili i pisze linie startu sesji.</summary>
        internal static void EnsureHooks()
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                if (s == null) return;
                try { _pClan = Clan.PlayerClan; } catch { }
                if (_h == null) _h = new Harmony("com.jeff.armoury.xpfair");
                if ((s.StewardFoodXpFair || s.ScoutXpFair) && !_baseTried) { _baseTried = true; HookBase(); }
                if (s.ShotXpFair && !_shotTried) { _shotTried = true; HookShot(); }
                if (s.StewardFoodXpFair && !_foodTried) { _foodTried = true; HookFood(); }
                if (s.ScoutXpFair && !_scoutTried) { _scoutTried = true; HookScout(); }
                if (s.RblPaidXpFair && !_rblTried) { _rblTried = true; HookRbl(); }

                int want = (_shotTried ? 1 : 0) + (_foodTried ? 1 : 0) + (_scoutTried ? 2 : 0) + (_rblTried ? 2 : 0);
                int wired = (_shotWired ? 1 : 0) + (_foodWired ? 1 : 0) + _scoutCount + _rblCount;
                var sb = new StringBuilder(700);
                sb.Append("FairXp (E8/X): start sesji - strzal RBM ")
                  .Append(!s.ShotXpFair ? "WYLACZONY (ShotXpFair off)" : _shotWired ? "wpiety" : "nie znaleziono");
                sb.Append(" | jedzenie ").Append(!s.StewardFoodXpFair ? "WYLACZONE (StewardFoodXpFair off)" : _foodWired ? "wpiete" : "nie znaleziono");
                sb.Append(" | zwiad ").Append(!s.ScoutXpFair ? "WYLACZONY (ScoutXpFair off)" : "wpiety " + _scoutCount + "/2");
                sb.Append(" | RBL ").Append(!s.RblPaidXpFair ? "WYLACZONE (RblPaidXpFair off)" : "wpiete " + _rblCount + "/2, wyplaty " + _rblCalls + "/" + RblCallsWant);
                sb.Append(" | paser i samonaprawa (nasz kod) ").Append(s.SmallXpFair ? "wl" : "WYLACZONE (SmallXpFair off)");
                sb.Append(" | latki wpiete ").Append(wired).Append('/').Append(want);
                sb.Append(" | stawki: strzal ").Append(L(ShotBow)).Append('/').Append(L(ShotCrossbow)).Append('/').Append(L(ShotThrown)).Append('/').Append(L(ShotSling))
                  .Append(" (luk/kusza/miotana/proca) tylko w bitwie, jedzenie ").Append(L(FoodBase)).Append(" x pierwiastek(ludzie) x (rodzaje - 2)/3 (rodzaj od pol dnia zapasu, zima x1.25 wedlug krainy), zwiad ")
                  .Append(L(ScoutPlain)).Append('/').Append(L(ScoutHard)).Append(" na godzine marszu (karawany polowa, premia AI 0), paser ").Append(L(FenceFlat))
                  .Append(", samonaprawa x przywrocony stan (min. 25%), RBL 0 za zaplate");
                if (_missing.Count > 0) sb.Append(" | nie znaleziono: ").Append(string.Join(", ", _missing.ToArray()));
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("XpFair.EnsureHooks", e); }
        }
    }
}
