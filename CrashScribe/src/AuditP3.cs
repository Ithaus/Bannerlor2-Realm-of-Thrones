using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>P3 audytu modow 14.10 (docs/audyt-modow-2026-10-14/RAPORT-AUDYT-MODOW.md rozdz. 3 i 5) - naprawy cudzych modow w CrashScribe,
    /// kazda pod wlasnym kluczem (Config), wylaczona = zachowanie b47fecc3. Pliki BK / BKROTPatch / ROT nietkniete.
    /// AM-29 (CultureTemplateRepair): ROT-Content wpisuje do list wzorcow kultur (lord_templates, notable_templates, rebellion_hero_templates)
    /// odwolania do postaci, ktorych nie ma (RGL 'Null object reference found': spc_brotherhood_2, spc_notable_reach_6, spc_yiti_headman_1,
    /// yiti_clansman ...). Gra dodaje odwolanie bez sprawdzenia (CultureObject.Deserialize), a MBObjectManager.UnregisterNonReadyObjects zostawia
    /// w liscie obiekt zastepczy (IsReady false, bez BodyPropertyRange). Rod towarzysza (CompanionRolesCampaignBehavior.SpawnNewHeroesForNewCompanionClan)
    /// losuje wzorzec lorda 2 razy - w Westerlands (vlandia, 1 z 12 zepsuty) ok. 16% szansy na pewny NullReference w
    /// DefaultHeroCreationModel.GetStaticBodyProperties w srodku zakladania rodu. Przy KAZDYM starcie sesji (listy nie sa w zapisie, gra buduje je
    /// z XML przy kazdym wczytaniu) z trzech list kazdej kultury wylatuja wpisy null / nie gotowe / bez BodyPropertyRange (nowa lista przez
    /// prywatny setter; lista, z ktorej nic by nie zostalo - bez zmian + linia). Siatka: prefiks na CreateNewHeroForNewCompanionClan podmienia
    /// zepsuty wzorzec na gotowy wzorzec lorda tej samej kultury (gdy filtr nie zadzialal).
    /// AM-01 (StopBkrotProsperityReset): 3 prefiksy BKROT EarlyHeal zdjete (dobrobyt twierdzy &lt; 100 -> 2000 / 4000 z niczego), straz NaN / Inf
    /// na Town.set_Prosperity - opis przy ProsperityInstall.
    /// AM-28 (LorathFaithFix): BKROT zna stara kulture 'lorathi' (w ROT 8.1.8 Lorath = 'nord') - postfiks IsCultureEssosi, kultura ulubiona wiary Pattern,
    /// jednorazowe nadanie wiary jak BK - opis przy LorathInstall.
    /// AM-20 (NotablesKeepSettlementCulture, droga B - decyzja Jeffa 14.10): BK nie nawraca notabli na kulture pana - opis przy TwentyInstall.</summary>
    internal static class AuditP3
    {
        /// <summary>Start procesu (OnBeforeInitialModuleScreenSetAsRoot, po Mends.Install i BkTitles.Install). Nie rzuca.</summary>
        internal static void Install(Harmony harmony)
        {
            TwentyInstall(harmony);
            TemplatesInstall(harmony);
            ProsperityInstall(harmony);
            LorathInstall(harmony);
        }

        /// <summary>OnSessionLaunched (nowa gra i kazde wczytanie). Nie rzuca.</summary>
        internal static void OnSession()
        {
            TemplatesRepair();
            ProsperitySession();
            _lorathSessionDone = false;
            _relMgr = null; _popMgr = null;   // AM-28: menedzery BK tej kampanii pobrane od nowa w pierwszej godzinie sesji
        }

        /// <summary>Pierwsza godzina sesji (HourlyTick) - po inicjalizacji wiar BK (OnGameLoaded / nowa gra). Nie rzuca.</summary>
        internal static void OnHourly()
        {
            if (!_lorathSessionDone) { _lorathSessionDone = true; LorathSession(); }
        }

        /// <summary>Koniec doby (DailyTick). Nie rzuca.</summary>
        internal static void OnDaily()
        {
            ProsperityDaily();
            LorathDaily();
        }

        // ------------------------------------------------------------------ AM-20: notable zawsze w kulturze osady (droga B, decyzja Jeffa 14.10)
        // Jedyne miejsce, ktore daje notablowi kulture pana: BK BKNotableBehavior.ApplyNotableCultureConversion(notable, converter, councilConversion)
        // - notable.Culture = converter.Culture, converter.Clan.Influence -= CultureModel.GetConversionCost, bez councilConversion relacja -8 i (dla
        // gracza) komunikat. Wolaja ja: HandleCultureConversions (doba osady, rod AI: pierwszy notabl z kultura inna niz pan, 5%, wplyw >= 1.5 x koszt),
        // BKClanBehavior.RunCouncilTasks (zadanie rady PromoteCulture, 2% x kompetencja, takze rod gracza - potem komunikat zarzadcy "has converted")
        // i rozmowa gracza z wlasnym notablem (CultureConversionAcceptedConsequence). Nasz Mends.LocalLevies nazajutrz cofal kazde nawrocenie
        // (bieg dwuletni 06:01: 675 Artisan + 24 Merchant) - pan placil wplywem za nic. Klucz NotablesKeepSettlementCulture: (1) prefiks na
        // ApplyNotableCultureConversion zwraca false - cala akcja (kultura, wplyw, relacja) nie dzieje sie, ktokolwiek ja wola; (2) postfiks
        // CultureConversionOnClickable - opcja rozmowy wyszarzona z opisem 'Notables keep the culture of their settlement.'; (3) postfiks
        // CourtManager.HasCurrentTask(CouncilData, CouncilTask, out float) - dla zadania 'PromoteCulture' false (PromoteCulture czyta tylko
        // RunCouncilTasks, wiec zadanie nic nie losuje i zarzadca nie oglasza nawrocenia, ktorego nie bylo). LocalLevies dalej prostuje wszystkich
        // notabli do kultury osady (b47fecc3); kultura calej osady zmienia sie przez asymilacje BK (Settlement.Culture = DominantCulture), notable
        // ida za nia. Linia doby: ogon linii "werbunek miejscowy" (TwentyDayTail). Klucz off = zadnej latki (b47fecc3).
        private static bool _twOn, _twClick, _twTask;
        private static int _twBlockedDay, _twBlockedPlayerDay, _twCouncilDay, _twBlockedAll;

        private static void TwentyInstall(Harmony harmony)
        {
            try
            {
                if (!Config.NotablesKeepSettlementCulture)
                {
                    Scribe.Line("Mends: kultura notabli (AM-20) wylaczone (NotablesKeepSettlementCulture=false) - BK nawraca notabli jak dotad; LocalLevies tylko kaplani "
                                + (Config.LocalLeviesPreachersOnly ? "TAK (droga A)" : "NIE (wszyscy notable, b47fecc3)") + ".");
                    return;
                }
                var tNb = AccessTools.TypeByName("BannerKings.Behaviours.BKNotableBehavior");
                var mApply = tNb != null ? AccessTools.Method(tNb, "ApplyNotableCultureConversion") : null;
                var mClick = tNb != null ? AccessTools.Method(tNb, "CultureConversionOnClickable") : null;
                var tCm = AccessTools.TypeByName("BannerKings.Managers.CourtManager");
                var tCd = AccessTools.TypeByName("BannerKings.Managers.Court.CouncilData");
                MethodInfo mTask = null;
                if (tCm != null && tCd != null)
                    foreach (var m in tCm.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name != "HasCurrentTask" || m.ReturnType != typeof(bool)) continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 3 && ps[0].ParameterType == tCd) { mTask = m; break; }
                    }
                var pa = mApply != null ? mApply.GetParameters() : null;
                if (pa != null && pa.Length >= 2 && pa[0].ParameterType == typeof(Hero) && pa[1].ParameterType == typeof(Hero))
                {
                    harmony.Patch(mApply, prefix: new HarmonyMethod(typeof(AuditP3), nameof(TwentyApplyPrefix)));
                    _twOn = true;
                }
                // rozmowa i rada - kazda latka osobno (blad jednej nie zdejmuje drugiej ani linii startu; prefiks i tak blokuje koszt)
                if (_twOn && mClick != null && mClick.ReturnType == typeof(bool) && mClick.GetParameters().Length == 1)
                    try { harmony.Patch(mClick, postfix: new HarmonyMethod(typeof(AuditP3), nameof(TwentyClickPostfix))); _twClick = true; }
                    catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.TwentyInstall(rozmowa)", null); } catch { } }
                if (_twOn && mTask != null)
                    try { harmony.Patch(mTask, postfix: new HarmonyMethod(typeof(AuditP3), nameof(TwentyTaskPostfix))); _twTask = true; }
                    catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.TwentyInstall(rada)", null); } catch { } }
                int n = (_twOn ? 1 : 0) + (_twClick ? 1 : 0) + (_twTask ? 1 : 0);
                Scribe.Line("Mends: kultura notabli (AM-20) - notable w kulturze osady (droga B) wpiete " + n + "/3: BK ApplyNotableCultureConversion "
                            + (_twOn ? "zablokowane (bez wplywu i relacji)" : "NIE wpiete (" + (tNb == null ? "brak BannerKings" : "brak metody albo inne parametry") + ")")
                            + ", rozmowa 'convert to my culture' " + (_twClick ? "wyszarzona" : "NIE") + ", zadanie rady PromoteCulture " + (_twTask ? "wylaczone" : "NIE")
                            + "; LocalLevies prostuje wszystkich notabli" + (Config.LocalLeviesPreachersOnly ? " (LocalLeviesPreachersOnly pominiete - nigdy obie drogi naraz)" : "") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.TwentyInstall", null); } catch { } }
        }

        /// <summary>Prefiks AM-20 na BK ApplyNotableCultureConversion: cala akcja nie dzieje sie (kultura, wplyw, relacja). Gracz (gdy wyszarzenie
        /// rozmowy albo wylaczenie zadania rady nie weszlo) dostaje komunikat.</summary>
        public static bool TwentyApplyPrefix(Hero __0, Hero __1)
        {
            if (!_twOn) return true;
            _twBlockedDay++; _twBlockedAll++;
            if (__1 != null && __1 == Hero.MainHero)
            {
                _twBlockedPlayerDay++;
                try
                {
                    string who = __0 != null && __0.Name != null ? __0.Name.ToString() : "The notable";
                    InformationManager.DisplayMessage(new InformationMessage("Notables keep the culture of their settlement: " + who + " did not change culture and no influence was spent."));
                }
                catch { }
            }
            return false;
        }

        /// <summary>Postfiks AM-20 na BK CultureConversionOnClickable (opcja rozmowy z wlasnym notablem): wyszarzona z opisem.</summary>
        public static void TwentyClickPostfix(ref bool __result, ref TextObject hintText)
        {
            try
            {
                if (!_twOn) return;
                __result = false;
                hintText = new TextObject("{=!}Notables keep the culture of their settlement.");
            }
            catch { }
        }

        /// <summary>Postfiks AM-20 na BK CourtManager.HasCurrentTask(CouncilData, CouncilTask, out float): zadanie 'PromoteCulture' nieczynne.
        /// Tanio: tylko gdy gra odpowiedziala true (inne zadania - jedno porownanie napisu).</summary>
        public static void TwentyTaskPostfix(object __1, ref bool __result)
        {
            if (!__result || !_twOn) return;
            try
            {
                var task = __1 as MBObjectBase;
                if (task != null && task.StringId == "PromoteCulture") { __result = false; _twCouncilDay++; }
            }
            catch { }
        }

        /// <summary>AM-20: ogon linii doby "werbunek miejscowy" (Mends.LocalLevies) - nawrocenia BK zablokowane od poprzedniej takiej linii; liczniki od nowa.</summary>
        internal static string TwentyDayTail()
        {
            try
            {
                string s = " | AM-20 kultura osady " + (_twOn ? "TAK" : (Config.NotablesKeepSettlementCulture ? "NIE WPIETE" : "NIE"))
                           + ": zablokowane nawrocenia BK " + _twBlockedDay + " (gracz " + _twBlockedPlayerDay + "), zadanie rady PromoteCulture pominiete " + _twCouncilDay
                           + ", w procesie " + _twBlockedAll;
                _twBlockedDay = 0; _twBlockedPlayerDay = 0; _twCouncilDay = 0;
                return s;
            }
            catch { return " | AM-20 kultura osady ?"; }
        }

        // ------------------------------------------------------------------ AM-28: wiara Lorath (BKROT zna kulture 'lorathi', w ROT 8.1.8 Lorath to 'nord')
        // ROT-Content spcultures.xml: id="nord" name="Lorathi"; kultury 'lorathi' nie ma (RGL 'Null object reference found with ID: lorathi').
        // BKROTPatch.Helpers.CultureHelpers.IsCultureEssosi zna 'lorathi', nie 'nord' - wiary Essos (Pattern, R'hllor, Boash, Black Goat, Many-Faced God,
        // Josimakazma: IsCultureNaturalFaith -> IsCultureEssosi) nie uznaja Lorath za swoja, wiec BK (ReligionsManager.GetIdealReligion / InitializeFaithfulHeroes)
        // nie daje wiary bohaterom nord, a osady Lorath (wiara osady z wiary pana i notabli - ReligionData) zostaja bez wiary i kaplanow. ROTReligions.Initialize:
        // Pattern.Initialize(..., { GetObject("lorathi") }) = { null } - wiara Lorath bez kultury ulubionej (FavoredCultures, MainCulture).
        // Klucz LorathFaithFix: postfiks IsCultureEssosi (nord -> true, jak BKROT chcial dla 'lorathi'); w pierwszej godzinie sesji null w FavoredCultures
        // wiary 'pattern' -> nord i - tylko gdy zaden bohater nord nie ma jeszcze wiary (kampania sprzed naprawy) - jednorazowe nadanie wiary tak jak BK
        // przy starcie gry (InitializeFaithfulHeroes: kazda wiara po kolei, bohater bez wiary, Faith.IsHeroNaturalFaith - losowanie BKROT, InitializeHeroFaith).
        // Wzorce wedrowcow BK craftsman_nord / noble_nord NIE (wymagalyby nowego pliku danych w folderze gry albo rejestracji postaci w kodzie przed
        // wczytaniem zapisu - partie wedrowcow w zapisie trzymaja id postaci).
        private static bool _lorathOn, _lorathSessionDone;
        private static int _lorathHits;

        private static void LorathInstall(Harmony harmony)
        {
            try
            {
                if (!Config.LorathFaithFix)
                {
                    Scribe.Line("Mends: wiara Lorath (AM-28) wylaczone (LorathFaithFix=false) - BKROT nie uznaje kultury nord (Lorath) za Essos, Lorath bez wiary BK.");
                    return;
                }
                var t = FindType("BKROTPatch.Helpers.CultureHelpers", "BKROTPatch");
                var m = t != null ? AccessTools.Method(t, "IsCultureEssosi", new[] { typeof(CultureObject) }) : null;
                if (m != null && m.ReturnType == typeof(bool))
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(AuditP3), nameof(LorathEssosi)));
                    _lorathOn = true;
                }
                Scribe.Line("Mends: wiara Lorath (AM-28) - postfiks BKROT CultureHelpers.IsCultureEssosi (nord -> Essos) "
                            + (_lorathOn ? "wpiety" : "NIE wpiety (" + (t == null ? "brak BKROTPatch" : "brak metody") + ")") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.LorathInstall", null); } catch { } }
        }

        /// <summary>Postfiks AM-28: kultura nord (Lorath w ROT 8.1.8) jest kultura Essos dla BKROT (jak 'lorathi' z ROT 6.x).</summary>
        public static void LorathEssosi(CultureObject __0, ref bool __result)
        {
            try
            {
                if (__result || __0 == null || __0.StringId != "nord") return;
                __result = true;
                _lorathHits++;
            }
            catch { }
        }

        private static object _relMgr;
        private static MethodInfo _mGetHeroRel, _mInitHeroFaith, _mRefresh, _mGetPop;

        /// <summary>ReligionsManager BK (odbicie - CrashScribe nie wiaze sie z BannerKings.dll) i jego slownik wiar (klucze = Religion).</summary>
        private static System.Collections.IDictionary BkReligions()
        {
            _relMgr = null;
            var tCfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
            var pInst = tCfg != null ? AccessTools.Property(tCfg, "Instance") : null;
            object cfg = pInst != null ? pInst.GetValue(null, null) : null;
            if (cfg == null) return null;
            var pMgr = AccessTools.Property(cfg.GetType(), "ReligionsManager");
            _relMgr = pMgr != null ? pMgr.GetValue(cfg, null) : null;
            if (_relMgr == null) return null;
            var tm = _relMgr.GetType();
            _mGetHeroRel = AccessTools.Method(tm, "GetHeroReligion", new[] { typeof(Hero) });
            _mInitHeroFaith = AccessTools.Method(tm, "InitializeHeroFaith");
            _mRefresh = AccessTools.Method(tm, "RefreshCaches");
            var pPop = AccessTools.Property(cfg.GetType(), "PopulationManager");
            object pop = pPop != null ? pPop.GetValue(cfg, null) : null;
            _mGetPop = pop != null ? AccessTools.Method(pop.GetType(), "GetPopData", new[] { typeof(Settlement) }) : null;
            _popMgr = pop;
            var pRel = AccessTools.Property(tm, "Religions");
            return pRel != null ? pRel.GetValue(_relMgr, null) as System.Collections.IDictionary : null;
        }
        private static object _popMgr;

        private static object HeroReligion(Hero h)
        {
            try { return _mGetHeroRel != null && _relMgr != null ? _mGetHeroRel.Invoke(_relMgr, new object[] { h }) : null; }
            catch { return null; }
        }

        private static bool IsNord(Hero h) { return h != null && h.Culture != null && h.Culture.StringId == "nord"; }

        /// <summary>Pierwsza godzina sesji AM-28: kultura ulubiona wiary Pattern i jednorazowe nadanie wiary bohaterom nord (kampania sprzed naprawy).</summary>
        private static void LorathSession()
        {
            if (!_lorathOn) return;
            try
            {
                var rels = BkReligions();
                if (rels == null) { Scribe.Line("Mends: wiara Lorath (AM-28) - start sesji: brak ReligionsManager BK - nic do zrobienia."); return; }
                var nord = MBObjectManager.Instance.GetObject<CultureObject>("nord");
                int fav = 0;
                string favNow = "?";
                var keys = new List<object>();
                foreach (var k in rels.Keys) keys.Add(k);
                foreach (var rel in keys)
                {
                    var mb = rel as MBObjectBase;
                    if (mb == null || mb.StringId != "pattern") continue;
                    var list = AccessTools.Property(rel.GetType(), "FavoredCultures")?.GetValue(rel, null) as List<CultureObject>;
                    if (list == null) continue;
                    if (nord != null) for (int i = 0; i < list.Count; i++) if (list[i] == null) { list[i] = nord; fav++; }
                    var names = new List<string>();
                    foreach (var c in list) names.Add(c != null ? c.StringId : "null");
                    favNow = "[" + string.Join(", ", names.ToArray()) + "]";
                }
                int with = 0, without = 0, given = 0;
                var heroes = new List<Hero>();
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (!IsNord(h) || h == Hero.MainHero || h.IsChild) continue;
                    if (HeroReligion(h) != null) with++; else { without++; heroes.Add(h); }
                }
                var byRel = new SortedDictionary<string, int>(StringComparer.Ordinal);
                bool run = with == 0 && heroes.Count > 0 && _mInitHeroFaith != null;
                if (run)
                {
                    foreach (var rel in keys)
                    {
                        object faith = null;
                        try { faith = AccessTools.Property(rel.GetType(), "Faith")?.GetValue(rel, null); } catch { }
                        var mNat = faith != null ? AccessTools.Method(faith.GetType(), "IsHeroNaturalFaith", new[] { typeof(Hero) }) : null;
                        if (mNat == null) continue;
                        string rid = rel is MBObjectBase ? ((MBObjectBase)rel).StringId : "?";
                        foreach (var h in heroes)
                        {
                            try
                            {
                                if (HeroReligion(h) != null) continue;
                                if (!(bool)mNat.Invoke(faith, new object[] { h })) continue;
                                var ps = _mInitHeroFaith.GetParameters();
                                _mInitHeroFaith.Invoke(_relMgr, ps.Length >= 2 ? new object[] { h, rel } : new object[] { h });
                                given++;
                                int n; byRel.TryGetValue(rid, out n); byRel[rid] = n + 1;
                            }
                            catch { }
                        }
                    }
                    try { if (_mRefresh != null) _mRefresh.Invoke(_relMgr, null); } catch { }
                }
                var br = new List<string>();
                foreach (var kv in byRel) br.Add(kv.Key + " " + kv.Value);
                Scribe.Line("Mends: wiara Lorath (AM-28) - start sesji: wiara pattern (Lorath) kultury ulubione " + favNow + " (null -> nord: " + fav + ")"
                            + ", bohaterow nord z wiara " + with + ", bez wiary " + without + " | jednorazowe nadanie wiary jak BK: "
                            + (run ? given + " [" + string.Join(", ", br.ToArray()) + "]" : "pominiete (" + (with > 0 ? "bohaterowie nord juz maja wiare" : "brak bohaterow albo metody BK") + ")")
                            + ", trafien postfiksu w procesie " + _lorathHits + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.LorathSession", null); } catch { } }
        }

        /// <summary>Koniec doby AM-28: osady i lordowie kultury nord z wiara BK (sam odczyt).</summary>
        private static void LorathDaily()
        {
            if (!_lorathOn) return;
            try
            {
                if (_relMgr == null && BkReligions() == null) return;
                int st = 0, stFaith = 0, lords = 0, lordsFaith = 0;
                foreach (var s in Settlement.All)
                {
                    if (s == null || s.Culture == null || s.Culture.StringId != "nord" || s.IsHideout) continue;
                    st++;
                    try
                    {
                        object pd = _mGetPop != null ? _mGetPop.Invoke(_popMgr, new object[] { s }) : null;
                        object rd = pd != null ? AccessTools.Property(pd.GetType(), "ReligionData")?.GetValue(pd, null) : null;
                        object dom = rd != null ? AccessTools.Property(rd.GetType(), "DominantReligion")?.GetValue(rd, null) : null;
                        if (dom != null) stFaith++;
                    }
                    catch { }
                }
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (!IsNord(h) || !h.IsLord || h.IsChild) continue;
                    lords++;
                    if (HeroReligion(h) != null) lordsFaith++;
                }
                Scribe.Line("Mends: wiara Lorath (AM-28) doba: osady nord z wiara BK " + stFaith + "/" + st + ", lordowie nord z wiara " + lordsFaith + "/" + lords
                            + ", trafien postfiksu " + _lorathHits + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.LorathDaily", null); } catch { } }
        }

        /// <summary>Typ po pelnej nazwie: najpierw w assembly BKROTPatch, potem wszedzie (AccessTools).</summary>
        private static Type FindType(string fullName, string asm)
        {
            Type t = null;
            try { t = Type.GetType(fullName + ", " + asm, false); } catch { }
            if (t == null) try { t = AccessTools.TypeByName(fullName); } catch { }
            return t;
        }

        /// <summary>Zdejmuje cudzy prefiks ze WSZYSTKICH metod, na ktorych wisi (Harmony.Unpatch po metodzie latki - jak Mends.Unhook
        /// lataczki stylow zycia BK; tu bez nazwy celu, wiec przeciazenia i nazwy parametrow nie maja znaczenia). Zwraca liczbe zdjec.</summary>
        private static int UnhookEverywhere(Harmony harmony, MethodInfo theirPrefix, List<MethodBase> from)
        {
            int n = 0;
            if (harmony == null || theirPrefix == null) return 0;
            foreach (var m in new List<MethodBase>(Harmony.GetAllPatchedMethods()))
            {
                try
                {
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null || info.Prefixes == null) continue;
                    bool has = false;
                    foreach (var p in info.Prefixes) if (p != null && p.PatchMethod == theirPrefix) { has = true; break; }
                    if (!has) continue;
                    harmony.Unpatch(m, theirPrefix);
                    if (from != null) from.Add(m);
                    n++;
                }
                catch { }
            }
            return n;
        }

        /// <summary>Czy prefiks wisi na danej metodzie (Harmony.GetPatchInfo jednej metody - tanio, raz na dobe).</summary>
        private static bool PrefixedOn(MethodBase m, MethodInfo prefix)
        {
            try
            {
                if (m == null || prefix == null) return false;
                var info = Harmony.GetPatchInfo(m);
                if (info == null || info.Prefixes == null) return false;
                foreach (var p in info.Prefixes) if (p != null && p.PatchMethod == prefix) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Metoda, na ktorej wisi dany prefiks (albo null) - przeglad wszystkich latanych metod, tylko przy starcie procesu.</summary>
        private static MethodBase PrefixedWhere(MethodInfo prefix)
        {
            if (prefix == null) return null;
            foreach (var m in Harmony.GetAllPatchedMethods())
            {
                try
                {
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null || info.Prefixes == null) continue;
                    foreach (var p in info.Prefixes) if (p != null && p.PatchMethod == prefix) return m;
                }
                catch { }
            }
            return null;
        }

        // ------------------------------------------------------------------ AM-01: BKROT EarlyHeal (dobrobyt ponizej 100 -> 2000 / 4000)
        // BKROTPatch wiesza 3 prefiksy (Priority 800), ktore przy dobrobycie twierdzy < 100 (albo NaN / Inf) wpisuja 2000 (zamek) / 4000 (miasto):
        // BKROTProsperityHeal_Patch na getterze Town.Prosperity (wyzwala go KAZDY odczyt, takze nasze tabele), VanillaLoyaltyChange_PREFIX_Heal_Patch
        // i VanillaSecurityChange_PREFIX_Heal_Patch (DefaultSettlementLoyaltyModel.CalculateLoyaltyChange / DefaultSettlementSecurityModel.CalculateSecurityChange).
        // Gra przycina dobrobyt tylko ponizej 0 (Town.set_Prosperity), ani gra, ani BK, ani nasze mody nie dziela przez dobrobyt - wiec zamek, ktory
        // naprawde upada (glod, oblezenie, spustoszenie), dostawal +1900 z niczego (RGL: Willow Wood 97.42 -> 2000, Pinkmaiden 99.03 -> 2000).
        // Klucz StopBkrotProsperityReset: 3 prefiksy zdjete przy starcie procesu (po OnSubModuleLoad BKROT), w zamian straz na Town.set_Prosperity:
        // NaN / Inf -> stara wartosc zostaje (licznik). Leczenie lojalnosci i bezpieczenstwa BKROT na getterach (prog < 0, NaN) zostaje.
        // Start sesji: NaN / Inf w zapisie -> 0 (jednorazowo, linia). Koniec doby: czy prefiks BKROT nie wrocil (konsola BKROT BkrotPatchToggle).
        private static readonly string[] HealPatches =
        {
            "BKROTPatch.Patches.BKROTProsperityHeal_Patch",
            "BKROTPatch.Patches.VanillaLoyaltyChange_PREFIX_Heal_Patch",
            "BKROTPatch.Patches.VanillaSecurityChange_PREFIX_Heal_Patch"
        };
        private static readonly MethodInfo[] _healPrefix = new MethodInfo[3];
        private static readonly MethodBase[] _healTarget = new MethodBase[3];   // gdzie wisial (albo cel wedlug nazwy) - kontrola doby tylko tej metody
        private static bool _healOn, _guardHooked;
        private static int _nanBlocked, _nanBlockedDay, _healBackDays;

        private static void ProsperityInstall(Harmony harmony)
        {
            try
            {
                if (!Config.StopBkrotProsperityReset)
                {
                    Scribe.Line("Mends: dobrobyt (AM-01) wylaczone (StopBkrotProsperityReset=false) - BKROT EarlyHeal jak dotad (twierdza ponizej 100 dobrobytu dostaje 2000 / 4000).");
                    return;
                }
                _healOn = true;
                int found = 0, off = 0;
                var parts = new List<string>();
                for (int i = 0; i < HealPatches.Length; i++)
                {
                    string shortName = HealPatches[i].Substring(HealPatches[i].LastIndexOf('.') + 1);
                    var t = FindType(HealPatches[i], "BKROTPatch");
                    var pm = t != null ? AccessTools.Method(t, "Prefix") : null;
                    _healPrefix[i] = pm;
                    if (pm == null) { parts.Add(shortName + " brak"); continue; }
                    found++;
                    var from = new List<MethodBase>();
                    int removed = UnhookEverywhere(harmony, pm, from);
                    _healTarget[i] = from.Count > 0 ? from[0] : HealTargetByName(i);
                    var still = PrefixedWhere(pm);
                    if (still == null) off++;
                    parts.Add(shortName + (still != null ? " NIE ZDJETY (" + still.Name + ")" : (removed > 0 ? " zdjety" : " nie byl wpiety")));
                }
                var setter = AccessTools.PropertySetter(typeof(Town), "Prosperity");
                if (setter != null)
                {
                    harmony.Patch(setter, prefix: new HarmonyMethod(typeof(AuditP3), nameof(ProsperityNanGuard)));
                    _guardHooked = true;
                }
                Scribe.Line("Mends: dobrobyt (AM-01) - " + (found == 0 ? "BKROTPatch bez latek EarlyHeal (nic do zdjecia), " : "")
                            + "BKROT EarlyHeal zdjety " + off + "/" + HealPatches.Length + " (" + string.Join(", ", parts.ToArray()) + "); straz NaN/Inf na Town.set_Prosperity "
                            + (_guardHooked ? "wpieta" : "NIE wpieta") + " (NaN / Inf -> stara wartosc zostaje).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.ProsperityInstall", null); } catch { } }
        }

        /// <summary>Cel prefiksu BKROT wedlug nazwy (gdy przy starcie nie wisial): getter Town.Prosperity, CalculateLoyaltyChange, CalculateSecurityChange.</summary>
        private static MethodBase HealTargetByName(int i)
        {
            try
            {
                if (i == 0) return AccessTools.PropertyGetter(typeof(Town), "Prosperity");
                var t = i == 1 ? typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel)
                               : typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel);
                string name = i == 1 ? "CalculateLoyaltyChange" : "CalculateSecurityChange";
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (m.Name == name) return m;
            }
            catch { }
            return null;
        }

        /// <summary>Straz AM-01 na Town.set_Prosperity: NaN / Inf nie wchodzi (zwraca false = stara wartosc zostaje).</summary>
        public static bool ProsperityNanGuard(float __0)
        {
            if (!float.IsNaN(__0) && !float.IsInfinity(__0)) return true;
            if (!_healOn) return true;
            _nanBlocked++; _nanBlockedDay++;
            return false;
        }

        /// <summary>Start sesji AM-01: NaN / Inf dobrobytu w zapisie -> 0 (bez BKROT nikt by go nie wyleczyl); twierdze ponizej 100 - liczba.</summary>
        private static void ProsperitySession()
        {
            if (!_healOn) return;
            try
            {
                int n = 0, nan = 0, low = 0;
                var lows = new List<string>();
                foreach (var st in Settlement.All)
                {
                    var t = st != null ? st.Town : null;
                    if (t == null) continue;
                    n++;
                    float p = t.Prosperity;
                    if (float.IsNaN(p) || float.IsInfinity(p)) { t.Prosperity = 0f; nan++; continue; }
                    if (p < 100f) { low++; if (lows.Count < 8) lows.Add(st.Name + " " + p.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)); }
                }
                Scribe.Line("Mends: dobrobyt (AM-01) - start sesji: twierdz " + n + ", NaN/Inf w zapisie -> 0: " + nan + ", ponizej 100: " + low
                            + (lows.Count > 0 ? " [" + string.Join(", ", lows.ToArray()) + "]" : "") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.ProsperitySession", null); } catch { } }
        }

        /// <summary>Koniec doby AM-01: licznik NaN, kontrola Harmony.GetPatchInfo (czy prefiks BKROT nie wrocil), twierdze ponizej 100.</summary>
        private static void ProsperityDaily()
        {
            if (!_healOn) return;
            try
            {
                var back = new List<string>();
                for (int i = 0; i < _healPrefix.Length; i++)
                {
                    if (PrefixedOn(_healTarget[i], _healPrefix[i]))
                        back.Add(HealPatches[i].Substring(HealPatches[i].LastIndexOf('.') + 1) + " na " + _healTarget[i].Name);
                }
                if (back.Count > 0) _healBackDays++;
                int low = 0;
                foreach (var st in Settlement.All)
                {
                    var t = st != null ? st.Town : null;
                    if (t != null && t.Prosperity < 100f) low++;
                }
                Scribe.Line("Mends: dobrobyt (AM-01) doba: NaN/Inf zablokowanych " + _nanBlockedDay + " (lacznie " + _nanBlocked + "), prefiks EarlyHeal BKROT "
                            + (back.Count == 0 ? "brak" : "WROCIL: " + string.Join(", ", back.ToArray()) + " (konsola BKROT?)") + ", twierdz ponizej 100: " + low + ".");
                _nanBlockedDay = 0;
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.ProsperityDaily", null); } catch { } }
        }

        // ------------------------------------------------------------------ AM-29: wzorce kultur
        private static readonly string[] TemplateLists = { "LordTemplates", "NotableTemplates", "RebelliousHeroTemplates" };
        private static int _netSwaps, _netMiss;
        private static bool _netHooked;

        private static bool Broken(CharacterObject c)
        {
            try { return c == null || !c.IsReady || c.BodyPropertyRange == null; }
            catch { return true; }
        }

        private static void TemplatesInstall(Harmony harmony)
        {
            try
            {
                if (!Config.CultureTemplateRepair)
                {
                    Scribe.Line("Mends: wzorce kultur (AM-29) wylaczone (CultureTemplateRepair=false) - listy wzorcow ROT jak dotad (zepsute wpisy zostaja).");
                    return;
                }
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.CompanionRolesCampaignBehavior");
                var m = t != null ? AccessTools.Method(t, "CreateNewHeroForNewCompanionClan") : null;
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && ps.Length >= 2 && ps[0].ParameterType == typeof(CharacterObject) && ps[1].ParameterType == typeof(Settlement))
                {
                    harmony.Patch(m, prefix: new HarmonyMethod(typeof(AuditP3), nameof(CompanionTemplateNet)));
                    _netHooked = true;
                }
                Scribe.Line("Mends: wzorce kultur (AM-29) - siatka na rod towarzysza (CreateNewHeroForNewCompanionClan) " + (_netHooked ? "wpieta" : "NIE wpieta (brak metody albo inne parametry)")
                            + "; filtr list wzorcow przy kazdym starcie sesji.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.TemplatesInstall", null); } catch { } }
        }

        /// <summary>Filtr list wzorcow wszystkich kultur. Linia: ile usunietych, z jakich kultur i list; pozostalo zepsutych (cel 0).</summary>
        internal static void TemplatesRepair()
        {
            if (!Config.CultureTemplateRepair) return;
            try
            {
                var cultures = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObjectTypeList<CultureObject>() : null;
                if (cultures == null) { Scribe.Line("Mends: wzorce kultur (AM-29) - brak listy kultur (MBObjectManager)."); return; }
                int removed = 0, keptEmpty = 0, leftBroken = 0, stumbles = 0, checkedN = 0;
                var parts = new List<string>();
                foreach (var cu in cultures)
                {
                    if (cu == null) continue;
                    foreach (var name in TemplateLists)
                    {
                        try
                        {
                            var prop = AccessTools.Property(typeof(CultureObject), name);
                            var setter = AccessTools.PropertySetter(typeof(CultureObject), name);
                            var list = prop != null ? prop.GetValue(cu, null) as MBReadOnlyList<CharacterObject> : null;
                            if (list == null || setter == null) continue;
                            checkedN += list.Count;
                            var good = new MBList<CharacterObject>();
                            var bad = new List<string>();
                            foreach (var c in list)
                            {
                                if (Broken(c)) bad.Add(c != null ? c.StringId : "null");
                                else good.Add(c);
                            }
                            if (bad.Count == 0) continue;
                            string kind = name == "LordTemplates" ? "lord" : (name == "NotableTemplates" ? "notable" : "rebel");
                            if (good.Count == 0)
                            {
                                keptEmpty++; leftBroken += bad.Count;
                                parts.Add(cu.StringId + " " + kind + " " + list.Count + " - zostawiona (same zepsute: " + string.Join(", ", bad.ToArray()) + ")");
                                continue;
                            }
                            setter.Invoke(cu, new object[] { good });
                            removed += bad.Count;
                            parts.Add(cu.StringId + " " + kind + " " + list.Count + "->" + good.Count + " [" + string.Join(", ", bad.ToArray()) + "]");
                        }
                        catch (Exception e)
                        {
                            stumbles++;
                            if (stumbles == 1) try { Scribe.Report("CrashScribe", e, "AuditP3.TemplatesRepair(" + cu.StringId + "." + name + ")", null); } catch { }
                        }
                    }
                }
                Scribe.Line("Mends: wzorce kultur (AM-29) - start sesji: wzorcow sprawdzonych " + checkedN + ", usunietych " + removed
                            + (parts.Count > 0 ? ": " + string.Join(" | ", parts.ToArray()) : "")
                            + "; list zostawionych bez zmian (same zepsute) " + keptEmpty + ", pozostalo zepsutych " + leftBroken
                            + ", siatka rodu towarzysza " + (_netHooked ? "wpieta" : "NIE") + " (podmian w procesie " + _netSwaps + ", bez zastepcy " + _netMiss + ")"
                            + ", potkniec " + stumbles + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.TemplatesRepair", null); } catch { } }
        }

        /// <summary>Siatka AM-29: zepsuty wzorzec przy tworzeniu bohatera rodu towarzysza -> gotowy wzorzec lorda tej samej kultury
        /// (kultura, ktorej lista go zawiera; inaczej kultura osady; inaczej pierwsza kultura z gotowym wzorcem lorda).</summary>
        public static void CompanionTemplateNet(ref CharacterObject __0, Settlement __1)
        {
            try
            {
                if (!Config.CultureTemplateRepair || !Broken(__0)) return;
                string was = __0 != null ? __0.StringId : "null";
                CharacterObject pick = null;
                CultureObject home = null;
                var cultures = MBObjectManager.Instance.GetObjectTypeList<CultureObject>();
                if (__0 != null)
                    foreach (var cu in cultures)
                    {
                        var lt = cu != null ? cu.LordTemplates : null;
                        if (lt != null && lt.Contains(__0)) { home = cu; break; }
                    }
                if (home == null && __1 != null) home = __1.Culture;
                pick = ReadyLord(home);
                if (pick == null)
                    foreach (var cu in cultures) { pick = ReadyLord(cu); if (pick != null) break; }
                if (pick == null) { _netMiss++; Scribe.Line("Mends: wzorce kultur (AM-29) - siatka: zepsuty wzorzec " + was + " przy rodzie towarzysza, brak gotowego zastepcy (gra moze sie wywrocic)."); return; }
                __0 = pick;
                _netSwaps++;
                Scribe.Line("Mends: wzorce kultur (AM-29) - siatka: zepsuty wzorzec " + was + " przy rodzie towarzysza (" + (__1 != null ? __1.Name.ToString() : "?")
                            + ") -> " + pick.StringId + " (" + (home != null ? home.StringId : "inna kultura") + "), lacznie " + _netSwaps + ".");
            }
            catch { }
        }

        private static CharacterObject ReadyLord(CultureObject cu)
        {
            try
            {
                var lt = cu != null ? cu.LordTemplates : null;
                if (lt == null) return null;
                var ok = new List<CharacterObject>();
                foreach (var c in lt) if (!Broken(c)) ok.Add(c);
                return ok.Count > 0 ? ok[MBRandom.RandomInt(ok.Count)] : null;
            }
            catch { return null; }
        }
    }

    /// <summary>P3 audytu 14.10: zdarzenia kampanii (start sesji).</summary>
    internal sealed class AuditP3Behavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { AuditP3.OnSession(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { AuditP3.OnDaily(); });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { AuditP3.OnHourly(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
