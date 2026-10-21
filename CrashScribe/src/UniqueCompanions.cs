using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CrashScribe
{
    /// <summary>
    /// UNIKATY ROT - TOWARZYSZE (decyzja Jeffa 14.10: "tak, popraw te rzeczy w companions - jak jest martwy companion, to jest martwy";
    /// rozpoznanie docs/pytania-2026-10-14/TOWARZYSZE-DUPLIKATY.md, rozdz. 5 i 7 "Weryfikacja").
    /// Unikat = bohater zrobiony z wzoru ROT "ROTwanderer*" (63 wzory: Tyrion, Jaime, Bronn...) BEZ ojca i matki. Dzieci unikatu maja ten
    /// sam Template (CharacterObject.CreateFrom: _originCharacter rodzica), wiec wszedzie liczymy je osobno.
    /// Skad blad (gra 1.4.8 + ROT 8.1.8, dekompilacja):
    ///  - ROT ROTSpawnUniqueWanderers.SpawnHero (co dobe): tworzy unikat, gdy w AllAliveHeroes + DeadOrDisabledHeroes nie ma NIKOGO z tym wzorem;
    ///  - gra CompanionsCampaignBehavior: OnHeroKilled wymazuje z listy zmarlych niepoznanego wedrowca od razu (UnregisterDeadHero),
    ///    WeeklyTick kazdego zmarlego wedrowca po 40 dobach; TryKillCompanion (10% na dobe) usuwa wolnego wedrowca (ApplyByRemove, Lost).
    ///  - skutek: martwy Tyrion wraca do karczmy jako nowa osoba z ta sama twarza, a usuniety "przeprowadza sie" przez smierc i nowe stworzenie.
    /// Piec czesci, kazda pod wlasnym kluczem CrashScribe.settings.xml (domyslnie true; false = zachowanie sprzed zmiany, b47fecc3):
    ///  (1) UniqueCompanionsStayDead - unikat, ktory naprawde zginal (DeathMark rozny od Lost), zostaje na liscie zmarlych gry: prefiks na
    ///      CampaignObjectManager.UnregisterDeadHero nie wymazuje go (wtedy straz ROT go widzi i nie robi nowego). Druga zapora: prefiks na
    ///      ROT ROTSpawnUniqueWanderers.SpawnHero nie tworzy wzoru z naszej listy zmarlych, gdy z listy gry zniknal (np. metoda gry wbudowana
    ///      przez JIT w wolajacego - Harmony jej wtedy nie widzi); wzor z zywym nastepca (odtworzony przed paczka albo przy kluczu false) nie
    ///      jest pod ta straza. Lista zmarlych w zapisie (SyncData "cs_uniq_dead", List of string).
    ///      Gracz - ta sama regula (zmarly towarzysz gracza tez). Zgon dziecka unikatu - jak w grze (inaczej martwe dziecko blokowaloby wzor).
    ///  (2) UniqueCompanionsMove - TryKillCompanion gry wybiera jak dotad (ten sam rzut i ten sam wzor), ale wolnego unikatu (bez rodu,
    ///      bez druzyny, nie jeniec, stan Active / Fugitive / Released) nie usuwa, tylko PRZENOSI do losowego miasta jego kultury (bez miasta
    ///      gracza, oblezonych i miast Innych; brak - dowolne takie miasto): ta sama osoba, nie znika z karczm na zawsze, nie ma nowego
    ///      stworzenia. Zwolnienie towarzysza z rozmowy (CompanionRolesCampaignBehavior.companion_fire_on_consequence) to w grze ApplyByFire
    ///      i zaraz ApplyByRemove (Lost) - recenzja 14.10: ten sam znacznik, zwolniony unikat (wolny po ApplyByFire) przenosi sie tak samo.
    ///      Odejscie towarzysza po klotni (CompanionRolesCampaignBehavior.OnHeroRelationChanged: relacja z graczem < -10) i towarzysz
    ///      zabrany przez gre przy resecie perkow (PerkResetCampaignBehavior.RemoveACompanionFromPlayerParty) - w grze ApplyByRemove (Lost)
    ///      jeszcze w rodzie gracza; Jeff 14.10 ("to ten sam karzel"): znacznik _inLeave - unikat najpierw odchodzi z rodu jak przy zwolnieniu
    ///      (RemoveCompanionAction.ApplyByFire), potem przenosi sie tak samo. Unikat nie do przeniesienia, dziecko unikatu i inne usuniecia
    ///      - gra jak dotad (Lost; po wymazaniu ROT go odtworzy, bo Lost to nie smierc - punkt 1 go nie blokuje).
    ///  (3) UniqueCompanionsDuplicateGuard - raz na dobe: dwa zywe unikaty (bez rodzicow) z tym samym wzorem - zostaje jeden (rod gracza,
    ///      potem rod / towarzysz, druzyna, poznany, najstarszy wpis), wolnego drugiego (bez rodu, druzyny, niewoli, nie w miescie gracza)
    ///      usuwamy jak gra (ApplyByRemove, Lost). Nie do usuniecia - tylko log. Oczekiwane 0 (z kodu duplikatow nie ma).
    ///  (4) UniqueCompanionsChildren - dziecko unikatu (postfiks HeroCreator.DeliverOffSpring): imie gra nadaje ZANIM zmieni zawod
    ///      (wedrowiec -> pelne imie = nazwa wzoru, "Tyrion Lannister"); gdy pelne imie = nazwa wzoru, dostaje zwykle imie jak lord tej kultury
    ///      (imie juz wylosowane przez gre, "{FIRSTNAME} of the {CLAN_NAME}" w rodzie najemnikow / koczownikow / sekcie - jak NameGenerator).
    ///      Imie poprawione przez BK (rodzic pasowany) zostaje. Stroj: dziecko rodzi sie w zestawach wzoru rodzica (Hero.SetCharacterObject),
    ///      gra przebiera je w zestaw dziecka kultury, a ochrona unikatow Armoury (UniqueSpoils.AssignPostfix) oddaje mu kopie rzeczy wzoru -
    ///      sloty z rzeczami wzoru dostaja zestaw dziecka kultury (przypisanie wprost do slotu, nie przez EquipmentHelper - Armoury nie oddaje
    ///      kopii; brak zestawu - zestaw cywilny neutral_culture jak gra; ukrycia - pusty slot). Tylko przy narodzinach (rzeczy odziedziczone pozniej po rodzicu zostaja). Przy starcie sesji - imiona
    ///      dzieci juz zyjacych (stroj nie).
    ///  (5) UniqueCompanionsLog - linia doby "Unikaty ROT (UC): dzien N | ..." - kazdy wzor: zywy / martwy / gdzie / rod / zawod, dzieci
    ///      unikatow, liczniki (duplikaty, odrodzenia, przeprowadzki, blokady wymazania). Narzedzie: tools/sprawdz_logi.py --grupa unikaty.
    /// Wyjatek w latce - gra jak dotad (prefiks przepuszcza), licznik potkniec (CLAUDE.md 7); raport tylko pierwszego.
    /// m101-postacie (21.10): Armoury (CanonCompanions, klucz CanonCompanionsToClans, tylko nowa kampania) przenosi postacie kanonu do rodow, a wzorow z grupy B
    /// (Hodor, Syrio...) nie tworzy wcale; po starcie zaden wzor A/B nie powstaje drugi raz (prefiks Armoury na ROT SpawnHero). Linia doby liczy taki wzor bez
    /// bohatera jako "usuniete z kanonu" (nie "brak" - UN-5), a usuniecie Lost postaci kanonu nie mowi "ROT go odtworzy" (CanonBlocked - refleksja, Armoury
    /// Armoury.CanonCompanions.IsCanonBlocked(string); brak Armoury / metody = jak dotad).
    /// </summary>
    internal static class UniqueCompanions
    {
        private const string Tag = "Unikaty ROT (UC)";
        internal const string WandererPrefix = "ROTwanderer";   // ROT ROTSpawnUniqueWanderers.ROT_WANDERER_ID_SUBSTRING

        // --- zapis (SyncData "cs_uniq_dead"): zmarli unikaty, wpis "idBohatera|wzor|dzien|rodzaj|imie" ---
        internal static List<string> Dead = new List<string>();
        private static readonly HashSet<string> _deadHeroes = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _deadTemplates = new HashSet<string>(StringComparer.Ordinal);   // straz ROT: wzory, ktorych ostatni unikat zginal (bez zywego nastepcy)
        private static readonly Dictionary<string, string> _deadInfo = new Dictionary<string, string>(StringComparer.Ordinal);   // wzor -> "dzien|rodzaj|imie" (ostatni)

        // --- latki (raz na proces) ---
        private static bool _installed, _pUnreg, _pSpawn, _pTryKill, _pKill, _pChild, _pFire, _pLeave;
        private static string _patchLine = "";
        private static int _inTryKill;   // > 0: trwa TryKillCompanion gry (watek kampanii)
        private static int _inFire;      // > 0: trwa zwolnienie towarzysza z rozmowy (CompanionRolesCampaignBehavior.companion_fire_on_consequence: ApplyByFire, zaraz potem ApplyByRemove)
        private static int _inLeave;     // > 0: trwa odejscie towarzysza po klotni (OnHeroRelationChanged) albo zabranie przy resecie perkow (RemoveACompanionFromPlayerParty) - ApplyByRemove jeszcze w rodzie gracza

        // --- sesja ---
        private static bool _live;       // po OnSessionLaunched (nowa gra: ROT tworzy unikaty w OnNewGameCreated, czyli wczesniej) - odrodzenie = nowy unikat potem
        private static bool _inGuard;    // trwa usuwanie duplikatu przez straznika (podpis w linii Lost)
        private static List<CharacterObject> _templates;
        private static readonly HashSet<string> _spawnBlockSaid = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _blockSaid = new HashSet<string>(StringComparer.Ordinal);   // bohaterowie z linia blokady wymazania w tej sesji
        private static int _dMoved, _tMoved, _dLostGame, _dLostOther, _dKept, _dSpawnBlocked, _dRespawn, _tRespawn, _dDupRemoved, _dDupLeft;
        private static int _dChildName, _dChildGear, _stumbles, _stumblesDay;
        private static System.Reflection.MethodInfo _canonFn;   // m101-postacie: Armoury.CanonCompanions.IsCanonBlocked(string) - raz na proces
        private static bool _canonTried;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        // ------------------------------------------------------------ rozpoznanie
        internal static bool IsRotTemplate(CharacterObject t)
        {
            return t != null && t.StringId != null && t.StringId.StartsWith(WandererPrefix, StringComparison.Ordinal);
        }

        /// <summary>Unikat ROT: wzor ROTwanderer*, bez ojca i matki (dziecko unikatu ma ten sam wzor).</summary>
        internal static bool IsOriginal(Hero h)
        {
            return h != null && IsRotTemplate(h.Template) && h.Father == null && h.Mother == null;
        }

        /// <summary>m101-postacie: wzor zablokowany przez postacie kanonu Armoury (ROT go nie odtworzy w tej kampanii); brak Armoury / metody - false (jak dotad).</summary>
        private static bool CanonBlocked(string templateId)
        {
            if (!_canonTried)
            {
                _canonTried = true;
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm == null || asm.GetName().Name != "Armoury") continue;
                        var t = asm.GetType("Armoury.CanonCompanions");
                        if (t != null)
                            _canonFn = t.GetMethod("IsCanonBlocked", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                                                   null, new[] { typeof(string) }, null);
                        break;
                    }
                }
                catch { _canonFn = null; }
            }
            if (_canonFn == null || templateId == null) return false;
            try { return (bool)_canonFn.Invoke(null, new object[] { templateId }); }
            catch { return false; }
        }

        private static string Short(string templateId)
        {
            return templateId != null && templateId.StartsWith(WandererPrefix, StringComparison.Ordinal) ? "#" + templateId.Substring(WandererPrefix.Length) : (templateId ?? "?");
        }

        private static string NameOf(Hero h)
        {
            try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; }
        }

        private static int Day()
        {
            try { return (int)CampaignTime.Now.ToDays; } catch { return 0; }
        }

        // ------------------------------------------------------------ lista zmarlych (zapis)
        private static bool Remember(Hero h, KillCharacterAction.KillCharacterActionDetail detail)
        {
            if (h == null || h.StringId == null || _deadHeroes.Contains(h.StringId)) return false;
            string tid = h.Template != null ? h.Template.StringId : "?";
            int day = 0;
            try { day = (int)h.DeathDay.ToDays; } catch { }
            string entry = h.StringId + "|" + tid + "|" + day + "|" + detail + "|" + NameOf(h).Replace("|", "/");
            if (Dead == null) Dead = new List<string>();
            Dead.Add(entry);
            Index(entry);
            return true;
        }

        private static void Index(string entry)
        {
            var p = entry.Split('|');
            if (p.Length < 2) return;
            _deadHeroes.Add(p[0]);
            _deadTemplates.Add(p[1]);
            _deadInfo[p[1]] = (p.Length > 2 ? p[2] : "?") + "|" + (p.Length > 3 ? p[3] : "?") + "|" + (p.Length > 4 ? p[4] : "?");
        }

        /// <summary>Po wczytaniu (SyncData): indeksy z listy z zapisu.</summary>
        internal static void Rebuild()
        {
            if (Dead == null) Dead = new List<string>();
            _deadHeroes.Clear(); _deadTemplates.Clear(); _deadInfo.Clear();
            foreach (var e in Dead) if (!string.IsNullOrEmpty(e)) Index(e);
        }

        /// <summary>Nowa gra / wczytanie (konstruktor zachowania w OnGameStart, przed SyncData): stan od zera.</summary>
        internal static void ResetSession()
        {
            Dead = new List<string>();
            _deadHeroes.Clear(); _deadTemplates.Clear(); _deadInfo.Clear();
            _spawnBlockSaid.Clear(); _blockSaid.Clear();
            _templates = null;
            _live = false; _inTryKill = 0; _inFire = 0; _inLeave = 0; _inGuard = false;
            _tMoved = _tRespawn = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            _dMoved = _dLostGame = _dLostOther = _dKept = _dSpawnBlocked = _dRespawn = _dDupRemoved = _dDupLeft = 0;
            _dChildName = _dChildGear = 0; _stumblesDay = 0;
        }

        // ------------------------------------------------------------ wpiecie (raz na proces, OnBeforeInitialModuleScreenSetAsRoot)
        internal static void Install(Harmony harmony)
        {
            if (_installed) return;
            _installed = true;
            var parts = new List<string>();
            try
            {
                if (Config.UniqueCompanionsStayDead)
                {
                    var mUnreg = AccessTools.Method(typeof(CampaignObjectManager), "UnregisterDeadHero", new[] { typeof(Hero) });
                    if (mUnreg != null)
                    {
                        try { harmony.Patch(mUnreg, prefix: new HarmonyMethod(typeof(UniqueCompanions), nameof(UnregPrefix))); _pUnreg = true; }
                        catch (Exception e) { Stumble("UniqueCompanions.Install(UnregisterDeadHero)", e); }
                    }
                    parts.Add("wymazanie zmarlych " + (_pUnreg ? "PODPIETE" : "BRAK (CampaignObjectManager.UnregisterDeadHero)"));
                    var tRot = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTSpawnUniqueWanderers");
                    var mSpawn = tRot != null ? AccessTools.Method(tRot, "SpawnHero", new[] { typeof(CharacterObject) }) : null;
                    if (mSpawn != null)
                    {
                        try { harmony.Patch(mSpawn, prefix: new HarmonyMethod(typeof(UniqueCompanions), nameof(RotSpawnPrefix))); _pSpawn = true; }
                        catch (Exception e) { Stumble("UniqueCompanions.Install(ROT SpawnHero)", e); }
                    }
                    parts.Add("straz ROT " + (_pSpawn ? "PODPIETA" : "BRAK (" + (tRot == null ? "ROT nieobecny" : "ROTSpawnUniqueWanderers.SpawnHero") + ")"));
                }
                else parts.Add("wymazanie zmarlych WYLACZONE (UniqueCompanionsStayDead = false)");

                if (Config.UniqueCompanionsMove)
                {
                    var mTry = AccessTools.Method(typeof(CompanionsCampaignBehavior), "TryKillCompanion");
                    var mKill = AccessTools.Method(typeof(KillCharacterAction), "ApplyInternal",
                        new[] { typeof(Hero), typeof(Hero), typeof(KillCharacterAction.KillCharacterActionDetail), typeof(bool), typeof(bool) });
                    if (mTry != null && mKill != null)
                    {
                        try
                        {
                            harmony.Patch(mTry, prefix: new HarmonyMethod(typeof(UniqueCompanions), nameof(TryKillPrefix)),
                                          finalizer: new HarmonyMethod(typeof(UniqueCompanions), nameof(TryKillFinalizer)));
                            _pTryKill = true;
                            harmony.Patch(mKill, prefix: new HarmonyMethod(typeof(UniqueCompanions), nameof(KillPrefix)));
                            _pKill = true;
                        }
                        catch (Exception e) { Stumble("UniqueCompanions.Install(TryKillCompanion)", e); }
                    }
                    parts.Add("przeprowadzki " + (_pTryKill && _pKill ? "PODPIETE" : "BRAK (" + (mTry == null ? "CompanionsCampaignBehavior.TryKillCompanion" : mKill == null ? "KillCharacterAction.ApplyInternal" : "wyjatek") + ")"));
                    // recenzja 14.10: zwolnienie z rozmowy ("I no longer have need of your services") to w grze ApplyByFire i ZARAZ ApplyByRemove (Lost) -
                    // zwolniony unikat nie byl zbiegiem, tylko znikal (po 40-46 dobach wymazany, ROT robil nowego). Ten sam znacznik co TryKillCompanion:
                    // wolny po ApplyByFire (bez rodu, druzyny, niewoli; zbieg) przenosi sie do miasta swojej kultury. BK ma tu prefiks (rycerz z majatkiem
                    // nie do zwolnienia) - finalizer zawsze zdejmuje znacznik, wiec kolejnosc prefiksow nie ma znaczenia.
                    var mFire = AccessTools.Method(typeof(CompanionRolesCampaignBehavior), "companion_fire_on_consequence");
                    if (mFire != null && _pKill)
                    {
                        try
                        {
                            harmony.Patch(mFire, prefix: new HarmonyMethod(typeof(UniqueCompanions), nameof(FirePrefix)),
                                          finalizer: new HarmonyMethod(typeof(UniqueCompanions), nameof(FireFinalizer)));
                            _pFire = true;
                        }
                        catch (Exception e) { Stumble("UniqueCompanions.Install(companion_fire_on_consequence)", e); }
                    }
                    parts.Add("zwolnienia " + (_pFire ? "PODPIETE" : "BRAK (" + (mFire == null ? "CompanionRolesCampaignBehavior.companion_fire_on_consequence" : !_pKill ? "KillCharacterAction.ApplyInternal" : "wyjatek") + ")"));
                    // Jeff 14.10: odejscie po klotni i zabranie przy resecie perkow - ten sam czlowiek przenosi sie do miasta (znacznik _inLeave).
                    var mQuarrel = AccessTools.Method(typeof(CompanionRolesCampaignBehavior), "OnHeroRelationChanged");
                    var mPerk = AccessTools.Method(typeof(PerkResetCampaignBehavior), "RemoveACompanionFromPlayerParty");
                    int nLeave = 0;
                    foreach (var mLeave in new[] { mQuarrel, mPerk })
                    {
                        if (mLeave == null || !_pKill) continue;
                        try
                        {
                            harmony.Patch(mLeave, prefix: new HarmonyMethod(typeof(UniqueCompanions), nameof(LeavePrefix)),
                                          finalizer: new HarmonyMethod(typeof(UniqueCompanions), nameof(LeaveFinalizer)));
                            nLeave++;
                        }
                        catch (Exception e) { Stumble("UniqueCompanions.Install(" + mLeave.Name + ")", e); }
                    }
                    _pLeave = nLeave == 2;
                    parts.Add("odejscia po klotni / reset perkow " + nLeave + "/2" + (_pLeave ? " PODPIETE" : " (BRAK: " + (mQuarrel == null ? "OnHeroRelationChanged " : "") + (mPerk == null ? "RemoveACompanionFromPlayerParty " : "") + (!_pKill ? "KillCharacterAction.ApplyInternal" : "") + ")"));
                }
                else parts.Add("przeprowadzki WYLACZONE (UniqueCompanionsMove = false)");

                if (Config.UniqueCompanionsChildren)
                {
                    var mBirth = AccessTools.Method(typeof(HeroCreator), "DeliverOffSpring", new[] { typeof(Hero), typeof(Hero), typeof(bool) });
                    if (mBirth != null)
                    {
                        try { harmony.Patch(mBirth, postfix: new HarmonyMethod(typeof(UniqueCompanions), nameof(OffspringPostfix))); _pChild = true; }
                        catch (Exception e) { Stumble("UniqueCompanions.Install(DeliverOffSpring)", e); }
                    }
                    parts.Add("dzieci " + (_pChild ? "PODPIETE" : "BRAK (HeroCreator.DeliverOffSpring)"));
                }
                else parts.Add("dzieci WYLACZONE (UniqueCompanionsChildren = false)");
            }
            catch (Exception e) { Stumble("UniqueCompanions.Install", e); parts.Add("wyjatek " + e.GetType().Name); }
            _patchLine = string.Join(", ", parts.ToArray());
            try { Scribe.Line(Tag + ": wpiecie | " + _patchLine + "."); } catch { }
        }

        // ------------------------------------------------------------ (1) zmarly zostaje zmarlym
        /// <summary>Prefiks CampaignObjectManager.UnregisterDeadHero: zmarlego unikatu (zgon inny niz Lost) nie wymazujemy z listy zmarlych.</summary>
        public static bool UnregPrefix(Hero __0)
        {
            try
            {
                if (!Config.UniqueCompanionsStayDead || __0 == null || !__0.IsDead || !IsOriginal(__0)) return true;
                if (__0.DeathMark == KillCharacterAction.KillCharacterActionDetail.Lost) return true;   // usuniecie / przeprowadzka gry - jak dotad
                _dKept++;
                Remember(__0, __0.DeathMark);
                if (__0.StringId != null && _blockSaid.Add(__0.StringId))
                    Scribe.Line(Tag + ": zmarly unikat zostaje martwy - " + NameOf(__0) + " [" + __0.Template.StringId + "], zgon dnia "
                                + SafeDeathDay(__0) + " (" + __0.DeathMark + "); gra chciala go wymazac z listy zmarlych (ROT zrobilby nowego)"
                                + " - blokada (linia raz na sesje na bohatera; gra ponawia co tydzien po 40 dobach).");
                return false;
            }
            catch (Exception e) { Stumble("UniqueCompanions.UnregPrefix", e); return true; }
        }

        private static string SafeDeathDay(Hero h)
        {
            try { return ((int)h.DeathDay.ToDays).ToString(CultureInfo.InvariantCulture); } catch { return "?"; }
        }

        /// <summary>Prefiks ROT ROTSpawnUniqueWanderers.SpawnHero: wzor z listy zmarlych CS nie wraca, gdy jego zmarly zniknal z listy gry.</summary>
        public static bool RotSpawnPrefix(CharacterObject __0)
        {
            try
            {
                if (!Config.UniqueCompanionsStayDead || __0 == null || __0.StringId == null || _deadTemplates.Count == 0 || !_deadTemplates.Contains(__0.StringId)) return true;
                if (AnyHeroWithTemplate(__0.StringId)) return true;   // ROT i tak nic nie zrobi
                _dSpawnBlocked++;
                if (_spawnBlockSaid.Add(__0.StringId))
                {
                    string info;
                    _deadInfo.TryGetValue(__0.StringId, out info);
                    Scribe.Line(Tag + ": ROT chcial odtworzyc zmarlego unikata " + __0.StringId + " (" + (info ?? "?").Replace("|", ", ")
                                + ") - zmarly zniknal z listy gry; nie tworzymy nowego (raz na sesje na wzor).");
                }
                return false;
            }
            catch (Exception e) { Stumble("UniqueCompanions.RotSpawnPrefix", e); return true; }
        }

        private static bool AnyHeroWithTemplate(string id)
        {
            foreach (var h in Hero.AllAliveHeroes) if (h != null && h.Template != null && h.Template.StringId == id) return true;
            foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.Template != null && h.Template.StringId == id) return true;
            return false;
        }

        // ------------------------------------------------------------ (2) przeprowadzka zamiast usuniecia
        public static void TryKillPrefix() { _inTryKill++; }

        public static Exception TryKillFinalizer(Exception __exception)
        {
            if (_inTryKill > 0) _inTryKill--;
            return __exception;
        }

        public static void FirePrefix() { _inFire++; }

        public static Exception FireFinalizer(Exception __exception)
        {
            if (_inFire > 0) _inFire--;
            return __exception;
        }

        public static void LeavePrefix() { _inLeave++; }

        public static Exception LeaveFinalizer(Exception __exception)
        {
            if (_inLeave > 0) _inLeave--;
            return __exception;
        }

        /// <summary>Prefiks KillCharacterAction.ApplyInternal: w TryKillCompanion gry albo przy zwolnieniu towarzysza z rozmowy (Lost) wolny unikat
        /// przenosi sie do innego miasta zamiast usuniecia.</summary>
        public static bool KillPrefix(Hero __0, KillCharacterAction.KillCharacterActionDetail __2)
        {
            if ((_inTryKill <= 0 && _inFire <= 0 && _inLeave <= 0) || __2 != KillCharacterAction.KillCharacterActionDetail.Lost) return true;
            try
            {
                if (!Config.UniqueCompanionsMove || !IsOriginal(__0)) return true;
                bool left = _inLeave > 0 && _inFire <= 0 && _inTryKill <= 0;
                Settlement dest;
                if (left)
                {
                    // odejscie po klotni / reset perkow: gra usuwa towarzysza jeszcze w rodzie gracza - najpierw odchodzi z rodu jak przy zwolnieniu
                    // (z druzyny, zbieg albo ucieczka z niewoli, zdjecie z namiestnika), dopiero wtedy przenosi sie do miasta
                    dest = PickTown(__0);
                    if (dest == null) return true;
                    if (__0.CompanionOf != null) RemoveCompanionAction.ApplyByFire(__0.CompanionOf, __0);
                }
                else dest = null;
                if (!Movable(__0)) return true;   // gra jak dotad (licznik w OnHeroKilled)
                if (dest == null) dest = PickTown(__0);
                if (dest == null) return true;
                bool fired = _inFire > 0;
                string from = Where(__0);
                if (__0.CurrentSettlement != null) LeaveSettlementAction.ApplyForCharacterOnly(__0);
                if (__0.HeroState != Hero.CharacterStates.Active) __0.ChangeState(Hero.CharacterStates.Active);
                EnterSettlementAction.ApplyForCharacterOnly(__0, dest);
                _dMoved++; _tMoved++;
                Scribe.Line(Tag + ": przeprowadzka - " + NameOf(__0) + " [" + __0.Template.StringId + "] " + from + " -> " + dest.Name
                            + (fired ? " (zwolniony towarzysz - gra usunelaby go od razu; " : left ? " (towarzysz odszedl po klotni / zabrany przy resecie perkow - gra usunelaby go od razu; " : " (gra usunelaby wolnego wedrowca; ")
                            + (__0.HasMet ? "poznany" : "niepoznany") + ").");
                return false;
            }
            catch (Exception e) { Stumble("UniqueCompanions.KillPrefix", e); return true; }
        }

        private static bool Movable(Hero h)
        {
            if (h == null || !h.IsAlive || h.Clan != null || h.CompanionOf != null || h.PartyBelongedTo != null || h.PartyBelongedToAsPrisoner != null) return false;
            var st = h.HeroState;
            return st == Hero.CharacterStates.Active || st == Hero.CharacterStates.Fugitive || st == Hero.CharacterStates.Released;
        }

        private static Settlement PickTown(Hero h)
        {
            CultureObject want = h.Template != null ? h.Template.Culture : h.Culture;
            CultureObject others = null;
            try { var ww = NightKingCall.WhiteWalkers(); others = ww != null ? ww.Culture : null; } catch { }
            Settlement here = h.CurrentSettlement, player = null;
            try { player = Hero.MainHero != null ? Hero.MainHero.CurrentSettlement : null; } catch { }
            var same = new List<Settlement>();
            var any = new List<Settlement>();
            foreach (var t in Town.AllTowns)
            {
                var s = t != null ? t.Settlement : null;
                if (s == null || s == here || s == player || s.SiegeEvent != null) continue;
                if (others != null && t.Culture == others) continue;
                any.Add(s);
                if (want != null && t.Culture == want) same.Add(s);
            }
            var pool = same.Count > 0 ? same : any;
            return pool.Count > 0 ? pool[MBRandom.RandomInt(pool.Count)] : null;
        }

        // ------------------------------------------------------------ zdarzenia
        internal static void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (!IsOriginal(victim)) return;
                if (detail == KillCharacterAction.KillCharacterActionDetail.Lost)
                {
                    if (CanonBlocked(victim.Template.StringId))
                    {
                        _dLostOther++;
                        Scribe.Line(Tag + ": unikat usuniety (Lost) - " + NameOf(victim) + " [" + victim.Template.StringId + "] przez Armoury (postacie kanonu m101), rod " + ClanName(victim)
                                    + " - wzor zablokowany, ROT go nie odtworzy.");
                        return;
                    }
                    if (_inTryKill > 0 || _inFire > 0 || _inLeave > 0) _dLostGame++; else _dLostOther++;
                    string by = (_inTryKill > 0 || _inFire > 0 || _inLeave > 0) ? "gre (" + (_inFire > 0 ? "zwolnienie towarzysza" : _inLeave > 0 ? "odejscie po klotni / reset perkow" : "TryKillCompanion") + "; nie do przeniesienia: rod " + ClanName(victim) + ", druzyna " + (victim.PartyBelongedTo != null ? "tak" : "nie")
                                                 + ", jeniec " + (victim.PartyBelongedToAsPrisoner != null ? "tak" : "nie") + ", stan " + victim.HeroState + ")"
                              : _inGuard ? "straznika duplikatow CS"
                              : (Config.UniqueCompanionsMove ? "inny kod (BK, inny mod)" : "gre (TryKillCompanion, UniqueCompanionsMove = false) albo inny kod");
                    Scribe.Line(Tag + ": unikat usuniety (Lost) - " + NameOf(victim) + " [" + victim.Template.StringId + "] przez " + by
                                + " - to nie smierc: po wymazaniu z listy zmarlych ROT moze go odtworzyc.");
                    return;
                }
                Remember(victim, detail);
                Scribe.Line(Tag + ": UNIKAT ZGINAL - " + NameOf(victim) + " [" + victim.Template.StringId + "], dzien " + Day() + " (" + detail + ")"
                                + (killer != null ? ", zabojca " + NameOf(killer) : "") + ", rod " + (victim.Clan != null ? victim.Clan.Name.ToString() : "-")
                                + (Config.UniqueCompanionsStayDead ? " - zostaje martwy (ROT nie zrobi nowego)." : " - UniqueCompanionsStayDead = false: gra wymaze go jak dotad."));
            }
            catch (Exception e) { Stumble("UniqueCompanions.OnHeroKilled", e); }
        }

        internal static void OnHeroCreated(Hero h, bool born)
        {
            try
            {
                if (!IsOriginal(h)) return;
                string id = h.Template.StringId;
                bool wasDead = _deadTemplates.Remove(id);   // jest zywy nastepca - straz ROT nie blokuje juz tego wzoru (gdyby zniknal jako Lost)
                if (!_live) return;
                _dRespawn++; _tRespawn++;
                Scribe.Line(Tag + ": ODRODZENIE - nowy bohater z wzoru " + id + " (" + NameOf(h) + "), dzien " + Day()
                            + (wasDead ? " - wzor byl na liscie zmarlych CS!" : " - poprzednik zniknal (Lost) albo wzor nie mial bohatera") + ".");
            }
            catch (Exception e) { Stumble("UniqueCompanions.OnHeroCreated", e); }
        }

        /// <summary>Start sesji: zmarli unikaty z listy gry dopisani do listy CS (stary zapis), imiona dzieci, linia startu.</summary>
        internal static void OnSession()
        {
            int fromSave = Dead != null ? Dead.Count : 0, added = 0, renamed = 0, superseded = 0;
            _live = true;
            try
            {
                foreach (var h in Hero.DeadOrDisabledHeroes)
                    if (h != null && h.IsDead && IsOriginal(h) && h.DeathMark != KillCharacterAction.KillCharacterActionDetail.Lost && Remember(h, h.DeathMark)) added++;
                // zmarly z listy CS, a wzor ma juz zywego nastepce (odtworzony przed paczka albo przy kluczu false) - straz ROT nie blokuje tego wzoru
                foreach (var h in Hero.AllAliveHeroes)
                    if (h != null && IsOriginal(h) && _deadTemplates.Remove(h.Template.StringId)) superseded++;
                if (Config.UniqueCompanionsChildren)
                    foreach (var h in Hero.AllAliveHeroes)
                        if (h != null && IsRotTemplate(h.Template) && (h.Father != null || h.Mother != null) && FixName(h, "przy starcie sesji")) renamed++;
            }
            catch (Exception e) { Stumble("UniqueCompanions.OnSession", e); }
            try
            {
                Scribe.Line(Tag + ": start sesji | klucze: StayDead " + YN(Config.UniqueCompanionsStayDead) + ", Move " + YN(Config.UniqueCompanionsMove)
                            + ", DuplicateGuard " + YN(Config.UniqueCompanionsDuplicateGuard) + ", Children " + YN(Config.UniqueCompanionsChildren)
                            + ", Log " + YN(Config.UniqueCompanionsLog) + " | latki: " + _patchLine + " | wzorow " + Templates().Count
                            + " | zmarli na liscie CS " + (Dead != null ? Dead.Count : 0) + " (z zapisu " + fromSave + ", dopisani z listy gry " + added
                            + "; wzorow pod straza ROT " + _deadTemplates.Count + ", z zywym nastepca " + superseded + ")"
                            + " | imiona dzieci poprawione przy starcie " + renamed + ".");
            }
            catch { }
        }

        private static string YN(bool b) { return b ? "tak" : "nie"; }

        private static List<CharacterObject> Templates()
        {
            if (_templates != null) return _templates;
            var list = new List<CharacterObject>();
            try
            {
                foreach (var c in CharacterObject.All)
                    if (c != null && c.Occupation == Occupation.Wanderer && IsRotTemplate(c)) list.Add(c);   // jak ROT SpawnHeroes
                list.Sort((a, b) => Num(a.StringId).CompareTo(Num(b.StringId)));
            }
            catch (Exception e) { Stumble("UniqueCompanions.Templates", e); }
            _templates = list;
            return list;
        }

        private static int Num(string id)
        {
            int n;
            return id != null && id.Length > WandererPrefix.Length && int.TryParse(id.Substring(WandererPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : int.MaxValue;
        }

        // ------------------------------------------------------------ (4) dziecko unikatu
        /// <summary>Postfiks HeroCreator.DeliverOffSpring: zwykle imie i bez stroju wzoru rodzica.</summary>
        public static void OffspringPostfix(Hero __result)
        {
            try
            {
                var c = __result;
                if (!Config.UniqueCompanionsChildren || c == null || !IsRotTemplate(c.Template) || (c.Father == null && c.Mother == null)) return;
                FixName(c, "przy narodzinach");
                int n = StripTemplateGear(c);
                if (n > 0)
                {
                    _dChildGear++;
                    Scribe.Line(Tag + ": dziecko unikatu " + NameOf(c) + " [" + c.Template.StringId + "] - " + n + " slotow z rzeczami wzoru rodzica dostalo zestaw dziecka kultury "
                                + (c.Culture != null ? c.Culture.StringId : "?") + ".");
                }
            }
            catch (Exception e) { Stumble("UniqueCompanions.OffspringPostfix", e); }
        }

        /// <summary>Pelne imie = nazwa wzoru: imie jak lord tej kultury (NameGenerator.GenerateHeroFullName poza galezia wedrowca).</summary>
        private static bool FixName(Hero c, string when)
        {
            var t = c.Template;
            if (t == null || t.Name == null || c.Name == null || c.FirstName == null) return false;
            string tn = t.Name.ToString(), old = c.Name.ToString();
            if (old != tn) return false;   // imie juz inne (takze poprawione przez BK - rodzic pasowany)
            TextObject first = c.FirstName;
            TextObject full = first;
            var clan = c.Clan;
            if (clan != null && (clan.IsMafia || clan.IsNomad || clan.IsUnderMercenaryService || clan.IsSect))
            {
                full = new TextObject("{=4z1t75be}{FIRSTNAME} of the {CLAN_NAME}");
                full.SetTextVariable("CLAN_NAME", clan.InformalName);
            }
            string cid = c.Culture != null ? c.Culture.StringId : "";
            full.SetTextVariable("FEMALE", c.IsFemale ? 1 : 0);
            full.SetTextVariable("IMPERIAL", cid == "empire" ? 1 : 0);
            full.SetTextVariable("COASTAL", (cid == "empire" || cid == "nord" || cid == "vlandia") ? 1 : 0);
            full.SetTextVariable("NORTHERN", (cid == "battania" || cid == "nord" || cid == "sturgia") ? 1 : 0);
            if (full != first) full.SetTextVariable("FIRSTNAME", first);
            else full.SetTextVariable("FIRSTNAME", first.ToString());
            c.SetName(full, first);
            _dChildName++;
            Hero parent = c.IsFemale ? (c.Mother ?? c.Father) : (c.Father ?? c.Mother);
            Scribe.Line(Tag + ": dziecko unikatu " + when + " - imie \"" + old + "\" (nazwa wzoru " + t.StringId + ") -> \"" + NameOf(c) + "\""
                        + ", rodzic " + NameOf(parent) + ", zawod " + c.Occupation + ", rod " + (clan != null ? clan.Name.ToString() : "-") + ".");
            return true;
        }

        private static int StripTemplateGear(Hero c)
        {
            var t = c.Template;
            if (t == null) return 0;
            var ids = new HashSet<ItemObject>();
            AddItems(ids, t.BattleEquipments);
            AddItems(ids, t.CivilianEquipments);
            AddItems(ids, t.StealthEquipments);
            if (ids.Count == 0) return 0;
            int n = 0;
            Equipment kid = null;
            bool kidTried = false;
            var camp = Campaign.Current;
            foreach (var set in new[] { c.BattleEquipment, c.CivilianEquipment })
            {
                if (set == null || camp == null || ReferenceEquals(set, camp.DeadBattleEquipment) || ReferenceEquals(set, camp.DeadCivilianEquipment)) continue;
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var it = set[(EquipmentIndex)i].Item;
                    if (it == null || !ids.Contains(it)) continue;
                    if (!kidTried) { kidTried = true; kid = KidSet(c); }
                    var repl = kid != null ? kid[(EquipmentIndex)i] : EquipmentElement.Invalid;
                    if (repl.Item == it) continue;   // ta sama rzecz jest w zestawie dziecka kultury (zwykla rzecz, nie stroj rodzica) - zostaje
                    set[(EquipmentIndex)i] = repl;
                    n++;
                }
            }
            var stealth = c.StealthEquipment;
            if (stealth != null && camp != null && !ReferenceEquals(stealth, camp.DefaultStealthEquipment))
            {
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var it = stealth[(EquipmentIndex)i].Item;
                    if (it == null || !ids.Contains(it)) continue;
                    stealth[(EquipmentIndex)i] = EquipmentElement.Invalid;
                    n++;
                }
            }
            return n;
        }

        private static void AddItems(HashSet<ItemObject> ids, IEnumerable<Equipment> sets)
        {
            if (sets == null) return;
            foreach (var eq in sets)
            {
                if (eq == null) continue;
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var it = eq[(EquipmentIndex)i].Item;
                    if (it != null) ids.Add(it);
                }
            }
        }

        /// <summary>Zestaw dziecka kultury jak gra (EquipmentSelectionModel.GetEquipmentForDeliveredOffspring); brak - cywilny neutral_culture (jak Hero.SetCharacterObject).</summary>
        private static Equipment KidSet(Hero c)
        {
            try
            {
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.EquipmentSelectionModel : null;
                var eq = m != null ? m.GetEquipmentForDeliveredOffspring(c) : null;
                if (eq != null) return eq;
            }
            catch { }
            try
            {
                var nc = Game.Current != null ? Game.Current.ObjectManager.GetObject<CultureObject>("neutral_culture") : null;
                var r = nc != null ? nc.DefaultCivilianEquipmentRoster : null;
                if (r != null) foreach (var eq in r.AllEquipments) if (eq != null && !eq.IsEmpty()) return eq;
            }
            catch { }
            return null;
        }

        // ------------------------------------------------------------ doba: (3) straznik duplikatow, (5) linia
        internal static void Daily()
        {
            try
            {
                if (Config.UniqueCompanionsDuplicateGuard || Config.UniqueCompanionsLog)
                {
                    var alive = new Dictionary<string, List<Hero>>(StringComparer.Ordinal);
                    var dead = new Dictionary<string, List<Hero>>(StringComparer.Ordinal);
                    var kids = new List<Hero>();
                    Collect(alive, dead, kids);
                    if (Config.UniqueCompanionsDuplicateGuard && Guard(alive))
                    {
                        alive.Clear(); dead.Clear(); kids.Clear();
                        Collect(alive, dead, kids);
                    }
                    if (Config.UniqueCompanionsLog) DayLine(alive, dead, kids);
                }
            }
            catch (Exception e) { Stumble("UniqueCompanions.Daily", e); }
            ClearDay();
        }

        private static void Collect(Dictionary<string, List<Hero>> alive, Dictionary<string, List<Hero>> dead, List<Hero> kids)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var h in pass == 0 ? Hero.AllAliveHeroes : Hero.DeadOrDisabledHeroes)
                {
                    if (h == null || !IsRotTemplate(h.Template)) continue;
                    if (h.Father != null || h.Mother != null) { if (h.IsAlive) kids.Add(h); continue; }
                    var map = h.IsAlive ? alive : dead;
                    List<Hero> l;
                    if (!map.TryGetValue(h.Template.StringId, out l)) { l = new List<Hero>(); map[h.Template.StringId] = l; }
                    l.Add(h);
                }
            }
        }

        private static int KeepScore(Hero h)
        {
            if (h.Clan == Clan.PlayerClan || h.CompanionOf == Clan.PlayerClan) return 5;
            if (h.Clan != null || h.CompanionOf != null) return 4;
            if (h.PartyBelongedTo != null || h.PartyBelongedToAsPrisoner != null) return 3;
            if (h.HasMet) return 2;
            return 1;
        }

        private static bool Removable(Hero h)
        {
            if (h == null || !h.IsAlive || h.IsDisabled || h.Clan != null || h.CompanionOf != null || h.PartyBelongedTo != null || h.PartyBelongedToAsPrisoner != null) return false;
            try { if (h.CurrentSettlement != null && Hero.MainHero != null && h.CurrentSettlement == Hero.MainHero.CurrentSettlement) return false; } catch { return false; }
            return true;
        }

        /// <summary>(3) Dwa zywe unikaty z jednym wzorem: zostaje jeden, wolny drugi usuniety jak gra (Lost). Zwraca true, gdy kogos usunieto.</summary>
        private static bool Guard(Dictionary<string, List<Hero>> alive)
        {
            bool removed = false;
            foreach (var kv in alive)
            {
                var l = kv.Value;
                if (l.Count < 2) continue;
                l.Sort((a, b) =>
                {
                    int s = KeepScore(b).CompareTo(KeepScore(a));
                    return s != 0 ? s : a.Id.InternalValue.CompareTo(b.Id.InternalValue);   // ta sama ocena: wczesniej stworzony zostaje
                });
                var keep = l[0];
                for (int i = 1; i < l.Count; i++)
                {
                    var d = l[i];
                    if (Removable(d))
                    {
                        string where = Where(d);
                        _inGuard = true;
                        try { KillCharacterAction.ApplyByRemove(d); }
                        finally { _inGuard = false; }
                        _dDupRemoved++; removed = true;
                        Scribe.Line(Tag + ": DUPLIKAT wzoru " + kv.Key + " - zostaje " + NameOf(keep) + " (" + Where(keep) + ", rod " + ClanName(keep)
                                    + "), usuniety wolny " + NameOf(d) + " (" + where + ").");
                    }
                    else
                    {
                        _dDupLeft++;
                        Scribe.Line(Tag + ": DUPLIKAT wzoru " + kv.Key + " - " + NameOf(keep) + " (" + Where(keep) + ", rod " + ClanName(keep) + ") i "
                                    + NameOf(d) + " (" + Where(d) + ", rod " + ClanName(d) + ") - drugi nie jest wolny, zostaje (tylko log).");
                    }
                }
            }
            return removed;
        }

        private static string ClanName(Hero h)
        {
            try { return h != null && h.Clan != null ? h.Clan.Name.ToString() : "-"; } catch { return "?"; }
        }

        private static string Where(Hero h)
        {
            try
            {
                if (h.PartyBelongedToAsPrisoner != null) return "jeniec " + h.PartyBelongedToAsPrisoner.Name;
                if (h.PartyBelongedTo != null) return "druzyna " + h.PartyBelongedTo.Name + (h.PartyBelongedTo.CurrentSettlement != null ? " w " + h.PartyBelongedTo.CurrentSettlement.Name : "");
                if (h.CurrentSettlement != null) return h.CurrentSettlement.Name.ToString();
                return "bez miejsca (" + h.HeroState + ")";
            }
            catch { return "?"; }
        }

        private static void DayLine(Dictionary<string, List<Hero>> alive, Dictionary<string, List<Hero>> dead, List<Hero> kids)
        {
            var templates = Templates();
            int nAlive = 0, inTown = 0, nowhere = 0, comp = 0, inClan = 0, pris = 0, nDead = 0, nLost = 0, none = 0, dups = 0, canon = 0;
            var sb = new StringBuilder();
            foreach (var t in templates)
            {
                string id = t.StringId;
                List<Hero> al, de;
                alive.TryGetValue(id, out al);
                dead.TryGetValue(id, out de);
                if (sb.Length > 0) sb.Append("; ");
                if (al != null && al.Count > 0)
                {
                    nAlive++;
                    if (al.Count > 1) dups++;
                    for (int i = 0; i < al.Count; i++)
                    {
                        var h = al[i];
                        if (i > 0) sb.Append(" + ");
                        if (h.CompanionOf != null) comp++;
                        else if (h.Clan != null) inClan++;
                        else if (h.PartyBelongedToAsPrisoner != null) pris++;
                        else if (h.CurrentSettlement != null && h.PartyBelongedTo == null) inTown++;
                        else nowhere++;
                        sb.Append(NameOf(h)).Append(' ').Append(Short(id)).Append(": zywy, ").Append(Where(h)).Append(", rod ").Append(ClanName(h))
                          .Append(", ").Append(h.Occupation);
                        if (h.CompanionOf != null) sb.Append(", towarzysz ").Append(h.CompanionOf.Name);
                    }
                    if (al.Count > 1) sb.Append(" (DUPLIKAT x").Append(al.Count).Append(')');
                    continue;
                }
                Hero last = null;
                if (de != null) foreach (var h in de) if (last == null || h.DeathDay.ToDays > last.DeathDay.ToDays) last = h;
                if (last != null && last.DeathMark != KillCharacterAction.KillCharacterActionDetail.Lost)
                {
                    nDead++;
                    sb.Append(NameOf(last)).Append(' ').Append(Short(id)).Append(": martwy od dnia ").Append(SafeDeathDay(last)).Append(" (").Append(last.DeathMark)
                      .Append("), rod ").Append(ClanName(last)).Append(", ").Append(last.Occupation);
                }
                else if (last != null && CanonBlocked(id))
                {
                    canon++;
                    sb.Append(NameOf(last)).Append(' ').Append(Short(id)).Append(": usuniety z kanonu dnia ").Append(SafeDeathDay(last)).Append(" (Lost - Armoury m101, ROT nie odtworzy)");
                }
                else if (last != null)
                {
                    nLost++;
                    sb.Append(NameOf(last)).Append(' ').Append(Short(id)).Append(": zniknal dnia ").Append(SafeDeathDay(last))
                      .Append(" (Lost - czeka na wymazanie, potem ROT go odtworzy)");
                }
                else if (_deadTemplates.Contains(id))
                {
                    nDead++;
                    string info;
                    _deadInfo.TryGetValue(id, out info);
                    var p = (info ?? "?|?|?").Split('|');
                    sb.Append(p.Length > 2 ? p[2] : "?").Append(' ').Append(Short(id)).Append(": martwy od dnia ").Append(p[0]).Append(" (").Append(p.Length > 1 ? p[1] : "?")
                      .Append("), wymazany z listy gry - straz CS");
                }
                else if (CanonBlocked(id))
                {
                    canon++;
                    sb.Append(t.Name).Append(' ').Append(Short(id)).Append(": usuniety z kanonu (Armoury m101 - nie powstaje)");
                }
                else
                {
                    none++;
                    sb.Append(t.Name).Append(' ').Append(Short(id)).Append(": BRAK (nikt z tym wzorem)");
                }
            }
            int kidsNamed = 0;
            var kb = new StringBuilder();
            foreach (var k in kids)
            {
                bool same = false;
                try { same = k.Template.Name != null && k.Name != null && k.Name.ToString() == k.Template.Name.ToString(); } catch { }
                if (same) kidsNamed++;
                if (kb.Length > 0) kb.Append("; ");
                Hero parent = k.IsFemale ? (k.Mother ?? k.Father) : (k.Father ?? k.Mother);
                kb.Append(NameOf(k)).Append(' ').Append(Short(k.Template.StringId)).Append(" (").Append(k.Father != null ? "ojciec " : "matka ").Append(NameOf(k.Father ?? k.Mother))
                  .Append(", ").Append(k.Occupation).Append(", rod ").Append(ClanName(k)).Append(", ").Append(Where(k)).Append(same ? ", IMIE WZORU" : "").Append(')');
            }
            Scribe.Line(Tag + ": dzien " + Day() + " | wzorow " + templates.Count + ", zywych " + nAlive + " (wolnych w miastach " + inTown + ", bez miejsca " + nowhere
                        + ", towarzyszy " + comp + ", w rodach " + inClan + ", jencow " + pris + "), martwych " + nDead + ", znikniete " + nLost + ", brak " + none
                        + ", usuniete z kanonu " + canon
                        + " | duplikaty " + dups + " (usuniete dzis " + _dDupRemoved + ", nie do usuniecia " + _dDupLeft + ")"
                        + " | odrodzenia dzis " + _dRespawn + " (od wczytania " + _tRespawn + ")"
                        + " | przeprowadzki dzis " + _dMoved + " (od wczytania " + _tMoved + "), usuniete przez gre dzis " + _dLostGame + ", inne usuniecia dzis " + _dLostOther
                        + " | zmarli zostaja: na liscie CS " + (Dead != null ? Dead.Count : 0) + ", blokad wymazania dzis " + _dKept + ", blokad ROT dzis " + _dSpawnBlocked
                        + " | dzieci unikatow " + kids.Count + ", z imieniem wzoru " + kidsNamed + " (dzis imie " + _dChildName + ", stroj " + _dChildGear + ")"
                        + " | klucze SD/MV/DG/CH " + YN(Config.UniqueCompanionsStayDead) + "/" + YN(Config.UniqueCompanionsMove) + "/" + YN(Config.UniqueCompanionsDuplicateGuard)
                        + "/" + YN(Config.UniqueCompanionsChildren)
                        + (_stumblesDay > 0 ? " | POTKNIECIA " + _stumblesDay + " (razem " + _stumbles + ")" : "")
                        + " | " + sb + (kb.Length > 0 ? " | dzieci: " + kb : "") + ".");
        }
    }

    /// <summary>Unikaty ROT: lista zmarlych w zapisie, zdarzenia smierci i stworzenia, start sesji, doba (straznik i linia).</summary>
    internal sealed class UniqueCompanionsBehavior : CampaignBehaviorBase
    {
        public UniqueCompanionsBehavior() { UniqueCompanions.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { UniqueCompanions.OnSession(); });
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, UniqueCompanions.OnHeroKilled);
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, UniqueCompanions.OnHeroCreated);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, UniqueCompanions.Daily);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("cs_uniq_dead", ref UniqueCompanions.Dead);
                if (dataStore.IsLoading) UniqueCompanions.Rebuild();
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "UniqueCompanionsBehavior.SyncData", null); } catch { } }
        }
    }
}
